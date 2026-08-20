<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class IconFormBase
    Inherits System.Windows.Forms.Form

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

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(IconFormBase))
        IconsSmall = New System.Windows.Forms.ImageList(components)
        IconsLarge = New System.Windows.Forms.ImageList(components)
        SuspendLayout()
        ' 
        ' IconsSmall
        ' 
        IconsSmall.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit
        IconsSmall.ImageStream = CType(resources.GetObject("IconsSmall.ImageStream"), System.Windows.Forms.ImageListStreamer)
        IconsSmall.TransparentColor = System.Drawing.Color.Transparent
        IconsSmall.Images.SetKeyName(0, "AddGroup")
        IconsSmall.Images.SetKeyName(1, "AgtActionFail")
        IconsSmall.Images.SetKeyName(2, "AgtActionSuccess")
        IconsSmall.Images.SetKeyName(3, "AgtForum")
        IconsSmall.Images.SetKeyName(4, "AgtReload")
        IconsSmall.Images.SetKeyName(5, "AgtUpdateDrivers")
        IconsSmall.Images.SetKeyName(6, "AgtVirusSafe")
        IconsSmall.Images.SetKeyName(7, "Appearance")
        IconsSmall.Images.SetKeyName(8, "ApplicationsDevelopment")
        IconsSmall.Images.SetKeyName(9, "Attach")
        IconsSmall.Images.SetKeyName(10, "ButtonCancel")
        IconsSmall.Images.SetKeyName(11, "Cancel")
        IconsSmall.Images.SetKeyName(12, "CompFile")
        IconsSmall.Images.SetKeyName(13, "Configure")
        IconsSmall.Images.SetKeyName(14, "ConnectCreating")
        IconsSmall.Images.SetKeyName(15, "ConnectNo")
        IconsSmall.Images.SetKeyName(16, "DbAdd")
        IconsSmall.Images.SetKeyName(17, "DbComit")
        IconsSmall.Images.SetKeyName(18, "DbRemove")
        IconsSmall.Images.SetKeyName(19, "DbUpdate")
        IconsSmall.Images.SetKeyName(20, "DownArrow")
        IconsSmall.Images.SetKeyName(21, "Edit")
        IconsSmall.Images.SetKeyName(22, "EditAdd")
        IconsSmall.Images.SetKeyName(23, "EditCopy")
        IconsSmall.Images.SetKeyName(24, "EditCut")
        IconsSmall.Images.SetKeyName(25, "EditRemove")
        IconsSmall.Images.SetKeyName(26, "FileOpen")
        IconsSmall.Images.SetKeyName(27, "FileSave")
        IconsSmall.Images.SetKeyName(28, "FileSaveAs")
        IconsSmall.Images.SetKeyName(29, "Filter")
        IconsSmall.Images.SetKeyName(30, "FolderSentMail")
        IconsSmall.Images.SetKeyName(31, "Gear")
        IconsSmall.Images.SetKeyName(32, "HelpHint")
        IconsSmall.Images.SetKeyName(33, "LayerVisibleOff")
        IconsSmall.Images.SetKeyName(34, "LayerVisibleOn")
        IconsSmall.Images.SetKeyName(35, "LeftArrow")
        IconsSmall.Images.SetKeyName(36, "LeftArrowDouble")
        IconsSmall.Images.SetKeyName(37, "MailFind")
        IconsSmall.Images.SetKeyName(38, "Personal")
        IconsSmall.Images.SetKeyName(39, "RightArrow")
        IconsSmall.Images.SetKeyName(40, "RightArrowDouble")
        IconsSmall.Images.SetKeyName(41, "RunProg")
        IconsSmall.Images.SetKeyName(42, "TabDuplicate")
        IconsSmall.Images.SetKeyName(43, "Thumbnail")
        IconsSmall.Images.SetKeyName(44, "VideoGeneric")
        ' 
        ' IconsLarge
        ' 
        IconsLarge.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit
        IconsLarge.ImageStream = CType(resources.GetObject("IconsLarge.ImageStream"), System.Windows.Forms.ImageListStreamer)
        IconsLarge.TransparentColor = System.Drawing.Color.Transparent
        IconsLarge.Images.SetKeyName(0, "FileClose")
        IconsLarge.Images.SetKeyName(1, "FileNew")
        IconsLarge.Images.SetKeyName(2, "FileOpen")
        IconsLarge.Images.SetKeyName(3, "FileSave")
        IconsLarge.Images.SetKeyName(4, "FileSaveAs")
        IconsLarge.Images.SetKeyName(5, "Filter")
        IconsLarge.Images.SetKeyName(6, "FolderNew")
        IconsLarge.Images.SetKeyName(7, "FolderSentMail")
        IconsLarge.Images.SetKeyName(8, "Forward")
        IconsLarge.Images.SetKeyName(9, "MailDelete")
        IconsLarge.Images.SetKeyName(10, "Reload")
        IconsLarge.Images.SetKeyName(11, "ViewRightP")
        ' 
        ' IconFormBase
        ' 
        Name = "IconFormBase"
        ResumeLayout(False)
    End Sub

    ' Protected, NO Friend. El disenador escribe `Friend WithEvents` por defecto y Friend NO cruza
    ' el limite de ensamblado: desde Wardrobe_Manager o NPC_Manager el campo directamente no existiria.
    ' Si abris este formulario en el disenador, verifica que la propiedad Modifiers de los dos
    ' ImageList siga en Protected.
    Protected WithEvents IconsSmall As System.Windows.Forms.ImageList
    Protected WithEvents IconsLarge As System.Windows.Forms.ImageList
End Class
