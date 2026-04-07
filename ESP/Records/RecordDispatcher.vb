Imports System.Text

' ============================================================================
' Unified Record Dispatcher
' Routes any PluginRecord to its appropriate parser based on signature.
' Returns the parsed data object, or Nothing for unsupported types.
' ============================================================================

Public Module RecordDispatcher

    ''' <summary>
    ''' Parse any Fallout 4 record into its strongly-typed data object.
    ''' Returns Nothing if the signature is not supported or parsing fails.
    ''' </summary>
    Public Function ParseRecord(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As Object
        If rec Is Nothing Then Return Nothing

        Try
            Select Case rec.Header.Signature

                ' === Original parsers (RecordParsers.vb) ===
                Case "NPC_" : Return RecordParsers.ParseNPC(rec, rec.SourcePluginName, pluginManager)
                Case "RACE" : Return RecordParsers.ParseRACE(rec, pluginManager)
                Case "ARMO" : Return RecordParsers.ParseARMO(rec, pluginManager)
                Case "ARMA" : Return RecordParsers.ParseARMA(rec, pluginManager)
                Case "OTFT" : Return RecordParsers.ParseOTFT(rec, pluginManager)
                Case "HDPT" : Return RecordParsers.ParseHDPT(rec, pluginManager)
                Case "TXST" : Return RecordParsers.ParseTXST(rec, pluginManager)
                Case "CLFM" : Return RecordParsers.ParseCLFM(rec, pluginManager)
                Case "LVLN" : Return RecordParsers.ParseLVLN(rec, pluginManager)
                Case "LVLI" : Return RecordParsers.ParseLVLI(rec, pluginManager)
                Case "FLST" : Return RecordParsers.ParseFLST(rec, pluginManager)
                Case "MSWP" : Return RecordParsers.ParseMSWP(rec, pluginManager)

                ' === Item / Inventory (ItemRecords.vb) ===
                Case "WEAP" : Return ItemRecordParsers.ParseWEAP(rec, pluginManager)
                Case "AMMO" : Return ItemRecordParsers.ParseAMMO(rec, pluginManager)
                Case "ALCH" : Return ItemRecordParsers.ParseALCH(rec, pluginManager)
                Case "MISC" : Return ItemRecordParsers.ParseMISC(rec, pluginManager)
                Case "BOOK" : Return ItemRecordParsers.ParseBOOK(rec, pluginManager)
                Case "KEYM" : Return ItemRecordParsers.ParseKEYM(rec, pluginManager)
                Case "LIGH" : Return ItemRecordParsers.ParseLIGH(rec, pluginManager)
                Case "INGR" : Return ItemRecordParsers.ParseINGR(rec, pluginManager)
                Case "CONT" : Return ItemRecordParsers.ParseCONT(rec, pluginManager)
                Case "FLOR" : Return ItemRecordParsers.ParseFLOR(rec, pluginManager)
                Case "NOTE" : Return ItemRecordParsers.ParseNOTE(rec, pluginManager)

                ' === Magic / Keywords / Settings (MagicRecords.vb) ===
                Case "ENCH" : Return MagicRecordParsers.ParseENCH(rec, pluginManager)
                Case "SPEL" : Return MagicRecordParsers.ParseSPEL(rec, pluginManager)
                Case "MGEF" : Return MagicRecordParsers.ParseMGEF(rec, pluginManager)
                Case "PERK" : Return MagicRecordParsers.ParsePERK(rec, pluginManager)
                Case "LVSP" : Return MagicRecordParsers.ParseLVSP(rec, pluginManager)
                Case "KYWD" : Return MagicRecordParsers.ParseKYWD(rec, pluginManager)
                Case "EQUP" : Return MagicRecordParsers.ParseEQUP(rec, pluginManager)
                Case "GLOB" : Return MagicRecordParsers.ParseGLOB(rec, pluginManager)
                Case "GMST" : Return MagicRecordParsers.ParseGMST(rec, pluginManager)
                Case "AVIF" : Return MagicRecordParsers.ParseAVIF(rec, pluginManager)
                Case "DMGT" : Return MagicRecordParsers.ParseDMGT(rec, pluginManager)

                ' === Crafting / Modification (CraftingRecords.vb) ===
                Case "OMOD" : Return CraftingRecordParsers.ParseOMOD(rec, pluginManager)
                Case "INNR" : Return CraftingRecordParsers.ParseINNR(rec, pluginManager)
                Case "COBJ" : Return CraftingRecordParsers.ParseCOBJ(rec, pluginManager)
                Case "CMPO" : Return CraftingRecordParsers.ParseCMPO(rec, pluginManager)
                Case "MATT" : Return CraftingRecordParsers.ParseMATT(rec, pluginManager)

                ' === World / Environment (WorldRecords.vb) ===
                Case "CELL" : Return WorldRecordParsers.ParseCELL(rec, pluginManager)
                Case "WRLD" : Return WorldRecordParsers.ParseWRLD(rec, pluginManager)
                Case "LCTN" : Return WorldRecordParsers.ParseLCTN(rec, pluginManager)
                Case "NAVM" : Return WorldRecordParsers.ParseNAVM(rec, pluginManager)
                Case "ECZN" : Return WorldRecordParsers.ParseECZN(rec, pluginManager)
                Case "REGN" : Return WorldRecordParsers.ParseREGN(rec, pluginManager)
                Case "WATR" : Return WorldRecordParsers.ParseWATR(rec, pluginManager)
                Case "WTHR" : Return WorldRecordParsers.ParseWTHR(rec, pluginManager)
                Case "CLMT" : Return WorldRecordParsers.ParseCLMT(rec, pluginManager)
                Case "LGTM" : Return WorldRecordParsers.ParseLGTM(rec, pluginManager)
                Case "LTEX" : Return WorldRecordParsers.ParseLTEX(rec, pluginManager)

                ' === Actor / Character (ActorRecords.vb) ===
                Case "FACT" : Return ActorRecordParsers.ParseFACT(rec, pluginManager)
                Case "CLAS" : Return ActorRecordParsers.ParseCLAS(rec, pluginManager)
                Case "EYES" : Return ActorRecordParsers.ParseEYES(rec, pluginManager)
                Case "BPTD" : Return ActorRecordParsers.ParseBPTD(rec, pluginManager)
                Case "MOVT" : Return ActorRecordParsers.ParseMOVT(rec, pluginManager)
                Case "CSTY" : Return ActorRecordParsers.ParseCSTY(rec, pluginManager)
                Case "VTYP" : Return ActorRecordParsers.ParseVTYP(rec, pluginManager)
                Case "RELA" : Return ActorRecordParsers.ParseRELA(rec, pluginManager)

                ' === Quest / Dialogue / AI (QuestRecords.vb) ===
                Case "QUST" : Return QuestRecordParsers.ParseQUST(rec, pluginManager)
                Case "DIAL" : Return QuestRecordParsers.ParseDIAL(rec, pluginManager)
                Case "INFO" : Return QuestRecordParsers.ParseINFO(rec, pluginManager)
                Case "PACK" : Return QuestRecordParsers.ParsePACK(rec, pluginManager)
                Case "SCEN" : Return QuestRecordParsers.ParseSCEN(rec, pluginManager)
                Case "IDLE" : Return QuestRecordParsers.ParseIDLE(rec, pluginManager)
                Case "DLBR" : Return QuestRecordParsers.ParseDLBR(rec, pluginManager)
                Case "DLVW" : Return QuestRecordParsers.ParseDLVW(rec, pluginManager)
                Case "SMBN" : Return QuestRecordParsers.ParseSMBN(rec, pluginManager)
                Case "SMEN" : Return QuestRecordParsers.ParseSMEN(rec, pluginManager)
                Case "SMQN" : Return QuestRecordParsers.ParseSMQN(rec, pluginManager)

                ' === Visual Effects / Projectiles (VisualRecords.vb) ===
                Case "IMGS" : Return VisualRecordParsers.ParseIMGS(rec, pluginManager)
                Case "IMAD" : Return VisualRecordParsers.ParseIMAD(rec, pluginManager)
                Case "EFSH" : Return VisualRecordParsers.ParseEFSH(rec, pluginManager)
                Case "PROJ" : Return VisualRecordParsers.ParsePROJ(rec, pluginManager)
                Case "EXPL" : Return VisualRecordParsers.ParseEXPL(rec, pluginManager)
                Case "HAZD" : Return VisualRecordParsers.ParseHAZD(rec, pluginManager)
                Case "CAMS" : Return VisualRecordParsers.ParseCAMS(rec, pluginManager)
                Case "CPTH" : Return VisualRecordParsers.ParseCPTH(rec, pluginManager)
                Case "RFCT" : Return VisualRecordParsers.ParseRFCT(rec, pluginManager)
                Case "SPGD" : Return VisualRecordParsers.ParseSPGD(rec, pluginManager)
                Case "GDRY" : Return VisualRecordParsers.ParseGDRY(rec, pluginManager)
                Case "LENS" : Return VisualRecordParsers.ParseLENS(rec, pluginManager)
                Case "ARTO" : Return VisualRecordParsers.ParseARTO(rec, pluginManager)
                Case "IPCT" : Return VisualRecordParsers.ParseIPCT(rec, pluginManager)
                Case "IPDS" : Return VisualRecordParsers.ParseIPDS(rec, pluginManager)

                ' === Audio (AudioRecords.vb) ===
                Case "SNDR" : Return AudioRecordParsers.ParseSNDR(rec, pluginManager)
                Case "SNCT" : Return AudioRecordParsers.ParseSNCT(rec, pluginManager)
                Case "SOPM" : Return AudioRecordParsers.ParseSOPM(rec, pluginManager)
                Case "MUSC" : Return AudioRecordParsers.ParseMUSC(rec, pluginManager)
                Case "MUST" : Return AudioRecordParsers.ParseMUST(rec, pluginManager)
                Case "REVB" : Return AudioRecordParsers.ParseREVB(rec, pluginManager)
                Case "KSSM" : Return AudioRecordParsers.ParseKSSM(rec, pluginManager)
                Case "AECH" : Return AudioRecordParsers.ParseAECH(rec, pluginManager)
                Case "SCSN" : Return AudioRecordParsers.ParseSCSN(rec, pluginManager)
                Case "STAG" : Return AudioRecordParsers.ParseSTAG(rec, pluginManager)
                Case "SOUN" : Return AudioRecordParsers.ParseSOUN(rec, pluginManager)

                ' === Misc World Objects (MiscRecords.vb) ===
                Case "ACTI" : Return MiscRecordParsers.ParseACTI(rec, pluginManager)
                Case "STAT" : Return MiscRecordParsers.ParseSTAT(rec, pluginManager)
                Case "DOOR" : Return MiscRecordParsers.ParseDOOR(rec, pluginManager)
                Case "FURN" : Return MiscRecordParsers.ParseFURN(rec, pluginManager)
                Case "MSTT" : Return MiscRecordParsers.ParseMSTT(rec, pluginManager)
                Case "TREE" : Return MiscRecordParsers.ParseTREE(rec, pluginManager)
                Case "GRAS" : Return MiscRecordParsers.ParseGRAS(rec, pluginManager)
                Case "TERM" : Return MiscRecordParsers.ParseTERM(rec, pluginManager)
                Case "MESG" : Return MiscRecordParsers.ParseMESG(rec, pluginManager)
                Case "LSCR" : Return MiscRecordParsers.ParseLSCR(rec, pluginManager)
                Case "SCOL" : Return MiscRecordParsers.ParseSCOL(rec, pluginManager)
                Case "PKIN" : Return MiscRecordParsers.ParsePKIN(rec, pluginManager)
                Case "TACT" : Return MiscRecordParsers.ParseTACT(rec, pluginManager)
                Case "ADDN" : Return MiscRecordParsers.ParseADDN(rec, pluginManager)
                Case "ANIO" : Return MiscRecordParsers.ParseANIO(rec, pluginManager)
                Case "DEBR" : Return MiscRecordParsers.ParseDEBR(rec, pluginManager)

                ' === System / Infrastructure (SystemRecords.vb) ===
                Case "COLL" : Return SystemRecordParsers.ParseCOLL(rec, pluginManager)
                Case "DFOB" : Return SystemRecordParsers.ParseDFOB(rec, pluginManager)
                Case "DOBJ" : Return SystemRecordParsers.ParseDOBJ(rec, pluginManager)
                Case "AACT" : Return SystemRecordParsers.ParseAACT(rec, pluginManager)
                Case "ASPC" : Return SystemRecordParsers.ParseASPC(rec, pluginManager)
                Case "ASTP" : Return SystemRecordParsers.ParseASTP(rec, pluginManager)
                Case "AORU" : Return SystemRecordParsers.ParseAORU(rec, pluginManager)
                Case "BNDS" : Return SystemRecordParsers.ParseBNDS(rec, pluginManager)
                Case "DUAL" : Return SystemRecordParsers.ParseDUAL(rec, pluginManager)
                Case "ZOOM" : Return SystemRecordParsers.ParseZOOM(rec, pluginManager)
                Case "AMDL" : Return SystemRecordParsers.ParseAMDL(rec, pluginManager)
                Case "TRNS" : Return SystemRecordParsers.ParseTRNS(rec, pluginManager)
                Case "RFGP" : Return SystemRecordParsers.ParseRFGP(rec, pluginManager)
                Case "LAYR" : Return SystemRecordParsers.ParseLAYR(rec, pluginManager)
                Case "SCCO" : Return SystemRecordParsers.ParseSCCO(rec, pluginManager)
                Case "LAND" : Return SystemRecordParsers.ParseLAND(rec, pluginManager)
                Case "NAVI" : Return SystemRecordParsers.ParseNAVI(rec, pluginManager)
                Case "FSTP" : Return SystemRecordParsers.ParseFSTP(rec, pluginManager)
                Case "FSTS" : Return SystemRecordParsers.ParseFSTS(rec, pluginManager)
                Case "IDLM" : Return SystemRecordParsers.ParseIDLM(rec, pluginManager)

                ' === Additional FO4+SSE records (AdditionalRecords.vb) ===
                ' FO4 full
                Case "LCRT" : Return AdditionalRecordParsers.ParseLCRT(rec, pluginManager)
                Case "MATO" : Return AdditionalRecordParsers.ParseMATO(rec, pluginManager)
                Case "NOCM" : Return AdditionalRecordParsers.ParseNOCM(rec, pluginManager)
                Case "OVIS" : Return AdditionalRecordParsers.ParseOVIS(rec, pluginManager)
                Case "PLYR" : Return AdditionalRecordParsers.ParsePLYR(rec, pluginManager)
                ' FO4 stubs
                Case "LSPR" : Return AdditionalRecordParsers.ParseLSPR(rec, pluginManager)
                Case "MICN" : Return AdditionalRecordParsers.ParseMICN(rec, pluginManager)
                Case "SCPT" : Return AdditionalRecordParsers.ParseSCPT(rec, pluginManager)
                Case "SKIL" : Return AdditionalRecordParsers.ParseSKIL(rec, pluginManager)
                Case "TLOD" : Return AdditionalRecordParsers.ParseTLOD(rec, pluginManager)
                Case "TOFT" : Return AdditionalRecordParsers.ParseTOFT(rec, pluginManager)
                ' SSE full
                Case "SCRL" : Return AdditionalRecordParsers.ParseSCRL(rec, pluginManager)
                Case "SHOU" : Return AdditionalRecordParsers.ParseSHOU(rec, pluginManager)
                Case "WOOP" : Return AdditionalRecordParsers.ParseWOOP(rec, pluginManager)
                Case "RGDL" : Return AdditionalRecordParsers.ParseRGDL(rec, pluginManager)
                Case "APPA" : Return AdditionalRecordParsers.ParseAPPA(rec, pluginManager)
                Case "SLGM" : Return AdditionalRecordParsers.ParseSLGM(rec, pluginManager)
                Case "VOLI" : Return AdditionalRecordParsers.ParseVOLI(rec, pluginManager)
                ' SSE stubs
                Case "CLDC" : Return AdditionalRecordParsers.ParseCLDC(rec, pluginManager)
                Case "HAIR" : Return AdditionalRecordParsers.ParseHAIR(rec, pluginManager)
                Case "PWAT" : Return AdditionalRecordParsers.ParsePWAT(rec, pluginManager)

                Case Else
                    Return Nothing
            End Select
        Catch ex As Exception
            Debug.Print($"[ESP] Failed to parse {rec.Header.Signature} record {rec.Header.FormID:X8}: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Get the list of all supported record type signatures.
    ''' </summary>
    Public ReadOnly Property SupportedSignatures As HashSet(Of String) = New HashSet(Of String)(
        {"NPC_", "RACE", "ARMO", "ARMA", "OTFT", "HDPT", "TXST", "CLFM", "LVLN", "LVLI", "FLST", "MSWP",
         "WEAP", "AMMO", "ALCH", "MISC", "BOOK", "KEYM", "LIGH", "INGR", "CONT", "FLOR", "NOTE",
         "ENCH", "SPEL", "MGEF", "PERK", "LVSP", "KYWD", "EQUP", "GLOB", "GMST", "AVIF", "DMGT",
         "OMOD", "INNR", "COBJ", "CMPO", "MATT",
         "CELL", "WRLD", "LCTN", "NAVM", "ECZN", "REGN", "WATR", "WTHR", "CLMT", "LGTM", "LTEX",
         "FACT", "CLAS", "EYES", "BPTD", "MOVT", "CSTY", "VTYP", "RELA",
         "QUST", "DIAL", "INFO", "PACK", "SCEN", "IDLE", "DLBR", "DLVW", "SMBN", "SMEN", "SMQN",
         "IMGS", "IMAD", "EFSH", "PROJ", "EXPL", "HAZD", "CAMS", "CPTH", "RFCT", "SPGD", "GDRY", "LENS", "ARTO", "IPCT", "IPDS",
         "SNDR", "SNCT", "SOPM", "MUSC", "MUST", "REVB", "KSSM", "AECH", "SCSN", "STAG", "SOUN",
         "ACTI", "STAT", "DOOR", "FURN", "MSTT", "TREE", "GRAS", "TERM", "MESG", "LSCR", "SCOL", "PKIN", "TACT", "ADDN", "ANIO", "DEBR",
         "COLL", "DFOB", "DOBJ", "AACT", "ASPC", "ASTP", "AORU", "BNDS", "DUAL", "ZOOM", "AMDL", "TRNS", "RFGP", "LAYR", "SCCO", "LAND", "NAVI", "FSTP", "FSTS", "IDLM",
         "LCRT", "MATO", "NOCM", "OVIS", "PLYR", "LSPR", "MICN", "SCPT", "SKIL", "TLOD", "TOFT",
         "SCRL", "SHOU", "WOOP", "RGDL", "APPA", "SLGM", "VOLI", "CLDC", "HAIR", "PWAT"},
        StringComparer.Ordinal)

End Module
