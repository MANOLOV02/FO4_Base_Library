Option Strict On
Option Infer On

''' <summary>The FORM IDENTIFIER: <c>Master.esp|HEX6</c> — the plugin that owns a record plus its object id, with no
''' load-order index. One format, one place: NPC Manager keys the .bssliders sidecar and the LooksMenu/RaceMenu JSON
''' with it (<c>BssliderSidecar.BuildIdentifier</c> delegates here) and SafeScrap keys its object list and decisions.
''' <para>The object id is <see cref="PluginManager.ToFaceGenLocalFormID"/>: 12 bits for a light owner, 24 for a full one,
''' the same mask the engine applies (cited there). The engine puts the current slot back when it reads the id.</para>
''' <para>Pure: no placeholder for an unknown owner. A caller that has one (the sidecar's <c>Unknown.esp</c> bucket)
''' substitutes it BEFORE calling; an empty owner here is a caller error and throws.</para></summary>
Public Module FormIdentifiers

    ''' <summary><c>{masterName}|{object id:X6}</c>. <paramref name="masterName"/> is written as given (no case folding).</summary>
    Public Function Build(masterName As String, globalFormID As UInteger) As String
        If String.IsNullOrEmpty(masterName) Then
            Throw New ArgumentException("A form identifier needs the owner plugin name.", NameOf(masterName))
        End If
        Return $"{masterName}|{PluginManager.ToFaceGenLocalFormID(globalFormID):X6}"
    End Function

    ''' <summary>Reverse of <see cref="Build"/>: split <c>"Master.esp|HEX6"</c> into the master filename and the local
    ''' 24-bit FormID. Returns False if the identifier is malformed (no pipe, hex unparseable, empty master).</summary>
    Public Function TryParse(identifier As String,
                             ByRef masterPluginName As String,
                             ByRef localFormID As UInteger) As Boolean
        masterPluginName = ""
        localFormID = 0UI
        If String.IsNullOrEmpty(identifier) Then Return False
        Dim pipeIdx = identifier.IndexOf("|"c)
        If pipeIdx <= 0 OrElse pipeIdx >= identifier.Length - 1 Then Return False
        Dim master = identifier.Substring(0, pipeIdx).Trim()
        If String.IsNullOrEmpty(master) Then Return False
        Dim hex = identifier.Substring(pipeIdx + 1).Trim()
        Dim parsed As UInteger
        If Not UInteger.TryParse(hex, Globalization.NumberStyles.HexNumber,
                                 Globalization.CultureInfo.InvariantCulture, parsed) Then Return False
        masterPluginName = master
        localFormID = parsed And &HFFFFFFUI
        Return True
    End Function

End Module
