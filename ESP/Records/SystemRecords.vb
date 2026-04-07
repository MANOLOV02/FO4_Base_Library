Imports System.Drawing
Imports System.Text

' ============================================================================
' System / Infrastructure Record Data Classes and Parsers
' COLL, DFOB, DOBJ, AACT, ASPC, ASTP, AORU, BNDS, DUAL, ZOOM, AMDL, TRNS,
' RFGP, LAYR, SCCO, LAND, NAVI, FSTP, FSTS, IDLM
' (OVIS, NOCM moved to AdditionalRecords.vb)
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 COLL record - Collision Layer.</summary>
Public Class COLL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Description As String = ""
    Public LayerIndex As UInteger
    Public DebugColor As Color = Color.Empty
    Public CollisionFlags As UInteger
    Public LayerName As String = ""
    Public CollidesWithFormIDs As New List(Of UInteger)

    Public ReadOnly Property IsTriggerVolume As Boolean
        Get
            Return (CollisionFlags And &H1UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 DFOB record - Default Object.</summary>
Public Class DFOB_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ObjectFormID As UInteger
End Class

''' <summary>DOBJ default object entry.</summary>
Public Class DOBJ_Entry
    Public UseType As UInteger
    Public ObjectFormID As UInteger
End Class

''' <summary>Fallout 4 DOBJ record - Default Object Manager.</summary>
Public Class DOBJ_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Objects As New List(Of DOBJ_Entry)
End Class

''' <summary>Fallout 4 AACT record - Action.</summary>
Public Class AACT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ActionColor As Color = Color.Empty
    Public Notes As String = ""
    Public ActionType As UInteger
    Public AttractionRuleFormID As UInteger
End Class

''' <summary>Fallout 4 ASPC record - Acoustic Space.</summary>
Public Class ASPC_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public LoopingSoundFormID As UInteger
    Public RegionSoundFormID As UInteger
    Public EnvironmentTypeFormID As UInteger
    Public IsInterior As Boolean
    Public WeatherAttenuationDB As Single
End Class

''' <summary>Fallout 4 ASTP record - Association Type.</summary>
Public Class ASTP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public MaleParentTitle As String = ""
    Public FemaleParentTitle As String = ""
    Public MaleChildTitle As String = ""
    Public FemaleChildTitle As String = ""
    Public IsFamilyAssociation As Boolean
End Class

''' <summary>Fallout 4 AORU record - Attraction Rule.</summary>
Public Class AORU_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Radius As Single = 600.0F
    Public MinDelay As Single
    Public MaxDelay As Single = 30.0F
    Public RequiresLineOfSight As Boolean
    Public IsCombatTarget As Boolean
End Class

''' <summary>Fallout 4 BNDS record - Bendable Spline.</summary>
Public Class BNDS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public DefaultNumTiles As Single = 1.0F
    Public DefaultNumSlices As UShort = 4
    Public RelativeToLength As Boolean
    Public DefaultColor As Color = Color.Empty
    Public WindSensibility As Single
    Public WindFlexibility As Single
End Class

''' <summary>Fallout 4 DUAL record - Dual Cast Data.</summary>
Public Class DUAL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ProjectileFormID As UInteger
    Public ExplosionFormID As UInteger
    Public EffectShaderFormID As UInteger
    Public HitEffectArtFormID As UInteger
    Public ImpactDataSetFormID As UInteger
    Public InheritScaleFlags As UInteger
End Class

''' <summary>Fallout 4 ZOOM record - Zoom Data.</summary>
Public Class ZOOM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FOVMult As Single = 1.0F
    Public OverlayType As UInteger
    Public ImageSpaceModFormID As UInteger
    Public CameraOffsetX As Single
    Public CameraOffsetY As Single
    Public CameraOffsetZ As Single
End Class

''' <summary>Fallout 4 AMDL record - Aim Model.</summary>
Public Class AMDL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ConeOfFireMinAngle As Single = 2.0F
    Public ConeOfFireMaxAngle As Single = 8.0F
    Public ConeOfFireIncreasePerShot As Single = 0.3F
    Public ConeOfFireDecreasePerSec As Single = 60.0F
    Public ConeOfFireDecreaseDelayMs As UInteger = 2
    Public ConeOfFireSneakMult As Single
    Public RecoilDiminishSpringForce As Single
    Public RecoilDiminishSightsMult As Single
    Public RecoilMaxPerShot As Single
    Public RecoilMinPerShot As Single
    Public RecoilHipMult As Single
End Class

''' <summary>Fallout 4 TRNS record - Transform.</summary>
Public Class TRNS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public PositionX As Single
    Public PositionY As Single
    Public PositionZ As Single
    Public RotationX As Single
    Public RotationY As Single
    Public RotationZ As Single
    Public Scale As Single = 1.0F
    Public ZoomMin As Single = -1.0F
    Public ZoomMax As Single = 1.0F
End Class

''' <summary>Fallout 4 RFGP record - Reference Group.</summary>
Public Class RFGP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public GroupName As String = ""
    Public ReferenceFormID As UInteger
    Public PackInFormID As UInteger
End Class

''' <summary>Fallout 4 LAYR record - Layer.</summary>
Public Class LAYR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentLayerFormID As UInteger
End Class

''' <summary>Fallout 4 SCCO record - Scene Collection.</summary>
Public Class SCCO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public QuestFormID As UInteger
    Public SceneFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 LAND record - Landscape (simplified).</summary>
Public Class LAND_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public LandFlags As UInteger
    Public HasHeightMap As Boolean
    Public HasVertexColors As Boolean
    Public HasLayers As Boolean
End Class

''' <summary>Fallout 4 NAVI record - Navmesh Info Map (simplified).</summary>
Public Class NAVI_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public HasData As Boolean
End Class

''' <summary>Fallout 4 FSTP record - Footstep.</summary>
Public Class FSTP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ImpactDataSetFormID As UInteger
    Public Tag As String = ""
End Class

''' <summary>Fallout 4 FSTS record - Footstep Set.</summary>
Public Class FSTS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public WalkForwardFormID As UInteger
    Public RunForwardFormID As UInteger
    Public WalkForwardAltFormID As UInteger
    Public RunForwardAltFormID As UInteger
    Public WalkForwardAlt2FormID As UInteger
End Class

''' <summary>Fallout 4 IDLM record - Idle Marker.</summary>
Public Class IDLM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public IdleFlags As Byte
    Public IdleCount As Byte
    Public IdleTimerMin As Single
    Public IdleTimerMax As Single
    Public IdleFormIDs As New List(Of UInteger)
End Class

#End Region

#Region "Parsers"

Public Module SystemRecordParsers

    Public Function ParseCOLL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As COLL_Data
        Dim c As New COLL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DESC" : c.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "BNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then c.LayerIndex = BitConverter.ToUInt32(sr.Data, 0)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        c.DebugColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "GNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then c.CollisionFlags = BitConverter.ToUInt32(sr.Data, 0)
                Case "MNAM" : c.LayerName = sr.AsString
                Case "CNAM"
                    c.CollidesWithFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next
        Return c
    End Function

    Public Function ParseDFOB(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DFOB_Data
        Dim d As New DFOB_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" Then d.ObjectFormID = ResolveFID(rec, sr, pluginManager)
        Next
        Return d
    End Function

    Public Function ParseDOBJ(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DOBJ_Data
        Dim d As New DOBJ_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                For i = 0 To sr.Data.Length - 8 Step 8
                    d.Objects.Add(New DOBJ_Entry With {
                        .UseType = BitConverter.ToUInt32(sr.Data, i),
                        .ObjectFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i + 4), pluginManager)
                    })
                Next
            End If
        Next
        Return d
    End Function

    Public Function ParseAACT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As AACT_Data
        Dim a As New AACT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.ActionColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "DNAM" : a.Notes = sr.AsString
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then a.ActionType = BitConverter.ToUInt32(sr.Data, 0)
                Case "DATA" : a.AttractionRuleFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return a
    End Function

    Public Function ParseASPC(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ASPC_Data
        Dim a As New ASPC_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "SNAM" : a.LoopingSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "RDAT" : a.RegionSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "BNAM" : a.EnvironmentTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "XTRI"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then a.IsInterior = sr.Data(0) <> 0
                Case "WNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        a.WeatherAttenuationDB = BitConverter.ToUInt16(sr.Data, 0) / 100.0F
                    End If
            End Select
        Next
        Return a
    End Function

    Public Function ParseASTP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ASTP_Data
        Dim a As New ASTP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MPRT" : a.MaleParentTitle = sr.AsString
                Case "FPRT" : a.FemaleParentTitle = sr.AsString
                Case "MCHT" : a.MaleChildTitle = sr.AsString
                Case "FCHT" : a.FemaleChildTitle = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.IsFamilyAssociation = (BitConverter.ToUInt32(sr.Data, 0) And &H1UI) <> 0
                    End If
            End Select
        Next
        Return a
    End Function

    Public Function ParseAORU(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As AORU_Data
        Dim a As New AORU_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "AOR2" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                a.Radius = BitConverter.ToSingle(sr.Data, 0)
                a.MinDelay = BitConverter.ToSingle(sr.Data, 4)
                a.MaxDelay = BitConverter.ToSingle(sr.Data, 8)
                a.RequiresLineOfSight = sr.Data(12) <> 0
                a.IsCombatTarget = sr.Data(13) <> 0
            End If
        Next
        Return a
    End Function

    Public Function ParseBNDS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As BNDS_Data
        Dim b As New BNDS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 28 Then
                b.DefaultNumTiles = BitConverter.ToSingle(sr.Data, 0)
                b.DefaultNumSlices = BitConverter.ToUInt16(sr.Data, 4)
                b.RelativeToLength = BitConverter.ToUInt16(sr.Data, 6) <> 0
                b.WindSensibility = BitConverter.ToSingle(sr.Data, 24)
                If sr.Data.Length >= 32 Then b.WindFlexibility = BitConverter.ToSingle(sr.Data, 28)
            End If
        Next
        Return b
    End Function

    Public Function ParseDUAL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DUAL_Data
        Dim d As New DUAL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                d.ProjectileFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                d.ExplosionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                d.EffectShaderFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 8), pluginManager)
                d.HitEffectArtFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 12), pluginManager)
                d.ImpactDataSetFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 16), pluginManager)
                d.InheritScaleFlags = BitConverter.ToUInt32(sr.Data, 20)
            End If
        Next
        Return d
    End Function

    Public Function ParseZOOM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ZOOM_Data
        Dim z As New ZOOM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "GNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                z.FOVMult = BitConverter.ToSingle(sr.Data, 0)
                z.OverlayType = BitConverter.ToUInt32(sr.Data, 4)
                z.ImageSpaceModFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 8), pluginManager)
                z.CameraOffsetX = BitConverter.ToSingle(sr.Data, 12)
                z.CameraOffsetY = BitConverter.ToSingle(sr.Data, 16)
                z.CameraOffsetZ = BitConverter.ToSingle(sr.Data, 20)
            End If
        Next
        Return z
    End Function

    Public Function ParseAMDL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As AMDL_Data
        Dim a As New AMDL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 20 Then
                a.ConeOfFireMinAngle = BitConverter.ToSingle(sr.Data, 0)
                a.ConeOfFireMaxAngle = BitConverter.ToSingle(sr.Data, 4)
                a.ConeOfFireIncreasePerShot = BitConverter.ToSingle(sr.Data, 8)
                a.ConeOfFireDecreasePerSec = BitConverter.ToSingle(sr.Data, 12)
                a.ConeOfFireDecreaseDelayMs = BitConverter.ToUInt32(sr.Data, 16)
                If sr.Data.Length >= 24 Then a.ConeOfFireSneakMult = BitConverter.ToSingle(sr.Data, 20)
                If sr.Data.Length >= 28 Then a.RecoilDiminishSpringForce = BitConverter.ToSingle(sr.Data, 24)
                If sr.Data.Length >= 32 Then a.RecoilDiminishSightsMult = BitConverter.ToSingle(sr.Data, 28)
                If sr.Data.Length >= 36 Then a.RecoilMaxPerShot = BitConverter.ToSingle(sr.Data, 32)
                If sr.Data.Length >= 40 Then a.RecoilMinPerShot = BitConverter.ToSingle(sr.Data, 36)
                If sr.Data.Length >= 44 Then a.RecoilHipMult = BitConverter.ToSingle(sr.Data, 40)
            End If
        Next
        Return a
    End Function

    Public Function ParseTRNS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TRNS_Data
        Dim t As New TRNS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 28 Then
                t.PositionX = BitConverter.ToSingle(sr.Data, 0)
                t.PositionY = BitConverter.ToSingle(sr.Data, 4)
                t.PositionZ = BitConverter.ToSingle(sr.Data, 8)
                t.RotationX = BitConverter.ToSingle(sr.Data, 12)
                t.RotationY = BitConverter.ToSingle(sr.Data, 16)
                t.RotationZ = BitConverter.ToSingle(sr.Data, 20)
                t.Scale = BitConverter.ToSingle(sr.Data, 24)
                If sr.Data.Length >= 36 Then
                    t.ZoomMin = BitConverter.ToSingle(sr.Data, 28)
                    t.ZoomMax = BitConverter.ToSingle(sr.Data, 32)
                End If
            End If
        Next
        Return t
    End Function

    Public Function ParseRFGP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As RFGP_Data
        Dim r As New RFGP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "NNAM" : r.GroupName = sr.AsString
                Case "RNAM" : r.ReferenceFormID = ResolveFID(rec, sr, pluginManager)
                Case "PNAM" : r.PackInFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return r
    End Function

    Public Function ParseLAYR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LAYR_Data
        Dim l As New LAYR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "PNAM" Then l.ParentLayerFormID = ResolveFID(rec, sr, pluginManager)
        Next
        Return l
    End Function

    Public Function ParseSCCO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SCCO_Data
        Dim s As New SCCO_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "QNAM" : s.QuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM" : s.SceneFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next
        Return s
    End Function

    Public Function ParseLAND(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LAND_Data
        Dim l As New LAND_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                l.LandFlags = BitConverter.ToUInt32(sr.Data, 0)
                l.HasHeightMap = (l.LandFlags And &H1UI) <> 0
                l.HasVertexColors = (l.LandFlags And &H2UI) <> 0
                l.HasLayers = (l.LandFlags And &H4UI) <> 0
            End If
        Next
        Return l
    End Function

    Public Function ParseNAVI(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As NAVI_Data
        Return New NAVI_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID,
            .HasData = rec.Subrecords.Count > 0
        }
    End Function

    Public Function ParseFSTP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FSTP_Data
        Dim f As New FSTP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DATA" : f.ImpactDataSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "ANAM" : f.Tag = sr.AsString
            End Select
        Next
        Return f
    End Function

    Public Function ParseFSTS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FSTS_Data
        Dim f As New FSTS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "XCNT" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 20 Then
                f.WalkForwardFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                f.RunForwardFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                f.WalkForwardAltFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 8), pluginManager)
                f.RunForwardAltFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 12), pluginManager)
                f.WalkForwardAlt2FormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 16), pluginManager)
            End If
        Next
        Return f
    End Function

    Public Function ParseIDLM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As IDLM_Data
        Dim i As New IDLM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If i.ModelPath = "" Then i.ModelPath = sr.AsString
                Case "IDLF"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then i.IdleFlags = sr.Data(0)
                Case "IDLC"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then i.IdleCount = sr.Data(0)
                Case "IDLT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then i.IdleTimerMin = BitConverter.ToSingle(sr.Data, 0)
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then i.IdleTimerMax = BitConverter.ToSingle(sr.Data, 4)
                Case "IDLA"
                    i.IdleFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next
        Return i
    End Function

End Module

#End Region
