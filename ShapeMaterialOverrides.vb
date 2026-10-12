Imports FO4_Base_Library
Imports FO4_Base_Library.Canon.CanonInterpretacion

''' <summary>
''' Per-shape material override helpers shared by the human path and the OMOD path.
''' </summary>
Public Module ShapeMaterialOverrides

    ''' <summary>Ola 0c pto 17: the three inputs of a shape's color remapping index as the engine reads them (0x140255EC0 / 0x140256060):
    ''' the instance's ([r9]-&gt;vfunc +0x80, ARMO 0x1404612A0 movss [rcx+0x44]: the OMOD ColorRemappingIndex properties), the CNAM of the
    ''' material-swap substitution that matched each shape's material (0x1404219A0), and the model's MODC ([form+0x38]). FLT_MAX
    ''' ([0x142466444]) = none: Nothing here.</summary>
    Public NotInheritable Class ColorRemapInputs
        Public Instance As Single?
        Public Model As Single?
        ''' <summary>The CNAM of the current pass's substitution that matched each shape (cleared when a pass starts): after the last pass,
        ''' the last pass's (resultado5 rev-03 cierre).</summary>
        Friend ReadOnly MatchedCnam As New Dictionary(Of IRenderableShape, Single)(ReferenceEqualityComparer.Instance)
        ''' <summary>The instance's MaterialSwaps list (ARMO InstanceData vt+0x50 0x140461150 -&gt; [+0x18]), built by the OMOD properties
        ''' (OmodResolutionApplier), applied after the model's swap in order (0x140255F8E, 0x140255FB1..020).</summary>
        Friend ReadOnly InstanceSwaps As New List(Of UInteger)
    End Class

    ''' <summary>Where the engine writes the index (0x140256815 / 0x14025681F..82A).</summary>
    Public Enum ColorRemapTarget
        ''' <summary>Not written: a lighting material without property flag bit 4 GreyscaleToPalette.</summary>
        None
        ''' <summary>An effect material's +0x84 (BaseColorScale; 0x140256815).</summary>
        EffectBaseColorScale
        ''' <summary>A lighting material's +0xB8 (GrayscaleToPaletteScale), only with flag bit 4 (0x14025681F..82A).</summary>
        LightingPaletteScale
    End Enum

    ''' <summary>Ola 0c pto 17: the precedence per shape (0x140256390): the instance's index (0x14025646F); if none, the CNAM of the
    ''' substitution that matches the shape's material (0x14025673A..755); if none, the model's MODC (0x1402567D7..E5).</summary>
    Public Function ColorRemapIndexOf(instance As Single?, cnam As Single?, model As Single?) As Single?
        If instance.HasValue Then Return instance
        If cnam.HasValue Then Return cnam
        Return model
    End Function

    ''' <summary>Ola 0c pto 17: the field the index goes to (ColorRemapTarget). <paramref name="greyscaleToPalette"/> = property flag bit 4
    ''' (the material's GrayscaleToPaletteColor: the applicator's +0x1BC and the NIF's own bit, Fo4GBufferTechnique.vb:52 / :71).</summary>
    Public Function ColorRemapTargetOf(isEffect As Boolean, greyscaleToPalette As Boolean) As ColorRemapTarget
        If isEffect Then Return ColorRemapTarget.EffectBaseColorScale
        Return If(greyscaleToPalette, ColorRemapTarget.LightingPaletteScale, ColorRemapTarget.None)
    End Function

    ''' <summary>Ola 0c pto 17: THE one writer of the color remapping index - each shape's index (ColorRemapIndexOf) where the engine
    ''' writes it (ColorRemapTargetOf), for the shapes a substitution matched and for the rest (0x14025659E -&gt; 0x14025673A). With the
    ''' deferred queue [[r12+0x10]] empty - the app's still scene - the substitution applies at once (0x140256665..672) and the index is
    ''' written here; with it not empty the index is not written here (0x1402567C4..D5): that path is NOT TRACED and does not apply to
    ''' the preview.</summary>
    Public Sub ApplyColorRemapIndex(inputs As ColorRemapInputs, shapes As IEnumerable(Of IRenderableShape))
        If inputs Is Nothing OrElse shapes Is Nothing Then Return
        Dim logEnabled = Logger.Enabled
        For Each shape In shapes
            MaterialResolver.EnsureShapeMaterialResolved(shape)
            Dim material = shape.ShapeMaterial?.material
            If material Is Nothing Then Continue For
            Dim cnam As Single
            Dim matched = inputs.MatchedCnam.TryGetValue(shape, cnam)
            Dim idx = ColorRemapIndexOf(inputs.Instance, If(matched, cnam, CType(Nothing, Single?)), inputs.Model)
            If Not idx.HasValue Then Continue For
            Dim target = ColorRemapTargetOf(material.IsBGEM(), material.GrayscaleToPaletteColor)
            Select Case target
                Case ColorRemapTarget.EffectBaseColorScale : material.BaseColorScale = idx.Value
                Case ColorRemapTarget.LightingPaletteScale : material.GrayscaleToPaletteScale = idx.Value
            End Select
            If logEnabled Then
                Dim shapeNameLog = shape.ShapeName
                Dim idxL = idx.Value
                Logger.LogLazy(Function() $"[CREMAP-APPLIED] shape='{shapeNameLog}' index={idxL:F4} target={target}")
            End If
        Next
    End Sub

    ''' <summary>Ola 0c pto 17 (resultado5 rev-03 cierre, rev-23): an OMOD MaterialSwaps property's edit of the instance's swap list (handler
    ''' 0x140421B60, (flags &gt;&gt; 11) And 3 at 0x140421C06..C19): 0 = SET empties the list and appends (0x140421CCF..CDB), 2 = ADD appends
    ''' (0x140421CDB), 1 = REM removes EVERY entry of that form (0x140421C32..CCA). The caller checks the form is an MSWP (0x140421B7B,
    ''' type 0x91).</summary>
    Public Sub EditInstanceSwaps(swaps As List(Of UInteger), functionType As Integer, mswpFid As UInteger)
        Select Case functionType
            Case 0
                swaps.Clear()
                swaps.Add(mswpFid)
            Case 2
                swaps.Add(mswpFid)
            Case 1
                swaps.RemoveAll(Function(x) x = mswpFid)
        End Select
    End Sub

    ''' <summary>FormID overload: resolve the MSWP record via the plugin manager, then delegate to the parsed-data overload. Callers:
    ''' ApplyModelMaterial (the model's swap and each swap of the instance's list).</summary>
    Public Sub ApplyMaterialSwap(mswpFormID As UInteger,
                                 shapes As IEnumerable(Of IRenderableShape),
                                 pluginManager As PluginManager,
                                 remap As ColorRemapInputs)
        Dim logEnabled = Logger.Enabled
        If logEnabled Then
            Logger.LogLazy(Function() $"[MSWP-ENTRY] mswp=0x{mswpFormID:X8}")
        End If

        If mswpFormID = 0UI Then Return
        If pluginManager Is Nothing Then Return

        Dim mswpRec = pluginManager.GetRecord(mswpFormID)
        If mswpRec Is Nothing OrElse mswpRec.Header.Signature <> "MSWP" Then
            If logEnabled Then
                Logger.LogLazy(Function() $"[MSWP-LOAD-FAIL] mswp=0x{mswpFormID:X8} reason='record-not-found-or-wrong-sig'")
            End If
            Return
        End If

        Dim mswp = Canon.CanonRecords.Mswp(mswpRec, pluginManager)
        ' Delegate the material-path matching + replacement CORE to the parsed-data overload (no PluginManager
        ' needed past this point — it's pure path matching on the shapes).
        ApplyMaterialSwap(mswp, shapes, remap)
    End Sub

    ''' <summary>One pass of a material swap (the engine's 0x140256060 -&gt; visitor 0x140256250 -&gt; 0x140256390 per geometry) on an
    ''' ALREADY-PARSED <see cref="Canon.IMswp"/> (a draft one too). A pass starts without CNAM: only this swap's counts (the last pass
    ''' wins, resultado5 rev-03 cierre). For each shape, the first substitution whose ORIGINAL material is the shape's ORIGINAL NIF path
    ''' (<see cref="Nifcontent_Class_Manolo.NifMaterialPath"/>): the engine looks it up by [prop+0x10] (0x14025648E..4A1), which it
    ''' renames only while [0x142ED8B58] = 1 (0x140256677), and that byte is 1 only when the members of a SCOL are cloned
    ''' (BGSStaticCollection slot 78 0x14043B4D0 -&gt; 0x14043C720 -&gt; per member 0x14043DEE0 -&gt; 0x140255E00); equipment / biped
    ''' (0x140434F60, 0x140247F90), NPC, placed reference (TESObjectREFR slot 134 0x14050AF40, MODS base + XMSP 0x14050BBC0), queued
    ''' reference, actor, art object and workshop menu run with 0 (resultado5 R1, measured live: the swap's texture with the original
    ''' name). No app view clones SCOL members, so the swaps do not chain. Its CNAM is recorded as
    ''' soon as the path matches: 0x1404219A0 runs whether the replacement loads or not (0x14025673A..750, 0x1402565C5, 0x14025655E,
    ''' rev-24). The replacement is applied with ApplyMaterialData(..., True) (0x140256672) and, only when it loads, becomes the shape's
    ''' effective path (relatedMaterial.path, the one the material is loaded from: the last loaded replacement wins).</summary>
    Public Sub ApplyMaterialSwap(mswp As Canon.IMswp,
                                 shapes As IEnumerable(Of IRenderableShape),
                                 remap As ColorRemapInputs)
        If mswp Is Nothing Then Return
        Dim logEnabled = Logger.Enabled
        Dim mswpFormID = mswp.FormID
        remap.MatchedCnam.Clear()

        If mswp.Sustituciones().Count = 0 Then
            If logEnabled Then
                Logger.LogLazy(Function() $"[MSWP-LOAD] mswp=0x{mswpFormID:X8} subs=0 (empty MSWP)")
            End If
            Return
        End If

        Dim subsCount = mswp.Sustituciones().Count
        If logEnabled Then
            Logger.LogLazy(Function() $"[MSWP-LOAD] mswp=0x{mswpFormID:X8} subs={subsCount}")
        End If

        For Each shape In shapes
            MaterialResolver.EnsureShapeMaterialResolved(shape)

            Dim relatedMaterial = shape.ShapeMaterial
            If relatedMaterial Is Nothing OrElse relatedMaterial.material Is Nothing Then
                If logEnabled Then
                    Dim shapeNameLog = shape.ShapeName
                    Logger.LogLazy(Function() $"[MSWP-SHAPE-SKIP] shape='{shapeNameLog}' reason='no-material'")
                End If
                Continue For
            End If

            ' The lookup key is the NIF's ORIGINAL path in every pass and every resolution ([prop+0x10] is not renamed, 0x140256677;
            ' resultado5 R1): read from the NIF (NifMaterialPath, the one place GetRelatedMaterial loads it from), not from
            ' relatedMaterial.path, which a loaded replacement renames. A shape without a NIF (an overlay layer) has only its path.
            Dim originalPath = If(If(shape.NifContent IsNot Nothing AndAlso shape.NifShape IsNot Nothing,
                                     shape.NifContent.NifMaterialPath(shape.NifShape),
                                     relatedMaterial.path), "").Trim()
            Dim currentPath = originalPath
            If currentPath = "" Then
                If logEnabled Then
                    Dim shapeNameLog = shape.ShapeName
                    Logger.LogLazy(Function() $"[MSWP-SHAPE-SKIP] shape='{shapeNameLog}' reason='empty-current-path'")
                End If
                Continue For
            End If

            Dim correctedCurrentPath = FO4UnifiedMaterial_Class.CorrectMaterialPath(currentPath)
            If logEnabled Then
                Dim shapeNameLog = shape.ShapeName
                Dim ccpLog = correctedCurrentPath
                Logger.LogLazy(Function() $"[MSWP-SHAPE] shape='{shapeNameLog}' currentPath='{ccpLog}'")
            End If

            Dim matched As Boolean = False
            For Each sub_ In mswp.Sustituciones()
                Dim fromPath = FO4UnifiedMaterial_Class.CorrectMaterialPath(If(sub_.SubstitutionOriginalMaterial, ""))
                If fromPath = "" Then Continue For

                If String.Equals(correctedCurrentPath, fromPath, StringComparison.OrdinalIgnoreCase) Then
                    If sub_.TieneIndiceDeColor() Then remap.MatchedCnam(shape) = sub_.SubstitutionColorRemappingIndex
                    Dim targetPath = If(sub_.SubstitutionReplacementMaterial, "")
                    If targetPath = "" Then
                        If logEnabled Then
                            Dim shapeNameLog = shape.ShapeName
                            Dim fromL = fromPath
                            Logger.LogLazy(Function() $"[MSWP-MATCH-EMPTY-TARGET] shape='{shapeNameLog}' from='{fromL}' target=''")
                        End If
                        matched = True
                        Exit For
                    End If

                    ' Material swap: the engine applies the swapped-in material with ApplyMaterialData(..., True)
                    ' (Fallout4.exe 0x140256390 -> call at 0x140256672).
                    Dim newMaterial = MaterialResolver.TryLoadMaterialFromDictionary(targetPath, relatedMaterial.material, shape.NifShape, shape.NifContent,
                                                                                     engineApplyArg3:=True)
                    If newMaterial IsNot Nothing Then
                        relatedMaterial.material = newMaterial
                        relatedMaterial.path = FO4UnifiedMaterial_Class.CorrectMaterialPath(targetPath)
                        If logEnabled Then
                            Dim shapeNameLog = shape.ShapeName
                            Dim fromL = fromPath
                            Dim toL = targetPath
                            Dim cnamLog = If(sub_.TieneIndiceDeColor(), sub_.SubstitutionColorRemappingIndex.ToString("F4"), "none")
                            Logger.LogLazy(Function() $"[MSWP-APPLIED] shape='{shapeNameLog}' from='{fromL}' -> to='{toL}' cnam={cnamLog} loadResult=OK")
                        End If
                    ElseIf logEnabled Then
                        Dim shapeNameLog = shape.ShapeName
                        Dim fromL = fromPath
                        Dim toL = targetPath
                        Logger.LogLazy(Function() $"[MSWP-APPLIED-LOAD-FAIL] shape='{shapeNameLog}' from='{fromL}' -> to='{toL}' loadResult=NULL - material unchanged")
                    End If
                    matched = True
                    Exit For
                End If
            Next

            If Not matched AndAlso logEnabled Then
                Dim shapeNameLog = shape.ShapeName
                Dim ccpLog = correctedCurrentPath
                Logger.LogLazy(Function() $"[MSWP-NO-MATCH] shape='{shapeNameLog}' currentPath='{ccpLog}' subs={subsCount} - no substitution matched")
            End If
        Next
    End Sub

    ' ================================================================================= a base object's own Model

    ''' <summary>The material of a base object's own Model and of its instance (ola 0c pto 17, the engine's 0x140255EC0): its material swap
    ''' (MODS) as SET, then the instance's OMOD properties (<paramref name="instance"/>: material swaps applied, ColorRemappingIndex
    ''' accumulated into the instance's index - OmodResolutionApplier), then ONE color remapping index per shape (ApplyColorRemapIndex:
    ''' instance &gt; the matched substitution's CNAM &gt; MODC). One place for that order: NPC Manager (ARMA base swap + the ARMO's
    ''' OBTS / OMOD) and SafeScrap (object preview) call it.
    ''' <para>FormID overload: a 0 swap is not applied (no <c>[MSWP-ENTRY]</c> is logged for it).</para></summary>
    Public Sub ApplyModelMaterial(swapFormID As UInteger,
                                  colorRemapIndex As Single?,
                                  shapes As IEnumerable(Of IRenderableShape),
                                  pluginManager As PluginManager,
                                  Optional instance As ObjectTemplateResolver.CombinationResolution = Nothing,
                                  Optional instanceFormType As String = Nothing)
        Dim list = shapes.ToList()
        Dim applySwap As Action(Of ColorRemapInputs) = Sub(inputs As ColorRemapInputs)
                                                           If swapFormID <> 0UI Then ApplyMaterialSwap(swapFormID, list, pluginManager, inputs)
                                                       End Sub
        ApplyModelMaterialCore(applySwap, colorRemapIndex, list, instance, instanceFormType, pluginManager)
    End Sub

    ''' <summary>Parsed-swap overload (a draft MSWP already resolved by the caller). A Nothing swap is skipped and the
    ''' color is STILL resolved — an unresolvable draft swap must not drop the color.</summary>
    Public Sub ApplyModelMaterial(swap As Canon.IMswp,
                                  colorRemapIndex As Single?,
                                  shapes As IEnumerable(Of IRenderableShape),
                                  Optional instance As ObjectTemplateResolver.CombinationResolution = Nothing,
                                  Optional instanceFormType As String = Nothing,
                                  Optional pluginManager As PluginManager = Nothing)
        Dim list = shapes.ToList()
        Dim applySwap As Action(Of ColorRemapInputs) = Sub(inputs As ColorRemapInputs)
                                                           If swap IsNot Nothing Then ApplyMaterialSwap(swap, list, inputs)
                                                       End Sub
        ApplyModelMaterialCore(applySwap, colorRemapIndex, list, instance, instanceFormType, pluginManager)
    End Sub

    ''' <summary>The one sequence of both overloads: the model's swap, the instance's OMOD properties, the index written once.</summary>
    Private Sub ApplyModelMaterialCore(applySwap As Action(Of ColorRemapInputs), colorRemapIndex As Single?, shapes As List(Of IRenderableShape),
                                       instance As ObjectTemplateResolver.CombinationResolution, instanceFormType As String, pluginManager As PluginManager)
        Dim inputs As New ColorRemapInputs With {.Model = colorRemapIndex}
        applySwap(inputs)
        If instance IsNot Nothing Then OmodResolutionApplier.ApplyResolutionToShapes(instance, instanceFormType, shapes, pluginManager, inputs)
        ' The instance's MaterialSwaps list, in order, after the model's swap (0x140255F8E, 0x140255FB1..020). Each pass looks up the
        ' shape's ORIGINAL NIF path (Nifcontent_Class_Manolo.NifMaterialPath): [prop+0x10] is not renamed (0x140256677) because [0x142ED8B58] = 0
        ' in equipment / biped (0x140434F60, 0x140247F90), NPC, placed reference (0x14050AF40, XMSP 0x14050BBC0) and actor; it is 1 only
        ' in a SCOL member clone (0x14043B4D0 -&gt; 0x14043C720 -&gt; 0x14043DEE0 -&gt; 0x140255E00), which no app view does (resultado5 R1).
        For Each fid In inputs.InstanceSwaps
            ApplyMaterialSwap(fid, shapes, pluginManager, inputs)
        Next
        ApplyColorRemapIndex(inputs, shapes)
    End Sub

End Module
