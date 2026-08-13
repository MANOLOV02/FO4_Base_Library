Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq

' =============================================================================
' Resuelve el árbol de behavior de un NPC/raza → los archivos .hkx (project +
' subgraphs) a cargar para enumerar/reproducir sus animaciones.
'
' HECHOS (verificados contra wbDefinitionsFO4.pas):
'  - El behavior es de la RAZA, NO del NPC. El record NPC_ no tiene campo de
'    behavior graph; está solo en RACE (Male/Female Behavior Graph + Subgraph Data)
'    y en IDLE (idles sueltos).
'  - NPC → raza efectiva: si el flag "Use Traits" (ACBS template flag bit 0) está y
'    el NPC tiene template (TPLT), la raza viene del template (recursivo); si no, la
'    RNAM propia. Use Traits trae la identidad (raza/género/voz/skin), de ahí el behavior.
'  - RACE → Male/Female Behavior Graph = project .hkx por gender; Subgraph Data = array
'    de behaviour graphs .hkx. Si el Subgraph Data propio está vacío y hay SRAC (Subgraph
'    Template Race), los subgraphs se HEREDAN de esa raza (caso humano: Raider→Humano);
'    SADD (Subgraph Additive Race) SUMA subgraphs encima.
'  - Las referencias internas del behavior usan .hkt, pero los archivos reales son .hkx
'    (en FO4 vanilla hay 0 archivos .hkt). DistinctBehaviorFiles normaliza .hkt→.hkx.
' =============================================================================

Public NotInheritable Class RaceBehaviorResolver
    Private Sub New()
    End Sub

    ' wbTemplateFlags bit 0 = "Use Traits" (incluye Race). Ver MainForm TraitsState.
    Private Const TemplateFlagUseTraits As UShort = &H1US

    ' ── KYWD.TNAM Type (wbDefinitionsFO4.pas:5213 wbKeywordTypeEnum) — discriminador AUTORITATIVO de los SAKD.
    '    'None'(0) = keyword de IDENTIDAD (ej 'Anims<X>Race', 'ActorType<X>'); 'Anim Injured'(17)/'Anim Archetype'(7)/
    '    'Anim Flavor'(13)/'Anim Gender'(14)/'Anim Face'(15) = EJES DE ESTADO runtime. NO se filtra por string.
    Public Const KwTypeNone As UInteger = 0UI
    Public Const KwTypeAnimGender As UInteger = 14UI
    Private Shared ReadOnly KeywordTypeNames As String() = {
        "None", "Component Tech Level", "Attach Point", "Component Property", "Instantiation Filter",
        "Mod Association", "Sound", "Anim Archetype", "Function Call", "Recipe Filter", "Attraction Type",
        "Dialogue Subtype", "Quest Target", "Anim Flavor", "Anim Gender", "Anim Face", "Quest Group",
        "Anim Injured", "Dispel Effect"}

    ' ⛔⛔ ESTOS CINCO SE ESCRIBEN DESDE DOS HILOS. `MainForm` corre `ResolveNpcBehavior` dentro de un
    ' `Task.Run` sobre todos los NPC (preload) y LA MISMA función en el hilo de UI cuando el usuario
    ' selecciona uno — y el preload arranca justo después de armar el árbol, que es cuando el usuario
    ' clickea. Un `Dictionary` de .NET escrito concurrentemente no se "ensucia": deja la cadena de buckets
    ' rota y `TryGetValue` se puede colgar en un loop infinito, o pierde entradas y la raza cae al fallback.
    ' El caché de la capa de ARRIBA (`MainForm._animRaceCache`) ya era `ConcurrentDictionary`: se protegió
    ' el de al lado y se olvidó el de la librería que lo alimenta.
    Private Shared _kwType As Dictionary(Of UInteger, UInteger)        ' KYWD FormID → TNAM Type
    Private Shared _raceIdentityKw As HashSet(Of UInteger)             ' keywords None-typed declaradas en ALGUNA KWDA de RACE
    Private Shared _parsedIdles As List(Of IDLE_Data)                  ' TODOS los IDLE parseados UNA vez (tabla global, race-independiente)
    ' El único que se escribe DESPUÉS de publicar los mapas ⇒ tiene que ser concurrente de por sí.
    Private Shared _rbCache As Concurrent.ConcurrentDictionary(Of UInteger, ResolvedRaceBehavior)
    Private Shared _kwMapsPm As PluginManager                          ' pm con el que se construyeron (rebuild si cambia)
    ' Los otros TRES son de sólo-lectura una vez construidos: se arman bajo este lock y se publican con
    ' `_kwMapsPm` ÚLTIMO y con escritura volátil, que es la barrera que hace visible al resto.
    Private Shared ReadOnly _mapsLock As New Object()

    ''' <summary>Construye (una vez por pm) el mapa KYWD→tipo y el set de keywords de IDENTIDAD de raza (None-typed
    ''' ∧ presentes en la KWDA de alguna RACE). Idempotente; rebuild si cambia el pm. Llamado por ResolveRaceBehavior.</summary>
    Public Shared Sub EnsureKeywordMaps(pm As PluginManager)
        If pm Is Nothing Then Return
        ' Camino rápido sin lock: `_kwMapsPm` se publica ÚLTIMO y con barrera, así que verlo igual a `pm`
        ' implica que los otros cuatro campos ya están completos y visibles.
        If Threading.Volatile.Read(_kwMapsPm) Is pm AndAlso _kwType IsNot Nothing Then Return
        SyncLock _mapsLock
            If Threading.Volatile.Read(_kwMapsPm) Is pm AndAlso _kwType IsNot Nothing Then Return
        Dim kt As New Dictionary(Of UInteger, UInteger)
        For Each rec In pm.GetRecordsOfType("KYWD")
            Dim t As UInteger = 0
            For Each sr In rec.Subrecords
                If sr.Signature = "TNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then t = BitConverter.ToUInt32(sr.Data, 0) : Exit For
            Next
            kt(rec.Header.FormID) = t
        Next
        Dim ident As New HashSet(Of UInteger)
        For Each rec In pm.GetRecordsOfType("RACE")
            Dim race As RACE_Data = Nothing
            Try : race = RecordParsers.ParseRACE(rec, pm) : Catch : Continue For : End Try
            If race Is Nothing Then Continue For
            For Each k In race.Keywords
                Dim tt As UInteger = 0
                If kt.TryGetValue(k, tt) AndAlso tt = KwTypeNone Then ident.Add(k)   ' None-typed ∧ declarada por una raza = identidad
            Next
        Next
        ' Parse de TODOS los records IDLE UNA sola vez (la tabla IDLE es global, no por-raza). ResolveRaceIdles solo
        ' FILTRA esta lista por condición de raza — evita re-parsear ~3691 IDLE (CTDA) en cada render.
        ' ⚠ GAME-AWARE: la cobertura IDLE es SOLO Fallout 4. En FO4 el IDLE.GNAM es un PATRÓN de archivo ($(Subgraph)+*)
        ' que apunta a .hkx sueltos NO referenciados por ningún clip-generator → hay que leerlos del record. En Skyrim
        ' (SSE) el IDLE es EVENT-DRIVEN: DNAM=behavior graph, ENAM=evento; el motor dispara el evento en el behavior y la
        ' state-machine resuelve el clip → esas animaciones YA están en el WALK (medido: cover=0 en las 161 razas SSE).
        ' Además el layout del record difiere (SSE no tiene GNAM) → NO intentar parsear GNAM en SSE.
        Dim idles As New List(Of IDLE_Data)
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
            For Each rec In pm.GetRecordsOfType("IDLE")
                Dim idle As IDLE_Data = Nothing
                Try : idle = QuestRecordParsers.ParseIDLE(rec, pm) : Catch : Continue For : End Try
                If idle IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(idle.AnimationFile) Then idles.Add(idle)
            Next
        End If
            Threading.Volatile.Write(_kwType, kt)
            Threading.Volatile.Write(_raceIdentityKw, ident)
            Threading.Volatile.Write(_parsedIdles, idles)
            Threading.Volatile.Write(_rbCache, New Concurrent.ConcurrentDictionary(Of UInteger, ResolvedRaceBehavior)())  ' nuevo pm → invalida el cache
            ' ⛔ ÚLTIMO Y CON BARRERA: es lo que hace visibles a los cuatro de arriba. Si se publicara
            ' antes (o sin `Volatile.Write`, que permite reordenar), otro hilo podría ver el pm nuevo y
            ' los mapas todavía a medio construir.
            Threading.Volatile.Write(_kwMapsPm, pm)
        End SyncLock
    End Sub

    ''' <summary>Tipo (TNAM) de una keyword; 0 ('None') si no se conoce. Requiere EnsureKeywordMaps previo.</summary>
    Public Shared Function KeywordType(fid As UInteger) As UInteger
        Dim t As UInteger = 0
        ' ⛔ LECTURA VOLATIL, igual que la publicacion. `_kwType` se arma en otro hilo y se publica con
        ' `Volatile.Write(_kwMapsPm, pm)` DESPUES de asignarlo; leerlo plano acá no toma esa barrera, asi
        ' que un hilo podia ver la referencia sin ver el contenido. Se captura UNA vez en un local: entre
        ' el chequeo de Nothing y el TryGetValue la referencia no puede cambiar bajo los pies.
        Dim mapa = Threading.Volatile.Read(_kwType)
        If mapa IsNot Nothing Then mapa.TryGetValue(fid, t)
        Return t
    End Function

    ''' <summary>Nombre del tipo TNAM (ej "Anim Injured"); "None" si desconocido.</summary>
    Public Shared Function KeywordTypeName(fid As UInteger) As String
        Dim t = KeywordType(fid)
        Return If(t < CUInt(KeywordTypeNames.Length), KeywordTypeNames(CInt(t)), $"Type{t}")
    End Function

    ''' <summary>¿Es keyword de IDENTIDAD de raza? = None-typed ∧ declarada en la KWDA de alguna RACE. Solo estas
    ''' discriminan entre actores (ej 'AnimsProtectronRace'); las de estado ('Anim Injured'…) NUNCA excluyen.</summary>
    Public Shared Function IsRaceIdentityKeyword(fid As UInteger) As Boolean
        Dim ident = Threading.Volatile.Read(_raceIdentityKw)
        Return ident IsNot Nothing AndAlso ident.Contains(fid)
    End Function

    ''' <summary>NPC → behavior de su raza efectiva (resolviendo Use Traits/TPLT).</summary>
    Public Shared Function ResolveNpcBehavior(npc As NPC_Data, pm As PluginManager) As ResolvedRaceBehavior
        If IsNothing(npc) OrElse IsNothing(pm) Then Return Nothing
        Dim raceFid = ResolveEffectiveRaceFormID(npc, pm, New HashSet(Of UInteger)())
        Dim result = ResolveRaceBehavior(raceFid, pm)
        If result IsNot Nothing Then result.IsFemale = npc.IsFemale
        Return result
    End Function

    ''' <summary>Raza efectiva: con Use Traits + template, sigue la cadena TPLT; si no, la RNAM propia.</summary>
    Private Shared Function ResolveEffectiveRaceFormID(npc As NPC_Data, pm As PluginManager, visited As HashSet(Of UInteger)) As UInteger
        If IsNothing(npc) Then Return 0UI
        If (npc.TemplateFlags And TemplateFlagUseTraits) <> 0US AndAlso npc.TemplateFormID <> 0UI AndAlso visited.Add(npc.TemplateFormID) Then
            Dim tmplRec = pm.GetRecord(npc.TemplateFormID)
            If tmplRec IsNot Nothing AndAlso tmplRec.Header.Signature = "NPC_" Then
                Dim tmpl = RecordParsers.ParseNPCLight(tmplRec, tmplRec.SourcePluginName, pm)
                If tmpl IsNot Nothing Then Return ResolveEffectiveRaceFormID(tmpl, pm, visited)
            End If
        End If
        Return npc.RaceFormID
    End Function

    ''' <summary>RACE → behavior: project por gender + subgraphs (propios, o heredados vía SRAC + SADD).</summary>
    Public Shared Function ResolveRaceBehavior(raceFormID As UInteger, pm As PluginManager) As ResolvedRaceBehavior
        If raceFormID = 0UI OrElse IsNothing(pm) Then Return Nothing
        EnsureKeywordMaps(pm)   ' mapas KYWD-type + identidades-de-raza + parse IDLE (1×/pm) listos para el filtro de EnumerateClips
        ' Cache por raza: ParseRACE (hasta 2601 subgraphs vía SRAC) + filtro IDLE se hacen UNA vez por raza, no por render.
        Dim cachedRb As ResolvedRaceBehavior = Nothing
        ' ⛔ SE CAPTURA LA REFERENCIA UNA VEZ. `EnsureKeywordMaps` REASIGNA `_rbCache` al cambiar de pm; si
        ' se leyera del campo dos veces, un hilo podia consultar el diccionario viejo y escribir en el nuevo
        ' (o al reves), guardando entradas calculadas con los mapas del pm ANTERIOR. Con un local, cada
        ' llamada usa un solo diccionario de punta a punta: si es el viejo, su resultado se descarta con el.
        Dim cacheRb = Threading.Volatile.Read(_rbCache)
        If cacheRb IsNot Nothing AndAlso cacheRb.TryGetValue(raceFormID, cachedRb) Then Return cachedRb
        Dim rec = pm.GetRecord(raceFormID)
        If IsNothing(rec) OrElse rec.Header.Signature <> "RACE" Then Return Nothing
        Dim race = RecordParsers.ParseRACE(rec, pm)
        If IsNothing(race) Then Return Nothing

        Dim result As New ResolvedRaceBehavior With {
            .RaceFormID = raceFormID,
            .RaceEditorID = race.EditorID,
            .MaleProject = race.MaleBehaviorGraphProject,
            .FemaleProject = race.FemaleBehaviorGraphProject,
            .MaleSkeleton = race.MaleSkeletonPath,
            .FemaleSkeleton = race.FemaleSkeletonPath
        }
        ' Keywords del race EFECTIVO (el 'Anims<X>Race' que filtra los subgraphs compartidos por SAKD).
        result.ActorKeywords.AddRange(race.Keywords)

        If race.SubgraphData.Count > 0 Then
            result.Subgraphs.AddRange(race.SubgraphData)
            result.SubgraphSource = "own"
        ElseIf race.SubgraphTemplateRaceFormID <> 0UI Then
            result.Subgraphs.AddRange(ResolveRaceSubgraphs(race.SubgraphTemplateRaceFormID, pm, New HashSet(Of UInteger)()))
            result.SubgraphSource = "SRAC:0x" & race.SubgraphTemplateRaceFormID.ToString("X8")
        End If
        If race.SubgraphAdditiveRaceFormID <> 0UI Then
            result.Subgraphs.AddRange(ResolveRaceSubgraphs(race.SubgraphAdditiveRaceFormID, pm, New HashSet(Of UInteger)()))
            result.SubgraphSource = (result.SubgraphSource & " +SADD:0x" & race.SubgraphAdditiveRaceFormID.ToString("X8")).Trim()
        End If
        ResolveRaceIdles(result)
        If cacheRb IsNot Nothing Then cacheRb(raceFormID) = result   ' el MISMO que se consulto arriba
        Return result
    End Function

    ''' <summary>Resuelve los patrones IDLE aplicables a la raza (gestos/poses/turns que ningún clip-generator
    ''' referencia). NO filtra por DNAM acá (eso lo hace el enumerador contra el set COMPLETO de behaviors caminados,
    ''' que incluye los alcanzados por hkbBehaviorReferenceGenerator). Acá solo gatea por condiciones GetIsRace (CTDA).
    ''' Categoría = el evento ENAM del propio record (campo autoritativo, sin manipular strings del filename).</summary>
    Private Shared Sub ResolveRaceIdles(rb As ResolvedRaceBehavior)
        ' ⛔ UNA sola lectura, con barrera. Era el unico de los cinco campos sin `Volatile` y ademas se
        ' leia DOS veces del campo: el `Is Nothing` podia mirar una instancia y el `For Each` otra.
        Dim idles = Threading.Volatile.Read(_parsedIdles)
        If idles Is Nothing Then Return
        For Each idle In idles   ' parseados 1×/pm en EnsureKeywordMaps — acá SOLO se filtra por raza (barato)
            ' GetIsRace: si hay condiciones positivas, alguna debe ser esta raza; si hay una negativa para esta raza, excluir.
            If idle.RaceConditions.Count > 0 Then
                Dim pos = idle.RaceConditions.Where(Function(c) c.Positive).ToList()
                If idle.RaceConditions.Any(Function(c) Not c.Positive AndAlso c.RaceFormID = rb.RaceFormID) Then Continue For
                If pos.Count > 0 AndAlso Not pos.Any(Function(c) c.RaceFormID = rb.RaceFormID) Then Continue For
            End If
            rb.IdleAnimations.Add(New RaceIdleAnimation With {
                .GnamPattern = idle.AnimationFile,
                .Category = If(idle.AnimationEvent, ""),
                .DnamBasename = If(String.IsNullOrWhiteSpace(idle.BehaviorGraph), "", System.IO.Path.GetFileNameWithoutExtension(idle.BehaviorGraph))})
        Next
    End Sub

    ' Subgraphs de una raza referenciada por SRAC/SADD: propios, o recursivamente su propio SRAC.
    Private Shared Function ResolveRaceSubgraphs(raceFormID As UInteger, pm As PluginManager, visited As HashSet(Of UInteger)) As List(Of RACE_SubgraphData)
        Dim r As New List(Of RACE_SubgraphData)
        If raceFormID = 0UI OrElse Not visited.Add(raceFormID) Then Return r
        Dim rec = pm.GetRecord(raceFormID)
        If IsNothing(rec) OrElse rec.Header.Signature <> "RACE" Then Return r
        Dim race = RecordParsers.ParseRACE(rec, pm)
        If IsNothing(race) Then Return r
        If race.SubgraphData.Count > 0 Then
            r.AddRange(race.SubgraphData)
        ElseIf race.SubgraphTemplateRaceFormID <> 0UI Then
            r.AddRange(ResolveRaceSubgraphs(race.SubgraphTemplateRaceFormID, pm, visited))
        End If
        Return r
    End Function
End Class

''' <summary>Behavior resuelto de una raza/NPC: project + skeleton (por gender) + subgraphs y la lista
''' de .hkx distintos a cargar. Los clips reproducibles salen de parsear esos .hkx con el behavior parser.</summary>
Public Class ResolvedRaceBehavior
    Public RaceFormID As UInteger
    Public RaceEditorID As String = ""
    Public IsFemale As Boolean
    Public MaleProject As String = ""
    Public FemaleProject As String = ""
    Public MaleSkeleton As String = ""
    Public FemaleSkeleton As String = ""
    Public ReadOnly Property Subgraphs As New List(Of RACE_SubgraphData)
    ''' <summary>Keywords del RACE EFECTIVO (no del SRAC template). Contienen el 'Anims&lt;X&gt;Race' que
    ''' discrimina qué subgraph (SAKD) aplica a este robot. Robots comparten subgraphs vía SRAC pero cada race
    ''' tiene su keyword propio → se filtran los clips por SAKD ∩ ActorKeywords. [[24-anim-behavior-por-raza]]</summary>
    Public ReadOnly Property ActorKeywords As New List(Of UInteger)
    ''' <summary>Diagnóstico: "own" / "SRAC:0x… +SADD:0x…" — de dónde salieron los subgraphs.</summary>
    Public Property SubgraphSource As String = ""
    ''' <summary>Patrones IDLE.GNAM aplicables a esta raza (DNAM ∈ behaviors de la raza ∧ no excluida por GetIsRace).
    ''' El enumerador los expande ($(Subgraph)→carpetas SAPT, *→glob) para mapear el pool de gestos/poses/turns.</summary>
    Public ReadOnly Property IdleAnimations As New List(Of RaceIdleAnimation)

    ''' <summary>Project .hkx del gender resuelto (fallback al otro gender si falta).</summary>
    Public ReadOnly Property Project As String
        Get
            Return If(IsFemale, If(FemaleProject <> "", FemaleProject, MaleProject), If(MaleProject <> "", MaleProject, FemaleProject))
        End Get
    End Property

    ''' <summary>Skeleton del gender resuelto.</summary>
    Public ReadOnly Property Skeleton As String
        Get
            Return If(IsFemale, If(FemaleSkeleton <> "", FemaleSkeleton, MaleSkeleton), If(MaleSkeleton <> "", MaleSkeleton, FemaleSkeleton))
        End Get
    End Property

    ''' <summary>Archivos .hkx DISTINTOS a cargar (project + subgraphs), con .hkt→.hkx normalizado.
    ''' Cargar cada uno con el behavior parser → enumerar todos los hkbClipGenerator (clips).</summary>
    Public Function DistinctBehaviorFiles() As List(Of String)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim result As New List(Of String)
        Dim all = New List(Of String) From {Project}
        all.AddRange(Subgraphs.Select(Function(s) s.BehaviourGraph))
        For Each p In all
            Dim f = NormalizeBehaviorPath(p)
            If f <> "" AndAlso seen.Add(f) Then result.Add(f)
        Next
        Return result
    End Function

    ' Las refs internas del behavior usan .hkt pero los archivos reales son .hkx (FO4 vanilla: 0 .hkt).
    Private Shared Function NormalizeBehaviorPath(p As String) As String
        If String.IsNullOrWhiteSpace(p) Then Return ""
        If p.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then Return p.Substring(0, p.Length - 4) & ".hkx"
        Return p
    End Function
End Class

''' <summary>Un patrón IDLE.GNAM + el evento ENAM (categoría, campo del record) + el basename del DNAM (behavior).
''' El enumerador expande GnamPattern ($(Subgraph)→carpetas SAPT, *→glob) gateando los patrones $(Subgraph) por
''' DnamBasename ∈ behaviors REALMENTE caminados.</summary>
Public Class RaceIdleAnimation
    Public Property GnamPattern As String = ""
    Public Property Category As String = ""      ' = IDLE.ENAM (Animation Event), campo autoritativo del record
    Public Property DnamBasename As String = ""  ' = basename de IDLE.DNAM (Behavior Graph), "" si no tiene
End Class
