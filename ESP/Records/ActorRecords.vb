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

''' <summary>Body part node entry. Replicates 1:1 the per-part struct in xEdit
''' wbDefinitionsFO4.pas:8047-8137 (BPTD body parts array).
''' Field order matches the xEdit struct order; each field maps to one subrecord
''' inside the part block.</summary>
Public Class BPTD_Part
    ' BPTN — lstring (Part Name, localized)
    Public PartName As String = ""
    ' BPNN — string (Part Node)
    Public NodeName As String = ""
    ' BPNT — string (VATS Target)
    Public VATSTarget As String = ""
    ' BPND — Node Data struct (101 bytes); fields below are the parsed members per xEdit:
    Public DamageMult As Single = 0.0F                     ' offset 0  (float)
    Public ExplodableDebrisFormID As UInteger              ' offset 4  (FormID DEBR/NULL)
    Public ExplodableExplosionFormID As UInteger           ' offset 8  (FormID EXPL/NULL)
    Public ExplodableDebrisScale As Single = 0.0F          ' offset 12 (float)
    Public SeverableDebrisFormID As UInteger               ' offset 16 (FormID DEBR/NULL)
    Public SeverableExplosionFormID As UInteger            ' offset 20 (FormID EXPL/NULL)
    Public SeverableDebrisScale As Single = 0.0F           ' offset 24 (float)
    Public CutMin As Single = 0.0F                         ' offset 28 (float)
    Public CutMax As Single = 0.0F                         ' offset 32 (float)
    Public CutRadius As Single = 0.0F                      ' offset 36 (float)
    Public GoreLocalRotateX As Single = 0.0F               ' offset 40 (float angle)
    Public GoreLocalRotateY As Single = 0.0F               ' offset 44 (float angle)
    Public CutTesselation As Single = 0.0F                 ' offset 48 (float)
    Public SeverableImpactDataSetFormID As UInteger        ' offset 52 (FormID IPDS/NULL)
    Public ExplodableImpactDataSetFormID As UInteger       ' offset 56 (FormID IPDS/NULL)
    Public ExplodableLimbReplacementScale As Single = 0.0F ' offset 60 (float)
    ''' <summary>BPND flags byte (offset 64). Bit 0 Severable, 1 Hit Reaction, 2 Hit Reaction Default,
    ''' 3 Explodable, 4 Cut Meat Cap Sever, 5 On Cripple, 6 Explodable Absolute Chance, 7 Show Cripple Geometry.</summary>
    Public Flags As Byte                                   ' offset 64 (u8)
    ''' <summary>BPND Part Type enum (offset 65). Values per wbDefinitionsFO4.pas:8079-8107:
    ''' 0 Torso, 1 Head1, 2 Eye, 3 LookAt, 4 FlyGrab, 5 Head2, 6 LeftArm1, 7 LeftArm2, 8 RightArm1,
    ''' 9 RightArm2, 10 LeftLeg1, 11 LeftLeg2, 12 LeftLeg3, 13 RightLeg1, 14 RightLeg2, 15 RightLeg3,
    ''' 16 Brain, 17 Weapon, 18 Root, 19 COM, 20 Pelvis, 21 Camera, 22 OffsetRoot, 23 LeftFoot,
    ''' 24 RightFoot, 25 FaceTargetSource.</summary>
    Public PartType As Byte                                ' offset 65 (u8)
    Public HealthPercent As Byte                           ' offset 66 (u8)
    Public ActorValueFormID As UInteger                    ' offset 67 (FormID AVIF/NULL)
    Public ToHitChance As Byte                             ' offset 71 (u8)
    Public ExplodableExplosionChance As Byte               ' offset 72 (u8)
    Public NonLethalDismembermentChance As Byte            ' offset 73 (u8)
    Public SeverableDebrisCount As Byte                    ' offset 74 (u8)
    Public ExplodableDebrisCount As Byte                   ' offset 75 (u8)
    Public SeverableDecalCount As Byte                     ' offset 76 (u8)
    Public ExplodableDecalCount As Byte                    ' offset 77 (u8)
    ''' <summary>BPND Geometry Segment Index (offset 78). Per wbDefinitionsFO4.pas:8117.
    ''' Likely indexes into the body mesh NIF's dismember segments (BSDismemberSkinInstance
    ''' partitions). Logged separately for future investigation as potential body-region source.</summary>
    Public GeometrySegmentIndex As Byte                    ' offset 78 (u8)
    Public OnCrippleArtObjectFormID As UInteger            ' offset 79 (FormID ARTO/NULL)
    Public OnCrippleDebrisFormID As UInteger               ' offset 83 (FormID DEBR/NULL)
    Public OnCrippleExplosionFormID As UInteger            ' offset 87 (FormID EXPL/NULL)
    Public OnCrippleImpactDataSetFormID As UInteger        ' offset 91 (FormID IPDS/NULL)
    Public OnCrippleDebrisScale As Single = 0.0F           ' offset 95 (float)
    Public OnCrippleDebrisCount As Byte                    ' offset 99 (u8)
    Public OnCrippleDecalCount As Byte                     ' offset 100 (u8)
    ' NAM1 — string (Limb Replacement Model)
    Public LimbReplacementModel As String = ""
    ' NAM4 — string (Gore Effects - Target Bone) ← bone name, NOT a path
    Public GoreTargetBone As String = ""
    ' NAM5 — Model Information (struct of texture hashes, not a path string). Skipped:
    ' nobody consumes it and the layout is non-trivial (counters + arrays). If we ever
    ' need byte-round-trip, capture sr.Data raw here.
    ' ENAM — string (Hit Reaction - Start)
    Public HitReactionStart As String = ""
    ' FNAM — string (Hit Reaction - End)
    Public HitReactionEnd As String = ""
    ' BNAM — FormID ARTO (Gore Effects - Dismember Blood Art)
    Public DismemberBloodArtFormID As UInteger
    ' INAM — FormID MATT (Gore Effects - Blood Impact Material Type)
    Public BloodImpactMaterialFormID As UInteger
    ' JNAM — FormID MATT (On Cripple - Blood Impact Material Type)
    Public OnCrippleBloodImpactFormID As UInteger
    ' CNAM — FormID TXST (Meat Cap TextureSet)
    Public MeatCapTextureSetFormID As UInteger
    ' NAM2 — FormID TXST (Collar TextureSet)
    Public CollarTextureSetFormID As UInteger
    ' DNAM — string (Twist Variable Prefix)
    Public TwistVariablePrefix As String = ""
End Class

''' <summary>Fallout 4 BPTD record - Body Part Data. Replicates xEdit
''' wbDefinitionsFO4.pas:8043-8144. Record-level fields = EDID + GenericModel
''' (MODL/MODT/MODC/MODS/MODF), then a repeating array of BPTN parts.</summary>
Public Class BPTD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    ' MODL — string (Model FileName, from wbGenericModel) — THE skeleton path the engine
    ' associates with the BPTD. RACE.GNAM points to a BPTD; the BPTD.MODL is the per-race
    ' authoritative skeleton (see Mr Handy → SkeletonRef.nif, etc).
    Public ModelPath As String = ""
    ' MODT/MODC/MODS/MODF — texture hashes / color remap / material swap / flags (binary
    ' structs from wbGenericModel). Skipped: not relevant to skeleton resolution and the
    ' layout is non-trivial. If round-trip becomes a requirement, capture sr.Data raw.
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

    ''' <summary>Parser BPTD replicando 1:1 wbDefinitionsFO4.pas:8043-8144.
    ''' Estructura del record:
    '''   EDID
    '''   wbGenericModel  (MODL/MODT/MODC/MODS/MODF) — record-level model; engine usa MODL como skeleton path canónico
    '''   array Body Parts:
    '''     BPTN (Part Name lstring, abre nuevo part — convención xEdit, primer subrecord del part)
    '''     BPNN (Part Node string)
    '''     BPNT (VATS Target string)
    '''     BPND (Node Data struct, 101 bytes — Damage Mult, FormIDs, flags, part type, etc.)
    '''     NAM1 (Limb Replacement Model string)
    '''     NAM4 (Gore Effects Target Bone string — bone name, no es path)
    '''     NAM5 (Model Information struct — texture hashes, NO se parsea, se descarta)
    '''     ENAM (Hit Reaction - Start string)
    '''     FNAM (Hit Reaction - End string)
    '''     BNAM (Gore Effects - Dismember Blood Art FormID → ARTO)
    '''     INAM (Gore Effects - Blood Impact Material Type FormID → MATT)
    '''     JNAM (On Cripple - Blood Impact Material Type FormID → MATT)
    '''     CNAM (Meat Cap TextureSet FormID → TXST)
    '''     NAM2 (Collar TextureSet FormID → TXST)
    '''     DNAM (Twist Variable Prefix string)
    ''' MODT/MODC/MODS/MODF y NAM5 se descartan (texture hash structs, no relevantes).</summary>
    Public Function ParseBPTD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As BPTD_Data
        Dim b As New BPTD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentPart As BPTD_Part = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                ' --- Record-level (wbGenericModel) ---
                Case "MODL"
                    ' Solo el primer MODL del record (record-level, no per-part — los parts no
                    ' tienen MODL en el schema). Si BPTN ya abrió un part, el MODL siguiente sería
                    ' anómalo; nos quedamos con el primero por seguridad.
                    If b.ModelPath = "" AndAlso currentPart Is Nothing Then
                        b.ModelPath = sr.AsString
                    End If
                Case "MODT", "MODC", "MODS", "MODF"
                    ' Discarded: texture hashes / color remap / material swap / flags (binary).
                    ' --- Per-part subrecords ---
                Case "BPTN"
                    ' BPTN abre nuevo part. Cierra el anterior si había uno.
                    If currentPart IsNot Nothing Then b.Parts.Add(currentPart)
                    currentPart = New BPTD_Part With {
                        .PartName = ResolveStr(rec, sr, pluginManager)
                    }
                Case "BPNN"
                    If currentPart IsNot Nothing Then currentPart.NodeName = sr.AsString
                Case "BPNT"
                    If currentPart IsNot Nothing Then currentPart.VATSTarget = sr.AsString
                Case "BPND"
                    ' Node Data struct (101 bytes) — wbDefinitionsFO4.pas:8051-8124.
                    ' Layout completo (todos los offsets):
                    '   0..3   Damage Mult (float)
                    '   4..7   Explodable - Debris (FormID DEBR/NULL)
                    '   8..11  Explodable - Explosion (FormID EXPL/NULL)
                    '   12..15 Explodable - Debris Scale (float)
                    '   16..19 Severable - Debris (FormID DEBR/NULL)
                    '   20..23 Severable - Explosion (FormID EXPL/NULL)
                    '   24..27 Severable - Debris Scale (float)
                    '   28..31 Cut - Min (float)
                    '   32..35 Cut - Max (float)
                    '   36..39 Cut - Radius (float)
                    '   40..43 Gore Effects - Local Rotate X (float angle)
                    '   44..47 Gore Effects - Local Rotate Y (float angle)
                    '   48..51 Cut - Tesselation (float)
                    '   52..55 Severable - Impact DataSet (FormID IPDS/NULL)
                    '   56..59 Explodable - Impact DataSet (FormID IPDS/NULL)
                    '   60..63 Explodable - Limb Replacement Scale (float)
                    '   64     Flags (u8)
                    '   65     Part Type (u8)
                    '   66     Health Percent (u8)
                    '   67..70 Actor Value (FormID AVIF/NULL)
                    '   71     To Hit Chance (u8)
                    '   72     Explodable - Explosion Chance % (u8)
                    '   73     Non-Lethal Dismemberment Chance (u8)
                    '   74     Severable - Debris Count (u8)
                    '   75     Explodable - Debris Count (u8)
                    '   76     Severable - Decal Count (u8)
                    '   77     Explodable - Decal Count (u8)
                    '   78     Geometry Segment Index (u8)
                    '   79..82 On Cripple - Art Object (FormID ARTO/NULL)
                    '   83..86 On Cripple - Debris (FormID DEBR/NULL)
                    '   87..90 On Cripple - Explosion (FormID EXPL/NULL)
                    '   91..94 On Cripple - Impact DataSet (FormID IPDS/NULL)
                    '   95..98 On Cripple - Debris Scale (float)
                    '   99     On Cripple - Debris Count (u8)
                    '   100    On Cripple - Decal Count (u8)
                    If currentPart IsNot Nothing AndAlso sr.Data IsNot Nothing Then
                        Dim d = sr.Data
                        If d.Length >= 4 Then currentPart.DamageMult = BitConverter.ToSingle(d, 0)
                        If d.Length >= 8 Then currentPart.ExplodableDebrisFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 4), pluginManager)
                        If d.Length >= 12 Then currentPart.ExplodableExplosionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 8), pluginManager)
                        If d.Length >= 16 Then currentPart.ExplodableDebrisScale = BitConverter.ToSingle(d, 12)
                        If d.Length >= 20 Then currentPart.SeverableDebrisFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 16), pluginManager)
                        If d.Length >= 24 Then currentPart.SeverableExplosionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 20), pluginManager)
                        If d.Length >= 28 Then currentPart.SeverableDebrisScale = BitConverter.ToSingle(d, 24)
                        If d.Length >= 32 Then currentPart.CutMin = BitConverter.ToSingle(d, 28)
                        If d.Length >= 36 Then currentPart.CutMax = BitConverter.ToSingle(d, 32)
                        If d.Length >= 40 Then currentPart.CutRadius = BitConverter.ToSingle(d, 36)
                        If d.Length >= 44 Then currentPart.GoreLocalRotateX = BitConverter.ToSingle(d, 40)
                        If d.Length >= 48 Then currentPart.GoreLocalRotateY = BitConverter.ToSingle(d, 44)
                        If d.Length >= 52 Then currentPart.CutTesselation = BitConverter.ToSingle(d, 48)
                        If d.Length >= 56 Then currentPart.SeverableImpactDataSetFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 52), pluginManager)
                        If d.Length >= 60 Then currentPart.ExplodableImpactDataSetFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 56), pluginManager)
                        If d.Length >= 64 Then currentPart.ExplodableLimbReplacementScale = BitConverter.ToSingle(d, 60)
                        If d.Length >= 65 Then currentPart.Flags = d(64)
                        If d.Length >= 66 Then currentPart.PartType = d(65)
                        If d.Length >= 67 Then currentPart.HealthPercent = d(66)
                        If d.Length >= 71 Then currentPart.ActorValueFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 67), pluginManager)
                        If d.Length >= 72 Then currentPart.ToHitChance = d(71)
                        If d.Length >= 73 Then currentPart.ExplodableExplosionChance = d(72)
                        If d.Length >= 74 Then currentPart.NonLethalDismembermentChance = d(73)
                        If d.Length >= 75 Then currentPart.SeverableDebrisCount = d(74)
                        If d.Length >= 76 Then currentPart.ExplodableDebrisCount = d(75)
                        If d.Length >= 77 Then currentPart.SeverableDecalCount = d(76)
                        If d.Length >= 78 Then currentPart.ExplodableDecalCount = d(77)
                        If d.Length >= 79 Then currentPart.GeometrySegmentIndex = d(78)
                        If d.Length >= 83 Then currentPart.OnCrippleArtObjectFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 79), pluginManager)
                        If d.Length >= 87 Then currentPart.OnCrippleDebrisFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 83), pluginManager)
                        If d.Length >= 91 Then currentPart.OnCrippleExplosionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 87), pluginManager)
                        If d.Length >= 95 Then currentPart.OnCrippleImpactDataSetFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 91), pluginManager)
                        If d.Length >= 99 Then currentPart.OnCrippleDebrisScale = BitConverter.ToSingle(d, 95)
                        If d.Length >= 100 Then currentPart.OnCrippleDebrisCount = d(99)
                        If d.Length >= 101 Then currentPart.OnCrippleDecalCount = d(100)
                    End If
                Case "NAM1"
                    If currentPart IsNot Nothing Then currentPart.LimbReplacementModel = sr.AsString
                Case "NAM4"
                    If currentPart IsNot Nothing Then currentPart.GoreTargetBone = sr.AsString
                Case "NAM5"
                    ' Discarded: Model Information struct (texture hashes).
                Case "ENAM"
                    If currentPart IsNot Nothing Then currentPart.HitReactionStart = sr.AsString
                Case "FNAM"
                    If currentPart IsNot Nothing Then currentPart.HitReactionEnd = sr.AsString
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
