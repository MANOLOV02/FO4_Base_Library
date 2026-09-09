Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' EL PELLIZCO — las dos piezas que corren dentro de `TtCollideAndSolve` (`0x141A69730`).
'
' ⛔⛔ POR QUÉ APARECIÓ ESTO. Salió del CENSO DE CAMPOS: `perParticlePinchDetectionEnabledFlags`,
' `collidablePinchingDatas`, `maxPinchedParticleIndex`, `maxCollisionPairs` y
' `info.pinchDetectionEnabled` figuraban en la reflexión y NADIE los leía en el motor. El motor
' tenía el RESCATE (`hclAntiPinchConstraintSet`, `0x1419F81A0`) y no tenía la DETECCIÓN, así que la
' bandera que el rescate lee estaba siempre en cero y el rescate no podía dispararse nunca.
'
' LA PUERTA, leída en `0x141A697BF`-`0x141A6980B`. Son TRES comprobaciones:
'
'     info.pinchDetectionEnabled (+0x1C) != 0                          ' 0x141A697BF
'     inst.numColisionables (+0x1B8) > 0                               ' 0x141A697D4
'     #{ i : buf[i][+0x80] != 0 } > 1                                  ' 0x141A697F0-0x141A6980B
'
' ⛔ La tercera cuenta sobre el BUFFER DE TRABAJO (`+0x1C0`, paso `0x90`), que es el que trae los
' campos de pellizco reescritos desde `collidablePinchingDatas`. Y exige **más de uno**: con un
' solo colisionable que opte no hay entre qué pellizcar. Declarar `pinch` en `simulationInfo` no
' alcanza.
'
' CON LA PUERTA ABIERTA, el orden es el de las etiquetas de perfilado del binario:
'
'     TtzeroCachedContacts                 0x141A75F40
'     TtiterateCollidables pinch-enabled   0x141A698CA  SolveContacts(conPellizco, sin sinPellizco)
'     Ttsolve contacts                     0x141A75E00
'     (sin etiqueta)                       0x141A69982  SolveContacts(sin conPellizco, sinPellizco)
'     los `antiPinchConstraintSets`        0x141A699D2  con k = 1,0 y usaK = False
'
' CON LA PUERTA CERRADA — la ruta de FO4 — es una sola llamada con los DOS booleanos en `True`
' (`0x141A699EE`: `mov r8b, 1` + `movzx edx, r8b`).
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    Friend Module Pellizco

        ''' <summary>
        ''' Cuántas partículas cubre el rango de pellizco — `max − min + 1`, **en 16 bits**.
        ''' <para>⛔ La resta y el `+1` son `word` (`0x141A75F5A movzx eax, word ptr [rdx+0x12A]`,
        ''' `0x141A75F61 sub ax, word ptr [rdx+0x128]`, `0x141A75F68 inc ax`, `0x141A75F6B movzx
        ''' eax, ax`). Con `max &lt; min` NO da negativo: da la vuelta. Se transcribe así porque el
        ''' tamaño que se reserva sale de esta cuenta, no de una intención.</para>
        ''' </summary>
        Friend Function CuantasEnRango(inst As Instancia) As Integer
            Return (inst.MaximoDePellizco - inst.MinimoDePellizco + 1) And &HFFFF
        End Function

        ''' <summary>
        ''' ⭐⭐ LA DETECCION — `0x141A6B0A5`-`0x141A6B172`, dentro del kernel de contacto.
        ''' <para>Es la pieza que faltaba: el motor tenia el RESCATE
        ''' (<see cref="AntiPellizco"/>) y la RESOLUCION
        ''' (<see cref="ResolverContactosDePellizco"/>), pero nadie escribia la bandera que el
        ''' rescate lee — asi que el rescate no podia dispararse NUNCA.</para>
        ''' <para>La ley: cada colisionable deja su PRIORIDAD en la particula. Si al llegar ya
        ''' habia otra, la particula esta entre DOS colisionables — eso es el pellizco. El array
        ''' arranca en `-1` (`0xFF`) y por eso el centinela es el BIT DE SIGNO:</para>
        ''' <para><code>
        ''' prioAnt        = prioridad[c]                          ' 0x141A6B0D1
        ''' pellizcada[c]  = (bit 7 de prioAnt en 0)               ' 0x141A6B0DE/E0/F2
        ''' si prio &gt; prioAnt (CON SIGNO):                        ' 0x141A6B0FA/FD `jle`
        '''     prioridad[c] = prio                                ' 0x141A6B109
        '''     m = (distancia + pinchDetectionRadius) &lt; 0        ' 0x141A6B119/1C
        '''     si m: contacto[c] = {punto, normal, velocidad}     ' 0x141A6B127-59
        '''     hayContacto[c] = m                                 ' 0x141A6B15D-65
        ''' si no y prio = prioAnt: hayContacto[c] = 0             ' 0x141A6B16D
        ''' </code></para>
        ''' <para>⛔ EL RADIO ES EL DE PELLIZCO, no el de la particula: la deteccion mira mas
        ''' lejos que el contacto (`0x141A6B0D6` lee `[rbx+0x84]`).</para>
        ''' <para>⛔ La velocidad que se guarda es la del CUERPO en el punto, SIN multiplicar por
        ''' `dtSub`: el `dtSub` lo aplica la resolucion (`0x141A75EB8`).</para>
        ''' </summary>
        Friend Sub RegistrarContacto(inst As Instancia, prioridad As Integer,
                                     radioDePellizco As Single, i As Integer,
                                     distancia As Single, punto As Vector128(Of Single),
                                     normal As Vector128(Of Single),
                                     velocidadDelCuerpo As Vector128(Of Single))
            If inst Is Nothing OrElse inst.PrioridadDeContacto Is Nothing Then Return
            If inst.HayContacto Is Nothing OrElse inst.EstaPellizcada Is Nothing Then Return
            Dim c = i - inst.MinimoDePellizco
            If c < 0 OrElse c >= inst.PrioridadDeContacto.Length Then Return
            If c >= inst.HayContacto.Length OrElse c >= inst.EstaPellizcada.Length Then Return

            ' ⛔ CON SIGNO: el centinela es `-1`, y la comparacion del motor es `jle`.
            Dim crudo = CInt(inst.PrioridadDeContacto(c))
            Dim prioAnt = If(crudo > 127, crudo - 256, crudo)          ' int8 con signo
            ' ⛔ `NOT(prioAnt) >> 7` sobre el byte: 1 si el bit de signo estaba en CERO, o sea
            ' si ALGUIEN ya la reclamo. Esa es la definicion de pellizcada. 0x141A6B0DE/E0
            inst.EstaPellizcada(c) = CByte((CInt(inst.PrioridadDeContacto(c)) Xor &HFF) >> 7)

            If prioridad > prioAnt Then                                  ' 0x141A6B0FA/FD
                inst.PrioridadDeContacto(c) = CByte(prioridad And &HFF)  ' 0x141A6B109
                ' ⛔ la mascara sale del SIGNO de `distancia + radioDePellizco` (psrad 31)
                Dim dentro = (distancia + radioDePellizco) < 0.0F        ' 0x141A6B119/1C
                If dentro AndAlso inst.ContactosCacheados IsNot Nothing Then
                    Dim b = c * 12
                    If b + 11 < inst.ContactosCacheados.Length Then
                        Simd.Escribir(inst.ContactosCacheados, c * 3, punto)          ' 0x141A6B138
                        Simd.Escribir(inst.ContactosCacheados, c * 3 + 1, normal)     ' 0x141A6B14C
                        Simd.Escribir(inst.ContactosCacheados, c * 3 + 2, velocidadDelCuerpo) ' 0x141A6B159
                    End If
                End If
                inst.HayContacto(c) = CByte(If(dentro, 1, 0))            ' 0x141A6B15D-65
            ElseIf prioridad = prioAnt Then                              ' 0x141A6B16B `jne`
                inst.HayContacto(c) = 0                                  ' 0x141A6B16D
            End If
        End Sub

        ''' <summary>
        ''' `zeroCachedContacts` — `0x141A75F40`. Deja los dos arrays de bandera en cero y los
        ''' índices de contacto en `−1`.
        ''' <para>⛔ SON DOS ARRAYS DISTINTOS, y los limpia por separado: `+0x198` («hay contacto
        ''' cacheado», `0x141A75F4C`) y `+0x1A0` («está pellizcada», `0x141A75F96`). El primero lo
        ''' escribe la colisión y lo lee <see cref="ResolverContactosDePellizco"/>; el segundo lo lee
        ''' el rescate de `hclAntiPinchConstraintSet`.</para>
        ''' <para>⛔ Limpia por BLOQUES DE 16 B, o sea `ceil(n/16)·16` bytes, no `n`
        ''' (`0x141A75F6E`-`8D`: `edx = ((n+15) &gt;&gt; 4) − 1` y `movups` mientras no sea negativo).
        ''' Y los índices son `((n+15) &gt;&gt; 2) AND 0x3FFFFFFC` `dword` a `0xFFFFFFFF`
        ''' (`0x141A75FB0`-`D0`).</para>
        ''' </summary>
        Friend Sub CerarContactosCacheados(inst As Instancia)
            If inst Is Nothing Then Return
            Dim n = CuantasEnRango(inst)

            ' 0x141A75F6E/71/78: los bloques de 16 B que se limpian. Con n = 0 da −1 y no limpia.
            Dim bloques = ((n + 15) >> 4)
            Dim bytes = bloques * 16
            If bytes > 0 Then
                If inst.HayContacto Is Nothing OrElse inst.HayContacto.Length < bytes Then
                    ReDim inst.HayContacto(bytes - 1)
                End If
                If inst.EstaPellizcada Is Nothing OrElse inst.EstaPellizcada.Length < bytes Then
                    ReDim inst.EstaPellizcada(bytes - 1)
                End If
                Array.Clear(inst.HayContacto, 0, bytes)                  ' 0x141A75F8A
                Array.Clear(inst.EstaPellizcada, 0, bytes)               ' 0x141A75FAB
            End If

            ' ⛔ El contacto cacheado son 48 B por partícula del rango (`{punto, normal, velocidad}`),
            ' y `0x141A75E5F`/`6B` lo indexa `c·3·16`.
            Dim celdas = Math.Max(0, n) * 12
            If celdas > 0 AndAlso (inst.ContactosCacheados Is Nothing OrElse
                                   inst.ContactosCacheados.Length < celdas) Then
                ReDim inst.ContactosCacheados(celdas - 1)
            End If

            ' 0x141A75FB0/B4/BB: `shr 2` + `and 0x3FFFFFFC`, y sólo si queda >= 1.
            Dim dwords = (n + 15) >> 2
            dwords = dwords And &H3FFFFFFC
            If dwords >= 1 Then
                ' ⛔ El `rep stosd` escribe DWORDS; en BYTES son cuatro veces mas.
                Dim bytesPrio = dwords * 4
                If inst.PrioridadDeContacto Is Nothing OrElse
                   inst.PrioridadDeContacto.Length < bytesPrio Then
                    ReDim inst.PrioridadDeContacto(bytesPrio - 1)
                End If
                ' ⛔ `0xFF` en cada byte = `-1` con signo: el centinela de «sin reclamar».
                For k = 0 To bytesPrio - 1
                    inst.PrioridadDeContacto(k) = &HFF                   ' 0x141A75FC8 `rep stosd`
                Next
            End If
        End Sub

        ''' <summary>
        ''' «Ttsolve contacts» — `0x141A75E00`. Aplica el contacto CACHEADO de cada partícula del
        ''' rango de pellizco que tenga bandera.
        ''' <para>Es la misma respuesta que la colisión general — proyección exacta a la superficie y
        ''' fricción sobre `Previas` — pero contra el plano guardado, no contra el shape:</para>
        ''' <para><code>
        ''' d     = ((x+y)+z) de (p − punto)·n̂                    ' 0x141A75E99-BC
        ''' pNew  = (radio − d)·n̂ + p                             ' 0x141A75EBF/C5/C8
        ''' P[i]  = pNew                                          ' 0x141A75EF9
        ''' dv    = (pNew − prev) − dtSub·velocidadDelCuerpo       ' 0x141A75ED0/D3
        ''' vt    = dv − ((x+y)+z de n̂·dv)·n̂                      ' 0x141A75ED6-0x141A75F04
        ''' Prev[i] = vt·fricción + prev                          ' 0x141A75F07/0A/0D
        ''' </code></para>
        ''' <para>⛔ EL RANGO ES INCLUSIVO Y SIN SIGNO: `0x141A75E2F cmp bx, di` + `ja` a la salida,
        ''' y el `jbe` del cierre (`0x141A75F1A`) vuelve mientras `i &lt;= max`.</para>
        ''' <para>⛔ La posición se escribe ANTES de calcular la velocidad, y la velocidad usa la
        ''' posición NUEVA (`xmm4`), no la vieja.</para>
        ''' </summary>
        Friend Sub ResolverContactosDePellizco(inst As Instancia, dtSub As Single)
            If inst Is Nothing OrElse inst.HayContacto Is Nothing Then Return
            If inst.ContactosCacheados Is Nothing Then Return
            Dim lo = inst.MinimoDePellizco
            Dim hi = inst.MaximoDePellizco
            If lo > hi Then Return                                       ' 0x141A75E2F/32
            Dim vDt = Vector128.Create(dtSub)
            For i = lo To hi                                             ' 0x141A75F16/1A
                Dim c = i - lo
                If c < 0 OrElse c >= inst.HayContacto.Length Then Continue For
                If inst.HayContacto(c) = 0 Then Continue For             ' 0x141A75E51/55
                If i < 0 OrElse i >= inst.NumParticulas Then Continue For
                Dim b = c * 12                                           ' 48 B = 12 Single
                If b + 11 >= inst.ContactosCacheados.Length Then Continue For

                Dim punto = Vector128.Create(inst.ContactosCacheados(b),
                                             inst.ContactosCacheados(b + 1),
                                             inst.ContactosCacheados(b + 2),
                                             inst.ContactosCacheados(b + 3))
                Dim nrm = Vector128.Create(inst.ContactosCacheados(b + 4),
                                           inst.ContactosCacheados(b + 5),
                                           inst.ContactosCacheados(b + 6),
                                           inst.ContactosCacheados(b + 7))
                Dim vel = Vector128.Create(inst.ContactosCacheados(b + 8),
                                           inst.ContactosCacheados(b + 9),
                                           inst.ContactosCacheados(b + 10),
                                           inst.ContactosCacheados(b + 11))

                Dim p = inst.Pos(i)
                Dim prev = inst.Prev(i)
                Dim radio = Vector128.Create(inst.Radio(i))              ' particleData +8
                Dim fric = Vector128.Create(inst.Friccion(i))            ' particleData +0xC

                Dim d = Simd.Dot3(Vector128.Subtract(p, punto), nrm)     ' 0x141A75E86-BC
                Dim pNueva = Vector128.Add(
                    Vector128.Multiply(Vector128.Subtract(radio, d), nrm), p)   ' 0x141A75EBF/C5/C8
                inst.SetPos(i, pNueva)                                   ' 0x141A75EF9

                Dim dv = Vector128.Subtract(Vector128.Subtract(pNueva, prev),
                                            Vector128.Multiply(vDt, vel))       ' 0x141A75ED0/D3
                Dim vn = Simd.Dot3(nrm, dv)                              ' 0x141A75ED6-0x141A75EFE
                Dim vt = Vector128.Subtract(dv, Vector128.Multiply(vn, nrm))    ' 0x141A75F01/04
                inst.SetPrev(i, Vector128.Add(Vector128.Multiply(vt, fric), prev)) ' 0x141A75F07/0A
            Next
        End Sub

    End Module

End Namespace

#End If
