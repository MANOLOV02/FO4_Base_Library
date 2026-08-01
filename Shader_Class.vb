' Version Uploaded of Fo4Library 3.2.0
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics


Public Class Floor_Shader_Class
    Inherits Shader_Base_Class
    Friend Const Vertex_Floor As String =
"#version 430
layout(location = 0) in vec3 vertexPosition;

uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;

void main()
{
    gl_Position = matProjection * matView * matModel * vec4(vertexPosition, 1.0);
}"

    Friend Const Fragment_Floor As String =
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
    Friend Const Vertex_FO4 As String = "
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
// SYNC: CPU/GPU skinning. The blend here has FIVE twin sites; changing weights,
// fallback or matrix composition in one of them WITHOUT the others is a silent bug
// (it compiles, throws nothing, and only the other path renders wrong):
//   1. This shader block - DUPLICATED in the FO4 and the SSE vertex shader.
//   2. SkinningHelper.BlendBoneMatrices        (CPU blend, double precision)
//   3. SkinningHelper.RecomputeGPUBoneMatrices (bone matrix composition -> SSBO)
//   4. SkinningHelper.ExtractSkinnedGeometry   (GPU arrays: idx/weights, sum=1)
//   5. Render.UpdateSkinBuffers_GL             (CPU pre-skin path)
//   + SkinBakeMath / FaceGenBuildPipeline      (the bake, same formula)
// Differences BY DESIGN (not drift):
//   - GPU: float precision, weights pre-normalized at extract (sum=1).
//   - CPU: double precision, normalized at runtime (1/sumW).
//   - GPU applies transpose(inverse(mat3)) to N/T/B; CPU keeps them in local
//     space and lets the shader transform them.
// Parity test: flip Setting_GPUSkinning on a posed/morphed shape - must look identical.
// See memory 00-reglas-ui-y-vb.md (section 10) and 00-reglas-comentarios.md.

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

out vec3 viewDirRaw;
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

	viewDirRaw = normalize(-vPos);
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
    Friend Const Fragment_FO4 As String = "
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

in vec3 viewDirRaw;
in mat3 mv_tbn;
in mat3 v_msnMatrix;   // MSN: object->view normal matrix from the vertex shader

in float maskFactor;
flat in int ZappedVert;
in vec3 weightColor;

in vec4 vColor;
in vec2 vUV;

out vec4 fragColor;

// El engine RENORMALIZA el vector de vista POR PIXEL. Los 18 PS de BSLighting de FO4 (la poblacion
// COMPLETA del bloque b06 en Shaders011.fxp) abren con
//     dp3 r0.x, v6.xyzx, v6.xyzx ; rsq r0.x, r0.x ; mul r0.yzw, r0.xxxx, v6.xxyz
// y de ahi salen el half-vector (`mad r7.yzw, v6.xxyz, r0.xxxx, cb2[0].xxyz`), N.V y la reflexion
// del cubemap. El VS de la app ya emitia normalize(-vPos), pero la INTERPOLACION lo desnormaliza a
// lo ancho del triangulo. viewDirRaw = varying crudo; viewDir = unitario, fijado al entrar a main().
vec3 viewDir = vec3(0.0);

vec3 normal = vec3(0.0);
float specGloss = 1.0;
float specFactor = 1.0;

vec2 uv = vec2(0.0);
vec3 albedo = vec3(0.0);
vec3 emissive = vec3(0.0);

vec4 baseMap = vec4(0.0);
// Equivalente al r1 del motor = el diffuse COMPUESTO (con el overlay de FaceTint ya aplicado) y SIN
// vColor. `baseMap` NO sirve para eso: es el t0 crudo, antes del overlay. En el motor r1 ES la cabeza
// ya horneada por la pasada b12 FaceCustom -- la app parte esa textura en diffuse + overlay, asi que
// el analogo fiel es el compuesto, no la mitad. Usarlo en subsurface y transmision evita que esos dos
// terminos corran sobre piel SIN TINTAR mientras el resto de la iluminacion usa la tintada.
//
// LA LEY, medida sobre los 18 PS del forward (poblacion COMPLETA del bloque b06):
// **los tres consumidores -- transmision, subsurface y el multiply del albedo -- leen SIEMPRE EL MISMO
// REGISTRO r1. 18 de 18, sin excepcion.** Eso es lo que obliga a que `diffuseComposed` siga al albedo:
// congelarlo en el sample (que es lo que hacia) dejaba subsurface y transmision corriendo sobre la
// textura base mientras el difuso principal usaba la ya procesada.
// QUE ES r1 depende de la tecnica, y NO siempre esta procesado -- en 11 de los 18 es el sample crudo de
// t0 y ahi `diffuseComposed = diffRgb` ya era correcto. Los 7 donde importa:
//   rec1499 (tecnica 4 FACE)      L70-72 : la curva `2a-a*a` escribe r1  -> ANTES de L146 y L285
//   rec1500 (tecnica 5 SKIN_TINT) L70-73 : la curva `a*a + 2*a*tint*(1-a)` escribe r1 -> ANTES de L147 y L286
//   los 5 con GRADIENT_REMAP (0x041,0x051,0x141,0x641,0x651): r1 = sample_l del LUT de paleta en t15,
//     o sea un REEMPLAZO TOTAL, no una curva. En rec1504 (0x641): L68 el sample, L154 la transmision
//     (`mul r7.yzw, r1.xxzw, cb1[7].yyyy`), L164 el subsurface, L293 el multiply del albedo.
//     Esos numeros valen para 4 de los 5; 0x141 tiene la misma estructura con otras lineas (71/157/167/311).
// PRECISION sobre L293: es el multiply del ALBEDO, no la ultima instruccion. En rec1504 la cola real es
// L296 `mad o0.xyz, r0.xyzx, r1.xzwx, r8.yzwy`, y para entonces r1.xzw ya fue PISADO en L294-295 por
// `lerp(1, cb1[1].xyz, v7.y)` (el tinte de pelo). O sea el registro se reusa despues; lo que importa aca
// es el valor que tiene mientras alimenta a los tres consumidores, y ese es el mismo para los tres.
//
// NO depende de este inicializador: main() lo asigna INCONDICIONALMENTE junto al albedo (ver la nota de
// la invariante ahi). El valor de aca es solo para que la global este definida.
vec3 diffuseComposed = vec3(1.0);
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
	// El CLAMP A 15 es DEL MOTOR, no un limite defensivo. Cadena exacta del forward de FO4
	// (b06 rec1498 L185-189; identica en el loop de luces puntuales L267-271):
	//     mul r3.z, r7.y, r3.z          ; G * F
	//     mul r3.z, r7.w, r3.z          ; * D
	//     mul r3.z, r3.z, l(0.250000)   ; / 4
	//     min r3.z, r3.z, l(15.000000)  ; <<< ACA
	//     mul r3.z, r3.z, r3.y          ; * (specMask * SpecMult * PI)
	// El min entra ANTES de multiplicar por la mascara y la fuerza del material, no despues.
	// Sin el, con NdotH -> 1 y exponente alto (power = exp2(Smoothness*10+1) llega a 2048) el
	// termino D = (power+2)/(2*PI) se dispara y el highlight revienta en vez de saturar.
	float spec = min((F * G_NdotV * D) / 4.0, 15.0);

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

// Soft-light W3C/Photoshop del tono de piel sobre un diffuse LINEAL, devuelto en LINEAL.
// Extraido tal cual del bloque uEffectiveType==4 para poder aplicarlo a los DOS analogos del r1 del
// motor (el albedo con vColor plegado, y diffuseComposed sin el) con la misma curva y sin duplicarla.
// La matematica es identica a la que estaba inline: no cambia el resultado del albedo.
vec3 skinToneSoftLight(in vec3 base, in vec3 tone)
{
	vec3 baseD = pow(max(base, 0.0), vec3(1.0/2.2));      // linear diffuse -> display
	vec3 blendD = pow(max(tone, 0.0), vec3(1.0/2.2));     // linear tone -> display
	vec3 loSL = 2.0 * baseD * blendD + baseD * baseD * (1.0 - 2.0 * blendD);
	vec3 hiSL = 2.0 * baseD * (1.0 - blendD) + sqrt(max(baseD, 0.0)) * (2.0 * blendD - 1.0);
	vec3 slR;
	slR.x = (blendD.x < 0.5) ? loSL.x : hiSL.x;
	slR.y = (blendD.y < 0.5) ? loSL.y : hiSL.y;
	slR.z = (blendD.z < 0.5) ? loSL.z : hiSL.z;
	return pow(max(slR, 0.0), vec3(2.2));
}

// isKeyLight = esta luz hace de la UNICA direccional del motor (el sol). El rig del preview tiene 4
// luces (key + 2 de relleno + una TRASERA dedicada), pero el forward de FO4 tiene UNA direccional mas
// un loop de luces PUNTUALES, y hay terminos que el motor aplica SOLO a la direccional.
// MEDIDO: cb1[7] aparece en las lineas 142/146/147 de rec1498 y el loop de puntuales va de 202 a 276:
// NINGUNA de las tres cae adentro. O sea la transmision (cb1[7].y) y el rolloff del subsurface
// (cb1[7].x) son EXCLUSIVOS de la direccional. Aplicarlos a las 4 luces del rig no es fidelidad: con
// la luz TRASERA del rig sat(-N.L) vale ~0.89 sobre toda la cara visible, y el termino deja de ser un
// borde para volverse un lavado plano -- el mismo modo de falla por el que el rim de NifSkope de mas
// abajo esta desactivado.
void directionalLight(in DirectionalLight light, in vec3 lightDir, in bool isKeyLight, inout vec3 outDiffuse, inout vec3 outSpec)
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
			// SIN specularColor: FO4 NO tiene color especular por material, MEDIDO EN LOS DOS CAMINOS.
			//   FORWARD  : en los 18 PS de b06, cb2[11] aparece SOLO con .x y .y -- dos escalares
			//              (Smoothness y SpecMult), ningun swizzle de 3 componentes. La cadena termina
			//              en `mul r8.yzw, r3.zzzz, cb2[1].xxyz` = el COLOR DE LA LUZ, y nada mas.
			//   DIFERIDO : el G-buffer NO transporta un color especular. CORRECCION del conteo que puse
			//              antes (`0 de 470 escriben o3 con 3+ componentes`): es FALSO, los 470/470 lo
			//              escriben con 3 o 4. Pero son ESCALARES EMPAQUETADOS, no un color: o3.z es
			//              `cb2[0].w * 0.010000` uniforme y el composite hace `r1.z*255` y lo compara
			//              contra 2 y 3 -- es un MATERIAL ID. Solo 22 de los 470 escriben o3.xyz con
			//              tres dp3, y eso es un vector de mundo, no un tinte.
			//              La conclusion se sostiene; la evidencia que yo habia citado, no.
			// OJO: el campo SI existe y esta autorado -- 1423 de los 6616 BGSM vanilla traen SpecularColor
			// distinto de blanco. El motor simplemente lo IGNORA en el forward. O sea el uniform no es
			// basura, es un dato real que este camino no consume.
			// O sea el tinte del highlight sale unicamente de la luz. El `* specularColor` que habia
			// aca era agregado. (En SSE si existe: alli es cb1[4].xyz, por eso Fragment_SSE lo conserva.)
			outSpec += TorranceSparrow(NdotL0, NdotH, NdotV, VdotH, vec3(specMask), fSpecularPower, 0.2) * NdotL0 * light.diffuse;
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
	//
	// SIN GATE, a proposito. Se probo un `if (isKeyLight)` aca y SE SACO: no estaba justificado.
	// El sintoma que motivo el intento (rebordes blancos en el cartilago de la oreja, dentro de la nariz
	// y entre los labios al subir la BackLight del rig) resulto NO ser este termino. Con el gate puesto
	// y el binario ya actualizado el sintoma SEGUIA, y la pista que lo cerro fue del usuario: inclinando
	// un poco hacia arriba la luz trasera, desaparece. Eso es `max(N.L,0)` puro -- el DIFUSO comun.
	// Ademas ese material tiene Smoothness = 1 => roughness = 0 => C2 = 0 en el Oren-Nayar, o sea el
	// difuso degenera EXACTAMENTE a Lambert. El interior de la nariz mira geometricamente hacia la luz
	// trasera y la recibe; en el juego la nariz le hace SOMBRA y queda oscuro, pero el preview no tiene
	// sombras (el motor multiplica todo el acumulador por r2.x, el lookup del shadow map de rec1498
	// L77-107, forzado a 0.0/1.0 y aplicado en L142/L154). No hay termino roto: falta oclusion.
	// Como el motor SI aplica este rim en todas sus luces, incluido el loop de puntuales (rec1498
	// L234-239, medido), gatearlo era una desviacion sin nada que la comprara. Queda fiel.
	// (Este bloque es FO4-only: Fragment_SSE no tiene el termino; alli el backlight sale de una textura.)
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

	// Soft Lighting -- subsurface con rolloff (cb1[7].x). Resta saturate(NdotL), NO el termino de
	// OrenNayar (rec1498 L147-153, verificado en el asm).
	// TRES correcciones, las tres MEDIDAS, y las tres son el mismo defecto que tenia la transmision
	// de mas abajo (se arreglaron juntas; dejar una y no la otra no tenia sentido):
	//  1) SOLO LA DIRECCIONAL. cb1[7].x esta en las lineas 146/147 de rec1498 y el loop de luces
	//     PUNTUALES va de 202 a 276 -> queda AFUERA. El motor no le da subsurface a las puntuales.
	//     La app se lo daba a las 4 luces del rig, incluida la TRASERA, que es donde mas dispara.
	//  2) GATE bSoftlight RESTAURADO. El motor lo tiene incondicional en 18/18, y yo lo habia quitado
	//     diciendo que era `casi inocuo` porque con rolloff = 0 el wrap se anula solo. Eso es FALSO
	//     sobre el corpus real: parseando los 6616 BGSM de Fallout4 - Materials.ba2 (los 6616 con
	//     offset final == EOF exacto), **6183 tienen SubsurfaceLighting = False con rolloff > 0**, y
	//     6152 de ellos con rolloff exactamente 0.3. Render.vb sube el rolloff CRUDO, asi que sin el
	//     gate el 93% del corpus vanilla recibia subsurface que su propio flag apaga -- incluida la
	//     cabeza masculina vanilla (basehumanskinHead.bgsm: sss = False, rolloff = 0.5).
	//     El motor recibe el valor YA gateado por su loader; la app tiene el flag y el valor por
	//     separado, asi que el gate es lo que reproduce esa entrada, no un invento.
	if (bSoftlight && isKeyLight)
	{
		float wrapR = clamp((NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff), 0.0, 1.0);
		outDiffuse += clamp(wrapR - clamp(NdotL, 0.0, 1.0), 0.0, 1.0) * diffuseComposed * light.diffuse;
	}

	// TRANSMISION AUTORADA (back-translucency). RE-AGREGADA: la nota anterior decia que el motor de FO4
	// NO tiene transmision y que el rim de arriba era el UNICO backlight, `verificado: los 18 del forward
	// tienen exactamente UN dot con -lightDir`. Eso es FALSO: hay DOS, y el segundo es este.
	// El barrido viejo lo perdio porque busco un producto punto contra el vector de luz NEGADO, y el
	// motor no hace eso: reusa el N.L ya calculado y NIEGA EL ESCALAR (`mov_sat rX, -rY`), que ningun
	// grep de `dot con -L` encuentra.
	// Medido en b06 rec1498 L142-145, y presente en **18 de 18** (incondicional, sin define que lo gatee):
	//     mul     r7.yzw, r1.xxyz, cb1[7].yyyy   ; diffuse(t0) * BackLightPower
	//     mov_sat r8.x,   -r3.z                  ; saturate(-(N.L))   [r3.z = dot(L,N) de L109]
	//     mul     r7.yzw, r7.yyzw, r8.xxxx
	//     mad     r6.xzw, cb2[1].xxyz, r7.yyzw, r6.xxzw   ; ACUMULADOR DE LUZ += lightColor * eso
	// cb1[7].x ya estaba identificado como SubsurfaceRolloff (L146 lo usa para el wrap del sss), y
	// cb1[7].y es el BackLightPower del material -> uniform backlightPower (Render.vb:3558, vale 0
	// cuando el material no tiene backlight, por eso NO hace falta gate por bBacklight: replica el
	// incondicional del motor y queda inerte solo).
	// Va al acumulador de DIFUSO igual que el motor, o sea que el composite lo vuelve a multiplicar por
	// el albedo: el resultado final es albedo^2 * BackLightPower * sat(-N.L) * lightColor. Eso es lo que
	// dice la instruccion (r1 entra aca Y en el multiply final), no un descuido.
	// OJO: NO es el backlight de SSE. SSE lo saca de una TEXTURA (slot 7): sat(-N.L)*backlightTex*
	// lightColor. FO4 lo saca de un ESCALAR del material sobre el propio diffuse. Los dos shaders
	// difieren aca porque los motores difieren, y ahora los dos estan medidos.
	// vColor UNA sola vez: el motor multiplica por r1 (rec1498 L142) y aplica el vertex color recien
	// en la cola. Si aca se usara el global `albedo` -- que ya lleva pow(vColor,2.2) plegado -- el
	// composite lo volveria a multiplicar y el vColor quedaria al CUADRADO. Se usa `diffuseComposed`,
	// que es el diffuse con el overlay de FaceTint ya aplicado y SIN vColor: ese es el analogo de r1
	// en esta app (ver la nota de su declaracion). NO es baseMap.rgb, que es el t0 previo al overlay.
	if (isKeyLight)
		outDiffuse += diffuseComposed * backlightPower * clamp(-NdotL, 0.0, 1.0) * light.diffuse;
}

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
}

// Hemispheric ambient = INVENCION DE PREVIEW. La justificacion que estaba aca (`FO4/SSE iluminan el ambiente como termino dependiente de la normal, DirectionalAmbient . vec4(N,1)`) es FALSA para
// el FORWARD de FO4: el ambiente ahi es un vec3 PLANO, `add r0.xyz, r6.xzwx, cb2[3].yzwy` en 16/18,
// y en los dos Glowmap `mad r0.xyz, cb2[3].yzwy, r0.xyzx, r6.xzwx` -- sin dot con N y sin matriz.
// Lo direccional en FO4 existe SOLO en el diferido y por otro mecanismo: un cubemap array de probes
// IBL en el composite (b11 rec3401, `dcl_resource_texturecubearray t8`, 80/180 PS del bloque).
// O sea el hemisferio de abajo NO replica al motor de FO4: es una decision de preview (da volumen sin
// tener la matriz de ambiente de la celda). Se conserva por eso, con la justificacion corregida.
// (En SSE la afirmacion SI se sostiene: alli el ambiente es `dp4 cb2[11..13] . vec4(N,1)`.)
// (-Z), mezclados por la componente Z de la normal llevada a mundo:
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
	viewDir = normalize(viewDirRaw);   // engine: rsq(dot(v6,v6)) por pixel, ver la nota del varying
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
	// El pelo del FORWARD NO lleva vColor: en la tecnica 6 la cola es `mad o0.xyz, r0, r1, spec`
	// con r1 = lerp(1, HairTintColor, vColor.g) -- el tint OCUPA EL LUGAR del vertex color, no se
	// suma a el (la tecnica 2, en cambio, cierra con `mad o0.xyz, r0, v7.xyzx, spec`). La app
	// plegaba vColor aca Y aplicaba el tint mas abajo, o sea los dos. Se excluye el fold en el mismo
	// caso EXACTO en que se aplica el tint (tipo 5 + alpha-blend), para no dejar al pelo alpha-test
	// -- que va por el diferido y no lleva el lerp -- sin vColor y sin tint.
	// INVARIANTE DEL SHADER, no un default: `albedo == vcFold * diffuseComposed` en todo momento.
	// diffuseComposed es el albedo SIN el vertex color = el analogo del r1 del motor. Los dos se asignan
	// JUNTOS, aca y dentro de bShowTexture, para que no puedan desincronizarse. Antes diffuseComposed
	// dependia de un inicializador global y se quedaba en su valor inicial cuando bShowTexture era false.
	// El diffuse `neutro` es BLANCO, no negro: con la textura apagada el albedo vale vcFold, o sea
	// diffuseComposed = 1. Eso es exactamente lo que hacia el shader antes de introducir la variable
	// (el subsurface multiplicaba por `albedo` directamente), asi que la vista sin textura no cambia.
	// REVERTIDA la exclusion del vColor en el pelo (`(uEffectiveType==5 && bHasAlphaBlend) ? vec3(1.0)`).
	// La medicion que la motivaba SIGUE SIENDO CIERTA: en la tecnica 6 del forward el PS ni siquiera
	// recibe el RGB del vertex color (`dcl_input_ps linear v7.yw` en rec1502/1503, `v7.xyw` en
	// rec1504/1505 -- nunca .z) y el lerp del tint OCUPA su lugar. Pero en ESTA app no compraba nada:
	//  1) INERTE en la practica. NpcMaterialResolver (:152) fuerza GrayscaleToPaletteColor=True en TODO
	//     el pelo, asi que corre el bloque de recolor de paleta, que PISA `albedo` entero unas lineas
	//     mas abajo y descarta este valor. Solo llegaba a importar en pelo SIN paleta, que aca no hay.
	//  2) TENIA COSTO. `vColor.rgb` no es solo el vertex color de la malla: el VS le pliega `subColor`
	//     (el tinte por-shape de Wardrobe Manager) y el wirecolor. Forzar 1.0 los mataba a los tres.
	// O sea: cero beneficio medible y una regresion real. Vuelve al comportamiento de HEAD.
	vec3 vcFold = pow(max(vColor.rgb, 0.0), vec3(2.2));
	albedo = vcFold;
	diffuseComposed = vec3(1.0);
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
			diffuseComposed = diffRgb;   // = el r1 del motor (overlay ya compuesto, sin vColor)
			albedo *= diffRgb;

			// Diffuse texture without lighting
			color.rgb = albedo;

			// El sampleo del normal map se SACO de adentro de `if (bLightEnabled)`. Motivo: el bloque
			// del cubemap del EFFECT shader (BGEM, mas abajo) NO esta anidado bajo bLightEnabled y sin
			// embargo lee normalMap.a y `normal`. Hoy eso no explota por un solo motivo: el uniform esta
			// CABLEADO a True en el unico call-site que existe (Render.vb:3343 `SetBool(bLightEnabled,
			// True)`; no hay otro seteo en todo el arbol). Era una trampa latente, no un bug vivo.
			// Sacar el sampleo de aca es IDENTICO en comportamiento mientras el uniform sea True, y
			// elimina la mitad de la trampa sin reestructurar nada.
			// LO QUE QUEDA: `normal` se sigue calculando solo dentro de bLightEnabled (init vec3(0.0)).
			// O sea `bLightEnabled = False` NO es un estado soportado para el camino BGEM: su falloff y
			// su cubemap dependen de valores derivados de la iluminacion. Si alguna vez se agrega un
			// call-site que lo ponga en False, hay que subir tambien el calculo de `normal`.
			// (En Fragment_SSE esto NO pasa: alli el bloque del cubemap SI esta anidado bajo
			// bLightEnabled, asi que las condiciones para llegar al multiply son exactamente las del
			// sampleo. Verificado contando profundidad de llaves, no por indentacion.)
			if (bNormalMap)
			{
				normalMap = texture(texNormal, uv);
			}

			if (bLightEnabled)
			{
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
					// El recolor PISA el albedo y con eso descarta el vColor, igual que el motor: en las
					// tecnicas con GRADIENT_REMAP el multiplicador final es r1 (la paleta) y v7.x se consume
					// como coordenada V del LUT, no como factor. Asi que aca albedo YA es el analogo exacto
					// de r1 -- sin vColor -- y diffuseComposed lo copia tal cual (rec1504 L68/L154/L164/L293).
					diffuseComposed = albedo;
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
			// `bHasTintColor` AGREGADO. Estaba declarado y NUNCA LEIDO en Fragment_FO4 (solo lo leia
			// Fragment_SSE), asi que la supresion `Ya esta` de Render.vb -- que lo pone en False cuando
			// SkinToneBaked, justamente para que el soft-light NO se aplique dos veces sobre una malla que
			// ya trae el tono horneado en su diffuse -- era INERTE en FO4: la rama se gateaba solo por el
			// tipo y el tono se aplicaba igual.
			// CAVEAT que este gate ACTIVA (preexistente, no lo crea): `SkinToneBaked` es un latch de una
			// sola via -- NpcFaceTintResolver lo pone en True al final de la iteracion INCONDICIONALMENTE,
			// aunque no se haya compuesto nada, y NUNCA lo resetea a False. (La comparacion original era contra
			// SseFoldDetailNeutralized -- su flag hermano, que si se reseteaba. Esa propiedad YA NO EXISTE: se
			// elimino por muerta cuando el fold paso a PRE-COMPENSAR la cadena en vez de neutralizar los slots 3/6.)
			// Camino concreto: edicion viva de tints -> se restaura
			// el diffuse PRISTINE -> el usuario borra todas las capas -> TryApplyFaceTints sale temprano ->
			// el flag queda True con un diffuse sin tono => con este gate esa malla se dibuja SIN tono.
			// Antes era invisible porque el uniform no se leia. El fix correcto es del lado del latch
			// (setearlo solo si esa malla realmente compuso, y bajarlo en el camino no-compuesto).
			// Y OJO: en una cabeza FO4 con el bit SLSF1 `Face` puesto el tipo resuelve a 3, que NO tiene
			// curva de tono, asi que este gate solo muerde el subconjunto DESINCRONIZADO (ShaderType=FaceTint
			// con `Face` apagado y `Skin_Tint` prendido). La `doble aplicacion` NO esta probada en la cabeza
			// estandar. Lo que SI arregla, medido: un leak de uniform -- Render.vb sube `tintColor` solo
			// `If hasTint`, asi que una malla tipo 4 con hasTint=False soft-lighteaba con el tintColor que
			// hubiera dejado la shape anterior en el mismo program.
			if (uEffectiveType == 4 && bHasTintColor)   // SkinTint body: soft-light W3C del tono por actor
			{
				albedo = mix(albedo, skinToneSoftLight(albedo, tintColor), skinTintStrength);
				// MISMA curva sobre diffuseComposed. En el motor la curva de tono de la tecnica 5 se aplica
				// AL PROPIO r1 (rec1500 L70-73 la escribe en r1) y recien despues r1 alimenta la transmision
				// (L147), el subsurface (L157) y el multiply final (L286). Si se tintara solo el albedo, esos
				// dos terminos correrian sobre la piel SIN TONO mientras el difuso principal usa la tintada
				// -- justamente el defecto que diffuseComposed decia evitar.
				// Se recalcula sobre diffuseComposed en vez de derivarlo del albedo porque el albedo ya trae
				// pow(vColor,2.2) plegado y el soft-light NO es lineal: dividirlo no reconstruye la base.
				diffuseComposed = mix(diffuseComposed, skinToneSoftLight(diffuseComposed, tintColor), skinTintStrength);
			}
			// uEffectiveType == 3 (Face / Facegen): NO tone curve -- the face renders its BAKED diffuse RAW.
			// The FaceGen head diffuse is fully baked by the engine BSFaceCustomization pass (b12 FaceCustom
			// rec3582 composites the FaceTint layers AND the skin tone into the head texture), which the
			// renderer samples directly. Applying any tone here double-processes it. This matches Render.vb's
			// own Ya-esta suppression (L3026-3029): SkinToneBaked -> bHasTintColor=false (runtime soft-light
			// forced off). El `albedo = 2*a - a*a` que estaba aca se saco porque rompia la paridad
			// cara/cuerpo (verificado in-app: sacandolo la cara matchea al cuerpo despues del skin tint).
			// CORRECCION de como estaba descrito antes: NO es `a spurious brightening placeholder`.
			// Esa curva EXISTE en el motor y es exactamente la tecnica **Facegen** del forward de FO4.
			// Medido en b06 rec1499 (techID 0x401), diff contra rec1498 (0x001) = +3 instrucciones y nada mas:
			//     add r4.xyz, r1.xyzx, r1.xyzx             ; 2a
			//     mad r4.xyz, -r4.xyzx, r1.xyzx, r4.xyzx   ; 2a*(1-a)
			//     mad r1.xyz, r1.xyzx, r1.xyzx, r4.xyzx    ; a*a + 2a*(1-a) = 2a - a*a
			// con r1 = el sample de t0. O sea softlight con tint = 1.
			// Los bits 8-11 del techID de FO4 son el **enum de TECNICA** (el mismo de Skyrim), no defines:
			// 0x401 = 4 Facegen (curva con tint=1), 0x501 = 5 FacegenRGBTint (la misma curva pero con
			// tint = cb1[1]), 0x6xx = 6 Hair (no lleva la curva: lleva lerp(1,HairTint,vColor.g)),
			// 0xC01 = C TreeAnim (no la lleva). O sea aparece en 1 de los 18 PS, no en 7.
            // Prueba cruzada en SSE: tecnica 4 (idx 8121) es la MISMA curva pero con el tint saliendo de
            // la textura t3, y tecnica 5 (idx 8577) agrega el rgbFix l(1.011719,0.996094,1.011719) que la
            // app ya implementa mas abajo. FO4-Facegen es la degeneracion a tint=1 de esa familia.
			// Lo enciende el SHADER TYPE del BSLightingShaderProperty del NIF, no un flag del BGSM.
			// Se sigue omitiendo aca a proposito, pero por OTRA razon: la app le entrega al shader un
			// diffuse que ya compuso su propio FaceTint aguas arriba, mientras que el motor recibe el t0
			// horneado por la pasada b12 FaceCustom. Si alguna vez se quiere cerrar si corresponde
			// re-agregarla, hace falta un A/B in-app contra un render del juego: no se decide leyendo shaders.

			directionalLight(frontal, lightFrontal, true, outDiffuse, outSpecular);   // key = la direccional del motor
			directionalLight(directional0, lightDirectional0, false, outDiffuse, outSpecular);
			directionalLight(directional1, lightDirectional1, false, outDiffuse, outSpecular);
			directionalLight(directional2, lightDirectional2, false, outDiffuse, outSpecular);

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
			// GATE `!bHasAlphaBlend` AGREGADO. En el FORWARD de FO4 (= el camino ALPHA-BLEND) el motor
			// NO suma emisivo en ningun lado: la tecnica Glowmap MODULA EL AMBIENTE y nada mas
			// (rec1512 L282-283: `sample r0.xyz, v1.xy, t6` ; `mad r0.xyz, cb2[3].yzwy, r0.xyzx,
			// r6.xzwx` -- el glow multiplica cb2[3].yzw, que es el ambiente, y se suma al acumulador
			// de luz). Las 18 colas de b06 son `mad o0.xyz, <alb*(luz+amb)>, <vColor>, <spec>`: CERO
			// sumas de emisivo. El emisivo aditivo existe SOLO en el diferido, donde el prepass escribe
			// o4 = EmitColor (462/470 con 3 componentes) y el composite lo suma -- o sea el camino
			// OPACO. Antes esto sumaba emisivo tambien en alpha-blend, que el forward no hace.
			// REVERTIDO el gate `!bHasAlphaBlend`. La medicion (el forward no suma emisivo; el Glowmap
			// modula el ambiente) es correcta, pero el PREDICADO de la app no significa alpha-blend:
			// Render.vb lo sube como `hasAlphaBlend OrElse EyeEnvironmentMapping`, y hasAlphaBlend a su
			// vez es `AlphaBlendEnabled OrElse Alpha < 1`. Con el gate, un OJO OPACO con emisivo
			// (sintetico, ghoul) perdia el brillo, igual que cualquier material opaco con Alpha = 0.99.
			// Y la compensacion (ambiente *= glowmap) exige ADEMAS uEffectiveType == 2, que casi nunca
			// se alcanza porque ResolveEffectiveType prioriza Eye/Envmap por encima de Glowmap: el
			// resultado neto era PERDER el emisivo sin ganar nada. Para gatearlo bien hace falta un
			// predicado que signifique `este material va por el forward`, que hoy no existe.
			if (bEmissive)
			{
				vec3 emitMask = (bGlowmap && !bHair && !bHasAlphaBlend) ? texture(texGlowmap, uv).rgb : vec3(1.0);
				emissive += emissiveColor * emissiveMultiple * emitMask;
			}

			// Backlight sumado DESPUES del glowmap (orden NifSkope fo4_default.frag:252-296 y
			// sk_default.frag:124-143: el glowmap modula SOLO el self-emissive, NO el backlight
			// de translucencia). Antes el backlight entraba en 'emissive' dentro del loop de luz
			// y el '*= glowMap' lo contaminaba (en pelo, glowTex = el flow map _f).
			// ELIMINADO `emissive += backlightEmissive;`: era CODIGO MUERTO -- backlightEmissive se
			// declaraba en vec3(0.0) y NUNCA se asignaba en ningun lado, asi que sumaba cero. Peor,
			// hacia creer (a mi entre otros) que en FO4 el backlight iba por 'emissive'. No va: la
			// transmision del motor entra al acumulador de DIFUSO (ver directionalLight, rec1498 L142-145).

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
			// bHasAlphaBlend. PREMISA ANTERIOR REFUTADA. Decia que recolor y tint eran mutuamente excluyentes porque
			// `el pelo palette pone HairTintColor = blanco`. NO lo pone: la rama palette de
			// NpcMaterialResolver marca didPalette y NO toca el campo, asi que queda el valor CRUDO del
			// BGSM. Medido sobre el BA2 de Bethesda (6616 BGSM, EOF exacto): de los 18 materiales con
			// Hair = True, **NINGUNO** tiene HairTintColor blanco -- 12 son (0.502,0.502,0.502) y 6 son
			// (0.9882,...). O sea el lerp NO es identidad: con vColor.g = 1 y el gris 0.502 el pelo se
			// oscurece fuerte. Eso PUEDE ser lo que hace el motor (su tecnica 6 hace el mismo lerp y
			// tambien reemplaza al vColor), pero depende de en que espacio de color esta cb1[1], que NO
			// esta verificado.
			// ALCANCE REAL, MEDIDO EN ESTA INSTALACION: la rama dispara en CERO materiales.
			//   vanilla (BA2, 6616): 18 con Hair=True; los 3 que el reorden desvia a este tipo son
			//                        alpha-TEST, y este bloque exige bHasAlphaBlend.
			//   mods (sueltos, 174): 174/174 con g2p=True, HairTintColor (0.502)^3, y **174/174 alpha-TEST**
			//                        -> alpha-BLEND = 0.
			// O sea el reorden Hair-antes-de-Glowmap es CORRECTO pero hoy es INERTE en este arbol: deja de
			// ser codigo muerto recien con pelo alpha-blend, que aca no hay. Si algun dia aparece, esto es
			// lo primero que hay que mirar, y ademas hay que resolver antes lo siguiente:
			// OJO: cb1[1] esta SOBRECARGADO en el motor. En la tecnica HAIR + GRADIENT_REMAP (rec1504 t=0x641)
			//   el MISMO registro es la coordenada V de la PALETA (L67-68, con el sample del LUT en t15) y
			//   el TINT (L294). Por eso los HairTintColor de vanilla son GRISES (0.502 / 0.9882) y no
			//   colores: en pelo-paleta el motor lee ese valor como COORDENADA, no como tinte. La app los
			//   trata como dos cosas independientes (paletteScale vs tintColor). Aplicar tintColor como
			//   color sobre pelo g2p seria usar una coordenada de paleta como multiplicador.
			// GATE `!bGreyscaleColor` REVERTIDO -- lo habia agregado y estaba MAL. Lo desmiente el asm:
			// en HAIR + GRADIENT_REMAP (rec1504 t=0x641) el motor aplica LOS DOS terminos, no uno u otro:
			//   L67  sample_l r1.xzw, (U, cb1[1].x), t15   <- salida de la PALETA
			//   L292 mul  r0.xyz, r1.xzwx, r0.xyzx        <- la luz se multiplica por la PALETA
			//   L293 add  r1.xzw, cb1[1].xxyz, l(-1,..)
			//   L294 mad  r1.xzw, v7.yyyy, r1.xxzw, l(1,..)  <- lerp(1, tint, vColor.g)
			//   L295 mad  o0.xyz, r0.xyzx, r1.xzwx, r8.yzwy  <- y ADEMAS por el TINT
			// Mi argumento era `con g2p el valor ya lo consume paletteScale`: plausible, no medido, y la
			// medicion dice lo contrario. Ademas la premisa `los dos campos son el mismo numero` vale sobre
			// el ARCHIVO pero NO en runtime: NpcMaterialResolver pone paletteScale = RemappingIndex del CLFM
			// del NPC (por actor) y deja tintColor con el valor de disco del BGSM (por material). La app
			// rompe esa igualdad a proposito, asi que el modelo `es un solo registro` no se traslada.
			// PENDIENTE DE MEDIR: el alcance. Yo cense alpha-blend con el campo del BGSM, pero el uniform
			// sale de `AlphaBlendEnabled OrElse Alpha < 1`, y AlphaBlendEnabled viene de la NiAlphaProperty
			// del NIF, NO del BGSM. O sea `10 de 18` y `los 174 de mod no llegan` estan medidos con el
			// predicado EQUIVOCADO y hay que rehacerlos sobre los NIF.
			// EL VECTOR DEL TINT ES MIXTO CUANDO HAY PALETA. Trazado en el binario:
			// BSLightingShader::SetupMaterial = 0x142232EA0 (switch por feature sobre el techID).
			//   rama feature 6 (HairTint) @0x1422331E3: escribe TRES floats en la constante [tabla+0x6D]
			//       pow(mat+0xC0, 2.2) -> .x   pow(mat+0xC4, 2.2) -> .y   pow(mat+0xC8, 2.2) -> .z
			//   y despues CAE en 0x1422330C7, que hace:
			//       test byte [rdi+0x194], 0x40      ; bit GRADIENT_REMAP del techID
			//       movzx ecx, byte [rax+0x6d]       ; LA MISMA constante
			//       mov eax, [rsi+0xB8] ; mov [rdx], eax   ; PISA SOLO .x, en CRUDO (sin pow)
			// mat+0xB8 = GrayscaleToPaletteScale (default 1.0f, escritura gateada por el bit de remap,
			// consumida como V del LUT en el PS). Resultado:
			//   sin paleta : cb1[1] = (pow(tint.r,2.2), pow(tint.g,2.2), pow(tint.b,2.2))
			//   con paleta : cb1[1] = (paletteScale CRUDO, pow(tint.g,2.2), pow(tint.b,2.2))
			// => con paleta el canal ROJO del HairTintColor NUNCA llega al shader; lo reemplaza la escala.
			// `tintColor` de la app ya viene linealizado (Vector_to_Linear = pow 2.2), asi que .g y .b
			// coinciden; `paletteScale` se sube crudo (Render.vb: GrayscaleToPaletteScale), asi que va tal cual.
			// OJO: esto NO es `no apliques el tint con paleta` -- eso lo probe MAL y lo reverti. El motor
			// aplica los dos terminos (rec1504 L292 por la paleta y L296 por el lerp), con las mismas 3
			// instrucciones que 0x601. Lo unico que cambia es DE DONDE sale el .x del vector.
			if (uEffectiveType == 5 && bHasAlphaBlend)
			{
				// REVERTIDO el vector MIXTO `vec3(paletteScale, tintColor.g, tintColor.b)`. Lo puse yo y
				// ROMPIA EL PELO: dejaba el hairline verde/cian. Sintoma reproducido y explicado.
				// La medicion del motor era correcta -- en la tecnica 0x641 (HAIR + GRADIENT_REMAP) el
				// registro cb1[1] esta SOBRECARGADO: su .x es la coordenada V del LUT (rec1504 L67) Y el
				// canal rojo del tint (L294). Pero esa mezcla solo tiene sentido porque en el motor
				// **es UN SOLO numero** cumpliendo los dos roles.
				// EN ESTA APP NO LO ES, y esta roto a proposito: NpcMaterialResolver (:152) fuerza
				// GrayscaleToPaletteColor=True en el pelo y pisa GrayscaleToPaletteScale con el
				// RemappingIndex del CLFM del NPC -- un indice de FILA del LUT, por ACTOR -- mientras
				// tintColor sigue siendo el HairTintColor de disco del BGSM, por MATERIAL. Son dos
				// numeros distintos. Meter el indice en el canal rojo da, con HairTintColor = 0.502
				// (tintColor lineal ~0.216) y un RemappingIndex chico, algo como (0.1, 0.216, 0.216):
				// el ROJO se aplasta al doble que verde y azul => viraje cian/verde.
				// La leccion: una identidad del motor solo se puede copiar si los valores que la
				// sostienen tambien son identicos en la app. Aca no lo son, y estaba escrito.
				color.rgb *= vec3(1.0) + vColor.y * (tintColor - vec3(1.0));
			}

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
				float NdotV_falloff = abs(dot(normal, viewDir));   // viewDir ya es unitario (main lo normaliza)
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

		// Tonemap + encode sRGB: DECISION DE PREVIEW, no el camino de display de BSLighting.
		// CORREGIDO: la nota anterior decia que eran `the BSLighting display path`. No lo son --
		// **0 de los 18** PS de b06 contienen la constante de Hable l(0.150000), y las 18 colas escriben
		// LINEAL a o0 (`mad o0.xyz, ...` sin curva ni pow). En el juego el tonemap y el encode son
		// POST-PROCESO, en otro bloque de shaders, sobre el buffer ya compuesto.
		// El preview no tiene esa pasada de post, asi que si no se hiciera aca los valores HDR (el
		// specular llega a min(...,15) por el propio motor) se recortarian a 1 y el highlight se
		// aplastaria. Se conserva por eso, como afordancia del visor, con la atribucion corregida.
		// Lo que SI esta medido y por eso sigue gateado: el BGEM (b05) no lleva tonemap ni encode en el
		// shader (0 PS de b05 con la curva de Hable) -- su salida es lineal y la compone el blend mode;
		// tonemapear un BGEM lo lava. DebugMode escribe fragColor despues de esto y queda sin encodear.
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
		// COMPARADOR: el engine descarta con `<` estricto, o sea CONSERVA la igualdad (GEQUAL).
		// FO4 rec1498 L284-286:  mad r0.x, r1.w, v7.w, -cb2[3].x ; lt r0.x, r0.x, l(0) ; discard_nz r0.x
		//   -> descarta si (alpha - ref) < 0, es decir si alpha < ref.
		// Identico en SSE (define DO_ALPHA_TEST, +6 instr, con cb11[0].x de ref).
		// El `<=` de la app descartaba tambien alpha == ref. Con alpha de 8 bits y refs tipicas
		// (128/255) la igualdad EXACTA es frecuente en un cutout dibujado a mano, asi que comia una
		// franja de pixeles que el motor conserva. Pasa a `<`.
		if (bAlphaTest)
			if (fragColor.a < alphaThreshold) // GL_GEQUAL (engine: discard si alpha < ref)
				discard;

		// REVERTIDO a `*= alpha`. La MEDICION del motor es correcta -- los 18 PS del forward escriben
		// `mov o0.w, cb2[2].z`, o sea alpha CONSTANTE del material, y el alpha de textura y vertice
		// alimentan solo el test -- pero aplicarla aca rompe DOS cosas que el motor no tiene y la app si:
		//  1) EL ALPHA DE VERTICE ES UNA FEATURE VIVA: el VS hace `if (bShowVertexAlpha) vColor.a =
		//     vertexAlpha`, y el uniform sale de un TOGGLE DEL USUARIO + dato del NIF
		//     (Render.vb: ShowVertexColor AndAlso hasVertexColorData AndAlso Not isTreeAnim).
		//     Con el alpha constante ese degradado desaparece y el toggle deja de hacer nada en FO4.
		//  2) EL PASE DE OVERLAYS (tatuajes / LooksMenu) dibuja LA MISMA geometria como decal coplanar
		//     con SrcAlpha/InvSrcAlpha y DepthMask(False), y su transparencia la lleva el ALPHA DE LA
		//     TEXTURA del overlay (un tatuaje es casi todo transparente). El motor no tiene ese pase.
		//     Si el material del slot es .bgsm -> bIsEffectShader = false -> con alpha constante el
		//     decal sale OPACO y tapa la cabeza entera.
		// O sea: la ley del motor vale para el camino que el motor tiene; este shader ademas sirve
		// pases que el motor no tiene. Se conserva el multiply y se deja la medicion documentada.
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
    Friend Const Vertex_SSE As String = "
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
// SYNC: CPU/GPU skinning. The blend here has FIVE twin sites; changing weights,
// fallback or matrix composition in one of them WITHOUT the others is a silent bug
// (it compiles, throws nothing, and only the other path renders wrong):
//   1. This shader block - DUPLICATED in the FO4 and the SSE vertex shader.
//   2. SkinningHelper.BlendBoneMatrices        (CPU blend, double precision)
//   3. SkinningHelper.RecomputeGPUBoneMatrices (bone matrix composition -> SSBO)
//   4. SkinningHelper.ExtractSkinnedGeometry   (GPU arrays: idx/weights, sum=1)
//   5. Render.UpdateSkinBuffers_GL             (CPU pre-skin path)
//   + SkinBakeMath / FaceGenBuildPipeline      (the bake, same formula)
// Differences BY DESIGN (not drift):
//   - GPU: float precision, weights pre-normalized at extract (sum=1).
//   - CPU: double precision, normalized at runtime (1/sumW).
//   - GPU applies transpose(inverse(mat3)) to N/T/B; CPU keeps them in local
//     space and lets the shader transform them.
// Parity test: flip Setting_GPUSkinning on a posed/morphed shape - must look identical.
// See memory 00-reglas-ui-y-vb.md (section 10) and 00-reglas-comentarios.md.

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

out vec3 viewDirRaw;
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

	viewDirRaw = normalize(-vPos);
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
    Friend Const Fragment_SSE As String = "
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
// SSE facegen: bHasDetailMask gatea TODA la cadena de albedo facegen (softlight con el facetint del
// slot 6 en texGlowmap + amplify del detail del slot 3 en texDetailMask). El engine no gatea por
// textura presente: rellena los slots vacios con sus defaults, y Render.vb hace lo mismo.

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

in vec3 viewDirRaw;
in mat3 mv_tbn;
in mat3 v_msnMatrix;  // MSN: per-vertex local->view (skinning+view combined in vertex shader)

in float maskFactor;
flat in int ZappedVert;
in vec3 weightColor;

in vec4 vColor;
in vec2 vUV;

out vec4 fragColor;

// El engine RENORMALIZA el vector de vista POR PIXEL, no confia en el interpolado: todos los PS de
// BSLightingShader abren con `dp3 r0.x, v6.xyzx, v6.xyzx ; rsq r0.x, r0.x` y recien ahi construyen
// el half-vector (`mad r5.xyz, v6.xyzx, r0.xxxx, cb2[0].xyzx`). El VS de la app ya emitia
// normalize(-vPos), pero la INTERPOLACION lo desnormaliza a lo ancho del triangulo, y de ahi salian
// H, N.V, el rim y la reflexion del cubemap con largo != 1. viewDirRaw = el varying crudo;
// viewDir = su version unitaria, asignada al entrar a main() antes de cualquier uso.
vec3 viewDir = vec3(0.0);

vec3 normal = vec3(0.0);
float specGloss = 1.0;
float specFactor = 1.0;

vec2 uv = vec2(0.0);
vec3 albedo = vec3(0.0);
vec3 emissive = vec3(0.0);

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
			// SIN clamp: el engine NO satura el specular en ningun punto. Cadena medida en el DXBC
			// (Default+SPECULAR 0x00000201, y la misma forma en las 13 tecnicas que llevan el define):
			//   por luz : dp3_sat(H,N) -> log / mul cb1[4].w / exp -> mul lightColor   (se ACUMULA)
			//   al final: mul MASK ; mul cb2[3].y (SpecularStrength) ; mad *cb1[4].xyz (SpecularColor)
			//             y recien ahi se suma sobre el color ya iluminado.
			// O sea mask, fuerza y color entran UNA sola vez al final, y no hay saturate en ningun
			// paso. El clamp per-luz que habia aca recortaba el highlight apenas
			// specularColor*specMask pasaba de 1 (SpecularMult > 1 es corriente), achatando el brillo.
			outSpec += specularColor * specMask * pow(NdotH, glossiness) * light.diffuse;
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
	viewDir = normalize(viewDirRaw);   // engine: rsq(dot(v6,v6)) por pixel, ver la nota del varying
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
					// OJO: ELIMINADA la rama `if (bBacklight)` que forzaba specFactor = normalMap.a.
					// Partia de que con backlight el slot 7 lleva el backlight y NO el specular, o sea que
					// eran EXCLUYENTES. El motor los lee A LA VEZ desde la MISMA textura: TXST slot 7 ->
					// material+0x68 (OnLoadTextureSet 0x1414B7920) y SetupMaterial lo bindea DOS veces --
					// t2 bajo MODELSPACENORMALS (0x14DCB65) y t9 bajo BACK_LIGHTING (0x14DCD22, `bts eax,9`).
					// Son gates independientes: una malla MSN con backlight alimenta specular Y backlight
					// desde el slot 7. Por eso la fuente del mask especular la decide SOLO MODELSPACENORMALS,
					// sin importar el backlight, y el backlight sigue leyendo texSpecular mas arriba.
					if (bHasSpecMap)
					{
						// SSE: el mask especular es de UN SOLO CANAL. Verificado en el DXBC:
						//   forward:  color += pow(N.H, cb1[4].w) * lightColor * MASK * cb2[3].y * cb1[4].rgb
						//   G-buffer: o2.w  = smoothstep(cb2[7].x, cb2[7].y, MASK) * cb2[7].w
						// El EXPONENTE es cb1[4].w = un ESCALAR del material (uniform glossiness), NO un canal
						// de la textura: Skyrim no tiene glossiness por pixel. Por eso specGloss se deja en 1.0
						// y NO se lee .g (eso es la convencion de FO4, ver Fragment_FO4).
						// OJO: el highlight ya usaba el uniform `glossiness` crudo, asi que specGloss no lo
						// tocaba; su unico otro consumidor era el LOD del cubemap, que resulto ser una
						// invencion de la app y ya se elimino (ver el bloque del cubemap). specGloss queda
						// hoy sin efecto real, se conserva por simetria con Fragment_FO4.
						// Cual textura es el MASK lo decide MODELSPACENORMALS, no la presencia del slot 7:
						// Render.vb hace ese gate y bindea aca el slot 7 (t2 del engine).
						specMap = texture(texSpecular, uv);
						specGloss = 1.0;
						specFactor = specMap.r;
					}
					else if (bNormalMap)
					{
						// SSE, malla NO model-space: el mask especular es el ALPHA del normal map (t1.w).
						// Medido sobre la poblacion COMPLETA de BSLightingShader sin terreno/LOD (6864 PS):
						// no-MSN NUNCA samplea t2 (0/6096) y toma el mask de t1.w.
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

			// SSE FACEGEN albedo -- LEY DEL ENGINE (DXBC + SkyrimSE.exe 1.6.1170 unpacked, byte a byte):
			//   albedo = softlight(diffuse, TINT) * ((DETAIL + vec3(1/255,0,1/255)) * 255/64)
			//   softlight(a,b) = a*a + 2*a*b*(1-a)   [pegtop]
			// TINT   = texture-set slot 6 (el facetint horneado) -> PS t3  (entra por SOFT-LIGHT)
			// DETAIL = texture-set slot 3                        -> PS t4  (entra por el AMPLIFY)
			// Cadena DXBC del PS facegen (identica en las 456 variantes que llevan la constante):
			//   sample r2,t4 ; add r2,l(0.003922,0,0.003922) ; mul r2,l(3.984375)
			//   sample r3,t3 ; mul r3,r0,r3 ; add r3,r3,r3 ; mad r3,-r3,r0,r3 ; mad r0,r0,r0,r3
			//   mul r0,r2,r0            <- SIN _sat en ningun paso (no hay clamp, ni aca ni en el engine)
			// Quien es quien (RE):
			//   BSLightingShader::SetupMaterial 0x1414DC310, jump table 0x14DCFD4, rama Facegen 0x1414DC542:
			//     SetPSTexture(3, mat+0xA0)   SetPSTexture(4, mat+0xA8)   SetPSTexture(12, mat+0xB0)
			//   OnLoadTextureSet 0x1414BA6E0: GetTexture(6)->+0xA0, GetTexture(3)->+0xA8, GetTexture(2)->+0xB0
			//   El facetint canonico se escribe en mat+0xA0 (0x1403BC573, tras GetFeature()==4 = 0x1414BAA00).
			// => el x255/64 = 255/64 es la NORMALIZACION DEL DETAIL (neutro 64 -> 1.0), NO del facetint.
			// El facetint entra por soft-light igual que el skin tint del CUERPO (tecnica FacegenRGBTint:
			// softlight(diffuse, cb1[1]) * l(1.011719,0.996094,1.011719)); por eso cuello y pecho matchean
			// in-game: mismo termino softlight, y las constantes difieren solo 0.39% en los 3 canales.
			// Defaults del engine con el slot VACIO (init 0x140E57E30, manager singleton 0x328CC20):
			//   slot 6 vacio -> DefaultGreyMap            = 0x80 = 0.5   => softlight IDENTIDAD
			//   slot 3 vacio -> BSShader_DefFacegenDetail = 0x40 = 0.251 => amplify (1.015625, 1.0, 1.015625)
			// Los bindea Render.vb, por eso aca NO hay gate por textura-presente: el engine tampoco lo tiene.
			if (bHasDetailMask)
			{
				// ENGINE-FAITHFUL vColor ORDER (facegen PS): la cadena corre sobre el diffuse CRUDO (t0),
				// NO sobre vColor*diffuse. vColor (COLOR0) es un multiply FINAL, re-aplicado abajo.
				vec3 fd = baseMap.rgb;
				vec3 tint = texture(texGlowmap, uv).rgb;                    // t3 = facetint (slot 6)
				albedo = fd * fd + 2.0 * fd * tint * (1.0 - fd);            // softlight(diffuse, tint)
				vec3 detailAmp = (texture(texDetailMask, uv).rgb + vec3(0.003922, 0.0, 0.003922)) * 3.984375;
				albedo *= detailAmp;                                        // t4 = detail normalizado (slot 3)
			}

			// Re-apply the mesh vertex color (COLOR0) as the FINAL multiply of the facegen albedo chain,
			// matching the engine order (facegen PS idx 8120 L183: color *= v12). The detail block above
			// rebuilt albedo from the raw diffuse, dropping the fold, so vColor is restored here exactly
			// once. Gated on bHasDetailMask (= facegen); no-op for a white vColor. Before the overlay so
			// the TETI/TEND premultiplied-over sees the same albedo it did previously.
			if (bHasDetailMask)
			{
				albedo *= vColor.rgb;
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
				// ELIMINADO el LOD por glossiness: era una INVENCION de la app, sin respaldo del motor.
				// MEDIDO sobre los 6924 PS de BSLightingShader: `sample_l` y `sample_b` aparecen 0 veces y
				// los 1968 sampleos de cubemap (Envmap 1152 + MLP 624 + Eye 192) usan `sample` PLANO, o sea
				// seleccion de mip por hardware desde las derivadas. SSE no desenfoca la reflexion por
				// glossiness. El `8.0 - x*8.0` salia de leer mal el
				// `mad r0.z, r0.z, l(-8.0), l(8.0); sqrt; max; div; add 0.5`, que es el ENCODE SPHEREMAP de la
				// normal al G-buffer (o2.xy) y aparece igual en shaders que ni siquiera tocan un cubemap.

				// EYE technique (16): the engine reflects the cubemap about the eyeball's RADIAL
				// normal (sse_eye L108-111 reflects about v7), NOT the bump normal that lighting uses
				// (L73-80). The eye VS builds v7 = normalize(worldPos - eyeCenter), eyeCenter =
				// lerp(cb1[0],cb1[1], v6.x) -> a procedural sphere normal, so the iris normal-map does
				// NOT distort the cornea reflection. The eye-center constants + per-vertex blend are not
				// loaded here, but for a spherical eye the radial normal == the mesh geometric normal
				// (mv_tbn[2]); reflecting about it is faithful to the engine (and strictly closer than the
				// bump-normal reflection). Non-eye envmap keeps the bump-normal reflection (sse_envmap L12-14).
				vec3 reflNormal = bEye ? normalize(mv_tbn[2]) : normal;

				// DIRECCION DE REFLEXION -- el signo estaba INVERTIDO en SSE.
				// Engine (Envmap tech 1 y Eye tech 16, identico en los 1968 sampleos de cubemap):
				//     dp3 r3.x, N, Vn ; add r3.x, r3.x, r3.x ; mad r0.xyz, r3.xxxx, N, -Vn
				//   => R = 2*(N.V)*N - V          con V = superficie->ojo (el mismo V del half-vector)
				// GLSL reflect(I,N) = I - 2*dot(N,I)*N, o sea reflect(viewDir,N) = V - 2(N.V)N = -R.
				// La app venia sampleando el cubemap con la direccion OPUESTA (el texel antipodal).
				// reflect(-viewDir, N) da exactamente 2(N.V)N - V.
				// EL SIGNO DEL VARYING TAMBIEN ESTA MEDIDO, no supuesto (medir solo el ALU del PS NO
				// alcanza: si el varying fuera ojo->superficie, la misma formula daria el espejo opuesto).
				//   VS de BSLighting SSE: `add o6.xyz, -r2.xyzx, cb2[6].xyzx` = eye - pos = superficie->ojo,
				//   y lo hacen 78/78 de los VS del bloque que emiten TEXCOORD5.
				// Corroboracion INTERNA al PS, independiente del VS: el half-vector es
				// `mad r5.xyz, v6.xyzx, r0.xxxx, cb2[0].xyzx` = normalize(V) + L, y un half-vector correcto
				// exige V = superficie->ojo. (cb2[0] es L=superficie->luz, probado por el wrap del difuso
				// `div_sat (N.cb2[0] + w)/(1+w)`.)
				// OJO: FO4 hace lo CONTRARIO y por eso NO se toca Fragment_FO4: alli el shader agrega un
				// `mov r0.yzw, -r0.yzw` despues del mad (b06 rec1507 t=0x101), o sea samplea con
				// V - 2(N.V)N = reflect(viewDir,N), que es justo lo que Fragment_FO4 ya hace -- con el
				// MISMO varying superficie->ojo (`add o6.xyz, -r1.xyzx, cb2[7].xyzx`, 18/18 de sus VS).
				// FO4 es antipodal en sus DOS familias (BGSM + BGEM): 205/205 sampleos de cubo llevan ese
				// `mov` final. El raro es FO4, no Skyrim.
				vec3 reflected = reflect(-viewDir, reflNormal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));

				vec4 cube = texture(texCubemap, reflectedWS);
				// Escala del cubemap = cb1[2].x * cb2[3].x (Envmap tech: `mul r3.x, cb1[2].x, cb2[3].x`).
				//   cb1[2].x = EnvmapData.x = el envmap scale del MATERIAL -> uniform envReflection.
				//   cb2[3].x <- BSLightingShaderProperty + 0x104, escrito SOLO en el case Envmap de la
				//               jump-table por tecnica de BSLightingShader::SetupGeometry (0x1414DD21C:
				//               `mov eax,[r14+0x104] ; mov [rcx+rdx*4], eax`, constante #0x47 offset +0).
				// specularStrength NO es ese factor: es cb2[3].**y** <- property+0x100, escrito solo bajo
				// SPECULAR (0x1414DDB80, `bt eax,9`) y usado UNICAMENTE para escalar el specular
				// (`mul r4.xyz, r4.xyzx, cb2[3].yyyy`). Multiplicar el cubemap por el SpecularMult del
				// material era una divergencia lisa y llana; se quita. cb2[3].x queda SIN ligar (la app no
				// tiene ese campo del property) y se asume 1.0, que es el neutro -- no se inventa un valor.
				// FO4 es OTRO caso y por eso Fragment_FO4 SI lleva specularStrength aca: alli el engine
				// multiplica por cb2[11].y = SpecMult (b06 rec1507 L290). Gate por shader, no por uniform.
				cube.rgb *= envReflection;
				if (bEnvMask && !bGlowmap)
				{
					cube.rgb *= envMask.r;
				}
				else
				{
					// Sin env mask, la base del lerp es el ALPHA DEL NORMAL
					// (lerp(normal.a, envMaskTex, EnvmapData.y)). Se lee normalMap.a EXPLICITO y NO
					// specFactor: specFactor es el mask ESPECULAR (sampler t2 del engine), que en SSE
					// cambia de fuente segun MODELSPACENORMALS y puede valer 0 (default negro del
					// slot 7 en piel MSN sin _s). Acoplarlos apagaba la reflexion de mallas MSN.
					// El 1.0 del fallback NO es arbitrario: el motor rellena su slot de normal de forma
					// INCONDICIONAL (default-fill 0x14B7B00, +0x58) con BSShader_DefNormalMap, cuyo fill
					// 0xffff8080 son los bytes RGBA (128,128,255,255) => ALPHA = 255 = 1.0.
					// (Yo habia puesto 0.501961 confundiendo el canal R (0x80) con el alpha (0xff).)
					// El lerp del motor es `lerp(normal.a, t5, cb1[2].y)` con cb1[2].y en {0,1} -- es un
					// selector de si-hay-mascara-bindeada, no un peso libre (SetupMaterial 0x14DC4AB/0x14DCA5D:
					// cmp [rbx+0xA8],0 -> xmm0 = 0 o xmm6=1.0). Por eso esta forma de dos ramas es fiel.
					cube.rgb *= bNormalMap ? normalMap.a : 1.0;
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
				// ENGINE-FAITHFUL vColor ORDER (skin PS idx 8577 L105): the SkinTint soft-light runs on the
				// RAW diffuse (t0), then vColor (COLOR0) multiplies the result. The old code soft-lit
				// vColor*diffuse (vColor folded into the non-linear base), which diverges for a non-white
				// vColor; for white it is bit-identical. vColor commutes with the lighting multiply below.
				vec3 sd = baseMap.rgb;
				sd = sd * sd + 2.0 * sd * tintColor * (1.0 - sd);
				sd *= vec3(1.011719, 0.996094, 1.011719);
				albedo = sd * vColor.rgb;
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
				float NdotV_falloff = abs(dot(normal, viewDir));   // viewDir ya es unitario (main lo normaliza)
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

				// MISMO SIGNO QUE EL BLOQUE BGSM DE ARRIBA. El BSEffectShader de Skyrim NO TIENE
				// CONTRAPARTE MEDIBLE ACA: de sus 3217 PS (bloque idx 78..3901), **0** declaran un
				// texturecube -- el Effect de SSE no refleja cubos (solo t0..t4 2D: greyscale-to-palette,
				// soft-particle depth, y MRT de motion vectors + normal spheremap).
				// OJO CON ESTA TRAMPA (me costo dos vueltas): los 540 PS con texturecube que estan FUERA
				// del rango de BSLighting NO son Effect, son el paquete de **AGUA** (VS 11457..13628 /
				// PS 13629..15800, emparejados 1:1 por offset). Identificarlos por `estar fuera del rango
				// de BSLighting` es exactamente el error que hay que no repetir: hay que medir la familia
				// (el agua se reconoce por 3 normal maps scrolleados t4/t5/t6 y la refraccion t10/t11).
				// Y aun en el agua la reflexion tambien es ESPEJO, porque ahi el varying es ojo->superficie:
				//     VS agua: mov o2.xyz, r1.xyzx con r1 = cb2[0..2]*pos y sqrt o2.w, dot(r1,r1)
				//     PS agua: mad r3.xyz, r3.xyzx, -r1.wwww, r4.xyzx  => reflect(ojo->sup, N) = 2(N.V)N - V
				// Sin referencia propia, se usa la unica referencia de SSE que existe (BSLighting,
				// 1968/1968 sampleos con 2(N.V)N - V) y el espejo fisico. Los dos bloques van igual.
				// El ANTIPODAL es FALLOUT 4, en sus DOS familias (205/205 sampleos de cubo con el
				// `mov ..., -...` final): por eso Fragment_FO4 conserva reflect(viewDir,N) en sus dos
				// bloques. Es el motor de FO4 el raro, no el de Skyrim.
				vec3 reflected = reflect(-viewDir, normal);
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

		// COMPARADOR: el engine descarta con `<` estricto -- CONSERVA la igualdad (GEQUAL).
		// SSE, define DO_ALPHA_TEST (delta de +6 instr, identico en las 11 tecnicas que lo llevan):
		//   mul r0.w, r0.w, cb2[3].z          ; alpha del material
		//   mad r0.x, r0.w, v11.w, -cb11[0].x ; (texAlpha * matAlpha * vColor.a) - AlphaTestRef
		//   lt r0.x, r0.x, l(0.000000) ; discard_nz r0.x
		// -> descarta si alpha < ref. El `<=` de la app tambien descartaba alpha == ref, que con
		// alpha de 8 bits y refs tipicas (128/255) es una franja real de pixeles. Mismo fix en FO4.
		// El ORDEN si coincidia: SSE multiplica el alpha del material ANTES del test (cb2[3].z), que
		// es lo que hace la linea de arriba -- y ahi SSE difiere de FO4, que testea sin el.
		if (bAlphaTest)
			if (fragColor.a < alphaThreshold) // GL_GEQUAL (engine: discard si alpha < ref)
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

