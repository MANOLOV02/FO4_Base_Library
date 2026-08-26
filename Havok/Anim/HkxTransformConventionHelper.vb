Option Strict On
Option Explicit On

Imports System.Linq
Imports OpenTK.Mathematics

''' <summary>
''' Canonical HKX QS-transform conversion used by both BSClothExtraData skeleton
''' injection and HKX pose import. HKX quaternions are xyzw, matching OpenTK.
''' </summary>
Public NotInheritable Class HkxTransformConventionHelper
    Private Sub New()
    End Sub

    ''' <summary>
    ''' La ESCALA de un `hkQsTransform`, por eje (0=X 1=Y 2=Z). Son los slots 8..10.
    ''' <para>⛔ EXISTE PORQUE LA EXCLUSIVIDAD QUE DECLARA LA CABECERA DE ESTE ARCHIVO ERA FALSA.
    ''' `HkxEscalaProbe` reescribia `qs(8)`, `qs(9)` y `qs(10)` en cuatro sitios, con su propio
    ''' comentario reenunciando el layout — y ese probe es el que MIDE la escala del corpus, o sea que
    ''' si leyera los indices equivocados caeria la justificacion del hueco de escala no finita.</para>
    ''' <para>Devuelve `Single.NaN` si el arreglo no tiene los 12 floats o el eje esta fuera de rango:
    ''' el llamador ya tiene que mirar `IsFinite` para decidir, y un centinela numerico se confundiria
    ''' con un dato.</para>
    ''' </summary>
    Public Shared Function EscalaDe(qs As Single(), eje As Integer) As Single
        If qs Is Nothing OrElse qs.Length < 12 OrElse eje < 0 OrElse eje > 2 Then Return Single.NaN
        Return qs(8 + eje)
    End Function

    ''' <summary>
    ''' ⛔⛔ EL LAYOUT DE `hkQsTransform`, UNA SOLA VEZ EN TODO EL ÁRBOL: 12 floats con
    ''' <b>traslación 0..2 · rotación 4..7 · escala 8..10</b> (el 3 y el 11 son el relleno del
    ''' `vector4`). Leer la rotación en 0..3 — el error obvio — da un cuaternión que parece válido y
    ''' rota cualquier cosa; ya pasó en tres consumidores a la vez.
    ''' <para>Acá no se materializa ningún objeto intermedio: este camino lo recorre el render por
    ''' hueso y por frame.</para>
    ''' </summary>
    Public Shared Function ToTransform(qs As Single()) As Transform_Class
        If qs Is Nothing OrElse qs.Length < 12 Then Return New Transform_Class()
        Return ToTransformRaw(qs(0), qs(1), qs(2), qs(4), qs(5), qs(6), qs(7), qs(8), qs(9), qs(10))
    End Function

    ''' <summary>Donde `2/|q|²` se desborda a +Inf en Single, o sea donde
    ''' `Matrix4.CreateFromQuaternion` empieza a devolver NaN. MEDIDO contra
    ''' OpenTK.Mathematics 4.9.3: 5,877472e-39.</summary>
    Private Shared ReadOnly EPS_OPENTK As Single = 2.0F / Single.MaxValue

    ''' <summary>
    ''' Version SIN ALLOCAR de <see cref="ToTransform"/>: el cuaternion entra como cuatro floats.
    ''' <para>⛔ Existe por el camino CALIENTE de animacion: `BuildPose` la llama por hueso y por
    ''' frame. Pasar por `HkxQuaternionGraph_Class` construia un objeto por llamada, que con ~100
    ''' huesos a 30 fps son miles de allocaciones por segundo y se nota en la fluidez.</para>
    ''' </summary>
    Public Shared Function ToTransformRaw(translationX As Single, translationY As Single, translationZ As Single,
                                          rotX As Single, rotY As Single, rotZ As Single, rotW As Single,
                                          scaleX As Single, scaleY As Single, scaleZ As Single) As Transform_Class
        ' ⛔⛔ NI LA ESCALA SE CORRIGE NI EL CUATERNION SE NORMALIZA. LOS DOS SALEN DEL BINARIO.
        '
        ' (1) LA ESCALA PASA COMO LA DECLARA EL ARCHIVO. En las CINCO operaciones canonicas de
        '     `hkQsTransform` que se leyeron —`setMul` (FO4 0x141594490 / SSE 0x140BBCB6C, cola
        '     `mulps xmm0,[rsi+0x20]` y `movups [rax+0x20],xmm0` en 0x1415945CC/DA, sin una sola
        '     comparacion), `setInterpolate4` (0x141594309), `setInverse` (0x141594451),
        '     `fastRenormalize` (0x141594EA0) e `isOk` (0x141594B10 -> 0x141483D70)— la escala 0 pasa
        '     sin tocarse. `isOk` ni la mira: solo exige que no sea NaN, asi que para el motor una
        '     escala 0 es un `hkQsTransform` VALIDO.
        '     La unica sustitucion por 1 del motor entero esta en `blendNormalize` (0x1419C07E1) y es
        '     por VECTOR COMPLETO: `lengthSq3(scale) < 1.1920929e-07` => `[1,1,1,1]` de 0x142F3C560.
        '     Aca habia un `EjeValido` POR EJE con umbral 1e-6: con escala `(0,1,1)` el lengthSq3 vale
        '     2 y el motor la deja intacta, y la app la volvia `(1,1,1)`. Y con NaN el `cmpltps` da
        '     falso — el motor la deja pasar — y la app la volvia 1. Tres divergencias, ninguna citada.
        '
        ' (2) EL CUATERNION DEGENERADO SE DECIDE POR EL MODO DE FALLA REAL, NO POR UNA CITA PRESTADA.
        '     El predicado vivo esta abajo, junto con la medicion que lo fija. Aca NO se repite: el
        '     parrafo estuvo dos veces y las dos copias derivaron — una llego a declarar como vivo un
        '     `= 0` que ya se habia retirado por medicion.
        '
        '     ⚠️ Y NO SE PUEDE SACAR LA GUARDA ENTERA — lo intente y estaba mal. `Matrix4.CreateFromQuaternion`
        '     de OpenTK calcula `s = 2 / |q|²` (IL_0092..IL_009f), o sea que NORMALIZA ADENTRO: quitar
        '     el `Quaternion.Normalize` explicito es un no-op para todo `|q|² > 0`, pero con
        '     `q = (0,0,0,0)` da `2/0 = +Inf` y la 3x3 entera sale NaN, que despues se propaga por
        '     `ComputeEmbeddedBindWorld` a todo el subarbol del hueso. El motor no produce eso: su
        '     propio `isOk` (0x141594B10 -> 0x141483D70) EXIGE que no haya NaN.
        ' ⚠️ HUECO DECLARADO — LA ESCALA NO FINITA. Al sacar `EjeValido` (que reponia 1.0 para un eje
        ' con |v| <= 1e-6 O no finito) se fue tambien la guarda de NaN/Inf, y con una escala no finita
        ' esta matriz sale NaN y se propaga por `ComposeTransforms` a todo el subarbol del hueso.
        ' NO se repone: el motor no sanea, ASERTA (`isOk` 0x141594B10 -> 0x141483D70), asi que un
        ' `1.0` aca seria una ley inventada — y el 1e-6 que la acompañaba tampoco tenia cita.
        ' MEDIDO con `HkxEscalaProbe` sobre los 1.099 `.hkx` del corpus: COMPONENTES DE ESCALA
        ' DEGENERADOS (cero o no finito) = 0, NEGATIVOS = 0. ⚠️ Ese probe mide la ESCALA por eje y NO
        ' dice nada de cuaterniones: la medicion cubre este hueco, no el de la rotacion.
        ' ⛔ EL PREDICADO SALE DEL MODO DE FALLA REAL, MEDIDO — no de una cita prestada ni de un
        '     razonamiento. `Matrix4.CreateFromQuaternion` de OpenTK calcula `s = 2 / |q|²`
        '     (IL_0092..IL_009f), asi que rompe cuando ESE COCIENTE se desborda, no solo con `|q|² = 0`.
        '     MEDIDO contra OpenTK.Mathematics **4.9.3** —la que resuelve `project.assets.json`, no la
        '     4.0.2 del `PackageReference`—: la ventana rota es `0 <= |q|² < 2/Single.MaxValue`
        '     (5,877472e-39). Con `q = (0,0,0,1e-20)`, `|q|² = 9,99e-41` y la 4x4 sale ENTERA en NaN.
        '     Escribi un rato `= 0.0F` "porque es el unico que rompe": era falso y era una REGRESION —
        '     el codigo viejo normalizaba y no producia NaN en ninguno de esos valores.
        '     El umbral es `2/Single.MaxValue` DERIVADO de esa medicion, no elegido: es exactamente
        '     donde el cociente se desborda. NO es una ley del motor y no se presenta como tal — es el
        '     limite de ESTE constructor de matrices. El neutro SI es el del motor: `(0,0,0,1)`, la
        '     constante de 0x142F3C730.
        '     ⛔ Y NO SE NORMALIZA EXPLICITAMENTE. Lo probe: `CreateFromQuaternion(Normalize(q))` da la
        '     misma matriz en aritmetica exacta pero NO bit a bit — `x*y*(2/|q|²)` y
        '     `(x/|q|)*(y/|q|)*2` redondean distinto —, y eso mueve los ultimos digitos de todo el
        '     render. MEDIDO sobre las 5 prendas: 1 de 5 cambiaba el `.diag` y los 30 PNG diferian.
        '     La guarda sola cierra la ventana sin tocar un solo numero de lo que ya andaba.
        '     Aca hubo antes un `<= 1e-6` sin cita y despues un `< HK_REAL_EPSILON` (1,19e-7) con la
        '     cita de `hkQsTransform::blendNormalize` (0x1419C0794). Esa constante es REAL pero es de
        '     OTRA operacion: el motor la usa al cerrar una acumulacion ponderada de blend, no al
        '     convertir un `hkQsTransform` a matriz. Traerla aca mataba a identidad toda rotacion con
        '     |q| en (0 , 3,45e-4] que el motor normaliza sin chistar — una regla de la app con una
        '     cita puesta encima, que es exactamente lo que la regla 1 prohibe.
        Dim rotation As New Quaternion(rotX, rotY, rotZ, rotW)
        ' `Not (x > eps)` y no `x <= eps`: asi el NaN tambien cae en la guarda.
        If Not (rotation.LengthSquared > EPS_OPENTK) Then rotation = Quaternion.Identity
        Dim transformMatrix =
            Matrix4.CreateScale(scaleX, scaleY, scaleZ) *
            Matrix4.CreateFromQuaternion(rotation) *
            Matrix4.CreateTranslation(translationX, translationY, translationZ)
        Return New Transform_Class(transformMatrix)
    End Function

End Class
