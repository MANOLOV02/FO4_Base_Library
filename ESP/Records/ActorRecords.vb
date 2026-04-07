Imports System.Drawing
Imports System.Text

' ============================================================================
' Actor / Character Related Record Data Classes and Parsers
' FACT, CLAS, EYES, BPTD, MOVT, CSTY, VTYP, RELA
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Faction rank entry.</summary>
Public Class FACT_Rank
    Public RankNumber As UInteger
    Public MaleTitle As String = ""
    Public FemaleTitle As String = ""
End Class

''' <summary>Faction relation entry.</summary>
Public Class FACT_Relation
    Public FactionFormID As UInteger
    Public Modifier As Integer
    Public GroupCombatReaction As UInteger
End Class

''' <summary>Fallout 4 FACT record - Faction.</summary>
Public Class FACT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Flags As UInteger
    Public Relations As New List(Of FACT_Relation)
    Public Ranks As New List(Of FACT_Rank)

    ' Crime values
    Public Arrest As Boolean
    Public AttackOnSight As Boolean
    Public MurderCrimeGold As UShort
    Public AssaultCrimeGold As UShort
    Public TrespassCrimeGold As UShort
    Public PickpocketCrimeGold As UShort
    Public StealMultiplier As Single
    Public EscapeCrimeGold As UShort

    ' Vendor
    Public VendorBuySellListFormID As UInteger
    Public MerchantContainerFormID As UInteger
    Public VendorStartHour As UShort
    Public VendorEndHour As UShort
    Public VendorRadius As UShort
    Public VendorBuysStolenItems As Boolean
    Public VendorBuysNonStolenItems As Boolean

    ' Jail outfit
    Public JailOutfitFormID As UInteger

    Public ReadOnly Property IsHiddenFromNPC As Boolean
        Get
            Return (Flags And &H1UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsVendor As Boolean
        Get
            Return (Flags And &H4000UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 CLAS record - Class.</summary>
Public Class CLAS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public IconPath As String = ""
    Public BleedoutDefault As Single
End Class

''' <summary>Fallout 4 EYES record - Eyes.</summary>
Public Class EYES_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public TexturePath As String = ""
    Public EyeFlags As Byte

    Public ReadOnly Property IsPlayable As Boolean
        Get
            Return (EyeFlags And &H1) <> 0
        End Get
    End Property

    Public ReadOnly Property IsNotMale As Boolean
        Get
            Return (EyeFlags And &H2) <> 0
        End Get
    End Property

    Public ReadOnly Property IsNotFemale As Boolean
        Get
            Return (EyeFlags And &H4) <> 0
        End Get
    End Property
End Class

''' <summary>Body part node entry.</summary>
Public Class BPTD_Part
    Public PartName As String = ""
    Public NodeName As String = ""
    Public VATSTarget As String = ""
    Public LimbReplacementModel As String = ""
    Public GoreTargetBone As String = ""
    Public TwistVariablePrefix As String = ""
    Public DismemberBloodArtFormID As UInteger
    Public BloodImpactMaterialFormID As UInteger
    Public OnCrippleBloodImpactFormID As UInteger
    Public MeatCapTextureSetFormID As UInteger
    Public CollarTextureSetFormID As UInteger
End Class

''' <summary>Fallout 4 BPTD record - Body Part Data.</summary>
Public Class BPTD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public Parts As New List(Of BPTD_Part)
End Class

''' <summary>Fallout 4 MOVT record - Movement Type.</summary>
Public Class MOVT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public MovementName As String = ""
    Public FloatHeight As Single
    Public FlightAngleGain As Single = 0.1F

    ' SPED speeds (simplified)
    Public LeftWalk As Single
    Public LeftRun As Single
    Public RightWalk As Single
    Public RightRun As Single
    Public ForwardWalk As Single
    Public ForwardRun As Single
    Public BackWalk As Single
    Public BackRun As Single
End Class

''' <summary>Fallout 4 CSTY record - Combat Style.</summary>
Public Class CSTY_Data
    Public FormID As UInteger
    Public EditorID As String = ""

    ' CSGD General
    Public OffensiveMult As Single
    Public DefensiveMult As Single
    Public GroupOffensiveMult As Single
    Public EquipScoreMelee As Single
    Public EquipScoreMagic As Single
    Public EquipScoreRanged As Single
    Public EquipScoreShout As Single
    Public EquipScoreUnarmed As Single
    Public EquipScoreStaff As Single
    Public AvoidThreatChance As Single
    Public DodgeChance As Single
    Public EvadeChance As Single

    ' CSRA
    Public RangedAccuracyMult As Single

    ' DATA flags
    Public StyleFlags As UInteger

    Public ReadOnly Property AllowDualWielding As Boolean
        Get
            Return (StyleFlags And &H4UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 VTYP record - Voice Type.</summary>
Public Class VTYP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public VoiceFlags As Byte

    Public ReadOnly Property AllowDefaultDialog As Boolean
        Get
            Return (VoiceFlags And &H1) <> 0
        End Get
    End Property

    Public ReadOnly Property IsFemale As Boolean
        Get
            Return (VoiceFlags And &H2) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 RELA record - Relationship.</summary>
Public Class RELA_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentNPCFormID As UInteger
    Public ChildNPCFormID As UInteger
    Public Rank As Byte  ' 0=Lover, 1=Ally, 2=Confidant, 3=Friend, 4=Acquaintance

    Public ReadOnly Property RankName As String
        Get
            Select Case Rank
                Case 0 : Return "Lover"
                Case 1 : Return "Ally"
                Case 2 : Return "Confidant"
                Case 3 : Return "Friend"
                Case 4 : Return "Acquaintance"
                Case Else : Return $"Unknown({Rank})"
            End Select
        End Get
    End Property
End Class

#End Region

#Region "Parsers"

Public Module ActorRecordParsers

    Public Function ParseFACT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FACT_Data
        Dim f As New FACT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentRank As FACT_Rank = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    f.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        f.Flags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "RELA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        f.Relations.Add(New FACT_Relation With {
                            .FactionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager),
                            .Modifier = BitConverter.ToInt32(sr.Data, 4),
                            .GroupCombatReaction = BitConverter.ToUInt32(sr.Data, 8)
                        })
                    End If
                Case "CRVA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        f.Arrest = sr.Data(0) <> 0
                        f.AttackOnSight = sr.Data(1) <> 0
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        f.MurderCrimeGold = BitConverter.ToUInt16(sr.Data, 2)
                        f.AssaultCrimeGold = BitConverter.ToUInt16(sr.Data, 4)
                        f.TrespassCrimeGold = BitConverter.ToUInt16(sr.Data, 6)
                        f.PickpocketCrimeGold = BitConverter.ToUInt16(sr.Data, 8)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        f.StealMultiplier = BitConverter.ToSingle(sr.Data, 12)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 18 Then
                        f.EscapeCrimeGold = BitConverter.ToUInt16(sr.Data, 16)
                    End If
                Case "JOUT"
                    f.JailOutfitFormID = ResolveFID(rec, sr, pluginManager)
                Case "VEND"
                    f.VendorBuySellListFormID = ResolveFID(rec, sr, pluginManager)
                Case "VENC"
                    f.MerchantContainerFormID = ResolveFID(rec, sr, pluginManager)
                Case "VENV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 11 Then
                        f.VendorStartHour = BitConverter.ToUInt16(sr.Data, 0)
                        f.VendorEndHour = BitConverter.ToUInt16(sr.Data, 2)
                        f.VendorRadius = BitConverter.ToUInt16(sr.Data, 4)
                        f.VendorBuysStolenItems = sr.Data(8) <> 0
                        f.VendorBuysNonStolenItems = sr.Data(10) <> 0
                    End If
                Case "RNAM"
                    If currentRank IsNot Nothing Then f.Ranks.Add(currentRank)
                    currentRank = New FACT_Rank()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        currentRank.RankNumber = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MNAM"
                    If currentRank IsNot Nothing Then
                        currentRank.MaleTitle = ResolveStr(rec, sr, pluginManager)
                    End If
                Case "FNAM"
                    If currentRank IsNot Nothing Then
                        currentRank.FemaleTitle = ResolveStr(rec, sr, pluginManager)
                    End If
            End Select
        Next

        If currentRank IsNot Nothing Then f.Ranks.Add(currentRank)
        Return f
    End Function

    Public Function ParseCLAS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CLAS_Data
        Dim c As New CLAS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    c.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    c.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "ICON"
                    c.IconPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        c.BleedoutDefault = BitConverter.ToSingle(sr.Data, 4)
                    End If
            End Select
        Next

        Return c
    End Function

    Public Function ParseEYES(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As EYES_Data
        Dim e As New EYES_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    e.FullName = ResolveStr(rec, sr, pluginManager)
                Case "ICON"
                    e.TexturePath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        e.EyeFlags = sr.Data(0)
                    End If
            End Select
        Next

        Return e
    End Function

    Public Function ParseBPTD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As BPTD_Data
        Dim b As New BPTD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentPart As BPTD_Part = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MOD2"
                    If b.ModelPath = "" Then b.ModelPath = sr.AsString
                Case "BPTN"
                    If currentPart IsNot Nothing Then b.Parts.Add(currentPart)
                    currentPart = New BPTD_Part With {
                        .PartName = ResolveStr(rec, sr, pluginManager)
                    }
                Case "BPNN"
                    If currentPart IsNot Nothing Then currentPart.NodeName = sr.AsString
                Case "BPNT"
                    If currentPart IsNot Nothing Then currentPart.VATSTarget = sr.AsString
                Case "NAM1"
                    If currentPart IsNot Nothing Then currentPart.LimbReplacementModel = sr.AsString
                Case "NAM4"
                    If currentPart IsNot Nothing Then currentPart.GoreTargetBone = sr.AsString
                Case "BNAM"
                    If currentPart IsNot Nothing Then currentPart.DismemberBloodArtFormID = ResolveFID(rec, sr, pluginManager)
                Case "INAM"
                    If currentPart IsNot Nothing Then currentPart.BloodImpactMaterialFormID = ResolveFID(rec, sr, pluginManager)
                Case "JNAM"
                    If currentPart IsNot Nothing Then currentPart.OnCrippleBloodImpactFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNAM"
                    If currentPart IsNot Nothing Then currentPart.MeatCapTextureSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "NAM2"
                    If currentPart IsNot Nothing Then currentPart.CollarTextureSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    If currentPart IsNot Nothing Then currentPart.TwistVariablePrefix = sr.AsString
            End Select
        Next

        If currentPart IsNot Nothing Then b.Parts.Add(currentPart)
        Return b
    End Function

    Public Function ParseMOVT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MOVT_Data
        Dim m As New MOVT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MNAM"
                    m.MovementName = sr.AsString
                Case "SPED"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 32 Then
                        m.LeftWalk = BitConverter.ToSingle(sr.Data, 0)
                        m.LeftRun = BitConverter.ToSingle(sr.Data, 4)
                        m.RightWalk = BitConverter.ToSingle(sr.Data, 8)
                        m.RightRun = BitConverter.ToSingle(sr.Data, 12)
                        m.ForwardWalk = BitConverter.ToSingle(sr.Data, 16)
                        m.ForwardRun = BitConverter.ToSingle(sr.Data, 20)
                        m.BackWalk = BitConverter.ToSingle(sr.Data, 24)
                        m.BackRun = BitConverter.ToSingle(sr.Data, 28)
                    End If
                Case "JNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.FloatHeight = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "LNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.FlightAngleGain = BitConverter.ToSingle(sr.Data, 0)
                    End If
            End Select
        Next

        Return m
    End Function

    Public Function ParseCSTY(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CSTY_Data
        Dim c As New CSTY_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "CSGD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 48 Then
                        c.OffensiveMult = BitConverter.ToSingle(sr.Data, 0)
                        c.DefensiveMult = BitConverter.ToSingle(sr.Data, 4)
                        c.GroupOffensiveMult = BitConverter.ToSingle(sr.Data, 8)
                        c.EquipScoreMelee = BitConverter.ToSingle(sr.Data, 12)
                        c.EquipScoreMagic = BitConverter.ToSingle(sr.Data, 16)
                        c.EquipScoreRanged = BitConverter.ToSingle(sr.Data, 20)
                        c.EquipScoreShout = BitConverter.ToSingle(sr.Data, 24)
                        c.EquipScoreUnarmed = BitConverter.ToSingle(sr.Data, 28)
                        c.EquipScoreStaff = BitConverter.ToSingle(sr.Data, 32)
                        c.AvoidThreatChance = BitConverter.ToSingle(sr.Data, 36)
                        c.DodgeChance = BitConverter.ToSingle(sr.Data, 40)
                        c.EvadeChance = BitConverter.ToSingle(sr.Data, 44)
                    End If
                Case "CSRA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        c.RangedAccuracyMult = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        c.StyleFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return c
    End Function

    Public Function ParseVTYP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As VTYP_Data
        Dim v As New VTYP_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                v.VoiceFlags = sr.Data(0)
            End If
        Next

        Return v
    End Function

    Public Function ParseRELA(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As RELA_Data
        Dim r As New RELA_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 9 Then
                r.ParentNPCFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                r.ChildNPCFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                r.Rank = sr.Data(8)
            End If
        Next

        Return r
    End Function

End Module

#End Region
