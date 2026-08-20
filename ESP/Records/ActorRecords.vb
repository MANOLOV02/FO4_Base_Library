' ============================================================================
' Actor / Character Related Record Data Classes
' BPTD
' ============================================================================
#Region "Data Classes"

''' <summary>Body part node entry. Replicates 1:1 the per-part struct of BPTD's body
''' parts array. Field order matches the struct's declared order; each field maps to
''' one subrecord inside the part block.</summary>
Public Class BPTD_Part
    ' BPTN — lstring (Part Name, localized)
    Public PartName As String = ""
    ' BPNN — string (Part Node)
    Public NodeName As String = ""
    ' BPNT — string (VATS Target)
    Public VATSTarget As String = ""
    ' BPND — Node Data struct (101 bytes); fields below are the parsed members:
    Public DamageMult As Single = 0.0F                     ' offset 0  (float)
    ''' <summary>SOLO SSE: es un byte con signo, índice de un enum de actor value, no una
    ''' referencia — por eso no comparte campo con <see cref="ActorValueFormID"/>, que en FO4 sí
    ''' es un FormID (a AVIF o NULL).</summary>
    Public ActorValueEnum As Integer

    ''' <summary>SOLO SSE: campo float "Tracking Max Angle". No existe en el BPND de FO4.</summary>
    Public TrackingMaxAngle As Single = 0.0F

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
    ''' <summary>BPND Part Type enum (offset 65). Values:
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
    ''' <summary>BPND Geometry Segment Index (offset 78).
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

#End Region
