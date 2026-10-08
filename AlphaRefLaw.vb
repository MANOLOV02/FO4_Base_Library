''' <summary>THE FLOAT32 ARITHMETIC OF THE ALPHA-TEST REFERENCE, shared by both exes (one owner; each game composes it in its own
''' writer: Fo4RenderPassLaw, SseRenderPassLaw). Both exes hold the same two constants and use scalar SSE float32 ops:
''' cvtdq2ps(ref) then mulss K1, then addss K2 where the writer biases. K1 = 0x3B808081 (float32 nearest 1/255): FO4
''' [0x1429293FC], SSE [0x141B5BDC8]. K2 = 0x3B80802C: FO4 [0x1426A4908], SSE [0x141B48BA4]. Bits, not decimal literals:
''' 0.00392153F is 0x3B80802E and ref / 255.0F differs from ref * K1 in 130 of 256 refs. VB Single arithmetic on .NET 8 (x64
''' and x86) gives the exe's bits for these formulas (C11 probe; gates fo4-pass-law / sse-pass-law hold the goldens).</summary>
Friend Module AlphaRefLaw
    Friend ReadOnly Inv255 As Single = BitConverter.Int32BitsToSingle(&H3B808081)
    Friend ReadOnly RefBias As Single = BitConverter.Int32BitsToSingle(&H3B80802C)

    ''' <summary>mulss K1 of an already converted count (cvtdq2ps / cvtsi2ss by the caller).</summary>
    Friend Function Scaled(count As Single) As Single
        Return count * Inv255
    End Function

    ''' <summary>cvtdq2ps(ref), mulss K1, addss K2: the biased reference of FO4's prepass 0x1A and shadow map, and of SSE's thrG.</summary>
    Friend Function ScaledBiased(ref As Byte) As Single
        Return Scaled(CSng(ref)) + RefBias
    End Function
End Module
