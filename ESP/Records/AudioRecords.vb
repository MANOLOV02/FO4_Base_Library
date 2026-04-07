Imports System.Text

' ============================================================================
' Audio Record Data Classes and Parsers
' SNDR, SNCT, SOPM, MUSC, MUST, REVB, KSSM, AECH, SCSN, STAG, SOUN
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>Fallout 4 SNDR record - Sound Descriptor.</summary>
Public Class SNDR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Notes As String = ""
    Public DescriptorType As UInteger  ' 0=Standard, 1=Compound, 2=AutoWeapon
    Public CategoryFormID As UInteger
    Public AlternateSoundFormID As UInteger
    Public OutputModelFormID As UInteger

    ' LNAM
    Public IsLooping As Boolean
    Public RumbleSend As Byte

    ' Child descriptors (for Compound type)
    Public ChildDescriptorFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 SNCT record - Sound Category.</summary>
Public Class SNCT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public CategoryFlags As UInteger
    Public ParentCategoryFormID As UInteger
    Public MenuSliderCategoryFormID As UInteger
    Public StaticVolumeMult As Single
    Public DefaultMenuValue As Single
    Public MinFrequencyMult As Single
    Public SidechainTargetMult As Single

    Public ReadOnly Property MuteWhenSubmerged As Boolean
        Get
            Return (CategoryFlags And &H1UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 SOPM record - Sound Output Model.</summary>
Public Class SOPM_Data
    Public FormID As UInteger
    Public EditorID As String = ""

    ' NAM1
    Public OutputFlags As Byte
    Public ReverbSendPct As Byte
    Public OutputType As UInteger  ' 0=UsesHRTF, 1=DefinedSpeakerOutput
    Public StaticAttenuation As Single ' stored as S16/100
End Class

''' <summary>MUSC track entry.</summary>
Public Class MUSC_TrackEntry
    Public TrackFormID As UInteger
End Class

''' <summary>Fallout 4 MUSC record - Music Type.</summary>
Public Class MUSC_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public MusicFlags As UInteger
    Public Priority As UShort = 50
    Public DuckingDB As Single
    Public FadeDuration As Single
    Public TrackFormIDs As New List(Of UInteger)

    Public ReadOnly Property PlaysOneSelection As Boolean
        Get
            Return (MusicFlags And &H1UI) <> 0
        End Get
    End Property
End Class

''' <summary>Fallout 4 MUST record - Music Track.</summary>
Public Class MUST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public TrackType As UInteger   ' 0x23CB=Palette, 0x6ED7=SingleTrack, 0xA1DC=SilentTrack
    Public Duration As Single
    Public FadeOut As Single
    Public TrackFileName As String = ""
    Public FinaleFileName As String = ""
    Public LoopBegins As Single
    Public LoopEnds As Single
    Public LoopCount As UInteger
    Public SubTrackFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 REVB record - Reverb Parameters.</summary>
Public Class REVB_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public DecayTimeMs As UShort = 1250
    Public HFReferenceHz As UShort = 800
    Public RoomFilter As SByte
    Public RoomHFFilter As SByte
    Public Reflections As SByte
    Public ReverbAmp As SByte
    Public DecayHFRatio As Single  ' stored as U8/100
    Public ReflectDelayMs As Byte
    Public ReverbDelayMs As Byte
    Public DiffusionPct As Byte = 100
    Public DensityPct As Byte = 100
    Public ReverbClass As UInteger
End Class

''' <summary>Fallout 4 KSSM record - Sound Keyword Mapping.</summary>
Public Class KSSM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public PrimaryDescriptorFormID As UInteger
    Public ExteriorTailFormID As UInteger
    Public VATSDescriptorFormID As UInteger
    Public VATSThreshold As Single
    Public KeywordFormIDs As New List(Of UInteger)
End Class

''' <summary>Fallout 4 AECH record - Audio Effect Chain.</summary>
Public Class AECH_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public EffectType As UInteger  ' 0=BSOverdrive, 1=BSStateVariableFilter, 2=BSDelayEffect
    Public IsEnabled As Boolean
End Class

''' <summary>SCSN category multiplier entry.</summary>
Public Class SCSN_CategoryMult
    Public CategoryFormID As UInteger
    Public Multiplier As Single
End Class

''' <summary>Fallout 4 SCSN record - Audio Category Snapshot.</summary>
Public Class SCSN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Priority As UShort
    Public CategoryMultipliers As New List(Of SCSN_CategoryMult)
End Class

''' <summary>STAG sound entry.</summary>
Public Class STAG_SoundEntry
    Public SoundFormID As UInteger
    Public Action As String = ""
End Class

''' <summary>Fallout 4 STAG record - Animation Sound Tag Set.</summary>
Public Class STAG_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Sounds As New List(Of STAG_SoundEntry)
End Class

''' <summary>Fallout 4 SOUN record - Sound Marker.</summary>
Public Class SOUN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public SoundDescriptorFormID As UInteger

    ' SDSC - Sound Descriptor
    ' REPT - Repeat
    Public RepeatMinTime As Single
    Public RepeatMaxTime As Single
    Public Stackable As Boolean
End Class

#End Region

#Region "Parsers"

Public Module AudioRecordParsers

    Public Function ParseSNDR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SNDR_Data
        Dim s As New SNDR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "NNAM"
                    s.Notes = sr.AsString
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.DescriptorType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "GNAM"
                    s.CategoryFormID = ResolveFID(rec, sr, pluginManager)
                Case "SNAM"
                    s.AlternateSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ONAM"
                    s.OutputModelFormID = ResolveFID(rec, sr, pluginManager)
                Case "LNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.IsLooping = sr.Data(1) <> 0
                        s.RumbleSend = sr.Data(3)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            s.ChildDescriptorFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseSNCT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SNCT_Data
        Dim s As New SNCT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    s.FullName = ResolveStr(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.CategoryFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "PNAM"
                    s.ParentCategoryFormID = ResolveFID(rec, sr, pluginManager)
                Case "ONAM"
                    s.MenuSliderCategoryFormID = ResolveFID(rec, sr, pluginManager)
                Case "VNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        s.StaticVolumeMult = BitConverter.ToUInt16(sr.Data, 0) / 65535.0F
                    End If
                Case "UNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        s.DefaultMenuValue = BitConverter.ToUInt16(sr.Data, 0) / 65535.0F
                    End If
                Case "MNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.MinFrequencyMult = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.SidechainTargetMult = BitConverter.ToSingle(sr.Data, 0)
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseSOPM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SOPM_Data
        Dim s As New SOPM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "NAM1"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.OutputFlags = sr.Data(0)
                        s.ReverbSendPct = sr.Data(3)
                    End If
                Case "MNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        s.OutputType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "VNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        s.StaticAttenuation = BitConverter.ToInt16(sr.Data, 0) / 100.0F
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseMUSC(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MUSC_Data
        Dim m As New MUSC_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.MusicFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.Priority = BitConverter.ToUInt16(sr.Data, 0)
                        If sr.Data.Length >= 4 Then m.DuckingDB = BitConverter.ToUInt16(sr.Data, 2) / 100.0F
                    End If
                Case "WNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.FadeDuration = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            m.TrackFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
            End Select
        Next

        Return m
    End Function

    Public Function ParseMUST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MUST_Data
        Dim m As New MUST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.TrackType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "FLTV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.Duration = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.FadeOut = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "ANAM"
                    m.TrackFileName = sr.AsString
                Case "BNAM"
                    m.FinaleFileName = sr.AsString
                Case "LNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        m.LoopBegins = BitConverter.ToSingle(sr.Data, 0)
                        m.LoopEnds = BitConverter.ToSingle(sr.Data, 4)
                        m.LoopCount = BitConverter.ToUInt32(sr.Data, 8)
                    End If
                Case "SNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            m.SubTrackFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
            End Select
        Next

        Return m
    End Function

    Public Function ParseREVB(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As REVB_Data
        Dim r As New REVB_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        r.DecayTimeMs = BitConverter.ToUInt16(sr.Data, 0)
                        r.HFReferenceHz = BitConverter.ToUInt16(sr.Data, 2)
                        r.RoomFilter = CSByte(sr.Data(4))
                        r.RoomHFFilter = CSByte(sr.Data(5))
                        r.Reflections = CSByte(sr.Data(6))
                        r.ReverbAmp = CSByte(sr.Data(7))
                        r.DecayHFRatio = sr.Data(8) / 100.0F
                        r.ReflectDelayMs = sr.Data(9)
                        r.ReverbDelayMs = sr.Data(10)
                        r.DiffusionPct = sr.Data(11)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 13 Then
                        r.DensityPct = sr.Data(12)
                    End If
                Case "ANAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        r.ReverbClass = BitConverter.ToUInt32(sr.Data, 0)
                    End If
            End Select
        Next

        Return r
    End Function

    Public Function ParseKSSM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As KSSM_Data
        Dim k As New KSSM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DNAM"
                    k.PrimaryDescriptorFormID = ResolveFID(rec, sr, pluginManager)
                Case "ENAM"
                    k.ExteriorTailFormID = ResolveFID(rec, sr, pluginManager)
                Case "VNAM"
                    k.VATSDescriptorFormID = ResolveFID(rec, sr, pluginManager)
                Case "TNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        k.VATSThreshold = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "KNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            k.KeywordFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
            End Select
        Next

        Return k
    End Function

    Public Function ParseAECH(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As AECH_Data
        Dim a As New AECH_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "KNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        a.EffectType = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        a.IsEnabled = sr.Data(0) <> 0
                    End If
            End Select
        Next

        Return a
    End Function

    Public Function ParseSCSN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SCSN_Data
        Dim s As New SCSN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        s.Priority = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        s.CategoryMultipliers.Add(New SCSN_CategoryMult With {
                            .CategoryFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager),
                            .Multiplier = BitConverter.ToSingle(sr.Data, 4)
                        })
                    End If
            End Select
        Next

        Return s
    End Function

    Public Function ParseSTAG(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As STAG_Data
        Dim s As New STAG_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentEntry As STAG_SoundEntry = Nothing

        For Each sr In rec.Subrecords
            If sr.Signature = "TNAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                If currentEntry IsNot Nothing Then s.Sounds.Add(currentEntry)
                currentEntry = New STAG_SoundEntry With {
                    .SoundFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, 0), pluginManager)
                }
                If sr.Data.Length > 4 Then
                    Dim strLen = sr.Data.Length - 4
                    If strLen > 0 AndAlso sr.Data(sr.Data.Length - 1) = 0 Then strLen -= 1
                    currentEntry.Action = Encoding.ASCII.GetString(sr.Data, 4, strLen)
                End If
            End If
        Next

        If currentEntry IsNot Nothing Then s.Sounds.Add(currentEntry)
        Return s
    End Function

    Public Function ParseSOUN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As SOUN_Data
        Dim s As New SOUN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "SDSC"
                    s.SoundDescriptorFormID = ResolveFID(rec, sr, pluginManager)
                Case "REPT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 9 Then
                        s.RepeatMinTime = BitConverter.ToSingle(sr.Data, 0)
                        s.RepeatMaxTime = BitConverter.ToSingle(sr.Data, 4)
                        s.Stackable = sr.Data(8) <> 0
                    End If
            End Select
        Next

        Return s
    End Function

End Module

#End Region
