''' <summary>FALLOUT 4'S OWN ROWS OF THE INPUT LAYOUT. The layout law of a vertex desc, with its row per game, is EngineVertexLayout
''' (Fallout4.exe 1.11.240 0x141823210 / SkyrimSE.exe 1.7.104 0x141011750). This module keeps, declared (ola SSE S1 F2.3):
''' <list type="bullet">
''' <item>Sem, the app's GL location of each semantic (ola 2 propuesta H4) - the seat the generated GLSL of both games names in its
''' comments ("Fo4EngineVertexLayout.Sem", 1316 times in the blocks, written by stage_text.py / asm2glsl.py / build_source_v6.py), and that
''' text is frozen bit for bit: the name stays where it points.</item>
''' <item>the Fallout 4 z-prepass layouts: masks 0x141828B77 (rigid) and 0x141828E34 (alpha test) of Fallout4.exe.</item>
''' </list></summary>
Friend Module Fo4EngineVertexLayout
    Friend Enum Sem
        Position0 = 0 : Texcoord0 = 1 : Texcoord1 = 2 : Normal0 = 3 : Binormal0 = 4 : Color0 = 5
        BlendWeight0 = 6 : BlendIndices0 = 7 : Texcoord2 = 8 : Texcoord3 = 9 : Texcoord4 = 10
    End Enum
    Friend Const PrepassRigidMask As ULong = &H401000000000FFUL       ' 0x141828B77
    Friend Const PrepassAlphaMask As ULong = &HC030000F000FFFUL       ' 0x141828E34
    ''' <summary>Bit 49, COLOR present in slot 0: the alpha-test prepass ORs it into its layout's desc after the mask, always
    ''' (0x141828E48 movabs + 0x141828E5A or; RE re22 1). The colour offset nibble (bits 24-27) is in the mask, so COLOR0 lands at the
    ''' desc's colour offset ((desc &gt;&gt; 22) AND 0x3C), slot 0.</summary>
    Friend Const PrepassAlphaForcedColor As ULong = &H2000000000000UL   ' 0x141828E48
    ''' <summary>Bits 49 and 59 (COLOR in slot 0 or 1), the test of cb2[0].y on the FULL desc (0x141828F69..F73).</summary>
    Friend Const PrepassColorStreamBits As ULong = &H802000000000000UL  ' 0x141828F69

    ''' <summary>THE DESC OF THE ALPHA-TEST Z-PREPASS'S INPUT LAYOUT (0x141828DE0, entry 0x1A): (desc AND PrepassAlphaMask) OR
    ''' PrepassAlphaForcedColor (0x141828E34..E5A) - the key of the layout cache (compared 0x141829017, stored 0x141829020), built by
    ''' 0x141823210 (0x14182908B). COLOR0 is always in it, as VS 3929 reads it (rec3929 L48) and D3D11 needs every VS input in the
    ''' layout; a geometry without a colour stream reads, there, the bytes at its desc's colour offset, which cb2[0].y = 10000 cancels
    ''' (PrepassColorStream). The one owner of that layout: Render's prepass VAO and RenderableMesh.PrepassStreamHasColor.</summary>
    Friend Function PrepassAlphaDesc(desc As ULong) As ULong
#If DEBUG Then
        ' Ola 7h tester (ShadowGate --ola7h, ParityGate fo4-prepass-colour-layout): the layout without the engine's OR (the 7f layout):
        ' no COLOR0 without a colour stream - the prepass VAO and PrepassStreamHasColor as before the 7h.
        If Fo4GBufferConstants.GateMutantOn("p7h-no-or") Then Return desc And PrepassAlphaMask
#End If
        Return (desc And PrepassAlphaMask) Or PrepassAlphaForcedColor
    End Function

    ''' <summary>The desc of the rigid z-prepass's input layout (0x141828B77, entry 0x19): desc AND PrepassRigidMask, no OR - VS 3928
    ''' reads no COLOR (RE re22 1).</summary>
    Friend Function PrepassRigidDesc(desc As ULong) As ULong
        Return desc And PrepassRigidMask
    End Function

    ''' <summary>Does the alpha-test prepass multiply the vertex alpha? cb2[0].y = 0 when the geometry's FULL desc has COLOR in slot 0
    ''' or 1 (bit 49 or 59: (desc AND 0x0802000000000000) &lt;&gt; 0, 0x141828F69..F8D), else [0x142475504] = 10000, so saturate(COLOR0.w +
    ''' 10000) = 1 cuts nothing. The one owner of Fo4ShapeInputs.HasColorStream.</summary>
    Friend Function PrepassColorStream(desc As ULong) As Boolean
        Return (desc And PrepassColorStreamBits) <> 0UL
    End Function

End Module
