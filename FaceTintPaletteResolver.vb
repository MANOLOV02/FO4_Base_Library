Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Text

''' <summary>
''' Resolver canónico para BlendOp / OpacityScale de las Palette layers (Discriminator=1)
''' de FaceTint. Single source of truth — consumido por:
'''   - FO4_NPC_Manager.FaceTintLayerBuilder (render + bake CK-side)
'''   - Tools/FaceDerivationGen.Program (dump command, output al analyzer Python)
'''   - Tools/FaceGenByteCompare/auto_analyze_esp.py (lee BlendOp+OpacityScale del dump)
'''
''' SYNC: si cambia la cadena Step1/Step2/Step3 acá, actualizar también el sweep candidato
''' del analyzer Python para reflejar la nueva semántica.
''' </summary>
Public Module FaceTintPaletteResolver

    ''' <summary>Fallback BlendOp cuando match-by-index y match-by-color fallan.
    ''' Estrategia F2: mode (mayoría) entre TemplateColors con Alpha&gt;0 dentro de la option.
    ''' Robusto contra outliers de autoría (Maquillaje tplCol[9] BlendOp=0 vs mayoría=3).
    ''' Si hay 2+ BlendOps distintos en activos, loguea para auditar pattern.
    ''' Safety net: SkinTone slot + BlendOp=0 → 3 (SoftLight).
    ''' Convenio: 0=Default(replace), 1=Multiply, 2=Overlay, 3=SoftLight, 4=HardLight.
    ''' </summary>
    Public Function ResolveFallbackBlendOp(opt As RACE_TintTemplateOption) As UInteger
        If opt Is Nothing Then Return 0UI

        Dim counts As New Dictionary(Of UInteger, Integer)
        If opt.TemplateColors IsNot Nothing Then
            For Each tc In opt.TemplateColors
                If tc.Alpha > 0.0F Then
                    Dim bop = tc.BlendOperation
                    If counts.ContainsKey(bop) Then
                        counts(bop) = counts(bop) + 1
                    Else
                        counts(bop) = 1
                    End If
                End If
            Next
        End If

        Dim modeBlendOp As UInteger
        If counts.Count > 0 Then
            modeBlendOp = counts.OrderByDescending(Function(kvp) kvp.Value).First().Key
            If counts.Count > 1 Then
                Dim sb As New StringBuilder()
                For Each kvp In counts.OrderByDescending(Function(p) p.Value)
                    If sb.Length > 0 Then sb.Append(",")
                    sb.Append($"{kvp.Key}:{kvp.Value}")
                Next
                Dim sbStr = sb.ToString()
                Logger.LogLazy(Function() $"[FACETINT-FALLBACK] opt={opt.Index} '{opt.Name}' mode={modeBlendOp} from distinct=[{sbStr}]")
            End If
        ElseIf opt.HasBlendOperation Then
            modeBlendOp = opt.BlendOperation
        Else
            modeBlendOp = 0UI
        End If

        If opt.Slot = CUShort(TintSlot.SkinTone) AndAlso modeBlendOp = 0UI Then
            Return 3UI
        End If
        Return modeBlendOp
    End Function

    ''' <summary>Find the TTEC preset a Palette layer's colour resolves to: exact CLFM RGB match,
    ''' tie-broken to the preset whose Alpha is closest to <paramref name="npcOpacity"/> (the
    ''' layer's Value/100) when several share the colour with different blend ops. Accepts Alpha=0
    ''' (Alpha is the preset's default slider value, not a validity gate). Returns Nothing when no
    ''' preset's colour matches. Single source of truth for the colour match, shared by the index
    ''' resolver (FaceTintLayerBuilder.ResolveTemplateColorIndex) and Step 2 below, so both pick the
    ''' same preset.</summary>
    Public Function FindTemplateColorByColor(layerColor As Color, npcOpacity As Single, opt As RACE_TintTemplateOption, pm As PluginManager) As RACE_TintTemplateColor
        If opt Is Nothing OrElse opt.TemplateColors Is Nothing OrElse opt.TemplateColors.Count = 0 Then Return Nothing
        If pm Is Nothing Then Return Nothing
        Dim targetR As Integer = layerColor.R
        Dim targetG As Integer = layerColor.G
        Dim targetB As Integer = layerColor.B
        Dim best As RACE_TintTemplateColor = Nothing
        Dim bestAlphaDist As Single = Single.MaxValue
        For Each tplCol In opt.TemplateColors
            If tplCol.ColorFormID = 0UI Then Continue For
            Dim clfmRec = pm.GetRecord(tplCol.ColorFormID)
            If clfmRec Is Nothing OrElse clfmRec.Header.Signature <> "CLFM" Then Continue For
            Dim clfm = RecordParsers.ParseCLFM(clfmRec, pm)
            If clfm Is Nothing OrElse Not clfm.HasColor Then Continue For
            If clfm.Color.R = targetR AndAlso clfm.Color.G = targetG AndAlso clfm.Color.B = targetB Then
                Dim dist As Single = Math.Abs(tplCol.Alpha - npcOpacity)
                If dist < bestAlphaDist Then
                    bestAlphaDist = dist
                    best = tplCol
                End If
            End If
        Next
        Return best
    End Function

    ''' <summary>Resuelve Color/BlendOp/Matched/OpacityScale para una Palette (disc=1) layer.
    ''' Cadena:
    '''   Step 1: TemplateColorIndex match — honra el índice con cualquier Alpha (sin gate Alpha&gt;0).
    '''   Step 2: Color match via FindTemplateColorByColor (alpha-closest a la opacidad, acepta Alpha=0).
    '''   Step 3: ResolveFallbackBlendOp (F2 mode) cuando no hay match de color.
    ''' COLOR: index>=0 (Step1) -> color del TEMPLATE (CLFM del RACE); custom -1 (Step2/3) -> TEND del NPC
    ''' verbatim. Replica build_3.effective_palette_color (el engine sustituye el color cuando hay index).
    ''' </summary>
    Public Function ResolvePaletteLayerEffective(tl As NPC_FaceTintLayerData, opt As RACE_TintTemplateOption, pm As PluginManager) As (Color As Color, BlendOp As UInteger, Matched As Boolean, OpacityScale As Single)
        Dim resolvedColor As Color = tl.Color
        Dim resolvedBlendOp As UInteger = ResolveFallbackBlendOp(opt)
        Dim matched As Boolean = False
        Dim opacityScale As Single = 1.0F

        If opt Is Nothing OrElse opt.TemplateColors Is Nothing OrElse opt.TemplateColors.Count = 0 Then
            Return (resolvedColor, resolvedBlendOp, matched, opacityScale)
        End If

        ' Step 1: TemplateColorIndex
        If tl.TemplateColorIndex >= 0 Then
            Dim needle As UShort = CUShort(tl.TemplateColorIndex)
            Dim tplByIdx As RACE_TintTemplateColor = opt.TemplateColors.FirstOrDefault(
                Function(t) t.TemplateIndex = needle)
            ' Honour the index regardless of the preset's Alpha: the TemplateColorIndex is resolved
            ' by colour, so Alpha=0 is a legitimate target. Gating the BlendOp on Alpha>0 dropped the
            ' matched preset's BlendOp and fell through to the fallback — wrong now that the index is
            ' colour-derived.
            If tplByIdx IsNot Nothing Then
                resolvedBlendOp = tplByIdx.BlendOperation
                opacityScale = tplByIdx.Alpha
                matched = True
                ' COLOR: con TemplateColorIndex>=0 el engine usa el color del TEMPLATE (CLFM del RACE),
                ' NO el TEND del NPC. Replica build_3.effective_palette_color (TTEC[tplcolidx]); el TEND
                ' del entry solo manda en custom (index=-1, Step2/3). Sin esto el dirt (TEND azul/purpura,
                ' template NEGRO) se pintaba azul y cubre ~60% de la cara (+40 byte en B). Si el CLFM no
                ' resuelve, se conserva el TEND (= build_3 cae a TEND cuando el indice no esta en TTEC).
                If tplByIdx.ColorFormID <> 0UI AndAlso pm IsNot Nothing Then
                    Dim tplRec = pm.GetRecord(tplByIdx.ColorFormID)
                    If tplRec IsNot Nothing AndAlso tplRec.Header.Signature = "CLFM" Then
                        Dim tplClfm = RecordParsers.ParseCLFM(tplRec, pm)
                        If tplClfm IsNot Nothing AndAlso tplClfm.HasColor Then
                            resolvedColor = tplClfm.Color
                        End If
                    End If
                End If
                If opt.Slot = CUShort(TintSlot.SkinTone) AndAlso resolvedBlendOp = 0UI Then
                    resolvedBlendOp = 3UI
                End If
                Return (resolvedColor, resolvedBlendOp, matched, opacityScale)
            End If
        End If

        ' Step 2: Colour match — same match as the index resolver (FindTemplateColorByColor): by
        ' colour, tie-broken to the preset whose Alpha is closest to the layer's opacity, accepting
        ' Alpha=0. So if several presets share the colour with different blend ops, the one matching
        ' the opacity wins — identical to how the TemplateColorIndex itself is chosen.
        Dim byColor = FindTemplateColorByColor(resolvedColor, tl.Value / 100.0F, opt, pm)
        If byColor IsNot Nothing Then
            resolvedBlendOp = byColor.BlendOperation
            opacityScale = byColor.Alpha
            matched = True
        End If

        ' Step 3: resolvedBlendOp ya tiene F2 fallback.
        If opt.Slot = CUShort(TintSlot.SkinTone) AndAlso resolvedBlendOp = 0UI Then
            resolvedBlendOp = 3UI
        End If

        Return (resolvedColor, resolvedBlendOp, matched, opacityScale)
    End Function

End Module
