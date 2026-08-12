Imports System.Runtime.CompilerServices
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

' Sombras proyectadas del previewer (FO4 + SSE). Un solo shadow map ortografico para la KEY del rig.
'
' ⛔ LA LEY QUE SE REPLICA, y es la unica. Medida en los dos motores (ver memoria
' 21-render-sombras-re-y-corpus):
'   · FO4 forward b06_BSLighting_PS_rec1498: L142/L154 multiplican TODO el acumulador de la
'     direccional por el lookup, L193 multiplica el ESPECULAR, y el ambiente se suma DESPUES
'     (L281 `add r0.xyz, r6.xzwx, cb2[3].yzwy`) => el ambiente NO se sombrea. El loop de luces
'     puntuales (L199-280) tampoco.
'   · SSE (defines SHADOW_DIR/DEFSHADOW): `mul r2.yzw, r4.xxxx, cb2[1].xxyz` = mascara x color de la
'     direccional, y el ambiente (`dp4 cb2[11..13].vec4(N,1)` + `cb2[4].yzw`) se suma despues. Igual.
' En el shader eso se implementa escalando `light.diffuse` de la key ANTES de entrar a
' directionalLight(): asi quedan multiplicados sus cuatro terminos (Oren-Nayar, rim, transmision,
' subsurface) y el especular, y hemiAmbient() queda intacto. Con sombra binaria es identico al motor;
' con PCF es la generalizacion suave (el doble multiply del motor es artefacto de un termino 0/1, no ley).
'
' ⛔ EL MECANISMO NO se replica, a proposito. FO4 usa 4 CASCADAS con un tap duro y SSE una MASCARA
' SCREEN-SPACE de 4 canales; las dos existen porque el motor cubre una celda entera con varias luces.
' Aca hay UN personaje y UNA direccional => un solo mapa ajustado al AABB de la escena da texeles
' sub-milimetricos. Meter cascadas seria complejidad sin nada que la compre.
Public Structure PreviewShadowSettings

    ''' <summary>Dibuja el shadow map y aplica la oclusion. False = la feature entera queda inerte
    ''' (ni FBO, ni pase, ni uniform distinto de "todo iluminado").</summary>
    Public Property Enabled As Boolean

    ''' <summary>Lado del mapa en texeles. **CENTINELA**: 0 = la clave no estaba en el config.json, y
    ''' Config_App.LoadConfig lo repara con Defaults(). Un mapa de 0 texeles no es un valor legitimo,
    ''' que es justo lo que se le pide a un centinela (ver memoria 10-stack-json-structure-defaults).</summary>
    Public Property MapSize As Integer

    ''' <summary>Radio del kernel de PCF, en texeles del mapa. Fraccionario: la parte entera es la
    ''' cantidad de taps y el sobrante viaja en el ESPACIADO, asi que el desenfoque es continuo.
    ''' <para>⛔ El default es 2,0 y no 1,5, y el cambio no es de gusto. Decia 1,5 pero CORRIA como 2,0: el
    ''' radio salia de <c>CInt(Math.Round(SoftnessTexels))</c> y el redondeo BANCARIO de .NET manda 1,5 al
    ''' 2 (par) — que ademas hacia que 1,5 y 2,5 fueran el mismo valor y la perilla tuviera un tramo
    ''' muerto. Toda la feature, incluido el A/B que se aprobo mirando, se vio siempre con 2,0. Ahora que
    ''' 1,5 significa 1,5 de verdad, dejarlo daria una sombra mas dura que la aprobada: medido, 10.974 px
    ''' contra 21.615 en la escena del arnes. Se deja el numero que describe lo que se vio.</para>
    ''' <para>⛔ El comentario vive ACA y no adentro del <c>{ }</c> de Defaults(): VB no acepta una linea de
    ''' comentario entre los elementos de un inicializador de objeto (BC30201).</para></summary>
    Public Property SoftnessTexels As Single

    ''' <summary>Cuanto oscurece la sombra: `factor = 1 - Intensity*(1-crudo)`. 1 = la key se apaga del
    ''' todo en sombra (lo que hace el motor). Menos de 1 NO es fiel; existe porque en un previewer se
    ''' necesita ver la textura del lado oscuro.</summary>
    Public Property Intensity As Single

    ''' <summary>Desplazamiento del punto de muestreo a lo largo de la normal, en TEXELES del mapa.
    ''' Es el anti-acne principal: escala con el tamano real del texel, asi que no hay que re-tunearlo
    ''' al cambiar MapSize.</summary>
    Public Property NormalBiasTexels As Single

    ''' <summary>Bias constante restado a la profundidad de referencia, en TEXELES (se convierte a
    ''' unidades de profundidad con el rango del mapa). Tapa el residuo de cuantizacion del depth.</summary>
    Public Property DepthBiasTexels As Single

    ''' <summary>Dibuja la silueta del personaje sobre el plano del piso (el "shadow catcher"). DEFAULT OFF. Es el
    ''' indicio mas legible de todos —sin el, el modelo flota—, pero NO es gratis: obliga a agrandar el
    ''' encuadre del mapa para que la sombra proyectada quepa (ver ShadowMapMath.ExpandForGroundShadow),
    ''' o sea texeles mas grandes en el personaje. Por eso es una opcion aparte y no parte de Enabled.</summary>
    Public Property GroundShadow As Boolean

    ''' <summary>Tope del radio de PCF. No es configurable: acota el costo del kernel en el fragment.</summary>
    Public Const MaxPcfRadius As Integer = 4

    Public Shared Function Defaults() As PreviewShadowSettings
        Return New PreviewShadowSettings With {
            .Enabled = True,
            .MapSize = 2048,
            .SoftnessTexels = 2.0F,
            .Intensity = 1.0F,
            .NormalBiasTexels = 2.0F,
            .DepthBiasTexels = 1.5F,
            .GroundShadow = False}
    End Function

    ''' <summary>Copia con los valores acotados al rango que el render sabe ejecutar. Lo llama el render
    ''' en vez de confiar en el config: un MapSize absurdo cargado a mano no puede tirar la app.
    ''' <para>⛔⛔ ACOTA CAMPO POR CAMPO Y NO REEMPLAZA LA ESTRUCTURA. Antes, con <c>MapSize &lt;= 0</c>
    ''' devolvia <c>Defaults()</c> ENTERO, y eso es otra cosa: <c>Defaults()</c> trae
    ''' <c>Enabled = True</c>, asi que una estructura armada como
    ''' <c>New PreviewShadowSettings With {.Enabled = False}</c> —que deja MapSize en 0 por ser el default
    ''' del tipo— salia del sanitizado CON LAS SOMBRAS PRENDIDAS. Un brazo de A/B construido asi se dibuja
    ''' con la feature que dice apagar, y el veredicto sale al reves sin que nada avise. Hoy ningun caller
    ''' lo hace, pero la trampa estaba armada y el doc decia "acotados", que no era lo que pasaba.</para>
    ''' <para>⚠️ EL CAMBIO AFECTA A LOS CINCO CAMPOS, no solo a Enabled y MapSize. Una estructura armada a
    ''' mano como <c>New PreviewShadowSettings With {.Enabled = True}</c> antes salia con los defaults de
    ''' TODO; ahora sale con <c>Intensity = 0</c> (o sea sombra invisible, porque Clamp(0,0,1) = 0) y con
    ''' los dos bias en 0. Hoy no hay caller que construya asi —todos parten de Defaults()— pero un brazo
    ''' de A/B futuro hecho de esa forma se dibujaria SIN sombra creyendo tenerla, que es el gemelo exacto
    ''' de la trampa que este mismo doc dice haber cerrado.</para>
    ''' <para>⚠️ Y hay un consumidor que NO sanitiza: <c>Render.vb</c> lee <c>ActiveShadows().Enabled</c>
    ''' crudo para decidir si recalcula bounds en Play, mientras el pase de sombra lee
    ''' <c>.Sanitized().Enabled</c>. Con la version vieja los dos discrepaban justo en ese caso y el pase
    ''' corria con bounds congelados. Acotando en vez de reemplazar, <c>Enabled</c> ya no puede cambiar de
    ''' valor al sanitizar y los dos coinciden por construccion.</para></summary>
    Public Function Sanitized() As PreviewShadowSettings
        Dim s = Me
        ' MapSize 0 es el default del TIPO (una estructura recien construida), no una eleccion del usuario:
        ' se lo lleva al default de la feature. `Enabled` y el resto se respetan tal como vinieron.
        If s.MapSize <= 0 Then s.MapSize = Defaults().MapSize
        s.MapSize = Math.Clamp(RoundToPowerOfTwo(s.MapSize), 256, 8192)
        s.SoftnessTexels = Math.Clamp(s.SoftnessTexels, 0.0F, CSng(MaxPcfRadius))
        s.Intensity = Math.Clamp(s.Intensity, 0.0F, 1.0F)
        s.NormalBiasTexels = Math.Clamp(s.NormalBiasTexels, 0.0F, 16.0F)
        s.DepthBiasTexels = Math.Clamp(s.DepthBiasTexels, 0.0F, 16.0F)
        Return s
    End Function

    ''' <summary>Potencia de 2 mas cercana (hacia abajo en el punto medio geometrico). El FBO no lo
    ''' exige, pero mantiene el paso de texel exacto en binario y hace el snap reproducible.</summary>
    Friend Shared Function RoundToPowerOfTwo(v As Integer) As Integer
        If v <= 1 Then Return 1
        ' Tope en 2^30: mas arriba `hi` no entra en un Integer, y ningun shadow map real se acerca.
        Const MaxPow As Integer = 1 << 30
        If v >= MaxPow Then Return MaxPow
        Dim lo As Integer = 1
        While lo * 2 <= v
            lo *= 2
        End While
        Dim hi As Integer = lo * 2
        ' ⛔ CLng OBLIGATORIO: `v * v` desborda Integer a partir de v = 46341, y MapSize lo puede escribir
        ' el usuario a mano en el config.json. Lo cazo el gate `shadow-degenerate` con Sanitized(999999),
        ' que tiraba OverflowException — o sea la app se caia al cargar un config editado.
        Return If(CLng(v) * v <= CLng(lo) * hi, lo, hi)
    End Function

End Structure


''' <summary>La matematica del encuadre de la luz. **Pura**: sin GL, sin estado, sin config.
''' <para>⛔ Vive separada del renderer a proposito: su resultado no depende de la maquina de nadie, asi
''' que su gate es un self-test de BUILD (Tools/ParityGate, slug <c>shadow-fit</c>) y no puede viajar
''' adentro del binario. Ver memoria 00-reglas-self-tests-no-van-en-el-binario.</para></summary>
Friend Module ShadowMapMath

    ''' <summary>Cuanto MAS GRANDE que su huella se dibuja el quad receptor. El margen existe para que
    ''' el desvanecido del borde tenga donde ocurrir SIN comerse la sombra.
    ''' <para>Fue 1,25 y bajo a 1,05. El fade es INERTE por construccion —la sombra nunca entra en la banda,
    ''' porque la huella es exactamente 1/margen del quad— asi que el margen no compra nitidez: compra
    ''' seguro. A 1,25 ese seguro costaba 1,25^2 = 56 % mas de fragmentos del quad, cada uno pagando el PCF
    ''' completo. A 1,05 cuesta 10 % y sigue habiendo banda.</para></summary>
    Friend Const GroundQuadMargin As Single = 1.05F

    ''' <summary>Donde arranca el desvanecido, en coordenadas locales del quad ([-1..1] por eje).
    ''' <para>⛔ ES EXACTAMENTE <c>1 / GroundQuadMargin</c>, y esa igualdad ES el invariante: la huella
    ''' ocupa <c>1/margen</c> del quad, asi que empezar a desvanecer justo ahi garantiza que TODA la
    ''' sombra se dibuja a intensidad plena y el degrade cae en el margen, donde nunca hay sombra.</para>
    ''' <para>⛔ El valor esta ADEMAS escrito a mano en el GLSL (<c>#define GROUND_FADE_START</c>): un
    ''' Const de VB no se puede concatenar dentro de un Const String. El gate `ground-catcher` compara
    ''' los dos, porque si se separan el sintoma es una sombra recortada y nadie lo relaciona con esto.
    ''' </para></summary>
    Friend Const GroundFadeStart As Single = 1.0F / GroundQuadMargin

    ''' <summary>El encuadre resuelto: las dos matrices, la combinada que consume el fragment, y el
    ''' tamano de un texel en unidades de mundo (que es lo que escala los dos bias).</summary>
    Friend Structure LightFit
        Public View As Matrix4
        Public Proj As Matrix4
        ''' <summary>⛔ `View * Proj`, en ESE orden. Es la misma convencion que ya usa el render para el
        ''' culling (<c>Dim vp As Matrix4 = viewMatrix * projection</c>, Render.RenderAll), y la que hace
        ''' que en GLSL —que lee la matriz de OpenTK transpuesta— quede `proj_glsl * view_glsl * v`.
        ''' Invertir el orden compila, no tira, y proyecta a cualquier lado.</summary>
        Public ViewProj As Matrix4
        Public TexelWorld As Single
        Public Radius As Single
        Public Center As Vector3
        ''' <summary>Rango de profundidad del ortho (far - near). Convierte el bias de texeles a
        ''' unidades de profundidad normalizada.</summary>
        Public DepthRange As Single
        Public Valid As Boolean
    End Structure

    ''' <summary>Encuadra la luz sobre el AABB de la escena.
    '''
    ''' <para>⭐ EL EXTENT SALE DE LA ESFERA ENVOLVENTE, no del AABB proyectado. La esfera es INVARIANTE A
    ''' LA ROTACION, asi que el ortho no cambia de tamano cuando la luz gira. Desde que las luces son fijas
    ''' al mundo el usuario ya no las mueve al orbitar, pero SI al arrastrar un slider del rig, y ademas el
    ''' AABB se mueve solo en cada frame de una animacion: en los dos casos un extent que se re-ajusta hace
    ''' que el borde de la sombra "hierva".</para>
    ''' <para>⚠️ Esto vale para el mapa AJUSTADO. El mapa ANCHO del receptor de suelo NO es invariante a la
    ''' rotacion: su AABB sale de proyectar la escena sobre el plano a lo largo de la luz
    ''' (<see cref="ExpandForGroundShadow"/>), asi que mover la key le cambia el radio y con el la grilla
    ''' del snap. Es aceptable porque solo pasa mientras se arrastra un slider del rig, no al orbitar.</para>
    '''
    ''' <para>⭐ Y el centro se SNAPEA a multiplos de texel en espacio de luz por la misma razon: sin eso
    ''' el borde de la sombra parpadea un texel para adelante y para atras en cada frame.</para></summary>
    ''' <param name="lightDir">Direccion SUPERFICIE→LUZ, normalizada, en mundo (la convencion del rig,
    ''' ver PreviewLight.Direction). La luz mira hacia <c>-lightDir</c>.</param>
    Friend Function Fit(lightDir As Vector3, sceneMin As Vector3, sceneMax As Vector3, mapSize As Integer) As LightFit
        Dim r As New LightFit With {.Valid = False}
        If mapSize <= 0 Then Return r

        ' Escena degenerada o sin cargar: minimo/maximo invertidos o no finitos.
        If Not (IsFinite(sceneMin) AndAlso IsFinite(sceneMax)) Then Return r
        If sceneMax.X < sceneMin.X OrElse sceneMax.Y < sceneMin.Y OrElse sceneMax.Z < sceneMin.Z Then Return r

        Dim lenSq = lightDir.LengthSquared
        If lenSq < 0.000001F OrElse Single.IsNaN(lenSq) Then Return r
        Dim L = Vector3.Normalize(lightDir)

        r.Center = (sceneMin + sceneMax) * 0.5F
        ' Radio de la ESFERA envolvente = media diagonal. Cubre el AABB desde cualquier direccion.
        r.Radius = (sceneMax - sceneMin).Length * 0.5F
        ' Escena de tamano cero (una sola shape degenerada): sin esto el ortho es singular.
        If r.Radius < 0.0001F Then r.Radius = 0.0001F
        ' +2 texeles de margen para que el SNAP de mas abajo —que corre la ventana hasta un texel— no
        ' pueda dejar afuera una esquina del AABB, que sobre la esfera envolvente esta JUSTO en el borde.
        ' Cuesta 2/mapSize de resolucion (0,1 % a 2048) y convierte la contencion en un invariante exacto,
        ' que es lo que verifica el gate `shadow-fit`.
        r.Radius *= (1.0F + 2.0F / mapSize)

        ' `up` no puede ser paralelo a L o LookAt devuelve NaN. El mundo del previewer es Z-up
        ' (ver hemiAmbient y PreviewLight.Direction), asi que el caso degenerado es la luz cenital.
        Dim up As Vector3 = If(Math.Abs(L.Z) > 0.999F, New Vector3(0, 1, 0), New Vector3(0, 0, 1))

        Dim pad As Single = r.Radius * 0.05F + 0.01F

        ' ⛔⛔ LA VIEW SE ANCLA AL ORIGEN DEL MUNDO, NO AL CENTRO DE LA ESCENA. Si mirara al centro, ese
        ' centro caeria SIEMPRE en (0,0) de espacio de luz y el snap de abajo seria un NO-OP: literalmente
        ' incapaz de cambiar nada, y con el una ley del gate incapaz de fallar. Lo destapo el CONTROL
        ' NEGATIVO de `shadow-fit`: sacando el snap el gate seguia verde. Anclada al origen, la grilla de
        ' texeles queda fija en el mundo y el snap SI la mueve de a un texel entero — que es lo que evita
        ' que el borde de la sombra hierva cuando el AABB se traslada (cada frame de una animacion lo mueve).
        r.View = Matrix4.LookAt(Vector3.Zero, -L, up)

        r.TexelWorld = (2.0F * r.Radius) / mapSize

        Dim centerLS As Vector3 = Vector3.TransformPosition(r.Center, r.View)
        Dim sx As Single = CSng(Math.Floor(centerLS.X / r.TexelWorld)) * r.TexelWorld
        Dim sy As Single = CSng(Math.Floor(centerLS.Y / r.TexelWorld)) * r.TexelWorld

        ' Profundidad alrededor del centro. En una LookAt right-handed lo que esta DELANTE tiene Z
        ' negativa, y el ortho toma near/far como distancias sobre -Z; por eso el signo. Pueden salir
        ' negativas si la escena queda "detras" del origen y esta bien: un ortho es un mapeo lineal y no
        ' exige near > 0.
        Dim distToCenter As Single = -centerLS.Z
        Dim zNear As Single = distToCenter - r.Radius - pad
        Dim zFar As Single = distToCenter + r.Radius + pad
        r.DepthRange = zFar - zNear
        r.Proj = Matrix4.CreateOrthographicOffCenter(sx - r.Radius, sx + r.Radius,
                                                     sy - r.Radius, sy + r.Radius,
                                                     zNear, zFar)
        r.ViewProj = r.View * r.Proj
        r.Valid = True
        Return r
    End Function

    ''' <summary>Agranda el AABB para que quepa TAMBIEN la sombra proyectada sobre el plano del suelo.
    '''
    ''' <para>⛔ SIN ESTO EL RECEPTOR DE SUELO SALE CORTADO, y el sintoma no apunta al encuadre: la sombra
    ''' de la cabeza cae LEJOS del personaje —a <c>altura / tan(elevacion)</c> del pie— y con el mapa
    ''' ajustado a la esfera del cuerpo eso cae afuera, donde la textura devuelve el borde blanco = "sin
    ''' ocluir". Resultado: una sombra que se corta en seco a media distancia.</para>
    '''
    ''' <para>Se proyectan las 8 esquinas del AABB sobre el plano a lo largo de <c>-lightDir</c> y se une
    ''' todo. El costo es un radio mayor —o sea texeles mas grandes—, por eso solo se llama cuando el
    ''' receptor de suelo esta encendido.</para>
    '''
    ''' <para>Devuelve el AABB sin tocar si la luz esta en el horizonte o por debajo (<c>L.Z</c> chico o
    ''' negativo): ahi la sombra se va al infinito y no hay encuadre finito que la contenga. El receptor
    ''' de suelo se apaga solo en ese caso, via <paramref name="valid"/>.</para></summary>
    ''' <summary>Seno de la elevacion minima de la key para que el receptor de suelo tenga sentido. La UI
    ''' la lee de aca: es la MISMA cantidad que decide el render, no una copia redondeada.</summary>
    Friend Const SenoDeElevacionMinima As Single = 0.2F

    ''' <summary>Esa misma elevacion, en grados, para mostrarla. Se calcula, no se transcribe.</summary>
    Friend ReadOnly Property ElevacionMinimaGrados As Single
        Get
            Return CSng(Math.Asin(SenoDeElevacionMinima) * 180.0 / Math.PI)
        End Get
    End Property

    Friend Sub ExpandForGroundShadow(ByRef sceneMin As Vector3, ByRef sceneMax As Vector3,
                                     lightDir As Vector3, groundZ As Single, ByRef valid As Boolean)
        valid = False
        If Not (IsFinite(sceneMin) AndAlso IsFinite(sceneMax)) Then Exit Sub
        If sceneMax.X < sceneMin.X OrElse sceneMax.Y < sceneMin.Y OrElse sceneMax.Z < sceneMin.Z Then Exit Sub
        If lightDir.LengthSquared < 0.000001F Then Exit Sub
        Dim L = Vector3.Normalize(lightDir)
        ' Elevacion minima: por debajo de este corte la sombra se estira tanto que el mapa pierde toda la
        ' resolucion util. Es un corte de calidad, no de correccion.
        ' ⛔ LA CONSTANTE SE EXPONE (SenoDeElevacionMinima) porque el DIALOGO la necesita para deshabilitar
        ' la casilla y para decir el numero en el texto. La tenia copiada como "11.54F  ' asin(0.2)", que es
        ' 11,5370 redondeado PARA ARRIBA: quedaba una banda muerta en [11,5370 , 11,54) donde el motor si
        ' dibuja y la casilla estaba gris, y el texto —formateado a un decimal— mostraba "11,5", un valor
        ' con el que la propia casilla se deshabilita. Un literal duplicado y redondeado a mano.
        If L.Z < SenoDeElevacionMinima Then Exit Sub

        Dim mn = sceneMin, mx = sceneMax
        ' Los 8 vertices sin `For Each {…}`: en VB ese literal materializa un array POR ENTRADA al bucle
        ' (1 + 2 + 4 = 7 por frame). Son bytes, pero este metodo corre en el camino de dibujo y la politica
        ' de este mismo archivo es no dejar basura de GC ahi (ver _shadowCasters en Render.vb).
        For i = 0 To 7
            Dim cx As Single = If((i And 1) = 0, sceneMin.X, sceneMax.X)
            Dim cy As Single = If((i And 2) = 0, sceneMin.Y, sceneMax.Y)
            Dim cz As Single = If((i And 4) = 0, sceneMin.Z, sceneMax.Z)
            ' Un punto p proyecta su sombra sobre z = groundZ recorriendo -L hasta el plano.
            Dim t As Single = (cz - groundZ) / L.Z
            If t <= 0.0F Then Continue For          ' ya esta en el plano o por debajo
            Dim px As Single = cx - L.X * t
            Dim py As Single = cy - L.Y * t
            mn.X = Math.Min(mn.X, px) : mx.X = Math.Max(mx.X, px)
            mn.Y = Math.Min(mn.Y, py) : mx.Y = Math.Max(mx.Y, py)
        Next
        mn.Z = Math.Min(mn.Z, groundZ)
        mx.Z = Math.Max(mx.Z, groundZ)

        If Not (IsFinite(mn) AndAlso IsFinite(mx)) Then Exit Sub
        sceneMin = mn
        sceneMax = mx
        valid = True
    End Sub

    ''' <summary>Resolucion del mapa ANCHO del receptor de suelo, a partir de los dos radios.
    ''' <para>El radio del encuadre del suelo depende de la ELEVACION de la key —la sombra mide
    ''' <c>altura / tan(elev)</c>—, asi que una fraccion fija de <paramref name="mapSize"/> hacia variar el
    ''' texel del suelo ~2x entre presets: Studio 0,36 u y Portrait 0,73, o sea que el preset que MAS
    ''' muestra la sombra en el piso era el que peor la dibujaba. Aca se apunta a una relacion de texeles
    ''' constante contra el mapa del personaje.</para>
    ''' <para>⛔ ES PURA, y vive aca y no en el render, porque tiene dos trampas que un gate SI puede
    ''' cubrir y un A/B de pixeles no: (1) el minimo no puede ser mayor que el maximo —<c>Sanitized()</c>
    ''' deja pasar <c>MapSize = 256</c>, y un <c>Math.Clamp(x, 512, 256)</c> tira ArgumentException en el
    ''' camino de dibujo, cada frame; (2) el producto tiene que caber en un Integer, y una shape
    ''' degenerada da un radio de personaje de 1e-4 contra uno proyectado de cientos de unidades.</para>
    ''' <para>⛔⭐ SIN HISTERESIS, Y ES UNA DECISION. Tuvo una: conservaba el tamano vigente mientras
    ''' estuviera adentro de un factor 2 del pedido, para que un ratio parado cerca del punto medio entre
    ''' dos potencias de dos no hiciera que <c>Ensure</c> destruyera y recreara la textura y el FBO en cada
    ''' frame de animacion. El precio era inaceptable: el resultado pasaba a depender de la HISTORIA. La
    ''' misma escena con la misma config se dibujaba con dos nitideces de sombra de suelo distintas segun de
    ''' que preset se viniera —cambiar de Studio a Portrait conservaba 2048 para siempre, porque nada libera
    ''' el target al cambiar de luces— y eso invalida cualquier A/B de pixeles sobre el receptor. Un frame
    ''' tiene que ser funcion de (escena, config) y nada mas. Ademas duplicaba de hecho la relacion de
    ''' texeles que el Const de abajo documenta.
    ''' <para>⚠️ RIESGO RESIDUAL, ABIERTO Y CUANTIFICADO. Sin banda muerta, el recrear-por-frame vuelve a
    ''' ser posible: con <c>MapSize = 2048</c> las salidas son {512, 1024, 2048} y las fronteras caen en
    ''' ratio 1,7688 y 3,5364, asi que un ratio parado ahi cruza con una variacion relativa del 0,03 %. Cada
    ''' cruce es <c>Release()</c> + <c>TexImage2D</c> + <c>CheckFramebufferStatus</c>, o sea dos puntos de
    ''' sincronizacion con el driver en el camino de dibujo. Se ve como un tiron. Los dos gestos que lo
    ''' disparan: una animacion en loop cuyo ratio ronde una frontera, y arrastrar el slider de elevacion de
    ''' la key parado sobre una. Mitiga que el ratio es mucho mas estable que cualquiera de los dos radios
    ''' (numerador y denominador salen del mismo AABB y se mueven juntos) y que las fronteras estan a un
    ''' factor 2 una de otra, o sea que hay que estar JUSTO encima.
    ''' <para>Se elige convivir con eso: la alternativa era la histeresis, y un resultado que depende de por
    ''' donde pasaste no se ve de ninguna forma, ni en pantalla ni en un gate. Un tiron ocasional si.</para>
    ''' </para></summary>
    Friend Function GroundMapSize(charRadius As Single, groundRadius As Single, mapSize As Integer) As Integer
        If mapSize <= 0 Then Return 0
        Dim minimo As Integer = Math.Min(512, mapSize)
        ' ⛔ El IsNaN va DESPUES del Clamp: Math.Clamp propaga NaN en Single, asi que chequear antes no
        ' cubre el NaN que puede nacer de la division.
        Dim ratio As Single = Math.Clamp(groundRadius / Math.Max(charRadius, 0.0001F), 1.0F, 64.0F)
        If Single.IsNaN(ratio) Then ratio = 1.0F
        Dim pedido As Integer = PreviewShadowSettings.RoundToPowerOfTwo(CInt(mapSize * ratio / GroundTexelRatioTarget))
        Return Math.Clamp(pedido, minimo, mapSize)
    End Function

    ''' <summary>Relacion de texeles a la que se apunta entre el mapa del suelo y el del personaje.
    ''' ⛔ Es un OBJETIVO, no una cota: RoundToPowerOfTwo redondea al mas cercano, asi que la relacion real
    ''' puede llegar a 5*raiz(2) = 7,07. Redondear hacia arriba la acotaria de verdad, al precio de
    ''' duplicar la VRAM del mapa ancho en la mitad de los casos.</summary>
    Friend Const GroundTexelRatioTarget As Single = 5.0F

    ''' <summary>De la huella que devuelve <see cref="ExpandForGroundShadow"/> al quad receptor:
    ''' centro en el plano del piso y semi-extensiones POR EJE, con el margen del desvanecido.
    ''' <para>Vive aca y no en el render para que el gate `ground-catcher` ejercite exactamente la
    ''' cuenta que corre en el frame. Un gate que re-implementa la formula que quiere verificar solo
    ''' comprueba que sabe copiar.</para></summary>
    Friend Sub GroundQuadFromFootprint(fpMin As Vector3, fpMax As Vector3, groundZ As Single,
                                       ByRef center As Vector3, ByRef half As Vector2)
        center = New Vector3((fpMin.X + fpMax.X) * 0.5F, (fpMin.Y + fpMax.Y) * 0.5F, groundZ)
        half = New Vector2((fpMax.X - fpMin.X) * 0.5F, (fpMax.Y - fpMin.Y) * 0.5F) * GroundQuadMargin
        ' ⛔ PISO POSITIVO EN LOS DOS EJES. Una escena plana en un eje (una sola shape degenerada, o un
        ' plano) da semi-extension 0: el quad no dibujaba nada, pero recien DESPUES de que el pase de
        ' profundidad ancho ya habia corrido y de dejar el programa del suelo bindeado. Y el gate dividia
        ' por esa semi-extension, con lo cual su comparacion daba NaN y NaN > 0.8 es False: verde.
        half.X = Math.Max(half.X, 0.0001F)
        half.Y = Math.Max(half.Y, 0.0001F)
    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function IsFinite(v As Vector3) As Boolean
        Return Not (Single.IsNaN(v.X) OrElse Single.IsNaN(v.Y) OrElse Single.IsNaN(v.Z) OrElse
                    Single.IsInfinity(v.X) OrElse Single.IsInfinity(v.Y) OrElse Single.IsInfinity(v.Z))
    End Function

End Module


''' <summary>El FBO de profundidad y su textura. Solo recursos GL + ciclo de vida; el encuadre lo
''' resuelve <see cref="ShadowMapMath"/> y el dibujo lo hace PreviewModel (que es quien tiene las mallas).
''' <para>⛔ Sin color attachment: <c>DrawBuffer(None)</c> + <c>ReadBuffer(None)</c>, o el FBO queda
''' incompleto en algunos drivers.</para></summary>
Friend Class ShadowMapTarget
    Implements IDisposable

    Private _fbo As Integer
    Private _tex As Integer
    Private _size As Integer

    Friend ReadOnly Property Texture As Integer
        Get
            Return _tex
        End Get
    End Property

    Friend ReadOnly Property Size As Integer
        Get
            Return _size
        End Get
    End Property

    Friend ReadOnly Property Ready As Boolean
        Get
            Return _fbo > 0 AndAlso _tex > 0
        End Get
    End Property

    ''' <summary>(Re)crea el target si cambio el tamano. Devuelve False si el FBO no queda completo —
    ''' el caller tiene que degradar a "sin sombras", NO dibujar igual.</summary>
    Friend Function Ensure(size As Integer) As Boolean
        If size <= 0 Then Return False
        If _fbo > 0 AndAlso _tex > 0 AndAlso _size = size Then Return True
        Release()

        _tex = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, _tex)
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, size, size, 0,
                      PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        ' CLAMP_TO_BORDER + borde 1.0 => todo lo que cae FUERA del mapa lee "sin ocluir". Con CLAMP_TO_EDGE
        ' el borde del mapa se estira y proyecta una sombra falsa por toda la escena.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToBorder))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToBorder))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, New Single() {1.0F, 1.0F, 1.0F, 1.0F})
        ' Modo comparacion: el sampler2DShadow del fragment devuelve el PCF por hardware.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, CInt(TextureCompareMode.CompareRefToTexture))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, CInt(All.Lequal))
        GL.BindTexture(TextureTarget.Texture2D, 0)

        Dim prevFbo As Integer = GL.GetInteger(GetPName.FramebufferBinding)
        _fbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                TextureTarget.Texture2D, _tex, 0)
        GL.DrawBuffer(DrawBufferMode.None)
        GL.ReadBuffer(ReadBufferMode.None)
        Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)

        If status <> FramebufferErrorCode.FramebufferComplete Then
            Logger.Log($"[SHADOW] FBO incompleto ({status}) con size={size}: sombras desactivadas este frame.")
            Release()
            Return False
        End If

        _size = size
        Return True
    End Function

    ''' <summary>Bindea el FBO y deja el viewport en el tamano del mapa. NO consulta el estado previo:
    ''' el caller lo captura UNA vez antes del primer mapa y lo restaura despues del ultimo, asi que un
    ''' glGet por pase seria un resultado que se descarta. Y los glGet de framebuffer son justo los que
    ''' fuerzan a varios drivers a vaciar la lista de comandos diferida.</summary>
    Friend Sub BindForWrite()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.Viewport(0, 0, _size, _size)
    End Sub

    Friend Sub Release()
        If _fbo > 0 Then
            Try : GL.DeleteFramebuffer(_fbo) : Catch : End Try
            _fbo = 0
        End If
        If _tex > 0 Then
            Try : GL.DeleteTexture(_tex) : Catch : End Try
            _tex = 0
        End If
        _size = 0
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Release()
        GC.SuppressFinalize(Me)
    End Sub

End Class


''' <summary>El quad del receptor de suelo. Dos triangulos y nada mas: toda la logica esta en el
''' fragment (ver <see cref="GroundShadowShaderSource"/>).
''' <para>⛔ Se dibuja DESPUES de opaco/cutout/decal y ANTES de blended: asi el personaje lo tapa por
''' depth-test donde corresponde, y lo blended (pelo alpha-blend, ojos) compone encima. Con
''' <c>DepthMask(False)</c>, porque un plano gigante escribiendo profundidad arruinaria el orden del
''' pase blended que viene despues.</para></summary>
Friend Class GroundShadowQuad
    Implements IDisposable

    Private _vao As Integer
    Private _vbo As Integer

    ''' <summary>Quad unitario en XY, dos triangulos. Las coordenadas van de -1 a 1 y el vertex shader las
    ''' escala; asi el buffer nunca cambia aunque la escena si.</summary>
    Private Shared ReadOnly Corners As Single() = {
        -1.0F, -1.0F, 1.0F, -1.0F, 1.0F, 1.0F,
        -1.0F, -1.0F, 1.0F, 1.0F, -1.0F, 1.0F}

    Private Sub EnsureBuffers()
        If _vao > 0 Then Return
        _vao = GL.GenVertexArray()
        _vbo = GL.GenBuffer()
        GL.BindVertexArray(_vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, Corners.Length * 4, Corners, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, False, 0, 0)
        GL.BindVertexArray(0)
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
    End Sub

    ''' <param name="viewProj">`view * projection` de la CAMARA, en la convencion del render.</param>
    ''' <param name="center">Centro del quad en mundo (con Z = el plano del suelo).</param>
    ''' <param name="half">Semi-extensiones en X e Y, en unidades de mundo.</param>
    ''' <summary>Dibuja el receptor. <paramref name="half"/> son las semi-extensiones EN X e Y por
    ''' separado, no un radio.
    ''' <para>⛔⭐ ANTES ERA UN ESCALAR, Y RECORTABA LA CABEZA. Se le pasaba <c>LightFit.Radius</c>, que
    ''' es la media diagonal de la esfera envolvente 3D — o sea que la ALTURA de la escena entraba en el
    ''' tamano de un quad que vive en el plano XY. Con el preset Studio (key a 25,88 grados) un cuerpo de
    ''' 180 u proyecta una sombra de ~430 u: la punta caia a 0,90 del radio, el desvanecido arrancaba en
    ''' 0,72 y la sombra de la CABEZA se dibujaba al ~28 %, apagandose antes de terminar. Justo el
    ''' sintoma que ExpandForGroundShadow dice haber arreglado, reintroducido dos capas mas abajo.</para>
    ''' <para>La huella es MUY anisotropa (~430 x 100 en ese mismo caso): un solo numero no puede
    ''' describirla sin sobrar en un eje y faltar en el otro.</para></summary>
    Friend Sub Render(shader As Shader_Base_Class, viewProj As Matrix4, center As Vector3, half As Vector2)
        If shader Is Nothing OrElse half.X <= 0.0F OrElse half.Y <= 0.0F Then Exit Sub
        EnsureBuffers()
        If _vao = 0 Then Exit Sub

        shader.Use()
        shader.SetMatrix4("matViewProj", viewProj)
        shader.SetVector3("uGroundCenter", center)
        shader.SetVector2("uGroundHalf", half)

        GL.Enable(EnableCap.DepthTest)
        GL.DepthMask(False)
        GL.Disable(EnableCap.CullFace)      ' visible desde arriba y desde abajo
        GL.Enable(EnableCap.Blend)
        ' MULTIPLICATIVO: resultado = destino x fuente. Ver el doc del fragment.
        GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.SrcColor)

        GL.BindVertexArray(_vao)
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
        GL.BindVertexArray(0)

        GL.Disable(EnableCap.Blend)
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
        GL.DepthMask(True)
        GL.Enable(EnableCap.CullFace)
        GL.CullFace(TriangleFace.Back)
    End Sub

    Friend Sub Release()
        If _vbo > 0 Then
            Try : GL.DeleteBuffer(_vbo) : Catch : End Try
            _vbo = 0
        End If
        If _vao > 0 Then
            Try : GL.DeleteVertexArray(_vao) : Catch : End Try
            _vao = 0
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Release()
        GC.SuppressFinalize(Me)
    End Sub

End Class
