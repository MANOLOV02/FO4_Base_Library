Imports System.Drawing
Imports System.Text

' ============================================================================
' Visual Effects / Projectile Record Data Classes and Parsers
' IMGS, IMAD, EFSH, PROJ, EXPL, HAZD, CAMS, CPTH, RFCT, SPGD, GDRY, LENS, ARTO, IPCT, IPDS
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

' ############################################################################
' # ⛔⛔⛔ NO USAR: PARSERS SIN VALIDAR. NO CABLEAR HASTA ARREGLARLOS.          #
' ############################################################################
' Este archivo NO tiene ni un llamador en las tres apps: su unica entrada es
' RecordDispatcher.ParseRecord, que esta marcado <Obsolete> y tampoco se llama
' desde produccion. LEER LA CABECERA DE RecordDispatcher.vb ANTES DE TOCAR ESTO.
'
' Sin defectos MEDIDOS, que NO es lo mismo que validado: el sweep 2026-08-18 solo
' verifico que no crashea y que los FormID que emite existan. NINGUN campo de este
' archivo se comparo campo-a-campo contra wbDefinitionsFO4.pas / wbDefinitionsTES5.pas,
' ni se distinguio FO4 de SSE donde el layout difiere.
'
' UN FormID LEIDO MAL NO FALLA: da un numero plausible y equivocado, sin error. Si
' esto llega al writer, sale un ESP con referencias apuntando a otro mod.
' Decision del usuario 2026-08-18: NO se borran; se arreglan cuando se aborde.
' ############################################################################
#Region "Data Classes"

''' <summary>Fallout 4 IMGS record - Image Space.</summary>
Friend Class IMGS_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend LUTTexture As String = ""

    ' HNAM HDR
    Friend EyeAdaptSpeed As Single
    Friend BloomThreshold As Single
    Friend BloomScale As Single
    Friend TargetLum As Single
    Friend SunlightScale As Single
    Friend SkyScale As Single

    ' CNAM Cinematic
    Friend Saturation As Single
    Friend Brightness As Single
    Friend Contrast As Single

    ' TNAM Tint
    Friend TintAmount As Single
    Friend TintColor As Color = Color.Empty

    ' DNAM DoF
    Friend DoFStrength As Single
    Friend DoFDistance As Single
    Friend DoFRange As Single
End Class

''' <summary>Fallout 4 IMAD record - Image Space Adapter (simplified).</summary>
Friend Class IMAD_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend IsAnimatable As Boolean
    Friend Duration As Single
    Friend IsRadialBlur As Boolean
    Friend IsDoF As Boolean
    ' IMAD contains extensive animation curves - raw data stored for specialized use
    Friend HasData As Boolean
End Class

''' <summary>Fallout 4 EFSH record - Effect Shader.</summary>
Friend Class EFSH_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FillTexture As String = ""
    Friend ParticleShaderTexture As String = ""
    Friend HolesTexture As String = ""
    Friend MembranePaletteTexture As String = ""
    Friend ParticlePaletteTexture As String = ""
    Friend HasData As Boolean
End Class

''' <summary>Fallout 4 PROJ record - Projectile.</summary>
Friend Class PROJ_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend MuzzleFlashModelPath As String = ""

    ' DNAM
    Friend ProjectileFlags As UShort
    Friend ProjectileType As UShort  ' 1=Missile, 2=Lobber, 4=Beam, 8=Flame, 16=Cone, 32=Barrier, 64=Arrow
    Friend Gravity As Single
    Friend Speed As Single = 1000.0F
    Friend Range As Single = 4000.0F
    Friend LightFormID As UInteger
    Friend MuzzleFlashLightFormID As UInteger
    Friend ExplosionProximity As Single
    Friend ExplosionTimer As Single
    Friend ExplosionFormID As UInteger
    Friend SoundFormID As UInteger
    Friend MuzzleFlashDuration As Single
    Friend FadeDuration As Single
    Friend ImpactForce As Single
    Friend CountdownSoundFormID As UInteger
    Friend DisableSoundFormID As UInteger
    Friend DefaultWeaponSourceFormID As UInteger
    Friend ConeSpread As Single
    Friend CollisionRadius As Single = 10.0F
    Friend Lifetime As Single
    Friend RelaunchInterval As Single = 0.25F
    Friend DecalDataFormID As UInteger
    Friend CollisionLayerFormID As UInteger
    Friend VATSProjectileFormID As UInteger

    Friend SoundLevelEnum As UInteger

    Friend ReadOnly Property TypeName As String
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
Friend Class EXPL_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend ImageSpaceModFormID As UInteger

    ' DATA
    Friend LightFormID As UInteger
    Friend Sound1FormID As UInteger
    Friend Sound2FormID As UInteger
    Friend ImpactDataSetFormID As UInteger
    Friend PlacedObjectFormID As UInteger
    Friend SpawnProjectileFormID As UInteger
    Friend Force As Single
    Friend Damage As Single
    Friend InnerRadius As Single
    Friend OuterRadius As Single
    Friend ISRadius As Single
    Friend VerticalOffsetMult As Single
    Friend ExplosionFlags As UInteger
    Friend SoundLevelEnum As UInteger
    Friend Stagger As UInteger
End Class

''' <summary>Fallout 4 HAZD record - Hazard.</summary>
Friend Class HAZD_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ModelPath As String = ""
    Friend ImageSpaceModFormID As UInteger

    ' DNAM
    Friend Limit As UInteger
    Friend Radius As Single
    Friend Lifetime As Single
    Friend ImageSpaceRadius As Single
    Friend TargetInterval As Single = 0.3F
    Friend HazardFlags As UInteger
    Friend EffectFormID As UInteger
    Friend LightFormID As UInteger
    Friend ImpactDataSetFormID As UInteger
    Friend SoundFormID As UInteger
End Class

''' <summary>Fallout 4 CAMS record - Camera Shot.</summary>
Friend Class CAMS_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend ImageSpaceModFormID As UInteger

    ' DATA
    Friend Action As UInteger    ' 0=Shoot, 1=Fly, 2=Hit, 3=Zoom
    Friend Location As UInteger  ' 0=Attacker, 1=Projectile, 2=Target, 3=LeadActor
    Friend Target As UInteger
    Friend CameraFlags As UInteger
    Friend TimeMultPlayer As Single
    Friend TimeMultTarget As Single
    Friend TimeMultGlobal As Single
    Friend MaxTime As Single
    Friend MinTime As Single
    Friend TargetPctBetweenActors As Single
End Class

''' <summary>Fallout 4 CPTH record - Camera Path.</summary>
Friend Class CPTH_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ParentFormID As UInteger
    Friend PreviousFormID As UInteger
    Friend CameraFlags As Byte
    Friend CameraShotFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 RFCT record - Visual Effect.</summary>
Friend Class RFCT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend EffectArtFormID As UInteger
    Friend ShaderFormID As UInteger
    Friend EffectFlags As UInteger
End Class

''' <summary>Fallout 4 SPGD record - Shader Particle Geometry.</summary>
Friend Class SPGD_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ParticleTexture As String = ""
    Friend GravityVelocity As Single
    Friend RotationVelocity As Single
    Friend ParticleSizeX As Single
    Friend ParticleSizeY As Single
    Friend BoxSize As UInteger = 4096
    Friend ParticleDensity As Single
End Class

''' <summary>Fallout 4 GDRY record - God Rays.</summary>
Friend Class GDRY_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Intensity As Single = 1.0F
    Friend AirColorScale As Single = 3.0F
    Friend BackColorScale As Single = 2.0F
    Friend FwdColorScale As Single = 4.0F
    Friend BackPhase As Single
End Class

''' <summary>Fallout 4 LENS record - Lens Flare.</summary>
Friend Class LENS_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ColorInfluence As Single
    Friend FadeDistanceRadiusScale As Single = 1.0F
    Friend SpriteCount As UInteger
End Class

''' <summary>Fallout 4 ARTO record - Art Object.</summary>
Friend Class ARTO_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend ArtType As UInteger  ' 0=Magic Casting, 1=Magic Hit Effect, 2=Enchantment Effect
End Class

''' <summary>Fallout 4 IPCT record - Impact.</summary>
Friend Class IPCT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend ModelPath As String = ""
    Friend EffectDuration As Single = 0.25F
    Friend AngleThreshold As Single = 15.0F
    Friend Orientation As UInteger
End Class

''' <summary>IPDS material-to-impact mapping entry.</summary>
Friend Class IPDS_Entry
    Friend MaterialFormID As UInteger
    Friend ImpactFormID As UInteger
End Class

''' <summary>Fallout 4 IPDS record - Impact Data Set.</summary>
Friend Class IPDS_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend Entries As New List(Of IPDS_Entry)
End Class

#End Region

#Region "Parsers"

Friend Module VisualRecordParsers

    Friend Function ParseIMGS(rec As PluginRecord, pluginManager As PluginManager) As IMGS_Data
        Dim img As New IMGS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "TX00"
                    img.LUTTexture = sr.AsStringGeneral
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

    Friend Function ParseIMAD(rec As PluginRecord, pluginManager As PluginManager) As IMAD_Data
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

    Friend Function ParseEFSH(rec As PluginRecord, pluginManager As PluginManager) As EFSH_Data
        Dim e As New EFSH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "ICON"
                    e.FillTexture = sr.AsStringGeneral
                Case "ICO2"
                    e.ParticleShaderTexture = sr.AsStringGeneral
                Case "NAM7"
                    e.HolesTexture = sr.AsStringGeneral
                Case "NAM8"
                    e.MembranePaletteTexture = sr.AsStringGeneral
                Case "NAM9"
                    e.ParticlePaletteTexture = sr.AsStringGeneral
                Case "DNAM", "DATA"
                    e.HasData = True
            End Select
        Next

        Return e
    End Function

    Friend Function ParsePROJ(rec As PluginRecord, pluginManager As PluginManager) As PROJ_Data
        Dim p As New PROJ_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    p.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If p.ModelPath = "" Then p.ModelPath = sr.AsStringGeneral
                Case "NAM1"
                    p.MuzzleFlashModelPath = sr.AsStringGeneral
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

    Friend Function ParseEXPL(rec As PluginRecord, pluginManager As PluginManager) As EXPL_Data
        Dim e As New EXPL_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    e.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If e.ModelPath = "" Then e.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseHAZD(rec As PluginRecord, pluginManager As PluginManager) As HAZD_Data
        Dim h As New HAZD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    h.FullName = ResolveStr(rec, sr, pluginManager)
                Case "MODL"
                    If h.ModelPath = "" Then h.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseCAMS(rec As PluginRecord, pluginManager As PluginManager) As CAMS_Data
        Dim c As New CAMS_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL"
                    ' FO4 CAMS model uses wbGenericModel → MODL (wbDefinitionsFO4.pas:8232→1044),
                    ' NOT MOD2. The old "MOD2" case never matched, so ModelPath stayed empty.
                    If c.ModelPath = "" Then c.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseCPTH(rec As PluginRecord, pluginManager As PluginManager) As CPTH_Data
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

    Friend Function ParseRFCT(rec As PluginRecord, pluginManager As PluginManager) As RFCT_Data
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

    Friend Function ParseSPGD(rec As PluginRecord, pluginManager As PluginManager) As SPGD_Data
        Dim s As New SPGD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MNAM"
                    s.ParticleTexture = sr.AsStringGeneral
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

    Friend Function ParseGDRY(rec As PluginRecord, pluginManager As PluginManager) As GDRY_Data
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

    Friend Function ParseLENS(rec As PluginRecord, pluginManager As PluginManager) As LENS_Data
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

    Friend Function ParseARTO(rec As PluginRecord, pluginManager As PluginManager) As ARTO_Data
        Dim a As New ARTO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL"
                    If a.ModelPath = "" Then a.ModelPath = sr.AsStringGeneral
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.ArtType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return a
    End Function

    Friend Function ParseIPCT(rec As PluginRecord, pluginManager As PluginManager) As IPCT_Data
        Dim i As New IPCT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MODL"
                    If i.ModelPath = "" Then i.ModelPath = sr.AsStringGeneral
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

    Friend Function ParseIPDS(rec As PluginRecord, pluginManager As PluginManager) As IPDS_Data
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
