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
        Public Property Vertices As Integer = 0
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
    End Class

    Public Property HairColor As Integer
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
        ''' <summary>Uniform node scale (kParam_NodeTransformScale = key 30). 1.0 = unscaled.</summary>
        Public Property Scale As Single = 1.0F
        Public Property HasScale As Boolean
        ''' <summary>The verbatim original transform element ({firstPerson, node, keys}); re-emitted on Save
        ''' with the scale key patched to <see cref="Scale"/>. Nothing for a UI-created scale (built fresh).</summary>
        Friend Raw As JsonNode

        ''' <summary>Deep-clone (detaches Raw JSON). Public so cross-assembly carriers (LooksmenuPreset,
        ''' sidecar) can copy the modeled transform without touching the Friend Raw directly.</summary>
        Public Function Clone() As JslotNodeTransform
            Return New JslotNodeTransform With {
                .NodeName = NodeName, .Scale = Scale, .HasScale = HasScale,
                .Raw = If(Raw Is Nothing, Nothing, JsonNode.Parse(Raw.ToJsonString()))}
        End Function
    End Class

    ''' <summary>Factory for a UI/sidecar-created scale-only node transform (node name + uniform scale).
    ''' Builds the Raw element so a later Save round-trips it. Public so the app's sidecar hydrate can
    ''' rebuild the carrier from a stored node→scale map.</summary>
    Public Shared Function MakeScaleTransform(nodeName As String, scale As Single) As JslotNodeTransform
        Return New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True, .Raw = BuildScaleTransformRaw(nodeName, scale)}
    End Function

    ''' <summary>Build a fresh RaceMenu transform element {firstPerson:false, node, keys:[{name:RSMTransform,
    ''' values:[{key:30,type:4,index:-1,data:scale}]}]} for a UI-created / Raw-less scale.</summary>
    Private Shared Function BuildScaleTransformRaw(nodeName As String, scale As Single) As JsonObject
        Return New JsonObject From {
            {"firstPerson", False}, {"node", nodeName},
            {"keys", New JsonArray From {New JsonObject From {
                {"name", "RSMTransform"},
                {"values", New JsonArray From {New JsonObject From {{"key", 30}, {"type", 4}, {"index", -1}, {"data", CDbl(scale)}}}}}}}}
    End Function

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
        For Each v In vals
            Dim vo = TryCast(v, JsonObject)
            If vo Is Nothing Then Continue For
            If GetInt(vo("key")) = key AndAlso GetInt(vo("type")) = vtype AndAlso GetInt(vo("index")) = index Then
                vo("data") = If(isString, JsonValue.Create(CStr(data)), JsonValue.Create(CLng(data)))
                Return
            End If
        Next
        vals.Add(New JsonObject From {{"key", key}, {"type", vtype}, {"index", index}, {"data", If(isString, JsonValue.Create(CStr(data)), JsonValue.Create(CLng(data)))}})
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
        Public Property DiffusePath As String
        Public Property NormalPath As String
        Public Property TintR As Single
        Public Property TintG As Single
        Public Property TintB As Single
        Public Property TintA As Single
        Public Property HasTint As Boolean
        Friend Raw As JsonNode
        Public Function Clone() As JslotSkinOverride
            Return New JslotSkinOverride With {
                .SlotMask = SlotMask, .DiffusePath = DiffusePath, .NormalPath = NormalPath,
                .TintR = TintR, .TintG = TintG, .TintB = TintB, .TintA = TintA, .HasTint = HasTint,
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
            j.HeadTexture = GetStr(actor("headTexture"))
            j.Weight = GetDbl(actor("weight"))
        End If
        For Each hp In AsArray(root("headParts"))
            Dim o = TryCast(hp, JsonObject) : If o Is Nothing Then Continue For
            j.HeadParts.Add(New JslotHeadPart With {.FormId = CUInt(GetInt(o("formId")) And &HFFFFFFFFL), .FormIdentifier = GetStr(o("formIdentifier")), .Type = GetInt(o("type"))})
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
                If o("vertices") IsNot Nothing Then part.Vertices = GetInt(o("vertices"))
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
        ' We model the node scale (key 30 = kParam_NodeTransformScale, float) editable; the full element is
        ' kept in Raw so Save round-trips any position/rotation/scaleMode keys byte-faithfully.
        If root("transforms") IsNot Nothing Then
            j._hadTransforms = True
            For Each tr In AsArray(root("transforms"))
                Dim o = TryCast(tr, JsonObject) : If o Is Nothing Then Continue For
                Dim nt As New JslotNodeTransform With {.NodeName = GetStr(o("node")), .Raw = JsonNode.Parse(o.ToJsonString())}
                For Each k In AsArray(o("keys"))
                    Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                    For Each v In AsArray(ko("values"))
                        Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                        If GetInt(vo("key")) = 30 Then
                            nt.Scale = CSng(GetDbl(vo("data"))) : nt.HasScale = True
                        End If
                    Next
                Next
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
                        If index = 0 Then
                            sk.DiffusePath = GetStr(vo("data"))
                        ElseIf index = 1 Then
                            sk.NormalPath = GetStr(vo("data"))
                        End If
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
            nt = New JslotNodeTransform With {.NodeName = nodeName, .Scale = scale, .HasScale = True, .Raw = BuildScaleTransformRaw(nodeName, scale)}
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
        Return System.Text.RegularExpressions.Regex.IsMatch(
            nodeName, "^(Body|Hands|Feet) \[S?Ovl\d+\]$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
    End Function

    ''' <summary>Decode one overlay <c>values</c> array per the §3.1 table. Recognized entries:
    '''   {key:9,type:2,index:0}=diffuse · {key:9,type:2,index:1}=normal · {key:7,type:3,index:-1}=tint
    ''' (signed 0xAARRGGBB). Unrecognized entries are ignored (e.g. TextureSet key 6, Alpha key 8).</summary>
    Private Shared Function DecodeOverlayNode(nodeName As String, valuesNode As JsonNode) As JslotOverlayNode
        Dim node As New JslotOverlayNode With {.NodeName = nodeName, .DiffusePath = "", .NormalPath = ""}
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
        If _morphsPresetsRaw IsNot Nothing Then def("presets") = JsonNode.Parse(_morphsPresetsRaw.ToJsonString())
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
            If part.Vertices > 0 Then po("vertices") = part.Vertices
            po("data") = dataArr
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
        ' transforms — rebuilt from the modeled nodes: re-emit each node's Raw with the (possibly edited)
        ' scale key patched. UI-created scales (no Raw) are built by SetNodeScale. Emitted when there are
        ' transforms or the loaded file carried the node.
        If NodeTransforms.Count > 0 OrElse _hadTransforms Then
            Dim trArr As New JsonArray()
            For Each nt In NodeTransforms
                If nt Is Nothing Then Continue For
                ' Raw-less (cloned across assemblies) → build a fresh scale element from the modeled scale.
                Dim raw = TryCast(If(nt.Raw Is Nothing, BuildScaleTransformRaw(nt.NodeName, nt.Scale), JsonNode.Parse(nt.Raw.ToJsonString())), JsonObject)
                If raw Is Nothing Then Continue For
                If nt.HasScale Then
                    For Each k In AsArray(raw("keys"))
                        Dim ko = TryCast(k, JsonObject) : If ko Is Nothing Then Continue For
                        For Each v In AsArray(ko("values"))
                            Dim vo = TryCast(v, JsonObject) : If vo Is Nothing Then Continue For
                            If GetInt(vo("key")) = 30 Then vo("data") = CDbl(nt.Scale)
                        Next
                    Next
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
                If raw Is Nothing Then
                    ' Raw-less (cloned across assemblies) → build a fresh element from the modeled fields.
                    raw = New JsonObject From {{"firstPerson", False}, {"slotMask", CLng(sk.SlotMask)}, {"values", New JsonArray()}}
                End If
                Dim vals = TryCast(raw("values"), JsonArray)
                If vals Is Nothing Then vals = New JsonArray() : raw("values") = vals
                PatchSkinValue(vals, 9, 2, 0, sk.DiffusePath, True)
                PatchSkinValue(vals, 9, 2, 1, sk.NormalPath, True)
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
    ''' <c>{ node, values:[…] }</c>. Emits the diffuse texture entry, the normal entry (only when a normal
    ''' path is set), and the tint entry (only when <see cref="JslotOverlayNode.HasTint"/>), mirroring the
    ''' §3.1 decode table. Tint packs to a signed 0xAARRGGBB int (the JSON's native signed form).</summary>
    Private Shared Function EncodeOverlayNode(ov As JslotOverlayNode) As JsonObject
        Dim valuesArr As New JsonArray()
        valuesArr.Add(New JsonObject From {{"key", 9}, {"type", 2}, {"index", 0}, {"data", If(ov.DiffusePath, "")}})
        If Not String.IsNullOrEmpty(ov.NormalPath) Then
            valuesArr.Add(New JsonObject From {{"key", 9}, {"type", 2}, {"index", 1}, {"data", ov.NormalPath}})
        End If
        If ov.HasTint Then
            Dim a As UInteger = ClampToByte(ov.TintA)
            Dim r As UInteger = ClampToByte(ov.TintR)
            Dim g As UInteger = ClampToByte(ov.TintG)
            Dim b As UInteger = ClampToByte(ov.TintB)
            Dim u As UInteger = (a << 24) Or (r << 16) Or (g << 8) Or b
            Dim signed As Integer = BitConverter.ToInt32(BitConverter.GetBytes(u), 0)
            valuesArr.Add(New JsonObject From {{"key", 7}, {"type", 3}, {"index", -1}, {"data", signed}})
        End If
        Return New JsonObject From {{"node", If(ov.NodeName, "")}, {"values", valuesArr}}
    End Function

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
End Class
