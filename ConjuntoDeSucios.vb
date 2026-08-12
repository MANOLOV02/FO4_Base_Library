Imports System.Collections.Generic

''' <summary>El conjunto de vertices sucios de una <c>SkinnedGeometry</c>. Reemplaza al
''' <c>HashSet(Of Integer)</c> que habia en <c>dirtyVertexIndices</c> / <c>dirtyMaskIndices</c> y se
''' comporta EXACTAMENTE igual, con una sola diferencia: sabe representar "estan sucios TODOS" sin
''' materializar nada.
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
        _todos = True
        _n = n
        If _set.Count > 0 Then _set.Clear()
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

    ''' <summary>Devuelve True si el indice no estaba. En modo "todos" ya esta, asi que False, igual que
    ''' <c>HashSet.Add</c> sobre un elemento presente.</summary>
    Public Function Add(indice As Integer) As Boolean
        If _todos Then Return False
        Return _set.Add(indice)
    End Function

    Public Sub Clear()
        _todos = False
        _n = 0
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
        Private ReadOnly _todos As Boolean
        Private ReadOnly _n As Integer
        Private _i As Integer

        Friend Sub New(duenio As ConjuntoDeSucios)
            _todos = duenio._todos
            _n = duenio._n
            _i = -1
            _hs = duenio._set.GetEnumerator()
        End Sub

        Public ReadOnly Property Current As Integer
            Get
                Return If(_todos, _i, _hs.Current)
            End Get
        End Property

        Public Function MoveNext() As Boolean
            If _todos Then
                _i += 1
                Return _i < _n
            End If
            Return _hs.MoveNext()
        End Function
    End Structure

End Class
