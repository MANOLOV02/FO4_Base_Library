' Version Uploaded of Wardrobe 3.1.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class DictionaryPicker_Control
    Inherits System.Windows.Forms.UserControl

    'Descartar overrides de Dispose para limpiar la lista de componentes.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Requerido por el Diseñador de Windows Forms
    Private components As System.ComponentModel.IContainer

    'NOTA: el Diseñador necesita el siguiente procedimiento
    'Se puede modificar usando el Diseñador de Windows Forms.  
    'No lo modifiques con el editor de código.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        panelTop = New Panel()
        lblRoot = New Label()
        chkShowOverrides = New CheckBox()
        splitMain = New SplitContainer()
        tvDirs = New TreeView()
        lvFiles = New ListView()
        colNombre = New ColumnHeader()
        colExtension = New ColumnHeader()
        colRuta = New ColumnHeader()
        panelBottom = New Panel()
        ButtonClone = New Button()
        txtPath = New TextBox()
        btnOk = New Button()
        btnCancel = New Button()
        ToolTip1 = New ToolTip(components)
        CType(splitMain, ComponentModel.ISupportInitialize).BeginInit()
        panelTop.SuspendLayout()
        splitMain.Panel1.SuspendLayout()
        splitMain.Panel2.SuspendLayout()
        splitMain.SuspendLayout()
        panelBottom.SuspendLayout()
        SuspendLayout()
        '
        ' panelTop
        '
        panelTop.Controls.Add(lblRoot)
        panelTop.Controls.Add(chkShowOverrides)
        panelTop.Dock = DockStyle.Top
        panelTop.Name = "panelTop"
        panelTop.Size = New Size(1041, 27)
        ' 
        ' lblRoot
        ' 
        lblRoot.AutoSize = True
        lblRoot.Dock = DockStyle.Left
        lblRoot.Location = New Point(0, 0)
        lblRoot.Name = "lblRoot"
        lblRoot.Padding = New Padding(8, 8, 0, 4)
        lblRoot.Size = New Size(57, 27)
        lblRoot.TabIndex = 0
        lblRoot.Text = "Filtered:"
        '
        ' chkShowOverrides
        '
        chkShowOverrides.AutoSize = True
        chkShowOverrides.Dock = DockStyle.Right
        chkShowOverrides.Name = "chkShowOverrides"
        chkShowOverrides.Padding = New Padding(0, 6, 8, 0)
        chkShowOverrides.TabIndex = 4
        chkShowOverrides.Text = "Show overrides"
        chkShowOverrides.UseVisualStyleBackColor = True
        ' 
        ' splitMain
        ' 
        splitMain.Dock = DockStyle.Fill
        splitMain.Location = New Point(0, 27)
        splitMain.Name = "splitMain"
        ' 
        ' splitMain.Panel1
        ' 
        splitMain.Panel1.Controls.Add(tvDirs)
        ' 
        ' splitMain.Panel2
        ' 
        splitMain.Panel2.Controls.Add(lvFiles)
        splitMain.Size = New Size(1041, 478)
        splitMain.SplitterDistance = 353
        splitMain.TabIndex = 1
        ' 
        ' tvDirs
        ' 
        tvDirs.Dock = DockStyle.Fill
        tvDirs.HideSelection = False
        tvDirs.Location = New Point(0, 0)
        tvDirs.Name = "tvDirs"
        tvDirs.Size = New Size(353, 478)
        tvDirs.TabIndex = 0
        ' 
        ' lvFiles
        ' 
        lvFiles.Columns.AddRange(New ColumnHeader() {colNombre, colExtension, colRuta})
        lvFiles.Dock = DockStyle.Fill
        lvFiles.FullRowSelect = True
        lvFiles.HeaderStyle = ColumnHeaderStyle.Nonclickable
        lvFiles.Location = New Point(0, 0)
        lvFiles.MultiSelect = False
        lvFiles.Name = "lvFiles"
        lvFiles.ShowItemToolTips = True
        lvFiles.Size = New Size(684, 478)
        lvFiles.TabIndex = 0
        lvFiles.UseCompatibleStateImageBehavior = False
        lvFiles.View = View.Details
        ' 
        ' colNombre
        ' 
        colNombre.Text = "Name"
        colNombre.Width = 280
        ' 
        ' colExtension
        ' 
        colExtension.Text = "Source"
        colExtension.Width = 100
        ' 
        ' colRuta
        ' 
        colRuta.Text = "Relative path"
        colRuta.Width = 300
        ' 
        ' panelBottom
        ' 
        panelBottom.Controls.Add(ButtonClone)
        panelBottom.Controls.Add(txtPath)
        panelBottom.Controls.Add(btnOk)
        panelBottom.Controls.Add(btnCancel)
        panelBottom.Dock = DockStyle.Bottom
        panelBottom.Location = New Point(0, 505)
        panelBottom.Name = "panelBottom"
        panelBottom.Size = New Size(1041, 49)
        panelBottom.TabIndex = 2
        ' 
        ' ButtonClone
        ' 
        ButtonClone.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        ButtonClone.Enabled = False
        ButtonClone.Location = New Point(781, 12)
        ButtonClone.Name = "ButtonClone"
        ButtonClone.Size = New Size(80, 25)
        ButtonClone.TabIndex = 3
        ButtonClone.Text = "Clone"
        ButtonClone.UseVisualStyleBackColor = True
        ' 
        ' txtPath
        ' 
        txtPath.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtPath.Location = New Point(8, 13)
        txtPath.Name = "txtPath"
        txtPath.ReadOnly = True
        txtPath.Size = New Size(754, 23)
        txtPath.TabIndex = 0
        ' 
        ' btnOk
        ' 
        btnOk.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnOk.Location = New Point(867, 12)
        btnOk.Name = "btnOk"
        btnOk.Size = New Size(80, 25)
        btnOk.TabIndex = 1
        btnOk.Text = "Accept"
        btnOk.UseVisualStyleBackColor = True
        ' 
        ' btnCancel
        ' 
        btnCancel.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnCancel.DialogResult = DialogResult.Cancel
        btnCancel.Location = New Point(953, 12)
        btnCancel.Name = "btnCancel"
        btnCancel.Size = New Size(80, 25)
        btnCancel.TabIndex = 2
        btnCancel.Text = "Cancel"
        btnCancel.UseVisualStyleBackColor = True
        ' 
        ' DictionaryPicker_Control
        ' 
        ToolTip1.SetToolTip(tvDirs, "Browse dictionary folders.")
        ToolTip1.SetToolTip(lvFiles, "Select a file from the current dictionary folder.")
        ToolTip1.SetToolTip(txtPath, "Shows the currently selected file path or current folder.")
        ToolTip1.SetToolTip(btnOk, "Accept the current file selection.")
        ToolTip1.SetToolTip(btnCancel, "Cancel without selecting a file.")
        ToolTip1.SetToolTip(ButtonClone, "Clone the selected dictionary file to a loose file on disk.")
        ' 
        ' DictionaryPicker_Control
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        Controls.Add(splitMain)
        Controls.Add(panelBottom)
        Controls.Add(panelTop)
        Name = "DictionaryPicker_Control"
        Size = New Size(1041, 554)
        panelTop.ResumeLayout(False)
        panelTop.PerformLayout()
        splitMain.Panel1.ResumeLayout(False)
        splitMain.Panel2.ResumeLayout(False)
        CType(splitMain, ComponentModel.ISupportInitialize).EndInit()
        splitMain.ResumeLayout(False)
        panelBottom.ResumeLayout(False)
        panelBottom.PerformLayout()
        ResumeLayout(False)
        PerformLayout()

    End Sub

    Friend WithEvents lblRoot As Label
    Friend WithEvents splitMain As SplitContainer
    Friend WithEvents tvDirs As TreeView
    Friend WithEvents lvFiles As ListView
    Friend WithEvents colNombre As ColumnHeader
    Friend WithEvents colExtension As ColumnHeader
    Friend WithEvents colRuta As ColumnHeader
    Friend WithEvents panelBottom As Panel
    Friend WithEvents txtPath As TextBox
    Public WithEvents btnOk As Button
    Public WithEvents btnCancel As Button
    Friend WithEvents ButtonClone As Button
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents panelTop As Panel
    Friend WithEvents chkShowOverrides As CheckBox

End Class
