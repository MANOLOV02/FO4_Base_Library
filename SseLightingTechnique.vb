Option Strict On
Option Explicit On

''' <summary>THE SKYRIM SE LIGHTING TECHNIQUE OF A SHAPE AND THE g6 RECORDS IT SELECTS (SkyrimSE.exe 1.7.104; Ola SSE S1 F2.1, s1_re.md 1).
''' <list type="bullet">
''' <item>The technique: the pass ID minus 0x4800002D (0x141547D2C) = SseRenderPassLaw.LitDescriptor (0x14151A932..AE58) with the frame's
''' shadow state (SseRenderPassLaw.SseLitShadowState: bits 13 / 14, 0x14151AA11..A27), rewritten by SetupTechnique 0x141547D37..D71
''' (SseRenderPassLaw.LitSetupTechnique).</item>
''' <item>The keys: VS 0x1415896A0 / PS 0x1415896D0 (SseRenderPassLaw.LitVsKey / LitPsKey, called at 0x141547D81 / 0x141547D8A).</item>
''' <item>The records: BeginTechnique 0x141579120 (called at 0x141547D9A with r9b = 0, 0x141547D8F) hashes the VS key in [shader+0x50]
''' (0x141579146..192) and the PS key in [shader+0x80] (0x14157919B..1E0) and takes the entry whose dword [prog] equals the key
''' (0x141579175 / 0x1415791C5): [prog+0] is the descriptor the creator copies from the fxp record (0x14100FC10 / 0x14100FFE0). So a
''' record is the one of the Lighting group whose descriptor is the key: the closed lists of the fxp, SseLightingSource.VertexStages /
''' PixelShaders (generated from it, never written by hand). Both found -&gt; VSSetShader / PSSetShader (0x1415791F5 / 0x14157920B).</item>
''' <item>No record: BeginTechnique returns al = 0 (0x141579214) and SetupTechnique jumps to its end, je 0x1415487AE at 0x141547DA4:
''' the pass is NOT drawn (D-7: not drawn + notice, MissingEnginePrograms names what is missing).</item>
''' </list>
''' Gate: ParityGate sse-technique-law (the technique, the keys and the records against Tools\ShaderTranscription\sse-common\sse_law.py
''' over the SSE corpus and the closed domain; the directed mutant sse-record-other-group). Its twin for the other game: Fo4GBufferTechnique.</summary>
Friend Module SseLightingTechnique

    ''' <summary>The technique SetupTechnique 0x141547D20 draws a lighting shape with, in the frame's shadow state
    ''' <paramref name="shadow"/>.</summary>
    Friend Function Technique(s As SseRenderPassLaw.SseShapeInputs, shadow As SseRenderPassLaw.SseLitShadowState) As UInteger
        Return SseRenderPassLaw.LitSetupTechnique(SseRenderPassLaw.LitDescriptor(s, shadow))
    End Function

    ''' <summary>The VS key of technique <paramref name="technique"/> (0x1415896A0).</summary>
    Friend Function VertexShaderKey(technique As UInteger) As UInteger
        Return SseRenderPassLaw.LitVsKey(technique)
    End Function

    ''' <summary>The PS key of technique <paramref name="technique"/> (0x1415896D0).</summary>
    Friend Function PixelShaderKey(technique As UInteger) As UInteger
        Return SseRenderPassLaw.LitPsKey(technique)
    End Function

    ''' <summary>The g6 VS record of key <paramref name="vsKey"/> (the fxp record whose descriptor is the key, BeginTechnique
    ''' 0x141579175); -1 when the shader cache has none.</summary>
    Friend Function VertexRecord(vsKey As UInteger) As Integer
        Dim v As GlslVertexStage = Nothing
#If DEBUG Then
        ' ParityGate sse-technique-law's directed mutant (Fo4GBufferConstants.GateMutantOn): the record looked up in another group.
        If Fo4GBufferConstants.GateMutantOn("sse-record-other-group") Then Return If(SseUtilitySource.VertexStages.TryGetValue(vsKey, v), v.Record, -1)
#End If
        Return If(SseLightingSource.VertexStages.TryGetValue(vsKey, v), v.Record, -1)
    End Function

    ''' <summary>The g6 PS record of key <paramref name="psKey"/> (BeginTechnique 0x1415791C5); -1 when the shader cache has none. The
    ''' record is the engine's whatever the stage's .Gap text holds (F1 transcribed every g6 / g7 stage: the 7051 stages of
    ''' SseLightingSource have an empty .Gap text).</summary>
    Friend Function PixelRecord(psKey As UInteger) As Integer
        Dim p As GlslPixelStage = Nothing
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("sse-record-other-group") Then Return If(SseUtilitySource.PixelShaders.TryGetValue(psKey, p), p.Record, -1)
#End If
        Return If(SseLightingSource.PixelShaders.TryGetValue(psKey, p), p.Record, -1)
    End Function

    ''' <summary>The programs of technique <paramref name="technique"/> that are NOT in the shader cache, named for the frame's notice
    ''' ("pixel shader 0x..." and / or "vertex shader 0x...", the keys); Nothing when both are there (BeginTechnique 0x141579120 needs
    ''' both, 0x1415791E2..1E9; SetupTechnique skips the pass otherwise, 0x141547DA4).</summary>
    Friend Function MissingEnginePrograms(technique As UInteger) As String
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
End Module
