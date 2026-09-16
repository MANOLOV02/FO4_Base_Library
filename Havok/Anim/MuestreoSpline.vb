Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Runtime.CompilerServices

' =================================================================================================
' ⛔⛔⛔ EL MUESTREADOR DE `hkaSplineCompressedAnimation` DE `Fallout4.exe`, TRANSCRIPTO ENTERO.
'
'   0x1419B1750  sampleTracks          (vtbl+0x20 de 0x1426CCF68) → 0x1419B1780 con todas las pistas
'   0x1419B1780  samplePartialTracks   tiempo → frame → bloque → recorrido de pistas y floats
'   0x141A46B20  puntero al bloque     data + blockOffsets[bloque] + (desplazamiento & 0x7FFFFFFF)
'   0x1419B2C30  nudos                 cabecera (u16 n, u8 grado), búsqueda del tramo, nudos · frameDuration
'   0x1419B2F50 / 0x1419B2FB0 / 0x1419B30B0   evaluación B-spline de grado 1 / 2 / 3
'   0x1419B32C0 / 0x1419B3670   vector cuantizado u16 / u8 (estático, mín/máx, puntos de control)
'   0x1419B4A40 / 0x1419B4AB0   traslación u16 / u8     0x1419B4980 / 0x1419B49E0   escala u16 / u8
'   0x1419B4680 / 0x1419B4710   float track u16 / u8
'   rotación, tabla de saltos 0x1419B1D3C (formato 0..5):
'     0 POLAR32      0x1419B4930 → 0x1419B4160 → 0x1419B45E0 → 0x141A475F0
'     1 THREECOMP40  0x1419B47A0 → 0x1419B3A00 → 0x1419B42E0 → 0x141A47910
'     2 THREECOMP48  0x1419B47F0 → 0x1419B3B70 → 0x1419B4370 → 0x141A47B40
'     3 THREECOMP24  0x1419B4840 → 0x1419B3CF0 → 0x1419B4410 → 0x141A474D0
'     4 STRAIGHT16   0x1419B4890 → 0x1419B3E60 → 0x1419B44A0 → 0x141A47430
'     5 UNCOMPRESSED 0x1419B48E0 → 0x1419B3FE0 → 0x1419B4540 → 0x141A47C60
'   (nombres del enum: `hkaSplineCompressedAnimationTrackCompressionParams.RotationQuantization`,
'    tabla de reflexión de HavokLayout_FO4; el orden de la tabla de saltos es el del enum)
'
' ⛔ ARITMÉTICA: simple precisión, las MISMAS operaciones y en el MISMO orden que el `.exe`. Las cuatro
' lanes de un `mulps`/`addps` son independientes, así que donde el motor opera las cuatro lanes con la
' misma cuenta se transcribe por lane (bit a bit idéntico).
' ⛔ `rsqrtps` (5 sitios: 0x141A47A6D, 0x141A47C14, 0x141A4759B, 0x141A474A0, 0x141A478A1) y `rcpps`
' (12 sitios, decodificados uno por uno: 0x1419B180E, 0x1419B2F86, 0x1419B3008/3032/304E, 0x1419B3159/3186/31A4/31C4/31E5/323B,
' 0x141A476F8) se evalúan EXACTOS (`1/√x`, `1/x`): es la decisión de `Havok/Motor/Simd.vb` (no `Sse.*`)
' y la semántica con la que se emula el `.exe` (unicorn = QEMU). Contra la CPU real difiere en los
' mismos sitios; eso lo mide el diferencial de CPU aparte, no este archivo.
' ⛔ Lo que el `.exe` NO escribe (grado fuera de 1..3, cuantización inválida) queda como estaba en el
' buffer del llamador, igual que en el motor: por eso `PoseAtTime` escribe sobre buffers ajenos.
' ⛔ No es seguro entre hilos: reutiliza los buffers de nudos y puntos de control (en el `.exe` son pila).
' =================================================================================================

''' <summary>Cuatro lanes de un registro `xmm`.</summary>
Friend Structure LanesSpline
    Friend L0 As Single
    Friend L1 As Single
    Friend L2 As Single
    Friend L3 As Single

    Friend Sub New(a As Single, b As Single, c As Single, d As Single)
        L0 = a : L1 = b : L2 = c : L3 = d
    End Sub
End Structure

''' <summary>Muestreador exacto de un `hkaSplineCompressedAnimation` (transcripción de 0x1419B1750).</summary>
Public NotInheritable Class HkaSplineMuestreador

    ' --------------------------------------------------------------------------------- campos del objeto
    Private ReadOnly _duracion As Single          ' hkaAnimation.duration +0x14 (0x1419B17F4)
    Private ReadOnly _nT As Integer               ' numberOfTransformTracks +0x18 (0x1419B1762)
    Private ReadOnly _nF As Integer               ' numberOfFloatTracks +0x1C (0x1419B1754)
    Private ReadOnly _numFrames As Integer        ' numFrames +0x38 (vtbl+0x48 = 0x1419B24F0)
    Private ReadOnly _numBlocks As Integer        ' numBlocks +0x3C (0x1419B18C9)
    Private ReadOnly _mfpb As Integer             ' maxFramesPerBlock +0x40 (0x1419B18C3)
    Private ReadOnly _mq As Integer               ' maskAndQuantizationSize +0x44 (0x1419B1913)
    Private ReadOnly _blockInv As Single          ' blockInverseDuration +0x4C (0x1419B192A)
    Private ReadOnly _frameDur As Single          ' frameDuration +0x50 (0x1419B1922, 0x1419B4A63)
    Private ReadOnly _blockOffsets As UInteger()  ' blockOffsets +0x58 (0x1419B18DF)
    Private ReadOnly _floatBlockOffsets As UInteger() ' floatBlockOffsets +0x68 (0x1419B1C49)
    Private ReadOnly _d As Byte()                 ' data +0x98 (0x1419B18EF)

    ' --------------------------------------------------------------------------------- pila del .exe
    Private ReadOnly _nudos(519) As Single        ' [rsp+0x40] de los helpers de 0x1419B2C30
    Private ReadOnly _puntos(256) As LanesSpline  ' [rsp+0x50]/[rsp+0x60] de 0x1419B32C0 / 0x1419B42E0

    ' --------------------------------------------------------------------------------- constantes
    Private Shared ReadOnly K1Sobre65535 As Single = BitConverter.UInt32BitsToSingle(&H37800080UI) ' 0x1424AE3E0
    Private Shared ReadOnly K1Sobre255 As Single = BitConverter.UInt32BitsToSingle(&H3B808081UI)   ' 0x142F3C6F0
    Private Shared ReadOnly K40 As Single = BitConverter.UInt32BitsToSingle(&H39B51B97UI)          ' 0x14271B618
    Private Shared ReadOnly K48 As Single = BitConverter.UInt32BitsToSingle(&H383507C7UI)          ' 0x14271B610
    Private Shared ReadOnly K24 As Single = BitConverter.UInt32BitsToSingle(&H3C37E485UI)          ' 0x14271B620
    Private Shared ReadOnly K16 As Single = BitConverter.UInt32BitsToSingle(&H3E124925UI)          ' 0x142F3C6A0
    Private Shared ReadOnly KPolarE As Single = BitConverter.UInt32BitsToSingle(&H3A802008UI)      ' 0x14262DD10
    Private Shared ReadOnly KPolarR As Single = BitConverter.UInt32BitsToSingle(&H3B004020UI)      ' 0x14271B61C
    Private Shared ReadOnly KMedioPi As Single = BitConverter.UInt32BitsToSingle(&H3FC90FDBUI)     ' 0x142F3C860
    Private Shared ReadOnly KCuatroSobrePi As Single = BitConverter.UInt32BitsToSingle(&H3FA2F983UI) ' 0x1426294B0
    Private Shared ReadOnly KPi4a As Single = BitConverter.UInt32BitsToSingle(&HBF490000UI)        ' 0x1426294C0
    Private Shared ReadOnly KPi4b As Single = BitConverter.UInt32BitsToSingle(&HB97DA000UI)        ' 0x1426294D0
    Private Shared ReadOnly KPi4c As Single = BitConverter.UInt32BitsToSingle(&HB3222169UI)        ' 0x1426294E0
    Private Shared ReadOnly KCos1 As Single = BitConverter.UInt32BitsToSingle(&H37CCF5CEUI)        ' 0x142629440
    Private Shared ReadOnly KCos2 As Single = BitConverter.UInt32BitsToSingle(&HBAB6061AUI)        ' 0x142629450
    Private Shared ReadOnly KCos3 As Single = BitConverter.UInt32BitsToSingle(&H3D2AAAA5UI)        ' 0x142629460
    Private Shared ReadOnly KSen1 As Single = BitConverter.UInt32BitsToSingle(&HB94CA1F9UI)        ' 0x142629410
    Private Shared ReadOnly KSen2 As Single = BitConverter.UInt32BitsToSingle(&H3C08839EUI)        ' 0x142629420
    Private Shared ReadOnly KSen3 As Single = BitConverter.UInt32BitsToSingle(&HBE2AAAA3UI)        ' 0x142629430
    Private Const KCasiMax As UInteger = &H7F7FFFEEUI                                               ' 0x1424684A0
    Private Const K2a23 As Single = 8388608.0F                                                      ' 0x14270FC60
    ' Tamaño y alineación del paquete por formato: `add qword ptr [rdi], N` de la rama estática
    ' (0x1419B429A 4, 0x1419B3B2F 5, 0x1419B3CA9 6, 0x1419B3E1F 3, 0x1419B3F99 2, 0x1419B411A 16) y
    ' `and rcx, ~3 / ~1 / nada` (0x1419B4286, 0x1419B3C95, 0x1419B3B27, 0x1419B3F85, 0x1419B4106, 0x1419B3E17).
    Private Shared ReadOnly TamanoRot As Integer() = {4, 5, 6, 3, 2, 16}
    Private Shared ReadOnly AlineacionRot As Integer() = {4, 1, 2, 1, 2, 4}

    ''' <param name="numFrames">`numFrames` (+0x38): lo devuelve vtbl+0x48 (0x1419B24F0 `mov eax,[rcx+0x38]`).</param>
    Public Sub New(duration As Single, numberOfTransformTracks As Integer, numberOfFloatTracks As Integer,
                   numFrames As Integer, numBlocks As Integer, maxFramesPerBlock As Integer,
                   maskAndQuantizationSize As Integer, blockInverseDuration As Single, frameDuration As Single,
                   blockOffsets As UInteger(), floatBlockOffsets As UInteger(), data As Byte())
        _duracion = duration
        _nT = numberOfTransformTracks
        _nF = numberOfFloatTracks
        _numFrames = numFrames
        _numBlocks = numBlocks
        _mfpb = maxFramesPerBlock
        _mq = maskAndQuantizationSize
        _blockInv = blockInverseDuration
        _frameDur = frameDuration
        _blockOffsets = If(blockOffsets, Array.Empty(Of UInteger)())
        _floatBlockOffsets = If(floatBlockOffsets, Array.Empty(Of UInteger)())
        ' ⛔ 64 bytes de cola en cero: el .exe lee de a 4/8 bytes (0x1419B2CC2 movss, 0x141A47914 movsd)
        ' y descarta lo que excede el paquete; acá sólo evita leer fuera del arreglo.
        Dim src = If(data, Array.Empty(Of Byte)())
        _d = New Byte(src.Length + 63) {}
        Buffer.BlockCopy(src, 0, _d, 0, src.Length)
    End Sub

    ''' <summary>Los campos del objeto leído por la reflexión (mismos offsets que usa el `.exe`).</summary>
    Public Shared Function Desde(hkr As Havok.Canon.Objects.HkObj_HkaSplineCompressedAnimation, data As Byte()) As HkaSplineMuestreador
        If hkr Is Nothing Then Return Nothing
        Return New HkaSplineMuestreador(hkr.Duration, hkr.NumberOfTransformTracks, hkr.NumberOfFloatTracks,
                                        hkr.NumFrames, hkr.NumBlocks, hkr.MaxFramesPerBlock, hkr.MaskAndQuantizationSize,
                                        hkr.BlockInverseDuration, hkr.FrameDuration,
                                        If(hkr.BlockOffsets, New List(Of UInteger)).ToArray(),
                                        If(hkr.FloatBlockOffsets, New List(Of UInteger)).ToArray(), data)
    End Function

    Public ReadOnly Property TransformTracks As Integer
        Get
            Return _nT
        End Get
    End Property

    Public ReadOnly Property FloatTracks As Integer
        Get
            Return _nF
        End Get
    End Property

    ''' <summary>
    ''' `sampleTracks` (0x1419B1750): muestrea TODAS las pistas en el tiempo <paramref name="t"/> sobre los
    ''' buffers del llamador. <paramref name="transforms"/> = 12 floats por pista (`hkQsTransform`:
    ''' traslación xyzw, rotación xyzw, escala xyzw, las 12 lanes tal cual las escribe el `.exe`);
    ''' <paramref name="floats"/> = un float por float track. Devuelve el bloque que se leyó (-1 si el
    ''' objeto no tiene ese bloque: ahí el `.exe` leería memoria ajena y no se replica).
    ''' </summary>
    Public Function PoseAtTime(t As Single, transforms As Single(), floats As Single()) As Integer
        ' 0x1419B1754 mov eax,[rcx+0x1c] ; 0x1419B1762 mov r8d,[rcx+0x18] ; 0x1419B176A call [rdx+0x28]
        Return Muestrear(t, _nT, transforms, _nF, floats)
    End Function

    ''' <summary>Igual, con buffers nuevos en cero.</summary>
    Public Function PoseAtTime(t As Single) As (Transforms As Single(), Floats As Single(), Bloque As Integer)
        Dim tr(Math.Max(0, 12 * _nT) - 1) As Single
        Dim fl(Math.Max(0, _nF) - 1) As Single
        Dim b = PoseAtTime(t, tr, fl)
        Return (tr, fl, b)
    End Function

    ''' <summary>
    ''' Qué componentes DECLARA la pista en ese bloque, con los bits de <see cref="HkxPoseImportSession.BitDeTrack"/>
    ''' — la misma lectura que hace el parser por frame (`HkxAnimationGraphParser.DecompressSplineAnimation`):
    ''' 4 bytes por pista en `data + blockOffsets[bloque]`, estático (bit eje) o spline (bit eje+4).
    ''' </summary>
    Public Function MascaraDeContenido(bloque As Integer, track As Integer) As Integer
        If bloque < 0 OrElse bloque >= _blockOffsets.Length OrElse track < 0 OrElse track >= _nT Then Return 0
        Dim o = CLng(_blockOffsets(bloque)) + 4L * track
        If o + 3 >= _d.Length Then Return 0
        Dim pm = CInt(_d(CInt(o + 1))), rm = CInt(_d(CInt(o + 2))), sm = CInt(_d(CInt(o + 3)))
        Dim msk = 0
        For eje = 0 To 2
            If ((pm >> eje) And &H11) <> 0 Then msk = msk Or (1 << eje)
            If ((sm >> eje) And &H11) <> 0 Then msk = msk Or (16 << eje)
        Next
        If (rm And &HFF) <> 0 Then msk = msk Or 8
        Return msk
    End Function

    ' ================================================================================= 0x1419B1780
    Private Function Muestrear(t As Single, maxTransform As Integer, salida As Single(), maxFloat As Integer, floats As Single()) As Integer
        Dim nf As Integer = _numFrames                                         ' 0x1419B17F1 call [rax+0x48] → [rcx+0x38]
        Dim dur As Single = _duracion                                          ' 0x1419B17F4 movss xmm4,[rsi+0x14] ; 0x1419B1806 shufps
        Dim inv As Single = Rcp(dur)                                           ' 0x1419B180E rcpps xmm1,xmm4
        Dim nf1 As Integer = S32(CLng(nf) - 1L)                                ' 0x1419B1811 dec eax
        Dim x0 As Single = dur * inv                                           ' 0x1419B1813 movaps ; 0x1419B181A mulps xmm0,xmm1
        Dim x5 As Single = CSng(nf1)                                           ' 0x1419B1820 cvtsi2ss xmm5,eax ; 0x1419B1835 shufps
        Dim nf2 As UInteger = U32(CLng(nf) - 2L)                               ' 0x1419B1824 dec eax
        Dim x2 As Single = 2.0F - x0                                           ' 0x1419B17FC movaps xmm2,[0x142629500] ; 0x1419B1826 subps
        Dim positivo As UInteger = If(0.0F < dur, &HFFFFFFFFUI, 0UI)           ' 0x1419B1832 xorps ; 0x1419B1839 cmpltps xmm0,xmm4
        x2 = x2 * inv                                                          ' 0x1419B183D mulps xmm2,xmm1
        x2 = x2 * t                                                            ' 0x1419B180A movss xmm3,xmm6 ; 0x1419B1840 mulps xmm2,xmm3
        x5 = x5 * x2                                                           ' 0x1419B184A mulps xmm5,xmm2
        x5 = Sng(Bits(x5) And positivo)                                        ' 0x1419B184D andps xmm5,xmm0
        Dim absx As Single = Sng(Bits(x5) And &H7FFFFFFFUI)                    ' 0x1419B1853 movdqa ; 0x1419B185A pslld 1 ; 0x1419B185F psrld 1
        Dim rnd As Single = x5 - K2a23                                         ' 0x1419B1843 movaps xmm3,[0x14270FC60] ; 0x1419B1857 subps
        rnd = rnd + K2a23                                                      ' 0x1419B1864 addps xmm2,xmm3
        rnd = rnd + K2a23                                                      ' 0x1419B1867 addps xmm2,xmm3
        rnd = rnd - K2a23                                                      ' 0x1419B186A subps xmm2,xmm3
        Dim grande As Boolean = K2a23 < absx                                   ' 0x1419B186D cmpltps xmm3,xmm0
        Dim menosUno As Single = If(x5 < rnd, -1.0F, 0.0F)                     ' 0x1419B187A cmpltps xmm0,xmm2 ; 0x1419B187E cvtdq2ps
        Dim suma As Single = menosUno + rnd                                    ' 0x1419B1881 addps xmm1,xmm2
        Dim piso As Single = If(grande, x5, suma)                              ' 0x1419B1877 andps ; 0x1419B1884 andnps ; 0x1419B1887 orps
        Dim fi As UInteger = U32(Cvtt(piso))                                   ' 0x1419B188A cvttps2dq xmm0,xmm4 ; 0x1419B188E movd r10d,xmm0
        Dim delta As Single = 0.0F                                             ' 0x1419B181D xorps xmm7,xmm7
        If fi > nf2 Then                                                       ' 0x1419B1893 cmp r10d,eax ; 0x1419B1896 jbe (sin signo)
            delta = 1.0F                                                       ' 0x1419B1898 movss xmm7,[0x142929458]
            fi = nf2                                                           ' 0x1419B18A0 mov r10d,eax
        Else
            Dim dd As Single = x5 - piso                                       ' 0x1419B18A5 subps xmm5,xmm4
            dd = If(dd < 1.0F, dd, 1.0F)                                       ' 0x1419B18AB cmpltps [0x142F3C560] ; 0x1419B18B3 andps ; 0x1419B18B6 andnps ; 0x1419B18BD orps
            delta = If(0.0F > dd, 0.0F, dd)                                    ' 0x1419B18C0 maxps xmm7,xmm5
        End If
        Dim mfpb1 As UInteger = U32(CLng(_mfpb) - 1L)                          ' 0x1419B18C3 mov r9d,[rsi+0x40] ; 0x1419B18D8 lea r8d,[r9-1]
        Dim cociente As UInteger = fi \ mfpb1                                  ' 0x1419B18C7 xor edx,edx ; 0x1419B18DC div r8d
        Dim bloque As Integer = 0                                              ' 0x1419B18D2 xor r12d,r12d ; 0x1419B18D5 mov ecx,r12d
        If S32(cociente) > 0 Then bloque = S32(cociente)                       ' 0x1419B18E3 test eax,eax ; 0x1419B18E5 cmovg ecx,eax
        Dim ultimo As Integer = S32(CLng(_numBlocks) - 1L)                     ' 0x1419B18C9 mov edi,[rsi+0x3c] ; 0x1419B18E8 dec edi
        If bloque < ultimo Then ultimo = bloque                                ' 0x1419B18EA cmp ecx,edi ; 0x1419B18EC cmovl edi,ecx
        bloque = ultimo                                                        ' 0x1419B18FA mov [rsp+0xa8],edi
        Dim local As UInteger = U32(CLng(fi) - CLng(U32(CLng(mfpb1) * CLng(U32(bloque))))) ' 0x1419B18F6 imul r8d,edi ; 0x1419B1901 sub r10d,r8d
        Dim bt As Single = CSng(CLng(local))                                   ' 0x1419B1907 mov eax,r10d ; 0x1419B190A cvtsi2ss xmm6,rax
        bt = bt + delta                                                        ' 0x1419B191E addss xmm6,xmm7
        bt = bt * _frameDur                                                    ' 0x1419B1922 mulss xmm6,[rsi+0x50]
        Dim qf As Single = bt * _blockInv                                      ' 0x1419B1927 movaps ; 0x1419B192A mulss xmm1,[rsi+0x4c]
        qf = qf * CSng(S32(mfpb1))                                             ' 0x1419B1917 movd ; 0x1419B191B cvtdq2ps ; 0x1419B192F mulss xmm1,xmm0
        Dim qt As Integer = Cvtt(qf) And &HFF                                  ' 0x1419B1933 cvttss2si ebp,xmm1 ; movzx r8d,bpl en cada llamada

        ' 0x141A46B20 (data, blockOffsets, bloque, desplazamiento): 0x141A46B68 mov ecx,[rsi+rdi*4] ;
        ' 0x141A46B6D btr eax,0x1f ; 0x141A46B71/0x141A46B74 add. La guarda 0x141A46B35 (byte 0x143DBD2C2,
        ' la verificación de la licencia de Havok en 0x141A77080) es verdadera en el juego distribuido.
        If bloque < 0 OrElse bloque >= _blockOffsets.Length Then Return -1    ' (fuera del arreglo: el .exe lee memoria ajena)
        Dim baseBloque As Long = _blockOffsets(bloque)
        Dim ptr As Long = baseBloque + (CLng(_mq) And &H7FFFFFFFL)            ' 0x1419B1913 mov r9d,[rsi+0x44] ; 0x1419B1937 call 0x141A46B20
        Dim mk As Long = baseBloque + (&H80000000L And &H7FFFFFFFL)           ' 0x1419B1940 mov r9d,0x80000000 ; 0x1419B195B call 0x141A46B20

        If maxTransform <> 0 Then                                              ' 0x1419B1978 test r14d,r14d ; 0x1419B197B je
            For tr As Integer = 0 To maxTransform - 1                          ' 0x1419B1975 mov ebx,r12d ; 0x1419B1C11 inc ebx ; 0x1419B1C1A cmp ebx,r12d ; jb
                Dim tipo As Integer = _d(CInt(mk))                             ' 0x1419B1990 movzx ecx,byte ptr [rdi]
                mk += 1                                                        ' 0x1419B1993 inc rdi
                Dim fmtRot As Integer = (tipo >> 2) And &HF                    ' 0x1419B1996 mov r15d,ecx ; 0x1419B199C shr r15d,2 ; 0x1419B19A0 and r15d,0xf
                Dim qEsc As Integer = tipo >> 6                                ' 0x1419B1999 mov r14d,ecx ; 0x1419B19A4 shr r14d,6
                Dim qPos As Integer = tipo And 3                               ' 0x1419B19A8 and ecx,3
                Dim b As Integer = 12 * tr                                     ' 0x1419B19BF lea rcx,[rax+rax*2] ; 0x1419B19C3 shl rcx,4 (48 bytes)
                If qPos = 0 OrElse qPos = 1 Then                               ' 0x1419B19AB je ; 0x1419B19AD cmp ecx,1 ; 0x1419B19B0 jne
                    PistaVector(_d(CInt(mk)), qt, bt, ptr, salida, b, False, If(qPos = 1, 2, 1)) ' 0x1419B19B2 movzx r9d,[rdi] ; 0x1419B19DF call 0x1419B4A40 | 0x1419B1A13 call 0x1419B4AB0
                    mk += 1                                                    ' 0x1419B1A1F inc rdi
                End If
                If fmtRot <= 5 Then                                            ' 0x1419B1A22 cmp r15d,5 ; 0x1419B1A26 ja
                    PistaRotacion(fmtRot, _d(CInt(mk)), qt, bt, ptr, salida, b + 4) ' 0x1419B1A2F tabla 0x1419B1D3C ; r9 = [rdi] ; lea rax,[r13+0x10]
                    mk += 1                                                    ' 0x1419B1B92 inc rdi
                End If
                If qEsc = 0 OrElse qEsc = 1 Then                               ' 0x1419B1B95 test r14d,r14d ; 0x1419B1B9A cmp r14d,1 ; jne
                    PistaVector(_d(CInt(mk)), qt, bt, ptr, salida, b + 8, True, If(qEsc = 1, 2, 1)) ' 0x1419B1BD1 call 0x1419B4980 | 0x1419B1C09 call 0x1419B49E0 (lea rax,[r13+0x20])
                    mk += 1                                                    ' 0x1419B1C0E inc rdi
                End If
            Next
        End If

        ' ---- float tracks (0x1419B1C2B-0x1419B1CEF)
        Dim bFloat As Long = _blockOffsets(bloque)                             ' 0x1419B1C2B movsxd rcx,[rsp+0xa8] ; 0x1419B1C46 mov edx,[rax+rcx*4]
        If bloque >= _floatBlockOffsets.Length AndAlso maxFloat <> 0 Then Return -1 ' (fuera del arreglo: el .exe lee memoria ajena)
        Dim fptr As Long = If(maxFloat <> 0, CLng(_floatBlockOffsets(bloque)) + bFloat, 0L) ' 0x1419B1C49 mov rax,[rsi+0x68] ; 0x1419B1C4D mov ecx,[rax+rcx*4] ; 0x1419B1C53 add rcx,rdx ; 0x1419B1C59 add rcx,r8
        Dim fm As Long = CLng(S32(CLng(_nT) << 2)) + bFloat                   ' 0x1419B1C50 mov eax,[rsi+0x18] ; 0x1419B1C56 shl eax,2 ; 0x1419B1C5C movsxd ; 0x1419B1C5F add ; 0x1419B1C6A add
        If maxFloat <> 0 Then                                                  ' 0x1419B1C6D test r14d,r14d ; 0x1419B1C70 je
            For k As Integer = 0 To maxFloat - 1                               ' 0x1419B1CE9 inc r12d ; 0x1419B1CEC cmp r12d,r14d ; jb
                Dim al As Integer = _d(CInt(fm))                               ' 0x1419B1C80 movzx eax,byte ptr [rbx]
                fm += 1                                                        ' 0x1419B1C83 lea rbx,[rbx+1]
                Dim m As Integer = al And &HF9                                 ' 0x1419B1C87 movzx r9d,al ; 0x1419B1C8D and r9b,0xf9
                Dim q As Integer = (al >> 1) And 3                             ' 0x1419B1C8B mov ecx,eax ; 0x1419B1C91 shr ecx,1 ; 0x1419B1C93 and ecx,3
                If q = 0 OrElse q = 1 Then                                     ' 0x1419B1C96 je ; 0x1419B1C98 cmp ecx,1 ; 0x1419B1C9B jne
                    floats(k) = PistaFloat(m, qt, bt, fptr, If(q = 1, 2, 1))   ' 0x1419B1CBD call 0x1419B4680 | 0x1419B1CE4 call 0x1419B4710 ([rdi+r12*4])
                End If
            Next
        End If
        Return bloque
    End Function

    ' ================================================================================= envoltorios
    ''' <summary>0x1419B4A40 / 0x1419B4AB0 (traslación u16 / u8) y 0x1419B4980 / 0x1419B49E0 (escala).</summary>
    Private Sub PistaVector(m As Integer, qt As Integer, bt As Single, ByRef ptr As Long, salida As Single(), b As Integer, escala As Boolean, ancho As Integer)
        If m = 0 Then                                                          ' 0x1419B4A59 test r9b,r9b ; jne (0x1419B4990 en la escala)
            Dim v As Single = If(escala, 1.0F, 0.0F)                           ' 0x1419B4A5E movups [rax],xmm0 (cero) | 0x1419B4995 movaps xmm0,[0x142F3C560]
            salida(b) = v : salida(b + 1) = v : salida(b + 2) = v : salida(b + 3) = v ' 0x1419B4A5E / 0x1419B499C movups [rax],xmm0
        Else
            Dim porDefecto As Single = If(escala, 1.0F, 0.0F)                  ' 0x1419B4A85 movaps [rsp+0x40],xmm0 (cero) | 0x1419B49AE lea rax,[0x142F3C560]
            Dim o As New LanesSpline(salida(b), salida(b + 1), salida(b + 2), salida(b + 3)) ' 0x1419B4A6B mov [rsp+0x38],rax (la salida)
            Dim r = Vector(ptr, qt, bt, m, New LanesSpline(porDefecto, porDefecto, porDefecto, porDefecto), o, ancho) ' 0x1419B4A63 movss xmm3,[rcx+0x50] ; 0x1419B4A8A call 0x1419B32C0 | 0x1419B4AFA call 0x1419B3670
            salida(b) = r.L0 : salida(b + 1) = r.L1 : salida(b + 2) = r.L2 : salida(b + 3) = r.L3
        End If
        ptr = (ptr + 3L) And Not 3L                                            ' 0x1419B4A8F mov rax,[rbx] ; 0x1419B4A92 add rax,3 ; 0x1419B4A96 and rax,~3
    End Sub

    ''' <summary>0x1419B4680 (u16) / 0x1419B4710 (u8): el float track es la lane 0 del vector.</summary>
    Private Function PistaFloat(m As Integer, qt As Integer, bt As Single, ByRef ptr As Long, ancho As Integer) As Single
        Dim v As Single = 0.0F                                                 ' 0x1419B469B mov dword ptr [rax],0
        If m <> 0 Then                                                         ' 0x1419B468E test r9b,r9b ; 0x1419B4691 jne
            Dim cero As New LanesSpline(0.0F, 0.0F, 0.0F, 0.0F)                ' 0x1419B46B2 xorps ; 0x1419B46BA movaps [rsp+0x50] ; 0x1419B46D2 movaps [rsp+0x40]
            v = Vector(ptr, qt, bt, m, cero, cero, ancho).L0                   ' 0x1419B46D7 call 0x1419B32C0 | 0x1419B4767 call 0x1419B3670 ; 0x1419B46E4 movaps ; 0x1419B46E9 movss [rax],xmm0
        End If
        ptr = (ptr + 3L) And Not 3L                                            ' 0x1419B46ED mov rax,[rbx] ; 0x1419B46F0 add rax,3 ; 0x1419B46F4 and rax,~3
        Return v
    End Function

    ''' <summary>0x1419B4930 / 0x1419B47A0 / 0x1419B47F0 / 0x1419B4840 / 0x1419B4890 / 0x1419B48E0.</summary>
    Private Sub PistaRotacion(fmt As Integer, m As Integer, qt As Integer, bt As Single, ByRef ptr As Long, salida As Single(), b As Integer)
        Dim o As New LanesSpline(salida(b), salida(b + 1), salida(b + 2), salida(b + 3)) ' 0x1419B4936 mov rax,[rsp+0x78] (la salida)
        Dim r = Rotacion(fmt, ptr, qt, bt, m, o)                               ' 0x1419B4940 movss xmm3,[rcx+0x50] ; 0x1419B4958 call 0x1419B4160 (idem 0x1419B47C8/4818/4868/48B8/4908)
        salida(b) = r.L0 : salida(b + 1) = r.L1 : salida(b + 2) = r.L2 : salida(b + 3) = r.L3
        ptr = (ptr + 3L) And Not 3L                                            ' 0x1419B495D mov rax,[rbx] ; 0x1419B4960 add rax,3 ; 0x1419B4964 and rax,~3
    End Sub

    ' ================================================================================= 0x1419B2C30
    ''' <summary>Cabecera, tramo y nudos. Deja los 2·grado nudos en `_nudos` y avanza el cursor.</summary>
    Private Sub Nudos(ByRef ptr As Long, qt As Integer, ByRef n As Integer, ByRef grado As Integer, ByRef tramo As Integer)
        n = CInt(_d(CInt(ptr))) Or (CInt(_d(CInt(ptr + 1))) << 8)             ' 0x1419B2C4C mov rax,[rcx] ; 0x1419B2C58 movzx r10d,word ptr [rax]
        Dim k0 As Long = ptr + 3L                                              ' 0x1419B2C5C lea r11,[rax+2] ; 0x1419B2C6A inc r11
        grado = _d(CInt(ptr + 2))                                              ' 0x1419B2C66 movzx eax,byte ptr [r11] ; 0x1419B2C70 mov edi,eax
        Dim sp As Integer                                                      ' r8d
        If qt >= _d(CInt(k0 + n + 1)) Then                                     ' 0x1419B2C72 lea ebx,[r10+1] ; 0x1419B2C7A cmp r9b,[rbx+r11] ; 0x1419B2C7E jb
            sp = n                                                             ' 0x1419B2C84 mov r8d,r10d
        ElseIf qt <= _d(CInt(k0)) Then                                         ' 0x1419B2D69 cmp r9b,[r11] ; 0x1419B2D6C ja
            sp = grado                                                         ' 0x1419B2D6E mov r8d,edi
        Else
            Dim alto As Integer = n + 1                                        ' 0x1419B2C72 lea ebx,[r10+1]
            Dim bajo As Integer = grado                                        ' 0x1419B2D7A mov r10d,edi
            Dim medio As Integer = CInt((CUInt(alto) + CUInt(grado)) >> 1)     ' 0x1419B2D76 lea r8d,[rbx+rax] ; 0x1419B2D7D shr r8d,1
            Do
                Dim c As Integer = _d(CInt(k0 + medio))                        ' 0x1419B2D80 movsxd rax,r8d ; 0x1419B2D83 movzx ecx,[rax+r11]
                If qt >= c AndAlso qt < _d(CInt(k0 + medio + 1)) Then          ' 0x1419B2D88 cmp r9b,cl ; jb ; 0x1419B2D8D cmp r9b,[rax+r11+1] ; 0x1419B2D92 jb
                    sp = medio                                                 ' (0x1419B2C87 con r8d = medio)
                    Exit Do
                End If
                If qt >= c Then                                                ' 0x1419B2D98 cmp r9b,cl
                    bajo = medio                                               ' 0x1419B2DA1 cmovae r10d,r8d (alto queda: 0x1419B2D9E cmovae eax,ebx)
                Else
                    alto = medio                                               ' 0x1419B2D9B mov eax,r8d ; 0x1419B2DA5 mov ebx,eax
                End If
                medio = S32(CLng(alto) + CLng(bajo)) \ 2                       ' 0x1419B2DA7 add eax,r10d ; 0x1419B2DAA cdq ; 0x1419B2DAB sub eax,edx ; 0x1419B2DAD sar eax,1
            Loop
        End If
        Dim desde As Long = k0 + CLng(S32(CLng(sp) - grado + 1))               ' 0x1419B2CA4 mov eax,r8d ; 0x1419B2CAA sub eax,edi ; 0x1419B2CAF inc eax ; 0x1419B2CB6 movsxd
        For i As Integer = 0 To 2 * grado - 1                                  ' 0x1419B2C8D lea r10d,[rdi+rdi] (de a 4: 0x1419B2CC2-0x1419B2CE7 ; resto: 0x1419B2D10-0x1419B2D36)
            _nudos(i) = CSng(CInt(_d(CInt(desde + i)))) * _frameDur            ' 0x1419B2CCC punpcklbw ; 0x1419B2CD8 cvtdq2ps ; 0x1419B2CDB mulps xmm0,xmm2 | 0x1419B2D21 cvtsi2ss ; 0x1419B2D29 mulps (xmm2 = [rsp+0x48] = frameDuration)
        Next
        ptr = k0 + grado + n + 2L                                              ' 0x1419B2D3D lea edx,[rdi+r13] ; 0x1419B2D44 add rdx,r11 ; 0x1419B2D5D mov [r15],rdx
        tramo = sp                                                             ' 0x1419B2D4C mov eax,r8d
    End Sub

    ' ================================================================================= evaluación
    ''' <summary>0x1419B2F50 — grado 1, una lane.</summary>
    Private Shared Function Grado1(t As Single, k As Single(), p0 As Single, p1 As Single) As Single
        Dim x1 As Single = t - k(0)                                            ' 0x1419B2F6B shufps xmm0,xmm3,0 ; 0x1419B2F72 subps xmm1,xmm0
        Dim x3 As Single = k(1) - t                                            ' 0x1419B2F75 shufps xmm3,xmm3,0x55 ; 0x1419B2F79 subps xmm3,xmm2
        x3 = x3 + x1                                                           ' 0x1419B2F83 addps xmm3,xmm1
        Dim r As Single = Rcp(x3)                                              ' 0x1419B2F86 rcpps xmm0,xmm3
        x3 = x3 * r                                                            ' 0x1419B2F89 mulps xmm3,xmm0
        Dim x2 As Single = 2.0F - x3                                           ' 0x1419B2F7C movaps xmm2,[0x142629500] ; 0x1419B2F8C subps xmm2,xmm3
        x2 = x2 * r                                                            ' 0x1419B2F8F mulps xmm2,xmm0
        x2 = x2 * x1                                                           ' 0x1419B2F92 mulps xmm2,xmm1
        Dim d As Single = p1 - p0                                              ' 0x1419B2F95 movups xmm1,[r9+0x10] ; 0x1419B2F9A subps xmm1,[r9]
        d = d * x2                                                             ' 0x1419B2F9E mulps xmm1,xmm2
        Return d + p0                                                          ' 0x1419B2FA1 addps xmm1,[r9] ; 0x1419B2FA5 movups [rax],xmm1
    End Function

    ''' <summary>0x1419B2FB0 — grado 2, una lane.</summary>
    Private Shared Function Grado2(t As Single, k As Single(), p0 As Single, p1 As Single, p2 As Single) As Single
        Dim x6 As Single = k(2) - t                                            ' 0x1419B2FE8 shufps xmm6,xmm8,0xaa ; 0x1419B2FF6 subps xmm6,xmm2
        Dim x7 As Single = t - k(1)                                            ' 0x1419B2FF1 shufps xmm0,xmm8,0x55 ; 0x1419B2FFF subps xmm7,xmm0
        Dim x1 As Single = x6 + x7                                             ' 0x1419B3002 movaps ; 0x1419B3005 addps xmm1,xmm7
        Dim r As Single = Rcp(x1)                                              ' 0x1419B3008 rcpps xmm0,xmm1
        x1 = x1 * r                                                            ' 0x1419B300B mulps xmm1,xmm0
        Dim x3 As Single = 2.0F - x1                                           ' 0x1419B2FD4 movaps xmm3,xmm5 ; 0x1419B300E subps xmm3,xmm1
        x3 = x3 * r                                                            ' 0x1419B3011 mulps xmm3,xmm0
        Dim x4 As Single = t - k(0)                                            ' 0x1419B3018 shufps xmm0,xmm8,0 ; 0x1419B301D subps xmm4,xmm0
        Dim x8 As Single = k(3) - t                                            ' 0x1419B3020 shufps xmm8,xmm8,0xff ; 0x1419B3025 subps xmm8,xmm2
        x1 = x4 + x6                                                           ' 0x1419B302C movaps xmm1,xmm4 ; 0x1419B302F addps xmm1,xmm6
        r = Rcp(x1)                                                            ' 0x1419B3032 rcpps xmm0,xmm1
        x1 = x1 * r                                                            ' 0x1419B3035 mulps xmm1,xmm0
        Dim x2 As Single = 2.0F - x1                                           ' 0x1419B3029 movaps xmm2,xmm5 ; 0x1419B3038 subps xmm2,xmm1
        x1 = x8 + x7                                                           ' 0x1419B303B movaps xmm1,xmm8 ; 0x1419B303F addps xmm1,xmm7
        x2 = x2 * r                                                            ' 0x1419B3042 mulps xmm2,xmm0
        Dim x0 As Single = x6 * x3                                             ' 0x1419B3045 movaps xmm0,xmm6 ; 0x1419B3048 mulps xmm0,xmm3
        x2 = x2 * x0                                                           ' 0x1419B304B mulps xmm2,xmm0
        r = Rcp(x1)                                                            ' 0x1419B304E rcpps xmm0,xmm1
        x6 = x6 * x2                                                           ' 0x1419B3051 mulps xmm6,xmm2
        x1 = x1 * r                                                            ' 0x1419B3054 mulps xmm1,xmm0
        x4 = x4 * x2                                                           ' 0x1419B3057 mulps xmm4,xmm2
        x6 = x6 * p0                                                           ' 0x1419B305A mulps xmm6,[r9]
        Dim x5 As Single = 2.0F - x1                                           ' 0x1419B305E subps xmm5,xmm1
        x5 = x5 * r                                                            ' 0x1419B3061 mulps xmm5,xmm0
        x0 = x7 * x3                                                           ' 0x1419B3064 movaps xmm0,xmm7 ; 0x1419B3067 mulps xmm0,xmm3
        x5 = x5 * x0                                                           ' 0x1419B306A mulps xmm5,xmm0
        x8 = x8 * x5                                                           ' 0x1419B306D mulps xmm8,xmm5
        x7 = x7 * x5                                                           ' 0x1419B3071 mulps xmm7,xmm5
        x8 = x8 + x4                                                           ' 0x1419B3074 addps xmm8,xmm4
        x7 = x7 * p2                                                           ' 0x1419B3078 mulps xmm7,[r9+0x20]
        x8 = x8 * p1                                                           ' 0x1419B307D mulps xmm8,[r9+0x10]
        x8 = x8 + x6                                                           ' 0x1419B3082 addps xmm8,xmm6
        Return x8 + x7                                                         ' 0x1419B308B addps xmm8,xmm7 ; 0x1419B3094 movups [rax],xmm8
    End Function

    ''' <summary>0x1419B30B0 — grado 3, una lane.</summary>
    Private Shared Function Grado3(t As Single, k As Single(), p0 As Single, p1 As Single, p2 As Single, p3 As Single) As Single
        Dim x13 As Single = t - k(2)                                           ' 0x1419B30C9 shufps xmm0,xmm5,0xaa ; 0x1419B3120 subps xmm13,xmm0
        Dim x12 As Single = k(3) - t                                           ' 0x1419B3117 shufps xmm12,xmm5,0xff ; 0x1419B312F subps xmm12,xmm8
        Dim x10 As Single = k(4) - t                                           ' 0x1419B3129 movsd xmm14,[r8+0x10] ; 0x1419B313B shufps xmm10,xmm14,0 ; 0x1419B3144 subps
        Dim x14 As Single = k(5) - t                                           ' 0x1419B3148 shufps xmm14,xmm14,0x55 ; 0x1419B314D subps xmm14,xmm8
        Dim x1 As Single = x12 + x13                                           ' 0x1419B3151 movaps ; 0x1419B3155 addps xmm1,xmm13
        Dim r As Single = Rcp(x1)                                              ' 0x1419B3159 rcpps xmm2,xmm1
        Dim x0 As Single = r * x1                                              ' 0x1419B315C movaps xmm0,xmm2 ; 0x1419B315F mulps xmm0,xmm1
        Dim x3 As Single = 2.0F - x0                                           ' 0x1419B3109 movaps xmm3,xmm11 ; 0x1419B3162 subps xmm3,xmm0
        Dim x9 As Single = t - k(1)                                            ' 0x1419B3168 shufps xmm0,xmm5,0x55 ; 0x1419B316C subps xmm9,xmm0
        Dim x4 As Single = t - k(0)                                            ' 0x1419B3170 shufps xmm5,xmm5,0 ; 0x1419B3174 subps xmm4,xmm5
        x3 = x3 * r                                                            ' 0x1419B317B mulps xmm3,xmm2
        x1 = x9 + x12                                                          ' 0x1419B317E movaps xmm1,xmm9 ; 0x1419B3182 addps xmm1,xmm12
        r = Rcp(x1)                                                            ' 0x1419B3186 rcpps xmm2,xmm1
        x0 = r * x1                                                            ' 0x1419B3189 movaps ; 0x1419B318C mulps xmm0,xmm1
        x1 = x10 + x13                                                         ' 0x1419B318F movaps xmm1,xmm10 ; 0x1419B3193 addps xmm1,xmm13
        Dim x6 As Single = 2.0F - x0                                           ' 0x1419B3137 movaps xmm6,xmm11 ; 0x1419B3197 subps xmm6,xmm0
        x0 = x12 * x3                                                          ' 0x1419B319A movaps xmm0,xmm12 ; 0x1419B319E mulps xmm0,xmm3
        x6 = x6 * r                                                            ' 0x1419B31A1 mulps xmm6,xmm2
        r = Rcp(x1)                                                            ' 0x1419B31A4 rcpps xmm2,xmm1
        x6 = x6 * x0                                                           ' 0x1419B31A7 mulps xmm6,xmm0
        x0 = r * x1                                                            ' 0x1419B31AA movaps ; 0x1419B31AD mulps xmm0,xmm1
        x1 = x4 + x12                                                          ' 0x1419B31B0 movaps xmm1,xmm4 ; 0x1419B31B3 addps xmm1,xmm12
        Dim x7 As Single = 2.0F - x0                                           ' 0x1419B3140 movaps xmm7,xmm11 ; 0x1419B31B7 subps xmm7,xmm0
        x0 = x13 * x3                                                          ' 0x1419B31BA movaps xmm0,xmm13 ; 0x1419B31BE mulps xmm0,xmm3
        x7 = x7 * r                                                            ' 0x1419B31C1 mulps xmm7,xmm2
        r = Rcp(x1)                                                            ' 0x1419B31C4 rcpps xmm2,xmm1
        x7 = x7 * x0                                                           ' 0x1419B31C7 mulps xmm7,xmm0
        x0 = r * x1                                                            ' 0x1419B31CA movaps ; 0x1419B31CD mulps xmm0,xmm1
        x1 = x10 + x9                                                          ' 0x1419B31D0 movaps xmm1,xmm10 ; 0x1419B31D4 addps xmm1,xmm9
        Dim x5 As Single = 2.0F - x0                                           ' 0x1419B3177 movaps xmm5,xmm11 ; 0x1419B31D8 subps xmm5,xmm0
        x0 = x12 * x6                                                          ' 0x1419B31DB movaps xmm0,xmm12 ; 0x1419B31DF mulps xmm0,xmm6
        x5 = x5 * r                                                            ' 0x1419B31E2 mulps xmm5,xmm2
        r = Rcp(x1)                                                            ' 0x1419B31E5 rcpps xmm2,xmm1
        x5 = x5 * x0                                                           ' 0x1419B31E8 mulps xmm5,xmm0
        x0 = r * x1                                                            ' 0x1419B31EB movaps ; 0x1419B31EE mulps xmm0,xmm1
        x3 = 2.0F - x0                                                         ' 0x1419B3201 movaps xmm3,xmm11 ; 0x1419B3205 subps xmm3,xmm0
        x12 = x12 * x5                                                         ' 0x1419B3208 mulps xmm12,xmm5
        x1 = x10                                                               ' 0x1419B320C movaps xmm1,xmm10
        x4 = x4 * x5                                                           ' 0x1419B3210 mulps xmm4,xmm5
        x1 = x1 * x7                                                           ' 0x1419B3213 mulps xmm1,xmm7
        x0 = x9                                                                ' 0x1419B3216 movaps xmm0,xmm9
        x12 = x12 * p0                                                         ' 0x1419B321A mulps xmm12,[r9]
        x0 = x0 * x6                                                           ' 0x1419B321E mulps xmm0,xmm6
        x3 = x3 * r                                                            ' 0x1419B3226 mulps xmm3,xmm2
        x1 = x1 + x0                                                           ' 0x1419B3229 addps xmm1,xmm0
        x3 = x3 * x1                                                           ' 0x1419B322C mulps xmm3,xmm1
        x1 = x14 + x13                                                         ' 0x1419B322F movaps xmm1,xmm14 ; 0x1419B3233 addps xmm1,xmm13
        x10 = x10 * x3                                                         ' 0x1419B3237 mulps xmm10,xmm3
        r = Rcp(x1)                                                            ' 0x1419B323B rcpps xmm2,xmm1
        x10 = x10 + x4                                                         ' 0x1419B323E addps xmm10,xmm4
        x9 = x9 * x3                                                           ' 0x1419B3242 mulps xmm9,xmm3
        x0 = r * x1                                                            ' 0x1419B3246 movaps ; 0x1419B3249 mulps xmm0,xmm1
        Dim x11 As Single = 2.0F - x0                                          ' 0x1419B30F8 movaps xmm11,[0x142629500] ; 0x1419B324C subps xmm11,xmm0
        x0 = x13 * x7                                                          ' 0x1419B3250 movaps xmm0,xmm13 ; 0x1419B3254 mulps xmm0,xmm7
        x11 = x11 * r                                                          ' 0x1419B325C mulps xmm11,xmm2
        x11 = x11 * x0                                                         ' 0x1419B3260 mulps xmm11,xmm0
        x0 = p1 * x10                                                          ' 0x1419B3264 movups xmm0,[r9+0x10] ; 0x1419B3269 mulps xmm0,xmm10
        x14 = x14 * x11                                                        ' 0x1419B3272 mulps xmm14,xmm11
        x13 = x13 * x11                                                        ' 0x1419B3276 mulps xmm13,xmm11
        x12 = x12 + x0                                                         ' 0x1419B327A addps xmm12,xmm0
        x14 = x14 + x9                                                         ' 0x1419B3283 addps xmm14,xmm9
        x13 = x13 * p3                                                         ' 0x1419B328C mulps xmm13,[r9+0x30]
        x14 = x14 * p2                                                         ' 0x1419B3291 mulps xmm14,[r9+0x20]
        x14 = x14 + x12                                                        ' 0x1419B3296 addps xmm14,xmm12
        Return x14 + x13                                                       ' 0x1419B329F addps xmm14,xmm13 ; 0x1419B32A9 movups [rax],xmm14
    End Function

    ''' <summary>El despacho por grado de los helpers (`sub r9d,1 / sub r9d,1 / cmp r9d,1`): 0x1419B35CC-0x1419B3640,
    ''' 0x1419B395B-0x1419B39CC, 0x1419B41D3-0x1419B4274 y sus gemelos. Fuera de 1..3 NO se escribe.</summary>
    Private Function Evaluar(grado As Integer, t As Single, salida As LanesSpline) As LanesSpline
        Dim P = _puntos
        Dim k = _nudos
        Select Case grado
            Case 1                                                             ' 0x1419B35CC sub r9d,1 ; 0x1419B35D0 je → 0x1419B3640 call 0x1419B2F50
                Return New LanesSpline(Grado1(t, k, P(0).L0, P(1).L0), Grado1(t, k, P(0).L1, P(1).L1),
                                       Grado1(t, k, P(0).L2, P(1).L2), Grado1(t, k, P(0).L3, P(1).L3))
            Case 2                                                             ' 0x1419B35D2 sub r9d,1 ; 0x1419B35D6 je → 0x1419B361D call 0x1419B2FB0
                Return New LanesSpline(Grado2(t, k, P(0).L0, P(1).L0, P(2).L0), Grado2(t, k, P(0).L1, P(1).L1, P(2).L1),
                                       Grado2(t, k, P(0).L2, P(1).L2, P(2).L2), Grado2(t, k, P(0).L3, P(1).L3, P(2).L3))
            Case 3                                                             ' 0x1419B35D8 cmp r9d,1 ; 0x1419B35DC jne → 0x1419B35FA call 0x1419B30B0
                Return New LanesSpline(Grado3(t, k, P(0).L0, P(1).L0, P(2).L0, P(3).L0), Grado3(t, k, P(0).L1, P(1).L1, P(2).L1, P(3).L1),
                                       Grado3(t, k, P(0).L2, P(1).L2, P(2).L2, P(3).L2), Grado3(t, k, P(0).L3, P(1).L3, P(2).L3, P(3).L3))
            Case Else
                Return salida                                                  ' 0x1419B35DC jne 0x1419B3645: sin llamada, la salida queda como estaba
        End Select
    End Function

    ' ================================================================================= 0x1419B32C0 / 0x1419B3670
    ''' <summary>Vector cuantizado: `ancho` 2 = u16 (0x1419B32C0), 1 = u8 (0x1419B3670).</summary>
    Private Function Vector(ByRef ptr As Long, qt As Integer, bt As Single, m As Integer, porDefecto As LanesSpline, salida As LanesSpline, ancho As Integer) As LanesSpline
        Dim n As Integer = 0, grado As Integer = 0, tramo As Integer = 0      ' 0x1419B32DC xor r12d ; 0x1419B32F7-0x1419B32FD mov r11d/r9d/r14d,r12d
        Dim spline As Integer = m And &HF0                                     ' 0x1419B32DF movzx edi,sil ; 0x1419B3300 and dil,0xf0 | 0x1419B36AB and sil,0xf0
        If spline <> 0 Then                                                    ' 0x1419B3304 je | 0x1419B36AF je
            Nudos(ptr, qt, n, grado, tramo)                                    ' 0x1419B3325 call 0x1419B2C30 | 0x1419B36D0
        End If
        ptr = (ptr + 3L) And Not 3L                                            ' 0x1419B3340 mov rdx,[rbx] ; 0x1419B3346 add rdx,3 ; 0x1419B334E and rdx,~3 ; 0x1419B3364 mov [rbx],rdx
        Dim est As New LanesSpline(0.0F, 0.0F, 0.0F, 0.0F)                     ' 0x1419B3352 movaps [rsp+0x30],xmm0 | 0x1419B36F3 movaps [rsp+0x40],xmm0
        Dim mn As New LanesSpline(0.0F, 0.0F, 0.0F, 0.0F)                      ' 0x1419B335F movaps [rsp+0x40],xmm1 | 0x1419B36FF movaps [rsp+0x50],xmm1
        Dim mx As New LanesSpline(0.0F, 0.0F, 0.0F, 0.0F)                      ' 0x1419B3357 movaps [rsp+0x50],xmm0 | 0x1419B36F8 movaps [rbp-0x79],xmm0
        If (m And 1) <> 0 Then                                                 ' 0x1419B3367 test r8b,1 ; je
            est.L0 = LeerF32(ptr) : ptr += 4                                   ' 0x1419B336D movss xmm0,[rdx] ; 0x1419B3371 add rdx,4
        ElseIf (m And &H10) <> 0 Then                                          ' 0x1419B337D test r8b,0x10 ; je
            mn.L0 = LeerF32(ptr) : mx.L0 = LeerF32(ptr + 4) : ptr += 8         ' 0x1419B3383 movss ; 0x1419B3398 movss xmm0,[rax] ; 0x1419B3391 lea rdx,[rax+4]
        End If
        If (m And 2) <> 0 Then                                                 ' 0x1419B33A5 test r8b,2 ; je
            est.L1 = LeerF32(ptr) : ptr += 4                                   ' 0x1419B33AB movss ; 0x1419B33AF add rdx,4
        ElseIf (m And &H20) <> 0 Then                                          ' 0x1419B33BB test r8b,0x20 ; je
            mn.L1 = LeerF32(ptr) : mx.L1 = LeerF32(ptr + 4) : ptr += 8         ' 0x1419B33C1 movss ; 0x1419B33D6 movss ; 0x1419B33CF lea rdx,[rax+4]
        End If
        If (m And 4) <> 0 Then                                                 ' 0x1419B33EA test r8b,4 ; je
            est.L2 = LeerF32(ptr) : ptr += 4                                   ' 0x1419B33F0 movss ; 0x1419B33F4 add rdx,4
        ElseIf (m And &H40) <> 0 Then                                          ' 0x1419B33E3 mov r10d,r8d ; and r10d,0x40 ; 0x1419B3400 test r10d,r10d
            mn.L2 = LeerF32(ptr) : mx.L2 = LeerF32(ptr + 4) : ptr += 8         ' 0x1419B3405 movss ; 0x1419B341A movss ; 0x1419B3413 lea rdx,[rax+4]
        End If
        Dim sm As LanesSpline = Mascara(m And &HF)                             ' 0x1419B342C lea r15,[0x14270FB60] ; 0x1419B3436 and eax,0xf ; 0x1419B343C movups xmm3,[r15+rax*4]
        Dim nm As Integer = Not m                                              ' 0x1419B3444 not eax
        Dim dm As LanesSpline = Mascara(((nm >> 4) And &HF) And nm)            ' 0x1419B3448 sar ecx,4 ; 0x1419B344B and ecx,0xf ; 0x1419B344E and ecx,eax ; 0x1419B3453 movups xmm4,[r15+rcx*4]
        If spline = 0 Then                                                     ' 0x1419B3460 test dil,dil ; 0x1419B3463 jne
            Dim o As LanesSpline = OrL(AndL(est, sm), AndNotL(sm, salida))     ' 0x1419B3469 andps xmm2,xmm3 ; 0x1419B3470 andnps xmm3,[rcx] ; 0x1419B3473 orps
            Return OrL(AndL(porDefecto, dm), AndNotL(dm, o))                   ' 0x1419B3479 movups xmm0,[rax] ; 0x1419B347C andps xmm0,xmm4 ; 0x1419B347F andnps xmm4,xmm2 ; 0x1419B3482 orps
        End If
        Dim nsp As Integer = PopCuenta((m >> 4) And 7)                         ' 0x1419B348D/0x1419B3498 tablas 0x14270FF20/0x14270FF30 ; 0x1419B34A4 shr rax,4 ; 0x1419B34A8 and eax,7 ; 0x1419B34C6 mov esi,[rsp+rax*4+0x50]
        Dim c As Long = ptr + CLng(S32(CLng(S32(CLng(tramo) - grado)) * nsp * ancho)) ' 0x1419B3495 sub r11d,r9d ; 0x1419B34CA imul r11d,esi ; 0x1419B34E0 add r11d,r11d (sólo u16) ; 0x1419B34E3 movsxd ; 0x1419B34E6 add rcx,rdx
        If grado >= 0 Then                                                     ' 0x1419B34E9 test r9d,r9d ; 0x1419B34EC js
            Dim rango As LanesSpline = SubL(mx, mn)                            ' 0x1419B34B3 movaps xmm6,[rsp+0x50] ; 0x1419B34F6 subps xmm6,xmm7 | 0x1419B3889 subps xmm5,xmm7
            Dim esc As Single = If(ancho = 2, K1Sobre65535, K1Sobre255)        ' 0x1419B34F9 movss xmm5,[0x1424AE3E0] ; shufps | 0x1419B38B7 movaps xmm9,[0x142F3C6F0]
            Dim defM As LanesSpline = AndL(porDefecto, dm)                     ' 0x1419B351F movups xmm8,[rax] ; 0x1419B3526 andps xmm8,xmm4
            Dim estM As LanesSpline = AndL(est, sm)                            ' 0x1419B3536 andps xmm2,xmm3
            Dim w0 As Integer = 0, w1 As Integer = 0, w2 As Integer = 0        ' 0x1419B34DB mov [rsp+0x30],r12 | 0x1419B386E mov dword ptr [rsp+0x30],0
            For i As Integer = 0 To grado                                      ' 0x1419B350F lea edi,[r9+1] ; 0x1419B35A4 sub rdi,1 ; jne
                If (m And &H10) <> 0 Then w0 = LeerCuanto(c, ancho) : c += ancho ' 0x1419B3540 test edx,edx ; 0x1419B3544 movzx eax,word ptr [rcx] ; add rcx,2 | 0x1419B38D4 movzx eax,byte ptr [rcx] ; inc rcx
                If (m And &H20) <> 0 Then w1 = LeerCuanto(c, ancho) : c += ancho ' 0x1419B3550 test r8d,r8d ; 0x1419B3555 movzx ; 0x1419B3558 add rcx,2
                If (m And &H40) <> 0 Then w2 = LeerCuanto(c, ancho) : c += ancho ' 0x1419B3561 test r10d,r10d ; 0x1419B3566 movzx ; 0x1419B3569 add rcx,2
                Dim q As New LanesSpline(CSng(w0), CSng(w1), CSng(w2), 0.0F)   ' 0x1419B3572 movsd ; 0x1419B3578 punpcklwd ; 0x1419B357D cvtdq2ps | 0x1419B3902 punpcklbw ; 0x1419B3907 punpcklwd ; 0x1419B390C cvtdq2ps
                Dim x0 As LanesSpline = MulE(q, esc)                           ' 0x1419B3583 mulps xmm0,xmm5
                x0 = MulL(x0, rango)                                           ' 0x1419B3586 mulps xmm0,xmm6
                x0 = AddL(x0, mn)                                              ' 0x1419B3589 addps xmm0,xmm7
                Dim x1 As LanesSpline = OrL(AndNotL(sm, x0), estM)             ' 0x1419B3580 movaps xmm1,xmm3 ; 0x1419B358C andnps xmm1,xmm0 ; 0x1419B3592 orps xmm1,xmm2
                _puntos(i) = OrL(AndNotL(dm, x1), defM)                        ' 0x1419B3595 andnps xmm0,xmm1 ; 0x1419B3598 orps xmm0,xmm8 ; 0x1419B359C movups [r11],xmm0
            Next
        End If
        Dim r As LanesSpline = Evaluar(grado, bt, salida)                      ' 0x1419B35CC-0x1419B3640 (xmm0 = [rbp+0x67] = tiempo de bloque ; r8 = nudos ; r9 = puntos)
        ptr += CLng(S32(CLng(n + 1) * nsp * ancho))                            ' 0x1419B3645 lea eax,[r14+1] ; 0x1419B3649 imul eax,esi ; 0x1419B364C add eax,eax (sólo u16) ; 0x1419B364E cdqe ; 0x1419B3650 add [rbx],rax
        Return r
    End Function

    ' ================================================================================= rotación
    ''' <summary>0x1419B4160 (0) / 0x1419B3A00 (1) / 0x1419B3B70 (2) / 0x1419B3CF0 (3) / 0x1419B3E60 (4) / 0x1419B3FE0 (5).</summary>
    Private Function Rotacion(fmt As Integer, ByRef ptr As Long, qt As Integer, bt As Single, m As Integer, salida As LanesSpline) As LanesSpline
        Dim tam As Integer = TamanoRot(fmt)
        Dim al As Long = AlineacionRot(fmt)
        If (m And &HF0) <> 0 Then                                              ' 0x1419B3A0F movzx eax,[rsp+0xd8] ; 0x1419B3A1D test al,0xf0 ; je
            Dim n As Integer = 0, grado As Integer = 0, tramo As Integer = 0
            Nudos(ptr, qt, n, grado, tramo)                                    ' 0x1419B3A46 call 0x1419B2C30
            ' 0x1419B42E0 / 0x1419B4370 / 0x1419B4410 / 0x1419B44A0 / 0x1419B4540 / 0x1419B45E0
            ptr = (ptr + al - 1L) And Not (al - 1L)                            ' 0x1419B4384 inc rdx ; and rdx,~1 | 0x1419B45F4 add rdx,3 ; and rdx,~3 | (40 y 24: sin alinear)
            If grado >= 0 Then                                                 ' 0x1419B42F1 test r9d,r9d ; 0x1419B42F4 js
                Dim desde As Long = ptr + CLng(S32(CLng(S32(CLng(tramo) - grado)) * tam)) ' 0x1419B42F6 mov eax,[rsp+0x50] ; 0x1419B42FF sub eax,r9d ; 0x1419B4315 lea eax,[rax+rax*4] ; 0x1419B4318 movsxd rdi,eax
                For i As Integer = 0 To grado                                  ' 0x1419B430C lea esi,[r9+1] ; 0x1419B4336 sub rsi,1 ; jne
                    _puntos(i) = Desempaquetar(fmt, desde + CLng(i) * tam)     ' 0x1419B4320 mov rcx,[r14] ; 0x1419B4326 add rcx,rdi ; 0x1419B4329 call 0x141A47910 ; 0x1419B432E add rdi,5 ; 0x1419B4332 add rbx,0x10
                Next
            End If
            ptr += CLng(S32(CLng(n + 1) * tam))                                ' 0x1419B434B inc ebp ; 0x1419B434D lea eax,[rbp*4] ; 0x1419B4354 add eax,ebp ; 0x1419B435B movsxd ; 0x1419B435E add [r14],rcx
            Return Evaluar(grado, bt, salida)                                  ' 0x1419B3A73 sub ebx,1 ; je 0x1419B3B14 / 0x1419B3AE8 / 0x1419B3AA5 (xmm0 = [rsp+0xd0])
        End If
        If (m And &HF) <> 0 Then                                               ' 0x1419B3B1B test al,0xf ; 0x1419B3B1D je
            ptr = (ptr + al - 1L) And Not (al - 1L)                            ' 0x1419B3C92 inc rcx ; and rcx,~1 | 0x1419B4282 add rcx,3 ; and rcx,~3 | (40 y 24: sin alinear)
            Dim q As LanesSpline = Desempaquetar(fmt, ptr)                     ' 0x1419B3B27 mov rcx,[rdi] ; 0x1419B3B2A call 0x141A47910
            ptr += tam                                                         ' 0x1419B3B2F add qword ptr [rdi],5
            Return q
        End If
        Return New LanesSpline(0.0F, 0.0F, 0.0F, 1.0F)                         ' 0x1419B3B4C movaps xmm0,[0x142F3C730] ; 0x1419B3B5B movups [rax],xmm0
    End Function

    Private Function Desempaquetar(fmt As Integer, p As Long) As LanesSpline
        Select Case fmt
            Case 0 : Return Polar32(p)                                         ' 0x1419B4639 call 0x141A475F0
            Case 1 : Return TresComp40(p)                                      ' 0x1419B4329 call 0x141A47910
            Case 2 : Return TresComp(CInt(LeerU16(p)), CInt(LeerU16(p + 2)), CInt(LeerU16(p + 4)), True) ' 0x1419B43C9 call 0x141A47B40
            Case 3 : Return TresComp(CInt(_d(CInt(p))), CInt(_d(CInt(p + 1))), CInt(_d(CInt(p + 2))), False) ' 0x1419B4459 call 0x141A474D0
            Case 4 : Return Recto16(p)                                         ' 0x1419B44F9 call 0x141A47430
            Case Else                                                          ' 0x1419B4599 call 0x141A47C60
                Return New LanesSpline(LeerF32(p), LeerF32(p + 4), LeerF32(p + 8), LeerF32(p + 12)) ' 0x141A47C60 movups xmm0,[rcx] ; 0x141A47C63 movups [rdx],xmm0
        End Select
    End Function

    ''' <summary>0x141A47910 — THREECOMP40 (5 bytes).</summary>
    Private Function TresComp40(p As Long) As LanesSpline
        Dim b0 As Integer = _d(CInt(p)), b1 As Integer = _d(CInt(p + 1)), b2 As Integer = _d(CInt(p + 2)) ' 0x141A47914 movsd xmm0,[rcx] ; 0x141A47935-0x141A4799B psrldq/movd → [rsp]
        Dim b3 As Integer = _d(CInt(p + 3)), b4 As Integer = _d(CInt(p + 4))
        Dim a As Integer = (b0 Or (b1 << 8)) And &HFFF                         ' lane 0: [rsp]=b0,[rsp+1]=b1 ; 0x141A479D1 pand (sin desplazar) ; 0x141A479F7 pand 0xfff
        Dim bb As Integer = ((b1 Or (b2 << 8)) >> 4) And &HFFF                 ' lane 1: [rsp+4]=b1,[rsp+5]=b2 ; 0x141A479CC psrld xmm2,4 (lane 1 por 0x14271B450) ; pand 0xfff
        Dim cc As Integer = (b3 Or (b4 << 8)) And &HFFF                        ' lane 2: [rsp+8]=b3,[rsp+9]=b4 ; pand 0xfff
        Dim x As Single = (CSng(a) - 2047.0F) * K40                            ' 0x141A47A03 cvtdq2ps ; 0x141A47A26 subps xmm6,xmm4 (0x14262DD20) ; 0x141A47A29 mulps xmm6,xmm3
        Dim y As Single = (CSng(bb) - 2047.0F) * K40
        Dim z As Single = (CSng(cc) - 2047.0F) * K40
        Dim w3 As Single = (CSng(&H7FF) - 2047.0F) * K40                       ' 0x141A479FD por [rsp+0x20]: lane 3 = 0x7ff
        Dim s As Single = 1.0F - Suma4(x * x, y * y, z * z, w3 * w3)           ' 0x141A47A36 mulps ; 0x141A47A3C-0x141A47A4A shufps/addps ; 0x141A47A4D subps xmm3,xmm0
        Dim rec As Single = RsqrtX(s) * s                                      ' 0x141A47A6D rsqrtps xmm1,xmm3 ; 0x141A47A7C mulps xmm1,xmm3
        rec = If(s <= 0.0F, 0.0F, rec)                                         ' 0x141A47A78 cmpleps xmm4,xmm0 ; 0x141A47A84 andnps xmm4,xmm1
        If (b4 And &H40) <> 0 Then rec = Sng(Bits(rec) Xor &H80000000UI)      ' 0x141A47A54 pand [rsp+0x20]=0x40 ; 0x141A47A62 pcmpeqd ; 0x141A47A68/0x141A47A7F psrld/pslld 0x1f ; 0x141A47A87 xorps
        Select Case b4 And &H30                                                ' 0x141A47A9E pand xmm0,xmm5 ([rsp+0x24] = 0x30) ; 0x141A47AA7 movd eax
            Case 0 : Return New LanesSpline(rec, x, y, z)                      ' 0x141A47B11 shufps 0x93 ; 0x141A47B1B movss xmm6,xmm4
            Case &H10 : Return New LanesSpline(x, rec, y, z)                   ' 0x141A47AF6 shufps 0x9c ; unpcklps ; shufps 0xe4
            Case &H20 : Return New LanesSpline(x, y, rec, z)                   ' 0x141A47AD5 shufps 0xb4 ; unpckhps ; shufps 0xb4
            Case Else : Return New LanesSpline(x, y, z, rec)                   ' 0x141A47ABE unpckhps xmm0,xmm4 ; 0x141A47AC4 shufps xmm6,xmm0,0xc4
        End Select
    End Function

    ''' <summary>0x141A47B40 — THREECOMP48 (tres u16) y 0x141A474D0 — THREECOMP24 (tres u8).</summary>
    Private Shared Function TresComp(a As Integer, b As Integer, c As Integer, dieciseis As Boolean) As LanesSpline
        Dim baseI As Integer = If(dieciseis, &H3FFF, &H3F)                     ' 0x141A47B46 movdqa xmm0,[0x14271B670] | 0x141A474D6 [0x14271B650]
        Dim lim As Integer = If(dieciseis, &H7FFF, &H7F)                       ' 0x141A47B86 and ecx,0x7fff | 0x141A47513 and ecx,0x7f
        Dim bitAlto As Integer = If(dieciseis, &H8000, &H80)                   ' 0x141A47B8E and r11d,0x8000 | 0x141A47518 and r11d,0x80
        Dim corr As Integer = If(dieciseis, 14, 6)                             ' 0x141A47BA0 shr r11d,0xe | 0x141A4752A shr r11d,6
        Dim cst As Single = If(dieciseis, 16383.0F, 63.0F)                     ' 0x141A47B57 movss xmm2,[0x14271B630] | 0x141A474E7 [0x14262DD18]
        Dim esc As Single = If(dieciseis, K48, K24)                            ' 0x141A47B62 movss xmm1,[0x14271B610] | 0x141A474F2 [0x14271B620]
        Dim lanes() As Integer = {baseI, baseI, baseI, baseI}                  ' 0x141A47B7F movdqa [rsp],xmm0
        Dim idx As Integer = ((b And bitAlto) Or (a >> 1)) >> corr             ' 0x141A47B8C shr eax,1 ; 0x141A47B99 or r11d,eax ; 0x141A47BA0 shr r11d
        lanes(If(idx = 0, 1, 0)) = a And lim                                   ' 0x141A47BA7 test ; 0x141A47BAA cmove eax,r10d(4) ; 0x141A47BAE mov [rsp+rax],ecx
        Dim cc As Integer = If(idx = 0, 1, 0) + 1                              ' 0x141A47BB4 sete cl ; 0x141A47BBA inc ecx
        cc += If(cc = idx, 1, 0)                                               ' 0x141A47BBC cmp ecx,r11d ; 0x141A47BBF sete al ; 0x141A47BC8 add ecx,eax
        lanes(cc) = b And lim                                                  ' 0x141A47BC2 and edx,0x7fff ; 0x141A47BCA mov [rsp+rcx*4],edx
        cc += 1                                                                ' 0x141A47BCD inc ecx
        cc += If(cc = idx, 1, 0)                                               ' 0x141A47BD8 cmp ecx,r11d ; 0x141A47BDB sete r8b ; 0x141A47BDF add r8d,ecx
        lanes(cc) = c And lim                                                  ' 0x141A47BD2 and edx,0x7fff ; 0x141A47BE2 mov [rsp+r8*4],edx
        Dim x0 As Single = (CSng(lanes(0)) - cst) * esc                        ' 0x141A47BE6 cvtdq2ps ; 0x141A47BEA subps xmm4,xmm2 ; 0x141A47BF4 mulps xmm4,xmm1
        Dim x1 As Single = (CSng(lanes(1)) - cst) * esc
        Dim x2 As Single = (CSng(lanes(2)) - cst) * esc
        Dim x3 As Single = (CSng(lanes(3)) - cst) * esc
        Dim s As Single = 1.0F - Suma4(x0 * x0, x1 * x1, x2 * x2, x3 * x3)     ' 0x141A47BFA mulps ; 0x141A47C00-0x141A47C0E ; 0x141A47C11 subps xmm2,xmm0
        Dim rec As Single = RsqrtX(s) * s                                      ' 0x141A47C14 rsqrtps xmm0,xmm2 ; 0x141A47C1E mulps xmm0,xmm2 | 0x141A4759B ; 0x141A475A5
        rec = If(s <= 0.0F, 0.0F, rec)                                         ' 0x141A47C1A cmpleps xmm1,xmm3 ; 0x141A47C21 andnps xmm1,xmm0 | 0x141A475A1 ; 0x141A475A8
        If (c And bitAlto) <> 0 Then rec = 0.0F - rec                          ' 0x141A47C24 test r9w,r9w ; jns ; 0x141A47C2A subps xmm3,xmm1 | 0x141A475AB test r9b,r9b ; 0x141A475B0 subps xmm1,xmm3
        ' 0x141A47C33 shl r10d,cl (4<<idx) ; 0x141A47C40 movups xmm0,[0x14271B430 + (4<<idx)*4] = lane idx ; andps/andnps/orps
        Return New LanesSpline(If(idx = 0, rec, x0), If(idx = 1, rec, x1), If(idx = 2, rec, x2), If(idx = 3, rec, x3))
    End Function

    ''' <summary>0x141A47430 — STRAIGHT16 (cuatro nibbles).</summary>
    Private Function Recto16(p As Long) As LanesSpline
        Dim c0 As Integer = _d(CInt(p)), c1 As Integer = _d(CInt(p + 1))       ' 0x141A47430 movzx r8d,[rcx+1] ; 0x141A47435 movzx r9d,[rcx]
        ' 0x141A4743F-0x141A47467: lanes (b0&F, b0>>4, b1&F, b1>>4) por punpckldq
        Dim x0 As Single = (CSng(c0 And &HF) - 7.0F) * K16                     ' 0x141A4746B cvtdq2ps ; 0x141A4746E subps [0x142F3C5C0] ; 0x141A47475 mulps [0x142F3C6A0]
        Dim x1 As Single = (CSng(c0 >> 4) - 7.0F) * K16
        Dim x2 As Single = (CSng(c1 And &HF) - 7.0F) * K16
        Dim x3 As Single = (CSng(c1 >> 4) - 7.0F) * K16
        Dim s As Single = Suma4(x0 * x0, x1 * x1, x2 * x2, x3 * x3)            ' 0x141A47482 mulps ; 0x141A47488-0x141A4749D shufps/addps
        Dim r As Single = RsqrtX(s)                                            ' 0x141A474A0 rsqrtps xmm1,xmm2
        s = s * r                                                              ' 0x141A474A3 mulps xmm2,xmm1
        s = s * r                                                              ' 0x141A474A6 mulps xmm2,xmm1
        r = r * 0.5F                                                           ' 0x141A474A9 mulps xmm1,[0x142629520]
        Dim f As Single = 3.0F - s                                             ' 0x141A4748F movaps xmm0,[0x142629510] ; 0x141A474B0 subps xmm0,xmm2
        f = f * r                                                              ' 0x141A474B3 mulps xmm0,xmm1
        Return New LanesSpline(f * x0, f * x1, f * x2, f * x3)                 ' 0x141A474B6 mulps xmm0,xmm4 ; 0x141A474B9 movups [rdx],xmm0
    End Function

    ''' <summary>0x141A475F0 — POLAR32.</summary>
    Private Function Polar32(p As Long) As LanesSpline
        Dim c As UInteger = LeerU32(p)                                         ' 0x141A475FA mov r8d,[rcx]
        Dim e As Integer = CInt((c >> 18) And &H3FFUI)                         ' 0x141A47612 mov eax,r8d ; 0x141A4765C shr eax,0x12 ; 0x141A4765F and eax,0x3ff
        Dim resto As Integer = CInt(c And &H3FFFFUI)                           ' 0x141A47619 mov ecx,r8d ; 0x141A47621 and ecx,0x3ffff
        Dim x0 As Single = CSng(e) * KPolarE                                   ' 0x141A47680 cvtsi2ss xmm0,eax ; 0x141A47691 mulps xmm0,xmm1
        x0 = x0 * x0                                                           ' 0x141A47694 mulps xmm0,xmm0
        Dim w As Single = 1.0F - x0                                            ' 0x141A47669 movaps xmm12,[0x142F3C560] ; 0x141A47684 movaps xmm14,xmm12 ; 0x141A47697 subps xmm14,xmm0
        Dim cf As Single = CSng(resto)                                         ' 0x141A4769E cvtsi2ss xmm0,ecx
        Dim raiz As Single = MathF.Sqrt(cf)                                    ' 0x141A476A9 sqrtps xmm0,xmm0
        raiz = If(cf <= 0.0F, 0.0F, raiz)                                      ' 0x141A476AC cmpleps xmm1,xmm13 ; 0x141A476B1 andnps xmm1,xmm0
        Dim r As Integer = Cvtt(raiz)                                          ' 0x141A476B4 cvttps2dq ; 0x141A476B8 movd eax,xmm0
        Dim rf As Single = CSng(r)                                             ' 0x141A476BC cvtsi2ss xmm3,eax
        resto = S32(CLng(resto) - CLng(S32(CLng(r) * CLng(r))))                ' 0x141A476C0 imul eax,eax ; 0x141A476CE sub ecx,eax
        Dim a As Single = KPolarR * rf                                         ' 0x141A4764E movss xmm11,[0x14271B61C] ; 0x141A476C7 mulps xmm11,xmm3
        Dim r2 As Single = rf + rf                                             ' 0x141A476CB addps xmm3,xmm3
        a = a * KMedioPi                                                       ' 0x141A476D0 mulps xmm11,[0x142F3C860]
        Dim signo2r As UInteger = Bits(r2) And &H80000000UI                    ' 0x141A476D8 movdqa ; 0x141A476DC psrld 0x1f ; 0x141A476E4 pslld 0x1f
        Dim inv As Single                                                      ' xmm2
        If r2 = 0.0F Then                                                      ' 0x141A476EC cmpeqps xmm1,xmm13
            inv = Sng(KCasiMax Xor signo2r)                                    ' 0x141A4760B movaps xmm2,[0x1424684A0] ; 0x141A476E9 xorps xmm2,xmm0 ; 0x141A476F1 andps xmm2,xmm1 ; 0x141A47708 orps
        Else
            inv = Rcp(r2)                                                      ' 0x141A476F8 rcpps xmm0,xmm3 ; 0x141A47701 andnps xmm1,xmm0 ; 0x141A47708 orps xmm2,xmm1
        End If
        Dim b As Single = CSng(resto) * inv                                    ' 0x141A47713 cvtsi2ss xmm0,ecx ; 0x141A4771B mulps xmm0,xmm2
        b = b * KMedioPi                                                       ' 0x141A47726 mulps xmm0,[0x142F3C860]
        b = If(inv = 0.0F, 0.0F, b)                                            ' 0x141A4770E cmpeqps xmm1,xmm13 ; 0x141A4772D andnps xmm1,xmm0
        ' 0x141A476F4 unpcklps xmm11,xmm11 ; 0x141A47730 unpcklps xmm1,xmm1 ; 0x141A47733 movlhps: lanes (a, a, b, b)
        Dim sa As Single, ca As Single, sb As Single, cb As Single
        PolarLanes(a, sa, ca)                                                  ' lanes 0 (sel) y 1 (otro)
        PolarLanes(b, sb, cb)                                                  ' lanes 2 (sel) y 3 (otro)
        Dim ww As Single = w * w                                               ' 0x141A4783D movaps xmm0,xmm14 ; 0x141A47841 mulps xmm0,xmm14
        Dim w2 As Single = 1.0F - ww                                           ' 0x141A47868 movaps xmm2,xmm12 ; 0x141A47870 subps xmm2,xmm0
        Dim l0 As Single = cb * sa                                             ' 0x141A47879 shufps xmm1,xmm3,0 ; 0x141A47880 shufps xmm4,xmm3,0xff ; 0x141A47884 mulps xmm4,xmm1
        Dim l1 As Single = sb * sa                                             ' 0x141A47887 shufps xmm0,xmm3,0xaa ; 0x141A4788B mulps xmm0,xmm1
        Dim esc As Single = RsqrtX(w2) * w2                                    ' 0x141A478A1 rsqrtps xmm0,xmm2 ; 0x141A478A7 mulps xmm0,xmm2
        esc = If(w2 <= 0.0F, 0.0F, esc)                                        ' 0x141A47897 cmpleps xmm1,xmm13 ; 0x141A478AA andnps xmm1,xmm0
        ' 0x141A47891/0x141A47896 shufps 0x55 · unpcklps xmm3,xmm12 ; 0x141A4789E unpcklps ; 0x141A478A4 movlhps → (l0, l1, ca, 1)
        Dim q0 As Single = l0 * esc                                            ' 0x141A478AD mulps xmm4,xmm1
        Dim q1 As Single = l1 * esc
        Dim q2 As Single = ca * esc
        ' 0x141A478E4 unpckhps xmm0,xmm14 ; 0x141A478F7 shufps xmm4,xmm0,0xc4 → lane 3 = w
        Dim signos As UInteger = c >> 28                                       ' 0x141A4777F shr r8d,0x1c ; 0x141A4778F shl r8d,2 ; 0x141A478B0 movups xmm1,[0x14271B430+r8*4]
        Return New LanesSpline(ConSigno(q0, (signos And 1UI) <> 0), ConSigno(q1, (signos And 2UI) <> 0),   ' 0x141A478ED psrld 0x1f ; 0x141A478F2 pslld 0x1f ; 0x141A478FB xorps xmm4,xmm1
                               ConSigno(q2, (signos And 4UI) <> 0), ConSigno(w, (signos And 8UI) <> 0))
    End Function

    ''' <summary>Las dos lanes de un ángulo del POLAR32 (0x141A4773B-0x141A47865): `sel` va a la lane par
    ''' (máscara 0x14271B480) y `otro` a la impar.</summary>
    Private Shared Sub PolarLanes(ang As Single, ByRef sel As Single, ByRef otro As Single)
        Dim absv As Single = Sng(Bits(ang) And &H7FFFFFFFUI)                   ' 0x141A476FB movaps xmm3,[0x142629470] ; 0x141A47737 andnps xmm3,xmm11
        Dim j As Integer = Cvtt(absv * KCuatroSobrePi)                         ' 0x141A4773E mulps xmm0,[0x1426294B0] ; 0x141A47745 cvttps2dq
        j = S32(CLng(j) + 1L) And Not 1                                        ' 0x141A47749 paddd xmm1,xmm10 ; 0x141A4774E pandn xmm10,xmm1
        Dim jf As Single = CSng(j)                                             ' 0x141A47753 cvtdq2ps xmm1,xmm10
        Dim signoAng As UInteger = Bits(ang) And &H80000000UI                  ' 0x141A4776A andps xmm11,[0x142629470]
        Dim y As Single = jf * KPi4a                                           ' 0x141A47772 movaps xmm5,xmm1 ; 0x141A47775 mulps xmm5,[0x1426294C0]
        Dim y2 As Single = jf * KPi4b                                          ' 0x141A4777C movaps ; 0x141A47783 mulps xmm0,[0x1426294D0]
        Dim y3 As Single = jf * KPi4c                                          ' 0x141A47793 mulps xmm1,[0x1426294E0]
        Dim bit4 As UInteger = CUInt(j And 4) << 29                            ' 0x141A4778A movdqa ; 0x141A4779A pand xmm8,xmm2 ; 0x141A477A2 pslld 0x1d
        y = y + absv                                                           ' 0x141A4779F addps xmm5,xmm3
        Dim j2 As Integer = j And 2                                            ' 0x141A477A8 pand xmm10,xmm6
        Dim bitC As UInteger = CUInt((Not S32(CLng(j) - 2L)) And 4) << 29      ' 0x141A477AD psubd xmm7,xmm6 ; 0x141A477B1 pandn xmm7,xmm2 ; 0x141A477B9 pslld 0x1d
        Dim signoSel As UInteger = signoAng Xor bit4                           ' 0x141A477B5 xorps xmm11,xmm8
        y = y + y2                                                             ' 0x141A477BE addps xmm5,xmm0
        y = y + y3                                                             ' 0x141A477C1 addps xmm5,xmm1
        Dim z As Single = y * y                                                ' 0x141A477C4 movaps ; 0x141A477C7 mulps xmm1,xmm5
        Dim c3 As Single = z * KCos1                                           ' 0x141A477D0 mulps xmm3,[0x142629440]
        Dim c2 As Single = z * KSen1                                           ' 0x141A477DA mulps xmm2,[0x142629410]
        Dim h As Single = z * 0.5F                                             ' 0x141A477E1 mulps xmm0,[0x142629520]
        c3 = c3 + KCos2                                                        ' 0x141A477E8 addps xmm3,[0x142629450]
        c2 = c2 + KSen2                                                        ' 0x141A477EF addps xmm2,[0x142629420]
        c3 = c3 * z                                                            ' 0x141A477F6 mulps xmm3,xmm1
        c2 = c2 * z                                                            ' 0x141A477F9 mulps xmm2,xmm1
        c3 = c3 + KCos3                                                        ' 0x141A477FC addps xmm3,[0x142629460]
        c2 = c2 + KSen3                                                        ' 0x141A47803 addps xmm2,[0x142629430]
        c3 = c3 * z                                                            ' 0x141A4780A mulps xmm3,xmm1
        c2 = c2 * z                                                            ' 0x141A4780D mulps xmm2,xmm1
        c3 = c3 * z                                                            ' 0x141A47810 mulps xmm3,xmm1
        c2 = c2 * y                                                            ' 0x141A47813 mulps xmm2,xmm5
        c3 = c3 - h                                                            ' 0x141A47816 subps xmm3,xmm0
        Dim usaSel As Boolean = (j2 = 0)                                       ' 0x141A47819 xorps ; 0x141A4781C pcmpeqd xmm10,xmm0
        c2 = c2 + y                                                            ' 0x141A47821 addps xmm2,xmm5
        c3 = c3 + 1.0F                                                         ' 0x141A4775C movaps xmm4,[0x1426294F0] ; 0x141A47828 addps xmm3,xmm4
        Dim a0 As Single = If(usaSel, c2, 0.0F)                                ' 0x141A47824 movaps xmm0,xmm10 ; 0x141A4782B andps xmm0,xmm2
        c2 = c2 - a0                                                           ' 0x141A4782E subps xmm2,xmm0
        Dim a10 As Single = If(usaSel, 0.0F, c3)                               ' 0x141A47831 andnps xmm10,xmm3
        c3 = c3 - a10                                                          ' 0x141A47835 subps xmm3,xmm10
        Dim s As Single = a10 + a0                                             ' 0x141A47839 addps xmm10,xmm0
        c2 = c2 + c3                                                           ' 0x141A47845 addps xmm2,xmm3
        s = If(s < 1.0F, s, 1.0F)                                              ' 0x141A4784F minps xmm10,xmm4
        c2 = If(c2 < 1.0F, c2, 1.0F)                                           ' 0x141A47853 minps xmm2,xmm4
        sel = Sng(Bits(s) Xor signoSel)                                        ' 0x141A47856 xorps xmm10,xmm11 ; 0x141A4785A andps [0x14271B480] (lanes pares)
        otro = Sng(Bits(c2) Xor bitC)                                          ' 0x141A47862 xorps xmm2,xmm7 ; 0x141A47865 andnps xmm3,xmm2 (lanes impares) ; 0x141A4786C orps
    End Sub

    ' ================================================================================= primitivas
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function Bits(x As Single) As UInteger
        Return BitConverter.SingleToUInt32Bits(x)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function Sng(u As UInteger) As Single
        Return BitConverter.UInt32BitsToSingle(u)
    End Function

    ''' <summary>`rcpps` exacto (ver el encabezado).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function Rcp(x As Single) As Single
        Return 1.0F / x
    End Function

    ''' <summary>`rsqrtps` exacto (ver el encabezado).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Shared Function RsqrtX(x As Single) As Single
        Return 1.0F / MathF.Sqrt(x)
    End Function

    ''' <summary>`cvttps2dq` / `cvttss2si`: trunca; NaN o fuera de rango → 0x80000000.</summary>
    Private Shared Function Cvtt(x As Single) As Integer
        If Single.IsNaN(x) OrElse x >= 2147483648.0F OrElse x < -2147483648.0F Then Return Integer.MinValue
        Return CInt(Math.Truncate(CDbl(x)))
    End Function

    Private Shared Function U32(x As Long) As UInteger
        Return CUInt(x And &HFFFFFFFFL)
    End Function

    Private Shared Function S32(x As Long) As Integer
        Dim u = x And &HFFFFFFFFL
        Return CInt(If(u >= &H80000000L, u - &H100000000L, u))
    End Function

    ''' <summary>La suma de cuatro cuadrados: `shufps 0x4e · addps · shufps 0xb1 · addps` (0x141A47A3C-0x141A47A4A).</summary>
    Private Shared Function Suma4(s0 As Single, s1 As Single, s2 As Single, s3 As Single) As Single
        Dim a As Single = s2 + s0                                              ' lane 0 de 0x141A47A40 addps xmm1,xmm0
        Dim b As Single = s3 + s1                                              ' lane 1 de 0x141A47A40
        Return b + a                                                           ' lane 0 de 0x141A47A4A addps xmm0,xmm1 (las cuatro lanes dan lo mismo)
    End Function

    Private Shared Function ConSigno(x As Single, negar As Boolean) As Single
        Return If(negar, Sng(Bits(x) Xor &H80000000UI), x)
    End Function

    ''' <summary>Tabla de máscaras por lane de 0x14270FB60 / 0x14271B430: lane k llena si el bit k está.</summary>
    Private Shared Function Mascara(i As Integer) As LanesSpline
        Return New LanesSpline(If((i And 1) <> 0, Sng(&HFFFFFFFFUI), 0.0F), If((i And 2) <> 0, Sng(&HFFFFFFFFUI), 0.0F),
                               If((i And 4) <> 0, Sng(&HFFFFFFFFUI), 0.0F), If((i And 8) <> 0, Sng(&HFFFFFFFFUI), 0.0F))
    End Function

    ''' <summary>0x14270FF20 / 0x14270FF30: {0,1,1,2, 1,2,2,3}.</summary>
    Private Shared Function PopCuenta(i As Integer) As Integer
        Return ((i And 1) + ((i >> 1) And 1) + ((i >> 2) And 1))
    End Function

    Private Shared Function AndL(a As LanesSpline, b As LanesSpline) As LanesSpline
        Return New LanesSpline(Sng(Bits(a.L0) And Bits(b.L0)), Sng(Bits(a.L1) And Bits(b.L1)), Sng(Bits(a.L2) And Bits(b.L2)), Sng(Bits(a.L3) And Bits(b.L3)))
    End Function

    ''' <summary>`andnps m, b` = ~m & b.</summary>
    Private Shared Function AndNotL(m As LanesSpline, b As LanesSpline) As LanesSpline
        Return New LanesSpline(Sng((Not Bits(m.L0)) And Bits(b.L0)), Sng((Not Bits(m.L1)) And Bits(b.L1)), Sng((Not Bits(m.L2)) And Bits(b.L2)), Sng((Not Bits(m.L3)) And Bits(b.L3)))
    End Function

    Private Shared Function OrL(a As LanesSpline, b As LanesSpline) As LanesSpline
        Return New LanesSpline(Sng(Bits(a.L0) Or Bits(b.L0)), Sng(Bits(a.L1) Or Bits(b.L1)), Sng(Bits(a.L2) Or Bits(b.L2)), Sng(Bits(a.L3) Or Bits(b.L3)))
    End Function

    Private Shared Function SubL(a As LanesSpline, b As LanesSpline) As LanesSpline
        Return New LanesSpline(a.L0 - b.L0, a.L1 - b.L1, a.L2 - b.L2, a.L3 - b.L3)
    End Function

    Private Shared Function AddL(a As LanesSpline, b As LanesSpline) As LanesSpline
        Return New LanesSpline(a.L0 + b.L0, a.L1 + b.L1, a.L2 + b.L2, a.L3 + b.L3)
    End Function

    Private Shared Function MulL(a As LanesSpline, b As LanesSpline) As LanesSpline
        Return New LanesSpline(a.L0 * b.L0, a.L1 * b.L1, a.L2 * b.L2, a.L3 * b.L3)
    End Function

    Private Shared Function MulE(a As LanesSpline, e As Single) As LanesSpline
        Return New LanesSpline(a.L0 * e, a.L1 * e, a.L2 * e, a.L3 * e)
    End Function

    Private Function LeerCuanto(p As Long, ancho As Integer) As Integer
        If ancho = 2 Then Return CInt(LeerU16(p))
        Return _d(CInt(p))
    End Function

    Private Function LeerU16(p As Long) As UShort
        Return BitConverter.ToUInt16(_d, CInt(p))
    End Function

    Private Function LeerU32(p As Long) As UInteger
        Return BitConverter.ToUInt32(_d, CInt(p))
    End Function

    Private Function LeerF32(p As Long) As Single
        Return BitConverter.ToSingle(_d, CInt(p))
    End Function

End Class
