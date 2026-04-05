' Version Uploaded of Wardrobe 3.1.0
Imports System.IO
Imports System.Net
Imports NiflySharp.Blocks

Public Class DictionaryPicker_Control
    ' Entradas (ya normalizadas/filtradas antes de crear el form)
    Private _allKeys As List(Of String)
    Private _allKeysSet As HashSet(Of String)
    Private _keysByDirectory As Dictionary(Of String, List(Of String))
    Private _rootPrefix As String
    Private _allowedExts As HashSet(Of String)
    ' Control de repoblado y selección diferida
    Private _suspendPopulate As Boolean = False
    Private _pendingSelectKey As String = Nothing
    Public Event OkClicked()
    Public Event CancelClicked()
    Public Event SelectionChanged(Key As String)
    Public Event Cloned(Key As String)

    Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Skyrim
                RootStr = "(Skyrim SE data)"
            Case Config_App.Game_Enum.Fallout4
                RootStr = "(Fallout 4 data)"
        End Select
    End Sub
    Public Sub Initialize(keys As List(Of String), rootPrefix As String, allowedExts As HashSet(Of String))
        ArgumentNullException.ThrowIfNull(keys)
        ArgumentNullException.ThrowIfNull(allowedExts)

        _allKeys = keys
        _allKeysSet = New HashSet(Of String)(_allKeys, StringComparer.OrdinalIgnoreCase)
        _rootPrefix = rootPrefix
        _allowedExts = allowedExts

        BuildDirectoryIndex()

        btnOk.Enabled = False
        lblRoot.Text = If(String.IsNullOrEmpty(_rootPrefix), "Filtered: (all)", "Filtered: " & _rootPrefix)

        AddHandler tvDirs.AfterSelect, AddressOf TvDirs_AfterSelect
        AddHandler lvFiles.SelectedIndexChanged, AddressOf LvFiles_SelectedIndexChanged
        AddHandler lvFiles.DoubleClick, AddressOf LvFiles_DoubleClick
        AddHandler btnOk.Click, AddressOf BtnOk_Click
        AddHandler panelBottom.Resize, AddressOf PanelBottom_Resize
        AddHandler chkShowOverrides.CheckedChanged, AddressOf ChkShowOverrides_CheckedChanged
        If Me.ParentForm IsNot Nothing Then AddHandler Me.ParentForm.Shown, AddressOf DictionaryFilePickerForm_Shown

        BuildTree()
    End Sub
    Private Sub BuildDirectoryIndex()
        _keysByDirectory = New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

        For Each k In _allKeys
            Dim dirPath = GetDirectoryPath(k)
            Dim bucket As List(Of String) = Nothing

            If _keysByDirectory.TryGetValue(dirPath, bucket) = False Then
                bucket = New List(Of String)
                _keysByDirectory.Add(dirPath, bucket)
            End If

            bucket.Add(k)
        Next

        For Each bucket In _keysByDirectory.Values
            bucket.Sort(StringComparer.OrdinalIgnoreCase)
        Next
    End Sub
    ' ----------------- API pública -----------------

    Public ReadOnly Property SelectedKey As String
        Get
            If lvFiles.SelectedItems.Count = 1 Then
                Return TryCast(lvFiles.SelectedItems(0).Tag, String)
            End If
            Return Nothing
        End Get
    End Property

    ' Preseleccionar archivo si existe en la lista filtrada.
    Public Sub Preselect(initialKey As String)
        ' No robamos el foco al abrir el diálogo (setFocus:=False)
        initialKey = initialKey.Correct_Path_Separator
        _pendingSelectKey = initialKey
        If Not String.IsNullOrEmpty(initialKey) Then
            ' No robamos foco al abrir; se re-aplica en Shown
            SelectFileByKey(initialKey, setFocus:=False)
        End If
    End Sub
    Public Function SelectFileByKey(fullKey As String, Optional setFocus As Boolean = True) As Boolean
        If String.IsNullOrEmpty(fullKey) Then Return False

        Dim norm = fullKey

        ' Buscar key coincidente dentro de las _allKeys (ya normalizadas y filtradas)
        If IsNothing(_allKeysSet) OrElse _allKeysSet.Contains(norm) = False Then Return False
        Dim match = norm

        ' Ir al directorio y poblar
        Dim dirPath = GetDirectoryPath(match)
        Dim node = FindOrExpandNodeByPath(dirPath)
        If node Is Nothing Then Return False

        tvDirs.SelectedNode = node
        PopulateFilesForNode(node)
        Try
            _suspendPopulate = True
            tvDirs.SelectedNode = node
            PopulateFilesForNode(node)
        Finally
            _suspendPopulate = False
        End Try

        ' Seleccionar en la lista
        For Each it As ListViewItem In lvFiles.Items
            Dim k = TryCast(it.Tag, String)
            If k IsNot Nothing AndAlso k.Equals(match, StringComparison.OrdinalIgnoreCase) Then
                it.Selected = True
                it.Focused = True
                txtPath.Text = match
                it.EnsureVisible()
                If setFocus Then lvFiles.Focus()
                btnOk.Enabled = True
                Return True
            End If
        Next

        Return False
    End Function
    ' ----------------- Construcción del árbol -----------------
    Private RootStr As String = "(Fallout 4 data)"
    Private Sub BuildTree()
        tvDirs.BeginUpdate()
        tvDirs.Nodes.Clear()

        Dim rootNode As New TreeNode(RootStr)
        tvDirs.Nodes.Add(rootNode)

        Dim nodeIndex As New Dictionary(Of String, TreeNode)(StringComparer.OrdinalIgnoreCase) From {
        {"", rootNode}
    }

        For Each dirPath In _keysByDirectory.Keys.OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase)
            Dim segments As String() = If(String.IsNullOrEmpty(dirPath), Array.Empty(Of String)(), dirPath.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            Dim currentPath As String = ""
            Dim parentNode As TreeNode = rootNode

            For Each seg In segments
                currentPath = If(currentPath = "", seg, currentPath & "\" & seg)

                Dim childNode As TreeNode = Nothing
                If Not nodeIndex.TryGetValue(currentPath, childNode) Then
                    childNode = New TreeNode(seg)
                    parentNode.Nodes.Add(childNode)
                    nodeIndex(currentPath) = childNode
                End If

                parentNode = childNode
            Next
        Next

        rootNode.Expand()
        tvDirs.EndUpdate()

        tvDirs.SelectedNode = rootNode
        PopulateFilesForNode(rootNode)
    End Sub

    Private Shared Function GetDirectoryPath(fullPath As String) As String
        If String.IsNullOrEmpty(fullPath) Then Return ""
        Dim idx = fullPath.LastIndexOf("\"c)
        If idx <= 0 Then Return "" ' en raíz o sin separador
        Return fullPath.Substring(0, idx)
    End Function

    Private ReadOnly separator As String() = New String() {"\"}
    Private Function FindOrExpandNodeByPath(dirPath As String) As TreeNode
        ' --- Precondiciones del TreeView ---
        If tvDirs Is Nothing Then Return Nothing
        If tvDirs.Nodes Is Nothing OrElse tvDirs.Nodes.Count = 0 Then
            ' Si no hay nodos, no hay nada que encontrar/expandir
            Return Nothing
        End If

        ' --- Normalizar dirPath a "\" y sin barra al final ---
        Dim p As String = If(dirPath, String.Empty).Replace("/"c, "\"c)
        Do
            Dim r = p.Replace("\\", "\")
            If r = p Then Exit Do
            p = r
        Loop
        p = p.TrimStart("\"c).TrimEnd("\"c)

        ' Si no hay ruta, devolvé la raíz existente (TopNode o el primer nodo)
        Dim node As TreeNode = tvDirs.Nodes(0)
        If String.IsNullOrEmpty(p) Then
            Return node
        End If

        ' Segmentar por directorios e ir bajando
        Dim segments = p.Split(separator, StringSplitOptions.RemoveEmptyEntries)

        For Each seg In segments
            If node Is Nothing Then Return Nothing ' seguridad extra

            Dim found As TreeNode = Nothing
            For Each child As TreeNode In node.Nodes
                If child.Text.Equals(seg, StringComparison.OrdinalIgnoreCase) Then
                    found = child
                    Exit For
                End If
            Next

            If found Is Nothing Then
                ' Segmento no encontrado en el árbol actual
                Return Nothing
            End If

            node = found
            node.Expand()
        Next

        Return node
    End Function

    ' ----------------- Poblado de archivos -----------------

    Private Sub PopulateFilesForNode(node As TreeNode)
        lvFiles.BeginUpdate()
        lvFiles.Items.Clear()

        Dim currentPath As String = GetFullPathOfNode(node)
        Dim keysInDirectory As List(Of String) = Nothing

        If _keysByDirectory.TryGetValue(currentPath, keysInDirectory) Then
            For Each k In keysInDirectory
                Dim location As FilesDictionary_class.File_Location = Nothing
                If FilesDictionary_class.Dictionary.TryGetValue(k, location) = False OrElse IsNothing(location) Then
                    Continue For
                End If

                Dim fileName As String = IO.Path.GetFileName(k)
                Dim ext As String = If(location.IsLosseFile, "Loose", IO.Path.GetExtension(location.BA2File))
                Dim lvi As New ListViewItem(fileName)

                lvi.SubItems.Add(ext)
                lvi.SubItems.Add(If(String.IsNullOrEmpty(currentPath), RootStr, currentPath))
                lvi.Tag = k

                If location.IsLosseFile Then
                    lvi.ForeColor = Color.Blue
                Else
                    lvi.ForeColor = Color.Brown
                End If

                lvFiles.Items.Add(lvi)

                ' Show overridden entries below the active one
                If chkShowOverrides.Checked Then
                    Dim overrideds = FilesDictionary_class.GetOverriddenEntries(k)
                    For idx = 0 To overrideds.Length - 1
                        Dim ov = overrideds(idx)
                        Dim ovSource As String = If(ov.IsLosseFile, "Loose", IO.Path.GetExtension(ov.BA2File))
                        Dim ovLvi As New ListViewItem("   " & fileName)
                        ovLvi.SubItems.Add(ovSource)
                        ovLvi.SubItems.Add("overridden #" & (idx + 1).ToString)
                        ovLvi.ForeColor = Color.Gray
                        ovLvi.Tag = Nothing
                        lvFiles.Items.Add(ovLvi)
                    Next
                End If
            Next
        End If

        lvFiles.EndUpdate()

        txtPath.Text = If(String.IsNullOrEmpty(currentPath), RootStr, currentPath)
        btnOk.Enabled = (lvFiles.SelectedItems.Count = 1)
    End Sub

    Private Shared Function GetFullPathOfNode(node As TreeNode) As String
        If node Is Nothing Then Return ""
        If node.Parent Is Nothing Then Return "" ' root virtual
        Dim parts As New Stack(Of String)
        Dim cur = node
        While cur IsNot Nothing AndAlso Not (cur.Parent Is Nothing)
            parts.Push(cur.Text)
            cur = cur.Parent
        End While
        Return String.Join("\", parts.ToArray())
    End Function

    Private Shared Function GetExtension(fileName As String) As String
        If String.IsNullOrEmpty(fileName) Then Return ""
        Dim i = fileName.LastIndexOf("."c)
        If i < 0 Then Return ""
        Return fileName.Substring(i)
    End Function

    ' ----------------- Eventos UI -----------------

    Private Sub TvDirs_AfterSelect(sender As Object, e As TreeViewEventArgs)
        If _suspendPopulate Then Return
        PopulateFilesForNode(e.Node)
    End Sub

    Private Sub LvFiles_SelectedIndexChanged(sender As Object, e As EventArgs)
        If lvFiles.SelectedItems.Count = 1 AndAlso lvFiles.SelectedItems(0).Tag Is Nothing Then
            ' Override item — deselect it
            lvFiles.SelectedItems(0).Selected = False
            Return
        End If
        btnOk.Enabled = (lvFiles.SelectedItems.Count = 1)
        If lvFiles.SelectedItems.Count = 1 Then
            ' Mostrar key completa del archivo seleccionado
            Dim sel = TryCast(lvFiles.SelectedItems(0).Tag, String)
            If sel IsNot Nothing Then
                txtPath.Text = sel
            End If
            RaiseEvent SelectionChanged(sel)
        Else
            ' Sin selección: mostrar carpeta actual
            Dim currentPath As String = GetFullPathOfNode(tvDirs.SelectedNode)
            txtPath.Text = If(String.IsNullOrEmpty(currentPath), RootStr, currentPath)
            RaiseEvent SelectionChanged("")
        End If
    End Sub

    Private Sub LvFiles_DoubleClick(sender As Object, e As EventArgs)
        If lvFiles.SelectedItems.Count = 1 Then
            RaiseEvent OkClicked()
        End If
    End Sub

    Private Sub BtnOk_Click(sender As Object, e As EventArgs)

        If lvFiles.SelectedItems.Count = 1 Then
            RaiseEvent OkClicked()
        End If
    End Sub
    Private Sub PanelBottom_Resize(sender As Object, e As EventArgs)
    End Sub

    Private Sub ChkShowOverrides_CheckedChanged(sender As Object, e As EventArgs)
        If tvDirs.SelectedNode IsNot Nothing Then PopulateFilesForNode(tvDirs.SelectedNode)
    End Sub


    Private Sub DictionaryFilePickerForm_Shown(sender As Object, e As EventArgs)
        If Not String.IsNullOrEmpty(_pendingSelectKey) Then
            ' Reaplicar selección cuando el handle ya existe (asegura que "pinte")
            SelectFileByKey(_pendingSelectKey, setFocus:=False)
        End If
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
        RaiseEvent CancelClicked()
    End Sub

    Private _AllowClone As Boolean = True
    Public Property AllowClone As Boolean
        Get
            Return _AllowClone
        End Get
        Set(value As Boolean)
            _AllowClone = value
            ButtonClone.Enabled = btnOk.Enabled AndAlso _AllowClone
        End Set
    End Property

    Private Sub BtnOk_EnabledChanged(sender As Object, e As EventArgs) Handles btnOk.EnabledChanged
        ButtonClone.Enabled = btnOk.Enabled AndAlso AllowClone
    End Sub

    Private Sub ButtonClone_Click(sender As Object, e As EventArgs) Handles ButtonClone.Click
        Try
            Dim fil = IO.Path.Combine(Config_App.Current.DataPath, SelectedKey)
            Dim dir = IO.Path.GetDirectoryName(fil)
            Dim ext = Path.GetExtension(fil).ToLower
            Dim filtro As String = "Unknwown files (*.*)|*.*"
            If ext <> "" Then filtro = ext.Remove(0, 1).ToUpper + " files (*" + ext + ")|*" + ext
            Dim Creado As Boolean = False
            If IO.Directory.Exists(dir) = False Then
                IO.Directory.CreateDirectory(dir)
                Creado = True
            End If
            Using sd As New SaveFileDialog With {.AddExtension = True, .OverwritePrompt = True, .AddToRecent = False, .DefaultExt = ext, .Filter = filtro, .InitialDirectory = dir, .FileName = IO.Path.GetFileName(fil), .Title = "Clone dictionary file"}
                If sd.ShowDialog = DialogResult.OK Then
                    Dim cloneLoc As FilesDictionary_class.File_Location = Nothing
                    If Not FilesDictionary_class.Dictionary.TryGetValue(SelectedKey, cloneLoc) Then Throw New Exception("Key no longer exists in dictionary")
                    File.WriteAllBytes(sd.FileName, cloneLoc.GetBytes())
                    RaiseEvent Cloned(sd.FileName)
                Else
                    If Creado Then IO.Directory.Delete(dir)
                End If
            End Using
        Catch ex As Exception
            MsgBox("Error cloning archive", vbCritical, "Error")
        End Try
    End Sub
End Class
