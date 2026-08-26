' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

Imports System.Linq
Imports System.Reflection

Namespace Havok.Canon

    ''' <summary>
    ''' ⛔⛔ LOS CONSTRAINT SETS, RESUELTOS POR EL NOMBRE QUE DECLARA EL BLOQUE. UNA SOLA LEY.
    '''
    ''' <para>`hclSimClothData.staticConstraintSets` (+0xB8) y `antiPinchConstraintSets` (+0xC8) son
    ''' arreglos de punteros a `hclConstraintSet`. La subclase real la dice el bloque del archivo y el
    ''' motor la resuelve por su vtable. El objeto generado entrega la lista con el tipo BASE, y los
    ''' `HkObj_*` son `NotInheritable` sin base común, así que
    ''' <c>OfType(Of HkObj_HclStandardLinkConstraintSet)</c> sobre esa lista devuelve CERO — y lo hace
    ''' EN SILENCIO, que es peor que fallar.</para>
    '''
    ''' <para>⛔ CERO LITERALES DE NOMBRE DE CLASE. Antes este archivo tenía siete envoltorios copiados
    ''' verbatim, cada uno con su string, y `HavokClothSimulation.IngerirSets` tenía OCHO ramas más con
    ''' los mismos strings. Las dos listas YA habían divergido: el solver cubría
    ''' `hclCompressibleLinkConstraintSet` y este archivo no. Ahora el nombre Havok se DERIVA del tipo
    ''' con la misma regla que aplica el generador (<see cref="NombreHavokDe"/>), así que no hay dos
    ''' listas que puedan quedar distintas.</para>
    ''' </summary>
    Public NotInheritable Class HavokConstraintSets

        Private Sub New()
        End Sub

        ''' <summary>De qué arreglo de punteros del sim-cloth salen los bloques. Los tres son
        ''' arreglos de punteros a una clase BASE, y en los tres la subclase real la dice el bloque.</summary>
        Public Enum Fuente
            Estaticos = 0
            AntiPinch = 1
            Acciones = 2
        End Enum

        ''' <summary>
        ''' ⛔ EL NOMBRE HAVOK DE LA CLASE QUE LEE UN `HkObj_*`, DERIVADO — NO ESCRITO.
        ''' `HkObj_HclStandardLinkConstraintSet` → `hclStandardLinkConstraintSet`: se saca el prefijo y
        ''' se baja la inicial.
        ''' <para>⚠️ LA MAYUSCULA DE LA PRIMERA LETRA NO ES RECUPERABLE. El generador sube la inicial al
        ''' emitir, asi que las ~100 clases de Bethesda que YA empiezan con mayuscula
        ''' (`BGSGamebryoSequenceGenerator`, `BSAlignBoneModifier`…) vuelven con la inicial minuscula.
        ''' Por eso TODA comparacion contra esto es `OrdinalIgnoreCase`, y `HavokLayoutGate` verifica
        ''' que la tabla no declare dos clases que difieran SOLO en mayusculas — que es la unica
        ''' condicion bajo la cual esa comparacion podria confundir dos clases.</para>
        ''' </summary>
        Public Shared Function NombreHavokDe(t As Type) As String
            If t Is Nothing Then Return String.Empty
            Dim n = t.Name
            If Not n.StartsWith("HkObj_", StringComparison.Ordinal) Then Return String.Empty
            n = n.Substring(6)
            If n.Length = 0 Then Return String.Empty
            Return Char.ToLowerInvariant(n(0)) & n.Substring(1)
        End Function

        ''' <summary>
        ''' El `Read(graph, source)` estático de cada `HkObj_*`, memoizado por tipo.
        ''' <para>⛔ CONCURRENTE, NO `SyncLock`. `Leer`/`LeerPorTipo` se llaman hasta 8 veces por
        ''' constraint set, 7 por operador y 4 por nodo de behavior, y el barrido HKX corre en
        ''' paralelo sobre 17.511 archivos: un lock global por lectura serializa a todos los workers
        ''' sobre un diccionario que, pasada la primera vuelta, ya no se escribe. Es el mismo remedio
        ''' que `HkxPackfileParser._offVerificado`.</para>
        ''' </summary>
        Private Shared ReadOnly _lectores As New Concurrent.ConcurrentDictionary(Of Type, MethodInfo)

        Private Shared Function LectorDe(t As Type) As MethodInfo
            Return _lectores.GetOrAdd(t,
                Function(k) k.GetMethod("Read", BindingFlags.Public Or BindingFlags.Static, Nothing,
                                        {GetType(HkxObjectGraph_Class), GetType(HkxVirtualObjectGraph_Class)}, Nothing))
        End Function

        ''' <summary>
        ''' ⛔ LA MISMA LEY SIN GENERICO, para el lector por reflexion. Devuelve el objeto leido como
        ''' `Object`, o Nothing si el bloque declara otra clase.
        ''' <para>Existe para que `HavokObjetoGenerico` no tenga su PROPIO `GetMethod("Read")` ni su
        ''' propia derivacion del nombre: la busqueda del `Read` generado y la regla `HkObj_` ↔ clase
        ''' Havok viven una sola vez, acá.</para>
        ''' </summary>
        Public Shared Function LeerPorTipo(t As Type, graph As HkxObjectGraph_Class,
                                           crudo As HkxVirtualObjectGraph_Class) As Object
            If t Is Nothing Then Throw New ArgumentNullException(NameOf(t))
            ' ⛔ UN `T` QUE NO SEA `HkObj_*` NO PUEDE DEVOLVER Nothing EN SILENCIO. Ese es exactamente
            ' el modo de falla del `OfType`/`TryCast` mudo: la lista sale vacia y el build sigue verde.
            Dim esperado = NombreHavokDe(t)
            If esperado.Length = 0 Then
                Throw New ArgumentException($"'{t.Name}' no es un objeto generado (`HkObj_*`): no hay clase Havok que resolver.")
            End If
            If graph Is Nothing OrElse crudo Is Nothing Then Return Nothing
            If Not String.Equals(crudo.ClassName, esperado, StringComparison.OrdinalIgnoreCase) Then Return Nothing
            ' ⛔ TIRA, NO DEVUELVE Nothing. Un `HkObj_*` sin `Read(graph, source)` publico es un
            ' defecto del GENERADOR, y confundirlo con "el bloque declara otra clase" es el modo de
            ' falla mudo que esta clase existe para matar: `ParseOperadorPorClase` haria desaparecer
            ' el operador de la cadena y el log diria la causa equivocada.
            Dim m = LectorDe(t)
            If m Is Nothing Then
                Throw New ArgumentException($"'{t.Name}' no expone `Read(graph, source)`: el generador no lo emitio.")
            End If
            Try
                Return m.Invoke(Nothing, {graph, crudo})
            Catch ex As Reflection.TargetInvocationException When ex.InnerException IsNot Nothing
                ' ⛔ SIN ENVOLTORIO Y SIN PERDER EL ORIGEN. `Invoke` convierte cualquier fallo de
                ' `Read` en `TargetInvocationException` y borra la causa. Y `Throw ex` a secas
                ' RE-CAPTURA el stack: el trace arrancaria aca en vez de en el `Read` que fallo.
                ' `ExceptionDispatchInfo` la relanza conservando el frame original.
                Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' ⛔ LA LEY: el bloque leído como <typeparamref name="T"/>, SÓLO si el bloque declara esa
        ''' clase. Nothing si declara otra. Es lo único que hace la resolución por nombre en todo el
        ''' árbol, y por eso no hay dos copias que puedan divergir.
        ''' </summary>
        Public Shared Function Leer(Of T As Class)(graph As HkxObjectGraph_Class,
                                                   crudo As HkxVirtualObjectGraph_Class) As T
            ' ⛔ EL CUERPO ES `LeerPorTipo`, NO UNA COPIA. Aca habia el mismo cuerpo escrito por segunda
            ' vez —derivar el nombre, comparar `ClassName`, `LectorDe`, `Invoke`, `ExceptionDispatchInfo`—
            ' adentro del archivo que se presenta como la unica copia de esa ley. Y ya habian DIVERGIDO:
            ' con un `T` que no es `HkObj_*` esta tiraba y `LeerPorTipo` devolvia Nothing en silencio.
            ' Esto NO es un envoltorio: es la firma TIPADA de la misma funcion — lo unico que agrega es
            ' el `TryCast`, que es lo que el generico existe para dar.
            Return TryCast(LeerPorTipo(GetType(T), graph, crudo), T)
        End Function

        ''' <summary>
        ''' Los bloques CRUDOS del arreglo que pide <paramref name="fuente"/>, en el ORDEN del archivo
        ''' y con su índice — `hclSimulateOperator.constraintExecution` los referencia por ese índice.
        ''' </summary>
        Public Shared Function Crudos(sim As Havok.Canon.Objects.HkObj_HclSimClothData,
                                      Optional fuente As Fuente = Fuente.Estaticos) _
                As List(Of (Indice As Integer, Bloque As HkxVirtualObjectGraph_Class))
            Dim r As New List(Of (Indice As Integer, Bloque As HkxVirtualObjectGraph_Class))
            If sim Is Nothing Then Return r
            ' ⛔ LA COTA ES EL CONTEO QUE DECLARA EL ARREGLO, NO EL LARGO DE LA LISTA MATERIALIZADA.
            ' El objeto generado COMPACTA: los punteros que no resuelven no entran a la lista tipada
            ' (`If o IsNot Nothing Then _..._c.Add(o)`). Acotando con ese `.Count`, un arreglo de 5 con
            ' el elemento 2 sin fixup deja el indice CRUDO 4 SIN VISITAR — y eso no es solo el log:
            ' `porSet` se queda sin esa clave y `constraintExecution`, que indexa por POSICION en el
            ' arreglo del archivo, la referencia igual. El set desaparece del solve en silencio.
            Dim n = CuantosDeclara(sim, fuente)
            ' ⛔ EL ACCESOR SE RESUELVE UNA VEZ, no por elemento. `CrudoEn` volvia a preguntar
            ' `CuantosDeclara` en cada llamada: eran 2N+1 lecturas de la cabecera por sim-cloth y
            ' quedan N+1 — el `*Ref(i)` generado la relee adentro de `*ItemOffset`, y eso no lo puede
            ' cambiar este lado. La LEY (la cota) no cambia: es la misma `n`.
            Dim refDe As Func(Of Integer, HkxVirtualObjectGraph_Class)
            Select Case fuente
                Case Fuente.AntiPinch : refDe = AddressOf sim.Raw.AntiPinchConstraintSetsRef
                Case Fuente.Acciones : refDe = AddressOf sim.Raw.ActionsRef
                Case Else : refDe = AddressOf sim.Raw.StaticConstraintSetsRef
            End Select
            For i = 0 To n - 1
                Dim crudo = refDe(i)
                If crudo Is Nothing Then Continue For
                r.Add((i, crudo))
            Next
            ' ⛔ UN PUNTERO QUE NO RESUELVE ES UN HUECO Y SE DICE. El arreglo declara `n` elementos y
            ' este bucle devuelve los que se pudieron resolver: la diferencia es un set que el archivo
            ' declara, que `constraintExecution` puede referenciar por POSICION, y que el solver no va
            ' a ver nunca. Callarlo es la forma de que la cobertura parezca total.
            If FO4_Base_Library.Logger.Enabled AndAlso r.Count <> n Then
                Dim q1 = n - r.Count, q2 = n, q3 = fuente
                FO4_Base_Library.Logger.LogLazy(Function() $"[CLOTH-SETSINRESOLVER] ⛔ {q1} de {q2} punteros de `{q3}` no resuelven a ningun bloque: esos sets quedan FUERA del solve")
            End If
            Return r
        End Function

        ''' <summary>
        ''' ⛔ EL ÚNICO SITIO DEL ÁRBOL QUE TOCA `staticConstraintSetsRef` y `antiPinchConstraintSetsRef`
        ''' — los arreglos de `hclSimClothData` cuyos elementos son subclases y cuyo nombre es SUYO.
        ''' <para>`.Raw` acá no es un atajo sino un hueco del GENERADOR: el objeto entrega la lista
        ''' tipada con la clase BASE y por eso no puede decir la subclase; el `*Ref(i)` devuelve el
        ''' bloque, que SÍ trae el `ClassName` que el motor resuelve por vtable. Cuando el generador
        ''' emita los `*Ref` sobre `HkObj_*`, esta función es lo único que hay que cambiar.</para>
        ''' <para>⛔ LA EXCLUSIVIDAD ES EXIGIBLE, NO DECLARATIVA: `HavokLayoutGate` fase 6 falla si esos
        ''' DOS `*Ref(` aparecen fuera de este archivo. Antes esto decía "el único sitio del árbol" a
        ''' secas y era FALSO — medido, había 14 usos de `.Raw.*Ref(` fuera de `Generated/`.</para>
        ''' <para>⚠️ `actionsRef` NO entra en esa exigencia, y esta función igual lo sirve
        ''' (<see cref="Fuente.Acciones"/>). MEDIDO: `hclClothData` declara `actions` en +0x68 y
        ''' `hclSimClothData` declara OTRO en +0xE8, así que el nombre solo no dice de qué clase es el
        ''' `.Raw` — y `HkxParserTool` vuelca legítimamente el del `hclClothData`. Un detector por
        ''' nombre marcaría ese uso como violación, así que el límite queda DICHO en vez de tapado con
        ''' una excepción.</para>
        ''' <para>⚠️ LOS OTROS `*Ref` NO son de esta función y no tienen una ley única todavía:
        ''' `hclClothData.operators` y `.bufferDefinitions` los lee `HclClothPackageParser`, y
        ''' `.simClothDatas`, `.transformSetDefinitions` y `hclSimClothData.perInstanceCollidables` los
        ''' lee `HkxParserTool` para volcarlos. Es un hueco DICHO, no tapado.</para>
        ''' </summary>
        Public Shared Function CrudoEn(sim As Havok.Canon.Objects.HkObj_HclSimClothData, indice As Integer,
                                       Optional fuente As Fuente = Fuente.Estaticos) As HkxVirtualObjectGraph_Class
            If sim Is Nothing OrElse indice < 0 OrElse indice >= CuantosDeclara(sim, fuente) Then Return Nothing
            Select Case fuente
                Case Fuente.AntiPinch : Return sim.Raw.AntiPinchConstraintSetsRef(indice)
                Case Fuente.Acciones : Return sim.Raw.ActionsRef(indice)
                Case Else : Return sim.Raw.StaticConstraintSetsRef(indice)
            End Select
        End Function

        ''' <summary>
        ''' Cuantos elementos DECLARA el arreglo, del header — no cuantos materializo el objeto.
        ''' <para>⛔ LA DIFERENCIA ES REAL: el generado saltea los punteros que no resuelven, asi que
        ''' `sim.StaticConstraintSets.Count` puede ser MENOR que el conteo declarado. Acotar el indice
        ''' CRUDO con ese numero corta la COLA del arreglo, y `constraintExecution` indexa por POSICION
        ''' en el arreglo del archivo: el set de la cola se pierde del solve sin que nada avise.</para>
        ''' </summary>
        Private Shared Function CuantosDeclara(sim As Havok.Canon.Objects.HkObj_HclSimClothData,
                                               fuente As Fuente) As Integer
            If sim Is Nothing Then Return 0
            Select Case fuente
                Case Fuente.AntiPinch : Return sim.Raw.AntiPinchConstraintSetsCount
                Case Fuente.Acciones : Return sim.Raw.ActionsCount
                Case Else : Return sim.Raw.StaticConstraintSetsCount
            End Select
        End Function

        ''' <summary>
        ''' Los sets de tipo <typeparamref name="T"/> que el sim-cloth declara, con su índice en el
        ''' arreglo. Reemplaza a los siete envoltorios copiados verbatim que había antes.
        ''' </summary>
        Public Shared Function SetsDe(Of T As Class)(sim As Havok.Canon.Objects.HkObj_HclSimClothData,
                                                     Optional fuente As Fuente = Fuente.Estaticos) _
                As List(Of (Indice As Integer, Conjunto As T))
            Dim r As New List(Of (Indice As Integer, Conjunto As T))
            ' ⛔ EL TIPO SE VALIDA ANTES DEL BUCLE. Estaba dentro de `Leer`, que solo corre por cada
            ' elemento: con un sim-cloth de CERO sets, un `T` que no fuera `HkObj_*` devolvia lista
            ' vacia SIN LANZAR — el mismo modo de falla mudo que esta clase existe para matar.
            If NombreHavokDe(GetType(T)).Length = 0 Then
                Throw New ArgumentException($"'{GetType(T).Name}' no es un objeto generado (`HkObj_*`): no hay clase Havok que resolver.")
            End If
            If sim Is Nothing Then Return r
            For Each e In Crudos(sim, fuente)
                Dim o = Leer(Of T)(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add((e.Indice, o))
            Next
            Return r
        End Function

        ''' <summary>
        ''' ⛔⛔ LA LISTA CERRADA DE SUBCLASES, SACADA DE LA REFLEXIÓN — no de lo que se me ocurrió.
        ''' Son las clases que declaran `base` como padre (por omisión `hclConstraintSet`). Existe
        ''' para que un gate pueda EXIGIR que la ingesta las cubra a todas, en vez de enumerar las
        ''' que ya conozco.
        ''' <para>⛔ `base` ES PARÁMETRO PORQUE LA MISMA PREGUNTA SE HACE PARA CUATRO FAMILIAS:
        ''' `hclConstraintSet`, `hclOperator`, `hclShape` y `hclAction`. `ClothCoverMode` clasificaba
        ''' la primera con esta lista y las otras tres por SUBCADENA del nombre — que es el defecto
        ''' que dejó invisible a `hclVolumeConstraintMx` —, y `HavokLayoutGate` tenía este cuerpo
        ''' copiado con el comentario "misma ley que SubclasesDeclaradas" encima.</para>
        ''' </summary>
        Public Shared Function SubclasesDeclaradas(lay As HavokLayout,
                                                   Optional base As String = "hclConstraintSet") As List(Of String)
            Dim r As New List(Of String)
            If lay Is Nothing Then Return r
            For Each c In lay.ClassNames
                If String.Equals(c, base, StringComparison.OrdinalIgnoreCase) Then Continue For
                If lay.DerivaDe(c, base) Then r.Add(c)
            Next
            r.Sort(StringComparer.OrdinalIgnoreCase)
            Return r
        End Function

    End Class

End Namespace
