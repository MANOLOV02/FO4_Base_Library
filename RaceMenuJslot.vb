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
        Public Property DiffusePath As String
        Public Property NormalPath As String
        ''' <summary>Tint RGBA, each 0..1. Only meaningful when <see cref="HasTint"/> (a missing TintColor
        ''' override leaves the material's own base color). A=opacity (skee treats A=0 as opaque/unspecified).</summary>
        Public Property TintR As Single
        Public Property TintG As Single
        Public Property TintB As Single
        Public Property TintA As Single
        Public Property HasTint As Boolean
        ''' <summary>skee64 <c>kParam_ShaderAlpha</c> (key 8, float — OverrideVariant.h:36). This is the
        ''' overlay's OPACITY; it is a separate override from the tint colour's alpha byte. Every overlay in
        ''' a RaceMenu-authored preset carries one, so it must round-trip or the overlay reloads fully opaque.</summary>
        Public Property Alpha As Single = 1.0F
        Public Property HasAlpha As Boolean
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

    ''' <summary>One RaceMenu NiOverride node transform (NiTransformInterface). RaceMenu's "body scale" sliders
    ''' scale skeleton nodes (e.g. "NPC L Breast", "NPC R Butt", "NPC BellyScaleTarget") via key
    ''' <c>kParam_NodeTransformScale</c> (30, float). We MODEL the scalar <see cref="Scale"/> (the editable body
    ''' scale) and keep the full original node object in <see cref="Raw"/> so a load→save round-trips any
    ''' position/rotation/scaleMode keys byte-faithfully — Save re-emits Raw with the scale value patched.
    ''' Schema: skee64 PresetInterface.cpp:559-593.</summary>
    Public Class JslotNodeTransform
        Public Property NodeName As String
        ''' <summary>Uniform node scale (kParam_NodeTransformScale = key 30, float @ index 0). 1.0 = unscaled.</summary>
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
        ''' default 0. A single app-authored override layer renders identically for any mode, so it is carried,
        ''' not interpreted.</summary>
        Public Property ScaleMode As Integer
        Public Property HasScaleMode As Boolean
        ''' <summary>Set by a UI rotation edit; gates whether Save rebuilds the key-32 matrix from the modeled
        ''' axis-angle (avoids matrix→axis-angle→matrix float drift for rotations the user never touched). Public so
        ''' the app's Edit Body rotation sliders (a separate assembly) can flag an edit.</summary>
        Public Property RotationDirty As Boolean
        ''' <summary>The verbatim original transform element ({firstPerson, node, keys}); Save re-emits it with the
        ''' modeled scale/position/scaleMode (and rotation when dirty) patched in and every other key untouched.
        ''' Nothing for a UI/sidecar-created transform (Save builds a fresh element from the modeled fields).</summary>
        Friend Raw As JsonNode

        ''' <summary>Deep-clone (detaches Raw JSON). Public so cross-assembly carriers (LooksmenuPreset,
        ''' sidecar) can copy the modeled transform without touching the Friend Raw directly.</summary>
        Public Function Clone() As JslotNodeTransform
            Return New JslotNodeTransform With {
                .NodeName = NodeName, .Scale = Scale, .HasScale = HasScale,
                .PosX = PosX, .PosY = PosY, .PosZ = PosZ, .HasPosition = HasPosition,
                .RotX = RotX, .RotY = RotY, .RotZ = RotZ, .HasRotation = HasRotation, .RotationDirty = RotationDirty,
                .ScaleMode = ScaleMode, .HasScaleMode = HasScaleMode,
                .Raw = If(Raw Is Nothing, Nothing, JsonNode.Parse(Raw.ToJsonString()))}
        End Function

        ''' <summary>True when every modeled component is at (or absent from) its identity — used to decide whether
        ''' the node is worth applying to the pose / persisting to the sidecar.</summary>
        Public ReadOnly Property IsIdentity As Boolean
            Get
                Dim scaleId = (Not HasScale) OrElse Math.Abs(Scale - 1.0F) < 0.00001F
                Dim posId = (Not HasPosition) OrElse (Math.Abs(PosX) < 0.00001F AndAlso Math.Abs(PosY) < 0.00001F AndAlso Math.Abs(PosZ) < 0.00001F)
                Dim rotId = (Not HasRotation) OrElse (Math.Abs(RotX) < 0.000001F AndAlso Math.Abs(RotY) < 0.000001F AndAlso Math.Abs(RotZ) < 0.000001F)
                Return scaleId AndAlso posId AndAlso rotId
            End Get
        End Property
    End Class

    ''' <summary>Factory for a UI/sidecar-created scale-only node transform (node name + uniform scale).
    ''' Builds the Raw element so a later Save round-trips it. Public so the app's sidecar hydrate can
    ''' rebuild the carrier from a stored node→scale map (legacy scale-only sidecars, schema &lt; 10).</summary>
    Public Shared Function MakeScaleTransform(nodeName As String, scale As Single) As JslotNodeTransform
        Return New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True, .Raw = BuildTransformRaw(New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True})}
    End Function

    ''' <summary>Build a fresh RaceMenu transform element {firstPerson:false, node, keys:[{name:RSMTransform,
    ''' values:[…]}]} from the modeled fields, for a UI/sidecar-created (Raw-less) transform. Emits keys 30
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
            Dim r = MatrixRowMajor(Transform_Class.BSRotationToMatrix33(New System.Numerics.Vector3(nt.RotX, nt.RotY, nt.RotZ)))
            For i = 0 To 8 : vals.Add(TransformValueNode(32, 4, i, CDbl(r(i)))) : Next
        End If
        If nt.HasScaleMode Then vals.Add(TransformValueNode(33, 3, 0, CLng(nt.ScaleMode)))
        ' A wholly-empty transform still carries the scale key (keeps parity with the legacy scale-only build).
        If vals.Count = 0 Then vals.Add(TransformValueNode(30, 4, 0, CDbl(nt.Scale)))
        Return New JsonObject From {
            {"firstPerson", False}, {"node", nt.NodeName},
            {"keys", New JsonArray From {New JsonObject From {{"name", "RSMTransform"}, {"values", vals}}}}}
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
    '''
    ''' <para>Exists so the Papyrus apply-script emitter can hand skee64 the exact float sequence skee64
    ''' itself round-trips, instead of re-deriving one. <c>NiOverride.AddNodeTransformRotation</c> accepts
    ''' EITHER 3 euler angles in degrees OR these 9 raw matrix floats, which it copies straight into
    ''' <c>NiMatrix33::arr[i]</c> (PapyrusNiOverride.cpp:1190-1193) — the same <c>arr[i]</c> it later packs
    ''' out under key 32 index i. Feeding the 9 floats therefore needs NO euler convention at all: whatever
    ''' order skee means by "row-major" internally, we are giving back the values it produced. The 3-float
    ''' euler path would instead depend on <c>NiMatrix33::SetEulerAngles</c>'s heading/attitude/bank order
    ''' matching ours — an assumption we do not need to make, so we do not make it.</para></summary>
    Public Shared Function RotationRowMajor(nt As JslotNodeTransform) As Single()
        If nt Is Nothing OrElse Not nt.HasRotation Then Return Nothing
        Return MatrixRowMajor(Transform_Class.BSRotationToMatrix33(
            New System.Numerics.Vector3(nt.RotX, nt.RotY, nt.RotZ)))
    End Function

    ''' <summary>Patch (or append) a transform value matching (<paramref name="key"/>, <paramref name="index"/>)
    ''' within the element's <c>keys</c> layers: update the data in place if the (key,index) already exists in any
    ''' layer, else append a fresh value to the first key layer (creating one if the element has none). Used by Save
    ''' to write the per-component position/rotation values while leaving every other key untouched.</summary>
    Private Shared Sub PatchTransformValue(keys As JsonArray, key As Integer, vtype As Integer, index As Integer, data As Object)
        For Each k In keys
            Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
            Dim kv = TryCast(ko("values"), JsonArray) : If kv Is Nothing Then Continue For
            For Each v In kv
                Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                If GetInt(vo("key")) = key AndAlso GetInt(vo("index")) = index Then
                    vo("type") = vtype
                    vo("data") = If(vtype = 4, JsonValue.Create(CDbl(data)), JsonValue.Create(CLng(data)))
                    Return
                End If
            Next
        Next
        Dim first = TryCast(If(keys.Count > 0, keys(0), Nothing), JsonObject)
        If first Is Nothing Then
            first = New JsonObject From {{"name", "RSMTransform"}, {"values", New JsonArray()}}
            keys.Add(first)
        End If
        Dim vals = TryCast(first("values"), JsonArray)
        If vals Is Nothing Then vals = New JsonArray() : first("values") = vals
        vals.Add(TransformValueNode(key, vtype, index, data))
    End Sub

    Private Shared Function Clamp01(v As Single) As Single
        If v < 0.0F Then Return 0.0F
        If v > 1.0F Then Return 1.0F
        Return v
    End Function

    ''' <summary>Patch (or append) a skinOverride value element matching key/type/index. <paramref name="isString"/>
    ''' true → data is the texture path (skip when empty); false → data is a numeric (tint uint as Long).</summary>
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
                                  If(vtype = 4, JsonValue.Create(CDbl(data)), JsonValue.Create(CLng(data))))
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
        Friend Raw As JsonNode
        Public Function Clone() As JslotSkinOverride
            Return New JslotSkinOverride With {
                .SlotMask = SlotMask, .Slots = New Dictionary(Of Integer, String)(Slots),
                .DiffusePath = DiffusePath, .NormalPath = NormalPath,
                .TintR = TintR, .TintG = TintG, .TintB = TintB, .TintA = TintA, .HasTint = HasTint,
                .Alpha = Alpha, .HasAlpha = HasAlpha,
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
        End If
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
            If morphs("sculptDivisor") IsNot Nothing Then j.SculptDivisor = Math.Max(1, GetInt(morphs("sculptDivisor")))
            For Each sp In AsArray(morphs("sculpt"))
                Dim o = TryCast(sp, JsonObject) : If o Is Nothing Then Continue For
                Dim part As New JslotSculptPart
                If o("host") IsNot Nothing Then part.Host = GetStr(o("host"))
                part.HadVertices = o("vertices") IsNot Nothing
                If part.HadVertices Then part.Vertices = GetLong(o("vertices"))
                part.HadData = o("data") IsNot Nothing
                For Each row In AsArray(o("data"))
                    Dim arr = TryCast(row, JsonArray) : If arr Is Nothing OrElse arr.Count < 4 Then Continue For
                    part.Indices.Add(GetInt(arr(0))) : part.Dx.Add(GetInt(arr(1))) : part.Dy.Add(GetInt(arr(2))) : part.Dz.Add(GetInt(arr(3)))
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
                Dim nt As New JslotNodeTransform With {.NodeName = GetStr(o("node")), .Raw = JsonNode.Parse(o.ToJsonString())}
                Dim rot(8) As Single
                Dim anyRot As Boolean = False
                For Each k In AsArray(o("keys"))
                    Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                    For Each v In AsArray(ko("values"))
                        Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                        Dim vkey = GetInt(vo("key"))
                        Dim vidx = GetInt(vo("index"))
                        Select Case vkey
                            Case 30
                                nt.Scale = CSng(GetDbl(vo("data"))) : nt.HasScale = True
                            Case 31
                                Dim d = CSng(GetDbl(vo("data")))
                                Select Case vidx
                                    Case 0 : nt.PosX = d
                                    Case 1 : nt.PosY = d
                                    Case 2 : nt.PosZ = d
                                End Select
                                nt.HasPosition = True
                            Case 32
                                If vidx >= 0 AndAlso vidx <= 8 Then
                                    rot(vidx) = CSng(GetDbl(vo("data")))
                                    anyRot = True
                                End If
                            Case 33
                                nt.ScaleMode = GetInt(vo("data")) : nt.HasScaleMode = True
                        End Select
                    Next
                Next
                If anyRot Then
                    ' 9 row-major floats → Matrix33 → axis-angle radians (exact inverse of BSRotationToMatrix33).
                    Dim m As New NiflySharp.Structs.Matrix33 With {
                        .M11 = rot(0), .M12 = rot(1), .M13 = rot(2),
                        .M21 = rot(3), .M22 = rot(4), .M23 = rot(5),
                        .M31 = rot(6), .M32 = rot(7), .M33 = rot(8)}
                    Dim aa = Transform_Class.Matrix33ToBSRotation(m)
                    nt.RotX = aa.X : nt.RotY = aa.Y : nt.RotZ = aa.Z : nt.HasRotation = True
                End If
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
                    ElseIf key = 7 AndAlso vtype = 3 Then
                        Dim u As UInteger = CUInt(GetLong(vo("data")) And &HFFFFFFFFL)
                        sk.TintA = ((u >> 24) And &HFF) / 255.0F : sk.TintR = ((u >> 16) And &HFF) / 255.0F
                        sk.TintG = ((u >> 8) And &HFF) / 255.0F : sk.TintB = (u And &HFF) / 255.0F
                        sk.HasTint = True
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
            ElseIf key = 8 AndAlso vtype = 4 Then
                ' kParam_ShaderAlpha (OverrideVariant.h:36) — the overlay's opacity, distinct from the tint
                ' colour's alpha byte. Modeled (not ignored) so Save re-emits it instead of silently
                ' resetting every authored overlay to fully opaque.
                node.Alpha = CSng(GetDbl(vo("data")))
                node.HasAlpha = True
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
        Dim actor As New JsonObject From {{"hairColor", HairColor}, {"headTexture", HeadTexture}, {"weight", Weight}}
        root("actor") = actor
        Dim hpArr As New JsonArray()
        For Each hp In HeadParts : hpArr.Add(New JsonObject From {{"formId", hp.FormId}, {"formIdentifier", hp.FormIdentifier}, {"type", hp.Type}}) : Next
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
        Dim cmArr As New JsonArray()
        For Each cm In CustomMorphs : cmArr.Add(New JsonObject From {{"name", cm.Name}, {"value", cm.Value}}) : Next
        morphs("custom") = cmArr
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
        If NodeTransforms.Count > 0 OrElse _hadTransforms Then
            Dim trArr As New JsonArray()
            For Each nt In NodeTransforms
                If nt Is Nothing Then Continue For
                Dim raw = TryCast(If(nt.Raw Is Nothing, BuildTransformRaw(nt), JsonNode.Parse(nt.Raw.ToJsonString())), JsonObject)
                If raw Is Nothing Then Continue For
                Dim keys = TryCast(raw("keys"), JsonArray)
                If keys Is Nothing Then keys = New JsonArray() : raw("keys") = keys
                ' Scale (key 30): patch every existing key-30 value in place (index-agnostic — skee reads scale off
                ' the value regardless of index); append at index 0 if the element had none.
                If nt.HasScale Then
                    Dim patched = False
                    For Each k In keys
                        Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                        For Each v In AsArray(ko("values"))
                            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                            If GetInt(vo("key")) = 30 Then vo("data") = CDbl(nt.Scale) : patched = True
                        Next
                    Next
                    If Not patched Then PatchTransformValue(keys, 30, 4, 0, CDbl(nt.Scale))
                End If
                ' Position (key 31): per-component index 0/1/2, plain floats (exact round-trip).
                If nt.HasPosition Then
                    PatchTransformValue(keys, 31, 4, 0, CDbl(nt.PosX))
                    PatchTransformValue(keys, 31, 4, 1, CDbl(nt.PosY))
                    PatchTransformValue(keys, 31, 4, 2, CDbl(nt.PosZ))
                End If
                ' Rotation (key 32): only rebuilt when a UI edit set RotationDirty — otherwise the original 9 floats
                ' in Raw stay byte-exact (axis-angle→matrix would drift ~1 ULP).
                If nt.HasRotation AndAlso nt.RotationDirty Then
                    Dim r = MatrixRowMajor(Transform_Class.BSRotationToMatrix33(New System.Numerics.Vector3(nt.RotX, nt.RotY, nt.RotZ)))
                    For i = 0 To 8 : PatchTransformValue(keys, 32, 4, i, CDbl(r(i))) : Next
                End If
                ' ScaleMode (key 33): patch every existing key-33 value (index-agnostic); append @ index 0 if none.
                If nt.HasScaleMode Then
                    Dim patched = False
                    For Each k In keys
                        Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                        For Each v In AsArray(ko("values"))
                            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                            If GetInt(vo("key")) = 33 Then vo("data") = CLng(nt.ScaleMode) : patched = True
                        Next
                    Next
                    If Not patched Then PatchTransformValue(keys, 33, 3, 0, CLng(nt.ScaleMode))
                End If
                trArr.Add(raw)
            Next
            root("transforms") = trArr
        End If
        ' skinOverrides — rebuilt from the modeled nodes: re-emit each element's Raw with the (possibly edited)
        ' diffuse/normal texture + tint values patched. Emitted when there are skin overrides or the loaded file
        ' carried the node (faithful round-trip).
        If SkinOverrides.Count > 0 OrElse _hadSkinOverrides Then
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
                    If sk.HasAlpha Then PatchSkinValue(vals, 8, 4, -1, CDbl(sk.Alpha), False)
                End If
                If sk.HasTint Then
                    Dim u As UInteger = (CUInt(Math.Round(Clamp01(sk.TintA) * 255)) << 24) Or (CUInt(Math.Round(Clamp01(sk.TintR) * 255)) << 16) Or (CUInt(Math.Round(Clamp01(sk.TintG) * 255)) << 8) Or CUInt(Math.Round(Clamp01(sk.TintB) * 255))
                    PatchSkinValue(vals, 7, 3, -1, CLng(u), False)
                End If
                soArr.Add(raw)
            Next
            root("skinOverrides") = soArr
        End If
        Dim opt As New JsonSerializerOptions With {.WriteIndented = True, .TypeInfoResolver = New System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()}
        Return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString(opt))
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

        If ov.HasTint Then
            Dim a As UInteger = ClampToByte(ov.TintA)
            Dim r As UInteger = ClampToByte(ov.TintR)
            Dim g As UInteger = ClampToByte(ov.TintG)
            Dim b As UInteger = ClampToByte(ov.TintB)
            Dim u As UInteger = (a << 24) Or (r << 16) Or (g << 8) Or b
            Dim signed As Integer = BitConverter.ToInt32(BitConverter.GetBytes(u), 0)
            PatchOverlayValue(valuesArr, 7, 3, -1, signed, isString:=False)
        Else
            RemoveOverlayKey(valuesArr, 7, Nothing)
        End If
        If ov.HasAlpha Then
            PatchOverlayValue(valuesArr, 8, 4, -1, CDbl(ov.Alpha), isString:=False)
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
    ''' preset. Any already-relative path is returned verbatim.</summary>
    Public Shared Function ToGameTexturePath(path As String) As String
        If String.IsNullOrWhiteSpace(path) Then Return ""
        Dim p = path.Trim()
        Dim isRooted = p.Contains(":"c) OrElse p.StartsWith("\") OrElse p.StartsWith("/")
        If Not isRooted Then Return p
        Return FO4UnifiedMaterial_Class.CorrectTexturePath(p)   ' → "textures\…"
    End Function
End Class
