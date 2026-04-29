Imports System.Linq

''' <summary>Realización de un outfit: por cada ARMO terminal equipada, las keywords contextuales
''' que el outfit aportó al equiparla. Las keywords vienen de cada LVLI.LLKC en el camino del
''' INAM hacia el ARMO terminal — se acumulan hacia abajo en la cadena. Caso típico: outfit
''' Gunner Boss → LVLI con LLKC `if_tmp_armor_Heavy chance=100` → resuelve a ARMO Combat_Torso →
''' la ARMO recibe la keyword Heavy → CollectArmoCandidates busca su OBTS combination con keyword
''' match → aplica OMOD AddonIndex swap → renderiza addon Heavy.</summary>
Public Class OutfitArmorPick
    Public ArmoFormID As UInteger
    ''' <summary>Keywords con `Chance > 0` heredadas de los LLKC en el camino. Multiset por formid:
    ''' una keyword puede venir de varios LLKC anidados; el dedup se hace al buscar match.</summary>
    Public ContextKeywords As New List(Of UInteger)
End Class

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
'''   - Propaga LVLI.LLKC (Filter Keyword Chances) hacia el ARMO resuelto. Cada keyword con
'''     `Chance > 0` se pasa rolada (random comparado con chance) al ARMO. El consumer usa esas
'''     keywords para matchear OBTS combinations y aplicar OMOD AddonIndex swaps.
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

    ''' <summary>Sampla una realización de outfit con propagación de keywords. Devuelve la lista
    ''' de ARMO terminal con sus keywords contextuales heredadas del camino LLKC.</summary>
    Public Function SampleOutfitWithKeywords(otftFormID As UInteger,
                                             pluginManager As PluginManager,
                                             Optional warnings As List(Of String) = Nothing) As List(Of OutfitArmorPick)
        Dim picks As New List(Of OutfitArmorPick)
        If otftFormID = 0UI OrElse pluginManager Is Nothing Then Return picks

        Dim rec = pluginManager.GetRecord(otftFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "OTFT" Then
            If warnings IsNot Nothing Then warnings.Add($"Outfit {otftFormID:X8} missing or not OTFT")
            Return picks
        End If

        Dim otft = RecordParsers.ParseOTFT(rec, pluginManager)
        For Each itemFormID In otft.ItemFormIDs
            SampleOutfitItem(itemFormID, pluginManager, New HashSet(Of UInteger)(), picks, warnings, New List(Of UInteger))
        Next

        ' Dedup ARMO FormIDs preservando keywords (merge keyword lists si la misma ARMO aparece dos veces).
        Dim merged As New Dictionary(Of UInteger, OutfitArmorPick)
        For Each p In picks
            If merged.ContainsKey(p.ArmoFormID) Then
                For Each kw In p.ContextKeywords
                    If Not merged(p.ArmoFormID).ContextKeywords.Contains(kw) Then
                        merged(p.ArmoFormID).ContextKeywords.Add(kw)
                    End If
                Next
            Else
                merged(p.ArmoFormID) = p
            End If
        Next
        Return merged.Values.ToList()
    End Function

    ''' <summary>Compat: devuelve sólo la lista de FormIDs sin keywords contextuales. Los consumers
    ''' que necesiten resolver multi-addon (Lite/Mid/Heavy) deben usar SampleOutfitWithKeywords.</summary>
    Public Function SampleOutfitRealization(otftFormID As UInteger,
                                            pluginManager As PluginManager,
                                            Optional warnings As List(Of String) = Nothing) As List(Of UInteger)
        Return SampleOutfitWithKeywords(otftFormID, pluginManager, warnings).Select(Function(p) p.ArmoFormID).ToList()
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
                                 result As List(Of OutfitArmorPick),
                                 warnings As List(Of String),
                                 inheritedKeywords As List(Of UInteger))
        If formID = 0UI OrElse visited.Contains(formID) Then Return

        Dim rec = pluginManager.GetRecord(formID)
        If rec Is Nothing Then
            If warnings IsNot Nothing Then warnings.Add($"Outfit item {formID:X8} missing")
            Return
        End If

        Select Case rec.Header.Signature
            Case "ARMO"
                Dim terminalID = ResolveTerminalArmorFormID(formID, pluginManager)
                If terminalID <> 0UI Then
                    Dim pick As New OutfitArmorPick With {.ArmoFormID = terminalID}
                    pick.ContextKeywords.AddRange(inheritedKeywords)
                    result.Add(pick)
                End If

            Case "LVLI"
                visited.Add(formID)
                SampleLeveledItem(formID, pluginManager, visited, result, warnings, inheritedKeywords)
                visited.Remove(formID)

            Case Else
                If warnings IsNot Nothing Then warnings.Add($"Unsupported outfit item {rec.Header.Signature} [{formID:X8}]")
        End Select
    End Sub

    Private Sub SampleLeveledItem(lvliFormID As UInteger,
                                  pluginManager As PluginManager,
                                  visited As HashSet(Of UInteger),
                                  result As List(Of OutfitArmorPick),
                                  warnings As List(Of String),
                                  inheritedKeywords As List(Of UInteger))
        Dim rec = pluginManager.GetRecord(lvliFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "LVLI" Then Return

        Dim lvli = RecordParsers.ParseLVLI(rec, pluginManager)

        ' Whole-list chance-none: la LVLI completa puede no contribuir (fiel al engine).
        If lvli.ChanceNone > 0 AndAlso NextPercent() < lvli.ChanceNone Then Return

        Dim usable = lvli.Entries.Where(Function(e) e.FormID <> 0UI).ToList()
        If usable.Count = 0 Then Return

        ' Build the keyword set for descendants: inherited + LLKC of THIS LVLI.
        ' For each LLKC entry, roll Chance% to decide whether it propagates.
        Dim mergedKeywords As New List(Of UInteger)
        mergedKeywords.AddRange(inheritedKeywords)
        For Each fk In lvli.FilterKeywords
            If fk.KeywordFormID = 0UI Then Continue For
            ' Chance >= 100 = always; 0 = never; in between = roll.
            Dim include As Boolean
            If fk.Chance >= 100UI Then
                include = True
            ElseIf fk.Chance = 0UI Then
                include = False
            Else
                include = (NextPercent() < CInt(fk.Chance))
            End If
            If include AndAlso Not mergedKeywords.Contains(fk.KeywordFormID) Then
                mergedKeywords.Add(fk.KeywordFormID)
            End If
        Next

        If lvli.UseAll Then
            For Each entry In usable
                SampleLeveledEntry(entry, pluginManager, visited, result, warnings, lvli.CalculateEachItemInCount, mergedKeywords)
            Next
        Else
            Dim entry = usable(NextIndex(usable.Count))
            SampleLeveledEntry(entry, pluginManager, visited, result, warnings, lvli.CalculateEachItemInCount, mergedKeywords)
        End If
    End Sub

    Private Sub SampleLeveledEntry(entry As LVLI_Entry,
                                   pluginManager As PluginManager,
                                   visited As HashSet(Of UInteger),
                                   result As List(Of OutfitArmorPick),
                                   warnings As List(Of String),
                                   calculateEachItemInCount As Boolean,
                                   inheritedKeywords As List(Of UInteger))
        If entry.ChanceNone > 0 AndAlso NextPercent() < entry.ChanceNone Then Return

        Dim count As Integer = If(entry.Count = 0US, 1, CInt(entry.Count))
        If count <= 1 OrElse Not calculateEachItemInCount Then
            SampleOutfitItem(entry.FormID, pluginManager, visited, result, warnings, inheritedKeywords)
        Else
            For i = 1 To count
                SampleOutfitItem(entry.FormID, pluginManager, visited, result, warnings, inheritedKeywords)
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
