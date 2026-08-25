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
                d(t.Name.Substring(6)) = t
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

            Dim mRead = t.GetMethod("Read", BindingFlags.Public Or BindingFlags.Static,
                                    Nothing, {GetType(HkxObjectGraph_Class), GetType(HkxVirtualObjectGraph_Class)}, Nothing)
            If mRead Is Nothing Then Return Nothing
            Dim o = mRead.Invoke(Nothing, {graph, source})
            If o Is Nothing Then Return Nothing

            Dim r As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)
            For Each p In t.GetProperties(BindingFlags.Public Or BindingFlags.Instance)
                ' `Raw`, `Graph` y `Source` son plomería del objeto, no campos de la clase Havok.
                If p.Name = "Raw" OrElse p.Name = "Graph" OrElse p.Name = "Source" Then Continue For
                If p.GetIndexParameters().Length > 0 Then Continue For
                Try
                    r(p.Name) = p.GetValue(o)
                Catch
                    ' Un campo ilegible no puede tumbar la lectura del objeto entero: se deja
                    ' anotado en Nothing y el barrido lo cuenta como campo presente pero sin valor.
                    r(p.Name) = Nothing
                End Try
            Next
            Return r
        End Function

    End Class

End Namespace
