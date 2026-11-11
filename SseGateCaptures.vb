Option Strict On
Option Explicit On

Imports OpenTK.Mathematics

#If DEBUG Then
''' <summary>DEBUG ONLY (Ola SSE S1 F5, ShadowGate --sse-rig-passes / --sse-character-light / --sse-shadow-law / --sse-eye-fallback): one pass of a
''' Skyrim SE colour draw (PreviewModel.RenderableMesh.DrawSseEngineLit) as the GL holds it right before its draw call - read back from the GL, not
''' from the code that set it: the program and its technique, the rig light of the pass (P1), the colour masks of attachments 0..4, the blend of
''' attachment 0, the depth state, the texture at unit 14 (t14) and the mask's four channel views of that frame, and the values of the named
''' uniforms the program received (Nothing = not an active uniform of the program).</summary>
Friend NotInheritable Class GateSseLitPass
    Friend Shape As String
    Friend Blended As Boolean
    ''' <summary>The key's depth write of this half (DrawSseEngineLit: the fills test EQUAL after a key that wrote its depth).</summary>
    Friend KeyWritesDepth As Boolean
    Friend Pass As Integer
    Friend Technique As UInteger
    Friend Program As Integer
    Friend ReadOnly ColorMask As Boolean()() = {New Boolean(3) {}, New Boolean(3) {}, New Boolean(3) {}, New Boolean(3) {}, New Boolean(3) {}}
    Friend BlendOn As Boolean
    Friend SrcRgb, DstRgb, SrcAlpha, DstAlpha As Integer
    Friend DepthTest As Boolean
    Friend DepthFunc As Integer
    Friend DepthMask As Boolean
    Friend Unit14 As Integer
    Friend ReadOnly MaskViews As Integer() = New Integer(3) {}
    Friend ReadOnly Uniforms As New Dictionary(Of String, Single())(StringComparer.Ordinal)
    ''' <summary>The bool uniform uAppEyeWithoutData of the eye's VS foot (Q-ojo (b)); Nothing = not in the program.</summary>
    Friend AppEyeWithoutData As Integer?
    ''' <summary>The names read back per pass (vec4 each; DirectionalAmbient by row).</summary>
    Friend Shared ReadOnly Names As String() = {"CharacterLightParams", "DirectionalAmbient[0]", "DirectionalAmbient[1]", "DirectionalAmbient[2]", "EmitColor",
                                                 "IBLParams", "DirLightColor", "DirLightDirection", "scb12_42", "ColourOutputClamp", "FogColor", "scb12_eye", "SSRParams",
                                                 "NumLightNumShadowLight", "ShadowLightMaskSelect"}
End Class

''' <summary>DEBUG ONLY (ShadowGate --sun-box-law [receiver]): one Fallout 4 sun pass that casts (Fo4DeferredTargets.RunLights) as the GL holds it
''' right before its draw - read back from the GL: ShadowMapProj[0..5], ShadowWorldScale[0..1], FadeDistances and LightVector of the program in
''' use, and the layers of the texture view bound at unit 5 (GL_TEXTURE_VIEW_NUM_LAYERS).</summary>
''' <summary>ShadowGate --ray-law: a Fallout 4 light's diffuse, per pixel, with its shadow (sun and focus) and without any.</summary>
Friend NotInheritable Class GateLightFactor
    Friend WithShadow As Single()
    Friend Without As Single()
End Class

Friend NotInheritable Class GateFo4SunLight
    Friend LightIndex As Integer
    Friend ShadowLayer As Integer
    Friend ReadOnly Proj As Single()() = New Single(5)() {}
    Friend ReadOnly WorldScale As Single()() = New Single(1)() {}
    Friend Fade As Single()
    Friend LightVector As Single()
    Friend ViewLayers As Integer

    Friend Shared Function Capture(program As Integer, lightIndex As Integer, layer As Integer, view As Integer) As GateFo4SunLight
        Dim r As New GateFo4SunLight With {.LightIndex = lightIndex, .ShadowLayer = layer}
        Dim leer = Function(name As String) As Single()
                       Dim loc = OpenTK.Graphics.OpenGL4.GL.GetUniformLocation(program, name)
                       If loc < 0 Then Return Nothing
                       Dim v(3) As Single
                       OpenTK.Graphics.OpenGL4.GL.GetUniform(program, loc, v)
                       Return v
                   End Function
        For i = 0 To 5 : r.Proj(i) = leer($"ShadowMapProj[{i}]") : Next
        For i = 0 To 1 : r.WorldScale(i) = leer($"ShadowWorldScale[{i}]") : Next
        r.Fade = leer("FadeDistances")
        r.LightVector = leer("LightVector")
        ' GL 4.3 core (Gl43): the view bound to its target, glGetTexParameteriv(GL_TEXTURE_2D_ARRAY, GL_TEXTURE_VIEW_NUM_LAYERS).
        Dim n, prev As Integer
        OpenTK.Graphics.OpenGL4.GL.GetInteger(OpenTK.Graphics.OpenGL4.GetPName.TextureBinding2DArray, prev)
        OpenTK.Graphics.OpenGL4.GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2DArray, view)
        OpenTK.Graphics.OpenGL4.GL.GetTexParameter(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2DArray, CType(&H82DE, OpenTK.Graphics.OpenGL4.GetTextureParameter), n)
        OpenTK.Graphics.OpenGL4.GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2DArray, prev)
        r.ViewLayers = n
        Return r
    End Function
End Class

''' <summary>DEBUG ONLY (Ola SSE S1 F5, ShadowGate --sse-shadow-law): one caster draw of Skyrim SE's emitter (PreviewModel.RenderDepthIntoSse) - the
''' box the casters' frustum test used, and what RenderableMesh.RenderShadowMapSse drew it with (the technique, the program's scb12_eye and World read back
''' after its constants were set; Nothing = the shape drew nothing).</summary>
Friend NotInheritable Class GateSseCasterDraw
    Friend Mesh As Object
    Friend Shape As String
    Friend BoxMin, BoxMax As Vector3
    Friend Technique As UInteger?
    Friend Eye As Single()
    ''' <summary>The program's World[0..3] (a rigid caster's VS: the eye enters as World's translation minus it, SseUtilityConstants.UtlWorld);
    ''' Nothing = not an active uniform.</summary>
    Friend World As Single()()
End Class
#End If

''' <summary>THE GATES' EXACT ARITHMETIC (ParityGate sse-editor-box in Debug and Release, ShadowGate --sse-bone-bounds; no caller in the app): a finite Double as an exact integer, x * 2^1100
''' (every finite Double is m * 2^e with e &gt;= -1074), so sums and products of Doubles compare without rounding.</summary>
Friend Module GateExact
    Friend Function ExactDyadic(x As Double) As Numerics.BigInteger
        Dim b = BitConverter.DoubleToInt64Bits(x)
        Dim ex = CInt((b >> 52) And &H7FFL), m = b And &HFFFFFFFFFFFFFL
        If ex = 0 Then ex = 1 Else m = m Or &H10000000000000L
        Dim r = Numerics.BigInteger.Multiply(New Numerics.BigInteger(m), Numerics.BigInteger.Pow(2, ex - 1075 + 1100))
        Return If(b < 0, -r, r)
    End Function
End Module
