Option Strict On
Option Explicit On

''' <summary>THE SKYRIM SE EFFECT TECHNIQUE OF A SHAPE AND THE g5 RECORDS IT SELECTS (SkyrimSE.exe 1.7.104; Ola SSE S2 F2.1, s2_re.md 1;
''' the twin of SseLightingTechnique).
''' <list type="bullet">
''' <item>The technique: BSEffectShaderProperty's 0x14152A2B0 whole - the mesh bits, the particle and strip particle bits (0x84, bit 12,
''' 0x2013) and the rest (<see cref="Technique"/>).</item>
''' <item>The keys: VS 0x14158DAB0 (t AND &amp;HFFC7FFFF) / PS 0x14158DAC0 (bit 3 dropped unless bit 15, then bit 24 cleared), called by
''' SetupTechnique 0x141556840 (s2_re.md 1; Tools\ShaderTranscription\common\build_block.py 'sse-eff').</item>
''' <item>The records: BeginTechnique 0x141579120 takes the entry whose dword [prog] is the key (0x141579175 / 0x1415791C5): the record of
''' the Effect group whose descriptor is the key (SseEffectSource.VertexStages / PixelShaders, generated from the fxp).</item>
''' <item>No record: BeginTechnique returns 0 (0x141579214) and SetupTechnique jumps to its end (0x141556877 -&gt; 0x141556C22): the pass
''' is NOT drawn (D-7: not drawn + notice, MissingEnginePrograms names what is missing). The one owner of "this effect has no program
''' in Skyrim SE" (rev-15).</item>
''' </list>
''' Gate: ParityGate sse-effect-technique-law (the technique, the keys and the records against
''' Tools\ShaderTranscription\sse-common\sse_law.py over the corpus - mesh and particle shapes - and the closed domain; its directed
''' mutants below).</summary>
Friend Module SseEffectTechnique

    ''' <summary>The technique 0x14152A2B0 of an effect shape, in the order its ORs build it (0x14152A500..5FB).</summary>
    Friend Function Technique(s As SseRenderPassLaw.SseShapeInputs) As UInteger
        Dim particles = s.EffectGeometry = SseRenderPassLaw.SseEffectGeometry.Particles      ' r13b: vfunc 0x80 non-null, RTTI != strip (0x14152A329..350)
        Dim strip = s.EffectGeometry = SseRenderPassLaw.SseEffectGeometry.StripParticles     ' RTTI == BSStripParticleSystem (0x14152A355..36F)
        Dim t As UInteger = If(s.VertexColorsFlag, 1UI, 0UI)                       ' bit 37, 0x14152A2CF..2E2, 0x14152A50D..510
        If s.EffectBaseTexture Then t = t Or &H42UI                                 ' 0x140EB12C0(material + 0x70), 0x14152A2E5, 0x14152A514..518
        ' 0x84: the sub-texture byte dl (vfunc 0x80 non-null and [[geom + 0x158] + 0x68] > 0, 0x14152A492..4B7) kept only with the
        ' base texture (cmove ecx, edi, 0x14152A500..50A), 0x14152A51B..524
        Dim subTex = s.ParticleSubTexture AndAlso s.EffectBaseTexture
#If DEBUG Then
        ' ParityGate sse-effect-technique-law: particles without 0x84.
        If Fo4GBufferConstants.GateMutantOn("sse-eff-no-subtex") Then subTex = False
#End If
        If subTex Then t = t Or &H84UI
        If s.Skinned Then t = t Or 8UI                                              ' 0x14152A2F2..312, 0x14152A527..534
        If s.Falloff AndAlso Not particles Then t = t Or &H110UI                    ' bit 6 without particles (r15d, 0x14152A481..48E), 0x14152A537..541
        If particles Then t = t Or &H1000UI                                         ' bts 0xC, 0x14152A544..54D
        If strip Then t = t Or &H2013UI                                             ' 0x14152A550..55F
        If s.AlphaPropertyPresent Then                                              ' 0x14152A4B9..4FD
            If s.AlphaDst = 0 Then t = t Or &H400UI                                 ' (ap &amp; 0x1E0) = 0, 0x14152A562..56B
            Dim multDecal = s.AlphaSrc = 4 AndAlso s.AlphaDst = 7                   ' (ap &amp; 0x1E) = 8, (ap &amp; 0x1E0) = 0xE0, 0x14152A4CE..4E5
            Dim mult = Not multDecal AndAlso s.AlphaDst = 2                         ' (ap &amp; 0x1E0) = 0x40, 0x14152A4EA..4F5
#If DEBUG Then
            ' ParityGate sse-effect-technique-law: MULTBLEND and MULTBLEND_DECAL swapped.
            If Fo4GBufferConstants.GateMutantOn("sse-eff-multblend-swap") Then
                Dim m0 = mult : mult = multDecal : multDecal = m0
            End If
#End If
            If mult Then t = t Or &H800UI                                           ' 0x14152A56E..577
            If multDecal Then t = t Or &H400000UI                                   ' 0x14152A57A..583
        End If
        If s.WeaponBlood Then t = t Or &H4000UI                                     ' bit 49, 0x14152A3A3..3B6, 0x14152A586..592
        If s.EffectLighting AndAlso SseRenderPassLaw.EffectLightingOn Then t = t Or &H10000UI   ' bit 62 and [0x1420D6D70], 0x14152A377..399, 0x14152A595..5A3
        If s.ProjectedUV Then t = t Or &H20012UI                                    ' bit 23, 0x14152A3BB..3C4, 0x14152A5A6..5B3
        If s.SoftEffect Then t = t Or &H40000UI                                     ' bit 30, 0x14152A3C9..3D2, 0x14152A5B6..5C2
        If s.PaletteColor Then t = t Or &H80000UI                                   ' bit 4 + texture, 0x14152A3D7..3F6, 0x14152A5C5..5D3
        If s.PaletteAlpha Then t = t Or &H100000UI                                  ' bit 5 + texture, 0x14152A3FE..41C, 0x14152A5D6..5E1
        If s.Billboard Then t = t Or &H1000000UI                                    ' bit 45, 0x14152A421..432, 0x14152A5E4..5ED
        Dim b26 = SseRenderPassLaw.EffectTechniqueBit26(s)
#If DEBUG Then
        ' ParityGate sse-effect-technique-law: bit 26 without the NORMAL test of the vertex desc.
        If Fo4GBufferConstants.GateMutantOn("sse-eff-bit26-nonormal") Then b26 = (Not s.AlphaPropertyPresent OrElse s.Test) AndAlso s.ZTest AndAlso s.ZWrite
#End If
        If b26 Then t = t Or &H4000000UI                                            ' 0x14152A43A..47C, 0x14152A5F0..5FB
        Return t
    End Function

    ''' <summary>The VS key of technique <paramref name="technique"/> (0x14158DAB0): t AND &amp;HFFC7FFFF.</summary>
    Friend Function VertexShaderKey(technique As UInteger) As UInteger
        Return technique And &HFFC7FFFFUI
    End Function

    ''' <summary>The PS key of technique <paramref name="technique"/> (0x14158DAC0): bit 3 dropped unless bit 15 (cmovne), then bit 24
    ''' cleared (btr eax, 0x18).</summary>
    Friend Function PixelShaderKey(technique As UInteger) As UInteger
        Dim keep3 = (technique And &H8000UI) <> 0UI
#If DEBUG Then
        ' ParityGate sse-effect-technique-law: the key without the cmovne of bit 3 (bit 3 kept always) / without the btr of bit 24.
        If Fo4GBufferConstants.GateMutantOn("sse-eff-pskey-bit3") Then keep3 = True
#End If
        Dim k = If(keep3, technique, technique And &HFFFFFFF7UI)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("sse-eff-pskey-bit24") Then Return k
#End If
        Return k And Not &H1000000UI
    End Function

    ''' <summary>The g5 VS record of key <paramref name="vsKey"/> (the fxp record whose descriptor is the key, BeginTechnique
    ''' 0x141579175); -1 when the shader cache has none.</summary>
    Friend Function VertexRecord(vsKey As UInteger) As Integer
        Dim v As GlslVertexStage = Nothing
        Return If(SseEffectSource.VertexStages.TryGetValue(vsKey, v), v.Record, -1)
    End Function

    ''' <summary>The g5 PS record of key <paramref name="psKey"/> (BeginTechnique 0x1415791C5); -1 when the shader cache has none.</summary>
    Friend Function PixelRecord(psKey As UInteger) As Integer
        Dim p As GlslPixelStage = Nothing
        Return If(SseEffectSource.PixelShaders.TryGetValue(psKey, p), p.Record, -1)
    End Function

    ''' <summary>The programs of technique <paramref name="technique"/> that are NOT in the shader cache, named for the frame's notice
    ''' ("pixel shader 0x..." and / or "vertex shader 0x...", the keys); Nothing when both are there (BeginTechnique 0x141579120 needs
    ''' both, 0x1415791E2..1E9; SetupTechnique skips the pass otherwise, 0x141556877 -&gt; 0x141556C22).</summary>
    Friend Function MissingEnginePrograms(technique As UInteger) As String
#If DEBUG Then
        ' ParityGate effect-draw-skip / sse-technique-law: the hand rule S2 retired (RenderableMesh.SseEffectDrawSkipped: SOFT and MULTBLEND_DECAL)
        ' as the only source - the corpus's 15 shapes without a record drawn.
        If Fo4GBufferConstants.GateMutantOn("sse-eff-skip-old-rule") Then
            Return If((technique And &H40000UI) <> 0UI AndAlso (technique And &H400000UI) <> 0UI, "the old rule's soft multiplicative decal", Nothing)
        End If
#End If
        Dim vs = VertexShaderKey(technique), ps = PixelShaderKey(technique)
        Dim vsIn = VertexRecord(vs) >= 0, psIn = PixelRecord(ps) >= 0
        If psIn AndAlso vsIn Then Return Nothing
        If Not psIn AndAlso Not vsIn Then Return $"pixel shader 0x{ps:X8} and vertex shader 0x{vs:X8}"
        Return If(psIn, $"vertex shader 0x{vs:X8}", $"pixel shader 0x{ps:X8}")
    End Function

    ''' <summary>The engine draws technique <paramref name="technique"/>: both records are in the cache (MissingEnginePrograms).</summary>
    Friend Function HasEnginePrograms(technique As UInteger) As Boolean
        Return MissingEnginePrograms(technique) Is Nothing
    End Function

    ''' <summary>DEBUG / GATE (ShadowGate --sse-coplanar, rev-16: the perturbations of the effect's position path, each of which must lose samples in
    ''' aetheriumshard03 / azurasstarshadertest02): the text of an effect VS (SseEffectSource.VertexOf) under the mutants
    ''' sse-eff-vs-dp4-order (each row of the position, cb12[8..11], summed a + (b + c) instead of the asm's (a + b) + c: the rigid VS's
    ''' mul / add / mul / add chain from cb12[8 + r].y (the add's operands in either order), the skinned VS's dp4 dot(v, cb12[8 + r])
    ''' written out as x + (y + (z + w)): the operands' association changed. Not row 8 alone, as rev-16 wrote it: the preview's orbit camera
    ''' has no roll, so cb12[8] = the projection's x scale times the camera's right vector, which is horizontal - cb12[8].z = 0 and the
    ''' chain's third term is 0 at every camera; measured 11-nov-2026 on the 22 pairs at the 201 cameras of [coplanar-clip-bits]: the row-8
    ''' sums in Single the same both ways in every component, so a row-8 change alone is equivalent in the app) and sse-eff-vs-noprecise
    ''' (the VS without precise and invariant). The text unchanged otherwise; GateVertexTextMutated counts the texts a mutant changed.</summary>
    Private Const GateDp4Pattern As String = "(r\d+p)\.xyzw = (r\d+p)\.xyzw \+ \1\.xyzw;[^\n]*\n(\s*)\2\.xyzw = ([^;]+);[^\n]*\n\s*\1\.xyzw = (?:\1\.xyzw \+ \2\.xyzw|\2\.xyzw \+ \1\.xyzw);"
    Private Const GateDp4DotPattern As String = "dot\((r\d+p)\.xyzw, (scb12_viewproj\[[0-3]\])\.xyzw\)"

#If DEBUG Then
    ''' <summary>DEBUG / GATE: how many vertex texts GateVertexTextMutants changed in this process.</summary>
    Friend GateVertexTextMutated As Integer = 0
#End If

    Friend Function GateVertexTextMutants(text As String) As String
#If DEBUG Then
        Dim antes = text
        If Fo4GBufferConstants.GateMutantOn("sse-eff-vs-dp4-order") Then
            Dim rx As New System.Text.RegularExpressions.Regex(GateDp4Pattern)
            For fila = 0 To 3
                Dim i = text.IndexOf($"scb12_viewproj[{fila}].yyyy * World[1].xyzw;", StringComparison.Ordinal)
                If i < 0 Then Continue For
                Dim m = rx.Match(text, i)
                If Not m.Success Then Continue For
                Dim nuevo = $"{m.Groups(2).Value}.xyzw = {m.Groups(2).Value}.xyzw + ({m.Groups(4).Value});" & vbLf & m.Groups(3).Value & $"{m.Groups(1).Value}.xyzw = {m.Groups(1).Value}.xyzw + {m.Groups(2).Value}.xyzw;"
                text = text.Substring(0, m.Index) & nuevo & text.Substring(m.Index + m.Length)
            Next
            text = System.Text.RegularExpressions.Regex.Replace(text, GateDp4DotPattern,
                Function(m)
                    Dim v = m.Groups(1).Value, c = m.Groups(2).Value
                    Return $"({v}.x * {c}.x + ({v}.y * {c}.y + ({v}.z * {c}.z + {v}.w * {c}.w)))"
                End Function)
        End If
        If Fo4GBufferConstants.GateMutantOn("sse-eff-vs-noprecise") Then text = text.Replace("precise ", "").Replace("invariant gl_Position;", "")
        If Not ReferenceEquals(antes, text) AndAlso antes <> text Then GateVertexTextMutated += 1
#End If
        Return text
    End Function
End Module
