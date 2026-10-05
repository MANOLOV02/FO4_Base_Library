' Version Uploaded of Fo4Library 3.2.0
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics


''' <summary>EL FONDO RADIAL DEL PREVIEW, EN UN SOLO LUGAR.
''' <para>El GLSL de <c>backgroundAt()</c> lo comparten DOS programas: el quad de fondo
''' (<see cref="Background_Shader_Class"/>) y el piso (<c>Floor_Shader_Class.Fragment_Floor</c>), que se
''' funde contra el fondo en el horizonte. Si cada uno tuviera su copia, cambiar la curva en uno dejaria
''' una COSTURA visible en la linea donde el piso se funde — que es exactamente el defecto que este
''' Const compartido existe para hacer imposible.</para>
''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES, como el resto del archivo: vive en un
''' <c>Const String</c> de VB y una comilla cierra el literal. El gate <c>glsl-ascii</c> lo cubre.</para>
''' </summary>
Friend Module BackgroundFadeSource

    ''' <summary>Declaracion de los uniforms del fondo + la funcion que lo evalua. Se INYECTA en los dos
    ''' fragments; por eso declara <c>backgroundColor</c> aca y no en el bloque de uniforms del piso.</summary>
    Friend Const Fade_Helper As String =
"
// ---- FONDO RADIAL ---------------------------------------------------------------------------
// backFade: -1..+1. En 0 el fondo es PLANO y vale exactamente backgroundColor en toda la pantalla
// (y ademas el quad ni se dibuja: ver PreviewControl.PaintBackground). Por encima de 0 las esquinas
// van a NEGRO, por debajo van a BLANCO. El centro conserva backgroundColor con cualquier fade.
// El radio se normaliza por SEMI-EJE, no por pixel: la iso-linea sigue la forma del viewport en vez
// de salir aplastada en un panel ancho. Y se divide por sqrt(2) para que la ESQUINA valga 1 exacto,
// que es lo que significa -al 100 % las esquinas quedan negras-.
uniform vec3 backgroundColor;
uniform float backFade;
uniform vec2 viewportSize;
// Se declara tambien en Vertex_Floor: un uniform puede declararse en los dos stages y linkea a uno solo.
uniform mat4 matView;
// NETO del rig en MUNDO (unitario) y DIRECCIONALIDAD del rig (0..1). Los dos los calcula
// ResolveFrameLights sobre las luces YA RESUELTAS -- o sea con follow-camera aplicado cuando la casilla
// esta prendida. Por eso aca no hay rama por ese setting: transformar por matView da una direccion de
// vista CONSTANTE al orbitar con la casilla prendida, y una que acompania al mundo con la casilla
// apagada. Los dos comportamientos salen de la misma cuenta.
// Se apunta al NETO y no a la key: si el fill pesa casi tanto, el punto claro va ENTRE las dos.
uniform vec3 netDirWorld;
uniform float rigDirectionality;

vec3 backgroundAt(in vec2 fragXY)
{
    vec2 semi = max(viewportSize * 0.5, vec2(1.0));
    vec2 d = (fragXY - semi) / semi;
    float r = clamp(length(d) * 0.70710678, 0.0, 1.0);

    // FONDO DIRECCIONAL: el degradado se abre hacia donde esta la luz y se cierra del lado opuesto.
    // [!] SE DOBLA EL EXPONENTE, NO EL RADIO. pow(1, g) = 1 para cualquier g > 0, asi que las CUATRO
    // esquinas siguen valiendo exactamente 1 pase lo que pase con la direccion: +100 % sigue dando
    // esquinas negras exactas y -100 % blancas exactas. Lo unico que se mueve son las iso-lineas del
    // interior. Mover el CENTRO del radial en vez del exponente habria roto ese contrato.
    float gain = 1.0;
    if (rigDirectionality > 0.0)
    {
        vec2 netScreen = (matView * vec4(netDirWorld, 0.0)).xy;
        float nl = length(netScreen);
        if (nl > 0.00001)
        {
            // LA CANTIDAD NO ES UNA CONSTANTE: sale de cuanto se cancelan las luces entre si
            // (rigDirectionality) por cuanto de ese neto se PROYECTA en pantalla (nl, que es el seno del
            // angulo contra el eje de camara). Una luz que apunta justo a la camara no tiene direccion en
            // pantalla y no dobla nada -- eso es una ley, no un guard.
            float amount = rigDirectionality * nl;
            vec2 toPixel = d / max(length(d), 0.00001);
            float toward = dot(toPixel, netScreen / nl);
            // El signo del fade decide que significa -mas luz-: con fade > 0 (esquinas al negro) el lado
            // iluminado se oscurece MENOS; con fade < 0 (esquinas al blanco) blanquea MAS. En los dos
            // casos el fondo queda mas luminoso donde apunta la luz, que es la unica lectura coherente.
            gain = 1.0 + amount * toward * sign(backFade);
        }
    }

    float rd = pow(r, max(gain, 0.05));
    float t = smoothstep(0.0, 1.0, rd) * abs(backFade);
    vec3 target = (backFade >= 0.0) ? vec3(0.0) : vec3(1.0);
    return mix(backgroundColor, target, t);
}
"

    ''' <summary>Triangulo de pantalla completa SIN atributos: los tres vertices salen de
    ''' <c>gl_VertexID</c>. Por eso el draw es <c>DrawArrays(Triangles, 0, 3)</c> con un VAO vacio y no
    ''' hace falta ni VBO ni geometria. Un triangulo, no dos: evita la costura por la diagonal.</summary>
    Friend Const Vertex_Background As String =
"#version 430

void main()
{
    vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}"

    Friend Const Fragment_Background As String =
"#version 430

out vec4 FragColor;
" & Fade_Helper & "
void main()
{
    FragColor = vec4(backgroundAt(gl_FragCoord.xy), 1.0);
}"

End Module

''' <summary>Programa del quad de fondo. Se dibuja SOLO cuando el fade no es 0.</summary>
Public Class Background_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(BackgroundFadeSource.Vertex_Background, BackgroundFadeSource.Fragment_Background)
    End Sub
End Class

Public Class Floor_Shader_Class
    Inherits Shader_Base_Class
    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE DE SOMBRA.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_FO4</c>, <c>Fragment_SSE</c> y <c>Fragment_ShadowDepth</c>: ahi lo
    ''' ve quien edita la logica, que es donde tiene que estar. Los tres se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado, y son DOS leyes (FO4 y SSE), no una.</b> Tocar una sin la otra rompe la silueta
    ''' de la sombra sin que nada lo reporte.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Vertex_Floor As String =
"#version 430
layout(location = 0) in vec3 vertexPosition;

uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;

out vec3 worldPos;

void main()
{
    vec4 wp = matModel * vec4(vertexPosition, 1.0);
    worldPos = wp.xyz;
    gl_Position = matProjection * matView * wp;
}"

    Friend Const Fragment_Floor As String =
"#version 430

in vec3 worldPos;

uniform float tileStep;
uniform float floorHalfSize;
uniform vec3 backgroundLinear;
uniform vec3 groutColorLinear;
uniform vec3 cameraPosition;
uniform float floorExposure;
uniform vec3 ambientSky;
uniform vec3 ambientGround;
uniform vec3 lightDiffuse[4];
uniform vec3 lightDirection[4];
// true: the frame goes through the game's post (PostProcess.vb). The floor then writes its radiance in the
// scene space, premultiplied by its fade, and the fade as coverage: the post exposes, tonemaps and composites
// it over the background like the rest of the frame (floorExposure arrives as 1). false: the display target
// of a frame without post (post off, debug views): the floor applies the 2.3.8 tail (LegacyDisplaySource) and
// fades to the background itself.
uniform bool bHdrTarget;
// Exponent from the scene space to linear for the 2.3.8 curve: FO4 1 (linear scene), SSE 2.2 (raw scene).
// 2.3.8 fed the floor linear colours in both games (old Render.vb:6698). Shader_Base_Class.SceneToLinearExponent.
uniform float uSceneToLinear;

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 CoverageOut;
" & BackgroundFadeSource.Fade_Helper & LegacyDisplaySource.Tonemap_Glsl & "

vec3 lightAt(in vec3 nrm, in vec3 viewDir, in float detail)
{
    vec3 diffuse = mix(ambientGround, ambientSky, clamp(nrm.z * 0.5 + 0.5, 0.0, 1.0));
    for (int i = 0; i < 4; ++i)
        diffuse += lightDiffuse[i] * max(dot(nrm, lightDirection[i]), 0.0);

    // One restrained key-light highlight is enough to read as tile without four pow calls per pixel.
    float keyNdl = max(dot(nrm, lightDirection[0]), 0.0);
    vec3 keyHalf = normalize(lightDirection[0] + viewDir);
    float highlight = pow(max(dot(nrm, keyHalf), 0.0), 36.0) * keyNdl;
    return diffuse + lightDiffuse[0] * highlight * (0.075 * detail);
}

void main()
{
    float safeStep = max(tileStep, 0.001);
    vec2 tile = worldPos.xy / safeStep;
    vec2 cell = fract(tile);
    vec2 cellId = floor(tile);
    vec2 edgeDistance = min(cell, 1.0 - cell);
    vec2 footprint = max(fwidth(tile), vec2(0.00001));
    float pixelSpan = max(footprint.x, footprint.y);

    // Three-stage LOD: relief and tile variation go first, then grout, then the whole floor merges
    // into the background. This avoids leaving a smooth lit plate after the tile pattern is gone.
    float detail = 1.0 - smoothstep(0.16, 0.62, pixelSpan);
    float pattern = 1.0 - smoothstep(0.38, 0.95, pixelSpan);
    float surfaceLod = 1.0 - smoothstep(0.70, 1.65, pixelSpan);
    float jointHalfWidth = 0.045;
    float nearestEdge = min(edgeDistance.x, edgeDistance.y);
    float aa = max(fwidth(nearestEdge), 0.0005);
    float joint = (1.0 - smoothstep(jointHalfWidth - aa, jointHalfWidth + aa, nearestEdge)) * pattern;

    // Signed bevel walls: opposite sides of a groove tilt in opposite directions.
    float wallT = clamp(nearestEdge / jointHalfWidth, 0.0, 1.0);
    float bevelBand = (1.0 - abs(wallT * 2.0 - 1.0)) * detail;
    vec2 bevel = vec2(0.0);
    if (edgeDistance.x < edgeDistance.y)
        bevel.x = sign(cell.x - 0.5) * bevelBand;
    else
        bevel.y = sign(cell.y - 0.5) * bevelBand;
    vec3 nrm = normalize(vec3(bevel * 0.68, 1.0));

    vec3 viewDir = normalize(cameraPosition - worldPos);
    vec3 lighting = lightAt(nrm, viewDir, detail);

    // Cheap stable seven-state variation. No texture, trigonometry or high-frequency random noise.
    float tileState = mod(cellId.x * 3.0 + cellId.y * 5.0, 7.0) / 6.0;
    float tileVariation = 1.0 + (tileState - 0.5) * (0.04 * pattern);
    vec3 tileLinear = backgroundLinear * lighting * floorExposure * tileVariation;
    vec3 groutLinear = groutColorLinear * lighting * floorExposure * 0.72;
    vec3 linearColor = mix(tileLinear, groutLinear, joint);
    // The narrow center is below both bevel walls. This contact occlusion makes the groove read as
    // recessed even under a nearly vertical rig, where the two wall normals alone are subtle.
    float grooveCore = (1.0 - smoothstep(0.0, jointHalfWidth * 0.30 + aa, nearestEdge)) * detail;
    linearColor *= mix(1.0, 0.48, grooveCore);

    // Grazing fade hides the projected horizon before the finite quad can read as a flat plate.
    // The floor stays opaque: it is mixed to the known clear color instead of using alpha blending.
    float grazingFade = smoothstep(0.012, 0.28, abs(viewDir.z));
    float edgeCoord = max(abs(worldPos.x), abs(worldPos.y)) / max(floorHalfSize, 0.001);
    float edgeFade = 1.0 - smoothstep(0.58, 0.98, edgeCoord);
    float floorFade = surfaceLod * grazingFade * edgeFade;
    if (bHdrTarget)
    {
        FragColor = vec4(linearColor * floorFade, 1.0);
        CoverageOut = vec4(floorFade, 0.0, 0.0, 1.0);
        return;
    }
    vec3 displayColor = legacyLitDisplay(linearColor, uSceneToLinear);
    // EL FONDO SE EVALUA EN ESTE PIXEL, no como color plano: con el fade radial encendido el quad de
    // fondo detras del piso no es uniforme, y fundir contra una constante dejaba una COSTURA justo en
    // la banda del horizonte. Misma funcion, mismas coordenadas de pantalla, mismo resultado.
    FragColor = vec4(mix(backgroundAt(gl_FragCoord.xy), displayColor, floorFade), 1.0);
}"
    Sub New()
        MyBase.New(Vertex_Floor, Fragment_Floor)
    End Sub
End Class

Public Class Shader_Class_Fo4
    Inherits Shader_Base_Class
    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE DE SOMBRA.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_FO4</c>, <c>Fragment_SSE</c> y <c>Fragment_ShadowDepth</c>: ahi lo
    ''' ve quien edita la logica, que es donde tiene que estar. Los tres se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado, y son DOS leyes (FO4 y SSE), no una.</b> Tocar una sin la otra rompe la silueta
    ''' de la sombra sin que nada lo reporte.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Vertex_FO4 As String = "
#version 430
// invariant: the FO4 z-prepass and the G-buffer colour pass are draws of this same VS (their DXBC position code is
// identical, Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 10.3).
// The depth one draw writes is tested by another program's draw (FO4 z-prepass -> G-buffer; translucent-decal base pass ->
// its colour pass): gl_Position must come out identical across programs, which GLSL guarantees only for an invariant output
// (GLSL 4.60 4.8.1). Every FO4 program is built on this one VS: Shader_Class_Fo4 (colour, prepass, effects), the G-buffer
// records and their decal-base variants, the refraction normals, the shadow depth.
invariant gl_Position;
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
// The G-buffer records' COLOR input (FO4 VS rec2288 L119..122: rgb = exp2(2.2 * log2(c)), a raw), D3D semantics.
out vec4 vGbufVcol;
// WORLD-space position of the skinned vertex. Only consumer: the shadow lookup in the fragment.
// matModel is Identity today (nobody ever writes MeshData.Transform), so this is skinnedPos verbatim
// -- it is written as the full transform anyway so it stays correct if a per-shape transform is ever
// introduced, which is the same thing hemiAmbient() does with the normal.
out vec3 vWorldPos;

// FO4 EFFECT FALLOFF, PER VERTEX (BSEffect VS rec0440: o1.z, read by the PS as v1.z). Off when the geometry has
// no vertex normals (model-space-normal geometry): the engine permutation for that case is not traced, and the
// PS keeps its per-pixel evaluation there.
uniform bool bVertexFalloff;
uniform vec4 effectFalloffParams;   // cb1[2]: x startAngle, y stopAngle, z startOpacity, w stopOpacity
out float vEffectFalloff;

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

" & D3d11SemanticsSource.Glsl & "
" & RefractionSource.Vertex_Glsl & "
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
	    // TANGENT AND BITANGENT GO WITH THE RAW MATRIX, not with the normal matrix. They are directions
	    // ALONG the surface: they move the way the geometry moves. The inverse-transpose is the law of the
	    // NORMAL and of nothing else -- applying it to the tangent pushes it out of the tangent plane as
	    // soon as there is shear, and a blend of two different bone rotations ALWAYS has shear, which means
	    // every vertex of an elbow, a knee or a shoulder.
	    // This is the render half of the SkinningHelper.PorMatriz3x3 fix: the bake was corrected first and
	    // the render kept the old law, which left RENDER==BAKE broken on TWO channels instead of one.
	    skinnedTangent = normalize(mat3(skinMatrix) * vertexTangent);
	    skinnedBitangent = normalize(mat3(skinMatrix) * vertexBitangent);
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
	vWorldPos = vec3(matModel * vec4(skinnedPos, 1.0));

	// TBN in view space
	vec3 mv_normal = mv_normalMatrix * skinnedNormal;
	vec3 mv_tangent = mv_normalMatrix * skinnedTangent;
	vec3 mv_bitangent = mv_normalMatrix * skinnedBitangent;

    mv_tbn = mat3(mv_tangent.x,   mv_tangent.y,   mv_tangent.z,
              mv_bitangent.x, mv_bitangent.y, mv_bitangent.z,
              mv_normal.x,    mv_normal.y,    mv_normal.z);

	viewDirRaw = normalize(-vPos);
	refractVertex(skinnedNormal, vPos);

	// rec0440: v = normalize(-pos) and n = normalize(3x3 * normal) in the same (camera-relative) frame;
	// t = sat((|n.v| - start) / (stop - start)); o1.z = t*t*(3 - 2t) * (stopOpacity - startOpacity) + startOpacity.
	vEffectFalloff = 1.0;
	if (bVertexFalloff)
	{
		float nv = abs(dot(normalize(mv_normalMatrix * skinnedNormal), viewDirRaw));
		float t = clamp((nv - effectFalloffParams.x) / (effectFalloffParams.y - effectFalloffParams.x), 0.0, 1.0);
		vEffectFalloff = (t * t * (-2.0 * t + 3.0)) * (effectFalloffParams.w - effectFalloffParams.z) + effectFalloffParams.z;
	}
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
	vGbufVcol = vec4(d3d_exp2(2.2 * d3d_log2(vColor.r)), d3d_exp2(2.2 * d3d_log2(vColor.g)),
	                 d3d_exp2(2.2 * d3d_log2(vColor.b)), vColor.a);
}
"
    Friend Const Fragment_FO4 As String = "
#version 430

// ##################################################################################################
// ####  SHADOW SYNC CONTRACT -- RENDER SHADERS MUST SYNCHRONIZE THEIR LOGIC WITH THE SHADOW PASS  ###
// ##################################################################################################
//
//   ANY CHANGE TO THE ALPHA LOGIC IN THIS SHADER MUST BE MIRRORED IN THE SHADOW DEPTH PASS:
//   ==>  ShadowDepthShaderSource.Fragment_ShadowDepth  (Shader_Class.vb)
//   And the other way round: that pass points back here. The two are ONE law written twice.
//
// WHY. The shadow map is drawn by a DIFFERENT program, and that program decides WHICH FRAGMENT
// EXISTS using the same alpha this shader uses. If the two stop agreeing, the shadow silhouette
// stops being the silhouette of the object on screen: shadow appears for geometry that was
// discarded, or is missing for geometry that is drawn. Nothing reports it -- it just looks wrong,
// and the cause is two files away.
//
// THE LIST. Touch any of these here, and go fix Fragment_ShadowDepth in the same commit:
//   * the ALPHA TEST -- which quantity is compared, and against which threshold
//   * the VERTEX ALPHA -- linear or gamma-corrected, and which predicate gates it
//   * the MATERIAL ALPHA SCALAR -- whether it enters the test or is applied afterwards
//   * GREYSCALE-TO-PALETTE on alpha -- it REPLACES the alpha with a palette lookup
//   * FALLOFF -- see the divergence list below: it does NOT work the same in both games
//   * the ZAP discard
//   * SKINNING -- if a vertex moves, its shadow moves too
//   * the UV transform (offset / scale) used to sample the diffuse
//
// !!!! TWO GAMES, TWO LAWS -- NOT ONE. Fallout 4 and Skyrim SE do NOT agree here, and the depth pass
// carries BOTH, selected by the bLeySse uniform. The FOUR divergences, all measured, all of which
// caused a real bug when someone assumed there was only one law:
//   1. effect shader (.bgem) vertex alpha: FO4 uses pow(vColor.a, 2.2), SSE uses it LINEAR.
//   2. lighting shader (.bgsm) material scalar: SSE multiplies it BEFORE the test, FO4 after.
//   3. FALLOFF ON THE ALPHA, with greyscale-to-palette recolor:
//        FO4 folds falloff into the palette V *AND* multiplies by it again afterwards -- falloff^2,
//            on purpose, that is what the engine does.
//        SSE folds it into the palette V and does NOT multiply afterwards -- so it is one or the
//            other, never both. Assuming FO4's rule here makes a .bgem cast nothing at all.
//      Without recolor, both games multiply once.
//   4. (WITHDRAWN) the falloff curve is the SAME in both games, measured on the vertex shaders: FO4
//      rec0440 and SSE t00010153 both do t = sat((|n.v| - start) / (stop - start)) (div_sat),
//      t*t*(3 - 2t), then t*(stopOp - startOp) + startOp -- no clamp on the opacities, per vertex.
// So a change that is correct for one game can be wrong for the other. Check both, always.
//
// ALSO: this GLSL lives inside a VB Const String. It must be PURE ASCII, and it must NOT contain a
// double quote -- a quote closes the VB literal, and escaping it as a pair sends a stray quote to
// the driver. The glsl-ascii gate in Tools/ParityGate enforces both.
// ##################################################################################################

/*
 * BodySlide and Outfit Studio
 * Shaders by jonwd7 and ousnius
 * https://github.com/ousnius/3rd party references/BodySlide-and-Outfit-Studio
 * http://www.niftools.org/
 * Modified By Manolo For WardrobeManager
 */

uniform sampler2D texDiffuse;
uniform sampler2D texNormal;
uniform samplerCube texCubemap;
uniform sampler2D texEnvMask;
uniform sampler2D texGreyscale;
uniform sampler2D texFaceTintOverlay;   // TETI/TEND composed tint layers, blended on top of diffuse

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
uniform bool bAlphaTest;
uniform bool bGreyscaleColor;
uniform bool bDoubleSided;
uniform bool bHide;
uniform bool bHasFaceTintOverlay;       // true when composed face tint texture is bound

uniform bool bIsEffectShader;
// What this draw adds to the coverage of the HDR target (attachment 1; see SceneTargets): 0 = the draw is not
// blended and covers its pixel (writes 1), 1 = blended and contributes its own light (writes its alpha;
// the caller blends attachment 1 with ONE / ONE_MINUS_SRC_ALPHA), 2 = blended with a destination-only factor
// (multiplicative modes: adds no light of its own, writes 0 and leaves the coverage as it was).
uniform int uCoverageMode;
uniform bool bEffectFalloff;
uniform bool bEffectFalloffColor;
uniform bool bEffectGreyscaleAlpha;
uniform float effectLightingInfluence;
uniform vec4 effectFalloffParams;   // x=startAngle, y=stopAngle, z=startOpacity, w=stopOpacity
uniform bool bVertexFalloff;        // the falloff arrives from the VS (vEffectFalloff), see Vertex_FO4
in float vEffectFalloff;
// cb1[0] of the BSEffect PS (SetupMaterial 0x142225A18..B11): rgb = powf(BaseColor.rgb * BaseColorScale, 2.2), or
// powf(BaseColor.rgb, 2.2) with greyscale-to-palette colour (then BaseColorScale goes to cb1[1].x =
// effectBaseColorScale and scales the palette colour); a = BaseColor.a = the file Alpha, raw.
uniform vec3 effectBaseColor;
uniform float effectBaseColorAlpha;
uniform float effectBaseColorScale;
// LIGHTING technique bit: EffectLighting on AND byte(LightingInfluence) > 0 (0x1421775CD..609, 0x142171806).
uniform bool bEffectLighting;
// The LIGHTING technique's light L (i1385): the sum of up to 4 point lights + DLightColor, no N.L, no shadow, no
// ambient. The preview has no point lights; L = DLightColor of the chosen weather and moment kept in the game's
// proportion to the preview key (PreviewImagingRow.EffectLightForKey), linear. Flat: one value per draw.
uniform vec3 effectDLightColor;
// MULTBLEND technique of the effect (see the effect block).
uniform bool bEffectMultBlend;
// Post off (Apply post-process unchecked) and not a debug view: this shader applies the 2.3.8 display law
// itself (see the tail of the lit block). uSceneToLinear = Shader_Base_Class.SceneToLinearExponent.
uniform bool bLegacyDisplay;
uniform float uSceneToLinear;
// Mip the effect samples its cube at: max(uiMinLOD, EnvmapMinLOD) (0x142225C6E..C75), sampler MIN_MAG_LINEAR_MIP_POINT
// with MinLOD = MaxLOD (0x141856390). uiMinLOD = 0 for a resident texture.
uniform float effectEnvMinLod;

uniform mat4 matModel;
uniform mat4 matModelViewInverse;
uniform float DebugMode;

uniform	vec2 uvOffset;
uniform vec2 uvScale;
uniform float envReflection;
uniform float alpha;
uniform float WireAlpha;

uniform float alphaThreshold;
// FO4 Z-PREPASS draw of a lighting shape (Fo4RenderPassLaw; groups 0x19 / 0x1A, 0x14181FAA0): depth only.
uniform bool bFo4Prepass;
// 0x1A: discard tex.a * saturate(vc.a + y) - (ref/255 + 0.00392153) < 0, y = 0 with a COLOR stream, else 10000
// (0x141828F45..F8D) - the vertex alpha counts only when the geometry has colours.
uniform bool bFo4PrepassAlphaTest;
uniform bool bFo4PrepassVertexAlpha;
uniform float fo4PrepassThreshold;
// FO4 translucent-decal base pass of an effect decal: 0 off; 1 = its blend's source factor is SRC_ALPHA (modes 1/2): a
// fragment with alpha <= 0 paints nothing; 2 = source factor ONE / DEST_COLOR (modes 3/4): every fragment that survives
// the effect's own discards paints (Fo4BlendModeFactors).
uniform int uFo4DecalBaseMode;
// The G-buffer's alpha test multiplies the vertex alpha only with technique bit 0 (F4SF2 Vertex_Colors) and without
// bits 10 / 6 (Tree_Anim; Tessellate + Dismemberment, Meatcuff, Face + eye data): Fo4RenderPassLaw.
uniform bool bFo4GBufferTestVertexAlpha;


// Engine-faithful FO4 path (Fallout4.exe). This fragment is FO4-only (Skyrim uses Fragment_SSE),
// so the engine path is unconditional here -- no runtime flag.
uniform bool bShowVertexColor;  // mesh has authored vertex colors AND the toggle is on (gates the BGEM vertex blend)


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

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec4 coverageOut;

// El engine RENORMALIZA el vector de vista POR PIXEL. Los 18 PS de BSLighting de FO4 (la poblacion
// COMPLETA del bloque b06 en Shaders011.fxp) abren con
//     dp3 r0.x, v6.xyzx, v6.xyzx ; rsq r0.x, r0.x ; mul r0.yzw, r0.xxxx, v6.xxyz
// y de ahi salen el half-vector (`mad r7.yzw, v6.xxyz, r0.xxxx, cb2[0].xxyz`), N.V y la reflexion
// del cubemap. El VS de la app ya emitia normalize(-vPos), pero la INTERPOLACION lo desnormaliza a
// lo ancho del triangulo. viewDirRaw = varying crudo; viewDir = unitario, fijado al entrar a main().
vec3 viewDir = vec3(0.0);

vec3 normal = vec3(0.0);

vec2 uv = vec2(0.0);
vec3 albedo = vec3(0.0);

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
vec4 normalMap = vec4(0.0);

#ifndef M_PI
	#define M_PI 3.1415926535897932384626433832795
#endif

#define FLT_EPSILON 1.192092896e-07F // smallest such that 1.0 + FLT_EPSILON != 1.0






" & LegacyDisplaySource.Tonemap_Glsl & SoftEffectSource.Soft_Glsl & "

vec4 colorLookup(in float x, in float y)
{
	return texture(texGreyscale, vec2(clamp(x, 0.0, 1.0), clamp(y, 0.0, 1.0)));
}



// 1 = fully lit, 0 = fully shadowed. Only ever called when bShadows is true.

void main(void)
{
	viewDir = normalize(viewDirRaw);   // engine: rsq(dot(v6,v6)) por pixel, ver la nota del varying
    uv = vUV * uvScale + uvOffset;
	if (bFo4Prepass)
	{
		if (bHide || (bApplyZap && ZappedVert == 1))
			discard;
		if (bFo4PrepassAlphaTest)
		{
			float texA = bShowTexture ? texture(texDiffuse, uv).a : 1.0;
			float vcA = bFo4PrepassVertexAlpha ? clamp(vColor.a, 0.0, 1.0) : 1.0;
			if (texA * vcA - fo4PrepassThreshold < 0.0)
				discard;
		}
		fragColor = vec4(0.0);
		return;
	}
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
			albedo *= diffRgb;

			// Diffuse texture without lighting
			color.rgb = albedo;

			// El sampleo del normal map se SACO de adentro de `if (bLightEnabled)`. Motivo: el bloque
			// del cubemap del EFFECT shader (BGEM, mas abajo) NO esta anidado bajo bLightEnabled y sin
			// embargo lee normalMap.a y `normal`. Hoy eso no explota por un solo motivo: el uniform esta
			// CABLEADO a True en el unico call-site que existe (Render.vb, `SetBool(bLightEnabled, True)`;
			// no hay otro seteo en todo el arbol). Es una trampa latente, no un bug vivo.
			// Sacar el sampleo de aca es IDENTICO en comportamiento mientras el uniform sea True, y
			// elimina la mitad de la trampa sin reestructurar nada.
			// LA OTRA MITAD -- el calculo de `normal` -- tambien se subio: vive mas abajo, en la nota que
			// arranca con: LA NORMAL SE CALCULA ACA. Con las dos afuera, `bLightEnabled = False` SI es un
			// estado soportado en este fragment.
			// `Fragment_SSE` lleva EL MISMO arreglo, con las dos mitades subidas y medido igual (10/10
			// frames byte-identicos). Los dos juegos quedan con la misma ley; si alguien toca uno, el otro.
			if (bNormalMap)
			{
				normalMap = texture(texNormal, uv);
			}

		}


		// LA NORMAL SE CALCULA ACA, FUERA DE `bLightEnabled`, y no adentro como estaba.
		// Motivo: el bloque del EFFECT shader (BGEM) de mas abajo NO esta anidado bajo
		// bLightEnabled: es su HERMANO, los dos cuelgan de `if (!bWireframe)`. Y sin embargo lee
		// `normal` (su falloff NdotV y el reflect() del cubemap) y `normalMap.a`. Con el calculo adentro,
		// `bLightEnabled = False` dejaba a `normal` en su valor inicial vec3(0.0) y el camino
		// BGEM leia basura. No explotaba por un solo motivo: el uniform esta cableado a True en
		// el unico call-site que existe. Era una trampa esperando a que alguien agregue otro.
		//
		// POR QUE ES IDENTICO mientras el uniform sea True: ninguna sentencia que quedo en el medio
		// LEE `normal`, `normalMap` ni `geoNormal`, y nada de lo que se movio lee `albedo`. La
		// formulacion importa: -no hay nada en el medio- seria FALSO -- el recolor de grises escribe
		// `albedo`, y la cola de este bloque (`if (bModelSpace) geoNormal = normal;` y el flip de doble
		// cara) ahora pasa por encima de el. Vale porque son disjuntos, no porque no haya nada.
		// MEDIDO, no argumentado: ShadowGate frame a frame contra HEAD, 10/10 frames byte-identicos, con
		// control positivo (una linea de mas en este mismo bloque SI mueve pixeles) y control de
		// determinismo entre dos procesos.
		//
		// El recolor de escala de grises se QUEDA adentro de bLightEnabled: toca `albedo`, no la
		// normal, asi que subirlo seria un cambio de conducta que nadie pidio.
		//
		// `Fragment_SSE` tenia la MISMA trampa y se arreglo igual, en el mismo lote. Alli tambien
		// `if (bLightEnabled)` abre y CIERRA antes de `if (bIsEffectShader)` -- son hermanos --, y el
		// effect leia `normal` en DOS sitios que no cuelgan de bLightEnabled: el
		// `abs(dot(normal, viewDir))` del falloff y el `reflect(-viewDir, normal)` del cubemap. Verificado
		// contando profundidad de llaves, no por indentacion. Si se toca uno de los dos fragments, tocar el otro.
		
		// Arranca neutra (en shapes MSN mv_tbn es degenerada -> se usa la matriz objeto->vista)
		if (bModelSpace)
			normal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
		else
			normal = normalize(mv_tbn * vec3(0.0, 0.0, 0.5));

		if (bShowTexture && bNormalMap)
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

		// Double-sided: flip normal for back faces
		if (bDoubleSided && !gl_FrontFacing)
		{
			normal = -normal;
		}

		// The lighting of the lighting shapes is the FO4 deferred frame's (Fo4DeferredTargets: G-buffer, lights,
		// composite; propuesta v3 E4). This fragment draws only effects, wireframes, debug views and the prepass.

		// Effect Shader (BGEM = BSEffectShader, block b05), reconstructed EXACT from the GAME:
		// base rec1026, VC rec1083, recolor-color rec1103, recolor-alpha rec0905, envmap rec0761.
		// Engine pixel order (all LINEAR; NO PS tonemap/encode). The blend STATE comes from the alpha property's src/dst
		// (Render: Fo4EffectBlend); MULTBLEND also changes the PS (below); PREMULTIPLY is never set in world rendering
		// (global byte 0x143E5E345, written only by Interface3D); ADDBLEND's PS term is the fog (none here):
		//   base.rgb = diffuse.rgb*BaseColor ; base.a = diffuse.a*BaseColor.a
		//   [ENVMAP]  base.rgb += cube(reflect(V,N)) * EnvmapScale * normal.a * envMask.r   (rec0761 L62-66)
		//   [VERTEX COLOR, only when mesh has them = rec1083 VC]: base.rgba *= pow(vColor.rgba,2.2)  (COLOR0 = mesh vColor, MULTIPLY)
			//   [RECOLOR-COLOR] base.rgb = palette(U=pow(diffuse.g,1/2.2), V=RAW BaseColor.r*falloff) * BaseColorScale  (rec1103 ignores COLOR0)
		//   eff = lerp(base, PropertyColor*base, LightingInfluence)
		//   alpha = base.a*PropertyColor.w ; [RECOLOR-ALPHA] alpha = palette(U=diffuse.a, V=pow(BaseColor.a,1/2.2)*falloff).a
		//   o0.rgb = lerp(eff, COLOR1.rgb, COLOR1.w)  <-- COLOR1 (v3) = the FOG, built by the VS from cb12[41..46] (Tools/re-docs/RE_BGEM_BLENDMODES_FO4_2026-10-03.md), NOT the mesh vertex color. The preview has no fog -> NOT replicated.
		// PropertyColor (cb2[13], runtime light tint) -> the effect's DLightColor (effectDLightColor, ApplyMaterial); PropertyColor.w ~ 1.
		// BaseColorScale (cb1[1]) is the PALETTE scale ONLY -- it is NOT a base multiplier (rec0512 has none).
		// Vertex color (COLOR0) is applied below ONLY as a MULTIPLY on base rgb+alpha (rec1083). There is
		// NO final lerp toward the mesh vColor -- the engine's final lerp targets COLOR1 (the fog), not COLOR0.
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
			
			// Falloff factor = v1.z of the VS (rec0440), interpolated. 1.0 when no falloff. Geometry without vertex
			// normals (MSN) keeps the per-pixel evaluation (see Vertex_FO4).
			float effFalloff = 1.0;
			if (bVertexFalloff)
				effFalloff = vEffectFalloff;
			else if (bEffectFalloff || bEffectFalloffColor)
			{
				float NdotV_falloff = abs(dot(normal, viewDir));   // viewDir ya es unitario (main lo normaliza)
				float ft = clamp((NdotV_falloff - effectFalloffParams.x) / (effectFalloffParams.y - effectFalloffParams.x), 0.0, 1.0);
				ft = ft * ft * (3.0 - 2.0 * ft);
				effFalloff = mix(effectFalloffParams.z, effectFalloffParams.w, ft);
			}
			
			// SOFT (technique bit 12, softFo4 in SoftEffectSource): it scales the base alpha (before PropertyColor.w and the
			// alpha test) and the palette V of both recolors; with palette ALPHA the output alpha is palette.a (not scaled
			// again). Hole, declared: PARTICLE_DISTORTION (soft on palette.a instead of V) is not rendered by the preview.
			float effSoft = bSoftEffect ? softFo4() : 1.0;
			effAlpha *= effSoft;

			// Grayscale->palette recolor. COLOR(0x2000) replaces rgb; ALPHA(0x4000) replaces alpha; both=0x6000.
			// U = pow(base.channel,1/2.2) for COLOR (rec1103 L38-40: the base slot is sRGB, so the sample is
			//   linear and the pow re-encodes it), raw base.alpha for ALPHA (rec0905 L32).
			// V (color) = pow(cb1[0].x,1/2.2) * falloff (rec1103 L34-36). effectBaseColor IS cb1[0] (powf'd on
			//   the CPU, see the uniform), so the pow undoes it and V is the authored BaseColor.r.
			//   (Alpha: BaseColor.a is NOT powf'd in setup, and its V still takes pow(.,1/2.2) -- rec0905 L34-37.)
			// When the mesh has vertex colors, the engine MULTIPLIES the palette V by the vertex color
			// channel, RAW (rec1002 `mul r0.yz, r0.yyzy, v2.xxwx`): V_color *= vColor.r, V_alpha *= vColor.a.
			// White verts (=1) -> no change (rec1103/rec0905, no-vColor perm). NOTE BGSM does this ADDITIVE
			// + gamma-encoded; BGEM does it MULTIPLICATIVE + raw (different families, verified per-asm).
			float vcRecolorR = bShowVertexColor ? max(vColor.r, 0.0) : 1.0;
			float vcRecolorA = bShowVertexColor ? max(vColor.a, 0.0) : 1.0;
			if (bGreyscaleColor)
			{
				float palU = pow(max(baseMap.g, 0.0), 1.0/2.2);
				float palV = pow(max(effectBaseColor.r, 0.0), 1.0/2.2) * vcRecolorR * effFalloff * effSoft;
				effRgb = colorLookup(palU, palV).rgb * effectBaseColorScale;   // * BaseColorScale (PaletteColorScale)
			}
			
			// ENVMAP reflection (0x80000): cube(reflect(V,N)) * EnvmapScale * normal.a * envMask.r,
			// ADDED to base BEFORE the lighting-influence lerp (rec0761 L62-66), on top of recolor.
			if (bCubemap && bEnvMap && bShowTexture)
			{
				vec3 reflected = reflect(viewDir, normal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));
				vec3 cube = textureLod(texCubemap, reflectedWS, effectEnvMinLod).rgb;
				float emask = bEnvMask ? texture(texEnvMask, uv).r : 1.0;
				float nrmA = bNormalMap ? normalMap.a : 1.0;
				effRgb += cube * envReflection * nrmA * emask;
			}
			if (bEffectGreyscaleAlpha)
			{
				float palUa = baseMap.a;                                        // alpha index = diffuse.alpha (rec0905)
				float palVa = pow(max(effectBaseColorAlpha, 0.0), 1.0/2.2) * vcRecolorA * effFalloff * effSoft;
				effAlpha = colorLookup(palUa, palVa).a;
			}
			
			// RGB_FALLOFF (0x200000) multiplies rgb; FALLOFF (0x10) multiplies alpha.
			if (bEffectFalloffColor && !bGreyscaleColor) effRgb *= effFalloff; // recolor already folds falloff into palette V (rec0550) -> avoid falloff^2
			// With palette ALPHA the output alpha IS palette.a, whose V already carries the falloff (rec0905, rec0927): no
			// second multiply.
			if (bEffectFalloff && !bEffectGreyscaleAlpha) effAlpha *= effFalloff;
			
			// LIGHTING technique (i1385): c = lerp(b, L * b, LI), L = point lights + DLightColor, no N.L.
			// Without it the PS lerps toward PropertyColor.rgb * b with PropertyColor = 1 (an effect property's
			// external emittance is null, 0x1421774F4 / 0x142226F0D..F9F): c = b.
			if (bEffectLighting)
				effRgb = mix(effRgb, effRgb * effectDLightColor, effectLightingInfluence);

			// MULTBLEND (technique bit 6: src DEST_COLOR or dst SRC_COLOR, not the decal pair 4/7; 0x142178150): the PS
			// writes 1 + a*(c - 1) = lerp(1, c, a) with the FINAL alpha (rec1099 L33-37, rec1035), so where the effect is
			// transparent it multiplies the destination by 1 (neutral) instead of by its colour. The fog term of that
			// permutation (lerp(c, 1, saturate(1.5 * fog.a))) is a no-op without fog.
			if (bEffectMultBlend)
				effRgb = vec3(1.0) + effAlpha * (effRgb - vec3(1.0));
			
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

		// SALIDA LINEAL HDR, COMO EL MOTOR: 0 de los 18 PS de b06 contienen la constante de Hable y las 18
		// colas escriben lineal a o0 (`mad o0.xyz, ...` sin curva, sin pow, sin _sat); los PS de b05 (BGEM)
		// tampoco. El tonemap, el encode y la LUT son POST-PROCESO sobre el buffer compuesto (ImageSpace HDR
		// i3700 + GammaCorrectLUT i3648): los hace la pasada de PostProcess.vb, no este shader.
		// POST OFF (bLegacyDisplay): THE 2.3.8 DISPLAY TAIL, NOT THE ENGINE, applied to the value in the space
		// 2.3.8 gave it. Lit: linear then and now -> Hable / Hable(1), then 1/2.2. Effect (BGEM): 2.3.8 composed
		// it in display space with no curve and no encode; it is linear now -> only 1/2.2.
		if (bLegacyDisplay)
		{
			if (bIsEffectShader)
				color.rgb = pow(max(color.rgb, vec3(0.0)), vec3(1.0 / 2.2));
			else
				color.rgb = legacyLitDisplay(color.rgb, uSceneToLinear);
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

	// Sin clamp: el motor escribe o0 sin _sat sobre un target R11G11B10_FLOAT (rgb y alpha crudos). El
	// camino wireframe se dibuja en el target de display (RGBA8), que satura al escribir.
	fragColor = color;



//====================DEBUG MODE==========================
if (DebugMode > 0.0) {
    // OJO: LAS VISTAS SON MODEL-SPACE AWARE, Y NO ES UN ADORNO. En una shape MSN `mv_tbn` es
    // DEGENERADA -- lo dice el propio camino iluminado de este fragment, unas lineas mas arriba:
    // `Arranca neutra (en shapes MSN mv_tbn es degenerada -> se usa la matriz objeto->vista)`.
    // La version anterior de este bloque derivaba las TRES direcciones de `mv_tbn` sin mirar
    // `bModelSpace` y llevaba escrito `no MSN in FO4`, que es FALSO: este mismo fragment declara
    // `uniform bool bModelSpace` y `in mat3 v_msnMatrix`, y su camino iluminado se bifurca con ellos.
    // O sea que sobre una cabeza o un cuerpo _msn las vistas dibujaban una matriz basura y el modo 4
    // reportaba un `error de TBN` calculado sobre esa basura.
    // LA LEY SE COPIA DEL CAMINO ILUMINADO DE ESTE MISMO SHADER, no se inventa una para el debug.
    vec3 dbgNormal;
    if (bModelSpace) {
        // EN MSN LA NORMAL DE LA GEOMETRIA ES LA DEL MAPA, no la semilla. Lo dice el camino iluminado
        // de este mismo fragment, que en esta rama hace `geoNormal = normal` con la razon escrita al
        // lado: la semilla `v_msnMatrix * (0,0,1)` es el eje +Z del OBJETO, o sea una direccion
        // CONSTANTE por shape -- dibujarla pinta la cabeza entera de un color plano que no dice nada
        // del fragmento. El mapa guarda la normal de objeto (no una perturbacion tangente), asi que
        // decodificarlo ES leer la normal de la geometria.
        // Se muestrea texNormal y NO se reusa `normalMap`: esa variable arranca en vec4(0.0) y solo se
        // llena dentro de la rama de iluminacion, asi que con bLightEnabled apagado saldria negra.
        if (bShowTexture && bNormalMap) {
            dbgNormal = normalize(v_msnMatrix * (texture(texNormal, uv).rbg * 2.0 - 1.0));
        } else {
            dbgNormal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
        }
    } else {
        dbgNormal = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));
    }

    // Gris plano = `esta vista NO APLICA a esta shape`. Tangente, bitangente y error de TBN solo
    // existen si hay base tangente; en model-space no la hay. Pintar gris es lo unico honesto:
    // dibujar `mv_tbn` degenerada seria inventar un dato, y dejar pasar el render iluminado haria
    // creer que la shape esta sana. Ademas se lee de una: las piezas _msn saltan a la vista.
    vec4 noAplica = vec4(0.5, 0.5, 0.5, 1.0);

    if (abs(DebugMode - 1.0) < 0.5) {
        // Modo 1: la normal GEOMETRICA (la semilla del camino iluminado), en espacio de vista.
        // No lleva el normal map: para eso estan los modos 5 (mapa crudo) y el render normal.
        fragColor = vec4(dbgNormal * 0.5 + 0.5, 1.0);
    }
    else if (abs(DebugMode - 2.0) < 0.5) {
        // Modo 2: tangentes. Solo en tangent-space.
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            fragColor = vec4(normalize(mv_tbn * vec3(1.0, 0.0, 0.0)) * 0.5 + 0.5, 1.0);
        }
    }
    else if (abs(DebugMode - 3.0) < 0.5) {
        // Modo 3: bitangentes. Solo en tangent-space.
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            fragColor = vec4(normalize(mv_tbn * vec3(0.0, 1.0, 0.0)) * 0.5 + 0.5, 1.0);
        }
    }
    else if (abs(DebugMode - 4.0) < 0.5) {
        // Modo 4: error de TBN. NO APLICA en model-space: ahi no hay TBN que comparar -- la normal
        // sale del mapa por v_msnMatrix y `mv_tbn` es degenerada.
        if (bModelSpace) {
            fragColor = noAplica;
            return;
        }
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
    else if (abs(DebugMode - 5.0) < 0.5) {
        // Modo 5: EL NORMAL MAP CRUDO, tal cual lo devuelve el sampler. Sin decodificar (nada de
        // *2-1), sin reconstruir Z y sin pasar por la TBN: se ve el contenido del archivo, que es
        // justo lo que hace falta para distinguir la textura esta mal de la TBN esta mal.
        // Sin textura de normales se dibuja el plano neutro (0.5, 0.5, 1.0), que es lo que un normal
        // map tangent-space trae donde la superficie no se desvia.
        if (bShowTexture && bNormalMap) {
            fragColor = vec4(texture(texNormal, uv).rgb, 1.0);
        } else {
            fragColor = vec4(0.5, 0.5, 1.0, 1.0);
        }
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
		// The world lighting of FO4 is drawn by the G-buffer pass (BSDFPrePassShader), not by these forward PS: same
		// compare, threshold ref/255 + 0.00392153 (Fo4RenderPassLaw.GBufferAlphaThreshold, uploaded by Render.vb).
		// El `<=` de la app descartaba tambien alpha == ref. Con alpha de 8 bits y refs tipicas
		// (128/255) la igualdad EXACTA es frecuente en un cutout dibujado a mano, asi que comia una
		// franja de pixeles que el motor conserva. Pasa a `<`.
		// BGSM / NIF lighting: the G-buffer's own test value, tex.a [* vc.a] (9.1, 10.2: COLOR0.w linear).
		float alphaTestValue = fragColor.a;
		if (!bIsEffectShader)
			alphaTestValue = (bShowTexture ? baseMap.a : 1.0) * (bFo4GBufferTestVertexAlpha ? vColor.a : 1.0);
		if (bAlphaTest)
			if (alphaTestValue < alphaThreshold) // GL_GEQUAL (engine: discard si alpha < ref)
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

	if (uFo4DecalBaseMode == 1 && fragColor.a <= 0.0) discard;

	// Coverage of the HDR target (ignored when the draw buffer 1 does not exist: display target).
	if (uCoverageMode == 1)
		coverageOut = vec4(fragColor.a, fragColor.a, 0.0, fragColor.a);
	else if (uCoverageMode == 2)
		coverageOut = vec4(0.0);
	else
		coverageOut = vec4(1.0);
}
"
    Sub New()
        MyBase.New(Vertex_FO4, Fragment_FO4)
    End Sub
End Class

Public Class Shader_Class_SSE
    Inherits Shader_Base_Class
    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE DE SOMBRA.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_FO4</c>, <c>Fragment_SSE</c> y <c>Fragment_ShadowDepth</c>: ahi lo
    ''' ve quien edita la logica, que es donde tiene que estar. Los tres se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado, y son DOS leyes (FO4 y SSE), no una.</b> Tocar una sin la otra rompe la silueta
    ''' de la sombra sin que nada lo reporte.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Vertex_SSE As String = "
#version 430
// SSE vertex shader with model-space normal (MSN) support
// invariant: the z-prepass and the colour pass are draws of this same VS and the colour pass of an opaque shape tests
// EQUAL against the prepass depth (SseRenderPassLaw): the position must come out bit-identical in both, as the engine's
// utility depth VS and main VS do (Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 5).
invariant gl_Position;
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
layout(location = 11) in vec3 vertexPreSkinNormal;
uniform int uSseAoEffectClass;
out vec3 vSseAoEffectNormal;

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
// WORLD-space position of the skinned vertex. Only consumer: the shadow lookup in the fragment.
// matModel is Identity today (nobody ever writes MeshData.Transform), so this is skinnedPos verbatim
// -- it is written as the full transform anyway so it stays correct if a per-shape transform is ever
// introduced, which is the same thing hemiAmbient() does with the normal.
out vec3 vWorldPos;

// SSE EFFECT FALLOFF, PER VERTEX (BSEffect VS t00010153: o1.z). Same law as Vertex_FO4; off on geometry without
// vertex normals (model-space-normal geometry), where the PS keeps its per-pixel evaluation.
uniform bool bVertexFalloff;
uniform vec4 effectFalloffParams;   // cb1[2]: x startAngle, y stopAngle, z startOpacity, w stopOpacity
out float vEffectFalloff;

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

" & RefractionSource.Vertex_Glsl & "
" & D3d11SemanticsSource.Glsl & SseAoNormalSource.EngineFrame_Glsl & SseAoNormalSource.Vs_Glsl & "
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
	    // TANGENT AND BITANGENT GO WITH THE RAW MATRIX, not with the normal matrix. They are directions
	    // ALONG the surface: they move the way the geometry moves. The inverse-transpose is the law of the
	    // NORMAL and of nothing else -- applying it to the tangent pushes it out of the tangent plane as
	    // soon as there is shear, and a blend of two different bone rotations ALWAYS has shear, which means
	    // every vertex of an elbow, a knee or a shoulder.
	    // This is the render half of the SkinningHelper.PorMatriz3x3 fix: the bake was corrected first and
	    // the render kept the old law, which left RENDER==BAKE broken on TWO channels instead of one.
	    skinnedTangent = normalize(mat3(skinMatrix) * vertexTangent);
	    skinnedBitangent = normalize(mat3(skinMatrix) * vertexBitangent);

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
	vWorldPos = vec3(matModel * vec4(skinnedPos, 1.0));

	// TBN in view space
	vec3 mv_normal = mv_normalMatrix * skinnedNormal;
	vec3 mv_tangent = mv_normalMatrix * skinnedTangent;
	vec3 mv_bitangent = mv_normalMatrix * skinnedBitangent;

    mv_tbn = mat3(mv_tangent.x,   mv_tangent.y,   mv_tangent.z,
              mv_bitangent.x, mv_bitangent.y, mv_bitangent.z,
              mv_normal.x,    mv_normal.y,    mv_normal.z);

	viewDirRaw = normalize(-vPos);
	refractVertex(skinnedNormal, vPos);

	// SAO o2 normal of a bit-26 effect (RE_SAO_BOTH 12.6.3): VS 420 / 431 transform the node's posed normal (World = the app's
	// matModel frame with the pose, user decision rev-31 A); VS 425 reads the bind-pose normal raw.
	vec3 sseAoNPre = bGPUSkinning ? vertexNormal : vertexPreSkinNormal;
	SseEngineFrame sseAoF = SseEngineFrameFromGL(matModel, matView);
	if (uSseAoEffectClass == 1)      vSseAoEffectNormal = SseAoN_VS420(skinnedNormal, sseAoF);
	else if (uSseAoEffectClass == 2) vSseAoEffectNormal = SseAoN_VS425(sseAoNPre);
	else if (uSseAoEffectClass == 3) vSseAoEffectNormal = SseAoN_VS431(skinnedNormal, sseAoF);
	else                             vSseAoEffectNormal = vec3(0.0, 0.0, 1.0);
	// t00010153: v = normalize(-pos), n = normalize(3x3 * normal) in the same frame;
	// t = sat((|n.v| - start) / (stop - start)); o1.z = t*t*(3 - 2t) * (stopOpacity - startOpacity) + startOpacity.
	vEffectFalloff = 1.0;
	if (bVertexFalloff)
	{
		float nv = abs(dot(normalize(mv_normalMatrix * skinnedNormal), viewDirRaw));
		float t = clamp((nv - effectFalloffParams.x) / (effectFalloffParams.y - effectFalloffParams.x), 0.0, 1.0);
		vEffectFalloff = (t * t * (-2.0 * t + 3.0)) * (effectFalloffParams.w - effectFalloffParams.z) + effectFalloffParams.z;
	}
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

// ##################################################################################################
// ####  SHADOW SYNC CONTRACT -- RENDER SHADERS MUST SYNCHRONIZE THEIR LOGIC WITH THE SHADOW PASS  ###
// ##################################################################################################
//
//   ANY CHANGE TO THE ALPHA LOGIC IN THIS SHADER MUST BE MIRRORED IN THE SHADOW DEPTH PASS:
//   ==>  ShadowDepthShaderSource.Fragment_ShadowDepth  (Shader_Class.vb)
//   And the other way round: that pass points back here. The two are ONE law written twice.
//
// WHY. The shadow map is drawn by a DIFFERENT program, and that program decides WHICH FRAGMENT
// EXISTS using the same alpha this shader uses. If the two stop agreeing, the shadow silhouette
// stops being the silhouette of the object on screen: shadow appears for geometry that was
// discarded, or is missing for geometry that is drawn. Nothing reports it -- it just looks wrong,
// and the cause is two files away.
//
// THE LIST. Touch any of these here, and go fix Fragment_ShadowDepth in the same commit:
//   * the ALPHA TEST -- which quantity is compared, and against which threshold
//   * the VERTEX ALPHA -- linear or gamma-corrected, and which predicate gates it
//   * the MATERIAL ALPHA SCALAR -- whether it enters the test or is applied afterwards
//   * GREYSCALE-TO-PALETTE on alpha -- it REPLACES the alpha with a palette lookup
//   * FALLOFF -- see the divergence list below: it does NOT work the same in both games
//   * the ZAP discard
//   * SKINNING -- if a vertex moves, its shadow moves too
//   * the UV transform (offset / scale) used to sample the diffuse
//
// !!!! TWO GAMES, TWO LAWS -- NOT ONE. Fallout 4 and Skyrim SE do NOT agree here, and the depth pass
// carries BOTH, selected by the bLeySse uniform. The FOUR divergences, all measured, all of which
// caused a real bug when someone assumed there was only one law:
//   1. effect shader (.bgem) vertex alpha: FO4 uses pow(vColor.a, 2.2), SSE uses it LINEAR.
//   2. lighting shader (.bgsm) material scalar: SSE multiplies it BEFORE the test, FO4 after.
//   3. FALLOFF ON THE ALPHA, with greyscale-to-palette recolor:
//        FO4 folds falloff into the palette V *AND* multiplies by it again afterwards -- falloff^2,
//            on purpose, that is what the engine does.
//        SSE folds it into the palette V and does NOT multiply afterwards -- so it is one or the
//            other, never both. Assuming FO4's rule here makes a .bgem cast nothing at all.
//      Without recolor, both games multiply once.
//   4. (WITHDRAWN) the falloff curve is the SAME in both games, measured on the vertex shaders: FO4
//      rec0440 and SSE t00010153 both do t = sat((|n.v| - start) / (stop - start)) (div_sat),
//      t*t*(3 - 2t), then t*(stopOp - startOp) + startOp -- no clamp on the opacities, per vertex.
// So a change that is correct for one game can be wrong for the other. Check both, always.
//
// ALSO: this GLSL lives inside a VB Const String. It must be PURE ASCII, and it must NOT contain a
// double quote -- a quote closes the VB literal, and escaping it as a pair sends a stray quote to
// the driver. The glsl-ascii gate in Tools/ParityGate enforces both.
// ##################################################################################################
// SSE fragment shader with model-space normal (MSN) support

/*
 * BodySlide and Outfit Studio
 * Shaders by jonwd7 and ousnius
 * https://github.com/ousnius/3rd party references/BodySlide-and-Outfit-Studio
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
// LIGHTING technique (bit 16): Effect_Lighting AND a static byte that is 1 (0x14152A377..A39B).
uniform bool bEffectLighting;
// L of the effect PS (cb2[7] + up to 4 point lights, no N.L): the effect light of the chosen weather and moment kept
// in the game's proportion to the preview key (PreviewImagingRow.EffectLightForKey), raw. Flat: one value per draw.
uniform vec3 effectLight;
// APP OPTION, NOT THE ENGINE: see Fragment_FO4 (bLegacyDisplay, uSceneToLinear).
uniform bool bLegacyDisplay;
uniform float uSceneToLinear;
// The falloff arrives from the VS (vEffectFalloff), see Vertex_SSE.
uniform bool bVertexFalloff;
in float vEffectFalloff;

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
in vec3 vWorldPos;

" & ShadowDepthShaderSource.SharedUniformsGlsl & "
layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec4 coverageOut;
// SSE SAO normals target (o2 of the opaque MRT, RE_SAO_BOTH 7.1 / 11-12); written only while draw buffer 3 is bound (SSE + post).
layout(location = 3) out vec4 aoNormalOut;
uniform int uSseAoNormal;        // SseAoNormal: 0 none, 1 normal, 2 colour
uniform int uSseAoEffectClass;   // SseAoEffectNormal: 0 lighting (sseAoLitNormal), 1 view (VS 420), 2 raw skinned (VS 425), 3 world (VS 431)
uniform vec4 uSseSsrParams;      // cb2[7] of PS 4255: (fSpecMaskBegin, fSpecMaskBegin + fSpecMaskSpan, 0, SSRParams.w)
in vec3 vSseAoEffectNormal;      // TEXCOORD7.yzw of the bit-26 effect VS
vec3 sseAoLitNormal = vec3(0.0, 0.0, 1.0);
// What this draw adds to the coverage of the HDR target (see SceneTargets / Fragment_FO4): 0 unblended (1),
// 1 blended with its own light (alpha), 2 destination-only blend (0).
uniform int uCoverageMode;

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


" & LegacyDisplaySource.Tonemap_Glsl & SoftEffectSource.Soft_Glsl & SseLitTailSource.Tail_Glsl & "
// SSE MULTBLEND (technique bit 11 = NiAlphaProperty dest blend SRC_COLOR, 0x14152A4BE..A570): the soft
// permutation has no 0.003 discard (census of the 3216 PS: discard <=> SOFT and not MULTBLEND,
// Tools/re-docs/RE_SSE_EFFECT_BIT11_2026-10-03.md).
uniform bool bSseMultBlend;
// SSE MULTBLEND_DECAL (technique bit 22 = src DEST_COLOR + dst INV_SRC_ALPHA, same builder).
uniform bool bSseMultBlendDecal;
// SSE BSLighting Hair technique with DEPTH_WRITE_DECALS (SseRenderPassLaw): the 4/255 discard and saturate(1.05 a).
uniform bool bSseHairDepthWriteDecal;
// invFrameBufferRange (sseInvFramebufferRange) and the lighting output tail: SseLitTailSource.
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
// VIEW-space direction -> WORLD. Extracted from hemiAmbient so the shadow lookup uses the SAME
// expression: two copies of a view->world convention is exactly the kind of drift that shows up as a
// hemisphere that rotates one way and a shadow that rotates the other.
" & ShadowDepthShaderSource.WorldDirGlsl & "

// SkinTint tec 5 soft-light (PS idx 8577). Sede unica del texto: SseOverlayCompositor.SoftLightPegtopEngineGlslFn.
" & SseOverlayCompositor.SoftLightPegtopEngineGlslFn & "

vec3 hemiAmbient(in vec3 nrm)
{
	vec3 nWS = toWorldDir(nrm);
	return mix(ambientGround, ambientSky, clamp(nWS.z * 0.5 + 0.5, 0.0, 1.0));
}

// 1 = fully lit, 0 = fully shadowed. Only ever called when bShadows is true.
" & ShadowDepthShaderSource.SharedLookupGlsl & "

" & SsePrepassSource.Prepass_Glsl & "
" & D3d11SemanticsSource.Glsl & SseAoNormalSource.Ps_Glsl & "
void main(void)
{
	viewDir = normalize(viewDirRaw);   // engine: rsq(dot(v6,v6)) por pixel, ver la nota del varying
    uv = vUV * uvScale + uvOffset;
	// Z-PREPASS draw of this shape (SseRenderPassLaw / SsePrepassSource): depth only, the colour is masked off.
	if (bSsePrepass)
	{
		if (bHide || (bApplyZap && ZappedVert == 1))
			discard;
		if (bSsePrepassAlphaTest)
		{
			float texA = bShowTexture ? texture(texDiffuse, uv).a : 1.0;
			if (!ssePrepassKeeps(texA, vColor.a, bIsEffectShader, bEffectGreyscaleAlpha, effectBaseColorAlpha))
				discard;
		}
		fragColor = vec4(0.0);
		return;
	}
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

			// El sampleo del normal map va FUERA de `bLightEnabled`, igual que en Fragment_FO4: el bloque
			// del EFFECT shader (bIsEffectShader) es HERMANO de bLightEnabled, no esta anidado bajo el, y
			// lee `normalMap`. Con el sampleo adentro, un bLightEnabled=False dejaba normalMap en su valor
			// inicial y el camino BGEM leia basura.
			if (bNormalMap)
			{
				normalMap = texture(texNormal, uv);
			}

			if (bLightEnabled)
			{
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

		// LA NORMAL SE CALCULA ACA, FUERA DE `bLightEnabled`, y no adentro como estaba. Es el MISMO
		// arreglo que ya lleva Fragment_FO4, por el mismo motivo y con la misma prueba.
		// Motivo: `if (bIsEffectShader)` NO esta anidado bajo bLightEnabled -- es su HERMANO, los dos
		// cuelgan de `if (!bWireframe)`; bLightEnabled ABRE y CIERRA antes de que el effect empiece.
		// Y el effect lee `normal` en DOS sitios que no cuelgan de bLightEnabled: el
		// `abs(dot(normal, viewDir))` del falloff y el `reflect(-viewDir, normal)` del cubemap. Con la semilla adentro,
		// `bLightEnabled = False` dejaba `normal` en vec3(0.0): el falloff salia constante en toda la
		// malla y el cubemap se sampleaba a lo largo del rayo de camara en vez de la reflexion.
		// No explotaba porque `SetBool(bLightEnabled, True)` es el UNICO call-site del arbol.
		//
		// POR QUE ES IDENTICO mientras el uniform sea True: ninguna sentencia que quedo en el medio LEE
		// `normal`, `normalMap` ni `geoNormal`, y nada de lo que se movio lee `albedo`. Lo que salta por
		// encima es el bloque FACEGEN (softlight + detail), que escribe `albedo` y no toca la normal.
		// MEDIDO, no argumentado: ShadowGate frame a frame, con control positivo y de determinismo.
		// Start off neutral (for MSN shapes, mv_tbn is degenerate so use v_msnMatrix)
		if (bModelSpace)
		{
			normal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
		}
		else
		{
			normal = normalize(mv_tbn * vec3(0.0, 0.0, 0.5));
		}

		// GEOMETRIC normal, captured before the normal map perturbs `normal`. Used ONLY for the
		// shadow normal-offset -- shading keeps using the perturbed one. Same reason as in the FO4
		// fragment: offsetting along a normal-mapped direction turns part of the push into a lateral
		// slide along the surface and leaves acne that follows the TEXTURE detail.
		vec3 geoNormal = normal;

		if (bShowTexture && bNormalMap)
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

		// !! EN MODEL-SPACE NORMALS, `geoNormal` NO SIRVE: la semilla de arriba es el eje +Z del
		// OBJETO llevado a vista (v_msnMatrix * (0,0,1)), o sea una direccion CONSTANTE por shape,
		// no la normal del fragmento. Usarla para el normal-offset empujaba toda la malla en la
		// misma direccion y en la mitad opuesta el offset apuntaba HACIA ADENTRO de la superficie:
		// acne solido. En MSN la normal del mapa ES la normal de la geometria (el mapa guarda la
		// normal de objeto, no una perturbacion tangente), asi que es la correcta para el offset.
		if (bModelSpace)
			geoNormal = normal;

		// The normal the lighting uses, before the double-sided flip: the engine's lighting PS never flips (RE_SAO_BOTH 12.6.2,
		// 0/6924 with SV_IsFrontFace) and writes this same n to o2.
		sseAoLitNormal = normal;

		// Double-sided: flip normal for back faces
		if (bDoubleSided && !gl_FrontFacing)
		{
			normal = -normal;
			geoNormal = -geoNormal;
		}

		if (bLightEnabled)
		{
			// Lighting with or without textures
			outDiffuse = vec3(0.0);
			outSpecular = vec3(0.0);

				// GREYSCALE-TO-PALETTE: SSE-only divergence from FO4. The Skyrim BSLightingShader
				// pixel shader has NO greyscale path: VanillaGetLightingShaderDefines (0x14151C2D0)
				// emits no greyscale #define, and GRAYSCALE_TO_COLOR/GRAYSCALE_TO_ALPHA live ONLY in
				// the BSEffectShader define block (BSXShaderSamplers, 0x1ac7840/58) alongside the
				// dedicated GrayscaleSampler. In SSE the recolor is exclusively a BSEffectShaderProperty
				// feature, handled in the effect path below (bIsEffectShader). So a BSLightingShaderProperty
				// that carries the SLSF1 greyscale flag is rendered WITHOUT recolor by the engine -> no-op
				// here. (FO4 differs: its lighting shader rec2389/rec2963 DO recolor; see Fragment_FO4.)
				// The material flag is preserved for round-trip; only the lit render ignores it.


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


			// SHADOW. Same law and same placement as the FO4 fragment (see the SHADOWS uniform block):
			// the SSE DEFSHADOW path multiplies the DIRECTIONAL by the mask and adds the ambient after,
			// so scaling each light-s own diffuse here reproduces it and leaves hemiAmbient() alone.
			// EVERY light with a layer casts; the branches are uniform per draw.
			DirectionalLight keyLight = frontal;
			DirectionalLight fillL = directional0;
			DirectionalLight fillR = directional1;
			DirectionalLight backL = directional2;
			if (bShadows)
			{
				vec3 wn = toWorldDir(geoNormal);
				if (uShadowSlot[0] >= 0) keyLight.diffuse *= shadowFactorAt(vWorldPos, wn, uShadowSlot[0]);
				if (uShadowSlot[1] >= 0) fillL.diffuse    *= shadowFactorAt(vWorldPos, wn, uShadowSlot[1]);
				if (uShadowSlot[2] >= 0) fillR.diffuse    *= shadowFactorAt(vWorldPos, wn, uShadowSlot[2]);
				if (uShadowSlot[3] >= 0) backL.diffuse    *= shadowFactorAt(vWorldPos, wn, uShadowSlot[3]);
			}

			directionalLight(keyLight, lightFrontal, outDiffuse, outSpecular);
			directionalLight(fillL, lightDirectional0, outDiffuse, outSpecular);
			directionalLight(fillR, lightDirectional1, outDiffuse, outSpecular);
			directionalLight(backL, lightDirectional2, outDiffuse, outSpecular);

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
				sd = softLightPegtopEngine(sd, tintColor);
				sd *= " & SseOverlayCompositor.SkinTintRgbFixGlsl & ";   // rgbFix tec 5: sede unica SseOverlayCompositor.SkinTintRgbFixGlsl
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

			// OUTPUT TAIL of every SSE lighting PS (6924/6924; Tools/re-docs/RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 2):
			// out = min(t*Y + C, lit) - t*Y*Z, t = lit - k*f, f = lerp(lit, fogColor, fogF), k = invFrameBufferRange.
			// cb12[42]: Y = 1; Z = sseLitTailZ (0 opaque, 1 translucent group 1). No fog in the preview (user decision,
			// 3-oct-2026): f = lit. Opaque: out = lit - max(0, k*lit - C) (identity up to C/k, compressed above);
			// translucent: out = min(C, k*lit).
			color.rgb = sseLitTail(color.rgb, color.rgb, 1.0, sseLitTailZ);
		}

		// SSE BSEffectShader (the effect PS family, permutations 0x42 / 0x10042 / 0x10153 / 0x90073 / 0x100042 /
		// 0x8042 of the dump): NO cube, NO emissive, NO N.L, NO Fresnel (0 of its 3216 PS declare a cube).
		//   colour : tex.rgb * cb1[0].rgb * vc.rgb; with palette colour (bit 19) palette(U = tex.g,
		//            V = bc.r * vc.r).rgb * scale -- V takes no falloff and no scale.
		//   alpha  : tex.a * falloff * bc.a * vc.a * cb2[8].w (1 unfaded); with palette alpha (bit 20)
		//            palette(U = tex.a, V = falloff * bc.a * vc.a).a, not multiplied again.
		//   light  : LIGHTING c = lerp(c, c * L, cb1[2].x), L = 4 point lights (none here) + cb2[7]; without
		//            it lerp(c, c * PropertyColor, cb1[2].x) with PropertyColor (1,1,1) for a NIF effect = c.
		//   tail   : SOFT (bit 18) on the alpha, then rgb * invFrameBufferRange (TEXCOORD5) unless MULTBLEND /
		//            MULTBLEND_DECAL; no clamp. The fog lerp before it is the identity here (no fog in the preview).
		if (bIsEffectShader)
		{
			float effFalloff = 1.0;
			if (bVertexFalloff)
				effFalloff = vEffectFalloff;
			else if (bEffectFalloff)
			{
				// Geometry without vertex normals: per-pixel with the same curve (see Vertex_SSE).
				float nvP = abs(dot(normal, viewDir));
				float tP = clamp((nvP - effectFalloffParams.x) / (effectFalloffParams.y - effectFalloffParams.x), 0.0, 1.0);
				effFalloff = (tP * tP * (-2.0 * tP + 3.0)) * (effectFalloffParams.w - effectFalloffParams.z) + effectFalloffParams.z;
			}
			float aBase = effectBaseColorAlpha * vColor.a;
			vec3 effRgb = bGreyscaleColor
				? colorLookup(baseMap.g, effectBaseColor.r * vColor.r).rgb * effectBaseColorScale
				: baseMap.rgb * effectBaseColor * vColor.rgb;
			// SOFT (idx 795 L40-58; palette alpha idx 1045): it scales the accumulated alpha A, or with palette
			// alpha the V coordinate before the lookup, never rgb; discard when A*soft < 0.003 unless MULTBLEND.
			float softA = bEffectGreyscaleAlpha ? effFalloff * aBase : baseMap.a * effFalloff * aBase;
			if (bSoftEffect)
			{
				float soft = softSse();
				if (!bSseMultBlend && softA * soft - 0.003 < 0.0)
					discard;
				softA *= soft;
			}
			// cb2[8].w = prop+0x30 = currentFade * baseColor.a (0x141557B7D..B82): baseColor.a enters a second time, after
			// SOFT; with palette alpha it goes into V and the looked-up alpha is not multiplied again (idx 691 / 800 / 956;
			// Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 12.2).
			float effAlpha = bEffectGreyscaleAlpha
				? colorLookup(baseMap.a, softA * sseEffectPropAlpha).a
				: softA * sseEffectPropAlpha;
			if (bEffectLighting)
				effRgb = mix(effRgb, effRgb * effectLight, effectLightingInfluence);
			if (!bSseMultBlend && !bSseMultBlendDecal)
				effRgb *= sseInvFramebufferRange;
			color = vec4(effRgb, effAlpha);
		}

		if (bShowMask)
		{
          color.rgb *= maskFactor;
		}

		if (bShowWeight)
		{
			color.rgb *= weightColor;
		}

		// RAW, LIKE THE ENGINE: SSE's PS (lighting and effect) write their colour with no curve and no encode, into
		// the R11G11B10 HDR target; the curve is the post (ImageSpace HDR PS 12545, PostProcess.vb).
		// POST OFF (bLegacyDisplay): THE 2.3.8 DISPLAY TAIL, NOT THE ENGINE, applied to the value in the space
		// 2.3.8 gave it. Lit: 2.3.8 shaded it LINEAR (sRGB-SRV diffuse) and encoded 1/2.2; the SSE value is raw
		// (display) now -> to linear (2.2), Hable / Hable(1), 1/2.2. Effect: 2.3.8 tonemapped its display-space
		// value with no encode; raw now -> Hable / Hable(1).
		if (bLegacyDisplay)
		{
			if (bIsEffectShader)
				color.rgb = tonemap(max(color.rgb, vec3(0.0))) / tonemap(vec3(1.0));
			else
				color.rgb = legacyLitDisplay(color.rgb, uSceneToLinear);
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

	// No clamp: o0 is not saturated. The wireframe branch is drawn on the display target (RGBA8, saturates on write).
	fragColor = color;



//====================DEBUG MODE==========================
if (DebugMode > 0.0) {
    // MISMA ESTRUCTURA QUE EL BLOQUE DE Fragment_FO4, y la ley de cada rama copiada del camino
    // ILUMINADO DE ESTE fragment -- que no es igual al de FO4 y por eso no se puede compartir el
    // texto: en tangent-space SSE NO reconstruye el canal azul (usa los tres canales del mapa tal
    // cual) y FO4 si lo reconstruye. Ver los dos `if (bShowTexture && bNormalMap)` de cada fragment.
    vec3 dbgNormal;
    if (bModelSpace) {
        // EN MSN LA NORMAL DE LA GEOMETRIA ES LA DEL MAPA, no la semilla. Lo dice el camino iluminado
        // de este mismo fragment, que en esta rama hace `geoNormal = normal` con la razon escrita al
        // lado: la semilla `v_msnMatrix * (0,0,1)` es el eje +Z del OBJETO, o sea una direccion
        // CONSTANTE por shape -- dibujarla pinta la cabeza entera de un color plano que no dice nada
        // del fragmento. El mapa guarda la normal de objeto (no una perturbacion tangente), asi que
        // decodificarlo ES leer la normal de la geometria.
        // Se muestrea texNormal y NO se reusa `normalMap`: esa variable arranca en vec4(0.0) y solo se
        // llena dentro de la rama de iluminacion, asi que con bLightEnabled apagado saldria negra.
        if (bShowTexture && bNormalMap) {
            dbgNormal = normalize(v_msnMatrix * (texture(texNormal, uv).rbg * 2.0 - 1.0));
        } else {
            dbgNormal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
        }
    } else {
        dbgNormal = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));
    }

    // Gris plano = `esta vista NO APLICA a esta shape`. Ver el bloque gemelo de FO4.
    // OJO: la version anterior SI dibujaba algo en model-space, y las tres cosas que dibujaba estaban
    // mal. Tangente y bitangente salian de `v_msnMatrix * X/Y`, que son los EJES DEL OBJETO llevados a
    // vista -- no una base tangente, que en MSN no se usa. Y el modo 4 comparaba la normal del mapa
    // contra el eje +Z del objeto y llamaba `error de TBN` a esa diferencia, que es grande en toda la
    // malla POR DISENO (para eso existe el mapa). O sea: rojo por todos lados y ninguna conclusion.
    vec4 noAplica = vec4(0.5, 0.5, 0.5, 1.0);

    if (abs(DebugMode - 1.0) < 0.5) {
        // Modo 1: la normal GEOMETRICA (la semilla del camino iluminado), en espacio de vista.
        // ANTES en MSN esta vista decodificaba `normalMap`, con dos problemas: mezclaba `normal de la
        // geometria` con `normal del mapa` segun la shape -- dos cantidades distintas bajo un mismo
        // nombre -- y leia una variable que arranca en vec4(0.0) y solo se llena dentro de la rama de
        // iluminacion, asi que con bLightEnabled apagado la vista salia negra.
        fragColor = vec4(dbgNormal * 0.5 + 0.5, 1.0);
    }
    else if (abs(DebugMode - 2.0) < 0.5) {
        // Modo 2: tangentes. Solo en tangent-space.
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            fragColor = vec4(normalize(mv_tbn * vec3(1.0, 0.0, 0.0)) * 0.5 + 0.5, 1.0);
        }
    }
    else if (abs(DebugMode - 3.0) < 0.5) {
        // Modo 3: bitangentes. Solo en tangent-space.
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            fragColor = vec4(normalize(mv_tbn * vec3(0.0, 1.0, 0.0)) * 0.5 + 0.5, 1.0);
        }
    }
    else if (abs(DebugMode - 4.0) < 0.5) {
        // Modo 4: error de TBN. NO APLICA en model-space: ahi no hay TBN que comparar.
        if (bModelSpace) {
            fragColor = noAplica;
            return;
        }
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
            // SIN reconstruir el azul: asi decodifica el camino iluminado de SSE (a diferencia de
            // FO4, que si lo reconstruye). Antes esta rama hacia el sqrt de FO4 y por lo tanto
            // media el error sobre una normal que este juego nunca calcula.
            n_ts = texture(texNormal, uv).rgb * 2.0 - 1.0;
        }
        nA = normalize(mv_tbn    * n_ts);
        nB = normalize(tbn_fixed * n_ts);

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
    else if (abs(DebugMode - 5.0) < 0.5) {
        // Modo 5: EL NORMAL MAP CRUDO, tal cual lo devuelve el sampler. Sin decodificar (nada de
        // *2-1), sin el swizzle .rbg del model-space y sin pasar por la TBN ni por v_msnMatrix: se ve
        // el contenido del archivo. Sirve igual para las dos familias de SSE, y ademas es la forma de
        // ver de un vistazo cual es cual: un normal map tangent-space es azulado (Z domina) y uno
        // model-space es multicolor.
        // OJO: se muestrea texNormal y NO se reusa `normalMap`: esa variable arranca en vec4(0.0) y solo
        // se llena adentro de la rama de iluminacion, asi que con bLightEnabled apagado esta vista
        // habria salido NEGRA sin que nada lo dijera.
        if (bShowTexture && bNormalMap) {
            fragColor = vec4(texture(texNormal, uv).rgb, 1.0);
        } else {
            fragColor = vec4(0.5, 0.5, 1.0, 1.0);
        }
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
		// Hair technique with DEPTH_WRITE_DECALS (descriptor bit 15; SseRenderPassLaw): a < 4/255 is discarded, then
		// the alpha is saturate(1.05 a) for the test below AND for the output (idx 9031 l. 62-69, 96;
		// Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 13).
		if (bSseHairDepthWriteDecal)
		{
			if (fragColor.a - 0.015686 < 0.0)
				discard;
			fragColor.a = clamp(1.05 * fragColor.a, 0.0, 1.0);
		}

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

	// SAO normals target (RE_SAO_BOTH 11-12). Engine view = (x, y, -z_gl).
	if (uSseAoNormal == 2)
		aoNormalOut = fragColor;
	else if (uSseAoNormal == 1)
	{
		if (uSseAoEffectClass == 0)
		{
			float m = bModelSpace ? specMap.r : normalMap.a;
			aoNormalOut = SseAoN_PS4255_N(vec3(sseAoLitNormal.xy, -sseAoLitNormal.z), m, uSseSsrParams);
		}
		else
			aoNormalOut = SseAoN_PS2357(vSseAoEffectNormal);
	}
	else
		aoNormalOut = vec4(0.0);
	if (uCoverageMode == 1)
		coverageOut = vec4(fragColor.a, fragColor.a, 0.0, fragColor.a);
	else if (uCoverageMode == 2)
		coverageOut = vec4(0.0);
	else
		coverageOut = vec4(1.0);
}
"
    Sub New()
        MyBase.New(Vertex_SSE, Fragment_SSE)
    End Sub
End Class
''' <summary>THE ALPHA TEST OF SKYRIM SE'S Z-PREPASS (BSUtilityShader depth techniques, slot 0x2B), in one place: Fragment_SSE
''' runs it in its prepass branch and ShadowGate --sse-prepass-law runs this same text. Tools/re-docs/
''' RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 10. ASCII only (GLSL). Uses colorLookup (texGreyscale) of the including
''' fragment.</summary>
Friend Module SsePrepassSource
    Friend Const Prepass_Glsl As String = "
// Z-PREPASS (SseRenderPassLaw decides who enters and the thresholds; Render.vb uploads them).
uniform bool bSsePrepass;
// The prepass PS has the alpha test (technique bit 7 = the NiAlphaProperty test).
uniform bool bSsePrepassAlphaTest;
// Lighting decal variant (0x22082): A = saturate(tex.a * 1.05) before the vertex alpha.
uniform bool bSsePrepassDecal;
// thrG = cb2[2].x (0x141567EC3..F27): 254/255 with blend, else ref/255 + 0.00392153 (+1/255 when ref == 4).
uniform float ssePrepassThrG;
// thrS = cb11[0].x: the engine's alpha-test reference when the batch subgroup has the test, else 0 (0x14101118D).
uniform float ssePrepassThrS;
// cb2[8].w / cb2[1].w = prop+0x30 = currentFade * baseColor.a of an effect (0x141567EBE, 0x141557B7D).
uniform float sseEffectPropAlpha;

// True when the prepass keeps the fragment. Every test discards on value - threshold < 0 (lt + discard_nz): equality
// is kept. texA = diffuse / base texture alpha (t0); vc = the linear vertex alpha the VS passes (COLOR.w; 1 without).
// Lighting (idx 11305): A = tex.a * vc; no material alpha, no palette.
// Effect (idx 11465; palette 11466): k = baseColor.a (cb1[1].w) * prop+0x30 (cb2[1].w) * vc; A0 = tex.a * k, or
// palette(U = tex.a, V = tex.a * k).a; tests A0 vs thrS, A0 * vc vs thrG, A0 * vc vs thrS. No falloff, no SOFT.
bool ssePrepassKeeps(float texA, float vc, bool isEffect, bool paletteAlpha, float baseColorA)
{
	if (!isEffect)
	{
		float a = (bSsePrepassDecal ? clamp(texA * 1.05, 0.0, 1.0) : texA) * vc;
		return !(a - ssePrepassThrG < 0.0) && !(a - ssePrepassThrS < 0.0);
	}
	float k = baseColorA * sseEffectPropAlpha * vc;
	float a0 = paletteAlpha ? colorLookup(texA, texA * k).a : texA * k;
	return !(a0 - ssePrepassThrS < 0.0) && !(a0 * vc - ssePrepassThrG < 0.0) && !(a0 * vc - ssePrepassThrS < 0.0);
}
"
End Module

''' <summary>THE REFRACTION OF BOTH GAMES, in one place (Tools/re-docs/RE_REFRACTION_BOTH_2026-10-03.md): the refraction-normals
''' pass of a refracting shape (BSUtilityShader technique bit 9; SSE 0x141520A10, FO4 0x1421D6540) and the image-space
''' ISRefraction that distorts the scene with it (SSE PS idx 15822, FO4 rec 3658). The vertex part is included by Vertex_SSE
''' and Vertex_FO4 (the pass draws a shape with its game's VS); the two fragments are programs of their own (RefractionPass,
''' Render.vb). ASCII only (GLSL).</summary>
Friend Module RefractionSource
    ''' <summary>Per-vertex terms of the normals VS (SSE idx 10969.., FO4 rec 1692..), computed by every draw of the VS and read
    ''' only by the refraction pass.</summary>
    Friend Const Vertex_Glsl As String = "
// REFRACTION NORMALS VS terms (read only by the refraction pass, RefractionSource).
// n = 2*NORMAL-1 raw (object space); model-space normals: the constant refractMsnNormal (SSE (1,1,1), FO4 (0,0,1));
// skinned: the skinned normal normalized. n_v = view * W3x3 * n (cb12[0..2]); clamped to +-0.1 with technique bit 11.
// d = max(0.8 + 0.001333 * clip.z, 1), clip.z = the D3D projection's z row: (w - n) * f / (f - n), w = view depth.
// falloff (bit 10): dot(n, normalize(cameraObject - pos)) in object space = in view space divided by W's uniform scale.
uniform vec3 refractMsnNormal;
uniform bool bRefractSkinned;
uniform bool bRefractClamp;
uniform vec2 uRefractNearFar;
out vec4 vRefractN;
out float vRefractD;
void refractVertex(vec3 skinnedNormal, vec3 vPos)
{
	vec3 nObj = bModelSpace ? refractMsnNormal : (bRefractSkinned ? normalize(skinnedNormal) : skinnedNormal);
	mat3 mv3 = mat3(matModelView);
	vec3 nv = mv3 * nObj;
	float escala = length(mv3[0]);
	float falloffDot = dot(nv / max(escala, 1e-20), normalize(-vPos));
	if (bRefractClamp)
		nv = clamp(nv, vec3(-0.1), vec3(0.1));
	vRefractN = vec4(nv, falloffDot);
	float n = uRefractNearFar.x;
	float f = uRefractNearFar.y;
	float clipZ = (-vPos.z - n) * f / (f - n);
	vRefractD = max(0.8 + 0.001333 * clipZ, 1.0);
}
"

    ''' <summary>The normals PS (SSE idx 11290 / 11294, FO4 the same without the never-taken discard).</summary>
    Friend Const Normals_Fragment As String = "#version 430
// REFRACTION NORMALS PS: N = diffuse (lighting) / base (FO4 effect) texture .xy;
// d = (N - 0.5) * 1.8, or with technique bit 11 clamp(2 (N - 0.5), +-0.1) * 0.9;
// out.xy = 0.5 + 0.5 (d + n_v.xy) / d_depth; out.z = s * strength * vertex alpha; out.w = 1.
// s = the LOD fade (times the falloff dot with technique bit 10). The target saturates (SSE RGBA8) or not (FO4 R11G11B10F).
uniform sampler2D texDiffuse;
uniform vec2 uvScale;
uniform vec2 uvOffset;
uniform bool bRefractClampPs;
uniform bool bRefractFalloff;
uniform bool bRefractVertexAlpha;
uniform float refractStrength;
uniform float refractFade;
uniform bool bApplyZap;
in vec4 vColor;
in vec2 vUV;
in vec4 vRefractN;
in float vRefractD;
flat in int ZappedVert;
out vec4 oNormals;
void main(void)
{
	if (bApplyZap && ZappedVert == 1)
		discard;
	vec2 N = texture(texDiffuse, vUV * uvScale + uvOffset).xy;
	vec2 d = bRefractClampPs ? clamp(2.0 * (N - 0.5), -0.1, 0.1) * 0.9 : (N - 0.5) * 1.8;
	float s = refractFade * (bRefractFalloff ? vRefractN.w : 1.0);
	oNormals = vec4(0.5 + 0.5 * (d + vRefractN.xy) / vRefractD, s * refractStrength * (bRefractVertexAlpha ? vColor.a : 1.0), 1.0);
}
"

    ''' <summary>The fullscreen VS of the image-space pass (the engine's passes are a quad with uv; here a triangle).</summary>
    Friend Const ImageSpace_Vertex As String = "#version 430
out vec2 vUv;
void main(void)
{
	vec2 p = vec2((gl_VertexID & 1) * 4 - 1, (gl_VertexID >> 1) * 4 - 1);
	vUv = p * 0.5 + 0.5;
	gl_Position = vec4(p, 0.0, 1.0);
}
"

    ''' <summary>ISRefraction. GL uv has v up where D3D has it down: the engine's p.y = v + o.y is v - o.y here (the edge
    ''' compression is symmetric around 0.5, so it reads the same).</summary>
    Friend Const ImageSpace_Fragment As String = "#version 430
// ISRefraction (SSE PS idx 15822, FO4 rec 3658):
//   N = t1(uv); o = k * N.z * (N.xy - 0.5) (k 0.1 SSE, 0.25 FO4); p = uv displaced by o;
//   c(p) = p > 0.85 ? (p - 0.85) * 0.78 + 0.85 : p; then p < 0.15 ? 0.15 - (0.15 - p) * 0.78 : c(p) (per component);
//   q = p + N.z * (c(p) - p); M = t1(q).w; S1 = t0(q); S0 = t0(uv); C = M != 0 ? S1 : S0;
//   rgb = (0.8 < N.w < 1) ? (1 - Tint.w) C.rgb + dot(lum, S1.rgb) Tint.w Tint.rgb : C.rgb; alpha SSE S0.a, FO4 C.a.
//   The tint never applies (N.w is 1 on a refracting pixel, 0 on the clear; FO4's target has no alpha: 1).
//   t0 = the scene, bilinear; t1 = the normals, nearest; both clamped. No dynamic resolution in the preview.
uniform sampler2D texScene;
uniform sampler2D texRefractNormals;
uniform float refractOffsetScale;
uniform bool bRefractSceneAlpha;
uniform vec4 refractTint;
in vec2 vUv;
out vec4 oColor;
float compress(float p)
{
	float c = p > 0.85 ? (p - 0.85) * 0.78 + 0.85 : p;
	return p < 0.15 ? 0.15 - (0.15 - p) * 0.78 : c;
}
void main(void)
{
	vec4 N = texture(texRefractNormals, vUv);
	vec2 o = refractOffsetScale * N.z * (N.xy - 0.5);
	vec2 p = vec2(vUv.x - o.x, vUv.y - o.y);
	vec2 q = p + N.z * (vec2(compress(p.x), compress(p.y)) - p);
	q = clamp(q, 0.0, 1.0);
	float M = texture(texRefractNormals, q).w;
	vec4 S1 = texture(texScene, q);
	vec4 S0 = texture(texScene, vUv);
	vec4 C = (M != 0.0) ? S1 : S0;
	float L = dot(vec3(0.299, 0.587, 0.114), S1.rgb) * refractTint.w;
	vec3 rgb = (0.8 < N.w && N.w < 1.0) ? (1.0 - refractTint.w) * C.rgb + L * refractTint.rgb : C.rgb;
	oColor = vec4(rgb, bRefractSceneAlpha ? S0.a : C.a);
}
"
End Module

''' <summary>The SSE SAO composite's colour law as the preview runs it (Sse_Opaque_Composite_Shader_Class). ASCII only.</summary>
Friend Module SseCompositeSource
    Friend Const Fragment As String = "#version 430
// SSE ISSAOCompositeSAOFog (PS 16004): c = scene (+ SSR, + snow); c *= ao; geometry: c = lerp(c, fog, F) * invFrameBufferRange;
// sky (depth >= 0.999999): no fog, no invFBR; out = saturate(c). Preview: no SSR / snow / fog / AO pass: a mesh pixel gets
// saturate(scene * invFBR), alpha saturate(scene.a). The engine's geometry is the meshes: a pixel no mesh drew (coverage g,
// written only by mesh draws, is 0: the background and the app's floor) is left as it is.
uniform sampler2D texScene;
uniform sampler2D texCoverage;
// kSAO (RT 0x2E), written by the SAO passes in D3D row order: read at (u, 1 - v), point sampling at texel centres (RE_SAO_BOTH 1.7).
uniform sampler2D texSao;
uniform float sseInvFramebufferRange;
in vec2 vUv;
out vec4 oColor;
void main(void)
{
	vec4 c = texture(texScene, vUv);
	if (texture(texCoverage, vUv).g == 0.0)
	{
		oColor = c;
		return;
	}
	c.rgb *= texture(texSao, vec2(vUv.x, 1.0 - vUv.y)).x;   // PS 16004 mul r3.xyz, r0.xxxx, r2.xyzx
	oColor = clamp(vec4(c.rgb * sseInvFramebufferRange, c.a), 0.0, 1.0);
}
"
End Module

''' <summary>THE SOFT FADE OF THE EFFECT SHADERS (FO4 technique bit 12, SSE bit 18; SLSF1 bit 30 = BGEM SoftEnabled), the
''' part both engines share: the scene depth behind the pixel and the base factor. Each fragment adds its own game's
''' terms. Tools/re-docs/RE_BGEM_SOFT_FO4_2026-10-03.md, RE_BGEM_SOFT_SSE_2026-10-03.md. ASCII only (GLSL).</summary>
''' <summary>SSE invFrameBufferRange and THE OUTPUT TAIL OF THE SSE LIGHTING PS, in one place (Fragment_SSE calls it; the
''' gate ShadowGate --lit-tail runs this same text). Tools/re-docs/RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 2.
''' ASCII only (GLSL).</summary>
Friend Module SseLitTailSource
    Friend Const Tail_Glsl As String = "
// invFrameBufferRange k (BSShaderManager::State+0x9C, Address Library 390951; world value 1/1.2, .data 0x1420D694C).
// Effect PS: the VS passes it (cb0[1].w -> TEXCOORD5) and 3207 of the 3216 effect PS multiply rgb by it - all but the
// MULTBLEND (7) and MULTBLEND_DECAL (2) ones; alpha untouched (Tools/re-docs/RE_EFFECT_PARTICLE_ENVCUBE_2026-10-03.md
// Q3). Lighting PS: cb0[0].w (0x14154C010), read by sseLitTail.
uniform float sseInvFramebufferRange;
// C of the lighting tail: cb0[1].z = fLightingOutputColourClampPostSpec in the SPECULAR / AMBIENT_SPECULAR permutations,
// cb0[1].x = ...PostLit otherwise (SetupTechnique 0x141547D20; Render.vb SseLightingOutputClamp*).
uniform float sseLitOutputClamp;
// Z of the tail = cb12[42].z of the list the shape is drawn in: 0 in the opaque world pass (render flags 0x41 / 0x51),
// 1 in the alpha finish's lists 7 (group 9) and 0x10 (group 1, the translucent lighting pass), which are drawn with
// flags | 4 (0x14151F87E..F88C, 0x14151FB80; Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 3). Render.vb
// SseLitTailZ decides it per shape.
uniform float sseLitTailZ;

// The tail of all 6924 SSE lighting PS, in the bytecode's order (idx 4027 / 4030):
//   t = lit - f*k ; out = min(t*Y + C, lit) - t*Y*Z
// lit = colour * vertex colour, f = lerp(lit, COLOR1.rgb, COLOR1.a) (the fog), Y = cb12[42].y = (flags & 2) ? 0 : 1,
// Z = cb12[42].z = (flags & 4) ? 1 : 0 (0x14100AE80). World render: Y = 1 in every list (bit 1 never set).
vec3 sseLitTail(vec3 lit, vec3 f, float Y, float Z)
{
	vec3 t = lit - f * sseInvFramebufferRange;
	return min(t * Y + vec3(sseLitOutputClamp), lit) - t * Y * Z;
}
"
End Module

Friend Module SoftEffectSource
    Friend Const Soft_Glsl As String = "
// SOFT. uNearFar = (n, f) of the frame's projection (FO4 CameraData.y = n / .z = f - n, 0x1422253CD; SSE cb0[0].y/.z,
// 0x141556A3D). texSceneDepth = the scene depth copied after the opaque groups (SceneDepthCopy; FO4 t3 = depth 1,
// SSE t3 = depth 7), read unfiltered at the pixel (t3.Load(SV_Position.xy)). softDepth = S, raw (FO4 material +0x80,
// SSE +0x68; default 100).
uniform bool bSoftEffect;
uniform float softDepth;
uniform vec2 uNearFar;
uniform sampler2D texSceneDepth;

// The factor both engines compute (FO4 b05 rec1027 L35-49, SSE idx 795 L40-58), as the bytecode writes it:
// K = f*n/S (CPU: FO4 cb1[2].y 0x142225B50, SSE cb1[2].y 0x141556F8F), the pixel's w/S from the VS (FO4 TEXCOORD5.z,
// SSE v1.w; w = 1 / gl_FragCoord.w in a perspective projection), saturate(K / ((1 - d)(f - n) + n) - w/S).
// S = 0: the engine divides with no guard (inf - inf = NaN) and D3D saturate(NaN) = 0, so the effect vanishes;
// GLSL leaves clamp(NaN) undefined, hence the explicit branch.
float softBase(out float wOverS)
{
	float n = uNearFar.x;
	float f = uNearFar.y;
	wOverS = (1.0 / gl_FragCoord.w) / softDepth;
	if (softDepth == 0.0)
		return 0.0;
	float d = texelFetch(texSceneDepth, ivec2(gl_FragCoord.xy), 0).r;
	float K = f * n / softDepth;
	return clamp(K / ((1.0 - d) * (f - n) + n) - wOverS, 0.0, 1.0);
}

// SSE: the base factor alone (idx 795 L40-58).
float softSse()
{
	float wOverS;
	return softBase(wOverS);
}

// FO4: the base factor times the near term (rec1027 L35-49, all 311 SOFT PS), constants literal from the bytecode:
// nearS = K / (n(f - n) + n), t = saturate((saturate(w/S - nearS) - 0.075) * 2.352941), soft = a * t*t*(3 - 2t).
float softFo4()
{
	float wOverS;
	float a = softBase(wOverS);
	float n = uNearFar.x;
	float K = uNearFar.y * n / softDepth;
	float nearS = K / (n * (uNearFar.y - n) + n);
	float t = clamp((clamp(wOverS - nearS, 0.0, 1.0) - 0.075) * 2.352941, 0.0, 1.0);
	return a * t * t * (3.0 - 2.0 * t);
}
"
End Module

''' <summary>THE DISPLAY CURVE OF 2.3.8 (Hable, A..F = .15/.50/.10/.20/.02/.30) and its lit tail, in ONE place (each
''' fragment keeps its own effect tail, since 2.3.8 treated the effect differently per game). It is NOT the
''' engine: the game tonemaps in its post (PostProcess.vb). It is the law of the frames that do not go through
''' that post - the floor of a direct frame and the post-off tail of Fragment_FO4/Fragment_SSE - and it is
''' the curve those frames had in 2.3.8 (user decision, 2-oct-2026). ASCII only, no double quotes.</summary>
Friend Module LegacyDisplaySource
    Friend Const Tonemap_Glsl As String = "
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

// THE 2.3.8 DISPLAY TAIL OF A LIT VALUE: 2.3.8 shaded lit surfaces and the floor in LINEAR, tonemapped them with
// Hable / Hable(1) and encoded 1/2.2. toLinear = exponent from the current scene space to linear: 1 for FO4
// (linear scene), 2.2 for SSE (raw scene).
vec3 legacyLitDisplay(in vec3 x, in float toLinear)
{
    vec3 lin = pow(max(x, vec3(0.0)), vec3(toLinear));
    return pow(max(tonemap(lin) / tonemap(vec3(1.0)), vec3(0.0)), vec3(1.0 / 2.2));
}
"
End Module

''' <summary>El fragment del pase de profundidad del shadow map. UNA sola fuente para los dos juegos:
''' lo unico que hace es el alpha-test, y esa ley NO es la misma en FO4 y en SSE (ver bLeySse).
''' <para>NO LLEVA VERTEX SHADER PROPIO, y eso es el punto. El blend de skinning tiene CINCO sitios
''' gemelos declarados en el SYNC de Vertex_FO4; un VS de sombra seria el SEXTO, y desincronizarlo no
''' rompe el build ni tira: deja la sombra de una malla posada en la posicion de bind. En vez de eso el
''' pase reusa Vertex_FO4 / Vertex_SSE tal cual y les entrega matView = lightView y matProjection =
''' lightOrtho, con lo que su `gl_Position = matProjection * (matModelView * pos)` ya cae en clip-space
''' de la luz. El VS calcula ademas varyings que este fragment no consume (TBN, direcciones de luz):
''' es legal en GLSL y es el precio de no duplicar el skinning.</para></summary>
Friend Module ShadowDepthShaderSource

    ''' <summary>VIEW-space direction -&gt; WORLD (needs matModel and matModelViewInverse declared before it). ONE copy for every
    ''' fragment that takes the shadow lookup's normal or the hemisphere to world: Fragment_SSE and the FO4 G-buffer entry.</summary>
    Friend Const WorldDirGlsl As String = "vec3 toWorldDir(in vec3 v)
{
	return normalize(vec3(matModel * (matModelViewInverse * vec4(v, 0.0))));
}
"

    ''' <summary>UNA SOLA DEFINICION del lookup de sombra, concatenada dentro de los TRES fragments que
    ''' la usan (FO4, SSE y el receptor de suelo). Antes estaba copiada en el de FO4 y el de SSE: dos
    ''' copias de una convencion —el normal-offset, el signo del bias, el kernel del PCF— es exactamente
    ''' el modo de falla que documenta el SYNC del vertex shader, y ademas el suelo habria sido la tercera.
    ''' <para>El bloque de uniforms va separado del de la funcion porque en los fragments iluminados los
    ''' uniforms tienen que quedar arriba, junto al resto de los varyings, y la funcion abajo, despues de
    ''' <c>matModel</c>/<c>matModelViewInverse</c>.</para></summary>
    Friend Const SharedUniformsGlsl As String = "// =============================== SHADOWS ===============================
// ONE orthographic shadow map PER CASTING LIGHT, packed as layers of a single depth texture array.
// The measurements that put this term exactly where it is (and nowhere else) live in ShadowMap.vb; the
// short version is that in BOTH engines the shadow multiplies the DIRECTIONAL light-s diffuse AND
// specular and NEVER the ambient:
//   FO4 forward rec1498 -> L142/L154 (whole per-light accumulator), L193 (specular), and L281 adds
//   the ambient AFTERWARDS.  SSE DEFSHADOW -> mul r2.yzw, r4.xxxx, cb2[1].xxyz and the ambient
//   (dp4 cb2[11..13].vec4(N,1)) is likewise added after.
// That is why the factor is applied by scaling EACH light-s own diffuse before directionalLight()
// instead of touching the accumulators: every term inside (Oren-Nayar, thin rim, transmission,
// subsurface) plus the specular get it, and hemiAmbient() does not.
//
// !!!! COMPOSITION HAPPENS IN THE LIGHT ACCUMULATOR, NOT IN A SHADOW BUFFER. Do NOT combine the N
// occlusions into one scalar and multiply the whole accumulator by it: that darkens light arriving
// from directions that are NOT blocked, and it throws away the colour. With the per-light law, the
// shadow of a warm key on ground still lit by a cool fill comes out BLUISH, which is what happens.
//
// The engine has ONE directional, so N > 1 is a previewer tool, not a fidelity claim. isKeyLight
// (transmission + subsurface rolloff) stays the KEY only: casting and being-the-sun are different.
#define MAX_SHADOW_LIGHTS 4
uniform sampler2DArrayShadow texShadowMap;
uniform mat4 matShadowViewProj[MAX_SHADOW_LIGHTS];  // world -> light clip space, PER LAYER
uniform int  uShadowSlot[MAX_SHADOW_LIGHTS];        // rig light index -> layer, -1 = does not cast
uniform bool bShadows;            // false = the whole feature is inert (factor is never computed)
uniform float uShadowIntensity;   // 1 = the light goes fully dark in shadow (what the engine does)
uniform float uShadowNormalBias;  // WORLD units, SCALAR: see below
uniform float uShadowDepthBias[MAX_SHADOW_LIGHTS];  // normalized-depth units, PER LAYER
uniform int  uShadowPcfRadius;    // kernel is (2r+1)^2 taps; 0 = single tap, like the FO4 forward
uniform vec2 uShadowTexelUV;      // PCF tap STEP in UV: (softness / radius) / ALLOCATED size, SCALAR
uniform float uShadowUvScale[MAX_SHADOW_LIGHTS];  // logical / allocated, PER LAYER. 1.0 = full layer.
// WHY uShadowUvScale EXISTS. The wide GROUND maps size themselves from the projected footprint, which
// is altitude / tan(elevation) -- and orbiting moves every light-s elevation on every frame of the
// drag. With an adaptive texture size that meant Release + TexImage3D + CheckFramebufferStatus several
// times per gesture, i.e. two driver sync points inside the draw path. So the texture is ALLOCATED at
// a fixed size (a function of the config, never of where the user came from) and each layer is DRAWN
// into a smaller viewport anchored at the corner; this scales the lookup back. The area outside the
// viewport is not garbage: the pass clears the whole layer (glClear ignores the viewport) to depth
// 1.0 = nothing occludes, which is exactly what the white border returns.
// 1.0 is the character map-s value and multiplying by it is exact, so this is a no-op there.
// WHY THE BIAS AND THE UV SCALE ARE PER-LAYER BUT THE TEXEL STEP AND THE NORMAL BIAS ARE NOT. ShadowMapMath.Fit takes its extent from the
// BOUNDING SPHERE, which is rotation invariant, so Radius, TexelWorld and DepthRange come out
// IDENTICAL for every light over the same AABB -- only the ViewProj differs. That is what lets the
// layers share resolution, filtering and normal bias. The wide GROUND maps are the exception: their
// extent is the shadow FOOTPRINT, which depends on each light-s elevation, so their DepthRange (and
// hence the depth bias) is per layer."

    ''' <summary>El lookup. Toma la posicion de mundo EXPLICITA (no un varying) porque los tres
    ''' consumidores la obtienen distinto: los iluminados por <c>vWorldPos</c> del vertex shader, el suelo
    ''' construyendola en el propio VS del quad.</summary>
    Friend Const SharedLookupGlsl As String = "// 1 = fully lit, 0 = fully shadowed. Only ever called when bShadows is true and the light has a layer.
float shadowFactorAt(in vec3 worldPos, in vec3 worldNrm, in int layer)
{
	// NORMAL OFFSET is the primary anti-acne: displacing the sample point along the surface normal by
	// about one texel kills the self-shadowing of surfaces at a grazing angle to the light WITHOUT the
	// peter-panning a large constant depth bias causes. It arrives in world units already multiplied by
	// the texel's world size, so changing the map resolution does not require re-tuning the knob.
	vec4 lc = matShadowViewProj[layer] * vec4(worldPos + worldNrm * uShadowNormalBias, 1.0);
	vec3 p = lc.xyz / lc.w;
	p = p * 0.5 + 0.5;   // NDC -> [0,1]

	// Past the far plane of the map there is nothing that could occlude. The XY case is NOT handled
	// here on purpose: it is handled by the texture border (CLAMP_TO_BORDER with a white border), so a
	// PCF kernel straddling the edge still reads 'lit' without a per-tap branch.
	if (p.z > 1.0)
		return 1.0;

	float refDepth = p.z - uShadowDepthBias[layer];

	// Hardware PCF: the sampler is in COMPARE_REF_TO_TEXTURE, so every texture() call already returns
	// the bilinear average of four depth comparisons. The loop widens that to (2r+1)^2 taps. The tap
	// count is computed, not accumulated: it is known from the radius.
	// The layer may have been drawn into a smaller viewport than the texture it lives in: bring the
	// [0,1] of the logical map into the [0,scale] it actually occupies. See uShadowUvScale.
	vec2 base = p.xy * uShadowUvScale[layer];

	float sum = 0.0;
	for (int y = -uShadowPcfRadius; y <= uShadowPcfRadius; ++y)
	{
		for (int x = -uShadowPcfRadius; x <= uShadowPcfRadius; ++x)
		{
			sum += texture(texShadowMap, vec4(base + vec2(float(x), float(y)) * uShadowTexelUV, float(layer), refDepth));
		}
	}
	float side = float(2 * uShadowPcfRadius + 1);

	// uShadowIntensity < 1 lifts the shadow. NOT engine-faithful (the engine's factor is a hard 0/1);
	// it exists because a previewer has to let you read the texture on the dark side.
	return 1.0 - uShadowIntensity * (1.0 - sum / (side * side));
}"

    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE DE SOMBRA.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_FO4</c>, <c>Fragment_SSE</c> y <c>Fragment_ShadowDepth</c>: ahi lo
    ''' ve quien edita la logica, que es donde tiene que estar. Los tres se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado, y son DOS leyes (FO4 y SSE), no una.</b> Tocar una sin la otra rompe la silueta
    ''' de la sombra sin que nada lo reporte.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Fragment_ShadowDepth As String = "
#version 430

// ##################################################################################################
// ####  SHADOW SYNC CONTRACT -- THIS PASS MIRRORS THE ALPHA LAW OF BOTH RENDER SHADERS  ############
// ##################################################################################################
//
//   THE OTHER HALF OF THIS CONTRACT LIVES IN:
//   ==>  Shader_Class_Fo4.Fragment_FO4   (Fallout 4 lit pass)
//   ==>  Shader_Class_SSE.Fragment_SSE   (Skyrim SE lit pass)
//   Each of those carries the same contract pointing back here. CHANGING EITHER OF THEM WITHOUT
//   CHANGING THIS FILE IS A BUG, even if everything still compiles and every gate stays green.
//
// WHAT THIS PASS IS. It writes the shadow map, so it decides WHICH FRAGMENT EXISTS. It does not
// shade anything: the ONLY thing it computes is whether a fragment survives, and that has to be
// decided with EXACTLY the quantity the lit pass tests. When the two disagree, the shadow
// silhouette stops matching the object that is drawn -- shadow for geometry discarded on screen,
// or no shadow for geometry that is visible.
//
// !!!! ONE FRAGMENT, TWO GAMES. Shader_Class builds TWO programs out of this single fragment, one
// with Vertex_FO4 and one with Vertex_SSE, and the alpha law is NOT the same in both. The uniform
// bLeySse selects which one runs. Measured divergences:
//   1. effect shader (.bgem) vertex alpha : FO4 pow(vColor.a, 2.2) -- SSE LINEAR
//   2. lighting shader (.bgsm) alpha scalar: SSE multiplies BEFORE the test -- FO4 after
//   3. falloff on the alpha WITH palette recolor: FO4 folds it into the palette V *and* multiplies
//      again (falloff^2, on purpose); SSE folds it in and does NOT multiply again.
//   4. (WITHDRAWN) the falloff curve is the same in both games (FO4 VS rec0440 = SSE VS t00010153:
//      div_sat, t*t*(3-2t), no clamp on the opacities).
// Writing one law for both is a bug that looks like a rounding problem. It already happened THREE times.
//
// WHAT MUST BE MIRRORED, in both directions:
//   * the alpha test quantity and its threshold
//   * the vertex alpha (linear vs gamma) and the predicate that gates it
//   * the material alpha scalar, and WHERE it is applied
//   * greyscale-to-palette on alpha (it REPLACES the alpha)
//   * falloff (it MULTIPLIES the alpha, and is folded into the palette V)
//   * the zap discard, the skinning, and the UV transform
//
// NOT MIRRORED, on purpose: everything about SHADING (lighting, tint, specular, emissive, cubemap).
// This pass has no colour output. Only what decides EXISTENCE belongs here.
//
// ALSO: this GLSL lives inside a VB Const String. PURE ASCII, and NO double quote -- a quote closes
// the VB literal. The glsl-ascii gate in Tools/ParityGate enforces both.
// ##################################################################################################

uniform sampler2D texDiffuse;
uniform vec2 uvOffset;
uniform vec2 uvScale;
uniform float alphaThreshold;
uniform bool bAlphaTest;
uniform bool bAlphaBlend;      // the material is drawn with blending (no cutout threshold to use)
uniform float uMaterialAlpha;  // the material Alpha scalar, the one the lit pass multiplies in for the blend
uniform bool bShowTexture;
uniform bool bApplyZap;
// !! ESTE FRAGMENT ES UNO SOLO PARA LOS DOS JUEGOS (Shader_Class arma dos programas, con Vertex_FO4 y
// con Vertex_SSE, sobre ESTE mismo fragment), y la ley del effect shader NO es la misma:
// EFFECT SHADER (.bgem):
//   FO4 : effAlpha = diffuse.a * pow(vColor.a, 2.2) * BaseColor.a   (gamma)
//   SSE : color.a  = BaseColor.a * vColor.a * effTexAlpha           (LINEAL)
// LIGHTING SHADER (.bgsm) -- y esta rama es la del 95 % de los materiales:
//   FO4 : testea diffuse.a * vColor.a  y multiplica el escalar Alpha DESPUES (solo para el blend)
//   SSE : multiplica el escalar Alpha ANTES del test (cb2[3].z), o sea que ENTRA al lado izquierdo
// El llamador manda cual juego corre. Poner la ley de FO4 para los dos ALEJABA la silueta en SSE en vez
// de acercarla, en las dos ramas.
uniform bool bLeySse;
// El material es un EFFECT SHADER (.bgem). Su ley de alpha en el pase iluminado NO es la del lighting
// shader, asi que el test de esta pasada tiene que cambiar con el. Ver el bloque de `aTex`.
uniform bool bIsEffectShader;

in vec4 vColor;
in vec2 vUV;
flat in int ZappedVert;
// El pase de profundidad REUSA los vertex shaders completos (Vertex_FO4 / Vertex_SSE), asi que estos
// varyings ya vienen calculados y no hay que agregar nada al VS. Se usan para el FALLOFF del effect
// shader, que depende del angulo entre la normal y la direccion de vista.
in mat3 mv_tbn;
in mat3 v_msnMatrix;

// --- effect shader: los dos modificadores del alpha que faltaban ---------------------------------
uniform bool bModelSpace;
uniform bool bShowVertexColor;
uniform bool bEffectGreyscaleAlpha;   // el alpha se REEMPLAZA por un lookup de paleta
uniform bool bEffectFalloff;          // el alpha se MULTIPLICA por el factor angular
uniform bool bEffectFalloffColor;     // (solo importa porque tambien enciende el calculo del factor)
uniform vec4 effectFalloffParams;     // x=start y=stop z=startOpacity w=stopOpacity
uniform sampler2D texGreyscale;

// ORDERED BAYER 4x4, pre-normalised to (v + 0.5) / 16 so the thresholds sit at the centre of each of the
// 16 buckets. Used for the STOCHASTIC / SCREEN-DOOR path below.
const float bayer4[16] = float[16](
	 0.03125, 0.53125, 0.15625, 0.65625,
	 0.78125, 0.28125, 0.90625, 0.40625,
	 0.21875, 0.71875, 0.09375, 0.59375,
	 0.96875, 0.46875, 0.84375, 0.34375);

void main(void)
{
	if (bApplyZap && ZappedVert == 1)
		discard;

	if (bAlphaTest || bAlphaBlend)
	{
		vec2 uv = vUV * uvScale + uvOffset;
		// !!!! EL FACTOR DE ALPHA DE LA TEXTURA CUANDO LA TEXTURA ESTA APAGADA, Y NO ES EL MISMO PARA LAS
		// DOS FAMILIAS. El pase iluminado NO gatea su alpha-test con bShowTexture (su `if (bAlphaTest)`
		// es hermano del `if (bShowTexture)`, no hijo), asi que con el toggle apagado SIGUE descartando,
		// y lo hace con dos cantidades distintas:
		//   .bgsm : `color.a *= baseMap.a` vive DENTRO del if, o sea que no se aplica => factor 1.0.
		//   .bgem : el bloque BGEM es HERMANO del if y lee `baseMap` igual, que quedo en su init
		//           vec4(0.0) => factor 0.0, y la shape entera desaparece de pantalla.
		// Este pase gateaba el bloque completo por bShowTexture y entonces no descartaba NADA: la card
		// entera proyectaba sombra de algo que no se dibuja. Replicar los dos factores es lo que vuelve a
		// juntar las dos siluetas.
		float dTexA = bShowTexture ? texture(texDiffuse, uv).a : (bIsEffectShader ? 0.0 : 1.0);
		// SAME left-hand side as the lit pass -- and the lit pass has TWO laws, one per shader family.
		// Any divergence here and the shadow silhouette stops matching the shape that is drawn.
		//
		//  - LIGHTING (.bgsm): tests `diffuse.a * vColor.a`, and multiplies the material Alpha scalar in
		//    AFTERWARDS, only for the blend. So the scalar is NOT part of the left-hand side.
		//  - EFFECT (.bgem): tests `effAlpha = diffuse.a * pow(vColor.a, 2.2) * BaseColor.a`, and does NOT
		//    apply the scalar again afterwards (`if (!bIsEffectShader) fragColor.a *= alpha;`). So here the
		//    scalar IS part of the left-hand side, the vertex alpha carries GAMMA 2.2, and the gate is the
		//    vertex-COLOR one, not the vertex-alpha one (the caller already sends it that way).
		//
		// !! Sin esto, con un BGEM de Alpha = 0.5 y AlphaTestRef = 128 el iluminado conservaba solo
		// diffuse.a >= 1.0 y esta pasada conservaba diffuse.a >= 0.5: la sombra dibujaba geometria que en
		// pantalla estaba descartada.
		float aTex;
		if (bIsEffectShader)
		{
			// * FALLOFF. El pase iluminado lo evalua contra la CAMARA; aca la camara ES LA LUZ, y eso es
			// lo correcto: lo que decide cuanta luz BLOQUEA un material es el angulo con el que la LUZ lo
			// atraviesa, no con el que lo mira el usuario. Un material que se desvanece en incidencia
			// rasante deja pasar luz, y su sombra tiene que aclararse igual.
			// Sin esto, un .bgem con falloff proyectaba la card ENTERA a opacidad plena mientras en
			// pantalla su borde se desvanecia -- el mismo sintoma de placa negra que el dither vino a
			// matar, por otra puerta.
			float effFalloff = 1.0;
			if (bEffectFalloff || bEffectFalloffColor)
			{
				// !!!! LA DIRECCION DE VISTA DE ESTE PASE ES UNA CONSTANTE, NO `viewDirRaw`.
				// Lo escribi con `normalize(-vPos)` razonando que 'la camara es la luz'. Es falso: la
				// view de la luz se ancla al ORIGEN DEL MUNDO (ShadowMapMath.Fit) y la proyeccion es
				// ORTOGRAFICA, asi que `-vPos` es una direccion de tipo perspectiva sobre una camara sin
				// posicion fisica. Un fragmento a 120 unidades de altura daba una direccion PURAMENTE
				// LATERAL, y una card mirando de frente a la luz obtenia NdotV = 0 en vez de 1: el
				// falloff tomaba StartOpacity justo donde el material bloquea MAS luz.
				// En un ortho todos los rayos son paralelos: en espacio de vista de la luz la direccion
				// es (0,0,1) exacta, o sea que NdotV es |nGeo.z| y no hay nada que normalizar.
				// !! DIVERGENCIA DECLARADA: `nGeo` es la normal GEOMETRICA, y los dos pases iluminados
				// evaluan el falloff contra la normal YA PERTURBADA por el normal map. Este programa no
				// declara texNormal ni lo bindea, asi que no puede replicarla: en una card con normal
				// map fuerte el NdotV del iluminado varia por pixel y el de la sombra es constante por
				// triangulo. Se acepta -- traer el normal map al pase de profundidad es un sampler y un
				// bind por malla para afinar el BORDE de una sombra translucida.
				vec3 nGeo = bModelSpace ? normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0))
				                        : normalize(mv_tbn * vec3(0.0, 0.0, 0.5));
				float NdotV = abs(nGeo.z);
				// The curve of BOTH engines' vertex shaders (FO4 rec0440, SSE t00010153): div_sat, t*t*(3-2t),
				// t*(stopOp - startOp) + startOp, no clamp on the opacities.
				float ft = clamp((NdotV - effectFalloffParams.x) / (effectFalloffParams.y - effectFalloffParams.x), 0.0, 1.0);
				effFalloff = (ft * ft * (-2.0 * ft + 3.0)) * (effectFalloffParams.w - effectFalloffParams.z) + effectFalloffParams.z;
			}
			// FO4 aplica gamma al alpha de vertice; SSE lo usa lineal.
			float vcA = bLeySse ? vColor.a : pow(max(vColor.a, 0.0), 2.2);
			aTex = dTexA * vcA * uMaterialAlpha;

			// * GREYSCALE-TO-PALETTE (ALPHA). No modifica el alpha: lo REEMPLAZA por una fila de la
			// paleta. Sin esto, un .bgem con este flag dibujaba opaco y su sombra se disolvia en el
			// dither (o al reves), porque las dos pasadas miraban numeros sin relacion.
			if (bEffectGreyscaleAlpha)
			{
				float vcRecolorA = bShowVertexColor ? max(vColor.a, 0.0) : 1.0;
				if (bLeySse)
				{
					// SSE: el alpha de textura se descarta (effTexAlpha = 1) y el lookup toma
					// U = diffuse.a, V = el alpha acumulado hasta aca.
					// !! LA V LLEVA EL FALLOFF. Fragment_SSE hace `color.a *= effFalloff` ANTES del
					// lookup (su propio comentario: Falloff calculated early, needed for greyscale), asi que
					// omitirlo aca muestreaba OTRA FILA de la paleta: con effFalloff = 0,104 el
					// iluminado lee V = 0,104 y la sombra leia V = 1,0.
					// !! El falloff entra en la V SOLO si bEffectFalloff. Fragment_SSE lo aplica dentro
					// de `if (bEffectFalloff)`, NO dentro del `if (bEffectFalloff || bEffectFalloffColor)`
					// que rodea el calculo: con FalloffColor prendido y Falloff apagado el iluminado lee
					// V = 1,0 y la sombra leia V = effFalloff, o sea otra fila de la paleta.
					float vSse = uMaterialAlpha * vColor.a;
					if (bEffectFalloff) vSse *= effFalloff;
					aTex = texture(texGreyscale, vec2(clamp(dTexA, 0.0, 1.0),
					                                  clamp(vSse, 0.0, 1.0))).a;
				}
				else
				{
					// FO4: U = diffuse.a crudo; V = pow(BaseColor.a, 1/2.2) * vColor.a CRUDO * falloff.
					float palUa = dTexA;
					float palVa = pow(max(uMaterialAlpha, 0.0), 1.0 / 2.2) * vcRecolorA * effFalloff;
					aTex = texture(texGreyscale, vec2(clamp(palUa, 0.0, 1.0), clamp(palVa, 0.0, 1.0))).a;
				}
			}

			// !!!! EL POST-MULTIPLY ES DE FO4, Y EN SSE-CON-RECOLOR NO EXISTE. Las dos leyes:
			//   FO4 : mete el falloff en la V de la paleta Y ADEMAS lo multiplica despues, sin condicion
			//         (`if (bEffectFalloff) effAlpha *= effFalloff;`) -- falloff^2 a proposito, es lo que
			//         hace el motor. Y sin recolor tambien multiplica. => post-multiply SIEMPRE.
			//   SSE : el lookup REEMPLAZA color.a y NO hay ningun multiply posterior en toda la rama
			//         BGEM. Sin recolor si multiplica (antes del lookup que no ocurre).
			// => el post-multiply va siempre EXCEPTO en SSE cuando hubo recolor de paleta. Sin este
			// gate, un .bgem de SSE con greyscale+falloff proyectaba con falloff^2: se dibujaba opaco y
			// no casteaba ninguna sombra.
			// !! Antes tenia el gate al reves (`&& !bEffectGreyscaleAlpha`, copiado de la nota del canal
			// RGB), que dejaba a FO4 sin aplicarlo ni una vez. Las dos veces el error fue transplantar
			// una nota de una ley a la otra: son DOS, no una.
			if (bEffectFalloff && !(bLeySse && bEffectGreyscaleAlpha)) aTex *= effFalloff;
		}
		else
		{
			// !! EN SSE EL ESCALAR ENTRA AL TEST; EN FO4 NO. Lo dice el propio Fragment_SSE: SSE
			// multiplica el alpha del material ANTES del test (cb2[3].z), y ahi difiere de FO4, que
			// testea sin el. Con Alpha = 0,5, ref = 128 y diffuse.a = 0,9, el iluminado de SSE
			// descarta (0,45 < 0,502) y esta pasada conservaba (0,9 >= 0,502): la sombra proyectaba
			// geometria que en pantalla no esta. Es el mismo defecto que se cerro para el .bgem, en la
			// rama que usan casi todos los materiales.
			aTex = dTexA * vColor.a * (bLeySse ? uMaterialAlpha : 1.0);
		}

		if (bAlphaTest)
		{
			// CUTOUT: a hard threshold, strict `<` so equality is KEPT.
			if (aTex < alphaThreshold)
				discard;
		}

		// !!!! NO ES `else`, Y ESO ERA UN DEFECTO. Los dos flags son INDEPENDIENTES en el pase iluminado:
		// con AlphaTest y AlphaBlend a la vez, el iluminado descarta por el umbral Y DESPUES blendea lo
		// que sobrevivio (Render.vb: la rama `ElseIf hasAlphaBlend` habilita GL_BLEND y va ANTES que la de
		// cutout). Con el `else`, el superviviente entraba al mapa OPACO y nunca veia el dither.
		// Y `HasAlphaBlend` es `AlphaBlendEnabled OrElse Alpha < 1`: TODO cutout con Alpha < 1 caia ahi.
		// Sintoma: una malla dibujada al 30 % de opacidad proyectaba la sombra de una malla solida.
		if (bAlphaBlend)
		{
			// TRANSLUCENT: there is no cutout threshold to use, so a hard test would be an invention --
			// picking 0.5 turns a 20%-opaque veil into either a solid slab or nothing at all. That was the
			// old behaviour by omission: an alpha-blended material had no discard AT ALL here and cast the
			// full quad. Fine hair came out as a black plate.
			//
			// STOCHASTIC (screen-door) instead: keep the fragment with probability = its opacity, using an
			// ordered pattern. One fragment is still binary, but the PCF kernel averages the neighbourhood
			// and the result reads as a shadow proportional to the opacity. This is the reason it fits HERE
			// specifically: the PCF is already there and already measured free (1 to 49 taps costs +0.10 ms),
			// so the machinery that turns the noise into a grey level is paid for.
			//
			// The pattern is indexed by gl_FragCoord OF THE DEPTH PASS, i.e. by the SHADOW MAP TEXEL, not by
			// anything in screen space -- a screen-space index would make the pattern crawl over the surface
			// while orbiting.
			// !! THAT ONLY HOLDS WITH THE LIGHTS FIXED TO THE WORLD. The map's frame comes from
			// ShadowMapMath.Fit(_frameLights.KeyDir, ...), so with Setting_LightsFollowCamera ON -- which is
			// the CURRENT DEFAULT -- KeyDir rotates with the camera, the texel grid rotates with it, and the
			// Bayer pattern does sweep across the surface while orbiting. Fine hair will shimmer.
			// Two features from the same batch that step on each other. There is no anchor that survives a
			// rotating light: the texel grid IS the light's frame.
			//
			// !!! DECIDED 2026-08-12 BY THE USER: option (c), ACCEPT IT. Do not 'fix' this without asking.
			// The alternatives were (a) index the pattern by a stable object-space quantity instead of the
			// map texel, and (b) blue noise instead of ordered Bayer (shimmers less, reads as grain rather
			// than banding). Both change how the shadow of a translucent material LOOKS, and that is a
			// visual-quality call the user owns -- it was put to them and they chose to keep the current
			// look. The shimmer only shows on fine alpha-blended hair while actively dragging the camera,
			// and only with CastShadows left on (vanilla ships those materials with it off).
			// If it ever does become a problem, (a) and (b) are still the two ways out.
			// Smaller, same family: TexelWorld = 2*Radius/mapSize and Radius comes from the scene AABB, which
			// moves every frame during animation; and GroundMapSize snaps between 512/1024/2048, so the
			// ground map's pattern re-indexes when it changes step.
			// El escalar ya entro en `aTex` para el effect shader (los dos juegos) y para el lighting
			// shader de SSE; volver a multiplicarlo lo aplicaria AL CUADRADO y la sombra saldria mas
			// transparente de lo que se dibuja.
			float aBlend = (bIsEffectShader || bLeySse) ? aTex : (aTex * uMaterialAlpha);
			ivec2 p = ivec2(gl_FragCoord.xy) & 3;
			if (aBlend < bayer4[p.y * 4 + p.x])
				discard;
		}
	}
}
"
End Module

''' <summary>Receptor de sombra del SUELO ("shadow catcher"): un quad en el plano del piso que NO pinta
''' superficie, solo OSCURECE lo que ya haya detras segun la sombra que le llega.
'''
''' <para>EL BLEND ES MULTIPLICATIVO (<c>ZERO, SRC_COLOR</c> ⇒ <c>resultado = destino x fuente</c>), no
''' alpha sobre negro. La diferencia importa: el previewer no tiene suelo real, asi que detras del quad
''' puede haber el color de fondo, la grilla, o nada — pintar "negro con alpha" tine el fondo de un color
''' que el usuario eligio. Multiplicar lo OSCURECE sea cual sea, que es lo que hace una sombra.</para>
'''
''' <para>El quad se desvanece en el borde (<c>smoothstep</c> sobre el radio) para que no se lea como una
''' chapa rectangular apoyada en el aire.</para></summary>
Friend Module GroundShadowShaderSource

    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE DE SOMBRA.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_FO4</c>, <c>Fragment_SSE</c> y <c>Fragment_ShadowDepth</c>: ahi lo
    ''' ve quien edita la logica, que es donde tiene que estar. Los tres se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado, y son DOS leyes (FO4 y SSE), no una.</b> Tocar una sin la otra rompe la silueta
    ''' de la sombra sin que nada lo reporte.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Vertex_Ground As String = "
#version 430
layout(location = 0) in vec2 aCorner;   // unit quad, [-1..1] on both axes

uniform mat4 matViewProj;      // camera view * projection (same convention as the rest of the render)
uniform vec3 uGroundCenter;    // world-space centre of the catcher
uniform vec2 uGroundHalf;      // world-space half-extents, per axis

out vec3 gWorldPos;
out vec2 gLocal;

void main(void)
{
	gLocal = aCorner;
	gWorldPos = uGroundCenter + vec3(aCorner * uGroundHalf, 0.0);
	gl_Position = matViewProj * vec4(gWorldPos, 1.0);
}
"

    Friend Const Fragment_Ground As String = "
#version 430

" & ShadowDepthShaderSource.SharedUniformsGlsl & "

in vec3 gWorldPos;
in vec2 gLocal;

// What actually reaches a +Z plane, per channel, in LINEAR radiance -- derived from the live rig on
// the CPU, never a hand-picked constant (see PreviewModel.SubirAporteDelSuelo). uGroundTotal is everything
// (ambient sky + every light), uGroundContrib[i] is what the light on LAYER i puts on that plane.
// Occluding layer i subtracts its contribution and nothing else. Without this the first version
// multiplied by 1 - a and came out PURE BLACK at Intensity 1, which no real shadow is.
uniform vec3 uGroundTotal;
uniform vec3 uGroundContrib[MAX_SHADOW_LIGHTS];
uniform int  uGroundCount;   // how many wide layers exist; 0 = nothing to draw
// The game's display encode of a linear ratio: FO4 1/2.2 (GammaCorrectLUT i3648), SSE 1 (raw pipeline).
uniform float uDisplayExponent;
// true: HDR target of the FO4 post (PostProcess.vb). The LINEAR ratio multiplies the linear radiance
// (attachment 0), the coverage is left as it is (attachment 1, times 1) and the DISPLAY-space factor
// multiplies the background-shadow target (attachment 2), which the post applies to the UI background.
// false: display target, the display-space factor multiplies the framebuffer (see below).
uniform bool bHdrTarget;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec4 coverageOut;
layout(location = 2) out vec4 backgroundShadowOut;

" & ShadowDepthShaderSource.SharedLookupGlsl & "

void main(void)
{
	if (!bShadows)
		discard;

	// Fade so the catcher has no hard edge. PER AXIS (Chebyshev), not radial: the footprint is a
	// rectangle, and a circular fade over a rectangle cuts the corners of what it must cover. With
	// uGroundHalf = footprint * GROUND_MARGIN, the whole footprint sits inside |gLocal| <= 1/MARGIN,
	// so the fade band only ever covers ground that has no shadow on it.
	// GROUND_FADE_START must stay equal to 1 / ShadowMapMath.GroundQuadMargin -- gate `ground-catcher`.
	#define GROUND_FADE_START 0.952380952
	float edge = 1.0 - smoothstep(GROUND_FADE_START, 1.0, max(abs(gLocal.x), abs(gLocal.y)));
	if (edge <= 0.0)
		discard;

	// The plane-s normal is world +Z, so the normal-offset of the lookup pushes the sample straight up.
	// EVERY casting light gets its own lookup and subtracts its own contribution: that is the same
	// per-light composition law the lit passes use, evaluated for one fixed normal.
	vec3 lost = vec3(0.0);
	for (int i = 0; i < uGroundCount; ++i)
	{
		// A LAYER WITH NO CONTRIBUTION IS SKIPPED, NOT MULTIPLIED BY ZERO -- AND THIS BRANCH IS THE ONLY
		// GUARD THERE IS. A reserved layer whose light is below the catcher-s minimum elevation this frame
		// still exists and still gets sampled by this loop; its matShadowViewProj is the zero matrix -- it
		// comes from the `_groundFits(capa) = Nothing` INSIDE the per-layer draw loop of
		// PreviewModel.RenderShadowPass, which is the only place that leaves a zeroed layer while
		// uGroundCount is still > 0. (Not from OlvidarEncuadresDeSuelo: that one always ends with
		// uGroundCount = 0, so this loop does not even run.) The lookup divides by w = 0 and returns NaN. And
		// 0.0 * NaN is NaN, not 0: without this continue, ONE such layer turns the whole quad black.
		// There used to be a CPU-side glClear of those layers as well; it was removed because it guarded
		// nothing -- the coordinates come out NaN whatever depth the layer holds, and the cleared set was
		// exactly the skipped set. Do not re-add it: fix the skip instead.
		// The branch is uniform per draw, so it also saves that layer-s PCF kernel.
		if (dot(uGroundContrib[i], vec3(1.0)) <= 0.0)
			continue;
		lost += uGroundContrib[i] * (1.0 - shadowFactorAt(gWorldPos, vec3(0.0, 0.0, 1.0), i));
	}

	vec3 lin = clamp((uGroundTotal - lost) / max(uGroundTotal, vec3(1e-4)), 0.0, 1.0);
	// A CHANNEL WITH NO LIGHT AT ALL CANNOT BE SHADOWED: it stays at 1 = no darkening. Without this the
	// max() above turns a fully dark rig (total = 0) into (0 - 0) / 1e-4 = 0, i.e. a BLACK ground -- the
	// exact case the previous SafeRatio() handled by returning 1. step(edge, x) is 1 when x >= edge.
	lin = mix(vec3(1.0), lin, step(vec3(1e-4), uGroundTotal));

	// ENCODED TO DISPLAY BEFORE LEAVING. The ratio above is RADIANCE, but this quad multiplies against
	// the framebuffer, which holds values that are already encoded: both lit fragments end in
	// pow(color, 1/2.2) and nobody turns GL_FRAMEBUFFER_SRGB on. Multiplying a linear ratio into an
	// encoded destination made the GROUND shadow darker than the BODY shadow in the same frame (Studio:
	// ratio 0.815, correct on-screen factor 0.911; over a background of 128 it came out 104 instead of
	// 117). Exact, not approximate, for the case that actually happens: the quad depth-tests, so where
	// the character is in front it does not draw, and its destination is the clear colour or the floor
	// grid -- neither went through the tonemap.
	vec3 fac = pow(lin, vec3(uDisplayExponent));
	if (all(greaterThan(fac, vec3(0.998))))
		discard;

	// Multiplicative blend: the caller sets ZERO / SRC_COLOR, so this value MULTIPLIES the framebuffer.
	// edge = 0 leaves white = the background untouched.
	if (bHdrTarget)
	{
		fragColor = vec4(mix(vec3(1.0), lin, edge), 1.0);
		coverageOut = vec4(1.0);
		backgroundShadowOut = vec4(mix(vec3(1.0), fac, edge), 1.0);
		return;
	}
	fragColor = vec4(mix(vec3(1.0), fac, edge), 1.0);
}
"
End Module

''' <summary>Programa del receptor de suelo.</summary>
Public Class Ground_Shadow_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(GroundShadowShaderSource.Vertex_Ground, GroundShadowShaderSource.Fragment_Ground)
    End Sub
End Class

''' <summary>Programa de profundidad para assets FO4: Vertex_FO4 + el fragment de sombra.</summary>
Public Class Shadow_Depth_Shader_Fo4
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(Shader_Class_Fo4.Vertex_FO4, ShadowDepthShaderSource.Fragment_ShadowDepth)
    End Sub
End Class

''' <summary>Programa de profundidad para assets SSE: Vertex_SSE + el MISMO fragment de sombra.</summary>
Public Class Shadow_Depth_Shader_SSE
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(Shader_Class_SSE.Vertex_SSE, ShadowDepthShaderSource.Fragment_ShadowDepth)
    End Sub
End Class

''' <summary>Las vistas de depuracion del fragment shader. Cada valor es el numero que el bloque
''' <c>DEBUG MODE</c> compara adentro del GLSL, en los DOS juegos: <c>Fragment_FO4</c> (linea del
''' <c>if (DebugMode &gt; 0.0)</c>) y <c>Fragment_SSE</c>.
''' <para>⛔ LOS NUMEROS SON EL CONTRATO CON EL GLSL. El uniform <c>DebugMode</c> es un <c>float</c> y el
''' shader decide con <c>abs(DebugMode - N) &lt; 0.5</c>: renumerar o reordenar este enum cambia lo que
''' dibuja cada opcion, sin error de compilacion y sin que nada falle. Agregar una vista nueva son DOS
''' ramas de GLSL —una en cada fragment— mas el item del combo en <c>LightRigForm</c>.</para></summary>
Public Enum ShaderDebugView
    ''' <summary>Render normal. Es el UNICO valor con el que el bloque de debug no corre: el GLSL entra
    ''' con <c>if (DebugMode &gt; 0.0)</c>.</summary>
    None = 0
    ''' <summary>La normal interpolada, en espacio de vista, mapeada de -1..1 a RGB.</summary>
    Normals = 1
    ''' <summary>La tangente, en espacio de vista, mapeada a RGB.</summary>
    Tangents = 2
    ''' <summary>La bitangente, en espacio de vista, mapeada a RGB.</summary>
    Bitangents = 3
    ''' <summary>Rojo/verde = cuanto difiere la TBN del NIF de una TBN re-ortogonalizada por
    ''' Gram-Schmidt; azul = el signo de la handedness.</summary>
    TbnError = 4
    ''' <summary>La textura de normales CRUDA, tal cual se muestrea.</summary>
    NormalMap = 5
End Enum

Public MustInherit Class Shader_Base_Class
    Implements IDisposable

    Private disposedValue As Boolean

    Private program As Integer

    ''' <summary>El id del programa GL. Friend y de SOLO LECTURA: lo consume el arnes Tools/ShadowGate
    ''' para verificar que el GLSL LINKEO de verdad — un shader que no compila deja esto en 0 y el render
    ''' se degrada en silencio (el error va por Logger, apagado en Release). No cambia la API distribuida.</summary>
    Friend ReadOnly Property ProgramId As Integer
        Get
            Return program
        End Get
    End Property

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

    ''' <summary>A compute program (one stage), with the same compile/link checks and uniform API.</summary>
    Protected Sub New(computeShaderSource As String)
        Dim computeShader = CompileShader(ShaderType.ComputeShader, computeShaderSource)
        program = GL.CreateProgram()
        GL.AttachShader(program, computeShader)
        GL.LinkProgram(program)

        Dim linkStatus As Integer
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, linkStatus)
        If linkStatus <> CInt(All.True) Then
            Dim linkInfo = GL.GetProgramInfoLog(program)
            Throw New Exception($"Shader program link error: {linkInfo}")
        End If

        GL.DetachShader(program, computeShader)
        GL.DeleteShader(computeShader)
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

    ''' <summary>LA VISTA DE DEPURACION DEL SHADER: UNA SOLA PARA TODO EL PROCESO Y PARA LOS DOS JUEGOS.
    ''' La sube <c>PreviewControl.ApplyMaterial</c> al uniform <c>DebugMode</c> y la elige el usuario en el
    ''' combo "Shader debug view" de la pestana Rendering de <c>LightRigForm</c>.
    ''' <para>⛔ <c>Shared</c> A PROPOSITO, Y ESO ARREGLA UN DEFECTO. Antes era un campo de INSTANCIA
    ''' (<c>Public Debugmode As Integer</c>) y el unico que lo escribia —el atajo F1..F5 de Wardrobe
    ''' Manager, y solo bajo <c>#If DEBUG</c>— lo hacia sobre <c>CurrentShader</c>, o sea sobre el programa
    ''' del JUEGO ACTIVO. Como <c>Shader_Class_Fo4</c> y <c>Shader_Class_SSE</c> son dos instancias
    ''' distintas, cada una con su propio campo, cambiar de juego dejaba la vista atras sin decir nada.
    ''' Compartida, la eleccion vale para los dos, que es lo que el combo promete.</para>
    ''' <para>NO SE PERSISTE, y es deliberado: no vive en <c>Config_App</c> ni en ningun archivo. Arranca
    ''' en <see cref="ShaderDebugView.None"/> en cada corrida y el boton "Reset rendering to defaults" la
    ''' devuelve a None. Es una ayuda de inspeccion, no un ajuste del usuario.</para>
    ''' <para>ALCANCE, MEDIDO Y NO SUPUESTO. El unico que lee esto es <c>ApplyMaterial</c>, o sea el pase
    ''' ILUMINADO del preview, y <c>RenderScene</c> —su unico camino de entrada— no se llama desde ningun
    ''' camino de bake. EL BAKE NO PASA POR ACA: <c>BakeAllRunner</c> crea un <c>PreviewControl</c> solo
    ''' para tener contexto GL, le PARA el <c>RenderTimer</c> ("no queremos renders del control") y compone
    ''' contra FBOs con los shaders propios de <c>FaceTintCompositor</c>, que no tienen este uniform. Lo
    ''' que SI la ve es <c>CaptureBitmap</c> —los dos "Save render screenshot" y la captura opcional del
    ''' wizard de FOMOD—, y eso es correcto: las tres capturan lo que la pantalla esta mostrando.</para>
    ''' </summary>
    Public Shared Property DebugView As ShaderDebugView = ShaderDebugView.None

    ''' <summary>Luma Rec.709 de un color de luz. UNA sola definicion: la comparten el auto-exposure del
    ''' piso (<c>floorExposure</c>) y la direccionalidad del rig que consume el fondo. Tener dos copias de
    ''' estos tres pesos es como empiezan a divergir dos partes del mismo cuadro.</summary>
    Public Shared Function Luma(v As Vector3) As Single
        Return v.X * 0.2126F + v.Y * 0.7152F + v.Z * 0.0722F
    End Function

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
    ''' <summary>A material/rig colour as the GAME's pipeline consumes it: FO4 powf(2.2) (SetupMaterial,
    ''' DAT_142475358 = 2.2), SSE raw (its setup has no gamma constant).</summary>
    Public Shared Function MaterialColor(color As Color, isSSE As Boolean) As Vector3
        Return If(isSSE, Color_to_Vector(color), Color_to_Vector_Linear(color))
    End Function

    ''' <summary>THE GAME'S DISPLAY ENCODE as an exponent, for the ground shadow and the SSE post: FO4 1/2.2 (the value
    ''' the ground shadow already used), SSE 1 (fGamma = 1.0, cb12[42].x of PS 12545). ⛔ NOT the only owner:
    ''' Fragment_PostFo4 writes i3648's DXBC literal 0.454545 itself (PostProcess.vb:318), which differs from 1/2.2F in
    ''' the 7th decimal; that shader is read verbatim by Tools/FabricGen and is not touched.</summary>
    Public Shared Function DisplayExponent(isSSE As Boolean) As Single
        Return If(isSSE, 1.0F, 1.0F / 2.2F)
    End Function

    ''' <summary>FROM THE GAME'S SCENE SPACE TO LINEAR, as an exponent: FO4 1 (the scene is linear: MaterialColor uses
    ''' Color_to_Vector_Linear), SSE 2.2 (the scene is raw: MaterialColor uses Color_to_Vector). It feeds the 2.3.8 tail of
    ''' the frames without post (LegacyDisplaySource.legacyLitDisplay): fragments and floor read it from here.</summary>
    Public Shared Function SceneToLinearExponent(isSSE As Boolean) As Single
        Return If(isSSE, 2.2F, 1.0F)
    End Function

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

    ''' <summary>Sube un array de matrices de una sola llamada.
    ''' <para>RESUELVE LA LOCATION DEL ELEMENTO [0] Y SUBE N DE CORRIDO. La alternativa —armar
    ''' <c>"nombre[" &amp; i &amp; "]"</c> por elemento— aloca strings en el camino de dibujo, que es
    ''' justo lo que este archivo evita en todos lados. GLSL garantiza locations consecutivas para los
    ''' elementos de un array de uniforms, asi que una sola llamada con count=N es correcta.</para>
    ''' <para>El caller pasa un array REUTILIZADO de floats; no se aloca nada aca.</para>
    ''' <para>⛔ EL NOMBRE LLEGA YA CON EL <c>[0]</c>. Recibir <c>"matShadowViewProj"</c> y hacer
    ''' <c>name &amp; "[0]"</c> adentro aloca una String POR LLAMADA en el camino de dibujo, que es
    ''' exactamente lo que este metodo existe para evitar. Con el literal completo desde el caller, la
    ''' String es una constante internada.</para></summary>
    Public Sub SetMatrix4Array(nombreElemento0 As String, valores As Single(), count As Integer)
        If count <= 0 Then Exit Sub
        Dim loc As Integer = GetUniformLocationCached(nombreElemento0)
        If loc <> -1 Then
            GL.UniformMatrix4(loc, count, False, valores)
        End If
    End Sub

    Public Sub SetFloatArray(nombreElemento0 As String, valores As Single(), count As Integer)
        If count <= 0 Then Exit Sub
        Dim loc As Integer = GetUniformLocationCached(nombreElemento0)
        If loc <> -1 Then
            GL.Uniform1(loc, count, valores)
        End If
    End Sub

    Public Sub SetIntArray(nombreElemento0 As String, valores As Integer(), count As Integer)
        If count <= 0 Then Exit Sub
        Dim loc As Integer = GetUniformLocationCached(nombreElemento0)
        If loc <> -1 Then
            GL.Uniform1(loc, count, valores)
        End If
    End Sub

    ''' <summary>Idem para vec3 (el array viene aplanado, 3 floats por elemento).</summary>
    Public Sub SetVector3Array(nombreElemento0 As String, valores As Single(), count As Integer)
        If count <= 0 Then Exit Sub
        Dim loc As Integer = GetUniformLocationCached(nombreElemento0)
        If loc <> -1 Then
            GL.Uniform3(loc, count, valores)
        End If
    End Sub

    ''' <summary>Bindea una TEXTURE_2D_ARRAY. Existe aparte de <see cref="BindTexture"/> porque el target
    ''' es parte del ESTADO de la unidad, no del uniform: bindear un array con el metodo de 2D deja el
    ''' sampler leyendo un target que no tiene nada, y el sintoma es una sombra que no aparece nunca.
    ''' <para>LAS UNIDADES 14 Y 15 SON EXCLUSIVAS DE SOMBRAS. Una unidad puede tener bindeados los dos
    ''' targets a la vez y el sampler elige por su tipo; mezclar ahi un texture2D de otro pase es como se
    ''' arma un bug que solo aparece con ciertos materiales.</para></summary>
    Public Sub BindTextureArray(uniformName As String, textureID As Integer, unit As TextureUnit)
        GL.ActiveTexture(unit)
        GL.BindTexture(TextureTarget.Texture2DArray, textureID)
        SetInt(uniformName, unit - TextureUnit.Texture0)
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
