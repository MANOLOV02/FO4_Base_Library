Imports System.Drawing
Imports System.Runtime.CompilerServices

Namespace Canon

    ''' <summary>Lo poco que NO es un campo.
    '''
    ''' <para>Los campos los da la clase generada, con el nombre que les pone el formato. Acá vive
    ''' sólo lo que hay que calcular: cosas que los dos juegos guardan distinto, banderas que el
    ''' formato no nombra, y listas que hay que filtrar.</para>
    '''
    ''' <para>La prueba de que algo pertenece acá es que no se pueda contestar mirando un campo. Si
    ''' se puede, va la propiedad generada y no una copia con otro nombre.</para></summary>
    Public Module CanonInterpretacion

        '======================================================================================
        ' Color
        '======================================================================================

        ''' <summary>Los dos juegos guardan el color distinto y hay que decirlo: Fallout 4 usa un
        ''' número de 32 bits cuyo significado depende de una bandera del propio record —o son
        ''' cuatro bytes de color, o es la fila de una paleta de remapeo— y Skyrim usa siempre
        ''' cuatro componentes, sin concepto de paleta.
        ''' <para>Aplicar la regla de un juego en el otro no da un color equivocado: da uno
        ''' inventado. Un record de Skyrim con esa bandera encendida se leería como fila de paleta y
        ''' perdería su color.</para></summary>
        <Extension>
        Public Function TieneColor(clfm As IClfm) As Boolean
            If clfm Is Nothing OrElse clfm.Node Is Nothing Then Return False
            If clfm.Node.BySignature("CNAM") Is Nothing Then Return False
            Dim fo4 = TryCast(clfm, ClfmFO4)
            If fo4 Is Nothing Then Return True
            Return (fo4.Flags And 2UI) = 0UI
        End Function

        <Extension>
        Public Function ColorDe(clfm As IClfm) As Color
            If Not clfm.TieneColor() Then Return Color.Empty
            Dim sse = TryCast(clfm, ClfmSSE)
            If sse IsNot Nothing Then
                Return Color.FromArgb(sse.ColorAlpha, sse.ColorRed, sse.ColorGreen, sse.ColorBlue)
            End If
            Dim v = DirectCast(clfm, ClfmFO4).ColorIndex
            Return Color.FromArgb(CInt((v >> 24) And &HFFUI), CInt(v And &HFFUI),
                                  CInt((v >> 8) And &HFFUI), CInt((v >> 16) And &HFFUI))
        End Function

        ''' <summary>El record apunta a una fila de la paleta en vez de llevar color. Sólo existe en
        ''' Fallout 4.</summary>
        <Extension>
        Public Function TieneIndiceDePaleta(clfm As IClfm) As Boolean
            Dim fo4 = TryCast(clfm, ClfmFO4)
            If fo4 Is Nothing OrElse fo4.Node Is Nothing Then Return False
            If fo4.Node.BySignature("CNAM") Is Nothing Then Return False
            Return (fo4.Flags And 2UI) <> 0UI
        End Function

        <Extension>
        Public Function IndiceDePaleta(clfm As IClfm) As Single
            If Not clfm.TieneIndiceDePaleta() Then Return 0.0F
            Return BitConverter.ToSingle(BitConverter.GetBytes(DirectCast(clfm, ClfmFO4).ColorIndex), 0)
        End Function

        ''' <summary>Banderas del record. El mismo campo significa cosas distintas: en Fallout 4
        ''' incluye el bit que convierte el color en índice de paleta, y en Skyrim sólo dice si el
        ''' color es elegible por el jugador.</summary>
        <Extension>
        Public Function BanderasDe(clfm As IClfm) As UInteger
            Dim fo4 = TryCast(clfm, ClfmFO4)
            If fo4 IsNot Nothing Then Return fo4.Flags
            Dim sse = TryCast(clfm, ClfmSSE)
            If sse IsNot Nothing Then Return sse.Playable
            Return 0UI
        End Function

        '======================================================================================
        ' Juego de texturas
        '======================================================================================

        ''' <summary>Ranura de textura por su número, que es lo único que significa lo mismo en los
        ''' dos juegos.
        ''' <para>Los nombres NO coinciden: la ranura 2 son las arrugas de la cara en Fallout 4 y la
        ''' máscara de entorno en Skyrim; la 7 es el mapa especular suave en uno y la máscara de luz
        ''' trasera en el otro. Por eso se pide por número y no por nombre.</para></summary>
        <Extension>
        Public Function Ranura(txst As ITxst, indice As Integer) As String
            Dim fo4 = TryCast(txst, TxstFO4)
            If fo4 IsNot Nothing Then
                Select Case indice
                    Case 0 : Return fo4.TexturesRGBADiffuse
                    Case 1 : Return fo4.TexturesRGBANormalGloss
                    Case 2 : Return fo4.TexturesRGBAWrinkles
                    Case 3 : Return fo4.TexturesRGBAGlow
                    Case 4 : Return fo4.TexturesRGBAHeight
                    Case 5 : Return fo4.TexturesRGBAEnvironment
                    Case 6 : Return fo4.TexturesRGBAMultilayer
                    Case 7 : Return fo4.TexturesRGBASmoothSpec
                End Select
                Return ""
            End If
            Dim sse = TryCast(txst, TxstSSE)
            If sse Is Nothing Then Return ""
            Select Case indice
                Case 0 : Return sse.TexturesRGBADiffuse
                Case 1 : Return sse.TexturesRGBANormalGloss
                Case 2 : Return sse.TexturesRGBAEnvironmentMaskSubsurfaceTint
                Case 3 : Return sse.TexturesRGBAGlowDetailMap
                Case 4 : Return sse.TexturesRGBAHeight
                Case 5 : Return sse.TexturesRGBAEnvironment
                Case 6 : Return sse.TexturesRGBAMultilayer
                Case 7 : Return sse.TexturesRGBABacklightMaskSpecular
            End Select
            Return ""
        End Function

        ''' <summary>Material asociado. Sólo lo declara Fallout 4.</summary>
        <Extension>
        Public Function MaterialDe(txst As ITxst) As String
            Dim fo4 = TryCast(txst, TxstFO4)
            If fo4 Is Nothing Then Return ""
            Return fo4.Material
        End Function

        <Extension>
        Public Function BanderasDe(txst As ITxst) As UShort
            Dim fo4 = TryCast(txst, TxstFO4)
            If fo4 IsNot Nothing Then Return fo4.Flags
            Dim sse = TryCast(txst, TxstSSE)
            If sse Is Nothing Then Return 0US
            Return sse.Flags
        End Function

        ''' <summary>El juego de texturas es el de una cara generada. Es el bit 1 del campo de banderas.</summary>
        <Extension>
        Public Function EsDeCaraGenerada(txst As ITxst) As Boolean
            Return (txst.BanderasDe() And 2US) <> 0US
        End Function

        '======================================================================================
        ' Parte de cabeza
        '======================================================================================

        ''' <summary>La parte usa la textura del cuerpo en vez de una propia. El formato no le pone
        ''' nombre a este bit, así que hay que nombrarlo acá: es el bit 5 del campo de banderas.</summary>
        <Extension>
        Public Function UsaTexturaDelCuerpo(hdpt As IHdpt) As Boolean
            Return (hdpt.Flags And &H20) <> 0
        End Function

        ''' <summary>Tipo de parte. Vale -1 cuando el record no lo declara, que no es lo mismo que
        ''' declararlo en cero.</summary>
        <Extension>
        Public Function TipoDeParte(hdpt As IHdpt) As Integer
            If hdpt Is Nothing OrElse hdpt.Node Is Nothing Then Return -1
            Dim n = hdpt.Node.ByFieldPath("PNAM")
            If n Is Nothing OrElse n.Value Is Nothing Then Return -1
            Try
                Return CInt(Convert.ToInt64(n.Value))
            Catch
                Return -1
            End Try
        End Function

        ''' <summary>Archivo de deformación de la primera parte de cada tipo: 0 morfos de raza,
        ''' 1 genérico, 2 morfos del editor de personaje. Si hay varias del mismo tipo vale la
        ''' primera.</summary>
        <Extension>
        Public Function ArchivoDeDeformacion(hdpt As IHdpt, tipo As UInteger) As String
            If hdpt Is Nothing Then Return ""
            For Each p In hdpt.Parts
                If p.PartPartType = tipo AndAlso Not String.IsNullOrEmpty(p.PartFileName) Then Return p.PartFileName
            Next
            Return ""
        End Function

        '======================================================================================
        ' Listas de referencias
        '======================================================================================

        ''' <summary>Miembros de una lista de formularios. Se descarta el cero, que en este formato
        ''' significa "ninguno" y no un record válido.</summary>
        <Extension>
        Public Function Miembros(flst As IFlst) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If flst Is Nothing Then Return salida
            For Each e In flst.FormIDs
                If e.FormID <> 0UI Then salida.Add(e.FormID)
            Next
            Return salida
        End Function

        <Extension>
        Public Function Prendas(otft As IOtft) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If otft Is Nothing Then Return salida
            For Each e In otft.Items
                If e.Item <> 0UI Then salida.Add(e.Item)
            Next
            Return salida
        End Function

        <Extension>
        Public Function PartesExtra(hdpt As IHdpt) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If hdpt Is Nothing Then Return salida
            For Each e In hdpt.ExtraParts
                If e.Part <> 0UI Then salida.Add(e.Part)
            Next
            Return salida
        End Function

        '======================================================================================
        ' Cambio de materiales
        '======================================================================================

        ''' <summary>Las sustituciones que hacen algo. Se descarta la que no nombra ni el material
        ''' que reemplaza ni el que pone.</summary>
        <Extension>
        Public Function Sustituciones(mswp As IMswp) As List(Of IMswp_MaterialSubstitutions)
            Dim salida As New List(Of IMswp_MaterialSubstitutions)
            If mswp Is Nothing Then Return salida
            For Each e In mswp.MaterialSubstitutions
                If e.SubstitutionOriginalMaterial <> "" OrElse e.SubstitutionReplacementMaterial <> "" Then
                    salida.Add(e)
                End If
            Next
            Return salida
        End Function

        ''' <summary>La sustitución trae índice de remapeo de color. Que no lo traiga es distinto de
        ''' que lo traiga en cero.</summary>
        <Extension>
        Public Function TieneIndiceDeColor(sub_ As IMswp_MaterialSubstitutions) As Boolean
            Return sub_ IsNot Nothing AndAlso sub_.Node IsNot Nothing AndAlso
                   sub_.Node.BySignature("CNAM") IsNot Nothing
        End Function


        ''' <summary>Agrega una sustitucion vacia al final y la devuelve, lista para escribirle.
        ''' <para>Escribe sobre el mismo arbol del record: no hay una lista aparte que despues haya
        ''' que volcar. Guardar el record emite lo que se ve.</para></summary>
        <Extension>
        Public Function AgregarSustitucion(mswp As IMswp) As IMswp_MaterialSubstitutions
            If mswp Is Nothing OrElse mswp.Node Is Nothing Then Return Nothing
            Dim vista = TryCast(mswp, CanonView)
            If vista Is Nothing Then Return Nothing
            Dim cont = mswp.Node.ByFieldPath("Material Substitutions")
            If cont Is Nothing Then Return Nothing
            Dim nuevo = WbEdit.AgregarElemento(cont, vista.Context)
            If nuevo Is Nothing Then Return Nothing
            Return New MswpFO4_MaterialSubstitutions(nuevo, vista.Context, vista.Resolver)
        End Function

        '======================================================================================
        ' Copiar y comparar records
        '======================================================================================

        ''' <summary>Una copia independiente del record, para editar sin tocar el original.</summary>
        <Extension>
        Public Function Copia(mswp As IMswp) As IMswp
            Dim v = TryCast(mswp, CanonView)
            If v Is Nothing OrElse v.Node Is Nothing Then Return Nothing
            Return New MswpFO4(v.Node.Clonar(), v.Context, v.Resolver)
        End Function

        ''' <summary>Dos records tienen el mismo contenido si producen los mismos bytes.
        ''' <para>Comparar campo por campo obliga a acordarse de cada campo, y el que se olvida es
        ''' justo el que despues aparece como "editado sin haberlo tocado". Los bytes no se olvidan
        ''' de ninguno.</para></summary>
        <Extension>
        Public Function MismoContenido(a As IMswp, b As IMswp) As Boolean
            Dim va = TryCast(a, CanonView), vb = TryCast(b, CanonView)
            If va Is Nothing OrElse vb Is Nothing Then Return va Is vb
            If va.Node Is Nothing OrElse vb.Node Is Nothing Then Return va.Node Is vb.Node
            Dim x = WbWriter.EmitBody(va.Node, va.Context)
            Dim y = WbWriter.EmitBody(vb.Node, vb.Context)
            If x.Length <> y.Length Then Return False
            For i = 0 To x.Length - 1
                If x(i) <> y(i) Then Return False
            Next
            Return True
        End Function

        ''' <summary>Reemplaza TODAS las sustituciones del record por las de la lista.
        ''' <para>Existe para el editor: la interfaz muestra una lista que el usuario reordena, agrega
        ''' y borra, y al aceptar hay que dejar el record igual a esa lista. Se rehace entera en vez
        ''' de ir sincronizando elemento por elemento, que es donde aparecen los desfases.</para></summary>
        <Extension>
        Public Sub ReemplazarSustituciones(mswp As IMswp, lista As IEnumerable(Of SustitucionEditable))
            If mswp Is Nothing OrElse mswp.Node Is Nothing Then Return
            Dim cont = mswp.Node.ByFieldPath("Material Substitutions")
            If cont Is Nothing Then Return
            cont.Children.Clear()
            If lista Is Nothing Then Return
            For Each x In lista
                Dim nuevo = mswp.AgregarSustitucion()
                If nuevo Is Nothing Then Continue For
                x.Aplicar(nuevo)
            Next
        End Sub
    End Module

    ''' <summary>Una sustitucion mientras se la edita en la interfaz.
    '''
    ''' <para>NO es un segundo modelo del record: es el buffer de un formulario, que existe entre
    ''' que el usuario abre el dialogo y acepta. Lo que se guarda sigue siendo el record, y se
    ''' rehace desde esta lista al aceptar.</para></summary>
    Public NotInheritable Class SustitucionEditable

        Public Property MaterialOriginal As String = ""
        Public Property MaterialReemplazo As String = ""
        Public Property CarpetaObsoleta As String = ""
        Public Property TieneIndiceDeColor As Boolean
        Public Property IndiceDeColor As Single

        Public Sub New()
        End Sub

        ''' <summary>Copia los valores de una sustitucion del record.</summary>
        Public Sub New(e As IMswp_MaterialSubstitutions)
            If e Is Nothing Then Return
            MaterialOriginal = e.SubstitutionOriginalMaterial
            MaterialReemplazo = e.SubstitutionReplacementMaterial
            CarpetaObsoleta = e.SubstitutionTreeFolderObsolete
            TieneIndiceDeColor = e.TieneIndiceDeColor()
            IndiceDeColor = e.SubstitutionColorRemappingIndex
        End Sub

        ''' <summary>Vuelca los valores sobre una sustitucion del record.</summary>
        Public Sub Aplicar(e As IMswp_MaterialSubstitutions)
            If e Is Nothing Then Return
            e.SubstitutionOriginalMaterial = MaterialOriginal
            e.SubstitutionReplacementMaterial = MaterialReemplazo
            e.SubstitutionTreeFolderObsolete = CarpetaObsoleta
            If TieneIndiceDeColor Then e.SubstitutionColorRemappingIndex = IndiceDeColor
        End Sub

        Public Function Copia() As SustitucionEditable
            Return New SustitucionEditable With {
                .MaterialOriginal = MaterialOriginal, .MaterialReemplazo = MaterialReemplazo,
                .CarpetaObsoleta = CarpetaObsoleta, .TieneIndiceDeColor = TieneIndiceDeColor,
                .IndiceDeColor = IndiceDeColor}
        End Function

    End Class

End Namespace
