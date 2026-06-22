' Version Uploaded of Fo4Library 3.2.0
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics


Public Class Floor_Shader_Class
    Inherits Shader_Base_Class
    Private Const Vertex_Floor As String =
"#version 430
layout(location = 0) in vec3 vertexPosition;

uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;

void main()
{
    gl_Position = matProjection * matView * matModel * vec4(vertexPosition, 1.0);
}"

    Private Const Fragment_Floor As String =
"#version 430
uniform vec3 gridColor;
out vec4 FragColor;

void main()
{
    FragColor = vec4(gridColor, 1.0);
}"
    Sub New()
        MyBase.New(Vertex_Floor, Fragment_Floor)
    End Sub
End Class

Public Class Shader_Class_Fo4
    Inherits Shader_Base_Class
    Private Const Vertex_FO4 As String = "
#version 430
uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;
uniform mat4 matModelView;
uniform mat3 mv_normalMatrix;
uniform vec3 color;
uniform vec3 subColor;

uniform bool bModelSpace;   // Model Space Normals: needs the object->view matrix in the VS (MSN CPU-skin path)
uniform bool bShowTexture;
uniform bool bShowMask;
uniform bool bShowWeight;
uniform bool bShowVertexColor;
uniform bool bShowVertexAlpha;
uniform bool bApplyZap;

uniform bool bWireframe;

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec3 vertexNormal;
layout(location = 2) in vec3 vertexTangent;
layout(location = 3) in vec3 vertexBitangent;
layout(location = 4) in vec3 vertexColors;
layout(location = 5) in float vertexAlpha;
layout(location = 6) in vec2 vertexUV;
layout(location = 7) in float vertexMask;
layout(location = 8) in float vertexWeight;
layout(location = 9) in vec4 boneIndicesF;   // bone palette indices as float (cast to int in shader)
layout(location = 10) in vec4 boneWeightsIn; // normalized bone weights

layout(std430, binding = 0) buffer BoneMatrices {
    mat4 bones[];
};
uniform bool bGPUSkinning;
uniform int uBoneCount;
// GPU SKINNING - SYNC CONTRACT
// The blend formula here MUST match SkinningHelper.BlendBoneMatrices()
// (CPU double-precision) and RecomputeGPUBoneMatrices() (bone matrix
// composition). Any change to weights, fallback, or matrix composition
// must be mirrored in all three locations:
//   1. This shader (Shader_Class_FO4 vertex + Shader_Class_SSE vertex)
//   2. SkinningHelper.BlendBoneMatrices()
//   3. SkinningHelper.RecomputeGPUBoneMatrices()
//   4. SkinningHelper.ExtractSkinnedGeometry() (GPU arrays extraction)
// Differences by design:
//   - GPU: float precision, pre-normalized weights (sum=1 at extract)
//   - CPU: double precision, runtime normalization (1/sumW)
//   - GPU applies transpose(inverse(mat3)) for N/T/B; CPU stores
//     N/T/B in local space and lets the shader transform them.
// This block is DUPLICATED in Shader_Class_FO4 and Shader_Class_SSE.

struct DirectionalLight
{
	vec3 diffuse;
	vec3 direction;
};

uniform DirectionalLight frontal;
uniform DirectionalLight directional0;
uniform DirectionalLight directional1;
uniform DirectionalLight directional2;

out vec3 lightFrontal;
out vec3 lightDirectional0;
out vec3 lightDirectional1;
out vec3 lightDirectional2;

out vec3 viewDir;
out mat3 mv_tbn;
out mat3 v_msnMatrix;   // MSN: object->view normal matrix (skinning+view), used by the Fragment MSN branch

out float maskFactor;
flat out int ZappedVert;
out vec3 weightColor;

out vec4 vColor;
out vec2 vUV;

vec3 colorRamp(in float value)
{
	float r;
	float g;
	float b;

	if (value <= 0.0f)
	{
		r = g = b = 1.0;
	}
	else if (value <= 0.25)
	{
		r = 0.0;
		b = 1.0;
		g = value / 0.25;
	}
	else if (value <= 0.5)
	{
		r = 0.0;
		g = 1.0;
		b = 1.0 + (-1.0) * (value - 0.25) / 0.25;
	}
	else if (value <= 0.75)
	{
		r = (value - 0.5) / 0.25;
		g = 1.0;
		b = 0.0;
	}
	else
	{
		r = 1.0;
		g = 1.0 + (-1.0) * (value - 0.75) / 0.25;
		b = 0.0;
	}

	return vec3(r, g, b);
}

void main(void)
{
	// Initialization
	maskFactor = 1.0;
    ZappedVert = 0;
    if (bApplyZap)
    {
     if (vertexMask<0)
      ZappedVert = 1;
    }
	if (bShowMask)
	{
		maskFactor = 1.0 - vertexMask / 1.5;

    if (ZappedVert==1) //zapped
        {
    		maskFactor = 1.0 - (-vertexMask) / 1.5;
        }

   	}
	weightColor = vec3(1.0, 1.0, 1.0);
	vColor = vec4(1.0, 1.0, 1.0, 1.0);
	vUV = vertexUV;

	if (bShowVertexColor)
	{
		vColor.rgb = vertexColors;
	}

	if (bShowVertexAlpha)
	{
		vColor.a = vertexAlpha;
	}

	// GPU Skinning
	vec3 skinnedPos;
	vec3 skinnedNormal;
	vec3 skinnedTangent;
	vec3 skinnedBitangent;

	if (bGPUSkinning) {
	    // GPU skinning: blend bone matrices
	    ivec4 bIdx = clamp(ivec4(boneIndicesF), ivec4(0), ivec4(max(uBoneCount - 1, 0)));
	    vec4 bWgt = boneWeightsIn;

	    mat4 skinMatrix = mat4(0.0);
	    // Accumulate weighted bone matrices
	    if (bWgt.x > 0.0) skinMatrix += bones[bIdx.x] * bWgt.x;
	    if (bWgt.y > 0.0) skinMatrix += bones[bIdx.y] * bWgt.y;
	    if (bWgt.z > 0.0) skinMatrix += bones[bIdx.z] * bWgt.z;
	    if (bWgt.w > 0.0) skinMatrix += bones[bIdx.w] * bWgt.w;

	    // Zero-weight fallback: first bone (matches CPU BlendBoneMatrices), then identity if no bones
	    float totalWeight = bWgt.x + bWgt.y + bWgt.z + bWgt.w;
	    if (totalWeight < 0.001) skinMatrix = (uBoneCount > 0) ? bones[bIdx.x] : mat4(1.0);

	    skinnedPos = vec3(skinMatrix * vec4(vertexPosition, 1.0));

	    // Correct normal matrix: transpose of inverse of upper-left 3x3
	    mat3 skinNormalMat = transpose(inverse(mat3(skinMatrix)));
	    skinnedNormal = normalize(skinNormalMat * vertexNormal);
	    skinnedTangent = normalize(skinNormalMat * vertexTangent);
	    skinnedBitangent = normalize(skinNormalMat * vertexBitangent);
	    // MSN: object->view normal matrix = (model->view normal) * (object->world skin normal matrix)
	    v_msnMatrix = mv_normalMatrix * skinNormalMat;
	} else {
	    // CPU skinning fallback: vertices already in world space
	    skinnedPos = vertexPosition;
	    skinnedNormal = vertexNormal;
	    skinnedTangent = vertexTangent;
	    skinnedBitangent = vertexBitangent;
	    if (bModelSpace) {
	        // CPU + MSN: the N/T/B VBOs carry the object->world normal matrix columns (Render.vb packs
	        // nm3.Row0/1/2 there for MSN shapes) -> rebuild and combine with model->view.
	        v_msnMatrix = mv_normalMatrix * mat3(vertexNormal, vertexTangent, vertexBitangent);
	    } else {
	        v_msnMatrix = mv_normalMatrix;
	    }
	}

	// Eye-coordinate position of vertex (now using skinned position)
	vec3 vPos = vec3(matModelView * vec4(skinnedPos, 1.0));
	gl_Position = matProjection * vec4(vPos, 1.0);

	// TBN in view space
	vec3 mv_normal = mv_normalMatrix * skinnedNormal;
	vec3 mv_tangent = mv_normalMatrix * skinnedTangent;
	vec3 mv_bitangent = mv_normalMatrix * skinnedBitangent;

    mv_tbn = mat3(mv_tangent.x,   mv_tangent.y,   mv_tangent.z,
              mv_bitangent.x, mv_bitangent.y, mv_bitangent.z,
              mv_normal.x,    mv_normal.y,    mv_normal.z);

	viewDir = normalize(-vPos);
	lightFrontal = normalize(mat3(matView) * frontal.direction);
	lightDirectional0 = normalize(mat3(matView) * directional0.direction);
	lightDirectional1 = normalize(mat3(matView) * directional1.direction);
	lightDirectional2 = normalize(mat3(matView) * directional2.direction);

	if (!bShowTexture || bWireframe)
	{
		vColor *= clamp(vec4(color, 1.0), 0.0, 1.0);
	}

	if (!bWireframe)
	{
		vColor.rgb *= subColor;

		if (bShowWeight)
		{
			weightColor = colorRamp(vertexWeight);
		}
	}
}
"
    Private Const Fragment_FO4 As String = "
#version 430

/*
 * BodySlide and Outfit Studio
 * Shaders by jonwd7 and ousnius
 * https://github.com/ousnius/BodySlide-and-Outfit-Studio
 * http://www.niftools.org/
 * Modified By Manolo For WardrobeManager
 */

uniform sampler2D texDiffuse;
uniform sampler2D texNormal;
uniform samplerCube texCubemap;
uniform sampler2D texEnvMask;
uniform sampler2D texSpecular;
uniform sampler2D texGreyscale;
uniform sampler2D texGlowmap;
uniform sampler2D texFaceTintOverlay;   // TETI/TEND composed tint layers, blended on top of diffuse

uniform bool bLightEnabled;
uniform bool bShowTexture;
uniform bool bShowMask;
uniform bool bShowWeight;
uniform bool bWireframe;
uniform bool bApplyZap;

uniform bool bNormalMap;
uniform bool bModelSpace;
uniform bool bCubemap;
uniform bool bEnvMap;
uniform bool bEnvMask;
uniform bool bSpecular;
uniform bool bEmissive;
uniform bool bBacklight;
uniform bool bRimlight;
uniform bool bSoftlight;
uniform bool bAlphaTest;
uniform bool bGlowmap;
uniform bool bGreyscaleColor;
uniform bool bDoubleSided;
uniform bool bHide;
uniform bool bHasFaceTintOverlay;       // true when composed face tint texture is bound

uniform bool bIsEffectShader;
uniform bool bDecal;
uniform int shaderType;
uniform bool bEffectFalloff;
uniform bool bEffectFalloffColor;
uniform bool bEffectGreyscaleAlpha;
uniform float effectLightingInfluence;
uniform vec4 effectFalloffParams;   // x=startAngle, y=stopAngle, z=startOpacity, w=stopOpacity
uniform vec3 effectBaseColor;
uniform float effectBaseColorAlpha;
uniform float effectBaseColorScale;

uniform mat4 matModel;
uniform mat4 matModelViewInverse;
uniform mat3 mv_normalMatrix;
uniform float DebugMode;

uniform	vec2 uvOffset;
uniform vec2 uvScale;
uniform	vec3 specularColor;
uniform	float specularStrength;
uniform	float shininess;
uniform float glossiness;
uniform float envReflection;
uniform vec3 emissiveColor;
uniform float emissiveMultiple;
uniform float alpha;
uniform float backlightPower;
uniform float rimlightPower;
uniform	float subsurfaceRolloff;
uniform	float fresnelPower;
uniform float paletteScale;
uniform float WireAlpha;

uniform float alphaThreshold;

uniform vec3 ambientSky;       // hemispheric ambient: color when N points world-up (+Z)
uniform vec3 ambientGround;    // hemispheric ambient: color when N points world-down (-Z)
uniform bool bHasTintColor;
uniform vec3 tintColor;

// Engine-faithful FO4 path (Fallout4.exe). This fragment is FO4-only (Skyrim uses Fragment_SSE),
// so the engine path is unconditional here -- no runtime flag.
uniform bool bDiffuseIsColor;   // diffuse slot is a color texture (sRGB), not greyscale/data
uniform int uEffectiveType;     // 0 Default,1 Envmap,2 Glowmap,3 Face,4 SkinTint,5 HairTint,6 Eye
uniform bool bHair;             // hair material (Hair flag) -- robust vs the Glowmap type override
uniform bool bHasGlowTex;       // glow-slot texture bound (for hair this is the _f strand FLOW map)
uniform bool bShowVertexColor;  // mesh has authored vertex colors AND the toggle is on (gates the BGEM vertex blend)
uniform float skinTintStrength; // SkinTint soft-light strength = skin tone .w (engine material+0xCC); default 1.0
uniform bool bHasAlphaBlend;    // material renders alpha-blended (forward b6) vs opaque (deferred). Gates the strong forward-b6 material-cube envmap.

struct DirectionalLight
{
	vec3 diffuse;
	vec3 direction;
};

uniform DirectionalLight frontal;
uniform DirectionalLight directional0;
uniform DirectionalLight directional1;
uniform DirectionalLight directional2;

in vec3 lightFrontal;
in vec3 lightDirectional0;
in vec3 lightDirectional1;
in vec3 lightDirectional2;

in vec3 viewDir;
in mat3 mv_tbn;
in mat3 v_msnMatrix;   // MSN: object->view normal matrix from the vertex shader

in float maskFactor;
flat in int ZappedVert;
in vec3 weightColor;

in vec4 vColor;
in vec2 vUV;

out vec4 fragColor;

vec3 normal = vec3(0.0);
float specGloss = 1.0;
float specFactor = 1.0;

vec2 uv = vec2(0.0);
vec3 albedo = vec3(0.0);
vec3 emissive = vec3(0.0);
vec3 backlightEmissive = vec3(0.0);

vec4 baseMap = vec4(0.0);
vec4 normalMap = vec4(0.0);
vec4 specMap = vec4(0.0);
vec4 envMask = vec4(0.0);

#ifndef M_PI
	#define M_PI 3.1415926535897932384626433832795
#endif

#define FLT_EPSILON 1.192092896e-07F // smallest such that 1.0 + FLT_EPSILON != 1.0

// FO4 diffuse BRDF: the SIMPLIFIED Oren-Nayar the GAME actually uses (Fallout4.exe forward
// rec1498 L114-129, byte-identical to deferred lighting rec3072 L106-125). The C1 constant is
// 0.57 (NOT the 0.33 of the full NifSkope/BodySlide model); C2 = 0.45*r2/(r2+0.09); there is
// NO C3 lobe and NO L2 interreflection. roughness = 1 - Smoothness is a per-MATERIAL constant
// (the spec map drives only the highlight power, not this). Faithful to the engine -- the prior
// full Oren-Nayar (0.33 + C3 + L2 retroreflection) was the NifSkope deviation, not Fallout 4.
//   gamma = projV.projL = LdotV - NdotL*NdotV   (engine derives it from the projected vectors)
//   diff  = max(NdotL,0) * (C1 + C2*max(gamma,0)*sinV*sinL/max(NdotV,NdotL))
float OrenNayarFO4(vec3 L, vec3 V, vec3 N, float roughness, float NdotL)
{
	float NdotV = dot(N, V);
	float r2 = roughness * roughness;
	float C1 = 1.0 - 0.5 * (r2 / (r2 + 0.57));
	float C2 = 0.45 * (r2 / (r2 + 0.09));
	float gamma = dot(L, V) - NdotL * NdotV;
	float sinVL = sqrt(clamp((1.0 - NdotV * NdotV) * (1.0 - NdotL * NdotL), 0.0, 1.0));
	float denom = max(NdotV, NdotL);
	float azimuth = C2 * max(gamma, 0.0) * sinVL / denom;
	return max(NdotL, 0.0) * (C1 + azimuth);
}

// Schlick's Fresnel approximation
float fresnelSchlick(float VdotH, float F0)
{
	float base = 1.0 - VdotH;
	float exp = pow(base, 5.0);  // engine g6: fixed Schlick exponent 5 (Fallout4.exe g6_PS, fresnelPower ignored)
	return clamp(exp + F0 * (1.0 - exp), 0.0, 1.0);
}

// The Torrance-Sparrow visibility factor, G
float VisibDiv(float NdotL, float NdotV, float VdotH, float NdotH)
{
	float denom = max(VdotH, FLT_EPSILON);
	float numL = min(NdotV, NdotL);
	float numR = 2.0 * NdotH;
	if (denom >= (numL * numR))
	{
		numL = (numL == NdotV) ? 1.0 : (NdotL / NdotV);
		return (numL * numR) / denom;
	}
	return 1.0 / NdotV;
}

// this is a normalized Phong model used in the Torrance-Sparrow model
vec3 TorranceSparrow(float NdotL, float NdotH, float NdotV, float VdotH, vec3 color, float power, float F0)
{
	// D: Normalized phong model
	float D = ((power + 2.0) / (2.0 * M_PI)) * pow(NdotH, power);

	// G: Torrance-Sparrow visibility term divided by NdotV
	float G_NdotV = VisibDiv(NdotL, NdotV, VdotH, NdotH);

	// F: Schlick's approximation
	float F = fresnelSchlick(VdotH, F0);

	// Torrance-Sparrow:
	// (F * G * D) / (4 * NdotL * NdotV)
	// Division by NdotV is done in VisibDiv()
	// and division by NdotL is removed since
	// outgoing radiance is determined by:
	// BRDF * NdotL * L()
	float spec = (F * G_NdotV * D) / 4.0;

	return color * spec * M_PI;
}

vec3 tonemap(in vec3 x)
{
	const float A = 0.15;
	const float B = 0.50;
	const float C = 0.10;
	const float D = 0.20;
	const float E = 0.02;
	const float F = 0.30;

	return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

void directionalLight(in DirectionalLight light, in vec3 lightDir, inout vec3 outDiffuse, inout vec3 outSpec)
{
	vec3 halfDir = normalize(lightDir + viewDir);
	float NdotL = dot(normal, lightDir);
	float NdotL0 = max(NdotL, FLT_EPSILON);
	float NdotH = max(dot(normal, halfDir), FLT_EPSILON);
	float NdotV = max(dot(normal, viewDir), FLT_EPSILON);
	float VdotH = max(dot(viewDir, halfDir), FLT_EPSILON);

	// Specularity
	float smoothness = 1.0;
	float roughness = clamp(1.0 - shininess, 0.0, 1.0);   // 3b engine: diffuse roughness = 1 - Smoothness (constant; spec map drives only the highlight power, rec1498 L108)
	float specMask = 1.0;
	if (bSpecular && bShowTexture)
	{
		smoothness = specGloss * shininess;
		// (roughness stays the constant 1 - Smoothness from above; engine does NOT per-pixel it)
		float fSpecularPower = exp2(smoothness * 10.0 + 1.0);
		specMask = specFactor * specularStrength;

		if (bHair && bHasGlowTex)
		{
			// HAIR anisotropic specular: 2-lobe Kajiya-Kay (FO4 deferred lighting rec3110;
			// [HairLighting] GameSettings, lane-resolved: lobe1 scale 1.2 / exp 160 / shift -0.4,
			// lobe2 scale 0.02 / exp 125 / shift 0.36, lobe2 tinted by light color). The deferred
			// saturate/min/diffuse-buffer coupling is approximated by *NdotL0 in this forward pass.
			// GATE = Hair flag AND the _f FLOW map present (glow slot). VERIFIED data-driven: the
			// engine writes matID-hair (-> KK) IFF the prepass samples t3 (the flow): 21/21 matID-hair
			// permutations have t3, and the hairline (Hair + Palette recolor, no flow, prepass rec2626)
			// writes matID DEFAULT -> regular lighting + recolored albedo, NO Kajiya-Kay.
			// T = strand direction from the hair _f FLOW MAP (glow slot = texGlowmap); the engine
			// prepass rec2653 samples it as t3 (*2-1). NO fallback: the engine samples the flow
			// unconditionally and FO4 hair always ships the _f -> without it, the regular specular
			// below applies (no anisotropic hair spec), the no-flow-texture case.
			vec3 Tk = normalize(mv_tbn * (texture(texGlowmap, uv).rgb * 2.0 - 1.0));
			float TdotL = dot(Tk, lightDir);
			float TdotV = dot(Tk, viewDir);
			float sinTL = sqrt(max(1.0 - TdotL * TdotL, 0.0));
			float sinTV = sqrt(max(1.0 - TdotV * TdotV, 0.0));
			float a1 = -(TdotL * cos(-0.4) + sinTL * sin(-0.4));
			float k1 = max(a1 * TdotV + sinTV * sqrt(max(1.0 - a1 * a1, 0.0)), 0.0);
			float a2 = -(TdotL * cos(0.36) + sinTL * sin(0.36));
			float k2 = max(a2 * TdotV + sinTV * sqrt(max(1.0 - a2 * a2, 0.0)), 0.0);
			outSpec += (1.2 * pow(k1, 160.0) + 0.02 * pow(k2, 125.0)) * specMask * NdotL0 * light.diffuse;
		}
		else
		{
			outSpec += TorranceSparrow(NdotL0, NdotH, NdotV, VdotH, vec3(specMask), fSpecularPower, 0.2) * NdotL0 * light.diffuse * specularColor;
			// (Removed the NifSkope ambient*Schlick(0.2)*(1-NdotV) spec rim: the engine forward highlight
			// is ONLY Torrance-Sparrow with the constant Schlick F0=0.2/exp5 (rec1498 L160-189 / rec1507
			// L181-186). Ambient (cb2[3].yzw) is added to the DIFFUSE accumulator, never to specular.)
		}
	}

	// 3a Back lighting: engine thin-rim translucency (rec1498 L131-141), ALWAYS on (no authored
	// BackLightPower gate), added to the DIFFUSE accumulator (-> *albedo in the composite).
	// Roughness-gated by the smoothness sigmoid -> ~0 on smooth materials (metal), visible on
	// rough ones (cloth/skin) at the light terminator. smoothness = specGloss*Smoothness (per-pixel).
	// Engine rec1498 L137: the rim term is sat(V.-L) -- the dot of the VIEW dir with the negated
	// light dir (NOT N.-L). App convention matches the engine: viewDir = surface->eye (= engine V,
	// rec1498 L155 half = V+L), lightDir = surface->light (= engine cb2[0], N.L = NdotL), both in
	// view space -> dot(viewDir,-lightDir) reproduces the engine's sat(V.-L) sign-for-sign.
	{
		float blSatNdotV = clamp(dot(normal, viewDir), 0.0, 1.0);
		float blRim = pow(max(1.0 - blSatNdotV, 0.0), 0.01);
		float blBackV = clamp(dot(viewDir, -lightDir), 0.0, 1.0);
		float blSig = 3.0 - 3.0 / (1.0 + exp2(8.655910 * (1.0 - 2.0 * smoothness)));
		outDiffuse += blRim * blBackV * clamp(NdotL, 0.0, 1.0) * blSig * light.diffuse;
	}

	// Diffuse
	vec3 diff = vec3(OrenNayarFO4(lightDir, viewDir, normal, roughness, NdotL0));
	outDiffuse += diff * light.diffuse;

	// Soft Lighting -- engine subsurface rolloff (cb1[7].x). DXBC g6_PS_Default:147-152 subtracts
	// saturate(NdotL), NOT the OrenNayar term (verified in the asm).
	if (bSoftlight)
	{
		float wrapR = clamp((NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff), 0.0, 1.0);
		outDiffuse += clamp(wrapR - clamp(NdotL, 0.0, 1.0), 0.0, 1.0) * albedo * light.diffuse;
	}

	// NO authored back-translucency (-NdotL * BackLightPower) term: VERIFIED that the FO4 engine renders
	// NO such transmission -- the always-on RIM above (sat(V*-L)*pow(1-NdotV,0.01)*sigmoid) is the ONLY
	// backlight, in BOTH paths (forward 18/18 b06 and deferred rec3110 each have exactly ONE dot with
	// -lightDir = the rim; none a normal*-L*BackLightPower transmission). The old term here was an SSE/
	// NifSkope port (SSE's backlight is texture-based: sat(-NdotL)*backlightTex*lightColor, the SSE path).
	// Removed 06-20 to be FO4-engine-faithful (rim only).
}

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
}

// Hemispheric ambient = engine-faithful STRUCTURE: FO4/SSE light the ambient as a normal-dependent
// term (DirectionalAmbient . vec4(N,1)), NOT a flat scalar. We have no cell ambient matrix, so we
// synthesize it from two preview colors: sky from world-up (+Z), ground from world-down (-Z). The
// shading normal is view-space; transform to world (reusing the envmap matrices) and blend by its
// up (Z) component. Anchored to world up so the hemisphere stays put as the camera orbits.
vec3 hemiAmbient(in vec3 nrm)
{
	vec3 nWS = normalize(vec3(matModel * (matModelViewInverse * vec4(nrm, 0.0))));
	return mix(ambientGround, ambientSky, clamp(nWS.z * 0.5 + 0.5, 0.0, 1.0));
}

void main(void)
{
    uv = vUV * uvScale + uvOffset;
	vec4 color = vColor;
	// vColor RGB -> LINEAR (pow 2.2) before the lit-albedo multiply. The FO4 engine ALWAYS gamma-decodes
	// the vertex color: BGSM does it in the VERTEX shader (forward rec1481 + deferred rec2288, both
	// L119-121: o = pow(COLOR0,2.2)) and the PS multiplies that linear value; BGEM does it in the PS
	// (rec1083 base*=pow(vColor,2.2), its VS rec0260 L46 passes vColor raw). NET for both = albedo *
	// pow(vColor,2.2). The old raw-vColor here (BGSM-crudo) was a misread: the PS not re-powing it
	// did NOT mean raw, because the VS had already decoded it. Universal (NOT tree-gated -- Tree was
	// just one BGSM with non-white vColor). RGB only; vColor.a (color.a) stays raw for the alpha-test
	// (the VS decodes rgb only: o.w = vColor.w). White verts (=1) -> pow=1 -> no change.
	albedo = pow(max(vColor.rgb, 0.0), vec3(2.2));
	vec3 outDiffuse = vec3(0.0);
	vec3 outSpecular = vec3(0.0);

	if (!bWireframe)
	{
		if (bShowTexture)
		{
			// Diffuse Texture
			baseMap = texture(texDiffuse, uv);
			color.a *= baseMap.a;
			vec3 diffRgb = baseMap.rgb;

			// FaceTint overlay (TETI/TEND tint layers, premultiplied-over). The engine bakes the
			// whole face into ONE diffuse and samples it sRGB once; the app splits it into
			// diffuse + overlay. For color-space consistency, composite the overlay in the
			// texture NATIVE space (G22) and decode the COMBINED result once (C1) -- matching the
			// engine. Legacy path keeps the original order (overlay over the lit-space albedo).
			if (bHasFaceTintOverlay)
			{
				vec4 ov = texture(texFaceTintOverlay, uv);
				diffRgb = diffRgb * (1.0 - ov.a) + ov.rgb;
			}
			if (bDiffuseIsColor) diffRgb = diffRgb; //pow(diffRgb, vec3(2.2))   // C1: sRGB/G22 -> linear, combined
			albedo *= diffRgb;

			// Diffuse texture without lighting
			color.rgb = albedo;

			if (bLightEnabled)
			{
				if (bNormalMap)
				{
					normalMap = texture(texNormal, uv);
				}

				if (bSpecular)
				{
					// FO4 dedicated specular map is independent from the normal map.
					specMap = texture(texSpecular, uv);
					specGloss = specMap.g;
					specFactor = specMap.r;
				}

				if (bCubemap)
				{
					if (bEnvMask && !bGlowmap)
					{
						// Environment Mask (BGSM slot 5 is dual: envmask when !bGlowmap)
						envMask = texture(texEnvMask, uv);
					}
				}
			}
		}

		if (bLightEnabled)
		{
			// Lighting with or without textures
			outDiffuse = vec3(0.0);
			outSpecular = vec3(0.0);

			// Start off neutral (for MSN shapes mv_tbn is degenerate -> use the object->view matrix)
			if (bModelSpace)
				normal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
			else
				normal = normalize(mv_tbn * vec3(0.0, 0.0, 0.5));

			if (bShowTexture)
			{
				if (bNormalMap)
				{
					if (bModelSpace)
					{
						// Model Space Normals: the normal map stores an OBJECT-space normal (all 3 channels,
						// no z-reconstruction), transformed by the object->view normal matrix (v_msnMatrix,
						// built in the VS). FO4 engine convention (prepass VS rec2215 -> PS rec2698): the VS
						// passes the object->view matrix rows in v1/v2/v3 and the PS reorders the sampled
						// (R,G,B)=(X,Z,Y) -> .rbg before the transform (same NIF object-space convention as SSE).
						normal = normalize(v_msnMatrix * (normalMap.rbg * 2.0 - 1.0));
					}
					else
					{
						normal = (normalMap.rgb * 2.0 - 1.0);

						// Calculate missing blue channel
						normal.b = sqrt(1.0 - dot(normal.rg, normal.rg));

						// Tangent space map
						normal = normalize(mv_tbn * normal);
					}
				}

				if (bGreyscaleColor && !bIsEffectShader)
				{
                    // FO4 grayscale-to-palette RECOLOR, reconstructed EXACT from the GAME deferred
                    // prepass (Shaders011.fxp b09 rec2985/rec2963):
                    //   U = pow(diffuse.green, 1/2.2)   (index re-encoded to gamma; log/mul 0.454545/exp)
                    //   V = PaletteScale (GrayscaleToPaletteScale material value), and WHEN the mesh has
                    //       vertex colors the engine ADDS a per-vertex offset: V = PaletteScale - 1 + vColor.r
                    //       with vColor.r RAW (NOT gamma-encoded): the prepass VS rec2389 L119-121 gamma-DECODES
                    //       the vertex color (o6 = pow(COLOR0,2.2)) and the PS rec2963 L63-65 re-ENCODES it
                    //       (pow(v6.x,1/2.2)); the two CANCEL -> net = raw vertex red. (A pow(vColor.r,1/2.2)
                    //       here was an extra encode the engine does not have -> wrong palette row on
                    //       non-white verts, e.g. Mr Handy arms went blue instead of gray.) White verts
                    //       (vColor.r=1 -> +0) -> exactly PaletteScale (rec2985, no-vColor perm).
                    //   palette = sample_l(LUT, U,V) lod0; sRGB-authored -> pow(2.2) decode to linear.
                    float palU = pow(max(baseMap.g, 0.0), 1.0/2.2);
                    float palV = paletteScale + (bShowVertexColor ? max(vColor.r, 0.0) - 1.0 : 0.0);
                    vec4 luG = colorLookup(palU, palV);
					albedo = luG.rgb;
					albedo = pow(max(albedo, vec3(0.0)), vec3(2.2));
				}
			}

			// Double-sided: flip normal for back faces
			if (bDoubleSided && !gl_FrontFacing)
			{
				normal = -normal;
			}

			// Engine skin tint = the DEFERRED path the body actually renders through
			// (opaque -> prepass). Verified at the byte: SetupMaterial SkinTint (0x142233168) writes
			// pow(skinTone.rgb,2.2) to the prepass tint cbuffer .xyz and material+0xCC (raw) to .w; the
			// prepass rec2804 (matID=5) does a W3C/Photoshop SOFT-LIGHT of that tint over the body
			// diffuse in DISPLAY space, then lerp(diffuse, result, strength). strength = skinTintStrength
			// (= the tone .w / app SkinTintAlpha, default 1.0). tintColor here is already pow(skinTone,2.2)
			// = linear, so pow(.,1/2.2) recovers the DISPLAY tone. (The old forward g6 curve
			// a^2 + 2a*tint*(1-a) matched at tint=0/0.5 but diverged at bright tones -> sqrt(a) vs 2a-a^2.)
			if (uEffectiveType == 4)       // SkinTint body: deferred W3C soft-light of the per-actor tone
			{
				vec3 baseD = pow(max(albedo, 0.0), vec3(1.0/2.2));      // linear diffuse -> display
				vec3 blendD = pow(max(tintColor, 0.0), vec3(1.0/2.2));  // linear tone -> display
				vec3 loSL = 2.0 * baseD * blendD + baseD * baseD * (1.0 - 2.0 * blendD);
				vec3 hiSL = 2.0 * baseD * (1.0 - blendD) + sqrt(max(baseD, 0.0)) * (2.0 * blendD - 1.0);
				vec3 slR;
				slR.x = (blendD.x < 0.5) ? loSL.x : hiSL.x;
				slR.y = (blendD.y < 0.5) ? loSL.y : hiSL.y;
				slR.z = (blendD.z < 0.5) ? loSL.z : hiSL.z;
				albedo = mix(albedo, pow(max(slR, 0.0), vec3(2.2)), skinTintStrength);
			}
			// uEffectiveType == 3 (Face / Facegen): NO tone curve -- the face renders its BAKED diffuse RAW.
			// The FaceGen head diffuse is fully baked by the engine BSFaceCustomization pass (b12 FaceCustom
			// rec3582 composites the FaceTint layers AND the skin tone into the head texture), which the
			// renderer samples directly. Applying any tone here double-processes it. This matches Render.vb's
			// own Ya-esta suppression (L3026-3029): SkinToneBaked -> bHasTintColor=false (runtime soft-light
			// forced off). The old `albedo = 2*a - a*a` was the forward-g6 skin curve a^2+2a*tint*(1-a)
			// degenerate at tint=1 -- a spurious brightening placeholder that broke face/body parity (verified
			// in-app: removing it makes the face match the body after skin tint). So type 3 is intentionally a
			// no-op (no `else if` branch needed -- the baked albedo passes through untouched).

			directionalLight(frontal, lightFrontal, outDiffuse, outSpecular);
			directionalLight(directional0, lightDirectional0, outDiffuse, outSpecular);
			directionalLight(directional1, lightDirectional1, outDiffuse, outSpecular);
			directionalLight(directional2, lightDirectional2, outDiffuse, outSpecular);

			// Rim lighting (FO4): disabled for multi-light rig. With the back fill
			// light dot(-L,V)~1 and low rimPower values (0.1) the smoothstep term
			// cannot attenuate, producing a full-surface wash. NifSkope/OS also disable it.
			//if (bRimlight)
			//{
			//	float rl0 = dot(-lightFrontal, viewDir);
			//	float rl1 = dot(-lightDirectional0, viewDir);
			//	float rl2 = dot(-lightDirectional1, viewDir);
			//	float rl3 = dot(-lightDirectional2, viewDir);
			//
			//	float bestRl = rl0;
			//	vec3 bestRlDiffuse = frontal.diffuse;
			//	if (rl1 > bestRl) { bestRl = rl1; bestRlDiffuse = directional0.diffuse; }
			//	if (rl2 > bestRl) { bestRl = rl2; bestRlDiffuse = directional1.diffuse; }
			//	if (rl3 > bestRl) { bestRl = rl3; bestRlDiffuse = directional2.diffuse; }
			//
			//	float NdotV_rim = max(dot(normal, viewDir), FLT_EPSILON);
			//	vec3 rim = vec3(pow((1.0 - NdotV_rim), rimlightPower));
			//	rim *= smoothstep(-0.2, 1.0, bestRl);
			//	emissive += rim * bestRlDiffuse;
			//}

			// Environment cubemap reflection (BGSM), reconstructed EXACT from the GAME forward
			// BSLightingShader rec1507 (t=0x101) L283-302 -- the ONLY per-material cube path:
			// the deferred prepass (b09) and lighting (b10) sample NO cubemap; the b11 composite
			// uses the world IBL probe ARRAY (scene), which cannot bind a per-material cube. So
			// the single-pass forward formula IS the faithful material-envmap reference.
			//   gloss = raw specMap.g (NOT *shininess);  lod = (1-gloss)*6 + screenZ*(1/512)
			//   intensity = specMap.r * 3 * min(sqrt(saturate(gloss-0.3)),1) * SpecularMult * EnvmapScale
			//   reflection = cube(reflect(V,N), lod) * intensity, modulated by (ambient + diffuse).
			// cb2[11].y (=SpecularMult) also scales the spec highlight (rec1507 L70) -> proven not
			// envmap-specific; cb1[2].x (=EnvmapScale=EnvironmentMappingMaskScale) is the UNIQUE
			// envmap-only multiplier (cb1 = per-material buffer; cb1[7]=subsurface). Mask = specMap.r
			// (engine reads t2.r); the eye routes its spec map into the env-mask slot, so there
			// envMask.r == spec.r -- the existing source branch picks the right channel either way.
			// Material cubemap reflection (BGSM Environment Mapping). The previewer renders the MATERIAL's
			// own cube for any material that carries one + EnvmapScale + spec mask, in BOTH paths:
			//  - ALPHA-BLEND: engine-EXACT (BSLightingShader forward rec1507, t=0x101): cube * spec.r * 3 *
			//    glossGate * SpecMult(cb2[11].y) * EnvmapScale(cb1[2].x) (L286/L290/L292/L298), modulated by
			//    (ambient + diffuse) (L299-302).  *3 is the engine's forward calibration.
			//  - OPAQUE: the engine has NO material-cube path (deferred reflects the WORLD IBL PROBE in the
			//    composite rec3401 -- verified: 0 deferred-prepass shaders sample a material texturecube). The
			//    previewer has no world probe, so the material cube stands in for it. The forward *3 calibration
			//    is for the bright forward sample and over-reflects here -> the shape reads metalizado; the
			//    subtle world-probe-like reflection is *1. Both paths still use the material's EnvmapScale,
			//    spec mask (envMaskR) and gloss gate, so a cube + EnvmapScale + specular material previews.
			if (bCubemap && bEnvMap && bShowTexture && !bIsEffectShader)
			{
				float envGloss = (bSpecular && bShowTexture) ? specGloss : 1.0;
				float lod = (1.0 - envGloss) * 6.0 + gl_FragCoord.z * 0.001953;

				vec3 reflected = reflect(viewDir, normal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));
				vec3 cube = textureLod(texCubemap, reflectedWS, lod).rgb;

				float envMaskR = (bEnvMask && !bGlowmap) ? envMask.r : specFactor;
				float glossGate = min(sqrt(clamp(envGloss - 0.3, 0.0, 1.0)), 1.0);
				float envScale = envReflection;
				float envIntensity = envMaskR * (bHasAlphaBlend ? 3.0 : 1.0) * glossGate * envScale * specularStrength;

				outSpecular += cube * envIntensity * (hemiAmbient(normal) + outDiffuse);
			}

			// Emissive (self-illumination). Engine DEFERRED prepass writes the emissive G-buffer o4 =
			// (glowMap if present)*EmissiveColor*EmissiveMult: rec2614 L125-126 `o4 = glowMap(t3)*cb2[1]`
			// when a glow map is set (technique bit 0x4000), else rec2607 L119 `o4 = cb2[1]` constant. The
			// composite adds it. So in the OPAQUE path the glow map MASKS the self-emission (it is the
			// emissive spatial pattern) -- it does NOT modulate ambient (that is the FORWARD/alpha-blend
			// path, rec1512 `ambient*glowmap`, applied below for bHasAlphaBlend). 76/470 prepass perms use
			// the glow-mask, 375 the constant.
			if (bEmissive)
			{
				vec3 emitMask = (bGlowmap && !bHair && !bHasAlphaBlend) ? texture(texGlowmap, uv).rgb : vec3(1.0);
				emissive += emissiveColor * emissiveMultiple * emitMask;
			}

			// Backlight sumado DESPUES del glowmap (orden NifSkope fo4_default.frag:252-296 y
			// sk_default.frag:124-143: el glowmap modula SOLO el self-emissive, NO el backlight
			// de translucencia). Antes el backlight entraba en 'emissive' dentro del loop de luz
			// y el '*= glowMap' lo contaminaba (en pelo, glowTex = el flow map _f).
			emissive += backlightEmissive;

			// Composite (DXBC g6_PS): out = albedo*(diffuse + ambient) + specular + emissive.
			// Per-type albedo curve (SkinTint a*a / Face 2a-a*a) was applied pre-lighting above.
			// Glowmap ambient modulation = the FORWARD/alpha-blend path ONLY (rec1512 `ambient*glowmap`).
			// The DEFERRED (opaque) path does NOT modulate ambient by the glow map -- it masks the EMISSIVE
			// instead (handled above). So gate this on bHasAlphaBlend. (hair's glow slot = the _f FLOW map.)
			vec3 ambientTerm = hemiAmbient(normal);
			if (bHasAlphaBlend && uEffectiveType == 2 && !bHair)
				ambientTerm *= texture(texGlowmap, uv).rgb;

			color.rgb = outDiffuse * albedo + ambientTerm * albedo;

			// HairTint (uEffectiveType==5): tint the lit diffuse+ambient by HairTintColor, mask = vertex
			// green. `out = lit * (1 + vColor.y*(tint-1))`; spec/emissive NOT tinted. ENGINE ROUTING (verified
			// blend-vs-test): the tint-lerp is the FORWARD b6 hair path = ALPHA-BLEND only. ALPHA-TEST hair
			// goes DEFERRED -> Kajiya-Kay + palette recolor/diffuse, NO tint-lerp (the color comes from the
			// recolor block above when bGreyscaleColor, else the diffuse). So gate the tint-lerp on
			// bHasAlphaBlend. (recolor vs tint are mutually exclusive upstream: palette-hair sets
			// GrayscaleToPaletteColor + HairTintColor=white -> lerp is identity; tint-hair sets HairTintColor.)
			if (uEffectiveType == 5 && bHasAlphaBlend)
				color.rgb *= vec3(1.0) + vColor.y * (tintColor - vec3(1.0));

			color.rgb += outSpecular;
			color.rgb += emissive;
		}

		// Effect Shader (BGEM = BSEffectShader, block b05), reconstructed EXACT from the GAME:
		// base rec1026, VC rec1083, recolor-color rec1103, recolor-alpha rec0905, envmap rec0761.
		// Engine pixel order (all LINEAR; NO PS tonemap/encode -- ADD/MULT/PREMULT blend = render-state):
		//   base.rgb = diffuse.rgb*BaseColor ; base.a = diffuse.a*BaseColor.a
		//   [ENVMAP]  base.rgb += cube(reflect(V,N)) * EnvmapScale * normal.a * envMask.r   (rec0761 L62-66)
		//   [VERTEX COLOR, only when mesh has them = rec1083 VC]: base.rgba *= pow(vColor.rgba,2.2)  (COLOR0 = mesh vColor, MULTIPLY)
			//   [RECOLOR-COLOR] base.rgb = palette(U=pow(diffuse.g,1/2.2), V=RAW BaseColor.r*falloff) * BaseColorScale  (rec1103 ignores COLOR0)
		//   eff = lerp(base, PropertyColor*base, LightingInfluence)
		//   alpha = base.a*PropertyColor.w ; [RECOLOR-ALPHA] alpha = palette(U=diffuse.a, V=pow(BaseColor.a,1/2.2)*falloff).a
		//   o0.rgb = lerp(eff, COLOR1.rgb, COLOR1.w)  <-- COLOR1 (v3) = VS-synthesized soft-particle DISTANCE falloff (rec0039 VS L72-78 / rec1083 PS L41-42), NOT the mesh vertex color. No preview analog -> NOT replicated.
		// PropertyColor (cb2[13], runtime light tint) -> rig light (outDiffuse+ambient); PropertyColor.w ~ 1.
		// BaseColorScale (cb1[1]) is the PALETTE scale ONLY -- it is NOT a base multiplier (rec0512 has none).
		// Vertex color (COLOR0) is applied below ONLY as a MULTIPLY on base rgb+alpha (rec1083). There is
		// NO final lerp toward the mesh vColor -- the engine's final lerp targets COLOR1 (the distance falloff), not COLOR0.
		if (bIsEffectShader)
		{
			// base = diffuse * BaseColor, with VC (vertex color) modulation when the mesh has vertex
			// colors (rec1083 VC bit): base.rgb *= pow(vColor.rgb,2.2) and the output alpha *=
			// pow(vColor.a,2.2) (COLOR0 gamma-decoded -- rec1083 L32-37). For EyeAO (black BaseColor) the AO
			// gradient is carried entirely by the vertex ALPHA (-> effAlpha, the OUTPUT alpha), NOT by any rgb
			// blend. bShowVertexColor = (toggle, default on) AND the mesh has vertex colors = the VC permutation.
			vec3 vcMod = bShowVertexColor ? pow(max(vColor.rgb, 0.0), vec3(2.2)) : vec3(1.0);
			float vcAlpha = bShowVertexColor ? pow(max(vColor.a, 0.0), 2.2) : 1.0;
			vec3 effRgb = baseMap.rgb * vcMod * effectBaseColor;
			float effAlpha = baseMap.a * vcAlpha * effectBaseColorAlpha;   // diffuse.a * pow(vColor.a,2.2) * BaseColor.a
			
			// Falloff factor (VS FalloffData -> v1.z; here angular). 1.0 when no falloff.
			float effFalloff = 1.0;
			if (bEffectFalloff || bEffectFalloffColor)
			{
				float NdotV_falloff = abs(dot(normal, normalize(viewDir)));
				float ft = clamp((NdotV_falloff - effectFalloffParams.x) / (effectFalloffParams.y - effectFalloffParams.x), 0.0, 1.0);
				ft = ft * ft * (3.0 - 2.0 * ft);
				effFalloff = mix(effectFalloffParams.z, effectFalloffParams.w, ft);
			}
			
			// Grayscale->palette recolor. COLOR(0x2000) replaces rgb; ALPHA(0x4000) replaces alpha; both=0x6000.
			// U = pow(base.channel,1/2.2) for COLOR (rec1103 L38-40), raw base.alpha for ALPHA (rec0905 L32).
			// V (color) = RAW BaseColor.r * falloff: SetupMaterial powf-encodes BaseColor.rgb into cb1[0]
			//   (0x14221DC20 L132-163), and rec1103 L34-36 pow(cb1[0].x,1/2.2) cancels it -> nets raw display
			//   BaseColor.r. NO extra pow here. (Alpha differs: BaseColor.a is NOT powf'd in setup, so its V
			//   KEEPS pow(.,1/2.2) -- rec0905 L34-37.)
			// When the mesh has vertex colors, the engine MULTIPLIES the palette V by the vertex color
			// channel, RAW (rec1002 `mul r0.yz, r0.yyzy, v2.xxwx`): V_color *= vColor.r, V_alpha *= vColor.a.
			// White verts (=1) -> no change (rec1103/rec0905, no-vColor perm). NOTE BGSM does this ADDITIVE
			// + gamma-encoded; BGEM does it MULTIPLICATIVE + raw (different families, verified per-asm).
			float vcRecolorR = bShowVertexColor ? max(vColor.r, 0.0) : 1.0;
			float vcRecolorA = bShowVertexColor ? max(vColor.a, 0.0) : 1.0;
			if (bGreyscaleColor)
			{
				float palU = pow(max(baseMap.g, 0.0), 1.0/2.2);
				float palV = max(effectBaseColor.r, 0.0) * vcRecolorR * effFalloff;
				effRgb = colorLookup(palU, palV).rgb * effectBaseColorScale;   // * BaseColorScale (PaletteColorScale)
			}
			
			// ENVMAP reflection (0x80000): cube(reflect(V,N)) * EnvmapScale * normal.a * envMask.r,
			// ADDED to base BEFORE the lighting-influence lerp (rec0761 L62-66), on top of recolor.
			if (bCubemap && bEnvMap && bShowTexture)
			{
				vec3 reflected = reflect(viewDir, normal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));
				vec3 cube = texture(texCubemap, reflectedWS).rgb;
				float emask = bEnvMask ? texture(texEnvMask, uv).r : 1.0;
				float nrmA = bNormalMap ? normalMap.a : 1.0;
				effRgb += cube * envReflection * nrmA * emask;
			}
			if (bEffectGreyscaleAlpha)
			{
				float palUa = baseMap.a;                                        // alpha index = diffuse.alpha (rec0905)
				float palVa = pow(max(effectBaseColorAlpha, 0.0), 1.0/2.2) * vcRecolorA * effFalloff;
				effAlpha = colorLookup(palUa, palVa).a;
			}
			
			// RGB_FALLOFF (0x200000) multiplies rgb; FALLOFF (0x10) multiplies alpha.
			if (bEffectFalloffColor && !bGreyscaleColor) effRgb *= effFalloff; // recolor already folds falloff into palette V (rec0550) -> avoid falloff^2
			if (bEffectFalloff)      effAlpha *= effFalloff;
			
			// Lighting influence: lerp base toward base*sceneLight (PropertyColor ~ rig light = outDiffuse+ambient).
			if (bLightEnabled)
				effRgb = mix(effRgb, effRgb * (outDiffuse + hemiAmbient(normal)), effectLightingInfluence);
			
			// NO emissive add: the engine b05 BGEM family has NO emissive term (verified -- none of the b05
			// PS sample a glow or add an emissive). A glow on an effect material is its base color + the
			// additive/glow BLEND MODE, not a separate emissive. Adding emissiveColor*mult here washed glowing
			// BGEM effects toward white (the green->white on a BGEM-with-glowmap). effRgb stays the effect.
			
			color.rgb = effRgb;
			color.a = effAlpha;

			// NO final vertex-color lerp here. The engine's BGEM PS ends with lerp(eff, COLOR1.rgb, COLOR1.w)
			// where COLOR1 (v3) is the VS-synthesized soft-particle DISTANCE falloff (rec0039 VS L72-78 /
			// rec1083 PS L41-42) -- NOT the mesh vertex color. The mesh vertex color is COLOR0 and is already
			// applied above as a MULTIPLY on base rgb+alpha (vcMod/vcAlpha). The distance falloff has no preview
			// analog (its alpha ~0 on solid geometry, a no-op); lerping toward the mesh vColor instead washed
			// the effect to white on white-a=1 verts (BloodBug) and inverted EyeAO. Angular material falloff is
			// already handled by effFalloff above.
		}

		if (bShowMask)
		{
          color.rgb *= maskFactor;
		}

		if (bShowWeight)
		{
			color.rgb *= weightColor;
		}

		// Tonemap + sRGB encode are the BSLighting display path. The engine BGEM (b05) has NO tonemap and
		// NO in-shader encode (verified: 0 b05 PS contain the Hable curve) -- the effect output is linear and
		// the blend mode composites it; tonemapping a BGEM desaturates/washes it. Gate BOTH by !bIsEffectShader
		// (the encode was already gated; the tonemap was not -- that washed BGEM-with-glow toward white).
		// DebugMode writes fragColor after this and is left unencoded.
		if (!bIsEffectShader)
		{
			color.rgb = tonemap(color.rgb) / tonemap(vec3(1.0));
			color.rgb = pow(max(color.rgb, vec3(0.0)), vec3(1.0/2.2));
		}
	}
	else
	{
    vec3 shaded = color.rgb ;
     if (bShowTexture)
     {
     shaded=texture(texDiffuse, uv).rgb;
      }
     shaded *= maskFactor;
     color = vec4(shaded, WireAlpha) ;
	}

	// T12: engine outputs RAW alpha (no clamp on .a); clamp rgb only.
	color.rgb = clamp(color.rgb, 0.0, 1.0);

	fragColor = color;



//====================DEBUG MODE==========================
if (DebugMode > 0.0) {
    // Calculamos en view-space las tres direcciones TBN
    vec3 dbgTangent  = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
    vec3 dbgBitangent= normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
    vec3 dbgNormal   = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));


    // Mapeo de -1..1 a 0..1 para visualizar en color
    dbgNormal    = dbgNormal    * 0.5 + 0.5;
    dbgTangent   = dbgTangent   * 0.5 + 0.5;
    dbgBitangent = dbgBitangent * 0.5 + 0.5;

    if (abs(DebugMode - 1.0) < 0.5) {
        // Modo 1: normales
        fragColor = vec4(dbgNormal, 1.0);
    }
    else if (abs(DebugMode - 2.0) < 0.5) {
        // Modo 2: tangentes
        fragColor = vec4(dbgTangent, 1.0);
    }
    else if (abs(DebugMode - 3.0) < 0.5) {
        // Modo 3: bitangentes
        fragColor = vec4(dbgBitangent, 1.0);
    }
    else if (abs(DebugMode - 4.0) < 0.5) {
        // Modo 4: TBN error comparison (no MSN in FO4)
        vec3 Tm = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
        vec3 Bm = normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
        vec3 Nm = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));

        vec3 Tgs = normalize(Tm - Nm * dot(Nm, Tm));
        vec3 Bx  = normalize(cross(Nm, Tgs));
        float h  = sign(dot(Bm, Bx));
        mat3 tbn_fixed = mat3(Tgs, Bx * h, Nm);

        vec3 n_ts = vec3(0.0, 0.0, 1.0);
        vec3 nA;
        vec3 nB;
        if (bShowTexture && bNormalMap) {
            vec3 nm = texture(texNormal, uv).rgb * 2.0 - 1.0;
            nm.z = sqrt(max(FLT_EPSILON, 1.0 - dot(nm.xy, nm.xy)));
            n_ts = nm;
            nA = normalize(mv_tbn   * n_ts);
            nB = normalize(tbn_fixed * n_ts);
        } else {
            nA = normalize(mv_tbn   * n_ts);
            nB = normalize(tbn_fixed * n_ts);
        }

        float errN = 0.5 * length(nA - nB);

        float IA = max(dot(nA, lightFrontal), 0.0)
                 + max(dot(nA, lightDirectional0), 0.0)
                 + max(dot(nA, lightDirectional1), 0.0)
                 + max(dot(nA, lightDirectional2), 0.0);

        float IB = max(dot(nB, lightFrontal), 0.0)
                 + max(dot(nB, lightDirectional0), 0.0)
                 + max(dot(nB, lightDirectional1), 0.0)
                 + max(dot(nB, lightDirectional2), 0.0);

        float errL = abs(IA - IB);

        float E = clamp(max(errN, errL), 0.0, 1.0);

        float good = 1.0 - smoothstep(0.0, 0.15, E);
        float bad  = smoothstep(0.0, 0.15, E);
        float hvis = h * 0.5 + 0.5;

        fragColor = vec4(bad, good, hvis, 1.0);
        return;
    }
}
//===================END DEBUG MODE=======================

if (bHide)
	    {
            discard;
	    }

  	if (bApplyZap) // Codigo Manolo para el ZAP
    {
  //  if (!bShowMask)
   // {
  	    if (ZappedVert==1)
	    {
    	    discard;
	    }
        }
    //}

   	if (!bWireframe)
	{
		// ALPHA TEST = ENGINE-faithful (rec1498 L284): discard if (diffuse.a * vColor.a) < ref. The test
		// uses the TEXTURE*VERTEX alpha only -- NOT the material Alpha scalar (which is the OUTPUT/blend
		// alpha = cb2[2].z, applied AFTER the test). The old order (NifSkope fo4_default.frag) multiplied
		// material Alpha in BEFORE the test, over-discarding cutouts when Alpha<1. For BGSM, fragColor.a
		// here is vColor.a*baseMap.a (color.a, pre material-alpha) -> matches the engine LHS. For BGEM,
		// fragColor.a is effAlpha which already carries BaseColor.a*PropertyColor.w -- the factors the
		// engine's BGEM alpha test uses (rec1103 L48) -- so it is tested as-is.
		if (bAlphaTest)
			if (fragColor.a <= alphaThreshold) // GL_GREATER
				discard;

		// Material Alpha = the OUTPUT/blend alpha (BGSM). BGEM baked it into effAlpha already.
		if (!bIsEffectShader)
			fragColor.a *= alpha;
	}

}
"
    Sub New()
        MyBase.New(Vertex_FO4, Fragment_FO4)
    End Sub
End Class

Public Class Shader_Class_SSE
    Inherits Shader_Base_Class
    Private Const Vertex_SSE As String = "
#version 430
// SSE vertex shader with model-space normal (MSN) support
uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;
uniform mat4 matModelView;
uniform mat3 mv_normalMatrix;
uniform vec3 color;
uniform vec3 subColor;
uniform bool bModelSpace;

uniform bool bShowTexture;
uniform bool bShowMask;
uniform bool bShowWeight;
uniform bool bShowVertexColor;
uniform bool bShowVertexAlpha;
uniform bool bApplyZap;

uniform bool bWireframe;

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec3 vertexNormal;
layout(location = 2) in vec3 vertexTangent;
layout(location = 3) in vec3 vertexBitangent;
layout(location = 4) in vec3 vertexColors;
layout(location = 5) in float vertexAlpha;
layout(location = 6) in vec2 vertexUV;
layout(location = 7) in float vertexMask;
layout(location = 8) in float vertexWeight;
layout(location = 9) in vec4 boneIndicesF;
layout(location = 10) in vec4 boneWeightsIn;

layout(std430, binding = 0) buffer BoneMatrices {
    mat4 bones[];
};
uniform bool bGPUSkinning;
uniform int uBoneCount;
// GPU SKINNING - SYNC CONTRACT
// The blend formula here MUST match SkinningHelper.BlendBoneMatrices()
// (CPU double-precision) and RecomputeGPUBoneMatrices() (bone matrix
// composition). Any change to weights, fallback, or matrix composition
// must be mirrored in all three locations:
//   1. This shader (Shader_Class_FO4 vertex + Shader_Class_SSE vertex)
//   2. SkinningHelper.BlendBoneMatrices()
//   3. SkinningHelper.RecomputeGPUBoneMatrices()
//   4. SkinningHelper.ExtractSkinnedGeometry() (GPU arrays extraction)
// Differences by design:
//   - GPU: float precision, pre-normalized weights (sum=1 at extract)
//   - CPU: double precision, runtime normalization (1/sumW)
//   - GPU applies transpose(inverse(mat3)) for N/T/B; CPU stores
//     N/T/B in local space and lets the shader transform them.
// This block is DUPLICATED in Shader_Class_FO4 and Shader_Class_SSE.

struct DirectionalLight
{
	vec3 diffuse;
	vec3 direction;
};

uniform DirectionalLight frontal;
uniform DirectionalLight directional0;
uniform DirectionalLight directional1;
uniform DirectionalLight directional2;

out vec3 lightFrontal;
out vec3 lightDirectional0;
out vec3 lightDirectional1;
out vec3 lightDirectional2;

out vec3 viewDir;
out mat3 mv_tbn;
out mat3 v_msnMatrix;

out float maskFactor;
flat out int ZappedVert;
out vec3 weightColor;

out vec4 vColor;
out vec2 vUV;

vec3 colorRamp(in float value)
{
	float r;
	float g;
	float b;

	if (value <= 0.0f)
	{
		r = g = b = 1.0;
	}
	else if (value <= 0.25)
	{
		r = 0.0;
		b = 1.0;
		g = value / 0.25;
	}
	else if (value <= 0.5)
	{
		r = 0.0;
		g = 1.0;
		b = 1.0 + (-1.0) * (value - 0.25) / 0.25;
	}
	else if (value <= 0.75)
	{
		r = (value - 0.5) / 0.25;
		g = 1.0;
		b = 0.0;
	}
	else
	{
		r = 1.0;
		g = 1.0 + (-1.0) * (value - 0.75) / 0.25;
		b = 0.0;
	}

	return vec3(r, g, b);
}

void main(void)
{
	// Initialization
	maskFactor = 1.0;
    ZappedVert = 0;
    if (bApplyZap)
    {
     if (vertexMask<0)
      ZappedVert = 1;
    }
	if (bShowMask)
	{
		maskFactor = 1.0 - vertexMask / 1.5;

    if (ZappedVert==1) //zapped
        {
    		maskFactor = 1.0 - (-vertexMask) / 1.5;
        }

   	}
	weightColor = vec3(1.0, 1.0, 1.0);
	vColor = vec4(1.0, 1.0, 1.0, 1.0);
	vUV = vertexUV;

	if (bShowVertexColor)
	{
		vColor.rgb = vertexColors;
	}

	if (bShowVertexAlpha)
	{
		vColor.a = vertexAlpha;
	}

	// GPU Skinning
	vec3 skinnedPos;
	vec3 skinnedNormal;
	vec3 skinnedTangent;
	vec3 skinnedBitangent;

	if (bGPUSkinning) {
	    // GPU skinning: blend bone matrices
	    ivec4 bIdx = clamp(ivec4(boneIndicesF), ivec4(0), ivec4(max(uBoneCount - 1, 0)));
	    vec4 bWgt = boneWeightsIn;

	    mat4 skinMatrix = mat4(0.0);
	    // Accumulate weighted bone matrices
	    if (bWgt.x > 0.0) skinMatrix += bones[bIdx.x] * bWgt.x;
	    if (bWgt.y > 0.0) skinMatrix += bones[bIdx.y] * bWgt.y;
	    if (bWgt.z > 0.0) skinMatrix += bones[bIdx.z] * bWgt.z;
	    if (bWgt.w > 0.0) skinMatrix += bones[bIdx.w] * bWgt.w;

	    // Zero-weight fallback: first bone (matches CPU BlendBoneMatrices), then identity if no bones
	    float totalWeight = bWgt.x + bWgt.y + bWgt.z + bWgt.w;
	    if (totalWeight < 0.001) skinMatrix = (uBoneCount > 0) ? bones[bIdx.x] : mat4(1.0);

	    skinnedPos = vec3(skinMatrix * vec4(vertexPosition, 1.0));

	    // Correct normal matrix: transpose of inverse of upper-left 3x3
	    mat3 skinNormalMat = transpose(inverse(mat3(skinMatrix)));
	    skinnedNormal = normalize(skinNormalMat * vertexNormal);
	    skinnedTangent = normalize(skinNormalMat * vertexTangent);
	    skinnedBitangent = normalize(skinNormalMat * vertexBitangent);

	    // MSN: combined matrix local -> world -> view (per-vertex due to skinning)
	    v_msnMatrix = mv_normalMatrix * skinNormalMat;
	} else {
	    // CPU skinning fallback: vertices already in world space
	    skinnedPos = vertexPosition;
	    skinnedNormal = vertexNormal;
	    skinnedTangent = vertexTangent;
	    skinnedBitangent = vertexBitangent;

	    if (bModelSpace) {
	        // CPU + MSN: N/T/B VBOs carry skinNormalMat columns (local->world)
	        // instead of vertex normals (which are zero for MSN shapes)
	        mat3 cpuSkinNormMat = mat3(vertexNormal, vertexTangent, vertexBitangent);
	        v_msnMatrix = mv_normalMatrix * cpuSkinNormMat;
	    } else {
	        v_msnMatrix = mv_normalMatrix;
	    }
	}

	// Eye-coordinate position of vertex (now using skinned position)
	vec3 vPos = vec3(matModelView * vec4(skinnedPos, 1.0));
	gl_Position = matProjection * vec4(vPos, 1.0);

	// TBN in view space
	vec3 mv_normal = mv_normalMatrix * skinnedNormal;
	vec3 mv_tangent = mv_normalMatrix * skinnedTangent;
	vec3 mv_bitangent = mv_normalMatrix * skinnedBitangent;

    mv_tbn = mat3(mv_tangent.x,   mv_tangent.y,   mv_tangent.z,
              mv_bitangent.x, mv_bitangent.y, mv_bitangent.z,
              mv_normal.x,    mv_normal.y,    mv_normal.z);

	viewDir = normalize(-vPos);
	lightFrontal = normalize(mat3(matView) * frontal.direction);
	lightDirectional0 = normalize(mat3(matView) * directional0.direction);
	lightDirectional1 = normalize(mat3(matView) * directional1.direction);
	lightDirectional2 = normalize(mat3(matView) * directional2.direction);

	if (!bShowTexture || bWireframe)
	{
		vColor *= clamp(vec4(color, 1.0), 0.0, 1.0);
	}

	if (!bWireframe)
	{
		vColor.rgb *= subColor;

		if (bShowWeight)
		{
			weightColor = colorRamp(vertexWeight);
		}
	}
}
"
    Private Const Fragment_SSE As String = "
#version 430
// SSE fragment shader with model-space normal (MSN) support

/*
 * BodySlide and Outfit Studio
 * Shaders by jonwd7 and ousnius
 * https://github.com/ousnius/BodySlide-and-Outfit-Studio
 * http://www.niftools.org/
 * Modified By Manolo For WardrobeManager
 */

uniform sampler2D texDiffuse;
uniform sampler2D texNormal;
uniform samplerCube texCubemap;
uniform sampler2D texEnvMask;
uniform sampler2D texSpecular;
uniform sampler2D texGreyscale;
uniform sampler2D texGlowmap;
uniform sampler2D texLightmask;
uniform sampler2D texDetailMask;
uniform sampler2D texFaceTintOverlay;   // TETI/TEND composed tint layers, blended on top of diffuse

uniform bool bLightEnabled;
uniform bool bShowTexture;
uniform bool bShowMask;
uniform bool bLightmask;
uniform bool bShowWeight;
uniform bool bWireframe;
uniform bool bApplyZap;

uniform bool bNormalMap;
uniform bool bModelSpace;
uniform bool bCubemap;
uniform bool bEnvMap;
uniform bool bEye;
uniform bool bEnvMask;
uniform bool bSpecular;
uniform bool bHasSpecMap;
uniform bool bEmissive;
uniform bool bBacklight;
uniform bool bRimlight;
uniform bool bAnisoLighting;
uniform bool bSoftlight;
uniform bool bAlphaTest;
uniform bool bGlowmap;
uniform bool bGreyscaleColor;
uniform bool bHasTintColor;
uniform bool bHairTint;
uniform bool bHasDetailMask;
uniform bool bHasFaceTintOverlay;       // true when composed face tint texture is bound
uniform bool bDoubleSided;
uniform bool bHide;

uniform bool bIsEffectShader;
uniform bool bDecal;
uniform int shaderType;
uniform bool bEffectFalloff;
uniform bool bEffectFalloffColor;
uniform bool bEffectGreyscaleAlpha;
uniform float effectLightingInfluence;
uniform vec4 effectFalloffParams;
uniform vec3 effectBaseColor;
uniform float effectBaseColorAlpha;
uniform float effectBaseColorScale;

uniform mat4 matModel;
uniform mat4 matModelViewInverse;
uniform mat3 mv_normalMatrix;
uniform float DebugMode;

uniform	vec2 uvOffset;
uniform vec2 uvScale;
uniform	vec3 specularColor;
uniform	float specularStrength;
uniform	float shininess;
uniform float glossiness;
uniform float envReflection;
uniform vec3 emissiveColor;
uniform float emissiveMultiple;
uniform float alpha;
uniform float backlightPower;
uniform float rimlightPower;
uniform	float subsurfaceRolloff;
uniform	float fresnelPower;
uniform float paletteScale;
uniform float WireAlpha;

uniform float alphaThreshold;

uniform vec3 ambientSky;       // hemispheric ambient: color when N points world-up (+Z)
uniform vec3 ambientGround;    // hemispheric ambient: color when N points world-down (-Z)
uniform vec3 tintColor;

struct DirectionalLight
{
	vec3 diffuse;
	vec3 direction;
};

uniform DirectionalLight frontal;
uniform DirectionalLight directional0;
uniform DirectionalLight directional1;
uniform DirectionalLight directional2;

in vec3 lightFrontal;
in vec3 lightDirectional0;
in vec3 lightDirectional1;
in vec3 lightDirectional2;

in vec3 viewDir;
in mat3 mv_tbn;
in mat3 v_msnMatrix;  // MSN: per-vertex local->view (skinning+view combined in vertex shader)

in float maskFactor;
flat in int ZappedVert;
in vec3 weightColor;

in vec4 vColor;
in vec2 vUV;

out vec4 fragColor;

vec3 normal = vec3(0.0);
float specGloss = 1.0;
float specFactor = 1.0;

vec2 uv = vec2(0.0);
vec3 albedo = vec3(0.0);
vec3 emissive = vec3(0.0);
vec3 backlightEmissive = vec3(0.0);

vec4 baseMap = vec4(0.0);
vec4 normalMap = vec4(0.0);
vec4 specMap = vec4(0.0);
vec4 envMask = vec4(0.0);

#ifndef M_PI
	#define M_PI 3.1415926535897932384626433832795
#endif

#define FLT_EPSILON 1.192092896e-07F // smallest such that 1.0 + FLT_EPSILON != 1.0

float OrenNayarFull(vec3 L, vec3 V, vec3 N, float roughness, float NdotL)
{
	//float NdotL = dot(N, L);
	float NdotV = dot(N, V);
	float LdotV = dot(L, V);

	float angleVN = acos(max(NdotV, FLT_EPSILON));
	float angleLN = acos(max(NdotL, FLT_EPSILON));

	float alpha = max(angleVN, angleLN);
	float beta = min(angleVN, angleLN);
	float gamma = LdotV - NdotL * NdotV;

	float roughnessSquared = roughness * roughness;
	float roughnessSquared9 = (roughnessSquared / (roughnessSquared + 0.09));

	// C1, C2, and C3
	float C1 = 1.0 - 0.5 * (roughnessSquared / (roughnessSquared + 0.33));
	float C2 = 0.45 * roughnessSquared9;

	if( gamma >= 0.0 )
		C2 *= sin(alpha);
	else
		C2 *= (sin(alpha) - pow((2.0 * beta) / M_PI, 3.0));

	float powValue = (4.0 * alpha * beta) / (M_PI * M_PI);
	float C3 = 0.125 * roughnessSquared9 * powValue * powValue;

	// Avoid asymptote at pi/2
	float asym = M_PI / 2.0;
	float lim1 = asym + 0.01;
	float lim2 = asym - 0.01;

	float ab2 = (alpha + beta) / 2.0;

	if (beta >= asym && beta < lim1)
		beta = lim1;
	else if (beta < asym && beta >= lim2)
		beta = lim2;

	if (ab2 >= asym && ab2 < lim1)
		ab2 = lim1;
	else if (ab2 < asym && ab2 >= lim2)
		ab2 = lim2;

	// Reflection
	float A = gamma * C2 * tan(beta);
	float B = (1.0 - abs(gamma)) * C3 * tan(ab2);

	float L1 = max(FLT_EPSILON, NdotL) * (C1 + A + B);

	// Interreflection
	float twoBetaPi = 2.0 * beta / M_PI;
	float L2 = 0.17 * max(FLT_EPSILON, NdotL) * (roughnessSquared / (roughnessSquared + 0.13)) * (1.0 - gamma * twoBetaPi * twoBetaPi);

	return L1 + L2;
}

// Schlick's Fresnel approximation
float fresnelSchlick(float VdotH, float F0)
{
	float base = 1.0 - VdotH;
	float exp = pow(base, fresnelPower);
	return clamp(exp + F0 * (1.0 - exp), 0.0, 1.0);
}

// The Torrance-Sparrow visibility factor, G
float VisibDiv(float NdotL, float NdotV, float VdotH, float NdotH)
{
	float denom = max(VdotH, FLT_EPSILON);
	float numL = min(NdotV, NdotL);
	float numR = 2.0 * NdotH;
	if (denom >= (numL * numR))
	{
		numL = (numL == NdotV) ? 1.0 : (NdotL / NdotV);
		return (numL * numR) / denom;
	}
	return 1.0 / NdotV;
}

// this is a normalized Phong model used in the Torrance-Sparrow model
vec3 TorranceSparrow(float NdotL, float NdotH, float NdotV, float VdotH, vec3 color, float power, float F0)
{
	// D: Normalized phong model
	float D = ((power + 2.0) / (2.0 * M_PI)) * pow(NdotH, power);

	// G: Torrance-Sparrow visibility term divided by NdotV
	float G_NdotV = VisibDiv(NdotL, NdotV, VdotH, NdotH);

	// F: Schlick's approximation
	float F = fresnelSchlick(VdotH, F0);

	// Torrance-Sparrow:
	// (F * G * D) / (4 * NdotL * NdotV)
	// Division by NdotV is done in VisibDiv()
	// and division by NdotL is removed since
	// outgoing radiance is determined by:
	// BRDF * NdotL * L()
	float spec = (F * G_NdotV * D) / 4.0;

	return color * spec * M_PI;
}

vec3 tonemap(in vec3 x)
{
	const float A = 0.15;
	const float B = 0.50;
	const float C = 0.10;
	const float D = 0.20;
	const float E = 0.02;
	const float F = 0.30;

	return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

void directionalLight(in DirectionalLight light, in vec3 lightDir, inout vec3 outDiffuse, inout vec3 outSpec)
{
	vec3 halfDir = normalize(lightDir + viewDir);
	float NdotL = dot(normal, lightDir);
	float NdotL0 = max(NdotL, FLT_EPSILON);
	float NdotH = max(dot(normal, halfDir), FLT_EPSILON);
	float NdotV = max(dot(normal, viewDir), FLT_EPSILON);
	float VdotH = max(dot(viewDir, halfDir), FLT_EPSILON);

	// Specularity
	float smoothness = 1.0;
	float roughness = 0.0;
	float specMask = 1.0;
	if (bSpecular && bShowTexture)
	{
		smoothness = specGloss * shininess;
		roughness = 1.0 - smoothness;
		specMask = specFactor * specularStrength;

		if (bHairTint && bAnisoLighting)
		{
			// SSE HAIR anisotropic specular = technique 6 (Hair) + ANISO_LIGHTING define
			// (sse_hair_aniso.asm L21-56). FO4 differs (flow-map Kajiya-Kay rec3110); SSE uses TWO
			// shifted-NORMAL lobes built from the geometric normal + tangent of the TBN (engine
			// v3.z/v4.z/v5.z = N_geo, v3.x/v4.x/v5.x = T -> mv_tbn[2], mv_tbn[0]):
			//   sh1 = normalize(0.5*bumpN + N_geo)                              (L21-27)
			//   sh2 = normalize(sh1 - 0.05*T)                                   (L36-41)
			//   a_i = pow(1 - min(|sh_i.(L - H)|, 1), Glossiness)              (L28-35 / L42-49)
			//   aniso = (0.7*a1 + a2*hairTint) * SpecularColor * specMask * lightColor
			//   hairTint = mix(1, HairTintColor, vColor.g)                     (L50-54)
			// Omitted: engine max(L.z,0) sun-elevation clamp (L52) -- a rig term; the app keeps its
			// own multi-light rig (porting the material response, not the engine's single-sun rig).
			// The app re-tints this spec at the hair-tint multiply below (engine tints lit color
			// separately, tail L1-3): minor composition-order deviation, documented, not invented.
			vec3 Ngeo = normalize(mv_tbn[2]);
			vec3 Ttan = normalize(mv_tbn[0]);
			vec3 sh1  = normalize(0.5 * normal + Ngeo);
			vec3 sh2  = normalize(sh1 - 0.05 * Ttan);
			float a1  = pow(1.0 - min(abs(dot(sh1, lightDir) - dot(sh1, halfDir)), 1.0), glossiness);
			float a2  = pow(1.0 - min(abs(dot(sh2, lightDir) - dot(sh2, halfDir)), 1.0), glossiness);
			vec3 hairTint = mix(vec3(1.0), tintColor, vColor.g);
			outSpec += (0.7 * a1 + a2 * hairTint) * specularColor * specMask * light.diffuse;
		}
		else
		{
			// SSE: Blinn-Phong with the RAW glossiness exponent passed from the app
			// (uniform glossiness = shad.Glossiness): no exp2 reconstruction, no specGloss
			// modulation. Matches NifSkope sk_default and OutfitStudio default.frag.
			outSpec += clamp(specularColor * specMask * pow(NdotH, glossiness), 0.0, 1.0) * light.diffuse;
		}
	}

	// Back lighting: simulates translucency (light through thin cloth/hair)
	// SSE: when bBacklight, texSpecular (slot 7) contains the backlight texture
	if (bBacklight)
	{
		// Engine (idx 4046): diffuse += saturate(dot(N,-L)) * backlightTex * lightColor.
		// No backlightPower scale; flows through outDiffuse so it is albedo-modulated like the engine.
		float NdotNegL = max(dot(normal, -lightDir), 0.0);
		vec3 backlightColor = texture(texSpecular, uv).rgb;
		outDiffuse += backlightColor * NdotNegL * light.diffuse;
	}

	// Diffuse (engine idx 4032: Lambert saturate(N.L) * lightColor; NOT Oren-Nayar)
	outDiffuse += max(NdotL, 0.0) * light.diffuse;

	// Soft Lighting / subsurface (engine idx 4038 SOFT_LIGHTING):
	//   wrap = saturate((NdotL + rolloff) / (1 + rolloff))
	//   sss  = saturate( SS(wrap) - SS(saturate(NdotL)) ),  SS(x) = x*x*(3-2x)
	//   diffuse += sss * subsurfaceTex * lightColor     (subsurfaceTex = texLightmask slot)
	if (bSoftlight)
	{
		vec3 softMask = bLightmask ? texture(texLightmask, uv).rgb : albedo;
		float w = clamp((NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff), 0.0, 1.0);
		float nl = clamp(NdotL, 0.0, 1.0);
		float sss = clamp(w * w * (3.0 - 2.0 * w) - nl * nl * (3.0 - 2.0 * nl), 0.0, 1.0);
		outDiffuse += sss * softMask * light.diffuse;
	}

	// Rim lighting (engine idx 4042): pow(1 - saturate(N.V), rimPower) * saturate(dot(Vn,-L)) * rimTex * lightColor.
	// The saturate(dot(Vn,-L)) gate keeps it an edge/back-lit effect (this is what avoids the full-surface wash).
	if (bRimlight)
	{
		float NdotVr = max(dot(normal, viewDir), 0.0);
		float rim = pow(1.0 - NdotVr, rimlightPower) * max(dot(viewDir, -lightDir), 0.0);
		vec3 rimMask = bLightmask ? texture(texLightmask, uv).rgb : vec3(1.0);
		outDiffuse += rim * rimMask * light.diffuse;
	}
}

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
}

// Hemispheric ambient = engine-faithful STRUCTURE: FO4/SSE light the ambient as a normal-dependent
// term (DirectionalAmbient . vec4(N,1)), NOT a flat scalar. We have no cell ambient matrix, so we
// synthesize it from two preview colors: sky from world-up (+Z), ground from world-down (-Z). The
// shading normal is view-space; transform to world (reusing the envmap matrices) and blend by its
// up (Z) component. Anchored to world up so the hemisphere stays put as the camera orbits.
vec3 hemiAmbient(in vec3 nrm)
{
	vec3 nWS = normalize(vec3(matModel * (matModelViewInverse * vec4(nrm, 0.0))));
	return mix(ambientGround, ambientSky, clamp(nWS.z * 0.5 + 0.5, 0.0, 1.0));
}

void main(void)
{
    uv = vUV * uvScale + uvOffset;
	vec4 color = vColor;
	albedo = vColor.rgb;
	vec3 outDiffuse = vec3(0.0);
	vec3 outSpecular = vec3(0.0);

	if (!bWireframe)
	{
		if (bShowTexture)
		{
			// Diffuse Texture
			baseMap = texture(texDiffuse, uv);
			albedo *= baseMap.rgb;
			color.a *= baseMap.a;

			// Diffuse texture without lighting
			color.rgb = albedo;

			if (bLightEnabled)
			{
				if (bNormalMap)
				{
					normalMap = texture(texNormal, uv);
				}

				if (bSpecular)
				{
					if (bBacklight)
					{
						// SSE: when backlight is active, slot 7 (texSpecular) contains the
						// backlight texture, not specular. Specular comes from normalMap.a.
						if (bNormalMap)
						{
							specGloss = 1.0;
							specFactor = normalMap.a;
						}
						else
						{
							// No valid specular source in this path: keep the backlight texture
							// bound for translucency, but suppress reflective highlights.
							specGloss = 0.0;
							specFactor = 0.0;
						}
					}
					else if (bHasSpecMap)
					{
						// Dedicated specular map: R=factor, G=glossiness
						specMap = texture(texSpecular, uv);
						specGloss = specMap.g;
						specFactor = specMap.r;
					}
					else if (bNormalMap)
					{
						// SSE fallback: specular intensity from normal map alpha,
						// glossiness entirely from the material property (shininess uniform)
						specGloss = 1.0;
						specFactor = normalMap.a;
					}
					else
					{
						// Defensive fallback: do not invent a glossy response without a source.
						specGloss = 0.0;
						specFactor = 0.0;
					}
				}

				if (bCubemap)
				{
					if (bEnvMask && !bGlowmap)
					{
						// Environment Mask (BGSM slot 5 is dual: envmask when !bGlowmap)
						envMask = texture(texEnvMask, uv);
					}
				}
			}
		}

		if (bLightEnabled)
		{
			// Lighting with or without textures
			outDiffuse = vec3(0.0);
			outSpecular = vec3(0.0);

			// Start off neutral (for MSN shapes, mv_tbn is degenerate so use v_msnMatrix)
			if (bModelSpace)
			{
				normal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
			}
			else
			{
				normal = normalize(mv_tbn * vec3(0.0, 0.0, 0.5));
			}

			if (bShowTexture)
			{
				if (bNormalMap)
				{
					if (bModelSpace)
					{
						// Model Space Normal Map (SSE _msn)
						// Bethesda SSE stores normals as (X, Z, Y) - swizzle .rbg to get (X, Y, Z)
						// matching NIF object-space where Y=forward, Z=up
						normal = normalize(normalMap.rbg * 2.0 - 1.0);
						// Transform from NIF local/object space to view space
						// v_msnMatrix = mv_normalMatrix * skinNormalMat (per-vertex, from vertex shader)
						normal = normalize(v_msnMatrix * normal);
					}
					else
					{
						normal = (normalMap.rgb * 2.0 - 1.0);


						// Tangent space map
						normal = normalize(mv_tbn * normal);
					}
				}

				// GREYSCALE-TO-PALETTE: SSE-only divergence from FO4. The Skyrim BSLightingShader
				// pixel shader has NO greyscale path: VanillaGetLightingShaderDefines (0x14151C2D0)
				// emits no greyscale #define, and GRAYSCALE_TO_COLOR/GRAYSCALE_TO_ALPHA live ONLY in
				// the BSEffectShader define block (BSXShaderSamplers, 0x1ac7840/58) alongside the
				// dedicated GrayscaleSampler. In SSE the recolor is exclusively a BSEffectShaderProperty
				// feature, handled in the effect path below (bIsEffectShader). So a BSLightingShaderProperty
				// that carries the SLSF1 greyscale flag is rendered WITHOUT recolor by the engine -> no-op
				// here. (FO4 differs: its lighting shader rec2389/rec2963 DO recolor; see Fragment_FO4.)
				// The material flag is preserved for round-trip; only the lit render ignores it.
			}

			// FaceTint detail map: engine facegen (idx 8126) uses a SOFT-LIGHT blend onto the diffuse,
			// not hard-overlay: result = a*a + 2*a*b*(1-a)  (neutral at b=0.5). For SSE facegen data.
			if (bHasDetailMask)
			{
				vec3 dm = texture(texDetailMask, uv).rgb;
				albedo = albedo * albedo + 2.0 * albedo * dm * (1.0 - albedo);
			}

			// FaceTint overlay (TETI/TEND composed at runtime via FBO, premultiplied-over)
			if (bHasFaceTintOverlay)
			{
				vec4 ov = texture(texFaceTintOverlay, uv);
				albedo = albedo * (1.0 - ov.a) + ov.rgb;
			}

			// Double-sided: flip normal for back faces
			if (bDoubleSided && !gl_FrontFacing)
			{
				normal = -normal;
			}

			directionalLight(frontal, lightFrontal, outDiffuse, outSpecular);
			directionalLight(directional0, lightDirectional0, outDiffuse, outSpecular);
			directionalLight(directional1, lightDirectional1, outDiffuse, outSpecular);
			directionalLight(directional2, lightDirectional2, outDiffuse, outSpecular);

			// Rim lighting is now applied per-light inside directionalLight() (engine idx 4042),
			// gated by saturate(dot(Vn,-L)) so it stays an edge effect across the multi-light rig.

			// Environment cubemap (BGSM only; BGEM has its own cubemap path)
			if (bCubemap && bEnvMap && bShowTexture && !bIsEffectShader)
			{
				float cubeSmooth = (bSpecular && bShowTexture) ? specGloss * shininess : 1.0;

				// EYE technique (16): the engine reflects the cubemap about the eyeball's RADIAL
				// normal (sse_eye L108-111 reflects about v7), NOT the bump normal that lighting uses
				// (L73-80). The eye VS builds v7 = normalize(worldPos - eyeCenter), eyeCenter =
				// lerp(cb1[0],cb1[1], v6.x) -> a procedural sphere normal, so the iris normal-map does
				// NOT distort the cornea reflection. The eye-center constants + per-vertex blend are not
				// loaded here, but for a spherical eye the radial normal == the mesh geometric normal
				// (mv_tbn[2]); reflecting about it is faithful to the engine (and strictly closer than the
				// bump-normal reflection). Non-eye envmap keeps the bump-normal reflection (sse_envmap L12-14).
				vec3 reflNormal = bEye ? normalize(mv_tbn[2]) : normal;
				vec3 reflected = reflect(viewDir, reflNormal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));

				vec4 cube = textureLod(texCubemap, reflectedWS, 8.0 - cubeSmooth * 8.0);
				cube.rgb *= envReflection * specularStrength;
				if (bEnvMask && !bGlowmap)
				{
					cube.rgb *= envMask.r;
				}
				else
				{
					cube.rgb *= specFactor;
				}

				outSpecular += cube.rgb * (hemiAmbient(normal) + outDiffuse);
			}

			// Emissive
			if (bEmissive)
			{
				emissive += emissiveColor * emissiveMultiple;

				// Glowmap
				if (bGlowmap)
				{
					vec4 glowMap = texture(texGlowmap, uv);
					emissive *= glowMap.rgb;
				}
			}

			// Backlight now flows through outDiffuse (engine idx 4046: albedo-modulated). The old
			// 'emissive += backlightEmissive' path is removed.

			// SkinTint = engine FacegenRGBTint technique (NIF type 5 -> technique 5; idx 8577):
			// the skin-tone color is SOFT-LIGHT-blended onto the diffuse (NOT a multiply), plus a
			// fixed RGB correction: albedo = albedo^2 + 2*albedo*tint*(1-albedo); albedo *= rgbFix.
			// HairTint (type 6) is engine-applied AFTER lighting, masked by vertex-green (below).
			if (bHasTintColor && !bHairTint && !bIsEffectShader)
			{
				albedo = albedo * albedo + 2.0 * albedo * tintColor * (1.0 - albedo);
				albedo *= vec3(1.011719, 0.996094, 1.011719);
			}

			// Engine (idx 4032 / 7473): color = albedo * (diffuse + ambient + emissive) + specular.
			// Emissive/glow are INSIDE the albedo multiply (albedo-modulated); specular added on top.
			color.rgb = albedo * (outDiffuse + hemiAmbient(normal) + emissive);
			color.rgb += outSpecular;

			// Hair tint (engine idx 8985): litColor *= mix(1, HairTintColor, vertexColor.g).
			// vColor.g = vertex-color green (mask); vertex-color path assumed active.
			if (bHairTint && !bIsEffectShader)
			{
				color.rgb *= mix(vec3(1.0), tintColor, vColor.g);
			}
		}

		// Effect Shader (BGEM) overrides
		if (bIsEffectShader)
		{
			float effScale = bGreyscaleColor ? 1.0 : effectBaseColorScale;
			vec3 effBase = baseMap.rgb * vColor.rgb * effectBaseColor * effScale;

			// BGEM alpha: baseColor.a * vertex alpha * texture alpha
			// Bethesda Effect.hlsl: alpha *= PropertyColor.w (single multiply, not squared)
			float bcAlpha = effectBaseColorAlpha;
			float effTexAlpha = bEffectGreyscaleAlpha ? 1.0 : baseMap.a;
			color.a = bcAlpha * vColor.a * effTexAlpha;

			// Falloff (calculated early - needed for cubemap and greyscale modulation)
			float effFalloff = 1.0;
			if (bEffectFalloff || bEffectFalloffColor)
			{
				float NdotV_falloff = abs(dot(normal, normalize(viewDir)));
				effFalloff = smoothstep(effectFalloffParams.x, effectFalloffParams.y, NdotV_falloff);
				effFalloff = mix(max(effectFalloffParams.z, 0.0), min(effectFalloffParams.w, 1.0), effFalloff);

				if (bEffectFalloff)
					color.a *= effFalloff;

				if (bEffectFalloffColor)
					effBase *= effFalloff;
			}

			// Compose base color
			color.rgb = effBase;

			// Greyscale color lookup (BEFORE lighting and cubemap - NifSkope order)
			if (bGreyscaleColor)
			{
				vec4 luG = colorLookup(baseMap.g, effectBaseColor.r * vColor.r * effFalloff);
				color.rgb = luG.rgb;
			}

			// Greyscale alpha lookup (uses original baseMap.a as X coordinate)
			if (bEffectGreyscaleAlpha)
			{
				vec4 luA = colorLookup(baseMap.a, color.a);
				color.a = luA.a;
			}

			// Lighting influence (AFTER greyscale, BEFORE cubemap - NifSkope order)
			if (bLightEnabled)
			{
				color.rgb = mix(color.rgb, color.rgb * (outDiffuse + hemiAmbient(normal)), effectLightingInfluence);
			}

			// Emissive (WM addition - NifSkope effect shader has no separate emissive)
			color.rgb += emissive;

			// Cubemap (LAST - added on top of everything, matching NifSkope)
			if (bCubemap && bEnvMap && bShowTexture)
			{
				float cubeIntensity = 1.0;
				if (bEnvMask)
				{
					cubeIntensity = texture(texEnvMask, uv).g;
				}

				vec3 reflected = reflect(viewDir, normal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));
				vec4 cube = texture(texCubemap, reflectedWS);

				cube.rgb *= envReflection * cubeIntensity;
				cube.rgb = mix(cube.rgb, cube.rgb * outDiffuse, effectLightingInfluence);

				color.rgb += cube.rgb * effFalloff;
			}
		}

		if (bShowMask)
		{
          color.rgb *= maskFactor;
		}

		if (bShowWeight)
		{
			color.rgb *= weightColor;
		}

		color.rgb = tonemap(color.rgb) / tonemap(vec3(1.0));

		// Linear pipeline: the lit (BSLighting) color is LINEAR (engine-faithful: sRGB-SRV diffuse +
		// linear lights/material) and the framebuffer is not sRGB, so encode linear -> display here,
		// like the FO4 tail. Effect shaders (BGEM) keep display-space textures (ColorTextures_Path_List
		// is empty for BGEM -> raw upload) and compose in display space, so they are NOT encoded
		// (matches the FO4 !bIsEffectShader encode gate).
		if (!bIsEffectShader)
		{
			color.rgb = pow(max(color.rgb, vec3(0.0)), vec3(1.0/2.2));
		}
	}
	else
	{
    vec3 shaded = color.rgb ;
     if (bShowTexture)
     {
     shaded=texture(texDiffuse, uv).rgb;
      }
     shaded *= maskFactor;
     color = vec4(shaded, WireAlpha) ;
	}

	color = clamp(color, 0.0, 1.0);

	fragColor = color;



//====================DEBUG MODE==========================
if (DebugMode > 0.0) {
    vec3 dbgNormal;
    vec3 dbgTangent;
    vec3 dbgBitangent;

    if (bModelSpace) {
        // MSN: decode texture normal and transform via v_msnMatrix
        vec3 msnN = normalize(normalMap.rbg * 2.0 - 1.0);
        dbgNormal = normalize(v_msnMatrix * msnN);
        dbgTangent  = normalize(v_msnMatrix * vec3(1.0, 0.0, 0.0));
        dbgBitangent= normalize(v_msnMatrix * vec3(0.0, 1.0, 0.0));
    } else {
        dbgTangent  = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
        dbgBitangent= normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
        dbgNormal   = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));
    }

    // Mapeo de -1..1 a 0..1 para visualizar en color
    dbgNormal    = dbgNormal    * 0.5 + 0.5;
    dbgTangent   = dbgTangent   * 0.5 + 0.5;
    dbgBitangent = dbgBitangent * 0.5 + 0.5;

    if (abs(DebugMode - 1.0) < 0.5) {
        // Modo 1: normales (MSN or TBN based on bModelSpace)
        fragColor = vec4(dbgNormal, 1.0);
    }
    else if (abs(DebugMode - 2.0) < 0.5) {
        // Modo 2: tangentes
        fragColor = vec4(dbgTangent, 1.0);
    }
    else if (abs(DebugMode - 3.0) < 0.5) {
        // Modo 3: bitangentes
        fragColor = vec4(dbgBitangent, 1.0);
    }
    else if (abs(DebugMode - 4.0) < 0.5) {
        if (bModelSpace) {
            // Modo 4 MSN: compare textured normal vs untextured (v_msnMatrix * Z-up)
            vec3 msnN = normalize(normalMap.rbg * 2.0 - 1.0);
            vec3 nA = normalize(v_msnMatrix * msnN);
            vec3 nB = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));

            float errN = 0.5 * length(nA - nB);
            float E = clamp(errN, 0.0, 1.0);
            float good = 1.0 - smoothstep(0.0, 0.15, E);
            float bad  = smoothstep(0.0, 0.15, E);

            fragColor = vec4(bad, good, 0.5, 1.0);
        } else {
            // Modo 4 TBN: error comparison between mv_tbn and Gram-Schmidt corrected TBN
            vec3 Tm = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
            vec3 Bm = normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
            vec3 Nm = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));

            vec3 Tgs = normalize(Tm - Nm * dot(Nm, Tm));
            vec3 Bx  = normalize(cross(Nm, Tgs));
            float h  = sign(dot(Bm, Bx));
            mat3 tbn_fixed = mat3(Tgs, Bx * h, Nm);

            vec3 n_ts = vec3(0.0, 0.0, 1.0);
            vec3 nA;
            vec3 nB;
            if (bShowTexture && bNormalMap) {
                vec3 nm = texture(texNormal, uv).rgb * 2.0 - 1.0;
                nm.z = sqrt(max(FLT_EPSILON, 1.0 - dot(nm.xy, nm.xy)));
                n_ts = nm;
                nA = normalize(mv_tbn   * n_ts);
                nB = normalize(tbn_fixed * n_ts);
            } else {
                nA = normalize(mv_tbn   * n_ts);
                nB = normalize(tbn_fixed * n_ts);
            }

            float errN = 0.5 * length(nA - nB);

            float IA = max(dot(nA, lightFrontal), 0.0)
                     + max(dot(nA, lightDirectional0), 0.0)
                     + max(dot(nA, lightDirectional1), 0.0)
                     + max(dot(nA, lightDirectional2), 0.0);

            float IB = max(dot(nB, lightFrontal), 0.0)
                     + max(dot(nB, lightDirectional0), 0.0)
                     + max(dot(nB, lightDirectional1), 0.0)
                     + max(dot(nB, lightDirectional2), 0.0);

            float errL = abs(IA - IB);

            float E = clamp(max(errN, errL), 0.0, 1.0);

            float good = 1.0 - smoothstep(0.0, 0.15, E);
            float bad  = smoothstep(0.0, 0.15, E);
            float hvis = h * 0.5 + 0.5;

            fragColor = vec4(bad, good, hvis, 1.0);
        }
        return;
    }
}
//===================END DEBUG MODE=======================

if (bHide)
	    {
            discard;
	    }

  	if (bApplyZap) // Codigo Manolo para el ZAP
    {
  //  if (!bShowMask)
   // {
  	    if (ZappedVert==1)
	    {
    	    discard;
	    }
        }
    //}

   	if (!bWireframe)
	{
		// BGSM: apply material alpha (NifSkope sk_default.frag does this)
		// BGEM: alpha already baked as effectBaseColorAlpha^2 (NifSkope sk_effectshader.frag does NOT)
		if (!bIsEffectShader)
			fragColor.a *= alpha;

		if (bAlphaTest)
			if (fragColor.a <= alphaThreshold) // GL_GREATER
				discard;

	}

}
"
    Sub New()
        MyBase.New(Vertex_SSE, Fragment_SSE)
    End Sub
End Class
Public MustInherit Class Shader_Base_Class
    Implements IDisposable

    Private disposedValue As Boolean

    Private program As Integer
    ' Método público para liberar recursos.
    Private ReadOnly UniformLocationCache As New Dictionary(Of String, Integer)
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(disposing:=True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If program > 0 And disposing Then
                UniformLocationCache.Clear()
                GL.DeleteProgram(program)
                program = 0
            End If
        End If
        disposedValue = True
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(disposing:=False)
        MyBase.Finalize()
    End Sub

    Public Sub New(VertexShaderSource, FragmentShaderSource)
        Dim vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource)
        Dim fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource)

        program = GL.CreateProgram()
        GL.AttachShader(program, vertexShader)
        GL.AttachShader(program, fragmentShader)
        GL.LinkProgram(program)

        Dim linkStatus As Integer
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, linkStatus)
        If linkStatus <> CInt(All.True) Then
            Dim linkInfo = GL.GetProgramInfoLog(program)
            Throw New Exception($"Shader program link error: {linkInfo}")
        End If

        GL.DetachShader(program, vertexShader)
        GL.DetachShader(program, fragmentShader)
        GL.DeleteShader(vertexShader)
        GL.DeleteShader(fragmentShader)
    End Sub

    Private Shared Function CompileShader(type As ShaderType, source As String) As Integer
        Dim shader = GL.CreateShader(type)
        GL.ShaderSource(shader, source)
        GL.CompileShader(shader)

        Dim compileStatus As Integer
        GL.GetShader(shader, ShaderParameter.CompileStatus, compileStatus)
        If compileStatus <> CInt(All.True) Then
            Dim info = GL.GetShaderInfoLog(shader)
            Throw New Exception($"Error compiling {type}: {info}")
        End If

        Return shader
    End Function

    Public Sub Use()
        GL.UseProgram(program)
    End Sub
    Private Function GetUniformLocationCached(name As String) As Integer
        Dim loc As Integer
        If UniformLocationCache.TryGetValue(name, loc) Then Return loc

        loc = GL.GetUniformLocation(program, name)
        UniformLocationCache(name) = loc
        Return loc
    End Function

    Public Debugmode As Integer = 0
    Public Shared Function Color_to_Vector(color As Color) As Vector3
        Return New Vector3(color.R / 255.0F, color.G / 255.0F, color.B / 255.0F)
    End Function

    ''' <summary>Color sRGB-&gt;lineal (powf 2.2), como sube el engine los colores de material al CB
    ''' (Fallout4.exe SetupMaterial, DAT_142475358=2.2). Usar SOLO cuando LinearPipeline esta ON;
    ''' gateado en los call-sites de Render.vb (el helper lo comparten Fragment_FO4 y Fragment_SSE).</summary>
    Public Shared Function Color_to_Vector_Linear(color As Color) As Vector3
        Return New Vector3(CSng(Math.Pow(color.R / 255.0F, 2.2)),
                           CSng(Math.Pow(color.G / 255.0F, 2.2)),
                           CSng(Math.Pow(color.B / 255.0F, 2.2)))
    End Function

    ''' <summary>Vector3 sRGB-&gt;lineal (powf 2.2) por componente. Para el light-rig (Ambient/diffuse,
    ''' autorizado en espacio perceptual) al subirlo cuando LinearPipeline esta ON: deja el termino
    ''' difuso identico al render legacy (luz_lin*albedo_lin, luego encode C3) y evita el sobre-brillo
    ''' de ambient/specular. Gateado en los call-sites de Render.vb.</summary>
    Public Shared Function Vector_to_Linear(v As Vector3) As Vector3
        Return New Vector3(CSng(Math.Pow(v.X, 2.2)),
                           CSng(Math.Pow(v.Y, 2.2)),
                           CSng(Math.Pow(v.Z, 2.2)))
    End Function
    Public Sub SetFloat(name As String, value As Single)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform1(loc, value)
        End If
    End Sub

    Public Sub SetInt(name As String, value As Integer)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform1(loc, value)
        End If
    End Sub

    Public Sub SetBool(name As String, value As Boolean)
        SetInt(name, If(value, 1, 0))
    End Sub

    Public Sub SetVector2(name As String, value As Vector2)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform2(loc, value.X, value.Y)
        End If
    End Sub

    Public Sub SetVector3(name As String, value As Vector3)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform3(loc, value.X, value.Y, value.Z)
        End If
    End Sub

    Public Sub SetVector4(name As String, value As Vector4)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform4(loc, value.X, value.Y, value.Z, value.W)
        End If
    End Sub

    Public Sub SetMatrix3(name As String, value As Matrix3)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.UniformMatrix3(loc, False, value)
        End If
    End Sub

    Public Sub SetMatrix4(name As String, value As Matrix4)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.UniformMatrix4(loc, False, value)
        End If
    End Sub

    Public Sub BindTexture(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
    End Sub

    Public Sub BindCubeMap(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.TextureCubeMap, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
    End Sub
End Class

