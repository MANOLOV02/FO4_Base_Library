Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Numerics

Public Partial Class HkxObjectGraph_Class
    Private Enum HkxSplineTrackValueType_Enum
        Identity = 0
        StaticValue = 1
        SplineValue = 2
    End Enum

    Private Structure HkxSplineTrackMask_Struct
        Public PosQuant As Byte
        Public RotQuant As Byte
        Public ScaleQuant As Byte
        Public PosFlags As Byte
        Public RotFlags As Byte
        Public ScaleFlags As Byte

        Public Function GetPositionType(axis As Integer) As HkxSplineTrackValueType_Enum
            If ((CInt(PosFlags) >> (axis + 4)) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.SplineValue
            If ((CInt(PosFlags) >> axis) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.StaticValue
            Return HkxSplineTrackValueType_Enum.Identity
        End Function

        Public Function GetScaleType(axis As Integer) As HkxSplineTrackValueType_Enum
            If ((CInt(ScaleFlags) >> (axis + 4)) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.SplineValue
            If ((CInt(ScaleFlags) >> axis) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.StaticValue
            Return HkxSplineTrackValueType_Enum.Identity
        End Function

        Public Function GetRotationType() As HkxSplineTrackValueType_Enum
            If ((CInt(RotFlags) >> 4) And &HF) <> 0 Then Return HkxSplineTrackValueType_Enum.SplineValue
            If (CInt(RotFlags) And &HF) <> 0 Then Return HkxSplineTrackValueType_Enum.StaticValue
            Return HkxSplineTrackValueType_Enum.Identity
        End Function

        Public Function HasAnyPositionSpline() As Boolean
            Return GetPositionType(0) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetPositionType(1) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetPositionType(2) = HkxSplineTrackValueType_Enum.SplineValue
        End Function

        Public Function HasAnyScaleSpline() As Boolean
            Return GetScaleType(0) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetScaleType(1) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetScaleType(2) = HkxSplineTrackValueType_Enum.SplineValue
        End Function
    End Structure

    Private Structure HkxSplineAxisInfo_Struct
        Public Type As HkxSplineTrackValueType_Enum
        Public MinValue As Single
        Public MaxValue As Single
    End Structure



    ''' <summary>El binding del clip: el PRIMERO QUE EL CONTENEDOR DECLARA, no el primer
    ''' bloque serializado. Nothing si el archivo no trae ninguno.</summary>
    Public Function BindingPrincipal() As Havok.Canon.Objects.HkObj_HkaAnimationBinding
        Dim b = ParseAnimationBindings()
        If b.Count = 0 Then Return Nothing
        Return b(0)
    End Function

    Public Function ParseAnimationBindings() As List(Of Havok.Canon.Objects.HkObj_HkaAnimationBinding)
        Dim result As New List(Of Havok.Canon.Objects.HkObj_HkaAnimationBinding)

        ' ⛔ EL ORDEN LO DECLARA `hkaAnimationContainer.bindings`. Ordenar por `RelativeOffset` es
        ' usar el orden en que el serializador dejo los bloques, que no es una ley del formato.
        For Each obj In BloquesDelContenedor("bindings", {"hkaAnimationBinding"})
            Dim binding = Havok.Canon.Objects.HkObj_HkaAnimationBinding.Read(Me, obj)
            If Not IsNothing(binding) Then result.Add(binding)
        Next

        Return result
    End Function


    ''' <summary>
    ''' ⛔ LAS ANIMACIONES DEL ARCHIVO, SEA CUAL SEA LA COMPRESION.
    '''
    ''' <para>Habia dos funciones con el MISMO cuerpo — emparejar bindings, fallback posicional,
    ''' log — una por compresion, y quince consumidores que llamaban a las dos en pareja. Un `.hkx`
    ''' con spline Y lossless devolvia solo la primera, porque el segundo brazo del `If` solo corria
    ''' cuando el primero venia vacio.</para>
    '''
    ''' <para>El emparejado con `hkaAnimationBinding` se hace UNA vez sobre el conjunto entero, que
    ''' es lo que el archivo declara: los bindings apuntan a su animacion por referencia, sin
    ''' importar como este comprimida.</para>
    ''' </summary>
    ''' <summary>Los clips que NO se pudieron descomprimir en la ultima llamada a
    ''' `Animaciones()`, con su clase y el motivo. Un clip roto es un DATO del archivo: si se
    ''' lo traga el parser, el llamador ve 'este .hkx no tiene animaciones' y no es cierto.</summary>
    Public ReadOnly Property AnimacionesFallidas As New List(Of String)

    Public Function Animaciones() As List(Of HkxAnimacionDescomprimida_Class)
        Dim r As New List(Of HkxAnimacionDescomprimida_Class)
        AnimacionesFallidas.Clear()

        ' ⛔ EL ORDEN LO DECLARA `hkaAnimationContainer.animations`, no el serializador.
        ' Antes eran DOS barridos por clase, cada uno ordenado por `RelativeOffset`: eso
        ' ademas ponia TODAS las spline antes que TODAS las lossless, un orden que el archivo
        ' no dice en ningun lado. La subclase concreta la sigue diciendo el nombre de clase
        ' del bloque, que es lo unico que la declara.
        For Each obj In BloquesDelContenedor("animations",
                                            {"hkaSplineCompressedAnimation", "hkaLosslessCompressedAnimation"})
            ' ⛔⛔ UN CLIP ROTO NO SE LLEVA PUESTOS A LOS DEMAS.
            ' Sin esta guarda, si el clip #2 del archivo tira, se pierden TODOS los del archivo y el
            ' llamador ve 'no tiene animaciones'. Medido sobre el corpus: 15.055 archivos de FO4 y
            ' 6.117 de SSE terminaban asi. El que falla queda ANOTADO, no tragado.
            Dim a As HkxAnimacionDescomprimida_Class = Nothing
            Try
                If obj.ClassName.Equals("hkaSplineCompressedAnimation", StringComparison.OrdinalIgnoreCase) Then
                    a = ParseAnimation(obj)
                ElseIf obj.ClassName.Equals("hkaLosslessCompressedAnimation", StringComparison.OrdinalIgnoreCase) Then
                    a = ParseLosslessAnimation(obj)
                End If
            Catch ex As Exception
                AnimacionesFallidas.Add($"{obj.ClassName} @0x{obj.RelativeOffset:X}: {ex.GetType().Name}: {ex.Message}")
                If Logger.Enabled Then Logger.LogLazy(Function() $"[ANIM-FALLO] {obj.ClassName} @0x{obj.RelativeOffset:X}: {ex.Message}")
                a = Nothing
            End Try
            If Not IsNothing(a) Then r.Add(a)
        Next
        If r.Count = 0 Then Return r

        Dim porOffset As New Dictionary(Of Integer, HkxAnimacionDescomprimida_Class)
        For Each a In r
            If a.Animacion?.Source Is Nothing Then Continue For
            porOffset(a.Animacion.Source.RelativeOffset) = a
        Next

        Dim sueltos As New List(Of Havok.Canon.Objects.HkObj_HkaAnimationBinding)
        For Each binding In ParseAnimationBindings()
            If binding.Animation IsNot Nothing Then
                Dim quien As HkxAnimacionDescomprimida_Class = Nothing
                If porOffset.TryGetValue(binding.Animation.RelativeOffset, quien) AndAlso quien.Binding Is Nothing Then
                    quien.Binding = binding
                    Continue For
                End If
            End If
            sueltos.Add(binding)
        Next

        ''' ⛔ El fallback POSICIONAL solo cuando es INEQUIVOCO: exactamente una animacion sin
        ''' binding y exactamente un binding sin animacion. Grapar N con N por orden de enumeracion
        ''' cruzaria tracks con huesos en silencio en cualquier archivo donde el ref-match era el
        ''' mapeo real, asi que se deja `Binding = Nothing` y se loguea en vez de adivinar.
        Dim sinBinding = r.Where(Function(a) a.Binding Is Nothing).ToList()
        If sueltos.Count = 1 AndAlso sinBinding.Count = 1 Then
            sinBinding(0).Binding = sueltos(0)
        ElseIf sueltos.Count > 0 Then
            Dim nb = sueltos.Count, na = sinBinding.Count
            Logger.LogLazy(Function() $"[HKX-ANIM] {nb} binding(s) no resolvieron su ref de animacion y {na} animacion(es) quedan sin binding; fallback posicional salteado (ambiguo).")
        End If

        Return r
    End Function

    Public Function ParseAnimation(source As HkxVirtualObjectGraph_Class) As HkxAnimacionDescomprimida_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaSplineCompressedAnimation", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' ⛔⛔ LOS DIECIOCHO OFFSETS SE DERIVABAN A MANO, sumando `BaseObjectFieldOffset`,
        ' `PointerSizeValue` y `ArrayHeaderSizeValue`, con un `AlignValue` en el medio. La reflexion
        ' declara la clase entera y el lector generado la resuelve por juego:
        '     hkaAnimation{ duration +0x14 . numberOfTransformTracks +0x18 . numberOfFloatTracks +0x1C
        '                   extractedMotion +0x20 . annotationTracks +0x28 }
        '     hkaSplineCompressedAnimation{ numFrames +0x38 . numBlocks +0x3C . maxFramesPerBlock +0x40
        '        . maskAndQuantizationSize +0x44 . frameDuration +0x50 . blockOffsets +0x58
        '        . floatBlockOffsets +0x68 . transformOffsets +0x78 . floatOffsets +0x88 . data +0x98 }
        Dim hkr As New Havok.Canon.Typed.Hk_HkaSplineCompressedAnimation(Me, source)
        If Not hkr.IsValid Then Return Nothing

        ' `maxFramesPerBlock` y `maskAndQuantizationSize` son de este parseo y no salen de aca:
        ' quedan como locales en vez de campos que nadie mas lee.
        Dim maxFramesPerBlock = hkr.MaxFramesPerBlock
        Dim maskAndQuantizationSize = hkr.MaskAndQuantizationSize

        Dim result As New HkxAnimacionDescomprimida_Class With {
            .Animacion = Havok.Canon.Objects.HkObj_HkaAnimation.Read(Me, source),
            .FrameDuration = hkr.FrameDuration,
            .NumFrames = hkr.NumFrames,
            .NumBlocks = hkr.NumBlocks
        }

        If result.NumFrames < 0 OrElse result.Animacion.NumberOfTransformTracks < 0 OrElse result.Animacion.NumberOfFloatTracks < 0 OrElse
           result.NumBlocks < 0 OrElse maxFramesPerBlock < 0 OrElse maskAndQuantizationSize < 0 Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} has invalid negative counts.")
        End If

        If result.NumBlocks > 0 AndAlso maxFramesPerBlock <= 0 Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} has invalid MaxFramesPerBlock={maxFramesPerBlock}.")
        End If

        ' ⛔⛔ LOS OFFSETS SALEN DEL OBJETO GENERADO, NO DEL LECTOR CRUDO.
        ' `hkr.BlockOffsetsItem(i)` devolvia la DIRECCION del elemento, no su valor, y esto lo
        ' sumaba como si fuera el dato: `blockOffsets(0)` daba 0xED0 donde el bloque arranca en 0.
        ' `HkObj_*.BlockOffsets` ya entrega la lista LEIDA — es la capa que existe para esto.
        Dim objAnim = Havok.Canon.Objects.HkObj_HkaSplineCompressedAnimation.Read(Me, source)
        Dim blockOffsets = If(objAnim?.BlockOffsets, New List(Of UInteger))
        Dim splineBlob = ReadByteArray(hkr.Data.FieldRelativeOffset)

        ' ⛔ `transformOffsets` LO DECLARA LA REFLEXION Y NADIE LO LEIA.
        Dim transformOffsets = If(objAnim?.TransformOffsets, New List(Of UInteger))

        If (result.NumFrames > 0 OrElse result.Animacion.NumberOfTransformTracks > 0 OrElse result.NumBlocks > 0) AndAlso (splineBlob.Length = 0 OrElse blockOffsets.Count = 0) Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} has no spline payload.")
        End If

        DecompressSplineAnimation(result, splineBlob,
                                                                  result.Animacion.NumberOfTransformTracks,
                                                                  result.NumFrames,
                                                                  result.NumBlocks,
                                                                  maxFramesPerBlock,
                                                                  blockOffsets, transformOffsets,
                                                                  maskAndQuantizationSize)

        Return result
    End Function

    Private Sub DecompressSplineAnimation(destino As HkxAnimacionDescomprimida_Class, blob As Byte(),
                                               numTracks As Integer,
                                               numFrames As Integer,
                                               numBlocks As Integer,
                                               maxFramesPerBlock As Integer,
                                               blockOffsets As IReadOnlyList(Of UInteger),
                                               transformOffsets As IReadOnlyList(Of UInteger),
                                               maskAndQuantSize As Integer)
        Dim totalTransformCount = CLng(numTracks) * CLng(numFrames)
        If totalTransformCount > Integer.MaxValue Then
            Throw New InvalidDataException($"Animation transform table is too large: {totalTransformCount} entries.")
        End If

        ' ⛔ EL FRAME EN LA FORMA CANONICA: un `hkQsTransform` de doce floats por track y frame.
        Dim totalTransforms = CInt(totalTransformCount)
        For index = 0 To totalTransforms - 1
            destino.TrackTransforms.Add(New Single() {0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F, 1.0F, 0.0F})
            destino.TrackMask.Add(0)
        Next

        If numTracks = 0 OrElse numFrames = 0 OrElse numBlocks = 0 Then Return
        If blockOffsets.Count < numBlocks Then Throw New InvalidDataException("Animation block offset array is truncated.")

        If maskAndQuantSize = 0 Then
            maskAndQuantSize = AlignValue(4 * numTracks, 4)
        End If

        Dim masks(numTracks - 1) As HkxSplineTrackMask_Struct
        Dim scalarControlPoints(2) As List(Of Single)
        For axis = 0 To 2
            scalarControlPoints(axis) = New List(Of Single)()
        Next

        Dim knots As New List(Of Single)
        Dim quaternionControlPoints As New List(Of Quaternion)

        For blockIndex = 0 To numBlocks - 1
            Dim blockStart = UInt32ToInt32(blockOffsets(blockIndex), "block offset")
            ' Los bloques spline COMPARTEN el frame de borde: el último frame de un bloque es el primero
            ' del siguiente, así que el stride entre bloques es (maxFramesPerBlock - 1), NO maxFramesPerBlock.
            ' Confirmado por (a) blockDuration = (maxFramesPerBlock-1)*frameDuration en los propios datos y
            ' (b) HavokLib (mapeo por tiempo con blockDuration/blockInverseDuration). Con stride=maxFPB un
            ' bloque NO-último escribe (firstFrame+maxFPB-1) que excede numFrames en animaciones multi-bloque
            ' → IndexOutOfRange (p.ej. Skyrim paired_dragonmount: 441 frames/15 bloques/32 maxFPB). FO4 no lo
            ' disparaba porque sus splines son de 1 bloque (firstFrame=0 con cualquier stride).
            Dim frameStride = Math.Max(1, maxFramesPerBlock - 1)
            Dim firstFrame = blockIndex * frameStride
            If firstFrame >= numFrames Then Continue For   ' bloque fuera de rango (datos inconsistentes)

            ' Un bloque escribe hasta maxFramesPerBlock frames, recortado a lo que reste hasta numFrames
            ' (el frame de borde compartido lo reescribe el bloque siguiente con el mismo valor).
            Dim framesInBlock = Math.Min(maxFramesPerBlock, numFrames - firstFrame)
            If framesInBlock <= 0 Then Continue For

            Dim offset = blockStart
            ' ⛔ EL FALLO TIENE QUE DECIR DONDE ESTABA. `blob offset 0x101F` solo no alcanza para
            ' saber si el problema es el bloque, el track o el tamano de la mascara.
            Dim ctx = Function() $"blk={blockIndex}/{numBlocks} start=0x{blockStart:X} mq={maskAndQuantSize} " &
                                $"tracks={numTracks} frames={framesInBlock}/{numFrames} blob={blob.Length} " &
                                $"maxFPB={maxFramesPerBlock} blkOff=[{String.Join(",", blockOffsets.Select(Function(x) "0x" & x.ToString("X")))}]"
            Try
                ' ⛔⛔ EL BLOQUE DE MASCARAS ES DE LARGO VARIABLE. Leido de `Fallout4.exe`
                ' (descompresor 0x1419B1780): hay UN byte de cuantizacion por track, y despues un
                ' byte de banderas por componente PERO SOLO SI su cuantizacion es una de las que
                ' declara el enum. Si no, el motor no lo consume:
                '     pos  ∉ {BITS8, BITS16} -> `jne 0x1419B1A22`, sin `inc rdi`
                '     rot  > UNCOMPRESSED    -> `ja  0x1419B1B95`, sin `inc rdi`
                '     esc  ∉ {BITS8, BITS16} -> `jne 0x1419B1C11`, sin `inc rdi`
                ' O sea entre 1 y 4 bytes por track. Esto leia CUATRO siempre, asi que en cuanto
                ' un track salteaba un byte, las banderas de todos los siguientes quedaban
                ' corridas. El corpus de FO4 trae el byte 0x07 (pos=3, salteado) en 2 de los 4
                ' tracks-bloque de su unica animacion spline: el defecto estaba VIVO.
            ' ⛔⛔ CUATRO BYTES POR TRACK — LO DICE EL ARCHIVO.
            ' `maskAndQuantizationSize` vale EXACTAMENTE `numTracks * 4` (medido: 332 con 83 tracks), y
            ' el hexdump del blob muestra el bloque en el offset 0 con grupos de cuatro:
            '     45 00 00 00 | 45 70 F0 00 | 45 00 04 00 | 45 07 F0 00 | ...
            ' Hubo una vuelta en que esto se leyo de largo VARIABLE, por el `inc rdi` condicional del
            ' motor (0x1419B1A1F / 0x1419B1B92 / 0x1419B1C0E). El archivo dice otra cosa y el archivo
            ' gana: ese `inc` no esta recorriendo este bloque.
            EnsureBlobReadable(blob, offset, 4 * numTracks, "Track mask block")
            For trackIndex = 0 To numTracks - 1
                Dim packedMask = blob(offset)
                masks(trackIndex).PosQuant = CByte(packedMask And &H3)
                masks(trackIndex).RotQuant = CByte((packedMask >> 2) And &HF)
                masks(trackIndex).ScaleQuant = CByte((packedMask >> 6) And &H3)
                masks(trackIndex).PosFlags = blob(offset + 1)
                masks(trackIndex).RotFlags = blob(offset + 2)
                masks(trackIndex).ScaleFlags = blob(offset + 3)
                Contar(destino.RotQuantUsados, CInt(masks(trackIndex).RotQuant))
                Contar(destino.PosQuantUsados, CInt(masks(trackIndex).PosQuant))
                Contar(destino.ScaleQuantUsados, CInt(masks(trackIndex).ScaleQuant))
                Contar(destino.MascaraCruda, CInt(packedMask))
                offset += 4
            Next

                offset = blockStart + maskAndQuantSize

                For trackIndex = 0 To numTracks - 1
                    Dim mask = masks(trackIndex)

                    Dim positionFrames = CreateVector3FrameArray(framesInBlock, 0.0F, 0.0F, 0.0F)
                    If mask.HasAnyPositionSpline() Then
                        EnsureBlobReadable(blob, offset, 3, "Position spline header")
                        Dim numItems = CInt(BitConverter.ToUInt16(blob, offset))
                        Dim degree = CInt(blob(offset + 2))
                        offset += 3

                        Dim knotCount = numItems + degree + 2
                        EnsureBlobReadable(blob, offset, knotCount, "Position knot array")
                        knots.Clear()
                        For knotIndex = 0 To knotCount - 1
                            knots.Add(CSng(blob(offset + knotIndex)))
                        Next
                        offset += knotCount
                        offset = AlignValue(offset, 4)

                        Dim axisInfos(2) As HkxSplineAxisInfo_Struct
                        For axis = 0 To 2
                            Dim axisType = mask.GetPositionType(axis)
                            axisInfos(axis).Type = axisType

                            Select Case axisType
                                Case HkxSplineTrackValueType_Enum.SplineValue
                                    EnsureBlobReadable(blob, offset, 8, "Position axis range")
                                    axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                    axisInfos(axis).MaxValue = BitConverter.ToSingle(blob, offset + 4)
                                    offset += 8
                                Case HkxSplineTrackValueType_Enum.StaticValue
                                    EnsureBlobReadable(blob, offset, 4, "Position axis static value")
                                    axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                    axisInfos(axis).MaxValue = axisInfos(axis).MinValue
                                    offset += 4
                                Case Else
                                    axisInfos(axis).MinValue = 0.0F
                                    axisInfos(axis).MaxValue = 0.0F
                            End Select
                        Next

                        For axis = 0 To 2
                            scalarControlPoints(axis).Clear()
                        Next

                        For itemIndex = 0 To numItems
                            For axis = 0 To 2
                                If axisInfos(axis).Type <> HkxSplineTrackValueType_Enum.SplineValue Then Continue For

                                If mask.PosQuant = 0 Then
                                    EnsureBlobReadable(blob, offset, 1, "Position 8-bit control point")
                                    scalarControlPoints(axis).Add(Read8BitScalar(blob(offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                    offset += 1
                                Else
                                    EnsureBlobReadable(blob, offset, 2, "Position 16-bit control point")
                                    scalarControlPoints(axis).Add(Read16BitScalar(BitConverter.ToUInt16(blob, offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                    offset += 2
                                End If
                            Next
                        Next

                        offset = AlignValue(offset, 4)

                        For frameInBlock = 0 To framesInBlock - 1
                            Dim time = CSng(frameInBlock)
                            Dim value As New Vector3

                            For axis = 0 To 2
                                Select Case axisInfos(axis).Type
                                    Case HkxSplineTrackValueType_Enum.SplineValue
                                        Dim span = FindKnotSpan(degree, time, scalarControlPoints(axis).Count, knots)
                                        SetVectorAxis(value, axis, EvalBSplineScalar(span, degree, time, knots, scalarControlPoints(axis)))
                                    Case HkxSplineTrackValueType_Enum.StaticValue
                                        SetVectorAxis(value, axis, axisInfos(axis).MinValue)
                                End Select
                            Next

                            positionFrames(frameInBlock) = value
                        Next
                    Else
                        Dim staticPosition As New Vector3
                        For axis = 0 To 2
                            If mask.GetPositionType(axis) <> HkxSplineTrackValueType_Enum.StaticValue Then Continue For
                            EnsureBlobReadable(blob, offset, 4, "Static position value")
                            SetVectorAxis(staticPosition, axis, BitConverter.ToSingle(blob, offset))
                            offset += 4
                        Next

                        For frameInBlock = 0 To framesInBlock - 1
                            positionFrames(frameInBlock) = staticPosition
                        Next
                    End If

                    offset = AlignValue(offset, 4)

                    Dim rotationFrames = CreateQuaternionFrameArray(framesInBlock, 0.0F, 0.0F, 0.0F, 1.0F)
                    Dim rotationType = mask.GetRotationType()
                    Dim quaternionFormat = CInt(mask.RotQuant)
                    Dim quaternionAlignment = GetQuaternionAlignment(quaternionFormat)

                    If rotationType = HkxSplineTrackValueType_Enum.SplineValue Then
                        EnsureBlobReadable(blob, offset, 3, "Rotation spline header")
                        Dim numItems = CInt(BitConverter.ToUInt16(blob, offset))
                        Dim degree = CInt(blob(offset + 2))
                        offset += 3

                        Dim knotCount = numItems + degree + 2
                        EnsureBlobReadable(blob, offset, knotCount, "Rotation knot array")
                        knots.Clear()
                        For knotIndex = 0 To knotCount - 1
                            knots.Add(CSng(blob(offset + knotIndex)))
                        Next
                        offset += knotCount
                        offset = AlignValue(offset, quaternionAlignment)

                        quaternionControlPoints.Clear()
                        For itemIndex = 0 To numItems
                            Dim consumed = 0
                            Dim quat = ReadQuaternion(quaternionFormat, blob, offset, blob.Length - offset, consumed)
                            offset += consumed

                            If quaternionControlPoints.Count > 0 AndAlso Quaternion.Dot(quat, quaternionControlPoints(quaternionControlPoints.Count - 1)) < 0.0F Then
                                quat = Quaternion.Negate(quat)
                        End If

                            quaternionControlPoints.Add(quat)
                        Next

                        For frameInBlock = 0 To framesInBlock - 1
                            Dim time = CSng(frameInBlock)
                            Dim span = FindKnotSpan(degree, time, quaternionControlPoints.Count, knots)
                            Dim quat = EvalBSplineQuaternion(span, degree, time, knots, quaternionControlPoints)
                            NormalizeQuaternion(quat)
                            rotationFrames(frameInBlock) = quat
                        Next
                    ElseIf rotationType = HkxSplineTrackValueType_Enum.StaticValue Then
                        offset = AlignValue(offset, quaternionAlignment)
                        Dim consumed = 0
                        Dim quat = ReadQuaternion(quaternionFormat, blob, offset, blob.Length - offset, consumed)
                        offset += consumed

                        For frameInBlock = 0 To framesInBlock - 1
                            rotationFrames(frameInBlock) = quat
                        Next
                    End If

                    offset = AlignValue(offset, 4)

                    Dim scaleFrames = CreateVector3FrameArray(framesInBlock, 1.0F, 1.0F, 1.0F)
                    If mask.HasAnyScaleSpline() Then
                        EnsureBlobReadable(blob, offset, 3, "Scale spline header")
                        Dim numItems = CInt(BitConverter.ToUInt16(blob, offset))
                        Dim degree = CInt(blob(offset + 2))
                        offset += 3

                        Dim knotCount = numItems + degree + 2
                        EnsureBlobReadable(blob, offset, knotCount, "Scale knot array")
                        knots.Clear()
                        For knotIndex = 0 To knotCount - 1
                            knots.Add(CSng(blob(offset + knotIndex)))
                        Next
                        offset += knotCount
                        offset = AlignValue(offset, 4)

                        Dim axisInfos(2) As HkxSplineAxisInfo_Struct
                        For axis = 0 To 2
                            Dim axisType = mask.GetScaleType(axis)
                            axisInfos(axis).Type = axisType

                            Select Case axisType
                                Case HkxSplineTrackValueType_Enum.SplineValue
                                    EnsureBlobReadable(blob, offset, 8, "Scale axis range")
                                    axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                    axisInfos(axis).MaxValue = BitConverter.ToSingle(blob, offset + 4)
                                    offset += 8
                                Case HkxSplineTrackValueType_Enum.StaticValue
                                    EnsureBlobReadable(blob, offset, 4, "Scale axis static value")
                                    axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                    axisInfos(axis).MaxValue = axisInfos(axis).MinValue
                                    offset += 4
                                Case Else
                                    axisInfos(axis).MinValue = 1.0F
                                    axisInfos(axis).MaxValue = 1.0F
                            End Select
                        Next

                        For axis = 0 To 2
                            scalarControlPoints(axis).Clear()
                        Next

                        For itemIndex = 0 To numItems
                            For axis = 0 To 2
                                If axisInfos(axis).Type <> HkxSplineTrackValueType_Enum.SplineValue Then Continue For

                                If mask.ScaleQuant = 0 Then
                                    EnsureBlobReadable(blob, offset, 1, "Scale 8-bit control point")
                                    scalarControlPoints(axis).Add(Read8BitScalar(blob(offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                    offset += 1
                                Else
                                    EnsureBlobReadable(blob, offset, 2, "Scale 16-bit control point")
                                    scalarControlPoints(axis).Add(Read16BitScalar(BitConverter.ToUInt16(blob, offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                    offset += 2
                                End If
                            Next
                        Next

                        offset = AlignValue(offset, 4)

                        For frameInBlock = 0 To framesInBlock - 1
                            Dim time = CSng(frameInBlock)
                            Dim value = Vector3.One

                            For axis = 0 To 2
                                Select Case axisInfos(axis).Type
                                    Case HkxSplineTrackValueType_Enum.SplineValue
                                        Dim span = FindKnotSpan(degree, time, scalarControlPoints(axis).Count, knots)
                                        SetVectorAxis(value, axis, EvalBSplineScalar(span, degree, time, knots, scalarControlPoints(axis)))
                                    Case HkxSplineTrackValueType_Enum.StaticValue
                                        SetVectorAxis(value, axis, axisInfos(axis).MinValue)
                                End Select
                            Next

                            scaleFrames(frameInBlock) = value
                        Next
                    Else
                        Dim staticScale = Vector3.One
                        For axis = 0 To 2
                            If mask.GetScaleType(axis) <> HkxSplineTrackValueType_Enum.StaticValue Then Continue For
                            EnsureBlobReadable(blob, offset, 4, "Static scale value")
                            SetVectorAxis(staticScale, axis, BitConverter.ToSingle(blob, offset))
                            offset += 4
                        Next

                        For frameInBlock = 0 To framesInBlock - 1
                            scaleFrames(frameInBlock) = staticScale
                        Next
                    End If

                    offset = AlignValue(offset, 4)

                    Dim msk = 0
                    If mask.GetPositionType(0) <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 1
                    If mask.GetPositionType(1) <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 2
                    If mask.GetPositionType(2) <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 4
                    If rotationType <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 8
                    If mask.GetScaleType(0) <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 16
                    If mask.GetScaleType(1) <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 32
                    If mask.GetScaleType(2) <> HkxSplineTrackValueType_Enum.Identity Then msk = msk Or 64

                    For frameInBlock = 0 To framesInBlock - 1
                        Dim destinationIndex = ((firstFrame + frameInBlock) * numTracks) + trackIndex
                        Dim pf = positionFrames(frameInBlock)
                        Dim rf = rotationFrames(frameInBlock)
                        Dim sf = scaleFrames(frameInBlock)
                        destino.TrackTransforms(destinationIndex) = New Single() {pf.X, pf.Y, pf.Z, 0.0F,
                                                                                 rf.X, rf.Y, rf.Z, rf.W,
                                                                                 sf.X, sf.Y, sf.Z, 0.0F}
                        destino.TrackMask(destinationIndex) = msk
                    Next
                Next
            Catch ex As Exception
                Throw New InvalidDataException($"{ex.Message}  [{ctx()}]", ex)
            End Try
        Next
    End Sub

    Private Shared Function CreateVector3FrameArray(count As Integer, x As Single, y As Single, z As Single) As Vector3()
        If count <= 0 Then Return Array.Empty(Of Vector3)()

        Dim result(count - 1) As Vector3
        For index = 0 To result.Length - 1
            result(index).X = x
            result(index).Y = y
            result(index).Z = z
        Next
        Return result
    End Function

    Private Shared Function CreateQuaternionFrameArray(count As Integer, x As Single, y As Single, z As Single, w As Single) As Quaternion()
        If count <= 0 Then Return Array.Empty(Of Quaternion)()

        Dim result(count - 1) As Quaternion
        For index = 0 To result.Length - 1
            result(index).X = x
            result(index).Y = y
            result(index).Z = z
            result(index).W = w
        Next
        Return result
    End Function

    Private Shared Function UInt32ToInt32(value As UInteger, fieldName As String) As Integer
        If value > Integer.MaxValue Then Throw New InvalidDataException($"{fieldName} exceeds Int32 range: 0x{value:X8}.")
        Return CInt(value)
    End Function

    Private Shared Sub EnsureBlobReadable(blob As Byte(), offset As Integer, byteCount As Integer, context As String)
        If offset < 0 OrElse byteCount < 0 OrElse offset > blob.Length OrElse byteCount > blob.Length - offset Then
            Throw New InvalidDataException($"{context} is truncated at blob offset 0x{Math.Max(offset, 0):X}.")
        End If
    End Sub

    ''' <summary>Un voto mas para ese valor de cuantizacion.</summary>
    Private Shared Sub Contar(d As Dictionary(Of Integer, Integer), v As Integer)
        Dim n = 0
        d.TryGetValue(v, n)
        d(v) = n + 1
    End Sub

    Private Shared Function AlignValue(offset As Integer, alignment As Integer) As Integer
        If alignment <= 1 Then Return offset
        Dim remainder = offset Mod alignment
        If remainder = 0 Then Return offset
        Return offset + (alignment - remainder)
    End Function

    Private Shared Function Read8BitScalar(value As Byte, minimum As Single, maximum As Single) As Single
        Return minimum + ((maximum - minimum) * (CSng(value) / 255.0F))
    End Function

    Private Shared Function Read16BitScalar(value As UShort, minimum As Single, maximum As Single) As Single
        Return minimum + ((maximum - minimum) * (CSng(value) / 65535.0F))
    End Function

    ''' <summary>
    ''' ⛔⛔ LOS FORMATOS DE ROTACION SALEN DE LA REFLEXION, NO DE NUMEROS SUELTOS.
    ''' <para>`hkaSplineCompressedAnimationTrackCompressionParams` declara el enum
    ''' `RotationQuantization`, y el ctor de `hkClass` lo pasa en `[rsp+0x30]` igual que a los
    ''' miembros — solo que el generador no lo miraba. Los dos ejecutables declaran los MISMOS
    ''' seis valores, y eso se comprueba aca abajo en vez de afirmarlo.</para>
    ''' <para>Antes esto era `Case 0 / 1 / 2 / 5` sin decir que era cada numero.</para>
    ''' </summary>
    Private Shared ReadOnly RotQ As IReadOnlyDictionary(Of String, Integer) = CargarEnum("RotationQuantization")

    ''' <summary>`ScalarQuantization` de la reflexion: `BITS8=0 . BITS16=1`. Es la que usan
    ''' la posicion y la escala, y el motor SALTEA el componente si el valor no es ninguno
    ''' de esos dos (`cmp ecx,1 / jne` en 0x1419B19B0 y 0x1419B1B9E).</summary>
    Private Shared ReadOnly EscQ As IReadOnlyDictionary(Of String, Integer) = CargarEnum("ScalarQuantization")

    ''' <summary>El valor mas alto que declara un enum. El motor compara contra el
    ''' (`cmp r15d,5 / ja` en 0x1419B1A26, y 5 es UNCOMPRESSED), asi que sale de la tabla.</summary>
    Private Shared Function MaximoDe(e As IReadOnlyDictionary(Of String, Integer)) As Integer
        Dim m = -1
        For Each kv In e
            If kv.Value > m Then m = kv.Value
        Next
        Return m
    End Function

    Private Shared Function CargarEnum(E As String) As IReadOnlyDictionary(Of String, Integer)
        Const C As String = "hkaSplineCompressedAnimationTrackCompressionParams"
        Dim a = Havok.Canon.HavokLayout.FO4.EnumValues(C, E)
        Dim b = Havok.Canon.HavokLayout.SSE.EnumValues(C, E)
        If a Is Nothing Then
            Throw New InvalidOperationException(
                $"La tabla de FO4 no declara {C}.{E}. Regenerar con Tools/HavokLayoutGen/gen.py.")
        End If
        ' El codec es UNO SOLO para los dos juegos: si las tablas discrepan, el formato
        ' cambio entre builds y hay que mirarlo, no elegir una.
        If b IsNot Nothing Then
            For Each kv In a
                Dim v As Integer
                If Not b.TryGetValue(kv.Key, v) OrElse v <> kv.Value Then
                    Throw New InvalidOperationException(
                        $"FO4 y SSE declaran {C}.{E}.{kv.Key} distinto: el codec asume que coinciden.")
                End If
            Next
        End If
        Return a
    End Function

    ''' <summary>Cuantos bytes ocupa cada formato en el flujo. NO lo declara la reflexion:
    ''' el enum dice QUE formatos hay, no cuanto miden. Los cuatro anchos salen del propio
    ''' nombre (32/40/48 bits, y UNCOMPRESSED = cuatro floats).</summary>
    Private Shared Function AnchoDe(nombre As String) As Integer
        Select Case nombre
            Case "POLAR32" : Return 4
            Case "THREECOMP40" : Return 5
            Case "THREECOMP48" : Return 6
            Case "THREECOMP24" : Return 3      ' `add qword ptr [rdi], 3` en 0x1419B3E1F
            Case "STRAIGHT16" : Return 2       ' `add qword ptr [rdi], 2` en 0x1419B3F99
            Case "UNCOMPRESSED" : Return 16
            Case Else : Return 0
        End Select
    End Function

    Private Shared Function ReadQuaternion(format As Integer, data As Byte(), offset As Integer, available As Integer, ByRef consumed As Integer) As Quaternion
        ' ⛔ EL HUECO, NOMBRADO. El binario declara SEIS formatos y aca se leen CUATRO:
        ' `THREECOMP24` (3) y `STRAIGHT16` (4) estan declarados y no tienen lector. Antes
        ' caian en un `Case Else` mudo que los leia como THREECOMP40 — o sea, basura
        ' silenciosa. Ahora se dice cual es y se sigue leyendo como antes, que es el
        ' comportamiento medido; si algun dia el corpus trae uno, el log lo va a decir.
        If format = RotQ("POLAR32") Then
            consumed = AnchoDe("POLAR32")
            Return Read32BitQuaternion(data, offset, available)
        ElseIf format = RotQ("THREECOMP40") Then
            consumed = AnchoDe("THREECOMP40")
            Return Read40BitQuaternion(data, offset, available)
        ElseIf format = RotQ("THREECOMP48") Then
            consumed = AnchoDe("THREECOMP48")
            Return Read48BitQuaternion(data, offset, available)
        ElseIf format = RotQ("THREECOMP24") Then
            consumed = AnchoDe("THREECOMP24")
            Return Read24BitQuaternion(data, offset, available)
        ElseIf format = RotQ("STRAIGHT16") Then
            consumed = AnchoDe("STRAIGHT16")
            Return Read16BitQuaternion(data, offset, available)
        ElseIf format = RotQ("UNCOMPRESSED") Then
            consumed = AnchoDe("UNCOMPRESSED")
            Return ReadUncompressedQuaternion(data, offset, available)
        End If

        If Logger.Enabled Then
            Dim nombre = RotQ.FirstOrDefault(Function(kv) kv.Value = format).Key
            Logger.LogLazy(Function() $"[ANIM-ROTQ] formato {format} ({If(nombre, "no declarado")}) sin lector: se lee como THREECOMP40")
        End If
        consumed = AnchoDe("THREECOMP40")
        Return Read40BitQuaternion(data, offset, available)
    End Function

    ''' <summary>
    ''' ⛔⛔ THREECOMP24 — TRES COMPONENTES DE 7 BITS EN 3 BYTES. Leido de `Fallout4.exe`:
    ''' el lector estatico es `0x141A474D0`, al que llega `0x1419B3CF0` desde el caso 3 de la
    ''' tabla de saltos `0x1419B1D3C` del descompresor `0x1419B1780`.
    ''' <para>Lo que hace el binario, instruccion por instruccion:</para>
    ''' <list type="bullet">
    ''' <item>`and ecx,0x7F` sobre cada uno de los tres bytes: tres componentes de 7 bits.</item>
    ''' <item>`or r11d, b0>>1` con `b1 And &amp;H80`, luego `shr r11d,6`: dos bits que dicen
    ''' QUE componente NO viene — el mayor, que se reconstruye.</item>
    ''' <item>El buffer se pre-carga con `[63,63,63,63]` (`movdqa` de `0x3F,0x3F,0x3F,0x3F`)
    ''' y los tres valores se escriben SALTEANDO ese carril; asi el que falta queda en 63 y
    ''' da cero al restar el sesgo.</item>
    ''' <item>`cvtdq2ps` / `subps` con 63.0 / `mulps` con `0.0112239169` — el paso de
    ''' cuantizacion, que es 1/89,0955. (En la MISMA tabla del motor esta `2894,89526`,
    ''' cuyo reciproco es `0.000345436`: el numero que este archivo ya usaba para
    ''' THREECOMP40 sin poder citarlo.)</item>
    ''' <item>`subps` de 1, `rsqrtps` y `cmpleps` contra cero: el componente que falta es
    ''' `sqrt(1 - suma)`, y CERO si la suma pasa de 1 (el `andnps` lo anula). El signo sale
    ''' del bit 7 del tercer byte (`test r9b,r9b` / `jns`).</item>
    ''' </list>
    ''' </summary>
    Private Shared Function Read24BitQuaternion(data As Byte(), offset As Integer, available As Integer) As Quaternion
        EnsureBlobReadable(data, offset, 3, "24-bit quaternion")

        Dim b0 = CInt(data(offset))
        Dim b1 = CInt(data(offset + 1))
        Dim b2 = CInt(data(offset + 2))

        ' El carril que NO viene: `((b1 And &H80) Or (b0 >> 1)) >> 6`, o sea el bit 7 de b1 y el
        ' bit 7 de b0 en ese orden.
        Dim faltante = (((b1 And &H80) Or (b0 >> 1)) >> 6) And 3

        ' Los cuatro carriles arrancan en el sesgo; los tres presentes lo pisan salteando el que falta.
        Dim carril() As Integer = {63, 63, 63, 63}
        Dim comps() As Integer = {b0 And &H7F, b1 And &H7F, b2 And &H7F}
        Dim k = 0
        For i = 0 To 3
            If i = faltante Then Continue For
            carril(i) = comps(k)
            k += 1
        Next

        Const Paso24 As Single = 0.0112239169F
        Dim v(3) As Single
        Dim suma = 0.0F
        For i = 0 To 3
            v(i) = (CSng(carril(i)) - 63.0F) * Paso24
            suma += v(i) * v(i)
        Next

        ' `subps` de 1 y `cmpleps` contra cero: si la suma pasa de 1, el que falta es CERO.
        Dim resto = 1.0F - suma
        Dim w = If(resto <= 0.0F, 0.0F, MathF.Sqrt(resto))
        If (b2 And &H80) <> 0 Then w = -w
        v(faltante) = w

        Dim result As New Quaternion With {.X = v(0), .Y = v(1), .Z = v(2), .W = v(3)}
        NormalizeQuaternion(result)
        Return result
    End Function

    ''' <summary>
    ''' ⛔⛔ STRAIGHT16 — CUATRO COMPONENTES DE 4 BITS EN 2 BYTES. Leido de `Fallout4.exe`:
    ''' el lector estatico es `0x141A47430`, al que llega `0x1419B3E60` desde el caso 4 de la
    ''' tabla `0x1419B1D3C`.
    ''' <para>A diferencia de los THREECOMP, aca NO hay componente reconstruido: vienen los
    ''' cuatro. El binario extrae `b0 And &amp;HF`, `b0 >> 4`, `b1 And &amp;HF`, `b1 >> 4` y los
    ''' entrelaza con dos `punpckldq` en ese orden (x, y, z, w). Despues `subps` con
    ''' `[7,7,7,7]` y `mulps` con `[0.142857149,…]`, que es 1/7 — o sea `(v - 7) / 7`.</para>
    ''' <para>El motor cierra con una vuelta de Newton-Raphson sobre `rsqrtps`
    ''' (`0.5*r*(3 - len2*r*r)`), que es normalizar aproximando. Aca se usa
    ''' `NormalizeQuaternion`, que normaliza EXACTO y ademas cubre el caso de magnitud ~0
    ''' (los cuatro carriles en 7) donde el `rsqrtps` del motor daria infinito.</para>
    ''' <para>El lector avanza DOS bytes y lee desde un origen alineado a 2
    ''' (`and rcx, &amp;HFFFFFFFFFFFFFFFE`).</para>
    ''' </summary>
    Private Shared Function Read16BitQuaternion(data As Byte(), offset As Integer, available As Integer) As Quaternion
        ' El origen se alinea a 2, como hace `0x1419B3F85`.
        Dim o = offset And Not 1
        EnsureBlobReadable(data, o, 2, "16-bit quaternion")

        Dim b0 = CInt(data(o))
        Dim b1 = CInt(data(o + 1))
        Dim carril() As Integer = {b0 And &HF, b0 >> 4, b1 And &HF, b1 >> 4}

        Const Sesgo16 As Single = 7.0F
        Const Paso16 As Single = 0.142857149F
        Dim result As New Quaternion With {
            .X = (CSng(carril(0)) - Sesgo16) * Paso16,
            .Y = (CSng(carril(1)) - Sesgo16) * Paso16,
            .Z = (CSng(carril(2)) - Sesgo16) * Paso16,
            .W = (CSng(carril(3)) - Sesgo16) * Paso16
        }
        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function Read32BitQuaternion(data As Byte(), offset As Integer, available As Integer) As Quaternion
        EnsureBlobReadable(data, offset, 4, "32-bit quaternion")

        Dim compressed = BitConverter.ToUInt32(data, offset)
        Dim radiusMask = (1UI << 10) - 1UI
        Dim radius = CSng((compressed >> 18) And radiusMask) / CSng(radiusMask)
        radius = 1.0F - (radius * radius)

        Dim phiTheta = CSng(compressed And &H3FFFFUI)
        Dim phi = MathF.Floor(MathF.Sqrt(phiTheta))
        Dim theta As Single = 0.0F
        If phi > 0.0F Then
            theta = CSng((Math.PI / 4.0) * ((phiTheta - (phi * phi)) / phi))
            phi = CSng((Math.PI / 2.0 / 511.0) * phi)
        End If

        Dim magnitude = MathF.Sqrt(Math.Max(0.0F, 1.0F - (radius * radius)))
        Dim result As New Quaternion With {
            .X = MathF.Sin(phi) * MathF.Cos(theta) * magnitude,
            .Y = MathF.Sin(phi) * MathF.Sin(theta) * magnitude,
            .Z = MathF.Cos(phi) * magnitude,
            .W = radius
        }

        Dim signMasks = {&H10000000UI, &H20000000UI, &H40000000UI, &H80000000UI}
        If (compressed And signMasks(0)) <> 0UI Then result.X = -result.X
        If (compressed And signMasks(1)) <> 0UI Then result.Y = -result.Y
        If (compressed And signMasks(2)) <> 0UI Then result.Z = -result.Z
        If (compressed And signMasks(3)) <> 0UI Then result.W = -result.W

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function Read40BitQuaternion(data As Byte(), offset As Integer, available As Integer) As Quaternion
        EnsureBlobReadable(data, offset, 5, "40-bit quaternion")

        Const Fractal As Single = 0.000345436F
        Dim raw As ULong = 0UL
        For byteIndex = 0 To 4
            raw = raw Or (CULng(data(offset + byteIndex)) << (byteIndex * 8))
        Next

        Dim a = CUInt(raw And &HFFFUL)
        Dim b = CUInt((raw >> 12) And &HFFFUL)
        Dim c = CUInt((raw >> 24) And &HFFFUL)
        ' Bias is the 11-bit mask (1<<11)-1 = 2047, per HavokLib hka_spline_decompressor.cpp
        ' (constexpr uint64 mask = (1 << 11) - 1; ... IVector4A16(tmpVal) - mask).
        Dim x = (CSng(a) - 2047.0F) * Fractal
        Dim y = (CSng(b) - 2047.0F) * Fractal
        Dim z = (CSng(c) - 2047.0F) * Fractal
        Dim w = MathF.Sqrt(Math.Max(0.0F, 1.0F - ((x * x) + (y * y) + (z * z))))
        If ((raw >> 38) And 1UL) <> 0UL Then w = -w

        Dim shift = CInt((raw >> 36) And 3UL)
        Dim result As New Quaternion
        Select Case shift
            Case 0
                result = New Quaternion With {.X = w, .Y = x, .Z = y, .W = z}
            Case 1
                result = New Quaternion With {.X = x, .Y = w, .Z = y, .W = z}
            Case 2
                result = New Quaternion With {.X = x, .Y = y, .Z = w, .W = z}
            Case Else
                result = New Quaternion With {.X = x, .Y = y, .Z = z, .W = w}
        End Select

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function Read48BitQuaternion(data As Byte(), offset As Integer, available As Integer) As Quaternion
        EnsureBlobReadable(data, offset, 6, "48-bit quaternion")

        Const Fractal As Single = 0.000043161F
        Dim mask = (1UI << 15) - 1UI
        Dim half = mask >> 1
        Dim xRaw = BitConverter.ToUInt16(data, offset)
        Dim yRaw = BitConverter.ToUInt16(data, offset + 2)
        Dim zRaw = BitConverter.ToUInt16(data, offset + 4)

        Dim shift = CInt((((CUInt(yRaw) >> 14) And 2UI) Or ((CUInt(xRaw) >> 15) And 1UI)))
        Dim radiusNegative = (CUInt(zRaw) >> 15) <> 0UI
        Dim x = (CSng(CUInt(xRaw) And mask) - CSng(half)) * Fractal
        Dim y = (CSng(CUInt(yRaw) And mask) - CSng(half)) * Fractal
        Dim z = (CSng(CUInt(zRaw) And mask) - CSng(half)) * Fractal
        Dim w = MathF.Sqrt(Math.Max(0.0F, 1.0F - ((x * x) + (y * y) + (z * z))))
        If radiusNegative Then w = -w

        Dim result As New Quaternion
        Select Case shift
            Case 0
                result = New Quaternion With {.X = w, .Y = x, .Z = y, .W = z}
            Case 1
                result = New Quaternion With {.X = x, .Y = w, .Z = y, .W = z}
            Case 2
                result = New Quaternion With {.X = x, .Y = y, .Z = w, .W = z}
            Case Else
                result = New Quaternion With {.X = x, .Y = y, .Z = z, .W = w}
        End Select

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function ReadUncompressedQuaternion(data As Byte(), offset As Integer, available As Integer) As Quaternion
        EnsureBlobReadable(data, offset, 16, "Uncompressed quaternion")

        Dim result As New Quaternion With {
            .X = BitConverter.ToSingle(data, offset),
            .Y = BitConverter.ToSingle(data, offset + 4),
            .Z = BitConverter.ToSingle(data, offset + 8),
            .W = BitConverter.ToSingle(data, offset + 12)
        }

        NormalizeQuaternion(result)
        Return result
    End Function

    ' ⛔ POR QUE ESTO NO ES `Quaternion.Normalize`. La de la BCL divide por la magnitud sin
    ' mirar: con un cuaternion de magnitud ~0 devuelve NaN y el NaN se propaga por toda la
    ' pose. Esta devuelve la IDENTIDAD en ese caso. No son la misma funcion, y la diferencia
    ' esta justo en el caso degenerado que un clip comprimido puede traer.
    Private Shared Sub NormalizeQuaternion(ByRef value As Quaternion)
        Dim magnitude = MathF.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z) + (value.W * value.W))
        If magnitude < 1.0E-10F Then
            value.X = 0.0F
            value.Y = 0.0F
            value.Z = 0.0F
            value.W = 1.0F
            Return
        End If

        Dim inverse = 1.0F / magnitude
        value.X *= inverse
        value.Y *= inverse
        value.Z *= inverse
        value.W *= inverse
    End Sub

    Private Shared Function GetQuaternionAlignment(format As Integer) As Integer
        Select Case format
            Case 0
                Return 4
            Case 1, 3
                Return 1
            Case 2, 4
                Return 2
            Case 5
                Return 4
            Case Else
                Return 1
        End Select
    End Function

    Private Shared Function FindKnotSpan(degree As Integer, value As Single, numControlPoints As Integer, knots As List(Of Single)) As Integer
        If numControlPoints <= 0 Then Return 0
        If value >= knots(numControlPoints) Then Return numControlPoints - 1

        Dim low = degree
        Dim high = numControlPoints
        Dim middle = (low + high) \ 2

        ' Algorithm A2.1 (The NURBS Book) converges in O(log n) steps on a valid,
        ' monotonically non-decreasing knot vector. The 100-iteration cap only ever
        ' trips on a malformed knot vector, which is an upstream stride/offset bug.
        ' Silently returning the unconverged 'middle' would mask that corruption and
        ' yield a subtly wrong pose, so throw instead of guessing a span.
        Dim converged = False
        For iteration = 0 To 99
            If value < knots(middle) Then
                high = middle
            ElseIf value >= knots(middle + 1) Then
                low = middle
            Else
                converged = True
                Exit For
            End If

            middle = (low + high) \ 2
        Next

        If Not converged Then
            Dim valLocal = value
            Dim degLocal = degree
            Dim cpLocal = numControlPoints
            Logger.LogLazy(Function() $"FindKnotSpan failed to converge for value={valLocal} degree={degLocal} numControlPoints={cpLocal}; knot vector is malformed (upstream stride/offset bug).")
            Throw New InvalidDataException($"FindKnotSpan did not converge after 100 iterations (value={valLocal}, degree={degLocal}, numControlPoints={cpLocal}); the knot vector is malformed, indicating an upstream offset/stride bug in spline decode.")
        End If

        Return middle
    End Function

    Private Shared Function EvalBSplineScalar(knotSpan As Integer,
                                              degree As Integer,
                                              time As Single,
                                              knots As List(Of Single),
                                              controlPoints As List(Of Single)) As Single
        If controlPoints.Count = 0 Then Return 0.0F
        If controlPoints.Count = 1 Then Return controlPoints(0)

        Dim basis(degree) As Single
        basis(0) = 1.0F

        For degreeIndex = 1 To degree
            For basisIndex = degreeIndex - 1 To 0 Step -1
                Dim denominator = knots(knotSpan + degreeIndex - basisIndex) - knots(knotSpan - basisIndex)
                Dim factor = If(denominator >= 1.0E-10F, (time - knots(knotSpan - basisIndex)) / denominator, 0.0F)
                Dim temp = basis(basisIndex) * factor
                If basisIndex + 1 < basis.Length Then basis(basisIndex + 1) += basis(basisIndex) - temp
                basis(basisIndex) = temp
            Next
        Next

        Dim result As Single = 0.0F
        For degreeIndex = 0 To degree
            Dim controlPointIndex = knotSpan - degreeIndex
            If controlPointIndex >= 0 AndAlso controlPointIndex < controlPoints.Count Then
                result += controlPoints(controlPointIndex) * basis(degreeIndex)
            End If
        Next

        Return result
    End Function

    Private Shared Function EvalBSplineQuaternion(knotSpan As Integer,
                                                  degree As Integer,
                                                  time As Single,
                                                  knots As List(Of Single),
                                                  controlPoints As List(Of Quaternion)) As Quaternion
        If controlPoints.Count = 0 Then Return Quaternion.Identity
        If controlPoints.Count = 1 Then Return controlPoints(0)

        Dim basis(degree) As Single
        basis(0) = 1.0F

        For degreeIndex = 1 To degree
            For basisIndex = degreeIndex - 1 To 0 Step -1
                Dim denominator = knots(knotSpan + degreeIndex - basisIndex) - knots(knotSpan - basisIndex)
                Dim factor = If(denominator >= 1.0E-10F, (time - knots(knotSpan - basisIndex)) / denominator, 0.0F)
                Dim temp = basis(basisIndex) * factor
                If basisIndex + 1 < basis.Length Then basis(basisIndex + 1) += basis(basisIndex) - temp
                basis(basisIndex) = temp
            Next
        Next

        Dim result As New Quaternion
        For degreeIndex = 0 To degree
            Dim controlPointIndex = knotSpan - degreeIndex
            If controlPointIndex < 0 OrElse controlPointIndex >= controlPoints.Count Then Continue For

            result.X += controlPoints(controlPointIndex).X * basis(degreeIndex)
            result.Y += controlPoints(controlPointIndex).Y * basis(degreeIndex)
            result.Z += controlPoints(controlPointIndex).Z * basis(degreeIndex)
            result.W += controlPoints(controlPointIndex).W * basis(degreeIndex)
        Next

        Return result
    End Function



    Private Shared Sub SetVectorAxis(ByRef value As Vector3, axis As Integer, component As Single)
        Select Case axis
            Case 0
                value.X = component
            Case 1
                value.Y = component
            Case Else
                value.Z = component
        End Select
    End Sub
    ''' <summary>
    ''' ── DESCOMPRESION LOSSLESS — venia de `HkxLosslessAnimationGraphParser.vb`, que se borro.
    ''' Eran las dos mitades de lo MISMO (descomprimir una animacion) repartidas en dos archivos,
    ''' y el unico miembro publico del segundo tenia CERO llamadores fuera del primero.
    ''' </summary>

    Private Enum LosslessTrackType_Enum
        Identity = 0
        StaticVal = 1
        Dynamic = 2
    End Enum

    ' Memoiza esqueletos parseados por RelativeOffset del origen, para que ParseSkeletonMapper no
    ' re-parsee el mismo esqueleto completo una vez por mapper. Es equivalente porque ParseSkeleton
    ' es función pura de su objeto de origen.
    Private ReadOnly _parsedSkeletonCache As New Dictionary(Of Integer, Havok.Canon.Objects.HkObj_HkaSkeleton)

    Private Function ParseSkeletonMemoized(source As HkxVirtualObjectGraph_Class) As Havok.Canon.Objects.HkObj_HkaSkeleton
        If IsNothing(source) Then Return Nothing
        Dim cached As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing
        If _parsedSkeletonCache.TryGetValue(source.RelativeOffset, cached) Then Return cached
        Dim parsed = Havok.Canon.Objects.HkObj_HkaSkeleton.Read(Me, source)
        _parsedSkeletonCache(source.RelativeOffset) = parsed
        Return parsed
    End Function

    ' hkArray slot size dentro del objeto (ptr + count(4) + capFlags(4)) = ArrayHeaderSizeValue.
    Private ReadOnly Property LosslessArraysBaseOffset As Integer
        Get
            ' = annotationTracksOffset + ArrayHeaderSizeValue (mismo cálculo de base que el parser spline).
            Dim baseField = BaseObjectFieldOffset
            Dim extractedMotionOffset = baseField + 16
            Dim annotationTracksOffset = extractedMotionOffset + PointerSizeValue
            Return annotationTracksOffset + ArrayHeaderSizeValue
        End Get
    End Property

    ''' <summary>Decodifica un hkaLosslessCompressedAnimation a TRS por (frame, track).</summary>
    Public Function ParseLosslessAnimation(source As HkxVirtualObjectGraph_Class) As HkxAnimacionDescomprimida_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaLosslessCompressedAnimation", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' ⛔⛔ TODO EL HEADER, DEL LECTOR GENERADO. Antes los once campos salian de ARITMETICA
        ' sobre dos constantes de la app (`BaseObjectFieldOffset`, `LosslessArraysBaseOffset`) mas el
        ' tamano de la cabecera de array: `rel + l + 7 * ahs`. Eso no es leer el archivo, es derivarlo
        ' — y una derivacion se rompe callada si el layout cambia entre juegos. La reflexion los
        ' declara por nombre:
        '     hkaAnimation{ duration +0x14 . numberOfTransformTracks +0x18 . numberOfFloatTracks +0x1C
        '                   annotationTracks +0x28 }
        '     hkaLosslessCompressedAnimation{ dynamicTranslations +0x38 . staticTranslations +0x48 .
        '        translationTypeAndOffsets +0x58 . dynamicRotations +0x68 . staticRotations +0x78 .
        '        rotationTypeAndOffsets +0x88 . dynamicScales +0x98 . staticScales +0xA8 .
        '        scaleTypeAndOffsets +0xB8 . floats +0xC8 . numFrames +0xD8 }
        ' ⛔ TODO POR EL OBJETO. Antes esto sacaba NUEVE cabeceras de array del lector tipado y
        ' despues las recorria a mano con el stride escrito aca (8 / 2 / 4 / 16). El objeto
        ' materializa los nueve arrays con el layout de la reflexion.
        Dim rel = source.RelativeOffset
        Dim lo = Havok.Canon.Objects.HkObj_HkaLosslessCompressedAnimation.Read(Me, source)
        If lo Is Nothing Then Return Nothing

        Dim duration = lo.Duration
        Dim numTransformTracks = lo.NumberOfTransformTracks
        Dim numFloatTracks = lo.NumberOfFloatTracks
        Dim numFrames = lo.NumFrames

        If numFrames < 0 OrElse numTransformTracks < 0 Then
            Throw New InvalidDataException($"hkaLosslessCompressedAnimation @0x{rel:X} has negative counts (numFrames={numFrames}, tracks={numTransformTracks}).")
        End If
        If CLng(numFrames) * CLng(numTransformTracks) > Integer.MaxValue Then
            Throw New InvalidDataException($"hkaLosslessCompressedAnimation @0x{rel:X} transform table is too large.")
        End If

        Dim result As New HkxAnimacionDescomprimida_Class With {
            .Animacion = Havok.Canon.Objects.HkObj_HkaAnimation.Read(Me, source),
            .FrameDuration = If(numFrames > 0, duration / numFrames, 1.0F / 30.0F),
            .NumFrames = numFrames
        }

        ' Nombres de hueso por track: el pose-import los usa para mapear track → hueso del NIF vivo.
        ' ⛔ `annotationTracks` sale del lector generado; antes su offset se derivaba sumando
        ' `baseField + 16 + PointerSizeValue`.

        ' Leer las tablas type+offset (una entrada por track).
        ' ⛔ LAS TABLAS type+offset, DEL OBJETO GENERADO. Cada `uint64` trae CUATRO uint16
        ' empaquetados — uno por componente — y eso si es decodificacion: la reflexion declara el
        ' array como `uint64`, no dice que adentro vengan cuatro.
        Dim transType = DesempaquetarCuatro(lo.TranslationTypeAndOffsets, numTransformTracks)
        Dim scaleType = DesempaquetarCuatro(lo.ScaleTypeAndOffsets, numTransformTracks)
        Dim rotType = If(lo.RotationTypeAndOffsets, New List(Of Integer)()).Take(numTransformTracks).ToList()

        ' Strides dinámicos por-frame = nº de componentes Dynamic sumados sobre todos los tracks.
        Dim transStride = SumDynamicComponents(transType)
        Dim scaleStride = SumDynamicComponents(scaleType)
        Dim rotStride = 0
        For Each rt In rotType
            If (rt And 3) = LosslessTrackType_Enum.Dynamic Then rotStride += 1
        Next

        ' Decodificar todos los frames × tracks.
        Dim totalTransforms = numFrames * numTransformTracks
        For i = 0 To totalTransforms - 1
            result.TrackTransforms.Add(Nothing)
            result.TrackMask.Add(0)
        Next

        For frame = 0 To numFrames - 1
            For track = 0 To numTransformTracks - 1
                Dim tT = If(track < transType.Count, transType(track), New Integer() {0, 0, 0, 0})
                Dim sT = If(track < scaleType.Count, scaleType(track), New Integer() {0, 0, 0, 0})
                Dim rT = If(track < rotType.Count, rotType(track), 0)

                Dim tx = ResolveScalar(tT(0), lo.StaticTranslations, lo.DynamicTranslations, 0.0F, transStride, frame)
                Dim ty = ResolveScalar(tT(1), lo.StaticTranslations, lo.DynamicTranslations, 0.0F, transStride, frame)
                Dim tz = ResolveScalar(tT(2), lo.StaticTranslations, lo.DynamicTranslations, 0.0F, transStride, frame)

                Dim sx = ResolveScalar(sT(0), lo.StaticScales, lo.DynamicScales, 1.0F, scaleStride, frame)
                Dim sy = ResolveScalar(sT(1), lo.StaticScales, lo.DynamicScales, 1.0F, scaleStride, frame)
                Dim sz = ResolveScalar(sT(2), lo.StaticScales, lo.DynamicScales, 1.0F, scaleStride, frame)

                Dim q = ResolveQuaternion(rT, lo.StaticRotations, lo.DynamicRotations, rotStride, frame)

                Dim iPlano = (frame * numTransformTracks) + track
                result.TrackTransforms(iPlano) = New Single() {tx, ty, tz, 0.0F, q(0), q(1), q(2), q(3), sx, sy, sz, 0.0F}
                Dim msk = 0
                If (tT(0) And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 1
                If (tT(1) And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 2
                If (tT(2) And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 4
                If (rT And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 8
                If (sT(0) And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 16
                If (sT(1) And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 32
                If (sT(2) And 3) <> LosslessTrackType_Enum.Identity Then msk = msk Or 64
                result.TrackMask(iPlano) = msk
            Next
        Next

        Return result
    End Function

    ''' <summary>
    ''' ⛔ EL DESEMPAQUETADO, QUE ES LO UNICO QUE LA REFLEXION NO DICE.
    ''' `translationTypeAndOffsets` y `scaleTypeAndOffsets` son `array&lt;uint64&gt;`: la tabla
    ''' declara el array y su tipo, pero no que cada elemento traiga CUATRO uint16 — uno por
    ''' componente (x, y, z, w) — con los dos bits bajos como tipo y el resto como indice.
    ''' </summary>
    Private Shared Function DesempaquetarCuatro(tabla As List(Of Long), cuantos As Integer) As List(Of Integer())
        Dim r As New List(Of Integer())
        If tabla Is Nothing Then Return r
        For i = 0 To Math.Min(cuantos, tabla.Count) - 1
            Dim v = tabla(i)
            r.Add(New Integer() {CInt(v And &HFFFFL), CInt((v >> 16) And &HFFFFL),
                                 CInt((v >> 32) And &HFFFFL), CInt((v >> 48) And &HFFFFL)})
        Next
        Return r
    End Function

    ''' <summary>Un componente escalar: los dos bits bajos dicen si es identidad, estatico o
    ''' por-frame, y el resto es el indice dentro de la lista que corresponda.</summary>
    Private Shared Function ResolveScalar(indexType As Integer, estaticos As List(Of Single),
                                          dinamicos As List(Of Single), identidad As Single,
                                          stride As Integer, frame As Integer) As Single
        Dim ttype = indexType And 3
        Dim index = indexType >> 2
        Select Case ttype
            Case LosslessTrackType_Enum.StaticVal
                If estaticos Is Nothing OrElse index < 0 OrElse index >= estaticos.Count Then Return identidad
                Return estaticos(index)
            Case LosslessTrackType_Enum.Dynamic
                If dinamicos Is Nothing Then Return identidad
                Dim k = index + (frame * stride)
                If k < 0 OrElse k >= dinamicos.Count Then Return identidad
                Return dinamicos(k)
            Case Else
                Return identidad
        End Select
    End Function

    ''' <summary>Idem para el cuaternion, que el objeto entrega como `Single()` de cuatro.</summary>
    Private Shared Function ResolveQuaternion(indexType As Integer, estaticos As List(Of Single()),
                                              dinamicos As List(Of Single()), stride As Integer,
                                              frame As Integer) As Single()
        Dim identidad = New Single() {0.0F, 0.0F, 0.0F, 1.0F}
        Dim ttype = indexType And 3
        Dim index = indexType >> 2
        Dim q As Single() = Nothing
        Select Case ttype
            Case LosslessTrackType_Enum.StaticVal
                If estaticos IsNot Nothing AndAlso index >= 0 AndAlso index < estaticos.Count Then q = estaticos(index)
            Case LosslessTrackType_Enum.Dynamic
                If dinamicos IsNot Nothing Then
                    Dim k = index + (frame * stride)
                    If k >= 0 AndAlso k < dinamicos.Count Then q = dinamicos(k)
                End If
        End Select
        If q Is Nothing OrElse q.Length < 4 Then Return identidad
        Return q
    End Function

    Private Shared Function SumDynamicComponents(typeTable As List(Of Integer())) As Integer
        Dim total = 0
        For Each entry In typeTable
            For c = 0 To 3
                If (entry(c) And 3) = LosslessTrackType_Enum.Dynamic Then total += 1
            Next
        Next
        Return total
    End Function

    Private Shared Function BoneName(bones As List(Of Havok.Canon.Objects.HkObj_HkaBone), index As Short) As String
        If IsNothing(bones) OrElse index < 0 OrElse index >= bones.Count Then Return ""
        Return bones(index).Name
    End Function


End Class

''' <summary>
''' ⛔ UNA ANIMACION YA DESCOMPRIMIDA A FRAMES.
'''
''' <para>No es el calco de una clase: la alimentan DOS — `hkaSplineCompressedAnimation` y
''' `hkaLosslessCompressedAnimation` — y lo que tienen en comun lo declara la base `hkaAnimation`,
''' que viaja entera en <see cref="Animacion"/>. De ahi salen `duration`, `numberOfTransformTracks`,
''' `numberOfFloatTracks` y `annotationTracks`, sin copiarlos.</para>
'''
''' <para>Los campos propios son los que NO salen de un solo campo declarado: `NumFrames` (lo
''' declara cada clase concreta, no la base), `FrameDuration` (spline lo declara; lossless lo deriva
''' de `duration / numFrames`), `NumBlocks` (spline; en lossless no existe) y los frames
''' descomprimidos, que son todo el trabajo.</para>
''' </summary>
Public Class HkxAnimacionDescomprimida_Class
    Public Property Animacion As Havok.Canon.Objects.HkObj_HkaAnimation
    ''' <summary>`numFrames` de la clase concreta.</summary>
    Public Property NumFrames As Integer
    ''' <summary>Spline lo declara; lossless lo deriva de `duration / numFrames`.</summary>
    Public Property FrameDuration As Single
    ''' <summary>`numBlocks` del spline. Cero cuando la fuente es lossless, que no los tiene.</summary>
    Public Property NumBlocks As Integer
    ''' <summary>
    ''' ⛔ LOS FRAMES DESCOMPRIMIDOS, EN LA FORMA CANONICA DEL `hkQsTransform`.
    ''' <para>Doce floats por frame y por track: `translation 0..3 · rotation 4..7 · scale 8..11`.
    ''' Es la MISMA forma que devuelve `hkaSkeleton.referencePose` y la que consume
    ''' `HkxTransformConventionHelper`, asi que el frame de un clip y la pose de reposo se leen
    ''' igual. Antes esto era un tipo a mano con `Translation`/`Rotation`/`Scale` por separado:
    ''' una segunda forma del mismo dato.</para>
    ''' <para>Indice: `frame * numTransformTracks + track`.</para>
    ''' </summary>
    Public ReadOnly Property TrackTransforms As New List(Of Single())

    ''' <summary>Que componentes ANIMA el clip en ese frame, en bits:
    ''' `1 TX · 2 TY · 4 TZ · 8 R · 16 SX · 32 SY · 64 SZ`. Los que no estan animados hay que
    ''' tomarlos del reposo del rig, no del frame. Mismo indice que `TrackTransforms`.</summary>
    Public ReadOnly Property TrackMask As New List(Of Integer)

    ''' <summary>Que valores de `RotationQuantization` usa este clip, y cuantos tracks-bloque
    ''' con cada uno. Sale del nibble `(packedMask >> 2) And &HF` de la mascara por track.
    ''' El binario declara SEIS (POLAR32..UNCOMPRESSED); esto dice cuales trae el archivo.</summary>
    Public ReadOnly Property RotQuantUsados As New Dictionary(Of Integer, Integer)

    ''' <summary>Idem para `ScalarQuantization` de posicion y escala (2 bits cada uno).</summary>
    Public ReadOnly Property PosQuantUsados As New Dictionary(Of Integer, Integer)
    Public ReadOnly Property ScaleQuantUsados As New Dictionary(Of Integer, Integer)

    ''' <summary>El BYTE CRUDO de la mascara por track, tal cual lo trae el bloque. Se guarda
    ''' entero a proposito: cuantos bits ocupa cada campo adentro es lo que hay que MEDIR, y
    ''' partirlo antes de medirlo seria presuponer la respuesta.</summary>
    Public ReadOnly Property MascaraCruda As New Dictionary(Of Integer, Integer)
    Public Property Binding As Havok.Canon.Objects.HkObj_HkaAnimationBinding

    ''' <summary>`hkaAnimation.annotationTracks[i].trackName`. Los dos parsers lo leian con un
    ''' stride escrito a mano; el objeto generado lo declara.</summary>
    Public ReadOnly Property TrackNames As List(Of String)
        Get
            Dim r As New List(Of String)
            If Animacion Is Nothing OrElse Animacion.AnnotationTracks Is Nothing Then Return r
            For Each t In Animacion.AnnotationTracks
                r.Add(If(t?.TrackName, ""))
            Next
            Return r
        End Get
    End Property

    ''' <summary>El `hkQsTransform` de un frame y un track: doce floats, o Nothing.</summary>
    Public Function GetTransform(frameIndex As Integer, trackIndex As Integer) As Single()
        Dim i = IndicePlano(frameIndex, trackIndex)
        If i < 0 OrElse i >= TrackTransforms.Count Then Return Nothing
        Return TrackTransforms(i)
    End Function

    ''' <summary>La mascara de componentes animados de ese frame y track. Cero si no hay.</summary>
    Public Function GetMask(frameIndex As Integer, trackIndex As Integer) As Integer
        Dim i = IndicePlano(frameIndex, trackIndex)
        If i < 0 OrElse i >= TrackMask.Count Then Return 0
        Return TrackMask(i)
    End Function

    Private Function IndicePlano(frameIndex As Integer, trackIndex As Integer) As Integer
        Dim nT = If(Animacion Is Nothing, 0, Animacion.NumberOfTransformTracks)
        If frameIndex < 0 OrElse trackIndex < 0 OrElse nT <= 0 Then Return -1
        If frameIndex >= NumFrames OrElse trackIndex >= nT Then Return -1
        Return (frameIndex * nT) + trackIndex
    End Function
End Class
