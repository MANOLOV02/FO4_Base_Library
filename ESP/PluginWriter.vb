Imports System.IO
Imports System.Text

''' <summary>
''' Writes minimal Bethesda plugin files (.esp/.esm) for runtime-generated mods.
''' Used by Wardrobe_Manager Pack to emit dummy "light master" plugins that anchor
''' BA2/BSA archive auto-discovery (engine loads "Foo - Main.ba2" + "Foo - Textures.ba2"
''' iff a plugin "Foo.esp" exists in Data).
''' </summary>
Public Module PluginWriter

    ' TES4 record header version field (per xEdit / wbInterface.pas).
    ' These are spec constants of the binary format, not game data.
    Private Const TES4_RECORD_VERSION_FO4 As UShort = &H83US   ' 131
    Private Const TES4_RECORD_VERSION_SSE As UShort = &H2BUS   ' 43

    ' HEDR subrecord version (float).
    Private Const HEDR_VERSION_FO4 As Single = 0.95F
    Private Const HEDR_VERSION_SSE As Single = 1.71F

    ' Convention: object IDs below 0x800 are reserved for the engine's own use.
    Private Const NEXT_OBJECT_ID_DEFAULT As UInteger = &H800UI

    ''' <summary>
    ''' Writes a "light master" dummy plugin: TES4 record only (no records of any other type),
    ''' flagged ESM+ESL so it occupies no load-order slot but is still recognized as a master
    ''' the engine ties archives to. The plugin lists exactly one master (the base game .esm).
    ''' </summary>
    ''' <param name="outputPath">Output path (typically "...\Data\WM_ClonePack.esp").</param>
    ''' <param name="game">Determines TES4 / HEDR version fields and the master to reference.</param>
    ''' <param name="author">CNAM author string. Default "Wardrobe Manager".</param>
    Public Sub WriteLightMasterDummy(outputPath As String,
                                     game As Config_App.Game_Enum,
                                     Optional author As String = "Wardrobe Manager")
        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("outputPath is empty.", NameOf(outputPath))

        Dim masterName As String = MasterFileName(game)
        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Dim hedrVersion As Single = If(game = Config_App.Game_Enum.Fallout4, HEDR_VERSION_FO4, HEDR_VERSION_SSE)

        ' Master file size: read from disk if the .esm sits next to our output. The DATA subrecord
        ' is informational; engines tolerate 0, but a real value matches what xEdit/CK produce.
        Dim masterFileSize As ULong = TryReadMasterFileSize(outputPath, masterName)

        ' === Build subrecord data block (TES4 record body) ===
        Using bodyMs As New MemoryStream()
            Using bw As New BinaryWriter(bodyMs)
                ' --- HEDR (12 bytes data) ---
                WriteSubrecordHeader(bw, "HEDR", 12)
                bw.Write(hedrVersion)                      ' float version
                bw.Write(0UI)                              ' numRecords (no records besides TES4)
                bw.Write(NEXT_OBJECT_ID_DEFAULT)           ' nextObjectID

                ' --- CNAM (author, ZSTRING) ---
                Dim authorBytes = Encoding.ASCII.GetBytes(If(author, ""))
                WriteSubrecordHeader(bw, "CNAM", authorBytes.Length + 1)
                bw.Write(authorBytes)
                bw.Write(CByte(0))                         ' NUL terminator

                ' --- MAST (master plugin name, ZSTRING) ---
                Dim masterBytes = Encoding.ASCII.GetBytes(masterName)
                WriteSubrecordHeader(bw, "MAST", masterBytes.Length + 1)
                bw.Write(masterBytes)
                bw.Write(CByte(0))

                ' --- DATA (master file size, u64) ---
                ' Per TES5Edit (wbInterface.pas), MAST is followed by DATA carrying the master's
                ' on-disk size. Pairing: MAST_n always comes with DATA_n.
                WriteSubrecordHeader(bw, "DATA", 8)
                bw.Write(masterFileSize)
            End Using

            Dim bodyBytes = bodyMs.ToArray()

            ' === Build TES4 record header (24 bytes) + body ===
            Dim outDir = Path.GetDirectoryName(outputPath)
            If Not String.IsNullOrEmpty(outDir) AndAlso Not Directory.Exists(outDir) Then
                Directory.CreateDirectory(outDir)
            End If

            Using fs As FileStream = File.Create(outputPath)
                Using bw As New BinaryWriter(fs)
                    bw.Write(Encoding.ASCII.GetBytes("TES4"))                       ' 4 — Signature
                    bw.Write(CUInt(bodyBytes.Length))                               ' 4 — DataSize
                    bw.Write(FLAG_ESM Or FLAG_ESL)                                  ' 4 — Flags (light master)
                    bw.Write(0UI)                                                   ' 4 — FormID (always 0 for TES4)
                    bw.Write(0UI)                                                   ' 4 — VCS1
                    bw.Write(recordVersion)                                         ' 2 — Version
                    bw.Write(0US)                                                   ' 2 — VCS2
                    bw.Write(bodyBytes)
                End Using
            End Using
        End Using
    End Sub

    Private Function MasterFileName(game As Config_App.Game_Enum) As String
        Select Case game
            Case Config_App.Game_Enum.Fallout4 : Return "Fallout4.esm"
            Case Config_App.Game_Enum.Skyrim : Return "Skyrim.esm"
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(game), $"Unsupported game: {game}")
        End Select
    End Function

    Private Function TryReadMasterFileSize(outputPath As String, masterName As String) As ULong
        Try
            Dim outDir = Path.GetDirectoryName(outputPath)
            If String.IsNullOrEmpty(outDir) Then Return 0UL

            Dim masterPath = Path.Combine(outDir, masterName)
            If Not File.Exists(masterPath) Then Return 0UL

            Return CULng(New FileInfo(masterPath).Length)
        Catch
            Return 0UL
        End Try
    End Function

    Private Sub WriteSubrecordHeader(bw As BinaryWriter, signature As String, dataSize As Integer)
        If signature.Length <> 4 Then Throw New InvalidDataException($"Subrecord signature must be 4 chars: '{signature}'.")
        If dataSize < 0 OrElse dataSize > UShort.MaxValue Then
            Throw New InvalidDataException($"Subrecord '{signature}' data size {dataSize} exceeds u16 (XXXX extension not implemented).")
        End If
        bw.Write(Encoding.ASCII.GetBytes(signature))
        bw.Write(CUShort(dataSize))
    End Sub

End Module
