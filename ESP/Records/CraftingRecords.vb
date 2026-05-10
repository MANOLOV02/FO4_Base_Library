Imports System.Text

' ============================================================================
' Crafting / Modification Record Data Classes and Parsers
' OMOD, INNR, COBJ, CMPO, MATT
' Based on TES5Edit wbDefinitionsFO4.pas
' ============================================================================

#Region "Data Classes"

''' <summary>OMOD property value type enum.</summary>
Public Enum OMOD_ValueType As Byte
    IntType = 0
    FloatType = 1
    BoolType = 2
    StringType = 3
    FormIDInt = 4
    EnumType = 5
    FormIDFloat = 6
End Enum

''' <summary>OMOD property entry.</summary>
Public Class OMOD_Property
    Public ValueType As OMOD_ValueType
    Public FunctionType As Byte
    Public PropertyIndex As UShort
    Public Value1 As Single      ' float/int/formid depending on type
    Public Value1FormID As UInteger ' resolved FormID when applicable
    Public Value2 As Single
    Public StepValue As Single
End Class

''' <summary>OMOD include entry (wbDefinitionsFO4.pas:12893-12898).</summary>
Public Class OMOD_Include
    Public ModFormID As UInteger
    Public MinimumLevel As Byte
    Public IsOptional As Boolean
    Public DontUseAll As Boolean
End Class

''' <summary>OMOD legacy "Items" array entry (wbDefinitionsFO4.pas:12888-12891). Two opaque
''' 4-byte values per entry. xEdit comment: "no way to change these in CK, legacy data
''' leftover?" — preserved verbatim so the parser doesn't mis-attribute these bytes to the
''' Attach Parent Slots array (the previous heuristic "remaining bytes / 4" did exactly that).</summary>
Public Class OMOD_LegacyItem
    Public Value1 As UInteger
    Public Value2 As UInteger
End Class

''' <summary>Fallout 4 OMOD record - Object Modification (wbDefinitionsFO4.pas:12863-12906).</summary>
Public Class OMOD_Data
    Public FormID As UInteger
    Public RecordFlags As UInteger     ' record header flags: bit 3 = Legendary Mod (0x08), bit 6 = Mod Collection (0x40)
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Description As String = ""
    Public ModelPath As String = ""
    Public LooseModFormID As UInteger
    Public Priority As Byte
    ''' <summary>FLTR (Filter) string subrecord (wbDefinitionsFO4.pas:12905 + :5642 wbString FLTR).</summary>
    Public Filter As String = ""

    ' DATA struct header
    Public IncludeCount As UInteger
    Public PropertyCount As UInteger
    ''' <summary>DATA byte 8: Unknown Bool 1 (xEdit unnamed). Captured raw for round-trip.</summary>
    Public UnknownBool1 As Byte
    ''' <summary>DATA byte 9: Unknown Bool 2 (xEdit unnamed). Captured raw for round-trip.</summary>
    Public UnknownBool2 As Byte
    Public FormType As UInteger   ' Sig2Int: ARMO, NPC_, WEAP, NONE
    Public MaxRank As Byte
    Public LevelTierScaledOffset As Byte
    Public AttachPointFormID As UInteger
    Public AttachParentSlotFormIDs As New List(Of UInteger)
    ''' <summary>"Items" array (wbDefinitionsFO4.pas:12888-12891). Legacy CK leftover, two
    ''' opaque u32 per entry. Empty for almost all vanilla OMODs. Captured to keep the parser
    ''' honest about which bytes belong where.</summary>
    Public LegacyItems As New List(Of OMOD_LegacyItem)

    ' Target OMOD Keywords (MNAM)
    Public TargetKeywordFormIDs As New List(Of UInteger)

    ' Filter Keywords (FNAM)
    Public FilterKeywordFormIDs As New List(Of UInteger)

    ' Parsed includes and properties
    Public Includes As New List(Of OMOD_Include)
    Public Properties As New List(Of OMOD_Property)

    Public ReadOnly Property FormTypeSignature As String
        Get
            If FormType = 0 Then Return "NONE"
            Dim bytes = BitConverter.GetBytes(FormType)
            Return Encoding.ASCII.GetString(bytes, 0, 4).TrimEnd(ChrW(0))
        End Get
    End Property

    ''' <summary>AddonIndex op extracted from a Property (idx 7 ARMO). FunctionType per
    ''' wbDefinitionsFO4.pas:5839/5842: SET=0, ADD=2 (and MUL+ADD=1 for Float — not used by
    ''' Int-typed AddonIndex). Vanilla dump v2 confirms 59 SET + 10 ADD across loaded plugins.</summary>
    Public Class AddonIndexOp
        Public Value As Integer
        Public IsSet As Boolean   ' True = SET, False = ADD
    End Class

    ''' <summary>Return every AddonIndex op (idx 7) declared by this OMOD, in declaration order.
    ''' Caller folds them into the effective index by walking SET (overwrite) / ADD (add to
    ''' running value). Schema: wbDefinitionsFO4.pas:5710 (wbArmorPropertyEnum) + :5842
    ''' (FormID-bucket FunctionType enum SET/REM/ADD; AddonIndex is Int but the engine reuses
    ''' the same SET/ADD semantics).</summary>
    Public Function GetAddonIndexOps() As List(Of AddonIndexOp)
        Const AddonIndexProperty As UShort = 7US
        Dim ops As New List(Of AddonIndexOp)
        For Each prop In Properties
            If prop.PropertyIndex <> AddonIndexProperty Then Continue For
            ' Value 1 stored as Single in OMOD_Property.Value1; reinterpret 4 bytes as Int32.
            Dim asInt = BitConverter.ToInt32(BitConverter.GetBytes(prop.Value1), 0)
            ops.Add(New AddonIndexOp With {
                .Value = asInt,
                .IsSet = (prop.FunctionType = 0)
            })
        Next
        Return ops
    End Function

    ''' <summary>Legacy: returns the LAST AddonIndex value as if every op were SET. Kept for
    ''' callers that pre-date the SET/ADD split (10 vanilla ADD cases were silently treated as
    ''' SET, masking the running-sum semantics). New callers should use <see cref="GetAddonIndexOps"/>
    ''' and fold ops correctly.</summary>
    Public Function GetAddonIndexOverride() As Integer
        Const AddonIndexProperty As UShort = 7US
        For Each prop In Properties
            If prop.PropertyIndex <> AddonIndexProperty Then Continue For
            Dim asBytes = BitConverter.GetBytes(prop.Value1)
            Dim asInt = BitConverter.ToInt32(asBytes, 0)
            Return asInt
        Next
        Return -1
    End Function
End Class

''' <summary>INNR naming rule entry.</summary>
Public Class INNR_Rule
    Public Text As String = ""
    Public KeywordFormIDs As New List(Of UInteger)
    Public PropertyValue As Single
    Public PropertyTarget As Byte
    Public PropertyOperator As Byte
    Public Index As UShort
End Class

''' <summary>INNR naming ruleset.</summary>
Public Class INNR_Ruleset
    Public Count As UInteger
    Public Rules As New List(Of INNR_Rule)
End Class

''' <summary>Fallout 4 INNR record - Instance Naming Rules.</summary>
Public Class INNR_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Target As UInteger   ' 0=None, 0x1D=Armor, 0x2D=Actor, 0x2A=Furniture, 0x2B=Weapon
    Public Rulesets As New List(Of INNR_Ruleset)

    Public ReadOnly Property TargetName As String
        Get
            Select Case Target
                Case &H1D : Return "Armor"
                Case &H2D : Return "Actor"
                Case &H2A : Return "Furniture"
                Case &H2B : Return "Weapon"
                Case Else : Return "None"
            End Select
        End Get
    End Property
End Class

''' <summary>COBJ component entry.</summary>
Public Class COBJ_Component
    Public ComponentFormID As UInteger
    Public Count As UInteger
End Class

''' <summary>Fallout 4 COBJ record - Constructible Object (crafting recipe).</summary>
Public Class COBJ_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public Description As String = ""
    Public CreatedObjectFormID As UInteger
    Public WorkbenchKeywordFormID As UInteger
    Public MenuArtObjectFormID As UInteger
    Public PickUpSoundFormID As UInteger
    Public PutDownSoundFormID As UInteger
    Public CategoryKeywordFormIDs As New List(Of UInteger)
    Public Components As New List(Of COBJ_Component)

    ' INTV
    Public CreatedObjectCount As UShort
    Public CraftPriority As UShort
End Class

''' <summary>Fallout 4 CMPO record - Component.</summary>
Public Class CMPO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public CraftingSoundFormID As UInteger
    Public AutoCalcValue As UInteger
    Public ScrapItemFormID As UInteger
    Public ModScrapScalarFormID As UInteger
End Class

''' <summary>Fallout 4 MATT record - Material Type.</summary>
Public Class MATT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public MaterialName As String = ""
    Public ParentFormID As UInteger
    Public HavokDisplayColor As Single()   ' RGBA floats
    Public Buoyancy As Single = 1.0F
    Public MaterialFlags As UInteger
    Public HavokImpactDataSetFormID As UInteger
    Public BreakableFX As String = ""

    Public ReadOnly Property IsStairMaterial As Boolean
        Get
            Return (MaterialFlags And &H1UI) <> 0
        End Get
    End Property

    Public ReadOnly Property ArrowsStick As Boolean
        Get
            Return (MaterialFlags And &H2UI) <> 0
        End Get
    End Property
End Class

#End Region

#Region "Parsers"

Public Module CraftingRecordParsers

    Public Function ParseOMOD(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As OMOD_Data
        Dim o As New OMOD_Data With {
            .FormID = rec.Header.FormID,
            .RecordFlags = rec.Header.Flags,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    o.FullName = ResolveStr(rec, sr, pluginManager)
                Case "DESC"
                    o.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "MODL"
                    If o.ModelPath = "" Then o.ModelPath = sr.AsString
                Case "LNAM"
                    o.LooseModFormID = ResolveFID(rec, sr, pluginManager)
                Case "NAM1"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then o.Priority = sr.Data(0)
                Case "MNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            o.TargetKeywordFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            o.FilterKeywordFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
                Case "FLTR"
                    o.Filter = sr.AsString
                Case "DATA"
                    ParseOMOD_DATA(sr, rec, pluginManager, o)
            End Select
        Next

        Return o
    End Function

    Private Sub ParseOMOD_DATA(sr As SubrecordData, rec As PluginRecord, pm As PluginManager, o As OMOD_Data)
        Dim d = sr.Data
        If d Is Nothing OrElse d.Length < 20 Then Return

        o.IncludeCount = BitConverter.ToUInt32(d, 0)
        o.PropertyCount = BitConverter.ToUInt32(d, 4)
        o.UnknownBool1 = d(8)
        o.UnknownBool2 = d(9)
        o.FormType = BitConverter.ToUInt32(d, 10)
        o.MaxRank = d(14)
        o.LevelTierScaledOffset = d(15)
        o.AttachPointFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, 16), pm)

        Dim offset = 20

        ' Layout after offset 20 per wbDefinitionsFO4.pas:12886-12899:
        '   AttachParentSlots[]   (wbArray -1 = u32 prefix count, each entry 4 bytes FormID KYWD)
        '   Items[]               (wbArray -1 = u32 prefix count, each entry 8 bytes — legacy CK leftover)
        '   Includes[]            (count from Include Count u32 at +0; 7 bytes each)
        '   Properties[]          (count from Property Count u32 at +4; 24 bytes each)
        '
        ' Both AttachParentSlots and Items use u32 prefix per xEdit wbInterface.pas:13933-13948
        ' (arCount=-1 → 4-byte length prefix). Reading them with explicit prefixes (not from
        ' "remaining bytes / 4" heuristic) is the only way to keep AttachParentSlots clean
        ' when an OMOD ships non-empty Items — the previous heuristic conflated both arrays.

        Const includeEntrySize As Integer = 7
        Const propertyEntrySize As Integer = 24
        Const itemEntrySize As Integer = 8

        ' AttachParentSlots: u32 count prefix + N × 4 bytes
        If offset + 4 <= d.Length Then
            Dim slotCount As UInteger = BitConverter.ToUInt32(d, offset)
            offset += 4
            For i = 0 To CInt(slotCount) - 1
                If offset + 4 > d.Length Then Exit For
                o.AttachParentSlotFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(d, offset), pm))
                offset += 4
            Next
        End If

        ' Items: u32 count prefix + N × (4+4) bytes. xEdit comment "no way to change these in CK,
        ' legacy data leftover" — vanilla almost always 0, but capture verbatim if present so the
        ' parser doesn't drift Includes/Properties offsets when an outlier ships data here.
        If offset + 4 <= d.Length Then
            Dim itemCount As UInteger = BitConverter.ToUInt32(d, offset)
            offset += 4
            For i = 0 To CInt(itemCount) - 1
                If offset + itemEntrySize > d.Length Then Exit For
                Dim item As New OMOD_LegacyItem With {
                    .Value1 = BitConverter.ToUInt32(d, offset),
                    .Value2 = BitConverter.ToUInt32(d, offset + 4)
                }
                o.LegacyItems.Add(item)
                offset += itemEntrySize
            Next
        End If

        ' Parse includes
        For i = 0 To CInt(o.IncludeCount) - 1
            If offset + includeEntrySize > d.Length Then Exit For
            Dim inc As New OMOD_Include With {
                .ModFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, offset), pm),
                .MinimumLevel = d(offset + 4),
                .IsOptional = d(offset + 5) <> 0,
                .DontUseAll = d(offset + 6) <> 0
            }
            o.Includes.Add(inc)
            offset += includeEntrySize
        Next

        ' Parse properties
        For i = 0 To CInt(o.PropertyCount) - 1
            If offset + propertyEntrySize > d.Length Then Exit For
            Dim prop = ParseObjectModProperty(d, offset, rec, pm)
            o.Properties.Add(prop)
            offset += propertyEntrySize
        Next
    End Sub

    ''' <summary>Parse a single 24-byte OMOD Property entry from a binary buffer at the given
    ''' offset. Caller validates `offset + 24 &lt;= d.Length` before invoking. Layout per
    ''' wbObjectModProperties (wbDefinitionsFO4.pas:5826-5865):
    '''   +0  u8   ValueType  (0=Int,1=Float,2=Bool,3=String,4=FormID,Int,5=Enum,6=FormID,Float)
    '''   +1..+3  unused
    '''   +4  u8   FunctionType (per-ValueType enum: SET/MUL+ADD/ADD/REM/AND/OR — see :5838-5843)
    '''   +5..+7  unused
    '''   +8  u16  PropertyIndex (interpreted via per-FormType enum: ARMO/NPC_/WEAP)
    '''   +10..+11 unused
    '''   +12 u32/float Value1 (FormID-typed values are resolved via ResolveFIDRaw)
    '''   +16 u32/float Value2
    '''   +20 float Step
    ''' Used by ParseOMOD_DATA (OMOD record's Properties array) and by ParseOBTSPayload
    ''' (Properties array embedded in an OBTS combination payload — same 24-byte layout).</summary>
    Friend Function ParseObjectModProperty(d As Byte(), offset As Integer, rec As PluginRecord, pm As PluginManager) As OMOD_Property
        Dim prop As New OMOD_Property With {
            .ValueType = CType(d(offset), OMOD_ValueType),
            .FunctionType = d(offset + 4),
            .PropertyIndex = BitConverter.ToUInt16(d, offset + 8)
        }

        Select Case prop.ValueType
            Case OMOD_ValueType.FormIDInt, OMOD_ValueType.FormIDFloat
                prop.Value1FormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(d, offset + 12), pm)
                prop.Value1 = BitConverter.ToSingle(BitConverter.GetBytes(prop.Value1FormID), 0)
            Case OMOD_ValueType.FloatType
                prop.Value1 = BitConverter.ToSingle(d, offset + 12)
            Case Else
                prop.Value1 = BitConverter.ToSingle(d, offset + 12)
        End Select

        prop.Value2 = BitConverter.ToSingle(d, offset + 16)
        prop.StepValue = BitConverter.ToSingle(d, offset + 20)
        Return prop
    End Function

    Public Function ParseINNR(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As INNR_Data
        Dim n As New INNR_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim currentRuleset As INNR_Ruleset = Nothing
        Dim currentRule As INNR_Rule = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "UNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        n.Target = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "VNAM"
                    ' Start of a new ruleset
                    If currentRule IsNot Nothing AndAlso currentRuleset IsNot Nothing Then
                        currentRuleset.Rules.Add(currentRule)
                        currentRule = Nothing
                    End If
                    If currentRuleset IsNot Nothing Then n.Rulesets.Add(currentRuleset)
                    currentRuleset = New INNR_Ruleset()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        currentRuleset.Count = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "WNAM"
                    ' Start of a new name rule within current ruleset
                    If currentRule IsNot Nothing AndAlso currentRuleset IsNot Nothing Then
                        currentRuleset.Rules.Add(currentRule)
                    End If
                    currentRule = New INNR_Rule With {
                        .Text = ResolveStr(rec, sr, pluginManager)
                    }
                Case "KWDA"
                    If currentRule IsNot Nothing Then
                        ParseFormIDArray(sr, rec, pluginManager, currentRule.KeywordFormIDs)
                    End If
                Case "XNAM"
                    If currentRule IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 6 Then
                        currentRule.PropertyValue = BitConverter.ToSingle(sr.Data, 0)
                        currentRule.PropertyTarget = sr.Data(4)
                        currentRule.PropertyOperator = sr.Data(5)
                    End If
                Case "YNAM"
                    If currentRule IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        currentRule.Index = BitConverter.ToUInt16(sr.Data, 0)
                    End If
            End Select
        Next

        If currentRule IsNot Nothing AndAlso currentRuleset IsNot Nothing Then currentRuleset.Rules.Add(currentRule)
        If currentRuleset IsNot Nothing Then n.Rulesets.Add(currentRuleset)

        Return n
    End Function

    Public Function ParseCOBJ(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As COBJ_Data
        Dim c As New COBJ_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "DESC"
                    c.Description = ResolveStr(rec, sr, pluginManager, LocalizedStringTableKind.DLStrings)
                Case "CNAM"
                    c.CreatedObjectFormID = ResolveFID(rec, sr, pluginManager)
                Case "BNAM"
                    c.WorkbenchKeywordFormID = ResolveFID(rec, sr, pluginManager)
                Case "ANAM"
                    c.MenuArtObjectFormID = ResolveFID(rec, sr, pluginManager)
                Case "YNAM"
                    c.PickUpSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "ZNAM"
                    c.PutDownSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            c.CategoryKeywordFormIDs.Add(ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager))
                        Next
                    End If
                Case "FVPA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        For i = 0 To sr.Data.Length - 8 Step 8
                            c.Components.Add(New COBJ_Component With {
                                .ComponentFormID = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager),
                                .Count = BitConverter.ToUInt32(sr.Data, i + 4)
                            })
                        Next
                    End If
                Case "INTV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        c.CreatedObjectCount = BitConverter.ToUInt16(sr.Data, 0)
                        c.CraftPriority = BitConverter.ToUInt16(sr.Data, 2)
                    End If
            End Select
        Next

        Return c
    End Function

    Public Function ParseCMPO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CMPO_Data
        Dim c As New CMPO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    c.FullName = ResolveStr(rec, sr, pluginManager)
                Case "CUSD"
                    c.CraftingSoundFormID = ResolveFID(rec, sr, pluginManager)
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        c.AutoCalcValue = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MNAM"
                    c.ScrapItemFormID = ResolveFID(rec, sr, pluginManager)
                Case "GNAM"
                    c.ModScrapScalarFormID = ResolveFID(rec, sr, pluginManager)
            End Select
        Next

        Return c
    End Function

    Public Function ParseMATT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MATT_Data
        Dim m As New MATT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "MNAM"
                    m.MaterialName = sr.AsString
                Case "PNAM"
                    m.ParentFormID = ResolveFID(rec, sr, pluginManager)
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        m.HavokDisplayColor = {
                            BitConverter.ToSingle(sr.Data, 0),
                            BitConverter.ToSingle(sr.Data, 4),
                            BitConverter.ToSingle(sr.Data, 8),
                            BitConverter.ToSingle(sr.Data, 12)
                        }
                    End If
                Case "BNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.Buoyancy = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        m.MaterialFlags = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "HNAM"
                    m.HavokImpactDataSetFormID = ResolveFID(rec, sr, pluginManager)
                Case "ANAM"
                    m.BreakableFX = sr.AsString
            End Select
        Next

        Return m
    End Function

End Module

#End Region
