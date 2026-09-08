Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' EL CUADRO ENTERO — `hclSimulateOperator::execute` `0x14195C350` (RE cap. 5).
'
' ⛔⛔ EL ORDEN NO ES EL INTUITIVO, y esto es lo que mas se rompe si uno lo escribe de memoria:
'
'   - la COLISION corre **antes** de integrar, sobre las posiciones del substep anterior;
'   - las ANCLAS se interpolan **entre** la colision y la integracion;
'   - `dtSub` es `(1/N)·(dt/s1)`, con el `1/N` calculado aparte (`0x14195C6E6 divss` sobre 1,0),
'     no `dt/N`;
'   - `alpha` de las anclas usa **el mismo** `1/N`, y el lerp es `(1−α)·a + α·b`.
'
' ⛔ Y el colisionable **camina** de su pose vieja a la nueva a lo largo de los substeps: el
' `setTransform` del paso 2 le deja la pose VIEJA y guarda la diferencia como velocidad.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>
    ''' Lo que el operador de simulacion necesita del cuadro y no vive en la instancia.
    ''' <para>En el motor son campos de `hclSimulateOperator` y del contexto; aca van juntos para
    ''' que la firma de <see cref="Motor.Simular"/> diga exactamente de que depende un cuadro.</para>
    ''' </summary>
    Friend Structure EntradaDelCuadro
        ''' <summary>El `dt` que llega al operador, **antes** de dividir por `s1`.</summary>
        Friend Dt As Single
        ''' <summary>`op.subSteps` — el que se usa si `info.subSteps` es 0 (`0x14195C6A8`).</summary>
        Friend SubStepsDelOperador As Integer
        ''' <summary>`info.gravity` (`simulationInfo+0x00`).</summary>
        Friend Gravedad As Vector128(Of Single)
        ''' <summary>`info.globalDampingPerSecond` (`+0x10`).</summary>
        Friend DampingPorSegundo As Single
        ''' <summary>`info.transferMotionEnabled` (`+0x1E`).</summary>
        Friend TransferenciaHabilitada As Boolean
        ''' <summary>`data.transferMotionData` (`+0x150`).</summary>
        Friend Transferencia As DatosDeTransferencia
        ''' <summary>El transform que le toca a la transferencia este cuadro.</summary>
        Friend TransformDeTransferencia As Mat4
        ''' <summary>`data.collidableTransformMap` (`+0x80`).</summary>
        Friend MapaDeColisionables As MapaDeColisionables
        ''' <summary>El `transformSet` del que salen las poses de los huesos.</summary>
        Friend TransformSet As Mat4()
        ''' <summary>`simCloth[+0x1C8]` — elige la variante de AABB del paso 5.</summary>
        Friend ModoAabb As Integer
        ''' <summary>`data.triangleIndices` (`+0x58`) y `data.triangleFlips` (`+0x68`), para las
        ''' normales. `Nothing` si `data.doNormals` esta apagado.</summary>
        Friend IndicesDeTriangulo As Integer()
        Friend FlipsDeTriangulo As Byte()
        ''' <summary>Los conjuntos de restricciones del paso 4e, en el orden del archivo.</summary>
        Friend Restricciones As SetCompilado()
        ''' <summary>Los `hclAntiPinchConstraintSet`, de `antiPinchConstraintSets` (+0xC8). ⛔ Es OTRA
        ''' lista que la de los estáticos, y el motor la recorre por separado.</summary>
        Friend AntiPellizcos As SetCompilado()
        ''' <summary>Los buffers y transform sets que el contexto de `solve` necesita.</summary>
        Friend Buffers As Buffer()
        Friend TransformSets As Mat4()()
    End Structure

    Friend Module Motor

        ''' <summary>
        ''' ⭐⭐ Un cuadro entero — `0x14195C350`.
        ''' <para>```
        ''' dt = dt / s1                                     ' 0x14195C3EF divss
        ''' (1) si info.transferMotionEnabled:  TransferMotion  ' 0x14195C401
        '''     y despues `simCloth[+0x120] = M`              ' guarda el transform previo
        ''' (2) si mapa.transformSetIndex &gt;= 0:  DriveCollidables  ' 0x14195C4B0
        ''' (3) N     = info.subSteps ? : op.subSteps         ' 0x14195C6A8/B4/B6
        '''     invN  = 1,0 / N                               ' 0x14195C6E6 divss sobre 1,0
        '''     dtSub = invN · dt                             ' 0x14195C6EF mulss
        '''     buffer de colisionables (copia) + SNAPSHOT de anclas + buffer de fuerzas
        ''' (4) por cada substep s:
        '''     (4a) SubstepColisionables(dtSub, dtSub·0,5)   ' 0x14195C9B0
        '''     (4b) COLISION                                 ' 0x14195DA70
        '''     (4c) anclas: α = (s+1)·invN, lerp (1−α)a + αb ' 0x14195CB88
        '''     (4d) acciones + integracion                   ' 0x141A12EE0
        '''     (4e) solve                                    ' 0x141A133E0 / 0x141A13650
        ''' (5) AABB (una de las dos variantes)               ' 0x1418C7300 / 0x1418C73C0
        '''     write-back de los transforms                  ' 0x14195CD28
        '''     si data.doNormals: UpdateSimNormals           ' 0x14195CEB0
        ''' ```</para>
        ''' <para>⛔⛔ **(4b) va ANTES de (4d)**: la colision corre sobre las posiciones que dejo el
        ''' substep anterior, no sobre las recien integradas. Y **(4c) va en el medio**. Poner la
        ''' integracion primero da una tela que penetra un substep entero antes de reaccionar.</para>
        ''' <para>⛔ El `dt` se divide por `s1` **una vez, al principio** (`0x14195C3EF`), y todo lo
        ''' de abajo usa el dividido.</para>
        ''' <para>⛔ Los colisionables del bucle son una **copia** (`simCloth[+0x1C0]`): el original
        ''' no se toca hasta el write-back del paso 5.</para>
        ''' </summary>
        Friend Sub Simular(inst As Instancia, colls As Colisionable(), e As EntradaDelCuadro)
            Dim dt = Simd.Lane0(Simd.DivExacta(Vector128.Create(e.Dt),
                                               Vector128.Create(inst.S1)))       ' 0x14195C3EF

            ' ---- (1) TRANSFER MOTION
            If e.TransferenciaHabilitada Then                                    ' 0x14195C401
                ' ⛔ EL PRIMER CUADRO ARRANCA CON DELTA CERO. `simCloth[+0x108]` es el `dtSub`
                ' cacheado y vale 0 sólo la primera vez; ahí el motor copia el transform ACTUAL al
                ' previo (`simCloth[+0x120..+0x160] = ts.transforms[transformIndex]`) y recién después
                ' transfiere. Con el previo en identidad, el primer cuadro ve la diferencia entre la
                ' identidad y la pose real del hueso — un salto de la distancia entera al origen del
                ' mundo — y la tela sale disparada.
                ' (a) la siembra va ANTES, no en lugar de la llamada: el primer cuadro transfiere
                ' igual, con delta CERO. Saltear la llamada tambien saltea lo que `TransferMotion`
                ' hace ademas del delta, y `GM10b` lo caza.
                If Not inst.TransferenciaSembrada Then
                    inst.TransformPrevioDeTransferMotion = e.TransformDeTransferencia ' la SIEMBRA
                    inst.TransferenciaSembrada = True
                End If
                Movimiento.TransferMotion(inst, e.Transferencia,
                                          inst.TransformPrevioDeTransferMotion,
                                          e.TransformDeTransferencia, dt)
                inst.TransformPrevioDeTransferMotion = e.TransformDeTransferencia ' simCloth[+0x120]
            End If

            ' ---- (2) DRIVE COLLIDABLES (con su propia puerta adentro)
            Colisionables.DriveCollidables(colls, e.MapaDeColisionables,
                                           e.TransformSet, dt)                   ' 0x14195C4B0

            ' ---- (3) preparacion
            Dim n = If(inst.Info IsNot Nothing AndAlso inst.Info.SubSteps > 0,
                       CInt(inst.Info.SubSteps), e.SubStepsDelOperador)          ' 0x14195C6A8/B4/B6
            If n <= 0 Then n = 1
            Dim invN = Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F),
                                                 Vector128.Create(CSng(n))))     ' 0x14195C6E6
            Dim dtSub = invN * dt                                                ' 0x14195C6EF

            Tiempo.Preparar(inst, e.Dt, n, e.DampingPorSegundo)
            Dim buffer = ClonarColisionables(colls)                      ' simCloth[+0x1C0]
            Dim snapAnclas = Anclas.Guardar(inst)                                    ' 0x14195C870
            Dim fuerzas(inst.NumParticulas * Instancia.AnchoDeParticula - 1) As Single

            Dim vDtSub = Vector128.Create(dtSub)
            Dim vMedio = Vector128.Create(dtSub * 0.5F)

            ' ---- (4) el bucle de substeps
            For s = 0 To n - 1
                Colisionables.SubstepColisionables(buffer, vDtSub, vMedio)       ' (4a) 0x14195C9B0
                Colision.ResolverContactos(inst, buffer, dtSub, False, True)     ' (4b) 0x14195DA70
                Anclas.Interpolar(inst, snapAnclas, s, invN)                         ' (4c) 0x14195CB88
                Integrador.CerarFuerzas(fuerzas)                                 ' (4d) 0x141A13080
                Integrador.Integrar(inst, e.Gravedad, fuerzas, dtSub)            '      0x141A12EE0
                Resolver(inst, e, dtSub, n, s)                                   ' (4e) 0x141A133E0
            Next

            ' ---- (5) cierre
            If e.ModoAabb = 0 Then                                               ' simCloth[+0x1C8]
                Aabb.ActualizarAabbDeParticulas(inst)                            ' 0x1418C7300
            Else
                Aabb.ActualizarAabbs(inst)                                       ' 0x1418C73C0
            End If
            Colisionables.EscribirTransforms(buffer, colls,
                                             e.MapaDeColisionables.IndiceDelSet) ' 0x14195CD28
            If inst.Normales IsNot Nothing Then                                  ' data.doNormals (+0x14C)
                Normales.ActualizarNormales(inst, e.IndicesDeTriangulo,
                                            e.FlipsDeTriangulo)                  ' 0x14195CEB0
            End If

            ' ⛔⛔ LA MARCA LA EMITE QUIEN HACE EL TRABAJO, Y SIEMPRE. Estaba en la fachada, donde los
            ' sets se COMPILAN: una mutación que los tiraba justo después (`e.Restricciones = Nothing`)
            ' dejaba la marca completa y el gate en verde con el solver corriendo sin una sola
            ' restricción. Acá el número es el del último substep resuelto de verdad.
            If Logger.Enabled Then
                ' `declarados` es lo que trae el archivo; `compilados` los que la fachada supo
                ' transcribir (la diferencia son las clases que faltan, y se ven en `[MOTOR-SETS]`);
                ' `aplicados` los que el solver corrió de verdad en el último substep.
                Dim nq = If(e.Restricciones Is Nothing, 0, e.Restricciones.Length)
                Dim cq = 0
                If e.Restricciones IsNot Nothing Then
                    For iq = 0 To e.Restricciones.Length - 1
                        If e.Restricciones(iq) IsNot Nothing Then cq += 1
                    Next
                End If
                Dim vq = _setsAplicados
                Logger.LogLazy(Function() $"[MOTOR-SETSOK] declarados={nq} compilados={cq} aplicados={vq}")
            End If
        End Sub

        ''' <summary>Cuántos conjuntos aplicó el último <see cref="Resolver"/>. Lo publica
        ''' <see cref="Simular"/> y lo lee el arnés: es la cuenta de quien HACE el trabajo, no la de
        ''' quien lo prepara.
        ''' <para>⛔ `ThreadStatic` porque el pipeline simula un bloque por hilo (`_state` es un
        ''' `ConcurrentDictionary` por esa razón): un contador compartido daría números de otra
        ''' prenda, que es peor que no tener contador.</para></summary>
        <ThreadStatic>
        Private _setsAplicados As Integer

        ''' <summary>
        ''' El paso 4e — `0x141A133E0` (el camino sin `constraintExecution`).
        ''' <para>El `k` de cada conjunto sale de <see cref="Tiempo.FactorDeRigidez"/>, y el
        ''' `usaK` de <see cref="Tiempo.UsaK"/> (`0x141A133E0`).</para>
        ''' </summary>
        Private Sub Resolver(inst As Instancia, e As EntradaDelCuadro, dtSub As Single,
                             n As Integer, s As Integer)
            _setsAplicados = 0
            If e.Restricciones Is Nothing Then Return
            Dim ctx As ContextoDeSolve
            ctx.Instancia = inst
            ctx.Buffers = e.Buffers
            ctx.TransformSets = e.TransformSets
            ctx.IndiceDelSet = 0
            ctx.UsaK = Tiempo.UsaK(inst.Modo, n, inst.S1, inst.S2)               ' 0x141A133E0
            ' ⛔ EL ÍNDICE DEL SET ES SU POSICIÓN EN LA LISTA. Es el 4.º argumento de `solve`
            ' (`0x141A1355A mov r9d, edi`), y con él los sets con estado buscan su bloque en la
            ' instancia. Dejarlo en 0 haría que todos compartieran el mismo bloque.
            For i = 0 To e.Restricciones.Length - 1
                Dim cs = e.Restricciones(i)
                If cs Is Nothing Then Continue For
                ctx.IndiceDelSet = i
                cs.Aplicar(ctx, Tiempo.FactorDeRigidez(cs.Tipo, inst.Modo, s, n, inst.S1, inst.S2))
                _setsAplicados += 1
            Next

            ' ---- y los ANTI-PELLIZCO, que son otra lista (`antiPinchConstraintSets`, +0xC8) ----
            If e.AntiPellizcos Is Nothing Then Return
            For i = 0 To e.AntiPellizcos.Length - 1
                Dim cs = e.AntiPellizcos(i)
                If cs Is Nothing Then Continue For
                ctx.IndiceDelSet = i
                cs.Aplicar(ctx, Tiempo.FactorDeRigidez(cs.Tipo, inst.Modo, s, n, inst.S1, inst.S2))
                _setsAplicados += 1
            Next
        End Sub

        ''' <summary>
        ''' El buffer de trabajo de colisionables — `0x90` B por entrada, `simCloth[+0x1C0]`.
        ''' <para>⛔ Es una **copia**: el bucle de substeps camina sobre ella y el original recien
        ''' se toca en el write-back del paso 5. Sin la copia, un colisionable compartido entre dos
        ''' prendas se integraria dos veces.</para>
        ''' </summary>
        Friend Function ClonarColisionables(origen As Colisionable()) As Colisionable()
            If origen Is Nothing Then Return Nothing
            Dim r(origen.Length - 1) As Colisionable
            For i = 0 To origen.Length - 1
                Dim c = origen(i)
                If c Is Nothing Then Continue For
                r(i) = New Colisionable() With {
                    .Transform = c.Transform,
                    .VelLineal = c.VelLineal,
                    .VelAngular = c.VelAngular,
                    .PellizcoActivo = c.PellizcoActivo,
                    .Forma = c.Forma}
            Next
            Return r
        End Function

    End Module

End Namespace

#End If
