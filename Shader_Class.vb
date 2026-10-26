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
// True while the frame draws in D3D row order (Fallout 4's forward stage, PreviewControl.SubirUniformsDeFondo): the window
// rows then count from the top and the background is a function of the display's rows, y = H - y.
uniform bool backgroundRowsD3D;

vec3 backgroundAt(in vec2 fragXYIn)
{
    vec2 fragXY = backgroundRowsD3D ? vec2(fragXYIn.x, viewportSize.y - fragXYIn.y) : fragXYIn;
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
" & Gl43.ClipGlsl & "
void main()
{
    vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    gl_Position = gl43ClipToGl(vec4(p * 2.0 - 1.0, 0.0, 1.0));
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
    Friend Const Vertex_Floor As String =
"#version 430
" & Gl43.ClipGlsl & "layout(location = 0) in vec3 vertexPosition;

uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModel;

out vec3 worldPos;

void main()
{
    vec4 wp = matModel * vec4(vertexPosition, 1.0);
    worldPos = wp.xyz;
    gl_Position = gl43ClipToGl(matProjection * matView * wp);
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

''' <summary>FALLOUT 4'S EDIT VIEWS: the wireframe and the shader debug views (ShaderDebugView), an app program (ola 7f, user decisions
''' 21-oct-2026: a small vector program without lighting or shadow; the wireframe drawn on top of the frame as before). Every shape the
''' game draws goes through the game's transcribed programs (G-buffer records, effects, utility, lights, composites, post); this one
''' draws only what the game has no counterpart for: a shape in wireframe (RenderableMesh.ColourDrawProgram, any frame) and every
''' shape of a frame with a debug view (PreviewModel.FrameUsesFo4Deferred False). PreviewControl.CurrentShader in Fallout 4.
''' <para>Its vertex stage is the app's skinning (the Fallout 4 GPU blend in the engine's order and weights, AppSkinLaw after it), so the
''' geometry it draws is the geometry the edit views always drew. The GLSL is PURE ASCII and has NO double quote (it lives in a VB
''' Const String); the glsl-ascii gate covers it.</para></summary>
Public Class Shader_Class_Fo4EditView
    Inherits Shader_Base_Class

    Friend Const Vertex_Fo4EditView As String = "
#version 430
" & Gl43.ClipGlsl & "// FALLOUT 4'S EDIT VIEWS (Shader_Class_Fo4EditView): the wireframe and the shader debug views. Not the engine's: the game's shapes are
// drawn by its transcribed programs.
// invariant: gl_Position is fixed by its expression and its inputs, whatever else a program computes (GLSL 4.60 4.8.1) - the
// expression below is the one the edit views always had, so the geometry they draw did not move when this program was cut down.
invariant gl_Position;
uniform mat4 matProjection;
uniform mat4 matView;
uniform mat4 matModelView;
uniform mat3 mv_normalMatrix;
uniform vec3 color;          // the shape's wire colour (IRenderableShape.Wirecolor)

uniform bool bModelSpace;   // Model Space Normals: needs the object->view matrix in the VS (MSN CPU-skin path)
uniform bool bShowMask;
uniform bool bShowVertexColor;
uniform bool bShowVertexAlpha;
uniform bool bApplyZap;
uniform vec4 uFo4PrepassTexcoord;   // cb2[4] of the shape's z-prepass VS (MaterialData.Fo4PrepassTexcoord): the prepass's cut
uniform bool bFo4PrepassVertexAlpha; // the prepass VAO carries COLOR0 (RenderableMesh.PrepassStreamHasColor)

layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec3 vertexNormal;
layout(location = 2) in vec3 vertexTangent;
layout(location = 3) in vec3 vertexBitangent;
layout(location = 4) in vec3 vertexColors;
layout(location = 5) in float vertexAlpha;
layout(location = 6) in vec2 vertexUV;
layout(location = 7) in float vertexMask;
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
//   1. The GPU blend: TWO texts, one per game's law - this VS (Fallout 4: the engine's order and weights) and
//      Vertex_SSE (Skyrim SE: normalized weights); the normal law they apply after it, AppSkinLaw.Glsl, is ONE text
//      included by both and by the transcribed engine VS (G-buffer).
//   2. SkinningHelper.BlendBoneMatrices        (CPU blend, double precision)
//   3. SkinningHelper.RecomputeGPUBoneMatrices (bone matrix composition -> SSBO)
//   4. SkinningHelper.ExtractSkinnedGeometry   (GPU arrays: idx/weights)
//   5. Render.UpdateSkinBuffers_GL             (CPU pre-skin path)
//   + SkinBakeMath / FaceGenBuildPipeline      (the bake: the engine's weights, its own accumulation)
// Fallout 4 (D21, user decision 12-oct-2026): view and bake use the engine's law (Fo4EngineSkin: w3 = 1 - sat(...); the view
// blends in its order 1, 0, 2, 3, in Single), always.
//   - N goes by the inverse-transpose of the blended 3x3 and T/B by the blended 3x3, in BOTH routes: GPU by AppSkinLaw.Glsl
//     (this VS, the other game's VS and the transcribed engine VS), CPU by FastSkin.UnVertice / TransformarDirecto - the same law
//     transcribed (AppSkinLaw summary).
// Parity test: flip Setting_GPUSkinning on a posed/morphed shape - must look identical.

// The rig's light directions (ApplyMaterial, PreviewModel.FrameLights): the TBN error view (DebugMode 4) measures the light
// each TBN would give along them. Nothing is lit here.
struct RigLightDirection
{
	vec3 direction;
};

uniform RigLightDirection frontal;
uniform RigLightDirection directional0;
uniform RigLightDirection directional1;
uniform RigLightDirection directional2;

out vec3 lightFrontal;
out vec3 lightDirectional0;
out vec3 lightDirectional1;
out vec3 lightDirectional2;

out mat3 mv_tbn;
out mat3 v_msnMatrix;   // MSN: object->view normal matrix (skinning+view), used by the Fragment MSN branch

out float maskFactor;
flat out int ZappedVert;

out vec4 vColor;
out vec2 vUV;
out vec3 vFo4PrepassTexcoord;   // the z-prepass VS's TEXCOORD0.xyz (rec3929), for the prepass's cut in the fragment stage

" & AppSkinLaw.Glsl & "
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
	vColor = vec4(1.0, 1.0, 1.0, 1.0);
	vUV = vertexUV;
	// The z-prepass VS's TEXCOORD0 (rec3929 L47-48: mad o1.xy, v1.xyxx, cb2[4].zwzz, cb2[4].xyxx / mov o1.z, v2.w): the UV through the
	// prepass's cb2[4] and its COLOR0.w, per vertex as the prepass computes them. The alpha-test layout always carries COLOR0 (the
	// engine ORs its bit in, Fo4EngineVertexLayout.PrepassAlphaDesc): the vertex alpha with a colour stream; without one, the
	// bytes at the desc's colour offset, which cb2[0].y = 10000 cancels - as any value in [0, 1] here. The 1.0 branch is for a
	// layout without COLOR0 (GL's current generic attribute, (0, 0, 0, 1), GL 4.6 10.2), which the owner no longer builds.
	vFo4PrepassTexcoord = vec3(vertexUV * uFo4PrepassTexcoord.zw + uFo4PrepassTexcoord.xy, bFo4PrepassVertexAlpha ? vertexAlpha : 1.0);

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
	    ivec4 bIdx = ivec4(boneIndicesF);
	    vec4 bWgt = boneWeightsIn;

	    mat4 skinMatrix = mat4(0.0);
	    // Accumulate weighted bone matrices in the engine VS order (rec2304 L54-61: slot 1, 0, 2, 3; in Fallout 4's view the
	    // weights are the engine's, Fo4EngineSkin.EngineWeights - D21). A zero weight or a bone outside the palette adds nothing
	    // (the engine reads 0 past the bound rows, D3D11.3 7.5; the CPU blends skip them too).
	    if (bWgt.y != 0.0 && bIdx.y >= 0 && bIdx.y < uBoneCount) skinMatrix = bones[bIdx.y] * bWgt.y + skinMatrix;
	    if (bWgt.x != 0.0 && bIdx.x >= 0 && bIdx.x < uBoneCount) skinMatrix = bones[bIdx.x] * bWgt.x + skinMatrix;
	    if (bWgt.z != 0.0 && bIdx.z >= 0 && bIdx.z < uBoneCount) skinMatrix = bones[bIdx.z] * bWgt.z + skinMatrix;
	    if (bWgt.w != 0.0 && bIdx.w >= 0 && bIdx.w < uBoneCount) skinMatrix = bones[bIdx.w] * bWgt.w + skinMatrix;

	    // Zero-weight fallback: first bone (matches CPU BlendBoneMatrices), then identity if no bones
	    float totalWeight = bWgt.x + bWgt.y + bWgt.z + bWgt.w;
	    if (totalWeight < 0.001) skinMatrix = (uBoneCount > 0) ? bones[clamp(bIdx.x, 0, uBoneCount - 1)] : mat4(1.0);

	    skinnedPos = vec3(skinMatrix * vec4(vertexPosition, 1.0));

	    // Correct normal matrix: transpose of inverse of upper-left 3x3
	    vec3 sr0 = vec3(skinMatrix[0][0], skinMatrix[1][0], skinMatrix[2][0]), sr1 = vec3(skinMatrix[0][1], skinMatrix[1][1], skinMatrix[2][1]), sr2 = vec3(skinMatrix[0][2], skinMatrix[1][2], skinMatrix[2][2]);
	    mat3 skinNormalMat = appSkinNormalMatrix(sr0, sr1, sr2);   // AppSkinLaw (D2 / revisor-22): the one law text
	    skinnedNormal = appSkinRotate(skinNormalMat, vertexNormal);
	    // TANGENT AND BITANGENT GO WITH THE RAW MATRIX, not with the normal matrix. They are directions
	    // ALONG the surface: they move the way the geometry moves. The inverse-transpose is the law of the
	    // NORMAL and of nothing else -- applying it to the tangent pushes it out of the tangent plane as
	    // soon as there is shear, and a blend of two different bone rotations ALWAYS has shear, which means
	    // every vertex of an elbow, a knee or a shoulder.
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
	gl_Position = gl43ClipToGl(matProjection * vec4(vPos, 1.0));

	// TBN in view space
	vec3 mv_normal = mv_normalMatrix * skinnedNormal;
	vec3 mv_tangent = mv_normalMatrix * skinnedTangent;
	vec3 mv_bitangent = mv_normalMatrix * skinnedBitangent;

    mv_tbn = mat3(mv_tangent.x,   mv_tangent.y,   mv_tangent.z,
              mv_bitangent.x, mv_bitangent.y, mv_bitangent.z,
              mv_normal.x,    mv_normal.y,    mv_normal.z);

	lightFrontal = normalize(mat3(matView) * frontal.direction);
	lightDirectional0 = normalize(mat3(matView) * directional0.direction);
	lightDirectional1 = normalize(mat3(matView) * directional1.direction);
	lightDirectional2 = normalize(mat3(matView) * directional2.direction);

	// The wire colour tints the line (rgb); the .w is the literal 1.0, so vColor.a stays the vertex alpha the alpha test reads.
	vColor *= clamp(vec4(color, 1.0), 0.0, 1.0);
}
"

    Friend Shared ReadOnly Fragment_Fo4EditView As String = "
#version 430

// FALLOUT 4'S EDIT VIEWS (Shader_Class_Fo4EditView). What it draws, and nothing else:
//  * a WIREFRAME (bWireframe): the line in the shape's wire colour (or its diffuse with the texture on) times the mask view, with
//    the shape's WireAlpha; drawn on top of the frame (PreviewModel.RenderDisplayOverlays after the post, or in the display target).
//  * a SHADER DEBUG VIEW (DebugMode 1..5, ShaderDebugView): the shape's vectors as RGB - no lighting, no shadow. Each shape keeps the
//    fragments the frame keeps: the hide / zap discards and the cut of its depth in the frame (ola 7f round 2, p7f-03, user
//    decision 21-oct-2026) - a shape the z-prepass draws (bFo4PrepassCut: Fo4RenderPassLaw InPrepass) takes its depth from the
//    prepass, so it keeps the prepass's fragments: the prepass's own alpha test (the 0x1A entry, bFo4PrepassAlphaTest), its very
//    text (Fo4GPre_rec3930, Fo4GBufferSource.PrepassAlphaKill, included below), on the prepass VS's TEXCOORD0 and the prepass's
//    t0 and constants; any other shape, the alpha test of its colour pass (bAlphaTest / alphaThreshold:
//    Fo4RenderPassLaw.ColourPassAlphaTest; the lighting shape's tested value tex.a [* vColor.a] under the G-buffer's vertex-alpha
//    gate, an effect's 1.0) - and the output alpha its blend reads (a lighting shape's material alpha, an effect 1.0).
// The program has no output for a filled shape without a debug view: ColourDrawProgram never draws one with it.
// ALSO: this GLSL lives inside a VB String. PURE ASCII, and NO double quote. The glsl-ascii gate in Tools/ParityGate enforces both.

uniform sampler2D texDiffuse;
uniform sampler2D texNormal;

uniform bool bShowTexture;
uniform bool bWireframe;
uniform bool bApplyZap;
uniform bool bHide;
uniform bool bNormalMap;
uniform bool bModelSpace;
uniform bool bAlphaTest;
uniform bool bIsEffectShader;
// The G-buffer's alpha test multiplies the vertex alpha only with technique bit 0 (F4SF2 Vertex_Colors) and without
// bits 10 / 6 (Tree_Anim; Tessellate + Dismemberment, Meatcuff, Face + eye data): Fo4RenderPassLaw.
uniform bool bFo4GBufferTestVertexAlpha;

uniform float DebugMode;
uniform vec2 uvOffset;
uniform vec2 uvScale;
uniform float alpha;
uniform float WireAlpha;
uniform float alphaThreshold;

in vec3 lightFrontal;
in vec3 lightDirectional0;
in vec3 lightDirectional1;
in vec3 lightDirectional2;

in mat3 mv_tbn;
in mat3 v_msnMatrix;   // MSN: object->view normal matrix from the vertex shader

in float maskFactor;
flat in int ZappedVert;

in vec4 vColor;
in vec2 vUV;

layout(location = 0) out vec4 fragColor;

// THE Z-PREPASS'S CUT (ola 7f round 2, p7f-03): ApplyMaterial, Render ApplyFo4EditViewDepthCut - from Fo4RenderPassLaw.Classify
// (InPrepass, PrepassAlphaTest), the prepass's PS constants (cb2[0] of rec3930: MaterialData.Fo4PrepassPsConstants, the prepass
// draw's own writer) and its t0 (the material's base texture with SamplerLaw.UtilityDiffuse, at SamplerLaw.AppUnit.EditViewPrepassT0).
uniform bool bFo4PrepassCut;
uniform bool bFo4PrepassAlphaTest;
uniform vec4 cb2_ps0;
layout(binding = " & CStr(CInt(SamplerLaw.AppUnit.EditViewPrepassT0)) & ") uniform sampler2D t0;
in vec3 vFo4PrepassTexcoord;
// The prepass PS's input TEXCOORD0, set from vFo4PrepassTexcoord before Fo4GPre_rec3930 reads it.
vec4 gbuf_TEXCOORD0;
" & D3d11SemanticsSource.Glsl & Fo4GBufferSource.PrepassAlphaKill() & "
// Tangent-space normal of a two-channel normal-map texel: xy = rg * 2 - 1, z = sqrt(1 - min(x^2 + y^2, 1)). The game clamps
// before the root in every b05 PS that rebuilds z (202 of 202: `dp2 / min r, r, l(1.0) / add -r, 1 / sqrt`, e.g. rec0828):
// a texel outside the unit disk gives z = 0, not NaN. The TBN error view decodes the map with it (gate fo4-effect-ps-law).
vec3 fo4TangentNormal(in vec3 texel)
{
	vec2 xy = texel.rg * 2.0 - 1.0;
	return vec3(xy, sqrt(1.0 - min(dot(xy, xy), 1.0)));
}

void main(void)
{
	vec2 uv = vUV * uvScale + uvOffset;
	// Every texture is read here, before any discard: the implicit derivatives stay defined (uniform branches only).
	vec4 baseMap = vec4(1.0);
	if (bShowTexture)
		baseMap = texture(texDiffuse, uv);
	vec4 normalTexel = vec4(0.5, 0.5, 1.0, 1.0);
	if (bShowTexture && bNormalMap)
		normalTexel = texture(texNormal, uv);
	// The z-prepass's alpha test of a prepass shape (the 0x1A entry): the prepass PS's own text on its TEXCOORD0 and its t0.
	bool prepassKill = false;
	if (!bWireframe && bFo4PrepassCut && bFo4PrepassAlphaTest)
	{
		gbuf_TEXCOORD0 = vec4(vFo4PrepassTexcoord, 0.0);
		Fo4GPre_rec3930(prepassKill);
	}

	if (bWireframe)
	{
		vec3 shaded = bShowTexture ? baseMap.rgb : vColor.rgb;
		fragColor = vec4(shaded * maskFactor, WireAlpha);
	}

//====================DEBUG MODE==========================
if (DebugMode > 0.0) {
    // THE VIEWS ARE MODEL-SPACE AWARE. On an MSN shape mv_tbn is DEGENERATE: the normal comes from the map through the
    // object->view matrix (v_msnMatrix), as the game's MSN programs decode it (prepass VS rec2215 -> PS rec2698: the VS
    // passes the object->view rows, the PS reorders the sampled (R,G,B) = (X,Z,Y) -> .rbg before the transform).
    vec3 dbgNormal;
    if (bModelSpace) {
        // ON MSN THE GEOMETRY NORMAL IS THE MAP'S: the seed v_msnMatrix * (0,0,1) is the object's +Z, a constant direction per
        // shape; the map stores the object-space normal, so decoding it IS reading the geometry normal.
        if (bShowTexture && bNormalMap) {
            dbgNormal = normalize(v_msnMatrix * (normalTexel.rbg * 2.0 - 1.0));
        } else {
            dbgNormal = normalize(v_msnMatrix * vec3(0.0, 0.0, 1.0));
        }
    } else {
        dbgNormal = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));
    }

    // Flat grey = this view does NOT APPLY to this shape: tangent, bitangent and TBN error exist only with a tangent basis,
    // and a model-space shape has none.
    vec4 noAplica = vec4(0.5, 0.5, 0.5, 1.0);

    if (abs(DebugMode - 1.0) < 0.5) {
        // Mode 1: the GEOMETRIC normal in view space (the normal map only where it is the geometry: MSN, above).
        fragColor = vec4(dbgNormal * 0.5 + 0.5, 1.0);
    }
    else if (abs(DebugMode - 2.0) < 0.5) {
        // Mode 2: tangents. Tangent space only.
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            fragColor = vec4(normalize(mv_tbn * vec3(1.0, 0.0, 0.0)) * 0.5 + 0.5, 1.0);
        }
    }
    else if (abs(DebugMode - 3.0) < 0.5) {
        // Mode 3: bitangents. Tangent space only.
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            fragColor = vec4(normalize(mv_tbn * vec3(0.0, 1.0, 0.0)) * 0.5 + 0.5, 1.0);
        }
    }
    else if (abs(DebugMode - 4.0) < 0.5) {
        // Mode 4: TBN error - the NIF's TBN against a Gram-Schmidt one, in the normal and in the light each gives along the rig's
        // directions. NOT APPLICABLE in model space: there is no TBN to compare. Like every view it keeps the frame's discards
        // and alpha test below (it used to return here and skip them).
        if (bModelSpace) {
            fragColor = noAplica;
        } else {
            vec3 Tm = normalize(mv_tbn * vec3(1.0, 0.0, 0.0));
            vec3 Bm = normalize(mv_tbn * vec3(0.0, 1.0, 0.0));
            vec3 Nm = normalize(mv_tbn * vec3(0.0, 0.0, 1.0));

            vec3 Tgs = normalize(Tm - Nm * dot(Nm, Tm));
            vec3 Bx  = normalize(cross(Nm, Tgs));
            float h  = sign(dot(Bm, Bx));
            mat3 tbn_fixed = mat3(Tgs, Bx * h, Nm);

            vec3 n_ts = vec3(0.0, 0.0, 1.0);
            if (bShowTexture && bNormalMap) {
                n_ts = fo4TangentNormal(normalTexel.rgb);   // the game's z law (rev-08)
            }
            vec3 nA = normalize(mv_tbn   * n_ts);
            vec3 nB = normalize(tbn_fixed * n_ts);

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
    }
    else if (abs(DebugMode - 5.0) < 0.5) {
        // Mode 5: THE RAW NORMAL MAP, as the sampler returns it: not decoded, no rebuilt Z, never through the TBN. Without a
        // normal map, the neutral plane (0.5, 0.5, 1.0) a tangent-space map holds where the surface does not deviate.
        fragColor = vec4(normalTexel.rgb, 1.0);
    }
}
//===================END DEBUG MODE=======================

	if (bHide)
		discard;
	if (bApplyZap && ZappedVert == 1)
		discard;

	if (!bWireframe)
	{
		// THE CUT OF THE SHAPE'S DEPTH IN THE FRAME. A shape the z-prepass draws: the prepass's fragments (prepassKill; an entry
		// without the test cuts nothing). Any other shape: its colour pass's test (engine: discard if alpha < ref, the equality
		// KEPT) - a lighting shape tests its texture alpha [x the vertex alpha under the G-buffer's gate]; an effect tests the value
		// this view writes (1.0).
		if (bFo4PrepassCut)
		{
			if (prepassKill)
				discard;
		}
		else
		{
			float alphaTestValue = bIsEffectShader ? fragColor.a : baseMap.a * (bFo4GBufferTestVertexAlpha ? vColor.a : 1.0);
			if (bAlphaTest && alphaTestValue < alphaThreshold)
				discard;
		}
		// The output alpha the draw's blend reads (ApplyMaterial: Fo4GBufferBlendMode / Fo4EffectBlend): a lighting shape's
		// material alpha, an effect's 1.0.
		if (!bIsEffectShader)
			fragColor.a *= alpha;
	}
}
"
    Sub New()
        MyBase.New(Vertex_Fo4EditView, Fragment_Fo4EditView)
    End Sub
End Class

Public Class Shader_Class_SSE
    Inherits Shader_Base_Class
    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE DE SOMBRA DE SKYRIM SE.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_SSE</c> y en <c>ShadowDepthShaderSource.Fragment_ShadowDepth</c>: ahi lo ve quien
    ''' edita la logica, que es donde tiene que estar. Los dos se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado.</b> Tocar uno sin el otro rompe la silueta de la sombra sin que nada lo reporte. Fallout 4 no
    ''' entra: dibuja sus mapas con la tecnica utilitaria del motor (b07, RenderableMesh.RenderShadowMapFo4), cuyo test
    ''' de alpha es el de su programa transcripto.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Vertex_SSE As String = "
#version 430
" & Gl43.ClipGlsl & "// SSE vertex shader with model-space normal (MSN) support
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
// EYE technique (type 16, S-O1). The eye VS (SSE lighting VS 0x10000002/3/202/203, recs 4019-4022: the only eye VS, all
// SKINNED) takes its reflection normal from a per-vertex centre: centre = cb1[0] + (cb1[1] - cb1[0]) * TEXCOORD2.x
// (`add [precise] r12, -cb1[0], cb1[1] ; mul r12, r12, v6.xxxx ; add r12, r12, cb1[0]`), cb1[0] / cb1[1] = the NIF's
// LeftEyeReflectionCenter / RightEyeReflectionCenter (SetupMaterial eye case 0x141548931..99E: material +0xB4 / +0xC0),
// TEXCOORD2.x = the vertex eye data. That centre goes through the SAME blended skin rows as the position (dp4 r2/r7/r5),
// then o7 = normalize(pos - centre). vertexEyeCenter = that bind-space centre (Render.vb SseEyeReflectionCenter), or - with
// CPU skinning, where the positions arrive skinned - the centre already through the vertex's skin matrix (Render.vb
// EyeCentresForUpload). Not bound (no eye shape): the attribute reads (0,0,0) and vEyeRadial is unused (bEye false).
layout(location = 12) in vec3 vertexEyeCenter;
out vec3 vEyeRadial;
vec3 eyeCenterPos = vec3(0.0);
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
//   1. The GPU blend: TWO texts, one per game's law - Shader_Class_Fo4EditView.Vertex_Fo4EditView (Fallout 4: the engine's order and weights) and
//      Vertex_SSE (Skyrim SE: normalized weights); the normal law they apply after it, AppSkinLaw.Glsl, is ONE text
//      included by both and by the transcribed engine VS (G-buffer).
//   2. SkinningHelper.BlendBoneMatrices        (CPU blend, double precision)
//   3. SkinningHelper.RecomputeGPUBoneMatrices (bone matrix composition -> SSBO)
//   4. SkinningHelper.ExtractSkinnedGeometry   (GPU arrays: idx/weights, sum=1)
//   5. Render.UpdateSkinBuffers_GL             (CPU pre-skin path)
//   + SkinBakeMath / FaceGenBuildPipeline      (the bake, same formula)
// Differences BY DESIGN (not drift):
//   - GPU: float precision, weights pre-normalized at extract (sum=1).
//   - CPU: double precision, normalized at runtime (1/sumW).
//   - N goes by the inverse-transpose of the blended 3x3 and T/B by the blended 3x3, in BOTH routes: GPU by AppSkinLaw.Glsl
//     (this VS, the other game's VS and the transcribed engine VS), CPU by FastSkin.UnVertice / TransformarDirecto - the same law
//     transcribed (AppSkinLaw summary).
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

// SSE EFFECT FALLOFF, PER VERTEX (BSEffect VS t00010153: o1.z). Off on geometry without
// vertex normals (model-space-normal geometry), where the PS keeps its per-pixel evaluation.
uniform bool bVertexFalloff;
uniform vec4 effectFalloffParams;   // cb1[2]: x startAngle, y stopAngle, z startOpacity, w stopOpacity
out float vEffectFalloff;

" & AppEditViewsSource.ColorRamp & "

" & RefractionSource.Vertex_Glsl & "
" & D3d11SemanticsSource.Glsl & SseAoNormalSource.EngineFrame_Glsl & SseAoNormalSource.Vs_Glsl & "
" & AppSkinLaw.Glsl & "
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
	    vec3 sr0 = vec3(skinMatrix[0][0], skinMatrix[1][0], skinMatrix[2][0]), sr1 = vec3(skinMatrix[0][1], skinMatrix[1][1], skinMatrix[2][1]), sr2 = vec3(skinMatrix[0][2], skinMatrix[1][2], skinMatrix[2][2]);
	    mat3 skinNormalMat = appSkinNormalMatrix(sr0, sr1, sr2);   // AppSkinLaw (D2 / revisor-22): the one law text
	    skinnedNormal = appSkinRotate(skinNormalMat, vertexNormal);
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
	    // EYE (S-O1): the reflection centre through the same blended matrix as the position (eye VS dp4 r2/r7/r5).
	    eyeCenterPos = vec3(skinMatrix * vec4(vertexEyeCenter, 1.0));
	} else {
	    // CPU skinning fallback: vertices already in world space
	    skinnedPos = vertexPosition;
	    skinnedNormal = vertexNormal;
	    skinnedTangent = vertexTangent;
	    skinnedBitangent = vertexBitangent;
	    // EYE (S-O1): uploaded already skinned, like the position (Render.vb EyeCentresForUpload).
	    eyeCenterPos = vertexEyeCenter;

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
	gl_Position = gl43ClipToGl(matProjection * vec4(vPos, 1.0));
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

	// EYE (S-O1): o7 = normalize(skinned pos - skinned centre) (eye VS `add r2.xyz, -r2.xyzx, r4.xyzx ; dp3 ; rsq ; mul o7`),
	// taken to view space by the same matrix as vPos, so that Fragment_SSE's view -> world (matModel * matModelViewInverse)
	// gives it back exactly. Interpolated raw, as o7 is (the PS reads v7 without normalizing it).
	vEyeRadial = mat3(matModelView) * normalize(skinnedPos - eyeCenterPos);

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
//   And the other way round: that pass points back here. The GLSL alpha logic is written twice and
//   must stay in step. The THRESHOLD is not GLSL: Render.vb uploads it per pass, each from its own
//   law -- SseRenderPassLaw for the lit pass, SseRenderPassLaw.ShadowAlphaTest for the shadow (the batch's cb11[0].x).
//   Skyrim SE only: Fallout 4 draws its shadow maps with the engine's utility technique (b07,
//   RenderableMesh.RenderShadowMapFo4), whose alpha test is its transcribed program's.
//
// WHY. The shadow map is drawn by a DIFFERENT program, and that program decides WHICH FRAGMENT
// EXISTS using the same alpha this shader uses. If the two stop agreeing, the shadow silhouette
// stops being the silhouette of the object on screen: shadow appears for geometry that was
// discarded, or is missing for geometry that is drawn. Nothing reports it -- it just looks wrong,
// and the cause is two files away.
//
// THE LIST. Touch any of these here, and go fix Fragment_ShadowDepth in the same commit -- for the
// LIGHTING shader (.bgsm), the only family the depth pass draws:
//   * the ALPHA TEST -- which quantity is compared (its threshold is uploaded per pass, see above)
//   * the VERTEX ALPHA -- and which predicate gates it
//   * the MATERIAL ALPHA SCALAR -- whether it enters the test or is applied afterwards
//   * the ZAP discard
//   * SKINNING -- if a vertex moves, its shadow moves too
//   * the UV transform (offset / scale) used to sample the diffuse
// The EFFECT shader (.bgem) is NOT mirrored: neither engine draws an effect into a shadow map (FO4
// BSEffectShaderProperty slot 44 = 0x142168DA0 xor eax,eax; ret -- SSE slot 0x2B builds a pass only in
// mode 0xC, 0x14152A0C2), nor a lighting shape that blends, whose alpha is below 1 or that refracts (FO4
// slot 44 0x14217B95C..98A; SSE slot 0x2B 0x14151B3C3..405), nor a decal (FO4 flags 0xC000000; SSE: a
// declared hole, kept out as before). The caster filter keeps them out
// (Fo4RenderPassLaw / SseRenderPassLaw.ShadowMapCasts): the depth pass is a cutout and nothing else.
//
// !!!! THE QUANTITY IS SKYRIM SE'S. The material alpha scalar of a lighting shader (.bgsm) enters it BEFORE the
// test (cb2[3].z, the DO_ALPHA_TEST block below) and the depth pass mirrors that. Fallout 4 tests without it, after,
// only for the blend (its own law, in its transcribed programs): a law correct for one game can be wrong for the
// other, never copy one into the other.
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
// APP OPTION, NOT THE ENGINE: the post-off 2.3.8 display tail (LegacyDisplaySource; ApplyMaterial uploads both).
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
// EYE technique (S-O1): the eye VS's o7, in view space (Vertex_SSE). bEyeRadial = the shape carries it (Render.vb: a type-16
// lighting block whose geometry has per-vertex eye data); without it the reflection keeps the vertex normal (declared hole).
in vec3 vEyeRadial;
uniform bool bEyeRadial;

" & ShadowDepthShaderSource.SharedUniformsGlsl & "
layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec4 coverageOut;
// SSE SAO normals target (o2 of the opaque MRT, RE_SAO_BOTH 7.1 / 11-12); written only while draw buffer 3 is bound (SSE + post).
layout(location = 3) out vec4 aoNormalOut;
// SSE SNOW o3 -> kSNOW_SPECALPHA (RT 0x70, R8G8_UNORM, 0x14153B87B..8A3): written only while draw buffer 4 is bound (SSE + post,
// the opaque MRT) and only by a snow draw (Render.vb masks buffer 4 for every other draw: a PS without o3 leaves the RT as it is).
layout(location = 4) out vec4 snowSpecAlphaOut;
// THE TRANSLUCENT DECALS' BASE (DecalBaseTarget; user decision 7-oct-2026). uDecalBaseMode (RenderableMesh.DecalBaseMode; Fallout 4's effect foot reads the same modes, Fo4EffectSource) - the base depth
// draw of a decal whose colour blend's source factor is SRC_ALPHA keeps only its fragments with alpha > 0 (RenderableMesh.
// DecalBaseMode). uSseDecalBaseSurface: the base SURFACE draw - the same program, inputs and discards as the decal's depth draw
// (so the same fragments), lit as a plain surface: albedo = the preview's background at the pixel (the SSE scene is raw,
// Shader_Base_Class.MaterialColor), normal = the geometric normal (geoNormal), no specular, back / soft / rim light, envmap,
// emissive or hair tint, the lit colour also for an effect, ALPHA THE DECAL'S (user decision 7-oct-2026, 3: the base follows the
// decal's alpha; RenderableMesh.DrawDecalBaseSurface blends it SRC_ALPHA / ONE_MINUS_SRC_ALPHA, coverage ONE / ONE_MINUS_SRC_ALPHA).
uniform int uDecalBaseMode;
uniform bool uSseDecalBaseSurface;
" & BackgroundFadeSource.Fade_Helper & "
uniform int uSseAoNormal;        // SseAoNormal: 0 none, 1 normal, 2 colour
uniform int uSseAoEffectClass;   // SseAoEffectNormal: 0 lighting (sseAoLitNormal), 1 view (VS 420), 2 raw skinned (VS 425), 3 world (VS 431)
uniform vec4 uSseSsrParams;      // cb2[7] of PS 4255: (fSpecMaskBegin, fSpecMaskBegin + fSpecMaskSpan, 0, SSRParams.w)
in vec3 vSseAoEffectNormal;      // TEXCOORD7.yzw of the bit-26 effect VS
vec3 sseAoLitNormal = vec3(0.0, 0.0, 1.0);
// What this draw adds to the coverage of the HDR target (see SceneTargets): 0 unblended (1),
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
// l(-0.015686) of that discard (PS idx 9031: mad r0.w, a, v11.w, l(-0.015686)) is the token 0xBC808081 = -4/255 in float32;
// the printed decimal would be 0xBC807FED. A macro, not a const: glslang rejects uintBitsToFloat in a global const initializer.
#define SSE_L_M0_015686 uintBitsToFloat(0xBC808081u)
// invFrameBufferRange (sseInvFramebufferRange) and the lighting output tail: SseLitTailSource.
// The space the SSE lighting PS works in, as a rotation of an app-WORLD direction (Render.vb SseLitSpace): identity when
// the technique is SKINNED (world space), otherwise the inverse of the matrix the app places the shape with (its model space).
uniform mat3 uSseLitSpace;
// SSE SNOW (technique bit 21, SseRenderPassLaw.SnowTechnique). The snow PS is the same key without bits 21 / 19 with SpecularColor 0
// (re2/snowblood validate_snow.py law (a)) and writes o3 = (dot(rim + point-light specular, (0.3, 0.59, 0.11)), t0.a) (laws (b), (c)).
// rim = (w > 1e-5) * x * pow(1 - sat(Ng.V), y) * pow(1 - sat(n.V), z); uSseSnowRim = cb1[13] (SseRenderPassLaw.SnowRimLightParameters).
uniform bool bSseSnow;
uniform vec4 uSseSnowRim;
// SSE effect BLOOD (technique bit 14, SseRenderPassLaw.EffectBloodTechnique; PS 0x4842 / 0x4843): G = t0.g * vc.g * bc.g,
// Bl = t0.b * vc.b * bc.b * vc.a, alpha = G * cb2[8].w; s = sat(G - p.x), k = (s < p.y) ? (s / p.y) * Bl : Bl, m = 1 - k * p.z,
// c = m * (sat(2 - k), sat(1 - k), sat(1 - k)); PropertyColor (1,1,1) and no fog here. uSseBloodParams = cb2[9] = (NiAlphaProperty
// threshold / 255, fWeaponBloodFalloffThreshold, fWeaponBloodAlphaToRGBScale): the last two are 0 at rest (globals 0x1434866F0 / F4).
uniform bool bSseBlood;
uniform vec3 uSseBloodParams;
// SKYRIM SE PARALLAX (technique type 3) and MULTILAYER PARALLAX (type 11), S-P / S-M (Render.vb ApplyMaterial): texHeight = slot 3
// (PS t3), texInnerLayer = slot 6 (PS t8), uMlpLayer = PS cb1[6] (inner layer thickness, refraction scale, inner scale u, v).
uniform bool bSseParallax;
uniform bool bSseMultiLayer;
uniform sampler2D texHeight;
uniform sampler2D texInnerLayer;
uniform vec4 uMlpLayer;
// ShadowDepthShaderSource.WorldDirGlsl is included further down (hemiAmbient, the shadow normal); the anisotropic lobe
// of directionalLight needs it before.
vec3 toWorldDir(in vec3 v);
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
	if (bSpecular && bShowTexture && !uSseDecalBaseSurface)
	{
		smoothness = specGloss * shininess;
		roughness = 1.0 - smoothness;
		specMask = specFactor * specularStrength;

		if (bAnisoLighting)
		{
			// SSE ANISO_LIGHTING = technique bit 16 (SLSF2 Anisotropic_Lighting), in ANY technique type, not only hair: the
			// light's specular is a shifted-normal lobe instead of Blinn. Bytecode (PS 0x00010201 rec 4112; hair 0x06010203
			// rec 8996), N_geo = (v3.z, v4.z, v5.z), T = (v3.x, v4.x, v5.x) of the TBN -> mv_tbn[2], mv_tbn[0]:
			//   sh1 = normalize(0.5*bumpN + N_geo) ; a1 = pow(1 - min(|sh1.L - sh1.H|, 1), Glossiness)
			//   lobes = 0.7*a1                     every technique but HairTint
			//   lobes = 0.7*a1 + a2*hairTint       HairTint: sh2 = normalize(sh1 - 0.05*T), a2 like a1,
			//                                      hairTint = mix(1, cb1[1], COLOR0.y)
			//   spec += lobes * lightColor * max(L.z, 0)   (`max r2.z, cb2[0].z, l(0.0)`; each point light the same on its
			//                                              own direction, `max r6.w, r9.z, l(0.0)`)
			// Mask, SpecularStrength and SpecularColor enter once at the end, as in the Blinn path (specMask, specularColor).
			// L.z = the light direction IN THE PS SPACE: SetupGeometry 0x141549550 sends the directional light (negated
			// [light+0x140]) through D3DXVec3TransformNormal by the inverse world when the technique is NOT skinned
			// (0x141549B7D..B8E) and then D3DXVec3Normalize (0x141549BA1); skinned, it is normalized untouched (world).
			vec3 Ngeo = normalize(mv_tbn[2]);
			vec3 sh1  = normalize(0.5 * normal + Ngeo);
			float a1  = pow(1.0 - min(abs(dot(sh1, lightDir) - dot(sh1, halfDir)), 1.0), glossiness);
			vec3 lobes = vec3(0.7 * a1);
			if (bHairTint)
			{
				vec3 Ttan = normalize(mv_tbn[0]);
				vec3 sh2  = normalize(sh1 - 0.05 * Ttan);
				float a2  = pow(1.0 - min(abs(dot(sh2, lightDir) - dot(sh2, halfDir)), 1.0), glossiness);
				lobes += a2 * mix(vec3(1.0), tintColor, vColor.g);
			}
			float lightUp = max(normalize(uSseLitSpace * toWorldDir(lightDir)).z, 0.0);
			outSpec += lobes * light.diffuse * lightUp * specularColor * specMask;
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
	if (bBacklight && !uSseDecalBaseSurface)
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
	if (bSoftlight && !uSseDecalBaseSurface)
	{
		vec3 softMask = bLightmask ? texture(texLightmask, uv).rgb : albedo;
		float w = clamp((NdotL + subsurfaceRolloff) / (1.0 + subsurfaceRolloff), 0.0, 1.0);
		float nl = clamp(NdotL, 0.0, 1.0);
		float sss = clamp(w * w * (3.0 - 2.0 * w) - nl * nl * (3.0 - 2.0 * nl), 0.0, 1.0);
		outDiffuse += sss * softMask * light.diffuse;
	}

	// Rim lighting (engine idx 4042): pow(1 - saturate(N.V), rimPower) * saturate(dot(Vn,-L)) * rimTex * lightColor.
	// The saturate(dot(Vn,-L)) gate keeps it an edge/back-lit effect (this is what avoids the full-surface wash).
	if (bRimlight && !uSseDecalBaseSurface)
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
	// PARALLAX (technique type 3; the 72 type-3 PS of the cache, rec 8047 l. 66-73): uv' = v1 + normalize(v6).xy * (t3.r * 0.08 - 0.04),
	// v6 = cb2[6] - pos in the PS space (model: the type-3 VS 03000001 / 03000201 write o6 = cb2[6] - v0 and none is SKINNED),
	// .xy taken after the 3D normalize; t0 and t1 are sampled at uv', t3 at v1. The z-prepass above keeps v1 (BSUtilityShader).
	if (bSseParallax && bShowTexture && !bWireframe)
		uv += normalize(uSseLitSpace * toWorldDir(viewDir)).xy * (texture(texHeight, uv).r * 0.08 - 0.04);
	vec4 color = vColor;
	// HairTint technique (6): its PS reads COLOR0.yw only (input signature `COLOR 0 xyzw 12 NONE float y w`, PS 0x06010203
	// rec 8996): no vertex-colour rgb multiply; COLOR0.y is the tint mask (the hair tint below), .w the alpha.
	albedo = (bHairTint && !bIsEffectShader) ? vec3(1.0) : vColor.rgb;
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

			// El sampleo del normal map va FUERA de `bLightEnabled`: el bloque
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
						// y NO se lee .g (eso es la convencion de FO4).
						// OJO: el highlight ya usaba el uniform `glossiness` crudo, asi que specGloss no lo
						// tocaba; su unico otro consumidor era el LOD del cubemap, que resulto ser una
						// invencion de la app y ya se elimino (ver el bloque del cubemap). specGloss queda
						// hoy sin efecto real: se conservaba por simetria con el fragment forward de FO4, retirado en la ola 7f.
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
					// MultiLayerParallax: the mask is always t5.r (cb1[2].y = 1, its vf10 never leaves +0xB0 NULL, 0x141548F6D..F7F).
					if ((bEnvMask && !bGlowmap) || bSseMultiLayer)
					{
						// Environment Mask (BGSM slot 5 is dual: envmask when !bGlowmap)
						envMask = texture(texEnvMask, uv);
					}
				}
			}
		}

		// LA NORMAL SE CALCULA ACA, FUERA DE `bLightEnabled`, y no adentro como estaba.
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
		// shadow normal-offset -- shading keeps using the perturbed one. The reason: offsetting along a
		// normal-mapped direction turns part of the push into a lateral
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
				// here. (FO4 differs: its lighting shader rec2389/rec2963 DO recolor.)
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


			// The translucent decals' base surface: the geometric normal (uSseDecalBaseSurface).
			if (uSseDecalBaseSurface)
				normal = geoNormal;

			// SHADOW (see the SHADOWS uniform block):
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

			// The envmap / eye cube term (masked and scaled); it is multiplied by S and joins lit below.
			vec3 envTerm = vec3(0.0);
			// Environment cubemap (BGSM only; BGEM has its own cubemap path)
			if (bCubemap && bEnvMap && bShowTexture && !bIsEffectShader && !uSseDecalBaseSurface)
			{
				// ELIMINADO el LOD por glossiness: era una INVENCION de la app, sin respaldo del motor.
				// MEDIDO sobre los 6924 PS de BSLightingShader: `sample_l` y `sample_b` aparecen 0 veces y
				// los 1968 sampleos de cubemap (Envmap 1152 + MLP 624 + Eye 192) usan `sample` PLANO, o sea
				// seleccion de mip por hardware desde las derivadas. SSE no desenfoca la reflexion por
				// glossiness. El `8.0 - x*8.0` salia de leer mal el
				// `mad r0.z, r0.z, l(-8.0), l(8.0); sqrt; max; div; add 0.5`, que es el ENCODE SPHEREMAP de la
				// normal al G-buffer (o2.xy) y aparece igual en shaders que ni siquiera tocan un cubemap.

				// EYE technique (16, S-O1): the cube is looked up about v7, the eye VS's radial vector, RAW (192 of 192 eye PS:
				// `dp3 r3.x, v7.xyzx, Vn ; add r3.x, r3.x, r3.x ; mad R, r3.xxxx, v7.xyzx, -Vn`, e.g. 0x10000602 rec 10742), not about
				// the bump normal the lights use; reflect(-viewDir, N) is that same 2(N.V)N - V for an N of any length. Without per-vertex
				// eye data (bEyeRadial false) the game's input layout has no TEXCOORD2 (0x141011750: the element only with vertexDesc
				// bit 52 or 62, 0x141011AB8..B0E) and its result is not traced: the vertex normal stays, and the frame notice says so.
				// Non-eye envmap keeps the bump-normal reflection (sse_envmap L12-14).
				vec3 reflNormal = bEye ? (bEyeRadial ? vEyeRadial : normalize(mv_tbn[2])) : normal;

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
				// OJO: FO4 hace lo CONTRARIO (su ley vive en sus programas transcriptos): alli el shader agrega un
				// `mov r0.yzw, -r0.yzw` despues del mad (b06 rec1507 t=0x101), o sea samplea con
				// V - 2(N.V)N = reflect(viewDir,N) -- con el
				// MISMO varying superficie->ojo (`add o6.xyz, -r1.xyzx, cb2[7].xyzx`, 18/18 de sus VS).
				// FO4 es antipodal en sus DOS familias (BGSM + BGEM): 205/205 sampleos de cubo llevan ese
				// `mov` final. El raro es FO4, no Skyrim.
				vec3 reflected = reflect(-viewDir, reflNormal);
				vec3 reflectedWS = vec3(matModel * (matModelViewInverse * vec4(reflected, 0.0)));
				// THE SPACE OF THE LOOKUP (chunk C12): the PS samples t4 with R built from N and V in ITS OWN space
				// (dp3 / add / mad above, then sample) -- model space when the technique is not SKINNED (V = cb2[6] -
				// pos, cb2[6] = the camera through the inverse world, 0x14154A646..657), world space when it is
				// (SetupGeometry 0x141549550, technique bit 1 0x1415495A4..5C1) or its type is 1, 11 or 16 (case 0x141549705 clears
				// the flag, 0x14154971E). uSseLitSpace takes an app-WORLD
				// direction to that space (Render.vb SseLitSpace; identity for SKINNED and for those types); its 1/scale does not move
				// a cube lookup.
				vec4 cube = texture(texCubemap, uSseLitSpace * reflectedWS);
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
				// FO4 es OTRO caso (sus programas transcriptos): alli el engine
				// multiplica por cb2[11].y = SpecMult (b06 rec1507 L290). Gate por shader, no por uniform.
				cube.rgb *= envReflection;
				if ((bEnvMask && !bGlowmap) || bSseMultiLayer)
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

				envTerm = cube.rgb;
			}

			// Emissive
			if (bEmissive && !uSseDecalBaseSurface)
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

			// Engine (PS 0x00000201 rec 4030 and the rest): S = (ambient + emissive) + diffuse (+ IBL cb1[8].yzw * cb1[8].x,
			// none in the preview); lit = albedo * S - emissive / glow INSIDE the albedo multiply. The specular joins in the
			// tail (sseLitOutput).
			// The translucent decals' base surface: the preview's background at the pixel (uSseDecalBaseSurface).
			if (uSseDecalBaseSurface)
				albedo = backgroundAt(gl_FragCoord.xy);
			// EYE technique (S-O1): the ambient takes v7 as its normal, not the bump normal (192 of 192 eye PS: `mov r3.xyz, v7.xyzx ;
			// mov r3.w, l(1.0) ; dp4 cb2[11..13], r3`); the preview's hemisphere stands for cb2[11..13] as for every SSE shape.
			vec3 ambientN = (bEye && bEyeRadial && !uSseDecalBaseSurface) ? vEyeRadial : normal;
			vec3 ambientS = hemiAmbient(ambientN);
			vec3 lightSum = outDiffuse + (ambientS + emissive);
			vec3 lit = albedo * lightSum;

			// MULTILAYER PARALLAX (technique type 11; the 624 type-11 PS of the cache, rec 9579 l. 122-143): the inner layer t8, seen
			// through the refracted bump and lit by the lights + ambient (no emissive), over the lit base by saturate(N.V) * (1 - t0.a).
			// Tangent space: Vt = (T.V, B.V, N.V) (l. 124-126), n' = cb1[6].y * (t1 * 2 - (1, 1, 2)) + (0, 0, 1) (l. 127-128),
			// R = reflect(-Vt, n') (l. 129-131), uv8 = v1 * cb1[6].zw + R.xy * (t8(v1).a * cb1[6].x / abs(R.z) / 1024) (l. 122-123,
			// 132-135; the token of l(0.000977) is 0x3A800000 = 1/1024 in 624 / 624 blobs). R is linear in Vt and only R.xy / abs(R.z)
			// is used, so the view-space frame of the app gives the same offset as the PS world frame. The envmap below is its cube.
			// ambientS is the hemisphere on the bump normal here: ambientN only differs on the eye (type 16), never type 11.
			if (bSseMultiLayer && bShowTexture && !uSseDecalBaseSurface)
			{
				vec3 vTs = transpose(mv_tbn) * viewDir;
				vec3 nLayer = uMlpLayer.y * (normalMap.rgb * 2.0 - vec3(1.0, 1.0, 2.0)) + vec3(0.0, 0.0, 1.0);
				vec3 rLayer = reflect(-vTs, nLayer);
				vec2 uvLayer = uv * uMlpLayer.zw + rLayer.xy * (texture(texInnerLayer, uv).a * uMlpLayer.x / abs(rLayer.z) * 0.0009765625);
				vec3 inner = texture(texInnerLayer, uvLayer).rgb * vColor.rgb * (outDiffuse + ambientS);
				lit += clamp(dot(normal, viewDir), 0.0, 1.0) * (1.0 - baseMap.a) * (inner - lit);
			}

			// Hair tint (HairTint PS 0x06010203 rec 8996: `mul r2.xyz, r5.xyzx, r1.xyzx`, r5 = mix(1, cb1[1], COLOR0.y)): the
			// LIT colour only; the specular is added after the tail's first stage, untinted.
			if (bHairTint && !bIsEffectShader && !uSseDecalBaseSurface)
			{
				lit *= mix(vec3(1.0), tintColor, vColor.g);
			}

			// Envmap (1) / Eye (16): the cube term times the same S - ambient + EMISSIVE + diffuse - joins lit BEFORE the
			// tail's first stage and outside the vertex colour (PS 0x01000201 rec 6320: `mul r0.xyz, r0.xyzx, r3.xyzx ;
			// mad r0.xyz, r1.xyzx, v12.xyzx, r0.xyzx`; Eye 0x10000602 rec 10742 the same).
			lit += envTerm * lightSum;

			// OUTPUT TAIL of every SSE lighting PS (6924/6924; Tools/re-docs/RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 2):
			// one stage, or two with the specular between them in the SPECULAR permutations (SseLitTailSource). cb12[42]:
			// Y = 1; Z = sseLitTailZ (0 opaque, 1 translucent group 1). No fog in the preview (user decision, 3-oct-2026).
			// SNOW: no specular term reaches o0 (law (a)); the tail keeps its two stages (bit 9).
			if (bSseSnow)
				outSpecular = vec3(0.0);
			color.rgb = sseLitOutput(lit, outSpecular, 1.0, sseLitTailZ);
			if (bSseSnow)
			{
				// Ng = geoNormal (the TBN normal, declared above in this same block), n = the mapped normal, V = viewDir: dot products are
				// frame-free. Both follow the preview's double-sided policy (1 of the 31 snow shapes is double-sided, 0 is MSN). The
				// preview's light model has no point lights (C5): o3.x = the rim's luminance. pow(0, y > 0) = 0 as the PS's log / mul / exp.
				float snowRim = (uSseSnowRim.w > 0.00001 ? uSseSnowRim.x : 0.0)
					* pow(1.0 - clamp(dot(geoNormal, viewDir), 0.0, 1.0), uSseSnowRim.y)
					* pow(1.0 - clamp(dot(normal, viewDir), 0.0, 1.0), uSseSnowRim.z);
				snowSpecAlphaOut = vec4(dot(vec3(snowRim), vec3(0.3, 0.59, 0.11)), baseMap.a, 0.0, 0.0);
			}
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
			// BLOOD: vColor is 1 without vertex colours, as PS 0x4842 reads none (Vertex_SSE starts vColor at 1 and only bShowVertexColor /
			// bShowVertexAlpha load the stream; SkinningHelper fills a shape without colours with (1, 1, 1, 1)).
			if (bSseBlood)
			{
				float bloodG = baseMap.g * vColor.g * effectBaseColor.g;
				float bloodB = baseMap.b * vColor.b * effectBaseColor.b * vColor.a;
				effAlpha = bloodG * sseEffectPropAlpha;
				float bloodS = clamp(bloodG - uSseBloodParams.x, 0.0, 1.0);
				float bloodK = (bloodS < uSseBloodParams.y) ? (bloodS / uSseBloodParams.y) * bloodB : bloodB;
				float bloodM = 1.0 - bloodK * uSseBloodParams.z;
				effRgb = bloodM * clamp(vec3(2.0 - bloodK, 1.0 - bloodK, 1.0 - bloodK), 0.0, 1.0);
			}
			color = vec4(uSseDecalBaseSurface ? color.rgb : effRgb, effAlpha);
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
			if (bIsEffectShader && !uSseDecalBaseSurface)
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
    // MISMA ESTRUCTURA QUE EL BLOQUE DE FALLOUT 4 (Shader_Class_Fo4EditView), y la ley de cada rama copiada del camino
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
			if (fragColor.a + SSE_L_M0_015686 < 0.0)
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

	// The translucent decals' base: the depth and surface draws keep the fragments the decal paints (uDecalBaseMode); the
	// surface keeps the decal's alpha (its opacity: the base follows the decal's alpha).
	if ((uDecalBaseMode == 1 && fragColor.a <= 0.0) || uDecalBaseMode == 3 || (uDecalBaseMode == 4 && fragColor.a >= 1.0)) discard;

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

    ''' <summary>GATE ONLY (ShadowGate --decal-base-surface mutants): the game shader with another fragment text.</summary>
    Friend Sub New(gateFragment As String)
        MyBase.New(Vertex_SSE, gateFragment)
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
// thrG = cb2[2].x (0x141567EE2..F27) = SseRenderPassLaw.PrepassThrG: 0x3F7EFEFF with blend, else the exe's float32
// ref * 0x3B808081 + 0x3B80802C (+ 0x3B808081 once more when ref == 4).
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
''' (Skyrim SE's pass draws a shape with its game's VS; Fallout 4's is its b07 technique, RenderRefractionNormalsFo4); the two
''' fragments are programs of their own (RefractionPass,
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
" & Gl43.ClipGlsl & "out vec2 vUv;
void main(void)
{
	vec2 p = vec2((gl_VertexID & 1) * 4 - 1, (gl_VertexID >> 1) * 4 - 1);
	vUv = p * 0.5 + 0.5;
	gl_Position = gl43ClipToGl(vec4(p, 0.0, 1.0));
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
// kSNOW_SPECALPHA (RT 0x70, R8G8_UNORM; o3 of the snow PS, 0 elsewhere: cleared 0, 0x1406575EA..678), written by the scene draws
// in GL row order like texScene. sseImprovedSnow = cb2[5].w (EyePosition.w = [0x1420D6CE8]); sseDeactivateAoOnSnow = cb2[8].x.
uniform sampler2D texSnowSpecAlpha;
uniform float sseImprovedSnow;
uniform float sseDeactivateAoOnSnow;
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
	float ao = texture(texSao, vec2(vUv.x, 1.0 - vUv.y)).x;
	if (sseImprovedSnow != 0.0)
	{
		// PS 16004: mad r2.xyz, t4.x, t4.y, r2 ; snow pixel = t4.y > 1e-5 ; ao = min(ao + cb2[8].x, 1). Sparkles: not drawn (notice).
		vec2 snow = texture(texSnowSpecAlpha, vUv).xy;
		c.rgb += vec3(snow.x * snow.y);
		if (snow.y > 0.00001)
			ao = min(ao + sseDeactivateAoOnSnow, 1.0);
	}
	c.rgb *= ao;   // PS 16004 mul r3.xyz, r0.xxxx, r2.xyzx
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
// Q3). Lighting PS: cb0[0].w (0x14154C010), read by sseLitTailStage.
uniform float sseInvFramebufferRange;
// The two clamps of the lighting tail, both written by SetupTechnique 0x141547D20 (0x141548619..656; Render.vb
// SseLightingOutputClamp*): cb0[1].x = fLightingOutputColourClampPostLit, cb0[1].z = fLightingOutputColourClampPostSpec.
uniform float sseLitClampPostLit;
uniform float sseLitClampPostSpec;
// Technique bit 9 SPECULAR (SLSF1 Specular or SLSF2 Multi_Index_Snow, 0x14151A9A2..A9BB, or the snow technique,
// 0x14151AAF0; SseRenderPassLaw.SpecularTechnique): the PS runs the tail in TWO stages, otherwise in ONE (sseLitOutput
// below). The cache also holds two-stage PS for bit 17 AMBIENT_SPECULAR and technique 14; the engine never reaches them
// without bit 9 (SseRenderPassLaw.SpecularTechnique).
uniform bool bSseSpecularTech;
// Z of the tail = cb12[42].z of the list the shape is drawn in: 0 in the opaque world pass (render flags 0x41 / 0x51),
// 1 in the alpha finish's lists 7 (group 9) and 0x10 (group 1, the translucent lighting pass), which are drawn with
// flags | 4 (0x14151F87E..F88C, 0x14151FB80; Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 3). Render.vb
// SseLitTailZ decides it per shape.
uniform float sseLitTailZ;

// The tail of all 6924 SSE lighting PS, in the bytecode's order. One STAGE (sseLitTailStage):
//   t = x - f*k ; m = min(t*Y + C, x)
// x = the colour so far, f = lerp(x, COLOR1.rgb, COLOR1.a) (the fog), Y = cb12[42].y = (flags & 2) ? 0 : 1,
// Z = cb12[42].z = (flags & 4) ? 1 : 0 (0x14100AE80). World render: Y = 1 in every list (bit 1 never set).
// The whole tail (sseLitOutput); lit = albedo * S (+ the envmap term), spec = the accumulated specular:
//   no SPECULAR (PS 0x00000001, rec 4027): out = m(lit, PostLit) - t*Y*Z
//   SPECULAR    (PS 0x00000201, rec 4030): A = m(lit, PostLit), NO Z term ; B = A + spec ;
//                                          out = m(B, PostSpec) - tB*Y*Z
//   (`mad r1, r1, cb12[42].y, cb0[1].x ; min r0, r0, r1 ; mad r0, r3, cb1[4].xyz, r0 ; ... ;
//   mad r1, r1, cb12[42].y, cb0[1].z ; min r0, r0, r1 ; mad r1, -r3, cb12[42].z, r0`.)
vec3 sseLitTailStage(vec3 x, vec3 f, float Y, float C, out vec3 tY)
{
	tY = (x - f * sseInvFramebufferRange) * Y;
	return min(tY + vec3(C), x);
}

// No fog in the preview (user decision, 3-oct-2026): f = the colour of each stage.
vec3 sseLitOutput(vec3 lit, vec3 spec, float Y, float Z)
{
	vec3 tY;
	vec3 a = sseLitTailStage(lit, lit, Y, sseLitClampPostLit, tY);
	if (!bSseSpecularTech)
		return a - tY * Z;
	vec3 b = a + spec;
	vec3 o = sseLitTailStage(b, b, Y, sseLitClampPostSpec, tY);
	return o - tY * Z;
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

// Fallout 4's SOFT is its transcribed b05 PS's (ola 3, Fo4EffectSource: rec1027 L35-49 in all 311 SOFT PS).
"
End Module

''' <summary>THE DISPLAY CURVE OF 2.3.8 (Hable, A..F = .15/.50/.10/.20/.02/.30) and its lit tail, in ONE place (each
''' fragment keeps its own effect tail, since 2.3.8 treated the effect differently per game). It is NOT the
''' engine: the game tonemaps in its post (PostProcess.vb). It is the law of the frames that do not go through
''' that post - the floor of a direct frame and the post-off tail of Fragment_SSE and of Fallout 4's direct resolve
''' (Fo4DeferredTargets) - and it is
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

''' <summary>El fragment del pase de profundidad del shadow map de SKYRIM SE (Shadow_Depth_Shader_SSE): lo unico que hace es el
''' alpha-test, con la cantidad del pase iluminado de Skyrim SE. Fallout 4 no pasa por aca: dibuja sus mapas con la tecnica
''' utilitaria del motor (b07, RenderableMesh.RenderShadowMapFo4, ola 4).
''' <para>NO LLEVA VERTEX SHADER PROPIO, y eso es el punto. El blend de skinning tiene CINCO sitios
''' gemelos declarados en el SYNC de Vertex_SSE; un VS de sombra seria el SEXTO, y desincronizarlo no
''' rompe el build ni tira: deja la sombra de una malla posada en la posicion de bind. En vez de eso el
''' pase reusa Vertex_SSE tal cual y le entrega matView = lightView y matProjection =
''' lightOrtho, con lo que su `gl_Position = matProjection * (matModelView * pos)` ya cae en clip-space
''' de la luz. El VS calcula ademas varyings que este fragment no consume (TBN, direcciones de luz):
''' es legal en GLSL y es el precio de no duplicar el skinning.</para></summary>
Friend Module ShadowDepthShaderSource

    ''' <summary>VIEW-space direction -&gt; WORLD (needs matModel and matModelViewInverse declared before it). ONE copy for every
    ''' fragment that takes the shadow lookup's normal or the hemisphere to world: Fragment_SSE.</summary>
    Friend Const WorldDirGlsl As String = "vec3 toWorldDir(in vec3 v)
{
	return normalize(vec3(matModel * (matModelViewInverse * vec4(v, 0.0))));
}
"

    ''' <summary>UNA SOLA DEFINICION del lookup de sombra, concatenada dentro de los DOS fragments que
    ''' la usan (Fragment_SSE y el receptor de suelo; los receptores de Fallout 4 son sus luces transcriptas). Antes
    ''' estaba copiada en un fragment de FO4 y en el de SSE: dos
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
	p.xy = p.xy * 0.5 + 0.5;   // NDC xy -> [0,1]; z is already the [0,1] depth (EngineCameraLaw.Orthographic, ZERO_TO_ONE)

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

    ''' <summary>CONTRATO DE SINCRONIA CON EL PASE ILUMINADO DE SKYRIM SE.
    ''' <para>El contrato COMPLETO —que cambiar, en que direccion y por que— esta escrito DENTRO del GLSL,
    ''' arriba de todo, en <c>Fragment_SSE</c> y en este fragment: ahi lo ve quien edita la logica, que es donde tiene que
    ''' estar. Los dos se referencian entre si.</para>
    ''' <para>En una linea: <b>el pase de sombra decide QUE FRAGMENTO EXISTE con la misma ley de alpha que
    ''' el pase iluminado.</b> Tocar uno sin el otro rompe la silueta de la sombra sin que nada lo reporte.</para>
    ''' <para>El GLSL va en ASCII PURO y SIN COMILLAS DOBLES (vive en un <c>Const String</c> de VB: una
    ''' comilla cierra el literal). El gate <c>glsl-ascii</c> lo cubre.</para></summary>
    Friend Const Fragment_ShadowDepth As String = "
#version 430

// ##################################################################################################
// ####  SHADOW SYNC CONTRACT -- THIS PASS MIRRORS THE ALPHA LAW OF SKYRIM SE'S RENDER SHADER  ######
// ##################################################################################################
//
//   THE OTHER HALF OF THIS CONTRACT LIVES IN:
//   ==>  Shader_Class_SSE.Fragment_SSE   (Skyrim SE lit pass)
//   It carries the same contract pointing back here. CHANGING IT WITHOUT CHANGING THIS FILE IS A
//   BUG, even if everything still compiles and every gate stays green.
//   Skyrim SE only: Fallout 4 draws its shadow maps with the engine's utility technique (b07,
//   RenderableMesh.RenderShadowMapFo4), whose alpha test is its transcribed program's.
//
// WHAT THIS PASS IS. It writes the shadow map, so it decides WHICH FRAGMENT EXISTS. It does not
// shade anything: the ONLY thing it computes is whether a fragment survives, and that has to be
// decided with EXACTLY the quantity the lit pass tests. When the two disagree, the shadow
// silhouette stops matching the object that is drawn -- shadow for geometry discarded on screen,
// or no shadow for geometry that is visible.
//
// WHAT REACHES IT: opaque LIGHTING shapes only, as in the engine. Skyrim SE draws no effect (.bgem)
// into a shadow map (slot 0x2B builds a pass only in mode 0xC, 0x14152A0C2), nor a lighting shape that
// blends, whose alpha is below 1 or that refracts (slot 0x2B 0x14151B3C3..405), nor a decal (a declared
// hole, kept out as before). The caster filter keeps them out (SseRenderPassLaw.ShadowMapCasts), so
// there is no effect branch and no translucent path here: a fragment passes a hard threshold or it is
// discarded.
//
// THE QUANTITY: the Skyrim SE lit pass multiplies the material alpha scalar in BEFORE its test
// (cb2[3].z). Mirrored here on purpose (chunk C11, DECISIONES 1), although the SSE SHADOWMAP PS
// tests t0.a [* vc] without it.
//
// WHAT MUST BE MIRRORED, in both directions:
//   * the alpha test quantity (NOT its threshold: Render.vb uploads it per pass --
//     SseRenderPassLaw.ShadowAlphaTest, gate sse-pass-law)
//   * the vertex alpha and the predicate that gates it
//   * the material alpha scalar, and WHERE it is applied
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
uniform float uMaterialAlpha;  // the material Alpha scalar: it enters the tested quantity (Skyrim SE's lit law)
uniform bool bShowTexture;
uniform bool bApplyZap;

in vec4 vColor;
in vec2 vUV;
flat in int ZappedVert;

void main(void)
{
	if (bApplyZap && ZappedVert == 1)
		discard;

	if (bAlphaTest)
	{
		vec2 uv = vUV * uvScale + uvOffset;
		// THE TEXTURE FACTOR WITH THE TEXTURE TURNED OFF. The lit pass does NOT gate its alpha test with
		// bShowTexture (its `if (bAlphaTest)` is a sibling of `if (bShowTexture)`, not a child), and
		// `color.a *= baseMap.a` lives INSIDE that if: with the toggle off the factor is 1.0. Gating this
		// block by bShowTexture made the shadow keep the whole card of a shape the lit pass still cuts.
		float dTexA = bShowTexture ? texture(texDiffuse, uv).a : 1.0;
		// SAME left-hand side as the lit pass: Skyrim SE multiplies the material scalar in BEFORE its test.
		float aTex = dTexA * vColor.a * uMaterialAlpha;
		// CUTOUT: a hard threshold, strict `<` so equality is KEPT.
		if (aTex < alphaThreshold)
			discard;
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

    Friend Const Vertex_Ground As String = "
#version 430
" & Gl43.ClipGlsl & "layout(location = 0) in vec2 aCorner;   // unit quad, [-1..1] on both axes

uniform mat4 matViewProj;      // camera view * projection (same convention as the rest of the render)
uniform vec3 uGroundCenter;    // world-space centre of the catcher
uniform vec2 uGroundHalf;      // world-space half-extents, per axis

out vec3 gWorldPos;
out vec2 gLocal;

void main(void)
{
	gLocal = aCorner;
	gWorldPos = uGroundCenter + vec3(aCorner * uGroundHalf, 0.0);
	gl_Position = gl43ClipToGl(matViewProj * vec4(gWorldPos, 1.0));
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

''' <summary>Programa de profundidad de Skyrim SE: Vertex_SSE + el fragment de sombra (Fallout 4 dibuja sus mapas con la
''' tecnica utilitaria del motor, RenderableMesh.RenderShadowMapFo4).</summary>
Public Class Shadow_Depth_Shader_SSE
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(Shader_Class_SSE.Vertex_SSE, ShadowDepthShaderSource.Fragment_ShadowDepth)
    End Sub
End Class

''' <summary>Las vistas de depuracion del fragment shader. Cada valor es el numero que el bloque
''' <c>DEBUG MODE</c> compara adentro del GLSL, en los DOS juegos: <c>Shader_Class_Fo4EditView.Fragment_Fo4EditView</c> (linea del
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
#If DEBUG Then
                PreviewControl.GateProg(Me.GetType().Name, False)
#End If
                program = 0
            End If
        End If
        disposedValue = True
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(disposing:=False)
        MyBase.Finalize()
    End Sub

    ''' <param name="TransformFeedbackVaryings">GATE ONLY (ShadowGate G5b): the outputs the program captures by transform feedback,
    ''' interleaved (OpenGL 4.6 11.1.2.1: declared before the link).</param>
    Public Sub New(VertexShaderSource As String, FragmentShaderSource As String, Optional TessControlSource As String = Nothing,
                   Optional TessEvaluationSource As String = Nothing, Optional TransformFeedbackVaryings As String() = Nothing)
        Dim stages As New List(Of Integer) From {CompileShader(ShaderType.VertexShader, VertexShaderSource),
                                                 CompileShader(ShaderType.FragmentShader, FragmentShaderSource)}
        ' Ola 2: the engine's HS / DS of a tessellated technique (0x14181D4A0 binds them; topology 0x23, 0x14181D60E).
        If TessControlSource IsNot Nothing Then stages.Add(CompileShader(ShaderType.TessControlShader, TessControlSource))
        If TessEvaluationSource IsNot Nothing Then stages.Add(CompileShader(ShaderType.TessEvaluationShader, TessEvaluationSource))
        program = GL.CreateProgram()
#If DEBUG Then
        PreviewControl.GateProg(Me.GetType().Name, True)
#End If
        For Each s In stages : GL.AttachShader(program, s) : Next
        If TransformFeedbackVaryings IsNot Nothing Then GL.TransformFeedbackVaryings(program, TransformFeedbackVaryings.Length, TransformFeedbackVaryings, TransformFeedbackMode.InterleavedAttribs)
        GL.LinkProgram(program)

        Dim linkStatus As Integer
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, linkStatus)
        If linkStatus <> CInt(All.True) Then
            Dim linkInfo = GL.GetProgramInfoLog(program)
            Throw New Exception($"Shader program link error: {linkInfo}")
        End If

        For Each s In stages
            GL.DetachShader(program, s)
            GL.DeleteShader(s)
        Next
        HasTessellation = TessControlSource IsNot Nothing
    End Sub
    ''' <summary>The program draws GL_PATCHES of 3 control points (D3D11_PRIMITIVE_TOPOLOGY 0x23).</summary>
    Public ReadOnly Property HasTessellation As Boolean

    ''' <summary>A compute program (one stage), with the same compile/link checks and uniform API.</summary>
    Protected Sub New(computeShaderSource As String)
        Dim computeShader = CompileShader(ShaderType.ComputeShader, computeShaderSource)
        program = GL.CreateProgram()
#If DEBUG Then
        PreviewControl.GateProg(Me.GetType().Name, True)
#End If
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
        Gl43.ShaderSource(shader, source)
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
        Gl43.UseProgram(program)
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
    ''' del JUEGO ACTIVO. Como el programa de FO4 y <c>Shader_Class_SSE</c> son dos instancias
    ''' distintas, cada una con su propio campo, cambiar de juego dejaba la vista atras sin decir nada.
    ''' Compartida, la eleccion vale para los dos, que es lo que el combo promete.</para>
    ''' <para>NO SE PERSISTE, y es deliberado: no vive en <c>Config_App</c> ni en ningun archivo. Arranca
    ''' en <see cref="ShaderDebugView.None"/> en cada corrida y el boton "Reset rendering to defaults" la
    ''' devuelve a None. Es una ayuda de inspeccion, no un ajuste del usuario.</para>
    ''' <para>ALCANCE, MEDIDO Y NO SUPUESTO. Lo leen <c>ApplyMaterial</c> (el uniform <c>DebugMode</c>),
    ''' <c>PreviewModel.FrameUsesFo4Deferred</c> (un cuadro FO4 con vista de depuracion no es el diferido: lo dibuja
    ''' <c>Shader_Class_Fo4EditView</c>) y <c>PreviewControl.FramePostOn</c> (sin post), todos bajo <c>RenderScene</c>
    ''' —su unico camino de entrada—, que no se llama desde ningun camino de bake. EL BAKE NO PASA POR ACA: <c>BakeAllRunner</c> crea un <c>PreviewControl</c> solo
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
    ''' gateado en los call-sites de Render.vb (MaterialColor, la rama de Fallout 4).</summary>
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

    Public Sub SetVector4Array(name As String, values As Vector4())
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc = -1 OrElse values Is Nothing OrElse values.Length = 0 Then Exit Sub
        Dim f(values.Length * 4 - 1) As Single
        For i = 0 To values.Length - 1
            f(4 * i) = values(i).X : f(4 * i + 1) = values(i).Y : f(4 * i + 2) = values(i).Z : f(4 * i + 3) = values(i).W
        Next
        GL.Uniform4(loc, values.Length, f)
    End Sub

    ''' <summary>A uvec4 array uniform (glUniform4uiv): <paramref name="words"/> = 4 words per element, kept bit for bit (ola 2b:
    ''' BoneTintIndices, the engine's integer rows; decision of 13-oct-2026: a vec4 upload does not keep every bit pattern).</summary>
    Public Sub SetUVector4Array(name As String, words As UInteger())
#If DEBUG Then
        ' rev-B12: the caller's words are the closed rows of Fo4GBufferConstants (16 words): checked in Debug only
        If words Is Nothing OrElse words.Length = 0 OrElse words.Length Mod 4 <> 0 Then Throw New ArgumentException("4 words per uvec4", NameOf(words))
#End If
        Dim loc As Integer = GetUniformLocationCached(name)
        If loc <> -1 Then GL.Uniform4(loc, words.Length \ 4, words)
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
