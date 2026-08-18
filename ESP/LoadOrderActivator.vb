Imports System.IO
Imports System.Text

''' <summary>
''' Turns a plugin ON in the game's <c>Plugins.txt</c> (the `*name.esp` marker the engine reads), and — only
''' when the current position actually breaks the "load after all your masters" rule — moves its line to the
''' first legal spot. Nothing else in the file is ever touched: no re-sort, no dedupe, no deactivation, no
''' rewrite of anybody else's line.
'''
''' <para><b>Why the position rules look the way they do.</b> The engine does NOT load in literal Plugins.txt
''' order: xEdit's engine-faithful comparator (wbLoadOrder.pas:188-224) puts every module flagged
''' <c>mfIsESM</c> before every module that isn't, and only WITHIN a group does the Plugins.txt index decide
''' (wbLoadOrder.pas:203). And for FO4/SSE <c>mfIsESM</c> comes from the .esm/.esl EXTENSION or from the ESM
''' header flag 0x01 (wbLoadOrder.pas:331-348) — the LIGHT flag 0x200 alone does NOT put a plugin in the
''' master group (wbLoadOrder.pas:368-369). Three consequences this class is built on:</para>
''' <list type="number">
''' <item>The app's own default output (<c>Name.esp</c> + Light flag, no ESM flag) is NOT in the master group,
''' so appending it at the end of the file is exactly where the engine will load it — and it shifts nobody
''' else's index.</item>
''' <item>An ESM-flagged plugin is hoisted into the master block by the engine no matter where its line sits,
''' so a new one is inserted at the END OF THE MASTER BLOCK. Otherwise the literal order we write would
''' disagree with the engine — and with <see cref="PluginManager.ReadActiveLoadOrder"/>, which is literal.</item>
''' <item>A master that lives in a group that always loads earlier can never be violated, so it is not even
''' considered. The flip side is the one case a move CANNOT fix: an ESM-flagged plugin whose master is a plain
''' .esp is hoisted ABOVE its own master wherever it is listed. That is reported as a warning, not "fixed".</item>
''' </list>
'''
''' <para><b>Ownership caveat (not fixable from here).</b> Mod Organizer keeps a per-profile Plugins.txt and
''' redirects this path through its VFS: run under MO2 the write lands on the profile file and is correct;
''' run outside it, MO2/Vortex may overwrite it at game launch and the activation is simply lost. The write
''' itself is still safe — worst case it is a no-op the user has to redo in their manager.</para>
''' </summary>
Public NotInheritable Class LoadOrderActivator

    Private Sub New()
    End Sub

    ''' <summary>Redirects the folder Plugins.txt / loadorder.txt are read from. Nothing in the shipped app —
    ''' the only writer is the self-test probe (via reflection), which cannot use the real load order as a test
    ''' bench: the phenomena that matter here (a move, a BOM, a full master block, a tripped cap) don't occur
    ''' in one user's file, and writing to it to create them is exactly what must never happen by accident.</summary>
    Private Shared _testGameDirOverride As String = Nothing

    ''' <summary>El Plugins.txt sobre el que se va a escribir, o "" + motivo si no hay uno confiable.
    '''
    ''' <para>⛔ ESTE ES EL PUNTO MÁS PELIGROSO DE TODA LA RESOLUCIÓN DE RUTAS, y hasta ahora fallaba para el
    ''' lado malo. La versión vieja componía la ruta contra la carpeta "preferida" aunque no existiera, y
    ''' <see cref="WriteEntries"/> la CREA: un usuario de la edición de GOG terminaba con un
    ''' <c>%LOCALAPPDATA%\Skyrim Special Edition\Plugins.txt</c> recién nacido, con un solo plugin adentro,
    ''' y un mensaje diciéndole que se había activado. El juego no lo leía nunca. Un archivo equivocado
    ''' escrito con cara de éxito es peor que un error.</para>
    '''
    ''' <para>Ahora: si el resolver no llegó a una ubicación, no se escribe nada. Sí se permite CREAR el
    ''' archivo cuando la carpeta salió de evidencia real (el .ini del juego está ahí, pero el usuario
    ''' todavía no corrió el juego) o cuando la ruta la fijó el usuario a mano — en los dos casos el destino
    ''' está confirmado por algo que no es una suposición nuestra.</para></summary>
    Private Shared Function ResolvePluginsTarget() As (Path As String, Problem As String)
        If Not String.IsNullOrEmpty(_testGameDirOverride) Then
            Return (IO.Path.Combine(_testGameDirOverride, "Plugins.txt"), "")
        End If
        Dim r = GamePathsResolver.Resolve()
        If Not r.HasPluginsTxt Then
            Return ("", If(r.Problem, "The game's load order file could not be located."))
        End If
        Return (r.PluginsTxtPath, "")
    End Function

    Public Enum OutcomeKind
        ''' <summary>Already active and already legally placed — the file was not written.</summary>
        NoOp
        ''' <summary>The `*` marker was added (and the entry inserted, if it was missing).</summary>
        Activated
        ''' <summary>The entry was moved because it sat before one of its masters (may also have been activated).</summary>
        Moved
        ''' <summary>A guard tripped: nothing was written. <see cref="Result.Summary"/> says which.</summary>
        Skipped
        ''' <summary>The write was attempted and failed; the file was restored from the backup.</summary>
        Failed
    End Enum

    Public NotInheritable Class Result
        Public Property Kind As OutcomeKind = OutcomeKind.Skipped
        ''' <summary>One-line, user-facing (English) description of what happened or why nothing did.</summary>
        Public Property Summary As String = ""
        ''' <summary>Non-fatal caveat worth surfacing even on success ("" when there is none) — e.g. the
        ''' ESM-mastering-an-ESP case, which no position can fix.</summary>
        Public Property Warning As String = ""
        Public Property PluginsTxtPath As String = ""
        Public Property BackupPath As String = ""
        ''' <summary>True when the file on disk was actually modified.</summary>
        Public ReadOnly Property Changed As Boolean
            Get
                Return Kind = OutcomeKind.Activated OrElse Kind = OutcomeKind.Moved
            End Get
        End Property
        ''' <summary>True when the user should be told (guard tripped, failure, or a caveat we can't fix).</summary>
        Public ReadOnly Property NeedsAttention As Boolean
            Get
                Return Kind = OutcomeKind.Skipped OrElse Kind = OutcomeKind.Failed OrElse Warning <> ""
            End Get
        End Property
    End Class

    ''' <summary>FormID index space, which is what really caps the load order: a full plugin owns the high
    ''' BYTE, and 0xFE/0xFF are reserved (0xFE is the light space), so 254 full slots. A light plugin owns a
    ''' 12-bit slot inside 0xFE ⇒ 4096. Same scheme <see cref="PluginManager.ToFaceGenLocalFormID"/> decodes.</summary>
    Private Const MAX_FULL_PLUGINS As Integer = 254
    Private Const MAX_LIGHT_PLUGINS As Integer = 4096

    ''' <summary>One physical line of Plugins.txt / loadorder.txt. <see cref="Raw"/> is kept verbatim so every
    ''' line we don't own is written back byte-for-byte (comments, blanks, odd spacing, unknown markers).</summary>
    Private NotInheritable Class Entry
        Public Property Raw As String = ""
        ''' <summary>Plugin filename, or "" when the line is blank / a comment (i.e. not an entry).</summary>
        Public Property Name As String = ""
        Public Property Active As Boolean
        Public ReadOnly Property IsPlugin As Boolean
            Get
                Return Name <> ""
            End Get
        End Property
    End Class

    ''' <summary>Text-file shape captured on read so the rewrite reproduces it exactly: a BOM the file already
    ''' had is preserved (and one it did NOT have is never introduced — a BOM on Plugins.txt would ride along
    ''' with the first entry's name), plus the line ending and the trailing-newline habit.</summary>
    Private NotInheritable Class FileShape
        Public Property HadBom As Boolean
        Public Property NewLine As String = vbCrLf
        Public Property TrailingNewLine As Boolean = True
    End Class

    ''' <summary>Activate <paramref name="pluginFullPath"/> in the current game's Plugins.txt, and move it if
    ''' (and only if) it currently sits before a master it depends on. Never throws: every failure path comes
    ''' back as <see cref="OutcomeKind.Skipped"/> or <see cref="OutcomeKind.Failed"/> with a reason.</summary>
    Public Shared Function Activate(pluginFullPath As String) As Result
        Dim res As New Result()
        Try
            If String.IsNullOrEmpty(pluginFullPath) OrElse Not File.Exists(pluginFullPath) Then
                res.Kind = OutcomeKind.Skipped
                res.Summary = "The plugin file was not found on disk, so it was not activated."
                Return res
            End If

            Dim pluginName = Path.GetFileName(pluginFullPath)
            Dim dataPath = Path.GetDirectoryName(pluginFullPath)

            ' Our own header: masters + the two facts that decide our sort group and our slot cost.
            Dim ourMasters As New List(Of String)
            Dim ourIsEsmGroup As Boolean
            Dim ourIsLight As Boolean
            Try
                Dim rdr As New PluginReader()
                rdr.LoadHeaderOnly(pluginFullPath)
                ourMasters.AddRange(rdr.Masters)
                ourIsEsmGroup = rdr.IsESM OrElse HasEsmExtension(pluginName)
                ' IsESL already folds in the .esl extension (PluginReader.ReadTES4, per wbLoadOrder.pas:362-363).
                ourIsLight = rdr.IsESL
            Catch ex As Exception
                res.Kind = OutcomeKind.Skipped
                res.Summary = "The plugin header could not be read, so the load order was left untouched: " & ex.Message
                Return res
            End Try

            Dim target = ResolvePluginsTarget()
            If target.Path = "" Then
                res.Kind = OutcomeKind.Skipped
                res.Summary = target.Problem &
                              " Nothing was written — set the Plugins.txt location in Setup and try again."
                Return res
            End If

            Dim pluginsTxt = target.Path
            Dim gameDir = If(Path.GetDirectoryName(pluginsTxt), "")
            res.PluginsTxtPath = pluginsTxt

            ' The engine rewrites its own copy of the load order on exit, so writing underneath a running
            ' game is a change that can silently disappear.
            Dim runningExe = RunningGameProcessName()
            If runningExe <> "" Then
                res.Kind = OutcomeKind.Skipped
                res.Summary = $"{runningExe} is running — Plugins.txt was left untouched. Activate the plugin after closing the game."
                Return res
            End If

            ' Vortex / Wrye Bash lock the load order by setting the read-only attribute. Clearing it would be
            ' taking ownership of a file another tool is actively managing.
            If File.Exists(pluginsTxt) AndAlso (File.GetAttributes(pluginsTxt) And FileAttributes.ReadOnly) <> 0 Then
                res.Kind = OutcomeKind.Skipped
                res.Summary = "Plugins.txt is read-only (a mod manager is locking the load order) — activate the plugin in that manager instead."
                Return res
            End If

            Dim shape As New FileShape()
            Dim entries = ReadEntries(pluginsTxt, shape)

            Dim ourIndex = IndexOfPlugin(entries, pluginName)
            Dim wasActive = ourIndex >= 0 AndAlso entries(ourIndex).Active

            ' A brand-new active plugin costs a slot; blowing the FormID index space makes the engine drop
            ' plugins, so this is a refuse-to-act guard, not a warning.
            If Not wasActive Then
                Dim capMsg = CheckSlotCap(dataPath, pluginName, ourIsLight, entries)
                If capMsg <> "" Then
                    res.Kind = OutcomeKind.Skipped
                    res.Summary = capMsg
                    Return res
                End If
            End If

            ' Masters that can actually be violated: same sort group (a master in an earlier group is always
            ' ahead of us) and listed as active (an inactive master is a broken dependency, not an ordering bug).
            Dim groupCache As New Dictionary(Of String, Boolean?)(StringComparer.OrdinalIgnoreCase)
            Dim unfixable As New List(Of String)
            Dim blocking As New List(Of String)
            For Each m In ourMasters
                Dim mIsEsmGroup = IsEsmGroup(dataPath, m, groupCache)
                If Not mIsEsmGroup.HasValue Then Continue For          ' not installed — nothing to order against
                If ourIsEsmGroup AndAlso Not mIsEsmGroup.Value Then
                    unfixable.Add(m)
                    Continue For
                End If
                If Not ourIsEsmGroup AndAlso mIsEsmGroup.Value Then Continue For
                blocking.Add(m)
            Next
            If unfixable.Count > 0 Then
                res.Warning = "This plugin carries the ESM flag but masters a plain .esp (" &
                              String.Join(", ", unfixable) & "). The engine loads every ESM-flagged plugin " &
                              "before every non-ESM one, so it will load BEFORE its own master no matter where " &
                              "it sits in the load order. Uncheck 'Mark as master (ESM flag)' to fix it."
            End If

            ' Decide the final line list. Ours is pulled out first so 'insert after the last blocking master'
            ' is computed against indices that already reflect its removal.
            Dim ourEntry As Entry
            If ourIndex >= 0 Then
                ourEntry = entries(ourIndex)
                entries.RemoveAt(ourIndex)
            Else
                ourEntry = New Entry With {.Name = pluginName}
            End If
            ourEntry.Active = True
            ourEntry.Raw = "*" & ourEntry.Name

            Dim desired = ComputeDesiredIndex(entries, ourIndex, ourIsEsmGroup, blocking, dataPath, groupCache)
            Dim moved = ourIndex >= 0 AndAlso desired <> ourIndex
            If ourIndex >= 0 AndAlso wasActive AndAlso Not moved Then
                res.Kind = OutcomeKind.NoOp
                res.Summary = $"{pluginName} was already active in the load order."
                Return res
            End If

            entries.Insert(desired, ourEntry)

            Dim backup As String = Nothing
            If Not WriteEntries(pluginsTxt, entries, shape, backup, res) Then Return res
            res.BackupPath = If(backup, "")

            ' Read back from disk and re-check the invariant we just wrote. A mismatch means something ate the
            ' write (AV, sync client, VFS); restoring the backup is preferable to leaving a half-applied order.
            Dim verifyShape As New FileShape()
            Dim written = ReadEntries(pluginsTxt, verifyShape)
            Dim vIdx = IndexOfPlugin(written, pluginName)
            If vIdx < 0 OrElse Not written(vIdx).Active OrElse vIdx < LastIndexOfAny(written, blocking) Then
                If backup IsNot Nothing AndAlso File.Exists(backup) Then File.Copy(backup, pluginsTxt, True)
                res.Kind = OutcomeKind.Failed
                res.Summary = "Plugins.txt did not verify after the write; the previous contents were restored."
                Return res
            End If

            ' loadorder.txt is NOT read by the engine, but LOOT/Vortex and this app's own
            ' PluginManager.ReadActiveLoadOrder use it as the ordering source — leaving it stale would make the
            ' app disagree with the game about where this plugin sits. Best effort: never fatal.
            Dim mirrorWarning = MirrorToLoadOrderTxt(gameDir, pluginName, blocking, ourIsEsmGroup, dataPath, groupCache)

            res.Kind = If(moved, OutcomeKind.Moved, OutcomeKind.Activated)
            If moved AndAlso Not wasActive Then
                res.Summary = $"{pluginName} was activated and moved after its masters in the load order."
            ElseIf moved Then
                res.Summary = $"{pluginName} was moved after its masters in the load order."
            ElseIf ourIndex >= 0 Then
                res.Summary = $"{pluginName} was activated in the load order."
            Else
                res.Summary = $"{pluginName} was added to the load order and activated."
            End If
            If mirrorWarning <> "" Then res.Warning = (res.Warning & If(res.Warning = "", "", vbCrLf)) & mirrorWarning
            Return res

        Catch ex As Exception
            res.Kind = OutcomeKind.Failed
            res.Summary = "The load order could not be updated: " & ex.Message
            Return res
        End Try
    End Function

    ' ========================================================================
    ' Parsing / writing
    ' ========================================================================

    Private Shared Function ResolveExistingOrDefault(dir As String, preferred As String, alternate As String) As String
        Dim a = Path.Combine(dir, preferred)
        If File.Exists(a) Then Return a
        Dim b = Path.Combine(dir, alternate)
        If File.Exists(b) Then Return b
        Return a
    End Function

    ''' <summary>Read the file into per-line entries, capturing its BOM / newline / trailing-newline shape in
    ''' <paramref name="shape"/>. A missing file yields an empty list and the default shape (CRLF, no BOM).</summary>
    Private Shared Function ReadEntries(filePath As String, shape As FileShape) As List(Of Entry)
        Dim result As New List(Of Entry)
        If Not File.Exists(filePath) Then Return result

        Dim bytes = File.ReadAllBytes(filePath)
        Dim offset As Integer = 0
        If bytes.Length >= 3 AndAlso bytes(0) = &HEF AndAlso bytes(1) = &HBB AndAlso bytes(2) = &HBF Then
            shape.HadBom = True
            offset = 3
        End If
        Dim text = New UTF8Encoding(False).GetString(bytes, offset, bytes.Length - offset)
        ' A file with no newline in it (single entry, no trailing newline) has no style to preserve — CRLF is
        ' then the right default: it is what the game and the mod managers write on Windows.
        shape.NewLine = If(text.Contains(vbCrLf), vbCrLf, If(text.Contains(vbLf), vbLf, vbCrLf))
        shape.TrailingNewLine = text.EndsWith(vbLf)

        Dim lines = text.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None).ToList()
        If shape.TrailingNewLine AndAlso lines.Count > 0 AndAlso lines(lines.Count - 1) = "" Then
            lines.RemoveAt(lines.Count - 1)
        End If

        For Each line In lines
            Dim e As New Entry With {.Raw = line}
            Dim t = line.Trim()
            If t.Length > 0 AndAlso Not t.StartsWith("#") AndAlso Not t.StartsWith(";") Then
                If t.StartsWith("*") Then
                    e.Active = True
                    t = t.Substring(1).Trim()
                End If
                If t.Length > 0 Then e.Name = t
            End If
            result.Add(e)
        Next
        Return result
    End Function

    ''' <summary>Backup (.npcm.bak) → write a temp file next to the target → atomic replace. Returns False and
    ''' fills <paramref name="res"/> on failure, leaving the original file untouched.</summary>
    Private Shared Function WriteEntries(filePath As String, entries As List(Of Entry), shape As FileShape,
                                         ByRef backupPath As String, res As Result) As Boolean
        Try
            Dim sb As New StringBuilder()
            For i = 0 To entries.Count - 1
                sb.Append(entries(i).Raw)
                If i < entries.Count - 1 OrElse shape.TrailingNewLine Then sb.Append(shape.NewLine)
            Next

            Dim dir = Path.GetDirectoryName(filePath)
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

            If File.Exists(filePath) Then
                backupPath = filePath & ".npcm.bak"
                File.Copy(filePath, backupPath, True)
            End If

            ' UTF8Encoding(False): the framework's Encoding.UTF8 emits a BOM, which would be prepended to the
            ' first entry's name and hide it from the engine. A BOM the file already had is reproduced instead.
            Dim payload = New UTF8Encoding(False).GetBytes(sb.ToString())
            Dim tmp = filePath & ".npcm.tmp"
            Using fs As New FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
                If shape.HadBom Then fs.Write(New Byte() {&HEF, &HBB, &HBF}, 0, 3)
                fs.Write(payload, 0, payload.Length)
                fs.Flush(True)
            End Using

            If File.Exists(filePath) Then
                File.Replace(tmp, filePath, Nothing, True)
            Else
                File.Move(tmp, filePath)
            End If
            Return True
        Catch ex As Exception
            res.Kind = OutcomeKind.Failed
            res.Summary = "Plugins.txt could not be written: " & ex.Message
            Return False
        End Try
    End Function

    ' ========================================================================
    ' Position helpers
    ' ========================================================================

    ''' <summary>Where our line must end up, given a list our own entry has ALREADY been removed from.
    ''' <paramref name="previousIndex"/> is where it used to be (-1 when it wasn't listed at all).
    ''' <list type="bullet">
    ''' <item>Already listed: keep the position the user or their mod manager chose. Only an actual violation
    ''' of "after all your masters" moves it — a cosmetic re-sort would shift other plugins' indices for
    ''' nothing.</item>
    ''' <item>Not listed: end of the master block for an ESM-group plugin (where the engine will load it
    ''' anyway), end of the list for anything else (index-preserving for every other plugin).</item>
    ''' </list></summary>
    Private Shared Function ComputeDesiredIndex(entries As List(Of Entry), previousIndex As Integer,
                                                ourIsEsmGroup As Boolean, blocking As List(Of String),
                                                dataPath As String,
                                                cache As Dictionary(Of String, Boolean?)) As Integer
        Dim desired As Integer
        If previousIndex < 0 Then
            desired = If(ourIsEsmGroup, EndOfMasterBlock(entries, dataPath, cache), EndOfPluginList(entries))
        Else
            desired = Math.Min(previousIndex, entries.Count)
        End If
        Dim minIndex = LastIndexOfAny(entries, blocking) + 1
        If desired < minIndex Then desired = minIndex
        If desired > entries.Count Then desired = entries.Count
        Return desired
    End Function

    Private Shared Function IndexOfPlugin(entries As List(Of Entry), name As String) As Integer
        For i = 0 To entries.Count - 1
            If entries(i).IsPlugin AndAlso String.Equals(entries(i).Name, name, StringComparison.OrdinalIgnoreCase) Then Return i
        Next
        Return -1
    End Function

    ''' <summary>Highest index among the ACTIVE entries whose name is in <paramref name="names"/>, or -1.
    ''' Inactive entries don't order anything: the engine never loads them.</summary>
    Private Shared Function LastIndexOfAny(entries As List(Of Entry), names As List(Of String)) As Integer
        If names Is Nothing OrElse names.Count = 0 Then Return -1
        Dim set0 As New HashSet(Of String)(names, StringComparer.OrdinalIgnoreCase)
        Dim last As Integer = -1
        For i = 0 To entries.Count - 1
            If entries(i).IsPlugin AndAlso entries(i).Active AndAlso set0.Contains(entries(i).Name) Then last = i
        Next
        Return last
    End Function

    ''' <summary>Insertion point right after the last master-group entry — where a mod manager would put a new
    ''' .esm/.esl. Falls back to the first plugin line (so a file that starts with the vanilla comment header
    ''' keeps it on top) and finally to the end of the file.</summary>
    Private Shared Function EndOfMasterBlock(entries As List(Of Entry), dataPath As String,
                                             cache As Dictionary(Of String, Boolean?)) As Integer
        Dim last As Integer = -1
        Dim firstPlugin As Integer = -1
        For i = 0 To entries.Count - 1
            If Not entries(i).IsPlugin Then Continue For
            If firstPlugin < 0 Then firstPlugin = i
            Dim g = IsEsmGroup(dataPath, entries(i).Name, cache)
            If g.HasValue AndAlso g.Value Then last = i
        Next
        If last >= 0 Then Return last + 1
        If firstPlugin >= 0 Then Return firstPlugin
        Return entries.Count
    End Function

    ''' <summary>Insertion point right after the last plugin line, so trailing blank lines stay trailing.</summary>
    Private Shared Function EndOfPluginList(entries As List(Of Entry)) As Integer
        For i = entries.Count - 1 To 0 Step -1
            If entries(i).IsPlugin Then Return i + 1
        Next
        Return entries.Count
    End Function

    Private Shared Function HasEsmExtension(name As String) As Boolean
        Return name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) OrElse
               name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Whether a plugin is in the engine's master group (wbLoadOrder.pas:331-348: .esm/.esl extension
    ''' OR the ESM header flag; the light flag alone is NOT enough). Nothing when the file is not in Data — an
    ''' entry pointing at an uninstalled plugin orders nothing. Header reads are cached per call site.</summary>
    Private Shared Function IsEsmGroup(dataPath As String, name As String,
                                       cache As Dictionary(Of String, Boolean?)) As Boolean?
        Dim hit As Boolean?
        If cache.TryGetValue(name, hit) Then Return hit

        Dim value As Boolean? = Nothing
        Dim full = Path.Combine(dataPath, name)
        If HasEsmExtension(name) Then
            value = If(File.Exists(full), CType(True, Boolean?), Nothing)
        ElseIf File.Exists(full) Then
            Try
                Dim rdr As New PluginReader()
                rdr.LoadHeaderOnly(full)
                value = rdr.IsESM
            Catch
                value = Nothing
            End Try
        End If
        cache(name) = value
        Return value
    End Function

    ''' <summary>"" when the plugin fits, else the refusal message. Counts the CURRENT effective load order
    ''' (<see cref="PluginManager.ReadActiveLoadOrder"/>: implicit masters + Creation Club + Plugins.txt
    ''' actives) split by slot space, and checks the one we are about to add.</summary>
    Private Shared Function CheckSlotCap(dataPath As String, pluginName As String, ourIsLight As Boolean,
                                         entries As List(Of Entry)) As String
        Try
            ' Under the self-test override the real load order is meaningless (and its implicit masters don't
            ' exist in the sandbox), so the count comes from the file under test instead.
            Dim active As List(Of String)
            If String.IsNullOrEmpty(_testGameDirOverride) Then
                active = PluginManager.ReadActiveLoadOrder()
            Else
                active = entries.Where(Function(e) e.IsPlugin AndAlso e.Active).Select(Function(e) e.Name).ToList()
            End If
            Dim full As Integer = 0
            Dim light As Integer = 0
            For Each n In active
                If String.Equals(n, pluginName, StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim p = Path.Combine(dataPath, n)
                If Not File.Exists(p) Then Continue For          ' listed but not installed: costs no slot
                If n.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) Then
                    light += 1
                    Continue For
                End If
                Try
                    Dim rdr As New PluginReader()
                    rdr.LoadHeaderOnly(p)
                    If rdr.IsESL Then light += 1 Else full += 1
                Catch
                    full += 1                                    ' unreadable: assume the expensive slot
                End Try
            Next

            If ourIsLight Then
                If light + 1 > MAX_LIGHT_PLUGINS Then
                    Return $"The load order already has {light} light plugins ({MAX_LIGHT_PLUGINS} is the engine limit), so it was left untouched."
                End If
            ElseIf full + 1 > MAX_FULL_PLUGINS Then
                Return $"The load order already has {full} full plugins ({MAX_FULL_PLUGINS} is the engine limit), so it was left untouched."
            End If
            Return ""
        Catch
            Return ""      ' the cap check is a safety net; its own failure must not block the activation
        End Try
    End Function

    ''' <summary>Name of the running game executable, or "" when it isn't running. The exe the user configured
    ''' decides which process to look for (same discriminator as <see cref="PluginManager.IsVrBuild"/>).</summary>
    Private Shared Function RunningGameProcessName() As String
        Try
            Dim exePath = If(Config_App.Current?.FO4ExePath, "")
            If exePath = "" Then Return ""
            Dim exeName = Path.GetFileNameWithoutExtension(exePath)
            If exeName = "" Then Return ""
            If Diagnostics.Process.GetProcessesByName(exeName).Length > 0 Then Return exeName & ".exe"
            Return ""
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>Apply the same presence/position rule to loadorder.txt when it exists. Returns "" on success
    ''' or when there is nothing to do, else a caveat string. The engine ignores this file, so a failure here
    ''' is never fatal — it only means tools may still show the old position.</summary>
    Private Shared Function MirrorToLoadOrderTxt(gameDir As String, pluginName As String, blocking As List(Of String),
                                                 ourIsEsmGroup As Boolean, dataPath As String,
                                                 cache As Dictionary(Of String, Boolean?)) As String
        Try
            ' gameDir sale del Plugins.txt vigente; si ese no tenía carpeta no hay dónde espejar.
            If gameDir = "" Then Return ""
            Dim loPath = ResolveExistingOrDefault(gameDir, "loadorder.txt", "LoadOrder.txt")
            If Not File.Exists(loPath) Then Return ""
            If (File.GetAttributes(loPath) And FileAttributes.ReadOnly) <> 0 Then
                Return "loadorder.txt is read-only, so only Plugins.txt was updated."
            End If

            Dim shape As New FileShape()
            Dim entries = ReadEntries(loPath, shape)
            ' loadorder.txt lists every plugin without activation markers, so an entry there is 'present'
            ' regardless of the `*` this class sets in Plugins.txt.
            For Each e In entries
                If e.IsPlugin Then e.Active = True
            Next

            Dim idx = IndexOfPlugin(entries, pluginName)
            Dim ourEntry As Entry
            If idx >= 0 Then
                ourEntry = entries(idx)
                entries.RemoveAt(idx)
            Else
                ourEntry = New Entry With {.Name = pluginName, .Raw = pluginName, .Active = True}
            End If

            Dim desired = ComputeDesiredIndex(entries, idx, ourIsEsmGroup, blocking, dataPath, cache)
            If idx = desired Then Return ""      ' already listed in a legal spot
            entries.Insert(desired, ourEntry)

            Dim backup As String = Nothing
            Dim throwaway As New Result()
            If Not WriteEntries(loPath, entries, shape, backup, throwaway) Then
                Return "Plugins.txt was updated, but loadorder.txt could not be: " & throwaway.Summary
            End If
            Return ""
        Catch ex As Exception
            Return "Plugins.txt was updated, but loadorder.txt could not be: " & ex.Message
        End Try
    End Function

End Class
