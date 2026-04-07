Imports System.Text

' ============================================================================
' Shared helper functions used by all record parser modules.
' These are called by every parser file to resolve FormIDs and strings.
' ============================================================================

Public Module ParserHelpers

    ''' <summary>Resolve a display string from a subrecord, handling localization.</summary>
    Public Function ResolveStr(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager,
                               Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If pluginManager Is Nothing Then Return sr.AsString
        Return pluginManager.ResolveFieldString(rec, sr, kind)
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
