Imports System.Runtime.CompilerServices
Imports System.Linq

''' <summary>
''' SSE (Skyrim Special Edition) face-tint compositor — the engine-faithful reproduction of what the
''' CreationKit bakes into <c>FaceGenData\FaceTint\&lt;plugin&gt;\&lt;fid&gt;.dds</c> (512×512 DXT5, _d only).
'''
''' ENGINE MODEL (verified in CreationKit.exe, re_sseck — NOT curve-fit):
'''  - BSFaceGenUtils (bsfacegenutils.cpp) builds a float4[16] of {color×1/255, interp} per tint mask and
'''    hands it to an image-space pixel shader. It NEVER reads a "type"/TINP field: EVERY layer is the same
'''    coverage-gated lerp — there is NO per-type blend-op. Setter @0x142ce73e0 (imagespaceshaderparam.cpp)
'''    is a pure store into the pixel-constant buffer. Color unpack constant = 1/255 (0.00392156) confirmed.
'''  - Per layer: coverage = maskR × TINV, then acc = lerp(acc, color, coverage). Uniform for all layers.
'''  - Base = the NPC skin colour (QNAM) FLAT. Measured: CK's baked _d skin region is flat = QNAM (Dremora
'''    QNAM=0 → CK black exact). The diffuse skin DETAIL is added at RENDER by the shader, so it is NOT in the
'''    baked _d (using the head diffuse as the base gives ~52/255 — wrong). Compose in LINEAR (DecodeDds
'''    returns linear); the DXT5 target has a ~3-5/255 compression floor.
'''
''' Measured mean 4.68/255 over 2032 Skyrim.esm NPCs (median 2.36, 98.4% ≤20/255). Tattoo masks (TINI 65-74)
''' carry TINT but no TINP — they must still register (flush-on-new-TINI), else war-paint NPCs go to ~74/255.
'''
''' This is the SSE analogue of <see cref="FaceTintInputBuilder"/> (FO4). It is SSE-only; callers gate on
''' <c>Config_App.Current.Game = Config_App.Game_Enum.Skyrim</c>. See project_sse_facetint_spec.
''' </summary>
Public Module SseFaceTintComposer

    ''' <summary>One RACE tint mask: the greyscale mask texture path + its TINP mask type (-1 when the layer
    ''' omits TINP, e.g. tattoos). Type is retained for tooling/diagnostics only — the blend is uniform.</summary>
    ''' <summary>One RACE tint-layer preset: a named colour swatch the CK's tinting dropdown offers for this layer.
    ''' The NPC record selects one by its <see cref="Tirs"/> value stored in the layer's TIAS field (TIAS = TIRS →
    ''' the preset's CLFM colour; TIAS = -1 → the layer uses a custom RGB, not a preset). Verified against vanilla
    ''' Skyrim.esm: for every NPC skin-tone layer with TIAS≥0, its TINC == the CLFM colour of the preset whose
    ''' TIRS == TIAS (1673/1673, zero exceptions).</summary>
    Public Structure SseTintPreset
        Public Tirs As Integer       ' TIRS — the preset's index; the NPC's TIAS references this value
        Public Clfm As UInteger      ' TINC (RACE preset) — the CLFM formID whose CNAM colour this preset applies
        Public Value As Double       ' TINV (RACE preset) — the preset's default coverage (FLOAT 0-1)
    End Structure

    ''' <summary>One RACE tint mask: the greyscale mask texture path + its TINP mask type (-1 when the layer
    ''' omits TINP, e.g. tattoos). Type is retained for tooling/diagnostics only — the blend is uniform.</summary>
    Public Structure SseTintMask
        Public Index As Integer      ' TINI — the layer's index (NPC tints reference the RACE layer by this)
        Public Path As String        ' TINT — greyscale mask texture path
        Public MaskType As Integer   ' TINP — mask type (-1 when omitted). Diagnostic only; blend is uniform.
        Public DefaultClfm As UInteger ' TIND — CLFM formID of the default preset (colour for unauthored layers)
        Public DefaultValue As Double  ' the default preset's TINV (coverage for unauthored layers)
        Public Presets As List(Of SseTintPreset) ' the CK dropdown's swatches for this layer (TIRS→CLFM/value); may be empty
    End Structure

    ' Per-race+gender ORDERED tint-layer list cache (identical across NPCs of the same race). The engine
    ' composes cb2[0..15] in this RACE order (builder @0x18C9F40). Keyed "<raceFid><F|M>".
    Private ReadOnly _layersCache As New Dictionary(Of String, List(Of SseTintMask))
    ' Decoded+resized mask cache at 512² (race-shared masks decode once across a batch). Keyed by dict path.
    Private ReadOnly _texCache As New Dictionary(Of String, Double())(StringComparer.OrdinalIgnoreCase)
    ' FUENTE decodificada por (path, target) — para targets != 512² (el fold SSE compone a la resolución NATIVA
    ' del complexion, p.ej. 4096² con COtR) cada fold re-leía (GetBytes) y re-decodeaba (DirectXTex) TODAS las
    ' máscaras del RACE. Acá se cachea el DECODE de la fuente (al mip que DecodeDds elige para ese target — por
    ' eso el target integra la key); el RESAMPLE al target NO se cachea (4096² ≈ 537 MB de Double por máscara,
    ' inviable retenerlo) pero corre paralelo en DecodeMask. Nothing cacheado = archivo ausente/indecodificable.
    Private ReadOnly _texSrcCache As New Dictionary(Of String, FaceTintCpuCompositor.DecodedTex)(StringComparer.OrdinalIgnoreCase)
    ' Resolved CLFM formID -> linear RGB [0,1] (race-default colours), cached.
    Private ReadOnly _clfmCache As New Dictionary(Of UInteger, Double())

    ''' <summary>Drop the per-race layer + decoded-texture + CLFM caches (call on FilesDictionary rebuild).</summary>
    Public Sub ClearCaches()
        _layersCache.Clear()
        _texCache.Clear()
        _texSrcCache.Clear()
        _clfmCache.Clear()
    End Sub

    ''' <summary>Compose the SSE face tint for an NPC into a W×H linear RGBA buffer ([0,1], length W*H*4,
    ''' order R,G,B,A). Returns Nothing when the race/face-texture/QNAM can't be resolved. Pure — no GL, no
    ''' file writes. The caller encodes to DXT5 (bake) or uploads to GL (render).</summary>
    ''' <param name="npcRec">The NPC_ record (source of QNAM + the TINI/TINC/TINV/TIAS tint layer list).</param>
    ''' <param name="race">Parsed RACE (source of the default face TXST + the per-gender tint masks).</param>
    ''' <param name="npcTintOverride">Optional edited tint layers (TINI/TINC/TINV/TIAS raw subrecords) from the
    ''' editor overlay. When provided, the NPC-authored tint map is read from THIS instead of the raw
    ''' npcRec.Subrecords — so live edits (Edit Face → Face Tints) reflect in render + bake. Nothing = raw.</param>
    Public Function ComposeLinearRgba(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                      raceFormID As UInteger, isFemale As Boolean,
                                      Optional w As Integer = 512, Optional h As Integer = 512,
                                      Optional baseImg As Double() = Nothing,
                                      Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                      Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As Double()
        If pm Is Nothing OrElse npcRec Is Nothing OrElse race Is Nothing Then Return Nothing
        Dim npix = w * h

        ' ENGINE-EXACT — decoded 100% from (a) the facegen-tint pixel shader (ps_5_0, DXBC @0x40033A8) and
        ' (b) the cb2-source builder @0x18C9F40. Shader: base = 0.5; per layer acc = lerp(acc, colour,
        ' mask.r × interp); out.w = 1; UNIFORM, no per-type branch. Builder: iterate the RACE's tint layers
        ' IN RACE ORDER — for each, colour+interp come from the NPC's authored tint for that layer INDEX if
        ' present, else the RACE default (TIND→CLFM colour). interp = value_byte × 0.01 (both confirmed in the
        ' binary). The RACE order (not NPC subrecord order) is what fills cb2[0..15]; lerp is not commutative.

        ' Map the NPC's authored tint layers: index → {R, G, B (TINC/255), interp (TINV/100)}. Subrecords per
        ' layer: TINI, TINC, TINV, TIAS (TIAS terminates the layer → commit).
        Dim npcMap As New Dictionary(Of Integer, Double())
        Dim tIdx As Integer = -1, tr As Double = 0, tg As Double = 0, tb As Double = 0, tvv As Double = 0
        ' Source of the NPC-authored tints: the editor's edited layers (overlay) if provided, else the raw
        ' record. Normalised to (sig, data) so both the PluginRecord and the NPC_RawSubrecord list parse identically.
        Dim tintPairs As IEnumerable(Of (Sig As String, Data As Byte()))
        If npcTintOverride IsNot Nothing Then
            tintPairs = npcTintOverride.Select(Function(s) (s.Sig, s.Data))
        Else
            tintPairs = npcRec.Subrecords.Select(Function(s) (s.Signature, s.Data))
        End If
        For Each sr In tintPairs
            Select Case sr.Sig
                Case "TINI" : tIdx = BitConverter.ToUInt16(sr.Data, 0)
                Case "TINC" : If sr.Data.Length >= 3 Then tr = sr.Data(0) / 255.0 : tg = sr.Data(1) / 255.0 : tb = sr.Data(2) / 255.0
                Case "TINV" : If sr.Data.Length >= 4 Then tvv = BitConverter.ToUInt32(sr.Data, 0) / 100.0
                Case "TIAS" : If tIdx >= 0 Then npcMap(tIdx) = New Double() {tr, tg, tb, tvv} : tIdx = -1 : tr = 0 : tg = 0 : tb = 0 : tvv = 0
            End Select
        Next

        Dim layers = SortSseTintLayers(GetRaceLayersOrdered(pm, raceFormID, isFemale), npcMap)   ' orden configurable, default RaceMenu

        ' Ley SSE del config (FaceTintConvention.ActiveSettings, default DefaultsFor(Skyrim)): el compositor NO
        ' hardcodea el álgebra — seed, canal de máscara y espacios/blend vienen de la convención. Con los defaults
        ' SSE (seed constante 0.5, máscara canal R cruda, todo Linear, Blend=Replace) esto es BYTE-IDÉNTICO al
        ' modelo previo (lerp(acc,color,maskR×tinv)); el usuario puede tunearlos desde CharGen Options (tab SSE).
        Dim settings = FaceTintConvention.ActiveSettings()
        Dim conv = FaceTintConvention.ResolveConvention(isTextureSet:=False, slot:=0US, blendOp:=0,
                                                        channel:=FaceTintChannel.Diffuse, useHairPalette:=False)
        Dim maskConvI As Integer = CInt(conv.MaskConv)
        Dim maskCh As Integer = MaskChannelIndex(settings.Diffuse)   ' SSE default = R (0)

        ' Seed del acumulador: Constant (SSE, engine-verificado 0.5) o la baseImg del caller (diagnóstico).
        Dim acc(npix * 4 - 1) As Double
        Dim seedR As Double = 0.5, seedG As Double = 0.5, seedB As Double = 0.5
        If settings.SeedMode = FaceTintConvention.FaceTintSeedMode.Constant AndAlso settings.SeedConstant IsNot Nothing AndAlso settings.SeedConstant.Length >= 3 Then
            seedR = settings.SeedConstant(0) : seedG = settings.SeedConstant(1) : seedB = settings.SeedConstant(2)
        End If
        If baseImg IsNot Nothing AndAlso baseImg.Length >= npix * 4 Then
            Array.Copy(baseImg, acc, npix * 4)
        Else
            ' Paralelo por rangos (escrituras disjuntas por píxel ⇒ bit-idéntico); a 4K son 67M de writes.
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    For i = range.Item1 To range.Item2 - 1
                        acc(i * 4) = seedR : acc(i * 4 + 1) = seedG : acc(i * 4 + 2) = seedB : acc(i * 4 + 3) = 1.0
                    Next
                End Sub)
        End If

        ' Compose the RACE's tint layers IN RACE ORDER (= cb2 slot order; lerp is not commutative). The SKIN
        ' layer (type 6) is NOT special — it is a normal layer. Its colour = TINC (or the RACE default CLFM),
        ' composed at its TINV over base 0.5. VERIFIED (2026-07-17, vanilla shipped facetints vs resolved QNAM,
        ' 12/12 NPC discriminantes con hue-match; p.ej. 0010D4B9 QNAM (183,156,145) ≈ facetint (181,158,147)):
        ' el facetint SÍ hornea el skintone = lerp(0.5, skinTINC, skinTINV) = QNAM. So QNAM is the RESULT of this
        ' composite (stored for the body to match). This matches the disassembled bake builder (@0x18C9F40)
        ' literally: authored → npcTint colour = TINC; unauthored → RACE default CLFM (entry+0x30). No per-type branch.
        For Each layer In layers
            Dim cr As Double, cg As Double, cbb As Double, iv As Double
            Dim authored As Double() = Nothing
            If npcMap.TryGetValue(layer.Index, authored) Then
                cr = authored(0) : cg = authored(1) : cbb = authored(2) : iv = authored(3)   ' TINC + TINV/100 (raw)
            Else
                Dim dc = ResolveClfmColor(pm, layer.DefaultClfm)
                cr = dc(0) : cg = dc(1) : cbb = dc(2) : iv = layer.DefaultValue
            End If
            ' RaceMenu can override this layer's mask texture by index (PresetInterface.cpp:203). When present,
            ' composite the custom path instead of the RACE layer's own TINT path; else use the RACE path.
            Dim maskPath = layer.Path
            Dim custPath As String = Nothing
            If tintTexOverride IsNot Nothing AndAlso tintTexOverride.TryGetValue(layer.Index, custPath) AndAlso Not String.IsNullOrEmpty(custPath) Then
                maskPath = custPath
            End If
            If iv <= 0.0 OrElse String.IsNullOrEmpty(maskPath) Then Continue For
            Dim mi = DecodeMask(maskPath, w, h)
            If mi IsNot Nothing Then ComposeLayer(acc, mi, cr, cg, cbb, iv, npix, conv, maskConvI, maskCh)
        Next
        Return acc
    End Function

    ''' <summary>Contraparte GPU de <see cref="ComposeLinearRgba"/>: resuelve las capas de tint del NPC como
    ''' <see cref="FaceTintLayerInput"/> (PaletteMask) para el compositor GL (<c>ApplyFaceTintPipeline</c>). La
    ''' resolución es IDÉNTICA a ComposeLinearRgba (RACE order, color authored-vs-default, TINV, override de máscara),
    ''' de modo que componer estas capas sobre un base PLANO = seed (0.5) por GL reproduce el compose CPU del <c>_2</c>
    ''' → el par <c>_2</c> vs <c>_2b</c> mide la paridad CPU==GPU del facetint base. Los bytes de máscara salen del
    ''' FilesDictionary (el pipeline GL los decodea/sube). Nothing si no resuelve race/npc. SSE-only (debug sandbox).</summary>
    Public Function BuildLayerInputs(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                     raceFormID As UInteger, isFemale As Boolean,
                                     Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                     Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As List(Of FaceTintLayerInput)
        If pm Is Nothing OrElse npcRec Is Nothing OrElse race Is Nothing Then Return Nothing

        ' NPC-authored tint map: index → {R,G,B (TINC/255), interp (TINV/100)}. MISMA lectura que ComposeLinearRgba.
        Dim npcMap As New Dictionary(Of Integer, Double())
        Dim tIdx As Integer = -1, tr As Double = 0, tg As Double = 0, tb As Double = 0, tvv As Double = 0
        Dim tintPairs As IEnumerable(Of (Sig As String, Data As Byte()))
        If npcTintOverride IsNot Nothing Then
            tintPairs = npcTintOverride.Select(Function(s) (s.Sig, s.Data))
        Else
            tintPairs = npcRec.Subrecords.Select(Function(s) (s.Signature, s.Data))
        End If
        For Each sr In tintPairs
            Select Case sr.Sig
                Case "TINI" : tIdx = BitConverter.ToUInt16(sr.Data, 0)
                Case "TINC" : If sr.Data.Length >= 3 Then tr = sr.Data(0) / 255.0 : tg = sr.Data(1) / 255.0 : tb = sr.Data(2) / 255.0
                Case "TINV" : If sr.Data.Length >= 4 Then tvv = BitConverter.ToUInt32(sr.Data, 0) / 100.0
                Case "TIAS" : If tIdx >= 0 Then npcMap(tIdx) = New Double() {tr, tg, tb, tvv} : tIdx = -1 : tr = 0 : tg = 0 : tb = 0 : tvv = 0
            End Select
        Next

        Dim layers = SortSseTintLayers(GetRaceLayersOrdered(pm, raceFormID, isFemale), npcMap)   ' orden configurable, default RaceMenu
        ' Canal de la máscara según la ley SSE (default R). El shader GL PaletteMask lo lee por uniform (FO4=verde).
        Dim maskCh As Integer = MaskChannelIndex(FaceTintConvention.ActiveSettings().Diffuse)
        Dim outp As New List(Of FaceTintLayerInput)
        For Each layer In layers
            Dim cr As Double, cg As Double, cbb As Double, iv As Double
            Dim authored As Double() = Nothing
            If npcMap.TryGetValue(layer.Index, authored) Then
                cr = authored(0) : cg = authored(1) : cbb = authored(2) : iv = authored(3)
            Else
                Dim dc = ResolveClfmColor(pm, layer.DefaultClfm)
                cr = dc(0) : cg = dc(1) : cbb = dc(2) : iv = layer.DefaultValue
            End If
            Dim maskPath = layer.Path
            Dim custPath As String = Nothing
            If tintTexOverride IsNot Nothing AndAlso tintTexOverride.TryGetValue(layer.Index, custPath) AndAlso Not String.IsNullOrEmpty(custPath) Then
                maskPath = custPath
            End If
            If iv <= 0.0 OrElse String.IsNullOrEmpty(maskPath) Then Continue For
            Dim key = maskPath.Replace("/"c, "\"c).ToLowerInvariant()
            If Not key.StartsWith("textures\") Then key = "textures\" & key
            Dim mb = FilesDictionary_class.GetBytes(key)
            If mb Is Nothing Then Continue For
            outp.Add(New FaceTintLayerInput With {
                .Kind = FaceTintLayerKind.PaletteMask,
                .LayerDdsBytes = mb, .LayerCacheKey = key,
                .R = ClampByteLocal(cr * 255.0), .G = ClampByteLocal(cg * 255.0), .B = ClampByteLocal(cbb * 255.0),
                .Opacity = CSng(iv), .BlendOp = 0, .Slot = 0US, .IsTextureSet = False,
                .PaletteMaskChannel = maskCh,
                .DebugName = $"sse-tint idx={layer.Index}"})
        Next
        Return outp
    End Function

    ''' <summary>Clamp a double a byte [0,255] con redondeo. Local (SseFaceTintComposer no comparte el de FaceGenBuilder).</summary>
    Private Function ClampByteLocal(v As Double) As Byte
        If v < 0.0 Then Return 0
        If v > 255.0 Then Return 255
        Return CByte(Math.Round(v))
    End Function

    ''' <summary>Índice de canal (0..3) de la máscara según la ley del bucket. ByKind en SSE = R (todas las
    ''' capas facegen-tint usan el canal rojo, verificado en los .fx type==1). R/G/B/A = ese canal explícito.</summary>
    Private Function MaskChannelIndex(bucket As FaceTintConvention.FaceTintBucketConvention) As Integer
        If bucket Is Nothing Then Return 0
        Select Case bucket.MaskChannel
            Case FaceTintConvention.FaceTintMaskChannel.G : Return 1
            Case FaceTintConvention.FaceTintMaskChannel.B : Return 2
            Case FaceTintConvention.FaceTintMaskChannel.A : Return 3
            Case Else : Return 0   ' R y ByKind → canal rojo (SSE)
        End Select
    End Function

    ''' <summary>SSE analogue of FO4's <c>DeriveSkinToneQnam</c>: derive the effective QNAM (TextureLighting)
    ''' colour from the RACE's SKIN-TONE tint layer (the one whose TINP mask type == 6). Returns Nothing when the
    ''' race has no such layer. Unlike FO4 (where QNAM.A carries the intensity), SSE QNAM has NO alpha — the engine
    ''' soft-lights the body at FULL strength and the intensity is FOLDED INTO the colour:
    ''' <c>q = clamp(0.5 + TINV*(TINC/255 - 0.5), 0, 1)</c> per channel (byte = round(q*255)). Returned A = 255.
    ''' <para>The (TINC, TINV) resolution MIRRORS <see cref="ComposeLinearRgba"/> EXACTLY so the body QNAM and the
    ''' baked/rendered face use the identical skin-tone input: authored NPC tint for that layer INDEX if present
    ''' (TINC bytes /255, TINV u32 /100), else the RACE default (TIND→CLFM colour, DefaultValue). Measured
    ''' byte-exact (Afflicted TINC=(0.263,0.016,0.004)@0.52 → QNAM (96,63,61)). SSE-only; FO4 never calls this.</summary>
    Public Function ResolveSkinToneQnam(pm As PluginManager, npc As NPC_Data, race As RACE_Data,
                                        raceFid As UInteger, isFemale As Boolean) As Nullable(Of System.Drawing.Color)
        If pm Is Nothing OrElse npc Is Nothing Then Return Nothing

        ' Find the RACE skin-tone layer (TINP mask type == 6 = SkinTone). No slot-12 in SSE.
        Dim layers = GetRaceLayersOrdered(pm, raceFid, isFemale)
        If layers Is Nothing Then Return Nothing
        Dim skin As SseTintMask? = Nothing
        For Each layer In layers
            If layer.MaskType = 6 Then skin = layer : Exit For
        Next
        If Not skin.HasValue Then
            If Logger.Enabled Then
                Dim nLayers = If(layers Is Nothing, 0, layers.Count)
                Logger.LogLazy(Function() $"[SSE-QNAM] raceFid=0x{raceFid:X8} female={isFemale} → NO skin-tone layer (MaskType=6) among {nLayers} race layers → QNAM=Nothing")
            End If
            Return Nothing
        End If

        ' Build the NPC-authored tint map EXACTLY as ComposeLinearRgba does (index → {R,G,B (TINC/255), TINV/100}).
        ' Source = npc.SseTintRaw (TINI/TINC/TINV/TIAS), the same list the composer reads via npcTintOverride, so
        ' the body skin tone traces to the identical authored-vs-default resolution the face compositor uses.
        Dim npcMap As New Dictionary(Of Integer, Double())
        Dim tIdx As Integer = -1, tr As Double = 0, tg As Double = 0, tb As Double = 0, tvv As Double = 0
        If npc.SseTintRaw IsNot Nothing Then
            For Each sr In npc.SseTintRaw
                Select Case sr.Sig
                    Case "TINI" : If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then tIdx = BitConverter.ToUInt16(sr.Data, 0)
                    Case "TINC" : If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 3 Then tr = sr.Data(0) / 255.0 : tg = sr.Data(1) / 255.0 : tb = sr.Data(2) / 255.0
                    Case "TINV" : If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then tvv = BitConverter.ToUInt32(sr.Data, 0) / 100.0
                    Case "TIAS" : If tIdx >= 0 Then npcMap(tIdx) = New Double() {tr, tg, tb, tvv} : tIdx = -1 : tr = 0 : tg = 0 : tb = 0 : tvv = 0
                End Select
            Next
        End If

        ' Resolve the skin layer's colour + interp: authored (npcMap) wins, else RACE default CLFM + DefaultValue.
        ' This is the SAME branch ComposeLinearRgba runs per layer (see the For Each layer loop there).
        Dim cr As Double, cg As Double, cbb As Double, iv As Double
        Dim authored As Double() = Nothing
        If npcMap.TryGetValue(skin.Value.Index, authored) Then
            cr = authored(0) : cg = authored(1) : cbb = authored(2) : iv = authored(3)   ' TINC/255 + TINV/100
        Else
            Dim dc = ResolveClfmColor(pm, skin.Value.DefaultClfm)
            cr = dc(0) : cg = dc(1) : cbb = dc(2) : iv = skin.Value.DefaultValue
        End If

        ' Fold intensity into the colour: q = lerp(0.5, TINC, TINV) per channel. QNAM.A = 255 (no SSE alpha).
        Dim rB = FoldSkinChannel(cr, iv)
        Dim gB = FoldSkinChannel(cg, iv)
        Dim bB = FoldSkinChannel(cbb, iv)
        If Logger.Enabled Then
            Dim skIdx = skin.Value.Index
            Dim wasAuthored = (authored IsNot Nothing)
            Dim crL = cr, cgL = cg, cbL = cbb, ivL = iv, rBL = rB, gBL = gB, bBL = bB
            Logger.LogLazy(Function() $"[SSE-QNAM] raceFid=0x{raceFid:X8} female={isFemale} skinLayerIdx={skIdx} authored={wasAuthored} TINC=({crL:F3},{cgL:F3},{cbL:F3}) TINV={ivL:F3} → QNAM=({rBL},{gBL},{bBL})")
        End If
        Return System.Drawing.Color.FromArgb(255, rB, gB, bB)
    End Function

    ''' <summary>True iff the RACE (for this gender) has a SKIN-TONE tint layer (TINP mask type == 6). The SSE
    ''' body-softlight guard uses this in place of FO4's slot-12 catalog check. SSE-only.</summary>
    Public Function RaceHasSkinToneLayer(pm As PluginManager, raceFid As UInteger, isFemale As Boolean) As Boolean
        If pm Is Nothing Then Return False
        Dim layers = GetRaceLayersOrdered(pm, raceFid, isFemale)
        If layers Is Nothing Then Return False
        For Each layer In layers
            If layer.MaskType = 6 Then Return True
        Next
        Return False
    End Function

    ''' <summary>Fold the skin-tone intensity into one colour channel: q = clamp(0.5 + tinv*(cNorm - 0.5), 0, 1),
    ''' returned as a 0..255 byte (round). cNorm is TINC/255 (0..1), tinv is the layer interp (0..1).</summary>
    Private Function FoldSkinChannel(cNorm As Double, tinv As Double) As Integer
        Dim q = 0.5 + tinv * (cNorm - 0.5)
        If q < 0.0 Then q = 0.0
        If q > 1.0 Then q = 1.0
        Return CInt(Math.Round(q * 255.0))
    End Function

    ''' <summary>Resolve a CLFM (Color Form) formID to linear RGB [0,1] from its CNAM (byte RGBA). Cached.
    ''' Returns white when the formID is 0 or unresolved.</summary>
    Private Function ResolveClfmColor(pm As PluginManager, clfmFid As UInteger) As Double()
        If clfmFid = 0 Then Return New Double() {1.0, 1.0, 1.0}
        Dim cached As Double() = Nothing
        If _clfmCache.TryGetValue(clfmFid, cached) Then Return cached
        Dim col = New Double() {1.0, 1.0, 1.0}
        Dim rec = pm.GetRecord(clfmFid)
        If rec IsNot Nothing AndAlso rec.Header.Signature = "CLFM" Then
            For Each sr In rec.Subrecords
                If sr.Signature = "CNAM" AndAlso sr.Data.Length >= 3 Then
                    col = New Double() {sr.Data(0) / 255.0, sr.Data(1) / 255.0, sr.Data(2) / 255.0}
                    Exit For
                End If
            Next
        End If
        _clfmCache(clfmFid) = col
        Return col
    End Function

    ''' <summary>One tint layer onto the accumulator, vía el compositor COMPARTIDO (FaceTintCpuCompositor.
    ''' ComposePixel) con la convención <paramref name="conv"/> de la ley SSE. coverage = convMask(mask[ch],
    ''' maskConv) × TINV, y el composite lo hace la ley (default SSE = lerp uniforme en linear, byte-idéntico al
    ''' modelo previo). El canal de máscara y la mask-conv salen de la ley — sin ramas por tipo hardcodeadas.</summary>
    Private Sub ComposeLayer(acc As Double(), mask As Double(), cR As Double, cG As Double, cB As Double, tinv As Double, npix As Integer,
                             conv As FaceTintConvention.FaceTintConventionSet, maskConv As Integer, maskCh As Integer,
                             Optional cov As Double() = Nothing)
        ' PARALELO por rangos: cada píxel toca sólo sus índices (acc/cov por i) ⇒ bit-idéntico al serial. El fold
        ' SSE compone a la resolución NATIVA del complexion (4096² con COtR), donde el serial era parte de los
        ' segundos por fold. El orden ENTRE capas (no conmutativo) lo preserva el caller (loop de capas serial).
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    Dim a = FaceTintCpuCompositor.ConvMaskShared(mask(i * 4 + maskCh), maskConv) * tinv   ' cobertura por la ley
                    If a <= 0.0 Then Continue For
                    acc(i * 4) = FaceTintCpuCompositor.ComposePixel(acc(i * 4), cR, a, conv)
                    acc(i * 4 + 1) = FaceTintCpuCompositor.ComposePixel(acc(i * 4 + 1), cG, a, conv)
                    acc(i * 4 + 2) = FaceTintCpuCompositor.ComposePixel(acc(i * 4 + 2), cB, a, conv)
                    If cov IsNot Nothing Then cov(i) = cov(i) + a * (1 - cov(i))   ' accumulate coverage
                Next
            End Sub)
    End Sub

    ''' <summary>Parse the RACE's per-gender tint layers IN ORDER (Male/Female Head Data, tracked by
    ''' MNAM/FNAM). Each Tint Layer: TINI index, TINT mask path, TINP mask type (optional), TIND default CLFM.
    ''' Returns the ordered list (= the cb2 slot order the engine composes). Cached per race+gender. TINP/TIND
    ''' are OPTIONAL (tattoo masks omit TINP) → flush on each new TINI so no-TINP masks still register.</summary>
    ''' <summary>Aplica el orden configurable SSE (<c>Setting_FaceTintSort_SSE.TintRules</c>, claves
    ''' <see cref="FaceTintSseTintSortKey"/>) sobre las capas del RACE que devuelve <see cref="GetRaceLayersOrdered"/>.
    ''' DEFAULT = <c>[Race_Order asc]</c> = IDENTIDAD ⇒ orden RaceMenu (posición en el RACE) ⇒ compose byte-idéntico.
    ''' El sort tiene tiebreak final por posición original (orden RACE), así claves iguales preservan RaceMenu. El
    ''' lerp NO es conmutativo ⇒ cualquier regla != default DESVÍA de RaceMenu (elección explícita del usuario).</summary>
    Public Function SortSseTintLayers(layers As List(Of SseTintMask), npcMap As Dictionary(Of Integer, Double())) As List(Of SseTintMask)
        If layers Is Nothing OrElse layers.Count <= 1 Then Return layers
        Dim cfg = Config_App.Current?.Setting_FaceTintSort_SSE
        Dim rules = If(cfg IsNot Nothing, cfg.TintRules, Nothing)
        If rules Is Nothing OrElse rules.Count = 0 Then Return layers
        Dim items As New List(Of (Layer As SseTintMask, Pos As Integer))
        For i = 0 To layers.Count - 1 : items.Add((layers(i), i)) : Next
        items.Sort(Function(a, b)
                       For Each r In rules
                           Dim c = SseTintKey(a.Layer, a.Pos, npcMap, r.Key).CompareTo(SseTintKey(b.Layer, b.Pos, npcMap, r.Key))
                           If r.Descending Then c = -c
                           If c <> 0 Then Return c
                       Next
                       Return a.Pos.CompareTo(b.Pos)   ' tiebreak estable = orden RACE (RaceMenu)
                   End Function)
        Return items.Select(Function(x) x.Layer).ToList()
    End Function

    Private Function SseTintKey(layer As SseTintMask, pos As Integer, npcMap As Dictionary(Of Integer, Double()), key As Integer) As Double
        Select Case CType(key, FaceTintSseTintSortKey)
            Case FaceTintSseTintSortKey.Tint_Index : Return layer.Index
            Case FaceTintSseTintSortKey.Mask_Type : Return layer.MaskType
            Case FaceTintSseTintSortKey.Authored : Return If(npcMap IsNot Nothing AndAlso npcMap.ContainsKey(layer.Index), 1.0, 0.0)
            Case FaceTintSseTintSortKey.Coverage
                Dim authored As Double() = Nothing
                If npcMap IsNot Nothing AndAlso npcMap.TryGetValue(layer.Index, authored) Then Return authored(3)
                Return layer.DefaultValue
            Case Else : Return pos   ' Race_Order (default) = posición en el RACE
        End Select
    End Function

    Public Function GetRaceLayersOrdered(pm As PluginManager, raceFid As UInteger, female As Boolean) As List(Of SseTintMask)
        Dim key = raceFid.ToString() & If(female, "F", "M")
        Dim cached As List(Of SseTintMask) = Nothing
        If _layersCache.TryGetValue(key, cached) Then Return cached
        Dim layers As New List(Of SseTintMask)
        Dim rr = pm.GetRecord(raceFid)
        If rr IsNot Nothing Then
            ' RACE tint layer: TINI, TINT, TINP, TIND(default preset formID), then a preset LIST of
            ' [TINC(CLFM formID), TINV(FLOAT 0-1), TIRS(idx)]×N. The default = the preset whose TINC==TIND;
            ' its TINV is the race-default coverage. (Gender: the head-data MNAM/FNAM markers precede the
            ' male/female TINI blocks — the earlier movement MNAM/FNAM are before any TINI so are harmless.)
            Dim inFemale = False, ci = -1, cp = "", ct = -1
            Dim cd As UInteger = 0
            ' preset list: [CLFM formID, TINV float, TIRS id] per preset (default value + TIAS→value lookup)
            Dim presets As New List(Of (Clfm As UInteger, Val As Double, Tirs As Integer))
            Dim lastClfm As UInteger = 0, lastVal As Double = 0
            Dim flush = Sub()
                            If female = inFemale AndAlso ci >= 0 AndAlso cp <> "" Then
                                ' default preset = the one whose CLFM == TIND; its TINV = default coverage (engine-
                                ' verified: 0xFCB3F2/0xFE52CA find the TIND-matching preset; ColorAverage@0 = off).
                                Dim dval As Double = 0
                                For Each pr In presets
                                    If pr.Clfm = cd Then dval = pr.Val : Exit For
                                Next
                                ' Snapshot the layer's preset swatches (TIRS→CLFM/value) for the editor's dropdown. Same
                                ' list the default-value lookup above reads; copied so the shared 'presets' can be reused.
                                Dim presetSnap As New List(Of SseTintPreset)(presets.Count)
                                For Each pr In presets
                                    presetSnap.Add(New SseTintPreset With {.Tirs = pr.Tirs, .Clfm = pr.Clfm, .Value = pr.Val})
                                Next
                                layers.Add(New SseTintMask With {.Index = ci, .Path = cp, .MaskType = ct, .DefaultClfm = cd, .DefaultValue = dval, .Presets = presetSnap})
                            End If
                        End Sub
            For Each sr In rr.Subrecords
                Select Case sr.Signature
                    Case "MNAM" : flush() : inFemale = False : ci = -1 : cp = "" : ct = -1 : cd = 0 : presets.Clear()
                    Case "FNAM" : flush() : inFemale = True : ci = -1 : cp = "" : ct = -1 : cd = 0 : presets.Clear()
                    Case "TINI" : flush() : ci = BitConverter.ToUInt16(sr.Data, 0) : cp = "" : ct = -1 : cd = 0 : presets.Clear()
                    Case "TINT" : cp = sr.AsStringGeneral
                    Case "TINP" : ct = BitConverter.ToUInt16(sr.Data, 0)
                    Case "TIND" : If sr.Data.Length >= 4 Then cd = BitConverter.ToUInt32(sr.Data, 0)
                    Case "TINC" : If sr.Data.Length >= 4 Then lastClfm = BitConverter.ToUInt32(sr.Data, 0)  ' RACE preset: CLFM formID
                    Case "TINV" : If sr.Data.Length >= 4 Then lastVal = BitConverter.ToSingle(sr.Data, 0)   ' RACE TINV = FLOAT 0-1
                    Case "TIRS" : If sr.Data.Length >= 2 Then presets.Add((lastClfm, lastVal, BitConverter.ToUInt16(sr.Data, 0)))
                End Select
            Next
            flush()
        End If
        _layersCache(key) = layers
        Return layers
    End Function

    ''' <summary>Decode a texture (FilesDictionary key) to linear RGBA[0,1] at exactly W×H (bilinear). Public
    ''' wrapper over <see cref="DecodeMask"/> so other SSE compositors (overlays into the per-NPC diffuse) reuse
    ''' the SAME decode+resize+cache path. Nothing when the file is missing/undecodable.</summary>
    Public Function DecodeTextureRgba(texPath As String, w As Integer, h As Integer) As Double()
        Return DecodeMask(texPath, w, h)
    End Function

    ''' <summary>Decode a mask texture (FilesDictionary key) to linear RGBA[0,1] at exactly W×H (bilinear).
    ''' Cached at 512². Nothing when the file is missing/undecodable.</summary>
    Private Function DecodeMask(texPath As String, w As Integer, h As Integer) As Double()
        Dim key = texPath.Replace("/"c, "\"c).ToLowerInvariant()
        If Not key.StartsWith("textures\") Then key = "textures\" & key
        Dim cached As Double() = Nothing
        If w = 512 AndAlso h = 512 AndAlso _texCache.TryGetValue(key, cached) Then Return cached
        ' Fuente decodificada, cacheada por (path, target) — ver _texSrcCache. La elección de mip de DecodeDds
        ' depende del target ⇒ el target integra la key. El miss (Nothing) también se cachea (archivo ausente).
        ' Fuentes grandes (> 1024² tras elegir mip) no se retienen: 32 MB de Double por entrada es el techo.
        Dim srcKey = $"{key}|{w}x{h}"
        Dim t As FaceTintCpuCompositor.DecodedTex = Nothing
        If Not _texSrcCache.TryGetValue(srcKey, t) Then
            Dim b = FilesDictionary_class.GetBytes(key)
            t = If(b Is Nothing, Nothing, FaceTintCpuCompositor.DecodeDds(b, w, h))
            If t IsNot Nothing AndAlso t.Rgba Is Nothing Then t = Nothing
            If t Is Nothing OrElse t.Rgba.Length <= 1024 * 1024 * 4 Then _texSrcCache(srcKey) = t
        End If
        If t Is Nothing Then
            If w = 512 AndAlso h = 512 Then _texCache(key) = Nothing
            Return Nothing
        End If
        Dim outp(w * h * 4 - 1) As Double
        ' Resample bilineal PARALELO por filas (misma fórmula, cada fila escribe sólo sus índices ⇒ bit-idéntico).
        ' A 4096² de target el serial era parte de los segundos por fold.
        System.Threading.Tasks.Parallel.For(0, h, Sub(y)
                                                      Dim fy = (y + 0.5) * t.Height / h - 0.5
                                                      Dim y0 = Math.Max(0, Math.Min(t.Height - 1, CInt(Math.Floor(fy)))) : Dim y1 = Math.Min(t.Height - 1, y0 + 1) : Dim ty = fy - Math.Floor(fy)
                                                      For x = 0 To w - 1
                                                          Dim fx = (x + 0.5) * t.Width / w - 0.5
                                                          Dim x0 = Math.Max(0, Math.Min(t.Width - 1, CInt(Math.Floor(fx)))) : Dim x1 = Math.Min(t.Width - 1, x0 + 1) : Dim tx = fx - Math.Floor(fx)
                                                          For c = 0 To 3
                                                              Dim p00 = t.Rgba((y0 * t.Width + x0) * 4 + c), p10 = t.Rgba((y0 * t.Width + x1) * 4 + c)
                                                              Dim p01 = t.Rgba((y1 * t.Width + x0) * 4 + c), p11 = t.Rgba((y1 * t.Width + x1) * 4 + c)
                                                              outp((y * w + x) * 4 + c) = (p00 * (1 - tx) + p10 * tx) * (1 - ty) + (p01 * (1 - tx) + p11 * tx) * ty
                                                          Next
                                                      Next
                                                  End Sub)
        If w = 512 AndAlso h = 512 Then _texCache(key) = outp
        Return outp
    End Function

End Module
