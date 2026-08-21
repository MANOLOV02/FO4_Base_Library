Imports System.IO
Imports System.Text
Imports ICSharpCode.SharpZipLib.Zip.Compression

''' <summary>
''' Reads a single ESP/ESM/ESL plugin file and extracts records of interest.
''' Skips groups whose signature is not in the interest set for performance.
''' </summary>
Public Class PluginReader
    Public Property FileName As String
    Public Property Masters As New List(Of String)
    Public Property IsESM As Boolean
    Public Property IsESL As Boolean
    Public Property IsLocalized As Boolean
    ''' <summary>HEDR Next Object ID — the FormID object counter the engine/CK dispenses from
    ''' when adding new self records to this plugin. Captured from TES4.HEDR, 3rd field.
    ''' Preserve-on-resave semantics: <see cref="SaveNpcEspWriter"/>
    ''' must seed its own dispense pointer with <c>max(disk, computed)</c> to avoid re-issuing
    ''' an ID that CK already consumed between saves.</summary>
    ''' <summary>Campo Version del HEDR, crudo (float Version + u32
    ''' NumRecords + u32 NextObjectID). Decide, junto con el juego y la cantidad de masters, si el archivo
    ''' puede direccionar object ids por debajo de 0x800. 0 si el HEDR no se pudo leer, que es lo mismo que
    ''' "version vieja" y por lo tanto NO permite el rango — el default seguro.</summary>
    Public Property HeaderVersion As Single = 0.0F

    Public Property NextObjectId As UInteger
    ''' <summary>
    ''' Per-file translatable encoding captured from TES4.SNAM &lt;cp:XXXX&gt; at load time.
    ''' Nothing when the plugin's TES4 description has no recognizable tag (default → use global).
    ''' </summary>
    Public Property TranslatableEncoding As Encoding
    ''' <summary>TES4.CNAM author string, captured at load. Lets callers identify plugins authored by a
    ''' particular app (e.g. this app's save flow writes <c>NPC Manager - Auto generated</c>), so the editors
    ''' can list "my records" (new AND override) by source plugin. "" when the plugin has no CNAM.</summary>
    Public Property Author As String = ""
    Public Property Records As New Dictionary(Of UInteger, PluginRecord)

    Private ReadOnly _sigFilter As HashSet(Of String)

    ' Byte-progress plumbing for LoadAllPlugins' rich progress bar. Invokes a SYNCHRONOUS callback with the
    ' ABSOLUTE stream position periodically (throttled in ReadRecord) so the parallel wrapper can turn it into
    ' a byte delta on this reader's own parse thread. A synchronous Action (not IProgress) on purpose: an
    ' IProgress built off the UI thread has no SynchronizationContext and posts the handler to the thread pool,
    ' which would run the wrapper's delta logic concurrently/out-of-order and race its per-reader lastPos. Both
    ' fields are per-reader state, set only at the start of Load and read only on this reader's own parse
    ' thread — no sharing across readers.
    Private _byteProgress As Action(Of Long)
    Private _recordCount As Integer

    Public Sub New(Optional sigFilter As HashSet(Of String) = Nothing)
        _sigFilter = If(sigFilter, SIGS_OF_INTEREST)
    End Sub

    ''' <summary>Load a plugin file, reading only records whose group signature is in the filter set.
    ''' <paramref name="byteProgress"/> (optional) is invoked SYNCHRONOUSLY with the absolute stream position
    ''' periodically (throttled) so a caller can drive a byte-weighted progress bar.</summary>
    Public Sub Load(filePath As String, Optional byteProgress As Action(Of Long) = Nothing)
        FileName = Path.GetFileName(filePath)
        _byteProgress = byteProgress
        _recordCount = 0
        ' Buffer de 64 KB y lectura SECUENCIAL. El archivo se recorre de principio a fin una sola
        ' vez y el master del juego pesa 316 MB; con el buffer por defecto (4 KB) eso son ~48.600
        ' llamadas al sistema, y con 64 KB quedan unas pocas miles.
        '
        ' ⛔ NO es "cuanto mas grande mejor", y esta MEDIDO. El lector SALTEA la mayor parte del
        ' archivo (los grupos que no estan en el filtro de firmas, y los hijos de CELL/WRLD), asi
        ' que un buffer grande convierte cada salto en una lectura desperdiciada: con 1 MB se leen
        ' 318 MB de un master de 331, contra 203 MB con 4 KB — 114 MB de mas por archivo. Y un
        ' buffer de 1 MB va al monton de objetos grandes, uno por plugin, en paralelo.
        ' Medido sobre el orden de carga real (mediana de 3 rondas, fase de carga de plugins):
        '     4 KB (defecto) ... el del "antes"      64 KB ... 1532 ms      1 MB ... 1590 ms
        ' 64 KB gana, no toca el monton de objetos grandes, y lee ~70 MB menos por master.
        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                   BufferSize:=64 * 1024, options:=FileOptions.SequentialScan)
            Using br As New BinaryReader(fs, Encoding.UTF8, True)
                ReadTES4(br)
                While fs.Position < fs.Length
                    ReadTopLevelGroup(br, fs)
                End While
            End Using
        End Using
    End Sub

    ''' <summary>Load a plugin from byte array.</summary>
    Public Sub Load(data As Byte(), name As String)
        FileName = name
        Using ms As New MemoryStream(data, False)
            Using br As New BinaryReader(ms, Encoding.UTF8, True)
                ReadTES4(br)
                While ms.Position < ms.Length
                    ReadTopLevelGroup(br, ms)
                End While
            End Using
        End Using
    End Sub

    ''' <summary>Cheap header-only load: reads just the TES4 record — master list
    ''' (<see cref="Masters"/>), ESM/ESL/localized flags, and the &lt;cp:XXXX&gt; translatable
    ''' encoding tag — then stops before the first GRUP. Leaves <see cref="Records"/> empty.
    ''' Use when the caller only needs a plugin's masters / header flags (e.g. dependency
    ''' validation in a preflight) without paying for a full parse: opening the file and reading
    ''' its first record is a single sub-1 KB sequential read.</summary>
    Public Sub LoadHeaderOnly(filePath As String)
        FileName = Path.GetFileName(filePath)
        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
            Using br As New BinaryReader(fs, Encoding.UTF8, True)
                ReadTES4(br)
            End Using
        End Using
    End Sub

    Private Sub ReadTES4(br As BinaryReader)
        Dim header = RecordHeader.Read(br)
        If header.Signature <> "TES4" Then Throw New InvalidDataException("Not a valid plugin file: missing TES4 header")

        IsESM = (header.Flags And FLAG_ESM) <> 0
        ' Slot LIGHT o full. La ley COMPLETA (0x200 OR (.esl AND NOT IsUpdate)) vive en UN solo
        ' lugar, `PluginManager.IsLightSlot`, con sus dos precondiciones de VR. Acá no se
        ' re-escribe: una copia local fue exactamente lo que dejo a `LoadOrderActivator` clasificando con
        ' una ley distinta del resto del arbol.
        ' Si esto se equivoca, un .esl se lleva un slot FULL y corre el high byte de ese plugin Y de todos
        ' los full que vengan despues: cada FormID que poseen resuelve al archivo equivocado.
        IsESL = PluginManager.IsLightSlot(Config_App.Current.DataPath, FileName, header.Flags)
        IsLocalized = (header.Flags And FLAG_LOCALIZED) <> 0

        Dim endPos = br.BaseStream.Position + header.DataSize
        Dim data = ReadRecordData(br, header)

        ' Two passes over TES4 subrecords:
        '  1) SNAM <cp:XXXX> — capture per-file translatable encoding BEFORE decoding MAST,
        '     so master filenames (technically translatable) are read with the right cp.
        '  2) MAST — accumulate master plugin filenames using the resolved encoding.
        ' Parsing SNAM before MAST guarantees the correct codepage is already known when master
        ' filenames get decoded, though in practice MAST content is ASCII-safe regardless of
        ' encoding (filesystem-friendly names).
        Dim tes4Subrecords = ParseSubrecords(data)

        For Each subrecord In tes4Subrecords
            If subrecord.Signature <> "SNAM" Then Continue For
            Dim snamText = subrecord.AsString  ' global decode is fine here — tag uses ASCII
            Dim parsed = PluginEncodingSettings.ParseSnamCpTag(snamText)
            If parsed IsNot Nothing Then
                TranslatableEncoding = parsed
                Exit For
            End If
        Next

        For Each subrecord In tes4Subrecords
            If subrecord.Signature <> "MAST" Then Continue For
            ' MAST is a non-translatable string field → decoded with the General codepage.
            Dim master = subrecord.AsStringGeneral
            If master <> "" Then Masters.Add(master)
        Next

        ' HEDR struct layout: float Version + u32 NumRecords + u32 NextObjectID.
        ' We only need NextObjectID for preserve-on-resave semantics.
        For Each subrecord In tes4Subrecords
            If subrecord.Signature <> "HEDR" Then Continue For
            If subrecord.Data IsNot Nothing AndAlso subrecord.Data.Length >= 12 Then
                ' La VERSION no es decorativa: el motor la compara contra 1.709 (SSE) / 1.0 (FO4)
                ' para decidir si este archivo puede usar object ids POR DEBAJO de 0x800. Antes se
                ' descartaba y el lector no podia aplicar esa rama. Se guarda cruda; la ley vive en
                ' PluginManager.AllowsHardcodedRange.
                HeaderVersion = BitConverter.ToSingle(subrecord.Data, 0)
                NextObjectId = BitConverter.ToUInt32(subrecord.Data, 8)
            End If
            Exit For
        Next

        ' CNAM (author, ZSTRING) — ASCII app marker (e.g. "NPC Manager - Auto generated"); general decode is fine.
        For Each subrecord In tes4Subrecords
            If subrecord.Signature <> "CNAM" Then Continue For
            Author = subrecord.AsStringGeneral
            Exit For
        Next

        br.BaseStream.Position = endPos
    End Sub

    Private Sub ReadTopLevelGroup(br As BinaryReader, stream As Stream)
        Dim startPos = stream.Position
        If stream.Length - startPos < GROUP_HEADER_SIZE Then
            stream.Position = stream.Length
            Return
        End If

        Dim groupHeader2 = GroupHeader.Read(br)
        If groupHeader2.Signature <> "GRUP" Then
            stream.Position = stream.Length
            Return
        End If

        Dim groupEndPos = startPos + groupHeader2.GroupSize
        If groupEndPos > stream.Length Then groupEndPos = stream.Length

        If groupHeader2.GroupType = 0 Then
            Dim labelSig = groupHeader2.LabelAsSignature
            If Not _sigFilter.Contains(labelSig) Then
                stream.Position = groupEndPos
                Return
            End If
        End If

        While stream.Position < groupEndPos - RECORD_HEADER_SIZE
            ' Se compara la CLAVE de 4 bytes contra la de "GRUP" en vez de decodificar una cadena
            ' para tirarla: esta linea corre una vez por CADA record del archivo, incluidos los que
            ' despues se saltean.
            Dim peekClave = PluginSignatures.LeerClave(br)
            stream.Position -= 4

            If peekClave = PluginSignatures.ClaveGRUP Then
                ReadTopLevelGroup(br, stream)
            Else
                ReadRecord(br, stream)
            End If
        End While

        stream.Position = groupEndPos
    End Sub

    Private Sub ReadRecord(br As BinaryReader, stream As Stream)
        Dim header = RecordHeader.Read(br)
        Dim dataEndPos = stream.Position + header.DataSize

        ' Throttle byte-progress: report the absolute position once per 1024 records (the &H3FF mask) so
        ' the parallel wrapper can convert it to a delta without flooding the IProgress with one event per
        ' record. The 1024 is the only magic number allowed here (progress throttle).
        _recordCount += 1
        If _byteProgress IsNot Nothing AndAlso (_recordCount And &H3FF) = 0 Then
            _byteProgress(stream.Position)
        End If

        If header.Signature = "TES4" Then
            stream.Position = dataEndPos
            Return
        End If

        ' Uniform record-level filter: a record whose signature is not in the filter is skip-seeked here
        ' (no decompress, no ParseSubrecords, no PluginRecord alloc). Top-level groups are still pre-filtered
        ' in ReadTopLevelGroup; this additionally drops UNwanted records nested inside KEPT groups — the cell
        ' children (REFR/NAVM/LAND/PGRE/PHZD) under CELL/WRLD that nothing consumes. ACHR is in the filter so
        ' it survives (see SIGS_NPC_RENDERING). TES4 is handled above.
        If Not _sigFilter.Contains(header.Signature) Then
            stream.Position = dataEndPos
            Return
        End If

        Dim data = ReadRecordData(br, header)
        Dim record As New PluginRecord With {
            .Header = header,
            .SourcePluginName = FileName,
            .SourcePluginIsLocalized = IsLocalized,
            .SourcePluginTranslatableEncoding = TranslatableEncoding
        }

        record.Subrecords.AddRange(ParseSubrecords(data))

        Records(header.FormID) = record
        stream.Position = dataEndPos
    End Sub

    Private Shared Function ParseSubrecords(data As Byte()) As List(Of SubrecordData)
        Dim result As New List(Of SubrecordData)
        If data Is Nothing OrElse data.Length < SUBRECORD_HEADER_SIZE Then Return result

        Using ms As New MemoryStream(data, False)
            Using sr As New BinaryReader(ms, Encoding.UTF8, True)
                Dim extendedSize As Integer = -1

                While ms.Position <= ms.Length - SUBRECORD_HEADER_SIZE
                    Dim subSig = PluginSignatures.Leer(sr)
                    Dim subSize = CInt(sr.ReadUInt16())

                    If subSig = "XXXX" Then
                        If ms.Position + subSize > ms.Length Then Exit While
                        Dim xxxxData = sr.ReadBytes(subSize)
                        If xxxxData.Length >= 4 Then
                            extendedSize = CInt(BitConverter.ToUInt32(xxxxData, 0))
                        Else
                            extendedSize = -1
                        End If
                        Continue While
                    End If

                    Dim actualSize = If(extendedSize >= 0, extendedSize, subSize)
                    extendedSize = -1

                    If actualSize < 0 OrElse ms.Position + actualSize > ms.Length Then Exit While

                    result.Add(New SubrecordData With {
                        .Signature = subSig,
                        .Data = sr.ReadBytes(actualSize)
                    })
                End While
            End Using
        End Using

        Return result
    End Function

    ''' <summary>Read record data, handling ZLIB compression if flagged.</summary>
    Private Shared Function ReadRecordData(br As BinaryReader, header As RecordHeader) As Byte()
        If Not header.IsCompressed Then
            Return br.ReadBytes(CInt(header.DataSize))
        End If

        Dim uncompressedSize = br.ReadUInt32()
        Dim compressedSize = CInt(header.DataSize) - 4
        Dim compressedData = br.ReadBytes(compressedSize)

        Dim output(CInt(uncompressedSize) - 1) As Byte
        Dim inflater As New Inflater()
        inflater.SetInput(compressedData)
        ' Inflater.Inflate may not fill the buffer in one call — loop until the full
        ' uncompressedSize is produced (or the stream finishes / stalls). A short inflate
        ' means the record's ZLIB payload is corrupt or truncated.
        Dim total As Integer = 0
        While total < output.Length AndAlso Not inflater.IsFinished
            Dim n = inflater.Inflate(output, total, output.Length - total)
            If n = 0 Then Exit While
            total += n
        End While
        If total <> output.Length Then
            Throw New InvalidDataException(
                $"Short inflate of compressed record: expected {output.Length} bytes, got {total} (corrupt or truncated data)")
        End If
        Return output
    End Function
End Class