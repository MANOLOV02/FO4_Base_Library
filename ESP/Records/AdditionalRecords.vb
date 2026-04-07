Imports System.Drawing
Imports System.Text

' ============================================================================
' Additional Record Data Classes and Parsers
' Records needed for 100% FO4 + SSE compatibility that were missing.
'
' FO4 full:   LCRT, MATO, NOCM, OVIS, PLYR
' FO4 stubs:  LSPR, MICN, SCPT, SKIL, TLOD, TOFT
' SSE full:   SCRL, SHOU, WOOP, RGDL, APPA, SLGM, VOLI
' SSE stubs:  CLDC, HAIR, PWAT
'
' Based on TES5Edit wbDefinitionsFO4.pas / wbDefinitionsTES5.pas
' ============================================================================

#Region "Data Classes"

' -------------------------------------------------------
' FO4 records with full definitions
' -------------------------------------------------------

''' <summary>Fallout 4 LCRT record - Location Reference Type.</summary>
Public Class LCRT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Color As Color = Color.Empty
    Public KeywordType As UInteger
End Class

''' <summary>MATO directional material data.</summary>
Public Class MATO_MaterialData
    Public FalloffScale As Single
    Public FalloffBias As Single
    Public NoiseUVScale As Single
    Public MaterialUVScale As Single
    Public ProjectionVectorX As Single
    Public ProjectionVectorY As Single
    Public ProjectionVectorZ As Single
    Public NormalDampener As Single
    Public SinglePassColorR As Single
    Public SinglePassColorG As Single
    Public SinglePassColorB As Single
    Public SinglePass As Boolean
    Public IsSnow As Boolean
End Class

''' <summary>Fallout 4 / SSE MATO record - Material Object.</summary>
Public Class MATO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public PropertyData As Byte() = Nothing
    Public MaterialData As New MATO_MaterialData
End Class

''' <summary>NOCM obstacle entry.</summary>
Public Class NOCM_Entry
    Public Index As UInteger
    Public DataEntries As New List(Of Byte())
    Public IntervalData As Byte() = Nothing
    Public ModelPath As String = ""
End Class

''' <summary>Fallout 4 NOCM record - Navigation Mesh Obstacle Manager.</summary>
Public Class NOCM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Entries As New List(Of NOCM_Entry)
End Class

''' <summary>OVIS object bounds entry.</summary>
Public Class OVIS_Entry
    Public ObjectFormID As UInteger
    Public BoundsX1 As Single
    Public BoundsY1 As Single
    Public BoundsZ1 As Single
    Public BoundsX2 As Single
    Public BoundsY2 As Single
    Public BoundsZ2 As Single
End Class

''' <summary>Fallout 4 OVIS record - Object Visibility Manager.</summary>
Public Class OVIS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Objects As New List(Of OVIS_Entry)
End Class

''' <summary>Fallout 4 PLYR record - Player Reference.</summary>
Public Class PLYR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

' -------------------------------------------------------
' SSE records with full definitions
' -------------------------------------------------------

''' <summary>SSE SCRL record - Scroll.</summary>
Public Class SCRL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public PickUpSoundFormID As UInteger
    Public PutDownSoundFormID As UInteger
    Public EquipTypeFormID As UInteger
    Public MenuDisplayObjectFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
    Public ItemValue As UInteger
    Public ItemWeight As Single
    ' SPIT data
    Public BaseCost As UInteger
    Public SpellFlags As UInteger
    Public SpellType As UInteger
    Public ChargeTime As Single
    Public CastType As UInteger
    Public DeliveryType As UInteger
    Public CastDuration As Single
    Public Range As Single
    Public HalfCostPerkFormID As UInteger
    ' Effects
    Public Effects As New List(Of MagicEffect_Entry)
End Class

''' <summary>SHOU word entry (word + spell + recovery).</summary>
Public Class SHOU_WordEntry
    Public WordFormID As UInteger
    Public SpellFormID As UInteger
    Public RecoveryTime As Single
End Class

''' <summary>SSE SHOU record - Shout.</summary>
Public Class SHOU_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public MenuDisplayObjectFormID As UInteger
    Public EquipTypeFormID As UInteger
    Public Words As New List(Of SHOU_WordEntry)
End Class

''' <summary>SSE WOOP record - Word of Power.</summary>
Public Class WOOP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Translation As String = ""
End Class

''' <summary>SSE RGDL record - Ragdoll.</summary>
Public Class RGDL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Version As UInteger
    ' DATA general
    Public DynamicBoneCount As UInteger
    Public FeedbackEnabled As Boolean
    Public FootIKEnabled As Boolean
    Public LookIKEnabled As Boolean
    Public GrabIKEnabled As Boolean
    Public PoseMatchingEnabled As Boolean
    ' XNAM
    Public ActorBaseFormID As UInteger
    ' TNAM
    Public BodyPartDataFormID As UInteger
    ' RAFD feedback data
    Public DynamicKeyframeBlend As Single
    Public HierarchyGain As Single
    Public PositionGain As Single
    Public VelocityGain As Single
    Public AccelerationGain As Single
    Public SnapGain As Single
    Public VelocityDamping As Single
    Public SnapMaxLinearVelocity As Single
    Public SnapMaxAngularVelocity As Single
    Public SnapMaxLinearDistance As Single
    Public SnapMaxAngularDistance As Single
    Public PositionMaxLinearVelocity As Single
    Public PositionMaxAngularVelocity As Single
    Public ProjectileMaxVelocity As Integer
    Public MeleeMaxVelocity As Integer
    ' RAFB
    Public FeedbackDynamicBones As New List(Of UShort)
    ' RAPS
    Public MatchBone1 As UShort
    Public MatchBone2 As UShort
    Public MatchBone3 As UShort
    Public DisableOnMove As Boolean
    Public MotorsStrength As Single
    Public PoseActivationDelayTime As Single
    Public MatchErrorAllowance As Single
    Public DisplacementToDisable As Single
    ' ANAM
    Public DeathPose As String = ""
End Class

''' <summary>SSE APPA record - Apparatus (Alchemy Station type).</summary>
Public Class APPA_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public PickUpSoundFormID As UInteger
    Public PutDownSoundFormID As UInteger
    Public Quality As UInteger
    Public ItemValue As UInteger
    Public ItemWeight As Single
End Class

''' <summary>SSE SLGM record - Soul Gem.</summary>
Public Class SLGM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public PickUpSoundFormID As UInteger
    Public PutDownSoundFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
    Public ItemValue As UInteger
    Public ItemWeight As Single
    Public ContainedSoul As Byte
    Public MaximumCapacity As Byte
    Public LinkedSoulGemFormID As UInteger
End Class

''' <summary>SSE VOLI record - Volumetric Lighting.</summary>
Public Class VOLI_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Intensity As Single
    Public CustomColorContribution As Single
    Public RedR As Single
    Public RedG As Single
    Public RedB As Single
    Public GreenR As Single
    Public GreenG As Single
    Public GreenB As Single
    Public BlueR As Single
    Public BlueG As Single
    Public BlueB As Single
    Public DensityContribution As Single
    Public DensitySize As Single
    Public DensityWindSpeed As Single
    Public DensityFallingSpeed As Single
    Public PhaseFunctionContribution As Single
    Public PhaseFunctionScattering As Single
    Public SamplingRepartitionRangeFactor As Single
End Class

' -------------------------------------------------------
' Stub records (exist in game files but have no defined subrecords beyond EDID)
' -------------------------------------------------------

''' <summary>Fallout 4 LSPR record - (stub, unused in practice).</summary>
Public Class LSPR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>Fallout 4 MICN record - Menu Icon (stub).</summary>
Public Class MICN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>Fallout 4 SCPT record - Script (legacy stub).</summary>
Public Class SCPT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>Fallout 4 SKIL record - Skill (legacy stub).</summary>
Public Class SKIL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>Fallout 4 TLOD record - (stub).</summary>
Public Class TLOD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>Fallout 4 TOFT record - (stub).</summary>
Public Class TOFT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>SSE CLDC record - (unused, empty GRUP).</summary>
Public Class CLDC_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>SSE HAIR record - Hair (legacy, empty GRUP).</summary>
Public Class HAIR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

''' <summary>SSE PWAT record - Placeable Water (unused, empty GRUP).</summary>
Public Class PWAT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
End Class

#End Region

#Region "Parsers"

Public Module AdditionalRecordParsers

    ' ===================================================================
    ' FO4 full parsers
    ' ===================================================================

    Public Function ParseLCRT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LCRT_Data
        Dim l As New LCRT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.Color = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then l.KeywordType = BitConverter.ToUInt32(sr.Data, 0)
            End Select
        Next
        Return l
    End Function

    Public Function ParseMATO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MATO_Data
        Dim m As New MATO_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If m.ModelPath = "" Then m.ModelPath = sr.AsString
                Case "DNAM" : m.PropertyData = sr.Data
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 28 Then
                        Dim d = m.MaterialData
                        d.FalloffScale = BitConverter.ToSingle(sr.Data, 0)
                        d.FalloffBias = BitConverter.ToSingle(sr.Data, 4)
                        d.NoiseUVScale = BitConverter.ToSingle(sr.Data, 8)
                        d.MaterialUVScale = BitConverter.ToSingle(sr.Data, 12)
                        d.ProjectionVectorX = BitConverter.ToSingle(sr.Data, 16)
                        d.ProjectionVectorY = BitConverter.ToSingle(sr.Data, 20)
                        d.ProjectionVectorZ = BitConverter.ToSingle(sr.Data, 24)
                        If sr.Data.Length >= 32 Then d.NormalDampener = BitConverter.ToSingle(sr.Data, 28)
                        If sr.Data.Length >= 44 Then
                            d.SinglePassColorR = BitConverter.ToSingle(sr.Data, 32)
                            d.SinglePassColorG = BitConverter.ToSingle(sr.Data, 36)
                            d.SinglePassColorB = BitConverter.ToSingle(sr.Data, 40)
                        End If
                        If sr.Data.Length >= 48 Then d.SinglePass = BitConverter.ToUInt32(sr.Data, 44) <> 0
                        If sr.Data.Length >= 52 Then d.IsSnow = BitConverter.ToUInt32(sr.Data, 48) <> 0
                    End If
            End Select
        Next
        Return m
    End Function

    Public Function ParseNOCM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As NOCM_Data
        Dim n As New NOCM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        Dim currentEntry As NOCM_Entry = Nothing
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "INDX"
                    If currentEntry IsNot Nothing Then n.Entries.Add(currentEntry)
                    currentEntry = New NOCM_Entry
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        currentEntry.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "DATA"
                    If currentEntry IsNot Nothing AndAlso sr.Data IsNot Nothing Then
                        currentEntry.DataEntries.Add(CType(sr.Data.Clone(), Byte()))
                    End If
                Case "INTV"
                    If currentEntry IsNot Nothing Then currentEntry.IntervalData = sr.Data
                Case "NAM1"
                    If currentEntry IsNot Nothing Then currentEntry.ModelPath = sr.AsString
            End Select
        Next
        If currentEntry IsNot Nothing Then n.Entries.Add(currentEntry)
        Return n
    End Function

    Public Function ParseOVIS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As OVIS_Data
        Dim o As New OVIS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        Dim pendingFormID As UInteger = 0
        Dim hasPending As Boolean = False
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "INDX"
                    pendingFormID = ResolveFID(rec, sr, pluginManager)
                    hasPending = True
                Case "DATA"
                    If hasPending AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                        o.Objects.Add(New OVIS_Entry With {
                            .ObjectFormID = pendingFormID,
                            .BoundsX1 = BitConverter.ToSingle(sr.Data, 0),
                            .BoundsY1 = BitConverter.ToSingle(sr.Data, 4),
                            .BoundsZ1 = BitConverter.ToSingle(sr.Data, 8),
                            .BoundsX2 = BitConverter.ToSingle(sr.Data, 12),
                            .BoundsY2 = BitConverter.ToSingle(sr.Data, 16),
                            .BoundsZ2 = BitConverter.ToSingle(sr.Data, 20)
                        })
                        hasPending = False
                    End If
            End Select
        Next
        Return o
    End Function

    Public Function ParsePLYR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As PLYR_Data
        Return New PLYR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    ' ===================================================================
    ' SSE full parsers
    ' ===================================================================

    Public Function ParseSCRL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SCRL_Data
        Dim s As New SCRL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : s.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsString
                Case "ICON" : s.IconPath = sr.AsString
                Case "YNAM" : s.PickUpSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ZNAM" : s.PutDownSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ETYP" : s.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "MDOB" : s.MenuDisplayObjectFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, s.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        s.ItemValue = BitConverter.ToUInt32(sr.Data, 0)
                        s.ItemWeight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "SPIT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 36 Then
                        s.BaseCost = BitConverter.ToUInt32(sr.Data, 0)
                        s.SpellFlags = BitConverter.ToUInt32(sr.Data, 4)
                        s.SpellType = BitConverter.ToUInt32(sr.Data, 8)
                        s.ChargeTime = BitConverter.ToSingle(sr.Data, 12)
                        s.CastType = BitConverter.ToUInt32(sr.Data, 16)
                        s.DeliveryType = BitConverter.ToUInt32(sr.Data, 20)
                        s.CastDuration = BitConverter.ToSingle(sr.Data, 24)
                        s.Range = BitConverter.ToSingle(sr.Data, 28)
                        s.HalfCostPerkFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 32), pluginManager)
                    End If
                Case "EFID"
                    ' Start of a new magic effect entry
                    Dim eff As New MagicEffect_Entry
                    eff.BaseEffectFormID = ResolveFID(rec, sr, pluginManager)
                    s.Effects.Add(eff)
                Case "EFIT"
                    If s.Effects.Count > 0 AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        Dim eff = s.Effects(s.Effects.Count - 1)
                        eff.Magnitude = BitConverter.ToSingle(sr.Data, 0)
                        eff.Area = BitConverter.ToUInt32(sr.Data, 4)
                        eff.Duration = BitConverter.ToUInt32(sr.Data, 8)
                    End If
            End Select
        Next
        Return s
    End Function

    Public Function ParseSHOU(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SHOU_Data
        Dim s As New SHOU_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : s.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MDOB" : s.MenuDisplayObjectFormID = ResolveFID(rec, sr, pluginManager)
                Case "ETYP" : s.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        s.Words.Add(New SHOU_WordEntry With {
                            .WordFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager),
                            .SpellFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager),
                            .RecoveryTime = BitConverter.ToSingle(sr.Data, 8)
                        })
                    End If
            End Select
        Next
        Return s
    End Function

    Public Function ParseWOOP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As WOOP_Data
        Dim w As New WOOP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : w.FullName = ResolveStr(rec, sr, pluginManager)
                Case "TNAM" : w.Translation = ResolveStr(rec, sr, pluginManager)
            End Select
        Next
        Return w
    End Function

    Public Function ParseRGDL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As RGDL_Data
        Dim r As New RGDL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "NVER"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then r.Version = BitConverter.ToUInt32(sr.Data, 0)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 10 Then
                        r.DynamicBoneCount = BitConverter.ToUInt32(sr.Data, 0)
                        ' bytes 4-7 unused
                        r.FeedbackEnabled = sr.Data(8) <> 0
                        If sr.Data.Length >= 13 Then
                            r.FootIKEnabled = sr.Data(9) <> 0
                            r.LookIKEnabled = sr.Data(10) <> 0
                            r.GrabIKEnabled = sr.Data(11) <> 0
                            r.PoseMatchingEnabled = sr.Data(12) <> 0
                        End If
                    End If
                Case "XNAM" : r.ActorBaseFormID = ResolveFID(rec, sr, pluginManager)
                Case "TNAM" : r.BodyPartDataFormID = ResolveFID(rec, sr, pluginManager)
                Case "RAFD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 52 Then
                        r.DynamicKeyframeBlend = BitConverter.ToSingle(sr.Data, 0)
                        r.HierarchyGain = BitConverter.ToSingle(sr.Data, 4)
                        r.PositionGain = BitConverter.ToSingle(sr.Data, 8)
                        r.VelocityGain = BitConverter.ToSingle(sr.Data, 12)
                        r.AccelerationGain = BitConverter.ToSingle(sr.Data, 16)
                        r.SnapGain = BitConverter.ToSingle(sr.Data, 20)
                        r.VelocityDamping = BitConverter.ToSingle(sr.Data, 24)
                        r.SnapMaxLinearVelocity = BitConverter.ToSingle(sr.Data, 28)
                        r.SnapMaxAngularVelocity = BitConverter.ToSingle(sr.Data, 32)
                        r.SnapMaxLinearDistance = BitConverter.ToSingle(sr.Data, 36)
                        r.SnapMaxAngularDistance = BitConverter.ToSingle(sr.Data, 40)
                        r.PositionMaxLinearVelocity = BitConverter.ToSingle(sr.Data, 44)
                        r.PositionMaxAngularVelocity = BitConverter.ToSingle(sr.Data, 48)
                        If sr.Data.Length >= 60 Then
                            r.ProjectileMaxVelocity = BitConverter.ToInt32(sr.Data, 52)
                            r.MeleeMaxVelocity = BitConverter.ToInt32(sr.Data, 56)
                        End If
                    End If
                Case "RAFB"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        For i = 0 To sr.Data.Length - 2 Step 2
                            r.FeedbackDynamicBones.Add(BitConverter.ToUInt16(sr.Data, i))
                        Next
                    End If
                Case "RAPS"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                        r.MatchBone1 = BitConverter.ToUInt16(sr.Data, 0)
                        r.MatchBone2 = BitConverter.ToUInt16(sr.Data, 2)
                        r.MatchBone3 = BitConverter.ToUInt16(sr.Data, 4)
                        r.DisableOnMove = sr.Data(6) <> 0
                        ' byte 7 unused
                        r.MotorsStrength = BitConverter.ToSingle(sr.Data, 8)
                        r.PoseActivationDelayTime = BitConverter.ToSingle(sr.Data, 12)
                        r.MatchErrorAllowance = BitConverter.ToSingle(sr.Data, 16)
                        r.DisplacementToDisable = BitConverter.ToSingle(sr.Data, 20)
                    End If
                Case "ANAM" : r.DeathPose = sr.AsString
            End Select
        Next
        Return r
    End Function

    Public Function ParseAPPA(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As APPA_Data
        Dim a As New APPA_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : a.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "ICON" : a.IconPath = sr.AsString
                Case "YNAM" : a.PickUpSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ZNAM" : a.PutDownSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "QUAL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then a.Quality = BitConverter.ToUInt32(sr.Data, 0)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        a.ItemValue = BitConverter.ToUInt32(sr.Data, 0)
                        a.ItemWeight = BitConverter.ToSingle(sr.Data, 4)
                    End If
            End Select
        Next
        Return a
    End Function

    Public Function ParseSLGM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SLGM_Data
        Dim s As New SLGM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsString
                Case "ICON" : s.IconPath = sr.AsString
                Case "YNAM" : s.PickUpSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ZNAM" : s.PutDownSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, s.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        s.ItemValue = BitConverter.ToUInt32(sr.Data, 0)
                        s.ItemWeight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "SOUL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then s.ContainedSoul = sr.Data(0)
                Case "SLCP"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then s.MaximumCapacity = sr.Data(0)
                Case "NAM0" : s.LinkedSoulGemFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return s
    End Function

    Public Function ParseVOLI(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As VOLI_Data
        Dim v As New VOLI_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.Intensity = BitConverter.ToSingle(sr.Data, 0)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.CustomColorContribution = BitConverter.ToSingle(sr.Data, 0)
                Case "ENAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        v.RedR = BitConverter.ToSingle(sr.Data, 0)
                        v.RedG = BitConverter.ToSingle(sr.Data, 4)
                        v.RedB = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        v.GreenR = BitConverter.ToSingle(sr.Data, 0)
                        v.GreenG = BitConverter.ToSingle(sr.Data, 4)
                        v.GreenB = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "GNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        v.BlueR = BitConverter.ToSingle(sr.Data, 0)
                        v.BlueG = BitConverter.ToSingle(sr.Data, 4)
                        v.BlueB = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "HNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.DensityContribution = BitConverter.ToSingle(sr.Data, 0)
                Case "INAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.DensitySize = BitConverter.ToSingle(sr.Data, 0)
                Case "JNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.DensityWindSpeed = BitConverter.ToSingle(sr.Data, 0)
                Case "KNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.DensityFallingSpeed = BitConverter.ToSingle(sr.Data, 0)
                Case "LNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.PhaseFunctionContribution = BitConverter.ToSingle(sr.Data, 0)
                Case "MNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.PhaseFunctionScattering = BitConverter.ToSingle(sr.Data, 0)
                Case "NNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then v.SamplingRepartitionRangeFactor = BitConverter.ToSingle(sr.Data, 0)
            End Select
        Next
        Return v
    End Function

    ' ===================================================================
    ' Stub parsers (minimal records, just extract EDID)
    ' ===================================================================

    Public Function ParseLSPR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LSPR_Data
        Return New LSPR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseMICN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MICN_Data
        Return New MICN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseSCPT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SCPT_Data
        Return New SCPT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseSKIL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SKIL_Data
        Return New SKIL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseTLOD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TLOD_Data
        Return New TLOD_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseTOFT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TOFT_Data
        Return New TOFT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseCLDC(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CLDC_Data
        Return New CLDC_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParseHAIR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As HAIR_Data
        Return New HAIR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Public Function ParsePWAT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As PWAT_Data
        Return New PWAT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

End Module

#End Region
