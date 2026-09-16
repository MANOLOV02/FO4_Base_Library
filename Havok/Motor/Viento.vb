Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' EL VIENTO — las acciones de `hclSimpleWindAction` y la subclase de Bethesda
' `hclBSClothParameterizedWindAction`. RE: RE_MOTOR_FISICA_CANONICO_2026-09-05.md cap. 6.8, 6.8.1,
' 6.8bis y 6.10bis; transcrito acá instrucción por instrucción.
'
' La cadena:
'   (1) arranque del mundo (`0x141875870`): 10 acciones de Bethesda con dirección (0,0,0,0), cada una
'       avanzada `i·(0,8/10)` (`0x141875B37`-`0x141875C06`);
'   (2) al enganchar la prenda (`0x1418A0BB4`-`0x1418A0BC7`): una del pool a `clothInstance+0x50`
'       (`0x1418C86A0`) y `[+0x58] = 1`;
'   (3) por cuadro (`0x1418752B8`-`0x1418752F5`): si `mgr[+0x170]`, `update(dt)` a las 10;
'   (4) `prepare` (`0x14195BB87`-…): la lista activa se rearma de cinco fuentes filtrando por
'       `vtbl[+0x30]`;
'   (5) cada substep (`0x141A13066`-`0x141A130AB`): `applyAction(sim, dtSub, fuerzas)`.
'
' ⛔ `mgr[+0x170]` sólo lo escribe `0x141878690`, que llama el sistema de cielo: la app no tiene clima,
' así que el pool NO se actualiza y cada acción conserva la `airVelocity` de su constructor.
' =================================================================================================

Namespace Havok.Motor

    ''' <summary>Un `hclSimpleWindAction` (o la subclase de Bethesda) con su layout.</summary>
    Friend NotInheritable Class AccionDeViento

        ''' <summary>`+0x10`</summary>
        Friend Direccion As Vector128(Of Single)
        ''' <summary>`+0x20`</summary>
        Friend VelMin As Single
        ''' <summary>`+0x24`</summary>
        Friend VelMax As Single
        ''' <summary>`+0x28`</summary>
        Friend Frecuencia As Single
        ''' <summary>`+0x2C`</summary>
        Friend ArrastreMaximo As Single
        ''' <summary>`+0x30`</summary>
        Friend VelocidadDelAire As Vector128(Of Single)
        ''' <summary>`+0x40`</summary>
        Friend TiempoActual As Single
        ''' <summary>`+0x58` — sólo en la subclase de Bethesda.</summary>
        Friend Encendida As Boolean
        ''' <summary>Vtable `0x1426B4838` (Bethesda) o `0x142704220` (`hclSimpleWindAction`).</summary>
        Friend ReadOnly DeBethesda As Boolean

        Private Sub New(deBethesda As Boolean)
            Me.DeBethesda = deBethesda
        End Sub

        ''' <summary>
        ''' `0x1418C3F60` → `0x1418F8310`: la acción de Bethesda del pool.
        ''' <para>```
        ''' dirección = n &gt; 0 ? dir · rsqrtNewton(n) : dir     ' 0x1418F833A…39F, n = dot3(dir, dir)
        ''' currentTime = 0; max = 5; min = −1; freq = 0,5; drag = 3   ' 0x1418F83A3…BF
        ''' airVelocity = velocidad(currentTime = 0) · dirección  ' 0x1418F83C6 (0x1418F8580)
        ''' encendida = 0                                        ' 0x1418C3F75
        ''' ```</para>
        ''' <para>`0x1418F8580` con `currentTime = 0`: el octante sale 0 (`cvttps2dq` de 0), el
        ''' polinomio del seno de 0 da `+0`, `min(+0, 1) = +0`, y `(0 + 1)·0,5 = 0,5`
        ''' (`0x1418F86C4`-`0x1418F86E1`); la velocidad es `0,5·(max − min) + min`
        ''' (`0x1418F86E9`/`0x1418F86ED`).</para>
        ''' </summary>
        Friend Shared Function DeBethesdaConDireccion(dir As Vector128(Of Single)) As AccionDeViento
            Dim a As New AccionDeViento(True)
            Dim n = Hsum3(Vector128.Multiply(dir, dir))                        ' 0x1418F8341…5F
            Dim esPositivo = Vector128.LessThan(Vector128.Subtract(Vector128(Of Single).Zero, n), Vector128(Of Single).Zero)   ' 0x1418F836C/6F
            Dim noPositivo = Vector128.LessThanOrEqual(n, Vector128(Of Single).Zero)                                           ' 0x1418F8376
            Dim newton = Vector128.AndNot(Simd.RsqrtNewton(n), noPositivo)     ' 0x1418F8369…90
            Dim r = Vector128.BitwiseAnd(Vector128.Multiply(newton, dir), esPositivo)   ' 0x1418F8393/96
            r = Vector128.BitwiseOr(r, Vector128.AndNot(dir, esPositivo))       ' 0x1418F8399/9C
            a.Direccion = r
            a.TiempoActual = 0.0F                                              ' 0x1418F83A3
            a.VelMax = 5.0F                                                    ' 0x1418F83AA
            a.VelMin = -1.0F                                                   ' 0x1418F83B1
            a.Frecuencia = 0.5F                                                ' 0x1418F83B8
            a.ArrastreMaximo = 3.0F                                            ' 0x1418F83BF
            Dim s = 0.5F * (a.VelMax - a.VelMin)                               ' 0x1418F86C4…E9
            s += a.VelMin                                                      ' 0x1418F86ED
            a.VelocidadDelAire = Vector128.Multiply(Vector128.Create(s), a.Direccion)   ' 0x1418F86F9
            a.Encendida = False                                                ' 0x1418C3F75
            Return a
        End Function

        ''' <summary>Un `hclSimpleWindAction` del archivo: los campos serializados tal cual.</summary>
        Friend Shared Function DelArchivo(o As HkObj_HclSimpleWindAction) As AccionDeViento
            Dim a As New AccionDeViento(False)
            a.Direccion = Fachada.V4(o.WindDirection)
            a.VelMin = o.WindMinSpeed
            a.VelMax = o.WindMaxSpeed
            a.Frecuencia = o.WindFrequency
            a.ArrastreMaximo = o.MaximumDrag
            a.VelocidadDelAire = Fachada.V4(o.AirVelocity)
            a.TiempoActual = o.CurrentTime
            Return a
        End Function

        ''' <summary>`vtbl[+0x30]`: Bethesda `0x1418C41F0` devuelve `[+0x58]`; la simple
        ''' `0x141A05FE0` devuelve 1.</summary>
        Friend Function EstaActiva() As Boolean
            Return If(DeBethesda, Encendida, True)
        End Function

        ''' <summary>
        ''' `update` de Bethesda — `0x1418C3FA0`.
        ''' <para>```
        ''' currentTime = dt + currentTime              ' 0x1418C3FAD/B2
        ''' si no encendida: return                     ' 0x1418C3FB7
        ''' s = curva(currentTime · freq)               ' 0x1418C3FB9 / 0x1418C3FC1 ([+0x50] = 0x1418C4090)
        ''' s = (s + 1) · 0,5                           ' 0x1418C3FC4 / 0x1418C3FDA
        ''' v = (max − min) · s + min                   ' 0x1418C3FD6…EA
        ''' airVelocity = dirección · v                 ' 0x1418C3FF5
        ''' ```</para>
        ''' </summary>
        Friend Sub Actualizar(dt As Single)
            TiempoActual = dt + TiempoActual
            If Not Encendida Then Return
            Dim s = Curva(TiempoActual * Frecuencia)
            s = (s + 1.0F) * 0.5F
            Dim v = ((VelMax - VelMin) * s) + VelMin
            VelocidadDelAire = Vector128.Multiply(Direccion, Vector128.Create(v))
        End Sub

        ''' <summary>
        ''' La curva de ráfaga — `0x1418C4090`: `3·sinf(π·sin t) − (c + c)`, `c = cosf(π·cos t)`.
        ''' <para>`0x1422BF150` es el `sincosf` del propio `.exe` (transcrito en `SenoDelMotor`);
        ''' `0x1422C473E`/`0x1422C4744` son `cosf`/`sinf` importados del CRT (`api-ms-win-crt-math`), las
        ''' mismas funciones que llama `MathF.Cos`/`Sin` en Windows.</para>
        ''' </summary>
        Friend Shared Function Curva(t As Single) As Single
            Dim sn As Single, cs As Single
            SenoDelMotor.SinCos(t, sn, cs)                                     ' 0x1418C40A9
            Dim c = MathF.Cos(cs * 3.14159274F)                                ' 0x1418C40B6 / 0x1418C40BE
            Dim s = MathF.Sin(sn * 3.14159274F)                                ' 0x1418C40C3 / 0x1418C40D3
            Return (s * 3.0F) - (c + c)                                        ' 0x1418C40D8…EA
        End Function

        ''' <summary>
        ''' `applyAction` — `0x1418F8420(action, simCloth, dtSub, fuerzas)`.
        ''' <para>```
        ''' si totalMass == 0 (o NaN): return                     ' 0x1418F8433 ucomiss / je
        ''' k = 1 / totalMass ; m = −1 / dtSub                     ' 0x1418F8450 / 0x1418F8473
        ''' con normales (count = numParticles):                  ' 0x1418F8454
        '''   w = |(n.y·d.y + n.x·d.x) + n.z·d.z| · drag · (k·mass)
        ''' sin normales:
        '''   w = (k·mass) · (drag · 0,7)                         ' 0x1418F8509 (0x142929450)
        ''' F += ((pos − prev) · m + airVelocity) · w              ' 0x1418F84B6…EA / 0x1418F8520…59
        ''' ```</para>
        ''' </summary>
        Friend Sub Aplicar(inst As Instancia, masaTotal As Single, dtSub As Single, fuerzas As Single())
            If masaTotal = 0.0F OrElse Single.IsNaN(masaTotal) Then Return
            Dim k = 1.0F / masaTotal                                           ' 0x1418F8450
            Dim m = Vector128.Create(-1.0F / dtSub)                            ' 0x1418F8473 / 0x1418F8477
            Dim n = inst.NumParticulas
            If inst.Normales IsNot Nothing Then
                For i = 0 To n - 1
                    Dim p = Vector128.Multiply(Simd.Leer(inst.Normales, i), Direccion)   ' 0x1418F8488
                    Dim d = (p.GetElement(1) + p.GetElement(0)) + p.GetElement(2)     ' 0x1418F8492…AC
                    Dim km = k * inst.Masa(i)                                  ' 0x1418F84A4
                    Dim w = MathF.Abs(d)                                       ' 0x1418F84BD / 0x1418F84C6 (bit de signo)
                    w = w * ArrastreMaximo                                     ' 0x1418F84CF
                    w = w * km                                                 ' 0x1418F84D7
                    Dim v = Vector128.Subtract(Simd.Leer(inst.Posiciones, i), Simd.Leer(inst.Previas, i))   ' 0x1418F84BA
                    v = Vector128.Multiply(v, m)                               ' 0x1418F84D4
                    v = Vector128.Add(v, VelocidadDelAire)                     ' 0x1418F84DB
                    v = Vector128.Multiply(v, Vector128.Create(w))             ' 0x1418F84E3
                    Simd.Escribir(fuerzas, i, Vector128.Add(v, Simd.Leer(fuerzas, i)))   ' 0x1418F84E6
                Next
            Else
                Dim arrastre = ArrastreMaximo * 0.699999988F                   ' 0x1418F853A / 0x1418F853F
                For i = 0 To n - 1
                    Dim w = (k * inst.Masa(i)) * arrastre                      ' 0x1418F852A / 0x1418F8546
                    Dim v = Vector128.Subtract(Simd.Leer(inst.Posiciones, i), Simd.Leer(inst.Previas, i))   ' 0x1418F8527
                    v = Vector128.Multiply(v, m)                               ' 0x1418F8543
                    v = Vector128.Add(v, VelocidadDelAire)                     ' 0x1418F854A
                    v = Vector128.Multiply(v, Vector128.Create(w))             ' 0x1418F8552
                    Simd.Escribir(fuerzas, i, Vector128.Add(v, Simd.Leer(fuerzas, i)))   ' 0x1418F8555
                Next
            End If
        End Sub

        Private Shared Function Hsum3(p As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Add(Vector128.Add(Simd.BcastY(p), Simd.BcastX(p)), Simd.BcastZ(p))
        End Function
    End Class

    ''' <summary>El pool de acciones del mundo y su reloj.</summary>
    Friend Module Viento

        ''' <summary>`[0x143D87E64] = 10` (`0x141875B18`).</summary>
        Friend Const TamanoDelPool As Integer = 10

        Private _pool As AccionDeViento()

        ''' <summary>`mgr[+0x170]`: sólo lo escribe `0x141878690` (sistema de cielo). Sin clima, falso.</summary>
        Friend Encendido As Boolean

        ''' <summary>
        ''' `0x141875B2E`-`0x141875C11`: `paso = 0,8 / 10`; la acción `i` nace con dirección
        ''' `(0,0,0,0)` (`0x142F3C550`) y se avanza `i·paso`.
        ''' </summary>
        Friend Function Pool() As AccionDeViento()
            If _pool IsNot Nothing Then Return _pool
            Dim p(TamanoDelPool - 1) As AccionDeViento
            Dim paso = 0.8F / CSng(TamanoDelPool)                              ' 0x141875B37…46
            For i = 0 To TamanoDelPool - 1
                p(i) = AccionDeViento.DeBethesdaConDireccion(Vector128(Of Single).Zero)   ' 0x141875B8B
                p(i).Actualizar(CSng(i) * paso)                                ' 0x141875BFD…C06
            Next
            _pool = p
            Return p
        End Function

        ''' <summary>`0x1418752B8`-`0x1418752F5`: con `mgr[+0x170]`, `update(dt)` a todo el pool.</summary>
        Friend Sub AvanzarPool(dt As Single)
            If Not Encendido Then Return
            For Each a In Pool()
                a?.Actualizar(dt)
            Next
        End Sub

        ''' <summary>
        ''' La acción que recibe una prenda al engancharse (`0x141878540`: `pool[rand(10)]`).
        ''' <para>⛔ El índice sale del generador global del juego (`0x14165B2B0` sobre el estado
        ''' `0x142F4A690`), que la app no tiene. Sin clima las diez son IGUALES en todo lo que
        ''' `applyAction` lee (dirección, `airVelocity`, `maximumDrag`): sólo difieren en
        ''' `currentTime`, que nada lee mientras `mgr[+0x170]` esté apagado. El gate lo verifica
        ''' (las diez dan la misma fuerza). Por eso da lo mismo cuál: se toma la 0.</para>
        ''' </summary>
        Friend Function AccionParaUnaPrenda() As AccionDeViento
            Return Pool()(0)
        End Function

        ''' <summary>
        ''' La lista activa de `prepare` — `0x14195BB87`-…: `acts.count = 0`, y de cada fuente, en
        ''' orden, las que `vtbl[+0x30]` da activas.
        ''' <list type="number">
        ''' <item>`ctx[+0x08]→+0x30/+0x38`: vacía — su único escritor, `0x1418BF2A0`, no tiene llamadores;</item>
        ''' <item>`hclClothData.actions` (`+0x68`/`+0x70`, `0x14195BC03`);</item>
        ''' <item>`hclSimClothData.actions` (`+0xE8`/`+0xF0`, `0x14195BC83`);</item>
        ''' <item>`hclClothInstance+0x50/+0x58` (la del pool);</item>
        ''' <item>`hclSimClothInstance+0x110`: vacía — su único escritor, `0x1418C71D0`, no tiene llamadores.</item>
        ''' </list>
        ''' </summary>
        Friend Function ListaActiva(deClothData As IList(Of AccionDeViento), deSimData As IList(Of AccionDeViento),
                                    deInstancia As IList(Of AccionDeViento)) As AccionDeViento()
            Dim r As New List(Of AccionDeViento)
            For Each fuente In {deClothData, deSimData, deInstancia}
                If fuente Is Nothing Then Continue For
                For Each a In fuente
                    If a IsNot Nothing AndAlso a.EstaActiva() Then r.Add(a)
                Next
            Next
            Return r.ToArray()
        End Function

        ''' <summary>Las acciones `hclSimpleWindAction` de una lista del archivo; la cuenta de las de
        ''' otra clase va a <paramref name="sinTranscribir"/>.</summary>
        Friend Function DelArchivo(acciones As IList(Of HkObj_HclAction),
                                   ByRef sinTranscribir As Integer) As List(Of AccionDeViento)
            Dim r As New List(Of AccionDeViento)
            If acciones Is Nothing Then Return r
            For Each accion In acciones
                If accion Is Nothing OrElse accion.Source Is Nothing Then Continue For
                Dim w = HkObj_HclSimpleWindAction.Leer(accion.Graph, accion.Source)
                If w Is Nothing Then
                    sinTranscribir += 1
                    Continue For
                End If
                r.Add(AccionDeViento.DelArchivo(w))
            Next
            Return r
        End Function

    End Module

End Namespace
