Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading
Imports OpenTK.Mathematics
Imports FO4_Base_Library.Canon

''' <summary>WHICH (weather, moment) OF THE GAME THE PREVIEW SHOWS, and the two app options next to it. Persisted
''' per game (Config_App.Setting_PreviewImaging_FO4/SSE, JSON keys Setting_PreviewImaging2_FO4/SSE).
''' <para>Identity of the weather = its ORIGIN plugin + object id (the 24 low bits; the vanilla masters are full
''' plugins) + the moment name as the WTHR declares it (xEdit: Sunrise, Day, Sunset, Night; FO4 also Early/Late
''' Sunrise/Sunset). Same in every load order.</para>
''' <para>⛔ STRUCTURE: a field added later arrives False/0/Nothing from an existing config.json. Adding a field
''' = RENAME the config key (see Setting_ShadowMaps16_*).</para></summary>
Public Structure PreviewImagingSettings
    Public Property WeatherPlugin As String
    Public Property WeatherObjectId As UInteger
    Public Property Moment As String
    ''' <summary>True: the frame goes through the game's post (PostProcess.vb). False: the fragments apply the
    ''' 2.3.8 display law (LegacyDisplaySource), which is not the engine.</summary>
    Public Property ApplyPostProcess As Boolean
    ''' <summary>Share of the key light an effect shader with the LIGHTING technique receives, relative to the GAME's
    ''' share: 1 = the weather's effect light keeps, against the key, the proportion it has against the sun in the
    ''' game (<see cref="PreviewImagingRow.EffectLightForKey"/>). An app choice, not the engine.</summary>
    Public Property EffectLightVsKey As Single

    Public Shared Function Defaults(isSse As Boolean) As PreviewImagingSettings
        Dim k = DefaultKey(isSse)
        Return New PreviewImagingSettings With {.WeatherPlugin = k.Plugin, .WeatherObjectId = k.ObjectId, .Moment = k.Moment,
                                                .ApplyPostProcess = True, .EffectLightVsKey = 1.0F}
    End Function

    ''' <summary>The preview's canonical choice: WTHR CommonwealthClear (Fallout4.esm 0x02B52A, since 2.4.0) /
    ''' SkyrimClearSN_A (Skyrim.esm 0x10E1F0, user choice 3-oct-2026), day. Its row is the literals of PostProcess.vb. Lives here and not in
    ''' the table module so that building a Config_App does not build the table.</summary>
    Friend Shared Function DefaultKey(isSse As Boolean) As PreviewImagingKey
        Return If(isSse, New PreviewImagingKey("Skyrim.esm", &H10E1F0UI, "Day"),
                         New PreviewImagingKey("Fallout4.esm", &H2B52AUI, "Day"))
    End Function

    ''' <summary>The same settings showing another weather and moment (the post and effect-light options stay).</summary>
    Friend Function WithWeather(key As PreviewImagingKey) As PreviewImagingSettings
        Dim s = Me
        s.WeatherPlugin = key.Plugin : s.WeatherObjectId = key.ObjectId : s.Moment = key.Moment
        Return s
    End Function
End Structure

''' <summary>(origin plugin, object id, moment). Plugin compared ignoring case, like every plugin name.</summary>
Friend Structure PreviewImagingKey
    Implements IEquatable(Of PreviewImagingKey)
    Public ReadOnly Plugin As String
    Public ReadOnly ObjectId As UInteger
    Public ReadOnly Moment As String

    Public Sub New(plugin As String, objectId As UInteger, moment As String)
        Me.Plugin = If(plugin, "") : Me.ObjectId = objectId : Me.Moment = If(moment, "")
    End Sub

    ''' <summary>The same weather at another moment.</summary>
    Public Function AtMoment(m As String) As PreviewImagingKey
        Return New PreviewImagingKey(Plugin, ObjectId, m)
    End Function

    Public Overloads Function Equals(o As PreviewImagingKey) As Boolean Implements IEquatable(Of PreviewImagingKey).Equals
        Return ObjectId = o.ObjectId AndAlso String.Equals(Plugin, o.Plugin, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(Moment, o.Moment, StringComparison.Ordinal)
    End Function
    Public Overrides Function Equals(obj As Object) As Boolean
        Return TypeOf obj Is PreviewImagingKey AndAlso Equals(DirectCast(obj, PreviewImagingKey))
    End Function
    Public Overrides Function GetHashCode() As Integer
        Return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(If(Plugin, "")), ObjectId, If(Moment, ""))
    End Function
End Structure

''' <summary>ONE ROW: a (weather, moment) with what the preview takes from it - the effect light and the image
''' space of the post. The FO4 pair is set for FO4 rows, the SSE pair for SSE rows; the other pair is Nothing.</summary>
Friend NotInheritable Class PreviewImagingRow
    Public ReadOnly Key As PreviewImagingKey
    ''' <summary>The game the row belongs to: the ONE owner of that fact (EffectLight and the frame read it).</summary>
    Public ReadOnly IsSse As Boolean
    Public ReadOnly WeatherEdid As String
    ''' <summary>Plugin of the WTHR record whose values the row carries (the winner it was read from).</summary>
    Public ReadOnly WeatherSource As String
    Public ReadOnly ImageSpacePlugin As String
    Public ReadOnly ImageSpaceObjectId As UInteger
    Public ReadOnly ImageSpaceEdid As String
    Public ReadOnly ImageSpaceSource As String
    Public ReadOnly Fo4ImageSpace As Fo4ImageSpace
    Public ReadOnly Fo4Weather As Fo4PreviewWeather
    Public ReadOnly SseImageSpace As SseImageSpace
    Public ReadOnly SseWeather As SsePreviewWeather
    ''' <summary>True: the values were read from the load order by <see cref="PreviewImagingTable.RefreshFromLoadOrder"/>;
    ''' False: they are the generated game table (vanilla + DLC).</summary>
    Public ReadOnly FromLoadOrder As Boolean

    Friend Sub New(key As PreviewImagingKey, isSse As Boolean, weatherEdid As String, weatherSource As String,
                   imageSpacePlugin As String, imageSpaceObjectId As UInteger, imageSpaceEdid As String, imageSpaceSource As String,
                   fo4ImageSpace As Fo4ImageSpace, fo4Weather As Fo4PreviewWeather,
                   sseImageSpace As SseImageSpace, sseWeather As SsePreviewWeather,
                   Optional fromLoadOrder As Boolean = False)
        Me.Key = key : Me.IsSse = isSse : Me.WeatherEdid = weatherEdid : Me.WeatherSource = weatherSource
        Me.ImageSpacePlugin = imageSpacePlugin : Me.ImageSpaceObjectId = imageSpaceObjectId
        Me.ImageSpaceEdid = imageSpaceEdid : Me.ImageSpaceSource = imageSpaceSource
        Me.Fo4ImageSpace = fo4ImageSpace : Me.Fo4Weather = fo4Weather
        Me.SseImageSpace = sseImageSpace : Me.SseWeather = sseWeather
        Me.FromLoadOrder = fromLoadOrder
    End Sub

    ''' <summary>The effect shader's LIGHTING light under this row (FO4 linear DLightColor, SSE raw cb2[7]).</summary>
    Public Function EffectLight() As Vector3
        Return If(IsSse, SseWeather.EffectLight, Fo4Weather.DLightColor(Fo4ImageSpace))
    End Function

    ''' <summary>The sun's light under this row (FO4 linear, SSE raw): the directional the game's lit shaders get.</summary>
    Public Function SunLight() As Vector3
        Return If(IsSse, SseWeather.SunLightColor, Fo4Weather.SunLightColor(Fo4ImageSpace))
    End Function

    ''' <summary>THE LIGHT THE PREVIEW GIVES AN EFFECT SHADER WITH LIGHTING. In the game the lit materials and the effects
    ''' are lit by the same weather: the sun and the Effect Lighting of the same moment. In the preview the lit materials
    ''' take the rig, whose key stands for the sun; so the effect keeps, against the key, the proportion the weather gives
    ''' it against the sun: EffectLight * Luma(key) / Luma(sun) * <paramref name="share"/>. Colour from the weather,
    ''' level from the key. No direction (the effect PS has no N.L: FO4 b05 i1385, SSE idx 730;
    ''' Tools/re-docs/RE_BGEM_POINT_LIGHTS_FO4/SSE_2026-10-03.md). SunlightScale is in both terms and cancels.
    ''' A moment whose sun is black (<see cref="EffectFollowsKey"/> False: 88 vanilla rows, 33 of them with Effect
    ''' Lighting &gt; 0 - FX window weathers, Blackreach, MQ203...) has no proportion: the effect takes the weather's
    ''' light as the engine does, <paramref name="share"/> does not apply.
    ''' <paramref name="keyDiffuse"/> = the key as the lit shaders receive it (FO4 linear, SSE raw).</summary>
    Public Function EffectLightForKey(keyDiffuse As Vector3, share As Single) As Vector3
        If Not EffectFollowsKey Then Return EffectLight()
        Return EffectLight() * (Shader_Base_Class.Luma(keyDiffuse) / Shader_Base_Class.Luma(SunLight()) * share)
    End Function

    ''' <summary>True when the moment has a sun to keep the proportion against (its light's luma &gt; 0): the ONE owner
    ''' of that predicate (EffectLightForKey and the "Effects vs key" control read it).</summary>
    Public ReadOnly Property EffectFollowsKey As Boolean
        Get
            Return Shader_Base_Class.Luma(SunLight()) > 0.0F
        End Get
    End Property

    ''' <summary>The same row marked as read from the load order.</summary>
    Friend Function MarkedFromLoadOrder() As PreviewImagingRow
        Return New PreviewImagingRow(Key, IsSse, WeatherEdid, WeatherSource, ImageSpacePlugin, ImageSpaceObjectId,
                                     ImageSpaceEdid, ImageSpaceSource, Fo4ImageSpace, Fo4Weather, SseImageSpace, SseWeather,
                                     fromLoadOrder:=True)
    End Function
End Class

''' <summary>THE TABLE OF (weather, moment) THE PREVIEW CAN SHOW, per game.
''' <para>Starts as PreviewImagingTable.Generated.vb (Tools\PreviewImagingTableGen: the vanilla masters + DLC,
''' every moment of the game) so that the apps that read no plugin (Wardrobe Manager) have it; a host that loads a
''' load order calls <see cref="RefreshFromLoadOrder"/> and the rows that exist take the winning records' values.
''' No row is ever added from a load order (user decision).</para>
''' <para>Engine law of a row: Tools/re-docs/RE_IMSP_NULO_E_IMGS_ENAM_2026-10-02.md (Fallout4.exe 1.11.240.0,
''' SkyrimSE.exe 1.7.104.0).</para></summary>
Friend Module PreviewImagingTable

    ''' <summary>The moments a WTHR can declare, in the order of the day (UI order only).</summary>
    Friend ReadOnly MomentOrder As String() = {"Early Sunrise", "Sunrise", "Late Sunrise", "Day",
                                               "Early Sunset", "Sunset", "Late Sunset", "Night"}

    ''' <summary>The moments of a WTHR by engine index, named as xEdit's wbWeatherTimeOfDay
    ''' (wbDefinitionsCommon.pas:8359-8378; the index law is the engine's, the names are xEdit's). SSE has the
    ''' first four.</summary>
    Friend ReadOnly MomentByIndex As String() = {"Sunrise", "Day", "Sunset", "Night",
                                                 "Early Sunrise", "Late Sunrise", "Early Sunset", "Late Sunset"}

    ''' <summary>DefaultImageSpace: the IMGS the engine takes for a moment whose image space is null (getter SSE
    ''' 0x140419c30, FO4 0x1406c4f20: null slot -> LookupByID(0x167)). Same FormID in Fallout4.esm and Skyrim.esm.</summary>
    Private Const DefaultImageSpaceFormId As UInteger = &H167UI

    Private NotInheritable Class Snapshot
        Public ReadOnly Rows As PreviewImagingRow()
        Public ReadOnly Index As Dictionary(Of PreviewImagingKey, PreviewImagingRow)
        Public Sub New(rows As PreviewImagingRow())
            Me.Rows = rows
            Index = New Dictionary(Of PreviewImagingKey, PreviewImagingRow)(rows.Length)
            For Each r In rows : Index(r.Key) = r : Next
        End Sub
    End Class

    ' The generated table is built on first use, not when the module or Config_App initializes.
    Private ReadOnly _baseFo4 As New Lazy(Of PreviewImagingRow())(AddressOf PreviewImagingTableData.Fo4Rows)
    Private ReadOnly _baseSse As New Lazy(Of PreviewImagingRow())(AddressOf PreviewImagingTableData.SseRows)
    Private _fo4 As Snapshot
    Private _sse As Snapshot
    Private _lastMissing As PreviewImagingKey

    Private Function Current(isSse As Boolean) As Snapshot
        Dim s = If(isSse, Volatile.Read(_sse), Volatile.Read(_fo4))
        If s IsNot Nothing Then Return s
        Dim fresh As New Snapshot(If(isSse, _baseSse.Value, _baseFo4.Value))
        If isSse Then
            Interlocked.CompareExchange(_sse, fresh, Nothing)
            Return Volatile.Read(_sse)
        End If
        Interlocked.CompareExchange(_fo4, fresh, Nothing)
        Return Volatile.Read(_fo4)
    End Function

    Private Sub SetSnapshot(isSse As Boolean, snap As Snapshot)
        If isSse Then
            Volatile.Write(_sse, snap)
        Else
            Volatile.Write(_fo4, snap)
        End If
    End Sub

    Friend Function Rows(isSse As Boolean) As IReadOnlyList(Of PreviewImagingRow)
        Return Current(isSse).Rows
    End Function

    Friend Function Find(isSse As Boolean, key As PreviewImagingKey) As PreviewImagingRow
        Dim r As PreviewImagingRow = Nothing
        Current(isSse).Index.TryGetValue(key, r)
        Return r
    End Function

    ''' <summary>The row of the persisted choice; a choice the table does not have shows the default row (the
    ''' imaging-table gate guarantees it exists) and is logged once.</summary>
    Friend Function Resolve(isSse As Boolean, s As PreviewImagingSettings) As PreviewImagingRow
        Dim key = New PreviewImagingKey(s.WeatherPlugin, s.WeatherObjectId, s.Moment)
        Dim r = Find(isSse, key)
        If r Is Nothing Then
            If Not key.Equals(_lastMissing) Then
                _lastMissing = key
                Logger.LogLazy(Function() $"[IMAGING] {key.Plugin} {key.ObjectId:X6} {key.Moment} is not in the table; showing the default.")
            End If
            r = Find(isSse, PreviewImagingSettings.DefaultKey(isSse))
        End If
        Debug.Assert(r Is Nothing OrElse r.IsSse = isSse, "PreviewImagingTable: row of the other game")
        Return r
    End Function

    ''' <summary>THE LAW OF A ROW (LEY 3'), the same for the generator and the runtime refresh. One row per (winning
    ''' WTHR, moment of the game: FO4 8, SSE 4).
    ''' <list type="bullet">
    ''' <item>Image space of the moment: the IMSP FormID; 0, a missing form or a form that is not an IMGS =&gt; 0x167
    ''' DefaultImageSpace (InitItem stores null when LookupByID or the RTTI cast fails; the getter turns null into
    ''' 0x167: SSE 0x140419c30, FO4 0x1406c4f20).</item>
    ''' <item>FO4 by SUBRECORD SIZE (TESWeather::Load): NAM0 &lt; 0x220 =&gt; moments 4..7 = (a + b) &gt;&gt; 1 per
    ''' byte of (3,0), (0,1), (1,2), (2,3) (0x14056d454, 0x14056f190); IMSP &lt;&gt; 0x20 =&gt; slots 4,5 &lt;- 0 and
    ''' 6,7 &lt;- 2 (0x14056e400 cmp ebx,0x20 / je). The tree is built by FORM VERSION (wbFromVersion 111): where size
    ''' and version disagree the WTHR gives no row (never a misaligned read).</item>
    ''' <item>IMGS fields: <see cref="ImgsFields"/>.</item>
    ''' </list>
    ''' A WTHR without IMSP (or without NAM0) gives no rows: whether the engine's slots stay null (and so 0x167) when the
    ''' subrecord is absent is NOT traced. Vanilla: none (imaging-table --check, skipped empty).
    ''' <paramref name="skipped"/> receives every (key, reason) that gives no row.</summary>
    Friend Function ReadFromPlugins(pm As PluginManager, isSse As Boolean, onlyKeys As ICollection(Of PreviewImagingKey),
                                    Optional skipped As List(Of String) = Nothing) As List(Of PreviewImagingRow)
        ' The trees are parsed with the SESSION schema (CanonBridge.SessionGame = Config_App.Current.Game): one game's
        ' WTHR read with the other's schema puts NAM0 at the wrong offsets. Refuse instead of reading wrong.
        If (CanonBridge.SessionGame() = WbGame.Skyrim) <> isSse Then
            Throw New InvalidOperationException("PreviewImagingTable.ReadFromPlugins: the session game is not the requested one.")
        End If
        Dim moments = If(isSse, 4, 8)
        Dim out As New List(Of PreviewImagingRow)
        Dim imgsCache As New Dictionary(Of UInteger, (Fields As Single(), Lut As String, Reason As String))
        For Each w In pm.GetRecordsOfType("WTHR")
            Dim t = CanonBridge.Tree(w, pm)
            Dim el = Declared(CanonBridge.Find(t, "NAM0\Weather Colors\Effect Lighting"))
            Dim sun = Declared(CanonBridge.Find(t, "NAM0\Weather Colors\Sunlight"))
            Dim ims = Declared(CanonBridge.Find(t, "IMSP\Image Spaces"))
            ' The engine decides by SIZE (NAM0: 0x14056d454 cmp esi,0x220 / jae; IMSP: 0x14056e400 cmp ebx,0x20 / je,
            ' i.e. the copy runs for ANY size other than 0x20); the tree is built by FORM VERSION (wbFromVersion 111).
            ' Where the two disagree the tree's offsets are not the engine's: no row, never a misaligned read.
            Dim nam0 = w.GetSubrecord("NAM0"), imsp = w.GetSubrecord("IMSP")
            Dim shortNam0 = Not isSse AndAlso nam0.HasValue AndAlso nam0.Value.Data.Length < &H220
            Dim shortImsp = Not isSse AndAlso imsp.HasValue AndAlso imsp.Value.Data.Length <> &H20
            Dim oldVersion = Not isSse AndAlso w.Header.Version < 111
            If Not isSse AndAlso (shortNam0 <> oldVersion OrElse shortImsp <> oldVersion) Then
                skipped?.Add($"{pm.GetOriginatingPluginName(w.Header.FormID)} {w.Header.FormID And &HFFFFFFUI:X6}: NAM0/IMSP size and form version disagree (NAM0 {If(nam0.HasValue, nam0.Value.Data.Length, -1)}, IMSP {If(imsp.HasValue, imsp.Value.Data.Length, -1)}, version {w.Header.Version})")
                Continue For
            End If
            Dim origin = pm.GetOriginatingPluginName(w.Header.FormID)
            Dim objectId = w.Header.FormID And &HFFFFFFUI
            For i = 0 To moments - 1
                Dim m = MomentByIndex(i)
                Dim key = New PreviewImagingKey(origin, objectId, m)
                If onlyKeys IsNot Nothing AndAlso Not onlyKeys.Contains(key) Then Continue For
                ' Effect Lighting and Sunlight of the moment (bytes), the same NAM0 law for both colour types.
                Dim elB = MomentBytes(el, i, shortNam0)
                Dim sunB = MomentBytes(sun, i, shortNam0)
                If elB Is Nothing OrElse sunB Is Nothing Then skipped?.Add($"{key.Plugin} {key.ObjectId:X6} {m}: NAM0 does not give the moment (Effect Lighting {elB IsNot Nothing}, Sunlight {sunB IsNot Nothing})") : Continue For
                ' Image space of the moment: a short IMSP copies slot 0 to 4,5 and slot 2 to 6,7 (0x14056e400).
                Dim imsName = If(i >= 4 AndAlso shortImsp, MomentByIndex(If(i < 6, 0, 2)), m)
                Dim imsNode As WbNode = Nothing
                If Not ims.TryGetValue(imsName, imsNode) Then skipped?.Add($"{key.Plugin} {key.ObjectId:X6} {m}: IMSP does not declare the moment") : Continue For
                ' InitItem resolves the FormID with LookupByID + an RTTI cast to TESImageSpace and stores null when
                ' either fails; the getter turns every null into 0x167 (SSE 0x140419c30, FO4 0x1406c4f20). So 0, a
                ' missing form and a form of another type all end in DefaultImageSpace.
                Dim imsId = CUInt(Convert.ToInt64(imsNode.Value) And &HFFFFFFFFL)
                Dim imgsRec = If(imsId = 0UI, Nothing, pm.GetRecord(imsId))
                If imgsRec Is Nothing OrElse imgsRec.Header.Signature <> "IMGS" Then
                    imsId = DefaultImageSpaceFormId
                    imgsRec = pm.GetRecord(imsId)
                End If
                ' No DefaultImageSpace either: the engine skips that contribution (je 0x1404159a7 / 0x1406c00da).
                If imgsRec Is Nothing OrElse imgsRec.Header.Signature <> "IMGS" Then skipped?.Add($"{key.Plugin} {key.ObjectId:X6} {m}: no IMGS and no DefaultImageSpace") : Continue For
                Dim imgs As (Fields As Single(), Lut As String, Reason As String) = Nothing
                If Not imgsCache.TryGetValue(imsId, imgs) Then imgs = ImgsFields(imgsRec, pm, isSse) : imgsCache(imsId) = imgs
                If imgs.Fields Is Nothing Then skipped?.Add($"{key.Plugin} {key.ObjectId:X6} {m}: IMGS {imgsRec.EditorID}: {imgs.Reason}") : Continue For
                Dim v = imgs.Fields
                Dim elV = New Vector3(elB(0), elB(1), elB(2))
                Dim sunV = New Vector3(sunB(0), sunB(1), sunB(2))
                Dim tint = New Vector3(v(9), v(10), v(11))
                Dim imgsOrigin = pm.GetOriginatingPluginName(imgsRec.Header.FormID)
                Dim row As PreviewImagingRow
                If isSse Then
                    row = New PreviewImagingRow(key, True, w.EditorID, w.SourcePluginName, imgsOrigin, imgsRec.Header.FormID And &HFFFFFFUI,
                                                imgsRec.EditorID, imgsRec.SourcePluginName, Nothing, Nothing,
                                                New SseImageSpace(v(0), v(1), v(5), v(6), v(7), tint, v(8)),
                                                New SsePreviewWeather(elV, v(4), sunV))
                Else
                    Dim lutPath = If(String.IsNullOrEmpty(imgs.Lut), "", FO4UnifiedMaterial_Class.CorrectTexturePath(imgs.Lut))
                    row = New PreviewImagingRow(key, False, w.EditorID, w.SourcePluginName, imgsOrigin, imgsRec.Header.FormID And &HFFFFFFUI,
                                                imgsRec.EditorID, imgsRec.SourcePluginName,
                                                New Fo4ImageSpace(v(0), v(1), v(2), v(3), v(4), v(5), v(6), v(7), tint, v(8), lutPath),
                                                New Fo4PreviewWeather(elV, sunV), Nothing, Nothing)
                End If
                out.Add(row)
            Next
        Next
        Return out
    End Function

    ''' <summary>The rows the table has take the values of the load order's winning records; rows the load order
    ''' cannot read keep the table's, with the reason logged. Called by the host that loads plugins
    ''' (LoadOrderPreflight_Form), with WTHR and IMGS in its signature filter. Never stops a load.</summary>
    Friend Function RefreshFromLoadOrder(pm As PluginManager, isSse As Boolean) As (Read As Integer, Kept As Integer, Skipped As IReadOnlyList(Of String), Failed As String)
        Dim table = If(isSse, _baseSse.Value, _baseFo4.Value)
        Dim keys = New HashSet(Of PreviewImagingKey)(table.Select(Function(r) r.Key))
        Dim skipped As New List(Of String)
        Dim read As Dictionary(Of PreviewImagingKey, PreviewImagingRow)
        Try
            read = ReadFromPlugins(pm, isSse, keys, skipped).ToDictionary(Function(r) r.Key)
        Catch ex As Exception
            ' The refresh never stops a load: the table stays as it was and the reason is logged.
            Logger.LogLazy(Function() $"[IMAGING] {If(isSse, "SSE", "FO4")}: load-order refresh failed, the table stays: {ex}")
            Return (0, table.Length, skipped, ex.Message)
        End Try
        Dim rows = table.Select(Function(r) If(read.ContainsKey(r.Key), read(r.Key).MarkedFromLoadOrder(), r)).ToArray()
        SetSnapshot(isSse, New Snapshot(rows))
        Logger.LogLazy(Function() $"[IMAGING] {If(isSse, "SSE", "FO4")}: {read.Count} rows read from the load order, {table.Length - read.Count} kept from the table." &
                                  If(skipped.Count = 0, "", " Not read: " & String.Join("; ", skipped)))
        Return (read.Count, table.Length - read.Count, skipped, Nothing)
    End Function

    ''' <summary>The IMGS fields the preview uses, as the engine leaves them after Load: the constructor's defaults
    ''' (FO4 0x1404af450, SSE 0x1402a61c0), then the record's HNAM+CNAM+TNAM, or its ENAM alone (loader FO4
    ''' 0x1404af83e, SSE 0x1402a652b: ENAM[4] goes to +0x30 AND +0x34; +0x40 keeps the constructor's value).
    ''' Order of the result: FO4 {TonemapE, AEMax, AEMin, MiddleGray, SunlightScale, Sat, Bright, Contrast, Amount,
    ''' R, G, B}; SSE {RBT, White, -, -, SunlightScale, Sat, Bright, Contrast, Amount, R, G, B}.
    ''' Any other mix of subrecords: Nothing + reason (whether the engine reinitializes between overrides is not
    ''' traced; no vanilla IMGS has a mix, measured).</summary>
    Private Function ImgsFields(rec As PluginRecord, pm As PluginManager, isSse As Boolean) As (Fields As Single(), Lut As String, Reason As String)
        Dim sigs = rec.Subrecords.Select(Function(s) s.Signature).ToList()
        Dim hasE = sigs.Contains("ENAM"), hasH = sigs.Contains("HNAM"), hasC = sigs.Contains("CNAM"), hasT = sigs.Contains("TNAM")
        Dim it = CanonBridge.Tree(rec, pm)
        Dim f As String()
        If hasH AndAlso hasC AndAlso hasT AndAlso Not hasE Then
            f = If(isSse,
                {"HNAM\HDR\Receive Bloom Threshold", "HNAM\HDR\White", Nothing, Nothing, "HNAM\HDR\Sunlight Scale",
                 "CNAM\Cinematic\Saturation", "CNAM\Cinematic\Brightness", "CNAM\Cinematic\Contrast",
                 "TNAM\Tint\Amount", "TNAM\Tint\Color\Red", "TNAM\Tint\Color\Green", "TNAM\Tint\Color\Blue"},
                {"HNAM\HDR\Tonemap E", "HNAM\HDR\Auto Exposure Max", "HNAM\HDR\Auto Exposure Min", "HNAM\HDR\Middle Gray",
                 "HNAM\HDR\Sunlight Scale", "CNAM\Cinematic\Saturation", "CNAM\Cinematic\Brightness", "CNAM\Cinematic\Contrast",
                 "TNAM\Tint\Amount", "TNAM\Tint\Color\Red", "TNAM\Tint\Color\Green", "TNAM\Tint\Color\Blue"})
        ElseIf hasE AndAlso Not (hasH OrElse hasC OrElse hasT) Then
            ' ENAM[4] = the xEdit member "Receive Bloom Threshold" (TES5) / "Auto Exposure Min/Max" (FO4), written to
            ' +0x30 and +0x34. FO4 Middle Gray (+0x40) is not in ENAM: Nothing = the constructor's 3.0 (0x1404af4c8).
            f = If(isSse,
                {"ENAM\Data\HDR\Receive Bloom Threshold", "ENAM\Data\HDR\Receive Bloom Threshold", Nothing, Nothing,
                 "ENAM\Data\HDR\Sunlight Scale", "ENAM\Data\Cinematic\Saturation", "ENAM\Data\Cinematic\Brightness",
                 "ENAM\Data\Cinematic\Contrast", "ENAM\Data\Tint\Amount", "ENAM\Data\Tint\Color\Red",
                 "ENAM\Data\Tint\Color\Green", "ENAM\Data\Tint\Color\Blue"},
                {"ENAM\Image Space Data\HDR\Tonemap E", "ENAM\Image Space Data\HDR\Auto Exposure Min/Max",
                 "ENAM\Image Space Data\HDR\Auto Exposure Min/Max", Nothing, "ENAM\Image Space Data\HDR\Sunlight Scale",
                 "ENAM\Image Space Data\Cinematic\Saturation", "ENAM\Image Space Data\Cinematic\Brightness",
                 "ENAM\Image Space Data\Cinematic\Contrast", "ENAM\Image Space Data\Tint\Amount",
                 "ENAM\Image Space Data\Tint\Color\Red", "ENAM\Image Space Data\Tint\Color\Green",
                 "ENAM\Image Space Data\Tint\Color\Blue"})
        Else
            Return (Nothing, Nothing, $"subrecords [{String.Join(",", sigs.Where(Function(s) s = "ENAM" OrElse s = "HNAM" OrElse s = "CNAM" OrElse s = "TNAM"))}] (mix not traced)")
        End If
        ' The constructor's value where the record writes nothing (FO4 Middle Gray +0x40 = 3.0, 0x1404af4c8).
        Dim ctorMiddleGray = 3.0F
        Dim v(11) As Single
        For i = 0 To 11
            If f(i) Is Nothing Then
                v(i) = If(Not isSse AndAlso i = 3, ctorMiddleGray, 0.0F)
                Continue For
            End If
            Dim n = CanonBridge.Find(it, f(i))
            If n Is Nothing Then Return (Nothing, Nothing, "field not found: " & f(i))
            v(i) = Convert.ToSingle(n.Value)
        Next
        Dim lut = If(isSse, Nothing, CanonBridge.Find(it, "TX00\LUT"))
        Return (v, If(lut Is Nothing, "", Convert.ToString(lut.Value)), Nothing)
    End Function

    ''' <summary>The moments a container (NAM0 Effect Lighting, IMSP) DECLARES, by name. A versioned member is a
    ''' union (wbFromVersion) with ONE child: the member itself, or wbEmpty when the record's form version
    ''' predates it (WbDsl.vb:120-122). Unwrapped by its TYPE; wbEmpty = not declared = absent.</summary>
    Private Function Declared(container As WbNode) As Dictionary(Of String, WbNode)
        Dim d As New Dictionary(Of String, WbNode)(StringComparer.Ordinal)
        If container Is Nothing Then Return d
        For Each ch In container.Children
            Dim n = ch
            If TypeOf n.Def Is WbUnionDef Then n = If(n.ChildCount = 1, n.Children(0), Nothing)
            If n Is Nothing OrElse TypeOf n.Def Is WbEmptyDef Then Continue For
            d(ch.Def.Name) = n
        Next
        Return d
    End Function

    ''' <summary>A NAM0 colour type's bytes at engine moment <paramref name="i"/>. FO4 with a short NAM0 (&lt; 0x220):
    ''' moments 4..7 = (a + b) &gt;&gt; 1 per byte, pairs (dest, a, b) = (4,3,0) (5,0,1) (6,1,2) (7,2,3) (0x14056d454,
    ''' 0x14056f190). Nothing when the record does not give the moment.</summary>
    Private Function MomentBytes(colours As Dictionary(Of String, WbNode), i As Integer, shortNam0 As Boolean) As Integer()
        If i >= 4 AndAlso shortNam0 Then
            Dim ea = Bytes(colours, MomentByIndex({3, 0, 1, 2}(i - 4))), eb = Bytes(colours, MomentByIndex({0, 1, 2, 3}(i - 4)))
            If ea Is Nothing OrElse eb Is Nothing Then Return Nothing
            Return {(ea(0) + eb(0)) >> 1, (ea(1) + eb(1)) >> 1, (ea(2) + eb(2)) >> 1}
        End If
        Return Bytes(colours, MomentByIndex(i))
    End Function

    Private Function Bytes(el As Dictionary(Of String, WbNode), moment As String) As Integer()
        Dim n As WbNode = Nothing
        If Not el.TryGetValue(moment, n) Then Return Nothing
        Return {Convert.ToInt32(CanonBridge.Find(n, "Red").Value), Convert.ToInt32(CanonBridge.Find(n, "Green").Value),
                Convert.ToInt32(CanonBridge.Find(n, "Blue").Value)}
    End Function
End Module
