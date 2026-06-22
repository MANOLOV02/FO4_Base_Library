Imports FO4_Base_Library.Config_App
Partial Public Class LightRigForm
    Inherits Form

    ''' <summary>
    ''' Raised whenever the user edits any control (slider / NUD / Reset). The new values have
    ''' already been written to <see cref="Config_App.Current.Setting_Lightrig"/>. Hosts (WM,
    ''' NPC_Manager) subscribe to refresh their own preview surface — the form itself owns no
    ''' preview reference.
    ''' </summary>
    Public Event LightsChanged()

    Public Sub New()

        InitializeComponent()
        'ThemeManager.SetTheme(Config_App.Current.theme, Me)

        CargarValoresIniciales()
        AddHandlers()
    End Sub

    ' ====== Valores recomendados (coinciden con tu rig) ======
    Private Sub CargarValoresIniciales()
        ' Strengths
        tbKey.Value = Config_App.Current.Setting_Lightrig.DirectL.Strength
        tbFillL.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Strength
        tbFillR.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Strength
        tbBack.Value = Config_App.Current.Setting_Lightrig.BackLight.Strength
        tambient.Value = Config_App.Current.Setting_Lightrig.Ambient

        ' Frontal (Key): Forward=+1
        nudK_U.Value = Config_App.Current.Setting_Lightrig.DirectL.Up : nudK_D.Value = Config_App.Current.Setting_Lightrig.DirectL.Down
        nudK_L.Value = Config_App.Current.Setting_Lightrig.DirectL.Left : nudK_R.Value = Config_App.Current.Setting_Lightrig.DirectL.Right
        nudK_F.Value = Config_App.Current.Setting_Lightrig.DirectL.Forward : nudK_B.Value = Config_App.Current.Setting_Lightrig.DirectL.Back

        ' Fill Izquierda: vector NEGADO de (0.4*up -0.6*right -0.7*forward)
        ' w = -v => Up=-0.4, Right=+0.6, Forward=+0.7
        nudL_U.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Up : nudL_D.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Down
        nudL_L.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Left : nudL_R.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Right
        nudL_F.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Forward : nudL_B.Value = Config_App.Current.Setting_Lightrig.FillLight_1.Back

        ' Fill Derecha: vector NEGADO de (0.4*up +0.6*right -0.7*forward)
        ' w = -v => Up=-0.4, Right=-0.6, Forward=+0.7
        nudR_U.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Up : nudR_D.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Down
        nudR_L.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Left : nudR_R.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Right
        nudR_F.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Forward : nudR_B.Value = Config_App.Current.Setting_Lightrig.FillLight_2.Back

        ' Contraluz (Back): 0.6*up -0.2*right +1.0*forward
        nudB_U.Value = Config_App.Current.Setting_Lightrig.BackLight.Up : nudB_D.Value = Config_App.Current.Setting_Lightrig.BackLight.Down
        nudB_L.Value = Config_App.Current.Setting_Lightrig.BackLight.Left : nudB_R.Value = Config_App.Current.Setting_Lightrig.BackLight.Right
        nudB_F.Value = Config_App.Current.Setting_Lightrig.BackLight.Forward : nudB_B.Value = Config_App.Current.Setting_Lightrig.BackLight.Back

        ' Background color picker (handler is wired later in AddHandlers, so this init does not fire it)
        cmbBackground.Rellena()
        cmbBackground.SelectedColor = Config_App.Current.Setting_BackColor

        ' Ambient = 3 perillas independientes: intensidad (tambient), hemisferio (tGroundLevel) y tinte
        ' (swatches Sky/Ground). NormalizeAmbient migra configs viejos (deriva el groundLevel del brillo del
        ' color ground del modelo intermedio; tintes a blanco) y se persiste para que el resto del app lo vea.
        ' Tints (0,0,0)=unset -> blanco. Cada swatch de luz vive ahora al lado del slider de su grupo.
        Dim rig = Config_App.Current.Setting_Lightrig
        Config_App.NormalizeAmbient(rig)
        Config_App.Current.Setting_Lightrig = rig
        tGroundLevel.Value = rig.AmbientGroundLevel
        btnAmbSky.BackColor = VecToCol(rig.AmbientSky, Color.White)
        btnAmbGround.BackColor = VecToCol(rig.AmbientGround, Color.White)
        btnKeyColor.BackColor = VecToCol(rig.DirectL.Tint, Color.White)
        btnFillLColor.BackColor = VecToCol(rig.FillLight_1.Tint, Color.White)
        btnFillRColor.BackColor = VecToCol(rig.FillLight_2.Tint, Color.White)
        btnBackColor.BackColor = VecToCol(rig.BackLight.Tint, Color.White)

        VolcarUIenModelo()
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

        AddHandler cmbBackground.SelectedIndexChanged, AddressOf BackgroundChanged

        For Each b In New Button() {btnAmbSky, btnAmbGround, btnKeyColor, btnFillLColor, btnFillRColor, btnBackColor}
            AddHandler b.Click, AddressOf PickColor
        Next
    End Sub

    ' Color <-> Vector3 (0..1). Vector3(0,0,0) se muestra con el fallback (legacy/unset).
    Private Shared Function ColToVec(c As Color) As System.Numerics.Vector3
        Return New System.Numerics.Vector3(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F)
    End Function
    Private Shared Function VecToCol(v As System.Numerics.Vector3, fallback As Color) As Color
        If v.X = 0 AndAlso v.Y = 0 AndAlso v.Z = 0 Then Return fallback
        Dim Clamp = Function(f As Single) Math.Max(0, Math.Min(255, CInt(f * 255.0F)))
        Return Color.FromArgb(255, Clamp(v.X), Clamp(v.Y), Clamp(v.Z))
    End Function

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
        If _PreventChanges = False Then
            Dim Lrig = New LightsRig_struct With {.Ambient = CSng(tambient.Value),
            .AmbientGroundLevel = CSng(tGroundLevel.Value),
            .AmbientConfigured = True,
            .AmbientSky = ColToVec(btnAmbSky.BackColor),
            .AmbientGround = ColToVec(btnAmbGround.BackColor),
            .DirectL = New LightData_struct With {.Strength = CSng(tbKey.Value), .Tint = ColToVec(btnKeyColor.BackColor), .Left = CSng(nudK_L.Value), .Right = CSng(nudK_R.Value), .Back = CSng(nudK_B.Value), .Down = CSng(nudK_D.Value), .Forward = CSng(nudK_F.Value), .Up = CSng(nudK_U.Value)},
            .FillLight_1 = New LightData_struct With {.Strength = CSng(tbFillL.Value), .Tint = ColToVec(btnFillLColor.BackColor), .Left = CSng(nudL_L.Value), .Right = CSng(nudL_R.Value), .Back = CSng(nudL_B.Value), .Down = CSng(nudL_D.Value), .Forward = CSng(nudL_F.Value), .Up = CSng(nudL_U.Value)},
            .FillLight_2 = New LightData_struct With {.Strength = CSng(tbFillR.Value), .Tint = ColToVec(btnFillRColor.BackColor), .Left = CSng(nudR_L.Value), .Right = CSng(nudR_R.Value), .Back = CSng(nudR_B.Value), .Down = CSng(nudR_D.Value), .Forward = CSng(nudR_F.Value), .Up = CSng(nudR_U.Value)},
            .BackLight = New LightData_struct With {.Strength = CSng(tbBack.Value), .Tint = ColToVec(btnBackColor.BackColor), .Left = CSng(nudB_L.Value), .Right = CSng(nudB_R.Value), .Back = CSng(nudB_B.Value), .Down = CSng(nudB_D.Value), .Forward = CSng(nudB_F.Value), .Up = CSng(nudB_U.Value)}}
            Config_App.Current.Setting_Lightrig = Lrig
            RaiseEvent LightsChanged()
        End If
    End Sub

    Private Sub BtnReset_Click(sender As Object, e As EventArgs) Handles btnReset.Click
        _preventchanges = True
        Config_App.Current.Setting_Lightrig = Default_Lights()
        ' Reset también el background al default del config (DarkGray). CargarValoresIniciales recarga
        ' el combo desde Setting_BackColor; el VolcarUIenModelo final (LightsChanged) refresca el preview.
        Config_App.Current.Setting_BackColorName = Color.DarkGray.Name
        CargarValoresIniciales()
        _preventchanges = False
        VolcarUIenModelo()
    End Sub
End Class
