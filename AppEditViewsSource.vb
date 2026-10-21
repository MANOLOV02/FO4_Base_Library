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
End Module
