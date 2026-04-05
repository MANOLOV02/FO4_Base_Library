' Version Uploaded of Fo4Library 3.2.0
<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Public Class DictionaryFilePicker_Form
    Inherits System.Windows.Forms.Form

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
        DictionaryPicker_Control1 = New DictionaryPicker_Control()
        SuspendLayout()
        ' 
        ' DictionaryPicker_Control1
        ' 
        DictionaryPicker_Control1.Dock = DockStyle.Fill
        DictionaryPicker_Control1.Location = New Point(0, 0)
        DictionaryPicker_Control1.Name = "DictionaryPicker_Control1"
        DictionaryPicker_Control1.Size = New Size(1041, 589)
        DictionaryPicker_Control1.TabIndex = 0
        ' 
        ' DictionaryFilePicker_Form
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1041, 589)
        Controls.Add(DictionaryPicker_Control1)
        MaximizeBox = False
        MinimizeBox = False
        MinimumSize = New Size(500, 250)
        Name = "DictionaryFilePicker_Form"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Select file from dictionary"
        ResumeLayout(False)

    End Sub

    Public WithEvents DictionaryPicker_Control1 As DictionaryPicker_Control
End Class
