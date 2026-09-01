Imports System.IO
Imports System.Text

''' <summary>
''' Turns a plugin ON in the game's <c>Plugins.txt</c> (the `*name.esp` marker the engine reads), and — only
''' when the current position actually breaks the "load after all your masters" rule — moves its line to the
''' first legal spot. Nothing else in the file is ever touched: no re-sort, no dedupe, no deactivation, no
''' rewrite of anybody else's line.
'''
''' <para><b>Why the position rules look the way they do.</b> The engine does NOT load in literal Plugins.txt
''' order: it puts every module in the master group before every module that isn't, and only WITHIN a
''' group does the Plugins.txt index decide. And for FO4/SSE, master-group membership comes from the
''' .esm/.esl EXTENSION or from the ESM header flag 0x01 — the LIGHT flag 0x200 alone does NOT put a plugin
''' in the master group. Three consequences this class is built on:</para>
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
    ''' <para>ESTE ES EL PUNTO MÁS PELIGROSO DE TODA LA RESOLUCIÓN DE RUTAS. ⛔ NO componer la ruta contra
    ''' la carpeta "preferida" sin confirmar que existe, porque
    ''' <see cref="WriteEntries"/> la CREA: un usuario de la edición de GOG termina con un
    ''' <c>%LOCALAPPDATA%\Skyrim Special Edition\Plugins.txt</c> recién nacido, con un solo plugin adentro,
    ''' y un mensaje diciéndole que se activó. El juego no lo lee nunca. Un archivo equivocado
    ''' escrito con cara de éxito es peor que un error.</para>
    '''
    ''' <para>La ley: si el resolver no llegó a una ubicación, no se escribe nada. Sí se permite CREAR el
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
            Dim groupCache As New Dictionary(Of String, Boolean?)(StringComparer.OrdinalIgnoreCase)
            Dim ourIsEsmGroup As Boolean
            Dim ourIsLight As Boolean
            Try
                Dim rdr As New PluginReader()
                rdr.LoadHeaderOnly(pluginFullPath)
                ourMasters.AddRange(rdr.Masters)
                ' NO se re-escribe la disyunción acá. La ley del grupo master vive en UN solo lugar
                ' (PluginManager.IsMasterGroup) y tiene una precondición que es fácil
                ' perder: el disyunto por extensión .esl no vale en VR sin el plugin VRESL. Con la copia local
                ' (`rdr.IsESM OrElse HasEsmExtension(...)`) este archivo clasificaba NUESTRO plugin con una ley
                ' y al resto de las líneas con otra. El File.Exists ya está garantizado más arriba.
                ourIsEsmGroup = PluginManager.IsMasterGroup(dataPath, pluginName, groupCache).GetValueOrDefault()
                ' IsESL already folds in the .esl extension (PluginReader.ReadTES4).
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
            If Not VerificaEnDisco(pluginsTxt, pluginName, blocking, sinMarcadores:=False) Then
                ' Se restaura desde el respaldo de ESTA corrida (`backup`), que es el unico del que
                ' sabemos que contenido tiene: son los bytes de antes de esta escritura. Un `.npcm.bak`
                ' heredado de una caida anterior NO se usa acá y no se toca — es de otra version.
                Dim volvio = RestaurarDesdeRespaldo(pluginsTxt, backup)
                res.Kind = OutcomeKind.Failed
                res.Summary = "Plugins.txt did not verify after the write; " &
                              If(volvio, "the previous contents were restored.",
                                 "and the previous contents could NOT be restored automatically" &
                                 If(String.IsNullOrEmpty(backup), ".",
                                    " — they are in " & Path.GetFileName(backup) & "."))
                res.BackupPath = If(volvio, "", If(backup, ""))
                Return res
            End If

            ' ⛔ PUNTO DE CONFIRMACION. Recien acá la escritura está verificada contra el disco, y recien
            ' acá el respaldo deja de hacer falta. Es el mismo instante que el `Borrar(copia)` de
            ' `GuardarConCopia`, corrido hasta después del verify — que es exactamente por qué este
            ' método no puede usar `GuardarConCopia` y sí puede compartir su ley.
            ConfirmarRespaldo(backup)
            res.BackupPath = ""

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
            result.Add(ParsearLinea(line))
        Next
        Return result
    End Function

    ''' <summary>Una linea fisica → <see cref="Entry"/>. ESTA es la ley del formato de linea y vive acá
    ''' sola: `#` y `;` abren comentario, `*` marca activo, y lo que queda (recortado) es el nombre.
    ''' <para>Se extrajo de <see cref="ReadEntries"/> para poder PREGUNTARLE al parser —en vez de repetir
    ''' su regla— si un nombre de plugin sobrevive a la ida y vuelta por una linea. Repetir la regla es
    ''' justo lo que deja dos verdades cuando una de las dos cambia.</para></summary>
    Private Shared Function ParsearLinea(line As String) As Entry
        Dim e As New Entry With {.Raw = line}
        Dim t = line.Trim()
        If t.Length > 0 AndAlso Not t.StartsWith("#") AndAlso Not t.StartsWith(";") Then
            If t.StartsWith("*") Then
                e.Active = True
                t = t.Substring(1).Trim()
            End If
            If t.Length > 0 Then e.Name = t
        End If
        Return e
    End Function

    ''' <summary>Lee un archivo de orden y lo deja listo para razonar sobre POSICION.
    ''' <para><paramref name="sinMarcadores"/> describe EL FORMATO DEL ARCHIVO, no una accion:
    ''' `loadorder.txt` lista todos los plugins sin el `*` de activacion, asi que sin normalizar
    ''' <c>Active</c> todas sus lineas saldrian inactivas — y <see cref="LastIndexOfAny"/>, que filtra por
    ''' activas, devolveria -1 y toda comparacion de posicion contra los masters no mediria NADA.</para></summary>
    Private Shared Function LeerParaOrden(filePath As String, shape As FileShape,
                                          sinMarcadores As Boolean) As List(Of Entry)
        Dim entries = ReadEntries(filePath, shape)
        If sinMarcadores Then
            For Each e In entries
                If e.IsPlugin Then e.Active = True
            Next
        End If
        Return entries
    End Function

    ''' <summary>Relee <paramref name="filePath"/> DEL DISCO y vuelve a comprobar la invariante que la
    ''' escritura prometia dejar. Una sola implementacion para los dos archivos.
    ''' <para>⛔ NO compara bytes a proposito: otro proceso puede tocar lineas ajenas legitimamente. Lo
    ''' que se afirma es lo que la operacion prometio, y son tres cosas: nuestro plugin (1) esta, (2) esta
    ''' activo y (3) esta en o despues del ultimo master que lo bloquea. En un archivo sin marcadores la
    ''' (2) es cierta por construccion —ese formato no puede violarla— y se deja igual: la ley es una, y
    ''' lo que cambia es lo que el formato puede expresar, no lo que se exige.</para></summary>
    Private Shared Function VerificaEnDisco(filePath As String, pluginName As String,
                                            blocking As List(Of String), sinMarcadores As Boolean) As Boolean
        Dim verifyShape As New FileShape()
        Dim written = LeerParaOrden(filePath, verifyShape, sinMarcadores)
        Dim vIdx = IndexOfPlugin(written, pluginName)
        If vIdx < 0 Then Return False
        If Not written(vIdx).Active Then Return False
        If vIdx < LastIndexOfAny(written, blocking) Then Return False
        Return True
    End Function

    ''' <summary>Devuelve <paramref name="filePath"/> al contenido que tenia antes de ESTA corrida, desde
    ''' el respaldo que ESTA corrida tomo. True si el archivo volvio.
    ''' <para>⛔ UNA SOLA LEY DE RESTAURACION, tres call sites (el Catch de <see cref="WriteEntries"/>, el
    ''' verify de <see cref="Activate"/> y el del espejo). Si la restauracion sale bien, el archivo en
    ''' disco y el respaldo tienen los mismos bytes: el respaldo queda PROBADO REDUNDANTE y se confirma
    ''' —igual que el `Borrar(copia)` de `GuardarConCopia`—. Si no se pudo restaurar, se queda: es lo
    ''' unico que le queda al usuario, y el mensaje del llamador lo nombra.</para></summary>
    Private Shared Function RestaurarDesdeRespaldo(filePath As String, backupPath As String) As Boolean
        If String.IsNullOrEmpty(backupPath) OrElse Not File.Exists(backupPath) Then Return False
        Try
            File.Copy(backupPath, filePath, True)
        Catch
            Return False
        End Try
        ConfirmarRespaldo(backupPath)
        Return True
    End Function

    ''' <summary>Por que una linea que contiene SOLO el nombre del plugin no se relee como ese plugin, o
    ''' "" si se relee bien. Es la causa raiz del unico modo de falla del espejo que podemos nombrar.
    ''' <para>⛔ No repite la regla del formato: le PREGUNTA a <see cref="ParsearLinea"/>. Y el caracter
    ''' culpable sale del dato, no de una lista escrita a mano acá.</para>
    ''' <para>Pasa de verdad: `loadorder.txt` no lleva el `*` de activacion, asi que un plugin llamado
    ''' `#Patch.esp` o `;Fix.esp` —prefijos que los autores usan para forzar orden— se escribe como una
    ''' linea que vuelve a leerse como COMENTARIO. En `Plugins.txt` el mismo nombre sobrevive, porque ahi
    ''' la linea empieza con `*`. El formato no tiene escape: no hay forma de representarlo.</para></summary>
    Private Shared Function MotivoNoRepresentableSinMarcador(pluginName As String) As String
        If ParsearLinea(pluginName).Name = pluginName Then Return ""
        Dim primero = pluginName.Substring(0, 1)
        Return $"loadorder.txt could not be updated: the plugin name '{pluginName}' starts with '{primero}'," &
               " which that file's format reads as the start of a comment, so the entry would be invisible" &
               " to the tools that use it (LOOT, Vortex). loadorder.txt was restored to its previous" &
               " contents. Plugins.txt was updated correctly and the game will load the plugin — only" &
               $" those tools may show the old position. Rename the plugin so it does not start with '{primero}'" &
               " if you want them to show it correctly."
    End Function

    ''' <summary>Sufijo del respaldo. Los slots son `.npcm.bak`, `.npcm.bak2`, `.npcm.bak3`… y el nombre
    ''' libre lo decide <c>EscrituraEnElLugar.PrimerSlotLibre</c>, que es donde vive esa ley.</summary>
    Private Const SufijoRespaldo As String = ".npcm.bak"

    ''' <summary>El respaldo cumplio su vida: la escritura quedo CONFIRMADA y el archivo en disco es el
    ''' bueno, asi que el respaldo se borra.
    ''' <para>⛔ ESTO NO ES LIMPIEZA COSMETICA: es lo que le da sentido a la prueba de "heredado" en
    ''' <see cref="WriteEntries"/>. Mientras un `.npcm.bak` solo se creara y nunca se borrara, su
    ''' presencia no significaba nada (siempre estaba) y no habia forma de distinguir un Plugins.txt
    ''' bueno de uno que quedo a medias sin inventar una heuristica sobre el texto. Con el borrado al
    ''' confirmar, la presencia del archivo ES la señal: <b>hay respaldo ⟺ la ultima escritura nunca se
    ''' confirmo</b>.</para>
    ''' <para>Lo que se pierde y se dice: hasta 1.4.1 el `.npcm.bak` quedaba en disco para siempre y
    ''' servia de "deshacer la activacion" a mano. Nadie lo documentaba ni lo ofrecia en la UI, y
    ''' <c>Result.BackupPath</c> no lo lee ningun consumidor.</para></summary>
    Private Shared Sub ConfirmarRespaldo(backupPath As String)
        If String.IsNullOrEmpty(backupPath) Then Return
        BorrarSilencioso(backupPath)
    End Sub

    Private Shared Sub BorrarSilencioso(ruta As String)
        If String.IsNullOrEmpty(ruta) Then Return
        Try
            File.Delete(ruta)
        Catch
            ' Best-effort: un respaldo huerfano no rompe nada — la proxima corrida lo trata como heredado.
        End Try
    End Sub

    ''' <summary>Backup (.npcm.bak) → escritura EN EL LUGAR sobre el archivo que ya esta. Devuelve False y
    ''' llena <paramref name="res"/> ante un fallo.
    ''' <para>⛔ NO es atomica y NO deja el original intacto ante un fallo: por eso el Catch restaura desde
    ''' el respaldo. El `.tmp` + `File.Replace` que habia aca si era atomico, pero `ReplaceFileW` no esta
    ''' entre las funciones que virtualiza el VFS de Mod Organizer, asi que escribia el Plugins.txt REAL en
    ''' vez del que MO2 le presenta al perfil.</para>
    ''' <para>⛔ EL LLAMADOR TIENE QUE CONFIRMAR. Este metodo deja el respaldo VIVO a proposito; quien
    ''' llama es responsable de <see cref="ConfirmarRespaldo"/> cuando la escritura quedo verificada. Si
    ''' no lo hace, el respaldo queda en disco y la proxima corrida lo trata como heredado — que es el
    ''' lado seguro del error, pero deja un archivo de mas.</para></summary>
    Private Shared Function WriteEntries(filePath As String, entries As List(Of Entry), shape As FileShape,
                                         ByRef backupPath As String, res As Result) As Boolean
        Dim respaldoOk As Boolean = False
        Try
            Dim sb As New StringBuilder()
            For i = 0 To entries.Count - 1
                sb.Append(entries(i).Raw)
                If i < entries.Count - 1 OrElse shape.TrailingNewLine Then sb.Append(shape.NewLine)
            Next

            Dim dir = Path.GetDirectoryName(filePath)
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)

            If File.Exists(filePath) Then
                ' ⛔ EL RESPALDO HEREDADO NO SE PISA — y esto NO es una heuristica sobre el texto.
                ' Con el borrado al confirmar (`ConfirmarRespaldo`), que haya un `.npcm.bak` en disco
                ' significa una sola cosa: la corrida anterior NUNCA confirmo su escritura. O sea que el
                ' Plugins.txt que estamos por copiar es el que quedo de esa corrida — puede estar a
                ' medias (la escritura en el lugar no es atomica y el texto plano parsea igual) — y el
                ' `.npcm.bak` es la unica version buena que le queda al usuario. Pisarlo con el
                ' degradado destruia el respaldo justo cuando hacia falta, y el verify no lo veia.
                ' El nombre lo decide la MISMA ley que la copia de GuardarConCopia; vive en un solo lado.
                backupPath = BSA_BA2_Library_DLL.EscrituraEnElLugar.PrimerSlotLibre(filePath, SufijoRespaldo)
                Try
                    File.Copy(filePath, backupPath, True)
                    ' ⛔ EXISTIR NO ES ESTAR COMPLETA. `File.Copy` puede dejar el respaldo a medias
                    ' (disco lleno, error de I/O) y lo que queda PARECE un respaldo. Restaurar desde uno
                    ' truncado es destruir la orden de carga con cara de rescate. Se compara el tamano.
                    respaldoOk = (New FileInfo(backupPath).Length = New FileInfo(filePath).Length)
                    If Not respaldoOk Then BorrarSilencioso(backupPath)
                Catch
                    respaldoOk = False
                    BorrarSilencioso(backupPath)
                End Try

                ' ⛔ SIN RED NO SE TRUNCA. Misma ley que `GuardarConCopia`: si hay contenido que perder y
                ' no se pudo respaldar, NO se toca el original. Antes se escribia igual con
                ' `respaldoOk = False`, y un corte a mitad dejaba al usuario sin orden de carga y sin
                ' nada para volver. Es un guard que no escribe nada, o sea Skipped, no Failed.
                If Not respaldoOk Then
                    backupPath = Nothing
                    res.Kind = OutcomeKind.Skipped
                    res.Summary = $"'{Path.GetFileName(filePath)}' was NOT modified: its backup could not" &
                                  " be created. Free up disk space or close whatever is holding that file," &
                                  " then try again."
                    Return False
                End If
            End If

            ' UTF8Encoding(False): the framework's Encoding.UTF8 emits a BOM, which would be prepended to the
            ' first entry's name and hide it from the engine. A BOM the file already had is reproduced instead.
            Dim payload = New UTF8Encoding(False).GetBytes(sb.ToString())

            ' Se escribe ENCIMA del archivo que ya está. ⛔ NO volver al `.npcm.tmp` + `File.Replace`:
            ' `ReplaceFileW` no está entre las funciones que virtualiza el VFS de Mod Organizer, así que
            ' escribía el Plugins.txt REAL en vez del que MO2 le presenta al perfil.
            ' Acá NO se usa `GuardarConCopia`: este método ya hace su propio respaldo unas líneas arriba
            ' (`backupPath`), y ése tiene otra vida — sobrevive a la escritura porque lo consume el
            ' rollback del verify de `Activate`, que corre DESPUÉS de que la escritura salió bien.
            ' ⛔ Eso justifica dos ARTEFACTOS, no dos LEYES. La ley es una sola y es la de
            ' `GuardarConCopia`: «un respaldo existe exactamente mientras el archivo que protege NO está
            ' confirmado; se toma antes de escribir, no se pisa si ya había uno, y se borra en el
            ' instante en que la escritura queda confirmada». Lo único que cambia entre los dos es DÓNDE
            ' está ese instante — acá es el verify de relectura (`ConfirmarRespaldo`), no el retorno.
            ' `sincronizar:=True`: esto es dato del usuario. El costo medido (+2,66 ms para un archivo
            ' del tamaño de Plugins.txt) se paga UNA vez por activación.
            BSA_BA2_Library_DLL.EscrituraEnElLugar.Escribir(
                filePath,
                Sub(fs)
                    If shape.HadBom Then fs.Write(New Byte() {&HEF, &HBB, &HBF}, 0, 3)
                    fs.Write(payload, 0, payload.Length)
                End Sub,
                sincronizar:=True)
            Return True
        Catch ex As Exception
            ' La escritura en el lugar puede dejar el archivo a medias (no es atómica): si hay respaldo,
            ' se restaura acá. Sin esto, un fallo a mitad dejaba al usuario sin orden de carga y el
            ' rollback de `Activate` no corre, porque sólo cubre el camino en que la escritura SALIÓ BIEN.
            ' Misma ley de restauracion que el verify de `Activate` y el del espejo — una funcion, tres
            ' call sites. Si vuelve, el respaldo esta probado redundante y se confirma; si no, se queda en
            ' disco (es lo unico que le queda al usuario), el mensaje lo nombra y la proxima corrida lo
            ' trata como HEREDADO y no lo pisa.
            Dim restaurado As Boolean = respaldoOk AndAlso RestaurarDesdeRespaldo(filePath, backupPath)
            res.Kind = OutcomeKind.Failed
            res.Summary = "Plugins.txt could not be written: " & ex.Message &
                          If(respaldoOk AndAlso Not restaurado,
                             " (a backup of the previous contents is at " & Path.GetFileName(backupPath) & ")",
                             "")
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

        ' ⛔ NO REPONER acá un "tope del grupo de masters" (bajar nuestra línea hasta el primer no-master).
        '
        ' La ley canónica es que el motor PARTICIONA: con dos módulos del MISMO
        ' grupo desempata por índice literal de Plugins.txt, y con dos de grupos DISTINTOS el master gana
        ' siempre, esté donde esté la línea. O sea que la posición de un plugin del grupo master RESPECTO DE
        ' LOS NO-MASTERS no puede cambiar quién gana un override: un tope que sólo mueve esa relación no
        ' arregla nada por construcción, y MEDIDO rompe dos cosas: (a) en la rama "plugin nuevo" baja
        ' nuestro ESP por encima de OTRO grupo-master que esté más abajo, y ahí sí le cambia la precedencia
        ' —un tercero le gana a nuestro propio output—; (b) `MirrorToLoadOrderTxt` fuerza `Active = True` en
        ' todas las líneas y llama a ESTA MISMA función, así que un tope que filtre por `.Active` es una ley
        ' con dos verdades, y deja Plugins.txt y loadorder.txt en desacuerdo sobre quién gana.
        '
        ' La discrepancia real entre la app y el motor sobre quién gana vive en el LECTOR, y ahí se aplica:
        ' `PluginManager.StablePartitionMasterGroup`.
        '
        ' `minIndex` (los masters que nos bloquean) sí se queda: es la otra ley, "después de tus masters".
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


    ''' <summary>Whether a plugin is in the engine's master group (.esm/.esl extension
    ''' OR the ESM header flag; the light flag alone is NOT enough). Nothing when the file is not in Data — an
    ''' entry pointing at an uninstalled plugin orders nothing. Header reads are cached per call site.</summary>
    Private Shared Function IsEsmGroup(dataPath As String, name As String,
                                       cache As Dictionary(Of String, Boolean?)) As Boolean?
        Return PluginManager.IsMasterGroup(dataPath, name, cache)
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

            ' loadorder.txt lists every plugin without activation markers, so an entry there is 'present'
            ' regardless of the `*` this class sets in Plugins.txt. La normalizacion vive en LeerParaOrden,
            ' que es la MISMA puerta por la que relee el verify de mas abajo: si el escritor y el verify
            ' leyeran distinto, el verify estaria midiendo otro archivo que el que se escribio.
            Dim shape As New FileShape()
            Dim entries = LeerParaOrden(loPath, shape, sinMarcadores:=True)

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
            ' ⛔ MISMO VERIFY QUE Plugins.txt, y por eso mismo punto de confirmación. Antes acá se
            ' confirmaba apenas `WriteEntries` volvía sin tirar, así que cualquier cosa que dejara el
            ' archivo degradado se llevaba el respaldo puesta y en silencio. La única diferencia con
            ' `Activate` es `sinMarcadores`, que describe el FORMATO — la ley es la misma función.
            If Not VerificaEnDisco(loPath, pluginName, blocking, sinMarcadores:=True) Then
                Dim volvio = RestaurarDesdeRespaldo(loPath, backup)
                ' Se nombra la CAUSA cuando la sabemos. El único modo de falla que este método puede
                ' explicar es que el nombre no sobreviva la ida y vuelta por una línea sin marcador, y
                ' eso se lo pregunta al parser, no a una regla repetida acá.
                Dim causa = MotivoNoRepresentableSinMarcador(pluginName)
                If causa <> "" Then
                    Return causa & If(volvio, "",
                                      " (the previous contents could NOT be restored automatically" &
                                      If(String.IsNullOrEmpty(backup), ".",
                                         " — they are in " & Path.GetFileName(backup) & ".") & ")")
                End If
                Return "Plugins.txt was updated, but loadorder.txt did not verify after the write" &
                       If(volvio, " and was restored to its previous contents.",
                          " and could NOT be restored automatically" &
                          If(String.IsNullOrEmpty(backup), ".",
                             " — the previous contents are in " & Path.GetFileName(backup) & "."))
            End If

            ' Sin este llamado el respaldo del espejo quedaría en disco para siempre y la corrida
            ' siguiente lo trataría como heredado, acumulando un `.npcm.bak2`, `.bak3`… por activación.
            ConfirmarRespaldo(backup)
            Return ""
        Catch ex As Exception
            Return "Plugins.txt was updated, but loadorder.txt could not be: " & ex.Message
        End Try
    End Function

End Class
