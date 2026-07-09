''' <summary>
''' RaceMenu / NiOverride (skee64) face-overlay compositor — the engine-EXACT blend of overlay layers ON TOP
''' of the vanilla facetint, decoded from the RaceMenu HLSL SOURCE (.fx in RaceMenu.bsa, not inferred). See
''' reference_racemenu_overlay_blend. skee applies overlays live (bExternalHeads=0 → not baked); to bake a
''' WYSIWYG _d (as the app does for LooksMenu overlays in FO4) we composite them here.
'''
''' Per layer: (1) TYPE combines the overlay texture with the layer colour; (2) BLENDMODE composites over the
''' accumulator, weighted by the layer alpha. Order = Ovl0..N (skee applies in slot order; lerp/blend NOT
''' commutative). SSE-only; the FO4 facetint path is untouched.
''' </summary>
Public Module SseOverlayCompositor

    ''' <summary>NiOverride blend modes (technique name → this enum). Math is per reference_racemenu_overlay_blend.</summary>
    Public Enum SseBlendMode
        Normal
        Multiply
        Overlay
        SoftLight       ' Pegtop
        LinearDodge     ' = Add / Screen-ish
        LinearBurn
        LinearLight
        ColorDodge
        ColorBurn
        Darken
        Lighten
        Tint
        Grayscale
        ColorMode       ' HSV: H,S from blend + V from source
        Rnm             ' reoriented normal map (normals only) — treated as Normal for colour _d
        TextureMode     ' replace/normal for textures — treated as Normal
    End Enum

    ''' <summary>One overlay layer. Color is RGBA [0,1]; Texture is decoded RGBA [0,1] (w*h*4) or Nothing
    ''' (type 2 solid). LayerType: 0 = texture×color, 1 = colour.rgb + (mask.r × colour.a) alpha, 2 = solid.</summary>
    Public Structure SseOverlay
        Public BlendMode As SseBlendMode
        Public LayerType As Integer
        Public Color As Double()     ' length 4, RGBA
        Public Texture As Double()   ' length w*h*4 RGBA, or Nothing
    End Structure

    ''' <summary>Map a NiOverride blend-mode technique string (lowercase .fx name) to the enum. Unknown → Normal.</summary>
    Public Function BlendModeFromName(name As String) As SseBlendMode
        Select Case If(name, "").Trim().ToLowerInvariant()
            Case "multiply" : Return SseBlendMode.Multiply
            Case "overlay" : Return SseBlendMode.Overlay
            Case "softlight" : Return SseBlendMode.SoftLight
            Case "lineardodge", "add", "screen" : Return SseBlendMode.LinearDodge
            Case "linearburn" : Return SseBlendMode.LinearBurn
            Case "linearlight" : Return SseBlendMode.LinearLight
            Case "colordodge" : Return SseBlendMode.ColorDodge
            Case "colorburn" : Return SseBlendMode.ColorBurn
            Case "darken" : Return SseBlendMode.Darken
            Case "lighten" : Return SseBlendMode.Lighten
            Case "tint" : Return SseBlendMode.Tint
            Case "grayscale" : Return SseBlendMode.Grayscale
            Case "color" : Return SseBlendMode.ColorMode
            Case "rnm" : Return SseBlendMode.Rnm
            Case "texture" : Return SseBlendMode.TextureMode
            Case Else : Return SseBlendMode.Normal
        End Select
    End Function

    ''' <summary>Composite the overlays over <paramref name="acc"/> (linear RGBA, in place). No-op when the list
    ''' is empty (vanilla NPCs) — the code path always runs so modded NPCs bake WYSIWYG.</summary>
    Public Sub ApplyOverlays(acc As Double(), overlays As IList(Of SseOverlay), w As Integer, h As Integer)
        If overlays Is Nothing OrElse overlays.Count = 0 Then Return
        Dim npix = w * h
        For Each ov In overlays
            For i = 0 To npix - 1
                ' (1) TYPE: combine the overlay texture with the layer colour → premultiplied layer {rgb, a}
                Dim lr As Double, lg As Double, lb As Double, la As Double
                Dim tr = 1.0, tg = 1.0, tb = 1.0, ta = 1.0
                If ov.Texture IsNot Nothing Then tr = ov.Texture(i * 4) : tg = ov.Texture(i * 4 + 1) : tb = ov.Texture(i * 4 + 2) : ta = ov.Texture(i * 4 + 3)
                Select Case ov.LayerType
                    Case 1 : lr = ov.Color(0) : lg = ov.Color(1) : lb = ov.Color(2) : la = tr * ov.Color(3)          ' colour.rgb, alpha = mask.r × colour.a
                    Case 2 : lr = ov.Color(0) : lg = ov.Color(1) : lb = ov.Color(2) : la = ov.Color(3)               ' solid colour
                    Case Else : lr = tr * ov.Color(0) : lg = tg * ov.Color(1) : lb = tb * ov.Color(2) : la = ta * ov.Color(3) ' texture × colour
                End Select
                If la <= 0.0 Then Continue For

                Dim ar = acc(i * 4), ag = acc(i * 4 + 1), ab = acc(i * 4 + 2)
                If ov.BlendMode = SseBlendMode.Normal OrElse ov.BlendMode = SseBlendMode.Rnm OrElse ov.BlendMode = SseBlendMode.TextureMode Then
                    ' normal.fx: over with PREMULTIPLIED layer.rgb (no un-premultiply)
                    acc(i * 4) = lr * la + ar * (1 - la)
                    acc(i * 4 + 1) = lg * la + ag * (1 - la)
                    acc(i * 4 + 2) = lb * la + ab * (1 - la)
                Else
                    ' all other modes un-premultiply the layer colour, blend, then alpha-over
                    Dim br = Clamp01(lr / la), bg = Clamp01(lg / la), bbl = Clamp01(lb / la)
                    Dim rr As Double, rg As Double, rb As Double
                    If ov.BlendMode = SseBlendMode.Grayscale Then
                        Dim lum = 0.299 * ar + 0.587 * ag + 0.114 * ab
                        rr = lum * br : rg = lum * bg : rb = lum * bbl
                    ElseIf ov.BlendMode = SseBlendMode.ColorMode Then
                        Dim hsvBlend = RgbToHsv(br, bg, bbl)
                        Dim vSrc = Math.Max(ar, Math.Max(ag, ab))
                        Dim outc = HsvToRgb(hsvBlend(0), hsvBlend(1), vSrc)
                        rr = outc(0) : rg = outc(1) : rb = outc(2)
                    Else
                        ' Reuse the SHARED FO4 blend dispatch (CPU/GL parity) — map RaceMenu mode → (blendOp, softLightModel).
                        Dim bop = 0, slm = 3   ' softlight model 3 = Pegtop (what RaceMenu uses)
                        Select Case ov.BlendMode
                            Case SseBlendMode.Multiply : bop = 1
                            Case SseBlendMode.Overlay, SseBlendMode.Tint : bop = 2   ' RaceMenu "tint" == overlay (same formula)
                            Case SseBlendMode.SoftLight : bop = 3
                            Case SseBlendMode.LinearDodge : bop = 12
                            Case SseBlendMode.LinearBurn : bop = 13
                            Case SseBlendMode.LinearLight : bop = 16
                            Case SseBlendMode.ColorDodge : bop = 8
                            Case SseBlendMode.ColorBurn : bop = 9
                            Case SseBlendMode.Darken : bop = 6
                            Case SseBlendMode.Lighten : bop = 7
                            Case Else : bop = 0
                        End Select
                        rr = FaceTintCpuCompositor.BlendChannel(bop, slm, ar, br)
                        rg = FaceTintCpuCompositor.BlendChannel(bop, slm, ag, bg)
                        rb = FaceTintCpuCompositor.BlendChannel(bop, slm, ab, bbl)
                    End If
                    acc(i * 4) = (1 - la) * ar + rr * la
                    acc(i * 4 + 1) = (1 - la) * ag + rg * la
                    acc(i * 4 + 2) = (1 - la) * ab + rb * la
                End If
            Next
        Next
    End Sub

    Private Function Clamp01(v As Double) As Double
        Return If(v < 0, 0, If(v > 1, 1, v))
    End Function

    Private Function RgbToHsv(r As Double, g As Double, b As Double) As Double()
        Dim mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn
        Dim h As Double = 0
        If d > 0.0000001 Then
            If mx = r Then h = ((g - b) / d) Mod 6 Else If mx = g Then h = (b - r) / d + 2 Else h = (r - g) / d + 4
            h /= 6 : If h < 0 Then h += 1
        End If
        Return New Double() {h, If(mx <= 0, 0, d / mx), mx}
    End Function

    Private Function HsvToRgb(h As Double, s As Double, v As Double) As Double()
        Dim r = Clamp01(Math.Abs(((h * 6 + 0) Mod 6) - 3) - 1)
        Dim g = Clamp01(Math.Abs(((h * 6 + 4) Mod 6) - 3) - 1)
        Dim b = Clamp01(Math.Abs(((h * 6 + 2) Mod 6) - 3) - 1)
        Return New Double() {v * (1 + s * (r - 1)), v * (1 + s * (g - 1)), v * (1 + s * (b - 1))}
    End Function

End Module
