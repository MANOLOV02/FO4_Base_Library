' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' Orquestador del parseo completo de un HKX de tela embebido en un NIF:
' skeleton, collidables, capsule shapes, cloth data, operators, states.
'
' ⛔ SI ESTA EN LA RUTA DEL RENDER. La cabecera decia lo contrario y nombraba a
' Wardrobe_Manager/PhysicsWeightCollapseHelper como consumidor principal: es falso.
' `HavokClothSimulation` lo llama para armar el paquete de tela, y `Render.vb` llama a
' `HavokClothSimulation.StepShapes` dentro del `#If DEBUG` que abre unas lineas mas arriba.
' Los otros consumidores (Wardrobe_Manager y las herramientas de Tools/) tambien existen.
'
' ALCANCE: los offsets ya no se escriben aca. Todo campo declarado sale del objeto generado
' (`HavokObjects.vb`), que resuelve la tabla de la reflexion del .exe del juego que corresponda.
' Lo unico que queda escrito a mano es lo que la reflexion NO declara: el agrupado de
' `triangleIndices` de a tres (`HclTrianguloDeSim_Class`) y el desempaquetado de
' `triangleBonePairs` (ver HclRenderGraphParser). Las `hcl*` no existen en la reflexion de
' Skyrim: ese juego no tiene motor de cloth.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq
Imports System.Numerics

Public NotInheritable Class HclClothPackageParser_Class
    Public Shared Function Parse(packfile As HkxPackfile_Class) As HclClothPackageGraph_Class
        If IsNothing(packfile) Then Throw New ArgumentNullException(NameOf(packfile))
        Return Parse(HkxObjectGraphParser_Class.BuildGraph(packfile))
    End Function

    Public Shared Function Parse(graph As HkxObjectGraph_Class) As HclClothPackageGraph_Class
        If IsNothing(graph) Then Throw New ArgumentNullException(NameOf(graph))

        Dim result As New HclClothPackageGraph_Class With {
            .Graph = graph
        }

        ' ⛔ El esqueleto sale de `hkaAnimationContainer.skeletons`, no del primer bloque.
        result.Skeleton = graph.EsqueletoPrincipal()

        ' ⛔ DEL ARBOL GENERADO. Antes habia un memo a mano (`Dictionary(Of Integer,
        ' DTO a mano)`) para no re-parsear el mismo `hclCollidable` a nivel package y
        ' por-sim. El objeto generado no lo necesita: NO copia el archivo, es una vista con la tabla
        ' de offsets ya resuelta y los campos memoizados al primer acceso. La identidad canonica
        ' sigue siendo el `RelativeOffset` del bloque, que el objeto expone en `Source`.
        For Each obj In graph.GetObjectsByClassName("hclCollidable")
            Dim c = Havok.Canon.Objects.HkObj_HclCollidable.Read(graph, obj)
            If c IsNot Nothing Then result.Collidables.Add(c)
        Next


        For Each clothObject In graph.GetObjectsByClassName("hclClothData")
            ' ⛔ EL ARBOL GENERADO, DIRECTO. `graph.ParseClothData` devolvia un DTO a mano que
            ' copiaba lo mismo. `HkObj_HclClothData` ya entrega `SimClothDatas`, `Operators`,
            ' `ClothStateDatas`, etc. como listas de objetos generados.
            Dim clothData = Havok.Canon.Objects.HkObj_HclClothData.Read(graph, clothObject)
            If IsNothing(clothData) Then Continue For

            ' Degradación per-cloth-config: los readers HCL LANZAN (InvalidDataException) cuando un
            ' offset empírico cae fuera de rango, en vez de devolver ceros silenciosos. Si UNA config
            ' explota por un layout inesperado se loguea y se omite ESA config — el resto del package
            ' se sigue parseando. Los llamadores (PhysicsWeightCollapseHelper) envuelven Parse() con el
            ' mismo criterio.
            Try
                Dim clothConfig As New HclClothConfigGraph_Class With {
                    .ClothData = clothData
                }


                ' ⛔⛔ LA LISTA TIENE QUE QUEDAR ALINEADA CON EL ARCHIVO.
                '
                ' El `.Where(Not IsNothing)` COMPACTABA la lista al descartar los scratch, y con eso los
                ' indices dejaban de ser los del array `hclClothData.bufferDefinitions`. Los operadores
                ' referencian sus buffers POR POSICION (`inputBufferIdx`, `outputBufferIdx`,
                ' `hclSimpleMeshBoneDeformOperator.inputBufferIdx`), asi que cualquier consumidor que
                ' indexe esta lista con esos numeros lee OTRO buffer — o se sale de rango en silencio.
                ' Medido en el vestido: el archivo declara [0]CLOTH_SIM (scratch) y [1]SimBuf, el deform
                ' pide el 1, y la lista compactada tenia un solo elemento.
                '
                ' Un scratch entra como Nothing en su posicion: se pierde el dato, no el indice.
                ' ⛔ DEL ARBOL GENERADO. `clothData.BufferDefinitions` ya es
                ' `List(Of HkObj_HclBufferDefinition)`. El scratch es una SUBCLASE: se lo distingue
                ' por el nombre de clase del objeto crudo, en la MISMA posicion del array.
                ' ⛔ LA COTA ES LA QUE DECLARA LA CABECERA, NO EL LARGO DE LA LISTA MATERIALIZADA.
                ' Es la misma ley que `HavokConstraintSets.CuantosDeclara`: la propiedad generada
                ' COMPACTA (`If o IsNot Nothing Then Add`), asi que su `.Count` puede ser MENOR que lo
                ' que el archivo declara y acotar con el corta la COLA. Y aca era peor: la linea
                ' mezclaba los dos espacios de indices —`Raw.BufferDefinitionsRef(iB)` es el crudo y
                ' `clothData.BufferDefinitions(iB)` el compactado—, asi que con UN hueco antes de `iB`
                ' se emparejaban objetos DISTINTOS. Justo la desalineacion que el comentario de arriba
                ' dice estar evitando. El dato sale ahora del ref crudo, en su posicion.
                For iB = 0 To clothData.Raw.BufferDefinitionsCount - 1
                    Dim crudoB = clothData.Raw.BufferDefinitionsRef(iB)
                    ' ⛔ SIN LITERAL: `Leer(Of T)` devuelve Nothing si el bloque declara otra clase, asi que
                    ' la lectura y el "¿es un scratch?" son la MISMA pregunta.
                    Dim scratch = Havok.Canon.HavokConstraintSets.Leer(Of Havok.Canon.Objects.HkObj_HclScratchBufferDefinition)(graph, crudoB)
                    Dim def_ = If(scratch IsNot Nothing, Nothing,
                                  Havok.Canon.HavokConstraintSets.Leer(Of Havok.Canon.Objects.HkObj_HclBufferDefinition)(graph, crudoB))
                    clothConfig.BufferDefinitions.Add(def_)
                    clothConfig.ScratchBufferDefinitions.Add(scratch)
                Next

                ' ⛔ TAMBIEN ALINEADA. `hclClothData.bufferDefinitions` mezcla `hclBufferDefinition` y
                ' `hclScratchBufferDefinition` en el MISMO array, y los operadores indexan ese array.
                ' Compactar cualquiera de las dos listas rompe la correspondencia: el `outputBufferIdx`
                ' de un skin puede apuntar a un scratch, y si ese hueco no existe el operador escribe
                ' en la nada. MEDIDO en el vestido: [0] es CLOTH_SIM (scratch, 340 v) y el skin escribe
                ' justo ahi.



                ' ⛔⛔ LA CADENA, EN EL ORDEN DEL ARCHIVO.
                '
                ' El motor NO corre una secuencia fija: `hclClothState.operators` es una lista de
                ' INDICES a `hclClothData.operators`, y `Execute Operators` los despacha uno por uno en
                ' ese orden. Las propiedades singulares de abajo (`Simulate`, `SimpleMeshBoneDeform`,
                ' …) son un resumen por CLASE — sirven para consultar, no para ejecutar: pierden el
                ' orden, pierden los repetidos, y obligan a la app a inventar una secuencia propia.
                ' `OperadoresEnOrden` conserva la posicion, que es lo unico que el estado referencia.
                ' ⛔ LA COTA DECLARADA, igual que arriba: `Operators` compacta y su `.Count` corta la
                ' cola. El indice es ademas la clave de `SkinDecodificado`/`DeformDecodificado` y el
                ' mismo que referencia `hclClothState.operators`.
                For iOp = 0 To clothData.Raw.OperatorsCount - 1
                    ' ⛔ LA CADENA GUARDA EL OBJETO GENERADO, SIEMPRE. Lo que ademas se DECODIFICA
                    ' (los dos operadores con analisis propio) queda indexado por esta misma posicion,
                    ' que es como `hclClothState.operators` los referencia.
                    clothConfig.OperadoresEnOrden.Add(
                        ParseOperadorPorClase(graph, clothData.Raw.OperatorsRef(iOp), result.Skeleton, clothConfig, iOp))
                Next

                ' ⛔ EL RESUMEN POR CLASE NO SE GUARDA: SE LEE DE LA CADENA.
                ' Aca habia un bucle de `TryCast` que copiaba cada operador a un campo singular
                ' (`Simulate`, `MoveParticles`, …). Eso dejaba la MISMA respuesta en dos lugares:
                ' la cadena y el resumen, que podian discrepar. Las propiedades de abajo salen
                ' hoy de `OperadoresEnOrden`, que es lo unico que el archivo declara.

                PopulateResolvedCollidableBindings(clothConfig, result.Skeleton)
                PopulateResolvedSimulateConfigs(clothConfig)
                PopulateResolvedTriangles(clothConfig)
                PopulateSkinCoverage(clothConfig)

                result.ClothConfigs.Add(clothConfig)
            Catch ex As Exception
                Dim clothName = clothData.Name
                Logger.LogLazy(Function() $"[CLOTH-HCL] se omite hclClothData '{clothName}' (offset +0x{clothObject.RelativeOffset:X}): el parseo lanzó {ex.GetType().Name}: {ex.Message}")
            End Try
        Next

        Return result
    End Function

    Private Shared Sub PopulateSkinBoneNames(skin As HclObjectSpaceSkinPNOperatorGraph_Class, skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton)
        If IsNothing(skin) OrElse IsNothing(skeleton?.Bones) Then Return

        ' ⛔ IDEMPOTENTE. El segundo bucle lee lo que este AGREGA, asi que llamar dos veces sobre el
        ' mismo skin duplicaria la paleta y todo `ti` resolveria al hueso equivocado, sin error.
        skin.ResolvedBoneNames.Clear()
        For Each boneIndex In skin.Operador.TransformSubset
            If boneIndex >= 0 AndAlso boneIndex < skeleton.Bones.Count Then
                skin.ResolvedBoneNames.Add(skeleton.Bones(CInt(boneIndex)).Name)
            Else
                skin.ResolvedBoneNames.Add("#" & boneIndex.ToString())
            End If
        Next

        ' Y el nombre de cada influencia de cada vertice. ⛔ POR LA TABLA QUE ACABA DE ARMARSE, no
        ' resolviendo el puente otra vez: `transformIndices` indexa la PALETA (`transformSubset`), y
        ' `ResolvedBoneNames` ya es esa paleta resuelta a nombres. Aca estaba escrito el mismo
        ' `TransformSubset(ti) -> Bones(si).Name` por segunda vez, en el mismo Sub.
        For Each v In skin.Vertices
            If v Is Nothing Then Continue For
            v.ResolvedBoneNames.Clear()
            For Each ti In v.TransformIndices
                If ti < 0 OrElse ti >= skin.ResolvedBoneNames.Count Then Continue For
                v.ResolvedBoneNames.Add(skin.ResolvedBoneNames(CInt(ti)))
            Next
        Next
    End Sub

    Private Shared Sub PopulateResolvedCollidableBindings(config As HclClothConfigGraph_Class, skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton)
        If IsNothing(config) Then Return

        For Each sim In config.ClothData.SimClothDatas
            Dim an = config.CollidableBindingsDe(sim)
            an.Clear()

            For i = 0 To sim.PerInstanceCollidables.Count - 1
                Dim binding As New HclSimCollidableBinding_Class With {
                    .Collidable = sim.PerInstanceCollidables(i),
                    .MatrixValues = If(i < sim.CollidableTransformMap.Offsets.Count, sim.CollidableTransformMap.Offsets(i), Nothing)
                }

                ' `collidableTransformMap.transformIndices[i]` indexa el esqueleto de la prenda: el
                ' puente que buscan los tres consumidores es el NOMBRE del hueso.
                If i < sim.CollidableTransformMap.TransformIndices.Count Then
                    Dim bi = CInt(sim.CollidableTransformMap.TransformIndices(i))
                    If Not IsNothing(skeleton?.Bones) AndAlso bi >= 0 AndAlso bi < skeleton.Bones.Count Then
                        binding.BoneName = skeleton.Bones(bi).Name
                    End If
                End If

                an.Add(binding)
            Next

        Next
    End Sub

    ''' <summary>
    ''' Los tipos generados que NO se entregan crudos porque van ENVUELTOS en una clase de analisis.
    ''' <para>⛔ SE DERIVAN DE LA PROPIEDAD `Operador` DE CADA ENVOLTORIO, no se escriben: si el
    ''' envoltorio cambia de tipo, esta lista cambia con el. Es la misma correspondencia que
    ''' `HavokLayoutGate` usa para exigir que `EjecutarOperador` tenga una rama por cada tipo
    ''' declarado.</para>
    ''' </summary>
    Private Shared ReadOnly TiposQueNecesitanDecodificacion As Type() =
        {
            GetType(HclObjectSpaceSkinPNOperatorGraph_Class).GetProperty("Operador").PropertyType,
            GetType(HclSimpleMeshBoneDeformOperatorGraph_Class).GetProperty("Operador").PropertyType
        }

    ''' <summary>
    ''' El detalle parseado de UN operador, elegido por su clase. Devuelve Nothing para las clases que
    ''' esta app no ejecuta — asi la posicion en la lista se conserva igual.
    ''' <para>⛔ CERO LITERALES DE NOMBRE DE CLASE Y CERO SEGUNDA LISTA. Aca hubo primero un
    ''' `Select Case` con siete strings en minuscula y un normalizador propio; despues, cinco
    ''' `Leer(Of T)` escritos a mano, que es una lista paralela a
    ''' <see cref="Havok.Physics.HavokClothSimulation.TiposDeOperadorQueEjecuta"/> con nada que las
    ''' obligue a coincidir. Ahora manda esa lista: se recorre y se prueba `LeerPorTipo`, que deriva
    ''' el nombre del tipo con la regla del generador.</para>
    ''' <para>Los DOS que llevan analisis propio (el entrelazado SIMD de los carriles, los pares
    ''' hueso/triangulo) se prueban ANTES, con su parser. Si ese parseo falla sobre un bloque que SI
    ''' es de esa clase, el operador es un HUECO y se dice: <see cref="TiposQueNecesitanDecodificacion"/>
    ''' impide que el bucle lo entregue CRUDO. `EjecutarOperador` hoy SI tiene rama para el objeto
    ''' pelado —despacha por el tipo generado— pero sin lo decodificado no puede hacer el trabajo:
    ''' entregarlo igual lo dejaria sin ejecutar mientras `[CLOTH-CADENA]`,
    ''' `--clothcover` y el gate lo cuentan como implementado.</para>
    ''' </summary>
    Private Shared Function ParseOperadorPorClase(graph As HkxObjectGraph_Class, op As HkxVirtualObjectGraph_Class,
                                                  skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                                  cfg As HclClothConfigGraph_Class, pos As Integer) As Object
        If op Is Nothing Then Return Nothing
        ' Los DOS que llevan decodificacion propia: se decodifican y lo decodificado se guarda por
        ' POSICION. Lo que va a la cadena es el objeto generado, igual que los otros cinco.
        Dim skinOp = HclRenderGraphParser_Class.ParseObjectSpaceSkinPNOperator(graph, op)
        If skinOp IsNot Nothing Then
            PopulateSkinBoneNames(skinOp, skeleton)
            cfg.SkinDecodificado(pos) = skinOp
            Return skinOp.Operador
        End If
        Dim deform = HclRenderGraphParser_Class.ParseSimpleMeshBoneDeformOperator(graph, op, skeleton)
        If deform IsNot Nothing Then
            cfg.DeformDecodificado(pos) = deform
            Return deform.Operador
        End If

        For Each t In Havok.Physics.HavokClothSimulation.TiposDeOperadorQueEjecuta
            Dim o = Havok.Canon.HavokConstraintSets.LeerPorTipo(t, graph, op)
            If o Is Nothing Then Continue For
            ' ⛔ LO QUE VA ENVUELTO NO SE ENTREGA CRUDO. Si el parseo del envoltorio fallo sobre un
            ' bloque que SI es de esa clase, el operador es un HUECO. Devolverlo crudo lo mete en la
            ' cadena, `EjecutarOperador` no tiene rama para el —despacha por el envoltorio— y el
            ' operador queda sin ejecutar mientras `[CLOTH-CADENA]`, `--clothcover` y el gate lo
            ' cuentan como implementado: tres instrumentos mintiendo a la vez.
            If TiposQueNecesitanDecodificacion.Contains(t) Then Exit For
            Return o
        Next

        ' ⛔ EL HUECO SE DICE ACA, QUE ES DONDE SE SABE LA CLASE. `EjecutarOperador` solo ve el objeto
        ' ya parseado, y el bucle de la cadena saltea los Nothing sin decir una palabra: un operador
        ' que el archivo declara y esta app no parsea desaparecia sin dejar rastro.
        If Logger.Enabled Then
            Dim cq = If(op.ClassName, "(sin clase)")
            Logger.LogLazy(Function() $"[CLOTH-OPDESC] operador declarado que esta app NO parsea: {cq}")
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' ⛔ LA RESOLUCION DE `constraintExecution` ES ANALISIS, NO UN CAMPO DEL ARCHIVO.
    ''' La reflexion declara `hclSimulateOperator.constraintExecution` (+0x30) como `array of int32`
    ''' y nada mas: cada entrada es el INDICE del set en `staticConstraintSets`, o -1 para el
    ''' terminador que el motor lee como 'aca va la colision'. El nombre y la clase del set que
    ''' cada indice resuelve se CALCULAN, asi que viven en el analisis y no encima del objeto
    ''' canonico, que es de solo lectura.
    ''' </summary>
    Private Shared Sub PopulateResolvedSimulateConfigs(config As HclClothConfigGraph_Class)
        If IsNothing(config?.Simulate) Then Return
        config.EjecucionResuelta.Clear()

        Dim sim = config.ClothData.SimClothDatas.FirstOrDefault()
        For i = 0 To config.Simulate.ConstraintExecution.Count - 1
            Dim valor = config.Simulate.ConstraintExecution(i)
            Dim e As New EntradaDeEjecucion_Class With {.EntryIndex = i, .Value = valor,
                                                        .IsTerminator = (valor < 0), .ConstraintIndex = -1}
            config.EjecucionResuelta.Add(e)
            If e.IsTerminator OrElse sim Is Nothing Then Continue For
            e.ConstraintIndex = valor
            ' ⛔ SIN COTA PROPIA. Habia un `valor >= sim.StaticConstraintSets.Count` — la lista
            ' COMPACTADA, que saltea los punteros sin fixup — y eso cortaba la COLA: una entrada de
            ' `constraintExecution` que apunta al ultimo elemento del arreglo salia sin resolver.
            ' `CrudoEn` ya acota con `CuantosDeclara` (el conteo del header, que es la ley), asi que
            ' esta cota no solo sobraba: contradecia a la unica que vale.
            ' ⛔ EL BLOQUE LO DA LA LEY. Este era el TERCER sitio que hacia el `.Raw` a mano.
            Dim crudo = Havok.Canon.HavokConstraintSets.CrudoEn(sim, valor)
            If crudo Is Nothing Then Continue For
            e.ResolvedConstraintType = crudo.ClassName
            Dim cs = Havok.Canon.Objects.HkObj_HclConstraintSet.Read(sim.Graph, crudo)
            If cs IsNot Nothing Then e.ResolvedConstraintName = cs.Name
        Next
    End Sub

    ''' <summary>Los triangulos del sim-cloth, desde la lista PLANA que entrega el objeto generado.
    ''' `hclSimClothData.triangleIndices` es `array of uint16` y cada triangulo son tres seguidos:
    ''' no hay una segunda lista que mantener, es la misma leida de a tres.</summary>
    Public Shared Function TriCount(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As Integer
        If sim Is Nothing OrElse sim.TriangleIndices Is Nothing Then Return 0
        Return sim.TriangleIndices.Count \ 3
    End Function

    Public Shared Function TriDe(sim As Havok.Canon.Objects.HkObj_HclSimClothData, i As Integer) As HclTrianguloDeSim_Class
        If sim Is Nothing OrElse i < 0 OrElse (i * 3) + 2 >= sim.TriangleIndices.Count Then Return Nothing
        Return New HclTrianguloDeSim_Class With {
            .Value0 = CUShort(sim.TriangleIndices(i * 3)),
            .Value1 = CUShort(sim.TriangleIndices((i * 3) + 1)),
            .Value2 = CUShort(sim.TriangleIndices((i * 3) + 2))}
    End Function

    Private Shared Sub PopulateResolvedTriangles(config As HclClothConfigGraph_Class)
        If IsNothing(config?.SimpleMeshBoneDeform) Then Return

        Dim sim = config.ClothData.SimClothDatas.FirstOrDefault()
        If IsNothing(sim) Then Return

        For Each mapping In config.SimpleMeshBoneDeform.BoneMappings
            If mapping.TriangleIndex < 0 OrElse mapping.TriangleIndex >= TriCount(sim) Then Continue For
            mapping.ResolvedTriangle = TriDe(sim, mapping.TriangleIndex)
        Next
    End Sub

    Private Shared Sub PopulateSkinCoverage(config As HclClothConfigGraph_Class)
        Dim skin = config?.ObjectSpaceSkin
        If IsNothing(skin) Then Return

        ' Cuantos vertices distintos cubre el skin. Antes se guardaba la LISTA entera para
        ' despues tomarle el `.Count`, que es lo unico que se consulta.
        skin.CoveredVertexCount = skin.Vertices.
                Where(Function(v) v IsNot Nothing AndAlso v.VertexIndex <> UShort.MaxValue).
                Select(Function(v) CInt(v.VertexIndex)).
                Distinct().
                Count()
    End Sub


End Class

Public Class HclClothPackageGraph_Class
    Public Property Graph As HkxObjectGraph_Class
    Public Property Skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton
    Public ReadOnly Property ClothConfigs As New List(Of HclClothConfigGraph_Class)
    Public ReadOnly Property Collidables As New List(Of Havok.Canon.Objects.HkObj_HclCollidable)
End Class

''' <summary>
''' La configuracion de UNA prenda: la cadena de operadores en el orden del archivo, mas las
''' proyecciones que este parser calcula sobre ella.
''' <para>⛔ LAS PROPIEDADES EN SINGULAR (`Simulate`, `MoveParticles`, `SimpleMeshBoneDeform`,
''' `CopyVertices`, `GatherAllVertices`, `GatherSomeVertices`) NO SON LA EJECUCION. El motor corre
''' la CADENA ENTERA en orden, y esta app tambien: `EjecutarCadena` recorre `OperadoresEnOrden` y
''' no mira ninguna de ellas. Lo unico que sale de ahi son PARAMETROS de arranque (substeps,
''' iteraciones, indices de buffer, el puente de gather), que en el corpus vienen del mismo
''' operador. Cuando el archivo declara mas de uno gana el ULTIMO; antes eso se justificaba con
''' "es lo que hacia el bucle que escribia este campo", que es auto-justificacion, no una cita.</para>
''' <para>⚠️ Cuando queda mas de uno, el otro no se mira. `--clothcover` lo cuenta y enrojece:
''' `operadores DESCARTADOS por quedar la propiedad en singular`.</para>
''' </summary>
Public Class HclClothConfigGraph_Class
    ' ⛔⛔ NO HAY LISTAS COPIADAS ACA. `HkObj_HclClothData` ya declara y CACHEA
    ' `SimClothDatas` y `ClothStateDatas`; este config las duplicaba con `AddRange`. El mismo
    ' dato en dos lados se desfasa y obliga al lector a elegir cual es el bueno: se pregunta al
    ' objeto generado, que es el unico que lo lee del archivo.
    Public Property ClothData As Havok.Canon.Objects.HkObj_HclClothData
    ''' <summary>La lista `constraintExecution` con su resolucion. Ver `PopulateResolvedSimulateConfigs`.</summary>
    Public ReadOnly Property EjecucionResuelta As New List(Of EntradaDeEjecucion_Class)
    ''' <summary>
    ''' ⛔ LOS BINDINGS DE COLISIONABLE POR SIM-CLOTH. Son un CALCULO: cruzan
    ''' `perInstanceCollidables` con `collidableTransformMap`, y por eso no pueden vivir en el
    ''' objeto generado, que es de solo lectura.
    ''' <para>Antes esto guardaba una `AnalisisDelSimCloth_Class` que adentro tenia UNA lista y
    ''' nada mas: una clase para envolver una lista. Se guarda la lista.</para>
    ''' </summary>
    Private ReadOnly Property Bindings As New Dictionary(Of Havok.Canon.Objects.HkObj_HclSimClothData, List(Of HclSimCollidableBinding_Class))

    Public Function CollidableBindingsDe(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of HclSimCollidableBinding_Class)
        If sim Is Nothing Then Return New List(Of HclSimCollidableBinding_Class)
        Dim l As List(Of HclSimCollidableBinding_Class) = Nothing
        If Not Bindings.TryGetValue(sim, l) Then
            l = New List(Of HclSimCollidableBinding_Class)
            Bindings(sim) = l
        End If
        Return l
    End Function
    Public ReadOnly Property BufferDefinitions As New List(Of Havok.Canon.Objects.HkObj_HclBufferDefinition)
    Public ReadOnly Property ScratchBufferDefinitions As New List(Of Havok.Canon.Objects.HkObj_HclScratchBufferDefinition)
    ''' <summary>
    ''' Lo DECODIFICADO de los dos operadores que lo necesitan, indexado por su POSICION en
    ''' `hclClothData.operators` — que es como `hclClothState.operators` los referencia.
    ''' <para>⛔ ESTO EXISTE PARA QUE LA CADENA SEA UNIFORME. `OperadoresEnOrden` guardaba el objeto
    ''' generado para cinco tipos y una clase envoltorio para estos dos, asi que el despacho, la
    ''' eleccion de estado y el bucle de siembra tenian que saber cual era cual — y hacia falta una
    ''' lista aparte (`TiposEnvueltos`) para que el objeto pelado no se colara en la cadena sin rama
    ''' que lo ejecutara. Ahora la cadena es una sola cosa y esto es el analisis, aparte.</para>
    ''' <para>El dato que guardan NO esta en el archivo: el desentrelazado SIMD de los carriles y los
    ''' pares hueso/triangulo desempaquetados. Por eso no puede vivir en el objeto generado.</para>
    ''' </summary>
    Public ReadOnly Property SkinDecodificado As New Dictionary(Of Integer, HclObjectSpaceSkinPNOperatorGraph_Class)
    ''' <summary>Idem para `hclSimpleMeshBoneDeformOperator`. Ver <see cref="SkinDecodificado"/>.</summary>
    Public ReadOnly Property DeformDecodificado As New Dictionary(Of Integer, HclSimpleMeshBoneDeformOperatorGraph_Class)

    ''' <summary>TODOS los `hclObjectSpaceSkinPNOperator` de la cadena, en el orden del archivo.
    ''' <para>⛔ SE ARMA UNA VEZ. Los diccionarios se llenan en el parseo y no vuelven a cambiar;
    ''' `EnsureState` y `ObjectSpaceSkin` piden esta lista por frame y por config, y ordenar+
    ''' materializar ahi es basura de GC por cada vuelta del render.</para></summary>
    Public ReadOnly Property ObjectSpaceSkins As List(Of HclObjectSpaceSkinPNOperatorGraph_Class)
        Get
            If _skinsEnOrden Is Nothing Then
                _skinsEnOrden = SkinDecodificado.OrderBy(Function(kv) kv.Key).Select(Function(kv) kv.Value).ToList()
            End If
            Return _skinsEnOrden
        End Get
    End Property
    Private _skinsEnOrden As List(Of HclObjectSpaceSkinPNOperatorGraph_Class)

    ''' <summary>
    ''' El skin que alimenta al deform de ESTE config: el que escribe en el buffer que el deform
    ''' declara leer (`hclSimpleMeshBoneDeformOperator.inputBufferIdx` contra
    ''' `hclObjectSpaceSkinOperator.outputBufferIndex`).
    ''' <para>⛔ NO es "el primero". Con dos skins escribiendo a buffers distintos, tomar el primero es
    ''' una moneda al aire; el archivo DECLARA cual va. Si no hay con que decidir —un solo skin, o
    ''' ninguno que coincida— se devuelve el primero, que es el comportamiento que habia.</para>
    ''' </summary>
    Public ReadOnly Property ObjectSpaceSkin As HclObjectSpaceSkinPNOperatorGraph_Class
        Get
            ' ⛔ UNA SOLA LECTURA DE LA LISTA. Se llamaba a `ObjectSpaceSkins` hasta cuatro veces por
            ' acceso, y cada llamada rearmaba la lista.
            Dim skins = ObjectSpaceSkins
            If skins.Count = 0 Then Return Nothing
            If skins.Count = 1 Then Return skins(0)
            Dim quiere = If(SimpleMeshBoneDeform Is Nothing, -1, CInt(SimpleMeshBoneDeform.Operador.InputBufferIdx))
            If quiere >= 0 Then
                For Each sk In skins
                    If sk IsNot Nothing AndAlso CInt(sk.Operador.OutputBufferIndex) = quiere Then Return sk
                Next
            End If
            Return skins(0)
        End Get
    End Property
    ''' <summary>El `HclMoveParticlesOperator` de la cadena. Si el archivo declara mas de uno gana el
    ''' ULTIMO — ver <see cref="HclClothConfigGraph_Class"/> para por que eso no afecta la
    ''' ejecucion.</summary>
    Public ReadOnly Property MoveParticles As Havok.Canon.Objects.HkObj_HclMoveParticlesOperator
        Get
            Return OperadoresEnOrden.OfType(Of Havok.Canon.Objects.HkObj_HclMoveParticlesOperator)().LastOrDefault()
        End Get
    End Property
    ''' <summary>El `HclSimulateOperator` de la cadena. Si el archivo declara mas de uno gana el
    ''' ULTIMO — ver <see cref="HclClothConfigGraph_Class"/> para por que eso no afecta la
    ''' ejecucion.</summary>
    Public ReadOnly Property Simulate As Havok.Canon.Objects.HkObj_HclSimulateOperator
        Get
            Return OperadoresEnOrden.OfType(Of Havok.Canon.Objects.HkObj_HclSimulateOperator)().LastOrDefault()
        End Get
    End Property
    ''' <summary>El `HclSimpleMeshBoneDeformOperator` de la cadena. Si el archivo declara mas de uno gana el
    ''' ULTIMO — ver <see cref="HclClothConfigGraph_Class"/> para por que eso no afecta la
    ''' ejecucion.</summary>
    Public ReadOnly Property SimpleMeshBoneDeform As HclSimpleMeshBoneDeformOperatorGraph_Class
        Get
            ' ⛔ IDEM: una vez, no por frame. Ver `ObjectSpaceSkins`.
            If Not _deformResuelto Then
                _deformUltimo = DeformDecodificado.OrderBy(Function(kv) kv.Key).Select(Function(kv) kv.Value).LastOrDefault()
                _deformResuelto = True
            End If
            Return _deformUltimo
        End Get
    End Property
    Private _deformUltimo As HclSimpleMeshBoneDeformOperatorGraph_Class
    Private _deformResuelto As Boolean
    ''' <summary>El `HclCopyVerticesOperator` de la cadena. Si el archivo declara mas de uno gana el
    ''' ULTIMO — ver <see cref="HclClothConfigGraph_Class"/> para por que eso no afecta la
    ''' ejecucion.</summary>
    Public ReadOnly Property CopyVertices As Havok.Canon.Objects.HkObj_HclCopyVerticesOperator
        Get
            Return OperadoresEnOrden.OfType(Of Havok.Canon.Objects.HkObj_HclCopyVerticesOperator)().LastOrDefault()
        End Get
    End Property
    ''' <summary>Los operadores del `hclClothData`, YA PARSEADOS y en la POSICION del archivo.
    ''' `hclClothState.operators` los referencia por indice: esta es la lista que hay que recorrer para
    ''' ejecutar la cadena. Nothing donde la clase no se ejecuta.</summary>
    Public ReadOnly Property OperadoresEnOrden As New List(Of Object)
    ''' <summary>El `HclGatherAllVerticesOperator` de la cadena. Si el archivo declara mas de uno gana el
    ''' ULTIMO — ver <see cref="HclClothConfigGraph_Class"/> para por que eso no afecta la
    ''' ejecucion.</summary>
    Public ReadOnly Property GatherAllVertices As Havok.Canon.Objects.HkObj_HclGatherAllVerticesOperator
        Get
            Return OperadoresEnOrden.OfType(Of Havok.Canon.Objects.HkObj_HclGatherAllVerticesOperator)().LastOrDefault()
        End Get
    End Property
    ''' <summary>El `HclGatherSomeVerticesOperator` de la cadena. Si el archivo declara mas de uno gana el
    ''' ULTIMO — ver <see cref="HclClothConfigGraph_Class"/> para por que eso no afecta la
    ''' ejecucion.</summary>
    Public ReadOnly Property GatherSomeVertices As Havok.Canon.Objects.HkObj_HclGatherSomeVerticesOperator
        Get
            Return OperadoresEnOrden.OfType(Of Havok.Canon.Objects.HkObj_HclGatherSomeVerticesOperator)().LastOrDefault()
        End Get
    End Property
    ''' <summary>Las POSICIONES de la cadena que ninguna clase supo parsear. Salen de la
    ''' propia cadena (un hueco en `OperadoresEnOrden`) cruzada con el arreglo del archivo.</summary>
    Public ReadOnly Property UnknownOperators As List(Of HkxVirtualObjectGraph_Class)
        Get
            Dim r As New List(Of HkxVirtualObjectGraph_Class)
            If ClothData Is Nothing Then Return r
            For i = 0 To OperadoresEnOrden.Count - 1
                If OperadoresEnOrden(i) Is Nothing Then r.Add(ClothData.Raw.OperatorsRef(i))
            Next
            Return r
        End Get
    End Property
End Class


''' <summary>
''' ⛔ UNA ENTRADA DE `constraintExecution` YA RESUELTA. El archivo trae un `int32`; el resto
''' (a que set apunta, como se llama, de que clase es) es un CALCULO sobre el arreglo de sets.
''' </summary>
Public Class EntradaDeEjecucion_Class
    Public Property EntryIndex As Integer
    Public Property Value As Integer
    Public Property IsTerminator As Boolean
    Public Property ConstraintIndex As Integer = -1
    Public Property ResolvedConstraintName As String
    Public Property ResolvedConstraintType As String
End Class


''' <summary>
''' ⛔ VIVEN ACA PORQUE ACA SE PRODUCEN. Estaban en `HclStructuredGraphParser.vb`, que era el
''' parser a mano; los dos son RESULTADOS: el binding cruza `perInstanceCollidables` con
''' `collidableTransformMap`, y la terna de triangulo agrupa `triangleIndices`, que el archivo
''' trae PLANO. Ninguno copia un campo del archivo.
''' </summary>
Public Class HclSimCollidableBinding_Class
    Public Property BoneName As String
    ''' <summary>El colisionable, COMO OBJETO GENERADO.</summary>
    Public Property Collidable As Havok.Canon.Objects.HkObj_HclCollidable
    ''' <summary>La matriz de `collidableTransformMap.offsets`, tal como la entrega el objeto
    ''' generado: 16 floats. No se re-envuelve en otro tipo.</summary>
    Public Property MatrixValues As Single()
End Class

''' <summary>
''' Los TRES indices de un triangulo del sim-cloth. La reflexion declara `hclSimClothData` con
''' `triangleIndices` como un array PLANO de uint16 y `triangleFlips` aparte: que van de a tres no
''' lo dice el formato, y por eso el agrupado vive aca y no en el objeto generado.
''' </summary>
Public Class HclTrianguloDeSim_Class
    Public Property Value0 As UShort
    Public Property Value1 As UShort
    Public Property Value2 As UShort
End Class
