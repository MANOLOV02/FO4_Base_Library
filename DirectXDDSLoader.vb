' Version Uploaded of Fo4Library 3.2.0
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime
Imports System.Runtime.InteropServices
Imports DirectXTexWrapperCLI
Imports OpenTK.Graphics.OpenGL4   ' Ajusta según tu binding de OpenGL

Public Module DirectXDDSLoader

    ''' <summary>
    ''' Genera un DDS de fallback (32×32, BGRA8 gris).
    ''' </summary>
    Public Function GenerateFallbackDDS() As Byte()
        Const width As Integer = 32, height As Integer = 32, bpp As Integer = 4
        Dim pixelData(width * height * bpp - 1) As Byte
        For i As Integer = 0 To pixelData.Length - 1 Step bpp
            pixelData(i + 0) = &H80  ' B
            pixelData(i + 1) = &H80  ' G
            pixelData(i + 2) = &H80  ' R
            pixelData(i + 3) = &HFF  ' A
        Next

        Using ms As New MemoryStream(), bw As New BinaryWriter(ms)
            bw.Write(&H20534444)           ' "DDS "
            bw.Write(124UI)                ' size
            bw.Write(&H21007UI)         ' flags
            bw.Write(CUInt(height))        ' height
            bw.Write(CUInt(width))         ' width
            bw.Write(CUInt(width * height * bpp)) ' pitchOrLinearSize
            bw.Write(0UI)                  ' depth
            bw.Write(0UI)                  ' mipCount
            For i As Integer = 0 To 10 : bw.Write(0UI) : Next
            ' PIXELFORMAT
            bw.Write(32UI)                 ' size
            bw.Write(&H4UI)                ' flags (RGB)
            bw.Write(CUInt(&H30315844))     ' fourCC = "DX10"
            bw.Write(32UI)                 ' RGBBitCount
            bw.Write(&HFF0000UI)         ' R mask
            bw.Write(&HFF00UI)         ' G mask
            bw.Write(&HFFUI)         ' B mask
            bw.Write(&HFF000000UI)         ' A mask
            bw.Write(&H1000UI)             ' caps
            bw.Write(0UI) : bw.Write(0UI) : bw.Write(0UI) : bw.Write(0UI)
            bw.Write(0UI)                  ' reserved2
            ' DXT10 header
            bw.Write(CUInt(&H1B))          ' DXGI_FORMAT_B8G8R8A8_UNORM
            bw.Write(3UI)                  ' TEXTURE2D
            bw.Write(0UI)                  ' miscFlag
            bw.Write(1UI)                  ' arraySize
            bw.Write(0UI)                  ' miscFlags2
            bw.Write(pixelData)
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>
    ''' Convierte un DDS a Bitmap .NET (nivel 0).
    ''' </summary>
    Public Function CreateBitmapFromDDS(ddsBytes As Byte()) As Bitmap
        If ddsBytes Is Nothing OrElse ddsBytes.Length = 0 Then Return Nothing
        Dim tex = Loader.ConvertForBitmap(ddsBytes)
        If tex Is Nothing OrElse Not tex.Loaded OrElse tex.Levels.Count = 0 Then Return Nothing

        Dim lvl = tex.Levels(0)
        Dim bmp = New Bitmap(lvl.Width, lvl.Height, Imaging.PixelFormat.Format32bppArgb)
        Dim bd = bmp.LockBits(New Rectangle(0, 0, lvl.Width, lvl.Height),
                              ImageLockMode.WriteOnly, Imaging.PixelFormat.Format32bppArgb)
        Marshal.Copy(lvl.Data, 0, bd.Scan0, lvl.Data.Length)
        bmp.UnlockBits(bd)
        For Each lvl In tex.Levels
            lvl.Data = Nothing         ' rompe la referencia al Byte()
        Next
        tex.Levels.Clear()

        Return bmp
    End Function

    ''' <summary>
    ''' Carga varios DDS y devuelve una lista de Bitmaps.
    ''' </summary>
    Public Function Load_And_CreateBitmapFromDDS(filepaths As String()) As List(Of Bitmap)
        Dim list As New List(Of Bitmap)(filepaths.Length)
        For Each p In filepaths
            list.Add(If(File.Exists(p), CreateBitmapFromDDS(File.ReadAllBytes(p)), Nothing))
        Next
        Return list
    End Function

    Const GL_UNPACK_ALIGNMENT As Integer = &HCF5
    Const GL_TEXTURE_MAX_ANISOTROPY_EXT As Integer = &H84FE
    Const GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT As Integer = &H84FF

    Const GL_TEXTURE_SWIZZLE_R As Integer = &H8E42
    Const GL_TEXTURE_SWIZZLE_G As Integer = &H8E43
    Const GL_TEXTURE_SWIZZLE_B As Integer = &H8E44
    Const GL_TEXTURE_SWIZZLE_A As Integer = &H8E45

    Const GL_ZERO As Integer = 0
    Const GL_ONE As Integer = 1
    Const GL_RED As Integer = &H1903

    Const GL_COMPRESSED_RGB_S3TC_DXT1_EXT As Integer = &H83F0
    Const GL_COMPRESSED_RGBA_S3TC_DXT1_EXT As Integer = &H83F1
    Const GL_COMPRESSED_SRGB_S3TC_DXT1_EXT As Integer = &H8C4C
    Const GL_COMPRESSED_SRGB_ALPHA_S3TC_DXT1_EXT As Integer = &H8C4D

    ' Change BC1 Alpha Preference
    Const PreferBC1Alpha As Boolean = True

    Public Function CreateOpenGL_FromTextureLoaded_PBO(tex As TextureLoaded) As Integer
        If tex Is Nothing OrElse Not tex.Loaded Then
            Return 0
        End If

        If tex.Levels Is Nothing OrElse tex.Levels.Count = 0 Then
            Return 0
        End If

        Dim target = If(tex.IsCubemap, TextureTarget.TextureCubeMap, TextureTarget.Texture2D)
        Dim texID As Integer = 0
        Dim pbo As Integer = 0
        Dim prevUnpackAlignment As Integer = 4

        Dim glInternal As Integer = CInt(tex.GlInternalFormat)
        Dim glFormat As Integer = CInt(tex.GlPixelFormat)
        Dim glType As Integer = CInt(tex.GlPixelType)

        Dim needsAlphaFromRedSwizzle As Boolean = False
        Dim needsAlphaOneSwizzle As Boolean = False

        Dim mipLevels As Integer = Math.Max(1, tex.Miplevels)
        Dim faces As Integer = Math.Max(1, tex.Faces)

        If tex.IsCubemap AndAlso faces <> 6 Then
            System.Diagnostics.Debug.WriteLine("CreateOpenGL_FromTextureLoaded_PBO: cubemap invalido, Faces=" & faces.ToString())
            Return 0
        End If

        Dim expectedImages As Integer = mipLevels * faces
        If tex.Levels.Count < expectedImages Then
            System.Diagnostics.Debug.WriteLine(
            "CreateOpenGL_FromTextureLoaded_PBO: TextureLoaded inconsistente. " &
            "Levels.Count=" & tex.Levels.Count.ToString() &
            ", Mips=" & mipLevels.ToString() &
            ", Faces=" & faces.ToString() &
            ", Esperados=" & expectedImages.ToString())
            Return 0
        End If

        ' Primera pasada: normalizacion por internal format que venga de la tabla/wrapper.
        Select Case glInternal
            Case &H8D70, &H8D76, &H8D7C, &H906F, &H8D82, &H8D88, &H8D8E
                glFormat = &H8D99 ' GL_RGBA_INTEGER

            Case &H8D71, &H8D83
                glFormat = &H8D98 ' GL_RGB_INTEGER

            Case &H823C, &H823B, &H823A, &H8239, &H8238, &H8237
                glFormat = &H8228 ' GL_RG_INTEGER

            Case &H8236, &H8235, &H8234, &H8233, &H8232, &H8231
                glFormat = &H8D94 ' GL_RED_INTEGER

            Case &H822E, &H822D, &H822A, &H8F98, &H8229, &H8F94
                glFormat = &H1903 ' GL_RED

            Case &H8230, &H822F, &H822C, &H8F99, &H822B, &H8F95
                glFormat = &H8227 ' GL_RG

            Case &H8815, &H8C3A, &H8C3D, &H8D62
                glFormat = &H1907 ' GL_RGB

            Case &H8814, &H881A, &H805B, &H8F9B, &H8058, &H8F97, &H8059, &H8C43, &H8057, &H8056
                glFormat = &H1908 ' GL_RGBA

            Case &H81A5, &H81A6, &H8CAC
                glFormat = &H1902 ' GL_DEPTH_COMPONENT

            Case &H88F0, &H8CAD
                glFormat = &H84F9 ' GL_DEPTH_STENCIL
        End Select

        Select Case glInternal
            Case &H8F97, &H8F95, &H8F94
                glType = &H1400 ' GL_BYTE

            Case &H8F9B, &H8F99, &H8F98
                glType = &H1402 ' GL_SHORT

            Case &H8D8E, &H8237, &H8231
                glType = &H1400 ' GL_BYTE

            Case &H8D7C, &H8238, &H8232
                glType = &H1401 ' GL_UNSIGNED_BYTE

            Case &H8D88, &H8239, &H8233
                glType = &H1402 ' GL_SHORT

            Case &H8D76, &H823A, &H8234
                glType = &H1403 ' GL_UNSIGNED_SHORT

            Case &H8D83, &H8D82, &H823B, &H8235
                glType = &H1404 ' GL_INT

            Case &H8D71, &H8D70, &H823C, &H8236
                glType = &H1405 ' GL_UNSIGNED_INT

            Case &H822D, &H822F, &H881A
                glType = &H140B ' GL_HALF_FLOAT

            Case &H8059
                glFormat = &H1908 ' GL_RGBA
                glType = &H8368   ' GL_UNSIGNED_INT_2_10_10_10_REV

            Case &H906F
                glFormat = &H8D99 ' GL_RGBA_INTEGER
                glType = &H8368   ' GL_UNSIGNED_INT_2_10_10_10_REV

            Case &H8C3A
                glFormat = &H1907 ' GL_RGB
                glType = &H8C3B   ' GL_UNSIGNED_INT_10F_11F_11F_REV

            Case &H8C3D
                glFormat = &H1907 ' GL_RGB
                glType = &H8C3E   ' GL_UNSIGNED_INT_5_9_9_9_REV

            Case &H88F0
                glFormat = &H84F9 ' GL_DEPTH_STENCIL
                glType = &H84FA   ' GL_UNSIGNED_INT_24_8

            Case &H8CAD
                glFormat = &H84F9 ' GL_DEPTH_STENCIL
                glType = &H8DAD   ' GL_FLOAT_32_UNSIGNED_INT_24_8_REV
        End Select

        ' Segunda pasada: DXGI final manda. Aca corregimos formatos especiales aunque la tabla venga mal.
        Select Case tex.DxgiCodeFinal
            Case 65 ' A8_UNORM
                glInternal = &H8229 ' GL_R8
                glFormat = &H1903   ' GL_RED
                glType = &H1401     ' GL_UNSIGNED_BYTE
                needsAlphaFromRedSwizzle = True

            Case 71 ' BC1_UNORM
                glInternal = If(PreferBC1Alpha, GL_COMPRESSED_RGBA_S3TC_DXT1_EXT, GL_COMPRESSED_RGB_S3TC_DXT1_EXT)

            Case 72 ' BC1_UNORM_SRGB
                glInternal = If(PreferBC1Alpha, GL_COMPRESSED_SRGB_ALPHA_S3TC_DXT1_EXT, GL_COMPRESSED_SRGB_S3TC_DXT1_EXT)

            Case 74 ' BC2_UNORM
                glInternal = &H83F2 ' GL_COMPRESSED_RGBA_S3TC_DXT3_EXT

            Case 75 ' BC2_UNORM_SRGB
                glInternal = &H8C4E ' GL_COMPRESSED_SRGB_ALPHA_S3TC_DXT3_EXT

            Case 77 ' BC3_UNORM
                glInternal = &H83F3 ' GL_COMPRESSED_RGBA_S3TC_DXT5_EXT

            Case 78 ' BC3_UNORM_SRGB
                glInternal = &H8C4F ' GL_COMPRESSED_SRGB_ALPHA_S3TC_DXT5_EXT

            Case 80 ' BC4_UNORM
                glInternal = &H8DBB ' GL_COMPRESSED_RED_RGTC1

            Case 81 ' BC4_SNORM
                glInternal = &H8DBC ' GL_COMPRESSED_SIGNED_RED_RGTC1

            Case 83 ' BC5_UNORM
                glInternal = &H8DBD ' GL_COMPRESSED_RG_RGTC2

            Case 84 ' BC5_SNORM
                glInternal = &H8DBE ' GL_COMPRESSED_SIGNED_RG_RGTC2

            Case 85 ' B5G6R5_UNORM
                glInternal = &H8D62 ' GL_RGB565
                glFormat = &H1907   ' GL_RGB
                glType = &H8364     ' GL_UNSIGNED_SHORT_5_6_5_REV

            Case 86 ' B5G5R5A1_UNORM
                glInternal = &H8057 ' GL_RGB5_A1
                glFormat = &H80E1   ' GL_BGRA
                glType = &H8366     ' GL_UNSIGNED_SHORT_1_5_5_5_REV

            Case 87 ' B8G8R8A8_UNORM
                glInternal = &H8058 ' GL_RGBA8
                glFormat = &H80E1   ' GL_BGRA
                glType = &H1401     ' GL_UNSIGNED_BYTE

            Case 88 ' B8G8R8X8_UNORM
                glInternal = &H8058 ' GL_RGBA8
                glFormat = &H80E1   ' GL_BGRA
                glType = &H1401     ' GL_UNSIGNED_BYTE
                needsAlphaOneSwizzle = True

            Case 91 ' B8G8R8A8_UNORM_SRGB
                glInternal = &H8C43 ' GL_SRGB8_ALPHA8
                glFormat = &H80E1   ' GL_BGRA
                glType = &H1401     ' GL_UNSIGNED_BYTE

            Case 93 ' B8G8R8X8_UNORM_SRGB
                glInternal = &H8C43 ' GL_SRGB8_ALPHA8
                glFormat = &H80E1   ' GL_BGRA
                glType = &H1401     ' GL_UNSIGNED_BYTE
                needsAlphaOneSwizzle = True

            Case 95 ' BC6H_UF16
                glInternal = &H8E8F ' GL_COMPRESSED_RGB_BPTC_UNSIGNED_FLOAT

            Case 96 ' BC6H_SF16
                glInternal = &H8E8E ' GL_COMPRESSED_RGB_BPTC_SIGNED_FLOAT

            Case 98 ' BC7_UNORM
                glInternal = &H8E8C ' GL_COMPRESSED_RGBA_BPTC_UNORM

            Case 99 ' BC7_UNORM_SRGB
                glInternal = &H8E8D ' GL_COMPRESSED_SRGB_ALPHA_BPTC_UNORM

            Case 115 ' B4G4R4A4_UNORM
                glInternal = &H8056 ' GL_RGBA4
                glFormat = &H80E1   ' GL_BGRA
                glType = &H8365     ' GL_UNSIGNED_SHORT_4_4_4_4_REV

            Case 191 ' A4B4G4R4_UNORM
                glInternal = &H8056 ' GL_RGBA4
                glFormat = &H1908   ' GL_RGBA
                glType = &H8365     ' GL_UNSIGNED_SHORT_4_4_4_4_REV
        End Select

        If glInternal = 0 Then
            System.Diagnostics.Debug.WriteLine(
            "CreateOpenGL_FromTextureLoaded_PBO: formato GL incompatible. " &
            "DXGI original=" & tex.DxgiCodeOriginal.ToString() &
            ", DXGI final=" & tex.DxgiCodeFinal.ToString())
            Return 0
        End If

        If Not tex.IsCompressedGL AndAlso (glFormat = 0 OrElse glType = 0) Then
            System.Diagnostics.Debug.WriteLine(
            "CreateOpenGL_FromTextureLoaded_PBO: glFormat/glType invalidos. " &
            "DXGI final=" & tex.DxgiCodeFinal.ToString() &
            ", glInternal=0x" & Hex(glInternal) &
            ", glFormat=0x" & Hex(glFormat) &
            ", glType=0x" & Hex(glType))
            Return 0
        End If

        Dim baseW As Integer = tex.Levels(0).Width
        Dim baseH As Integer = tex.Levels(0).Height
        If baseW <= 0 OrElse baseH <= 0 Then
            System.Diagnostics.Debug.WriteLine("CreateOpenGL_FromTextureLoaded_PBO: dimensiones invalidas en nivel 0.")
            Return 0
        End If

        Try
            texID = GL.GenTexture()
            GL.BindTexture(target, texID)

            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipLevels - 1)

            Dim isIntegerUpload As Boolean =
            (glFormat = &H8D94) OrElse ' GL_RED_INTEGER
            (glFormat = &H8228) OrElse ' GL_RG_INTEGER
            (glFormat = &H8D98) OrElse ' GL_RGB_INTEGER
            (glFormat = &H8D99)        ' GL_RGBA_INTEGER

            Dim isDepthStencilUpload As Boolean = (glFormat = &H84F9) ' GL_DEPTH_STENCIL
            Dim useNearest As Boolean = isIntegerUpload OrElse isDepthStencilUpload

            If useNearest Then
                Dim minFilter = If(mipLevels > 1, TextureMinFilter.NearestMipmapNearest, TextureMinFilter.Nearest)
                GL.TexParameter(target, TextureParameterName.TextureMinFilter, CInt(minFilter))
                GL.TexParameter(target, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
            Else
                Dim minFilter = If(mipLevels > 1, TextureMinFilter.LinearMipmapLinear, TextureMinFilter.Linear)
                GL.TexParameter(target, TextureParameterName.TextureMinFilter, CInt(minFilter))
                GL.TexParameter(target, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))

                ' Si quieres volver al comportamiento anterior, re-agrega:
                ' GL.TexParameter(target, TextureParameterName.TextureLodBias, -0.5F)

                Dim maxAniso As Single = 0
                GL.GetFloat(CType(GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT, GetPName), maxAniso)
                If maxAniso >= 1.0F Then
                    GL.TexParameter(target, CType(GL_TEXTURE_MAX_ANISOTROPY_EXT, TextureParameterName), maxAniso)
                End If
            End If

            If tex.IsCubemap Then
                GL.TexParameter(target, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                GL.TexParameter(target, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                GL.TexParameter(target, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))
            Else
                GL.TexParameter(target, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
                GL.TexParameter(target, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
            End If

            GL.GetInteger(CType(GL_UNPACK_ALIGNMENT, GetPName), prevUnpackAlignment)
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)

            GL.TexStorage2D(
            target,
            mipLevels,
            CType(glInternal, SizedInternalFormat),
            baseW,
            baseH)

            ' Contrato asumido del wrapper:
            ' tex.Levels esta ordenado mip-major:
            ' mip0-face0, mip0-face1, ..., mip1-face0, mip1-face1, ...
            Dim totalBytes As Integer = 0
            Dim offsets(expectedImages - 1) As Integer

            For m As Integer = 0 To mipLevels - 1
                For f As Integer = 0 To faces - 1
                    Dim idx = m * faces + f
                    Dim lvl = tex.Levels(idx)

                    If lvl Is Nothing OrElse lvl.Data Is Nothing Then
                        Throw New InvalidOperationException("Nivel de textura invalido en idx=" & idx.ToString())
                    End If

                    offsets(idx) = totalBytes
                    totalBytes += lvl.Data.Length
                Next
            Next

            pbo = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
            GL.BufferData(BufferTarget.PixelUnpackBuffer, totalBytes, IntPtr.Zero, BufferUsageHint.StreamDraw)

            Dim basePtr = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly)
            If basePtr = IntPtr.Zero Then
                Throw New InvalidOperationException("GL.MapBuffer devolvio IntPtr.Zero para PixelUnpackBuffer.")
            End If

            For i As Integer = 0 To expectedImages - 1
                Dim lvl = tex.Levels(i)
                Marshal.Copy(lvl.Data, 0, IntPtr.Add(basePtr, offsets(i)), lvl.Data.Length)
            Next

            If GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) = False Then
                Throw New InvalidOperationException("GL.UnmapBuffer devolvio False para PixelUnpackBuffer.")
            End If

            Dim faceTargets() As TextureTarget = {
            TextureTarget.TextureCubeMapPositiveX, TextureTarget.TextureCubeMapNegativeX,
            TextureTarget.TextureCubeMapPositiveY, TextureTarget.TextureCubeMapNegativeY,
            TextureTarget.TextureCubeMapPositiveZ, TextureTarget.TextureCubeMapNegativeZ
        }

            For m As Integer = 0 To mipLevels - 1
                For f As Integer = 0 To faces - 1
                    Dim idx = m * faces + f
                    Dim lvl = tex.Levels(idx)
                    Dim subTarget = If(tex.IsCubemap, faceTargets(f), TextureTarget.Texture2D)
                    Dim offsetPtr = New IntPtr(offsets(idx))

                    If tex.IsCompressedGL Then
                        GL.CompressedTexSubImage2D(
                        subTarget,
                        m, 0, 0,
                        lvl.Width, lvl.Height,
                        CType(glInternal, InternalFormat),
                        lvl.Data.Length,
                        offsetPtr)
                    Else
                        GL.TexSubImage2D(
                        subTarget,
                        m, 0, 0,
                        lvl.Width, lvl.Height,
                        CType(glFormat, OpenTK.Graphics.OpenGL4.PixelFormat),
                        CType(glType, PixelType),
                        offsetPtr)
                    End If
                Next
            Next

            If needsAlphaFromRedSwizzle Then
                GL.TexParameter(target, CType(GL_TEXTURE_SWIZZLE_R, TextureParameterName), CInt(GL_ZERO))
                GL.TexParameter(target, CType(GL_TEXTURE_SWIZZLE_G, TextureParameterName), CInt(GL_ZERO))
                GL.TexParameter(target, CType(GL_TEXTURE_SWIZZLE_B, TextureParameterName), CInt(GL_ZERO))
                GL.TexParameter(target, CType(GL_TEXTURE_SWIZZLE_A, TextureParameterName), CInt(GL_RED))
            ElseIf needsAlphaOneSwizzle Then
                GL.TexParameter(target, CType(GL_TEXTURE_SWIZZLE_A, TextureParameterName), CInt(GL_ONE))
            End If

            Return texID

        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine(
            "CreateOpenGL_FromTextureLoaded_PBO failed. " &
            "DXGI original=" & tex.DxgiCodeOriginal.ToString() &
            ", DXGI final=" & tex.DxgiCodeFinal.ToString() &
            ", glInternal=0x" & Hex(glInternal) &
            ", glFormat=0x" & Hex(glFormat) &
            ", glType=0x" & Hex(glType) &
            Environment.NewLine &
            ex.ToString())

            If texID <> 0 Then
                GL.DeleteTexture(texID)
            End If

        Finally
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)

            If pbo <> 0 Then
                GL.DeleteBuffer(pbo)
            End If

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, prevUnpackAlignment)
            GL.BindTexture(target, 0)
        End Try

        Return 0
    End Function


    Public Function Load_And_GenerateOpenGLTextures_FromFiles(fullpaths As String(), useCompress As Boolean, forceOpenGL As Boolean) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim ddsFiles As Byte()() = fullpaths.Select(Function(p)
                                                        If File.Exists(p) Then
                                                            Return File.ReadAllBytes(p)
                                                        Else
                                                            Return Array.Empty(Of Byte)()
                                                        End If
                                                    End Function).ToArray()

        Return Load_And_GenerateOpenGLTextures_Memory(fullpaths, ddsFiles, useCompress, forceOpenGL)
    End Function

    ''' <summary>
    ''' Carga DDS, genera IDs OpenGL y llena Diccionario con metadatos completos.
    ''' </summary>
    Public Function Load_And_GenerateOpenGLTextures_FromDictionary(fullpaths As String(), useCompress As Boolean, forceOpenGL As Boolean) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim ddsFiles As Byte()()
        Dim result As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        If fullpaths.Length = 1 Then
            ddsFiles = {FilesDictionary_class.GetBytes(fullpaths(0))}
            result = Load_And_GenerateOpenGLTextures_Memory(fullpaths, ddsFiles, useCompress, forceOpenGL)
        Else
            ddsFiles = FilesDictionary_class.GetMultipleFilesBytes(fullpaths)
            result = Load_And_GenerateOpenGLTextures_Memory(fullpaths, ddsFiles, useCompress, forceOpenGL)
        End If

        If result.Count <> fullpaths.Length Then Debugger.Break() : Throw New Exception("el loader no esta devolviendo la misma cantidad que las enviadas")
        Return result
    End Function

    ''' <summary>
    ''' O4.1 Phase 1 — Background DDS loading (CPU-only, no GL calls).
    ''' Loads DDS bytes from the files dictionary and decompresses them via DirectXTex.
    ''' Returns a dictionary mapping each path to its decompressed TextureLoaded data,
    ''' ready for GL upload on the render thread.
    ''' Thread-safe: can be called from any thread. Supports cancellation.
    ''' </summary>
    Public Function LoadTexturesFromDictionary_Background(
            fullpaths As String(),
            useCompress As Boolean,
            forceOpenGL As Boolean,
            ct As System.Threading.CancellationToken) As Dictionary(Of String, DirectXTexWrapperCLI.TextureLoaded)

        Dim dict As New Dictionary(Of String, DirectXTexWrapperCLI.TextureLoaded)(
            fullpaths.Length, StringComparer.OrdinalIgnoreCase)

        ' 1) Fetch raw DDS bytes from the files dictionary (I/O, may hit archive cache)
        Dim ddsFiles As Byte()()
        If fullpaths.Length = 1 Then
            ddsFiles = {FilesDictionary_class.GetBytes(fullpaths(0))}
        Else
            ddsFiles = FilesDictionary_class.GetMultipleFilesBytes(fullpaths)
        End If

        ct.ThrowIfCancellationRequested()

        ' 2) Decompress all DDS textures (CPU-heavy, no GL)
        Dim results As System.Collections.Generic.List(Of DirectXTexWrapperCLI.TextureLoaded)
        Try
            results = Loader.LoadTextures(ddsFiles.ToArray(), useCompress, forceOpenGL)
        Catch ex As Exception
            ' If decompression fails entirely, return empty entries so callers keep fallbacks
            For Each p In fullpaths
                dict(p) = Nothing
            Next
            Return dict
        End Try

        ct.ThrowIfCancellationRequested()

        ' 3) Map paths to their TextureLoaded results
        For i As Integer = 0 To Math.Min(fullpaths.Length, results.Count) - 1
            dict(fullpaths(i)) = results(i)
        Next

        ' Fill any missing entries with Nothing (in case results.Count < fullpaths.Length)
        For i As Integer = results.Count To fullpaths.Length - 1
            dict(fullpaths(i)) = Nothing
        Next

        Return dict
    End Function

    ''' <summary>
    ''' O4.1 Phase 2 — Upload a single decompressed TextureLoaded to OpenGL via PBO.
    ''' MUST be called on the GL context thread.
    ''' Returns (glTextureId, textureSize, isCubemap, dxgiOriginal, dxgiFinal, loaded).
    ''' On failure returns a Texture_Loaded_Class with Texture_ID = 0.
    ''' After upload, nulls out the TextureLoaded.Levels data to free memory.
    ''' </summary>
    Public Function UploadTextureToGL(tex As DirectXTexWrapperCLI.TextureLoaded, path As String) As PreviewModel.Texture_Loaded_Class
        If tex Is Nothing OrElse Not tex.Loaded Then
            Return New PreviewModel.Texture_Loaded_Class With {
                .Texture_ID = 0,
                .Size = New Size(2, 2),
                .Cubemap = If(tex IsNot Nothing, tex.IsCubemap, False),
                .DGXFormat_Original = If(tex IsNot Nothing, tex.DxgiCodeOriginal, 0),
                .DGXFormat_Final = If(tex IsNot Nothing, tex.DxgiCodeFinal, 0),
                .Loaded = False,
                .Path = path
            }
        End If

        Dim id As Integer = CreateOpenGL_FromTextureLoaded_PBO(tex)
        Dim lvl0Size As Size
        If tex.Levels IsNot Nothing AndAlso tex.Levels.Count > 0 Then
            lvl0Size = New Size(tex.Levels(0).Width, tex.Levels(0).Height)
        Else
            lvl0Size = New Size(2, 2)
        End If

        Dim result As New PreviewModel.Texture_Loaded_Class With {
            .Texture_ID = id,
            .Size = lvl0Size,
            .Cubemap = tex.IsCubemap,
            .DGXFormat_Original = tex.DxgiCodeOriginal,
            .DGXFormat_Final = tex.DxgiCodeFinal,
            .Loaded = (id > 0),
            .Path = path
        }

        ' Free pixel data now that it has been uploaded to GPU
        If tex.Levels IsNot Nothing Then
            For Each lvl In tex.Levels
                lvl.Data = Nothing
            Next
            tex.Levels.Clear()
        End If

        Return result
    End Function

    Public Function Load_And_GenerateOpenGLTextures_Memory(fullpaths As String(), ddsFiles As Byte()(), useCompress As Boolean, forceOpenGL As Boolean) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim diccionario As New Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim results = Loader.LoadTextures(ddsFiles.ToArray, useCompress, forceOpenGL)

        For i As Integer = 0 To results.Count - 1
            Dim tex = results(i)
            If tex.Loaded = False Then
                diccionario(fullpaths(i)) = New PreviewModel.Texture_Loaded_Class With {
                    .Texture_ID = 0,
                    .Size = New Size(2, 2),
                    .Cubemap = tex.IsCubemap,
                    .DGXFormat_Original = tex.DxgiCodeOriginal,
                    .DGXFormat_Final = tex.DxgiCodeFinal,
                    .Loaded = tex.Loaded,
                    .Path = fullpaths(i)
                    }
            Else
                Dim id = CreateOpenGL_FromTextureLoaded_PBO(tex)
                Dim lvl0 = tex.Levels(0)
                diccionario(fullpaths(i)) = New PreviewModel.Texture_Loaded_Class With {
                    .Texture_ID = id,
                    .Size = New Size(lvl0.Width, lvl0.Height),
                    .Cubemap = tex.IsCubemap,
                    .DGXFormat_Original = tex.DxgiCodeOriginal,
                    .DGXFormat_Final = tex.DxgiCodeFinal,
                    .Loaded = tex.Loaded,
                    .Path = fullpaths(i)
                    }
            End If

        Next
        results.Clear()
        Return diccionario
    End Function

End Module








