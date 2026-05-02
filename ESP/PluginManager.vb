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

    ''' <summary>Load only ACTIVATED plugins from the Fallout 4 Data path. Order source priority:
    ''' 1) loadorder.txt (LOOT/Vortex managed; full ordered list with implicits + actives).
    ''' 2) Plugins.txt with `*activated` markers + hardcoded implicits prepended.
    ''' Plugins NOT in the active set are ignored (no Data folder scan). Replicates engine load:
    ''' un .esp suelto en Data sin estar activado NO se carga in-game; tampoco acá.</summary>
    Public Sub LoadAllPlugins(dataPath As String, Optional progress As IProgress(Of String) = Nothing)
        LoadAllPlugins(dataPath, ReadActiveLoadOrder(), progress)
    End Sub

    ''' <summary>Load an explicit, caller-supplied set of plugins in the given order. Used when the
    ''' app wants to load inactive plugins too (e.g. NPC_Manager preflight: user tickea inactivos),
    ''' or to load a subset for inspection. The caller is responsible for ordering — implicit
    ''' masters (Fallout4.esm + DLCs) must come first if the caller wants engine-correct FormID
    ''' resolution. <see cref="ReadActiveLoadOrder"/> already produces a correctly-ordered list
    ''' that the caller can extend with extra inactive entries before passing in.</summary>
    Public Sub LoadAllPlugins(dataPath As String,
                              pluginsToLoad As IEnumerable(Of String),
                              Optional progress As IProgress(Of String) = Nothing)
        Dim pluginFiles As New List(Of String)

        _localizedStrings = New LocalizedStringResolver(dataPath)

        For Each pluginName In pluginsToLoad
            Dim fullPath = Path.Combine(dataPath, pluginName)
            If File.Exists(fullPath) Then
                pluginFiles.Add(fullPath)
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
        ' Regla canónica del engine FO4: el override REEMPLAZA al record entero.
        ' Si el override no incluye un subrecord que el master sí tenía, el subrecord
        ' queda EFECTIVAMENTE BORRADO en el record final — NO se hereda. Eso es lo que
        ' permite a un mod tipo CBBEHeadRearFix.esp "limpiar" un TNAM que CBBE.esp puso.
        ' Lo que xEdit muestra como "valor heredado" en columnas de override es display-only;
        ' el binario del override no contiene ese subrecord.
        ' Intento previo de subrecord-level merge: REVERTIDO. Era un invento mío que rompía
        ' el caso CBBEHeadRearFix (heredaba TNAM=SkinHeadRearCBBE de CBBE.esp pisando la
        ' decisión del modder de borrarlo).
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

    ''' <summary>Read the ordered list of ACTIVE plugins for the current game (FO4 or SSE).
    ''' Replicates engine load order: (1) implicit base masters always loaded by the engine
    ''' (game .esm + DLCs); these never appear in Plugins.txt but the engine always loads them.
    ''' (2) Creation Club entries from Fallout4.ccc (FO4 only) — engine treats these as
    ''' force-active, same as DLCs (xEdit Core/wbLoadOrder.pas:495-501; the .ccc file lives next
    ''' to the .exe, not in LocalAppData). (3) entries from Plugins.txt marked with `*` (active
    ''' flag). Plugins NOT in Plugins.txt or without `*` are skipped — un .esp suelto en Data
    ''' sin estar activado no se carga in-game; no debe cargarse acá.
    '''
    ''' Note on loadorder.txt: archivo opcional generado por LOOT/Vortex con todos los plugins
    ''' (activos e inactivos) en orden completo. NO indica activación, así que NO podemos usarlo
    ''' como única fuente. Plugins.txt + .ccc + implicits son las fuentes autoritativas de
    ''' activación. Si loadorder.txt existe, lo usamos como ORDEN para los actives; si no,
    ''' usamos el orden canónico (implicits → CC → Plugins.txt).</summary>
    Public Shared Function ReadActiveLoadOrder() As List(Of String)
        Dim isFO4 As Boolean = (Config_App.Current.Game = Config_App.Game_Enum.Fallout4)
        Dim appDataSubdir As String = If(isFO4, "Fallout4", "Skyrim Special Edition")
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim pluginsTxt = Path.Combine(appData, appDataSubdir, "Plugins.txt")
        If Not File.Exists(pluginsTxt) Then pluginsTxt = Path.Combine(appData, appDataSubdir, "plugins.txt")

        ' Implicit masters: el engine carga estos siempre primero, no aparecen en Plugins.txt.
        ' Spec verificada contra ejecución vanilla y herramientas LOOT/xEdit.
        Dim implicits As List(Of String)
        If isFO4 Then
            implicits = New List(Of String) From {
                "Fallout4.esm",
                "DLCRobot.esm",
                "DLCworkshop01.esm",
                "DLCCoast.esm",
                "DLCworkshop02.esm",
                "DLCworkshop03.esm",
                "DLCNukaWorld.esm",
                "DLCUltraHighResolution.esm"
            }
        Else
            implicits = New List(Of String) From {
                "Skyrim.esm",
                "Update.esm",
                "Dawnguard.esm",
                "HearthFires.esm",
                "Dragonborn.esm"
            }
        End If

        ' Creation Club content: Fallout4.ccc lives next to Fallout4.exe (xEdit
        ' xeMainForm.pas:5067-5080 derives it as ExtractFilePath(ExcludeTrailingPathDelimiter(wbDataPath))).
        ' Each non-empty non-comment line is a plugin name the engine force-loads after the DLCs.
        ' Skyrim has its own Skyrim.ccc; same shape. Only attempted if FO4ExePath resolved.
        Dim ccEntries As New List(Of String)
        Dim exePath = Config_App.Current.FO4ExePath
        If Not String.IsNullOrEmpty(exePath) AndAlso File.Exists(exePath) Then
            Dim cccName = If(isFO4, "Fallout4.ccc", "Skyrim.ccc")
            Dim cccPath = Path.Combine(Path.GetDirectoryName(exePath), cccName)
            If File.Exists(cccPath) Then
                For Each line In File.ReadAllLines(cccPath, Encoding.UTF8)
                    Dim trimmed = line.Trim()
                    If trimmed.Length = 0 Then Continue For
                    If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                    ccEntries.Add(trimmed)
                Next
            End If
        End If

        ' Build active set: implicits + CC + Plugins.txt actives.
        Dim activeSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each m In implicits
            activeSet.Add(m)
        Next
        For Each m In ccEntries
            activeSet.Add(m)
        Next

        Dim activeFromPluginsTxt As New List(Of String)
        If File.Exists(pluginsTxt) Then
            For Each line In File.ReadAllLines(pluginsTxt, Encoding.UTF8)
                Dim trimmed = line.Trim()
                If trimmed.Length = 0 Then Continue For
                If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                If Not trimmed.StartsWith("*") Then Continue For   ' inactive entries: skip
                trimmed = trimmed.Substring(1).Trim()
                If trimmed.Length > 0 Then
                    activeFromPluginsTxt.Add(trimmed)
                    activeSet.Add(trimmed)
                End If
            Next
        End If

        ' Use loadorder.txt as ordering source if available (LOOT/Vortex managed).
        ' Filter to only active plugins; preserves the ordering decisions made by the manager.
        Dim loadorderTxt = Path.Combine(appData, appDataSubdir, "loadorder.txt")
        Dim ordered As New List(Of String)
        If File.Exists(loadorderTxt) Then
            For Each line In File.ReadAllLines(loadorderTxt, Encoding.UTF8)
                Dim trimmed = line.Trim()
                If trimmed.Length = 0 Then Continue For
                If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                If trimmed.StartsWith("*") Then trimmed = trimmed.Substring(1).Trim()
                If trimmed.Length = 0 Then Continue For
                If activeSet.Contains(trimmed) Then ordered.Add(trimmed)
            Next
            ' Append any actives that loadorder.txt didn't list (rare edge: just-installed plugin
            ' or LOOT not aware of CC entries). Insert implicits at the front, CC after them, and
            ' Plugins.txt extras at the end — same canonical order as the no-loadorder.txt path.
            Dim insertPos As Integer = 0
            For Each p In implicits
                If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then
                    ordered.Insert(insertPos, p)
                    insertPos += 1
                End If
            Next
            For Each p In ccEntries
                If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then
                    ordered.Insert(insertPos, p)
                    insertPos += 1
                End If
            Next
            For Each p In activeFromPluginsTxt
                If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then ordered.Add(p)
            Next
            Return ordered
        End If

        ' Fallback: implicits + CC + Plugins.txt activos en orden literal.
        ordered.AddRange(implicits)
        For Each p In ccEntries
            If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then ordered.Add(p)
        Next
        For Each p In activeFromPluginsTxt
            If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then ordered.Add(p)
        Next
        Return ordered
    End Function

    ''' <summary>Backward-compat alias. Old callers expected "all plugins from loadorder.txt"
    ''' but the right semantic is "active load order". Returns same list as ReadActiveLoadOrder.</summary>
    Public Shared Function ReadLoadOrder() As List(Of String)
        Return ReadActiveLoadOrder()
    End Function
End Class


