' Rig de luces del previewer GL (4 direccionales + ambient hemisférico), compartido FO4/SSE.
'
' ⛔ SIN COMPATIBILIDAD con el `Setting_Lightrig` / `LightsRig_struct` anterior: aquel guardaba los
' colores como `System.Numerics.Vector3`, cuyos X/Y/Z son CAMPOS, y System.Text.Json (IncludeFields =
' False por default) los serializaba como `{}` -> al releer volvían (0,0,0) = ambient NEGRO, mientras
' que la UI los mostraba blancos (fallback del swatch) y los "arreglaba" en memoria al abrir el diálogo.
' De ahí el síntoma: preview oscuro que cambiaba solo con abrir el rig. Acá TODO son PROPIEDADES de
' tipos primitivos => round-trip exacto por JSON, sin converters ni fallbacks "(0,0,0) = blanco".
'
' El rig es POR JUEGO (Config_App.Setting_LightRig_FO4 / _SSE, igual que las opciones de CharGen).
' Nadie toca los campos por juego directamente: Render y LightRigForm van SIEMPRE por
' Config_App.Current.ActiveLights() / SetActiveLights(), así cambiar de juego cambia el rig solo.

''' <summary>Color RGB 0..1 del rig. Propiedades (no campos) para que serialice; sin semántica oculta:
''' negro es negro. Apagar una luz = Strength 0.</summary>
Public Structure RigColor
    Public Property R As Single
    Public Property G As Single
    Public Property B As Single

    Public Sub New(r As Single, g As Single, b As Single)
        Me.R = r : Me.G = g : Me.B = b
    End Sub

    Public Shared ReadOnly Property White As RigColor
        Get
            Return New RigColor(1, 1, 1)
        End Get
    End Property

    ''' <summary>Color tal cual (espacio perceptual/sRGB). El render lo lineariza al subirlo (pow 2.2).</summary>
    Public Function ToVector3() As OpenTK.Mathematics.Vector3
        Return New OpenTK.Mathematics.Vector3(R, G, B)
    End Function

    Public Function ToColor() As Drawing.Color
        Dim ch = Function(f As Single) As Integer
                     Return Math.Max(0, Math.Min(255, CInt(Math.Round(f * 255.0F))))
                 End Function
        Return Drawing.Color.FromArgb(255, ch(R), ch(G), ch(B))
    End Function

    Public Shared Function FromColor(c As Drawing.Color) As RigColor
        Return New RigColor(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F)
    End Function
End Structure

''' <summary>Una direccional del rig: fuerza + color + su dirección en el MUNDO, como azimut y elevación.
'''
''' <para>⛔⛔ LAS LUCES SON FIJAS AL MUNDO. Hasta 2026-08-11 la dirección se construía con 6
''' multiplicadores relativos a la base de la CÁMARA (Up/Down/Left/Right/Forward/Back), así que el rig
''' entero giraba al orbitar. Con sombras proyectadas eso es indefendible: la sombra de la nariz barre la
''' cara mientras el usuario gira, y nadie lo lee como "iluminación consistente". Un estudio real —y el
''' motor— tienen las luces quietas y es el modelo el que se gira.</para>
'''
''' <para>⛔ NO HAY CONVERSIÓN DESDE EL ESQUEMA VIEJO, y este párrafo la describía. Existió una
''' <c>FromCameraRelative</c> que convertía los 6 multiplicadores evaluándolos en la vista por defecto
''' —donde la base de cámara ES la del mundo (<c>Forward=(0,1,0)</c>, <c>right=(1,0,0)</c>), así que la
''' conversión no perdía nada—, y se borró entera cuando se re-autoraron los presets: convertir un rig
''' viejo lo dejaría fuera del set nuevo igual. Hoy tampoco hay VERSIÓN ni reparación: la propiedad que
''' persiste el rig se renombró, así que un rig viejo no se convierte ni se pisa — no se lee. Ver el
''' bloque de <c>Setting_LightRig_*</c> en <c>Config_Class.vb</c>.</para>
'''
''' <para>Convención: azimut 0 = +Y del mundo (de donde mira la cámara por defecto), creciendo hacia +X;
''' elevación 0 = horizonte, +90 = cenital. Es la misma fórmula que ya usa la cámara orbital, así que los
''' dos sistemas de ángulos del previewer coinciden.</para></summary>
Public Structure PreviewLight
    Public Property Strength As Single
    Public Property Color As RigColor

    ''' <summary>Grados, 0 = +Y del mundo, creciendo hacia +X. Se normaliza a [0,360) al leer.</summary>
    Public Property AzimuthDeg As Single
    ''' <summary>Grados, 0 = horizonte, +90 = arriba. Se acota a [−90,90] al leer.</summary>
    Public Property ElevationDeg As Single

    ''' <summary>Esta luz escribe su propio shadow map y ocluye su propia contribucion.
    ''' <para>⛔ ES PARTE DEL RIG, no de <c>PreviewShadowSettings</c>, y esa division es la que hace que
    ''' la cosa cierre: "que luces castean" es una propiedad de la LUZ y viaja con el preset; "como se
    ''' dibujan las sombras" (calidad, suavidad, oscuridad, bias, receptor de suelo) es global a todas y
    ''' vive del otro lado. Mezclarlas obligaria a versionar dos esquemas por el mismo cambio.</para>
    ''' <para>Default por preset: SOLO la key. Cada luz casteante extra cuesta un mapa de profundidad
    ''' completo —+16 MB a 2048, y +32 si ademas esta el receptor de suelo, que reserva su propio array del
    ''' mismo tamano— mas un lookup de PCF por fragmento; que las cuatro PUEDAN no significa que ninguna
    ''' arranque prendida.</para></summary>
    Public Property CastsShadow As Boolean

    ''' <summary>True si esta luz puede oscurecer algo: castea Y aporta luz.
    ''' <para>⭐ EL SEGUNDO TERMINO NO ES UNA OPTIMIZACION OPORTUNISTA, ES UNA IDENTIDAD: el shader
    ''' multiplica <c>diffuse * factor</c>, y con <c>diffuse = 0</c> el producto es 0 con cualquier factor.
    ''' O sea que una luz apagada con la casilla puesta produce EXACTAMENTE el mismo frame casteando que
    ''' no casteando, y castear le costaria un mapa entero. El epsilon esta en espacio perceptual (que es
    ''' donde vive <c>Diffuse()</c>); en lineal equivale a ~1e-7.</para></summary>
    Public Function CasteaDeVerdad() As Boolean
        If Not CastsShadow Then Return False
        Dim d = Diffuse()
        Return Math.Max(d.X, Math.Max(d.Y, d.Z)) > 0.0005F
    End Function

    ''' <summary>⛔⛔ TODOS LOS CAMPOS, TODOS OBLIGATORIOS, EN UN SOLO PASO. Ninguno es opcional y ninguno
    ''' se completa despues con un <c>With { }</c>.
    '''
    ''' <para>⭐ ESTO NO ES ESTILO: LA CONSTRUCCION EN DOS FASES YA COSTO EL PEOR DEFECTO DE ESTA FEATURE.
    ''' El ctor tomaba (strength, azimut, elevacion), ponia el color en blanco por su cuenta, y el caller
    ''' completaba <c>Color</c> y <c>CastsShadow</c> con un inicializador de objeto. El rig que pinea
    ''' <c>Tools/ShadowGate</c> se escribio asi y se OLVIDO el <c>CastsShadow</c>: como
    ''' <c>PreviewLight</c> es una Structure, el campo quedo en False —el default del TIPO— ninguna luz
    ''' casteo, el arnes entero paso a medir CERO en todos sus A/B de sombra, y encima reventaba con
    ''' NullReferenceException porque el target ni se instanciaba. Un arnes verde que no medi­a nada.
    ''' Con el ctor completo, olvidarse un campo es un ERROR DE COMPILACION.</para>
    '''
    ''' <para>⚠️ Lo que NO se puede evitar en VB: <c>New PreviewLight()</c> sin argumentos existe siempre en
    ''' una Structure y da todos los campos en cero. Por eso el ctor completo es una guia fuerte, no una
    ''' garantia — pero elimina el caso real, que es el de alguien escribiendo una luz a proposito.</para>
    ''' <para>El color va explicito hasta cuando es blanco: que un preset diga <c>RigColor.White</c> es
    ''' informacion (esa luz es neutra a proposito), y que lo pusiera el ctor por su cuenta era una decision
    ''' invisible desde el call site.</para></summary>
    Public Sub New(strength As Single, azimuthDeg As Single, elevationDeg As Single,
                   color As RigColor, castsShadow As Boolean)
        Me.Strength = strength
        Me.Color = color
        Me.AzimuthDeg = azimuthDeg
        Me.ElevationDeg = elevationDeg
        Me.CastsShadow = castsShadow
    End Sub

    ''' <summary>Color × fuerza, en espacio perceptual (el render lineariza).</summary>
    Public Function Diffuse() As OpenTK.Mathematics.Vector3
        Return Color.ToVector3() * Strength
    End Function

    ''' <summary>Dirección superficie→luz en WORLD (Z-up). No depende de la cámara: ése es el punto.</summary>
    Public Function Direction() As OpenTK.Mathematics.Vector3
        Dim az As Double = AzimuthDeg * Math.PI / 180.0
        Dim el As Double = Math.Clamp(CDbl(ElevationDeg), -90.0, 90.0) * Math.PI / 180.0
        Dim cosEl As Double = Math.Cos(el)
        Return New OpenTK.Mathematics.Vector3(CSng(cosEl * Math.Sin(az)),
                                              CSng(cosEl * Math.Cos(az)),
                                              CSng(Math.Sin(el)))
    End Function

End Structure

''' <summary>El rig completo: 4 direccionales + ambient hemisférico de 3 perillas independientes
''' (intensidad / nivel de suelo / tintes). Ver Render.ApplyMaterial y LightRigForm.</summary>
Public Structure PreviewLightRig
    Public Property KeyLight As PreviewLight
    Public Property FillLeft As PreviewLight
    Public Property FillRight As PreviewLight
    Public Property BackLight As PreviewLight

    ''' <summary>Intensidad global del ambient (0..2). Multiplica a los dos hemisferios.</summary>
    Public Property AmbientIntensity As Single
    ''' <summary>Brillo del suelo (normal hacia −Z) como fracción del cielo, 0..1. 1 = ambient plano;
    ''' 0 = suelo negro. Independiente de la intensidad y de los tintes.
    ''' <para>⛔ SE APLICA EN LINEAL, DESPUES del pow 2.2, y por eso no vive dentro de
    ''' <see cref="AmbientGroundDiffuse"/> sino en <c>Render.ResolveFrameLights</c>. Un TINTE se autora en
    ''' espacio perceptual y se decodea al subir —es un color—; esto NO es un color, es un COCIENTE DE
    ''' RADIANCIAS entre los dos hemisferios, y el <c>mix()</c> del shader que lo consume opera en lineal.
    ''' <para>Estuvo del otro lado —multiplicando al color antes del pow— y ahi la perilla entregaba
    ''' <c>nivel^2.2</c>: el 0,45 del Studio llegaba al shader como 17,3 % del cielo, no 45 %. MEDIDO con
    ''' `ShadowGate --ambient` sobre la escena canonica: pintar el hemisferio de abajo de ROJO PURO movia
    ''' 0,48/255 de promedio contra los 3,00 del MISMO gesto sobre el cielo (6,25x menos), y la mitad
    ''' inferior del slider era indistinguible de cero (0,00 vs 0,20 = 0,14/255). O sea la perilla existia,
    ''' llegaba al uniform, y no se sentia.</para>
    ''' <para>⚠️ El cambio REINTERPRETA los configs existentes a proposito (decision del usuario, no se
    ''' migro el esquema): un 0,45 guardado antes valia 17,3 % y ahora vale 45 %, o sea el suelo de todo rig
    ''' ya guardado se aclara ~2,6x. Los presets tampoco se reexpresaron.</para></para></summary>
    Public Property AmbientGroundLevel As Single
    ''' <summary>TINTE del hemisferio de arriba (normal hacia world +Z). Blanco = neutro.</summary>
    Public Property AmbientSkyColor As RigColor
    ''' <summary>TINTE del hemisferio de abajo. El BRILLO lo da <see cref="AmbientGroundLevel"/>.</summary>
    Public Property AmbientGroundColor As RigColor

    ' ==========================================================================================
    ' COMO SE VEN LAS SOMBRAS — parte del RIG, no de PreviewShadowSettings
    ' ==========================================================================================
    ' ⭐ EL CRITERIO ES LA NATURALEZA DE LA PERILLA, no dónde era cómodo ponerla. El rig ya decide QUE
    ' LUCES CASTEAN (PreviewLight.CastsShadow), asi que es incoherente que no pueda decidir COMO SE VEN
    ' esas sombras: la blandura, la oscuridad y si el modelo apoya en el piso son parte del LOOK de un set
    ' de luces, igual que la temperatura o el balance key/fill. Un "Dungeon" con sombra dura y negra y un
    ' "Overcast" con sombra apenas insinuada son dos escenarios distintos, y hasta ahora los cinco presets
    ' compartian una unica configuracion global.
    '
    ' ⛔ Y POR ESO MISMO **NO** SE MUDARON LAS OTRAS CINCO. `MapSize`, `Depth16`, los dos bias y `Enabled`
    ' se quedan en PreviewShadowSettings porque son presupuesto de MAQUINA, no estetica. Si viajaran en el
    ' preset, aplicar "Portrait" le reescribiria la calidad y la VRAM a alguien que puso 1024 porque su
    ' placa no da — un preset de ILUMINACION no tiene por que tocar el presupuesto de video, y el sintoma
    ' (se traba el preview despues de aplicar un preset) no apunta al preset.
    '
    ' ⚠️ Consecuencia deliberada: tocar estas tres AHORA marca el combo de presets como "Custom", cosa que
    ' antes no pasaba. Es correcto — el rig efectivamente dejo de coincidir con el preset. Ver RigCoincide.

    ''' <summary>Radio del kernel de PCF en TEXELES del mapa. Fraccionario: la parte entera son los taps y
    ''' el sobrante viaja en el ESPACIADO, asi que el desenfoque es continuo. Acotado a
    ''' <c>PreviewShadowSettings.MaxPcfRadius</c> al subirlo.</summary>
    Public Property ShadowSoftnessTexels As Single

    ''' <summary>Cuanto oscurece la sombra: <c>factor = 1 - Darkness*(1-crudo)</c>, acotado a [0,1] al
    ''' subirlo. 1 = la luz ocluida se apaga del todo, que es lo que hace el motor. Menos de 1 NO es fiel;
    ''' existe porque en un previewer hace falta leer la textura del lado oscuro.</summary>
    Public Property ShadowDarkness As Single

    ''' <summary>Dibuja la silueta sobre el plano del piso (el "shadow catcher").
    ''' <para>⚠️ NO es gratis: reserva un SEGUNDO array de shadow maps del mismo lado y con las mismas
    ''' capas que el del personaje, o sea que prenderlo DUPLICA la VRAM de la feature.</para></summary>
    Public Property ShadowOnGround As Boolean

    ' ⛔⭐ ESTE RIG NO TIENE VERSION DE ESQUEMA, Y ES A PROPOSITO. Tenia una (`SchemaVersion` +
    ' `CurrentSchemaVersion`) que `Config_App.LoadConfig` comparaba para reponer los defaults; se borraron
    ' las tres cosas. La invalidacion pasó a hacerse RENOMBRANDO la propiedad que persiste el rig
    ' (`Setting_PreviewLights_*` -> `Setting_LightRig_*`), con lo cual el dato viejo no se detecta: no se
    ' lee. Ver el bloque de `Setting_LightRig_*` en Config_Class.vb, que tiene el mecanismo y su precio.
    ' El motivo: una version obliga a mantener una rama que PUEDE no dispararse —y este proyecto ya tuvo
    ' tres mecanismos distintos de invalidacion conviviendo, uno de ellos ciego a "la clave existia y
    ' significaba otra cosa"—. Una clave que no existe no se puede leer mal.

    ''' <summary>El rig por default = preset "Studio" (el primero de <see cref="Presets"/>), que es
    ''' también al que vuelve el botón Reset. Una sola fuente de verdad.</summary>
    Public Shared Function Defaults() As PreviewLightRig
        Return Presets()(0).Rig
    End Function

    ''' <summary>Sets de luces predefinidos. Cada uno es un ESCENARIO coherente (dirección + temperatura
    ''' + relación key/fill/rim + ambiente), no una variación de intensidad.
    '''
    ''' Convención de dirección (la del rig, ver PreviewLight.Direction): Forward = desde la cámara
    ''' hacia el sujeto, Back = contraluz, Up/Down = cenital/contrapicado. ⚠ Right/Left del rig caen
    ''' del lado OPUESTO al de la pantalla (queda del rig anterior): por eso la luz "Left" usa Right y
    ''' viceversa — los presets respetan esa convención para que la etiqueta del grupo siga valiendo.
    '''
    ''' ⛔ DOS TRAMPAS AL DISEÑAR UN SET (las dos me quemaron un intento previo). Render sube
    ''' `Vector_to_Linear(color × strength)` = **pow 2.2 sobre el producto**, así que:
    '''   1. LA SATURACIÓN SE AMPLIFICA. Un tinte "naranja antorcha" (1.00, 0.58, 0.22) llega al shader
    '''      como (1.00, 0.30, 0.035) = rojo casi puro, y si además lo llevan 3 de las 4 luces, TODO el
    '''      modelo se tiñe. Regla: ningún canal por debajo de ~0.62 salvo la key de un escenario
    '''      explícitamente coloreado, y que las otras luces lleven la temperatura CONTRARIA o neutra.
    '''   2. LA FUERZA NO ES LINEAL. strength 0.6 = 0.33 lineal, pero 1.35 = 1.94 = SEIS veces la key de
    '''      Studio -> quemado. Presupuesto de referencia (Studio, que es el calibrado): directas ≈ 0.62
    '''      lineal sumadas, ambient sky 0.83 / ground 0.37. ⚠️ EL GROUND LINEAL CAMBIO SIN QUE SE TOCARA UN
    '''      PRESET: el nivel dejo de ir adentro del pow (ver AmbientGroundLevel), asi que el 0.45 del Studio
    '''      pasa de valer 0.14 lineal a valer 0.37. Los cinco sets se dejaron como estaban a proposito, o
    '''      sea todos tienen hoy el suelo mas claro de lo que tenian cuando se calibraron. Cada set se
    '''      queda cerca de ese total y gasta
    '''      la diferencia en CONTRASTE (dónde está la luz y cuánto baja el ambiente), no en potencia.</summary>
    Public Shared Function Presets() As LightRigPreset()
        ' ⛔ LA KEY DE STUDIO ESTABA A 0 GRADOS DE LA CAMARA (forward:=1 y nada mas), y una luz frontal
        ' pura NO PROYECTA SOMBRA VISIBLE por construccion: lo que ocluye tapa exactamente su propia
        ' sombra. MEDIDO con Tools/ShadowGate sobre cabeza+pelo+cuerpo vanilla: con aquel Studio, prender
        ' las sombras movia 6797 px (1,05 % de la pantalla) con un delta maximo de canal de 23 sobre 255.
        ' El gate `studio-rig` de Tools/ParityGate lo impide desde entonces.
        ' ⭐ LOS ANGULOS SON REDONDOS Y ESO ES DELIBERADO. Estuvieron con 5 decimales porque eran DERIVADOS
        ' de una conversion del esquema viejo y 2 decimales volteaban pixeles sueltos en bordes con
        ' alpha-test (340 px de 648.000 medidos). Esa conversion ya no existe: los sets se autoran a mano,
        ' en multiplos de 15 grados y 0,05 de fuerza, y el golden `rig-presets` los congela con tolerancia
        ' 0,00005 grados — no por precision, sino porque un golden flojo no congela nada.
        ' ⛔ TODA KEY POR ENCIMA DE ShadowMapMath.ElevacionMinimaGrados (11,54). La key de Dungeon estuvo en
        ' -22,29 grados, o sea bajo el horizonte, y con eso ExpandForGroundShadow rechaza el encuadre: el
        ' receptor de suelo quedaba PERMANENTEMENTE deshabilitado en ese preset y la UI se lo decia al
        ' usuario en un cartel. Un preset que apaga una feature no es una eleccion de escenario. Ley 5 de
        ' `studio-rig`.
        ' ⛔ SOLO LA KEY CASTEA EN LOS CINCO SETS. Cada luz casteante extra es un shadow map completo
        ' (+16 MB a 2048) y un lookup de PCF por fragmento; el usuario puede prender las otras tres desde el
        ' dialogo, pero ningun preset se las estrena por el.
        Return New LightRigPreset() {
            New LightRigPreset("Studio",
                "Neutral 3-point + rim. Colourless light for judging textures and materials, with the key off-axis so shaped shadows read.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.7F, azimuthDeg:=45.0F, elevationDeg:=30.0F, color:=RigColor.White, castsShadow:=True),
                    .FillLeft = New PreviewLight(0.35F, azimuthDeg:=300.0F, elevationDeg:=15.0F, color:=RigColor.White, castsShadow:=False),
                    .FillRight = New PreviewLight(0.2F, azimuthDeg:=30.0F, elevationDeg:=0.0F, color:=RigColor.White, castsShadow:=False),
                    .BackLight = New PreviewLight(0.3F, azimuthDeg:=225.0F, elevationDeg:=30.0F, color:=RigColor.White, castsShadow:=False),
                    .AmbientIntensity = 0.92F,
                    .AmbientGroundLevel = 0.45F,
                    .AmbientSkyColor = RigColor.White,
                    .AmbientGroundColor = RigColor.White,
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False}),
            New LightRigPreset("Sunny day",
                "Hard high sun from the upper left, blue sky as fill and a warm bounce off the ground. The most directional set: sunlit side vs shaded side is a wide ratio.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.9F, azimuthDeg:=45.0F, elevationDeg:=60.0F, color:=New RigColor(1.0F, 0.96F, 0.9F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.4F, azimuthDeg:=300.0F, elevationDeg:=30.0F, color:=New RigColor(0.82F, 0.88F, 1.0F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.3F, azimuthDeg:=30.0F, elevationDeg:=-15.0F, color:=New RigColor(0.82F, 0.88F, 1.0F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.35F, azimuthDeg:=225.0F, elevationDeg:=45.0F, color:=New RigColor(1.0F, 0.97F, 0.92F), castsShadow:=False),
                    .AmbientIntensity = 0.62F,
                    .AmbientGroundLevel = 0.6F,
                    .AmbientSkyColor = New RigColor(0.78F, 0.86F, 1.0F),
                    .AmbientGroundColor = New RigColor(1.0F, 0.9F, 0.76F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False}),
            New LightRigPreset("Overcast",
                "Cloudy dome: a high, weak key with the ambient doing most of the work, so shadows stay faint. Volume comes from the sky-to-ground gradient, not from shape.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.45F, azimuthDeg:=45.0F, elevationDeg:=75.0F, color:=New RigColor(0.97F, 0.98F, 1.0F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.35F, azimuthDeg:=315.0F, elevationDeg:=15.0F, color:=New RigColor(0.95F, 0.97F, 1.0F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.35F, azimuthDeg:=45.0F, elevationDeg:=15.0F, color:=New RigColor(0.95F, 0.97F, 1.0F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.2F, azimuthDeg:=180.0F, elevationDeg:=30.0F, color:=New RigColor(0.95F, 0.97F, 1.0F), castsShadow:=False),
                    .AmbientIntensity = 1.05F,
                    .AmbientGroundLevel = 0.55F,
                    .AmbientSkyColor = New RigColor(0.9F, 0.94F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.72F, 0.71F, 0.68F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False}),
            New LightRigPreset("Portrait",
                "Studio portrait: 3/4 key from the RIGHT (opposite side to Sunny day), high enough to shape the cheekbone, 2:1 fill, off-axis hair kicker and a dark ground so the body falls into shadow.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.9F, azimuthDeg:=315.0F, elevationDeg:=45.0F, color:=New RigColor(1.0F, 0.97F, 0.93F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.5F, azimuthDeg:=45.0F, elevationDeg:=15.0F, color:=New RigColor(0.94F, 0.96F, 1.0F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.25F, azimuthDeg:=285.0F, elevationDeg:=0.0F, color:=New RigColor(1.0F, 0.98F, 0.95F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.45F, azimuthDeg:=135.0F, elevationDeg:=45.0F, color:=New RigColor(1.0F, 0.95F, 0.88F), castsShadow:=False),
                    .AmbientIntensity = 0.58F,
                    .AmbientGroundLevel = 0.35F,
                    .AmbientSkyColor = New RigColor(0.96F, 0.97F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.7F, 0.66F, 0.62F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False}),
            New LightRigPreset("Dungeon",
                "A NEAR wall torch low on the left plus a far weaker one behind on the right (the asymmetric distance), with cold moonlight through a grate. Only the near torch is strongly warm; everything else is cool, so the contrast is one of TEMPERATURE instead of dyeing the whole model orange.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.0F, azimuthDeg:=60.0F, elevationDeg:=15.0F, color:=New RigColor(1.0F, 0.84F, 0.66F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.45F, azimuthDeg:=240.0F, elevationDeg:=30.0F, color:=New RigColor(1.0F, 0.9F, 0.8F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.3F, azimuthDeg:=30.0F, elevationDeg:=-30.0F, color:=New RigColor(1.0F, 0.88F, 0.74F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.4F, azimuthDeg:=210.0F, elevationDeg:=45.0F, color:=New RigColor(0.72F, 0.8F, 1.0F), castsShadow:=False),
                    .AmbientIntensity = 0.56F,
                    .AmbientGroundLevel = 0.35F,
                    .AmbientSkyColor = New RigColor(0.68F, 0.76F, 0.95F),
                    .AmbientGroundColor = New RigColor(0.8F, 0.7F, 0.6F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False})}
    End Function

    ''' <summary>Color del hemisferio de arriba ya escalado por la intensidad (sin linearizar).</summary>
    Public Function AmbientSkyDiffuse() As OpenTK.Mathematics.Vector3
        Return AmbientSkyColor.ToVector3() * AmbientIntensity
    End Function

    ''' <summary>Color del hemisferio de abajo: tinte × intensidad (sin linearizar). SIMETRICA con
    ''' <see cref="AmbientSkyDiffuse"/> a proposito — las dos devuelven color×intensidad y nada mas.
    ''' <para>⛔ EL NIVEL DE SUELO NO ESTA ACA. Va aplicado despues del pow 2.2, en
    ''' <c>Render.ResolveFrameLights</c>; ver <see cref="AmbientGroundLevel"/> para el por que y para lo
    ''' que costo tenerlo del otro lado. Si algun dia esto vuelve a multiplicar por el nivel, la perilla
    ''' se vuelve a apagar sola y no hay gate que lo cace.</para></summary>
    Public Function AmbientGroundDiffuse() As OpenTK.Mathematics.Vector3
        Return AmbientGroundColor.ToVector3() * AmbientIntensity
    End Function
End Structure

''' <summary>Un set con nombre de <see cref="PreviewLightRig.Presets"/>. NO se persiste: el config
''' guarda el rig resuelto, así que editar un preset aplicado no lo "desaplica" ni lo rompe.</summary>
Public Structure LightRigPreset
    Public ReadOnly Property Name As String
    Public ReadOnly Property Description As String
    Public ReadOnly Property Rig As PreviewLightRig

    Public Sub New(name As String, description As String, rig As PreviewLightRig)
        _Name = name
        _Description = description
        _Rig = rig
    End Sub

    ''' <summary>Lo que muestra el combo del diálogo.</summary>
    Public Overrides Function ToString() As String
        Return Name
    End Function
End Structure
