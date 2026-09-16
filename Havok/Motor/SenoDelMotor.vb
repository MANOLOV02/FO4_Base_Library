Option Strict On
Option Explicit On

' =================================================================================================
' EL SENO QUE TRAE EL `.exe` — `sincosf` (`0x1422BF150`) y su kernel vectorial `0x1422BFFF0`.
' Lo usa la curva de ráfaga del viento (`0x1418C4090`). No es `MathF.Sin`: es un polinomio SIMD con π
' partido en cuatro, un camino en `double` para |x| > 10000 y uno escalar con tabla para |x| > 1,07e9.
' Transcrito instrucción por instrucción; lo valida el diferencial GDFs contra el código emulado.
' ⛔ La aritmética entera de 32 bits DESBORDA a propósito (como el `add`/`shl` del `.exe`): va por
' `ULong` con máscara, porque VB verifica desbordes.
' =================================================================================================

Namespace Havok.Motor

    Friend Module SenoDelMotor

        ' 0x1440F5560 … 0x1440F55F0 (float, las cuatro lanes iguales)
        Private ReadOnly C560 As Single = BitsF(&HB94FBAF1UI)
        Private ReadOnly C570 As Single = BitsF(&HBE2AAAA5UI)
        Private ReadOnly C580 As Single = BitsF(&H3C088773UI)
        Private ReadOnly C590 As Single = BitsF(&H362F0519UI)
        Private ReadOnly C5A0 As Single = BitsF(&H34222000UI)
        Private ReadOnly C5B0 As Single = BitsF(&H2CB4611AUI)
        Private ReadOnly C5C0 As Single = BitsF(&H3A7DA000UI)
        Private ReadOnly C5D0 As Single = BitsF(&H40490000UI)
        Private ReadOnly C5F0 As Single = BitsF(&H3EA2F983UI)
        Private ReadOnly CuartoPi As Single = BitsF(&H3FC90FDBUI)             ' 0x14291FA90 (π/2)
        Private ReadOnly Chico As Single = BitsF(&H39000000UI)                ' 0x14291FA80
        Private Const UmbralGrande As UInteger = &H461C4000UI                 ' 0x1440F55E0 = 10000,0
        Private Const UmbralEnorme As UInteger = &H4E800000UI                 ' 0x1440F56C0
        ' 0x1440F5620 … 0x1440F56A0 (double)
        Private ReadOnly D620 As Double = BitsD(&H3A945C06E0E68948UL)
        Private ReadOnly D630 As Double = BitsD(&HBC03B39A00000000UL)
        Private ReadOnly D640 As Double = BitsD(&HBD473DCA00000000UL)
        Private ReadOnly D650 As Double = BitsD(&H3EB5444300000000UL)
        Private ReadOnly D660 As Double = BitsD(&H3FF921FA00000000UL)
        Private ReadOnly D6A0 As Double = BitsD(&H3FE45F306DC9C883UL)
        Private Const Q670 As ULong = &H4338000000000000UL

        ''' <summary>La tabla del camino escalar, `0x1440F5000` … `0x1440F555F` (172 `double`).</summary>
        Private ReadOnly Tabla As ULong() = {
            &H3EC4ABBCE625BE53UL, &H0000000000000000UL, &H3EC4AA24E4ABFC7BUL, &H3EDF00CF576F4616UL,
            &H3EC4A55D1F2953FBUL, &H3EEEFE6B64F35F47UL, &H3EC49D665253E82DUL, &H3EF73BD3CBCA663FUL,
            &H3EC49241B8904918UL, &H3EFEF4DCB614F93BUL, &H3EC483F109C0F853UL, &H3F03548FC987C88BUL,
            &H3EC472767B0293F2UL, &H3F072BB620714C1BUL, &H3EC45DD4BE54B1EAUL, &H3F0AFF49C4876484UL,
            &H3EC4460F022F7964UL, &H3F0ECEB3A79778C1UL, &H3EC42B28F1060A65UL, &H3F114CAEAFE38D93UL,
            &H3EC40D26B0B5C728UL, &H3F132F589F62E99FUL, &H3EC3EC0CE1E2957BUL, &H3F150F0D3470FE1DUL,
            &H3EC3C7E09F40315EUL, &H3F16EB8275FE9475UL, &H3EC3A0A77CC8AD12UL, &H3F18C46EEB2E4178UL,
            &H3EC3766786E03DADUL, &H3F1A9989A6A8D058UL, &H3EC3492741667619UL, &H3F1C6A8A51DC29A7UL,
            &H3EC318EDA6B5156CUL, &H3F1E37293822FA8AUL, &H3EC2E5C2268C901DUL, &H3F1FFF1F51D363A7UL,
            &H3EC2AFACA4EE7EB5UL, &H3F20E11327997DB1UL, &H3EC276B578E61F29UL, &H3F21BFFC51A7397CUL,
            &H3EC23AE56B3F18E9UL, &H3F229C28C759993FUL, &H3EC1FC45B52AB676UL, &H3F2375769504A22BUL,
            &H3EC1BADFFED3C9E8UL, &H3F244BC43845F6D1UL, &H3EC176BE5DE17496UL, &H3F251EF0A52FAB2BUL,
            &H3EC12FEB53E90CA0UL, &H3F25EEDB4B60D478UL, &H3EC0E671CCCF5DC5UL, &H3F26BB641B0B0BBFUL,
            &H3EC09A5D1D198579UL, &H3F27846B89E41CC1UL, &H3EC04BB9002DACB2UL, &H3F2849D298031E27UL,
            &H3EBFF5232D07C8DCUL, &H3F290B7AD4A832EFUL, &H3EBF4DE6C78ED89EUL, &H3F29C94662EE386DUL,
            &H3EBEA1D699D15A63UL, &H3F2A8317FE65A776UL, &H3EBDF10D2C4067B1UL, &H3F2B38D2FF97F311UL,
            &H3EBD3BA5C1C5F47EUL, &H3F2BEA5B6072B262UL, &H3EBC81BC5390A085UL, &H3F2C9795C099E776UL,
            &H3EBBC36D8CC36D57UL, &H3F2D406769A0B84FUL, &H3EBB00D6C60A0365UL, &H3F2DE4B65327F38BUL,
            &H3EBA3A1601123487UL, &H3F2E846926E1BE09UL, &H3EB96F49E3EB6EB3UL, &H3F2F1F674479CB19UL,
            &H3EB8A091B44CD57BUL, &H3F2FB598C56184E8UL, &H3EB7CE0D52C2BCF1UL, &H3F302373403FC7ACUL,
            &H3EB6F7DD35C4444CUL, &H3F30699D06E109D7UL, &H3EB61E2264B1D25CUL, &H3F30AD3EE4C9A419UL,
            &H3EB540FE72BD3967UL, &H3F30EE4E6C17D59AUL, &H3EB4609379BC4B86UL, &H3F312CC1946EB6F9UL,
            &H3EB37D0414E6ABD8UL, &H3F31688EBC824815UL, &H3EB296735B7FAC32UL, &H3F31A1ACAB939934UL,
            &H3EB1AD04DB6D09EDUL, &H3F31D81292DCD4D9UL, &H3EB0C0DC93BB5F68UL, &H3F320BB80EECF225UL,
            &H3EAFA43DDE224580UL, &H3F323C9528F2DA2DUL, &H3EADC1E17C22199BUL, &H3F326AA257F7CD38UL,
            &H3EABDAEE63598C3DUL, &H3F3295D88208D757UL, &H3EA9EFAFAAD1740BUL, &H3F32BE30FD4F268DUL,
            &H3EA800711323E456UL, &H3F32E3A591171745UL, &H3EA60D7EFACDBE48UL, &H3F33063076C5CE7DUL,
            &H3EA417265267E951UL, &H3F3325CC5ABD3BD7UL, &H3EA21DB490CA03B5UL, &H3F3342745D2E6057UL,
            &H3EA02177A7185F8FUL, &H3F335C2412D9B972UL, &H3E9C457BE97E47CCUL, &H3F3372D785BDB2ABUL,
            &H3E9843AC76B8D885UL, &H3F33868B35B302F3UL, &H3E943E1F253687C6UL, &H3F33973C18F6DD96UL,
            &H3E903572B802B7EAUL, &H3F33A4E79CA2E178UL, &H3E88548CDAC5793CUL, &H3F33AF8BA512B41CUL,
            &H3E803A73CC8EFFA6UL, &H3F33B7268E3738C4UL, &H3E703BB436BFBE01UL, &H3F33BBB72BD756E5UL,
            &H0000000000000000UL, &H3F33BD3CC9BE45DEUL, &H40445F306D3E7939UL, &H3D7054A7F09D5F48UL,
            &H40445F306D3E7939UL, &H3D7054A7F09D5F48UL, &H3FE7CC1B723E0C88UL, &H3D1529FC2757D1F5UL,
            &H3F5836E4E43D7054UL, &H3CA4FE13ABE8FA9AUL, &H3EEB7272203D1529UL, &H3C4F84EAFA3EA69CUL,
            &H3E627220A93C83F8UL, &H3BA3ABE8FA9A6EE0UL, &H3DE220A94F3C1C27UL, &H3B35F47D4D377037UL,
            &H3D1529FC273B35F4UL, &H3A5F534DDC0DB629UL, &H3CD29FC2753AFF46UL, &H3A3D4D377036D8A5UL,
            &H3C5FC2757D3A5F50UL, &H39AA6EE06DB14ACDUL, &H3BA3ABE8FA39D34DUL, &H390B81B6C52B3279UL,
            &H3B6ABE8FA93994DDUL, &H38C81B6C52B32788UL, &H3AEE8FA9A6391DC0UL, &H384B6C52B3278872UL,
            &H3A5F534DDC384B60UL, &H37B8A5664F10E410UL, &H39D34DDC0D3806C5UL, &H3715993C439041FEUL,
            &H3FEFFFFFFF800000UL, &H0000000000000000UL, &H40445F306E000000UL, &H0000000000000000UL,
            &H4338000000000000UL, &H0000000000000000UL, &HBE5B1BBEAD603D8BUL, &H0000000000000000UL,
            &H40C37423899A1558UL, &H40A9F02F6222C720UL, &HFFFFFFFFFF000000UL, &H0000000000000000UL
        }

        Private Function BitsF(u As UInteger) As Single
            Return BitConverter.ToSingle(BitConverter.GetBytes(u), 0)
        End Function

        Private Function BitsD(u As ULong) As Double
            Return BitConverter.ToDouble(BitConverter.GetBytes(u), 0)
        End Function

        Private Function U32(f As Single) As UInteger
            Return BitConverter.ToUInt32(BitConverter.GetBytes(f), 0)
        End Function

        Private Function U64(d As Double) As ULong
            Return BitConverter.ToUInt64(BitConverter.GetBytes(d), 0)
        End Function

        Private Function T(offsetBytes As Integer) As Double
            Return BitsD(Tabla(offsetBytes \ 8))
        End Function

        ''' <summary>`cvtps2dq`: al par más cercano; fuera de rango o NaN = 0x80000000.</summary>
        Private Function Cvtps2dq(v As Single) As UInteger
            If Single.IsNaN(v) Then Return &H80000000UI
            Dim r = Math.Round(CDbl(v), MidpointRounding.ToEven)
            ' el entero indefinido del x86 fuera de int32 — `cvtps2dq` de 0x1422C0049 / 0x1422C00E4
            If r >= 2147483648.0R OrElse r < -2147483648.0R Then Return &H80000000UI
            Return BitConverter.ToUInt32(BitConverter.GetBytes(CInt(r)), 0)
        End Function

        ''' <summary>`cvttpd2dq`: trunca; fuera de rango o NaN = 0x80000000.</summary>
        Private Function Cvttpd2dq(v As Double) As UInteger
            If Double.IsNaN(v) Then Return &H80000000UI
            Dim r = Math.Truncate(v)
            ' el entero indefinido del x86 fuera de int32 — `cvttpd2dq` de 0x1422C0167 / 0x1422C0280
            If r >= 2147483648.0R OrElse r < -2147483648.0R Then Return &H80000000UI
            Return BitConverter.ToUInt32(BitConverter.GetBytes(CInt(r)), 0)
        End Function

        ''' <summary>`cvtdq2ps`: el entero de 32 bits CON SIGNO a float.</summary>
        Private Function DqAPs(u As UInteger) As Single
            Return CSng(BitConverter.ToInt32(BitConverter.GetBytes(u), 0))
        End Function

        ''' <summary>
        ''' `sincosf` — `0x1422BF150`: |x| &lt; 0,000122070312 ⇒ `(x, 1 − (|x|·|x|)·0,5)`; si no, el
        ''' kernel vectorial sobre `(x, |x| + π/2, 0, 0)` (`0x1422BF16D`/`0x1422BF175`; las lanes altas
        ''' vienen en 0 del `xorps`/`movss` del llamador `0x1418C409C`/`0x1418C40A5`).
        ''' </summary>
        Friend Sub SinCos(x As Single, ByRef s As Single, ByRef c As Single)
            Dim ax = BitsF(U32(x) And &H7FFFFFFFUI)                          ' 0x1422BF154
            If ax < Chico Then                                               ' 0x1422BF15C comiss / jb
                s = x                                                        ' 0x1422BF192 unpcklps
                c = 1.0F - (ax * ax) * 0.5F                                  ' 0x1422BF17D…18F
                Return
            End If
            Dim v = Seno4({x, ax + CuartoPi, 0.0F, 0.0F})                   ' 0x1422BF16D / 0x1422BF178
            s = v(0) : c = v(1)
        End Sub

        ''' <summary>El kernel vectorial `0x1422BFFF0`, con el acople entre lanes del motor.</summary>
        Friend Function Seno4(x As Single()) As Single()
            Dim r(3) As Single
            Dim grande(3) As Boolean
            Dim mascara = 0, hayEnorme = False
            For i = 0 To 3
                Dim ab = U32(x(i)) And &H7FFFFFFFUI
                grande(i) = ab > UmbralGrande                                ' 0x1422C0033 pcmpgtd
                If grande(i) Then mascara = mascara Or (1 << i)
                If ab > UmbralEnorme Then hayEnorme = True                   ' 0x1422C00F8 pcmpgtd
            Next

            If mascara = 0 Then                                              ' 0x1422C0043 jne
                For i = 0 To 3
                    r(i) = Rapido(x(i))
                Next
                Return r
            End If

            If hayEnorme Then                                                ' 0x1422C010A → 0x1422C065A
                If mascara <> &HF Then                                       ' 0x1422C065D
                    For i = 0 To 3
                        r(i) = Rapido(x(i))                                  ' 0x1422C0663…6E7
                    Next
                End If
                For i = 0 To 3
                    If grande(i) Then r(i) = Escalar(x(i))                   ' 0x1422C06FA…8A4
                Next
                Return r
            End If

            ' en double: todas grandes (0x1422C03FF) o mezclado (0x1422C0119) — la misma cuenta por lane
            For i = 0 To 3
                r(i) = If(grande(i), GrandeEnDouble(x(i)), Rapido(x(i)))
            Next
            Return r
        End Function

        ''' <summary>El camino rápido, `0x1422C0029`-`0x1422C00B8`.</summary>
        Private Function Rapido(x As Single) As Single
            Dim q = Cvtps2dq(x * C5F0)                                       ' 0x1422C0029 / 0x1422C0049
            Dim qf = DqAPs(q)                                                ' 0x1422C0057
            Dim signo = (q << 31)                                            ' 0x1422C0061
            Dim a = x - C5D0 * qf                                            ' 0x1422C0066 / 79
            a = a - C5C0 * qf                                                ' 0x1422C0069 / 7C
            a = a - C5A0 * qf                                                ' 0x1422C006C / 7F
            Dim a1 = a - C5B0 * qf                                           ' 0x1422C006F / 93
            Dim z = a * a                                                    ' 0x1422C0096
            Dim p = C590 * z                                                 ' 0x1422C0099
            p = p + C560 : p = p * z                                         ' 0x1422C009C / A3
            p = p + C580 : p = p * z                                         ' 0x1422C00A6 / A9
            p = p + C570 : p = p * z                                         ' 0x1422C00AC / AF
            p = p * a1 : p = p + a1                                          ' 0x1422C00B2 / B5
            Return BitsF(U32(p) Xor signo)                                   ' 0x1422C00B8
        End Function

        ''' <summary>La lane grande en double — `0x1422C0150`-`0x1422C0284` y el polinomio de
        ''' `0x1422C0361`-`0x1422C03F6` (la misma cuenta en `0x1422C0404`-`0x1422C0651`).</summary>
        Private Function GrandeEnDouble(x As Single) As Single
            Dim signoX = U32(x) And &H80000000UI                             ' 0x1422C00DB
            Dim a = CDbl(BitsF(U32(x) And &H7FFFFFFFUI))                    ' 0x1422C0150 cvtps2pd
            Dim k = Cvttpd2dq(D6A0 * a)                                      ' 0x1422C015B/63/67
            Dim n = CULng((CULng(k) + 1UL) And &HFFFFFFFEUL)                 ' 0x1422C0176 paddd / 0x1422C0181 pand
            Dim nd = BitsD(Q670 Or n) - BitsD(Q670)                          ' 0x1422C018C…198
            Dim t0 = D660 * nd                                               ' 0x1422C01A8
            Dim t1 = D650 * nd                                               ' 0x1422C01BD
            Dim hi = a - t0                                                  ' 0x1422C01C1
            Dim t2 = D640 * nd                                               ' 0x1422C01D5
            Dim x0 = hi                                                      ' 0x1422C01D9
            hi = hi - t1                                                     ' 0x1422C01DD
            x0 = x0 - hi                                                     ' 0x1422C01EA
            Dim x1 = hi                                                      ' 0x1422C01F9
            x0 = x0 - t1                                                     ' 0x1422C01FD
            hi = hi - t2                                                     ' 0x1422C0201
            x1 = x1 - hi                                                     ' 0x1422C0213
            Dim x2 = hi                                                      ' 0x1422C021F
            x1 = x1 - t2                                                     ' 0x1422C022B
            x0 = x0 + x1                                                     ' 0x1422C022F
            hi = hi + x0                                                     ' 0x1422C0238
            Dim t3 = D630 * nd                                               ' 0x1422C023C
            x2 = x2 - hi                                                     ' 0x1422C0240
            Dim x3 = hi                                                      ' 0x1422C0244
            hi = hi - t3                                                     ' 0x1422C0248
            x0 = x0 + x2                                                     ' 0x1422C024C
            x3 = x3 - hi                                                     ' 0x1422C0250
            Dim t4 = nd * D620                                               ' 0x1422C0254
            x3 = x3 - t3                                                     ' 0x1422C025C
            x0 = x0 + x3                                                     ' 0x1422C0260
            x0 = x0 - t4                                                     ' 0x1422C026C
            x0 = x0 + hi                                                     ' 0x1422C0284
            Dim signoN = CUInt(((n << 62) >> 32) And &H80000000UL)           ' 0x1422C020E psllq 62 + pand 0x1440F5610 + pshufd 0xDD
            Dim rl = CSng(x0)                                                ' 0x1422C0361 cvtpd2ps
            Dim z = rl * rl                                                  ' 0x1422C038B
            Dim p = C590 * z                                                 ' 0x1422C039D
            p = p + C560 : p = p * z                                         ' 0x1422C03A5 / B5
            p = p + C580 : p = p * z                                         ' 0x1422C03BC / C9
            p = p + C570 : p = p * z                                         ' 0x1422C03D2 / DE
            p = p * rl : p = p + rl                                          ' 0x1422C03E7 / F3
            Dim signo = signoN Xor signoX                                    ' 0x1422C03C3 pxor
            Return BitsF(U32(p) Xor signo)                                   ' 0x1422C03F6
        End Function

        ''' <summary>El camino escalar con tabla — `0x1422C072D`-`0x1422C0884`.</summary>
        Private Function Escalar(f As Single) As Single
            Dim bf = U32(f)
            If (bf And &H7F800000UI) = &H7F800000UI Then Return f - f        ' 0x1422C0770 / 0x1422C088A
            Dim d = CDbl(f)                                                  ' 0x1422C0755
            Dim ecx = CInt((((CULng(bf And &H7FFFFFFFUI) >> 23) + &HFFFFFF72UL) And &HFFFFFFFFUL) And &HFFF8UL)   ' 0x1422C077B…84
            Dim tb = Tabla((&H410 + ecx * 2) \ 8)                            ' 0x1422C078A
            Dim t5 = T(&H418 + ecx * 2)                                      ' 0x1422C0794
            Dim c4338 = BitsD(Q670)                                          ' 0x1422C07AB/B0
            Dim x2 = BitsD(Tabla(&H550 \ 8) And tb)                          ' 0x1422C079E / 0x1422C07B5
            Dim x3 = BitsD(tb << 40)                                         ' 0x1422C07B9
            x2 = x2 * d                                                      ' 0x1422C07BE
            x3 = x3 * d                                                      ' 0x1422C07C2
            Dim x1 = d * t5                                                  ' 0x1422C07C6
            Dim x0 = x2                                                      ' 0x1422C07CA
            x2 = x2 + x3                                                     ' 0x1422C07CE
            Dim x5 = x2                                                      ' 0x1422C07D2
            x0 = x0 - x2                                                     ' 0x1422C07D6
            x2 = x2 + c4338                                                  ' 0x1422C07DA
            x3 = x3 + x0                                                     ' 0x1422C07DE
            Dim ec = U64(x2) And &HFFFFFFFFUL                                ' 0x1422C07E2 movd
            x2 = x2 - c4338                                                  ' 0x1422C07E6
            x1 = x1 + x3                                                     ' 0x1422C07EA
            Dim ea = &H180UL                                                 ' 0x1422C07EE
            Dim x4 = T(&H540)                                                ' 0x1422C07F3
            x5 = x5 - x2                                                     ' 0x1422C07FC
            ec = (ec + ec) And &HFFFFFFFFUL                                  ' 0x1422C0800
            Dim ed = ec                                                      ' 0x1422C0802
            Dim ecs = If(((ec << 24) And &H80000000UL) <> 0UL, &HFFFFFFFFUL, 0UL)   ' 0x1422C0804 shl 24 / 07 sar 31
            ea = ea And ed                                                   ' 0x1422C080A
            ed = (ed + ecs) And &HFFFFFFFFUL                                 ' 0x1422C080C
            ed = ed Xor ecs                                                  ' 0x1422C080E
            x1 = x1 + x5                                                     ' 0x1422C0810
            ed = ed And &HFEUL                                               ' 0x1422C0814
            Dim x3b = T(CInt(ed) * 8)                                        ' 0x1422C081A
            Dim x0b = T(&H548)                                               ' 0x1422C0820
            Dim rr = x1                                                      ' 0x1422C0829 pshufd 0x44
            Dim x1c = x1 * x1                                                ' 0x1422C082E
            Dim x5b = T(CInt(ed) * 8 + 8)                                    ' 0x1422C0832
            Dim ec2 = (((ea + &H80UL) And &H100UL) << 23) And &HFFFFFFFFUL   ' 0x1422C0839…47
            x3b = x3b * rr                                                   ' 0x1422C084A
            x0b = x0b - x1c                                                  ' 0x1422C0852
            Dim ea2 = ((ea And &H100UL) << 23) And &HFFFFFFFFUL              ' 0x1422C0856 / 5B
            x4 = x4 - x1c                                                    ' 0x1422C085E
            x4 = BitsD(U64(x4) Xor (ec2 << 32))                              ' 0x1422C0862 psllq / 6B xorpd
            x0b = x0b * x5b                                                  ' 0x1422C086F
            x3b = x3b * x4                                                   ' 0x1422C0873
            x0b = BitsD(U64(x0b) Xor (ea2 << 32))                            ' 0x1422C0877 / 7C
            x0b = x0b + x3b                                                  ' 0x1422C0880
            Return CSng(x0b)                                                 ' 0x1422C0884
        End Function

    End Module

End Namespace
