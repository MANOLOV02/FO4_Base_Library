' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

Imports System.Linq

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
    ''' con la misma regla que aplica el generador (cada clase emite su `NombreDeClase`), así que no hay dos
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
        ''' ⛔⛔ UN LECTOR CANONICO: EL NOMBRE QUE DECLARA EL .EXE Y EL `Leer` DE SU CLASE.
        ''' <para>Las listas del solver eran `Type()`, y todo el que las recorria tenia que volver
        ''' del `Type` al nombre Havok. Eso lo hacia `NombreHavokDe`, que lo derivaba del NOMBRE DEL
        ''' TIPO —`t.Name.Substring(6)` y bajar la primera letra—: una SEGUNDA transcripcion de la
        ''' regla del generador, con el mismo molde que ya dejo `SizeOfClass` y `SizeOfType`
        ''' divergiendo en 227 clases de 946. Y `LeerPorTipo` buscaba el `Read` con `GetMethod`.</para>
        ''' <para>Aca no queda nada que derivar: el nombre sale de `HkObj_X.NombreDeClase`, que el
        ''' generador emite desde la reflexion, y el lector es `AddressOf HkObj_X.Leer`. Si la clase
        ''' deja de existir, no compila.</para>
        ''' </summary>
        Public NotInheritable Class LectorDeClase

            ''' <summary>El nombre que la reflexion del .exe le da a la clase.</summary>
            Public ReadOnly Property Nombre As String

            Private ReadOnly _leer As Func(Of HkxObjectGraph_Class, HkxVirtualObjectGraph_Class, Object)

            Public Sub New(nombre As String, leer As Func(Of HkxObjectGraph_Class, HkxVirtualObjectGraph_Class, Object))
                Me.Nombre = nombre
                _leer = leer
            End Sub

            ''' <summary>Lee el bloque si declara esa clase o deriva de ella; Nothing si no.</summary>
            Public Function Leer(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As Object
                If _leer Is Nothing Then Return Nothing
                Return _leer(graph, source)
            End Function

        End Class


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
                                                     lector As Func(Of HkxObjectGraph_Class, HkxVirtualObjectGraph_Class, T),
                                                     Optional fuente As Fuente = Fuente.Estaticos) _
                As List(Of (Indice As Integer, Conjunto As T))
            Dim r As New List(Of (Indice As Integer, Conjunto As T))
            ' ⛔ EL LECTOR LO TRAE EL CONSUMIDOR, NO SE RESUELVE POR REFLEXION. Aca habia una
            ' validacion en ejecucion —`NombreHavokDe(GetType(T))`, que derivaba el nombre de la clase
            ' Havok desde el NOMBRE DEL TIPO— y una lectura por `GetMethod`. Con `AddressOf
            ' HkObj_X.Leer` no hay nada que derivar ni que validar: si la clase no existe, no compila,
            ' y el modo de falla mudo que esa validacion venia a matar no puede ocurrir.
            If sim Is Nothing OrElse lector Is Nothing Then Return r
            For Each e In Crudos(sim, fuente)
                Dim o = lector(sim.Graph, e.Bloque)
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
                                                   Optional base As String = Havok.Canon.Objects.HkObj_HclConstraintSet.NombreDeClase) As List(Of String)
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
