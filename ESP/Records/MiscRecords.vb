Imports System.Drawing
Imports System.Text

' ============================================================================
' Misc World Object Record Data Classes and Parsers
' ACTI, STAT, DOOR, FURN, MSTT, TREE, GRAS, TERM, MESG, LSCR, SCOL, PKIN, TACT,
' ADDN, ANIO, DEBR
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

' ############################################################################
' # ⛔⛔⛔ NO USAR: PARSERS SIN VALIDAR. NO CABLEAR HASTA ARREGLARLOS.          #
' ############################################################################
' Este archivo NO tiene ni un llamador en las tres apps: su unica entrada es
' RecordDispatcher.ParseRecord, que esta marcado <Obsolete> y tampoco se llama
' desde produccion. LEER LA CABECERA DE RecordDispatcher.vb ANTES DE TOCAR ESTO.
'
' DEFECTOS MEDIDOS 2026-08-18 (Tools\RecordParserSweepProbe, dos juegos reales,
' FO4 420.731 + SSE 330.953 records: 0 excepciones, pero FormID que NO EXISTEN):
'   FO4 TERM.LoopingSoundFormID 37/37 -> FormID inexistente.
'
' UN FormID LEIDO MAL NO FALLA: da un numero plausible y equivocado, sin error. Si
' esto llega al writer, sale un ESP con referencias apuntando a otro mod.
' Decision del usuario 2026-08-18: NO se borran; se arreglan cuando se aborde.
' ############################################################################
#Region "Data Classes"

''' <summary>Fallout 4 ACTI record - Activator.</summary>
Friend Class ACTI_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend ActivateTextOverride As String = ""
    Friend MarkerColor As Color = Color.Empty
    Friend LoopingSoundFormID As UInteger
    Friend ActivationSoundFormID As UInteger
    Friend WaterTypeFormID As UInteger
    Friend InteractionKeywordFormID As UInteger
    Friend NativeTerminalFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend ActivatorFlags As UShort

    ' RADR Radio Receiver
    Friend RadioSoundModelFormID As UInteger
    Friend RadioFrequency As Single
    Friend RadioVolume As Single
    Friend RadioStartsActive As Boolean
End Class

''' <summary>Fallout 4 STAT record - Static.</summary>
Friend Class STAT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""

    ' DNAM
    Friend MaxAngle As Single = 90.0F
    Friend DirectionMaterialFormID As UInteger
    Friend LeafAmplitude As Single = 1.0F
    Friend LeafFrequency As Single = 1.0F

    ' MNAM LOD meshes
    Friend LODMeshes As String() = {"", "", "", ""}
End Class

''' <summary>Fallout 4 DOOR record - Door.</summary>
Friend Class DOOR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend OpenSoundFormID As UInteger
    Friend CloseSoundFormID As UInteger
    Friend LoopSoundFormID As UInteger
    Friend DoorFlags As Byte
    Friend OpenText As String = ""
    Friend CloseText As String = ""
    Friend RandomTeleportFormIDs As New List(Of UInteger)
    Friend KeywordFormIDs As New List(Of UInteger)

    Friend ReadOnly Property IsAutomatic As Boolean
        Get
            Return (DoorFlags And &H2) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsHidden As Boolean
        Get
            Return (DoorFlags And &H4) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 FURN record - Furniture.</summary>
Friend Class FURN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend NativeTerminalFormID As UInteger
    Friend FurnitureFlags As UShort

    ' WBDT Workbench
    Friend WorkbenchType As Byte  ' 0=None, 1=CreateObject, 2=Weapons, 5=Alchemy, 8=Armor, 9=PowerArmor, 10=RobotMod
    Friend AssociatedFormID As UInteger

    ' Container items
    Friend Items As New List(Of ContainerItem)
End Class

''' <summary>Fallout 4 MSTT record - Moveable Static.</summary>
Friend Class MSTT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend LoopingSoundFormID As UInteger
    Friend OnLocalMap As Boolean = True
End Class

''' <summary>Fallout 4 TREE record - Tree.</summary>
Friend Class TREE_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IngredientFormID As UInteger
    Friend HarvestSoundFormID As UInteger
    Friend ProductionSpring As Byte
    Friend ProductionSummer As Byte
    Friend ProductionFall As Byte
    Friend ProductionWinter As Byte

    ' CNAM tree data
    Friend TrunkFlexibility As Single = 1.0F
    Friend BranchFlexibility As Single = 1.0F
    Friend LeafAmplitude As Single = 1.0F
    Friend LeafFrequency As Single = 1.0F
End Class

''' <summary>Fallout 4 GRAS record - Grass.</summary>
Friend Class GRAS_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend Density As Byte = 30
    Friend MinSlope As Byte
    Friend MaxSlope As Byte = 90
    Friend UnitsFromWater As UShort
End Class

''' <summary>Terminal menu item entry.</summary>
Friend Class TERM_MenuItem
    Friend ItemText As String = ""
    Friend ResponseText As String = ""
    Friend ItemType As Byte
    Friend ItemID As UShort
    Friend SubmenuFormID As UInteger
End Class

''' <summary>Fallout 4 TERM record - Terminal.</summary>
Friend Class TERM_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend HeaderText As String = ""
    Friend WelcomeText As String = ""
    Friend LoopingSoundFormID As UInteger
    Friend TerminalFlags As UShort
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend BodyTexts As New List(Of String)
    Friend MenuItems As New List(Of TERM_MenuItem)
End Class

''' <summary>Message button entry.</summary>
Friend Class MESG_Button
    Friend ButtonText As String = ""
End Class

''' <summary>Fallout 4 MESG record - Message.</summary>
Friend Class MESG_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend ShortTitle As String = ""
    Friend SWFPath As String = ""
    Friend OwnerQuestFormID As UInteger
    Friend MessageFlags As UInteger
    Friend DisplayTime As UInteger = 2
    Friend Buttons As New List(Of MESG_Button)

    Friend ReadOnly Property IsMessageBox As Boolean
        Get
            Return (MessageFlags And &H1UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 LSCR record - Load Screen.</summary>
Friend Class LSCR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Description As String = ""
    Friend LoadingNIFFormID As UInteger
    Friend TransformFormID As UInteger
    Friend CameraPath As String = ""
End Class

''' <summary>Fallout 4 SCOL record - Static Collection.</summary>
Friend Class SCOL_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
End Class

''' <summary>Fallout 4 PKIN record - Pack-In.</summary>
Friend Class PKIN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend CellFormID As UInteger
    Friend Version As UInteger
End Class

''' <summary>Fallout 4 TACT record - Talking Activator.</summary>
Friend Class TACT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend VoiceTypeFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 ADDN record - Addon Node.</summary>
Friend Class ADDN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend NodeIndex As UInteger = 284
    Friend SoundFormID As UInteger
    Friend LightFormID As UInteger
End Class

''' <summary>Fallout 4 ANIO record - Animated Object.</summary>
Friend Class ANIO_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend UnloadEvent As String = ""
End Class

''' <summary>Fallout 4 DEBR record - Debris.</summary>
Friend Class DEBR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend HasData As Boolean
End Class

#End Region

#Region "Parsers"

Friend Module MiscRecordParsers

    Friend Function ParseACTI(rec As PluginRecord, pluginManager As PluginManager) As ACTI_Data
        Dim a As New ACTI_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseSTAT(rec As PluginRecord, pluginManager As PluginManager) As STAT_Data
        Dim s As New STAT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseDOOR(rec As PluginRecord, pluginManager As PluginManager) As DOOR_Data
        Dim d As New DOOR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : d.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If d.ModelPath = "" Then d.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseFURN(rec As PluginRecord, pluginManager As PluginManager) As FURN_Data
        Dim f As New FURN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : f.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If f.ModelPath = "" Then f.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseMSTT(rec As PluginRecord, pluginManager As PluginManager) As MSTT_Data
        Dim m As New MSTT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If m.ModelPath = "" Then m.ModelPath = sr.AsStringGeneral
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, m.KeywordFormIDs)
                Case "SNAM" : m.LoopingSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then m.OnLocalMap = sr.Data(0) <> 0
            End Select
        Next

        Return m
    End Function

    Friend Function ParseTREE(rec As PluginRecord, pluginManager As PluginManager) As TREE_Data
        Dim t As New TREE_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : t.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If t.ModelPath = "" Then t.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseGRAS(rec As PluginRecord, pluginManager As PluginManager) As GRAS_Data
        Dim g As New GRAS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If g.ModelPath = "" Then g.ModelPath = sr.AsStringGeneral
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then
                        g.Density = sr.Data(0) : g.MinSlope = sr.Data(1) : g.MaxSlope = sr.Data(2)
                        g.UnitsFromWater = BitConverter.ToUInt16(sr.Data, 3)
                    End If
            End Select
        Next

        Return g
    End Function

    Friend Function ParseTERM(rec As PluginRecord, pluginManager As PluginManager) As TERM_Data
        Dim t As New TERM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        Dim currentMenuItem As TERM_MenuItem = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : t.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If t.ModelPath = "" Then t.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseMESG(rec As PluginRecord, pluginManager As PluginManager) As MESG_Data
        Dim m As New MESG_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : m.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "NNAM" : m.ShortTitle = ResolveStr(rec, sr, pluginManager)
                Case "SNAM" : m.SWFPath = sr.AsStringGeneral
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

    Friend Function ParseLSCR(rec As PluginRecord, pluginManager As PluginManager) As LSCR_Data
        Dim l As New LSCR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DESC" : l.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "NNAM" : l.LoadingNIFFormID = ResolveFID(rec, sr, pluginManager)
                Case "TNAM" : l.TransformFormID = ResolveFID(rec, sr, pluginManager)
                Case "MOD2" : l.CameraPath = sr.AsStringGeneral
            End Select
        Next

        Return l
    End Function

    Friend Function ParseSCOL(rec As PluginRecord, pluginManager As PluginManager) As SCOL_Data
        Dim s As New SCOL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsStringGeneral
            End Select
        Next
        Return s
    End Function

    Friend Function ParsePKIN(rec As PluginRecord, pluginManager As PluginManager) As PKIN_Data
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

    Friend Function ParseTACT(rec As PluginRecord, pluginManager As PluginManager) As TACT_Data
        Dim t As New TACT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : t.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If t.ModelPath = "" Then t.ModelPath = sr.AsStringGeneral
                Case "KWDA" : ParseFormIDArray(sr, rec, pluginManager, t.KeywordFormIDs)
                Case "VNAM" : t.VoiceTypeFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return t
    End Function

    Friend Function ParseADDN(rec As PluginRecord, pluginManager As PluginManager) As ADDN_Data
        Dim a As New ADDN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral  ' FO4 ADDN model is wbGenericModel→MODL (wbDefinitionsFO4.pas:8149), not MOD2.
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then a.NodeIndex = BitConverter.ToUInt32(sr.Data, 0)
                Case "SNAM" : a.SoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "LNAM" : a.LightFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return a
    End Function

    Friend Function ParseANIO(rec As PluginRecord, pluginManager As PluginManager) As ANIO_Data
        Dim a As New ANIO_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral
                Case "BNAM" : a.UnloadEvent = sr.AsStringGeneral
            End Select
        Next
        Return a
    End Function

    Friend Function ParseDEBR(rec As PluginRecord, pluginManager As PluginManager) As DEBR_Data
        Return New DEBR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID,
            .HasData = rec.Subrecords.Count > 0
        }
    End Function

End Module

#End Region
