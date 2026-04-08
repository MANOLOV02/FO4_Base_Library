Imports System.Drawing
Imports System.Text

Public Enum NPC_TemplateCategory As Integer
    Traits = 0
    Stats = 1
    Factions = 2
    SpellList = 3
    AIData = 4
    AIPackages = 5
    ModelAnimation = 6
    BaseData = 7
    Inventory = 8
    Script = 9
    DefaultPackageList = 10
    AttackData = 11
    Keywords = 12
End Enum

Public Class NPC_FaceTintLayerData
    Public DataType As UShort
    Public Index As UShort
    Public Value As Integer
    Public Color As Color = Color.Empty
    Public TemplateColorIndex As Short
End Class

Public Class NPC_FaceMorphData
    Public Index As UInteger
    ''' <summary>Raw float values from FMRS. Typically: PosX, PosY, PosZ, RotX, RotY, RotZ, Scale, plus optional unknowns.</summary>
    Public Values As New List(Of Single)

    Public ReadOnly Property PositionX As Single
        Get
            Return If(Values.Count > 0, Values(0), 0.0F)
        End Get
    End Property
    Public ReadOnly Property PositionY As Single
        Get
            Return If(Values.Count > 1, Values(1), 0.0F)
        End Get
    End Property
    Public ReadOnly Property PositionZ As Single
        Get
            Return If(Values.Count > 2, Values(2), 0.0F)
        End Get
    End Property
    Public ReadOnly Property RotationX As Single
        Get
            Return If(Values.Count > 3, Values(3), 0.0F)
        End Get
    End Property
    Public ReadOnly Property RotationY As Single
        Get
            Return If(Values.Count > 4, Values(4), 0.0F)
        End Get
    End Property
    Public ReadOnly Property RotationZ As Single
        Get
            Return If(Values.Count > 5, Values(5), 0.0F)
        End Get
    End Property
    Public ReadOnly Property Scale As Single
        Get
            Return If(Values.Count > 6, Values(6), 0.0F)
        End Get
    End Property
End Class

Public Class NPC_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public RaceFormID As UInteger
    Public SkinFormID As UInteger
    Public DefaultOutfitFormID As UInteger
    Public SleepOutfitFormID As UInteger
    Public HeadTextureFormID As UInteger
    Public HairColorFormID As UInteger
    Public FacialHairColorFormID As UInteger
    Public HasTextureLighting As Boolean
    Public TextureLightingColor As Color = Color.Empty
    Public HeadPartFormIDs As New List(Of UInteger)
    Public IsFemale As Boolean
    Public WeightThin As Single = 0
    Public WeightMuscular As Single = 0
    Public WeightFat As Single = 0
    Public TemplateFormID As UInteger
    Public TemplateFlags As UShort
    Public TemplateActorFormIDs As New Dictionary(Of NPC_TemplateCategory, UInteger)
    Public MorphValues As New Dictionary(Of UInteger, Single)
    Public FaceTintLayers As New List(Of NPC_FaceTintLayerData)
    Public FaceMorphs As New List(Of NPC_FaceMorphData)
    Public BodyMorphRegionValues As New List(Of Single)
    Public FacialMorphIntensity As Single = 1.0F
    Public PluginName As String = ""

    Public Overrides Function ToString() As String
        If FullName <> "" Then Return $"{FullName} [{EditorID}]"
        Return EditorID
    End Function
End Class

''' <summary>RACE face morph definition (FMRI index -> FMRN name).</summary>
Public Class RACE_FaceMorphDef
    Public Index As UInteger
    Public Name As String = ""
End Class

''' <summary>RACE morph value definition (MSID -> MSM0 min name / MSM1 max name).
''' These map MSDK keys to TriHead morph names for chargen sliders.</summary>
Public Class RACE_MorphValueDef
    Public Index As UInteger   ' MSID = same as MSDK key in NPC record
    Public MinName As String = ""  ' MSM0 = morph name when value is negative (e.g. "BrowDown")
    Public MaxName As String = ""  ' MSM1 = morph name when value is positive (e.g. "BrowUp")
End Class

''' <summary>RACE morph group preset (MPPI -> MPPM morph name).
''' Maps MSDK preset keys to chargen TRI morph names.</summary>
Public Class RACE_MorphPresetDef
    Public Index As UInteger      ' MPPI = same as MSDK key in NPC record
    Public PresetName As String = ""  ' MPPN = display name (localized)
    Public MorphName As String = ""   ' MPPM = morph name in Chargen.tri
End Class

Public Enum TintSlot As UShort
    ForeheadMask = 0
    EyesMask = 1
    NoseMask = 2
    EarsMask = 3
    CheeksMask = 4
    MouthMask = 5
    NeckMask = 6
    LipColor = 7
    CheekColor = 8
    Eyeliner = 9
    EyeSocketUpper = 10
    EyeSocketLower = 11
    SkinTone = 12
    Paint = 13
    LaughLines = 14
    CheekColorLower = 15
    Nose = 16
    Chin = 17
    Neck = 18
    Forehead = 19
    Dirt = 20
    Scars = 21
    FaceDetail = 22
    Brows = 23
    Wrinkles = 24
    Beards = 25
End Enum

Public Class RACE_TintTemplateColor
    Public ColorFormID As UInteger
    Public Alpha As Single
    Public TemplateIndex As UShort
    Public BlendOperation As UInteger
End Class

Public Class RACE_TintTemplateOption
    Public Slot As UShort
    Public Index As UShort
    Public Name As String = ""
    Public Flags As UShort
    Public Textures As New List(Of String)
    Public BlendOperation As UInteger
    Public TemplateColors As New List(Of RACE_TintTemplateColor)
    Public DefaultValue As Single
End Class

Public Class RACE_TintTemplateGroup
    Public GroupName As String = ""
    Public Options As New List(Of RACE_TintTemplateOption)
    Public CategoryIndex As UInteger
End Class

Public Class RACE_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public SkinFormID As UInteger
    Public MaleSkeletonPath As String = ""
    Public FemaleSkeletonPath As String = ""
    Public MaleBodyMeshes As New List(Of String)
    Public FemaleBodyMeshes As New List(Of String)
    Public MaleHeadPartFormIDs As New List(Of UInteger)
    Public FemaleHeadPartFormIDs As New List(Of UInteger)
    Public MaleFaceDetailTextureFormIDs As New List(Of UInteger)
    Public FemaleFaceDetailTextureFormIDs As New List(Of UInteger)
    Public MaleDefaultFaceTextureFormID As UInteger
    Public FemaleDefaultFaceTextureFormID As UInteger
    Public HairColorLookupTexture As String = ""
    Public HairColorExtendedLookupTexture As String = ""
    Public MaleFaceMorphs As New List(Of RACE_FaceMorphDef)
    Public FemaleFaceMorphs As New List(Of RACE_FaceMorphDef)
    ''' <summary>Morph Values (MSID/MSM0/MSM1) - maps MSDK slider keys to TriHead morph names.</summary>
    Public MorphValues As New List(Of RACE_MorphValueDef)
    ''' <summary>Morph Presets (MPPI/MPPM) from Morph Groups - maps MSDK preset keys to TriHead morph names.</summary>
    Public MaleMorphPresets As New List(Of RACE_MorphPresetDef)
    Public FemaleMorphPresets As New List(Of RACE_MorphPresetDef)
    ''' <summary>Morph Group Slider indices (MPGS) - additional MSDK keys per group.</summary>
    Public MaleMorphGroupSliders As New List(Of UInteger)
    Public FemaleMorphGroupSliders As New List(Of UInteger)
    Public MaleTintTemplateGroups As New List(Of RACE_TintTemplateGroup)
    Public FemaleTintTemplateGroups As New List(Of RACE_TintTemplateGroup)

    ''' <summary>Find a tint template option by its TETI index for the given gender.</summary>
    Public Function FindTintOption(index As UShort, isFemale As Boolean) As RACE_TintTemplateOption
        Dim groups = If(isFemale, FemaleTintTemplateGroups, MaleTintTemplateGroups)
        For Each grp In groups
            For Each opt In grp.Options
                If opt.Index = index Then Return opt
            Next
        Next
        Return Nothing
    End Function
End Class

Public Class ARMO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public RaceFormID As UInteger
    Public SlotMask As UInteger
    Public TemplateArmorFormID As UInteger
    Public ArmorAddonFormIDs As New List(Of UInteger)
End Class

Public Class ARMA_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public RaceFormID As UInteger
    Public SlotMask As UInteger
    Public MalePriority As Integer
    Public FemalePriority As Integer
    Public MaleMeshPath As String = ""
    Public FemaleMeshPath As String = ""
    Public MaleFPMeshPath As String = ""
    Public FemaleFPMeshPath As String = ""
    Public MaleSkinTextureFormID As UInteger
    Public FemaleSkinTextureFormID As UInteger
    Public MaleSkinTextureSwapListFormID As UInteger
    Public FemaleSkinTextureSwapListFormID As UInteger
    Public MaleMaterialSwapFormID As UInteger
    Public FemaleMaterialSwapFormID As UInteger
    Public MaleColorRemapIndex As Nullable(Of Single)
    Public FemaleColorRemapIndex As Nullable(Of Single)
    Public AdditionalRaces As New List(Of UInteger)
End Class

Public Class OTFT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ItemFormIDs As New List(Of UInteger)
End Class

Public Class HDPT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public MeshPath As String = ""
    Public Flags As Byte
    Public PartType As Integer = -1
    Public ColorFormID As UInteger
    Public TextureSetFormID As UInteger
    Public UsesBodyTexture As Boolean
    Public ExtraPartFormIDs As New List(Of UInteger)
End Class

Public Class CLFM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Flags As UInteger
    Public HasColor As Boolean
    Public Color As Color = Color.Empty
    Public HasRemappingIndex As Boolean
    Public RemappingIndex As Single
End Class

Public Class TXST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public DiffuseTexture As String = ""
    Public NormalTexture As String = ""
    Public WrinklesTexture As String = ""
    Public GlowTexture As String = ""
    Public HeightTexture As String = ""
    Public EnvironmentTexture As String = ""
    Public MultilayerTexture As String = ""
    Public SmoothSpecTexture As String = ""
    Public MaterialPath As String = ""
    Public Flags As UShort

    Public ReadOnly Property IsFacegenTextures As Boolean
        Get
            Return (Flags And 2US) <> 0US
        End Get
    End Property
End Class

Public Class LVLN_Entry
    Public Level As UShort
    Public FormID As UInteger
    Public Count As UShort = 1US
    Public ChanceNone As Byte
End Class

Public Class LVLN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ChanceNone As Byte
    Public Flags As Byte
    Public Entries As New List(Of LVLN_Entry)
End Class

Public Class LVLI_Entry
    Public Level As UShort
    Public FormID As UInteger
    Public Count As UShort = 1US
    Public ChanceNone As Byte
End Class

Public Class LVLI_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ChanceNone As Byte
    Public Flags As Byte
    Public Entries As New List(Of LVLI_Entry)

    Public ReadOnly Property UseAll As Boolean
        Get
            Return (Flags And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property CalculateEachItemInCount As Boolean
        Get
            Return (Flags And &H2) <> 0
        End Get
    End Property
End Class

Public Class FLST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ItemFormIDs As New List(Of UInteger)
End Class

Public Class MSWP_Substitution
    Public OriginalMaterial As String = ""
    Public ReplacementMaterial As String = ""
    Public TreeFolder As String = ""
    Public HasColorRemapIndex As Boolean
    Public ColorRemapIndex As Single
End Class

Public Class MSWP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public TreeFolder As String = ""
    Public Substitutions As New List(Of MSWP_Substitution)
End Class

Public Module RecordParsers

    Private Function ResolveDisplayString(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager, Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If pluginManager Is Nothing Then Return sr.AsString
        Return pluginManager.ResolveFieldString(rec, sr, kind)
    End Function

    Private Function ResolveFormIDReference(rec As PluginRecord, rawFormID As UInteger, pluginManager As PluginManager) As UInteger
        If pluginManager Is Nothing OrElse rec Is Nothing Then Return rawFormID
        Return pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rawFormID)
    End Function

    Private Function ResolveFormIDReference(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As UInteger
        If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Return 0UI
        Return ResolveFormIDReference(rec, sr.AsUInt32, pluginManager)
    End Function

    Private Function ParseNormalizedFloatColor(data As Byte()) As Color
        If data Is Nothing OrElse data.Length < 12 Then Return Color.Empty

        Dim red = BitConverter.ToSingle(data, 0)
        Dim green = BitConverter.ToSingle(data, 4)
        Dim blue = BitConverter.ToSingle(data, 8)
        Dim alpha = If(data.Length >= 16, BitConverter.ToSingle(data, 12), 1.0F)

        Return Color.FromArgb(
            NormalizeColorChannel(alpha),
            NormalizeColorChannel(red),
            NormalizeColorChannel(green),
            NormalizeColorChannel(blue))
    End Function

    Private Function NormalizeColorChannel(value As Single) As Integer
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return 255
        Dim normalized = value
        If normalized <= 1.0F Then normalized *= 255.0F
        Return Math.Max(0, Math.Min(255, CInt(Math.Round(normalized))))
    End Function

    Private Function ParseClfmColor(rawColor As UInteger) As Color
        Dim r = CInt(rawColor And &HFFUI)
        Dim g = CInt((rawColor >> 8) And &HFFUI)
        Dim b = CInt((rawColor >> 16) And &HFFUI)
        Dim a = CInt((rawColor >> 24) And &HFFUI)
        Return Color.FromArgb(a, r, g, b)
    End Function

    Private Function ParseByteColor(data As Byte(), offset As Integer) As Color
        If data Is Nothing OrElse offset < 0 OrElse offset + 4 > data.Length Then Return Color.Empty
        Return Color.FromArgb(data(offset + 3), data(offset), data(offset + 1), data(offset + 2))
    End Function

    Private Function ParseLeveledEntry(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As LVLN_Entry
        If sr.Data Is Nothing OrElse sr.Data.Length < 8 Then Return Nothing

        Dim entry As New LVLN_Entry With {
            .Level = BitConverter.ToUInt16(sr.Data, 0),
            .FormID = ResolveFormIDReference(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
        }

        If sr.Data.Length >= 10 Then entry.Count = BitConverter.ToUInt16(sr.Data, 8)
        If sr.Data.Length >= 11 Then entry.ChanceNone = sr.Data(10)

        Return entry
    End Function

    Public Function ParseNPC(rec As PluginRecord, pluginName As String, Optional pluginManager As PluginManager = Nothing) As NPC_Data
        Dim npc As New NPC_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID,
            .PluginName = pluginName
        }

        Dim morphKeys As New List(Of UInteger)
        Dim pendingTintLayer As NPC_FaceTintLayerData = Nothing
        Dim pendingFaceMorph As NPC_FaceMorphData = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    npc.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "RNAM"
                    npc.RaceFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "WNAM"
                    npc.SkinFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "DOFT"
                    npc.DefaultOutfitFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "SOFT"
                    npc.SleepOutfitFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "FTSF", "FTST"
                    npc.HeadTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "HCLF"
                    npc.HairColorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "BCLF"
                    npc.FacialHairColorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "QNAM"
                    npc.TextureLightingColor = ParseNormalizedFloatColor(sr.Data)
                    npc.HasTextureLighting = (sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12)
                Case "TPLT"
                    npc.TemplateFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "TPTA"
                    If sr.Data IsNot Nothing Then
                        Dim entryCount = Math.Min(sr.Data.Length \ 4, 13)
                        For i = 0 To entryCount - 1
                            Dim rawFormID = BitConverter.ToUInt32(sr.Data, i * 4)
                            If rawFormID = 0UI Then Continue For
                            Dim category = CType(i, NPC_TemplateCategory)
                            npc.TemplateActorFormIDs(category) = ResolveFormIDReference(rec, rawFormID, pluginManager)
                        Next
                    End If
                Case "PNAM"
                    Dim headPartFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    If headPartFormID <> 0UI Then npc.HeadPartFormIDs.Add(headPartFormID)
                Case "ACBS"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        Dim flags = BitConverter.ToUInt32(sr.Data, 0)
                        npc.IsFemale = (flags And 1UI) <> 0UI
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        npc.TemplateFlags = BitConverter.ToUInt16(sr.Data, 14)
                    End If
                Case "MWGT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        npc.WeightThin = BitConverter.ToSingle(sr.Data, 0)
                        npc.WeightMuscular = BitConverter.ToSingle(sr.Data, 4)
                        npc.WeightFat = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "MSDK"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            morphKeys.Add(BitConverter.ToUInt32(sr.Data, i))
                        Next
                    End If
                Case "MSDV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        Dim valueCount = Math.Min(morphKeys.Count, sr.Data.Length \ 4)
                        For i = 0 To valueCount - 1
                            npc.MorphValues(morphKeys(i)) = BitConverter.ToSingle(sr.Data, i * 4)
                        Next
                    End If
                Case "TETI"
                    If pendingTintLayer IsNot Nothing Then npc.FaceTintLayers.Add(pendingTintLayer)
                    pendingTintLayer = New NPC_FaceTintLayerData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingTintLayer.DataType = BitConverter.ToUInt16(sr.Data, 0)
                        pendingTintLayer.Index = BitConverter.ToUInt16(sr.Data, 2)
                    End If
                Case "TEND"
                    If pendingTintLayer Is Nothing Then pendingTintLayer = New NPC_FaceTintLayerData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then pendingTintLayer.Value = sr.Data(0)
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 5 Then pendingTintLayer.Color = ParseByteColor(sr.Data, 1)
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 7 Then pendingTintLayer.TemplateColorIndex = BitConverter.ToInt16(sr.Data, 5)
                    npc.FaceTintLayers.Add(pendingTintLayer)
                    pendingTintLayer = Nothing
                Case "MRSV"
                    npc.BodyMorphRegionValues.Clear()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            npc.BodyMorphRegionValues.Add(BitConverter.ToSingle(sr.Data, i))
                        Next
                    End If
                Case "FMIN"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        npc.FacialMorphIntensity = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "FMRI"
                    If pendingFaceMorph IsNot Nothing Then npc.FaceMorphs.Add(pendingFaceMorph)
                    pendingFaceMorph = New NPC_FaceMorphData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then pendingFaceMorph.Index = BitConverter.ToUInt32(sr.Data, 0)
                Case "FMRS"
                    If pendingFaceMorph Is Nothing Then pendingFaceMorph = New NPC_FaceMorphData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            pendingFaceMorph.Values.Add(BitConverter.ToSingle(sr.Data, i))
                        Next
                    End If
                    npc.FaceMorphs.Add(pendingFaceMorph)
                    pendingFaceMorph = Nothing
            End Select
        Next

        If pendingTintLayer IsNot Nothing Then npc.FaceTintLayers.Add(pendingTintLayer)
        If pendingFaceMorph IsNot Nothing Then npc.FaceMorphs.Add(pendingFaceMorph)

        Return npc
    End Function

    Public Function ParseRACE(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As RACE_Data
        Dim race As New RACE_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim inMaleBody As Boolean = False
        Dim inFemaleBody As Boolean = False
        Dim expectingHeadGender As Boolean = False
        Dim inMaleHead As Boolean = False
        Dim inFemaleHead As Boolean = False
        Dim pendingFaceMorphDef As RACE_FaceMorphDef = Nothing
        Dim pendingMorphValueDef As RACE_MorphValueDef = Nothing
        Dim pendingMorphPresetDef As RACE_MorphPresetDef = Nothing
        Dim pendingTintGroup As RACE_TintTemplateGroup = Nothing
        Dim pendingTintOption As RACE_TintTemplateOption = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    race.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "WNAM"
                    race.SkinFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "ANAM"
                    Dim path = sr.AsString
                    If path <> "" Then
                        If race.MaleSkeletonPath = "" Then
                            race.MaleSkeletonPath = path
                        ElseIf race.FemaleSkeletonPath = "" Then
                            race.FemaleSkeletonPath = path
                        End If
                    End If
                Case "NAM0"
                    expectingHeadGender = True
                    inMaleHead = False
                    inFemaleHead = False
                Case "NAM1"
                    expectingHeadGender = False
                    inMaleHead = False
                    inFemaleHead = False
                    inMaleBody = False
                    inFemaleBody = False
                Case "MNAM"
                    If expectingHeadGender Then
                        inMaleHead = True
                        inFemaleHead = False
                        inMaleBody = False
                        inFemaleBody = False
                        expectingHeadGender = False
                    Else
                        inMaleBody = True
                        inFemaleBody = False
                    End If
                Case "FNAM"
                    If expectingHeadGender Then
                        inFemaleHead = True
                        inMaleHead = False
                        inMaleBody = False
                        inFemaleBody = False
                        expectingHeadGender = False
                    Else
                        inFemaleBody = True
                        inMaleBody = False
                    End If
                Case "MODL"
                    Dim meshPath = sr.AsString
                    If meshPath <> "" AndAlso meshPath.EndsWith(".nif", StringComparison.OrdinalIgnoreCase) Then
                        If inFemaleBody Then
                            race.FemaleBodyMeshes.Add(meshPath)
                        ElseIf inMaleBody Then
                            race.MaleBodyMeshes.Add(meshPath)
                        End If
                    End If
                Case "HEAD"
                    Dim headPartFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    If headPartFormID = 0UI Then Continue For
                    If inFemaleHead Then
                        race.FemaleHeadPartFormIDs.Add(headPartFormID)
                    ElseIf inMaleHead Then
                        race.MaleHeadPartFormIDs.Add(headPartFormID)
                    End If
                Case "FTSM"
                    Dim textureSetId = ResolveFormIDReference(rec, sr, pluginManager)
                    If textureSetId <> 0UI Then race.MaleFaceDetailTextureFormIDs.Add(textureSetId)
                Case "FTSF"
                    Dim textureSetId = ResolveFormIDReference(rec, sr, pluginManager)
                    If textureSetId <> 0UI Then race.FemaleFaceDetailTextureFormIDs.Add(textureSetId)
                Case "DFTM"
                    race.MaleDefaultFaceTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "DFTF"
                    race.FemaleDefaultFaceTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "HNAM"
                    race.HairColorLookupTexture = sr.AsString
                Case "HLTX"
                    race.HairColorExtendedLookupTexture = sr.AsString
                Case "MPPI"
                    ' Morph Group preset definition
                    If pendingMorphPresetDef IsNot Nothing Then
                        If inFemaleHead Then
                            race.FemaleMorphPresets.Add(pendingMorphPresetDef)
                        ElseIf inMaleHead Then
                            race.MaleMorphPresets.Add(pendingMorphPresetDef)
                        End If
                    End If
                    pendingMorphPresetDef = New RACE_MorphPresetDef()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingMorphPresetDef.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MPPN"
                    If pendingMorphPresetDef IsNot Nothing Then
                        pendingMorphPresetDef.PresetName = ResolveDisplayString(rec, sr, pluginManager)
                    End If
                Case "MPPM"
                    If pendingMorphPresetDef IsNot Nothing Then
                        pendingMorphPresetDef.MorphName = sr.AsString
                    End If
                Case "FMRI"
                    ' Face morph definition start (in RACE context, not NPC)
                    ' Flush pending morph preset
                    If pendingMorphPresetDef IsNot Nothing Then
                        If inFemaleHead Then
                            race.FemaleMorphPresets.Add(pendingMorphPresetDef)
                        ElseIf inMaleHead Then
                            race.MaleMorphPresets.Add(pendingMorphPresetDef)
                        End If
                        pendingMorphPresetDef = Nothing
                    End If
                    If pendingFaceMorphDef IsNot Nothing Then
                        If inFemaleHead Then
                            race.FemaleFaceMorphs.Add(pendingFaceMorphDef)
                        ElseIf inMaleHead Then
                            race.MaleFaceMorphs.Add(pendingFaceMorphDef)
                        End If
                    End If
                    pendingFaceMorphDef = New RACE_FaceMorphDef()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingFaceMorphDef.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "FMRN"
                    If pendingFaceMorphDef IsNot Nothing Then
                        pendingFaceMorphDef.Name = ResolveDisplayString(rec, sr, pluginManager)
                    End If
                Case "MSID"
                    ' Morph Value definition start
                    If pendingMorphValueDef IsNot Nothing Then race.MorphValues.Add(pendingMorphValueDef)
                    pendingMorphValueDef = New RACE_MorphValueDef()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingMorphValueDef.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MSM0"
                    If pendingMorphValueDef IsNot Nothing Then pendingMorphValueDef.MinName = sr.AsString
                Case "MSM1"
                    If pendingMorphValueDef IsNot Nothing Then pendingMorphValueDef.MaxName = sr.AsString
                Case "MPGS"
                    ' Morph Group Sliders - array of uint32 indices
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For idx = 0 To sr.Data.Length - 4 Step 4
                            Dim sliderKey = BitConverter.ToUInt32(sr.Data, idx)
                            If inFemaleHead Then
                                race.FemaleMorphGroupSliders.Add(sliderKey)
                            ElseIf inMaleHead Then
                                race.MaleMorphGroupSliders.Add(sliderKey)
                            End If
                        Next
                    End If

                ' --- Tint Template parsing (in HEAD section) ---
                Case "TTGP"
                    Dim name = ResolveDisplayString(rec, sr, pluginManager)
                    If pendingTintOption IsNot Nothing Then
                        ' TTGP after TETI = option name
                        pendingTintOption.Name = name
                    Else
                        ' TTGP without pending option = new group
                        ' Flush previous group
                        If pendingTintGroup IsNot Nothing Then
                            If inFemaleHead Then
                                race.FemaleTintTemplateGroups.Add(pendingTintGroup)
                            ElseIf inMaleHead Then
                                race.MaleTintTemplateGroups.Add(pendingTintGroup)
                            End If
                        End If
                        pendingTintGroup = New RACE_TintTemplateGroup With {.GroupName = name}
                    End If
                Case "TETI"
                    ' Flush pending option into current group
                    If pendingTintOption IsNot Nothing AndAlso pendingTintGroup IsNot Nothing Then
                        pendingTintGroup.Options.Add(pendingTintOption)
                    End If
                    pendingTintOption = New RACE_TintTemplateOption()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingTintOption.Slot = BitConverter.ToUInt16(sr.Data, 0)
                        pendingTintOption.Index = BitConverter.ToUInt16(sr.Data, 2)
                    End If
                Case "TTEF"
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        pendingTintOption.Flags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "TTET"
                    If pendingTintOption IsNot Nothing Then
                        pendingTintOption.Textures.Add(sr.AsString)
                    End If
                Case "TTEB"
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingTintOption.BlendOperation = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "TTEC"
                    ' Template Colors array - each entry: FormID(4) + Alpha(4) + TemplateIndex(2) + BlendOp(4) = 14 bytes
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 14 Then
                        For idx = 0 To sr.Data.Length - 14 Step 14
                            Dim rawFID = BitConverter.ToUInt32(sr.Data, idx)
                            Dim tc As New RACE_TintTemplateColor With {
                                .ColorFormID = ResolveFormIDReference(rec, rawFID, pluginManager),
                                .Alpha = BitConverter.ToSingle(sr.Data, idx + 4),
                                .TemplateIndex = BitConverter.ToUInt16(sr.Data, idx + 8),
                                .BlendOperation = BitConverter.ToUInt32(sr.Data, idx + 10)
                            }
                            pendingTintOption.TemplateColors.Add(tc)
                        Next
                    End If
                Case "TTED"
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingTintOption.DefaultValue = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "TTGE"
                    ' Category index — end of group
                    If pendingTintOption IsNot Nothing AndAlso pendingTintGroup IsNot Nothing Then
                        pendingTintGroup.Options.Add(pendingTintOption)
                        pendingTintOption = Nothing
                    End If
                    If pendingTintGroup IsNot Nothing Then
                        If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                            pendingTintGroup.CategoryIndex = BitConverter.ToUInt32(sr.Data, 0)
                        End If
                        If inFemaleHead Then
                            race.FemaleTintTemplateGroups.Add(pendingTintGroup)
                        ElseIf inMaleHead Then
                            race.MaleTintTemplateGroups.Add(pendingTintGroup)
                        End If
                        pendingTintGroup = Nothing
                    End If
            End Select
        Next

        ' Flush pending
        If pendingMorphPresetDef IsNot Nothing Then
            If inFemaleHead Then
                race.FemaleMorphPresets.Add(pendingMorphPresetDef)
            ElseIf inMaleHead Then
                race.MaleMorphPresets.Add(pendingMorphPresetDef)
            End If
        End If
        If pendingMorphValueDef IsNot Nothing Then race.MorphValues.Add(pendingMorphValueDef)
        If pendingFaceMorphDef IsNot Nothing Then
            If inFemaleHead Then
                race.FemaleFaceMorphs.Add(pendingFaceMorphDef)
            ElseIf inMaleHead Then
                race.MaleFaceMorphs.Add(pendingFaceMorphDef)
            End If
        End If
        ' Flush pending tint template
        If pendingTintOption IsNot Nothing AndAlso pendingTintGroup IsNot Nothing Then
            pendingTintGroup.Options.Add(pendingTintOption)
        End If
        If pendingTintGroup IsNot Nothing Then
            If inFemaleHead Then
                race.FemaleTintTemplateGroups.Add(pendingTintGroup)
            ElseIf inMaleHead Then
                race.MaleTintTemplateGroups.Add(pendingTintGroup)
            End If
        End If

        Return race
    End Function

    Public Function ParseARMO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ARMO_Data
        Dim armo As New ARMO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    armo.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "BOD2", "BODT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then armo.SlotMask = BitConverter.ToUInt32(sr.Data, 0)
                Case "RNAM"
                    armo.RaceFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "TNAM"
                    armo.TemplateArmorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "MODL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length = 4 Then armo.ArmorAddonFormIDs.Add(ResolveFormIDReference(rec, sr, pluginManager))
            End Select
        Next

        Return armo
    End Function

    Public Function ParseARMA(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ARMA_Data
        Dim arma As New ARMA_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "BOD2", "BODT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then arma.SlotMask = BitConverter.ToUInt32(sr.Data, 0)
                Case "RNAM"
                    arma.RaceFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        arma.MalePriority = sr.Data(0)
                        arma.FemalePriority = sr.Data(1)
                    End If
                Case "MOD2"
                    arma.MaleMeshPath = sr.AsString
                Case "MOD3"
                    arma.FemaleMeshPath = sr.AsString
                Case "MOD4"
                    arma.MaleFPMeshPath = sr.AsString
                Case "MOD5"
                    arma.FemaleFPMeshPath = sr.AsString
                Case "NAM0", "NAM1", "NAM2", "NAM3"
                    If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Continue For
                    Select Case sr.Signature
                        Case "NAM0"
                            arma.MaleSkinTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        Case "NAM1"
                            arma.FemaleSkinTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        Case "NAM2"
                            arma.MaleSkinTextureSwapListFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        Case "NAM3"
                            arma.FemaleSkinTextureSwapListFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    End Select
                Case "MO2S"
                    arma.MaleMaterialSwapFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "MO3S"
                    arma.FemaleMaterialSwapFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "MO2C"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then arma.MaleColorRemapIndex = BitConverter.ToSingle(sr.Data, 0)
                Case "MO3C"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then arma.FemaleColorRemapIndex = BitConverter.ToSingle(sr.Data, 0)
                Case "MODL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length = 4 Then arma.AdditionalRaces.Add(ResolveFormIDReference(rec, sr, pluginManager))
            End Select
        Next

        Return arma
    End Function

    Public Function ParseOTFT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As OTFT_Data
        Dim otft As New OTFT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "INAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                For i = 0 To sr.Data.Length - 4 Step 4
                    Dim rawFormID = BitConverter.ToUInt32(sr.Data, i)
                    If rawFormID = 0UI Then Continue For
                    otft.ItemFormIDs.Add(ResolveFormIDReference(rec, rawFormID, pluginManager))
                Next
            End If
        Next

        Return otft
    End Function

    Public Function ParseHDPT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As HDPT_Data
        Dim hdpt As New HDPT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    hdpt.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "MODL", "MOD2"
                    If hdpt.MeshPath = "" Then hdpt.MeshPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        hdpt.Flags = sr.Data(0)
                        hdpt.UsesBodyTexture = (hdpt.Flags And &H40) <> 0
                    End If
                Case "HNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        Dim extraPartFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        If extraPartFormID <> 0UI Then hdpt.ExtraPartFormIDs.Add(extraPartFormID)
                    End If
                Case "CNAM"
                    hdpt.ColorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "TNAM"
                    hdpt.TextureSetFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then hdpt.PartType = BitConverter.ToInt32(sr.Data, 0)
            End Select
        Next

        Return hdpt
    End Function

    Public Function ParseCLFM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CLFM_Data
        Dim clfm As New CLFM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim rawColor As UInteger = 0UI
        Dim hasRawColor As Boolean = False

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    clfm.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        rawColor = sr.AsUInt32
                        hasRawColor = True
                    End If
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then clfm.Flags = sr.AsUInt32
            End Select
        Next

        If hasRawColor Then
            If (clfm.Flags And 2UI) = 0UI Then
                clfm.Color = ParseClfmColor(rawColor)
                clfm.HasColor = True
            Else
                clfm.RemappingIndex = BitConverter.ToSingle(BitConverter.GetBytes(rawColor), 0)
                clfm.HasRemappingIndex = True
            End If
        End If

        Return clfm
    End Function

    Public Function ParseTXST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TXST_Data
        Dim txst As New TXST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "TX00"
                    txst.DiffuseTexture = sr.AsString
                Case "TX01"
                    txst.NormalTexture = sr.AsString
                Case "TX02"
                    txst.WrinklesTexture = sr.AsString
                Case "TX03"
                    txst.GlowTexture = sr.AsString
                Case "TX04"
                    txst.HeightTexture = sr.AsString
                Case "TX05"
                    txst.EnvironmentTexture = sr.AsString
                Case "TX06"
                    txst.MultilayerTexture = sr.AsString
                Case "TX07"
                    txst.SmoothSpecTexture = sr.AsString
                Case "MNAM"
                    txst.MaterialPath = sr.AsString
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then txst.Flags = BitConverter.ToUInt16(sr.Data, 0)
            End Select
        Next

        Return txst
    End Function

    Public Function ParseLVLN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LVLN_Data
        Dim lvln As New LVLN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "LVLD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvln.ChanceNone = sr.Data(0)
                Case "LVLF"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvln.Flags = sr.Data(0)
                Case "LVLO"
                    Dim entry = ParseLeveledEntry(rec, sr, pluginManager)
                    If entry IsNot Nothing AndAlso entry.FormID <> 0UI Then lvln.Entries.Add(entry)
            End Select
        Next

        Return lvln
    End Function

    Public Function ParseLVLI(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LVLI_Data
        Dim lvli As New LVLI_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "LVLD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvli.ChanceNone = sr.Data(0)
                Case "LVLF"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvli.Flags = sr.Data(0)
                Case "LVLO"
                    Dim entry = ParseLeveledEntry(rec, sr, pluginManager)
                    If entry Is Nothing OrElse entry.FormID = 0UI Then Continue For
                    lvli.Entries.Add(New LVLI_Entry With {
                        .Level = entry.Level,
                        .FormID = entry.FormID,
                        .Count = entry.Count,
                        .ChanceNone = entry.ChanceNone
                    })
            End Select
        Next

        Return lvli
    End Function

    Public Function ParseFLST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FLST_Data
        Dim flst As New FLST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature <> "LNAM" OrElse sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Continue For
            Dim itemId = ResolveFormIDReference(rec, sr, pluginManager)
            If itemId <> 0UI Then flst.ItemFormIDs.Add(itemId)
        Next

        Return flst
    End Function

    Public Function ParseMSWP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MSWP_Data
        Dim mswp As New MSWP_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim current As MSWP_Substitution = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FNAM"
                    If current Is Nothing Then
                        If mswp.TreeFolder = "" Then mswp.TreeFolder = sr.AsString
                    Else
                        current.TreeFolder = sr.AsString
                    End If
                Case "BNAM"
                    If current IsNot Nothing AndAlso (current.OriginalMaterial <> "" OrElse current.ReplacementMaterial <> "") Then
                        mswp.Substitutions.Add(current)
                    End If
                    current = New MSWP_Substitution With {
                        .OriginalMaterial = sr.AsString
                    }
                Case "SNAM"
                    If current Is Nothing Then current = New MSWP_Substitution()
                    current.ReplacementMaterial = sr.AsString
                Case "CNAM"
                    If current Is Nothing Then current = New MSWP_Substitution()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        current.ColorRemapIndex = BitConverter.ToSingle(sr.Data, 0)
                        current.HasColorRemapIndex = True
                    End If
            End Select
        Next

        If current IsNot Nothing AndAlso (current.OriginalMaterial <> "" OrElse current.ReplacementMaterial <> "") Then
            mswp.Substitutions.Add(current)
        End If

        Return mswp
    End Function
End Module

