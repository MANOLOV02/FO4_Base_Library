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
End Class
