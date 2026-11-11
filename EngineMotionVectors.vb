''' <summary>Ola SSE S1 F1.b (user decision 2-nov-2026): THE ENGINE'S MOTION-VECTOR CODE in the transcribed programs. What the engine
''' computes and the app does not consume - the PS motion vectors (Fallout 4 G-buffer o5, Skyrim SE Lighting o1), the vertex-stage
''' outputs no consumer reads, the previous bone palette cb9 - is transcribed whole as GLSL under #ifdef ENGINE_MOTION_VECTORS
''' (Tools\ShaderTranscription\fo4-gbuffer\asm2glsl.py _transcribe_body): the app never defines the symbol, so the program it compiles is
''' the one without that code; ShadowGate --engine-motion-vectors compiles every class that has it with and without. This module is the
''' symbol's one name, the wrapper of the resource blocks only that code declares (the generators' gen_fo4_sources.vb_resources) and
''' the rule of its regions (MotionLines) for the gates that read a stage text.</summary>
Friend Module EngineMotionVectors
    ''' <summary>The preprocessor symbol of the motion-vector code (asm2glsl.MOTION_SYMBOL).</summary>
    Friend Const Symbol As String = "ENGINE_MOTION_VECTORS"

    ''' <summary>A declaration text only the motion code reads (a resource block), inside #ifdef ENGINE_MOTION_VECTORS / #endif;
    ''' "" for "".</summary>
    Friend Function Only(text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""
        Return "#ifdef " & Symbol & vbLf & text & "#endif" & vbLf
    End Function

    ''' <summary>For each line, whether the preprocessor leaves it out WITHOUT the symbol: the "#ifdef ENGINE_MOTION_VECTORS" and its
    ''' "#endif", and every line between them (a conditional inside one goes with it; every other conditional and its lines are kept).
    ''' The one rule of the motion regions for the gates that read a stage text (Without; ShadowGate Fo4GBufferGate.MeasuredCaptures).</summary>
    Friend Function MotionLines(lines As IList(Of String)) As Boolean()
        Dim fuera(lines.Count - 1) As Boolean
        Dim pila As New List(Of Boolean)
        For i = 0 To lines.Count - 1
            Dim t = lines(i).Trim()
            If t.StartsWith("#if", StringComparison.Ordinal) Then
                pila.Add(t = "#ifdef " & Symbol)
                fuera(i) = pila.Contains(True)
            ElseIf t = "#endif" AndAlso pila.Count > 0 Then
                fuera(i) = pila.Contains(True)
                pila.RemoveAt(pila.Count - 1)
            Else
                fuera(i) = pila.Contains(True)
            End If
        Next
        Return fuera
    End Function

    ''' <summary>The text the preprocessor gives WITHOUT the symbol (MotionLines): the program the app compiles, for the gates that
    ''' read what a stage text computes (ShadowGate Fo4VertexConstantsGate.TextLive).</summary>
    Friend Function Without(text As String) As String
        Dim lineas = text.Split(CChar(vbLf))
        Dim fuera = MotionLines(lineas)
        Dim sb As New Text.StringBuilder(text.Length)
        For i = 0 To lineas.Length - 1
            If Not fuera(i) Then sb.Append(lineas(i)).Append(vbLf)
        Next
        Dim r = sb.ToString()
        Return If(text.EndsWith(vbLf, StringComparison.Ordinal), r, r.Substring(0, Math.Max(0, r.Length - 1)))
    End Function
End Module
