' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

Imports System.Reflection

Namespace Havok.Canon

    ''' <summary>
    ''' ⛔⛔ LECTURA GENÉRICA **A TRAVÉS DEL OBJETO CANÓNICO**, no por un segundo camino.
    '''
    ''' <para>Un barrido de cobertura necesita poder decir "leé este bloque, sea de la clase que sea,
    ''' y contame los campos". Eso lo hacía <c>HavokGenericReader</c>, que recorría la tabla de layout
    ''' por su cuenta y leía los bytes él mismo: un SEGUNDO lector del mismo dato, con su propia
    ''' aritmética de offsets y su propia idea de qué mide cada tipo. Dos lectores del mismo archivo
    ''' es exactamente lo que este trabajo vino a sacar.</para>
    '''
    ''' <para>Acá se hace lo mismo sin ese segundo camino: se busca el objeto generado de esa clase
    ''' (<c>HkObj_&lt;Clase&gt;</c>), se lo lee con su propio <c>Read</c> — el ÚNICO lector — y se
    ''' enumeran sus propiedades con reflexión de .NET. La aritmética de offsets sigue viviendo en un
    ''' solo lugar: el código generado.</para>
    '''
    ''' <para>El nombre del tipo sale de la misma regla que usa el generador
    ''' (<c>Tools/HavokLayoutGen/gentyped.py:vb()</c>): no-alfanumérico a <c>_</c> y primera letra en
    ''' mayúscula. `hclSimClothData` → `HkObj_HclSimClothData`.</para>
    ''' </summary>
    Public NotInheritable Class HavokObjetoGenerico

        Private Sub New()
        End Sub

        ''' <summary>Los `HkObj_*` del ensamblado, indexados por el nombre de clase Havok.</summary>
        Private Shared ReadOnly _porClase As Dictionary(Of String, Type) = Construir()

        Private Shared Function Construir() As Dictionary(Of String, Type)
            Dim d As New Dictionary(Of String, Type)(StringComparer.OrdinalIgnoreCase)
            For Each t In GetType(HavokObjetoGenerico).Assembly.GetTypes()
                If t.Namespace Is Nothing OrElse Not t.Namespace.EndsWith("Canon.Objects", StringComparison.Ordinal) Then Continue For
                If Not t.Name.StartsWith("HkObj_", StringComparison.Ordinal) Then Continue For
                ' ⛔ LA REGLA DEL NOMBRE LA TIENE `HavokConstraintSets.NombreHavokDe`, no este archivo:
                ' estaba escrita dos veces (aca `t.Name.Substring(6)`, alla la inversa completa).
                d(Havok.Canon.HavokConstraintSets.NombreHavokDe(t)) = t
            Next
            Return d
        End Function

        ''' <summary>
        ''' Lee el bloque con su objeto generado y devuelve el valor de cada propiedad declarada.
        ''' Nothing si el generador no emite esa clase — que es un dato real (la clase no está en la
        ''' tabla de ESE juego), no un fallo del lector.
        ''' </summary>
        Public Shared Function Leer(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As Dictionary(Of String, Object)
            If graph Is Nothing OrElse source Is Nothing Then Return Nothing
            Dim t As Type = Nothing
            If Not _porClase.TryGetValue(If(source.ClassName, String.Empty), t) Then Return Nothing

            ' ⛔ LA BUSQUEDA DEL `Read` GENERADO VIVE UNA SOLA VEZ, y esta memoizada por tipo:
            ' `HavokConstraintSets.LeerPorTipo`. Aca habia un `GetMethod` POR LLAMADA, con la misma
            ' comprobacion de nombre de clase escrita de nuevo.
            Dim o = Havok.Canon.HavokConstraintSets.LeerPorTipo(t, graph, source)
            If o Is Nothing Then Return Nothing

            Dim r As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)
            ' ⛔ LOS QUE TIRARON NO CUENTAN COMO LEIDOS. Antes el `Catch` anotaba `Nothing` y despues
            ' `RegistrarLectura(r.Keys)` los incluia: el censo de cobertura se inflaba HACIA ARRIBA,
            ' que es la unica direccion en la que un censo de cobertura no puede equivocarse. El campo
            ' se sigue entregando en Nothing —un campo ilegible no puede tumbar el objeto entero— pero
            ' se anota aparte y no entra a la cuenta.
            Dim leidos As New List(Of String)
            For Each p In t.GetProperties(BindingFlags.Public Or BindingFlags.Instance)
                ' `Raw`, `Graph` y `Source` son plomería del objeto, no campos de la clase Havok.
                If p.Name = "Raw" OrElse p.Name = "Graph" OrElse p.Name = "Source" Then Continue For
                If p.GetIndexParameters().Length > 0 Then Continue For
                Try
                    r(p.Name) = p.GetValue(o)
                    leidos.Add(p.Name)
                Catch
                    r(p.Name) = Nothing
                End Try
            Next
            ' ⛔ LO LEIDO SE ANOTA. Este es el unico punto por el que pasa la capa de objetos entera,
            ' asi que es el unico lugar desde el que el censo de cobertura puede ver lo que el
            ' generado lee. Se registra DESPUES de leer, con los campos que de verdad se resolvieron.
            Dim lay = HavokLayout.ForGraph(graph)
            If lay IsNot Nothing Then lay.RegistrarLectura(source.ClassName, leidos)
            Return r
        End Function

    End Class

End Namespace
