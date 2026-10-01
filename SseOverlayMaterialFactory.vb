Imports System.Text.Json.Nodes
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>A RACEMENU (skee64) BODY / HANDS / FEET OVERLAY LAYER, BUILT THE WAY skee64 INSTALLS IT.
''' <para>Law (skee64 source, "Script extenders, Racemenu y Looksmenu\SKSE64Plugins\skee64"):</para>
''' <list type="bullet">
''' <item>Template per part and pool: <c>meshes\actors\character\character assets\{body|hands|feet}_{overlay|magicoverlay}.nif</c>
''' (OverlayInterface.h:23-46). skee64 opens it and takes the FIRST NiGeometry in pre-order and only its two
''' properties, shader and alpha (OverlayInterface.cpp:103-136). Measured on the 8 installed templates: BSLightingShaderProperty
''' type 5 (SkinTint = kShaderType_FaceGenRGBTint), SLSF1 0x82601303, SLSF2 0x02008081, gloss 30, specular (1,1,1) x3,
''' lighting effects 0.4 / 2.0, skin tint (1,1,1), slot 0 = Overlays\default.dds.</item>
''' <item>Target: the first SkinTint geometry of the ARMA (NifUtils.cpp:901-928) - chosen by the caller.</item>
''' <item>Copied from the skin: texture-set slots 1..8 (OverlayInterface.cpp:218-224), Model_Space_Normals and
''' Vertex_Colors mirrored (:196-204); the geometry is the skin's (shared skin instance, :157/:187-188).</item>
''' <item>Forced: NiAlphaProperty flags 4845 / threshold 0 on EVERY layer, also the magic ones (main.cpp:130-132,
''' OverlayInterface.cpp:242-247); SLSF1 Decal (main.cpp:133, OverlayInterface.cpp:206-208).</item>
''' <item>Per-layer overrides, applied in (key, index) order (OverrideVariant.h:19) by SetShaderProperty
''' (ShaderUtilities.cpp:283-366): 0 emissive colour (Int, raw byte / 255, OverrideVariant.cpp:249-264), 1 emissive
''' multiple, 2 glossiness, 3 specular strength, 4/5 lighting effect 1/2, 7 SkinTint colour (raw byte / 255, high
''' byte dropped), 8 alpha, 9 texture of slot <c>index</c> (verbatim path, ShaderUtilities.cpp:147-180).</item>
''' </list>
''' <para>Holes, declared (engine side, not in skee64): the draw order and the Decal bias of the separate shape; the
''' float controller the magic template carries; how a path without the textures\ prefix resolves. FACE: the
''' normal pool [Ovl] stays in the shared fold (render == bake; FaceGen closed) and never reaches Build; a magic
''' [SOvl] face layer is built here, its slots 1..8 from the head's texture set (headData-&gt;headTexture,
''' OverlayInterface.cpp:558-560, 596, 228-232).</para></summary>
Public Module SseOverlayMaterialFactory

    ''' <summary>Bytes de cada plantilla, con la <see cref="FilesDictionary_class.ScanGeneration"/> en que se
    ''' leyeron. Una entrada vale sólo mientras la generación no cambió (re-scan, RegisterArchive, Unregister...,
    ''' todo mutador del diccionario la sube): es el sello que la librería declara para las vistas derivadas.</summary>
    Private ReadOnly TemplateBytes As New Concurrent.ConcurrentDictionary(Of String, (Gen As Integer, Bytes As Byte()))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>The skee64 part of an overlay node ("Body [Ovl0]" / "Hands [SOvl0]" / "Feet ..." / "Face ..."), or ""
    ''' for any other node. (Face [Ovl] layers are folded into the head diffuse by the caller and never reach Build.)</summary>
    Public Function OverlayPart(nodeName As String) As String
        If String.IsNullOrEmpty(nodeName) Then Return ""
        Dim n = nodeName.TrimStart()
        For Each p In {"Body", "Hands", "Feet", "Face"}
            If n.StartsWith(p, StringComparison.OrdinalIgnoreCase) Then Return p
        Next
        Return ""
    End Function

    ''' <summary>The template NIF of a part and pool (OverlayInterface.h:23-46).</summary>
    Public Function TemplatePath(part As String, spell As Boolean) As String
        Return $"meshes\actors\character\character assets\{part.ToLowerInvariant()}_{If(spell, "magicoverlay", "overlay")}.nif"
    End Function

    ''' <summary>Forgets the cached template bytes and the defaults read from them. NOT needed for correctness in
    ''' the app: every entry carries the <see cref="FilesDictionary_class.ScanGeneration"/> it was read under and is
    ''' re-read when the dictionary changes. Kept for harnesses that want a clean slate.</summary>
    Public Sub Invalidate()
        TemplateBytes.Clear()
        TemplateDefaultsCache.Clear()
        MissingTemplateLogged.Clear()
    End Sub

    ''' <summary>Plantillas cuya AUSENCIA ya se avisó en el log, por generación del diccionario (una línea por
    ''' plantilla y por contenido del diccionario, no por consulta: el resolvedor lo consultan el sort, los gates y
    ''' cada refresh).</summary>
    Private ReadOnly MissingTemplateLogged As New Concurrent.ConcurrentDictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>The template of a part and pool, parsed fresh (each caller may mutate it): the NIF, its FIRST
    ''' NiGeometry in pre-order and that geometry's BSLightingShaderProperty - the only things skee64 keeps
    ''' (OverlayInterface.cpp:103-136). Nothing when the template is not installed (skee64 installs nothing,
    ''' OverlayInterface.cpp:104-106) or has no lighting shader.</summary>
    Private Function LoadTemplate(part As String, spell As Boolean) As (Nif As Nifcontent_Class_Manolo, Shape As INiShape, Shader As BSLightingShaderProperty)?
        Dim path = TemplatePath(part, spell)
        ' LA GENERACIÓN SE LEE ANTES DE LOS BYTES (el patrón de LocalizedStrings): si el diccionario cambia en
        ' el medio, la entrada queda bajo la generación VIEJA y la próxima lectura la descarta. Así no se publica
        ' en el caché un byte del contenido anterior, y una plantilla montada/desmontada en runtime se ve.
        ' SÓLO SE CACHEA LO ENCONTRADO: la ausencia se vuelve a preguntar (la plantilla decide si un Face [Ovl]
        ' se pliega y si el bake lo hornea).
        Dim gen = FilesDictionary_class.ScanGeneration
        Dim bytes As Byte() = Nothing
        Dim hit As (Gen As Integer, Bytes As Byte()) = Nothing
        If TemplateBytes.TryGetValue(path, hit) AndAlso hit.Gen = gen Then
            bytes = hit.Bytes
        Else
            Try
                bytes = FilesDictionary_class.GetBytes(path)
            Catch
                bytes = Nothing
            End Try
            If bytes IsNot Nothing AndAlso bytes.Length > 0 AndAlso FilesDictionary_class.ScanGeneration = gen Then
                TemplateBytes(path) = (gen, bytes)
            End If
        End If
        If bytes Is Nothing OrElse bytes.Length = 0 Then
            If MissingTemplateLogged.TryAdd(path & "|" & gen.ToString(Globalization.CultureInfo.InvariantCulture), True) Then
                Logger.LogLazy(Function() $"[OVERLAY-SSE] plantilla '{path}' no instalada: skee64 no instala esas capas (ni vivas ni plegadas)")
            End If
            Return Nothing
        End If
        Dim nif As New Nifcontent_Class_Manolo()
        nif.Load_Manolo(bytes)
        Dim shape = nif.ShapesEnOrdenDeEscena().FirstOrDefault()
        If shape Is Nothing Then Return Nothing
        Dim shad = TryCast(nif.GetShader(shape), BSLightingShaderProperty)
        If shad Is Nothing Then Return Nothing
        Return (nif, shape, shad)
    End Function

    ''' <summary>What a layer gets from its template when the preset does not override it: the SkinTint colour
    ''' (the material tintColor the engine's technique-5 PS reads as <c>cb1[1]</c>) and the material alpha.
    ''' Measured on the installed templates: (1,1,1) and 1 in all eight.</summary>
    Public Structure TemplateLayerDefaults
        Public TintR As Single
        Public TintG As Single
        Public TintB As Single
        Public Alpha As Single
    End Structure

    Private ReadOnly TemplateDefaultsCache As New Concurrent.ConcurrentDictionary(Of String, (Gen As Integer, Defaults As TemplateLayerDefaults))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>The defaults of the template of <paramref name="part"/> / pool, or Nothing when it is not
    ''' installed (then skee64 installs no layer at all). Cached with the template bytes.</summary>
    Public Function TemplateDefaults(part As String, spell As Boolean) As TemplateLayerDefaults?
        If String.IsNullOrEmpty(part) Then Return Nothing
        Dim path = TemplatePath(part, spell)
        ' Igual que los bytes: generación leída ANTES, válido sólo bajo la misma, sólo se cachea lo encontrado.
        Dim gen = FilesDictionary_class.ScanGeneration
        Dim cached As (Gen As Integer, Defaults As TemplateLayerDefaults) = Nothing
        If TemplateDefaultsCache.TryGetValue(path, cached) AndAlso cached.Gen = gen Then Return cached.Defaults
        Dim t = LoadTemplate(part, spell)
        If Not t.HasValue Then Return Nothing
        Dim s = t.Value.Shader
        Dim d As New TemplateLayerDefaults With {.TintR = s.SkinTintColor.R, .TintG = s.SkinTintColor.G,
                                                 .TintB = s.SkinTintColor.B, .Alpha = s.Alpha}
        If FilesDictionary_class.ScanGeneration = gen Then TemplateDefaultsCache(path) = (gen, d)
        Return d
    End Function

    ''' <summary>The byte skee64 stores for one channel of a key-7 tint. The preset's colour travels as an Int
    ''' and the shader unpacks it as byte / 255 (OverrideVariant.cpp:249-264), so this byte IS the value the
    ''' engine sees. The one place that quantizes it: the live layer (<see cref="OverrideValues"/>) and the
    ''' fold (<see cref="SseOverlayCompositor.ResolveSkinTintLayer"/>) and the apply-script packer
    ''' (NpcApplyScriptEmitter.PackTint) all use it. Clamped to [0,1] first: a byte out of range would bleed into
    ''' the neighbouring channel of the packed 0xAARRGGBB.</summary>
    Public Function TintByte(v As Single) As Integer
        Return CInt(Math.Round(Math.Max(0.0F, Math.Min(1.0F, v)) * 255.0F))
    End Function

    ''' <summary>Builds the layer skee64 would install for <paramref name="ov"/> over the skin
    ''' <paramref name="skinShape"/> (its resolved material and its NIF shader). Nothing when the node is not a body overlay or the template is not installed
    ''' (skee64 then installs nothing either).</summary>
    ''' <summary>What skee64.ini decides about EVERY layer at install ([Overlays/Data], main.cpp:130-133 defaults,
    ''' 783-786 reads, 830 threshold clamp to 0xFF).</summary>
    Public Structure InstallOptions
        Public AlphaOverride As Boolean
        Public AlphaFlags As UShort
        Public AlphaThreshold As UShort
        Public ForceDecal As Boolean
        ''' <summary>skee64's hardcoded defaults (main.cpp:130-133).</summary>
        Public Shared ReadOnly Property Defaults As InstallOptions
            Get
                Return New InstallOptions With {.AlphaOverride = True, .AlphaFlags = 4845US, .AlphaThreshold = 0US, .ForceDecal = True}
            End Get
        End Property
    End Structure

    Public Function Build(skinShape As IRenderableShape, ov As RaceMenuJslot.JslotOverlayNode, options As InstallOptions) As OverlayMaterialLayer
        If skinShape Is Nothing OrElse ov Is Nothing Then Return Nothing
        Dim skinMaterial = skinShape.ShapeMaterial?.material
        If skinMaterial Is Nothing Then Return Nothing
        Dim part = OverlayPart(ov.NodeName)
        If part = "" Then Return Nothing
        ' A fresh copy of the template per layer: every layer carries its own overrides.
        Dim tpl = LoadTemplate(part, ov.IsSpell)
        If Not tpl.HasValue Then Return Nothing   ' ausencia ya avisada (una vez) por LoadTemplate
        Dim nif = tpl.Value.Nif
        Dim shape = tpl.Value.Shape
        Dim shad = tpl.Value.Shader

        ' Copied from the skin (OverlayInterface.cpp:196-204, 218-224).
        shad.ModelSpace = skinMaterial.ModelSpaceNormals
        Dim skinShader = TryCast(skinShape.NifShader, BSShaderProperty)
        shad.HasVertexColors = skinShader IsNot Nothing AndAlso skinShader.HasVertexColors
        Dim skinSlots = skinMaterial.SkyrimTextureSetSlots()
        Dim texset = If(shad.TextureSetRef Is Nothing OrElse shad.TextureSetRef.Index < 0, Nothing,
                        TryCast(nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet))
        If texset Is Nothing OrElse skinSlots Is Nothing Then Return Nothing
        FO4UnifiedMaterial_Class.EnsureTextureSetSlots(texset)
        For i = 1 To Math.Min(8, Math.Min(skinSlots.Length, texset.Textures.Count) - 1)
            texset.Textures(i).Content = skinSlots(i)
        Next

        ' Forced at install per skee64.ini (OverlayInterface.cpp:206-208 Decal, 242-247 alpha on every layer).
        If options.ForceDecal Then FO4UnifiedMaterial_Class.SetDecalFlag(shad, True)
        If options.AlphaOverride AndAlso shape.AlphaPropertyRef IsNot Nothing AndAlso shape.AlphaPropertyRef.Index >= 0 Then
            Dim alp = TryCast(nif.Blocks(shape.AlphaPropertyRef.Index), NiAlphaProperty)
            If alp IsNot Nothing Then
                alp.Flags.Value = options.AlphaFlags
                alp.Threshold = CByte(Math.Min(options.AlphaThreshold, 255US))
            End If
        End If

        ' Overrides in (key, index) order (OverrideVariant.h:19; ShaderUtilities.cpp:283-366).
        For Each kv In OverrideValues(ov).OrderBy(Function(t) t.Key).ThenBy(Function(t) t.Index)
            Select Case kv.Key
                Case 0 : If kv.IsInt Then shad.EmissiveColor = RawColor4(kv.IntValue, shad.EmissiveColor.A)
                Case 1 : If kv.IsFloat Then shad.EmissiveMultiple = kv.FloatValue
                Case 2 : If kv.IsFloat Then shad.Glossiness = kv.FloatValue
                Case 3 : If kv.IsFloat Then shad.SpecularStrength = kv.FloatValue
                Case 4 : If kv.IsFloat Then shad.Softlight = kv.FloatValue
                Case 5 : If kv.IsFloat Then shad.RimlightPower = kv.FloatValue
                Case 7 : If kv.IsInt Then shad.SkinTintColor = RawColor3(kv.IntValue)
                Case 8 : If kv.IsFloat Then shad.Alpha = kv.FloatValue
                Case 9 : If kv.IsString AndAlso kv.Index >= 0 AndAlso kv.Index < texset.Textures.Count Then texset.Textures(kv.Index).Content = kv.StringValue
            End Select
        Next

        Dim rel = nif.GetRelatedMaterial(shape)
        If rel Is Nothing OrElse rel.material Is Nothing Then Return Nothing
        Return New OverlayMaterialLayer With {
            .Material = New Nifcontent_Class_Manolo.RelatedMaterial_Class With {.material = rel.material, .path = ""}
        }
    End Function

    ''' <summary>One skee64 override value of the layer.</summary>
    Public Structure OverrideValue
        Public Key As Integer
        Public Index As Integer
        Public IsInt As Boolean
        Public IsFloat As Boolean
        Public IsString As Boolean
        Public IntValue As Integer
        Public FloatValue As Single
        Public StringValue As String
    End Structure

    ''' <summary>The layer's overrides: the modelled ones (tint 7, alpha 8, diffuse 9/0, normal 9/1 - what the editor
    ''' edits) from their fields, every other one from the preset's verbatim values array.</summary>
    Friend Function OverrideValues(ov As RaceMenuJslot.JslotOverlayNode) As List(Of OverrideValue)
        Dim r As New List(Of OverrideValue)
        Dim modelled = Function(k As Integer, i As Integer) (k = 7) OrElse (k = 8) OrElse (k = 9 AndAlso (i = 0 OrElse i = 1))
        Dim arr = TryCast(ov.RawValues, JsonArray)
        If arr IsNot Nothing Then
            For Each n In arr
                Dim o = TryCast(n, JsonObject)
                If o Is Nothing Then Continue For
                Dim key = CInt(o("key")?.GetValue(Of Long)())
                Dim index = If(o("index") Is Nothing, -1, CInt(o("index").GetValue(Of Long)()))
                If modelled(key, index) Then Continue For
                Dim d = o("data")
                If d Is Nothing Then Continue For
                Dim v As New OverrideValue With {.Key = key, .Index = index}
                ' OverrideVariant.h:63-68: 2 String, 3 Int, 4 Float (read by PresetInterface.cpp:1150-1172).
                Dim typ = If(o("type") Is Nothing, -1, CInt(o("type").GetValue(Of Long)()))
                Select Case typ
                    Case 2 : v.IsString = True : v.StringValue = d.GetValue(Of String)()
                    Case 3
                        Dim lv = CLng(d.GetValue(Of Double)()) And &HFFFFFFFFL
                        v.IsInt = True : v.IntValue = CInt(If(lv > Integer.MaxValue, lv - &H100000000L, lv))
                    Case 4 : v.IsFloat = True : v.FloatValue = CSng(d.GetValue(Of Double)())
                    Case Else : Continue For
                End Select
                r.Add(v)
            Next
        End If
        If ov.HasTint Then
            Dim u = (TintByte(ov.TintR) << 16) Or (TintByte(ov.TintG) << 8) Or TintByte(ov.TintB)
            r.Add(New OverrideValue With {.Key = 7, .Index = ov.TintIndex, .IsInt = True, .IntValue = u})
        End If
        If ov.HasAlpha Then r.Add(New OverrideValue With {.Key = 8, .Index = ov.AlphaIndex, .IsFloat = True, .FloatValue = ov.Alpha})
        If Not String.IsNullOrEmpty(ov.DiffusePath) Then r.Add(New OverrideValue With {.Key = 9, .Index = 0, .IsString = True, .StringValue = ov.DiffusePath})
        If Not String.IsNullOrEmpty(ov.NormalPath) Then r.Add(New OverrideValue With {.Key = 9, .Index = 1, .IsString = True, .StringValue = ov.NormalPath})
        Return r
    End Function

    ' UnpackValue<NiColor> (OverrideVariant.cpp:249-264): bytes 2/1/0 / 255, the high byte dropped, no gamma.
    Private Function RawColor3(u As Integer) As NiflySharp.Structs.Color3
        Return New NiflySharp.Structs.Color3(((u >> 16) And &HFF) / 255.0F, ((u >> 8) And &HFF) / 255.0F, (u And &HFF) / 255.0F)
    End Function

    Private Function RawColor4(u As Integer, a As Single) As NiflySharp.Structs.Color4
        Return New NiflySharp.Structs.Color4(((u >> 16) And &HFF) / 255.0F, ((u >> 8) And &HFF) / 255.0F, (u And &HFF) / 255.0F, a)
    End Function
End Module
