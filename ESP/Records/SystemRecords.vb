Imports System.Drawing
Imports System.Text

' ============================================================================
' System / Infrastructure Record Data Classes and Parsers
' COLL, DFOB, DOBJ, AACT, ASPC, ASTP, AORU, BNDS, DUAL, ZOOM, AMDL, TRNS,
' RFGP, LAYR, SCCO, LAND, NAVI, FSTP, FSTS, IDLM
' (OVIS, NOCM moved to AdditionalRecords.vb)
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

' ############################################################################
' # ⛔ SIN LLAMADOR Y SIN VALIDAR. NO CABLEAR SIN COMPARAR CAMPO A CAMPO.     #
' ############################################################################
' Este archivo NO tiene ni un llamador en las tres apps: su unica entrada es
' RecordDispatcher.ParseRecord, que esta marcado <Obsolete> y tampoco se llama
' desde produccion. LEER LA CABECERA DE RecordDispatcher.vb ANTES DE TOCAR ESTO.
'
' ESTADO 2026-08-19: los defectos MEDIDOS que fabricaban FormID inexistentes SE
' ARREGLARON. El sweep (Tools\RecordParserSweepProbe, los dos juegos reales) da
' 0 excepciones y el residuo son referencias colgadas REALES de Bethesda.
'
' ⛔ Eso NO significa "validado". Nadie comparo campo a campo la mayoria de los
' ~130 parsers contra wbDefinitions{FO4,TES5}.pas. Lo que se cerro es "no
' inventan referencias", que es otra cosa.
'
' ⛔ Y el problema ESTRUCTURAL sigue: estos parsers son un Select Case PLANO
' sobre una lista plana de subrecords, y el formato canonico es un ARBOL. Por eso
' la misma firma significa cosas distintas segun donde aparezca y el ultimo gana
' (paso con QUST/PACK/TERM/SCEN). Cada corte por contexto es un pedazo de arbol
' reconstruido a mano. El arreglo de fondo es parsear con el anidamiento que el
' canonico declara. Decision del usuario: se encara despues.
'
' UN FormID LEIDO MAL NO FALLA: da un numero plausible y equivocado, sin error.
' Antes de cablear cualquiera de estos parsers a produccion, comparar sus campos
' contra el .pas y volver a correr el sweep.
' ############################################################################
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

''' <summary>Record FSTS — Footstep Set. Estructura IDÉNTICA en FO4 y TES5 (verificado en los dos .pas), así
''' que no hay despacho por juego.
''' <code>
''' XCNT (requerido) : 5 × itU32 — Walking, Running, Sprinting, Sneaking, Swimming
''' DATA (requerido) : CINCO arrays de FormID [FSTP], con el largo de cada uno tomado de XCNT vía
'''                    SetCountPath, y en ESTE orden: Swimming, Sneaking, Sprinting, Running, Walking
''' </code>
''' <para>⛔⛔ EL ORDEN DE DATA ES EL INVERSO DEL DE XCNT. Es la trampa del record: los contadores van
''' Walking→Swimming y los arrays Swimming→Walking. Emparejarlos en el mismo orden da cinco listas cruzadas
''' entre sí, con largos que casi siempre "cierran" y por lo tanto sin ningún síntoma.</para>
''' <para>⛔ Reemplaza a un parser que leía CINCO FormID de offsets fijos DENTRO DE XCNT — o sea que remapeaba
''' los CONTADORES como si fueran referencias — y los guardaba en campos inventados
''' (<c>WalkForwardFormID</c>, <c>RunForwardAltFormID</c>, <c>WalkForwardAlt2FormID</c>) que no existen en
''' ninguna de las dos definiciones canónicas. MEDIDO: los cinco campos daban 100 % de FormID inexistentes en
''' los dos juegos, con valores 0x8/0x9/0xC/0xD — que son los contadores.</para></summary>
Public Class FSTS_Data
    Public FormID As UInteger
    Public EditorID As String = ""

    ''' <summary>Los cinco contadores de XCNT, en el orden del canónico.</summary>
    Public WalkingCount As UInteger
    Public RunningCount As UInteger
    Public SprintingCount As UInteger
    Public SneakingCount As UInteger
    Public SwimmingCount As UInteger

    ''' <summary>Los cinco arrays de DATA, cada uno con FormID de FSTP ya resueltos a global.</summary>
    Public SwimmingFootsteps As New List(Of UInteger)
    Public SneakingFootsteps As New List(Of UInteger)
    Public SprintingFootsteps As New List(Of UInteger)
    Public RunningFootsteps As New List(Of UInteger)
    Public WalkingFootsteps As New List(Of UInteger)

    ''' <summary>True cuando DATA no trae exactamente los <c>Walking+Running+Sprinting+Sneaking+Swimming</c>
    ''' FormID que anuncia XCNT. No se adivina: se parsea lo que entre y se deja la marca, porque partir el
    ''' bloque con contadores que no cierran cruza los cinco arrays entre sí.</summary>
    Public CountsMismatch As Boolean = False
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

    Public Function ParseCOLL(rec As PluginRecord, pluginManager As PluginManager) As COLL_Data
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
                Case "MNAM" : c.LayerName = sr.AsStringGeneral
                Case "CNAM"
                    c.CollidesWithFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next
        Return c
    End Function

    Public Function ParseDFOB(rec As PluginRecord, pluginManager As PluginManager) As DFOB_Data
        Dim d As New DFOB_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" Then d.ObjectFormID = ResolveFID(rec, sr, pluginManager)
        Next
        Return d
    End Function

    Public Function ParseDOBJ(rec As PluginRecord, pluginManager As PluginManager) As DOBJ_Data
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

    Public Function ParseAACT(rec As PluginRecord, pluginManager As PluginManager) As AACT_Data
        Dim a As New AACT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.ActionColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "DNAM" : a.Notes = sr.AsStringGeneral
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then a.ActionType = BitConverter.ToUInt32(sr.Data, 0)
                Case "DATA" : a.AttractionRuleFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return a
    End Function

    Public Function ParseASPC(rec As PluginRecord, pluginManager As PluginManager) As ASPC_Data
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

    Public Function ParseASTP(rec As PluginRecord, pluginManager As PluginManager) As ASTP_Data
        Dim a As New ASTP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MPRT" : a.MaleParentTitle = sr.AsStringGeneral
                Case "FPRT" : a.FemaleParentTitle = sr.AsStringGeneral
                Case "MCHT" : a.MaleChildTitle = sr.AsStringGeneral
                Case "FCHT" : a.FemaleChildTitle = sr.AsStringGeneral
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.IsFamilyAssociation = (BitConverter.ToUInt32(sr.Data, 0) And &H1UI) <> 0
                    End If
            End Select
        Next
        Return a
    End Function

    Public Function ParseAORU(rec As PluginRecord, pluginManager As PluginManager) As AORU_Data
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

    Public Function ParseBNDS(rec As PluginRecord, pluginManager As PluginManager) As BNDS_Data
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

    Public Function ParseDUAL(rec As PluginRecord, pluginManager As PluginManager) As DUAL_Data
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

    Public Function ParseZOOM(rec As PluginRecord, pluginManager As PluginManager) As ZOOM_Data
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

    Public Function ParseAMDL(rec As PluginRecord, pluginManager As PluginManager) As AMDL_Data
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

    Public Function ParseTRNS(rec As PluginRecord, pluginManager As PluginManager) As TRNS_Data
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

    Public Function ParseRFGP(rec As PluginRecord, pluginManager As PluginManager) As RFGP_Data
        Dim r As New RFGP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "NNAM" : r.GroupName = sr.AsStringGeneral
                Case "RNAM" : r.ReferenceFormID = ResolveFID(rec, sr, pluginManager)
                Case "PNAM" : r.PackInFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next
        Return r
    End Function

    Public Function ParseLAYR(rec As PluginRecord, pluginManager As PluginManager) As LAYR_Data
        Dim l As New LAYR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            If sr.Signature = "PNAM" Then l.ParentLayerFormID = ResolveFID(rec, sr, pluginManager)
        Next
        Return l
    End Function

    Public Function ParseSCCO(rec As PluginRecord, pluginManager As PluginManager) As SCCO_Data
        Dim s As New SCCO_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "QNAM" : s.QuestFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM" : s.SceneFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next
        Return s
    End Function

    Public Function ParseLAND(rec As PluginRecord, pluginManager As PluginManager) As LAND_Data
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

    Public Function ParseNAVI(rec As PluginRecord, pluginManager As PluginManager) As NAVI_Data
        Return New NAVI_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID,
            .HasData = rec.Subrecords.Count > 0
        }
    End Function

    Public Function ParseFSTP(rec As PluginRecord, pluginManager As PluginManager) As FSTP_Data
        Dim f As New FSTP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DATA" : f.ImpactDataSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "ANAM" : f.Tag = sr.AsStringGeneral
            End Select
        Next
        Return f
    End Function

    ''' <summary>Parsea FSTS según la ley canónica: XCNT trae los cinco contadores y DATA los cinco arrays de
    ''' FormID [FSTP] — <b>en orden inverso al de los contadores</b>. Ver el doc de <see cref="FSTS_Data"/>.
    ''' <para>⛔ XCNT se lee SIEMPRE antes que DATA, sin depender del orden en que vengan los subrecords: se
    ''' hacen dos pasadas. El canónico los declara XCNT→DATA y en los datos reales vienen así, pero atar la
    ''' correctitud a ese orden es gratis de evitar y caro de descubrir.</para></summary>
    Public Function ParseFSTS(rec As PluginRecord, pluginManager As PluginManager) As FSTS_Data
        Dim f As New FSTS_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}

        ' Pasada 1 — XCNT: 5 × itU32, orden Walking, Running, Sprinting, Sneaking, Swimming.
        For Each sr In rec.Subrecords
            If sr.Signature <> "XCNT" Then Continue For
            If sr.Data Is Nothing OrElse sr.Data.Length < 20 Then Exit For
            f.WalkingCount = BitConverter.ToUInt32(sr.Data, 0)
            f.RunningCount = BitConverter.ToUInt32(sr.Data, 4)
            f.SprintingCount = BitConverter.ToUInt32(sr.Data, 8)
            f.SneakingCount = BitConverter.ToUInt32(sr.Data, 12)
            f.SwimmingCount = BitConverter.ToUInt32(sr.Data, 16)
            Exit For
        Next

        ' Pasada 2 — DATA: los cinco arrays, en el orden del canónico (Swimming primero, Walking último).
        For Each sr In rec.Subrecords
            If sr.Signature <> "DATA" Then Continue For
            Dim d = sr.Data
            If d Is Nothing Then Exit For

            Dim disponibles As Long = d.Length \ 4
            Dim anunciados As Long = CLng(f.SwimmingCount) + f.SneakingCount + f.SprintingCount +
                                     f.RunningCount + f.WalkingCount
            f.CountsMismatch = (anunciados <> disponibles)

            Dim pos As Integer = 0
            Dim tomar = Sub(cuantos As UInteger, destino As List(Of UInteger))
                            For k = 1UI To cuantos
                                If pos + 4 > d.Length Then Exit For
                                destino.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(d, pos), pluginManager))
                                pos += 4
                            Next
                        End Sub
            tomar(f.SwimmingCount, f.SwimmingFootsteps)
            tomar(f.SneakingCount, f.SneakingFootsteps)
            tomar(f.SprintingCount, f.SprintingFootsteps)
            tomar(f.RunningCount, f.RunningFootsteps)
            tomar(f.WalkingCount, f.WalkingFootsteps)
            Exit For
        Next

        Return f
    End Function

    Public Function ParseIDLM(rec As PluginRecord, pluginManager As PluginManager) As IDLM_Data
        Dim i As New IDLM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If i.ModelPath = "" Then i.ModelPath = sr.AsStringGeneral
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
