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

''' <summary>Una direccional del rig: fuerza + color + los 6 multiplicadores de dirección relativos
''' a la cámara (Up/Down/Left/Right/Forward/Back, −2..2).</summary>
Public Structure PreviewLight
    Public Property Strength As Single
    Public Property Color As RigColor

    ' Multiplicadores relativos a la base de cámara
    Public Property Up As Single
    Public Property Down As Single
    Public Property Left As Single
    Public Property Right As Single
    Public Property Forward As Single   ' hacia cámara
    Public Property Back As Single      ' opuesto a cámara

    Public Sub New(strength As Single, up As Single, down As Single, left As Single, right As Single,
                   forward As Single, back As Single)
        Me.Strength = strength
        Me.Color = RigColor.White
        Me.Up = up : Me.Down = down : Me.Left = left : Me.Right = right
        Me.Forward = forward : Me.Back = back
    End Sub

    ''' <summary>Color × fuerza, en espacio perceptual (el render lineariza).</summary>
    Public Function Diffuse() As OpenTK.Mathematics.Vector3
        Return Color.ToVector3() * Strength
    End Function

    ''' <summary>Dirección superficie→luz en WORLD (Z-up), ponderando los ejes de la cámara orbital:
    ''' (Up−Down)·worldUp + (Right−Left)·cross(cam.Forward, worldUp) + (Forward−Back)·cam.Forward.</summary>
    Public Function Direction(cam As OrbitCamera) As OpenTK.Mathematics.Vector3
        Dim upVec As New OpenTK.Mathematics.Vector3(0, 0, 1)
        Dim rightVec As OpenTK.Mathematics.Vector3 =
            OpenTK.Mathematics.Vector3.Normalize(OpenTK.Mathematics.Vector3.Cross(cam.Forward, upVec))
        Dim forwardVec As OpenTK.Mathematics.Vector3 = cam.Forward

        Dim dir As OpenTK.Mathematics.Vector3 =
            (Up - Down) * upVec + (Right - Left) * rightVec + (Forward - Back) * forwardVec

        ' Vector degenerado -> forward de cámara (evita NaN al normalizar)
        If dir.LengthSquared() < 0.00000001F Then dir = forwardVec

        Return OpenTK.Mathematics.Vector3.Normalize(dir)
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
    ''' 0 = suelo negro. Independiente de la intensidad y de los tintes.</summary>
    Public Property AmbientGroundLevel As Single
    ''' <summary>TINTE del hemisferio de arriba (normal hacia world +Z). Blanco = neutro.</summary>
    Public Property AmbientSkyColor As RigColor
    ''' <summary>TINTE del hemisferio de abajo. El BRILLO lo da <see cref="AmbientGroundLevel"/>.</summary>
    Public Property AmbientGroundColor As RigColor

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
    '''      lineal sumadas, ambient sky 1.0 / ground 0.22. Cada set se queda cerca de ese total y gasta
    '''      la diferencia en CONTRASTE (dónde está la luz y cuánto baja el ambiente), no en potencia.</summary>
    Public Shared Function Presets() As LightRigPreset()
    ' ⛔ LA KEY DE STUDIO ESTABA A 0 GRADOS DE LA CAMARA (forward:=1 y nada mas), y una luz frontal
    ' pura NO PROYECTA SOMBRA VISIBLE por construccion: lo que ocluye tapa exactamente su propia
    ' sombra. MEDIDO con Tools/ShadowGate sobre cabeza+pelo+cuerpo vanilla: con el Studio viejo,
    ' prender las sombras movia 6797 px (1,05 % de la pantalla) con un delta maximo de canal de 23
    ' sobre 255 — o sea la feature estaba practicamente invisible en el preset por default.
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
                    .KeyLight = New PreviewLight(0.7F, up:=0.55F, down:=0, left:=0, right:=0.75F, forward:=0.85F, back:=0),
                    .FillLeft = New PreviewLight(0.34F, up:=0.15F, down:=0, left:=0.85F, right:=0, forward:=0.6F, back:=0),
                    .FillRight = New PreviewLight(0.18F, up:=0, down:=0, left:=0, right:=0.4F, forward:=0.5F, back:=0),
                    .BackLight = New PreviewLight(0.28F, up:=0.5F, down:=0, left:=0.35F, right:=0, forward:=0, back:=1),
                    .AmbientIntensity = 0.92F,
                    .AmbientGroundLevel = 0.45F,
                    .AmbientSkyColor = RigColor.White,
                    .AmbientGroundColor = RigColor.White}),
            New LightRigPreset("Sunny day",
                "Hard high sun from the upper left, blue sky as fill and a warm bounce off the ground. The most directional set: sunlit side vs shaded side is a wide ratio.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.0F, up:=1, down:=0, left:=0, right:=0.55F, forward:=0.5F, back:=0) With {.Color = New RigColor(1.0F, 0.96F, 0.9F)},
                    .FillLeft = New PreviewLight(0.35F, up:=0.45F, down:=0, left:=0, right:=0.9F, forward:=0.5F, back:=0) With {.Color = New RigColor(0.82F, 0.88F, 1.0F)},
                    .FillRight = New PreviewLight(0.48F, up:=0.35F, down:=0, left:=0.9F, right:=0, forward:=0.45F, back:=0) With {.Color = New RigColor(0.82F, 0.88F, 1.0F)},
                    .BackLight = New PreviewLight(0.35F, up:=0.6F, down:=0, left:=0, right:=0, forward:=0, back:=1) With {.Color = New RigColor(1.0F, 0.97F, 0.92F)},
                    .AmbientIntensity = 0.62F,
                    .AmbientGroundLevel = 0.6F,
                    .AmbientSkyColor = New RigColor(0.78F, 0.86F, 1.0F),
                    .AmbientGroundColor = New RigColor(1.0F, 0.9F, 0.76F)}),
            New LightRigPreset("Overcast",
                "Cloudy dome: soft OVERHEAD key, even sides and no rim. Volume comes from the ambient sky-to-ground gradient, not from shadows.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.5F, up:=1, down:=0, left:=0, right:=0, forward:=0.45F, back:=0) With {.Color = New RigColor(0.97F, 0.98F, 1.0F)},
                    .FillLeft = New PreviewLight(0.33F, up:=0.35F, down:=0, left:=0, right:=0.85F, forward:=0.5F, back:=0) With {.Color = New RigColor(0.95F, 0.97F, 1.0F)},
                    .FillRight = New PreviewLight(0.33F, up:=0.35F, down:=0, left:=0.85F, right:=0, forward:=0.5F, back:=0) With {.Color = New RigColor(0.95F, 0.97F, 1.0F)},
                    .BackLight = New PreviewLight(0.2F, up:=0.3F, down:=0, left:=0, right:=0, forward:=0, back:=1) With {.Color = New RigColor(0.95F, 0.97F, 1.0F)},
                    .AmbientIntensity = 0.8F,
                    .AmbientGroundLevel = 0.4F,
                    .AmbientSkyColor = New RigColor(0.9F, 0.94F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.72F, 0.71F, 0.68F)}),
            New LightRigPreset("Portrait",
                "Studio portrait: 3/4 key from the RIGHT (opposite side to Sunny day) with almost no overhead component, 4:1 fill, off-axis hair kicker and a dark ground so the body falls into shadow.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.0F, up:=0.35F, down:=0, left:=0.85F, right:=0, forward:=0.95F, back:=0) With {.Color = New RigColor(1.0F, 0.97F, 0.93F)},
                    .FillLeft = New PreviewLight(0.36F, up:=0, down:=0.15F, left:=0, right:=0.9F, forward:=0.6F, back:=0) With {.Color = New RigColor(0.94F, 0.96F, 1.0F)},
                    .FillRight = New PreviewLight(0.17F, up:=0, down:=0, left:=0.5F, right:=0, forward:=0.4F, back:=0) With {.Color = New RigColor(1.0F, 0.98F, 0.95F)},
                    .BackLight = New PreviewLight(0.39F, up:=0.6F, down:=0, left:=0, right:=0.5F, forward:=0, back:=1) With {.Color = New RigColor(1.0F, 0.95F, 0.88F)},
                    .AmbientIntensity = 0.58F,
                    .AmbientGroundLevel = 0.35F,
                    .AmbientSkyColor = New RigColor(0.96F, 0.97F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.7F, 0.66F, 0.62F)}),
            New LightRigPreset("Dungeon",
                "A NEAR wall torch low on the left plus a far weaker one behind on the right (the asymmetric distance), with cold moonlight through a grate. Only the near torch is strongly warm; everything else is cool, so the contrast is one of TEMPERATURE instead of dyeing the whole model orange.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(0.74F, up:=0, down:=0.45F, left:=0, right:=0.95F, forward:=0.55F, back:=0) With {.Color = New RigColor(1.0F, 0.84F, 0.66F)},
                    .FillLeft = New PreviewLight(0.3F, up:=0, down:=0.3F, left:=0, right:=0.6F, forward:=0.5F, back:=0) With {.Color = New RigColor(1.0F, 0.9F, 0.8F)},
                    .FillRight = New PreviewLight(0.28F, up:=0.15F, down:=0, left:=0.9F, right:=0, forward:=0, back:=0.7F) With {.Color = New RigColor(1.0F, 0.88F, 0.74F)},
                    .BackLight = New PreviewLight(0.38F, up:=0.45F, down:=0, left:=0, right:=0, forward:=0, back:=1) With {.Color = New RigColor(0.72F, 0.8F, 1.0F)},
                    .AmbientIntensity = 0.56F,
                    .AmbientGroundLevel = 0.35F,
                    .AmbientSkyColor = New RigColor(0.68F, 0.76F, 0.95F),
                    .AmbientGroundColor = New RigColor(0.8F, 0.7F, 0.6F)})}
    End Function

    ''' <summary>Color del hemisferio de arriba ya escalado por la intensidad (sin linearizar).</summary>
    Public Function AmbientSkyDiffuse() As OpenTK.Mathematics.Vector3
        Return AmbientSkyColor.ToVector3() * AmbientIntensity
    End Function

    ''' <summary>Color del hemisferio de abajo: tinte × intensidad × nivel de suelo (sin linearizar).</summary>
    Public Function AmbientGroundDiffuse() As OpenTK.Mathematics.Vector3
        Return AmbientGroundColor.ToVector3() * (AmbientIntensity * AmbientGroundLevel)
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
