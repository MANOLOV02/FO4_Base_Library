Imports System.Text

' ============================================================================
' Quest / Dialogue / AI Record Data Classes and Parsers
' QUST, DIAL, INFO, PACK, SCEN, IDLE, DLBR, DLVW, SMBN, SMEN, SMQN
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Quest stage entry.</summary>
Public Class QUST_Stage
    Public StageIndex As UShort
    Public StageFlags As Byte
    Public LogEntry As String = ""
End Class

''' <summary>Quest objective entry.</summary>
Public Class QUST_Objective
    Public ObjectiveIndex As UShort
    Public ObjectiveFlags As UInteger
    Public DisplayText As String = ""
End Class

''' <summary>Quest alias entry.</summary>
Public Class QUST_Alias
    Public AliasID As Integer
    Public AliasName As String = ""
    Public AliasFlags As UInteger
    Public ForcedRefFormID As UInteger
    Public UniqueActorFormID As UInteger
    Public KeywordFormID As UInteger
    Public IsLocation As Boolean
    Public SpecificLocationFormID As UInteger
End Class

''' <summary>Fallout 4 QUST record - Quest.</summary>
Public Class QUST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""

    ' DNAM
    Public QuestFlags As UShort
    Public Priority As Byte
    Public DelayTime As Single
    Public QuestType As Byte

    ' Event
    Public EventType As UInteger

    Public LocationFormID As UInteger
    Public CompletionXPFormID As UInteger
    Public QuestGroupFormID As UInteger
    Public SWFFile As String = ""

    Public Stages As New List(Of QUST_Stage)
    Public Objectives As New List(Of QUST_Objective)
    Public Aliases As New List(Of QUST_Alias)

    Public ReadOnly Property IsStartGameEnabled As Boolean
        Get
            Return (QuestFlags And &H1US) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 DIAL record - Dialog Topic.</summary>
Public Class DIAL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Priority As Single = 50.0F
    Public BranchFormID As UInteger
    Public QuestFormID As UInteger
    Public KeywordFormID As UInteger
    Public TopicFlags As Byte
    Public Category As Byte
    Public Subtype As UShort
    Public SubtypeName As String = ""
    Public InfoCount As UInteger
End Class

''' <summary>Dialog response entry.</summary>
Public Class INFO_Response
    Public ResponseNumber As Byte
    Public EmotionFormID As UInteger
    Public SoundFileFormID As UInteger
    Public ResponseText As String = ""
    Public ScriptNotes As String = ""
End Class

''' <summary>Fallout 4 INFO record - Dialog Response.</summary>
Public Class INFO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ResponseFlags As UShort
    Public ResetHours As UShort
    Public PreviousTopicFormID As UInteger
    Public PreviousInfoFormID As UInteger
    Public SharedInfoFormID As UInteger
    Public InfoGroupFormID As UInteger
    Public Prompt As String = ""
    Public SpeakerFormID As UInteger
    Public StartSceneFormID As UInteger
    Public AudioOutputOverrideFormID As UInteger
    Public GreetDistance As UInteger

    Public Responses As New List(Of INFO_Response)
End Class

''' <summary>Fallout 4 PACK record - Package (AI package).</summary>
Public Class PACK_Data
    Public FormID As UInteger
    Public EditorID As String = ""

    ' PKDT
    Public GeneralFlags As UInteger
    Public PackageType As Byte
    Public InterruptOverride As Byte
    Public PreferredSpeed As Byte
    Public InterruptFlags As UShort

    ' PSDT schedule
    Public ScheduleMonth As SByte
    Public ScheduleDayOfWeek As Byte
    Public ScheduleDate As Byte
    Public ScheduleHour As SByte
    Public ScheduleMinute As Byte
    Public ScheduleDuration As UInteger

    ' References
    Public IdleAnimationFormID As UInteger
    Public CombatStyleFormID As UInteger
    Public OwnerQuestFormID As UInteger
    Public PackageTemplateFormID As UInteger
End Class

''' <summary>Fallout 4 SCEN record - Scene (simplified).</summary>
Public Class SCEN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentQuestFormID As UInteger
    Public SceneFlags As UInteger
    Public LastActionIndex As UInteger
    Public Notes As String = ""
    Public TemplateSceneFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 IDLE record - Idle Animation.</summary>
Public Class IDLE_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public BehaviorGraph As String = ""
    Public AnimationEvent As String = ""
    Public AnimationFile As String = ""
    Public ParentFormID As UInteger
    Public PreviousFormID As UInteger

    ' DATA
    Public LoopingMin As Byte
    Public LoopingMax As Byte
    Public IdleFlags As Byte
    Public AnimGroupSection As Byte
    Public ReplayDelay As UShort
End Class

''' <summary>Fallout 4 DLBR record - Dialog Branch.</summary>
Public Class DLBR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public QuestFormID As UInteger
    Public CategoryFlags As UInteger
    Public StartingTopicFormID As UInteger
End Class

''' <summary>Fallout 4 DLVW record - Dialog View.</summary>
Public Class DLVW_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public QuestFormID As UInteger
    Public BranchFormIDs As New List(Of UInteger)
    Public TopicFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 SMBN record - Story Manager Branch Node.</summary>
Public Class SMBN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentFormID As UInteger
    Public PreviousSiblingFormID As UInteger
    Public NodeFlags As UInteger
End Class

''' <summary>Fallout 4 SMEN record - Story Manager Event Node.</summary>
Public Class SMEN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentFormID As UInteger
    Public PreviousSiblingFormID As UInteger
    Public NodeFlags As UInteger
    Public EventType As UInteger
End Class

''' <summary>Fallout 4 SMQN record - Story Manager Quest Node.</summary>
Public Class SMQN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentFormID As UInteger
    Public PreviousSiblingFormID As UInteger
    Public NodeFlags As UInteger
    Public QuestFormIDs As New List(Of UInteger)
    Public MaxConcurrentQuests As UInteger
    Public MaxNumQuestsToRun As UInteger
End Class

#End Region

#Region "Parsers"

Public Module QuestRecordParsers

    Public Function ParseQUST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As QUST_Data
        Dim q As New QUST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentStage As QUST_Stage = Nothing
        Dim currentObjective As QUST_Objective = Nothing
        Dim currentAlias As QUST_Alias = Nothing
        Dim inAliasSection As Boolean = False

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    q.FullName = ResolveStr(rec, sr, pluginManager)
                Case "NNAM"
                    q.Description = sr.AsString
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        q.QuestFlags = BitConverter.ToUInt16(sr.Data, 0)
                        q.Priority = sr.Data(2)
                        q.DelayTime = BitConverter.ToSingle(sr.Data, 4)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 9 Then
                        q.QuestType = sr.Data(8)
                    End If
                Case "ENAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        q.EventType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "LNAM"
                    q.LocationFormID = ResolveFID(rec, sr, pluginManager)
                Case "XNAM"
                    q.CompletionXPFormID = ResolveFID(rec, sr, pluginManager)
                Case "GNAM"
                    q.QuestGroupFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    q.SWFFile = sr.AsString

                ' Stages
                Case "INDX"
                    If currentStage IsNot Nothing Then q.Stages.Add(currentStage)
                    currentStage = New QUST_Stage()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        currentStage.StageIndex = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 3 Then
                        currentStage.StageFlags = sr.Data(2)
                    End If
                Case "CNAM"
                    If currentStage IsNot Nothing Then
                        currentStage.LogEntry = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                    End If

                ' Objectives
                Case "QOBJ"
                    If currentObjective IsNot Nothing Then q.Objectives.Add(currentObjective)
                    If currentStage IsNot Nothing Then
                        q.Stages.Add(currentStage)
                        currentStage = Nothing
                    End If
                    currentObjective = New QUST_Objective()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        currentObjective.ObjectiveIndex = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "FNAM"
                    If currentObjective IsNot Nothing AndAlso Not inAliasSection Then
                        If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                            currentObjective.ObjectiveFlags = BitConverter.ToUInt32(sr.Data, 0)
                        End If
                    End If

                ' Aliases
                Case "ANAM"
                    ' Next Alias ID marker - start of alias section
                    If currentObjective IsNot Nothing Then
                        q.Objectives.Add(currentObjective)
                        currentObjective = Nothing
                    End If
                    inAliasSection = True
                Case "ALST"
                    If currentAlias IsNot Nothing Then q.Aliases.Add(currentAlias)
                    currentAlias = New QUST_Alias()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        currentAlias.AliasID = BitConverter.ToInt32(sr.Data, 0)
                    End If
                Case "ALLS"
                    If currentAlias IsNot Nothing Then q.Aliases.Add(currentAlias)
                    currentAlias = New QUST_Alias With {.IsLocation = True}
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        currentAlias.AliasID = BitConverter.ToInt32(sr.Data, 0)
                    End If
                Case "ALID"
                    If currentAlias IsNot Nothing Then currentAlias.AliasName = sr.AsString
                Case "ALFR"
                    If currentAlias IsNot Nothing Then currentAlias.ForcedRefFormID = ResolveFID(rec, sr, pluginManager)
                Case "ALUA"
                    If currentAlias IsNot Nothing Then currentAlias.UniqueActorFormID = ResolveFID(rec, sr, pluginManager)
                Case "KNAM"
                    If currentAlias IsNot Nothing Then currentAlias.KeywordFormID = ResolveFID(rec, sr, pluginManager)
                Case "ALFL"
                    If currentAlias IsNot Nothing Then currentAlias.SpecificLocationFormID = ResolveFID(rec, sr, pluginManager)
                Case "ALED"
                    If currentAlias IsNot Nothing Then
                        q.Aliases.Add(currentAlias)
                        currentAlias = Nothing
                    End If
            End Select
        Next

        If currentStage IsNot Nothing Then q.Stages.Add(currentStage)
        If currentObjective IsNot Nothing Then q.Objectives.Add(currentObjective)
        If currentAlias IsNot Nothing Then q.Aliases.Add(currentAlias)
        Return q
    End Function

    Public Function ParseDIAL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DIAL_Data
        Dim d As New DIAL_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    d.FullName = ResolveStr(rec, sr, pluginManager)
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        d.Priority = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "BNAM"
                    d.BranchFormID = ResolveFID(rec, sr, pluginManager)
                Case "QNAM"
                    d.QuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "KNAM"
                    d.KeywordFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        d.TopicFlags = sr.Data(0)
                        d.Category = sr.Data(1)
                        d.Subtype = BitConverter.ToUInt16(sr.Data, 2)
                    End If
                Case "SNAM"
                    d.SubtypeName = sr.AsString
                Case "TIFC"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        d.InfoCount = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return d
    End Function

    Public Function ParseINFO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As INFO_Data
        Dim info As New INFO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentResponse As INFO_Response = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "ENAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        info.ResponseFlags = BitConverter.ToUInt16(sr.Data, 0)
                        info.ResetHours = BitConverter.ToUInt16(sr.Data, 2)
                    End If
                Case "TPIC"
                    info.PreviousTopicFormID = ResolveFID(rec, sr, pluginManager)
                Case "PNAM"
                    info.PreviousInfoFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    info.SharedInfoFormID = ResolveFID(rec, sr, pluginManager)
                Case "GNAM"
                    info.InfoGroupFormID = ResolveFID(rec, sr, pluginManager)
                Case "RNAM"
                    info.Prompt = ResolveStr(rec, sr, pluginManager)
                Case "ANAM"
                    info.SpeakerFormID = ResolveFID(rec, sr, pluginManager)
                Case "TSCE"
                    info.StartSceneFormID = ResolveFID(rec, sr, pluginManager)
                Case "ONAM"
                    info.AudioOutputOverrideFormID = ResolveFID(rec, sr, pluginManager)
                Case "GREE"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        info.GreetDistance = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "TRDA"
                    If currentResponse IsNot Nothing Then info.Responses.Add(currentResponse)
                    currentResponse = New INFO_Response()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then
                        currentResponse.EmotionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                        currentResponse.ResponseNumber = sr.Data(4)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 9 Then
                        currentResponse.SoundFileFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 5), pluginManager)
                    End If
                Case "NAM1"
                    If currentResponse IsNot Nothing Then
                        currentResponse.ResponseText = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.ILStrings)
                    End If
                Case "NAM2"
                    If currentResponse IsNot Nothing Then currentResponse.ScriptNotes = sr.AsString
            End Select
        Next

        If currentResponse IsNot Nothing Then info.Responses.Add(currentResponse)
        Return info
    End Function

    Public Function ParsePACK(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As PACK_Data
        Dim p As New PACK_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PKDT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        p.GeneralFlags = BitConverter.ToUInt32(sr.Data, 0)
                        p.PackageType = sr.Data(4)
                        p.InterruptOverride = sr.Data(5)
                        p.PreferredSpeed = sr.Data(6)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 10 Then
                        p.InterruptFlags = BitConverter.ToUInt16(sr.Data, 8)
                    End If
                Case "PSDT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 9 Then
                        p.ScheduleMonth = CSByte(sr.Data(0))
                        p.ScheduleDayOfWeek = sr.Data(1)
                        p.ScheduleDate = sr.Data(2)
                        p.ScheduleHour = CSByte(sr.Data(3))
                        p.ScheduleMinute = sr.Data(4)
                        p.ScheduleDuration = BitConverter.ToUInt32(sr.Data, 5)
                    End If
                Case "INAM"
                    p.IdleAnimationFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNAM"
                    p.CombatStyleFormID = ResolveFID(rec, sr, pluginManager)
                Case "QNAM"
                    p.OwnerQuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "PKCU"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        p.PackageTemplateFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                    End If
            End Select
        Next

        Return p
    End Function

    Public Function ParseSCEN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SCEN_Data
        Dim s As New SCEN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PNAM"
                    s.ParentQuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.SceneFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "INAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.LastActionIndex = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "NNAM"
                    s.Notes = sr.AsString
                Case "TNAM"
                    s.TemplateSceneFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, s.KeywordFormIDs)
            End Select
        Next

        Return s
    End Function

    Public Function ParseIDLE(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As IDLE_Data
        Dim idle As New IDLE_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DNAM"
                    idle.BehaviorGraph = sr.AsString
                Case "ENAM"
                    idle.AnimationEvent = sr.AsString
                Case "GNAM"
                    idle.AnimationFile = sr.AsString
                Case "ANAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        idle.ParentFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                        idle.PreviousFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                    End If
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        idle.LoopingMin = sr.Data(0)
                        idle.LoopingMax = sr.Data(1)
                        idle.IdleFlags = sr.Data(2)
                        idle.AnimGroupSection = sr.Data(3)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 6 Then
                        idle.ReplayDelay = BitConverter.ToUInt16(sr.Data, 4)
                    End If
            End Select
        Next

        Return idle
    End Function

    Public Function ParseDLBR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DLBR_Data
        Dim d As New DLBR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "QNAM"
                    d.QuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        d.CategoryFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "SNAM"
                    d.StartingTopicFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next

        Return d
    End Function

    Public Function ParseDLVW(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DLVW_Data
        Dim d As New DLVW_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "QNAM"
                    d.QuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "BNAM"
                    d.BranchFormIDs.Add(ResolveFID(rec, sr, pluginManager))
                Case "TNAM"
                    d.TopicFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next

        Return d
    End Function

    Public Function ParseSMBN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SMBN_Data
        Dim s As New SMBN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PNAM"
                    s.ParentFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    s.PreviousSiblingFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.NodeFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseSMEN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SMEN_Data
        Dim s As New SMEN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PNAM"
                    s.ParentFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    s.PreviousSiblingFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.NodeFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "ENAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.EventType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseSMQN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SMQN_Data
        Dim s As New SMQN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PNAM"
                    s.ParentFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    s.PreviousSiblingFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.NodeFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "NNAM"
                    s.QuestFormIDs.Add(ResolveFID(rec, sr, pluginManager))
                Case "MNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.MaxConcurrentQuests = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "QNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.MaxNumQuestsToRun = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return s
    End Function

End Module

#End Region
