Imports System.Drawing
Imports System.Text

' ============================================================================
' Magic / Keywords / Game Settings Record Data Classes and Parsers
' ENCH, SPEL, MGEF, PERK, LVSP, KYWD, EQUP, GLOB, GMST, AVIF, DMGT
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 ENCH record - Enchantment (Object Effect).</summary>
Public Class ENCH_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""

    ' ENIT
    Public EnchantmentCost As Integer
    Public Flags As UInteger
    Public CastType As UInteger
    Public EnchantmentAmount As Integer
    Public TargetType As UInteger
    Public EnchantType As UInteger
    Public ChargeTime As Single
    Public BaseEnchantmentFormID As UInteger
    Public WornRestrictionsFormID As UInteger

    ' Effects
    Public Effects As New List(Of MagicEffect_Entry)
End Class

''' <summary>Fallout 4 SPEL record - Spell.</summary>
Public Class SPEL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public EquipTypeFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)

    ' SPIT
    Public BaseCost As UInteger
    Public SpellFlags As UInteger
    Public SpellType As UInteger   ' 0=Spell, 1=Disease, 2=Power, 3=LesserPower, 4=Ability, 5=Poison, 10=Addiction, 11=Voice
    Public ChargeTime As Single
    Public CastType As UInteger
    Public DeliveryType As UInteger
    Public CastDuration As Single
    Public Range As Single
    Public CastingPerkFormID As UInteger

    ' Effects
    Public Effects As New List(Of MagicEffect_Entry)

    Public ReadOnly Property IsAbility As Boolean
        Get
            Return SpellType = 4
        End Get
    End Property

    Public ReadOnly Property IsAddiction As Boolean
        Get
            Return SpellType = 10
        End Get
    End Property
End Class

''' <summary>Fallout 4 MGEF record - Magic Effect.</summary>
Public Class MGEF_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public MenuDisplayObjectFormID As UInteger

    ' DATA struct (massive, ~160 bytes)
    Public EffectFlags As UInteger
    Public BaseCost As Single
    Public AssociatedItemFormID As UInteger
    Public ResistValueFormID As UInteger
    Public CounterEffectCount As UShort
    Public CastingLightFormID As UInteger
    Public TaperWeight As Single
    Public HitShaderFormID As UInteger
    Public EnchantShaderFormID As UInteger
    Public MinSkillLevel As UInteger
    Public SpellmakingArea As UInteger
    Public SpellmakingCastingTime As Single
    Public TaperCurve As Single
    Public TaperDuration As Single
    Public SecondAVWeight As Single
    Public EffectType As Integer
    Public PrimaryAV As Integer
    Public ProjectileFormID As UInteger
    Public ExplosionFormID As UInteger
    Public CastingType As UInteger
    Public DeliveryType As UInteger
    Public SecondAV As Integer
    Public CastingArtFormID As UInteger
    Public HitEffectArtFormID As UInteger
    Public ImpactDataFormID As UInteger
    Public SkillUsageMultiplier As Single
    Public DualCastArtFormID As UInteger
    Public DualCastScale As Single
    Public EnchantArtFormID As UInteger
    Public HitVisualsFormID As UInteger
    Public EnchantVisualsFormID As UInteger
    Public EquipAbilityFormID As UInteger
    Public ImageSpaceModFormID As UInteger
    Public PerkToApplyFormID As UInteger
    Public CastingSoundLevel As UInteger
    Public ScriptEffectAIScore As Single
    Public ScriptEffectAIDelayTime As Single

    ' Counter effects
    Public CounterEffectFormIDs As New List(Of UInteger)

    ' Sounds
    Public Sounds As New List(Of KeyValuePair(Of UInteger, UInteger)) ' Type, SNDR FormID
End Class

''' <summary>Fallout 4 PERK record - Perk.</summary>
Public Class PERK_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public IconPath As String = ""
    Public SWFPath As String = ""
    Public SoundFormID As UInteger
    Public NextPerkFormID As UInteger

    ' DATA
    Public IsTrait As Boolean
    Public Level As Byte
    Public NumRanks As Byte
    Public IsPlayable As Boolean
    Public IsHidden As Boolean

    ' Effects (simplified)
    Public EffectCount As Integer
End Class

''' <summary>Fallout 4 LVSP record - Leveled Spell.</summary>
Public Class LVSP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ChanceNone As Byte
    Public Flags As Byte
    Public Entries As New List(Of LVSP_Entry)

    Public ReadOnly Property UseAll As Boolean
        Get
            Return (Flags And &H4) <> 0
        End Get
    End Property
End Class

Public Class LVSP_Entry
    Public Level As UShort
    Public FormID As UInteger
    Public Count As UShort = 1US
End Class

''' <summary>Fallout 4 KYWD record - Keyword.</summary>
Public Class KYWD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Notes As String = ""
    Public KeywordColor As Color = Color.Empty
    Public KeywordType As UInteger
    Public AttractionRuleFormID As UInteger
End Class

''' <summary>Fallout 4 EQUP record - Equip Type.</summary>
Public Class EQUP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public SlotParentFormIDs As New List(Of UInteger)
    Public Flags As UInteger
    Public ConditionActorValueFormID As UInteger

    Public ReadOnly Property UseAllParents As Boolean
        Get
            Return (Flags And &H1UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsItemSlot As Boolean
        Get
            Return (Flags And &H4UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 GLOB record - Global Variable.</summary>
Public Class GLOB_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ValueType As Byte  ' 's'=Short, 'l'=Long, 'f'=Float, 'b'=Boolean
    Public Value As Single
End Class

''' <summary>Fallout 4 GMST record - Game Setting.</summary>
Public Class GMST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public StringValue As String = ""
    Public IntValue As Integer
    Public FloatValue As Single
    Public BoolValue As Boolean
    Public DataType As Char  ' 's'=String, 'i'=Int, 'f'=Float, 'b'=Bool (derived from EditorID prefix)
End Class

''' <summary>Fallout 4 AVIF record - Actor Value Information.</summary>
Public Class AVIF_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public Abbreviation As String = ""
    Public DefaultValue As Single
    Public AVFlags As UInteger
    Public AVType As UInteger
End Class

''' <summary>Fallout 4 DMGT record - Damage Type.</summary>
Public Class DMGT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ActorValueFormIDs As New List(Of UInteger)
    Public SpellFormIDs As New List(Of UInteger)
End Class

#End Region

#Region "Parsers"

Public Module MagicRecordParsers

    Public Function ParseENCH(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ENCH_Data
        Dim e As New ENCH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentEffect As MagicEffect_Entry = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    e.FullName = ResolveStr(rec, sr, pluginManager)
                Case "ENIT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        e.EnchantmentCost = BitConverter.ToInt32(sr.Data, 0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        e.Flags = BitConverter.ToUInt32(sr.Data, 4)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        e.CastType = BitConverter.ToUInt32(sr.Data, 8)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        e.EnchantmentAmount = BitConverter.ToInt32(sr.Data, 12)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 20 Then
                        e.TargetType = BitConverter.ToUInt32(sr.Data, 16)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                        e.EnchantType = BitConverter.ToUInt32(sr.Data, 20)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 28 Then
                        e.ChargeTime = BitConverter.ToSingle(sr.Data, 24)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 32 Then
                        e.BaseEnchantmentFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 28), pluginManager)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 36 Then
                        e.WornRestrictionsFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 32), pluginManager)
                    End If
                Case "EFID"
                    If currentEffect IsNot Nothing Then e.Effects.Add(currentEffect)
                    currentEffect = New MagicEffect_Entry With {
                        .BaseEffectFormID = ResolveFID(rec, sr, pluginManager)
                    }
                Case "EFIT"
                    If currentEffect Is Nothing Then currentEffect = New MagicEffect_Entry()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        currentEffect.Magnitude = BitConverter.ToSingle(sr.Data, 0)
                        currentEffect.Area = BitConverter.ToUInt32(sr.Data, 4)
                        currentEffect.Duration = BitConverter.ToUInt32(sr.Data, 8)
                    End If
            End Select
        Next

        If currentEffect IsNot Nothing Then e.Effects.Add(currentEffect)
        Return e
    End Function

    Public Function ParseSPEL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SPEL_Data
        Dim s As New SPEL_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentEffect As MagicEffect_Entry = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    s.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "ETYP"
                    s.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, s.KeywordFormIDs)
                Case "SPIT"
                    Dim d = sr.Data
                    If d IsNot Nothing AndAlso d.Length >= 36 Then
                        s.BaseCost = BitConverter.ToUInt32(d, 0)
                        s.SpellFlags = BitConverter.ToUInt32(d, 4)
                        s.SpellType = BitConverter.ToUInt32(d, 8)
                        s.ChargeTime = BitConverter.ToSingle(d, 12)
                        s.CastType = BitConverter.ToUInt32(d, 16)
                        s.DeliveryType = BitConverter.ToUInt32(d, 20)
                        s.CastDuration = BitConverter.ToSingle(d, 24)
                        s.Range = BitConverter.ToSingle(d, 28)
                        s.CastingPerkFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 32), pluginManager)
                    End If
                Case "EFID"
                    If currentEffect IsNot Nothing Then s.Effects.Add(currentEffect)
                    currentEffect = New MagicEffect_Entry With {
                        .BaseEffectFormID = ResolveFID(rec, sr, pluginManager)
                    }
                Case "EFIT"
                    If currentEffect Is Nothing Then currentEffect = New MagicEffect_Entry()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        currentEffect.Magnitude = BitConverter.ToSingle(sr.Data, 0)
                        currentEffect.Area = BitConverter.ToUInt32(sr.Data, 4)
                        currentEffect.Duration = BitConverter.ToUInt32(sr.Data, 8)
                    End If
            End Select
        Next

        If currentEffect IsNot Nothing Then s.Effects.Add(currentEffect)
        Return s
    End Function

    Public Function ParseMGEF(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MGEF_Data
        Dim m As New MGEF_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DNAM"
                    m.Description = ResolveStr(rec, sr, pluginManager)
                Case "MDOB"
                    m.MenuDisplayObjectFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, m.KeywordFormIDs)
                Case "DATA"
                    ParseMGEF_DATA(sr, rec, pluginManager, m)
                Case "ESCE"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            m.CounterEffectFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
                Case "SNDD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        For i = 0 To sr.Data.Length - 8 Step 8
                            Dim sndType = BitConverter.ToUInt32(sr.Data, i)
                            Dim sndFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i + 4), pluginManager)
                            m.Sounds.Add(New KeyValuePair(Of UInteger, UInteger)(sndType, sndFormID))
                        Next
                    End If
            End Select
        Next

        Return m
    End Function

    Private Sub ParseMGEF_DATA(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, m As MGEF_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 64 Then Return

        m.EffectFlags = BitConverter.ToUInt32(d, 0)
        m.BaseCost = BitConverter.ToSingle(d, 4)
        m.AssociatedItemFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 8), pm)
        m.ResistValueFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 16), pm)
        m.CounterEffectCount = BitConverter.ToUInt16(d, 20)
        m.CastingLightFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 24), pm)
        m.TaperWeight = BitConverter.ToSingle(d, 28)
        m.HitShaderFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 32), pm)
        m.EnchantShaderFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 36), pm)
        m.MinSkillLevel = BitConverter.ToUInt32(d, 40)
        m.SpellmakingArea = BitConverter.ToUInt32(d, 44)
        m.SpellmakingCastingTime = BitConverter.ToSingle(d, 48)
        m.TaperCurve = BitConverter.ToSingle(d, 52)
        m.TaperDuration = BitConverter.ToSingle(d, 56)
        m.SecondAVWeight = BitConverter.ToSingle(d, 60)

        If d.Length >= 96 Then
            m.EffectType = BitConverter.ToInt32(d, 64)
            m.PrimaryAV = BitConverter.ToInt32(d, 68)
            m.ProjectileFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 72), pm)
            m.ExplosionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 76), pm)
            m.CastingType = BitConverter.ToUInt32(d, 80)
            m.DeliveryType = BitConverter.ToUInt32(d, 84)
            m.SecondAV = BitConverter.ToInt32(d, 88)
            m.CastingArtFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 92), pm)
        End If

        If d.Length >= 132 Then
            m.HitEffectArtFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 96), pm)
            m.ImpactDataFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 100), pm)
            m.SkillUsageMultiplier = BitConverter.ToSingle(d, 104)
            m.DualCastArtFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 108), pm)
            m.DualCastScale = BitConverter.ToSingle(d, 112)
            m.EnchantArtFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 120), pm)
            m.HitVisualsFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 124), pm)
            m.EnchantVisualsFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 128), pm)
        End If

        If d.Length >= 160 Then
            m.EquipAbilityFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 132), pm)
            m.ImageSpaceModFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 136), pm)
            m.PerkToApplyFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 140), pm)
            m.CastingSoundLevel = BitConverter.ToUInt32(d, 144)
            m.ScriptEffectAIScore = BitConverter.ToSingle(d, 148)
            m.ScriptEffectAIDelayTime = BitConverter.ToSingle(d, 152)
        End If
    End Sub

    Public Function ParsePERK(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As PERK_Data
        Dim p As New PERK_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim effectCount = 0

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    p.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    p.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "ICON"
                    p.IconPath = sr.AsString
                Case "SNAM"
                    p.SoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "NNAM"
                    p.NextPerkFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    p.SWFPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 3 Then
                        p.IsTrait = sr.Data(0) <> 0
                        p.Level = sr.Data(1)
                        p.NumRanks = sr.Data(2)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        p.IsPlayable = sr.Data(3) <> 0
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then
                        p.IsHidden = sr.Data(4) <> 0
                    End If
                Case "PRKE"
                    effectCount += 1
            End Select
        Next

        p.EffectCount = effectCount
        Return p
    End Function

    Public Function ParseLVSP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LVSP_Data
        Dim l As New LVSP_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "LVLD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then l.ChanceNone = sr.Data(0)
                Case "LVLF"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then l.Flags = sr.Data(0)
                Case "LVLO"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        Dim entry As New LVSP_Entry With {
                            .Level = BitConverter.ToUInt16(sr.Data, 0),
                            .FormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                        }
                        If sr.Data.Length >= 10 Then entry.Count = BitConverter.ToUInt16(sr.Data, 8)
                        If entry.FormID <> 0UI Then l.Entries.Add(entry)
                    End If
            End Select
        Next

        Return l
    End Function

    Public Function ParseKYWD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As KYWD_Data
        Dim k As New KYWD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    k.FullName = ResolveStr(rec, sr, pluginManager)
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        k.KeywordColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "DNAM"
                    k.Notes = sr.AsString
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        k.KeywordType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "DATA"
                    k.AttractionRuleFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next

        Return k
    End Function

    Public Function ParseEQUP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As EQUP_Data
        Dim e As New EQUP_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            e.SlotParentFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        e.Flags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "ANAM"
                    e.ConditionActorValueFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next

        Return e
    End Function

    Public Function ParseGLOB(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As GLOB_Data
        Dim g As New GLOB_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        g.ValueType = sr.Data(0)
                    End If
                Case "FLTV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        g.Value = BitConverter.ToSingle(sr.Data, 0)
                    End If
            End Select
        Next

        Return g
    End Function

    Public Function ParseGMST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As GMST_Data
        Dim g As New GMST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        ' Data type is determined by the first character of the EditorID
        If g.EditorID.Length > 0 Then
            g.DataType = g.EditorID(0)
        End If

        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing Then
                Select Case g.DataType
                    Case "s"c
                        g.StringValue = ResolveStr(rec, sr, pluginManager)
                    Case "i"c
                        If sr.Data.Length >= 4 Then g.IntValue = BitConverter.ToInt32(sr.Data, 0)
                    Case "f"c
                        If sr.Data.Length >= 4 Then g.FloatValue = BitConverter.ToSingle(sr.Data, 0)
                    Case "b"c
                        If sr.Data.Length >= 4 Then g.BoolValue = BitConverter.ToUInt32(sr.Data, 0) <> 0
                End Select
            End If
        Next

        Return g
    End Function

    Public Function ParseAVIF(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As AVIF_Data
        Dim a As New AVIF_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    a.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "ANAM"
                    a.Abbreviation = ResolveStr(rec, sr, pluginManager)
                Case "NAM0"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.DefaultValue = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "AVFL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.AVFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "NAM1"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.AVType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return a
    End Function

    Public Function ParseDMGT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As DMGT_Data
        Dim d As New DMGT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DNAM" AndAlso sr.Data IsNot Nothing Then
                ' Post form-version 78: pairs of AVIF+SPEL FormIDs
                If sr.Data.Length >= 8 Then
                    For i = 0 To sr.Data.Length - 8 Step 8
                        d.ActorValueFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        d.SpellFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i + 4), pluginManager))
                    Next
                ElseIf sr.Data.Length >= 4 Then
                    ' Pre form-version 78: just actor value indices
                    For i = 0 To sr.Data.Length - 4 Step 4
                        d.ActorValueFormIDs.Add(BitConverter.ToUInt32(sr.Data, i))
                    Next
                End If
            End If
        Next

        Return d
    End Function

End Module

#End Region
