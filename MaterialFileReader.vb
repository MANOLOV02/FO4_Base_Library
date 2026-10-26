Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports MaterialLib
Imports MaterialLib.BaseMaterialFile

''' <summary>READS AND WRITES A .bgsm / .bgem AS THE ENGINE DOES, on top of MaterialLib (a third party that is not
''' edited). ONE place for every reader of material files (FO4UnifiedMaterial_Class.Deserialize, the Wardrobe Manager
''' clone), so the two rules below cannot drift apart:
''' <list type="bullet">
''' <item><b>JSON payloads.</b> The engine reads .bgem/.bgsm stored as JSON (FO4 BGEM reader 0x14216EFF0): each key
''' as json.get(key, literal), so an ABSENT key takes the reader's literal, and some key names are not MaterialLib's
''' (Tools/re-docs/RE_EFFECT_PARTICLE_ENVCUBE_2026-10-03.md Q6/Q7). MaterialLib's DataContract reads the document
''' once it is brought to MaterialLib's names and the reader's literals are filled in.</item>
''' <item><b>Raw blend pair.</b> The engine copies the file's blend byte and src/dst ints (offsets 0x20 / 0x21 / 0x25)
''' into the alpha property as they are (0x142171954..19CC); MaterialLib only knows five tuples and throws on any
''' other (BaseMaterialFile.cs:363-387). Any other tuple is kept RAW here: MaterialLib reads a copy carrying its
''' Unknown tuple (0,6,7), the raw pair travels next to the material, and writing puts it back.</item>
''' </list></summary>
Public Module MaterialFileReader

    ''' <summary>A file's blend pair MaterialLib cannot represent: as the engine reads it (blend = byte != 0, src, dst) and
    ''' the 9 raw bytes at 0x20..0x28, written back verbatim.</summary>
    Public NotInheritable Class BlendCrudoArchivo
        Public ReadOnly Blend As FO4UnifiedMaterial_Class.MaterialFileBlend
        Public ReadOnly Bytes As Byte()
        Public Sub New(blend As FO4UnifiedMaterial_Class.MaterialFileBlend, bytes As Byte())
            Me.Blend = blend : Me.Bytes = bytes
        End Sub
    End Class

    ''' <summary>What a read produced: the material, and the file's blend pair when MaterialLib cannot represent it.</summary>
    Public NotInheritable Class LecturaMaterial
        Public Property Material As BaseMaterialFile
        ''' <summary>Nothing when the file's tuple is one of MaterialLib's five. Valid while the material's AlphaBlendMode
        ''' stays Unknown (an edit of the mode replaces it).</summary>
        Public Property BlendCrudo As BlendCrudoArchivo
        Public Property EsJson As Boolean
        ''' <summary>Ola 7h round 4e (re27 1): why MaterialLib could not read this binary file, which was then read as the ENGINE reads it
        ''' (LeerBinarioComoElMotor: what it reached, the BSShaderData ctor's values beyond); Nothing for a full read.</summary>
        Public Property Ilegible As String
        ''' <summary>Ola 7h round 4e (re28 1): the JSON key whose value has a type the engine's reader cannot convert - jsoncpp's
        ''' asFloat / asBool / asInt / asCString hit _wassert + abort() (asFloat 0x1411CCAA0 -&gt; 0x1411CCB9F / 0x1411CCBA5): the game
        ''' closes. The material is then the BSShaderData ctor's (what the preview draws) and the frame says so as an ERROR
        ''' (PreviewModel.RenderableMesh.ReportMaterialFileCrash). Nothing otherwise.</summary>
        Public Property TipoIncorrecto As String
        ''' <summary>Ola 7h round 4g: the warning a save of this read gives (MaterialFileReader.AvisoAlGuardar), Nothing for a full read.</summary>
        Public Function AvisoAlGuardar(ruta As String) As String
            Return MaterialFileReader.AvisoAlGuardar(ruta, Ilegible, TipoIncorrecto)
        End Function
    End Class

    ''' <summary>A JSON value of a type the engine's reader aborts on (see LecturaMaterial.TipoIncorrecto): its key.</summary>
    Private NotInheritable Class TipoIncorrectoDelMotor
        Inherits Exception
        Friend ReadOnly Clave As String
        Friend Sub New(clave As String, mensaje As String)
            MyBase.New(mensaje)
            Me.Clave = clave
        End Sub
    End Class

    ''' <summary>The five (blend byte, src, dst) MaterialLib converts (BaseMaterialFile.ConvertAlphaBlendMode).</summary>
    Private ReadOnly TuplasMaterialLib As (Byte, UInteger, UInteger)() = {(0, 6, 7), (0, 0, 0), (1, 6, 7), (1, 6, 0), (1, 4, 1)}

    Private Const OffsetBlend As Integer = &H20
    Private Const OffsetSrc As Integer = &H21
    Private Const OffsetDst As Integer = &H25

    ''' <summary>Ola 7h round 4f: THE file's blend pair as the engine takes it - ApplyMaterialData reads the src and dst fields of the
    ''' BSShaderData (+0x7C / +0x80, the file's dwords at 0x21 / 0x25) as 16-bit unsigned WORDS (movzx word, 0x142171954 / 0x142171976)
    ''' and the blend byte +0x78 as "not zero" (0x142171997). No checked conversion: any dword the file holds is a valid read. The whole
    ''' word goes on to the property: what of it lands beyond the src / dst bits is EngineAlphaState.PairSpill (ola 7h round 4g).</summary>
    Private Function ParCrudoDelMotor(b0 As Byte, src As UInteger, dst As UInteger) As FO4UnifiedMaterial_Class.MaterialFileBlend
#If DEBUG Then
        ' Ola 7h tester round 4f: the checked conversion of the pair (CInt of a UInteger) as before.
        If Fo4GBufferConstants.GateMutantOn("p7h-r4f-blend-checked") Then
            Return New FO4UnifiedMaterial_Class.MaterialFileBlend With {
                .Byte0 = b0 <> 0, .Src = CType(CInt(src), NiflySharp.Enums.AlphaFunction), .Dst = CType(CInt(dst), NiflySharp.Enums.AlphaFunction)}
        End If
#End If
        Return New FO4UnifiedMaterial_Class.MaterialFileBlend With {
            .Byte0 = b0 <> 0, .Src = CType(CInt(src And &HFFFFUI), NiflySharp.Enums.AlphaFunction), .Dst = CType(CInt(dst And &HFFFFUI), NiflySharp.Enums.AlphaFunction)}
    End Function

    Public Function Leer(bytes As Byte(), type As Type) As LecturaMaterial
        If type IsNot GetType(BGSM) AndAlso type IsNot GetType(BGEM) Then Throw New ArgumentException("Unsupported material type: " & type?.Name)
        ' The engine's parse 0x1417A9420 dispatches on the FIRST byte of the stream (re27 1): '{' goes to the JSON reader 0x14216EF30, any
        ' other byte - whitespace included - to the binary reader 0x14216BF20.
        If bytes.Length > 0 AndAlso bytes(0) = AscW("{"c) Then Return LeerJsonComoElMotor(bytes, 0, type)
#If DEBUG Then
        ' Ola 7h tester round 4f: leading blanks skipped before the "{" as before.
        If Fo4GBufferConstants.GateMutantOn("p7h-r4f-skip-space") Then
            Dim iBlanco = 0
            While iBlanco < bytes.Length AndAlso (bytes(iBlanco) = 32 OrElse bytes(iBlanco) = 9 OrElse bytes(iBlanco) = 13 OrElse bytes(iBlanco) = 10)
                iBlanco += 1
            End While
            If iBlanco < bytes.Length AndAlso bytes(iBlanco) = AscW("{"c) Then Return LeerJsonComoElMotor(bytes, iBlanco, type)
        End If
#End If
        ' The engine has no parse error (re27 1): the stream that opened is parsed by 0x1417A9420 into a NEW BSShaderData (ctor
        ' 0x14216A4E0) by the binary reader 0x14216BF20, which checks neither magic nor version nor the buffer's end, and its caller
        ' 0x1417A9B50 ignores the parse. A file MaterialLib refuses (bad magic, truncated, a string past the end) is read as the engine
        ' reads it - the app's one reader for shapes and layers - and says so (Ilegible).
        Try
            Return LeerBinario(bytes, type)
        Catch ex As Exception
            ' Ola 7h round 4g (reviewer r7h-06): ANY failure of MaterialLib's read, whatever its exception type, is the engine's read -
            ' the engine has no parse error, so no exception of the third party leaves this reader.
#If DEBUG Then
            ' Ola 7h tester round 4e (ShadowGate --ola7h-r4e): a file MaterialLib refuses is not read as the engine reads it - it throws again.
            If Fo4GBufferConstants.GateMutantOn("p7h-r4e-no-engine-read") Then Throw
#End If
            Return LeerBinarioComoElMotor(bytes, type, ex.GetType().Name & ": " & ex.Message)
        End Try
    End Function

    ''' <summary>THE ENGINE'S JSON READER 0x14216EF30 (re28 1): the text is the buffer up to its first NUL (0x1417A9420 ends it with 0,
    ''' 0x1417A953E; strrchr stops there), cut at its LAST '}' (strrchr 0x14216EF70); without one nothing is parsed. Json::Reader::parse
    ''' 0x1411D52F0 stops at the first error and fills nothing (0x14216EF94): the BSShaderData 0x1417A9420 already made stays whole with
    ''' the ctor 0x14216A4E0's values, and LoadMaterialFile still returns 0 - so the caller applies them (MaterialDelCtorDelMotor, the
    ''' frame's "unreadable"). A valid document goes to the filler 0x14216EFF0 (LeerJson). A value of a type its conversion aborts on
    ''' (TipoIncorrectoDelMotor) closes the game: the preview draws the ctor's values and names the key (LecturaMaterial.TipoIncorrecto).
    ''' HUECO, declared: System.Text.Json and jsoncpp do not accept exactly the same documents (comments, trailing commas, nesting depth,
    ''' invalid UTF-8); each key is read as the filler reads it (LeerJson, ola 7h round 4g).</summary>
    Private Function LeerJsonComoElMotor(bytes As Byte(), inicio As Integer, type As Type) As LecturaMaterial
        Dim fin = Array.IndexOf(bytes, CByte(0))
        If fin < 0 Then fin = bytes.Length
        Dim cierre = Array.LastIndexOf(bytes, CByte(AscW("}"c)), Math.Max(0, fin - 1), fin)
        If cierre < inicio Then
            Return LeerBinarioComoElMotorVacio(type, "JSON material without a closing '}': nothing parsed (0x14216EF70)")
        End If
        Dim texto(cierre) As Byte
        Array.Copy(bytes, texto, cierre + 1)
        Try
            Return LeerJson(texto, inicio, type)
        Catch ex As TipoIncorrectoDelMotor
            Dim l = LeerBinarioComoElMotorVacio(type, Nothing)
            l.TipoIncorrecto = ex.Clave
            l.EsJson = True
            Return l
        Catch ex As System.Text.Json.JsonException
            Return LeerBinarioComoElMotorVacio(type, "JSON material that does not parse: nothing filled (0x14216EF94): " & ex.Message)
        End Try
    End Function

    ''' <summary>The BSShaderData ctor's values with nothing read (MaterialDelCtorDelMotor), as a read of the engine.</summary>
    Private Function LeerBinarioComoElMotorVacio(type As Type, motivo As String) As LecturaMaterial
        Return New LecturaMaterial With {.Material = MaterialDelCtorDelMotor(type), .BlendCrudo = Nothing, .EsJson = True, .Ilegible = motivo}
    End Function

    ''' <summary>The engine's cursor over a material file's bytes (0x14216BF20 reads [rsi+0x10] byte by byte). Past the end the engine
    ''' reads whatever follows the heap buffer - undefined (re27 1); the preview stops there (FinDelArchivo) and the fields not reached
    ''' keep the BSShaderData ctor's values: the same case as a partial read, and the frame says so.</summary>
    Private NotInheritable Class CursorDelMotor
        Private ReadOnly _b As Byte()
        Private _p As Integer
        Friend Sub New(b As Byte())
            _b = If(b, Array.Empty(Of Byte)())
        End Sub
        Private Sub Pedir(n As Integer)
            If n < 0 OrElse _p + n > _b.Length Then Throw New FinDelArchivo()
        End Sub
        Friend Sub Saltar(n As Integer)
            Pedir(n) : _p += n
        End Sub
        Friend Function U8() As Byte
            Pedir(1) : _p += 1 : Return _b(_p - 1)
        End Function
        Friend Function B() As Boolean
            Return U8() <> 0
        End Function
        Friend Function U32() As UInteger
            Pedir(4) : _p += 4 : Return BitConverter.ToUInt32(_b, _p - 4)
        End Function
        Friend Function F32() As Single
            Pedir(4) : _p += 4 : Return BitConverter.ToSingle(_b, _p - 4)
        End Function
        ''' <summary>A string: its length (4 bytes, from the file) and that many bytes (0x14216C436..45F, the texture set's
        ''' vfunc +0x160), decoded and with its last NUL dropped as MaterialLib's ReadString does.</summary>
        Friend Function Str() As String
            Dim n = U32()
            If n = 0UI Then Return ""
            If n > CUInt(_b.Length - _p) Then Throw New FinDelArchivo()
            Dim s = System.Text.Encoding.UTF8.GetString(_b, _p, CInt(n))
            _p += CInt(n)
            Dim k = s.LastIndexOf(ChrW(0))
            Return If(k >= 0, s.Remove(k, 1), s)
        End Function
        ''' <summary>A colour: 3 floats (0x142176690), packed as MaterialLib packs a file's colour.</summary>
        Friend Function Rgb() As UInteger
            Dim r = F32(), g = F32(), bb = F32()
            Return EmpaquetarRgb(r, g, bb)
        End Function
    End Class

    Private NotInheritable Class FinDelArchivo
        Inherits Exception
    End Class

    ''' <summary>A colour of 3 floats as MaterialLib stores it: (byte)(c * 255) per channel (BaseMaterialFile.Color.ToUInt32), C#'s unchecked
    ''' float-to-byte conversion on .NET 8 x64 - the product truncated to a 32-bit integer and its low byte; a product that is NaN or
    ''' outside the 32-bit range gives cvttss2si's 0x80000000, low byte 0 (measured, D:\WinTmp\rc\ola7h\r4\tmp\convu1: NaN, +-inf,
    ''' +-1e10 -> 0; -1 -> 1; -0.5 -> 129; 2.5 -> 125; 8.47 -> 111). Ola 7h round 4g: the checked CInt it had threw OverflowException on
    ''' such a product - an exception out of the engine's reader for a file whose colour floats are corrupt.</summary>
    Private Function EmpaquetarRgb(r As Single, g As Single, b As Single) As UInteger
        Dim c = Function(v As Single) As UInteger
                    Dim x = v * 255.0F
                    If Single.IsNaN(x) OrElse x < -2147483648.0F OrElse x >= 2147483648.0F Then Return 0UI
                    Return CUInt(CInt(Math.Truncate(x)) And &HFF)
                End Function
        Return (c(r) << 16) Or (c(g) << 8) Or c(b)
    End Function

    ''' <summary>THE BSShaderData A NEW PARSE STARTS FROM (ctor 0x14216A4E0; r4\re\bsd_ctor.txt), in MaterialLib's fields - every member
    ''' the binary reader can write (r4\re\binorder.txt: the struct byte each read goes to). TileU / TileV off (+0x1D8 = 0); UV offset
    ''' 0 / scale 1 (+0x1AC..0x1BB); alpha 1 (+0x60); blend (0, 6, 7) (+0x78 / 0x7C / 0x80); test ref 0, test off (+0x8C, +0x84); Z write
    ''' and test on (+0xB9 / 0xBA); every other boolean off (+0x8D..0xA9, +0xBB, +0xC0, +0x17A..0x17C, +0x18F, +0x190, +0x1A4, +0x1BC,
    ''' +0x1BD); refraction power 0 (+0xBC); env mask scale 1 (+0x180); textures and root path empty (texture set, +0x58). Effect: base
    ''' colour 0 (+0x1C), its scale 1 (+0x28), falloff (0, 0, 0, 1) (+0x194..0x1A3), lighting influence 1 (+0x2C), env min LOD 0 (+0x30),
    ''' soft depth 100 (+0x1A8). Lighting: rim / back light power and rolloff 0 (+0xB0 / 0xB4 / 0xAC), specular colour 0 (+0xC) and mult
    ''' 0 (+0x18), smoothness 1 (+0x34), fresnel 5 (+0x38), the six wetness values -1 (+0x3C..0x50), emittance colour 0 (+0x1C) and mult
    ''' 1 (+0x64), hair tint (0.5, 0.5, 0.5) (+0x1C0), tessellation values 0 (+0x164..0x177), greyscale-to-palette scale 1 (+0x68).</summary>
    Public Function MaterialDelCtorDelMotor(type As Type) As BaseMaterialFile
        If type IsNot GetType(BGSM) AndAlso type IsNot GetType(BGEM) Then Throw New ArgumentException("Unsupported material type: " & type?.Name)
        Dim m As BaseMaterialFile = If(type Is GetType(BGSM), CType(New BGSM(), BaseMaterialFile), New BGEM())
        m.Version = 2UI
        m.TileU = False : m.TileV = False
        m.UOffset = 0.0F : m.VOffset = 0.0F : m.UScale = 1.0F : m.VScale = 1.0F
        m.Alpha = 1.0F
        m.AlphaBlendMode = AlphaBlendModeType.Unknown          ' (0, 6, 7)
        m.AlphaTestRef = 0 : m.AlphaTest = False
        m.ZBufferWrite = True : m.ZBufferTest = True
        m.ScreenSpaceReflections = False : m.WetnessControlScreenSpaceReflections = False
        m.Decal = False : m.TwoSided = False : m.DecalNoFade = False : m.NonOccluder = False
        m.Refraction = False : m.RefractionFalloff = False : m.RefractionPower = 0.0F
        m.EnvironmentMapping = False : m.EnvironmentMappingMaskScale = 1.0F
        m.GrayscaleToPaletteColor = False
        Dim e = TryCast(m, BGEM)
        If e IsNot Nothing Then
            e.BaseTexture = "" : e.GrayscaleTexture = "" : e.EnvmapTexture = "" : e.NormalTexture = "" : e.EnvmapMaskTexture = ""
            e.BloodEnabled = False : e.EffectLightingEnabled = False : e.FalloffEnabled = False : e.FalloffColorEnabled = False
            e.GrayscaleToPaletteAlpha = False : e.SoftEnabled = False
            e.BaseColor = 0UI : e.BaseColorScale = 1.0F
            e.FalloffStartAngle = 0.0F : e.FalloffStopAngle = 0.0F : e.FalloffStartOpacity = 0.0F : e.FalloffStopOpacity = 1.0F
            e.LightingInfluence = 1.0F : e.EnvmapMinLOD = 0 : e.SoftDepth = 100.0F
        End If
        Dim s = TryCast(m, BGSM)
        If s IsNot Nothing Then
            s.DiffuseTexture = "" : s.NormalTexture = "" : s.SmoothSpecTexture = "" : s.GreyscaleTexture = "" : s.EnvmapTexture = ""
            s.GlowTexture = "" : s.InnerLayerTexture = "" : s.WrinklesTexture = "" : s.DisplacementTexture = ""
            s.EnableEditorAlphaRef = False : s.RimLighting = False : s.RimPower = 0.0F : s.BackLightPower = 0.0F
            s.SubsurfaceLighting = False : s.SubsurfaceLightingRolloff = 0.0F
            s.SpecularEnabled = False : s.SpecularColor = 0UI : s.SpecularMult = 0.0F : s.Smoothness = 1.0F : s.FresnelPower = 5.0F
            s.WetnessControlSpecScale = -1.0F : s.WetnessControlSpecPowerScale = -1.0F : s.WetnessControlSpecMinvar = -1.0F
            s.WetnessControlEnvMapScale = -1.0F : s.WetnessControlFresnelPower = -1.0F : s.WetnessControlMetalness = -1.0F
            s.RootMaterialPath = ""
            s.AnisoLighting = False : s.EmitEnabled = False : s.EmittanceColor = 0UI : s.EmittanceMult = 1.0F
            s.ModelSpaceNormals = False : s.ExternalEmittance = False : s.BackLighting = False : s.ReceiveShadows = False
            s.HideSecret = False : s.CastShadows = False : s.DissolveFade = False : s.AssumeShadowmask = False : s.Glowmap = False
            s.EnvironmentMappingWindow = False : s.EnvironmentMappingEye = False
            s.Hair = False : s.HairTintColor = EmpaquetarRgb(0.5F, 0.5F, 0.5F)
            s.Tree = False : s.Facegen = False : s.SkinTint = False : s.Tessellate = False
            s.DisplacementTextureBias = 0.0F : s.DisplacementTextureScale = 0.0F : s.TessellationPnScale = 0.0F
            s.TessellationBaseFactor = 0.0F : s.TessellationFadeDistance = 0.0F
            s.GrayscaleToPaletteScale = 1.0F : s.SkewSpecularAlpha = False
        End If
        Return m
    End Function

    ''' <summary>THE ENGINE'S BINARY READER 0x14216BF20 (r4\re\binorder.txt, from D:\WinTmp\rc\re27\bin.txt), field by field in its order,
    ''' over MaterialDelCtorDelMotor: 4 bytes of magic skipped without a compare (0x14216BFD9), the version read and used only for the
    ''' last lighting byte (+0xA5 when version &gt;= 1, 0x14216D5EA), then FO4's fixed layout - the order of MaterialLib's version-2 read
    ''' (the family is the caller's, by extension; re27 1). The first field the bytes do not hold ends the read: that field and every
    ''' later one keep the ctor's value (beyond the buffer the engine reads undefined memory; the same case, declared). Also the
    ''' 0-byte file: nothing read, the ctor's values (F4EE applies its stack BSShaderData, re27 1).</summary>
    Public Function LeerBinarioComoElMotor(bytes As Byte(), type As Type, motivo As String) As LecturaMaterial
        Dim m = MaterialDelCtorDelMotor(type)
        Dim c As New CursorDelMotor(bytes)
        Dim crudo As BlendCrudoArchivo = Nothing
        Dim b0 As Byte = 0, src As UInteger = 6UI, dst As UInteger = 7UI
        Dim fijarBlend = Sub()
                             If TuplasMaterialLib.Contains((b0, src, dst)) Then
                                 m.AlphaBlendMode = ModoDeTupla(b0, src, dst)
                                 crudo = Nothing
                             Else
                                 m.AlphaBlendMode = AlphaBlendModeType.Unknown
                                 Dim raw(8) As Byte
                                 raw(0) = b0 : BitConverter.GetBytes(src).CopyTo(raw, 1) : BitConverter.GetBytes(dst).CopyTo(raw, 5)
                                 crudo = New BlendCrudoArchivo(ParCrudoDelMotor(b0, src, dst), raw)
                             End If
                         End Sub
        Try
            c.Saltar(4)                                                       ' magic, not compared (0x14216BFD9)
            Dim version = c.U32()                                             ' to the stack; read again only at 0x14216D5EA
            Dim tile = c.U32()                                                ' +0x1D8
            m.TileU = (tile And 2UI) <> 0UI : m.TileV = (tile And 1UI) <> 0UI
            m.UOffset = c.F32() : m.VOffset = c.F32() : m.UScale = c.F32() : m.VScale = c.F32()   ' +0x1AC..0x1BB
            m.Alpha = c.F32()                                                 ' +0x60
            b0 = c.U8() : fijarBlend()                                        ' +0x78
            src = c.U32() : fijarBlend()                                      ' +0x7C
            dst = c.U32() : fijarBlend()                                      ' +0x80
            m.AlphaTestRef = c.U8() : m.AlphaTest = c.B()                     ' +0x8C, +0x84
            m.ZBufferWrite = c.B() : m.ZBufferTest = c.B()                    ' +0xB9, +0xBA
            m.ScreenSpaceReflections = c.B() : m.WetnessControlScreenSpaceReflections = c.B()   ' +0xA3, +0x54
            m.Decal = c.B() : m.TwoSided = c.B() : m.DecalNoFade = c.B() : m.NonOccluder = c.B()   ' +0x91, +0x99, +0x94, +0xA4
            m.Refraction = c.B() : m.RefractionFalloff = c.B() : m.RefractionPower = c.F32()      ' +0xBB, +0xC0, +0xBC
            m.EnvironmentMapping = c.B() : m.EnvironmentMappingMaskScale = c.F32()                ' +0x17A, +0x180
            m.GrayscaleToPaletteColor = c.B()                                                     ' +0x1BC
            Dim e = TryCast(m, BGEM)
            If e IsNot Nothing Then                                           ' the caller's flag 0 (0x14216C3DA)
                e.BaseTexture = c.Str() : e.GrayscaleTexture = c.Str() : e.EnvmapTexture = c.Str() : e.NormalTexture = c.Str()
                e.EnvmapMaskTexture = c.Str()                                 ' texture set 0..4
                e.BloodEnabled = c.B() : e.EffectLightingEnabled = c.B() : e.FalloffEnabled = c.B() : e.FalloffColorEnabled = c.B()   ' +0x9B, +0x9D, +0x18F, +0x190
                e.GrayscaleToPaletteAlpha = c.B() : e.SoftEnabled = c.B()     ' +0x1BD, +0x1A4
                e.BaseColor = c.Rgb() : e.BaseColorScale = c.F32()            ' +0x1C, +0x28
                e.FalloffStartAngle = c.F32() : e.FalloffStopAngle = c.F32() : e.FalloffStartOpacity = c.F32() : e.FalloffStopOpacity = c.F32()   ' +0x194..0x1A3
                e.LightingInfluence = c.F32() : e.EnvmapMinLOD = c.U8() : e.SoftDepth = c.F32()        ' +0x2C, +0x30, +0x1A8
            Else                                                              ' flag 8 (0x14216C9B5)
                Dim s = DirectCast(m, BGSM)
                s.DiffuseTexture = c.Str() : s.NormalTexture = c.Str() : s.SmoothSpecTexture = c.Str() : s.GreyscaleTexture = c.Str()
                s.EnvmapTexture = c.Str() : s.GlowTexture = c.Str() : s.InnerLayerTexture = c.Str() : s.WrinklesTexture = c.Str()
                s.DisplacementTexture = c.Str()                               ' texture set 0, 1, 7, 3, 4, 2, 6, 5, 8
                s.EnableEditorAlphaRef = c.B()                                ' +0x8D
                s.RimLighting = c.B() : s.RimPower = c.F32() : s.BackLightPower = c.F32()             ' +0xA9, +0xB0, +0xB4
                s.SubsurfaceLighting = c.B() : s.SubsurfaceLightingRolloff = c.F32()                  ' +0xA8, +0xAC
                s.SpecularEnabled = c.B() : s.SpecularColor = c.Rgb() : s.SpecularMult = c.F32()      ' +0x8, +0xC, +0x18
                s.Smoothness = c.F32() : s.FresnelPower = c.F32()                                     ' +0x34, +0x38
                s.WetnessControlSpecScale = c.F32() : s.WetnessControlSpecPowerScale = c.F32() : s.WetnessControlSpecMinvar = c.F32()
                s.WetnessControlEnvMapScale = c.F32() : s.WetnessControlFresnelPower = c.F32() : s.WetnessControlMetalness = c.F32()   ' +0x3C..0x53
                s.RootMaterialPath = c.Str()                                  ' +0x58 (0x1421768B0)
                s.AnisoLighting = c.B() : s.EmitEnabled = c.B()               ' +0xB8, +0x6C
                If s.EmitEnabled Then s.EmittanceColor = c.Rgb()              ' +0x1C, only with +0x6C (0x14216D1FF..208)
                s.EmittanceMult = c.F32()                                     ' +0x64
                s.ModelSpaceNormals = c.B() : s.ExternalEmittance = c.B() : s.BackLighting = c.B()    ' +0x90, +0x92, +0xA7
                s.ReceiveShadows = c.B() : s.HideSecret = c.B() : s.CastShadows = c.B() : s.DissolveFade = c.B()   ' +0x9F, +0x93, +0x9E, +0xA0
                s.AssumeShadowmask = c.B() : s.Glowmap = c.B()                ' +0xA2, +0xA1
                s.EnvironmentMappingWindow = c.B() : s.EnvironmentMappingEye = c.B()                  ' +0x17C, +0x17B
                s.Hair = c.B() : s.HairTintColor = c.Rgb()                    ' +0x98, +0x1C0
                s.Tree = c.B() : s.Facegen = c.B() : s.SkinTint = c.B() : s.Tessellate = c.B()        ' +0x9A, +0x96, +0x97, +0xA6
                s.DisplacementTextureBias = c.F32() : s.DisplacementTextureScale = c.F32() : s.TessellationPnScale = c.F32()
                s.TessellationBaseFactor = c.F32() : s.TessellationFadeDistance = c.F32()             ' +0x164, 0x168, 0x16C, 0x174, 0x170
                s.GrayscaleToPaletteScale = c.F32()                                                   ' +0x68
                If version >= 1UI Then s.SkewSpecularAlpha = c.B()            ' +0xA5 (0x14216D5EA..5EE)
            End If
        Catch ex As FinDelArchivo
            ' The bytes ended: the fields not reached keep the ctor's values.
        End Try
        Return New LecturaMaterial With {.Material = m, .BlendCrudo = crudo, .EsJson = False, .Ilegible = If(motivo, "")}
    End Function

    ''' <summary>The mode MaterialLib gives one of its five tuples (BaseMaterialFile.ConvertAlphaBlendMode, the order of TuplasMaterialLib).</summary>
    Private Function ModoDeTupla(b0 As Byte, src As UInteger, dst As UInteger) As AlphaBlendModeType
        Select Case True
            Case b0 = 0 AndAlso src = 6UI AndAlso dst = 7UI : Return AlphaBlendModeType.Unknown
            Case b0 = 0 AndAlso src = 0UI AndAlso dst = 0UI : Return AlphaBlendModeType.None
            Case b0 = 1 AndAlso src = 6UI AndAlso dst = 7UI : Return AlphaBlendModeType.Standard
            Case b0 = 1 AndAlso src = 6UI AndAlso dst = 0UI : Return AlphaBlendModeType.Additive
            Case Else : Return AlphaBlendModeType.Multiplicative                ' (1, 4, 1)
        End Select
    End Function

    Private Function LeerBinario(bytes As Byte(), type As Type) As LecturaMaterial
#If DEBUG Then
        ' Ola 7h tester round 4g: MaterialLib's read throws a type outside the six round 4e filtered.
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-lib-throws-invalid") Then Throw New InvalidDataException("tester mutant")
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-lib-throws-null") Then Throw New NullReferenceException("tester mutant")
#End If
        Dim lectura = bytes
        Dim crudo As BlendCrudoArchivo = Nothing
        If bytes.Length >= OffsetDst + 4 Then
            Dim b0 = bytes(OffsetBlend)
            Dim src = BitConverter.ToUInt32(bytes, OffsetSrc), dst = BitConverter.ToUInt32(bytes, OffsetDst)
            If Not TuplasMaterialLib.Contains((b0, src, dst)) Then
                crudo = New BlendCrudoArchivo(ParCrudoDelMotor(b0, src, dst), bytes.Skip(OffsetBlend).Take(9).ToArray())
                lectura = CType(bytes.Clone(), Byte())
                lectura(OffsetBlend) = 0
                BitConverter.GetBytes(6UI).CopyTo(lectura, OffsetSrc)
                BitConverter.GetBytes(7UI).CopyTo(lectura, OffsetDst)
            End If
        End If
        Dim mat As BaseMaterialFile = If(type Is GetType(BGSM), CType(New BGSM(), BaseMaterialFile), New BGEM())
        Using ms As New MemoryStream(lectura)
            Using reader As New BinaryReader(ms)
                mat.Deserialize(reader)
            End Using
        End Using
        ' Ola 7h round 4g (r7h-07): the emittance colour +0x1C is in the file only with bEmitEnabled +0x6C (the engine's reader
        ' 0x14216D1FF..208; MaterialLib's BGSM.cs:405-409 alike). Without it the engine keeps the BSShaderData ctor's value
        ' (MaterialDelCtorDelMotor), not MaterialLib's constructor default 0xFFFFFFFF: the one field a full read and the engine's read
        ' disagreed on, so a material saved as it was read (partial, or with the engine's defaults) read back differently.
        Dim s = TryCast(mat, BGSM)
        If s IsNot Nothing AndAlso Not s.EmitEnabled Then s.EmittanceColor = DirectCast(MaterialDelCtorDelMotor(GetType(BGSM)), BGSM).EmittanceColor
        Return New LecturaMaterial With {.Material = mat, .BlendCrudo = crudo, .EsJson = False}
    End Function

    ' THE ENGINE'S JSON FILLER 0x14216EFF0 (ola 7h round 4g, reviewer r7h-06): ONE table, in the filler's order, of the keys it reads -
    ' each with the converter the filler calls on its value and the default root.get 0x1411D0E10 passes for an absent key (the whole
    ' disassembly: D:\WinTmp\rc\re28\jsd_out.txt; the texture keys of the helper 0x142176C00: r4\re\tex_keys2.py). A key outside the
    ' table is not read by the engine (eParallax* and bEnvironmentMappingLightFade are fetched and never converted; the *Enable flags,
    ' Glass*, translucency, MaskWrites are not fetched): the material carries the value the game uses, not the one the file wrote. The
    ' fill starts from the BSShaderData ctor's values (MaterialDelCtorDelMotor: the struct 0x1417A9420 makes before the parse), so a key
    ' the filler only reads through isMember keeps them when absent.

    ''' <summary>A Json::Value as the engine's jsoncpp holds it: the type byte [value+8] its converters switch on (null 0, int 1, uint 2,
    ''' real 3, string 4, bool 5, array 6, object 7: 0x1411CC6B0, 0x1411CCAA0, 0x1411CCF00) and the payload.</summary>
    Friend Structure ValorJson
        Friend Tipo As Byte
        Friend Entero As Long
        Friend SinSigno As ULong
        Friend Real As Double
        Friend Texto As String
        Friend Booleano As Boolean
    End Structure

    Private Const JsonNulo As Byte = 0, JsonEntero As Byte = 1, JsonSinSigno As Byte = 2, JsonReal As Byte = 3, JsonTexto As Byte = 4,
                  JsonBooleano As Byte = 5, JsonLista As Byte = 6, JsonObjeto As Byte = 7

    ''' <summary>The Json::Value Json::Reader builds for a JSON value. A number as Reader::decodeNumber 0x1411CEF00 classifies it: an
    ''' optional '-' and digits only, accumulated in 64 bits - an int when negative (down to -2^63) or up to 0x7FFFFFFF (0x1411CEFD5..CEFE9),
    ''' a uint above it (up to 2^64-1: the thresholds 0x1999999999999999 / 0x0CCCCCCCCCCCCCCC and the last digit, 0x1411CEF26..CEFAF);
    ''' any other character, or an overflow, goes to decodeDouble 0x1411CE850 - a real. HUECO, declared: decodeDouble's text-to-double is
    ''' not traced; .NET's correctly rounded parse stands in for it (it can differ in the last bit, or for a number beyond double's range).</summary>
    Private Function ValorDe(e As System.Text.Json.JsonElement) As ValorJson
        Select Case e.ValueKind
            Case System.Text.Json.JsonValueKind.Null : Return New ValorJson With {.Tipo = JsonNulo}
            Case System.Text.Json.JsonValueKind.True : Return New ValorJson With {.Tipo = JsonBooleano, .Booleano = True}
            Case System.Text.Json.JsonValueKind.False : Return New ValorJson With {.Tipo = JsonBooleano, .Booleano = False}
            Case System.Text.Json.JsonValueKind.String : Return New ValorJson With {.Tipo = JsonTexto, .Texto = e.GetString()}
            Case System.Text.Json.JsonValueKind.Array : Return New ValorJson With {.Tipo = JsonLista}
            Case System.Text.Json.JsonValueKind.Object : Return New ValorJson With {.Tipo = JsonObjeto}
        End Select
        Dim token = e.GetRawText()
        Dim negativo = token.StartsWith("-", StringComparison.Ordinal)
        Dim digitos = If(negativo, token.Substring(1), token)
        Dim magnitud As ULong
        If digitos.Length > 0 AndAlso digitos.All(Function(ch) ch >= "0"c AndAlso ch <= "9"c) AndAlso
           ULong.TryParse(digitos, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, magnitud) AndAlso
           (Not negativo OrElse magnitud <= &H8000000000000000UL) Then
            If negativo Then Return New ValorJson With {.Tipo = JsonEntero, .Entero = If(magnitud = &H8000000000000000UL, Long.MinValue, -CLng(magnitud))}
            If magnitud <= &H7FFFFFFFUL Then Return New ValorJson With {.Tipo = JsonEntero, .Entero = CLng(magnitud)}
            Return New ValorJson With {.Tipo = JsonSinSigno, .SinSigno = magnitud}
        End If
        Return New ValorJson With {.Tipo = JsonReal, .Real = Double.Parse(token, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture)}
    End Function

    ''' <summary>The abort of a jsoncpp converter on a type it does not convert (_wassert + abort(): the game closes, re28 1), as the
    ''' reader's exception naming the key (LecturaMaterial.TipoIncorrecto).</summary>
    Private Function Aborta(clave As String, v As ValorJson, conversor As String) As TipoIncorrectoDelMotor
        Dim tipo = {"null", "an int", "a uint", "a real", "a string", "a bool", "an array", "an object"}(v.Tipo)
        Return New TipoIncorrectoDelMotor(clave, $"JSON material: '{clave}' is {tipo}; the engine aborts on it (Json::Value::{conversor}).")
    End Function

    ''' <summary>Json::Value::asBool 0x1411CC6B0: null false, an int / uint not 0, a real not 0.0, a bool itself; a string, an array or
    ''' an object abort.</summary>
    Private Function ComoBool(clave As String, v As ValorJson) As Boolean
#If DEBUG Then
        ' Ola 7h tester round 4g (ShadowGate --ola7h-r4e V): jsoncpp's conversions are not modelled - a number aborts asBool.
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-no-conversions") AndAlso (v.Tipo = JsonEntero OrElse v.Tipo = JsonSinSigno OrElse v.Tipo = JsonReal) Then Throw Aborta(clave, v, "asBool")
#End If
        Select Case v.Tipo
            Case JsonNulo : Return False
            Case JsonEntero : Return v.Entero <> 0
            Case JsonSinSigno : Return v.SinSigno <> 0UL
            Case JsonReal : Return v.Real <> 0.0
            Case JsonBooleano : Return v.Booleano
        End Select
        Throw Aborta(clave, v, "asBool")
    End Function

    ''' <summary>Json::Value::asFloat 0x1411CCAA0: null 0, an int / uint / real converted to float (to nearest), a bool 1 / 0; a string,
    ''' an array or an object abort (0x1411CCB9F / 0x1411CCBA5). HUECO, declared: a uint at or above 2^63 goes through MSVC's own
    ''' unsigned sequence, not traced (.NET's correctly rounded conversion stands in).</summary>
    Private Function ComoFloat(clave As String, v As ValorJson) As Single
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-no-conversions") AndAlso v.Tipo = JsonBooleano Then Throw Aborta(clave, v, "asFloat")
#End If
        Select Case v.Tipo
            Case JsonNulo : Return 0.0F
            Case JsonEntero : Return CSng(v.Entero)
            Case JsonSinSigno : Return CSng(v.SinSigno)
            Case JsonReal : Return CSng(v.Real)
            Case JsonBooleano : Return If(v.Booleano, 1.0F, 0.0F)
        End Select
        Throw Aborta(clave, v, "asFloat")
    End Function

    ''' <summary>0x1411CCF00 (jsoncpp's asInt; re28 calls it asUInt) and the filler's store of its LOW BYTE (fAlphaTestRef: mov byte +0x8C
    ''' 0x14216F3CB; iEnvmapMinLOD: +0x30 0x14216FEFA). null 0; an int within [-2^31, 2^31-1] or a uint up to 0x7FFFFFFF as it is (isInt
    ''' 0x1411D3720), any other aborts; a real within [-2147483648.0, 2147483647.0] truncated (cvttsd2si 0x1411CD032), outside it - NaN
    ''' too - aborts (0x1411CD01A..CD030); a bool 1 / 0; a string, an array or an object abort.</summary>
    Private Function ComoByteBajo(clave As String, v As ValorJson) As Byte
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-no-conversions") AndAlso (v.Tipo = JsonReal OrElse v.Tipo = JsonBooleano) Then Throw Aborta(clave, v, "asInt")
#End If
        Dim n As Integer
        Select Case v.Tipo
            Case JsonNulo
                n = 0
            Case JsonEntero
                If v.Entero < Integer.MinValue OrElse v.Entero > Integer.MaxValue Then Throw Aborta(clave, v, "asInt")
                n = CInt(v.Entero)
            Case JsonSinSigno
                If v.SinSigno > &H7FFFFFFFUL Then Throw Aborta(clave, v, "asInt")
                n = CInt(v.SinSigno)
            Case JsonReal
                If Not (v.Real >= -2147483648.0 AndAlso v.Real <= 2147483647.0) Then Throw Aborta(clave, v, "asInt")
                n = CInt(Math.Truncate(v.Real))
            Case JsonBooleano
                n = If(v.Booleano, 1, 0)
            Case Else
                Throw Aborta(clave, v, "asInt")
        End Select
        Return CByte(n And &HFF)
    End Function

    ''' <summary>Json::Value::asCString 0x1411CC810: the string, which its readers take as a C string (up to its first NUL); any other
    ''' type - null too - hits _wassert("requires stringValue", jsoncpp.cpp:2987) and abort() (0x1411CC82E..CC8F2)
    ''' (Tools/re-docs/RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 3).</summary>
    Private Function ComoCadena(clave As String, v As ValorJson) As String
        If v.Tipo <> JsonTexto Then Throw Aborta(clave, v, "asCString")
        Dim k = v.Texto.IndexOf(ChrW(0))
        Return If(k >= 0, v.Texto.Substring(0, k), v.Texto)
    End Function

    ''' <summary>The texture helper 0x142176C00 on a present key: asCString, then ONE leading '\' or '/' stripped and a doubled
    ''' backslash collapsed (0x142176C54..C9A); "" is the empty slot (0x142924FED).</summary>
    Private Function ComoTextura(clave As String, v As ValorJson) As String
        Dim s = ComoCadena(clave, v)
        If s.StartsWith("\", StringComparison.Ordinal) OrElse s.StartsWith("/", StringComparison.Ordinal) Then s = s.Substring(1)
        Return s.Replace("\\", "\")
    End Function

    ''' <summary>A colour as the engine holds it: three floats (NiColor at +0xC, +0x1C, +0x1C0).</summary>
    Friend Structure RgbDelMotor
        Friend R As Single, G As Single, B As Single
        Friend Sub New(r As Single, g As Single, b As Single)
            Me.R = r : Me.G = g : Me.B = b
        End Sub
    End Structure

    ''' <summary>1/255 as the colour parser multiplies by it (0x142176DF9: the float 0x3B808081 at 0x1429293FC).</summary>
    Private ReadOnly UnoSobre255 As Single = BitConverter.Int32BitsToSingle(&H3B808081)

    ''' <summary>One digit as 0x142176DA0 computes it: the byte plus 0xC9 when it is 'A'..'F' (byte - 0x41 &lt;= 5 unsigned), plus 0xA9
    ''' when 'a'..'f' (byte - 0x61 &lt;= 5), plus 0xD0 otherwise - in 8 bits, no validation - then read signed (movsx).</summary>
    Private Function DigitoDelMotor(c As Byte) As Integer
        Dim suma = If(((c + &HBF) And &HFF) <= 5, &HC9, If(((c + &H9F) And &HFF) <= 5, &HA9, &HD0))
        Dim r = (c + suma) And &HFF
        Return If(r >= &H80, r - &H100, r)
    End Function

    ''' <summary>THE ENGINE'S COLOUR PARSER 0x142176CE0 on the C string asCString gives: one leading '#' skipped (0x142176CEA..CFB); "fff"
    ''' (_stricmp 0x142176D02, ASCII case) is (1, 1, 1) and "000" (0x142176D36) (0, 0, 0) - the constants at 0x142F4C610 / 0x143449698;
    ''' otherwise three channels of two characters each through DigitoDelMotor, (hi * 16 + lo) as a float times 1/255. The parser reads
    ''' six characters whatever the string's length: its NUL (at its length) reads as 0; a character beyond it is memory after the
    ''' string - undefined. HUECO, declared: such a channel keeps <paramref name="previo"/> (the struct's value before the write: the
    ''' ctor's), and <paramref name="indefinido"/> says so (the read's Ilegible, the frame's "unreadable").</summary>
    Private Function ColorDelMotor(s As String, previo As RgbDelMotor, ByRef indefinido As Boolean) As RgbDelMotor
        Dim b = System.Text.Encoding.UTF8.GetBytes(s)
        Dim p = If(b.Length > 0 AndAlso b(0) = &H23, 1, 0)
        Dim n = b.Length - p
        Dim minuscula = Function(c As Byte) As Integer
                            Return If(c >= &H41 AndAlso c <= &H5A, c + &H20, CInt(c))
                        End Function
        Dim es = Function(t As String) n = t.Length AndAlso Enumerable.Range(0, n).All(Function(i) minuscula(b(p + i)) = AscW(t(i)))
        If es("fff") Then Return New RgbDelMotor(1.0F, 1.0F, 1.0F)
        If es("000") Then Return New RgbDelMotor(0.0F, 0.0F, 0.0F)
        Dim antes = {previo.R, previo.G, previo.B}
        Dim v(2) As Single
        For k = 0 To 2
            If 2 * k + 1 > n Then
                v(k) = antes(k)
                indefinido = True
                Continue For
            End If
            Dim hi = DigitoDelMotor(If(2 * k < n, b(p + 2 * k), CByte(0)))
            Dim lo = DigitoDelMotor(If(2 * k + 1 < n, b(p + 2 * k + 1), CByte(0)))
            v(k) = CSng(hi * 16 + lo) * UnoSobre255
        Next
        Return New RgbDelMotor(v(0), v(1), v(2))
    End Function

    ''' <summary>A colour as MaterialLib stores it (EmpaquetarRgb: its packing of a file's three floats).</summary>
    Private Function EmpaquetarRgb(c As RgbDelMotor) As UInteger
        Return EmpaquetarRgb(c.R, c.G, c.B)
    End Function

    ''' <summary>The converter the engine's JSON filler calls on a key's value (re28\jsd_out.txt, the call after each root.get / isMember).</summary>
    Friend Enum LectorJson
        ''' <summary>Json::Value::asBool 0x1411CC6B0.</summary>
        Bool
        ''' <summary>Json::Value::asFloat 0x1411CCAA0.</summary>
        Float
        ''' <summary>0x1411CCF00, its low byte stored.</summary>
        ByteBajo
        ''' <summary>Json::Value::asCString 0x1411CC810 (eAlphaBlendMode, sRootMaterialPath).</summary>
        Cadena
        ''' <summary>The texture helper 0x142176C00 (isMember, asCString, the path cleanup).</summary>
        Textura
        ''' <summary>asCString and the colour parser 0x142176CE0.</summary>
        Color
    End Enum

    ''' <summary>One key of the filler: its name; the one the filler reads instead when it is not a member (BGEM's greyscale texture
    ''' 0x14216F940, base colour 0x14216FBE4); the converter; the default root.get passes for an absent key (Nothing: the filler reads it
    ''' only through isMember, and absent the struct keeps its value); the struct's colour before the write (Color only, for an undefined
    ''' channel); and the MaterialLib field it fills (Nothing: a member MaterialLib does not have - the value is still converted, so a
    ''' type the engine aborts on still aborts).</summary>
    Friend NotInheritable Class ClaveDelLlenador
        Friend ReadOnly Clave As String
        Friend ReadOnly Alterna As String
        Friend ReadOnly Lector As LectorJson
        Friend ReadOnly Defecto As ValorJson?
        Friend ReadOnly Previo As RgbDelMotor
        Friend ReadOnly Poner As Action(Of BaseMaterialFile, Object)
        Friend Sub New(clave As String, lector As LectorJson, defecto As ValorJson?, poner As Action(Of BaseMaterialFile, Object), alterna As String, previo As RgbDelMotor)
            Me.Clave = clave : Me.Lector = lector : Me.Defecto = defecto : Me.Poner = poner : Me.Alterna = alterna : Me.Previo = previo
        End Sub
        ''' <summary>The default as the gate compares it with re28\jsd_out.txt: "bool 1", "double 0.3...", "cstr #ffffff", "uint 128", or "".</summary>
        Friend Function DefectoComoTexto() As String
            If Not Defecto.HasValue Then Return ""
            Dim d = Defecto.Value
            Select Case d.Tipo
                Case JsonBooleano : Return If(d.Booleano, "bool 1", "bool 0")
                Case JsonReal : Return "double " & d.Real.ToString("R", Globalization.CultureInfo.InvariantCulture)
                Case JsonTexto : Return "cstr " & d.Texto
                Case Else : Return "uint " & d.SinSigno.ToString(Globalization.CultureInfo.InvariantCulture)
            End Select
        End Function
    End Class

    Private Function Entrada(clave As String, lector As LectorJson, defecto As ValorJson?, poner As Action(Of BaseMaterialFile, Object),
                       Optional alterna As String = Nothing, Optional previo As RgbDelMotor = Nothing) As ClaveDelLlenador
        Return New ClaveDelLlenador(clave, lector, defecto, poner, alterna, previo)
    End Function
    Private Function DB(b As Boolean) As ValorJson?
        Return New ValorJson With {.Tipo = JsonBooleano, .Booleano = b}
    End Function
    Private Function DR(d As Double) As ValorJson?
        Return New ValorJson With {.Tipo = JsonReal, .Real = d}
    End Function
    Private Function DS(s As String) As ValorJson?
        Return New ValorJson With {.Tipo = JsonTexto, .Texto = s}
    End Function
    Private Function DU(u As ULong) As ValorJson?
        Return New ValorJson With {.Tipo = JsonSinSigno, .SinSigno = u}
    End Function
    Private ReadOnly SinDefecto As ValorJson? = Nothing
    Private Function Bgsm(m As BaseMaterialFile) As BGSM
        Return DirectCast(m, BGSM)
    End Function
    Private Function Bgem(m As BaseMaterialFile) As BGEM
        Return DirectCast(m, BGEM)
    End Function

    ''' <summary>eAlphaBlendMode, the same code for both families (0x14216F29F..F36F): blend = the string not empty and not "None"
    ''' (case-insensitive); src / dst Standard 6/7, Additive 6/0, Multiplicative 4/1, any other string keeps the ctor's 6/7. In
    ''' MaterialLib's enum: (0,6,7) Unknown, (1,6,7) Standard, (1,6,0) Additive, (1,4,1) Multiplicative.</summary>
    Private Function ModoDeBlendJson(modo As String) As AlphaBlendModeType
        If String.IsNullOrEmpty(modo) OrElse String.Equals(modo, "None", StringComparison.OrdinalIgnoreCase) Then Return AlphaBlendModeType.Unknown
        If String.Equals(modo, "Additive", StringComparison.OrdinalIgnoreCase) Then Return AlphaBlendModeType.Additive
        If String.Equals(modo, "Multiplicative", StringComparison.OrdinalIgnoreCase) Then Return AlphaBlendModeType.Multiplicative
        Return AlphaBlendModeType.Standard
    End Function

    ''' <summary>The keys both families read, in the filler's order (0x14216F0F4..F8C9).</summary>
    Private ReadOnly LlenadorComun As ClaveDelLlenador() = {
        Entrada("bTileU", LectorJson.Bool, DB(True), Sub(m, v) m.TileU = CBool(v)),
        Entrada("bTileV", LectorJson.Bool, DB(True), Sub(m, v) m.TileV = CBool(v)),
        Entrada("fUOffset", LectorJson.Float, SinDefecto, Sub(m, v) m.UOffset = CSng(v)),
        Entrada("fVOffset", LectorJson.Float, SinDefecto, Sub(m, v) m.VOffset = CSng(v)),
        Entrada("fUScale", LectorJson.Float, SinDefecto, Sub(m, v) m.UScale = CSng(v)),
        Entrada("fVScale", LectorJson.Float, SinDefecto, Sub(m, v) m.VScale = CSng(v)),
        Entrada("fAlpha", LectorJson.Float, DR(1.0), Sub(m, v) m.Alpha = CSng(v)),
        Entrada("eAlphaBlendMode", LectorJson.Cadena, DS("None"), Sub(m, v) m.AlphaBlendMode = ModoDeBlendJson(CStr(v))),
        Entrada("fAlphaTestRef", LectorJson.ByteBajo, DU(128), Sub(m, v) m.AlphaTestRef = CByte(v)),
        Entrada("bAlphaTest", LectorJson.Bool, DB(False), Sub(m, v) m.AlphaTest = CBool(v)),
        Entrada("bZBufferWrite", LectorJson.Bool, DB(True), Sub(m, v) m.ZBufferWrite = CBool(v)),
        Entrada("bZBufferTest", LectorJson.Bool, DB(True), Sub(m, v) m.ZBufferTest = CBool(v)),
        Entrada("bScreenSpaceReflections", LectorJson.Bool, DB(False), Sub(m, v) m.ScreenSpaceReflections = CBool(v)),
        Entrada("bWetnessControl_ScreenSpaceReflections", LectorJson.Bool, DB(False), Sub(m, v) m.WetnessControlScreenSpaceReflections = CBool(v)),
        Entrada("bDecal", LectorJson.Bool, DB(False), Sub(m, v) m.Decal = CBool(v)),
        Entrada("bTwoSided", LectorJson.Bool, DB(False), Sub(m, v) m.TwoSided = CBool(v)),
        Entrada("bDecalNoFade", LectorJson.Bool, DB(False), Sub(m, v) m.DecalNoFade = CBool(v)),
        Entrada("bNonOccluder", LectorJson.Bool, DB(False), Sub(m, v) m.NonOccluder = CBool(v)),
        Entrada("bSkewSpecularAlpha", LectorJson.Bool, DB(False), Sub(m, v)
                                                               If TypeOf m Is BGSM Then Bgsm(m).SkewSpecularAlpha = CBool(v)
                                                           End Sub),
        Entrada("bRefraction", LectorJson.Bool, DB(False), Sub(m, v) m.Refraction = CBool(v)),
        Entrada("bRefractionFalloff", LectorJson.Bool, DB(False), Sub(m, v) m.RefractionFalloff = CBool(v)),
        Entrada("fRefractionPower", LectorJson.Float, DR(0.0), Sub(m, v) m.RefractionPower = CSng(v)),
        Entrada("bEnvironmentMapping", LectorJson.Bool, DB(False), Sub(m, v) m.EnvironmentMapping = CBool(v)),
        Entrada("fEnvironmentMappingMaskScale", LectorJson.Float, DR(1.0), Sub(m, v) m.EnvironmentMappingMaskScale = CSng(v)),
        Entrada("bGrayscaleToPaletteColor", LectorJson.Bool, DB(False), Sub(m, v) m.GrayscaleToPaletteColor = CBool(v))}

    ''' <summary>BGEM (the filler's flag 0): its five textures (0x14216F931..F9DD), then 0x14216FA08..FF21.</summary>
    Private ReadOnly LlenadorBgem As ClaveDelLlenador() = {
        Entrada("sBaseTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgem(m).BaseTexture = CStr(v)),
        Entrada("sGreyscaleTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgem(m).GrayscaleTexture = CStr(v), alterna:="sGrayscaleTexture"),
        Entrada("sEnvmapTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgem(m).EnvmapTexture = CStr(v)),
        Entrada("sNormalTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgem(m).NormalTexture = CStr(v)),
        Entrada("sEnvmapMaskTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgem(m).EnvmapMaskTexture = CStr(v)),
        Entrada("bBloodEnabled", LectorJson.Bool, DB(False), Sub(m, v) Bgem(m).BloodEnabled = CBool(v)),
        Entrada("bEffectLightingEnabled", LectorJson.Bool, DB(False), Sub(m, v) Bgem(m).EffectLightingEnabled = CBool(v)),
        Entrada("bFalloffEnabled", LectorJson.Bool, DB(False), Sub(m, v) Bgem(m).FalloffEnabled = CBool(v)),
        Entrada("bFalloffColorEnabled", LectorJson.Bool, DB(False), Sub(m, v) Bgem(m).FalloffColorEnabled = CBool(v)),
        Entrada("bGrayscaleToPaletteAlpha", LectorJson.Bool, DB(False), Sub(m, v) Bgem(m).GrayscaleToPaletteAlpha = CBool(v)),
        Entrada("bSoftEnabled", LectorJson.Bool, DB(False), Sub(m, v) Bgem(m).SoftEnabled = CBool(v)),
        Entrada("cBaseColor", LectorJson.Color, DS("#ffffff"), Sub(m, v) Bgem(m).BaseColor = EmpaquetarRgb(CType(v, RgbDelMotor)), alterna:="uBaseColor",
          previo:=New RgbDelMotor(0.0F, 0.0F, 0.0F)),
        Entrada("fBaseColorScale", LectorJson.Float, DR(1.0), Sub(m, v) Bgem(m).BaseColorScale = CSng(v)),
        Entrada("fFalloffStartAngle", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).FalloffStartAngle = CSng(v)),
        Entrada("fFalloffStopAngle", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).FalloffStopAngle = CSng(v)),
        Entrada("fFalloffStartOpacity", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).FalloffStartOpacity = CSng(v)),
        Entrada("fFalloffStopOpacity", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).FalloffStopOpacity = CSng(v)),
        Entrada("fLightingInfluence", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).LightingInfluence = CSng(v)),
        Entrada("iEnvmapMinLOD", LectorJson.ByteBajo, DU(0), Sub(m, v) Bgem(m).EnvmapMinLOD = CByte(v)),
        Entrada("fSoftDepth", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).SoftDepth = CSng(v))}

    ''' <summary>BGSM (the filler's flag 8, 0x14216FF56): its nine textures in the helper's order (0x14216FF73..170042), then
    ''' 0x14217006D..1712AC. The emittance colour is read only with bEmitEnabled (0x142170744) and bGlowmap converted only with the glow
    ''' slot set (0x142170AF7..B22): LeerJson's loop.</summary>
    Private ReadOnly LlenadorBgsm As ClaveDelLlenador() = {
        Entrada("sDiffuseTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).DiffuseTexture = CStr(v)),
        Entrada("sNormalTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).NormalTexture = CStr(v)),
        Entrada("sSmoothSpecTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).SmoothSpecTexture = CStr(v)),
        Entrada("sGreyscaleTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).GreyscaleTexture = CStr(v)),
        Entrada("sEnvmapTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).EnvmapTexture = CStr(v)),
        Entrada("sGlowTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).GlowTexture = CStr(v)),
        Entrada("sInnerLayerTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).InnerLayerTexture = CStr(v)),
        Entrada("sWrinklesTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).WrinklesTexture = CStr(v)),
        Entrada("sDisplacementTexture", LectorJson.Textura, SinDefecto, Sub(m, v) Bgsm(m).DisplacementTexture = CStr(v)),
        Entrada("bEnableEditorAlphaRef", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).EnableEditorAlphaRef = CBool(v)),
        Entrada("bRimLighting", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).RimLighting = CBool(v)),
        Entrada("fRimPower", LectorJson.Float, DR(2.0), Sub(m, v) Bgsm(m).RimPower = CSng(v)),
        Entrada("fBackLightPower", LectorJson.Float, DR(0.0), Sub(m, v) Bgsm(m).BackLightPower = CSng(v)),
        Entrada("bSubsurfaceLighting", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).SubsurfaceLighting = CBool(v)),
        Entrada("fSubsurfaceLightingRolloff", LectorJson.Float, DR(0.30000001192092896), Sub(m, v) Bgsm(m).SubsurfaceLightingRolloff = CSng(v)),
        Entrada("bSpecularEnabled", LectorJson.Bool, DB(True), Sub(m, v) Bgsm(m).SpecularEnabled = CBool(v)),
        Entrada("cSpecularColor", LectorJson.Color, DS("#ffffff"), Sub(m, v) Bgsm(m).SpecularColor = EmpaquetarRgb(CType(v, RgbDelMotor)),
          previo:=New RgbDelMotor(0.0F, 0.0F, 0.0F)),
        Entrada("fSpecularMult", LectorJson.Float, DR(80.0), Sub(m, v) Bgsm(m).SpecularMult = CSng(v)),
        Entrada("fSmoothness", LectorJson.Float, DR(1.0), Sub(m, v) Bgsm(m).Smoothness = CSng(v)),
        Entrada("fFresnelPower", LectorJson.Float, DR(5.0), Sub(m, v) Bgsm(m).FresnelPower = CSng(v)),
        Entrada("fWetnessControl_SpecScale", LectorJson.Float, DR(-1.0), Sub(m, v) Bgsm(m).WetnessControlSpecScale = CSng(v)),
        Entrada("fWetnessControl_SpecPowerScale", LectorJson.Float, DR(-1.0), Sub(m, v) Bgsm(m).WetnessControlSpecPowerScale = CSng(v)),
        Entrada("fWetnessControl_SpecMin", LectorJson.Float, DR(-1.0), Sub(m, v) Bgsm(m).WetnessControlSpecMinvar = CSng(v)),
        Entrada("fWetnessControl_EnvMapScale", LectorJson.Float, DR(-1.0), Sub(m, v) Bgsm(m).WetnessControlEnvMapScale = CSng(v)),
        Entrada("fWetnessControl_FresnelPower", LectorJson.Float, DR(-1.0), Sub(m, v) Bgsm(m).WetnessControlFresnelPower = CSng(v)),
        Entrada("fWetnessControl_Metalness", LectorJson.Float, DR(-1.0), Sub(m, v) Bgsm(m).WetnessControlMetalness = CSng(v)),
        Entrada("sRootMaterialPath", LectorJson.Cadena, DS(""), Sub(m, v) Bgsm(m).RootMaterialPath = CStr(v)),
        Entrada("bAnisoLighting", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).AnisoLighting = CBool(v)),
        Entrada("bEmitEnabled", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).EmitEnabled = CBool(v)),
        Entrada("cEmittanceColor", LectorJson.Color, DS("#ffffff"), Sub(m, v) Bgsm(m).EmittanceColor = EmpaquetarRgb(CType(v, RgbDelMotor)),
          previo:=New RgbDelMotor(0.0F, 0.0F, 0.0F)),
        Entrada("fEmittanceMult", LectorJson.Float, DR(1.0), Sub(m, v) Bgsm(m).EmittanceMult = CSng(v)),
        Entrada("bModelSpaceNormals", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).ModelSpaceNormals = CBool(v)),
        Entrada("bExternalEmittance", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).ExternalEmittance = CBool(v)),
        Entrada("bBackLighting", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).BackLighting = CBool(v)),
        Entrada("bReceiveShadows", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).ReceiveShadows = CBool(v)),
        Entrada("bHideSecret", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).HideSecret = CBool(v)),
        Entrada("bCastShadows", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).CastShadows = CBool(v)),
        Entrada("bDissolveFade", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).DissolveFade = CBool(v)),
        Entrada("bAssumeShadowmask", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).AssumeShadowmask = CBool(v)),
        Entrada("bGlowmap", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).Glowmap = CBool(v)),
        Entrada("bEnvironmentMappingWindow", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).EnvironmentMappingWindow = CBool(v)),
        Entrada("bEnvironmentMappingEye", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).EnvironmentMappingEye = CBool(v)),
        Entrada("bHair", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).Hair = CBool(v)),
        Entrada("cHairTintColor", LectorJson.Color, DS("#808080"), Sub(m, v) Bgsm(m).HairTintColor = EmpaquetarRgb(CType(v, RgbDelMotor)),
          previo:=New RgbDelMotor(0.5F, 0.5F, 0.5F)),
        Entrada("bTree", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).Tree = CBool(v)),
        Entrada("bFacegen", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).Facegen = CBool(v)),
        Entrada("bSkinTint", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).SkinTint = CBool(v)),
        Entrada("bTessellate", LectorJson.Bool, DB(False), Sub(m, v) Bgsm(m).Tessellate = CBool(v)),
        Entrada("fDisplacementTextureBias", LectorJson.Float, DR(-0.5), Sub(m, v) Bgsm(m).DisplacementTextureBias = CSng(v)),
        Entrada("fDisplacementTextureScale", LectorJson.Float, DR(10.0), Sub(m, v) Bgsm(m).DisplacementTextureScale = CSng(v)),
        Entrada("fTessellationPNScale", LectorJson.Float, DR(1.0), Sub(m, v) Bgsm(m).TessellationPnScale = CSng(v)),
        Entrada("fTessellationBaseFactor", LectorJson.Float, DR(1.0), Sub(m, v) Bgsm(m).TessellationBaseFactor = CSng(v)),
        Entrada("fTessellationFadeDistance", LectorJson.Float, DR(0.0), Sub(m, v) Bgsm(m).TessellationFadeDistance = CSng(v)),
        Entrada("bTessellationNeedsDominantUVs", LectorJson.Bool, DB(False), Nothing),
        Entrada("bTessellationNeedsCrackFreeNormals", LectorJson.Bool, DB(False), Nothing),
        Entrada("fGrayscaleToPaletteScale", LectorJson.Float, SinDefecto, Sub(m, v) Bgsm(m).GrayscaleToPaletteScale = CSng(v))}

    ''' <summary>The filler's keys for one family, in its order (the common ones first). Friend: the gate compares the table with
    ''' re28\jsd_out.txt.</summary>
    Friend Function ClavesDelLlenador(esBgem As Boolean) As IEnumerable(Of ClaveDelLlenador)
        Return LlenadorComun.Concat(If(esBgem, LlenadorBgem, LlenadorBgsm))
    End Function

    ''' <summary>THE FILLER 0x14216EFF0 on a valid document: the table's keys in order over the ctor's BSShaderData, each value through
    ''' its converter (the first type a converter aborts on throws TipoIncorrectoDelMotor: the key the game closes on), then the
    ''' family's fixups. Json::Reader::readObject 0x1411D7920 resolves each member name on the object (0x1411DA990, find or insert)
    ''' and readValue 0x1411D9130 writes into it: a repeated key keeps its LAST value.</summary>
    Private Function LeerJson(bytes As Byte(), inicio As Integer, type As Type) As LecturaMaterial
        Dim fuente As New Dictionary(Of String, ValorJson)(StringComparer.Ordinal)
        Using documento = System.Text.Json.JsonDocument.Parse(New ReadOnlyMemory(Of Byte)(bytes, inicio, bytes.Length - inicio))
            If documento.RootElement.ValueKind <> System.Text.Json.JsonValueKind.Object Then Throw New System.Text.Json.JsonException("the document's root is not an object")
            For Each parJson In documento.RootElement.EnumerateObject()
                fuente(parJson.Name) = ValorDe(parJson.Value)
            Next
        End Using
        Dim esBgem = type Is GetType(BGEM)
        Dim m = MaterialDelCtorDelMotor(type)
        Dim indefinidos As New List(Of String)
        Dim pelo = 0.5F                                                   ' the hair tint's R as the struct holds it (+0x1C0; ctor 0.5)
        For Each c In ClavesDelLlenador(esBgem)
            If c.Clave = "cEmittanceColor" AndAlso Not Bgsm(m).EmitEnabled Then Continue For       ' 0x142170744: not fetched
            Dim clave = If(c.Alterna IsNot Nothing AndAlso Not fuente.ContainsKey(c.Clave), c.Alterna, c.Clave)
            Dim v As ValorJson
            If Not fuente.TryGetValue(clave, v) Then
                If Not c.Defecto.HasValue Then Continue For               ' isMember: absent, the struct keeps its value
                v = c.Defecto.Value
            End If
#If DEBUG Then
            ' Ola 7h tester round 4f, rebased by the author onto round 4g's filler (ShadowGate --ola7h-r4e V): a JSON null on a number /
            ' boolean key throws as the serializer did before round 4f.
            If v.Tipo = JsonNulo AndAlso (c.Lector = LectorJson.Bool OrElse c.Lector = LectorJson.Float OrElse c.Lector = LectorJson.ByteBajo) AndAlso
               Fo4GBufferConstants.GateMutantOn("p7h-r4f-null-throws") Then Throw New System.Runtime.Serialization.SerializationException("JSON null on '" & clave & "'")
#End If
            If c.Clave = "bGlowmap" AndAlso String.IsNullOrEmpty(Bgsm(m).GlowTexture) Then
                Bgsm(m).Glowmap = False                                   ' 0x142170AF7..B22: no asBool, the byte 0
                Continue For
            End If
            Dim x As Object
            Select Case c.Lector
                Case LectorJson.Bool : x = ComoBool(clave, v)
                Case LectorJson.Float : x = ComoFloat(clave, v)
                Case LectorJson.ByteBajo : x = ComoByteBajo(clave, v)
                Case LectorJson.Cadena : x = ComoCadena(clave, v)
                Case LectorJson.Textura : x = ComoTextura(clave, v)
                Case Else
                    Dim indefinido = False
                    Dim rgb = ColorDelMotor(ComoCadena(clave, v), c.Previo, indefinido)
                    If indefinido Then indefinidos.Add(clave)
                    If c.Clave = "cHairTintColor" Then pelo = rgb.R
                    x = rgb
            End Select
            c.Poner?.Invoke(m, x)
        Next
        If esBgem Then
            Dim bj = Bgem(m)
            ' BGEM fixups: bNonOccluder |= !bZBufferWrite (0x14216F900..F922); environment mapping on when the envmap path is not empty
            ' (0x14216F986..F9A5).
            If Not bj.ZBufferWrite Then bj.NonOccluder = True
            If Not String.IsNullOrEmpty(bj.EnvmapTexture) Then bj.EnvironmentMapping = True
        Else
            Dim bs = Bgsm(m)
            ' BGSM: an empty root is the wet template (0x142170622..69A); fGrayscaleToPaletteScale absent = the hair tint's R as the parser
            ' left it (0x142171270..12E1).
            If String.IsNullOrEmpty(bs.RootMaterialPath) Then bs.RootMaterialPath = "template\defaultTemplate_wet.bgsm"
            If Not fuente.ContainsKey("fGrayscaleToPaletteScale") Then bs.GrayscaleToPaletteScale = pelo
        End If
        Return New LecturaMaterial With {.Material = m, .BlendCrudo = Nothing, .EsJson = True,
                                         .Ilegible = If(indefinidos.Count = 0, Nothing,
                                                        $"JSON colour {String.Join(", ", indefinidos)} shorter than the 6 characters the engine's parser reads (0x142176CE0): past the string it reads undefined memory; those channels keep the ctor's values")}
    End Function

    ''' <summary>Ola 7h round 4g (user decision for WM, reviewer r7h-07): THE one producer of the warning a save gives when the material it
    ''' writes was read partial or with the BSShaderData ctor's values (<paramref name="ilegible"/>: an unreadable or 0-byte file, a JSON
    ''' that does not parse, an undefined colour channel) or from a JSON the engine aborts on (<paramref name="tipoIncorrecto"/>): the file
    ''' is written AS IT WAS READ - the engine's defaults for what the file did not give - and the user is told. Nothing for a full read.</summary>
    Public Function AvisoAlGuardar(ruta As String, ilegible As String, tipoIncorrecto As String) As String
        If ilegible Is Nothing AndAlso tipoIncorrecto Is Nothing Then Return Nothing
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-no-warning") Then Return Nothing
#End If
        Return $"Material {ruta} was unreadable; saved with the engine's defaults for the missing values"
    End Function

    ''' <summary>Writes <paramref name="material"/> in MaterialLib's binary layout (FO4UnifiedMaterial_Class.SerializarMaterial)
    ''' and, when the file came with a raw blend pair MaterialLib cannot represent and the mode was not edited (still
    ''' Unknown), puts that pair back at 0x20 / 0x21 / 0x25.</summary>
    Public Sub Escribir(material As BaseMaterialFile, blendCrudo As BlendCrudoArchivo, fs As Stream)
        If blendCrudo Is Nothing OrElse material.AlphaBlendMode <> AlphaBlendModeType.Unknown Then
            FO4UnifiedMaterial_Class.SerializarMaterial(material, fs)
            Return
        End If
        Dim bytes As Byte()
        Using ms As New MemoryStream()
            FO4UnifiedMaterial_Class.SerializarMaterial(material, ms)
            bytes = ms.ToArray()
        End Using
        blendCrudo.Bytes.CopyTo(bytes, OffsetBlend)
        fs.Write(bytes, 0, bytes.Length)
    End Sub
End Module
