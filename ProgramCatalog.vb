Option Strict On
Option Explicit On
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Threading

''' <summary>THE PROGRAM CATALOG (F9 r2 "Duplicacion" (i), r3 rev-08 / rev-17, r4 rev-17; rev-08 -&gt; (b): user decision 3-nov-2026,
''' literal equality: coordinator decision 3-nov-2026): the one enumeration of every program the library can build, by family, as specs
''' (no GL) - the CENSUS -, each entry marked Asked when the app's request sites can ask for it. THE CATALOG is the asked entries
''' (AllDigests): exactly the domain the app can ask for. The others are transcribed programs the app never asks for (a declared gap of
''' their stages, the UI mask, a census-only family, an image-space program the post does not run): only the census builds them
''' (CensusOnlySpecs). Its owners of the texts are the programs' own spec functions (Fo4DeferredPrograms' shared specs, the control's
''' slot rows, the post's and the SAO's Specs); this module only walks the closed lists that say which exist. Its readers:
''' <list type="bullet">
''' <item>ShadowGate --program-census builds every entry, family by family (one block per family); ShadowGate registers the census-only
''' ones at its start (Register: a Tools host's own programs);</item>
''' <item>Tools\ProgramCatalogTool writes the catalog's digests - the manifest ProgramCatalog.keys - next to each app's library
''' (ProgramCatalog.targets), with the library's MVID; ParityGate program-catalog-manifest checks every manifest it finds against them;</item>
''' <item>the program cache's cleanup (ProgramBinaryCache) keeps only the entries of the installed manifest's programs (and of the specs a
''' host registered, Register); a manifest that is absent, unreadable or of another build of the library (its MVID) deletes nothing by text;</item>
''' <item>ParityGate program-catalog-domain proves it EQUAL to the domain (rev-08 -&gt; (b): production admits every program the app asks
''' for, no admission by catalog): family by family, the domain derived from the engine's id functions over their whole stage lists and
''' the request sites' preconditions against the catalog, both ways;</item>
''' <item>in Debug, ProgramCompiler.Build counts every program whose digest is not in the installed manifest, not registered and with no text a
''' harness declared (GateOutsideManifest): the battery requires 0.</item>
''' </list>
''' The families are the census's blocks (r2 rev-07: the enumeration of ShadowGate --program-census moved here).</summary>
Friend Module ProgramCatalog

    ''' <summary>One program the library can build: its family, its name (what a failure names), its spec, composed on demand (a census
    ''' mutant's broken text is seen by the next composition), and Asked: the app's request sites can ask for it (it is of the catalog).</summary>
    Friend Structure Entry
        Friend ReadOnly Family As String
        Friend ReadOnly Name As String
        Friend ReadOnly Spec As Func(Of ProgramSpec)
        Friend ReadOnly Asked As Boolean
        Friend Sub New(family As String, name As String, spec As Func(Of ProgramSpec), asked As Boolean)
            Me.Family = family
            Me.Name = name
            Me.Spec = spec
            Me.Asked = asked
        End Sub
    End Structure

    ''' <summary>The families, in the census's order: the control's slots and its holders' (both games), the programs outside them (the
    ''' labels, the facetint compositor, Gl43's offset probe), then Fallout 4's transcribed lists and Skyrim SE's (s1_propuesta.md §F3 point 8).</summary>
    Friend ReadOnly Families As String() = {"app-sets", "other", "fo4-gbuffer", "fo4-effect", "fo4-b10", "fo4-b11", "fo4-b07", "fo4-imagespace", "fo4-b06", "sse-lighting", "sse-utility"}

    ''' <summary>Every entry of <paramref name="family"/> (one of Families).</summary>
    Friend Function Entries(family As String) As IEnumerable(Of Entry)
        Select Case family
            Case "app-sets" : Return AppSets()
            Case "other" : Return Others()
            Case "fo4-gbuffer" : Return GBufferEntries()
            Case "fo4-effect" : Return EffectEntries()
            Case "fo4-b10" : Return LightEntries()
            Case "fo4-b11" : Return CompositeEntries()
            Case "fo4-b07" : Return UtilityEntries()
            Case "fo4-imagespace" : Return ImageSpaceEntries()
            Case "fo4-b06" : Return LightingEntries()
            Case "sse-lighting" : Return SseLightingEntries()
            Case "sse-utility" : Return SseUtilityEntries()
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(family), family, "not a family of the program catalog")
        End Select
    End Function

    ''' <summary>Every entry of every family.</summary>
    Friend Iterator Function AllEntries() As IEnumerable(Of Entry)
        For Each f In Families
            For Each e In Entries(f) : Yield e : Next
        Next
    End Function

    ' ---- the families: every program the library can build (the census), each with Asked - the app's request sites can ask for it ----

    Private Iterator Function AppSets() As IEnumerable(Of Entry)
        For Each s In PreviewControl.ControlSlotSpecs()
            Dim sp = s.Spec
            Yield New Entry("app-sets", s.Name, Function() sp, True)
        Next
        For Each s In SseSaoPrograms.Specs()
            Dim sp = s.Spec
            Yield New Entry("app-sets", s.Name, Function() sp, True)
        Next
        For Each s In Fo4ImageSpaceAppPrograms.Specs()
            Dim sp = s.Spec
            Yield New Entry("app-sets", s.Name, Function() sp, True)
        Next
        For Each t In Fo4DeferredLightLaw.AppLightTechniques.Distinct()
            Dim tl = t
            Yield New Entry("app-sets", $"light technique 0x{t:X8}", Function() Fo4DeferredPrograms.LightSpec(tl), True)
        Next
        For Each t In Fo4DeferredLightLaw.AppCompositeTechniques.Distinct()
            Dim tc = t
            Yield New Entry("app-sets", $"composite technique 0x{t:X8}", Function() Fo4DeferredPrograms.CompositeSpec(tc), True)
        Next
        Yield New Entry("app-sets", "DirectResolve", AddressOf Fo4DeferredPrograms.DirectResolveSpec, True)
        Yield New Entry("app-sets", "LightSum", AddressOf Fo4DeferredPrograms.LightSumSpec, True)
        Yield New Entry("app-sets", "DecalBase", AddressOf Fo4DeferredPrograms.DecalBaseSpec, True)
        Yield New Entry("app-sets", "PrepassRigid", AddressOf Fo4DeferredPrograms.PrepassRigidSpec, True)
        Yield New Entry("app-sets", "PrepassAlpha", AddressOf Fo4DeferredPrograms.PrepassAlphaSpec, True)
    End Function

    Private Iterator Function Others() As IEnumerable(Of Entry)
        Yield New Entry("other", "text overlay", Function() Shader_Base_Class.SpecOf(TextOverlayRenderer.VertexOverlaySrc, TextOverlayRenderer.FragmentOverlaySrc), True)
        Yield New Entry("other", "facetint compositor", AddressOf FaceTintCompositor.CompositorSpec, True)
        Yield New Entry("other", "gl43 offset probe", Function() Shader_Base_Class.SpecOf(Gl43.OffsetProbeVertex, Gl43.OffsetProbeFragment), True)
    End Function

    ''' <summary>g6: every (VS, PS) of the law (SseLightingSource.LawTuples, the closed list's tuples: SetupTechnique 0x141547D20) on its VS's routes. Asked: the
    ''' colour draw (SseEnginePrograms.LightingProgram) asks for the technique's record whatever it is, and the keys are the only thing it composes.</summary>
    Private Iterator Function SseLightingEntries() As IEnumerable(Of Entry)
        For Each tu In SseLightingSource.LawTuples
            Dim k = tu
            For Each cpu In Routes(Function(cr) SseLightingSource.EffectiveCpuLayout(k.Vs, cr))
                Dim c = cpu
                Yield New Entry("sse-lighting", $"SSE lighting (VS 0x{k.Vs:X8}{Ruta(c)}, PS 0x{k.Ps:X8})", Function() SseEnginePrograms.LightingSpec(k.Vs, k.Ps, c), True)
            Next
        Next
    End Function

    ''' <summary>g7: every (VS, PS) of the law (SseUtilitySource.LawTuples) on its VS's routes, and every VS with the null PS (SseUtilityTechnique.PixelShaderNull: its
    ''' VS key keeps no PS). Asked: the z-prepass, the shadow map and the mask (SseEnginePrograms.UtilityProgram) compose only the keys.</summary>
    Private Iterator Function SseUtilityEntries() As IEnumerable(Of Entry)
        For Each kv In SseUtilitySource.VertexStages
            Dim vs = kv.Key
            For Each cpu In Routes(Function(cr) SseUtilitySource.EffectiveCpuLayout(vs, cr))
                Dim c = cpu
                ' Asked: a VS its key selects (fixed point, 0x14158E990..EA00) whose technique takes the null PS (0x141566735..778); the others only the census builds.
                Dim pedido = SseUtilityTechnique.VertexShaderKey(vs) = vs AndAlso SseUtilityTechnique.PixelShaderNull(vs)
                Yield New Entry("sse-utility", $"SSE utility (VS 0x{vs:X8}{Ruta(c)}, null PS)", Function() SseEnginePrograms.UtilitySpec(vs, 0UI, True, c), pedido)
            Next
        Next
        For Each tu In SseUtilitySource.LawTuples
            Dim k = tu
            For Each cpu In Routes(Function(cr) SseUtilitySource.EffectiveCpuLayout(k.Vs, cr))
                Dim c = cpu
                ' The mask PS of another filter than the frame's (SseUtilityTechnique.ShadowFilterType, bits 17..20 of the key, 0x14158EA59..AB2): the law's, the census's, never production's.
                Dim esMascara = (k.Ps And &H1E00000UI) <> 0UI AndAlso (k.Ps And &H2000UI) <> 0UI
                ' PS key 0x2000: what the key function answers exactly when the technique takes the null PS (0x14158EA10..; PixelShaderNull), which looks no PS up: a record production never binds.
                Dim pedido = k.Ps <> &H2000UI AndAlso (Not esMascara OrElse (k.Ps And &H1E0000UI) = (1UI << 19))
                Yield New Entry("sse-utility", $"SSE utility (VS 0x{k.Vs:X8}{Ruta(c)}, PS 0x{k.Ps:X8})", Function() SseEnginePrograms.UtilitySpec(k.Vs, k.Ps, False, c), pedido)
            Next
        Next
    End Function

    ''' <summary>The routes of a VS (the effective ones: the GPU route, and the CPU route when the VS has one).</summary>
    Private Function Routes(cpuLayout As Func(Of Boolean, Fo4CpuRouteLayout)) As Boolean()
        Return If(cpuLayout(True) <> Fo4CpuRouteLayout.None, {False, True}, {False})
    End Function

    Private Function Ruta(cpu As Boolean) As String
        Return If(cpu, " CPU route", "")
    End Function

    ''' <summary>Every G-buffer tuple of the law whose VS is transcribed, on its routes: the record, its variant without early tests (a
    ''' record with forceEarlyDepthStencil), the decals' base surface and the position capture of its VS (an untessellated tuple's).
    ''' Asked (Fo4MaterialData.BuildFo4GBufferPass asks for a technique whose record has no Gap; DecalBaseSurfaceProgram and
    ''' EnginePositionProgram for such an untessellated tuple): the tuple's record without a declared gap; a position capture when an
    ''' asked untessellated tuple has its VS on that route.</summary>
    Private Iterator Function GBufferEntries() As IEnumerable(Of Entry)
        Dim pedido = Function(k As (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger)) Fo4GBufferSource.Records(k.Ps).Gap = ""
        Dim posicionesPedidas As New HashSet(Of (UInteger, Boolean))
        For Each k In GBufferKeys()
            If GateDropsTechnique(k) OrElse Not pedido(k) OrElse Fo4GBufferSource.Tessellates(k.Hs) Then Continue For
            For Each cpu In Routes(Function(cr) Fo4GBufferSource.EffectiveCpuLayout(k.Vs, cr))
                posicionesPedidas.Add((k.Vs, cpu))
            Next
        Next
        Dim posiciones As New HashSet(Of (UInteger, Boolean))
        For Each k In GBufferKeys()
            Dim key = k
            If GateDropsTechnique(key) Then Continue For
            Dim asked = pedido(key)
            Dim untessellated = Not Fo4GBufferSource.Tessellates(key.Hs)
            For Each cpu In Routes(Function(cr) Fo4GBufferSource.EffectiveCpuLayout(key.Vs, cr))
                Dim c = cpu
                Dim nombre = $"G-buffer record (VS 0x{key.Vs:X8}, HS 0x{key.Hs:X8}, DS 0x{key.Ds:X8}, PS {key.Ps:X8}){Ruta(c)}"
                Yield New Entry("fo4-gbuffer", nombre, Function() Fo4DeferredPrograms.GBufferSpec(key, c), asked)
                If Fo4GBufferSource.Records(key.Ps).EarlyZ Then
                    Yield New Entry("fo4-gbuffer", nombre & " without early tests", Function() Fo4DeferredPrograms.GBufferSpec(key, c, earlyTests:=False), asked)
                End If
                If untessellated Then
                    Yield New Entry("fo4-gbuffer", "decal base surface of " & nombre, Function() Fo4DeferredPrograms.DecalSurfaceSpec(key, c), asked)
                    If posiciones.Add((key.Vs, c)) Then
                        Yield New Entry("fo4-gbuffer", $"position capture VS 0x{key.Vs:X8}{Ruta(c)}", Function() Fo4DeferredPrograms.PositionSpec(key.Vs, c),
                                        posicionesPedidas.Contains((key.Vs, c)))
                    End If
                End If
            Next
        Next
    End Function

    ''' <summary>Every effect technique of the law on its routes, with its VS's decals' base surface. Asked (BuildFo4EffectPass, then
    ''' EffectSurfaceProgram for its VS): the engine has its programs (MissingEnginePrograms), it is not vetoed by the UI mask
    ''' (Fo4EffectEngineUndrawn) and its stages declare no gap (GapOf) - all three are of the key's ids (the VS id drops the UI mask bit 27,
    ''' the PS id keeps it), so any technique of the law with the key answers them; a surface when an asked key has its VS on that route.</summary>
    Private Iterator Function EffectEntries() As IEnumerable(Of Entry)
        Dim pedidas As New HashSet(Of (UInteger, UInteger))
        For Each tv In Fo4EffectSource.LawTechniques
            If Fo4EffectSource.MissingEnginePrograms(tv) Is Nothing AndAlso Not Fo4EffectTechnique.UiMaskVetoed(tv) AndAlso Fo4EffectSource.GapOf(tv) = "" Then
                Dim k = New Fo4EffectTech(tv).Key()
                pedidas.Add((k.Vs, k.Ps))
            End If
        Next
        Dim superficiesPedidas As New HashSet(Of (UInteger, Boolean))
        For Each k In EffectKeys()
            If Not pedidas.Contains((k.Vs, k.Ps)) Then Continue For
            For Each cpu In Routes(Function(cr) Fo4EffectSource.EffectiveCpuLayout(k.Vs, cr))
                superficiesPedidas.Add((k.Vs, cpu))
            Next
        Next
        Dim superficies As New HashSet(Of (UInteger, Boolean))
        For Each k In EffectKeys()
            Dim key = k
            Dim asked = pedidas.Contains((key.Vs, key.Ps))
            For Each cpu In Routes(Function(cr) Fo4EffectSource.EffectiveCpuLayout(key.Vs, cr))
                Dim c = cpu
                Yield New Entry("fo4-effect", $"effect (VS 0x{key.Vs:X8}, PS 0x{key.Ps:X8}){Ruta(c)}", Function() Fo4DeferredPrograms.EffectSpec(key, c), asked)
                If superficies.Add((key.Vs, c)) Then
                    Yield New Entry("fo4-effect", $"effect base surface VS 0x{key.Vs:X8}{Ruta(c)}", Function() Fo4DeferredPrograms.EffectSurfaceSpec(key.Vs, c),
                                    superficiesPedidas.Contains((key.Vs, c)))
                End If
            Next
        Next
    End Function

    ''' <summary>b10: VS 0 with every transcribed light PS (a declared gap - Fn empty - is not built). Census only: the frame's lights
    ''' are app-sets' (Fo4DeferredLightLaw.AppLightTechniques).</summary>
    Private Iterator Function LightEntries() As IEnumerable(Of Entry)
        For Each p In Fo4DFLightSource.PixelShaders.Keys.ToList()
            If Fo4DFLightSource.PixelShaders(p).Fn = "" Then Continue For
            Dim ps = p
            Yield New Entry("fo4-b10", $"light PS 0x{ps:X8}", Function() Fo4DeferredPrograms.LightPairSpec(0UI, ps), False)
        Next
    End Function

    ''' <summary>b11: every composite law tuple. Census only: the frame's composites are app-sets' (AppCompositeTechniques).</summary>
    Private Iterator Function CompositeEntries() As IEnumerable(Of Entry)
        For Each t In Fo4DFCompositeSource.LawTuples
            Dim tu = t
            Yield New Entry("fo4-b11", $"composite (VS 0x{tu.Vs:X8}, PS 0x{tu.Ps:X8})", Function() Fo4DeferredPrograms.CompositePairSpec(tu.Vs, tu.Ps), False)
        Next
    End Function

    ''' <summary>b07: every utility VS of the law that is transcribed on its routes with the null fragment, and every (VS, PS) pair of
    ''' the law whose PS is transcribed on the VS's routes. Asked (UtilityProgram: MissingEnginePrograms Nothing, GapOf empty): a law
    ''' tuple whose stages declare no gap - the null fragment's PS gap does not count (GapOf) - and whose PS id draws with the null
    ''' fragment (PixelShaderNull: its bits 13, 14, 7, 16 and 28 are kept by the PS id's mask, so the PS id answers it) for the VS's
    ''' null-fragment program, or does not for the pair's.</summary>
    Private Iterator Function UtilityEntries() As IEnumerable(Of Entry)
        Dim nulosPedidos As New HashSet(Of UInteger), paresPedidos As New HashSet(Of (UInteger, UInteger))
        For Each tu In Fo4UtilitySource.LawTuples
            If Not Fo4UtilitySource.VertexStages.ContainsKey(tu.Vs) OrElse Not Fo4UtilitySource.PixelShaders.ContainsKey(tu.Ps) Then Continue For
            Dim nulo = Fo4UtilitySource.PixelShaderNull(tu.Ps)
            If Fo4UtilitySource.VertexStages(tu.Vs).Gap <> "" OrElse (Not nulo AndAlso Fo4UtilitySource.PixelShaders(tu.Ps).Gap <> "") Then Continue For
            If nulo Then nulosPedidos.Add(tu.Vs) Else paresPedidos.Add((tu.Vs, tu.Ps))
        Next
        Dim adm = UtilityAdmission.Value
        For Each v In adm.Vs
            Dim vs = v
            For Each cpu In Routes(Function(cr) Fo4UtilitySource.EffectiveCpuLayout(vs, cr))
                Dim c = cpu
                Yield New Entry("fo4-b07", $"utility (VS 0x{vs:X8}{Ruta(c)}, null PS)", Function() Fo4DeferredPrograms.UtilitySpec(vs, 0UI, True, c), nulosPedidos.Contains(vs))
            Next
        Next
        For Each p In adm.Pairs
            Dim pr = p
            For Each cpu In Routes(Function(cr) Fo4UtilitySource.EffectiveCpuLayout(pr.Vs, cr))
                Dim c = cpu
                Yield New Entry("fo4-b07", $"utility (VS 0x{pr.Vs:X8}{Ruta(c)}, PS 0x{pr.Ps:X8})", Function() Fo4DeferredPrograms.UtilitySpec(pr.Vs, pr.Ps, False, c),
                                paresPedidos.Contains((pr.Vs, pr.Ps)))
            Next
        Next
    End Function

    ''' <summary>The image-space records the post asks for: its chain's (Fo4ImageSpaceChain.Records) and the camera Z
    ''' (Fo4DeferredTargets.RecCameraZ).</summary>
    Friend Function PostImageSpaceRecords() As IEnumerable(Of UInteger)
        Return Fo4ImageSpaceChain.Records.Append(Fo4DeferredTargets.RecCameraZ).Distinct()
    End Function

    ''' <summary>Image space: every VS / PS pair and every CS whose stages are transcribed. Asked: a record the post asks for
    ''' (PostImageSpaceRecords) in the program Fo4DeferredPrograms.ImageSpaceSpec builds for it (the CS, or the first pair with that PS).</summary>
    Private Iterator Function ImageSpaceEntries() As IEnumerable(Of Entry)
        Dim pedidos As New HashSet(Of UInteger)(PostImageSpaceRecords())
        Dim primero As New HashSet(Of UInteger)
        For Each p In Fo4ImageSpaceSource.Programs
            Dim pr = p
            If pr.Cs <> 0UI Then
                If Fo4ImageSpaceSource.ComputeStages(pr.Cs).Fn = "" Then Continue For
                Yield New Entry("fo4-imagespace", $"image-space CS {pr.Cs} {pr.Name}", Function() Shader_Base_Class.ComputeSpecOf(Fo4ImageSpaceSource.ComputeOf(pr.Cs)),
                                pedidos.Contains(pr.Cs))
            Else
                Dim esElPrimero = primero.Add(pr.Ps)
                If Fo4ImageSpaceSource.VertexStages(pr.Vs).Fn <> "" AndAlso Fo4ImageSpaceSource.PixelShaders(pr.Ps).Fn <> "" Then
                    Yield New Entry("fo4-imagespace", $"image-space {pr.Vs}/{pr.Ps} {pr.Name}", Function() Fo4DeferredPrograms.ImageSpacePairSpec(pr.Vs, pr.Ps),
                                    esElPrimero AndAlso pedidos.Contains(pr.Ps))
                End If
            End If
        Next
    End Function

    ''' <summary>b06: every lighting VS on its routes with the null fragment, and every law tuple (VS, PS) on the VS's routes with its PS.
    ''' Census only: no request site of the app draws b06 yet.</summary>
    Private Iterator Function LightingEntries() As IEnumerable(Of Entry)
        For Each v In Fo4LightingSource.VertexStages.Keys.ToList()
            Dim vs = v
            For Each cpu In {False, True}
                If cpu AndAlso Fo4LightingSource.VertexStages(vs).CpuLayout = Fo4CpuRouteLayout.None Then Continue For
                Dim c = cpu
                Yield New Entry("fo4-b06", $"lighting VS 0x{vs:X8}{Ruta(c)}", Function() Shader_Base_Class.SpecOf(Fo4LightingSource.VertexOf(vs, c), Fo4GBufferSource.PrepassFragmentNone), False)
            Next
        Next
        For Each t In Fo4LightingSource.LawTuples
            Dim tu = t
            For Each cpu In {False, True}
                If cpu AndAlso Fo4LightingSource.VertexStages(tu.Vs).CpuLayout = Fo4CpuRouteLayout.None Then Continue For
                Dim c = cpu
                Yield New Entry("fo4-b06", $"lighting (VS 0x{tu.Vs:X8}{Ruta(c)}, PS 0x{tu.Ps:X8})", Function() Shader_Base_Class.SpecOf(Fo4LightingSource.VertexOf(tu.Vs, c), Fo4LightingSource.FragmentOf(tu.Ps)), False)
            Next
        Next
    End Function

    ' ---- the families' keys, computed from the closed lists (no text composed) ----

    ''' <summary>The G-buffer tuples of the law whose VS is transcribed (Fo4GBufferSource.VertexStages.NotTranscribed: a declared gap), in
    ''' the law's order.</summary>
    Private ReadOnly GBufferAdmission As New Lazy(Of List(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger)))(
        Function()
            Dim r As New List(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger))
            Dim seen As New HashSet(Of (UInteger, UInteger, UInteger, UInteger))
            For Each tv In Fo4GBufferSource.LawTechniques
                Dim k = New Fo4Technique(tv).Key()
                If Fo4GBufferSource.VertexStages(k.Vs).NotTranscribed <> "" Then Continue For
                If seen.Add((k.Vs, k.Hs, k.Ds, k.Ps)) Then r.Add(k)
            Next
            Return r
        End Function, LazyThreadSafetyMode.ExecutionAndPublication)

    ''' <summary>The effect techniques of the law (their (VS, PS) keys), in the law's order.</summary>
    Private ReadOnly EffectAdmission As New Lazy(Of List(Of (Vs As UInteger, Ps As UInteger)))(
        Function() Fo4EffectSource.LawTechniques.Select(Function(tv) New Fo4EffectTech(tv).Key()).Distinct().ToList(),
        LazyThreadSafetyMode.ExecutionAndPublication)

    ''' <summary>The utility law's transcribed VS (with the null fragment) and its (VS, PS) pairs whose PS is transcribed.</summary>
    Private ReadOnly UtilityAdmission As New Lazy(Of (Vs As List(Of UInteger), Pairs As List(Of (Vs As UInteger, Ps As UInteger))))(
        Function()
            Dim vsl As New List(Of UInteger), pairs As New List(Of (Vs As UInteger, Ps As UInteger))
            For Each tu In Fo4UtilitySource.LawTuples
                If Fo4UtilitySource.VertexStages(tu.Vs).Fn = "" Then Continue For
                If Not vsl.Contains(tu.Vs) Then vsl.Add(tu.Vs)
                If Fo4UtilitySource.PixelShaders(tu.Ps).Fn <> "" AndAlso Not pairs.Contains((tu.Vs, tu.Ps)) Then pairs.Add((tu.Vs, tu.Ps))
            Next
            Return (vsl, pairs)
        End Function, LazyThreadSafetyMode.ExecutionAndPublication)

    Private Function GBufferKeys() As List(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger))
        Return GBufferAdmission.Value
    End Function

    Private Function EffectKeys() As List(Of (Vs As UInteger, Ps As UInteger))
        Return EffectAdmission.Value
    End Function

    ''' <summary>DEBUG ONLY - GATE (rev-08 -&gt; (b), mutant catalog-drops-technique): the catalog without the law's first G-buffer tuple
    ''' (its record, variant, surface; ParityGate program-catalog-domain: the domain has it, the catalog does not).</summary>
    Private Function GateDropsTechnique(key As (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger)) As Boolean
#If DEBUG Then
        Return Fo4GBufferConstants.GateMutantOn("catalog-drops-technique") AndAlso key.Equals(GBufferKeys()(0))
#Else
        Return False
#End If
    End Function

    ' ---- the manifest (r3 rev-17): the catalog's digests next to the library, written by Tools\ProgramCatalogTool at build time ----

    ''' <summary>The manifest's file name, next to FO4_Base_Library.dll in each output (ProgramCatalog.targets).</summary>
    Friend Const ManifestFileName As String = "ProgramCatalog.keys"
    ''' <summary>"FPCK" read as a little-endian Int32: the first four bytes of a manifest.</summary>
    Private Const ManifestMagic As Integer = &H4B435046
    ''' <summary>The manifest's layout: Magic, ManifestFormat, the library's MVID (16 bytes), the count, the digests (32 bytes each,
    ''' ordinal order of their hex).</summary>
    Friend Const ManifestFormat As Integer = 1

    ''' <summary>The MVID of this library (the one the manifest is written for and read against).</summary>
    Friend Function LibraryMvid() As Guid
        Return GetType(ProgramCompiler).Assembly.ManifestModule.ModuleVersionId
    End Function

    ''' <summary>The digest of every program of the catalog - the asked entries, the domain the app can ask for (hex, distinct, ordinal
    ''' order): the specs composed, nothing built.</summary>
    Friend Function AllDigests() As List(Of String)
        Dim r As New HashSet(Of String)(StringComparer.Ordinal)
        For Each e In AllEntries()
            If e.Asked Then r.Add(Convert.ToHexString(ProgramCompiler.ProgramDigest(e.Spec())))
        Next
        Dim l = r.ToList()
        l.Sort(StringComparer.Ordinal)
        Return l
    End Function

    ''' <summary>The census-only programs: the entries the app never asks for whose program is not also one of the catalog's (ShadowGate
    ''' registers them at its start, Register: its census and its gates build them; the app never does).</summary>
    Friend Function CensusOnlySpecs() As List(Of ProgramSpec)
        Dim catalogo As New HashSet(Of String)(AllDigests(), StringComparer.Ordinal)
        Dim r As New List(Of ProgramSpec)
        For Each e In AllEntries()
            If e.Asked Then Continue For
            Dim sp = e.Spec()
            If Not catalogo.Contains(Convert.ToHexString(ProgramCompiler.ProgramDigest(sp))) Then r.Add(sp)
        Next
        Return r
    End Function

    ''' <summary>Writes the manifest of this library into <paramref name="folder"/> (Tools\ProgramCatalogTool, after the build: the
    ''' library it loads is the one of that folder). Returns how many digests it wrote.</summary>
    Friend Function WriteManifest(folder As String) As Integer
        Dim digests = AllDigests()
        Dim final = Path.Combine(folder, ManifestFileName)
        Dim pending = final & "." & Guid.NewGuid().ToString("N") & ".pending"
        Using fs As New FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None),
              w As New BinaryWriter(fs)
            w.Write(ManifestMagic)
            w.Write(ManifestFormat)
            w.Write(LibraryMvid().ToByteArray())
            w.Write(digests.Count)
            For Each d In digests : w.Write(Convert.FromHexString(d)) : Next
        End Using
        File.Move(pending, final, overwrite:=True)
        Return digests.Count
    End Function

    ''' <summary>Reads the manifest at <paramref name="file"/>: its MVID and digests (hex); Nothing when it is absent or not a whole
    ''' manifest of this ManifestFormat.</summary>
    Friend Function ReadManifest(file As String) As (Mvid As Guid, Digests As HashSet(Of String))?
        Try
            If Not IO.File.Exists(file) Then Return Nothing
            Using fs As New FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read),
                  r As New BinaryReader(fs)
                If fs.Length < 28 OrElse r.ReadInt32() <> ManifestMagic OrElse r.ReadInt32() <> ManifestFormat Then Return Nothing
                Dim mvid As New Guid(r.ReadBytes(16))
                Dim n = r.ReadInt32()
                If n < 0 OrElse fs.Length - fs.Position <> CLng(n) * 32 Then Return Nothing
                Dim d As New HashSet(Of String)(StringComparer.Ordinal)
                For i = 1 To n : d.Add(Convert.ToHexString(r.ReadBytes(32))) : Next
                Return (mvid, d)
            End Using
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Return Nothing
        End Try
    End Function

    ''' <summary>Where this library reads its manifest: next to its own DLL (a gate's file in Debug, GateManifestPath).</summary>
    Friend Function InstalledManifestPath() As String
#If DEBUG Then
        If GateManifestPath IsNot Nothing Then Return GateManifestPath
#End If
        Return Path.Combine(Path.GetDirectoryName(GetType(ProgramCompiler).Assembly.Location), ManifestFileName)
    End Function

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-cache, the cleanup by text): the manifest read instead of the installed one (Nothing = the
    ''' installed one); GateReloadManifest makes the next read take it.</summary>
    Friend GateManifestPath As String = Nothing
    ''' <summary>GATE ONLY: the manifest is read again at its next use (after GateManifestPath changed).</summary>
    Friend Sub GateReloadManifest()
        _installed = NuevaLectura()
    End Sub
#End If

    ''' <summary>The installed manifest's digests; Nothing when it is absent, unreadable or of another build of the library (its MVID is
    ''' not this library's: treated as absent, r3 rev-17 4 - runtime state, so in Release too). Read once per process.</summary>
    Friend Function InstalledDigests() As HashSet(Of String)
        Return _installed.Value
    End Function

    Private _installed As Lazy(Of HashSet(Of String)) = NuevaLectura()

    ''' <summary>The once-per-process read of the installed manifest (see InstalledDigests).</summary>
    Private Function NuevaLectura() As Lazy(Of HashSet(Of String))
        Return New Lazy(Of HashSet(Of String))(
        Function()
            Dim p = InstalledManifestPath()
            Dim m = ReadManifest(p)
            If Not m.HasValue Then
                Logger.LogLazy(Function() $"[PROGRAM-CATALOG] no manifest at {p}: the program cache deletes nothing by text")
                Return Nothing
            End If
            Dim stale = m.Value.Mvid <> LibraryMvid()
#If DEBUG Then
            ' GATE (ShadowGate --program-cache, subject mutant): a manifest of another build of the library taken as this one's.
            If Fo4GBufferConstants.GateMutantOn("stale-manifest-accepted") Then stale = False
#End If
            If stale Then
                Logger.LogLazy(Function() $"[PROGRAM-CATALOG] the manifest at {p} is of another build of the library (MVID {m.Value.Mvid}, this one {LibraryMvid()}): treated as absent")
                Return Nothing
            End If
            Return m.Value.Digests
        End Function, LazyThreadSafetyMode.ExecutionAndPublication)
    End Function

    ' ---- the host's own programs (r2 rev-08: a Tools host registers its specs; its cache keeps them, the counter does not count them) ----

    Private ReadOnly _registered As New ConcurrentDictionary(Of String, Byte)(StringComparer.Ordinal)

    ''' <summary>A host's programs outside the library's catalog (the Tools' own builds): the cleanup of this process keeps their
    ''' entries (register before the first program is built: the cleanup runs once) and the Debug counter does not count them.</summary>
    Friend Sub Register(specs As IEnumerable(Of ProgramSpec))
        For Each s In specs
            _registered.TryAdd(Convert.ToHexString(ProgramCompiler.ProgramDigest(s)), 0)
        Next
    End Sub

    ''' <summary>The digests a host registered (Register).</summary>
    Friend Function RegisteredDigests() As ICollection(Of String)
        Return _registered.Keys
    End Function

    ''' <summary>The cache keys the cleanup keeps for identity <paramref name="id"/>: the installed manifest's and the registered digests'
    ''' (ProgramCompiler.ProgramKey); Nothing when there is no usable manifest (nothing is deleted by text).</summary>
    Friend Function KeptKeys(id As ProgramBinaryCache.CacheIdentity) As HashSet(Of String)
        Dim installed = InstalledDigests()
        If installed Is Nothing Then Return Nothing
        Dim r As New HashSet(Of String)(StringComparer.Ordinal)
        For Each d In installed.Concat(RegisteredDigests())
            r.Add(Convert.ToHexString(ProgramCompiler.ProgramKey(id, Convert.FromHexString(d))))
        Next
        Return r
    End Function

#If DEBUG Then
    ''' <summary>DEBUG ONLY (r3 rev-08, r4 (2)): the programs Build built whose digest is not in the installed manifest, not registered,
    ''' and with no stage a harness declared (Gl43.GateHarnessTexts) - the battery requires 0; with the name each was built under.</summary>
    Friend GateOutsideManifest As Integer
    Friend ReadOnly GateOutsideManifestLog As New ConcurrentQueue(Of String)
    ''' <summary>DEBUG ONLY: the programs Build built while no usable manifest was installed (the counter could not judge them).</summary>
    Friend GateBuiltWithoutManifest As Integer

    ''' <summary>DEBUG ONLY: ProgramCompiler.Build built the program of <paramref name="digest"/> from stages whose texts hash to
    ''' <paramref name="hashes"/>, as <paramref name="name"/> (the slot, or Nothing).</summary>
    Friend Sub NoteBuilt(digest As Byte(), hashes As Byte()(), name As String)
        Dim installed = InstalledDigests()
        If installed Is Nothing Then
            Interlocked.Increment(GateBuiltWithoutManifest)
            Return
        End If
        Dim hex = Convert.ToHexString(digest)
        If installed.Contains(hex) OrElse _registered.ContainsKey(hex) Then Return
        ' GATE (subject mutant harness-text-counted): the harness's declared texts counted too.
        If Not Fo4GBufferConstants.GateMutantOn("harness-text-counted") AndAlso
           hashes.Any(Function(h) Gl43.GateHarnessTexts.ContainsKey(Convert.ToHexString(h))) Then Return
        Interlocked.Increment(GateOutsideManifest)
        Dim line = $"{If(name, "(no slot)")}: digest {hex} built at {String.Join(" < ", New Diagnostics.StackTrace(1, False).GetFrames().Select(Function(f) f.GetMethod()?.Name).Where(Function(x) x IsNot Nothing).Take(14))}"
        GateOutsideManifestLog.Enqueue(line)
        Logger.LogLazy(Function() "[PROGRAM-CATALOG] built outside the manifest: " & line)
    End Sub
#End If

End Module
