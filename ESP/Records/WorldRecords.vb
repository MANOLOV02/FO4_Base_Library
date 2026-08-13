Imports System.Drawing
Imports System.Text

' ============================================================================
' World / Environment Record Data Classes and Parsers
' CELL, WRLD, LCTN, NAVM, ECZN, REGN, WATR, WTHR, CLMT, LGTM, LTEX
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 CELL record - Cell.</summary>
Friend Class CELL_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend CellFlags As UShort
    Friend LightingTemplateFormID As UInteger
    Friend WaterHeight As Single
    Friend WaterFormID As UInteger
    Friend LocationFormID As UInteger
    Friend EncounterZoneFormID As UInteger
    Friend MusicFormID As UInteger
    Friend ImageSpaceFormID As UInteger
    Friend AcousticSpaceFormID As UInteger
    Friend GodRaysFormID As UInteger
    Friend SkyWeatherRegionFormID As UInteger

    ' XCLL Lighting data (simplified)
    Friend AmbientColor As Color = Color.Empty
    Friend DirectionalColor As Color = Color.Empty
    Friend FogNearColor As Color = Color.Empty
    Friend FogNear As Single
    Friend FogFar As Single
    Friend FogPower As Single

    ' Grid coordinates (for exterior cells)
    Friend GridX As Integer
    Friend GridY As Integer

    Friend ReadOnly Property IsInterior As Boolean
        Get
            Return (CellFlags And &H1US) <> 0
        End Get
    End Property

    Friend ReadOnly Property HasWater As Boolean
        Get
            Return (CellFlags And &H2US) <> 0
        End Get
    End Property

    ''' <summary>"Player Followers Can't Travel Here" flag (CELL.DATA pos 13 = 0x2000) per
    ''' wbDefinitionsFO4.pas:6336. NOTA: el bit 0x80 que usaba antes era "Show Sky", no fast-travel
    ''' (probablemente port heredado de Skyrim). En FO4 vanilla no existe un flag explícito
    ''' "Player CantFastTravel"; este flag SÓLO afecta a followers (companions), no al player.
    ''' Si el consumer necesitaba "player fast-travel disabled" debe revisar su lógica de negocio.</summary>
    Friend ReadOnly Property CantFastTravel As Boolean
        Get
            Return (CellFlags And &H2000US) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 WRLD record - Worldspace.</summary>
Friend Class WRLD_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ParentWorldFormID As UInteger
    Friend ClimateFormID As UInteger
    Friend WaterFormID As UInteger
    Friend LocationFormID As UInteger
    Friend EncounterZoneFormID As UInteger
    Friend MusicFormID As UInteger
    Friend LightingTemplateFormID As UInteger
    Friend MapImagePath As String = ""
    Friend HDLODDiffuseTexture As String = ""
    Friend HDLODNormalTexture As String = ""
    Friend WaterEnvironmentMap As String = ""
    Friend InheritanceFlags As UShort
    Friend WorldFlags As Byte
    Friend DistantLODMultiplier As Single = 1.0F
End Class

''' <summary>Fallout 4 LCTN record - Location.</summary>
Friend Class LCTN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend ParentLocationFormID As UInteger
    Friend MusicFormID As UInteger
    Friend UnreportedCrimeFactionFormID As UInteger
    Friend WorldLocMarkerRefFormID As UInteger
    Friend WorldLocRadius As Single
    Friend ActorFadeMult As Single = 1.0F
    Friend LocationColor As Color = Color.Empty
    Friend KeywordFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 NAVM record - Navmesh (simplified).</summary>
Friend Class NAVM_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend HasData As Boolean
    ' Navmesh geometry is complex binary data - store raw for specialized use
    Friend RawNVNM As Byte()
End Class

''' <summary>Fallout 4 ECZN record - Encounter Zone.</summary>
Friend Class ECZN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend OwnerFormID As UInteger
    Friend LocationFormID As UInteger
    Friend Rank As SByte
    Friend MinLevel As SByte
    Friend Flags As Byte

    Friend ReadOnly Property NeverResets As Boolean
        Get
            Return (Flags And &H1) <> 0
        End Get
    End Property

    Friend ReadOnly Property MatchPCBelowMin As Boolean
        Get
            Return (Flags And &H2) <> 0
        End Get
    End Property
End Class

''' <summary>REGN weather entry.</summary>
Friend Class REGN_WeatherEntry
    Friend WeatherFormID As UInteger
    Friend Chance As UInteger
    Friend GlobalFormID As UInteger
End Class

''' <summary>Fallout 4 REGN record - Region.</summary>
Friend Class REGN_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend MapColor As Color = Color.Empty
    Friend WorldspaceFormID As UInteger
    Friend MusicFormID As UInteger
    Friend MapName As String = ""
    Friend WeatherTypes As New List(Of REGN_WeatherEntry)
End Class

''' <summary>Fallout 4 WATR record - Water Type.</summary>
Friend Class WATR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend FullName As String = ""
    Friend WaterFlags As Byte
    Friend ConsumeSpellFormID As UInteger
    Friend ContactSpellFormID As UInteger
    Friend ImageSpaceFormID As UInteger
    Friend OpenSoundFormID As UInteger

    ' Visual properties (from DNAM, simplified)
    Friend Opacity As Single
    Friend ShallowColor As Color = Color.Empty
    Friend DeepColor As Color = Color.Empty
    Friend ReflectionColor As Color = Color.Empty
    Friend DepthAmount As Single

    Friend ReadOnly Property IsDangerous As Boolean
        Get
            Return (WaterFlags And &H1) <> 0
        End Get
    End Property
End Class

''' <summary>WTHR sound entry.</summary>
Friend Class WTHR_SoundEntry
    Friend SoundFormID As UInteger
    Friend SoundType As UInteger
End Class

''' <summary>Fallout 4 WTHR record - Weather.</summary>
Friend Class WTHR_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend PrecipitationTypeFormID As UInteger
    Friend VisualEffectFormID As UInteger
    Friend SunGlareLensFlareFormID As UInteger
    Friend MaxCloudLayers As UInteger = 16

    ' DATA
    Friend WindSpeed As Single
    Friend TransDelta As Single
    Friend SunGlare As Single
    Friend SunDamage As Single
    Friend WeatherFlags As UInteger
    Friend VolatilityMult As Single = 1.0F
    Friend VisibilityMult As Single = 1.0F

    ' Cloud textures
    Friend CloudTextures As New List(Of String)

    ' Sounds
    Friend Sounds As New List(Of WTHR_SoundEntry)

    ' Image spaces (Dawn, Day, Dusk, Night)
    Friend ImageSpaceFormIDs As New List(Of UInteger)
    ' God rays
    Friend GodRayFormIDs As New List(Of UInteger)
End Class

''' <summary>CLMT weather entry.</summary>
Friend Class CLMT_WeatherEntry
    Friend WeatherFormID As UInteger
    Friend Chance As Integer
    Friend GlobalFormID As UInteger
End Class

''' <summary>Fallout 4 CLMT record - Climate.</summary>
Friend Class CLMT_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend SunTexture As String = ""
    Friend SunGlareTexture As String = ""
    Friend SkyModelPath As String = ""
    Friend WeatherTypes As New List(Of CLMT_WeatherEntry)
End Class

''' <summary>Fallout 4 LGTM record - Lighting Template.</summary>
Friend Class LGTM_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""

    ' DATA - lighting parameters
    Friend AmbientColor As Color = Color.Empty
    Friend DirectionalColor As Color = Color.Empty
    Friend FogNearColor As Color = Color.Empty
    Friend FogFarColor As Color = Color.Empty
    Friend FogNear As Single
    Friend FogFar As Single
    Friend FogMax As Single
    Friend FogPower As Single
    Friend DirectionalRotationXY As Single
    Friend DirectionalRotationZ As Single
    Friend DirectionalFade As Single
    Friend LightFadeBegin As Single
    Friend LightFadeEnd As Single
End Class

''' <summary>Fallout 4 LTEX record - Landscape Texture.</summary>
Friend Class LTEX_Data
    Friend FormID As UInteger
    Friend EditorID As String = ""
    Friend TextureSetFormID As UInteger
    Friend MaterialTypeFormID As UInteger
    Friend HavokFriction As Byte = 30
    Friend HavokRestitution As Byte = 30
    Friend TextureSpecularExponent As Byte = 30
    Friend GrassFormIDs As New List(Of UInteger)
End Class

#End Region

#Region "Parsers"

Friend Module WorldRecordParsers

    Friend Function ParseCELL(rec As PluginRecord, pluginManager As PluginManager) As CELL_Data
        Dim c As New CELL_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    c.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        c.CellFlags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "LTMP"
                    c.LightingTemplateFormID = ResolveFID(rec, sr, pluginManager)
                Case "XCLW"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        c.WaterHeight = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "XCWT"
                    c.WaterFormID = ResolveFID(rec, sr, pluginManager)
                Case "XLCN"
                    c.LocationFormID = ResolveFID(rec, sr, pluginManager)
                Case "XEZN"
                    c.EncounterZoneFormID = ResolveFID(rec, sr, pluginManager)
                Case "XCMO"
                    c.MusicFormID = ResolveFID(rec, sr, pluginManager)
                Case "XCIM"
                    c.ImageSpaceFormID = ResolveFID(rec, sr, pluginManager)
                Case "XCAS"
                    c.AcousticSpaceFormID = ResolveFID(rec, sr, pluginManager)
                Case "XGDR"
                    c.GodRaysFormID = ResolveFID(rec, sr, pluginManager)
                Case "XCCM"
                    c.SkyWeatherRegionFormID = ResolveFID(rec, sr, pluginManager)
                Case "XCLL"
                    ParseCELL_XCLL(sr, c)
                Case "XCLC"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        c.GridX = BitConverter.ToInt32(sr.Data, 0)
                        c.GridY = BitConverter.ToInt32(sr.Data, 4)
                    End If
            End Select
        Next

        Return c
    End Function

    Private Sub ParseCELL_XCLL(sr As SubrecordData, c As CELL_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 32 Then Return
        c.AmbientColor = Color.FromArgb(d(3), d(0), d(1), d(2))
        c.DirectionalColor = Color.FromArgb(d(7), d(4), d(5), d(6))
        c.FogNearColor = Color.FromArgb(d(11), d(8), d(9), d(10))
        If d.Length >= 20 Then c.FogNear = BitConverter.ToSingle(d, 12)
        If d.Length >= 24 Then c.FogFar = BitConverter.ToSingle(d, 16)
        If d.Length >= 36 Then c.FogPower = BitConverter.ToSingle(d, 32)
    End Sub

    Friend Function ParseWRLD(rec As PluginRecord, pluginManager As PluginManager) As WRLD_Data
        Dim w As New WRLD_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    w.FullName = ResolveStr(rec, sr, pluginManager)
                Case "WNAM"
                    w.ParentWorldFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNAM"
                    w.ClimateFormID = ResolveFID(rec, sr, pluginManager)
                Case "NAM2"
                    w.WaterFormID = ResolveFID(rec, sr, pluginManager)
                Case "XLCN"
                    w.LocationFormID = ResolveFID(rec, sr, pluginManager)
                Case "XEZN"
                    w.EncounterZoneFormID = ResolveFID(rec, sr, pluginManager)
                Case "ZNAM"
                    w.MusicFormID = ResolveFID(rec, sr, pluginManager)
                Case "LTMP"
                    w.LightingTemplateFormID = ResolveFID(rec, sr, pluginManager)
                Case "ICON"
                    w.MapImagePath = sr.AsStringGeneral
                Case "TNAM"
                    w.HDLODDiffuseTexture = sr.AsStringGeneral
                Case "UNAM"
                    w.HDLODNormalTexture = sr.AsStringGeneral
                Case "XWEM"
                    w.WaterEnvironmentMap = sr.AsStringGeneral
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        w.InheritanceFlags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        w.WorldFlags = sr.Data(0)
                    End If
                Case "NAMA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        w.DistantLODMultiplier = BitConverter.ToSingle(sr.Data, 0)
                    End If
            End Select
        Next

        Return w
    End Function

    Friend Function ParseLCTN(rec As PluginRecord, pluginManager As PluginManager) As LCTN_Data
        Dim l As New LCTN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    l.FullName = ResolveStr(rec, sr, pluginManager)
                Case "PNAM"
                    l.ParentLocationFormID = ResolveFID(rec, sr, pluginManager)
                Case "NAM1"
                    l.MusicFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    l.UnreportedCrimeFactionFormID = ResolveFID(rec, sr, pluginManager)
                Case "MNAM"
                    l.WorldLocMarkerRefFormID = ResolveFID(rec, sr, pluginManager)
                Case "RNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.WorldLocRadius = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "ANAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.ActorFadeMult = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        l.LocationColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "KWDA"
                    ParseFormIDArray(sr, rec, pluginManager, l.KeywordFormIDs)
            End Select
        Next

        Return l
    End Function

    Friend Function ParseNAVM(rec As PluginRecord, pluginManager As PluginManager) As NAVM_Data
        Dim n As New NAVM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "NVNM" Then
                n.HasData = True
                n.RawNVNM = sr.Data
                Exit For
            End If
        Next

        Return n
    End Function

    Friend Function ParseECZN(rec As PluginRecord, pluginManager As PluginManager) As ECZN_Data
        Dim e As New ECZN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                e.OwnerFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                e.LocationFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
                ' Rank/MinLevel are s8 per xEdit. Use ReadInt8 to bit-reinterpret bytes ≥ 128
                ' (direct CSByte overflows for negative s8 values like rank = -1).
                If sr.Data.Length >= 9 Then e.Rank = RecordParsers.ReadInt8(sr.Data(8))
                If sr.Data.Length >= 10 Then e.MinLevel = RecordParsers.ReadInt8(sr.Data(9))
                If sr.Data.Length >= 11 Then e.Flags = sr.Data(10)
            End If
        Next

        Return e
    End Function

    Friend Function ParseREGN(rec As PluginRecord, pluginManager As PluginManager) As REGN_Data
        Dim r As New REGN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "RCLR"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        r.MapColor = Color.FromArgb(sr.Data(3), sr.Data(0), sr.Data(1), sr.Data(2))
                    End If
                Case "WNAM"
                    r.WorldspaceFormID = ResolveFID(rec, sr, pluginManager)
                Case "RDMO"
                    r.MusicFormID = ResolveFID(rec, sr, pluginManager)
                Case "RDMP"
                    r.MapName = ResolveStr(rec, sr, pluginManager)
                Case "RDWT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        For i = 0 To sr.Data.Length - 12 Step 12
                            r.WeatherTypes.Add(New REGN_WeatherEntry With {
                                .WeatherFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager),
                                .Chance = BitConverter.ToUInt32(sr.Data, i + 4),
                                .GlobalFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i + 8), pluginManager)
                            })
                        Next
                    End If
            End Select
        Next

        Return r
    End Function

    Friend Function ParseWATR(rec As PluginRecord, pluginManager As PluginManager) As WATR_Data
        Dim w As New WATR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    w.FullName = ResolveStr(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then w.WaterFlags = sr.Data(0)
                Case "SNAM"
                    w.OpenSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "XNAM"
                    w.ConsumeSpellFormID = ResolveFID(rec, sr, pluginManager)
                Case "YNAM"
                    w.ContactSpellFormID = ResolveFID(rec, sr, pluginManager)
                Case "INAM"
                    w.ImageSpaceFormID = ResolveFID(rec, sr, pluginManager)
                Case "DNAM"
                    ' DNAM is a large struct with fog/visual properties; extract key values
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        w.DepthAmount = BitConverter.ToSingle(sr.Data, 0)
                    End If
            End Select
        Next

        Return w
    End Function

    Friend Function ParseWTHR(rec As PluginRecord, pluginManager As PluginManager) As WTHR_Data
        Dim w As New WTHR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "LNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        w.MaxCloudLayers = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MNAM"
                    w.PrecipitationTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "NNAM"
                    w.VisualEffectFormID = ResolveFID(rec, sr, pluginManager)
                Case "GNAM"
                    w.SunGlareLensFlareFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        w.WindSpeed = BitConverter.ToSingle(sr.Data, 0)
                        w.TransDelta = BitConverter.ToSingle(sr.Data, 4)
                        w.SunGlare = BitConverter.ToSingle(sr.Data, 8)
                        w.SunDamage = BitConverter.ToSingle(sr.Data, 12)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 20 Then
                        w.WeatherFlags = BitConverter.ToUInt32(sr.Data, 16)
                    End If
                Case "VNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        w.VolatilityMult = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "WNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        w.VisibilityMult = BitConverter.ToSingle(sr.Data, 0)
                    End If
            End Select
        Next

        Return w
    End Function

    Friend Function ParseCLMT(rec As PluginRecord, pluginManager As PluginManager) As CLMT_Data
        Dim c As New CLMT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FNAM"
                    c.SunTexture = sr.AsStringGeneral
                Case "GNAM"
                    c.SunGlareTexture = sr.AsStringGeneral
                Case "MODL"
                    ' FO4 CLMT sky model uses wbGenericModel → MODL (wbDefinitionsFO4.pas:6481→1044),
                    ' NOT MOD2. The old "MOD2" case never matched, so SkyModelPath stayed empty.
                    c.SkyModelPath = sr.AsStringGeneral
                Case "WLST"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        For i = 0 To sr.Data.Length - 12 Step 12
                            c.WeatherTypes.Add(New CLMT_WeatherEntry With {
                                .WeatherFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager),
                                .Chance = BitConverter.ToInt32(sr.Data, i + 4),
                                .GlobalFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i + 8), pluginManager)
                            })
                        Next
                    End If
            End Select
        Next

        Return c
    End Function

    Friend Function ParseLGTM(rec As PluginRecord, pluginManager As PluginManager) As LGTM_Data
        Dim l As New LGTM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "DATA" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 40 Then
                Dim d = sr.Data
                l.AmbientColor = Color.FromArgb(d(3), d(0), d(1), d(2))
                l.DirectionalColor = Color.FromArgb(d(7), d(4), d(5), d(6))
                l.FogNearColor = Color.FromArgb(d(11), d(8), d(9), d(10))
                l.FogNear = BitConverter.ToSingle(d, 16)
                l.FogFar = BitConverter.ToSingle(d, 20)
                If d.Length >= 28 Then l.DirectionalRotationXY = BitConverter.ToSingle(d, 24)
                If d.Length >= 32 Then l.DirectionalRotationZ = BitConverter.ToSingle(d, 28)
                If d.Length >= 36 Then l.DirectionalFade = BitConverter.ToSingle(d, 32)
                If d.Length >= 40 Then l.FogMax = BitConverter.ToSingle(d, 36)
                If d.Length >= 44 Then l.LightFadeBegin = BitConverter.ToSingle(d, 40)
                If d.Length >= 48 Then l.LightFadeEnd = BitConverter.ToSingle(d, 44)
                If d.Length >= 56 Then l.FogPower = BitConverter.ToSingle(d, 52)
            End If
        Next

        Return l
    End Function

    Friend Function ParseLTEX(rec As PluginRecord, pluginManager As PluginManager) As LTEX_Data
        Dim l As New LTEX_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "TNAM"
                    l.TextureSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "MNAM"
                    l.MaterialTypeFormID = ResolveFID(rec, sr, pluginManager)
                Case "HNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        l.HavokFriction = sr.Data(0)
                        l.HavokRestitution = sr.Data(1)
                    End If
                Case "SNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        l.TextureSpecularExponent = sr.Data(0)
                    End If
                Case "GNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            l.GrassFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
            End Select
        Next

        Return l
    End Function

End Module

#End Region
