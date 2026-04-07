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
    Public Property Records As New Dictionary(Of UInteger, PluginRecord)

    Private ReadOnly _sigFilter As HashSet(Of String)

    Public Sub New(Optional sigFilter As HashSet(Of String) = Nothing)
        _sigFilter = If(sigFilter, SIGS_OF_INTEREST)
    End Sub

    ''' <summary>Load a plugin file, reading only records whose group signature is in the filter set.</summary>
    Public Sub Load(filePath As String)
        FileName = Path.GetFileName(filePath)
        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
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

    Private Sub ReadTES4(br As BinaryReader)
        Dim header = RecordHeader.Read(br)
        If header.Signature <> "TES4" Then Throw New InvalidDataException("Not a valid plugin file: missing TES4 header")

        IsESM = (header.Flags And FLAG_ESM) <> 0
        IsESL = (header.Flags And FLAG_ESL) <> 0
        IsLocalized = (header.Flags And FLAG_LOCALIZED) <> 0

        Dim endPos = br.BaseStream.Position + header.DataSize
        Dim data = ReadRecordData(br, header)

        For Each subrecord In ParseSubrecords(data)
            If subrecord.Signature <> "MAST" Then Continue For
            Dim master = subrecord.AsString
            If master <> "" Then Masters.Add(master)
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
            Dim peekSig = Encoding.ASCII.GetString(br.ReadBytes(4))
            stream.Position -= 4

            If peekSig = "GRUP" Then
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

        If header.Signature = "TES4" Then
            stream.Position = dataEndPos
            Return
        End If

        Dim data = ReadRecordData(br, header)
        Dim record As New PluginRecord With {
            .Header = header,
            .SourcePluginName = FileName,
            .SourcePluginIsLocalized = IsLocalized
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
                    Dim subSig = Encoding.ASCII.GetString(sr.ReadBytes(4))
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
        inflater.Inflate(output)
        Return output
    End Function
End Class