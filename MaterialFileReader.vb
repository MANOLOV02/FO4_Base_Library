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
    End Class

    ''' <summary>The five (blend byte, src, dst) MaterialLib converts (BaseMaterialFile.ConvertAlphaBlendMode).</summary>
    Private ReadOnly TuplasMaterialLib As (Byte, UInteger, UInteger)() = {(0, 6, 7), (0, 0, 0), (1, 6, 7), (1, 6, 0), (1, 4, 1)}

    Private Const OffsetBlend As Integer = &H20
    Private Const OffsetSrc As Integer = &H21
    Private Const OffsetDst As Integer = &H25

    Public Function Leer(bytes As Byte(), type As Type) As LecturaMaterial
        If type IsNot GetType(BGSM) AndAlso type IsNot GetType(BGEM) Then Throw New ArgumentException("Unsupported material type: " & type?.Name)
        Dim i = 0
        While i < bytes.Length AndAlso (bytes(i) = 32 OrElse bytes(i) = 9 OrElse bytes(i) = 13 OrElse bytes(i) = 10)
            i += 1
        End While
        If i < bytes.Length AndAlso bytes(i) = AscW("{"c) Then Return LeerJson(bytes, i, type)
        Return LeerBinario(bytes, type)
    End Function

    Private Function LeerBinario(bytes As Byte(), type As Type) As LecturaMaterial
        Dim lectura = bytes
        Dim crudo As BlendCrudoArchivo = Nothing
        If bytes.Length >= OffsetDst + 4 Then
            Dim b0 = bytes(OffsetBlend)
            Dim src = BitConverter.ToUInt32(bytes, OffsetSrc), dst = BitConverter.ToUInt32(bytes, OffsetDst)
            If Not TuplasMaterialLib.Contains((b0, src, dst)) Then
                crudo = New BlendCrudoArchivo(New FO4UnifiedMaterial_Class.MaterialFileBlend With {
                    .Byte0 = b0 <> 0, .Src = CType(CInt(src), NiflySharp.Enums.AlphaFunction), .Dst = CType(CInt(dst), NiflySharp.Enums.AlphaFunction)},
                    bytes.Skip(OffsetBlend).Take(9).ToArray())
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
        Return New LecturaMaterial With {.Material = mat, .BlendCrudo = crudo, .EsJson = False}
    End Function

    ' THE KEYS THE ENGINE'S JSON READER READS (FO4 0x14216EFF0; closed list from the whole disassembly,
    ' Tools/re-docs/RE_SSE_ZPREPASS_BGSM_JSON_2026-10-03.md 4.2-4.3, RE_EFFECT_PARTICLE_ENVCUBE_2026-10-03.md 7.1-7.3), as
    ' engine name -> MaterialLib DataMember name. A key outside this list is NOT read by the engine (eParallax*,
    ' bEnvironmentMappingLightFade, the *Enable flags, Glass*, translucency, MaskWrites...): it is dropped, so the material
    ' carries the value the game uses, not the one the file wrote.
    Private ReadOnly ClavesComunes As String() = {
        "bTileU", "bTileV", "fUOffset", "fVOffset", "fUScale", "fVScale", "fAlpha", "fAlphaTestRef", "bAlphaTest",
        "bZBufferWrite", "bZBufferTest", "bScreenSpaceReflections", "bWetnessControl_ScreenSpaceReflections", "bDecal",
        "bTwoSided", "bDecalNoFade", "bNonOccluder", "bSkewSpecularAlpha", "bRefraction", "fRefractionPower",
        "bEnvironmentMapping", "fEnvironmentMappingMaskScale", "bGrayscaleToPaletteColor"}
    Private ReadOnly RenombresComunes As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
        {"bRefractionFalloff", "fRefractionFalloff"}}
    Private ReadOnly ClavesBgem As String() = {
        "bBloodEnabled", "bEffectLightingEnabled", "bFalloffEnabled", "bFalloffColorEnabled", "bGrayscaleToPaletteAlpha",
        "bSoftEnabled", "fBaseColorScale", "fFalloffStartAngle", "fFalloffStopAngle", "fFalloffStartOpacity",
        "fFalloffStopOpacity", "fLightingInfluence", "iEnvmapMinLOD", "fSoftDepth",
        "sBaseTexture", "sEnvmapTexture", "sNormalTexture", "sEnvmapMaskTexture"}
    Private ReadOnly ClavesBgsm As String() = {
        "sDiffuseTexture", "sNormalTexture", "sSmoothSpecTexture", "sGreyscaleTexture", "sEnvmapTexture", "sGlowTexture",
        "sInnerLayerTexture", "sWrinklesTexture", "sDisplacementTexture", "bEnableEditorAlphaRef", "bRimLighting", "fRimPower",
        "fBackLightPower", "bSubsurfaceLighting", "fSubsurfaceLightingRolloff", "bSpecularEnabled", "cSpecularColor",
        "fSpecularMult", "fSmoothness", "fFresnelPower", "fWetnessControl_SpecScale", "fWetnessControl_SpecPowerScale",
        "fWetnessControl_EnvMapScale", "fWetnessControl_FresnelPower", "fWetnessControl_Metalness", "sRootMaterialPath",
        "bAnisoLighting", "bEmitEnabled", "cEmittanceColor", "fEmittanceMult", "bModelSpaceNormals", "bExternalEmittance",
        "bBackLighting", "bReceiveShadows", "bHideSecret", "bCastShadows", "bDissolveFade", "bAssumeShadowmask", "bGlowmap",
        "bEnvironmentMappingWindow", "bEnvironmentMappingEye", "bHair", "cHairTintColor", "bTree", "bFacegen", "bSkinTint",
        "bTessellate", "fDisplacementTextureBias", "fDisplacementTextureScale", "fTessellationBaseFactor",
        "fTessellationFadeDistance", "bTessellationNeedsDominantUVs", "bTessellationNeedsCrackFreeNormals",
        "fGrayscaleToPaletteScale"}
    Private ReadOnly RenombresBgsm As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
        {"fWetnessControl_SpecMin", "fWetnessControl_SpecMinvar"}, {"fTessellationPNScale", "fTessellationPnScale"}}
    ' Absent keys whose reader literal differs from MaterialLib's SetDefaults (MaterialLib name -> literal).
    Private ReadOnly LiteralesBgem As (String, Double)() = {
        ("fFalloffStartAngle", 0.0), ("fFalloffStopAngle", 0.0), ("fLightingInfluence", 0.0), ("fSoftDepth", 0.0)}

    ''' <summary>A JSON value the engine reads with Json::Value::asCString (FO4 0x1411CC810): any type other than
    ''' stringValue - number, bool, array, object, and a key present with null - hits _wassert("requires stringValue",
    ''' jsoncpp.cpp:2987) and abort() (0x1411CC82E..CC8F2): the game process ends, there is no value to model. The file
    ''' is invalid for the engine, so it is rejected here as invalid data
    ''' (Tools/re-docs/RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 3).</summary>
    Private Function CadenaDelMotor(nodo As System.Text.Json.Nodes.JsonNode, clave As String) As String
        If nodo Is Nothing OrElse nodo.GetValueKind() <> System.Text.Json.JsonValueKind.String Then
            Throw New InvalidDataException($"JSON material: '{clave}' is not a string ({If(nodo Is Nothing, "null", nodo.GetValueKind().ToString())}); the engine aborts on it (Json::Value::asCString).")
        End If
        Return nodo.GetValue(Of String)()
    End Function

    Private Function LeerJson(bytes As Byte(), inicio As Integer, type As Type) As LecturaMaterial
        Dim fuente = System.Text.Json.Nodes.JsonNode.Parse(New MemoryStream(bytes, inicio, bytes.Length - inicio)).AsObject()
        Dim esBgem = type Is GetType(BGEM)
        Dim doc As New System.Text.Json.Nodes.JsonObject()
        Dim copiar = Sub(desde As String, hacia As String)
                         If fuente.ContainsKey(desde) Then doc(hacia) = fuente(desde)?.DeepClone()
                     End Sub
        For Each k In ClavesComunes.Concat(If(esBgem, ClavesBgem, ClavesBgsm))
            copiar(k, k)
        Next
        For Each r In RenombresComunes.Concat(If(esBgem, Enumerable.Empty(Of KeyValuePair(Of String, String))(), RenombresBgsm))
            copiar(r.Key, r.Value)
        Next
        If esBgem Then
            ' Greyscale: "sGreyscaleTexture" (0x142901C68) if present, else "sGrayscaleTexture" (0x142901C80); MaterialLib's
            ' BGEM key is the second. cBaseColor, else uBaseColor, through the same colour parser (0x142176CE0).
            If fuente.ContainsKey("sGreyscaleTexture") Then copiar("sGreyscaleTexture", "sGrayscaleTexture") Else copiar("sGrayscaleTexture", "sGrayscaleTexture")
            If fuente.ContainsKey("cBaseColor") Then copiar("cBaseColor", "cBaseColor") Else copiar("uBaseColor", "cBaseColor")
            For Each lit In LiteralesBgem
                If Not doc.ContainsKey(lit.Item1) Then doc(lit.Item1) = lit.Item2
            Next
        Else
            ' Absent keys whose reader literal differs from MaterialLib's SetDefaults: bSpecularEnabled true, fSpecularMult 80.
            If Not doc.ContainsKey("bSpecularEnabled") Then doc("bSpecularEnabled") = True
            If Not doc.ContainsKey("fSpecularMult") Then doc("fSpecularMult") = 80.0
        End If
        ' Texture paths (helper 0x142176C00): an absent key leaves the slot alone; a present one goes through asCString
        ' (a non-string value aborts the game, see CadenaDelMotor); "" sets the slot to "". Then ONE leading '\' or '/' is
        ' stripped and a doubled backslash collapsed (0x142176C54..C9A).
        For Each k In doc.Select(Function(p) p.Key).Where(Function(n) n.StartsWith("s", StringComparison.Ordinal) AndAlso n.EndsWith("Texture", StringComparison.Ordinal)).ToList()
            Dim v = CadenaDelMotor(doc(k), k)
            If v.StartsWith("\", StringComparison.Ordinal) OrElse v.StartsWith("/", StringComparison.Ordinal) Then v = v.Substring(1)
            doc(k) = v.Replace("\\", "\")
        Next
        Dim mat As BaseMaterialFile
        Using msJson As New MemoryStream(System.Text.Encoding.UTF8.GetBytes(doc.ToJsonString()))
            mat = CType(New System.Runtime.Serialization.Json.DataContractJsonSerializer(type).ReadObject(msJson), BaseMaterialFile)
        End Using
        ' eAlphaBlendMode, same code for both readers (0x14216F29F..F36F): blend = string not empty and not "None"
        ' (case-insensitive); src/dst Standard 6/7, Additive 6/0, Multiplicative 4/1, any other string keeps the
        ' constructor's 6/7. In MaterialLib's enum: (0,6,7) Unknown, (1,6,7) Standard, (1,6,0) Additive, (1,4,1) Multiplicative.
        ' Absent: json.get's default "None" (0x1411D0E10 returns the default only for a missing key); present: asCString.
        Dim modo = If(fuente.ContainsKey("eAlphaBlendMode"), CadenaDelMotor(fuente("eAlphaBlendMode"), "eAlphaBlendMode"), "None")
        If String.IsNullOrEmpty(modo) OrElse String.Equals(modo, "None", StringComparison.OrdinalIgnoreCase) Then
            mat.AlphaBlendMode = AlphaBlendModeType.Unknown
        ElseIf String.Equals(modo, "Additive", StringComparison.OrdinalIgnoreCase) Then
            mat.AlphaBlendMode = AlphaBlendModeType.Additive
        ElseIf String.Equals(modo, "Multiplicative", StringComparison.OrdinalIgnoreCase) Then
            mat.AlphaBlendMode = AlphaBlendModeType.Multiplicative
        Else
            mat.AlphaBlendMode = AlphaBlendModeType.Standard
        End If
        If esBgem Then
            Dim bj = DirectCast(mat, BGEM)
            ' BGEM fixups: bNonOccluder |= !bZBufferWrite (0x14216F900..F922); environment mapping on when the envmap path is
            ' not empty (0x14216F986..F9A5).
            If Not bj.ZBufferWrite Then bj.NonOccluder = True
            If Not String.IsNullOrEmpty(bj.EnvmapTexture) Then bj.EnvironmentMapping = True
        Else
            Dim bs = DirectCast(mat, BGSM)
            ' BGSM: an empty root is the wet template (0x142170622..69A); the emittance colour is read only with bEmitEnabled,
            ' else black (0x142170744..7BC); bGlowmap is read only when the glow slot is not empty (0x142170AF7..B2B);
            ' fGrayscaleToPaletteScale absent = HairTintColor.R (0x142171270..12E1).
            If String.IsNullOrEmpty(bs.RootMaterialPath) Then bs.RootMaterialPath = "template\defaultTemplate_wet.bgsm"
            If Not bs.EmitEnabled Then bs.EmittanceColor = 0UI
            If String.IsNullOrEmpty(bs.GlowTexture) Then bs.Glowmap = False
            If Not fuente.ContainsKey("fGrayscaleToPaletteScale") Then bs.GrayscaleToPaletteScale = ((bs.HairTintColor >> 16) And &HFFUI) / 255.0F
        End If
        Return New LecturaMaterial With {.Material = mat, .BlendCrudo = Nothing, .EsJson = True}
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
