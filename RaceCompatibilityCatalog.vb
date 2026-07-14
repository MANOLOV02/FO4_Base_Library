''' <summary>Reconstructs, statically, the FormList mutation that RaceCompatibility performs AT RUNTIME so a
''' custom race can wear vanilla head parts.
'''
''' THE PROBLEM. A vanilla head part (a hair, a pair of brows) carries <c>HDPT.RNAM</c> = a FormList of the races
''' allowed to use it, and the chargen catalog (the game's RaceSex menu — and therefore our head-part pickers)
''' filters on membership in that list. A race invented by a mod years later obviously is NOT in those vanilla
''' lists, and CANNOT be: the FormLists live in Skyrim.esm.
'''
''' WHAT THE GAME DOES. RaceCompatibility ships <c>GenericRaceController</c>. A race mod attaches it to one of its
''' quests, fills its properties with the vanilla FormLists, and declares its own races in per-slot FormLists
''' ("my races occupy the NORD slot"). On <c>OnInit</c> — ONCE, when the mod first enters a save — the script
''' walks the 10 vanilla race slots and INSERTS each new race into the matching vanilla FormLists
''' (<c>addIfNeeded</c> = HasForm check + AddForm). From then on the menu offers vanilla hair for that race.
''' The mutation lives in memory (and in the save); NOTHING is ever written to a plugin. So a records-only reader
''' cannot see it — not because we fail to parse something, but because the data does not exist until the game
''' fabricates it. Reconstructing it is the only option.
'''
''' WHY THAT IS SAFE TO REPLICATE. The operation is one-shot, purely ADDITIVE and IDEMPOTENT (it never removes or
''' reorders), and it depends on nothing but records + scripts. So it is a PURE FUNCTION of the load order: no
''' state machine, no ordering, no timing (the <c>RaceDispatcherScript</c> "Busy" state is just a mutex between the
''' mod's controllers, with no effect on the result). We compute it once per load and cache it in memory only — so
''' it always reflects the CURRENT load order (a mod update / added / removed race mod is picked up on reload).
'''
''' DETECTION IS BY SHAPE, NOT BY NAME. We do not look for COtR, nor for any EDID: we look for any QUST whose VMAD
''' attaches a script named <c>GenericRaceController</c>. Every mod that uses RaceCompatibility matches, whoever
''' wrote it.
'''
''' THE THREE INPUTS (all authoritative, none guessed):
'''   1. The QUST's VMAD (<see cref="VmadPropertyReader"/>) → what each script property is BOUND to: the vanilla
'''      FormLists (on GenericRaceController) and the mod's own race FormLists (on its controller script).
'''   2. The mod's compiled script (<see cref="PapyrusPexParser.ExtractPropertyBindings"/>) → WHICH slot each of the
'''      mod's FormLists occupies (<c>raceController.NewNord = HeadPartsNord_DZ</c>). The record cannot express this;
'''      only the script can. We read the <c>.pex</c>, not the <c>.psc</c> (COtR ships 8 .pex but only 7 .psc — the
'''      Breton source is missing, and the game loads .pex anyway).
'''   3. The rules below, transcribed from RaceCompatibility's <c>GenericRaceController.psc</c>
'''      (<c>internalProxyRaces</c> / <c>updateStandardRaces</c> / <c>updateVampireRaces</c>).
'''
''' SKYRIM-ONLY: RaceCompatibility is a Skyrim mod; there is no Fallout 4 counterpart. <see cref="Load"/> no-ops
''' for FO4.</summary>
Public Class RaceCompatibilityCatalog

    ''' <summary>The 10 vanilla race slots, in the exact order of <c>internalProxyRaces</c>'s RaceList[] — the index
    ''' IS the "element" the script's isHuman/isElf/… predicates test.</summary>
    Private Shared ReadOnly SlotNames As String() =
        {"Argonian", "Breton", "DarkElf", "HighElf", "Imperial", "Khajiit", "Nord", "Orc", "Redguard", "WoodElf"}

    ' Predicates, verbatim from GenericRaceController.psc (isHuman:272, isBreton:291, isNord:300, isImperial:309,
    ' isOrc:318, isElf:327, isArgonian:340, isKhajiit:349). Note isHuman is FALSE for the beasts, the elves AND the
    ' orc — and note that the script adds every NON-human (elves and orc included) to RacesBeast in
    ' updateStandardRaces. That is what the script does; we replicate it, quirks and all.
    Private Shared Function IsHuman(i As Integer) As Boolean
        Return i <> 0 AndAlso i <> 2 AndAlso i <> 3 AndAlso i <> 5 AndAlso i <> 7 AndAlso i <> 9
    End Function
    Private Shared Function IsBreton(i As Integer) As Boolean
        Return i = 1
    End Function
    Private Shared Function IsNord(i As Integer) As Boolean
        Return i = 6
    End Function
    Private Shared Function IsImperial(i As Integer) As Boolean
        Return i = 4
    End Function
    Private Shared Function IsOrc(i As Integer) As Boolean
        Return i = 7
    End Function
    Private Shared Function IsElf(i As Integer) As Boolean
        Return i = 2 OrElse i = 3 OrElse i = 9
    End Function
    Private Shared Function IsArgonian(i As Integer) As Boolean
        Return i = 0
    End Function
    Private Shared Function IsKhajiit(i As Integer) As Boolean
        Return i = 5
    End Function

    ''' <summary>FLST FormID → the races the script would have inserted into it. Empty = nothing to reconstruct
    ''' (no RaceCompatibility-based mod in the load order), and every consumer then behaves exactly as before.</summary>
    Private ReadOnly _augment As New Dictionary(Of UInteger, HashSet(Of UInteger))

    ''' <summary>Races that got injected somewhere (for logging / diagnostics).</summary>
    Public ReadOnly Property InjectedRaceCount As Integer
        Get
            Return _augment.Values.SelectMany(Function(s) s).Distinct().Count()
        End Get
    End Property

    Public ReadOnly Property AugmentedListCount As Integer
        Get
            Return _augment.Count
        End Get
    End Property

    ''' <summary>Would the script have put <paramref name="raceFormID"/> into <paramref name="flstFormID"/>?
    ''' This is the ONLY thing consumers need: OR it with the FormList's real contents.</summary>
    Public Function ContainsRace(flstFormID As UInteger, raceFormID As UInteger) As Boolean
        If flstFormID = 0UI OrElse raceFormID = 0UI Then Return False
        Dim set_ As HashSet(Of UInteger) = Nothing
        If Not _augment.TryGetValue(flstFormID, set_) Then Return False
        Return set_.Contains(raceFormID)
    End Function

    Private Sub Add(flstFormID As UInteger, raceFormID As UInteger)
        If flstFormID = 0UI OrElse raceFormID = 0UI Then Return
        Dim set_ As HashSet(Of UInteger) = Nothing
        If Not _augment.TryGetValue(flstFormID, set_) Then
            set_ = New HashSet(Of UInteger)
            _augment(flstFormID) = set_
        End If
        set_.Add(raceFormID)   ' addIfNeeded: idempotent by construction
    End Sub

    ''' <summary>Build the catalog from the current load order. Skyrim-only; returns an empty catalog for FO4 or
    ''' when no mod in the load order uses RaceCompatibility.</summary>
    Public Shared Function Load(pluginManager As PluginManager, game As Config_App.Game_Enum) As RaceCompatibilityCatalog
        Dim cat As New RaceCompatibilityCatalog()
        If pluginManager Is Nothing OrElse game <> Config_App.Game_Enum.Skyrim Then Return cat

        For Each rec In pluginManager.GetRecordsOfType("QUST")
            Try
                Dim scripts = VmadPropertyReader.ReadScripts(rec, pluginManager, game)
                If scripts Is Nothing OrElse scripts.Count = 0 Then Continue For
                Dim generic = scripts.FirstOrDefault(Function(s) String.Equals(s.Name, "GenericRaceController", StringComparison.OrdinalIgnoreCase))
                If generic Is Nothing Then Continue For

                ' Property name → FormID, on the GenericRaceController instance: these are the VANILLA lists the
                ' script mutates (HeadPartsNord, HeadPartsHuman, RacesElf, PlayableRaceList, …).
                '
                ' Self-repair, verbatim from internalProxyRaces (GenericRaceController.psc:139-145, comment:
                ' "since these we're added later, let's try to repair the damage, if needed"): the two BEAST-vampire
                ' properties were added to GenericRaceController in a LATER version of RaceCompatibility, so a race
                ' mod authored against the older one has a QUST whose VMAD does not bind them — they come in as None.
                ' Rather than lose those lists, the script fetches them straight from Skyrim.esm by FormID. Same
                ' thing here: an unbound property falls back to the hardcoded vanilla FormList (the hardcoding is
                ' the SCRIPT's, not ours — we are reproducing what the game does).
                Dim target = Function(propName As String) As UInteger
                                 Dim pv As VmadPropertyReader.PropValue = Nothing
                                 If generic.Properties.TryGetValue(propName, pv) AndAlso pv.Kind = VmadPropertyReader.PropKind.Obj AndAlso pv.FormID <> 0UI Then Return pv.FormID
                                 ' Game.GetFormFromFile(0x000D82FA / 0x000D82FB, "Skyrim.esm")
                                 If String.Equals(propName, "HeadPartsArgonianVampire", StringComparison.OrdinalIgnoreCase) Then Return VanillaForm(pluginManager, &HD82FAUI)
                                 If String.Equals(propName, "HeadPartsKhajiitVampire", StringComparison.OrdinalIgnoreCase) Then Return VanillaForm(pluginManager, &HD82FBUI)
                                 Return 0UI
                             End Function

                ' The mod's own controller script(s) on the same quest: its .pex says which of ITS FormList
                ' properties feeds each vanilla slot (NewNord / NewNordVampire / …).
                For Each modScript In scripts
                    If modScript Is generic Then Continue For
                    Dim pex = FilesDictionary_class.GetBytes($"scripts\{modScript.Name}.pex")
                    If pex Is Nothing OrElse pex.Length = 0 Then Continue For
                    Dim bindings = PapyrusPexParser.ExtractPropertyBindings(pex)   ' e.g. "NewNord" → "HeadPartsNord_DZ"
                    If bindings.Count = 0 Then Continue For

                    ' Resolve, per slot, the mod's race FormList (standard + vampire).
                    Dim raceList(SlotNames.Length - 1) As UInteger
                    Dim vampList(SlotNames.Length - 1) As UInteger
                    For i = 0 To SlotNames.Length - 1
                        raceList(i) = ModList(modScript, bindings, "New" & SlotNames(i))
                        vampList(i) = ModList(modScript, bindings, "New" & SlotNames(i) & "Vampire")
                    Next

                    cat.ApplyProxyRaces(pluginManager, raceList, vampList, target)
                Next
            Catch ex As Exception
                Logger.LogLazy(Function() $"[RACECOMPAT] QUST 0x{rec.Header.FormID:X8}: {ex.GetType().Name}: {ex.Message}")
            End Try
        Next

        If cat.AugmentedListCount > 0 Then
            Logger.LogLazy(Function() $"[RACECOMPAT] reconstructed proxyRaces: {cat.InjectedRaceCount} custom races injected into {cat.AugmentedListCount} FormLists.")
        End If
        Return cat
    End Function

    ''' <summary>The FormList the mod bound to a given slot: the .pex says which property feeds it, the VMAD says
    ''' what that property holds. Missing binding (the script assigns <c>= None</c>) ⇒ 0.</summary>
    Private Shared Function ModList(modScript As VmadPropertyReader.ScriptEntry, bindings As Dictionary(Of String, String),
                                    slotProperty As String) As UInteger
        Dim srcProp As String = Nothing
        If Not bindings.TryGetValue(slotProperty, srcProp) Then Return 0UI
        Dim pv As VmadPropertyReader.PropValue = Nothing
        If Not modScript.Properties.TryGetValue(srcProp, pv) Then Return 0UI
        If pv.Kind <> VmadPropertyReader.PropKind.Obj Then Return 0UI
        Return pv.FormID
    End Function

    ''' <summary>internalProxyRaces (GenericRaceController.psc:110-181): for each slot, for each race in the mod's
    ''' list, insert it into the vanilla lists. The vampire race is taken from the PARALLEL list AT THE SAME INDEX
    ''' — that pairing is the script's, not ours.</summary>
    Private Sub ApplyProxyRaces(pm As PluginManager, raceList As UInteger(), vampList As UInteger(),
                                target As Func(Of String, UInteger))
        For element = 0 To SlotNames.Length - 1
            Dim races = FlstItems(pm, raceList(element))
            If races.Count = 0 Then Continue For
            Dim vampires = FlstItems(pm, vampList(element))

            For index = 0 To races.Count - 1
                Dim standardRace = races(index)
                If standardRace = 0UI Then Continue For
                Add(target("PlayableRaceList"), standardRace)
                UpdateStandardRaces(element, standardRace, target)

                If index < vampires.Count Then
                    Dim vampireRace = vampires(index)
                    If vampireRace <> 0UI Then
                        Add(target("PlayableVampireList"), vampireRace)
                        UpdateVampireRaces(element, vampireRace, target)
                    End If
                End If
            Next
        Next
    End Sub

    ''' <summary>updateStandardRaces (GenericRaceController.psc:189-228), rule for rule.</summary>
    Private Sub UpdateStandardRaces(element As Integer, race As UInteger, target As Func(Of String, UInteger))
        Dim slot = SlotNames(element)
        ' Self listing: its own slot's list, and its own slot's "…andVampire" list.
        Add(target($"HeadParts{slot}"), race)
        Add(target($"HeadParts{slot}andVampire"), race)

        If Not IsHuman(element) Then Add(target("RacesBeast"), race)

        If IsHuman(element) OrElse IsOrc(element) OrElse IsElf(element) Then Add(target("HeadPartsAllRacesMinusBeast"), race)
        If IsHuman(element) Then
            Add(target("RacesHuman"), race)
            Add(target("HeadPartsHuman"), race)
            Add(target("HeadPartsHumansandVampires"), race)
            Add(target("HeadPartsHumansOrcsandVampires"), race)
        End If
        If IsOrc(element) Then Add(target("HeadPartsHumansOrcsandVampires"), race)
        If IsElf(element) Then
            Add(target("RacesElf"), race)
            Add(target("HeadPartsElves"), race)
            Add(target("HeadPartsElvesandVampires"), race)
        End If
        If IsBreton(element) OrElse IsNord(element) OrElse IsImperial(element) Then Add(target("HeadPartsBretsNordsImpsandVampires"), race)
        If IsArgonian(element) Then
            Add(target("HeadPartsArgonian"), race)
            Add(target("HeadPartsArgonianandVampire"), race)
        End If
        If IsKhajiit(element) Then
            Add(target("HeadPartsKhajiit"), race)
            Add(target("HeadPartsKhajiitandVampire"), race)
        End If
    End Sub

    ''' <summary>updateVampireRaces (GenericRaceController.psc:230-266), rule for rule.</summary>
    Private Sub UpdateVampireRaces(element As Integer, race As UInteger, target As Func(Of String, UInteger))
        Dim slot = SlotNames(element)
        Add(target($"HeadParts{slot}andVampire"), race)   ' hpVampList = HeadPartsVampireList[element]

        If IsHuman(element) OrElse IsOrc(element) OrElse IsElf(element) Then Add(target("HeadPartsAllRacesMinusBeast"), race)
        If IsArgonian(element) OrElse IsKhajiit(element) Then Add(target("RacesBeast"), race)
        If IsHuman(element) Then
            Add(target("HeadPartsHumanVampires"), race)
            Add(target("HeadPartsHumansandVampires"), race)
            Add(target("HeadPartsHumansOrcsandVampires"), race)
            Add(target("HeadPartsHumanoidVampire"), race)
        End If
        If IsOrc(element) Then Add(target("HeadPartsHumansOrcsandVampires"), race)
        If IsElf(element) Then Add(target("HeadPartsElvesandVampires"), race)
        If IsBreton(element) OrElse IsNord(element) OrElse IsImperial(element) Then Add(target("HeadPartsBretsNordsImpsandVampires"), race)
        If IsOrc(element) OrElse IsElf(element) Then Add(target("HeadPartsHumanoidVampire"), race)
        If IsArgonian(element) Then
            Add(target("HeadPartsArgonianVampire"), race)
            Add(target("HeadPartsArgonianandVampire"), race)
        End If
        If IsKhajiit(element) Then
            Add(target("HeadPartsKhajiitVampire"), race)
            Add(target("HeadPartsKhajiitandVampire"), race)
        End If
    End Sub

    ''' <summary>The engine's <c>Game.GetFormFromFile(localFormID, "Skyrim.esm")</c>: resolve a Skyrim.esm-local
    ''' FormID against the current load order. 0 when Skyrim.esm is not loaded or the form is not a FormList
    ''' (a mod could, in principle, override it into something else — then we do NOT touch it).</summary>
    Private Shared Function VanillaForm(pm As PluginManager, skyrimLocalFormID As UInteger) As UInteger
        Try
            Dim g = pm.ResolveReferencedFormID("Skyrim.esm", skyrimLocalFormID)
            If g = 0UI Then Return 0UI
            Dim rec = pm.GetRecord(g)
            If rec Is Nothing OrElse rec.Header.Signature <> "FLST" Then Return 0UI
            Return g
        Catch
            Return 0UI
        End Try
    End Function

    Private Shared Function FlstItems(pm As PluginManager, flstFormID As UInteger) As List(Of UInteger)
        If flstFormID = 0UI Then Return New List(Of UInteger)
        Dim rec = pm.GetRecord(flstFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "FLST" Then Return New List(Of UInteger)
        Return RecordParsers.ParseFLST(rec, pm).ItemFormIDs
    End Function
End Class
