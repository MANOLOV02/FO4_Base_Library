Imports System

''' <summary>
''' Convención de composición FaceTint configurable por estrato. Centraliza la tabla derivada
''' empíricamente (single-layer B03-06, 2026-05-28) en UN resolver, con enums, para que WS / FW /
''' MaskConv / Blend sean cambiables y unificables sin tocar el shader ni el builder.
'''
''' Modelo ENGINE-FAITHFUL (re-derivado del b12 BSFaceCustomizationShader, V2 DXBC + V1 CK builder
''' FUN_140ED0E40 — ck_bake_facetint_RULE_verified; reemplaza el modelo empírico "ws=entry_type" que
''' arch_facetint_mask_src_conventions describía y que el shader REFUTÓ):
'''   - mask conv = G22Encode (= shader pow(mask,1/2.2)), universal D y N/S.
'''   - DIFFUSE: acumulador en G22 (os), lerp por cobertura en LINEAR (cs). El BLEND OP corre en su
'''     espacio intrínseco del engine: SoftLight en G22 (modelo GIMP, sqrt siempre) ; Normal/Multiply/
'''     Overlay/HardLight en LINEAR. El ws NO depende de entry_type/slot — depende del blend op (lo
'''     aplica ResolveConvention: G22 sólo para SoftLight). Replace cancela el ws (irrelevante).
'''   - colores de capa en SrcSpace=G22 (= los colores pre-decodificados a lineal del engine).
'''   - N/S: todo Linear, lerp puro por el MISMO alpha del diffuse (sin blend op, sin gamma).
'''   - framework = OverPrev (over-running); seed D = srgb→g22.
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
        Bc5 = 0
        Uncompressed = 1
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
    '''                  composite-lerp va en LINEAR-light (D/N/S). Para el render (forBake=False) =
    '''                  WorkingSpace, con lo que el shader generalizado se reduce al modelo previo
    '''                  (lerp en working) y el render queda BYTE-IDÉNTICO.
    '''   OutputSpace  = espacio de almacenamiento del acumulador. D render=Srgb (storage encode al
    '''                  escribir el DDS) ; D bake=G22 (ley gen3, acumula en g22) ; N/S=Linear (datos).
    ''' El compositor convierte prev(OutputSpace)->WorkingSpace y src(SrcSpace)->WorkingSpace, blendea,
    ''' luego prev/blend->CompositeSpace, lerpea por cov, y devuelve CompositeSpace->OutputSpace.
    ''' Sin ramas hardcodeadas en el compositor: toda la ley vive ACÁ, parametrizada por
    ''' (canal/entry/slot/blendOp/flags/useHairPalette/forBake). Tunear = cambiar esta tabla.</summary>
    Public Structure FaceTintConventionSet
        Public WorkingSpace As FaceTintWorkingSpace
        Public CompositeSpace As FaceTintWorkingSpace
        Public SrcSpace As FaceTintWorkingSpace
        Public OutputSpace As FaceTintWorkingSpace
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
        ''' <summary>SrcSpace de las capas TextureSet-diffuse (textura de COLOR) del DIFFUSE. Engine-faithful =
        ''' Srgb: el engine bindea las texturas color como SRV sRGB (MakeSRGB FUN_14183e1c0) → el shader las
        ''' recibe ya lineales (IEC), por eso NO les hace pow(). El color SÓLIDO (uColor PaletteMask/QNAM) NO
        ''' pasa por acá: usa Diffuse.SrcSpace (G22, pre-decode γ2.2 puro del CK bake, DAT_142F99744). Las
        ''' MÁSCARAS tampoco (van crudas + MaskConv g22encode). Parametrizable; default Srgb.</summary>
        Public Property DiffuseTextureSrcSpace As FaceTintWorkingSpace
        Public Property SeedDiffuseG22 As Boolean
        ''' <summary>Hair/brow grayscale→palette LUT lookup: gamma-encode (pow 1/2.2) las COORDENADAS antes
        ''' de samplear el LUT — engine-faithful. Verificado del binario (RE_RECOLOR_PALETTE §2a, BGEM rec1103/
        ''' rec550 + prepass BSLighting rec2637): el engine samplea Palette[U,V] con U=pow(diffuse.G,1/2.2) y
        ''' V=pow(GrayscaleToPaletteScale,1/2.2)·texcoord, NO con el verde/scale crudos. Aplica al brow (slot 23,
        ''' UseHairPalette) en ambos compositores y al recolor de pelo en el shader. Parametrizable (config.json);
        ''' default True. False = comportamiento legacy (coords crudas, brow==pelo pero ambos ≠ engine).</summary>
        Public Property HairLutCoordGamma As Boolean

        Public Sub New()
            ' Defaults = ley derivada actual. Diffuse: blend tonal en G22. N·S: datos lineales (raw).
            ' Swap: convención del DIFFUSE swap (los swaps de N·S usan el bucket NormalSpecular, no éste).
            ' Cambiar acá = cambiar el default de fábrica.
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
            Swap = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.G22,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.G22,
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
            ' BAKE-faithful = coords CRUDAS (medido vs vanilla en la región de ceja: crudo 6.55 vs pow 16.77).
            ' El CK FaceGen bake NO pow-ea las coords del LUT; el pow(1/2.2) (RE_RECOLOR_PALETTE §2a) es del
            ' path RUNTIME, no del bake. Default False (crudo). True disponible para el render runtime si se cablea.
            HairLutCoordGamma = False
        End Sub
    End Class

    ''' <summary>Seed del diffuse en G22 (lo leen ambos compositores: GL y CPU). Vive en el config; esto
    ''' sólo lo reenvía, null-safe. Los 2 lectores no cambian (lo usan como Boolean de sólo lectura).</summary>
    Public ReadOnly Property SeedConventionIs_G22 As Boolean
        Get
            Dim s = Config_App.Current?.Setting_FaceTintConvention
            Return s IsNot Nothing AndAlso s.SeedDiffuseG22
        End Get
    End Property

    ''' <summary>Hair/brow LUT: pow(1/2.2) en las coordenadas del lookup (engine-faithful). Lo leen ambos
    ''' compositores (GL y CPU). Null-safe; default True si el config no está cargado.</summary>
    Public ReadOnly Property HairLutCoordGammaEnabled As Boolean
        Get
            Dim s = Config_App.Current?.Setting_FaceTintConvention
            Return s IsNot Nothing AndAlso s.HairLutCoordGamma   ' default False (crudo = bake-faithful)
        End Get
    End Property

    ''' <summary>Slot SkinTone (RACE TintTemplateOption.Slot). Centralizado para no hardcodear 12.</summary>
    Private Const SLOT_SKINTONE As UShort = 12US

    ''' <summary>Resuelve la convención de composición para una capa+canal según la tabla derivada.
    ''' Es el ÚNICO lugar donde vive la tabla — cambiar acá unifica/ajusta el compositor entero.</summary>
    ''' <param name="isTextureSet">True = TextureSet (disc=2); False = Palette/Mask (disc=1).</param>
    ''' <param name="slot">RACE TintTemplateOption.Slot (12 = SkinTone).</param>
    ''' <param name="blendOp">BlendOp efectivo del resolver (0..4).</param>
    ''' <param name="channel">0=Diffuse, 1=Normal, 2=Specular.</param>
    ''' <param name="useHairPalette">True para Brow LUT (afecta mask conv del D channel).</param>
    ''' <param name="forBake">Mantenido por compat de API (el bake lo pasa True). YA NO forkea: la
    ''' ley es ÚNICA para render Y bake (WYSIWYG, el render replica el bake) — decisión del usuario
    ''' 2026-05-31 ("implementación completa, el render también"). Tanto render como bake acumulan D en
    ''' G22 y lerpean en LINEAR; el único punto abierto es si el RENDER FINAL se muestra en g22 o se
    ''' reconvierte a sRGB, lo cual NO cambia esta tabla (es consumo) y se confirma visualmente.</param>
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
        Dim s = Config_App.Current?.Setting_FaceTintConvention
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

        ' SRC SPACE engine-faithful para capas TextureSet-diffuse (textura de COLOR): el engine las bindea
        ' como SRV sRGB (MakeSRGB) → entran IEC-lineales (el shader no las pow()). El color SÓLIDO (uColor de
        ' PaletteMask) NO entra acá: usa bucket.SrcSpace (G22, γ2.2 puro del CK bake). Las máscaras tampoco
        ' (van crudas + MaskConv). Parametrizable (s.DiffuseTextureSrcSpace, default Srgb). GL==CPU (mismo
        ' resolver). Nota: con ForceUniformColor (brow-tint con HCLF) el src es uColor aunque isTextureSet —
        ' caso borde raro; el delta IEC-vs-γ2.2 sobre un color sólido es <1/255, despreciable.
        If channel = FaceTintChannel.Diffuse AndAlso Not forSwap AndAlso isTextureSet Then
            c.SrcSpace = s.DiffuseTextureSrcSpace
        End If

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
