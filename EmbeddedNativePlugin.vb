Option Strict On
Option Infer On

Imports System.IO

''' <summary><b>A native game plugin (.dll) that an app carries EMBEDDED and installs into the game's Data.</b> The one
''' seat of that law for every app of the workspace (moved from NPC Manager's <c>NativePluginInstaller</c>, SafeScrap phase
''' C, 26-sep; NPC Manager keeps its API as a facade over this class).
''' <para><b>The owner's assembly is a parameter</b>, never <c>GetExecutingAssembly</c>: that would be THIS library, which
''' embeds no plugin, and every lookup would come back empty.</para>
''' <para><b>The game is a parameter too</b> (<see cref="TargetGame"/>): an F4SE plugin belongs to Fallout 4, an SKSE one to
''' Skyrim. With another game active, or without a Data folder, <see cref="Install"/>, <see cref="Remove"/> and
''' <see cref="Reconcile"/> touch NOTHING — deleting "just in case" in another game's Data would touch that game's mods.</para>
''' <para><b>The version check is a comparison of BYTES</b> against the embedded resource, not a number: a file that
''' differs in one byte from the one this build carries is overwritten. It covers an old build of ours and a foreign copy
''' at that path at once, without anyone having to remember to bump a counter.</para>
''' <para><b>It writes OUTSIDE the app</b> with <c>EscrituraEnElLugar</c> (never <c>File.WriteAllBytes</c>: CREATE_ALWAYS
''' gets ACCESS_DENIED on a hidden target and breaks MO2's VFS / Vortex's hardlink). It asks nothing: the confirmation
''' belongs to the caller's UI, because this also runs at start-up where there is nobody to ask.</para>
''' <para>Every method returns a short text of what it did (for the log and the caller), "" when it did nothing. A failure
''' to read, write or delete is RETURNED ("…-failed: message"), never thrown, whatever its type — the behaviour NPC Manager
''' had: this runs at start-up, where an exception would take the app down for a plugin file.</para></summary>
Public NotInheritable Class EmbeddedNativePlugin

    Public ReadOnly Property Owner As Reflection.Assembly
    ''' <summary>The resource's LogicalName, as the owner's project declares it.</summary>
    Public ReadOnly Property ResourceName As String
    ''' <summary>Path relative to <c>Data\</c>, e.g. <c>F4SE\Plugins\X.dll</c>.</summary>
    Public ReadOnly Property DataRelativePath As String
    ''' <summary>The only game whose Data this plugin may be written to or deleted from.</summary>
    Public ReadOnly Property TargetGame As Config_App.Game_Enum
    ''' <summary>The tag of the owner's log lines, e.g. <c>LOADBAKE</c>.</summary>
    Public ReadOnly Property LogTag As String

    Public Sub New(owner As Reflection.Assembly, resourceName As String, dataRelativePath As String,
                   targetGame As Config_App.Game_Enum, logTag As String)
        If owner Is Nothing Then Throw New ArgumentNullException(NameOf(owner))
        Me.Owner = owner
        Me.ResourceName = resourceName
        Me.DataRelativePath = dataRelativePath
        Me.TargetGame = targetGame
        Me.LogTag = logTag
    End Sub

    ''' <summary>The bytes of the plugin this build carries, or Nothing when the resource is not embedded.</summary>
    Public Function EmbeddedBytes() As Byte()
        Using s = Owner.GetManifestResourceStream(ResourceName)
            If s Is Nothing Then Return Nothing
            Using ms As New MemoryStream()
                s.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Using
    End Function

    ''' <summary>True when this build carries the plugin (a non-empty embedded resource): the one predicate the
    ''' installer and every caller (e.g. an export confirmation) use.</summary>
    Public Function HasResource() As Boolean
        Dim b = EmbeddedBytes()
        Return b IsNot Nothing AndAlso b.Length > 0
    End Function

    ''' <summary>Absolute path of the installed file, or "" without a Data folder.</summary>
    Public Function InstalledPath(dataPath As String) As String
        If String.IsNullOrEmpty(dataPath) Then Return ""
        Return Path.Combine(dataPath, DataRelativePath)
    End Function

    ''' <summary>True if the file is in Data (without looking whether it is ours or up to date).</summary>
    Public Function IsInstalled(dataPath As String) As Boolean
        Dim p = InstalledPath(dataPath)
        Return p <> "" AndAlso File.Exists(p)
    End Function

    ''' <summary>True if the installed file is EXACTLY the one of this build.</summary>
    Public Function IsUpToDate(dataPath As String) As Boolean
        Dim p = InstalledPath(dataPath)
        If p = "" OrElse Not File.Exists(p) Then Return False
        Dim ours = EmbeddedBytes()
        If ours Is Nothing OrElse ours.Length = 0 Then Return False
        Try
            Dim onDisk = File.ReadAllBytes(p)
            Return onDisk.Length = ours.Length AndAlso onDisk.SequenceEqual(ours)
        Catch ex As Exception
            ' Unreadable (locked / permissions): it cannot be said to be up to date.
            Return False
        End Try
    End Function

    ''' <summary>True when this plugin's game is the active one and there is a Data folder (the guard of every write).</summary>
    Public Function Applies(dataPath As String) As Boolean
        Dim cfg = Config_App.Current
        Return cfg IsNot Nothing AndAlso cfg.Game = TargetGame AndAlso Not String.IsNullOrEmpty(dataPath)
    End Function

    ''' <summary>Writes the plugin if it is missing or differs from this build's (ours ALWAYS wins).
    ''' "installed" / "up-to-date" / "missing-resource" / "install-failed: …", or "" when it does not apply.</summary>
    Public Function Install(dataPath As String) As String
        If Not Applies(dataPath) Then Return ""
        Dim dest = InstalledPath(dataPath)
        If Not HasResource() Then
            Logger.LogLazy(Function() $"[{LogTag}] the plugin is not embedded in this build: nothing to install")
            Return "missing-resource"
        End If
        If IsUpToDate(dataPath) Then Return "up-to-date"
        Dim bytes = EmbeddedBytes()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(dest))
            BSA_BA2_Library_DLL.EscrituraEnElLugar.Escribir(dest, Sub(fs) fs.Write(bytes, 0, bytes.Length))
            Logger.LogLazy(Function() $"[{LogTag}] installed '{dest}' ({bytes.Length} bytes)")
            Return "installed"
        Catch ex As Exception
            Dim m = ex.Message
            Logger.LogLazy(Function() $"[{LogTag}] could NOT install '{dest}': {m}")
            Return "install-failed: " & m
        End Try
    End Function

    ''' <summary>Deletes the file at the plugin's path EVEN IF IT IS NOT OURS: at that path, with that name, this app put
    ''' it; leaving a different copy "because I do not recognise it" leaves hooked exactly what the user asked to remove.
    ''' "removed" / "remove-failed: …", or "" when there is nothing to remove or it does not apply.</summary>
    Public Function Remove(dataPath As String) As String
        If Not Applies(dataPath) Then Return ""
        Dim dest = InstalledPath(dataPath)
        If Not File.Exists(dest) Then Return ""
        Try
            File.Delete(dest)
            Logger.LogLazy(Function() $"[{LogTag}] removed '{dest}'")
            Return "removed"
        Catch ex As Exception
            Dim m = ex.Message
            Logger.LogLazy(Function() $"[{LogTag}] could NOT remove '{dest}': {m}")
            Return "remove-failed: " & m
        End Try
    End Function

    ''' <summary><b>Makes the disk follow the owner's choice.</b> Idempotent and without UI: <paramref name="wanted"/> ⇒
    ''' <see cref="Install"/>, not wanted ⇒ <see cref="Remove"/>.</summary>
    Public Function Reconcile(wanted As Boolean) As String
        Dim dataPath = If(Config_App.Current Is Nothing, "", Config_App.Current.DataPath)
        Return If(wanted, Install(dataPath), Remove(dataPath))
    End Function

End Class
