Imports System.Text

''' <summary>
''' The skeleton NODE NAMES RaceMenu (skee64) offers as body-scale / node-transform sliders — reconstructed the
''' same way RaceMenu itself builds that list: by reading the installed Papyrus scripts and collecting the node
''' arguments each one passes to <c>NiOverride.Add/SetNodeTransform{Scale,Position,Rotation}</c>. RaceMenu has NO
''' skeleton scan and NO fixed catalog in C++ (PapyrusNiOverride.cpp:1381 enumerates only nodes that ALREADY carry
''' an override); the slider list the user sees is exactly the UNION of what <c>RaceMenuPlugin.psc</c>, XPMSE and any
''' other mod register through that API. This is the node-transform analogue of <see cref="RaceMenuPaintCatalog"/>.
'''
''' On the reference install this recovers RaceMenuPlugin's built-in body/weapon nodes (NPC …/Weapon…); an XPMSE
''' install adds its <c>CME …</c> offset-node set through the identical mechanism.
'''
''' SKYRIM ONLY (RaceMenu is a Skyrim mod). Built once after the file dictionary is ready (reads
''' <c>scripts\*.pex</c> through <see cref="FilesDictionary_class"/>, loose and inside BSAs). We parse <c>.pex</c>
''' (the compiled artifact the game loads), not <c>.psc</c> — see <see cref="PapyrusPexParser"/>.
''' </summary>
Public NotInheritable Class RaceMenuNodeCatalog

    ''' <summary>Distinct node names registered across all scanned scripts (raw skeleton node strings). The caller
    ''' intersects these with the actor's real rig, so a stray non-bone string (e.g. a keyName that slipped through)
    ''' is harmless. Sorted for a stable list.</summary>
    Private ReadOnly _nodes As New List(Of String)
    Private ReadOnly _seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _scriptsScanned As New List(Of String)

    ''' <summary>The process-wide instance, populated once for a Skyrim session (Nothing on FO4).</summary>
    Public Shared Property Current As RaceMenuNodeCatalog

    Public Function Nodes() As IReadOnlyList(Of String)
        Return _nodes
    End Function

    Public Function Count() As Integer
        Return _nodes.Count
    End Function

    ''' <summary>Scan every <c>scripts\*.pex</c> in the load order (loose + BSA), parse each, and accumulate the
    ''' union of node-transform node names. Safe to call once; malformed scripts are skipped.</summary>
    Public Sub Load()
        Dim keys As List(Of String)
        Try
            keys = FilesDictionary_class.Dictionary.Keys.
                Where(Function(k) k.EndsWith(".pex", StringComparison.OrdinalIgnoreCase)).ToList()
        Catch ex As Exception
            Logger.LogLazy(Function() $"[NODE-CATALOG] script enumeration failed: {ex.GetType().Name}: {ex.Message}")
            Return
        End Try
        If keys Is Nothing Then Return

        For Each key In keys
            ' Cheap gate: only decode scripts that even mention the node-transform API.
            Dim bytes As Byte()
            Try
                bytes = FilesDictionary_class.GetBytes(key)
            Catch
                Continue For
            End Try
            If bytes Is Nothing OrElse Not MentionsNodeTransform(bytes) Then Continue For

            Dim names = PapyrusPexParser.ExtractNodeTransformNodeNames(bytes)
            If names Is Nothing OrElse names.Count = 0 Then Continue For
            _scriptsScanned.Add(key)
            For Each n In names
                Dim node = If(n, "").Trim()
                If node.Length = 0 Then Continue For
                ' Drop obvious non-node strings: the transform keyName ("RSMTransform"/"RaceMenu") and computed temps.
                If node.StartsWith("::") OrElse node.Equals("RSMTransform", StringComparison.OrdinalIgnoreCase) OrElse
                   node.Equals("RaceMenu", StringComparison.OrdinalIgnoreCase) Then Continue For
                If _seen.Add(node) Then _nodes.Add(node)
            Next
        Next
        _nodes.Sort(StringComparer.OrdinalIgnoreCase)
        Logger.LogLazy(Function() $"[NODE-CATALOG] scripts={_scriptsScanned.Count} nodes={_nodes.Count}")
    End Sub

    ''' <summary>ASCII byte-scan pre-filter: does the raw script even contain a node-transform method name?</summary>
    Private Shared ReadOnly Marker As Byte() = Encoding.ASCII.GetBytes("NodeTransform")

    Private Shared Function MentionsNodeTransform(bytes As Byte()) As Boolean
        Dim needle = Marker
        Dim last = bytes.Length - needle.Length
        For i = 0 To last
            Dim ok = True
            For j = 0 To needle.Length - 1
                If bytes(i + j) <> needle(j) Then ok = False : Exit For
            Next
            If ok Then Return True
        Next
        Return False
    End Function

End Class
