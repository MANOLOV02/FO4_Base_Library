Imports System.IO
Imports System.Linq
Imports System.Text

''' <summary>
''' Manages multiple plugins with load order, FormID resolution, and record override logic.
''' </summary>
Public Class PluginManager
    ''' <summary>All loaded plugins in load order.</summary>
    Public Property Plugins As New List(Of PluginReader)

    ''' <summary>Plugin name -> index in Plugins list.</summary>
    Private ReadOnly _pluginIndex As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private _localizedStrings As LocalizedStringResolver

    ''' <summary>Global FormID -> final PluginRecord (last override wins).</summary>
    Public Property AllRecords As New Dictionary(Of UInteger, PluginRecord)

    ''' <summary>Records grouped by signature type.</summary>
    Public Property RecordsByType As New Dictionary(Of String, List(Of PluginRecord))

    ''' <summary>Load all plugins from the Fallout 4 Data path using loadorder.txt.</summary>
    Public Sub LoadAllPlugins(dataPath As String, Optional progress As IProgress(Of String) = Nothing)
        Dim loadOrder = ReadLoadOrder()
        Dim pluginFiles As New List(Of String)

        _localizedStrings = New LocalizedStringResolver(dataPath)

        For Each pluginName In loadOrder
            Dim fullPath = Path.Combine(dataPath, pluginName)
            If File.Exists(fullPath) Then
                pluginFiles.Add(fullPath)
            End If
        Next

        For Each f In Directory.EnumerateFiles(dataPath, "*.es?", SearchOption.TopDirectoryOnly)
            Dim ext = Path.GetExtension(f).ToLowerInvariant()
            If ext = ".esm" OrElse ext = ".esp" OrElse ext = ".esl" Then
                If Not pluginFiles.Any(Function(p) String.Equals(Path.GetFileName(p), Path.GetFileName(f), StringComparison.OrdinalIgnoreCase)) Then
                    pluginFiles.Add(f)
                End If
            End If
        Next

        For i = 0 To pluginFiles.Count - 1
            Dim filePath = pluginFiles(i)
            Dim fileName = Path.GetFileName(filePath)
            progress?.Report($"Loading {fileName} ({i + 1}/{pluginFiles.Count})")
            Try
                Dim reader As New PluginReader()
                reader.Load(filePath)
                _pluginIndex(reader.FileName) = Plugins.Count
                Plugins.Add(reader)
                MergeRecords(reader)
            Catch ex As Exception
                Debug.Print($"[ESP] Failed to load {fileName}: {ex.Message}")
            End Try
        Next

        BuildTypeIndex()
    End Sub

    ''' <summary>Resolve a file-local FormID to a global FormID using the plugin's master list.</summary>
    Public Function ResolveFormID(localFormID As UInteger, plugin As PluginReader) As UInteger
        Dim masterIndex = CInt(localFormID >> 24)
        Dim objectID = localFormID And &HFFFFFFUI

        If masterIndex < plugin.Masters.Count Then
            Dim masterName = plugin.Masters(masterIndex)
            Dim masterIdx As Integer = -1
            If _pluginIndex.TryGetValue(masterName, masterIdx) Then
                Return (CUInt(masterIdx) << 24) Or objectID
            End If
        End If

        Dim selfIdx As Integer = -1
        If _pluginIndex.TryGetValue(plugin.FileName, selfIdx) Then
            Return (CUInt(selfIdx) << 24) Or objectID
        End If

        Return localFormID
    End Function

    ''' <summary>Resolve a referenced FormID using the source plugin that owns the record.</summary>
    Public Function ResolveReferencedFormID(sourcePluginName As String, localFormID As UInteger) As UInteger
        If localFormID = 0UI Then Return 0UI
        If String.IsNullOrWhiteSpace(sourcePluginName) Then Return localFormID

        Dim pluginIdx As Integer = -1
        If Not _pluginIndex.TryGetValue(sourcePluginName, pluginIdx) Then Return localFormID
        If pluginIdx < 0 OrElse pluginIdx >= Plugins.Count Then Return localFormID

        Return ResolveFormID(localFormID, Plugins(pluginIdx))
    End Function

    Public Function GetPluginNameByLoadOrderIndex(index As Integer) As String
        If index < 0 OrElse index >= Plugins.Count Then Return ""
        Return Plugins(index).FileName
    End Function

    Public Function GetOriginatingPluginName(formID As UInteger) As String
        Dim loadOrderIndex = CInt((formID >> 24) And &HFFUI)
        If loadOrderIndex = &HFE Then
            Dim rec = GetRecord(formID)
            Return If(rec IsNot Nothing, rec.SourcePluginName, "")
        End If
        Return GetPluginNameByLoadOrderIndex(loadOrderIndex)
    End Function
    Public Function ResolveFieldString(rec As PluginRecord, sr As SubrecordData, Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If sr.Data Is Nothing OrElse sr.Data.Length = 0 Then Return ""

        If rec IsNot Nothing AndAlso rec.SourcePluginIsLocalized AndAlso rec.SourcePluginName <> "" AndAlso sr.Data.Length >= 4 Then
            Dim stringId = BitConverter.ToUInt32(sr.Data, 0)
            If stringId <> 0UI AndAlso _localizedStrings IsNot Nothing Then
                Dim resolved = _localizedStrings.Resolve(rec.SourcePluginName, stringId, kind)
                If resolved <> "" Then Return resolved
            End If
            Return $"<Error: Unknown lstring ID {stringId:X8}>"
        End If

        Return sr.AsString
    End Function

    ''' <summary>Get the final resolved record for a FormID (after overrides).</summary>
    Public Function GetRecord(formID As UInteger) As PluginRecord
        Dim rec As PluginRecord = Nothing
        AllRecords.TryGetValue(formID, rec)
        Return rec
    End Function

    ''' <summary>Get all records of a specific type.</summary>
    Public Function GetRecordsOfType(sig As String) As List(Of PluginRecord)
        Dim result As List(Of PluginRecord) = Nothing
        If RecordsByType.TryGetValue(sig, result) Then Return result
        Return New List(Of PluginRecord)
    End Function

    ''' <summary>Get all NPC_ records.</summary>
    Public Function GetNPCs() As List(Of PluginRecord)
        Return GetRecordsOfType("NPC_")
    End Function

    Private Sub MergeRecords(reader As PluginReader)
        For Each kvp In reader.Records
            Dim globalFormID = ResolveFormID(kvp.Key, reader)
            kvp.Value.Header.FormID = globalFormID
            AllRecords(globalFormID) = kvp.Value
        Next
    End Sub

    Private Sub BuildTypeIndex()
        RecordsByType.Clear()
        For Each kvp In AllRecords
            Dim sig = kvp.Value.Header.Signature
            Dim list As List(Of PluginRecord) = Nothing
            If Not RecordsByType.TryGetValue(sig, list) Then
                list = New List(Of PluginRecord)
                RecordsByType(sig) = list
            End If
            list.Add(kvp.Value)
        Next
    End Sub

    ''' <summary>Get the set of base NPC FormIDs that are placed in the world (ACHR records).
    ''' Requires CELL/WRLD groups to be loaded.</summary>
    Public Function GetPlacedNPCFormIDs() As HashSet(Of UInteger)
        Dim result As New HashSet(Of UInteger)()
        Dim achrRecords = GetRecordsOfType("ACHR")
        For Each rec In achrRecords
            Dim nameSr = rec.GetSubrecord("NAME")
            If nameSr.HasValue AndAlso nameSr.Value.Data IsNot Nothing AndAlso nameSr.Value.Data.Length >= 4 Then
                Dim baseFormID = ResolveReferencedFormID(rec.SourcePluginName, nameSr.Value.AsUInt32)
                If baseFormID <> 0UI Then result.Add(baseFormID)
            End If
        Next
        Return result
    End Function

    ''' <summary>Read load order from Fallout4's loadorder.txt.</summary>
    Public Shared Function ReadLoadOrder() As List(Of String)
        Dim result As New List(Of String)
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim loadOrderPath = Path.Combine(appData, "Fallout4", "loadorder.txt")

        If Not File.Exists(loadOrderPath) Then
            loadOrderPath = Path.Combine(appData, "Fallout4", "plugins.txt")
        End If

        If Not File.Exists(loadOrderPath) Then Return result

        For Each line In File.ReadAllLines(loadOrderPath, Encoding.UTF8)
            Dim trimmed = line.Trim()
            If trimmed.Length = 0 Then Continue For
            If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
            If trimmed.StartsWith("*") Then trimmed = trimmed.Substring(1).Trim()
            If trimmed.Length > 0 Then result.Add(trimmed)
        Next

        Return result
    End Function
End Class


