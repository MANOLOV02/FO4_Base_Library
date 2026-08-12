Imports System.Collections.Generic

''' <summary>El conjunto de vertices sucios de una <c>SkinnedGeometry</c>. Reemplaza al
''' <c>HashSet(Of Integer)</c> que habia en <c>dirtyVertexIndices</c> / <c>dirtyMaskIndices</c> y se
''' comporta igual para todo lo que los ~30 lectores hacen con el: sabe representar "estan sucios TODOS"
''' sin materializar nada.
'''
''' <para>⛔ LAS DIFERENCIAS CONTRA <c>HashSet</c>, ENUMERADAS. Decir "se comporta EXACTAMENTE igual" y
''' dejar que el lector descubra las excepciones es peor que no decir nada:</para>
''' <list type="number">
''' <item>Representa "todos" en O(1) — la razon de existir.</item>
''' <item>Mutar el conjunto MIENTRAS se lo enumera se detecta, pero con TRES limites declarados: (a) al
''' TERMINAR la enumeracion y no en la vuelta donde ocurrio, (b) un <c>Exit For</c> temprano no lo detecta,
''' y (c) en modo DISPERSO el aviso lo da el <c>HashSet</c>, o sea que una mutacion que no llegue a tocarlo
''' —un <c>MarcarTodos</c> sobre un disperso VACIO, que saltea el <c>Clear</c>— pasa desapercibida. Ver la
''' nota de <c>MoveNext</c>: el chequeo por vuelta cuesta mas que todo lo que esta clase ahorra.</item>
''' </list>
''' <para>Y una que era diferencia y se cerro: <see cref="Add"/> con un indice fuera de <c>[0, n)</c> ya
''' no se pierde en silencio.</para>
'''
''' <para>⭐ POR QUE EXISTE. En modo CPU-skinning el camino de pose hacia esto POR FRAME y POR MALLA:</para>
''' <code>dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, nVertices))</code>
''' <para>o sea alocar un HashSet y HASHEAR 130.500 enteros para decir "todos". MEDIDO con
''' <c>ShadowGate --nif SG172_Serena_BattleSuit</c> (26 mallas, 130.500 vertices): <b>1,16 ms por
''' frame</b>, sobre un frame CPU de 11,21 ms ⇒ el <b>10 %</b> del frame se iba en construir un
''' conjunto cuyo CONTENIDO despues no se usa: cuando estan todos sucios la subida es completa y solo
''' se mira <c>.Count</c>. Con esta clase esa marca es O(1).</para>
'''
''' <para>⛔⛔ POR QUE UNA CLASE Y NO UN <c>Boolean TodosSucios</c> AL LADO. El campo es publico y lo
''' leen CUATRO repos en ~30 lugares (Render, MorphEngine, SkinningHelper, Wardrobe_Manager/
''' MorphingHelper, Editor_Form y tres arneses). Un flag paralelo obliga a que los 30 se acuerden de
''' consultarlo, y el que se olvide no falla: ve <c>.Count = 0</c> y silenciosamente no hace nada —el
''' peor modo de falla posible, porque el sintoma es "no se actualizo la malla" a mil lineas de
''' distancia. Con un tipo que responde <c>Count</c>, <c>Contains</c> y <c>For Each</c> igual que
''' antes, NINGUN lector cambia y no hay nada que olvidarse.</para>
'''
''' <para>⛔⭐ POR QUE EL ORDEN DE ENUMERACION IMPORTA, Y POR QUE ESTE ES BIT-IDENTICO.
''' <c>RecalculateNormalsTangentsBitangents</c> hace <c>For Each vi In geo.dirtyVertexIndices</c> y con
''' eso siembra <c>vertArr</c>, que fija el ORDEN EN QUE SE ACUMULAN los aportes de cada triangulo al
''' TBN. Sumar en punto flotante NO es asociativo: cambiar ese orden cambia los ultimos bits de las
''' normales horneadas. Este enumerador rinde <c>0, 1, ... n-1</c>, que es exactamente lo que rendia el
''' <c>HashSet</c> que reemplaza: un HashSet(Of Integer) poblado en orden creciente desde vacio ocupa
''' los slots en orden de insercion y su enumerador recorre el array de slots, de modo que devuelve la
''' secuencia de insercion. El gate <c>orden-sucios</c> de ParityGate lo COMPARA en vez de suponerlo, y
''' tiene control negativo.</para></summary>
Public NotInheritable Class ConjuntoDeSucios
    Implements IEnumerable(Of Integer)

    Private ReadOnly _set As HashSet(Of Integer)

    ''' <summary>True = "todos los indices de [0, _n) estan sucios", sin nada materializado. Mientras
    ''' vale True, <c>_set</c> se mantiene VACIO: es la unica representacion, no una copia parcial.</summary>
    Private _todos As Boolean
    Private _n As Integer

    ''' <summary>Sube con cada mutacion. Solo lo mira el <see cref="Enumerador"/>, y solo al TERMINAR: ver
    ''' la nota de <c>MoveNext</c> sobre por que no se chequea en cada vuelta.</summary>
    Private _version As Integer

    Public Sub New()
        _set = New HashSet(Of Integer)()
    End Sub

    ''' <summary>Todos los indices de [0, n) sucios. Sustituye a
    ''' <c>New HashSet(Of Integer)(Enumerable.Range(0, n))</c>.</summary>
    Public Shared Function Todos(n As Integer) As ConjuntoDeSucios
        Dim c As New ConjuntoDeSucios()
        c.MarcarTodos(n)
        Return c
    End Function

    ''' <summary>Pasa a "todos sucios" en O(1). Descartar lo que hubiera es correcto: el conjunto nuevo
    ''' CONTIENE al anterior.</summary>
    Public Sub MarcarTodos(n As Integer)
        ' ⛔ UN NO-OP NO ES UNA MUTACION. Sin este corte, `MarcarTodos` con el MISMO n sobre un conjunto
        ' que ya estaba en modo "todos" subia la version igual, y un enumerador en vuelo tiraba por algo
        ' que no cambio nada. `HashSet` tampoco sube su version cuando un `Add` no agrega.
        If _todos AndAlso _n = n Then Return
        _todos = True
        _n = n
        SubirVersion()
        If _set.Count > 0 Then _set.Clear()
    End Sub

    ''' <summary>⛔ VB TIENE EL CHEQUEO DE DESBORDAMIENTO ACTIVO (ningun .vbproj del repo pone
    ''' <c>RemoveIntegerChecks</c>), asi que un <c>_version += 1</c> pelado LANZA <c>OverflowException</c> al
    ''' llegar a <c>Integer.MaxValue</c> — desde el camino de morph de una app que se distribuye.
    ''' <para>Y es alcanzable: <c>MorphEngine</c> hace un <c>Add</c> por vertice cambiado por tick, o sea
    ''' ~130.000 incrementos por tick sobre la malla pesada; el contador es por instancia y vive lo que viva
    ''' la <c>SkinnedGeometry</c>. Dar la vuelta a 0 es correcto: lo unico que se pregunta es
    ''' <c>&lt;&gt;</c> contra el valor snapshoteado, y para que una vuelta completa lo enmascare harian falta
    ''' 2^31 mutaciones DENTRO de una sola enumeracion.</para></summary>
    Private Sub SubirVersion()
        If _version = Integer.MaxValue Then _version = 0 Else _version += 1
    End Sub

    ''' <summary>Para los pocos lugares que quieran saltear trabajo cuando la respuesta es "todos".
    ''' Nadie NECESITA consultarlo: el resto de la API ya se comporta bien sin preguntarlo.</summary>
    Public ReadOnly Property EsTodos As Boolean
        Get
            Return _todos
        End Get
    End Property

    Public ReadOnly Property Count As Integer
        Get
            Return If(_todos, _n, _set.Count)
        End Get
    End Property

    ''' <summary>Devuelve True si el indice no estaba, igual que <c>HashSet.Add</c>.
    '''
    ''' <para>⛔ EL INDICE FUERA DE <c>[0, n)</c> NO SE PIERDE. La version anterior hacia
    ''' <c>If _todos Then Return False</c> a secas, y eso miente para todo indice que el modo "todos" NO
    ''' cubre: <c>Todos(100).Add(150)</c> devolvia False, <c>Contains(150)</c> devolvia False y la
    ''' enumeracion rendia 0..99 — mientras que el <c>HashSet</c> que esta clase reemplaza habria devuelto
    ''' True y rendido el 150. O sea PERDIDA SILENCIOSA de un vertice sucio, que aguas abajo es "esa parte
    ''' de la malla no se actualizo" a mil lineas de distancia.</para>
    ''' <para>Hoy ningun llamador lo dispara (todos pasan indices &lt; <c>Vertices.Length</c> y los de
    ''' MorphEngine estan guardados por <c>&lt; count</c>), pero el contrato de la clase es "se comporta
    ''' EXACTAMENTE igual", y una excepcion no sirve: esto corre en el camino de dibujo de una app que se
    ''' distribuye. Se MATERIALIZA: se paga O(n) una vez, en un caso que no ocurre, y el resultado es
    ''' exacto.</para>
    ''' <para>⛔ LA POLITICA, PORQUE ACA HAY DOS DECISIONES QUE PARECEN CONTRADICTORIAS. <c>Add</c> NO tira
    ''' y <c>MoveNext</c> SI tira, y las dos corren en el camino de dibujo. El criterio no es "excepciones
    ''' si / excepciones no", es <b>imitar al <c>HashSet</c> que esta clase reemplaza</b>: donde el HashSet
    ''' resuelve, se resuelve (un <c>Add</c> fuera de rango es una operacion perfectamente valida para el);
    ''' donde el HashSet TIRA, se tira (mutar durante la enumeracion). Inventar un fallo nuevo esta
    ''' prohibido; silenciar uno que el tipo original daba, tambien.</para></summary>
    Public Function Add(indice As Integer) As Boolean
        If _todos Then
            If indice >= 0 AndAlso indice < _n Then Return False
            Materializar()
        End If
        SubirVersion()
        Return _set.Add(indice)
    End Function

    ''' <summary>Sale del modo "todos" poblando <c>_set</c> con [0, n). Solo lo llama <see cref="Add"/>
    ''' cuando le entra un indice que el modo compacto no puede representar.
    ''' <para>⚠️ ES O(n) EN TIEMPO UNA VEZ, PERO LA MEMORIA QUEDA. <c>HashSet.Clear()</c> no encoge sus
    ''' arrays internos, asi que ni el <c>MarcarTodos</c> siguiente ni un <c>Clear()</c> devuelven el ~1,5 MB
    ''' de una malla de 130.500 vertices: vive lo que viva la <c>SkinnedGeometry</c>. Es justo lo que esta
    ''' clase vino a evitar — y se acepta porque llega aca solo un indice fuera de rango, que hoy no ocurre
    ''' en ningun llamador. Si algun dia ocurre de rutina, esto hay que repensarlo, no ampliarlo.</para></summary>
    Private Sub Materializar()
        _todos = False
        For i = 0 To _n - 1
            _set.Add(i)
        Next
        _n = 0
    End Sub

    Public Sub Clear()
        _todos = False
        _n = 0
        SubirVersion()
        _set.Clear()
    End Sub

    Public Function Contains(indice As Integer) As Boolean
        If _todos Then Return indice >= 0 AndAlso indice < _n
        Return _set.Contains(indice)
    End Function

    ''' <summary>Enumerador por PATRON (struct, sin boxing): es el que toma <c>For Each</c> en VB, que
    ''' prefiere el metodo publico antes que la interfaz. La implementacion de
    ''' <see cref="IEnumerable(Of Integer)"/> queda igual para LINQ y para quien reciba la interfaz.</summary>
    Public Function GetEnumerator() As Enumerador
        Return New Enumerador(Me)
    End Function

    Private Function GetEnumeratorGenerico() As IEnumerator(Of Integer) Implements IEnumerable(Of Integer).GetEnumerator
        If _todos Then Return Enumerable.Range(0, _n).GetEnumerator()
        Return _set.GetEnumerator()
    End Function

    Private Function GetEnumeratorLlano() As IEnumerator Implements IEnumerable.GetEnumerator
        Return GetEnumeratorGenerico()
    End Function

    ''' <summary>⛔ Structure a proposito: <c>For Each</c> sobre el camino disperso corre por malla y por
    ''' frame; con una clase serian 26 alocaciones Gen0 por frame para nada.</summary>
    Public Structure Enumerador
        Private _hs As HashSet(Of Integer).Enumerator
        Private ReadOnly _duenio As ConjuntoDeSucios
        Private ReadOnly _todos As Boolean
        Private ReadOnly _n As Integer
        Private ReadOnly _version As Integer
        Private _i As Integer

        Friend Sub New(duenio As ConjuntoDeSucios)
            _duenio = duenio
            _todos = duenio._todos
            _n = duenio._n
            _version = duenio._version
            _i = -1
            _hs = duenio._set.GetEnumerator()
        End Sub

        Public ReadOnly Property Current As Integer
            Get
                Return If(_todos, _i, _hs.Current)
            End Get
        End Property

        ''' <summary>⛔⛔ EL CHEQUEO DE MUTACION VA AL FINAL, NO EN CADA VUELTA, Y ES A PROPOSITO.
        ''' <para>El modo disperso delega en <c>HashSet.Enumerator</c>, que ya tira
        ''' <c>InvalidOperationException</c> si el conjunto cambia. El modo "todos" no tenia NADA: mutar
        ''' durante la enumeracion quedaba INVISIBLE y el bucle seguia rindiendo la secuencia vieja. O sea
        ''' que el mismo error de programacion fallaba fuerte o fallaba mudo segun cuantos vertices hubiera
        ''' ensuciado el gesto del usuario — el peor reparto posible.</para>
        ''' <para>⭐ POR QUE NO EN CADA <c>MoveNext</c>. Esta clase existe para sacar 1,16 ms por frame; el
        ''' modo "todos" es su camino caliente y corre 130.500 vueltas por malla, 26 mallas por frame. Un
        ''' <c>_duenio._version</c> por vuelta son 3,4 millones de lecturas del heap que el JIT NO puede
        ''' izar —justamente porque el campo puede cambiar, que es el punto del chequeo— y se comerian
        ''' buena parte de lo que la clase vino a ganar. Chequear al terminar cuesta O(1) por enumeracion y
        ''' convierte igual el resultado silenciosamente equivocado en una excepcion.</para>
        ''' <para>⚠️ LIMITE DECLARADO: un <c>Exit For</c> antes del final no lo detecta, y la excepcion
        ''' llega despues de que el cuerpo del bucle ya corrio con datos viejos. No es la garantia de
        ''' <c>HashSet</c>, es la que se puede pagar en este camino.</para></summary>
        Public Function MoveNext() As Boolean
            If _todos Then
                _i += 1
                If _i < _n Then Return True
                If _duenio._version <> _version Then
                    Throw New InvalidOperationException(
                        "El conjunto de vertices sucios se modifico mientras se lo enumeraba.")
                End If
                Return False
            End If
            Return _hs.MoveNext()
        End Function
    End Structure

End Class
