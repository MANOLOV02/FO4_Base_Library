Imports System.IO
Imports System.Linq
Imports System.Text

''' <summary>
''' Manages multiple plugins with load order, FormID resolution, and record override logic.
''' </summary>
Public Class PluginManager
    ''' <summary>All loaded plugins in load order.</summary>
    Public Property Plugins As New List(Of PluginReader)

    ''' <summary>Plugin name -> index in Plugins list (raw load position, counts ALL plugins).</summary>
    Private ReadOnly _pluginIndex As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    ' Engine-faithful FileID slots (xEdit TwbFileID.CreateFull / CreateLight): full plugins (ESM + full
    ' ESP) occupy the 0x00-0xFD high-byte space; light (ESL) plugins occupy the 0xFE light space, with a
    ' 12-bit light index in bits 12..23. WITHOUT this split, a full plugin loaded after N ESLs would get
    ' the wrong high byte (e.g. 0x3D instead of 0x0F), so its records' FormIDs wouldn't match the game /
    ' xEdit and the saved plugin's references would be mis-encoded. Built once during LoadAllPlugins.
    Private ReadOnly _fullSlotByName As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _lightSlotByName As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _nameByFullSlot As New Dictionary(Of Integer, String)
    Private ReadOnly _nameByLightSlot As New Dictionary(Of Integer, String)

    Private _localizedStrings As LocalizedStringResolver

    ' Guards the record / plugin / FileID-slot collections (AllRecords, RecordsByType, Plugins,
    ' _pluginIndex and the four slot dicts) against concurrent read+write. Multiple reader threads
    ' (overlapping preview renders and FaceGen bakes run their record lookups on Task.Run background
    ' threads) read concurrently under the read lock; the only post-load mutation, MergeOverridePlugin
    ' (Save read-back, on the UI thread), takes the write lock — so a reader can never observe a
    ' half-rebuilt RecordsByType (BuildTypeIndex does Clear()+repopulate) or a torn slot reassignment.
    ' SupportsRecursion so a public reader may call another public reader, and MergeRecords (running
    ' under the write lock) may call the public ResolveFormID, without deadlocking.
    Private ReadOnly _rwLock As New System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.SupportsRecursion)

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

        _rwLock.EnterWriteLock()
        Try
            For i = 0 To pluginFiles.Count - 1
                Dim filePath = pluginFiles(i)
                Dim fileName = Path.GetFileName(filePath)
                progress?.Report($"Loading {fileName} ({i + 1}/{pluginFiles.Count})")
                Try
                    Dim reader As New PluginReader()
                    reader.Load(filePath)
                    IndexAndMergePlugin(reader)
                Catch ex As Exception
                    Logger.LogLazy(Function() $"[ESP] Failed to load {fileName}: {ex.Message}")
                End Try
            Next

            BuildTypeIndex()
        Finally
            _rwLock.ExitWriteLock()
        End Try
    End Sub

    ''' <summary>Append a loaded <see cref="PluginReader"/> as the next plugin in load order:
    ''' record it in the name→index map, assign its engine-faithful FileID slot, and merge its
    ''' records. Shared by <see cref="LoadAllPlugins"/> (batched: caller runs BuildTypeIndex once
    ''' at the end) and <see cref="MergeOverridePlugin"/> (which rebuilds the type index itself).
    ''' Slot assignment is done BEFORE MergeRecords so this plugin's own records (self-refs)
    ''' resolve via its just-assigned slot, and master-refs resolve via earlier plugins' slots.</summary>
    Private Sub IndexAndMergePlugin(reader As PluginReader)
        _pluginIndex(reader.FileName) = Plugins.Count
        Plugins.Add(reader)
        AssignFileIdSlot(reader)
        MergeRecords(reader)
    End Sub

    ''' <summary>Assign the engine-faithful FileID slot for a plugin: ESL → next light slot, full
    ''' (ESM/ESP) → next full slot. Done BEFORE MergeRecords so self-refs resolve via this slot.
    ''' Uses max-index+1 (not dict.Count) so it stays correct if a prior re-slot left a gap; at load
    ''' time the dicts are dense so this equals Count (no behaviour change).</summary>
    Private Sub AssignFileIdSlot(reader As PluginReader)
        If reader.IsESL Then
            Dim ls = NextSlotIndex(_nameByLightSlot)
            _lightSlotByName(reader.FileName) = ls
            _nameByLightSlot(ls) = reader.FileName
        Else
            Dim fsx = NextSlotIndex(_nameByFullSlot)
            _fullSlotByName(reader.FileName) = fsx
            _nameByFullSlot(fsx) = reader.FileName
        End If
    End Sub

    ''' <summary>Next free slot index for a name-by-slot dict: max occupied index + 1 (0 when empty).
    ''' Gap-safe so re-slotting (DropSlotAssignment) never reuses a vacated index.</summary>
    Private Shared Function NextSlotIndex(nameBySlot As Dictionary(Of Integer, String)) As Integer
        Dim maxIdx As Integer = -1
        For Each k In nameBySlot.Keys
            If k > maxIdx Then maxIdx = k
        Next
        Return maxIdx + 1
    End Function

    ''' <summary>Remove a plugin's slot assignment from BOTH the full and light dicts (by name). Used
    ''' before re-assigning a slot when a plugin is re-mounted with a flipped ESM/ESL flag.</summary>
    Private Sub DropSlotAssignment(name As String)
        Dim f As Integer
        If _fullSlotByName.TryGetValue(name, f) Then
            _fullSlotByName.Remove(name)
            _nameByFullSlot.Remove(f)
        End If
        Dim l As Integer
        If _lightSlotByName.TryGetValue(name, l) Then
            _lightSlotByName.Remove(name)
            _nameByLightSlot.Remove(l)
        End If
    End Sub

    ''' <summary>Mount an already-written plugin file at runtime as the top (last-wins) override,
    ''' so <see cref="GetRecord"/> / <see cref="GetRecordsOfType"/> immediately reflect its records
    ''' — the same picture the engine would show if the plugin loaded last in the load order. Used
    ''' by NPC_Manager after Save ESP to re-read the just-saved NPC override without reloading the
    ''' whole load order.
    '''
    ''' <para>New plugin name → appended as the last full/light FileID slot, records merged (last
    ''' override wins). Already-loaded name (e.g. a second save to the same auto-gen plugin in the
    ''' same session) → its reader is replaced in place and its FileID slot is re-derived from the
    ''' new reader's ESM/ESL flag (so flipping Light-master between saves re-encodes its FormIDs
    ''' correctly instead of leaving a stale full/light slot), then its records are re-merged as the
    ''' top override. Always rebuilds the type index so GetRecordsOfType reflects the swapped record
    ''' references. Returns the loaded reader.</para>
    '''
    ''' <para>Invariant: the plugin is treated as the winning override. For NPC_Manager's auto-gen
    ''' plugins this holds (we just wrote it; it is conceptually last). Mounting a plugin that other
    ''' active plugins override is out of scope — the caller's record wins, which matches the
    ''' "show me what I just saved" preview intent.</para></summary>
    Public Function MergeOverridePlugin(filePath As String) As PluginReader
        Dim reader As New PluginReader()
        reader.Load(filePath)
        _rwLock.EnterWriteLock()
        Try
            Dim existingIdx As Integer = -1
            If _pluginIndex.TryGetValue(reader.FileName, existingIdx) Then
                ' Re-save to a plugin already loaded this session: swap the reader, re-derive the FileID
                ' slot (handles a flipped ESM/ESL flag — the slot dicts are name-keyed, so a stale
                ' full-slot entry for a now-ESL plugin would mis-encode its FormIDs), then re-merge.
                Plugins(existingIdx) = reader
                DropSlotAssignment(reader.FileName)
                AssignFileIdSlot(reader)
                MergeRecords(reader)
            Else
                IndexAndMergePlugin(reader)
            End If
            BuildTypeIndex()
        Finally
            _rwLock.ExitWriteLock()
        End Try
        Return reader
    End Function

    ''' <summary>Resolve a file-local FormID to a global FormID using the plugin's master list. The
    ''' global high byte follows the engine FileID scheme — full plugins use (fullSlot &lt;&lt; 24);
    ''' light (ESL) plugins use the 0xFE light space — so it matches the game / xEdit even when ESLs
    ''' precede the owner in load order.</summary>
    Public Function ResolveFormID(localFormID As UInteger, plugin As PluginReader) As UInteger
        _rwLock.EnterReadLock()
        Try
            Dim masterIndex = CInt(localFormID >> 24)
            Dim objectID = localFormID And &HFFFFFFUI

            Dim owner As PluginReader = Nothing
            If masterIndex < plugin.Masters.Count Then
                ' Reference into one of this plugin's masters. The master index is full-style (xEdit
                ' LoadOrderFileIDtoFileFileID emits CreateFull(i) even when the master is an ESL).
                Dim masterName = plugin.Masters(masterIndex)
                Dim mi As Integer = -1
                If _pluginIndex.TryGetValue(masterName, mi) Then owner = Plugins(mi)
            Else
                owner = plugin   ' self record (master index == master count)
            End If

            If owner Is Nothing Then Return localFormID   ' unresolved master — best effort
            Return MakeGlobalFormID(owner, objectID)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Build the global FormID for a record owned by <paramref name="owner"/>: full plugins →
    ''' (fullSlot &lt;&lt; 24) | object24; ESL plugins → 0xFE | (lightSlot &lt;&lt; 12) | object12.</summary>
    Private Function MakeGlobalFormID(owner As PluginReader, objectID As UInteger) As UInteger
        If owner.IsESL Then
            Dim L As Integer = 0
            _lightSlotByName.TryGetValue(owner.FileName, L)
            Return &HFE000000UI Or (CUInt(L) << 12) Or (objectID And &HFFFUI)
        End If
        Dim F As Integer = 0
        _fullSlotByName.TryGetValue(owner.FileName, F)
        Return (CUInt(F) << 24) Or (objectID And &HFFFFFFUI)
    End Function

    ''' <summary>Resolve a referenced FormID using the source plugin that owns the record.</summary>
    Public Function ResolveReferencedFormID(sourcePluginName As String, localFormID As UInteger) As UInteger
        If localFormID = 0UI Then Return 0UI
        If String.IsNullOrWhiteSpace(sourcePluginName) Then Return localFormID

        _rwLock.EnterReadLock()
        Try
            Dim pluginIdx As Integer = -1
            If Not _pluginIndex.TryGetValue(sourcePluginName, pluginIdx) Then Return localFormID
            If pluginIdx < 0 OrElse pluginIdx >= Plugins.Count Then Return localFormID

            Return ResolveFormID(localFormID, Plugins(pluginIdx))
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Combine a LooksMenu-style "Plugin|FormID" identifier back into a global FormID. The
    ''' identifier's FormID part is the runtime FormID masked to 24 bits (Utilities.cpp:112
    ''' <c>formID &amp; 0xFFFFFF</c>) — for an ESL it already carries the 12-bit light-slot in bits 12..23,
    ''' so the global is just 0xFE | local; for a full plugin the global is (fullSlot &lt;&lt; 24) | local.
    ''' Returns 0 when the named plugin isn't loaded. Inverse of <c>LooksmenuLoader.FormatFormIdentifier</c>.</summary>
    Public Function GlobalFormIDFromIdentifierLocal(pluginName As String, identifierLocal As UInteger) As UInteger
        _rwLock.EnterReadLock()
        Try
            Dim idx As Integer
            If String.IsNullOrEmpty(pluginName) OrElse Not _pluginIndex.TryGetValue(pluginName, idx) Then Return 0UI
            Dim p = Plugins(idx)
            If p.IsESL Then Return &HFE000000UI Or (identifierLocal And &HFFFFFFUI)
            Dim f As Integer = 0
            _fullSlotByName.TryGetValue(p.FileName, f)
            Return (CUInt(f) << 24) Or (identifierLocal And &HFFFFFFUI)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Plugin occupying a given FULL FileID slot (high byte 0x00..0xFD). For light (ESL)
    ''' plugins use the 0xFE light path in <see cref="GetOriginatingPluginName"/>. "" if no full plugin
    ''' occupies that slot.</summary>
    Public Function GetPluginNameByLoadOrderIndex(fullSlot As Integer) As String
        _rwLock.EnterReadLock()
        Try
            Dim nm As String = Nothing
            If _nameByFullSlot.TryGetValue(fullSlot, nm) Then Return nm
            Return ""
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Resolve the master plugin that "owns" a FormID for engine-faithful asset
    ''' resolution (e.g. FaceGen path lookup). Critical: this must NOT return the override
    ''' plugin in cases where the FormID has been overridden — the engine resolves FaceGen
    ''' under the master's path regardless of overrides. Two cases:
    '''
    ''' 1) Full slot (high byte 0x00..0xFD): the high byte is the load-order index of the
    '''    master, period. Doesn't matter who overrides it; the high byte itself encodes
    '''    the master.
    ''' 2) Light slot (high byte 0xFE): the FormID encodes a light-slot index in bits
    '''    12..23 (0xFExxxYYY where xxx = ESL slot, YYY = ObjectID). The master is the
    '''    N-th plugin with the ESL flag set, in load order — same algorithm xEdit uses
    '''    [TwbFile.LoadOrderFileIDtoFileFileID, wbImplementation.pas:3441-3444].
    '''
    ''' Returns "" when the FormID's slot can't be resolved (load order doesn't have a
    ''' plugin in that position, or fewer ESLs than the slot index demands).</summary>
    Public Function GetOriginatingPluginName(formID As UInteger) As String
        _rwLock.EnterReadLock()
        Try
            Dim highByte = CInt((formID >> 24) And &HFFUI)
            If highByte = &HFE Then
                ' Light slot: lightSlot = bits 12..23 → the lightSlot-th ESL plugin (built at load time).
                Dim lightSlot = CInt((formID >> 12) And &HFFFUI)
                Dim nm As String = Nothing
                If _nameByLightSlot.TryGetValue(lightSlot, nm) Then Return nm
                Return ""
            End If
            Return GetPluginNameByLoadOrderIndex(highByte)
        Finally
            _rwLock.ExitReadLock()
        End Try
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

        ' Per-file translatable encoding (from TES4 SNAM <cp:XXXX>) takes precedence over the
        ' global PluginEncodingSettings.Translatable. Mirror of bsdGetEncoding precedence
        ' (wbInterface.pas:23519-23535): aElement._File.Encoding[translatable] beats wbEncodingTrans.
        If rec IsNot Nothing AndAlso rec.SourcePluginTranslatableEncoding IsNot Nothing Then
            Dim len = sr.Data.Length
            If len > 0 AndAlso sr.Data(len - 1) = 0 Then len -= 1
            Return PluginEncodingSettings.DecodeWithEncoding(sr.Data, 0, len, rec.SourcePluginTranslatableEncoding)
        End If

        Return sr.AsString
    End Function

    ''' <summary>Get the final resolved record for a FormID (after overrides).</summary>
    Public Function GetRecord(formID As UInteger) As PluginRecord
        _rwLock.EnterReadLock()
        Try
            Dim rec As PluginRecord = Nothing
            AllRecords.TryGetValue(formID, rec)
            Return rec
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Get all records of a specific type.</summary>
    Public Function GetRecordsOfType(sig As String) As List(Of PluginRecord)
        _rwLock.EnterReadLock()
        Try
            Dim result As List(Of PluginRecord) = Nothing
            If RecordsByType.TryGetValue(sig, result) Then Return result
            Return New List(Of PluginRecord)
        Finally
            _rwLock.ExitReadLock()
        End Try
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

        ' Use loadorder.txt as ordering source if available (LOOT/Vortex managed). Implicit masters
        ' (game .esm + DLCs) and Creation Club entries are FORCE-loaded by the engine in their
        ' canonical order regardless of what loadorder.txt says — LOOT/Vortex listings for those
        ' are advisory only and the engine ignores them. We must replicate that to keep FormID high
        ' bytes aligned with the runtime engine; otherwise a `loadorder.txt` that places any plugin
        ' before Fallout4.esm would shove the game master to slot 1+, desyncing every FormID
        ' diagnostic / clipboard helper / FaceGen path lookup that depends on the high byte.
        Dim loadorderTxt = Path.Combine(appData, appDataSubdir, "loadorder.txt")
        Dim ordered As New List(Of String)
        If File.Exists(loadorderTxt) Then
            Dim implicitsSet As New HashSet(Of String)(implicits, StringComparer.OrdinalIgnoreCase)
            Dim ccSet As New HashSet(Of String)(ccEntries, StringComparer.OrdinalIgnoreCase)

            ' 1) Implicits at the front, in the hardcoded engine order.
            ordered.AddRange(implicits)

            ' 2) Creation Club entries next, skipping any that overlap with implicits.
            For Each p In ccEntries
                If implicitsSet.Contains(p) Then Continue For
                ordered.Add(p)
            Next

            ' 3) Everything else from loadorder.txt, in its order, skipping implicits + CC (already
            '    placed above) and inactive plugins (must also be in Plugins.txt with `*`).
            For Each line In File.ReadAllLines(loadorderTxt, Encoding.UTF8)
                Dim trimmed = line.Trim()
                If trimmed.Length = 0 Then Continue For
                If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                If trimmed.StartsWith("*") Then trimmed = trimmed.Substring(1).Trim()
                If trimmed.Length = 0 Then Continue For
                If Not activeSet.Contains(trimmed) Then Continue For
                If implicitsSet.Contains(trimmed) Then Continue For
                If ccSet.Contains(trimmed) Then Continue For
                If ordered.Any(Function(x) String.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)) Then Continue For
                ordered.Add(trimmed)
            Next

            ' 4) Fallback for actives in Plugins.txt that loadorder.txt didn't list (rare edge:
            '    just-installed plugin not yet sorted by LOOT). Append at the end.
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


