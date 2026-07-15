Imports System.Text

''' <summary>Faithful port of RaceMenu/skee64's EXTENDED face-slider catalog loader (FaceMorphInterface.cpp
''' ReadRaces/ReadSliders/CreateSliderList). This is the RaceMenu "on top" layer — the sliders a mod adds via
''' config files, distinct from the vanilla native-chargen sliders that write to NPC.faceMorph (NAM9/NAMA).
'''
''' Source (SKSE64Plugins skee64):
'''   • LoadMods (FaceMorphInterface.cpp:517-534): for each installed mod, read
'''     "Meshes\actors\character\FaceGenMorphs\&lt;modName&gt;\races.ini". SLIDER_MOD_DIRECTORY = FaceMorphInterface.h:28.
'''   • ReadRaces (:762-830): each line "raceEditorId = file1, file2, ...". A file prefixed ':' is loaded from the
'''     shared FaceGenMorphs root instead of the mod subfolder (pathOverride = "").
'''   • ReadSliders (:844-985): "[Male]"/"[Female]" sections; "sliderName = category, type, [params]".
'''       - Slider   : category, "Slider",   lowerBoundMorph, upperBoundMorph   ("None" → empty). Gated by g_extendedMorphs.
'''       - Preset   : category, "Preset",   morphPrefix,     presetCount.       Gated by g_extendedMorphs.
'''       - HeadPart : category, "HeadPart", headPartType(count).
'''   • Categories (FaceMorphInterface.h:73-81): Expressions=1024, Extra=512, Body=4, Head=8, Face=16, Eyes=32,
'''     Brow=64, Mouth=128, Hair=256. Types: Slider=0, Preset=1, HeadPart=2.
'''
''' The per-actor VALUE of a slider lives in the NiOverride ValueSet keyed by the SLIDER NAME (LoadSliders:1315),
''' and is what the .jslot "custom" array stores (PresetInterface.cpp:444-456). Application (ApplyMorphs:1229-1247):
''' value V &lt; 0 ⇒ apply lowerBound morph at |V|; V &gt; 0 ⇒ apply upperBound morph at V; Preset ⇒ morph
''' (lowerBound &amp; int(V)) at 1.0 — all by NAME against the head TRI (TRIFile::Apply:216).</summary>
Public Class RaceMenuSliderCatalog

    Public Const FaceGenMorphsDir As String = "actors\character\FaceGenMorphs"

    Public Enum SliderType
        Slider = 0
        Preset = 1
        HeadPart = 2
    End Enum

    ''' <summary>skee64 category bitflags (FaceMorphInterface.h:73-81). One category per slider.</summary>
    Public Enum SliderCategory
        Body = 4
        Head = 8
        Face = 16
        Eyes = 32
        Brow = 64
        Mouth = 128
        Hair = 256
        Extra = 512
        Expressions = 1024
    End Enum

    ''' <summary>One extended slider (a line in a .slider file). <see cref="Name"/> is the ValueSet key (and the
    ''' .jslot custom morph name). LowerBound/UpperBound are TRI morph names (Slider), or the morph-name prefix
    ''' (Preset). PresetCount = number of presets (Preset) / head-part type (HeadPart).</summary>
    Public Class SliderDef
        Public Property Name As String
        Public Property Category As Integer
        Public Property Type As SliderType
        Public Property LowerBound As String = ""
        Public Property UpperBound As String = ""
        Public Property PresetCount As Integer
    End Class

    ' Per-race, per-gender slider storage (race EditorID → slider name → def), mirroring SliderMap's
    ' [Male]/[Female] sections. Sliders accumulate across all mods (each races.ini contributes).
    Private ReadOnly _maleByRace As New Dictionary(Of String, Dictionary(Of String, SliderDef))(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _femaleByRace As New Dictionary(Of String, Dictionary(Of String, SliderDef))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>skee64's MorphMap (<c>morphs.ini</c>): base head-part <c>.tri</c> → the EXTENDED <c>.tri</c> files
    ''' that carry the extra morphs for it. A slider's LowerBound/UpperBound is a morph NAME; the geometry for
    ''' that name lives in one of these files, NOT in the head's chargen tri. skee64 applies it in
    ''' <c>MorphVisitor::Accept</c> (SKEEHooks.cpp:687-696): for each mapped file, load it and
    ''' <c>triFile->Apply(geometry, morphName, relative)</c>. Keys are stored as skee64 writes them (the base tri
    ''' model name); values are relative to <see cref="SliderMorphsDir"/>.</summary>
    Private ReadOnly _morphMap As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>SLIDER_DIRECTORY (FaceMorphInterface.h:29) — root of the extended morph .tri files.</summary>
    Public Const SliderMorphsDir As String = "actors\character\FaceGenMorphs\morphs"

    ''' <summary>The extended morph <c>.tri</c> files registered for a base head-part tri, as MESHES-relative
    ''' paths ready for FilesDictionary. Empty when the base tri has no extended morphs. Lookup is by file NAME
    ''' as well as full path, because <c>morphs.ini</c> keys are bare names (e.g. <c>femalehead.tri</c>) while a
    ''' shape's tri is a full path.</summary>
    Public Function GetExtendedMorphTris(baseTriPath As String) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrEmpty(baseTriPath) OrElse _morphMap.Count = 0 Then Return result
        Dim files As List(Of String) = Nothing
        If Not _morphMap.TryGetValue(baseTriPath, files) Then
            Dim bare = IO.Path.GetFileName(baseTriPath)
            If String.IsNullOrEmpty(bare) OrElse Not _morphMap.TryGetValue(bare, files) Then Return result
        End If
        For Each f In files
            result.Add($"meshes\{SliderMorphsDir}\{f}")
        Next
        Return result
    End Function

    ''' <summary>ReadMorphs (FaceMorphInterface.cpp:709-760). Lines: <c>extension = &lt;baseTri&gt;, &lt;file1&gt;, &lt;file2&gt;…</c>
    ''' The left-hand side must literally be "extension"; the first parameter is the map KEY, the rest are the
    ''' extended tri files added under it. '#' starts a comment.</summary>
    Private Sub ReadMorphs(iniBytes As Byte())
        For Each rawLine In ReadLines(iniBytes)
            Dim line = rawLine.Trim()
            If line.Length = 0 OrElse line.StartsWith("#") Then Continue For
            Dim eq = line.IndexOf("="c)
            If eq <= 0 Then Continue For
            If Not line.Substring(0, eq).Trim().StartsWith("extension", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim parts = line.Substring(eq + 1).Split(","c)
            If parts.Length < 2 Then Continue For
            Dim key = parts(0).Trim()
            If key.Length = 0 Then Continue For
            Dim list As List(Of String) = Nothing
            If Not _morphMap.TryGetValue(key, list) Then
                list = New List(Of String)()
                _morphMap(key) = list
            End If
            For i = 1 To parts.Length - 1
                Dim f = parts(i).Trim()
                If f.Length > 0 AndAlso Not list.Contains(f, StringComparer.OrdinalIgnoreCase) Then list.Add(f)
            Next
        Next
    End Sub

    ''' <summary>Build the catalog by scanning every mod folder for its races.ini (mirror of LoadMods→ForEachMod).
    ''' <paramref name="modFolderNames"/> = the loaded plugin filenames WITH extension (e.g. "RaceMenu.esp"), the
    ''' folder names skee64 uses (modInfo->name). Reads through FilesDictionary (loose &gt; BA2/BSA), so it sees
    ''' whatever the game would load. Missing files are skipped silently (same as skee64's IsValid() guard).</summary>
    Public Sub Load(modFolderNames As IEnumerable(Of String))
        If modFolderNames Is Nothing Then Return
        ' Cache of already-parsed slider files (mirror ReadRaces fileMap) keyed by full dictionary path.
        Dim fileCache As New Dictionary(Of String, List(Of (Gender As Integer, Def As SliderDef)))(StringComparer.OrdinalIgnoreCase)
        For Each modName In modFolderNames
            If String.IsNullOrEmpty(modName) Then Continue For
            Dim racesIniPath = $"{FaceGenMorphsDir}\{modName}\races.ini"
            Dim raceBytes = TryReadFile($"meshes\{racesIniPath}")
            If raceBytes Is Nothing Then Continue For
            _configMods.Add(modName)
            ReadRaces(raceBytes, modName, fileCache)
            ' morphs.ini — the base-tri → extended-tri map (LoadMods reads it from the same mod folder,
            ' FaceMorphInterface.cpp:517-534). Without it a slider's morph name has no geometry to resolve against.
            Dim morphBytes = TryReadFile($"meshes\{FaceGenMorphsDir}\{modName}\morphs.ini")
            If morphBytes IsNot Nothing Then ReadMorphs(morphBytes)
        Next
    End Sub

    ''' <summary>ReadRaces: parse a races.ini (raceEdid = sliderFiles). ':' prefix on a file ⇒ load from the shared
    ''' FaceGenMorphs root, else from the mod's subfolder.</summary>
    Private Sub ReadRaces(iniBytes As Byte(), modName As String, fileCache As Dictionary(Of String, List(Of (Gender As Integer, Def As SliderDef))))
        For Each raw In ReadLines(iniBytes)
            Dim line = raw.Trim()
            If line.Length = 0 OrElse line(0) = "#"c Then Continue For
            Dim eq = line.IndexOf("="c)
            If eq < 0 Then Continue For
            Dim raceEdid = line.Substring(0, eq).Trim()
            If raceEdid.Length = 0 Then Continue For
            Dim files = line.Substring(eq + 1).Split(","c)
            For Each f0 In files
                Dim f = f0.Trim()
                If f.Length = 0 Then Continue For
                Dim isSharedRoot As Boolean = (f(0) = ":"c)
                If isSharedRoot Then f = f.Substring(1).Trim()
                Dim sliderPath = If(isSharedRoot, $"meshes\{FaceGenMorphsDir}\{f}", $"meshes\{FaceGenMorphsDir}\{modName}\{f}")
                Dim sliders As List(Of (Gender As Integer, Def As SliderDef)) = Nothing
                If Not fileCache.TryGetValue(sliderPath, sliders) Then
                    Dim bytes = TryReadFile(sliderPath)
                    sliders = If(bytes Is Nothing, New List(Of (Gender As Integer, Def As SliderDef)), ReadSliders(bytes))
                    fileCache(sliderPath) = sliders
                End If
                For Each item In sliders
                    AddSlider(raceEdid, item.Gender, item.Def)
                Next
            Next
        Next
    End Sub

    ''' <summary>ReadSliders: parse a .slider file into (gender, def) pairs. Faithful to FaceMorphInterface.cpp:844-985.</summary>
    Private Shared Function ReadSliders(bytes As Byte()) As List(Of (Gender As Integer, Def As SliderDef))
        Dim result As New List(Of (Gender As Integer, Def As SliderDef))
        Dim gender As Integer = 0
        For Each raw In ReadLines(bytes)
            Dim line = raw.Trim()
            If line.Length = 0 OrElse line(0) = "#"c Then Continue For
            If line(0) = "["c Then
                Dim s = line.Substring(1)
                If s.StartsWith("Male", StringComparison.OrdinalIgnoreCase) Then gender = 0
                If s.StartsWith("Female", StringComparison.OrdinalIgnoreCase) Then gender = 1
                Continue For
            End If
            Dim eq = line.IndexOf("="c)
            If eq < 0 Then Continue For
            Dim name = line.Substring(0, eq).Trim()
            Dim params = line.Substring(eq + 1).Split(","c)
            For i = 0 To params.Length - 1 : params(i) = params(i).Trim() : Next
            If params.Length < 3 OrElse name.Length = 0 Then Continue For

            Dim def As New SliderDef With {.Name = name}
            Dim cat As Integer = 0 : Integer.TryParse(params(0), cat)
            If cat = -1 Then cat = SliderCategory.Extra
            If Not IsKnownCategory(cat) Then Continue For
            def.Category = cat

            If String.Equals(params(1), "Slider", StringComparison.OrdinalIgnoreCase) Then
                def.Type = SliderType.Slider
                If params.Length < 4 Then Continue For
                def.LowerBound = NoneToEmpty(params(2))
                def.UpperBound = NoneToEmpty(params(3))
            ElseIf String.Equals(params(1), "Preset", StringComparison.OrdinalIgnoreCase) Then
                def.Type = SliderType.Preset
                If params.Length < 4 Then Continue For
                def.LowerBound = params(2)
                Dim pc As Integer = 0 : Integer.TryParse(params(3), pc)
                def.PresetCount = Math.Min(255, pc)
            ElseIf String.Equals(params(1), "HeadPart", StringComparison.OrdinalIgnoreCase) Then
                def.Type = SliderType.HeadPart
                Dim pc As Integer = 0 : Integer.TryParse(params(2), pc)
                def.PresetCount = pc
            Else
                Continue For
            End If
            result.Add((gender, def))
        Next
        Return result
    End Function

    Private Sub AddSlider(raceEdid As String, gender As Integer, def As SliderDef)
        Dim map = If(gender = 1, _femaleByRace, _maleByRace)
        Dim sliders As Dictionary(Of String, SliderDef) = Nothing
        If Not map.TryGetValue(raceEdid, sliders) Then
            sliders = New Dictionary(Of String, SliderDef)(StringComparer.OrdinalIgnoreCase)
            map(raceEdid) = sliders
        End If
        sliders(def.Name) = def   ' later mods override same-name slider (last wins), matching insert-into-set semantics
    End Sub

    ''' <summary>All extended sliders for a race+gender, in name order (skee64 sorts the list). Empty if none.</summary>
    Public Function GetSliders(raceEditorId As String, isFemale As Boolean) As List(Of SliderDef)
        Dim map = If(isFemale, _femaleByRace, _maleByRace)
        Dim sliders As Dictionary(Of String, SliderDef) = Nothing
        If String.IsNullOrEmpty(raceEditorId) OrElse Not map.TryGetValue(raceEditorId, sliders) Then Return New List(Of SliderDef)
        Return sliders.Values.OrderBy(Function(s) s.Name, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    ''' <summary>Look up one slider by race+gender+name (the ValueSet key). Nothing if unknown.</summary>
    Public Function GetSlider(raceEditorId As String, isFemale As Boolean, sliderName As String) As SliderDef
        Dim map = If(isFemale, _femaleByRace, _maleByRace)
        Dim sliders As Dictionary(Of String, SliderDef) = Nothing
        If String.IsNullOrEmpty(raceEditorId) OrElse String.IsNullOrEmpty(sliderName) OrElse Not map.TryGetValue(raceEditorId, sliders) Then Return Nothing
        Dim def As SliderDef = Nothing
        sliders.TryGetValue(sliderName, def)
        Return def
    End Function

    Public Function HasAny() As Boolean
        Return _maleByRace.Count > 0 OrElse _femaleByRace.Count > 0
    End Function

    ''' <summary>Distinct race EditorIDs the catalog answers for (either gender). Diagnostic: <see cref="HasAny"/>
    ''' can be True off a single mod's races.ini while the vanilla races have nothing.</summary>
    Public Function RaceCount() As Integer
        Dim races As New HashSet(Of String)(_maleByRace.Keys, StringComparer.OrdinalIgnoreCase)
        races.UnionWith(_femaleByRace.Keys)
        Return races.Count
    End Function

    ''' <summary>The mod folders whose races.ini was actually FOUND and read (in scan order). The single most
    ''' useful thing to log: "RaceMenu.esp" missing here means the slider config never loaded — usually because
    ''' the plugin is not in the load order the scan was built from.</summary>
    Public Function LoadedConfigMods() As List(Of String)
        Return New List(Of String)(_configMods)
    End Function

    Private ReadOnly _configMods As New List(Of String)

    Public Shared Function CategoryName(cat As Integer) As String
        Select Case cat
            Case SliderCategory.Body : Return "Body"
            Case SliderCategory.Head : Return "Head"
            Case SliderCategory.Face : Return "Face"
            Case SliderCategory.Eyes : Return "Eyes"
            Case SliderCategory.Brow : Return "Brow"
            Case SliderCategory.Mouth : Return "Mouth"
            Case SliderCategory.Hair : Return "Hair"
            Case SliderCategory.Extra : Return "Extra"
            Case SliderCategory.Expressions : Return "Expressions"
            Case Else : Return $"Category {cat}"
        End Select
    End Function

    Private Shared Function IsKnownCategory(cat As Integer) As Boolean
        Select Case cat
            Case SliderCategory.Body, SliderCategory.Head, SliderCategory.Face, SliderCategory.Eyes,
                 SliderCategory.Brow, SliderCategory.Mouth, SliderCategory.Hair, SliderCategory.Extra, SliderCategory.Expressions
                Return True
            Case Else : Return False
        End Select
    End Function

    Private Shared Function NoneToEmpty(s As String) As String
        Return If(String.Equals(s, "None", StringComparison.OrdinalIgnoreCase), "", s)
    End Function

    ''' <summary>Read a file from FilesDictionary (loose &gt; BA2/BSA) by its meshes-prefixed path. Nothing if absent.
    ''' GetBytes normalizes the key internally and returns an empty array on a miss.</summary>
    Private Shared Function TryReadFile(meshesPath As String) As Byte()
        Try
            Dim b = FilesDictionary_class.GetBytes(meshesPath)
            If b IsNot Nothing AndAlso b.Length > 0 Then Return b
        Catch
        End Try
        Return Nothing
    End Function

    Private Shared Iterator Function ReadLines(bytes As Byte()) As IEnumerable(Of String)
        ' skee64 reads with BSFileUtil::ReadLine (ASCII lines). cp1252 covers the config's plain ASCII content.
        Dim text = Encoding.GetEncoding(1252).GetString(bytes)
        For Each ln In text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf(0))
            Yield ln
        Next
    End Function

End Class
