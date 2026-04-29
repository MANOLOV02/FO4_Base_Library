' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' ESTADO: DEBUG / EN REVISIÓN — NO CERRADO
' -----------------------------------------------------------------------------
' Parsing del header y secciones del formato Havok Packfile binario.
' Actualmente usado por SkeletonClothOverlayHelper (ruta activa del render).
'
' Lo que está bien:
'  - Lectura de header, secciones, classnames, local/global/virtual fixups.
'  - Validaciones de bounds, magic y variante de packfile soportada.
'  - Detección de variantes Bethesda: Skyrim LE/SE y Fallout 4 (little-endian).
'
' PENDIENTES CONOCIDOS:
'  - El grafo HCL sigue basado en offsets verificados sobre FO4 64-bit.
'    El soporte 32/64-bit genérico de este parser se usa principalmente para
'    arrays/base object layouts compartidos (root container, skeleton, animation).
'  - Solo soporta little-endian (Endianness=1). Big-endian lanza excepción; correcto.
' =============================================================================

Imports System.IO
Imports System.Linq
Imports System.Text
Imports NiflySharp.Blocks

Public Enum HkxPackfileFormat_Enum
    Unknown = 0
    Skyrim32 = 1
    Skyrim64 = 2
    Fallout64 = 3
End Enum

Public NotInheritable Class HkxPackfileParser_Class
    Private Const HeaderFixedSize As Integer = &H40
    Public Const HavokMagic0 As UInteger = &H57E0E057UI
    Public Const HavokMagic1 As UInteger = &H10C0C010UI

    Public Shared Function Parse(cloth As BSClothExtraData) As HkxPackfile_Class
        If IsNothing(cloth) Then Throw New ArgumentNullException(NameOf(cloth))
        If IsNothing(cloth.BinaryData) Then Throw New InvalidDataException("BSClothExtraData has no BinaryData block.")
        If IsNothing(cloth.BinaryData.Data) OrElse cloth.BinaryData.Data.Count = 0 Then Throw New InvalidDataException("BSClothExtraData has no HKX payload.")
        Return Parse(cloth.BinaryData.Data.ToArray())
    End Function

    Public Shared Function TryParse(cloth As BSClothExtraData, ByRef result As HkxPackfile_Class) As Boolean
        Try
            result = Parse(cloth)
            Return True
        Catch
            result = Nothing
            Return False
        End Try
    End Function

    Public Shared Function Parse(bytes As Byte()) As HkxPackfile_Class
        If IsNothing(bytes) Then Throw New ArgumentNullException(NameOf(bytes))
        If bytes.Length < HeaderFixedSize Then Throw New InvalidDataException("HKX payload is too small to contain a Havok packfile header.")

        Dim result As New HkxPackfile_Class(bytes)

        Using stream As New MemoryStream(bytes, writable:=False)
            Using reader As New BinaryReader(stream, Encoding.ASCII, leaveOpen:=True)
                result.Header = ReadHeader(reader, bytes.Length)
                ReadSections(reader, result, bytes.Length)
            End Using
        End Using

        ParseClassNames(result)
        ParseFixups(result)
        ResolveRootObject(result)

        Return result
    End Function

    Private Shared Function ReadHeader(reader As BinaryReader, fileLength As Integer) As HkxPackfileHeader_Class
        Dim header As New HkxPackfileHeader_Class With {
            .Magic0 = reader.ReadUInt32(),
            .Magic1 = reader.ReadUInt32(),
            .UserTag = reader.ReadUInt32(),
            .FileVersion = reader.ReadInt32(),
            .PointerSize = reader.ReadByte(),
            .Endianness = reader.ReadByte(),
            .ReusePaddingOptimization = reader.ReadByte(),
            .EmptyBaseClassOptimization = reader.ReadByte(),
            .SectionCount = reader.ReadInt32(),
            .ContentsSectionIndex = reader.ReadInt32(),
            .ContentsSectionOffset = reader.ReadInt32(),
            .ContentsClassNameSectionIndex = reader.ReadInt32(),
            .ContentsClassNameSectionOffset = reader.ReadInt32(),
            .ContentsVersion = ReadFixedAscii(reader, 16),
            .Flags = reader.ReadUInt32(),
            .MaxPredicate = reader.ReadByte(),
            .PredicateArraySizePlusPadding = reader.ReadByte(),
            .SectionHeaderRelativeOffset = reader.ReadUInt16(),
            .Reserved = Array.Empty(Of Byte)()
        }
        If header.Magic0 <> HavokMagic0 OrElse header.Magic1 <> HavokMagic1 Then
            Throw New InvalidDataException("Unsupported HKX magic. The payload is not a Havok packfile.")
        End If

        If header.Endianness <> 1 Then
            Throw New InvalidDataException($"Unsupported HKX endianness flag: {header.Endianness}.")
        End If

        Select Case True
            Case header.FileVersion = 11 AndAlso header.PointerSize = 8
                header.PackfileFormat = HkxPackfileFormat_Enum.Fallout64
            Case header.FileVersion = 8 AndAlso header.PointerSize = 8
                header.PackfileFormat = HkxPackfileFormat_Enum.Skyrim64
            Case header.FileVersion = 8 AndAlso header.PointerSize = 4
                header.PackfileFormat = HkxPackfileFormat_Enum.Skyrim32
            Case Else
                Throw New InvalidDataException($"Unsupported HKX variant: FileVersion={header.FileVersion}, PointerSize={header.PointerSize}.")
        End Select

        header.SectionHeadersAbsoluteOffset =
            If(header.PackfileFormat = HkxPackfileFormat_Enum.Fallout64,
               HeaderFixedSize + CInt(header.SectionHeaderRelativeOffset),
               HeaderFixedSize)

        If header.SectionCount <= 0 OrElse header.SectionCount > 64 Then
            Throw New InvalidDataException($"Invalid HKX section count: {header.SectionCount}.")
        End If

        If header.SectionHeadersAbsoluteOffset < HeaderFixedSize OrElse header.SectionHeadersAbsoluteOffset >= fileLength Then
            Throw New InvalidDataException($"Invalid HKX section header offset: 0x{header.SectionHeadersAbsoluteOffset:X}.")
        End If

        Return header
    End Function

    Private Shared Sub ReadSections(reader As BinaryReader, packfile As HkxPackfile_Class, fileLength As Integer)
        reader.BaseStream.Position = packfile.Header.SectionHeadersAbsoluteOffset
        Dim sectionHeaderSize = If(packfile.Header.PackfileFormat = HkxPackfileFormat_Enum.Fallout64, &H40, &H30)
        Dim trailingBytes = Math.Max(0, sectionHeaderSize - &H30)

        For i = 0 To packfile.Header.SectionCount - 1
            If reader.BaseStream.Position + sectionHeaderSize > fileLength Then
                Throw New InvalidDataException($"Section header #{i} is truncated.")
            End If

            Dim section As New HkxPackfileSection_Class With {
                .Index = i,
                .Name = ReadFixedAscii(reader, 19),
                .Tag = reader.ReadByte(),
                .AbsoluteDataStart = reader.ReadInt32(),
                .LocalFixupsRelativeOffset = reader.ReadInt32(),
                .GlobalFixupsRelativeOffset = reader.ReadInt32(),
                .VirtualFixupsRelativeOffset = reader.ReadInt32(),
                .ExportsRelativeOffset = reader.ReadInt32(),
                .ImportsRelativeOffset = reader.ReadInt32(),
                .EndRelativeOffset = reader.ReadInt32(),
                .PaddingMarker = reader.ReadBytes(trailingBytes)
            }

            If section.AbsoluteDataStart < 0 OrElse section.AbsoluteDataStart > fileLength Then
                Throw New InvalidDataException($"Section '{section.Name}' has an invalid data start: 0x{section.AbsoluteDataStart:X}.")
            End If

            section.LocalFixupsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, section.LocalFixupsRelativeOffset, fileLength)
            section.GlobalFixupsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, section.GlobalFixupsRelativeOffset, fileLength)
            section.VirtualFixupsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, section.VirtualFixupsRelativeOffset, fileLength)
            section.ExportsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, section.ExportsRelativeOffset, fileLength)
            section.ImportsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, section.ImportsRelativeOffset, fileLength)
            section.AbsoluteEnd = ResolveRelativeOffset(section.AbsoluteDataStart, section.EndRelativeOffset, fileLength, allowZero:=True)

            If section.AbsoluteEnd < section.AbsoluteDataStart Then
                Throw New InvalidDataException($"Section '{section.Name}' has an end offset before its data start.")
            End If

            section.DataEndAbsolute = FirstExistingBoundary(section.LocalFixupsAbsoluteStart,
                                                            section.GlobalFixupsAbsoluteStart,
                                                            section.VirtualFixupsAbsoluteStart,
                                                            section.ExportsAbsoluteStart,
                                                            section.ImportsAbsoluteStart,
                                                            section.AbsoluteEnd)

            section.LocalFixupsAbsoluteEnd = If(section.LocalFixupsAbsoluteStart >= 0,
                                                FirstExistingBoundary(section.GlobalFixupsAbsoluteStart,
                                                                     section.VirtualFixupsAbsoluteStart,
                                                                     section.ExportsAbsoluteStart,
                                                                     section.ImportsAbsoluteStart,
                                                                     section.AbsoluteEnd),
                                                -1)

            section.GlobalFixupsAbsoluteEnd = If(section.GlobalFixupsAbsoluteStart >= 0,
                                                 FirstExistingBoundary(section.VirtualFixupsAbsoluteStart,
                                                                      section.ExportsAbsoluteStart,
                                                                      section.ImportsAbsoluteStart,
                                                                      section.AbsoluteEnd),
                                                 -1)

            section.VirtualFixupsAbsoluteEnd = If(section.VirtualFixupsAbsoluteStart >= 0,
                                                  FirstExistingBoundary(section.ExportsAbsoluteStart,
                                                                       section.ImportsAbsoluteStart,
                                                                       section.AbsoluteEnd),
                                                  -1)

            ValidateSectionBoundaries(section)
            packfile.Sections.Add(section)
        Next
    End Sub

    Private Shared Sub ParseClassNames(packfile As HkxPackfile_Class)
        Dim section = packfile.GetSection("__classnames__")
        If IsNothing(section) Then Exit Sub
        If section.DataEndAbsolute <= section.AbsoluteDataStart Then Exit Sub

        Dim cursor = section.AbsoluteDataStart
        While cursor + 5 <= section.DataEndAbsolute
            If IsPadding(packfile.RawBytes, cursor, section.DataEndAbsolute) Then Exit While

            Dim entryAbsoluteOffset = cursor
            Dim signature = BitConverter.ToUInt32(packfile.RawBytes, cursor)
            If signature = UInteger.MaxValue Then Exit While
            cursor += 4

            Dim marker = packfile.RawBytes(cursor)
            cursor += 1

            Dim nulIndex = Array.IndexOf(packfile.RawBytes, CByte(0), cursor, section.DataEndAbsolute - cursor)
            If nulIndex < 0 Then Throw New InvalidDataException("Unterminated HKX classname entry.")

            Dim entry As New HkxClassNameEntry_Class With {
                .EntryAbsoluteOffset = entryAbsoluteOffset,
                .EntryRelativeOffset = entryAbsoluteOffset - section.AbsoluteDataStart,
                .StringAbsoluteOffset = entryAbsoluteOffset + 5,
                .StringRelativeOffset = entryAbsoluteOffset + 5 - section.AbsoluteDataStart,
                .Signature = signature,
                .Marker = marker,
                .Name = Encoding.ASCII.GetString(packfile.RawBytes, cursor, nulIndex - cursor)
            }

            packfile.ClassNames.Add(entry)
            cursor = nulIndex + 1
        End While
    End Sub

    Private Shared Sub ParseFixups(packfile As HkxPackfile_Class)
        For Each section In packfile.Sections
            ParseLocalFixups(packfile, section)
            ParseGlobalFixups(packfile, section)
            ParseVirtualFixups(packfile, section)
        Next
    End Sub

    Private Shared Sub ParseLocalFixups(packfile As HkxPackfile_Class, section As HkxPackfileSection_Class)
        If section.LocalFixupsAbsoluteStart < 0 OrElse section.LocalFixupsAbsoluteEnd <= section.LocalFixupsAbsoluteStart Then Exit Sub

        Dim cursor = section.LocalFixupsAbsoluteStart
        While cursor + 8 <= section.LocalFixupsAbsoluteEnd
            Dim sourceOffset = BitConverter.ToInt32(packfile.RawBytes, cursor)
            If sourceOffset = -1 Then Exit While  ' padding sentinel — same convention as Global/Virtual fixups

            packfile.LocalFixups.Add(New HkxLocalFixupEntry_Class With {
                .SectionIndex = section.Index,
                .SourceRelativeOffset = sourceOffset,
                .DestinationRelativeOffset = BitConverter.ToInt32(packfile.RawBytes, cursor + 4)
            })
            cursor += 8
        End While
    End Sub

    Private Shared Sub ParseGlobalFixups(packfile As HkxPackfile_Class, section As HkxPackfileSection_Class)
        If section.GlobalFixupsAbsoluteStart < 0 OrElse section.GlobalFixupsAbsoluteEnd <= section.GlobalFixupsAbsoluteStart Then Exit Sub

        Dim cursor = section.GlobalFixupsAbsoluteStart
        While cursor + 12 <= section.GlobalFixupsAbsoluteEnd
            Dim sourceOffset = BitConverter.ToInt32(packfile.RawBytes, cursor)
            If sourceOffset = -1 Then Exit While

            packfile.GlobalFixups.Add(New HkxGlobalFixupEntry_Class With {
                .SectionIndex = section.Index,
                .SourceRelativeOffset = sourceOffset,
                .TargetSectionIndex = BitConverter.ToInt32(packfile.RawBytes, cursor + 4),
                .TargetRelativeOffset = BitConverter.ToInt32(packfile.RawBytes, cursor + 8)
            })
            cursor += 12
        End While
    End Sub

    Private Shared Sub ParseVirtualFixups(packfile As HkxPackfile_Class, section As HkxPackfileSection_Class)
        If section.VirtualFixupsAbsoluteStart < 0 OrElse section.VirtualFixupsAbsoluteEnd <= section.VirtualFixupsAbsoluteStart Then Exit Sub

        Dim cursor = section.VirtualFixupsAbsoluteStart
        While cursor + 12 <= section.VirtualFixupsAbsoluteEnd
            Dim sourceOffset = BitConverter.ToInt32(packfile.RawBytes, cursor)
            If sourceOffset = -1 Then Exit While

            packfile.VirtualFixups.Add(New HkxVirtualFixupEntry_Class With {
                .SectionIndex = section.Index,
                .ObjectRelativeOffset = sourceOffset,
                .ClassNameSectionIndex = BitConverter.ToInt32(packfile.RawBytes, cursor + 4),
                .ClassNameRelativeOffset = BitConverter.ToInt32(packfile.RawBytes, cursor + 8)
            })
            cursor += 12
        End While
    End Sub

    Private Shared Sub ResolveRootObject(packfile As HkxPackfile_Class)
        Dim header = packfile.Header
        If header.ContentsSectionIndex < 0 OrElse header.ContentsSectionIndex >= packfile.Sections.Count Then Exit Sub
        If header.ContentsClassNameSectionIndex < 0 OrElse header.ContentsClassNameSectionIndex >= packfile.Sections.Count Then Exit Sub

        Dim section = packfile.Sections(header.ContentsSectionIndex)
        Dim classEntry = packfile.GetClassName(header.ContentsClassNameSectionIndex, header.ContentsClassNameSectionOffset)

        packfile.RootObject = New HkxRootObject_Class With {
            .SectionIndex = header.ContentsSectionIndex,
            .RelativeOffset = header.ContentsSectionOffset,
            .AbsoluteOffset = section.AbsoluteDataStart + header.ContentsSectionOffset,
            .ClassNameSectionIndex = header.ContentsClassNameSectionIndex,
            .ClassNameRelativeOffset = header.ContentsClassNameSectionOffset,
            .ClassName = If(classEntry?.Name, String.Empty)
        }
    End Sub

    Private Shared Function ResolveRelativeOffset(dataStartAbsolute As Integer, relativeOffset As Integer, fileLength As Integer, Optional allowZero As Boolean = False) As Integer
        If relativeOffset = 0 AndAlso Not allowZero Then Return -1
        If relativeOffset < 0 Then Throw New InvalidDataException($"Negative HKX section offset: {relativeOffset}.")
        Dim absoluteOffset = dataStartAbsolute + relativeOffset
        If absoluteOffset < dataStartAbsolute OrElse absoluteOffset > fileLength Then
            Throw New InvalidDataException($"HKX section offset points outside the file: 0x{absoluteOffset:X}.")
        End If
        Return absoluteOffset
    End Function

    Private Shared Function FirstExistingBoundary(ParamArray candidates() As Integer) As Integer
        For Each candidate In candidates
            If candidate >= 0 Then Return candidate
        Next
        Return -1
    End Function

    Private Shared Sub ValidateSectionBoundaries(section As HkxPackfileSection_Class)
        If section.DataEndAbsolute < section.AbsoluteDataStart Then
            Throw New InvalidDataException($"Section '{section.Name}' has invalid data bounds.")
        End If

        If section.LocalFixupsAbsoluteStart >= 0 AndAlso section.LocalFixupsAbsoluteEnd < section.LocalFixupsAbsoluteStart Then
            Throw New InvalidDataException($"Section '{section.Name}' has invalid local fixup bounds.")
        End If

        If section.GlobalFixupsAbsoluteStart >= 0 AndAlso section.GlobalFixupsAbsoluteEnd < section.GlobalFixupsAbsoluteStart Then
            Throw New InvalidDataException($"Section '{section.Name}' has invalid global fixup bounds.")
        End If

        If section.VirtualFixupsAbsoluteStart >= 0 AndAlso section.VirtualFixupsAbsoluteEnd < section.VirtualFixupsAbsoluteStart Then
            Throw New InvalidDataException($"Section '{section.Name}' has invalid virtual fixup bounds.")
        End If
    End Sub

    Private Shared Function IsPadding(bytes As Byte(), startOffset As Integer, endOffset As Integer) As Boolean
        For i = startOffset To endOffset - 1
            If bytes(i) <> 0 AndAlso bytes(i) <> &HFF Then Return False
        Next
        Return True
    End Function

    Private Shared Function ReadFixedAscii(reader As BinaryReader, length As Integer) As String
        Dim raw = reader.ReadBytes(length)
        Dim nul = Array.IndexOf(raw, CByte(0))
        If nul < 0 Then nul = raw.Length
        Return Encoding.ASCII.GetString(raw, 0, nul)
    End Function
End Class

Public Class HkxPackfile_Class
    Friend Sub New(rawBytes As Byte())
        Me.RawBytes = rawBytes
    End Sub

    Public Property Header As HkxPackfileHeader_Class
    Public ReadOnly Property RawBytes As Byte()
    Public ReadOnly Property Sections As New List(Of HkxPackfileSection_Class)
    Public ReadOnly Property ClassNames As New List(Of HkxClassNameEntry_Class)
    Public ReadOnly Property LocalFixups As New List(Of HkxLocalFixupEntry_Class)
    Public ReadOnly Property GlobalFixups As New List(Of HkxGlobalFixupEntry_Class)
    Public ReadOnly Property VirtualFixups As New List(Of HkxVirtualFixupEntry_Class)
    Public Property RootObject As HkxRootObject_Class

    Public Function GetSection(name As String) As HkxPackfileSection_Class
        Return Sections.FirstOrDefault(Function(pf) pf.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Function GetSection(index As Integer) As HkxPackfileSection_Class
        If index < 0 OrElse index >= Sections.Count Then Return Nothing
        Return Sections(index)
    End Function

    Public Function GetClassName(sectionIndex As Integer, entryRelativeOffset As Integer) As HkxClassNameEntry_Class
        Dim section = GetSection(sectionIndex)
        If IsNothing(section) OrElse Not section.Name.Equals("__classnames__", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Return ClassNames.FirstOrDefault(Function(pf) pf.EntryRelativeOffset = entryRelativeOffset OrElse pf.StringRelativeOffset = entryRelativeOffset)
    End Function
End Class

Public Class HkxPackfileHeader_Class
    Public Property Magic0 As UInteger
    Public Property Magic1 As UInteger
    Public Property UserTag As UInteger
    Public Property FileVersion As Integer
    Public Property PointerSize As Byte
    Public Property Endianness As Byte
    Public Property ReusePaddingOptimization As Byte
    Public Property EmptyBaseClassOptimization As Byte
    Public Property SectionCount As Integer
    Public Property ContentsSectionIndex As Integer
    Public Property ContentsSectionOffset As Integer
    Public Property ContentsClassNameSectionIndex As Integer
    Public Property ContentsClassNameSectionOffset As Integer
    Public Property ContentsVersion As String
    Public Property Flags As UInteger
    Public Property MaxPredicate As Byte
    Public Property PredicateArraySizePlusPadding As Byte
    Public Property SectionHeaderRelativeOffset As UShort
    Public Property SectionHeadersAbsoluteOffset As Integer
    Public Property PackfileFormat As HkxPackfileFormat_Enum
    Public Property Reserved As Byte()
End Class

Public Class HkxPackfileSection_Class
    Public Property Index As Integer
    Public Property Name As String
    Public Property Tag As Byte
    Public Property AbsoluteDataStart As Integer
    Public Property LocalFixupsRelativeOffset As Integer
    Public Property GlobalFixupsRelativeOffset As Integer
    Public Property VirtualFixupsRelativeOffset As Integer
    Public Property ExportsRelativeOffset As Integer
    Public Property ImportsRelativeOffset As Integer
    Public Property EndRelativeOffset As Integer
    Public Property PaddingMarker As Byte()
    Public Property LocalFixupsAbsoluteStart As Integer = -1
    Public Property GlobalFixupsAbsoluteStart As Integer = -1
    Public Property VirtualFixupsAbsoluteStart As Integer = -1
    Public Property ExportsAbsoluteStart As Integer = -1
    Public Property ImportsAbsoluteStart As Integer = -1
    Public Property AbsoluteEnd As Integer = -1
    Public Property DataEndAbsolute As Integer = -1
    Public Property LocalFixupsAbsoluteEnd As Integer = -1
    Public Property GlobalFixupsAbsoluteEnd As Integer = -1
    Public Property VirtualFixupsAbsoluteEnd As Integer = -1
End Class

Public Class HkxClassNameEntry_Class
    Public Property EntryAbsoluteOffset As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property StringAbsoluteOffset As Integer
    Public Property StringRelativeOffset As Integer
    Public Property Signature As UInteger
    Public Property Marker As Byte
    Public Property Name As String
End Class

Public Class HkxLocalFixupEntry_Class
    Public Property SectionIndex As Integer
    Public Property SourceRelativeOffset As Integer
    Public Property DestinationRelativeOffset As Integer
End Class

Public Class HkxGlobalFixupEntry_Class
    Public Property SectionIndex As Integer
    Public Property SourceRelativeOffset As Integer
    Public Property TargetSectionIndex As Integer
    Public Property TargetRelativeOffset As Integer
End Class

Public Class HkxVirtualFixupEntry_Class
    Public Property SectionIndex As Integer
    Public Property ObjectRelativeOffset As Integer
    Public Property ClassNameSectionIndex As Integer
    Public Property ClassNameRelativeOffset As Integer
End Class

Public Class HkxRootObject_Class
    Public Property SectionIndex As Integer
    Public Property RelativeOffset As Integer
    Public Property AbsoluteOffset As Integer
    Public Property ClassNameSectionIndex As Integer
    Public Property ClassNameRelativeOffset As Integer
    Public Property ClassName As String
End Class





