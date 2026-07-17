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
    ''' <param name="dxgiFormat">Formato de salida. -1 = BC3 (el del facetint vanilla). ⛔ NO hardcodear en el caller:
    ''' pasar el elegido por el usuario (CharGen Options → Diffuse) para que el facetint REAL siga la misma opción que
    ''' el resto de los artefactos del bake (el neutral del fold ya la seguía; antes esto forzaba BC3 y quedaban con
    ''' formatos distintos según el NPC estuviera plegado o no).</param>
    Public Function BakeFaceTintDds(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                    raceFormID As UInteger, isFemale As Boolean,
                                    Optional w As Integer = 512, Optional h As Integer = 512,
                                    Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                    Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing,
                                    Optional dxgiFormat As Integer = -1) As Byte()
        Dim acc = ComposeFacetintAcc(pm, npcRec, race, raceFormID, isFemale, w, h, npcTintOverride, tintTexOverride)
        If acc Is Nothing Then Return Nothing
        Return EncodeLinearRgbaToBc3(acc, w, h, dxgiFormat)
    End Function

    ''' <summary>Compose the SSE facetint linear RGBA accumulator (tint + RaceMenu overlays), the buffer both the
    ''' DDS encode and the TGA dump derive from. Same inputs as <see cref="BakeFaceTintDds"/>. Nothing on fail.
    ''' Public so the bake can dump a lossless TGA (via <see cref="LinearRgbaToBgra"/>) without a second compose.</summary>
    ''' <summary>⛔ El facetint es TINT-ONLY por construcción: NO lleva overlays ni skee-masks. Los overlays de
    ''' RaceMenu y las máscaras skee (MASKT) se componen sobre el DIFFUSE (en el fold, ver
    ''' <c>FaceGenBuilder.WriteSseFaceDiffuseWithOverlays</c>), no acá — porque el engine las aplica sobre el ALBEDO
    ''' ya tintado, y el albedo sólo existe después de plegar. El parámetro <c>overlays</c> que esta función tenía
    ''' (y que <see cref="BakeFaceTintDds"/> le pasaba) llegaba SIEMPRE Nothing: era código muerto que sugería lo
    ''' contrario del modelo. Eliminado.</summary>
    Public Function ComposeFacetintAcc(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                       raceFormID As UInteger, isFemale As Boolean,
                                       Optional w As Integer = 512, Optional h As Integer = 512,
                                       Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                       Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As Double()
        Return SseFaceTintComposer.ComposeLinearRgba(pm, npcRec, race, raceFormID, isFemale, w, h, Nothing, npcTintOverride, tintTexOverride)
    End Function

    ''' <summary>Convert a linear RGBA accumulator ([0,1], length w*h*4) to BGRA bytes (opaque alpha) — the same
    ''' byte order <see cref="EncodeLinearRgbaToBc3"/> feeds the encoder, for a lossless TGA dump.</summary>
    Public Function LinearRgbaToBgra(acc As Double(), w As Integer, h As Integer) As Byte()
        If acc Is Nothing OrElse acc.Length < w * h * 4 Then Return Nothing
        Dim bgra(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            bgra(i * 4) = ClampByte(acc(i * 4 + 2))       ' B
            bgra(i * 4 + 1) = ClampByte(acc(i * 4 + 1))   ' G
            bgra(i * 4 + 2) = ClampByte(acc(i * 4))       ' R
            bgra(i * 4 + 3) = 255                          ' A
        Next
        Return bgra
    End Function

    ''' <summary>Encode a linear RGBA buffer ([0,1], length w*h*4) to DDS bytes with mips. Default format = BC3
    ''' (DXT5), el formato del facetint: BGRA byte order + BC3 = lo que escribe el CK (round-trip validado ≈ piso
    ''' del DXT5) y lo que trae el vanilla (medido: los 3.158 facetint del BSA son DXT5 512² 9 mips).
    ''' <paramref name="dxgiFormat"/> permite seguir el formato elegido por el usuario (CharGen Options → Diffuse)
    ''' en vez de hardcodear; -1 = BC3.</summary>
    Public Function EncodeLinearRgbaToBc3(acc As Double(), w As Integer, h As Integer,
                                          Optional dxgiFormat As Integer = -1) As Byte()
        Dim bgra(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            bgra(i * 4) = ClampByte(acc(i * 4 + 2))       ' B
            bgra(i * 4 + 1) = ClampByte(acc(i * 4 + 1))   ' G
            bgra(i * 4 + 2) = ClampByte(acc(i * 4))       ' R
            bgra(i * 4 + 3) = 255                          ' A
        Next
        Dim fmt = If(dxgiFormat >= 0, dxgiFormat, DirectXTextureConversionHelper.DxgiFormatBc3Unorm)
        Return DirectXTextureConversionHelper.Bgra32BytesToDdsBytes(w, h, bgra, fmt, generateMipMaps:=True)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ClampByte(v As Double) As Byte
        Return CByte(Math.Max(0.0, Math.Min(255.0, Math.Round(v * 255.0))))
    End Function

    ' === Op engine facetint→albedo (Shader_Class.vb:1876-1880, de sse_facegen_skin.asm 71-79, VERIFICADO) ===
    ' El engine NO multiplica el facetint _d crudo: lo AMPLIFICA. fgTint = (_d + (1/255,0,1/255)) * 255/64, y
    ' albedo *= fgTint. ÚNICA fuente de la op para render Y bake (WYSIWYG): ambos pliegan igual por construcción.
    Public Const FgTintAmp As Double = 255.0 / 64.0            ' = 3.984375
    Public Const FgTintOffR As Double = 1.0 / 255.0
    Public Const FgTintOffG As Double = 0.0
    Public Const FgTintOffB As Double = 1.0 / 255.0

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function FgOff(ch As Integer) As Double
        Return If(ch = 1, FgTintOffG, If(ch = 2, FgTintOffB, FgTintOffR))
    End Function

    ''' <summary>fgTint de UN canal (0=R,1=G,2=B) del facetint _d lineal [0,1] → el multiplicador que el engine
    ''' aplica al albedo. Verificado: (v+off)·(255/64). El _d NEUTRAL (fgTint=1) es (63,64,63)/255.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function FgTintChannel(dChannel As Double, ch As Integer) As Double
        Return (dChannel + FgOff(ch)) * FgTintAmp
    End Function

    ''' <summary>Valor lineal del _d que NEUTRALIZA el facetint (fgTint=1) por canal — para un slot 6 no-op cuando
    ''' el facetint se pliega en el diffuse. (v+off)·(255/64)=1 ⇒ v = 64/255 − off. R,B=63/255; G=64/255.</summary>
    Public Function FacetintNeutralChannel(ch As Integer) As Double
        Return 64.0 / 255.0 - FgOff(ch)
    End Function

    ''' <summary>Un facetint _d NEUTRAL (fgTint=1) — para el slot 6 cuando el facetint se pliega en el diffuse.
    ''' Todos los píxeles = (63,64,63)/255. El engine hace albedo *= fgTint(neutral) = albedo (no-op). Formato:
    ''' el que pase el caller (CharGen Options → Diffuse); -1 = BC3 (default, = vanilla). Al ser un color CONSTANTE
    ''' el formato no cambia el resultado (BC3 codifica un bloque uniforme sin error), pero el archivo sigue al
    ''' setting igual que el resto del bake en vez de hardcodear.</summary>
    Public Function NeutralFacetintDds(w As Integer, h As Integer, Optional dxgiFormat As Integer = -1) As Byte()
        Dim npix = w * h
        Dim acc(npix * 4 - 1) As Double
        Dim nR = FacetintNeutralChannel(0), nG = FacetintNeutralChannel(1), nB = FacetintNeutralChannel(2)
        For i = 0 To npix - 1
            acc(i * 4) = nR : acc(i * 4 + 1) = nG : acc(i * 4 + 2) = nB : acc(i * 4 + 3) = 1.0
        Next
        Return EncodeLinearRgbaToBc3(acc, w, h, dxgiFormat)
    End Function

    ''' <summary>Un detail map (slot 3 / DisplacementTexture) NEUTRAL para el softlight del engine:
    ''' <c>softlight(diffuse, detail)</c> con detail = 0.5 es la IDENTIDAD (<c>a² + 2·a·0.5·(1−a) = a</c>). Se usa
    ''' cuando el facetint se pliega en el diffuse (el softlight con el detail REAL ya está horneado en slot 0), para
    ''' que el engine NO lo re-aplique. ⛔ NO se puede VACIAR el slot 3: el engine rellena un slot detail vacío con su
    ''' default <c>BSShader_DefFacegenDetail</c> = una textura UNIFORME <c>0x40 = 64/255 = 0.251</c> (RE byte-level
    ''' SkyrimSE.exe: la init @0x140E57E30 la rellena con <c>0x40404040</c>; = vanilla blankdetailmap.dds. ⚠️ NO es
    ''' la Bayer 8×8 media 0.1235 — esa es <c>BSShader_DitheringNoise</c>, otra textura). 0.251 &lt; 0.5 ⇒ oscurece
    ''' la cara. El detail se samplea CRUDO (raw), así que 0.5 = byte 128 literal. Constante ⇒ compartible por plugin;
    ''' el engine SÍ respeta el slot 3 del NIF (a diferencia del tint, que
    ''' arma por path canónico). Formato = el que pase el caller; -1 = BC3 (constante ⇒ sin error de compresión).</summary>
    Public Function NeutralDetailDds(w As Integer, h As Integer, Optional dxgiFormat As Integer = -1) As Byte()
        Dim npix = w * h
        Dim acc(npix * 4 - 1) As Double
        For i = 0 To npix - 1
            acc(i * 4) = 0.5 : acc(i * 4 + 1) = 0.5 : acc(i * 4 + 2) = 0.5 : acc(i * 4 + 3) = 1.0
        Next
        Return EncodeLinearRgbaToBc3(acc, w, h, dxgiFormat)
    End Function

    ''' <summary>Pliega el facetint _d DENTRO del complexion (in place): reproduce la op del engine
    ''' <c>albedo_linear *= fgTint</c>. ⚠️ El engine multiplica en LINEAR: el complexion (slot 0) es un diffuse sRGB
    ''' que el shader decodifica sRGB→linear ANTES de multiplicar por fgTint. Como el <paramref name="complexionRgba"/>
    ''' llega CRUDO (sRGB, de DecodeDds), acá se hace sRGB→linear, ×fgTint, y linear→sRGB para volver a almacenarlo
    ''' como diffuse (el engine lo re-samplea sRGB→linear). MEDIDO: plegar en sRGB crudo salía ~0.33 MÁS CLARO (bug).
    ''' fgTint usa el _d CRUDO (slot 6 se samplea sin sRGB). RGB; alpha intacto. Ambos buffers [0,1] w*h*4, mismo tamaño.</summary>
    Public Sub FoldFacetintIntoDiffuse(complexionRgba As Double(), facetintRgba As Double(), npix As Integer,
                                       Optional detailRgba As Double() = Nothing)
        If complexionRgba Is Nothing OrElse facetintRgba Is Nothing Then Return
        ' Engine EXACTO (Shader_Class 1864→1878): albedo = fgTint × softlight(sRGBtoLin(complexion), detail). El
        ' softlight con el detail (slot 3) va ANTES del fgTint. detailRgba = detail CRUDO (no está en color textures →
        ' se samplea raw). ⛔ Nothing (slot 3 vacío) NO es identidad: el motor bindea su default interno
        ' BSShader_DefFacegenDetail = 0.251 (RE byte-level SkyrimSE.exe 0x140E57E30 = uniforme 0x40 = vanilla
        ' blankdetailmap; NO la Bayer 0.1235 de BSShader_DitheringNoise, NO 0.5). Se pliega ese 0.251 para
        ' matchear al motor (mods que borran el TX04 del TXST, ej. Enhanced Khajiit). El caller DEBE neutralizar
        ' el slot 3 del NIF a 0.5 (si no, el engine re-aplica el softlight encima del _2c).
        Const emptyDetailDefault As Double = 64.0 / 255.0   ' BSShader_DefFacegenDetail (0.251)
        ' PARALELO por rangos de píxeles: cada píxel lee/escribe SOLO sus propios índices (sin estado compartido,
        ' sin acumulación cruzada) ⇒ resultado BIT-IDÉNTICO al loop serial (el mismo double-math por píxel; sólo
        ' cambia qué thread lo ejecuta). Por qué: la op lleva 2 Math.Pow por canal (Srgb2Lin+Lin2Srgb) y el fold
        ' corre a la resolución NATIVA del complexion — a 4096² (caras COtR) el serial costaba segundos por fold.
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    For ch = 0 To 2
                        Dim clin = Srgb2Lin(complexionRgba(i * 4 + ch))
                        Dim b = If(detailRgba IsNot Nothing, detailRgba(i * 4 + ch), emptyDetailDefault)
                        Dim sl = clin * clin + 2.0 * clin * b * (1.0 - clin)          ' softlight(complexion_lin, detail)
                        complexionRgba(i * 4 + ch) = Lin2Srgb(sl * FgTintChannel(facetintRgba(i * 4 + ch), ch))
                    Next
                Next
            End Sub)
    End Sub

    ''' <summary>sRGB→linear por canal (curva estándar IEC 61966-2-1). Para plegar el albedo en linear como el engine.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Srgb2Lin(c As Double) As Double
        If c <= 0.04045 Then Return c / 12.92
        Return Math.Pow((c + 0.055) / 1.055, 2.4)
    End Function

    ''' <summary>linear→sRGB por canal (curva estándar), clamp [0,1].</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Lin2Srgb(c As Double) As Double
        If c <= 0.0 Then Return 0.0
        If c >= 1.0 Then Return 1.0
        If c <= 0.0031308 Then Return c * 12.92
        Return 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055
    End Function

End Module
