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
            Avisar("Error saving " & appLabel & ": " & ex.Message)
        End Try
    End Sub

    ''' <summary>Reporta un error de IO de config SIN colgar una corrida sin nadie mirando.
    '''
    ''' <para>EL <c>MessageBox</c> ERA INCONDICIONAL, Y ESTO LO LLAMAN LOS CLI. <c>LoadConfig</c> corre
    ''' desde <c>WM_Cli</c>, <c>FO4_FaceTint_CLI</c>, <c>BakeAllRunner</c> y los arneses, y desde que la
    ''' reparacion de opciones GRABA, un config no escribible (instalacion en Program Files, disco lleno,
    ''' archivo de solo lectura) abre un modal en un proceso headless: el batch queda colgado para siempre,
    ''' sin salida y sin log. Un horneado nocturno se pierde entero por un cartel que nadie ve.</para>
    ''' <para>Y <c>Application.MessageLoop</c> SOLO NO ALCANZA — era el arreglo a medias. <c>LoadConfig</c>
    ''' corre en el <c>Main</c> del host, ANTES de <c>Application.Run</c>: en el arranque real de
    ''' NPC_Manager <c>MessageLoop</c> da <b>False</b>, el aviso se iba a <c>stderr</c>, y un ejecutable de
    ''' GUI no tiene consola adjunta ⇒ <b>el mensaje se descartaba entero</b>. Y ese es EXACTAMENTE el
    ''' momento que importa: desde que la migracion de config graba, el escenario que motivo todo esto
    ''' —instalacion en Program Files, archivo de solo lectura— quedaba mudo justo en la GUI.</para>
    ''' <para>EL PREDICADO ES "¿ADONDE PUEDO ESCRIBIR?", no "¿en que estado esta el bucle?". Si el proceso
    ''' NO tiene consola adjunta, la unica salida que existe es un modal — sea antes o despues de
    ''' <c>Application.Run</c>. Si la tiene, es un CLI o un arnes y va a <c>stderr</c>. Los dos casos que el
    ''' arreglo tiene que cubrir salen bien: NPC_Manager arrancando (sin consola ⇒ modal) y un
    ''' <c>--build</c> headless (con consola ⇒ stderr, sin colgar nada).</para></summary>
    ''' <remarks>Se mira TAMBIEN <c>Console.IsErrorRedirected</c>: un orquestador que lanza un CLI con
    ''' <c>CreateNoWindow = True</c> + <c>RedirectStandardError</c> no tiene ventana de consola, pero SI esta
    ''' capturando stderr — y ahi el modal colgaria el batch igual. Si alguien esta escuchando stderr, se le
    ''' escribe a stderr.</remarks>
    Private Sub Avisar(mensaje As String)
        If TieneConsola() OrElse Console.IsErrorRedirected Then
            Console.Error.WriteLine(mensaje)
        Else
            MessageBox.Show(mensaje, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
    End Sub

    <Runtime.InteropServices.DllImport("kernel32.dll")>
    Private Function GetConsoleWindow() As IntPtr
    End Function

    ''' <summary>¿Hay una consola adjunta a este proceso? Es lo que distingue un CLI/arnes de un ejecutable
    ''' de GUI, y a diferencia de <c>Application.MessageLoop</c> NO depende de en que punto del arranque se
    ''' pregunte. Si la P/Invoke fallara, se asume que NO hay consola: equivocarse hacia el modal deja un
    ''' cartel de mas; equivocarse hacia stderr pierde el aviso.</summary>
    Private Function TieneConsola() As Boolean
        Try
            Return GetConsoleWindow() <> IntPtr.Zero
        Catch
            Return False
        End Try
    End Function

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
            Avisar("Error loading " & appLabel & ": " & ex.Message)
        End Try
        Return Nothing
    End Function

    ''' <summary>Abre el archivo como JSON CRUDO para las migraciones que necesitan claves que el tipo
    ''' actual ya no tiene. Devuelve Nothing si no existe o no parsea — el caller decide.
    ''' <para>El <c>JsonDocument</c> es IDisposable: usarlo con <c>Using</c>.</para></summary>
    Public Function TryOpenRaw(filePath As String) As System.Text.Json.JsonDocument
        Try
            If Not System.IO.File.Exists(filePath) Then Return Nothing
            Return System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(filePath))
        Catch
            Return Nothing
        End Try
    End Function

    Public Function TryGetSingle(root As System.Text.Json.JsonElement, name As String, ByRef value As Single) As Boolean
        Dim el As System.Text.Json.JsonElement
        If root.TryGetProperty(name, el) AndAlso el.ValueKind = System.Text.Json.JsonValueKind.Number Then
            value = el.GetSingle()
            Return True
        End If
        Return False
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
