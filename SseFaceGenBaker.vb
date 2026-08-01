Option Strict On

Imports System.Runtime.CompilerServices
Imports System.Numerics

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
''' byte-identical. See 31-sse-facetint-spec / 22-morphs-sse-nam9-map.
''' </summary>
Public Module SseFaceGenBaker

    ''' <summary>Lanes de un Vector(Of Single) en ESTA maquina (8 con AVX2, 4 con SSE2). Tamano de bloque
    ''' de todos los loops vectoriales de este modulo.</summary>
    Private ReadOnly lanes As Integer = FastPow.LaneCount

    ''' <summary>Exponente sRGB. Se calcula en Double y RECIEN AHI se angosta: el float mas cercano al
    ''' exponente real. Escribirlo 1.0F/2.4F daria OTRO float y por lo tanto otra imagen.</summary>
    ' (InvG24Baker se fue con el ultimo MathF.Pow de este modulo: el exponente ahora vive partido en
    '  FastPow.InvG24, que es lo que exige el truco de precision.)

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
                                    Optional dxgiFormat As Integer = -1,
                                    Optional ByRef accOut As Single() = Nothing) As Byte()
        ' accOut devuelve el ACUMULADOR que se acaba de componer, para que el caller que ademas vuelca el
        ' TGA no tenga que componerlo de nuevo. El doc de ComposeFacetintAcc ya decia "public so the bake
        ' can dump a lossless TGA without a second compose", pero el call site del bake SI recomponia:
        ' con "Generate TGA" tildado (que NO es un flag de debug — es la opcion de CharGen Options) el
        ' facetint de CADA NPC de SSE se componia DOS VECES. Devolverlo aca lo deja en una.
        Dim acc = ComposeFacetintAcc(pm, npcRec, race, raceFormID, isFemale, w, h, npcTintOverride, tintTexOverride)
        accOut = acc
        If acc Is Nothing Then Return Nothing
        Return EncodeLinearRgbaToBc3(acc, w, h, dxgiFormat)
    End Function

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
        Return RgbaFloatToBgraBytes(acc, w * h)
    End Function

    ''' <summary>RGBA float [0,1] → BGRA byte (alpha opaco). ⭐ Era el MISMO cuerpo copiado en
    ''' <see cref="LinearRgbaToBgra"/> y en <see cref="EncodeLinearRgbaToBc3"/>, y en los dos estaba SERIAL —
    ''' una omisión: el cuerpo es puramente por píxel con escrituras disjuntas, así que paralelizarlo es
    ''' bit-idéntico (sólo cambia qué thread lo ejecuta) igual que en el resto del módulo.
    ''' <para>⛔ FIDELIDAD DEL NaN: el escalar hace <c>CByte(Max(0, Min(255, Round(v*255))))</c>, y con NaN eso
    ''' TIRA <c>OverflowException</c>. El camino vectorial NO puede "arreglarlo" devolviendo 0 en silencio —
    ''' sería degradar una condición anómala a un default, justo lo que la regla del proyecto prohíbe. Por eso
    ''' el bloque que contenga un NaN cae al escalar y tira la MISMA excepción que antes.</para></summary>
    Private Function RgbaFloatToBgraBytes(acc As Single(), npix As Integer) As Byte()
        Dim bgra(npix * 4 - 1) As Byte
        Dim pixPerBlock = lanes \ 4                 ' LaneCount es multiplo de 4 => cubre pixeles ENTEROS
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                Dim i = range.Item1
                ' ⛔ ANTES esto estaba gateado SOLO por Accelerated256, sin rama V128, mientras el fold y la
                ' pre-compensacion de este MISMO archivo si la tenian. En una maquina SSE2 el byte-pack de la
                ' cara ENTERA (hasta 4096²) quedaba 100 % escalar sin ninguna razon. Con el ancho variable el
                ' runtime elige 8 o 4 lanes y no hay dos caminos que mantener.
                If FastPow.AcceleratedV AndAlso pixPerBlock >= 1 Then   ' guard simetrico con el pack de FO4
                    Dim tmp(lanes - 1) As Single
                    While i + pixPerBlock <= range.Item2
                        Dim e = i * 4
                        Dim v = New Vector(Of Single)(acc, e)
                        ' MISMO orden que el escalar: Round primero, DESPUES Min(255), DESPUES Max(0).
                        Dim s = Vector.Multiply(v, New Vector(Of Single)(255.0F))
                        Dim mg = New Vector(Of Single)(12582912.0F)             ' 1.5*2^23: round-half-to-even
                        Dim rr = Vector.Subtract(Vector.Add(s, mg), mg)
                        If Not Vector.EqualsAll(rr, rr) Then Exit While          ' hay NaN -> que lo tire el escalar
                        rr = Vector.Min(rr, New Vector(Of Single)(255.0F))
                        rr = Vector.Max(rr, Vector(Of Single).Zero)
                        rr.CopyTo(tmp, 0)
                        For p = 0 To pixPerBlock - 1
                            Dim o = e + p * 4, t = p * 4
                            bgra(o) = CByte(tmp(t + 2))          ' B
                            bgra(o + 1) = CByte(tmp(t + 1))      ' G
                            bgra(o + 2) = CByte(tmp(t))          ' R
                            bgra(o + 3) = 255                    ' A
                        Next
                        i += pixPerBlock
                    End While
                End If
                While i < range.Item2
                    bgra(i * 4) = ClampByte(acc(i * 4 + 2))       ' B
                    bgra(i * 4 + 1) = ClampByte(acc(i * 4 + 1))   ' G
                    bgra(i * 4 + 2) = ClampByte(acc(i * 4))       ' R
                    bgra(i * 4 + 3) = 255                          ' A
                    i += 1
                End While
            End Sub)
        Return bgra
    End Function

    ''' <summary>Encode a linear RGBA buffer ([0,1], length w*h*4) to DDS bytes with mips. Default format = BC3
    ''' (DXT5), el formato del facetint: BGRA byte order + BC3 = lo que escribe el CK (round-trip validado ≈ piso
    ''' del DXT5) y lo que trae el vanilla (medido: los 3.158 facetint del BSA son DXT5 512² 9 mips).
    ''' <paramref name="dxgiFormat"/> permite seguir el formato elegido por el usuario (CharGen Options → Diffuse)
    ''' en vez de hardcodear; -1 = BC3.</summary>
    Public Function EncodeLinearRgbaToBc3(acc As Single(), w As Integer, h As Integer,
                                          Optional dxgiFormat As Integer = -1) As Byte()
        Dim bgra = RgbaFloatToBgraBytes(acc, w * h)      ' era este MISMO cuerpo, copiado y serial
        Dim fmt = If(dxgiFormat >= 0, dxgiFormat, DirectXTextureConversionHelper.DxgiFormatBc3Unorm)
        Return DirectXTextureConversionHelper.Bgra32BytesToDdsBytes(w, h, bgra, fmt, generateMipMaps:=True)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ClampByte(v As Single) As Byte
        Return CByte(MathF.Max(0.0F, MathF.Min(255.0F, MathF.Round(v * 255.0F, MidpointRounding.ToEven))))
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
    Public Const FgTintAmp As Single = 255.0F / 64.0F            ' = 3.984375
    Public Const FgTintOffR As Single = 1.0F / 255.0F
    Public Const FgTintOffG As Single = 0.0F
    Public Const FgTintOffB As Single = 1.0F / 255.0F

    ''' <summary>Default del engine para el slot 3 (DETAIL) cuando está VACÍO: <c>BSShader_DefFacegenDetail</c>,
    ''' textura UNIFORME <c>0x40</c> = 64/255 = 0.251. RE byte-level SkyrimSE.exe: la init de defaults
    ''' @0x140E57E30 la crea con fill <c>0x40404040</c> y la guarda en manager+0x88 (singleton 0x328CC20 ⇒
    ''' 0x328CCA8 = el default que el material facegen mete en +0xA8 @0x1414BA8B0). = vanilla blankdetailmap.dds.
    ''' ⚠️ NO es la Bayer 8×8 media 0.1235: esa es <c>BSShader_DitheringNoise</c>, creada en la MISMA función
    ''' unas instrucciones antes (por eso la nota vieja citaba 0x140E57E30 para el 0x40 y era ambigua).</summary>
    Public Const EngineDefaultDetail As Single = 64.0F / 255.0F

    ''' <summary>Default del engine para el slot 6 (TINT) cuando no hay facetint: <c>DefaultGreyMap</c>, uniforme
    ''' <c>0x80</c> = 128/255 = 0.50196. RE: misma init @0x140E57E30 (fill <c>0x80808080</c>), manager+0x70 =
    ''' 0x328CC90 = el default que el material facegen mete en +0xA0. Es (casi) la IDENTIDAD del soft-light:
    ''' con b = 0.5 EXACTO <c>a² + 2·a·0.5·(1−a) = a</c>, pero a 8 bits el valor representable es 128/255, y el
    ''' residuo queda acotado por <c>|softlight(a,128/255) − a| = 2·a·(1−a)·(1/510) ≤ 0.00098</c> (&lt; 1/4 de byte).
    ''' ⭐ Se usa el valor BYTE-EXACTO, no 0.5: es lo que hace el motor, y es también lo único que sobrevive a un
    ''' DDS de 8 bits — así el neutro que escribe el bake, el que instala el preview y el default del engine son
    ''' EL MISMO número en los cuatro caminos.</summary>
    Public Const EngineDefaultTint As Single = 128.0F / 255.0F

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function FgOff(ch As Integer) As Single
        Return If(ch = 1, FgTintOffG, If(ch = 2, FgTintOffB, FgTintOffR))
    End Function

    ''' <summary>El multiplicador amplificado de UN canal (0=R,1=G,2=B) a partir del DETAIL crudo [0,1]:
    ''' <c>(v+off)·(255/64)</c>. ⚠️ Se aplica al DETAIL (slot 3 → t4), NO al facetint. El detail NEUTRAL
    ''' (multiplicador = 1) es (63,64,63)/255; el default del engine 0.251 da (1.015625, 1.0, 1.015625).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function FgTintChannel(dChannel As Single, ch As Integer) As Single
        Return (dChannel + FgOff(ch)) * FgTintAmp
    End Function

    ''' <summary>⭐⭐ DOMINIO LEGAL DEL AMPLIFY — la política para <c>amp ≤ 0</c>, escrita UNA vez acá y
    ''' replicada LITERAL en el GLSL (decisión 1 del plan).
    '''
    ''' <para>⛔ EL PISO SE SACÓ. Había un <c>max(amp, 0.25)</c> en los seis sitios (fold escalar, fold
    ''' vectorial, inversa escalar, inversa vectorial, y las dos ramas del shader). <b>El motor NO acota</b>:
    ''' el desensamblado no tiene <c>_sat</c> en ningún paso de la cadena, y el shader del preview tampoco.
    ''' Un piso inventado hace que la cadena deje de reproducir al motor justo donde el detail es oscuro.</para>
    '''
    ''' <para><b>La DIRECTA no acota</b>: multiplica por el amp real, sea el que sea.</para>
    ''' <para><b>La INVERSA, con <c>amp ≤ 0</c>, NO divide y devuelve el valor tal cual.</b> Justificación: con
    ''' <c>amp = 0</c> la directa multiplica por 0 y DESTRUYE la información — ninguna política la recupera,
    ''' así que la única definida y no explosiva es la identidad. Y es la misma en los dos lenguajes, a
    ''' diferencia de dividir por 0 (±Inf) o de <c>clamp(NaN)</c>, que en GLSL es implementation-defined.
    ''' <c>amp = 0</c> es ALCANZABLE exacto en el canal verde, porque su offset es 0.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function FgAmpInverse(y As Single, dChannel As Single, ch As Integer) As Single
        Dim a = FgTintChannel(dChannel, ch)
        If a <= 0.0F Then Return y      ' dominio ilegal: la inversa NO divide (ver arriba)
        Return y / a
    End Function

    ''' <summary>Valor crudo del DETAIL que hace el amplify IDENTIDAD (multiplicador = 1) por canal — para dejar el
    ''' slot 3 no-op cuando la cadena ya se plegó en el diffuse. (v+off)·(255/64)=1 ⇒ v = 64/255 − off.
    ''' R,B=63/255; G=64/255.</summary>
    Public Function DetailNeutralChannel(ch As Integer) As Single
        Return 64.0F / 255.0F - FgOff(ch)
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
    ''' <param name="softLightModelOverride">-1 (default) = el modelo sale de la CONVENCIÓN
    ''' (<see cref="FoldSoftLightModel"/>). Un valor explícito la pisa. ⛔ Existe para los GOLDEN ABSOLUTOS,
    ''' que miden la LEY DEL MOTOR y por lo tanto NO pueden depender de lo que el usuario elija en el bucket
    ''' Fold: con el modelo leído del config, mover ese bucket hacía fallar <c>fold-golden</c> y ABORTABA el
    ''' bake — MEDIDO 2026-08-01. Un golden que se mueve con una opción no es un golden.</param>
    Public Sub FoldFacetintIntoDiffuse(complexionRgba As Single(), facetintRgba As Single(), npix As Integer,
                                       Optional detailRgba As Single() = Nothing,
                                       Optional softLightModelOverride As Integer = -1)
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
        ' ⭐ EL MODELO DE SOFT-LIGHT, RESUELTO UNA VEZ Y ACA (no por píxel): sale del bucket Fold de la
        ' convención. Estaba CABLEADO en 3 (pegtop) dentro de FoldOne. Default = pegtop = la ley del motor,
        ' así que esto es byte-inerte; lo verifica el self-test `fold-golden`, cuyos golden NO se movieron.
        Dim slModel As Integer = If(softLightModelOverride >= 0, softLightModelOverride, FoldSoftLightModel())
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                Dim lo = range.Item1 * 4, hi = range.Item2 * 4      ' indices de ELEMENTO, no de pixel
                Dim i = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count

                ' ⛔ LOS RANGOS DEL PARTITIONER NO VIENEN ALINEADOS. `lo` es multiplo de 4 (4 elementos por
                ' pixel) pero es multiplo de 8 solo si el pixel inicial es PAR, o sea la MITAD de las veces.
                ' Por eso hay PROLOGO escalar hasta alinear, cuerpo vectorial, y COLA escalar — las tres
                ' partes corriendo la MISMA ley, que es lo que hace que el resultado no dependa de donde
                ' cayo el corte de la particion.
                ' ⭐ UN SOLO camino: el ancho lo elige el runtime (8 lanes con AVX2, 4 con SSE2). Antes habia
                ' dos cuerpos DUPLICADOS a mano (V256 y V128) que habia que mantener en sincronia — y este era
                ' el unico modulo con esa duplicacion Y sin self-test que la verificara.
                ' ⭐ LOS CUATRO MODELOS VAN ACELERADOS. El cuerpo vectorial no tiene gate por modelo: el
                ' dispatch compartido (BlendDispatchV → SoftLightV) los cubre a todos, Illusions incluido
                ' (exponente variable por lane vía FastPow.PowVarV).
                If FastPow.AcceleratedV Then
                    While (i And (lanes - 1)) <> 0 AndAlso i < hi
                        FoldOne(complexionRgba, facetintRgba, detailRgba, i, slModel)
                        i += 1
                    End While
                    i = FoldRangeV(complexionRgba, facetintRgba, detailRgba, i, hi, slModel)
                End If

                ' COLA. Omitirla dejaba pixeles SIN PLEGAR y eso se midio: |byte delta| = 124.
                While i < hi
                    FoldOne(complexionRgba, facetintRgba, detailRgba, i, slModel)
                    i += 1
                End While
            End Sub)
    End Sub

    ''' <summary>El fold de UN elemento. Es la LEY ESCALAR, y la usan el prologo y la cola de cada rango;
    ''' el cuerpo vectorial de abajo es su espejo exacto. Una sola definicion ⇒ no puede haber deriva.</summary>
    ''' <summary>Modelo de soft-light del PLIEGUE (índice de <see cref="FaceTintConvention.FaceTintSoftLight"/>),
    ''' leído del bucket <c>Fold</c> de la convención. Default = pegtop = la ley del motor (DXBC).
    ''' <para>⛔ Se resuelve UNA vez por llamada, fuera del loop de píxeles: <c>ResolveConvention</c> lee el
    ''' config y no es gratis. Y va acá, no en <see cref="FoldOne"/>, para que el fold y su INVERSA
    ''' (<see cref="PreCompensateEngineChain"/>) no puedan resolver modelos distintos — si divergen, la cadena
    ''' del motor deja de cancelar y el render muestra algo que el juego no dibuja.</para></summary>
    Public Function FoldSoftLightModel() As Integer
        Return CInt(FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.Fold,
                                                         FaceTintChannel.Diffuse,
                                                         isTextureSet:=False, blendOp:=0).SoftLight)
    End Function

    ''' <summary>El modelo del MOTOR. Es el default del bucket Fold y el único con espejo vectorial.</summary>
    Friend Const PEGTOP_MODEL As Integer = 3

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub FoldOne(comp As Single(), tint As Single(), detail As Single(), i As Integer, slModel As Integer)
        Dim ch = i And 3
        If ch = 3 Then Return                                        ' el alpha no se toca
        Dim clin = Srgb2Lin(comp(i))
        Dim tv = tint(i)                                             ' slot 6 -> t3
        Dim det = If(detail IsNot Nothing, detail(i), EngineDefaultDetail)   ' slot 3 -> t4
        ' ⭐ EL DISPATCH COMPARTIDO, no una expresion propia (decision 4). Es la MISMA cuenta —la forma del
        ' motor es la del modelo 3— pero escrita en UN solo lugar. Hereda ademas los Clamp01 de entrada
        ' del dispatch: inerte en la practica (complexion y tint vienen de bytes, o sea [0,1]), se DECLARA.
        ' ⭐ El MODELO ya no es el literal 3: viene del bucket Fold (ver FoldSoftLightModel). Su inversa
        ' analitica esta en FaceTintCpuCompositor.BlendSoftLightModelInverse y la verifica `softlight-inv`.
        Dim sl = FaceTintCpuCompositor.BlendChannel(3, slModel, clin, tv)  ' softlight(complexion_lin, tint)
        ' ⛔ SIN PISO: la DIRECTA multiplica por el amp REAL (decision 1 — el motor no acota). Ver FgAmpInverse.
        comp(i) = Lin2Srgb(sl * FgTintChannel(det, ch))
    End Sub

    ' ---------------------------------------------------------------------------------------------
    ' Cuerpo VECTORIAL del fold. Entra en un indice YA alineado y devuelve el primero que no proceso.
    '
    ' ⭐ POR QUE EL AoS NO ESTORBA. El acumulador es intercalado (i*4+ch) y la intuicion dice "gather".
    ' No hace falta: la op es ELEMENTO A ELEMENTO POR CANAL, asi que 8 floats = 2 pixeles EXACTOS y el
    ' patron de canal se repite R,G,B,A,R,G,B,A. Tanto el offset por canal (FgOff difiere R/G/B) como la
    ' mascara "no tocar el alpha" son VECTORES CONSTANTES en ese layout. Cero gather, cero shuffle, y NO
    ' hace falta convertir el acumulador a SoA. Eso si: el enganche TIENE que estar alineado a 8 (V256) o
    ' a 4 (V128), o las constantes por canal quedarian corridas — de ahi el prologo del caller.
    ' Se usa FastPow.VBroadcastS(arr, idx) / CopyTo en vez de LoadUnsafe porque VB no tiene locals ByRef.
    ' ---------------------------------------------------------------------------------------------
    ''' <summary>Cuerpo vectorial del fold, de ANCHO VARIABLE. Reemplaza a los DOS cuerpos que habia
    ''' (FoldRangeV256 y FoldRangeV128), duplicados a mano: la ley se escribe UNA sola vez y el runtime elige
    ''' el ancho. Este era el unico modulo con esa duplicacion Y sin self-test que la verificara, o sea el que
    ''' mas facil podia divergir en silencio.
    ''' <para>Los patrones por canal (offsets del engine, y la mascara 'no toques el alpha') son de PERIODO 4,
    ''' asi que se generan para el ancho de la maquina en vez de ser literales de 8 lanes.</para></summary>
    Private Function FoldRangeV(comp As Single(), tint As Single(), detail As Single(),
                                lo As Integer, hi As Integer, slModel As Integer) As Integer
        Dim i = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
        Dim offV = FastPow.VPerChannel(FgTintOffR, FgTintOffG, FgTintOffB, 0.0F)
        Dim rgbMask = FastPow.VPerChannelMask(-1, -1, -1, 0)
        Dim ampV = New Vector(Of Single)(FgTintAmp)
        Dim oneV = New Vector(Of Single)(1.0F)
        Dim twoV = New Vector(Of Single)(2.0F)
        Dim defDet = New Vector(Of Single)(EngineDefaultDetail)
        While i + lanes <= hi
            Dim c = New Vector(Of Single)(comp, i)
            Dim clin = Srgb2LinV(c)
            Dim t = New Vector(Of Single)(tint, i)
            Dim d = If(detail Is Nothing, defDet, New Vector(Of Single)(detail, i))
            ' El MISMO dispatch que el escalar, en su espejo vectorial. Ver FoldOne.
            Dim sl = FaceTintCpuCompositor.BlendDispatchV(3, slModel, clin, t)
            ' FgTintChannel(d, ch) = (d + FgOff(ch)) * FgTintAmp. SIN piso: espejo exacto de FoldOne.
            Dim amp = Vector.Multiply(Vector.Add(d, offV), ampV)
            Dim res = Lin2SrgbV(Vector.Multiply(sl, amp))
            Vector.ConditionalSelect(rgbMask, res, c).CopyTo(comp, i)
            i += lanes
        End While
        Return i
    End Function

    ''' <summary>Espejo vectorial EXACTO de <see cref="Srgb2Lin"/> (misma rama, mismo pow).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function Srgb2LinV(c As Vector(Of Single)) As Vector(Of Single)
        Return FaceTintCpuCompositor.SrgbToLinV(c)
    End Function

    ''' <summary>Espejo vectorial EXACTO de <see cref="Lin2Srgb"/>. El orden de los selects replica el orden
    ''' de los <c>If</c> del escalar: primero la rama lineal, y DESPUES los dos clamps de los extremos, que
    ''' en el escalar son returns tempranos y por lo tanto ganan.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function Lin2SrgbV(c As Vector(Of Single)) As Vector(Of Single)
        Return FaceTintCpuCompositor.LinToSrgbV(c)
    End Function



    ''' <summary>⭐⭐⭐ PRE-COMPENSACIÓN del amplify del detail. Divide el buffer (in place, sRGB) por
    ''' <c>amplify(detail)</c> EN LINEAL, para que cuando el motor lo multiplique por ESE MISMO amplify desde el
    ''' slot 3 el resultado sea EXACTAMENTE el buffer que entró — o sea, lo que muestra el preview.
    '''
    ''' <para><b>Por qué hace falta.</b> El fold deja en el slot 0 la base ya amplificada CON los overlays encima
    ''' (orden RaceMenu: el overlay NO lleva tint ni detail). Para que el motor no re-aplicara nada, el bake
    ''' neutralizaba los slots 3 y 6. El 6 funciona (el motor arma su path canónico y ahí escribimos el gris).
    ''' El 3 NO: la nota de RE del repo (<c>50-facetint-leyes-y-compositor</c>) registra que
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
        ' ⭐ MISMO modelo que la DIRECTA, resuelto por la MISMA función y una sola vez: si el fold y su inversa
        ' resolvieran modelos distintos, la cadena del motor no cancelaría y el render mostraría algo que el
        ' juego no dibuja. Ver FoldSoftLightModel.
        Dim slModel As Integer = FoldSoftLightModel()
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                Dim lo = range.Item1 * 4, hi = range.Item2 * 4      ' indices de ELEMENTO, no de pixel
                Dim i = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
                ' Prologo / cuerpo vectorial / cola — misma estructura y mismo motivo que el fold: los
                ' rangos del Partitioner no vienen alineados y las tres partes corren la MISMA ley.
                ' ⭐ LOS CUATRO MODELOS ACELERADOS, igual que la directa: BlendSoftLightModelInverseV cubre
                ' pegtop, GIMP, Illusions y la cúbica de W3C (Cardano con FastPow.CbrtV).
                If FastPow.AcceleratedV Then
                    While (i And (lanes - 1)) <> 0 AndAlso i < hi
                        PreCompOne(bufferSrgb, facetintRgba, detailRgba, i, slModel)
                        i += 1
                    End While
                    i = PreCompRangeV(bufferSrgb, facetintRgba, detailRgba, i, hi, slModel)
                End If
                While i < hi
                    PreCompOne(bufferSrgb, facetintRgba, detailRgba, i, slModel)
                    i += 1
                End While
            End Sub)
    End Sub

    ''' <summary>La pre-compensacion de UN elemento: la LEY ESCALAR, usada por el prologo y la cola. El
    ''' cuerpo vectorial es su espejo exacto.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub PreCompOne(buf As Single(), tint As Single(), detail As Single(), i As Integer, slModel As Integer)
        Dim ch = i And 3
        If ch = 3 Then Return                                        ' el alpha no se toca
        Dim y = Srgb2Lin(buf(i))

        ' 1) invertir el AMPLIFY del detail (slot 3): y /= amp
        ' ⭐ DECISION 2 — DETAIL AUSENTE. Antes esto era `If detail IsNot Nothing`, o sea que sin slot 3 la
        ' inversa NO dividia. El comentario decia "ya esta balanceado" y era FALSO: la que tiene que cancelar
        ' es la del MOTOR, y el motor con el slot 3 vacio usa su propio default (EngineDefaultDetail, 0.251 ⇒
        ' amp = 1,015625/1,0/1,015625, que NO es 1). O sea que el amplify se aplicaba DOS veces en esos NPCs.
        ' Ahora la inversa usa EL MISMO default que la directa (FoldOne) y que el GLSL ⇒ la cadena cancela.
        Dim det = If(detail IsNot Nothing, detail(i), EngineDefaultDetail)
        y = FgAmpInverse(y, det, ch)

        ' 2) invertir el SOFT-LIGHT del facetint (slot 6) — POR MODELO, con la inversa ANALITICA compartida.
        '    ⭐ FUENTE UNICA: FaceTintCpuCompositor.BlendSoftLightModelInverse, con la derivacion cerrada de
        '    los cuatro modelos y su gate (`softlight-inv`, que exige Inv(Fwd(d,s),s) = d a menos de 1 byte).
        '    ⛔ Aca estaba escrita A MANO y SOLO la de pegtop: era correcta mientras el modelo estaba cableado,
        '    y dejaba de cancelar apenas el bucket Fold eligiera otro. Ahora la directa (FoldOne) y la inversa
        '    resuelven el MISMO `slModel` por la MISMA funcion.
        If tint IsNot Nothing Then
            Dim b = tint(i)
            y = FaceTintCpuCompositor.BlendSoftLightModelInverse(slModel, y, b)
        End If

        If Single.IsNaN(y) Then y = 0.0F
        If y < 0.0F Then y = 0.0F
        If y > 1.0F Then y = 1.0F
        buf(i) = Lin2Srgb(y)
    End Sub

    ' Cuerpo vectorial de la pre-compensacion. Las dos ramas condicionales del escalar se vuelven SELECTS:
    '   · `If Abs(k) > 1e-6` -> se calcula la inversa en TODOS los lanes y se elige. En los lanes con k~0 la
    '     division puede dar +-Inf o NaN, pero ese lane se DESCARTA (y .NET no lanza excepciones de FP).
    '   · `If disc < 0 OrElse IsNaN(disc) Then disc = 0` -> `Select(disc >= 0, disc, 0)`: para NaN la
    '     comparacion es falsa, con lo que cae en 0 igual que el escalar. Es equivalencia exacta, no
    '     aproximada — por eso el test de paridad exige 0 diferencias contra la cola escalar.
    Private Function PreCompRangeV(buf As Single(), tint As Single(), detail As Single(),
                                      lo As Integer, hi As Integer, slModel As Integer) As Integer
        Dim i = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
        Dim offV = FastPow.VPerChannel(FgTintOffR, FgTintOffG, FgTintOffB, 0.0F)
        Dim rgbMask = FastPow.VPerChannelMask(-1, -1, -1, 0)
        Dim ampV = FastPow.VBroadcastS(FgTintAmp)
        Dim defDetV = FastPow.VBroadcastS(EngineDefaultDetail)
        Dim zero = Vector(Of Single).Zero
        Dim one = FastPow.VBroadcastS(1.0F)
        Dim two = FastPow.VBroadcastS(2.0F)
        Dim eps = FastPow.VBroadcastS(0.000001F)
        While i + lanes <= hi
            Dim orig = FastPow.VBroadcastS(buf, i)
            Dim y = Srgb2LinV(orig)

            ' Espejo EXACTO de FgAmpInverse: default del motor cuando falta el detail (decision 2) y, con
            ' amp <= 0, NO se divide — se deja el valor (decision 1). El select replica ese `If`.
            Dim dv = If(detail Is Nothing, defDetV, FastPow.VBroadcastS(detail, i))
            Dim amp = Vector.Multiply(Vector.Add(dv, offV), ampV)
            y = Vector.ConditionalSelect(Vector.GreaterThan(amp, zero), Vector.Divide(y, amp), y)

            ' ⭐ La inversa POR MODELO, espejo exacto de PreCompOne. Acá estaba escrita a mano y sólo la de
            ' pegtop — la MISMA duplicación que tenía el escalar. Ahora las dos leen la única definición.
            If tint IsNot Nothing Then
                Dim b = FastPow.VBroadcastS(tint, i)
                y = FaceTintCpuCompositor.BlendSoftLightModelInverseV(slModel, y, b)
            End If

            y = Vector.ConditionalSelect(Vector.Equals(Of Single)(y, y), y, zero)      ' NaN -> 0
            y = Vector.Min(Vector.Max(y, zero), one)
            Vector.ConditionalSelect(rgbMask, Lin2SrgbV(y), orig).CopyTo(buf, i)
            i += lanes
        End While
        Return i
    End Function


    ''' <summary>sRGB→linear por canal (curva estándar IEC 61966-2-1). Para plegar el albedo en linear como el engine.
    ''' <para>El <c>pow</c> es <see cref="FastPow"/>, no <c>MathF.Pow</c>: es la MISMA ley que corre la versión
    ''' vectorizada del fold (<see cref="FoldFacetintIntoDiffuse"/>), así que la cola escalar de cada rango de
    ''' partición da EXACTAMENTE lo mismo que el cuerpo vectorial. Tenerlas distintas era el bug.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Srgb2Lin(c As Single) As Single
        Return FaceTintCpuCompositor.SrgbToLinShared(c)
    End Function

    ''' <summary>linear→sRGB por canal (curva estándar), clamp [0,1]. Ver la nota de <see cref="Srgb2Lin"/>.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Lin2Srgb(c As Single) As Single
        Return FaceTintCpuCompositor.LinToSrgbShared(c)
    End Function

    ''' <summary>⭐ SELF-TEST de los tres caminos vectorizados de ESTE módulo: el fold, la pre-compensación de
    ''' la cadena del engine, y el byte-pack. Devuelve "" si todo coincide BIT A BIT con la ley escalar.
    '''
    ''' <para><b>Por qué existe.</b> Este era el ÚNICO módulo vectorizado SIN self-test — y encima el único que
    ''' tenía las leyes DUPLICADAS a mano (un cuerpo Vector256 y otro Vector128). O sea: la cobertura estaba
    ''' exactamente invertida respecto del riesgo. Hoy la duplicación ya no existe (un solo cuerpo de ancho
    ''' variable), pero el test tiene que quedar: es lo que impide que la próxima edición del cuerpo vectorial
    ''' se desincronice de <see cref="FoldOne"/> / <see cref="PreCompOne"/> / <see cref="ClampByte"/> en
    ''' silencio.</para>
    '''
    ''' <para>⛔ Los largos se eligen para que los rangos NO caigan alineados: el fold y la pre-compensación
    ''' tienen prólogo escalar + cuerpo vectorial + cola escalar, y el bug clásico vive justo ahí (omitir la
    ''' cola dio <c>|byte delta| = 124</c>). Se barren también <c>detail = Nothing</c> (que usa el default del
    ''' engine) y los bordes: 0, 1, fuera de rango, y el <c>k → 0</c> de la inversa, que es su singularidad.</para>
    ''' <para>⚠️ NaN NO se barre en el byte-pack a propósito: ahí el contrato es que TIRE
    ''' <c>OverflowException</c>, y eso se verifica aparte al final.</para></summary>
    ' =================================================================================================
    ' GOLDEN VECTORS del fold — la salida ABSOLUTA, congelada.
    '
    ' ⛔ POR QUE HACIA FALTA: todos los tests del fold que ya existian son RELATIVOS (escalar-vs-vector,
    ' fold-vs-inversa). Un cambio de ley que entre en las DOS ramas los deja verdes a los dos. Estos
    ' vectores fijan el numero, no la coincidencia entre dos copias del mismo numero.
    '
    ' ⛔ NO hace early-return sin SIMD: ejercita la ley ESCALAR, que corre en toda maquina. Los siete tests
    ' de espejo vectorial si salen vacios sin SIMD, y por eso el gate reporta cobertura POR EJE.
    '
    ' El buffer es de 37 pixeles (impar y no multiplo del ancho) para que el MISMO caso pase por prologo
    ' escalar, cuerpo vectorial y cola escalar, y las tres partes tengan que dar lo mismo.
    '
    ' ✅ YA PASO lo que este comentario anunciaba: la decision 1 saco el piso del amplify, el test FALLO en
    ' golden[5] (el caso det=0) y los literales se re-congelaron con `--dump-golden`. Funciono exactamente
    ' como se esperaba: de los 11 casos se movieron los TRES que el piso levantaba y ni uno mas.
    ' =================================================================================================

    ''' <summary>Las ternas (complexion, tint, detail) del golden. Incluye las esquinas: amp=0 exacto (sólo
    ''' alcanzable en VERDE, cuyo offset es 0), los dos lados del piso, tint 0 y 1, complexion 0 y 1, el codo
    ''' de la curva sRGB, los defaults del engine, y un caso que satura por arriba antes del Lin2Srgb.</summary>
    Public ReadOnly FoldGoldenCases As (Comp As Single, Tint As Single, Detail As Single)() = {
        (0.5F, EngineDefaultTint, EngineDefaultDetail),   ' el caso neutro del engine
        (0.0F, 0.0F, 0.0F),                               ' amp=0 exacto en VERDE -> hoy lo levanta el piso
        (1.0F, 1.0F, 1.0F),
        (0.5F, 0.0F, EngineDefaultDetail),                ' tint=0
        (0.5F, 1.0F, EngineDefaultDetail),                ' tint=1
        (0.5F, 0.5F, 0.0F),                               ' amp=0 en verde, con complexion medio
        (0.5F, 0.5F, 0.06F),                              ' amp por DEBAJO del piso (0.2391)
        (0.5F, 0.5F, 0.07F),                              ' amp por ENCIMA del piso (0.2789) — el par del borde
        (0.04045F, 0.5F, EngineDefaultDetail),            ' codo de la curva sRGB
        (0.25F, 0.75F, 0.5F),                             ' interior generico
        (0.9F, 0.1F, 0.9F)                                ' satura por arriba antes de Lin2Srgb
    }

    ''' <summary>Salida congelada, como PATRONES DE BITS de Single (no decimales: un literal decimal no
    ''' round-trippea garantizado y el test se volveria aproximado justo donde tiene que ser exacto).
    ''' Una fila por caso, tres columnas = canales R, G, B.</summary>
    ' ⚠️ RE-CONGELADOS 2026-08-01 con `--paritygate --dump-golden`, A PROPOSITO: la fase 6 SACO el piso del
    ' amplify (decision 1). Los que se movieron son los tres casos que el piso levantaba; el resto no cambio,
    ' que es justo lo que confirma que el cambio fue el buscado y no una deriva de arriba.
    Private ReadOnly FoldGoldenBits As Integer(,) = {
        {&H3F011AB4I, &H3F002EACI, &H3F011AB4I},   ' 0,5043137 0,50071216 0,5043137
        {&H00000000I, &H00000000I, &H00000000I},   ' 0 0 0  — comp=0 anula el amplify
        {&H3F800000I, &H3F800000I, &H3F800000I},   ' 1 1 1
        {&H3E749778I, &H3E72A76EI, &H3E749778I},   ' 0,23885906 0,23696682 0,23885906
        {&H3F280270I, &H3F26D646I, &H3F280270I},   ' 0,6562872 0,65170705 0,6562872
        {&H3D309538I, &H00000000I, &H3D309538I},   ' ⭐ det=0: amp=0 EXACTO en verde ⇒ 0. R/B con amp=1/64. SIN piso
        {&H3E848F00I, &H3E805FCFI, &H3E848F00I},   ' det=0,06: amp real (0,2547 / 0,2391) — el piso ya no interviene
        {&H3E8E97BEI, &H3E8AC21EI, &H3E8E97BEI},   ' det=0,07: no cambio (ya estaba por encima del viejo piso)
        {&H3D283786I, &H3D25AEDCI, &H3D283786I},   ' 0,041068576 0,040449962 0,041068576
        {&H3ED94CCCI, &H3ED88094I, &H3ED94CCCI},   ' 0,42441404 0,42285597 0,42441404
        {&H3F800000I, &H3F800000I, &H3F800000I}    ' 1 1 1 — satura
    }

    ''' <summary>Corre el fold REAL (la entrada pública, con su prólogo/cuerpo/cola) sobre un caso y devuelve
    ''' los tres canales. La usan el self-test y el volcado de <c>--paritygate --dump-golden</c>, que es como
    ''' se re-congelan los literales cuando un cambio de ley los mueve a propósito.</summary>
    ''' <summary>⭐ GATE del espejo escalar-vs-vectorial del fold Y del unfold PARA LOS CUATRO MODELOS de
    ''' soft-light. El self-test `baker` sólo cubre el modelo que diga el config (en la práctica, el default
    ''' pegtop), así que los otros tres espejos vectoriales quedarían SIN GATE — y una divergencia ahí no se
    ''' ve: sale una cara levemente distinta, no un fallo.
    ''' <para>Compara <c>FoldRangeV</c> vs <c>FoldOne</c> y <c>PreCompRangeV</c> vs <c>PreCompOne</c> BIT A BIT.
    ''' Sin SIMD devuelve "" (no hay espejo que comparar; el gate lo reporta por eje).</para>
    ''' <para>El buffer es múltiplo exacto de <c>lanes</c> a propósito: acá se mide el CUERPO vectorial contra
    ''' la ley escalar, no el prólogo/cola (eso ya lo cubre `baker` con tamaños impares).</para></summary>
    Public Function FoldSoftLightModelsVectorSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""
        Dim lanes = Vector(Of Single).Count
        Dim n = lanes * 8 * 4
        Dim comp(n - 1) As Single, tint(n - 1) As Single, det(n - 1) As Single
        ' Relleno DETERMINISTA y con periodos coprimos entre sí y con 4 (el ancho del píxel), para que cada
        ' canal vea combinaciones distintas y se crucen las ramas de los cuatro modelos (s por encima y por
        ' debajo de 0,5, d por encima y por debajo de 0,25).
        For i = 0 To n - 1
            comp(i) = CSng((i * 7 Mod 251) / 250.0)
            tint(i) = CSng((i * 13 Mod 257) / 256.0)
            det(i) = CSng((i * 5 Mod 241) / 240.0)
        Next
        For model = 0 To 3
            Dim fv(n - 1) As Single, fs(n - 1) As Single
            Array.Copy(comp, fv, n) : Array.Copy(comp, fs, n)
            FoldRangeV(fv, tint, det, 0, n, model)
            For i = 0 To n - 1
                FoldOne(fs, tint, det, i, model)
            Next
            For i = 0 To n - 1
                If BitConverter.SingleToInt32Bits(fv(i)) <> BitConverter.SingleToInt32Bits(fs(i)) Then
                    Return $"Fold model={model} vector MISMATCH i={i} " &
                           $"escalar=0x{BitConverter.SingleToInt32Bits(fs(i)):X8} vector=0x{BitConverter.SingleToInt32Bits(fv(i)):X8}"
                End If
            Next

            Dim uv(n - 1) As Single, us(n - 1) As Single
            Array.Copy(comp, uv, n) : Array.Copy(comp, us, n)
            PreCompRangeV(uv, tint, det, 0, n, model)
            For i = 0 To n - 1
                PreCompOne(us, tint, det, i, model)
            Next
            For i = 0 To n - 1
                If BitConverter.SingleToInt32Bits(uv(i)) <> BitConverter.SingleToInt32Bits(us(i)) Then
                    Return $"Unfold model={model} vector MISMATCH i={i} " &
                           $"escalar=0x{BitConverter.SingleToInt32Bits(us(i)):X8} vector=0x{BitConverter.SingleToInt32Bits(uv(i)):X8}"
                End If
            Next
        Next
        Return ""
    End Function

    Public Function FoldGoldenActual(caseIndex As Integer) As Single()
        Const NPIX As Integer = 37                     ' impar y no múltiplo del ancho: fuerza las tres partes
        Dim k = FoldGoldenCases(caseIndex)
        Dim n = NPIX * 4
        Dim comp(n - 1) As Single, tint(n - 1) As Single, det(n - 1) As Single
        For i = 0 To n - 1
            comp(i) = k.Comp : tint(i) = k.Tint : det(i) = k.Detail
        Next
        ' ⛔ MODELO PINEADO EN PEGTOP, no el de la convención: este golden mide la LEY DEL MOTOR, y una ley no
        ' puede moverse porque el usuario elija otra cosa en el bucket Fold. Con el modelo leído del config,
        ' poner `Fold.SoftLight` en W3C/GIMP/Illusions hacía fallar este test y el gate ABORTABA el bake
        ' entero — culpando además al SIMD, que no tenía nada que ver (MEDIDO 2026-08-01).
        FoldFacetintIntoDiffuse(comp, tint, NPIX, det, softLightModelOverride:=PEGTOP_MODEL)
        ' Todos los píxeles llevan la misma terna ⇒ si prólogo, cuerpo y cola no coinciden, esto lo delata.
        For p = 1 To NPIX - 1
            For c = 0 To 2
                If BitConverter.SingleToInt32Bits(comp(p * 4 + c)) <> BitConverter.SingleToInt32Bits(comp(c)) Then
                    Return Nothing                     ' Nothing = las tres partes del loop NO coinciden
                End If
            Next
        Next
        Return New Single() {comp(0), comp(1), comp(2)}
    End Function

    ''' <summary>Compara el fold contra los golden vectors congelados. "" si pasa.</summary>
    Public Function FoldGoldenSelfTest() As String
        For ci = 0 To FoldGoldenCases.Length - 1
            Dim got = FoldGoldenActual(ci)
            Dim k = FoldGoldenCases(ci)
            If got Is Nothing Then
                Return $"golden[{ci}] (comp={k.Comp}, tint={k.Tint}, det={k.Detail}): prólogo/cuerpo/cola del fold NO coinciden entre sí"
            End If
            For c = 0 To 2
                Dim gotBits = BitConverter.SingleToInt32Bits(got(c))
                If gotBits <> FoldGoldenBits(ci, c) Then
                    Return $"golden[{ci}] ch{c} (comp={k.Comp}, tint={k.Tint}, det={k.Detail}): " &
                           $"got 0x{gotBits:X8} ({got(c):R}), want 0x{FoldGoldenBits(ci, c):X8} " &
                           $"({BitConverter.Int32BitsToSingle(FoldGoldenBits(ci, c)):R})"
                End If
            Next
        Next
        Return ""
    End Function

    ''' <summary>Vuelca los golden en el formato exacto de <c>FoldGoldenBits</c>, para re-congelarlos cuando
    ''' un cambio de ley los mueva A PROPÓSITO. Es un volcado, no un gate.</summary>
    Public Function FoldGoldenDump() As String
        Dim sb As New Text.StringBuilder()
        For ci = 0 To FoldGoldenCases.Length - 1
            Dim got = FoldGoldenActual(ci)
            If got Is Nothing Then
                sb.AppendLine($"        {{ *** caso {ci}: prólogo/cuerpo/cola divergen *** }},")
            Else
                sb.AppendLine($"        {{&H{BitConverter.SingleToInt32Bits(got(0)):X8}I, " &
                              $"&H{BitConverter.SingleToInt32Bits(got(1)):X8}I, " &
                              $"&H{BitConverter.SingleToInt32Bits(got(2)):X8}I}},   ' {got(0):R} {got(1):R} {got(2):R}")
            End If
        Next
        Return sb.ToString()
    End Function

    Public Function BakerVectorSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""
        Dim seed As UInteger = 1357911UI

        For Each npix In New Integer() {1, 2, 3, 5, 9, 17, 1021}
            Dim n = npix * 4

            ' ---------- FOLD ----------
            Dim comp(n - 1) As Single, tint(n - 1) As Single, det(n - 1) As Single
            For i = 0 To n - 1
                comp(i) = NextU(seed) : tint(i) = NextU(seed) : det(i) = NextU(seed)
            Next
            If n >= 8 Then
                comp(0) = 0.0F : comp(1) = 1.0F : comp(2) = -0.25F : comp(3) = 1.5F
                tint(4) = 0.0F : tint(5) = 1.0F : det(6) = 0.0F : det(7) = 1.0F
            End If
            For Each withDetail In New Boolean() {True, False}
                Dim dv = If(withDetail, det, Nothing)
                Dim got(n - 1) As Single, want(n - 1) As Single
                Array.Copy(comp, got, n) : Array.Copy(comp, want, n)
                FoldFacetintIntoDiffuse(got, tint, npix, dv)   ' npix va TERCERO; detail es el opcional
                ' El espejo escalar se corre con EL MISMO modelo que resolvió la entrada pública, no con un
                ' literal: si el config trajera otro, el test compararía dos leyes distintas y daría rojo por
                ' el motivo equivocado.
                For i = 0 To n - 1
                    FoldOne(want, tint, dv, i, FoldSoftLightModel())
                Next
                For i = 0 To n - 1
                    If BitConverter.SingleToInt32Bits(got(i)) <> BitConverter.SingleToInt32Bits(want(i)) Then
                        Return $"Fold vector MISMATCH: npix={npix} detail={withDetail} i={i} " &
                               $"escalar=0x{BitConverter.SingleToInt32Bits(want(i)):X8} vector=0x{BitConverter.SingleToInt32Bits(got(i)):X8}"
                    End If
                Next
            Next

            ' ---------- PRE-COMPENSACIÓN (la inversa de la cadena) ----------
            Dim buf(n - 1) As Single
            For i = 0 To n - 1
                buf(i) = NextU(seed)
            Next
            If n >= 8 Then
                buf(0) = 0.0F : buf(1) = 1.0F
                ' k = 1 - 2b -> 0 es la SINGULARIDAD de la inversa (la fórmula daría 0/0 y el límite es x = y)
                tint(2) = 0.5F : tint(3) = 0.5F
            End If
            For Each withDetail In New Boolean() {True, False}
                Dim dv = If(withDetail, det, Nothing)
                Dim got2(n - 1) As Single, want2(n - 1) As Single
                Array.Copy(buf, got2, n) : Array.Copy(buf, want2, n)
                PreCompensateEngineChain(got2, tint, dv, npix)
                For i = 0 To n - 1
                    PreCompOne(want2, tint, dv, i, FoldSoftLightModel())
                Next
                For i = 0 To n - 1
                    If BitConverter.SingleToInt32Bits(got2(i)) <> BitConverter.SingleToInt32Bits(want2(i)) Then
                        Return $"PreComp vector MISMATCH: npix={npix} detail={withDetail} i={i} " &
                               $"escalar=0x{BitConverter.SingleToInt32Bits(want2(i)):X8} vector=0x{BitConverter.SingleToInt32Bits(got2(i)):X8}"
                    End If
                Next
            Next

            ' ---------- BYTE-PACK ----------
            Dim acc(n - 1) As Single
            For i = 0 To n - 1
                acc(i) = NextU(seed)
            Next
            If n >= 8 Then
                ' los bordes de redondeo half-to-even, que son los que deciden el byte
                acc(0) = 0.0F : acc(1) = 1.0F : acc(2) = -0.5F : acc(3) = 1.5F
                acc(4) = 0.5F / 255.0F : acc(5) = 1.5F / 255.0F : acc(6) = 2.5F / 255.0F : acc(7) = 254.5F / 255.0F
            End If
            Dim gotB = RgbaFloatToBgraBytes(acc, npix)
            For i = 0 To npix - 1
                Dim wB = ClampByte(acc(i * 4 + 2)), wG = ClampByte(acc(i * 4 + 1)), wR = ClampByte(acc(i * 4))
                If gotB(i * 4) <> wB OrElse gotB(i * 4 + 1) <> wG OrElse gotB(i * 4 + 2) <> wR OrElse gotB(i * 4 + 3) <> 255 Then
                    Return $"RgbaFloatToBgraBytes MISMATCH: npix={npix} px={i} " &
                           $"escalar=({wB},{wG},{wR},255) vector=({gotB(i * 4)},{gotB(i * 4 + 1)},{gotB(i * 4 + 2)},{gotB(i * 4 + 3)})"
                End If
            Next
        Next

        ' ---------- el contrato del NaN en el byte-pack: TIENE que explotar ----------
        ' ⛔ El vector NO puede "arreglar" un NaN devolviendo 0: sería degradar una anomalía a un default.
        Dim nanAcc(15) As Single
        nanAcc(6) = Single.NaN
        Try
            RgbaFloatToBgraBytes(nanAcc, 4)
            Return "RgbaFloatToBgraBytes: un NaN NO tiró OverflowException (el vector se lo tragó)"
        Catch ex As Exception
            If TypeOf ex IsNot OverflowException AndAlso
               Not (TypeOf ex Is AggregateException AndAlso
                    DirectCast(ex, AggregateException).InnerExceptions.Any(Function(e) TypeOf e Is OverflowException)) Then
                Return $"RgbaFloatToBgraBytes: con NaN tiró {ex.GetType().Name}, se esperaba OverflowException"
            End If
        End Try
        Return ""
    End Function

    ''' <summary>xorshift32 → [0,1). Determinista: el gate tiene que dar lo mismo en cada corrida y máquina.</summary>
    Private Function NextU(ByRef s As UInteger) As Single
        s = s Xor (s << 13) : s = s Xor (s >> 17) : s = s Xor (s << 5)
        Return CSng(s Mod 1000000UI) / 1000000.0F
    End Function

End Module
