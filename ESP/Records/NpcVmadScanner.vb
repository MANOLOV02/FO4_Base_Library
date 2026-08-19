Imports System.IO

' ============================================================================
' VMAD (Virtual Machine Adapter) - scanner de posiciones de FormID.
'
' Estrategia: conservar el subrecord VMAD entero como bytes crudos para el round-trip, y emitir una lista
' ordenada de offsets de FormID para que el writer reescriba el byte alto de cada uno cuando el reorden de la
' MAST cambia el indice de master. Es el mismo enfoque que usa xEdit internamente (FindUsedMasters +
' MastersUpdated), sin forzar un modelo de objetos type-safe del VMAD.
'
' Layout (spec de xEdit, wbVMAD / wbScriptEntry / wbScriptProperty / wbScriptPropertyObject):
'   VMAD          : s16 Version Â· s16 ObjectFormat Â· u16 ScriptCount Â· N x ScriptEntry
'   ScriptEntry   : u16+name Â· u8 Flags Â· u16 PropertyCount Â· N x ScriptProperty
'   ScriptProperty: u16+name Â· u8 Type Â· u8 Flags Â· Value segun Type
'   Object (8 by) : el ObjectFormat del header decide el layout GLOBALMENTE -
'                   v2 (FO4 vanilla) u16 Unused + s16 Alias + FormID (FormID en +4);
'                   v1 (legacy)      FormID + s16 Alias + u16 Unused (FormID en +0).
'   Types: 0 Null Â· 1 Object Â· 2 String (u16+bytes) Â· 3 Int32 Â· 4 Float Â· 5 Bool Â· 6 reservado Â·
'          7 Struct (u32 count + miembros, recursivo) Â· 11..15 arrays de Object/String/Int32/Float/Bool
'          (u32 count + elementos) Â· 16 Array of Variable (solo el count, centinela) Â· 17 Array of Struct.
'
' Es FormID-ONLY: recorre toda la estructura y anota el offset de cada FormID, pero no arma un arbol. El
' round-trip lo da el buffer crudo y la equivalencia de MAST del cleanup la da la lista de posiciones.
' ============================================================================

Public Module NpcVmadScanner

    ''' <summary>Scan a VMAD subrecord payload and return a populated NPC_VmadData with raw bytes
    ''' + sorted FormID position list. Returns Nothing on malformed input (any read past EOF).</summary>
    Public Function Scan(payload As Byte(), pluginName As String, pluginManager As PluginManager) As NPC_VmadData
        If payload Is Nothing OrElse payload.Length < 6 Then Return Nothing

        Dim data As New NPC_VmadData With {
            .RawBytes = payload
        }

        Dim ms As New MemoryStream(payload, False)
        Dim br As New BinaryReader(ms)

        Try
            data.Version = br.ReadInt16()
            data.ObjectFormat = br.ReadInt16()
            data.ScriptCount = br.ReadUInt16()

            For i = 0 To CInt(data.ScriptCount) - 1
                ScanScriptEntry(br, data, pluginName, pluginManager)
            Next

            ' Walked every script and every property without desyncing → FormIdPositions is COMPLETE.
            ' Only now may the writer patch the raw bytes (see NPC_VmadData.ScanComplete).
            data.ScanComplete = True
        Catch ex As EndOfStreamException
            ' Desync: a length/type we read sent us past the end. Everything after the failure point is
            ' unscanned, so the position list is PARTIAL. Raw bytes are still preserved for diagnosis,
            ' but ScanComplete stays False and the writer will refuse to emit rather than write FormIDs
            ' that are only half-remapped.
            data.ScanFailureReason = "read past end of VMAD payload (structure desync)"
        Catch ex As IOException
            ' Unknown property type (thrown by ScanValue) — same consequence: partial position list.
            data.ScanFailureReason = ex.Message
        End Try

        Return data
    End Function

    Private Sub ScanScriptEntry(br As BinaryReader, data As NPC_VmadData, pluginName As String, pluginManager As PluginManager)
        ' u16 nameLen + name + u8 flags + u16 propCount + propCount × ScriptProperty
        Dim entryStart = br.BaseStream.Position
        Dim name = ReadLenString(br)
        br.ReadByte() ' flags
        Dim propCount = br.ReadUInt16()
        For i = 0 To CInt(propCount) - 1
            ScanScriptProperty(br, data, pluginName, pluginManager)
        Next

        ' Record the entry's byte span so NpcVmadBuilder can splice THIS script out (or replace it)
        ' while copying the others verbatim — the basis of idempotent re-saves.
        data.Scripts.Add(New NPC_VmadScriptRef With {
            .Name = name,
            .Offset = CInt(entryStart),
            .Length = CInt(br.BaseStream.Position - entryStart)
        })
    End Sub

    Private Sub ScanScriptProperty(br As BinaryReader, data As NPC_VmadData, pluginName As String, pluginManager As PluginManager)
        ' u16 nameLen + name + u8 type + u8 flags + Value (variable)
        SkipLenString(br)
        Dim ptype = br.ReadByte()
        br.ReadByte() ' flags
        ScanValue(br, data, pluginName, pluginManager, ptype)
    End Sub

    ''' <summary>Saltear <paramref name="byteCount"/> bytes, validando ANTES contra lo que queda del payload.
    ''' <para>⛔ Acá había <c>br.ReadBytes(CInt(count) * 4)</c> con <c>count</c> leído del propio payload, y eso
    ''' EVADÍA el gate que esta clase existe para sostener: <see cref="Scan"/> sólo captura
    ''' <c>EndOfStreamException</c> e <c>IOException</c>, así que la <c>OverflowException</c> del <c>CInt</c>
    ''' (VB tiene los checks de desborde PRENDIDOS — es el mismo motivo por el que los bucles de arriba usan
    ''' <c>CLng</c>) y la <c>OutOfMemoryException</c> de reservar el array se escapaban. Con
    ''' <c>count = 0xFFFFFFFF</c> la excepción atraviesa <c>ParseNPC</c> y la traga el <c>Catch</c> del parse
    ''' masivo: el NPC desaparece del árbol sin un mensaje. Con <c>count = 0x08000000</c> no hay desborde y se
    ''' piden 1 GB de una — que un <c>Catch</c> tampoco evita, porque la memoria ya se pidió.</para>
    ''' <para>Validar primero convierte el desborde en imposible por construcción y expresa la falla en el
    ''' vocabulario que el gate YA entiende: <c>IOException</c> ⇒ <c>ScanComplete = False</c> ⇒ el writer se
    ''' niega a emitir en vez de patchear medio VMAD.</para></summary>
    Private Sub SkipBytes(br As BinaryReader, byteCount As Long)
        If byteCount < 0L Then Throw New IOException($"VMAD array length {byteCount} is negative.")
        Dim s = br.BaseStream
        Dim remaining = s.Length - s.Position
        If byteCount > remaining Then
            Throw New IOException(
                $"VMAD array declares {byteCount} byte(s) but only {remaining} remain in the payload " &
                "(structure desync).")
        End If
        s.Position += byteCount
    End Sub

    ''' <summary>⛔ EMPTY ARRAYS ARE LEGAL AND MUST NOT CRASH. The array cases below iterate as
    ''' <c>For i As Long = 0 To CLng(count) - 1</c>, NOT <c>For i = 0UI To count - 1UI</c>: with
    ''' <c>count = 0</c> the unsigned form underflows to 4294967295 and, because VB.NET has integer
    ''' overflow checks ON by default, throws OverflowException on the subtraction itself.
    ''' <para>This never fired on vanilla data — no vanilla NPC_ VMAD carries a zero-length array — but our
    ''' own emitter does (an NPC with skin overrides but no overlays leaves the overlay arrays empty), and it
    ''' blew up the moment we read our own output back. Widening to Long makes the loop simply not execute,
    ''' which is the correct behaviour for a 0-element array.</para></summary>
    Private Sub ScanValue(br As BinaryReader, data As NPC_VmadData, pluginName As String, pluginManager As PluginManager, ptype As Byte)
        Select Case ptype
            Case 0, 6
                ' Null — 0 bytes
                Return
            Case 1
                ScanObject(br, data, pluginName, pluginManager)
            Case 2
                SkipLenString(br)
            Case 3, 4
                br.ReadInt32() ' Int32 or Float
            Case 5
                br.ReadByte() ' Bool
            Case 7
                ScanStruct(br, data, pluginName, pluginManager)
            Case 11
                Dim count = br.ReadUInt32()
                For i As Long = 0 To CLng(count) - 1
                    ScanObject(br, data, pluginName, pluginManager)
                Next
            Case 12
                Dim count = br.ReadUInt32()
                For i As Long = 0 To CLng(count) - 1
                    SkipLenString(br)
                Next
            Case 13, 14
                Dim count = br.ReadUInt32()
                SkipBytes(br, CLng(count) * 4L)
            Case 15
                Dim count = br.ReadUInt32()
                SkipBytes(br, CLng(count))
            Case 16
                ' Array of Variable — u32 count only per wbDefinitionsFO4.pas:4163.
                br.ReadUInt32()
            Case 17
                Dim count = br.ReadUInt32()
                For i As Long = 0 To CLng(count) - 1
                    ScanStruct(br, data, pluginName, pluginManager)
                Next
            Case Else
                ' Unknown type — we no longer know the value's byte length, so we cannot keep walking
                ' without desyncing. Abort: Scan() catches this and leaves ScanComplete = False, which
                ' makes the writer refuse the record instead of emitting a half-remapped VMAD.
                ' Measured: 0 occurrences across all 1333 vanilla NPC_ VMADs (Skyrim.esm 805,
                ' Dawnguard.esm 146, Fallout4.esm 382), so this only fires on malformed/exotic input.
                Throw New IOException($"unknown VMAD property type {ptype}")
        End Select
    End Sub

    Private Sub ScanObject(br As BinaryReader, data As NPC_VmadData, pluginName As String, pluginManager As PluginManager)
        ' Object union — 8 bytes total. Layout depends on ObjectFormat global.
        ' Per wbScriptObjFormatDecider (wbDefinitionsCommon.pas:4518) + wbGetScriptObjFormat
        ' (wbDefinitionsCommon.pas:2050-2070): the decider returns 1 ONLY when ObjectFormat == 1,
        ' returning 0 for any other value (including 0=uninitialized and 2=FO4 vanilla default).
        ' The wbScriptPropertyObject union (wbDefinitionsFO4.pas:4115-4137) lists v2 at index 0
        ' (default) and v1 at index 1. So: ObjectFormat == 1 → v1 layout; everything else → v2.
        Dim posStart = br.BaseStream.Position
        Dim formIdOffset As Long
        If data.ObjectFormat = 1 Then
            ' v1: formid + s16 Alias + u16 Unused (FormID @ +0)
            formIdOffset = posStart
        Else
            ' v2 (FO4 vanilla, also fallback for ObjectFormat=0 and other unknowns):
            '   u16 Unused + s16 Alias + formid (FormID @ +4)
            formIdOffset = posStart + 4
        End If

        ' Capture FormID at the resolved offset.
        br.BaseStream.Position = formIdOffset
        Dim raw = br.ReadUInt32()
        Dim resolved As UInteger = raw
        If pluginManager IsNot Nothing AndAlso Not String.IsNullOrEmpty(pluginName) Then
            resolved = pluginManager.ResolveReferencedFormID(pluginName, raw)
        End If
        data.FormIdPositions.Add(New NPC_VmadFormIdRef With {
            .Offset = CInt(formIdOffset),
            .RawFormID = raw,
            .ResolvedFormID = resolved
        })

        ' Advance to end of the 8-byte Object struct regardless of where we read FormID.
        br.BaseStream.Position = posStart + 8
    End Sub

    Private Sub ScanStruct(br As BinaryReader, data As NPC_VmadData, pluginName As String, pluginManager As PluginManager)
        ' wbScriptPropertyStruct @ wbDefinitionsFO4.pas:4139-4166: array u32 count + count × Member.
        ' Each Member = u16 nameLen + name + u8 type + u8 flags + Value.
        Dim memberCount = br.ReadUInt32()
        ' Long, not UInteger — a 0-member struct would underflow the loop bound (see ScanValue's note).
        For i As Long = 0 To CLng(memberCount) - 1
            SkipLenString(br)
            Dim ptype = br.ReadByte()
            br.ReadByte() ' flags
            ScanValue(br, data, pluginName, pluginManager, ptype)
        Next
    End Sub

    Private Sub SkipLenString(br As BinaryReader)
        ' LenString prefixed by u16 length (xEdit wbLenString(name, 2)).
        Dim len = br.ReadUInt16()
        If len > 0 Then br.ReadBytes(CInt(len))
    End Sub

    ''' <summary>Same LenString, but decoded. VMAD strings are UTF-8 regardless of game or language
    ''' (xEdit wbEncodingVMAD := TEncoding.UTF8) — see PluginEncodingSettings.EncodeVmad.</summary>
    Private Function ReadLenString(br As BinaryReader) As String
        Dim len = br.ReadUInt16()
        If len = 0 Then Return ""
        Dim bytes = br.ReadBytes(CInt(len))
        If bytes.Length < len Then Throw New EndOfStreamException("VMAD: truncated string")
        Return Text.Encoding.UTF8.GetString(bytes)
    End Function

End Module
