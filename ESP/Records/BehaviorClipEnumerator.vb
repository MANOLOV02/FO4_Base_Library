Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq

' =============================================================================
' Enumera las animaciones (clips) reproducibles de un NPC/raza: carga los behavior
' .hkx resueltos (RaceBehaviorResolver) y junta todos los hkbClipGenerator con su
' contexto (Role del subgraph + behavior de origen), deduplicados por archivo de clip.
'
' El caller inyecta el loader (Func path→bytes) que sabe de FilesDictionary (BA2+loose).
' Las referencias internas usan .hkt; los archivos reales son .hkx → se normaliza y se
' prueban candidatos (con/sin "Meshes\", .hkx/.hkt). El clip resultante (.hkx) se reproduce
' con HkxPoseImport sobre el skeleton de la raza.
' =============================================================================

Public NotInheritable Class BehaviorClipEnumerator
    Private Sub New()
    End Sub

    ''' <summary>Enumera los clips de todos los behavior .hkx de <paramref name="rb"/>. loadBehaviorHkx
    ''' recibe un path lógico ("Actors\…\X.hkx") y devuelve los bytes (o Nothing) — el caller resuelve
    ''' vía FilesDictionary. Devuelve clips deduplicados por archivo, con Roles/behaviors de origen.</summary>
    Public Shared Function EnumerateClips(rb As ResolvedRaceBehavior,
                                          loadBehaviorHkx As Func(Of String, Byte())) As List(Of ResolvedAnimationClip)
        Dim result As New List(Of ResolvedAnimationClip)
        If IsNothing(rb) OrElse loadBehaviorHkx Is Nothing Then Return result

        Dim actorRoot = DirName(rb.Project)
        ' Índice de EXISTENCIA (.hkx/.hkt del load order, canon OrdinalIgnoreCase). La resolución clip→archivo es
        ' por existencia sobre las rutas SAPT (search-path del engine), NO por animationNames (incompleto).
        Dim animSet = BuildAnimExistenceSet()
        ' Filtro TYPE-DRIVEN (KYWD.TNAM): se EXCLUYE un subgraph solo si requiere una keyword de IDENTIDAD de OTRA
        ' raza (None-typed ∧ ∈ KWDA de alguna raza ∧ ∉ esta raza). Los ejes de estado (Anim Injured/Archetype/
        ' Gender/…) NUNCA excluyen. [[arch_race_behavior_resolution]]
        Dim kwSet As New HashSet(Of UInteger)(rb.ActorKeywords)
        Dim byClip As New Dictionary(Of String, ResolvedAnimationClip)(StringComparer.OrdinalIgnoreCase)
        Dim graphCache As New Dictionary(Of String, HkxObjectGraph_Class)(StringComparer.OrdinalIgnoreCase)

        ' (1) ROOT behavior del actor (project→character→behaviorFilename): clips nativos (death/getup/swim/camera/
        '     pipboy) que NO están en ningún subgraph. SIN SAPT → resolución relativa al actor (incluye reuse
        '     explícito "..\Character\…", ej. SuperMutant reusa death humano). Eje "Core", neutro (no female-gated).
        Dim rootBeh = ResolveRootBehaviorFile(rb.Project, loadBehaviorHkx, actorRoot)
        If rootBeh <> "" Then
            EnumBehaviorClips(rootBeh, Nothing, "Core", "Normal", False, loadBehaviorHkx, animSet, actorRoot,
                              byClip, result, graphCache, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        End If

        ' (2) Subgraphs aplicables.
        For Each sg In rb.Subgraphs
            Dim foreignId = sg.ActorKeywordFormIDs.FirstOrDefault(Function(k) RaceBehaviorResolver.IsRaceIdentityKeyword(k) AndAlso Not kwSet.Contains(k))
            If foreignId <> 0UI Then Continue For
            Dim axis = StateAxisLabel(sg.ActorKeywordFormIDs)
            Dim reqFemale = sg.ActorKeywordFormIDs.Any(Function(k) RaceBehaviorResolver.KeywordType(k) = RaceBehaviorResolver.KwTypeAnimGender)
            ' Sigue el behavior del subgraph Y sus referencias (hkbBehaviorReferenceGenerator) recursivamente, con
            ' el MISMO SAPT/Role/eje. visited per-subgraph (un Core re-usado por varios actores con SAPT distinto).
            EnumBehaviorClips(NormHkx(sg.BehaviourGraph), sg.AnimationPaths, RoleName(sg.Role), axis, reqFemale, loadBehaviorHkx,
                              animSet, actorRoot, byClip, result, graphCache, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        Next
        Return result
    End Function

    ''' <summary>Enumera los clips alcanzables desde un behavior file: sus hkbClipGenerator (resueltos por EXISTENCIA
    ''' sobre las rutas SAPT) Y los behaviors referenciados (hkbBehaviorReferenceGenerator @+0x88), con el MISMO
    ''' SAPT/Role/eje. visited (per-subgraph) evita ciclos pero permite re-usar un Core con otro SAPT.</summary>
    Private Shared Sub EnumBehaviorClips(behFile As String, saptFolders As List(Of String), role As String, stateAxis As String, reqFemale As Boolean,
                                         loadBehaviorHkx As Func(Of String, Byte()),
                                         animSet As HashSet(Of String), actorRoot As String,
                                         byClip As Dictionary(Of String, ResolvedAnimationClip),
                                         result As List(Of ResolvedAnimationClip),
                                         graphCache As Dictionary(Of String, HkxObjectGraph_Class),
                                         visited As HashSet(Of String), depth As Integer)
        If depth > 12 OrElse String.IsNullOrWhiteSpace(behFile) OrElse Not visited.Add(behFile) Then Return
        Dim graph As HkxObjectGraph_Class = Nothing
        If Not graphCache.TryGetValue(behFile, graph) Then
            Dim bytes = TryLoad(loadBehaviorHkx, behFile)
            If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                Try
                    graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(bytes))
                Catch
                End Try
            End If
            graphCache(behFile) = graph
        End If
        If graph Is Nothing Then Return

        For Each obj In graph.GetObjectsByClassName("hkbClipGenerator")
            Dim cg = graph.ParseClipGenerator(obj)
            If IsNothing(cg) OrElse String.IsNullOrWhiteSpace(cg.AnimationName) Then Continue For
            Dim animFile = ResolveClipByExistence(cg.AnimationName, saptFolders, actorRoot, animSet)
            If animFile = "" Then Continue For
            Dim clip As ResolvedAnimationClip = Nothing
            If Not byClip.TryGetValue(animFile, clip) Then
                clip = New ResolvedAnimationClip With {
                    .AnimationFile = animFile,
                    .ClipName = If(cg.Name, ""),
                    .PlaybackSpeed = cg.PlaybackSpeed,
                    .SourceSkeletonPath = ActorRootOfAnim(animFile) & "\CharacterAssets\skeleton.hkx"
                }
                byClip(animFile) = clip
                result.Add(clip)
            End If
            If Not clip.SourceBehaviorFiles.Contains(behFile, StringComparer.OrdinalIgnoreCase) Then clip.SourceBehaviorFiles.Add(behFile)
            If Not clip.Roles.Contains(role) Then clip.Roles.Add(role)
            If Not clip.StateAxes.Contains(stateAxis) Then clip.StateAxes.Add(stateAxis)
            If Not reqFemale Then clip.RequiresFemale = False   ' alcanzable por un subgraph NEUTRO → disponible para varón
        Next

        ' Referencias a otros behaviors (relativas al actor del behavior referenciante), MISMO SAPT/Role/eje.
        Dim behRoot = ActorRootOfAnim(behFile)
        For Each refObj In graph.GetObjectsByClassName("hkbBehaviorReferenceGenerator")
            Dim refName = graph.ResolveLocalString(refObj.RelativeOffset + &H88)
            If String.IsNullOrWhiteSpace(refName) Then Continue For
            EnumBehaviorClips(NormHkx(CombineActor(behRoot, refName)), saptFolders, role, stateAxis, reqFemale, loadBehaviorHkx,
                              animSet, actorRoot, byClip, result, graphCache, visited, depth + 1)
        Next
    End Sub

    ''' <summary>Resuelve el archivo de animación REAL por EXISTENCIA (mecanismo search-path del engine), SIN
    ''' heurística de nombres. clipRel = parte del animName tras "Animations\". CON SAPT: por cada ruta (en orden
    ''' de prioridad) prueba el path completo y luego sin el primer segmento (actor-autor del core compartido); la
    ''' existencia en <paramref name="animSet"/> decide; NO cae a redirects cross-actor (el core del Alien no agarra
    ''' "..\MirelurkQueen\…" de otro consumidor). SIN SAPT (root): ResolveActorRelative (incluye reuse "..\Character\…").</summary>
    Private Shared Function ResolveClipByExistence(animName As String, saptFolders As List(Of String),
                                                   actorRoot As String, animSet As HashSet(Of String)) As String
        If String.IsNullOrWhiteSpace(animName) Then Return ""
        Dim norm = animName.Replace("/"c, "\"c)
        Dim i = norm.IndexOf("Animations\", StringComparison.OrdinalIgnoreCase)
        Dim clipRel = If(i >= 0, norm.Substring(i + "Animations\".Length), norm.TrimStart("\"c, "."c))
        If clipRel = "" Then Return ""
        If saptFolders Is Nothing OrElse saptFolders.Count = 0 Then
            Dim cand = CanonHkx(ResolveActorRelative(actorRoot, norm))
            Return If(animSet.Contains(cand), cand, "")
        End If
        For Each s In saptFolders
            If String.IsNullOrWhiteSpace(s) Then Continue For
            Dim sn = s.Replace("/"c, "\"c).TrimEnd("\"c)
            Dim c1 = CanonHkx(sn & "\" & clipRel)
            If animSet.Contains(c1) Then Return c1
            Dim j = clipRel.IndexOf("\"c)
            If j >= 0 Then
                Dim c2 = CanonHkx(sn & "\" & clipRel.Substring(j + 1))
                If animSet.Contains(c2) Then Return c2
            End If
        Next
        Return ""
    End Function

    ''' <summary>Etiqueta de eje de ESTADO de un subgraph = los nombres de tipo (KYWD.TNAM) de sus keywords de
    ''' estado (tipo ≠ None), EXCLUYENDO 'Anim Gender' (eso va al checkbox de género). "Normal" si no hay ninguna.</summary>
    Private Shared Function StateAxisLabel(sakd As List(Of UInteger)) As String
        If sakd Is Nothing OrElse sakd.Count = 0 Then Return "Normal"
        Dim names = sakd.Where(Function(k) RaceBehaviorResolver.KeywordType(k) <> RaceBehaviorResolver.KwTypeNone AndAlso
                                            RaceBehaviorResolver.KeywordType(k) <> RaceBehaviorResolver.KwTypeAnimGender).
                         Select(Function(k) RaceBehaviorResolver.KeywordTypeName(k)).Distinct().OrderBy(Function(s) s).ToList()
        Return If(names.Count = 0, "Normal", String.Join("+", names))
    End Function

    ' project → CharacterFilenames → character → behaviorFilename (root behavior del actor). "" si no resuelve.
    Private Shared Function ResolveRootBehaviorFile(proj As String, loadBehaviorHkx As Func(Of String, Byte()), actorRoot As String) As String
        Dim pb = TryLoad(loadBehaviorHkx, NormHkx(proj))
        If pb Is Nothing Then Return ""
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(pb))
            For Each o In g.GetObjectsByClassName("hkbProjectStringData")
                Dim psd = g.ParseProjectStringData(o)
                If psd Is Nothing Then Continue For
                For Each cf In psd.CharacterFilenames
                    If String.IsNullOrWhiteSpace(cf) Then Continue For
                    Dim cb = TryLoad(loadBehaviorHkx, CombineActor(actorRoot, cf))
                    If cb Is Nothing Then cb = TryLoad(loadBehaviorHkx, cf)
                    If cb Is Nothing Then Continue For
                    Dim gc = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(cb))
                    For Each co In gc.GetObjectsByClassName("hkbCharacterStringData")
                        Dim csd = gc.ParseCharacterStringData(co)
                        If csd IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(csd.BehaviorFilename) Then Return NormHkx(CombineActor(actorRoot, csd.BehaviorFilename))
                    Next
                Next
            Next
        Catch
        End Try
        Return ""
    End Function

    ' Índice de existencia: todos los .hkx/.hkt del load order, canon (sin Meshes\, .hkt→.hkx) OrdinalIgnoreCase.
    Private Shared Function BuildAnimExistenceSet() As HashSet(Of String)
        Dim s As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each key In FilesDictionary_class.Dictionary.Keys
            If key.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase) OrElse key.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then s.Add(CanonHkx(key))
        Next
        Return s
    End Function

    ' Canon de un path .hkx/.hkt: quita "Meshes\", normaliza .hkt→.hkx. NO lowercasea (el match es OrdinalIgnoreCase;
    ' preservar case para que el path resuelto cargue tal cual vía FilesDictionary, que es OrdinalIgnoreCase igual).
    Private Shared Function CanonHkx(p As String) As String
        If String.IsNullOrEmpty(p) Then Return ""
        p = p.Replace("/"c, "\"c)
        If p.StartsWith("Meshes\", StringComparison.OrdinalIgnoreCase) Then p = p.Substring(7)
        Return NormHkx(p)
    End Function

    ''' <summary>Skeleton de Havok SÓLIDO para NPC Manager: NO el "hermano" del skeleton.nif (heurístico
    ''' de WM), sino el <c>rigName</c> que declara el behavior character. Cadena: project .hkx →
    ''' hkbProjectStringData.CharacterFilenames → character .hkx → hkbCharacterStringData.RigName
    ''' (ej. "CharacterAssets\skeleton.HKT", relativo a la carpeta del actor = dirname del project).
    ''' Devuelve el path .hkx normalizado (o "" si no se pudo resolver).</summary>
    Public Shared Function ResolveHavokSkeleton(rb As ResolvedRaceBehavior, loadBehaviorHkx As Func(Of String, Byte())) As String
        If IsNothing(rb) OrElse loadBehaviorHkx Is Nothing OrElse String.IsNullOrWhiteSpace(rb.Project) Then Return ""
        Dim actorRoot = DirName(rb.Project)   ' p.ej. "actors\Character"

        ' 1) project → character files
        Dim projBytes = TryLoad(loadBehaviorHkx, NormHkx(rb.Project))
        If projBytes Is Nothing Then Return ""
        Dim charFiles As New List(Of String)
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(projBytes))
            For Each o In g.GetObjectsByClassName("hkbProjectStringData")
                Dim psd = g.ParseProjectStringData(o)
                If psd IsNot Nothing Then charFiles.AddRange(psd.CharacterFilenames)
            Next
        Catch
        End Try

        ' 2) character → rigName (skeleton de Havok)
        For Each cf In charFiles
            If String.IsNullOrWhiteSpace(cf) Then Continue For
            Dim charBytes = TryLoad(loadBehaviorHkx, CombineActor(actorRoot, cf))
            If charBytes Is Nothing Then charBytes = TryLoad(loadBehaviorHkx, cf)
            If charBytes Is Nothing Then Continue For
            Try
                Dim gc = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(charBytes))
                For Each o In gc.GetObjectsByClassName("hkbCharacterStringData")
                    Dim csd = gc.ParseCharacterStringData(o)
                    If csd IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(csd.RigName) Then
                        Return NormHkx(CombineActor(actorRoot, csd.RigName))
                    End If
                Next
            Catch
            End Try
        Next
        Return ""
    End Function


    ' Actor root de un path de animación = prefijo antes de la subcarpeta estándar. Maneja creatures DLC
    ' de 3 segmentos: "Actors\DLC03\Angler\Animations\X.hkx" → "Actors\DLC03\Angler".
    Private Shared Function ActorRootOfAnim(animPath As String) As String
        If String.IsNullOrWhiteSpace(animPath) Then Return ""
        For Each marker In {"\Animations\", "\CharacterAssets\", "\Characters\", "\Behaviors\"}
            Dim i = animPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase)
            If i > 0 Then Return animPath.Substring(0, i)
        Next
        Return DirName(animPath)
    End Function

    ' dirname con separador backslash (los paths de FO4 usan "\").
    Private Shared Function DirName(p As String) As String
        Dim i = p.LastIndexOf("\"c)
        Return If(i > 0, p.Substring(0, i), "")
    End Function

    ' Combina actorRoot + path actor-relativo; si el path ya es absoluto (empieza con actors\/meshes\), lo deja.
    Private Shared Function CombineActor(actorRoot As String, rel As String) As String
        If String.IsNullOrWhiteSpace(rel) Then Return rel
        Dim lc = rel.TrimStart("\"c)
        If lc.StartsWith("actors\", StringComparison.OrdinalIgnoreCase) OrElse lc.StartsWith("meshes\", StringComparison.OrdinalIgnoreCase) Then Return lc
        If actorRoot = "" Then Return lc
        Return actorRoot.TrimEnd("\"c) & "\" & lc
    End Function

    ' Combina con actorRoot y RESUELVE segmentos "..\" / ".\" → path Data-relativo limpio (ej.
    ' "Actors\Molerat" + "..\Bloatfly\Animations\X.hkx" = "Actors\Bloatfly\Animations\X.hkx").
    Private Shared Function ResolveActorRelative(actorRoot As String, rel As String) As String
        If String.IsNullOrWhiteSpace(rel) Then Return ""
        Dim combined = CombineActor(actorRoot, rel)
        Dim stack As New List(Of String)
        For Each seg In combined.Split("\"c)
            If seg = "" OrElse seg = "." Then Continue For
            If seg = ".." Then
                If stack.Count > 0 Then stack.RemoveAt(stack.Count - 1)
            Else
                stack.Add(seg)
            End If
        Next
        Return String.Join("\", stack)
    End Function

    ' Carga probando candidatos de path (con/sin "Meshes\", .hkx/.hkt).
    Private Shared Function TryLoad(loader As Func(Of String, Byte()), path As String) As Byte()
        For Each cand In Candidates(path)
            Dim b = loader(cand)
            If b IsNot Nothing AndAlso b.Length > 0 Then Return b
        Next
        Return Nothing
    End Function

    Private Shared Iterator Function Candidates(path As String) As IEnumerable(Of String)
        If String.IsNullOrWhiteSpace(path) Then Return
        Dim variants As New List(Of String) From {path}
        If path.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase) Then variants.Add(path.Substring(0, path.Length - 4) & ".hkt")
        If path.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then variants.Add(path.Substring(0, path.Length - 4) & ".hkx")
        For Each v In variants
            Yield v
            If Not v.StartsWith("Meshes\", StringComparison.OrdinalIgnoreCase) Then Yield "Meshes\" & v
        Next
    End Function

    Private Shared Function NormHkx(p As String) As String
        If String.IsNullOrWhiteSpace(p) Then Return ""
        If p.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then Return p.Substring(0, p.Length - 4) & ".hkx"
        Return p
    End Function

    Private Shared Function RoleName(role As Integer) As String
        Select Case role
            Case 0 : Return "MT"
            Case 1 : Return "Weapon"
            Case 2 : Return "Furniture"
            Case 3 : Return "Idle"
            Case 4 : Return "Pipboy"
            Case Else : Return "Other"
        End Select
    End Function
End Class

''' <summary>Una animación reproducible del NPC/raza: el archivo de clip .hkx, el nombre del clip
''' (hkbClipGenerator), velocidad, y el contexto (Roles + behaviors de origen). Se reproduce con
''' HkxPoseImport sobre el skeleton de la raza.</summary>
Public Class ResolvedAnimationClip
    Public AnimationFile As String = ""        ' path Data-relativo YA RESUELTO por existencia (.hkt→.hkx)
    Public ClipName As String = ""             ' nombre del hkbClipGenerator
    Public PlaybackSpeed As Single = 1.0F
    Public SourceSkeletonPath As String = ""   ' skeleton del actor de ORIGEN de la anim (para interpretarla)
    Public ReadOnly Property Roles As New List(Of String)               ' MT/Weapon/Furniture/Idle/Pipboy/Core
    Public ReadOnly Property SourceBehaviorFiles As New List(Of String) ' behavior .hkx que lo contienen
    ''' <summary>Ejes de ESTADO (nombre del tipo KYWD.TNAM de los SAKD del subgraph: "Anim Injured"/"Anim Archetype"/
    ''' "Anim Flavor"/"Attach Point"…), o "Normal" si el subgraph no tiene keyword de estado. Para el árbol del selector.</summary>
    Public ReadOnly Property StateAxes As New List(Of String)
    ''' <summary>True si el clip SOLO es alcanzable vía subgraphs que requieren la keyword 'Anim Gender' (Female).
    ''' Se pone False en cuanto un subgraph NEUTRO (sin gender) lo alcanza (queda disponible para varón).
    ''' El selector lo usa para el checkbox "filter by gender".</summary>
    Public RequiresFemale As Boolean = True
End Class
