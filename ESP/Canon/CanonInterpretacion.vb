Imports System.Drawing
Imports System.IO
Imports System.Linq
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

        ''' <summary>Las categorías de plantilla de un NPC, en el orden en que las declara la
        ''' enumeración. DERIVADAS de la enumeración, no listadas a mano: agregar una categoría la
        ''' incluye sola, que es lo contrario de un arreglo literal que hay que acordarse de tocar.
        '''
        ''' <para>Lo que cambia respecto de llamar a <c>[Enum].GetValues</c> en cada uso es que la
        ''' reflexión y el arreglo se pagan UNA vez y no una por NPC. Los cuatro sitios que
        ''' recorrían las categorías lo hacían por NPC, y la clasificación de la lista las recorre
        ''' dos veces por cada uno.</para></summary>
        ''' <para>⛔ Se expone como lista de SOLO LECTURA, no como arreglo. `ReadOnly` protege la
        ''' referencia, no el contenido: `[Enum].GetValues` devolvia un arreglo NUEVO en cada
        ''' llamada, y compartir uno solo hace que cualquiera de los tres exes —o de los ~130
        ''' arneses— pueda escribirle un elemento y dejar corrupta, para todo el proceso, la
        ''' clasificacion de la lista, los checkboxes de categoria y el filtro avanzado.</para>
        Public ReadOnly CategoriasDePlantilla As IReadOnlyList(Of NPC_TemplateCategory) =
            Array.AsReadOnly(DirectCast([Enum].GetValues(GetType(NPC_TemplateCategory)), NPC_TemplateCategory()))

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

        ''' <summary>Escribe una REFERENCIA OPCIONAL, o SACA el campo si quedó sin valor. Es LA ley del
        ''' volcado de un editor, y vive acá — no repetida en cada campo ni en cada formulario.
        ''' <para>⛔ «Sin valor» NO se graba como un 0. Un subrecord de referencia presente apuntando a
        ''' nada no es un estado del formato —xEdit lo marca «Found a NULL reference»— y no existe en el
        ''' ecosistema: medido sobre 175 plugins y 3.482.830 records, CERO referencias nulas en
        ''' ARMA/ARMO/NPC_/RACE/HDPT, y las 30 bajas de una referencia opcional son 30 POR OMISIÓN.</para>
        ''' <para>Los tres casos salen de la MISMA línea, apoyada en dos propiedades del motor: leer un
        ''' campo AUSENTE devuelve 0, y quitar uno que no está es un no-op. Entonces: campo ausente + caja
        ''' vacía ⇒ sigue ausente · campo con valor + caja vacía ⇒ se saca · caja con valor ⇒ se escribe.
        ''' Antes se escribía SIEMPRE, y por eso un ARMA salía del editor con hasta DIEZ referencias nulas
        ''' que nadie había tocado.</para>
        ''' <para>⛔ NO va para campos REQUERIDOS de hecho, como el <c>RNAM</c> de ARMA/ARMO: está en 5.825
        ''' de 5.825 y no declara NULL, así que una caja vacía ahí no es «borrar» sino entrada inválida, y
        ''' eso lo ataja la validación del editor.</para></summary>
        Public Sub PonerReferenciaOpcional(nuevo As UInteger, poner As Action(Of UInteger), quitar As Action)
            If nuevo = 0UI Then quitar() Else poner(nuevo)
        End Sub

        '======================================================================================
        ' Plantilla de cuerpo (armadura)
        '======================================================================================

        ''' <summary>Cuál de las dos ramas de la unión trajo ESTE record: la de Skyrim (BODT) o la
        ''' común (BOD2). Es LA pregunta de la unión, y vive acá una sola vez porque la contestan los
        ''' dos lados —leer el slot mask y escribirlo—: escrita dos veces, el día que una cambie la otra
        ''' queda vieja y nadie se entera.
        ''' <para>El esquema declara `Wb.RUnion("Biped Body Template", BOD2, BODT)`: el record trae UNA
        ''' de las dos, nunca las dos, y guardan el mismo campo en el mismo offset.</para></summary>
        Private Function UsaRamaBodt(vista As CanonView, presenteBod2 As Boolean, presenteBodt As Boolean) As Boolean
            Return vista IsNot Nothing AndAlso Not presenteBod2 AndAlso presenteBodt
        End Function

        ''' <summary>El slot mask (First Person Flags) de la plantilla de cuerpo. En Fallout 4
        ''' siempre sale de BOD2; en Skyrim el record trae BOD2 O BODT —nunca las dos—, y las dos
        ''' firmas guardan el mismo campo en el mismo offset, así que hay que mirar cuál de las dos
        ''' trajo el record en vez de asumir siempre BOD2.</summary>
        <Extension>
        Public Function SlotMaskDe(armo As IArmo) As UInteger
            If armo Is Nothing Then Return 0UI
            Dim sse = TryCast(armo, ArmoSSE)
            If sse IsNot Nothing AndAlso UsaRamaBodt(sse, armo.BipedBodyTemplateFirstPersonFlagsPresente,
                                                    sse.BodyTemplateFirstPersonFlagsPresente) Then
                Return sse.BodyTemplateFirstPersonFlags
            End If
            Return armo.BipedBodyTemplateFirstPersonFlags
        End Function

        ''' <summary>Mismo caso que <see cref="SlotMaskDe(IArmo)"/> pero para ARMA.</summary>
        <Extension>
        Public Function SlotMaskDe(arma As IArma) As UInteger
            If arma Is Nothing Then Return 0UI
            Dim sse = TryCast(arma, ArmaSSE)
            If sse IsNot Nothing AndAlso UsaRamaBodt(sse, arma.BipedBodyTemplateFirstPersonFlagsPresente,
                                                    sse.BodyTemplateFirstPersonFlagsPresente) Then
                Return sse.BodyTemplateFirstPersonFlags
            End If
            Return arma.BipedBodyTemplateFirstPersonFlags
        End Function

        ''' <summary>Escribe el slot mask EN LA RAMA QUE EL RECORD TRAE. Gemelo exacto de
        ''' <see cref="SlotMaskDe(IArmo)"/>, y por eso los dos preguntan por <c>UsaRamaBodt</c>.
        ''' <para>⛔ Escribir siempre en BOD2 NO es equivalente. Sobre un record que trae BODT, el
        ''' setter de BOD2 crea la rama nueva sin sacar la vieja —<c>WbEdit</c> acierta el miembro por
        ''' la firma de una de las ramas y <c>WbRUnionDef.CreateRequired</c> devuelve siempre
        ''' <c>Members(0)</c>, o sea BOD2—, y el árbol queda con LAS DOS. Al releerlo, BODT consume la
        ''' unión, BOD2 agota el cursor de miembros y <b>todo lo que sigue cae en passthrough</b>
        ''' (<c>WbReader</c>): raza, prioridades, modelos, NAM0-3, SNDD y ONAM. Ese record además ya no
        ''' se puede volver a guardar, porque cada passthrough hace tirar al emisor.</para>
        ''' <para>No es un caso raro: medido sobre <c>Skyrim.esm</c> + 3 DLC, <b>BODT está en 916 de
        ''' 1.083 ARMA (85 %)</b>. En ARMO manda la otra rama —BOD2 en 3.669 de 3.679—, así que la
        ''' respuesta tampoco es "por juego": la decide el record.</para></summary>
        <Extension>
        Public Sub PonerSlotMaskEn(armo As IArmo, valor As UInteger)
            If armo Is Nothing Then Return
            Dim sse = TryCast(armo, ArmoSSE)
            If sse IsNot Nothing AndAlso UsaRamaBodt(sse, armo.BipedBodyTemplateFirstPersonFlagsPresente,
                                                    sse.BodyTemplateFirstPersonFlagsPresente) Then
                sse.BodyTemplateFirstPersonFlags = valor
                Return
            End If
            armo.BipedBodyTemplateFirstPersonFlags = valor
        End Sub

        ''' <summary>Mismo caso que <see cref="PonerSlotMaskEn(IArmo, UInteger)"/> pero para ARMA.</summary>
        <Extension>
        Public Sub PonerSlotMaskEn(arma As IArma, valor As UInteger)
            If arma Is Nothing Then Return
            Dim sse = TryCast(arma, ArmaSSE)
            If sse IsNot Nothing AndAlso UsaRamaBodt(sse, arma.BipedBodyTemplateFirstPersonFlagsPresente,
                                                    sse.BodyTemplateFirstPersonFlagsPresente) Then
                sse.BodyTemplateFirstPersonFlags = valor
                Return
            End If
            arma.BipedBodyTemplateFirstPersonFlags = valor
        End Sub


        ''' <summary>Los complementos de armadura que declara este ARMO, en orden.
        ''' <para>Los dos juegos lo declaran distinto: uno guarda pares (indice, referencia) y el otro una
        ''' lista donde la POSICION es el indice. Quien pregunta "que complementos tiene" no tiene por
        ''' que saber eso, y escribirlo en cada consumidor es garantizar que alguno se olvide de un
        ''' juego.</para></summary>
        <Extension>
        Public Function ComplementosDe(armo As IArmo) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If armo Is Nothing Then Return salida
            Dim fo4 = TryCast(armo, ArmoFO4)
            If fo4 IsNot Nothing Then
                For Each m In fo4.Models
                    If m.ModelArmorAddon <> 0UI Then salida.Add(m.ModelArmorAddon)
                Next
                Return salida
            End If
            Dim sse = TryCast(armo, ArmoSSE)
            If sse IsNot Nothing Then
                For Each a In sse.Armature
                    If a.ModelFilename <> 0UI Then salida.Add(a.ModelFilename)
                Next
            End If
            Return salida
        End Function

        ''' <summary>Las razas adicionales que acepta este complemento.</summary>
        <Extension>
        Public Function RazasAdicionalesDe(arma As IArma) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If arma Is Nothing Then Return salida
            For Each r In arma.AdditionalRaces
                If r.Race <> 0UI Then salida.Add(r.Race)
            Next
            Return salida
        End Function

        '======================================================================================
        ' Object Template (OBTS) y attach-parent-slots del ARMO de Fallout 4
        '======================================================================================
        ' Las combinaciones se LEEN de la vista del record: acá no hay conversor ni lista
        ' paralela. Lo único que vive acá son las operaciones que la vista sola no da: llegar a la
        ' lista del NPC_ por su interfaz -la clase del record no está en INpc-, dejar el bloque
        ' igual a una lista que puede venir de otro record, y las altas y bajas que un editor
        ' necesita sobre las listas de adentro.

        <Extension>
        Public Function ReadAttachParentSlots(fo4 As ArmoFO4) As List(Of UInteger)
            If fo4 Is Nothing Then Return New List(Of UInteger)
            Return fo4.AttachParentSlots.Select(Function(x) x.Keyword).ToList()
        End Function

        <Extension>
        Public Sub WriteAttachParentSlots(fo4 As ArmoFO4, ids As IEnumerable(Of UInteger))
            While fo4.AttachParentSlots.Count > 0
                If Not fo4.QuitarAttachParentSlots(0) Then Exit While
            End While
            If ids Is Nothing Then Return
            For Each slotFid In ids
                Dim e = fo4.AgregarAttachParentSlots()
                If e IsNot Nothing Then e.Keyword = slotFid
            Next
        End Sub

        ''' <summary>Las combinaciones de un NPC_. La lista generada vive en la clase del record,
        ''' así que hay que bajar a ella; los elementos se devuelven por la interfaz de forma, que
        ''' es la que ARMO y NPC_ comparten -es el mismo bloque OBTE/OBTS en los dos.</summary>
        <Extension>
        Public Function CombinacionesDelNpc(npc As INpc) As IReadOnlyList(Of IBloque_Combinations)
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return Array.Empty(Of IBloque_Combinations)()
            Return nf.Combinations
        End Function

        ''' <summary>Deja el Object Template del ARMO con exactamente estas combinaciones y en ese
        ''' orden. <para>Cada una se clona ENTERA: se copia el NODO, no campo por campo, así viaja
        ''' también lo que ninguna propiedad muestra -los tramos de relleno, por ejemplo- y el
        ''' record vuelve a emitir los mismos bytes. Se clona TODO antes de vaciar, porque la lista
        ''' de origen puede ser la del propio record.</para> <para>El primer elemento se agrega con
        ''' la API generada y recién después se cuelgan los clones: agregarlo es lo que asegura de
        ''' una vez la ruta del bloque y el contador que el formato declara aparte. Los contadores
        ''' de cada arreglo (Include Count, Property Count) no se tocan: se recalculan solos al
        ''' escribir.</para></summary>
        <Extension>
        Public Sub ReemplazarCombinations(fo4 As ArmoFO4,
                                          combos As IEnumerable(Of IBloque_Combinations))
            If fo4 Is Nothing Then Return
            Dim clones = ClonarCombinaciones(combos)
            While fo4.Combinations.Count > 0
                If Not fo4.QuitarCombinations(0) Then Exit While
            End While
            If clones.Count = 0 Then Return
            Dim semilla = fo4.AgregarCombinations()
            If semilla Is Nothing Then Return
            ColgarEnElContenedor(semilla.Node, clones)
        End Sub

        ''' <summary>Lo mismo para el NPC_, que declara el mismo bloque.</summary>
        <Extension>
        Public Sub ReemplazarCombinations(npc As INpc,
                                          combos As IEnumerable(Of IBloque_Combinations))
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return
            Dim clones = ClonarCombinaciones(combos)
            While nf.Combinations.Count > 0
                If Not nf.QuitarCombinations(0) Then Exit While
            End While
            If clones.Count = 0 Then Return
            Dim semilla = nf.AgregarCombinations()
            If semilla Is Nothing Then Return
            ColgarEnElContenedor(semilla.Node, clones)
        End Sub

        Private Function ClonarCombinaciones(
                combos As IEnumerable(Of IBloque_Combinations)) As List(Of WbNode)
            Dim salida As New List(Of WbNode)
            If combos Is Nothing Then Return salida
            For Each c In combos
                If c Is Nothing OrElse c.Node Is Nothing Then Continue For
                salida.Add(c.Node.Clonar())
            Next
            Return salida
        End Function

        ''' <summary>Deja el contenedor del elemento recién agregado con exactamente estos nodos.
        ''' El elemento semilla se va con el resto: sólo estaba para que el bloque quedara armado y
        ''' para que el contador que el formato declara aparte tuviera dónde ir.</summary>
        Private Sub ColgarEnElContenedor(semilla As WbNode, clones As List(Of WbNode))
            If semilla Is Nothing Then Return
            Dim cont = semilla.Parent
            If cont Is Nothing Then Return
            cont.LimpiarHijos()
            For Each n In clones
                cont.AddChild(n)
            Next
        End Sub

        ''' <summary>Agrega al ARMO una combinación igual a <paramref name="modelo"/> -o una vacía
        ''' si no se le pasa ninguno- y la devuelve. <para>Es lo que necesita un editor: una vista
        ''' no existe sin un nodo, y un nodo no existe fuera del árbol de un record, así que la
        ''' combinación "suelta" que el usuario arma antes de aceptar se sostiene sobre una COPIA
        ''' del record que se está editando. La copia hereda el contexto del original -entre otras
        ''' cosas, si el archivo guarda los textos en tablas de idioma-, que es lo que hace que el
        ''' nombre se lea igual que en el record.</para></summary>
        <Extension>
        Public Function AgregarCombinacion(fo4 As ArmoFO4,
                                           modelo As IBloque_Combinations) As IBloque_Combinations
            If fo4 Is Nothing Then Return Nothing
            Dim nueva = fo4.AgregarCombinations()
            VolcarCombinacion(nueva, modelo)
            Return nueva
        End Function

        ''' <summary>Lo mismo sobre un NPC_.</summary>
        <Extension>
        Public Function AgregarCombinacion(npc As INpc,
                                           modelo As IBloque_Combinations) As IBloque_Combinations
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return Nothing
            Dim nueva = nf.AgregarCombinations()
            VolcarCombinacion(nueva, modelo)
            Return nueva
        End Function

        ''' <summary>Deja la combinación destino con el contenido de la de origen. Se clonan los
        ''' NODOS que cuelgan de ella, no campo por campo: así viaja también lo que ninguna
        ''' propiedad muestra.</summary>
        Private Sub VolcarCombinacion(destino As IBloque_Combinations,
                                      origen As IBloque_Combinations)
            If destino Is Nothing OrElse destino.Node Is Nothing Then Return
            If origen Is Nothing OrElse origen.Node Is Nothing Then Return
            destino.Node.LimpiarHijos()
            For Each h In origen.Node.Children
                destino.Node.AddChild(h.Clonar(destino.Node))
            Next
        End Sub

        ''' <summary>Las combinaciones las declaran DOS records con la misma forma, y las listas de
        ''' adentro -keywords, includes, properties- sólo se pueden agregar y quitar desde la clase
        ''' generada de cada uno: la interfaz de forma las expone para leer. Estas dos funciones son
        ''' el único lugar donde se baja a la clase concreta, y hay exactamente dos porque son
        ''' exactamente dos los records que declaran el bloque.</summary>
        Private Function ComoArmo(combo As IBloque_Combinations) As ArmoFO4_Combinations
            Return TryCast(combo, ArmoFO4_Combinations)
        End Function

        Private Function ComoNpc(combo As IBloque_Combinations) As NpcFO4_Combinations
            Return TryCast(combo, NpcFO4_Combinations)
        End Function

        ''' <summary>Deja la combinación con exactamente estos keywords, en ese orden.</summary>
        <Extension>
        Public Sub ReemplazarKeywordsDeCombinacion(combo As IBloque_Combinations,
                                                   ids As IEnumerable(Of UInteger))
            Dim a = ComoArmo(combo)
            Dim n = ComoNpc(combo)
            If a IsNot Nothing Then
                While a.Keywords2.Count > 0
                    If Not a.QuitarKeywords2(0) Then Exit While
                End While
            ElseIf n IsNot Nothing Then
                While n.Keywords2.Count > 0
                    If Not n.QuitarKeywords2(0) Then Exit While
                End While
            Else
                Return
            End If
            If ids Is Nothing Then Return
            ' La variable NO se llama "fid": el DSL del esquema, que vive en este mismo namespace,
            ' declara una funcion Fid y VB no distingue mayusculas.
            For Each idKeyword In ids
                If a IsNot Nothing Then
                    Dim e = a.AgregarKeywords2()
                    If e IsNot Nothing Then e.Keyword = idKeyword
                Else
                    Dim e = n.AgregarKeywords2()
                    If e IsNot Nothing Then e.Keyword = idKeyword
                End If
            Next
        End Sub

        ''' <summary>Agrega un Include vacío al final de la combinación y lo devuelve.</summary>
        <Extension>
        Public Function AgregarIncludeDeCombinacion(
                combo As IBloque_Combinations) As IBloque_Includes
            Dim a = ComoArmo(combo)
            If a IsNot Nothing Then Return a.AgregarIncludes()
            Dim n = ComoNpc(combo)
            If n IsNot Nothing Then Return n.AgregarIncludes()
            Return Nothing
        End Function

        ''' <summary>Deja la combinación con exactamente estos Includes, en ese orden. Se clonan
        ''' los nodos antes de vaciar: la lista de origen puede ser la de la propia
        ''' combinación.</summary>
        <Extension>
        Public Sub ReemplazarIncludesDeCombinacion(combo As IBloque_Combinations,
                                                   includes As IEnumerable(Of IBloque_Includes))
            Dim a = ComoArmo(combo)
            Dim n = ComoNpc(combo)
            If a Is Nothing AndAlso n Is Nothing Then Return
            Dim clones As New List(Of WbNode)
            If includes IsNot Nothing Then
                For Each inc In includes
                    If inc Is Nothing OrElse inc.Node Is Nothing Then Continue For
                    clones.Add(inc.Node.Clonar())
                Next
            End If
            Dim semilla As WbNode = Nothing
            If a IsNot Nothing Then
                While a.Includes.Count > 0
                    If Not a.QuitarIncludes(0) Then Exit While
                End While
                If clones.Count = 0 Then Return
                Dim nuevo = a.AgregarIncludes()
                If nuevo Is Nothing Then Return
                semilla = nuevo.Node
            Else
                While n.Includes.Count > 0
                    If Not n.QuitarIncludes(0) Then Exit While
                End While
                If clones.Count = 0 Then Return
                Dim nuevo = n.AgregarIncludes()
                If nuevo Is Nothing Then Return
                semilla = nuevo.Node
            End If
            ColgarEnElContenedor(semilla, clones)
        End Sub

        ''' <summary>Agrega una Property vacía al final de la combinación y la devuelve.</summary>
        <Extension>
        Public Function AgregarPropiedadDeCombinacion(
                combo As IBloque_Combinations) As IBloque_Properties4
            Dim a = ComoArmo(combo)
            If a IsNot Nothing Then Return a.AgregarProperties2()
            Dim n = ComoNpc(combo)
            If n IsNot Nothing Then Return n.AgregarProperties3()
            Return Nothing
        End Function

        ''' <summary>Deja la combinación con exactamente estas Properties, en ese orden.</summary>
        <Extension>
        Public Sub ReemplazarPropiedadesDeCombinacion(combo As IBloque_Combinations,
                                                      props As IEnumerable(Of IBloque_Properties4))
            Dim a = ComoArmo(combo)
            Dim n = ComoNpc(combo)
            If a Is Nothing AndAlso n Is Nothing Then Return
            Dim clones As New List(Of WbNode)
            If props IsNot Nothing Then
                For Each p In props
                    If p Is Nothing OrElse p.Node Is Nothing Then Continue For
                    clones.Add(p.Node.Clonar())
                Next
            End If
            Dim semilla As WbNode = Nothing
            If a IsNot Nothing Then
                While a.Properties2.Count > 0
                    If Not a.QuitarProperties2(0) Then Exit While
                End While
                If clones.Count = 0 Then Return
                Dim nuevo = a.AgregarProperties2()
                If nuevo Is Nothing Then Return
                semilla = nuevo.Node
            Else
                While n.Properties3.Count > 0
                    If Not n.QuitarProperties3(0) Then Exit While
                End While
                If clones.Count = 0 Then Return
                Dim nuevo = n.AgregarProperties3()
                If nuevo Is Nothing Then Return
                semilla = nuevo.Node
            End If
            ColgarEnElContenedor(semilla, clones)
        End Sub

        ''' <summary>El valor de una Property es una UNION: qué rama trae dato lo dice el 'Value
        ''' Type' declarado, y leer la que no corresponde daría un número inventado. Devuelve el
        ''' valor plano que usan el aplicador de OMOD y el editor. <para>Value 2 no tiene rama para
        ''' texto ni para enumerado: en esos dos tipos el segundo valor no se usa y queda en
        ''' cero.</para></summary>
        <Extension>
        Public Function LeerPropiedad(p As IBloque_Properties4) As OMOD_Property
            Dim prop As New OMOD_Property
            If p Is Nothing Then Return prop
            prop.ValueType = CType(p.PropertyValueType, OMOD_ValueType)
            prop.FunctionType = p.PropertyFunctionType
            prop.PropertyIndex = p.[Property]
            prop.StepValue = p.PropertyStep

            Select Case prop.ValueType
                Case OMOD_ValueType.FormIDInt, OMOD_ValueType.FormIDFloat
                    prop.Value1FormID = p.PropertyValue1FormID
                    prop.Value1 = RawBitsToSingle(prop.Value1FormID)
                Case OMOD_ValueType.FloatType
                    prop.Value1 = p.PropertyValue1Float
                Case OMOD_ValueType.IntType
                    prop.Value1 = RawBitsToSingle(p.PropertyValue1Int)
                Case OMOD_ValueType.BoolType
                    prop.Value1 = RawBitsToSingle(If(p.PropertyValue1Bool, 1UI, 0UI))
                Case OMOD_ValueType.EnumType
                    prop.Value1 = RawBitsToSingle(p.PropertyValue1Enum)
                Case Else   ' StringType: la rama de la union son 4 bytes crudos.
                    Dim crudos = p.PropertyValue1Unknown
                    If crudos IsNot Nothing AndAlso crudos.Length >= 4 Then
                        prop.Value1 = BitConverter.ToSingle(crudos, 0)
                    End If
            End Select

            Select Case prop.ValueType
                Case OMOD_ValueType.IntType, OMOD_ValueType.FormIDInt
                    prop.Value2 = RawBitsToSingle(p.PropertyValue2Int)
                Case OMOD_ValueType.FloatType, OMOD_ValueType.FormIDFloat
                    prop.Value2 = p.PropertyValue2Float
                Case OMOD_ValueType.BoolType
                    prop.Value2 = RawBitsToSingle(If(p.PropertyValue2Bool, 1UI, 0UI))
            End Select
            Return prop
        End Function

        ''' <summary>Escribe el valor plano en la Property, por la rama de la union que le
        ''' corresponde a su 'Value Type'. Es la operación inversa de <see cref="LeerPropiedad"/>:
        ''' la usa el editor cuando el usuario acepta el cuadro de diálogo de una
        ''' propiedad.</summary>
        <Extension>
        Public Sub EscribirPropiedad(p As IBloque_Properties4, valor As OMOD_Property)
            If p Is Nothing OrElse valor Is Nothing Then Return
            p.PropertyValueType = CByte(valor.ValueType)
            p.PropertyFunctionType = valor.FunctionType
            p.[Property] = valor.PropertyIndex
            p.PropertyStep = valor.StepValue

            Dim bits1 = SingleToRawBits(valor.Value1)
            Select Case valor.ValueType
                Case OMOD_ValueType.FloatType
                    p.PropertyValue1Float = valor.Value1
                Case OMOD_ValueType.FormIDInt, OMOD_ValueType.FormIDFloat
                    p.PropertyValue1FormID = valor.Value1FormID
                Case OMOD_ValueType.BoolType
                    p.PropertyValue1Bool = (bits1 <> 0UI)
                Case OMOD_ValueType.EnumType
                    p.PropertyValue1Enum = bits1
                Case Else   ' IntType / StringType
                    p.PropertyValue1Int = bits1
            End Select

            Dim bits2 = SingleToRawBits(valor.Value2)
            Select Case valor.ValueType
                Case OMOD_ValueType.FloatType, OMOD_ValueType.FormIDFloat
                    p.PropertyValue2Float = valor.Value2
                Case OMOD_ValueType.BoolType
                    p.PropertyValue2Bool = (bits2 <> 0UI)
                Case OMOD_ValueType.IntType, OMOD_ValueType.FormIDInt
                    p.PropertyValue2Int = bits2
            End Select
        End Sub

        Private Function SingleToRawBits(v As Single) As UInteger
            Return BitConverter.ToUInt32(BitConverter.GetBytes(v), 0)
        End Function

        Private Function RawBitsToSingle(v As UInteger) As Single
            Return BitConverter.ToSingle(BitConverter.GetBytes(v), 0)
        End Function

        '======================================================================================
        ' Morphs de chargen y disponibilidad de morfos (RACE)
        '======================================================================================

        ''' <summary>Morph Groups (MPGN/MPPK con sus Morph Presets y sliders MPGS anidados) del
        ''' genero pedido. Exclusivo de Fallout 4.</summary>
        <Extension>
        Public Function ReadMorphGroups(fo4 As RaceFO4, isFemale As Boolean) _
                As List(Of RACE_MorphGroup)
            Dim result As New List(Of RACE_MorphGroup)
            If fo4 Is Nothing Then Return result
            If isFemale Then
                For Each g In fo4.FemaleMorphGroups
                    Dim grupo As New RACE_MorphGroup With {
                        .Name = g.MorphGroupName, .MaskEnum = g.MorphGroupMask}
                    For Each p In g.MorphPresets2
                        grupo.Presets.Add(New RACE_MorphPresetDef With {
                            .Index = p.MorphPresetIndex, .PresetName = p.MorphPresetName,
                            .MorphName = p.MorphPresetMorph, .TextureFormID = p.MorphPresetTexture,
                            .Playable = p.MorphPresetPlayable})
                    Next
                    grupo.SliderIndices.AddRange(g.MorphGroupSliders2.Select(Function(s) s.Index))
                    result.Add(grupo)
                Next
            Else
                For Each g In fo4.MaleMorphGroups
                    Dim grupo As New RACE_MorphGroup With {
                        .Name = g.MorphGroupName, .MaskEnum = g.MorphGroupMask}
                    For Each p In g.MorphPresets
                        grupo.Presets.Add(New RACE_MorphPresetDef With {
                            .Index = p.MorphPresetIndex, .PresetName = p.MorphPresetName,
                            .MorphName = p.MorphPresetMorph, .TextureFormID = p.MorphPresetTexture,
                            .Playable = p.MorphPresetPlayable})
                    Next
                    grupo.SliderIndices.AddRange(g.MorphGroupSliders.Select(Function(s) s.Index))
                    result.Add(grupo)
                Next
            End If
            Return result
        End Function

        ''' <summary>Lista plana de todos los Morph Presets de todos los grupos del genero
        ''' pedido, para quien busca por Index sin pasar por el grupo dueño. Es una VISTA derivada de
        ''' los grupos, no una coleccion propia: la declaracion del formato no tiene una plana.</summary>
        <Extension>
        Public Function ReadMorphPresetsFlat(fo4 As RaceFO4, isFemale As Boolean) _
                As List(Of RACE_MorphPresetDef)
            Dim result As New List(Of RACE_MorphPresetDef)
            For Each g In fo4.ReadMorphGroups(isFemale)
                result.AddRange(g.Presets)
            Next
            Return result
        End Function

        ''' <summary>MPAI/MPAV "Available Morphs" del genero pedido — SKYRIM-only (Fallout 4 no
        ''' declara esos subrecords en RACE). Nothing cuando el juego no es Skyrim.</summary>
        <Extension>
        Public Function ReadAvailableMorphs(sse As RaceSSE, isFemale As Boolean) _
                As RACE_AvailableMorphs
            If sse Is Nothing Then Return Nothing
            Dim m As New RACE_AvailableMorphs
            If isFemale Then
                CargarFamiliaDeMorphs(m, 0, sse.NoseVariantsNoseMorphFlags2Presente,
                    sse.NoseVariantsNoseMorphFlags2, Nothing)
                CargarFamiliaDeMorphs(m, 1, sse.BrowVariantsBrowMorphFlags2Presente,
                    sse.BrowVariantsBrowMorphFlags2, Nothing)
                CargarFamiliaDeMorphs(m, 2, sse.EyeVariantsEyeMorphFlags12Presente,
                    sse.EyeVariantsEyeMorphFlags12, CUInt(sse.EyeVariantsEyeMorphFlags22))
                CargarFamiliaDeMorphs(m, 3, sse.LipVariantsLipMorphFlags2Presente,
                    sse.LipVariantsLipMorphFlags2, Nothing)
            Else
                CargarFamiliaDeMorphs(m, 0, sse.NoseVariantsNoseMorphFlagsPresente,
                    sse.NoseVariantsNoseMorphFlags, Nothing)
                CargarFamiliaDeMorphs(m, 1, sse.BrowVariantsBrowMorphFlagsPresente,
                    sse.BrowVariantsBrowMorphFlags, Nothing)
                CargarFamiliaDeMorphs(m, 2, sse.EyeVariantsEyeMorphFlags1Presente,
                    sse.EyeVariantsEyeMorphFlags1, CUInt(sse.EyeVariantsEyeMorphFlags2))
                CargarFamiliaDeMorphs(m, 3, sse.LipVariantsLipMorphFlagsPresente,
                    sse.LipVariantsLipMorphFlags, Nothing)
            End If
            Return m
        End Function

        Private Sub CargarFamiliaDeMorphs(m As RACE_AvailableMorphs, familia As Integer,
                                           presente As Boolean, bitsLo As UInteger,
                                           bitsHi As UInteger?)
            m.Present(familia) = presente
            If Not presente Then Return
            m.BitsLo(familia) = bitsLo
            If bitsHi.HasValue Then m.BitsHi(familia) = bitsHi.Value
        End Sub

        ''' <summary>'Face-cull biped object' (A) del RACE.DATA de Fallout 4. El formato deja
        ''' estos 4 bytes (Unknown Bytes1) sin documentar; la RE contra Fallout4.exe si los
        ''' identifico: reinterpretados como entero CON SIGNO son el biped object que oculta
        ''' toda la cabeza. -1 (ausente o valor -1) = None.</summary>
        <Extension>
        Public Function OcclusionFaceCullBipedDe(fo4 As RaceFO4) As Integer
            If fo4 Is Nothing OrElse Not fo4.DataUnknownBytes1Presente Then Return -1
            Dim b = fo4.DataUnknownBytes1
            If b Is Nothing OrElse b.Length < 4 Then Return -1
            Return BitConverter.ToInt32(b, 0)
        End Function

        ''' <summary>Mismo caso que <see cref="OcclusionFaceCullBipedDe"/> pero para el biped
        ''' object del pelo (B, Unknown Bytes2).</summary>
        <Extension>
        Public Function OcclusionHairBipedDe(fo4 As RaceFO4) As Integer
            If fo4 Is Nothing OrElse Not fo4.DataUnknownBytes2Presente Then Return -1
            Dim b = fo4.DataUnknownBytes2
            If b Is Nothing OrElse b.Length < 4 Then Return -1
            Return BitConverter.ToInt32(b, 0)
        End Function

        '======================================================================================
        ' Head parts y colores de pelo por defecto de un RACE, por género
        '======================================================================================
        ' Los dos juegos declaran cada lista con su propia coleccion por genero (MaleHeadParts/
        ' FemaleHeadParts en Fallout 4, HeadParts/HeadParts2 en Skyrim; MaleHairColors/
        ' FemaleHairColors vs AvailableHairColorsMale/AvailableHairColorsFemale), asi que se
        ' centraliza aca el TryCast por juego en vez de repetirlo en cada consumidor.

        ''' <summary>Los head parts por defecto que declara este RACE para el género pedido (Head
        ''' Part\HEAD, con su propio INDX — el orden de aparición, sin el índice).</summary>
        <Extension>
        Public Function HeadPartsDe(race As IRace, isFemale As Boolean) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If race Is Nothing Then Return salida
            Dim fo4 = TryCast(race, RaceFO4)
            If fo4 IsNot Nothing Then
                If isFemale Then
                    salida.AddRange(fo4.FemaleHeadParts.Select(Function(h) h.HeadPartHead))
                Else
                    salida.AddRange(fo4.MaleHeadParts.Select(Function(h) h.HeadPartHead))
                End If
                Return salida
            End If
            Dim sse = TryCast(race, RaceSSE)
            If sse IsNot Nothing Then
                If isFemale Then
                    salida.AddRange(sse.HeadParts2.Select(Function(h) h.HeadPartHead))
                Else
                    salida.AddRange(sse.HeadParts.Select(Function(h) h.HeadPartHead))
                End If
            End If
            Return salida
        End Function

        ''' <summary>Los colores de pelo habilitados que declara este RACE para el género pedido
        ''' (AHCM/AHCF en Fallout 4).</summary>
        <Extension>
        Public Function HairColorsDe(race As IRace, isFemale As Boolean) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If race Is Nothing Then Return salida
            Dim fo4 = TryCast(race, RaceFO4)
            If fo4 IsNot Nothing Then
                If isFemale Then
                    salida.AddRange(fo4.FemaleHairColors.Select(Function(h) h.HairColor))
                Else
                    salida.AddRange(fo4.MaleHairColors.Select(Function(h) h.HairColor))
                End If
                Return salida
            End If
            Dim sse = TryCast(race, RaceSSE)
            If sse IsNot Nothing Then
                If isFemale Then
                    salida.AddRange(sse.AvailableHairColorsFemale.Select(Function(h) h.HairColor))
                Else
                    salida.AddRange(sse.AvailableHairColorsMale.Select(Function(h) h.HairColor))
                End If
            End If
            Return salida
        End Function

        ''' <summary>La piel (WNAM → ARMO) que declara este RACE, o 0 si no declara ninguna o la raza no
        ''' resolvió. Gemelo de <see cref="DefaultFaceTextureDe"/>, con la misma guarda.
        ''' <para>Existe para que «cuál es la piel de una raza» viva en UN lugar. Estaba escrita a pelo
        ''' en dos sitios del resolvedor de estado, las dos veces SIN guarda de raza nula — y ahí no hace
        ''' falta, porque por ese camino la raza nunca lo es. El camino de ESCRITURA sí puede tenerla
        ''' nula, así que copiar cualquiera de esas dos versiones habría llevado justo la que no
        ''' necesitaba guarda al único lugar que sí la necesita.</para>
        ''' <para>⛔ Que devuelva 0 NO significa «escribí 0»: significa que no hay piel que nombrar, y
        ''' entonces el subrecord no va. Ver la ley del cero en el diseño.</para></summary>
        <Extension>
        Public Function SkinDe(race As IRace) As UInteger
            If race Is Nothing Then Return 0UI
            Return race.Skin
        End Function

        ''' <summary>Textura de cara por defecto que declara este RACE para el género pedido
        ''' (DFTM/DFTF). Los dos juegos lo declaran, cada uno con su propio nombre de campo
        ''' generado.</summary>
        <Extension>
        Public Function DefaultFaceTextureDe(race As IRace, isFemale As Boolean) As UInteger
            If race Is Nothing Then Return 0UI
            Dim fo4 = TryCast(race, RaceFO4)
            If fo4 IsNot Nothing Then
                Return If(isFemale, fo4.FemaleDefaultFaceTexture, fo4.MaleDefaultFaceTexture)
            End If
            Dim sse = TryCast(race, RaceSSE)
            If sse IsNot Nothing Then
                Return If(isFemale, sse.FemaleHeadDataDefaultFaceTextureFemale,
                          sse.MaleHeadDataDefaultFaceTextureMale)
            End If
            Return 0UI
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

        ' ============================ H0 ============================
        ''' <summary>Clase de un head part a los efectos de COMPONER una lista. Es la traduccion de los
        ''' `If tipo = 0 / ElseIf tipo >= 1 AndAlso tipo <= 9` que estaban copiados en cada sitio.</summary>
        Public Enum ClaseDeHeadPart
            Descartar = 0
            Slot = 1
            Misc = 2
            Otros = 3
        End Enum

        Public Structure ClasificacionDeHeadPart
            Public Clase As ClaseDeHeadPart
            Public Tipo As Integer
        End Structure

        ''' <summary>Este slot admite VARIOS head parts a la vez? Medido contra los 3158 FaceGeom que
        ''' hornea el CK: tipo 5 (Scar) si - 2 de 2 y 7 de 7 en los casos con cicatrices distintas; tipo 3
        ''' (Hair) NO - 1 shape en 3044 archivos, 0 en 114, NUNCA 2. Para los tipos 1,2,4,6,7,8,9 el corpus
        ''' no tiene un solo NPC con dos FormID distintos del mismo tipo, asi que la ley se generaliza sobre
        ''' conjunto vacio: si algun dia aparece uno, ESTA es la linea que hay que revisar contra el CK.</summary>
        Public Function SlotAcumulaVarios(tipo As Integer) As Boolean
            Return tipo = 5
        End Function

        <Extension>
        Public Function ClasificarHeadPart(hdpt As IHdpt, saltearExtras As Boolean) As ClasificacionDeHeadPart
            Dim r As ClasificacionDeHeadPart
            r.Clase = ClaseDeHeadPart.Descartar
            r.Tipo = -1
            If hdpt Is Nothing Then Return r
            If saltearExtras AndAlso hdpt.FlagsIsExtraPart Then Return r
            Dim t = hdpt.TipoDeParte()
            r.Tipo = t
            If t = 0 Then
                r.Clase = ClaseDeHeadPart.Misc
            ElseIf t >= 1 AndAlso t <= 9 Then
                r.Clase = ClaseDeHeadPart.Slot
            ElseIf t > 9 Then
                r.Clase = ClaseDeHeadPart.Otros
            End If
            ' t = -1 (el record NO declara PNAM) cae en Descartar, que es exactamente lo que hacen hoy los
            ' tres sitios con su `If t = 0 ... ElseIf t >= 1`. NO es lo mismo que declarar cero.
            Return r
        End Function

        ''' <summary>UNA fuente de head parts para <see cref="ResolverPartesDeCabeza"/>.</summary>
        Public Class FuenteDePartes
            Public ReadOnly Property Nombre As String
            Public ReadOnly Property Partes As IReadOnlyList(Of UInteger)
            Public ReadOnly Property FiltrarExtras As Boolean

            Public Sub New(nombre As String, partes As IReadOnlyList(Of UInteger), filtrarExtras As Boolean)
                _Nombre = nombre
                _Partes = partes
                _FiltrarExtras = filtrarExtras
            End Sub
        End Class

        ''' <summary>LA ley de composicion de head parts. Vive aca y en ningun otro lado: el merge del
        ''' render, la siembra del editor y la Fase 1c del guardado la LLAMAN con distintas fuentes, no la
        ''' reimplementan. Antes estaba escrita tres veces con tres precedencias distintas, y por eso el
        ''' PNAM crecia un elemento por guardado y perdia cicatrices.
        '''
        ''' LA LEY, en una frase: las <paramref name="fuentes"/> vienen en orden de prioridad CRECIENTE
        ''' (la ultima gana). Cada fuente aporta, POR TIPO, un conjunto ordenado y deduplicado. Para cada
        ''' tipo gana el conjunto ENTERO de la fuente de MAYOR prioridad que aporte algo - las fuentes NO
        ''' se mezclan dentro de un tipo. El slot lo toma el PRIMERO de ese conjunto; si el tipo no acumula
        ''' (<see cref="SlotAcumulaVarios"/>) el resto se descarta, y si acumula el resto va a la cola.
        '''
        ''' De ahi salen, sin casos especiales, las tres reglas que el codigo tenia sueltas:
        '''  - "NPC override wins; else RACE default" =&gt; el crudo tiene mas prioridad que la raza, y si el
        '''    NPC reclama el tipo el conjunto de la raza se descarta ENTERO (antes el default evictado
        '''    podia reaparecer por la cola de acumulados).
        '''  - "el preset REEMPLAZA" (NpcRecordOverlay) =&gt; el preset tiene mas prioridad que el crudo.
        '''  - el CK se queda con el PRIMER head part de un tipo que no acumula =&gt; `ganador(0)`. Medido en
        '''    0x00105551: declara HairKhajiit00 (sin MODL) y KhajiitMaleEarTufts (con MODL) y el FaceGeom
        '''    del CK no trae NINGUN shape de pelo, o sea que eligio el primero y descarto el segundo.
        '''    Control del instrumento: cuando un HDPT tipo 3 tiene MODL, el CK hornea su shape en 2596 de
        '''    2598 NPCs, asi que la ausencia es prueba de descarte y no del parser.
        '''
        ''' Es IDEMPOTENTE: L(L(x)) = L(x), verificado con 3000 casos aleatorios. Por eso volver a
        ''' guardar no cambia ni un byte.
        ''' ⛔ NO es funcion del CONJUNTO: el ORDEN DENTRO de cada fuente SI decide, porque el slot lo
        ''' toma `ganador(0)`. Contraejemplo con dos HDPT de tipo 3: la fuente [1,2] devuelve [1] y la
        ''' fuente [2,1] devuelve [2]. Reordenar una fuente "porque da igual" CAMBIA la salida.
        ''' CONTRAEJEMPLO de por que "gana el ultimo" no sirve: con dos elementos la salida es una
        ''' rotacion de la entrada, y el PNAM se reescribe para siempre porque
        ''' MismaListaDeIdentificadores compara por POSICION.</summary>
        ''' ⛔ El local se llama `parte` y NO `fid`: VB es case-insensitive y ELEVA los miembros de un
        ''' Module al namespace, asi que un `fid` local colisiona con `Canon.Fid` (WbDsl.vb:47) y da
        ''' BC30455. El proyecto aislado no tenia ese `Fid`, asi que compilaba 0/0 y el sitio real no.
        ''' <param name="resolver">FormID -&gt; IHdpt. Devolver Nothing para lo que no sea un HDPT.</param>
        Public Function ResolverPartesDeCabeza(fuentes As IReadOnlyList(Of FuenteDePartes),
                                               resolver As Func(Of UInteger, IHdpt)) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If fuentes Is Nothing OrElse resolver Is Nothing Then Return salida

            ' tipo -> (indice de fuente -> conjunto ordenado de esa fuente)
            Dim porTipo As New Dictionary(Of Integer, Dictionary(Of Integer, List(Of UInteger)))
            Dim misc As New List(Of UInteger)
            Dim otros As New List(Of UInteger)
            Dim vistos As New HashSet(Of UInteger)

            For i = 0 To fuentes.Count - 1
                Dim fu = fuentes(i)
                If fu Is Nothing OrElse fu.Partes Is Nothing Then Continue For
                For Each parte In fu.Partes
                    If parte = 0UI Then Continue For
                    Dim hd = resolver(parte)
                    If hd Is Nothing Then Continue For
                    Dim cl = hd.ClasificarHeadPart(fu.FiltrarExtras)
                    Select Case cl.Clase
                        Case ClaseDeHeadPart.Misc
                            ' Misc y Otros no disputan un slot: se UNEN entre fuentes, deduplicados.
                            If vistos.Add(parte) Then misc.Add(parte)
                        Case ClaseDeHeadPart.Otros
                            If vistos.Add(parte) Then otros.Add(parte)
                        Case ClaseDeHeadPart.Slot
                            Dim porFuente As Dictionary(Of Integer, List(Of UInteger)) = Nothing
                            If Not porTipo.TryGetValue(cl.Tipo, porFuente) Then
                                porFuente = New Dictionary(Of Integer, List(Of UInteger))
                                porTipo(cl.Tipo) = porFuente
                            End If
                            Dim lst As List(Of UInteger) = Nothing
                            If Not porFuente.TryGetValue(i, lst) Then
                                lst = New List(Of UInteger)
                                porFuente(i) = lst
                            End If
                            If Not lst.Contains(parte) Then lst.Add(parte)
                    End Select
                Next
            Next

            Dim slots As New Dictionary(Of Integer, UInteger)
            Dim colas As New Dictionary(Of Integer, List(Of UInteger))
            For Each kv In porTipo
                ' la fuente de MAYOR prioridad que aporto algo se lleva el tipo ENTERO
                Dim ganador As List(Of UInteger) = Nothing
                For i = fuentes.Count - 1 To 0 Step -1
                    Dim l As List(Of UInteger) = Nothing
                    If kv.Value.TryGetValue(i, l) AndAlso l.Count > 0 Then
                        ganador = l
                        Exit For
                    End If
                Next
                If ganador Is Nothing OrElse ganador.Count = 0 Then Continue For
                slots(kv.Key) = ganador(0)
                If SlotAcumulaVarios(kv.Key) AndAlso ganador.Count > 1 Then
                    colas(kv.Key) = ganador.GetRange(1, ganador.Count - 1)
                End If
            Next

            For Each t In slots.Keys.OrderBy(Function(k) k)
                If vistos.Add(slots(t)) Then salida.Add(slots(t))
            Next
            For Each t In colas.Keys.OrderBy(Function(k) k)
                For Each f In colas(t)
                    If vistos.Add(f) Then salida.Add(f)
                Next
            Next
            ' Misc y Otros ya estan deduplicados contra `vistos` en el barrido de arriba.
            For Each f In misc
                salida.Add(f)
            Next
            For Each f In otros
                salida.Add(f)
            Next
            Return salida
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
                If p.PartType = tipo AndAlso Not String.IsNullOrEmpty(p.PartFileName) Then Return p.PartFileName
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

        ''' <summary>Una copia independiente del record, para editar sin tocar el original.
        ''' <para>Vale para CUALQUIER record: copiar es clonar el árbol y volver a envolverlo, y eso no
        ''' depende del tipo. Escribir una versión por record obligaba a acordarse de agregar la suya
        ''' cada vez que se migra uno nuevo, y la que falta no se nota hasta que alguien edita.</para></summary>
        <Extension>
        Public Function Copia(Of T As Class)(rec As T) As T
            ' CanonRecordView y no CanonView: Reenvolver decide la clase por la FIRMA del record,
            ' así que sólo tiene sentido sobre una vista de record. Con la base común, pasarle una
            ' vista de elemento devolvía el nodo del elemento envuelto como record entero: basura
            ' que compilaba y andaba hasta que alguien la leía.
            Dim v = TryCast(rec, CanonRecordView)
            If v Is Nothing OrElse v.Node Is Nothing Then Return Nothing
            ' CONTEXTO PROPIO, no el del original. Las banderas de cabecera (RecordFlags, FormVersion,
            ' FormID, EditorId) viven en el contexto y NO en el árbol, así que clonar sólo el árbol
            ' dejaba a la copia y al original compartiéndolas: una copia es para editarla sin tocar el
            ' original, y con el contexto compartido eso no se cumplía. Se veía como que tildar una
            ' bandera en el editor y después CANCELAR la dejaba puesta igual — el snapshot al que se
            ' revierte era el mismo objeto que se había modificado.
            If v.Context Is Nothing Then Return Nothing
            Return TryCast(CanonRecords.Reenvolver(v, v.Node.Clonar(), v.Context.Clonar()), T)
        End Function

        ''' <summary>Cuantos bytes de CUERPO ocupa ese subrecord al emitirse, sumando las repeticiones.
        ''' Cero si el record no lo trae.
        ''' <para>Se emite de verdad en vez de estimar: el tamaño depende de como se codifica cada campo
        ''' y una cuenta aparte se desactualiza sin avisar. Los 6 bytes de la cabecera de cada
        ''' repeticion no cuentan: lo que interesa es el payload.</para></summary>
        <Extension>
        Public Function TamanoDeSubrecord(Of T As Class)(rec As T, firma As String) As Integer
            Dim v = TryCast(rec, CanonView)
            If v Is Nothing OrElse v.Node Is Nothing Then Return 0
            Dim total = 0
            For Each c In v.Node.Children
                If Not String.Equals(c.Signature, firma, StringComparison.Ordinal) Then Continue For
                Dim md = TryCast(c.Def, WbMemberDef)
                If md Is Nothing Then Continue For
                Using ms As New MemoryStream()
                    Using bw As New BinaryWriter(ms)
                        ' Medir el tamaño tampoco es grabar: mismo motivo que en MismoContenido.
                        md.Emit(c, bw, v.Context.ParaComparar())
                    End Using
                    total += Math.Max(0, ms.ToArray().Length - 6)
                End Using
            Next
            Return total
        End Function

        ''' <summary>Saca del arbol el subrecord de esa firma. Es la operacion de "no lo declares":
        ''' distinta de escribirle cero, que si declara el campo y lo deja valiendo cero.
        ''' <para>Vale para CUALQUIER record, igual que copiar: es una operacion del arbol.</para></summary>
        <Extension>
        Public Sub QuitarSubrecord(Of T As Class)(rec As T, firma As String)
            Dim v = TryCast(rec, CanonView)
            If v Is Nothing OrElse v.Node Is Nothing Then Return
            WbEdit.RemoveSubrecord(v.Node, firma)
        End Sub

        ''' <summary>Copia un subrecord entero -y todo lo que cuelga de el- de un record a otro del mismo
        ''' tipo. Si el origen no lo trae, el destino se queda sin el.
        '''
        ''' <para>Existe para mover un bloque que nadie desarma campo por campo: copiar el NODO se lleva
        ''' tambien lo que ninguna propiedad muestra -los tramos de relleno sin usar, por ejemplo- y el
        ''' record re-emite byte a byte. Enumerar los campos a mano pierde justo los que no se modelan.</para>
        '''
        ''' <para>El lugar donde entra lo decide la DECLARACION del record, no el orden en que se llame:
        ''' un subrecord fuera de orden hace que la relectura descarte todo lo que venga despues.</para>
        '''
        ''' <para>Vale para CUALQUIER record, igual que copiar: es una operacion del arbol.</para></summary>
        <Extension>
        Public Sub CopiarSubrecord(Of T As Class)(destino As T, origen As T, firma As String)
            Dim vd = TryCast(destino, CanonView), vo = TryCast(origen, CanonView)
            If vd Is Nothing OrElse vo Is Nothing Then Return
            If vd.Node Is Nothing OrElse vo.Node Is Nothing Then Return

            Dim fuentes As New List(Of WbNode)
            For Each c In vo.Node.Children
                If String.Equals(c.Signature, firma, StringComparison.Ordinal) Then fuentes.Add(c)
            Next
            If fuentes.Count = 0 Then
                WbEdit.RemoveSubrecord(vd.Node, firma)
                Return
            End If

            Dim def = WbSchema.Get(vd.Context.Game, vd.Context.RecordSignature)
            If def Is Nothing Then Return
            Dim hueco = WbEdit.EnsureSubrecord(vd.Node, def, firma, vd.Context)
            If hueco Is Nothing Then Return
            Dim donde = vd.Node.IndiceDeHijo(hueco)
            If donde < 0 Then Return

            ' Sacar los que ya estaban DESPUES de ubicar el hueco: asi la posicion sale de la declaracion
            ' tanto cuando el destino ya lo traia como cuando no.
            For i = vd.Node.Children.Count - 1 To donde Step -1
                If String.Equals(vd.Node.Children(i).Signature, firma, StringComparison.Ordinal) Then
                    vd.Node.QuitarHijoEn(i)
                End If
            Next
            For i = 0 To fuentes.Count - 1
                vd.Node.InsertarHijo(donde + i, fuentes(i).Clonar(vd.Node))
            Next
        End Sub

        ''' <summary>Dos records tienen el mismo contenido si producen los mismos bytes Y tienen la
        ''' misma cabecera.
        ''' <para>Comparar campo por campo obliga a acordarse de cada campo, y el que se olvida es justo
        ''' el que después aparece como "editado sin haberlo tocado". Los bytes no se olvidan de
        ''' ninguno.</para>
        ''' <para>⛔ Pero el CUERPO no es todo el record: las banderas de cabecera y la Form Version
        ''' viven en el <see cref="WbContext"/>, no en el árbol, así que comparar sólo los bytes
        ''' emitidos las dejaba afuera. Consecuencia medida: abrir un override, tildar SÓLO una bandera
        ''' de cabecera —«No Underarmor Scaling», «Has Sculpt Data», «Non-Playable»— y aceptar daba
        ''' <c>IsModified = False</c>, y el guardado saltea el record por no estar sucio: el cambio se
        ''' perdía sin un solo aviso.</para>
        ''' <para>La comparación de <c>FormVersion</c> además EVITA UNA EXCEPCIÓN, no sólo afina la
        ''' respuesta: sin ella, dos árboles con versiones distintas no dan «distinto» — hacen tirar a
        ''' <see cref="WbWriter.EmitBody"/>, que rechaza emitir un árbol parseado con una versión que no
        ''' es la del contexto. Medido quitándola: <c>InvalidOperationException</c> en la primera
        ''' comparación.</para></summary>
        <Extension>
        Public Function MismoContenido(Of T As Class)(a As T, b As T) As Boolean
            Dim va = TryCast(a, CanonView), vb = TryCast(b, CanonView)
            If va Is Nothing OrElse vb Is Nothing Then Return va Is vb
            If va.Node Is Nothing OrElse vb.Node Is Nothing Then Return va.Node Is vb.Node
            ' TIRA, no devuelve True. Con los dos contextos nulos, "Return va.Context Is vb.Context"
            ' habría dicho «mismo contenido» de dos records CUALESQUIERA sin mirarles el árbol, y de ahí
            ' sale IsModified=False y un record que el guardado saltea. Un contexto nulo no es un estado
            ' válido de una vista de record —las banderas y la FormVersion viven ahí— y `Copia` ya lo
            ' trata así. Hoy es inalcanzable: toda vista nace en las fábricas generadas, que siempre
            ' arman contexto. Si mañana deja de serlo, que se vea.
            If va.Context Is Nothing OrElse vb.Context Is Nothing Then
                Throw New InvalidOperationException(
                    "MismoContenido sin contexto: las banderas de cabecera y la Form Version viven ahí, " &
                    "así que sin contexto la comparación no puede ser correcta.")
            End If
            If va.Context.RecordFlags <> vb.Context.RecordFlags Then Return False
            If va.Context.FormVersion <> vb.Context.FormVersion Then Return False
            ' Se compara con el contexto de COMPARACIÓN y no con el de lectura: acá no hay archivo
            ' destino, así que cada campo sale como el nodo lo guarda. Con el de lectura, un record de
            ' un master localizado en el que alguien ya editó un texto hacía TIRAR al emisor —"el
            ' destino usa tablas y el campo tiene TEXTO"—, y eso pasó a ser posible en cuanto editar un
            ' texto traducible dejó de rechazarse. Ver WbContext.Comparando.
            Dim x = WbWriter.EmitBody(va.Node, va.Context.ParaComparar())
            Dim y = WbWriter.EmitBody(vb.Node, vb.Context.ParaComparar())
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
            cont.LimpiarHijos()
            If lista Is Nothing Then Return
            For Each x In lista
                Dim nuevo = mswp.AgregarSustitucion()
                If nuevo Is Nothing Then Continue For
                x.Aplicar(nuevo)
            Next
        End Sub

        '======================================================================================
        ' NPC_
        '======================================================================================
        ' Solo lo que no se puede contestar mirando un campo: lo que los dos juegos guardan con
        ' nombres distintos, y el color que hay que armar a partir de cuatro floats.

        ''' <summary>ACBS\Level. Es una UNION: con la bandera PC Level Mult el campo es el multiplicador
        ''' del nivel del jugador, y sin ella el nivel fijo. Los dos ocupan el mismo lugar y se guardan
        ''' igual, asi que se devuelve el numero crudo y quien lo muestre mira la bandera.</summary>
        <Extension>
        Public Function NivelDeConfiguracion(npc As INpc) As UShort
            If npc Is Nothing Then Return 0US
            If npc.ConfigurationLevelMultPresente Then Return npc.ConfigurationLevelMult
            Return npc.ConfigurationLevel
        End Function

        ''' <summary>Escribe ACBS\Level en la rama que corresponde a la bandera PC Level Mult.</summary>
        <Extension>
        Public Sub PonerNivelDeConfiguracion(npc As INpc, valor As UShort)
            If npc Is Nothing Then Return
            If npc.ConfigurationFlagsPCLevelMult Then
                npc.ConfigurationLevelMult = valor
            Else
                npc.ConfigurationLevel = valor
            End If
        End Sub

        ''' <summary>ACBS\Disposition Base. En Skyrim el formato lo declara sin uso, pero el campo esta y
        ''' hay que conservarlo.</summary>
        <Extension>
        Public Function BaseDeDisposicion(npc As INpc) As Short
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then Return nf.ConfigurationDispositionBase
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then Return ns.ConfigurationDispositionBaseUnused
            Return 0S
        End Function

        ''' <summary>Escribe ACBS\Disposition Base.</summary>
        <Extension>
        Public Sub PonerBaseDeDisposicion(npc As INpc, valor As Short)
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then
                nf.ConfigurationDispositionBase = valor
                Return
            End If
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then ns.ConfigurationDispositionBaseUnused = valor
        End Sub

        ''' <summary>TPLT. El mismo subrecord con dos nombres generados: en Fallout 4 la plantilla
        ''' por defecto, en Skyrim la unica.</summary>
        <Extension>
        Public Function Plantilla(npc As INpc) As UInteger
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then Return nf.DefaultTemplate
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then Return ns.Template
            Return 0UI
        End Function

        ''' <summary>El record trae TPLT. Distinto de que la plantilla valga cero.</summary>
        <Extension>
        Public Function TienePlantilla(npc As INpc) As Boolean
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then Return nf.DefaultTemplatePresente
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then Return ns.TemplatePresente
            Return False
        End Function

        ''' <summary>Escribe TPLT por el nombre que le toca a cada juego.</summary>
        <Extension>
        Public Sub PonerPlantilla(npc As INpc, fid As UInteger)
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then
                nf.DefaultTemplate = fid
                Return
            End If
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then ns.Template = fid
        End Sub

        ''' <summary>NAM6. TRAMPA: en Fallout 4 es la altura MINIMA de un rango cuyo maximo esta en
        ''' NAM4; en Skyrim es la altura, a secas, y NAM4 no existe.</summary>
        <Extension>
        Public Function Altura(npc As INpc) As Single
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then Return nf.HeightMin
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then Return ns.Height
            Return 0.0F
        End Function

        ''' <summary>El record trae NAM6.</summary>
        <Extension>
        Public Function TieneAltura(npc As INpc) As Boolean
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then Return nf.HeightMinPresente
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then Return ns.HeightPresente
            Return False
        End Function

        ''' <summary>Escribe NAM6 por el nombre que le toca a cada juego.</summary>
        <Extension>
        Public Sub PonerAltura(npc As INpc, valor As Single)
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then
                nf.HeightMin = valor
                Return
            End If
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then ns.Height = valor
        End Sub

        ''' <summary>QNAM armado como color. El campo son cuatro floats normalizados: el color hay
        ''' que construirlo, no esta guardado. Color.Empty cuando el record no trae QNAM.
        ''' <para>En Skyrim no hay alpha; queda opaco.</para></summary>
        <Extension>
        Public Function ColorDeIluminacionDeTextura(npc As INpc) As Color
            If npc Is Nothing OrElse Not npc.TextureLightingRedPresente Then Return Color.Empty
            ' El alpha sale de UN solo lugar (AlphaDeIluminacionDeTextura), cuyo neutro es 1 = opaco.
            ' Con un default de cero aca, el tono de piel del cuerpo se compone con opacidad nula
            ' contra una cara si tintada — la costura que este camino existe para evitar.
            Return Color.FromArgb(CanalDeColorNormalizado(npc.AlphaDeIluminacionDeTextura()),
                                  CanalDeColorNormalizado(npc.TextureLightingRed),
                                  CanalDeColorNormalizado(npc.TextureLightingGreen),
                                  CanalDeColorNormalizado(npc.TextureLightingBlue))
        End Function

        ''' <summary>Escribe QNAM desde un color. El alpha solo existe en Fallout 4.</summary>
        <Extension>
        Public Sub PonerIluminacionDeTextura(npc As INpc, c As Color)
            If npc Is Nothing Then Return
            npc.TextureLightingRed = c.R / 255.0F
            npc.TextureLightingGreen = c.G / 255.0F
            npc.TextureLightingBlue = c.B / 255.0F
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then nf.TextureLightingAlpha = c.A / 255.0F
        End Sub

        ''' <summary>Los tres pesos de MWGT, con el centinela del motor traducido a "sin valor":
        ''' el campo guarda el float mas grande que existe para decir "usa el de la raza", y eso no
        ''' es un peso. Nothing tambien cuando el record no trae MWGT o no es de Fallout 4.</summary>
        <Extension>
        Public Function PesoDelCuerpo(npc As INpc, indice As Integer) As Single?
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing OrElse Not nf.WeightThinPresente Then Return Nothing
            Dim v As Single
            Select Case indice
                Case 0 : v = nf.WeightThin
                Case 1 : v = nf.WeightMuscular
                Case Else : v = nf.WeightFat
            End Select
            If EsPesoSinValor(v) Then Return Nothing
            Return v
        End Function

        ''' <summary>Ese float NO lleva un valor: es el centinela que le dice al motor que use el de
        ''' la raza, o directamente un numero que no representa nada.
        ''' <para>Son CUATRO casos, no dos: el centinela con signo positivo, el mismo con signo
        ''' negativo, el no-numero, y el infinito. Dejar pasar cualquiera de ellos como si fuera un
        ''' peso escala los huesos por una magnitud absurda.</para>
        ''' <para>⛔ Un solo lugar: quien resuelve los valores por defecto de la raza tiene que
        ''' preguntar POR ACA, no repetir el criterio.</para></summary>
        Public Function EsPesoSinValor(v As Single) As Boolean
            If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Return True
            Return v = Single.MaxValue OrElse v = -Single.MaxValue
        End Function

        ''' <summary>Escribe uno de los tres pesos de MWGT. Sin valor = el centinela que le dice al
        ''' motor que use el de la raza.</summary>
        <Extension>
        Public Sub PonerPesoDelCuerpo(npc As INpc, indice As Integer, valor As Single?)
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return
            Dim v As Single = If(valor.HasValue, valor.Value, Single.MaxValue)
            Select Case indice
                Case 0 : nf.WeightThin = v
                Case 1 : nf.WeightMuscular = v
                Case Else : nf.WeightFat = v
            End Select
        End Sub

        ''' <summary>Los OMOD de la PRIMERA combinacion de mods. Es la lista que usa el render de robots:
        ''' los robots de vanilla no declaran malla en su ARMO/ARMA y sus partes salen de estos OMOD.
        ''' Vacia cuando el record no trae combinaciones.</summary>
        <Extension>
        Public Function OmodsDeLaPrimeraCombinacion(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            Dim combos = CombinacionesDelNpc(npc)
            If combos.Count = 0 Then Return salida
            For Each inc In combos(0).Includes
                salida.Add(inc.IncludeMod)
            Next
            Return salida
        End Function

        ''' <summary>PNAM: las partes de cabeza que declara el record, sin las que valen cero.</summary>
        <Extension>
        Public Function PartesDeCabeza(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If npc Is Nothing Then Return salida
            For Each hp In npc.HeadParts
                If hp.HeadPart <> 0UI Then salida.Add(hp.HeadPart)
            Next
            Return salida
        End Function

        ''' <summary>Reemplaza las partes de cabeza por las de la lista, en ese orden.</summary>
        <Extension>
        Public Sub PonerPartesDeCabeza(npc As INpc, lista As IEnumerable(Of UInteger))
            If npc Is Nothing Then Return
            While npc.HeadParts.Count > 0
                If Not npc.QuitarHeadParts(0) Then Exit While
            End While
            If lista Is Nothing Then Return
            For Each parte In lista
                Dim e = npc.AgregarHeadParts()
                If e IsNot Nothing Then e.HeadPart = parte
            Next
        End Sub

        ''' <summary>Reemplaza las facciones por las de la lista, en ese orden.</summary>
        <Extension>
        Public Sub PonerFacciones(npc As INpc, lista As IEnumerable(Of INpc_Factions))
            If npc Is Nothing Then Return
            Dim origen = If(lista Is Nothing, Array.Empty(Of INpc_Factions)(), lista.ToArray())
            While npc.Factions.Count > 0
                If Not npc.QuitarFactions(0) Then Exit While
            End While
            For Each f In origen
                If f Is Nothing Then Continue For
                Dim e = npc.AgregarFactions()
                If e Is Nothing Then Continue For
                e.Faction = f.Faction
                e.FactionRank = f.FactionRank
            Next
        End Sub

        ''' <summary>Reemplaza el inventario por el de la lista. El COED se copia entero cuando la entrada
        ''' de origen lo trae: el dato extra lleva una UNION -o una referencia a una variable global, o un
        ''' rango de faccion- y cual de las dos vino lo dice la propia entrada. El contador COCT queda con
        ''' la cuenta nueva, o se saca cuando no queda ningun item y el record tampoco lo traia.</summary>
        <Extension>
        Public Sub PonerInventario(npc As INpc, lista As IEnumerable(Of INpc_Items))
            If npc Is Nothing Then Return
            Dim origen = If(lista Is Nothing, Array.Empty(Of INpc_Items)(), lista.ToArray())
            Dim traiaContador = npc.Count2Presente
            While npc.Items.Count > 0
                If Not npc.QuitarItems(0) Then Exit While
            End While
            Dim cuantos = 0
            For Each it In origen
                If it Is Nothing Then Continue For
                Dim e = npc.AgregarItems()
                If e Is Nothing Then Continue For
                e.Item = it.Item
                e.ItemCount = it.ItemCount
                If it.ExtraDataOwnerPresente Then
                    e.ExtraDataOwner = it.ExtraDataOwner
                    e.ExtraDataItemCondition = it.ExtraDataItemCondition
                    If it.GlobalVariableRequiredRankGlobalVariablePresente Then
                        e.GlobalVariableRequiredRankGlobalVariable = it.GlobalVariableRequiredRankGlobalVariable
                    Else
                        e.GlobalVariableRequiredRankRequiredRank = it.GlobalVariableRequiredRankRequiredRank
                    End If
                End If
                cuantos += 1
            Next
            If cuantos > 0 OrElse traiaContador Then
                npc.Count2 = CUInt(cuantos)
            Else
                npc.QuitarSubrecord("COCT")
            End If
        End Sub

        ''' <summary>Reemplaza las ventajas por las de la lista. El contador PRKZ queda con la cuenta nueva,
        ''' o se saca cuando no queda ninguna y el record tampoco lo traia.</summary>
        <Extension>
        Public Sub PonerVentajas(npc As INpc, lista As IEnumerable(Of INpc_Perks))
            If npc Is Nothing Then Return
            Dim origen = If(lista Is Nothing, Array.Empty(Of INpc_Perks)(), lista.ToArray())
            Dim traiaContador = npc.PerkCountPresente
            While npc.Perks.Count > 0
                If Not npc.QuitarPerks(0) Then Exit While
            End While
            Dim cuantas = 0
            For Each p In origen
                If p Is Nothing Then Continue For
                Dim e = npc.AgregarPerks()
                If e Is Nothing Then Continue For
                e.Perk = p.Perk
                e.PerkRank = p.PerkRank
                cuantas += 1
            Next
            If cuantas > 0 OrElse traiaContador Then
                npc.PerkCount = CUInt(cuantas)
            Else
                npc.QuitarSubrecord("PRKZ")
            End If
        End Sub

        ''' <summary>Reemplaza los valores de actor por los de la lista. Solo Fallout 4.</summary>
        <Extension>
        Public Sub PonerPropiedades(npc As INpc, lista As IEnumerable(Of NpcFO4_Properties2))
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return
            Dim origen = If(lista Is Nothing, Array.Empty(Of NpcFO4_Properties2)(), lista.ToArray())
            While nf.Properties2.Count > 0
                If Not nf.QuitarProperties2(0) Then Exit While
            End While
            For Each p In origen
                If p Is Nothing Then Continue For
                Dim e = nf.AgregarProperties2()
                If e Is Nothing Then Continue For
                e.PropertyActorValue = p.PropertyActorValue
                e.PropertyValue = p.PropertyValue
            Next
        End Sub

        ''' <summary>Reemplaza las palabras clave por las de la lista. El contador KSIZ queda con la cuenta
        ''' nueva: sin el, la lectura interpreta la lista como vacia.</summary>
        <Extension>
        Public Sub PonerPalabrasClave(npc As INpc, lista As IEnumerable(Of UInteger))
            If npc Is Nothing Then Return
            Dim traiaContador = npc.KeywordsKeywordCountPresente
            While npc.Keywords.Count > 0
                If Not npc.QuitarKeywords(0) Then Exit While
            End While
            Dim cuantas = 0
            If lista IsNot Nothing Then
                For Each k In lista
                    Dim e = npc.AgregarKeywords()
                    If e Is Nothing Then Continue For
                    e.Keyword = k
                    cuantas += 1
                Next
            End If
            If cuantas > 0 OrElse traiaContador Then
                npc.KeywordsKeywordCount = CUInt(cuantas)
            Else
                npc.QuitarSubrecord("KSIZ")
                npc.QuitarSubrecord("KWDA")
            End If
        End Sub

        ''' <summary>Reemplaza los efectos de actor por los de la lista. El contador SPCT queda con la cuenta
        ''' nueva, o se saca cuando no queda ninguno y el record tampoco lo traia.</summary>
        <Extension>
        Public Sub PonerEfectosDeActor(npc As INpc, lista As IEnumerable(Of UInteger))
            If npc Is Nothing Then Return
            Dim traiaContador = npc.CountPresente
            While npc.ActorEffects.Count > 0
                If Not npc.QuitarActorEffects(0) Then Exit While
            End While
            Dim cuantos = 0
            If lista IsNot Nothing Then
                For Each efecto In lista
                    Dim e = npc.AgregarActorEffects()
                    If e Is Nothing Then Continue For
                    e.ActorEffect = efecto
                    cuantos += 1
                Next
            End If
            If cuantos > 0 OrElse traiaContador Then
                npc.Count = CUInt(cuantos)
            Else
                npc.QuitarSubrecord("SPCT")
            End If
        End Sub

        ''' <summary>Reemplaza los enganches (APPR) por los de la lista. Solo Fallout 4.</summary>
        <Extension>
        Public Sub PonerRanurasDeEnganche(npc As INpc, lista As IEnumerable(Of UInteger))
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return
            While nf.AttachParentSlots.Count > 0
                If Not nf.QuitarAttachParentSlots(0) Then Exit While
            End While
            If lista Is Nothing Then Return
            For Each k In lista
                Dim e = nf.AgregarAttachParentSlots()
                If e IsNot Nothing Then e.Keyword = k
            Next
        End Sub

        ''' <summary>KWDA: las palabras clave que declara el record, sin las que valen cero.</summary>
        <Extension>
        Public Function PalabrasClave(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If npc Is Nothing Then Return salida
            For Each k In npc.Keywords
                If k.Keyword <> 0UI Then salida.Add(k.Keyword)
            Next
            Return salida
        End Function

        ''' <summary>APPR: los enganches que declara el record. Solo Fallout 4.</summary>
        <Extension>
        Public Function RanurasDeEnganche(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return salida
            For Each s In nf.AttachParentSlots
                If s.Keyword <> 0UI Then salida.Add(s.Keyword)
            Next
            Return salida
        End Function

        ''' <summary>TPTA: el actor del que hereda una categoria, o cero si esa ranura esta vacia.
        ''' Solo Fallout 4 -en Skyrim toda la herencia sale de TPLT-.</summary>
        <Extension>
        Public Function ActorDePlantilla(npc As INpc, categoria As NPC_TemplateCategory) As UInteger
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return 0UI
            Select Case categoria
                Case NPC_TemplateCategory.Traits : Return nf.TemplateActorsTraits
                Case NPC_TemplateCategory.Stats : Return nf.TemplateActorsStats
                Case NPC_TemplateCategory.Factions : Return nf.TemplateActorsFactions
                Case NPC_TemplateCategory.SpellList : Return nf.TemplateActorsSpellList
                Case NPC_TemplateCategory.AIData : Return nf.TemplateActorsAIData
                Case NPC_TemplateCategory.AIPackages : Return nf.TemplateActorsAIPackages
                Case NPC_TemplateCategory.ModelAnimation : Return nf.TemplateActorsModelAnimation
                Case NPC_TemplateCategory.BaseData : Return nf.TemplateActorsBaseData
                Case NPC_TemplateCategory.Inventory : Return nf.TemplateActorsInventory
                Case NPC_TemplateCategory.Script : Return nf.TemplateActorsScript
                Case NPC_TemplateCategory.DefaultPackageList : Return nf.TemplateActorsDefPackageList
                Case NPC_TemplateCategory.AttackData : Return nf.TemplateActorsAttackData
                Case NPC_TemplateCategory.Keywords : Return nf.TemplateActorsKeywords
            End Select
            Return 0UI
        End Function

        ''' <summary>TPTA: los actores de los que hereda por categoria, sin las ranuras vacias. Solo
        ''' Fallout 4 -en Skyrim toda la herencia sale de TPLT-.</summary>
        <Extension>
        Public Function ActoresDePlantilla(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            For Each cat As NPC_TemplateCategory In CategoriasDePlantilla
                Dim actor = ActorDePlantilla(npc, cat)
                If actor <> 0UI AndAlso Not salida.Contains(actor) Then salida.Add(actor)
            Next
            Return salida
        End Function

        ''' <summary>MSDK + MSDV: los morfos del editor de personaje, emparejados por posicion. Solo
        ''' Fallout 4. Las dos listas son paralelas en el record; aca se juntan porque todo lo que los
        ''' consume busca por clave.</summary>
        <Extension>
        Public Function MorfosDeCara(npc As INpc) As Dictionary(Of UInteger, Single)
            Dim salida As New Dictionary(Of UInteger, Single)
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return salida
            Dim claves = nf.MorphKeys
            Dim valores = nf.MorphValues
            For i = 0 To Math.Min(claves.Count, valores.Count) - 1
                salida(claves(i).Key) = valores(i).Value
            Next
            Return salida
        End Function

        ''' <summary>Reemplaza los morfos del editor de personaje. El orden de las claves es el que
        ''' se emite, y los valores van en el mismo orden: son dos listas paralelas.</summary>
        <Extension>
        Public Sub PonerMorfosDeCara(npc As INpc, morfos As IEnumerable(Of KeyValuePair(Of UInteger, Single)))
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return
            While nf.MorphKeys.Count > 0
                If Not nf.QuitarMorphKeys(0) Then Exit While
            End While
            While nf.MorphValues.Count > 0
                If Not nf.QuitarMorphValues(0) Then Exit While
            End While
            If morfos Is Nothing Then Return
            For Each kv In morfos
                Dim k = nf.AgregarMorphKeys()
                If k IsNot Nothing Then k.Key = kv.Key
                Dim v = nf.AgregarMorphValues()
                If v IsNot Nothing Then v.Value = kv.Value
            Next
        End Sub

        ''' <summary>MRSV: los cinco valores de region del cuerpo, en orden. Solo Fallout 4; lista
        ''' vacia si el record no trae el subrecord.</summary>
        <Extension>
        Public Function ValoresDeRegionCorporal(npc As INpc) As List(Of Single)
            Dim salida As New List(Of Single)
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing OrElse Not nf.BodyMorphRegionValuesHeadPresente Then Return salida
            salida.Add(nf.BodyMorphRegionValuesHead)
            salida.Add(nf.BodyMorphRegionValuesUpperTorso)
            salida.Add(nf.BodyMorphRegionValuesArms)
            salida.Add(nf.BodyMorphRegionValuesLowerTorso)
            salida.Add(nf.BodyMorphRegionValuesLegs)
            Return salida
        End Function

        ''' <summary>Escribe los cinco valores de region del cuerpo. Los que falten quedan en cero.</summary>
        <Extension>
        Public Sub PonerValoresDeRegionCorporal(npc As INpc, valores As IList(Of Single))
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing OrElse valores Is Nothing Then Return
            nf.BodyMorphRegionValuesHead = ValorEnLista(valores, 0)
            nf.BodyMorphRegionValuesUpperTorso = ValorEnLista(valores, 1)
            nf.BodyMorphRegionValuesArms = ValorEnLista(valores, 2)
            nf.BodyMorphRegionValuesLowerTorso = ValorEnLista(valores, 3)
            nf.BodyMorphRegionValuesLegs = ValorEnLista(valores, 4)
        End Sub

        Private Function ValorEnLista(lista As IList(Of Single), i As Integer) As Single
            If lista Is Nothing OrElse i >= lista.Count Then Return 0.0F
            Return lista(i)
        End Function

        ''' <summary>NAM9: los 19 deslizadores de cara de Skyrim, en el orden del formato. Nothing
        ''' cuando el record no los trae o no es de Skyrim.</summary>
        <Extension>
        Public Function DeslizadoresDeCara(npc As INpc) As Single()
            Dim ns = TryCast(npc, NpcSSE)
            If ns Is Nothing OrElse Not ns.FaceMorphNoseLongShortPresente Then Return Nothing
            Return New Single() {
                ns.FaceMorphNoseLongShort, ns.FaceMorphNoseUpDown, ns.FaceMorphJawUpDown,
                ns.FaceMorphJawNarrowWide, ns.FaceMorphJawFarwardBack, ns.FaceMorphCheeksUpDown,
                ns.FaceMorphCheeksFarwardBack, ns.FaceMorphEyesUpDown, ns.FaceMorphEyesInOut,
                ns.FaceMorphBrowsUpDown, ns.FaceMorphBrowsInOut, ns.FaceMorphBrowsFarwardBack,
                ns.FaceMorphLipsUpDown, ns.FaceMorphLipsInOut, ns.FaceMorphChinNarrowWide,
                ns.FaceMorphChinUpDown, ns.FaceMorphChinUnderbiteOverbite, ns.FaceMorphEyesFarwardBack,
                ns.FaceMorphVampireMorph}
        End Function

        ''' <summary>Escribe los 19 deslizadores de cara de Skyrim. Los que falten quedan como estaban.</summary>
        <Extension>
        Public Sub PonerDeslizadoresDeCara(npc As INpc, v As Single())
            Dim ns = TryCast(npc, NpcSSE)
            If ns Is Nothing OrElse v Is Nothing Then Return
            If v.Length > 0 Then ns.FaceMorphNoseLongShort = v(0)
            If v.Length > 1 Then ns.FaceMorphNoseUpDown = v(1)
            If v.Length > 2 Then ns.FaceMorphJawUpDown = v(2)
            If v.Length > 3 Then ns.FaceMorphJawNarrowWide = v(3)
            If v.Length > 4 Then ns.FaceMorphJawFarwardBack = v(4)
            If v.Length > 5 Then ns.FaceMorphCheeksUpDown = v(5)
            If v.Length > 6 Then ns.FaceMorphCheeksFarwardBack = v(6)
            If v.Length > 7 Then ns.FaceMorphEyesUpDown = v(7)
            If v.Length > 8 Then ns.FaceMorphEyesInOut = v(8)
            If v.Length > 9 Then ns.FaceMorphBrowsUpDown = v(9)
            If v.Length > 10 Then ns.FaceMorphBrowsInOut = v(10)
            If v.Length > 11 Then ns.FaceMorphBrowsFarwardBack = v(11)
            If v.Length > 12 Then ns.FaceMorphLipsUpDown = v(12)
            If v.Length > 13 Then ns.FaceMorphLipsInOut = v(13)
            If v.Length > 14 Then ns.FaceMorphChinNarrowWide = v(14)
            If v.Length > 15 Then ns.FaceMorphChinUpDown = v(15)
            If v.Length > 16 Then ns.FaceMorphChinUnderbiteOverbite = v(16)
            If v.Length > 17 Then ns.FaceMorphEyesFarwardBack = v(17)
            If v.Length > 18 Then ns.FaceMorphVampireMorph = v(18)
        End Sub

        ''' <summary>NAMA: las cuatro partes de cara de Skyrim (nariz, desconocida, ojos, boca).
        ''' Nothing cuando el record no las trae o no es de Skyrim.</summary>
        <Extension>
        Public Function PartesDeCara(npc As INpc) As UInteger()
            Dim ns = TryCast(npc, NpcSSE)
            If ns Is Nothing OrElse Not ns.FacePartsNosePresente Then Return Nothing
            ' El segundo campo lo declara el formato con signo y los otros tres sin signo. Convertirlo
            ' con CUInt tira cuando trae un valor negativo, asi que se reinterpretan los bits: aca las
            ' cuatro partes viajan como un numero opaco, no como una cantidad.
            Return New UInteger() {ns.FacePartsNose,
                                   CUInt(CLng(ns.FacePartsUnknown) And &HFFFFFFFFL),
                                   ns.FacePartsEyes,
                                   ns.FacePartsMouth}
        End Function

        ''' <summary>Escribe las cuatro partes de cara de Skyrim.</summary>
        <Extension>
        Public Sub PonerPartesDeCara(npc As INpc, v As UInteger())
            Dim ns = TryCast(npc, NpcSSE)
            If ns Is Nothing OrElse v Is Nothing Then Return
            If v.Length > 0 Then ns.FacePartsNose = v(0)
            ' El segundo de los cuatro lo declara el formato CON signo y los otros tres sin signo, asi
            ' que asignarle el valor tal cual tira cuando no entra en el rango positivo. Se reinterpretan
            ' los bits, igual que al leerlo: aca las cuatro partes viajan como un numero opaco.
            If v.Length > 1 Then ns.FacePartsUnknown = CInt(CLng(v(1)) - If(v(1) > &H7FFFFFFFUI, 4294967296L, 0L))
            If v.Length > 2 Then ns.FacePartsEyes = v(2)
            If v.Length > 3 Then ns.FacePartsMouth = v(3)
        End Sub

        ''' <summary>NAM4. Solo Fallout 4: es el maximo del rango de altura cuyo minimo esta en NAM6.
        ''' Skyrim no declara el subrecord -su NAM6 es la altura a secas- y ahi vale cero.</summary>
        <Extension>
        Public Function AlturaMaxima(npc As INpc) As Single
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return 0.0F
            Return nf.HeightMax
        End Function

        ''' <summary>El record trae NAM4.</summary>
        <Extension>
        Public Function TieneAlturaMaxima(npc As INpc) As Boolean
            Dim nf = TryCast(npc, NpcFO4)
            Return nf IsNot Nothing AndAlso nf.HeightMaxPresente
        End Function

        ''' <summary>Escribe NAM4. En Skyrim no hay donde: no hace nada.</summary>
        <Extension>
        Public Sub PonerAlturaMaxima(npc As INpc, valor As Single)
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then nf.HeightMax = valor
        End Sub

        ''' <summary>FMIN. Solo Fallout 4. Sin el subrecord vale 1, que es el neutro.</summary>
        <Extension>
        Public Function IntensidadDeMorfoFacial(npc As INpc) As Single
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing OrElse Not nf.FacialMorphIntensityPresente Then Return 1.0F
            Return nf.FacialMorphIntensity
        End Function

        ''' <summary>El record trae FMIN.</summary>
        <Extension>
        Public Function TieneIntensidadDeMorfoFacial(npc As INpc) As Boolean
            Dim nf = TryCast(npc, NpcFO4)
            Return nf IsNot Nothing AndAlso nf.FacialMorphIntensityPresente
        End Function

        ''' <summary>Escribe FMIN. En Skyrim no hay donde: no hace nada.</summary>
        <Extension>
        Public Sub PonerIntensidadDeMorfoFacial(npc As INpc, valor As Single)
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then nf.FacialMorphIntensity = valor
        End Sub

        ''' <summary>El record trae el bloque de combinaciones de mods (OBTE). Solo Fallout 4.</summary>
        <Extension>
        Public Function TieneCombinaciones(npc As INpc) As Boolean
            Dim nf = TryCast(npc, NpcFO4)
            Return nf IsNot Nothing AndAlso nf.ObjectTemplateCountPresente
        End Function

        ''' <summary>NAM7. En Skyrim es el peso del cuerpo -0 a 100-; en Fallout 4 el subrecord existe
        ''' pero no lleva dato, asi que ahi vale cero.</summary>
        <Extension>
        Public Function PesoDeSkyrim(npc As INpc) As Single
            Dim ns = TryCast(npc, NpcSSE)
            If ns Is Nothing OrElse Not ns.WeightPresente Then Return 0.0F
            Return ns.Weight
        End Function

        ''' <summary>El record trae el peso de Skyrim.</summary>
        <Extension>
        Public Function TienePesoDeSkyrim(npc As INpc) As Boolean
            Dim ns = TryCast(npc, NpcSSE)
            Return ns IsNot Nothing AndAlso ns.WeightPresente
        End Function

        ''' <summary>Escribe el peso del cuerpo de Skyrim. En Fallout 4 no hay donde: no hace nada.</summary>
        <Extension>
        Public Sub PonerPesoDeSkyrim(npc As INpc, valor As Single)
            Dim ns = TryCast(npc, NpcSSE)
            If ns IsNot Nothing Then ns.Weight = valor
        End Sub

        ''' <summary>SPLO: los efectos de actor que declara el record, sin los que valen cero.</summary>
        <Extension>
        Public Function EfectosDeActor(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If npc Is Nothing Then Return salida
            For Each e In npc.ActorEffects
                If e.ActorEffect <> 0UI Then salida.Add(e.ActorEffect)
            Next
            Return salida
        End Function

        ''' <summary>PKID: los paquetes de IA que declara el record, sin los que valen cero.</summary>
        <Extension>
        Public Function PaquetesDeIA(npc As INpc) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If npc Is Nothing Then Return salida
            For Each p In npc.Packages
                If p.Package <> 0UI Then salida.Add(p.Package)
            Next
            Return salida
        End Function

        ''' <summary>El alpha de QNAM, que en Fallout 4 es la OPACIDAD con la que el tono de piel se
        ''' compone sobre el cuerpo. Skyrim no tiene alpha en QNAM: ahi, y cuando el record no trae el
        ''' subrecord, vale 1 -opaco-, que es el neutro.</summary>
        <Extension>
        Public Function AlphaDeIluminacionDeTextura(npc As INpc) As Single
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing OrElse Not nf.TextureLightingAlphaPresente Then Return 1.0F
            Return nf.TextureLightingAlpha
        End Function

        ''' <summary>BCLF. Solo Fallout 4: Skyrim no declara un color de barba aparte.</summary>
        <Extension>
        Public Function ColorDeBarba(npc As INpc) As UInteger
            Dim nf = TryCast(npc, NpcFO4)
            If nf Is Nothing Then Return 0UI
            Return nf.FacialHairColor
        End Function

        ''' <summary>El record trae BCLF.</summary>
        <Extension>
        Public Function TieneColorDeBarba(npc As INpc) As Boolean
            Dim nf = TryCast(npc, NpcFO4)
            Return nf IsNot Nothing AndAlso nf.FacialHairColorPresente
        End Function

        ''' <summary>Escribe BCLF. En Skyrim no hay donde: no hace nada.</summary>
        <Extension>
        Public Sub PonerColorDeBarba(npc As INpc, fid As UInteger)
            Dim nf = TryCast(npc, NpcFO4)
            If nf IsNot Nothing Then nf.FacialHairColor = fid
        End Sub

        Private Function CanalDeColorNormalizado(value As Single) As Integer
            If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return 255
            Dim normalizado = value
            If normalizado <= 1.0F Then normalizado *= 255.0F
            Return Math.Max(0, Math.Min(255, CInt(Math.Round(normalizado))))
        End Function
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
            ' Sólo se reescribe si la sustitución YA la traía. Escribir vacío CREA el campo, y una
            ' ida y vuelta por el editor le agregaría a cada sustitución un subrecord que la fuente
            ' no tenía. El campo está declarado como obsoleto: se conserva el que viene, no se
            ' inventa uno.
            If Not String.IsNullOrEmpty(CarpetaObsoleta) Then
                e.SubstitutionTreeFolderObsolete = CarpetaObsoleta
            End If
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
