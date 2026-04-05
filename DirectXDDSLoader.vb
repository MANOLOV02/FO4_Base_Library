' Version Uploaded of Wardrobe 3.1.0
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

    Public Function CreateOpenGL_FromTextureLoaded_PBO(tex As TextureLoaded) As Integer
        If tex Is Nothing OrElse Not tex.Loaded Then
            Return 0
        End If

        Dim target = If(tex.IsCubemap, TextureTarget.TextureCubeMap, TextureTarget.Texture2D)
        Dim texID As Integer = 0
        Dim pbo As Integer = 0

        Try
            ' 1) Generar y bindear textura
            texID = GL.GenTexture()
            GL.BindTexture(target, texID)

            ' 2) Parámetros de miplevels
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, tex.Miplevels - 1)

            ' 3) Filtros y LOD bias
            Dim minFilter = If(tex.Miplevels > 1, TextureMinFilter.LinearMipmapLinear, TextureMinFilter.Linear)
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, CInt(minFilter))
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(target, TextureParameterName.TextureLodBias, -0.5F)

            ' 4) Wrapping
            If tex.IsCubemap Then
                GL.TexParameter(target, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                GL.TexParameter(target, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                GL.TexParameter(target, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))
            Else
                GL.TexParameter(target, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
                GL.TexParameter(target, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
            End If

            ' 5) Filtrado anisotrópico
            Dim maxAniso As Single
            GL.GetFloat(GetPName.MaxTextureMaxAnisotropy, maxAniso)
            GL.TexParameter(target, CType(&H84FE, TextureParameterName), maxAniso)

            ' 6) Reservar almacenamiento
            Dim baseW = tex.Levels(0).Width
            Dim baseH = tex.Levels(0).Height
            GL.TexStorage2D(
            target,
            tex.Miplevels,
            CType(tex.GlInternalFormat, SizedInternalFormat),
            baseW,
            baseH)

            ' 7) Calcular offsets y tamaño total
            Dim faces = tex.Faces
            Dim levels = tex.Miplevels
            Dim totalBytes As Integer = 0
            Dim offsets(levels * faces - 1) As Integer

            For m As Integer = 0 To levels - 1
                For f As Integer = 0 To faces - 1
                    Dim idx = m * faces + f
                    offsets(idx) = totalBytes
                    totalBytes += tex.Levels(idx).Data.Length
                Next
            Next

            ' 8) Crear y bindear PBO
            pbo = GL.GenBuffer()
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo)
            GL.BufferData(BufferTarget.PixelUnpackBuffer, totalBytes, IntPtr.Zero, BufferUsageHint.StreamDraw)

            ' 9) Mapear PBO y copiar datos
            Dim basePtr = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly)
            If basePtr = IntPtr.Zero Then
                Throw New InvalidOperationException("GL.MapBuffer devolvió IntPtr.Zero para PixelUnpackBuffer.")
            End If

            For i As Integer = 0 To offsets.Length - 1
                Dim lvl = tex.Levels(i)
                Marshal.Copy(lvl.Data, 0, IntPtr.Add(basePtr, offsets(i)), lvl.Data.Length)
            Next

            If GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer) = False Then
                Throw New InvalidOperationException("GL.UnmapBuffer devolvió False para PixelUnpackBuffer.")
            End If

            ' 10) Targets de cubemap
            Dim faceTargets() As TextureTarget = {
            TextureTarget.TextureCubeMapPositiveX, TextureTarget.TextureCubeMapNegativeX,
            TextureTarget.TextureCubeMapPositiveY, TextureTarget.TextureCubeMapNegativeY,
            TextureTarget.TextureCubeMapPositiveZ, TextureTarget.TextureCubeMapNegativeZ
        }

            ' 11) Subir niveles/caras desde PBO
            For m As Integer = 0 To levels - 1
                For f As Integer = 0 To faces - 1
                    Dim idx = m * faces + f
                    Dim lvl = tex.Levels(idx)
                    Dim offsetPtr = New IntPtr(offsets(idx))

                    If tex.IsCompressedGL Then
                        GL.CompressedTexSubImage2D(
                        If(tex.IsCubemap, faceTargets(f), TextureTarget.Texture2D),
                        m, 0, 0,
                        lvl.Width, lvl.Height,
                        CType(tex.GlInternalFormat, InternalFormat),
                        lvl.Data.Length,
                        offsetPtr)
                    Else
                        GL.TexSubImage2D(
                        If(tex.IsCubemap, faceTargets(f), TextureTarget.Texture2D),
                        m, 0, 0,
                        lvl.Width, lvl.Height,
                        CType(tex.GlPixelFormat, OpenTK.Graphics.OpenGL4.PixelFormat),
                        CType(tex.GlPixelType, PixelType),
                        offsetPtr)
                    End If
                Next
            Next

            ' 13) (Opcional) Generar mipmaps en GPU si decides no subir todos manualmente
            ' GL.GenerateTextureMipmap(texID)

            Return texID

        Catch
            If texID <> 0 Then
                GL.DeleteTexture(texID)
            End If
        Finally
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)
            If pbo <> 0 Then
                GL.DeleteBuffer(pbo)
            End If
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








