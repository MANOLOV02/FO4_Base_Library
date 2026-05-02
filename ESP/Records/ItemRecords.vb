Imports System.Drawing
Imports System.Text

' ============================================================================
' Item / Inventory Record Data Classes and Parsers
' WEAP, AMMO, ALCH, MISC, BOOK, KEYM, LIGH, INGR, CONT, FLOR, NOTE
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 WEAP record - Weapon.</summary>
Public Class WEAP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public EquipTypeFormID As UInteger
    Public EnchantmentFormID As UInteger
    Public InstanceNamingFormID As UInteger
    Public TemplateFormID As UInteger
    Public EmbeddedWeaponModFormID As UInteger
    Public ImpactDataSetFormID As UInteger
    Public NPCAddAmmoListFormID As UInteger
    Public AimModelFormID As UInteger
    Public ZoomFormID As UInteger
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public WorldModelPath As String = ""
    Public FirstPersonModelPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)

    ' DNAM struct fields
    Public AmmoFormID As UInteger
    Public Speed As Single
    Public ReloadSpeed As Single
    Public Reach As Single
    Public MinRange As Single
    Public MaxRange As Single
    Public AttackDelay As Single
    Public OutOfRangeDamageMult As Single = 0.5F
    Public OnHit As UInteger
    Public SkillFormID As UInteger
    Public ResistFormID As UInteger
    Public WeaponFlags As UInteger
    Public Capacity As UShort
    Public AnimationType As Byte
    Public SecondaryDamage As Single
    Public Weight As Single
    Public Value As UInteger
    Public BaseDamage As UShort
    Public SoundLevel As UInteger
    Public SoundAttackFormID As UInteger
    Public SoundAttack2DFormID As UInteger
    Public SoundAttackLoopFormID As UInteger
    Public SoundAttackFailFormID As UInteger
    Public SoundIdleFormID As UInteger
    Public SoundEquipFormID As UInteger
    Public SoundUnequipFormID As UInteger
    Public SoundFastEquipFormID As UInteger
    Public AccuracyBonus As Byte
    Public AnimAttackSeconds As Single = 0.3F
    Public ActionPointCost As Single = 20.0F
    Public FullPowerSeconds As Single
    Public MinPowerPerShot As Single
    Public Stagger As UInteger

    ' FNAM firing data
    Public AnimFireSeconds As Single
    Public RumbleLeftMotor As Single = 0.5F
    Public RumbleRightMotor As Single = 1.0F
    Public RumbleDuration As Single = 0.33F
    Public AnimReloadSeconds As Single
    Public SightedTransitionSeconds As Single = 0.25F
    Public NumProjectiles As Byte = 1
    Public OverrideProjectileFormID As UInteger
    Public FiringPattern As UInteger

    ' CRDT critical data
    Public CritDamageMult As Single = 2.0F
    Public CritChargeBonus As Single
    Public CritEffectFormID As UInteger

    ' Damage types (DAMA)
    Public DamageTypes As New List(Of KeyValuePair(Of UInteger, Single))

    ' Melee speed
    Public MeleeSpeed As UInteger

    ' Flags - bits per position en wbDefinitionsFO4.pas:13284-13316 (wbFlags array of strings).
    ' Sin gaps hasta bit 23, después unknowns. Verificación cruzada con xEdit display.
    Public ReadOnly Property IsAutomatic As Boolean
        Get
            ' Spec WEAP.DNAM bit 15 (0x00008000) — antes 0x80 era "Unknown 8".
            Return (WeaponFlags And &H8000UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsBoltAction As Boolean
        Get
            ' Spec WEAP.DNAM bit 22 (0x00400000) — antes 0x100 era "Crit Effect - on Death".
            Return (WeaponFlags And &H400000UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsNPCsUseAmmo As Boolean
        Get
            ' Spec WEAP.DNAM bit 1 (0x02) — pos coincide con valor, OK.
            Return (WeaponFlags And &H2UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 AMMO record - Ammunition.</summary>
Public Class AMMO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ShortName As String = ""
    Public Description As String = ""
    Public ModelPath As String = ""
    Public CasingModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)

    ' DATA struct
    Public Value As UInteger
    Public Weight As Single

    ' DNAM struct
    Public ProjectileFormID As UInteger
    Public Flags As Byte
    Public Damage As Single
    Public Health As UInteger

    Public ReadOnly Property IsNonPlayable As Boolean
        Get
            Return (Flags And &H2) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 ALCH record - Ingestible (potion/chem/food).</summary>
Public Class ALCH_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public EquipTypeFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
    Public Weight As Single

    ' ENIT struct
    Public Value As Integer
    Public ENITFlags As UInteger
    Public AddictionFormID As UInteger
    Public AddictionChance As Single
    Public ConsumeSound As UInteger
    Public AddictionName As String = ""

    ' Effects
    Public Effects As New List(Of MagicEffect_Entry)

    ' Flags - bits per position en wbDefinitionsFO4.pas:6080-6098 (wbFlags array of strings).
    ' Hay GAP grande: tras "Food Item" (pos 1) hay 14 "Unknown" hasta "Medicine" (pos 16) y "Poison" (pos 17).
    Public ReadOnly Property IsFood As Boolean
        Get
            ' Spec ALCH.ENIT pos 1 (0x02) — coincide.
            Return (ENITFlags And &H2UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsMedicine As Boolean
        Get
            ' Spec ALCH.ENIT pos 16 (0x00010000) — antes 0x04 era "Unknown 3".
            Return (ENITFlags And &H10000UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsPoison As Boolean
        Get
            ' Spec ALCH.ENIT pos 17 (0x00020000) — antes 0x08 era "Unknown 4".
            Return (ENITFlags And &H20000UI) <> 0
        End Get
    End Property
End Class

''' <summary>Shared magic effect entry used by ALCH, ENCH, SPEL, INGR.</summary>
Public Class MagicEffect_Entry
    Public BaseEffectFormID As UInteger
    Public Magnitude As Single
    Public Area As UInteger
    Public Duration As UInteger
End Class

''' <summary>Fallout 4 MISC record - Miscellaneous Item.</summary>
Public Class MISC_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public Value As Integer
    Public Weight As Single

    ' Components (CVPA)
    Public Components As New List(Of KeyValuePair(Of UInteger, UInteger)) ' FormID, Count
End Class

''' <summary>Fallout 4 BOOK record - Book/Holotape.</summary>
Public Class BOOK_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public BookText As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public InventoryArtFormID As UInteger
    Public FeaturedItemMessageFormID As UInteger
    Public Value As UInteger
    Public Weight As Single

    ' DNAM
    Public BookFlags As Byte
    Public TeachesFormID As UInteger
    Public TextOffsetX As UInteger
    Public TextOffsetY As UInteger

    ' Flags - bits per position en wbDefinitionsFO4.pas:6276-6282 (wbFlags array of strings).
    ' GAP en pos 3 (Unknown 3) entre AddSpell (pos 2) y AddPerk (pos 4) — mismo patrón que HDPT.
    Public ReadOnly Property CanBeTaken As Boolean
        Get
            ' Spec BOOK.DNAM pos 1 (0x02 "Can't be Taken") — chequea =0 para "puede tomarse".
            Return (BookFlags And &H2) = 0
        End Get
    End Property

    Public ReadOnly Property IsAddSpell As Boolean
        Get
            ' Spec BOOK.DNAM pos 2 (0x04 "Add Spell") — coincide.
            Return (BookFlags And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property IsAddPerk As Boolean
        Get
            ' Spec BOOK.DNAM pos 4 (0x10 "Add Perk") — antes 0x08 era "Unknown 3".
            Return (BookFlags And &H10) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 KEYM record - Key.</summary>
Public Class KEYM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public Value As Integer
    Public Weight As Single
End Class

''' <summary>Fallout 4 LIGH record - Light.</summary>
Public Class LIGH_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public GoboTexture As String = ""
    Public SoundFormID As UInteger
    Public LensFormID As UInteger
    Public GodRaysFormID As UInteger

    ' DATA struct
    Public Time As Integer
    Public Radius As UInteger
    Public LightColor As Color = Color.Empty
    Public LightFlags As UInteger
    Public FalloffExponent As Single
    Public FOV As Single
    Public NearClip As Single
    Public FlickerPeriod As Single
    Public FlickerIntensityAmplitude As Single
    Public FlickerMovementAmplitude As Single
    Public ConstantAttenuation As Single
    Public ScalarAttenuation As Single
    Public ExponentAttenuation As Single
    Public GodRaysNearClip As Single
    Public Value As UInteger
    Public Weight As Single

    Public FadeValue As Single

    Public ReadOnly Property CanBeCarried As Boolean
        Get
            Return (LightFlags And &H2UI) <> 0
        End Get
    End Property

    Public ReadOnly Property IsFlicker As Boolean
        Get
            Return (LightFlags And &H8UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 INGR record - Ingredient.</summary>
Public Class INGR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""
    Public EquipTypeFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
    Public Value As Integer
    Public Weight As Single

    ' ENIT
    Public IngredientValue As Integer
    Public IngredientFlags As UInteger

    ' Effects
    Public Effects As New List(Of MagicEffect_Entry)

    Public ReadOnly Property IsFood As Boolean
        Get
            Return (IngredientFlags And &H2UI) <> 0
        End Get
    End Property
End Class

''' <summary>Container entry for CONT record.</summary>
Public Class ContainerItem
    Public ItemFormID As UInteger
    Public Count As Integer
End Class

''' <summary>Fallout 4 CONT record - Container.</summary>
Public Class CONT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public Items As New List(Of ContainerItem)
    Public OpenSoundFormID As UInteger
    Public CloseSoundFormID As UInteger
    Public TakeAllSoundFormID As UInteger
    Public FilterListFormID As UInteger
    Public NativeTerminalFormID As UInteger

    ' DATA
    Public ContainerFlags As Byte
    Public Weight As Single

    Public ReadOnly Property IsRespawns As Boolean
        Get
            Return (ContainerFlags And &H2) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 FLOR record - Flora.</summary>
Public Class FLOR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public ActivateTextOverride As String = ""
    Public IngredientFormID As UInteger
    Public HarvestSoundFormID As UInteger
    Public KeywordFormIDs As New List(Of UInteger)
    Public Flags As UShort

    ' PFPC
    Public ProductionSpring As Byte
    Public ProductionSummer As Byte
    Public ProductionFall As Byte
    Public ProductionWinter As Byte
End Class

''' <summary>Fallout 4 NOTE record - Note/Holotape.</summary>
Public Class NOTE_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public IconPath As String = ""
    Public MessageIconPath As String = ""

    ' DNAM type
    Public NoteType As Byte  ' 0=Sound, 1=Voice, 2=Program, 3=Terminal

    ' DATA
    Public Value As UInteger
    Public Weight As Single

    ' SNAM (union based on type)
    Public SoundFormID As UInteger    ' When type=Sound
    Public SceneFormID As UInteger    ' When type=Voice
    Public TerminalFormID As UInteger ' When type=Terminal
    Public ProgramFile As String = ""
End Class

#End Region

#Region "Parsers"

Public Module ItemRecordParsers

    Public Function ParseWEAP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As WEAP_Data
        Dim w As New WEAP_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim keywordFormIDs As New List(Of UInteger)

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    w.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    w.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "ICON"
                    w.IconPath = sr.AsString
                Case "MICO"
                    w.MessageIconPath = sr.AsString
                Case "MOD2"
                    w.WorldModelPath = sr.AsString
                Case "MOD4"
                    w.FirstPersonModelPath = sr.AsString
                Case "ETYP"
                    w.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "ENCH"
                    w.EnchantmentFormID = ResolveFID(rec, sr, pluginManager)
                Case "INRD"
                    w.InstanceNamingFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNAM"
                    w.TemplateFormID = ResolveFID(rec, sr, pluginManager)
                Case "NNAM"
                    w.EmbeddedWeaponModFormID = ResolveFID(rec, sr, pluginManager)
                Case "INAM"
                    w.ImpactDataSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "LNAM"
                    w.NPCAddAmmoListFormID = ResolveFID(rec, sr, pluginManager)
                Case "WAMD"
                    w.AimModelFormID = ResolveFID(rec, sr, pluginManager)
                Case "WZMD"
                    w.ZoomFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, w.KeywordFormIDs)
                Case "DNAM"
                    ParseWEAP_DNAM(sr, rec, pluginManager, w)
                Case "FNAM"
                    ParseWEAP_FNAM(sr, rec, pluginManager, w)
                Case "CRDT"
                    ParseWEAP_CRDT(sr, rec, pluginManager, w)
                Case "DAMA"
                    ParseWEAP_DAMA(sr, rec, pluginManager, w)
                Case "MASE"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        w.MeleeSpeed = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return w
    End Function

    Private Sub ParseWEAP_DNAM(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, w As WEAP_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 100 Then Return

        w.AmmoFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 0), pm)
        w.Speed = BitConverter.ToSingle(d, 4)
        w.ReloadSpeed = BitConverter.ToSingle(d, 8)
        w.Reach = BitConverter.ToSingle(d, 12)
        w.MinRange = BitConverter.ToSingle(d, 16)
        w.MaxRange = BitConverter.ToSingle(d, 20)
        w.AttackDelay = BitConverter.ToSingle(d, 24)
        w.OutOfRangeDamageMult = BitConverter.ToSingle(d, 32)
        w.OnHit = BitConverter.ToUInt32(d, 36)
        w.SkillFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 40), pm)
        w.ResistFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 44), pm)
        w.WeaponFlags = BitConverter.ToUInt32(d, 48)
        w.Capacity = BitConverter.ToUInt16(d, 52)
        w.AnimationType = d(54)
        w.SecondaryDamage = BitConverter.ToSingle(d, 56)
        w.Weight = BitConverter.ToSingle(d, 60)
        w.Value = BitConverter.ToUInt32(d, 64)
        w.BaseDamage = BitConverter.ToUInt16(d, 68)
        w.SoundLevel = BitConverter.ToUInt32(d, 70)

        If d.Length >= 102 Then
            w.SoundAttackFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 74), pm)
            w.SoundAttack2DFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 78), pm)
            w.SoundAttackLoopFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 82), pm)
            w.SoundAttackFailFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 86), pm)
            w.SoundIdleFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 90), pm)
            w.SoundEquipFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 94), pm)
            w.SoundUnequipFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 98), pm)
        End If

        If d.Length >= 106 Then
            w.SoundFastEquipFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 102), pm)
        End If

        If d.Length >= 107 Then
            w.AccuracyBonus = d(106)
        End If

        If d.Length >= 111 Then
            w.AnimAttackSeconds = BitConverter.ToSingle(d, 107)
        End If

        If d.Length >= 117 Then
            w.ActionPointCost = BitConverter.ToSingle(d, 113)
        End If

        If d.Length >= 121 Then
            w.FullPowerSeconds = BitConverter.ToSingle(d, 117)
        End If

        If d.Length >= 125 Then
            w.MinPowerPerShot = BitConverter.ToSingle(d, 121)
        End If

        If d.Length >= 129 Then
            w.Stagger = BitConverter.ToUInt32(d, 125)
        End If
    End Sub

    Private Sub ParseWEAP_FNAM(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, w As WEAP_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 20 Then Return

        w.AnimFireSeconds = BitConverter.ToSingle(d, 0)
        w.RumbleLeftMotor = BitConverter.ToSingle(d, 4)
        w.RumbleRightMotor = BitConverter.ToSingle(d, 8)
        w.RumbleDuration = BitConverter.ToSingle(d, 12)
        w.AnimReloadSeconds = BitConverter.ToSingle(d, 16)

        If d.Length >= 28 Then
            w.SightedTransitionSeconds = BitConverter.ToSingle(d, 24)
        End If

        If d.Length >= 29 Then
            w.NumProjectiles = d(28)
        End If

        If d.Length >= 33 Then
            w.OverrideProjectileFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 29), pm)
        End If

        If d.Length >= 37 Then
            w.FiringPattern = BitConverter.ToUInt32(d, 33)
        End If
    End Sub

    Private Sub ParseWEAP_CRDT(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, w As WEAP_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 8 Then Return

        w.CritDamageMult = BitConverter.ToSingle(d, 0)
        w.CritChargeBonus = BitConverter.ToSingle(d, 4)

        If d.Length >= 12 Then
            w.CritEffectFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 8), pm)
        End If
    End Sub

    Private Sub ParseWEAP_DAMA(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, w As WEAP_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 8 Then Return
        For i = 0 To d.Length - 8 Step 8
            Dim dmgType = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, i), pm)
            Dim dmgValue = BitConverter.ToSingle(d, i + 4)
            w.DamageTypes.Add(New KeyValuePair(Of UInteger, Single)(dmgType, dmgValue))
        Next
    End Sub

    Public Function ParseAMMO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As AMMO_Data
        Dim a As New AMMO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "ONAM"
                    a.ShortName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    a.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL"
                    If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "NAM1"
                    a.CasingModelPath = sr.AsString
                Case "ICON"
                    a.IconPath = sr.AsString
                Case "MICO"
                    a.MessageIconPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, a.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        a.Value = BitConverter.ToUInt32(sr.Data, 0)
                        a.Weight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        a.ProjectileFormID = ResolveFID(rec, sr, pluginManager)
                        a.Flags = sr.Data(4)
                        a.Damage = BitConverter.ToSingle(sr.Data, 8)
                        If sr.Data.Length >= 16 Then a.Health = BitConverter.ToUInt32(sr.Data, 12)
                    End If
            End Select
        Next

        Return a
    End Function

    Public Function ParseALCH(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ALCH_Data
        Dim a As New ALCH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentEffect As MagicEffect_Entry = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    a.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    a.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL"
                    If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "ICON"
                    a.IconPath = sr.AsString
                Case "MICO"
                    a.MessageIconPath = sr.AsString
                Case "ETYP"
                    a.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, a.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.Weight = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "ENIT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.Value = BitConverter.ToInt32(sr.Data, 0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        a.ENITFlags = BitConverter.ToUInt32(sr.Data, 4)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        a.AddictionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 8), pluginManager)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        a.AddictionChance = BitConverter.ToSingle(sr.Data, 12)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 20 Then
                        a.ConsumeSound = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 16), pluginManager)
                    End If
                Case "DNAM"
                    a.AddictionName = ResolveStr(rec, sr, pluginManager)
                Case "EFID"
                    If currentEffect IsNot Nothing Then a.Effects.Add(currentEffect)
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

        If currentEffect IsNot Nothing Then a.Effects.Add(currentEffect)
        Return a
    End Function

    Public Function ParseMISC(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MISC_Data
        Dim m As New MISC_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If m.ModelPath = "" Then m.ModelPath = sr.AsString
                Case "ICON"
                    m.IconPath = sr.AsString
                Case "MICO"
                    m.MessageIconPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, m.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        m.Value = BitConverter.ToInt32(sr.Data, 0)
                        m.Weight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "CVPA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        For i = 0 To sr.Data.Length - 8 Step 8
                            Dim compFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager)
                            Dim compCount = BitConverter.ToUInt32(sr.Data, i + 4)
                            m.Components.Add(New KeyValuePair(Of UInteger, UInteger)(compFormID, compCount))
                        Next
                    End If
            End Select
        Next

        Return m
    End Function

    Public Function ParseBOOK(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As BOOK_Data
        Dim b As New BOOK_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    b.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    b.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "CNAM"
                    b.BookText = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL"
                    If b.ModelPath = "" Then b.ModelPath = sr.AsString
                Case "ICON"
                    b.IconPath = sr.AsString
                Case "MICO"
                    b.MessageIconPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, b.KeywordFormIDs)
                Case "INAM"
                    b.InventoryArtFormID = ResolveFID(rec, sr, pluginManager)
                Case "FIMD"
                    b.FeaturedItemMessageFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        b.Value = BitConverter.ToUInt32(sr.Data, 0)
                        b.Weight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        b.BookFlags = sr.Data(0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then
                        b.TeachesFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 1), pluginManager)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 13 Then
                        b.TextOffsetX = BitConverter.ToUInt32(sr.Data, 5)
                        b.TextOffsetY = BitConverter.ToUInt32(sr.Data, 9)
                    End If
            End Select
        Next

        Return b
    End Function

    Public Function ParseKEYM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As KEYM_Data
        Dim k As New KEYM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    k.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If k.ModelPath = "" Then k.ModelPath = sr.AsString
                Case "ICON"
                    k.IconPath = sr.AsString
                Case "MICO"
                    k.MessageIconPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, k.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        k.Value = BitConverter.ToInt32(sr.Data, 0)
                        k.Weight = BitConverter.ToSingle(sr.Data, 4)
                    End If
            End Select
        Next

        Return k
    End Function

    Public Function ParseLIGH(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LIGH_Data
        Dim l As New LIGH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    l.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If l.ModelPath = "" Then l.ModelPath = sr.AsString
                Case "ICON"
                    l.IconPath = sr.AsString
                Case "MICO"
                    l.MessageIconPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, l.KeywordFormIDs)
                Case "NAM0"
                    l.GoboTexture = sr.AsString
                Case "SNAM"
                    l.SoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "LNAM"
                    l.LensFormID = ResolveFID(rec, sr, pluginManager)
                Case "WGDR"
                    l.GodRaysFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.FadeValue = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "DATA"
                    ParseLIGH_DATA(sr, l)
            End Select
        Next

        Return l
    End Function

    Private Sub ParseLIGH_DATA(sr As SubrecordData, l As LIGH_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 48 Then Return

        l.Time = BitConverter.ToInt32(d, 0)
        l.Radius = BitConverter.ToUInt32(d, 4)
        l.LightColor = Color.FromArgb(If(d.Length > 11, d(11), 255), d(8), d(9), d(10))
        l.LightFlags = BitConverter.ToUInt32(d, 12)
        l.FalloffExponent = BitConverter.ToSingle(d, 16)
        l.FOV = BitConverter.ToSingle(d, 20)
        l.NearClip = BitConverter.ToSingle(d, 24)
        l.FlickerPeriod = BitConverter.ToSingle(d, 28)
        l.FlickerIntensityAmplitude = BitConverter.ToSingle(d, 32)
        l.FlickerMovementAmplitude = BitConverter.ToSingle(d, 36)
        l.ConstantAttenuation = BitConverter.ToSingle(d, 40)
        l.ScalarAttenuation = BitConverter.ToSingle(d, 44)

        If d.Length >= 52 Then l.ExponentAttenuation = BitConverter.ToSingle(d, 48)
        If d.Length >= 56 Then l.GodRaysNearClip = BitConverter.ToSingle(d, 52)
        If d.Length >= 60 Then l.Value = BitConverter.ToUInt32(d, 56)
        If d.Length >= 64 Then l.Weight = BitConverter.ToSingle(d, 60)
    End Sub

    Public Function ParseINGR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As INGR_Data
        Dim ig As New INGR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentEffect As MagicEffect_Entry = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    ig.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If ig.ModelPath = "" Then ig.ModelPath = sr.AsString
                Case "ICON"
                    ig.IconPath = sr.AsString
                Case "MICO"
                    ig.MessageIconPath = sr.AsString
                Case "ETYP"
                    ig.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, ig.KeywordFormIDs)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        ig.Value = BitConverter.ToInt32(sr.Data, 0)
                        ig.Weight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "ENIT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        ig.IngredientValue = BitConverter.ToInt32(sr.Data, 0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        ig.IngredientFlags = BitConverter.ToUInt32(sr.Data, 4)
                    End If
                Case "EFID"
                    If currentEffect IsNot Nothing Then ig.Effects.Add(currentEffect)
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

        If currentEffect IsNot Nothing Then ig.Effects.Add(currentEffect)
        Return ig
    End Function

    Public Function ParseCONT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CONT_Data
        Dim c As New CONT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    c.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If c.ModelPath = "" Then c.ModelPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, c.KeywordFormIDs)
                Case "CNTO"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        c.Items.Add(New ContainerItem With {
                            .ItemFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager),
                            .Count = BitConverter.ToInt32(sr.Data, 4)
                        })
                    End If
                Case "SNAM"
                    c.OpenSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "QNAM"
                    c.CloseSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "TNAM"
                    c.TakeAllSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ONAM"
                    c.FilterListFormID = ResolveFID(rec, sr, pluginManager)
                Case "NTRM"
                    c.NativeTerminalFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then
                        c.ContainerFlags = sr.Data(0)
                        c.Weight = BitConverter.ToSingle(sr.Data, 1)
                    End If
            End Select
        Next

        Return c
    End Function

    Public Function ParseFLOR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FLOR_Data
        Dim f As New FLOR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    f.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If f.ModelPath = "" Then f.ModelPath = sr.AsString
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, f.KeywordFormIDs)
                Case "RNAM"
                    f.ActivateTextOverride = ResolveStr(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        f.Flags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "PFIG"
                    f.IngredientFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    f.HarvestSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "PFPC"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        f.ProductionSpring = sr.Data(0)
                        f.ProductionSummer = sr.Data(1)
                        f.ProductionFall = sr.Data(2)
                        f.ProductionWinter = sr.Data(3)
                    End If
            End Select
        Next

        Return f
    End Function

    Public Function ParseNOTE(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As NOTE_Data
        Dim n As New NOTE_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    n.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If n.ModelPath = "" Then n.ModelPath = sr.AsString
                Case "ICON"
                    n.IconPath = sr.AsString
                Case "MICO"
                    n.MessageIconPath = sr.AsString
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        n.NoteType = sr.Data(0)
                    End If
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        n.Value = BitConverter.ToUInt32(sr.Data, 0)
                        n.Weight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "SNAM"
                    Dim fid = ResolveFID(rec, sr, pluginManager)
                    Select Case n.NoteType
                        Case 0 : n.SoundFormID = fid
                        Case 1 : n.SceneFormID = fid
                        Case 3 : n.TerminalFormID = fid
                    End Select
                Case "PNAM"
                    n.ProgramFile = sr.AsString
            End Select
        Next

        Return n
    End Function

#End Region

End Module
