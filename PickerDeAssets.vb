''' <summary>⛔⛔ LA SEDE ÚNICA de «elegir un asset del juego y dejarlo en una caja de texto».
'''
''' <para>Un archivo del juego se elige con el PICKER DEL DICCIONARIO, que ve los sueltos Y los
''' BA2/BSA, nunca con un diálogo del sistema — que ve el disco y nada más. Eso ya estaba escrito en
''' varios formularios; lo que NO estaba es en un solo lugar, y por eso había cuatro políticas
''' distintas para el mismo par de decisiones (cómo se SIEMBRA el picker y qué se GUARDA al volver):
''' una sembraba a mano, otra no sembraba nada —y por eso su picker no preselecciona NUNCA—, otra
''' guardaba con el prefijo y otra sin él.</para>
'''
''' <para>Las dos decisiones, acá y una sola vez:</para>
''' <list type="number">
''' <item><b>La semilla se normaliza con la MISMA regla que el render</b>
''' (<see cref="FO4UnifiedMaterial_Class.CorrectGameRelativePath"/>), que resuelve las tres formas en
''' que un valor puede venir escrito: ruta absoluta con la raíz adentro, relativa con barra inicial, y
''' relativa pelada. Una versión propia y más débil hace que la caja y el render discrepen sobre el
''' mismo string.</item>
''' <item><b>Se devuelve la clave SIN el prefijo raíz</b>, que es como los records guardan el valor
''' (medido sobre el corpus de Fallout 4: 0 de 654 <c>TX00</c> traen <c>textures\</c> y 0 de 186
''' <c>MNAM</c> traen <c>materials\</c>). El que necesite la clave completa —el <c>MSWP</c>, por
''' ejemplo, que la guarda con prefijo— usa <see cref="ElegirClave"/>.</item>
''' </list>
'''
''' <para>⛔ Y la guarda del diccionario VACÍO, que no es cosmética: el diccionario se llena en una
''' tarea de fondo, así que un botón apretado temprano abría un árbol vacío sin decir por qué. La
''' forma guardada ya existía en <c>SseCatalogs.PickSkinTexture</c> y no estaba copiada en los demás
''' selectores.</para></summary>
Public Module PickerDeAssets

    ''' <summary>Abre el picker sembrado con lo que la caja tenga y, si el usuario elige, deja ahí la
    ''' clave SIN el prefijo raíz. Devuelve True sólo si cambió la caja.</summary>
    Public Function ElegirEnCaja(owner As IWin32Window, caja As TextBox,
                                 cfg As FilesDictionary_class.DictionaryFilePickerConfig) As Boolean
        If caja Is Nothing OrElse cfg Is Nothing Then Return False
        Dim elegido = ElegirClave(owner, caja.Text, cfg)
        If elegido Is Nothing Then Return False
        caja.Text = elegido.StripPrefix(cfg.RootPrefix)
        Return True
    End Function

    ''' <summary>El picker crudo: devuelve la CLAVE COMPLETA del diccionario (con el prefijo raíz), o
    ''' <c>Nothing</c> si el usuario canceló, si no eligió nada o si no hay nada que ofrecer.</summary>
    Public Function ElegirClave(owner As IWin32Window, valorActual As String,
                                cfg As FilesDictionary_class.DictionaryFilePickerConfig) As String
        If cfg Is Nothing Then Return Nothing
        Dim exts = cfg.AllowedExtensions
        Dim keys As List(Of String)
        Try
            keys = FilesDictionary_class.GetFilteredKeys(cfg)
        Catch ex As Exception
            Logger.LogLazy(Function() $"[PICKER] no se pudo leer el diccionario para '{cfg.RootPrefix}': {ex.GetType().Name}: {ex.Message}")
            keys = Nothing
        End Try
        If keys Is Nothing OrElse keys.Count = 0 Then
            ' El diccionario todavía no está (se llena en una tarea de fondo) o esa raíz no tiene nada.
            ' Se dice: un árbol vacío sin explicación parece un selector roto.
            MessageBox.Show(owner,
                            $"No files under '{cfg.RootPrefix}' are indexed yet." & vbCrLf & vbCrLf &
                            "The file dictionary (loose files + BA2/BSA) is still being built, or the game's Data " &
                            "folder is not set. Try again once it finishes.",
                            "Pick from the game's files", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return Nothing
        End If

        ' La semilla va con el prefijo porque las claves del diccionario lo llevan; sin esto el picker
        ' abre en la raíz del árbol en vez de la carpeta del archivo que ya está puesto.
        Dim semilla As String = ""
        Dim actual = If(valorActual, "").Trim()
        If actual.Length > 0 Then
            semilla = FO4UnifiedMaterial_Class.CorrectGameRelativePath(actual, cfg.RootPrefix)
        End If

        Using dlg As New DictionaryFilePicker_Form(keys, cfg.RootPrefix, exts, semilla)
            If dlg.ShowDialog(owner) <> DialogResult.OK Then Return Nothing
            Dim sel = dlg.DictionaryPicker_Control1.SelectedKey
            If String.IsNullOrEmpty(sel) Then Return Nothing
            Return sel
        End Using
    End Function

End Module
