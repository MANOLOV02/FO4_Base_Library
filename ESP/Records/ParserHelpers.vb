Imports System.Text

' ============================================================================
' Shared helper functions used by all record parser modules.
' These are called by every parser file to resolve FormIDs and strings.
' ============================================================================

Public Module ParserHelpers

    ''' <summary>Resolve a display string from a subrecord, handling localization.</summary>
    Public Function ResolveStr(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager,
                               Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If pluginManager IsNot Nothing Then Return pluginManager.ResolveFieldString(rec, sr, kind)

        ' No pluginManager: still honor per-file encoding (TES4 SNAM <cp:XXXX>) when available,
        ' falling back to the global only when the record has no override. Mirror of the same
        ' bsdGetEncoding precedence used inside ResolveFieldString.
        If rec IsNot Nothing AndAlso rec.SourcePluginTranslatableEncoding IsNot Nothing AndAlso Not rec.SourcePluginIsLocalized Then
            If sr.Data Is Nothing OrElse sr.Data.Length = 0 Then Return ""
            Dim len = sr.Data.Length
            If len > 0 AndAlso sr.Data(len - 1) = 0 Then len -= 1
            Return PluginEncodingSettings.DecodeWithEncoding(sr.Data, 0, len, rec.SourcePluginTranslatableEncoding)
        End If

        Return sr.AsString
    End Function

    ''' <summary>Resolve a FormID reference from a subrecord.</summary>
    Public Function ResolveFID(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As UInteger
        If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Return 0UI
        Return ResolveFIDRaw(rec, sr.AsUInt32, pluginManager)
    End Function

    ''' <summary>Resolve a raw FormID value using the plugin's master list.</summary>
    Public Function ResolveFIDRaw(rec As PluginRecord, rawFormID As UInteger, pluginManager As PluginManager) As UInteger
        If pluginManager Is Nothing OrElse rec Is Nothing Then Return rawFormID
        Return pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rawFormID)
    End Function

    ''' <summary>Parse an array of FormIDs from a KWDA-style subrecord into a list.</summary>
    Public Sub ParseFormIDArray(sr As SubrecordData, rec As PluginRecord, pluginManager As PluginManager, target As List(Of UInteger))
        If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Return
        For i = 0 To sr.Data.Length - 4 Step 4
            Dim fid = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager)
            If fid <> 0UI Then target.Add(fid)
        Next
    End Sub

End Module
