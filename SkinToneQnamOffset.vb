''' <summary>Ajuste manual del SKIN TONE del cuerpo (QNAM / "Texture lighting"): un delta por canal
''' + un delta de intensidad, autorado por el usuario en el editor de cuerpo cuando el cuerpo y la cara
''' vienen de mods distintos y el tono no matchea con las reglas del motor.
'''
''' <para><b>Dominio CANÓNICO (no de UI):</b> los cuatro campos son deltas en el espacio del dato, NO en
''' bytes. R/G/B viven en [-1..1] porque el color de la capa de skin-tone es TINC/255 (FO4 y SSE por igual)
''' e <see cref="Intensity"/> vive en [-1..1] porque su dato es un float [0..1]: en FO4 es el alpha del QNAM
''' (opacidad del soft-light del cuerpo, <c>NPC_TextureLightingFloats.A</c>) y en SSE es el interp TINV/100
''' que se PLIEGA dentro del color (el QNAM de SSE no tiene alpha). La UI muestra R/G/B en bytes (×255) y la
''' intensidad en porcentaje (×100) — la conversión vive ACÁ y en ningún otro lado, así que el ±255 no queda
''' horneado como si fuera el dominio del campo (el QNAM en disco son FLOATS, no bytes).</para>
'''
''' <para><b>Dónde se aplica:</b> SÓLO donde el QNAM se materializa como "tono del CUERPO" — el
''' <c>state.TextureLightingColor</c> del render y el <c>shadow.TextureLighting*</c> del save/bake. NUNCA
''' dentro de la resolución compartida del skin tone: la CARA (compositor de facetint, sentinels skee, preset
''' −2 del bake) lee esa misma resolución y el offset la movería — y el punto entero de la feature es que el
''' ORIGEN no se mueva mientras el destino lo persigue.</para></summary>
Public Class SkinToneQnamOffset

    ''' <summary>Unidad de UI de R/G/B: byte del color (TINC). El campo canónico es la fracción.</summary>
    Public Const RgbUiScale As Single = 255.0F
    ''' <summary>Unidad de UI de la intensidad: porcentaje. El campo canónico es la fracción.</summary>
    Public Const IntensityUiScale As Single = 100.0F

    Public Property R As Single = 0.0F
    Public Property G As Single = 0.0F
    Public Property B As Single = 0.0F
    Public Property Intensity As Single = 0.0F

    ''' <summary>True cuando el ajuste no cambia nada. Lo consultan el gate de persistencia (una fila de
    ''' sidecar que no hace nada no se escribe) y los call sites que se saltean el trabajo con offset cero.</summary>
    Public ReadOnly Property IsZero As Boolean
        Get
            Return R = 0.0F AndAlso G = 0.0F AndAlso B = 0.0F AndAlso Intensity = 0.0F
        End Get
    End Property

    Public Function Clone() As SkinToneQnamOffset
        Return New SkinToneQnamOffset With {.R = R, .G = G, .B = B, .Intensity = Intensity}
    End Function

    ''' <summary>Clon defensivo tolerante a Nothing (Nothing ⇒ Nothing = "sin ajuste autorado").</summary>
    Public Shared Function CloneOrNothing(src As SkinToneQnamOffset) As SkinToneQnamOffset
        If src Is Nothing Then Return Nothing
        Return src.Clone()
    End Function

    ' ===== Conversión UI <-> canónico. UN solo lugar. =====

    Public Property RUi As Single
        Get
            Return R * RgbUiScale
        End Get
        Set(value As Single)
            R = value / RgbUiScale
        End Set
    End Property

    Public Property GUi As Single
        Get
            Return G * RgbUiScale
        End Get
        Set(value As Single)
            G = value / RgbUiScale
        End Set
    End Property

    Public Property BUi As Single
        Get
            Return B * RgbUiScale
        End Get
        Set(value As Single)
            B = value / RgbUiScale
        End Set
    End Property

    ''' <summary>Intensidad en PORCENTAJE (-100..100). El campo canónico es la fracción (-1..1).</summary>
    Public Property IntensityUi As Single
        Get
            Return Intensity * IntensityUiScale
        End Get
        Set(value As Single)
            Intensity = value / IntensityUiScale
        End Set
    End Property

    ''' <summary>Aplica los deltas de color a una terna ya normalizada a [0..1] y clampea al dominio del
    ''' dato. NO toca la intensidad: quién la consume depende del juego (alpha en FO4, interp plegado en SSE)
    ''' y esa decisión vive en el resolver de cada juego, no acá.</summary>
    Public Sub ApplyToRgb01(ByRef r01 As Double, ByRef g01 As Double, ByRef b01 As Double)
        r01 = Clamp01(r01 + R)
        g01 = Clamp01(g01 + G)
        b01 = Clamp01(b01 + B)
    End Sub

    ''' <summary>Aplica el delta de intensidad a un valor ya normalizado a [0..1].</summary>
    Public Function ApplyToIntensity01(v01 As Double) As Double
        Return Clamp01(v01 + Intensity)
    End Function

    Public Shared Function Clamp01(v As Double) As Double
        If v < 0.0R Then Return 0.0R
        If v > 1.0R Then Return 1.0R
        Return v
    End Function

    Public Overrides Function ToString() As String
        Return $"R={RUi:0.#} G={GUi:0.#} B={BUi:0.#} I={IntensityUi:0.#}%"
    End Function
End Class
