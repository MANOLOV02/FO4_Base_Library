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
' ============================================================================

' ############################################################################
' # SIN LLAMADOR Y SIN VALIDAR. NO CABLEAR SIN COMPARAR CAMPO A CAMPO. #
' ############################################################################
' Este archivo NO tiene ni un llamador en las tres apps: su unica entrada es
' RecordDispatcher.ParseRecord, que esta marcado <Obsolete> y tampoco se llama
' desde produccion. LEER LA CABECERA DE RecordDispatcher.vb ANTES DE TOCAR ESTO.
'
' ESTADO 2026-08-19: los defectos MEDIDOS que fabricaban FormID inexistentes SE
' ARREGLARON. El sweep (Tools\RecordParserSweepProbe, los dos juegos reales) da
' 0 excepciones y el residuo son referencias colgadas REALES de Bethesda.
'
' Eso NO significa "validado". Nadie comparo campo a campo la mayoria de los
' ~130 parsers contra la especificacion real de cada record. Lo que se cerro es "no
' inventan referencias", que es otra cosa.
'
' Y el problema ESTRUCTURAL sigue: estos parsers son un Select Case PLANO
' sobre una lista plana de subrecords, y el formato canonico es un ARBOL. Por eso
' la misma firma significa cosas distintas segun donde aparezca y el ultimo gana
' (paso con QUST/PACK/TERM/SCEN). Cada corte por contexto es un pedazo de arbol
' reconstruido a mano. El arreglo de fondo es parsear con el anidamiento que el
' canonico declara. Decision del usuario: se encara despues.
'
' UN FormID LEIDO MAL NO FALLA: da un numero plausible y equivocado, sin error.
' Antes de cablear cualquiera de estos parsers a produccion, comparar sus campos
' contra la especificacion real y volver a correr el sweep.
' ############################################################################
#Region "Data Classes"

' -------------------------------------------------------
' FO4 records with full definitions
' -------------------------------------------------------

''' <summary>Fallout 4 LCRT record - Location Reference Type.</summary>
Friend Class LCRT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Color As Color = Color.Empty
    Friend KeywordType As UInteger
End Class

''' <summary>MATO directional material data.</summary>
Friend Class MATO_MaterialData
    Friend FalloffScale As Single
    Friend FalloffBias As Single
    Friend NoiseUVScale As Single
    Friend MaterialUVScale As Single
    Friend ProjectionVectorX As Single
    Friend ProjectionVectorY As Single
    Friend ProjectionVectorZ As Single
    Friend NormalDampener As Single
    Friend SinglePassColorR As Single
    Friend SinglePassColorG As Single
    Friend SinglePassColorB As Single
    Friend SinglePass As Boolean
    Friend IsSnow As Boolean
End Class

''' <summary>Fallout 4 / SSE MATO record - Material Object.</summary>
Friend Class MATO_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend PropertyData As Byte() = Nothing
    Friend MaterialData As New MATO_MaterialData
End Class

''' <summary>NOCM obstacle entry.</summary>
Friend Class NOCM_Entry
    Friend Index As UInteger
    Friend DataEntries As New List(Of Byte())
    Friend IntervalData As Byte() = Nothing
    Friend ModelPath As String = ""
End Class

''' <summary>Fallout 4 NOCM record - Navigation Mesh Obstacle Manager.</summary>
Friend Class NOCM_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Entries As New List(Of NOCM_Entry)
End Class

''' <summary>OVIS object bounds entry.</summary>
Friend Class OVIS_Entry
    Friend ObjectFormID As UInteger
    Friend BoundsX1 As Single
    Friend BoundsY1 As Single
    Friend BoundsZ1 As Single
    Friend BoundsX2 As Single
    Friend BoundsY2 As Single
    Friend BoundsZ2 As Single
End Class

''' <summary>Fallout 4 OVIS record - Object Visibility Manager.</summary>
Friend Class OVIS_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Objects As New List(Of OVIS_Entry)
End Class

''' <summary>Fallout 4 PLYR record - Player Reference.</summary>
Friend Class PLYR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

' -------------------------------------------------------
' SSE records with full definitions
' -------------------------------------------------------

''' <summary>SSE SCRL record - Scroll.</summary>
Friend Class SCRL_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend PickUpSoundFormID As UInteger
    Friend PutDownSoundFormID As UInteger
    Friend EquipTypeFormID As UInteger
    Friend MenuDisplayObjectFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend ItemValue As UInteger
    Friend ItemWeight As Single
    ' SPIT data
    Friend BaseCost As UInteger
    Friend SpellFlags As UInteger
    Friend SpellType As UInteger
    Friend ChargeTime As Single
    Friend CastType As UInteger
    Friend DeliveryType As UInteger
    Friend CastDuration As Single
    Friend Range As Single
    Friend HalfCostPerkFormID As UInteger
    ' Effects
    Friend Effects As New List(Of MagicEffect_Entry)
End Class

''' <summary>SHOU word entry (word + spell + recovery).</summary>
Friend Class SHOU_WordEntry
    Friend WordFormID As UInteger
    Friend SpellFormID As UInteger
    Friend RecoveryTime As Single
End Class

''' <summary>SSE SHOU record - Shout.</summary>
Friend Class SHOU_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend MenuDisplayObjectFormID As UInteger
    Friend EquipTypeFormID As UInteger
    Friend Words As New List(Of SHOU_WordEntry)
End Class

''' <summary>SSE WOOP record - Word of Power.</summary>
Friend Class WOOP_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Translation As String = ""
End Class

''' <summary>SSE RGDL record - Ragdoll.</summary>
Friend Class RGDL_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Version As UInteger
    ' DATA general
    Friend DynamicBoneCount As UInteger
    Friend FeedbackEnabled As Boolean
    Friend FootIKEnabled As Boolean
    Friend LookIKEnabled As Boolean
    Friend GrabIKEnabled As Boolean
    Friend PoseMatchingEnabled As Boolean
    ' XNAM
    Friend ActorBaseFormID As UInteger
    ' TNAM
    Friend BodyPartDataFormID As UInteger
    ' RAFD feedback data
    Friend DynamicKeyframeBlend As Single
    Friend HierarchyGain As Single
    Friend PositionGain As Single
    Friend VelocityGain As Single
    Friend AccelerationGain As Single
    Friend SnapGain As Single
    Friend VelocityDamping As Single
    Friend SnapMaxLinearVelocity As Single
    Friend SnapMaxAngularVelocity As Single
    Friend SnapMaxLinearDistance As Single
    Friend SnapMaxAngularDistance As Single
    Friend PositionMaxLinearVelocity As Single
    Friend PositionMaxAngularVelocity As Single
    Friend ProjectileMaxVelocity As Integer
    Friend MeleeMaxVelocity As Integer
    ' RAFB
    Friend FeedbackDynamicBones As New List(Of UShort)
    ' RAPS
    Friend MatchBone1 As UShort
    Friend MatchBone2 As UShort
    Friend MatchBone3 As UShort
    Friend DisableOnMove As Boolean
    Friend MotorsStrength As Single
    Friend PoseActivationDelayTime As Single
    Friend MatchErrorAllowance As Single
    Friend DisplacementToDisable As Single
    ' ANAM
    Friend DeathPose As String = ""
End Class

''' <summary>SSE APPA record - Apparatus (Alchemy Station type).</summary>
Friend Class APPA_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend PickUpSoundFormID As UInteger
    Friend PutDownSoundFormID As UInteger
    Friend Quality As UInteger
    Friend ItemValue As UInteger
    Friend ItemWeight As Single
End Class

''' <summary>SSE SLGM record - Soul Gem.</summary>
Friend Class SLGM_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend PickUpSoundFormID As UInteger
    Friend PutDownSoundFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend ItemValue As UInteger
    Friend ItemWeight As Single
    Friend ContainedSoul As Byte
    Friend MaximumCapacity As Byte
    Friend LinkedSoulGemFormID As UInteger
End Class

''' <summary>SSE VOLI record - Volumetric Lighting.</summary>
Friend Class VOLI_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Intensity As Single
    Friend CustomColorContribution As Single
    Friend RedR As Single
    Friend RedG As Single
    Friend RedB As Single
    Friend GreenR As Single
    Friend GreenG As Single
    Friend GreenB As Single
    Friend BlueR As Single
    Friend BlueG As Single
    Friend BlueB As Single
    Friend DensityContribution As Single
    Friend DensitySize As Single
    Friend DensityWindSpeed As Single
    Friend DensityFallingSpeed As Single
    Friend PhaseFunctionContribution As Single
    Friend PhaseFunctionScattering As Single
    Friend SamplingRepartitionRangeFactor As Single
End Class

' -------------------------------------------------------
' Stub records (exist in game files but have no defined subrecords beyond EDID)
' -------------------------------------------------------

''' <summary>Fallout 4 LSPR record - (stub, unused in practice).</summary>
Friend Class LSPR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>Fallout 4 MICN record - Menu Icon (stub).</summary>
Friend Class MICN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>Fallout 4 SCPT record - Script (legacy stub).</summary>
Friend Class SCPT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>Fallout 4 SKIL record - Skill (legacy stub).</summary>
Friend Class SKIL_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>Fallout 4 TLOD record - (stub).</summary>
Friend Class TLOD_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>Fallout 4 TOFT record - (stub).</summary>
Friend Class TOFT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>SSE CLDC record - (unused, empty GRUP).</summary>
Friend Class CLDC_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>SSE HAIR record - Hair (legacy, empty GRUP).</summary>
Friend Class HAIR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

''' <summary>SSE PWAT record - Placeable Water (unused, empty GRUP).</summary>
Friend Class PWAT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
End Class

#End Region

#Region "Parsers"

Friend Module AdditionalRecordParsers

    ' ===================================================================
    ' FO4 full parsers
    ' ===================================================================

    Friend Function ParseLCRT(rec As PluginRecord, pluginManager As PluginManager) As LCRT_Data
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

    Friend Function ParseMATO(rec As PluginRecord, pluginManager As PluginManager) As MATO_Data
        Dim m As New MATO_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL" : If m.ModelPath = "" Then m.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseNOCM(rec As PluginRecord, pluginManager As PluginManager) As NOCM_Data
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
                    If currentEntry IsNot Nothing Then currentEntry.ModelPath = sr.AsStringGeneral
            End Select
        Next
        If currentEntry IsNot Nothing Then n.Entries.Add(currentEntry)
        Return n
    End Function

    Friend Function ParseOVIS(rec As PluginRecord, pluginManager As PluginManager) As OVIS_Data
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

    Friend Function ParsePLYR(rec As PluginRecord, pluginManager As PluginManager) As PLYR_Data
        Return New PLYR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    ' ===================================================================
    ' SSE full parsers
    ' ===================================================================

    Friend Function ParseSCRL(rec As PluginRecord, pluginManager As PluginManager) As SCRL_Data
        Dim s As New SCRL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : s.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsStringGeneral
                ' SCRL has no ICON in the schema (FO4's definition leaves it unused;
                ' TES5 SCRL declares none) — dead case removed.
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
                    Dim eff As New MagicEffect_Entry With {
                        .BaseEffectFormID = ResolveFID(rec, sr, pluginManager)
                    }
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

    Friend Function ParseSHOU(rec As PluginRecord, pluginManager As PluginManager) As SHOU_Data
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

    Friend Function ParseWOOP(rec As PluginRecord, pluginManager As PluginManager) As WOOP_Data
        Dim w As New WOOP_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : w.FullName = ResolveStr(rec, sr, pluginManager)
                Case "TNAM" : w.Translation = ResolveStr(rec, sr, pluginManager)
            End Select
        Next
        Return w
    End Function

    Friend Function ParseRGDL(rec As PluginRecord, pluginManager As PluginManager) As RGDL_Data
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
                Case "ANAM" : r.DeathPose = sr.AsStringGeneral
            End Select
        Next
        Return r
    End Function

    Friend Function ParseAPPA(rec As PluginRecord, pluginManager As PluginManager) As APPA_Data
        Dim a As New APPA_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC" : a.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL" : If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral
                Case "ICON" : a.IconPath = sr.AsStringGeneral
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

    Friend Function ParseSLGM(rec As PluginRecord, pluginManager As PluginManager) As SLGM_Data
        Dim s As New SLGM_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL" : s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL" : If s.ModelPath = "" Then s.ModelPath = sr.AsStringGeneral
                Case "ICON" : s.IconPath = sr.AsStringGeneral
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

    Friend Function ParseVOLI(rec As PluginRecord, pluginManager As PluginManager) As VOLI_Data
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

    Friend Function ParseLSPR(rec As PluginRecord, pluginManager As PluginManager) As LSPR_Data
        Return New LSPR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseMICN(rec As PluginRecord, pluginManager As PluginManager) As MICN_Data
        Return New MICN_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseSCPT(rec As PluginRecord, pluginManager As PluginManager) As SCPT_Data
        Return New SCPT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseSKIL(rec As PluginRecord, pluginManager As PluginManager) As SKIL_Data
        Return New SKIL_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseTLOD(rec As PluginRecord, pluginManager As PluginManager) As TLOD_Data
        Return New TLOD_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseTOFT(rec As PluginRecord, pluginManager As PluginManager) As TOFT_Data
        Return New TOFT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseCLDC(rec As PluginRecord, pluginManager As PluginManager) As CLDC_Data
        Return New CLDC_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParseHAIR(rec As PluginRecord, pluginManager As PluginManager) As HAIR_Data
        Return New HAIR_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

    Friend Function ParsePWAT(rec As PluginRecord, pluginManager As PluginManager) As PWAT_Data
        Return New PWAT_Data With {.FormID = rec.Header.FormID, .EditorID = rec.EditorID}
    End Function

End Module

#End Region
