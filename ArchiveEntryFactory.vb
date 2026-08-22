Imports System.IO
Imports BSA_BA2_Library_DLL.BethesdaArchive.Core

''' <summary>CÓMO SE ARMA UNA ENTRADA DE ARCHIVE, ESCRITO UNA VEZ. Única copia: la consumen
''' <c>Wardrobe_Manager.WM_PackUnpack</c> y <c>FO4_NPC_Manager.NpcFaceGenPacker</c>.
''' <para>NO volver a copiarlas en cada app: el día que una toque el preset de compresión, el orden de los
''' campos o el manejo del SSE, la otra sigue como estaba y los archives dejan de ser comparables sin que
''' nada avise.</para>
''' <para>Vive acá y no en <c>Ba2_Bsa_Library</c> porque esa librería NO conoce <c>Config_App</c> (sólo
''' referencia el wrapper de DirectXTex): el mapeo juego↔<c>GameKind</c> necesita las dos mitades, y ésta
''' es la única que las tiene.</para></summary>
''' <para>Va DENTRO de un Namespace a proposito. Un `Public Module` en el namespace RAIZ mete sus
''' miembros —`MapGame`, `MapGameBack`— en el scope global SIN CALIFICAR de los 64 proyectos que
''' referencian esta libreria. `MapGame` es un nombre generico: el dia que un Tool declare el suyo,
''' sale un BC30562 por ambiguedad. Calificarlo lo evita de entrada.</para>
Namespace Archives

Public Module ArchiveEntryFactory

    ''' <summary>Lee la textura de disco y devuelve la <see cref="VirtualEntry"/> lista para el writer,
    ''' YA PRE-COMPRIMIDA según el juego activo.
    ''' <para>SSE/BSA guarda el DDS entero (header incluido) como bytes opacos; FO4/BA2 DX10 parsea el
    ''' header, separa el payload y comprime sólo eso. El CRC32 es sobre lo que cada formato considera el
    ''' contenido lógico: el archivo completo en BSA, el payload sin header en DX10.</para>
    ''' <para>TRAMPA: <c>version:=8UI</c> está cableado y NO acompaña a la versión de BA2 que elige el
    ''' usuario. Hoy es inocuo porque <c>CompressForBa2Dx10</c> sólo mira ese parámetro en la rama v3+LZ4;
    ''' cambiarlo mueve bytes, así que va como cambio aparte y medido.</para></summary>
    ''' <param name="sourcePath">De dónde se LEEN los bytes.</param>
    ''' <param name="entryPath">Qué ruta lleva la entrada DENTRO del archive. Es un parámetro aparte
    ''' porque no siempre coinciden: el packer de FaceGen hornea a un temporal y lo archiva bajo la ruta
    ''' canónica del juego. Wardrobe Manager pasa el mismo valor en los dos.</param>
    ''' <param name="game">Juego explícito en vez de <c>Config_App.Current.Game</c>: el packer corre en
    ''' hilos del pool durante un bake y el global puede cambiar bajo sus pies.</param>
    Public Function MakeTextureEntry(dataDir As String, sourcePath As String, entryPath As String,
                                     game As Config_App.Game_Enum) As VirtualEntry
        Dim relUnderData = Path.GetRelativePath(dataDir, entryPath).Correct_Path_Separator
        Dim bytes = File.ReadAllBytes(sourcePath)

        If game = Config_App.Game_Enum.Skyrim Then
            Dim dir As String = "", file As String = ""
            PathUtil.SplitDirFile(relUnderData, dir, file)
            Dim cp = PayloadCompressor.CompressForBsa(bytes, wantCompressed:=True)
            Return New VirtualEntry With {
                .Directory = dir,
                .FileName = file,
                .Crc32 = Ba2WriterCommon.Crc32Bytes(bytes),
                .PreCompressed = True,
                .PreCompressedBytes = cp.Bytes,
                .PreCompressedCompSize = cp.CompSize,
                .PreCompressedDecompSize = cp.DecompSize
            }
        End If

        ' FO4 BA2 DX10: parse header → metadata + stripped payload → compress payload.
        Dim ve = Dx10Importer.FromDdsBytes(bytes, relUnderData)
        Dim payload = If(ve.Data, Array.Empty(Of Byte)())
        ve.Crc32 = Ba2WriterCommon.Crc32Bytes(payload)
        Dim cpDx10 = PayloadCompressor.CompressForBa2Dx10(payload,
            version:=8UI,
            compressionFormat:=Ba2WriterCommon.CompressionFormat.Zip,
            preset:=Ba2WriterCommon.ZlibPreset.Default)
        ve.Data = Nothing                           ' se suelta el payload crudo: río abajo sólo se usa el comprimido
        ve.PreCompressed = True
        ve.PreCompressedBytes = cpDx10.Bytes
        ve.PreCompressedCompSize = cpDx10.CompSize
        ve.PreCompressedDecompSize = cpDx10.DecompSize
        Return ve
    End Function

    ''' <summary>Juego activo → tipo de archive. Tira en vez de elegir un default: un juego que no mapea
    ''' es un estado que el llamador tiene que resolver, no algo que esta función pueda inventar.</summary>
    Public Function MapGame(g As Config_App.Game_Enum) As GameKind
        Select Case g
            Case Config_App.Game_Enum.Fallout4 : Return GameKind.FO4_BA2
            Case Config_App.Game_Enum.Skyrim : Return GameKind.SSE_BSA
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(g))
        End Select
    End Function

    ''' <summary>Tipo de archive → juego. Inversa exacta de <see cref="MapGame"/>.</summary>
    Public Function MapGameBack(g As GameKind) As Config_App.Game_Enum
        Select Case g
            Case GameKind.FO4_BA2 : Return Config_App.Game_Enum.Fallout4
            Case GameKind.SSE_BSA : Return Config_App.Game_Enum.Skyrim
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(g))
        End Select
    End Function

    ''' <summary>Lo mismo que <see cref="MakeTextureEntry"/> pero para materiales (.bgsm/.bgem) y cualquier
    ''' otro archivo que NO sea textura: no hay metadata DX10, va entero por el camino GNRL/BSA.</summary>
    Public Function MakeMaterialEntry(dataDir As String, sourcePath As String, entryPath As String,
                                      game As Config_App.Game_Enum) As VirtualEntry
        Dim relUnderData = Path.GetRelativePath(dataDir, entryPath).Correct_Path_Separator
        Dim bytes = File.ReadAllBytes(sourcePath)
        Dim relDir As String = "", relFile As String = ""
        PathUtil.SplitDirFile(relUnderData, relDir, relFile)

        Dim ve As New VirtualEntry With {
            .Directory = relDir,
            .FileName = relFile,
            .Crc32 = Ba2WriterCommon.Crc32Bytes(bytes)
        }

        If game = Config_App.Game_Enum.Skyrim Then
            Dim cp = PayloadCompressor.CompressForBsa(bytes, wantCompressed:=True)
            ve.PreCompressed = True
            ve.PreCompressedBytes = cp.Bytes
            ve.PreCompressedCompSize = cp.CompSize
            ve.PreCompressedDecompSize = cp.DecompSize
        Else
            Dim cp = PayloadCompressor.CompressForBa2Gnrl(bytes,
                version:=8UI,
                compressionFormat:=Ba2WriterCommon.CompressionFormat.Zip,
                preset:=Ba2WriterCommon.ZlibPreset.Default)
            ve.PreCompressed = True
            ve.PreCompressedBytes = cp.Bytes
            ve.PreCompressedCompSize = cp.CompSize
            ve.PreCompressedDecompSize = cp.DecompSize
        End If

        Return ve
    End Function

End Module

End Namespace
