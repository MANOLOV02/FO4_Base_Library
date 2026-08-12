' Rig de luces del previewer GL (4 direccionales + ambient hemisférico), compartido FO4/SSE.
'
' ⛔ SIN COMPATIBILIDAD con el `Setting_Lightrig` / `LightsRig_struct` anterior: aquel guardaba los
' colores como `System.Numerics.Vector3`, cuyos X/Y/Z son CAMPOS, y System.Text.Json (IncludeFields =
' False por default) los serializaba como `{}` -> al releer volvían (0,0,0) = ambient NEGRO, mientras
' que la UI los mostraba blancos (fallback del swatch) y los "arreglaba" en memoria al abrir el diálogo.
' De ahí el síntoma: preview oscuro que cambiaba solo con abrir el rig. Acá TODO son PROPIEDADES de
' tipos primitivos => round-trip exacto por JSON, sin converters ni fallbacks "(0,0,0) = blanco".
'
' El rig es POR JUEGO (Config_App.Setting_PreviewLights_FO4 / _SSE, igual que las opciones de CharGen).
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
''' <para>⭐ LA MIGRACIÓN ES EXACTA EN LA VISTA POR DEFECTO. Con la cámara en (angleX=0, angleY=0),
''' <c>OrbitCamera.UpdateDirectionFromAngles</c> da <c>Forward=(0,1,0)</c> y
''' <c>right=cross(Forward,UnitZ)=(1,0,0)</c>: la base de cámara ES la del mundo. Por eso el esquema viejo
''' se convierte sin pérdida con <see cref="FromCameraRelative"/> y los presets conservan su aspecto en
''' esa vista — sólo dejan de seguir al usuario.</para>
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

    Public Sub New(strength As Single, azimuthDeg As Single, elevationDeg As Single)
        Me.Strength = strength
        Me.Color = RigColor.White
        Me.AzimuthDeg = azimuthDeg
        Me.ElevationDeg = elevationDeg
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

    ''' <summary>Convierte los 6 multiplicadores del esquema VIEJO (relativos a la cámara) al par
    ''' azimut/elevación de mundo, evaluándolos en la VISTA POR DEFECTO — donde la base de cámara es la
    ''' del mundo, así que la conversión no pierde nada.
    ''' <para>Vive acá y no en el cargador del config porque además es lo que usa el gate
    ''' <c>rig-migration</c> de Tools/ParityGate para verificar que los presets nuevos son exactamente la
    ''' conversión de los viejos.</para></summary>
    Public Shared Function FromCameraRelative(strength As Single, up As Single, down As Single,
                                              left As Single, right As Single,
                                              forward As Single, back As Single) As PreviewLight
        ' Base de la cámara por defecto: right=(1,0,0), forward=(0,1,0), up=(0,0,1).
        Dim x As Double = right - left
        Dim y As Double = forward - back
        Dim z As Double = up - down
        Dim n As Double = Math.Sqrt(x * x + y * y + z * z)
        If n < 0.0001 Then
            ' Mismo fallback que tenía el código viejo ante un vector degenerado: el forward de la cámara.
            x = 0.0 : y = 1.0 : z = 0.0 : n = 1.0
        End If
        x /= n : y /= n : z /= n
        Dim az As Double = Math.Atan2(x, y) * 180.0 / Math.PI
        If az < 0.0 Then az += 360.0
        Dim el As Double = Math.Asin(Math.Clamp(z, -1.0, 1.0)) * 180.0 / Math.PI
        Return New PreviewLight(strength, CSng(az), CSng(el))
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

    ''' <summary>Versión del ESQUEMA de este rig. **CENTINELA de migración**: un config.json escrito antes
    ''' de las luces fijas al mundo no trae la clave, el deserializador la deja en 0, y ahí
    ''' <c>Config_App.LoadConfig</c> sabe que tiene que convertir los 6 multiplicadores viejos leyéndolos
    ''' del JSON crudo. Un Boolean no serviría de centinela: ausente y False son indistinguibles.
    ''' <para>1 = azimut/elevación de mundo. Al cambiar el esquema otra vez: subir la constante y agregar
    ''' la rama de migración, nunca reinterpretar los valores en silencio.</para></summary>
    Public Property SchemaVersion As Integer

    ''' <summary>El esquema que escribe esta versión del código.</summary>
    Public Const CurrentSchemaVersion As Integer = 1

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
    ' sombra. MEDIDO con Tools/ShadowGate sobre cabeza+pelo+cuerpo vanilla: con el Studio viejo,
    ' prender las sombras movia 6797 px (1,05 % de la pantalla) con un delta maximo de canal de 23
    ' sobre 255 — o sea la feature estaba practicamente invisible en el preset por default.
    ' ⛔ LOS ANGULOS VAN CON 5 DECIMALES, no con 2. Con 2 el error de direccion es 8,4e-5 rad — mil veces
    ' el ruido del float— y eso alcanza para VOLTEAR pixeles sueltos en un borde con alpha-test (medido:
    ' 340 px de 648.000 contra el rig viejo, 11 de ellos con delta alto en la silueta del pelo). Con 5
    ' decimales el error baja a 7,5e-8 = el piso del Single. Al retocar un preset a mano, conservar la
    ' precision o el A/B contra el commit anterior deja de dar cero.
    ' El nuevo Studio pone la key a 3/4 (arriba y a un lado), que es donde la pone un estudio real,
    ' y mantiene el PRESUPUESTO DE LUZ del set calibrado: la suma de las cuatro directas en
    ' LINEAL sigue en ~0,63 (el viejo daba 0,62). El gate `studio-rig` de Tools/ParityGate verifica
    ' las dos cosas —presupuesto y angulo— para que nadie devuelva la key al frente sin darse cuenta.
    ' El ambiente baja de 1,00/0,50 a 0,92/0,45: apenas lo justo para que la sombra se lea sin
    ' perder la lectura de textura, que es el proposito declarado de este preset.
    ' ⚠️ Un usuario EXISTENTE no ve ningun cambio: su rig esta persistido en config.json y esto solo
    ' cambia el preset (o sea lo que aplica Reset / lo que estrena un config nuevo). Efecto lateral
    ' conocido: si su rig coincidia con el Studio viejo, el combo del dialogo ahora dice "Custom".
        Return New LightRigPreset() {
            New LightRigPreset("Studio",
                "Neutral 3-point + rim. Colourless light for judging textures and materials, with the key off-axis so shaped shadows read.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.7F, azimuthDeg:=41.42367F, elevationDeg:=25.88216F),
                    .FillLeft = New PreviewLight(0.34F, azimuthDeg:=305.21759F, elevationDeg:=8.20385F),
                    .FillRight = New PreviewLight(0.18F, azimuthDeg:=38.65981F, elevationDeg:=0.00000F),
                    .BackLight = New PreviewLight(0.28F, azimuthDeg:=199.29005F, elevationDeg:=25.26399F),
                    .AmbientIntensity = 0.92F,
                    .AmbientGroundLevel = 0.45F,
                    .AmbientSkyColor = RigColor.White,
                    .AmbientGroundColor = RigColor.White,
                    .SchemaVersion = CurrentSchemaVersion}),
            New LightRigPreset("Sunny day",
                "Hard high sun from the upper left, blue sky as fill and a warm bounce off the ground. The most directional set: sunlit side vs shaded side is a wide ratio.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1F, azimuthDeg:=47.72631F, elevationDeg:=53.37645F) With {.Color = New RigColor(1.0F, 0.96F, 0.9F)},
                    .FillLeft = New PreviewLight(0.35F, azimuthDeg:=60.94540F, elevationDeg:=23.60911F) With {.Color = New RigColor(0.82F, 0.88F, 1.0F)},
                    .FillRight = New PreviewLight(0.48F, azimuthDeg:=296.56505F, elevationDeg:=19.17935F) With {.Color = New RigColor(0.82F, 0.88F, 1.0F)},
                    .BackLight = New PreviewLight(0.35F, azimuthDeg:=180.00000F, elevationDeg:=30.96376F) With {.Color = New RigColor(1.0F, 0.97F, 0.92F)},
                    .AmbientIntensity = 0.62F,
                    .AmbientGroundLevel = 0.6F,
                    .AmbientSkyColor = New RigColor(0.78F, 0.86F, 1.0F),
                    .AmbientGroundColor = New RigColor(1.0F, 0.9F, 0.76F),
                    .SchemaVersion = CurrentSchemaVersion}),
            New LightRigPreset("Overcast",
                "Cloudy dome: the key carries only a quarter of the light and the ambient does the rest, so shadows stay faint. Volume comes from the sky-to-ground gradient, not from shape.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.34F, azimuthDeg:=0.00000F, elevationDeg:=65.77225F) With {.Color = New RigColor(0.97F, 0.98F, 1.0F)},
                    .FillLeft = New PreviewLight(0.4F, azimuthDeg:=59.53446F, elevationDeg:=19.54049F) With {.Color = New RigColor(0.95F, 0.97F, 1.0F)},
                    .FillRight = New PreviewLight(0.4F, azimuthDeg:=300.46554F, elevationDeg:=19.54049F) With {.Color = New RigColor(0.95F, 0.97F, 1.0F)},
                    .BackLight = New PreviewLight(0.22F, azimuthDeg:=180.00000F, elevationDeg:=16.69924F) With {.Color = New RigColor(0.95F, 0.97F, 1.0F)},
                    .AmbientIntensity = 1.05F,
                    .AmbientGroundLevel = 0.55F,
                    .AmbientSkyColor = New RigColor(0.9F, 0.94F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.72F, 0.71F, 0.68F),
                    .SchemaVersion = CurrentSchemaVersion}),
            New LightRigPreset("Portrait",
                "Studio portrait: 3/4 key from the RIGHT (opposite side to Sunny day) with almost no overhead component, 4:1 fill, off-axis hair kicker and a dark ground so the body falls into shadow.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1F, azimuthDeg:=318.17983F, elevationDeg:=15.35295F) With {.Color = New RigColor(1.0F, 0.97F, 0.93F)},
                    .FillLeft = New PreviewLight(0.36F, azimuthDeg:=56.30993F, elevationDeg:=-7.89514F) With {.Color = New RigColor(0.94F, 0.96F, 1.0F)},
                    .FillRight = New PreviewLight(0.17F, azimuthDeg:=308.65981F, elevationDeg:=0.00000F) With {.Color = New RigColor(1.0F, 0.98F, 0.95F)},
                    .BackLight = New PreviewLight(0.39F, azimuthDeg:=153.43495F, elevationDeg:=28.22051F) With {.Color = New RigColor(1.0F, 0.95F, 0.88F)},
                    .AmbientIntensity = 0.58F,
                    .AmbientGroundLevel = 0.35F,
                    .AmbientSkyColor = New RigColor(0.96F, 0.97F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.7F, 0.66F, 0.62F),
                    .SchemaVersion = CurrentSchemaVersion}),
            New LightRigPreset("Dungeon",
                "A NEAR wall torch low on the left plus a far weaker one behind on the right (the asymmetric distance), with cold moonlight through a grate. Only the near torch is strongly warm; everything else is cool, so the contrast is one of TEMPERATURE instead of dyeing the whole model orange.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.74F, azimuthDeg:=59.93142F, elevationDeg:=-22.29063F) With {.Color = New RigColor(1.0F, 0.84F, 0.66F)},
                    .FillLeft = New PreviewLight(0.3F, azimuthDeg:=50.19443F, elevationDeg:=-21.01231F) With {.Color = New RigColor(1.0F, 0.9F, 0.8F)},
                    .FillRight = New PreviewLight(0.28F, azimuthDeg:=232.12502F, elevationDeg:=7.49472F) With {.Color = New RigColor(1.0F, 0.88F, 0.74F)},
                    .BackLight = New PreviewLight(0.38F, azimuthDeg:=180.00000F, elevationDeg:=24.22775F) With {.Color = New RigColor(0.72F, 0.8F, 1.0F)},
                    .AmbientIntensity = 0.56F,
                    .AmbientGroundLevel = 0.35F,
                    .AmbientSkyColor = New RigColor(0.68F, 0.76F, 0.95F),
                    .AmbientGroundColor = New RigColor(0.8F, 0.7F, 0.6F),
                    .SchemaVersion = CurrentSchemaVersion})}
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
