Option Strict On
Option Explicit On

Imports System.Runtime.CompilerServices

''' <summary>THE law of Fallout 4's texture creation: what the engine does with a texture file that IS in the data (Fallout4.exe
''' 1.11.240, SHA-256 FDCEF37AC1230AF6D0B0050EB2142B139EF3A867B37B9211FB6EDFCC646072F8; Tools/re-docs/
''' RE_FO4_TEXTURE_CREATION_2026-10-19, "the RE" below). Three answers for a slot's sRGB request (Verdict):
''' <list type="bullet">
''' <item>Accepted: CreateTexture2D and CreateShaderResourceView succeed (0x14183EA70) and the slot samples the file.</item>
''' <item>Rejected: one of them fails, or the file is empty; the NiTexture exists, so neither the ladder of an absent file nor the
''' smooth-spec White swap applies, and the binder binds the NULL SRV: (0, 0, 0, 0) (RE 5; EngineDefaultTextureLaw.SrvNull).</item>
''' <item>NotLaw: D3D accepts what the engine builds but the engine reads the file inconsistently (data it does not have, a layout
''' it does not read, a format D3D cannot sample): what the game draws is not a law of the exe (RE 3.3, 4.3, 4.4, 8.2).</item>
''' </list>
''' Pure: the descriptor comes from the DDS header (path B, 0x14183F320) or the BA2 DX10 record (path A, 0x14183F680) and the
''' verdict from the descriptor. D3D11's part is the runtime's rules MEASURED on WARP with the engine's exact descriptor and
''' initial data (RE 4.1 / 4.2, fmt_table.txt, warp_probe_out.txt, pitch_probe_out.txt, and warp_probe2_out.txt of the same folder
''' for the R8G8_B8G8 width and the BC / mip rules on lower mips). The slot-4 cube has its own consequences (RE Addendum A.2 - A.5:
''' LightingCubeVerdict, EffectCubeVerdict). The reader of the data (DescriptorOf) is apart, with its cache per file entry.</summary>
Friend Module EngineTextureCreationLaw

    ''' <summary>The three answers of the law, plus Unread: the app could not read the entry's header or record (no claim).</summary>
    Friend Enum CreationOutcome As Byte
        Accepted = 0
        Rejected = 1
        NotLaw = 2
        Unread = 3
    End Enum

    ''' <summary>A verdict and, for Rejected / NotLaw, the rule (UI English: it is the notice's text). Crashes: a NotLaw whose game
    ''' reads an invalid address and falls (RE Addendum A.4 / A.5) - not an undefined value but a crash: the data is wrong.</summary>
    Friend Structure CreationVerdict
        Friend Outcome As CreationOutcome
        Friend Cause As String
        Friend Crashes As Boolean
        Friend Shared Function Make(outcome As CreationOutcome, cause As String) As CreationVerdict
            Return New CreationVerdict With {.Outcome = outcome, .Cause = cause}
        End Function
        ''' <summary>NotLaw-crash: the engine reads an invalid address with this file (<paramref name="cause"/> names it).</summary>
        Friend Shared Function Crash(cause As String) As CreationVerdict
#If DEBUG Then
            If Fo4GBufferConstants.GateMutantOn("tcl-crash-unflagged") Then Return Make(CreationOutcome.NotLaw, cause)   ' ParityGate / ShadowGate
#End If
            Return New CreationVerdict With {.Outcome = CreationOutcome.NotLaw, .Cause = cause, .Crashes = True}
        End Function
    End Structure

    ''' <summary>How the engine got its descriptor.</summary>
    Friend Enum DescriptorSource As Byte
        ''' <summary>Path B: a loose file or a GNRL archive entry, header read blind by 0x14183F320.</summary>
        DdsHeader = 0
        ''' <summary>Path A: a BA2 DX10 entry through the streaming index, the record's qword copied verbatim (0x1417CCB9D..CBB7).</summary>
        Ba2Record = 1
        ''' <summary>A file of 0 bytes: 0x14183F36B..374 returns NULL before any D3D call ([NiTexture+0x38] = NULL).</summary>
        EmptyFile = 2
        ''' <summary>A file shorter than the header 0x14183F320 reads (0x80, or 0x94 with DX10): no size test before 0x14183F3C2,
        ''' the fields are read past the buffer.</summary>
        ShortHeader = 3
    End Enum

    ''' <summary>The texture header the engine builds (0x14183E680 copies it to tex+0x28: +0x28 height, +0x2A width, +0x2C mips,
    ''' +0x2D DXGI byte, +0x2E cube; RE 2.1 / 2.4) and what the file does not match of it (Inconsistency: Nothing when consistent).
    ''' </summary>
    Friend Structure TextureDescriptor
        Friend Source As DescriptorSource
        Friend Height As Integer
        Friend Width As Integer
        Friend MipLevels As Integer
        Friend Format As Integer
        Friend Cube As Boolean
        Friend Inconsistency As String
    End Structure

    ''' <summary>The header 0x14183F320 reads: 0x80 bytes, 0x94 with the DX10 extension (0x14183F3DE..3F0).</summary>
    Friend Const DdsHeaderBytes As Integer = &H94
    Private Const DdsLegacyHeaderBytes As Integer = &H80
    Private Const FourCcDx10 As UInteger = &H30315844UI          ' 'DX10' (0x14183F3CF)
    Private Const DdpfFourCc As UInteger = &H4UI
    Private Const DdpfRgb As UInteger = &H40UI
    Private Const DdpfLuminance As UInteger = &H20000UI
    Private Const Ddscaps2Cubemap As UInteger = &H200UI         ' 0x14183F409..411
    Private Const Ddscaps2Volume As UInteger = &H200000UI

    Private Function U32(b As Byte(), offset As Integer) As UInteger
        Return BitConverter.ToUInt32(b, offset)
    End Function

    ''' <summary>Path B, 0x14183F320: the descriptor from the first bytes of the file (<paramref name="head"/>: at least
    ''' DdsHeaderBytes when the file has them) and its length. Nothing of the header is validated by the exe (RE 2.1): the magic,
    ''' dwSize, ddspf.dwSize, the flags, depth, caps2 faces, DDSCAPS2_VOLUME, and the DX10 dimension / misc / array are not read;
    ''' the ones that make the file say something the engine does not read are its Inconsistency.</summary>
    Friend Function FromDdsHeader(head As Byte(), fileLength As Long) As TextureDescriptor
        If fileLength = 0 Then Return New TextureDescriptor With {.Source = DescriptorSource.EmptyFile}
        If fileLength < DdsLegacyHeaderBytes OrElse head.Length < DdsLegacyHeaderBytes Then Return New TextureDescriptor With {.Source = DescriptorSource.ShortHeader}
        Dim pfFlags = U32(head, &H50), fourCc = U32(head, &H54)
        Dim dx10 = (pfFlags And DdpfFourCc) <> 0UI AndAlso fourCc = FourCcDx10               ' 0x14183F3C2..3CF
        If dx10 AndAlso (fileLength < DdsHeaderBytes OrElse head.Length < DdsHeaderBytes) Then Return New TextureDescriptor With {.Source = DescriptorSource.ShortHeader}
        Dim headerSize = If(dx10, DdsHeaderBytes, DdsLegacyHeaderBytes)                        ' 0x14183F3DE..3F0
        Dim formatDword = If(dx10, U32(head, &H80), CUInt(LegacyPixelFormatToDxgi(head, &H4C))) ' 0x14183F3F8 / 0x14183F400
        Dim heightDword = U32(head, &HC), widthDword = U32(head, &H10)
        Dim mipDword = U32(head, &H1C)
        Dim caps2 = U32(head, &H70)
        Dim d As New TextureDescriptor With {
            .Source = DescriptorSource.DdsHeader,
            .Height = CInt(heightDword And &HFFFFUI),                                           ' word, 0x14183F415 / 423
            .Width = CInt(widthDword And &HFFFFUI),                                             ' word, 0x14183F418 / 41B
            .MipLevels = If((mipDword And &HFFUI) <> 0UI, CInt(mipDword And &HFFUI), If(mipDword < 1UI, 1, 0)), ' 0x14183F432..441
            .Format = CInt(formatDword And &HFFUI),                                             ' a byte, 0x14183F42B
            .Cube = (caps2 And Ddscaps2Cubemap) <> 0UI}                                        ' 0x14183F409..411 / 448
#If DEBUG Then
        ' ParityGate fo4-texture-creation-law's directed mutants (Fo4GBufferConstants.GateMutantOn, the one switch).
        If Fo4GBufferConstants.GateMutantOn("tcl-b-word") Then d.Height = CInt(heightDword And &H7FFFFFFFUI) : d.Width = CInt(widthDword And &H7FFFFFFFUI)
        If Fo4GBufferConstants.GateMutantOn("tcl-b-mips256") AndAlso d.MipLevels = 0 Then d.MipLevels = 1
        If Fo4GBufferConstants.GateMutantOn("tcl-b-cube-faces") Then d.Cube = (caps2 And &HFC00UI) <> 0UI
#End If
        ' What the file declares and the engine does not read (RE 2.1, 4.3, 8.2; engine_law.py's notes): the engine creates the
        ' texture from other bytes than the file means.
        Dim dataBytes = CLng(CUInt((fileLength - headerSize) And &HFFFFFFFFL))                  ' 32-bit unsigned, 0x14183F48C
        If head(0) <> &H44 OrElse head(1) <> &H44 OrElse head(2) <> &H53 OrElse head(3) <> &H20 Then
            d.Inconsistency = "no 'DDS ' magic (the engine reads the header blind)"
        ElseIf formatDword > &HFFUI Then
            d.Inconsistency = $"DXGI format {formatDword} (the engine keeps its low byte)"
        ElseIf heightDword > &HFFFFUI OrElse widthDword > &HFFFFUI Then
            d.Inconsistency = "width or height above 65535 (the engine keeps 16 bits)"
        ElseIf mipDword > &HFFUI Then
            d.Inconsistency = $"{mipDword} mip levels (the engine keeps the low byte)"
        ElseIf (caps2 And Ddscaps2Volume) <> 0UI OrElse (dx10 AndAlso U32(head, &H84) = 4UI) Then
            d.Inconsistency = "a volume texture (the engine creates a 2D of its first slice)"
        ElseIf dx10 AndAlso U32(head, &H8C) > 1UI AndAlso Not d.Cube Then
            d.Inconsistency = "a texture array (the engine creates one 2D)"
        ElseIf dx10 AndAlso (U32(head, &H88) And 4UI) <> 0UI AndAlso Not d.Cube Then
            d.Inconsistency = "a DX10 cube without DDSCAPS2_CUBEMAP (the engine creates a 2D)"
        ElseIf Not dx10 AndAlso (pfFlags And DdpfRgb) = 0UI AndAlso (pfFlags And DdpfFourCc) = 0UI AndAlso
               (pfFlags And DdpfLuminance) <> 0UI AndAlso
               Not (U32(head, &H58) = 8UI AndAlso U32(head, &H5C) = &HFFUI AndAlso U32(head, &H68) = 0UI) Then
            ' Only L8 (8 bits, R mask 0xFF, no alpha mask) is the R8_UNORM the engine creates for any luminance (0x14183E2F2):
            ' L16, A8L8, A4L4 or another bit count are read as R8 with an 8-bit pitch (RE 8.2).
            d.Inconsistency = $"a luminance format other than L8 ({U32(head, &H58)} bits; the engine reads it as R8_UNORM, 0x14183E2F2)"
        ElseIf Not dx10 AndAlso (pfFlags And DdpfRgb) <> 0UI AndAlso (pfFlags And DdpfFourCc) <> 0UI AndAlso d.Format <> 0 Then
            d.Inconsistency = "both RGB and FourCC pixel-format flags (the engine reads the RGB masks, 0x14183E053)"
        ElseIf SpannedBytes(d) > dataBytes AndAlso Not ShortDataMutant() Then
            d.Inconsistency = "data shorter than its header declares (the engine reads past the file)"
        End If
        Return d
    End Function

    ''' <summary>Path A, 0x14183F680: the BA2 DX10 record as the engine copies it (0x1417CCB9D..CBB7: height, width, mips, format,
    ''' flags; the tile byte is overwritten with 0 at 0x14183E6C2, and the mips have no max(.., 1), RE 2.4).
    ''' <paramref name="dataBytes"/> = the sum of the entry's chunks (0x14183CF40 sums them, 0x1417C6BD0).</summary>
    Friend Function FromBa2Record(height As Integer, width As Integer, mips As Integer, format As Integer, flags As Integer,
                                  dataBytes As Long) As TextureDescriptor
        Dim d As New TextureDescriptor With {
            .Source = DescriptorSource.Ba2Record, .Height = height, .Width = width, .MipLevels = mips, .Format = format,
            .Cube = (flags And 1) <> 0}
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-a-mips-max") Then d.MipLevels = Math.Max(mips, 1)
        If Fo4GBufferConstants.GateMutantOn("tcl-a-cube-bit") Then d.Cube = (flags And 2) <> 0
#End If
        If SpannedBytes(d) > dataBytes AndAlso Not ShortDataMutant() Then d.Inconsistency = "chunks shorter than the record declares (the engine reads past them)"
        Return d
    End Function

    ''' <summary>DEBUG ONLY: ParityGate's mutant "tcl-short-off" (no test of the data length). Always False in Release.
    ''' <para>Ola 9 (ronda 3), declared: it stays in Release as a constant False - the pattern of GateSoftApagado /
    ''' GateMascaraDeCalcaAbierta - because its two callers are the LAST branch of the engine law's ElseIf chain (the DDS header and the
    ''' BA2 record): splitting a branch of that chain with #If would write the law twice.</para></summary>
    Private Function ShortDataMutant() As Boolean
#If DEBUG Then
        Return Fo4GBufferConstants.GateMutantOn("tcl-short-off")
#Else
        Return False
#End If
    End Function

    ''' <summary>0x14183E050: the legacy pixel format at <paramref name="offset"/> (dwFlags +4, dwFourCC +8, dwRGBBitCount +0xC,
    ''' masks R +0x10, G +0x14, B +0x18, A +0x1C) to its DXGI code; 0 = UNKNOWN. RGB first (0x14183E053), then FourCC (0x14183E1F1),
    ''' then luminance (0x14183E2F2); RE 2.2, jt_e050_out.txt.</summary>
    Friend Function LegacyPixelFormatToDxgi(head As Byte(), offset As Integer) As Integer
        Dim flags = U32(head, offset + 4), fourCc = U32(head, offset + 8), bits = U32(head, offset + &HC)
        Dim r = U32(head, offset + &H10), g = U32(head, offset + &H14), b = U32(head, offset + &H18), a = U32(head, offset + &H1C)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-e050-fourcc-first") AndAlso (flags And DdpfFourCc) <> 0UI Then flags = flags And Not DdpfRgb
        If Fo4GBufferConstants.GateMutantOn("tcl-e050-numeric") AndAlso (flags And DdpfRgb) = 0UI AndAlso (flags And DdpfFourCc) <> 0UI AndAlso fourCc <= &HFFUI Then Return 0
        If Fo4GBufferConstants.GateMutantOn("tcl-e050-lum-masks") AndAlso (flags And (DdpfRgb Or DdpfFourCc)) = 0UI AndAlso (flags And DdpfLuminance) <> 0UI AndAlso bits <> 8UI Then Return 0
        If Fo4GBufferConstants.GateMutantOn("tcl-e050-rgb24") AndAlso (flags And DdpfRgb) <> 0UI AndAlso bits = 24UI Then Return 87
#End If
        If (flags And DdpfRgb) <> 0UI Then
            If bits = 32UI Then                                                                   ' 0x14183E067
                If r = &HFFUI AndAlso g = &HFF00UI AndAlso b = &HFF0000UI AndAlso a = &HFF000000UI Then Return 28    ' 0x14183E070..0A6
                If r = &HFF0000UI AndAlso g = &HFF00UI AndAlso b = &HFFUI AndAlso a = &HFF000000UI Then Return 87    ' 0x14183E0A7..0D3
                If r = &HFF0000UI AndAlso g = &HFF00UI AndAlso b = &HFFUI AndAlso a = 0UI Then Return 88             ' 0x14183E0D4..0FB
                If r = &H3FF00000UI AndAlso g = &HFFC00UI AndAlso b = &H3FFUI AndAlso a = &HC0000000UI Then Return 24 ' 0x14183E0FC..12F
                If r = &HFFFFUI AndAlso g = &HFFFF0000UI AndAlso b = 0UI AndAlso a = 0UI Then Return 35               ' 0x14183E130..15D
                If r = &HFFFFFFFFUI AndAlso g = 0UI AndAlso b = 0UI AndAlso a = 0UI Then Return 41                   ' 0x14183E15E..18A
                Return 0
            End If
            If bits = 16UI Then                                                                   ' 0x14183E18B
                If r = &H7C00UI AndAlso g = &H3E0UI AndAlso b = &H1FUI AndAlso a = &H8000UI Then Return 86            ' ..1BE
                If r = &HF800UI AndAlso g = &H7E0UI AndAlso b = &H1FUI AndAlso a = 0UI Then Return 85                 ' ..1F0
                Return 0
            End If
            Return 0                                                                               ' 0x14183E06A
        End If
        If (flags And DdpfFourCc) <> 0UI Then
            Select Case fourCc                                                                     ' 0x14183E1F9..28C
                Case &H31545844UI : Return 71                                  ' DXT1
                Case &H32545844UI, &H33545844UI : Return 74                    ' DXT2, DXT3
                Case &H34545844UI, &H35545844UI : Return 77                    ' DXT4, DXT5
                Case &H31495441UI, &H55344342UI : Return 80                    ' ATI1, BC4U
                Case &H53344342UI : Return 81                                  ' BC4S
                Case &H32495441UI, &H55354342UI : Return 83                    ' ATI2, BC5U
                Case &H53354342UI : Return 84                                  ' BC5S
                Case &H47424752UI : Return 68                                  ' RGBG
                Case &H42475247UI : Return 69                                  ' GRGB
                ' Numeric D3DFMT (fourCC - 0x24 in 0..0x50, tables 0x14183E328 / 0x14183E304, 0x14183E28D..2AE)
                Case 36UI : Return 11
                Case 110UI : Return 13
                Case 111UI : Return 54
                Case 112UI : Return 34
                Case 113UI : Return 10
                Case 114UI : Return 41
                Case 115UI : Return 16
                Case 116UI : Return 2
                Case Else : Return 0                                           ' 0x14183E293 -> E2FE
            End Select
        End If
        If (flags And DdpfLuminance) <> 0UI Then Return 61                                         ' R8_UNORM, nothing else read
        Return 0                                                                                   ' 0x14183E2FE
    End Function

    ''' <summary>0x14183DCE0: bits per pixel of a DXGI code (tables 0x14183DD58 / 0x14183DD38, jt_sizes_out.txt); 0 outside
    ''' 1..99.</summary>
    Friend Function BitsPerPixel(format As Integer) As Integer
        Select Case format
            Case 1 To 4 : Return 128
            Case 5 To 8 : Return 96
            Case 9 To 22 : Return 64
            Case 23 To 47 : Return 32
            Case 48 To 59 : Return 16
            Case 60 To 65 : Return 8
            Case 66 : Return 1
            Case 67 To 69 : Return 32
            Case 70 To 72, 79 To 81 : Return 4
            Case 73 To 78, 82 To 84 : Return 8
            Case 85, 86 : Return 16
            Case 87 To 93 : Return 32
            Case 94 To 99 : Return 8
            Case Else
#If DEBUG Then
                If Fo4GBufferConstants.GateMutantOn("tcl-bpp-100") AndAlso format >= 100 Then Return 32
#End If
                Return 0
        End Select
    End Function

    ''' <summary>The block of a block-compressed DXGI code, the pitch class of 0x14183E470 (tables 0x14183E64C / 0x14183E63C): 8
    ''' bytes for BC1 / BC4, 16 for BC2 / BC3 / BC5 / BC6H / BC7; 0 = not block-compressed.</summary>
    Private Function BlockBytes(format As Integer) As Integer
        Select Case format
            Case 70 To 72, 79 To 81 : Return 8
            Case 73 To 78, 82 To 84, 94 To 99 : Return 16
            Case Else : Return 0
        End Select
    End Function

    ''' <summary>The bytes the engine's subresources span (0x14183E470): per face (6 with the cube bit) and per mip, w and h of
    ''' the mip with floor 1 (0x14183E5D8..5F1); BC: ((w+3)/4)*block per row times (h+3)/4 rows (0x14183E544..579); R8G8_B8G8 /
    ''' G8R8_G8B8: ((w+1)/2)*4 times h (0x14183E57B..583); else (bpp*w+7)/8 times h (0x14183E585..5A8). 32-bit as the exe.</summary>
    Private Function SpannedBytes(d As TextureDescriptor) As Long
        Dim total As Long = 0
        Dim block = BlockBytes(d.Format)
        For face = 1 To If(d.Cube, 6, 1)
            Dim w = d.Width, h = d.Height
            For m = 1 To d.MipLevels
                Dim rowPitch As Long, slice As Long
                If block <> 0 Then
                    rowPitch = If(w = 0, 0L, CLng((w + 3) >> 2) * block)
                    slice = rowPitch * If(h = 0, 0L, CLng((h + 3) >> 2))
                ElseIf d.Format = 68 OrElse d.Format = 69 Then
                    rowPitch = CLng((w + 1) >> 1) << 2
                    slice = rowPitch * h
                Else
                    rowPitch = (CLng(BitsPerPixel(d.Format)) * w + 7) >> 3
                    slice = rowPitch * h
                End If
                total += slice And &HFFFFFFFFL
                w = If(w >= 2, w >> 1, 1)
                h = If(h >= 2, h >> 1, 1)
            Next
        Next
        Return total
    End Function

    ''' <summary>The verdict for a slot whose loader passes <paramref name="srgb"/> (the slot's flag, 0x142182420 5th argument -&gt;
    ''' dl of 0x14183E680, RE 1.3; EngineDefaultTextureLaw.Fo4SlotLoadsSrgb). Order: what the engine decides before D3D (an empty
    ''' file, a header read past the file, 0 mips), D3D's rules on the promoted format, then what makes an accepted creation not a
    ''' law.</summary>
    Friend Function Verdict(d As TextureDescriptor, srgb As Boolean) As CreationVerdict
        Select Case d.Source
            Case DescriptorSource.EmptyFile
#If DEBUG Then
                If Fo4GBufferConstants.GateMutantOn("tcl-empty-notlaw") Then Return CreationVerdict.Make(CreationOutcome.NotLaw, "an empty file")
#End If
                Return CreationVerdict.Make(CreationOutcome.Rejected, "an empty file")
            Case DescriptorSource.ShortHeader
                Return CreationVerdict.Make(CreationOutcome.NotLaw, "a file shorter than its DDS header (the engine reads past it)")
        End Select
        ' MipLevels 0 reaches D3D (path A has no max(.., 1); path B a mip count of 256): D3D builds the full chain from initial
        ' data 0x14183E470 never wrote (0x14183E4F6, RE 4.4).
        If d.MipLevels = 0 Then Return CreationVerdict.Make(CreationOutcome.NotLaw, "0 mip levels (the engine hands D3D unwritten data)")
        ' The format of the texture AND of its view: MakeSRGB 0x14183E680 with dl = 1 (RE 2.3; the app's transcription).
        Dim format = If(srgb, DirectXDDSLoader.MakeSrgbDxgi(d.Format), d.Format)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-no-srgb-rescue") Then format = d.Format
#End If
        Dim rejected = D3D11Rejects(d.Width, d.Height, d.MipLevels, d.Cube, format)
        If rejected IsNot Nothing Then Return CreationVerdict.Make(CreationOutcome.Rejected, rejected)
        ' FL 11 documents 16384; WARP accepted 16385..65535 (warp_probe2_out.txt) and the hardware was not measured (RE 4.4).
        Dim bigIsNotLaw = True
#If DEBUG Then
        bigIsNotLaw = Not Fo4GBufferConstants.GateMutantOn("tcl-big-accepted")
#End If
        If bigIsNotLaw AndAlso (d.Width > 16384 OrElse d.Height > 16384) Then Return CreationVerdict.Make(CreationOutcome.NotLaw, "wider or taller than 16384")
        If d.Inconsistency IsNot Nothing Then Return CreationVerdict.Make(CreationOutcome.NotLaw, d.Inconsistency)
        If Not ShaderSampleable(format) Then Return CreationVerdict.Make(CreationOutcome.NotLaw, $"DXGI format {format} cannot be sampled (an integer format)")
        Return CreationVerdict.Make(CreationOutcome.Accepted, Nothing)
    End Function

    ''' <summary>D3D11's CreateTexture2D / CreateShaderResourceView on the engine's descriptor (0x14183EBB8..EC36, view
    ''' 0x14183ED63..EDB0) and initial data (0x14183E470), MEASURED on WARP (RE 4.1 / 4.2: fmt_table.txt, warp_probe_out.txt,
    ''' pitch_probe_out.txt; warp_probe2_out.txt): the rule that fails, or Nothing. The same for 2D and cube (fmt_table.txt).</summary>
    Private Function D3D11Rejects(w As Integer, h As Integer, mips As Integer, cube As Boolean, format As Integer) As String
        If format = 0 Then Return "DXGI format 0 (UNKNOWN)"
        ' 0x14183DCE0 gives 0 bits per pixel above 99: SysMemPitch 0, E_INVALIDARG even for a format the device supports (115).
        If BitsPerPixel(format) = 0 Then Return $"DXGI format {format} (the engine computes a row pitch of 0)"
        Select Case format
            Case 20, 40, 45, 55 : Return $"DXGI format {format} (a depth format)"
            Case 66 : Return "DXGI format 66 (R1_UNORM)"
            Case 89 : Return "DXGI format 89 (R10G10B10_XR_BIAS_A2_UNORM)"
            Case 1, 5, 9, 15, 19, 23, 27, 33, 39, 44, 48, 53, 60, 70, 73, 76, 79, 82, 90, 92, 94, 97
                Return $"DXGI format {format} (TYPELESS: no view)"
        End Select
        If w = 0 OrElse h = 0 Then Return "a width or height of 0"
        Dim testBc = True, testOdd = True, testChain = True, testSquare = True
#If DEBUG Then
        testBc = Not Fo4GBufferConstants.GateMutantOn("tcl-bc-mult4-off") : testOdd = Not Fo4GBufferConstants.GateMutantOn("tcl-packed-odd-off")
        testChain = Not Fo4GBufferConstants.GateMutantOn("tcl-mips-chain-off") : testSquare = Not Fo4GBufferConstants.GateMutantOn("tcl-cube-square-off")
#End If
        If testBc AndAlso BlockBytes(format) <> 0 AndAlso (w Mod 4 <> 0 OrElse h Mod 4 <> 0) Then Return "block-compressed with a width or height that is not a multiple of 4"
        If testOdd AndAlso (format = 68 OrElse format = 69) AndAlso w Mod 2 <> 0 Then Return $"DXGI format {format} with an odd width"
        Dim chain = 1, extent = Math.Max(w, h)
        While extent > 1 : extent >>= 1 : chain += 1 : End While
        If testChain AndAlso mips > chain Then Return $"{mips} mip levels for a {w} x {h} texture"
        If testSquare AndAlso cube AndAlso w <> h Then Return "a cube that is not square"
        Return Nothing
    End Function

    ''' <summary>The verdict of the LIGHTING slot-4 cube (sRGB slot), which is not bound by SetupMaterial but registered in the
    ''' EnvMapArray (RE Addendum A.2). <paramref name="registerAccepts"/>: the register's header test 0x142239942..9EE accepts the
    ''' descriptor's header now (Fo4EnvMapArray.AcceptsHeader). A file the engine cannot create:
    ''' <list type="bullet">
    ''' <item>empty: [NiTexture+0x38] NULL, 0x142205913 -&gt; constant .x 0 (0x14220593A), no reflection; except the global cube
    ''' [[0x143E5DF60]+0x320] taken when [[0x143E5DF60]+0x2F8] &gt; 0 (0x14220591E..938), NOT TRACED: what it is and when it is set.
    ''' Rejected (the app draws no reflection).</item>
    ''' <item>header refused (height, width, format, cube or fewer mips than the array): 0xFF (0x14223999F), constant .x 0
    ''' (0x142205947..961), no reflection: Rejected, the law.</item>
    ''' <item>header accepted (the array's header, reachable only with mips &gt; log2+1; or no array yet and this cube first): the
    ''' copy 0x14181D020 takes the NULL resource (0x14181D050) and D3D drops it, so the slice keeps its earlier content (A.3, measured
    ''' on WARP): NotLaw; with no array and no queued cube (<paramref name="arraySeeded"/> False: this cube creates the array) the
    ''' creation reads address 0 (0x14181CDE3, A.4): NotLaw-crash.</item>
    ''' </list></summary>
    Friend Function LightingCubeVerdict(d As TextureDescriptor, registerAccepts As Boolean, Optional arraySeeded As Boolean = True) As CreationVerdict
        Dim v = Verdict(d, True)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-lightcube-accepts-ignored") Then registerAccepts = False
#End If
        If v.Outcome <> CreationOutcome.Rejected OrElse d.Source = DescriptorSource.EmptyFile OrElse Not registerAccepts Then Return v
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-lightcube-seed-ignored") Then arraySeeded = True
#End If
        If Not arraySeeded Then
            Return CreationVerdict.Crash($"a cube the game cannot create ({v.Cause}) whose header the environment array accepts; with no array yet the game crashes reading address 0")
        End If
        Return CreationVerdict.Make(CreationOutcome.NotLaw, $"a cube the game cannot create ({v.Cause}) whose header the environment array accepts")
    End Function

    ''' <summary>The verdict of the EFFECT's slot-4 cube (t5 under technique bit 19, sRGB slot; RE Addendum A.5): a failed creation
    ''' binds its NULL SRV (0x142225C1E), as every effect slot; an EMPTY file leaves [NiTexture+0x38] NULL and the bind reads byte
    ''' [0 + 0x3C] without a test (0x142225C69): NotLaw-crash.</summary>
    Friend Function EffectCubeVerdict(d As TextureDescriptor) As CreationVerdict
        Dim emptyIsNotLaw = True
#If DEBUG Then
        emptyIsNotLaw = Not Fo4GBufferConstants.GateMutantOn("tcl-effectcube-empty-rejected")
#End If
        If emptyIsNotLaw AndAlso d.Source = DescriptorSource.EmptyFile Then
            Return CreationVerdict.Crash("an empty file in the effect's cube: the game crashes reading address 0x3C")
        End If
        Return Verdict(d, True)
    End Function

    ''' <summary>D3D11_FORMAT_SUPPORT_SHADER_SAMPLE (0x200) of the formats D3D creates and views, MEASURED (fmt_support_warp.txt):
    ''' clear exactly for the integer formats (UINT / SINT, and X32_TYPELESS_G8X24_UINT / X24_TYPELESS_G8_UINT). The engine's pixel
    ''' shaders sample every slot, and sampling such a view is undefined.</summary>
    Private Function ShaderSampleable(format As Integer) As Boolean
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-integer-sampled") Then Return True
#End If
        Select Case format
            Case 3, 4, 7, 8, 12, 14, 17, 18, 22, 25, 30, 32, 36, 38, 42, 43, 47, 50, 52, 57, 59, 62, 64 : Return False
            Case Else : Return True
        End Select
    End Function

    ' ---------------------------------------------------------------------------------------------------------------------------
    ' The reader (not pure): the descriptor of the file the data dictionary resolves a path to.
    ' ---------------------------------------------------------------------------------------------------------------------------

    ''' <summary>Descriptors read, per file entry: a File_Location is the winner of its path until the dictionary replaces it (a
    ''' re-scan, a pack), so the entry is the key and the cache dies with it - a failed read too (Nothing), with its entry.</summary>
    Private ReadOnly _descriptors As New ConditionalWeakTable(Of FilesDictionary_class.File_Location, StrongBox(Of TextureDescriptor?))

    ''' <summary>The descriptor the engine builds for the file <paramref name="path"/> resolves to in the data, or Nothing when it is
    ''' not in the data or its header / record could not be read. Reads only the header or the record: a loose file's first
    ''' DdsHeaderBytes, a DX10 entry's record (no extraction), a GNRL entry's first DdsHeaderBytes (File_Location.ArchiveHead).</summary>
    Friend Function DescriptorOf(path As String) As TextureDescriptor?
        If String.IsNullOrEmpty(path) Then Return Nothing
        Dim loc = FilesDictionary_class.TryGetEntry(path)
        If loc Is Nothing Then Return Nothing
        Dim box As StrongBox(Of TextureDescriptor?) = Nothing
        Dim cached = _descriptors.TryGetValue(loc, box)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("tcl-negcache-off") AndAlso cached AndAlso Not box.Value.HasValue Then cached = False   ' ParityGate [T-negcache]
#End If
        If cached Then Return box.Value
        Dim d = ReadDescriptor(loc)
        _descriptors.AddOrUpdate(loc, New StrongBox(Of TextureDescriptor?)(d))
        Return d
    End Function

    Private Function ReadDescriptor(loc As FilesDictionary_class.File_Location) As TextureDescriptor?
        If loc.IsLosseFile Then
            Dim length As Long
            Dim looseHead = loc.ReadLooseHead(DdsHeaderBytes, length)
            If looseHead Is Nothing Then Return Nothing
            Return FromDdsHeader(looseHead, length)
        End If
        Dim info = loc.ArchiveTextureInfo()
        If Not info.Found Then Return Nothing
        Dim recordRead = True
#If DEBUG Then
        recordRead = Not Fo4GBufferConstants.GateMutantOn("tcl-reader-dx10-as-header")
        If Fo4GBufferConstants.GateMutantOn("tcl-tile-unread") AndAlso info.Dx10Record.HasValue AndAlso info.Dx10Record.Value.TileMode <> 8 Then Return Nothing
#End If
        If recordRead AndAlso info.Dx10Record.HasValue Then
            Dim r = info.Dx10Record.Value
#If DEBUG Then
            If Fo4GBufferConstants.GateMutantOn("tcl-reader-hw-swapped") Then Return FromBa2Record(r.Width, r.Height, r.MipCount, r.DxgiFormat, r.Flags, info.DecompressedSize)
#End If
            Return FromBa2Record(r.Height, r.Width, r.MipCount, r.DxgiFormat, r.Flags, info.DecompressedSize)
        End If
        If info.DecompressedSize = 0 Then Return FromDdsHeader(Array.Empty(Of Byte)(), 0)
        ' A GNRL entry: its first DdsHeaderBytes (a zlib chunk inflated only that far) and its length = the sum of its chunks.
        Dim head = loc.ArchiveHead(DdsHeaderBytes)
        If head Is Nothing OrElse head.Length = 0 Then Return Nothing   ' the entry declares data and the read gave none: a failed read
        Return FromDdsHeader(head, info.DecompressedSize)
    End Function

End Module
