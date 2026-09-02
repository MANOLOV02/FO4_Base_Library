Imports System.Xml.Linq

''' <summary>Guardar un <see cref="XDocument"/> a disco con la red de <c>GuardarConCopia</c>.
'''
''' <para>⛔ POR QUE EXISTE, Y POR QUE ACA. <c>XDocument.Save(String)</c> abre el destino con
''' <c>CREATE_ALWAYS</c>: sobre un archivo OCULTO da <c>ACCESS_DENIED</c>, y un archivo nuevo rompe el VFS
''' de Mod Organizer y el hardlink de Vortex. La ley completa —y por que no puede ser atomica— vive en
''' <c>Ba2_Bsa_Library\EscrituraEnElLugar.vb</c> y no se repite aca.</para>
'''
''' <para>⛔ Y VA EN LA LIBRERIA COMPARTIDA, no adentro de un consumidor, porque el archivo que motiva
''' esto lo escriben DOS aplicaciones: el catalogo de poses <c>WardrobeManagerPoses.xml</c> lo tocan
''' Wardrobe Manager y NPC Manager. Si cada una deriva sus <see cref="Xml.XmlWriterSettings"/> por su
''' cuenta, el mismo archivo sale con bytes distintos segun quien lo guardo — indentacion o encoding
''' distintos— y el diff que el usuario ve depende de que app abrio ultimo.</para>
'''
''' <para>⚠️ DUPLICACION CONOCIDA Y DECLARADA: <c>Wardrobe_Manager\WM_EscrituraTexto.vb</c> tiene hoy una
''' copia privada e identica de esta derivacion (<c>GuardarXDocumentConCopia</c>). Es de donde salio esta,
''' y migrarla a llamar aca es un cambio de una linea que le corresponde a esa app. Mientras tanto las dos
''' derivan igual A PROPOSITO y cualquier cambio va en LAS DOS o en ninguna.</para></summary>
Public Module EscrituraXml

    ''' <summary>Escribe <paramref name="doc"/> en <paramref name="destino"/> con copia previa verificada.
    ''' <para>⛔ Los <see cref="Xml.XmlWriterSettings"/> replican EXACTAMENTE lo que hace
    ''' <c>XDocument.Save(String)</c>: <c>Indent = True</c> y, si el documento declara un encoding, ESE
    ''' encoding (con el mismo <c>Catch ArgumentException</c> que deja el default UTF-8 cuando el nombre no
    ''' lo conoce el framework). Sin esa derivacion, pasar por un <c>XmlWriter</c> propio cambia los bytes
    ''' del archivo aunque el XML sea el mismo.</para>
    ''' <para>El cuerpo escribe y NO cierra el stream: el dueño es <c>EscrituraEnElLugar</c>. Por eso se
    ''' serializa primero a memoria y se vuelca el buffer — asi la trampa del <c>leaveOpen</c> ni se
    ''' presenta.</para></summary>
    Public Sub GuardarXDocumentConCopia(destino As String, doc As XDocument)
        If String.IsNullOrEmpty(destino) Then Throw New ArgumentException("Empty path.", NameOf(destino))
        If doc Is Nothing Then Throw New ArgumentNullException(NameOf(doc))

        Dim opciones As New Xml.XmlWriterSettings With {.Indent = True}
        If doc.Declaration IsNot Nothing AndAlso Not String.IsNullOrEmpty(doc.Declaration.Encoding) Then
            Try
                opciones.Encoding = Text.Encoding.GetEncoding(doc.Declaration.Encoding)
            Catch ex As ArgumentException
                ' Un nombre de encoding que el framework no conoce: se queda el default (UTF-8), que es
                ' exactamente lo que hace XDocument.Save(String) con el mismo Catch.
            End Try
        End If

        Dim bytes As Byte()
        Using ms As New IO.MemoryStream()
            Using xw = Xml.XmlWriter.Create(ms, opciones)
                doc.Save(xw)
            End Using
            bytes = ms.ToArray()
        End Using

        BSA_BA2_Library_DLL.EscrituraEnElLugar.GuardarConCopia(
            destino, Sub(fs) fs.Write(bytes, 0, bytes.Length))
    End Sub

End Module
