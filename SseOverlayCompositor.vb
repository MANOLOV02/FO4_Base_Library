Imports System.Linq

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
    ''' <summary>Mapea un blend-mode de skee al par (blendOp, softLightModel) del dispatch COMPARTIDO CPU/GL.
    ''' ⭐ FUENTE ÚNICA: la usan <see cref="ApplyOverlays"/> (CPU) y el path GPU (uniform uBlendOp del compositor),
    ''' así los dos no pueden desincronizarse (antes el mapeo vivía inline en el CPU y el GPU no tenía ninguno).
    ''' softLightModel = 3 (Pegtop) SIEMPRE: es el que usa RaceMenu.
    ''' ⛔ Grayscale(20) y ColorMode(21) son NO SEPARABLES (necesitan los 3 canales del DESTINO juntos: luminancia y
    ''' HSV). El shader los implementa en <c>blendDispatch</c> (blendGrayscale/blendColorMode); el CPU los resuelve en
    ''' ramas propias dentro de ApplyOverlays con la MISMA fórmula. Por eso NO pueden pasar por BlendChannel, que es
    ''' escalar (per-canal).</summary>
    Public Function BlendOpFromSseMode(mode As SseBlendMode) As (BlendOp As Integer, SoftLight As Integer)
        Const PEGTOP As Integer = 3
        Select Case mode
            Case SseBlendMode.Multiply : Return (1, PEGTOP)
            Case SseBlendMode.Overlay, SseBlendMode.Tint : Return (2, PEGTOP)   ' RaceMenu "tint" == overlay (misma fórmula)
            Case SseBlendMode.SoftLight : Return (3, PEGTOP)
            Case SseBlendMode.LinearDodge : Return (12, PEGTOP)
            Case SseBlendMode.LinearBurn : Return (13, PEGTOP)
            Case SseBlendMode.LinearLight : Return (16, PEGTOP)
            Case SseBlendMode.ColorDodge : Return (8, PEGTOP)
            Case SseBlendMode.ColorBurn : Return (9, PEGTOP)
            Case SseBlendMode.Darken : Return (6, PEGTOP)
            Case SseBlendMode.Lighten : Return (7, PEGTOP)
            Case SseBlendMode.Grayscale : Return (20, PEGTOP)    ' NO separable → shader blendGrayscale
            Case SseBlendMode.ColorMode : Return (21, PEGTOP)    ' NO separable → shader blendColorMode
            Case Else : Return (0, PEGTOP)                        ' Normal / Rnm / TextureMode
        End Select
    End Function

    Public Sub ApplyOverlays(acc As Double(), overlays As IList(Of SseOverlay), w As Integer, h As Integer)
        If overlays Is Nothing OrElse overlays.Count = 0 Then Return
        Dim npix = w * h
        ' El loop de CAPAS queda SERIAL (el composite no es conmutativo: cada capa lee el acumulado de la
        ' anterior). El loop de PÍXELES dentro de cada capa es paralelo por rangos: cada píxel lee/escribe sólo
        ' sus propios índices ⇒ bit-idéntico al serial. El fold SSE corre a la resolución nativa del complexion
        ' (4096² con COtR), donde esto era parte de los segundos por fold.
        For Each ovIter In overlays
            Dim ov = ovIter   ' copia local para el lambda (el iterador muta)
            ' El mapeo modo→(blendOp, softLight) es constante por capa: se resuelve UNA vez, no por píxel.
            Dim m = BlendOpFromSseMode(ov.BlendMode)
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    For i = range.Item1 To range.Item2 - 1
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
                                ' Reuse the SHARED FO4 blend dispatch (CPU/GL parity). El mapeo modo→(blendOp, softLight)
                                ' sale de BlendOpFromSseMode = la MISMA fuente que usa el path GPU (uBlendOp del compositor),
                                ' así CPU y GL no pueden desincronizarse.
                                rr = FaceTintCpuCompositor.BlendChannel(m.BlendOp, m.SoftLight, ar, br)
                                rg = FaceTintCpuCompositor.BlendChannel(m.BlendOp, m.SoftLight, ag, bg)
                                rb = FaceTintCpuCompositor.BlendChannel(m.BlendOp, m.SoftLight, ab, bbl)
                            End If
                            acc(i * 4) = (1 - la) * ar + rr * la
                            acc(i * 4 + 1) = (1 - la) * ag + rg * la
                            acc(i * 4 + 2) = (1 - la) * ab + rb * la
                        End If
                    Next
                End Sub)
        Next
    End Sub

    ''' <summary>Bake the NPC's FACE overlays (the <c>Face [Ovl{n}]</c> nodes of the .jslot <c>overrides</c>
    ''' array — the SAME <see cref="RaceMenuJslot.JslotOverlayNode"/> list the editor edits and the render draws
    ''' as coplanar decals) INTO a diffuse accumulator, in place. The engine renders these live and never bakes
    ''' them; we bake so the per-NPC diffuse is WYSIWYG. Composite = skee's <c>normal.fx</c> straight-alpha-over
    ''' (the .jslot overrides carry NO per-overlay blend mode → skee uses "normal"): per pixel the overlay colour
    ''' is <c>tex.rgb × tint.rgb</c> (type 0, when <see cref="JslotOverlayNode.HasTint"/>) else <c>tex.rgb</c>,
    ''' and coverage = <c>tex.a × opacity</c> (opacity = key8 <see cref="JslotOverlayNode.Alpha"/> when
    ''' <see cref="JslotOverlayNode.HasAlpha"/>, else 1). Overlays with no diffuse texture are skipped.
    '''
    ''' <paramref name="acc"/> is linear RGBA [0,1] (length w*h*4) — the resolved head complexion diffuse.
    ''' <paramref name="overlays"/> is the FULL overrides list; only nodes whose name starts with "Face" are
    ''' composited (body/hands/feet are the body path). Returns True iff at least one overlay contributed.
    ''' Decode is via <paramref name="decode"/> (path → linear RGBA at w×h) so the module stays FilesDictionary-
    ''' agnostic; callers pass <see cref="SseFaceTintComposer.DecodeTextureRgba"/>. Order = por ÍNDICE DE NODO
    ''' Ovl{n} ascendente (Ovl0 abajo → OvlN arriba), = skee (OverlayInterface for i=0..N), NO por posición de lista.</summary>
    ''' <summary>Orden configurable de los overlays Face[Ovl] (= análogo SSE de los SWAPS de FO4:
    ''' <c>Setting_FaceTintSort_SSE.SwapRules</c>, claves <see cref="FaceTintSseOverlaySortKey"/>). DEFAULT =
    ''' <c>[Ovl_Index asc]</c> = orden skee (Ovl0 abajo→OvlN arriba, OverlayInterface for i=0..N) = IDENTIDAD ⇒
    ''' byte-idéntico. Tiebreak final por posición original ⇒ claves iguales preservan el orden skee. Recibe la lista
    ''' YA filtrada (los dos callers filtran por DiffusePath vs NormalPath). Reordenar DESVÍA de skee (elección del usuario).</summary>
    Public Function SortFaceOverlays(list As List(Of RaceMenuJslot.JslotOverlayNode)) As List(Of RaceMenuJslot.JslotOverlayNode)
        If list Is Nothing OrElse list.Count <= 1 Then Return list
        Dim cfg = Config_App.Current?.Setting_FaceTintSort_SSE
        Dim rules = If(cfg IsNot Nothing, cfg.SwapRules, Nothing)
        If rules Is Nothing OrElse rules.Count = 0 Then Return list.OrderBy(Function(o) ParseOvlIndex(o.NodeName)).ToList()
        Dim items As New List(Of (Ov As RaceMenuJslot.JslotOverlayNode, Pos As Integer))
        For i = 0 To list.Count - 1 : items.Add((list(i), i)) : Next
        items.Sort(Function(a, b)
                       For Each r In rules
                           Dim c = SseOverlayKey(a.Ov, r.Key).CompareTo(SseOverlayKey(b.Ov, r.Key))
                           If r.Descending Then c = -c
                           If c <> 0 Then Return c
                       Next
                       Return a.Pos.CompareTo(b.Pos)   ' tiebreak estable = orden de entrada (post-skee OrderBy del default)
                   End Function)
        Return items.Select(Function(x) x.Ov).ToList()
    End Function

    Private Function SseOverlayKey(ov As RaceMenuJslot.JslotOverlayNode, key As Integer) As Double
        Select Case CType(key, FaceTintSseOverlaySortKey)
            Case FaceTintSseOverlaySortKey.Alpha : Return If(ov.HasAlpha, ov.Alpha, 1.0)
            Case FaceTintSseOverlaySortKey.Has_Tint : Return If(ov.HasTint, 1.0, 0.0)
            Case Else : Return ParseOvlIndex(ov.NodeName)   ' Ovl_Index (default) = orden skee
        End Select
    End Function

    Public Function ComposeFaceOverlaysIntoDiffuse(acc As Double(), overlays As IList(Of RaceMenuJslot.JslotOverlayNode),
                                                   w As Integer, h As Integer,
                                                   decode As Func(Of String, Integer, Integer, Double())) As Boolean
        If acc Is Nothing OrElse overlays Is Nothing OrElse overlays.Count = 0 OrElse decode Is Nothing Then Return False
        Dim npix = w * h
        Dim any = False
        ' ORDEN = skee: por ÍNDICE DE NODO Ovl{n} ASCENDENTE (Ovl0 abajo → OvlN arriba). skee instala
        ' `for i=0..N` + AttachChild ⇒ Ovl0 se dibuja primero (abajo) y OvlN último (arriba); el topmost gana en
        ' solapes. NO por posición en la lista (el jslot puede venir en cualquier orden). Ver OverlayInterface.cpp.
        Dim faceOrdered = SortFaceOverlays(overlays.
            Where(Function(o) IsFaceOverlay(o) AndAlso Not String.IsNullOrEmpty(o.DiffusePath)).ToList())   ' predicado unico + orden skee
        For Each ov In faceOrdered
            Dim tex = decode(ov.DiffusePath, w, h)
            If tex Is Nothing OrElse tex.Length < npix * 4 Then Continue For
            Dim opacity As Double = If(ov.HasAlpha, ov.Alpha, 1.0)
            If opacity <= 0.0 Then Continue For
            Dim tr As Double = If(ov.HasTint, ov.TintR, 1.0)
            Dim tg As Double = If(ov.HasTint, ov.TintG, 1.0)
            Dim tb As Double = If(ov.HasTint, ov.TintB, 1.0)
            ' COBERTURA = ALPHA del diffuse (FIEL AL ENGINE). VERIFICADO (Shader_Class.vb:1851-1859, sse_facegen_skin
            ' RE): en SSE el BSLightingShader —el shader del overlay decal (SkinTint/FaceGen)— NO tiene greyscale-to-
            ' color/alpha (eso vive SOLO en el BSEffectShader). El diffuse se usa normal: RGB=color, alpha=cobertura
            ' (color.a *= baseMap.a). type 0 de skee: color = tex.rgb × tint.
            ' Paralelo por rangos (píxeles independientes ⇒ bit-idéntico); el orden ENTRE overlays lo da el
            ' For Each de afuera, que sigue serial (alpha-over no conmutativo).
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    For i = range.Item1 To range.Item2 - 1
                        Dim la = Clamp01(tex(i * 4 + 3) * opacity)
                        If la <= 0.0 Then Continue For
                        acc(i * 4) = (tex(i * 4) * tr) * la + acc(i * 4) * (1 - la)
                        acc(i * 4 + 1) = (tex(i * 4 + 1) * tg) * la + acc(i * 4 + 1) * (1 - la)
                        acc(i * 4 + 2) = (tex(i * 4 + 2) * tb) * la + acc(i * 4 + 2) * (1 - la)
                    Next
                End Sub)
            any = True
        Next
        Return any
    End Function

    ''' <summary>Compose the FACE overlays' NORMAL maps into the head normal accumulator (MODEL-SPACE / MSN, in
    ''' place), in the SAME node-index order as the diffuse (Ovl0 bottom → OvlN top). Los normales NO se mezclan
    ''' como colores: se DECODIFICAN a vector [-1,1], se lerpean por cobertura, se RENORMALIZAN y se re-encodean —
    ''' así promediar dos normales no aplana la superficie. Espacio: el decal copia el flag MSN del head, así que
    ''' el normal del overlay se interpreta en el MISMO espacio que el head (sin conversión TS→MS). Cobertura =
    ''' alpha del DIFFUSE del overlay × opacidad (el decal blendea por el alpha del diffuse). Solo overlays con
    ''' <see cref="JslotOverlayNode.NormalPath"/>; los que no traen normal no tocan el _msn. Returns True si alguno
    ''' contribuyó. <paramref name="msnAcc"/> = head _msn decodificado RGBA [0,1] (length w*h*4).</summary>
    Public Function ComposeFaceOverlayNormalsIntoMsn(msnAcc As Double(), overlays As IList(Of RaceMenuJslot.JslotOverlayNode),
                                                     w As Integer, h As Integer,
                                                     decode As Func(Of String, Integer, Integer, Double())) As Boolean
        If msnAcc Is Nothing OrElse overlays Is Nothing OrElse overlays.Count = 0 OrElse decode Is Nothing Then Return False
        Dim npix = w * h
        Dim any = False
        Dim faceOrdered = SortFaceOverlays(overlays.
            Where(Function(o) IsFaceOverlay(o) AndAlso Not String.IsNullOrEmpty(o.NormalPath)).ToList())   ' predicado unico + orden skee
        For Each ov In faceOrdered
            Dim ovNorm = decode(ov.NormalPath, w, h)
            If ovNorm Is Nothing OrElse ovNorm.Length < npix * 4 Then Continue For
            ' Cobertura = alpha del DIFFUSE del overlay (la forma del tatuaje) × opacidad. Si no hay diffuse,
            ' usa el alpha del propio normal (fallback).
            Dim ovDiff = If(Not String.IsNullOrEmpty(ov.DiffusePath), decode(ov.DiffusePath, w, h), Nothing)
            Dim opacity As Double = If(ov.HasAlpha, ov.Alpha, 1.0)
            If opacity <= 0.0 Then Continue For
            ' Paralelo por rangos (píxeles independientes ⇒ bit-idéntico); orden entre overlays = For Each serial.
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    For i = range.Item1 To range.Item2 - 1
                        Dim cov As Double = If(ovDiff IsNot Nothing AndAlso ovDiff.Length >= npix * 4, ovDiff(i * 4 + 3), ovNorm(i * 4 + 3)) * opacity
                        If cov <= 0.0 Then Continue For
                        If cov > 1.0 Then cov = 1.0
                        ' decode ambos a [-1,1], lerp, renormalize, re-encode a [0,1].
                        Dim hx = 2.0 * msnAcc(i * 4) - 1.0, hy = 2.0 * msnAcc(i * 4 + 1) - 1.0, hz = 2.0 * msnAcc(i * 4 + 2) - 1.0
                        Dim ox = 2.0 * ovNorm(i * 4) - 1.0, oy = 2.0 * ovNorm(i * 4 + 1) - 1.0, oz = 2.0 * ovNorm(i * 4 + 2) - 1.0
                        Dim nx = hx + cov * (ox - hx), ny = hy + cov * (oy - hy), nz = hz + cov * (oz - hz)
                        Dim len = Math.Sqrt(nx * nx + ny * ny + nz * nz)
                        If len > 0.0000001 Then nx /= len : ny /= len : nz /= len
                        msnAcc(i * 4) = (nx + 1.0) * 0.5
                        msnAcc(i * 4 + 1) = (ny + 1.0) * 0.5
                        msnAcc(i * 4 + 2) = (nz + 1.0) * 0.5
                    Next
                End Sub)
            any = True
        Next
        Return any
    End Function

    ''' <summary>True iff any FACE overlay carries a normal map (cheap check, no decode).</summary>
    Public Function HasFaceOverlayNormals(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As Boolean
        If overlays Is Nothing Then Return False
        For Each ov In overlays
            If IsFaceOverlay(ov) AndAlso Not String.IsNullOrEmpty(ov.NormalPath) Then Return True
        Next
        Return False
    End Function

    ''' <summary>⭐ THE canonical "is this overlay on the head?" test. EVERY path — CPU bake, GPU bake, the folded
    ''' and non-folded variants, the live render, and the Papyrus apply-script emitter — must agree on this one
    ''' predicate, or an overlay ends up composited twice or not at all.
    '''
    ''' <para>It exists because they did NOT agree: <c>BuildFaceOverlayGpuLayers</c> filtered on "has a diffuse"
    ''' and forgot the node check entirely, so the GPU bake path composited BODY tattoos into the FACE texture
    ''' while the CPU path did not.</para></summary>
    Public Function IsFaceOverlay(ov As RaceMenuJslot.JslotOverlayNode) As Boolean
        Return ov IsNot Nothing AndAlso IsFaceOverlayNodeName(ov.NodeName)
    End Function

    ''' <summary>El MISMO test, por nombre de nodo — para los call sites que sólo tienen el string (el emisor del
    ''' script Papyrus, el ruteo de shapes del render). Que exista una sola implementación es el punto entero.</summary>
    Public Function IsFaceOverlayNodeName(nodeName As String) As Boolean
        Return Not String.IsNullOrEmpty(nodeName) AndAlso
               nodeName.TrimStart().StartsWith("Face", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>The FACE overlays of <paramref name="overlays"/> — node filter only, no texture requirement.
    ''' Callers pass THIS to the composers; each composer then keeps what it can actually consume (the diffuse
    ''' composer wants <c>DiffusePath</c>, the normal composer wants <c>NormalPath</c>). Filtering on a texture
    ''' at the CALLER is what dropped normal-only face overlays.</summary>
    Public Function FaceOverlaysOnly(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As List(Of RaceMenuJslot.JslotOverlayNode)
        If overlays Is Nothing Then Return New List(Of RaceMenuJslot.JslotOverlayNode)()
        Return overlays.Where(AddressOf IsFaceOverlay).ToList()
    End Function

    ''' <summary>True iff <paramref name="overlays"/> has at least one FACE overlay with a diffuse texture — i.e.
    ''' whether <see cref="ComposeFaceOverlaysIntoDiffuse"/> would emit anything.</summary>
    Public Function HasBakeableFaceOverlays(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As Boolean
        If overlays Is Nothing Then Return False
        For Each ov In overlays
            If IsFaceOverlay(ov) AndAlso Not String.IsNullOrEmpty(ov.DiffusePath) Then Return True
        Next
        Return False
    End Function

    ''' <summary>⭐ The gate EVERY bake path must use: is there ANY face overlay the bake can fold — diffuse OR
    ''' normal? A normal-only face overlay is legal (<see cref="ComposeFaceOverlayNormalsIntoMsn"/> folds it using
    ''' the normal's own alpha as coverage), and gating on diffuse alone made the bake return early and drop it —
    ''' while the apply-script skips ALL face nodes on principle, so nobody applied it and it vanished.</summary>
    Public Function HasAnyFoldableFaceOverlay(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As Boolean
        Return HasBakeableFaceOverlays(overlays) OrElse HasFaceOverlayNormals(overlays)
    End Function

    ''' <summary>skee's colour presets (TintMaskInterface.h): a MASKC / TintData colour stored as this raw
    ''' SInt32 is NOT a literal colour but a live-NPC colour reference. −2 = the NPC skin colour, −1 = the NPC
    ''' hair colour (hair channels ×2, clamped — CreateTintsFromData:59-61). As unsigned: 0xFFFFFFFE / 0xFFFFFFFF.</summary>
    Public Const SkeePresetSkin As UInteger = &HFFFFFFFEUI
    Public Const SkeePresetHair As UInteger = &HFFFFFFFFUI

    ''' <summary>Build ONE skee GPU-compositor layer (TintMaskInterface / CDXNifTextureRenderer) as an
    ''' <see cref="SseOverlay"/> ready for <see cref="ApplyOverlays"/>. This is the skee analogue of the vanilla
    ''' facetint: MASKT (texture) + MASKC (colour, ARGB with A=opacity) + MASKA (alpha) per index, or a TintData
    ''' XML mask. <paramref name="colorArgbOrPreset"/> is the raw MASKC/TintData colour — if it equals
    ''' <see cref="SkeePresetSkin"/>/<see cref="SkeePresetHair"/> the live NPC skin/hair colour is substituted
    ''' (<paramref name="skinRgb"/>/<paramref name="hairRgb"/>, hair ×2 clamped). <paramref name="opacity"/> is
    ''' MASKA (skee folds it into the colour's A byte). <paramref name="layerType"/> 0=Normal/1=Mask/2=Color;
    ''' <paramref name="blend"/> the technique (default normal). <paramref name="texRgba"/> = decoded mask texture
    ''' (linear RGBA w*h*4) or Nothing for a type-2 solid layer.</summary>
    Public Function BuildSkeeMaskLayer(colorArgbOrPreset As UInteger, opacity As Double, texRgba As Double(),
                                       layerType As Integer, blend As SseBlendMode,
                                       skinRgb As Double(), hairRgb As Double()) As SseOverlay
        Dim r As Double, g As Double, b As Double
        If colorArgbOrPreset = SkeePresetSkin AndAlso skinRgb IsNot Nothing AndAlso skinRgb.Length >= 3 Then
            r = skinRgb(0) : g = skinRgb(1) : b = skinRgb(2)
        ElseIf colorArgbOrPreset = SkeePresetHair AndAlso hairRgb IsNot Nothing AndAlso hairRgb.Length >= 3 Then
            r = Clamp01(hairRgb(0) * 2.0) : g = Clamp01(hairRgb(1) * 2.0) : b = Clamp01(hairRgb(2) * 2.0)   ' skee ×2 clamp
        Else
            ' ARGB byte order (skee SetColorA: A<<24|R<<16|G<<8|B). RGB from bits 16/8/0.
            r = CDbl((colorArgbOrPreset >> 16) And &HFF) / 255.0
            g = CDbl((colorArgbOrPreset >> 8) And &HFF) / 255.0
            b = CDbl(colorArgbOrPreset And &HFF) / 255.0
        End If
        Return New SseOverlay With {
            .BlendMode = blend,
            .LayerType = layerType,
            .Color = New Double() {r, g, b, Clamp01(opacity)},
            .Texture = texRgba}
    End Function

    ''' <summary>Índice del nodo <c>[Ovl{n}]</c> / <c>[SOvl{n}]</c> (n entero) o Integer.MaxValue si no matchea
    ''' (los sin índice van al final). Es el orden de composición de skee (OverlayInterface for i=0..N).</summary>
    Public Function ParseOvlIndex(nodeName As String) As Integer
        If String.IsNullOrEmpty(nodeName) Then Return Integer.MaxValue
        Dim open = nodeName.IndexOf("[Ovl", StringComparison.OrdinalIgnoreCase)
        If open < 0 Then Return Integer.MaxValue
        Dim close = nodeName.IndexOf("]"c, open)
        If close < 0 Then Return Integer.MaxValue
        ' Salta "[Ovl" o "[SOvl": avanza hasta el primer dígito.
        Dim i = open + 4
        While i < close AndAlso Not Char.IsDigit(nodeName(i))
            i += 1
        End While
        Dim digits = nodeName.Substring(i, close - i)
        Dim n As Integer
        Return If(Integer.TryParse(digits, n), n, Integer.MaxValue)
    End Function

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
