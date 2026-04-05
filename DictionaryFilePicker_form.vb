' Version Uploaded of Fo4Library 3.2.0
Partial Public Class DictionaryFilePicker_Form

    Sub New()
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub
    Public Sub New(keys As List(Of String), rootPrefix As String, allowedExts As HashSet(Of String), initialkey As String)
        InitializeComponent()
        ArgumentNullException.ThrowIfNull(keys)
        ArgumentNullException.ThrowIfNull(allowedExts)
        Me.DictionaryPicker_Control1.Initialize(keys, rootPrefix, allowedExts)
        Me.DictionaryPicker_Control1.Preselect(initialkey)
    End Sub

    Private Sub DictionaryPicker_Control1_OkClicked() Handles DictionaryPicker_Control1.OkClicked
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Sub DictionaryPicker_Control1_CancelClicked() Handles DictionaryPicker_Control1.CancelClicked
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
End Class
