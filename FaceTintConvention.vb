Imports System

''' <summary>
''' Convención de composición FaceTint configurable por estrato. Centraliza la tabla derivada
''' empíricamente (single-layer B03-06, 2026-05-28) en UN resolver, con enums, para que WS / FW /
''' MaskConv / Blend sean cambiables y unificables sin tocar el shader ni el builder.
'''
''' Modelo ENGINE-FAITHFUL (re-derivado del b12 BSFaceCustomizationShader, V2 DXBC + V1 CK builder
''' FUN_140ED0E40 — ck_bake_facetint_RULE_verified; reemplaza el modelo empírico "ws=entry_type" que
''' 50-facetint-leyes-y-compositor describía y que el shader REFUTÓ):
'''   - mask conv = G22Encode (= shader pow(maskSample,1/2.2)·opacity), universal D y N/S.
'''   - DIFFUSE: el ENGINE acumula en LINEAL y lerpea por cobertura en LINEAL (cs). El BLEND OP corre en su
'''     espacio intrínseco: SoftLight encodea base+src a G22 (pow 1/2.2), GIMP/sqrt, y vuelve a LINEAL (pow
'''     2.2) ANTES del lerp ; Normal/Multiply/Overlay/HardLight directos en LINEAL. El ws depende del blend
'''     op (G22 sólo para SoftLight). NOSOTROS guardamos el acumulador en G22 (os=G22) = pow(1/2.2) del
'''     acumulador lineal del engine — EQUIVALENTE (el lerp sigue en cs=Linear). Replace cancela el ws.
'''   - SRC SPACE por TIPO DE FUENTE (dos curvas del engine, ver ResolveConvention): TEXTURA de color
'''     (base/tint/swap) = SRV sRGB → Srgb ; COLOR SÓLIDO de paleta = builder γ2.2 → G22. NO "todo G22".
'''   - N/S: todo Linear, lerp puro por el MISMO alpha del diffuse (sin blend op, sin gamma).
'''   - framework = OverPrev (over-running); seed D (base = textura de color) = Srgb→OutputSpace.
'''
''' SYNC: el sweep del analyzer Python (auto_analyze_esp.py / test_conventions.py) replica estos
''' mismos ejes. Cambiar la tabla acá = cambiar el modelo del compositor; mantener el sweep alineado.
''' </summary>
Public Module FaceTintConvention

    ''' <summary>Espacio de trabajo en el que base+src se combinan antes de volver a stored sRGB.
    ''' Derivado: Replace (mix lineal) → Linear ; SoftLight sobre TextureSet (DDS sRGB) → Srgb ;
    ''' SkinTone → G22 (≈Srgb, gana por 0.24 byte, posible efecto del base→g22). Linear default.</summary>
    Public Enum FaceTintWorkingSpace
        Linear = 0
        Srgb = 1
        G22 = 2
        G24 = 3
    End Enum

    ''' <summary>Transformación aplicada a la mask espacial antes de multiplicar por opacity.
    ''' Reemplaza/extiende el enum legacy FaceTintBlendConvention (que solo tenía Linear/SrgbOpacity).
    ''' Derivado: G22Encode universal (outlier Brow D = Raw).</summary>
    Public Enum FaceTintMaskConv
        Raw = 0
        SrgbEncode = 1
        SrgbDecode = 2
        G22Encode = 3
        G22Decode = 4
        G24Encode = 5
        G24Decode = 6
    End Enum

    ''' <summary>Cómo el blend(prev/base, src) se compone con la cobertura en el acumulador. 4 frameworks
    ''' (GL+CPU agnósticos, se eligen por uFramework / el param de ComposeOne). base = textura original sin
    ''' tintar (uBase). OverPrev NO usa base -> byte-idéntico al modelo previo (DEFAULT).
    '''   OverPrev = mix(prev, blend(prev,src), cov)       source-over al acumulador corriente
    '''   OverBase = mix(base, blend(base,src), cov)       source-over al base original
    '''   AddBase  = prev + cov*(blend(base,src) - base)   additive del delta vs base
    '''   ModSrc   = blend(prev, mix(neutral, src, cov))   source modulado por cobertura</summary>
    Public Enum FaceTintFramework
        OverPrev = 0
        OverBase = 1
        AddBase = 2
        ModSrc = 3
    End Enum

    ''' <summary>Operador de blend efectivo. 0..4 = dominio del BlendOp del record FO4 (mapea via MapBlend).
    ''' 5..19 = modos separables estándar (Photoshop/W3C) que el record NO emite pero el dispatch SÍ soporta
    ''' (GL+CPU), agregados por pedido del usuario 2026-06-04. Apéndice: jamás reordenar (el config serializa
    ''' el entero). Read-only en la UI (no hay selección de blend hoy; sólo del record / Replace).</summary>
    Public Enum FaceTintBlend
        Replace = 0
        Multiply = 1
        Overlay = 2
        SoftLight = 3
        HardLight = 4
        Screen = 5
        Darken = 6
        Lighten = 7
        ColorDodge = 8
        ColorBurn = 9
        Difference = 10
        Exclusion = 11
        LinearDodge = 12
        LinearBurn = 13
        Subtract = 14
        Divide = 15
        LinearLight = 16
        VividLight = 17
        PinLight = 18
        HardMix = 19
    End Enum

    ''' <summary>Modelo de SOFT-LIGHT a usar cuando Blend=SoftLight (bop3). El compositor (GL+CPU) es
    ''' AGNÓSTICO: implementa TODOS los modelos y elige por este id (igual que dispatch de BlendOp). El
    ''' resolver decide cuál; HOY default = GIMP (derivado vs CK: minimiza el error en bop3). W3C queda como
    ''' el de la libreria previa; Illusions/Pegtop disponibles para A/B. Sin tocar el shader para cambiarlo.</summary>
    Public Enum FaceTintSoftLight
        W3C = 0          ' W3C SVG soft-light (la formula previa de la libreria)
        Gimp = 1         ' GIMP/Photoshop soft-light (DEFAULT derivado)
        Illusions = 2    ' Illusions.hu  d^(2^(2(0.5-s)))
        Pegtop = 3       ' pegtop  (1-2s)d^2 + 2sd
    End Enum

    ''' <summary>Canal de la textura de capa que aporta la COBERTURA espacial. FO4 lo deriva del layer-kind
    ''' (PaletteMask→.g, TextureSet-D→.a) y por eso el default es <c>ByKind</c> (comportamiento previo, byte-
    ''' idéntico). SSE usa el canal ROJO de la máscara para TODAS las capas (facegen tint + skee MASKT type 1),
    ''' así que su default es <c>R</c>. Parametrizable por bucket; GL y CPU lo heredan del mismo resolver.</summary>
    Public Enum FaceTintMaskChannel
        ByKind = -1     ' FO4: PaletteMask=.g, TextureSet-D=.a (lo decide el compositor por kind)
        R = 0
        G = 1
        B = 2
        A = 3
    End Enum

    ''' <summary>De dónde sale el SEED del acumulador diffuse antes de componer capas. <c>BaseTexture</c> (FO4,
    ''' default) = la textura de color base (head diffuse resuelto) llevada a OutputSpace — comportamiento previo.
    ''' <c>Constant</c> (SSE) = un color plano (<see cref="FaceTintConventionSettings.SeedConstant"/>, engine-
    ''' verificado 0.5) sin textura: el facegen tint del CK arranca de 0.5 constante. Config-driven por juego.</summary>
    Public Enum FaceTintSeedMode
        BaseTexture = 0
        Constant = 1
    End Enum

    ''' <summary>Tipo de capa del compositor de skee (CDXTextureRenderer.TextureType) — decide cómo se arma el
    ''' color/cobertura de la capa ANTES del blend. Verificado en los .fx (switch(type)): Normal = tex×color
    ''' (RGBA), Mask = (color.rgb, tex.R×color.a), Color = color sólido. FO4 facegen no lo usa (queda en el
    ''' default histórico vía el layer-kind); lo consume el loader MASKT/TintData de skee.</summary>
    Public Enum FaceTintLayerType
        Normal = 0      ' b = layerTex * color
        Mask = 1        ' b = (color.rgb, layerTex.r * color.a)
        Color = 2       ' b = color (sólido)
    End Enum

    ''' <summary>Resolución target por canal del FaceGen. Inherit (-1, DEFAULT) = MIP 0 NATIVO del source
    ''' (la res que tenga, por canal, SIN downgrade ni hardcode). 1..5 = tamaño explícito 512/1024/2048/
    ''' 4096/8192. Regla del seed a un target (ver ResolveResolutionSize): usar el MIP STORED del source a
    ''' ese tamaño si existe (mejor calidad, filtro de Bethesda, matchea a CK donde CK usó ese mip); si no,
    ''' downsamplear/upsamplear el mip 0. CK hace el specular a media res (usa su mip stored); nosotros NO
    ''' por default (Inherit = mip0 nativo, mejor calidad), es OPCIÓN. Bodyparts FUERZAN Inherit en los 3
    ''' canales (el enum aplica SOLO a la cara).</summary>
    Public Enum FaceTintChannelResolution
        Inherit = -1
        R512 = 1
        R1024 = 2
        R2048 = 3
        R4096 = 4
        R8192 = 5
    End Enum

    ''' <summary>Tamaño en px del enum, o el nativo si Inherit. 1..5 -> 512&lt;&lt;(n-1).</summary>
    Public Function ResolveResolutionSize(res As FaceTintChannelResolution, nativeSize As Integer) As Integer
        If res = FaceTintChannelResolution.Inherit Then Return nativeSize
        Dim n = CInt(res)
        If n < 1 OrElse n > 5 Then Return nativeSize
        Return 512 << (n - 1)
    End Function

    Public Enum FaceTintDiffuseCompression
        Bc3 = 0
        Bc7 = 1
        Uncompressed = 2
    End Enum

    ''' <summary>Compresión de salida de Normal/Specular: BC5 (default) o Uncompressed (B8G8R8A8).</summary>
    Public Enum FaceTintNormalSpecularCompression
        Bc5 = 0          ' 2-canales (tangent-space _n, FO4)
        Uncompressed = 1 ' B8G8R8A8 (= formato vanilla del _msn de SSE, medido 32bpp)
        Bc7 = 2          ' 3-canales comprimido, alta calidad — pero encode CPU lentísimo con la wrapper en debug
        Bc3 = 3          ' 4-canales comprimido (DXT5), encode rápido — DEFAULT del normal facegen
    End Enum

    Public Class FaceTintResolutionSettings
        Public Property Diffuse As FaceTintChannelResolution = FaceTintChannelResolution.Inherit
        Public Property Normal As FaceTintChannelResolution = FaceTintChannelResolution.Inherit
        Public Property Specular As FaceTintChannelResolution = FaceTintChannelResolution.Inherit
        ''' <summary>Compresión del diffuse de salida (default BC3; o BC7 / Uncompressed).</summary>
        Public Property DiffuseCompression As FaceTintDiffuseCompression = FaceTintDiffuseCompression.Bc3
        ''' <summary>Compresión del normal de salida (default BC5; o Uncompressed).</summary>
        Public Property NormalCompression As FaceTintNormalSpecularCompression = FaceTintNormalSpecularCompression.Bc5
        ''' <summary>Compresión del specular de salida (default BC5; o Uncompressed).</summary>
        Public Property SpecularCompression As FaceTintNormalSpecularCompression = FaceTintNormalSpecularCompression.Bc5
        Public Function ForChannel(ch As FaceTintChannel) As FaceTintChannelResolution
            Select Case ch
                Case FaceTintChannel.Normal : Return Normal
                Case FaceTintChannel.Specular : Return Specular
                Case Else : Return Diffuse
            End Select
        End Function
    End Class

    ''' <summary>Convención completa para una capa+canal. Inmutable; producida por ResolveConvention.
    ''' Cuatro espacios (el compositor — GL y CPU — es AGNÓSTICO y solo aplica estos via uniforms/params):
    '''   SrcSpace     = espacio del color de la capa (textura). D=color sRGB ; N/S=datos lineales (raw).
    '''   WorkingSpace = espacio donde corre el BLEND OP. alpha-over(replace)=Linear (mezcla física) ;
    '''                  blend-mode tonal (softlight/etc)=G22 (estilo Photoshop, sobre valores encoded).
    '''   CompositeSpace = espacio donde corre el COMPOSITE (la lerp por cobertura base+cov*(blend−base)).
    '''                  Ley derivada gen3 (Tools/FaceTintDerive): el blend va en su espacio pero el
    '''                  composite-lerp va en LINEAR-light (D/N/S).
    '''                  ⛔ NO forkea con forBake. Es `= bucket.CompositeSpace`, punto. (Este comentario
    '''                  afirmaba "para el render (forBake=False) = WorkingSpace": ERA FALSO —
    '''                  ResolveConvention no lee forBake NI UNA VEZ. Verificado 2026-07-30.)
    '''   OutputSpace  = espacio de ALMACENAMIENTO del canal: el espacio en el que el compose tiene que
    '''                  DEJAR el resultado. Default D=G22, N/S=Linear.
    '''                  ⛔ Tampoco forkea render-vs-bake: la ley es UNICA (WYSIWYG, el render replica el
    '''                  bake). El texto viejo "D render=Srgb ; D bake=G22" era falso por el mismo motivo.
    '''   AccumSpace   = espacio en el que VIVE el acumulador DURANTE el compose (ver el campo mas abajo).
    ''' El compositor convierte prev(AccumSpace)->WorkingSpace y src(SrcSpace)->WorkingSpace, blendea,
    ''' luego prev/blend->CompositeSpace, lerpea por cov, y devuelve CompositeSpace->AccumSpace. Al CERRAR
    ''' el canal —una sola vez, no por capa— hace AccumSpace->OutputSpace.
    ''' (Antes esto decia OutputSpace donde ahora dice AccumSpace; con el default los dos coinciden, pero
    '''  la descripcion correcta es la del acumulador.)
    ''' Sin ramas hardcodeadas en el compositor: toda la ley vive ACÁ, parametrizada por
    ''' (canal/entry/slot/blendOp/flags/useHairPalette). Tunear = cambiar esta tabla.</summary>
    Public Structure FaceTintConventionSet
        Public WorkingSpace As FaceTintWorkingSpace
        Public CompositeSpace As FaceTintWorkingSpace
        Public SrcSpace As FaceTintWorkingSpace
        Public OutputSpace As FaceTintWorkingSpace
        ''' <summary>Espacio en el que VIVE el acumulador MIENTRAS se componen las capas. Lo resuelve
        ''' <see cref="ResolveConvention"/> a un valor CONCRETO (nunca un flag) para que el compositor —CPU y
        ''' GL— siga siendo agnostico: lee este campo y convierte, sin saber por que vale lo que vale.
        ''' <para>Con <c>AccumSpace = OutputSpace</c> (DEFAULT) el comportamiento es EXACTAMENTE el previo: el
        ''' acumulador se guarda en OutputSpace y cada capa hace el ida-y-vuelta OutputSpace-&gt;Working/Composite
        ''' y de vuelta. Con <c>AccumSpace = CompositeSpace</c> el acumulador se queda en el espacio del
        ''' composite y esas dos conversiones por capa desaparecen (medido: el compose es el 94,9 % del bake y
        ''' su costo son los <c>Math.Pow</c> de estas conversiones).</para>
        ''' <para>⛔ NO es solo velocidad: quitar el round-trip quita tambien su PERDIDA DE PRECISION, asi que
        ''' la salida CAMBIA. Que eso ACERQUE o ALEJE del CK es empirico y hay que medirlo — el RE del motor
        ''' (buffer <c>float4[16]</c> a UN pixel shader image-space) sugiere que el motor NO round-tripea entre
        ''' capas, con lo que el round-trip seria NUESTRO artefacto. Por eso el default NO cambia y esto se
        ''' habilita desde el config para poder correr el A/B.</para></summary>
        Public AccumSpace As FaceTintWorkingSpace
        Public MaskConv As FaceTintMaskConv
        Public Framework As FaceTintFramework
        Public Blend As FaceTintBlend
        Public SoftLight As FaceTintSoftLight
    End Structure

    ''' <summary>Convención de UN bucket (Diffuse / Normal+Specular / Swap). Valores CONCRETOS, sin nulos:
    ''' estos SON la ley. Los defaults los fija FaceTintConventionSettings.New (= la ley derivada actual);
    ''' el usuario los edita desde CharGen Options o el config.json y se persisten en Config_App.
    ''' ResolveConvention los lee SIEMPRE de ahí. Blend NO está acá: es record-driven (diffuse = MapBlend)
    ''' o Replace (N·S, swap), read-only en la UI.</summary>
    Public Class FaceTintBucketConvention
        Public Property WorkingSpace As FaceTintWorkingSpace
        Public Property CompositeSpace As FaceTintWorkingSpace
        Public Property SrcSpace As FaceTintWorkingSpace
        Public Property OutputSpace As FaceTintWorkingSpace
        Public Property MaskConv As FaceTintMaskConv
        Public Property Framework As FaceTintFramework
        Public Property SoftLight As FaceTintSoftLight
        ''' <summary>Canal de máscara que aporta la cobertura. Default ByKind (FO4: el compositor lo decide por
        ''' layer-kind). SSE lo fija en R. Configs viejos sin el campo caen a ByKind (= comportamiento previo).</summary>
        Public Property MaskChannel As FaceTintMaskChannel = FaceTintMaskChannel.ByKind
        ''' <summary>True (DEFAULT desde 2026-07-30) = el acumulador se queda en CompositeSpace durante todo
        ''' el compose y se convierte a OutputSpace UNA sola vez al final. False = se guarda en OutputSpace y
        ''' cada capa paga el ida-y-vuelta (dos Math.Pow por canal por capa).
        ''' <para>Por que ON por default: ese ida-y-vuelta por capa era trabajo puro perdido. Medido sobre una
        ''' muestra de 200 NPCs de FO4, la fase Textures baja de 180,5 s a 108,8 s (-39,7 %) y el reloj de
        ''' pared de 3:12 a 2:00.</para>
        ''' <para>CAMBIA LA SALIDA (no es bit-identico al camino previo): al no redondear en cada capa el
        ''' resultado queda MAS preciso, no distinto por ley. Es editable y reversible desde CharGen Options
        ''' (checkbox por bucket de canal + Revert to defaults) y se persiste en config.json.</para></summary>
        Public Property AccumInCompositeSpace As Boolean = True
    End Class

    ''' <summary>WORKING SPACE del blend op POR CADA op del record (0..4) en el canal DIFFUSE. PARAMETRIZABLE
    ''' (config.json, NO hardcodeado): el espacio donde corre el blend depende del op, y el usuario lo puede
    ''' cambiar por op. Defaults ENGINE-FAITHFUL (b12 BSFaceCustomizationShader V2 DXBC + V1 CK builder
    ''' FUN_140ED0E40 — ck_bake_facetint_RULE_verified §4): el engine corre SoftLight en gamma-2.2 (decode
    ''' dst+color, GIMP, re-encode) y Normal/Multiply/Overlay/HardLight en LINEAR. Replace cancela el ws por
    ''' construcción (Cvt(Cvt(src,ss→ws),ws→cs)=Cvt(src,ss→cs)) — se expone igual por completitud. Persistido
    ''' como objeto plano (System.Text.Json); configs viejos sin el campo caen al default del constructor.</summary>
    Public Class FaceTintBlendWorkingSpaces
        Public Property Replace As FaceTintWorkingSpace = FaceTintWorkingSpace.Linear
        Public Property Multiply As FaceTintWorkingSpace = FaceTintWorkingSpace.Linear
        Public Property Overlay As FaceTintWorkingSpace = FaceTintWorkingSpace.Linear
        Public Property SoftLight As FaceTintWorkingSpace = FaceTintWorkingSpace.G22
        Public Property HardLight As FaceTintWorkingSpace = FaceTintWorkingSpace.Linear

        ''' <summary>ws para el blend op resuelto. Ops 0..4 = la prop correspondiente; modos extendidos
        ''' 5..19 (app-only, NO emitidos por el record) → <paramref name="fallback"/> (= bucket.WorkingSpace).</summary>
        Public Function ForBlend(b As FaceTintBlend, fallback As FaceTintWorkingSpace) As FaceTintWorkingSpace
            Select Case b
                Case FaceTintBlend.Replace : Return Replace
                Case FaceTintBlend.Multiply : Return Multiply
                Case FaceTintBlend.Overlay : Return Overlay
                Case FaceTintBlend.SoftLight : Return SoftLight
                Case FaceTintBlend.HardLight : Return HardLight
                Case Else : Return fallback
            End Select
        End Function
    End Class

    ''' <summary>La ley FaceTint completa, persistida en Config_App (config.json). Los defaults del
    ''' constructor = la ley derivada actual (byte-match con CK si no se tocan). Si el usuario los cambia
    ''' (UI o config.json) ESOS pasan a ser la ley: ResolveConvention los lee siempre. Sin nulos, sin
    ''' capa de override — KISS.</summary>
    Public Class FaceTintConventionSettings
        Public Property Diffuse As FaceTintBucketConvention
        Public Property NormalSpecular As FaceTintBucketConvention
        Public Property Swap As FaceTintBucketConvention
        ''' <summary>Working space del blend op POR op del record en el DIFFUSE (parametrizable). Default
        ''' engine-faithful: SoftLight=G22, resto=Linear. Reemplaza el uso plano de Diffuse.WorkingSpace
        ''' para el tint diffuse (Diffuse.WorkingSpace queda de fallback de los modos extendidos 5..19).</summary>
        Public Property DiffuseWorkingSpaceByBlend As FaceTintBlendWorkingSpaces
        ''' <summary>SrcSpace de TODA TEXTURA de color del DIFFUSE — tint TextureSet, SWAP, y la base-seed
        ''' (las 3 son texturas de color sampleadas). Engine-faithful = Srgb: el engine las bindea como SRV
        ''' sRGB (MakeSRGB FUN_14183e1c0) → el shader las recibe LINEALES (IEC) y el softlight les hace
        ''' pow(1/2.2). El color SÓLIDO (uColor PaletteMask) NO pasa por acá: el builder lo decodea con γ2.2
        ''' (powf, FUN_140ED0E40 ~363-369) ⇒ usa Diffuse.SrcSpace=G22. Las MÁSCARAS tampoco (crudas + MaskConv
        ''' g22encode). Parametrizable; default Srgb. (Lo consume ResolveConvention para tint+swap y el seed
        ''' del base en ambos compositores.)</summary>
        Public Property DiffuseTextureSrcSpace As FaceTintWorkingSpace
        Public Property SeedDiffuseG22 As Boolean

        ''' <summary>De dónde sale el seed del acumulador diffuse. BaseTexture (FO4, default) = head diffuse
        ''' resuelto → OutputSpace (comportamiento previo). Constant (SSE) = <see cref="SeedConstant"/> plano.
        ''' Configs viejos sin el campo caen a BaseTexture.</summary>
        Public Property SeedMode As FaceTintSeedMode = FaceTintSeedMode.BaseTexture
        ''' <summary>Color del seed cuando SeedMode=Constant (RGB [0,1], long 3). SSE engine-verificado = 0.5
        ''' plano. Inerte cuando SeedMode=BaseTexture. Serializado como array plano; null/short → 0.5.</summary>
        Public Property SeedConstant As Double() = New Double() {0.5, 0.5, 0.5}

        Public Sub New()
            ' Defaults = ley derivada actual. Diffuse: blend tonal en G22. N·S: datos lineales (raw).
            ' Swap: convención del DIFFUSE swap (los swaps de N·S usan el bucket NormalSpecular, no éste).
            ' Cambiar acá = cambiar el default de fábrica.
            ' Diffuse.SrcSpace = G22 = src del COLOR SÓLIDO de paleta (uColor): el builder lo decodea con γ2.2
            ' ⇒ el softlight ve el byte crudo (G22). Las TEXTURAS de color (tint/swap/base) NO usan esto: usan
            ' DiffuseTextureSrcSpace=Srgb (SRV sRGB), lo aplica ResolveConvention por tipo de fuente.
            Diffuse = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.G22,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.G22,
                .OutputSpace = FaceTintWorkingSpace.G22,
                .MaskConv = FaceTintMaskConv.G22Encode,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp}
            NormalSpecular = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Linear,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Linear,
                .OutputSpace = FaceTintWorkingSpace.Linear,
                .MaskConv = FaceTintMaskConv.G22Encode,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp}
            ' Swap.SrcSpace es LIVE: ResolveConvention YA NO lo pisa (se sacó el override hardcodeado). Es el src
            ' del swap de diffuse (textura de color) y se consume tal cual; default Srgb porque el engine bindea
            ' las texturas de color como SRV sRGB. Medido (00005C80): swap→Srgb subió las manchitas de las
            ' regiones de swap 35%→55% de CK sin regresión full-face. (WorkingSpace=G22 es inerte: el swap es
            ' Replace, que cancela el ws.) El path EXACTO del swap quedó inconcluso en el RE — no hay pre-pass
            ' byte-space separado en el FaceTint bake; Srgb es el best-fit vs CK, ajustable en la UI/config.
            Swap = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.G22,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Srgb,
                .OutputSpace = FaceTintWorkingSpace.G22,
                .MaskConv = FaceTintMaskConv.G22Encode,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp}
            ' Working space por blend op (engine-faithful: SoftLight=G22, resto=Linear). Parametrizable.
            DiffuseWorkingSpaceByBlend = New FaceTintBlendWorkingSpaces()
            ' SrcSpace de las texturas color TextureSet-diffuse: Srgb (SRV sRGB del engine, IEC). El uColor
            ' sólido queda en Diffuse.SrcSpace (G22). Parametrizable; engine-faithful por default.
            DiffuseTextureSrcSpace = FaceTintWorkingSpace.Srgb
            SeedDiffuseG22 = True
            ' FO4: seed = head diffuse (textura), canal de máscara por kind. Comportamiento previo intacto.
            SeedMode = FaceTintSeedMode.BaseTexture
            SeedConstant = New Double() {0.5, 0.5, 0.5}
        End Sub

        ''' <summary>Ley por DEFAULT para un juego. FO4 = el constructor (ley derivada byte-exacta vs CK).
        ''' SSE = el modelo facegen-tint del CreationKit (re_sseck, bsfacegenutils.cpp + ps DXBC): seed CONSTANTE
        ''' 0.5, cada capa un lerp UNIFORME por cobertura (Blend=Replace ⇒ acc=lerp(acc,color,cov)), sin blend-op
        ''' por tipo, TODO en LINEAR, máscara cruda por el canal ROJO. N/S y Swap no aplican en SSE (el _d es
        ''' tint-only); se dejan como el diffuse para que la UI/serialización tengan valores concretos.</summary>
        Public Shared Function DefaultsFor(game As Config_App.Game_Enum) As FaceTintConventionSettings
            Dim s As New FaceTintConventionSettings()
            If game <> Config_App.Game_Enum.Skyrim Then Return s   ' FO4 = defaults del constructor
            ' --- SSE ---
            Dim sseDiffuse = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Linear,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Linear,
                .OutputSpace = FaceTintWorkingSpace.Linear,
                .MaskConv = FaceTintMaskConv.Raw,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp,
                .MaskChannel = FaceTintMaskChannel.R}
            s.Diffuse = sseDiffuse
            ' N/S y Swap: clones del diffuse SSE (no se usan en el _d tint-only, pero evitan nulos en UI/JSON).
            s.NormalSpecular = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Linear, .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Linear, .OutputSpace = FaceTintWorkingSpace.Linear,
                .MaskConv = FaceTintMaskConv.Raw, .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp, .MaskChannel = FaceTintMaskChannel.R}
            s.Swap = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Linear, .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Linear, .OutputSpace = FaceTintWorkingSpace.Linear,
                .MaskConv = FaceTintMaskConv.Raw, .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp, .MaskChannel = FaceTintMaskChannel.R}
            ' Blend uniforme (Replace) ⇒ el working-space-por-op es inerte; se deja default. Seed constante 0.5.
            s.DiffuseWorkingSpaceByBlend = New FaceTintBlendWorkingSpaces()
            s.DiffuseTextureSrcSpace = FaceTintWorkingSpace.Linear
            s.SeedDiffuseG22 = False
            s.SeedMode = FaceTintSeedMode.Constant
            s.SeedConstant = New Double() {0.5, 0.5, 0.5}
            Return s
        End Function
    End Class

    ''' <summary>La ley del JUEGO ACTIVO (Config_App.Current.Game). FO4 → Setting_FaceTintConvention (persistido,
    ''' back-compat byte-exacto). SSE → Setting_FaceTintConvention_SSE. Null-safe: si el config no está cargado o
    ''' el set falta, cae al <see cref="FaceTintConventionSettings.DefaultsFor"/> del juego. ÚNICO punto de
    ''' lectura: ResolveConvention y las props Seed* leen de acá, así el compositor es agnóstico de juego.</summary>
    Public Function ActiveSettings() As FaceTintConventionSettings
        Dim c = Config_App.Current
        If c Is Nothing Then Return FaceTintConventionSettings.DefaultsFor(Config_App.Game_Enum.Skyrim)
        If c.Game = Config_App.Game_Enum.Skyrim Then
            Return If(c.Setting_FaceTintConvention_SSE, FaceTintConventionSettings.DefaultsFor(Config_App.Game_Enum.Skyrim))
        End If
        Return If(c.Setting_FaceTintConvention, FaceTintConventionSettings.DefaultsFor(Config_App.Game_Enum.Fallout4))
    End Function

    ''' <summary>¿El seed del diffuse aplica la conversion de espacio? Lo leen ambos compositores (GL y CPU).
    ''' Vive en el config; esto solo lo reenvia, null-safe.
    ''' <para>⛔ El nombre dice "G22" por historia: el seed ya NO lleva la base a G22 sino a <c>AccumSpace</c>
    ''' (ver <see cref="AccumSpaceForChannel"/>), que coincide con G22 solo mientras
    ''' <c>AccumInCompositeSpace</c> este apagado. Con el flag False este Boolean elige entre
    ''' "convertir SrcSpace-&gt;AccumSpace" y "sembrar crudo"; no fija el espacio destino.</para></summary>
    Public ReadOnly Property SeedConventionIs_G22 As Boolean
        Get
            Dim s = ActiveSettings()
            Return s IsNot Nothing AndAlso s.SeedDiffuseG22
        End Get
    End Property

    ''' <summary>SrcSpace del seed del base diffuse — la base ES una textura de color (SRV sRGB → lineal),
    ''' así que usa la MISMA regla que tint/swap: DiffuseTextureSrcSpace (default Srgb). Config-driven (ya no
    ''' literal "1"). Lo leen ambos compositores.</summary>
    Public ReadOnly Property SeedDiffuseSrcSpaceValue As Integer
        Get
            Dim s = ActiveSettings()
            Return If(s Is Nothing, CInt(FaceTintWorkingSpace.Srgb), CInt(s.DiffuseTextureSrcSpace))
        End Get
    End Property

    ' ⛔ ELIMINADA `SeedDiffuseOutputSpaceValue` (2026-07-30). Devolvia `Diffuse.OutputSpace` y su doc decia
    ' "el seed lleva la base a ESE espacio, lo leen ambos compositores". Las DOS cosas dejaron de ser ciertas:
    ' el seed ahora lleva la base a AccumSpace (ver AccumSpaceForChannel) y no quedaba UN solo lector en el
    ' repo. Su reemplazo exacto para el OutputSpace del diffuse es `OutputSpaceForChannel(Diffuse)`, que ademas
    ' es el MISMO resolver que usa el pase final — asi el par (origen, destino) no se puede desalinear.
    ' Se borra en vez de dejarla: dos formas de pedir lo mismo es como se elige la equivocada.

    ''' <summary>Slot SkinTone (RACE TintTemplateOption.Slot). Centralizado para no hardcodear 12.</summary>
    Private Const SLOT_SKINTONE As UShort = 12US

    ''' <summary>CAPACIDAD DECLARADA del compositor CPU que hace de ESPEJO de un camino de compose. Es la
    ''' condicion REAL que gatea <see cref="FaceTintBucketConvention.AccumInCompositeSpace"/>: el acumulador solo
    ''' puede salir de OutputSpace si LOS DOS lados —el GL y su espejo CPU— implementan la misma ley.
    ''' <para>⛔ LA CONDICION NO ES EL JUEGO. El compositor GL (<c>FaceTintCompositor</c>) es UNO SOLO y lo
    ''' comparten los dos motores; lo que cambia por camino es QUIEN es su espejo CPU. Por eso cada call site
    ''' declara el suyo pasando la constante que PUBLICA ese compositor
    ''' (<c>FaceTintCpuCompositor.AccumSpaceCapability</c> / <c>SseFaceTintComposer.AccumSpaceCapability</c>).
    ''' Habilitar un compositor nuevo es cambiar UNA constante al lado de su implementacion — no editar un
    ''' <c>If</c> por nombre de juego aca, que ademas seria falso: un mismo juego puede tener varios caminos.</para></summary>
    Public Enum FaceTintCpuMirrorCapability
        ''' <summary>El espejo CPU guarda el acumulador SIEMPRE en OutputSpace: no implementa un espacio de
        ''' acumulador propio. Con esto <c>AccumSpace</c> colapsa a <c>OutputSpace</c> y el flag queda INERTE
        ''' POR CONSTRUCCION — el GL no puede apartarse de un CPU que no lo sigue, pase lo que pase en el config.</summary>
        OutputSpaceOnly = 0
        ''' <summary>El espejo CPU implementa la ley completa: seed EN AccumSpace, compose entero en AccumSpace y
        ''' UN unico pase final AccumSpace-&gt;OutputSpace al cerrar. Solo con esto el flag puede tener efecto.</summary>
        FourSpaceAccumulator = 1
    End Enum

    ''' <summary>⭐ Espacio del ACUMULADOR del canal. UNICA fuente de verdad, para CPU y GL.
    ''' <para>POR QUE EXISTE: el acumulador es UN buffer por canal (los <c>accR/accG/accB</c> del CPU, la textura
    ''' de ping-pong del GL) que sobrevive a TODAS las fases — seed, region swaps y capas de tint. No puede vivir
    ''' en dos espacios a la vez, asi que su espacio NO puede salir del bucket de cada fase: si el bucket Swap
    ''' dijera "acumular en composite" y el del canal dijera "acumular en output", el swap y el tint estarian
    ''' escribiendo el mismo buffer en espacios distintos. Se resuelve entonces SIEMPRE con
    ''' <c>forSwap:=False</c> — el bucket del CANAL manda — y el <c>AccumInCompositeSpace</c> del bucket Swap NO
    ''' participa (su WorkingSpace/CompositeSpace/SrcSpace si, que son de la FASE, no del storage).</para>
    ''' <para>⛔ Llamar a esto y NO a ResolveConvention(...).AccumSpace en los caminos del acumulador: asi los dos
    ''' compositores leen el MISMO valor por construccion, y sigue siendo correcto si el usuario cambia los
    ''' settings (hoy en N/S es un no-op porque cs==os==Linear, pero deja de serlo si los cambia).</para>
    ''' <para>⛔ NO TOMA <c>forBake</c> A PROPOSITO. La version previa de este comentario afirmaba que la
    ''' convencion forkea con ese flag ("OutputSpace del diffuse G22 al hornear y Srgb al renderizar") y que por
    ''' eso el parametro tenia que ser obligatorio: ERA FALSO. <see cref="ResolveConvention"/> NO referencia
    ''' <c>forBake</c> ni una vez — la ley es UNICA para render y bake por decision de diseño (WYSIWYG, el render
    ''' replica el bake), como ya documenta el propio parametro alla. Un parametro obligatorio que no acopla nada
    ''' es falsa seguridad, asi que se saca en vez de propagarlo.</para></summary>
    ''' <param name="cpuMirror">Capacidad del compositor CPU que espeja ESTE camino. Sin default: el caller es
    ''' el unico que sabe con quien tiene que mantener paridad, y asumirlo es justamente como se rompio antes.</param>
    Public Function AccumSpaceForChannel(channel As FaceTintChannel,
                                         cpuMirror As FaceTintCpuMirrorCapability) As FaceTintWorkingSpace
        Dim c = ResolveConvention(False, 0US, 0, channel, False, forSwap:=False)
        If cpuMirror = FaceTintCpuMirrorCapability.OutputSpaceOnly Then Return c.OutputSpace
        Return c.AccumSpace
    End Function

    ' =====================================================================================================
    ' ADVERTENCIAS DE CONVENCION (latcheadas, always-on).
    ' =====================================================================================================
    ' ⛔ POR QUE NO ALCANZA `Logger.LogLazy`: sale por `If Enabled = False Then Exit Sub`, y `Logger.Enabled`
    ' esta APAGADO en release. Una advertencia que solo existe en debug NO es una advertencia — es la
    ' degradacion silenciosa que la regla del arnes prohibe ("toda condicion anomala ABORTA o se MARCA").
    ' Aca se MARCA: se latchea el primer caso y el runner lo imprime por su `log()`, que sale siempre (consola
    ' y ventana de progreso). Latcheado y no por evento => no puede spamear por NPC ni por pixel.
    Private ReadOnly _warnLock As New Object()
    Private _swapAccumWarning As String = Nothing

    ''' <summary>Primera advertencia de "el bucket Swap no gobierna el acumulador", o Nothing si no hubo.
    ''' La consume el resumen del bake (BakeAllRunner) para que salga SIEMPRE, tambien en release.</summary>
    Public ReadOnly Property SwapAccumWarning As String
        Get
            SyncLock _warnLock
                Return _swapAccumWarning
            End SyncLock
        End Get
    End Property

    ''' <summary>Limpia la advertencia latcheada. La llama el runner al empezar un barrido.</summary>
    Public Sub ResetConventionWarnings()
        SyncLock _warnLock
            _swapAccumWarning = Nothing
        End SyncLock
    End Sub

    ''' <summary>Latchea (solo la primera vez) el aviso de que el <c>OutputSpace</c> del bucket SWAP no coincide
    ''' con el espacio del acumulador del canal. El acumulador es UN buffer que cruza swaps y tints, asi que lo
    ''' gobierna el bucket del CANAL; el del Swap no participa del storage. Antes del 2026-07-30 la fase de swap
    ''' usaba el bucket Swap, con lo cual un config que separe los dos combos produce una salida DISTINTA de la
    ''' version previa (CPU y GL se movieron juntos: la paridad CPU/GPU NO se rompe).
    ''' <para>El mensaje NO dice "poné Swap.OutputSpace = Diffuse.OutputSpace": eso es falso cuando
    ''' <c>AccumInCompositeSpace</c> esta prendido, porque ahi el acumulador vive en CompositeSpace. Se informa
    ''' el valor CONCRETO del acumulador, que es correcto en los dos casos.</para></summary>
    Public Sub NoteSwapAccumMismatch(channel As FaceTintChannel, swapOutputSpace As Integer, accumSpace As Integer)
        SyncLock _warnLock
            If _swapAccumWarning IsNot Nothing Then Return
            _swapAccumWarning =
                $"Swap.OutputSpace={swapOutputSpace} is not the {channel} accumulator space ({accumSpace}). " &
                "The accumulator is a SINGLE buffer shared by the region-swap and tint phases, so the CHANNEL " &
                "bucket governs it and the swaps composite in " & accumSpace.ToString() & ". Before 2026-07-30 " &
                "the swap phase used the Swap bucket instead, so this config produces a DIFFERENT result than " &
                "the previous version (CPU and GL moved together - CPU/GPU parity is unaffected). Set " &
                $"Swap.OutputSpace = {accumSpace} in CharGen Options to make the two agree."
        End SyncLock
    End Sub

    ''' <summary>⭐ LEY UNICA de la INTENSIDAD de un region swap (el MSDV del preset de FaceMorph), compartida
    ''' por el compositor CPU y el GL. Existe para que no haya DOS escrituras de la misma regla.
    ''' <para>⛔ ESTABAN ESCRITAS DISTINTO (2026-07-30): el CPU hacia <c>Math.Max(0.0, CDbl(sw.Intensity))</c>
    ''' —piso en 0, SIN techo— y el GL <c>Math.Max(0.0F, Math.Min(1.0F, sw.Intensity))</c> —piso Y techo—.
    ''' Con los datos vanilla es un NO-OP (medido: los MSDV estan en [-1, 1]; sobre los 6 NPCs que cargan la
    ''' cola de divergencia, 54 valores, el maximo es 1,0000 exacto), asi que NO era la causa de ninguna
    ''' divergencia CPU/GPU. Se unifica igual: dos caminos que calculan lo mismo no deben estar escritos dos
    ''' veces, porque el dia que uno cambie el otro no lo sigue y el bug no se ve.</para>
    ''' <para>POR QUE [0,1] y no [-1,1]: la intensidad de un swap es "cuanta de esta textura se mezcla".
    ''' Un valor negativo no tiene significado ahi (no se puede aplicar -73 % de una textura) y 0 = sin swap.
    ''' El rango [-1,1] SI es el correcto para los MORPHS, donde el signo es la DIRECCION del slider
    ''' (min/max del MSID de la RACE) — eso lo resuelve MorphEngine, no este camino. Son dos consumidores
    ''' distintos del mismo campo.</para>
    ''' <para>Se clampea en <c>Single</c> (el tipo del campo) y el CPU widenea despues: asi los dos lados
    ''' obtienen EL MISMO numero bit a bit, en vez de uno clampeando en Double y el otro en Single.</para></summary>
    Public Function ClampSwapIntensity(intensity As Single) As Single
        If Single.IsNaN(intensity) Then Return 0.0F
        Return Math.Max(0.0F, Math.Min(1.0F, intensity))
    End Function

    ''' <summary>OutputSpace del acumulador de un canal — el espacio en el que el compose tiene que DEJAR el
    ''' resultado, y el destino del unico pase final. Mismo resolver y mismos argumentos que
    ''' <see cref="AccumSpaceForChannel"/> para que el par (origen, destino) de ese pase no se pueda desalinear.</summary>
    Public Function OutputSpaceForChannel(channel As FaceTintChannel) As FaceTintWorkingSpace
        Return ResolveConvention(False, 0US, 0, channel, False, forSwap:=False).OutputSpace
    End Function

    ''' <summary>Resuelve la convención de composición para una capa+canal según la tabla derivada.
    ''' <para>⛔ SYNC: CPU/GPU compositor — ÉSTE es el punto que hace que el contrato se cumpla por
    ''' construcción: es el ÚNICO lugar donde vive la tabla, y la leen los DOS caminos (el GL de
    ''' <c>FaceTintCompositor.ApplyFaceTintPipeline</c> y el CPU de <c>FaceTintCpuCompositor</c>).
    ''' Cualquier ajuste va acá; hardcodear un valor en un compositor rompe la paridad en silencio, y el
    ''' bake (que es 100 % CPU) validaría un camino distinto del que ve el usuario.</para></summary>
    ''' <param name="isTextureSet">True = TextureSet (disc=2); False = Palette/Mask (disc=1).</param>
    ''' <param name="slot">RACE TintTemplateOption.Slot (12 = SkinTone).</param>
    ''' <param name="blendOp">BlendOp efectivo del resolver (0..4).</param>
    ''' <param name="channel">0=Diffuse, 1=Normal, 2=Specular.</param>
    ''' <param name="useHairPalette">True para Brow LUT (afecta mask conv del D channel).</param>
    ''' <param name="forBake">Mantenido por compat de API (el bake lo pasa True). YA NO forkea: la
    ''' ley es ÚNICA para render Y bake (WYSIWYG, el render replica el bake) — decisión del usuario
    ''' 2026-05-31 ("implementación completa, el render también"). Tanto render como bake acumulan D en
    ''' G22 y lerpean en LINEAR; el único punto abierto es si el RENDER FINAL se muestra en g22 o se
    ''' reconvierte a sRGB, lo cual NO cambia esta tabla (es consumo) y se confirma visualmente.
    ''' ⛔ VERIFICADO 2026-07-30: el cuerpo de esta función NO lo referencia ni una vez. Cualquier doc que
    ''' afirme que la ley forkea con este flag es falsa (pasó en AccumSpaceForChannel) — o se implementa el
    ''' fork acá, o no se afirma.</param>
    Public Function ResolveConvention(isTextureSet As Boolean,
                                      slot As UShort,
                                      blendOp As Integer,
                                      channel As FaceTintChannel,
                                      useHairPalette As Boolean,
                                      Optional forBake As Boolean = True,
                                      Optional forSwap As Boolean = False) As FaceTintConventionSet
        ' La ley vive en Config_App.Setting_FaceTintConvention (los defaults los pone el constructor =
        ' ley derivada; si el usuario los cambia ESOS pasan a ser la ley). Se elige el bucket por
        ' (forSwap / canal) y se copia tal cual. Null-safe: si el config no está cargado, usa los defaults.
        Dim s = ActiveSettings()
        If s Is Nothing Then s = New FaceTintConventionSettings()
        ' Swaps: sólo el DIFFUSE swap tiene bucket propio (s.Swap). Los swaps de Normal/Specular usan la
        ' MISMA convención que su tint (s.NormalSpecular). Sólo el diffuse cambia.
        Dim bucket As FaceTintBucketConvention =
            If(forSwap AndAlso channel = FaceTintChannel.Diffuse, s.Swap,
               If(channel = FaceTintChannel.Diffuse, s.Diffuse, s.NormalSpecular))

        Dim c As FaceTintConventionSet
        c.WorkingSpace = bucket.WorkingSpace
        c.CompositeSpace = bucket.CompositeSpace
        c.SrcSpace = bucket.SrcSpace
        c.OutputSpace = bucket.OutputSpace
        c.MaskConv = bucket.MaskConv
        c.Framework = bucket.Framework
        c.SoftLight = bucket.SoftLight
        ' Blend: record-driven (MapBlend) en el tint diffuse; Replace en N·S y en swaps. Read-only en UI.
        c.Blend = If(forSwap OrElse channel <> FaceTintChannel.Diffuse, FaceTintBlend.Replace, MapBlend(blendOp))

        ' WORKING SPACE del DIFFUSE = POR BLEND OP, leído del config (PARAMETRIZABLE, no hardcodeado):
        ' s.DiffuseWorkingSpaceByBlend. Defaults engine-faithful (b12 BSFaceCustomizationShader V2 DXBC +
        ' V1 CK builder FUN_140ED0E40, ck_bake_facetint_RULE_verified §4): SoftLight en gamma-2.2 (decode
        ' dst+color, GIMP, re-encode), Normal/Multiply/Overlay/HardLight en LINEAR. Replace cancela el ws por
        ' construcción (Cvt(Cvt(src,ss→ws),ws→cs)=Cvt(src,ss→cs)) ⇒ con los defaults esto es BYTE-IDÉNTICO en
        ' TODA la data vanilla (scan 2026-06-20: 4008/4008 TemplateColors de las 110 RACE de Fallout4.esm+DLCs
        ' son bop 0 ó 3; CERO Multiply/Overlay/HardLight) y sólo corrige RACEs modeadas con bop 1/2/4 (antes en
        ' G22, ≠ engine). El usuario puede cambiar el espacio de cada op en config.json. Fallback (modos 5..19
        ' app-only y config viejo/null) = bucket.WorkingSpace. GL y CPU lo heredan juntos (mismo resolver).
        If channel = FaceTintChannel.Diffuse AndAlso Not forSwap Then
            Dim wsb = s.DiffuseWorkingSpaceByBlend
            c.WorkingSpace = If(wsb IsNot Nothing, wsb.ForBlend(c.Blend, bucket.WorkingSpace), bucket.WorkingSpace)
        End If

        ' SRC SPACE del DIFFUSE = por TIPO DE FUENTE, todo PARAMETRIZABLE (sin override hardcodeado):
        '   · TINT de TEXTURA del diffuse (TextureSet, isTextureSet) + el base-seed → s.DiffuseTextureSrcSpace
        '     (default Srgb: el engine la bindea SRV sRGB/MakeSRGB → entra lineal y el softlight le hace pow(1/2.2)).
        '   · COLOR SÓLIDO de paleta (PaletteMask uColor) → bucket.SrcSpace del Diffuse (default G22: el builder
        '     lo decodea con γ2.2 a cb12; el shader le hace el MISMO pow(1/2.2) ⇒ el softlight ve el byte crudo).
        '   · SWAP (forSwap) → su PROPIO bucket.SrcSpace (= s.Swap.SrcSpace, default Srgb). YA NO se pisa con
        '     DiffuseTextureSrcSpace: el campo Swap.SrcSpace es LIVE/configurable. (El path exacto del swap quedó
        '     inconcluso en el RE del engine — no hay pre-pass byte-space separado en el FaceTint bake; default
        '     Srgb = best-fit vs CK, pendiente de test controlado para fijar G22-vs-Srgb.) El bucket ya se eligió
        '     arriba (forSwap+Diffuse → s.Swap), así que para el swap bucket.SrcSpace YA es Swap.SrcSpace.
        '   · N/S → su bucket.SrcSpace (Linear). Las MÁSCARAS espaciales no entran (van crudas + MaskConv).
        ' GL==CPU (mismo resolver). Cada fuente usa SU campo de config; nadie pisa a nadie.
        If channel = FaceTintChannel.Diffuse Then
            c.SrcSpace = If(isTextureSet, s.DiffuseTextureSrcSpace, bucket.SrcSpace)
        End If

        ' ⭐ AccumSpace se resuelve AL FINAL, A PROPOSITO: `c.WorkingSpace` lo pisa el override por-blend-op de
        ' arriba (:547-550) y resolverlo antes tomaria el valor previo a ese override.
        ' ⛔ NO decir "y c.CompositeSpace depende de forBake": es FALSO. `c.CompositeSpace` se asigna una sola
        ' vez, `= bucket.CompositeSpace`, y nada mas en este cuerpo lo toca; `forBake` no se lee NUNCA (ver el
        ' <param> de arriba). Esa frase estuvo escrita aca 60 lineas debajo del comentario que la declara falsa.
        ' El compositor —CPU y GL— NO ve el booleano: ve un espacio CONCRETO.
        ' Default (AccumInCompositeSpace=False) => OutputSpace.
        c.AccumSpace = If(bucket.AccumInCompositeSpace, c.CompositeSpace, c.OutputSpace)

        Return c
    End Function

    ''' <summary>Mapea BlendOp numérico (0..4) al enum FaceTintBlend. Fuera de rango → Replace.</summary>
    Public Function MapBlend(blendOp As Integer) As FaceTintBlend
        Select Case blendOp
            Case 1 : Return FaceTintBlend.Multiply
            Case 2 : Return FaceTintBlend.Overlay
            Case 3 : Return FaceTintBlend.SoftLight
            Case 4 : Return FaceTintBlend.HardLight
            Case Else : Return FaceTintBlend.Replace
        End Select
    End Function

End Module
