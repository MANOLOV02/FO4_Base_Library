Option Strict On
Option Explicit On

' =================================================================================================
' EL RELOJ DE LA TELA — cuántos pasos corre la tela en un cuadro, de qué largo, y con qué sobrante.
'
' ⛔⛔ ESTO NO ES DEL SOLVER, ES DE BETHESDA, Y SIN ESTO LA ANIMACIÓN NO PUEDE SALIR BIEN.
' La app corría UN paso de 1/60 s por cada render que marcara la pose. Un clip de 30 Hz dispara 30
' renders por segundo ⇒ la tela vivía a la MITAD del tiempo real, y las anclas saltaban un cuadro de
' clip entero sin interpolar. El juego hace otra cosa, y está entera en el `.exe`:
'
'   1. el timer global mide el cuadro en MS ENTEROS y lo recorta a [8, 166] ms   (0x14165BA10)
'   2. `bhkWorld::SetDeltaTime` adapta el PASO dentro de [1/60, 1/30]              (0x141874260)
'   3. el mundo acumula lo que sobra                                              (0x1418745A0)
'   4. `InitializeClothJobs` reparte el cuadro en ≤ 6 pasos de tela de ese PASO    (0x14187526B)
'
' ⚠️ PREMISA Y HUECO DECLARADOS:
'   · la señal vale 1 sólo si el mundo corrió (`[bhkWorld+0x172] != 0` y `PASO > 0`, `0x141874510`);
'     acá se asume que el mundo de la app SIEMPRE corre;
'   · con `[bhkWorld+0x170] != 0`, `InitializeClothJobs` llama `vtbl+0x38(obj, dtConsumido)` sobre la
'     lista `0x143D87F18` y relee PASO y sobrante (`0x1418752B8`-`0x141875300`): no transcripto.
'
' Los pasos, la interpolación de la ENTRADA entre cuadros y la extrapolación de la SALIDA viven en
' `ConjuntoDeTransforms` (el `BSTransformSet`), que recibe lo que devuelve `Avanzar`.
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>Lo que `InitializeClothJobs` le carga a cada `BSTransformSet` con `0x1418A5DF0`.</summary>
    Friend Structure CuadroDeTela
        ''' <summary>`nPasosTela` (`0x143D87EA8`) ⇒ `ts[+0x18C]`.</summary>
        Friend NPasos As Integer
        ''' <summary>`PASO` (`0x143D87E8C`) ⇒ `ts[+0x1A8]`. Es también el `dt` de cada paso de la
        ''' cadena: `dtTela = PASO · (1/escala)` (`0x141875351`/`0x141875359`) con escala 1.</summary>
        Friend Paso As Single
        ''' <summary>El `dt` del cuadro que la tela consume (`xmm6`) ⇒ `ts[+0x1A4]`.</summary>
        Friend DtConsumido As Single
        ''' <summary>El sobrante ANTES de este cuadro (`xmm8`) ⇒ `ts[+0x1AC]`.</summary>
        Friend SobranteViejo As Single
        ''' <summary>El sobrante DESPUÉS (`0x143D87EA0`) ⇒ `ts[+0x1B0]`.</summary>
        Friend SobranteNuevo As Single
        ''' <summary>`s1 = max(1/escala, 1)` (`0x14187535E`/`0x141875362`).</summary>
        Friend S1 As Single
    End Structure

    ''' <summary>El estado global de tiempo del mundo de física. Uno por proceso, como en el juego.</summary>
    Friend NotInheritable Class RelojDelMundo

        Private Sub New()
        End Sub

        ''' <summary>`escalaDeTiempo` — `0x142F4BA38`, 1,0 por defecto.</summary>
        Friend Const EscalaDeTiempo As Single = 1.0F

        ''' <summary>El recorte del timer: `0x14165BA62 mov eax, 0xA6` y `0x14165BA6F mov r10d, 8`.</summary>
        Friend Const MsMinimo As Long = 8
        Friend Const MsMaximo As Long = 166

        Private Shared _pasoSinEscalar As Single = 0.0F   ' 0x143D87E90
        Private Shared _paso As Single = 0.0F             ' 0x143D87E8C
        Private Shared _acumulador As Single = 0.0F       ' 0x143D87E94
        Private Shared _senal As Boolean = False          ' 0x143D87E84
        Private Shared _sobranteTela As Single = 0.0F     ' 0x143D87EA0
        Private Shared ReadOnly _candado As New Object()

        ''' <summary>`PASO` (`0x143D87E8C`) tal como está ahora; lo relee el job al asentar (`0x141879078`).</summary>
        Friend Shared ReadOnly Property PasoActual As Single
            Get
                SyncLock _candado
                    Return _paso
                End SyncLock
            End Get
        End Property

        ''' <summary>Vuelve al estado del arranque. Lo usan los arneses entre corridas.</summary>
        Friend Shared Sub Reiniciar()
            SyncLock _candado
                _pasoSinEscalar = 0.0F : _paso = 0.0F : _acumulador = 0.0F
                _senal = False : _sobranteTela = 0.0F
            End SyncLock
        End Sub

        ''' <summary>
        ''' El `dtFrame` del timer global — `0x14165BA10`, rama sin paso fijo (`[timer+8] == 0`).
        ''' <para>`ms = ahora − antes` en ENTEROS; `ms &gt; 166 ⇒ 166` (`0x14165BA6A cmp` / `ja`),
        ''' `ms &lt; 8 ⇒ 8` (`0x14165BA78 cmp` / `cmovb`); `dt = ms · 0,001` (`0x14165BA94`,
        ''' `0x1429293F8`) `· escala` (`0x14165BABA`).</para>
        ''' </summary>
        Friend Shared Function DtDelTimer(milisegundos As Long) As Single
            Dim ms = milisegundos
            ' ⛔ `cmp rcx, rax` / `ja` es SIN SIGNO de 64 bits: un delta negativo es enorme ⇒ 166 (motor-139).
            If ms < 0 OrElse ms > MsMaximo Then
                ms = MsMaximo
            ElseIf ms < MsMinimo Then
                ms = MsMinimo
            End If
            Return CSng(ms) * 0.001F * EscalaDeTiempo
        End Function

        ''' <summary>
        ''' Un cuadro: `SetDeltaTime` + el paso del mundo + el acumulador de la tela.
        ''' </summary>
        Friend Shared Function Avanzar(dtFrame As Single) As CuadroDeTela
            SyncLock _candado
                ' ---- bhkWorld::SetDeltaTime — 0x141874260
                Dim acum = _acumulador                                         ' 0x1418742DE
                Dim total = acum + dtFrame                                     ' 0x1418742F7
                If _paso = 0.0F Then                                           ' 0x1418742F4 ucomiss / jne
                    _pasoSinEscalar = 1.0F / 30.0F                             ' 0x141874315 (0x3D088889)
                    _paso = _pasoSinEscalar                                    ' 0x14187430D → 0x1418743C1
                ElseIf _senal Then                                             ' 0x141874324
                    Dim p = _pasoSinEscalar
                    If dtFrame > (1.0F / 28.0F) * EscalaDeTiempo OrElse        ' 0x141874335/45 (0x3D124925)
                       acum > _pasoSinEscalar Then                             ' 0x14187434A
                        p = p * 1.05F                                          ' 0x141874368 (0x3F866666)
                    Else
                        p = p - (p * 0.05F) * 0.1F                             ' 0x14187434F…362
                    End If
                    _senal = False                                             ' 0x141874373
                    p = Math.Max(p, 1.0F / 60.0F)                              ' 0x14187437A maxss (0x3C888889)
                    p = Math.Min(p, 1.0F / 30.0F)                              ' 0x141874382 minss
                    _pasoSinEscalar = p                                        ' 0x14187438A
                    _paso = p * EscalaDeTiempo                                 ' 0x141874392
                Else
                    _paso = EscalaDeTiempo * _pasoSinEscalar                   ' 0x14187439C
                End If
                _acumulador = 0.0F                                             ' 0x1418743B0
                Dim nMundo = (Truncar(total / _paso))               ' 0x1418743A7…3DB

                ' ---- el paso del mundo corre y devuelve lo que sobra — 0x1418745A0…5DB, 0x141874FB8
                _acumulador = _acumulador + (total - CSng(nMundo) * _paso)
                _senal = True                                                  ' 0x1418750E7 (r14b = 1)

                ' ---- bhkWorld::InitializeClothJobs — 0x141875140
                Dim c As CuadroDeTela
                c.Paso = _paso
                If Not (_paso > 0.0F) Then Return c                            ' 0x141875261 comiss / jbe
                Dim consumido = dtFrame                                        ' 0x141875273
                Dim viejo = _sobranteTela                                      ' 0x14187528C
                Dim totalTela = viejo + consumido                              ' 0x1418752A2
                If totalTela < _paso Then                                      ' 0x1418752A6 comiss / jae
                    totalTela = _paso                                          ' 0x1418752AC
                    consumido = totalTela - viejo                              ' 0x1418752B0/B3
                End If
                Dim n = (Truncar(totalTela / _paso))                ' 0x141875309…323
                _sobranteTela = totalTela - CSng(n) * _paso                    ' 0x141875329…337
                c.NPasos = n
                c.DtConsumido = consumido
                c.SobranteViejo = viejo
                c.SobranteNuevo = _sobranteTela
                c.S1 = Math.Max(1.0F / EscalaDeTiempo, 1.0F)                   ' 0x14187534E…362
                Return c
            End SyncLock
        End Function

        ''' <summary>`cvttss2si rcx, xmm0` (64 bits) y después `cmp ecx, 6` / `cmovb` sobre los 32
        ''' bajos y SIN SIGNO (`0x141875316`…`0x141875320`, igual en `0x1418743C9`…`0x1418743D8`).
        ''' <para>Fuera de rango o NaN el `cvttss2si` de 64 bits da `0x8000000000000000`, cuyos 32
        ''' bajos son 0 ⇒ cero pasos. Devuelve el valor ANTES del `min` con 6.</para></summary>
        Private Shared Function Truncar(x As Single) As Integer
            Dim t As Long
            If Single.IsNaN(x) OrElse x >= 9.2233720368547758E+18F OrElse x < -9.2233720368547758E+18F Then
                t = Long.MinValue
            Else
                t = CLng(Math.Truncate(CDbl(x)))
            End If
            Dim bajos = CUInt(t And &HFFFFFFFFL)
            If bajos < 6UI Then Return CInt(bajos)
            Return 6
        End Function

    End Class

End Namespace
