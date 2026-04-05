' Version Uploaded of Wardrobe 3.1.0
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
    Public Const DxgiFormatBc5Unorm As Integer = 83
    Public Const DxgiFormatB8G8R8A8Unorm As Integer = 87


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
        If sourceBitmap Is Nothing Then Throw New ArgumentNullException(NameOf(sourceBitmap))
        If sourceBitmap.Width <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sourceBitmap), "Width debe ser > 0.")
        If sourceBitmap.Height <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sourceBitmap), "Height debe ser > 0.")
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "El DXGI de salida debe ser valido.")
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")
        If generateMipMaps AndAlso mipmaps IsNot Nothing Then Throw New ArgumentException("No combines mipmaps explicitos con generateMipMaps=True.", NameOf(mipmaps))

        Dim mipChain As New List(Of Bitmap) From {sourceBitmap}
        If mipmaps IsNot Nothing Then
            For Each mipBitmap In mipmaps
                If mipBitmap Is Nothing Then
                    Throw New ArgumentException("Hay un mipmap Nothing en la coleccion.", NameOf(mipmaps))
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
                Throw New ArgumentException($"El mip {mipLevel} debe medir {expectedWidth} px de ancho y llego con {mipBitmap.Width}.", NameOf(mipmaps))
            End If

            If mipBitmap.Height <> expectedHeight Then
                Throw New ArgumentException($"El mip {mipLevel} debe medir {expectedHeight} px de alto y llego con {mipBitmap.Height}.", NameOf(mipmaps))
            End If

            request.Subresources.Add(CreateBitmapSubresource(mipBitmap, mipLevel))
        Next

        Dim conversion = DirectXTextureConversionHelper.ConvertSubresources(request)
        Return CreateDdsBytesFromConversion(conversion)
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

        If String.IsNullOrWhiteSpace(outputFilePath) Then Throw New ArgumentException("La ruta de salida es obligatoria.", NameOf(outputFilePath))

        Dim ddsBytes = BitmapToDdsBytes(sourceBitmap, outputDxgiFormat, mipmaps, generateMipMaps, generatedMipLevels, filterFlags, compressFlags, alphaThreshold)
        Dim directoryPath = Path.GetDirectoryName(outputFilePath)

        If Not String.IsNullOrWhiteSpace(directoryPath) Then
            Directory.CreateDirectory(directoryPath)
        End If

        File.WriteAllBytes(outputFilePath, ddsBytes)
    End Sub

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
        If bgraPixels Is Nothing Then Throw New ArgumentNullException(NameOf(bgraPixels))
        Dim expectedLength = Math.BigMul(width, height) * 4L
        If expectedLength > Integer.MaxValue Then Throw New ArgumentOutOfRangeException(NameOf(bgraPixels), "El buffer BGRA excede el maximo soportado.")
        If bgraPixels.Length <> CInt(expectedLength) Then Throw New ArgumentException($"El buffer BGRA debe tener {expectedLength} bytes y llego con {bgraPixels.Length}.", NameOf(bgraPixels))
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "El DXGI de salida debe ser valido.")
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")

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

        request.Subresources.Add(New DxTextureSubresourceBuffer(
            data:=CType(bgraPixels.Clone(), Byte()),
            width:=width,
            height:=height,
            rowPitch:=width * 4,
            slicePitch:=CInt(expectedLength),
            mipLevel:=0,
            arrayIndex:=0))

        Dim conversion = DirectXTextureConversionHelper.ConvertSubresources(request)
        Return CreateDdsBytesFromConversion(conversion)
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

        If String.IsNullOrWhiteSpace(outputFilePath) Then Throw New ArgumentException("La ruta de salida es obligatoria.", NameOf(outputFilePath))

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

        If sourceDdsBytes Is Nothing OrElse sourceDdsBytes.Length = 0 Then Throw New ArgumentException("Los bytes DDS de entrada son obligatorios.", NameOf(sourceDdsBytes))
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "El DXGI de salida debe ser valido.")
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")

        Dim loadedTextures = Loader.LoadTextures({sourceDdsBytes}, useCompress:=True, forceOpenGL:=False)
        If loadedTextures Is Nothing OrElse loadedTextures.Count = 0 OrElse loadedTextures(0) Is Nothing Then
            Throw New InvalidOperationException("No se pudo cargar el DDS de entrada para convertirlo.")
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

        If String.IsNullOrWhiteSpace(outputFilePath) Then Throw New ArgumentException("La ruta de salida es obligatoria.", NameOf(outputFilePath))

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

        If String.IsNullOrWhiteSpace(inputFilePath) Then Throw New ArgumentException("La ruta DDS de entrada es obligatoria.", NameOf(inputFilePath))
        If Not File.Exists(inputFilePath) Then Throw New FileNotFoundException("No se encontro el DDS de entrada.", inputFilePath)

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

        If loadedTexture Is Nothing Then Throw New ArgumentNullException(NameOf(loadedTexture))
        If generatedMipLevels < 0 Then Throw New ArgumentOutOfRangeException(NameOf(generatedMipLevels), "generatedMipLevels debe ser >= 0.")
        If Not loadedTexture.Loaded Then Throw New ArgumentException("La textura cargada no esta marcada como Loaded.", NameOf(loadedTexture))
        If loadedTexture.Levels Is Nothing OrElse loadedTexture.Levels.Count = 0 Then
            Throw New ArgumentException("La textura cargada no trae subrecursos en Levels.", NameOf(loadedTexture))
        End If
        If loadedTexture.DxgiCodeFinal <= 0 Then
            Throw New ArgumentException("DxgiCodeFinal no es valido para conversion.", NameOf(loadedTexture))
        End If
        If outputDxgiFormat <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(outputDxgiFormat), "El DXGI de salida debe ser valido.")

        Dim mipLevels = Math.Max(1, loadedTexture.Miplevels)
        Dim arraySize = Math.Max(1, loadedTexture.Faces)
        If generateMipMaps Then
            If loadedTexture.Levels.Count < arraySize Then
                Throw New ArgumentException($"La textura cargada necesita al menos {arraySize} subrecursos base para regenerar mipmaps.", NameOf(loadedTexture))
            End If
        Else
            Dim expectedSubresources = mipLevels * arraySize
            If loadedTexture.Levels.Count <> expectedSubresources Then
                Throw New ArgumentException($"La textura cargada trae {loadedTexture.Levels.Count} subrecursos pero se esperaban {expectedSubresources}.", NameOf(loadedTexture))
            End If
        End If

        Dim level0 = loadedTexture.Levels(0)
        If level0 Is Nothing Then Throw New ArgumentException("Levels(0) es Nothing.", NameOf(loadedTexture))

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
                Throw New ArgumentException($"Levels({i}) es Nothing.", NameOf(loadedTexture))
            End If
            If level.Data Is Nothing Then
                Throw New ArgumentException($"Levels({i}).Data es Nothing.", NameOf(loadedTexture))
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

        Dim conversion = DirectXTextureConversionHelper.ConvertSubresources(request)
        Return CreateDdsBytesFromConversion(conversion)
    End Function
    Private Function CreateDdsBytesFromConversion(conversion As DxTextureConversionResult) As Byte()
        If conversion Is Nothing Then Throw New InvalidOperationException("La conversion DDS devolvio Nothing.")

        Dim headerBytes = Loader.EncodeDDSHeader(
            conversion.DxgiFormat,
            conversion.Width,
            conversion.Height,
            conversion.ArraySize,
            conversion.MipLevels,
            conversion.IsCubemap)

        Dim payloadBytes = DirectXTextureConversionHelper.ConcatenateSubresources(conversion.Subresources)
        Dim ddsBytes(headerBytes.Length + payloadBytes.Length - 1) As Byte

        System.Buffer.BlockCopy(headerBytes, 0, ddsBytes, 0, headerBytes.Length)
        If payloadBytes.Length > 0 Then
            System.Buffer.BlockCopy(payloadBytes, 0, ddsBytes, headerBytes.Length, payloadBytes.Length)
        End If

        Return ddsBytes
    End Function

    Private Function CreateBitmapSubresource(sourceBitmap As Bitmap, mipLevel As Integer) As DxTextureSubresourceBuffer
        If sourceBitmap Is Nothing Then Throw New ArgumentNullException(NameOf(sourceBitmap))

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
                    Throw New InvalidDataException("El stride del Bitmap no es valido para exportar DDS.")
                End If

                Dim rowPitch = bitmapData.Stride
                If normalizedBitmap.Height > Integer.MaxValue / rowPitch Then
                    Throw New InvalidDataException("El bitmap excede el tamano maximo de subrecurso soportado.")
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

        If subresourceData Is Nothing Then Throw New ArgumentNullException(NameOf(subresourceData))

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
                Throw New ArgumentException("Se recibieron mÃ¡s subrecursos de los esperados.", NameOf(subresourceData))
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
            Throw New ArgumentException($"Faltan subrecursos. Esperados={expectedCount}, recibidos={index}.", NameOf(subresourceData))
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

    Public Function ConvertSubresources(request As DxTextureConversionRequest) As DxTextureConversionResult
        ValidateRequest(request)

        Dim nativeRequest = CreateNativeConversionRequest(request)
        Dim loaderType = GetType(Loader)
        Dim convertMethod As MethodInfo = Nothing

        For Each candidate In loaderType.GetMethods(BindingFlags.Public Or BindingFlags.Static)
            If String.Equals(candidate.Name, "ConvertSubresources", StringComparison.Ordinal) Then
                Dim parameters = candidate.GetParameters()
                If parameters.Length = 1 Then
                    convertMethod = candidate
                    Exit For
                End If
            End If
        Next

        If convertMethod Is Nothing Then
            Throw New MissingMethodException(loaderType.FullName, "ConvertSubresources")
        End If

        Dim nativeResult = convertMethod.Invoke(Nothing, {nativeRequest})
        Return ConvertNativeResult(nativeResult)
    End Function

    Public Function ConvertToArrays(request As DxTextureConversionRequest) As Byte()()
        Dim result = ConvertSubresources(request)
        Dim arrays(result.Subresources.Count - 1)() As Byte

        For i As Integer = 0 To result.Subresources.Count - 1
            arrays(i) = result.Subresources(i).Data
        Next

        Return arrays
    End Function

    Public Function ConcatenateSubresources(subresources As IEnumerable(Of DxTextureSubresourceBuffer)) As Byte()
        If subresources Is Nothing Then Throw New ArgumentNullException(NameOf(subresources))

        Dim payloads As New List(Of Byte())()
        Dim total As Long = 0

        For Each subresource In subresources
            If subresource Is Nothing Then
                Throw New InvalidDataException("Hay un subrecurso Nothing en la colección.")
            End If

            Dim data = If(subresource.Data, Array.Empty(Of Byte)())
            payloads.Add(data)
            total += data.Length
        Next

        If total = 0 Then Return Array.Empty(Of Byte)()
        If total > Integer.MaxValue Then
            Throw New InvalidDataException("El blob concatenado excede Int32.MaxValue.")
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
            Throw New ArgumentOutOfRangeException(NameOf(arraySize), "La cantidad de subrecursos excede Int32.MaxValue.")
        End If

        Return CInt(total)
    End Function

    Private Function ConvertNativeResult(nativeResult As Object) As DxTextureConversionResult
        If nativeResult Is Nothing Then
            Throw New InvalidOperationException("El wrapper devolvio un resultado nulo.")
        End If

        Dim result As New DxTextureConversionResult With {
            .Width = GetRequiredIntProperty(nativeResult, "Width"),
            .Height = GetRequiredIntProperty(nativeResult, "Height"),
            .DxgiFormat = GetRequiredIntProperty(nativeResult, "DxgiFormat"),
            .MipLevels = GetRequiredIntProperty(nativeResult, "MipLevels"),
            .ArraySize = GetRequiredIntProperty(nativeResult, "ArraySize"),
            .IsCubemap = GetRequiredBooleanProperty(nativeResult, "IsCubemap")
        }

        Dim nativeSubresources = TryCast(GetRequiredMemberValue(nativeResult, "Subresources"), System.Collections.IEnumerable)
        If nativeSubresources Is Nothing Then Return result

        For Each nativeSubresource In nativeSubresources
            If nativeSubresource Is Nothing Then
                Throw New InvalidOperationException("El wrapper devolvio un subrecurso nulo.")
            End If

            result.Subresources.Add(New DxTextureSubresourceBuffer(
                data:=GetRequiredByteArrayProperty(nativeSubresource, "Data"),
                width:=GetRequiredIntProperty(nativeSubresource, "Width"),
                height:=GetRequiredIntProperty(nativeSubresource, "Height"),
                rowPitch:=GetRequiredIntProperty(nativeSubresource, "RowPitch"),
                slicePitch:=GetRequiredIntProperty(nativeSubresource, "SlicePitch"),
                mipLevel:=GetRequiredIntProperty(nativeSubresource, "MipLevel"),
                arrayIndex:=GetRequiredIntProperty(nativeSubresource, "ArrayIndex")))
        Next

        Return result
    End Function

    Private Function CreateNativeConversionRequest(request As DxTextureConversionRequest) As Object
        Dim wrapperAssembly = GetType(Loader).Assembly
        Dim requestType = wrapperAssembly.GetType("DirectXTexWrapperCLI.TextureConversionRequest", throwOnError:=True)
        Dim subresourceType = wrapperAssembly.GetType("DirectXTexWrapperCLI.TextureSubresource", throwOnError:=True)
        Dim nativeRequest = Activator.CreateInstance(requestType)

        SetRequiredMemberValue(nativeRequest, "Width", request.Width)
        SetRequiredMemberValue(nativeRequest, "Height", request.Height)
        SetRequiredMemberValue(nativeRequest, "InputDxgiFormat", request.InputDxgiFormat)
        SetRequiredMemberValue(nativeRequest, "OutputDxgiFormat", request.OutputDxgiFormat)
        SetRequiredMemberValue(nativeRequest, "MipLevels", request.MipLevels)
        SetRequiredMemberValue(nativeRequest, "ArraySize", request.ArraySize)
        SetRequiredMemberValue(nativeRequest, "IsCubemap", request.IsCubemap)
        SetRequiredMemberValue(nativeRequest, "AutoGenerateMipMaps", request.AutoGenerateMipMaps)
        SetRequiredMemberValue(nativeRequest, "FilterFlags", request.FilterFlags)
        SetRequiredMemberValue(nativeRequest, "CompressFlags", request.CompressFlags)
        SetRequiredMemberValue(nativeRequest, "AlphaThreshold", request.AlphaThreshold)

        Dim nativeSubresources = Array.CreateInstance(subresourceType, request.Subresources.Count)
        For i As Integer = 0 To request.Subresources.Count - 1
            Dim subresource = request.Subresources(i)
            If subresource Is Nothing Then
                Throw New ArgumentException($"Subresources({i}) es Nothing.", NameOf(request))
            End If

            Dim nativeSubresource = Activator.CreateInstance(subresourceType)
            SetRequiredMemberValue(nativeSubresource, "Data", If(subresource.Data, Array.Empty(Of Byte)()))
            SetRequiredMemberValue(nativeSubresource, "Width", subresource.Width)
            SetRequiredMemberValue(nativeSubresource, "Height", subresource.Height)
            SetRequiredMemberValue(nativeSubresource, "RowPitch", subresource.RowPitch)
            SetRequiredMemberValue(nativeSubresource, "SlicePitch", subresource.SlicePitch)
            SetRequiredMemberValue(nativeSubresource, "MipLevel", subresource.MipLevel)
            SetRequiredMemberValue(nativeSubresource, "ArrayIndex", subresource.ArrayIndex)
            nativeSubresources.SetValue(nativeSubresource, i)
        Next

        SetRequiredMemberValue(nativeRequest, "Subresources", nativeSubresources)
        Return nativeRequest
    End Function

    Private Function GetRequiredMemberValue(instance As Object, memberName As String) As Object
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If String.IsNullOrWhiteSpace(memberName) Then Throw New ArgumentException("El nombre de miembro es obligatorio.", NameOf(memberName))

        Dim instanceType = instance.GetType()
        Dim propertyInfo = instanceType.GetProperty(memberName, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
        If propertyInfo IsNot Nothing Then
            Return propertyInfo.GetValue(instance)
        End If

        Dim fieldInfo = instanceType.GetField(memberName, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
        If fieldInfo IsNot Nothing Then
            Return fieldInfo.GetValue(instance)
        End If

        Throw New MissingMemberException(instanceType.FullName, memberName)
    End Function

    Private Sub SetRequiredMemberValue(instance As Object, memberName As String, value As Object)
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If String.IsNullOrWhiteSpace(memberName) Then Throw New ArgumentException("El nombre de miembro es obligatorio.", NameOf(memberName))

        Dim instanceType = instance.GetType()
        Dim propertyInfo = instanceType.GetProperty(memberName, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
        If propertyInfo IsNot Nothing AndAlso propertyInfo.CanWrite Then
            propertyInfo.SetValue(instance, value)
            Return
        End If

        Dim fieldInfo = instanceType.GetField(memberName, BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
        If fieldInfo IsNot Nothing Then
            fieldInfo.SetValue(instance, value)
            Return
        End If

        Throw New MissingMemberException(instanceType.FullName, memberName)
    End Sub

    Private Function GetRequiredIntProperty(instance As Object, propertyName As String) As Integer
        Return CInt(GetRequiredMemberValue(instance, propertyName))
    End Function

    Private Function GetRequiredBooleanProperty(instance As Object, propertyName As String) As Boolean
        Return CBool(GetRequiredMemberValue(instance, propertyName))
    End Function

    Private Function GetRequiredByteArrayProperty(instance As Object, propertyName As String) As Byte()
        Dim value = TryCast(GetRequiredMemberValue(instance, propertyName), Byte())
        If value Is Nothing Then
            Throw New InvalidCastException($"El miembro {propertyName} no devolvio un Byte().")
        End If

        Return value
    End Function

    Private Sub ValidateRequest(request As DxTextureConversionRequest)
        If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
        If request.Width <= 0 Then Throw New ArgumentOutOfRangeException("Width", "Width debe ser > 0.")
        If request.Height <= 0 Then Throw New ArgumentOutOfRangeException("Height", "Height debe ser > 0.")
        If request.ArraySize <= 0 Then Throw New ArgumentOutOfRangeException("ArraySize", "ArraySize debe ser > 0.")

        If request.AutoGenerateMipMaps Then
            If request.MipLevels < 0 Then Throw New ArgumentOutOfRangeException("MipLevels", "MipLevels debe ser >= 0 cuando AutoGenerateMipMaps = True.")
        Else
            If request.MipLevels <= 0 Then Throw New ArgumentOutOfRangeException("MipLevels", "MipLevels debe ser > 0.")
        End If

        If request.IsCubemap AndAlso (request.ArraySize Mod 6 <> 0) Then
            Throw New ArgumentException("ArraySize debe ser múltiplo de 6 para cubemap.", NameOf(request))
        End If

        Dim expectedCount = If(request.AutoGenerateMipMaps, request.ArraySize, GetExpectedSubresourceCount(request.MipLevels, request.ArraySize))
        If request.Subresources.Count <> expectedCount Then
            Throw New ArgumentException($"La cantidad de subrecursos no coincide. Esperados={expectedCount}, recibidos={request.Subresources.Count}.", NameOf(request))
        End If

        For i As Integer = 0 To request.Subresources.Count - 1
            Dim subresource = request.Subresources(i)
            If subresource Is Nothing Then
                Throw New ArgumentException($"Subresources({i}) es Nothing.", NameOf(request))
            End If
            If subresource.Data Is Nothing Then
                Throw New ArgumentException($"Subresources({i}).Data es Nothing.", NameOf(request))
            End If

            Dim expectedMip = If(request.AutoGenerateMipMaps, 0, i \ request.ArraySize)
            Dim expectedArrayIndex = If(request.AutoGenerateMipMaps, i, i Mod request.ArraySize)
            Dim expectedWidth = CalculateMipExtent(request.Width, expectedMip)
            Dim expectedHeight = CalculateMipExtent(request.Height, expectedMip)

            If subresource.Width <> expectedWidth Then
                Throw New ArgumentException($"Subresources({i}).Width={subresource.Width} pero el mip esperado mide {expectedWidth}.", NameOf(request))
            End If
            If subresource.Height <> expectedHeight Then
                Throw New ArgumentException($"Subresources({i}).Height={subresource.Height} pero el mip esperado mide {expectedHeight}.", NameOf(request))
            End If

            If subresource.MipLevel >= 0 AndAlso subresource.MipLevel <> expectedMip Then
                Throw New ArgumentException($"Subresources({i}).MipLevel={subresource.MipLevel} no coincide con su posición esperada ({expectedMip}).", NameOf(request))
            End If
            If subresource.ArrayIndex >= 0 AndAlso subresource.ArrayIndex <> expectedArrayIndex Then
                Throw New ArgumentException($"Subresources({i}).ArrayIndex={subresource.ArrayIndex} no coincide con su posición esperada ({expectedArrayIndex}).", NameOf(request))
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








