' Version Uploaded of Fo4Library 3.2.0
Imports System.Drawing
Imports System.Windows.Forms

Public Enum AppTheme
    Light = 0
    Dark = 1
End Enum

Public Module ThemeManager
    Public Property CurrentTheme As AppTheme = AppTheme.Dark

    ' Paleta base
    Private ReadOnly DarkBg As Color = Color.FromArgb(30, 30, 30)
    Private ReadOnly DarkBgAlt As Color = Color.FromArgb(37, 37, 38)
    Private ReadOnly DarkBgAlt2 As Color = Color.FromArgb(45, 45, 48)
    Private ReadOnly DarkFg As Color = Color.White
    Private ReadOnly DarkBorder As Color = Color.DimGray

    Private ReadOnly LightBg As Color = SystemColors.Control
    Private ReadOnly LightBgAlt As Color = Color.White
    Private ReadOnly LightBgAlt2 As Color = SystemColors.ControlLight
    Private ReadOnly LightFg As Color = Color.Black
    Private ReadOnly LightBorder As Color = SystemColors.ActiveBorder

    ' Punto de entrada: aplica el tema a todo un árbol de controles y
    ' engancha ControlAdded para mantener la consistencia global.
    Public Sub ApplyTheme(root As Control)
        If root Is Nothing Then Return
        root.SuspendLayout()
        ApplyToControlTree(root)
        HookControlAddedRecursively(root)
        root.ResumeLayout()
        root.Invalidate(True)
    End Sub

    ' Cambiar tema y reaplicar en el root indicado.
    Public Sub SetTheme(theme As AppTheme, root As Control)
        CurrentTheme = theme
        ApplyTheme(root)
    End Sub

    ' ======== Internos ========

    Private Sub ApplyToControlTree(parent As Control)
        ApplyToControl(parent)
        For Each c As Control In parent.Controls
            ApplyToControlTree(c)
        Next
    End Sub

    Private Sub HookControlAddedRecursively(parent As Control)
        RemoveHandler parent.ControlAdded, AddressOf OnControlAdded
        AddHandler parent.ControlAdded, AddressOf OnControlAdded
        For Each c As Control In parent.Controls
            RemoveHandler c.ControlAdded, AddressOf OnControlAdded
            AddHandler c.ControlAdded, AddressOf OnControlAdded
            If c.HasChildren Then
                HookControlAddedRecursively(c)
            End If
        Next
    End Sub

    Private Sub OnControlAdded(sender As Object, e As ControlEventArgs)
        If e Is Nothing OrElse e.Control Is Nothing Then Return
        ApplyToControl(e.Control)
        If e.Control.HasChildren Then
            HookControlAddedRecursively(e.Control)
        End If
    End Sub

    Private Sub ApplyToControl(ctrl As Control)
        Dim palette = ResolvePalette()

        Dim bg As Color = palette.bg
        Dim bgAlt As Color = palette.bgAlt
        Dim bgAlt2 As Color = palette.bgAlt2
        Dim fg As Color = palette.fg
        Dim border As Color = palette.border

        ' Defaults
        ctrl.BackColor = If(TypeOf ctrl Is Label OrElse TypeOf ctrl Is LinkLabel, ctrl.BackColor, bg)
        ctrl.ForeColor = fg

        ' Específicos por tipo
        If TypeOf ctrl Is Form Then
            Dim f = DirectCast(ctrl, Form)
            f.BackColor = bg
            f.ForeColor = fg
        ElseIf TypeOf ctrl Is Button Then
            Dim b = DirectCast(ctrl, Button)
            b.FlatStyle = FlatStyle.Flat
            b.BackColor = bgAlt2
            b.ForeColor = fg
            b.FlatAppearance.BorderColor = border
        ElseIf TypeOf ctrl Is Label Then
            Dim l = DirectCast(ctrl, Label)
            l.ForeColor = fg
            ' Mantener transparente para respetar fondo del contenedor
            l.BackColor = Color.Transparent
        ElseIf TypeOf ctrl Is LinkLabel Then
            Dim ll = DirectCast(ctrl, LinkLabel)
            ll.LinkColor = fg
            ll.ActiveLinkColor = fg
            ll.VisitedLinkColor = fg
            ll.BackColor = Color.Transparent
        ElseIf TypeOf ctrl Is TextBox Then
            Dim t = DirectCast(ctrl, TextBox)
            t.BackColor = bgAlt
            t.ForeColor = fg
            t.BorderStyle = BorderStyle.FixedSingle
        ElseIf TypeOf ctrl Is RichTextBox Then
            Dim rt = DirectCast(ctrl, RichTextBox)
            rt.BackColor = bgAlt
            rt.ForeColor = fg
            rt.BorderStyle = BorderStyle.FixedSingle
        ElseIf TypeOf ctrl Is ComboBox Then
            Dim cb = DirectCast(ctrl, ComboBox)
            cb.BackColor = bgAlt
            cb.ForeColor = fg
            cb.FlatStyle = FlatStyle.Flat
        ElseIf TypeOf ctrl Is ListBox Then
            Dim lb = DirectCast(ctrl, ListBox)
            lb.BackColor = bgAlt
            lb.ForeColor = fg
            lb.BorderStyle = BorderStyle.FixedSingle
        ElseIf TypeOf ctrl Is CheckedListBox Then
            Dim clb = DirectCast(ctrl, CheckedListBox)
            clb.BackColor = bgAlt
            clb.ForeColor = fg
            clb.BorderStyle = BorderStyle.FixedSingle
        ElseIf TypeOf ctrl Is GroupBox Then
            Dim g = DirectCast(ctrl, GroupBox)
            g.ForeColor = fg
            g.BackColor = bg
        ElseIf TypeOf ctrl Is Panel Then
            Dim p = DirectCast(ctrl, Panel)
            p.BackColor = bg
        ElseIf TypeOf ctrl Is TabControl Then
            Dim tc = DirectCast(ctrl, TabControl)
            tc.BackColor = bg
            tc.ForeColor = fg
            For Each page As TabPage In tc.TabPages
                page.BackColor = bg
                page.ForeColor = fg
            Next
        ElseIf TypeOf ctrl Is ListView Then
            Dim lv = DirectCast(ctrl, ListView)
            lv.BackColor = bgAlt
            lv.ForeColor = fg
            lv.BorderStyle = BorderStyle.FixedSingle
        ElseIf TypeOf ctrl Is DataGridView Then
            Dim dgv = DirectCast(ctrl, DataGridView)
            dgv.EnableHeadersVisualStyles = False
            dgv.BackgroundColor = bg
            dgv.GridColor = border

            dgv.ColumnHeadersDefaultCellStyle.BackColor = bgAlt2
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = fg
            dgv.RowHeadersDefaultCellStyle.BackColor = bgAlt2
            dgv.RowHeadersDefaultCellStyle.ForeColor = fg

            dgv.DefaultCellStyle.BackColor = bgAlt
            dgv.DefaultCellStyle.ForeColor = fg
            dgv.DefaultCellStyle.SelectionBackColor = bgAlt2
            dgv.DefaultCellStyle.SelectionForeColor = fg

            dgv.AlternatingRowsDefaultCellStyle.BackColor = bg
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = fg
        ElseIf TypeOf ctrl Is MenuStrip Then
            Dim ms = DirectCast(ctrl, MenuStrip)
            ms.BackColor = bg
            ms.ForeColor = fg
            For Each it As ToolStripItem In ms.Items
                ApplyToToolStripItem(it, bg, fg)
            Next
        ElseIf TypeOf ctrl Is StatusStrip Then
            Dim ss = DirectCast(ctrl, StatusStrip)
            ss.BackColor = bg
            ss.ForeColor = fg
            For Each it As ToolStripItem In ss.Items
                ApplyToToolStripItem(it, bg, fg)
            Next
        ElseIf TypeOf ctrl Is ToolStrip Then
            Dim ts = DirectCast(ctrl, ToolStrip)
            ts.BackColor = bg
            ts.ForeColor = fg
            For Each it As ToolStripItem In ts.Items
                ApplyToToolStripItem(it, bg, fg)
            Next
        End If
    End Sub

    Private Sub ApplyToToolStripItem(it As ToolStripItem, bg As Color, fg As Color)
        it.BackColor = bg
        it.ForeColor = fg
        If TypeOf it Is ToolStripMenuItem Then
            Dim mi = DirectCast(it, ToolStripMenuItem)
            For Each child As ToolStripItem In mi.DropDownItems
                ApplyToToolStripItem(child, bg, fg)
            Next
        End If
    End Sub

    Private Function ResolvePalette() As (bg As Color, bgAlt As Color, bgAlt2 As Color, fg As Color, border As Color)
        If CurrentTheme = AppTheme.Dark Then
            Return (DarkBg, DarkBgAlt, DarkBgAlt2, DarkFg, DarkBorder)
        Else
            Return (LightBg, LightBgAlt, LightBgAlt2, LightFg, LightBorder)
        End If
    End Function
End Module

