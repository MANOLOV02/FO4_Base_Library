Imports System.Drawing
Imports System.Text

' ============================================================================
' Misc World Object Record Data Classes and Parsers
' ACTI, STAT, DOOR, FURN, MSTT, TREE, GRAS, TERM, MESG, LSCR, SCOL, PKIN, TACT,
' ADDN, ANIO, DEBR
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 ACTI record - Activator.</summary>
Public Class ACTI_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public ActivateTextOverride As String = ""
    Public MarkerColor As Color = Color.Empty
    Public LoopingSoundFormID As UInteger
    Public ActivationSoundFormID As UInteger
    Public WaterTypeFormID As UInteger
    Public InteractionKeywordFormID As UInteger
    Public NativeTerminalFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
    Public ActivatorFlags As UShort

    ' RADR Radio Receiver
    Public RadioSoundModelFormID As UInteger
    Public RadioFrequency As Single
    Public RadioVolume As Single
    Public RadioStartsActive As Boolean
End Class

''' <summary>Fallout 4 STAT record - Static.</summary>
Public Class STAT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""

    ' DNAM
    Public MaxAngle As Single = 90.0F
    Public DirectionMaterialFormID As UInteger
    Public LeafAmplitude As Single = 1.0F
    Public LeafFrequency As Single = 1.0F

    ' MNAM LOD meshes
    Public LODMeshes As String() = {"", "", "", ""}
End Class

''' <summary>Fallout 4 DOOR record - Door.</summary>
Public Class DOOR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public OpenSoundFormID As UInteger
    Public CloseSoundFormID As UInteger
    Public LoopSoundFormID As UInteger
    Public DoorFlags As Byte
    Public OpenText As String = ""
    Public CloseText As String = ""
    Public RandomTeleportFormIDs As New List(Of UInteger)
    Public KeywordFormIDs As New List(Of UInteger)

    Public ReadOnly Property IsAutomatic As Boolean
        Get
            Return (DoorFlags And &H2) <> 0
        End Get
    End Property

    Public ReadOnly Property IsHidden As Boolean
        Get
            Return (DoorFlags And &H4) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 FURN record - Furniture.</summary>
Public Class FURN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public NativeTerminalFormID As UInteger
    Public FurnitureFlags As UShort

    ' WBDT Workbench
    Public WorkbenchType As Byte  ' 0=None, 1=CreateObject, 2=Weapons, 5=Alchemy, 8=Armor, 9=PowerArmor, 10=RobotMod
    Public AssociatedFormID As UInteger

    ' Container items
    Public Items As New List(Of ContainerItem)
End Class

''' <summary>Fallout 4 MSTT record - Moveable Static.</summary>
Public Class MSTT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public LoopingSoundFormID As UInteger
    Public OnLocalMap As Boolean = True
End Class

''' <summary>Fallout 4 TREE record - Tree.</summary>
Public Class TREE_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IngredientFormID As UInteger
    Public HarvestSoundFormID As UInteger
    Public ProductionSpring As Byte
    Public ProductionSummer As Byte
    Public ProductionFall As Byte
    Public ProductionWinter As Byte

    ' CNAM tree data
    Public TrunkFlexibility As Single = 1.0F
    Public BranchFlexibility As Single = 1.0F
    Public LeafAmplitude As Single = 1.0F
    Public LeafFrequency As Single = 1.0F
End Class

''' <summary>Fallout 4 GRAS record - Grass.</summary>
Public Class GRAS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public Density As Byte = 30
    Public MinSlope As Byte
    Public MaxSlope As Byte = 90
    Public UnitsFromWater As UShort
End Class

''' <summary>Terminal menu item entry.</summary>
Public Class TERM_MenuItem
    Public ItemText As String = ""
    Public ResponseText As String = ""
    Public ItemType As Byte
    Public ItemID As UShort
    Public SubmenuFormID As UInteger
End Class

''' <summary>Fallout 4 TERM record - Terminal.</summary>
Public Class TERM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public HeaderText As String = ""
    Public WelcomeText As String = ""
    Public LoopingSoundFormID As UInteger
    Public TerminalFlags As UShort
    Public KeywordFormIDs As New List(Of UInteger)
    Public BodyTexts As New List(Of String)
    Public MenuItems As New List(Of TERM_MenuItem)
End Class

''' <summary>Message button entry.</summary>
Public Class MESG_Button
    Public ButtonText As String = ""
End Class

''' <summary>Fallout 4 MESG record - Message.</summary>
Public Class MESG_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public ShortTitle As String = ""
    Public SWFPath As String = ""
    Public OwnerQuestFormID As UInteger
    Public MessageFlags As UInteger
    Public DisplayTime As UInteger = 2
    Public Buttons As New List(Of MESG_Button)

    Public ReadOnly Property IsMessageBox As Boolean
        Get
            Return (MessageFlags And &H1UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 LSCR record - Load Screen.</summary>
Public Class LSCR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Description As String = ""
    Public LoadingNIFFormID As UInteger
    Public TransformFormID As UInteger
    Public CameraPath As String = ""
End Class

''' <summary>Fallout 4 SCOL record - Static Collection.</summary>
Public Class SCOL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
End Class

''' <summary>Fallout 4 PKIN record - Pack-In.</summary>
Public Class PKIN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public CellFormID As UInteger
    Public Version As UInteger
End Class

''' <summary>Fallout 4 TACT record - Talking Activator.</summary>
Public Class TACT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public VoiceTypeFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 ADDN record - Addon Node.</summary>
Public Class ADDN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public NodeIndex As UInteger = 284
    Public SoundFormID As UInteger
    Public LightFormID As UInteger
End Class

''' <summary>Fallout 4 ANIO record - Animated Object.</summary>
Public Class ANIO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public UnloadEvent As String = ""
End Class

''' <summary>Fallout 4 DEBR record - Debris.</summary>
Public Class DEBR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public HasData As Boolean
End Class

#End Region

#Region "Parsers"

Public Module MiscRecordParsers

    Public Function ParseACTI(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ACTI_Data
        Dim a As New ACTI_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "ATTX" : a.ActivateTextOverride = ResolveStr(rec, sr, pluginManager)
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, a.KeywordFormIDs)
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.MarkerColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "SNAM" : a.LoopingSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "VNAM" : a.ActivationSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "WNAM" : a.WaterTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "KNAM" : a.InteractionKeywordFormID = ResolveFID(rec, sr, pluginManager)
                Case "NTRM" : a.NativeTerminalFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        a.ActivatorFlags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "RADR"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 14 Then
                        a.RadioSoundModelFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                        a.RadioFrequency = BitConverter.ToSingle(sr.Data, 4)
                        a.RadioVolume = BitConverter.ToSingle(sr.Data, 8)
                        a.RadioStartsActive = sr.Data(12) <> 0
                    End If
            End Select
        Next

        Return a
    End Function

    Public Function ParseSTAT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As STAT_Data
        Dim s As New STAT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsString
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.MaxAngle = BitConverter.ToSingle(sr.Data, 0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        s.DirectionMaterialFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        s.LeafAmplitude = BitConverter.ToSingle(sr.Data, 8)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        s.LeafFrequency = BitConverter.ToSingle(sr.Data, 12)
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseDOOR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DOOR_Data
        Dim d As New DOOR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : d.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If d.ModelPath = "" Then d.ModelPath = sr.AsString
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, d.KeywordFormIDs)
                Case "SNAM" : d.OpenSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ANAM" : d.CloseSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "BNAM" : d.LoopSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then d.DoorFlags = sr.Data(0)
                Case "ONAM" : d.OpenText = ResolveStr(rec, sr, pluginManager)
                Case "CNAM" : d.CloseText = ResolveStr(rec, sr, pluginManager)
                Case "TNAM" : d.RandomTeleportFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next

        Return d
    End Function

    Public Function ParseFURN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FURN_Data
        Dim f As New FURN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : f.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If f.ModelPath = "" Then f.ModelPath = sr.AsString
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, f.KeywordFormIDs)
                Case "NTRM" : f.NativeTerminalFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        f.FurnitureFlags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "WBDT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then f.WorkbenchType = sr.Data(0)
                Case "NAM1"
                    f.AssociatedFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNTO"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        f.Items.Add(New ContainerItem With {
                            .ItemFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager),
                            .Count = BitConverter.ToInt32(sr.Data, 4)
                        })
                    End If
            End Select
        Next

        Return f
    End Function

    Public Function ParseMSTT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MSTT_Data
        Dim m As New MSTT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If m.ModelPath = "" Then m.ModelPath = sr.AsString
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, m.KeywordFormIDs)
                Case "SNAM" : m.LoopingSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then m.OnLocalMap = sr.Data(0) <> 0
            End Select
        Next

        Return m
    End Function

    Public Function ParseTREE(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TREE_Data
        Dim t As New TREE_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : t.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If t.ModelPath = "" Then t.ModelPath = sr.AsString
                Case "PFIG" : t.IngredientFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM" : t.HarvestSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "PFPC"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        t.ProductionSpring = sr.Data(0) : t.ProductionSummer = sr.Data(1)
                        t.ProductionFall = sr.Data(2) : t.ProductionWinter = sr.Data(3)
                    End If
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 48 Then
                        t.TrunkFlexibility = BitConverter.ToSingle(sr.Data, 0)
                        t.BranchFlexibility = BitConverter.ToSingle(sr.Data, 4)
                        t.LeafAmplitude = BitConverter.ToSingle(sr.Data, 40)
                        t.LeafFrequency = BitConverter.ToSingle(sr.Data, 44)
                    End If
            End Select
        Next

        Return t
    End Function

    Public Function ParseGRAS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As GRAS_Data
        Dim g As New GRAS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If g.ModelPath = "" Then g.ModelPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then
                        g.Density = sr.Data(0) : g.MinSlope = sr.Data(1) : g.MaxSlope = sr.Data(2)
                        g.UnitsFromWater = BitConverter.ToUInt16(sr.Data, 3)
                    End If
            End Select
        Next

        Return g
    End Function

    Public Function ParseTERM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TERM_Data
        Dim t As New TERM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        Dim currentMenuItem As TERM_MenuItem = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : t.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If t.ModelPath = "" Then t.ModelPath = sr.AsString
                Case "NAM0" : t.HeaderText = ResolveStr(rec, sr, pluginManager)
                Case "WNAM" : t.WelcomeText = ResolveStr(rec, sr, pluginManager)
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, t.KeywordFormIDs)
                Case "SNAM" : t.LoopingSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        t.TerminalFlags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "BTXT"
                    t.BodyTexts.Add(ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings))
                Case "ITXT"
                    If currentMenuItem IsNot Nothing Then t.MenuItems.Add(currentMenuItem)
                    currentMenuItem = New TERM_MenuItem With {
                        .ItemText = ResolveStr(rec, sr, pluginManager)
                    }
                Case "RNAM"
                    If currentMenuItem IsNot Nothing Then
                        currentMenuItem.ResponseText = ResolveStr(rec, sr, pluginManager)
                    End If
                Case "ANAM"
                    If currentMenuItem IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        currentMenuItem.ItemType = sr.Data(0)
                    End If
                Case "ITID"
                    If currentMenuItem IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        currentMenuItem.ItemID = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "TNAM"
                    If currentMenuItem IsNot Nothing Then
                        currentMenuItem.SubmenuFormID = ResolveFID(rec, sr, pluginManager)
                    End If
            End Select
        Next

        If currentMenuItem IsNot Nothing Then t.MenuItems.Add(currentMenuItem)
        Return t
    End Function

    Public Function ParseMESG(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MESG_Data
        Dim m As New MESG_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : m.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "NNAM" : m.ShortTitle = ResolveStr(rec, sr, pluginManager)
                Case "SNAM" : m.SWFPath = sr.AsString
                Case "QNAM" : m.OwnerQuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.MessageFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.DisplayTime = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "ITXT"
                    m.Buttons.Add(New MESG_Button With {
                        .ButtonText = ResolveStr(rec, sr, pluginManager)
                    })
            End Select
        Next

        Return m
    End Function

    Public Function ParseLSCR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LSCR_Data
        Dim l As New LSCR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DESC" : l.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "NNAM" : l.LoadingNIFFormID = ResolveFID(rec, sr, pluginManager)
                Case "TNAM" : l.TransformFormID = ResolveFID(rec, sr, pluginManager)
                Case "MOD2" : l.CameraPath = sr.AsString
            End Select
        Next

        Return l
    End Function

    Public Function ParseSCOL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SCOL_Data
        Dim s As New SCOL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsString
            End Select
        Next
        Return s
    End Function

    Public Function ParsePKIN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As PKIN_Data
        Dim p As New PKIN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "CNAM" : p.CellFormID = ResolveFID(rec, sr, pluginManager)
                Case "VNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then p.Version = BitConverter.ToUInt32(sr.Data, 0)
            End Select
        Next
        Return p
    End Function

    Public Function ParseTACT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TACT_Data
        Dim t As New TACT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : t.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If t.ModelPath = "" Then t.ModelPath = sr.AsString
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, t.KeywordFormIDs)
                Case "VNAM" : t.VoiceTypeFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return t
    End Function

    Public Function ParseADDN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ADDN_Data
        Dim a As New ADDN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MOD2" : If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then a.NodeIndex = BitConverter.ToUInt32(sr.Data, 0)
                Case "SNAM" : a.SoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "LNAM" : a.LightFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return a
    End Function

    Public Function ParseANIO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ANIO_Data
        Dim a As New ANIO_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "BNAM" : a.UnloadEvent = sr.AsString
            End Select
        Next
        Return a
    End Function

    Public Function ParseDEBR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DEBR_Data
        Return New DEBR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID,
            .HasData = rec.Subrecords.Count > 0
        }
    End Function

End Module

#End Region
