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
        Try
            ' El destino es memoria NATIVA de GDI+: copiar sin comparar contra su tamaño real no da una
            ' excepción, da corrupción de heap. `Marshal.Copy` sólo mira el array de origen.
            Dim capacidad As Long = CLng(Math.Abs(bd.Stride)) * bd.Height
            If lvl.Data.Length > capacidad Then
                Throw New InvalidDataException(
                    $"El nivel 0 trae {lvl.Data.Length} bytes y el bitmap GDI+ sólo admite {capacidad}.")
            End If
            Marshal.Copy(lvl.Data, 0, bd.Scan0, lvl.Data.Length)
        Finally
            ' Sin este Finally, una excepción en el copy dejaba el bitmap BLOQUEADO y sus bytes nativos
            ' —67 MB en un 4096²— colgados hasta el finalizador, fuera del heap administrado.
            bmp.UnlockBits(bd)
        End Try
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

    ''' <summary>LA ÚNICA LEY DE SAMPLEO de las texturas del render: niveles, filtros, anisotropía y wrap.
    ''' <para>SYNC — NO TRANSCRIBIRLA EN OTRO LADO. La consumen DOS caminos: el upload del DDS (acá abajo) y
    ''' la instalación de las texturas COMPUESTAS del pliegue de SSE (<c>NpcFaceTintResolver.InstallTexture</c>),
    ''' que reemplazan a un DDS en el bind y por lo tanto tienen que samplearse IGUAL que el DDS al que
    ''' reemplazan. Estaba escrita sólo acá y la textura del pliegue se subía con <c>MinFilter=Linear</c> y un
    ''' solo nivel: era la única del render sin minificación. Copiarla en el otro sitio habría dejado dos
    ''' transcripciones que se separan en el primer cambio (LodBias, wrap, el gate de la anisotropía).</para>
    ''' <para><paramref name="mipLevels"/> = niveles REALES que tiene la textura (1 = sin cadena). El llamador
    ''' que compone en GL tiene que haber corrido <c>GenerateMipmap</c> ANTES y pasar los niveles que generó,
    ''' o el filtro con mips leería niveles que no existen.</para></summary>
    Public Sub ApplySamplingState(target As TextureTarget, mipLevels As Integer,
                                  useNearest As Boolean, isCubemap As Boolean)
        GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0)
        GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mipLevels - 1)

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

            ' Sin la extensión de anisotropía este GetFloat deja `maxAniso` en 0 y encola un
            ' GL_INVALID_ENUM. El gate `>= 1` hace que no se aplique nada, y el drenaje evita que ese error
            ' quede en la cola y lo cobre un chequeo posterior ajeno.
            Dim maxAniso As Single = 0
            GL.GetFloat(CType(GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT, GetPName), maxAniso)
            If maxAniso >= 1.0F Then
                GL.TexParameter(target, CType(GL_TEXTURE_MAX_ANISOTROPY_EXT, TextureParameterName), maxAniso)
            Else
                DrainGlErrors()
            End If
        End If

        If isCubemap Then
            GL.TexParameter(target, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(target, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(target, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))
        Else
            GL.TexParameter(target, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
            GL.TexParameter(target, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
        End If
    End Sub

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

    ''' <summary>Drain any pending GL error so subsequent CheckGlOk calls only report errors
    ''' attributable to this upload. Caller MUST hold the GL context current.</summary>
    Private Sub DrainGlErrors()
        Dim guard As Integer = 0
        Do While GL.GetError() <> ErrorCode.NoError
            guard += 1
            If guard > 32 Then Exit Do
        Loop
    End Sub

    ''' <summary>Returns True iff GL has no error after the last operation.
    ''' Use after each upload op to certify success.</summary>
    Private Function CheckGlOk(opLabel As String) As Boolean
        Dim e = GL.GetError()
        If e = ErrorCode.NoError Then Return True
        Logger.LogLazy(Function() $"[DDS] GL error after {opLabel}: 0x{Hex(CInt(e))} ({e})")
        Return False
    End Function

    Public Function CreateOpenGL_FromTextureLoaded_PBO(tex As TextureLoaded, srgb As Boolean) As Integer
        If tex Is Nothing OrElse Not tex.Loaded Then
            Return 0
        End If

        If tex.Levels Is Nothing OrElse tex.Levels.Count = 0 Then
            Return 0
        End If

        Dim target = If(tex.IsCubemap, TextureTarget.TextureCubeMap, TextureTarget.Texture2D)
        Dim texID As Integer = 0
        ' PBO que hubiera bindeado al entrar. Se LEE adentro del Try (ver la nota del bindeo, más abajo), no
        ' acá: sólo lo consume el Finally, y el Finally sólo corre si se entró al Try.
        Dim prevPixelUnpackBuffer As Integer = 0
        ' Binding de textura que hubiera al entrar. Mismo criterio que el PBO: se lee adentro del Try, que es
        ' donde vive el unico consumidor (el Finally).
        Dim prevTextureBinding As Integer = 0
        ' El alineamiento previo se lee ACÁ, ANTES del Try. Estaba leído adentro, así que cualquier
        ' excepción anterior a esa línea hacía que el Finally "restaurara" el literal 4 de la
        ' inicialización — y 4 NO es lo que hay: `CreateColorTexture` deja el UNPACK_ALIGNMENT global en 1
        ' y no lo devuelve. O sea que el camino de error cambiaba estado global del contexto en silencio.
        ' Y va con su propio Try: al sacarla del Try grande tambien la saqué del `Catch` que convertía
        ' TODA esta función en `Return 0`. Sin esto, una excepción de GL acá escapa a `UploadTextureToGL` y
        ' a `Load_And_GenerateOpenGLTextures_Memory`, que NO la atrapan — o sea, ensanchar el radio para
        ' arreglar un alineamiento. Si no se puede leer, 4 (el default de GL) es la respuesta correcta.
        Dim prevUnpackAlignment As Integer = 4
        Try
            GL.GetInteger(CType(GL_UNPACK_ALIGNMENT, GetPName), prevUnpackAlignment)
        Catch
            prevUnpackAlignment = 4
        End Try

        Dim glInternal As Integer = CInt(tex.GlInternalFormat)
        Dim glFormat As Integer = CInt(tex.GlPixelFormat)
        Dim glType As Integer = CInt(tex.GlPixelType)

        Dim needsAlphaFromRedSwizzle As Boolean = False
        Dim needsAlphaOneSwizzle As Boolean = False

        Dim mipLevels As Integer = Math.Max(1, tex.Miplevels)
        Dim faces As Integer = Math.Max(1, tex.Faces)

        If tex.IsCubemap AndAlso faces <> 6 Then
            Return 0
        End If

        ' UN Texture2DArray SUBIA LA SLICE EQUIVOCADA, EN SILENCIO. `Faces` lo llena el wrapper con el
        ' `arraySize` CRUDO, no con "6 si es cubemap si no 1": para un DDS 2D-array vale N. Sin cubemap, el
        ' doble loop de abajo usa `TextureTarget.Texture2D` para las N slices del MISMO mip, asi que cada
        ' `TexSubImage2D` pisa a la anterior — sin error de GL, sin log — y la textura terminaba con la
        ' ULTIMA slice y `Loaded = True`. Bytes equivocados en silencio, que es el peor resultado posible.
        ' Subirlo bien pide GL_TEXTURE_2D_ARRAY (TexStorage3D/TexSubImage3D) y un sampler distinto en los
        ' shaders; ningun material de FO4/SSE trae uno, asi que la respuesta correcta es NO CARGARLO y
        ' dejar rastro, no adivinar cual de las N slices queria el llamador.
        ' ESTE LOG NO SE VE EN RELEASE: `Logger.Enabled` esta forzado a False y su setter descarta
        ' cualquier True (ver Logger.vb). O sea que este guard es una RED MUDA. El canal visible para el
        ' mismo defecto esta del lado CPU, que es ademas el que ESCRIBE los bytes en disco: ver el guard
        ' gemelo en FaceTintCpuCompositor.DecodeDds, que devuelve Nothing y sube como fallo de textura.
        If Not tex.IsCubemap AndAlso faces <> 1 Then
            Dim nSlices = faces
            Logger.LogLazy(Function() $"[DDS] Texture2DArray no soportado: arraySize={nSlices} sin cubemap. No se carga (antes se subia la ultima slice como si fuera la textura).")
            Return 0
        End If

        Dim expectedImages As Integer = mipLevels * faces
        If tex.Levels.Count < expectedImages Then
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

        ' sRGB decode AT LOAD, exactly like the engine: the renderer carries a per-texture sRGB flag and,
        ' for color textures, MakeSRGB (Fallout4.exe FUN_14183e1c0) rewrites the texture format UNORM->_SRGB
        ' BEFORE the SRV is created, so the GPU gamma-decodes on sample (that is why the engine shaders never
        ' pow() the diffuse). The previewer mirrors it here. 'srgb' is True only for COLOR textures (diffuse /
        ' base color), passed by the caller from the material's texture role. We upgrade the chosen UNORM GL
        ' format to its sRGB variant. Formats already sRGB (DDS authored _SRGB, e.g. BC7_UNORM_SRGB mods) were
        ' mapped to an sRGB GL format above and DO NOT appear here -> idempotent, NO double decode. Data formats
        ' (BC5/BC4/single-channel) have no sRGB variant and fall through untouched.
        If srgb Then
            Select Case glInternal
                Case &H83F0 : glInternal = GL_COMPRESSED_SRGB_S3TC_DXT1_EXT          ' BC1 RGB  -> sRGB
                Case &H83F1 : glInternal = GL_COMPRESSED_SRGB_ALPHA_S3TC_DXT1_EXT    ' BC1 RGBA -> sRGB
                Case &H83F2 : glInternal = &H8C4E                                    ' BC2 (DXT3) -> SRGB_ALPHA
                Case &H83F3 : glInternal = &H8C4F                                    ' BC3 (DXT5) -> SRGB_ALPHA
                Case &H8E8C : glInternal = &H8E8D                                    ' BC7_UNORM -> BC7_SRGB
                Case &H8058 : glInternal = &H8C43                                    ' RGBA8 -> SRGB8_ALPHA8
            End Select
        End If

        If glInternal = 0 Then
            Return 0
        End If

        If Not tex.IsCompressedGL AndAlso (glFormat = 0 OrElse glType = 0) Then
            Return 0
        End If

        Dim baseW As Integer = tex.Levels(0).Width
        Dim baseH As Integer = tex.Levels(0).Height
        If baseW <= 0 OrElse baseH <= 0 Then
            Return 0
        End If

        Try
            ' Drain any pre-existing GL error so the post-upload check below only flags
            ' errors caused by THIS upload, not leftovers from another caller.
            DrainGlErrors()

            ' El binding previo se lee ACA, antes de generar y bindear el nuestro, y con el pname TIPADO.
            ' MEDIDO por reflexion sobre OpenTK.Graphics 4.9.3 (el ensamblado que resuelve este proyecto):
            ' GetPName.TextureBinding2D = 0x8069, GetPName.TextureBindingCubeMap = 0x8514. Un
            ' `CType(numero, GetPName)` con el valor equivocado COMPILA —los enums del CLR no validan rango— y
            ' `glGetIntegerv` con pname invalido NO escribe el destino: es exactamente como se colo antes un
            ' `&H8CA8` que en realidad era GL_READ_FRAMEBUFFER. Mismo patron tipado que usa
            ' FaceTintCompositor.SaveGlState.
            ' ESTA FUNCION NO TOCA `ActiveTexture` EN NINGUN LADO, Y DE AHI SALE LA CORRECCION:
            ' glGetIntegerv(TEXTURE_BINDING_*) reporta el binding de la unidad ACTIVA, asi que la lectura y la
            ' restauracion caen sobre la MISMA unidad por construccion. Si alguien agrega un ActiveTexture aca
            ' adentro, este par se desaparea en silencio (lee en la unidad N, restaura en la 0).
            Try
                GL.GetInteger(If(tex.IsCubemap, GetPName.TextureBindingCubeMap, GetPName.TextureBinding2D), prevTextureBinding)
            Catch
                prevTextureBinding = 0
            End Try

            texID = GL.GenTexture()
            GL.BindTexture(target, texID)

            Dim isIntegerUpload As Boolean =
            (glFormat = &H8D94) OrElse ' GL_RED_INTEGER
            (glFormat = &H8228) OrElse ' GL_RG_INTEGER
            (glFormat = &H8D98) OrElse ' GL_RGB_INTEGER
            (glFormat = &H8D99)        ' GL_RGBA_INTEGER

            Dim isDepthStencilUpload As Boolean = (glFormat = &H84F9) ' GL_DEPTH_STENCIL
            Dim useNearest As Boolean = isIntegerUpload OrElse isDepthStencilUpload

            ApplySamplingState(target, mipLevels, useNearest, tex.IsCubemap)

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
            For m As Integer = 0 To mipLevels - 1
                For f As Integer = 0 To faces - 1
                    Dim idx = m * faces + f
                    Dim lvl = tex.Levels(idx)
                    If lvl Is Nothing OrElse lvl.Data Is Nothing Then
                        Throw New InvalidOperationException("Invalid texture level at idx=" & idx.ToString())
                    End If
                Next
            Next

            ' ACA VIVIA UN PBO Y NO COMPRABA NADA.
            ' El sentido de un Pixel Buffer Object es la transferencia ASINCRONA: se llena, se sigue
            ' trabajando, y el driver la consume cuando puede. Este se creaba, se llenaba, se consumia y se
            ' borraba DENTRO DE LA MISMA LLAMADA, sin fence y sin reuso entre texturas. El driver tenia que
            ' terminar la transferencia igual, y encima costaba:
            '   1. un `Marshal.Copy` del contenido ENTERO de la textura (administrado -> PBO), y
            '   2. un `BufferData(totalBytes)` que reserva y libera esa misma cantidad de memoria DEL
            '      DRIVER por textura — ~22 MB por un diffuse 4096² BC7 con mips.
            ' Subiendo directo desde `lvl.Data` queda UNA sola copia, la que hace el driver hacia la
            ' textura, y cero asignaciones intermedias. Si algun dia se quiere el beneficio asincrono de
            ' verdad, eso es un PBO PERSISTENTE por contexto con fence, no uno por textura.
            ' Desbindear explicitamente: con un PBO bindeado, el argumento de datos de TexSubImage2D se
            ' interpreta como un OFFSET dentro del buffer y no como un puntero al array.
            ' PERO ESO ES PRECONDICION DE ESTA FUNCION, NO ESTADO GLOBAL QUE LE TOQUE FIJAR. Antes el
            ' Finally lo dejaba en 0, o sea que convertia "sin PBO durante la subida" en "sin PBO para
            ' siempre" y se llevaba puesto el binding de cualquier uploader ajeno. Se guarda el previo acá y
            ' el Finally lo devuelve. Hoy no hay ningun otro bindeo de PIXEL_UNPACK_BUFFER en la solucion ⇒
            ' esto lee 0 y el comportamiento no cambia en un solo byte; existe para el dia que aparezca un
            ' PBO PERSISTENTE con fence, que es la unica forma en que un PBO compra algo (ver arriba).
            ' El pname va TIPADO (`GetPName.PixelUnpackBufferBinding` = 0x88EF). Un `CType(&H..., GetPName)`
            ' con el numero equivocado COMPILA igual —los enums del CLR no validan el rango— y `glGetIntegerv`
            ' con un pname invalido NO escribe el destino y encola GL_INVALID_ENUM: quedaria siempre 0 (o sea,
            ' el bug de antes, pero documentado como arreglado) y el error contaminaria al proximo GetError.
            ' Va DESPUES del DrainGlErrors de arriba a proposito.
            Try
                GL.GetInteger(GetPName.PixelUnpackBufferBinding, prevPixelUnpackBuffer)
            Catch
                prevPixelUnpackBuffer = 0
            End Try
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0)

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

                    If tex.IsCompressedGL Then
                        ' El overload que toma `PixelFormat` está marcado Obsolete en OpenTK 4
                        ' ("Use InternalFormat overload instead") ⇒ BC40000 en un build que estaba limpio.
                        ' Es el mismo GLenum por el cable: `glInternal` SIEMPRE fue un formato interno
                        ' (GL_COMPRESSED_*), nunca un PixelFormat — el cast viejo mentía sobre el tipo.
                        GL.CompressedTexSubImage2D(Of Byte)(
                        subTarget,
                        m, 0, 0,
                        lvl.Width, lvl.Height,
                        CType(glInternal, OpenTK.Graphics.OpenGL4.InternalFormat),
                        lvl.Data.Length,
                        lvl.Data)
                    Else
                        GL.TexSubImage2D(Of Byte)(
                        subTarget,
                        m, 0, 0,
                        lvl.Width, lvl.Height,
                        CType(glFormat, OpenTK.Graphics.OpenGL4.PixelFormat),
                        CType(glType, PixelType),
                        lvl.Data)
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

            ' Certify upload success. A silent GL error here means the texture is allocated
            ' but its contents are undefined (driver typically zeros it = solid black). We
            ' refuse to return a poisoned ID so the caller can retry instead of caching it.
            Dim subidaOk = CheckGlOk("CreateOpenGL_FromTextureLoaded_PBO upload, DXGI=" & tex.DxgiCodeFinal.ToString())

            ' [AUDIT-UPLOAD] valida la ELIMINACION DEL PBO: ahora se sube directo desde el array
            ' administrado. Una linea por textura con todo lo que hace falta para decir si anduvo.
            If Logger.Enabled Then
                Dim aDxgi = tex.DxgiCodeFinal, aInt = glInternal, aFmt = glFormat, aTyp = glType
                Dim aComp = tex.IsCompressedGL, aCube = tex.IsCubemap, aMips = mipLevels, aFaces = faces
                Dim aW = baseW, aH = baseH, aOk = subidaOk
                Dim aBytes As Long = 0
                For Each l In tex.Levels
                    If l IsNot Nothing AndAlso l.Data IsNot Nothing Then aBytes += l.Data.Length
                Next
                Logger.LogLazy(Function() $"[AUDIT-UPLOAD] {aW}x{aH} dxgi={aDxgi} glInternal=0x{Hex(aInt)} glFormat=0x{Hex(aFmt)} glType=0x{Hex(aTyp)} comp={aComp} cube={aCube} mips={aMips} faces={aFaces} bytes={aBytes} => {If(aOk, "OK", "FALLO")}")
            End If

            If Not subidaOk Then
                GL.DeleteTexture(texID)
                Return 0
            End If

            Return texID

        Catch ex As Exception
            Logger.LogLazy(Function() $"[DDS] CreateOpenGL_FromTextureLoaded_PBO failed. DXGI={tex.DxgiCodeFinal} glInternal=0x{Hex(glInternal)} glFormat=0x{Hex(glFormat)} glType=0x{Hex(glType)} {ex.GetType().Name}: {ex.Message}")

            If texID <> 0 Then
                GL.DeleteTexture(texID)
            End If

        Finally
            ' Se DEVUELVE el PIXEL_UNPACK_BUFFER que hubiera al entrar (hoy siempre 0). El desbindeo es
            ' precondicion de la subida, no un estado que esta funcion deba dejar fijado — ver la nota
            ' completa en el sitio del bindeo.
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, prevPixelUnpackBuffer)
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, prevUnpackAlignment)
            ' Se DEVUELVE el binding que hubiera al entrar, por el mismo argumento que el PBO: desbindear es
            ' precondicion de la subida, no un estado que esta funcion deba dejar fijado. Dejarlo en 0 pisaba
            ' el binding de la unidad activa de cualquier llamador.
            GL.BindTexture(target, prevTextureBinding)
        End Try

        Return 0
    End Function


    Public Function Load_And_GenerateOpenGLTextures_FromFiles(fullpaths As String(), useCompress As Boolean, forceOpenGL As Boolean, srgb As Boolean()) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim ddsFiles As Byte()() = fullpaths.Select(Function(p)
                                                        If File.Exists(p) Then
                                                            Return File.ReadAllBytes(p)
                                                        Else
                                                            Return Array.Empty(Of Byte)()
                                                        End If
                                                    End Function).ToArray()

        Return Load_And_GenerateOpenGLTextures_Memory(fullpaths, ddsFiles, useCompress, forceOpenGL, srgb)
    End Function

    ''' <summary>
    ''' Carga DDS, genera IDs OpenGL y llena Diccionario con metadatos completos.
    ''' </summary>
    Public Function Load_And_GenerateOpenGLTextures_FromDictionary(fullpaths As String(), useCompress As Boolean, forceOpenGL As Boolean, srgb As Boolean()) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim ddsFiles As Byte()()
        Dim result As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        If fullpaths.Length = 1 Then
            ddsFiles = {FilesDictionary_class.GetBytes(fullpaths(0))}
            result = Load_And_GenerateOpenGLTextures_Memory(fullpaths, ddsFiles, useCompress, forceOpenGL, srgb)
        Else
            ddsFiles = FilesDictionary_class.GetMultipleFilesBytes(fullpaths)
            result = Load_And_GenerateOpenGLTextures_Memory(fullpaths, ddsFiles, useCompress, forceOpenGL, srgb)
        End If

        If result.Count <> fullpaths.Length Then
#If DEBUG Then
            Debugger.Break()
#End If
            Throw New Exception("the loader is not returning the same count as the number sent")
        End If
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
    Public Function UploadTextureToGL(tex As DirectXTexWrapperCLI.TextureLoaded, path As String, srgb As Boolean) As PreviewModel.Texture_Loaded_Class
        If tex Is Nothing OrElse Not tex.Loaded Then
            Return New PreviewModel.Texture_Loaded_Class With {
                .Texture_ID = 0,
                .Size = New Size(2, 2),
                .Cubemap = tex IsNot Nothing AndAlso tex.IsCubemap,
                .DGXFormat_Original = If(tex IsNot Nothing, tex.DxgiCodeOriginal, 0),
                .DGXFormat_Final = If(tex IsNot Nothing, tex.DxgiCodeFinal, 0),
                .Loaded = False,
                .Path = path,
                .IsSRGB = srgb
            }
        End If

        Dim id As Integer = CreateOpenGL_FromTextureLoaded_PBO(tex, srgb)
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
            .Loaded = (tex.Loaded AndAlso id > 0),
            .Path = path,
            .IsSRGB = srgb
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

    Public Function Load_And_GenerateOpenGLTextures_Memory(fullpaths As String(), ddsFiles As Byte()(), useCompress As Boolean, forceOpenGL As Boolean, Srgb As Boolean()) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
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
                    .Path = fullpaths(i),
                    .IsSRGB = Srgb(i)
                    }
            Else
                Dim id = CreateOpenGL_FromTextureLoaded_PBO(tex, Srgb(i))
                Dim lvl0 = tex.Levels(0)
                diccionario(fullpaths(i)) = New PreviewModel.Texture_Loaded_Class With {
                    .Texture_ID = id,
                    .Size = New Size(lvl0.Width, lvl0.Height),
                    .Cubemap = tex.IsCubemap,
                    .DGXFormat_Original = tex.DxgiCodeOriginal,
                    .DGXFormat_Final = tex.DxgiCodeFinal,
                    .Loaded = (tex.Loaded AndAlso id > 0),
                    .Path = fullpaths(i),
                    .IsSRGB = Srgb(i)
                    }
            End If

        Next
        results.Clear()
        Return diccionario
    End Function

End Module








