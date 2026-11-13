''' <summary>THE APP'S EDIT-VIEW GLSL shared by the vertex stages that draw the weight view (Vertex_SSE, the foot of the transcribed
''' engine VS, Fo4GBufferSource.AppVertexFoot, and of the effects', Fo4EffectSource): ONE text (ola 2, propuesta H6). ASCII, no
''' double quotes.</summary>
Friend Module AppEditViewsSource
    ''' <summary>The bone-weight colour ramp of the weight view (moved here from Vertex_FO4 / Vertex_SSE, same text).</summary>
    Friend Const ColorRamp As String = "vec3 colorRamp(in float value)
{
	float r;
	float g;
	float b;

	if (value <= 0.0f)
	{
		r = g = b = 1.0;
	}
	else if (value <= 0.25)
	{
		r = 0.0;
		b = 1.0;
		g = value / 0.25;
	}
	else if (value <= 0.5)
	{
		r = 0.0;
		g = 1.0;
		b = 1.0 + (-1.0) * (value - 0.25) / 0.25;
	}
	else if (value <= 0.75)
	{
		r = (value - 0.5) / 0.25;
		g = 1.0;
		b = 0.0;
	}
	else
	{
		r = 1.0;
		g = 1.0 + (-1.0) * (value - 0.75) / 0.25;
		b = 0.0;
	}

	return vec3(r, g, b);
}
"

    ''' <summary>THE MASK VIEW'S FACTOR of a vertex of mask <c>m</c> (the app's edit view, no engine counterpart): ONE text and ONE law for
    ''' the six vertex stages that draw the view (Ola SSE S2 rev-06: Vertex_Fo4EditView, Vertex_SSE and the feet of Fallout 4's G-buffer and
    ''' effect VS and of Skyrim SE's lighting and effect VS), each calling it with its mask and the shape's zap switch (bApplyZap,
    ''' IRenderableShape.ApplyZaps). A mask m &gt;= 0 gives 1 - m / 1.5. A negative mask is the zap mark (MorphEngine.ApplyMorphPlan writes
    ''' -t): with the zap on, 1 - |m| / 1.5 (the vertex is not drawn anyway: the element buffer and the PS kill drop it); with the zap off the
    ''' vertex shows as one with no mask, 1.0 (user decision U12, 12-nov-2026, "neutros en todo"; before it Fallout 4's feet and the app's VS
    ''' gave 1 + |m| / 1.5 and Skyrim SE's engine feet 1 - |m| / 1.5). ShadowGate --mask-factor runs every foot.</summary>
    Friend Const MaskFactor As String = "float appMaskFactor(in float m, in bool zap)
{
	return m < 0.0 ? (zap ? 1.0 - (-m) / 1.5 : 1.0) : 1.0 - m / 1.5;
}
"

    ' ---- THE EDIT FOOT OF AN EFFECT PS (Ola SSE S2 F3.3, rev-21): one text for the app entry of Fallout 4's effect PS
    ' (Fo4EffectSource.AppMain, ola 3) and of Skyrim SE's (SseEffectSource.AppMain). Each entry writes its own comment and the line of
    ' its flat view's colour law (the one game difference: Fallout 4 exp2(2.2 log2(e)), Skyrim SE the raw colour) between
    ' EffectFlatViewOpen and EffectEditFootClose; Fallout 4's program texts stay byte for byte what they were before S2 (ParityGate
    ' fo4-effect-foot-text). ----

    ''' <summary>The inputs and uniforms of an effect PS's edit foot: the VS foot's mask factor, weight colour and zapped flag, and the
    ''' shape's edit switches and colours (RenderableMesh: ApplyFo4EffectDraw / DrawSseEngineEffect set them).</summary>
    Friend Const EffectEditFootDecl As String = "in float maskFactor;
in vec3 weightColor;
flat in int ZappedVert;
uniform bool bHide;
uniform bool bApplyZap;
uniform bool bShowMask;
uniform bool bShowWeight;
uniform bool bShowTexture;
uniform bool bWireframe;
uniform vec3 color;              // the shape's Wirecolor (edit view)
uniform vec3 subColor;           // the shape's TintColor (edit view)
"

    Private Const EffectFlatViewOpenText As String = "    if (!bShowTexture)
    {
        vec3 e = clamp(color, 0.0, 1.0);
        if (!bWireframe) e *= subColor;
"

    ''' <summary>The flat view's opening: without texture, the edit colour e = the Wirecolor times the TintColor outside wireframe; the
    ''' caller's next line writes o0.rgb from e by its game's colour law. Directed mutants (ParityGate fo4-effect-foot-text,
    ''' ShadowGate --sse-effect-edit-views): fo4-foot-no-wireframe, the TintColor in wireframe too; eff-foot-flat-no-subcolor, the flat
    ''' view without the TintColor.</summary>
    Friend ReadOnly Property EffectFlatViewOpen As String
        Get
#If DEBUG Then
            If Fo4GBufferConstants.GateMutantOn("fo4-foot-no-wireframe") Then Return EffectFlatViewOpenText.Replace("if (!bWireframe) e *= subColor;", "e *= subColor;")
            If Fo4GBufferConstants.GateMutantOn("eff-foot-flat-no-subcolor") Then Return EffectFlatViewOpenText.Replace("if (!bWireframe) e *= subColor;", "")
#End If
            Return EffectFlatViewOpenText
        End Get
    End Property

    Private Const EffectEditFootCloseText As String = "    }
    if (bShowMask) o0.rgb *= maskFactor;
    if (bShowWeight) o0.rgb *= weightColor;
    if (bHide || (bApplyZap && ZappedVert == 1)) kill = true;
    if (kill) discard;
    if ((uDecalBaseMode == 1 && o0.a <= 0.0) || uDecalBaseMode == 3 || (uDecalBaseMode == 4 && o0.a >= 1.0)) discard;
"

    ''' <summary>The rest of the foot after the flat view's colour line: the mask and weight views, hide and zap (a kill), the record's
    ''' kill and the translucent decals' base modes (uDecalBaseMode: 1 discards alpha &lt;= 0, 3 all, 4 alpha &gt;= 1). Directed mutants
    ''' (ShadowGate --sse-effect-edit-views): eff-foot-no-mask, eff-foot-no-weight - the foot without that view's line.</summary>
    Friend ReadOnly Property EffectEditFootClose As String
        Get
#If DEBUG Then
            If Fo4GBufferConstants.GateMutantOn("eff-foot-no-mask") Then Return EffectEditFootCloseText.Replace("if (bShowMask) o0.rgb *= maskFactor;", "")
            If Fo4GBufferConstants.GateMutantOn("eff-foot-no-weight") Then Return EffectEditFootCloseText.Replace("if (bShowWeight) o0.rgb *= weightColor;", "")
#End If
            Return EffectEditFootCloseText
        End Get
    End Property
End Module
