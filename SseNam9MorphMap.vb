''' <summary>
''' SSE (Skyrim) head-morph slider map — the engine's NAM9/NAMA -> chargen-morph mapping, byte-verified
''' against SkyrimSE.exe (slider table @0x1ff92a0). Single source of truth shared by the render/bake morph
''' resolver AND the face editor (so the UI sliders, the live render and the bake all agree).
'''
''' NAM9 = 18 signed floats (+ a 19th trailing float, unused here). Each slider i drives the chargen TRI
''' morph <see cref="Slider.Pos"/> when value>=0 or <see cref="Slider.Neg"/> when value&lt;0, weighted by |value|.
''' NAMA = 4 uint32 "type" indices (Nose, Brow, Eyes, Mouth); index 0 = "Default", index N = family+N morph
''' (e.g. Eyes 3 -> "EyesType3"). 0xFFFFFFFF = unset.
''' </summary>
Public NotInheritable Class SseNam9MorphMap
    Private Sub New()
    End Sub

    ''' <summary>One NAM9 slider: the positive/negative chargen-morph names and a human label for the UI.</summary>
    Public Structure Slider
        Public ReadOnly Pos As String
        Public ReadOnly Neg As String
        Public ReadOnly Label As String
        Public Sub New(pos As String, neg As String, label As String)
            Me.Pos = pos : Me.Neg = neg : Me.Label = label
        End Sub
    End Structure

    ''' <summary>The 18 NAM9 sliders in engine order (index = NAM9 float index). Names are byte-verified
    ''' chargen TRI morph names; labels are for the editor UI.</summary>
    Public Shared ReadOnly Sliders As Slider() = {
        New Slider("NoseLong", "NoseShort", "Nose Length"),
        New Slider("NoseUp", "NoseDown", "Nose Height"),
        New Slider("JawDown", "JawUp", "Jaw Height"),
        New Slider("JawWide", "JawNarrow", "Jaw Width"),
        New Slider("JawForward", "JawBack", "Jaw Forward"),
        New Slider("CheeksUp", "CheeksDown", "Cheekbone Height"),
        New Slider("CheeksOut", "CheeksIn", "Cheekbone Width"),
        New Slider("EyesMoveUp", "EyesMoveDown", "Eye Height"),
        New Slider("EyesMoveOut", "EyesMoveIn", "Eye Width"),
        New Slider("BrowUp", "BrowDown", "Brow Height"),
        New Slider("BrowOut", "BrowIn", "Brow Width"),
        New Slider("BrowForward", "BrowBack", "Brow Forward"),
        New Slider("LipMoveUp", "LipMoveDown", "Mouth Height"),
        New Slider("LipMoveOut", "LipMoveIn", "Mouth Forward"),
        New Slider("ChinWide", "ChinThin", "Chin Width"),
        New Slider("ChinMoveDown", "ChinMoveUp", "Chin Length"),
        New Slider("Underbite", "Overbite", "Chin Forward"),
        New Slider("EyesForward", "EyesBack", "Eye Depth")}

    ''' <summary>A NAMA "type" family: the chargen-morph name prefix and a UI label. NAMA value N selects
    ''' "&lt;Prefix&gt;N" (N>=1); value 0 = "Default" (no type morph).</summary>
    Public Structure TypeFamily
        Public ReadOnly Prefix As String
        Public ReadOnly Label As String
        Public Sub New(prefix As String, label As String)
            Me.Prefix = prefix : Me.Label = label
        End Sub
    End Structure

    ''' <summary>The 4 NAMA families in engine order (index = NAMA uint index): Nose, Brow, Eyes, Mouth.</summary>
    Public Shared ReadOnly Families As TypeFamily() = {
        New TypeFamily("NoseType", "Nose Type"),
        New TypeFamily("BrowType", "Brow Type"),
        New TypeFamily("EyesType", "Eyes Type"),
        New TypeFamily("LipType", "Mouth Type")}

    Public Const Nam9SliderCount As Integer = 18
    Public Const NamaFamilyCount As Integer = 4
    Public Const NamaUnset As UInteger = &HFFFFFFFFUI

    ''' <summary>The chargen-morph name a slider value selects (Pos if >=0, Neg if &lt;0), or "" if ~zero.</summary>
    Public Shared Function MorphForSlider(sliderIndex As Integer, value As Single) As String
        If sliderIndex < 0 OrElse sliderIndex >= Sliders.Length Then Return ""
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) OrElse Math.Abs(value) < 0.001F Then Return ""
        Return If(value >= 0, Sliders(sliderIndex).Pos, Sliders(sliderIndex).Neg)
    End Function

    ''' <summary>The chargen-morph name a NAMA type value selects ("Default" for 0, "&lt;Prefix&gt;N" for N>=1),
    ''' or "" if unset (0xFFFFFFFF).</summary>
    Public Shared Function MorphForType(familyIndex As Integer, value As UInteger) As String
        If familyIndex < 0 OrElse familyIndex >= Families.Length OrElse value = NamaUnset Then Return ""
        If value = 0UI Then Return "Default"
        Return Families(familyIndex).Prefix & value.ToString()
    End Function
End Class
