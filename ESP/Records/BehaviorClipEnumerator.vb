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

        ' behaviorFile (normalizado) → roles de los subgraphs que lo referencian.
        Dim rolesByFile As New Dictionary(Of String, SortedSet(Of String))(StringComparer.OrdinalIgnoreCase)
        For Each sg In rb.Subgraphs
            Dim f = NormHkx(sg.BehaviourGraph)
            If f = "" Then Continue For
            If Not rolesByFile.ContainsKey(f) Then rolesByFile(f) = New SortedSet(Of String)(StringComparer.OrdinalIgnoreCase)
            rolesByFile(f).Add(RoleName(sg.Role))
        Next

        Dim byClip As New Dictionary(Of String, ResolvedAnimationClip)(StringComparer.OrdinalIgnoreCase)
        Dim actorRoot = DirName(rb.Project)

        ' Lista AUTORITATIVA de animaciones del actor = hkbCharacterStringData.animationNames del personaje
        ' (project → CharacterFilenames → character). El engine NO reproduce el archivo que apunta el
        ' animationName del clip (ese es una referencia de autoría, ej "..\Bloatfly\Animations\Idle.hkt",
        ' al actor original del que se copió el behavior compartido): liga el clip a la animación NATIVA
        ' del actor por su suffix "Animations\…". Un clip cuyo suffix NO está en esta lista NO es una
        ' animación de este actor (ej furniture humano en un aguijoneador) → se descarta. El path nativo
        ' sale del propio animationNames + actor root, sin construir paths a mano. [[arch_race_behavior_resolution]]
        Dim actorAnims = ResolveActorAnimations(rb, loadBehaviorHkx, actorRoot)

        For Each behaviorFile In rb.DistinctBehaviorFiles()
            Dim bytes = TryLoad(loadBehaviorHkx, behaviorFile)
            If bytes Is Nothing OrElse bytes.Length = 0 Then Continue For

            Dim graph As HkxObjectGraph_Class
            Try
                graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(bytes))
            Catch
                Continue For
            End Try

            Dim fileRoles As SortedSet(Of String) = Nothing
            rolesByFile.TryGetValue(behaviorFile, fileRoles)
            Dim roleTags = If(fileRoles IsNot Nothing AndAlso fileRoles.Count > 0, fileRoles.ToArray(), New String() {"Project"})

            For Each obj In graph.GetObjectsByClassName("hkbClipGenerator")
                Dim cg = graph.ParseClipGenerator(obj)
                If IsNothing(cg) Then Continue For
                ' Resolver al archivo NATIVO del actor vía animationNames del character (no seguir el
                ' redirect "..\OtroActor\"). Si el clip no corresponde a una animación del actor → drop.
                Dim key = AnimSuffixKey(cg.AnimationName)
                Dim animFile As String = Nothing
                If key = "" OrElse Not actorAnims.TryGetValue(key, animFile) Then Continue For

                Dim clip As ResolvedAnimationClip = Nothing
                If Not byClip.TryGetValue(animFile, clip) Then
                    ' Skeleton para interpretar la anim = el del ACTOR del path YA RESUELTO (animationNames).
                    ' Una anim nativa ("Actors\Supermutant\…") usa el rig de Supermutant; una redirigida por
                    ' Bethesda ("..\Character\…" → "Actors\Character\…") usa el rig HUMANO. El rig de cualquier
                    ' actor es "<actorFolder>\CharacterAssets\skeleton" (es el rigName que parseamos). El engine
                    ' mapea por NOMBRE de hueso al skeleton vivo → no deforma aunque el actor de origen difiera.
                    clip = New ResolvedAnimationClip With {
                        .AnimationFile = animFile,
                        .ClipName = If(cg.Name, ""),
                        .PlaybackSpeed = cg.PlaybackSpeed,
                        .SourceSkeletonPath = ActorRootOfAnim(animFile) & "\CharacterAssets\skeleton.hkx"
                    }
                    byClip(animFile) = clip
                    result.Add(clip)
                End If
                If Not clip.SourceBehaviorFiles.Contains(behaviorFile, StringComparer.OrdinalIgnoreCase) Then clip.SourceBehaviorFiles.Add(behaviorFile)
                For Each r In roleTags
                    If Not clip.Roles.Contains(r) Then clip.Roles.Add(r)
                Next
            Next
        Next
        Return result
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

    ''' <summary>Lista autoritativa de animaciones del actor: project → hkbProjectStringData.CharacterFilenames
    ''' → character → hkbCharacterStringData.animationNames. Devuelve un mapa suffix-normalizado
    ''' ("animations\idle.hkx") → path nativo del actor ("Actors\Stingwing\Animations\Idle.hkx"). El path
    ''' sale del propio animationNames + actor root (no se construye a mano).</summary>
    Private Shared Function ResolveActorAnimations(rb As ResolvedRaceBehavior,
                                                   loadBehaviorHkx As Func(Of String, Byte()),
                                                   actorRoot As String) As Dictionary(Of String, String)
        Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        If IsNothing(rb) OrElse loadBehaviorHkx Is Nothing OrElse String.IsNullOrWhiteSpace(rb.Project) Then Return result

        Dim projBytes = TryLoad(loadBehaviorHkx, NormHkx(rb.Project))
        If projBytes Is Nothing Then Return result
        Dim charFiles As New List(Of String)
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(projBytes))
            For Each o In g.GetObjectsByClassName("hkbProjectStringData")
                Dim psd = g.ParseProjectStringData(o)
                If psd IsNot Nothing Then charFiles.AddRange(psd.CharacterFilenames)
            Next
        Catch
        End Try

        For Each cf In charFiles
            If String.IsNullOrWhiteSpace(cf) Then Continue For
            Dim charBytes = TryLoad(loadBehaviorHkx, CombineActor(actorRoot, cf))
            If charBytes Is Nothing Then charBytes = TryLoad(loadBehaviorHkx, cf)
            If charBytes Is Nothing Then Continue For
            Try
                Dim gc = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(charBytes))
                For Each o In gc.GetObjectsByClassName("hkbCharacterStringData")
                    Dim csd = gc.ParseCharacterStringData(o)
                    If csd Is Nothing Then Continue For
                    For Each an In csd.AnimationFilenames
                        Dim key = AnimSuffixKey(an)
                        If key = "" Then Continue For
                        ' ResolveActorRelative (no CombineActor) para canonicalizar redirects "..\" del
                        ' animationNames (ej humano: "..\PowerArmor\Animations\…" → "Actors\PowerArmor\…").
                        If Not result.ContainsKey(key) Then result(key) = NormHkx(ResolveActorRelative(actorRoot, an))
                    Next
                Next
            Catch
            End Try
        Next
        Return result
    End Function

    ''' <summary>Clave de matcheo de una animación: el suffix desde "Animations\" en minúsculas y .hkx
    ''' (el animationName del clip y el animationNames del character usan la misma forma relativa).</summary>
    Private Shared Function AnimSuffixKey(animName As String) As String
        If String.IsNullOrWhiteSpace(animName) Then Return ""
        Dim norm = animName.Replace("/"c, "\"c)
        Dim i = norm.IndexOf("Animations\", StringComparison.OrdinalIgnoreCase)
        Dim suffix = If(i >= 0, norm.Substring(i), norm.TrimStart("\"c, "."c))
        Return NormHkx(suffix).ToLowerInvariant()
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
    Public AnimationFile As String = ""        ' path Data-relativo YA RESUELTO (de animationNames; .hkt→.hkx)
    Public ClipName As String = ""             ' nombre del hkbClipGenerator
    Public PlaybackSpeed As Single = 1.0F
    Public SourceSkeletonPath As String = ""   ' skeleton del actor de ORIGEN de la anim (para interpretarla)
    Public ReadOnly Property Roles As New List(Of String)               ' MT/Weapon/Furniture/Idle/Pipboy/Project
    Public ReadOnly Property SourceBehaviorFiles As New List(Of String) ' behavior .hkx que lo contienen
End Class
