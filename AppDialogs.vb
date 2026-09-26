Option Strict On
Option Infer On

Imports System.IO
Imports System.Windows.Forms

''' <summary>The generic message dialogs of the workspace apps (moved from Item Sorter, 26-sep, so SafeScrap and Item
''' Sorter share one copy). The title is passed on every call: there is no global app title.
''' <para>A class of Shared members, not a Module: a Module's members are visible unqualified across the whole
''' project and <c>Info</c> collided with <c>For Each info In</c> loops.</para></summary>
Public NotInheritable Class AppDialogs

    Private Sub New()
    End Sub

    Private Shared Function Eol(text As String) As String
        Return If(text, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Replace(vbLf, vbCrLf)
    End Function

    ''' <summary>OK / Cancel.</summary>
    Public Shared Function Confirm(owner As IWin32Window, title As String, text As String) As Boolean
        Return MessageBox.Show(owner, Eol(text), title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) = DialogResult.OK
    End Function

    ''' <summary>Yes / No / Cancel.</summary>
    Public Shared Function YesNoCancel(owner As IWin32Window, title As String, text As String) As DialogResult
        Return MessageBox.Show(owner, Eol(text), title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
    End Function

    Public Shared Sub Info(owner As IWin32Window, title As String, text As String)
        MessageBox.Show(owner, Eol(text), title, MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Shared Sub Warn(owner As IWin32Window, title As String, text As String)
        MessageBox.Show(owner, Eol(text), title, MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Public Shared Sub [Error](owner As IWin32Window, title As String, ex As Exception)
        MessageBox.Show(owner, ex.GetType().Name & ": " & ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

End Class

''' <summary>Opens a file or a folder with the system.</summary>
Public NotInheritable Class ShellOpen

    Private Sub New()
    End Sub

    Public Shared Sub OpenFile(owner As IWin32Window, path As String)
        Try
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(path) With {.UseShellExecute = True})
        Catch ex As System.ComponentModel.Win32Exception
            AppDialogs.Warn(owner, "Open file", "No application is associated with this file." & vbCr & ex.Message)
        End Try
    End Sub

    ''' <summary>Opens a folder in the Explorer (created when missing).</summary>
    Public Shared Sub OpenFolder(owner As IWin32Window, folder As String)
        Try
            Directory.CreateDirectory(folder)
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(folder) With {.UseShellExecute = True})
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is System.ComponentModel.Win32Exception
            AppDialogs.Warn(owner, "Open folder", folder & vbCr & ex.Message)
        End Try
    End Sub

End Class

''' <summary>Wait cursor while the block runs.</summary>
Public NotInheritable Class WaitCursor
    Implements IDisposable
    Private ReadOnly _prev As Cursor
    Public Sub New()
        _prev = Cursor.Current
        Cursor.Current = Cursors.WaitCursor
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        Cursor.Current = _prev
    End Sub
End Class
