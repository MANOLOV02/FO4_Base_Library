Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports MaterialLib
Imports MaterialLib.BaseMaterialFile

''' <summary>READS AND WRITES A .bgsm / .bgem AS THE ENGINE DOES, on top of MaterialLib (a third party that is not
''' edited). ONE place for every reader of material files (FO4UnifiedMaterial_Class.Deserialize, the Wardrobe Manager
''' clone), so the rules below cannot drift apart:
''' <list type="bullet">
''' <item><b>JSON payloads.</b> The engine reads a .bgem/.bgsm whose first byte is '{' as JSON (0x1417A94EC): the backslash
''' pre-pass (0x1417A9545..956D), jsoncpp's Json::Reader (<see cref="Jsoncpp.Parsear"/>, Fallout4.exe's dialect) and the filler
''' 0x14216EFF0, each key as get(key, default) or only when it is a member, converted with jsoncpp's law
''' (D:\WinTmp\rc\re30\re.md 1-6).</item>
''' <item><b>Raw blend pair.</b> The engine copies the file's blend byte and src/dst ints (offsets 0x20 / 0x21 / 0x25)
''' into the alpha property as they are (0x142171954..19CC); MaterialLib only knows five tuples and throws on any
''' other (BaseMaterialFile.cs:363-387). Any other tuple is kept RAW here: MaterialLib reads a copy carrying its
''' Unknown tuple (0,6,7), the raw pair travels next to the material, and writing puts it back.</item>
''' <item><b>Colours.</b> The engine holds a material's colours as three floats (+0x0C, +0x1C, +0x1C0); MaterialLib packs them in
''' 8-bit channels. The floats travel next to the material: as the engine holds them (<see cref="ColoresDelMotor"/>, the render
''' reads them) and as the file holds each of MaterialLib's colour fields (<see cref="LecturaMaterial.FlotantesDeColor"/>, the
''' writer puts them back, whatever the version).</item>
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

    ''' <summary>A colour as the engine holds it: three floats (NiColor at +0x0C, +0x1C, +0x1C0).</summary>
    Public Structure RgbDelMotor
        Public R As Single, G As Single, B As Single
        Public Sub New(r As Single, g As Single, b As Single)
            Me.R = r : Me.G = g : Me.B = b
        End Sub
    End Structure

    ''' <summary>The colours of a material file read as the engine reads them (floats, outside [0, 1] too: a JSON colour's digit is
    ''' not validated, 0x142176DA0, re30 4.2). Each one the BSShaderData field the reader leaves - the file's, the reader's default or
    ''' the ctor's (0x14216A4E0: +0x0C and +0x1C the BSS NiColor 0x1434496A0 = (0, 0, 0), never written, re30 4.3; +0x1C0
    ''' 0x142F4C620 = (0.5, 0.5, 0.5)). Nothing = an edit replaced it: the 8-bit channel of MaterialLib is then the value.</summary>
    Public NotInheritable Class ColoresDelMotor
        ''' <summary>BGSM cSpecularColor, +0x0C.</summary>
        Public Property Specular As RgbDelMotor?
        ''' <summary>BGSM cEmittanceColor, +0x1C (read only with bEmitEnabled; else the ctor's).</summary>
        Public Property Emittance As RgbDelMotor?
        ''' <summary>BGSM cHairTintColor, +0x1C0.</summary>
        Public Property HairTint As RgbDelMotor?
        ''' <summary>BGEM cBaseColor / uBaseColor, +0x1C.</summary>
        Public Property Base As RgbDelMotor?
        Public Function Copia() As ColoresDelMotor
            Return DirectCast(MemberwiseClone(), ColoresDelMotor)
        End Function
        ''' <summary>The ctor's colours (the BSShaderData a parse starts from).</summary>
        Public Shared Function DelCtor(type As Type) As ColoresDelMotor
            If type Is GetType(BGEM) Then Return New ColoresDelMotor With {.Base = New RgbDelMotor(0.0F, 0.0F, 0.0F)}
            Return New ColoresDelMotor With {.Specular = New RgbDelMotor(0.0F, 0.0F, 0.0F), .Emittance = New RgbDelMotor(0.0F, 0.0F, 0.0F),
                                             .HairTint = New RgbDelMotor(0.5F, 0.5F, 0.5F)}
        End Function
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
        ''' <summary>The JSON key whose value the engine's reader aborts on - jsoncpp's asBool / asFloat / asInt / asCString / find hit
        ''' _wassert + abort() (re30 5) -, or the reader's own abort (nesting 1000 deep, re30 2.3): Fallout 4 closes loading this
        ''' material. User decision 27-oct-2026: the shape is not drawn and the frame says so as an ERROR
        ''' (PreviewModel.RenderableMesh.ReportMaterialFileCrash). Nothing otherwise.</summary>
        Public Property TipoIncorrecto As String
        ''' <summary>What the key held (<see cref="TipoIncorrecto"/>'s value: "a string", "an array", "3000000000 out of Int range"...).</summary>
        Public Property TipoIncorrectoValor As String
        ''' <summary>The material's colours as the engine holds them (<see cref="ColoresDelMotor"/>).</summary>
        Public Property Colores As ColoresDelMotor
        ''' <summary>The file's floats of each of MaterialLib's colour fields (<see cref="CamposDeColor"/>), by property name: what
        ''' <see cref="Escribir"/> puts back where MaterialLib writes that field, any version. A binary file: the floats MaterialLib's
        ''' read took (<see cref="LeerBinario"/>); a JSON, a file MaterialLib refuses or nothing read: the engine's floats
        ''' (<see cref="Colores"/>). A field whose floats are not here is written with MaterialLib's 8-bit channels.</summary>
        Public Property FlotantesDeColor As New Dictionary(Of String, RgbDelMotor)(StringComparer.Ordinal)
        ''' <summary>The JSON texture paths longer than the 0x110-byte stack buffer of the engine's texture helper holds (0x142176C00:
        ''' 271 bytes and the NUL; no bound check, re30 4.1). User decision 27-oct-2026: read as they are, with a WARNING.</summary>
        Public Property TexturasDesbordadas As New List(Of String)
        ''' <summary>Ola 7h round 4g: the warning a save of this read gives (MaterialFileReader.AvisoAlGuardar), Nothing for a full read.</summary>
        Public Function AvisoAlGuardar(ruta As String) As String
            Return MaterialFileReader.AvisoAlGuardar(ruta, Ilegible, TipoIncorrecto)
        End Function
    End Class

    ''' <summary>A value the engine's reader aborts on (see LecturaMaterial.TipoIncorrecto): its key and what it held.</summary>
    Private NotInheritable Class TipoIncorrectoDelMotor
        Inherits Exception
        Friend ReadOnly Clave As String
        Friend ReadOnly Valor As String
        Friend Sub New(clave As String, valor As String)
            MyBase.New($"JSON material: '{clave}' is {valor}; the engine aborts on it.")
            Me.Clave = clave : Me.Valor = valor
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
        ' The engine's parse 0x1417A9420 reads ONE byte (0x1417A94C9) and dispatches on it (0x1417A94EC): '{' goes to the JSON reader
        ' 0x14216EF30, any other byte - whitespace and a UTF-8 BOM included - to the binary reader 0x14216BF20 (re30 1.1).
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

    ''' <summary>THE BACKSLASH PRE-PASS of 0x1417A9420 (re30 1.2): the payload copied into a buffer of its size + 1 (0x1417A9516), its
    ''' last byte 0 (0x1417A953E), and every '\' that is not followed by another '\' made '/' (0x1417A9545..956D, the next byte read
    ''' before any change: a run of N backslashes keeps N - 1 and ends in '/'). So "Effects\\x.dds" reads "Effects/x.dds", a lone
    ''' "Armor\Foo" reads "Armor/Foo", and \" \n \u.... never reach the parser as escapes.</summary>
    Friend Function PrepasadaDeBarras(bytes As Byte()) As Byte()
        Dim b(bytes.Length) As Byte
        Array.Copy(bytes, b, bytes.Length)
#If DEBUG Then
        ' Ola JSON tester: the pre-pass left out (the app's read before this wave).
        If Fo4GBufferConstants.GateMutantOn("oj-sin-prepasada") Then Return b
#End If
        For i = 0 To bytes.Length - 1
            Dim siguiente = If(i + 1 < bytes.Length, bytes(i + 1), CByte(0))
            If bytes(i) = &H5C AndAlso siguiente <> &H5C Then b(i) = &H2F
        Next
        Return b
    End Function

    ''' <summary>THE ENGINE'S JSON READER 0x14216EF30 (re30 1.3) over the pre-pass' buffer: the text is the buffer up to its first NUL,
    ''' cut at its LAST '}' (strrchr 0x14216EF70); without one nothing is parsed. Json::Reader::parse 0x1411D52F0
    ''' (<see cref="Jsoncpp.Parsear"/>, Fallout4.exe's dialect) rejects -&gt; the filler is not called (0x14216EF94): the BSShaderData
    ''' 0x1417A9420 made stays whole with the ctor 0x14216A4E0's values, and LoadMaterialFile still returns 0 - the caller applies them
    ''' (MaterialDelCtorDelMotor, the frame's "unreadable"). Nested 1000 deep: the reader aborts - the game closes. A document read
    ''' goes to the filler 0x14216EFF0 (<see cref="LeerJson"/>), where a value of a type its conversion aborts on closes the game too
    ''' (LecturaMaterial.TipoIncorrecto).</summary>
    Private Function LeerJsonComoElMotor(bytes As Byte(), inicio As Integer, type As Type) As LecturaMaterial
        Dim buf = PrepasadaDeBarras(bytes)
        Dim fin = Array.IndexOf(buf, CByte(0))
        Dim cierre = If(fin > 0, Array.LastIndexOf(buf, CByte(AscW("}"c)), fin - 1, fin), -1)
        If cierre < inicio Then
            Return LeerBinarioComoElMotorVacio(type, "JSON material without a closing '}': nothing parsed (0x14216EF70)")
        End If
        Dim raiz As Jsoncpp.JsonValor = Nothing
        Dim mensaje As String = Nothing
        Select Case Jsoncpp.Parsear(buf, inicio, cierre + 1, Jsoncpp.Dialecto.Fallout4Exe, raiz, mensaje)
            Case Jsoncpp.ResultadoDelParseo.Rechazado
                Return LeerBinarioComoElMotorVacio(type, "JSON material that does not parse: nothing filled (0x14216EF94): " & mensaje)
            Case Jsoncpp.ResultadoDelParseo.Aborta
                Dim l = LeerBinarioComoElMotorVacio(type, Nothing)
                l.TipoIncorrecto = "(the document)"
                l.TipoIncorrectoValor = "nested 1000 levels deep (" & mensaje & ")"
                Return l
        End Select
        Try
            Return LeerJson(raiz, type)
        Catch ex As TipoIncorrectoDelMotor
            Dim l = LeerBinarioComoElMotorVacio(type, Nothing)
            l.TipoIncorrecto = ex.Clave
            l.TipoIncorrectoValor = ex.Valor
            Return l
        End Try
    End Function

    ''' <summary>The BSShaderData ctor's values with nothing read (MaterialDelCtorDelMotor), as a read of the engine.</summary>
    Private Function LeerBinarioComoElMotorVacio(type As Type, motivo As String) As LecturaMaterial
        Dim colores = ColoresDelMotor.DelCtor(type)
        Return New LecturaMaterial With {.Material = MaterialDelCtorDelMotor(type), .BlendCrudo = Nothing, .EsJson = True, .Ilegible = motivo,
                                         .Colores = colores, .FlotantesDeColor = FlotantesDeLosColoresDelMotor(colores)}
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
        ''' <summary>A colour: 3 floats (0x142176690), as the engine holds them.</summary>
        Friend Function Rgb() As RgbDelMotor
            Dim r = F32(), g = F32(), bb = F32()
            Return New RgbDelMotor(r, g, bb)
        End Function
    End Class

    Private NotInheritable Class FinDelArchivo
        Inherits Exception
    End Class

    ''' <summary>A colour of 3 floats as MaterialLib stores it: (byte)(c * 255) per channel (BaseMaterialFile.Color.ToUInt32), C#'s unchecked
    ''' float-to-byte conversion on .NET 8 x64 - the product truncated to a 32-bit integer and its low byte; a product that is NaN or
    ''' outside the 32-bit range gives cvttss2si's 0x80000000, low byte 0 (measured, D:\WinTmp\rc\ola7h\r4\tmp\convu1: NaN, +-inf,
    ''' +-1e10 -> 0; -1 -> 1; -0.5 -> 129; 2.5 -> 125; 8.47 -> 111). Ola 7h round 4g: the checked CInt it had threw OverflowException on
    ''' such a product - an exception out of the engine's reader for a file whose colour floats are corrupt. MaterialLib's 8-bit
    ''' channel is what the editor shows; the engine's floats travel in <see cref="ColoresDelMotor"/>.</summary>
    Private Function EmpaquetarRgb(r As Single, g As Single, b As Single) As UInteger
        Dim c = Function(v As Single) As UInteger
                    Dim x = v * 255.0F
                    If Single.IsNaN(x) OrElse x < -2147483648.0F OrElse x >= 2147483648.0F Then Return 0UI
                    Return CUInt(CInt(Math.Truncate(x)) And &HFF)
                End Function
        Return (c(r) << 16) Or (c(g) << 8) Or c(b)
    End Function

    ''' <summary>A colour as MaterialLib stores it (EmpaquetarRgb: its packing of a file's three floats).</summary>
    Private Function EmpaquetarRgb(c As RgbDelMotor) As UInteger
        Return EmpaquetarRgb(c.R, c.G, c.B)
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
        Dim colores = ColoresDelMotor.DelCtor(type)
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
                Dim baseRgb = c.Rgb() : e.BaseColor = EmpaquetarRgb(baseRgb) : colores.Base = baseRgb : e.BaseColorScale = c.F32()   ' +0x1C, +0x28
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
                s.SpecularEnabled = c.B()                                                             ' +0x8
                Dim spec = c.Rgb() : s.SpecularColor = EmpaquetarRgb(spec) : colores.Specular = spec : s.SpecularMult = c.F32()      ' +0xC, +0x18
                s.Smoothness = c.F32() : s.FresnelPower = c.F32()                                     ' +0x34, +0x38
                s.WetnessControlSpecScale = c.F32() : s.WetnessControlSpecPowerScale = c.F32() : s.WetnessControlSpecMinvar = c.F32()
                s.WetnessControlEnvMapScale = c.F32() : s.WetnessControlFresnelPower = c.F32() : s.WetnessControlMetalness = c.F32()   ' +0x3C..0x53
                s.RootMaterialPath = c.Str()                                  ' +0x58 (0x1421768B0)
                s.AnisoLighting = c.B() : s.EmitEnabled = c.B()               ' +0xB8, +0x6C
                If s.EmitEnabled Then                                         ' +0x1C, only with +0x6C (0x14216D1FF..208)
                    Dim emit = c.Rgb() : s.EmittanceColor = EmpaquetarRgb(emit) : colores.Emittance = emit
                End If
                s.EmittanceMult = c.F32()                                     ' +0x64
                s.ModelSpaceNormals = c.B() : s.ExternalEmittance = c.B() : s.BackLighting = c.B()    ' +0x90, +0x92, +0xA7
                s.ReceiveShadows = c.B() : s.HideSecret = c.B() : s.CastShadows = c.B() : s.DissolveFade = c.B()   ' +0x9F, +0x93, +0x9E, +0xA0
                s.AssumeShadowmask = c.B() : s.Glowmap = c.B()                ' +0xA2, +0xA1
                s.EnvironmentMappingWindow = c.B() : s.EnvironmentMappingEye = c.B()                  ' +0x17C, +0x17B
                s.Hair = c.B()                                                ' +0x98
                Dim pelo = c.Rgb() : s.HairTintColor = EmpaquetarRgb(pelo) : colores.HairTint = pelo   ' +0x1C0
                s.Tree = c.B() : s.Facegen = c.B() : s.SkinTint = c.B() : s.Tessellate = c.B()        ' +0x9A, +0x96, +0x97, +0xA6
                s.DisplacementTextureBias = c.F32() : s.DisplacementTextureScale = c.F32() : s.TessellationPnScale = c.F32()
                s.TessellationBaseFactor = c.F32() : s.TessellationFadeDistance = c.F32()             ' +0x164, 0x168, 0x16C, 0x174, 0x170
                s.GrayscaleToPaletteScale = c.F32()                                                   ' +0x68
                If version >= 1UI Then s.SkewSpecularAlpha = c.B()            ' +0xA5 (0x14216D5EA..5EE)
            End If
        Catch ex As FinDelArchivo
            ' The bytes ended: the fields not reached keep the ctor's values.
        End Try
        Return New LecturaMaterial With {.Material = m, .BlendCrudo = crudo, .EsJson = False, .Ilegible = If(motivo, ""), .Colores = colores,
                                         .FlotantesDeColor = FlotantesDeLosColoresDelMotor(colores)}
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
        Dim leidos As List(Of Single)
        Using ms As New MemoryStream(lectura)
            Using reader As New LectorQueAnota(ms)
                mat.Deserialize(reader)
                leidos = reader.Leidos
            End Using
        End Using
        ' Ola 7h round 4g (r7h-07): the emittance colour +0x1C is in the file only with bEmitEnabled +0x6C (the engine's reader
        ' 0x14216D1FF..208; MaterialLib's BGSM.cs:405-409 alike). Without it the engine keeps the BSShaderData ctor's value
        ' (MaterialDelCtorDelMotor), not MaterialLib's constructor default 0xFFFFFFFF: the one field a full read and the engine's read
        ' disagreed on, so a material saved as it was read (partial, or with the engine's defaults) read back differently.
        Dim s = TryCast(mat, BGSM)
        If s IsNot Nothing AndAlso Not s.EmitEnabled Then s.EmittanceColor = DirectCast(MaterialDelCtorDelMotor(GetType(BGSM)), BGSM).EmittanceColor
        ' The colours as the engine holds them: the file's floats where its binary reader takes them (the same walk).
        ' The file's floats of each of MaterialLib's colour fields: Deserialize reads its floats in the order Serialize writes them (the
        ' paired Color.Read / Color.Write of CamposDeColor), so the floats of field k of the write are the reads k, k+1, k+2. Kept only
        ' when they pack to the channels MaterialLib set from them (Color.ToUInt32, BaseMaterialFile.cs:431-440 = EmpaquetarRgb): the
        ' read and the write walked the same layout.
        Dim flotantes As New Dictionary(Of String, RgbDelMotor)(StringComparer.Ordinal)
        For Each kv In IndicesDeColor(mat).Indices
            Dim k = kv.Value
            If k + 2 >= leidos.Count Then Continue For
            Dim rgb As New RgbDelMotor(leidos(k), leidos(k + 1), leidos(k + 2))
            If EmpaquetarRgb(rgb) = (CampoDeColor(mat, kv.Key) And &HFFFFFFUI) Then flotantes(kv.Key) = rgb
        Next
        Return New LecturaMaterial With {.Material = mat, .BlendCrudo = crudo, .EsJson = False,
                                         .Colores = LeerBinarioComoElMotor(bytes, type, Nothing).Colores, .FlotantesDeColor = flotantes}
    End Function

    ''' <summary>MaterialLib's colour fields, the closed list of its Color.Read / Color.Write calls: BGSM TranslucencySubsurfaceColor
    ''' (BGSM.cs:359 / :528, version &gt;= 8), SpecularColor (:374 / :543), EmittanceColor (:409 / :578, with EmitEnabled), HairTintColor
    ''' (:449 / :618); BGEM GlassFresnelColor (BGEM.cs:203 / :288, version &gt;= 21 with GlassEnabled), BaseColor (:228 / :313),
    ''' EmittanceColor (:242 / :327, version &gt;= 11).</summary>
    Friend Function CamposDeColor(type As Type) As String()
        If type Is GetType(BGEM) Then Return {"GlassFresnelColor", "BaseColor", "EmittanceColor"}
        Return {"TranslucencySubsurfaceColor", "SpecularColor", "EmittanceColor", "HairTintColor"}
    End Function

    Private Function CampoDeColor(m As BaseMaterialFile, nombre As String) As UInteger
        Return CUInt(m.GetType().GetProperty(nombre).GetValue(m))
    End Function

    Private Sub PonerCampoDeColor(m As BaseMaterialFile, nombre As String, v As UInteger)
        m.GetType().GetProperty(nombre).SetValue(m, v)
    End Sub

    ''' <summary>The engine's colours as the floats of MaterialLib's fields they are (+0x0C SpecularColor, +0x1C EmittanceColor of a BGSM
    ''' and BaseColor of a BGEM, +0x1C0 HairTintColor).</summary>
    Private Function FlotantesDeLosColoresDelMotor(c As ColoresDelMotor) As Dictionary(Of String, RgbDelMotor)
        Dim r As New Dictionary(Of String, RgbDelMotor)(StringComparer.Ordinal)
        If c Is Nothing Then Return r
        If c.Specular.HasValue Then r("SpecularColor") = c.Specular.Value
        If c.Emittance.HasValue Then r("EmittanceColor") = c.Emittance.Value
        If c.HairTint.HasValue Then r("HairTintColor") = c.HairTint.Value
        If c.Base.HasValue Then r("BaseColor") = c.Base.Value
        Return r
    End Function

    ''' <summary>MaterialLib's read, noting every float it reads (Color.Read is three ReadSingle, BaseMaterialFile.cs:484-489).</summary>
    Private NotInheritable Class LectorQueAnota
        Inherits BinaryReader
        Friend ReadOnly Leidos As New List(Of Single)
        Friend Sub New(s As Stream)
            MyBase.New(s)
        End Sub
        Public Overrides Function ReadSingle() As Single
            Dim v = MyBase.ReadSingle()
            Leidos.Add(v)
            Return v
        End Function
    End Class

    ''' <summary>MaterialLib's write, noting where each float goes (Color.Write is three Write(Single), BaseMaterialFile.cs:492-497). The
    ''' encoding is FO4UnifiedMaterial_Class.SerializarMaterial's: the same bytes.</summary>
    Private NotInheritable Class EscritorQueAnota
        Inherits BinaryWriter
        Friend ReadOnly Escritos As New List(Of (Posicion As Integer, Bits As Integer))
        Friend Sub New(s As Stream)
            MyBase.New(s, FO4UnifiedMaterial_Class.CodificacionDelEscritor, leaveOpen:=True)
        End Sub
        Public Overrides Sub Write(value As Single)
            Escritos.Add((CInt(OutStream.Position), BitConverter.SingleToInt32Bits(value)))
            MyBase.Write(value)
        End Sub
    End Class

    Private Function SerializarAnotando(material As BaseMaterialFile) As (Bytes As Byte(), Escritos As List(Of (Posicion As Integer, Bits As Integer)))
        Using ms As New MemoryStream()
            Using w As New EscritorQueAnota(ms)
                material.Serialize(w)
                w.Flush()
                Return (ms.ToArray(), w.Escritos)
            End Using
        End Using
    End Function

    ''' <summary>WHERE MATERIALLIB WRITES EACH COLOUR FIELD of <paramref name="material"/>, any version: its Serialize once as it is, and
    ''' once per field with that field's three channels changed (each byte xor 0x80: every channel's float changes): the floats that
    ''' change are that field's Color.Write - three consecutive writes, 4 bytes apart -, none when the version or a flag leaves the field
    ''' out. Indices = the field's first float in the write order (the read's order too, LeerBinario); Bytes / Escritos = the plain
    ''' write. The material is left as it came.</summary>
    Private Function IndicesDeColor(material As BaseMaterialFile) As (Indices As Dictionary(Of String, Integer), Bytes As Byte(), Escritos As List(Of (Posicion As Integer, Bits As Integer)))
        Dim base = SerializarAnotando(material)
        Dim r As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
        For Each nombre In CamposDeColor(material.GetType())
            Dim v = CampoDeColor(material, nombre)
            Dim otra As (Bytes As Byte(), Escritos As List(Of (Posicion As Integer, Bits As Integer)))
            PonerCampoDeColor(material, nombre, v Xor &H808080UI)
            Try
                otra = SerializarAnotando(material)
            Finally
                PonerCampoDeColor(material, nombre, v)
            End Try
            If otra.Escritos.Count <> base.Escritos.Count Then Throw New InvalidOperationException($"MaterialLib wrote {nombre}'s change with another layout")
            Dim cambian As New List(Of Integer)
            For i = 0 To base.Escritos.Count - 1
                If base.Escritos(i).Posicion <> otra.Escritos(i).Posicion Then Throw New InvalidOperationException($"MaterialLib wrote {nombre}'s change with another layout")
                If base.Escritos(i).Bits <> otra.Escritos(i).Bits Then cambian.Add(i)
            Next
            If cambian.Count = 0 Then Continue For
            Dim k = cambian(0)
            If cambian.Count <> 3 OrElse cambian(1) <> k + 1 OrElse cambian(2) <> k + 2 OrElse
               base.Escritos(k + 1).Posicion <> base.Escritos(k).Posicion + 4 OrElse base.Escritos(k + 2).Posicion <> base.Escritos(k).Posicion + 8 Then
                Throw New InvalidOperationException($"MaterialLib wrote {nombre} other than as one Color.Write")
            End If
            r(nombre) = k
        Next
        Return (r, base.Bytes, base.Escritos)
    End Function

    ' THE ENGINE'S JSON FILLER 0x14216EFF0 (re30 3): ONE table, in the filler's order, of the keys it reads - each with the converter the
    ' filler calls on its value and the default root.get 0x1411D0E10 passes for an absent key (the 92 get calls, the 7 isMember, the 14
    ' texture helper calls and the 4 colour parses of re30 3). A key outside the table is not read by the engine (eParallax* and
    ' bEnvironmentMappingLightFade are fetched and never converted; the *Enable flags, Glass*, translucency, MaskWrites are not
    ' fetched): the material carries the value the game uses, not the one the file wrote. The fill starts from the BSShaderData ctor's
    ' values (MaterialDelCtorDelMotor: the struct 0x1417A9420 makes before the parse), so a key the filler only reads through isMember
    ' keeps them when absent. The conversions are jsoncpp's (Jsoncpp, Fallout4.exe's dialect): one law with the presets' loaders.

    ''' <summary>The abort of a jsoncpp converter (_wassert + abort(): the game closes, re30 5), naming the key and what it held: the
    ''' type for a type no conversion takes, the value for a number out of the converter's range.</summary>
    Private Function Aborta(clave As String, v As Jsoncpp.JsonValor) As TipoIncorrectoDelMotor
        Dim valor As String
        Select Case v.Tipo
            Case Jsoncpp.JsonTipo.Entero, Jsoncpp.JsonTipo.SinSigno, Jsoncpp.JsonTipo.Real
                Dim ok As Boolean
                valor = $"{Jsoncpp.AsString(v, ok)}, out of Int range"
            Case Else
                valor = v.NombreDelTipo
        End Select
        Return New TipoIncorrectoDelMotor(clave, valor)
    End Function

    ''' <summary>The converter the engine's JSON filler calls on a key's value (re30 3, the call after each root.get / isMember).</summary>
    Friend Enum LectorJson
        ''' <summary>Json::Value::asBool 0x1411CC6B0.</summary>
        Bool
        ''' <summary>Json::Value::asFloat 0x1411CCAA0.</summary>
        Float
        ''' <summary>Json::Value::asInt 0x1411CCF00, its low byte stored (fAlphaTestRef: mov byte +0x8C 0x14216F3CB; iEnvmapMinLOD: +0x30
        ''' 0x14216FEFA).</summary>
        ByteBajo
        ''' <summary>Json::Value::asCString 0x1411CC810 taken as a C string (eAlphaBlendMode, sRootMaterialPath).</summary>
        Cadena
        ''' <summary>The texture helper 0x142176C00 (isMember, asCString, the path cleanup).</summary>
        Textura
        ''' <summary>asCString and the colour parser 0x142176CE0.</summary>
        Color
    End Enum

    ''' <summary>The texture helper 0x142176C00 on a present key (re30 4.1): asCString's C string, then ONE leading '\' or '/' stripped
    ''' (0x142176C54..C62) and each doubled backslash collapsed (0x142176C70..C9A) into the 0x110-byte stack buffer; "" is the empty
    ''' slot (0x142924FED). <paramref name="desborda"/> = the copy and its NUL do not fit the buffer (no bound check: the engine's
    ''' stack is overwritten).</summary>
    Private Function TexturaDelMotor(b As Byte(), ByRef desborda As Boolean) As String
        Dim s = Jsoncpp.CortarEnNul(b)
        If s.Length = 0 Then desborda = False : Return ""
        Dim i = If(s(0) = &H5C OrElse s(0) = &H2F, 1, 0)
        Dim o As New List(Of Byte)(s.Length)
        While i < s.Length
            Dim c = s(i)
            If c = &H5C AndAlso i + 1 < s.Length AndAlso s(i + 1) = &H5C Then i += 2 Else i += 1
            o.Add(c)
        End While
        desborda = o.Count + 1 > &H110
        Return Jsoncpp.TextoDe(o.ToArray())
    End Function

    ''' <summary>1/255 as the colour parser multiplies by it (0x142176DF9: the float 0x3B808081 at 0x1429293FC).</summary>
    Private ReadOnly UnoSobre255 As Single = BitConverter.Int32BitsToSingle(&H3B808081)

    ''' <summary>One digit as 0x142176DA0 computes it: the byte plus 0xC9 when it is 'A'..'F' (byte - 0x41 &lt;= 5 unsigned), plus 0xA9
    ''' when 'a'..'f' (byte - 0x61 &lt;= 5), plus 0xD0 otherwise - in 8 bits, no validation - then read signed (movsx).</summary>
    Private Function DigitoDelMotor(c As Byte) As Integer
        Dim suma = If(((c + &HBF) And &HFF) <= 5, &HC9, If(((c + &H9F) And &HFF) <= 5, &HA9, &HD0))
        Dim r = (c + suma) And &HFF
        Return If(r >= &H80, r - &H100, r)
    End Function

    ''' <summary>THE ENGINE'S COLOUR PARSER 0x142176CE0 on the bytes asCString gives (re30 4.2): one leading '#' skipped (0x142176CEA..CFB);
    ''' "fff" (_stricmp 0x142176D02, ASCII case, as a C string) is (1, 1, 1) and "000" (0x142176D36) (0, 0, 0) - the constants at
    ''' 0x142F4C610 / 0x1434496A0 (BSS, never written); otherwise three channels of two bytes each through DigitoDelMotor, (hi * 16 + lo)
    ''' as a float times 1/255. The parser reads six bytes whatever the string's length: the string's own bytes (past an embedded NUL
    ''' too), its NUL as a digit, a byte beyond that is the heap after the string - undefined. HUECO, declared: such a channel keeps
    ''' <paramref name="previo"/> (the struct's value before the write: the ctor's), and <paramref name="indefinido"/> says so (the
    ''' read's Ilegible, the frame's "unreadable").</summary>
    Private Function ColorDelMotor(b As Byte(), previo As RgbDelMotor, ByRef indefinido As Boolean) As RgbDelMotor
        Dim p = If(b.Length > 0 AndAlso b(0) = &H23, 1, 0)
        Dim cuerpo = b.Skip(p).ToArray()
        Dim c = Jsoncpp.CortarEnNul(cuerpo)
        Dim es = Function(t As String) c.Length = t.Length AndAlso Enumerable.Range(0, c.Length).All(Function(i) (If(c(i) >= &H41 AndAlso c(i) <= &H5A, c(i) + &H20, CInt(c(i)))) = AscW(t(i)))
        If es("fff") Then Return New RgbDelMotor(1.0F, 1.0F, 1.0F)
        If es("000") Then Return New RgbDelMotor(0.0F, 0.0F, 0.0F)
        Dim n = cuerpo.Length
        Dim antes = {previo.R, previo.G, previo.B}
        Dim v(2) As Single
        For k = 0 To 2
            If 2 * k + 1 > n Then
                v(k) = antes(k)
                indefinido = True
                Continue For
            End If
            Dim hi = DigitoDelMotor(If(2 * k < n, cuerpo(2 * k), CByte(0)))
            Dim lo = DigitoDelMotor(If(2 * k + 1 < n, cuerpo(2 * k + 1), CByte(0)))
            v(k) = CSng(hi * 16 + lo) * UnoSobre255
        Next
        Return New RgbDelMotor(v(0), v(1), v(2))
    End Function

    ''' <summary>One key of the filler: its name; the one the filler reads instead when it is not a member (BGEM's greyscale texture
    ''' 0x14216F940, base colour 0x14216FBE4); the converter; the default root.get passes for an absent key (Nothing: the filler reads it
    ''' only through isMember, and absent the struct keeps its value); the struct's colour before the write (Color only, for an undefined
    ''' channel); and the MaterialLib field it fills (Nothing: a member MaterialLib does not have - the value is still converted, so a
    ''' type the engine aborts on still aborts).</summary>
    Friend NotInheritable Class ClaveDelLlenador
        Friend ReadOnly Clave As String
        Friend ReadOnly Alterna As String
        Friend ReadOnly Lector As LectorJson
        Friend ReadOnly Defecto As Jsoncpp.JsonValor
        Friend ReadOnly Previo As RgbDelMotor
        Friend ReadOnly Poner As Action(Of BaseMaterialFile, Object)
        Friend Sub New(clave As String, lector As LectorJson, defecto As Jsoncpp.JsonValor, poner As Action(Of BaseMaterialFile, Object), alterna As String, previo As RgbDelMotor)
            Me.Clave = clave : Me.Lector = lector : Me.Defecto = defecto : Me.Poner = poner : Me.Alterna = alterna : Me.Previo = previo
        End Sub
        ''' <summary>The default as the gate compares it with re28\jsd_out.txt: "bool 1", "double 0.3...", "cstr #ffffff", "int 128", or "".</summary>
        Friend Function DefectoComoTexto() As String
            If Defecto Is Nothing Then Return ""
            Dim d = Defecto
            Select Case d.Tipo
                Case Jsoncpp.JsonTipo.Booleano : Return If(d.Booleano, "bool 1", "bool 0")
                Case Jsoncpp.JsonTipo.Real : Return "double " & d.Real.ToString("R", Globalization.CultureInfo.InvariantCulture)
                Case Jsoncpp.JsonTipo.Texto : Return "cstr " & Jsoncpp.TextoDe(d.Bytes)
                Case Else : Return "int " & d.Entero.ToString(Globalization.CultureInfo.InvariantCulture)
            End Select
        End Function
    End Class

    Private Function Entrada(clave As String, lector As LectorJson, defecto As Jsoncpp.JsonValor, poner As Action(Of BaseMaterialFile, Object),
                       Optional alterna As String = Nothing, Optional previo As RgbDelMotor = Nothing) As ClaveDelLlenador
        Return New ClaveDelLlenador(clave, lector, defecto, poner, alterna, previo)
    End Function
    ''' <summary>Value(bool) 0x1411C8630.</summary>
    Private Function DB(b As Boolean) As Jsoncpp.JsonValor
        Return Jsoncpp.JsonValor.DeBool(b)
    End Function
    ''' <summary>Value(double) 0x1411C8420.</summary>
    Private Function DR(d As Double) As Jsoncpp.JsonValor
        Return Jsoncpp.JsonValor.DeDouble(d)
    End Function
    ''' <summary>Value(const char*) 0x1411C8490.</summary>
    Private Function DS(s As String) As Jsoncpp.JsonValor
        Return Jsoncpp.JsonValor.DeTexto(s)
    End Function
    ''' <summary>Value(Int) 0x1411C83C0 (type 1, intValue: 0x1411C83C2).</summary>
    Private Function DI(i As Integer) As Jsoncpp.JsonValor
        Return Jsoncpp.JsonValor.DeInt(i)
    End Function
    Private ReadOnly SinDefecto As Jsoncpp.JsonValor = Nothing
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
        Entrada("fAlphaTestRef", LectorJson.ByteBajo, DI(128), Sub(m, v) m.AlphaTestRef = CByte(v)),
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
                                                               ' +0xA5. BGEM: MaterialLib has no field and nothing reads it for an
                                                               ' effect: ApplyMaterialData 0x14216AF90 copies it to the property's flag
                                                               ' bit 54 (0x14216B323 -&gt; SetFlag 0x142169460), whose only reader is
                                                               ' BSLightingShaderProperty vfunc +0x158 0x14217A050 (re30 9.2). Converted
                                                               ' (a wrong type still aborts), not kept.
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
        Entrada("iEnvmapMinLOD", LectorJson.ByteBajo, DI(0), Sub(m, v) Bgem(m).EnvmapMinLOD = CByte(v)),
        Entrada("fSoftDepth", LectorJson.Float, DR(0.0), Sub(m, v) Bgem(m).SoftDepth = CSng(v))}

    ''' <summary>BGSM (the filler's flag 8, 0x14216FF56): its nine textures in the helper's order (0x14216FF73..170042), then
    ''' 0x14217006D..1712AC. The emittance colour is read only with bEmitEnabled (0x142170744) and bGlowmap converted only with the glow
    ''' slot set (0x142170AF7..B22): LeerJson's loop. bTessellationNeedsDominantUVs / bTessellationNeedsCrackFreeNormals (+0x178 / +0x179)
    ''' are converted (a wrong type aborts) and not kept: MaterialLib has no field and nothing applies them - ApplyMaterialData 0x1421715E0
    ''' and its callees (three levels, the walk of re30 9.2) read nothing of the BSShaderData in +0x164..+0x17F. HUECO, declared: a
    ''' reader outside that walk is not traced.</summary>
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

    ''' <summary>One conversion of the filler: jsoncpp's (Jsoncpp, Fallout4.exe's dialect), the abort thrown as TipoIncorrectoDelMotor.</summary>
    Private Function Convertir(c As ClaveDelLlenador, clave As String, v As Jsoncpp.JsonValor, ByRef desborda As Boolean, ByRef indefinido As Boolean) As Object
        Dim ok As Boolean
#If DEBUG Then
        ' Ola 7h tester round 4g (ShadowGate --ola7h-r4e V): jsoncpp's conversions are not modelled - a number aborts asBool / a real or a
        ' bool aborts asInt / a bool aborts asFloat.
        If Fo4GBufferConstants.GateMutantOn("p7h-r4g-no-conversions") Then
            Dim numero = v.Tipo = Jsoncpp.JsonTipo.Entero OrElse v.Tipo = Jsoncpp.JsonTipo.SinSigno OrElse v.Tipo = Jsoncpp.JsonTipo.Real
            If (c.Lector = LectorJson.Bool AndAlso numero) OrElse (c.Lector = LectorJson.Float AndAlso v.Tipo = Jsoncpp.JsonTipo.Booleano) OrElse
               (c.Lector = LectorJson.ByteBajo AndAlso (v.Tipo = Jsoncpp.JsonTipo.Real OrElse v.Tipo = Jsoncpp.JsonTipo.Booleano)) Then Throw Aborta(clave, v)
        End If
#End If
        Select Case c.Lector
            Case LectorJson.Bool
                Dim r = Jsoncpp.AsBool(v, ok) : If Not ok Then Throw Aborta(clave, v)
                Return r
            Case LectorJson.Float
                Dim r = Jsoncpp.AsFloat(v, Jsoncpp.Dialecto.Fallout4Exe, ok) : If Not ok Then Throw Aborta(clave, v)
                Return r
            Case LectorJson.ByteBajo
                Dim r = Jsoncpp.AsInt(v, ok) : If Not ok Then Throw Aborta(clave, v)
                Return CByte(r And &HFF)
            Case LectorJson.Cadena
                Dim r = Jsoncpp.AsCString(v, ok) : If Not ok Then Throw Aborta(clave, v)
                Return r
            Case LectorJson.Textura
                Dim b = Jsoncpp.AsCStringBytes(v, ok) : If Not ok Then Throw Aborta(clave, v)
                Return TexturaDelMotor(b, desborda)
            Case Else
                Dim b = Jsoncpp.AsCStringBytes(v, ok) : If Not ok Then Throw Aborta(clave, v)
                Return ColorDelMotor(b, c.Previo, indefinido)
        End Select
    End Function

    ''' <summary>THE FILLER 0x14216EFF0 on a document read: the table's keys in order over the ctor's BSShaderData, each value through
    ''' its converter (the first value a converter aborts on throws TipoIncorrectoDelMotor: the key the game closes on), then the
    ''' family's fixups. The root is always an object (the first byte is '{', re30 1.3), so find never fails on it.</summary>
    Private Function LeerJson(raiz As Jsoncpp.JsonValor, type As Type) As LecturaMaterial
        Dim esBgem = type Is GetType(BGEM)
        Dim m = MaterialDelCtorDelMotor(type)
        Dim colores = ColoresDelMotor.DelCtor(type)
        Dim indefinidos As New List(Of String)
        Dim desbordadas As New List(Of String)
        Dim ok As Boolean
        For Each c In ClavesDelLlenador(esBgem)
            If c.Clave = "cEmittanceColor" AndAlso Not Bgsm(m).EmitEnabled Then Continue For       ' 0x142170744: not fetched, +0x1C = (0, 0, 0)
            Dim clave = If(c.Alterna IsNot Nothing AndAlso Not Jsoncpp.EsMiembro(raiz, c.Clave, ok), c.Alterna, c.Clave)
            Dim v As Jsoncpp.JsonValor
            If c.Defecto Is Nothing Then
                v = Jsoncpp.Buscar(raiz, clave, ok)                       ' isMember: absent, the struct keeps its value
                If v Is Nothing Then Continue For
            Else
                v = Jsoncpp.Obtener(raiz, clave, c.Defecto, ok)           ' get(key, default) 0x1411D0E10
            End If
#If DEBUG Then
            ' Ola 7h tester round 4f, rebased by the author onto round 4g's filler (ShadowGate --ola7h-r4e V): a JSON null on a number /
            ' boolean key throws as the serializer did before round 4f.
            If v.Tipo = Jsoncpp.JsonTipo.Nulo AndAlso (c.Lector = LectorJson.Bool OrElse c.Lector = LectorJson.Float OrElse c.Lector = LectorJson.ByteBajo) AndAlso
               Fo4GBufferConstants.GateMutantOn("p7h-r4f-null-throws") Then Throw New System.Runtime.Serialization.SerializationException("JSON null on '" & clave & "'")
#End If
            If c.Clave = "bGlowmap" AndAlso String.IsNullOrEmpty(Bgsm(m).GlowTexture) Then
                Bgsm(m).Glowmap = False                                   ' 0x142170AF7..B22: no asBool, the byte 0
                Continue For
            End If
            Dim desborda = False, indefinido = False
            Dim x = Convertir(c, clave, v, desborda, indefinido)
            If desborda Then desbordadas.Add(CStr(x))
            If indefinido Then indefinidos.Add(clave)
            If c.Lector = LectorJson.Color Then
                Dim rgb = CType(x, RgbDelMotor)
                Select Case c.Clave
                    Case "cSpecularColor" : colores.Specular = rgb
                    Case "cEmittanceColor" : colores.Emittance = rgb
                    Case "cHairTintColor" : colores.HairTint = rgb
                    Case "cBaseColor" : colores.Base = rgb
                End Select
            End If
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
            If Not Jsoncpp.EsMiembro(raiz, "fGrayscaleToPaletteScale", ok) Then bs.GrayscaleToPaletteScale = colores.HairTint.Value.R
        End If
        Return New LecturaMaterial With {.Material = m, .BlendCrudo = Nothing, .EsJson = True, .Colores = colores, .TexturasDesbordadas = desbordadas,
                                         .FlotantesDeColor = FlotantesDeLosColoresDelMotor(colores),
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

    ''' <summary>Writes <paramref name="material"/> in MaterialLib's binary layout (FO4UnifiedMaterial_Class.SerializarMaterial's bytes), then
    ''' puts back what MaterialLib cannot hold: the file's raw blend pair at 0x20 / 0x21 / 0x25 (when it came with one and the mode was
    ''' not edited, still Unknown) and the file's colour floats (<paramref name="flotantes"/>, LecturaMaterial.FlotantesDeColor: each one
    ''' an edit did not replace) where MaterialLib writes that field (IndicesDeColor), whatever the version - MaterialLib packs a colour in
    ''' 8-bit channels, so without this a material read and saved lost its colours' precision and anything outside [0, 1]. A material read
    ''' from a JSON (<paramref name="rutasDelJson"/> = LecturaMaterial.EsJson) has its texture paths written with '\'
    ''' (<see cref="NormalizarRutasDeTextura"/>, user decision 27-oct-2026); a binary one keeps the separators its file had.</summary>
    Public Sub Escribir(material As BaseMaterialFile, blendCrudo As BlendCrudoArchivo, fs As Stream, Optional flotantes As IReadOnlyDictionary(Of String, RgbDelMotor) = Nothing,
                        Optional rutasDelJson As Boolean = False)
        Dim normalizar = rutasDelJson
#If DEBUG Then
        ' Ola JSON tester (revisor oj-03): a binary material's paths normalized too (12218 binary files of the corpus would change bytes).
        If Fo4GBufferConstants.GateMutantOn("oj-escritor-normaliza-binario") Then normalizar = True
#End If
        Dim cambiadas = If(normalizar, NormalizarRutasDeTextura(material), New List(Of (Propiedad As Reflection.PropertyInfo, Original As String)))
        Try
            EscribirNormalizado(material, blendCrudo, fs, flotantes)
        Finally
            For Each c In cambiadas
                c.Propiedad.SetValue(material, c.Original)
            Next
        End Try
    End Sub

    ''' <summary>MaterialLib's texture path fields, the closed list of its WriteString calls less RootMaterialPath (a material path):
    ''' BGSM.cs:494-518 (Diffuse, Normal, SmoothSpec, Greyscale, Glow, Wrinkles, Specular, Lighting, Flow, DistanceFieldAlpha, Envmap,
    ''' InnerLayer, Displacement), BGEM.cs:267-283 (Base, Grayscale, Envmap, Normal, EnvmapMask, Specular, Lighting, Glow,
    ''' GlassRoughnessScratch, GlassDirtOverlay).</summary>
    Friend Function RutasDeTextura(type As Type) As String()
        If type Is GetType(BGEM) Then Return {"BaseTexture", "GrayscaleTexture", "EnvmapTexture", "NormalTexture", "EnvmapMaskTexture", "SpecularTexture",
                                             "LightingTexture", "GlowTexture", "GlassRoughnessScratch", "GlassDirtOverlay"}
        Return {"DiffuseTexture", "NormalTexture", "SmoothSpecTexture", "GreyscaleTexture", "GlowTexture", "WrinklesTexture", "SpecularTexture",
                "LightingTexture", "FlowTexture", "DistanceFieldAlphaTexture", "EnvmapTexture", "InnerLayerTexture", "DisplacementTexture"}
    End Function

    ''' <summary>THE STRINGS OF A JSON MATERIAL THAT THE PRE-PASS ALTERS AND THE WRITER WRITES: every string field of the filler (re30 3:
    ''' the 14 texture paths, LectorJson.Textura, and sRootMaterialPath, LectorJson.Cadena - eAlphaBlendMode, the filler's only other
    ''' string, becomes the blend tuple, not a written string) that MaterialLib writes with WriteString: <see cref="RutasDeTextura"/> and,
    ''' for a BGSM, RootMaterialPath (BGSM.cs:571). The pre-pass turns their backslashes into '/' ("template\\x.bgsm" reads
    ''' "template/x.bgsm"); one rule for all of them (<see cref="NormalizarRutasDeTextura"/>).</summary>
    Friend Function CadenasDeLaPrepasada(type As Type) As String()
        Dim r = RutasDeTextura(type)
#If DEBUG Then
        ' Ola JSON tester (revisor oj-05): RootMaterialPath left out (written with the pre-pass's '/').
        If Fo4GBufferConstants.GateMutantOn("oj-escritor-sin-raiz") Then Return r
#End If
        If type Is GetType(BGSM) Then Return r.Concat({"RootMaterialPath"}).ToArray()
        Return r
    End Function

    ''' <summary>THE SEPARATOR OF A WRITTEN TEXTURE PATH OF A MATERIAL READ FROM A JSON: '\' (user decision 27-oct-2026). A convention of the
    ''' app's writer, NOT a law of the engine: the engine reads a JSON's paths with '/' (its pre-pass, 0x1417A9545..956D: every backslash of
    ''' the file's text becomes '/') and its texture loading takes either separator. With it, a JSON read and saved gives the bytes HEAD
    ''' gave (the corpus's 4 JSON materials, med\escritura). Only for a JSON: the binary corpus is not uniform - 12218 of its 27807
    ''' materials hold '/' in a texture path (med\escritor) - and a binary file is written back as it was. Applied here, in the writer, the
    ''' one place a material file is written. The same rule for RootMaterialPath, the other string the pre-pass alters and the writer
    ''' writes (<see cref="CadenasDeLaPrepasada"/>, revisor oj-05). Each such field holding a '/' has it replaced for the write; the caller's material
    ''' keeps its values (the fields are put back after it). Returns the fields changed with their values.</summary>
    Private Function NormalizarRutasDeTextura(material As BaseMaterialFile) As List(Of (Propiedad As Reflection.PropertyInfo, Original As String))
        Dim r As New List(Of (Propiedad As Reflection.PropertyInfo, Original As String))
#If DEBUG Then
        ' Ola JSON tester (revisor oj-03): the paths written as they are in memory.
        If Fo4GBufferConstants.GateMutantOn("oj-escritor-sin-normalizar") Then Return r
#End If
        For Each nombre In CadenasDeLaPrepasada(material.GetType())
            Dim p = material.GetType().GetProperty(nombre)
            Dim v = TryCast(p.GetValue(material), String)
            If v Is Nothing OrElse v.IndexOf("/"c) < 0 Then Continue For
            r.Add((p, v))
            p.SetValue(material, v.Replace("/"c, "\"c))
        Next
        Return r
    End Function

    Private Sub EscribirNormalizado(material As BaseMaterialFile, blendCrudo As BlendCrudoArchivo, fs As Stream, flotantes As IReadOnlyDictionary(Of String, RgbDelMotor))
        Dim bytes As Byte()
        If flotantes Is Nothing OrElse flotantes.Count = 0 Then
            bytes = SerializarAnotando(material).Bytes
        Else
            Dim donde = IndicesDeColor(material)
            bytes = donde.Bytes
            Dim indices = donde.Indices
#If DEBUG Then
            ' Ola JSON tester: the writer keeps MaterialLib's 8-bit channels (before this wave).
            If Fo4GBufferConstants.GateMutantOn("oj-escritor-empaqueta") Then indices = New Dictionary(Of String, Integer)()
#End If
            For Each kv In indices
                Dim rgb As RgbDelMotor
                If Not flotantes.TryGetValue(kv.Key, rgb) Then Continue For
                Dim p = donde.Escritos(kv.Value).Posicion
                BitConverter.GetBytes(rgb.R).CopyTo(bytes, p)
                BitConverter.GetBytes(rgb.G).CopyTo(bytes, p + 4)
                BitConverter.GetBytes(rgb.B).CopyTo(bytes, p + 8)
            Next
        End If
        If blendCrudo IsNot Nothing AndAlso material.AlphaBlendMode = AlphaBlendModeType.Unknown Then blendCrudo.Bytes.CopyTo(bytes, OffsetBlend)
        fs.Write(bytes, 0, bytes.Length)
    End Sub
End Module
