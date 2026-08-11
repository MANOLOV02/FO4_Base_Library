Imports FO4_Base_Library.Config_App

''' <summary>
''' Editor del rig de luces del previewer. Opera SIEMPRE sobre el rig del JUEGO ACTIVO
''' (<see cref="Config_App.ActiveLights"/> / <see cref="Config_App.SetActiveLights"/>): FO4 y SSE
''' tienen sets separados, igual que las opciones de CharGen. Ver PreviewLightRig.vb.
''' </summary>
Partial Public Class LightRigForm
    Inherits Form

    ''' <summary>
    ''' Raised whenever the user edits any control (slider / NUD / preset / Reset). The new values have
    ''' already been written to the active game's rig in <see cref="Config_App.Current"/>. Hosts (WM,
    ''' NPC_Manager) subscribe to refresh their own preview surface — the form itself owns no
    ''' preview reference.
    ''' </summary>
    Public Event LightsChanged()

    Private ReadOnly _presets As LightRigPreset() = PreviewLightRig.Presets()

    Public Sub New()

        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)

        ' El rig es por juego: decirlo en el título, que si no dos sets distintos parecen el mismo diálogo.
        Text = If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, "Light Rig - Skyrim SE", "Light Rig - Fallout 4")

        CargarPresets()
        CargarValoresIniciales()
        AddHandlers()
    End Sub

    ' ====== Presets ======
    ' El combo tiene los presets + una entrada "Custom" AL FINAL: es la que queda seleccionada cuando el
    ' rig no coincide con ninguno (o sea, apenas el usuario toca un control). No es un set aplicable —
    ' seleccionarla deshabilita Apply — pero deja el combo siempre diciendo qué se está viendo.
    Private Const CustomLabel As String = "Custom"

    Private ReadOnly Property CustomIndex As Integer
        Get
            Return _presets.Length
        End Get
    End Property

    Private Sub CargarPresets()
        cmbPreset.Items.Clear()
        For Each p In _presets
            cmbPreset.Items.Add(p)          ' LightRigPreset.ToString = Name
        Next
        cmbPreset.Items.Add(CustomLabel)
    End Sub

    ''' <summary>Selecciona en el combo el preset que coincide con el rig actual, o "Custom" si el
    ''' usuario editó a mano. El combo nunca queda vacío ni miente sobre lo que se está viendo.</summary>
    Private Sub SincronizarComboConRig(rig As PreviewLightRig)
        Dim idx = Array.FindIndex(_presets, Function(p) RigCoincide(p.Rig, rig))
        If idx < 0 Then idx = CustomIndex
        If cmbPreset.SelectedIndex <> idx Then cmbPreset.SelectedIndex = idx
        ActualizarTooltipPreset()
    End Sub

    ' Comparación con tolerancia, NO igualdad exacta: el viaje por la UI cuantiza los colores a 8 bits
    ' (swatch = System.Drawing.Color), así que aplicar un preset y releerlo devuelve p.ej. 0.58 -> 148/255
    ' = 0.5803922. Con Equals el combo se deseleccionaría solo apenas se aplica el preset. Epsilon = un
    ' paso de 8 bits (1/255) con margen.
    Private Const RigMatchEpsilon As Single = 0.005F

    Private Shared Function CasiIgual(a As Single, b As Single) As Boolean
        Return Math.Abs(a - b) <= RigMatchEpsilon
    End Function

    Private Shared Function ColorCoincide(a As RigColor, b As RigColor) As Boolean
        Return CasiIgual(a.R, b.R) AndAlso CasiIgual(a.G, b.G) AndAlso CasiIgual(a.B, b.B)
    End Function

    Private Shared Function LuzCoincide(a As PreviewLight, b As PreviewLight) As Boolean
        Return CasiIgual(a.Strength, b.Strength) AndAlso ColorCoincide(a.Color, b.Color) AndAlso
               CasiIgual(a.Up, b.Up) AndAlso CasiIgual(a.Down, b.Down) AndAlso
               CasiIgual(a.Left, b.Left) AndAlso CasiIgual(a.Right, b.Right) AndAlso
               CasiIgual(a.Forward, b.Forward) AndAlso CasiIgual(a.Back, b.Back)
    End Function

    Private Shared Function RigCoincide(a As PreviewLightRig, b As PreviewLightRig) As Boolean
        Return LuzCoincide(a.KeyLight, b.KeyLight) AndAlso LuzCoincide(a.FillLeft, b.FillLeft) AndAlso
               LuzCoincide(a.FillRight, b.FillRight) AndAlso LuzCoincide(a.BackLight, b.BackLight) AndAlso
               CasiIgual(a.AmbientIntensity, b.AmbientIntensity) AndAlso
               CasiIgual(a.AmbientGroundLevel, b.AmbientGroundLevel) AndAlso
               ColorCoincide(a.AmbientSkyColor, b.AmbientSkyColor) AndAlso
               ColorCoincide(a.AmbientGroundColor, b.AmbientGroundColor)
    End Function

    Private Sub ActualizarTooltipPreset()
        Dim esPreset = cmbPreset.SelectedIndex >= 0 AndAlso cmbPreset.SelectedIndex < _presets.Length
        Dim descr = If(esPreset,
                       _presets(cmbPreset.SelectedIndex).Description,
                       "The current rig does not match any preset (you edited it by hand). Nothing to apply.")
        ToolTip1.SetToolTip(cmbPreset, descr)
        btnApplyPreset.Enabled = esPreset
    End Sub

    ' ====== Modelo -> UI ======
    Private Sub CargarValoresIniciales()
        Dim rig = Config_App.Current.ActiveLights()

        ' Strengths + swatch de color de cada luz
        tbKey.Value = rig.KeyLight.Strength
        tbFillL.Value = rig.FillLeft.Strength
        tbFillR.Value = rig.FillRight.Strength
        tbBack.Value = rig.BackLight.Strength
        btnKeyColor.BackColor = rig.KeyLight.Color.ToColor()
        btnFillLColor.BackColor = rig.FillLeft.Color.ToColor()
        btnFillRColor.BackColor = rig.FillRight.Color.ToColor()
        btnBackColor.BackColor = rig.BackLight.Color.ToColor()

        ' Grilla de dirección de cada luz (6 multiplicadores relativos a la cámara)
        nudK_U.Value = CDec(rig.KeyLight.Up) : nudK_D.Value = CDec(rig.KeyLight.Down)
        nudK_L.Value = CDec(rig.KeyLight.Left) : nudK_R.Value = CDec(rig.KeyLight.Right)
        nudK_F.Value = CDec(rig.KeyLight.Forward) : nudK_B.Value = CDec(rig.KeyLight.Back)

        nudL_U.Value = CDec(rig.FillLeft.Up) : nudL_D.Value = CDec(rig.FillLeft.Down)
        nudL_L.Value = CDec(rig.FillLeft.Left) : nudL_R.Value = CDec(rig.FillLeft.Right)
        nudL_F.Value = CDec(rig.FillLeft.Forward) : nudL_B.Value = CDec(rig.FillLeft.Back)

        nudR_U.Value = CDec(rig.FillRight.Up) : nudR_D.Value = CDec(rig.FillRight.Down)
        nudR_L.Value = CDec(rig.FillRight.Left) : nudR_R.Value = CDec(rig.FillRight.Right)
        nudR_F.Value = CDec(rig.FillRight.Forward) : nudR_B.Value = CDec(rig.FillRight.Back)

        nudB_U.Value = CDec(rig.BackLight.Up) : nudB_D.Value = CDec(rig.BackLight.Down)
        nudB_L.Value = CDec(rig.BackLight.Left) : nudB_R.Value = CDec(rig.BackLight.Right)
        nudB_F.Value = CDec(rig.BackLight.Forward) : nudB_B.Value = CDec(rig.BackLight.Back)

        ' Ambient = 3 perillas independientes: intensidad, hemisferio (ground level) y tintes.
        tambient.Value = rig.AmbientIntensity
        tGroundLevel.Value = rig.AmbientGroundLevel
        btnAmbSky.BackColor = rig.AmbientSkyColor.ToColor()
        btnAmbGround.BackColor = rig.AmbientGroundColor.ToColor()

        ' Sombras. NO son parte del rig (viven en Setting_PreviewShadows_*), asi que se cargan y se
        ' vuelcan por separado: mezclarlas en VolcarUIenModelo haria que tocar una sombra marque el combo
        ' de presets como "Custom", que es exactamente lo que no significa.
        Dim sh = Config_App.Current.ActiveShadows().Sanitized()
        chkShadows.Checked = sh.Enabled
        CargarCalidadSombra(sh.MapSize)
        tShadowSoft.Value = sh.SoftnessTexels
        tShadowStrength.Value = sh.Intensity
        ActualizarHabilitadoSombras()

        ' Background color picker (handler is wired later in AddHandlers, so this init does not fire it)
        cmbBackground.Rellena()
        cmbBackground.SelectedColor = Config_App.Current.Setting_BackColor

        SincronizarComboConRig(rig)
    End Sub

    Private Sub AddHandlers()
        AddHandler tbKey.ValueChanged, AddressOf SliderChanged
        AddHandler tbFillL.ValueChanged, AddressOf SliderChanged
        AddHandler tbFillR.ValueChanged, AddressOf SliderChanged
        AddHandler tbBack.ValueChanged, AddressOf SliderChanged
        AddHandler tambient.ValueChanged, AddressOf SliderChanged
        AddHandler tGroundLevel.ValueChanged, AddressOf SliderChanged

        Dim nudChanged As EventHandler = Sub(sender, e) VolcarUIenModelo()

        For Each nud In New NumericUpDown() {
            nudK_U, nudK_D, nudK_L, nudK_R, nudK_F, nudK_B,
            nudL_U, nudL_D, nudL_L, nudL_R, nudL_F, nudL_B,
            nudR_U, nudR_D, nudR_L, nudR_R, nudR_F, nudR_B,
            nudB_U, nudB_D, nudB_L, nudB_R, nudB_F, nudB_B}
            AddHandler nud.ValueChanged, nudChanged
        Next

        AddHandler chkShadows.CheckedChanged, Sub(sender, e)
                                                  ActualizarHabilitadoSombras()
                                                  VolcarSombrasEnModelo()
                                              End Sub
        AddHandler cmbShadowQuality.SelectedIndexChanged, Sub(sender, e) VolcarSombrasEnModelo()
        AddHandler tShadowSoft.ValueChanged, Sub(sender, e) VolcarSombrasEnModelo()
        AddHandler tShadowStrength.ValueChanged, Sub(sender, e) VolcarSombrasEnModelo()

        AddHandler cmbBackground.SelectedIndexChanged, AddressOf BackgroundChanged
        AddHandler cmbPreset.SelectedIndexChanged, Sub(sender, e) ActualizarTooltipPreset()

        For Each b In New Button() {btnAmbSky, btnAmbGround, btnKeyColor, btnFillLColor, btnFillRColor, btnBackColor}
            AddHandler b.Click, AddressOf PickColor
        Next
    End Sub

    Private Sub PickColor(sender As Object, e As EventArgs)
        Dim b = CType(sender, Button)
        Using dlg As New ColorDialog() With {.Color = b.BackColor, .FullOpen = True, .AnyColor = True}
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                b.BackColor = dlg.Color
                VolcarUIenModelo()
            End If
        End Using
    End Sub

    Private Sub BackgroundChanged(sender As Object, e As EventArgs)
        If _preventchanges = False Then
            Config_App.Current.Setting_BackColorName = cmbBackground.SelectedColor.Name
            RaiseEvent LightsChanged()
        End If
    End Sub

    Private Sub SliderChanged(sender As Object, e As EventArgs)
        VolcarUIenModelo()
    End Sub

    Private _preventchanges As Boolean = False

    ' ====== Transferencia UI -> Modelo ======
    Private Sub VolcarUIenModelo()
        If _preventchanges Then Return

        Dim rig As New PreviewLightRig With {
            .KeyLight = LeerLuz(tbKey, btnKeyColor, nudK_U, nudK_D, nudK_L, nudK_R, nudK_F, nudK_B),
            .FillLeft = LeerLuz(tbFillL, btnFillLColor, nudL_U, nudL_D, nudL_L, nudL_R, nudL_F, nudL_B),
            .FillRight = LeerLuz(tbFillR, btnFillRColor, nudR_U, nudR_D, nudR_L, nudR_R, nudR_F, nudR_B),
            .BackLight = LeerLuz(tbBack, btnBackColor, nudB_U, nudB_D, nudB_L, nudB_R, nudB_F, nudB_B),
            .AmbientIntensity = CSng(tambient.Value),
            .AmbientGroundLevel = CSng(tGroundLevel.Value),
            .AmbientSkyColor = RigColor.FromColor(btnAmbSky.BackColor),
            .AmbientGroundColor = RigColor.FromColor(btnAmbGround.BackColor)}

        Config_App.Current.SetActiveLights(rig)
        SincronizarComboConRig(rig)
        RaiseEvent LightsChanged()
    End Sub

    ' ====== Sombras (UI <-> Config_App.Setting_PreviewShadows_*) ======

    ''' <summary>Los tres tamanos de mapa que ofrece la UI. El valor es el LADO en texeles; el texto dice
    ''' cuanto cuesta, que es lo que el usuario necesita para elegir.</summary>
    Private Shared ReadOnly ShadowQualities As (Label As String, Size As Integer)() = {
        ("Low (1024)", 1024), ("Medium (2048)", 2048), ("High (4096)", 4096)}

    Private Sub CargarCalidadSombra(mapSize As Integer)
        If cmbShadowQuality.Items.Count = 0 Then
            For Each q In ShadowQualities
                cmbShadowQuality.Items.Add(q.Label)
            Next
        End If
        Dim idx = Array.FindIndex(ShadowQualities, Function(q) q.Size = mapSize)
        ' Un tamano fuera de la lista (editado a mano en el config) NO se pisa en silencio: se elige el
        ' mas cercano para que el combo diga algo cierto, y recien el proximo cambio del usuario lo escribe.
        If idx < 0 Then
            idx = 0
            Dim best = Integer.MaxValue
            For i = 0 To ShadowQualities.Length - 1
                Dim d = Math.Abs(ShadowQualities(i).Size - mapSize)
                If d < best Then best = d : idx = i
            Next
        End If
        cmbShadowQuality.SelectedIndex = idx
    End Sub

    ''' <summary>Con las sombras apagadas, sus perillas no hacen nada: deshabilitarlas evita que alguien
    ''' mueva Softness diez minutos preguntandose por que no pasa nada.</summary>
    Private Sub ActualizarHabilitadoSombras()
        Dim on_ = chkShadows.Checked
        lblShadowQuality.Enabled = on_
        cmbShadowQuality.Enabled = on_
        lblShadowSoft.Enabled = on_
        tShadowSoft.Enabled = on_
        lblShadowStrength.Enabled = on_
        tShadowStrength.Enabled = on_
    End Sub

    Private Sub VolcarSombrasEnModelo()
        If _preventchanges Then Return
        Dim sh = Config_App.Current.ActiveShadows().Sanitized()
        sh.Enabled = chkShadows.Checked
        If cmbShadowQuality.SelectedIndex >= 0 Then sh.MapSize = ShadowQualities(cmbShadowQuality.SelectedIndex).Size
        sh.SoftnessTexels = CSng(tShadowSoft.Value)
        sh.Intensity = CSng(tShadowStrength.Value)
        Config_App.Current.SetActiveShadows(sh)
        RaiseEvent LightsChanged()
    End Sub

    Private Shared Function LeerLuz(strength As TinySliderTextBox, swatch As Button,
                                    up As NumericUpDown, down As NumericUpDown,
                                    left As NumericUpDown, right As NumericUpDown,
                                    forward As NumericUpDown, back As NumericUpDown) As PreviewLight
        Return New PreviewLight(CSng(strength.Value),
                                up:=CSng(up.Value), down:=CSng(down.Value),
                                left:=CSng(left.Value), right:=CSng(right.Value),
                                forward:=CSng(forward.Value), back:=CSng(back.Value)) With {
            .Color = RigColor.FromColor(swatch.BackColor)}
    End Function

    ''' <summary>Carga un rig completo en la UI sin disparar un evento por control (un solo
    ''' <see cref="LightsChanged"/> al final).</summary>
    Private Sub AplicarRig(rig As PreviewLightRig)
        _preventchanges = True
        Config_App.Current.SetActiveLights(rig)
        CargarValoresIniciales()
        _preventchanges = False
        VolcarUIenModelo()
    End Sub

    Private Sub BtnApplyPreset_Click(sender As Object, e As EventArgs) Handles btnApplyPreset.Click
        If cmbPreset.SelectedIndex < 0 OrElse cmbPreset.SelectedIndex >= _presets.Length Then Return   ' "Custom"
        AplicarRig(_presets(cmbPreset.SelectedIndex).Rig)
    End Sub

    Private Sub BtnReset_Click(sender As Object, e As EventArgs) Handles btnReset.Click
        ' Reset = preset Studio + background al default del config (DarkGray). CargarValoresIniciales
        ' recarga el combo de background desde Setting_BackColor; el VolcarUIenModelo final refresca el preview.
        Config_App.Current.Setting_BackColorName = Color.DarkGray.Name
        ' Reset devuelve TODO el estado de iluminacion del preview, sombras incluidas: dejarlas afuera
        ' hacia que "Reset" no reseteara la mitad del dialogo.
        Config_App.Current.SetActiveShadows(PreviewShadowSettings.Defaults())
        AplicarRig(PreviewLightRig.Defaults())
    End Sub
End Class
