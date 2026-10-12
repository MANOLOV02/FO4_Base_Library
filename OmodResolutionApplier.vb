Imports System.Linq
Imports FO4_Base_Library
Imports FO4_Base_Library.Canon.CanonInterpretacion

''' <summary>
''' Applies a CombinationResolution (from ObjectTemplateResolver) to a set of shapes by
''' walking visual Properties in declaration order and dispatching to ShapeMaterialOverrides.
'''
''' Order — verified in dump v2 (vanilla 4473 NPCs / 580 ARMOs):
'''   1. DirectProperties of every applied combination (in record declaration order).
'''   2. Then for each IncludedOmod (in walk order), its own Properties (same dispatch).
'''
''' SET vs ADD semantics fall out naturally from this order: each ShapeMaterialOverrides.ApplyMaterialSwap
''' mutates the shape's current material in place, so a later ADD acts on whatever the previous SET left
''' there; a ColorRemappingIndex property accumulates into the instance's index (ola 0c pto 17), which
''' ShapeMaterialOverrides.ApplyColorRemapIndex writes once per shape.
''' No vanilla case mixes SET+ADD inside a single combination's Properties (33 NPC_ cases
''' have multiple MSWPs of the same op kind), but the order-respecting walk handles both
''' cases uniformly.
'''
''' FormType context (passed by caller) tells the applier which PropertyIndex enum interprets
''' the property's idx — wbArmorPropertyEnum (idx 12 / 13) for ARMO, wbActorPropertyEnum
''' (idx 4 / 5) for NPC_. WEAP is parsed but ignored for shape rendering (weapon meshes are
''' a separate path).
'''
''' Non-visual properties (Keywords, ForcedInventory, Weight, Value, Rating, etc.) are
''' silently skipped — they have no rendering side-effect.
'''
''' OMOD chunks (FormType=NPC_ with non-empty ModelPath) are NOT applied here — they need
''' AttachPoint mounting via BSConnectPoint, which is a different path
''' (ConnectPointMountResolver + BSConnectPointBoneInjector). They're logged with
''' [OMOD-MESH-DEFERRED] so we can see where the shapes would have gone.
''' </summary>
Public Module OmodResolutionApplier

    Public Sub ApplyResolutionToShapes(res As ObjectTemplateResolver.CombinationResolution,
                                       formTypeContext As String,
                                       shapes As IEnumerable(Of IRenderableShape),
                                       pm As PluginManager,
                                       remap As ShapeMaterialOverrides.ColorRemapInputs)
        If res Is Nothing OrElse shapes Is Nothing OrElse pm Is Nothing Then Return
        If res.AppliedCombinations.Count = 0 AndAlso res.DirectProperties.Count = 0 AndAlso res.IncludedOmods.Count = 0 Then Return

        ' Materialize once up front. shapes may be a lazy LINQ sequence; every property below re-walks
        ' it (and ShapeMaterialOverrides re-walks it again per shape), so without this the source
        ' enumerable would be re-evaluated once per property. ToList makes downstream walks O(shapes).
        Dim shapeList = shapes.ToList()

        Dim logEnabled = Logger.Enabled
        If logEnabled Then
            Dim shapeCount = shapeList.Count
            Dim directCount = res.DirectProperties.Count
            Dim omodCount = res.IncludedOmods.Count
            Logger.LogLazy(Function() $"[OMOD-APPLY-ENTRY] ctx={formTypeContext} shapes={shapeCount} directProps={directCount} omods={omodCount}")
        End If

        ' (1) DirectProperties of applied combinations — walked in the order ObjectTemplateResolver
        ' produced them (which matches record declaration order).
        For Each prop In res.DirectProperties
            ApplyOneProperty(prop, formTypeContext, shapeList, pm, "DIRECT", remap)
        Next

        ' (2) Properties carried by each IncludedOmod, in walk order.
        For Each omod In res.IncludedOmods
            If omod Is Nothing Then Continue For

            ' Skip OMODs whose target FormType doesn't match — vanilla example: OMODs targeting
            ' NONE (workshop wrappers) reached via inventory-side chains. They contribute no
            ' visual change to the actor.
            Dim omodFormType = ObjectTemplateResolver.OmodFormTypeSignature(omod.DataFormType)
            If omodFormType <> formTypeContext Then
                If logEnabled Then
                    Dim oL = omod
                    Logger.LogLazy(Function() $"[OMOD-APPLY-OMOD-SKIP] omod={oL.EditorID}(0x{oL.FormID:X8}) omodFormType='{omodFormType}' ctx='{formTypeContext}' propsDropped={oL.Properties.Count}")
                End If
                Continue For
            End If

            If logEnabled Then
                Dim oL2 = omod
                Dim propsLen = omod.Properties.Count
                Dim hasMesh = Not String.IsNullOrEmpty(omod.ModelFileName)
                Logger.LogLazy(Function() $"[OMOD-APPLY-OMOD] omod={oL2.EditorID}(0x{oL2.FormID:X8}) hasMesh={hasMesh} props={propsLen}")
            End If

            For Each prop In omod.Properties
                ' La misma Property se ve por dos interfaces: la del OMOD y la de FORMA, que es la que
                ' comparte con las Properties inline de una combinación. La clase generada declara las
                ' dos, así que el cast no puede fallar; si fallara sería un defecto del generador y
                ' conviene que se oiga.
                ApplyOneProperty(DirectCast(prop, Canon.IBloque_Properties4), formTypeContext, shapeList, pm, $"OMOD:{omod.FormID:X8}", remap)
            Next
        Next
    End Sub

    ' ───────────────────────────── internals ─────────────────────────────

    Private Sub ApplyOneProperty(vista As Canon.IBloque_Properties4,
                                 formTypeContext As String,
                                 shapes As IEnumerable(Of IRenderableShape),
                                 pm As PluginManager,
                                 sourceTag As String,
                                 remap As ShapeMaterialOverrides.ColorRemapInputs)
        Dim prop = vista.LeerPropiedad()
        Dim logEnabled = Logger.Enabled
        ' Dispatch by (FormType context, PropertyIdx) — only visual properties produce side-effects.
        Dim dispatched As String = "SKIP"
        Select Case formTypeContext
            Case "ARMO"
                Select Case prop.PropertyIndex
                    Case 12US ' ColorRemappingIndex (Float)
                        dispatched = "CREMAP"
                    Case 13US ' MaterialSwaps (FormID,Int)
                        dispatched = "MSWP"
                End Select
            Case "NPC_"
                Select Case prop.PropertyIndex
                    Case 4US ' ColorRemappingIndex (NPC_) — 0 vanilla cases per dump v2 but supported
                        dispatched = "CREMAP"
                    Case 5US ' MaterialSwaps (FormID,Int)
                        dispatched = "MSWP"
                End Select
                ' WEAP / NONE — out of scope for shape rendering.
        End Select

        If logEnabled Then
            Dim propL = prop
            Dim srcL = sourceTag
            Dim ctxL = formTypeContext
            Dim dispL = dispatched
            Logger.LogLazy(Function() $"[OMOD-APPLY-PROP] src={srcL} ctx={ctxL} idx={propL.PropertyIndex} func={propL.FunctionType} v1=0x{propL.Value1FormID:X8}/v1f={propL.Value1:F3} v2f={propL.Value2:F3} → {dispL}")
        End If

        Select Case dispatched
            Case "CREMAP"
                ApplyColorRemapProp(prop, sourceTag, remap)
            Case "MSWP"
                ApplyMswpProp(prop, pm, sourceTag, remap)
        End Select
    End Sub

    ''' <summary>Ola 0c pto 17 (resultado5 rev-03 cierre, rev-23): an OMOD MaterialSwaps property edits the instance's swap list (ARMO
    ''' InstanceData vt+0x50 0x140461150 -&gt; [+0x18]) with the handler 0x140421B60's law (ShapeMaterialOverrides.EditInstanceSwaps), only
    ''' when the form is an MSWP (0x140421B7B, form type 0x91); ShapeMaterialOverrides.ApplyModelMaterial applies the list after the
    ''' model's swap, in order (0x140255F8E, 0x140255FB1..020).</summary>
    Private Sub ApplyMswpProp(prop As OMOD_Property,
                              pm As PluginManager,
                              sourceTag As String,
                              remap As ShapeMaterialOverrides.ColorRemapInputs)
        Dim mswpFid = prop.Value1FormID
        If mswpFid = 0UI Then Return
        Dim rec = pm?.GetRecord(mswpFid)
        If rec Is Nothing OrElse rec.Header.Signature <> "MSWP" Then Return       ' 0x140421B7B: form type 0x91 only
        ShapeMaterialOverrides.EditInstanceSwaps(remap.InstanceSwaps, CInt(prop.FunctionType), mswpFid)
    End Sub

    ''' <summary>Ola 0c pto 17: an OMOD ColorRemappingIndex property accumulates into the instance's index (+0x44, read by vfunc +0x80,
    ''' ARMO 0x1404612A0); ShapeMaterialOverrides.ApplyColorRemapIndex writes it once per shape. SET = the value; ADD = the current value
    ''' + Value1. NOT TRACED (declared, user decision 12-oct-2026: future): the writer of +0x44 (the float property law SET / ADD /
    ''' MUL_ADD) - an ADD with no value yet and MUL_ADD are not applied, and logged. Census 12-oct (omod_muladd.py): the 69
    ''' ColorRemappingIndex properties of the 80 plugins are all SET.</summary>
    Private Sub ApplyColorRemapProp(prop As OMOD_Property,
                                    sourceTag As String,
                                    remap As ShapeMaterialOverrides.ColorRemapInputs)
        Select Case prop.FunctionType
            Case 0
                remap.Instance = prop.Value1
            Case 2
                If remap.Instance.HasValue Then
                    remap.Instance = remap.Instance.Value + prop.Value1
                ElseIf Logger.Enabled Then
                    Logger.LogLazy(Function() $"[CREMAP-ADD-NO-BASE] src={sourceTag} v1={prop.Value1:F4} - the instance index has no value yet; its initial value is not traced")
                End If
            Case 1
                If Logger.Enabled Then
                    Logger.LogLazy(Function() $"[CREMAP-MUL_ADD-STUB] src={sourceTag} v1={prop.Value1:F4} v2={prop.Value2:F4} - MUL_ADD not implemented (future)")
                End If
        End Select
    End Sub

End Module
