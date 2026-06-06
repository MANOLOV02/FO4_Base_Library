Imports System

''' <summary>
''' Convención de composición FaceTint configurable por estrato. Centraliza la tabla derivada
''' empíricamente (single-layer B03-06, 2026-05-28) en UN resolver, con enums, para que WS / FW /
''' MaskConv / Blend sean cambiables y unificables sin tocar el shader ni el builder.
'''
''' Modelo derivado (ver memoria arch_facetint_mask_src_conventions):
'''   - mask conv = G22Encode universal (outlier: Brow D = Raw, post-LUT)
'''   - ws: SkinTone→G22 ; TextureSet+softlight→Srgb ; resto→Linear (default)
'''   - blend: BlendOp 0→Replace, 3→SoftLight (1/2/4 mapean directo)
'''   - framework: D = AdditiveOverBase ; N/S = mask-gated (binary-cov)
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
    End Enum

    Public Class FaceTintResolutionSettings
        Public Property Diffuse As FaceTintChannelResolution = FaceTintChannelResolution.Inherit
        Public Property Normal As FaceTintChannelResolution = FaceTintChannelResolution.Inherit
        Public Property Specular As FaceTintChannelResolution = FaceTintChannelResolution.Inherit
        ''' <summary>Compresión del diffuse de salida (flag, default BC3). N/S siempre BC5.</summary>
        Public Property DiffuseCompression As FaceTintDiffuseCompression = FaceTintDiffuseCompression.Bc3
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

    ''' <summary>La ley FaceTint completa, persistida en Config_App (config.json). Los defaults del
    ''' constructor = la ley derivada actual (byte-match con CK si no se tocan). Si el usuario los cambia
    ''' (UI o config.json) ESOS pasan a ser la ley: ResolveConvention los lee siempre. Sin nulos, sin
    ''' capa de override — KISS.</summary>
    Public Class FaceTintConventionSettings
        Public Property Diffuse As FaceTintBucketConvention
        Public Property NormalSpecular As FaceTintBucketConvention
        Public Property Swap As FaceTintBucketConvention
        Public Property SeedDiffuseG22 As Boolean

        Public Sub New()
            ' Defaults = ley derivada actual. Diffuse: blend tonal en G22. N·S: datos lineales (raw).
            ' Swap: convención del DIFFUSE swap (los swaps de N·S usan el bucket NormalSpecular, no éste).
            ' Cambiar acá = cambiar el default de fábrica.
            Diffuse = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Srgb,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Srgb,
                .OutputSpace = FaceTintWorkingSpace.Srgb,
                .MaskConv = FaceTintMaskConv.SrgbEncode,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp}
            NormalSpecular = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Linear,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Linear,
                .OutputSpace = FaceTintWorkingSpace.Linear,
                .MaskConv = FaceTintMaskConv.SrgbEncode,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp}
            Swap = New FaceTintBucketConvention With {
                .WorkingSpace = FaceTintWorkingSpace.Srgb,
                .CompositeSpace = FaceTintWorkingSpace.Linear,
                .SrcSpace = FaceTintWorkingSpace.Srgb,
                .OutputSpace = FaceTintWorkingSpace.Srgb,
                .MaskConv = FaceTintMaskConv.SrgbEncode,
                .Framework = FaceTintFramework.OverPrev,
                .SoftLight = FaceTintSoftLight.Gimp}
            SeedDiffuseG22 = False
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
