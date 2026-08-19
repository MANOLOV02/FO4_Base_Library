Imports System.Text

' ============================================================================
' Shared helper functions used by all record parser modules.
' These are called by every parser file to resolve FormIDs and strings.
' ============================================================================

Friend Module ParserHelpers

    ''' <summary>Resolve a display string from a subrecord, handling localization.</summary>
    Friend Function ResolveStr(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager,
                               Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If pluginManager IsNot Nothing Then Return pluginManager.ResolveFieldString(rec, sr, kind)

        ' No pluginManager: still honor per-file encoding (TES4 SNAM <cp:XXXX>) when available,
        ' falling back to the global only when the record has no override. Mirror of the same
        ' bsdGetEncoding precedence used inside ResolveFieldString.
        If rec IsNot Nothing AndAlso rec.SourcePluginTranslatableEncoding IsNot Nothing AndAlso Not rec.SourcePluginIsLocalized Then
            If sr.Data Is Nothing OrElse sr.Data.Length = 0 Then Return ""
            Dim len = sr.Data.Length
            If len > 0 AndAlso sr.Data(len - 1) = 0 Then len -= 1
            Return PluginEncodingSettings.DecodeWithEncoding(sr.Data, 0, len, rec.SourcePluginTranslatableEncoding)
        End If

        Return sr.AsString
    End Function

    ''' <summary>Resolve a FormID reference from a subrecord.</summary>
    Friend Function ResolveFID(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As UInteger
        If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Return 0UI
        Return ResolveFIDRaw(rec, sr.AsUInt32, pluginManager)
    End Function

    ''' <summary>Resolve a raw FormID value using the plugin's master list. ÚNICO lugar donde vive la
    ''' política de nulos de los parsers — <c>RecordParsers.ResolveFormIDReference</c> reenvía acá.
    ''' <para>⛔ SIN PluginManager EL FormID VUELVE CRUDO: sin el mapeo de master-index queda un número
    ''' PLAUSIBLE Y EQUIVOCADO, sin error. Es el motivo por el que los <c>Optional pluginManager As
    ''' PluginManager = Nothing</c> de estos parsers dejaron de ser opcionales: la firma ANUNCIABA como
    ''' soportado un modo que corrompe, mientras los llamadores reales habían concluido por separado que
    ''' no lo era (se sacó el Optional y compiló limpio: nadie lo omitía). Esta guarda queda como red para
    ''' quien pase Nothing a propósito — que ahora es un acto explícito, no un default.</para></summary>
    ''' <summary>Centinela <c>0xFFFFFFFF</c>: NO es una referencia y pasa SIN TOCAR.
    ''' <para>El canónico lo declara campo por campo agregando la pseudo-firma <c>FFFF</c> a la lista de
    ''' destinos permitidos — p. ej. <c>wbFormIDCk('Emotion', [KYWD, FFFF])</c> (wbDefinitionsFO4.pas:10074) y
    ''' <c>wbFormIDCk(ANAM, 'Condition Actor Value', [AVIF, NULL, FFFF])</c> en EQUP. Significa "ninguno / todos",
    ''' no "el record 0xFFFFFFFF".</para>
    ''' <para>⛔ Sin este corte el valor se remapeaba: el byte alto <c>0xFF</c> nunca es un índice de master
    ''' válido (el tope es 0xFD full / 0xFE light), así que caía en la rama "self", se le tomaba el object id
    ''' 0xFFFFFF y salía como un FormID del propio archivo. MEDIDO: el peor ejemplo de
    ''' <c>INFO.Responses[].EmotionFormID</c> era exactamente <c>0x00FFFFFF</c> — el centinela reconstruido —
    ''' con 41.668/96.828 casos en FO4.</para>
    ''' <para>Va acá y no en cada campo porque <c>0xFFFFFFFF</c> no puede ser una referencia real en NINGÚN
    ''' campo: el índice de master no existe. Un campo que no admita el centinela lo va a ver igual como
    ''' 0xFFFFFFFF y podrá tratarlo como inválido, que es lo correcto.</para></summary>
    Friend Const FORMID_SENTINEL_NONE As UInteger = &HFFFFFFFFUI

    Friend Function ResolveFIDRaw(rec As PluginRecord, rawFormID As UInteger, pluginManager As PluginManager) As UInteger
        If rawFormID = FORMID_SENTINEL_NONE Then Return rawFormID
        If pluginManager Is Nothing OrElse rec Is Nothing Then Return rawFormID
        Return pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rawFormID)
    End Function

    ''' <summary>Parse an array of FormIDs from a KWDA-style subrecord into a list.</summary>
    Friend Sub ParseFormIDArray(sr As SubrecordData, rec As PluginRecord, pluginManager As PluginManager, target As List(Of UInteger))
        If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Return
        For i = 0 To sr.Data.Length - 4 Step 4
            Dim fid = ResolveFIDRaw(rec, BitConverter.ToUInt32(sr.Data, i), pluginManager)
            If fid <> 0UI Then target.Add(fid)
        Next
    End Sub

End Module
