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
    ''' <summary>Shape name -> list of morph entries. Case-sensitive keys.</summary>
    Public ReadOnly Property ShapeMorphs As New Dictionary(Of String, List(Of TriMorphEntry))(StringComparer.Ordinal)

    ''' <summary>Add a morph entry for a shape (deduplicates by name).</summary>
    Public Sub AddMorph(shapeName As String, entry As TriMorphEntry)
        Dim list As List(Of TriMorphEntry) = Nothing
        If Not ShapeMorphs.TryGetValue(shapeName, list) Then
            list = New List(Of TriMorphEntry)()
            ShapeMorphs(shapeName) = list
        End If
        If Not list.Exists(Function(e) e.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)) Then
            list.Add(entry)
        End If
    End Sub

    ''' <summary>Get all morph entries for a shape, or empty list.</summary>
    Public Function GetMorphsForShape(shapeName As String) As List(Of TriMorphEntry)
        Dim list As List(Of TriMorphEntry) = Nothing
        If ShapeMorphs.TryGetValue(shapeName, list) Then Return list
        Return New List(Of TriMorphEntry)()
    End Function

    ''' <summary>Get a specific morph entry by shape and morph name, or Nothing.</summary>
    Public Function GetMorph(shapeName As String, morphName As String) As TriMorphEntry
        Return GetMorphsForShape(shapeName).Find(
            Function(e) e.Name.Equals(morphName, StringComparison.OrdinalIgnoreCase))
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
                ValidateHeader(br)

                ' Position morph section
                ReadSection(br, ms, tri, TriMorphType.Position)

                ' UV morph section
                ReadSection(br, ms, tri, TriMorphType.UV)
            End Using
        End Using

        Return tri
    End Function

    ''' <summary>Parse a TRI file from disk. Throws if file not found or invalid.</summary>
    Public Function ParseTriFromFile(path As String) As TriFile
        If Not File.Exists(path) Then
            Throw New IO.FileNotFoundException("TRI file not found.", path)
        End If
        Return ParseTriFromBytes(File.ReadAllBytes(path))
    End Function

    Private Sub ValidateHeader(br As BinaryReader)
        Dim hdr = br.ReadBytes(4)
        If hdr Is Nothing OrElse hdr.Length <> 4 Then
            Throw New FormatException("Cannot read TRI header.")
        End If
        If Not (hdr(0) = &H50 AndAlso hdr(1) = &H49 AndAlso hdr(2) = &H52 AndAlso hdr(3) = &H54) Then
            Throw New FormatException("Invalid TRI header. Expected 'PIRT'.")
        End If
    End Sub

    Private Sub ReadSection(br As BinaryReader, ms As MemoryStream, tri As TriFile, sectionType As TriMorphType)
        If ms.Position > ms.Length - 2 Then Return

        Dim shapeCount = br.ReadUInt16()

        For i = 0 To shapeCount - 1
            Dim shapeLen = CInt(br.ReadByte())
            Dim shapeName = ReadAsciiString(br, shapeLen)
            Dim morphCount = br.ReadUInt16()

            For m = 0 To morphCount - 1
                Dim morphLen = CInt(br.ReadByte())
                Dim morphName = ReadAsciiString(br, morphLen)
                Dim mult = br.ReadSingle()
                Dim vertexCount = br.ReadUInt16()

                Dim entry As New TriMorphEntry With {
                    .Name = morphName,
                    .MorphType = sectionType
                }

                For k = 0 To vertexCount - 1
                    Dim vid = br.ReadUInt16()

                    If sectionType = TriMorphType.Position Then
                        Dim sx = br.ReadInt16()
                        Dim sy = br.ReadInt16()
                        Dim sz = br.ReadInt16()
                        Dim x = CSng(sx) * mult
                        Dim y = CSng(sy) * mult
                        Dim z = CSng(sz) * mult
                        If Not (x = 0.0F AndAlso y = 0.0F AndAlso z = 0.0F) Then
                            entry.Offsets(vid) = New Vector3(x, y, z)
                        End If
                    Else
                        Dim sx = br.ReadInt16()
                        Dim sy = br.ReadInt16()
                        Dim x = CSng(sx) * mult
                        Dim y = CSng(sy) * mult
                        If Not (x = 0.0F AndAlso y = 0.0F) Then
                            entry.Offsets(vid) = New Vector3(x, y, 0.0F)
                        End If
                    End If
                Next

                If entry.Offsets.Count > 0 Then
                    tri.AddMorph(shapeName, entry)
                End If
            Next
        Next
    End Sub

    Private Function ReadAsciiString(br As BinaryReader, length As Integer) As String
        If length < 0 Then Throw New FormatException("Negative string length in TRI data.")
        If length = 0 Then Return ""
        Dim bytes = br.ReadBytes(length)
        If bytes Is Nothing OrElse bytes.Length <> length Then
            Throw New FormatException("Could not read expected ASCII bytes from TRI data.")
        End If
        Return Encoding.ASCII.GetString(bytes)
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
                        If modVertsIndex >= modVertsPool.Length Then Exit For
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
            Using fs As New FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None)
                Using bw As New BinaryWriter(fs, Encoding.ASCII, leaveOpen:=False)
                    ' Header "PIRT"
                    bw.Write(Encoding.ASCII.GetBytes("PIRT"))

                    ' Position section
                    WriteSection(bw, tri, TriMorphType.Position)

                    ' UV section
                    WriteSection(bw, tri, TriMorphType.UV)
                End Using
            End Using
        Catch
            Return False
        End Try

        Return True
    End Function

    ''' <summary>Write a TriFile to a byte array in PIRT binary format.</summary>
    Public Function WriteTriToBytes(tri As TriFile) As Byte()
        If tri Is Nothing Then Return Nothing

        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms, Encoding.ASCII, leaveOpen:=True)
                bw.Write(Encoding.ASCII.GetBytes("PIRT"))
                WriteSection(bw, tri, TriMorphType.Position)
                WriteSection(bw, tri, TriMorphType.UV)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Sub WriteSection(bw As BinaryWriter, tri As TriFile, sectionType As TriMorphType)
        ' Count shapes that have morphs of this type
        Dim shapeNames = tri.ShapeMorphs.Keys.
            Where(Function(sn) tri.GetMorphsForShape(sn).Any(Function(m) m.MorphType = sectionType)).
            ToList()
        bw.Write(CUShort(shapeNames.Count))

        For Each shapeName In shapeNames
            If shapeName.Length > 255 Then Throw New InvalidOperationException($"Shape name exceeds 255-character TRI format limit: '{shapeName}'")
            bw.Write(CByte(shapeName.Length))
            If shapeName.Length > 0 Then bw.Write(Encoding.ASCII.GetBytes(shapeName))

            Dim morphs = tri.GetMorphsForShape(shapeName).
                Where(Function(m) m.MorphType = sectionType).
                OrderBy(Function(m) m.Name, StringComparer.Ordinal).
                ToList()
            bw.Write(CUShort(morphs.Count))

            For Each morph In morphs
                Dim morphName = If(morph.Name, "")
                If morphName.Length > 255 Then Throw New InvalidOperationException($"Morph name exceeds 255-character TRI format limit: '{morphName}'")
                bw.Write(CByte(morphName.Length))
                If morphName.Length > 0 Then bw.Write(Encoding.ASCII.GetBytes(morphName))

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
