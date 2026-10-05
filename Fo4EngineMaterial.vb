Imports OpenTK.Mathematics

''' <summary>THE BSLightingShaderMaterial FALLOUT 4 DRAWS WITH, as its G-buffer constants read it (Fallout4.exe 1.11.240.0): the fields
''' SetupGeometry 0x142205540 / SetupMaterial 0x142204140 read from the material [prop+0x58] and the property, built by the engine's
''' own two paths and then the slot-42 fixer:
''' <list type="bullet">
''' <item>a material file applied (ApplyMaterialData 0x1421715E0): the class from the applied flags (0x14216B610, first match wins:
''' bit 17 Eye, 7 Envmap, 38 Glowmap, 10 Face, 21 SkinTint, 18 HairTint, 56 MultiLayerParallax, else the base material) and the
''' fields the applicator copies (0x14216B850..0x14216BAF4), the wetness completed from the root material (SetMaterial 0x142169620 -&gt;
''' 0x1421CE1B0);</item>
''' <item>no material file: BSLightingShaderProperty::LoadBinary 0x142178BF0 on the block the save writes (the class from the Shader Type,
''' factory 0x1421CEB20; the property's fields 0x142178C73..0x142178CC1; the base reader 0x1421CE880 and the class readers), the
''' wetness completed from the block's Root Material the same way;</item>
''' <item>the fixer 0x1421793F0 on the material (0x1421794FD..0x142179516; 0x14217957E..0x1421795D7 clears flag 0, applied by the
''' caller on the flags).</item>
''' </list>
''' Only the fields a G-buffer record reads are carried (the closed list of SetupGeometry / SetupMaterial reads); the skin tint stays the
''' runtime actor tone (FaceTint, closed). Source: scratchpad opaque-fo4/hallazgos.md "RE LoadBinary" and the applicator map
''' (D:\WinTmp\loadbin, D:\WinTmp\applicator).</summary>
Friend Structure Fo4EngineLightingMaterial
    ''' <summary>The material class' feature (vfunc +0x28): 0 base, 1 Envmap, 2 Glowmap, 3 Parallax, 4 Face, 5 SkinTint, 6 HairTint,
    ''' 7 ParallaxOcc, 0xB MultiLayerParallax, 0xE Snow, 0x10 Eye, 0x12 LODLandscape, 0x13 Landscape; -1 = no material (factory null).</summary>
    Public Feature As Integer
    Public Alpha As Single                ' M+0x80
    Public Smoothness As Single           ' M+0x88
    Public SpecularColor As Vector3       ' M+0x38..0x40
    Public SpecularMult As Single         ' M+0x8C
    Public Wetness As Single()            ' M+0x94..0xA8: SpecScale, SpecPowerScale, SpecMinvar, EnvMapScale, FresnelPower, Metalness
    Public Rolloff As Single              ' M+0xAC
    Public Backlight As Single            ' M+0xB4
    ''' <summary>mat+0xD0 read as a float (CubeMapIdxR_EnvmapScaleG.z with flag 7, 0x14220593F..0x1422059A9, whatever the class):
    ''' Envmap = Environment Map Scale; Eye = Left Eye Reflection Center x. Flag 7 reaches no other class (0x14216B610 priority).</summary>
    Public MaterialD0 As Single
    Public Ssr As Boolean                 ' Envmap +0xD4
    Public WetSsr As Boolean              ' Envmap +0xD5
    Public EmissiveColor As Vector3       ' *(prop+0xB8)
    Public EmissiveMult As Single         ' prop+0xC8
End Structure

Friend Module Fo4EngineMaterial

    ''' <summary>-1 sentinel band of the root-material completion (0x1421CE216..0x1421CE31C: (-1.001, -0.999), constants 0x142692458 /
    ''' 0x14262F42C).</summary>
    Private Function IsUnsetWetness(v As Single) As Boolean
        Return v > -1.001F AndAlso v < -0.999F
    End Function

    ''' <summary>The wetness the engine ends with: each value still in the -1 band takes the root material's raw value
    ''' (0x1421CE1B0; the root loaded as "materials\" + name, 0x1417A9B50; a root that fails to load leaves -1).</summary>
    Private Function CompleteWetness(own As Single(), root As Single()) As Single()
        Dim w = CType(own.Clone(), Single())
        If root Is Nothing Then Return w
        For i = 0 To 5
            If IsUnsetWetness(w(i)) Then w(i) = root(i)
        Next
        Return w
    End Function

    ''' <summary>The class 0x14216B610 creates for applied flags <paramref name="f"/> (0x14216B649..0x14216B84B, first match wins).</summary>
    Private Function AppliedFeature(f As ULong) As Integer
        Dim bit = Function(n As Integer) ((f >> n) And 1UL) <> 0UL
        If bit(17) Then Return &H10
        If bit(7) Then Return 1
        If bit(38) Then Return 2
        If bit(10) Then Return 4
        If bit(21) Then Return 5
        If bit(18) Then Return 6
        If bit(56) Then Return &HB
        Return 0
    End Function

    ''' <summary>The class LoadBinary's factory 0x1421CEB20 builds for a Shader Type (switch table 0x1421CEE7C): 12, 13, 15, 17 and
    ''' &gt;= 20 give no material (-1).</summary>
    Friend Function InlineFeature(shaderType As Integer) As Integer
        Select Case shaderType
            Case 0 : Return 0
            Case 1 : Return 1
            Case 2 : Return 2
            Case 3 : Return 3
            Case 4 : Return 4
            Case 5 : Return 5
            Case 6 : Return 6
            Case 7 : Return 7
            Case 8, 19 : Return &H13
            Case 9, 18 : Return &H12
            Case 10, 14 : Return &HE
            Case 11 : Return &HB
            Case 16 : Return &H10
            Case Else : Return -1
        End Select
    End Function

    ''' <summary>The subsurface rolloff (M+0xAC) and the backlight (M+0xB4) of a material with the .bgsm law: rolloff only with
    ''' bSubsurfaceLighting (0x14216B901..916), Back Light Power with no condition - bBackLighting is not read (0x14216B92E..938).</summary>
    Private Function MaterialLightTerms(mb As FO4UnifiedMaterial_Class) As (Rolloff As Single, Backlight As Single)
        Return (If(mb.SubsurfaceLighting, mb.SubsurfaceLightingRolloff, 0.0F), mb.BackLightPower)
    End Function

    ''' <summary>A material file applied: 0x14216B610's copies from the file (as FO4UnifiedMaterial_Class holds it) on the class of the
    ''' applied flags <paramref name="applied"/>; the root material completes the wetness (the reader's default root when the file names
    ''' none: "template\defaultTemplate_wet.bgsm", 0x14216D1C1..0x14216D1D9).</summary>
    Friend Function FromMaterialFile(mb As FO4UnifiedMaterial_Class, applied As ULong) As Fo4EngineLightingMaterial
        Dim colourOf = Function(c As Drawing.Color) New Vector3(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F)
        Dim m As New Fo4EngineLightingMaterial With {.Feature = AppliedFeature(applied)}
        m.Alpha = mb.Alpha                                                                    ' P->vf[0x190] 0x14216BA35..A40
        m.Smoothness = mb.Smoothness                                                          ' 0x14216B969..96C
        m.SpecularColor = colourOf(mb.SpecularColor)                                          ' 0x14216BA14..A25
        m.SpecularMult = mb.SpecularMult                                                      ' 0x14216BA28..A2F
        Dim light = MaterialLightTerms(mb)
        m.Rolloff = light.Rolloff
        m.Backlight = light.Backlight
        ' Wetness: the class is built (ctor -1), the root completes it (SetMaterial), then 0x14216B610 overwrites each value that is
        ' not -1 (ucomiss/je: -1 or NaN keep the root's), 0x14216B97F..0x14216B9FB.
        Dim own = {mb.WetnessControlSpecScale, mb.WetnessControlSpecPowerScale, mb.WetnessControlSpecMinvar,
                   mb.WetnessControlEnvMapScale, mb.WetnessControlFresnelPower, mb.WetnessControlMetalness}
        Dim rootName = If(String.IsNullOrEmpty(mb.RootMaterialPath), "template\defaultTemplate_wet.bgsm", mb.RootMaterialPath)
        Dim w = CompleteWetness({-1.0F, -1.0F, -1.0F, -1.0F, -1.0F, -1.0F}, mb.EngineRootWetness(rootName))
        For i = 0 To 5
            If Not (own(i) = -1.0F OrElse Single.IsNaN(own(i))) Then w(i) = own(i)
        Next
        m.Wetness = w
        ' +0xD0: Envmap = fEnvironmentMappingMaskScale (0x14216B6A0..6B0); Eye = the left eye centre, which the applicator does not
        ' write: the ctor's 0 (0x1421CFA90, .data 0x143448450).
        If m.Feature = 1 Then m.MaterialD0 = mb.EnvironmentMappingMaskScale
        If m.Feature = 1 Then
            m.Ssr = mb.ScreenSpaceReflections                                                 ' 0x1421716CA..6E5
            m.WetSsr = mb.WetnessControlScreenSpaceReflections                                ' 0x1421716EB..6F5
        End If
        m.EmissiveColor = If(mb.EmitEnabled, colourOf(mb.EmittanceColor), Vector3.Zero)       ' 0x14216B93E..95C (reader 0x14216D2C0..2D4)
        m.EmissiveMult = mb.EmittanceMult                                                     ' 0x14216B95C..963
        Return m
    End Function

    ''' <summary>No material file: LoadBinary's law (0x142178BF0, base reader 0x1421CE880, class readers) on THE MATERIAL IN MEMORY
    ''' (<paramref name="mb"/>: Create_From_Shader read every field from the NIF block, the app's edits on top) with the flags
    ''' <paramref name="flags"/>; <paramref name="nifBlock"/> (the NIF as loaded) only for what the material does not hold (the eye's
    ''' reflection centre). Colours are the material's (MaterialLib keeps them as 8-bit channels: declared).</summary>
    Friend Function FromInlineMaterial(mb As FO4UnifiedMaterial_Class, flags As ULong,
                                       nifBlock As NiflySharp.Blocks.BSLightingShaderProperty) As Fo4EngineLightingMaterial
        Dim colourOf = Function(c As Drawing.Color) New Vector3(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F)
        Dim m As New Fo4EngineLightingMaterial With {.Feature = InlineFeature(CInt(mb.NifShaderType))}
        m.Alpha = mb.Alpha                                                                    ' 0x1421CE8B8
        m.Smoothness = mb.Smoothness                                                          ' 0x1421CE8FA
        m.SpecularColor = colourOf(mb.SpecularColor)                                          ' 0x1421CE91B
        m.SpecularMult = mb.SpecularMult                                                      ' 0x1421CE92E
        ' Rolloff and backlight: the .bgsm law, as for a material file. The two gates LoadBinary reads the block with (rolloff raw,
        ' 0x1421CE948; Backlight Power only with Rimlight Power FLT_MAX or NaN, 0x1421CE98A..9B8) are applied once, where the block
        ' is read (FO4UnifiedMaterial_Class.Create_From_Shader: Fo4NifBacklightPower, SubsurfaceLighting = rolloff <> 0); for a
        ' block as read both laws give the same values. After an edit the material only leaves the app with the .bgsm law: WM saves
        ' it as a .bgsm, Save_To_Shader folds it like the CK (rolloff gated, backlight ungated, rim FLT_MAX).
        Dim light = MaterialLightTerms(mb)
        m.Rolloff = light.Rolloff
        m.Backlight = light.Backlight
        Dim rootName = mb.RootMaterialPath
        m.Wetness = CompleteWetness({mb.WetnessControlSpecScale, mb.WetnessControlSpecPowerScale, mb.WetnessControlSpecMinvar,
                                     mb.WetnessControlEnvMapScale, mb.WetnessControlFresnelPower, mb.WetnessControlMetalness},
                                    If(String.IsNullOrEmpty(rootName), Nothing, mb.EngineRootWetness(rootName)))   ' 0x14216964A..58
        Select Case m.Feature
            Case 1                                                                            ' Envmap reader 0x1421CF7E1..0x1421CF822
                m.MaterialD0 = mb.EnvironmentMappingMaskScale
                m.Ssr = mb.ScreenSpaceReflections
                m.WetSsr = mb.WetnessControlScreenSpaceReflections
            Case &H10 : m.MaterialD0 = If(nifBlock Is Nothing, 0.0F, nifBlock.LeftEyeReflectionCenter.X)   ' Eye +0xD0..0xD8, 0x1421D000B
        End Select
        ' Emissive Color only with flags1 bit 22 Own_Emit (0x142178C73..0x142178C9D), else the ctor's (0, 0, 0).
        m.EmissiveColor = If((flags And (1UL << 22)) <> 0UL, colourOf(mb.EmittanceColor), Vector3.Zero)
        m.EmissiveMult = mb.EmittanceMult                                                     ' 0x142178CA7
        Return m
    End Function

    ''' <summary>The fixer's material edits (0x1421793F0): with flag 0, a negative smoothness becomes 1 (0x1421794FD..516). The flag
    ''' edits are Fo4GBufferTechnique.Fixer's; flag 0 is cleared there when SpecularColor * SpecularMult is 0 (0x14217957E..5D7).</summary>
    Friend Sub FixMaterial(ByRef m As Fo4EngineLightingMaterial, flags As ULong)
        If (flags And 1UL) <> 0UL AndAlso m.Smoothness < 0.0F Then m.Smoothness = 1.0F
    End Sub

    ''' <summary>0x14217957E..5D7: SpecularColor * SpecularMult == (0, 0, 0).</summary>
    Friend Function SpecularIsZero(m As Fo4EngineLightingMaterial) As Boolean
        Dim s = m.SpecularColor * m.SpecularMult
        Return s.X = 0.0F AndAlso s.Y = 0.0F AndAlso s.Z = 0.0F
    End Function
End Module
