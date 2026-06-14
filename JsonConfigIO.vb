' IO de configuración JSON compartido: Save/Load con el mismo error-MessageBox que antes copiaban
' Config_App, WM_Config y NPC_Config. Más los lectores genéricos TryGetString/Bool/Int (usados por la
' migración legacy de WM). System.Text.Json se referencia full-qualified (la lib no lo tiene importado).
Public Module JsonConfigIO

    Private ReadOnly SaveOptions As New System.Text.Json.JsonSerializerOptions With {.WriteIndented = True}

    ''' <summary>Serializa instance a filePath (indentado). Ante error muestra el MessageBox
    ''' "Error saving {appLabel}" — mismo texto que tenían los tres configs.</summary>
    Public Sub Save(Of T)(instance As T, filePath As String, appLabel As String)
        Try
            Dim jsonString As String = System.Text.Json.JsonSerializer.Serialize(instance, SaveOptions)
            System.IO.File.WriteAllText(filePath, jsonString)
        Catch ex As Exception
            MessageBox.Show("Error saving " & appLabel & ": " & ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>Si filePath existe, lee + deserializa a T y lo devuelve (puede ser Nothing). Si no existe
    ''' devuelve Nothing. Ante error muestra "Error loading {appLabel}" y devuelve Nothing. El caller decide
    ''' qué hacer con Nothing (reusar el default / migrar / post-procesar).</summary>
    Public Function Load(Of T As Class)(filePath As String, appLabel As String) As T
        Try
            If System.IO.File.Exists(filePath) Then
                Dim jsonString As String = System.IO.File.ReadAllText(filePath)
                Return System.Text.Json.JsonSerializer.Deserialize(Of T)(jsonString)
            End If
        Catch ex As Exception
            MessageBox.Show("Error loading " & appLabel & ": " & ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Return Nothing
    End Function

    Public Function TryGetString(root As System.Text.Json.JsonElement, name As String, ByRef value As String) As Boolean
        Dim el As System.Text.Json.JsonElement
        If root.TryGetProperty(name, el) AndAlso el.ValueKind = System.Text.Json.JsonValueKind.String Then
            value = el.GetString()
            Return True
        End If
        Return False
    End Function

    Public Function TryGetBool(root As System.Text.Json.JsonElement, name As String, ByRef value As Boolean) As Boolean
        Dim el As System.Text.Json.JsonElement
        If root.TryGetProperty(name, el) Then
            If el.ValueKind = System.Text.Json.JsonValueKind.True Then value = True : Return True
            If el.ValueKind = System.Text.Json.JsonValueKind.False Then value = False : Return True
        End If
        Return False
    End Function

    Public Function TryGetInt(root As System.Text.Json.JsonElement, name As String, ByRef value As Integer) As Boolean
        Dim el As System.Text.Json.JsonElement
        If root.TryGetProperty(name, el) AndAlso el.ValueKind = System.Text.Json.JsonValueKind.Number Then
            value = el.GetInt32()
            Return True
        End If
        Return False
    End Function
End Module
