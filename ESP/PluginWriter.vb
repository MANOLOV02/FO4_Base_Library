Imports System.IO
Imports System.Text

''' <summary>
''' Writes minimal Bethesda plugin files (.esp/.esm) for runtime-generated mods.
''' Used by Wardrobe_Manager Pack to emit dummy "light master" plugins that anchor
''' BA2/BSA archive auto-discovery (engine loads "Foo - Main.ba2" + "Foo - Textures.ba2"
''' iff a plugin "Foo.esp" exists in Data).
'''
''' Also used by NPC_Manager Save ESP to emit auto-generated plugins containing NPC_
''' overrides with proper master cleanup.
''' </summary>
Public Module PluginWriter

    ' TES4 record header version field.
    ' These are spec constants of the binary format, not game data.
    Friend Const TES4_RECORD_VERSION_FO4 As UShort = &H83US   ' 131 (FO4)
    ' 44 = Skyrim SPECIAL EDITION, NOT 43 (that is Skyrim LE). Skyrim SE, Skyrim VR, and Enderal SE
    ' all stamp 44 on every newly-created SSE record. Writing 0x2B (43) here mislabels every
    ' app-authored SSE record and the TES4 header as Skyrim LE.
    Friend Const TES4_RECORD_VERSION_SSE As UShort = &H2CUS   ' 44

    ' HEDR subrecord version (float).
    Friend Const HEDR_VERSION_FO4 As Single = 0.95F
    Friend Const HEDR_VERSION_SSE As Single = 1.71F

    ' Convention: object IDs below 0x800 are reserved for the engine's own use.
    Friend Const NEXT_OBJECT_ID_DEFAULT As UInteger = &H800UI

    ''' <summary>¿El archivo que estamos escribiendo puede usar el rango HARDCODED, o sea object ids por
    ''' DEBAJO de 0x800? Depende del JUEGO y de la VERSIÓN DEL HEDR que emitimos, y exige
    ''' al menos un master.
    ''' <para>SSE pide <c>HEDR &gt;= 1.709</c> y nosotros escribimos <see cref="HEDR_VERSION_SSE"/> = 1.71
    ''' ⇒ True en cuanto el archivo tiene un master. FO4 pide <c>&gt;= 1.0</c> y escribimos
    ''' <see cref="HEDR_VERSION_FO4"/> = 0.95 ⇒ siempre False. O sea que es GAME-AWARE de verdad, no una
    ''' constante: el espacio direccionable de un ESL nuestro es 0x001..0xFFF en SSE y 0x800..0xFFF en FO4.</para>
    ''' <para>Decide el PISO del espacio (1 vs 0x800), que es lo que el canónico usa para el wrap, la
    ''' recuperación y el agotamiento de object ids.
    ''' NO cambia de dónde arranca un guardado normal: eso sale del contador del HEDR, que esta app siembra
    ''' en 0x800 igual que el CK, así que los FormID de un guardado corriente no se mueven.</para></summary>
    ''' <para>⛔ NO reimplementar la regla acá: esto reenvía a <c>PluginManager.AllowsHardcodedRange</c>, la
    ''' única implementación, pasándole la versión de HEDR que ESTE escritor emite. Una copia local se come
    ''' las dos precondiciones de VR del canónico (FO4 VR no está en la lista; Skyrim VR exige VRESL), y en
    ''' un rig de VR el escritor y el lector terminan con pisos distintos para el mismo archivo.</para>
    Friend Function AllowsHardcodedRange(game As Config_App.Game_Enum, masterCount As Integer) As Boolean
        Dim version As Single = If(game = Config_App.Game_Enum.Skyrim, HEDR_VERSION_SSE, HEDR_VERSION_FO4)
        Return PluginManager.AllowsHardcodedRange(version, masterCount, Config_App.Current.DataPath)
    End Function

    ' ========================================================================
    ' NPC_Manager Save ESP — author CNAM canonical string.
    ' Used to (a) tag plugins emitted by NPC_Manager and (b) detect existing
    ' auto-generated plugins on Data\ scan ("Update existing" workflow).
    ' Single source of truth — do not duplicate the literal anywhere else.
    ' ========================================================================
    Public Const NPC_MANAGER_AUTHOR_CNAM As String = "NPC Manager - Auto generated"

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
        ' is informational; engines tolerate 0, but a real value matches what CK produces.
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
                ' TES4.CNAM is a translatable string field. Route through the central
                ' encoder so future callers passing non-ASCII authors (e.g. 中文 author tools)
                ' don't get silent '?' replacement.
                Dim authorBytes = PluginEncodingSettings.EncodeTranslatable(If(author, ""))
                WriteSubrecordHeader(bw, "CNAM", authorBytes.Length + 1)
                bw.Write(authorBytes)
                bw.Write(CByte(0))                         ' NUL terminator

                ' --- MAST (master plugin name, ZSTRING) ---
                ' Misma ley que SaveNpcEspWriter: General (cpNormal, lo que lee PluginReader) y rehusar si el
                ' nombre no sobrevive. Hoy acá siempre es el master del juego (ASCII), pero la ley va en UN
                ' lugar — el encoder central, nunca un Encoding.ASCII local que sustituye por '?' en silencio.
                Dim masterBytes = PluginEncodingSettings.EncodeMasterFileName(masterName)
                WriteSubrecordHeader(bw, "MAST", masterBytes.Length + 1)
                bw.Write(masterBytes)
                bw.Write(CByte(0))

                ' --- DATA (master file size, u64) ---
                ' MAST is followed by DATA carrying the master's
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

    Friend Sub WriteSubrecordHeader(bw As BinaryWriter, signature As String, dataSize As Integer)
        If signature.Length <> 4 Then Throw New InvalidDataException($"Subrecord signature must be 4 chars: '{signature}'.")
        If dataSize < 0 OrElse dataSize > UShort.MaxValue Then
            Throw New InvalidDataException($"Subrecord '{signature}' data size {dataSize} exceeds u16 (XXXX extension not implemented).")
        End If
        bw.Write(Encoding.ASCII.GetBytes(signature))
        bw.Write(CUShort(dataSize))
    End Sub

End Module
