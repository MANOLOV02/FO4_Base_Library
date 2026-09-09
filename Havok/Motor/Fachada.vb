Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' LA FACHADA — de los `HkObj_*` del archivo a los tipos del motor.
'
' Todo lo que hay aca sale de la REFLEXION (`HavokLayout_FO4.vb`), que es la lista cerrada de
' campos de cada clase. Ni un offset a mano.
'
' ⛔ Esta es la unica pieza que traduce; el resto del motor no sabe que existe un archivo.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor


    ''' <summary>
    ''' ⭐ El estado vivo de una prenda simulada: lo que sobrevive de un cuadro al siguiente.
    ''' <para>⛔ **Tiene que persistir.** `Instancia.Posiciones`/`Previas` **son** el estado del
    ''' Verlet: si se reconstruyen cada cuadro, la tela arranca de cero y no se mueve nunca. Los
    ''' `Colisionable` guardan el transform del cuadro anterior, que es la «pose vieja» desde la que
    ''' `setTransform` deriva las velocidades (`0x1419605F0`). Y los BUFFERS tambien: el de tipo 1 ES
    ''' el array de particulas (`0x1418C7040`), asi que rearmarlos seria tirar la simulacion.</para>
    ''' <para>Esta clase es **la unica** superficie que el resto de la app necesita del motor.</para>
    ''' </summary>
    Friend NotInheritable Class PrendaSimulada

        ''' <summary>El `hclClothData` del que salio todo.</summary>
        Friend ReadOnly Datos As HkObj_HclClothData

        ''' <summary>El `hclSimClothData` que la cadena simula — el que nombra el
        ''' `hclSimulateOperator.simClothIndex`, o el 0 si la cadena no simula.</summary>
        Friend ReadOnly Sim As HkObj_HclSimClothData

        ''' <summary>El estado de las particulas. ⛔ Persiste entre cuadros.</summary>
        Friend ReadOnly Estado As Instancia

        ''' <summary>Los colisionables, con su transform del cuadro anterior. ⛔ Persiste.</summary>
        Friend ReadOnly Colisionadores As Colisionable()

        ''' <summary>Los buffers vivos. ⛔ Persiste: el de tipo 1 comparte el array de particulas.</summary>
        Friend ReadOnly Buffers As Buffer()

        ''' <summary>Los nombres de hueso de `hkaSkeleton.bones` de la prenda, en su orden.</summary>
        Friend ReadOnly NombresDeHueso As System.Collections.Generic.IList(Of String)

        ''' <summary>Los conjuntos de restricciones del archivo, compilados una vez.</summary>
        Friend ReadOnly Restricciones As SetCompilado()

        ''' <summary>Los `hclAntiPinchConstraintSet`, de la OTRA lista (`antiPinchConstraintSets`).
        ''' ⛔ Se corren aparte de los estaticos, como en el motor.</summary>
        Friend ReadOnly AntiPellizcos As SetCompilado()

        ''' <summary>La cadena `hclClothState.operators`, compilada en el orden del archivo.</summary>
        Friend ReadOnly Operadores As OperadorCompilado()

        ''' <summary>Cuantos conjuntos de transforms declara el archivo
        ''' (`hclClothData.transformSetDefinitions`). ⛔ El tamano de cada uno lo declara
        ''' `numTransforms`, que el censo M18 midio igual a `hkaSkeleton.bones.Count` en las 759.</summary>
        Friend ReadOnly NumTransformSets As Integer

        Private Sub New(datos As HkObj_HclClothData, sim As HkObj_HclSimClothData, estado As Instancia,
                        colisionadores As Colisionable(), buffers As Buffer(),
                        nombresDeHueso As System.Collections.Generic.IList(Of String),
                        restricciones As SetCompilado(), operadores As OperadorCompilado(),
                        numTransformSets As Integer, antiPellizcos As SetCompilado())
            Me.Datos = datos
            Me.Sim = sim
            Me.Estado = estado
            Me.Colisionadores = colisionadores
            Me.Buffers = buffers
            Me.NombresDeHueso = nombresDeHueso
            Me.Restricciones = restricciones
            Me.Operadores = operadores
            Me.NumTransformSets = numTransformSets
            Me.AntiPellizcos = antiPellizcos
        End Sub

        ''' <summary>
        ''' Arma la prenda desde el archivo. Devuelve `Nothing` si el dato no da para simular.
        ''' </summary>
        ''' <param name="estadoACorrer">El `hclClothState` que se corre. ⛔ Lo elige el LLAMADOR: el
        ''' motor corre el estado que el juego le pide por nombre («Simulate», «Animate»), y esta
        ''' fachada no tiene por que tener politica propia sobre eso.</param>
        ''' <param name="nombresDeHueso">`hkaSkeleton.bones` de la prenda, por su nombre y en su orden.
        ''' Es el puente que `transformIndices` indexa.</param>
        Friend Shared Function Crear(datos As HkObj_HclClothData,
                                     estadoACorrer As HkObj_HclClothState,
                                     nombresDeHueso As System.Collections.Generic.IList(Of String)) As PrendaSimulada
            If datos Is Nothing OrElse datos.SimClothDatas Is Nothing OrElse datos.SimClothDatas.Count = 0 Then
                Return Nothing
            End If
            Dim ops = CadenaDe(datos, estadoACorrer)

            ' ⛔ El sim-cloth lo nombra el operador, no el orden: el motor hace
            ' `simCloth = clothInstance.simCloths[op.simClothIndex]` (0x14195C3E0). Sin simulate en la
            ' cadena (las cadenas «Animate») igual hace falta una instancia: es la que el deform lee.
            Dim si = 0
            For Each o In ops
                Dim osim = TryCast(o, OpSimular)
                If osim IsNot Nothing Then si = osim.IndiceDelSimCloth : Exit For
            Next
            If si < 0 OrElse si >= datos.SimClothDatas.Count Then si = 0
            Dim sim = datos.SimClothDatas(si)
            If sim Is Nothing Then Return Nothing

            Dim inst = InstanciaDe(sim)
            If inst Is Nothing OrElse inst.NumParticulas = 0 Then Return Nothing
            Dim nts = If(datos.TransformSetDefinitions Is Nothing, 0, datos.TransformSetDefinitions.Count)
            Dim prenda = New PrendaSimulada(datos, sim, inst, ColisionablesDe(sim), BuffersDe(datos, inst),
                                            nombresDeHueso, RestriccionesDe(sim), ops, nts,
                                            AntiPellizcosDe(sim))
            ' ⛔ La cobertura del DATO se publica al armar, una vez y SIEMPRE (con el log prendido):
            ' es lo que deja ver si lo que el archivo declara llego entero al motor.
            Cobertura.Publicar(prenda)
            Return prenda
        End Function

        ''' <summary>
        ''' ⭐⭐ Un cuadro — la cadena `hclClothState.operators` entera, en el orden del archivo.
        ''' <para>⛔ `poseDe` devuelve la pose global del hueso vivo con ese nombre, o `Nothing`. Para
        ''' los colisionables la ley es esa pose **tal cual** (`offsets[i] × transformSet[hueso]`); el
        ''' bind embebido es del SKINNING y no va aca.</para>
        ''' <para>⛔ `modoAabb` es `simCloth[+0x1C8]`, campo de runtime.</para>
        ''' </summary>
        Friend Sub Cuadro(dt As Single, modoAabb As Integer,
                          poseDe As Func(Of String, Single()))
            ' ⛔⛔ EL TRANSFORM SET ES ESTADO VIVO, NO SE REARMA. El deform escribe ahi la pose de
            ' los cloth-bones y el skin del cuadro SIGUIENTE la lee: ese es el lazo del sistema. Los
            ' cloth-bones no estan animados — su pose sin fisica es la del bind —, asi que rearmarlo
            ' entero cada cuadro los devolvia al bind mientras el cuerpo se movia, y el skin producia
            ' una malla estirada entre las dos poses. En reposo no se nota; en animacion la falda
            ' explota.
            ' La regla del refresco sale de quien es DUEÑO de cada entrada: la que el deform escribe
            ' es suya y se conserva; las demas las refresca la pose del esqueleto, que es lo que el
            ' juego mueve.
            Dim ts = TransformSetDeHuesos(NombresDeHueso, poseDe)
            If _sets Is Nothing Then
                ' primer cuadro: todo sale de la pose, incluidos los cloth-bones (todavia no hay
                ' deform que haya escrito nada)
                ReDim _sets(Math.Max(0, NumTransformSets - 1))
                For i = 0 To _sets.Length - 1
                    Dim copia(ts.Length - 1) As Mat4
                    Array.Copy(ts, copia, ts.Length)
                    _sets(i) = copia
                Next
            Else
                Dim delDeform = HuesosConCapa
                For i = 0 To _sets.Length - 1
                    Dim dst = _sets(i)
                    If dst Is Nothing OrElse dst.Length <> ts.Length Then
                        Dim copia(ts.Length - 1) As Mat4
                        Array.Copy(ts, copia, ts.Length)
                        _sets(i) = copia
                        Continue For
                    End If
                    For b = 0 To ts.Length - 1
                        dst(b) = ts(b)
                    Next
                    ' y se devuelve lo que el deform habia escrito: es suyo
                    For Each b In delDeform
                        If b >= 0 AndAlso b < dst.Length AndAlso _propiasDelDeform IsNot Nothing AndAlso
                           b < _propiasDelDeform.Length Then
                            dst(b) = _propiasDelDeform(b)
                        End If
                    Next
                Next
            End If
            Dim sets = _sets

            Dim ctx As ContextoDeCadena
            ctx.Buffers = Buffers
            ctx.TransformSets = sets
            ctx.Instancia = Estado
            ctx.Colisionadores = Colisionadores
            ' ⛔ EL TRANSFORM DE TRANSFERENCIA SALE DEL TRANSFORM SET (`0x14195C401`):
            '     M = transformSets[ transferMotionData.transformSetIndex ].transforms[ transformIndex ]
            ' Pasarle la identidad hacia que la transferencia comparara identidad contra identidad y
            ' no transfiriera nada: en animacion la tela se quedaba atras del actor y los enlaces la
            ' estiraban (x60 de distorsion contra OFF, la falda desplegada hacia un lado).
            Dim tm = TransferenciaDe(Sim)
            Dim mT = Mat4.Identidad
            If tm.IndiceDelSet >= 0 AndAlso tm.IndiceDelSet < sets.Length Then
                Dim tsT = sets(tm.IndiceDelSet)
                If tsT IsNot Nothing AndAlso tm.IndiceDelTransform >= 0 AndAlso
                   tm.IndiceDelTransform < tsT.Length Then
                    mT = tsT(tm.IndiceDelTransform)
                End If
            End If
            ctx.Cuadro = EntradaDeCuadro(Sim, dt, 1, modoAabb, ts, mT)
            ctx.Cuadro.Restricciones = Restricciones
            ctx.Cuadro.AntiPellizcos = AntiPellizcos
            Cadena.Ejecutar(Operadores, ctx)
            _salida = ctx.TransformSets
            ' lo que el deform acaba de escribir queda guardado: es lo que el skin del proximo cuadro
            ' tiene que ver, y lo que el refresco de arriba no puede pisar.
            Dim sal = TransformSetDeSalida
            If sal IsNot Nothing Then
                If _propiasDelDeform Is Nothing OrElse _propiasDelDeform.Length <> sal.Length Then
                    ReDim _propiasDelDeform(sal.Length - 1)
                End If
                For Each b In HuesosConCapa
                    If b >= 0 AndAlso b < sal.Length Then _propiasDelDeform(b) = sal(b)
                Next
            End If
            ' el control del instrumento: sobre la piel, no sobre las particulas
            Cobertura.ControlDeLaPiel(Me)
        End Sub

        ''' <summary>
        ''' ⭐ El `transformSet` que el deform escribio en el ultimo cuadro — la pose GLOBAL de cada
        ''' cloth-bone, en el orden de `hkaSkeleton.bones`.
        ''' <para>Es la unica salida que el render necesita: de aca sale
        ''' `HierarchiBone_class.PhysicsDeltaTransform`.</para>
        ''' <para>El indice sale del `outputTransformSetIdx` del propio operador de deform; si la
        ''' cadena no trae deform, no hay salida y devuelve `Nothing`.</para>
        ''' </summary>
        Friend ReadOnly Property TransformSetDeSalida As Mat4()
            Get
                If _salida Is Nothing Then Return Nothing
                For Each o In Operadores
                    Dim od = TryCast(o, OpDeformar)
                    If od Is Nothing Then Continue For
                    If od.TransformSetDeSalida < 0 OrElse od.TransformSetDeSalida >= _salida.Length Then Return Nothing
                    Return _salida(od.TransformSetDeSalida)
                Next
                Return Nothing
            End Get
        End Property

        ''' <summary>Los huesos que el deform de esta cadena escribe — los unicos que tienen capa
        ''' de fisica. Vacio si la cadena no trae deform.</summary>
        Friend ReadOnly Property HuesosConCapa As Integer()
            Get
                For Each o In Operadores
                    Dim od = TryCast(o, OpDeformar)
                    If od IsNot Nothing Then Return od.HuesosQueEscribe
                Next
                Return Array.Empty(Of Integer)()
            End Get
        End Property

        Private _salida As Mat4()()

        ''' <summary>`hclClothInstance.transformSets` (+0x40) — ESTADO VIVO, persiste entre cuadros.</summary>
        Private _sets As Mat4()()

        ''' <summary>Lo que el deform escribio en el ultimo cuadro, por hueso. El refresco de la pose
        ''' no lo pisa: esas entradas son del deform, no del esqueleto.</summary>
        Private _propiasDelDeform As Mat4()

    End Class


    Friend Module Fachada

        ''' <summary>
        ''' La forma de colision de un `hclCollidable`, segun el `type` de su `hclShape` (`+0x10`).
        ''' <para>El censo del cap. 2.3 es una **lista cerrada de 12 entradas**; los tipos 4, 6, 7 y
        ''' 8 son *assert / no-op* en la tabla de despacho `0x141A75DD0`, asi que devuelven
        ''' `Nothing` y el colisionable no colisiona con nada.</para>
        ''' <para>⛔ El **tipo 10** (`hclPointContactPlanesShape`) **no puede salir de un archivo**:
        ''' no esta en la reflexion y su vtable es de runtime (cap. 2ter). Lo fabrica
        ''' `computeContactPlanes` para el terreno; aca nunca se construye.</para>
        ''' </summary>
        Friend Function FormaDeColisionable(cd As HkObj_HclCollidable) As Forma
            If cd Is Nothing OrElse cd.Shape Is Nothing Then Return Nothing
            Dim g = cd.Graph

            Dim esf = HkObj_HclSphereShape.Leer(g, cd.Shape)
            If esf IsNot Nothing Then
                ' ⛔ el radio es la `w` del `hkSphere` (`shape+0x2C`), no un campo aparte
                Dim s = V4(If(esf.Sphere Is Nothing, Nothing, esf.Sphere.Pos))
                Return New Esfera(s.WithElement(Simd.LaneW, 0.0F), s.GetElement(Simd.LaneW))
            End If

            Dim cono = HkObj_HclTaperedCapsuleShape.Leer(g, cd.Shape)
            If cono IsNot Nothing Then
                ' ⛔ el archivo SERIALIZA los derivados; se toman de ahi, que es lo que el motor
                ' tiene en memoria al cargar. `Derivar` los recalcula por cuadro.
                Return New CapsulaConica(V4(cono.Small), V4(cono.Big), V4(cono.ConeApex),
                                         V4(cono.ConeAxis), cono.SmallRadius, cono.BigRadius,
                                         cono.L, cono.D, cono.CosTheta, cono.SinTheta, cono.TanTheta)
            End If

            Dim cap = HkObj_HclCapsuleShape.Leer(g, cd.Shape)
            If cap IsNot Nothing Then
                Return New Capsula(V4(cap.Start), V4(cap.End), cap.Radius, cap.CapLenSqrdInv)
            End If

            Dim pl = HkObj_HclPlaneShape.Leer(g, cd.Shape)
            If pl IsNot Nothing Then Return New Plano(V4(pl.PlaneEquation))

            Dim cp = HkObj_HclConvexPlanesShape.Leer(g, cd.Shape)
            If cp IsNot Nothing Then
                Return New PlanosConvexos(Aplanar(cp.PlaneEquations),
                                          M4(cp.LocalFromWorld), M4(cp.WorldFromLocal),
                                          AabbMin(cp.ObjAabb), AabbMax(cp.ObjAabb),
                                          V4(cp.GeomCentroid))
            End If

            Dim cg = HkObj_HclConvexGeometryShape.Leer(g, cd.Shape)
            If cg IsNot Nothing Then
                Return New GeometriaConvexa(Aplanar(cg.TetrahedraEquations), CInt(cg.GridRes),
                                            M4(cg.LocalFromWorld), M4(cg.WorldFromLocal),
                                            AabbMin(cg.ObjAabb), AabbMax(cg.ObjAabb),
                                            V4(cg.GeomCentroid),
                                            AEnteros(cg.TetrahedraGrid), AEnteros(cg.GridCells),
                                            V4(cg.InvCellSize))
            End If

            Dim hf = HkObj_HclConvexHeightFieldShape.Leer(g, cd.Shape)
            If hf IsNot Nothing Then
                Return New CampoDeAlturasConvexo(CInt(hf.Res), CInt(hf.ResIncBorder),
                                                 ABytesDeEnteros(hf.Heights), AEnteros(hf.Faces),
                                                 M4(hf.LocalToMapTransform), V4(hf.LocalToMapScale))
            End If

            ' tipos 4, 6, 7, 8 (assert / no-op) y cualquier cosa que no reconozcamos
            Return Nothing
        End Function

        ''' <summary>
        ''' Los colisionables de una prenda — `data.perInstanceCollidables` (`+0xA8`).
        ''' <para>⛔ El transform que traen es **el del archivo**, y es el punto de partida: el paso
        ''' 2 del cuadro se lo pisa con la pose vieja y le deriva las velocidades
        ''' (`setTransform`, `0x1419605F0`). Las `linearVelocity`/`angularVelocity` serializadas
        ''' son **basura** y no se copian.</para>
        ''' <para>⛔ `pinchDetectionEnabled` (`+0x80`) del archivo tampoco: el buffer de trabajo lo
        ''' reescribe desde `data.collidablePinchingDatas` (`+0x118`), que es una lista PARALELA a
        ''' `perInstanceCollidables`. Eso es lo que cuenta la puerta de `TtCollideAndSolve`
        ''' (`0x141A697F0`: `cmp byte ptr [rcx], dil` con paso `0x90` sobre el buffer de
        ''' trabajo).</para>
        ''' </summary>
        Friend Function ColisionablesDe(sim As HkObj_HclSimClothData) As Colisionable()
            If sim Is Nothing Then Return Array.Empty(Of Colisionable)()
            Dim lista = sim.PerInstanceCollidables
            If lista Is Nothing OrElse lista.Count = 0 Then Return Array.Empty(Of Colisionable)()
            ' ⛔ LISTA PARALELA, por INDICE. `collidablePinchingDatas` (+0x118) no trae de que
            ' colisionable habla: el motor la recorre a la par (misma `i`).
            ' ⛔⛔ QUE PASA SI ES MAS CORTA: el motor NO compara contra el `count`
            ' (`0x1418C68A3 mov rdx, [rdi+0x118]` + `[rdx + r13]` con `r13 = i*8`), o sea que
            ' leeria fuera de la lista. Aca se recorta, y eso ES un default — pero MEDIDO no se
            ' ejerce: `collidablePinchingDatas` cubre `perInstanceCollidables` en las 759
            ' prendas del corpus, ninguna mas corta, ninguna sin lista (`--motorcenso`).
            Dim pinch = sim.CollidablePinchingDatas
            Dim r(lista.Count - 1) As Colisionable
            For i = 0 To lista.Count - 1
                Dim cd = lista(i)
                If cd Is Nothing Then Continue For
                Dim opta = False
                Dim radioPellizco = 0.0F
                Dim prioridad = 0
                If pinch IsNot Nothing AndAlso i < pinch.Count AndAlso pinch(i) IsNot Nothing Then
                    opta = pinch(i).PinchDetectionEnabled                ' +0x00
                    prioridad = pinch(i).PinchDetectionPriority          ' +0x01
                    radioPellizco = pinch(i).PinchDetectionRadius        ' +0x04
                End If
                r(i) = New Colisionable() With {
                    .Transform = M4(cd.Transform),
                    .VelLineal = Vector128(Of Single).Zero,
                    .VelAngular = Vector128(Of Single).Zero,
                    .PellizcoActivo = opta,
                    .PrioridadDePellizco = prioridad,
                    .RadioDePellizco = radioPellizco,
                    .Forma = FormaDeColisionable(cd)}
            Next
            Return r
        End Function

        ''' <summary>
        ''' El mapa de transforms de los colisionables — `data.collidableTransformMap` (`+0x80`).
        ''' <para>⛔ `transformSetIndex` es **`int32` con signo** en la reflexion: negativo apaga los
        ''' pasos 2 y 5b enteros.</para>
        ''' </summary>
        Friend Function MapaDe(sim As HkObj_HclSimClothData) As MapaDeColisionables
            Dim r As MapaDeColisionables
            r.IndiceDelSet = -1
            If sim Is Nothing OrElse sim.CollidableTransformMap Is Nothing Then Return r
            Dim m = sim.CollidableTransformMap
            r.IndiceDelSet = m.TransformSetIndex
            r.Indices = AEnteros(m.TransformIndices)
            Dim offs = m.Offsets
            If offs IsNot Nothing AndAlso offs.Count > 0 Then
                Dim a(offs.Count - 1) As Mat4
                For i = 0 To offs.Count - 1
                    a(i) = M4(offs(i))
                Next
                r.Offsets = a
            End If
            Return r
        End Function

        ''' <summary>`data.transferMotionData` (`+0x150`) — los 12 campos de la reflexion.</summary>
        Friend Function TransferenciaDe(sim As HkObj_HclSimClothData) As DatosDeTransferencia
            Dim r As DatosDeTransferencia
            If sim Is Nothing OrElse sim.TransferMotionData Is Nothing Then Return r
            Dim t = sim.TransferMotionData
            r.IndiceDelSet = CInt(t.TransformSetIndex)
            r.IndiceDelTransform = CInt(t.TransformIndex)
            r.TransfiereTraslacion = t.TransferTranslationMotion
            r.VelMinTraslacion = t.MinTranslationSpeed
            r.VelMaxTraslacion = t.MaxTranslationSpeed
            r.BlendMinTraslacion = t.MinTranslationBlend
            r.BlendMaxTraslacion = t.MaxTranslationBlend
            r.TransfiereRotacion = t.TransferRotationMotion
            r.VelMinRotacion = t.MinRotationSpeed
            r.VelMaxRotacion = t.MaxRotationSpeed
            r.BlendMinRotacion = t.MinRotationBlend
            r.BlendMaxRotacion = t.MaxRotationBlend
            Return r
        End Function

        ''' <summary>
        ''' El `transformSet` del cuadro, a partir de las poses globales de los huesos.
        ''' <para>⛔ Esta funcion NO decide de donde salen las poses: eso es de la app, y la ley ya
        ''' esta establecida y validada en `HavokClothSimulation` — para los COLISIONABLES es la
        ''' pose global del hueso vivo tal cual (`offsets[i] × transformSet[hueso]`), y para el
        ''' SKINNING lleva ademas el bind embebido (`bindEmbebido × inv(bindVivo) × actualVivo`,
        ''' medido a 0,0011 u contra el `DefaultClothPose`). Son leyes distintas y no se mezclan.
        ''' Aca solo se convierte el tipo.</para>
        ''' <para>⛔ Las filas van tal cual: `Mat4` es row-major con la traslacion en `F3`, igual
        ''' que el `transform` de Havok (`+0x50` = fila 3) y que `Matrix4` de OpenTK
        ''' (`M41..M44`).</para>
        ''' </summary>
        Friend Function TransformSetDePoses(poses As Single()()) As Mat4()
            If poses Is Nothing OrElse poses.Length = 0 Then Return Array.Empty(Of Mat4)()
            Dim r(poses.Length - 1) As Mat4
            For i = 0 To poses.Length - 1
                r(i) = M4(poses(i))
            Next
            Return r
        End Function


        ''' <summary>
        ''' El `transformSet` que consume `DriveCollidables`, indexado como
        ''' `collidableTransformMap.transformIndices` — o sea por **hueso del esqueleto de la
        ''' prenda**.
        ''' <para>⛔ Para los COLISIONABLES la ley es `offsets[i] × transformSet[hueso]` con
        ''' `transformSet[hueso]` = la **pose global del hueso vivo tal cual**. NO lleva el bind
        ''' embebido: eso es del SKINNING (`bindEmbebido × inv(bindVivo) × actualVivo`, otra ley,
        ''' medida aparte a 0,0011 u contra el `DefaultClothPose`). Mezclarlas mueve las capsulas
        ''' a cualquier lado.</para>
        ''' <para>⛔ El puente indice→hueso es el **NOMBRE**: `transformIndices[i]` indexa
        ''' `hkaSkeleton.bones` de la prenda, y el hueso vivo se busca por su nombre. Es lo que ya
        ''' resuelve el parser en `PopulateResolvedCollidableBindings`.</para>
        ''' <para>`poseDe` devuelve los 16 `Single` de la pose global de un hueso, o `Nothing` si
        ''' ese hueso no esta vivo. Los que no estan quedan en **identidad**, que es lo que deja el
        ''' colisionable en su transform serializado.</para>
        ''' </summary>
        Friend Function TransformSetDeHuesos(nombresDeHueso As System.Collections.Generic.IList(Of String),
                                             poseDe As Func(Of String, Single())) As Mat4()
            If nombresDeHueso Is Nothing OrElse nombresDeHueso.Count = 0 Then Return Array.Empty(Of Mat4)()
            Dim r(nombresDeHueso.Count - 1) As Mat4
            For i = 0 To nombresDeHueso.Count - 1
                r(i) = Mat4.Identidad
                Dim nm = nombresDeHueso(i)
                If String.IsNullOrWhiteSpace(nm) OrElse poseDe Is Nothing Then Continue For
                Dim p = poseDe(nm.Trim())
                If p IsNot Nothing Then r(i) = M4(p)
            Next
            Return r
        End Function

        ''' <summary>
        ''' La `EntradaDelCuadro` armada desde el archivo: todo lo que sale de `simulationInfo`
        ''' (`data+0x10`) y de `data`.
        ''' <para>⛔ Lo que **no** puede salir del archivo y por eso va por parametro:
        ''' <list type="bullet">
        ''' <item>`dt` — lo trae el contexto del operador;</item>
        ''' <item>`subStepsDelOperador` — es `op.subSteps` de `hclSimulateOperator`, que solo se usa
        ''' si `info.subSteps` es 0 (`0x14195C6A8`);</item>
        ''' <item>`modoAabb` — es `simCloth[+0x1C8]`, campo de **runtime**;</item>
        ''' <item>`transformSet` — sale del esqueleto vivo, no del paquete.</item>
        ''' </list></para>
        ''' <para>⛔ `data.doNormals` (`+0x14C`) es lo que decide si hay normales; si esta apagado
        ''' los indices de triangulo quedan en `Nothing` y el paso 5 no las toca.</para>
        ''' </summary>
        Friend Function EntradaDeCuadro(sim As HkObj_HclSimClothData, dt As Single,
                                        subStepsDelOperador As Integer, modoAabb As Integer,
                                        transformSet As Mat4(),
                                        transformDeTransferencia As Mat4) As EntradaDelCuadro
            Dim e As EntradaDelCuadro
            e.Dt = dt
            e.SubStepsDelOperador = subStepsDelOperador
            e.ModoAabb = modoAabb
            e.TransformSet = transformSet
            e.TransformDeTransferencia = transformDeTransferencia
            e.MapaDeColisionables = MapaDe(sim)
            e.Transferencia = TransferenciaDe(sim)
            If sim Is Nothing Then Return e

            Dim info = sim.SimulationInfo
            If info IsNot Nothing Then
                e.Gravedad = V4(info.Gravity)                            ' simulationInfo+0x00
                e.DampingPorSegundo = info.GlobalDampingPerSecond        ' +0x10
                e.TransferenciaHabilitada = info.TransferMotionEnabled   ' +0x1E
            End If

            ' ---- lo del paso 4b (`computeContactPlanes`) ----
            ' ⛔ `MundoDeTerreno` queda en `Nothing`: en FO4 NADIE escribe `[inst+0xF8]`/`[+0x100]`
            ' (el unico escritor, `0x1418C7B10`, no tiene llamadores), asi que la puerta
            ' `0x14195E3A0` no abre. Los otros tres si salen del archivo y son las condiciones
            ' que el motor mira para decidirlo, no un adorno.
            e.ParticulasDeTerreno = CInt(sim.NumLandscapeCollidableParticles)   ' +0x148
            Dim tierra = sim.LandscapeCollisionData                             ' +0x134
            If tierra IsNot Nothing Then
                e.RadioDeTerreno = tierra.LandscapeRadius
                e.DetectarPegadas = tierra.EnableStuckParticleDetection
                e.FactorDePegado = tierra.StuckParticlesStretchFactorSq
            End If

            ' ⛔ sin `doNormals` no hay normales que actualizar: se dejan en Nothing y el paso 5
            ' ni entra (0x14195CEB0 se llama solo si `data+0x14C`).
            If sim.DoNormals Then
                e.IndicesDeTriangulo = AEnteros(sim.TriangleIndices)     ' data+0x58
                e.FlipsDeTriangulo = ABytesDeEnteros(sim.TriangleFlips)  ' data+0x68
            End If
            Return e
        End Function


        ''' <summary>Una lista de enteros del archivo, a bytes.</summary>
        Private Function ABytes(l As List(Of Integer)) As Byte()
            If l Is Nothing Then Return Array.Empty(Of Byte)()
            Dim r(l.Count - 1) As Byte
            For i = 0 To l.Count - 1
                r(i) = CByte(l(i) And &HFF)
            Next
            Return r
        End Function

        ''' <summary>Una lista de reales del archivo, a arreglo.</summary>
        Private Function ASingles(l As List(Of Single)) As Single()
            If l Is Nothing Then Return Array.Empty(Of Single)()
            Return l.ToArray()
        End Function

        ''' <summary>
        ''' ⭐ Un `hclObjectSpaceSkinPNOperator` del archivo, compilado.
        ''' <para>El LAYOUT — que vertice sale de que bloque, de que familia y de que carril — lo resuelve
        ''' `HclRenderGraphParser_Class`, que es la pieza del FORMATO y ya la usa el resto del arbol.
        ''' La ARITMETICA no: la desquantizacion se rehace aca desde los `int16` crudos con la ley del
        ''' `.exe` (`Piel.Desquantizar`), porque el parser divide por la escala en DOBLE y el motor mete
        ''' el entero en la mitad alta y multiplica en float. Son numeros distintos.</para>
        ''' <para>⚠ Dos decisiones del parser que NO salen del motor y quedan anotadas: corta el bloque
        ''' cuando un slot repite el `vertexIndex` del anterior (el relleno del ultimo bloque), y saltea el
        ''' vertice si la `w` de la posicion no da una escala. Si el A/B visual mostrara diferencia, se
        ''' persiguen; hoy no hay medicion que diga que estan mal.</para>
        ''' </summary>
        Friend Function PielDe(g As HkxObjectGraph_Class, crudo As HkxVirtualObjectGraph_Class) As PielCompilada
            If g Is Nothing OrElse crudo Is Nothing Then Return Nothing
            Return PielDeGrafo(HclRenderGraphParser_Class.ParseObjectSpaceSkinOperator(g, crudo))
        End Function

        ''' <summary>La piel de un grafo ya parseado — las cuatro variantes entran por acá.</summary>
        Friend Function PielDeGrafo(graf As HclObjectSpaceSkinPNOperatorGraph_Class) As PielCompilada
            If graf Is Nothing Then Return Nothing

            ' (a) las matrices `boneFromSkinMeshTransforms` (+0x20), una por hueso
            Dim hm = graf.HuesosDesdeMalla
            Dim nb = If(hm Is Nothing, 0, hm.Count)
            Dim bfm(Math.Max(0, nb - 1)) As Mat4
            For i = 0 To nb - 1
                bfm(i) = M4(hm(i))
            Next

            Dim n = graf.Vertices.Count
            Dim vert(Math.Max(0, n - 1)) As Integer
            Dim hue(Math.Max(0, n * PielCompilada.MaxInfluencias - 1)) As Integer
            Dim pes(Math.Max(0, n * PielCompilada.MaxInfluencias - 1)) As Single
            Dim pl(Math.Max(0, n * 4 - 1)) As Single
            Dim nl(Math.Max(0, n * 4 - 1)) As Single
            ' ⛔ Los canales que el bloque NO trae quedan VACIOS, no en ceros: `Deformar` comprueba
            ' `Length > 0`, igual que el motor comprueba que el puntero del canal no sea nulo antes
            ' de tocarlo (`0x14190A216`, `0x14190A23B`).
            Dim tl = If(graf.Canales >= 3, New Single(Math.Max(0, n * 4 - 1)) {}, Array.Empty(Of Single)())
            Dim bl = If(graf.Canales >= 4, New Single(Math.Max(0, n * 4 - 1)) {}, Array.Empty(Of Single)())

            For i = 0 To n - 1
                Dim v = graf.Vertices(i)
                vert(i) = CInt(v.VertexIndex)
                Dim k = 0
                While k < PielCompilada.MaxInfluencias
                    If k < v.TransformIndices.Count Then
                        hue(i * PielCompilada.MaxInfluencias + k) = CInt(v.TransformIndices(k))
                        ' ⛔ la familia de UNA influencia no trae pesos: el motor no lee el array y
                        ' el peso es 1. Las otras traen `uint8` × 0,00392156886 (0x142492850).
                        If v.WeightBytes.Count = 0 Then
                            pes(i * PielCompilada.MaxInfluencias + k) = 1.0F
                        ElseIf k < v.WeightBytes.Count Then
                            pes(i * PielCompilada.MaxInfluencias + k) = CSng(v.WeightBytes(k)) * Piel.PorPeso
                        End If
                    End If
                    k += 1
                End While
                Simd.Escribir(pl, i, CrudoDesquantizado(v.Position))
                Simd.Escribir(nl, i, CrudoDesquantizado(v.Normal))
                If tl.Length > 0 Then Simd.Escribir(tl, i, CrudoDesquantizado(v.Tangent))
                If bl.Length > 0 Then Simd.Escribir(bl, i, CrudoDesquantizado(v.BiTangent))
            Next

            Return New PielCompilada(graf.BufferDeSalida, graf.IndiceDelTransformSet,
                                     bfm, AEnteros(graf.Subconjunto), vert, hue, pes, pl, nl,
                                     graf.Canales, tl, bl)
        End Function

        ''' <summary>
        ''' ⭐ `hclBoneSpaceSkin*Operator` — las cuatro variantes (types 18 a 21), compiladas.
        ''' <para>⛔ El desentrelazado se hace ACÁ y no en `HclRenderGraphParser_Class` a propósito:
        ''' el de espacio-OBJETO vive allá porque lo comparten cinco consumidores (el package parser,
        ''' el audit, el render). El de espacio de HUESO no lo consume nadie más, y meterlo en la capa
        ''' del formato sería una segunda sede de una ley que sólo el motor usa.</para>
        ''' <para>⛔ El empaquetado, de la tabla de layout:</para>
        ''' <para>· `four`  → `vertexIndices uint16[4]` @0x00 · `boneIndices uint16[16]` @0x08</para>
        ''' <para>· `three` → `vertexIndices uint16[5]` @0x00 · `boneIndices uint16[15]` @0x0A</para>
        ''' <para>· `two`   → `vertexIndices uint16[8]` @0x00 · `boneIndices uint16[16]` @0x10</para>
        ''' <para>· `one`   → `vertexIndices uint16[16]` @0x00 · `boneIndices uint16[16]` @0x20</para>
        ''' <para>⛔⛔ Y **NO HAY ARRAY DE PESOS**: el peso viene HORNEADO en `localPosition`, que es
        ''' un `vector4` CRUDO cuya `w` multiplica la fila 3 de la matriz de hueso (`0x141925550`).
        ''' Por eso los bloques de entrada no lo traen y por eso el deform usa cuatro términos.</para>
        ''' <para>⛔ El bloque local tiene **16 casillas** y se consumen EN ORDEN, agrupadas por
        ''' vértice: el vértice `i` se lleva sus `n` influencias seguidas (`0x1419255B6`: la posición
        ''' avanza 0x10 por influencia y la normal 8). `localNormal` / `Tangent` / `BiTangent` son
        ''' `int16[64]` con la MISMA desquantización que el espacio-objeto.</para>
        ''' <para>⚠️ El corpus vanilla trae **cero** de las cuatro. Va igual: es de la lista cerrada.</para>
        ''' </summary>
        Friend Function PielDeHuesoDe(g As HkxObjectGraph_Class,
                                      crudo As HkxVirtualObjectGraph_Class) As PielDeHuesoCompilada
            If g Is Nothing OrElse crudo Is Nothing Then Return Nothing

            Dim d As HkObj_HclBoneSpaceDeformer = Nothing
            Dim subset As List(Of Integer) = Nothing
            Dim bufSalida = 0, tsIdx = 0, canales = 0
            Dim nombre As String = Nothing
            Dim pos As New List(Of List(Of Single()))()
            Dim canal As New List(Of IReadOnlyList(Of Integer)())()

            Dim oPn = HkObj_HclBoneSpaceSkinPNOperator.Leer(g, crudo)
            If oPn IsNot Nothing Then
                d = oPn.BoneSpaceDeformer : subset = oPn.TransformSubset
                bufSalida = CInt(oPn.OutputBufferIndex) : tsIdx = CInt(oPn.TransformSetIndex)
                canales = 2 : nombre = oPn.Name
                If oPn.LocalPNs IsNot Nothing Then
                    For Each b In oPn.LocalPNs
                        If b Is Nothing Then Continue For
                        pos.Add(b.LocalPosition)
                        canal.Add(New IReadOnlyList(Of Integer)() {b.LocalNormal})
                    Next
                End If
            Else
                Dim oP = HkObj_HclBoneSpaceSkinPOperator.Leer(g, crudo)
                If oP IsNot Nothing Then
                    d = oP.BoneSpaceDeformer : subset = oP.TransformSubset
                    bufSalida = CInt(oP.OutputBufferIndex) : tsIdx = CInt(oP.TransformSetIndex)
                    canales = 1 : nombre = oP.Name
                    If oP.LocalPs IsNot Nothing Then
                        For Each b In oP.LocalPs
                            If b Is Nothing Then Continue For
                            pos.Add(b.LocalPosition)
                            canal.Add(Array.Empty(Of IReadOnlyList(Of Integer))())
                        Next
                    End If
                Else
                    Dim oPnt = HkObj_HclBoneSpaceSkinPNTOperator.Leer(g, crudo)
                    If oPnt IsNot Nothing Then
                        d = oPnt.BoneSpaceDeformer : subset = oPnt.TransformSubset
                        bufSalida = CInt(oPnt.OutputBufferIndex) : tsIdx = CInt(oPnt.TransformSetIndex)
                        canales = 3 : nombre = oPnt.Name
                        If oPnt.LocalPNTs IsNot Nothing Then
                            For Each b In oPnt.LocalPNTs
                                If b Is Nothing Then Continue For
                                pos.Add(b.LocalPosition)
                                canal.Add(New IReadOnlyList(Of Integer)() {b.LocalNormal, b.LocalTangent})
                            Next
                        End If
                    Else
                        Dim oPntb = HkObj_HclBoneSpaceSkinPNTBOperator.Leer(g, crudo)
                        If oPntb Is Nothing Then Return Nothing
                        d = oPntb.BoneSpaceDeformer : subset = oPntb.TransformSubset
                        bufSalida = CInt(oPntb.OutputBufferIndex) : tsIdx = CInt(oPntb.TransformSetIndex)
                        canales = 4 : nombre = oPntb.Name
                        If oPntb.LocalPNTBs IsNot Nothing Then
                            For Each b In oPntb.LocalPNTBs
                                If b Is Nothing Then Continue For
                                pos.Add(b.LocalPosition)
                                canal.Add(New IReadOnlyList(Of Integer)() {
                                    b.LocalNormal, b.LocalTangent, b.LocalBiTangent})
                            Next
                        End If
                    End If
                End If
            End If
            If d Is Nothing Then Return Nothing

            ' familia -> (vertices por bloque, influencias por vertice)
            Dim forma = New Integer()() {New Integer() {4, 4}, New Integer() {5, 3},
                                         New Integer() {8, 2}, New Integer() {16, 1}}
            Dim control = If(d.ControlBytes, New List(Of Integer)())
            Dim tomados(3) As Integer

            Dim vert As New List(Of Integer)(), hue As New List(Of Integer)()
            Dim cuantas As New List(Of Integer)()
            Dim pl As New List(Of Single)(), nl As New List(Of Single)()
            Dim tl As New List(Of Single)(), bl As New List(Of Single)()

            Dim nBloques = Math.Max(control.Count, pos.Count)
            For iB = 0 To nBloques - 1
                Dim fam = If(iB < control.Count, control(iB) And &HFF, -1)
                If fam < 0 OrElse fam > 3 Then Continue For
                Dim nv = forma(fam)(0), ni = forma(fam)(1)
                Dim ent = EntradaDeHueso(d, fam, tomados(fam))
                tomados(fam) += 1
                If ent Is Nothing OrElse iB >= pos.Count Then Continue For
                Dim lp = pos(iB), lc = canal(iB)

                Dim slot = 0
                For k = 0 To nv - 1
                    If ent.Vertices Is Nothing OrElse k >= ent.Vertices.Count Then Exit For
                    vert.Add(ent.Vertices(k) And &HFFFF)
                    Dim n = 0
                    For inf = 0 To PielDeHuesoCompilada.MaxInfluencias - 1
                        If inf < ni AndAlso ent.Huesos IsNot Nothing AndAlso
                           k * ni + inf < ent.Huesos.Count AndAlso slot < 16 Then
                            hue.Add(ent.Huesos(k * ni + inf) And &HFFFF)
                            VolcarVector4(pl, lp, slot)
                            ' ⛔ el canal 0 es la NORMAL, el 1 la TANGENTE y el 2 la BITANGENTE, y
                            ' los tres van desquantizados con la MISMA ley del `.exe`
                            ' (`0x141928C21`/`53` usan el mismo `punpcklwd` + `cvtdq2ps` + `mulps`).
                            If lc.Length > 0 Then
                                VolcarDesquantizado(nl, lc(0), slot)
                            Else
                                VolcarCero(nl)
                            End If
                            If lc.Length > 1 Then
                                VolcarDesquantizado(tl, lc(1), slot)
                            Else
                                VolcarCero(tl)
                            End If
                            If lc.Length > 2 Then
                                VolcarDesquantizado(bl, lc(2), slot)
                            Else
                                VolcarCero(bl)
                            End If
                            slot += 1
                            n += 1
                        Else
                            hue.Add(-1)
                            VolcarCero(pl)
                            VolcarCero(nl)
                            VolcarCero(tl)
                            VolcarCero(bl)
                        End If
                    Next
                    cuantas.Add(n)
                Next
            Next

            Return New PielDeHuesoCompilada(bufSalida, tsIdx, AEnteros(subset), vert.ToArray(),
                                            hue.ToArray(), cuantas.ToArray(), pl.ToArray(),
                                            nl.ToArray(), canales, tl.ToArray(), bl.ToArray())
        End Function

        ''' <summary>Las entradas `{vertexIndices, boneIndices}` de una familia y un bloque.</summary>
        Private Function EntradaDeHueso(d As HkObj_HclBoneSpaceDeformer, familia As Integer,
                                        i As Integer) As EntradaDeHuesoLeida
            Select Case familia
                Case 0
                    If d.FourBlendEntries Is Nothing OrElse i >= d.FourBlendEntries.Count Then Return Nothing
                    Return New EntradaDeHuesoLeida(d.FourBlendEntries(i).VertexIndices,
                                                   d.FourBlendEntries(i).BoneIndices)
                Case 1
                    If d.ThreeBlendEntries Is Nothing OrElse i >= d.ThreeBlendEntries.Count Then Return Nothing
                    Return New EntradaDeHuesoLeida(d.ThreeBlendEntries(i).VertexIndices,
                                                   d.ThreeBlendEntries(i).BoneIndices)
                Case 2
                    If d.TwoBlendEntries Is Nothing OrElse i >= d.TwoBlendEntries.Count Then Return Nothing
                    Return New EntradaDeHuesoLeida(d.TwoBlendEntries(i).VertexIndices,
                                                   d.TwoBlendEntries(i).BoneIndices)
                Case Else
                    If d.OneBlendEntries Is Nothing OrElse i >= d.OneBlendEntries.Count Then Return Nothing
                    Return New EntradaDeHuesoLeida(d.OneBlendEntries(i).VertexIndices,
                                                   d.OneBlendEntries(i).BoneIndices)
            End Select
        End Function

        ''' <summary>Un `vector4` del bloque local, tal cual (CRUDO: la `w` trae el peso).</summary>
        Private Sub VolcarVector4(destino As List(Of Single), fuente As List(Of Single()), slot As Integer)
            Dim v = If(fuente Is Nothing OrElse slot >= fuente.Count, Nothing, fuente(slot))
            For k = 0 To 3
                destino.Add(If(v Is Nothing OrElse k >= v.Length, 0.0F, v(k)))
            Next
        End Sub

        ''' <summary>Un canal `int16[64]` del bloque local, desquantizado con la ley del `.exe`.</summary>
        Private Sub VolcarDesquantizado(destino As List(Of Single), fuente As IReadOnlyList(Of Integer),
                                        slot As Integer)
            Dim q = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(fuente, slot \ 2, slot Mod 2)
            If q.Length < 4 Then
                VolcarCero(destino)
                Return
            End If
            Dim v = Piel.Desquantizar(q(0), q(1), q(2), q(3))
            destino.Add(Simd.Lane0(v))
            destino.Add(v.GetElement(1))
            destino.Add(v.GetElement(2))
            destino.Add(0.0F)
        End Sub

        Private Sub VolcarCero(destino As List(Of Single))
            For k = 0 To 3
                destino.Add(0.0F)
            Next
        End Sub

        ''' <summary>El `name` del operador de espacio de hueso, sea cual sea la variante.</summary>
        Private Function NombreDeHueso(g As HkxObjectGraph_Class,
                                       crudo As HkxVirtualObjectGraph_Class) As String
            Dim a = HkObj_HclBoneSpaceSkinPNOperator.Leer(g, crudo)
            If a IsNot Nothing Then Return a.Name
            Dim b = HkObj_HclBoneSpaceSkinPOperator.Leer(g, crudo)
            If b IsNot Nothing Then Return b.Name
            Dim c = HkObj_HclBoneSpaceSkinPNTOperator.Leer(g, crudo)
            If c IsNot Nothing Then Return c.Name
            Dim e = HkObj_HclBoneSpaceSkinPNTBOperator.Leer(g, crudo)
            If e IsNot Nothing Then Return e.Name
            Return Nothing
        End Function

        ''' <summary>
        ''' `hclObjectSpaceMeshMeshDeform*Operator` — types 30 a 33.
        ''' <para>⛔ El deformer es el MISMO `hclObjectSpaceDeformer` que la piel, asi que se compila
        ''' con el mismo parser; lo distinto es que sus indices son de TRIANGULO y que las matrices
        ''' salen de `triangleFromMeshTransforms` compuesto con el marco y con la matriz de buffer.</para>
        ''' </summary>
        Private Function MallaAMallaEspacioObjetoDe(g As HkxObjectGraph_Class,
                                                    crudo As HkxVirtualObjectGraph_Class) As OperadorCompilado
            Dim ent = 0, esc = 0
            Dim sub_ As List(Of Integer) = Nothing
            Dim tdm As List(Of Single()) = Nothing
            Dim nombre As String = Nothing
            Dim oPn = HkObj_HclObjectSpaceMeshMeshDeformPNOperator.Leer(g, crudo)
            If oPn IsNot Nothing Then
                ent = CInt(oPn.InputBufferIdx) : sub_ = oPn.InputTrianglesSubset
                tdm = oPn.TriangleFromMeshTransforms
                esc = CInt(oPn.ScaleNormalBehaviour) : nombre = oPn.Name
            Else
                Dim oP = HkObj_HclObjectSpaceMeshMeshDeformPOperator.Leer(g, crudo)
                If oP IsNot Nothing Then
                    ent = CInt(oP.InputBufferIdx) : sub_ = oP.InputTrianglesSubset
                    tdm = oP.TriangleFromMeshTransforms
                    esc = CInt(oP.ScaleNormalBehaviour) : nombre = oP.Name
                Else
                    Dim oT = HkObj_HclObjectSpaceMeshMeshDeformPNTOperator.Leer(g, crudo)
                    If oT IsNot Nothing Then
                        ent = CInt(oT.InputBufferIdx) : sub_ = oT.InputTrianglesSubset
                        tdm = oT.TriangleFromMeshTransforms
                        esc = CInt(oT.ScaleNormalBehaviour) : nombre = oT.Name
                    Else
                        Dim oB = HkObj_HclObjectSpaceMeshMeshDeformPNTBOperator.Leer(g, crudo)
                        If oB Is Nothing Then Return Nothing
                        ent = CInt(oB.InputBufferIdx) : sub_ = oB.InputTrianglesSubset
                        tdm = oB.TriangleFromMeshTransforms
                        esc = CInt(oB.ScaleNormalBehaviour) : nombre = oB.Name
                    End If
                End If
            End If
            Dim piel = PielDeGrafo(HclRenderGraphParser_Class.ParseObjectSpaceSkinOperator(g, crudo))
            If piel Is Nothing Then Return Nothing
            Return New OpMallaAMallaEnEspacioObjeto(ent, MatricesDe(tdm), AEnteros(sub_), esc, piel, nombre)
        End Function

        ''' <summary>
        ''' `hclBoneSpaceMeshMeshDeform*Operator` — types 26 a 29.
        ''' <para>⛔ Esta familia **NO declara `triangleFromMeshTransforms`**: su compuesta es solo
        ''' `Componer(marco[t], M)`. No es simetrica con la de espacio-objeto.</para>
        ''' </summary>
        Private Function MallaAMallaEspacioDeHuesoDe(g As HkxObjectGraph_Class,
                                                     crudo As HkxVirtualObjectGraph_Class) As OperadorCompilado
            Dim ent = 0, esc = 0
            Dim sub_ As List(Of Integer) = Nothing
            Dim nombre As String = Nothing
            Dim oPn = HkObj_HclBoneSpaceMeshMeshDeformPNOperator.Leer(g, crudo)
            If oPn IsNot Nothing Then
                ent = CInt(oPn.InputBufferIdx) : sub_ = oPn.InputTrianglesSubset
                esc = CInt(oPn.ScaleNormalBehaviour) : nombre = oPn.Name
            Else
                Dim oP = HkObj_HclBoneSpaceMeshMeshDeformPOperator.Leer(g, crudo)
                If oP IsNot Nothing Then
                    ent = CInt(oP.InputBufferIdx) : sub_ = oP.InputTrianglesSubset
                    esc = CInt(oP.ScaleNormalBehaviour) : nombre = oP.Name
                Else
                    Dim oT = HkObj_HclBoneSpaceMeshMeshDeformPNTOperator.Leer(g, crudo)
                    If oT IsNot Nothing Then
                        ent = CInt(oT.InputBufferIdx) : sub_ = oT.InputTrianglesSubset
                        esc = CInt(oT.ScaleNormalBehaviour) : nombre = oT.Name
                    Else
                        Dim oB = HkObj_HclBoneSpaceMeshMeshDeformPNTBOperator.Leer(g, crudo)
                        If oB Is Nothing Then Return Nothing
                        ent = CInt(oB.InputBufferIdx) : sub_ = oB.InputTrianglesSubset
                        esc = CInt(oB.ScaleNormalBehaviour) : nombre = oB.Name
                    End If
                End If
            End If
            Dim piel = PielDeHuesoDe(g, crudo)
            If piel Is Nothing Then Return Nothing
            Return New OpMallaAMallaEnEspacioDeHueso(ent, AEnteros(sub_), esc, piel, nombre)
        End Function

        ''' <summary>
        ''' `hclRuntimeConversionInfo` → la lista de ternas que el bucle recorre.
        ''' <para>⛔ Se recorta a `numElementsConverted` (`0x14195E88E`, la comparación contra
        ''' `[op+0x51]`), y `slotConversions` **no entra**: ninguno de los dos `execute` lo lee.</para>
        ''' </summary>
        Private Function ConversionDe(usuario As Integer, sombra As Integer,
                                      info As HkObj_HclRuntimeConversionInfo) As ConversionCompilada
            If info Is Nothing Then Return New ConversionCompilada(usuario, sombra, Nothing)
            Dim lista = info.ElementConversions
            Dim cuantas = Math.Max(0, Math.Min(info.NumElementsConverted,
                                               If(lista Is Nothing, 0, lista.Count)))
            Dim r(Math.Max(0, cuantas - 1)) As ElementoDeConversion
            For k = 0 To cuantas - 1
                r(k).Canal = lista(k).Index
                r(k).Offset = lista(k).Offset
                r(k).Conversion = lista(k).Conversion
            Next
            If cuantas = 0 Then Return New ConversionCompilada(usuario, sombra, Nothing)
            Return New ConversionCompilada(usuario, sombra, r)
        End Function

        ''' <summary>La desquantizacion CANONICA desde los `int16` que guardo el parser. Si el vector no
        ''' trae los cuatro enteros, va en cero — el motor tampoco tendria de donde sacarlos.</summary>
        Private Function CrudoDesquantizado(q As HclObjectSpaceSkinQuantizedVectorGraph_Class) As Vector128(Of Single)
            If q Is Nothing OrElse q.RawInt16Values Is Nothing OrElse q.RawInt16Values.Count < 4 Then
                Return Vector128(Of Single).Zero
            End If
            Return Piel.Desquantizar(q.RawInt16Values(0), q.RawInt16Values(1),
                                     q.RawInt16Values(2), q.RawInt16Values(3))
        End Function


        ''' <summary>
        ''' ⭐⭐ LOS BUFFERS DE LA PRENDA — `hclInstantiationUtil::createBuffers` (`0x1418ED2D0`).
        ''' <para>Recorre `clothData.bufferDefinitions` (+0x28/+0x30) y despacha por
        ''' `hclBufferDefinition.type` (+0x18), con la tabla de 8 entradas de `0x1418ED6E0`:</para>
        ''' <para>⭐⭐⭐ **type 1** — el creador `0x1418C7040` lo arma DESDE el `hclSimClothInstance`:
        ''' `buf[+0x10] = simCloth[+0x18]`, o sea **el MISMO puntero de posiciones de las particulas**. Lo
        ''' que un operador escriba en ese buffer ES la posicion de la particula; no hay copia. El
        ''' proveedor sale de `[ctx+0x20][bd.subType(+0x1C)]`, que con un solo sim-cloth es el sim-cloth.</para>
        ''' <para>**types 6, 7 y 8** — buffer propio del motor, con su `numVertices`.</para>
        ''' <para>**type 3** — assert del motor: «Unknown buffer type. Can't instantiate cloth.»
        ''' (`hclinstantiationutil.cpp:342`). Aca revienta igual, que es lo que corresponde.</para>
        ''' <para>⛔ **Cada buffer tiene su PROPIO espacio** y el constructor base (`0x1418ED150`) deja las
        ''' dos matrices (+0x80 y +0xC0) en IDENTIDAD. Tratarlos como si compartieran espacio es una
        ''' invencion que rompe todo aguas abajo; por eso `CopyVertices` y `MoveParticles` componen.</para>
        ''' <para>Censo del corpus (M11, 759 prendas): 759 `hclBufferDefinition` con `type = 1` y 762
        ''' `hclScratchBufferDefinition` con `type = 6`. Ningun otro tipo aparece.</para>
        ''' </summary>
        Friend Function BuffersDe(cd As HkObj_HclClothData, inst As Instancia) As Buffer()
            If cd Is Nothing Then Return Array.Empty(Of Buffer)()
            ' ⛔⛔ SE RECORRE EL ARRAY CRUDO, NO LA LISTA TIPADA. `bufferDefinitions` mezcla
            ' `hclBufferDefinition` y `hclScratchBufferDefinition`, y las listas tipadas COMPACTAN:
            ' con un scratch delante, todos los indices que los operadores usan se corren uno.
            ' Medido en el vestido: `[0]` es CLOTH_SIM, un scratch de 340 vertices, y el skin escribe
            ' justo ahi.
            Dim n = cd.Raw.BufferDefinitionsCount
            If n <= 0 Then Return Array.Empty(Of Buffer)()
            Dim g = cd.Graph
            Dim r(n - 1) As Buffer
            For i = 0 To n - 1
                Dim crudo = cd.Raw.BufferDefinitionsRef(i)
                If crudo Is Nothing Then Continue For

                Dim scratch = HkObj_HclScratchBufferDefinition.Leer(g, crudo)
                Dim bd = If(scratch IsNot Nothing, Nothing, HkObj_HclBufferDefinition.Leer(g, crudo))
                Dim t = CInt(If(scratch IsNot Nothing, scratch.Type, If(bd Is Nothing, -1, bd.Type)))

                Select Case t
                    Case 1, 2
                        ' ⭐⭐⭐ `0x1418C7040`: el buffer se arma DESDE el `hclSimClothInstance` y
                        ' `buf[+0x10] = simCloth[+0x18]`, o sea el MISMO array de posiciones. Lo que un
                        ' operador escriba aca ES la posicion de la particula.
                        Buffers.PonerBuffer(r, i, Buffers.CrearDelSimCloth(inst))
                    Case 3
                        ' ⛔ el motor ASSERTEA aca. No se inventa un buffer vacio para seguir.
                        Throw New InvalidOperationException(
                            $"BuffersDe: `hclBufferDefinition.type` = 3 en la ranura {i}. El motor assertea " &
                            "(«Unknown buffer type. Can't instantiate cloth.», hclinstantiationutil.cpp:342).")
                    Case Else
                        Dim nv = CInt(If(scratch IsNot Nothing, scratch.NumVertices, If(bd Is Nothing, 0UI, bd.NumVertices)))
                        Dim b As New Buffer() With {.Cuenta = nv, .LayoutSimple = True, .StrideBytes = 16,
                                                    .LayoutSimpleNormales = True, .StrideNormalesBytes = 16}
                        ReDim b.Datos(Math.Max(0, nv * 4 - 1))
                        ReDim b.Normales(Math.Max(0, nv * 4 - 1))
                        ' ⭐ LOS OTROS DOS CANALES SALEN DE LO QUE EL SCRATCH DECLARA, no de una
                        ' suposicion: `hclScratchBufferDefinition.storeTangentsAndBiTangents`. Un canal
                        ' que el archivo no pide queda en `Nothing`, que es lo que el motor comprueba
                        ' antes de escribirlo (`0x14190A216`, `0x14190A23B`).
                        If scratch IsNot Nothing AndAlso scratch.StoreTangentsAndBiTangents Then
                            b.StrideTangentesBytes = 16 : b.LayoutSimpleTangentes = True
                            b.StrideBitangentesBytes = 16 : b.LayoutSimpleBitangentes = True
                            ReDim b.Tangentes(Math.Max(0, nv * 4 - 1))
                            ReDim b.Bitangentes(Math.Max(0, nv * 4 - 1))
                        End If
                        ' ⛔ EL SCRATCH TRAE SUS PROPIOS TRIANGULOS, y `hclSimpleMeshBoneDeformOperator`
                        ' los necesita para armar el marco: sin ellos lee un array vacio.
                        If scratch IsNot Nothing AndAlso scratch.TriangleIndices IsNot Nothing AndAlso
                           scratch.TriangleIndices.Count >= 3 Then
                            Dim ti = scratch.TriangleIndices
                            Dim tt(ti.Count - 1) As UShort
                            For k = 0 To ti.Count - 1
                                tt(k) = CUShort(ti(k) And &HFFFF)
                            Next
                            b.IndicesDeTriangulo = tt
                            b.NumTriangulos = ti.Count \ 3
                        End If
                        Buffers.PonerBuffer(r, i, b)
                End Select
            Next
            Return r
        End Function


        ''' <summary>
        ''' ⭐ Los `hclAntiPinchConstraintSet` — tipo 19, de `antiPinchConstraintSets` (+0xC8).
        ''' <para>⛔ ES OTRA LISTA que `staticConstraintSets`: `hclSimClothData` tiene las dos y el
        ''' motor las recorre por separado. El AntiPinch solo vive en la segunda, asi que ingerirlo
        ''' desde la primera no lo encontraria nunca.</para>
        ''' <para>El corpus vanilla trae CERO (medido en las 759 prendas); esta igual porque un mod
        ''' puede traerlo.</para>
        ''' </summary>
        Friend Function AntiPellizcosDe(sim As HkObj_HclSimClothData) As SetCompilado()
            If sim Is Nothing Then Return Array.Empty(Of SetCompilado)()
            Dim crudos = Havok.Canon.HavokConstraintSets.Crudos(sim, Havok.Canon.HavokConstraintSets.Fuente.AntiPinch)
            If crudos Is Nothing OrElse crudos.Count = 0 Then Return Array.Empty(Of SetCompilado)()
            Dim r(crudos.Count - 1) As SetCompilado
            Dim g = sim.Graph
            For i = 0 To crudos.Count - 1
                Dim crudo = crudos(i).Bloque
                If crudo Is Nothing Then Continue For
                Dim o = HkObj_HclAntiPinchConstraintSet.Leer(g, crudo)
                If o IsNot Nothing Then
                    r(i) = New AntiPellizco(o, 19)
                ElseIf Logger.Enabled Then
                    Dim cq = If(crudo.ClassName, "?"), iq = i
                    Logger.LogLazy(Function() $"[MOTOR-SETS] ⛔ hueco en antiPinch: el set #{iq} es `{cq}` y este motor no lo transcribe")
                End If
            Next
            Return r
        End Function

        ''' <summary>
        ''' ⭐⭐ LA CADENA DE UN `hclClothState`, en el ORDEN DEL ARCHIVO.
        ''' <para>`hclClothState.operators` (+0x18) es un arreglo de `uint32` con el INDICE del operador
        ''' dentro de `hclClothData.operators` — no un orden de aparicion ni una receta. El motor lo
        ''' recorre y despacha por clase.</para>
        ''' <para>⛔ El despacho va por CLASE, no por `hclOperator.type` (+0x18): ese campo esta marcado
        ''' `SERIALIZE_IGNORED` y viene en CERO en las 3.798 apariciones del corpus. Los `type` del cap.
        ''' 2.1 se guardan igual en cada `OperadorCompilado`, porque son la identidad de la clase.</para>
        ''' <para>Una clase que este motor no transcribe deja su lugar VACIO y se dice en el log; el censo
        ''' M1 midio que el corpus vanilla no trae ninguna otra.</para>
        ''' </summary>
        Friend Function CadenaDe(cd As HkObj_HclClothData, estado As HkObj_HclClothState) As OperadorCompilado()
            If cd Is Nothing OrElse estado Is Nothing OrElse estado.Operators Is Nothing Then
                Return Array.Empty(Of OperadorCompilado)()
            End If
            Dim g = cd.Graph
            ' ⚠ ACA `.Count` SI ES LA COTA BUENA, y conviene decir por que: `[Operators]` es una lista
            ' de PRIMITIVAS (`List(Of UInteger)`) y el generador la llena con
            ' `For i = 0 To h.Count - 1 : Add(ReadUInt32(...))`, sin filtro — no compacta. Las que SI
            ' compactan son las de OBJETOS, donde un puntero que no resuelve no entra: por eso
            ' `BuffersDe` recorre el arreglo CRUDO. `HavokLayoutGate` marca este uso; es un falso
            ' positivo de su ley, no un hueco.
            Dim n = estado.Operators.Count
            If n <= 0 Then Return Array.Empty(Of OperadorCompilado)()
            Dim r(n - 1) As OperadorCompilado
            For i = 0 To n - 1
                Dim idx = CInt(estado.Operators(i))
                If idx < 0 OrElse idx >= cd.Raw.OperatorsCount Then Continue For
                Dim crudo = cd.Raw.OperatorsRef(idx)
                If crudo Is Nothing Then Continue For
                r(i) = OperadorDe(g, crudo)
                If r(i) Is Nothing AndAlso Logger.Enabled Then
                    Dim cq = If(crudo.ClassName, "?"), iq = idx
                    Logger.LogLazy(Function() $"[MOTOR-OPS] ⛔ hueco: el operador #{iq} es `{cq}` y este motor no lo transcribe")
                End If
            Next
            Return r
        End Function

        ''' <summary>Un operador suelto, por su clase. `Nothing` si este motor no la transcribe.</summary>
        Friend Function OperadorDe(g As HkxObjectGraph_Class, crudo As HkxVirtualObjectGraph_Class) As OperadorCompilado
            ' ⛔ LAS CUATRO VARIANTES (tipos 22 a 25). El corpus vanilla usa SOLO `PN` (762
            ' apariciones, cero de las otras tres), pero un mod puede traer cualquiera y hasta hoy
            ' caian en el hueco: el operador quedaba sin transcribir y la malla no se deformaba.
            ' ⭐ LAS CUATRO VARIANTES DE PIEL, en una sola prueba: el parser las acepta a las
            ' cuatro. Antes se probaban una por una y para tres de ellas se llamaba a
            ' `PielDeVariante`, que terminaba en el parser de `PN` y devolvia Nothing — caian en el
            ' hueco igual, con una linea de log diciendo que se soportaban.
            Dim grafPiel = HclRenderGraphParser_Class.ParseObjectSpaceSkinOperator(g, crudo)
            If grafPiel IsNot Nothing Then
                Dim p = PielDeGrafo(grafPiel)
                If p Is Nothing Then Return Nothing
                Return New OpPiel(p, grafPiel.Nombre)
            End If

            ' ⭐⭐ `hclInputConvertOperator` (14) y `hclOutputConvertOperator` (15) — los dos
            ' únicos que hablan con el buffer del USUARIO. ⛔ Su índice de buffer es DIRECTO
            ' (`0x1418C6242`/`46`), sin el `buffers[buffers[i].Ranura]` del resto.
            Dim oCIn = HkObj_HclInputConvertOperator.Leer(g, crudo)
            If oCIn IsNot Nothing Then
                Return New OpConvertirEntrada(
                    ConversionDe(CInt(oCIn.UserBufferIndex), CInt(oCIn.ShadowBufferIndex),
                                 oCIn.ConversionInfo), oCIn.Name)
            End If
            Dim oCOut = HkObj_HclOutputConvertOperator.Leer(g, crudo)
            If oCOut IsNot Nothing Then
                Return New OpConvertirSalida(
                    ConversionDe(CInt(oCOut.UserBufferIndex), CInt(oCOut.ShadowBufferIndex),
                                 oCOut.ConversionInfo), oCOut.Name)
            End If

            ' ⭐⭐ `hclSkinOperator` — type 8, la piel de PESOS. Es la unica con
            ' `boneInfluences` (peso propio, `uint8`), la unica con camino de CUATERNION DUAL
            ' (`dualQuaternionSkinning`, +0x75, `0x1418C604D`) y la unica que compone las matrices
            ' de hueso con la matriz del buffer de SALIDA.
            Dim oPP = HkObj_HclSkinOperator.Leer(g, crudo)
            If oPP IsNot Nothing Then
                Dim inf = oPP.BoneInfluences
                Dim ni = If(inf Is Nothing, 0, inf.Count)
                Dim hh(Math.Max(0, ni - 1)) As Integer
                Dim ww(Math.Max(0, ni - 1)) As Byte
                For k = 0 To ni - 1
                    hh(k) = inf(k).BoneIndex
                    ww(k) = CByte(inf(k).Weight And &HFF)
                Next
                Dim pp As New PielConPesosCompilada(
                    CInt(oPP.InputBufferIndex), CInt(oPP.OutputBufferIndex),
                    CInt(oPP.TransformSetIndex), hh, ww,
                    AEnteros(oPP.BoneInfluenceStartPerVertex),
                    MatricesDe(oPP.BoneFromSkinMeshTransforms),
                    AEnteros(oPP.UsedBoneGroupIds), oPP.BoneGroupSize,
                    oPP.StartVertex, oPP.EndVertex,
                    oPP.SkinPositions, oPP.SkinNormals, oPP.SkinTangents, oPP.SkinBiTangents,
                    oPP.DualQuaternionSkinning)
                Return New OpPielConPesos(pp, oPP.Name)
            End If

            ' ⛔ `bufferIdx_A/B/C` estan en la tabla de layout (+0x30/+0x34/+0x38) pero el
            ' generador NO los emitio en `HkObj_` — el guion bajo del nombre se los comio. Se leen
            ' por `Raw`, que si los trae.
            Dim oMez = HkObj_HclBlendSomeVerticesOperator.Leer(g, crudo)
            If oMez IsNot Nothing Then
                Dim ent = oMez.BlendEntries
                Dim n = If(ent Is Nothing, 0, ent.Count)
                Dim vv(Math.Max(0, n - 1)) As Integer
                Dim pp(Math.Max(0, n - 1)) As Single
                For k = 0 To n - 1
                    vv(k) = CInt(ent(k).VertexIndex)
                    pp(k) = ent(k).BlendWeight
                Next
                Return New OpMezclarAlgunos(CInt(oMez.Raw.BufferIdx_A), CInt(oMez.Raw.BufferIdx_B),
                                            CInt(oMez.Raw.BufferIdx_C), vv, pp,
                                            oMez.BlendNormals, oMez.BlendTangents,
                                            oMez.BlendBitangents, oMez.Name)
            End If

            Dim oMar = HkObj_HclUpdateAllVertexFramesOperator.Leer(g, crudo)
            If oMar IsNot Nothing Then
                Return New OpMarcosDeVertice(CInt(oMar.BufferIdx),
                                             AEnteros(oMar.VertToNormalID),
                                             ABytes(oMar.TriangleFlips),
                                             AEnteros(oMar.ReferenceVertices),
                                             ASingles(oMar.TangentEdgeCosAngle),
                                             ASingles(oMar.TangentEdgeSinAngle),
                                             ASingles(oMar.BiTangentFlip),
                                             CInt(oMar.NumUniqueNormalIDs),
                                             oMar.UpdateNormals, oMar.UpdateTangents,
                                             oMar.UpdateBiTangents, oMar.Name)
            End If

            Dim oMarAlg = HkObj_HclUpdateSomeVertexFramesOperator.Leer(g, crudo)
            If oMarAlg IsNot Nothing Then
                ' los triangulos vienen como `{uint16[3]}` de 6 B; se aplanan a un arreglo de indices
                Dim tri As New List(Of Integer)()
                If oMarAlg.InvolvedTriangles IsNot Nothing Then
                    For Each t In oMarAlg.InvolvedTriangles
                        If t Is Nothing OrElse t.Indices Is Nothing Then Continue For
                        For k = 0 To Math.Min(2, t.Indices.Count - 1)
                            tri.Add(t.Indices(k))
                        Next
                    Next
                End If
                Return New OpMarcosDeVerticeAlgunos(CInt(oMarAlg.BufferIdx), tri.ToArray(),
                                                    AEnteros(oMarAlg.InvolvedVertices),
                                                    AEnteros(oMarAlg.SelectionVertexToInvolvedVertex),
                                                    AEnteros(oMarAlg.InvolvedVertexToNormalID),
                                                    ABytes(oMarAlg.TriangleFlips),
                                                    AEnteros(oMarAlg.ReferenceVertices),
                                                    ASingles(oMarAlg.TangentEdgeCosAngle),
                                                    ASingles(oMarAlg.TangentEdgeSinAngle),
                                                    ASingles(oMarAlg.BiTangentFlip),
                                                    CInt(oMarAlg.NumUniqueNormalIDs),
                                                    oMarAlg.UpdateNormals, oMarAlg.UpdateTangents,
                                                    oMarAlg.UpdateBiTangents, oMarAlg.Name)
            End If

            Dim oDefH = HkObj_HclMeshBoneDeformOperator.Leer(g, crudo)
            If oDefH IsNot Nothing Then
                Dim pares = oDefH.TriangleBonePairs
                Dim n = If(pares Is Nothing, 0, pares.Count)
                Dim lt(Math.Max(0, n - 1)) As Mat4
                Dim pe(Math.Max(0, n - 1)) As Single
                Dim tr(Math.Max(0, n - 1)) As Integer
                For k = 0 To n - 1
                    lt(k) = M4(pares(k).LocalBoneTransform)
                    pe(k) = pares(k).Weight
                    tr(k) = CInt(pares(k).TriangleIndex)
                Next
                Return New OpDeformarHuesos(CInt(oDefH.InputBufferIdx),
                                            CInt(oDefH.OutputTransformSetIdx),
                                            lt, pe, tr,
                                            AEnteros(oDefH.TriangleBoneStartForBone), oDefH.Name)
            End If

            ' ⭐⭐ `hclMeshMeshDeformOperator` — type 5, `0x1419529F0`.
            ' ⛔ NO es la familia `*MeshMeshDeform*` de 26-33: es una clase aparte, con
            ' `triangleVertexPairs` propios. Ver `MallaAMallaPorPares.vb`.
            Dim oMM = HkObj_HclMeshMeshDeformOperator.Leer(g, crudo)
            If oMM IsNot Nothing Then
                Dim pr = oMM.TriangleVertexPairs
                Dim nP = If(pr Is Nothing, 0, pr.Count)
                Dim pares5(Math.Max(0, nP - 1)) As ParDeVerticeDeTriangulo
                For k = 0 To nP - 1
                    pares5(k).PosicionLocal = V4(pr(k).LocalPosition)
                    pares5(k).NormalLocal = V4(pr(k).LocalNormal)
                    pares5(k).Triangulo = pr(k).TriangleIndex
                    pares5(k).Peso = pr(k).Weight
                Next
                Dim comp As New MallaAMallaPorParesCompilada(
                    AEnteros(oMM.InputTrianglesSubset), pares5,
                    AEnteros(oMM.TriangleVertexStartForVertex),
                    CInt(oMM.InputBufferIdx), CInt(oMM.OutputBufferIdx),
                    oMM.StartVertex, oMM.EndVertex, oMM.ScaleNormalBehaviour,
                    oMM.DeformNormals, oMM.PartialDeform)
                Return New OpMallaAMallaPorPares(comp, oMM.Name)
            End If

            ' ⭐ LAS CUATRO VARIANTES DE PIEL EN ESPACIO DE HUESO (types 18 a 21).
            Dim pHueso = PielDeHuesoDe(g, crudo)
            If pHueso IsNot Nothing Then Return New OpPielDeHueso(pHueso, NombreDeHueso(g, crudo))

            ' ⭐ LAS OCHO DE MESH-MESH (26 a 33). No traen aritmetica nueva: marco de triangulo (el
            ' mismo del type 16) + dos composiciones de TRES terminos + el deform que ya esta
            ' transcrito. ⛔ Y NO son simetricas: la de HUESO no declara `triangleFromMeshTransforms`.
            Dim oMmO = MallaAMallaEspacioObjetoDe(g, crudo)
            If oMmO IsNot Nothing Then Return oMmO
            Dim oMmH = MallaAMallaEspacioDeHuesoDe(g, crudo)
            If oMmH IsNot Nothing Then Return oMmH

            Dim oSim = HkObj_HclSimulateOperator.Leer(g, crudo)
            If oSim IsNot Nothing Then
                ' ⛔ LOS TRES SALEN DEL ARCHIVO, no de un default. `numberOfSolveIterations`
                ' es el lazo del paso 4e (0x141A134F7), `adaptConstraintStiffness` promueve el
                ' modo (0x141A1349B) y `constraintExecution` manda el orden con `-1` = colision
                ' (0x141A13798). Estaban los tres sin leer.
                Return New OpSimular(CInt(oSim.SubSteps), CInt(oSim.SimClothIndex),
                                     oSim.NumberOfSolveIterations,
                                     oSim.AdaptConstraintStiffness,
                                     AEnteros(oSim.ConstraintExecution),
                                     oSim.Name)
            End If

            Dim oMov = HkObj_HclMoveParticlesOperator.Leer(g, crudo)
            If oMov IsNot Nothing Then
                Dim pr = ParesDeParticula(oMov)
                Return New OpMoverParticulas(CInt(oMov.RefBufferIdx), pr, oMov.Name)
            End If

            Dim oDef = HkObj_HclSimpleMeshBoneDeformOperator.Leer(g, crudo)
            If oDef IsNot Nothing Then
                Dim pares = ParesDeTriangulo(oDef)
                Dim lbt = MatricesDe(oDef.LocalBoneTransforms)
                Return New OpDeformar(CInt(oDef.InputBufferIdx), CInt(oDef.OutputTransformSetIdx),
                                      pares, lbt, oDef.Name)
            End If

            Dim oCop = HkObj_HclCopyVerticesOperator.Leer(g, crudo)
            If oCop IsNot Nothing Then
                Return New OpCopiarVertices(CInt(oCop.InputBufferIdx), CInt(oCop.OutputBufferIdx),
                                            CInt(oCop.StartVertexIn), CInt(oCop.StartVertexOut),
                                            CInt(oCop.NumberOfVertices), oCop.CopyNormals, oCop.Name)
            End If

            Dim oGat = HkObj_HclGatherAllVerticesOperator.Leer(g, crudo)
            If oGat IsNot Nothing Then
                Dim m = oGat.VertexInputFromVertexOutput
                Dim src(Math.Max(0, If(m Is Nothing, 0, m.Count) - 1)) As Short
                If m IsNot Nothing Then
                    For k = 0 To m.Count - 1
                        src(k) = CShort(m(k))
                    Next
                End If
                Return New OpJuntarTodos(CInt(oGat.InputBufferIdx), CInt(oGat.OutputBufferIdx),
                                         src, oGat.GatherNormals, oGat.Name)
            End If

            Dim oGs = HkObj_HclGatherSomeVerticesOperator.Leer(g, crudo)
            If oGs IsNot Nothing Then
                Dim n = If(oGs.VertexPairs Is Nothing, 0, oGs.VertexPairs.Count)
                Dim pr(Math.Max(0, n - 1))() As Integer
                For k = 0 To n - 1
                    pr(k) = New Integer() {CInt(oGs.VertexPairs(k).IndexInput),
                                           CInt(oGs.VertexPairs(k).IndexOutput)}
                Next
                Return New OpJuntarAlgunos(CInt(oGs.InputBufferIdx), CInt(oGs.OutputBufferIdx),
                                           pr, oGs.GatherNormals, oGs.Name)
            End If

            Return Nothing
        End Function

        ''' <summary>`hclMoveParticlesOperator.vertexParticlePairs` — el puente vertice del buffer de piel
        ''' a particula. ⛔ El indice de vertice NO es el de particula.</summary>
        Private Function ParesDeParticula(op As HkObj_HclMoveParticlesOperator) As Integer()()
            Dim n = If(op.VertexParticlePairs Is Nothing, 0, op.VertexParticlePairs.Count)
            Dim r(Math.Max(0, n - 1))() As Integer
            For k = 0 To n - 1
                r(k) = New Integer() {CInt(op.VertexParticlePairs(k).VertexIndex),
                                      CInt(op.VertexParticlePairs(k).ParticleIndex)}
            Next
            Return r
        End Function

        ''' <summary>`hclSimpleMeshBoneDeformOperator.triangleBonePairs`. ⛔ Los dos campos son OFFSETS EN
        ''' BYTES, no indices: `DeformarHuesosSimple` divide el del triangulo por 2 para llegar al `u16`.</summary>
        Private Function ParesDeTriangulo(op As HkObj_HclSimpleMeshBoneDeformOperator) As Integer()()
            Dim n = If(op.TriangleBonePairs Is Nothing, 0, op.TriangleBonePairs.Count)
            Dim r(Math.Max(0, n - 1))() As Integer
            For k = 0 To n - 1
                r(k) = New Integer() {CInt(op.TriangleBonePairs(k).BoneOffset),
                                      CInt(op.TriangleBonePairs(k).TriangleOffset)}
            Next
            Return r
        End Function

        ''' <summary>Una lista de matrices del archivo a `Mat4`.</summary>
        Private Function MatricesDe(lista As System.Collections.Generic.IList(Of Single())) As Mat4()
            Dim n = If(lista Is Nothing, 0, lista.Count)
            Dim r(Math.Max(0, n - 1)) As Mat4
            For k = 0 To n - 1
                r(k) = M4(lista(k))
            Next
            Return r
        End Function


        ''' <summary>
        ''' ⭐⭐ LOS CONJUNTOS DE RESTRICCIONES DEL ARCHIVO, COMPILADOS UNA VEZ.
        ''' <para>El `type` de cada clase NO esta en el archivo — el campo `hclConstraintSet.type`
        ''' (+0x18) esta marcado `SERIALIZE_IGNORED` y viene en CERO en las 1.668 apariciones del corpus.
        ''' Lo pone el CONSTRUCTOR de cada clase en el `.exe` (`mov dword [this+0x18], imm`), y es el que
        ''' `StiffnessFactor` (`0x1418C64A6`) lee para decidir el `k`. La tabla sale del RE cap. 2.2,
        ''' leida de los ctors: 1 StandardLink · 2 StretchLink · 3 BendLink · 4 BendStiffness ·
        ''' 5 LocalRange · 8 Transition · 10 BonePlanes · 13/14/15/16 las `Mx` de enlace ·
        ''' 17 VolumeMx · 19 AntiPinch · 20 CompressibleLink · 21 CompressibleLinkMx.</para>
        ''' <para>⛔ EL ORDEN Y EL INDICE SON LOS DEL ARCHIVO: `constraintExecution` referencia los sets
        ''' POR POSICION en `staticConstraintSets`, asi que una clase que este motor todavia no transcribe
        ''' deja su lugar VACIO en vez de correr la lista. `Motor.Resolver` ya saltea los nulos.</para>
        ''' <para>⛔ HUECOS DECLARADOS: `hclVolumeConstraint` (6),
        ''' `hclAntiPinchConstraintSet` (19) y las cinco `Mx` de enlace (13, 14, 15, 16, 21). Se cuentan y
        ''' se dicen en el log; no se silencian ni se aproximan con otra clase.</para>
        ''' </summary>
        Friend Function RestriccionesDe(sim As HkObj_HclSimClothData) As SetCompilado()
            If sim Is Nothing Then Return Array.Empty(Of SetCompilado)()
            Dim crudos = Havok.Canon.HavokConstraintSets.Crudos(sim, Havok.Canon.HavokConstraintSets.Fuente.Estaticos)
            If crudos Is Nothing OrElse crudos.Count = 0 Then Return Array.Empty(Of SetCompilado)()

            Dim r(crudos.Count - 1) As SetCompilado
            Dim g = sim.Graph
            For i = 0 To crudos.Count - 1
                Dim crudo = crudos(i).Bloque
                If crudo Is Nothing Then Continue For

                Dim oStd = HkObj_HclStandardLinkConstraintSet.Leer(g, crudo)
                If oStd IsNot Nothing Then r(i) = New EnlaceEstandar(oStd, 1) : Continue For

                Dim oStr = HkObj_HclStretchLinkConstraintSet.Leer(g, crudo)
                If oStr IsNot Nothing Then r(i) = New EnlaceDeEstiramiento(oStr, 2) : Continue For

                Dim oBnd = HkObj_HclBendLinkConstraintSet.Leer(g, crudo)
                If oBnd IsNot Nothing Then r(i) = New EnlaceDeDoblez(oBnd, 3) : Continue For

                Dim oBst = HkObj_HclBendStiffnessConstraintSet.Leer(g, crudo)
                If oBst IsNot Nothing Then r(i) = New RigidezDeDoblez(oBst, 4) : Continue For

                Dim oLoc = HkObj_HclLocalRangeConstraintSet.Leer(g, crudo)
                If oLoc IsNot Nothing Then r(i) = New RangoLocal(oLoc, 5) : Continue For

                Dim oTra = HkObj_HclTransitionConstraintSet.Leer(g, crudo)
                If oTra IsNot Nothing Then r(i) = New Transicion(oTra, 8) : Continue For

                Dim oBpl = HkObj_HclBonePlanesConstraintSet.Leer(g, crudo)
                If oBpl IsNot Nothing Then r(i) = New PlanosDeHueso(oBpl, 10) : Continue For

                Dim oCmp = HkObj_HclCompressibleLinkConstraintSet.Leer(g, crudo)
                If oCmp IsNot Nothing Then r(i) = New EnlaceCompresible(oCmp, 20) : Continue For

                Dim oVol = HkObj_HclVolumeConstraintMx.Leer(g, crudo)
                If oVol IsNot Nothing Then r(i) = New Volumen(oVol, 17) : Continue For

                ' ⛔ EL GEMELO ESCALAR (tipo 6), que es el mismo dato sin empaquetar en lotes.
                Dim oVolEsc = HkObj_HclVolumeConstraint.Leer(g, crudo)
                If oVolEsc IsNot Nothing Then r(i) = New Volumen(oVolEsc, 6) : Continue For

                ' ⛔ LAS CINCO `Mx` DE ENLACE. Cero apariciones en el corpus (medido), pero un mod
                ' puede traerlas y hasta hoy caian en el hueco.
                Dim oStdMx = HkObj_HclStandardLinkConstraintSetMx.Leer(g, crudo)
                If oStdMx IsNot Nothing Then r(i) = New EnlaceEstandarMx(oStdMx, 13) : Continue For

                Dim oBndMx = HkObj_HclBendLinkConstraintSetMx.Leer(g, crudo)
                If oBndMx IsNot Nothing Then r(i) = New EnlaceDeDoblezMx(oBndMx, 14) : Continue For

                Dim oStrMx = HkObj_HclStretchLinkConstraintSetMx.Leer(g, crudo)
                If oStrMx IsNot Nothing Then r(i) = New EnlaceDeEstiramientoMx(oStrMx, 15) : Continue For

                Dim oBstMx = HkObj_HclBendStiffnessConstraintSetMx.Leer(g, crudo)
                If oBstMx IsNot Nothing Then r(i) = New RigidezDeDoblezMx(oBstMx, 16) : Continue For

                Dim oCmpMx = HkObj_HclCompressibleLinkConstraintSetMx.Leer(g, crudo)
                If oCmpMx IsNot Nothing Then r(i) = New EnlaceCompresibleMx(oCmpMx, 21) : Continue For

                ' ⛔ clase que este motor todavia no transcribe: el hueco queda VACIO y se DICE.
                If Logger.Enabled Then
                    Dim cq = If(crudo.ClassName, "?"), iq = i
                    Logger.LogLazy(Function() $"[MOTOR-SETS] ⛔ hueco: el set #{iq} es `{cq}` y este motor no lo transcribe — no se aplica")
                End If
            Next

            ' ⛔ La marca de lo que llega al SOLVER no se emite acá: la emite `Motor.Simular`, que es
            ' quien los aplica. Emitirla acá ya dejó pasar una mutación — `e.Restricciones = Nothing`
            ' justo después de compilarlos daba la marca completa y el gate en verde. Quien mide un
            ' trabajo tiene que ser quien lo hace, no quien lo prepara.
            Return r
        End Function


        ''' <summary>
        ''' La instancia del motor para una prenda, con lo que el archivo declara.
        ''' <para>El constructor de <see cref="Instancia"/> ya lee `particleDatas`, `fixedParticles`,
        ''' `staticCollisionMasks` y `perInstanceCollidables`. Aca se completa lo que ese ctor no
        ''' toca y el motor si necesita: la **tolerancia de colision** del paso 5
        ''' (`simulationInfo+0x14`, `0x1418C750A`).</para>
        ''' </summary>
        Friend Function InstanciaDe(sim As HkObj_HclSimClothData) As Instancia
            If sim Is Nothing Then Return Nothing
            Dim inst As New Instancia(sim)
            Dim info = sim.SimulationInfo
            If info IsNot Nothing Then
                inst.ToleranciaDeColision = info.CollisionTolerance
                inst.LandscapeHabilitado = info.LandscapeCollisionEnabled   ' +0x1D, 0x14195E3B2
                ' ⛔ La PRIMERA comprobacion de la puerta de `TtCollideAndSolve` (0x141A697BF).
                inst.PellizcoHabilitado = info.PinchDetectionEnabled        ' +0x1C
            End If
            ' ⛔ El rango de pellizco es INCLUSIVO y las dos puntas salen del dato: el minimo ya
            ' lo pone el ctor, el maximo faltaba (0x141A75F5A `movzx eax, word [rdx+0x12A]`).
            inst.MaximoDePellizco = sim.MaxPinchedParticleIndex             ' +0x12A
            ' ⛔ La bandera POR PARTICULA (+0x108): parte la lista de la colision en dos
            ' (`0x141A71898`). Sin ella todas iban por el mismo camino.
            inst.PellizcoPorParticula = ABytesDeBool(sim.PerParticlePinchDetectionEnabledFlags)
            Return inst
        End Function

        ' -----------------------------------------------------------------------------------------
        ' Conversiones. ⛔ Todas asumen row-major y `vector4` de 4 `Single`, que es lo que declara
        ' la reflexion (`vector4,,0,,0,10,10` = 16 B) y lo que el motor lee con `movups`.
        ' -----------------------------------------------------------------------------------------

        ''' <summary>Un `vector4` del archivo. Con menos de 4 componentes se rellena con cero, que
        ''' es lo que hay en memoria si el campo no existe.</summary>
        Friend Function V4(v As Single()) As Vector128(Of Single)
            If v Is Nothing Then Return Vector128(Of Single).Zero
            Return Vector128.Create(If(v.Length > 0, v(0), 0.0F), If(v.Length > 1, v(1), 0.0F),
                                    If(v.Length > 2, v(2), 0.0F), If(v.Length > 3, v(3), 0.0F))
        End Function

        ''' <summary>Un `Matrix4x4` de System.Numerics a `Mat4`, fila por fila.</summary>
        Friend Function M4(m As System.Numerics.Matrix4x4) As Mat4
            Return M4(New Single() {m.M11, m.M12, m.M13, m.M14,
                                    m.M21, m.M22, m.M23, m.M24,
                                    m.M31, m.M32, m.M33, m.M34,
                                    m.M41, m.M42, m.M43, m.M44})
        End Function

        ''' <summary>Un `Mat4` a `Matrix4x4` de System.Numerics, fila por fila.</summary>
        Friend Function ANumerics(m As Mat4) As System.Numerics.Matrix4x4
            Return New System.Numerics.Matrix4x4(
                Vector128.GetElement(m.F0, 0), Vector128.GetElement(m.F0, 1), Vector128.GetElement(m.F0, 2), Vector128.GetElement(m.F0, 3),
                Vector128.GetElement(m.F1, 0), Vector128.GetElement(m.F1, 1), Vector128.GetElement(m.F1, 2), Vector128.GetElement(m.F1, 3),
                Vector128.GetElement(m.F2, 0), Vector128.GetElement(m.F2, 1), Vector128.GetElement(m.F2, 2), Vector128.GetElement(m.F2, 3),
                Vector128.GetElement(m.F3, 0), Vector128.GetElement(m.F3, 1), Vector128.GetElement(m.F3, 2), Vector128.GetElement(m.F3, 3))
        End Function

        ''' <summary>Un PUNTO de System.Numerics a lane, con `w = 1`.
        ''' <para>Nombre propio y no otra sobrecarga de `V4`: `V4(Nothing)` se volvia ambigua entre
        ''' `Single()` y `Vector3`, y una ambiguedad en el traductor del motor no se arregla con un
        ''' cast en cada llamada.</para></summary>
        Friend Function PuntoV4(v As System.Numerics.Vector3) As Vector128(Of Single)
            Return Vector128.Create(v.X, v.Y, v.Z, 1.0F)
        End Function

        ''' <summary>Un `transform` del archivo (16 `Single`, cuatro filas de 4).</summary>
        Friend Function M4(m As Single()) As Mat4
            Dim r As Mat4
            If m Is Nothing OrElse m.Length < 16 Then Return Mat4.Identidad
            r.F0 = Vector128.Create(m(0), m(1), m(2), m(3))
            r.F1 = Vector128.Create(m(4), m(5), m(6), m(7))
            r.F2 = Vector128.Create(m(8), m(9), m(10), m(11))
            r.F3 = Vector128.Create(m(12), m(13), m(14), m(15))
            Return r
        End Function

        ''' <summary>Una lista de `vector4` o de `matrix4`, aplanada a `Single()` — que es como el
        ''' motor la tiene: memoria contigua.</summary>
        Friend Function Aplanar(lista As System.Collections.Generic.IList(Of Single())) As Single()
            If lista Is Nothing OrElse lista.Count = 0 Then Return Array.Empty(Of Single)()
            Dim ancho = 0
            For Each e In lista
                If e IsNot Nothing Then ancho = Math.Max(ancho, e.Length)
            Next
            If ancho = 0 Then Return Array.Empty(Of Single)()
            Dim r(lista.Count * ancho - 1) As Single
            For i = 0 To lista.Count - 1
                Dim e = lista(i)
                If e Is Nothing Then Continue For
                For k = 0 To Math.Min(ancho, e.Length) - 1
                    r(i * ancho + k) = e(k)
                Next
            Next
            Return r
        End Function

        Friend Function AEnteros(Of T As IConvertible)(lista As System.Collections.Generic.IList(Of T)) As Integer()
            If lista Is Nothing OrElse lista.Count = 0 Then Return Array.Empty(Of Integer)()
            Dim r(lista.Count - 1) As Integer
            For i = 0 To lista.Count - 1
                r(i) = Convert.ToInt32(lista(i))
            Next
            Return r
        End Function

        ''' <summary>`heights` viene como `List(Of Integer)` del parser, pero en el archivo son
        ''' `uint8` (reflexion: `heights,30,array,uint8`). Se recorta a byte, que es el ancho
        ''' real: el kernel lee con `movzx eax, byte ptr`.</summary>
        ''' <summary>Una lista de `bool` del archivo, a bytes — la bandera por particula del
        ''' pellizco (`data+0x108`), que el motor lee de a un byte (`0x141A71898`).</summary>
        Friend Function ABytesDeBool(lista As System.Collections.Generic.IList(Of Boolean)) As Byte()
            If lista Is Nothing OrElse lista.Count = 0 Then Return Array.Empty(Of Byte)()
            Dim r(lista.Count - 1) As Byte
            For i = 0 To lista.Count - 1
                r(i) = CByte(If(lista(i), 1, 0))
            Next
            Return r
        End Function

        Friend Function ABytesDeEnteros(lista As System.Collections.Generic.IList(Of Integer)) As Byte()
            If lista Is Nothing OrElse lista.Count = 0 Then Return Array.Empty(Of Byte)()
            Dim r(lista.Count - 1) As Byte
            For i = 0 To lista.Count - 1
                r(i) = CByte(lista(i) And &HFF)
            Next
            Return r
        End Function

        ''' <summary>`hkAabb.min` (`+0x00`) y `hkAabb.max` (`+0x10`), de la reflexion.</summary>
        Friend Function AabbMin(a As HkObj_HkAabb) As Vector128(Of Single)
            Return If(a Is Nothing, Vector128(Of Single).Zero, V4(a.Min))
        End Function

        Friend Function AabbMax(a As HkObj_HkAabb) As Vector128(Of Single)
            Return If(a Is Nothing, Vector128(Of Single).Zero, V4(a.Max))
        End Function

    End Module


    ''' <summary>Las dos listas de un bloque de entradas de espacio de hueso.</summary>
    Friend NotInheritable Class EntradaDeHuesoLeida
        Friend ReadOnly Vertices As List(Of Integer)
        Friend ReadOnly Huesos As List(Of Integer)
        Friend Sub New(vertices As List(Of Integer), huesos As List(Of Integer))
            Me.Vertices = vertices
            Me.Huesos = huesos
        End Sub
    End Class

End Namespace

#End If
