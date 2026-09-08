Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclAntiPinchConstraintSet` — tipo 19, `solve` en `0x1419F81A0`. RE cap. 6.6.2.
'
' Es el mecanismo que RESCATA una partícula pellizcada entre dos colisionables: la devuelve a la
' animación y la vuelve a soltar con una rampa. Sin él, una partícula atrapada entre dos cápsulas se
' queda ahí y la tela se clava.
'
' ⛔ CERO apariciones en el corpus vanilla (medido: `antiPinchConstraintSets` vacío en las 759
' prendas). Está igual porque un mod puede traerlo, y hasta hoy caía en el hueco.
'
' ARRANQUE, leído en `0x1419F81A0`-`0x1419F820C`:
'
'   real   = buffers[ buffers[ set.referenceMeshBufferIdx (+0x3C) ].slot ]   ' la doble indirección
'   estado = buscarPorId( inst[+0xC0], inst[+0xC8], idDelSet )               ' entradas de 16 B
'   ⛔ ES OTRA LISTA que la de Transition y Volume (`inst[+0xB0]`): dos arreglos distintos.
'
'   si real[+0x20] bit 0 (layout simple):  0x1419F8790
'   si no:                                 0x1419F8250
'   ⚠️ El RE decía «dos kernels según normales»; el binario discrimina por el LAYOUT SIMPLE del
'   buffer (`0x1419F8216 test byte [r10+0x20], 1`). Corrección medida.
'
' ⭐⭐ LA DIFERENCIA DE FONDO CON `hclTransitionConstraintSet`: **acá la fase y el reloj son POR
' PARTÍCULA**, no por set.
'   · `Transition`: `est.Fase` escalar (`0x141A08D85` la lee una vez y sale del kernel entero).
'   · `AntiPinch`:  `byte[ inst[+0x30] + i ]` y `float[ inst[+0x20] + i*4 ]` (`0x1419F83D3`,
'     `0x1419F83E3`) — una partícula puede estar volviendo a animación mientras la de al lado sigue
'     simulando. Tiene sentido: lo que se pellizca es UNA partícula, no la prenda.
' Por eso son dos kernels y no uno con un parámetro.
'
' FASE 1 — el rescate, leída en `0x1419F8330`-`0x1419F838B`:
'
'   lo = data.minPinchedParticleIndex (data+0x128)
'   por cada perParticleData i (4 B: particleIndex u16, referenceVertex u16):
'       p       = perParticleData[i].particleIndex
'       pellizc = byte[ simCloth[+0x1A0] + (p − lo) ]     ' ⬅ RELATIVO a `lo`, no absoluto
'       si (fase[i] == 0 ó fase[i] == 3) y pellizc != 0:
'            fase[i]     = 2                               ' fuerza el retorno a animación
'            reloj[i]    = 0                               ' reinicia el reloj
'            distancia[i] = 0x7F7FFFEE                     ' ⬅ NO es FLT_MAX
'
' ⛔ `0x7F7FFFEE` es `CasiFltMax`, el mismo valor que usan las AABB y las formas.
'
' FASE 2 — la rampa (`0x1419F83C0`…), que es la de `Transicion` con dos diferencias medidas:
'   · la fase y el reloj salen por partícula (arriba);
'   · no hay retardos por partícula ni `toSimMaxDistance` por partícula — la clase declara los tres
'     parámetros como ESCALARES (`toAnimPeriod` +0x30, `toSimPeriod` +0x34, `toSimMaxDistance` +0x38).
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>
    ''' Un `hclAntiPinchConstraintSet` compilado.
    ''' <para>⛔ Su estado por instancia vive en OTRA lista que la de `Transition` y `Volume`
    ''' (`inst[+0xC0]` contra `inst[+0xB0]`), y dentro de ese estado la fase y el reloj son POR
    ''' PARTÍCULA.</para>
    ''' </summary>
    Friend NotInheritable Class AntiPellizco
        Inherits SetCompilado

        Private ReadOnly _particula As Integer(), _refVertice As Integer()
        Private ReadOnly _periodoAAnim As Single, _periodoASim As Single
        Private ReadOnly _distMaxASim As Single
        Private ReadOnly _bufferIdx As Integer
        Private ReadOnly _n As Integer

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>⛔ NO corta con `k <= 0`: como `Transicion`, tiene ramas que corren igual y el
        ''' corte lo hace el propio kernel según la fase (`0x141A08DAD` en el gemelo).</summary>
        Protected Overrides ReadOnly Property CortaConKNoPositivo As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>Sin archivo: para el A/B de GAP.</summary>
        Friend Sub New(particulas As Integer(), refVertices As Integer(),
                       periodoAAnim As Single, periodoASim As Single, distMaxASim As Single,
                       bufferIdx As Integer)
            MyBase.New(19, "prueba")
            _n = particulas.Length
            _particula = particulas
            _refVertice = refVertices
            _periodoAAnim = periodoAAnim
            _periodoASim = periodoASim
            _distMaxASim = distMaxASim
            _bufferIdx = bufferIdx
        End Sub

        Friend Sub New(src As HkObj_HclAntiPinchConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim pp = src.PerParticleData
            _n = If(pp Is Nothing, 0, pp.Count)
            Dim m = Math.Max(1, _n)
            ReDim _particula(m - 1) : ReDim _refVertice(m - 1)
            For i = 0 To _n - 1
                _particula(i) = pp(i).ParticleIndex
                _refVertice(i) = pp(i).ReferenceVertex
            Next
            _periodoAAnim = src.ToAnimPeriod
            _periodoASim = src.ToSimPeriod
            _distMaxASim = src.ToSimMaxDistance
            _bufferIdx = CInt(src.ReferenceMeshBufferIdx)
        End Sub

        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            ' el bloque de estado, en la lista PROPIA de AntiPinch (`0x1419F81E3`)
            Dim est = inst.EstadoDeAntiPellizco(ctx.IndiceDelSet)
            If est Is Nothing Then
                est = New EstadoPorParticula(ctx.IndiceDelSet, Math.Max(1, _n))
                inst.EstadosDeAntiPellizco.Add(est)
            End If

            ' ⛔ el buffer de referencia, con la doble indirección del motor (`0x1419F81BE`)
            Dim buf = Buffers.Real(ctx.Buffers, _bufferIdx)

            ' ---- FASE 1: el rescate (`0x1419F8330`) ----
            Dim lo = inst.MinimoDePellizco
            Dim contacto = inst.HayContacto
            For i = 0 To _n - 1
                Dim rel = _particula(i) - lo
                Dim pellizcada = contacto IsNot Nothing AndAlso rel >= 0 AndAlso
                                 rel < contacto.Length AndAlso contacto(rel) <> 0
                If Not pellizcada Then Continue For
                ' `0x1419F834F`-`0x1419F835F`: sólo desde 0 (simulando) o 3 (asentada)
                If est.Fase(i) <> 0 AndAlso est.Fase(i) <> 3 Then Continue For
                est.Fase(i) = 2                                  ' 0x1419F8365
                est.Reloj(i) = 0.0F                              ' 0x1419F836E
                est.Distancia(i) = Simd.CasiFltMax             ' 0x1419F8376: 0x7F7FFFEE
            Next

            ' ---- FASE 2: la rampa (`0x1419F83C0`) ----
            Dim m = buf.AEspacioDeSimulacion
            Dim pos = inst.Posiciones
            Dim prev = inst.Previas
            Dim kEsc = Simd.Lane0(k)

            For i = 0 To _n - 1
                Dim fase = est.Fase(i)
                If fase = 0 Then Continue For                    ' 0x1419F83D7/D9
                If fase <> 1 AndAlso fase <> 2 AndAlso fase <> 3 Then Continue For   ' 0x1419F8401

                Dim ci = _particula(i)
                Dim q = Operadores.TransformarPunto(buf.Vertice(_refVertice(i)), m)

                If fase = 1 Then
                    Simd.Escribir(pos, ci, q)
                    Continue For
                End If

                Dim p = Simd.Leer(pos, ci)
                Dim vel = If(ctx.UsaK, Vector128.Subtract(p, Simd.Leer(prev, ci)),
                             Vector128(Of Single).Zero)
                Dim d = Vector128.Subtract(p, q)
                Dim len2 = Simd.Dot3(d, d)
                Dim largo = Simd.Lane0(Vector128.Multiply(len2, Simd.RsqrtNewtonConGuarda(len2)))

                Dim f As Single
                If fase = 2 Then
                    ' ⛔ SIN RETARDO: la clase no declara retardos por partícula, sólo los tres
                    ' períodos escalares. El reloj es el de ESTA partícula.
                    Dim t = est.Reloj(i)
                    If Not (t > 0.0F) Then
                        est.Distancia(i) = largo
                        Continue For
                    End If
                    If Not (t < _periodoAAnim) Then
                        Simd.Escribir(pos, ci, q)                ' llegó
                        Continue For
                    End If
                    f = (1.0F - t / _periodoAAnim) * est.Distancia(i)
                    f = f / kEsc
                Else
                    Dim t = est.Reloj(i)
                    If Not (t > 0.0F) Then
                        Simd.Escribir(pos, ci, q)                ' clavada
                        Continue For
                    End If
                    ' ⛔ `Continue For`, no `Exit For`: la fase es POR PARTÍCULA, así que una que ya
                    ' terminó su rampa no corta el bucle de las demás. En `Transicion`, donde la fase
                    ' es del SET entero, el motor sí sale (`0x141A08ECF jae`).
                    If t >= _periodoASim Then Continue For
                    f = (t / _periodoASim) * _distMaxASim
                    f = f / kEsc
                End If

                If Not (largo > f) Then Continue For
                Dim g = (f - largo) / largo
                Dim pNuevo = Vector128.Add(p, Vector128.Multiply(Vector128.Create(g), d))
                Simd.Escribir(pos, ci, pNuevo)
                If ctx.UsaK Then
                    ' repone la velocidad para no inyectar energía, como el gemelo
                    Simd.Escribir(prev, ci, Vector128.Subtract(pNuevo, vel))
                End If
            Next
        End Sub

    End Class

End Namespace

#End If
