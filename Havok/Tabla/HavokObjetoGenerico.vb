' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On


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
    ''' <para>Acá se hace lo mismo sin ese segundo camino y SIN REFLEXIÓN: el generador emite
    ''' <c>HkObjDelBloque</c>, un <c>Select Case</c> sobre el nombre de clase que lee el bloque
    ''' con el <c>Read</c> de su objeto — el ÚNICO lector — y devuelve el valor de cada miembro
    ''' indexado por <b>el nombre que declara la reflexión del .exe</b>. La aritmética de offsets
    ''' y la regla del nombre viven cada una en un solo lugar: el código generado.</para>
    '''
    ''' <para>Antes esto era un <c>Assembly.GetTypes()</c> indexado con <c>NombreHavokDe</c> —que
    ''' re-derivaba el nombre Havok desde el NOMBRE DEL TIPO, una segunda transcripción de lo que
    ''' el generador ya sabe— más un <c>GetMethod</c> memoizado y un <c>GetProperties</c> por
    ''' objeto, sobre 148.354 objetos por barrido.</para>
    ''' </summary>
    Public NotInheritable Class HavokObjetoGenerico

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Lee el bloque con su objeto generado y devuelve el valor de cada propiedad declarada.
        ''' Nothing si el generador no emite esa clase — que es un dato real (la clase no está en la
        ''' tabla de ESE juego), no un fallo del lector.
        ''' </summary>
        Public Shared Function Leer(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As Dictionary(Of String, Object)
            ' ⛔ SIN UNA SOLA REFLEXION, Y SIN UNA SEGUNDA TRANSCRIPCION DEL NOMBRE.
            ' Aca habia: un `Assembly.GetTypes()` que armaba `nombre -> Type` al arrancar; el nombre
            ' de clase re-derivado del NOMBRE DEL TIPO con `NombreHavokDe`; un `GetMethod("Read")`
            ' memoizado; y `GetProperties()` + `GetValue()` por objeto, sobre 148.354 objetos por
            ' barrido. Las cuatro cosas las emite el generador desde la reflexion del .exe.
            '
            ' ⛔ Y EL DICCIONARIO VIENE POR EL NOMBRE QUE DECLARA EL MOTOR, no por el nombre VB de
            ' la propiedad. Con eso se cae solo el filtro que este archivo tenia escrito por LITERAL
            ' —`p.Name = "Raw" OrElse "Graph" OrElse "Source"`—, que era la plomeria del objeto
            ' colandose en el censo de campos: `BSInterpValueModifier.source` figuraba como NO leido
            ' estando leido, porque la propiedad salia `Source_` para no chocar con la plomeria.
            Dim leidos As New List(Of String)
            Dim r = Havok.Canon.Objects.HkObjDelBloque.Campos(graph, source, leidos)
            If r Is Nothing Then Return Nothing

            ' ⛔ LO LEIDO SE ANOTA. Este es el unico punto por el que pasa la capa de objetos
            ' entera, asi que es el unico lugar desde el que el censo de cobertura puede ver lo que el
            ' generado lee. Los que tiraron NO entran: un censo de cobertura solo puede equivocarse
            ' hacia arriba, y esa es la direccion en la que no puede.
            Dim lay = HavokLayout.ForGraph(graph)
            If lay IsNot Nothing Then lay.RegistrarLectura(source.ClassName, leidos)
            Return r
        End Function

    End Class

End Namespace
