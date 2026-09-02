Imports FO4_Base_Library.Canon.CanonInterpretacion
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

    ''' <summary>Maps a FormID to its leveled-list view (<see cref="LVLI_Data"/>) when the caller knows that
    ''' FormID as a leveled list — the hook that teaches this sampler about lists that DON'T live in the
    ''' PluginManager (e.g. NPC_Manager's in-memory LVLI drafts, provisional FormIDs the engine can't resolve).
    ''' Returns Nothing when the FormID is not such a list, so the sampler falls back to the real record
    ''' (GetRecord → ParseLVLI). When no resolver is supplied the sampler is purely record-based — its original
    ''' behavior — so every existing caller is unaffected. This is what lets the SAME sampling/enumeration
    ''' algorithm serve both real records and drafts (no duplicated leveled-list logic in the app).</summary>
    Public Delegate Function LeveledListResolver(formID As UInteger) As Canon.ILvli

    ''' <summary>Sampla una realización de outfit con propagación de keywords. Devuelve la lista
    ''' de ARMO terminal con sus keywords contextuales heredadas del camino LLKC.</summary>
    Public Function SampleOutfitWithKeywords(otftFormID As UInteger,
                                             pluginManager As PluginManager,
                                             Optional warnings As List(Of String) = Nothing,
                                             Optional leveledResolver As LeveledListResolver = Nothing,
                                             Optional armoResolver As Func(Of UInteger, Canon.IArmo) = Nothing) As List(Of OutfitArmorPick)
        Dim picks As New List(Of OutfitArmorPick)
        If otftFormID = 0UI OrElse pluginManager Is Nothing Then Return picks

        Dim rec = pluginManager.GetRecord(otftFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "OTFT" Then
            If warnings IsNot Nothing Then warnings.Add($"Outfit {otftFormID:X8} missing or not OTFT")
            Return picks
        End If

        Dim otft = Canon.CanonRecords.Otft(rec, pluginManager)
        For Each itemFormID In otft.Prendas()
            SampleOutfitItem(itemFormID, pluginManager, New HashSet(Of UInteger)(), picks, warnings, New List(Of UInteger), leveledResolver, armoResolver)
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

    ''' <summary>Sampla UNA realización de un solo item (ARMO o LVLI) — devuelve los ARMO terminales con
    ''' sus keywords contextuales. Para el editor de outfits: cuando el usuario agrega una LVLI como pieza,
    ''' se cachea su realización (re-sampleable con un botón Reroll) para preview/display/conflicto, mientras
    ''' el draft guarda el FormID de la LVLI (se persiste como LVLI). ARMO → se devuelve directo; LVLI → se
    ''' rola una entry (o todas si UseAll), con propagación LLKC. Mismo motor que SampleOutfitWithKeywords,
    ''' pero a nivel de un único item en vez de un OTFT completo.
    ''' <para>⛔ <paramref name="armoResolver"/> es el ESPEJO de <paramref name="leveledResolver"/> para
    ''' los ARMO: un borrador vive sólo en memoria y el <c>PluginManager</c> no lo resuelve, así que sin
    ''' él un ARMO propio adentro de una lista por nivel propia se reportaba «missing» y se PERDÍA. Va
    ''' AL FINAL y opcional: quien no lo pasa se comporta byte a byte como antes.</para></summary>
    Public Function SampleItemWithKeywords(itemFormID As UInteger,
                                           pluginManager As PluginManager,
                                           Optional warnings As List(Of String) = Nothing,
                                           Optional leveledResolver As LeveledListResolver = Nothing,
                                           Optional armoResolver As Func(Of UInteger, Canon.IArmo) = Nothing) As List(Of OutfitArmorPick)
        Dim picks As New List(Of OutfitArmorPick)
        If itemFormID = 0UI OrElse pluginManager Is Nothing Then Return picks
        SampleOutfitItem(itemFormID, pluginManager, New HashSet(Of UInteger)(), picks, warnings, New List(Of UInteger), leveledResolver, armoResolver)
        Dim merged As New Dictionary(Of UInteger, OutfitArmorPick)
        For Each p In picks
            If merged.ContainsKey(p.ArmoFormID) Then
                For Each kw In p.ContextKeywords
                    If Not merged(p.ArmoFormID).ContextKeywords.Contains(kw) Then merged(p.ArmoFormID).ContextKeywords.Add(kw)
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

    ''' <summary>Determinista: enumera TODOS los ARMO terminales posibles de un OTFT, tratando cada
    ''' LVLI como UseAll e ignorando ChanceNone. Sin RNG — pensado para filtros y listas estables
    ''' (¿este outfit puede producir alguna pieza válida para la raza X?), NO para el render (que usa
    ''' <see cref="SampleOutfitWithKeywords"/> con sampleo aleatorio). Cada LVLI se expande una sola
    ''' vez (visited permanente) para evitar blow-up exponencial en cadenas anidadas/diamante; los
    ''' ARMO terminales se deduplican por FormID.</summary>
    Public Function EnumerateAllTerminalArmos(otftFormID As UInteger,
                                              pluginManager As PluginManager,
                                              Optional leveledResolver As LeveledListResolver = Nothing,
                                              Optional armoResolver As Func(Of UInteger, Canon.IArmo) = Nothing) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If otftFormID = 0UI OrElse pluginManager Is Nothing Then Return result

        Dim rec = pluginManager.GetRecord(otftFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "OTFT" Then Return result

        Dim otft = Canon.CanonRecords.Otft(rec, pluginManager)
        Dim seen As New HashSet(Of UInteger)        ' ARMO terminales ya emitidos (dedup)
        Dim expandedLvli As New HashSet(Of UInteger) ' LVLI ya expandidas (anti-ciclo + anti-blowup)
        For Each itemFormID In otft.Prendas()
            EnumerateItemAllTerminal(itemFormID, pluginManager, expandedLvli, result, seen, leveledResolver, armoResolver)
        Next
        Return result
    End Function

    ''' <summary>Determinista: enumera todos los ARMO terminales de UN solo item (ARMO o LVLI), no de un
    ''' OTFT. <see cref="EnumerateAllTerminalArmos"/> exige un OTFT (parsea INAM); este sirve para una LVLI
    ''' suelta — p.ej. la lista de ítems del editor que ofrece una LVLI como pieza y necesita saber qué
    ''' terminales (y slots) puede producir. ARMO → la ARMO terminal; LVLI → expansión recursiva.</summary>
    Public Function EnumerateItemTerminalArmos(itemFormID As UInteger,
                                               pluginManager As PluginManager,
                                               Optional leveledResolver As LeveledListResolver = Nothing,
                                               Optional armoResolver As Func(Of UInteger, Canon.IArmo) = Nothing) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If itemFormID = 0UI OrElse pluginManager Is Nothing Then Return result
        EnumerateItemAllTerminal(itemFormID, pluginManager, New HashSet(Of UInteger)(), result, New HashSet(Of UInteger)(), leveledResolver, armoResolver)
        Return result
    End Function

    Private Sub EnumerateItemAllTerminal(formID As UInteger,
                                         pluginManager As PluginManager,
                                         expandedLvli As HashSet(Of UInteger),
                                         result As List(Of UInteger),
                                         seen As HashSet(Of UInteger),
                                         leveledResolver As LeveledListResolver,
                                         armoResolver As Func(Of UInteger, Canon.IArmo))
        If formID = 0UI Then Return

        ' Leveled list? Ask the resolver first (it sees drafts that aren't in the PluginManager); fall back to
        ' the real record. Nothing → not a leveled list → treat as an ARMO terminal.
        Dim lvli = ResolveLeveled(formID, pluginManager, leveledResolver)
        If lvli Is Nothing Then
            ' ⛔ El MISMO resolver de borradores que el camino de muestreo: sin esto el camino
            ' DETERMINISTA -el que decide la marca «no aplica» y el footprint de la fila- pierde el ARMO
            ' propio y la fila lo tacha mientras el preview lo dibuja.
            Dim terminalID = ResolveTerminalArmorFormID(formID, pluginManager, armoResolver)
            If terminalID <> 0UI AndAlso seen.Add(terminalID) Then result.Add(terminalID)
            Return
        End If

        ' Expand-once: si ya la recorrimos por otra rama, sus descendientes ya están en result.
        If Not expandedLvli.Add(formID) Then Return
        For Each entry In lvli.LeveledListEntries
            EnumerateItemAllTerminal(entry.LeveledListEntryItem, pluginManager, expandedLvli, result, seen, leveledResolver, armoResolver)
        Next
    End Sub

    ''' <summary>Resuelve la ARMO terminal siguiendo la cadena de templates TNAM.
    ''' <para>MK <paramref name="armoResolver"/> se consulta PRIMERO en CADA eslabón: un borrador no está
    ''' en ningún archivo, así que sin él la cadena se corta en cuanto uno de los eslabones es propio.</para>
    ''' <para>⛔ EL RECORRIDO NO VIVE ACÁ: lo hace <see cref="Canon.CanonHerencia.TerminalFormID"/>, que es
    ''' la única caminata de la cadena en el árbol. Acá quedó lo que SÍ es de esta clase: cómo se ENCUENTRA
    ''' un ARMO (el borrador primero, después el archivo) y el contrato de devolver <c>0</c> cuando el
    ''' FormID no es un ARMO, que es lo que miran los llamadores.</para>
    ''' <para><b>Por qué se mudó.</b> Había DOS caminatas y ante una cadena CÍCLICA cada una contestaba la
    ''' pregunta de la OTRA: ésta devolvía el primer nodo re-visitado (identidad) y la de
    ''' <c>CanonHerencia</c> devolvía el último eslabón resuelto (materialización), pero las dos se usaban
    ''' para ambas cosas. Como de este FormID sale la IDENTIDAD de la prenda, el selector podía agrupar por
    ''' una y el render dibujar la otra: vista previa, agrupamiento y clon de la misma armadura,
    ''' incoherentes entre sí.</para>
    ''' <para><b>Nada cambió de conducta acá</b>: la cara de identidad de la caminata única devuelve
    ''' exactamente lo que devolvía esta función —el primer nodo re-visitado, que agrupa la cola con el
    ''' ciclo (<c>A→B→C→B</c> ⇒ <c>A</c> y <c>B</c> dan <c>B</c>)—. Lo que cambió es que ya no es una
    ''' segunda implementación. La justificación completa, con la separación de las dos preguntas y su
    ''' marca de DESEMPATE DE APP sin cita del motor, está en <c>CanonHerencia.CaminarCadena</c>.</para></summary>
    Public Function ResolveTerminalArmorFormID(armoFormID As UInteger,
                                               pluginManager As PluginManager,
                                               Optional armoResolver As Func(Of UInteger, Canon.IArmo) = Nothing) As UInteger
        If armoFormID = 0UI OrElse pluginManager Is Nothing Then Return 0UI
        ' La regla de RESOLUCIÓN —borrador primero, después el archivo— es de esta clase y se arma una vez
        ' acá; la caminata la hace CanonHerencia con este mismo delegado en CADA eslabón.
        Dim resolverDeArmo =
            Function(fid As UInteger) As Canon.IArmo
                If fid = 0UI Then Return Nothing
                If armoResolver IsNot Nothing Then
                    Dim borrador = armoResolver(fid)
                    If borrador IsNot Nothing Then Return borrador
                End If
                Dim rec = pluginManager.GetRecord(fid)
                If rec Is Nothing OrElse rec.Header.Signature <> "ARMO" Then Return Nothing
                Return Canon.CanonRecords.Armo(rec, pluginManager)
            End Function
        Return Canon.CanonHerencia.TerminalFormID(armoFormID, resolverDeArmo)
    End Function

    ''' <summary>Resolve a FormID to its leveled-list view: the injected resolver first (so caller-known
    ''' lists outside the PluginManager — e.g. drafts — are seen), then the real record. Nothing when the
    ''' FormID is not a leveled list (the caller then treats it as an ARMO terminal).</summary>
    Private Function ResolveLeveled(formID As UInteger, pluginManager As PluginManager, leveledResolver As LeveledListResolver) As Canon.ILvli
        If formID = 0UI Then Return Nothing
        If leveledResolver IsNot Nothing Then
            Dim v = leveledResolver(formID)
            If v IsNot Nothing Then Return v
        End If
        Dim rec = pluginManager.GetRecord(formID)
        If rec IsNot Nothing AndAlso rec.Header.Signature = "LVLI" Then Return Canon.CanonRecords.Lvli(rec, pluginManager)
        Return Nothing
    End Function

    ''' <summary>¿El sorteo de <paramref name="itemFormID"/> puede pasar por <paramref name="targetFormID"/>?
    ''' — o sea, ¿bajando por las listas por nivel desde el item se llega a ese FormID? Recorre lo MISMO que
    ''' el sampler y por la MISMA puerta (<see cref="ResolveLeveled"/>): el resolvedor inyectado primero, el
    ''' record real después, en CADA eslabón.
    '''
    ''' <para>⛔ <b>VIVE ACÁ, PEGADO AL SAMPLER, A PROPÓSITO.</b> Quien edita el contenido de una lista tiene
    ''' que volver a sortear todo lo que dependa de ella; si esa pregunta se contesta con un recorrido
    ''' PROPIO, los dos se separan y la respuesta deja de valer. Ya pasó: una versión que sólo bajaba por
    ''' BORRADORES daba False para una lista vanilla que contiene a la editada — y el sampler sí entraba,
    ''' porque un borrador de OVERRIDE conserva el FormID REAL del record y las listas vanilla lo siguen
    ''' nombrando. Compartiendo <c>ResolveLeveled</c> eso no se puede volver a escribir distinto.</para>
    '''
    ''' <para>Es PURA y <c>Public</c> para que un gate la corra: la ley que decide qué se re-sortea no puede
    ''' vivir adentro de un formulario, donde ningún testigo la alcanza.</para>
    ''' <para><paramref name="visited"/> corta ciclos —el árbol puede traer uno de antes— y hace que cada
    ''' nodo se recorra una sola vez. La arista se compara ANTES de bajar, así que un destino ya visitado se
    ''' detecta igual.</para></summary>
    Public Function ItemReachesLeveledList(itemFormID As UInteger,
                                           targetFormID As UInteger,
                                           pluginManager As PluginManager,
                                           Optional leveledResolver As LeveledListResolver = Nothing,
                                           Optional visited As HashSet(Of UInteger) = Nothing) As Boolean
        If itemFormID = 0UI OrElse targetFormID = 0UI OrElse pluginManager Is Nothing Then Return False
        If visited Is Nothing Then visited = New HashSet(Of UInteger)()
        If Not visited.Add(itemFormID) Then Return False
        Dim lvli = ResolveLeveled(itemFormID, pluginManager, leveledResolver)
        If lvli Is Nothing Then Return False          ' no es una lista: no hay por dónde bajar
        For Each e In lvli.LeveledListEntries
            Dim it = e.LeveledListEntryItem
            If it = targetFormID Then Return True
            If ItemReachesLeveledList(it, targetFormID, pluginManager, leveledResolver, visited) Then Return True
        Next
        Return False
    End Function

    Private Sub SampleOutfitItem(formID As UInteger,
                                 pluginManager As PluginManager,
                                 visited As HashSet(Of UInteger),
                                 result As List(Of OutfitArmorPick),
                                 warnings As List(Of String),
                                 inheritedKeywords As List(Of UInteger),
                                 leveledResolver As LeveledListResolver,
                                 armoResolver As Func(Of UInteger, Canon.IArmo))
        If formID = 0UI OrElse visited.Contains(formID) Then Return

        ' Leveled list? (real record OR a resolver-known draft). Checked first so a draft FormID — which the
        ' PluginManager can't resolve — is handled instead of being misreported as a missing item.
        Dim lvli = ResolveLeveled(formID, pluginManager, leveledResolver)
        If lvli IsNot Nothing Then
            visited.Add(formID)
            SampleLeveledItem(lvli, pluginManager, visited, result, warnings, inheritedKeywords, leveledResolver, armoResolver)
            visited.Remove(formID)
            Return
        End If

        ' ⛔ ANTES de darlo por perdido: un ARMO BORRADOR no está en ningún archivo, así que `GetRecord`
        ' devuelve Nothing y esto lo reportaba «missing» y lo tiraba. Es el mismo hueco que el
        ' resolvedor de listas por nivel ya cerraba para las LVLI.
        ' ⛔ LA IDENTIDAD ES SIEMPRE EL TERMINAL, también para un borrador. Acá se emitía el FormID CRUDO
        ' con el argumento «el clon le saca el TNAM al nacer»: eso es FALSO para un borrador de
        ' OVERRIDE — `OutfitDraft`/`ArmoDraft.Edicion` copian el record ENTERO, `TNAM` incluido; el
        ' único que lo saca es el clon del EDITOR (`ArmoEditor_Form.BuildDraftFromExisting`). Con el
        ' crudo, un override de un ARMO con plantilla entraba al torneo con OTRA identidad que la que
        ' el render agrupa, que es el defecto que la ronda del agrupamiento vino a cerrar.
        If armoResolver IsNot Nothing AndAlso armoResolver(formID) IsNot Nothing Then
            Dim terminalDraft = ResolveTerminalArmorFormID(formID, pluginManager, armoResolver)
            If terminalDraft <> 0UI Then
                Dim pickDraft As New OutfitArmorPick With {.ArmoFormID = terminalDraft}
                pickDraft.ContextKeywords.AddRange(inheritedKeywords)
                result.Add(pickDraft)
            End If
            Return
        End If

        Dim rec = pluginManager.GetRecord(formID)
        If rec Is Nothing Then
            If warnings IsNot Nothing Then warnings.Add($"Outfit item {formID:X8} missing")
            Return
        End If

        Select Case rec.Header.Signature
            Case "ARMO"
                Dim terminalID = ResolveTerminalArmorFormID(formID, pluginManager, armoResolver)
                If terminalID <> 0UI Then
                    Dim pick As New OutfitArmorPick With {.ArmoFormID = terminalID}
                    pick.ContextKeywords.AddRange(inheritedKeywords)
                    result.Add(pick)
                End If

            Case Else
                If warnings IsNot Nothing Then warnings.Add($"Unsupported outfit item {rec.Header.Signature} [{formID:X8}]")
        End Select
    End Sub

    Private Sub SampleLeveledItem(lvli As Canon.ILvli,
                                  pluginManager As PluginManager,
                                  visited As HashSet(Of UInteger),
                                  result As List(Of OutfitArmorPick),
                                  warnings As List(Of String),
                                  inheritedKeywords As List(Of UInteger),
                                  leveledResolver As LeveledListResolver,
                                  armoResolver As Func(Of UInteger, Canon.IArmo))
        If lvli Is Nothing Then Return

        ' Whole-list chance-none: la LVLI completa puede no contribuir (fiel al engine).
        If lvli.ChanceNone > 0 AndAlso NextPercent() < lvli.ChanceNone Then Return

        Dim usable = lvli.LeveledListEntries.Where(Function(e) e.LeveledListEntryItem <> 0UI).ToList()
        If usable.Count = 0 Then Return

        ' Build the keyword set for descendants: inherited + LLKC of THIS LVLI.
        ' For each LLKC entry, roll Chance% to decide whether it propagates.
        Dim mergedKeywords As New List(Of UInteger)
        mergedKeywords.AddRange(inheritedKeywords)
        ' Las palabras clave de filtro son un sistema de Fallout 4: en el otro juego no existen.
        Dim fo4 = TryCast(lvli, Canon.LvliFO4)
        For Each fk In If(fo4 Is Nothing, CType(New List(Of Canon.LvliFO4_FilterKeywordChances), IEnumerable(Of Canon.LvliFO4_FilterKeywordChances)), fo4.FilterKeywordChances)
            If fk.FilterKeyword = 0UI Then Continue For
            ' Chance >= 100 = always; 0 = never; in between = roll.
            Dim include As Boolean
            If fk.FilterChance >= 100UI Then
                include = True
            ElseIf fk.FilterChance = 0UI Then
                include = False
            Else
                include = (NextPercent() < CInt(fk.FilterChance))
            End If
            If include AndAlso Not mergedKeywords.Contains(fk.FilterKeyword) Then
                mergedKeywords.Add(fk.FilterKeyword)
            End If
        Next

        If lvli.FlagsUseAll Then
            For Each entry In usable
                SampleLeveledEntry(entry, pluginManager, visited, result, warnings, lvli.FlagsCalculateForEachItemInCount, mergedKeywords, leveledResolver, armoResolver)
            Next
        Else
            Dim entry = usable(NextIndex(usable.Count))
            SampleLeveledEntry(entry, pluginManager, visited, result, warnings, lvli.FlagsCalculateForEachItemInCount, mergedKeywords, leveledResolver, armoResolver)
        End If
    End Sub

    Private Sub SampleLeveledEntry(entry As Canon.ILvli_LeveledListEntries,
                                   pluginManager As PluginManager,
                                   visited As HashSet(Of UInteger),
                                   result As List(Of OutfitArmorPick),
                                   warnings As List(Of String),
                                   calculateEachItemInCount As Boolean,
                                   inheritedKeywords As List(Of UInteger),
                                   leveledResolver As LeveledListResolver,
                                   armoResolver As Func(Of UInteger, Canon.IArmo))
        ' El chance-none POR ENTRADA solo existe en Fallout 4.
        Dim entFo4 = TryCast(entry, Canon.LvliFO4_LeveledListEntries)
        If entFo4 IsNot Nothing AndAlso entFo4.LeveledListEntryChanceNone > 0 AndAlso
           NextPercent() < entFo4.LeveledListEntryChanceNone Then Return

        Dim count As Integer = If(entry.LeveledListEntryCount = 0US, 1, CInt(entry.LeveledListEntryCount))
        If count <= 1 OrElse Not calculateEachItemInCount Then
            SampleOutfitItem(entry.LeveledListEntryItem, pluginManager, visited, result, warnings, inheritedKeywords, leveledResolver, armoResolver)
        Else
            For i = 1 To count
                SampleOutfitItem(entry.LeveledListEntryItem, pluginManager, visited, result, warnings, inheritedKeywords, leveledResolver, armoResolver)
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
