Imports System.IO
Imports System.Text
Imports OpenTK.Mathematics

' ============================================================================
' TRI File Parser - Pure binary format ("PIRT" header)
' Parses BodySlide/Outfit Studio TRI morph files into typed data.
' No OSD, slider, LooksMenu, or Wardrobe Manager dependencies.
' ============================================================================

''' <summary>
''' A single morph entry from a TRI file: named morph with vertex offsets.
''' </summary>
Public Class TriMorphEntry
    ''' <summary>Morph name (e.g. "WeightThin", "WeightMuscular").</summary>
    Public Property Name As String = ""

    ''' <summary>Position or UV morph.</summary>
    Public Property MorphType As TriMorphType = TriMorphType.Position

    ''' <summary>Vertex index -> position delta (X,Y,Z). For UV morphs, Z=0.</summary>
    Public ReadOnly Property Offsets As New Dictionary(Of UShort, Vector3)()
End Class

''' <summary>TRI morph type.</summary>
Public Enum TriMorphType As Byte
    Position = 0
    UV = 1
End Enum

''' <summary>
''' Parsed TRI file containing morph data organized by shape name.
''' Shape name matching is case-sensitive (Ordinal) to match the original engine behavior.
''' </summary>
Public Class TriFile
    ''' <summary>
    ''' Shape name -&gt; list of morph entries. Claves CASE-SENSITIVE, igual que el
    ''' <c>std::map&lt;std::string, std::vector&lt;MorphDataPtr&gt;&gt;</c> de BSOS (TriFile.h:31), que es la
    ''' autoridad del ESCRITOR: dos shapes que difieren solo en caja son dos entradas distintas y las
    ''' dos se emiten. Colapsarlas perdia los morphs de la segunda al construir el .tri.
    '''
    ''' La busqueda case-insensitive del MOTOR vive en <see cref="GetMorphsForShape"/>, no aca — ver
    ''' la nota ahi.
    ''' </summary>
    Public ReadOnly Property ShapeMorphs As New Dictionary(Of String, List(Of TriMorphEntry))(StringComparer.Ordinal)

    ''' <summary>
    ''' Add a morph entry for a shape. Deduplica por (nombre, TIPO), no sólo por nombre.
    '''
    ''' El modelo del motor de SSE es exactamente ese: <c>BodyMorphMap</c> de skee64 es
    ''' <c>unordered_map&lt;SKEEFixedString, pair&lt;posicion, uv&gt;&gt;</c> (BodyMorphInterface.h:194) —
    ''' UN nombre de morph lleva datos de posicion en <c>.first</c> y de UV en <c>.second</c>, y
    ''' <c>ApplyMorphs</c> recibe dos functors, uno por cada cosa. Dedupear sólo por nombre (como hace
    ''' <c>TriFile::AddMorph</c> de BSOS, TriFile.cpp:282-288) descartaba la entrada UV de un morph que
    ''' tuviera las dos, perdiendo datos que RaceMenu sí aplica.
    ''' Para FO4 es indistinto: f4ee nunca lee la seccion UV.
    ''' <c>TriFile::Write</c> de BSOS ya filtra por tipo, asi que emitir ambas es valido en el formato.
    ''' </summary>
    Public Sub AddMorph(shapeName As String, entry As TriMorphEntry)
        Dim list As List(Of TriMorphEntry) = Nothing
        If Not ShapeMorphs.TryGetValue(shapeName, list) Then
            list = New List(Of TriMorphEntry)()
            ShapeMorphs(shapeName) = list
        End If
        ' Ordinal (case-SENSITIVE) igual que `searchData->name == data->name` de TriFile::AddMorph
        ' (TriFile.cpp:282-286): la autoridad de este metodo es el ESCRITOR de BSOS. Emitir los dos
        ' es equivalente para el motor, que dedupea case-insensitive quedandose con el primero.
        If Not list.Exists(Function(e) e.MorphType = entry.MorphType AndAlso
                                       String.Equals(e.Name, entry.Name, StringComparison.Ordinal)) Then
            list.Add(entry)
        End If
    End Sub

    ''' <summary>
    ''' Get all morph entries for a shape, or empty list.
    '''
    ''' Match exacto primero y, si falla, fallback case-insensitive: los mapas de shape de los DOS
    ''' motores comparan con <c>_stricmp</c> — f4ee <c>TriShapeMap : unordered_map&lt;F4EEFixedString,…&gt;</c>
    ''' (BodyMorphInterface.h:86 + StringTable.h:22-30) y skee64 igual (BodyMorphInterface.h:194 +
    ''' StringTable.h:28-36). El fallback devuelve la PRIMERA coincidencia, que es lo que hace
    ''' <c>emplace</c> ante claves equivalentes.
    '''
    ''' ⚠️ Esto NO habilita el match case-insensitive del NIF contra el .tri: ese paso lo hace
    ''' <c>object-&gt;GetObjectByName</c> (BodyMorphInterface.cpp:1372) y su sensibilidad a caja NO es
    ''' verificable desde este workspace (BSFixedString es StringCache::Ref con ctor nativo).
    ''' BodySlideMorphResolver mantiene su comparacion exacta a proposito.
    '''
    ''' El desempate ante varias claves equivalentes es la ORDINAL-MENOR, no la primera que devuelva
    ''' el Dictionary (cuyo orden .NET declara no especificado): el `emplace` del motor gana en orden
    ''' de ARCHIVO, y el archivo lo escribimos ordenado Ordinal (ver WriteSection).
    ''' </summary>
    Public Function GetMorphsForShape(shapeName As String) As List(Of TriMorphEntry)
        Dim list As List(Of TriMorphEntry) = Nothing
        If ShapeMorphs.TryGetValue(shapeName, list) Then Return list

        Dim bestKey As String = Nothing
        For Each kv In ShapeMorphs
            If String.Equals(kv.Key, shapeName, StringComparison.OrdinalIgnoreCase) Then
                If bestKey Is Nothing OrElse String.CompareOrdinal(kv.Key, bestKey) < 0 Then bestKey = kv.Key
            End If
        Next
        If bestKey IsNot Nothing Then Return ShapeMorphs(bestKey)
        ' Instancia nueva a proposito: devolver una lista Shared mutable desde una API publica de una
        ' libreria compartida deja que el primer caller que haga .Add envenene a todas las TriFile
        ' del proceso.
        Return New List(Of TriMorphEntry)(0)
    End Function

    ''' <summary>
    ''' Get a specific morph entry by shape and morph name, or Nothing.
    '''
    ''' Filtra por tipo, con Position por defecto, porque es lo unico que aplica el motor de FO4:
    ''' <c>GetTrishapeMap</c> lee la seccion de posiciones del PIRT y RETORNA sin tocar la seccion UV
    ''' (BodyMorphInterface.cpp:150-304). Sin el filtro, un morph que existiera SOLO en la seccion UV
    ''' se devolvia y sus deltas de UV se aplicaban como posiciones.
    ''' skee64 si lee la seccion UV (:953-1042), pero la aplica a UVs, no a vertices.
    ''' </summary>
    Public Function GetMorph(shapeName As String, morphName As String,
                             Optional morphType As TriMorphType = TriMorphType.Position) As TriMorphEntry
        Return GetMorphsForShape(shapeName).Find(
            Function(e) e.MorphType = morphType AndAlso e.Name.Equals(morphName, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>Write this TRI file to disk in PIRT binary format.</summary>
    Public Function Write(fileName As String) As Boolean
        Return TriFileWriter.WriteTriToFile(Me, fileName)
    End Function
End Class

''' <summary>
''' Parser for TRI binary files (PIRT header format).
''' Used by BodySlide/Outfit Studio for body and face morphs.
''' </summary>
Public Module TriFileParser

    ''' <summary>Parse a TRI file from a byte array. Throws FormatException on invalid data.</summary>
    Public Function ParseTriFromBytes(data As Byte()) As TriFile
        If data Is Nothing OrElse data.Length < 4 Then
            Throw New FormatException("Insufficient data: not enough bytes for TRI header.")
        End If

        Dim tri As New TriFile()

        Using ms As New MemoryStream(data, writable:=False)
            Using br As New BinaryReader(ms, Encoding.ASCII, leaveOpen:=False)
                If ValidateHeader(br) Then
                    ' Position morph section
                    ReadSection(br, ms, tri, TriMorphType.Position)

                    ' UV morph section
                    ReadSection(br, ms, tri, TriMorphType.UV)
                Else
                    tri = Nothing
                End If
            End Using
        End Using
        Return tri
    End Function

    Private Function ValidateHeader(br As BinaryReader) As Boolean
        Dim hdr = br.ReadBytes(4)
        If hdr Is Nothing OrElse hdr.Length <> 4 Then
            Throw New FormatException("Cannot read TRI header.")
        End If
        If Not (hdr(0) = &H50 AndAlso hdr(1) = &H49 AndAlso hdr(2) = &H52 AndAlso hdr(3) = &H54) Then
            Return False
        End If
        Return True
    End Function

    Private Sub ReadSection(br As BinaryReader, ms As MemoryStream, tri As TriFile, sectionType As TriMorphType)
        If ms.Position > ms.Length - 2 Then Return

        Dim shapeCount = br.ReadUInt16()

        For i = 0 To shapeCount - 1
            Dim shapeLen = CInt(br.ReadByte())
            Dim shapeName = ReadTriName(br, shapeLen)
            Dim morphCount = br.ReadUInt16()

            For m = 0 To morphCount - 1
                Dim morphLen = CInt(br.ReadByte())
                Dim morphName = ReadTriName(br, morphLen)
                Dim mult = br.ReadSingle()
                Dim vertexCount = br.ReadUInt16()

                Dim entry As New TriMorphEntry With {
                    .Name = morphName,
                    .MorphType = sectionType
                }

                For k = 0 To vertexCount - 1
                    Dim vid = br.ReadUInt16()

                    ' ⛔ Sin epsilon y ACUMULANDO, a proposito. La autoridad del LECTOR es el MOTOR, no el
                    ' editor de BodySlide:
                    '   • f4ee y skee64 cargan TODO delta sin filtrar por magnitud
                    '     (BodyMorphInterface.cpp:265-274 y :927-942) y lo aplican con
                    '     `vertices[idx] += delta * factor` (TriShapePacked/FullVertexData::ApplyMorph),
                    '     recorriendo el VECTOR: un id repetido se suma DOS veces.
                    '   • `!offset.IsZero(true)` de TriFile::Read (TriFile.cpp:81/:134) y el first-wins de
                    '     su `emplace` son el modelo in-memory del EDITOR, que es otra cosa.
                    ' El skip de cero exacto se conserva porque sumar 0 es inerte: no cambia el resultado.
                    ' (El epsilon SI es canonico del lado del ESCRITOR — ver IsOffsetNegligible en
                    ' TriFiles.vb, que replica el `!v.IsZero(true)` de BodySlideApp::WriteMorphTRI:1457.)
                    If sectionType = TriMorphType.Position Then
                        Dim sx = br.ReadInt16()
                        Dim sy = br.ReadInt16()
                        Dim sz = br.ReadInt16()
                        Dim x = CSng(sx) * mult
                        Dim y = CSng(sy) * mult
                        Dim z = CSng(sz) * mult
                        If Not (x = 0.0F AndAlso y = 0.0F AndAlso z = 0.0F) Then
                            Dim prev As Vector3
                            If entry.Offsets.TryGetValue(vid, prev) Then
                                entry.Offsets(vid) = New Vector3(prev.X + x, prev.Y + y, prev.Z + z)
                            Else
                                entry.Offsets(vid) = New Vector3(x, y, z)
                            End If
                        End If
                    Else
                        Dim sx = br.ReadInt16()
                        Dim sy = br.ReadInt16()
                        Dim x = CSng(sx) * mult
                        Dim y = CSng(sy) * mult
                        If Not (x = 0.0F AndAlso y = 0.0F) Then
                            Dim prev As Vector3
                            If entry.Offsets.TryGetValue(vid, prev) Then
                                entry.Offsets(vid) = New Vector3(prev.X + x, prev.Y + y, 0.0F)
                            Else
                                entry.Offsets(vid) = New Vector3(x, y, 0.0F)
                            End If
                        End If
                    End If
                Next

                If entry.Offsets.Count > 0 Then
                    tri.AddMorph(shapeName, entry)
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' Nombre del .tri como string .NET. Latin1, NO ASCII: es 1 byte = 1 char sin perdida, y es
    ''' exactamente lo que usa NiflySharp para NiStringRef (NiStringRef.cs:53-62), o sea que el nombre
    ''' leido del .tri y el nombre leido del NIF quedan comparables caracter a caracter.
    ''' Con Encoding.ASCII todo byte &gt;= 0x80 se decodificaba como '?' y la shape no matcheaba nunca —
    ''' BSOS en cambio lee los bytes crudos a un std::string (TriFile.cpp:49-51).
    ''' </summary>
    Private Function ReadTriName(br As BinaryReader, length As Integer) As String
        If length < 0 Then Throw New FormatException("Negative string length in TRI data.")
        If length = 0 Then Return ""
        Dim bytes = br.ReadBytes(length)
        If bytes Is Nothing OrElse bytes.Length <> length Then
            Throw New FormatException("Could not read expected name bytes from TRI data.")
        End If
        Return Encoding.Latin1.GetString(bytes)
    End Function

End Module

' ============================================================================
' Bethesda TriHead format parser ("FRTRI003" header)
' Used by vanilla FO4 for chargen face morphs.
' Morphs are dense (all vertices per morph), not sparse like PIRT.
' ============================================================================

''' <summary>
''' Parsed Bethesda TriHead file. Contains morph data for a single mesh.
''' Unlike TriFile (PIRT), this has one shape with all morphs, and morphs
''' are dense (deltas for every vertex, not sparse).
''' </summary>
Public Class TriHeadFile
    Public Property NumVertices As UInteger
    Public Property NumTriangles As UInteger
    Public Property NumMorphs As UInteger
    Public Property Morphs As New List(Of TriHeadMorph)
    ''' <summary>Base vertex positions from the FRTRI003 header section. Kept for diagnostic
    ''' logging (locate which vertex id corresponds to which anatomical region).</summary>
    Public Property BaseVertices As Vector3()

    ''' <summary>Get a morph by name (case-insensitive).</summary>
    Public Function GetMorph(name As String) As TriHeadMorph
        Return Morphs.Find(Function(m) m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
    End Function
End Class

''' <summary>A single morph from a TriHead file. Has deltas for ALL vertices.</summary>
Public Class TriHeadMorph
    Public Property Name As String = ""
    Public Property Multiplier As Single = 1.0F
    ''' <summary>Vertex deltas. Length = NumVertices. Index = vertex index.</summary>
    Public Property Vertices As Vector3()
    ''' <summary>True if this morph came from the mod-morph (addMorph) section — sparse per-region data.
    ''' Regular morphs (IsModMorph=False) are dense per-vertex chargen sliders like LipFeature1.</summary>
    Public Property IsModMorph As Boolean = False
End Class

''' <summary>Parser for Bethesda TriHead files (FRTRI003 header).</summary>
Public Module TriHeadParser

    ''' <summary>Parse a Bethesda TriHead file from bytes. Returns Nothing if not this format.</summary>
    Public Function ParseTriHeadFromBytes(data As Byte()) As TriHeadFile
        If data Is Nothing OrElse data.Length < 8 Then Return Nothing

        Using ms As New MemoryStream(data, writable:=False)
            Using br As New BinaryReader(ms, Encoding.ASCII, leaveOpen:=False)
                ' Header: "FR" (2 bytes) + "TRI" (3 bytes) + version "003" (3 bytes) = 8 bytes
                Dim ident = Encoding.ASCII.GetString(br.ReadBytes(2))
                If ident <> "FR" Then Return Nothing

                Dim fileType = Encoding.ASCII.GetString(br.ReadBytes(3))
                If fileType <> "TRI" Then Return Nothing

                Dim version = Encoding.ASCII.GetString(br.ReadBytes(3))

                ' 14 uint32 header fields
                Dim numVertices = br.ReadUInt32()
                Dim numTriangles = br.ReadUInt32()
                Dim numQuads = br.ReadUInt32()
                Dim unknown2 = br.ReadUInt32()
                Dim unknown3 = br.ReadUInt32()
                Dim numUV = br.ReadUInt32()
                Dim flags = br.ReadUInt32()
                Dim numMorphs = br.ReadUInt32()
                Dim numModifiers = br.ReadUInt32()     ' aka addMorphNum (stat/mod-morph count)
                Dim numModVertices = br.ReadUInt32()   ' aka addVertexNum (pool of absolute positions)
                Dim unknown7 = br.ReadUInt32()
                Dim unknown8 = br.ReadUInt32()
                Dim unknown9 = br.ReadUInt32()
                Dim unknown10 = br.ReadUInt32()

                ' Read base vertices (numVertices * 12 bytes) — we need these to compute mod-morph deltas later
                Dim baseVerts(CInt(numVertices) - 1) As Vector3
                For j = 0 To CInt(numVertices) - 1
                    baseVerts(j) = New Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                Next

                ' Read mod vertices (the shared absolute-position pool used by mod-morphs)
                Dim modVertsPool(CInt(numModVertices) - 1) As Vector3
                For j = 0 To CInt(numModVertices) - 1
                    modVertsPool(j) = New Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                Next

                ' Skip triangles (numTriangles * 3 * 4 bytes)
                br.ReadBytes(CInt(numTriangles) * 12)

                ' Skip UVs (numUV * 8 bytes)
                br.ReadBytes(CInt(numUV) * 8)

                ' Skip tex triangles (numTriangles * 3 * 4 bytes)
                br.ReadBytes(CInt(numTriangles) * 12)

                ' Read regular morphs (dense, per-vertex int16 * multiplier deltas)
                Dim result As New TriHeadFile With {
                    .NumVertices = numVertices,
                    .NumTriangles = numTriangles,
                    .NumMorphs = numMorphs,
                    .BaseVertices = baseVerts
                }

                For i = 0 To CInt(numMorphs) - 1
                    Dim nameLen = br.ReadUInt32()
                    Dim morphName = ""
                    If nameLen > 0 Then
                        Dim nameBytes = br.ReadBytes(CInt(nameLen))
                        morphName = Encoding.ASCII.GetString(nameBytes).TrimEnd(ChrW(0))
                    End If

                    Dim multiplier = br.ReadSingle()

                    Dim verts(CInt(numVertices) - 1) As Vector3
                    For j = 0 To CInt(numVertices) - 1
                        Dim x = br.ReadInt16()
                        Dim y = br.ReadInt16()
                        Dim z = br.ReadInt16()
                        verts(j) = New Vector3(CSng(x) * multiplier, CSng(y) * multiplier, CSng(z) * multiplier)
                    Next

                    result.Morphs.Add(New TriHeadMorph With {
                        .Name = morphName,
                        .Multiplier = multiplier,
                        .Vertices = verts,
                        .IsModMorph = False
                    })
                Next

                ' Read mod-morphs (sparse, per-region). Each references vertex indices that look up absolute
                ' positions in modVertsPool sequentially. Convert to deltas (abs - base) so the morph engine
                ' can treat them uniformly with regular morphs.
                Dim modVertsIndex As Integer = 0
                For i = 0 To CInt(numModifiers) - 1
                    Dim nameLen = br.ReadUInt32()
                    Dim morphName = ""
                    If nameLen > 0 Then
                        Dim nameBytes = br.ReadBytes(CInt(nameLen))
                        morphName = Encoding.ASCII.GetString(nameBytes).TrimEnd(ChrW(0))
                    End If

                    Dim blockLength = br.ReadUInt32()
                    Dim affectedIndices(CInt(blockLength) - 1) As UInteger
                    For k = 0 To CInt(blockLength) - 1
                        affectedIndices(k) = br.ReadUInt32()
                    Next

                    ' Build a dense delta array (same shape as regular morphs) so downstream code is uniform.
                    ' Non-affected vertices get zero delta; affected vertices get (absolute - base).
                    Dim deltas(CInt(numVertices) - 1) As Vector3
                    For k = 0 To CInt(blockLength) - 1
                        Dim vertIdx = CInt(affectedIndices(k))
                        If modVertsIndex >= modVertsPool.Length Then
                            ' Pool underflow: the mod-vert pool (numModVertices) is exhausted before this
                            ' modifier's affected-index list is consumed. Surfaces a corrupt/misparsed
                            ' FRTRI003 instead of silently yielding a partial (wrong) mod-morph.
                            Dim capturedName = morphName
                            Dim capturedMod = i
                            Dim capturedK = k
                            Dim capturedBlock = CInt(blockLength)
                            Logger.LogLazy(Function() $"[TRI] FRTRI003 mod-morph pool underflow: modifier #{capturedMod} '{capturedName}' ran out of modVertsPool ({modVertsPool.Length} verts) at affected-index {capturedK}/{capturedBlock}; remaining deltas dropped.")
                            Exit For
                        End If
                        If vertIdx >= 0 AndAlso vertIdx < numVertices Then
                            deltas(vertIdx) = modVertsPool(modVertsIndex) - baseVerts(vertIdx)
                        End If
                        modVertsIndex += 1
                    Next

                    result.Morphs.Add(New TriHeadMorph With {
                        .Name = morphName,
                        .Multiplier = 1.0F,
                        .Vertices = deltas,
                        .IsModMorph = True
                    })
                Next

                Return result
            End Using
        End Using
    End Function

End Module

''' <summary>
''' Writer for TRI binary files (PIRT header format).
''' </summary>
Public Module TriFileWriter

    ''' <summary>Write a TriFile to disk in PIRT binary format.</summary>
    Public Function WriteTriToFile(tri As TriFile, fileName As String) As Boolean
        If tri Is Nothing OrElse String.IsNullOrWhiteSpace(fileName) Then Return False

        Try
            ' Una SOLA implementación del formato, en WriteTriToBytes: tener el header y las dos
            ' WriteSection duplicados acá hacía que los tests validaran una copia distinta de la que
            ' corre en el build. Se serializa a memoria y recién ahí se toca el disco, así que un
            ' throw de los límites del formato ya no deja un .tri truncado (FileMode.Create trunca
            ' antes de escribir).
            Dim payload = BuildTriBytes(tri)
            File.WriteAllBytes(fileName, payload)
        Catch ex As Exception
            ' Un .tri que no se escribio deja al NIF con un BODYTRI apuntando a nada. El caller
            ' DEBE propagar el False; aca solo queda el rastro de la causa real, que si no se
            ' pierde entera: nombre de shape/morph >255 chars, o Offsets.Count >65535.
            Logger.LogLazy(Function() $"[TRI] Write failed for '{fileName}': {ex.GetType().Name}: {ex.Message}")
            Return False
        End Try

        Return True
    End Function

    ''' <summary>Write a TriFile to a byte array in PIRT binary format.</summary>
    Public Function WriteTriToBytes(tri As TriFile) As Byte()
        If tri Is Nothing Then Return Nothing

        Try
            Return BuildTriBytes(tri)
        Catch ex As Exception
            ' Mismo contrato que WriteTriToFile: los limites del formato (nombre >255 bytes,
            ' Offsets.Count >65535) no deben escaparse crudos al consumidor.
            Logger.LogLazy(Function() $"[TRI] WriteTriToBytes failed: {ex.GetType().Name}: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>Serializa el PIRT completo a memoria. ÚNICA implementación del formato: la usan tanto
    ''' <see cref="WriteTriToFile"/> como <see cref="WriteTriToBytes"/>.</summary>
    Private Function BuildTriBytes(tri As TriFile) As Byte()
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms, Encoding.Latin1, leaveOpen:=True)
                bw.Write(Encoding.Latin1.GetBytes("PIRT"))
                WriteSection(bw, tri, TriMorphType.Position)
                WriteSection(bw, tri, TriMorphType.UV)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Sub WriteSection(bw As BinaryWriter, tri As TriFile, sectionType As TriMorphType)
        ' Count shapes that have morphs of this type.
        ' El orden de shapes replica TriFile::Write de BSOS, que itera un std::map<std::string>.
        ' Sin este OrderBy el orden dependia del orden de insercion de un Dictionary, que no esta
        ' garantizado por contrato.
        ' ⚠️ Residuo conocido: MSVC compara std::string con char SIGNED (bytes >= 0x80 ordenan ANTES
        ' del ASCII) y StringComparer.Ordinal compara unidades UTF-16 (ordenan DESPUES). Un nombre de
        ' shape con bytes altos se ordena distinto que en BSOS. Los BYTES emitidos si coinciden desde
        ' que la escritura pasa por Latin1.
        Dim shapeNames = tri.ShapeMorphs.Keys.
            Where(Function(sn) tri.GetMorphsForShape(sn).Any(Function(m) m.MorphType = sectionType)).
            OrderBy(Function(sn) sn, StringComparer.Ordinal).
            ToList()
        bw.Write(CUShort(shapeNames.Count))

        For Each shapeName In shapeNames
            ' Latin1, NO ASCII: son los MISMOS bytes que NiflySharp decodifico del NIF
            ' (NiString.cs:102/135 para la string table de FO4/SSE; NiStringRef.cs:53-62 en el
            ' formato legacy), o sea los mismos que escribe BSOS copiando el std::string crudo.
            ' Con ASCII cualquier byte >= 0x80 salia como '?'.
            ' El prefijo de largo va en BYTES, no en chars: un par suplente son 2 chars y 1 byte,
            ' y el desfasaje corrompia el archivo entero.
            Dim shapeBytes = Encoding.Latin1.GetBytes(shapeName)
            If shapeBytes.Length > 255 Then Throw New InvalidOperationException($"Shape name exceeds the 255-byte TRI format limit: '{shapeName}'")
            bw.Write(CByte(shapeBytes.Length))
            If shapeBytes.Length > 0 Then bw.Write(shapeBytes)

            ' Orden de morphs = orden de insercion (= orden de sliders del .osp), igual que el
            ' std::vector<MorphDataPtr> de BSOS. NO alfabetico: ordenar por nombre desviaba del
            ' layout canonico e impedia el diff binario contra BodySlide.
            Dim morphs = tri.GetMorphsForShape(shapeName).
                Where(Function(m) m.MorphType = sectionType).
                ToList()
            bw.Write(CUShort(morphs.Count))

            For Each morph In morphs
                Dim morphName = If(morph.Name, "")
                Dim morphBytes = Encoding.Latin1.GetBytes(morphName)
                If morphBytes.Length > 255 Then Throw New InvalidOperationException($"Morph name exceeds the 255-byte TRI format limit: '{morphName}'")
                bw.Write(CByte(morphBytes.Length))
                If morphBytes.Length > 0 Then bw.Write(morphBytes)

                ' Compute quantization multiplier: max absolute component / 0x7FFF
                Dim maxAbs As Single = 0.0F
                For Each v In morph.Offsets.Values
                    If Math.Abs(v.X) > maxAbs Then maxAbs = Math.Abs(v.X)
                    If Math.Abs(v.Y) > maxAbs Then maxAbs = Math.Abs(v.Y)
                    If sectionType = TriMorphType.Position Then
                        If Math.Abs(v.Z) > maxAbs Then maxAbs = Math.Abs(v.Z)
                    End If
                Next
                Dim mult = maxAbs / CSng(&H7FFF)
                bw.Write(mult)
                bw.Write(CUShort(morph.Offsets.Count))

                For Each kvp In morph.Offsets.OrderBy(Function(p) p.Key)
                    bw.Write(kvp.Key)
                    If mult <> 0.0F Then
                        bw.Write(CType(Fix(kvp.Value.X / mult), Short))
                        bw.Write(CType(Fix(kvp.Value.Y / mult), Short))
                        If sectionType = TriMorphType.Position Then
                            bw.Write(CType(Fix(kvp.Value.Z / mult), Short))
                        End If
                    Else
                        bw.Write(CShort(0))
                        bw.Write(CShort(0))
                        If sectionType = TriMorphType.Position Then bw.Write(CShort(0))
                    End If
                Next
            Next
        Next
    End Sub

End Module
