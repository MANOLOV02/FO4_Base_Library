Option Strict On
Option Explicit On

Imports System.Xml.Linq

''' <summary>El XML de HDT-SMP de Skyrim, leído por la MISMA ley en toda la app.
'''
''' <para>El vínculo lo declara el NIF: un <c>NiStringExtraData</c> en el ROOT llamado
''' <c>"HDT Skinned Mesh Physics Object"</c> cuyo <c>StringData</c> es la ruta del XML con prefijo
''' <c>"Data\"</c>. Eso es lo ÚNICO que el motor lee — el sidecar same-basename es sólo una convención
''' de Outfit Studio.</para>
'''
''' <para>⛔ Y el XML liga la física <b>POR NOMBRE DE SHAPE</b>: <c>&lt;per-vertex-shape name="X"&gt;</c> y
''' <c>&lt;per-triangle-shape name="X"&gt;</c> (los dos únicos tags que nombran shapes; verificado en el
''' fuente de referencia, <c>3rd party references\BodySlide-and-Outfit-Studio\src\physics\SystemBuilder.cpp:434</c> y
''' <c>:441</c>). Si alguien RENOMBRA la shape y no el tag, el motor carga el XML, no encuentra la shape,
''' y la física queda muerta <b>sin un solo error</b>. Para eso está
''' <see cref="NombresDeShape"/>.</para></summary>
Public NotInheritable Class SmpPhysicsXml
    Private Sub New()
    End Sub

    ''' <summary>Quita el prefijo <c>"Data\"</c> (case-insensitive) y normaliza separadores.</summary>
    Public Shared Function SinPrefijoData(ruta As String) As String
        If String.IsNullOrWhiteSpace(ruta) Then Return ""
        Dim s = ruta.Trim().Replace("/"c, "\"c)
        If s.StartsWith("Data\", StringComparison.OrdinalIgnoreCase) Then s = s.Substring("Data\".Length)
        Return s
    End Function

    ''' <summary>Lee el XML por su ruta Data-relative: primero <c>FilesDictionary</c> (loose + BA2, que es
    ''' lo que ve el motor), y sólo si ahí no está, el disco. Devuelve Nothing si no se pudo.</summary>
    Public Shared Function LeerPorRutaRelativa(rel As String, Optional raizDeDatos As String = Nothing) As String
        If String.IsNullOrWhiteSpace(rel) Then Return Nothing
        Try
            Dim bytes = FilesDictionary_class.GetBytes(rel)
            If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                Using ms As New IO.MemoryStream(bytes)
                    Using sr As New IO.StreamReader(ms, Text.Encoding.UTF8, detectEncodingFromByteOrderMarks:=True)
                        Return sr.ReadToEnd()
                    End Using
                End Using
            End If
        Catch
        End Try
        Try
            If Not String.IsNullOrEmpty(raizDeDatos) Then
                Dim abs = IO.Path.Combine(raizDeDatos, rel)
                If IO.File.Exists(abs) Then Return IO.File.ReadAllText(abs, Text.Encoding.UTF8)
            End If
        Catch
        End Try
        Return Nothing
    End Function

    ''' <summary>True si el string es XML bien formado cuya raiz es una raiz conocida de HDT-SMP:
    ''' <c>&lt;system&gt;</c> (SMP clasico) o <c>&lt;hdt-smp&gt;</c> (SMP 3.x).</summary>
    Public Shared Function EsXmlValido(contenido As String) As Boolean
        If String.IsNullOrWhiteSpace(contenido) Then Return False
        Try
            Dim doc As New Xml.XmlDocument()
            doc.LoadXml(contenido)
            Dim root = doc.DocumentElement
            If root Is Nothing Then Return False
            Return root.LocalName.Equals("system", StringComparison.OrdinalIgnoreCase) OrElse
                   root.LocalName.Equals("hdt-smp", StringComparison.OrdinalIgnoreCase)
        Catch ex As Xml.XmlException
            Return False
        End Try
    End Function

    ''' <summary>Resuelve el contenido del XML de fisica HDT-SMP (SSE) de forma AUTORITATIVA: primero el
    ''' path declarado por el NiStringExtraData "HDT Skinned Mesh Physics Object" del NIF (resuelto via
    ''' FilesDictionary y luego disco, o sea loose+BA2 en cualquier carpeta de Data), y como fallback la
    ''' convencion sidecar same-basename en disco. El link in-NIF es la fuente de verdad del motor (igual
    ''' que HH_OFFSET para tacones); el sidecar es solo una convencion que no todos los mods siguen (KS
    ''' Hairdos apunta a HDT\XML\). Nothing si no hay fisica SMP o el juego no es Skyrim.
    ''' <para>Vive aca y no en Wardrobe Manager porque el consumidor son los DOS proyectos: WM la usa al
    ''' cargar un sliderSet y NPC Manager al construir FaceGen. Tenerla duplicada era la misma ley en dos
    ''' lados.</para></summary>
    Public Shared Function ResolverXmlDeFisica(nif As Nifcontent_Class_Manolo,
                                               sidecarEnDisco As String,
                                               Optional raizDeDatos As String = Nothing) As String
        If Config_App.Current Is Nothing OrElse Config_App.Current.Game <> Config_App.Game_Enum.Skyrim Then Return Nothing

        If nif IsNot Nothing Then
            Dim pathInNif = nif.TryGetSmpPhysicsXmlPath()
            If Not String.IsNullOrWhiteSpace(pathInNif) Then
                Dim raw = LeerPorRutaRelativa(SinPrefijoData(pathInNif), raizDeDatos)
                If raw IsNot Nothing AndAlso EsXmlValido(raw) Then Return raw
            End If
        End If

        If Not String.IsNullOrEmpty(sidecarEnDisco) AndAlso IO.File.Exists(sidecarEnDisco) Then
            Dim raw = IO.File.ReadAllText(sidecarEnDisco, Text.Encoding.UTF8)
            If EsXmlValido(raw) Then Return raw
        End If

        Return Nothing
    End Function

    ''' <summary>Los nombres de shape que el XML referencia. Conjunto VACÍO si el XML no se pudo parsear o
    ''' no nombra ninguna — y esa distinción importa: "no pude leerlo" no es "no referencia nada", por eso
    ''' el llamador tiene que mirar <paramref name="parseo"/> antes de concluir.</summary>
    Public Shared Function NombresDeShape(xml As String, ByRef parseo As Boolean) As HashSet(Of String)
        Dim salida As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        parseo = False
        If String.IsNullOrWhiteSpace(xml) Then Return salida
        Dim doc As XDocument
        Try
            doc = XDocument.Parse(xml)
        Catch
            Return salida
        End Try
        parseo = True
        For Each el In doc.Descendants()
            Dim ln = el.Name.LocalName
            If Not (ln.Equals("per-vertex-shape", StringComparison.OrdinalIgnoreCase) OrElse
                    ln.Equals("per-triangle-shape", StringComparison.OrdinalIgnoreCase)) Then Continue For
            Dim a = el.Attribute("name")
            If a IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(a.Value) Then salida.Add(a.Value.Trim())
        Next
        Return salida
    End Function

    ''' <summary>Renombra en el XML la shape <paramref name="viejo"/> a <paramref name="nuevo"/> en los MISMOS dos tags y
    ''' con la MISMA comparacion que <see cref="NombresDeShape"/> (Trim + OrdinalIgnoreCase: los nombres del motor son
    ''' BSFixedString, sin mayusculas — SystemBuilder.cpp:347-348; el Trim es la regla existente de NombresDeShape).
    ''' <para>⛔ CONTRATO: el string de salida es el de entrada salvo los spans CRUDOS de los valores renombrados. No se
    ''' re-serializa el documento (XmlDocument cambiaria comillas, entidades, elementos vacios y la declaracion). La
    ''' codificacion y el BOM del archivo los decide quien lo escribe (Save del proyecto), no esta funcion.</para>
    ''' <para>Ubicacion: <c>IXmlLineInfo</c> da linea/columna del NOMBRE del atributo, contando como un salto
    ''' <c>\r\n</c>, <c>\r</c> o <c>\n</c> (normalizacion de fin de linea, XML 1.0 §2.11) y columnas en chars del
    ''' texto; desde ahi se escanea <c>name</c>, espacios, <c>=</c>, espacios y la comilla hasta el valor crudo. La
    ''' decision usa el valor DECODIFICADO; se reemplaza el span crudo (entidades incluidas), conservando los espacios
    ''' literales alrededor del nucleo.</para></summary>
    ''' <returns>(xml resultante, valores renombrados, si el XML se pudo parsear). Si no parsea, se devuelve el
    ''' original intacto con Parseo = False.</returns>
    Public Shared Function RenombrarShape(xml As String, viejo As String, nuevo As String) As (Xml As String, Cambios As Integer, Parseo As Boolean)
        If String.IsNullOrEmpty(xml) Then Return (xml, 0, True)
        If String.IsNullOrWhiteSpace(viejo) Then Throw New ArgumentException("The old name cannot be empty.", NameOf(viejo))
        If String.IsNullOrEmpty(nuevo) Then Throw New ArgumentException("The new name cannot be empty.", NameOf(nuevo))
        Dim objetivo = viejo.Trim()

        Dim inicios As New List(Of Integer) From {0}
        Dim i = 0
        While i < xml.Length
            Dim c = xml(i)
            If c = ControlChars.Cr Then
                If i + 1 < xml.Length AndAlso xml(i + 1) = ControlChars.Lf Then i += 1
                inicios.Add(i + 1)
            ElseIf c = ControlChars.Lf Then
                inicios.Add(i + 1)
            End If
            i += 1
        End While

        Dim spans As New List(Of (Inicio As Integer, Largo As Integer, Comilla As Char))
        Try
            ' DTD prohibido: la MISMA definicion de "parseable" que NombresDeShape (XDocument.Parse lo prohibe).
            Dim opciones As New System.Xml.XmlReaderSettings With {.DtdProcessing = System.Xml.DtdProcessing.Prohibit}
            Using sr As New IO.StringReader(xml)
                Using rd = System.Xml.XmlReader.Create(sr, opciones)
                    Dim info = DirectCast(rd, System.Xml.IXmlLineInfo)
                    While rd.Read()
                        If rd.NodeType <> System.Xml.XmlNodeType.Element Then Continue While
                        Dim ln = rd.LocalName
                        If Not (ln.Equals("per-vertex-shape", StringComparison.OrdinalIgnoreCase) OrElse
                                ln.Equals("per-triangle-shape", StringComparison.OrdinalIgnoreCase)) Then Continue While
                        If Not rd.MoveToAttribute("name") Then Continue While
                        If String.Equals(rd.Value.Trim(), objetivo, StringComparison.OrdinalIgnoreCase) Then
                            spans.Add(ValorCrudoDelAtributo(xml, inicios(info.LineNumber - 1) + info.LinePosition - 1, "name"))
                        End If
                        rd.MoveToElement()
                    End While
                End Using
            End Using
        Catch ex As System.Xml.XmlException
            Return (xml, 0, False)
        Catch ex As InvalidOperationException
            ' La posicion que da el lector no cae en el atributo (p.ej. un U+FEFF inicial que el lector salta): no se
            ' puede ubicar el span crudo sin adivinar. Es "no se pudo", nunca una excepcion para el llamador (Merge
            ' llama a mitad de camino y su contrato es no lanzar).
            Return (xml, 0, False)
        End Try

        Dim sb As New Text.StringBuilder(xml)
        For Each s In spans.OrderByDescending(Function(x) x.Inicio)
            Dim crudo = xml.Substring(s.Inicio, s.Largo)
            Dim delante = crudo.Length - crudo.TrimStart(EspaciosXml).Length
            Dim detras = crudo.Length - crudo.TrimEnd(EspaciosXml).Length
            Dim largoNucleo = Math.Max(0, s.Largo - delante - detras)
            sb.Remove(s.Inicio + delante, largoNucleo)
            sb.Insert(s.Inicio + delante, EscaparValorDeAtributo(nuevo, s.Comilla))
        Next
        Return (sb.ToString(), spans.Count, True)
    End Function

    Private Shared ReadOnly EspaciosXml As Char() = {" "c, ControlChars.Tab, ControlChars.Cr, ControlChars.Lf}

    ''' <summary>Desde <paramref name="offsetNombre"/> (primer char del nombre del atributo) escanea
    ''' <c>nombre S? = S? comilla valor comilla</c> (XML 1.0 [41] Attribute, [25] Eq) y devuelve el span del valor crudo.</summary>
    Private Shared Function ValorCrudoDelAtributo(xml As String, offsetNombre As Integer, nombre As String) As (Inicio As Integer, Largo As Integer, Comilla As Char)
        If String.CompareOrdinal(xml, offsetNombre, nombre, 0, nombre.Length) <> 0 Then
            Throw New InvalidOperationException($"The XML reader position does not point to the '{nombre}' attribute.")
        End If
        Dim j = offsetNombre + nombre.Length
        While j < xml.Length AndAlso EspaciosXml.Contains(xml(j)) : j += 1 : End While
        If j >= xml.Length OrElse xml(j) <> "="c Then Throw New InvalidOperationException($"Malformed '{nombre}' attribute.")
        j += 1
        While j < xml.Length AndAlso EspaciosXml.Contains(xml(j)) : j += 1 : End While
        If j >= xml.Length OrElse (xml(j) <> """"c AndAlso xml(j) <> "'"c) Then Throw New InvalidOperationException($"Malformed '{nombre}' attribute.")
        Dim comilla = xml(j)
        Dim inicio = j + 1
        Dim fin = xml.IndexOf(comilla, inicio)
        If fin < 0 Then Throw New InvalidOperationException($"Malformed '{nombre}' attribute.")
        Return (inicio, fin - inicio, comilla)
    End Function

    ''' <summary>Valor de atributo escapado para ir entre <paramref name="comilla"/> (XML 1.0 [10] AttValue: ni
    ''' <c>&lt;</c>, ni <c>&amp;</c> suelto, ni la comilla que lo delimita).</summary>
    Private Shared Function EscaparValorDeAtributo(valor As String, comilla As Char) As String
        Dim s = valor.Replace("&", "&amp;").Replace("<", "&lt;")
        Return If(comilla = """"c, s.Replace("""", "&quot;"), s.Replace("'", "&apos;"))
    End Function
End Class
