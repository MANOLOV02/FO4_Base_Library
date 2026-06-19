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
    ''' <summary>Filtro de clase activo/off: una capa ACTIVA (op&gt;0) usa entradas Alpha&gt;0 ("encendida");
    ''' SOLO op==0 usa Alpha=0 ("apagado/default"). Si la clase queda vacia, devuelve el pool completo
    ''' (no romper). Es el discriminante robusto del 2026-06-03 (dirt rojo custom op&gt;0 -&gt; Alpha&gt;0 -&gt;
    ''' Replace, NO la off-entry SoftLight transparente).</summary>
    Private Function FilterByOpacityClass(candidates As IEnumerable(Of RACE_TintTemplateColor), npcOpacity As Single) As List(Of RACE_TintTemplateColor)
        Dim pool As New List(Of RACE_TintTemplateColor)
        If candidates Is Nothing Then Return pool
        Dim wantPositive As Boolean = (npcOpacity > 0.0F)
        For Each tc In candidates
            If tc IsNot Nothing AndAlso (tc.Alpha > 0.0F) = wantPositive Then pool.Add(tc)
        Next
        If pool.Count = 0 Then
            For Each tc In candidates
                If tc IsNot Nothing Then pool.Add(tc)
            Next
        End If
        Return pool
    End Function

    ''' <summary>TemplateColor default de la opcion segun TTED = index POSICIONAL en TemplateColors (raw
    ''' bits del float = el int). Nothing si no hay TTED o el indice cae fuera de rango. MISMO index que
    ''' MergeTintLayersWithRaceDefaults usa para los defaults heredados. Ver
    ''' [[arch_facetint_race_default_inheritance]].</summary>
    Public Function TtedDefaultColor(opt As RACE_TintTemplateOption) As RACE_TintTemplateColor
        If opt Is Nothing OrElse Not opt.HasDefaultValue Then Return Nothing
        If opt.TemplateColors Is Nothing OrElse opt.TemplateColors.Count = 0 Then Return Nothing
        Dim pos As UInteger = BitConverter.ToUInt32(BitConverter.GetBytes(opt.DefaultValue), 0)
        If pos >= CUInt(opt.TemplateColors.Count) Then Return Nothing
        Return opt.TemplateColors(CInt(pos))
    End Function

    ''' <summary>DESEMPATE UNIFICADO (reusable). Elige UN TemplateColor de <paramref name="candidates"/>
    ''' dado opt + opacidad de la capa:
    '''   1. Filtra a la clase activo/off (op&gt;0 -&gt; Alpha&gt;0; op=0 -&gt; Alpha=0; vacio -&gt; todos).
    '''   2. Si el TTED-default cae en esa clase -&gt; ese (el preset default de la opcion; regla usuario
    '''      2026-06-06). Asi se respeta el blend intencional del default SIN tomar la off-entry para una
    '''      capa activa (el dirt rojo de Alana: TTED apunta a la off-entry SoftLight, pero al estar activa
    '''      queda fuera del filtro -&gt; gana la activa Replace).
    '''   3. Si no -&gt; Alpha mas cercano a op, luego TemplateIndex menor.
    ''' Usado por FindTemplateColorByColor (Step 2: varios presets MISMO color) y ResolveFallbackBlendOp
    ''' (Step 3: sin match -&gt; sobre TODOS los presets de la opcion). Nothing si no hay candidatos.</summary>
    Public Function PickTemplateColor(candidates As IEnumerable(Of RACE_TintTemplateColor), opt As RACE_TintTemplateOption, npcOpacity As Single) As RACE_TintTemplateColor
        Dim pool = FilterByOpacityClass(candidates, npcOpacity)
        If pool.Count = 0 Then Return Nothing
        Dim tdef = TtedDefaultColor(opt)
        If tdef IsNot Nothing AndAlso pool.Contains(tdef) Then Return tdef
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

    ''' <summary>Compat: desempate por opacidad SIN preferencia de TTED-default (= PickTemplateColor con
    ''' opt=Nothing). La logica vive en PickTemplateColor; conservada por si hay refs externas.</summary>
    Public Function BreakTieByOpacity(candidates As IEnumerable(Of RACE_TintTemplateColor), npcOpacity As Single) As RACE_TintTemplateColor
        Return PickTemplateColor(candidates, Nothing, npcOpacity)
    End Function

    ''' <summary>BlendOp para un color CUSTOM sin match por color (Step 3). Desempate UNIFICADO
    ''' (<see cref="PickTemplateColor"/>) sobre TODOS los TemplateColors de la opcion: clase activo/off ->
    ''' TTED-default-si-cae-en-la-clase -> Alpha mas cercano / TemplateIndex menor. Reemplaza la "moda"
    ''' estadistica previa por el MISMO desempate del Step 2. Para una capa ACTIVA el filtro activo/off
    ''' descarta la off-entry (Alpha=0), asi que el dirt rojo custom de Alana (cuyo TTED apunta a la
    ''' off-entry SoftLight) cae en la activa = Replace, no en SoftLight transparente (regresion 2026-06-03
    ''' evitada). Safety net: SkinTone + 0 -> 3. Convenio: 0=Replace 1=Multiply 2=Overlay 3=SoftLight
    ''' 4=HardLight.</summary>
    Public Function ResolveFallbackBlendOp(opt As RACE_TintTemplateOption, npcOpacity As Single) As UInteger
        If opt Is Nothing Then Return 0UI
        Dim pick = PickTemplateColor(opt.TemplateColors, opt, npcOpacity)
        Dim bop As UInteger
        If pick IsNot Nothing Then
            bop = pick.BlendOperation
        ElseIf opt.HasBlendOperation Then
            bop = opt.BlendOperation
        Else
            bop = 0UI
        End If
        If opt.Slot = CUShort(TintSlot.SkinTone) AndAlso bop = 0UI Then Return 3UI
        Return bop
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
        ' Desempate UNIFICADO (mismo que el fallback Step 3): clase activo/off -> TTED-default-si-cae ->
        ' Alpha/idx. Asi "dos del mismo color" se resuelve con la misma regla que el resto.
        Return PickTemplateColor(matches, opt, npcOpacity)
    End Function

    ''' <summary>BlendOp por capa = regla del Creation-Kit (<see cref="ResolveBlendOpCk"/>: last-color-match)
    ''' sobre el color efectivo, con la red SkinTone (slot-12 + 0 -&gt; 3/SoftLight). UNICA fuente del blend-op
    ''' por capa usada por <see cref="ResolvePaletteLayerEffective"/> en cada return.</summary>
    Private Function CkBlendOpWithSkinToneNet(opt As RACE_TintTemplateOption, color As Color, value As Integer, pm As PluginManager) As UInteger
        Dim bop As UInteger = ResolveBlendOpCk(opt, color, value, pm)
        If opt IsNot Nothing AndAlso opt.Slot = CUShort(TintSlot.SkinTone) AndAlso bop = 0UI Then bop = 3UI
        Return bop
    End Function

    ''' <summary>Resuelve Color/BlendOp/Matched/OpacityScale para una Palette (disc=1) layer.
    ''' COLOR + OpacityScale + Matched (cadena Step1 index / Step2 color):
    '''   Step 1: TemplateColorIndex match — honra el índice con cualquier Alpha (sin gate Alpha&gt;0).
    '''   Step 2: Color match via FindTemplateColorByColor (alpha-closest a la opacidad, acepta Alpha=0).
    ''' COLOR: index>=0 (Step1) -> color del TEMPLATE (CLFM del RACE); custom -1 (Step2) -> TEND del NPC
    ''' verbatim. Replica build_3.effective_palette_color (el engine sustituye el color cuando hay index).
    ''' BLEND-OP: regla del Creation-Kit (ResolveBlendOpCk: last-color-match sobre el color efectivo) +
    ''' red SkinTone slot-12->3, via CkBlendOpWithSkinToneNet, en TODO return. Color/OpacityScale/Matched
    ''' sin cambios respecto de la cadena Step1/Step2.
    ''' </summary>
    Public Function ResolvePaletteLayerEffective(tl As NPC_FaceTintLayerData, opt As RACE_TintTemplateOption, pm As PluginManager) As (Color As Color, BlendOp As UInteger, Matched As Boolean, OpacityScale As Single)
        Dim resolvedColor As Color = tl.Color
        Dim matched As Boolean = False
        Dim opacityScale As Single = 1.0F

        If opt Is Nothing OrElse opt.TemplateColors Is Nothing OrElse opt.TemplateColors.Count = 0 Then
            Return (resolvedColor, CkBlendOpWithSkinToneNet(opt, resolvedColor, tl.Value, pm), matched, opacityScale)
        End If

        ' Step 1: TemplateColorIndex
        If tl.TemplateColorIndex >= 0 Then
            Dim needle As UShort = CUShort(tl.TemplateColorIndex)
            Dim tplByIdx As RACE_TintTemplateColor = opt.TemplateColors.FirstOrDefault(
                Function(t) t.TemplateIndex = needle)
            ' Honour the index regardless of the preset's Alpha: the TemplateColorIndex is resolved
            ' by colour, so Alpha=0 is a legitimate target.
            If tplByIdx IsNot Nothing Then
                opacityScale = tplByIdx.Alpha
                matched = True
                ' COLOR: con TemplateColorIndex>=0 el engine usa el color del TEMPLATE (CLFM del RACE),
                ' NO el TEND del NPC. Replica build_3.effective_palette_color (TTEC[tplcolidx]); el TEND
                ' del entry solo manda en custom (index=-1, Step2). Sin esto el dirt (TEND azul/purpura,
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
                Return (resolvedColor, CkBlendOpWithSkinToneNet(opt, resolvedColor, tl.Value, pm), matched, opacityScale)
            End If
        End If

        ' Step 2: Colour match — same match as the index resolver (FindTemplateColorByColor): by
        ' colour, tie-broken to the preset whose Alpha is closest to the layer's opacity, accepting
        ' Alpha=0. So if several presets share the colour with different blend ops, the one matching
        ' the opacity wins — identical to how the TemplateColorIndex itself is chosen.
        Dim byColor = FindTemplateColorByColor(resolvedColor, tl.Value / 100.0F, opt, pm)
        If byColor IsNot Nothing Then
            opacityScale = byColor.Alpha
            matched = True
        End If

        Return (resolvedColor, CkBlendOpWithSkinToneNet(opt, resolvedColor, tl.Value, pm), matched, opacityScale)
    End Function

    ''' <summary>Blend-op EXACTO del CK (FUN_14041D220): itera TemplateColors EN ORDEN; por cada preset cuyo
    ''' CLFM color == layerColor (RGB exacto) recuerda su BlendOp (LAST gana); si ademas Alpha == value*0.0099999998
    ''' EXACTO corta. Sin color-match -> 0. THE rule: unica fuente del blend-op por capa (via
    ''' ResolvePaletteLayerEffective).</summary>
    Public Function ResolveBlendOpCk(opt As RACE_TintTemplateOption, layerColor As Color, value As Integer, pm As PluginManager) As UInteger
        If opt Is Nothing OrElse opt.TemplateColors Is Nothing OrElse pm Is Nothing Then Return 0UI
        Dim vScaled As Single = CSng(value) * 0.0099999998F
        Dim result As UInteger = 0UI
        For Each tc In opt.TemplateColors
            If tc Is Nothing OrElse tc.ColorFormID = 0UI Then Continue For
            Dim rec = pm.GetRecord(tc.ColorFormID)
            If rec Is Nothing OrElse rec.Header.Signature <> "CLFM" Then Continue For
            Dim clfm = RecordParsers.ParseCLFM(rec, pm)
            If clfm Is Nothing OrElse Not clfm.HasColor Then Continue For
            If clfm.Color.R = layerColor.R AndAlso clfm.Color.G = layerColor.G AndAlso clfm.Color.B = layerColor.B Then
                result = tc.BlendOperation
                If tc.Alpha = vScaled Then Return result
            End If
        Next
        Return result
    End Function

End Module
