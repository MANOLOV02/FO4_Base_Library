Imports FO4_Base_Library
Imports FO4_Base_Library.Canon.CanonInterpretacion

''' <summary>
''' Per-shape material override helpers shared by the human path and the OMOD path.
''' </summary>
Public Module ShapeMaterialOverrides

    ''' <summary>FormID-typed Property function used by MSWP property idx 13.</summary>
    Public Enum MaterialSwapFunction As Byte
        [SET] = 0
        Remov = 1
        ADD = 2
    End Enum

    ''' <summary>Float-typed Property function used by ColorRemappingIndex property idx 12.</summary>
    Public Enum ColorRemapFunction As Byte
        [SET] = 0
        MUL_ADD = 1
        ADD = 2
    End Enum

    ''' <summary>FormID overload: resolve the MSWP record via the plugin manager, then delegate to the
    ''' parsed-data overload. Callers: robot / OMOD / WM.</summary>
    Public Sub ApplyMaterialSwap(mswpFormID As UInteger,
                                 funcType As MaterialSwapFunction,
                                 shapes As IEnumerable(Of IRenderableShape),
                                 pluginManager As PluginManager)
        Dim logEnabled = Logger.Enabled
        If logEnabled Then
            Logger.LogLazy(Function() $"[MSWP-ENTRY] mswp=0x{mswpFormID:X8} func={funcType}")
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
        ApplyMaterialSwap(mswp, funcType, shapes)
    End Sub

    ''' <summary>Parsed-data overload: applies an ALREADY-PARSED <see cref="Canon.IMswp"/> to the shapes, so a
    ''' DRAFT MSWP (no record to resolve by FormID) can be applied straight from its parsed substitutions. No
    ''' <see cref="PluginManager"/> is needed — this is pure material-path matching + replacement on the
    ''' shapes.</summary>
    Public Sub ApplyMaterialSwap(mswp As Canon.IMswp,
                                 funcType As MaterialSwapFunction,
                                 shapes As IEnumerable(Of IRenderableShape))
        If mswp Is Nothing Then Return
        Dim logEnabled = Logger.Enabled
        Dim mswpFormID = mswp.FormID

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

        Dim isRemove = (funcType = MaterialSwapFunction.Remov)
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

            Dim currentPath = If(relatedMaterial.path, "").Trim()
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
                Dim dirLog = If(isRemove, "REM", "SET/ADD")
                Dim ccpLog = correctedCurrentPath
                Logger.LogLazy(Function() $"[MSWP-SHAPE] shape='{shapeNameLog}' currentPath='{ccpLog}' dir={dirLog}")
            End If

            Dim matched As Boolean = False
            For Each sub_ In mswp.Sustituciones()
                ' SET/ADD match Original->Replacement; REM matches Replacement->Original.
                Dim fromPath = FO4UnifiedMaterial_Class.CorrectMaterialPath(If(If(isRemove, sub_.SubstitutionReplacementMaterial, sub_.SubstitutionOriginalMaterial), ""))
                If fromPath = "" Then Continue For

                If String.Equals(correctedCurrentPath, fromPath, StringComparison.OrdinalIgnoreCase) Then
                    Dim targetPath = If(If(isRemove, sub_.SubstitutionOriginalMaterial, sub_.SubstitutionReplacementMaterial), "")
                    If targetPath = "" Then
                        If logEnabled Then
                            Dim shapeNameLog = shape.ShapeName
                            Dim fromL = fromPath
                            Dim dirLog = If(isRemove, "REM", "SET/ADD")
                            Logger.LogLazy(Function() $"[MSWP-MATCH-EMPTY-TARGET] shape='{shapeNameLog}' from='{fromL}' target='' dir={dirLog}")
                        End If
                        matched = True
                        Exit For
                    End If

                    Dim newMaterial = MaterialResolver.TryLoadMaterialFromDictionary(targetPath, relatedMaterial.material, shape.NifShape, shape.NifContent)
                    If newMaterial IsNot Nothing Then
                        relatedMaterial.material = newMaterial
                        relatedMaterial.path = FO4UnifiedMaterial_Class.CorrectMaterialPath(targetPath)
                        ' Per-substitution Color Remap Index (CNAM): in the engine a material-swap
                        ' substitution's color-remap index overrides the swapped-IN material's
                        ' grayscale-to-palette scale (the palette column selected on lookup). Skip it and the
                        ' replacement renders at its AUTHORED GrayscaleToPaletteScale, so the value edited in
                        ' the substitution dialog has no visual effect. Only on SET/ADD (the swap-in direction);
                        ' a REMOVE restores the original material, whose scale we must not touch. Setting
                        ' it on a non-grayscale material is a harmless visual no-op (matches ApplyColorRemap).
                        If Not isRemove AndAlso sub_.TieneIndiceDeColor() Then
                            newMaterial.GrayscaleToPaletteScale = sub_.SubstitutionColorRemappingIndex
                        End If
                        If logEnabled Then
                            Dim shapeNameLog = shape.ShapeName
                            Dim fromL = fromPath
                            Dim toL = targetPath
                            Dim dirLog = If(isRemove, "REM", "SET/ADD")
                            Dim cnamLog = If(sub_.TieneIndiceDeColor(), sub_.SubstitutionColorRemappingIndex.ToString("F4"), "none")
                            Logger.LogLazy(Function() $"[MSWP-APPLIED] shape='{shapeNameLog}' from='{fromL}' -> to='{toL}' dir={dirLog} cnam={cnamLog} loadResult=OK")
                        End If
                    ElseIf logEnabled Then
                        Dim shapeNameLog = shape.ShapeName
                        Dim fromL = fromPath
                        Dim toL = targetPath
                        Dim dirLog = If(isRemove, "REM", "SET/ADD")
                        Logger.LogLazy(Function() $"[MSWP-APPLIED-LOAD-FAIL] shape='{shapeNameLog}' from='{fromL}' -> to='{toL}' dir={dirLog} loadResult=NULL - material unchanged")
                    End If
                    matched = True
                    Exit For
                End If
            Next

            If Not matched AndAlso logEnabled Then
                Dim shapeNameLog = shape.ShapeName
                Dim ccpLog = correctedCurrentPath
                Dim dirLog = If(isRemove, "REM", "SET/ADD")
                Logger.LogLazy(Function() $"[MSWP-NO-MATCH] shape='{shapeNameLog}' currentPath='{ccpLog}' subs={subsCount} dir={dirLog} - no substitution matched")
            End If
        Next
    End Sub

    Public Sub ApplyColorRemap(value1 As Single,
                               value2 As Single,
                               funcType As ColorRemapFunction,
                               shapes As IEnumerable(Of IRenderableShape))
        Dim logEnabled = Logger.Enabled
        If logEnabled Then
            Logger.LogLazy(Function() $"[CREMAP-ENTRY] func={funcType} v1={value1:F4} v2={value2:F4}")
        End If

        If shapes Is Nothing Then Return

        If funcType = ColorRemapFunction.MUL_ADD Then
            If logEnabled Then
                Logger.LogLazy(Function() $"[CREMAP-MUL_ADD-STUB] v1={value1:F4} v2={value2:F4} - MUL_ADD not implemented, no-op")
            End If
            Return
        End If

        For Each shape In shapes
            MaterialResolver.EnsureShapeMaterialResolved(shape)

            Dim relatedMaterial = shape.ShapeMaterial
            If relatedMaterial Is Nothing Then
                If logEnabled Then
                    Dim shapeNameLog = shape.ShapeName
                    Logger.LogLazy(Function() $"[CREMAP-SHAPE-SKIP] shape='{shapeNameLog}' reason='no-related-material'")
                End If
                Continue For
            End If

            Dim material = relatedMaterial.material
            If material Is Nothing Then
                If logEnabled Then
                    Dim shapeNameLog = shape.ShapeName
                    Logger.LogLazy(Function() $"[CREMAP-SHAPE-SKIP] shape='{shapeNameLog}' reason='material-Nothing'")
                End If
                Continue For
            End If

            Dim oldScale = material.GrayscaleToPaletteScale
            Dim paletteEnabled = material.GrayscaleToPaletteColor
            Dim paletteTex = If(material.GreyscaleTexture, "")
            Dim newScale As Single
            Select Case funcType
                Case ColorRemapFunction.SET
                    newScale = value1
                Case ColorRemapFunction.ADD
                    newScale = oldScale + value1
                Case Else
                    Continue For
            End Select

            material.GrayscaleToPaletteScale = newScale

            If logEnabled Then
                Dim shapeNameLog = shape.ShapeName
                Dim oldL = oldScale
                Dim newL = newScale
                Dim palL = paletteEnabled
                Dim palTexL = paletteTex
                Logger.LogLazy(Function() $"[CREMAP-APPLIED] shape='{shapeNameLog}' palEnabled={palL} palTex='{palTexL}' oldScale={oldL:F4} -> newScale={newL:F4}")
                If Not paletteEnabled Then
                    Logger.LogLazy(Function() $"[CREMAP-NO-PALETTE] shape='{shapeNameLog}' newScale={newL:F4} but GrayscaleToPaletteColor=False - visual no-op")
                End If
            End If
        Next
    End Sub

End Module
