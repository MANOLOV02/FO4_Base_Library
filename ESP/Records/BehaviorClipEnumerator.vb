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
        ' Basenames de TODOS los clip-generators vistos en el walk (resuelvan o no). Las variantes mood/furniture y
        ' los furniture-direccionales (FrontEnterFromWalk…, cuyo path autoreado no resuelve por anidamiento SAPT) se
        ' recuperan expandiendo ESTOS nombres contra las carpetas SAPT — estructural, no folder-scan ciego.
        Dim clipGenBases As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        ' (1) ROOT behavior del actor (project→character→behaviorFilename): clips nativos (death/getup/swim/camera/
        '     pipboy) que NO están en ningún subgraph. SIN SAPT → resolución relativa al actor (incluye reuse
        '     explícito "..\Character\…", ej. SuperMutant reusa death humano). Eje "Core", neutro (no female-gated).
        Dim rootBeh = ResolveRootBehaviorFile(rb.Project, loadBehaviorHkx, actorRoot)
        If rootBeh <> "" Then
            EnumBehaviorClips(rootBeh, Nothing, "Core", "Normal", False, -1, loadBehaviorHkx, animSet, clipGenBases, actorRoot,
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
            EnumBehaviorClips(NormHkx(sg.BehaviourGraph), sg.AnimationPaths, RoleName(sg.Role), axis, reqFemale, sg.Perspective, loadBehaviorHkx,
                              animSet, clipGenBases, actorRoot, byClip, result, graphCache, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        Next

        ' ── PASADA DE COBERTURA (file-driven): mapea TODO .hkx bajo las rutas SAPT de los subgraphs APLICADOS que el
        ' walk de clip-generators no alcanzó (variantes mood/gender + gestos/idle reproducidos por evento en runtime).
        ' Scope = subárbol de cada SAPT-dir de subgraphs aplicados (MISMO filtro de identidad ajena que arriba) → para
        ' robots de carpeta compartida queda DENTRO de su subcarpeta. Validado: residual=0 en razas de carpeta dedicada.
        Dim coverageDirs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim foreignDirs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)   ' SAPT de subgraphs de OTRA raza
        For Each sg In rb.Subgraphs
            If sg.AnimationPaths Is Nothing Then Continue For
            Dim foreignId = sg.ActorKeywordFormIDs.FirstOrDefault(Function(k) RaceBehaviorResolver.IsRaceIdentityKeyword(k) AndAlso Not kwSet.Contains(k))
            Dim target = If(foreignId <> 0UI, foreignDirs, coverageDirs)
            For Each sp In sg.AnimationPaths
                If String.IsNullOrWhiteSpace(sp) Then Continue For
                target.Add(CanonHkx(sp.Replace("/"c, "\"c).TrimEnd("\"c)))
            Next
        Next
        ' Las carpetas que pertenecen a OTRA raza (un subgraph EXCLUIDO por identidad las lista, ej. Assaultron\ para
        ' Protectron) NO se cubren — aunque un subgraph genérico (SAKD=[], ej. FurnitureBehavior compartido) también las
        ' liste. Si no, la pasada de variantes traía TODA la carpeta del otro robot (locomoción/combate) a esta raza.
        ' Los clips compartidos puntuales (furniture) que esa raza SÍ usa siguen entrando por el WALK (resuelve clip-gens).
        coverageDirs.ExceptWith(foreignDirs)

        ' ── PASADA IDLE (estructural): expande los patrones IDLE.GNAM aplicables a la raza. $(Subgraph) → cada carpeta
        ' SAPT aplicada (coverageDirs); * → glob sobre animSet; literal → match directo. Cada archivo matcheado entra
        ' con su Category (= evento ENAM del IDLE). Esto es la fuente AUTORITATIVA del pool de gestos/poses/turns.
        ' Gateo de los patrones $(Subgraph) por el DNAM del IDLE ∈ behaviors REALMENTE caminados (graphCache.Keys,
        ' que incluye los alcanzados por hkbBehaviorReferenceGenerator — no solo DistinctBehaviorFiles).
        Dim coverageList = coverageDirs.ToList()
        Dim walkedBases As New HashSet(Of String)(graphCache.Keys.Select(Function(k) System.IO.Path.GetFileNameWithoutExtension(k)), StringComparer.OrdinalIgnoreCase)
        For Each ia In rb.IdleAnimations
            ' GATE de raza para TODOS los patrones (token y literal): el IDLE aplica solo si su behavior (DNAM) es uno que
            ' la raza REALMENTE camina. Sin esto, un patrón de path literal (Actors\Character\…\Quest\Cheering\…) matchea
            ' por existencia GLOBAL y le metería animaciones de Character/PowerArmor a un robot (que nunca camina ese DNAM).
            If ia.DnamBasename = "" OrElse Not walkedBases.Contains(ia.DnamBasename) Then Continue For
            Dim pat = ia.GnamPattern.Replace("/"c, "\"c)
            Dim candidates As New List(Of String)
            Dim tok = pat.IndexOf("$(Subgraph)", StringComparison.OrdinalIgnoreCase)
            If tok >= 0 Then
                Dim tail = pat.Substring(tok + "$(Subgraph)".Length).TrimStart("\"c)
                For Each d In coverageList : candidates.Add(d & "\" & tail) : Next
            ElseIf pat.IndexOf("$(", StringComparison.Ordinal) >= 0 Then
                Continue For   ' otro token desconocido → no expandir
            Else
                candidates.Add(CanonHkx(pat))   ' patrón de path literal (ej. Quest\Cheering\…\*.hkx) → existencia gatea
            End If
            For Each cand In candidates
                Dim cf = CanonHkx(cand)
                Dim star = cf.IndexOf("*"c)
                If star < 0 Then
                    If animSet.Contains(cf) AndAlso Not byClip.ContainsKey(cf) Then AddIdleClip(cf, ia.Category, byClip, result)
                Else
                    Dim pre = cf.Substring(0, star), suf = cf.Substring(star + 1)
                    For Each f In animSet
                        If f.StartsWith(pre, StringComparison.OrdinalIgnoreCase) AndAlso f.EndsWith(suf, StringComparison.OrdinalIgnoreCase) AndAlso Not byClip.ContainsKey(f) Then AddIdleClip(f, ia.Category, byClip, result)
                    Next
                End If
            Next
        Next

        ' ── PASADA POR-SUBGRAPH (estructural, NO folder-scan ciego): recupera las VARIANTES mood/furniture de cada
        ' clip-generator. La resolución por-existencia colapsa cada clip-gen a UN archivo (primer match SAPT global);
        ' acá agregamos los DEMÁS archivos cuyo nombre == un clip-generator REAL (behaviorClipBases) y que existen bajo
        ' otra carpeta SAPT de la raza (mood/furniture variant). Mismo mecanismo SAPT que el engine; sin heurística de
        ' carpeta — solo archivos nombrados por un clip-generator. Lo que NO matchea queda como huérfano REPORTABLE.
        If coverageDirs.Count > 0 Then
            ' Mapa carpeta→Roles de los clips YA enumerados (para propagar el Role a las variantes de la misma carpeta).
            Dim folderRoles As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase)
            For Each c In result
                Dim fr = FolderRelKey(c.AnimationFile)
                Dim hs As HashSet(Of String) = Nothing
                If Not folderRoles.TryGetValue(fr, hs) Then hs = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) : folderRoles(fr) = hs
                For Each r In c.Roles : hs.Add(r) : Next
            Next
            For Each key In animSet
                If byClip.ContainsKey(key) Then Continue For
                If Not IsUnderCoverageDir(key, coverageDirs) Then Continue For
                ' Solo archivos nombrados por un clip-generator REAL del walk (clipGenBases, resuelvan o no): recupera las
                ' variantes mood/furniture + los furniture-direccionales cuyo path autoreado no resolvió. NO folder-scan ciego.
                If Not clipGenBases.Contains(System.IO.Path.GetFileNameWithoutExtension(key)) Then Continue For
                Dim clip As New ResolvedAnimationClip With {
                    .AnimationFile = key,
                    .ClipName = "",
                    .PlaybackSpeed = 1.0F,
                    .SourceSkeletonPath = ActorRootOfAnim(key) & "\CharacterAssets\skeleton.hkx",
                    .FromBehaviorGraph = False,
                    .RequiresFemale = False,
                    .Is1stPersonOnly = (key.IndexOf("\_1stPerson\", StringComparison.OrdinalIgnoreCase) >= 0)
                }
                ' Role propagado: carpeta exacta, si no el ancestro más cercano con Role conocido.
                For Each r In RolesForFolder(FolderRelKey(key), folderRoles) : clip.Roles.Add(r) : Next
                byClip(key) = clip
                result.Add(clip)
            Next

            ' ── FALLBACK CONVENCIÓN DEL ENGINE (opción B, decisión usuario): SOLO para archivos que NINGUNA fuente
            ' estructural anterior mapeó. Estos los arma el DynamicAnimationTaggingGenerator en RUNTIME (su nombre no
            ' está en los datos); el prefijo to_/alt_ es la convención de filename de Bethesda, y el archivo está
            ' anclado estructuralmente por vivir BAJO una carpeta SAPT (= un archetype/actividad de la raza):
            '   alt_<X>  = toma alternativa de una animación X YA mapeada (mappedBases).
            '   to_<Z>   = transición a un archetype/flavor Z, desde la carpeta-archetype SAPT donde vive el archivo.
            Dim mappedBases As New HashSet(Of String)(result.Select(Function(c) System.IO.Path.GetFileNameWithoutExtension(c.AnimationFile)), StringComparer.OrdinalIgnoreCase)
            For Each key In animSet
                If byClip.ContainsKey(key) Then Continue For      ' "solo si no están mapeados de otro lado"
                If Not IsUnderCoverageDir(key, coverageDirs) Then Continue For
                Dim b = System.IO.Path.GetFileNameWithoutExtension(key)
                Dim isAlt = b.StartsWith("alt_", StringComparison.OrdinalIgnoreCase) AndAlso mappedBases.Contains(b.Substring(4))
                Dim isTo = b.StartsWith("to_", StringComparison.OrdinalIgnoreCase)   ' anclado por IsUnderCoverageDir (carpeta SAPT)
                If Not (isAlt OrElse isTo) Then Continue For
                Dim clip As New ResolvedAnimationClip With {
                    .AnimationFile = key,
                    .ClipName = "",
                    .PlaybackSpeed = 1.0F,
                    .SourceSkeletonPath = ActorRootOfAnim(key) & "\CharacterAssets\skeleton.hkx",
                    .FromBehaviorGraph = False,
                    .RequiresFemale = False,
                    .Is1stPersonOnly = (key.IndexOf("\_1stPerson\", StringComparison.OrdinalIgnoreCase) >= 0)
                }
                For Each r In RolesForFolder(FolderRelKey(key), folderRoles) : clip.Roles.Add(r) : Next
                byClip(key) = clip
                result.Add(clip)
            Next
        End If

        ' NOTA (canónico, sin descartar clips): NO se filtra el resultado a posteriori. El WALK es la resolución del
        ' engine (clip-gen → archivo vía SAPT) y se conserva ÍNTEGRO — incluido lo que un subgraph genérico (SAKD=[],
        ' ej. FurnitureBehavior con SAPT=carpeta de otro bot) resuelve, porque ESO es lo que el engine reproduce.
        ' El único scoping es sobre la HEURÍSTICA de cobertura (variante/IDLE por NOMBRE): coverageDirs.ExceptWith(
        ' foreignDirs) evita que la expansión por-nombre arrastre la carpeta ENTERA de otra raza (los 125 de Assaultron
        ' en Protectron eran eso — locomoción/combate ajenos pescados por nombre compartido). Modular/propio (RoboBrain\
        ' AssaultronArms\…) y reuse explícito (SuperMutant→..\Character\…) entran por el WALK y NO se tocan.
        Return result
    End Function

    ''' <summary>Pasada LAZY: para cada clip carga su archivo de animación resuelto, parsea el primer
    ''' hkaAnimationBinding y setea IsAdditive = (BlendHint &lt;&gt; 0). Idempotente (salta los que ya tienen
    ''' AdditiveKnown). Cara (carga 1 archivo por clip) → el caller la corre en background, UNA vez por
    ''' lista cacheada. loadHkx = el mismo Func(path→bytes) del caller (FilesDictionary BA2+loose).</summary>
    Public Shared Sub DetectAdditiveFlags(clips As IEnumerable(Of ResolvedAnimationClip), loadHkx As Func(Of String, Byte()))
        If clips Is Nothing OrElse loadHkx Is Nothing Then Return
        For Each c In clips
            If c Is Nothing OrElse c.AdditiveKnown Then Continue For
            Dim bytes = LoadFirstHkxCandidate(loadHkx, c.AnimationFile)
            If bytes Is Nothing OrElse bytes.Length = 0 Then c.AdditiveKnown = True : Continue For
            Try
                Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(bytes))
                Dim b = g.GetObjectsByClassName("hkaAnimationBinding").FirstOrDefault()
                If b IsNot Nothing Then
                    Dim ab = g.ParseAnimationBinding(b)
                    If ab IsNot Nothing Then c.IsAdditive = (ab.BlendHint <> 0)
                End If
            Catch
            End Try
            c.AdditiveKnown = True
        Next
    End Sub

    ''' <summary>Enumera los clips alcanzables desde un behavior file: sus hkbClipGenerator (resueltos por EXISTENCIA
    ''' sobre las rutas SAPT) Y los behaviors referenciados (hkbBehaviorReferenceGenerator @+0x88), con el MISMO
    ''' SAPT/Role/eje. visited (per-subgraph) evita ciclos pero permite re-usar un Core con otro SAPT.</summary>
    Private Shared Sub EnumBehaviorClips(behFile As String, saptFolders As List(Of String), role As String, stateAxis As String, reqFemale As Boolean, perspective As Integer,
                                         loadBehaviorHkx As Func(Of String, Byte()),
                                         animSet As HashSet(Of String), clipGenBases As HashSet(Of String), actorRoot As String,
                                         byClip As Dictionary(Of String, ResolvedAnimationClip),
                                         result As List(Of ResolvedAnimationClip),
                                         graphCache As Dictionary(Of String, HkxObjectGraph_Class),
                                         visited As HashSet(Of String), depth As Integer)
        If depth > 12 OrElse String.IsNullOrWhiteSpace(behFile) OrElse Not visited.Add(behFile) Then Return
        Dim graph As HkxObjectGraph_Class = Nothing
        If Not graphCache.TryGetValue(behFile, graph) Then
            Dim bytes = LoadFirstHkxCandidate(loadBehaviorHkx, behFile)
            If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                Try
                    graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(bytes))
                Catch
                End Try
            End If
            graphCache(behFile) = graph
        End If
        If graph Is Nothing Then Return

        ' NOTA aditivos: la aditividad es CANÓNICA del archivo de animación — hkaAnimationBinding.blendHint
        ' (=2 en RotateRing*_Add/CrippledNoise/DialogueIdle_Long1; =0 en clips normales). Un offset roto
        ' del parser (saltaba 2 arrays en vez de 3) la leía mal y motivó un scan por NOMBRE de
        ' DynamicAnimationTaggingGenerator acá — ELIMINADO: el binding del archivo es la única fuente.
        For Each obj In graph.GetObjectsByClassName("hkbClipGenerator")
            Dim cg = graph.ParseClipGenerator(obj)
            If IsNothing(cg) OrElse String.IsNullOrWhiteSpace(cg.AnimationName) Then Continue For
            clipGenBases.Add(System.IO.Path.GetFileNameWithoutExtension(cg.AnimationName))   ' nombre del clip-gen (resuelva o no)
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
            If perspective <> 1 Then clip.Is1stPersonOnly = False
        Next

        ' Referencias a otros behaviors (relativas al actor del behavior referenciante), MISMO SAPT/Role/eje.
        Dim behRoot = ActorRootOfAnim(behFile)
        For Each refObj In graph.GetObjectsByClassName("hkbBehaviorReferenceGenerator")
            Dim refName = graph.ResolveLocalString(refObj.RelativeOffset + &H88)
            If String.IsNullOrWhiteSpace(refName) Then Continue For
            EnumBehaviorClips(NormHkx(CombineActor(behRoot, refName)), saptFolders, role, stateAxis, reqFemale, perspective, loadBehaviorHkx,
                              animSet, clipGenBases, actorRoot, byClip, result, graphCache, visited, depth + 1)
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
        Dim pb = LoadFirstHkxCandidate(loadBehaviorHkx, NormHkx(proj))
        If pb Is Nothing Then Return ""
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(pb))
            For Each o In g.GetObjectsByClassName("hkbProjectStringData")
                Dim psd = g.ParseProjectStringData(o)
                If psd Is Nothing Then Continue For
                For Each cf In psd.CharacterFilenames
                    If String.IsNullOrWhiteSpace(cf) Then Continue For
                    Dim cb = LoadFirstHkxCandidate(loadBehaviorHkx, CombineActor(actorRoot, cf))
                    If cb Is Nothing Then cb = LoadFirstHkxCandidate(loadBehaviorHkx, cf)
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
        Dim projBytes = LoadFirstHkxCandidate(loadBehaviorHkx, NormHkx(rb.Project))
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
            Dim charBytes = LoadFirstHkxCandidate(loadBehaviorHkx, CombineActor(actorRoot, cf))
            If charBytes Is Nothing Then charBytes = LoadFirstHkxCandidate(loadBehaviorHkx, cf)
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


    ' Carpeta del clip relativa a "Animations\" (clave para propagar Role), canon lower. "" si cuelga directo o no hay marcador.
    Private Shared Function FolderRelKey(animFile As String) As String
        If String.IsNullOrWhiteSpace(animFile) Then Return ""
        Dim p = animFile.Replace("/"c, "\"c)
        Dim i = p.IndexOf("Animations\", StringComparison.OrdinalIgnoreCase)
        If i < 0 Then Return ""
        Dim rest = p.Substring(i + "Animations\".Length)
        Dim j = rest.LastIndexOf("\"c)
        Return If(j > 0, rest.Substring(0, j).ToLowerInvariant(), "")
    End Function

    ' ¿key bajo algún coverageDir (subárbol)? Camina los ancestros del dir de key buscando membresía (HashSet) — O(profundidad).
    Private Shared Function IsUnderCoverageDir(key As String, coverageDirs As HashSet(Of String)) As Boolean
        Dim d = key
        Dim j = d.LastIndexOf("\"c)
        d = If(j > 0, d.Substring(0, j), "")
        While d <> ""
            If coverageDirs.Contains(d) Then Return True
            Dim k = d.LastIndexOf("\"c)
            d = If(k > 0, d.Substring(0, k), "")
        End While
        Return False
    End Function

    ' Roles a propagar a una carpeta: la propia, si no el ancestro más cercano con Roles conocidos; vacío si ninguno.
    Private Shared Function RolesForFolder(folderRel As String, folderRoles As Dictionary(Of String, HashSet(Of String))) As IEnumerable(Of String)
        Dim f = folderRel
        While True
            Dim hs As HashSet(Of String) = Nothing
            If folderRoles.TryGetValue(f, hs) AndAlso hs.Count > 0 Then Return hs
            If f = "" Then Return Enumerable.Empty(Of String)()
            Dim k = f.LastIndexOf("\"c)
            f = If(k > 0, f.Substring(0, k), "")
        End While
        Return Enumerable.Empty(Of String)()
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
    Public Shared Function LoadFirstHkxCandidate(loader As Func(Of String, Byte()), path As String) As Byte()
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

    Private Shared Sub AddIdleClip(animFile As String, category As String, byClip As Dictionary(Of String, ResolvedAnimationClip), result As List(Of ResolvedAnimationClip))
        Dim clip As New ResolvedAnimationClip With {
            .AnimationFile = animFile,
            .ClipName = "",
            .PlaybackSpeed = 1.0F,
            .SourceSkeletonPath = ActorRootOfAnim(animFile) & "\CharacterAssets\skeleton.hkx",
            .FromBehaviorGraph = False,
            .RequiresFemale = False,
            .Is1stPersonOnly = (animFile.IndexOf("\_1stPerson\", StringComparison.OrdinalIgnoreCase) >= 0),
            .Category = category
        }
        byClip(animFile) = clip
        result.Add(clip)
    End Sub

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
    ''' <summary>Aditivo: el archivo de animación resuelto tiene hkaAnimationBinding.BlendHint &lt;&gt; 0
    ''' (1=ADDITIVE_DEPRECATED, 2=ADDITIVE; ambos = overlay, no pose standalone). Lo puebla
    ''' DetectAdditiveFlags (lazy, carga el archivo). El selector lo muestra con insignia ⊕.</summary>
    Public IsAdditive As Boolean = False
    ''' <summary>True hasta que se sabe que NO requiere additive-detection (guard de DetectAdditiveFlags
    ''' para no recargar archivos). Interno del pipeline lazy.</summary>
    Public AdditiveKnown As Boolean = False
    ''' <summary>True si el clip SOLO es alcanzable vía subgraphs de 1ª persona (SRAF.Perspective=1):
    ''' cámara/viewmodel (brazos del player), inútil para preview de NPC. Pasa a False en cuanto un
    ''' subgraph 3ª-persona o el root behavior (Perspective=none) lo alcanza. El selector lo oculta por
    ''' defecto. Análogo a RequiresFemale.</summary>
    Public Is1stPersonOnly As Boolean = True
    ''' <summary>False = clip "search-path-only": el archivo existe bajo una ruta SAPT de la raza pero NINGÚN
    ''' hkbClipGenerator estático lo referencia (variantes mood/archetype/gender + gestos/diálogo/special-idle
    ''' que el engine reproduce por evento en runtime). Lo agrega la pasada de cobertura por existencia; no trae
    ''' metadata de behavior (Roles se propagan por carpeta). True = vino del walk del behavior graph.</summary>
    Public FromBehaviorGraph As Boolean = True
    ''' <summary>Categoría semántica del clip cuando vino de un patrón IDLE (Talk_M/IdleDialogue/Listen/…); "" si no.</summary>
    Public Category As String = ""
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
