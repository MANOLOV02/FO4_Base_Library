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

    ''' <summary>Radio del PCF en TEXELES del mapa. 0 = un solo tap (lo que hace el forward de FO4).
    ''' El kernel es (2r+1)^2 con r = redondeo de este valor, acotado en MaxPcfRadius.</summary>
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

    ''' <summary>Tope del radio de PCF. No es configurable: acota el costo del kernel en el fragment.</summary>
    Public Const MaxPcfRadius As Integer = 4

    Public Shared Function Defaults() As PreviewShadowSettings
        Return New PreviewShadowSettings With {
            .Enabled = True,
            .MapSize = 2048,
            .SoftnessTexels = 1.5F,
            .Intensity = 1.0F,
            .NormalBiasTexels = 2.0F,
            .DepthBiasTexels = 1.5F}
    End Function

    ''' <summary>Copia con los valores acotados al rango que el render sabe ejecutar. Lo llama el render
    ''' en vez de confiar en el config: un MapSize absurdo cargado a mano no puede tirar la app.</summary>
    Public Function Sanitized() As PreviewShadowSettings
        Dim s = Me
        If s.MapSize <= 0 Then Return Defaults()
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
    ''' LA ROTACION, asi que el ortho no cambia de tamano cuando la luz gira — y aca la luz gira todo el
    ''' tiempo, porque las direcciones del rig se derivan de la camara (PreviewLight.Direction usa
    ''' cam.Forward). Con un extent que se re-ajusta por frame, la sombra "hierve" al orbitar.</para>
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

    ''' <summary>Bindea el FBO y deja el viewport en el tamano del mapa. Devuelve el FBO previo para que
    ''' el caller lo restaure (el compositor de FaceTint tambien bindea FBOs: asumir 0 esta mal).</summary>
    Friend Function BindForWrite() As Integer
        Dim prevFbo As Integer = GL.GetInteger(GetPName.FramebufferBinding)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.Viewport(0, 0, _size, _size)
        Return prevFbo
    End Function

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
