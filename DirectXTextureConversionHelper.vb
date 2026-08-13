' Version Uploaded of Fo4Library 3.2.0
Option Strict On

Imports System.Collections.Generic
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports DirectXTexWrapperCLI




''' <summary>
''' Helpers VB.NET para consumir la API robusta de conversión por subrecurso del wrapper.
''' Reglas importantes:
''' - El orden siempre es mip-major y luego array/face-major.
''' - Si AutoGenerateMipMaps = False, deben venir todos los mipmaps solicitados.
''' - Si AutoGenerateMipMaps = True, solo deben venir los subrecursos base y MipLevels = 0 significa cadena completa.
''' - Si RowPitch/SlicePitch = 0, el subrecurso se interpreta como tight-packed.
''' </summary>
Public Module DirectXTextureConversionHelper
    Public Const DxgiFormatBc1Unorm As Integer = 71
    Public Const DxgiFormatBc3Unorm As Integer = 77
    Public Const DxgiFormatBc4Unorm As Integer = 80
    Public Const DxgiFormatBc5Unorm As Integer = 83
    Public Const DxgiFormatB8G8R8A8Unorm As Integer = 87
    Public Const DxgiFormatBc7Unorm As Integer = 98


    ''' <summary>
    ''' Convierte un Bitmap .NET a un DDS completo (header + payload).
    ''' Si el Bitmap proviene de un PNG, basta con cargarlo con New Bitmap(rutaPng).
    ''' Si generateMipMaps = True, generatedMipLevels = 0 significa cadena completa como texconv -m 0.
    ''' Si generateMipMaps = False, los mipmaps opcionales deben venir completos y ordenados desde mip 1 en adelante.
    ''' </summary>
    Public Function BitmapToDdsBytes(
        sourceBitmap As Bitmap,
        outputDxgiFormat As Integer,
        Optional mipmaps As IEnumerable(Of Bitmap) = Nothing,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F) As Byte()
        ArgumentNullException.ThrowIfNull(sourceBitmap)
        If sourceBitmap.Width <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sourceBitmap), "Width debe ser > 0.")
        If sourceBitmap.Height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sourceBitmap), "Height debe ser > 0.")
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "The output DXGI format is not valid.")
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")
        If generateMipMaps AndAlso mipmaps IsNot Nothing Then Throw New ArgumentException("Do not combine explicit mipmaps with generateMipMaps=True.", NameOf(mipmaps))

        Dim mipChain As New List(Of Bitmap) From {sourceBitmap}
        If mipmaps IsNot Nothing Then
            For Each mipBitmap In mipmaps
                If mipBitmap Is Nothing Then
                    Throw New ArgumentException("There is a Nothing mipmap in the collection.", NameOf(mipmaps))
                End If

                mipChain.Add(mipBitmap)
            Next
        End If

        Dim request As New DxTextureConversionRequest With {
            .Width = sourceBitmap.Width,
            .Height = sourceBitmap.Height,
            .InputDxgiFormat = DxgiFormatB8G8R8A8Unorm,
            .OutputDxgiFormat = outputDxgiFormat,
            .MipLevels = If(generateMipMaps, generatedMipLevels, mipChain.Count),
            .ArraySize = 1,
            .IsCubemap = False,
            .AutoGenerateMipMaps = generateMipMaps,
            .FilterFlags = filterFlags,
            .CompressFlags = compressFlags,
            .AlphaThreshold = alphaThreshold
        }

        Dim inputMipCount = If(generateMipMaps, 1, mipChain.Count)

        For mipLevel As Integer = 0 To inputMipCount - 1
            Dim mipBitmap = mipChain(mipLevel)
            Dim expectedWidth = CalculateMipExtent(sourceBitmap.Width, mipLevel)
            Dim expectedHeight = CalculateMipExtent(sourceBitmap.Height, mipLevel)

            If mipBitmap.Width <> expectedWidth Then
                Throw New ArgumentException($"Mip {mipLevel} must be {expectedWidth} px wide but got {mipBitmap.Width}.", NameOf(mipmaps))
            End If

            If mipBitmap.Height <> expectedHeight Then
                Throw New ArgumentException($"Mip {mipLevel} must be {expectedHeight} px tall but got {mipBitmap.Height}.", NameOf(mipmaps))
            End If

            request.Subresources.Add(CreateBitmapSubresource(mipBitmap, mipLevel))
        Next

        Return ConvertToDdsBytes(request)
    End Function

    ''' <summary>
    ''' Convierte un Bitmap a DDS y lo graba a disco con encabezado completo.
    ''' </summary>
    Public Sub SaveBitmapAsDds(
        sourceBitmap As Bitmap,
        outputFilePath As String,
        outputDxgiFormat As Integer,
        Optional mipmaps As IEnumerable(Of Bitmap) = Nothing,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F)

        If String.IsNullOrWhiteSpace(outputFilePath) Then Throw New ArgumentException("The output path is required.", NameOf(outputFilePath))

        Dim ddsBytes = BitmapToDdsBytes(sourceBitmap, outputDxgiFormat, mipmaps, generateMipMaps, generatedMipLevels, filterFlags, compressFlags, alphaThreshold)
        Dim directoryPath = Path.GetDirectoryName(outputFilePath)

        If Not String.IsNullOrWhiteSpace(directoryPath) Then
            Directory.CreateDirectory(directoryPath)
        End If

        File.WriteAllBytes(outputFilePath, ddsBytes)
    End Sub

    ''' <summary>Encodea un buffer BGRA8 a un DDS completo (header + payload).
    ''' <para>⛔ CONTRATO: el llamador NO puede mutar <paramref name="bgraPixels"/> mientras esta función
    ''' no haya vuelto. El buffer no se clona — el wrapper lo fija y lo copia dentro de la misma llamada
    ''' síncrona— y clonarlo costaba 64 MB por encode de 4096². Pasar un buffer que otro hilo está
    ''' escribiendo produce una textura con un estado intermedio, sin error.</para></summary>
    Public Function Bgra32BytesToDdsBytes(
        width As Integer,
        height As Integer,
        bgraPixels As Byte(),
        outputDxgiFormat As Integer,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F) As Byte()

        If width <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(width), "Width debe ser > 0.")
        If height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(height), "Height debe ser > 0.")
        ArgumentNullException.ThrowIfNull(bgraPixels)
        Dim expectedLength = Math.BigMul(width, height) * 4L
        If expectedLength > Integer.MaxValue Then Throw New ArgumentOutOfRangeException(NameOf(bgraPixels), "The BGRA buffer exceeds the maximum supported size.")
        If bgraPixels.Length <> CInt(expectedLength) Then Throw New ArgumentException($"The BGRA buffer must be {expectedLength} bytes but got {bgraPixels.Length}.", NameOf(bgraPixels))
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "The output DXGI format is not valid.")
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")

        ' Sigue siendo el ÚNICO entry point que fuerza el paralelismo del códec, igual que antes: los
        ' otros dos respetan lo que pida el caller. Lo que cambió es la justificación, no el alcance.
        compressFlags = ResolveCompressFlags(compressFlags)

        Dim request As New DxTextureConversionRequest With {
            .Width = width,
            .Height = height,
            .InputDxgiFormat = DxgiFormatB8G8R8A8Unorm,
            .OutputDxgiFormat = outputDxgiFormat,
            .MipLevels = If(generateMipMaps, generatedMipLevels, 1),
            .ArraySize = 1,
            .IsCubemap = False,
            .AutoGenerateMipMaps = generateMipMaps,
            .FilterFlags = filterFlags,
            .CompressFlags = compressFlags,
            .AlphaThreshold = alphaThreshold
        }

        ' ⛔ SIN Clone. El wrapper sólo LEE este buffer: lo fija con pin_ptr y lo copia al ScratchImage
        ' dentro de la misma llamada SÍNCRONA, antes de devolver. El clon no protegía de nada — para que
        ' hiciera falta, otro hilo tendría que estar mutando el mismo array mientras esta llamada corre, y
        ' en ese escenario el clon tampoco salva (copiaría un estado intermedio). Lo que sí costaba: 64 MB
        ' por encode de 4096², medidos como 320 MB de asignación administrada por llamada contra 85 MB
        ' del camino sin clon ni concatenación.
        ' ⇒ CONTRATO: el llamador no puede mutar `bgraPixels` mientras esta función no haya vuelto.
        request.Subresources.Add(New DxTextureSubresourceBuffer(
            data:=bgraPixels,
            width:=width,
            height:=height,
            rowPitch:=width * 4,
            slicePitch:=CInt(expectedLength),
            mipLevel:=0,
            arrayIndex:=0))

        Return ConvertToDdsBytes(request)
    End Function

    Public Sub SaveBgra32BytesAsDds(
        width As Integer,
        height As Integer,
        bgraPixels As Byte(),
        outputFilePath As String,
        outputDxgiFormat As Integer,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F)

        If String.IsNullOrWhiteSpace(outputFilePath) Then Throw New ArgumentException("The output path is required.", NameOf(outputFilePath))

        Dim ddsBytes = Bgra32BytesToDdsBytes(width, height, bgraPixels, outputDxgiFormat, generateMipMaps, generatedMipLevels, filterFlags, compressFlags, alphaThreshold)
        Dim directoryPath = Path.GetDirectoryName(outputFilePath)

        If Not String.IsNullOrWhiteSpace(directoryPath) Then
            Directory.CreateDirectory(directoryPath)
        End If

        File.WriteAllBytes(outputFilePath, ddsBytes)
    End Sub
    Public Function DdsBytesToDdsBytes(
        sourceDdsBytes As Byte(),
        outputDxgiFormat As Integer,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F) As Byte()

        If sourceDdsBytes Is Nothing OrElse sourceDdsBytes.Length = 0 Then Throw New ArgumentException("The input DDS bytes are required.", NameOf(sourceDdsBytes))
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "The output DXGI format is not valid.")
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")

        Dim loadedTextures = Loader.LoadTextures({sourceDdsBytes}, useCompress:=True, forceOpenGL:=False)
        If loadedTextures Is Nothing OrElse loadedTextures.Count = 0 OrElse loadedTextures(0) Is Nothing Then
            Throw New InvalidOperationException("Could not load the input DDS to convert it.")
        End If

        Return ConvertLoadedTextureToDdsBytes(loadedTextures(0), outputDxgiFormat, generateMipMaps, generatedMipLevels, filterFlags, compressFlags, alphaThreshold)
    End Function

    Public Sub SaveDdsBytesAsDds(
        sourceDdsBytes As Byte(),
        outputFilePath As String,
        outputDxgiFormat As Integer,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F)

        If String.IsNullOrWhiteSpace(outputFilePath) Then Throw New ArgumentException("The output path is required.", NameOf(outputFilePath))

        Dim ddsBytes = DdsBytesToDdsBytes(sourceDdsBytes, outputDxgiFormat, generateMipMaps, generatedMipLevels, filterFlags, compressFlags, alphaThreshold)
        Dim directoryPath = Path.GetDirectoryName(outputFilePath)

        If Not String.IsNullOrWhiteSpace(directoryPath) Then
            Directory.CreateDirectory(directoryPath)
        End If

        File.WriteAllBytes(outputFilePath, ddsBytes)
    End Sub

    Public Sub ConvertDdsFileToDds(
        inputFilePath As String,
        outputFilePath As String,
        outputDxgiFormat As Integer,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F)

        If String.IsNullOrWhiteSpace(inputFilePath) Then Throw New ArgumentException("The input DDS path is required.", NameOf(inputFilePath))
        If Not File.Exists(inputFilePath) Then Throw New FileNotFoundException("The input DDS was not found.", inputFilePath)

        SaveDdsBytesAsDds(File.ReadAllBytes(inputFilePath), outputFilePath, outputDxgiFormat, generateMipMaps, generatedMipLevels, filterFlags, compressFlags, alphaThreshold)
    End Sub

    ''' <summary>
    ''' Convierte un TextureLoaded devuelto por Loader.LoadTextures a un DDS completo en el DXGI pedido.
    ''' Usa DxgiCodeFinal como formato de entrada, porque es el formato real de los bytes guardados en Levels.
    ''' </summary>
    ' Ejemplo BC3 (preserva alpha):
    ' Dim tex = Loader.LoadTextures({File.ReadAllBytes("C:\Texturas\entrada.dds")}, useCompress:=True, forceOpenGL:=False)(0)
    ' File.WriteAllBytes("C:\Texturas\salida_bc3.dds", ConvertLoadedTextureToDdsBytes(tex, DxgiFormatBc3Unorm, generateMipMaps:=True, generatedMipLevels:=0))
    '
    ' Ejemplo BC1 (mas chico, alpha recortado/limitado):
    ' Dim tex = Loader.LoadTextures({File.ReadAllBytes("C:\Texturas\entrada.dds")}, useCompress:=True, forceOpenGL:=False)(0)
    ' File.WriteAllBytes("C:\Texturas\salida_bc1.dds", ConvertLoadedTextureToDdsBytes(tex, DxgiFormatBc1Unorm, generateMipMaps:=True, generatedMipLevels:=0))
    Public Function ConvertLoadedTextureToDdsBytes(
        loadedTexture As TextureLoaded,
        outputDxgiFormat As Integer,
        Optional generateMipMaps As Boolean = False,
        Optional generatedMipLevels As Integer = 0,
        Optional filterFlags As Integer = 0,
        Optional compressFlags As Integer = 0,
        Optional alphaThreshold As Single = 0.5F) As Byte()

        ArgumentNullException.ThrowIfNull(loadedTexture)
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")
        If Not loadedTexture.Loaded Then Throw New ArgumentException("The loaded texture is not marked as Loaded.", NameOf(loadedTexture))
        If loadedTexture.Levels Is Nothing OrElse loadedTexture.Levels.Count = 0 Then
            Throw New ArgumentException("The loaded texture has no subresources in Levels.", NameOf(loadedTexture))
        End If
        If loadedTexture.DxgiCodeFinal <= 0 Then
            Throw New ArgumentException("DxgiCodeFinal is not valid for conversion.", NameOf(loadedTexture))
        End If
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "The output DXGI format is not valid.")

        Dim mipLevels = Math.Max(1, loadedTexture.Miplevels)
        Dim arraySize = Math.Max(1, loadedTexture.Faces)
        If generateMipMaps Then
            If loadedTexture.Levels.Count < arraySize Then
                Throw New ArgumentException($"The loaded texture needs at least {arraySize} base subresources to regenerate mipmaps.", NameOf(loadedTexture))
            End If
        Else
            Dim expectedSubresources = mipLevels * arraySize
            If loadedTexture.Levels.Count <> expectedSubresources Then
                Throw New ArgumentException($"The loaded texture has {loadedTexture.Levels.Count} subresources but {expectedSubresources} were expected.", NameOf(loadedTexture))
            End If
        End If

        Dim level0 = loadedTexture.Levels(0)
        If level0 Is Nothing Then Throw New ArgumentException("Levels(0) is Nothing.", NameOf(loadedTexture))

        Dim request As New DxTextureConversionRequest With {
            .Width = level0.Width,
            .Height = level0.Height,
            .InputDxgiFormat = loadedTexture.DxgiCodeFinal,
            .OutputDxgiFormat = outputDxgiFormat,
            .MipLevels = If(generateMipMaps, generatedMipLevels, mipLevels),
            .ArraySize = arraySize,
            .IsCubemap = loadedTexture.IsCubemap,
            .AutoGenerateMipMaps = generateMipMaps,
            .FilterFlags = filterFlags,
            .CompressFlags = compressFlags,
            .AlphaThreshold = alphaThreshold
        }

        Dim inputSubresourceCount = If(generateMipMaps, arraySize, loadedTexture.Levels.Count)

        For i As Integer = 0 To inputSubresourceCount - 1
            Dim level = loadedTexture.Levels(i)
            If level Is Nothing Then
                Throw New ArgumentException($"Levels({i}) is Nothing.", NameOf(loadedTexture))
            End If
            If level.Data Is Nothing Then
                Throw New ArgumentException($"Levels({i}).Data is Nothing.", NameOf(loadedTexture))
            End If

            Dim mipLevel = If(generateMipMaps, 0, i \ arraySize)
            Dim arrayIndex = If(generateMipMaps, i, i Mod arraySize)

            request.Subresources.Add(New DxTextureSubresourceBuffer(
                data:=level.Data,
                width:=level.Width,
                height:=level.Height,
                rowPitch:=0,
                slicePitch:=0,
                mipLevel:=mipLevel,
                arrayIndex:=arrayIndex))
        Next

        Return ConvertToDdsBytes(request)
    End Function
    ''' <summary>TEX_COMPRESS_PARALLEL (0x10000000) va SIEMPRE. Los bytes son idénticos al serial —el
    ''' compress BCn es por bloque independiente— así que el único eje es el tiempo.
    ''' <para>⛔ La justificación que estaba escrita acá ("BC3 90 ms→4 ms") era FALSA, y el modo de falla
    ''' vale más que el número: salía de <c>RunFmtTest</c>, que cronometra el brazo SERIE en la PRIMERA
    ''' llamada del proceso. Medía el arranque en frío (carga del wrapper C++/CLI, factoría WIC, JIT),
    ''' no el códec. Con calentamiento no reproduce ni de lejos.</para>
    ''' <para>Lo que sí mide <c>Tools/TexCodecPerfProbe</c> (12 hilos lógicos, mismo buffer, bytes
    ''' verificados idénticos en los dos brazos):</para>
    ''' <list type="table">
    ''' <item><term>BC1/BC3/BC5 1024²</term><description><b>0,96–1,05×</b> — no gana nada, y el CPU/wall
    ''' sube de 0,98 a 11,2.</description></item>
    ''' <item><term>BC1/BC3/BC5 2048²</term><description><b>1,14–1,21×</b> — acá sí gana.</description></item>
    ''' <item><term>BC7 1024²</term><description>serie <b>220,9 s</b> / paralelo <b>39,0 s</b> ⇒ <b>5,66×</b>.
    ''' BC7 es el único códec compute-bound; los baratos están limitados por ancho de banda de memoria, y
    ''' por eso no escalan con los núcleos.</description></item>
    ''' <item><term>lote de 24×1024² con DOP=12 (la forma del bake)</term><description><b>0,98–1,09×</b> —
    ''' o sea NINGUNA diferencia. Con el fan-out por NPC la máquina ya está saturada y el paralelismo
    ''' interno no suma ni resta.</description></item>
    ''' </list>
    ''' <para>⇒ Se probó derivarlo por formato (prenderlo sólo en BC7) y la medición lo REFUTÓ: no mejora
    ''' el lote y hace perder 1,14–1,21× en una textura grande sola, que es el caso interactivo. Queda
    ''' incondicional, que es lo que la medición sostiene.</para>
    ''' <para>⛔ NO hay perilla de entorno para apagarlo. La hubo por dos horas y la sacó la revisión de
    ''' arquitectura, con razón: el valor no se DERIVA de nada, así que una env var no "fija" un valor
    ''' derivado —que es la única variante que <c>00-reglas-app-distribuida</c> habilita— sino que agrega
    ''' un SEGUNDO comportamiento, y su único valor útil era exactamente el de antes. Eso es
    ''' <c>00-reglas-nunca-modo-legacy-para-no-romper</c>. Para medir el eje está
    ''' <c>Tools/TexCodecPerfProbe</c>, que le pasa los flags exactos al wrapper sin tocar el entorno.</para></summary>
    Private Function ResolveCompressFlags(compressFlags As Integer) As Integer
        Const TEX_COMPRESS_PARALLEL As Integer = &H10000000
        Return compressFlags Or TEX_COMPRESS_PARALLEL
    End Function

    ''' <summary>Convierte y devuelve el DDS COMPLETO en UN solo array, por el camino de un solo buffer
    ''' del wrapper.
    ''' <para>El camino anterior (convertir a subrecursos → <c>ConcatenateSubresources</c> → copia final)
    ''' materializaba el payload TRES veces en memoria administrada, todas en LOH para una textura de
    ''' cara. Medido con 4096² BGRA8: <b>320 MB</b> asignados por encode (con el Clone de la entrada)
    ''' contra <b>~85 MB</b> por este camino. Los bytes de salida son los mismos para ArraySize = 1, que
    ''' es todo lo que la app produce; ver la nota de orden del payload en el wrapper.</para></summary>
    Private Function ConvertToDdsBytes(request As DxTextureConversionRequest) As Byte()
        ValidateRequest(request)
        Dim ddsBytes = Loader.ConvertSubresourcesToDds(ToNativeRequest(request))
        If ddsBytes Is Nothing Then Throw New InvalidOperationException("The DDS conversion returned Nothing.")
        Return ddsBytes
    End Function

    Private Function CreateBitmapSubresource(sourceBitmap As Bitmap, mipLevel As Integer) As DxTextureSubresourceBuffer
        ArgumentNullException.ThrowIfNull(sourceBitmap)

        Using normalizedBitmap As New Bitmap(sourceBitmap.Width, sourceBitmap.Height, Imaging.PixelFormat.Format32bppArgb)
            Using g = System.Drawing.Graphics.FromImage(normalizedBitmap)
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy
                g.DrawImage(sourceBitmap, New Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height))
            End Using

            Dim rect As New Rectangle(0, 0, normalizedBitmap.Width, normalizedBitmap.Height)
            Dim bitmapData As BitmapData = Nothing

            Try
                bitmapData = normalizedBitmap.LockBits(rect, ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)

                If bitmapData.Stride <= 0 Then
                    Throw New InvalidDataException("The Bitmap stride is not valid for exporting DDS.")
                End If

                Dim rowPitch = bitmapData.Stride
                If normalizedBitmap.Height > Integer.MaxValue / rowPitch Then
                    Throw New InvalidDataException("The bitmap exceeds the maximum supported subresource size.")
                End If

                Dim slicePitch = rowPitch * normalizedBitmap.Height
                Dim pixelBytes(slicePitch - 1) As Byte

                For row As Integer = 0 To normalizedBitmap.Height - 1
                    Dim sourcePtr = IntPtr.Add(bitmapData.Scan0, row * rowPitch)
                    Marshal.Copy(sourcePtr, pixelBytes, row * rowPitch, rowPitch)
                Next

                Return New DxTextureSubresourceBuffer(
                    data:=pixelBytes,
                    width:=normalizedBitmap.Width,
                    height:=normalizedBitmap.Height,
                    rowPitch:=rowPitch,
                    slicePitch:=slicePitch,
                    mipLevel:=mipLevel,
                    arrayIndex:=0)
            Finally
                If bitmapData IsNot Nothing Then
                    normalizedBitmap.UnlockBits(bitmapData)
                End If
            End Try
        End Using
    End Function


    Public NotInheritable Class DxTextureSubresourceBuffer
        Public Property Data As Byte()
        Public Property Width As Integer
        Public Property Height As Integer
        Public Property RowPitch As Integer
        Public Property SlicePitch As Integer
        Public Property MipLevel As Integer
        Public Property ArrayIndex As Integer

        Public Sub New()
            Data = Array.Empty(Of Byte)()
            MipLevel = -1
            ArrayIndex = -1
        End Sub

        Public Sub New(
            data As Byte(),
            width As Integer,
            height As Integer,
            Optional rowPitch As Integer = 0,
            Optional slicePitch As Integer = 0,
            Optional mipLevel As Integer = -1,
            Optional arrayIndex As Integer = -1)

            Me.Data = If(data, Array.Empty(Of Byte)())
            Me.Width = width
            Me.Height = height
            Me.RowPitch = rowPitch
            Me.SlicePitch = slicePitch
            Me.MipLevel = mipLevel
            Me.ArrayIndex = arrayIndex
        End Sub
    End Class

    Public NotInheritable Class DxTextureConversionRequest
        Public Property Width As Integer
        Public Property Height As Integer
        Public Property InputDxgiFormat As Integer
        Public Property OutputDxgiFormat As Integer
        Public Property MipLevels As Integer = 1
        Public Property ArraySize As Integer = 1
        Public Property IsCubemap As Boolean
        Public Property AutoGenerateMipMaps As Boolean
        Public Property FilterFlags As Integer
        Public Property CompressFlags As Integer
        Public Property AlphaThreshold As Single = 0.5F

        Public ReadOnly Property Subresources As List(Of DxTextureSubresourceBuffer)

        Public Sub New()
            Subresources = New List(Of DxTextureSubresourceBuffer)()
        End Sub
    End Class

    Public NotInheritable Class DxTextureConversionResult
        Public Property Width As Integer
        Public Property Height As Integer
        Public Property DxgiFormat As Integer
        Public Property MipLevels As Integer
        Public Property ArraySize As Integer
        Public Property IsCubemap As Boolean

        Public ReadOnly Property Subresources As List(Of DxTextureSubresourceBuffer)

        Friend Sub New()
            Subresources = New List(Of DxTextureSubresourceBuffer)()
        End Sub
    End Class

    Public Function BuildTightRequest(
        width As Integer,
        height As Integer,
        inputDxgiFormat As Integer,
        outputDxgiFormat As Integer,
        mipLevels As Integer,
        arraySize As Integer,
        isCubemap As Boolean,
        subresourceData As IEnumerable(Of Byte())) As DxTextureConversionRequest

        ArgumentNullException.ThrowIfNull(subresourceData)

        Dim request As New DxTextureConversionRequest With {
            .Width = width,
            .Height = height,
            .InputDxgiFormat = inputDxgiFormat,
            .OutputDxgiFormat = outputDxgiFormat,
            .MipLevels = mipLevels,
            .ArraySize = arraySize,
            .IsCubemap = isCubemap
        }

        Dim expectedCount = GetExpectedSubresourceCount(mipLevels, arraySize)
        Dim index As Integer = 0

        For Each subresourceBytes In subresourceData
            If index >= expectedCount Then
                Throw New ArgumentException("Received more subresources than expected.", NameOf(subresourceData))
            End If

            Dim mipLevel = index \ arraySize
            Dim arrayIndex = index Mod arraySize

            request.Subresources.Add(New DxTextureSubresourceBuffer(
                data:=If(subresourceBytes, Array.Empty(Of Byte)()),
                width:=CalculateMipExtent(width, mipLevel),
                height:=CalculateMipExtent(height, mipLevel),
                rowPitch:=0,
                slicePitch:=0,
                mipLevel:=mipLevel,
                arrayIndex:=arrayIndex))

            index += 1
        Next

        If index <> expectedCount Then
            Throw New ArgumentException($"Missing subresources. Expected={expectedCount}, received={index}.", NameOf(subresourceData))
        End If

        Return request
    End Function

    ''' <summary>
    ''' Atajo para el caso en que ya tienes todos los subrecursos como byte()() tight-packed.
    ''' </summary>
    Public Function ConvertTightSubresources(
        width As Integer,
        height As Integer,
        inputDxgiFormat As Integer,
        outputDxgiFormat As Integer,
        mipLevels As Integer,
        arraySize As Integer,
        isCubemap As Boolean,
        subresourceData As IEnumerable(Of Byte())) As DxTextureConversionResult

        Dim request = BuildTightRequest(width, height, inputDxgiFormat, outputDxgiFormat, mipLevels, arraySize, isCubemap, subresourceData)
        Return ConvertSubresources(request)
    End Function

    ''' <summary>
    ''' Igual que ConvertTightSubresources, pero devuelve solo los byte()() resultantes.
    ''' </summary>
    Public Function ConvertTightSubresourcesToArrays(
        width As Integer,
        height As Integer,
        inputDxgiFormat As Integer,
        outputDxgiFormat As Integer,
        mipLevels As Integer,
        arraySize As Integer,
        isCubemap As Boolean,
        subresourceData As IEnumerable(Of Byte())) As Byte()()

        Dim result = ConvertTightSubresources(width, height, inputDxgiFormat, outputDxgiFormat, mipLevels, arraySize, isCubemap, subresourceData)
        Dim arrays(result.Subresources.Count - 1)() As Byte

        For i As Integer = 0 To result.Subresources.Count - 1
            arrays(i) = result.Subresources(i).Data
        Next

        Return arrays
    End Function

    ''' <summary>Conversión por subrecursos. Para el consumidor que quiere el DDS entero está
    ''' <see cref="ConvertToDdsBytes"/>, que no materializa un array por mip.</summary>
    Public Function ConvertSubresources(request As DxTextureConversionRequest) As DxTextureConversionResult
        ValidateRequest(request)
        Return FromNativeResult(Loader.ConvertSubresources(ToNativeRequest(request)))
    End Function

    Public Function ConvertToArrays(request As DxTextureConversionRequest) As Byte()()
        Dim result = ConvertSubresources(request)
        Dim arrays(result.Subresources.Count - 1)() As Byte

        For i As Integer = 0 To result.Subresources.Count - 1
            arrays(i) = result.Subresources(i).Data
        Next

        Return arrays
    End Function

    ''' <summary>Concatena los subrecursos en el ORDEN DEL FORMATO DDS: array-major y después mip
    ''' (para cada cara, toda su cadena de mips; después la cara siguiente).
    ''' <para>⛔ Antes concatenaba en el orden en que venían, que es el orden en que el wrapper los
    ''' EMITE — mip-major. Los dos coinciden con <c>ArraySize = 1</c>, que es todo lo que la app produce,
    ''' y por eso nadie lo vio; con un cubemap el archivo salía con las caras entrelazadas. Medido con la
    ''' fixture <c>huecos</c> del probe (cubemap 32² × 6 caras × 2 mips, cara i marcada con R = 10 + i·40):
    ''' el orden viejo devolvía <c>10;50;90;130;210;50</c> en las posiciones de las caras, el correcto
    ''' devuelve <c>10;50;90;130;170;210</c>.</para>
    ''' <para>Hace falta ARRAY-major porque es lo que escribe <c>DirectX::SaveToDDSMemory</c>
    ''' (<c>for item { for level }</c>) y lo que espera cualquier lector de DDS. El reordenamiento usa el
    ''' <c>MipLevel</c>/<c>ArrayIndex</c> que cada subrecurso ya trae; si vienen sin marcar (índices
    ''' negativos) se respeta el orden de llegada, que es lo único que se puede hacer sin inventar.</para></summary>
    Public Function ConcatenateSubresources(subresources As IEnumerable(Of DxTextureSubresourceBuffer)) As Byte()
        ArgumentNullException.ThrowIfNull(subresources)

        Dim lista As New List(Of DxTextureSubresourceBuffer)()
        For Each subresource In subresources
            If subresource Is Nothing Then
                Throw New InvalidDataException("There is a Nothing subresource in the collection.")
            End If
            lista.Add(subresource)
        Next

        Dim todosMarcados = lista.Count > 0 AndAlso lista.TrueForAll(Function(s) s.MipLevel >= 0 AndAlso s.ArrayIndex >= 0)
        Dim ordenados As IEnumerable(Of DxTextureSubresourceBuffer) = lista
        If todosMarcados Then
            ordenados = lista.OrderBy(Function(s) s.ArrayIndex).ThenBy(Function(s) s.MipLevel)
        End If

        Dim payloads As New List(Of Byte())()
        Dim total As Long = 0

        For Each subresource In ordenados
            Dim data = If(subresource.Data, Array.Empty(Of Byte)())
            payloads.Add(data)
            total += data.Length
        Next

        If total = 0 Then Return Array.Empty(Of Byte)()
        If total > Integer.MaxValue Then
            Throw New InvalidDataException("The concatenated blob exceeds Int32.MaxValue.")
        End If

        Dim output(CInt(total) - 1) As Byte
        Dim offset As Integer = 0

        For Each payloadBytes In payloads
            Buffer.BlockCopy(payloadBytes, 0, output, offset, payloadBytes.Length)
            offset += payloadBytes.Length
        Next

        Return output
    End Function

    Public Function GetExpectedSubresourceCount(mipLevels As Integer, arraySize As Integer) As Integer
        If mipLevels <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(mipLevels))
        If arraySize <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(arraySize))

        Dim total = CLng(mipLevels) * CLng(arraySize)
        If total > Integer.MaxValue Then
            Throw New ArgumentOutOfRangeException(NameOf(arraySize), "The subresource count exceeds Int32.MaxValue.")
        End If

        Return CInt(total)
    End Function

    ''' <summary>Mapea el resultado del wrapper a los tipos de este módulo.
    ''' <para>⛔ Antes esto se hacía por REFLEXIÓN: se buscaba el tipo por nombre, se creaba con
    ''' <c>Activator</c>, se seteaba miembro por miembro con <c>SetValue</c> y se invocaba el método con
    ''' <c>MethodInfo.Invoke</c> — todo en CADA llamada, incluido el barrido de <c>GetMethods</c>. El
    ''' módulo ya hacía <c>Imports DirectXTexWrapperCLI</c> y usaba <c>Loader</c> tipado dos líneas más
    ''' arriba, así que la ABI era conocida en compilación. En tiempo no se medía (queda bajo el piso de
    ''' ruido del A/A hasta 4096²); lo que se saca es el modo de falla: un renombre en el wrapper pasaba
    ''' el compilador y explotaba en runtime como <c>MissingMemberException</c> en el medio de un bake.</para></summary>
    Private Function FromNativeResult(nativeResult As TextureConversionResult) As DxTextureConversionResult
        If nativeResult Is Nothing Then
            Throw New InvalidOperationException("The wrapper returned a null result.")
        End If

        Dim result As New DxTextureConversionResult With {
            .Width = nativeResult.Width,
            .Height = nativeResult.Height,
            .DxgiFormat = nativeResult.DxgiFormat,
            .MipLevels = nativeResult.MipLevels,
            .ArraySize = nativeResult.ArraySize,
            .IsCubemap = nativeResult.IsCubemap
        }

        If nativeResult.Subresources Is Nothing Then Return result

        For Each nativeSubresource In nativeResult.Subresources
            If nativeSubresource Is Nothing Then
                Throw New InvalidOperationException("The wrapper returned a null subresource.")
            End If
            If nativeSubresource.Data Is Nothing Then
                Throw New InvalidOperationException("The wrapper returned a subresource without Data.")
            End If

            result.Subresources.Add(New DxTextureSubresourceBuffer(
                data:=nativeSubresource.Data,
                width:=nativeSubresource.Width,
                height:=nativeSubresource.Height,
                rowPitch:=nativeSubresource.RowPitch,
                slicePitch:=nativeSubresource.SlicePitch,
                mipLevel:=nativeSubresource.MipLevel,
                arrayIndex:=nativeSubresource.ArrayIndex))
        Next

        Return result
    End Function

    Private Function ToNativeRequest(request As DxTextureConversionRequest) As TextureConversionRequest
        Dim nativeRequest As New TextureConversionRequest()
        nativeRequest.Width = request.Width
        nativeRequest.Height = request.Height
        nativeRequest.InputDxgiFormat = request.InputDxgiFormat
        nativeRequest.OutputDxgiFormat = request.OutputDxgiFormat
        nativeRequest.MipLevels = request.MipLevels
        nativeRequest.ArraySize = request.ArraySize
        nativeRequest.IsCubemap = request.IsCubemap
        nativeRequest.AutoGenerateMipMaps = request.AutoGenerateMipMaps
        nativeRequest.FilterFlags = request.FilterFlags
        nativeRequest.CompressFlags = request.CompressFlags
        nativeRequest.AlphaThreshold = request.AlphaThreshold

        Dim nativeSubresources(request.Subresources.Count - 1) As TextureSubresource
        For i As Integer = 0 To request.Subresources.Count - 1
            Dim subresource = request.Subresources(i)
            If subresource Is Nothing Then
                Throw New ArgumentException($"Subresources({i}) is Nothing.", NameOf(request))
            End If

            nativeSubresources(i) = New TextureSubresource(
                If(subresource.Data, Array.Empty(Of Byte)()),
                subresource.Width,
                subresource.Height,
                subresource.RowPitch,
                subresource.SlicePitch,
                subresource.MipLevel,
                subresource.ArrayIndex)
        Next

        nativeRequest.Subresources = nativeSubresources
        Return nativeRequest
    End Function

    Private Sub ValidateRequest(request As DxTextureConversionRequest)
        ArgumentNullException.ThrowIfNull(request)
        If request.Width <= 0 Then Throw New ArgumentOutOfRangeException("Width", "Width debe ser > 0.")
        If request.Height <= 0 Then Throw New ArgumentOutOfRangeException("Height", "Height debe ser > 0.")
        If request.ArraySize <= 0 Then Throw New ArgumentOutOfRangeException("ArraySize", "ArraySize debe ser > 0.")

        If request.AutoGenerateMipMaps Then
            If request.MipLevels < 0 Then Throw New ArgumentOutOfRangeException("MipLevels", "MipLevels must be >= 0 when AutoGenerateMipMaps = True.")
        Else
            If request.MipLevels <= 0 Then Throw New ArgumentOutOfRangeException("MipLevels", "MipLevels must be > 0.")
        End If

        If request.IsCubemap AndAlso (request.ArraySize Mod 6 <> 0) Then
            Throw New ArgumentException("ArraySize must be a multiple of 6 for a cubemap.", NameOf(request))
        End If

        Dim expectedCount = If(request.AutoGenerateMipMaps, request.ArraySize, GetExpectedSubresourceCount(request.MipLevels, request.ArraySize))
        If request.Subresources.Count <> expectedCount Then
            Throw New ArgumentException($"The subresource count does not match. Expected={expectedCount}, received={request.Subresources.Count}.", NameOf(request))
        End If

        For i As Integer = 0 To request.Subresources.Count - 1
            Dim subresource = request.Subresources(i)
            If subresource Is Nothing Then
                Throw New ArgumentException($"Subresources({i}) is Nothing.", NameOf(request))
            End If
            If subresource.Data Is Nothing Then
                Throw New ArgumentException($"Subresources({i}).Data is Nothing.", NameOf(request))
            End If

            Dim expectedMip = If(request.AutoGenerateMipMaps, 0, i \ request.ArraySize)
            Dim expectedArrayIndex = If(request.AutoGenerateMipMaps, i, i Mod request.ArraySize)
            Dim expectedWidth = CalculateMipExtent(request.Width, expectedMip)
            Dim expectedHeight = CalculateMipExtent(request.Height, expectedMip)

            If subresource.Width <> expectedWidth Then
                Throw New ArgumentException($"Subresources({i}).Width={subresource.Width} but the expected mip is {expectedWidth} px wide.", NameOf(request))
            End If
            If subresource.Height <> expectedHeight Then
                Throw New ArgumentException($"Subresources({i}).Height={subresource.Height} but the expected mip is {expectedHeight} px tall.", NameOf(request))
            End If

            If subresource.MipLevel >= 0 AndAlso subresource.MipLevel <> expectedMip Then
                Throw New ArgumentException($"Subresources({i}).MipLevel={subresource.MipLevel} does not match its expected position ({expectedMip}).", NameOf(request))
            End If
            If subresource.ArrayIndex >= 0 AndAlso subresource.ArrayIndex <> expectedArrayIndex Then
                Throw New ArgumentException($"Subresources({i}).ArrayIndex={subresource.ArrayIndex} does not match its expected position ({expectedArrayIndex}).", NameOf(request))
            End If
        Next
    End Sub

    Private Function CalculateMipExtent(baseExtent As Integer, mipLevel As Integer) As Integer
        If baseExtent <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(baseExtent))
        If mipLevel < 0 Then Throw New ArgumentOutOfRangeException(NameOf(mipLevel))

        Dim value = baseExtent
        For i As Integer = 1 To mipLevel
            value = Math.Max(1, value \ 2)
        Next

        Return value
    End Function

End Module








