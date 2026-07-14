Imports System.Text

''' <summary>Reads the SCRIPT PROPERTIES out of a record's <c>VMAD</c> (Papyrus script attachment), BY NAME.
'''
''' Why this exists: some behaviour a mod relies on is not in the records at all — it is a Papyrus script that
''' mutates game state at runtime, and the only static trace of it is (a) which scripts are attached and (b) what
''' their properties are bound to. RaceCompatibility is the canonical case: a custom-race mod attaches
''' <c>GenericRaceController</c> to a quest, fills its properties with the vanilla head-part FormLists, and on
''' <c>OnInit</c> the script INSERTS its new races into those FormLists in memory (see RaceCompatibilityCatalog).
''' None of that lands in a plugin, so a records-only reader can never see it; reconstructing it needs these typed
''' property values plus the compiled script (<see cref="PapyrusPexParser.ExtractPropertyBindings"/>).
'''
''' Distinct from <see cref="NpcVmadScanner"/>, which is a FormID-POSITION scanner (it records byte offsets so the
''' writer can remap them and never interprets the payload). Here we want typed values keyed by property name.
'''
''' LAYOUT — faithful to xEdit (wbDefinitionsTES5.pas:2953-3030 + :3170-3183, wbDefinitionsFO4.pas:4115-4215;
''' the shape is IDENTICAL in both games, only the property-type set differs):
'''   VMAD    : Version(s16) | Object Format(s16) | Scripts: u16 count
'''   Script  : ScriptName(u16-len string) | Flags(u8) | Properties: u16 count
'''   Property: propertyName(u16-len string) | Type(u8) | Flags(u8) | Value
'''   Object union (wbScriptObjFormatDecider → wbGetScriptObjFormat, wbDefinitionsCommon.pas:2050): Object Format
'''     == 1 ⇒ v1 = FormID(u32), Alias(s16), Unused(u16);  anything else (2) ⇒ v2 = Unused(u16), Alias(s16),
'''     FormID(u32) — i.e. in v2 (the modern default) the FormID is the LAST 4 bytes, not the first.
'''   Value types: 1=Object, 2=String, 3=Int32, 4=Float, 5=Bool, 11..15 = arrays (u32 count) of 1..5.
'''     FO4 additionally has 6/7 (Struct) and 16/17 (Array of Variable / of Struct) — game-gated below.
''' Note the Flags byte is ALWAYS present per the spec (it is not version-gated).
'''
''' Object FormIDs are plugin-LOCAL and are resolved to GLOBAL here, exactly like every other parsed reference.</summary>
Public NotInheritable Class VmadPropertyReader

    Private Sub New()
    End Sub

    Public Enum PropKind
        Unsupported = 0
        Obj = 1
        Str = 2
        Int32 = 3
        Flt = 4
        Bool = 5
    End Enum

    Public Class PropValue
        Public Kind As PropKind = PropKind.Unsupported
        ''' <summary>Resolved GLOBAL FormID for <see cref="PropKind.Obj"/>. 0 = None (property left unbound).</summary>
        Public FormID As UInteger
        Public StringValue As String = ""
        Public IntValue As Integer
        Public FloatValue As Single
        Public BoolValue As Boolean
    End Class

    Public Class ScriptEntry
        Public Name As String = ""
        Public Properties As New Dictionary(Of String, PropValue)(StringComparer.OrdinalIgnoreCase)
    End Class

    ''' <summary>The scripts attached to <paramref name="rec"/> with their properties. Empty when the record has no
    ''' VMAD; on a malformed payload it returns whatever parsed cleanly before the break (a broken mod script must
    ''' not take the load down). <paramref name="game"/> selects the property-type set (FO4 has struct types).</summary>
    Public Shared Function ReadScripts(rec As PluginRecord, pluginManager As PluginManager,
                                       game As Config_App.Game_Enum) As List(Of ScriptEntry)
        Dim result As New List(Of ScriptEntry)
        If rec Is Nothing OrElse rec.Subrecords Is Nothing Then Return result
        ' Subrecord is a value type here, so no null-check pattern: pull the payload directly.
        Dim d As Byte() = Nothing
        For Each sr In rec.Subrecords
            If sr.Signature = "VMAD" Then d = sr.Data : Exit For
        Next
        If d Is Nothing OrElse d.Length < 6 Then Return result

        Dim p As Integer = 0
        Try
            p += 2                                                   ' Version (s16) — not needed: the layout below is version-invariant
            Dim objFormat = BitConverter.ToInt16(d, p) : p += 2      ' Object Format (s16): 1 = v1, else v2
            Dim scriptCount = BitConverter.ToUInt16(d, p) : p += 2

            For i = 1 To scriptCount
                Dim entry As New ScriptEntry With {.Name = ReadWString(d, p)}
                p += 1                                               ' Script Flags (u8)
                Dim propCount = BitConverter.ToUInt16(d, p) : p += 2
                For j = 1 To propCount
                    Dim pname = ReadWString(d, p)
                    Dim ptype = CInt(d(p)) : p += 1
                    p += 1                                           ' Property Flags (u8)
                    Dim pv = ReadValue(d, p, ptype, objFormat, rec, pluginManager, game)
                    If Not String.IsNullOrEmpty(pname) Then entry.Properties(pname) = pv
                Next
                result.Add(entry)
            Next
        Catch
            ' Truncated / unexpected layout: keep the scripts that parsed cleanly, drop the rest.
        End Try
        Return result
    End Function

    ''' <summary>u16-length-prefixed string (wbLenString ..., 2), advancing the cursor.</summary>
    Private Shared Function ReadWString(d As Byte(), ByRef p As Integer) As String
        Dim n = BitConverter.ToUInt16(d, p) : p += 2
        Dim s = Encoding.GetEncoding("ISO-8859-1").GetString(d, p, n)
        p += n
        Return s
    End Function

    ''' <summary>One property value. Arrays and (FO4) structs are WALKED even though we don't surface them — the
    ''' cursor must stay aligned or every property after them is garbage. An unknown type throws, which the caller
    ''' turns into "keep what parsed so far" rather than silently mis-reading the tail.</summary>
    Private Shared Function ReadValue(d As Byte(), ByRef p As Integer, ptype As Integer, objFormat As Integer,
                                      rec As PluginRecord, pluginManager As PluginManager,
                                      game As Config_App.Game_Enum) As PropValue
        Select Case ptype
            Case 1 ' Object union — 8 bytes. v1: FormID first. v2 (objFormat <> 1): FormID LAST.
                Dim localFid As UInteger = If(objFormat = 1, BitConverter.ToUInt32(d, p), BitConverter.ToUInt32(d, p + 4))
                p += 8
                Dim glob As UInteger = 0
                If localFid <> 0UI AndAlso pluginManager IsNot Nothing Then
                    glob = pluginManager.ResolveReferencedFormID(rec.SourcePluginName, localFid)
                End If
                Return New PropValue With {.Kind = PropKind.Obj, .FormID = glob}
            Case 2
                Return New PropValue With {.Kind = PropKind.Str, .StringValue = ReadWString(d, p)}
            Case 3
                Dim v = BitConverter.ToInt32(d, p) : p += 4
                Return New PropValue With {.Kind = PropKind.Int32, .IntValue = v}
            Case 4
                Dim v = BitConverter.ToSingle(d, p) : p += 4
                Return New PropValue With {.Kind = PropKind.Flt, .FloatValue = v}
            Case 5
                Dim v = d(p) : p += 1
                Return New PropValue With {.Kind = PropKind.Bool, .BoolValue = (v <> 0)}
            Case 6 ' FO4: wbNull
                If game <> Config_App.Game_Enum.Fallout4 Then Throw New FormatException("VMAD: type 6 is FO4-only")
                Return New PropValue With {.Kind = PropKind.Unsupported}
            Case 7 ' FO4: Struct = u32 count of members, each memberName + Type + Flags + Value
                If game <> Config_App.Game_Enum.Fallout4 Then Throw New FormatException("VMAD: type 7 (Struct) is FO4-only")
                ReadStruct(d, p, objFormat, rec, pluginManager, game)
                Return New PropValue With {.Kind = PropKind.Unsupported}
            Case 11 To 15 ' Array of (type - 10): u32 count + elements
                Dim count = BitConverter.ToUInt32(d, p) : p += 4
                For k = 1UI To count
                    ReadValue(d, p, ptype - 10, objFormat, rec, pluginManager, game)
                Next
                Return New PropValue With {.Kind = PropKind.Unsupported}
            Case 16 ' FO4: Array of Variable = just an element count
                If game <> Config_App.Game_Enum.Fallout4 Then Throw New FormatException("VMAD: type 16 is FO4-only")
                p += 4
                Return New PropValue With {.Kind = PropKind.Unsupported}
            Case 17 ' FO4: Array of Struct
                If game <> Config_App.Game_Enum.Fallout4 Then Throw New FormatException("VMAD: type 17 is FO4-only")
                Dim count = BitConverter.ToUInt32(d, p) : p += 4
                For k = 1UI To count
                    ReadStruct(d, p, objFormat, rec, pluginManager, game)
                Next
                Return New PropValue With {.Kind = PropKind.Unsupported}
            Case Else
                Throw New FormatException($"VMAD: unknown property type {ptype}")
        End Select
    End Function

    ''' <summary>FO4 Struct (wbScriptPropertyStruct): u32 member count, then memberName/Type/Flags/Value each.</summary>
    Private Shared Sub ReadStruct(d As Byte(), ByRef p As Integer, objFormat As Integer,
                                  rec As PluginRecord, pluginManager As PluginManager, game As Config_App.Game_Enum)
        Dim members = BitConverter.ToUInt32(d, p) : p += 4
        For m = 1UI To members
            ReadWString(d, p)                   ' memberName
            Dim mtype = CInt(d(p)) : p += 1
            p += 1                              ' Flags
            ReadValue(d, p, mtype, objFormat, rec, pluginManager, game)
        Next
    End Sub
End Class
