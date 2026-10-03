' Rig de luces del previewer GL (4 direccionales + ambient hemisférico), compartido FO4/SSE.
'
' ⛔ TODO ES PROPIEDAD DE TIPO PRIMITIVO, y no es estilo: System.Text.Json va con IncludeFields = False,
' asi que un color guardado como `System.Numerics.Vector3` —cuyos X/Y/Z son CAMPOS— serializa como `{}` y
' al releer vuelve (0,0,0) = ambient NEGRO. El sintoma no apunta al JSON: preview oscuro que se "arregla"
' solo al abrir el diálogo, porque la UI le pone el blanco del swatch en memoria. Con propiedades el
' round-trip es exacto y no hacen falta converters ni fallbacks "(0,0,0) = blanco".
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
''' <para>⛔ LAS LUCES SON FIJAS AL MUNDO: azimut y elevación de MUNDO, NO multiplicadores relativos a la
''' base de la cámara. Con la dirección atada a la cámara el rig entero gira al orbitar, y con sombras
''' proyectadas eso es indefendible — la sombra de la nariz barre la cara mientras el usuario gira, y nadie
''' lo lee como "iluminación consistente". Un estudio real, y el motor, tienen las luces quietas y giran el
''' modelo.</para>
'''
''' <para>NO HAY CONVERSIÓN NI VERSIÓN DE ESQUEMA para un rig viejo: la invalidación se hace RENOMBRANDO
''' la propiedad que lo persiste, así que el dato viejo no se convierte ni se pisa — no se lee. Ver el
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
    ''' <para>ES PARTE DEL RIG, no de <c>PreviewShadowSettings</c>, y esa division es la que hace que
    ''' la cosa cierre: "que luces castean" es una propiedad de la LUZ y viaja con el preset; "como se
    ''' dibujan las sombras" (calidad, suavidad, oscuridad, bias, receptor de suelo) es global a todas y
    ''' vive del otro lado. Mezclarlas obligaria a versionar dos esquemas por el mismo cambio.</para>
    ''' <para>Cuantas castean lo decide CADA preset (ver <see cref="Presets"/>): la mayoria solo la key,
    ''' Studio las cuatro. Cada luz casteante extra cuesta un mapa de profundidad completo —+16 MB a 2048, y
    ''' +32 si ademas esta el receptor de suelo, que reserva su propio array del mismo tamano— mas un lookup
    ''' de PCF por fragmento.</para></summary>
    Public Property CastsShadow As Boolean

    ''' <summary>True si esta luz puede oscurecer algo: castea Y aporta luz.
    ''' <para>EL SEGUNDO TERMINO NO ES UNA OPTIMIZACION OPORTUNISTA, ES UNA IDENTIDAD: el shader
    ''' multiplica <c>diffuse * factor</c>, y con <c>diffuse = 0</c> el producto es 0 con cualquier factor.
    ''' O sea que una luz apagada con la casilla puesta produce EXACTAMENTE el mismo frame casteando que
    ''' no casteando, y castear le costaria un mapa entero. El epsilon esta en espacio perceptual (que es
    ''' donde vive <c>Diffuse()</c>); en lineal equivale a ~1e-7.</para></summary>
    Public Function CasteaDeVerdad() As Boolean
        If Not CastsShadow Then Return False
        Dim d = Diffuse()
        Return Math.Max(d.X, Math.Max(d.Y, d.Z)) > 0.0005F
    End Function

    ''' <summary>TODOS LOS CAMPOS, TODOS OBLIGATORIOS, EN UN SOLO PASO. Ninguno es opcional y ninguno
    ''' se completa despues con un <c>With { }</c>.
    '''
    ''' <para>⛔ ESTO NO ES ESTILO. Con un ctor parcial + <c>With { }</c>, olvidarse <c>CastsShadow</c> no
    ''' es un error: <c>PreviewLight</c> es una Structure y el campo queda en False, el default del TIPO. Asi
    ''' se escribio el rig que pinea <c>Tools/ShadowGate</c>, ninguna luz casteo, y el arnes entero paso a
    ''' medir CERO en todos sus A/B de sombra —verde, sin medir nada— ademas de reventar con
    ''' NullReferenceException porque el target ni se instanciaba. Con el ctor completo eso es un ERROR DE
    ''' COMPILACION.</para>
    '''
    ''' <para>Lo que NO se puede evitar en VB: <c>New PreviewLight()</c> sin argumentos existe siempre en
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
    ''' <para>SE APLICA EN LINEAL, DESPUES del pow 2.2, y por eso no vive dentro de
    ''' <see cref="AmbientGroundDiffuse"/> sino en <c>Render.ResolveFrameLights</c>. Un TINTE se autora en
    ''' espacio perceptual y se decodea al subir —es un color—; esto NO es un color, es un COCIENTE DE
    ''' RADIANCIAS entre los dos hemisferios, y el <c>mix()</c> del shader que lo consume opera en lineal.
    ''' <para>⛔ NO MOVERLO ADENTRO DEL POW (multiplicando al color): ahi la perilla entrega
    ''' <c>nivel^2.2</c> y un 0,45 llega al shader como 17,3 % del cielo, no 45 %. MEDIDO con
    ''' `ShadowGate --ambient` sobre la escena canonica: pintar el hemisferio de abajo de ROJO PURO movia
    ''' 0,48/255 de promedio contra los 3,00 del MISMO gesto sobre el cielo (6,25x menos), y la mitad
    ''' inferior del slider era indistinguible de cero (0,00 vs 0,20 = 0,14/255). La perilla existia,
    ''' llegaba al uniform, y no se sentia.</para>
    ''' <para>Los configs y los presets ya guardados NO se migraron, a proposito (decision del usuario): el
    ''' mismo numero vale hoy ~2,6x mas claro que cuando se calibro.</para></para></summary>
    Public Property AmbientGroundLevel As Single
    ''' <summary>TINTE del hemisferio de arriba (normal hacia world +Z). Blanco = neutro.</summary>
    Public Property AmbientSkyColor As RigColor
    ''' <summary>TINTE del hemisferio de abajo. El BRILLO lo da <see cref="AmbientGroundLevel"/>.</summary>
    Public Property AmbientGroundColor As RigColor

    ' ==========================================================================================
    ' COMO SE VEN LAS SOMBRAS — parte del RIG, no de PreviewShadowSettings
    ' ==========================================================================================
    ' EL CRITERIO ES LA NATURALEZA DE LA PERILLA, no dónde era cómodo ponerla. El rig ya decide QUE
    ' LUCES CASTEAN (PreviewLight.CastsShadow), asi que es incoherente que no pueda decidir COMO SE VEN
    ' esas sombras: la blandura, la oscuridad y si el modelo apoya en el piso son parte del LOOK de un set
    ' de luces, igual que la temperatura o el balance key/fill. Un "Dungeon" con sombra dura y negra y un
    ' "Overcast" con sombra apenas insinuada son dos escenarios distintos, y hasta ahora los cinco presets
    ' compartian una unica configuracion global.
    '
    ' Y POR ESO MISMO **NO** SE MUDARON LAS OTRAS CINCO. `MapSize`, `Depth16`, los dos bias y `Enabled`
    ' se quedan en PreviewShadowSettings porque son presupuesto de MAQUINA, no estetica. Si viajaran en el
    ' preset, aplicar "Portrait" le reescribiria la calidad y la VRAM a alguien que puso 1024 porque su
    ' placa no da — un preset de ILUMINACION no tiene por que tocar el presupuesto de video, y el sintoma
    ' (se traba el preview despues de aplicar un preset) no apunta al preset.
    '
    ' Consecuencia deliberada: tocar estas tres marca el combo de presets como "Custom", porque el rig
    ' efectivamente dejo de coincidir con el preset. Ver Coincide.

    ''' <summary>Radio del kernel de PCF en TEXELES del mapa. Fraccionario: la parte entera son los taps y
    ''' el sobrante viaja en el ESPACIADO, asi que el desenfoque es continuo. Acotado a
    ''' <c>PreviewShadowSettings.MaxPcfRadius</c> al subirlo.</summary>
    Public Property ShadowSoftnessTexels As Single

    ''' <summary>Cuanto oscurece la sombra: <c>factor = 1 - Darkness*(1-crudo)</c>, acotado a [0,1] al
    ''' subirlo. 1 = la luz ocluida se apaga del todo, que es lo que hace el motor. Menos de 1 NO es fiel;
    ''' existe porque en un previewer hace falta leer la textura del lado oscuro.</summary>
    Public Property ShadowDarkness As Single

    ''' <summary>Dibuja la silueta sobre el plano del piso (el "shadow catcher").
    ''' <para>NO es gratis: reserva un SEGUNDO array de shadow maps del mismo lado y con las mismas
    ''' capas que el del personaje, o sea que prenderlo DUPLICA la VRAM de la feature.</para></summary>
    Public Property ShadowOnGround As Boolean

    ' ⛔ ESTE RIG NO TIENE VERSION DE ESQUEMA, Y ES A PROPOSITO. La invalidacion se hace RENOMBRANDO la
    ' propiedad que persiste el rig, con lo cual el dato viejo no se detecta: no se lee. Ver el bloque de
    ' `Setting_LightRig_*` en Config_Class.vb, que tiene el mecanismo y su precio.
    ' El motivo: un `SchemaVersion` obliga a mantener una rama de migracion que PUEDE no dispararse, y es
    ' ciega justo al caso peor —"la clave existia y significaba otra cosa"—. Una clave que no existe no se
    ' puede leer mal.

    ''' <summary>El rig por default = preset <b>"Portrait"</b>, que es también al que vuelve el botón
    ''' Reset. Una sola fuente de verdad.
    ''' <para>ALCANCE: lo comparten las TRES apps. Cambiarlo mueve el aspecto por default de Wardrobe
    ''' Manager y NPC Manager para quien todavía no tenga un rig guardado en su <c>config.json</c>, y
    ''' para todo el que apriete "Reset Lighting to default". No toca a quien ya haya guardado el suyo:
    ''' la config persistida gana. Cambiarlo es decisión del usuario.</para>
    ''' <para>Se busca POR NOMBRE y no por índice: reordenar <see cref="Presets"/> —cosa que pasa cada vez
    ''' que se agrega uno— cambiaría el default en silencio. Si el nombre no estuviera, cae al primero.</para></summary>
    Public Shared Function Defaults() As PreviewLightRig
        Return DefaultPreset().Rig
    End Function

    ''' <summary>The preset Reset applies (Portrait).</summary>
    Public Shared Function DefaultPreset() As LightRigPreset
        Dim ps = Presets()
        For Each p In ps
            If String.Equals(p.Name, "Portrait", StringComparison.OrdinalIgnoreCase) Then Return p
        Next
        Return ps(0)
    End Function

    ' Comparación con tolerancia, NO igualdad exacta: el viaje por la UI cuantiza los colores a 8 bits
    ' (swatch = System.Drawing.Color), así que aplicar un preset y releerlo devuelve p.ej. 0.58 -> 148/255
    ' = 0.5803922. Con Equals el combo se deseleccionaría solo apenas se aplica el preset. Epsilon = un
    ' paso de 8 bits (1/255) con margen.
    Friend Const RigMatchEpsilon As Single = 0.005F

    Friend Shared Function CasiIgual(a As Single, b As Single) As Boolean
        Return Math.Abs(a - b) <= RigMatchEpsilon
    End Function

    Friend Shared Function ColorCoincide(a As RigColor, b As RigColor) As Boolean
        Return CasiIgual(a.R, b.R) AndAlso CasiIgual(a.G, b.G) AndAlso CasiIgual(a.B, b.B)
    End Function

    Friend Shared Function LuzCoincide(a As PreviewLight, b As PreviewLight) As Boolean
        ' El azimut se compara MODULO 360: 0 y 360 son la misma dirección y el NUD deja escribir los dos.
        ' Sin esto, aplicar un preset con azimut 0 y que el control redondee a 360 deseleccionaba el combo.
        ' `dAz` ES la diferencia angular en [0,180], y la resta cruda daria 360 para el mismo rayo.
        ' LA TOLERANCIA ES LA MISMA EN LOS DOS EJES. Aflojar sólo el azimut para absorber el redondeo del
        ' control no absorbe nada —la elevación sola ya manda el combo a "Custom"— y encima tapa el problema
        ' real, que es que el modelo se cuantice. De eso se ocupa AnguloDesdeNud; acá alcanza el epsilon.
        Dim dAz As Single = Math.Abs(((a.AzimuthDeg - b.AzimuthDeg) Mod 360.0F + 540.0F) Mod 360.0F - 180.0F)
        ' EL FLAG DE CASTEO ENTRA EN LA COMPARACION. Sin esto, prender la sombra de un fill dejaba el
        ' combo diciendo "Studio" cuando el rig ya NO es Studio — y Apply habilitado, o sea un click de
        ' distancia de perder el cambio sin aviso.
        Return CasiIgual(a.Strength, b.Strength) AndAlso ColorCoincide(a.Color, b.Color) AndAlso
               dAz <= RigMatchEpsilon AndAlso CasiIgual(a.ElevationDeg, b.ElevationDeg) AndAlso
               a.CastsShadow = b.CastsShadow
    End Function

    ' Compartida con el menu contextual del preview: los dos lugares deben decidir "preset activo"
    ' con exactamente la misma tolerancia (incluida la cuantizacion de colores de la UI).
    Public Shared Function Coincide(a As PreviewLightRig, b As PreviewLightRig) As Boolean
        Return LuzCoincide(a.KeyLight, b.KeyLight) AndAlso LuzCoincide(a.FillLeft, b.FillLeft) AndAlso
               LuzCoincide(a.FillRight, b.FillRight) AndAlso LuzCoincide(a.BackLight, b.BackLight) AndAlso
               CasiIgual(a.AmbientIntensity, b.AmbientIntensity) AndAlso
               CasiIgual(a.AmbientGroundLevel, b.AmbientGroundLevel) AndAlso
               ColorCoincide(a.AmbientSkyColor, b.AmbientSkyColor) AndAlso
               ColorCoincide(a.AmbientGroundColor, b.AmbientGroundColor) AndAlso
               CasiIgual(a.ShadowSoftnessTexels, b.ShadowSoftnessTexels) AndAlso
               CasiIgual(a.ShadowDarkness, b.ShadowDarkness) AndAlso
               a.ShadowOnGround = b.ShadowOnGround
    End Function

    ''' <summary>Sets de luces predefinidos. Cada uno es un ESCENARIO coherente (dirección + temperatura
    ''' + relación key/fill/rim + ambiente), no una variación de intensidad.
    '''
    ''' Convención de dirección (la del rig, ver PreviewLight.Direction): Forward = desde la cámara
    ''' hacia el sujeto, Back = contraluz, Up/Down = cenital/contrapicado. Right/Left del rig caen
    ''' del lado OPUESTO al de la pantalla (queda del rig anterior): por eso la luz "Left" usa Right y
    ''' viceversa — los presets respetan esa convención para que la etiqueta del grupo siga valiendo.
    '''
    ''' DOS TRAMPAS AL DISEÑAR UN SET. Render sube `Vector_to_Linear(color × strength)` = **pow 2.2 sobre
    ''' el producto**, así que:
    '''   1. LA SATURACIÓN SE AMPLIFICA. Un tinte "naranja antorcha" (1.00, 0.58, 0.22) llega al shader
    '''      como (1.00, 0.30, 0.035) = rojo casi puro, y si además lo llevan 3 de las 4 luces, TODO el
    '''      modelo se tiñe. Regla: ningún canal por debajo de ~0.62 salvo la key de un escenario
    '''      explícitamente coloreado, y que las otras luces lleven la temperatura CONTRARIA o neutra.
    '''   2. LA FUERZA NO ES LINEAL. strength 0.6 = 0.33 lineal, pero 1.35 = 1.94 = SEIS veces la key de
    '''      Studio -> quemado. Presupuesto de referencia (Studio, que es el calibrado): directas ≈ 0.62
    '''      lineal sumadas, ambient sky 0.83 / ground 0.37. Cada set se queda cerca de ese total y gasta la
    '''      diferencia en CONTRASTE (dónde está la luz y cuánto baja el ambiente), no en potencia.
    '''      ⚠️ El ground lineal de los cinco sets es ~2,6x mas claro que cuando se calibraron: el nivel
    '''      dejo de ir adentro del pow (ver AmbientGroundLevel) y los presets no se reexpresaron.</summary>
    Public Shared Function Presets() As LightRigPreset()
        ' ⛔ NINGUNA KEY A 0 GRADOS DE LA CAMARA. Una luz frontal pura NO PROYECTA SOMBRA VISIBLE por
        ' construccion: lo que ocluye tapa exactamente su propia sombra. MEDIDO con Tools/ShadowGate sobre
        ' cabeza+pelo+cuerpo vanilla: con una key asi, prender las sombras mueve 6797 px (1,05 % de la
        ' pantalla) con un delta maximo de canal de 23 sobre 255. Lo impide el gate `studio-rig`.
        ' ⛔ TODA KEY POR ENCIMA DE ShadowMapMath.ElevacionMinimaGrados (11,54). Bajo el horizonte
        ' ExpandForGroundShadow rechaza el encuadre, el receptor de suelo queda PERMANENTEMENTE
        ' deshabilitado en ese preset y la UI se lo tiene que decir al usuario en un cartel. Un preset que
        ' apaga una feature no es una eleccion de escenario. Ley 5 de `studio-rig`.
        ' LOS ANGULOS SON REDONDOS Y ESO ES DELIBERADO: multiplos de 15 grados y 0,05 de fuerza, autorados a
        ' mano. Angulos derivados de una conversion llevaban 5 decimales, y redondearlos a 2 volteaba pixeles
        ' sueltos en bordes con alpha-test (340 px de 648.000 medidos). El golden `rig-presets` los congela
        ' con tolerancia 0,00005 grados — no por precision, sino porque un golden flojo no congela nada.
        ' CUANTAS LUCES CASTEAN POR SET, y no es gratis: cada luz casteante extra es un shadow map completo
        ' (+16 MB a 2048) y un lookup de PCF por fragmento. Studio castea con las CUATRO (es un set de
        ' estudio y la simetria es el punto), Portrait con key + fill izquierdo, y Sunny day / Full moon /
        ' Sunset solo con la key. Agregar una casilla mas a un preset le sube la VRAM a todo el que lo aplique.
        Return New LightRigPreset() {
            New LightRigPreset("Studio", "Day",
                "Studio: four symmetric directional lights, all casting shadows.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.0F, azimuthDeg:=0.0F, elevationDeg:=45.0F, color:=RigColor.White, castsShadow:=True),
                    .FillLeft = New PreviewLight(0.25F, azimuthDeg:=270.0F, elevationDeg:=45.0F, color:=RigColor.White, castsShadow:=True),
                    .FillRight = New PreviewLight(0.25F, azimuthDeg:=90.0F, elevationDeg:=45.0F, color:=RigColor.White, castsShadow:=True),
                    .BackLight = New PreviewLight(0.25F, azimuthDeg:=180.0F, elevationDeg:=45.0F, color:=RigColor.White, castsShadow:=True),
                    .AmbientIntensity = 1.0F,
                    .AmbientGroundLevel = 0.5F,
                    .AmbientSkyColor = RigColor.White,
                    .AmbientGroundColor = RigColor.White,
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False}),
            New LightRigPreset("Sunny day", "Day",
                "Sunny day: Hard high sun from the upper right blue sky as fill and a warm bounce off the ground.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.25F, azimuthDeg:=315.0F, elevationDeg:=45.0F, color:=New RigColor(1.0F, 0.96F, 0.9F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.25F, azimuthDeg:=270.0F, elevationDeg:=0.0F, color:=New RigColor(0.82F, 0.88F, 1.0F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.25F, azimuthDeg:=90.0F, elevationDeg:=0.0F, color:=New RigColor(0.82F, 0.88F, 1.0F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.25F, azimuthDeg:=180.0F, elevationDeg:=45.0F, color:=New RigColor(1.0F, 0.97F, 0.92F), castsShadow:=False),
                    .AmbientIntensity = 1.0F,
                    .AmbientGroundLevel = 0.5F,
                    .AmbientSkyColor = New RigColor(0.78F, 0.86F, 1.0F),
                    .AmbientGroundColor = New RigColor(1.0F, 0.9F, 0.76F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = True}),
            New LightRigPreset("Full moon", "Night",
                "Full moon: Dark night with a full moon over the sky.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(2.0F, azimuthDeg:=75.0F, elevationDeg:=75.0F, color:=New RigColor(0.97F, 0.98F, 1.0F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.0F, azimuthDeg:=270.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.0F, azimuthDeg:=90.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.0F, azimuthDeg:=180.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .AmbientIntensity = 1.0F,
                    .AmbientGroundLevel = 0.5F,
                    .AmbientSkyColor = New RigColor(0.0F, 0.0F, 0.0F),
                    .AmbientGroundColor = New RigColor(0.0F, 0.0F, 0.0F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = True}),
            New LightRigPreset("Portrait", "Day",
                "Portrait: Key from the left, high enough to shape the cheekbone, fill opposite offset, off-axis hair kicker and a dark ground so the body falls into shadow.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.35F, azimuthDeg:=35.0F, elevationDeg:=35.0F, color:=New RigColor(1.0F, 0.97F, 0.93F), castsShadow:=True),
                    .FillLeft = New PreviewLight(0.45F, azimuthDeg:=325.0F, elevationDeg:=35.0F, color:=New RigColor(1.0F, 0.97F, 0.93F), castsShadow:=True),
                    .FillRight = New PreviewLight(0.0F, azimuthDeg:=90.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.0F, azimuthDeg:=180.0F, elevationDeg:=60.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .AmbientIntensity = 0.5F,
                    .AmbientGroundLevel = 0.5F,
                    .AmbientSkyColor = New RigColor(0.96F, 0.97F, 1.0F),
                    .AmbientGroundColor = New RigColor(0.7F, 0.66F, 0.62F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = False}),
            New LightRigPreset("Sunset", "Sunset",
                "Sunset: Almost dark with a warm glow from the setting sun.",
                New PreviewLightRig With {
                    .KeyLight = New PreviewLight(1.75F, azimuthDeg:=60.0F, elevationDeg:=15.0F, color:=New RigColor(1.0F, 0.8F, 0.8F), castsShadow:=True),
                      .FillLeft = New PreviewLight(0.0F, azimuthDeg:=270.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .FillRight = New PreviewLight(0.0F, azimuthDeg:=90.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .BackLight = New PreviewLight(0.0F, azimuthDeg:=180.0F, elevationDeg:=0.0F, color:=New RigColor(0.0F, 0.0F, 0.0F), castsShadow:=False),
                    .AmbientIntensity = 0.25F,
                    .AmbientGroundLevel = 0.5F,
                    .AmbientSkyColor = New RigColor(1.0F, 0.8F, 0.8F),
                    .AmbientGroundColor = New RigColor(0.0F, 0.0F, 0.0F),
                    .ShadowSoftnessTexels = 2.0F,
                    .ShadowDarkness = 1.0F,
                    .ShadowOnGround = True})}
    End Function

    ''' <summary>Color del hemisferio de arriba ya escalado por la intensidad (sin linearizar).</summary>
    Public Function AmbientSkyDiffuse() As OpenTK.Mathematics.Vector3
        Return AmbientSkyColor.ToVector3() * AmbientIntensity
    End Function

    ''' <summary>Color del hemisferio de abajo: tinte × intensidad (sin linearizar). SIMETRICA con
    ''' <see cref="AmbientSkyDiffuse"/> a proposito — las dos devuelven color×intensidad y nada mas.
    ''' <para>EL NIVEL DE SUELO NO ESTA ACA. Va aplicado despues del pow 2.2, en
    ''' <c>Render.ResolveFrameLights</c>; ver <see cref="AmbientGroundLevel"/> para el por que y la
    ''' medicion. ⛔ Si esto vuelve a multiplicar por el nivel, la perilla se apaga sola y no hay gate que
    ''' lo cace.</para></summary>
    Public Function AmbientGroundDiffuse() As OpenTK.Mathematics.Vector3
        Return AmbientGroundColor.ToVector3() * AmbientIntensity
    End Function
End Structure

''' <summary>Un set con nombre de <see cref="PreviewLightRig.Presets"/>. NO se persiste: el config
''' guarda el rig resuelto, así que editar un preset aplicado no lo "desaplica" ni lo rompe.</summary>
Public Structure LightRigPreset
    Public ReadOnly Property Name As String
    ''' <summary>The moment of the day the preset shows, on the game's default (clear) weather. App choice, not
    ''' the engine (user decision 2-oct-2026): Studio/Portrait/Sunny day = Day, Full moon = Night, Sunset = Sunset.</summary>
    Public ReadOnly Property Moment As String
    Public ReadOnly Property Description As String
    Public ReadOnly Property Rig As PreviewLightRig

    Public Sub New(name As String, moment As String, description As String, rig As PreviewLightRig)
        _Name = name
        _Moment = moment
        _Description = description
        _Rig = rig
    End Sub

    ''' <summary>THE ONE WAY TO APPLY A PRESET (dialog, preview context menu, Reset): the rig of the active game and
    ''' its weather+moment. The post and effect-light options are not the preset's (user decision).</summary>
    Public Sub ApplyTo(cfg As Config_App)
        cfg.SetActiveLights(Rig)
        Dim isSse = cfg.Game = Config_App.Game_Enum.Skyrim
        cfg.SetPreviewImaging(isSse, cfg.PreviewImaging(isSse).WithWeather(PreviewImagingSettings.DefaultKey(isSse).AtMoment(Moment)))
    End Sub

    ''' <summary>Whether the config shows THIS preset: its rig (PreviewLightRig.Coincide) and its weather+moment.</summary>
    Public Function MatchesConfig(cfg As Config_App) As Boolean
        Dim isSse = cfg.Game = Config_App.Game_Enum.Skyrim
        Dim s = cfg.PreviewImaging(isSse)
        Return PreviewLightRig.Coincide(Rig, cfg.ActiveLights()) AndAlso
               New PreviewImagingKey(s.WeatherPlugin, s.WeatherObjectId, s.Moment).Equals(PreviewImagingSettings.DefaultKey(isSse).AtMoment(Moment))
    End Function

    ''' <summary>Lo que muestra el combo del diálogo.</summary>
    Public Overrides Function ToString() As String
        Return Name
    End Function
End Structure
