Imports OpenTK.Mathematics

''' <summary>THE CONSTANTS OF THE FALLOUT 4 G-BUFFER PS, BY NAME (BSDFPrePassShaderPixelConstants::GetString 0x1422733F0, jump table
''' 0x1422734C0), as BSDFPrePassShader::SetupGeometry 0x142205540 / SetupMaterial 0x142204140 write them. The register each name
''' occupies is per record; the GLSL reads them by name. Sources: scratchpad opaque-fo4/transcripcion-gbuffer/notas.md 1.4 and 4;
''' Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md 7, 8, 14, 18; RE_FO4_WORLD_ENVMAP_2026-10-03.md 2.3, 11, 12.</summary>
Friend Module Fo4GBufferConstants

    Private Function Lit(bits As UInteger) As Single
        Return BitConverter.UInt32BitsToSingle(bits)
    End Function

    ''' <summary>SpecularParam (constant 1), 0x14220762B..0x142207693: x = Smoothness (0.22 fLODLandSpecPower with technique bit 9),
    ''' y = SpecularMult only with property bit 0 (else 0; DF 7), z = fBackLightPower (mat+0xB4), w = SSS rolloff (mat+0xAC).</summary>
    Friend Function SpecularParam(technique As UInteger, flags As ULong, smoothness As Single, specularMult As Single,
                                  backLightPower As Single, subsurfaceRolloff As Single) As Vector4
        Dim lodLand = (technique And (1UI << 9)) <> 0UI
        Return New Vector4(If(lodLand, 0.22F, smoothness),
                           If(lodLand, 0.06F, If((flags And 1UL) <> 0UL, specularMult, 0.0F)),
                           backLightPower, subsurfaceRolloff)
    End Function

    ''' <summary>SetupGeometry writes AdditionalAlphaMaskRef and binds t15 when technique AND 0x1000020 = 0x1000000 (bit 24 without
    ''' bit 5, 0x14220714B..0x142207150).</summary>
    Friend Function HasAdditionalAlphaMask(technique As UInteger) As Boolean
        Return (technique And &H1000020UI) = &H1000000UI
    End Function

    ''' <summary>AdditionalAlphaMaskRef (constant 17) of a pass without effectData (the preview never has one): .x is not written
    ''' (0x1422071E4 only under effectData; the buffer is WRITE_DISCARD) and the PS reads it only when .w is not 0, so 0 here;
    ''' .y = -1 with property flag 58 (F4SPF2 bit 26), +1 without (0x1422072A7..0x1422072B1: xmm13 -1 from 0x142205B4B /
    ''' 0x142206A01 / 0x142206A99, xmm15 +1 from 0x1422058B6); .z = the pass alpha [prop+0x28] = material alpha * fade (1) *
    ''' prop+0x64 (1) (0x14217A47C..0x14217A4AB), because [pass+0x4E] is 3 &gt;= 0 (BSRenderPass ctor 0x14221D35A; 0x142207292);
    ''' .w = 0 without effectData (0x1422071B7).</summary>
    Friend Function AdditionalAlphaMaskRef(flags As ULong, materialAlpha As Single) As Vector4
        Return New Vector4(0.0F, If((flags And (1UL << 58)) <> 0UL, -1.0F, 1.0F), materialAlpha, 0.0F)
    End Function

    ''' <summary>EmissiveColor.xyz (constant 2), 0x142207432..0x1422074D6: pow(colour * mult, 2.2) per channel (DF 14; the reader puts
    ''' black when bEmitEnabled = 0, DF 18.1). .w: the alpha-test / blend-discard reference, written when the technique has ALPHA_TEST
    ''' (bit 8) or the NiAlphaProperty blends (0x142207340..0x14220737D): ref * 1/255 (0x3B808081) + 0x3B80802C, or + 0x3C008056 when
    ''' ref = 4 (0x142207354..0x142207371); otherwise 0.</summary>
    Friend Function EmissiveColor(emitColor As Vector3, emitMult As Single, technique As UInteger, alphaBlend As Boolean, alphaRef As Byte) As Vector4
        Dim c = emitColor * emitMult
        Dim p = New Vector3(CSng(Math.Pow(c.X, 2.2)), CSng(Math.Pow(c.Y, 2.2)), CSng(Math.Pow(c.Z, 2.2)))
        Dim w = 0.0F
        If (technique And (1UI << 8)) <> 0UI OrElse alphaBlend Then
            w = alphaRef * Lit(&H3B808081UI) + If(alphaRef = 4, Lit(&H3C008056UI), Lit(&H3B80802CUI))
        End If
        Return New Vector4(p, w)
    End Function

    ''' <summary>AlphaScale (constant 3), only with technique bit 15 (0x14220738B): x = the pass alpha [prop+0x28] = material alpha *
    ''' fade (1) * prop+0x64 (1) (0x14217A47C..0x14217A4AB); y = 1 when the NiAlphaProperty blends (0x1422073BB..0x1422073D6).</summary>
    Friend Function AlphaScale(technique As UInteger, materialAlpha As Single, alphaBlend As Boolean) As Vector4
        If (technique And (1UI << 15)) = 0UI Then Return Vector4.Zero
        Return New Vector4(materialAlpha, If(alphaBlend, 1.0F, 0.0F), 0.0F, 0.0F)
    End Function

    ''' <summary>TintColor (constant 4), 0x142206AAB..0x142206BB4: SKIN_TINT (technique bit 18) with the SkinTint material class:
    ''' (pow(tone, 2.2), tone alpha); GRADIENT_REMAP (bit 26): x = grayscaleToPaletteScale (mat+0xB8); otherwise 0.</summary>
    Friend Function TintColor(technique As UInteger, toneLinear As Vector3, toneAlpha As Single, hasTone As Boolean, paletteScale As Single) As Vector4
        If (technique And (1UI << 18)) <> 0UI Then Return If(hasTone, New Vector4(toneLinear, toneAlpha), Vector4.Zero)
        If (technique And (1UI << 26)) <> 0UI Then Return New Vector4(paletteScale, 0.0F, 0.0F, 0.0F)
        Return Vector4.Zero
    End Function

    ''' <summary>SpecularParam2 (constant 7), 0x1422076AA..0x1422076F2: (WetnessControl_SpecPowerScale mat+0x98, WetnessControl_SpecScale
    ''' mat+0x94), or (-1, -1) with technique bit 5 (bit 28 without instance object never happens in the preview, ENV 12.1).</summary>
    Friend Function SpecularParam2(technique As UInteger, wetSpecPowerScale As Single, wetSpecScale As Single) As Vector4
        If (technique And &H20UI) <> 0UI Then Return New Vector4(-1.0F, -1.0F, 0.0F, 0.0F)
        Return New Vector4(wetSpecPowerScale, wetSpecScale, 0.0F, 0.0F)
    End Function

    ''' <summary>WetnessControl_Spec (constant 8), 0x142207705..0x142207805: (mat+0x94 SpecScale, +0x98 SpecPowerScale, +0x9C SpecMin,
    ''' +0xA8 Metalness); an Envmap-class material without technique bit 5 replaces .x/.y with the bytes mat+0xD4 / +0xD5
    ''' (bScreenSpaceReflections, bWetnessControl_ScreenSpaceReflections; ENV 8.2).</summary>
    Friend Function WetnessControlSpec(technique As UInteger, envmapClass As Boolean, wetSpecScale As Single, wetSpecPowerScale As Single,
                                       wetSpecMin As Single, wetMetalness As Single, ssr As Boolean, wetSsr As Boolean) As Vector4
        If envmapClass AndAlso (technique And &H20UI) = 0UI Then
            Return New Vector4(If(ssr, 1.0F, 0.0F), If(wetSsr, 1.0F, 0.0F), wetSpecMin, wetMetalness)
        End If
        Return New Vector4(wetSpecScale, wetSpecPowerScale, wetSpecMin, wetMetalness)
    End Function

    ''' <summary>CubeMapIdxR_EnvmapScaleG (constant 20), 0x14220593F..0x1422059A9 (ENV 2.3): x = slice + 1 of the EnvMapArray (0 for 0xFF
    ''' or without bit 7), y = property bit 7 ? 1 : 0, z = bit 7 ? mat+0xD0 read as a float (Fo4EngineLightingMaterial.MaterialD0: the
    ''' Envmap class' scale; the Eye class' left eye reflection centre x) : 0, w = mat+0xA0 (WetnessControl_EnvMapScale).</summary>
    Friend Function CubeMapIdxEnvmapScale(flags As ULong, slice As Integer, materialD0 As Single, wetEnvMapScale As Single) As Vector4
        Dim envBit = (flags And (1UL << 7)) <> 0UL
        Return New Vector4(If(envBit AndAlso slice >= 0, slice + 1, 0),
                           If(envBit, 1.0F, 0.0F),
                           If(envBit, materialD0, 0.0F),
                           wetEnvMapScale)
    End Function
End Module
