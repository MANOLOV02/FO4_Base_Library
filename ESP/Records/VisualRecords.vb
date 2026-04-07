Imports System.Drawing
Imports System.Text

' ============================================================================
' Visual Effects / Projectile Record Data Classes and Parsers
' IMGS, IMAD, EFSH, PROJ, EXPL, HAZD, CAMS, CPTH, RFCT, SPGD, GDRY, LENS, ARTO, IPCT, IPDS
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 IMGS record - Image Space.</summary>
Public Class IMGS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public LUTTexture As String = ""

    ' HNAM HDR
    Public EyeAdaptSpeed As Single
    Public BloomThreshold As Single
    Public BloomScale As Single
    Public TargetLum As Single
    Public SunlightScale As Single
    Public SkyScale As Single

    ' CNAM Cinematic
    Public Saturation As Single
    Public Brightness As Single
    Public Contrast As Single

    ' TNAM Tint
    Public TintAmount As Single
    Public TintColor As Color = Color.Empty

    ' DNAM DoF
    Public DoFStrength As Single
    Public DoFDistance As Single
    Public DoFRange As Single
End Class

''' <summary>Fallout 4 IMAD record - Image Space Adapter (simplified).</summary>
Public Class IMAD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public IsAnimatable As Boolean
    Public Duration As Single
    Public IsRadialBlur As Boolean
    Public IsDoF As Boolean
    ' IMAD contains extensive animation curves - raw data stored for specialized use
    Public HasData As Boolean
End Class

''' <summary>Fallout 4 EFSH record - Effect Shader.</summary>
Public Class EFSH_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FillTexture As String = ""
    Public ParticleShaderTexture As String = ""
    Public HolesTexture As String = ""
    Public MembranePaletteTexture As String = ""
    Public ParticlePaletteTexture As String = ""
    Public HasData As Boolean
End Class

''' <summary>Fallout 4 PROJ record - Projectile.</summary>
Public Class PROJ_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public MuzzleFlashModelPath As String = ""

    ' DNAM
    Public ProjectileFlags As UShort
    Public ProjectileType As UShort  ' 1=Missile, 2=Lobber, 4=Beam, 8=Flame, 16=Cone, 32=Barrier, 64=Arrow
    Public Gravity As Single
    Public Speed As Single = 1000.0F
    Public Range As Single = 4000.0F
    Public LightFormID As UInteger
    Public MuzzleFlashLightFormID As UInteger
    Public ExplosionProximity As Single
    Public ExplosionTimer As Single
    Public ExplosionFormID As UInteger
    Public SoundFormID As UInteger
    Public MuzzleFlashDuration As Single
    Public FadeDuration As Single
    Public ImpactForce As Single
    Public CountdownSoundFormID As UInteger
    Public DisableSoundFormID As UInteger
    Public DefaultWeaponSourceFormID As UInteger
    Public ConeSpread As Single
    Public CollisionRadius As Single = 10.0F
    Public Lifetime As Single
    Public RelaunchInterval As Single = 0.25F
    Public DecalDataFormID As UInteger
    Public CollisionLayerFormID As UInteger
    Public VATSProjectileFormID As UInteger

    Public SoundLevelEnum As UInteger

    Public ReadOnly Property TypeName As String
        Get
            Select Case ProjectileType
                Case 1 : Return "Missile"
                Case 2 : Return "Lobber"
                Case 4 : Return "Beam"
                Case 8 : Return "Flame"
                Case 16 : Return "Cone"
                Case 32 : Return "Barrier"
                Case 64 : Return "Arrow"
                Case Else : Return $"Unknown({ProjectileType})"
            End Select
        End Get
    End Property
End Class

''' <summary>Fallout 4 EXPL record - Explosion.</summary>
Public Class EXPL_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public ImageSpaceModFormID As UInteger

    ' DATA
    Public LightFormID As UInteger
    Public Sound1FormID As UInteger
    Public Sound2FormID As UInteger
    Public ImpactDataSetFormID As UInteger
    Public PlacedObjectFormID As UInteger
    Public SpawnProjectileFormID As UInteger
    Public Force As Single
    Public Damage As Single
    Public InnerRadius As Single
    Public OuterRadius As Single
    Public ISRadius As Single
    Public VerticalOffsetMult As Single
    Public ExplosionFlags As UInteger
    Public SoundLevelEnum As UInteger
    Public Stagger As UInteger
End Class

''' <summary>Fallout 4 HAZD record - Hazard.</summary>
Public Class HAZD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public ModelPath As String = ""
    Public ImageSpaceModFormID As UInteger

    ' DNAM
    Public Limit As UInteger
    Public Radius As Single
    Public Lifetime As Single
    Public ImageSpaceRadius As Single
    Public TargetInterval As Single = 0.3F
    Public HazardFlags As UInteger
    Public EffectFormID As UInteger
    Public LightFormID As UInteger
    Public ImpactDataSetFormID As UInteger
    Public SoundFormID As UInteger
End Class

''' <summary>Fallout 4 CAMS record - Camera Shot.</summary>
Public Class CAMS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public ImageSpaceModFormID As UInteger

    ' DATA
    Public Action As UInteger    ' 0=Shoot, 1=Fly, 2=Hit, 3=Zoom
    Public Location As UInteger  ' 0=Attacker, 1=Projectile, 2=Target, 3=LeadActor
    Public Target As UInteger
    Public CameraFlags As UInteger
    Public TimeMultPlayer As Single
    Public TimeMultTarget As Single
    Public TimeMultGlobal As Single
    Public MaxTime As Single
    Public MinTime As Single
    Public TargetPctBetweenActors As Single
End Class

''' <summary>Fallout 4 CPTH record - Camera Path.</summary>
Public Class CPTH_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParentFormID As UInteger
    Public PreviousFormID As UInteger
    Public CameraFlags As Byte
    Public CameraShotFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 RFCT record - Visual Effect.</summary>
Public Class RFCT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public EffectArtFormID As UInteger
    Public ShaderFormID As UInteger
    Public EffectFlags As UInteger
End Class

''' <summary>Fallout 4 SPGD record - Shader Particle Geometry.</summary>
Public Class SPGD_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ParticleTexture As String = ""
    Public GravityVelocity As Single
    Public RotationVelocity As Single
    Public ParticleSizeX As Single
    Public ParticleSizeY As Single
    Public BoxSize As UInteger = 4096
    Public ParticleDensity As Single
End Class

''' <summary>Fallout 4 GDRY record - God Rays.</summary>
Public Class GDRY_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Intensity As Single = 1.0F
    Public AirColorScale As Single = 3.0F
    Public BackColorScale As Single = 2.0F
    Public FwdColorScale As Single = 4.0F
    Public BackPhase As Single
End Class

''' <summary>Fallout 4 LENS record - Lens Flare.</summary>
Public Class LENS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ColorInfluence As Single
    Public FadeDistanceRadiusScale As Single = 1.0F
    Public SpriteCount As UInteger
End Class

''' <summary>Fallout 4 ARTO record - Art Object.</summary>
Public Class ARTO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public ArtType As UInteger  ' 0=Magic Casting, 1=Magic Hit Effect, 2=Enchantment Effect
End Class

''' <summary>Fallout 4 IPCT record - Impact.</summary>
Public Class IPCT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ModelPath As String = ""
    Public EffectDuration As Single = 0.25F
    Public AngleThreshold As Single = 15.0F
    Public Orientation As UInteger
End Class

''' <summary>IPDS material-to-impact mapping entry.</summary>
Public Class IPDS_Entry
    Public MaterialFormID As UInteger
    Public ImpactFormID As UInteger
End Class

''' <summary>Fallout 4 IPDS record - Impact Data Set.</summary>
Public Class IPDS_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Entries As New List(Of IPDS_Entry)
End Class

#End Region

#Region "Parsers"

Public Module VisualRecordParsers

    Public Function ParseIMGS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As IMGS_Data
        Dim img As New IMGS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "TX00"
                    img.LUTTexture = sr.AsString
                Case "HNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                        img.EyeAdaptSpeed = BitConverter.ToSingle(sr.Data, 0)
                        img.BloomThreshold = BitConverter.ToSingle(sr.Data, 4)
                        img.BloomScale = BitConverter.ToSingle(sr.Data, 8)
                        img.TargetLum = BitConverter.ToSingle(sr.Data, 12)
                        img.SunlightScale = BitConverter.ToSingle(sr.Data, 16)
                        img.SkyScale = BitConverter.ToSingle(sr.Data, 20)
                    End If
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        img.Saturation = BitConverter.ToSingle(sr.Data, 0)
                        img.Brightness = BitConverter.ToSingle(sr.Data, 4)
                        img.Contrast = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        img.TintAmount = BitConverter.ToSingle(sr.Data, 0)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        Dim rgba = BitConverter.ToUInt32(sr.Data, 4)
                        img.TintColor = Color.FromArgb(CInt(rgba >> 24) And &HFF, CInt(rgba) And &HFF, CInt(rgba >> 8) And &HFF, CInt(rgba >> 16) And &HFF)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        img.DoFStrength = BitConverter.ToSingle(sr.Data, 0)
                        img.DoFDistance = BitConverter.ToSingle(sr.Data, 4)
                        img.DoFRange = BitConverter.ToSingle(sr.Data, 8)
                    End If
            End Select
        Next

        Return img
    End Function

    Public Function ParseIMAD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As IMAD_Data
        Dim i As New IMAD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                i.IsAnimatable = BitConverter.ToUInt32(sr.Data, 0) <> 0
                i.Duration = BitConverter.ToSingle(sr.Data, 4)
                i.HasData = True
            End If
        Next

        Return i
    End Function

    Public Function ParseEFSH(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As EFSH_Data
        Dim e As New EFSH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "ICON"
                    e.FillTexture = sr.AsString
                Case "ICO2"
                    e.ParticleShaderTexture = sr.AsString
                Case "NAM7"
                    e.HolesTexture = sr.AsString
                Case "NAM8"
                    e.MembranePaletteTexture = sr.AsString
                Case "NAM9"
                    e.ParticlePaletteTexture = sr.AsString
                Case "DNAM", "DATA"
                    e.HasData = True
            End Select
        Next

        Return e
    End Function

    Public Function ParsePROJ(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As PROJ_Data
        Dim p As New PROJ_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    p.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If p.ModelPath = "" Then p.ModelPath = sr.AsString
                Case "NAM1"
                    p.MuzzleFlashModelPath = sr.AsString
                Case "VNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        p.SoundLevelEnum = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "DNAM"
                    ParsePROJ_DNAM(sr, rec, pluginManager, p)
            End Select
        Next

        Return p
    End Function

    Private Sub ParsePROJ_DNAM(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, p As PROJ_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 48 Then Return

        p.ProjectileFlags = BitConverter.ToUInt16(d, 0)
        p.ProjectileType = BitConverter.ToUInt16(d, 2)
        p.Gravity = BitConverter.ToSingle(d, 4)
        p.Speed = BitConverter.ToSingle(d, 8)
        p.Range = BitConverter.ToSingle(d, 12)
        p.LightFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 16), pm)
        p.MuzzleFlashLightFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 20), pm)
        p.ExplosionProximity = BitConverter.ToSingle(d, 24)
        p.ExplosionTimer = BitConverter.ToSingle(d, 28)
        p.ExplosionFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 32), pm)
        p.SoundFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 36), pm)
        p.MuzzleFlashDuration = BitConverter.ToSingle(d, 40)
        p.FadeDuration = BitConverter.ToSingle(d, 44)

        If d.Length >= 76 Then
            p.ImpactForce = BitConverter.ToSingle(d, 48)
            p.CountdownSoundFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 52), pm)
            p.DisableSoundFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 56), pm)
            p.DefaultWeaponSourceFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 60), pm)
            p.ConeSpread = BitConverter.ToSingle(d, 64)
            p.CollisionRadius = BitConverter.ToSingle(d, 68)
            p.Lifetime = BitConverter.ToSingle(d, 72)
        End If

        If d.Length >= 88 Then
            p.RelaunchInterval = BitConverter.ToSingle(d, 76)
            p.DecalDataFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 80), pm)
            p.CollisionLayerFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 84), pm)
        End If
    End Sub

    Public Function ParseEXPL(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As EXPL_Data
        Dim e As New EXPL_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    e.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If e.ModelPath = "" Then e.ModelPath = sr.AsString
                Case "MNAM"
                    e.ImageSpaceModFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    Dim d = sr.Data
                    If d IsNot Nothing AndAlso d.Length >= 48 Then
                        e.LightFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 0), pluginManager)
                        e.Sound1FormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 4), pluginManager)
                        e.Sound2FormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 8), pluginManager)
                        e.ImpactDataSetFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 12), pluginManager)
                        e.PlacedObjectFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 16), pluginManager)
                        e.SpawnProjectileFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 20), pluginManager)
                        e.Force = BitConverter.ToSingle(d, 24)
                        e.Damage = BitConverter.ToSingle(d, 28)
                        e.InnerRadius = BitConverter.ToSingle(d, 32)
                        e.OuterRadius = BitConverter.ToSingle(d, 36)
                        e.ISRadius = BitConverter.ToSingle(d, 40)
                        e.VerticalOffsetMult = BitConverter.ToSingle(d, 44)
                    End If
                    If d IsNot Nothing AndAlso d.Length >= 52 Then
                        e.ExplosionFlags = BitConverter.ToUInt32(d, 48)
                    End If
                    If d IsNot Nothing AndAlso d.Length >= 56 Then
                        e.SoundLevelEnum = BitConverter.ToUInt32(d, 52)
                    End If
            End Select
        Next

        Return e
    End Function

    Public Function ParseHAZD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As HAZD_Data
        Dim h As New HAZD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    h.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If h.ModelPath = "" Then h.ModelPath = sr.AsString
                Case "MNAM"
                    h.ImageSpaceModFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    Dim d = sr.Data
                    If d IsNot Nothing AndAlso d.Length >= 28 Then
                        h.Limit = BitConverter.ToUInt32(d, 0)
                        h.Radius = BitConverter.ToSingle(d, 4)
                        h.Lifetime = BitConverter.ToSingle(d, 8)
                        h.ImageSpaceRadius = BitConverter.ToSingle(d, 12)
                        h.TargetInterval = BitConverter.ToSingle(d, 16)
                        h.HazardFlags = BitConverter.ToUInt32(d, 20)
                        h.EffectFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 24), pluginManager)
                    End If
                    If d IsNot Nothing AndAlso d.Length >= 36 Then
                        h.LightFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 28), pluginManager)
                        h.ImpactDataSetFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 32), pluginManager)
                    End If
                    If d IsNot Nothing AndAlso d.Length >= 40 Then
                        h.SoundFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 36), pluginManager)
                    End If
            End Select
        Next

        Return h
    End Function

    Public Function ParseCAMS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CAMS_Data
        Dim c As New CAMS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MOD2"
                    If c.ModelPath = "" Then c.ModelPath = sr.AsString
                Case "MNAM"
                    c.ImageSpaceModFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    Dim d = sr.Data
                    If d IsNot Nothing AndAlso d.Length >= 40 Then
                        c.Action = BitConverter.ToUInt32(d, 0)
                        c.Location = BitConverter.ToUInt32(d, 4)
                        c.Target = BitConverter.ToUInt32(d, 8)
                        c.CameraFlags = BitConverter.ToUInt32(d, 12)
                        c.TimeMultPlayer = BitConverter.ToSingle(d, 16)
                        c.TimeMultTarget = BitConverter.ToSingle(d, 20)
                        c.TimeMultGlobal = BitConverter.ToSingle(d, 24)
                        c.MaxTime = BitConverter.ToSingle(d, 28)
                        c.MinTime = BitConverter.ToSingle(d, 32)
                        c.TargetPctBetweenActors = BitConverter.ToSingle(d, 36)
                    End If
            End Select
        Next

        Return c
    End Function

    Public Function ParseCPTH(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CPTH_Data
        Dim c As New CPTH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "ANAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        c.ParentFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                        c.PreviousFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                    End If
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        c.CameraFlags = sr.Data(0)
                    End If
                Case "SNAM"
                    c.CameraShotFormIDs.Add(ResolveFID(rec, sr, pluginManager))
            End Select
        Next

        Return c
    End Function

    Public Function ParseRFCT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As RFCT_Data
        Dim r As New RFCT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                r.EffectArtFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                r.ShaderFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                r.EffectFlags = BitConverter.ToUInt32(sr.Data, 8)
            End If
        Next

        Return r
    End Function

    Public Function ParseSPGD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SPGD_Data
        Dim s As New SPGD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MNAM"
                    s.ParticleTexture = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 24 Then
                        s.GravityVelocity = BitConverter.ToSingle(sr.Data, 0)
                        s.RotationVelocity = BitConverter.ToSingle(sr.Data, 4)
                        s.ParticleSizeX = BitConverter.ToSingle(sr.Data, 8)
                        s.ParticleSizeY = BitConverter.ToSingle(sr.Data, 12)
                        s.BoxSize = BitConverter.ToUInt32(sr.Data, 16)
                        s.ParticleDensity = BitConverter.ToSingle(sr.Data, 20)
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseGDRY(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As GDRY_Data
        Dim g As New GDRY_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 36 Then
                ' Skip back/fwd/air colors (3x16 bytes = 48), extract scalars
                g.Intensity = BitConverter.ToSingle(sr.Data, 32)
            End If
        Next

        Return g
    End Function

    Public Function ParseLENS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LENS_Data
        Dim l As New LENS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.ColorInfluence = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.FadeDistanceRadiusScale = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "LFSP"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.SpriteCount = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return l
    End Function

    Public Function ParseARTO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ARTO_Data
        Dim a As New ARTO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL"
                    If a.ModelPath = "" Then a.ModelPath = sr.AsString
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.ArtType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return a
    End Function

    Public Function ParseIPCT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As IPCT_Data
        Dim i As New IPCT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL"
                    If i.ModelPath = "" Then i.ModelPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        i.EffectDuration = BitConverter.ToSingle(sr.Data, 0)
                        i.Orientation = BitConverter.ToUInt32(sr.Data, 4)
                        i.AngleThreshold = BitConverter.ToSingle(sr.Data, 8)
                    End If
            End Select
        Next

        Return i
    End Function

    Public Function ParseIPDS(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As IPDS_Data
        Dim i As New IPDS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "PNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                For offset = 0 To sr.Data.Length - 8 Step 8
                    i.Entries.Add(New IPDS_Entry With {
                        .MaterialFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, offset), pluginManager),
                        .ImpactFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, offset + 4), pluginManager)
                    })
                Next
            End If
        Next

        Return i
    End Function

End Module

#End Region
