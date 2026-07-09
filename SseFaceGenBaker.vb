Imports System.Runtime.CompilerServices

''' <summary>
''' SSE (Skyrim Special Edition) FaceGen BAKE — the single source of truth for producing the two vanilla
''' facegen artifacts the engine consumes, mirroring FO4's bake seam (FaceTintInputBuilder + FaceGenBuilder):
'''  (1) the FaceTint <c>_d.dds</c> (512² BC3, tint-only) — <see cref="BakeFaceTintDds"/>, and
'''  (2) the FaceGeom <c>.nif</c> head texture set (complexion FTST slots + facetint slot) — see the NIF bake.
'''
''' ENGINE-VERIFIED (re_sseck disasm of the CK bake builder @0x18C9F40 + DXBC of the tint pixel shader):
'''  - The facetint _d is TINT-ONLY: base 0.5 + per-layer uniform lerp(acc, TINC, maskR×TINV/100), RACE order.
'''    It does NOT include the complexion — proven: FaceGeom NIF slot[6]=facetint, while the complexion (FTST
'''    TX00/01/03/04/07) is written to head slots [0,1,2,3,7] and combined at RENDER, not in the _d.
'''  - So the bake has TWO products (as FO4): the _d overlay AND the NIF whose head texture set references the
'''    NPC's FTST complexion in [0,1,2,3,7] plus the facetint in [6].
'''
''' SSE-ONLY. Callers gate on <c>Config_App.Current.Game = Config_App.Game_Enum.Skyrim</c>; the FO4 path stays
''' byte-identical. See project_sse_facetint_spec / project_sse_nam9_morph_map.
''' </summary>
Public Module SseFaceGenBaker

    ''' <summary>Bake the SSE FaceTint <c>_d.dds</c> for an NPC: compose the tint (engine-exact, tint-only) and
    ''' encode to 512² BC3 (DXT5) with mips — the exact format CK writes to
    ''' <c>FaceGenData\FaceTint\&lt;plugin&gt;\&lt;fid&gt;.dds</c>. Returns Nothing when the tint can't be
    ''' composed (race/QNAM unresolved). Pure — no file writes; the caller writes/uploads the bytes.</summary>
    Public Function BakeFaceTintDds(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                    raceFormID As UInteger, isFemale As Boolean,
                                    Optional w As Integer = 512, Optional h As Integer = 512,
                                    Optional overlays As IList(Of SseOverlayCompositor.SseOverlay) = Nothing,
                                    Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                    Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As Byte()
        Dim acc = SseFaceTintComposer.ComposeLinearRgba(pm, npcRec, race, raceFormID, isFemale, w, h, Nothing, npcTintOverride, tintTexOverride)
        If acc Is Nothing Then Return Nothing
        ' RaceMenu / NiOverride overlays ON TOP (engine-exact blend; reference_racemenu_overlay_blend). No-op
        ' for vanilla NPCs (no overlays) but always in the pipeline so modded NPCs bake WYSIWYG.
        SseOverlayCompositor.ApplyOverlays(acc, overlays, w, h)
        Return EncodeLinearRgbaToBc3(acc, w, h)
    End Function

    ''' <summary>Encode a linear RGBA buffer ([0,1], length w*h*4) to BC3 (DXT5) DDS bytes with mips — the
    ''' facetint _d format. BGRA byte order + BC3 match CK's output (round-trip validated ≈ DXT5 floor).</summary>
    Public Function EncodeLinearRgbaToBc3(acc As Double(), w As Integer, h As Integer) As Byte()
        Dim bgra(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            bgra(i * 4) = ClampByte(acc(i * 4 + 2))       ' B
            bgra(i * 4 + 1) = ClampByte(acc(i * 4 + 1))   ' G
            bgra(i * 4 + 2) = ClampByte(acc(i * 4))       ' R
            bgra(i * 4 + 3) = 255                          ' A
        Next
        Return DirectXTextureConversionHelper.Bgra32BytesToDdsBytes(w, h, bgra, DirectXTextureConversionHelper.DxgiFormatBc3Unorm, generateMipMaps:=True)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ClampByte(v As Double) As Byte
        Return CByte(Math.Max(0.0, Math.Min(255.0, Math.Round(v * 255.0))))
    End Function

End Module
