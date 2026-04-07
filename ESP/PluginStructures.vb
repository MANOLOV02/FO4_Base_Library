Imports System.IO
Imports System.Text

''' <summary>
''' Core binary structures for reading Bethesda ESP/ESM/ESL plugin files (Fallout 4).
''' Based on TES5Edit (xEdit) definitions.
''' </summary>
Public Module PluginConstants
    ' Record flags
    Public Const FLAG_ESM As UInteger = &H1UI
    Public Const FLAG_LOCALIZED As UInteger = &H80UI
    Public Const FLAG_ESL As UInteger = &H200UI
    Public Const FLAG_COMPRESSED As UInteger = &H40000UI

    ' Group header size
    Public Const RECORD_HEADER_SIZE As Integer = 24
    Public Const GROUP_HEADER_SIZE As Integer = 24
    Public Const SUBRECORD_HEADER_SIZE As Integer = 6

    ' Signatures for NPC rendering (original subset - for backward compatibility)
    Public ReadOnly SIGS_NPC_RENDERING As New HashSet(Of String)(
        {"NPC_", "RACE", "ARMO", "ARMA", "OTFT", "HDPT", "TXST", "CLFM", "LVLN", "LVLI", "FLST", "MSWP",
         "CELL", "WRLD"},
        StringComparer.Ordinal)

    ' Default filter kept for backward compatibility
    Public ReadOnly SIGS_OF_INTEREST As HashSet(Of String) = SIGS_NPC_RENDERING

    ''' <summary>All Fallout 4 record type signatures supported by the parser.</summary>
    Public ReadOnly ALL_FO4_SIGNATURES As New HashSet(Of String)(
        {"AACT", "ACTI", "ADDN", "AECH", "ALCH", "AMDL", "AMMO", "ANIO", "AORU", "ARMA", "ARMO", "ARTO",
         "ASPC", "ASTP", "AVIF", "BNDS", "BOOK", "BPTD", "CAMS", "CELL", "CLAS", "CLFM", "CLMT", "CMPO",
         "COBJ", "COLL", "CONT", "CPTH", "CSTY", "DEBR", "DFOB", "DIAL", "DLBR", "DLVW", "DMGT", "DOBJ",
         "DOOR", "DUAL", "ECZN", "EFSH", "ENCH", "EQUP", "EXPL", "EYES", "FACT", "FLOR", "FLST", "FSTP",
         "FSTS", "FURN", "GDRY", "GLOB", "GMST", "GRAS", "HAZD", "HDPT", "IDLE", "IDLM", "IMAD", "IMGS",
         "INFO", "INGR", "INNR", "IPCT", "IPDS", "KEYM", "KSSM", "KYWD", "LAND", "LAYR", "LCTN", "LENS",
         "LGTM", "LIGH", "LSCR", "LTEX", "LVLI", "LVLN", "LVSP", "MATT", "MESG", "MGEF", "MISC", "MOVT",
         "MSWP", "MSTT", "MUSC", "MUST", "NAVI", "NAVM", "NOTE", "NPC_", "OMOD", "OTFT", "PACK", "PERK",
         "PKIN", "PROJ", "QUST", "RACE", "REGN", "RELA", "REVB", "RFCT", "RFGP", "SCCO", "SCEN", "SCOL",
         "SCSN", "SMBN", "SMEN", "SMQN", "SNCT", "SNDR", "SOPM", "SOUN", "SPEL", "SPGD", "STAG", "STAT",
         "TACT", "TERM", "TREE", "TRNS", "TXST", "VTYP", "WATR", "WEAP", "WRLD", "WTHR", "ZOOM"},
        StringComparer.Ordinal)
End Module

Public Structure RecordHeader
    Public Signature As String    ' 4 chars
    Public DataSize As UInteger   ' Size of data after this header
    Public Flags As UInteger
    Public FormID As UInteger
    Public VCS1 As UInteger
    Public Version As UShort
    Public VCS2 As UShort

    Public ReadOnly Property IsCompressed As Boolean
        Get
            Return (Flags And FLAG_COMPRESSED) <> 0
        End Get
    End Property

    Public Shared Function Read(br As BinaryReader) As RecordHeader
        Dim h As New RecordHeader
        h.Signature = Encoding.ASCII.GetString(br.ReadBytes(4))
        h.DataSize = br.ReadUInt32()
        h.Flags = br.ReadUInt32()
        h.FormID = br.ReadUInt32()
        h.VCS1 = br.ReadUInt32()
        h.Version = br.ReadUInt16()
        h.VCS2 = br.ReadUInt16()
        Return h
    End Function
End Structure

Public Structure GroupHeader
    Public Signature As String    ' Always "GRUP"
    Public GroupSize As UInteger  ' Total size INCLUDING this 24-byte header
    Public Label As UInteger      ' For type 0: record signature as uint
    Public GroupType As Integer
    Public Stamp As UInteger
    Public Unknown As UInteger

    ''' <summary>Label as a 4-char signature string (for type 0 groups)</summary>
    Public ReadOnly Property LabelAsSignature As String
        Get
            Dim bytes = BitConverter.GetBytes(Label)
            Return Encoding.ASCII.GetString(bytes, 0, 4)
        End Get
    End Property

    Public Shared Function Read(br As BinaryReader) As GroupHeader
        Dim h As New GroupHeader
        h.Signature = Encoding.ASCII.GetString(br.ReadBytes(4))
        h.GroupSize = br.ReadUInt32()
        h.Label = br.ReadUInt32()
        h.GroupType = br.ReadInt32()
        h.Stamp = br.ReadUInt32()
        h.Unknown = br.ReadUInt32()
        Return h
    End Function
End Structure

Public Structure SubrecordData
    Public Signature As String    ' 4 chars
    Public Data As Byte()

    Public ReadOnly Property AsString As String
        Get
            If Data Is Nothing OrElse Data.Length = 0 Then Return ""
            ' Strip null terminator
            Dim len = Data.Length
            If len > 0 AndAlso Data(len - 1) = 0 Then len -= 1
            Return PluginTextDecoding.DecodePluginString(Data, 0, len)
        End Get
    End Property

    Public ReadOnly Property AsUInt32 As UInteger
        Get
            If Data Is Nothing OrElse Data.Length < 4 Then Return 0
            Return BitConverter.ToUInt32(Data, 0)
        End Get
    End Property

    Public ReadOnly Property AsUInt16 As UShort
        Get
            If Data Is Nothing OrElse Data.Length < 2 Then Return 0
            Return BitConverter.ToUInt16(Data, 0)
        End Get
    End Property

    Public ReadOnly Property AsFloat As Single
        Get
            If Data Is Nothing OrElse Data.Length < 4 Then Return 0
            Return BitConverter.ToSingle(Data, 0)
        End Get
    End Property
End Structure

''' <summary>Parsed record with header and list of subrecords.</summary>
Public Class PluginRecord
    Public Header As RecordHeader
    Public SourcePluginName As String = ""
    Public SourcePluginIsLocalized As Boolean
    Public Subrecords As New List(Of SubrecordData)

    ''' <summary>Get first subrecord with given signature, or Nothing.</summary>
    Public Function GetSubrecord(sig As String) As SubrecordData?
        For Each sr In Subrecords
            If sr.Signature = sig Then Return sr
        Next
        Return Nothing
    End Function

    ''' <summary>Get all subrecords with given signature.</summary>
    Public Function GetSubrecords(sig As String) As List(Of SubrecordData)
        Return Subrecords.Where(Function(sr) sr.Signature = sig).ToList()
    End Function

    ''' <summary>Editor ID (EDID subrecord).</summary>
    Public ReadOnly Property EditorID As String
        Get
            Dim sr = GetSubrecord("EDID")
            Return If(sr?.AsString, "")
        End Get
    End Property
End Class

''' <summary>Represents a resolved global FormID with plugin name.</summary>
Public Structure ResolvedFormID
    Public FormID As UInteger
    Public PluginName As String

    Public Sub New(id As UInteger, plugin As String)
        FormID = id
        PluginName = plugin
    End Sub

    Public Overrides Function ToString() As String
        Return $"[{PluginName}:{FormID:X8}]"
    End Function
End Structure

