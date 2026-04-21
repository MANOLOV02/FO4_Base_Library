Imports System.Linq

''' <summary>Resolver canónico de outfits (OTFT) para los consumidores del stack
''' (FO4_NPC_Manager hoy; Wardrobe_Manager o futuras apps mañana).
'''
''' Semántica fiel al engine FO4:
'''   - Por cada INAM del OTFT:
'''       * ARMO  → incluir la ARMO terminal (resolviendo cadena de templates CNAM).
'''       * LVLI  → sampling recursivo. UseAll → todas las entries. No UseAll → una entry al azar.
'''                 ChanceNone puede dejar slot vacío (sin reintento: coherente con engine).
'''   - Soporta `CalculateEachItemInCount` (bit 0x02 de LVLF): cuando está activo y Count > 1,
'''     se samplea la entry tantas veces como Count.
'''
''' No enumera combinaciones (el cross-join explota con OTFTs modded). El consumidor llama
''' `SampleOutfitRealization` cuantas veces necesite (ej. Reroll).</summary>
Public Module OutfitResolver

    Private ReadOnly _rngLock As New Object()
    Private _rng As New Random()

    ''' <summary>Opcional: permite a los tests o apps fijar un seed determinista.</summary>
    Public Sub SetSeed(seed As Integer)
        SyncLock _rngLock
            _rng = New Random(seed)
        End SyncLock
    End Sub

    ''' <summary>Sampla una realización de outfit. Devuelve la lista de ARMO terminal
    ''' FormIDs a equipar simultáneamente (dedupeada).</summary>
    Public Function SampleOutfitRealization(otftFormID As UInteger,
                                            pluginManager As PluginManager,
                                            Optional warnings As List(Of String) = Nothing) As List(Of UInteger)
        Dim armors As New List(Of UInteger)
        If otftFormID = 0UI OrElse pluginManager Is Nothing Then Return armors

        Dim rec = pluginManager.GetRecord(otftFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "OTFT" Then
            If warnings IsNot Nothing Then warnings.Add($"Outfit {otftFormID:X8} missing or not OTFT")
            Return armors
        End If

        Dim otft = RecordParsers.ParseOTFT(rec, pluginManager)
        For Each itemFormID In otft.ItemFormIDs
            SampleOutfitItem(itemFormID, pluginManager, New HashSet(Of UInteger)(), armors, warnings)
        Next

        Return armors.Distinct().ToList()
    End Function

    ''' <summary>Resuelve la ARMO terminal siguiendo la cadena de templates CNAM.</summary>
    Public Function ResolveTerminalArmorFormID(armoFormID As UInteger,
                                               pluginManager As PluginManager,
                                               Optional visited As HashSet(Of UInteger) = Nothing) As UInteger
        If armoFormID = 0UI OrElse pluginManager Is Nothing Then Return 0UI
        If visited Is Nothing Then visited = New HashSet(Of UInteger)()
        If visited.Contains(armoFormID) Then Return armoFormID

        Dim rec = pluginManager.GetRecord(armoFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "ARMO" Then Return 0UI

        visited.Add(armoFormID)
        Dim armo = RecordParsers.ParseARMO(rec, pluginManager)
        If armo.TemplateArmorFormID <> 0UI Then
            Dim resolved = ResolveTerminalArmorFormID(armo.TemplateArmorFormID, pluginManager, visited)
            If resolved <> 0UI Then Return resolved
        End If

        Return armoFormID
    End Function

    Private Sub SampleOutfitItem(formID As UInteger,
                                 pluginManager As PluginManager,
                                 visited As HashSet(Of UInteger),
                                 result As List(Of UInteger),
                                 warnings As List(Of String))
        If formID = 0UI OrElse visited.Contains(formID) Then Return

        Dim rec = pluginManager.GetRecord(formID)
        If rec Is Nothing Then
            If warnings IsNot Nothing Then warnings.Add($"Outfit item {formID:X8} missing")
            Return
        End If

        Select Case rec.Header.Signature
            Case "ARMO"
                Dim terminalID = ResolveTerminalArmorFormID(formID, pluginManager)
                If terminalID <> 0UI Then result.Add(terminalID)

            Case "LVLI"
                visited.Add(formID)
                SampleLeveledItem(formID, pluginManager, visited, result, warnings)
                visited.Remove(formID)

            Case Else
                If warnings IsNot Nothing Then warnings.Add($"Unsupported outfit item {rec.Header.Signature} [{formID:X8}]")
        End Select
    End Sub

    Private Sub SampleLeveledItem(lvliFormID As UInteger,
                                  pluginManager As PluginManager,
                                  visited As HashSet(Of UInteger),
                                  result As List(Of UInteger),
                                  warnings As List(Of String))
        Dim rec = pluginManager.GetRecord(lvliFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "LVLI" Then Return

        Dim lvli = RecordParsers.ParseLVLI(rec, pluginManager)

        ' Whole-list chance-none: la LVLI completa puede no contribuir (fiel al engine).
        If lvli.ChanceNone > 0 AndAlso NextPercent() < lvli.ChanceNone Then Return

        Dim usable = lvli.Entries.Where(Function(e) e.FormID <> 0UI).ToList()
        If usable.Count = 0 Then Return

        If lvli.UseAll Then
            For Each entry In usable
                SampleLeveledEntry(entry, pluginManager, visited, result, warnings, lvli.CalculateEachItemInCount)
            Next
        Else
            Dim entry = usable(NextIndex(usable.Count))
            SampleLeveledEntry(entry, pluginManager, visited, result, warnings, lvli.CalculateEachItemInCount)
        End If
    End Sub

    Private Sub SampleLeveledEntry(entry As LVLI_Entry,
                                   pluginManager As PluginManager,
                                   visited As HashSet(Of UInteger),
                                   result As List(Of UInteger),
                                   warnings As List(Of String),
                                   calculateEachItemInCount As Boolean)
        If entry.ChanceNone > 0 AndAlso NextPercent() < entry.ChanceNone Then Return

        Dim count As Integer = If(entry.Count = 0US, 1, CInt(entry.Count))
        If count <= 1 OrElse Not calculateEachItemInCount Then
            SampleOutfitItem(entry.FormID, pluginManager, visited, result, warnings)
        Else
            For i = 1 To count
                SampleOutfitItem(entry.FormID, pluginManager, visited, result, warnings)
            Next
        End If
    End Sub

    Private Function NextIndex(count As Integer) As Integer
        SyncLock _rngLock
            Return _rng.Next(count)
        End SyncLock
    End Function

    Private Function NextPercent() As Integer
        SyncLock _rngLock
            Return _rng.Next(100)
        End SyncLock
    End Function

End Module
