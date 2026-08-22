Imports System.Text.Json
Imports System.Text.Json.Nodes

''' <summary>
''' RaceMenu (skee64) .jslot sidecar preset — the SSE face-edit data that does NOT live in the ESP NPC record:
''' per-vertex head SCULPT, NiOverride CUSTOM morphs (CME_/EFM_), tint/overlay layers (tintInfo), face texture
''' overrides, node transforms. Format reverse-read from real .jslot files (RaceMenu preset collection):
'''   { actor{hairColor,headTexture,weight}, headParts[{formId,formIdentifier,type}], faceTextures[{index,texture}],
'''     morphs{ default{morphs[19 floats = NAM9 sliders], presets}, custom[{name,value}], sculpt[perPart{data[[idx,dx,dy,dz]]}], sculptDivisor },
'''     tintInfo[{color(uint32 ARGB),index,texture}], transforms[...], mods[], modNames[], version{...} }
'''
''' Read/write is round-trip faithful: unknown/verbatim nodes (transforms, version, mods) are preserved as raw
''' JSON so a load→save cycle doesn't drop data. Sculpt deltas are integers scaled by <see cref="SculptDivisor"/>.
''' </summary>
Public NotInheritable Class RaceMenuJslot
    Public Class JslotTint
        Public Property Color As UInteger   ' ARGB packed
        Public Property Index As Integer
        Public Property Texture As String
    End Class
    Public Class JslotFaceTexture
        Public Property Index As Integer
        Public Property Texture As String
    End Class
    Public Class JslotHeadPart
        Public Property FormId As UInteger
        Public Property FormIdentifier As String
        Public Property Type As Integer
    End Class

    ''' <summary>La tabla <c>mods</c> del archivo decodificada: <b>partial index del load order del AUTOR</b>
    ''' → nombre de plugin. Es la ÚNICA forma de interpretar un <c>headParts[].formId</c>, cuyo byte alto es
    ''' un slot de la sesión que escribió el preset y no significa nada en la nuestra.
    ''' <para>skee la escribe con <c>ModInfo::GetPartialIndex</c> (PresetInterface.cpp:361,396-401) y la usa
    ''' para exactamente esto al leer (:992-997): <c>modList.find(...)</c> → <c>LookupModByName</c> →
    ''' <c>modInfo-&gt;GetFormID(formId)</c>, o sea re-encodear con el índice ACTUAL del mod.</para>
    ''' <para>Es de SÓLO LECTURA: el nodo <c>mods</c> se sigue round-trippeando verbatim desde
    ''' <see cref="_raw"/>, así que decodificarlo no cambia un byte del archivo re-guardado.
    ''' <c>modNames</c> NO sirve para esto: es una lista de nombres sin índice (skee sólo la usa para
    ''' <c>presetData-&gt;modList</c>, :970-974), así que no puede traducir un slot.</para></summary>
    Public Property ModIndexToName As New Dictionary(Of UInteger, String)
    Public Class JslotCustomMorph
        Public Property Name As String
        Public Property Value As Double
    End Class
    ''' <summary>Per-vertex sculpt for one head part: parallel arrays index/dx/dy/dz (raw integers; divide by
    ''' <see cref="RaceMenuJslot.SculptDivisor"/> for the world delta). <see cref="Host"/> is the head-part
    ''' chargen .tri the block targets (RaceMenu's per-shape "host" — e.g. FemaleHeadBrowsCharGen.tri) and
    ''' <see cref="Vertices"/> its vertex count; together they route the block to the right rendered shape
    ''' (each preset sculpts head + brows + eyes + mouth as SEPARATE blocks).</summary>
    Public Class JslotSculptPart
        Public Property Indices As New List(Of Integer)
        Public Property Dx As New List(Of Integer)
        Public Property Dy As New List(Of Integer)
        Public Property Dz As New List(Of Integer)
        Public Property Host As String = ""
        ''' <summary>Vertex count of the sculpt host. Long, not Integer: RaceMenu writes 4294967295 (0xFFFFFFFF)
        ''' for a block whose host it could not size, which overflows a signed 32-bit read.</summary>
        Public Property Vertices As Long = 0
        ''' <summary>The source preset carried a "vertices" key. Distinguishes "absent" from "zero".</summary>
        Public Property HadVertices As Boolean = False
        ''' <summary>The source preset carried a "data" key for this block (possibly an empty array). Kept so a
        ''' load→save round-trips a delta-less sculpt block exactly as it was written.</summary>
        Public Property HadData As Boolean = False
    End Class
    ''' <summary>One keyed contribution to a body morph slider (RaceMenu/BodySlide). A body morph
    ''' name accumulates one entry per BodySlide preset/source that touched it; the engine nets
    ''' (sums) the per-key values (skee64 BodyMorphInterface.h:70-75).</summary>
    Public Class JslotBodyMorphKey
        Public Property Key As String
        Public Property Value As Single
    End Class
    ''' <summary>A RaceMenu body morph: a named slider carrying one or more keyed contributions.
    ''' Schema: skee64 PresetInterface.cpp:655-666.</summary>
    Public Class JslotBodyMorph
        Public Property Name As String
        Public Property Keys As New List(Of JslotBodyMorphKey)
    End Class

    ''' <summary>Nombre de key con el que el motor absorbe la forma LEGACY <c>{name, value}</c> de un body morph:
    ''' <c>presetData-&gt;bodyMorphData[name]["RSMLegacy"] = value</c> (skee64 PresetInterface.cpp:1220).
    ''' Se usa al leer para que ese aporte no se pierda al re-serializar.</summary>
    Friend Const SkeeLegacyMorphKey As String = "RSMLegacy"

    ''' <summary>One RaceMenu body overlay ("tattoo") — a decoded <c>overrides</c> node whose name matches
    ''' the skee64 overlay node convention (<c>Body/Hands/Feet [Ovl{n}]</c> or the <c>[SOvl{n}]</c> skin
    ''' variant, OverlayInterface.h:23-46). Unlike the FO4 f4ee overlay (a catalog template id), a RaceMenu
    ''' overlay carries DIRECT texture paths + an optional tint; there is no template. Decoded from the
    ''' <c>values</c> array per the §3.1 table (skee64 OverrideVariant.h:31-69 / PresetInterface.cpp:601-617,
    ''' :1160-1173):
    '''   {key:9,type:2,index:0}=diffuse path · {key:9,type:2,index:1}=normal path ·
    '''   {key:7,type:3,index:-1}=tint from a signed 0xAARRGGBB int. TextureSet (key 6) is not serialized.</summary>
    Public Class JslotOverlayNode
        ''' <summary>The full skee64 node name, e.g. "Body [Ovl0]". Kept verbatim so the render can map it
        ''' to the target biped slot (Body/Hands/Feet) and Save re-emits it byte-faithfully.</summary>
        Public Property NodeName As String

        ''' <summary>El "magic flag": este overlay vive en el pool SPELL de skee (<c>… [SOvl{n}]</c>) en vez del
        ''' normal (<c>… [Ovl{n}]</c>).
        ''' <para>ES DERIVADO DEL NOMBRE A PROPÓSITO, no un campo aparte. El nombre del nodo es la IDENTIDAD del
        ''' override en las TRES representaciones (el store de skee, el co-save y el array <c>overrides</c> del
        ''' <c>.jslot</c>): un bool guardado al lado podría contradecirlo, y entonces habría dos verdades y un bug
        ''' esperando. Cambiar el flag = renombrar el nodo (lo hace el editor eligiendo un índice libre del pool
        ''' destino), y así el round-trip, el Clone, el sidecar y el copy/paste lo llevan GRATIS.</para>
        ''' <para>Semántica completa (qué difiere entre los pools, medido) en
        ''' <see cref="SseOverlayCompositor.IsSpellOverlayNodeName"/>, que es también la ÚNICA implementación del
        ''' test: acá se delega en vez de repetir el <c>IndexOf</c>. Dos copias del mismo predicado es justo lo que
        ''' ya se pagó caro con "¿es de cara?" (cinco caminos, no todos de acuerdo).</para></summary>
        Public ReadOnly Property IsSpell As Boolean
            Get
                Return SseOverlayCompositor.IsSpellOverlayNodeName(NodeName)
            End Get
        End Property
        Public Property DiffusePath As String
        Public Property NormalPath As String
        ''' <summary>Tint RGBA, each 0..1. Only meaningful when <see cref="HasTint"/> (a missing TintColor
        ''' override leaves the material's own base color). A=opacity (skee treats A=0 as opaque/unspecified).</summary>
        Public Property TintR As Single
        Public Property TintG As Single
        Public Property TintB As Single
        Public Property TintA As Single
        Public Property HasTint As Boolean
        ''' <summary>skee64 <c>kParam_ShaderAlpha</c> (key 8, float — OverrideVariant.h:41). This is the
        ''' overlay's OPACITY; it is a separate override from the tint colour's alpha byte. Every overlay in
        ''' a RaceMenu-authored preset carries one, so it must round-trip or the overlay reloads fully opaque.</summary>
        Public Property Alpha As Single = 1.0F
        Public Property HasAlpha As Boolean

        ''' <summary>El <c>index</c> con el que vinieron el tint (key 7) y el alpha (key 8) en el archivo, para
        ''' re-emitirlos EN SU LUGAR. −1 es lo que escribe RaceMenu y el default de un overlay creado acá.
        ''' <para>NO es decoración: el decode reconocía la key mirando sólo <c>(key,type)</c> mientras el encode
        ''' parcheaba con <c>index := -1</c> cableado, así que un value que viniera en otro índice NO se encontraba
        ''' y se APENDABA uno nuevo — quedaban los dos. Y no queda en empate: skee guarda los overrides en un
        ''' <c>std::set</c> ordenado por <c>(key,index)</c> (OverrideVariant.h:19) y los aplica en ese orden
        ''' (OverrideSet::Visit, OverrideInterface.cpp:1200-1206), así que el nuestro se aplica PRIMERO y el viejo
        ''' lo pisa DESPUÉS. El archivo muestra una opacidad y el juego rinde otra.</para>
        ''' <para>Medido sobre los 48 presets reales: 38 key-8 en index −1 y 4 en index 0. RaceMenu escribe −1,
        ''' pero el índice distinto existe, así que el modelo tiene que poder representarlo.</para></summary>
        Public Property TintIndex As Integer = -1
        Public Property AlphaIndex As Integer = -1
        ''' <summary>The verbatim original <c>values</c> array of this overlay node; Save patches the modeled keys
        ''' (tint 7, alpha 8, diffuse 9/0, normal 9/1) into a clone of it and leaves every UNMODELED entry untouched
        ''' (extra texture slots ≥2, key 6 TextureSet, keys 0-5) — so those round-trip instead of being dropped.
        ''' Nothing for a UI-created overlay (Save builds a fresh values array from the modeled fields).</summary>
        Friend RawValues As JsonNode

        ''' <summary>Deep-clone (detaches RawValues JSON). Public so cross-assembly carriers copy the overlay,
        ''' including its unmodeled-key preservation, without touching the Friend RawValues directly.</summary>
        Public Function Clone() As JslotOverlayNode
            Return New JslotOverlayNode With {
                .NodeName = NodeName, .DiffusePath = DiffusePath, .NormalPath = NormalPath,
                .TintR = TintR, .TintG = TintG, .TintB = TintB, .TintA = TintA, .HasTint = HasTint,
                .Alpha = Alpha, .HasAlpha = HasAlpha,
                .TintIndex = TintIndex, .AlphaIndex = AlphaIndex,
                .RawValues = If(RawValues Is Nothing, Nothing, JsonNode.Parse(RawValues.ToJsonString()))}
        End Function
    End Class

    ''' <summary>actor.hairColor — packed 0xRRGGBB (skee64 PresetInterface.cpp:677 red&lt;&lt;16|green&lt;&lt;8|blue). This
    ''' is an ABSOLUTE hair tint the preset carries, NOT a CLFM ref; skee writes it straight onto the hair shape's
    ''' BSLightingShaderMaterialHairTint.tintColor. See <see cref="HadHairColor"/> for present-vs-absent.</summary>
    Public Property HairColor As Integer
    ''' <summary>True when the loaded actor block carried a hairColor key (0 is a valid black override, so a plain
    ''' <see cref="HairColor"/>=0 is ambiguous without this).</summary>
    Public Property HadHairColor As Boolean
    ''' <summary>True cuando el <c>.jslot</c> traía la key <c>actor.weight</c>. GEMELO de
    ''' <see cref="HadHairColor"/> y por la MISMA razón, que a este campo no se le había aplicado.
    ''' <para>Sin esto, un preset que NO trae <c>weight</c> le pisaba el peso del NPC con <b>0</b>:
    ''' <c>GetDbl(Nothing)</c> devuelve 0.0, el mapper asignaba <c>SseWeight</c> incondicionalmente y
    ''' <c>Save</c> emitía la key siempre. Encontrado probando en el juego: "body weight 0 también".</para></summary>
    Public Property HadWeight As Boolean
    Public Property HeadTexture As String
    Public Property Weight As Double
    Public Property HeadParts As New List(Of JslotHeadPart)
    Public Property FaceTextures As New List(Of JslotFaceTexture)
    Public Property SliderMorphs As New List(Of Single)          ' morphs.default.morphs (= NAM9 sliders)
    Public Property CustomMorphs As New List(Of JslotCustomMorph) ' morphs.custom (CME_/EFM_ NiOverride)
    Public Property Sculpt As New List(Of JslotSculptPart)        ' morphs.sculpt (per head part)
    Public Property SculptDivisor As Integer = 10000
    Public Property TintInfo As New List(Of JslotTint)
    ''' <summary>RaceMenu body morphs (BodySlide `.tri` sliders), each a name with keyed contributions.
    ''' Flatten via <see cref="BodyMorphsToFlatSliderDict"/> for the render slider dict.</summary>
    Public Property BodyMorphs As New List(Of JslotBodyMorph)
    ''' <summary>RaceMenu body overlays (tattoos) decoded from the top-level <c>overrides</c> array.
    ''' Only nodes whose name matches the skee64 overlay convention are modeled here; other override
    ''' nodes are preserved verbatim (<see cref="_otherOverridesRaw"/>) and re-emitted unchanged on Save.</summary>
    Public Property Overlays As New List(Of JslotOverlayNode)

    ''' <summary>LA KEY CON LA QUE ESTA APP AUTORA SUS NODE TRANSFORMS. Un node transform de NiOverride está
    ''' KEYEADO POR NOMBRE: el nodo puede tener varias capas y cada contribuyente escribe la suya (los sliders de
    ''' RaceMenu/XPMSE usan <c>RMX_*</c>), y el motor las COMPONE — <c>combinedTransform = combinedTransform *
    ''' localTransform</c> sobre TODAS (NiTransformInterface.cpp:675-681). Es el mismo mecanismo keyed de los body
    ''' morphs.
    ''' <para>GEMELO DE <c>XformKey()</c> EN <c>NPCM_Manolov_ApplySSE.psc</c>: el apply-script escribe y borra
    ''' con esa misma string. Si cambia acá hay que cambiarla en el <c>.psc</c> y recompilar el <c>.pex</c>, o el
    ''' script barrería una capa distinta de la que el archivo declara (lo gatea <c>check_sweep_ceiling.py</c>).</para>
    ''' <para>La key existe para que RaceMenu, XPMSE y nosotros podamos tener un valor sobre el MISMO hueso sin
    ''' pisarnos, y para que <c>RemoveNodeTransform*</c> saque SÓLO la nuestra — que es lo único que nos deja
    ''' deshacer lo que escribimos.</para>
    ''' <para>⛔ NO agregar al <c>.psc</c> un "reclamo" del nodo que borre las capas ajenas de los huesos
    ''' autorados: eso confunde las capas de un PRESET (el desglose por slider de un autor, que vive en un
    ''' archivo y nunca llega solo a un NPC) con las de un ACTOR en runtime (de mods distintos). Sobre un NPC
    ''' real las únicas ajenas son <c>internal</c> del motor —donde componer ES correcto: el NPC con tacos
    ''' tiene que levantarse— y los nodos de arma de XPMSE, que vuelven al próximo cambio de arma.</para>
    ''' <para>El residuo que eso deja, dicho: nuestro valor es el TOTAL del hueso, así que si el actor ya tiene
    ''' otro aporte ahí, el motor los compone y el juego muestra más de lo que muestra la app.</para>
    ''' <para>SU ALCANCE NO ES "LA CAPA DE TRANSFORMS", y por eso NO se llama AppTransformKey: en el <c>.psc</c>
    ''' la MISMA <c>XformKey()</c> se usa también como key de los BODY MORPHS (<c>SetBodyMorph</c>) y en el barrido
    ''' de <c>RemovePrevious</c>. Renombrarla o cambiarla "porque es la de transforms" rompería los morphs.</para></summary>
    Public Const AppOverrideKey As String = "NPCM_Manolov"

    ''' <summary>One RaceMenu NiOverride node transform (NiTransformInterface) — the app's model of one BONE,
    ''' not of one contributor: scale (key 30), position (31), rotation (32) and scaleMode (33) over a named node.
    ''' <para>⚠️ El decode COMPONE todas las capas del nodo con la fórmula completa de
    ''' <c>NiTransform::operator*</c> y el encode reescribe UNA sola capa nuestra, así que un load→save de un
    ''' preset multi-capa NO es byte-idéntico A PROPÓSITO: es la colapsada. Ver <see cref="AppOverrideKey"/> y
    ''' el decode de <c>transforms</c>.</para>
    ''' Schema: skee64 <c>PresetInterface.cpp:559-593</c>.</summary>
    Public Class JslotNodeTransform
        Public Property NodeName As String
        ''' <summary>Escala uniforme del nodo (kParam_NodeTransformScale = key 30, float @ índice 0). 1.0 = sin
        ''' escalar.
        ''' <para>ES EL VALOR **EFECTIVO**, no el crudo de una capa: cuando el nodo traía varias capas nombradas, el
        ''' decode las compuso (la escala es el PRODUCTO) y esto es el resultado. Es también lo que se escribe: una
        ''' sola capa nuestra con el total. Ver <see cref="AppOverrideKey"/>.</para></summary>
        Public Property Scale As Single = 1.0F
        Public Property HasScale As Boolean
        ''' <summary>Node-local translation (kParam_NodeTransformPosition = key 31), game units, x/y/z at value
        ''' index 0/1/2 (skee64 NiTransformInterface.cpp:761-779). Decoded/encoded as plain floats → exact
        ''' round-trip; this is the real CME-node placement RaceMenu writes (e.g. CME Neck head offset).</summary>
        Public Property PosX As Single
        Public Property PosY As Single
        Public Property PosZ As Single
        Public Property HasPosition As Boolean
        ''' <summary>Node rotation (kParam_NodeTransformRotation = key 32) as an AXIS-ANGLE vector in radians —
        ''' the "BS rotation" form the render already consumes (<see cref="Transform_Class.BSRotationToMatrix33"/>).
        ''' The .jslot stores it as a 3×3 matrix (9 floats, row-major, value index 0..8, skee64
        ''' NiTransformInterface.cpp:791-838); we decode it via <see cref="Transform_Class.Matrix33ToBSRotation"/>
        ''' and re-encode via <see cref="Transform_Class.BSRotationToMatrix33"/> (an exact log/exp pair). Only
        ''' re-encoded when <see cref="RotationDirty"/> is set (a UI edit), so an untouched rotation stays
        ''' byte-exact from <see cref="Raw"/> (matrix→axis-angle→matrix would otherwise drift ~1 ULP).</summary>
        Public Property RotX As Single
        Public Property RotY As Single
        Public Property RotZ As Single
        Public Property HasRotation As Boolean
        ''' <summary>skee ScaleMode (kParam_NodeTransformScaleMode = key 33, int @ index 0): 0 multiplicative /
        ''' 1 average / 2 additive / 3 max (skee64 NiTransformInterface.cpp:682-707). Preserved for round-trip;
        ''' default 0. Se ACARREA, no se interpreta.
        ''' <para>⚠️ El modo IMPORTA aunque haya UNA sola capa: <c>fScaleValue</c> arranca en <b>1.0</b>
        ''' (NiTransformInterface.cpp:655), así que con una capa de
        ''' escala <i>s</i> el motor rinde <i>s</i> en modo 0, <i>(1+s)/2</i> en el 1, <i>1+s</i> en el 2 y
        ''' <i>max(1,s)</i> en el 3 (:682-706) — sólo coinciden si <i>s</i> = 1. El residuo real es el
        ''' <c>iScaleMode</c> del JUGADOR, que es global, y la key 33 por nodo NO lo arregla porque el motor nunca
        ''' la lee (busca <c>(33,-1)</c> y todo se guarda en <c>(33,0)</c>).</para></summary>
        Public Property ScaleMode As Integer
        Public Property HasScaleMode As Boolean
        ''' <summary>Set by a UI rotation edit; gates whether Save rebuilds the key-32 matrix from the modeled
        ''' axis-angle (avoids matrix→axis-angle→matrix float drift for rotations the user never touched). Public so
        ''' the app's Edit Body rotation sliders (a separate assembly) can flag an edit.</summary>
        Public Property RotationDirty As Boolean
        ''' <summary>The verbatim original transform element ({firstPerson, node, keys}); Save re-emits it with the
        ''' modeled scale/position (and rotation when dirty) patched in and every other key untouched.
        ''' Nothing for a UI-created transform (Save builds a fresh element from the modeled fields).
        ''' <para><b>Public</b>, no <c>Friend</c>: el sidecar vive en otra assembly y tiene que PERSISTIRLO. Es lo
        ''' que hace que sobrevivan al cerrar-y-reabrir las cosas que la app no modela — la key 40 (re-parenteo), la
        ''' key 33, y cualquier value que RaceMenu agregue mañana. Si no se persiste,
        ''' <see cref="BuildTransformRaw"/> reconstruye el elemento desde los campos modelados y todo eso
        ''' desaparece del <c>.jslot</c> re-exportado.</para></summary>
        Public Raw As JsonNode
        ' La rotación se reescribe SIEMPRE que el nodo tenga rotación, igual que la escala y la posición: no
        ' hace falta ningún flag de "vino de otra capa". El byte-exacto lo garantiza `RotationRowMajor`,
        ' que devuelve los mismos 9 floats cuando hay matriz cruda sin editar.
        ''' <summary>La matriz de rotacion COMPUESTA, cruda (9 floats row-major), tal como se va a re-emitir.
        ''' <para>Existe para NO pasar por axis-angle al escribir: esa vuelta pierde los casos degenerados (180
        ''' grados y reflexiones). El axis-angle del modelo sigue siendo lo que consumen el render y la UI; esto es
        ''' solo para el archivo. Nothing cuando la rotacion la genero una edicion de la UI (ahi el axis-angle ES la
        ''' fuente y hay que reconstruir la matriz).</para>
        ''' <para>Es <c>Public</c> y NO <c>Friend</c> porque el sidecar <c>.bssliders</c> vive en la OTRA
        ''' assembly y tiene que PERSISTIRLO: sin eso la matriz se pierde en cuanto el usuario guarda el NPC y
        ''' reabre la app, que es justo el caso que este campo existe para cubrir. Un campo que tiene que
        ''' sobrevivir a la serialización no puede ser <c>Friend</c>.</para>
        ''' <para>Es un array y se expone directo, como el resto de los campos de esta clase. Quien lo LEA para
        ''' escribirlo a un archivo debe pasar por <see cref="RotationRowMajor"/>, que devuelve una copia y además
        ''' decide crudo-vs-axis-angle; asignarlo directo (el sidecar al leer) es correcto porque el array es
        ''' recién construido.</para></summary>
        Public RotMatrixRaw As Single()

        ''' <summary>Los NOMBRES de las capas ajenas cuyo TRS se colapso en el valor efectivo de este hueso.
        ''' <para>PARA QUE SIRVE: nuestro valor es el TOTAL del hueso. Si esas mismas capas estan presentes
        ''' en el co-save del jugador —pasa cuando un mod le aplica ESTE preset a ESTE NPC con
        ''' <c>CharGen.LoadCharacterPresetEx</c>— el motor compondria las suyas con nuestro total y el hueso
        ''' saldria al doble. El apply-script las neutraliza escribiendoles IDENTIDAD COMPLETA bajo su propio
        ''' nombre.</para>
        ''' <para>POR QUE POR NOMBRE Y NO BARRIENDO TODO: asi se toca EXACTAMENTE lo que nuestro valor ya
        ''' representa. Un barrido a ciegas se llevaba <c>internal</c> (el lift de los tacos altos, donde componer
        ''' ES correcto) y el aporte de un mod que nunca vimos, y eso no tiene vuelta atras.</para>
        ''' <para>NUNCA incluye <see cref="AppOverrideKey"/> (no es ajena), ni <c>internal</c> (es del motor;
        ''' skee la excluye de sus propios presets y no aparece en ninguno de los 41 del corpus), ni un nombre
        ''' terminado en <c>.esp</c>/<c>.esm</c>/<c>.esl</c> (skee los poda en CADA carga del co-save via
        ''' <c>RemoveInvalidTransforms</c>, asi que escribirles seria escribir en el aire).</para></summary>
        Public CollapsedLayerNames As List(Of String) = Nothing

        ''' <summary>Fija la rotación desde una EDICIÓN DE LA UI: axis-angle (radianes) como única fuente.
        ''' <para>EXISTE PARA QUE LA INVARIANTE NO SE PUEDA ROMPER, y se rompió. El editor seteaba
        ''' <c>RotX/Y/Z</c> + <c>RotationDirty</c> y dejaba <see cref="RotMatrixRaw"/> con la matriz VIEJA. En la
        ''' sesión no se notaba (<see cref="RotationRowMajor"/> ignora el crudo cuando está dirty), pero el sidecar
        ''' persiste el crudo y NO persiste <c>RotationDirty</c>: al reabrir la app volvía dirty=False, ganaba la
        ''' matriz vieja, y la edición del usuario desaparecía del <c>.jslot</c> Y del ESP mientras la UI seguía
        ''' mostrándola. Un dato derivado que sobrevive a su fuente es un dato podrido.
        ''' <para>La regla es una sola línea: <b>si la UI edita la rotación, la matriz cruda deja de existir</b>. Por
        ''' eso las tres asignaciones viven acá y no en el form.</para></para></summary>
        Public Sub SetRotationFromUi(x As Single, y As Single, z As Single)
            RotX = x : RotY = y : RotZ = z
            HasRotation = True
            RotationDirty = True
            RotMatrixRaw = Nothing
        End Sub

        ''' <summary>Deep-clone (detaches Raw JSON). Public para los carriers de otra assembly (LooksmenuPreset,
        ''' sidecar). Clona el <see cref="Raw"/> TAMBIÉN: es lo que hace que el elemento crudo sobreviva un
        ''' cerrar-y-reabrir en vez de reconstruirse desde los campos modelados perdiendo todo lo no
        ''' modelado.</summary>
        Public Function Clone() As JslotNodeTransform
            Return New JslotNodeTransform With {
                .NodeName = NodeName, .Scale = Scale, .HasScale = HasScale,
                .PosX = PosX, .PosY = PosY, .PosZ = PosZ, .HasPosition = HasPosition,
                .RotX = RotX, .RotY = RotY, .RotZ = RotZ, .HasRotation = HasRotation, .RotationDirty = RotationDirty,
                .ScaleMode = ScaleMode, .HasScaleMode = HasScaleMode,
                .RotMatrixRaw = DirectCast(RotMatrixRaw?.Clone(), Single()),
                .CollapsedLayerNames = If(CollapsedLayerNames Is Nothing, Nothing, New List(Of String)(CollapsedLayerNames)),
                .Raw = If(Raw Is Nothing, Nothing, JsonNode.Parse(Raw.ToJsonString()))}
        End Function

        ''' <summary>True cuando ningún componente modelado mueve el hueso. Es la pregunta del RENDER y de la UI
        ''' ("¿está editado?"), <b>no</b> la de la persistencia — para eso está <see cref="HasPersistableContent"/>.
        ''' <para>MIRA LA MATRIZ CRUDA, no sólo el axis-angle. Una REFLEXIÓN —el caso exacto para el que existe
        ''' <see cref="RotMatrixRaw"/>— se veía como identidad: con <c>diag(1,1,-1)</c> la traza es 1 ⇒ ángulo π/2,
        ''' pero la matriz es SIMÉTRICA y los tres términos <c>(Mij − Mji)</c> se anulan ⇒ el axis-angle sale
        ''' <c>(0,0,0)</c>. Se perdía en DOS lugares a la vez: el resolver de pose la salteaba (no se renderizaba) y
        ''' el sidecar no la guardaba (desaparecía al reabrir la app). Es el mismo defecto que
        ''' <c>Matrix33ToBSRotation</c> tenía a 180°, entrando por otra puerta.</para></summary>
        Public ReadOnly Property IsIdentity As Boolean
            Get
                ' La matriz cruda MANDA cuando está y la UI no la invalidó — misma regla que RotationRowMajor, que es
                ' el dueño único de esa elección. Sin esto el axis-angle degenerado decide por ella.
                If RotMatrixRaw IsNot Nothing AndAlso RotMatrixRaw.Length = 9 AndAlso Not RotationDirty Then
                    If Not IsIdentityMatrix33(RotMatrixRaw) Then Return False
                End If
                Dim scaleId = (Not HasScale) OrElse Math.Abs(Scale - 1.0F) < 0.00001F
                Dim posId = (Not HasPosition) OrElse (Math.Abs(PosX) < 0.00001F AndAlso Math.Abs(PosY) < 0.00001F AndAlso Math.Abs(PosZ) < 0.00001F)
                Dim rotId = (Not HasRotation) OrElse (Math.Abs(RotX) < 0.000001F AndAlso Math.Abs(RotY) < 0.000001F AndAlso Math.Abs(RotZ) < 0.000001F)
                Return scaleId AndAlso posId AndAlso rotId
            End Get
        End Property

        ''' <summary>True cuando el elemento lleva <b>algo</b> que valga la pena guardar. NO es
        ''' <see cref="IsIdentity"/> negado: un nodo puede no mover el hueso y ser igual imprescindible.
        ''' <para>Gatear la persistencia con <c>Not IsIdentity</c> perdía dos cosas al cerrar y reabrir la app:
        ''' (1) un nodo cuyo único contenido es la <b>key 40</b> (<c>NodeDestination</c>, el re-parenteo con el que
        ''' XPMSE te cuelga la espada de la espalda) — no prende ningún <c>Has*</c>, así que "era identidad", y el
        ''' elemento entero desaparecía del <c>.jslot</c> re-exportado, que es exactamente el daño que
        ''' <see cref="StripForeignTrsLayers"/> se arregló para no hacer; y (2) los
        ''' <see cref="CollapsedLayerNames"/>, sin los cuales el ESP deja de neutralizar y el hueso vuelve a sumar
        ''' doble.</para></summary>
        Public ReadOnly Property HasPersistableContent As Boolean
            Get
                If Not IsIdentity Then Return True
                If CollapsedLayerNames IsNot Nothing AndAlso CollapsedLayerNames.Count > 0 Then Return True
                Return RawCarriesNonComposingValues()
            End Get
        End Property

        ''' <summary>Deja el nodo SIN nada que se componga —lo que hace el botón "Reset" del editor— y devuelve True
        ''' si todavía queda algo que valga la pena conservar (o sea, si el elemento NO se debe borrar de la lista).
        ''' <para>⛔ NO hacer <c>RemoveAll</c> del nodo desde el editor: se lleva el elemento COMPLETO y con él la
        ''' key 40 (el re-parenteo) y cualquier value ajeno no modelado — la misma pérdida que evita
        ''' <see cref="StripForeignTrsLayers"/>, entrando por la puerta de la UI. La ley es por COMPONENTE:
        ''' "resetear" es sacar los componentes que se componen, no demoler el nodo.</para>
        ''' <para>Se limpian también los <see cref="CollapsedLayerNames"/>: si ya no aportamos nada al hueso, no hay
        ''' total nuestro que pueda contarse doble, y neutralizar capas ajenas dejaría de tener justificación.</para></summary>
        Public Function ResetComposingComponents() As Boolean
            HasScale = False : Scale = 1.0F
            HasPosition = False : PosX = 0.0F : PosY = 0.0F : PosZ = 0.0F
            HasRotation = False : RotX = 0.0F : RotY = 0.0F : RotZ = 0.0F
            RotMatrixRaw = Nothing
            RotationDirty = False
            CollapsedLayerNames = Nothing
            ' Y del Raw se sacan los values que se componen, en TODAS las capas — incluida la nuestra. Sin esto el
            ' Save re-emitiría la escala/posición/rotación viejas desde el Raw (los parches de Save están gateados
            ' por los Has*, que acabamos de apagar).
            Dim ro = TryCast(Raw, JsonObject)
            If ro Is Nothing Then Return False
            Dim outKeys As New JsonArray()
            For Each k In AsArray(ro("keys"))
                Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                Dim keep As New JsonArray()
                For Each v In AsArray(ko("values"))
                    Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                    Dim vk = GetInt(vo("key"))
                    If vk = 30 OrElse vk = 31 OrElse vk = 32 Then Continue For
                    keep.Add(JsonNode.Parse(vo.ToJsonString()))
                Next
                If keep.Count > 0 Then
                    outKeys.Add(New JsonObject From {{"name", GetStr(ko("name"))}, {"values", keep}})
                End If
            Next
            If outKeys.Count = 0 Then
                Raw = Nothing
                Return False
            End If
            ro("keys") = outKeys
            Return True
        End Function

        ''' <summary>True si el <see cref="Raw"/> lleva algún value que el motor <b>no compone</b> (o sea: que no es
        ''' 30/31/32) — la key 40 y la 33 son los casos reales. Son los values que no modelamos y que viajan
        ''' verbatim, así que si se pierde el <c>Raw</c> se pierden ellos.</summary>
        Private Function RawCarriesNonComposingValues() As Boolean
            If Raw Is Nothing Then Return False
            Dim ro = TryCast(Raw, JsonObject)
            If ro Is Nothing Then Return False
            For Each k In AsArray(ro("keys"))
                Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                For Each v In AsArray(ko("values"))
                    Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                    Dim vk = GetInt(vo("key"))
                    If vk <> 30 AndAlso vk <> 31 AndAlso vk <> 32 Then Return True
                Next
            Next
            Return False
        End Function
    End Class

    ''' <summary>Factory for a UI/sidecar-created scale-only node transform (node name + uniform scale).
    ''' Builds the Raw element so a later Save round-trips it. Public so the app's sidecar hydrate can
    ''' rebuild the carrier from a stored node→scale map (legacy scale-only sidecars, schema &lt; 10).</summary>
    Public Shared Function MakeScaleTransform(nodeName As String, scale As Single) As JslotNodeTransform
        Return New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True, .Raw = BuildTransformRaw(New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True})}
    End Function

    ''' <summary>Build a fresh RaceMenu transform element {firstPerson:false, node,
    ''' keys:[{name:<see cref="AppOverrideKey"/>, values:[…]}]} from the modeled fields, for a UI/sidecar-created
    ''' (Raw-less) transform. La capa se nombra SIEMPRE con <see cref="AppOverrideKey"/>: es la key que el
    ''' apply-script reclama y barre (<c>XformKey()</c>); con cualquier otra, nadie la limpia. Emits keys 30
    ''' (scale), 31 (position x/y/z), 32 (rotation 3×3 from the axis-angle) and 33 (scaleMode) for whichever
    ''' components are present. Value layout mirrors skee64's PackValue (NiTransformInterface.cpp:1009-1049).</summary>
    Private Shared Function BuildTransformRaw(nt As JslotNodeTransform) As JsonObject
        Dim vals As New JsonArray()
        If nt.HasScale Then vals.Add(TransformValueNode(30, 4, 0, CDbl(nt.Scale)))
        If nt.HasPosition Then
            vals.Add(TransformValueNode(31, 4, 0, CDbl(nt.PosX)))
            vals.Add(TransformValueNode(31, 4, 1, CDbl(nt.PosY)))
            vals.Add(TransformValueNode(31, 4, 2, CDbl(nt.PosZ)))
        End If
        If nt.HasRotation Then
            Dim r As Single()
            r = RotationRowMajor(nt)   ' UN solo dueño de la elección crudo-vs-axis-angle: ver su doc.
            For i = 0 To 8 : vals.Add(TransformValueNode(32, 4, i, CDbl(r(i)))) : Next
        End If
        ' Sin key 33: el motor no la lee nunca (bug de indice; ver el bloque del decode con las citas).
        ' A wholly-empty transform still carries the scale key (keeps parity with the legacy scale-only build).
        If vals.Count = 0 Then vals.Add(TransformValueNode(30, 4, 0, CDbl(nt.Scale)))
        Return New JsonObject From {
            {"firstPerson", False}, {"node", nt.NodeName},
            {"keys", New JsonArray From {New JsonObject From {{"name", AppOverrideKey}, {"values", vals}}}}}
    End Function

    ''' <summary>Identidad 3x3.</summary>
    Private Shared Function Identity33() As NiflySharp.Structs.Matrix33
        Return New NiflySharp.Structs.Matrix33 With {.M11 = 1.0F, .M22 = 1.0F, .M33 = 1.0F}
    End Function

    ''' <summary>True si los 9 floats row-major son la identidad. La tolerancia es la misma que usa
    ''' <see cref="JslotNodeTransform.IsIdentity"/> para el axis-angle, para que las dos vías coincidan.</summary>
    Friend Shared Function IsIdentityMatrix33(r As Single()) As Boolean
        If r Is Nothing OrElse r.Length <> 9 Then Return True
        For i = 0 To 8
            Dim expect = If(i = 0 OrElse i = 4 OrElse i = 8, 1.0F, 0.0F)
            If Math.Abs(r(i) - expect) > 0.000001F Then Return False
        Next
        Return True
    End Function

    ''' <summary>Los 9 floats ROW-MAJOR del .jslot como Matrix33 (mismo orden que skee empaqueta bajo la
    ''' key 32, indice 0..8).</summary>
    Private Shared Function Matrix33From(r As Single()) As NiflySharp.Structs.Matrix33
        Return New NiflySharp.Structs.Matrix33 With {
            .M11 = r(0), .M12 = r(1), .M13 = r(2),
            .M21 = r(3), .M22 = r(4), .M23 = r(5),
            .M31 = r(6), .M32 = r(7), .M33 = r(8)}
    End Function

    ''' <summary>Producto 3x3 (a * b), row-major.</summary>
    Private Shared Function Multiply33(a As NiflySharp.Structs.Matrix33, b As NiflySharp.Structs.Matrix33) As NiflySharp.Structs.Matrix33
        Return New NiflySharp.Structs.Matrix33 With {
            .M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            .M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            .M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
            .M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            .M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            .M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
            .M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            .M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            .M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33}
    End Function

    ''' <summary>¿Este nombre de capa se puede neutralizar con identidad? Excluye lo que NO es de otro autor y
    ''' lo que no tiene sentido escribir.
    ''' <para><c>internal</c> es del MOTOR: skee la sintetiza del equipo (el lift de los tacos altos por
    ''' <c>HH_OFFSET</c> sobre el nodo <c>NPC</c>, y el <c>SDTA</c> de una armadura) y ahi componer ES correcto —
    ''' el NPC con tacos tiene que levantarse. No puede aparecer en un preset (skee la excluye de sus exports,
    ''' <c>PresetInterface.cpp:534</c>, y no esta en ninguno de los 41 del corpus), pero el filtro va igual: un
    ''' archivo escrito a mano podria traerla y neutralizarla hundiria al NPC en el piso.</para>
    ''' <para>Un nombre terminado en <c>.esp</c>/<c>.esm</c>/<c>.esl</c> lo poda skee en CADA carga del co-save
    ''' (<c>RemoveInvalidTransforms</c>), asi que escribirle identidad es escribir en el aire.</para></summary>
    ''' <remarks><c>Public</c>, no <c>Friend</c>: el sidecar <c>.bssliders</c> vive en la OTRA assembly y tiene que
    ''' poder aplicar esta misma regla al leer — un sidecar es un archivo editable, y un <c>internal</c> escrito ahí a
    ''' mano hundiría al NPC en el piso. Es el mismo motivo por el que <see cref="JslotNodeTransform.RotMatrixRaw"/>
    ''' dejó de ser <c>Friend</c>: una regla que la persistencia tiene que respetar no puede ser invisible para
    ''' ella.</remarks>
    Public Shared Function IsNeutralizableLayerName(name As String) As Boolean
        If String.IsNullOrWhiteSpace(name) Then Return False
        If String.Equals(name, "internal", StringComparison.OrdinalIgnoreCase) Then Return False
        If String.Equals(name, "NodeDestination", StringComparison.OrdinalIgnoreCase) Then Return False
        Dim dot = name.LastIndexOf("."c)
        If dot >= 0 Then
            Dim ext = name.Substring(dot + 1)
            If String.Equals(ext, "esp", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, "esm", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(ext, "esl", StringComparison.OrdinalIgnoreCase) Then Return False
        End If
        Return True
    End Function

    ''' <summary>¿Esta capa aporta algun value TRS (30/31/32)? Una que sólo trae, por ejemplo, un
    ''' <c>NodeDestination</c> (key 40) no se compone y no hay nada que neutralizar.</summary>
    Private Shared Function LayerHasTrs(layer As JsonObject) As Boolean
        For Each v In AsArray(layer("values"))
            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
            Dim vk = GetInt(vo("key"))
            If vk = 30 OrElse vk = 31 OrElse vk = 32 Then Return True
        Next
        Return False
    End Function

    ''' <summary>Saca de las capas AJENAS únicamente sus values que se COMPONEN (30/31/32) y conserva todo el resto; una capa
    ''' que queda sin values se elimina. Nuestra capa (<see cref="AppOverrideKey"/>) pasa intacta.
    ''' <para>El porqué del recorte: los values TRS son los que el motor <b>compone</b>, y nuestro valor ya lleva su
    ''' aporte COMPUESTO. Si quedaran, el próximo import los contaría dos veces y el preset se deformaría un poco más
    ''' en cada vuelta.</para>
    ''' <para>⛔ POR **VALUE**, NUNCA POR CAPA. "La capa no es nuestra ⇒ se va entera" se lleva de arrastre la
    ''' <b>key 40</b> (<c>NodeDestination</c>), que no es un valor de transform sino un <b>re-parenteo</b>: le dice
    ''' al motor de qué otro hueso tiene que colgar este nodo — es el mecanismo con el que XPMSE te pone la espada
    ''' en la espalda en vez de la cintura. No se compone, no entra en la multiplicación, no la modelamos y nunca
    ''' la autoramos: borrarla destruye una decisión estructural ajena sin ningún beneficio.</para>
    ''' <para>La regla es por COMPONENTE en todo el subsistema. Con eso la key 40 nunca está en nuestro camino: no
    ''' se lee, no se escribe, no se neutraliza. Y no tiene "identidad" posible — su neutro sería la cadena vacía,
    ''' que para el motor <b>es otra orden</b> ("no cuelgues de nadie"), no la ausencia de orden.</para></summary>
    Private Shared Function StripForeignTrsLayers(keys As JsonArray, hasTrs As Boolean) As JsonArray
        Dim outKeys As New JsonArray()
        Dim ours As JsonObject = Nothing
        For Each k In keys
            Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
            If String.Equals(GetStr(ko("name")), AppOverrideKey, StringComparison.OrdinalIgnoreCase) Then
                ours = TryCast(JsonNode.Parse(ko.ToJsonString()), JsonObject)
                Continue For
            End If
            ' SE FILTRA POR **VALUE**, NO POR CAPA (ver el summary): de la capa ajena se sacan sólo los values TRS
            ' (30/31/32) —los únicos que se COMPONEN y que por lo tanto duplicarían nuestro total— y se conserva
            ' todo lo demás. La capa se elimina sólo si queda sin values.
            ' LA KEY 33 (scaleMode) TAMPOCO SE SACA, por el MISMO argumento que la 40: el motor NUNCA lee el
            ' key-33 de un nodo (busca (33,-1) y todo se almacena en (33,0) — ver el decode de transforms), así
            ' que no se compone y no puede duplicar nuestro total. Sacarla es churn destructiva en el archivo de
            ' otro sin ningún beneficio.
            ' MISMO PREDICADO QUE EL DECODE: una capa que NO se puede neutralizar tampoco se absorbió, así que
            ' pasa ENTERA — con su TRS. Sacarle el TRS sin haberlo absorbido era destruirlo, y absorberlo sin poder
            ' apagarlo era duplicarlo; las dos mitades tienen que decidirse con la misma pregunta.
            If Not IsNeutralizableLayerName(GetStr(ko("name"))) Then
                outKeys.Add(JsonNode.Parse(ko.ToJsonString()))
                Continue For
            End If
            Dim keep As New JsonArray()
            For Each v In AsArray(ko("values"))
                Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                Dim vk = GetInt(vo("key"))
                If vk = 30 OrElse vk = 31 OrElse vk = 32 Then Continue For
                keep.Add(JsonNode.Parse(vo.ToJsonString()))
            Next
            If keep.Count > 0 Then
                outKeys.Add(New JsonObject From {{"name", GetStr(ko("name"))}, {"values", keep}})
            End If
        Next
        ' NUESTRA CAPA SÓLO SE AGREGA SI VA A LLEVAR ALGO: un `{"name":"NPCM_Manolov","values":[]}` es inerte
        ' in-game (el push_back del loader está dentro del loop de values, PresetInterface.cpp:1124) pero planta
        ' nuestro nombre en un nodo al que no aportamos nada.
        If ours IsNot Nothing Then
            outKeys.Add(ours)
        ElseIf hasTrs Then
            outKeys.Add(New JsonObject From {{"name", AppOverrideKey}, {"values", New JsonArray()}})
        End If
        Return outKeys
    End Function

    ''' <summary>One transform value element {key,type,index,data}. type 4 = float (data as Double), else int
    ''' (data as Long) — matching the OverrideVariant type enum skee64 serialises (PresetInterface.cpp:576-586).</summary>
    Private Shared Function TransformValueNode(key As Integer, vtype As Integer, index As Integer, data As Object) As JsonObject
        Dim jval As JsonNode = If(vtype = 4, JsonValue.Create(CDbl(data)), JsonValue.Create(CLng(data)))
        Return New JsonObject From {{"key", key}, {"type", vtype}, {"index", index}, {"data", jval}}
    End Function

    ''' <summary>Flatten a <see cref="NiflySharp.Structs.Matrix33"/> to the row-major 9-float order skee64 stores
    ''' (index i → data[i\3][i mod 3], NiTransformInterface.cpp:791-838).</summary>
    Private Shared Function MatrixRowMajor(m As NiflySharp.Structs.Matrix33) As Single()
        Return New Single() {m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33}
    End Function

    ''' <summary>The rotation of <paramref name="nt"/> as the SAME 9 floats this class writes to the
    ''' <c>.jslot</c> under key 32, value index 0..8 — i.e. <c>BSRotationToMatrix33(axis-angle)</c> flattened
    ''' row-major. Returns Nothing when the transform carries no rotation.
    ''' <para>Devolver los 9 floats no exige NINGUNA convención de euler: skee los escribe y los vuelve a leer en
    ''' el mismo orden, así que le estamos dando de vuelta los valores que él produjo. El camino de 3 floats sí
    ''' dependería de que el orden heading/attitude/bank de <c>NiMatrix33::SetEulerAngles</c> coincida con el
    ''' nuestro: una suposición que no hace falta hacer, así que no se hace.</para>
    '''
    ''' <para>Exists so the Papyrus apply-script emitter can hand skee64 the exact float sequence skee64
    ''' itself round-trips, instead of re-deriving one. <c>NiOverride.AddNodeTransformRotation</c> accepts
    ''' EITHER 3 euler angles in degrees OR these 9 raw matrix floats, which it copies straight into
    ''' <c>NiMatrix33::arr[i]</c> (PapyrusNiOverride.cpp:1190-1193).</para>
    ''' <para>⛔ ES EL ÚNICO DUEÑO de la elección crudo-vs-axis-angle, y los TRES consumidores (<c>Save</c>,
    ''' <see cref="BuildTransformRaw"/> y el emisor del ESP) tienen que llamarla. Rearmar la matriz desde el
    ''' axis-angle en uno solo de ellos destruye las rotaciones de 180° y las reflexiones por ese camino — el
    ''' <c>.jslot</c> las preservaría y el ESP no, que es justo la divergencia que <c>RotMatrixRaw</c> existe
    ''' para evitar. Devuelve una COPIA para que nadie pueda mutar el crudo del modelo.</para></summary>
    Public Shared Function RotationRowMajor(nt As JslotNodeTransform) As Single()
        If nt Is Nothing OrElse Not nt.HasRotation Then Return Nothing
        ' La matriz CRUDA cuando la hay (los mismos 9 floats que se leyeron, o su producto exacto). Sólo se
        ' reconstruye desde el axis-angle cuando la rotación la generó la UI, que es el caso donde el
        ' axis-angle ES la fuente y el crudo está viejo.
        If nt.RotMatrixRaw IsNot Nothing AndAlso nt.RotMatrixRaw.Length = 9 AndAlso Not nt.RotationDirty Then
            Return DirectCast(nt.RotMatrixRaw.Clone(), Single())
        End If
        Return MatrixRowMajor(Transform_Class.BSRotationToMatrix33(
            New System.Numerics.Vector3(nt.RotX, nt.RotY, nt.RotZ)))
    End Function

    ''' <summary>El ÚLTIMO value que matchee (key, index) DENTRO DE NUESTRA CAPA, o Nothing.
    ''' <paramref name="index"/> &lt; 0 = cualquier índice.
    ''' <para>Busca SÓLO dentro de <see cref="OurTransformLayer"/>, donde por construcción hay un único value por
    ''' (key, index) — el "último vs primero" no decide nada mientras eso valga. Se queda con el ÚLTIMO por
    ''' robustez ante un archivo con values repetidos en nuestra propia capa (un <c>.jslot</c> escrito a mano, o
    ''' por otra herramienta que use nuestra key), que es también el que leería el motor.</para></summary>
    Private Shared Function LastTransformValue(keys As JsonArray, key As Integer, index As Integer) As JsonObject
        Dim ours = OurTransformLayer(keys, create:=False)
        If ours Is Nothing Then Return Nothing
        Dim found As JsonObject = Nothing
        For Each v In AsArray(ours("values"))
            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
            If GetInt(vo("key")) = key AndAlso (index < 0 OrElse GetInt(vo("index")) = index) Then found = vo
        Next
        Return found
    End Function

    ''' <summary>La capa <see cref="AppOverrideKey"/> dentro de <paramref name="keys"/>, o Nothing (con
    ''' <paramref name="create"/> = True se agrega vacía y se devuelve).
    ''' <para>LEY: todo lo que escribimos vive en NUESTRA capa, la misma que el apply-script aplica y borra
    ''' in-game (<c>XformKey()</c>). Buscar el value por (key,index) en TODAS las capas termina escribiendo
    ''' DENTRO de la capa de otro mod.</para>
    ''' <para>Esta función no TOCA las capas ajenas (sólo busca la nuestra); el que sí las modifica es
    ''' <see cref="StripForeignTrsLayers"/>, que les borra los values TRS y elimina la capa si queda vacía.</para>
    ''' <para>POR QUÉ EL STRIP ES ACEPTABLE: el propio cargador de presets de skee es MÁS destructivo. Antes de
    ''' replayear los <c>transforms</c> de un <c>.jslot</c> llama a <c>Impl_RemoveAllReferenceTransforms(actor)</c>
    ''' (<c>PresetInterface.cpp:264</c> — encima FUERA del gate <c>kPresetApplyTransforms</c> — y <c>:1631</c>), que
    ''' es <c>m_data.erase(formID)</c>: borra la entrada ENTERA del actor — los dos géneros, primera y tercera
    ''' persona, todos los nodos, todas las keys, sin salvar ni <c>"internal"</c>
    ''' (<c>NiTransformInterface.cpp:342-351</c>) — y después repone SÓLO lo que el archivo trae. Cargar un preset
    ''' en RaceMenu ya es "el archivo es la verdad y lo demás se va".</para>
    ''' <para>CONSECUENCIA para la regla "el <c>.jslot</c> tiene que aplicar lo mismo que la herramienta": sobre
    ''' un hueso que autoramos coinciden (el archivo trae una sola capa con el efectivo y el cargador la repone tal
    ''' cual). Donde NO coinciden es sobre los huesos que NO autoramos: el <c>.jslot</c> los borra igual, porque el
    ''' cargador poda el actor entero, y el ESP no los toca.</para></summary>
    Private Shared Function OurTransformLayer(keys As JsonArray, create As Boolean) As JsonObject
        For Each k In keys
            Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
            If String.Equals(GetStr(ko("name")), AppOverrideKey, StringComparison.OrdinalIgnoreCase) Then
                If TryCast(ko("values"), JsonArray) Is Nothing Then ko("values") = New JsonArray()
                Return ko
            End If
        Next
        If Not create Then Return Nothing
        Dim fresh As New JsonObject From {{"name", AppOverrideKey}, {"values", New JsonArray()}}
        keys.Add(fresh)
        Return fresh
    End Function

    ''' <summary>Escribe el value de (<paramref name="key"/>, <paramref name="index"/>) DENTRO DE NUESTRA CAPA
    ''' (<see cref="AppOverrideKey"/>): si ya existe ahí, se actualiza; si no, se agrega, creando la capa si el nodo
    ''' todavía no la tenía. Las capas de otros mods NO se leen ni se tocan.
    ''' <para>⛔ NO buscar el value en TODAS las capas —ni "la primera que matchee" ni "la última"—: escribe
    ''' nuestro valor DENTRO de la capa ajena que encuentre (medido: <c>RMX_Leg_Calf</c> 0.3 → 0.7). La premisa
    ''' falsa es que el nodo tiene UN valor: tiene una capa POR CONTRIBUYENTE, y la nuestra es
    ''' <c>NPCM_Manolov</c>, la misma con la que el apply-script aplica y borra (<c>XformKey()</c> en el
    ''' <c>.psc</c>). Así el archivo y el juego dicen lo mismo, y un load→save no puede tocar el dato de otro mod
    ''' porque nunca lo mira.</para></summary>
    ''' <param name="writeType">False = escribir SÓLO <c>data</c> y dejar el <c>type</c> que hubiera. Los cuatro
    ''' call sites usan el default True, o sea que hoy es un camino sin recorrer; se conserva porque el caso que
    ''' describe es real (patchear un value sin tocar su <c>type</c>).</param>
    Private Shared Sub PatchTransformValue(keys As JsonArray, key As Integer, vtype As Integer, index As Integer, data As Object,
                                           Optional writeType As Boolean = True)
        Dim target = LastTransformValue(keys, key, index)
        If target IsNot Nothing Then
            If writeType Then target("type") = vtype
            target("data") = If(vtype = 4, JsonValue.Create(CDbl(data)), JsonValue.Create(CLng(data)))
            Return
        End If
        Dim ours = OurTransformLayer(keys, create:=True)
        Dim vals = TryCast(ours("values"), JsonArray)
        If vals Is Nothing Then vals = New JsonArray() : ours("values") = vals
        vals.Add(TransformValueNode(key, vtype, index, data))
    End Sub

    Private Shared Function Clamp01(v As Single) As Single
        If v < 0.0F Then Return 0.0F
        If v > 1.0F Then Return 1.0F
        Return v
    End Function

    ''' <summary>El 0xAARRGGBB de un tint, en la forma que el motor sabe leer: <b>Int32 CON SIGNO</b>.
    ''' <para>Es UNA sola ley y vive acá porque estaba escrita dos veces con distinto resultado: el encoder
    ''' de overlays convertía a Int32 y el de skinOverrides emitía el UInteger tal cual como Long. Con alpha 255
    ''' (el default al crear un skin override) el valor supera Int32.MaxValue, y ahí skee no lo lee mal — <b>lanza</b>:
    ''' <c>value.data.i = jvalue["data"].asInt()</c> (PresetInterface.cpp:1196) sobre un literal que jsoncpp guardó
    ''' como <c>uintValue</c> dispara <c>JSON_ASSERT_MESSAGE(isInt(), "LargestUInt out of Int range")</c>
    ''' (json_value.cpp:636-638) con <c>JSON_USE_EXCEPTION 1</c> (json/config.h:33), y <c>LoadJsonPreset</c>
    ''' (PresetInterface.cpp:898-1240) no tiene un solo try/catch. El preset entero deja de cargar.</para>
    ''' <para>Que la forma firmada es la canónica no es preferencia: skee emite ese campo con
    ''' <c>static_cast&lt;Json::Int&gt;</c> (PresetInterface.cpp:639) y los valores key-7 de los presets reales son
    ''' todos negativos.</para></summary>
    Private Shared Function SignedTintValue(u As UInteger) As Integer
        Return BitConverter.ToInt32(BitConverter.GetBytes(u), 0)
    End Function

    ''' <summary>Repara IN PLACE todo value de tipo 3 (kType_Int) cuyo número no entre en un Int32 con signo,
    ''' reescribiéndolo a su forma firmada. Se corre sobre los <c>values</c> ANTES de parchear los campos modelados.
    ''' <para>Sin esto el fix sólo alcanzaría a lo que se re-emite desde el modelo: un value que viene por la rama
    ''' de preservación verbatim (p. ej. el tint de un skinOverride cuyo checkbox está destildado) conservaría para
    ''' siempre el número sin signo que escribió la versión anterior de esta app, y el preset seguiría sin cargar.
    ''' Un solo re-guardado repara el archivo.</para>
    ''' <para>Aplica a TODO tipo 3, no sólo a la key 7: skee lee cada uno con <c>asInt()</c>, así que cualquiera
    ''' por encima de Int32.MaxValue tiene el mismo final.</para></summary>
    Private Shared Sub NormalizeSignedIntValues(vals As JsonArray)
        If vals Is Nothing Then Return
        For Each v In vals
            Dim vo = TryCast(v, JsonObject)
            If vo Is Nothing OrElse GetInt(vo("type")) <> 3 Then Continue For
            Dim jv = TryCast(vo("data"), JsonValue)
            If jv Is Nothing Then Continue For
            Dim raw As ULong
            If Not jv.TryGetValue(Of ULong)(raw) Then Continue For   ' negativo o no numérico ⇒ ya está bien
            If raw <= CULng(Integer.MaxValue) Then Continue For
            If raw > CULng(UInteger.MaxValue) Then Continue For      ' fuera del ancho del campo: no es nuestro
            vo("data") = JsonValue.Create(SignedTintValue(CUInt(raw)))
        Next
    End Sub

    ''' <summary>Patch (or append) a skinOverride value element matching key/type/index. <paramref name="isString"/>
    ''' true → data is the texture path (skip when empty); false → data is a numeric.
    ''' <para>El numérico de tipo ≠4 se emite como <b>Int32</b>, igual que <see cref="PatchOverlayValue"/>: los
    ''' dos parchean la MISMA clase de value (key 7 type 3) y emitirlos distinto es lo que rompía el preset. Ver
    ''' <see cref="SignedTintValue"/>.</para></summary>
    Private Shared Sub PatchSkinValue(vals As JsonArray, key As Integer, vtype As Integer, index As Integer, data As Object, isString As Boolean)
        If isString AndAlso String.IsNullOrEmpty(TryCast(data, String)) Then
            ' Empty texture path → remove any existing element for this slot so we don't emit an empty override.
            For i = vals.Count - 1 To 0 Step -1
                Dim vo = TryCast(vals(i), JsonObject)
                If vo IsNot Nothing AndAlso GetInt(vo("key")) = key AndAlso GetInt(vo("type")) = vtype AndAlso GetInt(vo("index")) = index Then vals.RemoveAt(i)
            Next
            Return
        End If
        Dim jval As JsonNode = If(isString, JsonValue.Create(CStr(data)),
                                  If(vtype = 4, JsonValue.Create(CDbl(data)), JsonValue.Create(CInt(data))))
        For Each v In vals
            Dim vo = TryCast(v, JsonObject)
            If vo Is Nothing Then Continue For
            If GetInt(vo("key")) = key AndAlso GetInt(vo("type")) = vtype AndAlso GetInt(vo("index")) = index Then
                vo("data") = jval
                Return
            End If
        Next
        vals.Add(New JsonObject From {{"key", key}, {"type", vtype}, {"index", index}, {"data", jval}})
    End Sub
    ''' <summary>RaceMenu NiOverride node transforms (body-scale sliders). Modeled to the editable per-node
    ''' <see cref="JslotNodeTransform.Scale"/>; unmodeled keys preserved verbatim via Raw.</summary>
    Public Property NodeTransforms As New List(Of JslotNodeTransform)
    ''' <summary>True when the loaded .jslot carried a top-level "transforms" node (fidelity flag, same role
    ''' as <see cref="_hadBodyMorphs"/>).</summary>
    Private _hadTransforms As Boolean

    ''' <summary>One RaceMenu NiOverride SKIN override (body-paint / skin texture-tint per biped slot). The
    ''' <c>.jslot</c> <c>skinOverrides</c> element = {firstPerson, slotMask, values:[{key,type,index,data}]}
    ''' keyed by armor slot bitmask (skee64 PresetInterface.cpp:623-653). We model the editable diffuse/normal
    ''' texture paths (key 9 slot 0/1) + tint (key 7, 0xAARRGGBB) — same decode as an overlay — and keep the
    ''' full element in <see cref="Raw"/> so a load→save round-trips any other keys byte-faithfully.</summary>
    Public Class JslotSkinOverride
        Public Property SlotMask As UInteger
        ''' <summary>Every kParam_ShaderTexture (key 9) slot the override sets, keyed by texture-set slot index
        ''' (0=diffuse, 1=normal, 2=subsurface/detail, 7=backlight/specular, … up to kNumTextures−1). skee replaces
        ''' each of these slots IN PLACE on the skin shape's BSShaderTextureSet, keeping the untouched slots
        ''' (ShaderUtilities.cpp NIOVTaskUpdateTexture). <see cref="DiffusePath"/>/<see cref="NormalPath"/> are the
        ''' convenience views of slots 0/1 the editor edits.</summary>
        Public Property Slots As New Dictionary(Of Integer, String)
        Public Property DiffusePath As String
        Public Property NormalPath As String
        Public Property TintR As Single
        Public Property TintG As Single
        Public Property TintB As Single
        Public Property TintA As Single
        Public Property HasTint As Boolean
        ''' <summary>kParam_ShaderAlpha (key 8) — the override's material alpha; independent of the tint colour.</summary>
        Public Property Alpha As Single = 1.0F
        Public Property HasAlpha As Boolean
        ''' <summary>Índice de origen del tint / alpha, misma ley y mismo motivo que
        ''' <see cref="JslotOverlayNode.TintIndex"/>: el value se re-emite EN SU índice en vez de en −1 cableado,
        ''' porque skee ordena y aplica por <c>(key,index)</c> y un duplicado lo resuelve a favor del viejo.</summary>
        Public Property TintIndex As Integer = -1
        Public Property AlphaIndex As Integer = -1
        Friend Raw As JsonNode
        Public Function Clone() As JslotSkinOverride
            Return New JslotSkinOverride With {
                .SlotMask = SlotMask, .Slots = New Dictionary(Of Integer, String)(Slots),
                .DiffusePath = DiffusePath, .NormalPath = NormalPath,
                .TintR = TintR, .TintG = TintG, .TintB = TintB, .TintA = TintA, .HasTint = HasTint,
                .Alpha = Alpha, .HasAlpha = HasAlpha,
                .TintIndex = TintIndex, .AlphaIndex = AlphaIndex,
                .Raw = If(Raw Is Nothing, Nothing, JsonNode.Parse(Raw.ToJsonString()))}
        End Function
    End Class
    ''' <summary>RaceMenu skin overrides (body-paint / skin texture-tint per slot). Editable diffuse/normal/tint;
    ''' Raw preserves the rest for round-trip.</summary>
    Public Property SkinOverrides As New List(Of JslotSkinOverride)
    Private _hadSkinOverrides As Boolean
    ''' <summary>Verbatim JSON of nodes we round-trip but don't model (transforms, version, mods, modNames,
    ''' morphs.default.presets). Preserved so a load→save doesn't lose them.</summary>
    Private _raw As JsonObject
    Private _morphsPresetsRaw As JsonNode
    ''' <summary><c>morphs.default.presets</c> decoded: the NAMA face-part TYPE per family (nose/brow/eyes/lip), the
    ''' same 4-value vector as the NPC record's NAMA (0xFFFFFFFF = "unset/default"). skee applies these
    ''' (PresetInterface.cpp:1540-1543). Modeled so the mapper can carry them to/from the preset; when non-empty it
    ''' is re-emitted in place of <see cref="_morphsPresetsRaw"/> so an edit round-trips.</summary>
    Public Property NamaPresets As New List(Of UInteger)
    ''' <summary>Non-overlay <c>overrides</c> nodes kept verbatim (deep-cloned JSON) so modeling the overlay
    ''' subset doesn't drop the rest of the array. Re-emitted alongside the rebuilt overlay nodes on Save.</summary>
    Private ReadOnly _otherOverridesRaw As New List(Of JsonNode)
    ''' <summary>Los elementos de <c>transforms</c> con <c>firstPerson = true</c>, crudos. NO se modelan (son el 3D
    ''' de primera persona, que un NPC no tiene y que nuestro script nunca escribe) pero SÍ se re-emiten verbatim.
    ''' <para>Las dos mitades hacen falta y por motivos distintos. No modelarlos arregla que el modelo tuviera DOS
    ''' entradas del mismo <c>NodeName</c>. Re-emitirlos evita perder dato ajeno — misma razón que la key 40: no
    ''' modelar algo no da derecho a borrarlo. Mismo patrón que <c>_otherOverridesRaw</c>.</para></summary>
    Private ReadOnly _firstPersonTransformsRaw As New List(Of JsonNode)
    ''' <summary>Los <c>skinOverrides</c> con <c>firstPerson: true</c>, verbatim. MISMA LEY QUE
    ''' <see cref="_firstPersonTransformsRaw"/>: el decode los saltea (son del brazo en primera persona del
    ''' jugador, que un NPC no usa) y <c>Save</c> reconstruye el array sólo desde <c>SkinOverrides</c>, así que sin
    ''' guardarlos acá el elemento DESAPARECE del archivo.
    ''' <para>⚠️ 0 de los 41 presets del corpus lo ejercita: ningún gate cubre este camino.</para></summary>
    Private ReadOnly _firstPersonSkinRaw As New List(Of JsonNode)

    ''' <summary>Los elementos <c>firstPerson</c> como JSON, para que el CARRIER los pueda transportar: el "Save
    ''' RaceMenu preset" de la app construye un <c>RaceMenuJslot</c> NUEVO desde el preset, así que sin esto se
    ''' perderían al re-exportar.</summary>
    Public ReadOnly Property FirstPersonTransformsJson As List(Of String)
        Get
            Dim res As New List(Of String)
            For Each e In _firstPersonTransformsRaw
                If e IsNot Nothing Then res.Add(e.ToJsonString())
            Next
            Return res
        End Get
    End Property

    ''' <summary>Repone un elemento <c>firstPerson</c> desde su JSON (lo usa el mapper al reconstruir el
    ''' <c>.jslot</c>). Ignora en silencio lo que no parsea: es dato ajeno y opaco, y hacerlo fallar impediría
    ''' guardar el NPC por algo que no es nuestro.</summary>
    Public Sub AddFirstPersonTransformJson(json As String)
        If String.IsNullOrWhiteSpace(json) Then Return
        Try
            Dim node = JsonNode.Parse(json)
            If node IsNot Nothing Then _firstPersonTransformsRaw.Add(node)
        Catch
        End Try
    End Sub
    ''' <summary>True when the loaded .jslot carried a top-level "bodyMorphs" node (even empty). Lets
    ''' Save re-emit an empty array faithfully while NOT injecting the node into presets that lacked it.</summary>
    Private _hadBodyMorphs As Boolean
    ''' <summary>True when the loaded .jslot carried a top-level "overrides" node (even empty). Same
    ''' fidelity role as <see cref="_hadBodyMorphs"/>: re-emit the node when it was present, but never
    ''' inject it into a preset that lacked it.</summary>
    Private _hadOverrides As Boolean

    Public Shared Function Load(bytes As Byte()) As RaceMenuJslot
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing
        Dim node As JsonNode
        Using ms As New IO.MemoryStream(bytes)
            node = JsonNode.Parse(ms)
        End Using
        Dim root = TryCast(node, JsonObject)
        If root Is Nothing Then Return Nothing
        Dim j As New RaceMenuJslot() With {._raw = root}
        Dim actor = TryCast(root("actor"), JsonObject)
        If actor IsNot Nothing Then
            j.HairColor = GetInt(actor("hairColor"))
            j.HadHairColor = actor("hairColor") IsNot Nothing   ' present-vs-absent (0 is a legit black override)
            j.HeadTexture = GetStr(actor("headTexture"))
            j.Weight = GetDbl(actor("weight"))
            j.HadWeight = actor("weight") IsNot Nothing   ' presencia, igual que hairColor (0 es un peso legítimo)
        End If
        ' Tabla `mods` (partial index del AUTOR → nombre). Se decodifica para poder traducir los
        ' headParts[].formId de un .jslot viejo sin identifier; el nodo sigue emitiéndose verbatim.
        For Each md In AsArray(root("mods"))
            Dim o = TryCast(md, JsonObject) : If o Is Nothing Then Continue For
            Dim mname = GetStr(o("name"))
            If String.IsNullOrEmpty(mname) Then Continue For
            Dim mkey = CUInt(GetLong(o("index")) And &HFFFFFFFFL)
            If Not j.ModIndexToName.ContainsKey(mkey) Then j.ModIndexToName(mkey) = mname
        Next
        For Each hp In AsArray(root("headParts"))
            Dim o = TryCast(hp, JsonObject) : If o Is Nothing Then Continue For
            ' formId is an unsigned 32-bit FormID; GetInt overflows (→ 0) on anything ≥ 0x80000000, which is
            ' every FormID from a plugin at load-order index ≥ 0x80. Read it as a Long.
            j.HeadParts.Add(New JslotHeadPart With {.FormId = CUInt(GetLong(o("formId")) And &HFFFFFFFFL), .FormIdentifier = GetStr(o("formIdentifier")), .Type = GetInt(o("type"))})
        Next
        For Each ft In AsArray(root("faceTextures"))
            Dim o = TryCast(ft, JsonObject) : If o Is Nothing Then Continue For
            j.FaceTextures.Add(New JslotFaceTexture With {.Index = GetInt(o("index")), .Texture = GetStr(o("texture"))})
        Next
        For Each ti In AsArray(root("tintInfo"))
            Dim o = TryCast(ti, JsonObject) : If o Is Nothing Then Continue For
            j.TintInfo.Add(New JslotTint With {.Color = CUInt(GetLong(o("color")) And &HFFFFFFFFL), .Index = GetInt(o("index")), .Texture = GetStr(o("texture"))})
        Next
        Dim morphs = TryCast(root("morphs"), JsonObject)
        If morphs IsNot Nothing Then
            Dim def = TryCast(morphs("default"), JsonObject)
            If def IsNot Nothing Then
                For Each mv In AsArray(def("morphs"))
                    j.SliderMorphs.Add(CSng(GetDbl(mv)))
                Next
                j._morphsPresetsRaw = def("presets")
                For Each pval In AsArray(def("presets"))
                    j.NamaPresets.Add(CUInt(GetLong(pval) And &HFFFFFFFFL))
                Next
            End If
            For Each cm In AsArray(morphs("custom"))
                Dim o = TryCast(cm, JsonObject) : If o Is Nothing Then Continue For
                j.CustomMorphs.Add(New JslotCustomMorph With {.Name = GetStr(o("name")), .Value = GetDbl(o("value"))})
            Next
            ' El formato del sculpt tiene DOS variantes y la decide la PRESENCIA de `sculptDivisor`:
            '   presente  → los deltas son ENTEROS y se dividen por él  (skee PresetInterface.cpp:1094-1097)
            '   ausente   → `multiplier = -1` y los deltas son FLOATS directos           (:1099-1102)
            ' Leerlos siempre con GetInt truncaba a 0 los de la variante float, o sea que el sculpt entero se
            ' perdía — y encima el Save inyectaba un `sculptDivisor`, dejando el archivo diciendo que esos ceros
            ' eran enteros escalados. Doble daño sobre un preset ajeno.
            ' Al escribir SIEMPRE emitimos la variante con divisor, que es la única que produce el propio motor
            ' (`root["morphs"]["sculptDivisor"] = VERTEX_MULTIPLIER`, :694), así que normalizar al leer es canónico.
            Dim sculptIsFloatForm As Boolean = (morphs("sculptDivisor") Is Nothing)
            If Not sculptIsFloatForm Then j.SculptDivisor = Math.Max(1, GetInt(morphs("sculptDivisor")))
            For Each sp In AsArray(morphs("sculpt"))
                Dim o = TryCast(sp, JsonObject) : If o Is Nothing Then Continue For
                Dim part As New JslotSculptPart
                If o("host") IsNot Nothing Then part.Host = GetStr(o("host"))
                part.HadVertices = o("vertices") IsNot Nothing
                If part.HadVertices Then part.Vertices = GetLong(o("vertices"))
                part.HadData = o("data") IsNot Nothing
                For Each row In AsArray(o("data"))
                    Dim arr = TryCast(row, JsonArray) : If arr Is Nothing OrElse arr.Count < 4 Then Continue For
                    part.Indices.Add(GetInt(arr(0)))
                    If sculptIsFloatForm Then
                        ' Delta en unidades de mundo → al entero escalado que usa el resto del modelo.
                        part.Dx.Add(CInt(Math.Round(GetDbl(arr(1)) * j.SculptDivisor)))
                        part.Dy.Add(CInt(Math.Round(GetDbl(arr(2)) * j.SculptDivisor)))
                        part.Dz.Add(CInt(Math.Round(GetDbl(arr(3)) * j.SculptDivisor)))
                    Else
                        part.Dx.Add(GetInt(arr(1))) : part.Dy.Add(GetInt(arr(2))) : part.Dz.Add(GetInt(arr(3)))
                    End If
                Next
                j.Sculpt.Add(part)
            Next
        End If
        ' bodyMorphs — top-level array [{ name, keys:[{key,value}, …] }] (RaceMenu/BodySlide sliders).
        If root("bodyMorphs") IsNot Nothing Then
            j._hadBodyMorphs = True
            For Each bm In AsArray(root("bodyMorphs"))
                Dim o = TryCast(bm, JsonObject) : If o Is Nothing Then Continue For
                Dim entry As New JslotBodyMorph With {.Name = GetStr(o("name"))}
                ' Forma LEGACY `{name, value}` (sin `keys`). El motor la lee y la mapea a una key llamada
                ' "RSMLegacy": `presetData->bodyMorphData[name]["RSMLegacy"] = value`
                ' (skee64 PresetInterface.cpp:1215-1221), y la lee ADEMÁS de `keys`, no en vez de.
                ' Mirar sólo `keys` hace que el Save reconstruya `{name, keys:[]}` y el morph DESAPAREZCA.
                ' Adoptarla como una key más es exactamente lo que hace el motor, y de paso la
                ' normaliza a la forma moderna sin cambiar lo que rinde (el motor SUMA las keys de un morph).
                Dim legacyValue = o("value")
                If legacyValue IsNot Nothing Then
                    entry.Keys.Add(New JslotBodyMorphKey With {.Key = SkeeLegacyMorphKey, .Value = CSng(GetDbl(legacyValue))})
                End If
                For Each k In AsArray(o("keys"))
                    Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                    entry.Keys.Add(New JslotBodyMorphKey With {.Key = GetStr(ko("key")), .Value = CSng(GetDbl(ko("value")))})
                Next
                j.BodyMorphs.Add(entry)
            Next
        End If
        ' overrides — top-level array [{ node, values:[{key,type,index,data}, …] }]. Overlay nodes
        ' (Body/Hands/Feet [Ovl{n}]/[SOvl{n}]) are decoded to JslotOverlayNode; every other override node
        ' is kept verbatim so a load→save cycle preserves it (§3.1).
        If root("overrides") IsNot Nothing Then
            j._hadOverrides = True
            For Each ov In AsArray(root("overrides"))
                Dim o = TryCast(ov, JsonObject) : If o Is Nothing Then Continue For
                Dim nodeName = GetStr(o("node"))
                If IsOverlayNodeName(nodeName) Then
                    j.Overlays.Add(DecodeOverlayNode(nodeName, o("values")))
                Else
                    ' Non-overlay override node → preserve verbatim (detach via re-parse of its JSON string).
                    j._otherOverridesRaw.Add(JsonNode.Parse(o.ToJsonString()))
                End If
            Next
        End If
        ' transforms — top-level array [{ firstPerson, node, keys:[{name, values:[{key,type,index,data}]}] }].
        ' We model the full TRS: scale (key 30, float), position (key 31, x/y/z @ index 0/1/2), rotation (key 32,
        ' 3×3 matrix @ index 0..8 → axis-angle) and scaleMode (key 33, int). The whole element is also kept in Raw
        ' so Save re-emits any UNmodeled key (e.g. node-destination key 40) byte-faithfully.
        If root("transforms") IsNot Nothing Then
            j._hadTransforms = True
            For Each tr In AsArray(root("transforms"))
                Dim o = TryCast(tr, JsonObject) : If o Is Nothing Then Continue For
                ' LOS ELEMENTOS firstPerson=True NO SE LEEN NI SE EMITEN. Son el 3D de PRIMERA PERSONA (los
                ' brazos que ve el jugador desde sus propios ojos): un NPC no tiene ese arbol, y nuestro apply-script
                ' escribe siempre `AddNodeTransform*(self, false, ...)`. RaceMenu los guarda porque un preset puede
                ' ser del jugador, y por eso casi todo nodo aparece DOS veces en el archivo. Leer los dos deja el
                ' modelo con dos entradas del MISMO NodeName: la UI muestra una, `SetNodeScale` (FirstOrDefault
                ' por nombre) edita una, y la otra queda con el valor del autor — una edicion aplicada a medias
                ' sin decirlo.
                Dim isFirstPerson As Boolean = False
                Dim fpNode = o("firstPerson")
                If fpNode IsNot Nothing Then
                    Try : isFirstPerson = fpNode.GetValue(Of Boolean)() : Catch : isFirstPerson = False : End Try
                End If
                ' SE SALTEAN AL MODELAR PERO **SÍ SE RE-EMITEN**. ⛔ NO borrarlos porque "un NPC no tiene primera
                ' persona": es la misma razón que la key 40 — no modelar algo no da derecho a destruirlo, la regla
                ' es POR COMPONENTE. No afectan al NPC, pero si alguien carga este preset sobre su propio
                ' personaje en RaceMenu, ahí sí valen.
                If isFirstPerson Then
                    j._firstPersonTransformsRaw.Add(JsonNode.Parse(o.ToJsonString()))
                    Continue For
                End If
                Dim nt As New JslotNodeTransform With {.NodeName = GetStr(o("node")), .Raw = JsonNode.Parse(o.ToJsonString())}
                ' La composicion de capas usa la formula EXACTA de NiTransform::operator* (f4se
                ' NiTypes.cpp:180-187, la misma clase del motor):
                '     scale = a.scale * b.scale
                '     rot   = a.rot * b.rot
                '     pos   = a.pos + (a.rot * b.pos) * a.scale     <- usa el rot y el scale ACUMULADOS
                ' "La posicion suma" es solo el caso particular con rot=identidad y scale=1: correcto para los
                ' presets de hoy y silenciosamente falso para el primero que rote o escale.
                ' EL ORDEN ENTRE CAPAS NO ESTA ESPECIFICADO NI EN EL MOTOR: el contenedor es un `unordered_map`
                ' (`NodeTransformKeys`, NiTransformInterface.h:17) recorrido con begin()/end(). Para escala y para
                ' posicion-sin-rotacion el orden no cambia el resultado (producto y suma conmutan); con dos capas
                ' que rotan el producto NO conmuta y el motor mismo no promete cual va primero. Aca se compone en
                ' el orden del ARCHIVO, que es determinista y reproducible.
                Dim accRot = Identity33()
                Dim accScale As Single = 1.0F
                Dim anyRot As Boolean = False
                ' ⛔ NO acumular suma/máximo de escalas "para el scaleMode": el scaleMode POR NODO es INERTE (el
                ' motor lo busca en (33,-1) y todo se guarda en (33,0)), así que no hay ninguna rama que elegir.
                ' Lo que gobierna es el `iScaleMode` GLOBAL del jugador, y para eso no sirve acumular acá.
                For Each k In AsArray(o("keys"))
                    Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                    ' SE REGISTRA EL NOMBRE de toda capa AJENA que aporte TRS: es lo que el apply-script va a
                    ' neutralizar con identidad para que su aporte no se sume a nuestro total. Ver CollapsedLayerNames.
                    Dim layerName = GetStr(ko("name"))
                    If Not String.Equals(layerName, AppOverrideKey, StringComparison.OrdinalIgnoreCase) AndAlso
                       IsNeutralizableLayerName(layerName) AndAlso LayerHasTrs(ko) Then
                        If nt.CollapsedLayerNames Is Nothing Then nt.CollapsedLayerNames = New List(Of String)
                        If Not nt.CollapsedLayerNames.Contains(layerName, StringComparer.OrdinalIgnoreCase) Then
                            nt.CollapsedLayerNames.Add(layerName)
                        End If
                    End If
                    ' NO SE ABSORBE LO QUE NO SE PUEDE NEUTRALIZAR. UN SOLO predicado gobierna las tres decisiones
                    ' —qué se compone, qué se saca del archivo y a qué se le escribe identidad—. Con dos predicados
                    ' distintos (componer TODAS las capas, pero neutralizar sólo las que no son `internal`,
                    ' `NodeDestination` ni nombres con sufijo de plugin) nuestro valor incluye el aporte ajeno, el
                    ' archivo pierde la capa original, el ESP no puede neutralizarla ⇒ el hueso sale AL DOBLE
                    ' in-game.
                    ' Absorber es un compromiso: "me quedo con tu número Y me hago cargo de apagar el tuyo". Si no
                    ' podemos cumplir la segunda mitad, no tomamos la primera. Estas capas quedan intactas en el
                    ' archivo y aportan por su cuenta, que es lo correcto para el lift de los tacos (`internal`).
                    If Not String.Equals(layerName, AppOverrideKey, StringComparison.OrdinalIgnoreCase) AndAlso
                       Not IsNeutralizableLayerName(layerName) Then
                        Continue For
                    End If
                    ' La capa como TRS LOCAL: identidad en lo que no declare (igual que `localTransform` en skee).
                    Dim lScale As Single = 1.0F
                    Dim lPosX As Single = 0.0F, lPosY As Single = 0.0F, lPosZ As Single = 0.0F
                    ' IDENTIDAD, NO CEROS. `localTransform` en skee arranca identidad e
                    ' `Impl_GetOverrideTransform` sobreescribe SOLO los indices presentes (NiTransformInterface.cpp
                    ' :791-838). Con ceros, una key-32 PARCIAL (menos de 9 values, o un indice fuera de 0..8) daba una
                    ' matriz casi nula: el acumulado colapsaba, toda capa posterior aportaba posicion 0 y el
                    ' axis-angle salia de basura. Y desde que la rotacion se REESCRIBE (el strip la saca), esa basura
                    ' se PERSISTIA en el archivo: paso de defecto de display a corrupcion.
                    Dim lRot() As Single = {1.0F, 0.0F, 0.0F, 0.0F, 1.0F, 0.0F, 0.0F, 0.0F, 1.0F}
                    Dim lHasRot As Boolean = False
                    For Each v In AsArray(ko("values"))
                        Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                        Dim vkey = GetInt(vo("key"))
                        Dim vidx = GetInt(vo("index"))
                        Select Case vkey
                            Case 30
                                ' SOLO EL INDICE 0. El motor busca la escala con `value.index = 0` explicito
                                ' (NiTransformInterface.cpp:784), asi que un key-30 en otro indice NO lo aplica.
                                ' Contarlo daba una escala que el juego no usa.
                                If vidx = 0 Then
                                    lScale = CSng(GetDbl(vo("data"))) : nt.HasScale = True
                                End If
                            Case 31
                                Dim d = CSng(GetDbl(vo("data")))
                                Select Case vidx
                                    Case 0 : lPosX = d
                                    Case 1 : lPosY = d
                                    Case 2 : lPosZ = d
                                End Select
                                ' Solo 0/1/2 prenden HasPosition: un key-31 en indice >=3 o -1 no aporta componente, y
                                ' prenderlo hacia que el encode APPENDEARA tres values en 0.0 al archivo ajeno.
                                If vidx >= 0 AndAlso vidx <= 2 Then nt.HasPosition = True
                            Case 32
                                If vidx >= 0 AndAlso vidx <= 8 Then
                                    lRot(vidx) = CSng(GetDbl(vo("data")))
                                    lHasRot = True : anyRot = True
                                End If
                            Case 33
                                ' El motor toma el scaleMode de UNA capa (`scaleModes.rbegin()`, :666), no lo combina.
                                nt.ScaleMode = GetInt(vo("data")) : nt.HasScaleMode = True
                        End Select
                    Next
                    ' combined = combined * local. La posicion se calcula ANTES de actualizar rot/scale, porque la
                    ' formula usa los acumulados VIEJOS.
                    nt.PosX += (accRot.M11 * lPosX + accRot.M12 * lPosY + accRot.M13 * lPosZ) * accScale
                    nt.PosY += (accRot.M21 * lPosX + accRot.M22 * lPosY + accRot.M23 * lPosZ) * accScale
                    nt.PosZ += (accRot.M31 * lPosX + accRot.M32 * lPosY + accRot.M33 * lPosZ) * accScale
                    If lHasRot Then accRot = Multiply33(accRot, Matrix33From(lRot))
                    accScale *= lScale
                Next
                If anyRot Then
                    ' El axis-angle es lo que consumen el render y la UI.
                    Dim aa = Transform_Class.Matrix33ToBSRotation(accRot)
                    nt.RotX = aa.X : nt.RotY = aa.Y : nt.RotZ = aa.Z : nt.HasRotation = True
                    ' Y LA MATRIZ COMPUESTA SE GUARDA CRUDA, para escribirla SIN pasar por axis-angle.
                    ' Motivo: la vuelta matriz→axis-angle→matriz NO es fiel en los casos degenerados. A 180 grados la
                    ' matriz es simetrica, los tres terminos del eje se anulan y el fallback elige el eje X — o sea
                    ' que cualquier rotacion de 180 sobre otro eje volveria como 180 sobre X. Y una REFLEXION
                    ' (det = -1) pasa el chequeo de ortonormalidad (que mira normas y ortogonalidad, no el
                    ' determinante) y se convertiria en rotacion propia. Como el strip saca la rotacion ajena, hay
                    ' que reescribirla SIEMPRE, asi que se reescriben LOS MISMOS 9 FLOATS que se leyeron (y para
                    ' varias capas, el producto exacto).
                    nt.RotMatrixRaw = New Single() {accRot.M11, accRot.M12, accRot.M13,
                                                   accRot.M21, accRot.M22, accRot.M23,
                                                   accRot.M31, accRot.M32, accRot.M33}
                End If
                ' LA ESCALA EFECTIVA ES EL PRODUCTO, Y EL `scaleMode` DEL ARCHIVO ES **INERTE**.
                ' El motivo es un BUG DE INDICE EN skee64, verificado en el fuente:
                '   · `OverrideVariant()` inicializa `index(-1)` (OverrideVariant.h:16) y `operator<` compara
                '     (key, index) sobre un `std::set` (OverrideVariant.h:19, OverrideInterface.h:28).
                '   · La composicion busca el modo con un OverrideVariant DEFAULT ⇒ busca (33, **-1**)
                '     (NiTransformInterface.cpp:667-670).
                '   · Todos los caminos lo ALMACENAN en indice 0 (`PackValue<UInt32>(…, …ScaleMode, 0, …)`,
                '     NiTransformInterface.cpp:1047; idem :1000/:1083/:1135 y el loader del .jslot).
                '   · La ESCALA, en cambio, se busca con `value.index = 0` explicito (:784) — o sea que ahi el
                '     autor de skee si puso el indice, y en el modo se le paso.
                ' ⇒ `find((33,-1))` nunca matchea `(33,0)`: el modo POR NODO no se lee nunca y `scaleMode` se queda
                ' en `g_scaleMode`, el `[General] iScaleMode` del JUGADOR (main.cpp:144, :797), cuyo default es 0 =
                ' multiplicativo. Interpretar el key-33 del archivo calculaba un numero que el motor NO aplica.
                ' RESIDUO QUE NO PODEMOS CERRAR, y hay que decirlo: si el jugador pone `iScaleMode` != 0, TODOS
                ' los node transforms se componen con otra ley (con 2, cada capa suma su escala — y una capa sin
                ' key-30 suma 1.0), y NO existe ninguna key que podamos escribir para evitarlo, porque justamente la
                ' que serviria es la que el motor no lee. Queda fuera de nuestro alcance, no disimulado.
                If nt.HasScale Then nt.Scale = accScale
                j.NodeTransforms.Add(nt)
            Next
        End If
        ' skinOverrides — top-level array [{ firstPerson, slotMask, values:[{key,type,index,data}] }]. RaceMenu
        ' body-paint / skin texture-tint per biped slot. We decode the diffuse/normal/tint (same value table as
        ' overlays) and keep the full element in Raw for round-trip.
        If root("skinOverrides") IsNot Nothing Then
            j._hadSkinOverrides = True
            For Each so In AsArray(root("skinOverrides"))
                Dim o = TryCast(so, JsonObject) : If o Is Nothing Then Continue For
                ' LOS DE PRIMERA PERSONA SE SALTEAN, IGUAL QUE EN transforms. No mirar `firstPerson` acá tiene
                ' tres consecuencias:
                '   1) un preset con override de 1ª y de 3ª sobre el MISMO slotMask da DOS entradas del mismo slot:
                '      el editor edita una y la otra se queda con el valor del autor;
                '   2) el sidecar no persiste el flag, así que al reabrir la app el elemento se reconstruye con
                '      `firstPerson:false` ⇒ un body-paint de PRIMERA persona termina aplicado al cuerpo de tercera;
                '   3) el archivo re-emite `firstPerson:true` mientras el apply-script escribe siempre `false`
                '      (`AddSkinOverride*(self, …, false, …)`), o sea archivo ≠ ESP en el mismo array.
                ' Ningún preset del corpus instalado tiene un skin override de primera persona (0 de 41), así que
                ' esto está razonado en el código y no medido sobre datos reales.
                Dim soFp = o("firstPerson")
                If soFp IsNot Nothing Then
                    Dim isFp As Boolean = False
                    Try : isFp = soFp.GetValue(Of Boolean)() : Catch : isFp = False : End Try
                    If isFp Then
                        ' Se GUARDA antes de saltearlo: no se modela, pero tiene que volver a salir.
                        j._firstPersonSkinRaw.Add(JsonNode.Parse(o.ToJsonString()))
                        Continue For
                    End If
                End If
                Dim sk As New JslotSkinOverride With {.SlotMask = CUInt(GetLong(o("slotMask")) And &HFFFFFFFFL), .DiffusePath = "", .NormalPath = "", .Raw = JsonNode.Parse(o.ToJsonString())}
                For Each v In AsArray(o("values"))
                    Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                    Dim key = GetInt(vo("key")), vtype = GetInt(vo("type")), index = GetInt(vo("index"))
                    If key = 9 AndAlso vtype = 2 Then
                        ' Every kParam_ShaderTexture slot, not just diffuse/normal (skee replaces each in place).
                        Dim path = GetStr(vo("data"))
                        sk.Slots(index) = path
                        If index = 0 Then sk.DiffusePath = path
                        If index = 1 Then sk.NormalPath = path
                    ElseIf key = 8 AndAlso vtype = 4 Then
                        sk.Alpha = CSng(GetDbl(vo("data"))) : sk.HasAlpha = True
                        sk.AlphaIndex = index   ' se re-emite EN SU índice; ver JslotOverlayNode.TintIndex
                    ElseIf key = 7 AndAlso vtype = 3 Then
                        Dim u As UInteger = CUInt(GetLong(vo("data")) And &HFFFFFFFFL)
                        sk.TintA = ((u >> 24) And &HFF) / 255.0F : sk.TintR = ((u >> 16) And &HFF) / 255.0F
                        sk.TintG = ((u >> 8) And &HFF) / 255.0F : sk.TintB = (u And &HFF) / 255.0F
                        sk.HasTint = True
                        sk.TintIndex = index
                    End If
                Next
                j.SkinOverrides.Add(sk)
            Next
        End If
        Return j
    End Function

    ''' <summary>Set (or add) the uniform scale of a skeleton node transform. Used by the Edit Body SSE
    ''' "Body scale" sliders. Creates a fresh RaceMenu transform element (key 30 float) when the node has
    ''' none; otherwise patches the modeled scale (the raw element is patched on Save).</summary>
    Public Sub SetNodeScale(nodeName As String, scale As Single)
        If String.IsNullOrEmpty(nodeName) Then Return
        Dim nt = NodeTransforms.FirstOrDefault(Function(x) x IsNot Nothing AndAlso String.Equals(x.NodeName, nodeName, StringComparison.OrdinalIgnoreCase))
        If nt Is Nothing Then
            nt = New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True}
            nt.Raw = BuildTransformRaw(nt)
            NodeTransforms.Add(nt)
        Else
            nt.Scale = scale : nt.HasScale = True
        End If
    End Sub

    ''' <summary>True when a node name matches the skee64 overlay convention: <c>Body/Hands/Feet</c>
    ''' followed by a <c>[Ovl{n}]</c> or <c>[SOvl{n}]</c> bracket (OverlayInterface.h:23-46). Case-
    ''' insensitive on the prefix; the digit is not validated beyond the bracket shape.</summary>
    Friend Shared Function IsOverlayNodeName(nodeName As String) As Boolean
        If String.IsNullOrEmpty(nodeName) Then Return False
        ' Face MUST be here: RaceMenu face paint lives on "Face [Ovl{n}]" nodes, and the app's face editor / render
        ' / bake all read those nodes out of the same Overlays list. Excluding Face (as an earlier version did) sent
        ' loaded face-paint nodes to the verbatim-preserve bucket, so a real preset's face paint round-tripped in the
        ' FILE but was invisible to the editor and never rendered/baked.
        Return System.Text.RegularExpressions.Regex.IsMatch(
            nodeName, "^(Body|Hands|Feet|Face) \[S?Ovl\d+\]$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
    End Function

    ''' <summary>Decode one overlay <c>values</c> array per the §3.1 table. Recognized entries:
    '''   {key:9,type:2,index:0}=diffuse · {key:9,type:2,index:1}=normal · {key:7,type:3,index:-1}=tint
    ''' (signed 0xAARRGGBB). Unrecognized entries are ignored (e.g. TextureSet key 6, Alpha key 8).</summary>
    Private Shared Function DecodeOverlayNode(nodeName As String, valuesNode As JsonNode) As JslotOverlayNode
        Dim node As New JslotOverlayNode With {.NodeName = nodeName, .DiffusePath = "", .NormalPath = ""}
        ' Keep the whole values array so Save re-emits any UNMODELED entry (texture slots >=2, key 6, keys 0-5).
        If valuesNode IsNot Nothing Then node.RawValues = JsonNode.Parse(valuesNode.ToJsonString())
        For Each v In AsArray(valuesNode)
            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
            Dim key = GetInt(vo("key"))
            Dim vtype = GetInt(vo("type"))
            Dim index = GetInt(vo("index"))
            If key = 9 AndAlso vtype = 2 Then
                ' Texture slot (string). index 0 = diffuse, 1 = normal.
                Dim path = GetStr(vo("data"))
                If index = 0 Then
                    node.DiffusePath = path
                ElseIf index = 1 Then
                    node.NormalPath = path
                End If
            ElseIf key = 7 AndAlso vtype = 3 Then
                ' TintColor (signed 0xAARRGGBB int). Decode via the unsigned bit pattern.
                Dim u As UInteger = CUInt(GetLong(vo("data")) And &HFFFFFFFFL)
                node.TintA = ((u >> 24) And &HFF) / 255.0F
                node.TintR = ((u >> 16) And &HFF) / 255.0F
                node.TintG = ((u >> 8) And &HFF) / 255.0F
                node.TintB = (u And &HFF) / 255.0F
                node.HasTint = True
                node.TintIndex = index   ' se re-emite EN SU índice; ver JslotOverlayNode.TintIndex
            ElseIf key = 8 AndAlso vtype = 4 Then
                ' kParam_ShaderAlpha (OverrideVariant.h:41) — the overlay's opacity, distinct from the tint
                ' colour's alpha byte. Modeled (not ignored) so Save re-emits it instead of silently
                ' resetting every authored overlay to fully opaque.
                node.Alpha = CSng(GetDbl(vo("data")))
                node.HasAlpha = True
                node.AlphaIndex = index   ' idem: sin esto el Save apendaba un segundo alpha en index -1
            End If
        Next
        Return node
    End Function

    ''' <summary>Serialize back to .jslot JSON bytes, preserving verbatim the nodes we didn't model.</summary>
    Public Function Save() As Byte()
        ' Fresh root; clone the unmodeled top-level nodes (transforms, version, mods, modNames, bodyMorphs, ...)
        ' verbatim so a load->save doesn't drop them. Rebuild the modeled nodes below. (Reusing the parsed
        ' _raw nodes directly throws on re-serialize — their frozen options conflict; DeepClone detaches them.)
        Dim root As New JsonObject()
        If _raw IsNot Nothing Then
            For Each kv In _raw
                Select Case kv.Key
                    Case "actor", "headParts", "faceTextures", "tintInfo", "morphs", "bodyMorphs", "overrides", "transforms", "skinOverrides" ' modeled — rebuilt below
                    Case Else : root(kv.Key) = If(kv.Value Is Nothing, Nothing, JsonNode.Parse(kv.Value.ToJsonString()))
                End Select
            Next
        End If
        ' `headTexture` SÓLO SI HAY ALGO QUE DECIR (o si el archivo cargado ya traía la key). Se emitía SIEMPRE, y
        ' con la cadena vacía cuando el preset no tenía override de texture set: MEDIDO con Tools\JslotRoundtripProbe,
        ' 8 de los 41 presets instalados ganaban una key que no tenían en un load→save sin tocar nada. Es la misma
        ' churn que ToGameTexturePath documenta evitar: cambiar el archivo de otro sin necesidad. El orden de las
        ' claves se mantiene (hairColor, headTexture, weight) para que el diff sea vacío de verdad, no "equivalente".
        Dim hadHeadTexture = False, headTextureWasNull = False
        If _raw IsNot Nothing Then
            Dim rawActor = TryCast(_raw("actor"), JsonObject)
            If rawActor IsNot Nothing AndAlso rawActor.ContainsKey("headTexture") Then
                hadHeadTexture = True
                headTextureWasNull = rawActor("headTexture") Is Nothing
            End If
        End If
        ' `hairColor` ES EL MISMO CASO QUE headTexture, y éste sí tiene consecuencia SEMÁNTICA en nuestro propio
        ' camino. skee lo emite sólo `if (hairColor)` (PresetInterface.cpp:675-677), o sea que un preset legítimo
        ' puede no traerlo. Emitiéndolo SIEMPRE, un preset sin la key salía con `"hairColor": 0`, y al recargarlo
        ' nuestro decode veía la key presente ⇒ `HadHairColor=True` ⇒ `SseHairColorRgb = 0` ⇒ **pelo NEGRO forzado**
        ' en vez de caer al CLFM del NPC (RaceMenuPresetMapper: `If(j.HadHairColor, …, Nothing)`).
        ' `HadHairColor` ya es exactamente el flag que hace falta: el decode lo prende por PRESENCIA (0 es un negro
        ' legítimo) y el mapper lo prende cuando tiene un RGB real (del preset o del CLFM efectivo).
        Dim actor As New JsonObject From {}
        If HadHairColor Then actor("hairColor") = HairColor
        If Not String.IsNullOrEmpty(HeadTexture) Then
            actor("headTexture") = HeadTexture
        ElseIf hadHeadTexture Then
            ' `null` NO ES `""`. RaceMenu escribe `"headTexture": null` cuando el preset no overridea el texture
            ' set; convertirlo en cadena vacía mueve bytes sin ganar nada (medido en 3 presets del corpus). Sin
            ' dato propio, se devuelve la forma que traía el archivo.
            actor("headTexture") = If(headTextureWasNull, Nothing, JsonValue.Create(""))
        End If
        ' `weight` se emite SIEMPRE, porque el motor lo lee SIN gate: `presetData->weight =
        ' headData["weight"].asFloat()` (skee64 PresetInterface.cpp:1019 — una key ausente devuelve 0.0) y lo
        ' aplica incondicionalmente (`npc->weight = presetData->weight`, :174). Su propio escritor la emite
        ' siempre (:672), o sea que un preset SIN la key no es una forma válida del formato: en RaceMenu deja el
        ' peso en 0. Omitirla no "preserva", adelgaza al actor.
        ' ⛔ NO gatearlo por HadWeight: ese gate (no inyectar `weight: 0` en un preset que nunca la tuvo) sirve
        ' para un round-trip VERBATIM, que esta app no hace — el único camino de guardado es
        ' `MainForm.BuildPresetFromState`, que siempre setea SseWeight (fallback 100.0F).
        actor("weight") = Weight
        root("actor") = actor
        Dim hpArr As New JsonArray()
        ' La key `formIdentifier` se emite SÓLO si tiene contenido, y no es una preferencia: es la única
        ' forma que el LECTOR canónico sabe leer.
        '   · skee ramifica por PRESENCIA, no por valor: `if (part.isMember("formIdentifier"))`
        '     (PresetInterface.cpp:979). Con la key presente pero vacía cae en `GetFormFromIdentifier("")`,
        '     que falla, y el head part se DESCARTA — dejando además INALCANZABLE su propio fallback por
        '     `formId` (:988-1002).
        '   · skee tampoco produce nunca una key vacía: su exportador sólo mete en la lista los head parts
        '     cuyo mod resolvió (`GetModInfoByFormID`, :357-364) antes de escribir el par formId/
        '     formIdentifier (:415-416). O sea que "entrada sin identifier" existe en el formato SÓLO como
        '     la forma legacy `{formId, type}` — que es justamente la que :988 sabe resolver por tabla.
        ' ⇒ Omitir la key hace que una entrada que no pudimos resolver round-trippee EXACTAMENTE como la
        ' forma que el motor sabe leer, en vez de una que le hace descartar el head part.
        ' El ORDEN de las keys se conserva (formId, formIdentifier, type) para que un preset con identifier
        ' salga byte-idéntico al original: reordenar mueve bytes en TODOS los archivos del usuario sin ganar nada.
        For Each hp In HeadParts
            Dim hpObj As New JsonObject From {{"formId", hp.FormId}}
            If Not String.IsNullOrEmpty(hp.FormIdentifier) Then hpObj("formIdentifier") = hp.FormIdentifier
            hpObj("type") = hp.Type
            hpArr.Add(hpObj)
        Next
        root("headParts") = hpArr
        Dim ftArr As New JsonArray()
        For Each ft In FaceTextures : ftArr.Add(New JsonObject From {{"index", ft.Index}, {"texture", ft.Texture}}) : Next
        root("faceTextures") = ftArr
        Dim tiArr As New JsonArray()
        For Each ti In TintInfo : tiArr.Add(New JsonObject From {{"color", ti.Color}, {"index", ti.Index}, {"texture", ti.Texture}}) : Next
        root("tintInfo") = tiArr
        Dim morphs As JsonObject = TryCast(root("morphs"), JsonObject)
        If morphs Is Nothing Then morphs = New JsonObject() : root("morphs") = morphs
        Dim def As New JsonObject()
        Dim slArr As New JsonArray()
        For Each s In SliderMorphs : slArr.Add(CDbl(s)) : Next
        def("morphs") = slArr
        ' NAMA face-part presets: emit the model when set (so an edit round-trips), else the verbatim node.
        If NamaPresets IsNot Nothing AndAlso NamaPresets.Count > 0 Then
            Dim prArr As New JsonArray()
            For Each pval In NamaPresets : prArr.Add(JsonValue.Create(pval)) : Next
            def("presets") = prArr
        ElseIf _morphsPresetsRaw IsNot Nothing Then
            def("presets") = JsonNode.Parse(_morphsPresetsRaw.ToJsonString())
        End If
        morphs("default") = def
        ' `custom` sólo si hay morphs custom, o si el archivo cargado ya traía la key. Emitir `[]` donde no había
        ' nada es la misma churn que headTexture="" (1 preset más de los 41 medidos): una key nueva en el archivo
        ' de otro, en un load→save que no tocó nada.
        Dim hadCustomMorphs = False, customMorphsWereNull = False
        If _raw IsNot Nothing Then
            Dim rawMorphs = TryCast(_raw("morphs"), JsonObject)
            If rawMorphs IsNot Nothing AndAlso rawMorphs.ContainsKey("custom") Then
                hadCustomMorphs = True
                customMorphsWereNull = rawMorphs("custom") Is Nothing
            End If
        End If
        If CustomMorphs.Count > 0 Then
            Dim cmArr As New JsonArray()
            For Each cm In CustomMorphs : cmArr.Add(New JsonObject From {{"name", cm.Name}, {"value", cm.Value}}) : Next
            morphs("custom") = cmArr
        ElseIf hadCustomMorphs Then
            ' Sin morphs custom propios se devuelve la forma que traía el archivo: `null` es `null` y `[]` es `[]`
            ' (RaceMenu escribe las dos). Convertir una en la otra es cambiar bytes ajenos sin ganar nada.
            morphs("custom") = If(customMorphsWereNull, Nothing, New JsonArray())
        End If
        morphs("sculptDivisor") = SculptDivisor
        Dim scArr As New JsonArray()
        For Each part In Sculpt
            Dim dataArr As New JsonArray()
            For i = 0 To part.Indices.Count - 1
                dataArr.Add(New JsonArray From {part.Indices(i), part.Dx(i), part.Dy(i), part.Dz(i)})
            Next
            ' Emit the per-shape "host" (chargen tri) + "vertices" so RaceMenu binds each block to the right
            ' geometry (head/brows/eyes/mouth). Preserved verbatim from the loaded preset for a faithful round-trip.
            Dim po As New JsonObject()
            If Not String.IsNullOrEmpty(part.Host) Then po("host") = part.Host
            If part.HadVertices OrElse part.Vertices > 0 Then po("vertices") = part.Vertices
            ' A sculpt block with no deltas carries no "data" key in a RaceMenu-authored preset; synthesising an
            ' empty array would make an untouched preset differ from its source.
            If dataArr.Count > 0 OrElse part.HadData Then po("data") = dataArr
            scArr.Add(po)
        Next
        morphs("sculpt") = scArr
        ' bodyMorphs — rebuilt explicitly (removed from the verbatim Case-Else above). Emitted when the
        ' preset actually has body morphs, or when the loaded file carried the node (faithful round-trip);
        ' a face-only preset that never had the node stays without it.
        If BodyMorphs.Count > 0 OrElse _hadBodyMorphs Then
            Dim bmArr As New JsonArray()
            For Each bm In BodyMorphs
                ' ACÁ COLAPSABA LOS APORTES EN UNA SOLA KEY NUESTRA CON EL TOTAL, y es la misma equivocación
                ' que la key 40 y que los firstPerson: el ARCHIVO no es el lugar donde se decide eso.
                ' El total lo manda el ESP, que es el único que necesita un número solo bajo un nombre que podamos
                ' barrer. El archivo es un documento: sale con el desglose por contribuyente, como vino. El motor
                ' SUMA las keys de un morph (Impl_GetBodyMorphs, BodyMorphInterface.cpp:220-240, default
                ' iBodyMorphMode=0), así que el desglose y el total rinden EXACTAMENTE lo mismo — y el desglose
                ' además conserva qué aportó cada uno y deja los sliders de BodySlide/RaceMenu funcionando.
                Dim keysArr As New JsonArray()
                For Each k In bm.Keys
                    keysArr.Add(New JsonObject From {{"key", k.Key}, {"value", CDbl(k.Value)}})
                Next
                bmArr.Add(New JsonObject From {{"name", bm.Name}, {"keys", keysArr}})
            Next
            root("bodyMorphs") = bmArr
        End If
        ' overrides — rebuilt explicitly: the decoded overlay nodes plus every non-overlay override node
        ' kept verbatim. Emitted when the preset has overlays, when there are preserved verbatim nodes, or
        ' when the loaded file carried the node (faithful round-trip); a preset that never had it stays without.
        If Overlays.Count > 0 OrElse _otherOverridesRaw.Count > 0 OrElse _hadOverrides Then
            Dim ovArr As New JsonArray()
            For Each ov In Overlays
                ovArr.Add(EncodeOverlayNode(ov))
            Next
            For Each raw In _otherOverridesRaw
                ovArr.Add(If(raw Is Nothing, Nothing, JsonNode.Parse(raw.ToJsonString())))
            Next
            root("overrides") = ovArr
        End If
        ' transforms — rebuilt from the modeled nodes: re-emit each node's Raw with the modeled TRS patched in and
        ' every unmodeled key untouched. Raw-less nodes (cloned across assemblies / rehydrated from the sidecar) are
        ' rebuilt fresh from the fields. Emitted when there are transforms or the loaded file carried the node.
        If NodeTransforms.Count > 0 OrElse _firstPersonTransformsRaw.Count > 0 OrElse _hadTransforms Then
            Dim trArr As New JsonArray()
            For Each nt In NodeTransforms
                If nt Is Nothing Then Continue For
                Dim raw = TryCast(If(nt.Raw Is Nothing, BuildTransformRaw(nt), JsonNode.Parse(nt.Raw.ToJsonString())), JsonObject)
                If raw Is Nothing Then Continue For
                Dim keys = TryCast(raw("keys"), JsonArray)
                If keys Is Nothing Then keys = New JsonArray() : raw("keys") = keys
                ' SE ESCRIBE **UNA** CAPA: la nuestra, con el TRS ya compuesto. De las capas ajenas se
                ' conservan solo los values de keys que NO son TRS (p.ej. 40 = node-destination), porque esas no
                ' entran en la composicion y tirarlas perderia comportamiento que no modelamos.
                ' POR QUE NO SE RE-EMITEN LAS TRS AJENAS: el modelo ya lleva su aporte COMPUESTO (el decode las
                ' colapso con la formula del motor). Dejarlas ademas de nuestro valor haria que el proximo import
                ' las contara DOS VECES — el preset se deformaria un poco mas en cada vuelta.
                keys = StripForeignTrsLayers(keys, nt.HasScale OrElse nt.HasPosition OrElse nt.HasRotation)
                raw("keys") = keys
                ' Scale (key 30): SIEMPRE en el ÍNDICE 0 y SIEMPRE dentro de NUESTRA capa.
                '   · El índice NO es agnóstico: `Impl_GetOverrideTransform` fija `value.index = 0` antes del find
                '     (NiTransformInterface.cpp:784), así que un (30, k≠0) queda invisible para el juego.
                '   · Patchear TODOS los key-30 del NODO es corrupción cuando hay varias capas nombradas (cada mod
                '     registra la suya): les escribe a todas el único valor del modelo. Por eso
                '     `LastTransformValue` busca sólo dentro de `OurTransformLayer`.
                ' ⚠️ En los 41 presets instalados no hay ningún nodo multi-key con key 30 ni un solo key 33: para
                ' estas dos, lo de arriba está razonado por construcción, no medido.
                If nt.HasScale Then PatchTransformValue(keys, 30, 4, 0, CDbl(nt.Scale))
                ' Position (key 31): per-component index 0/1/2, plain floats.
                ' "Exact round-trip" vale para el VALOR (el float32 que lee skee es el mismo bit a bit), NO para
                ' el TEXTO: jsoncpp imprime %.16g del double ensanchado y .NET imprime el shortest-roundtrip, así
                ' que `-0.1000000014901161` se re-escribe `-0.10000000149011612`. Medido: 302 números de los 41
                ' presets instalados. Sin efecto in-game (skee lee asFloat); el probe lo tapa con su epsilon.
                If nt.HasPosition Then
                    PatchTransformValue(keys, 31, 4, 0, CDbl(nt.PosX))
                    PatchTransformValue(keys, 31, 4, 1, CDbl(nt.PosY))
                    PatchTransformValue(keys, 31, 4, 2, CDbl(nt.PosZ))
                End If
                ' Rotation (key 32): se reescribe SIEMPRE que el nodo tenga rotación, igual que la escala y la
                ' posición. ⛔ NO gatearla por flags de "dirty": no se persisten en el sidecar, así que después de
                ' cerrar y reabrir la app valen False con el `Raw` vivo, y ahí:
                '   · una rotación que vino de una capa AJENA se pierde — el strip le saca los values 32 y el gate
                '     no los reescribe ⇒ el nodo sale con la capa vacía y SIN rotación;
                '   · una rotación EDITADA en la UI queda con la matriz VIEJA — `SetRotationFromUi` borra
                '     `RotMatrixRaw`, el `Raw` persistido conserva la vieja y el gate no la patchea ⇒ el .jslot
                '     re-emite la vieja mientras la UI y el ESP muestran la nueva, o sea `.jslot` ≠ herramienta.
                ' Reescribir siempre NO cuesta el byte-exacto: `RotationRowMajor` devuelve LOS MISMOS 9 floats que
                ' se leyeron cuando hay matriz cruda y la UI no la invalidó, así que es un no-op numérico.
                If nt.HasRotation Then
                    ' De la matriz CRUDA cuando la hay (los mismos 9 floats que se leyeron, o su producto exacto):
                    ' asi 180 grados y reflexiones sobreviven. Solo se reconstruye desde el axis-angle cuando la
                    ' rotacion la genero la UI, que es el caso donde el axis-angle ES la fuente.
                    Dim r = RotationRowMajor(nt)   ' UN solo dueño de la elección crudo-vs-axis-angle: ver su doc.
                    For i = 0 To 8 : PatchTransformValue(keys, 32, 4, i, CDbl(r(i))) : Next
                End If
                ' NO SE ESCRIBE key 33: el motor NUNCA lee el key-33 de un nodo (busca (33,-1) y todo lo almacena
                ' en (33,0) — ver el bloque del decode, con las citas), así que escribirlo es churn inerte en el
                ' archivo de otro. Lo que gobierna es `g_scaleMode`, global del jugador.
                trArr.Add(raw)
            Next
            ' Los elementos de primera persona vuelven tal cual: no se modelan, no se editan, no se pierden.
            For Each fp In _firstPersonTransformsRaw
                trArr.Add(If(fp Is Nothing, Nothing, JsonNode.Parse(fp.ToJsonString())))
            Next
            root("transforms") = trArr
        End If
        ' skinOverrides — rebuilt from the modeled nodes: re-emit each element's Raw with the (possibly edited)
        ' diffuse/normal texture + tint values patched. Emitted when there are skin overrides or the loaded file
        ' carried the node (faithful round-trip).
        If SkinOverrides.Count > 0 OrElse _firstPersonSkinRaw.Count > 0 OrElse _hadSkinOverrides Then
            Dim soArr As New JsonArray()
            For Each sk In SkinOverrides
                If sk Is Nothing Then Continue For
                Dim raw = TryCast(If(sk.Raw Is Nothing, Nothing, JsonNode.Parse(sk.Raw.ToJsonString())), JsonObject)
                Dim rebuilt = raw Is Nothing
                If rebuilt Then
                    ' Raw-less (e.g. after a sidecar round-trip, where the Friend Raw isn't persisted) → build a fresh
                    ' element from the modeled fields, including every slot ≥2 and alpha so nothing is dropped.
                    raw = New JsonObject From {{"firstPerson", False}, {"slotMask", CLng(sk.SlotMask)}, {"values", New JsonArray()}}
                End If
                Dim vals = TryCast(raw("values"), JsonArray)
                If vals Is Nothing Then vals = New JsonArray() : raw("values") = vals
                ' Repara los tipo-3 sin signo que dejó una versión anterior de esta app ANTES de parchear lo
                ' modelado, así también se sanea el tint que llega por la rama verbatim (checkbox destildado).
                NormalizeSignedIntValues(vals)
                PatchSkinValue(vals, 9, 2, 0, ToGameTexturePath(sk.DiffusePath), True)
                PatchSkinValue(vals, 9, 2, 1, ToGameTexturePath(sk.NormalPath), True)
                ' Higher texture slots (subsurface/specular/…) and alpha are the editor doesn't surface but skee
                ' applies. In the Raw path they survive verbatim in Raw (with their original index), so we only
                ' re-emit them when rebuilding from the model — avoids inventing a possibly-wrong index on values
                ' Raw already carries.
                If rebuilt Then
                    For Each kvp In sk.Slots
                        If kvp.Key >= 2 Then PatchSkinValue(vals, 9, 2, kvp.Key, ToGameTexturePath(kvp.Value), True)
                    Next
                    If sk.HasAlpha Then PatchSkinValue(vals, 8, 4, sk.AlphaIndex, CDbl(sk.Alpha), False)
                End If
                If sk.HasTint Then
                    Dim u As UInteger = (CUInt(Math.Round(Clamp01(sk.TintA) * 255)) << 24) Or (CUInt(Math.Round(Clamp01(sk.TintR) * 255)) << 16) Or (CUInt(Math.Round(Clamp01(sk.TintG) * 255)) << 8) Or CUInt(Math.Round(Clamp01(sk.TintB) * 255))
                    ' En el índice de ORIGEN, no en -1 cableado: ver JslotOverlayNode.TintIndex.
                    PatchSkinValue(vals, 7, 3, sk.TintIndex, SignedTintValue(u), False)
                End If
                soArr.Add(raw)
            Next
            ' Los de primera persona vuelven tal cual, igual que los `transforms` de primera persona: no se
            ' modelan, no se editan, no se pierden.
            For Each fp In _firstPersonSkinRaw
                soArr.Add(If(fp Is Nothing, Nothing, JsonNode.Parse(fp.ToJsonString())))
            Next
            root("skinOverrides") = soArr
        End If
        ' Header nodes RaceMenu REQUIRES to load the file at all — must be emitted last, after the verbatim
        ' _raw copy, so a fresh (built-from-NPC) preset is never missing them.
        EnsurePresetHeaders(root)
        Dim opt As New JsonSerializerOptions With {.WriteIndented = True, .TypeInfoResolver = New System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()}
        Return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString(opt))
    End Function

    ' skee64 preset header constants (PresetInterface.cpp:300-301). signature = MACRO_SWAP32('SKSE') =
    ' 0x45534B53; verified against every real RaceMenu-authored .jslot (version.signature == 1163086675).
    Private Const PresetSignature As Long = 1163086675L
    Private Const PresetFormatVersion As Integer = 3

    ''' <summary>Guarantee the three header nodes skee64's <c>LoadJsonPreset</c> validates before it will load a
    ''' preset (PresetInterface.cpp:925-954): a non-empty <c>version</c> whose <c>signature</c> == kSignature and
    ''' <c>formatVersion</c> &gt; 0, and at least one non-empty <c>mods</c>/<c>modNames</c>. A .jslot round-tripped
    ''' from disk keeps its own (copied verbatim from <see cref="_raw"/> before this runs); a preset built fresh from
    ''' an NPC has no <c>_raw</c>, so WITHOUT this the nodes are never emitted and RaceMenu rejects the file with
    ''' "No version header" / "No mods header" — the "Failed to load preset" the user sees. Only the MISSING nodes are
    ''' synthesised; anything the source file carried is left untouched.</summary>
    Private Sub EnsurePresetHeaders(root As JsonObject)
        ' version — required, signature+formatVersion validated (:925-946). skseVersion/runtimeVersion are read into
        ' the preset but never gated, so 0 is safe.
        Dim ver = TryCast(root("version"), JsonObject)
        If ver Is Nothing OrElse ver("signature") Is Nothing OrElse ver("formatVersion") Is Nothing Then
            root("version") = New JsonObject From {
                {"signature", PresetSignature}, {"formatVersion", PresetFormatVersion},
                {"skseVersion", 0}, {"runtimeVersion", 0}}
        End If
        ' mods/modNames — skee rejects only when BOTH are empty (:950). Our head parts carry a "formIdentifier", which
        ' RaceMenu resolves via GetFormFromIdentifier INDEPENDENT of this list (:979-987), so the list just has to be
        ' present + non-empty. Build it faithfully from the plugins our head parts / head texture reference (parity
        ' with SaveJsonPreset, :404-408); a body-only preset with no head-part deps falls back to Skyrim.esm (always
        ' in a Skyrim load order) so it still loads.
        Dim modsArr = TryCast(root("mods"), JsonArray)
        Dim modNamesArr = TryCast(root("modNames"), JsonArray)
        Dim haveMods = (modsArr IsNot Nothing AndAlso modsArr.Count > 0) OrElse (modNamesArr IsNot Nothing AndAlso modNamesArr.Count > 0)
        If Not haveMods Then
            Dim names = CollectDependencyPlugins()
            If names.Count = 0 Then names.Add("Skyrim.esm")
            Dim mn As New JsonArray()
            Dim md As New JsonArray()
            Dim idx As Integer = 0
            For Each n In names
                mn.Add(n)
                md.Add(New JsonObject From {{"index", idx}, {"name", n}})
                idx += 1
            Next
            root("modNames") = mn
            root("mods") = md
        End If
    End Sub

    ''' <summary>Distinct plugin filenames referenced by the modeled head parts + head texture (the "Plugin" part of
    ''' each "Plugin|FormID" formIdentifier), in first-seen order. Feeds the synthesised <c>mods</c>/<c>modNames</c>
    ''' header — the set of plugin dependencies RaceMenu itself lists for a preset (PresetInterface.cpp:368-378).</summary>
    Private Function CollectDependencyPlugins() As List(Of String)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim outp As New List(Of String)
        For Each hp In HeadParts
            If hp Is Nothing Then Continue For
            Dim plug = PluginFromIdentifier(hp.FormIdentifier)
            If plug.Length > 0 AndAlso seen.Add(plug) Then outp.Add(plug)
        Next
        Dim htPlug = PluginFromIdentifier(HeadTexture)
        If htPlug.Length > 0 AndAlso seen.Add(htPlug) Then outp.Add(htPlug)
        Return outp
    End Function

    ''' <summary>The plugin filename portion of a "Plugin|FormID" formIdentifier (empty when malformed/blank).</summary>
    Private Shared Function PluginFromIdentifier(ident As String) As String
        If String.IsNullOrEmpty(ident) Then Return ""
        Dim bar = ident.IndexOf("|"c)
        If bar <= 0 Then Return ""
        Return ident.Substring(0, bar).Trim()
    End Function

    ''' <summary>Flatten <see cref="BodyMorphs"/> to the render slider dict: one entry per morph name
    ''' whose value is the SUM of that morph's keyed contributions (case-insensitive names; the engine
    ''' nets keyed values — skee64 BodyMorphInterface.h:70-75). Feeds the same slider dict the
    ''' BodySlide morph resolver consumes.</summary>
    Public Function BodyMorphsToFlatSliderDict() As Dictionary(Of String, Single)
        Dim d As New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        For Each bm In BodyMorphs
            If bm Is Nothing OrElse String.IsNullOrEmpty(bm.Name) Then Continue For
            Dim sum As Single = 0.0F
            If bm.Keys IsNot Nothing Then
                For Each k In bm.Keys : sum += k.Value : Next
            End If
            Dim existing As Single
            If d.TryGetValue(bm.Name, existing) Then
                d(bm.Name) = existing + sum
            Else
                d(bm.Name) = sum
            End If
        Next
        Return d
    End Function

    ''' <summary>Encode a <see cref="JslotOverlayNode"/> back to a skee64 <c>overrides</c> element:
    ''' <c>{ node, values:[…] }</c>. Each override key is emitted only when the node actually carries it —
    ''' tint (7) when <see cref="JslotOverlayNode.HasTint"/>, alpha (8) when <see cref="JslotOverlayNode.HasAlpha"/>,
    ''' and the texture entries (9) when a path is set — because RaceMenu writes tint-only and alpha-only
    ''' overlays. Tint packs to a signed 0xAARRGGBB int (the JSON's native signed form).</summary>
    Private Shared Function EncodeOverlayNode(ov As JslotOverlayNode) As JsonObject
        ' Start from the ORIGINAL values array when we have it (RawValues) so unmodeled entries (texture slots >=2,
        ' key 6 TextureSet, keys 0-5) and the original ordering survive; else build fresh. Then patch the modeled
        ' keys: tint (7), alpha (8), diffuse (9/0), normal (9/1) — adding, updating or removing each in place.
        Dim valuesArr As JsonArray = TryCast(If(ov.RawValues Is Nothing, Nothing, JsonNode.Parse(ov.RawValues.ToJsonString())), JsonArray)
        Dim fresh = valuesArr Is Nothing
        If fresh Then valuesArr = New JsonArray()
        ' Mismo saneo que en skinOverrides: un tipo-3 sin signo heredado de una versión anterior se repara aunque
        ' la key no se re-emita desde el modelo. Ver SignedTintValue.
        NormalizeSignedIntValues(valuesArr)

        If ov.HasTint Then
            Dim a As UInteger = ClampToByte(ov.TintA)
            Dim r As UInteger = ClampToByte(ov.TintR)
            Dim g As UInteger = ClampToByte(ov.TintG)
            Dim b As UInteger = ClampToByte(ov.TintB)
            Dim u As UInteger = (a << 24) Or (r << 16) Or (g << 8) Or b
            ' En el índice de ORIGEN, no en -1 cableado: ver JslotOverlayNode.TintIndex.
            PatchOverlayValue(valuesArr, 7, 3, ov.TintIndex, SignedTintValue(u), isString:=False)
        Else
            RemoveOverlayKey(valuesArr, 7, Nothing)
        End If
        If ov.HasAlpha Then
            PatchOverlayValue(valuesArr, 8, 4, ov.AlphaIndex, CDbl(ov.Alpha), isString:=False)
        Else
            RemoveOverlayKey(valuesArr, 8, Nothing)
        End If
        ' Texture slots: emit an override only when there IS a path (RaceMenu writes tint-only / alpha-only overlays);
        ' an empty modeled path removes just that slot (0 or 1) while leaving any unmodeled slot >=2 untouched.
        If Not String.IsNullOrEmpty(ov.DiffusePath) Then
            PatchOverlayValue(valuesArr, 9, 2, 0, ToGameTexturePath(ov.DiffusePath), isString:=True)
        Else
            RemoveOverlayKey(valuesArr, 9, 0)
        End If
        If Not String.IsNullOrEmpty(ov.NormalPath) Then
            PatchOverlayValue(valuesArr, 9, 2, 1, ToGameTexturePath(ov.NormalPath), isString:=True)
        Else
            RemoveOverlayKey(valuesArr, 9, 1)
        End If
        Return New JsonObject From {{"node", If(ov.NodeName, "")}, {"values", valuesArr}}
    End Function

    ''' <summary>Patch (update in place) or append an overlay value matching (<paramref name="key"/>,
    ''' <paramref name="index"/>). <paramref name="isString"/> true → data is a texture path; false and type 4 →
    ''' Double; false and type 3 → the signed tint int.</summary>
    Private Shared Sub PatchOverlayValue(vals As JsonArray, key As Integer, vtype As Integer, index As Integer, data As Object, isString As Boolean)
        Dim jval As JsonNode = If(isString, JsonValue.Create(CStr(data)),
                                  If(vtype = 4, JsonValue.Create(CDbl(data)), JsonValue.Create(CInt(data))))
        For Each v In vals
            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
            If GetInt(vo("key")) = key AndAlso GetInt(vo("index")) = index Then
                vo("type") = vtype : vo("data") = jval : Return
            End If
        Next
        vals.Add(New JsonObject From {{"key", key}, {"type", vtype}, {"index", index}, {"data", jval}})
    End Sub

    ''' <summary>Remove overlay value entries for <paramref name="key"/> (all indices when <paramref name="index"/>
    ''' is Nothing, else only that index). Used to drop a modeled key that is no longer set.</summary>
    Private Shared Sub RemoveOverlayKey(vals As JsonArray, key As Integer, index As Integer?)
        For i = vals.Count - 1 To 0 Step -1
            Dim vo = TryCast(vals(i), JsonObject) : If vo Is Nothing Then Continue For
            If GetInt(vo("key")) = key AndAlso (Not index.HasValue OrElse GetInt(vo("index")) = index.Value) Then vals.RemoveAt(i)
        Next
    End Sub

    Private Shared Function ClampToByte(v As Single) As UInteger
        Dim n = CInt(Math.Round(v * 255.0F))
        Return CUInt(Math.Min(255, Math.Max(0, n)))
    End Function

    ' ---- JSON helpers (null-safe scalar reads) ----
    Private Shared Function AsArray(n As JsonNode) As JsonArray
        Return If(TryCast(n, JsonArray), New JsonArray())
    End Function
    Private Shared Function GetInt(n As JsonNode) As Integer
        Try : Return If(n Is Nothing, 0, n.GetValue(Of Integer)()) : Catch : Try : Return CInt(n.GetValue(Of Double)()) : Catch : Return 0 : End Try : End Try
    End Function
    Private Shared Function GetLong(n As JsonNode) As Long
        Try : Return If(n Is Nothing, 0L, n.GetValue(Of Long)()) : Catch : Try : Return CLng(n.GetValue(Of Double)()) : Catch : Return 0L : End Try : End Try
    End Function
    Private Shared Function GetDbl(n As JsonNode) As Double
        Try : Return If(n Is Nothing, 0.0, n.GetValue(Of Double)()) : Catch : Return 0.0 : End Try
    End Function
    Private Shared Function GetStr(n As JsonNode) As String
        Try : Return If(n Is Nothing, "", n.GetValue(Of String)()) : Catch : Return "" : End Try
    End Function

    ''' <summary>A texture path in a form the engine can resolve. RaceMenu-authored presets use BOTH
    ''' <c>Actors\Character\Overlays\x.dds</c> and <c>textures\actors\character\overlays\x.dds</c> (measured: 19
    ''' vs 2 across the installed presets), and skee64's own default is the prefixed form
    ''' (<c>skee64.ini sDefaultTexture</c>) — so the engine accepts either and neither may be rewritten into the
    ''' other: doing so would churn every preset we touch.
    '''
    ''' What this DOES fix is an absolute disk path (<c>F:\…\Data\textures\…</c>), which is what a file dialog
    ''' hands back. Such a path renders fine in our own preview — the renderer normalizes it via
    ''' <see cref="FO4UnifiedMaterial_Class.CorrectTexturePath"/> — but is dead in-game, so it must never reach a
    ''' preset. Any already-relative path is returned verbatim.
    '''
    ''' <para>UN SOLO SEPARADOR AL PRINCIPIO **NO** ES UNA RUTA ABSOLUTA. El test decía
    ''' <c>p.StartsWith("\")</c> y metía en la misma bolsa <c>F:\…\Data\textures\x.dds</c> (absoluta, muerta
    ''' in-game) con <c>\SL Survival\spanky\x.dds</c> (relativa, con un separador de más — forma que usan presets
    ''' reales). MEDIDO con <c>Tools\JslotRoundtripProbe</c>: 3 de los 41 presets instalados se reescribían solos en
    ''' un load→save sin tocar nada, justo el "churn every preset we touch" que este docstring dice evitar. La ruta
    ''' resultante es equivalente, pero cambiar bytes del preset de otro sin necesidad no es gratis: ensucia
    ''' cualquier diff y borra la forma que el autor eligió.</para>
    ''' <para>Absoluto de verdad = con letra de unidad (<c>:</c>) o UNC (<c>\\host</c>). TODO lo demás viaja
    ''' VERBATIM, incluido el separador inicial suelto. (Primer intento de este arreglo lo sacaba "porque no
    ''' aporta": seguía siendo cambiar el archivo de otro sin necesidad, o sea el mismo defecto con menos bytes.
    ''' Si esa forma renderiza y resuelve, no es nuestra para normalizar; y si no resolviera, es una propiedad del
    ''' preset del autor, no algo que arreglemos en silencio al guardar.)</para></summary>
    Public Shared Function ToGameTexturePath(path As String) As String
        If String.IsNullOrWhiteSpace(path) Then Return ""
        Dim p = path.Trim()
        Dim isUnc = p.StartsWith("\\") OrElse p.StartsWith("//")
        Dim isRooted = p.Contains(":"c) OrElse isUnc
        If Not isRooted Then Return p
        Return FO4UnifiedMaterial_Class.CorrectTexturePath(p)   ' → "textures\…"
    End Function
End Class
