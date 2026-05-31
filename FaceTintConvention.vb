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
    End Enum

    ''' <summary>Cómo cov, base, src y blend se combinan en el resultado.
    ''' Derivado: D = AdditiveOverBase (delta vs base original) ; N/S = mask-gated (additive de la
    ''' desviación, binary-cov). MixTowardSrc y BlendWithModulatedSrc quedan expuestos para A/B.</summary>
    Public Enum FaceTintFramework
        AdditiveOverBase = 0
        MixTowardSrc = 1
        BlendWithModulatedSrc = 2
    End Enum

    ''' <summary>Operador de blend efectivo (mapea desde BlendOp 0..4 del record / resolver).
    ''' 0=Replace(blendDefault), 1=Multiply, 2=Overlay, 3=SoftLight, 4=HardLight.</summary>
    Public Enum FaceTintBlend
        Replace = 0
        Multiply = 1
        Overlay = 2
        SoftLight = 3
        HardLight = 4
    End Enum

    ''' <summary>Convención completa para una capa+canal. Inmutable; producida por ResolveConvention.
    ''' Tres espacios (el shader es agnóstico y solo aplica estos via uniforms):
    '''   SrcSpace     = espacio del color de la capa (textura). D=color sRGB ; N/S=datos lineales (raw).
    '''   WorkingSpace = espacio donde corre el blend. alpha-over(replace)=Linear (mezcla física) ;
    '''                  blend-mode tonal (softlight/etc)=G22 (estilo Photoshop, sobre valores encoded).
    '''   OutputSpace  = espacio de almacenamiento del acumulador. D=G22 (formato diffuse del engine) ;
    '''                  N/S=Linear (datos).
    ''' El shader convierte prev(OutputSpace)->WorkingSpace y src(SrcSpace)->WorkingSpace, blendea,
    ''' y devuelve WorkingSpace->OutputSpace. Sin ramas hardcodeadas en el shader.</summary>
    Public Structure FaceTintConventionSet
        Public WorkingSpace As FaceTintWorkingSpace
        Public SrcSpace As FaceTintWorkingSpace
        Public OutputSpace As FaceTintWorkingSpace
        Public MaskConv As FaceTintMaskConv
        Public Framework As FaceTintFramework
        Public Blend As FaceTintBlend
    End Structure

    ''' <summary>Cuando True, ResolveConvention devuelve la convención LEGACY (el render que ya
    ''' funcionaba: ws implícito g22 para diffuse vía ConvertDiffuseBaseToGamma22, mask SrgbOpacity,
    ''' additive-over-base). Cuando False (default nuevo), devuelve la tabla DERIVADA single-layer.
    ''' Permite A/B sin recompilar lógica: flip de un flag. La validación full-stack contra B01-B02
    ''' decide cuál queda.</summary>
    Public Property UseLegacyConvention As Boolean = False

    ''' <summary>Slot SkinTone (RACE TintTemplateOption.Slot). Centralizado para no hardcodear 12.</summary>
    Private Const SLOT_SKINTONE As UShort = 12US

    ''' <summary>Resuelve la convención de composición para una capa+canal según la tabla derivada.
    ''' Es el ÚNICO lugar donde vive la tabla — cambiar acá unifica/ajusta el compositor entero.</summary>
    ''' <param name="isTextureSet">True = TextureSet (disc=2); False = Palette/Mask (disc=1).</param>
    ''' <param name="slot">RACE TintTemplateOption.Slot (12 = SkinTone).</param>
    ''' <param name="blendOp">BlendOp efectivo del resolver (0..4).</param>
    ''' <param name="channel">0=Diffuse, 1=Normal, 2=Specular.</param>
    ''' <param name="useHairPalette">True para Brow LUT (afecta mask conv del D channel).</param>
    Public Function ResolveConvention(isTextureSet As Boolean,
                                      slot As UShort,
                                      blendOp As Integer,
                                      channel As FaceTintChannel,
                                      useHairPalette As Boolean) As FaceTintConventionSet
        Dim c As FaceTintConventionSet
        Dim blend As FaceTintBlend = MapBlend(blendOp)

        If UseLegacyConvention Then
            ' Reproduce el render previo: mask sRGB, ws gamma para diffuse (vía base→g22), additive,
            ' salida sRGB (OutputSpace=Srgb) y src/base tratados como sRGB.
            c.MaskConv = FaceTintMaskConv.SrgbEncode
            c.WorkingSpace = If(channel = FaceTintChannel.Diffuse, FaceTintWorkingSpace.G22, FaceTintWorkingSpace.Linear)
            c.SrcSpace = FaceTintWorkingSpace.Srgb
            c.OutputSpace = FaceTintWorkingSpace.Srgb
            c.Framework = FaceTintFramework.AdditiveOverBase
            c.Blend = blend
            Return c
        End If

        ' ===== Convención KISS con LÓGICA FÍSICA (2026-05-29, validada batches+source vs CK) =====
        ' El espacio lo determina el TIPO DE OPERACIÓN, no el tipo de capa:
        '   - alpha-over / replace (bop0): mezcla de color por cobertura -> luz LINEAL (físico).
        '       Medido: tattoo/dirt/lipstick replace en linear = 87-98% byte (vs g22 que rompe).
        '   - blend-mode tonal softlight (bop3, y mult/overlay/hardlight): definido sobre valores
        '       gamma-encoded (estilo Photoshop) -> G22. Medido: scars/arrugas/skintone en g22 = 90-99%.
        '   Storage (OutputSpace): Diffuse = G22 (formato diffuse del engine) ; N/S = Linear (datos).
        '   Src: Diffuse = color sRGB ; N/S = datos lineales (raw).
        ' Reemplaza las 3 ramas previas por-tipo (is_ts/is_skin) por UNA regla gateada por el blend.
        c.Framework = FaceTintFramework.AdditiveOverBase

        ' Mask conv: G22Encode universal. Outlier: Brow D (post-LUT) usa Raw.
        c.MaskConv = If(channel = FaceTintChannel.Diffuse AndAlso useHairPalette,
                        FaceTintMaskConv.Raw, FaceTintMaskConv.G22Encode)

        If channel = FaceTintChannel.Diffuse Then
            c.Blend = blend                          ' blend del record
            c.SrcSpace = FaceTintWorkingSpace.Srgb   ' textura de color
            ' OutputSpace = espacio del ACUMULADOR (ping-pong float, roundtrip sin perdida) = Srgb.
            ' El engine almacena el diffuse en g22, pero ese encode de STORAGE se aplica al ESCRIBIR
            ' el DDS (NpcFaceGenPacker/FaceGenBuilder), NO en el compositor. Acumulador sRGB =
            ' equivalente matematico a acumular en g22 (probado) con ping-pong float, sin pre-pass.
            c.OutputSpace = FaceTintWorkingSpace.Srgb
            ' replace = alpha-over -> Linear ; cualquier blend-mode tonal -> G22
            c.WorkingSpace = If(blend = FaceTintBlend.Replace,
                                FaceTintWorkingSpace.Linear, FaceTintWorkingSpace.G22)
        Else
            ' N/S: datos lineales. replace (alpha-over) en lineal; sin gamma en src/output.
            c.Blend = FaceTintBlend.Replace
            c.SrcSpace = FaceTintWorkingSpace.Linear
            c.OutputSpace = FaceTintWorkingSpace.Linear
            c.WorkingSpace = FaceTintWorkingSpace.Linear
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
