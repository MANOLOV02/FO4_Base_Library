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
	} else {
	    // CPU skinning fallback: vertices already in world space
	    skinnedPos = vertexPosition;
	    skinnedNormal = vertexNormal;
	    skinnedTangent = vertexTangent;
	    skinnedBitangent = vertexBitangent;
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

uniform bool bIsEffectShader;
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

uniform float ambient;
uniform bool bHasTintColor;
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
		float fSpecularPower = exp2(smoothness * 10.0 + 1.0);
		specMask = specFactor * specularStrength;

		outSpec += TorranceSparrow(NdotL0, NdotH, NdotV, VdotH, vec3(specMask), fSpecularPower, 0.2) * NdotL0 * light.diffuse * specularColor;
		outSpec += ambient * specMask * fresnelSchlick(VdotH, 0.2) * (1.0 - NdotV) * light.diffuse;
	}

	// Back lighting: simulates translucency (light through thin cloth/hair)
	if (bBacklight)
	{
		float NdotNegL = max(dot(normal, -lightDir), FLT_EPSILON);
		vec3 backlight = albedo * NdotNegL * clamp(backlightPower, 0.0, 1.0);
		emissive += backlight * light.diffuse;
	}

	// Rim lighting: bright edge when backlit
	if (bRimlight)
	{
		vec3 rim = vec3(pow((1.0 - NdotV), rimlightPower));
		rim *= smoothstep(-0.2, 1.0, dot(-lightDir, viewDir));
		emissive += rim * light.diffuse * specMask;
	}

	// Diffuse
	vec3 diff = vec3(OrenNayarFull(lightDir, viewDir, normal, roughness, NdotL0));
	outDiffuse += diff * light.diffuse;

	// Soft Lighting
	if (bSoftlight)
	{
		float wrap = (NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff);
		vec3 soft = albedo * max(0.0, wrap) * smoothstep(1.0, 0.0, sqrt(diff));
		outDiffuse += soft;
	}
}

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
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

			// Start off neutral
			normal = normalize(mv_tbn * vec3(0.0, 0.0, 0.5));

			if (bShowTexture)
			{
				if (bNormalMap)
				{
					if (bModelSpace)
					{
						// No proper FO4 model space map rendering yet
						//normal = normalize(normalMap.rgb * 2.0 - 1.0);
						//normal.r = -normal.r;
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
                    // BGSM: palette lookup for armor recoloring
                    vec4 luG = colorLookup(baseMap.g, paletteScale - (1.0 - vColor.r));
					albedo = luG.rgb;
				}
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

			// Environment cubemap (BGSM only; BGEM has its own cubemap path)
			if (bCubemap && bEnvMap && bShowTexture && !bIsEffectShader)
			{
				float cubeSmooth = (bSpecular && bShowTexture) ? specGloss * shininess : 1.0;

				vec3 reflected = reflect(viewDir, normal);
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

				outSpecular += cube.rgb * (vec3(ambient) + outDiffuse);
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

			// SkinTint / HairTint: applied after lighting so soft/backlight use untinted albedo (matches NifSkope)
			if (bHasTintColor && !bIsEffectShader)
			{
				albedo *= tintColor;
			}

			color.rgb = outDiffuse * albedo;
			color.rgb += outSpecular;
			color.rgb += emissive;
			color.rgb += ambient * albedo;
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
				color.rgb = mix(color.rgb, color.rgb * (outDiffuse + vec3(ambient)), effectLightingInfluence);
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
		// BGSM: apply material alpha (NifSkope fo4_default.frag does this)
		// BGEM: alpha already baked as effectBaseColorAlpha² (NifSkope fo4_effectshader.frag does NOT)
		if (!bIsEffectShader)
			fragColor.a *= alpha;

		if (bAlphaTest)
			if (fragColor.a <= alphaThreshold) // GL_GREATER
				discard;

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
uniform sampler2D texTintMask;

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
uniform bool bEnvMask;
uniform bool bSpecular;
uniform bool bHasSpecMap;
uniform bool bEmissive;
uniform bool bBacklight;
uniform bool bRimlight;
uniform bool bSoftlight;
uniform bool bAlphaTest;
uniform bool bGlowmap;
uniform bool bGreyscaleColor;
uniform bool bHasTintColor;
uniform bool bHasDetailMask;
uniform bool bHasTintMask;
uniform bool bDoubleSided;
uniform bool bHide;

uniform bool bIsEffectShader;
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

uniform float ambient;
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
		float fSpecularPower = exp2(smoothness * 10.0 + 1.0);
		specMask = specFactor * specularStrength;

		outSpec += TorranceSparrow(NdotL0, NdotH, NdotV, VdotH, vec3(specMask), fSpecularPower, 0.2) * NdotL0 * light.diffuse * specularColor;
		outSpec += ambient * specMask * fresnelSchlick(VdotH, 0.2) * (1.0 - NdotV) * light.diffuse;
	}

	// Back lighting: simulates translucency (light through thin cloth/hair)
	// SSE: when bBacklight, texSpecular (slot 7) contains the backlight texture
	if (bBacklight)
	{
		float NdotNegL = max(dot(normal, -lightDir), FLT_EPSILON);
		vec3 backlightColor = texture(texSpecular, uv).rgb;
		vec3 backlight = backlightColor * NdotNegL * clamp(backlightPower, 0.0, 1.0);
		emissive += backlight * light.diffuse;
	}

	// Rim lighting: bright edge when backlit
	if (bRimlight)
	{
		vec3 rimMask = bLightmask ? texture(texLightmask, uv).rgb : vec3(1.0);
		vec3 rim = rimMask * pow(vec3(1.0 - NdotV), vec3(rimlightPower));
		rim *= smoothstep(-0.2, 1.0, dot(-lightDir, viewDir));
		emissive += rim * light.diffuse * specMask;
	}

	// Diffuse
	vec3 diff = vec3(OrenNayarFull(lightDir, viewDir, normal, roughness, NdotL0));
	outDiffuse += diff * light.diffuse;

	// Soft Lighting
	if (bSoftlight)
	{
		vec3 softMask = bLightmask ? texture(texLightmask, uv).rgb : albedo;
		float wrap = (NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff);
		vec3 soft = softMask * max(0.0, wrap) * smoothstep(1.0, 0.0, sqrt(diff));
		outDiffuse += soft;
	}
}

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
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

						// Calculate missing blue channel
						normal.b = sqrt(1.0 - dot(normal.rg, normal.rg));

						// Tangent space map
						normal = normalize(mv_tbn * normal);
					}
				}

				if (bGreyscaleColor && !bIsEffectShader)
				{
                    // BGSM: palette lookup for armor recoloring
                    vec4 luG = colorLookup(baseMap.g, paletteScale - (1.0 - vColor.r));
					albedo = luG.rgb;
				}
			}

			// FaceTint: detail mask and tint mask with overlay blending (SSE)
			if (bHasDetailMask)
			{
				vec3 dm = texture(texDetailMask, uv).rgb;
				albedo = vec3(
					dm.r < 0.5 ? 2.0 * albedo.r * dm.r : 1.0 - 2.0 * (1.0 - dm.r) * (1.0 - albedo.r),
					dm.g < 0.5 ? 2.0 * albedo.g * dm.g : 1.0 - 2.0 * (1.0 - dm.g) * (1.0 - albedo.g),
					dm.b < 0.5 ? 2.0 * albedo.b * dm.b : 1.0 - 2.0 * (1.0 - dm.b) * (1.0 - albedo.b)
				);
			}
			if (bHasTintMask)
			{
				vec3 tm = texture(texTintMask, uv).rgb;
				albedo = vec3(
					tm.r < 0.5 ? 2.0 * albedo.r * tm.r : 1.0 - 2.0 * (1.0 - tm.r) * (1.0 - albedo.r),
					tm.g < 0.5 ? 2.0 * albedo.g * tm.g : 1.0 - 2.0 * (1.0 - tm.g) * (1.0 - albedo.g),
					tm.b < 0.5 ? 2.0 * albedo.b * tm.b : 1.0 - 2.0 * (1.0 - tm.b) * (1.0 - albedo.b)
				);
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

			// Environment cubemap (BGSM only; BGEM has its own cubemap path)
			if (bCubemap && bEnvMap && bShowTexture && !bIsEffectShader)
			{
				float cubeSmooth = (bSpecular && bShowTexture) ? specGloss * shininess : 1.0;

				vec3 reflected = reflect(viewDir, normal);
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

				outSpecular += cube.rgb * (vec3(ambient) + outDiffuse);
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

			// SkinTint / HairTint: applied after lighting so soft/backlight use untinted albedo (matches NifSkope)
			if (bHasTintColor && !bIsEffectShader)
			{
				albedo *= tintColor;
			}

			color.rgb = outDiffuse * albedo;
			color.rgb += outSpecular;
			color.rgb += emissive;
			color.rgb += ambient * albedo;
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
				color.rgb = mix(color.rgb, color.rgb * (outDiffuse + vec3(ambient)), effectLightingInfluence);
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
		// BGEM: alpha already baked as effectBaseColorAlpha² (NifSkope sk_effectshader.frag does NOT)
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
    Public Sub SetFloat(name As String, value As Single)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform1(loc, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetInt(name As String, value As Integer)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform1(loc, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetBool(name As String, value As Boolean)
        SetInt(name, If(value, 1, 0))
    End Sub

    Public Sub SetVector2(name As String, value As Vector2)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform2(loc, value.X, value.Y)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetVector3(name As String, value As Vector3)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform3(loc, value.X, value.Y, value.Z)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetVector4(name As String, value As Vector4)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.Uniform4(loc, value.X, value.Y, value.Z, value.W)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetMatrix3(name As String, value As Matrix3)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.UniformMatrix3(loc, False, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub SetMatrix4(name As String, value As Matrix4)
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then
            GL.UniformMatrix4(loc, False, value)
            '#If DEBUG Then
            '            Debug.Print($"[Uniform] {name} = {value}")
            '#End If
        Else
            '#If DEBUG Then
            '            Debug.Print($"⚠️ [Uniform] {name} not found!")
            '#End If
        End If
    End Sub

    Public Sub BindTexture(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
        '#If DEBUG Then
        '        Debug.Print($"[Texture] {uniformName} bound to TextureUnit {unit - TextureUnit.Texture0} with ID {textureID}")
        '#End If
    End Sub

    Public Sub BindCubeMap(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.TextureCubeMap, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
        '#If DEBUG Then
        '        Debug.Print($"[CubeMap] {uniformName} bound to TextureUnit {unit - TextureUnit.Texture0} with ID {textureID}")
        '#End If
    End Sub
End Class

