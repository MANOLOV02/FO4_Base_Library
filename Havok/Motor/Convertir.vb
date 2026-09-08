Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclInputConvertOperator` (type **14**, `0x14195E540`) y `hclOutputConvertOperator`
' (type **15**, `0x14195EA20`).
'
' Son los DOS ÚNICOS operadores que hablan con el buffer del USUARIO — el que trae el vértice
' empaquetado— y su trabajo es traducirlo al buffer SOMBRA de floats y de vuelta. Todo el resto del
' motor trabaja sobre la sombra.
'
' ⭐⭐ LA ENTRADA DEL DESPACHADOR — `0x1418C6233` (14) y `0x1418C6257` (15), leídas enteras:
'
'     14: rdx = buffers[ op.userBufferIndex   (+0x20) ]      ⬅ ÍNDICE DIRECTO, sin el `+0x100`
'         r8  = buffers[ op.shadowBufferIndex (+0x24) ]
'     15: r8  = buffers[ op.userBufferIndex   (+0x20) ]
'         rdx = buffers[ op.shadowBufferIndex (+0x24) ]
'
' ⛔ **NO hay doble indirección acá.** El resto de los operadores hace
' `buffers[buffers[idx].Ranura]`; estos dos toman el buffer de la ranura y listo. Meterle la doble
' indirección «por consistencia» sería inventar.
'
' ⭐⭐ EL BUCLE (`0x14195E5CB`-`0x14195E890` y `0x14195EAB6`-`0x14195EE12`):
'
'     por cada e de conversionInfo.elementConversions[0 .. numElementsConverted−1]:
'         canal   = e.index        ' 0 = posiciones · 1 = normales · 2 = tangentes · 3 = bitangentes
'         formato = e.conversion
'         n       = buffer del USUARIO . canal . numVertices      ⬅ el conteo sale del USUARIO
'         por cada vértice: traducir según `formato`, avanzando cada lado por SU stride
'
' ⛔ `conversionInfo.slotConversions` (y con él `numSlotsConverted`) **no lo mira ninguno de los
' dos**: la reflexión lo declara y el `execute` no lo lee. Se dice, no se rellena de sentido.
'
' ⛔ `e.offset` (+1 de la terna) **tampoco se lee**: los dos punteros salen del canal, sin sumarle
' nada (`0x14195E5FD` y `0x14195EAFD`). La terna es de TRES bytes y el bucle avanza `add rdi, 3`.
'
' ⭐⭐ `VectorConversion` — el enum sale de la tabla de reflexión del `.exe` (`VC_FLOAT4`,
' `VC_FLOAT3`, `VC_BYTE4`, `VC_SHORT3`, `VC_HFLOAT3`, `VC_CUSTOM_A`…`E`, `VC_NONE`) y coincide con
' el orden del despacho:
'
'     0 VC_FLOAT4   entrada `0x14195E870` 16 B tal cual   · salida `0x14195EDF0` 16 B tal cual
'     1 VC_FLOAT3   entrada `0x14195E840` 12 B → w = 0    · salida `0x14195EDB0` 12 B
'     2 VC_BYTE4    entrada `0x14195E7E0` `b·(2/255) − 1` · salida `0x14195ED10` `v·127,5 + 127,5`
'                   con la conversión SATURADA a `uint32` y el byte bajo
'     3 VC_SHORT3   entrada `0x14195E780` `int16 / 32767` · salida `0x14195ECA0` `trunc(min(v,1)·32767)`
'     4 VC_HFLOAT3  entrada `0x14195E660` half → float    · salida `0x14195EB60` float → half
'     ≥5            no hacen NADA (`0x14195E63A` / `0x14195EB3A`, los dos `jne` al final del bucle)
'
' ⛔⛔ **ACÁ SÍ HAY UN 32767** (`0x142496F68` a la salida, `0x14270BE20` = `1/32767` a la entrada).
' No lo confundas con la desquantización de la piel, que es `float(int16 << 16) · bitcast(w << 16)`
' y NO divide por 32767: son dos leyes distintas, en dos operadores distintos.
'
' ⛔ El `VC_BYTE4` de salida **no tiene `minps` contra 1**: satura por arriba a `uint32` y se queda
' con el byte bajo, así que un valor mayor que 1 no se recorta, se envuelve. El `VC_SHORT3` de
' salida sí trae `minps` contra `{1,1,1,1}` (`0x14195ECA7`) pero **no** un `maxps` contra −1.
'
' ⚠️ CERO apariciones de las dos clases en el corpus vanilla (el censo M1 da siete clases de
' operador y ninguna es ésta). Van igual: son de la lista cerrada del despachador.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>`hclRuntimeConversionInfo::VectorConversion` — la tabla de reflexión del `.exe`.</summary>
    Friend Enum ConversionDeVector
        Float4 = 0
        Float3 = 1
        Byte4 = 2
        Short3 = 3
        HalfFloat3 = 4
        CustomA = 5
        CustomB = 6
        CustomC = 7
        CustomD = 8
        CustomE = 9
        Ninguna = 10
    End Enum

    ''' <summary>Una terna de `elementConversions` — `{index, offset, conversion}`, TRES bytes
    ''' (`0x14195E88A` `add rdi, 3`).</summary>
    Friend Structure ElementoDeConversion
        ''' <summary>El canal: 0 posiciones · 1 normales · 2 tangentes · 3 bitangentes.</summary>
        Friend Canal As Integer
        ''' <summary>⛔ Declarado en la reflexión y NO leído por ninguno de los dos `execute`.</summary>
        Friend Offset As Integer
        Friend Conversion As Integer
    End Structure

    ''' <summary>`hclInputConvertOperator` / `hclOutputConvertOperator` — types 14 y 15.</summary>
    Friend NotInheritable Class ConversionCompilada

        ''' <summary>`userBufferIndex` (+0x20) — el buffer con el vértice EMPAQUETADO.</summary>
        Friend ReadOnly BufferDelUsuario As Integer

        ''' <summary>`shadowBufferIndex` (+0x24) — la sombra de floats.</summary>
        Friend ReadOnly BufferSombra As Integer

        ''' <summary>`conversionInfo.elementConversions`, recortado a `numElementsConverted`.</summary>
        Friend ReadOnly Elementos As ElementoDeConversion()

        Friend Sub New(bufferDelUsuario As Integer, bufferSombra As Integer,
                       elementos As ElementoDeConversion())
            Me.BufferDelUsuario = bufferDelUsuario
            Me.BufferSombra = bufferSombra
            Me.Elementos = If(elementos, Array.Empty(Of ElementoDeConversion)())
        End Sub

    End Class

    ''' <summary>`hclInputConvertOperator` — type 14: del buffer del usuario a la sombra.</summary>
    Friend NotInheritable Class OpConvertirEntrada
        Inherits OperadorCompilado

        Friend ReadOnly Conv As ConversionCompilada

        Friend Sub New(conv As ConversionCompilada, nombre As String)
            MyBase.New(14, nombre)                      ' la tabla de `0x1418C6390`
            Me.Conv = conv
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If Conv Is Nothing OrElse ctx.Buffers Is Nothing Then Return
            Dim usuario = BufferDirecto(ctx.Buffers, Conv.BufferDelUsuario)   ' 0x1418C623E, DIRECTO
            Dim sombra = BufferDirecto(ctx.Buffers, Conv.BufferSombra)        ' 0x1418C623A
            If usuario Is Nothing OrElse sombra Is Nothing Then Return
            Convertir.Decodificar(Conv, usuario, sombra)
        End Sub

        ''' <summary>⛔ El índice es DIRECTO: `buffers[idx]`, sin el salto por `+0x100` que hacen
        ''' los demás operadores (`0x1418C6242`/`46` y `0x1418C6266`/`6A`).</summary>
        Friend Shared Function BufferDirecto(buffers As Buffer(), idx As Integer) As Buffer
            If buffers Is Nothing OrElse idx < 0 OrElse idx >= buffers.Length Then Return Nothing
            Return buffers(idx)
        End Function

    End Class

    ''' <summary>`hclOutputConvertOperator` — type 15: de la sombra al buffer del usuario.</summary>
    Friend NotInheritable Class OpConvertirSalida
        Inherits OperadorCompilado

        Friend ReadOnly Conv As ConversionCompilada

        Friend Sub New(conv As ConversionCompilada, nombre As String)
            MyBase.New(15, nombre)
            Me.Conv = conv
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If Conv Is Nothing OrElse ctx.Buffers Is Nothing Then Return
            Dim usuario = OpConvertirEntrada.BufferDirecto(ctx.Buffers, Conv.BufferDelUsuario)
            Dim sombra = OpConvertirEntrada.BufferDirecto(ctx.Buffers, Conv.BufferSombra)
            If usuario Is Nothing OrElse sombra Is Nothing Then Return
            Convertir.Codificar(Conv, sombra, usuario)
        End Sub

    End Class

    ''' <summary>Las dos traducciones, con la tabla de formatos del `.exe`.</summary>
    Friend Module Convertir

        ''' <summary>`1/32767` — `0x14270BE20`, el `{3,05185094e-05 ×4}` del `VC_SHORT3` de entrada.</summary>
        Friend Const InvShort As Single = 3.05185094E-05F

        ''' <summary>`32767` — `0x142496F68`, el escalar del `VC_SHORT3` de salida.</summary>
        Friend Const EscalaShort As Single = 32767.0F

        ''' <summary>`2/255` — `0x142F5AE80`, el `{0,00784313772 ×4}` del `VC_BYTE4` de entrada.</summary>
        Friend Const DosSobre255 As Single = 0.00784313772F

        ''' <summary>`127,5` — `0x142F5AE90`, el del `VC_BYTE4` de salida.</summary>
        Friend Const Ciento27Coma5 As Single = 127.5F

        ''' <summary>
        ''' `112 << 23` — el sesgo de exponente que separa al `half` del `float`, escrito como
        ''' patrón de bits porque así está en el `.exe`: `add ecx, 0x38000000` en `0x14195E69C`.
        ''' </summary>
        Private Const SesgoDeExponenteAFloat As Integer = &H38000000

        ''' <summary>`112` — el mismo sesgo del otro lado: `lea eax, [rcx - 0x70]` en
        ''' `0x14195EB7F`, sobre el exponente de ocho bits del `float`.</summary>
        Private Const SesgoDeExponenteAHalf As Integer = &H70

        ' -----------------------------------------------------------------------------------------
        ' type 14 — `0x14195E540`
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `hclInputConvertOperator::execute` — el buffer del usuario, desempaquetado a la sombra.
        ''' <para>⛔ El conteo de vértices sale del canal del buffer del USUARIO
        ''' (`0x14195E605`, `[rcx+8]` con `rcx` apuntando a la tabla de la ENTRADA).</para>
        ''' </summary>
        Friend Sub Decodificar(c As ConversionCompilada, usuario As Buffer, sombra As Buffer)
            For Each e In c.Elementos
                Dim i = e.Canal
                If i < 0 OrElse i > 3 Then Continue For
                Dim src = Buffers.BytesDeCanal(usuario, i)
                Dim dst = Buffers.FloatsDeCanal(sombra, i)
                If src Is Nothing OrElse dst Is Nothing Then Continue For
                Dim pasoSrc = Buffers.StrideDeCanal(usuario, i)
                Dim pasoDst = Buffers.StrideDeCanal(sombra, i) \ 4
                Dim n = Buffers.CuentaDeCanal(usuario, i)                 ' 0x14195E605
                If pasoSrc <= 0 OrElse pasoDst <= 0 Then Continue For

                For v = 0 To n - 1
                    Dim so = pasoSrc * v
                    Dim d = pasoDst * v
                    If d + 3 >= dst.Length Then Exit For
                    Select Case e.Conversion
                        Case ConversionDeVector.Float4                    ' 0x14195E870, 16 B
                            If so + 15 >= src.Length Then Exit For
                            For k = 0 To 3
                                dst(d + k) = BitConverter.ToSingle(src, so + k * 4)
                            Next
                        Case ConversionDeVector.Float3                    ' 0x14195E840, 12 B + w = 0
                            If so + 11 >= src.Length Then Exit For
                            dst(d) = BitConverter.ToSingle(src, so)
                            dst(d + 1) = BitConverter.ToSingle(src, so + 4)
                            dst(d + 2) = BitConverter.ToSingle(src, so + 8)
                            dst(d + 3) = 0.0F                             ' 0x14195E84E movlhps
                        Case ConversionDeVector.Byte4                     ' 0x14195E7E0
                            If so + 3 >= src.Length Then Exit For
                            ' `cvtdq2ps` de los cuatro bytes, `·(2/255)` y `−1` — 0x14195E80C/11/18
                            Dim b = Vector128.Create(CSng(src(so)), CSng(src(so + 1)),
                                                     CSng(src(so + 2)), CSng(src(so + 3)))
                            Dim r = Vector128.Add(
                                Vector128.Multiply(b, Vector128.Create(DosSobre255)),
                                Vector128.Create(-1.0F))
                            For k = 0 To 3
                                dst(d + k) = r.GetElement(k)
                            Next
                        Case ConversionDeVector.Short3                    ' 0x14195E780
                            If so + 5 >= src.Length Then Exit For
                            Dim s3 = Vector128.Create(CSng(BitConverter.ToInt16(src, so)),
                                                      CSng(BitConverter.ToInt16(src, so + 2)),
                                                      CSng(BitConverter.ToInt16(src, so + 4)), 0.0F)
                            Dim r3 = Vector128.Multiply(s3, Vector128.Create(InvShort))
                            For k = 0 To 3
                                dst(d + k) = r3.GetElement(k)
                            Next
                        Case ConversionDeVector.HalfFloat3                ' 0x14195E660
                            If so + 5 >= src.Length Then Exit For
                            dst(d) = DeMedio(BitConverter.ToUInt16(src, so))
                            dst(d + 1) = DeMedio(BitConverter.ToUInt16(src, so + 2))
                            dst(d + 2) = DeMedio(BitConverter.ToUInt16(src, so + 4))
                            ' ⛔ el motor escribe TRES dwords y no toca el cuarto (0x14195E6A4/F3/743)
                    End Select
                Next
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' type 15 — `0x14195EA20`
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `hclOutputConvertOperator::execute` — la sombra, empaquetada al buffer del usuario.
        ''' <para>⛔ El conteo sale otra vez del canal del USUARIO (`0x14195EB05`), que acá es el
        ''' DESTINO.</para>
        ''' </summary>
        Friend Sub Codificar(c As ConversionCompilada, sombra As Buffer, usuario As Buffer)
            For Each e In c.Elementos
                Dim i = e.Canal
                If i < 0 OrElse i > 3 Then Continue For
                Dim src = Buffers.FloatsDeCanal(sombra, i)
                Dim dst = Buffers.BytesDeCanal(usuario, i)
                If src Is Nothing OrElse dst Is Nothing Then Continue For
                Dim pasoSrc = Buffers.StrideDeCanal(sombra, i) \ 4
                Dim pasoDst = Buffers.StrideDeCanal(usuario, i)
                Dim n = Buffers.CuentaDeCanal(usuario, i)                 ' 0x14195EB05
                If pasoSrc <= 0 OrElse pasoDst <= 0 Then Continue For

                For v = 0 To n - 1
                    Dim s = pasoSrc * v
                    Dim d = pasoDst * v
                    If s + 3 >= src.Length Then Exit For
                    ' ⛔ el motor lee 16 B con `movups` sea cual sea el stride
                    ' (`0x14195ED21`, `0x14195ECA0`); acá la guarda de arriba corta antes de
                    ' pasarse del arreglo, que es lo único que no se puede replicar.
                    Dim vec = Vector128.Create(src(s), src(s + 1), src(s + 2), src(s + 3))
                    Select Case e.Conversion
                        Case ConversionDeVector.Float4                    ' 0x14195EDF0, 16 B
                            If d + 15 >= dst.Length Then Exit For
                            For k = 0 To 3
                                BitConverter.GetBytes(src(s + k)).CopyTo(dst, d + k * 4)
                            Next
                        Case ConversionDeVector.Float3                    ' 0x14195EDB0, 12 B
                            If d + 11 >= dst.Length Then Exit For
                            For k = 0 To 2
                                BitConverter.GetBytes(src(s + k)).CopyTo(dst, d + k * 4)
                            Next
                        Case ConversionDeVector.Byte4                     ' 0x14195ED10
                            If d + 3 >= dst.Length Then Exit For
                            Dim u = ASinSignoSaturado(vec)
                            For k = 0 To 3
                                dst(d + k) = CByte(u.GetElement(k) And 255UI)
                            Next
                        Case ConversionDeVector.Short3                    ' 0x14195ECA0
                            If d + 5 >= dst.Length Then Exit For
                            ' `minps` contra 1 y `cvttss2si` de `v·32767`, sin `maxps` contra −1
                            Dim m = Vector128.Min(vec, Vector128.Create(1.0F))   ' 0x14195ECA7
                            For k = 0 To 2
                                ' ⛔ `mov word ptr [r8], ax` (`0x14195ECC8`) guarda los DIECISÉIS bits
                                ' bajos del entero, sin comprobar nada: con −2 el `cvttss2si` da
                                ' −65534 y lo que queda en la palabra es +2. Un `CShort` ahí
                                ' revienta por desbordamiento, que es una ley que el motor NO tiene.
                                Dim q = ATruncado(m.GetElement(k) * EscalaShort)
                                dst(d + k * 2) = CByte(q And &HFF)
                                dst(d + k * 2 + 1) = CByte((q >> 8) And &HFF)
                            Next
                        Case ConversionDeVector.HalfFloat3                ' 0x14195EB60
                            If d + 5 >= dst.Length Then Exit For
                            For k = 0 To 2
                                BitConverter.GetBytes(AMedio(src(s + k))).CopyTo(dst, d + k * 2)
                            Next
                    End Select
                Next
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' Las tres piezas de aritmética propias
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `cvttss2si` — trunca hacia cero, y si no entra en 32 bits devuelve el «entero
        ''' indefinido» `0x80000000`, que es lo que el x86 pone. Sitios: `0x14195ECC0`, `0x14195ECCC`
        ''' y `0x14195ECD9`, los tres del `VC_SHORT3` de salida.
        ''' </summary>
        Private Function ATruncado(x As Single) As Integer
            ' ⭐ LA LEY VIVE EN `Simd.ATruncado`: el mismo `cvttss2si` lo usan el `exp` del terreno
            ' (`0x141A15935`) y la cuantización de la broadphase (`0x141A1518B`).
            Return Simd.ATruncado(x)
        End Function

        ''' <summary>
        ''' `float → uint32` SATURADA, la de MSVC — `0x14195ED10`-`0x14195ED56`, tal cual.
        ''' <para>```
        ''' x  = v·127,5 + 127,5                     ' 0x14195ED21/2E
        ''' m31 = (2³¹ &lt;= x) ; m32 = (2³² &lt;= x)     ' 0x14195ED35/39 cmpleps
        ''' alto = m31 &lt;&lt; 31                        ' 0x14195ED40 pslld
        ''' x  = max(x − (m31 AND 2³¹), 0)           ' 0x14195ED45/48/4B
        ''' r  = (trunc(x) + alto) OR m32            ' 0x14195ED4E/52/56
        ''' ```</para>
        ''' <para>⛔ No hay `minps` contra 1 antes: el que se pasa de 1 no se recorta.</para>
        ''' </summary>
        Friend Function ASinSignoSaturado(v As Vector128(Of Single)) As Vector128(Of UInteger)
            Dim c = Vector128.Create(Ciento27Coma5)
            Dim dosA31 = Vector128.Create(2147483648.0F)                  ' 0x14262BA60
            Dim x = Vector128.Add(Vector128.Multiply(c, v), c)
            Dim m31 = Vector128.LessThanOrEqual(dosA31, x)                ' 0x14195ED35
            Dim m32 = Vector128.LessThanOrEqual(Vector128.Add(dosA31, dosA31), x)  ' 0x14195ED39
            Dim alto = Vector128.ShiftLeft(m31.AsInt32(), 31)             ' 0x14195ED40
            x = Vector128.Subtract(x, Vector128.BitwiseAnd(m31, dosA31))  ' 0x14195ED45/48
            x = Vector128.Max(x, Vector128(Of Single).Zero)               ' 0x14195ED4B
            Dim r = Vector128.Add(Vector128.ConvertToInt32(x), alto)      ' 0x14195ED4E/52
            Return Vector128.BitwiseOr(r, m32.AsInt32()).AsUInt32()       ' 0x14195ED56
        End Function

        ''' <summary>
        ''' `half → float` — `0x14195E660`-`0x14195E6A4`.
        ''' <para>```
        ''' si exp = 0 y mant = 0 : d = sgn &lt;&lt; 31              ' cero CON signo
        ''' si exp = 0 y mant ≠ 0 : se normaliza a mano          ' 0x14195E681-0x14195E68D
        ''' d = ((sgn &lt;&lt; 18 OR mant) &lt;&lt; 13) OR ((exp &lt;&lt; 23) + 0x38000000)
        ''' ```</para>
        ''' </summary>
        Friend Function DeMedio(h As UShort) As Single
            Dim sgn As UInteger = CUInt(h) >> 15                          ' 0x14195E668
            Dim mant As UInteger = CUInt(h) And &H3FFUI                   ' 0x14195E66B
            Dim e As Integer = (CInt(h) >> 10) And &H1F                   ' 0x14195E670/73
            If e = 0 Then
                If mant = 0UI Then
                    Return BitConverter.UInt32BitsToSingle(sgn << 31)       ' 0x14195E67C
                End If
                Do
                    mant += mant                                          ' 0x14195E681
                    e -= 1                                                ' 0x14195E683
                Loop Until (mant And &H400UI) <> 0UI                       ' 0x14195E685 bt/jae
                e += 1                                                     ' 0x14195E68B
                mant = mant And &HFFFFFBFFUI                               ' 0x14195E68D btr
            End If
            Dim d As UInteger = ((sgn << 18) Or mant) << 13                ' 0x14195E691/94/99
            d = d Or CUInt((e << 23) + SesgoDeExponenteAFloat)             ' 0x14195E696/9C/A2
            Return BitConverter.UInt32BitsToSingle(d)
        End Function

        ''' <summary>
        ''' `float → half` — `0x14195EB60`-`0x14195EBB4`.
        ''' <para>```
        ''' e = (d &gt;&gt; 23) AND 0xFF ; sgn = (d &gt;&gt; 16) AND 0x8000 ; mant = d AND 0x7FFFFF
        ''' k = e − 0x70
        ''' k &gt; 0   : h = (mant &gt;&gt; 13) OR ((k &lt;&lt; 10) AND 0xFFFF) OR sgn
        ''' k &lt; −10 : h = 0                                    ' ⛔ CERO SIN SIGNO
        ''' resto   : mant OR= 1&lt;&lt;23 ; mant &gt;&gt;= (0x71 − e) ; mant &gt;&gt;= 13 ; h = mant OR sgn
        ''' ```</para>
        ''' <para>⛔ No hay saturación a infinito ni caso de NaN: con `k` grande el `shl ax, 0xA`
        ''' desborda los 16 bits y se queda con lo que quede. Es lo que el motor hace.</para>
        ''' </summary>
        Friend Function AMedio(f As Single) As UShort
            Dim d As UInteger = BitConverter.SingleToUInt32Bits(f)
            Dim e As Integer = CInt((d >> 23) And &HFFUI)                  ' 0x14195EB65/75 movzx al
            Dim sgn As UInteger = (d >> 16) And &H8000UI                   ' 0x14195EB6B/78
            Dim mant As Integer = CInt(d And &H7FFFFFUI)                   ' 0x14195EB6F
            Dim k = e - SesgoDeExponenteAHalf                              ' 0x14195EB7F
            If k > 0 Then                                                  ' 0x14195EB84 jg
                mant = CInt(CUInt(mant) >> 13)                             ' 0x14195EBA6 shr
                Return CUShort(((CUInt(mant) Or (CUInt(k << 10) And &HFFFFUI)) Or sgn) And &HFFFFUI)
            End If
            If k < -10 Then Return 0US                                     ' 0x14195EB86/8B
            mant = mant Or &H800000                                        ' 0x14195EB91 bts
            mant >>= (&H71 - e)                                            ' 0x14195EB9A/9F sar
            mant >>= 13                                                    ' 0x14195EBA1 sar
            Return CUShort((CUInt(mant) Or sgn) And &HFFFFUI)              ' 0x14195EBB0
        End Function

    End Module

End Namespace

#End If
