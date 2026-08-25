' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' Header y secciones del formato Havok Packfile binario. Entry point de todo el parseo
' HKX: SkeletonClothOverlayHelper, HkxPoseImportHelper y los Hcl*/Hkx* parten de acá.
'
' Variantes soportadas (FileVersion / PointerSize): 11/8 = Fallout 4 (hk_2014),
' 8/8 = Skyrim SE (hk_2010), 8/4 = Skyrim LE. Solo little-endian (Endianness=1).
'
' ALCANCE: solo el ENVOLTORIO — cabecera, secciones y los tres tipos de fixup. Ni un campo de
' ninguna clase Havok se lee aca: eso empieza en `HkxObjectGraphParser` (el sustrato) y lo resuelve
' el codigo generado a partir de la tabla de la reflexion.
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
    ' ⛔ POR QUE ESTE ARCHIVO NO USA EL LECTOR GENERADO.
    ' La reflexion SI declara el envoltorio — `hkPackfileHeader{magic[2],0 . userTag,8 . fileVersion,C
    ' . layoutRules[4],10 . numSections,14 . contentsSectionIndex,18 . contentsSectionOffset,1C .
    ' contentsClassNameSectionIndex,20 . contentsClassNameSectionOffset,24 . contentsVersion[16],28 .
    ' flags,38 . maxpredicate,3C . predicateArraySizePlusPadding,3E}` y
    ' `hkPackfileSectionHeader{sectionTag[19],0 . nullByte,13 . absoluteDataStart,14 .
    ' localFixupsOffset,18 . globalFixupsOffset,1C . virtualFixupsOffset,20 . exportsOffset,24 .
    ' importsOffset,28 . endOffset,2C . pad[4],30}` — y los campos de abajo se leen EN ESE ORDEN,
    ' secuencialmente, sin un solo offset escrito a mano.
    ' Lo que no se puede es leerlos CON el lector generado: `Hk_*` necesita un
    ' `HkxObjectGraph_Class`, que se construye a partir de las secciones y los fixups que este
    ' archivo produce. Es el arranque: la cabecera se lee antes de que exista el grafo que la leeria.
    ''' <summary>El envoltorio no declara `objectSize`, asi que su tamano lo deduce la tabla
    ''' del ultimo miembro declarado: FO4 `predicateArraySizePlusPadding` @0x3E uint16 y SSE
    ''' `pad` @0x3C int32 terminan los dos en 0x40.</summary>
    Private Shared ReadOnly HeaderFixedSize As Integer = Havok.Canon.HavokLayout.FO4.SizeOfClass("hkPackfileHeader")
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

        ' ⛔ EL ORDEN DEL ARRANQUE. 1) el formato sale de dos campos que las dos tablas declaran en
        ' el mismo offset; 2) con el formato ya se puede anclar un grafo en el byte 0; 3) desde ahi
        ' `Hk_HkPackfileHeader` lee la cabecera como cualquier otra clase declarada.
        result.Formato = FormatoDeclarado(bytes)
        result.Grafo0 = New HkxObjectGraph_Class(bytes, result.Formato, PointerSizeDe(bytes))
        result.Header = New Havok.Canon.Typed.Hk_HkPackfileHeader(result.Grafo0, 0)
        ValidarCabecera(result, bytes.Length)
        ReadSections(result, bytes, bytes.Length)

        ParseClassNames(result)
        ParseFixups(result)
        ResolveRootObject(result)

        Return result
    End Function

    ''' <summary>
    ''' ⛔ EL OFFSET DE UN CAMPO DEL ENVOLTORIO, DE LA TABLA — NO CONTANDO BYTES.
    ''' Las dos tablas declaran `hkPackfileHeader` y `hkPackfileSectionHeader` con los MISMOS
    ''' offsets en todo lo que el arranque necesita, asi que se puede usar cualquiera antes de
    ''' saber de que juego es el archivo — que es justo lo que la cabecera viene a decir.
    ''' </summary>
    Private Shared Function Off(clase As String, campo As String) As Integer
        Return Havok.Canon.HavokLayout.FO4.RequireOffset(clase, campo)
    End Function

    ''' <summary>
    ''' ⛔⛔ EL ENVOLTORIO SE LEE POR OFFSET DECLARADO, NO EN SECUENCIA.
    ''' <para>Esto era un inicializador donde cada `reader.ReadX()` avanzaba el stream, o sea que
    ''' el ORDEN DE LAS LINEAS era el formato. Un campo que nadie usaba no se podia borrar: sacar
    ''' `userTag` corria CUATRO BYTES todo lo que venia despues y el cloth entero dejaba de
    ''' resolver, en silencio. Leyendo por offset, un campo que no se usa simplemente no se lee.</para>
    ''' </summary>
    ''' <summary>El ancho de puntero, que es `layoutRules[0]`. Se lee antes de elegir tabla.</summary>
    Private Shared Function PointerSizeDe(bytes As Byte()) As Integer
        Return bytes(Off("hkPackfileHeader", "layoutRules"))
    End Function

    ''' <summary>
    ''' ⛔⛔ LOS DOS UNICOS CAMPOS QUE SE LEEN ANTES DE SABER QUE TABLA USAR, Y SE COMPRUEBA.
    ''' <para>Para elegir entre la tabla de FO4 y la de SSE hace falta `fileVersion` y
    ''' `layoutRules[0]` — o sea, hay que leer dos campos sin haber elegido todavia. Se puede
    ''' porque las DOS tablas los declaran en el MISMO offset, y eso no se afirma en un
    ''' comentario: se VERIFICA aca. Si algun dia dejan de coincidir, esto tira y se ve.</para>
    ''' </summary>
    Private Shared Function FormatoDeclarado(bytes As Byte()) As HkxPackfileFormat_Enum
        Const C As String = "hkPackfileHeader"
        For Each campo In {"magic", "fileVersion", "layoutRules"}
            Dim a = Havok.Canon.HavokLayout.FO4.RequireOffset(C, campo)
            Dim b = Havok.Canon.HavokLayout.SSE.RequireOffset(C, campo)
            If a <> b Then
                Throw New InvalidDataException(
                    $"Las dos tablas declaran {C}.{campo} en offsets distintos (FO4 0x{a:X}, SSE 0x{b:X}); " &
                    "el arranque del packfile asume que coinciden.")
            End If
        Next

        If BitConverter.ToUInt32(bytes, Off(C, "magic")) <> HavokMagic0 OrElse
           BitConverter.ToUInt32(bytes, Off(C, "magic") + 4) <> HavokMagic1 Then
            Throw New InvalidDataException("Unsupported HKX magic. The payload is not a Havok packfile.")
        End If

        Dim fileVersion = BitConverter.ToInt32(bytes, Off(C, "fileVersion"))
        Dim pointerSize = PointerSizeDe(bytes)
        Select Case True
            Case fileVersion = 11 AndAlso pointerSize = 8 : Return HkxPackfileFormat_Enum.Fallout64
            Case fileVersion = 8 AndAlso pointerSize = 8 : Return HkxPackfileFormat_Enum.Skyrim64
            Case fileVersion = 8 AndAlso pointerSize = 4 : Return HkxPackfileFormat_Enum.Skyrim32
            Case Else
                Throw New InvalidDataException($"Unsupported HKX variant: FileVersion={fileVersion}, PointerSize={pointerSize}.")
        End Select
    End Function

    ''' <summary>Lo que el envoltorio EXIGE de la cabecera ya leida, mas lo que se DERIVA de ella
    ''' (el offset absoluto de la tabla de secciones, que no es un campo declarado).</summary>
    Private Shared Sub ValidarCabecera(packfile As HkxPackfile_Class, fileLength As Integer)
        Dim h = packfile.Header
        Dim endianness = h.LayoutRules(1)
        If endianness <> 1 Then Throw New InvalidDataException($"Unsupported HKX endianness flag: {endianness}.")

        ' En FO4 la tabla de secciones arranca DESPUES del arreglo de predicados; en SSE, justo
        ' al terminar la cabecera. `predicateArraySizePlusPadding` es el campo que lo dice, y solo
        ' la tabla de FO4 lo declara — por eso la rama.
        packfile.SectionHeadersAbsoluteOffset =
            If(packfile.Formato = HkxPackfileFormat_Enum.Fallout64 AndAlso h.HasPredicateArraySizePlusPadding,
               HeaderFixedSize + h.PredicateArraySizePlusPadding,
               HeaderFixedSize)

        If h.NumSections <= 0 OrElse h.NumSections > 64 Then
            Throw New InvalidDataException($"Invalid HKX section count: {h.NumSections}.")
        End If
        If packfile.SectionHeadersAbsoluteOffset < HeaderFixedSize OrElse packfile.SectionHeadersAbsoluteOffset >= fileLength Then
            Throw New InvalidDataException($"Invalid HKX section header offset: 0x{packfile.SectionHeadersAbsoluteOffset:X}.")
        End If
    End Sub

    ''' <summary>
    ''' ⛔ CADA ENCABEZADO DE SECCION, POR EL LECTOR GENERADO. El grafo de arranque esta anclado
    ''' en el byte 0, asi que el offset absoluto del encabezado se le pasa tal cual como base.
    ''' <para>Los seis offsets del encabezado son RELATIVOS a `absoluteDataStart` y no se guardan:
    ''' se resuelven a absolutos aca mismo. Guardar las dos formas del mismo dato era tener el
    ''' campo dos veces.</para>
    ''' </summary>
    Private Shared Sub ReadSections(packfile As HkxPackfile_Class, bytes As Byte(), fileLength As Integer)
        Const C As String = "hkPackfileSectionHeader"
        ' ⛔ EL TAMANO DEL ENCABEZADO DE SECCION, DE LA TABLA. Era `0x40 en Fallout, 0x30 si no`
        ' escrito a mano; la reflexion lo dice: FO4 declara `pad,30,int32,,4` (0x40) y SSE termina
        ' en `endOffset,2C` (0x30). La tabla que corresponde la elige el formato del archivo.
        Dim lay = Havok.Canon.HavokLayout.For(packfile.Formato)
        Dim sectionHeaderSize = lay.SizeOfClass(C)
        Dim base_ = packfile.SectionHeadersAbsoluteOffset

        For i = 0 To packfile.Header.NumSections - 1
            Dim o = base_ + (i * sectionHeaderSize)
            If o + sectionHeaderSize > fileLength Then
                Throw New InvalidDataException($"Section header #{i} is truncated.")
            End If

            Dim sh As New Havok.Canon.Typed.Hk_HkPackfileSectionHeader(packfile.Grafo0, o)
            Dim section As New HkxPackfileSection_Class With {
                .Index = i,
                .Name = AsciiFijo(bytes, o + Off(C, "sectionTag"), 19),
                .AbsoluteDataStart = sh.AbsoluteDataStart
            }

            If section.AbsoluteDataStart < 0 OrElse section.AbsoluteDataStart > fileLength Then
                Throw New InvalidDataException($"Section '{section.Name}' has an invalid data start: 0x{section.AbsoluteDataStart:X}.")
            End If

            section.LocalFixupsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, sh.LocalFixupsOffset, fileLength)
            section.GlobalFixupsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, sh.GlobalFixupsOffset, fileLength)
            section.VirtualFixupsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, sh.VirtualFixupsOffset, fileLength)
            section.ExportsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, sh.ExportsOffset, fileLength)
            section.ImportsAbsoluteStart = ResolveRelativeOffset(section.AbsoluteDataStart, sh.ImportsOffset, fileLength)
            section.AbsoluteEnd = ResolveRelativeOffset(section.AbsoluteDataStart, sh.EndOffset, fileLength, allowZero:=True)

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
                .EntryRelativeOffset = entryAbsoluteOffset - section.AbsoluteDataStart,
                .StringRelativeOffset = entryAbsoluteOffset + 5 - section.AbsoluteDataStart,
                .Signature = signature,
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

    ''' <summary>ASCII de largo fijo en un offset ABSOLUTO. Sin stream, para que leer un campo no
    ''' dependa de haber leido los de arriba.</summary>
    Private Shared Function AsciiFijo(bytes As Byte(), offset As Integer, length As Integer) As String
        If offset < 0 OrElse offset + length > bytes.Length Then Return String.Empty
        Dim nul = Array.IndexOf(bytes, CByte(0), offset, length)
        Dim n = If(nul < 0, length, nul - offset)
        Return Encoding.ASCII.GetString(bytes, offset, n)
    End Function
End Class

Public Class HkxPackfile_Class
    Friend Sub New(rawBytes As Byte())
        Me.RawBytes = rawBytes
    End Sub

    ''' <summary>La cabecera, LEIDA POR EL LECTOR GENERADO. No hay clase espejo.</summary>
    Public Property Header As Havok.Canon.Typed.Hk_HkPackfileHeader

    ''' <summary>Que tabla de la reflexion declara este archivo. Se deriva de `fileVersion`
    ''' + `layoutRules[0]`; no es un campo declarado.</summary>
    Public Property Formato As HkxPackfileFormat_Enum

    ''' <summary>Donde empieza la tabla de encabezados de seccion. Se deduce del tamano de
    ''' la cabecera mas el arreglo de predicados; no es un campo declarado.</summary>
    Public Property SectionHeadersAbsoluteOffset As Integer

    ''' <summary>El grafo anclado en el byte 0 con el que se leyo el envoltorio.</summary>
    Friend Property Grafo0 As HkxObjectGraph_Class
    Public ReadOnly Property RawBytes As Byte()
    Public ReadOnly Property Sections As New List(Of HkxPackfileSection_Class)
    Public ReadOnly Property ClassNames As New List(Of HkxClassNameEntry_Class)
    Public ReadOnly Property LocalFixups As New List(Of HkxLocalFixupEntry_Class)
    Public ReadOnly Property GlobalFixups As New List(Of HkxGlobalFixupEntry_Class)
    Public ReadOnly Property VirtualFixups As New List(Of HkxVirtualFixupEntry_Class)
    Public Property RootObject As HkxRootObject_Class

    ''' <summary>`contentsVersion` es un `char[16]` terminado en cero. El String no es un campo:
    ''' es la lectura de ese arreglo, y se arma aca para no armarlo en cada consumidor.</summary>
    Public Function ContentsVersionTexto() As String
        If Not Header.IsValid OrElse Not Header.HasContentsVersion Then Return String.Empty
        Dim sb As New Text.StringBuilder(16)
        For i = 0 To 15
            Dim c = Header.ContentsVersion(i)
            If c = 0 Then Exit For
            sb.Append(ChrW(c))
        Next
        Return sb.ToString()
    End Function

    Public Function GetSection(name As String) As HkxPackfileSection_Class
        Return Sections.FirstOrDefault(Function(pf) pf.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Function GetSection(index As Integer) As HkxPackfileSection_Class
        If index < 0 OrElse index >= Sections.Count Then Return Nothing
        Return Sections(index)
    End Function

    ' Lookup por EntryRelativeOffset Y StringRelativeOffset → la PRIMERA entrada de ClassNames
    ' (en orden de lista) que coincida por cualquiera de los dos campos. Se arma en el primer uso
    ' porque ClassNames se puebla durante el parseo y no cambia después.
    Private _classNameByOffset As Dictionary(Of Integer, HkxClassNameEntry_Class)

    Public Function GetClassName(sectionIndex As Integer, entryRelativeOffset As Integer) As HkxClassNameEntry_Class
        Dim section = GetSection(sectionIndex)
        If IsNothing(section) OrElse Not section.Name.Equals("__classnames__", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        If _classNameByOffset Is Nothing Then
            Dim lookup As New Dictionary(Of Integer, HkxClassNameEntry_Class)
            For Each entry In ClassNames
                ' TryAdd y NO asignación: ante offsets repetidos gana la PRIMERA entrada de la
                ' lista. Cada entrada se registra bajo sus DOS offsets.
                lookup.TryAdd(entry.EntryRelativeOffset, entry)
                lookup.TryAdd(entry.StringRelativeOffset, entry)
            Next
            _classNameByOffset = lookup
        End If

        Dim result As HkxClassNameEntry_Class = Nothing
        If _classNameByOffset.TryGetValue(entryRelativeOffset, result) Then Return result
        Return Nothing
    End Function
End Class

' ⛔⛔ `HkxPackfileHeader_Class` SE BORRO. Repetia, campo por campo, lo que la reflexion
' declara para `hkPackfileHeader`. Hoy `HkxPackfile_Class.Header` ES `Hk_HkPackfileHeader`: el
' mismo lector generado que se usa para todo lo demas. Lo que NO es un campo declarado — el
' formato del archivo y el offset absoluto de la tabla de secciones — vive en `HkxPackfile_Class`.

''' <summary>
''' Una seccion, en la forma en que el resto del arbol la necesita: NADA de esto es un campo
''' declarado. `hkPackfileSectionHeader` da seis offsets RELATIVOS a `absoluteDataStart`; aca
''' viven ya resueltos a absolutos, mas los limites que se deducen del orden de los tramos.
''' <para>Los relativos NO se guardan: leerlos es `Hk_HkPackfileSectionHeader`, y tener la
''' misma magnitud en dos formas era tener el campo dos veces.</para>
''' </summary>
Public Class HkxPackfileSection_Class
    Public Property Index As Integer
    Public Property Name As String
    Public Property AbsoluteDataStart As Integer
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
    Public Property EntryRelativeOffset As Integer
    Public Property StringRelativeOffset As Integer
    Public Property Signature As UInteger
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





