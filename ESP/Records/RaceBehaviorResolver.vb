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
    Friend Const KwTypeNone As UInteger = 0UI
    Friend Const KwTypeAnimGender As UInteger = 14UI
    Private Shared ReadOnly KeywordTypeNames As String() = {
        "None", "Component Tech Level", "Attach Point", "Component Property", "Instantiation Filter",
        "Mod Association", "Sound", "Anim Archetype", "Function Call", "Recipe Filter", "Attraction Type",
        "Dialogue Subtype", "Quest Target", "Anim Flavor", "Anim Gender", "Anim Face", "Quest Group",
        "Anim Injured", "Dispel Effect"}

    Private Shared _kwType As Dictionary(Of UInteger, UInteger)        ' KYWD FormID → TNAM Type
    Private Shared _raceIdentityKw As HashSet(Of UInteger)             ' keywords None-typed declaradas en ALGUNA KWDA de RACE
    Private Shared _kwMapsPm As PluginManager                          ' pm con el que se construyeron (rebuild si cambia)

    ''' <summary>Construye (una vez por pm) el mapa KYWD→tipo y el set de keywords de IDENTIDAD de raza (None-typed
    ''' ∧ presentes en la KWDA de alguna RACE). Idempotente; rebuild si cambia el pm. Llamado por ResolveRaceBehavior.</summary>
    Friend Shared Sub EnsureKeywordMaps(pm As PluginManager)
        If pm Is Nothing Then Return
        If _kwMapsPm Is pm AndAlso _kwType IsNot Nothing Then Return
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
        _kwType = kt : _raceIdentityKw = ident : _kwMapsPm = pm
    End Sub

    ''' <summary>Tipo (TNAM) de una keyword; 0 ('None') si no se conoce. Requiere EnsureKeywordMaps previo.</summary>
    Public Shared Function KeywordType(fid As UInteger) As UInteger
        Dim t As UInteger = 0
        If _kwType IsNot Nothing Then _kwType.TryGetValue(fid, t)
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
        Return _raceIdentityKw IsNot Nothing AndAlso _raceIdentityKw.Contains(fid)
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
        EnsureKeywordMaps(pm)   ' mapas KYWD-type + identidades-de-raza listos para el filtro type-driven de EnumerateClips
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
        Return result
    End Function

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
    ''' tiene su keyword propio → se filtran los clips por SAKD ∩ ActorKeywords. [[arch_race_behavior_resolution]]</summary>
    Public ReadOnly Property ActorKeywords As New List(Of UInteger)
    ''' <summary>Diagnóstico: "own" / "SRAC:0x… +SADD:0x…" — de dónde salieron los subgraphs.</summary>
    Public Property SubgraphSource As String = ""

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
