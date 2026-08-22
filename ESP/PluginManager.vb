Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks

''' <summary>
''' Manages multiple plugins with load order, FormID resolution, and record override logic.
''' </summary>
Public Class PluginManager
    ''' <summary>All loaded plugins in load order.</summary>
    Public Property Plugins As New List(Of PluginReader)

    ''' <summary>Plugin name -> index in Plugins list (raw load position, counts ALL plugins).</summary>
    Private ReadOnly _pluginIndex As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    ' Engine-faithful FileID slots: full plugins (ESM + full
    ' ESP) occupy the 0x00-0xFD high-byte space; light (ESL) plugins occupy the 0xFE light space, with a
    ' 12-bit light index in bits 12..23. WITHOUT this split, a full plugin loaded after N ESLs would get
    ' the wrong high byte (e.g. 0x3D instead of 0x0F), so its records' FormIDs wouldn't match the game
    ' and the saved plugin's references would be mis-encoded. Built once during LoadAllPlugins.
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

    ''' <summary>For a FormID an app-authored (NPC_Manager) plugin OVERRIDES, the record that was WINNING right
    ''' BEFORE the app override — i.e. the LAST non-app override in load order (NOT the base master; if ModA then ModB
    ''' both override a Fallout4.esm record, this holds ModB's version). Lets <see cref="RevertAppOverride"/> restore
    ''' exactly what the game would show with the app plugin absent, IN MEMORY, since <see cref="AllRecords"/> keeps
    ''' only the winning (app override) record. Captured in MergeRecords the FIRST time an app plugin overrides a
    ''' FormID: app plugins load LAST, so at that instant AllRecords[fid] already holds the winning non-app version;
    ''' the ContainsKey guard then keeps it (a 2nd app plugin won't clobber it with an app record). A FormID the app
    ''' CREATED NEW has no entry here → revert removes it entirely.</summary>
    Private ReadOnly _recordBeforeAppOverride As New Dictionary(Of UInteger, PluginRecord)

    ''' <summary>Records grouped by signature type.</summary>
    Public Property RecordsByType As New Dictionary(Of String, List(Of PluginRecord))

    ''' <summary>Los plugins que el último <see cref="LoadAllPlugins"/> dejó FUERA porque les falta un
    ''' master (o porque se lo dejó fuera a un master suyo, transitivamente). Vacía cuando cargó todo.
    ''' <para>Existe para que la UI pueda decirlo: excluir en silencio es el modo de falla mudo que este
    ''' camino viene a eliminar, y el log solo no lo ve el usuario final (Release no escribe log).</para></summary>
    Public Property LastExcludedForMissingMasters As New List(Of String)

    ''' <summary>Load only ACTIVATED plugins from the Fallout 4 Data path. Order source priority:
    ''' 1) loadorder.txt (LOOT/Vortex managed; full ordered list with implicits + actives).
    ''' 2) Plugins.txt with `*activated` markers + hardcoded implicits prepended.
    ''' Plugins NOT in the active set are ignored (no Data folder scan). Replicates engine load:
    ''' un .esp suelto en Data sin estar activado NO se carga in-game; tampoco acá.</summary>
    Public Sub LoadAllPlugins(dataPath As String, Optional progress As IProgress(Of PluginLoadProgress) = Nothing)
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
                              Optional progress As IProgress(Of PluginLoadProgress) = Nothing,
                              Optional sigFilter As HashSet(Of String) = Nothing)
        Dim pluginFiles As New List(Of String)
        Dim fileSizes As New List(Of Long)

        _localizedStrings = New LocalizedStringResolver(dataPath)

        Dim bytesTotal As Long = 0
        For Each pluginName In pluginsToLoad
            Dim fullPath = Path.Combine(dataPath, pluginName)
            If File.Exists(fullPath) Then
                Dim len As Long = New FileInfo(fullPath).Length
                pluginFiles.Add(fullPath)
                fileSizes.Add(len)
                bytesTotal += len
            End If
        Next

        Dim n = pluginFiles.Count

        ' ---- Fan-out parse (parallel, NO shared PluginManager state touched) ----
        ' Each plugin is parsed into its own PluginReader (reader.Records / .Masters / flags are 100%
        ' per-reader; PluginReader.Load opens its own FileStream). Results land in a pre-sized array indexed
        ' by LOAD-ORDER position so the merge below can replay them in exactly the sequential order. A failed
        ' parse leaves readers(i) = Nothing (and logs). Nothing here writes Plugins /
        ' AllRecords / the slot dicts, so this runs BEFORE taking the write lock.
        Dim readers(Math.Max(0, n - 1)) As PluginReader
        Dim bytesDone As Long = 0
        Dim filesDone As Integer = 0
        ' Per-reader last-reported absolute position, for translating absolute → delta under Interlocked.Add.
        Dim lastPos(Math.Max(0, n - 1)) As Long

        ' DOP capped at ProcessorCount (not unbounded) — parse is CPU/IO bound; more threads than cores just
        ' thrashes. Parallel.For preserves the index i, so load order is never lost. (n = 0 → no-op.)
        Dim parallelOpts As New ParallelOptions With {.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)}

        Parallel.For(0, n, parallelOpts,
            Sub(i)
                Dim filePath = pluginFiles(i)
                Dim fileName = Path.GetFileName(filePath)
                Dim sizeI = fileSizes(i)
                Try
                    ' Per-reader byte-progress: a SYNCHRONOUS callback (NOT New Progress(Of Long)) so it runs
                    ' inline on THIS i's parse thread — lastPos(i) stays genuinely single-threaded and the only
                    ' cross-thread state is bytesDone (Interlocked). A Progress(Of Long) built here would have no
                    ' UI SynchronizationContext and post the handler to the thread pool, running it concurrently/
                    ' out-of-order and racing lastPos(i). The outer `progress` (PluginLoadProgress) DOES marshal
                    ' to the UI — it was created by the app with New Progress — so .Report from here is safe.
                    Dim bp As Action(Of Long) = Nothing
                    If progress IsNot Nothing Then
                        bp = Sub(absPos)
                                 Dim delta = absPos - lastPos(i)
                                 If delta <= 0 Then Return
                                 lastPos(i) = absPos
                                 Dim bd = Interlocked.Add(bytesDone, delta)
                                 progress.Report(New PluginLoadProgress With {
                                     .FilesDone = Volatile.Read(filesDone),
                                     .FilesTotal = n,
                                     .BytesDone = bd,
                                     .BytesTotal = bytesTotal,
                                     .CurrentName = fileName
                                 })
                             End Sub
                    End If

                    Dim reader As New PluginReader(sigFilter)
                    reader.Load(filePath, bp)
                    readers(i) = reader
                Catch ex As Exception
                    Logger.LogLazy(Function() $"[ESP] Failed to load {fileName}: {ex.Message}")
                Finally
                    ' On completion add this plugin's remaining bytes (so BytesDone is monotonic and ends ==
                    ' BytesTotal whether the parse threw or finished mid-file) and bump the file count.
                    Dim remaining = sizeI - lastPos(i)
                    If remaining > 0 Then Interlocked.Add(bytesDone, remaining)
                    Dim fd = Interlocked.Increment(filesDone)
                    progress?.Report(New PluginLoadProgress With {
                        .FilesDone = fd,
                        .FilesTotal = n,
                        .BytesDone = Volatile.Read(bytesDone),
                        .BytesTotal = bytesTotal,
                        .CurrentName = fileName
                    })
                End Try
            End Sub)

        ' ---- Dependency ordering, BEFORE taking the write lock so a bad set is resolved without mutating ----
        ' Los plugins con masters faltantes (y sus dependientes, transitivamente) quedan FUERA en vez de
        ' tumbar la carga entera — ver OrderByMasters. Se nombran en el log: excluir en silencio sería el
        ' mismo modo de falla mudo que esto viene a sacar.
        Dim excludedForMissingMasters As List(Of String) = Nothing
        Dim mergeOrder = OrderByMasters(readers, excludedForMissingMasters)
        If excludedForMissingMasters IsNot Nothing AndAlso excludedForMissingMasters.Count > 0 Then
            Dim names = String.Join(", ", excludedForMissingMasters)
            Dim n2 = excludedForMissingMasters.Count
            Logger.LogLazy(Function() $"[ESP] {n2} plugin(s) NOT loaded: a master they require is missing, " &
                                      $"so their FormIDs could not be resolved — {names}. " &
                                      "The rest of the load order was loaded normally.")
        End If
        LastExcludedForMissingMasters = If(excludedForMissingMasters, New List(Of String))

        ' ---- Fan-in merge (sequential, load order 0..N-1, under the write lock) ----
        ' Replaying IndexAndMergePlugin in order preserves: FileID slot assignment order, last-override-wins,
        ' and the order AllRecords / RecordsByType are populated — so the parallel fan-out above cannot
        ' change the resolved record set.
        _rwLock.EnterWriteLock()
        Try
            For Each r In mergeOrder
                IndexAndMergePlugin(r)
            Next

            BuildTypeIndex()
        Finally
            _rwLock.ExitWriteLock()
        End Try
    End Sub


    ''' <summary>Order the parsed plugins so every master precedes the file that declares it.
    ''' <para>This is what makes resolution correct at all: <see cref="MergeRecords"/> resolves each
    ''' record AS IT MERGES, against the index built so far. A plugin merged before one of its masters
    ''' therefore resolves every reference it owns against a master list that is not in the index yet,
    ''' gets the file-local FormID back unchanged, and files its records under the key of whatever
    ''' plugin happens to own that slot — silently overwriting a third party's record and hiding its
    ''' own. The load set alone does not prevent this: the Preflight validates MEMBERSHIP (all masters
    ''' ticked) but not ORDER, and it lists inactive plugins alphabetically after the active ones, so
    ''' its own "Check Masters" button can tick a master into a position AFTER its dependent.</para>
    ''' <para>El algoritmo recorre cada master ANTES de asignarle a un módulo su propio load order y su
    ''' FileID, y lanza sobre un master que no encuentra o sobre una referencia circular: un conjunto que
    ''' no se puede ordenar es un conjunto que no se puede resolver, y seguir adelante sobre una
    ''' suposición es justamente el modo de falla que esto viene a sacar. Los llamadores que
    ''' deliberadamente cargan un subconjunto parcial (probes, CLI) tienen que listar los masters de los
    ''' que dependen.</para>
    ''' <para>STABLE by construction: visiting in the caller's order and appending post-order yields the
    ''' IDENTITY permutation whenever the input is already correctly ordered, so a valid load order is
    ''' never reshuffled and cannot change which override wins.</para></summary>
    Private Shared Function OrderByMasters(readers As PluginReader(),
                                           ByRef excludedForMissingMasters As List(Of String)) As List(Of PluginReader)
        Dim byName As New Dictionary(Of String, PluginReader)(StringComparer.OrdinalIgnoreCase)
        For Each r In readers
            ' readers(i) = Nothing is a plugin whose parse failed; it was already logged and dropped.
            If r IsNot Nothing AndAlso Not byName.ContainsKey(r.FileName) Then byName(r.FileName) = r
        Next

        ' ---- Fase 1: marcar los módulos con masters faltantes y PROPAGAR a sus dependientes ----
        ' La MECÁNICA: marcar los módulos con un master faltante, propagar esa marca con un punto fijo
        ' sobre sus dependientes, y excluir del recorrido todo lo marcado, de modo que sólo se recorre lo
        ' que quedó. El throw queda SÓLO para el ciclo.
        '
        ' EL PREDICADO NO ES EL MISMO que el de otras herramientas de edición de plugins, y hay que
        ' decirlo: para ellas "falta el master" significa NO EXISTE EN Data\, mirando la carpeta entera.
        ' Acá significa "no está entre los plugins que se están cargando". La diferencia aparece con un
        ' master INSTALADO pero DESTILDADO: esas herramientas no lo marcan como faltante y encima lo
        ' CARGAN igual, porque su recorrido de dependencias sigue los masters de un módulo sin mirar si
        ' están activados — ese filtro existe sólo en el bucle raíz de esas herramientas.
        ' Ese comportamiento NO se replica acá, y no por preferencia: esas herramientas están pensadas
        ' para MODELADO y el MOTOR hace lo contrario — un plugin cuyo master no está activo no se carga
        ' in-game.
        ' Nuestro espacio de slots tiene que espejar el del juego (es lo que hace que cada FormID que
        ' resolvemos signifique lo mismo que en runtime), así que arrastrar un plugin no seleccionado
        ' correría el FileID de todo lo que viene después y desalinearía la sesión entera respecto de la
        ' selección del Preflight y del load order real. Manda el motor, no la herramienta de edición.
        ' El `raise` sobre un ciclo es una ASERCIÓN sobre ese conjunto ya filtrado, no la política ante un
        ' master colgado. Abortar la carga entera por un plugin roto se apartaba del MOTOR —que tampoco
        ' carga el dependiente, pero sí todo lo demás— y encima contradecía lo que esta misma función ya
        ' hace con los otros dos modos de falla: un archivo ausente se saltea mudo
        ' (LoadAllPlugins, el File.Exists del fan-out) y un parseo fallido también (readers(i) = Nothing).
        ' Un patch activo cuyo master quedó desinstalado es un estado corriente de modding; convertirlo en
        ' "no carga NADA" rompía el bake-all y el CLI, que no pasan por el gate del Preflight.
        Dim broken As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each r In readers
            If r Is Nothing Then Continue For
            For Each m In r.Masters
                If Not byName.ContainsKey(m) Then
                    broken.Add(r.FileName)
                    Exit For
                End If
            Next
        Next
        Dim changed As Boolean = True
        While changed
            changed = False
            For Each r In readers
                If r Is Nothing OrElse broken.Contains(r.FileName) Then Continue For
                For Each m In r.Masters
                    If broken.Contains(m) Then
                        broken.Add(r.FileName)
                        changed = True
                        Exit For
                    End If
                Next
            Next
        End While
        excludedForMissingMasters = broken.OrderBy(Function(x) x, StringComparer.OrdinalIgnoreCase).ToList()

        Dim ordered As New List(Of PluginReader)
        Const VISITING As Integer = 1, DONE As Integer = 2
        Dim state As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Dim path As New List(Of String)

        Dim visit As Action(Of PluginReader) = Nothing
        visit = Sub(r)
                    Dim st As Integer
                    If state.TryGetValue(r.FileName, st) Then
                        If st = DONE Then Return
                        Throw New InvalidDataException(
                            "Circular master reference between plugins: " &
                            String.Join(" -> ", path) & " -> " & r.FileName &
                            ". No load order can satisfy this, so none is guessed at.")
                    End If
                    state(r.FileName) = VISITING
                    path.Add(r.FileName)
                    For Each m In r.Masters
                        ' Fase 1 garantiza que todo master de un plugin NO roto está en byName.
                        Dim master As PluginReader = Nothing
                        If Not byName.TryGetValue(m, master) Then Continue For
                        visit(master)
                    Next
                    path.RemoveAt(path.Count - 1)
                    state(r.FileName) = DONE
                    ordered.Add(r)
                End Sub

        For Each r In readers
            ' Los marcados en fase 1 no entran: quedan excluidos del recorrido antes de resolver el orden,
            ' así que nunca se visitan.
            If r IsNot Nothing AndAlso Not broken.Contains(r.FileName) Then visit(r)
        Next
        Return ordered
    End Function

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
    ''' time the dicts are dense so this equals Count.</summary>
    Private Sub AssignFileIdSlot(reader As PluginReader)
        If reader.IsESL Then
            Dim ls = NextSlotIndex(_nameByLightSlot)
            If ls > MAX_LIGHT_SLOT Then Throw SlotSpaceExhausted(reader, ls, isLight:=True)
            _lightSlotByName(reader.FileName) = ls
            _nameByLightSlot(ls) = reader.FileName
        Else
            Dim fsx = NextSlotIndex(_nameByFullSlot)
            If fsx > MAX_FULL_SLOT Then Throw SlotSpaceExhausted(reader, fsx, isLight:=False)
            _fullSlotByName(reader.FileName) = fsx
            _nameByFullSlot(fsx) = reader.FileName
        End If
    End Sub

    ''' <summary>Highest usable FULL slot. 0xFE is the LIGHT marker and 0xFF is reserved, so the full
    ''' space stops at 0xFD on both games: the top of the full range decrements once because light
    ''' modules are supported.</summary>
    Private Const MAX_FULL_SLOT As Integer = &HFD

    ''' <summary>Highest usable LIGHT slot: the light index is the 12 bits at 12..23 of a 0xFE FormID,
    ''' so 0xFFF, the largest value that fits in that 12-bit field.</summary>
    Private Const MAX_LIGHT_SLOT As Integer = &HFFF

    ''' <summary>The exception for running out of FileID slots. Without this check the overflow is SILENT
    ''' and total: a full slot of 0xFE would make every FormID of that plugin read back as a LIGHT FormID
    ''' (0xFE is the light marker), and a light slot above 0xFFF would shift straight through the 12-bit
    ''' field into the high byte and destroy the marker itself. This mirrors the engine's own hard cap on
    ''' plugin count — 'too many light modules' / 'too many full modules'.</summary>
    Private Shared Function SlotSpaceExhausted(reader As PluginReader, slot As Integer, isLight As Boolean) As InvalidOperationException
        Return New InvalidOperationException(
            $"Too many {If(isLight, "light", "full")} plugins: '{reader.FileName}' would need " &
            $"{If(isLight, "light", "full")} slot {slot}, past the maximum of " &
            $"{If(isLight, MAX_LIGHT_SLOT, MAX_FULL_SLOT)}. Load fewer plugins — going past this point " &
            "silently corrupts every FormID the plugin owns.")
    End Function

    ''' <summary>Next free slot index for a name-by-slot dict: max occupied index + 1 (0 when empty).
    ''' Gap-safe so re-slotting (DropSlotAssignment) never reuses a vacated index.</summary>
    Private Shared Function NextSlotIndex(nameBySlot As Dictionary(Of Integer, String)) As Integer
        Dim maxIdx As Integer = -1
        For Each k In nameBySlot.Keys
            If k > maxIdx Then maxIdx = k
        Next
        Return maxIdx + 1
    End Function

    ''' <summary>Drop every record OWNED by the given FileID slot from <see cref="AllRecords"/>, i.e.
    ''' the self records of whichever plugin holds that slot. Overrides that plugin carries of another
    ''' file's records are keyed by the MASTER's global FormID, not by this slot, so they are untouched.
    ''' Caller must already hold the write lock.</summary>
    Private Sub PurgeRecordsOwnedBySlot(isLight As Boolean, slot As Integer)
        Dim stale As New List(Of UInteger)
        For Each k In AllRecords.Keys
            ' 0xFE is the light-space marker, never a full slot — test it explicitly in BOTH branches
            ' so a full slot that happened to equal 254 could not sweep the whole light space.
            If isLight Then
                If (k >> 24) = &HFEUI AndAlso CInt((k >> 12) And &HFFFUI) = slot Then stale.Add(k)
            ElseIf (k >> 24) <> &HFEUI AndAlso CInt(k >> 24) = slot Then
                stale.Add(k)
            End If
        Next
        For Each k In stale
            AllRecords.Remove(k)
        Next
    End Sub

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
                ' Re-save to a plugin already loaded this session. Do NOT unconditionally re-slot.
                ' DropSlotAssignment + AssignFileIdSlot hands out NextSlotIndex = max+1, so a plugin
                ' that was not the LAST of its slot space gets a brand new slot and every one of its
                ' self records changes global FormID. Measured consequences: the post-save readback
                ' looks up the pre-save record under the old key; AllRecords keeps a stale duplicate;
                ' GetOriginatingPluginName("old") returns "" so the sidecar row is written under
                ' "Unknown.esp"; and a SECOND save in the same session writes its rows under yet
                ' another key, so on reopen the second save's morphs are silently lost.
                ' A slot is a property of load order, not of when the file happens to be re-mounted.
                Dim nm = reader.FileName
                Dim oldLightSlot As Integer, oldFullSlot As Integer
                Dim hadLight = _lightSlotByName.TryGetValue(nm, oldLightSlot)
                Dim hadFull = _fullSlotByName.TryGetValue(nm, oldFullSlot)

                ' Drop the previous mount's OWN records first. Self records are filed under THIS
                ' plugin's slot (an override of a master's record is filed under the MASTER's key and
                ' must survive), so this removes exactly what the re-merge is about to restate — and
                ' with it any record the user DELETED from the plugin between saves, which would
                ' otherwise linger in AllRecords forever.
                If hadLight OrElse hadFull Then
                    PurgeRecordsOwnedBySlot(hadLight, If(hadLight, oldLightSlot, oldFullSlot))
                End If

                Plugins(existingIdx) = reader
                ' Re-slot ONLY when the slot SPACE changed (ESP<->ESL flip). The ESM flag does not
                ' select a space — AssignFileIdSlot branches on IsESL alone — so an ESM-only flip
                ' must NOT re-slot, or it re-introduces exactly the bug above.
                If reader.IsESL <> hadLight Then
                    DropSlotAssignment(nm)
                    AssignFileIdSlot(reader)
                End If
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
    ''' light (ESL) plugins use the 0xFE light space — so it matches the game even when ESLs
    ''' precede the owner in load order.</summary>
    Public Function ResolveFormID(localFormID As UInteger, plugin As PluginReader) As UInteger
        _rwLock.EnterReadLock()
        Try
            Return ResolveFormIDNoLock(localFormID, plugin)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Hermano SIN LOCK de <see cref="ResolveFormID"/>: MISMO cuerpo, misma ley, mismo
    ''' resultado. Sólo vale si el llamador ya tiene tomado el lock de lectura — típicamente dentro
    ''' de un <see cref="RunUnderRecordsReadLock"/>, igual que <see cref="GetRecordNoLock"/>.
    ''' <para>Existe porque hay UN camino que resuelve decenas de miles de referencias seguidas
    ''' (la traducción del árbol de un record al espacio del orden de carga) y tomar el lock por cada
    ''' una es pagarlo una vez por referencia en vez de una vez por record. La ley NO se duplica: la
    ''' versión con lock es una línea que delega en ésta.</para></summary>
    Friend Function ResolveFormIDNoLock(localFormID As UInteger, plugin As PluginReader) As UInteger
        Dim masterIndex = CInt(localFormID >> 24)
        Dim objectID = localFormID And &HFFFFFFUI

        ' RANGO HARDCODED. La regla completa:
        '     si el object id es < 0x800:
        '       si el archivo permite el rango hardcoded:
        '         si el FormID entero también es < 0x800, pasa SIN TOCAR
        '         (si no, cae al mapeo normal)
        '       si no lo permite:
        '         el resultado ES el master del juego (FileID nulo)
        ' Son TRES casos, no dos, y el del medio CAE al mapeo normal:
        '   (a) permitido + FormID entero < 0x800 (hardcoded) ⇒ tal cual;
        '   (b) permitido + FileID != 0 ⇒ sigue de largo, el archivo posee records ahí legítimamente;
        '   (c) NO permitido ⇒ FileID := 0. En espacio de load order el slot 0 es el master del juego,
        '       y como el objectID ya es < 0x800 el resultado ES el objectID.
        ' Las DOS direcciones tienen que cortar en 0x800 — el escritor lo hace en
        ' TryMapGlobalToFileLocal. Si sólo corta una, el ida-y-vuelta no cierra.
        If objectID < &H800UI Then
            If AllowsHardcodedRange(plugin.HeaderVersion, plugin.Masters.Count, Config_App.Current.DataPath) Then
                If localFormID < &H800UI Then Return localFormID          ' (a)
                ' (b) cae al mapeo normal
            Else
                Return objectID                                           ' (c)
            End If
        End If

        Dim owner As PluginReader = Nothing
        If masterIndex < plugin.Masters.Count Then
            ' Reference into one of this plugin's masters. The master index is always full-style,
            ' even when the master itself is an ESL.
            Dim masterName = plugin.Masters(masterIndex)
            Dim mi As Integer = -1
            If _pluginIndex.TryGetValue(masterName, mi) Then owner = Plugins(mi)
        Else
            owner = plugin   ' self record (master index == master count)
        End If

        If owner Is Nothing Then Return localFormID   ' unresolved master — best effort
        Return MakeGlobalFormID(owner, objectID)
    End Function

    ''' <summary>Build the global FormID for a record owned by <paramref name="owner"/>: full plugins →
    ''' (fullSlot &lt;&lt; 24) | object24; ESL plugins → 0xFE | (lightSlot &lt;&lt; 12) | object12.</summary>
    ''' <para>A plugin with NO slot assigned is a broken invariant, not a value to guess at: NEVER
    ''' ignore the <c>TryGetValue</c> result here. A missing slot would silently become slot <b>0</b>
    ''' — i.e. the GAME MASTER — handing out every record of that plugin as a Fallout4.esm/Skyrim.esm
    ''' FormID, the worst possible default for a failure. Both call sites reach here with an owner taken from
    ''' <c>_pluginIndex</c>, and <see cref="IndexAndMergePlugin"/>/<see cref="MergeOverridePlugin"/>
    ''' assign the slot BEFORE <see cref="MergeRecords"/> runs, so this throw is an assertion on an
    ''' invariant that holds today — if it ever fires, the bug is ours and upstream.</para>
    Private Function MakeGlobalFormID(owner As PluginReader, objectID As UInteger) As UInteger
        If owner.IsESL Then
            Dim L As Integer
            If Not _lightSlotByName.TryGetValue(owner.FileName, L) Then Throw NoSlotAssigned(owner)
            Return &HFE000000UI Or (CUInt(L) << 12) Or (objectID And &HFFFUI)
        End If
        Dim F As Integer
        If Not _fullSlotByName.TryGetValue(owner.FileName, F) Then Throw NoSlotAssigned(owner)
        Return (CUInt(F) << 24) Or (objectID And &HFFFFFFUI)
    End Function

    ''' <summary>The exception for "this plugin is indexed but has no FileID slot". Names the plugin
    ''' and which slot space was expected, because the caller (a FormID resolution) has no context
    ''' of its own to report.</summary>
    Private Function NoSlotAssigned(owner As PluginReader) As InvalidOperationException
        Return New InvalidOperationException(
            $"Plugin '{owner.FileName}' has no {If(owner.IsESL, "light", "full")} FileID slot assigned, " &
            "so its FormIDs cannot be resolved. This is an internal invariant failure: a plugin is " &
            "indexed only via IndexAndMergePlugin/MergeOverridePlugin, both of which assign the slot first.")
    End Function

    ''' <summary>FormID global a partir del nombre del plugin y el object id del record. Devuelve 0 si el
    ''' plugin no está cargado.
    ''' <para>Tolera las DOS formas que circulan por el proyecto: el object id PELADO (12 bits útiles en un ESL,
    ''' 24 en uno completo — la convención del CK y de los JSON de f4ee) y el "local de 24 bits" de
    ''' LooksMenu, que en un ESL trae además el light slot en los bits 12..23. <see cref="MakeGlobalFormID"/>
    ''' enmascara al ancho del dueño, así que las dos entran al mismo resultado y un identificador viejo con el
    ''' slot embebido sigue resolviendo sin migración.</para>
    ''' <para><c>GlobalFormIDFromIdentifierLocal</c> es un ALIAS que reenvía acá, NO una segunda ley: se
    ''' comportan idéntico. ⛔ No "restaurarle" un OR crudo de 0xFE — el enmascarado por ancho del dueño
    ''' de <see cref="MakeGlobalFormID"/> es lo que hace que las dos formas de entrada entren igual, y
    ''' partirlas rompe los 12 call sites del alias.</para></summary>
    Public Function GlobalFormIDFromObjectID(pluginName As String, objectID As UInteger) As UInteger
        _rwLock.EnterReadLock()
        Try
            Dim idx As Integer
            If String.IsNullOrEmpty(pluginName) OrElse Not _pluginIndex.TryGetValue(pluginName, idx) Then Return 0UI
            Return MakeGlobalFormID(Plugins(idx), objectID)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Resolve a referenced FormID using the source plugin that owns the record.</summary>
    Public Function ResolveReferencedFormID(sourcePluginName As String, localFormID As UInteger) As UInteger
        If localFormID = 0UI Then Return 0UI
        If String.IsNullOrWhiteSpace(sourcePluginName) Then Return localFormID

        _rwLock.EnterReadLock()
        Try
            Return ResolveReferenciaNoLock(GetPluginByNameNoLock(sourcePluginName), localFormID)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Traduce UNA referencia al espacio del orden de carga, con el plugin de origen YA
    ''' resuelto y con el lock de lectura ya tomado por el llamador.
    '''
    ''' <para><b>Acá vive la ley completa</b>, y por eso existe: los DOS caminos que traducen
    ''' referencias la comparten. <see cref="ResolveReferencedFormID"/> es el que resuelve el plugin
    ''' por nombre y toma el lock; la traducción del árbol de un record
    ''' (<c>CanonBridge.NormalizarReferencias</c>) resuelve el plugin UNA vez y toma el lock UNA vez
    ''' para todo el record, y después llama acá por cada referencia.</para>
    '''
    ''' <para>⛔ NO reescribirla en el llamador: dos copias divergen en detalles como el corte del nombre
    ''' vacío. Es el mismo modo de falla que <c>WbFormIdWalker.EsReferencia</c> viene a evitar del otro
    ''' lado: dos traducciones distintas para el mismo FormID en la misma sesión, y un ESP que sale
    ''' apuntando a otro mod sin ningún aviso.</para>
    '''
    ''' <para>Sin plugin de origen la referencia vuelve CRUDA, que es la misma política de siempre:
    ''' es preferible un valor local reconocible a uno traducido con una tabla que no está.</para></summary>
    Friend Function ResolveReferenciaNoLock(duenio As PluginReader, localFormID As UInteger) As UInteger
        If localFormID = 0UI Then Return 0UI
        If duenio Is Nothing Then Return localFormID
        Return ResolveFormIDNoLock(localFormID, duenio)
    End Function

    ''' <summary>Alias histórico de <see cref="GlobalFormIDFromObjectID"/>, conservado porque lo nombran ~20 call
    ''' sites (los persistidos de LooksMenu / RaceMenu / sidecar y una docena de probes) y renombrarlos sería
    ''' churn sin beneficio. <b>Es un REENVÍO, no una segunda implementación</b>: las dos formas de entrada
    ''' (object id pelado y local de 24 bits con el light slot embebido) las unifica
    ''' <see cref="MakeGlobalFormID"/> al enmascarar por el ancho del dueño.</summary>
    Public Function GlobalFormIDFromIdentifierLocal(pluginName As String, identifierLocal As UInteger) As UInteger
        Return GlobalFormIDFromObjectID(pluginName, identifierLocal)
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

    ''' <summary>Posición EFECTIVA del plugin en la carga: su índice en <see cref="Plugins"/>, es decir el
    ''' orden en que este manager lo mergeó — partición por grupo master, orden topológico por masters y
    ''' exclusión de los que tienen masters faltantes YA aplicados. Es el eje de OVERRIDE: a mayor índice,
    ''' más tarde carga y más pisa (last-override-wins de <c>MergeRecords</c>).
    ''' <para>NO es el FileID slot. El slot está PARTICIONADO en full (0x00..0xFD) y light (0xFE + 12 bits)
    ''' y es lo que viaja en el FormID; para eso están <see cref="GetPluginNameByLoadOrderIndex"/> y
    ''' <see cref="GetOriginatingPluginName"/>. Dos plugins distintos pueden tener el mismo número de slot
    ''' (uno full, uno light) pero nunca la misma posición efectiva.</para>
    ''' <para>-1 cuando el plugin no está cargado (nombre vacío, o quedó fuera por masters faltantes).</para></summary>
    Public Function GetLoadOrderPosition(pluginName As String) As Integer
        _rwLock.EnterReadLock()
        Try
            Dim idx As Integer
            If String.IsNullOrEmpty(pluginName) OrElse Not _pluginIndex.TryGetValue(pluginName, idx) Then Return -1
            Return idx
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Cuántos plugins quedaron efectivamente cargados. Los excluidos por masters faltantes
    ''' (<see cref="LastExcludedForMissingMasters"/>) NO cuentan, así que <c>Count - 1</c> es la posición
    ''' efectiva más alta que puede devolver <see cref="GetLoadOrderPosition"/>.</summary>
    Public ReadOnly Property LoadedPluginCount As Integer
        Get
            _rwLock.EnterReadLock()
            Try
                Return Plugins.Count
            Finally
                _rwLock.ExitReadLock()
            End Try
        End Get
    End Property

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
    '''    N-th plugin with the ESL flag set, in load order — the same algorithm the engine's own
    '''    FileID scheme uses.
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
    ''' <summary>Outcome of <see cref="TryMapGlobalToFileLocal"/>. Son TRES, no dos, porque los dos
    ''' llamadores necesitan comportamiento OPUESTO para <see cref="OwnerNotInMasterList"/>: el writer
    ''' lanza (su pasada de descubrimiento garantiza que todo dueño ya está en la MAST, así que es una
    ''' aserción) y el saver simplemente no anota nada (el master del NPC recién se suma en ESE guardado,
    ''' o sea que es un caso legítimo y frecuente).
    ''' <para>Por eso el resultado NO es un <c>UInteger?</c>: en VB el ternario <c>If(x, 0UI)</c> sobre
    ''' un Nullable colapsa Nothing con 0 y devuelve HasValue=True, y 0 es un FormID local VÁLIDO (slot 0
    ''' = game master, object 0). Un enum + ByRef no tiene esa trampa. Ver 00-reglas-vb-trampas-que-me-comi.</para></summary>
    Public Enum FileLocalMapResult
        ''' <summary>Se resolvió: <c>localFormID</c> es válido.</summary>
        Ok = 0
        ''' <summary>El FormID global no pertenece a ningún plugin cargado; no hay a qué mapearlo.</summary>
        NoOwner = 1
        ''' <summary>El dueño se resolvió pero no está en la MAST del archivo destino ni es el destino.</summary>
        OwnerNotInMasterList = 2
    End Enum

    ''' <summary>Índice nombre→POSICIÓN de una lista de masters, para <see cref="TryMapGlobalToFileLocal"/>.
    ''' <para>La posición sale del recorrido, no del <c>Count</c> del diccionario: un MAST con una entrada
    ''' repetida (no lo producen ni el CK ni las herramientas de edición habituales, pero un archivo
    ''' editado a mano sí) haría que el
    ''' diccionario se quedara corto y corriera el índice de todos los masters siguientes. Por la misma
    ''' razón el "self index" que espera <see cref="TryMapGlobalToFileLocal"/> es el <c>Count</c> de la
    ''' LISTA, que se pasa aparte.</para>
    ''' <para>Ante un nombre REPETIDO gana la ÚLTIMA posición, porque es lo que hace el motor: recorre
    ''' la lista de masters de atrás para adelante y se queda con el primer índice que encuentra, o sea
    ''' el más alto.</para></summary>
    Public Shared Function BuildMasterIndex(masters As IEnumerable(Of String)) As Dictionary(Of String, Integer)
        Dim idx As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        If masters Is Nothing Then Return idx
        Dim i As Integer = 0
        For Each m In masters
            ' Asignación incondicional = "gana la última", el `downto 0` del canónico.
            If Not String.IsNullOrEmpty(m) Then idx(m) = i
            i += 1
        Next
        Return idx
    End Function

    ''' <summary>Traducir un FormID GLOBAL (numeración de la sesión: slot de lo cargado) al espacio LOCAL
    ''' de un archivo cuya MAST es <paramref name="masterIndex"/> y que se llama <paramref name="outName"/>
    ''' (numeración local: el byte alto es un índice en ESA MAST). Es LA conversión entre las dos
    ''' numeraciones, y vive acá una sola vez.
    '''
    ''' <para>La usan DOS llamadores con listas de masters distintas: el remapper de
    ''' <c>SaveNpcEspWriter</c> (contra la MAST NUEVA que se está escribiendo) y
    ''' <c>NpcOverrideSaver.MapGlobalToLocalInPlugin</c> (contra la MAST VIEJA del disco). ⛔ Ninguno la
    ''' reimplementa. En particular, "el dueño no está en la MAST" NO es SELF: SELF vale sólo cuando el
    ''' dueño ES el archivo destino. Con un master todavía ausente, tratarlo como SELF da un FormID local
    ''' que colisiona con los records propios (drafts y records nuevos de cualquier mod arrancan en 0x800,
    ''' por la convención del CK), y ese FormID se usa para DESCARTAR records al preservar.</para>
    '''
    ''' <para>El ancho del object id lo decide el ENCODING DE ORIGEN, no el destino: un global light es
    ''' <c>0xFE | lightSlot&lt;&lt;12 | object12</c>, así que enmascarar con 0xFFFFFF conservaría los bits del
    ''' slot. La salida es siempre full-form (<c>idx &lt;&lt; 24 | object</c>), igual que el motor, que
    ''' referencia a los masters ESL como si fueran full.</para></summary>
    ''' <param name="masterCount">Cantidad de entradas de la MAST — el "self index", o sea el byte alto de
    ''' los records propios del archivo. Va aparte del diccionario a propósito: con un MAST que repita un
    ''' nombre, <c>masterIndex.Count</c> sería menor que la cantidad real de entradas.</param>
    Public Function TryMapGlobalToFileLocal(globalFormID As UInteger,
                                            masterIndex As IDictionary(Of String, Integer),
                                            masterCount As Integer,
                                            outName As String,
                                            ByRef localFormID As UInteger) As FileLocalMapResult
        localFormID = 0UI

        ' Un FormID HARDCODED (valor entero < 0x800) pasa SIN TOCAR: no pertenece a ningún archivo, lo
        ' define el motor. Va PRIMERO, antes de resolver dueño alguno.
        If globalFormID < &H800UI Then
            localFormID = globalFormID
            Return FileLocalMapResult.Ok
        End If

        Dim ownerName = GetOriginatingPluginName(globalFormID)
        If String.IsNullOrEmpty(ownerName) Then Return FileLocalMapResult.NoOwner

        Dim isLightSource As Boolean = ((globalFormID >> 24) And &HFFUI) = &HFEUI
        Dim objectID As UInteger = If(isLightSource, globalFormID And &HFFFUI, globalFormID And &HFFFFFFUI)

        ' El destino se chequea ANTES que la MAST: un archivo nunca se lista a sí mismo como master, así
        ' que "no está en la lista" es la respuesta esperada para él y el self index es masterCount.
        If String.Equals(ownerName, outName, StringComparison.OrdinalIgnoreCase) Then
            localFormID = (CUInt(Math.Max(0, masterCount)) << 24) Or objectID
            Return FileLocalMapResult.Ok
        End If

        Dim idx As Integer
        If masterIndex IsNot Nothing AndAlso masterIndex.TryGetValue(ownerName, idx) Then
            localFormID = (CUInt(idx) << 24) Or objectID
            Return FileLocalMapResult.Ok
        End If

        Return FileLocalMapResult.OwnerNotInMasterList
    End Function

    ''' <summary>Texto de una tabla externa por su identificador.
    ''' <para>Existe porque un campo traducible ya no llega como bytes crudos sino como el valor
    ''' que se leyo de el: cuando el archivo usa tablas externas ese valor ES el identificador.
    ''' El identificador cero significa 'sin texto' y devuelve cadena vacia; solo uno distinto de
    ''' cero que no resuelve es un error de verdad.</para></summary>
    Public Function ResolveLocalizedString(pluginFileName As String, stringId As UInteger,
                                           Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If stringId = 0UI OrElse _localizedStrings Is Nothing Then Return ""
        Return _localizedStrings.Resolve(pluginFileName, stringId, kind)
    End Function

    Public Function ResolveFieldString(rec As PluginRecord, sr As SubrecordData, Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If sr.Data Is Nothing OrElse sr.Data.Length = 0 Then Return ""

        If rec IsNot Nothing AndAlso rec.SourcePluginIsLocalized AndAlso rec.SourcePluginName <> "" AndAlso sr.Data.Length >= 4 Then
            Dim stringId = BitConverter.ToUInt32(sr.Data, 0)
            ' lstring ID 0 is the canonical "no string" sentinel (an ABSENT/empty translatable field) — it
            ' must render BLANK, it is NOT an error. NEVER return the "<Error: Unknown lstring ID ...>"
            ' placeholder for id 0: it gets stored as the field's TEXT (e.g. ARMO DESC / FULL) and re-emitted
            ' verbatim on save, so an override of a record whose DESC is a 0-id sprouts a bogus description.
            ' Only a NON-ZERO id that fails to resolve is a real error (missing STRINGS sidecar).
            If stringId = 0UI Then Return ""
            If _localizedStrings IsNot Nothing Then
                Dim resolved = _localizedStrings.Resolve(rec.SourcePluginName, stringId, kind)
                If resolved <> "" Then Return resolved
            End If
            Return $"<Error: Unknown lstring ID {stringId:X8}>"
        End If

        ' Per-file translatable encoding (from TES4 SNAM <cp:XXXX>) takes precedence over the
        ' global PluginEncodingSettings.Translatable: the file's own declared encoding for translatable
        ' fields beats the global default.
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

    ''' <summary>Run <paramref name="body"/> while holding the records READ lock for its whole duration,
    ''' so no writer (the Save read-back's <see cref="MergeOverridePlugin"/>) can interleave a record-set
    ''' rewrite in the middle. Multiple readers still run concurrently (a read lock does not block the
    ''' render thread's own read-locked lookups), so only the rare Save writer waits for the body to
    ''' finish. Use together with <see cref="GetRecordNoLock"/> for record fetches inside the body — the
    ''' lock is already held, so re-fetching through the lock-taking <see cref="GetRecord"/> is
    ''' unnecessary (the SupportsRecursion policy makes it harmless, but the lock-free path is the intent).
    '''
    ''' <para>Deadlock-safe: a read-lock holder must NOT reach any WRITE-lock path (a read lock cannot
    ''' upgrade), so <paramref name="body"/> may only call read-locked / lock-free PluginManager members.
    ''' The only write-lock callers are <see cref="LoadAllPlugins"/> and <see cref="MergeOverridePlugin"/>,
    ''' neither of which is reachable from a record-resolution walk.</para>
    ''' <para>Thread-affine: <paramref name="body"/> MUST be fully synchronous — no Await, no resuming on
    ''' a different thread before it returns. ReaderWriterLockSlim requires the same thread that entered
    ''' the read lock to exit it, so an awaited continuation on another pool thread would throw
    ''' SynchronizationLockException. Only wrap synchronous walks here.</para></summary>
    Public Function RunUnderRecordsReadLock(Of T)(body As Func(Of T)) As T
        _rwLock.EnterReadLock()
        Try
            Return body()
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>El <see cref="PluginReader"/> de ese nombre SIN tomar el lock. Sólo vale con el lock
    ''' de lectura ya tomado por el llamador (ver <see cref="RunUnderRecordsReadLock"/>), igual que
    ''' <see cref="GetRecordNoLock"/>. Nothing si el plugin no está cargado.</summary>
    Friend Function GetPluginByNameNoLock(pluginName As String) As PluginReader
        If String.IsNullOrEmpty(pluginName) Then Return Nothing
        Dim idx As Integer
        If Not _pluginIndex.TryGetValue(pluginName, idx) Then Return Nothing
        If idx < 0 OrElse idx >= Plugins.Count Then Return Nothing
        Return Plugins(idx)
    End Function

    ''' <summary>Lock-free sibling of <see cref="GetRecord"/>: returns the SAME final resolved record
    ''' (same <c>AllRecords</c> dictionary, same <c>TryGetValue</c>, same Nothing-on-miss) WITHOUT
    ''' acquiring the read lock. Only valid when the caller already holds the records read lock — e.g.
    ''' inside a <see cref="RunUnderRecordsReadLock"/> body — so the whole sequence of fetches observes
    ''' one consistent, writer-frozen <c>AllRecords</c>. Calling it without the lock held is unsafe (could
    ''' observe a half-rebuilt record set under a concurrent write).</summary>
    Public Function GetRecordNoLock(formID As UInteger) As PluginRecord
        Dim rec As PluginRecord = Nothing
        AllRecords.TryGetValue(formID, rec)
        Return rec
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

    ''' <summary>Drop every record of a type from memory, after whatever needed it has consumed it.
    '''
    ''' For record types we only READ ONCE at load and never resolve again. QUST is the case this exists for: it is
    ''' loaded solely so <see cref="RaceCompatibilityCatalog"/> can read the VMAD of the quests that carry
    ''' RaceCompatibility's GenericRaceController (8 quests in a COtR load order, out of thousands). Quests are
    ''' heavy records (aliases, conditions, dialogue) and nothing else in the app touches them, so keeping them
    ''' resident is pure waste. If a future feature needs QUST at runtime (scripts, aliases, stages), remove the
    ''' DropRecordsOfType("QUST") call in MainForm instead of re-adding a second load pass.</summary>
    Public Sub DropRecordsOfType(sig As String)
        If String.IsNullOrEmpty(sig) Then Return
        _rwLock.EnterWriteLock()
        Try
            Dim recs As List(Of PluginRecord) = Nothing
            If Not RecordsByType.TryGetValue(sig, recs) OrElse recs Is Nothing Then Return
            For Each r In recs
                AllRecords.Remove(r.Header.FormID)
            Next
            Dim n = recs.Count
            RecordsByType.Remove(sig)
            Logger.LogLazy(Function() $"[PLUGINS] dropped {n} {sig} records from memory (consumed at load, not needed at runtime).")
        Finally
            _rwLock.ExitWriteLock()
        End Try
    End Sub

    ''' <summary>True if the plugin named <paramref name="pluginName"/> was authored by this app's NPC Manager
    ''' save flow — identified by its TES4.CNAM author = <see cref="PluginWriter.NPC_MANAGER_AUTHOR_CNAM"/>. Lets
    ''' the editors list "my records" (new AND override) by their source plugin, robustly across sessions.</summary>
    Public Function IsNpcManagerPlugin(pluginName As String) As Boolean
        If String.IsNullOrEmpty(pluginName) Then Return False
        _rwLock.EnterReadLock()
        Try
            Dim idx As Integer
            If Not _pluginIndex.TryGetValue(pluginName, idx) Then Return False
            Return String.Equals(Plugins(idx).Author, PluginWriter.NPC_MANAGER_AUTHOR_CNAM, StringComparison.Ordinal)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>Whether <paramref name="pluginName"/> is loaded in this session, i.e. it has an entry in
    ''' the plugin index and therefore a FileID slot and a usable master list. Anything NOT loaded cannot
    ''' have its FormIDs resolved: the resolver would read the file-local master index as a load-order
    ''' slot and silently name a different plugin.</summary>
    Public Function IsLoaded(pluginName As String) As Boolean
        If String.IsNullOrEmpty(pluginName) Then Return False
        _rwLock.EnterReadLock()
        Try
            Return _pluginIndex.ContainsKey(pluginName)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>El <see cref="PluginReader"/> CARGADO con ese nombre, o Nothing si no está en la sesión.
    ''' Para comparar contra una lectura fresca del disco (¿cambió el archivo desde que lo cargamos?), que es la
    ''' única forma de detectar que la MAST en memoria y la del archivo ya no son la misma.</summary>
    Public Function GetPluginByName(pluginName As String) As PluginReader
        If String.IsNullOrEmpty(pluginName) Then Return Nothing
        _rwLock.EnterReadLock()
        Try
            Dim idx As Integer
            If Not _pluginIndex.TryGetValue(pluginName, idx) Then Return Nothing
            If idx < 0 OrElse idx >= Plugins.Count Then Return Nothing
            Return Plugins(idx)
        Finally
            _rwLock.ExitReadLock()
        End Try
    End Function

    ''' <summary>The masters <paramref name="pluginName"/> transitively depends on that are NOT loaded,
    ''' in discovery order and without duplicates. Empty when the closure is satisfied.
    ''' <para>Transitive on purpose: the missing-master mark propagates up the dependency graph, so a
    ''' master whose own master is absent poisons its dependents too.
    ''' Returns empty for a plugin that is not loaded at all — that is a different, blunter problem and
    ''' <see cref="IsLoaded"/> is the question to ask first.</para></summary>
    Public Function MissingMastersOf(pluginName As String) As List(Of String)
        Dim missing As New List(Of String)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        _rwLock.EnterReadLock()
        Try
            Dim rootIdx As Integer
            If String.IsNullOrEmpty(pluginName) OrElse Not _pluginIndex.TryGetValue(pluginName, rootIdx) Then Return missing
            Dim queue As New Queue(Of Integer)
            queue.Enqueue(rootIdx)
            seen.Add(pluginName)
            While queue.Count > 0
                Dim p = Plugins(queue.Dequeue())
                If p Is Nothing Then Continue While
                For Each m In p.Masters
                    If Not seen.Add(m) Then Continue For
                    Dim mi As Integer
                    If _pluginIndex.TryGetValue(m, mi) Then
                        queue.Enqueue(mi)
                    Else
                        missing.Add(m)
                    End If
                Next
            End While
        Finally
            _rwLock.ExitReadLock()
        End Try
        Return missing
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
        ' Lo que otras herramientas de edición muestran como "valor heredado" en columnas de override es display-only;
        ' el binario del override no contiene ese subrecord.
        ' ⛔ NO hacer merge a nivel de subrecord: heredaría TNAM=SkinHeadRearCBBE de CBBE.esp y
        ' pisaría la decisión del modder de borrarlo en CBBEHeadRearFix.esp.
        ' When an APP-authored (NPC_Manager) plugin is about to override a FormID, remember the record it's
        ' overriding — the WINNING non-app version (app plugins load last, so AllRecords[fid] currently holds it) —
        ' so "revert override" can restore exactly that in memory. ContainsKey guard keeps the FIRST such capture,
        ' so a 2nd app plugin can't replace the real non-app record with an app one. See _recordBeforeAppOverride.
        Dim readerIsApp = String.Equals(reader.Author, PluginWriter.NPC_MANAGER_AUTHOR_CNAM, StringComparison.Ordinal)
        For Each kvp In reader.Records
            Dim globalFormID = ResolveFormID(kvp.Key, reader)
            kvp.Value.Header.FormID = globalFormID
            If readerIsApp Then
                Dim existing As PluginRecord = Nothing
                ' Capture ONLY a NON-app predecessor: the record must exist, not already be captured, and not itself
                ' be app-authored (so the post-save readback re-merging the app plugin can't record an APP record as
                ' the "before" — which would make a later revert restore an app override instead of the mod's / dropping).
                If AllRecords.TryGetValue(globalFormID, existing) AndAlso existing IsNot Nothing _
                   AndAlso Not _recordBeforeAppOverride.ContainsKey(globalFormID) _
                   AndAlso Not RecordIsAppAuthoredNoLock(existing) Then
                    _recordBeforeAppOverride(globalFormID) = existing
                End If
            End If
            AllRecords(globalFormID) = kvp.Value
        Next
    End Sub

    ''' <summary>Revert an app-authored override IN MEMORY: make <paramref name="fid"/> resolve again to the record
    ''' that was WINNING before the app override — the last non-app override captured in
    ''' <see cref="_recordBeforeAppOverride"/> — or REMOVE it entirely when the app created it new (no prior record).
    ''' So GetRecord / render / pickers immediately reflect "the app override is gone", matching what the next Save
    ''' writes via RecordsToRemove. Rebuilds the type index. Returns True if <see cref="AllRecords"/> changed. The
    ''' caller must clear parse caches + re-render (the app record objects stay valid; only the winner map changes).</summary>
    Public Function RevertAppOverride(fid As UInteger) As Boolean
        If fid = 0UI Then Return False
        _rwLock.EnterWriteLock()
        Try
            Dim prior As PluginRecord = Nothing
            If _recordBeforeAppOverride.TryGetValue(fid, prior) AndAlso prior IsNot Nothing Then
                ' The app OVERRODE a non-app record → restore that (the mod's winning version).
                AllRecords(fid) = prior
                _recordBeforeAppOverride.Remove(fid)
                BuildTypeIndex()
                Return True
            End If
            ' No captured predecessor. Only remove if the CURRENT winner is actually an app-authored record (an
            ' app-CREATED new record → drop it). If it's already a non-app record (a prior revert restored it, or the
            ' app never overrode it), this is a no-op — guards a double-revert from deleting the restored mod record.
            Dim current As PluginRecord = Nothing
            If AllRecords.TryGetValue(fid, current) AndAlso RecordIsAppAuthoredNoLock(current) Then
                AllRecords.Remove(fid)
                BuildTypeIndex()
                Return True
            End If
            Return False
        Finally
            _rwLock.ExitWriteLock()
        End Try
    End Function

    ''' <summary>True if <paramref name="rec"/> comes from an app-authored (NPC_Manager) plugin. Caller MUST already
    ''' hold <see cref="_rwLock"/> (read or write) — this reads <c>_pluginIndex</c>/<c>Plugins</c> without taking it,
    ''' so it is safe to call from inside <see cref="RevertAppOverride"/>'s write lock (no read-lock re-entry).</summary>
    Private Function RecordIsAppAuthoredNoLock(rec As PluginRecord) As Boolean
        If rec Is Nothing OrElse String.IsNullOrEmpty(rec.SourcePluginName) Then Return False
        Dim idx As Integer
        If Not _pluginIndex.TryGetValue(rec.SourcePluginName, idx) Then Return False
        If idx < 0 OrElse idx >= Plugins.Count Then Return False
        Return String.Equals(Plugins(idx).Author, PluginWriter.NPC_MANAGER_AUTHOR_CNAM, StringComparison.Ordinal)
    End Function

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

    ''' <summary>Cuando True, ReadActiveLoadOrder devuelve SOLO los plugins OFICIALES de Bethesda
    ''' (vanilla + DLCs + Creation Club), excluyendo los mods del usuario del Plugins.txt. Lo usan TANTO
    ''' el load de plugins COMO el mount de archivos (FilesDictionary llama a ReadActiveLoadOrder), así que
    ''' con este flag el entorno headless queda 100% vanilla (records Y texturas) — para comparar FaceGen
    ''' vs CK sin contaminación de mods. Lo prende el CLI con --vanillaonly. Default False (comportamiento app).</summary>
    Public Shared OfficialPluginsOnly As Boolean = False

    ''' <summary>Con <see cref="OfficialPluginsOnly"/> puesto, deja afuera TAMBIEN el Creation Club, o sea
    ''' que el corpus queda en juego base + DLC.
    ''' <para>Existe porque "oficial" incluye el cc y eso hace que el corpus dependa de lo que el usuario
    ''' tenga comprado: una instalacion con 74 plugins de cc mide 3602 NPC de Skyrim y otra sin ninguno
    ''' mide bastante menos, asi que dos corridas no se pueden comparar cuenta contra cuenta. Con esto en
    ''' True el corpus es REPRODUCIBLE entre maquinas.</para>
    ''' <para>Default False (comportamiento app).</para></summary>
    Public Shared ExcludeCreationClub As Boolean = False

    ''' <summary>Plugin oficial de Bethesda (vanilla + DLC FO4/SSE + master VR + Creation Club cc*). Lo demás
    ''' = mod del usuario. Los .esm de VR son oficiales por definición: van en la misma lista de
    ''' oficiales que los DLC. Sin ellos acá,
    ''' <see cref="FilterOfficialIfRequested"/> (CLI --vanillaonly) borraría el implícito de VR del load order.</summary>
    Public Shared Function IsOfficialPlugin(name As String) As Boolean
        If String.IsNullOrEmpty(name) Then Return False
        Dim n = name.ToLowerInvariant()
        Select Case n
            Case "fallout4.esm", "dlcrobot.esm", "dlcworkshop01.esm", "dlccoast.esm",
                 "dlcworkshop02.esm", "dlcworkshop03.esm", "dlcnukaworld.esm", "dlcultrahighresolution.esm",
                 "fallout4_vr.esm",
                 "skyrim.esm", "update.esm", "dawnguard.esm", "hearthfires.esm", "dragonborn.esm",
                 "skyrimvr.esm"
                Return True
        End Select
        ' Creation Club (FO4 + SSE). Se puede dejar afuera para que el corpus no dependa de lo comprado.
        If ExcludeCreationClub Then Return False
        Return n.StartsWith("cc")
    End Function

    ''' <summary>La "partial index" de Bethesda para un FormID: el <c>modIndex</c> de un plugin completo, o
    ''' <c>0xFE000 | lightIndex</c> para uno light. Es exactamente <c>ModInfo::GetPartialIndex</c>
    ''' (f4se GameData.h:87-90) y es la CLAVE con la que un <c>.jslot</c> de RaceMenu indexa su tabla
    ''' <c>mods</c> (<c>[{index,name}]</c>, escrita por skee con esa misma función,
    ''' PresetInterface.cpp:361,396-401).
    ''' <para>skee la reconstruye al leer como <c>modIndex &lt;&gt; 0xFE ? modIndex : (formId &gt;&gt; 12)</c>
    ''' (PresetInterface.cpp:993), que es la misma expresión: para un light,
    ''' <c>0xFE000000 | lightIndex&lt;&lt;12 | obj12</c> desplazado 12 da <c>0xFE000 | lightIndex</c>.</para>
    ''' <para>Sirve para leer una tabla de OTRO load order. NO es un slot de esta sesión: el número que
    ''' devuelve sólo tiene sentido contra la tabla del archivo del que salió el FormID.</para></summary>
    Public Shared Function PartialIndexOfFormID(formID As UInteger) As UInteger
        If (formID >> 24) <> &HFEUI Then Return formID >> 24
        Return formID >> 12
    End Function

    ''' <summary>Local FormID used in the FaceGen file name, per CK convention. Full plugins: strip the
    ''' high (load-order) byte (&amp; 0xFFFFFF). ESL/light plugins (high byte 0xFE): ALSO strip the 12-bit
    ''' light slot, leaving only the 12-bit record (&amp; 0xFFF). Matches the engine's ESL FileID scheme
    ''' used by ResolveFormID / ToLocalFormID above (0xFE | lightSlot&lt;&lt;12 | object12). Verified: ESL runtime
    ''' 0xFE032800 → CK writes "00000800" (record 0x800), NOT "00032800"; without the ESL mask the light
    ''' slot leaks into the FaceGen mesh/texture name and the game can't find it. Stateless.
    ''' <para>NO confundir con <see cref="PartialIndexOfFormID"/>, justo arriba: aquélla devuelve el
    ''' ÍNDICE del plugin y ésta el OBJECT ID del record — las dos mitades complementarias del mismo
    ''' FormID (<c>ModInfo::GetPartialIndex</c> y el inverso de <c>ModInfo::GetFormID</c>).</para></summary>
    Public Shared Function ToFaceGenLocalFormID(globalFormID As UInteger) As UInteger
        If (globalFormID >> 24) = &HFEUI Then Return globalFormID And &HFFFUI
        Return globalFormID And &HFFFFFFUI
    End Function

    Private Shared Function FilterOfficialIfRequested(list As List(Of String)) As List(Of String)
        If Not OfficialPluginsOnly Then Return list
        Return list.Where(AddressOf IsOfficialPlugin).ToList()
    End Function

    ''' <summary>True when the configured game executable is the VR build (Fallout4VR.exe / SkyrimVR.exe).
    ''' The VR game mode is picked from the exe name ('Fallout4VR' / 'SkyrimVR') and everything else
    ''' (AppData folder, ini folder) follows from the mode — NOT from which folder happens to exist on
    ''' disk. So the exe the user pointed at decides which game's files we read; folder existence is only
    ''' a last-resort fallback. SSE's exe is SkyrimSE.exe and FO4's is Fallout4.exe, so neither matches
    ''' the VR suffix.</summary>
    ''' <para>El discriminador vive en <see cref="GamePathsResolver.IdentifyExe"/> y compara contra los cuatro
    ''' nombres canónicos: NO alcanza con mirar si la ruta TERMINA en "VR", que da False para un usuario que
    ''' apunta a <c>skse64_loader.exe</c> al lado de <c>SkyrimVR.exe</c> — y de esa respuesta cuelga el
    ''' master implícito de VR del load order.</para>
    ''' <para>MEMOIZADO. <c>ResolveFormID</c> lo llama por CADA FormID con object id &lt; 0x800, y por debajo
    ''' hace <c>GamePathsResolver.IdentifyExe</c>, que cuando el exe configurado no es uno de los cuatro
    ''' canónicos (caso REAL y documentado: <c>f4se_loader.exe</c>) prueba hasta cuatro <c>File.Exists</c>.
    ''' MEDIDO, 200k llamadas: 0,095 µs con <c>Fallout4.exe</c> contra <b>5,6 µs</b> con
    ''' <c>f4se_loader.exe</c> y <b>8,0 µs</b> con <c>SkyrimVR.exe</c> — 58× y 84×.</para>
    ''' <para>La clave es la ruta del exe configurado, así que cambiar de juego o de exe lo recalcula solo.</para>
    Private Shared _vrBuildMemoExe As String = Nothing
    Private Shared _vrBuildMemoValue As Boolean = False
    Public Shared Function IsVrBuild() As Boolean
        Dim exe = If(Config_App.Current?.FO4ExePath, "")
        SyncLock _masterGroupMemo
            If String.Equals(exe, _vrBuildMemoExe, StringComparison.OrdinalIgnoreCase) Then Return _vrBuildMemoValue
        End SyncLock
        Dim v = GamePathsResolver.IsVrBuild()
        SyncLock _masterGroupMemo
            _vrBuildMemoExe = exe
            _vrBuildMemoValue = v
        End SyncLock
        Return v
    End Function

    ''' <summary>Resolves the LocalAppData game directory that holds Plugins.txt / loadorder.txt.
    ''' The folder is named after the game itself, directly under LocalAppData: flat "Fallout4" /
    ''' "Skyrim Special Edition", VR "Fallout4VR" / "Skyrim VR", with Plugins.txt right inside it.
    ''' PREFERENCE FOLLOWS THE EXE (<see cref="IsVrBuild"/>): a VR exe reads the VR folder first and only
    ''' falls back to the flat one if the VR folder is absent; a flat exe does the reverse. If the flat
    ''' folder always won when it existed, a VR user with both games installed would get the FLAT game's
    ''' load order. Always returns the preferred path when neither exists, so callers can still build a
    ''' (non-existent) file path without crashing.</summary>
    Public Shared Function ResolveGameAppDataDir() As String
        Return GamePathsResolver.Resolve().PluginsDir
    End Function

    ''' <summary>Ruta completa del <c>Plugins.txt</c> vigente, o "" si no se resolvió. Preferir ESTA sobre
    ''' <c>ResolveGameAppDataDir</c> + "Plugins.txt": cuando el usuario fija la ruta a mano puede apuntar al
    ''' archivo de un perfil de mod manager, que no tiene por qué llamarse igual ni vivir en una carpeta con
    ''' nombre de juego.</summary>
    Public Shared Function ResolvePluginsTxtPath() As String
        Return GamePathsResolver.Resolve().PluginsTxtPath
    End Function

    ''' <summary>"" cuando la ubicación del load order está resuelta; si no, la explicación en inglés para
    ''' mostrarle al usuario.
    '''
    ''' <para>Existe porque el modo de falla de todo esto es MUDO: sin Plugins.txt,
    ''' <see cref="ReadActiveLoadOrder"/> devuelve los masters implícitos y nada más, o sea una lista
    ''' perfectamente válida que representa un juego sin un solo mod. Sin esto ningún caller puede
    ''' distinguir eso de "el usuario efectivamente no tiene mods".</para></summary>
    Public Shared Function LoadOrderSourceProblem() As String
        Dim r = GamePathsResolver.Resolve()
        If r.HasPluginsTxt Then Return ""
        Return r.Problem
    End Function

    ''' <summary>Resolves the Documents\My Games directory that holds the game .ini files
    ''' ('My Games\' + the game's own folder name). Same VR-first-by-exe rule
    ''' as <see cref="ResolveGameAppDataDir"/>. NOTE: only the FOLDER carries the VR suffix — the ini FILE
    ''' names follow the base (non-VR) game name instead, i.e. FO4VR still reads Fallout4[Custom].ini and
    ''' SkyrimVR still reads Skyrim[Custom].ini. Use <see cref="ResolveGameIniPath"/> to build a full path.</summary>
    Public Shared Function ResolveGameIniDir() As String
        Return GamePathsResolver.Resolve().IniDir
    End Function

    ''' <summary>Full path of an ini file (<paramref name="iniFileName"/> = "Fallout4.ini",
    ''' "SkyrimCustom.ini", …) inside <see cref="ResolveGameIniDir"/>. Extra VR rule: VR builds don't
    ''' create the ini file in My Games by default, they use the one in the game folder instead — so for
    ''' a VR build, if the ini is NOT in My Games, fall back to the game root — the
    ''' folder that contains Data, i.e. the exe's own folder. Returns the My Games path when nothing exists.</summary>
    ''' <para>Puede devolver "" (carpeta de inis sin resolver). Los dos consumidores
    ''' (<c>PluginEncodingSettings.ReadSLanguageFrom</c> y <c>LocalizedStrings</c>) arrancan con un
    ''' <c>File.Exists</c> que trata "" como ausente, que es la semántica correcta: sin ini no hay
    ''' <c>sLanguage</c> y se cae al default del juego, igual que si el archivo no existiera.</para>
    Public Shared Function ResolveGameIniPath(iniFileName As String) As String
        Return GamePathsResolver.ResolveIniPath(iniFileName)
    End Function

    ''' <summary>Si está instalado el plugin VRESL, que es lo que le agrega soporte de light/update a los builds
    ''' de VR. La ley: en un build VR, si existe el dll de VRESL en la carpeta de scripts del juego, eso
    ''' habilita a la vez el soporte de plugins light y el de plugins "update" (0x100); sin VRESL ninguno
    ''' de los dos existe en VR.
    ''' <para>Es UN SOLO booleano del que cuelgan las DOS capacidades. Por eso vive en una función sola y
    ''' <see cref="LightIsSupported"/> y <see cref="UpdateIsSupported"/> la llaman: si algún día cambia la
    ''' detección, cambia para las dos.</para></summary>
    ''' <para>⛔ NO MEMOIZAR, aunque tiente: esto cuelga de <see cref="AllowsHardcodedRange"/>, al que
    ''' <see cref="ResolveFormIDNoLock"/> llama por CADA referencia con object id &lt; 0x800, o sea decenas
    ''' de miles de <c>File.Exists</c> en un build de VR. Pero un memo por (exe, dataPath, juego) <b>no
    ''' puede ver que el archivo apareció o desapareció</b>, que es justamente el dato: instalar VRESL sin
    ''' reiniciar la aplicación dejaría de tener efecto. Rompe <c>SlotResolutionProbe</c> (114/1: el caso
    ''' "SkyrimVR CON VRESL" responde que no, con la respuesta memoizada del caso anterior).
    ''' <para>Para el usuario que NO está en VR el costo ya es cero: la primera línea corta antes de
    ''' tocar el disco, y <see cref="IsVrBuild"/> sí está memoizado. Si algún día hay que arreglarlo
    ''' para VR, el camino es sacar la llamada del bucle por referencia, no cachear el resultado.</para></summary>
    Private Shared Function VreslInstalled(dataPath As String) As Boolean
        If Not IsVrBuild() Then Return False
        If String.IsNullOrEmpty(dataPath) Then Return False
        Dim isFO4 As Boolean = (Config_App.Current.Game = Config_App.Game_Enum.Fallout4)
        ' Cada juego lo detecta por la presencia del plugin del cargador de scripts que agrega el
        ' soporte. Fallout 4 VR tiene DOS implementaciones y alcanza con cualquiera de las dos;
        ' Skyrim VR tiene una sola. Buscar sólo la primera de Fallout 4 deja al rig que usa la otra
        ' con el piso de FormID equivocado.
        Dim dlls As String() = If(isFO4,
            {Path.Combine(dataPath, "F4SE", "Plugins", "falloutvresl.dll"),
             Path.Combine(dataPath, "F4SE", "Plugins", "Daytripper4.dll")},
            {Path.Combine(dataPath, "SKSE", "Plugins", "skyrimvresl.dll")})
        Return dlls.Any(AddressOf File.Exists)
    End Function

    ''' <summary>Ley de soporte de plugins light: los juegos planos (FO4, SSE) lo soportan siempre;
    ''' los dos de VR NO por defecto, y ahí depende de <see cref="VreslInstalled"/>.</summary>
    Private Shared Function LightIsSupported(dataPath As String) As Boolean
        If Not IsVrBuild() Then Return True          ' FO4 / SSE planos: soportado siempre
        Return VreslInstalled(dataPath)
    End Function

    ''' <summary>Ley de soporte del flag "update" (0x100): sólo Starfield (que no soportamos) lo trae
    ''' de fábrica; en el resto de los juegos depende de VRESL.
    ''' <para>Ojo con la asimetría respecto de <see cref="LightIsSupported"/>: acá los juegos planos
    ''' <b>NO</b> están soportados por defecto, así que el flag 0x100 sólo significa
    ''' algo en VR con VRESL. En FO4/SSE normales el resultado de IsUpdate es False
    ''' SIEMPRE, esté o no puesto el bit — el corte está en si el juego soporta el flag en absoluto, no
    ''' en el header. MEDIDO: 0 de 71 plugins de FO4 y 0 de 103 de SSE tienen 0x100, así que hoy
    ''' es inerte en los dos rigs — pero la ley se implementa igual, porque VR con VRESL sí lo activa.</para>
    ''' <para>El "pseudo update" que existe en otras herramientas de edición de plugins NO se replica acá:
    ''' es un modo de línea de comandos propio de esas herramientas, no una propiedad del juego.</para></summary>
    Private Shared Function UpdateIsSupported(dataPath As String) As Boolean
        Return VreslInstalled(dataPath)              ' gmSF1 no lo soportamos
    End Function

    ''' <summary>Si el plugin ocupa un slot LIGHT (0xFE + índice de 12 bits) en vez de un slot full. Ley
    ''' canónica COMPLETA: al entrar, si el plugin es "update" (0x100, y el juego soporta ese flag), un
    ''' flag de light o de medium en el header hace que deje de contar como update — pero esa comprobación
    ''' es lo ÚNICO que pasa en esa rama: la marca por EXTENSIÓN (.esl) sólo se evalúa en la rama
    ''' contraria (plugin que NO era update al entrar). Aparte y sin condición, el flag 0x200 (IsLight)
    ''' siempre marca el plugin como light.
    ''' o sea <c>light = 0x200 OR (extensión .esl AND NOT IsUpdate)</c>.
    ''' <para>La marca por extensión se decide sobre el valor de <c>IsUpdate</c> AL ENTRAR: aunque el propio
    ''' chequeo de arriba lo vuelva False adentro de la rama "update", esa rama ya no vuelve a evaluar la
    ''' condición de extensión. Por eso un <c>.esl</c> con 0x100 NO se vuelve light por su extensión. Leer
    ''' sólo el flag 0x200 y escribir un OR simple pierde esa condición.</para>
    ''' <para>Y <c>IsUpdate</c> no es el bit pelado: primero se comprueba si el juego soporta el flag 0x100
    ''' en absoluto. En FO4/SSE no-VR eso es siempre
    ''' False ⇒ ahí la ley COLAPSA al OR simple y las dos formas coinciden. La diferencia aparece sólo en VR con
    ''' VRESL. Ver <see cref="UpdateIsSupported"/>.</para>
    ''' <para>El flag "medium" (Starfield) no se replica: sólo aplica a un juego que no soportamos, y
    ''' sólo influye para volver <c>IsUpdate</c> False — o sea que ignorarlo no
    ''' puede darnos un light de más, sólo evitarnos uno de menos en un juego que no soportamos.</para></summary>
    ''' <param name="headerFlags">El campo de flags del registro TES4, crudo.</param>
    Public Shared Function IsLightSlot(dataPath As String, name As String, headerFlags As UInteger) As Boolean
        ' GUARDA QUE ENVUELVE A LOS DOS DISYUNTOS, no sólo al de la extensión: SIN soporte light NINGÚN
        ' plugin es light — ni siquiera con el flag 0x200 puesto — y un .esl sin soporte ni siquiera
        ' entra al load order.
        ' Es la MISMA guarda que IsMasterGroup: en un rig VR sin el plugin VRESL, darle un slot light a un
        ' .esl lo mete en el espacio 0xFE y corre a TODOS los full que vengan después — cada FormID que
        ' poseen resuelve al archivo equivocado.
        If Not LightIsSupported(dataPath) Then Return False
        If (headerFlags And FLAG_ESL) <> 0 Then Return True                     ' IsLight (0x200)
        If name Is Nothing OrElse Not name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) Then Return False
        Dim isUpdate As Boolean = (headerFlags And FLAG_UPDATE) <> 0 AndAlso UpdateIsSupported(dataPath)
        Return Not isUpdate                                                     ' extensión .esl, sólo en el ELSE
    End Function

    ''' <summary>Discriminante de la memo de grupo: "flat" / "vr" / "vr+esl". Va en la CLAVE porque el valor
    ''' depende de VR y de VRESL, no sólo del archivo.
    ''' <para>⛔ NO MEMOIZAR esta función: cachearía un estado del FILESYSTEM (si está el dll de VRESL) que
    ''' puede cambiar mientras el proceso vive. Rompe <c>Case17c</c> del LoadOrderActivatorProbe, donde el
    ''' dll se planta DESPUÉS de la primera consulta y el memo se queda con "vr".</para>
    ''' <para>El costo real es acotado: fuera de VR <see cref="IsVrBuild"/> está memoizado y devuelve False sin
    ''' tocar disco, así que no hay ni un <c>File.Exists</c>. Sólo en un rig VR se paga un stat por consulta.</para></summary>
    Private Shared Function GroupMemoVariant(dataPath As String) As String
        If Not IsVrBuild() Then Return "flat"
        Return If(VreslInstalled(dataPath), "vr+esl", "vr")
    End Function

    ''' <summary>Memo de <see cref="IsMasterGroup"/> con vida de proceso, clavada a la IDENTIDAD del archivo
    ''' (ruta + fecha de modificación + tamaño) y no sólo al nombre: la app REESCRIBE plugins, y un ESP al que
    ''' se le acaba de tildar "Mark as master" cambia de grupo sin cambiar de nombre.
    ''' <para>Por qué existe: MEDIDO sobre el rig real (50 activos, 49 <c>.esp</c>),
    ''' <b>4,558 ms por barrido</b> de sólo I/O, y <c>ReadActiveLoadOrder</c> no cachea nada. Peor:
    ''' <c>CheckSlotCap</c> lo llama y acto seguido vuelve a abrir el header de cada plugin, o sea 2× el
    ''' barrido por activación. Lineal en la cantidad de <c>.esp</c>: ~23 ms con 250.</para></summary>
    Private Shared ReadOnly _masterGroupMemo As New Dictionary(Of String, Boolean?)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Si un archivo puede usar el RANGO HARDCODED, o sea object ids por debajo de <c>0x800</c>.
    ''' La ley: el archivo necesita al menos un master; SSE (o SSE en VR con VRESL) lo permite desde la
    ''' versión 1.709 del HEDR, FO4 (no VR) desde la versión 1.0, y FO4VR nunca lo permite.
    ''' <para>Es POR ARCHIVO, no una constante del juego: depende de la VERSION DEL HEDR de ESE plugin y de
    ''' que tenga al menos un master. Dos plugins del mismo juego pueden dar respuestas distintas.</para>
    ''' <para>Y es asimétrico en VR, que es donde se pierde al leer rápido:
    ''' <list type="bullet">
    ''' <item><b>FO4VR nunca permite el rango.</b> Sólo el FO4 plano lo permite, tenga la
    ''' versión que tenga.</item>
    ''' <item>SSE en VR sí puede permitirlo, pero exige que esté instalado el plugin VRESL
    ''' (<see cref="VreslInstalled"/>).</item>
    ''' </list></para>
    ''' <para>Los demás juegos a los que se aplica esta ley en general (Morrowind, Starfield) no se
    ''' replican acá: no son juegos que esta app soporte.</para>
    ''' <para>Esta es la ÚNICA implementación de la ley. La usan el LECTOR
    ''' (<see cref="ResolveFormID"/>, con la versión leída del archivo) y el ESCRITOR
    ''' (<c>PluginWriter.AllowsHardcodedRange</c>, con la versión que emitimos). Un lector y un escritor con
    ''' dos copias de esta regla se desincronizan sin que nada avise.</para></summary>
    ''' <param name="hedrVersion">Campo Version del HEDR del archivo en cuestión.</param>
    ''' <param name="masterCount">Cuántos masters lista. La ley exige que sea mayor que cero.</param>
    ''' <param name="dataPath">Carpeta Data, sólo para detectar VRESL en un rig de VR.</param>
    Public Shared Function AllowsHardcodedRange(hedrVersion As Single, masterCount As Integer,
                                                dataPath As String) As Boolean
        If masterCount <= 0 Then Return False
        Dim isVr As Boolean = IsVrBuild()
        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then
            ' SSE plano siempre; SSE en VR sólo con VRESL.
            If isVr AndAlso Not VreslInstalled(dataPath) Then Return False
            Return hedrVersion >= 1.709F
        End If
        ' FO4 plano sí, FO4VR NO — la ley no lo contempla.
        If isVr Then Return False
        Return hedrVersion >= 1.0F
    End Function

    ''' <summary>Si el plugin pertenece al GRUPO MASTER del motor. Ley canónica: son DOS disyuntos
    ''' independientes y hay que evaluar los dos:
    ''' <code>
    ''' si el juego es FO4 o SSE (no una versión más vieja de la familia) y la extensión es .esm o
    ''' .esl: (A) grupo master
    ''' si el flag 0x01 (ESM) del header está puesto: (B) grupo master
    ''' </code>
    ''' <para>(A) aplica a nuestros dos juegos, que son los dos más nuevos de la familia que soportamos.
    ''' Pero para <c>.esl</c> hay además la precondición de <see cref="LightIsSupported"/>: está en la
    ''' GUARDA del <c>if</c> canónico, no en su cuerpo, y saltearla da un light de más en un rig VR sin
    ''' VRESL.</para>
    ''' <para>El flag LIGHT (0x200) por sí solo NO alcanza: es una bandera distinta de la de grupo master,
    ''' y el comparador de orden sólo mira la de grupo master. Un <c>.esp</c> con 0x200 y sin 0x01 es light y NO es grupo
    ''' master; un <c>.esp</c> con 0x201 SÍ lo es por (B). MEDIDO en el rig del usuario: los 30
    ''' <c>WM_ClonePack*.esp</c> son 0x201 (grupo master con extensión .esp) y <c>ShowCollectibles.esl</c> es
    ''' 0x200 (light SIN flag ESM, grupo master por (A)). Mirar un solo disyunto se equivoca en los dos.</para>
    ''' <para>Un archivo que no está en Data devuelve <c>Nothing</c>: no se puede saber su grupo, y el motor
    ''' tampoco lo carga. NO es lo mismo que False — ver <see cref="StablePartitionMasterGroup"/>, que por eso
    ''' no lo mueve.</para>
    ''' <para>Esta es la ÚNICA implementación de la ley; <c>LoadOrderActivator</c> la llama, no la copia
    ''' (00-reglas-paridad-canonica §15).</para></summary>
    Public Shared Function IsMasterGroup(dataPath As String, name As String,
                                         cache As Dictionary(Of String, Boolean?)) As Boolean?
        Dim hit As Boolean?
        If cache IsNot Nothing AndAlso cache.TryGetValue(name, hit) Then Return hit

        Dim value As Boolean? = Nothing
        Dim full = Path.Combine(dataPath, name)
        Dim fi As New FileInfo(full)
        If fi.Exists Then
            ' Clave por IDENTIDAD del archivo: si lo reescribimos, la memo caduca sola.
            ' La variante VR entra en la CLAVE: el valor depende de LightIsSupported (VR + VRESL), no sólo
            ' del archivo. Sin esto, cambiar el exe de Fallout4.exe a Fallout4VR.exe deja las mismas rutas,
            ' mtime y tamaño, y la memo seguiría devolviendo la respuesta del juego anterior.
            Dim variante = GroupMemoVariant(dataPath)
            Dim key = full & "|" & fi.LastWriteTimeUtc.Ticks.ToString() & "|" & fi.Length.ToString() & "|" & variante
            Dim memo As Boolean?
            Dim found As Boolean
            SyncLock _masterGroupMemo
                found = _masterGroupMemo.TryGetValue(key, memo)
            End SyncLock
            If found Then
                If cache IsNot Nothing Then cache(name) = memo
                Return memo
            End If

            If name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) Then
                value = True                                              ' (A) .esm
            ElseIf name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) AndAlso
                   LightIsSupported(dataPath) Then
                value = True                                              ' (A) .esl, con su precondición
            Else
                Try
                    Dim rdr As New PluginReader()
                    rdr.LoadHeaderOnly(full)
                    value = rdr.IsESM                                     ' (B)
                Catch
                    value = Nothing
                End Try
            End If
            SyncLock _masterGroupMemo
                _masterGroupMemo(key) = value
            End SyncLock
        End If
        If cache IsNot Nothing Then cache(name) = value
        Return value
    End Function

    ''' <summary>Aplica el 3er desempate del comparador de orden de carga: dentro del
    ''' tramo NO forzado, todo el GRUPO MASTER va antes que todo el resto, y adentro de cada grupo se
    ''' conserva el orden previo (partición ESTABLE).
    ''' <code>
    ''' si a y b están en el mismo grupo (los dos master o los dos no-master): gana el orden literal
    ''' de Plugins.txt
    ''' si no: gana el que sea del grupo master
    ''' </code>
    ''' <para>Es <c>Public</c> y toma <paramref name="dataPath"/> explícito, las dos cosas a propósito.
    ''' Explícito, porque leyendo <c>Config_App.Current.DataPath</c> por su cuenta es INGATEABLE: se puede
    ''' invertir la partición entera y el probe sigue dando 33/33. Pública, porque no la usa sólo el lector:
    ''' el Preflight de NPC Manager ordena con ELLA la selección del usuario, que es otro Plugins.txt virtual.
    ''' Una ley, una implementación (00-reglas-paridad-canonica §15).</para>
    ''' <para>Arranca en <paramref name="forcedCount"/> y no antes: los masters implícitos y el
    ''' Creation Club se ordenan por su propio índice forzado, que en el comparador
    ''' está ANTES del grupo. Particionarlos los reordenaría contra el motor.</para>
    ''' <para>⚠ Un plugin cuyo grupo NO se puede determinar (no está en Data) se queda CLAVADO en su índice:
    ''' no entra en ningún bucket. Para el motor ese módulo no existe (el propio comparador lo salta si no
    ''' puede leer el header), pero la app sí lo conserva en la lista, y esta lista es la
    ''' que <c>FilesDictionary.BuildArchivePriority</c> usa para dar prioridad a los BA2/BSA POR POSICIÓN. Si lo
    ''' mandáramos al fondo con los no-masters, el <c>.ba2</c> de un plugin desinstalado pasaría a ganarle el
    ''' conflicto de texturas a todos los mods que estaban después. <c>Nothing</c> no es False ni True: es
    ''' "no lo toques".</para>
    ''' <para>Por qué importa aunque hoy no se note: MEDIDO sobre los dos
    ''' rigs reales del usuario, 0 posiciones y 0 slots de diferencia — pero sólo porque los dos
    ''' <c>Plugins.txt</c> ya venían particionados. Control negativo del mismo arnés: moviendo UN grupo-master
    ''' (<c>ShowCollectibles.esl</c>) detrás de los no-masters, la lista se corre 20 posiciones y 9 plugins
    ''' cambian de slot. Sin esto, en ese rig la app le asigna a 9 mods un FileID que el motor no usa.</para></summary>
    ''' <param name="cache">Caché de grupo por NOMBRE, opcional. Pasar uno compartido cuando esto se llama en
    ''' bucle: sin él cada llamada arma el suyo y vuelve a hacer un <c>stat</c> por archivo. Con 1500 plugins y
    ''' un planner que itera, eso es la diferencia entre una pasada y N pasadas de I/O.</param>
    Public Shared Sub StablePartitionMasterGroup(ordered As List(Of String), forcedCount As Integer,
                                                 dataPath As String,
                                                 Optional cache As Dictionary(Of String, Boolean?) = Nothing)
        If ordered Is Nothing OrElse ordered.Count - forcedCount < 2 Then Exit Sub
        ' Sin Data no se puede leer un solo header, así que no hay grupo que decidir. La lista tampoco sirve
        ' para nada en ese estado (LoadAllPlugins falla igual), así que se devuelve el orden literal.
        If String.IsNullOrEmpty(dataPath) Then Exit Sub
        If cache Is Nothing Then cache = New Dictionary(Of String, Boolean?)(StringComparer.OrdinalIgnoreCase)
        Dim tail = ordered.GetRange(forcedCount, ordered.Count - forcedCount)

        Dim masters As New List(Of String)()
        Dim rest As New List(Of String)()
        Dim pinned As New Dictionary(Of Integer, String)()      ' índice EN EL TRAMO -> entrada sin grupo
        For k = 0 To tail.Count - 1
            Dim g = IsMasterGroup(dataPath, tail(k), cache)
            If Not g.HasValue Then
                pinned(k) = tail(k)                             ' grupo desconocido: NO se mueve
            ElseIf g.Value Then
                masters.Add(tail(k))
            Else
                rest.Add(tail(k))
            End If
        Next

        Dim rebuilt As New List(Of String)(tail.Count)
        Dim feed = masters.Concat(rest).GetEnumerator()
        For k = 0 To tail.Count - 1
            Dim stuck As String = Nothing
            If pinned.TryGetValue(k, stuck) Then
                rebuilt.Add(stuck)
            ElseIf feed.MoveNext() Then
                rebuilt.Add(feed.Current)
            End If
        Next

        ordered.RemoveRange(forcedCount, tail.Count)
        ordered.AddRange(rebuilt)
    End Sub

    Public Shared Function ReadActiveLoadOrder() As List(Of String)
        Dim isFO4 As Boolean = (Config_App.Current.Game = Config_App.Game_Enum.Fallout4)
        Dim gameDir = ResolveGameAppDataDir()
        ' "" cuando no se resolvió, y Path.Combine("", "x") devuelve "x" — o sea una ruta RELATIVA que se
        ' resolvería contra el directorio de trabajo y podría llegar a encontrar un archivo que no es. Con
        ' rutas vacías no se arma nada: el load order queda en los implícitos y LoadOrderSourceProblem() lo
        ' explica.
        Dim pluginsTxt = ResolvePluginsTxtPath()

        ' Implicit masters: el engine carga estos siempre primero, no aparecen en Plugins.txt.
        ' Spec verificada contra ejecución vanilla y contra LOOT.
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

        ' VR builds ship one extra force-loaded master, AFTER the DLCs: 'SkyrimVR.esm' for Skyrim,
        ' 'Fallout4_VR.esm' for FO4, appended to the tail of the official/DLC list and force-loaded
        ' regardless of Plugins.txt, exactly like the other implicit masters.
        ' Without it, every user mod in a VR session sits one index off the engine's, and VR mods that
        ' master this .esm can't resolve their FormIDs.
        If IsVrBuild() Then implicits.Add(If(isFO4, "Fallout4_VR.esm", "SkyrimVR.esm"))

        ' Creation Club content: Fallout4.ccc lives next to Fallout4.exe (the same folder that contains
        ' Data, one level up from wherever the game data path points).
        ' Each non-empty non-comment line is a plugin name the engine force-loads after the DLCs.
        ' Skyrim has its own Skyrim.ccc; same shape. Only attempted if FO4ExePath resolved.
        '
        ' A VR build has NO Creation Club list at all: the format declares the .ccc name only in the
        ' non-VR branch, for both games (wbDefinitionsFO4.pas: the assignment is the `else` of
        ' `if wbGameMode = gmFO4VR`; wbDefinitionsTES5.pas mirrors it for gmTES5VR). Reading a .ccc
        ' that a VR session never force-loads would insert plugins the engine does not, and every
        ' index after them would sit one off the engine's.
        Dim ccEntries As New List(Of String)
        Dim exePath = Config_App.Current.FO4ExePath
        If Not IsVrBuild() AndAlso Not String.IsNullOrEmpty(exePath) AndAlso File.Exists(exePath) Then
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
        Dim loadorderTxt = If(gameDir = "", "", Path.Combine(gameDir, "loadorder.txt"))
        Dim ordered As New List(Of String)
        If loadorderTxt <> "" AndAlso File.Exists(loadorderTxt) Then
            Dim implicitsSet As New HashSet(Of String)(implicits, StringComparer.OrdinalIgnoreCase)
            Dim ccSet As New HashSet(Of String)(ccEntries, StringComparer.OrdinalIgnoreCase)

            ' 1) Implicits at the front, in the hardcoded engine order.
            ordered.AddRange(implicits)

            ' 2) Creation Club entries next, skipping any that overlap with implicits.
            For Each p In ccEntries
                If implicitsSet.Contains(p) Then Continue For
                ordered.Add(p)
            Next

            ' Fin del tramo FORZADO (implícitos + Creation Club). Todo lo que sigue se particiona
            ' por grupo master al final; ver StablePartitionMasterGroup.
            Dim forcedCount = ordered.Count

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
            StablePartitionMasterGroup(ordered, forcedCount, Config_App.Current.DataPath)
            Return FilterOfficialIfRequested(ordered)
        End If

        ' Fallback: implicits + CC + Plugins.txt activos en orden literal.
        ordered.AddRange(implicits)
        For Each p In ccEntries
            If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then ordered.Add(p)
        Next
        Dim forcedCountFallback = ordered.Count
        For Each p In activeFromPluginsTxt
            If Not ordered.Any(Function(x) String.Equals(x, p, StringComparison.OrdinalIgnoreCase)) Then ordered.Add(p)
        Next
        StablePartitionMasterGroup(ordered, forcedCountFallback, Config_App.Current.DataPath)
        Return FilterOfficialIfRequested(ordered)
    End Function

    ''' <summary>Alias de <see cref="ReadActiveLoadOrder"/>, conservado por sus call sites. ⚠ El nombre
    ''' sugiere "todos los plugins de loadorder.txt"; la semántica real es "el load order ACTIVO".</summary>
    Public Shared Function ReadLoadOrder() As List(Of String)
        Return ReadActiveLoadOrder()
    End Function
End Class


