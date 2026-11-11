Option Strict On
Option Explicit On

''' <summary>THE SKYRIM SE UTILITY TECHNIQUES OF THE PREVIEW AND THE g7 RECORDS THEY SELECT (SkyrimSE.exe 1.7.104; Ola SSE S1 F2.1,
''' s1_re.md 1, 3, 4; re_vertices.md 6.1): the z-prepass (RENDER_DEPTH, mode 0xC), the
''' directional shadow map (RENDER_SHADOWMAP*, mode 0xE) and the screen-space shadow mask (RENDER_SHADOWMASK).
''' <list type="bullet">
''' <item>A lighting shape's utility pass is built by BSLightingShaderProperty slot 0x2B 0x14151B1E0 (vtable 0x141B3E868) in the
''' accumulator's mode [acc+0x150] (r8d at 0x14151EC89): technique = DetermineUtilityShaderDecl (Decl) | the mode's bits | ..., pass ID
''' = technique + 0x2B (0x14151B6D5). The shape filters of that slot are SseRenderPassLaw.LightingSlot2BPasses (shared).</item>
''' <item>SetupTechnique 0x141566710: technique = pass - 0x2B (0x14156671F); VS key 0x14158E990, PS key 0x14158EA10; BeginTechnique
''' 0x141579120 with r9b = PixelShaderNull (0x141566735..78B): with r9b set it does not look the PS up and binds no PS.</item>
''' <item>The records: the Utility group's closed lists of the fxp (SseUtilitySource, generated), looked up by descriptor as in
''' SseLightingTechnique. No record -&gt; al = 0 (0x141579214), SetupTechnique jumps out (je 0x141566FF4 at 0x1415667A2): not drawn.</item>
''' </list>
''' Gate: ParityGate sse-technique-law (against sse_law.py: the techniques of the corpus shapes and of the closed domain, the keys, the
''' null PS and the records; directed mutant sse-record-other-group). Its twin for the other game: Fo4UtilityTechnique.</summary>
Friend Module SseUtilityTechnique

    ''' <summary>[0x1420D6840], the shadow filter selector the PS key reads (0x14158EA6F): written by 0x1414EAEBB..EC7 from the Setting
    ''' uShadowFilterType:Display (object 0x1420D6B60), whose .data value is 3 (s1_re.md 4.2, rev-01). The exe default, no INI (user
    ''' decision 3-oct-2026: app values / exe defaults). THE one owner of the filter (SseUtilityConstants reads it).</summary>
    Friend Const ShadowFilterType As Integer = 3

    ''' <summary>[0x1420D6811], .data 1: the VS key's focus-shadow rewrite is on (0x14158E996).</summary>
    Friend Const FocusShadowKeyOn As Boolean = True

    ''' <summary>The mask technique of a frame with focus shadows (0x14153B0C0 = [0x1420D6810] (.data 1) and the focus-shadow count [0x1433D3E90] != 0): the preview's subject
    ''' has one per casting rig light (user decision 8-nov-2026, PreviewModel.FocusShadowsActive).</summary>
    Friend Const PreviewFocusShadows As Boolean = True

    ''' <summary>The accumulator mode a lighting shape's slot 0x2B is called in ([acc+0x150]; the four modes 0x14151B2E8..2EF lets through).</summary>
    Friend Enum SseUtilityMode
        ''' <summary>0xC: the z-prepass (RENDER_DEPTH, bit 13, 0x14151B674).</summary>
        Depth = &HC
        ''' <summary>0xD: bit 14 only (set at 0x141587D97 in 0x141587BC0).</summary>
        ShadowMapD = &HD
        ''' <summary>0xE: bits 14 | 15 (0x14151B67A..688): the directional light's split accumulators (BSShadowDirectionalLight vtable
        ''' slot 16 0x14157F080, 0x141581606) and the focus shadows' (0x14155D1EB).</summary>
        ShadowMapDirectional = &HE
        ''' <summary>0xF: bits 14 | 16 (0x14151B68A..690; set at 0x141588115 / 0x1415882B6 in 0x141587F90).</summary>
        ShadowMapF = &HF
    End Enum

    ''' <summary>DetermineUtilityShaderDecl, BSLightingShaderProperty vfunc +0x1E8 = 0x14151A0B0 (never 0): base 2 with kModelSpaceNormals
    ''' (flag 12) else 0x1A (0x14151A0B4..C5); | 4 SKINNED (flag 1, 0x14151A0C8..D6); | 1 VC (flag 37, 0x14151A0D9..ED); | 0x20 with flag
    ''' 14 or 46 (0x14151A0F1..109); | 0x100 with flag 33 (0x14151A10D..12A); | 0x40 with flag 17 (0x14151A12E..145); | bit 26 with flag
    ''' 61 (0x14151A148..154).</summary>
    Friend Function Decl(s As SseRenderPassLaw.SseShapeInputs) As UInteger
        Dim t As UInteger = If(s.ModelSpaceNormals, 2UI, &H1AUI)
        If s.LitSkinned Then t = t Or 4UI
        If s.VertexColorsFlag Then t = t Or 1UI
        If s.MultiTextureLandscape OrElse s.NoLodLandBlend Then t = t Or &H20UI
        If s.LodLandscape Then t = t Or &H100UI
        If s.Eye Then t = t Or &H40UI
        If s.TreeAnim Then t = t Or (1UI << 26)
        Return t
    End Function

    ''' <summary>Bit 16 of a mode-0xC technique: the stipple fade (r14b, 0x14151B304..344: needs the fade node's currentFade below 1) or the
    ''' landscape fade (bpl, 0x14151B353..375: flags 14 / 46 and [prop+0x108] below 1). The preview frame has neither: no fade node fades
    ''' (currentFade 1, SseRenderPassLaw's frame) and [prop+0x108] keeps the 1.0 of the property's ctor (0x141518E7B, ctor 0x141518DD0 of
    ''' vtable 0x141B3E868); in modes 0xD..0xF both are 0 by the mode test (0x14151B30D, 0x14151B36F).</summary>
    Friend Const PreviewFadeBit16 As Boolean = False

    ''' <summary>The technique slot 0x2B 0x14151B1E0 builds for a lighting shape that passes its filters (SseRenderPassLaw.
    ''' LightingSlot2BPasses) in mode <paramref name="mode"/>, as its first pass (0x14151B62B..6D5; a pass already in the property's list
    ''' is rebuilt from its own technique minus 0x2B AND &amp;HFFFE1FFF with the same bits, 0x14151B553..5FE: the same value):
    ''' Decl | bit 7 with the NiAlphaProperty's test (flags bit 9, 0x14151B641..65A) | the mode's bits (0x14151B66E..690) | bit 16
    ''' (PreviewFadeBit16) | bit 27 with flag 34 or 63 (0x14151B6A2..6C4) | &amp;H20080 for a decal (flags AND &amp;HC000000, 0x14151B6C8..6D2).
    ''' <para>Mode 0xE and a shape WITHOUT Cast_Shadows (SLSF1 bit 9, <paramref name="castShadows"/>): with [0x1420D6D00] = 2 (.data) and
    ''' byte [acc+0x12E] set (<paramref name="acc12E"/>: the directional light's slot 9 0x14157EAD0 sets it on every split accumulator,
    ''' 0x14157EB60; the accumulator's ctor 0x14151DEA0 leaves 0, 0x14151E15D, e.g. the focus shadows' 0x14155D1EB) the slot builds
    ''' instead Decl | &amp;HC000 | bit 7 | bit 27 - no decal bits - into the property's list +0xD8, group 8 (0x14151B42F..52C). Which list
    ''' the directional light draws is the caster law's (SseRenderPassLaw.ShadowMapCasts, F3.2), not this function's.</para></summary>
    Friend Function Slot2BTechnique(s As SseRenderPassLaw.SseShapeInputs, mode As SseUtilityMode, castShadows As Boolean, acc12E As Boolean) As UInteger
        Dim t = Decl(s)
        If s.AlphaPropertyPresent AndAlso s.Test Then t = t Or &H80UI
#If DEBUG Then
        ' ShadowGate --sse-shadow-law [emitter-alpha]: the shadow map's alpha test also from SLSF2 No_Transparency_Multisampling (bit 54), the subgroup's
        ' rule (c11 rev-07) instead of the exe's NiAlphaProperty test alone (0x14151B641..65A).
        If mode <> SseUtilityMode.Depth AndAlso s.NoTransparencyMultisampling AndAlso Fo4GBufferConstants.GateMutantOn("sse-shadow-alpha-ntms") Then t = t Or &H80UI
#End If
        Dim lod = s.LodObjects OrElse s.HdLodObjects
        If mode = SseUtilityMode.ShadowMapDirectional AndAlso Not castShadows AndAlso acc12E Then
            Dim n = t Or &HC000UI
            If lod Then n = n Or (1UI << 27)
            Return n
        End If
        If mode = SseUtilityMode.Depth Then
            t = t Or &H2000UI
        Else
            t = t Or &H4000UI
            If mode = SseUtilityMode.ShadowMapDirectional Then t = t Or &H8000UI
            If mode = SseUtilityMode.ShadowMapF Then t = t Or &H10000UI
        End If
        If mode = SseUtilityMode.Depth AndAlso PreviewFadeBit16 Then t = t Or &H10000UI
        If lod Then t = t Or (1UI << 27)
        If s.Decal OrElse s.DynamicDecal Then t = t Or &H20080UI
        Return t
    End Function

    ''' <summary>The pass ID of a utility technique (0x14151B6D5: add eax, 0x2B).</summary>
    Friend Function PassId(technique As UInteger) As UInteger
        Return technique + &H2BUI
    End Function

    ''' <summary>The technique SetupTechnique 0x141566710 draws pass <paramref name="pass"/> with (0x14156671F: lea esi, [rdx - 0x2B]).</summary>
    Friend Function TechniqueOfPass(pass As UInteger) As UInteger
        Return pass - &H2BUI
    End Function

    ''' <summary>The technique of the screen-space shadow mask pass of the directional light (0x141539C51..C69: ((focus + 0x20) &lt;&lt; 8)
    ''' OR &amp;H200002, + 0x2B; focus = 0x14153B0C0): RENDER_SHADOWMASK | RENDER_DEPTH-bit 13 | TEXTURE, bit 8 with focus shadows.</summary>
    Friend Function MaskTechnique(focus As Boolean) As UInteger
        Return ((If(focus, 1UI, 0UI) + &H20UI) << 8) Or &H200002UI
    End Function

    ''' <summary>The VS key 0x14158E990..EA00: v = t AND &amp;HF7E5FF9F; with [0x1420D6811] (FocusShadowKeyOn), mask bits 21..24 and bit 13:
    ''' v = (v AND &amp;H1E00000) OR &amp;H2002 (0x14158E996..9BA); unless v AND &amp;H14000 = &amp;H14000 (0x14158E9CA..9D0): when (v AND &amp;H20004000) =
    ''' &amp;H4000 or (v AND &amp;H1E02000) = &amp;H2000, without bit 7 (test cl, cl / js) and v AND &amp;H14000 &lt;&gt; &amp;H10000, v AND &amp;HDFFFE1E4
    ''' (0x14158E9D2..9F8).</summary>
    Friend Function VertexShaderKey(technique As UInteger) As UInteger
        Dim v = technique And &HF7E5FF9FUI
        If FocusShadowKeyOn AndAlso (v And &H1E00000UI) <> 0UI AndAlso (v And &H2000UI) <> 0UI Then v = (v And &H1E00000UI) Or &H2002UI
        Dim e = v And &H14000UI
        If e = &H14000UI Then Return v
        If ((v And &H20004000UI) = &H4000UI OrElse (v And &H1E02000UI) = &H2000UI) AndAlso (v And &H80UI) = 0UI AndAlso e <> &H10000UI Then
            v = v And &HDFFFE1E4UI
        End If
#If DEBUG Then
        ' ShadowGate --sse-prepass-law [prepass-equal]: the z-prepass (bit 13 without bit 14) with another VS than its colour pass's: the skin bit dropped.
        If Fo4GBufferConstants.GateMutantOn("sse-prepass-vs-other") AndAlso (technique And &H2000UI) <> 0UI AndAlso (technique And &H4000UI) = 0UI Then v = v And Not 4UI
#End If
        Return v
    End Function

    ''' <summary>THE depth-only condition of SetupTechnique (0x141566735..778) and of the PS key (0x14158EA15..A4A): t AND &amp;H14000 &lt;&gt;
    ''' &amp;H14000, (t AND &amp;H20004000) = &amp;H4000 or (t AND &amp;H1E02000) = &amp;H2000, no bit 7 (test sil, sil / js), t AND &amp;H14000 &lt;&gt; &amp;H10000.
    ''' Then r12b = 0, BeginTechnique gets r9b = 1 (0x141566787) and binds NO pixel shader without looking it up (0x14157914A,
    ''' 0x141579196..199, cmovne 0x141579204): the null PS. Its PS key (&amp;H2000) is never searched.</summary>
    Friend Function PixelShaderNull(technique As UInteger) As Boolean
        Dim e = technique And &H14000UI
        If e = &H14000UI Then Return False
        Return ((technique And &H20004000UI) = &H4000UI OrElse (technique And &H1E02000UI) = &H2000UI) AndAlso
               (technique And &H80UI) = 0UI AndAlso e <> &H10000UI
    End Function

    ''' <summary>The PS key 0x14158EA10..EABA: the depth-only condition (PixelShaderNull) gives &amp;H2000 (0x14158EA4C); else r = t AND
    ''' &amp;HFFFFFB83 (0x14158EA52), and with mask bits 21..24 and bit 13: (r AND &amp;H1E00100) OR &amp;H2002 OR the filter's bit - 1: bit 17, 2: 18,
    ''' 3: 19, 4: 20, else none (0x14158EA59..AB2; the filter [0x1420D6840] = ShadowFilterType in the draw; a gate passes another).</summary>
    Friend Function PixelShaderKey(technique As UInteger, Optional filter As Integer = ShadowFilterType) As UInteger
        If PixelShaderNull(technique) Then Return &H2000UI
#If DEBUG Then
        ' ParityGate sse-utl-law [mask-record]: the mask with the filter 2 (rec 11379, the first round's PCF 3x3) instead of the .data's 3.
        If Fo4GBufferConstants.GateMutantOn("sse-mask-filter-2") Then filter = 2
#End If
        Dim r = technique And &HFFFFFB83UI
        If (r And &H1E00000UI) <> 0UI AndAlso (r And &H2000UI) <> 0UI Then
            r = (r And &H1E00100UI) Or &H2002UI
            Select Case filter
                Case 1 : r = r Or (1UI << 17)
                Case 2 : r = r Or (1UI << 18)
                Case 3 : r = r Or (1UI << 19)
                Case 4 : r = r Or (1UI << 20)
            End Select
        End If
        Return r
    End Function

    ''' <summary>The g7 VS record of key <paramref name="vsKey"/> (the fxp record whose descriptor is the key, BeginTechnique
    ''' 0x141579175); -1 when the cache has none.</summary>
    Friend Function VertexRecord(vsKey As UInteger) As Integer
        Dim v As GlslVertexStage = Nothing
#If DEBUG Then
        ' ParityGate sse-technique-law's directed mutant (Fo4GBufferConstants.GateMutantOn): the record looked up in another group.
        If Fo4GBufferConstants.GateMutantOn("sse-record-other-group") Then Return If(SseLightingSource.VertexStages.TryGetValue(vsKey, v), v.Record, -1)
#End If
        Return If(SseUtilitySource.VertexStages.TryGetValue(vsKey, v), v.Record, -1)
    End Function

    ''' <summary>The g7 PS record of key <paramref name="psKey"/> (BeginTechnique 0x1415791C5); -1 when the cache has none.</summary>
    Friend Function PixelRecord(psKey As UInteger) As Integer
        Dim p As GlslPixelStage = Nothing
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("sse-record-other-group") Then Return If(SseLightingSource.PixelShaders.TryGetValue(psKey, p), p.Record, -1)
#End If
        Return If(SseUtilitySource.PixelShaders.TryGetValue(psKey, p), p.Record, -1)
    End Function

    ''' <summary>The programs of technique <paramref name="technique"/> that are NOT in the cache, named for the frame's notice; Nothing when
    ''' the technique draws: its VS always (0x141579146..192), its PS only when it is not the null PS (PixelShaderNull: not looked up).</summary>
    Friend Function MissingEnginePrograms(technique As UInteger, Optional filter As Integer = ShadowFilterType) As String
        Dim vs = VertexShaderKey(technique), ps = PixelShaderKey(technique, filter)
        Dim vsIn = VertexRecord(vs) >= 0
        Dim psIn = PixelShaderNull(technique) OrElse PixelRecord(ps) >= 0
        If psIn AndAlso vsIn Then Return Nothing
        If Not psIn AndAlso Not vsIn Then Return $"pixel shader 0x{ps:X8} and vertex shader 0x{vs:X8}"
        Return If(psIn, $"vertex shader 0x{vs:X8}", $"pixel shader 0x{ps:X8}")
    End Function

    ''' <summary>THE DEPTH PS A LIGHTING SHAPE'S Z-PREPASS CAN BIND WITH ITS ALPHA TEST (Ola SSE S1 F4, the views' cut): record -&gt; its class, over
    ''' every lighting shape the slot builds a technique for - Slot2BTechnique of mode 0xC on every combination of the inputs it reads (Decl's
    ''' seven flags, the LOD bits, the decal bits; the NiAlphaProperty's test on: the cut exists only with it) - whose PS is not the null PS
    ''' and is in the cache (PixelRecord). Derived from the law, not listed: a change of Slot2BTechnique or PixelShaderKey changes it.</summary>
    Friend ReadOnly Property PrepassCutRecords As IReadOnlyDictionary(Of Integer, String)
        Get
            Return _prepassCutRecords.Value
        End Get
    End Property
    Private ReadOnly _prepassCutRecords As New Lazy(Of IReadOnlyDictionary(Of Integer, String))(AddressOf BuildPrepassCutRecords)

    Private Function BuildPrepassCutRecords() As IReadOnlyDictionary(Of Integer, String)
        Dim r As New SortedDictionary(Of Integer, String)()
        For m = 0 To (1 << 12) - 1
            Dim s As New SseRenderPassLaw.SseShapeInputs With {
                .AlphaPropertyPresent = True, .Test = True,
                .ModelSpaceNormals = (m And 1) <> 0, .LitSkinned = (m And 2) <> 0, .VertexColorsFlag = (m And 4) <> 0,
                .MultiTextureLandscape = (m And 8) <> 0, .NoLodLandBlend = (m And 16) <> 0, .LodLandscape = (m And 32) <> 0,
                .Eye = (m And 64) <> 0, .TreeAnim = (m And 128) <> 0, .LodObjects = (m And 256) <> 0, .HdLodObjects = (m And 512) <> 0,
                .Decal = (m And 1024) <> 0, .DynamicDecal = (m And 2048) <> 0}
            Dim t = Slot2BTechnique(s, SseUtilityMode.Depth, castShadows:=True, acc12E:=False)
            If PixelShaderNull(t) Then Continue For
            Dim p As GlslPixelStage = Nothing
            If Not SseUtilitySource.PixelShaders.TryGetValue(PixelShaderKey(t), p) Then Continue For
            r(p.Record) = p.FnKey
        Next
        Return r
    End Function

    ''' <summary>The record of the depth PS the z-prepass of a lighting shape binds (Slot2BTechnique mode 0xC, PixelShaderKey): 0 = the null PS (no
    ''' alpha test: the prepass keeps every fragment), -1 = no record in the cache (the prepass draws nothing: DrawSseEnginePrepass's
    ''' notice; the views keep nothing of it either).</summary>
    Friend Function PrepassCutRecord(s As SseRenderPassLaw.SseShapeInputs) As Integer
        Dim t = Slot2BTechnique(s, SseUtilityMode.Depth, castShadows:=True, acc12E:=False)
        If PixelShaderNull(t) Then Return 0
        Dim rec = PixelRecord(PixelShaderKey(t))
        Return If(rec >= 0, rec, -1)
    End Function

    ''' <summary>THE Z-PREPASS'S ALPHA TEST OF A LIGHTING SHAPE as GLSL functions (Fallout 4's Fo4GBufferSource.PrepassAlphaKill): each class of
    ''' PrepassCutRecords, its transcribed function without its declarations and its engine_main. Reader: Fragment_SSE's debug and edit views,
    ''' which declare sutl_TEXCOORD2 / sutl_TEXCOORD4, AlphaTestRef, cb11_AlphaTestRef and t0 and include D3d11SemanticsSource.Glsl and
    ''' SseUtilitySource.Literals: the views cut with the prepass's very text, not a copy of it.</summary>
    Friend Function PrepassAlphaKill() As String
        Dim sb As New System.Text.StringBuilder()
        For Each fnKey In PrepassCutRecords.Values.Distinct().OrderBy(Function(k) k, StringComparer.Ordinal)
            Dim t = SseUtilitySource.ProgramText(fnKey)
            Dim i = t.IndexOf("void " & fnKey & "(", StringComparison.Ordinal)
            Dim j = t.IndexOf("void engine_main(", StringComparison.Ordinal)
            If i < 0 OrElse j < i Then Throw New InvalidOperationException($"SseUtilityTechnique.PrepassAlphaKill: the function of {fnKey} was not found")
            sb.Append(t, i, j - i)
        Next
        Return sb.ToString()
    End Function

    ''' <summary>The GLSL that runs the depth PS of record <paramref name="recordVar"/> (an int uniform: PrepassCutRecord) on the declared inputs:
    ''' its function writes o and k (the kill); -1 kills every fragment (no record: the prepass draws nothing). PrepassAlphaKill's reader.</summary>
    Friend Function PrepassAlphaKillDispatch(recordVar As String) As String
        Dim sb As New System.Text.StringBuilder()
        sb.Append(vbTab & "switch (" & recordVar & ")" & vbLf & vbTab & "{" & vbLf)
        For Each kv In PrepassCutRecords
            sb.Append(vbTab & "case " & kv.Key.ToString(Globalization.CultureInfo.InvariantCulture) & ": " & kv.Value & "(o, k); break;" & vbLf)
        Next
        sb.Append(vbTab & "case -1: k = true; break;" & vbLf & vbTab & "}" & vbLf)
        Return sb.ToString()
    End Function
End Module
