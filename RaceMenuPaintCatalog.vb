Imports System.Text

''' <summary>
''' The named paint lists RaceMenu (skee64) presents — warpaint (face tint masks) and body/hand/feet/face paint
''' (NiOverride overlays) — reconstructed exactly the way RaceMenu builds them: by reading the Papyrus scripts of
''' every installed mod and collecting the <c>Add{Warpaint,BodyPaint,HandPaint,FeetPaint,FacePaint}[Ex](name, path)</c>
''' registrations. There is NO static catalog and NO file browser in RaceMenu; the list the user sees is the UNION
''' of what every mod registers in its <c>On*PaintRequest</c> handler (RaceMenuBase.psc, inside RaceMenu.bsa).
'''
''' On the reference install this yields (measured): warpaints from RaceMenu.bsa (124) + FMS (87); overlays from
''' CommunityOverlays (56 body / 29 hand / 27 feet / 25 face) — all shipped inside BSAs, none loose.
'''
''' SKYRIM ONLY. RaceMenu is a Skyrim mod; Fallout 4 (LooksMenu/f4ee) has a different, catalog-based mechanism, so
''' this stays empty on FO4 and the FO4 editors never consult it. Built once after the file dictionary is ready
''' (it reads <c>scripts\*.pex</c> through <see cref="FilesDictionary_class"/>, loose and inside BSAs).
'''
''' We parse <c>.pex</c> (the compiled artifact the game loads), not <c>.psc</c> — see <see cref="PapyrusPexParser"/>.
''' </summary>
Public NotInheritable Class RaceMenuPaintCatalog

    Public Enum PaintCategory
        Warpaint = 0   ' face TINT MASK (red channel) — RSM_AddWarpaints
        Body = 1       ' Body [Ovl{n}] overlay      — RSM_AddBodyPaints
        Hands = 2      ' Hands [Ovl{n}] overlay      — RSM_AddHandPaints
        Feet = 3       ' Feet [Ovl{n}] overlay       — RSM_AddFeetPaints
        Face = 4       ' Face [Ovl{n}] overlay       — RSM_AddFacePaints
    End Enum

    ''' <summary>One offered entry: what the user sees (<see cref="DisplayName"/>) and what gets stored
    ''' (<see cref="Path"/>, and the 8 texture-set <see cref="Slots"/> for an <c>*Ex</c> registration).</summary>
    Public Structure Entry
        Public Name As String          ' as registered (may be a "$translation_key")
        Public DisplayName As String   ' leading '$' stripped for the list
        Public Path As String          ' game-relative texture path (slot 0)
        Public Slots As String()       ' *Ex 8-slot texture set, else Nothing
        Public SourceScript As String  ' the .pex it came from (for diagnostics)
    End Structure

    Private ReadOnly _byCat As New Dictionary(Of PaintCategory, List(Of Entry))
    Private ReadOnly _scriptsScanned As New List(Of String)

    ''' <summary>The process-wide instance, populated once for a Skyrim session (Nothing on FO4).</summary>
    Public Shared Property Current As RaceMenuPaintCatalog

    Public Sub New()
        For Each cat As PaintCategory In [Enum].GetValues(GetType(PaintCategory))
            _byCat(cat) = New List(Of Entry)()
        Next
    End Sub

    ''' <summary>Entries for a category, sorted by display name. Never Nothing.</summary>
    Public Function Entries(cat As PaintCategory) As IReadOnlyList(Of Entry)
        Dim lst As List(Of Entry) = Nothing
        If _byCat.TryGetValue(cat, lst) Then Return lst
        Return New List(Of Entry)()
    End Function

    Public Function CountFor(cat As PaintCategory) As Integer
        Return Entries(cat).Count
    End Function

    Public Function HasAny() As Boolean
        For Each kv In _byCat
            If kv.Value.Count > 0 Then Return True
        Next
        Return False
    End Function

    ''' <summary>Scripts that were parsed (for logging).</summary>
    Public ReadOnly Property ScriptsScanned As IReadOnlyList(Of String)
        Get
            Return _scriptsScanned
        End Get
    End Property

    ''' <summary>Scan every <c>scripts\*.pex</c> in the load order (loose + BSA), parse each, and accumulate the
    ''' union of paint registrations per category. Safe to call once; malformed scripts are skipped.</summary>
    Public Sub Load()
        ' Every .pex in the load order (loose + BSA). Enumerated straight from the dictionary rather than
        ' GetFilteredKeys(rootPrefix,…): scripts sit directly in "scripts\" (no subdirectory), and the
        ' directory-prefix index only matches nested paths, so a root-prefix query would miss them. Papyrus scripts
        ' only ever live under scripts\, so this is exactly the script set.
        Dim keys As List(Of String)
        Try
            keys = FilesDictionary_class.Dictionary.Keys.
                Where(Function(k) k.EndsWith(".pex", StringComparison.OrdinalIgnoreCase)).ToList()
        Catch ex As Exception
            Logger.LogLazy(Function() $"[PAINT-CATALOG] script enumeration failed: {ex.GetType().Name}: {ex.Message}")
            Return
        End Try
        If keys Is Nothing Then Return

        ' Dedup within a category by (displayName|path) — a mod may register a pair once, and two mods may ship the
        ' same texture; RaceMenu would show one row per distinct (name,path).
        Dim seen As New Dictionary(Of PaintCategory, HashSet(Of String))
        For Each cat As PaintCategory In [Enum].GetValues(GetType(PaintCategory))
            seen(cat) = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Next

        For Each key In keys
            ' Only scripts that even mention a paint function are worth decoding; the byte check is far cheaper
            ' than a full parse and skips the thousands of unrelated scripts a heavy load order carries.
            Dim bytes As Byte()
            Try
                bytes = FilesDictionary_class.GetBytes(key)
            Catch
                Continue For
            End Try
            If bytes Is Nothing OrElse Not MentionsPaint(bytes) Then Continue For

            Dim regs = PapyrusPexParser.ExtractPaints(bytes)
            If regs Is Nothing OrElse regs.Count = 0 Then Continue For
            _scriptsScanned.Add(key)

            For Each r In regs
                Dim cat As PaintCategory
                If Not TryMapCategory(r.Category, cat) Then Continue For
                Dim path = If(r.Path, "").Trim()
                ' Drop computed args (identifiers like "::temp107" — e.g. RaceMenu's "Default" placeholders) and the
                ' Ex "ignore" sentinel; these are not selectable textures.
                If path.Length = 0 OrElse path.StartsWith("::") OrElse path.Equals("ignore", StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim disp = If(r.Name, "").Trim()
                If disp.StartsWith("$") Then disp = disp.Substring(1)
                If disp.Length = 0 Then disp = path
                Dim dedupKey = disp.ToLowerInvariant() & "|" & path.ToLowerInvariant()
                If Not seen(cat).Add(dedupKey) Then Continue For
                _byCat(cat).Add(New Entry With {
                    .Name = r.Name,
                    .DisplayName = disp,
                    .Path = path,
                    .Slots = r.Slots,
                    .SourceScript = key})
            Next
        Next

        For Each cat As PaintCategory In [Enum].GetValues(GetType(PaintCategory))
            _byCat(cat).Sort(Function(a, b) String.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase))
        Next

        Logger.LogLazy(Function() $"[PAINT-CATALOG] scripts={_scriptsScanned.Count} warpaint={CountFor(PaintCategory.Warpaint)} body={CountFor(PaintCategory.Body)} hand={CountFor(PaintCategory.Hands)} feet={CountFor(PaintCategory.Feet)} face={CountFor(PaintCategory.Face)}")
    End Sub

    ''' <summary>Cheap gate: does the raw script even contain a paint-registration method name? (The names live in
    ''' the .pex string table as ASCII, so a byte scan is a sound pre-filter before the full parse.)</summary>
    Private Shared ReadOnly PaintMarkers As Byte()() = {
        Encoding.ASCII.GetBytes("AddWarpaint"), Encoding.ASCII.GetBytes("AddBodyPaint"),
        Encoding.ASCII.GetBytes("AddHandPaint"), Encoding.ASCII.GetBytes("AddFeetPaint"),
        Encoding.ASCII.GetBytes("AddFacePaint")}

    Private Shared Function MentionsPaint(bytes As Byte()) As Boolean
        For Each marker In PaintMarkers
            If IndexOf(bytes, marker) >= 0 Then Return True
        Next
        Return False
    End Function

    Private Shared Function IndexOf(haystack As Byte(), needle As Byte()) As Integer
        Dim last = haystack.Length - needle.Length
        For i = 0 To last
            Dim ok = True
            For j = 0 To needle.Length - 1
                If haystack(i + j) <> needle(j) Then ok = False : Exit For
            Next
            If ok Then Return i
        Next
        Return -1
    End Function

    Private Shared Function TryMapCategory(s As String, ByRef cat As PaintCategory) As Boolean
        Select Case If(s, "").ToLowerInvariant()
            Case "warpaint" : cat = PaintCategory.Warpaint : Return True
            Case "body" : cat = PaintCategory.Body : Return True
            Case "hand" : cat = PaintCategory.Hands : Return True
            Case "feet" : cat = PaintCategory.Feet : Return True
            Case "face" : cat = PaintCategory.Face : Return True
        End Select
        Return False
    End Function

End Class
