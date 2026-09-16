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
        ''' <summary>`op.numberOfSolveIterations` (+0x28) — cuantas veces corre el paso 4e
        ''' entero (sets **y** colision) dentro de UN substep. `0x141A134F7`/`0x141A135B1`.</summary>
        Friend IteracionesDeSolve As Integer
        ''' <summary>`op.adaptConstraintStiffness` (+0x40). Promueve el modo 1 a 2
        ''' (`0x141A1349B`), y con el cambian `k` y `usaK`.</summary>
        Friend AdaptaRigidez As Boolean
        ''' <summary>`op.constraintExecution` (+0x30). Vacia ⇒ orden del archivo y colision al
        ''' final; no vacia ⇒ manda la lista y **`-1` es la colision** (`0x141A13798`).</summary>
        Friend EjecucionDeRestricciones As Integer()
        ''' <summary>La lista activa de acciones que arma `prepare` (`simCloth+0x168`,
        ''' `0x14195BB87`), en su orden. `Nothing` = ninguna.</summary>
        Friend Acciones As AccionDeViento()
        ''' <summary>`hclSimClothData.totalMass` (`+0x78`), que lee `applyAction` (`0x1418F842E`).</summary>
        Friend MasaTotal As Single
        ''' <summary>La geometria del mundo para `computeContactPlanes` (el paso 4b).
        ''' <para>⛔⛔ EN FO4 ES SIEMPRE `Nothing`, y eso esta MEDIDO, no supuesto: el unico
        ''' escritor de `[inst+0xF8]`/`[inst+0x100]` es `0x1418C7B10` y no tiene NI UN llamador.
        ''' Por eso la puerta `0x14195E3A0` no abre y el paso 4b es no-op. Se cablea igual porque
        ''' el paso EXISTE y su lugar en el orden es el que es.</para></summary>
        Friend MundoDeTerreno As TerrenoDelMundo
        ''' <summary>`data.numLandscapeCollidableParticles` (+0x148) — la tercera comprobacion de
        ''' la puerta (`0x14195E3DF`).</summary>
        Friend ParticulasDeTerreno As Integer
        ''' <summary>`data.landscapeCollisionData` (+0x134): radio, deteccion de pegadas y el
        ''' factor de estiramiento al cuadrado.</summary>
        Friend RadioDeTerreno As Single
        Friend DetectarPegadas As Boolean
        Friend FactorDePegado As Single
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
        ''' <para>⛔⛔ **(4b) ES EL TERRENO**, no la colision general: `computeContactPlanes`
        ''' (`0x14195DA70`) tiene UN llamador — `0x14195CB7A` — y `SolveContacts` (`0x141A71610`)
        ''' tiene TRES, los tres dentro de `TtCollideAndSolve`, que corre en el (4e). La puerta
        ''' del (4b) (`0x14195E3A0`) no abre nunca en FO4, asi que ese paso es no-op.</para>
        ''' <para>⛔ La SIEMBRA del transform previo de la transferencia NO vive en `execute`:
        ''' esta en `prepare` (`0x14195B848`-`0x14195B8FF`, con su propio
        ''' `cmp byte ptr [rax+0x1e], 0`). En `execute`, `0x14195C401` es la PUERTA del paso 1,
        ''' no la siembra (motor-102). ⛔ Y desde motor-125 el codigo lo CUMPLE: la siembra la
        ''' hace `Tiempo.Preparar`. Antes el comentario decia esto y el codigo la hacia aca, con
        ''' un booleano propio en vez de `dtSubCacheado == 0`.</para>
        ''' <para>⛔ El `dt` se divide por `s1` **una vez, al principio** (`0x14195C3EF`), y todo lo
        ''' de abajo usa el dividido.</para>
        ''' <para>⛔ Los colisionables del bucle son una **copia** (`simCloth[+0x1C0]`): el original
        ''' no se toca hasta el write-back del paso 5.</para>
        ''' </summary>
        Friend Sub Simular(inst As Instancia, colls As Colisionable(), e As EntradaDelCuadro)
            Dim dt = Simd.Lane0(Simd.DivExacta(Vector128.Create(e.Dt),
                                               Vector128.Create(inst.S1)))       ' 0x14195C3EF

            ' ⛔⛔ ESTA LLAMADA ES DE LA APP, NO DEL MOTOR, y va ACÁ ARRIBA. El motor corre
            ' `prepare` en su propia pasada, ANTES de todos los `execute` (`0x1418C8E60`-`8C`
            ' dentro de `0x1418C8B70`), y `execute` NO lo vuelve a llamar. Queda sólo para los
            ' llamadores que entran derecho a `Simular` sin pasar por la cadena (los gates), y
            ' tiene que ser lo PRIMERO: el paso (1) de acá abajo consume el transform previo que
            ' `prepare` siembra, así que llamarla más adelante la deja llegar tarde — que es
            ' exactamente lo que cazó `GM10c` (motor-125). Es idempotente: `Tiempo.Preparar`
            ' corta con `dtViejo = dtNuevo`, así que por la cadena esta segunda llamada no hace nada.
            Tiempo.Preparar(inst, e.Dt, SubStepsDelCuadro(inst, e.SubStepsDelOperador),
                            e.DampingPorSegundo,
                            e.TransferenciaHabilitada, e.TransformDeTransferencia)

            ' ---- (1) TRANSFER MOTION
            If e.TransferenciaHabilitada Then                                    ' 0x14195C401
                ' ⛔ EL PRIMER CUADRO ARRANCA CON DELTA CERO. `simCloth[+0x108]` es el `dtSub`
                ' cacheado y vale 0 sólo la primera vez; ahí el motor copia el transform ACTUAL al
                ' previo (`simCloth[+0x120..+0x160] = ts.transforms[transformIndex]`) y recién después
                ' transfiere. Con el previo en identidad, el primer cuadro ve la diferencia entre la
                ' identidad y la pose real del hueso — un salto de la distancia entera al origen del
                ' mundo — y la tela sale disparada.
                ' (a) la siembra NO esta aca: vive en `prepare` (`Tiempo.Preparar`), que es
                ' donde la hace el motor (`0x14195B848`-`0x14195B8FF`) y con la senal que el
                ' motor usa (`dtSubCacheado == 0`). El primer cuadro transfiere igual, con
                ' delta CERO: saltear la llamada tambien saltea lo que `TransferMotion` hace
                ' ademas del delta, y `GM10b` lo caza.
                Movimiento.TransferMotion(inst, e.Transferencia,
                                          inst.TransformPrevioDeTransferMotion,
                                          e.TransformDeTransferencia, dt)
                inst.TransformPrevioDeTransferMotion = e.TransformDeTransferencia ' simCloth[+0x120]
            End If

            ' ---- (2) DRIVE COLLIDABLES (con su propia puerta adentro)
            Colisionables.DriveCollidables(colls, e.MapaDeColisionables,
                                           e.TransformSet, dt)                   ' 0x14195C4B0

            ' ---- (3) preparacion
            Dim n = SubStepsDelCuadro(inst, e.SubStepsDelOperador)            ' 0x14195C6A8/B4/B6
            Dim invN = Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F),
                                                 Vector128.Create(CSng(n))))     ' 0x14195C6E6
            Dim dtSub = invN * dt                                                ' 0x14195C6EF

            Dim buffer = ClonarColisionables(colls)                      ' simCloth[+0x1C0]
            Dim snapAnclas = Anclas.Guardar(inst)                                    ' 0x14195C870
            Dim fuerzas(inst.NumParticulas * Instancia.AnchoDeParticula - 1) As Single

            Dim vDtSub = Vector128.Create(dtSub)
            Dim vMedio = Vector128.Create(dtSub * 0.5F)

            ' ---- (4) el bucle de substeps
            For s = 0 To n - 1
                Colisionables.SubstepColisionables(buffer, vDtSub, vMedio)       ' (4a) 0x14195C9B0
                ' ⛔⛔ (4b) ES EL TERRENO, NO LA COLISION GENERAL. Medido sobre el `.exe`:
                ' `computeContactPlanes` (`0x14195DA70`) tiene UN llamador — este, `0x14195CB7A` —
                ' y `SolveContacts` (`0x141A71610`) tiene TRES, los tres adentro de
                ' `TtCollideAndSolve` (`0x141A69730`), que corre en el (4e). Poner la colision
                ' general aca la hacia correr ANTES del integrador y de los constraints, o sea
                ' sobre posiciones de un substep viejo y sin la ultima palabra.
                ' La puerta de este paso (`0x14195E3A0`) no abre nunca en FO4, asi que es no-op;
                ' se llama igual porque el paso existe y su lugar en el orden es este.
                Terreno.ComputarPlanosDeContacto(inst, e.MundoDeTerreno, e.ParticulasDeTerreno,
                                                 e.RadioDeTerreno, e.DetectarPegadas,
                                                 e.FactorDePegado, Nothing)      ' (4b) 0x14195DA70
                Anclas.Interpolar(inst, snapAnclas, s, invN)                         ' (4c) 0x14195CB88
                ' ⛔ 0x141A13007 (`movaps xmm0, [rip+...]` + el bucle de 0x141A13020), sobre el
                ' buffer que el integrador reserva en 0x141A12FD1-0x141A12FFF; despues el LAZO DE
                ' ACCIONES (`0x141A13080`, `mov rax, [rdi+0x168]`) con `xmm2 = dtSub` y
                ' `r9 = fuerzas`, y recien despues el bucle de integracion.
                Integrador.CerarFuerzas(fuerzas)                                 ' (4d) 0x141A13007
                If e.Acciones IsNot Nothing Then                                 '      0x141A13066
                    For Each a In e.Acciones
                        a.Aplicar(inst, e.MasaTotal, dtSub, fuerzas)             '      0x141A1309C vtbl[+0x20]
                    Next
                End If
                Integrador.Integrar(inst, e.Gravedad, fuerzas, dtSub)            '      0x141A12EE0
                Resolver(inst, e, buffer, dtSub, n, s)                           ' (4e) 0x141A133E0
            Next

            ' ---- (5) cierre
            If e.ModoAabb = 0 Then                                               ' simCloth[+0x1C8]
                Aabb.ActualizarAabbDeParticulas(inst)                            ' 0x1418C7300
                ' y el ancho y la máscara quedan VACÍOS: `0x142F3C740` (0x7F7FFFEE en las 4 lanes) al
                ' mínimo y su negación (xorps con 0x80000000 difundido, `0x14195CCED`-`CCFF`) al máximo
                ' (`0x14195CCE9` +0x70, `0x14195CD02` +0x80, `0x14195CD13` +0x90, `0x14195CD1A` +0xA0).
                Dim mas = Vector128.Create(Simd.CasiFltMax)
                Dim menos = Vector128.Xor(mas, Vector128.Create(-0.0F))
                inst.AabbMinAncho = mas : inst.AabbMaxAncho = menos
                inst.AabbMinMascara = mas : inst.AabbMaxMascara = menos
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
                Dim fq = _indicesFueraDeRango
                Logger.LogLazy(Function() $"[MOTOR-SETSOK] declarados={nq} compilados={cq} aplicados={vq} indicesFueraDeRango={fq}")
            End If
        End Sub

        ''' <summary>
        ''' `N` — los substeps del cuadro. `info.subSteps` gana, y si es 0 manda el del operador.
        ''' <para>`0x14195C6A8` lee `info.subSteps`, `0x14195C6B4` lo prueba y `0x14195C6B6` cae al
        ''' `op.subSteps` (+0x24). Vive aca y no en dos lados porque lo necesitan el `prepare` y el
        ''' `execute`, y una ley con dos dueños se desincroniza (motor-103).</para>
        ''' </summary>
        Friend Function SubStepsDelCuadro(inst As Instancia, subStepsDelOperador As Integer) As Integer
            Dim n = If(inst IsNot Nothing AndAlso inst.Info IsNot Nothing AndAlso inst.Info.SubSteps > 0,
                       CInt(inst.Info.SubSteps), subStepsDelOperador)
            If n <= 0 Then n = 1
            Return n
        End Function

        ''' <summary>Cuántos conjuntos aplicó el último <see cref="Resolver"/>. Lo publica
        ''' <see cref="Simular"/> y lo lee el arnés: es la cuenta de quien HACE el trabajo, no la de
        ''' quien lo prepara.
        ''' <para>⛔ `ThreadStatic` porque el pipeline simula un bloque por hilo (`_state` es un
        ''' `ConcurrentDictionary` por esa razón): un contador compartido daría números de otra
        ''' prenda, que es peor que no tener contador.</para></summary>
        <ThreadStatic>
        Private _setsAplicados As Integer

        ''' <summary>Indices de `constraintExecution` fuera de rango que el paso 4e descarto.
        ''' El motor indexa sin comprobar, asi que esto no deberia pasar nunca: si pasa, el dato
        ''' esta corrupto y hay que verlo, no saltearlo en silencio (motor-104).</summary>
        <ThreadStatic>
        Private _indicesFueraDeRango As Integer

        ''' <summary>
        ''' El paso 4e — `0x141A133E0` (sin `constraintExecution`) y `0x141A13650` (con).
        ''' <para>⛔⛔ ES UN LAZO, no una pasada. `op.numberOfSolveIterations` (+0x28) envuelve
        ''' TODO el paso: los sets **y** la colision (`0x141A134F7`/`0x141A135B1` en una rama,
        ''' `0x141A13756`/`0x141A13825` en la otra). En cero no corre nada, y se transcribe asi:
        ''' poner un minimo de 1 seria inventar.</para>
        ''' <para>⛔⛔ EL MODO SE PROMUEVE ANTES DE TODO: si vale 1 y `op.adaptConstraintStiffness`
        ''' esta prendido, pasa a 2 (`0x141A1349B`, `0x141A136F1`). El promovido es el que viaja a
        ''' `k` (`0x141A13538 mov edx, ebp`) y a `usaK` (`0x141A134BC`) — cambia las dos.</para>
        ''' <para>⛔⛔ LA COLISION VA ADENTRO, DESPUES DE LOS SETS (`0x141A135A4`). Los constraints
        ''' no pueden tener la ultima palabra: si la tuvieran, la tela cerraria el substep
        ''' penetrando el colisionable.</para>
        ''' <para>El `k` de cada conjunto sale de <see cref="Tiempo.FactorDeRigidez"/>
        ''' (`0x1418C6420`), y el `usaK` de <see cref="Tiempo.UsaK"/>.</para>
        ''' </summary>
        Private Sub Resolver(inst As Instancia, e As EntradaDelCuadro,
                             colisionables As Colisionable(), dtSub As Single,
                             n As Integer, s As Integer)
            _setsAplicados = 0
            _indicesFueraDeRango = 0
            Dim ctx As ContextoDeSolve
            ctx.Instancia = inst
            ctx.Buffers = e.Buffers
            ctx.TransformSets = e.TransformSets
            ctx.IndiceDelSet = 0

            ' ⛔ LA PROMOCION DEL MODO, ANTES DE `usaK` Y DE `k` — 0x141A1349B / 0x141A136F1.
            Dim modo = inst.Modo
            If modo = 1 AndAlso e.AdaptaRigidez Then modo = 2

            ctx.UsaK = Tiempo.UsaK(modo, n, inst.S1, inst.S2)                    ' 0x141A134BC-E3
            ' CUAL DE LOS DOS CAMINOS: lo necesita el pase de relojes de abajo, que es de
            ' `0x141A13650` y NO existe en `0x141A133E0` (medido: cero `call 0x1419f7f70` en
            ' todo el rango del camino sin lista).
            Dim conLista = e.EjecucionDeRestricciones IsNot Nothing AndAlso
                           e.EjecucionDeRestricciones.Length > 0
            ' ⛔ EL LAZO EXTERNO. `jle` a la salida si el contador es <= 0 (0x141A134FB).
            For it = 0 To e.IteracionesDeSolve - 1                                ' 0x141A134F7
                If conLista Then
                    ' ---- el camino CON `constraintExecution` — 0x141A13650 ----
                    ' ⛔ `-1` NO es un indice invalido: es «aca va la colision» (0x141A13798/A9).
                    ' Por eso el campo existe — para INTERCALAR colision entre conjuntos.
                    For j = 0 To e.EjecucionDeRestricciones.Length - 1            ' 0x141A13772/0F
                        Dim idx = e.EjecucionDeRestricciones(j)                   ' 0x141A13790/94
                        If idx = -1 Then
                            _setsAplicados += Colision.ColisionYSolve(
                                inst, e, colisionables, dtSub, ctx)      ' 0x141A137A9
                        ElseIf e.Restricciones Is Nothing OrElse
                               idx < 0 OrElse idx >= e.Restricciones.Length Then
                            ' ⛔ EL DESCARTE DEJA RASTRO (motor-104). El motor indexa SIN
                            ' comprobar (`0x141A137CE mov rbx, [rax+rcx*8]`), asi que un indice
                            ' fuera de rango es dato corrupto; saltearlo en silencio esconde
                            ' eso. MEDIDO: en el corpus estan todos en rango (`--motorcenso`, M4).
                            _indicesFueraDeRango += 1
                        Else
                            Dim cs = e.Restricciones(idx)                         ' 0x141A137CE
                            If cs IsNot Nothing Then
                                ' ⛔ el 4.º argumento de `solve` es el INDICE DE LA LISTA, no el
                                ' del bucle (`0x141A137F6 mov r9d, dword ptr [rsi + rax]`).
                                ctx.IndiceDelSet = idx
                                cs.Aplicar(ctx, Tiempo.FactorDeRigidez(
                                    cs.Tipo, modo, s, n, inst.S1, inst.S2))       ' 0x141A137D5
                                _setsAplicados += 1
                            End If
                        End If
                    Next
                Else
                    ' ---- el camino SIN `constraintExecution` — 0x141A133E0 ----
                    If e.Restricciones IsNot Nothing Then
                        ' ⛔ EL ÍNDICE DEL SET ES SU POSICIÓN EN LA LISTA. Es el 4.º argumento de
                        ' `solve` (`0x141A1355A mov r9d, edi`), y con él los sets con estado buscan
                        ' su bloque en la instancia. Dejarlo en 0 haría que compartieran uno.
                        For i = 0 To e.Restricciones.Length - 1                   ' 0x141A13512/7C
                            Dim cs = e.Restricciones(i)
                            If cs Is Nothing Then Continue For
                            ctx.IndiceDelSet = i
                            cs.Aplicar(ctx, Tiempo.FactorDeRigidez(
                                cs.Tipo, modo, s, n, inst.S1, inst.S2))
                            _setsAplicados += 1
                        Next
                    End If
                    ' ⛔ Y LA COLISION AL FINAL DE CADA ITERACION — 0x141A135A4.
                    _setsAplicados += Colision.ColisionYSolve(inst, e, colisionables, dtSub, ctx)
                End If
            Next

            ' ⛔⛔ EL RELOJ POR PARTICULA DEL ANTI-PELLIZCO: `0x141A138C9 call 0x1419f7f70`.
            ' Va DESPUES del lazo de iteraciones, una vez por substep, y SOLO en el camino con
            ' lista: `0x141A133E0` no lo tiene (medido). La puerta es `pinchDetectionEnabled`
            ' (`0x141A13857 cmp byte [rax+0x1c], 0`) y solo entran los de TIPO 19
            ' (`0x141A1388B cmp dword [rcx+0x18], 0x13`), de la lista `antiPinchConstraintSets`
            ' (+0xC8/+0xD0), que es OTRA que la de los estaticos.
            If conLista AndAlso inst.PellizcoHabilitado AndAlso e.AntiPellizcos IsNot Nothing Then
                For q = 0 To e.AntiPellizcos.Length - 1                           ' 0x141A13873/D8
                    Dim ap = TryCast(e.AntiPellizcos(q), AntiPellizco)
                    If ap Is Nothing OrElse ap.Tipo <> 19 Then Continue For       ' 0x141A1388B
                    ap.AvanzarReloj(inst, q, dtSub)                               ' 0x141A138C9
                Next
            End If
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
                ' ⛔ LOS TRES CAMPOS DE PELLIZCO, no uno. El ctor del buffer de trabajo los
                ' escribe juntos desde `collidablePinchingDatas` (0x1418C68B6-0x1418C68D5):
                ' `+0x80` activo, `+0x81` prioridad, `+0x84` radio. Sin la prioridad, dos
                ' colisionables empatan en 0 y el segundo BORRA el contacto del primero.
                r(i) = New Colisionable() With {
                    .Transform = c.Transform,
                    .VelLineal = c.VelLineal,
                    .VelAngular = c.VelAngular,
                    .PellizcoActivo = c.PellizcoActivo,
                    .PrioridadDePellizco = c.PrioridadDePellizco,
                    .RadioDePellizco = c.RadioDePellizco,
                    .Forma = c.Forma}
            Next
            Return r
        End Function

    End Module

End Namespace

