Imports System.IO

''' <summary>THE GAME'S OWN INI SETTINGS, read like the game reads them (Tools/re-docs/RE_REFRACTION_BOTH_2026-10-03.md 8): one
''' place for every value the app takes from the installed Skyrim*.ini / Fallout4*.ini (the folder PluginManager.ResolveGameIniPath
''' resolves).
''' <para>User decision 3-oct-2026: the render reads NO engine Setting from these files (exe .data values or app values,
''' RefractionLaw.LodScale, SseRenderPassLaw.ImprovedSnowExeDefault); this module serves only sLanguage (LocalizedStrings).</para>
''' <list type="bullet">
''' <item>Every setting belongs to ONE list and is read only from that list's files, at startup (SSE 0x140652AB0, FO4
''' 0x140C2FFF0): MAIN (INISettingCollection) = Skyrim.ini / Fallout4.ini, then SkyrimCustom.ini / Fallout4Custom.ini, then (SSE
''' only, 0x140661720) Data\&lt;plugin&gt;.ini of each active plugin in load order; PREF (INIPrefSettingCollection) =
''' Skyrim.ini / Fallout4.ini, then SkyrimPrefs.ini / Fallout4Prefs.ini.</item>
''' <item>Each file is read with GetPrivateProfileString, the current value as the default (SSE 0x140FD0E10, FO4 0x1417A0A00):
''' a later file overrides only the keys it has; inside a file the FIRST occurrence of the key in its section is the one
''' Windows returns; section and key match case-insensitively; a key outside any section is never found.</item>
''' <item>A setting's name is key:Section.</item>
''' </list></summary>
Friend Module GameIniSettings

    Friend Enum IniList
        Main
        Pref
    End Enum

    ''' <summary>The files of <paramref name="list"/> in the order the game reads them (a later one wins).</summary>
    Friend Function Files(isSse As Boolean, list As IniList) As List(Of String)
        Dim r As New List(Of String) From {PluginManager.ResolveGameIniPath(If(isSse, "Skyrim.ini", "Fallout4.ini"))}
        If list = IniList.Pref Then
            r.Add(PluginManager.ResolveGameIniPath(If(isSse, "SkyrimPrefs.ini", "Fallout4Prefs.ini")))
            Return r
        End If
        r.Add(PluginManager.ResolveGameIniPath(If(isSse, "SkyrimCustom.ini", "Fallout4Custom.ini")))
        If isSse AndAlso Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso
           Not String.IsNullOrEmpty(Config_App.Current.FO4ExePath) Then
            Dim data = Path.Combine(Path.GetDirectoryName(Config_App.Current.FO4ExePath), "Data")
            Try
                For Each plugin In PluginManager.ReadActiveLoadOrder()
                    r.Add(Path.Combine(data, Path.GetFileNameWithoutExtension(plugin) & ".ini"))
                Next
            Catch
                ' No readable load order: the plugin INIs are not added (their absence is the same as a load order
                ' without them).
            End Try
        End If
        Return r
    End Function

    ''' <summary>[<paramref name="section"/>] <paramref name="key"/> of a list (the last file that has it; inside a file the
    ''' first occurrence), or Nothing.</summary>
    Friend Function GetValue(isSse As Boolean, list As IniList, section As String, key As String) As String
        Dim result As String = Nothing
        For Each path In Files(isSse, list)
            If String.IsNullOrEmpty(path) OrElse Not File.Exists(path) Then Continue For
            Dim v = FirstInFile(path, section, key)
            If v IsNot Nothing Then result = v
        Next
        Return result
    End Function

    Private Function FirstInFile(path As String, section As String, key As String) As String
        Dim actual As String = Nothing
        For Each rawLine In File.ReadLines(path)
            Dim line = rawLine.Trim()
            If line.Length = 0 OrElse line.StartsWith(";") Then Continue For
            If line.StartsWith("[") Then
                Dim fin = line.IndexOf("]"c)
                actual = If(fin > 0, line.Substring(1, fin - 1).Trim(), Nothing)
                Continue For
            End If
            If actual Is Nothing OrElse Not String.Equals(actual, section, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim eq = line.IndexOf("="c)
            If eq <= 0 Then Continue For
            If String.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase) Then Return line.Substring(eq + 1).Trim()
        Next
        Return Nothing
    End Function

End Module
