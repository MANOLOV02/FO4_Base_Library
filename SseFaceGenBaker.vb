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
                                       Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As Single()
        Return SseFaceTintComposer.ComposeLinearRgba(pm, npcRec, race, raceFormID, isFemale, w, h, Nothing, npcTintOverride, tintTexOverride)
    End Function

    ''' <summary>Convert a linear RGBA accumulator ([0,1], length w*h*4) to BGRA bytes (opaque alpha) — the same
    ''' byte order <see cref="EncodeLinearRgbaToBc3"/> feeds the encoder, for a lossless TGA dump.</summary>
    Public Function LinearRgbaToBgra(acc As Single(), w As Integer, h As Integer) As Byte()
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
    Public Function EncodeLinearRgbaToBc3(acc As Single(), w As Integer, h As Integer,
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

    ' === LEY DEL ENGINE para el albedo facegen SSE (Shader_Class.vb, DXBC + RE SkyrimSE.exe, VERIFICADO) ===
    '     albedo = softlight(diffuse, TINT) * ((DETAIL + (1/255,0,1/255)) * 255/64)
    ' TINT   = texture-set slot 6 (facetint) -> material+0xA0 -> PS t3   (entra por SOFT-LIGHT)
    ' DETAIL = texture-set slot 3            -> material+0xA8 -> PS t4   (entra por el AMPLIFY de abajo)
    ' ⛔ CORRIGE la premisa previa "el engine AMPLIFICA el facetint y lo multiplica": el x255/64 normaliza el
    ' DETAIL (neutro 64 -> 1.0 exacto), NO el facetint. Con el tint pasando por el amplify, un skin tone
    ' saturado aplastaba R/B y el cuello salía mucho más saturado que el pecho (in-game matchean).
    ' Ver SetupMaterial 0x1414DC310 / rama facegen 0x1414DC542 / OnLoadTextureSet 0x1414BA6E0.
    ' ÚNICA fuente de la op para render Y bake (WYSIWYG): ambos pliegan igual por construcción.
    Public Const FgTintAmp As Double = 255.0 / 64.0            ' = 3.984375
    Public Const FgTintOffR As Double = 1.0 / 255.0
    Public Const FgTintOffG As Double = 0.0
    Public Const FgTintOffB As Double = 1.0 / 255.0

    ''' <summary>Default del engine para el slot 3 (DETAIL) cuando está VACÍO: <c>BSShader_DefFacegenDetail</c>,
    ''' textura UNIFORME <c>0x40</c> = 64/255 = 0.251. RE byte-level SkyrimSE.exe: la init de defaults
    ''' @0x140E57E30 la crea con fill <c>0x40404040</c> y la guarda en manager+0x88 (singleton 0x328CC20 ⇒
    ''' 0x328CCA8 = el default que el material facegen mete en +0xA8 @0x1414BA8B0). = vanilla blankdetailmap.dds.
    ''' ⚠️ NO es la Bayer 8×8 media 0.1235: esa es <c>BSShader_DitheringNoise</c>, creada en la MISMA función
    ''' unas instrucciones antes (por eso la nota vieja citaba 0x140E57E30 para el 0x40 y era ambigua).</summary>
    Public Const EngineDefaultDetail As Double = 64.0 / 255.0

    ''' <summary>Default del engine para el slot 6 (TINT) cuando no hay facetint: <c>DefaultGreyMap</c>, uniforme
    ''' <c>0x80</c> = 128/255 = 0.50196. RE: misma init @0x140E57E30 (fill <c>0x80808080</c>), manager+0x70 =
    ''' 0x328CC90 = el default que el material facegen mete en +0xA0. Es (casi) la IDENTIDAD del soft-light:
    ''' con b = 0.5 EXACTO <c>a² + 2·a·0.5·(1−a) = a</c>, pero a 8 bits el valor representable es 128/255, y el
    ''' residuo queda acotado por <c>|softlight(a,128/255) − a| = 2·a·(1−a)·(1/510) ≤ 0.00098</c> (&lt; 1/4 de byte).
    ''' ⭐ Se usa el valor BYTE-EXACTO, no 0.5: es lo que hace el motor, y es también lo único que sobrevive a un
    ''' DDS de 8 bits — así el neutro que escribe el bake, el que instala el preview y el default del engine son
    ''' EL MISMO número en los cuatro caminos.</summary>
    Public Const EngineDefaultTint As Double = 128.0 / 255.0

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function FgOff(ch As Integer) As Double
        Return If(ch = 1, FgTintOffG, If(ch = 2, FgTintOffB, FgTintOffR))
    End Function

    ''' <summary>El multiplicador amplificado de UN canal (0=R,1=G,2=B) a partir del DETAIL crudo [0,1]:
    ''' <c>(v+off)·(255/64)</c>. ⚠️ Se aplica al DETAIL (slot 3 → t4), NO al facetint. El detail NEUTRAL
    ''' (multiplicador = 1) es (63,64,63)/255; el default del engine 0.251 da (1.015625, 1.0, 1.015625).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function FgTintChannel(dChannel As Double, ch As Integer) As Double
        Return (dChannel + FgOff(ch)) * FgTintAmp
    End Function

    ''' <summary>⭐⭐ PISO del multiplicador del amplify, COMPARTIDO por el fold y por su inversa.
    '''
    ''' <para>⛔ EL BUG QUE ESTO ARREGLA: el piso existía SÓLO en la inversa
    ''' (<see cref="PreCompensateEngineChain"/> acotaba con <c>MinAmp = 0.25</c> "para que un detail
    ''' patológicamente oscuro no dispare el brillo") y NO en <see cref="FoldFacetintIntoDiffuse"/>. Con un detail
    ''' oscuro —<c>detail &lt; 0,0587</c> ⇒ <c>amp &lt; 0,25</c>— el fold MULTIPLICABA por el amp real y la inversa
    ''' DIVIDÍA por 0,25: la cadena dejaba de cancelar y el diffuse horneado quedaba mal en esos píxeles. Una
    ''' inversa que no usa el mismo número que la directa no es una inversa. El mismo desbalance estaba en el
    ''' shader (rama <c>uFgTintFold==2</c> con <c>max(..., 0.25)</c> y la <c>==1</c> sin piso).</para>
    '''
    ''' <para>⚠️ Poner el piso en LOS DOS lados cambia el resultado SÓLO donde antes la cadena ya estaba rota
    ''' (amp &lt; 0,25). Donde amp ≥ 0,25 —todo el corpus normal, incluido el default del engine 0,251 ⇒ amp ≈ 1,0156—
    ''' es byte-inerte.</para></summary>
    Public Const FgAmpFloor As Double = 0.25

    ''' <summary>El multiplicador del amplify de un canal, YA ACOTADO por <see cref="FgAmpFloor"/>. Es la ÚNICA
    ''' función que deben usar el fold y la inversa, para que no puedan volver a divergir.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function FgTintChannelClamped(dChannel As Double, ch As Integer) As Double
        Dim a = FgTintChannel(dChannel, ch)
        Return If(a < FgAmpFloor, FgAmpFloor, a)
    End Function

    ''' <summary>Valor crudo del DETAIL que hace el amplify IDENTIDAD (multiplicador = 1) por canal — para dejar el
    ''' slot 3 no-op cuando la cadena ya se plegó en el diffuse. (v+off)·(255/64)=1 ⇒ v = 64/255 − off.
    ''' R,B=63/255; G=64/255.</summary>
    Public Function DetailNeutralChannel(ch As Integer) As Double
        Return 64.0 / 255.0 - FgOff(ch)
    End Function

    ''' <summary>Un facetint _d NEUTRAL para el slot 6 cuando la cadena se pliega en el diffuse: gris <b>128</b>
    ''' uniforme = IDENTIDAD del SOFT-LIGHT a 8 bits (ver <see cref="EngineDefaultTint"/>: residuo ≤ 0.00098, el
    ''' mismo que tiene el motor con su DefaultGreyMap). ⚠️ NO es (63,64,63): ese es el neutro
    ''' del AMPLIFY y le corresponde al slot 3 (<see cref="DetailNeutralChannel"/>). Coincide además con el default de
    ''' engine del propio slot (<c>DefaultGreyMap</c>), así que sirve igual si el slot queda vacío. El tint se
    ''' samplea CRUDO (raw), así que 0.5 = byte 128 literal. Formato: el que pase el caller (CharGen Options →
    ''' Diffuse); -1 = BC3 (default, = vanilla). Al ser un color CONSTANTE el formato no cambia el resultado (BC3
    ''' codifica un bloque uniforme sin error), pero el archivo sigue al setting en vez de hardcodear.</summary>
        ' (Eliminada NeutralFacetintDds: el fold YA NO neutraliza el slot 6 — conserva el facetint REAL y la
    '  cadena del engine se cancela con PreCompensateEngineChain.)

    ''' <summary>Un detail map (slot 3 / DisplacementTexture) NEUTRAL para el AMPLIFY del engine:
    ''' <c>(v+off)·255/64 = 1</c> ⇒ v = (63,64,63)/255. Se usa cuando la cadena se pliega en el diffuse (el amplify
    ''' con el detail REAL ya está horneado en slot 0), para que el engine NO lo re-aplique. ⚠️ NO es 0.5: 0.5 es la
    ''' identidad del SOFT-LIGHT y le corresponde al slot 6 (que el fold ya no neutraliza).
    ''' ⛔ NO se puede VACIAR el slot 3: el engine lo rellena con <see cref="EngineDefaultDetail"/> (0.251), que
    ''' amplificado da (1.015625, 1.0, 1.015625) ≠ 1 ⇒ la cara saldría 1.5% más clara en R/B. El detail se samplea
    ''' CRUDO (raw). Constante ⇒ compartible por plugin; el engine SÍ respeta el slot 3 del NIF (a diferencia del
    ''' tint, que arma por path canónico). Formato = el que pase el caller; -1 = BC3 (constante ⇒ sin error).</summary>
    ' (Eliminada NeutralDetailDds: el fold ya no neutraliza el slot 3 — deja el detail REAL y pre-compensa el
    '  amplify en el diffuse. Ver PreCompensateDetailAmplify. DetailNeutralChannel SIGUE: documenta el valor de
    '  identidad del amplify y lo verifica el probe NpcSseRoundtripProbe.)

    ''' <summary>Pliega la cadena de albedo facegen DENTRO del complexion (in place): reproduce la op del engine
    ''' <c>albedo_lin = softlight(complexion_lin, TINT) × ((DETAIL + off)·255/64)</c>.
    ''' <para>⚠️ El resultado de ESTA función es la BASE sobre la que van los overlays (sin teñir): ése es el
    ''' orden de RaceMenu y es lo que el preview muestra. Para que el juego muestre lo MISMO, el caller debe
    ''' después llamar a <see cref="PreCompensateDetailAmplify"/> — ver la nota ahí.</para></summary>
    ''' ⚠️ El engine opera en LINEAR: el complexion (slot 0) es un diffuse sRGB que el shader decodifica sRGB→linear
    ''' ANTES de la cadena. Como el <paramref name="complexionRgba"/> llega CRUDO (sRGB, de DecodeDds), acá se hace
    ''' sRGB→linear, la cadena, y linear→sRGB para volver a almacenarlo como diffuse (el engine lo re-samplea
    ''' sRGB→linear). MEDIDO: plegar en sRGB crudo salía ~0.33 MÁS CLARO (bug).
    ''' <paramref name="facetintRgba"/> (slot 6) y <paramref name="detailRgba"/> (slot 3) se samplean CRUDOS (raw).
    ''' RGB; alpha intacto. Buffers [0,1] w*h*4, mismo tamaño.
    ''' ⭐ RÉPLICA EXACTA de la rama <c>uFgTintFold</c> del shader del compositor (fold GPU) — si tocás una, tocá la
    ''' otra: el sandbox _2c-vs-_2d mide esa paridad.</summary>
    Public Sub FoldFacetintIntoDiffuse(complexionRgba As Single(), facetintRgba As Single(), npix As Integer,
                                       Optional detailRgba As Single() = Nothing)
        If complexionRgba Is Nothing OrElse facetintRgba Is Nothing Then Return
        ' Engine EXACTO: albedo = softlight(sRGBtoLin(complexion), facetint) × amplify(detail).
        ' ⛔ CORREGIDO: antes esto estaba INVERTIDO (softlight con el detail y amplify sobre el facetint). El
        ' x255/64 normaliza el DETAIL, no el tint. Ver el bloque de la ley arriba.
        ' Slot vacío ⇒ default del engine, NO identidad arbitraria:
        '   detail  vacío -> EngineDefaultDetail 0.251 -> amplify (1.015625, 1.0, 1.015625)
        '   facetint vacío -> EngineDefaultTint  0.5    -> softlight identidad
        ' (mods que borran el TX04 del TXST, ej. Enhanced Khajiit, caen en el primero).
        ' ⛔ ESTA NOTA DECÍA: "El caller DEBE neutralizar los slots del NIF: slot 3 -> (63,64,63), slot 6 -> 0.5
        ' (si no, el engine re-aplica encima del plegado)". YA NO — y hacerlo hoy ROMPERÍA el resultado: los dos
        ' slots quedan con su contenido REAL y el caller cancela la cadena del motor con PreCompensateEngineChain.
        ' Ningún caller neutraliza nada.
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
                        Dim tint = facetintRgba(i * 4 + ch)                                              ' slot 6 -> t3
                        Dim det = If(detailRgba IsNot Nothing, detailRgba(i * 4 + ch), EngineDefaultDetail)  ' slot 3 -> t4
                        Dim sl = clin * clin + 2.0 * clin * tint * (1.0 - clin)      ' softlight(complexion_lin, tint)
                        ' ⭐ FgTintChannelClamped, NO FgTintChannel: el MISMO piso que aplica la inversa
                        ' (PreCompensateEngineChain). Ver FgAmpFloor — tenerlo en un solo lado rompia la cancelacion.
                        complexionRgba(i * 4 + ch) = CSng(Lin2Srgb(sl * FgTintChannelClamped(det, ch)))
                    Next
                Next
            End Sub)
    End Sub

    ''' <summary>⭐⭐⭐ PRE-COMPENSACIÓN del amplify del detail. Divide el buffer (in place, sRGB) por
    ''' <c>amplify(detail)</c> EN LINEAL, para que cuando el motor lo multiplique por ESE MISMO amplify desde el
    ''' slot 3 el resultado sea EXACTAMENTE el buffer que entró — o sea, lo que muestra el preview.
    '''
    ''' <para><b>Por qué hace falta.</b> El fold deja en el slot 0 la base ya amplificada CON los overlays encima
    ''' (orden RaceMenu: el overlay NO lleva tint ni detail). Para que el motor no re-aplicara nada, el bake
    ''' neutralizaba los slots 3 y 6. El 6 funciona (el motor arma su path canónico y ahí escribimos el gris).
    ''' El 3 NO: la nota de RE del repo (<c>arch_sse_face_txst_layered_law</c>) registra que
    ''' <c>RegenerateHead 0x14042BD90</c> empuja <c>_sk</c>/detail al material (<c>+0xB0</c>/<c>+0xA8</c>) desde el
    ''' TXST RESUELTO al attachear la cabeza ⇒ el neutro del NIF se descarta y el amplify se aplica DOS VECES.
    ''' Con el detail medido de un NPC real (0,256/0,241/0,245) el amplify es (1,036, 0,960, 0,992); al cuadrado
    ''' (1,073, 0,922, 0,984) ⇒ ~2% más oscuro en luminancia, verde −8%. Es el síntoma reportado, y explica que el
    ''' PREVIEW se viera bien: el preview sí respeta su propio neutro.</para>
    '''
    ''' <para><b>Por qué pre-compensar y no dejar de plegar el detail.</b> Sacar el amplify del fold arregla la
    ''' base pero se lo aplica AL OVERLAY, que en RaceMenu va limpio. Pre-compensar preserva el orden exacto: el
    ''' motor calcula <c>((base×amp) ⊕ overlay)/amp × amp</c> = el buffer original, overlay intacto.</para>
    '''
    ''' <para><b>Robusto ante la incógnita del motor.</b> El slot 3 queda con el detail REAL. Si el motor lo
    ''' reinstala, reinstala EL MISMO archivo; si lo respeta, es el mismo también. En los dos casos multiplica por
    ''' el amplify que acá dividimos ⇒ ya no depende de que un neutro sobreviva. Y desaparece
    ''' <c>facedetailneutral.dds</c>, el único artefacto COMPARTIDO por plugin entre NPCs/ESPs.</para>
    '''
    ''' <para>⚠️ <paramref name="detailRgba"/> DEBE ser el MISMO buffer que recibió
    ''' <see cref="FoldFacetintIntoDiffuse"/>, al mismo tamaño. Nothing ⇒ no-op: sin detail el fold usó el default
    ''' del engine (0,251) y el motor usará ese mismo default, así que ya está balanceado.
    ''' El divisor se acota por abajo (un detail patológicamente oscuro dispararía el brillo) y el resultado se
    ''' satura a 1: donde <c>amp &lt; 1</c> la división sube el valor y un LDR de 8 bits no tiene cabecera.</para></summary>
    Public Sub PreCompensateEngineChain(bufferSrgb As Single(), facetintRgba As Single(), detailRgba As Single(),
                                        npix As Integer)
        If bufferSrgb Is Nothing Then Return
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    For ch = 0 To 2
                        Dim y = Srgb2Lin(bufferSrgb(i * 4 + ch))

                        ' 1) invertir el AMPLIFY del detail (slot 3): y /= amp
                        If detailRgba IsNot Nothing Then
                            ' MISMO piso que el fold (FgTintChannelClamped). Ver FgAmpFloor.
                            y /= FgTintChannelClamped(detailRgba(i * 4 + ch), ch)
                        End If

                        ' 2) invertir el SOFT-LIGHT del facetint (slot 6).
                        '    softlight(x,b) = x²(1−2b) + 2bx = y  ⇒  x = (−b + √(b² + k·y)) / k,  k = 1−2b.
                        '    k→0 (b=0,5) es la identidad: el límite es x = y (la fórmula daría 0/0).
                        If facetintRgba IsNot Nothing Then
                            Dim b = facetintRgba(i * 4 + ch)
                            Dim k = 1.0 - 2.0 * b
                            If Math.Abs(k) > 0.000001 Then
                                Dim disc = b * b + k * y
                                If disc < 0.0 Then disc = 0.0
                                y = (-b + Math.Sqrt(disc)) / k
                            End If
                        End If

                        If y < 0.0 Then y = 0.0
                        If y > 1.0 Then y = 1.0
                        bufferSrgb(i * 4 + ch) = CSng(Lin2Srgb(y))
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
