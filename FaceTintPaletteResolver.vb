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

    ''' <summary>TIE-BREAK por OPACITY (UMBRAL en 0): una capa ACTIVA (opacity&gt;0) usa una entrada Alpha&gt;0
    ''' ("encendida"); SOLO opacity==0 usa la entrada Alpha=0 ("apagado/default"). Filtra el pool a la clase de
    ''' Alpha correcta; si esa clase queda vacia, usa el pool completo (no romper). Dentro de la clase, desempate
    ''' por Alpha mas cercano a npcOpacity, luego TemplateIndex menor. Usada por (a) FindTemplateColorByColor
    ''' (2+ presets comparten color) y (b) la moda empatada de ResolveFallbackBlendOp. Nothing si no hay candidatos.
    ''' 2026-06-03: el "primera opcion (min TemplateIndex)" ROMPIA la capa topmost custom (-1) del dirt (Polvo
    ''' radiactivo, rojo): elegia la entrada Alpha=0/default (bop3/SoftLight) y la libreria renderea SoftLight de
    ''' un rojo custom como TRANSPARENTE. El umbral garantiza que una capa activa NUNCA tome la entrada Alpha=0
    ''' (off): Polvo (op=1.0) -> Alpha=1 -> bop0/Replace (opaco). Mas robusto que "Alpha mas cercano" (que para
    ''' op&lt;0.5 tomaria Alpha=0 y re-rompe). El bake es per-capa; el "primera opcion" matcheaba el DISPLAY
    ''' per-grupo del editor de CK, no el bake.</summary>
    Public Function BreakTieByOpacity(candidates As IEnumerable(Of RACE_TintTemplateColor), npcOpacity As Single) As RACE_TintTemplateColor
        If candidates Is Nothing Then Return Nothing
        Dim wantPositive As Boolean = (npcOpacity > 0.0F)
        Dim pool As New List(Of RACE_TintTemplateColor)
        For Each tc In candidates
            If tc IsNot Nothing AndAlso (tc.Alpha > 0.0F) = wantPositive Then pool.Add(tc)
        Next
        If pool.Count = 0 Then
            For Each tc In candidates
                If tc IsNot Nothing Then pool.Add(tc)
            Next
        End If
        Dim best As RACE_TintTemplateColor = Nothing
        Dim bestDist As Single = Single.MaxValue
        For Each tc In pool
            Dim dist As Single = Math.Abs(tc.Alpha - npcOpacity)
            If dist < bestDist OrElse (dist = bestDist AndAlso best IsNot Nothing AndAlso tc.TemplateIndex < best.TemplateIndex) Then
                bestDist = dist
                best = tc
            End If
        Next
        Return best
    End Function

    ''' <summary>BlendOp por defecto del grupo cuando NO hay match de color (Step3). Regla del usuario:
    ''' = la MODA del BlendOp entre los TemplateColors activos (Alpha&gt;0) de la option; si la moda está
    ''' EMPATADA, se rompe con el MISMO tie-break compartido (Alpha vs opacity = <see cref="BreakTieByOpacity"/>).
    ''' Robusto contra outliers de autoría (Maquillaje tplCol[9] BlendOp=0 vs mayoría=3).
    ''' Safety net: SkinTone slot + BlendOp=0 → 3 (SoftLight).
    ''' Convenio: 0=Default(replace), 1=Multiply, 2=Overlay, 3=SoftLight, 4=HardLight.
    ''' </summary>
    Public Function ResolveFallbackBlendOp(opt As RACE_TintTemplateOption, npcOpacity As Single) As UInteger
        If opt Is Nothing Then Return 0UI

        ' TODOS los tplColors entran (sin gate de Alpha): Alpha=0 es el valor-default del slider, NO un
        ' gate de validez — coherente con Step 1 (índice) y FindTemplateColorByColor, que ya aceptan
        ' Alpha=0. El gate Alpha>0 acá quedó STALE. (Empíricamente no mueve el dump de Alana: el grime
        ' resuelve por Step 1/índice, no por este fallback; pero elimina la incoherencia del resolver.)
        Dim active As New List(Of RACE_TintTemplateColor)
        If opt.TemplateColors IsNot Nothing Then
            For Each tc In opt.TemplateColors
                If tc IsNot Nothing Then active.Add(tc)
            Next
        End If

        Dim modeBlendOp As UInteger
        If active.Count > 0 Then
            Dim counts As New Dictionary(Of UInteger, Integer)
            For Each tc In active
                Dim bop = tc.BlendOperation
                counts(bop) = If(counts.ContainsKey(bop), counts(bop) + 1, 1)
            Next
            Dim maxCount As Integer = counts.Values.Max()
            Dim tied = counts.Where(Function(kvp) kvp.Value = maxCount).Select(Function(kvp) kvp.Key).ToList()
            If tied.Count = 1 Then
                modeBlendOp = tied(0)
            Else
                ' MODA EMPATADA -> mismo tie-break compartido (Alpha vs opacity) entre los tplColors
                ' activos cuyo BlendOp está empatado.
                Dim tiedCols = active.Where(Function(c) tied.Contains(c.BlendOperation)).ToList()
                Dim winner = BreakTieByOpacity(tiedCols, npcOpacity)
                modeBlendOp = If(winner IsNot Nothing, winner.BlendOperation, tied.Min())
                Logger.LogLazy(Function() $"[FACETINT-FALLBACK] opt={opt.Index} '{opt.Name}' moda-empate tied=[{String.Join(",", tied)}] op={npcOpacity} -> {modeBlendOp}")
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
        ' Recolectar TODOS los presets que comparten el color exacto, y romper el empate con el tie-break
        ' COMPARTIDO (Alpha vs opacity) — el mismo que usa la moda-empate del fallback.
        Dim matches As New List(Of RACE_TintTemplateColor)
        For Each tplCol In opt.TemplateColors
            If tplCol.ColorFormID = 0UI Then Continue For
            Dim clfmRec = pm.GetRecord(tplCol.ColorFormID)
            If clfmRec Is Nothing OrElse clfmRec.Header.Signature <> "CLFM" Then Continue For
            Dim clfm = RecordParsers.ParseCLFM(clfmRec, pm)
            If clfm Is Nothing OrElse Not clfm.HasColor Then Continue For
            If clfm.Color.R = targetR AndAlso clfm.Color.G = targetG AndAlso clfm.Color.B = targetB Then
                matches.Add(tplCol)
            End If
        Next
        Return BreakTieByOpacity(matches, npcOpacity)
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
        Dim resolvedBlendOp As UInteger = ResolveFallbackBlendOp(opt, tl.Value / 100.0F)
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
