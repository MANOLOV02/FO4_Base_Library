Imports System.IO
Imports System.Linq
Imports System.Text

' ============================================================================================
' El escritor de plugins UNO SOLO, extraido de SaveNpcEspWriter.
'
' ⛔ PASO 1 del plan (L7) = REFACTOR PURO: todo lo que esta aca salio de SaveNpcEspWriter.SaveOverridePlugin
' sin cambiar una coma de su conducta, y SaveOverridePlugin pasa a llamarlo. La prueba es de bytes: los
' guardados de NPC Manager y Wardrobe Manager (golden L0 + OutfitDraftSaveGate, HeadPartSaveGate,
' ChargenFlagSaveGate, WmEscrituraGate, los round-trip probes) tienen que salir identicos.
'
' PASO 2 = los parametros que necesita un escritor que no es el de NPC (Item_Sorter), con defaults que son
' la conducta NPC:
'   - autor del TES4 (CNAM) y si va el SNAM con el tag de codificacion;
'   - el master del juego siempre en la MAST (CleanMasters de xEdit lo conserva siempre, wbI:3136-3140);
'   - orden de GRUP declarado por el esquema (wbAddGroupOrder) en vez del orden fijo de este writer;
'   - orden de records dentro de cada GRUP dado por el llamador (xEdit: CompareGroupContents, wbI:18310-18434).
' ============================================================================================

''' <summary>La vista minima de un record armado fuera de un lector (el arbol ya en texto, sin tablas de idioma).</summary>
Public NotInheritable Class GenericRecordView
    Inherits Canon.CanonRecordView
    Public Sub New(node As Canon.WbNode, ctx As Canon.WbContext)
        MyBase.New(node, ctx, Nothing)
    End Sub
End Class

''' <summary>Un record a escribir por <see cref="GenericPluginWriter.Save"/>.</summary>
Public NotInheritable Class GenericRecordEntry
    ''' <summary>El record. Su contexto dice firma, banderas y version de formulario (SerializarRecord).</summary>
    Public ReadOnly Property Record As Canon.CanonView
    ''' <summary>FormID en el espacio del ORDEN DE CARGA, o un provisional (byte alto 0xFF) para un record nuevo.</summary>
    Public ReadOnly Property FormID As UInteger
    Public ReadOnly Property Vcs1 As UInteger
    Public ReadOnly Property Vcs2 As UShort

    Public Sub New(record As Canon.CanonView, formID As UInteger, vcs1 As UInteger, vcs2 As UShort)
        If record Is Nothing Then
            Throw New ArgumentNullException(NameOf(record), "El cuerpo del record sale del arbol: sin el no hay nada que grabar.")
        End If
        _Record = record
        _FormID = formID
        _Vcs1 = vcs1
        _Vcs2 = vcs2
    End Sub

    Public ReadOnly Property Signature As String
        Get
            Return _Record.Context.RecordSignature
        End Get
    End Property

    Public ReadOnly Property IsNew As Boolean
        Get
            Return GenericPluginWriter.IsProvisionalDraftFormID(_FormID)
        End Get
    End Property
End Class

''' <summary>Parametros del TES4 y del armado del archivo.</summary>
Public NotInheritable Class GenericSaveOptions
    Public Game As Config_App.Game_Enum = Config_App.Game_Enum.Fallout4
    Public MarkAsMaster As Boolean
    Public LightMaster As Boolean
    ''' <summary>CNAM del TES4.</summary>
    Public Author As String = NPC_MANAGER_AUTHOR_CNAM
    ''' <summary>SNAM "Plugin encoding: &lt;cp:XXXX&gt;" (convencion de NPC Manager). False = sin SNAM.</summary>
    Public WriteEncodingSnam As Boolean = True
    ''' <summary>True = el master del juego queda en la MAST aunque nada lo referencie (CleanMasters, wbI:3136-3140).</summary>
    Public AlwaysKeepGameMaster As Boolean
    ''' <summary>Orden de los GRUP de primer nivel. Toda firma a escribir tiene que estar; un GRUP vacio no se
    ''' escribe (wbI:18153-18164).</summary>
    Public GroupOrder As IReadOnlyList(Of String)
    ''' <summary>Clave de orden de los records dentro de cada GRUP (comparacion SIN signo, estable). Recibe el
    ''' record y, si es nuevo, el object id que le toco (0 si no). Nothing = el orden de la lista de entrada.
    ''' xEdit ordena por <c>LoadOrderFormID</c> (CompareGroupContents, wbI:18392-18400, con
    ''' <c>wbDisplayLoadOrderFormID := True</c> en xeMainForm.pas:5150); la clave la arma quien conoce el slot.</summary>
    Public RecordSortKey As Func(Of GenericRecordEntry, UInteger, UInteger)
    ''' <summary>Contador del HEDR del archivo que se reemplaza (0 = archivo nuevo, arranca en 0x800).</summary>
    Public ExistingNextObjectId As UInteger
    ''' <summary>MAST del archivo que se reemplaza, para preservar el orden de los sobrevivientes.</summary>
    Public ExistingMasters As List(Of String)
    ''' <summary>Records NUEVOS de otro archivo de salida ya escrito (una parte anterior): FormID provisional → (archivo,
    ''' object id real). Esos archivos no estan en el orden de carga: si se referencian van como masters DESPUES de todos
    ''' los cargados, en el orden de <see cref="ExternalFileOrder"/> (el orden en que se crearon).</summary>
    Public ExternalDrafts As Dictionary(Of UInteger, KeyValuePair(Of String, UInteger))
    Public ExternalFileOrder As IReadOnlyList(Of String)
End Class

''' <summary>Resultado de <see cref="GenericPluginWriter.Save"/>.</summary>
Public NotInheritable Class GenericSaveResult
    Public OutputPath As String
    Public MasterList As New List(Of String)
    Public MasterAudit As New Dictionary(Of String, List(Of UInteger))(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Provisional → FormID LOCAL del archivo escrito.</summary>
    Public DraftFormIdMap As New Dictionary(Of UInteger, UInteger)
    Public Advertencias As New List(Of String)
    Public RecordCount As Integer
End Class

Public Module GenericPluginWriter

    ''' <summary>True si un FormID es el centinela provisional de un record nuevo. La ley vive en SaveNpcEspWriter
    ''' (<c>FormIdAltoDeBorrador</c>); aca solo se reenvia.</summary>
    Friend Function IsProvisionalDraftFormID(formID As UInteger) As Boolean
        Return SaveNpcEspWriter.IsProvisionalDraftFormID(formID)
    End Function

    ' =========================================================================================== MAST

    ''' <summary>
    ''' La MAST del archivo, derivada de lo que la pasada de DESCUBRIMIENTO vio escribir (SaveNpcEspWriter Pasos
    ''' 2a–2c): sobrevivientes de la MAST previa en su orden, los nuevos en orden de carga, y todo ordenado por
    ''' orden de carga con el master del juego primero. Con <paramref name="alwaysKeepGameMaster"/> el master del
    ''' juego entra aunque nadie lo referencie (xEdit CleanMasters: <c>SameText(flMasters[i].FileName,
    ''' wbGameMasterESM)</c>, wbI:3136-3140).
    ''' </summary>
    Public Function BuildMasterList(existingMasters As List(Of String),
                                    referencedPluginNames As HashSet(Of String),
                                    pluginManager As PluginManager,
                                    gameMaster As String,
                                    alwaysKeepGameMaster As Boolean) As List(Of String)
        Dim sortedMasters As New List(Of String)
        Dim seenLower As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each oldM In existingMasters
            If seenLower.Contains(oldM) Then Continue For
            If referencedPluginNames.Contains(oldM) OrElse String.Equals(oldM, gameMaster, StringComparison.OrdinalIgnoreCase) Then
                sortedMasters.Add(oldM)
                seenLower.Add(oldM)
            End If
        Next

        For Each plugin In pluginManager.Plugins
            If plugin Is Nothing Then Continue For
            If seenLower.Contains(plugin.FileName) Then Continue For
            If referencedPluginNames.Contains(plugin.FileName) OrElse
               (alwaysKeepGameMaster AndAlso String.Equals(plugin.FileName, gameMaster, StringComparison.OrdinalIgnoreCase)) Then
                sortedMasters.Add(plugin.FileName)
                seenLower.Add(plugin.FileName)
            End If
        Next

        Dim loadOrderRank As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To pluginManager.Plugins.Count - 1
            Dim pl = pluginManager.Plugins(i)
            If pl IsNot Nothing AndAlso Not loadOrderRank.ContainsKey(pl.FileName) Then loadOrderRank(pl.FileName) = i
        Next
        Return sortedMasters.
            OrderBy(Function(m) If(String.Equals(m, gameMaster, StringComparison.OrdinalIgnoreCase), -1, 0)).
            ThenBy(Function(m)
                       Dim r As Integer
                       Return If(loadOrderRank.TryGetValue(m, r), r, Integer.MaxValue)
                   End Function).
            ToList()
    End Function

    ''' <summary>El remapper de la pasada de DESCUBRIMIENTO: anota el plugin dueño de cada FormID y lo devuelve
    ''' sin tocar (SaveNpcEspWriter Paso 2).</summary>
    Public Function DiscoveryRemapper(pluginManager As PluginManager, outName As String,
                                      referencedPluginNames As HashSet(Of String),
                                      auditPerPlugin As Dictionary(Of String, HashSet(Of UInteger))) As SaveNpcEspWriter.FormIdRemapper
        Return Function(g As UInteger) As UInteger
                   If g = 0UI Then Return 0UI
                   If IsProvisionalDraftFormID(g) Then Return g
                   Dim pn = pluginManager.GetOriginatingPluginName(g)
                   If String.IsNullOrEmpty(pn) Then
                       Throw New InvalidOperationException(
                           $"FormID {g:X8} does not belong to any loaded plugin, so it cannot be re-mastered into the output.")
                   End If
                   If Not String.Equals(pn, outName, StringComparison.OrdinalIgnoreCase) Then
                       referencedPluginNames.Add(pn)
                       Dim lst As HashSet(Of UInteger) = Nothing
                       If Not auditPerPlugin.TryGetValue(pn, lst) Then
                           lst = New HashSet(Of UInteger)
                           auditPerPlugin(pn) = lst
                       End If
                       lst.Add(g)
                   End If
                   Return g
               End Function
    End Function

    ''' <summary>El remapper REAL (SaveNpcEspWriter Paso 3): provisional → draftRemap; el resto por
    ''' <see cref="PluginManager.TryMapGlobalToFileLocal"/>. Los dos fallos son aserciones.</summary>
    Public Function FinalRemapper(pluginManager As PluginManager, outName As String,
                                  masterIndexLookup As Dictionary(Of String, Integer), selfMasterIdx As Integer,
                                  draftRemap As Dictionary(Of UInteger, UInteger)) As SaveNpcEspWriter.FormIdRemapper
        Return Function(globalFormID As UInteger) As UInteger
                   If globalFormID = 0UI Then Return 0UI
                   If IsProvisionalDraftFormID(globalFormID) Then
                       Dim mappedDraft As UInteger
                       If draftRemap.TryGetValue(globalFormID, mappedDraft) Then Return mappedDraft
                       Throw New InvalidOperationException(
                           $"Draft FormID {globalFormID:X8} is referenced by a record being written but no draft " &
                           "record claims it, so it cannot be given a real FormID. A referenced draft was most " &
                           "likely cancelled or deleted while something still pointed at it.")
                   End If
                   Dim mappedLocal As UInteger = 0UI
                   Dim mapRes = pluginManager.TryMapGlobalToFileLocal(globalFormID, masterIndexLookup, selfMasterIdx, outName, mappedLocal)
                   If mapRes = PluginManager.FileLocalMapResult.Ok Then Return mappedLocal
                   Dim pname = pluginManager.GetOriginatingPluginName(globalFormID)
                   If mapRes = PluginManager.FileLocalMapResult.NoOwner Then
                       Throw New InvalidOperationException(
                           $"FormID {globalFormID:X8} does not belong to any loaded plugin, so it cannot be " &
                           "re-mastered into the output. Writing it unchanged would silently repoint it at " &
                           "whichever plugin occupies that index in the new master list.")
                   End If
                   Throw New InvalidOperationException(
                       $"FormID {globalFormID:X8} is owned by '{pname}', which is not in the master list " &
                       "being written. The master list is built from the discovery pass over this very " &
                       "emission walk, so every plugin reached here should already be in it — this means " &
                       "the two passes disagreed. Writing it unchanged would silently repoint the " &
                       "reference at whichever plugin occupies that index.")
               End Function
    End Function

    ' =========================================================================================== object ids

    ''' <summary>
    ''' El repartidor de object ids de un archivo (SaveNpcEspWriter Paso 3): ancho por la LEY del slot
    ''' (<see cref="PluginManager.IsLightSlot"/>), piso por archivo (<see cref="PluginWriter.AllowsHardcodedRange"/>),
    ''' semilla del HEDR con recuperacion, salteo de ocupados y error duro al agotarse.
    ''' </summary>
    Public NotInheritable Class ObjectIdAllocator
        Private ReadOnly _pm As PluginManager
        Private ReadOnly _outName As String
        Private ReadOnly _masterIndexLookup As Dictionary(Of String, Integer)
        Private ReadOnly _selfMasterIdx As Integer
        Private ReadOnly _used As New HashSet(Of UInteger)
        Private _next As UInteger
        Private _seeded As Boolean
        Private ReadOnly _existingNext As UInteger

        Public ReadOnly Property Mask As UInteger
        Public ReadOnly Property Floor As UInteger
        Public ReadOnly Property LightEfectivo As Boolean

        Public Sub New(pm As PluginManager, outputPath As String, game As Config_App.Game_Enum, lightMaster As Boolean,
                       masterIndexLookup As Dictionary(Of String, Integer), masterCount As Integer, existingNextObjectId As UInteger)
            _pm = pm
            _outName = Path.GetFileName(outputPath)
            _masterIndexLookup = masterIndexLookup
            _selfMasterIdx = masterCount
            _existingNext = existingNextObjectId
            Dim flagsParaLey As UInteger = If(lightMaster, FLAG_ESL, 0UI)
            _LightEfectivo = PluginManager.IsLightSlot(Path.GetDirectoryName(outputPath), _outName, flagsParaLey)
            If _LightEfectivo <> lightMaster Then
                Dim nomL = _outName, casL = lightMaster, efeL = _LightEfectivo
                Logger.LogLazy(Function() $"[SAVE-ESP] '{nomL}': la casilla Light dice {casL} pero la ley " &
                                          $"(0x200 OR extensión .esl) dice {efeL}. El espacio de FormID usa la LEY.")
            End If
            _Mask = If(_LightEfectivo, &HFFFUI, &HFFFFFFUI)
            _Floor = If(PluginWriter.AllowsHardcodedRange(game, masterCount), 1UI, NEXT_OBJECT_ID_DEFAULT)
        End Sub

        ''' <summary>Anota el object id de un record PROPIO que se preserva (FormID global).</summary>
        Public Sub NoteUsed(g As UInteger)
            If g = 0UI OrElse IsProvisionalDraftFormID(g) Then Return
            Dim lf As UInteger = 0UI
            If _pm.TryMapGlobalToFileLocal(g, _masterIndexLookup, _selfMasterIdx, _outName, lf) <> PluginManager.FileLocalMapResult.Ok Then Return
            If (lf >> 24) <> CUInt(_selfMasterIdx) Then Return
            _used.Add(lf And &HFFFFFFUI)
        End Sub

        ''' <summary>Rehusa si un record propio preservado no cabe en el ancho de salida; despues siembra.</summary>
        Public Sub CheckWidthAndSeed()
            Dim overWide = _used.Where(Function(o) o > _Mask).OrderBy(Function(o) o).ToList()
            If overWide.Count > 0 Then
                Throw New InvalidOperationException(
                    $"'{_outName}' already contains {overWide.Count} record(s) whose object id does not fit this " &
                    $"file's FormID width (first: 0x{overWide(0):X}, maximum 0x{_Mask:X})." &
                    If(_LightEfectivo, $" A light (ESL) plugin only addresses 0x{_Floor:X}..0x{_Mask:X}, so the game would fold " &
                                    "those records onto other FormIDs. Save it without the Light flag, or split " &
                                    "the records across two plugins.", " Split the records across two plugins."))
            End If
            _next = If(_existingNext > 0UI, _existingNext And _Mask, NEXT_OBJECT_ID_DEFAULT)
            If _next < _Floor OrElse _next = _Mask Then
                Dim highest As UInteger = _Floor
                For Each u In _used
                    If u >= highest Then highest = u + 1UI
                Next
                _next = If(highest > _Mask, _Floor, highest)
            End If
            _seeded = True
        End Sub

        ''' <summary>El proximo object id libre.</summary>
        Public Function Dispense() As UInteger
            If Not _seeded Then Throw New InvalidOperationException("ObjectIdAllocator.Dispense antes de CheckWidthAndSeed.")
            Dim span As Long = CLng(_Mask) - CLng(_Floor) + 1L
            For attempt As Long = 0 To span - 1
                If _next > _Mask OrElse _next < _Floor Then _next = _Floor
                Dim candidate = _next
                _next += 1UI
                If _used.Add(candidate) Then Return candidate
            Next
            Throw New InvalidOperationException(
                $"'{_outName}' has no free FormID left: every object id from 0x{_Floor:X} to " &
                $"0x{_Mask:X} is already used by a record in the file. " &
                If(_LightEfectivo, $"A light (ESL) plugin only addresses {span} of them — save without the " &
                                "Light flag, or split the records across two plugins.",
                                "Split the records across two plugins."))
        End Function

        ''' <summary>HEDR.nextObjectId: el contador, envuelto al piso si quedo pasado del tope (Paso 6).</summary>
        Public Function NextObjectIdForHeader() As UInteger
            Dim n = _next
            If n > _Mask Then n = _Floor
            Return n
        End Function
    End Class

    ' =========================================================================================== bytes

    ''' <summary>Cabecera de 24 bytes + cuerpo (SaveNpcEspWriter.WrapRecord).</summary>
    Public Function WrapRecord(signature As String, body As Byte(), flags As UInteger, mappedFormID As UInteger,
                               vcs1 As UInteger, vcs2 As UShort, game As Config_App.Game_Enum,
                               Optional versionOverride As UShort = 0US) As Byte()
        Dim recordVersion As UShort = If(versionOverride <> 0US, versionOverride,
                                         If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE))
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes(signature))
                bw.Write(CUInt(body.Length))
                bw.Write(flags)
                bw.Write(mappedFormID)
                bw.Write(vcs1)
                bw.Write(recordVersion)
                bw.Write(vcs2)
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>GRUP de primer nivel (tipo 0) con sus records (SaveNpcEspWriter.BuildGrup).</summary>
    Public Function BuildGrup(label As String, recordBuffers As List(Of Byte())) As Byte()
        If label Is Nothing OrElse label.Length <> 4 Then Throw New ArgumentException($"GRUP label must be 4 chars: '{label}'.", NameOf(label))
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                Dim contentSize = recordBuffers.Sum(Function(b) b.Length)
                Dim totalSize = 24 + contentSize
                bw.Write(Encoding.ASCII.GetBytes("GRUP"))
                bw.Write(CUInt(totalSize))
                bw.Write(Encoding.ASCII.GetBytes(label))
                bw.Write(0)
                bw.Write(0UI)
                bw.Write(0UI)
                For Each b In recordBuffers
                    bw.Write(b)
                Next
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>El TES4 (SaveNpcEspWriter.BuildTes4Header) con autor y SNAM como parametros. INCC = 0: ninguno
    ''' de los dos escritores emite CELL (la ley de xEdit cuenta los CELL interiores, wbI:5377-5386).</summary>
    Public Function BuildTes4Header(game As Config_App.Game_Enum,
                                    markAsMaster As Boolean,
                                    lightMaster As Boolean,
                                    masters As List(Of String),
                                    numContentRecords As Integer,
                                    nextObjectId As UInteger,
                                    author As String,
                                    writeEncodingSnam As Boolean) As Byte()
        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Dim hedrVersion As Single = PluginWriter.HedrVersionFor(game)
        Using bodyMs As New MemoryStream()
            Using bw As New BinaryWriter(bodyMs)
                WriteSubrecordHeader(bw, "HEDR", 12)
                bw.Write(hedrVersion)
                bw.Write(CUInt(numContentRecords))
                bw.Write(nextObjectId)

                Dim authorBytes = PluginEncodingSettings.EncodeTranslatable(author)
                WriteSubrecordHeader(bw, "CNAM", authorBytes.Length + 1)
                bw.Write(authorBytes)
                bw.Write(CByte(0))

                If writeEncodingSnam Then
                    Dim cpTag = PluginEncodingSettings.GetTranslatableSnamCpTag()
                    If cpTag <> "" Then
                        Dim snamText = "Plugin encoding: " & cpTag
                        Dim snamBytes = PluginEncodingSettings.EncodeTranslatable(snamText)
                        WriteSubrecordHeader(bw, "SNAM", snamBytes.Length + 1)
                        bw.Write(snamBytes)
                        bw.Write(CByte(0))
                    End If
                End If

                For Each masterName In masters
                    Dim masterBytes = PluginEncodingSettings.EncodeMasterFileName(masterName)
                    WriteSubrecordHeader(bw, "MAST", masterBytes.Length + 1)
                    bw.Write(masterBytes)
                    bw.Write(CByte(0))
                    WriteSubrecordHeader(bw, "DATA", 8)
                    bw.Write(0UL)
                Next

                WriteSubrecordHeader(bw, "INCC", 4)
                bw.Write(0UI)
            End Using
            Dim bodyBytes = bodyMs.ToArray()

            Using ms As New MemoryStream()
                Using bw As New BinaryWriter(ms)
                    bw.Write(Encoding.ASCII.GetBytes("TES4"))
                    bw.Write(CUInt(bodyBytes.Length))
                    Dim flags As UInteger = 0UI
                    If markAsMaster Then flags = flags Or FLAG_ESM
                    If lightMaster Then flags = flags Or FLAG_ESL
                    bw.Write(flags)
                    bw.Write(0UI)
                    bw.Write(0UI)
                    bw.Write(recordVersion)
                    bw.Write(0US)
                    bw.Write(bodyBytes)
                End Using
                Return ms.ToArray()
            End Using
        End Using
    End Function

    ' =========================================================================================== Save

    ''' <summary>
    ''' Escribe un plugin con records arbitrarios, con la misma ley que el guardado de NPC: dos pasadas del mismo
    ''' recorrido de emision (descubrimiento → MAST → reparto de object ids → emision real), GRUP de primer nivel en
    ''' <see cref="GenericSaveOptions.GroupOrder"/>, HEDR.numRecords = records + GRUP, escritura en el lugar con copia.
    ''' </summary>
    Public Function Save(outputPath As String, entries As IReadOnlyList(Of GenericRecordEntry),
                         pluginManager As PluginManager, options As GenericSaveOptions) As GenericSaveResult
        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("outputPath is empty.", NameOf(outputPath))
        If entries Is Nothing Then Throw New ArgumentNullException(NameOf(entries))
        If pluginManager Is Nothing Then Throw New ArgumentNullException(NameOf(pluginManager))
        If options Is Nothing Then Throw New ArgumentNullException(NameOf(options))
        If options.GroupOrder Is Nothing Then Throw New ArgumentException("GroupOrder is required.", NameOf(options))

        Dim game = options.Game
        Dim gameMaster = SaveNpcEspWriter.MasterFileNamePublic(game)
        Dim outName = Path.GetFileName(outputPath)
        Dim existingMasters = If(options.ExistingMasters, New List(Of String))

        ' Orden del archivo: GRUP por el orden declarado; records por el orden del llamador.
        Dim grupIndex As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
        For i = 0 To options.GroupOrder.Count - 1
            grupIndex(options.GroupOrder(i)) = i
        Next
        Dim porGrupo As New SortedDictionary(Of Integer, List(Of GenericRecordEntry))
        For Each e In entries
            Dim gi As Integer
            If Not grupIndex.TryGetValue(e.Signature, gi) Then
                Throw New InvalidOperationException($"Record type '{e.Signature}' has no top-level group in the declared group order.")
            End If
            Dim l As List(Of GenericRecordEntry) = Nothing
            If Not porGrupo.TryGetValue(gi, l) Then
                l = New List(Of GenericRecordEntry)
                porGrupo(gi) = l
            End If
            l.Add(e)
        Next

        Dim emitAll As Func(Of SaveNpcEspWriter.FormIdRemapper, Integer, List(Of String), List(Of KeyValuePair(Of String, List(Of Byte())))) =
            Function(rm, selfIdx, avisos)
                Dim out As New List(Of KeyValuePair(Of String, List(Of Byte())))
                For Each kv In porGrupo
                    Dim bufs As New List(Of Byte())
                    For Each e In kv.Value
                        bufs.Add(SaveNpcEspWriter.SerializarRecord(e.Record, e.FormID, rm, game, e.Vcs1, e.Vcs2, selfIdx, avisos))
                    Next
                    out.Add(New KeyValuePair(Of String, List(Of Byte()))(options.GroupOrder(kv.Key), bufs))
                Next
                Return out
            End Function

        ' Pasada de descubrimiento.
        Dim referenced As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim audit As New Dictionary(Of String, HashSet(Of UInteger))(StringComparer.OrdinalIgnoreCase)
        Dim externos = If(options.ExternalDrafts, New Dictionary(Of UInteger, KeyValuePair(Of String, UInteger)))
        Dim externosUsados As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim baseDiscovery = DiscoveryRemapper(pluginManager, outName, referenced, audit)
        Call emitAll(Function(g)
                         Dim ext As KeyValuePair(Of String, UInteger) = Nothing
                         If IsProvisionalDraftFormID(g) AndAlso externos.TryGetValue(g, ext) Then
                             externosUsados.Add(ext.Key)
                             Return g
                         End If
                         Return baseDiscovery(g)
                     End Function, -1, New List(Of String))

        Dim masters = BuildMasterList(existingMasters, referenced, pluginManager, gameMaster, options.AlwaysKeepGameMaster)
        If options.ExternalFileOrder IsNot Nothing Then
            For Each f In options.ExternalFileOrder
                If externosUsados.Contains(f) Then masters.Add(f)
            Next
        End If
        Dim masterIndexLookup As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To masters.Count - 1
            masterIndexLookup(masters(i)) = i
        Next
        Dim selfMasterIdx = masters.Count

        Dim alloc As New ObjectIdAllocator(pluginManager, outputPath, game, options.LightMaster, masterIndexLookup, masters.Count, options.ExistingNextObjectId)
        For Each e In entries
            If Not e.IsNew Then alloc.NoteUsed(e.FormID)
        Next
        alloc.CheckWidthAndSeed()

        ' Reparto en el orden de ENTRADA: es el orden en que el llamador creo los records.
        Dim draftRemap As New Dictionary(Of UInteger, UInteger)
        For Each e In entries
            If Not e.IsNew OrElse draftRemap.ContainsKey(e.FormID) Then Continue For
            draftRemap(e.FormID) = (CUInt(selfMasterIdx) << 24) Or alloc.Dispense()
        Next

        Dim result As New GenericSaveResult With {
            .OutputPath = outputPath,
            .MasterList = masters,
            .DraftFormIdMap = New Dictionary(Of UInteger, UInteger)(draftRemap),
            .RecordCount = entries.Count
        }
        For Each m In masters
            Dim hs As HashSet(Of UInteger) = Nothing
            result.MasterAudit(m) = If(audit.TryGetValue(m, hs), hs.ToList(), New List(Of UInteger))
        Next

        ' Orden de records DENTRO del GRUP: se aplica con el FormID ya definitivo de los nuevos.
        If options.RecordSortKey IsNot Nothing Then
            Dim key = options.RecordSortKey
            For Each kv In porGrupo
                ' OrderBy es estable: dos claves iguales conservan el orden de entrada (el desempate de xEdit por
                ' ElementID no puede darse entre records distintos de un mismo archivo: la clave es el FormID).
                Dim ordenada = kv.Value.OrderBy(Function(x) key(x, If(x.IsNew, draftRemap(x.FormID) And &HFFFFFFUI, 0UI))).ToList()
                kv.Value.Clear()
                kv.Value.AddRange(ordenada)
            Next
        End If

        Dim baseRemapper = FinalRemapper(pluginManager, outName, masterIndexLookup, selfMasterIdx, draftRemap)
        Dim remapper As SaveNpcEspWriter.FormIdRemapper =
            Function(g)
                Dim ext As KeyValuePair(Of String, UInteger) = Nothing
                If IsProvisionalDraftFormID(g) AndAlso Not draftRemap.ContainsKey(g) AndAlso externos.TryGetValue(g, ext) Then
                    Return (CUInt(masterIndexLookup(ext.Key)) << 24) Or (ext.Value And &HFFFFFFUI)
                End If
                Return baseRemapper(g)
            End Function
        Dim grupos = emitAll(remapper, selfMasterIdx, result.Advertencias)

        Dim grupBytes As New List(Of Byte())
        Dim totalRecords As Integer = 0
        For Each g In grupos
            If g.Value.Count = 0 Then Continue For
            grupBytes.Add(BuildGrup(g.Key, g.Value))
            totalRecords += g.Value.Count + 1
        Next

        Dim tes4 = BuildTes4Header(game, options.MarkAsMaster, options.LightMaster, masters, totalRecords,
                                   alloc.NextObjectIdForHeader(), options.Author, options.WriteEncodingSnam)

        BSA_BA2_Library_DLL.EscrituraEnElLugar.GuardarConCopia(
            outputPath,
            Sub(fs)
                fs.Write(tes4, 0, tes4.Length)
                For Each gb In grupBytes
                    fs.Write(gb, 0, gb.Length)
                Next
            End Sub)
        Return result
    End Function

End Module
