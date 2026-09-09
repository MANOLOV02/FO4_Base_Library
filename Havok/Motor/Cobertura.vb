Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' LO QUE EL MOTOR PUBLICA SOBRE EL DATO QUE INGIRIÓ — la cobertura, medida y dicha.
'
' ⛔⛔ NO SON LEYES DE LA FÍSICA: son leyes del DATO, y por eso valen. Dicen si lo que el archivo
' declara llegó entero al motor:
'
'   · toda ancla (`hclSimClothData.fixedParticles`) tiene entrada en el puente partícula↔vértice
'     que `hclMoveParticlesOperator.vertexParticlePairs` define — sin ella el ancla queda clavada
'     donde estaba y la prenda cuelga de un punto que no se mueve;
'   · el skin cubre el buffer entero, y ningún enlace mide contra un vértice que nadie escribe;
'   · los `restLength` de los enlaces son EXACTAMENTE las distancias del `DefaultClothPose`
'     (`simClothPoses[0]`), que es de donde el autor los sacó.
'
' Cada una es un número con valor esperado conocido — cero huecos, cobertura completa, diferencia
' cero — así que no hay umbral que inventar.
'
' ⛔ Se publican DESDE EL MOTOR, no desde el arnés: el arnés no puede ver lo que el motor ingirió,
' sólo lo que el motor dice. Y se publican SIEMPRE que el log esté prendido, no sólo cuando algo
' está mal: una marca que aparece únicamente en el caso malo deja «no se midió» indistinguible de
' «salió bien», y ese es el agujero por el que esta sesión vio tres corridas VERDES con el motor
' entero desconectado.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    Friend Module Cobertura

        ''' <summary>
        ''' Publica la cobertura del dato de una prenda recién armada. Barata y una sola vez: se
        ''' llama al crear la <see cref="PrendaSimulada"/>, no por cuadro.
        ''' </summary>
        Friend Sub Publicar(prenda As PrendaSimulada)
            If prenda Is Nothing OrElse Not Logger.Enabled Then Exit Sub
            LaCadena(prenda)
            Anclas_(prenda)
            SkinCubreElBuffer(prenda)
            RestLengths(prenda)
            Volumen(prenda)
        End Sub

        ''' <summary>
        ''' ⭐ EL CONTROL DEL INSTRUMENTO — los mismos enlaces, medidos sobre la malla SKINNEADA.
        ''' <para>Una transformacion RIGIDA no puede cambiar una distancia: en reposo y bajo giro
        ''' rigido la diferencia media contra el `restLength` tiene que ser CERO. Si no lo es, lo que
        ''' este midiendo el arnes no es la fisica — es el skinning.</para>
        ''' <para>⛔ Se mide sobre el buffer del SKIN, no sobre las particulas: el simulate toca las
        ''' particulas (el buffer de tipo 1) y el del skin queda con la piel pura.</para>
        ''' <para>Se llama por CUADRO, no al armar: depende de la pose.</para>
        ''' </summary>
        Friend Sub ControlDeLaPiel(prenda As PrendaSimulada)
            If prenda Is Nothing OrElse Not Logger.Enabled Then Exit Sub
            Dim skin As OpPiel = Nothing
            For Each o In prenda.Operadores
                skin = TryCast(o, OpPiel)
                If skin IsNot Nothing Then Exit For
            Next
            If skin Is Nothing Then Exit Sub
            Dim idx = skin.Piel.BufferDeSalida
            If prenda.Buffers Is Nothing OrElse idx < 0 OrElse idx >= prenda.Buffers.Length Then Exit Sub
            Dim buf = prenda.Buffers(idx)
            If buf Is Nothing OrElse buf.Cuenta = 0 Then Exit Sub

            ' ⛔ EL BUFFER SE INDEXA POR VERTICE Y LOS ENLACES POR PARTICULA. El puente
            ' `vertexParticlePairs` existe exactamente por eso: sin el, `buf.Vertice(particula)` lee
            ' otro punto y la medicion no habla del enlace (media 196 %, que era ruido puro).
            Dim vertDe As New Dictionary(Of Integer, Integer)()
            For Each o2 In prenda.Operadores
                Dim om2 = TryCast(o2, OpMoverParticulas)
                If om2 Is Nothing OrElse om2.Pares Is Nothing Then Continue For
                For Each pr In om2.Pares
                    If pr IsNot Nothing AndAlso pr.Length > 1 Then vertDe(pr(1)) = pr(0)
                Next
            Next
            If vertDe.Count = 0 Then
                Dim opsq = prenda.Operadores.Length, movq = 0
                For Each oz In prenda.Operadores
                    If TypeOf oz Is OpMoverParticulas Then movq += 1
                Next
                Dim mq2 = movq
                Logger.LogLazy(Function() $"[MOTOR-CONTROLNA] la cadena no trae puente vertice-particula (operadores={opsq} moveParticles={mq2}): no hay control que medir")
                Exit Sub
            End If

            Dim n = 0, peor = 0.0R, suma = 0.0R
            For Each cs In prenda.Restricciones
                Dim e = TryCast(cs, EnlaceEstandar)
                If e Is Nothing Then Continue For
                For k = 0 To e.Cuenta - 1
                    Dim va2 = 0, vb2 = 0
                    If Not vertDe.TryGetValue(e.ParticulaA(k), va2) Then Continue For
                    If Not vertDe.TryGetValue(e.ParticulaB(k), vb2) Then Continue For
                    If va2 < 0 OrElse vb2 < 0 OrElse va2 >= buf.Cuenta OrElse vb2 >= buf.Cuenta Then Continue For
                    Dim d = Vector128.Subtract(buf.Vertice(va2), buf.Vertice(vb2))
                    Dim dist = CDbl(Simd.SqrtExacta(Simd.Lane0(Simd.Dot3(d, d))))
                    Dim r = CDbl(e.LongitudDeReposo(k))
                    If r <= 0.0R Then Continue For
                    Dim rel = Math.Abs(dist - r) / r
                    suma += rel
                    If rel > peor Then peor = rel
                    n += 1
                Next
            Next
            If n = 0 Then
                ' ⛔ NO SALE MUDO. Un control que no midio nada es indistinguible de uno que midio
                ' bien, y esa confusion ya costo tres corridas verdes en esta sesion.
                Dim pq3 = vertDe.Count, rq = prenda.Restricciones.Length
                Logger.LogLazy(Function() $"[MOTOR-CONTROLNA] ningun enlace resolvio por el puente (puente={pq3} entradas, {rq} constraint sets)")
                Exit Sub
            End If
            Dim nq = n, pq = peor, mq = suma / n
            Logger.LogLazy(Function() $"[MOTOR-CONTROL] los MISMOS links sobre la malla SKINNEADA (sin fisica): n={nq} media={mq:P1} peor={pq:P1}")
        End Sub

        ''' <summary>
        ''' La puerta del volumen. `hclVolumeConstraintMx` es el unico hueco de transcripcion que
        ''' queda (38 sets en el corpus, medido por M17). La marca sale SIEMPRE, con el conteo, para
        ''' que la ley pueda decir NO APLICABLE en vez de NO CONCLUYENTE: una prenda sin volumen no
        ''' deja la ley sin medir, la deja sin sujeto.
        ''' </summary>
        Private Sub Volumen(prenda As PrendaSimulada)
            Dim crudos = Havok.Canon.HavokConstraintSets.Crudos(prenda.Sim,
                                                               Havok.Canon.HavokConstraintSets.Fuente.Estaticos)
            Dim n = 0
            If crudos IsNot Nothing Then
                ' ⛔⛔ POR NOMBRE DE CLASE EXACTO, NO POR SUBCADENA. Decia
                ' `ClassName.IndexOf("Volume") >= 0`, que es una regla inventada: el motor no
                ' clasifica por texto. La lista es CERRADA y sale de la reflexion — son las dos
                ' clases de volumen que la tabla declara como conjunto de restricciones — y la
                ' comparacion es `Ordinal`, igual que en `HkObj_*.Leer`.
                ' (La subcadena ademas matcheaba `hclVolumeConstraintApplyData` y sus seis
                ' hermanas de datos, que no son conjuntos.)
                For Each c In crudos
                    If c.Bloque Is Nothing OrElse c.Bloque.ClassName Is Nothing Then Continue For
                    If String.Equals(c.Bloque.ClassName,
                                     HkObj_HclVolumeConstraint.NombreDeClase, StringComparison.Ordinal) OrElse
                       String.Equals(c.Bloque.ClassName,
                                     HkObj_HclVolumeConstraintMx.NombreDeClase, StringComparison.Ordinal) Then
                        n += 1
                    End If
                Next
            End If
            Dim poses = prenda.Sim.SimClothPoses
            Dim hayPose = poses IsNot Nothing AndAlso poses.Count > 0 AndAlso poses(0) IsNot Nothing
            Dim nq = n, hq = hayPose
            Logger.LogLazy(Function() $"[MOTOR-VOLCTLOK] sets de volumen declarados={nq} · DefaultClothPose disponible={hq}")
            If n = 0 OrElse Not hayPose Then Exit Sub

            ' ⭐⭐ LA LEY, MEDIDA SOBRE EL DATO REAL: con las particulas en el `DefaultClothPose`, el
            ' volumen es un NO-OP. Su `frameVector` es la posicion de reposo relativa al centroide
            ' ponderado, asi que el objetivo de cada particula es donde ya esta. Valor esperado CERO.
            ' ⛔ Se corre sobre una COPIA de las posiciones y se reponen: esto es una medicion, no
            ' puede mover la prenda.
            Dim pos = prenda.Estado.Posiciones
            Dim copia(pos.Length - 1) As Single
            Array.Copy(pos, copia, pos.Length)
            Dim pp2 = poses(0).Positions
            For i = 0 To Math.Min(pp2.Count, prenda.Estado.NumParticulas) - 1
                Simd.Escribir(pos, i, Fachada.V4(pp2(i)))
            Next
            Dim antes(prenda.Estado.NumParticulas - 1) As Vector128(Of Single)
            For i = 0 To prenda.Estado.NumParticulas - 1
                antes(i) = Simd.Leer(pos, i)
            Next

            Dim ctx As ContextoDeSolve
            ctx.Instancia = prenda.Estado
            Dim aplicados = 0
            For i = 0 To prenda.Restricciones.Length - 1
                Dim v = TryCast(prenda.Restricciones(i), Volumen)
                If v Is Nothing Then Continue For
                ctx.IndiceDelSet = i
                v.Aplicar(ctx, 1.0F)
                aplicados += 1
            Next

            Dim peor = 0.0F
            For i = 0 To prenda.Estado.NumParticulas - 1
                Dim d = Vector128.Subtract(Simd.Leer(pos, i), antes(i))
                peor = Math.Max(peor, Simd.SqrtExacta(Simd.Lane0(Simd.Dot3(d, d))))
            Next
            Array.Copy(copia, pos, pos.Length)

            Dim aq = aplicados, pq = peor
            Logger.LogLazy(Function() $"[MOTOR-VOLNOOP] el volumen sobre el DefaultClothPose movio {pq:0.######} u ({aq} sets)")
        End Sub

        ''' <summary>
        ''' ⭐ LA CADENA QUE SE CORRE, en el orden del archivo. Es el primer dato que se mira cuando
        ''' un numero no cierra: dice que `hclClothState` se eligio y con que operadores.
        ''' </summary>
        Private Sub LaCadena(prenda As PrendaSimulada)
            Dim nombres As New List(Of String)()
            For Each o In prenda.Operadores
                nombres.Add(If(o Is Nothing, "(sin transcribir)", o.Nombre & "/" & o.Tipo.ToString()))
            Next
            Dim txt = String.Join(" -> ", nombres)
            Dim pq = prenda.Estado.NumParticulas, bq = If(prenda.Buffers Is Nothing, 0, prenda.Buffers.Length)
            Logger.LogLazy(Function() $"[MOTOR-CADENA] {pq} particulas, {bq} buffers :: {txt}")
        End Sub

        ''' <summary>
        ''' Las anclas contra el puente partícula↔vértice.
        ''' <para>`hclSimClothData.fixedParticles` (+0x50) nombra partículas; el puente lo declara
        ''' `hclMoveParticlesOperator.vertexParticlePairs`. Un ancla sin entrada en el puente no
        ''' recibe la pose de la piel: queda donde la dejó la última siembra.</para>
        ''' </summary>
        Private Sub Anclas_(prenda As PrendaSimulada)
            Dim fijas = prenda.Estado.ParticulasFijas
            Dim n = If(fijas Is Nothing, 0, fijas.Length)
            Dim conPuente = 0, sinPuente = 0, primera = -1

            Dim puente As New HashSet(Of Integer)()
            Dim hayMove = False
            For Each o In prenda.Operadores
                Dim om = TryCast(o, OpMoverParticulas)
                If om Is Nothing Then Continue For
                hayMove = True
                For Each p In om.Pares
                    If p IsNot Nothing AndAlso p.Length > 1 Then puente.Add(p(1))
                Next
            Next

            For i = 0 To n - 1
                If puente.Contains(fijas(i)) Then
                    conPuente += 1
                Else
                    sinPuente += 1
                    If primera < 0 Then primera = fijas(i)
                End If
            Next

            Dim aq = conPuente, bq = n, cq = sinPuente, dq = primera

            ' ⛔ SIN `hclMoveParticlesOperator` NO HAY PUENTE QUE EXIGIR. Las cadenas «Animate» del
            ' corpus (320 + 127 + 2 prendas) no lo traen: la malla se copia entera y no hay pares
            ' vertice-particula. Exigir el puente ahi es exigir algo que el archivo no declara — la
            ' ley NO APLICA, que no es lo mismo que fallar ni que no haberse medido.
            If Not hayMove Then
                Logger.LogLazy(Function() $"[MOTOR-ANCNA] la cadena no trae `hclMoveParticlesOperator`: las {bq} anclas no tienen puente que exigir")
                Exit Sub
            End If

            Logger.LogLazy(Function() $"[MOTOR-ANCOK] anclas con puente particula-vertice: {aq} de {bq} · sin puente={cq}")
            If sinPuente > 0 Then
                Logger.LogLazy(Function() $"[MOTOR-ANCSINMAPA] ⛔ {cq} de {bq} anclas sin entrada en el puente (la primera: particula {dq}) ⇒ quedan clavadas donde estaban")
            End If
        End Sub

        ''' <summary>
        ''' El skin contra el buffer al que escribe.
        ''' <para>Un vértice del buffer que ningún bloque del deformer nombra no lo escribe nadie: se
        ''' queda con lo que hubiera. Si un enlace mide contra él, mide contra basura.</para>
        ''' </summary>
        Private Sub SkinCubreElBuffer(prenda As PrendaSimulada)
            For Each o In prenda.Operadores
                Dim op = TryCast(o, OpPiel)
                If op Is Nothing Then Continue For
                Dim p = op.Piel
                Dim destino = If(p.BufferDeSalida >= 0 AndAlso p.BufferDeSalida < prenda.Buffers.Length,
                                 prenda.Buffers(p.BufferDeSalida), Nothing)
                Dim total = If(destino Is Nothing, -1, destino.Cuenta)
                Dim cubiertos As New HashSet(Of Integer)()
                For i = 0 To p.Cuenta - 1
                    cubiertos.Add(p.Vertice(i))
                Next
                Dim aq = cubiertos.Count, bq = total, cq = Math.Max(0, total - cubiertos.Count)
                Dim nq = op.Nombre
                Logger.LogLazy(Function() $"[MOTOR-BUFCOV] '{nq}' cubre {aq} de {bq} entradas del buffer · {cq} sin cubrir")
            Next
        End Sub

        ''' <summary>
        ''' Los `restLength` de los enlaces contra el `DefaultClothPose`.
        ''' <para>`hclSimClothData.simClothPoses[0]` es la pose de la que el autor sacó las
        ''' distancias de reposo. La diferencia esperada es CERO; no hay tolerancia que elegir.</para>
        ''' </summary>
        Private Sub RestLengths(prenda As PrendaSimulada)
            Dim poses = prenda.Sim.SimClothPoses
            If poses Is Nothing OrElse poses.Count = 0 OrElse poses(0) Is Nothing OrElse
               poses(0).Positions Is Nothing Then
                Logger.LogLazy(Function() "[MOTOR-REST] la prenda no declara `simClothPoses[0]`: no hay contra que medir")
                Exit Sub
            End If
            Dim pp = poses(0).Positions
            Dim n = 0, peor = 0.0R, suma = 0.0R
            For Each cs In prenda.Restricciones
                Dim e = TryCast(cs, EnlaceEstandar)
                If e Is Nothing Then Continue For
                For k = 0 To e.Cuenta - 1
                    Dim a = e.ParticulaA(k), b = e.ParticulaB(k)
                    If a < 0 OrElse b < 0 OrElse a >= pp.Count OrElse b >= pp.Count Then Continue For
                    Dim va = Fachada.V4(pp(a)), vb = Fachada.V4(pp(b))
                    Dim d = Vector128.Subtract(va, vb)
                    Dim dist = CDbl(Simd.SqrtExacta(Simd.Lane0(Simd.Dot3(d, d))))
                    Dim r = CDbl(e.LongitudDeReposo(k))
                    If dist <= 0.0R Then Continue For
                    Dim rel = Math.Abs(dist - r) / dist
                    suma += rel
                    If rel > peor Then peor = rel
                    n += 1
                Next
            Next
            Dim nq = n, pq = peor, mq = If(n > 0, suma / n, 0.0R)
            Logger.LogLazy(Function() $"[MOTOR-REST] restLength vs DefaultClothPose sobre {nq} links: media={mq:P4} peor={pq:P4}")
        End Sub

    End Module

End Namespace

#End If
