''' <summary>
''' One render-only material layer drawn as a coplanar decal over a shape's ALREADY-deformed
''' (morphed + skinned) geometry — the LooksMenu "body overlays"/tattoos model: the engine clones
''' the base skin shape, shares its skinInstance/geometry, and draws a different material on top
''' (f4ee/OverlayInterface.cpp, <c>OverlayInterface::UpdateOverlays</c> + <c>::LoadMaterialData</c>).
'''
''' Fallout 4's LooksMenu layers are built by <see cref="FromF4ee"/> (F4EE's LoadMaterialData, one owner); the lib renders the
''' supplied material - no UV/tint fields live here on purpose, they are written into the layer's material as F4EE writes them.
''' </summary>
Public Class OverlayMaterialLayer
    ''' <summary>The overlay material (BGEM effect-shader for tattoos, or BGSM), already loaded and
    ''' fully configured by the app. Same wrapper type the base shape carries
    ''' (Nifcontent_Class_Manolo.RelatedMaterial_Class), so it flows through MaterialData unchanged. Nothing: the layer is not drawn.</summary>
    Public Property Material As Nifcontent_Class_Manolo.RelatedMaterial_Class

    ''' <summary>The template's material path when its file did not load (F4EE's LoadMaterialFile failed: OverlayInterface.cpp:178,
    ''' nothing applied), else Nothing. The frame's WARNING "Overlay material not found" names it (PreviewModel.RenderableMesh
    ''' .ReportMissingOverlayMaterials, the one producer).</summary>
    Public Property MaterialNotFound As String

    ''' <summary>Ola 7h round 4e (re27 1): the template's material path when its file exists but could not be read whole (read as the
    ''' engine reads it: partial, or a 0-byte file's ctor values), else Nothing. The frame's WARNING "Overlay material unreadable" names
    ''' it (PreviewModel.RenderableMesh.ReportMissingOverlayMaterials, the one producer).</summary>
    Public Property MaterialUnreadable As String

    ''' <summary>F4EE's own default remap index (OverlayInterface.h:84, OverlayData ctor): a LooksMenu preset never sets it
    ''' (CharGenInterface.cpp:586-620 -&gt; AddOverlay 0x235..263 writes tint, offsetUV and scaleUV only).</summary>
    Private Const F4eeDefaultRemapIndex As Single = 0.50196F

    ''' <summary>THE one predicate "the overlay template is an EFFECT" (F4EE OverlayInterface.cpp:1113-1120, read when the template is
    ''' loaded): the text after the LAST '.' of the template's material string, compared with "bgem" by _stricmp; no '.' is not an effect.
    ''' Read by <see cref="FromF4ee"/> (which property the clone gets, :166-174) and by
    ''' PreviewModel.RenderableMesh.MaterialData.OverlayReplacesHostShaderProperty (whether that property replaced the host's).</summary>
    Public Shared Function IsEffectTemplate(materialPath As String) As Boolean
        If materialPath Is Nothing Then Return False
        Dim i = materialPath.LastIndexOf("."c)
        Return i >= 0 AndAlso String.Equals(materialPath.Substring(i + 1), "bgem", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>THE LAYER F4EE BUILDS for one overlay template slot over the skin shape <paramref name="skinShape"/> - its
    ''' LoadMaterialData (OverlayInterface.cpp:162-230) on the clone of the host (BSTriShape::CreateClone 0x1416D9F00: its shader and
    ''' alpha properties COPIES of the host's, D:\WinTmp\rc\re24\re.md addendum 1).
    ''' <list type="bullet">
    ''' <item>The template is an effect when its material path ends in ".bgem" (:1118-1120); an effect template over a lighting host
    ''' gets a NEW BSEffectShaderProperty (:166-169), a lighting template over an effect host a new BSLightingShaderProperty
    ''' (:171-174); otherwise the clone keeps the host's property copy.</item>
    ''' <item>The material file loaded: ApplyMaterialData(shape, true) (:178-179) - Deserialize on the HOST's NifShape / NifContent
    ''' (the clone's alpha property is a copy of the host's, the applicator's starting state) with the 3rd argument 1.</item>
    ''' <item>The file did NOT load (<see cref="MaterialNotFound"/>): nothing is applied. A lighting template keeps the property copy
    ''' and so a copy of the host's material: BSLightingShaderProperty::CopyMembers 0x1421685E6..606 makes it UNIQUE when the host
    ''' property has a controller or [mat+0x30] != 0, and the material manager gives every SkinTint material (type 5) a unique code
    ''' 0x142201A61..A64 / 0x142201AA0..AC5 ([mat+0x30] = counter + 1): F4EE only clones SkinTint hosts (:101-104), so the copy is
    ''' never the host's own material and the host never changes. An effect template's new property keeps its ctor's state
    ''' (0x1421774D0: flags ZBufferTest and ZBufferWrite only, the shared default effect material [0x143E716B0]; re25 2):
    ''' FO4UnifiedMaterial_Class.Fo4EngineDefaultEffectMaterial, with the clone's NiAlphaProperty (the host's copy) as its only
    ''' other input. The game draws it (GetRenderPasses 0x1421777A0: an opaque forward effect with depth write); so does the
    ''' preview, and the frame's warning names the path. HUECO, declared: that default material is SHARED ([0x143E716B0], SetMaterial's
    ''' 3rd argument 0), so F4EE's writes below land on the one object every such layer holds and stay there; the preview gives each
    ''' layer the ctor's values plus its own writes - the game's value after earlier layers depends on their order, not modelled.</item>
    ''' <item>Then F4EE writes the overlay's data on the material the property holds (:183-229): offsetUV added, scaleUV multiplied;
    ''' a SkinTint lighting material: kTintColor = the tint when the overlay has one (kHasTintColor: any component non-zero,
    ''' OverlayData::UpdateFlags, AreEqual Utilities.h:65) else the NPC's skin colour (TESNPC QNAM, <paramref name="npcSkinColor"/>),
    ''' and with a tint fLookupScale = the remap index; an effect material: kBaseColor = the tint when it has one. On an NPC that
    ''' tint stays: the engine's walk that writes the actor's skin tone into every SkinTint material under the actor's root
    ''' (0x1406EF070 -&gt; 0x1406F24B0, movups [mat+0xC0] at 0x1406F251E) runs at the 3D load only for the PlayerCharacter
    ''' [0x1431EDE50] or with bUseFaceGenPreprocessedHeads 0 (default 1; [tarea+0x85], 0x1402C6463..647F), D:\WinTmp\rc\re25\re.md 3
    ''' and its addenda. The app's subjects are NPC_ records (the load state of an NPC), so this tint is what it draws. Outside that
    ''' state, declared: rebuilding the 3D of an actor already loaded (0x140D27280, an event not traced) also runs the walk for an actor
    ''' whose base is the PlayerSpouseFemale_DO object [0x1431F1278] (Nora; bpl 0x140D272F0..31B, re25 addendum 2).</item>
    ''' </list>
    ''' Nothing when the host is not a SkinTint lighting shape (F4EE's candidate filter, :101-104) or the path is empty.</summary>
    Public Shared Function FromF4ee(skinShape As IRenderableShape, slotMaterialPath As String, tint As Single(), offsetUV As Single(),
                                    scaleUV As Single(), npcSkinColor As Drawing.Color, Optional actorSkinTone As Drawing.Color? = Nothing) As OverlayMaterialLayer
        Dim hostMat = skinShape?.ShapeMaterial?.material
        If hostMat Is Nothing OrElse Not hostMat.IsEngineSkinTint() Then Return Nothing
        Dim fullpath = FO4UnifiedMaterial_Class.CorrectMaterialPath(slotMaterialPath).StripPrefix(MaterialsPrefix)
        If String.IsNullOrEmpty(fullpath) Then Return Nothing
        Dim effect = IsEffectTemplate(slotMaterialPath)
        Dim mat As New FO4UnifiedMaterial_Class()
        Dim notFound As String = Nothing, unreadable As String = Nothing
        ' LoadMaterialFile 0x1417A9D80 returns 1 only when the stream does not open (0x1416A6E50 -> 0x1416A7150: the file is not in the
        ' data); a file that exists returns 0, and F4EE applies what the parse left (re27 1): MaterialFileReader's read as the engine
        ' reads it - whole, partial, or a 0-byte file's ctor values (re28 2: the same as a shape's) - with the frame's warning when not whole.
        If MaterialResolver.IsMaterialFileResolvable(MaterialsPrefix & fullpath) Then
            mat.Deserialize(MaterialsPrefix & fullpath, If(effect, GetType(MaterialLib.BGEM), GetType(MaterialLib.BGSM)), skinShape.NifShape,
                            skinShape.NifContent, engineApplyArg3:=True)
            If mat.PayloadIlegibleParaElMotor() IsNot Nothing Then unreadable = fullpath
        Else
            notFound = fullpath
        End If
        If notFound IsNot Nothing Then
            ' Nothing applied (:177-179): an effect template's new property keeps its ctor's default material; a lighting template keeps
            ' its unique copy of the host's material.
#If DEBUG Then
            ' Ola 7h tester round 4 (ShadowGate --ola7h-r4t M): a missing .bgem is not drawn (round 4 before 4c).
            If effect AndAlso Fo4GBufferConstants.GateMutantOn("p7h-r4c-bgem-undrawn") Then Return New OverlayMaterialLayer With {.MaterialNotFound = notFound}
#End If
            mat = If(effect, FO4UnifiedMaterial_Class.Fo4EngineDefaultEffectMaterial(skinShape.NifShape, skinShape.NifContent), hostMat.Clone())
        End If
        Dim hasTint = tint IsNot Nothing AndAlso tint.Length >= 4 AndAlso (tint(0) <> 0.0F OrElse tint(1) <> 0.0F OrElse tint(2) <> 0.0F OrElse tint(3) <> 0.0F)
        ' :185-196 - the UV transform (F4EE's defaults: offset 0, scale 1).
        If offsetUV IsNot Nothing AndAlso offsetUV.Length >= 2 Then
            mat.UOffset += offsetUV(0)
            mat.VOffset += offsetUV(1)
        End If
        If scaleUV IsNot Nothing AndAlso scaleUV.Length >= 2 Then
            mat.UScale *= scaleUV(0)
            mat.VScale *= scaleUV(1)
        End If
        ' The app stores a colour in bytes (Drawing.Color): F4EE's float channel, rounded and clamped.
        Dim b = Function(v As Single) CInt(Math.Round(Math.Max(0.0F, Math.Min(1.0F, v)) * 255.0F))
        If mat.IsBGEM() Then
            ' :222-229 - the effect material's kBaseColor.
            If hasTint Then mat.BaseColor = Drawing.Color.FromArgb(b(tint(3)), b(tint(0)), b(tint(1)), b(tint(2)))
        Else
            ' :200-219 - a SkinTint lighting material's kTintColor (rgb + its .a, SkinTintAlpha [mat+0xCC]) and fLookupScale.
            If mat.IsEngineSkinTint() Then
                If hasTint Then
                    mat.SkinTintColor = Drawing.Color.FromArgb(255, b(tint(0)), b(tint(1)), b(tint(2)))
                    mat.SkinTintAlpha = tint(3)
                Else
                    mat.SkinTintColor = Drawing.Color.FromArgb(255, npcSkinColor.R, npcSkinColor.G, npcSkinColor.B)
                    mat.SkinTintAlpha = npcSkinColor.A / 255.0F
                End If
            End If
            If hasTint Then mat.GrayscaleToPaletteScale = F4eeDefaultRemapIndex
            ' The PLAYER (re27 2-3, re25 addenda): building its 3D walks the actor's root (0x1406E31F0 -> 0x1406EF070 -> 0x1406F24B0) and
            ' writes the actor's skin tone into every SkinTint material under it, the clone's too (movups [mat+0xC0], 0x1406F251E): the
            ' tone 0x140655910 recomputes from its tint of type 0xC (<paramref name="actorSkinTone"/>, NPC Manager's
            ' NpcMorphPoseResolver.ResolvePlayerEngineSkinTone). F4EE's fLookupScale stays. DIFFERENCE WITH THE GAME, declared (user
            ' decision 22-oct-2026): the same walk writes the player's BODY SkinTint shapes too (0x1406F24B0 visits every SkinTint
            ' material under the root); the preview applies it to the layers only and leaves the body's tone as the facetints code sets it.
            If actorSkinTone.HasValue AndAlso mat.IsEngineSkinTint() Then
                mat.SkinTintColor = Drawing.Color.FromArgb(255, actorSkinTone.Value.R, actorSkinTone.Value.G, actorSkinTone.Value.B)
                mat.SkinTintAlpha = actorSkinTone.Value.A / 255.0F
            End If
        End If
        Return New OverlayMaterialLayer With {
            .Material = New Nifcontent_Class_Manolo.RelatedMaterial_Class With {.material = mat, .path = fullpath},
            .MaterialNotFound = notFound, .MaterialUnreadable = unreadable}
    End Function
End Class
