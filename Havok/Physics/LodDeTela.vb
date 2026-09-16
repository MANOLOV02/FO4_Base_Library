Option Strict On
Option Explicit On

' =================================================================================================
' EL LOD DE LA TELA POR ACTOR — `0x140DB3A40`, transcripto.
'
' Lo llaman el job de los actores de alto proceso (`0x140DB31B0`, `0x140DB3841`: ordenados por distancia²
' a la cámara, cuenta y costo COMPARTIDOS entre todos) y los dos jobs del jugador (`0x140BD670C` /
' `0x140BD69C8`: distancia² 0, cuenta 0, costo 0). En ambos la escala del costo es `1 / [0x142F3105C]`
' = 1/6 (`0x140BD66C8`…`0x140BD6708`, `0x140DB37E2`…`0x140DB37FD` con `0x140BD4490`).
'
' Por prenda del actor decide:
'   modo 2 ⇒ `[ts+0x194] = 1` (pedir Simulate)   modo 1 ⇒ `[ts+0x194] = 0` (Animate)
'   sin mundo ⇒ `0x1418A0DA0(prenda, NULL)`: la prenda sale del mundo y no se paso.
'
' ⛔ Las ENTRADAS son el estado del actor en el juego. Lo que la app no tenga se pasa como entrada, no
' se decide acá: `EntradaDeLodDeTela` trae valores de un actor sin estado (ver `ActorSinEstado`).
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>Lo que `0x140DB3A40` lee del actor y del juego.</summary>
    Friend NotInheritable Class EntradaDeLodDeTela
        ''' <summary>`xmm2`: la distancia² a la cámara (`0x140DB34A1`…`0x140DB3519`); 0 para el jugador.</summary>
        Friend DistanciaCuadrada As Single
        ''' <summary>`xmm3`: `1 / [0x142F3105C]` = 1/6.</summary>
        Friend EscalaDeCosto As Single = 1.0F / CSng(6)

        ''' <summary>`[actor+0x300] ≠ 0` (el proceso). Sin él no hace nada (`0x140DB3A64`).</summary>
        Friend TieneProceso As Boolean = True
        ''' <summary>`vtbl[+0x460](actor) ≠ 0` (la raíz 3D, `0x140DB3AA3`) — `bl`.</summary>
        Friend TieneRaiz As Boolean = True
        ''' <summary>`0x1404CAA60([actor+0xB8]) ≠ 0` (`0x140DB3AAA`…`0x140DB3AC5`) — `[rsp+0x28]`.</summary>
        Friend TieneCeldaConMundo As Boolean = True
        ''' <summary>`[celda+0x148] ≠ 0`: el mundo de tela al que se engancha (`0x140DB3DEE`).</summary>
        Friend TieneMundoDeTela As Boolean = True
        ''' <summary>El bit 39 de `[raíz+0x108]` (`0x140DB3ADC`…`0x140DB3AE7`).</summary>
        Friend RaizBit39 As Boolean
        ''' <summary>`[[0x1430F9FD0]+0x90] == actor` (`0x140DB3AF3`).</summary>
        Friend EsElActorDe1430F9FD0 As Boolean
        ''' <summary>`0x14102B5C0(cámara, 0xC)`: `[cam+0x28] ≠ 0` y `= cameraStates[12]` (`0x140DB3B07`).</summary>
        Friend CamaraEnEstado12 As Boolean
        ''' <summary>`0x140D38820(proceso) ≠ 0` (`0x140DB3B4B`).</summary>
        Friend TieneDatosDeProceso As Boolean
        ''' <summary>`[0x140D38820(proceso)+0x60]`, u32 (`0x140DB3B55`).</summary>
        Friend ContadorDeProceso As UInteger
        ''' <summary>`[0x143E5DF98]` (`0x140DB3B5B`).</summary>
        Friend Reloj As Single
        ''' <summary>`actor == [0x1431EDE50]`, el `PlayerCharacter` (`0x140DB3B83`).</summary>
        Friend EsJugador As Boolean
        ''' <summary>`vtbl[+0x470](jugador)` = bit 4 de `[jugador+0xDFE]` (`0x140D8A8D0`; `0x140DB3BAF`).</summary>
        Friend JugadorVfunc470 As Boolean
        ''' <summary>`[[0x1430F9FD0]+0x40] == 1` (`0x140DB3BC0`).</summary>
        Friend Global40Es1 As Boolean
        ''' <summary>`0x140E59C30(...)` devolvió `al ≠ 0` (`0x140DB3BCE`).</summary>
        Friend Registro59C30 As Boolean
        ''' <summary>`[registro+4]` (`0x140DB3BDF cmp dword [rbx+4], 9`).</summary>
        Friend Registro59C30Tipo As Integer
        ''' <summary>`[actor+0x128]→vtbl[+0x120]` (`0x140DB3C6A`), el resultado entero.</summary>
        Friend EstadoSentadoODurmiendo As Integer
        ''' <summary>`[actor+0x130]`, u32: lo leen `0x140C73EA0`, `0x140C73E70`, `0x140DB3C9D`,
        ''' `0x140DB3CBF`, `0x140C73DF0` (vtbl `+0x600` de `Actor` `0x1425683B0`) y `0x140CEB320`.</summary>
        Friend Estado130 As UInteger
        ''' <summary>`[proceso+0x98]` (`0x140CF1200`, `0x140DB3CB2`).</summary>
        Friend Proceso98 As Single

        ''' <summary>
        ''' ENTRADAS de un actor del que la app no conoce estado: todos los estados en cero (ni sentado,
        ''' ni en el suelo, ni muerto, ni nadando: `Estado130 = 0`, `Proceso98 = 0`, sentado 0), con proceso,
        ''' raíz y mundo, bit 39 apagado y, si es el jugador, `vtbl[+0x470]` prendido (sin eso la rama
        ''' `0x140DB3BF4` lo deja fuera). ⛔ Son entradas elegidas por la app, no una ley del motor.
        ''' </summary>
        ''' <summary>Copia campo a campo (el llamador conserva la suya).</summary>
        Friend Function Copia() As EntradaDeLodDeTela
            Return DirectCast(MemberwiseClone(), EntradaDeLodDeTela)
        End Function

        Friend Shared Function ActorSinEstado(distanciaCuadrada As Single, esJugador As Boolean) As EntradaDeLodDeTela
            Return New EntradaDeLodDeTela With {.DistanciaCuadrada = distanciaCuadrada, .EsJugador = esJugador,
                                                .JugadorVfunc470 = esJugador}
        End Function
    End Class

    ''' <summary>La decisión para una prenda.</summary>
    Friend Structure DecisionDeLodDeTela
        ''' <summary>`esi`: 0, 1 (Animate) o 2 (Simulate).</summary>
        Friend Modo As Integer
        ''' <summary>`rdi ≠ 0`: la prenda queda en el mundo (`0x1418A0DA0(prenda, rdi)`).</summary>
        Friend EnElMundo As Boolean
        ''' <summary>Si se escribió `[ts+0x194]` (`0x1418A1B20`) y con qué.</summary>
        Friend EscribeValido As Boolean
        Friend Valido As Boolean
    End Structure

    Friend NotInheritable Class LodDeTela

        Private Sub New()
        End Sub

        ''' <summary>`0x1418A1AC0`: el costo de una prenda. `Σ max(nPasos · [inst+0x1C], 0)` sobre las
        ''' instancias (`[prenda+0x60]`, `[+0x70]`), `nPasos = [0x143D87EA8]` sin signo.</summary>
        Friend Shared Function CostoDePrenda(nPasosTela As UInteger, tiempos As Single()) As Single
            Dim s = 0.0F                                                        ' 0x1418A1AC6
            If tiempos Is Nothing Then Return s
            Dim n = CSng(nPasosTela)                                            ' 0x1418A1AE3 cvtsi2ss rcx
            For Each t In tiempos
                Dim x = n * t                                                   ' 0x1418A1AFA
                If Not (x > 0.0F) Then x = 0.0F                                 ' 0x1418A1AFF maxss xmm1, 0
                s += x                                                          ' 0x1418A1B03
            Next
            Return s
        End Function

        ''' <summary>
        ''' `0x140DB3A40`. `costos(i)` es `0x1418A1AC0` de la prenda i (`Nothing` = hueco en la lista,
        ''' `0x140DB3D86`). `cuenta`/`costo` son los acumuladores del llamador (`[rsp+0xA0]`/`[rsp+0xA8]`).
        ''' Devuelve `Nothing` si no hace nada (sin proceso o sin lista de prendas).
        ''' </summary>
        Friend Shared Function Decidir(e As EntradaDeLodDeTela, costos As Single?(), ByRef cuenta As UInteger,
                                       ByRef costo As Single, Optional ajustes As AjustesDeTela = Nothing) As DecisionDeLodDeTela()
            Dim a = If(ajustes, AjustesDeTela.Defecto)
            If Not e.TieneProceso OrElse costos Is Nothing Then Return Nothing  ' 0x140DB3A64 / 0x140DB3A7D

            ' ---- permitido — [rsp+0x24]
            Dim bl = e.TieneRaiz                                                ' 0x140DB3AD0 setne bl
            Dim permitido As Boolean
            Dim ramaDelTiempo As Boolean
            If e.TieneCeldaConMundo AndAlso bl Then                             ' 0x140DB3AD3 / 0x140DB3AD8
                If Not e.RaizBit39 Then                                         ' 0x140DB3AEA je
                    permitido = True
                ElseIf e.EsElActorDe1430F9FD0 Then                              ' 0x140DB3AFA je
                    permitido = True
                ElseIf e.CamaraEnEstado12 Then                                  ' 0x140DB3B0E
                    permitido = True
                Else
                    ramaDelTiempo = True
                End If
            Else
                ramaDelTiempo = True
            End If
            If ramaDelTiempo Then
                permitido = False                                               ' 0x140DB3B16
                If bl AndAlso e.TieneDatosDeProceso Then                        ' 0x140DB3B1F / 0x140DB3B53
                    Dim dif = e.Reloj - CSng(CLng(e.ContadorDeProceso))         ' 0x140DB3B5B…B6C (cvtsi2ss rax)
                    If a.FMaxFrameCounterDifferenceToConsiderVisible > dif Then permitido = True   ' 0x140DB3B78 comiss / cmova
                End If
            End If

            ' ---- el jugador — 0x140DB3B83…0x140DB3C42
            If e.EsJugador AndAlso permitido Then
                If e.JugadorVfunc470 Then                                       ' 0x140DB3BB7 jne
                    permitido = True
                ElseIf e.Global40Es1 AndAlso e.Registro59C30 Then               ' 0x140DB3BC4 / 0x140DB3BD5
                    permitido = (e.Registro59C30Tipo = 9)                       ' 0x140DB3BE3
                Else
                    permitido = False                                           ' 0x140DB3BF4
                End If
            End If

            ' ---- cerca — 0x140DB3C47…0x140DB3C52
            Dim cerca = a.FClothLODDistanceSqr > e.DistanciaCuadrada

            ' ---- animar a la fuerza — cl
            Dim forzado As Boolean
            If a.BAnimateClothOnSitSleep AndAlso e.EstadoSentadoODurmiendo <> 0 Then forzado = True   ' 0x140DB3C57…C72
            If Not forzado AndAlso a.BAnimateClothOnDown Then                   ' 0x140DB3C78
                Dim x = e.Estado130
                If (x And &H1E00000UI) = &HE00000UI Then                        ' 0x140C73EA0
                    forzado = True
                ElseIf (x And &H1E00000UI) <> 0UI AndAlso (x And &H1E00000UI) <> &HE00000UI Then   ' 0x140C73E70
                    forzado = True
                ElseIf (x And &H1E0000UI) = &HE0000UI Then                      ' 0x140DB3C9D…CAD
                    forzado = True
                ElseIf e.Proceso98 > 0.0F Then                                  ' 0x140DB3CB7…CBD comiss / ja
                    forzado = True
                ElseIf (x And &H1E0000UI) = &H100000UI Then                     ' 0x140DB3CBF…CCF
                    forzado = True
                End If
            End If
            If Not forzado AndAlso a.BAnimateClothOnDead AndAlso Muerto(e.Estado130) Then forzado = True   ' 0x140DB3CD1…CEA
            If Not forzado AndAlso a.BAnimateClothOnSwim AndAlso
               ((e.Estado130 And &H400UI And &H3FFFUI) = &H400UI) Then forzado = True                     ' 0x140DB3CEC…D08 (0x140CEB320)
            If Not forzado AndAlso a.BSimulateClothDisable Then forzado = True  ' 0x140DB3D0A

            ' ---- por prenda — 0x140DB3D80…0x140DB3E51
            Dim r(costos.Length - 1) As DecisionDeLodDeTela
            For i = 0 To costos.Length - 1
                If Not costos(i).HasValue Then Continue For                     ' 0x140DB3D86
                Dim modo = 0
                If permitido Then                                               ' 0x140DB3D93
                    If cerca AndAlso Not forzado AndAlso cuenta < a.UMaxClothCount AndAlso
                       (a.FClothLODDistanceSqr * 0.5F) > costo Then             ' 0x140DB3D95…DBC
                        modo = 2
                    ElseIf cuenta < a.UMaxClothCount2 AndAlso a.FClothTimingBudget > costo Then   ' 0x140DB3DC5…DDD
                        modo = 1
                    End If
                End If
                Dim enMundo = modo <> 0 AndAlso e.TieneCeldaConMundo AndAlso e.TieneMundoDeTela   ' 0x140DB3DE4…DF7
                r(i).Modo = modo
                r(i).EnElMundo = enMundo
                If enMundo Then                                                 ' 0x140DB3E08
                    r(i).EscribeValido = True
                    r(i).Valido = (modo = 2)                                    ' 0x140DB3E0A…E1E
                    costo = costos(i).Value * e.EscalaDeCosto + costo           ' 0x140DB3E2B…E34
                    cuenta += 1UI                                               ' 0x140DB3E39
                End If
            Next
            Return r
        End Function

        ''' <summary>La distancia² de la referencia a la cámara — `0x140DB3495`…`0x140DB3519`:
        ''' `d = cámara − [actor+0xD0]` por componente, `mulps`, `(x² + y²) + z²`.</summary>
        Friend Shared Function DistanciaCuadrada(camara As System.Numerics.Vector3, posicion As System.Numerics.Vector3) As Single
            Dim dx = camara.X - posicion.X                                      ' 0x140DB34A1
            Dim dy = camara.Y - posicion.Y                                      ' 0x140DB34B2
            Dim dz = camara.Z - posicion.Z                                      ' 0x140DB34CE
            Dim xx = dx * dx, yy = dy * dy, zz = dz * dz                        ' 0x140DB3504 mulps
            Return (xx + yy) + zz                                               ' 0x140DB350E / 0x140DB3519 addss
        End Function

        ''' <summary>
        ''' El orden de la lista {actor, distancia²} por distancia — el quicksort de `0x140DB3727`…`0x140DB37D5`
        ''' (nivel superior) y `0x140DBA4F0` (recursivo). Intercambia entradas de 16 B con `0x141659E60`.
        ''' Devuelve la permutación: `r(k)` = índice original que quedó en la posición k.
        ''' </summary>
        Friend Shared Function OrdenarPorDistancia(claves As Single()) As Integer()
            Dim n = claves.Length
            Dim idx = Enumerable.Range(0, n).ToArray()
            Dim k = CType(claves.Clone(), Single())
            Dim cambiar As Action(Of Integer, Integer) = Sub(a As Integer, b As Integer)   ' 0x141659E60: tmp←a, a←b, b←tmp
                              Dim ti = idx(a), tk = k(a)
                              idx(a) = idx(b) : k(a) = k(b)
                              idx(b) = ti : k(b) = tk
                          End Sub
            If n <= 1 Then Return idx                                           ' 0x140DB3735 cmp eax, 1 / jbe
            Dim hiTop = n - 1                                                   ' 0x140DB373E
            If hiTop = 0 Then Return idx                                        ' 0x140DB3742
            Dim i = 0, j = hiTop                                                ' 0x140DB3750 / 0x140DB3753
            Particionar(k, 0, i, j, cambiar)                                    ' 0x140DB3760…0x140DB38C4
            cambiar(0, j)                                                       ' 0x140DB377C…0x140DB3792
            If j <> 0 Then Recursivo(k, 0, j - 1, n, cambiar)                   ' 0x140DB3797…0x140DB37AE
            If j < n - 1 Then Recursivo(k, j + 1, hiTop, n, cambiar)            ' 0x140DB37B3…0x140DB37D0
            Return idx
        End Function

        ''' <summary>La partición común (`0x140DB3760`…`0x140DB38C4` / `0x140DBA550`…`0x140DBA60A`):
        ''' pivote = clave del primero (recargado tras cada intercambio); j baja mientras NO
        ''' `pivote ≥ clave(j)`; si sí, i sube mientras `pivote ≥ clave(i)` y, si no, intercambia i↔j.</summary>
        Private Shared Sub Particionar(k As Single(), lo As Integer, ByRef i As Integer, ByRef j As Integer,
                                       cambiar As Action(Of Integer, Integer))
            Do
                Dim pivote = k(lo)                                              ' 0x140DB3760 / 0x140DBA554 movss xmm0, [r14]
                Dim reiniciar = False
                Do
                    If pivote >= k(j) Then                                      ' 0x140DB376A / 0x140DBA569 comiss; jae
                        Do                                                      ' 0x140DB3890 / 0x140DBA5E0
                            If Not (pivote >= k(i)) Then                        ' 0x140DB3895 / 0x140DBA5E9 comiss; jb
                                cambiar(i, j)                                   ' 0x140DB38BA / 0x140DBA605
                                reiniciar = True                                ' 0x140DB38C4 / 0x140DBA60A jmp
                                Exit Do
                            End If
                            i += 1                                              ' 0x140DB389D / 0x140DBA5EF
                            If Not (i < j) Then Return                          ' 0x140DB389F / 0x140DBA5F1 jb
                        Loop
                        Exit Do
                    End If
                    j -= 1                                                      ' 0x140DB3776 / 0x140DBA56F
                    If Not (i < j) Then Return                                  ' 0x140DB3778 / 0x140DBA571 jb
                Loop
                If Not reiniciar Then Return
            Loop
        End Sub

        ''' <summary>`0x140DBA4F0(lista, tmp, lo, hi)`.</summary>
        Private Shared Sub Recursivo(k As Single(), lo As Integer, hi As Integer, cuenta As Integer,
                                     cambiar As Action(Of Integer, Integer))
            If lo >= hi Then Return                                             ' 0x140DBA4F0 cmp r8d, r9d / jae (sin signo)
            Do                                                                  ' 0x140DBA530
                Dim i = lo, j = hi                                              ' 0x140DBA534 / 0x140DBA539
                If lo < hi Then Particionar(k, lo, i, j, cambiar)               ' 0x140DBA53F
                cambiar(lo, j)                                                  ' 0x140DBA575…0x140DBA58D
                If j <> 0 Then Recursivo(k, lo, j - 1, cuenta, cambiar)         ' 0x140DBA592…0x140DBA5A3
                If Not (j < cuenta - 1) Then Return                             ' 0x140DBA5A8…0x140DBA5AF
                lo = j + 1                                                      ' 0x140DBA5B1
                If Not (lo < hi) Then Return                                    ' 0x140DBA5B4 / 0x140DBA5B7
            Loop
        End Sub

        ''' <summary>`0x140C73DF0` con `dl = 0` (vtbl `+0x600` de `Actor`, `0x140DB3CE2`).</summary>
        Private Shared Function Muerto(x As UInteger) As Boolean
            Dim m = x And &H1E0000UI
            Return m = &H40000UI OrElse m = &H20000UI OrElse m = &HE0000UI OrElse m = &HA0000UI   ' 0x140C73E2C…E5F
        End Function

    End Class

End Namespace
