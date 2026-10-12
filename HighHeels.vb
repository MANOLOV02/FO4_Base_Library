Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports NiflySharp.Blocks

''' <summary>
''' Tacos altos: cuánto levanta al actor una malla puesta, tal como lo resuelve el extender de cada juego.
''' SÓLO LECTURA. Es la sede única de la ley para las dos apps: Wardrobe Manager la usa para su proyecto (y le
''' suma su sidecar <c>.hht</c> autorado, que es cosa del proyecto y vive en WM) y NPC Manager para bajar el piso
''' del preview según lo que el NPC tiene puesto.
'''  • FO4 (plugin HHS): el registro de <c>Data\F4SE\Plugins\HHS</c> (json + txt) por path del mesh; si no hay
'''    match, el sidecar <c>&lt;mesh&gt;.txt</c> que el mod deja junto al mesh.
'''  • SSE (skee/RaceMenu): el <c>NiFloatExtraData "HH_OFFSET"</c> del NIF, el PRIMERO del recorrido
'''    (SkeletonExtender.cpp: FindExtraData corta en el primer hit y lo escribe como pos Z del nodo "NPC").
''' ⚠ El formato real de HHS sigue SIN VERIFICAR en este workspace (no está su fuente): se conserva la lectura
''' que ya hacía WM, sin cambios.
''' </summary>
Public NotInheritable Class HighHeels
    Private Sub New()
    End Sub

    ''' <summary>Carpeta del registro del plugin HHS (sólo FO4).</summary>
    Public Shared ReadOnly Property HhsPluginFolder As String
        Get
            Return Path.Combine(Config_App.Current.FO4EDataPath, "F4SE\Plugins\HHS")
        End Get
    End Property

    ''' <summary>Lee "clave=valor" de un texto de altura (el .hht de un proyecto de WM o un .txt de HHS). Tolera
    ''' líneas vacías y líneas sin "=". Devuelve False si no hay valor legible, que NO es lo mismo que "vale 0".
    ''' No se exige la clave "Height": el formato real de HHS no está verificado, así que se acepta la primera
    ''' línea clave=valor cuyo valor parsee.</summary>
    Public Shared Function TryParseHeightText(lines As IEnumerable(Of String), ByRef value As Double) As Boolean
        value = 0
        If lines Is Nothing Then Return False
        For Each raw In lines
            If raw Is Nothing Then Continue For
            Dim lin As String = raw.Trim()
            If lin.Length = 0 Then Continue For
            Dim idx As Integer = lin.IndexOf("="c)
            If idx < 0 Then Continue For
            Dim parsed As Double
            If Double.TryParse(lin.Substring(idx + 1).Trim(),
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, parsed) Then
                value = parsed
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary><see cref="TryParseHeightText"/> sobre un archivo de disco. Un archivo inexistente, vacío o
    ''' ilegible da False (antes un .hht de 0 bytes tiraba NullReference en la carga del proyecto).</summary>
    Public Shared Function TryReadHeightFile(archivoName As String, ByRef value As Double) As Boolean
        value = 0
        If String.IsNullOrWhiteSpace(archivoName) Then Return False
        Try
            If File.Exists(archivoName) = False Then Return False
            Return TryParseHeightText(File.ReadLines(archivoName), value)
        Catch
        End Try
        Return False
    End Function

    ''' <summary>HH_OFFSET tal como lo resuelve skee: el PRIMERO del recorrido (la raíz antes que los hijos), NO
    ''' el máximo. 0 si el NIF no lo trae.</summary>
    Public Shared Function HhOffsetOf(nif As Nifcontent_Class_Manolo) As Double
        If IsNothing(nif) Then Return 0
        Try
            Dim root = nif.GetRootNode()
            If Not IsNothing(root) AndAlso Not IsNothing(root.ExtraDataList) Then
                For Each edr In root.ExtraDataList.References
                    Dim ed = TryCast(nif.Blocks(edr.Index), NiFloatExtraData)
                    If Not IsNothing(ed) AndAlso Not IsNothing(ed.Name) AndAlso ed.Name.String = "HH_OFFSET" Then Return CDbl(ed.FloatData)
                Next
            End If
            For Each shap In nif.GetShapes
                If IsNothing(shap) OrElse IsNothing(shap.ExtraDataList) Then Continue For
                For Each edr In shap.ExtraDataList.References
                    Dim ed = TryCast(nif.Blocks(edr.Index), NiFloatExtraData)
                    If Not IsNothing(ed) AndAlso Not IsNothing(ed.Name) AndAlso ed.Name.String = "HH_OFFSET" Then Return CDbl(ed.FloatData)
                Next
            Next
        Catch
        End Try
        Return 0
    End Function

    ''' <summary>Registro del plugin HHS: path de mesh → altura. Sólo FO4; en SSE el offset vive dentro del NIF y
    ''' esta carpeta no se consulta nunca.</summary>
    Public NotInheritable Class HhsRegistry
        Private _keys As New Dictionary(Of String, Double)
        Private _loaded As Boolean

        Private Class HHJsonItem
            <JsonPropertyName("key")>
            Public Property ItemKey As String
            <JsonPropertyName("value")>
            Public Property ItemValue As Double
        End Class

        ''' <summary>Las claves cargadas (copia de sólo lectura).</summary>
        Public ReadOnly Property Keys As IReadOnlyDictionary(Of String, Double)
            Get
                Return _keys
            End Get
        End Property

        ''' <summary>Relee la carpeta del plugin. Se reemplaza el diccionario entero (no se muta el que se está
        ''' leyendo).</summary>
        Public Sub Reload()
            Dim nuevo As New Dictionary(Of String, Double)
            If Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                Lee_HH_Json_y_txt(HhsPluginFolder, nuevo)
            End If
            _keys = nuevo
            _loaded = True
        End Sub

        Public Sub EnsureLoaded()
            If Not _loaded Then Reload()
        End Sub

        Private Shared Sub Lee_HH_Json_y_txt(carpeta As String, destino As Dictionary(Of String, Double))
            If Directory.Exists(carpeta) = False Then Exit Sub

            For Each archivo As String In Directory.GetFiles(carpeta, "*.json")
                Dim contenido As String = File.ReadAllText(archivo)
                Dim data = JsonSerializer.Deserialize(Of Dictionary(Of String, List(Of HHJsonItem)))(contenido)
                If data IsNot Nothing Then
                    For Each kvp In data
                        For Each item In kvp.Value
                            destino(Correct_Path_Separator(item.ItemKey)) = item.ItemValue
                        Next
                    Next
                End If
            Next

            For Each archivo As String In Directory.GetFiles(carpeta, "*.txt")
                Dim lin As String = ""
                Using archivoSTM As New StreamReader(archivo)
                    lin = archivoSTM.ReadLine()
                End Using
                If String.IsNullOrWhiteSpace(lin) Then Continue For
                If lin.Contains("="c) = False Then Continue For
                Dim sep = lin.Split("="c)
                If sep.Length <> 2 Then Continue For
                destino(Path.GetFileNameWithoutExtension(archivo) + ".nif") =
                    Double.Parse(sep(1).Trim(), System.Globalization.CultureInfo.InvariantCulture)
            Next
        End Sub

        ''' <summary>Match ANCLADO a frontera de separador: las keys que salen de los .txt de la carpeta son sólo
        ''' el basename, y un EndsWith crudo le asignaba tacones "body.nif" a "...\femalebody.nif". Gana la key
        ''' más larga (la más específica). 0 si ninguna key matchea.</summary>
        Public Function Match(meshPath As String) As Double
            Dim target As String = meshPath.Correct_Path_Separator
            Dim best As Double = 0
            Dim bestLen As Integer = -1
            For Each kvp In _keys
                Dim k As String = kvp.Key.Correct_Path_Separator
                If k.Length = 0 OrElse k.Length <= bestLen Then Continue For
                If target.EndsWith(k, StringComparison.OrdinalIgnoreCase) = False Then Continue For
                If target.Length > k.Length AndAlso target(target.Length - k.Length - 1) <> "\"c Then Continue For
                bestLen = k.Length
                best = kvp.Value
            Next
            Return best
        End Function
    End Class

    ''' <summary>El registro HHS del proceso (uno por app).</summary>
    Public Shared ReadOnly Property Registry As New HhsRegistry()

    ''' <summary>Altura de tacos de una malla INSTALADA (la que el juego carga), según el juego. Sólo lectura.
    ''' <paramref name="meshKey"/> es la clave del diccionario de archivos (<c>meshes\...\x.nif</c>), que resuelve
    ''' sueltos y BA2 con la prioridad del juego; <paramref name="nif"/> es ese NIF ya cargado.
    '''  • FO4: registro HHS por path; si no hay match, el sidecar <c>&lt;mesh&gt;.txt</c> (mismo orden que el
    '''    import de WM).
    '''  • SSE: el HH_OFFSET del NIF.</summary>
    Public Shared Function ForInstalledMesh(meshKey As String, nif As Nifcontent_Class_Manolo) As Double
        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                If String.IsNullOrEmpty(meshKey) Then Return 0
                Registry.EnsureLoaded()
                Dim v As Double = Registry.Match(meshKey)
                If v <> 0 Then Return v
                Dim txt = FilesDictionary_class.GetBytes(Path.ChangeExtension(meshKey, ".txt"))
                If txt Is Nothing OrElse txt.Length = 0 Then Return 0
                Dim lines = System.Text.Encoding.UTF8.GetString(txt).Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                TryParseHeightText(lines, v)
                Return v
            Case Config_App.Game_Enum.Skyrim
                Return HhOffsetOf(nif)
        End Select
        Return 0
    End Function
End Class
