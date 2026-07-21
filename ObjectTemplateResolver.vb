Imports System.Linq
Imports FO4_Base_Library

''' <summary>
''' Canonical resolver for ARMO/NPC_ ObjectTemplate (OBTE/OBTS) combinations.
''' </summary>
Public Module ObjectTemplateResolver

    ''' <summary>Resolved combination payload in engine application order.</summary>
    Public Class CombinationResolution
        Public AppliedCombinations As New List(Of ARMO_Combination)
        Public DirectProperties As New List(Of OMOD_Property)
        Public IncludedOmods As New List(Of OMOD_Data)
        Public IncludedOmodApIdx As New List(Of Byte)
        Public IncludedOmodHostFormID As New List(Of UInteger)
        Public IncludedOmodHostApIdx As New List(Of Byte)
        Public IncludedOmodInstanceOrdinal As New List(Of Integer)
        Public IncludedOmodHostInstanceOrdinal As New List(Of Integer)
    End Class

    ''' <param name="rngSeed">
    ''' Controls the DontUseAll include choice — see SelectDontUseAllIndex.  Leave Nothing (the
    ''' default) for a fully deterministic, reproducible resolution: identical inputs always yield
    ''' an identical result, which is what bake/export/hash-comparison paths require.  Pass a value
    ''' only when you deliberately want variation (an interactive re-roll); the same seed then
    ''' always reproduces the same variation.
    ''' </param>
    Public Function ResolveArmoCombinations(armo As ARMO_Data,
                                            ctxKeywords As List(Of UInteger),
                                            pm As PluginManager,
                                            Optional rngSeed As Integer? = Nothing) As CombinationResolution
        Dim result As New CombinationResolution()
        If armo Is Nothing OrElse armo.Combinations Is Nothing OrElse armo.Combinations.Count = 0 Then
            Return result
        End If

        Dim initialPool As New HashSet(Of UInteger)
        If armo.AttachParentSlotFormIDs IsNot Nothing Then
            For Each fid In armo.AttachParentSlotFormIDs
                If fid <> 0UI Then initialPool.Add(fid)
            Next
        End If

        ResolveCombinationList(armo.Combinations, ctxKeywords, pm, result, initialPool, rngSeed)
        Return result
    End Function

    ''' <param name="rngSeed">See ResolveArmoCombinations. Nothing (default) = deterministic.</param>
    Public Function ResolveNpcCombinations(npc As NPC_Data,
                                           ctxKeywords As List(Of UInteger),
                                           pm As PluginManager,
                                           Optional rngSeed As Integer? = Nothing) As CombinationResolution
        Dim result As New CombinationResolution()
        If npc Is Nothing OrElse Not npc.HasObjectTemplate OrElse npc.ObjectTemplateCombinations Is Nothing Then
            Return result
        End If

        Dim flat As New List(Of ARMO_Combination)
        For Each hdr In npc.ObjectTemplateCombinations
            If hdr.Combination IsNot Nothing Then flat.Add(hdr.Combination)
        Next

        Dim initialPool As New HashSet(Of UInteger)
        If npc.AttachParentSlotFormIDs IsNot Nothing Then
            For Each fid In npc.AttachParentSlotFormIDs
                If fid <> 0UI Then initialPool.Add(fid)
            Next
        End If

        If npc.RaceFormID <> 0UI AndAlso pm IsNot Nothing Then
            Dim raceRec = pm.GetRecord(npc.RaceFormID)
            If raceRec IsNot Nothing AndAlso raceRec.Header.Signature = "RACE" Then
                Dim race = RecordParsers.ParseRACE(raceRec, pm)
                If race IsNot Nothing AndAlso race.AttachParentSlotFormIDs IsNot Nothing Then
                    For Each fid In race.AttachParentSlotFormIDs
                        If fid <> 0UI Then initialPool.Add(fid)
                    Next
                End If
            End If
        End If

        ResolveCombinationList(flat, ctxKeywords, pm, result, initialPool, rngSeed)
        Return result
    End Function

    Public Function KywdEditorIdSafe(fid As UInteger, pm As PluginManager) As String
        If fid = 0UI Then Return "-"
        If pm Is Nothing Then Return "?"
        Try
            Dim r = pm.GetRecord(fid)
            If r Is Nothing Then Return "?MISSING"
            If r.Header.Signature <> "KYWD" Then Return "?" & r.Header.Signature
            Return If(r.EditorID, "?NOEDID")
        Catch ex As Exception
            Return "?EX:" & ex.GetType().Name
        End Try
    End Function

    Private Sub ResolveCombinationList(combos As List(Of ARMO_Combination),
                                       ctxKeywords As List(Of UInteger),
                                       pm As PluginManager,
                                       result As CombinationResolution,
                                       initialApPool As HashSet(Of UInteger),
                                       rngSeed As Integer?)
        Dim logEnabled = Logger.Enabled
        Dim ctxKeywordSet As HashSet(Of UInteger) = Nothing
        If ctxKeywords IsNot Nothing AndAlso ctxKeywords.Count > 0 Then
            ctxKeywordSet = New HashSet(Of UInteger)(ctxKeywords)
        End If

        If logEnabled Then
            Dim ctxKwStr = If(ctxKeywords Is Nothing OrElse ctxKeywords.Count = 0,
                              "(empty)",
                              String.Join(",", ctxKeywords.Select(Function(k) "0x" & k.ToString("X8") & "(" & KywdEditorIdSafe(k, pm) & ")")))
            Logger.LogLazy(Function() $"[OBTE-RESOLVE-START] combos={combos.Count} ctxKeywords={ctxKwStr} initialPool={initialApPool.Count}")
        End If

        Dim applicable As New List(Of ARMO_Combination)
        Dim comboIdx As Integer = 0
        For Each combo In combos
            Dim curIdx = comboIdx
            comboIdx += 1
            If combo Is Nothing Then
                If logEnabled Then
                    Logger.LogLazy(Function() $"[OBTE-COMBO] idx={curIdx} NULL combo - skipped")
                End If
                Continue For
            End If

            Dim isApplicable As Boolean = combo.IsDefault
            Dim reason As String = If(combo.IsDefault, "Default", "")
            If Not isApplicable AndAlso combo.Keywords IsNot Nothing AndAlso combo.Keywords.Count > 0 AndAlso ctxKeywordSet IsNot Nothing Then
                For Each kw In combo.Keywords
                    If ctxKeywordSet.Contains(kw) Then
                        isApplicable = True
                        reason = $"KWMatch(0x{kw:X8}={KywdEditorIdSafe(kw, pm)})"
                        Exit For
                    End If
                Next
            End If

            If logEnabled Then
                Dim kwsStr = If(combo.Keywords Is Nothing OrElse combo.Keywords.Count = 0,
                                "[]",
                                "[" & String.Join(",", combo.Keywords.Select(Function(k) "0x" & k.ToString("X8") & "(" & KywdEditorIdSafe(k, pm) & ")")) & "]")
                Dim incCount = If(combo.Includes Is Nothing, 0, combo.Includes.Count)
                Dim propCount = If(combo.Properties Is Nothing, 0, combo.Properties.Count)
                Dim reasonLog = If(isApplicable, reason, "INERT-no-default-no-kwmatch")
                Logger.LogLazy(Function() $"[OBTE-COMBO] idx={curIdx} isDefault={combo.IsDefault} kw={kwsStr} inc={incCount} props={propCount} applicable={isApplicable} reason={reasonLog}")
            End If

            If isApplicable Then applicable.Add(combo)
        Next

        If applicable.Count = 0 Then
            For Each combo In combos
                If combo Is Nothing Then Continue For
                If logEnabled Then
                    Logger.LogLazy(Function() $"[OBTE-FALLBACK] no combo applicable -> forcing first non-null combo")
                End If
                applicable.Add(combo)
                Exit For
            Next
        End If

        If applicable.Count = 0 Then Return
        result.AppliedCombinations.AddRange(applicable)

        Dim instanceOrdinalCounter As Integer = 0
        Dim pathStack As New HashSet(Of (UInteger, Byte))
        Dim candidates As New List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer))
        Dim unslottedOmods As New List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer))

        For Each combo In applicable
            If combo.Properties IsNot Nothing Then
                For Each prop In combo.Properties
                    result.DirectProperties.Add(prop)
                Next
            End If
            If combo.Includes Is Nothing Then Continue For

            For Each inc In combo.Includes
                If inc Is Nothing OrElse inc.ModFormID = 0UI Then Continue For
                If logEnabled Then
                    Dim incLocal = inc
                    Logger.LogLazy(Function() $"[OBTE-INC] modFid=0x{incLocal.ModFormID:X8} apIdx={incLocal.AttachPointIndex} dontUseAll={incLocal.DontUseAll}")
                End If
                CollectOmodCandidate(inc.ModFormID, inc.AttachPointIndex, inc.DontUseAll, pm, pathStack, candidates, unslottedOmods, instanceOrdinalCounter, rngSeed)
            Next
        Next

        Dim apPool As New HashSet(Of UInteger)(initialApPool)
        If logEnabled Then
            Dim apPoolBeforeStr = String.Join(",", apPool.Select(Function(f) "0x" & f.ToString("X8") & "(" & KywdEditorIdSafe(f, pm) & ")"))
            Logger.LogLazy(Function() $"[OBTE-POOL-INIT] initial pool({apPool.Count}) = [{apPoolBeforeStr}]")
        End If

        Dim accepted As New List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer, HostInstanceOrdinal As Integer, HostFormID As UInteger, HostApIdx As Byte))
        Dim apProvider As New Dictionary(Of UInteger, List(Of (HostOrdinal As Integer, HostFid As UInteger, HostApIdx As Byte)))
        For Each seedAp In initialApPool
            If Not apProvider.ContainsKey(seedAp) Then
                apProvider(seedAp) = New List(Of (Integer, UInteger, Byte)) From {(0, 0UI, CByte(0))}
            End If
        Next

        Dim pending As New List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer))(candidates)
        Dim iterations As Integer = 0
        Const maxIter As Integer = 16
        Do
            iterations += 1
            Dim changed As Boolean = False
            Dim stillPending As New List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer))

            For Each cand In pending
                If Not apPool.Contains(cand.Omod.AttachPointFormID) Then
                    stillPending.Add(cand)
                    Continue For
                End If

                Dim hostOrd As Integer = 0
                Dim hostFid As UInteger = 0UI
                Dim hostApIdxResolved As Byte = 0
                Dim hostList As List(Of (HostOrdinal As Integer, HostFid As UInteger, HostApIdx As Byte)) = Nothing
                If apProvider.TryGetValue(cand.Omod.AttachPointFormID, hostList) AndAlso hostList.Count > 0 Then
                    If hostList.Count = 1 Then
                        Dim only0 = hostList(0)
                        hostOrd = only0.HostOrdinal
                        hostFid = only0.HostFid
                        hostApIdxResolved = only0.HostApIdx
                    Else
                        Dim hasFirstMatch As Boolean = False
                        Dim firstMatch As (HostOrdinal As Integer, HostFid As UInteger, HostApIdx As Byte) = Nothing
                        Dim matchCount As Integer = 0
                        For Each provider In hostList
                            If provider.HostApIdx <> cand.ApIdx Then Continue For
                            If Not hasFirstMatch Then
                                firstMatch = provider
                                hasFirstMatch = True
                            End If
                            matchCount += 1
                        Next

                        If matchCount > 0 Then
                            hostOrd = firstMatch.HostOrdinal
                            hostFid = firstMatch.HostFid
                            hostApIdxResolved = firstMatch.HostApIdx
                        Else
                            Dim first0 = hostList(0)
                            hostOrd = first0.HostOrdinal
                            hostFid = first0.HostFid
                            hostApIdxResolved = first0.HostApIdx
                        End If

                        If logEnabled Then
                            Dim apFidL = cand.Omod.AttachPointFormID
                            Dim providersStr = String.Join(",", hostList.Select(Function(p) $"(ord={p.HostOrdinal},0x{p.HostFid:X8},apIdx={p.HostApIdx})"))
                            If matchCount = 1 Then
                                Logger.LogLazy(Function() $"[OBTE-AP-MULTI-PROVIDER] cand={cand.Omod.EditorID}(0x{cand.Omod.FormID:X8}) ord={cand.InstanceOrdinal} apIdx={cand.ApIdx} ap=0x{apFidL:X8}({KywdEditorIdSafe(apFidL, pm)}) providers({hostList.Count})=[{providersStr}] -> apIdx-match unique ord={hostOrd} (0x{hostFid:X8})")
                            ElseIf matchCount > 1 Then
                                Logger.LogLazy(Function() $"[OBTE-AP-AMBIGUOUS-MATCH] cand={cand.Omod.EditorID}(0x{cand.Omod.FormID:X8}) ord={cand.InstanceOrdinal} apIdx={cand.ApIdx} ap=0x{apFidL:X8}({KywdEditorIdSafe(apFidL, pm)}) providers({hostList.Count})=[{providersStr}] {matchCount} apIdx matches -> first-wins ord={hostOrd}")
                            Else
                                Logger.LogLazy(Function() $"[OBTE-AP-NO-IDX-MATCH] cand={cand.Omod.EditorID}(0x{cand.Omod.FormID:X8}) ord={cand.InstanceOrdinal} apIdx={cand.ApIdx} ap=0x{apFidL:X8}({KywdEditorIdSafe(apFidL, pm)}) providers({hostList.Count})=[{providersStr}] no provider with apIdx={cand.ApIdx} -> fallback first-wins ord={hostOrd}")
                            End If
                        End If
                    End If
                End If

                accepted.Add((cand.Omod, cand.ApIdx, cand.InstanceOrdinal, hostOrd, hostFid, hostApIdxResolved))
                changed = True

                If cand.Omod.AttachParentSlotFormIDs IsNot Nothing Then
                    For Each fid In cand.Omod.AttachParentSlotFormIDs
                        If fid = 0UI Then Continue For
                        apPool.Add(fid)
                        Dim plist As List(Of (HostOrdinal As Integer, HostFid As UInteger, HostApIdx As Byte)) = Nothing
                        If Not apProvider.TryGetValue(fid, plist) Then
                            plist = New List(Of (Integer, UInteger, Byte))
                            apProvider(fid) = plist
                        End If
                        plist.Add((cand.InstanceOrdinal, cand.Omod.FormID, cand.ApIdx))
                    Next
                End If

                If logEnabled Then
                    Dim addedApStr = If(cand.Omod.AttachParentSlotFormIDs Is Nothing,
                                        "(none)",
                                        String.Join(",", cand.Omod.AttachParentSlotFormIDs.Select(Function(f) "0x" & f.ToString("X8") & "(" & KywdEditorIdSafe(f, pm) & ")")))
                    Logger.LogLazy(Function() $"[OBTE-POOL-ACCEPT] omod={cand.Omod.EditorID}(0x{cand.Omod.FormID:X8}) ord={cand.InstanceOrdinal} ap=0x{cand.Omod.AttachPointFormID:X8}({KywdEditorIdSafe(cand.Omod.AttachPointFormID, pm)}) host=(ord={hostOrd},0x{hostFid:X8},apIdx={hostApIdxResolved}) addedAPs=[{addedApStr}]")
                End If
            Next

            pending = stillPending
            If Not changed Then Exit Do
            If iterations >= maxIter Then Exit Do
        Loop

        If logEnabled Then
            For Each rej In pending
                Logger.LogLazy(Function() $"[OBTE-POOL-REJECT] omod={rej.Omod.EditorID}(0x{rej.Omod.FormID:X8}) ftype={rej.Omod.FormTypeSignature} ap=0x{rej.Omod.AttachPointFormID:X8}({KywdEditorIdSafe(rej.Omod.AttachPointFormID, pm)}) (not in pool)")
            Next
            Logger.LogLazy(Function() $"[OBTE-RESOLVE] applicable={applicable.Count} collected={candidates.Count} accepted={accepted.Count} rejected={pending.Count} unslotted={unslottedOmods.Count} iterations={iterations}")
        End If

        Dim dedupedAccepted As New List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer, HostInstanceOrdinal As Integer, HostFormID As UInteger, HostApIdx As Byte))
        Dim slotSeen As New Dictionary(Of (UInteger, Byte, Integer), Integer)
        For Each entry In accepted
            Dim key = (entry.Omod.AttachPointFormID, entry.ApIdx, entry.HostInstanceOrdinal)
            Dim existingIdx As Integer
            If slotSeen.TryGetValue(key, existingIdx) Then
                dedupedAccepted(existingIdx) = entry
            Else
                slotSeen(key) = dedupedAccepted.Count
                dedupedAccepted.Add(entry)
            End If
        Next

        Dim droppedDup = accepted.Count - dedupedAccepted.Count
        If droppedDup > 0 AndAlso logEnabled Then
            Dim keptL = dedupedAccepted.Count
            Logger.LogLazy(Function() $"[SLOT-DEDUP] accepted={droppedDup + keptL} -> kept={keptL} dropped={droppedDup} (slot-by-slot last-wins by (AttachPoint,apIdx,host))")
        End If
        accepted = dedupedAccepted

        For Each entry In accepted
            result.IncludedOmods.Add(entry.Omod)
            result.IncludedOmodApIdx.Add(entry.ApIdx)
            result.IncludedOmodInstanceOrdinal.Add(entry.InstanceOrdinal)
            result.IncludedOmodHostInstanceOrdinal.Add(entry.HostInstanceOrdinal)
            result.IncludedOmodHostFormID.Add(entry.HostFormID)
            result.IncludedOmodHostApIdx.Add(entry.HostApIdx)
        Next

        For Each entry In unslottedOmods
            result.IncludedOmods.Add(entry.Omod)
            result.IncludedOmodApIdx.Add(entry.ApIdx)
            result.IncludedOmodInstanceOrdinal.Add(entry.InstanceOrdinal)
            result.IncludedOmodHostInstanceOrdinal.Add(0)
            result.IncludedOmodHostFormID.Add(0UI)
            result.IncludedOmodHostApIdx.Add(CByte(0))
        Next
    End Sub

    Private Sub CollectOmodCandidate(omodFid As UInteger,
                                     apIdx As Byte,
                                     dontUseAll As Boolean,
                                     pm As PluginManager,
                                     pathStack As HashSet(Of (UInteger, Byte)),
                                     candidates As List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer)),
                                     unslotted As List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer)),
                                     ByRef instanceOrdinalCounter As Integer,
                                     rngSeed As Integer?)
        If omodFid = 0UI Then Return

        Dim logEnabled = Logger.Enabled
        Dim pathKey = (omodFid, apIdx)
        If pathStack.Contains(pathKey) Then
            If logEnabled Then
                Dim fl = omodFid
                Dim al = apIdx
                Logger.LogLazy(Function() $"[OBTE-CYCLE] omod=0x{fl:X8} apIdx={al} already in current path - cycle skipped")
            End If
            Return
        End If

        pathStack.Add(pathKey)
        Try
            Dim rec = pm.GetRecord(omodFid)
            If rec Is Nothing OrElse rec.Header.Signature <> "OMOD" Then Return

            Dim omod = CraftingRecordParsers.ParseOMOD(rec, pm)

            If omod.AttachPointFormID <> 0UI Then
                instanceOrdinalCounter += 1
                Dim ord = instanceOrdinalCounter
                candidates.Add((omod, apIdx, ord))
                If logEnabled Then
                    Dim parentSlotsStr = If(omod.AttachParentSlotFormIDs Is Nothing,
                                            "(none)",
                                            String.Join(",", omod.AttachParentSlotFormIDs.Select(Function(f) "0x" & f.ToString("X8") & "(" & KywdEditorIdSafe(f, pm) & ")")))
                    Logger.LogLazy(Function() $"[OBTE-CAND] omod={omod.EditorID}(0x{omodFid:X8}) ord={ord} ftype={omod.FormTypeSignature} ap=0x{omod.AttachPointFormID:X8}({KywdEditorIdSafe(omod.AttachPointFormID, pm)}) apIdx={apIdx} model='{omod.ModelPath}' parentSlots=[{parentSlotsStr}]")
                End If
                If omod.Includes IsNot Nothing AndAlso omod.Includes.Count > 0 Then
                    RecurseContainerIncludes(omod, apIdx, dontUseAll, pm, pathStack, candidates, unslotted, instanceOrdinalCounter, rngSeed)
                End If
                Return
            End If

            ' AttachPoint == 0 → this OMOD is a CONTAINER.  Its own Properties and its Includes
            ' are INDEPENDENT payloads: it may carry either, both, or neither.
            '
            ' This used to be an if/else-if chain gated on "has Properties AND has NO Includes",
            ' so a container carrying BOTH fell through to the recursion branch and its own
            ' Properties were never added to the unslotted bucket → material/model swaps silently
            ' discarded with no diagnostic.  Both payloads are now processed unconditionally.
            '
            ' MEASURED (vanilla FO4 + all DLC, 3987 unique OMODs): ZERO containers carry both —
            ' containers are strictly bimodal, 43 properties-only and 68 includes-only.  So this
            ' was a LATENT defect, not a live one: no vanilla NPC changes as a result of this fix.
            ' Nothing in the OBTE format forbids the combination, so mod-authored records can hit
            ' it; that is why it is fixed rather than asserted away.
            Dim hasProps As Boolean = omod.Properties IsNot Nothing AndAlso omod.Properties.Count > 0
            Dim hasIncludes As Boolean = omod.Includes IsNot Nothing AndAlso omod.Includes.Count > 0

            If hasProps Then
                instanceOrdinalCounter += 1
                Dim ordU = instanceOrdinalCounter
                unslotted.Add((omod, apIdx, ordU))
                If logEnabled Then
                    Dim kindL = If(hasIncludes, "properties+includes", "properties-only")
                    Logger.LogLazy(Function() $"[OBTE-CAND] omod={omod.EditorID}(0x{omodFid:X8}) ord={ordU} ap=0 (container, {kindL}) -> unslotted bucket")
                End If
            End If

            If hasIncludes Then
                If logEnabled Then
                    Logger.LogLazy(Function() $"[OBTE-CAND] omod={omod.EditorID}(0x{omodFid:X8}) ap=0 (container, recurse children, dontUseAll={dontUseAll})")
                End If
                RecurseContainerIncludes(omod, apIdx, dontUseAll, pm, pathStack, candidates, unslotted, instanceOrdinalCounter, rngSeed)
            End If
        Finally
            pathStack.Remove(pathKey)
        End Try
    End Sub

    Private Sub RecurseContainerIncludes(omod As OMOD_Data,
                                         apIdx As Byte,
                                         dontUseAll As Boolean,
                                         pm As PluginManager,
                                         pathStack As HashSet(Of (UInteger, Byte)),
                                         candidates As List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer)),
                                         unslotted As List(Of (Omod As OMOD_Data, ApIdx As Byte, InstanceOrdinal As Integer)),
                                         ByRef instanceOrdinalCounter As Integer,
                                         rngSeed As Integer?)
        If omod.Includes Is Nothing OrElse omod.Includes.Count = 0 Then Return

        Dim validIncludes As New List(Of OMOD_Include)
        For Each inc In omod.Includes
            If inc Is Nothing OrElse inc.ModFormID = 0UI Then Continue For
            validIncludes.Add(inc)
        Next
        If validIncludes.Count = 0 Then Return

        Dim logEnabled = Logger.Enabled
        If dontUseAll Then
            Dim pickIdx As Integer = SelectDontUseAllIndex(omod, apIdx, validIncludes.Count, rngSeed)
            Dim pick = validIncludes(pickIdx)
            If logEnabled Then
                Dim pickL = pick
                Dim idxL = pickIdx
                Dim modeL = If(rngSeed.HasValue, $"seeded({rngSeed.Value})", "first-wins")
                Logger.LogLazy(Function() $"[OBTE-DONTUSEALL-PICK] parent={omod.EditorID}(0x{omod.FormID:X8}) mode={modeL} picks idx={idxL} modFid=0x{pickL.ModFormID:X8} (of {validIncludes.Count})")
            End If
            CollectOmodCandidate(pick.ModFormID, 0, pick.DontUseAll, pm, pathStack, candidates, unslotted, instanceOrdinalCounter, rngSeed)
            Return
        End If

        For Each inc In validIncludes
            CollectOmodCandidate(inc.ModFormID, 0, inc.DontUseAll, pm, pathStack, candidates, unslotted, instanceOrdinalCounter, rngSeed)
        Next
    End Sub

    ''' <summary>
    ''' Chooses WHICH include a DontUseAll container contributes.  This decides which OMOD — and
    ''' therefore which model and which material — is written to the NIF, so it must never depend
    ''' on ambient process state.
    '''
    ''' ENGINE LAW (VERIFIED FROM BINARY — Fallout4.exe, image base 0x140000000; include evaluator
    ''' at 0x140251AAE, DontUseAll gate at 0x140251D19, picker at 0x140251740):
    '''   - The engine applies EXACTLY ONE include, never a subset (single call to the apply path
    '''     0x140251A70 at 0x1402521B0, versus the use-all loop at 0x140251D50..0x140251D9C).
    '''     Picking one, as we do, is structurally correct.
    '''   - Its choice is RANDOM, drawn from a PROCESS-GLOBAL RNG stream (randRange 0x14165B140
    '''     over global state 0x142F3F6A0 — the same stream Papyrus Utility.RandomInt uses).  It is
    '''     seeded from NOTHING stable: not the base form, not the RefID, not the instance.
    '''
    ''' Therefore the engine's selection is NON-REPRODUCIBLE BY DESIGN.  There is no seed we could
    ''' derive that would make a bake match a specific in-game actor — being engine-faithful and
    ''' being deterministic are mutually exclusive here.  A bake must be reproducible, so it takes
    ''' the deterministic side, and that choice is a documented BAKE CONVENTION, NOT an engine law:
    '''
    '''   rngSeed = Nothing (default)  → first-wins.  Index 0 of the record-order include list.
    '''                                  Fully deterministic, derives from nothing but the record,
    '''                                  and is the most conservative choice available: it invents
    '''                                  no distribution and no per-instance mimicry.
    '''   rngSeed = supplied           → deterministic FUNCTION of (seed, container FormID, apIdx).
    '''                                  Lets a caller that WANTS variation (an interactive re-roll)
    '''                                  get it while keeping every run reproducible from its seed.
    '''
    ''' What this replaces: Random.Shared.Next, which made the SAME NPC produce DIFFERENT NIFs on
    ''' two runs.  That silently invalidated every byte-identical-hash comparison downstream — a
    ''' diff could be noise and a match could be luck.
    '''
    ''' MEASURED blast radius (vanilla FO4 + all DLC, 3987 unique OMODs): 493 OMODs hold two or
    ''' more valid DontUseAll includes and were therefore genuinely nondeterministic (candidate
    ''' pools of 2..30; the weapon/armor/robot modcol_* part-and-material collections).  This was a
    ''' LIVE defect, not a latent one.
    '''
    ''' ⚠ KNOWN DIVERGENCES from the engine, surfaced by the RE above and deliberately NOT fixed
    ''' here (they are separate defects, each needing context this resolver is not given — recorded
    ''' so they are not mistaken for settled behaviour):
    '''   1. MINIMUM LEVEL is not filtered.  The engine drops includes whose Minimum Level exceeds
    '''      the item/actor level, on BOTH the use-all and the pick-one path (0x140251D50).  We
    '''      consider every include regardless of level; no level is threaded into this resolver.
    '''   2. LEVEL-TIER PREFERENCE is not applied.  The engine does not choose uniformly over the
    '''      valid includes: it keeps only the highest-level-tier survivors and randomizes within
    '''      that window (picker 0x140251740).  A uniform/first-wins choice over the whole list
    '''      therefore over-selects LOW-tier mods relative to the engine.
    '''   3. The OMOD FORM's own "don't use all" bit is ignored.  A second, independent gate at
    '''      0x140251D24 reads it from the OMOD record itself (one of xEdit's OMOD DATA "Unknown
    '''      Bool" fields, wbDefinitionsFO4.pas:12861-12862); either source triggers the same path.
    '''      We honour only the include-side flag, so some containers that the engine treats as
    '''      pick-one are still expanded as use-all here.
    '''
    ''' The mixer below is written out explicitly rather than using GetHashCode so the mapping is
    ''' stable across processes, runtimes and platforms — GetHashCode guarantees none of that.
    '''
    ''' It is XOR/shift ONLY (xorshift64), deliberately with no multiply step: this project does not
    ''' set RemoveIntegerChecks, so VB's integer overflow checking is ON and the wrap-around
    ''' multiply that a classic FNV/SplitMix mixer depends on would raise OverflowException at
    ''' runtime.  Shift and XOR never overflow-check, so this stays exception-free.
    ''' </summary>
    Private Function SelectDontUseAllIndex(omod As OMOD_Data,
                                           apIdx As Byte,
                                           count As Integer,
                                           rngSeed As Integer?) As Integer
        If count <= 1 Then Return 0
        If Not rngSeed.HasValue Then Return 0

        ' Pack the case identity into one 64-bit word, on disjoint bit ranges so no two distinct
        ' (seed, FormID, apIdx) triples collide before mixing.  The constant guarantees a non-zero
        ' state (xorshift is a fixed point at zero).
        Dim h As ULong = CULng(CUInt(rngSeed.Value)) Xor
                         (CULng(omod.FormID) << 32) Xor
                         (CULng(apIdx) << 24) Xor
                         &H9E3779B97F4A7C15UL

        ' xorshift64 (Marsaglia 13/7/17) — avalanche without any multiply.
        h = h Xor (h << 13)
        h = h Xor (h >> 7)
        h = h Xor (h << 17)

        ' Fold the high half down so the low bits that Mod consumes carry the whole word's entropy.
        h = h Xor (h >> 32)
        Return CInt(h Mod CULng(count))
    End Function

End Module
