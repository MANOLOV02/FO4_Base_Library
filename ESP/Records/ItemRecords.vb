Imports System.Drawing
Imports System.Text

' ============================================================================
' Item / Inventory Record Data Classes and Parsers
' WEAP, AMMO, ALCH, MISC, BOOK, KEYM, LIGH, INGR, CONT, FLOR, NOTE
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
'   FO4 WEAP.SoundAttackLoopFormID 169/169, .SoundAttackFailFormID 289/290,
'        .SoundEquipFormID 249/310, .SoundIdleFormID 146/152 -> FormID inexistentes.
'   SSE WEAP.ResistFormID 3865/3865 y .SkillFormID 1697/1700: son ENUM, NO FormID
'        (wbDefinitionsTES5.pas:10863 itS32 wbActorValueEnum / :4477 itS32 wbSkillEnum)
'        y aca (lineas 503-504) se los pasa por ResolveFIDRaw.
'
' UN FormID LEIDO MAL NO FALLA: da un numero plausible y equivocado, sin error. Si
' esto llega al writer, sale un ESP con referencias apuntando a otro mod.
' Decision del usuario 2026-08-18: NO se borran; se arreglan cuando se aborde.
' ############################################################################
#Region "Data Classes"

''' <summary>Fallout 4 WEAP record - Weapon.</summary>
Friend Class WEAP_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend EquipTypeFormID As UInteger
    Friend EnchantmentFormID As UInteger
    Friend InstanceNamingFormID As UInteger
    Friend TemplateFormID As UInteger
    Friend EmbeddedWeaponModFormID As UInteger
    Friend ImpactDataSetFormID As UInteger
    Friend NPCAddAmmoListFormID As UInteger
    Friend AimModelFormID As UInteger
    Friend ZoomFormID As UInteger
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend WorldModelPath As String = ""
    Friend FirstPersonModelPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)

    ' DNAM struct fields
    Friend AmmoFormID As UInteger
    Friend Speed As Single
    Friend ReloadSpeed As Single
    Friend Reach As Single
    Friend MinRange As Single
    Friend MaxRange As Single
    Friend AttackDelay As Single
    Friend OutOfRangeDamageMult As Single = 0.5F
    Friend OnHit As UInteger
    Friend SkillFormID As UInteger
    Friend ResistFormID As UInteger
    Friend WeaponFlags As UInteger
    Friend Capacity As UShort
    Friend AnimationType As Byte
    Friend SecondaryDamage As Single
    Friend Weight As Single
    Friend Value As UInteger
    Friend BaseDamage As UShort
    Friend SoundLevel As UInteger
    Friend SoundAttackFormID As UInteger
    Friend SoundAttack2DFormID As UInteger
    Friend SoundAttackLoopFormID As UInteger
    Friend SoundAttackFailFormID As UInteger
    Friend SoundIdleFormID As UInteger
    Friend SoundEquipFormID As UInteger
    Friend SoundUnequipFormID As UInteger
    Friend SoundFastEquipFormID As UInteger
    Friend AccuracyBonus As Byte
    Friend AnimAttackSeconds As Single = 0.3F
    Friend ActionPointCost As Single = 20.0F
    Friend FullPowerSeconds As Single
    Friend MinPowerPerShot As Single
    Friend Stagger As UInteger

    ' FNAM firing data
    Friend AnimFireSeconds As Single
    Friend RumbleLeftMotor As Single = 0.5F
    Friend RumbleRightMotor As Single = 1.0F
    Friend RumbleDuration As Single = 0.33F
    Friend AnimReloadSeconds As Single
    Friend SightedTransitionSeconds As Single = 0.25F
    Friend NumProjectiles As Byte = 1
    Friend OverrideProjectileFormID As UInteger
    Friend FiringPattern As UInteger

    ' CRDT critical data
    Friend CritDamageMult As Single = 2.0F
    Friend CritChargeBonus As Single
    Friend CritEffectFormID As UInteger

    ' Damage types (DAMA)
    Friend DamageTypes As New List(Of KeyValuePair(Of UInteger, Single))

    ' Melee speed
    Friend MeleeSpeed As UInteger

    ' Flags - bits per position en wbDefinitionsFO4.pas:13284-13316 (wbFlags array of strings).
    ' Sin gaps hasta bit 23, después unknowns. Verificación cruzada con xEdit display.
    Friend ReadOnly Property IsAutomatic As Boolean
        Get
            ' Spec WEAP.DNAM bit 15 (0x00008000) — antes 0x80 era "Unknown 8".
            Return (WeaponFlags And &H8000UI) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsBoltAction As Boolean
        Get
            ' Spec WEAP.DNAM bit 22 (0x00400000) — antes 0x100 era "Crit Effect - on Death".
            Return (WeaponFlags And &H400000UI) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsNPCsUseAmmo As Boolean
        Get
            ' Spec WEAP.DNAM bit 1 (0x02) — pos coincide con valor, OK.
            Return (WeaponFlags And &H2UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 AMMO record - Ammunition.</summary>
Friend Class AMMO_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ShortName As String = ""
    Friend Description As String = ""
    Friend ModelPath As String = ""
    Friend CasingModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)

    ' DATA struct
    Friend Value As UInteger
    Friend Weight As Single

    ' DNAM struct
    Friend ProjectileFormID As UInteger
    Friend Flags As Byte
    Friend Damage As Single
    Friend Health As UInteger

    Friend ReadOnly Property IsNonPlayable As Boolean
        Get
            Return (Flags And &H2) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 ALCH record - Ingestible (potion/chem/food).</summary>
Friend Class ALCH_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend EquipTypeFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend Weight As Single

    ' ENIT struct
    Friend Value As Integer
    Friend ENITFlags As UInteger
    Friend AddictionFormID As UInteger
    Friend AddictionChance As Single
    Friend ConsumeSound As UInteger
    Friend AddictionName As String = ""

    ' Effects
    Friend Effects As New List(Of MagicEffect_Entry)

    ' Flags - bits per position en wbDefinitionsFO4.pas:6080-6098 (wbFlags array of strings).
    ' Hay GAP grande: tras "Food Item" (pos 1) hay 14 "Unknown" hasta "Medicine" (pos 16) y "Poison" (pos 17).
    Friend ReadOnly Property IsFood As Boolean
        Get
            ' Spec ALCH.ENIT pos 1 (0x02) — coincide.
            Return (ENITFlags And &H2UI) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsMedicine As Boolean
        Get
            ' Spec ALCH.ENIT pos 16 (0x00010000) — antes 0x04 era "Unknown 3".
            Return (ENITFlags And &H10000UI) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsPoison As Boolean
        Get
            ' Spec ALCH.ENIT pos 17 (0x00020000) — antes 0x08 era "Unknown 4".
            Return (ENITFlags And &H20000UI) <> 0
        End Get
    End Property
End Class

''' <summary>Shared magic effect entry used by ALCH, ENCH, SPEL, INGR.</summary>
Friend Class MagicEffect_Entry
    Friend BaseEffectFormID As UInteger
    Friend Magnitude As Single
    Friend Area As UInteger
    Friend Duration As UInteger
End Class

''' <summary>Fallout 4 MISC record - Miscellaneous Item.</summary>
Friend Class MISC_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend Value As Integer
    Friend Weight As Single

    ' Components (CVPA)
    Friend Components As New List(Of KeyValuePair(Of UInteger, UInteger)) ' FormID, Count
End Class

''' <summary>Fallout 4 BOOK record - Book/Holotape.</summary>
Friend Class BOOK_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend Description As String = ""
    Friend BookText As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend InventoryArtFormID As UInteger
    Friend FeaturedItemMessageFormID As UInteger
    Friend Value As UInteger
    Friend Weight As Single

    ' DNAM
    Friend BookFlags As Byte
    Friend TeachesFormID As UInteger
    Friend TextOffsetX As UInteger
    Friend TextOffsetY As UInteger

    ' Flags - bits per position en wbDefinitionsFO4.pas:6276-6282 (wbFlags array of strings).
    ' GAP en pos 3 (Unknown 3) entre AddSpell (pos 2) y AddPerk (pos 4) — mismo patrón que HDPT.
    Friend ReadOnly Property CanBeTaken As Boolean
        Get
            ' Spec BOOK.DNAM pos 1 (0x02 "Can't be Taken") — chequea =0 para "puede tomarse".
            Return (BookFlags And &H2) = 0
        End Get
    End Property

    Friend ReadOnly Property IsAddSpell As Boolean
        Get
            ' Spec BOOK.DNAM pos 2 (0x04 "Add Spell") — coincide.
            Return (BookFlags And &H4) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsAddPerk As Boolean
        Get
            ' Spec BOOK.DNAM pos 4 (0x10 "Add Perk") — antes 0x08 era "Unknown 3".
            Return (BookFlags And &H10) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 KEYM record - Key.</summary>
Friend Class KEYM_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend Value As Integer
    Friend Weight As Single
End Class

''' <summary>Fallout 4 LIGH record - Light.</summary>
Friend Class LIGH_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend GoboTexture As String = ""
    Friend SoundFormID As UInteger
    Friend LensFormID As UInteger
    Friend GodRaysFormID As UInteger

    ' DATA struct
    Friend Time As Integer
    Friend Radius As UInteger
    Friend LightColor As Color = Color.Empty
    Friend LightFlags As UInteger
    Friend FalloffExponent As Single
    Friend FOV As Single
    Friend NearClip As Single
    Friend FlickerPeriod As Single
    Friend FlickerIntensityAmplitude As Single
    Friend FlickerMovementAmplitude As Single
    Friend ConstantAttenuation As Single
    Friend ScalarAttenuation As Single
    Friend ExponentAttenuation As Single
    Friend GodRaysNearClip As Single
    Friend Value As UInteger
    Friend Weight As Single

    Friend FadeValue As Single

    Friend ReadOnly Property CanBeCarried As Boolean
        Get
            Return (LightFlags And &H2UI) <> 0
        End Get
    End Property

    Friend ReadOnly Property IsFlicker As Boolean
        Get
            Return (LightFlags And &H8UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 INGR record - Ingredient.</summary>
Friend Class INGR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""
    Friend EquipTypeFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend Value As Integer
    Friend Weight As Single

    ' ENIT
    Friend IngredientValue As Integer
    Friend IngredientFlags As UInteger

    ' Effects
    Friend Effects As New List(Of MagicEffect_Entry)

    Friend ReadOnly Property IsFood As Boolean
        Get
            Return (IngredientFlags And &H2UI) <> 0
        End Get
    End Property
End Class

''' <summary>Container entry for CONT record.</summary>
Friend Class ContainerItem
    Friend ItemFormID As UInteger
    Friend Count As Integer
End Class

''' <summary>Fallout 4 CONT record - Container.</summary>
Friend Class CONT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend Items As New List(Of ContainerItem)
    Friend OpenSoundFormID As UInteger
    Friend CloseSoundFormID As UInteger
    Friend TakeAllSoundFormID As UInteger
    Friend FilterListFormID As UInteger
    Friend NativeTerminalFormID As UInteger

    ' DATA
    Friend ContainerFlags As Byte
    Friend Weight As Single

    Friend ReadOnly Property IsRespawns As Boolean
        Get
            Return (ContainerFlags And &H2) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 FLOR record - Flora.</summary>
Friend Class FLOR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend ActivateTextOverride As String = ""
    Friend IngredientFormID As UInteger
    Friend HarvestSoundFormID As UInteger
    Friend KeywordFormIDs As New List(Of UInteger)
    Friend Flags As UShort

    ' PFPC
    Friend ProductionSpring As Byte
    Friend ProductionSummer As Byte
    Friend ProductionFall As Byte
    Friend ProductionWinter As Byte
End Class

''' <summary>Fallout 4 NOTE record - Note/Holotape.</summary>
Friend Class NOTE_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend IconPath As String = ""
    Friend MessageIconPath As String = ""

    ' DNAM type
    Friend NoteType As Byte  ' 0=Sound, 1=Voice, 2=Program, 3=Terminal

    ' DATA
    Friend Value As UInteger
    Friend Weight As Single

    ' SNAM (union based on type)
    Friend SoundFormID As UInteger    ' When type=Sound
    Friend SceneFormID As UInteger    ' When type=Voice
    Friend TerminalFormID As UInteger ' When type=Terminal
    Friend ProgramFile As String = ""
End Class

#End Region

#Region "Parsers"

Friend Module ItemRecordParsers

    Friend Function ParseWEAP(rec As PluginRecord, pluginManager As PluginManager) As WEAP_Data
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
                    w.IconPath = sr.AsStringGeneral
                Case "MICO"
                    w.MessageIconPath = sr.AsStringGeneral
                Case "MODL"
                    ' FO4 WEAP world model is wbGenericModel → MODL (wbDefinitionsFO4.pas:13249),
                    ' not MOD2. MOD4 below is the 1st-person model. The old "MOD2" case never matched.
                    w.WorldModelPath = sr.AsStringGeneral
                Case "MOD4"
                    w.FirstPersonModelPath = sr.AsStringGeneral
                Case "ETYP"
                    w.EquipTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "ENCH"
                    w.EnchantmentFormID = ResolveFID(rec, sr, pluginManager)
                Case "INRD"
                    w.InstanceNamingFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNAM"
                    w.TemplateFormID = ResolveFID(rec, sr, pluginManager)
                Case "NNAM"
                    ' ⛔ Game-dependent: FO4 es wbFormIDCk(NNAM, 'Embedded Weapon Mod', [OMOD]); TES5 es
                    ' wbByteArray(NNAM, 'Unused', 0, cpIgnore, False) — un sobrante, NO una referencia.
                    ' Resolverlo en SSE daba 11/11 FormID inexistentes, con ejemplo 0x006C6F42 = ASCII "Blo".
                    If IsFallout4() Then w.EmbeddedWeaponModFormID = ResolveFID(rec, sr, pluginManager)
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

    ''' <summary>⛔⛔⛔ ESTE PARSER DE <c>DNAM</c> ESTÁ INVENTADO Y YA NO EMITE FormID. NO CABLEARLO.
    '''
    ''' <para>Se comparó campo a campo contra el canónico y <b>no corresponde a NINGUNO de los dos juegos</b>:
    ''' <list type="bullet">
    ''' <item><b>TES5/SSE</b> (<c>wbDefinitionsTES5.pas</c>, WEAP → <c>wbStruct(DNAM)</c>) arranca con
    ''' <c>Animation Type</c> <c>itU8</c> + <c>wbUnused(3)</c> + <c>Speed</c> float + <c>Reach</c> float +
    ''' <c>Flags</c> <c>itU16</c> + <c>wbUnused(2)</c> + <c>Sight FOV</c> float + 4 bytes desconocidos +
    ''' <c>Base VATS To-Hit</c> u8 + <c>Attack Animation</c> u8 + <c>#Projectiles</c> u8 +
    ''' <c>Embedded Weapon AV</c> u8 + <c>Range Min</c> float…</item>
    ''' <item><b>FO4</b> (<c>wbDefinitionsFO4.pas</c>, WEAP → <c>wbStruct(DNAM)</c>) es OTRA estructura, más
    ''' grande y con un bloque de flags de 32 bits que no existe en TES5.</item>
    ''' </list>
    ''' Este código leía un FormID de munición en el offset 0 — que en TES5 son el <c>Animation Type</c> y tres
    ''' bytes sin usar, y en FO4 otra cosa distinta. O sea que los offsets no salieron de ninguna spec.</para>
    '''
    ''' <para><b>MEDIDO</b> (<c>Tools\RecordParserSweepProbe</c>, los dos juegos reales): con esos offsets,
    ''' <c>SoundAttackLoop</c> 107/107, <c>SoundAttackFail</c> 289/290, <c>SoundAttack</c> 47/49,
    ''' <c>SoundIdle</c> 110/116, <c>SoundEquip</c> 216/277, <c>SoundUnequip</c> 104/366,
    ''' <c>SoundFastEquip</c> 6/6 en FO4; <c>Resist</c> 3865/3865, <c>Skill</c> 1697/1700,
    ''' <c>Ammo</c> 573/3846, <c>EmbeddedWeaponMod</c> 11/11 en SSE — todos apuntando a records que NO
    ''' EXISTEN.</para>
    '''
    ''' <para><b>Y aunque los offsets fueran correctos, dos de esos campos NO SON FormID en SSE</b>:
    ''' <c>wbInteger('Skill', itS32, wbSkillEnum)</c> (wbDefinitionsTES5.pas:4477) y
    ''' <c>wbInteger('Resist', itS32, wbActorValueEnum)</c> (:10863). En FO4 el equivalente de
    ''' <c>Resist Value</c> sí es <c>wbFormIDCk([AVIF, NULL])</c> (wbDefinitionsFO4.pas:10498). Es
    ''' game-dependent, no una constante.</para>
    '''
    ''' <para><b>Por qué se desactiva en vez de "arreglar los offsets"</b>: un FormID leído del lugar equivocado
    ''' NO falla — devuelve un número plausible, pasa por el remapper de índices de master y sale como una
    ''' referencia válida a otro mod. Dejarlo emitiendo mientras se reescribe la estructura es peor que no
    ''' emitir: cero es visiblemente "no parseado", un FormID inventado no. Los campos numéricos que no son
    ''' referencias tampoco se conservan, porque salen de los MISMOS offsets inventados.</para>
    '''
    ''' <para><b>Qué falta para reactivarlo</b>: escribir las DOS estructuras (FO4 y TES5) campo por campo desde
    ''' <c>wbDefinitions*.pas</c>, despachar por juego, y volver a correr el sweep hasta que las 11 filas de
    ''' arriba den 0/N. Hasta entonces esto deja los campos en su valor por defecto, a propósito.</para></summary>
    Private Sub ParseWEAP_DNAM(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, w As WEAP_Data)
        ' ⛔ Intencionalmente vacío. Ver el summary: los offsets no corresponden a ninguno de los dos juegos y
        ' emitir desde acá fabrica referencias. Los parámetros se conservan para no tocar el call site cuando
        ' se implementen las dos estructuras de verdad.
        Return
    End Sub

    ''' <summary>Igual que <see cref="ParseWEAP_DNAM"/>: los offsets de esta estructura no se verificaron contra
    ''' el canónico y el sweep no pudo medirla por separado (sus campos caen en los mismos contadores). Se deja
    ''' viva porque no emitió ningún FormID marcado como inexistente, pero NO está validada — vale el banner del
    ''' encabezado del archivo.</summary>

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

    Friend Function ParseAMMO(rec As PluginRecord, pluginManager As PluginManager) As AMMO_Data
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
                    If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral
                Case "NAM1"
                    a.CasingModelPath = sr.AsStringGeneral
                ' FO4 AMMO has no ICON/MICO (only wbYNAM, wbDefinitionsFO4.pas) — dead cases removed.
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

    Friend Function ParseALCH(rec As PluginRecord, pluginManager As PluginManager) As ALCH_Data
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
                    If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    a.IconPath = sr.AsStringGeneral
                Case "MICO"
                    a.MessageIconPath = sr.AsStringGeneral
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

    Friend Function ParseMISC(rec As PluginRecord, pluginManager As PluginManager) As MISC_Data
        Dim m As New MISC_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    m.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If m.ModelPath = "" Then m.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    m.IconPath = sr.AsStringGeneral
                Case "MICO"
                    m.MessageIconPath = sr.AsStringGeneral
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

    Friend Function ParseBOOK(rec As PluginRecord, pluginManager As PluginManager) As BOOK_Data
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
                    If b.ModelPath = "" Then b.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    b.IconPath = sr.AsStringGeneral
                Case "MICO"
                    b.MessageIconPath = sr.AsStringGeneral
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

    Friend Function ParseKEYM(rec As PluginRecord, pluginManager As PluginManager) As KEYM_Data
        Dim k As New KEYM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    k.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If k.ModelPath = "" Then k.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    k.IconPath = sr.AsStringGeneral
                Case "MICO"
                    k.MessageIconPath = sr.AsStringGeneral
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

    Friend Function ParseLIGH(rec As PluginRecord, pluginManager As PluginManager) As LIGH_Data
        Dim l As New LIGH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    l.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If l.ModelPath = "" Then l.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    l.IconPath = sr.AsStringGeneral
                Case "MICO"
                    l.MessageIconPath = sr.AsStringGeneral
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, l.KeywordFormIDs)
                Case "NAM0"
                    l.GoboTexture = sr.AsStringGeneral
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

    Friend Function ParseINGR(rec As PluginRecord, pluginManager As PluginManager) As INGR_Data
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
                    If ig.ModelPath = "" Then ig.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    ig.IconPath = sr.AsStringGeneral
                Case "MICO"
                    ig.MessageIconPath = sr.AsStringGeneral
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

    Friend Function ParseCONT(rec As PluginRecord, pluginManager As PluginManager) As CONT_Data
        Dim c As New CONT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    c.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If c.ModelPath = "" Then c.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseFLOR(rec As PluginRecord, pluginManager As PluginManager) As FLOR_Data
        Dim f As New FLOR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    f.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If f.ModelPath = "" Then f.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseNOTE(rec As PluginRecord, pluginManager As PluginManager) As NOTE_Data
        Dim n As New NOTE_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    n.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If n.ModelPath = "" Then n.ModelPath = sr.AsStringGeneral
                Case "ICON"
                    n.IconPath = sr.AsStringGeneral
                ' FO4 NOTE has ICON but no MICO (wbDefinitionsFO4.pas:12839) — dead case removed.
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
                    n.ProgramFile = sr.AsStringGeneral
            End Select
        Next

        Return n
    End Function

#End Region

End Module
