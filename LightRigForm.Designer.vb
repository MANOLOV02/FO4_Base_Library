<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class LightRigForm
    Inherits System.Windows.Forms.Form

    'Form reemplaza a Dispose para limpiar la lista de componentes.
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

    'NOTA: el Diseñador de Windows Forms requiere el siguiente procedimiento
    'Se puede modificar usando el Diseñador de Windows Forms.
    'No lo modifique con el editor de código.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        grpKey = New GroupBox()
        lblK_Str = New Label()
        tK_Az = New FO4_Base_Library.TinySliderTextBox()
        tK_El = New FO4_Base_Library.TinySliderTextBox()
        lblK_Az = New Label()
        lblK_El = New Label()
        tbKey = New FO4_Base_Library.TinySliderTextBox()
        grpFillL = New GroupBox()
        lblL_Str = New Label()
        tL_Az = New FO4_Base_Library.TinySliderTextBox()
        tL_El = New FO4_Base_Library.TinySliderTextBox()
        lblL_Az = New Label()
        lblL_El = New Label()
        tbFillL = New FO4_Base_Library.TinySliderTextBox()
        grpFillR = New GroupBox()
        lblR_Str = New Label()
        tR_Az = New FO4_Base_Library.TinySliderTextBox()
        tR_El = New FO4_Base_Library.TinySliderTextBox()
        lblR_Az = New Label()
        lblR_El = New Label()
        tbFillR = New FO4_Base_Library.TinySliderTextBox()
        grpBack = New GroupBox()
        lblB_Str = New Label()
        tB_Az = New FO4_Base_Library.TinySliderTextBox()
        tB_El = New FO4_Base_Library.TinySliderTextBox()
        lblB_Az = New Label()
        lblB_El = New Label()
        tbBack = New FO4_Base_Library.TinySliderTextBox()
        grpPresets = New GroupBox()
        lblPreset = New Label()
        cmbPreset = New ComboBox()
        btnApplyPreset = New Button()
        btnReset = New Button()
        grpAmbient = New GroupBox()
        tambient = New FO4_Base_Library.TinySliderTextBox()
        tGroundLevel = New FO4_Base_Library.TinySliderTextBox()
        lblIntensity = New Label()
        lblGroundLvl = New Label()
        grpBackground = New GroupBox()
        grpShadows = New GroupBox()
        chkLightsFollowCamera = New CheckBox()
        TabsMain = New TabControl()
        TabLights = New TabPage()
        TabRender = New TabPage()
        grpNormals = New GroupBox()
        chkRecalcNormals = New CheckBox()
        chkRepairNaN = New CheckBox()
        chkNormalize = New CheckBox()
        chkDeterministic = New CheckBox()
        chkSmoothSeams = New CheckBox()
        lblSeamAngle = New Label()
        nudSeamAngle = New NumericUpDownCultura()
        grpWeld = New GroupBox()
        chkWelding = New CheckBox()
        rbWeldPosOnly = New RadioButton()
        rbWeldBoth = New RadioButton()
        lblWeldPos = New Label()
        nudWeldPos = New NumericUpDownCultura()
        lblWeldUv = New Label()
        nudWeldUv = New NumericUpDownCultura()
        lblEpsPos = New Label()
        nudEpsPos = New NumericUpDownCultura()
        grpSkin = New GroupBox()
        chkGpuSkinning = New CheckBox()
        chkSingleBone = New CheckBox()
        chkHiddenSegments = New CheckBox()
        grpCamera = New GroupBox()
        chkResetAngles = New CheckBox()
        chkResetZoom = New CheckBox()
        chkFreezeCamera = New CheckBox()
        grpFloor = New GroupBox()
        btnResetRender = New Button()
        chkFloorEnabled = New CheckBox()
        lblFloorSize = New Label()
        nudFloorSize = New NumericUpDownCultura()
        lblFloorStep = New Label()
        nudFloorStep = New NumericUpDownCultura()
        lblFloorColor = New Label()
        cmbFloorColor = New ColorComboBox()

        chkShadows = New CheckBox()
        chkGroundShadow = New CheckBox()
        lblShadowQuality = New Label()
        cmbShadowQuality = New ComboBox()
        lblShadowSoft = New Label()
        tShadowSoft = New FO4_Base_Library.TinySliderTextBox()
        lblShadowStrength = New Label()
        tShadowStrength = New FO4_Base_Library.TinySliderTextBox()
        lblBackground = New Label()
        cmbBackground = New ColorComboBox()
        lblAmbSky = New Label()
        btnAmbSky = New Button()
        lblAmbGround = New Label()
        btnAmbGround = New Button()
        btnKeyColor = New Button()
        btnFillLColor = New Button()
        btnFillRColor = New Button()
        btnBackColor = New Button()
        ToolTip1 = New ToolTip(components)
        grpKey.SuspendLayout()
        grpFillL.SuspendLayout()
        grpFillR.SuspendLayout()
        grpBack.SuspendLayout()
        grpAmbient.SuspendLayout()
        grpBackground.SuspendLayout()
        grpShadows.SuspendLayout()
        TabsMain.SuspendLayout()
        TabLights.SuspendLayout()
        TabRender.SuspendLayout()
        grpNormals.SuspendLayout()
        grpWeld.SuspendLayout()
        grpSkin.SuspendLayout()
        grpCamera.SuspendLayout()
        grpFloor.SuspendLayout()
        grpPresets.SuspendLayout()
        ' ⛔ Begin/EndInit en TODOS los NumericUpDown. Sin el par, cada asignacion de Minimum/Maximum
        ' CLAMPEA el Value ahi mismo: nudWeldPos/nudWeldUv fijan Maximum=1e-3 y despues Minimum=1e-12,
        ' con lo cual el 0 inicial se arrastra a 1e-12, y nudFloorSize/nudFloorStep se clampean contra el
        ' Maximum default de 100 antes de recibir el suyo. Hoy es inocuo porque CargarPestanaRender los
        ' pisa a todos, pero es una bomba: el dia que alguien agregue un `.Value =` o abra el form en el
        ' disenador de VS, el valor se clampea en silencio. Es el mismo error que ClampDec evita del otro
        ' lado. El Designer de VS los genera solo; este archivo se escribio a mano.
        CType(nudSeamAngle, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudWeldPos, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudWeldUv, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudEpsPos, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudFloorSize, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudFloorStep, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        '
        ' ===================================================================================
        ' PESTANA "Lights and shadows" -- REJILLA UNICA. Las dos columnas usan las MISMAS metricas y
        ' terminan en la MISMA y, que es lo unico que hace que un dialogo se lea como una sola cosa:
        '   columna izquierda x=12  ancho 418      columna derecha x=444  ancho 418
        '   dentro de cada caja:  label x=11 w=70  ·  slider x=87 w=264  ·  swatch x=357 w=50
        '   filas a y = 24 / 54 / 84 (paso 30, que es el alto del slider + 2)
        ' Los 4 grupos de luz miden 118 y van cada 130; el grupo Ambient mide 118 IGUAL que ellos para
        ' que las dos columnas arranquen parejas. El gate [ui-layout] del arnes verifica que nada se
        ' pise ni se salga; si alguien mueve una caja sin recalcular el resto, ahi salta.
        '
        ' ⛔ AZIMUT Y ELEVACION SON SLIDERS, no NumericUpDown. Un angulo es una magnitud CONTINUA sobre
        ' un rango cerrado y conocido (0..360 / -90..90): el control que lo dice es una regla, no una
        ' cajita con flechas de 5 en 5. Ademas el NUD obligaba a un mecanismo aparte para no cuantizar
        ' los presets a 0,1 grados (ver AnguloDesdeNud, ya eliminado): TinySliderTextBox guarda Double
        ' sin redondear, asi que el angulo de 5 decimales del preset sobrevive intacto al viaje por la UI.
        ' La elevacion usa FillMode.Center porque su cero es el HORIZONTE, no el extremo: la barra crece
        ' hacia arriba o hacia abajo desde el medio, que es lo que la magnitud significa.
        ' ===================================================================================
        '
        '
        ' grpKey -- Strength / Azimuth / Elevation + la muestra de tinte, que ocupa las tres filas de alto
        '
        grpKey.Controls.Add(lblK_Str)
        grpKey.Controls.Add(tbKey)
        grpKey.Controls.Add(lblK_Az)
        grpKey.Controls.Add(tK_Az)
        grpKey.Controls.Add(lblK_El)
        grpKey.Controls.Add(tK_El)
        grpKey.Controls.Add(btnKeyColor)
        grpKey.Location = New Point(12, 12)
        grpKey.Name = "grpKey"
        grpKey.Size = New Size(418, 118)
        grpKey.TabIndex = 0
        grpKey.TabStop = False
        grpKey.Text = "Key Light"
        '
        lblK_Str.AutoSize = True
        lblK_Str.Location = New Point(11, 30)
        lblK_Str.Name = "lblK_Str"
        lblK_Str.Text = "Strength"
        '
        tbKey.Location = New Point(87, 24)
        tbKey.Size = New Size(264, 28)
        tbKey.Minimum = 0R
        tbKey.Maximum = 2R
        tbKey.DisplayFormat = "0.00%"
        tbKey.InputScale = 0.01R
        tbKey.SmallChange = 0.05R
        tbKey.LargeChange = 0.1R
        tbKey.TickFrequency = 0.25R
        tbKey.ShowTicks = True
        tbKey.Name = "tbKey"
        tbKey.TabIndex = 0
        '
        lblK_Az.AutoSize = True
        lblK_Az.Location = New Point(11, 60)
        lblK_Az.Name = "lblK_Az"
        lblK_Az.Text = "Azimuth"
        '
        tK_Az.Location = New Point(87, 54)
        tK_Az.Size = New Size(264, 28)
        tK_Az.Minimum = 0R
        tK_Az.Maximum = 360R
        tK_Az.DisplayFormat = "0.0°"
        tK_Az.InputScale = 1R
        tK_Az.SmallChange = 1R
        tK_Az.LargeChange = 15R
        tK_Az.TickFrequency = 45R
        tK_Az.ShowTicks = True
        tK_Az.Name = "tK_Az"
        tK_Az.TabIndex = 1
        '
        lblK_El.AutoSize = True
        lblK_El.Location = New Point(11, 90)
        lblK_El.Name = "lblK_El"
        lblK_El.Text = "Elevation"
        '
        tK_El.Location = New Point(87, 84)
        tK_El.Size = New Size(264, 28)
        tK_El.Minimum = -90R
        tK_El.Maximum = 90R
        tK_El.DisplayFormat = "0.0°"
        tK_El.InputScale = 1R
        tK_El.SmallChange = 1R
        tK_El.LargeChange = 15R
        tK_El.TickFrequency = 30R
        tK_El.ShowTicks = True
        tK_El.FillMode = TinySliderFillMode.Center
        tK_El.Name = "tK_El"
        tK_El.TabIndex = 2
        '
        btnKeyColor.Location = New Point(357, 24)
        btnKeyColor.Name = "btnKeyColor"
        btnKeyColor.Size = New Size(50, 88)
        btnKeyColor.TabIndex = 3
        btnKeyColor.UseVisualStyleBackColor = False
        '
        ' grpFillL -- Strength / Azimuth / Elevation + la muestra de tinte, que ocupa las tres filas de alto
        '
        grpFillL.Controls.Add(lblL_Str)
        grpFillL.Controls.Add(tbFillL)
        grpFillL.Controls.Add(lblL_Az)
        grpFillL.Controls.Add(tL_Az)
        grpFillL.Controls.Add(lblL_El)
        grpFillL.Controls.Add(tL_El)
        grpFillL.Controls.Add(btnFillLColor)
        grpFillL.Location = New Point(12, 142)
        grpFillL.Name = "grpFillL"
        grpFillL.Size = New Size(418, 118)
        grpFillL.TabIndex = 1
        grpFillL.TabStop = False
        grpFillL.Text = "Fill Left"
        '
        lblL_Str.AutoSize = True
        lblL_Str.Location = New Point(11, 30)
        lblL_Str.Name = "lblL_Str"
        lblL_Str.Text = "Strength"
        '
        tbFillL.Location = New Point(87, 24)
        tbFillL.Size = New Size(264, 28)
        tbFillL.Minimum = 0R
        tbFillL.Maximum = 2R
        tbFillL.DisplayFormat = "0.00%"
        tbFillL.InputScale = 0.01R
        tbFillL.SmallChange = 0.05R
        tbFillL.LargeChange = 0.1R
        tbFillL.TickFrequency = 0.25R
        tbFillL.ShowTicks = True
        tbFillL.Name = "tbFillL"
        tbFillL.TabIndex = 0
        '
        lblL_Az.AutoSize = True
        lblL_Az.Location = New Point(11, 60)
        lblL_Az.Name = "lblL_Az"
        lblL_Az.Text = "Azimuth"
        '
        tL_Az.Location = New Point(87, 54)
        tL_Az.Size = New Size(264, 28)
        tL_Az.Minimum = 0R
        tL_Az.Maximum = 360R
        tL_Az.DisplayFormat = "0.0°"
        tL_Az.InputScale = 1R
        tL_Az.SmallChange = 1R
        tL_Az.LargeChange = 15R
        tL_Az.TickFrequency = 45R
        tL_Az.ShowTicks = True
        tL_Az.Name = "tL_Az"
        tL_Az.TabIndex = 1
        '
        lblL_El.AutoSize = True
        lblL_El.Location = New Point(11, 90)
        lblL_El.Name = "lblL_El"
        lblL_El.Text = "Elevation"
        '
        tL_El.Location = New Point(87, 84)
        tL_El.Size = New Size(264, 28)
        tL_El.Minimum = -90R
        tL_El.Maximum = 90R
        tL_El.DisplayFormat = "0.0°"
        tL_El.InputScale = 1R
        tL_El.SmallChange = 1R
        tL_El.LargeChange = 15R
        tL_El.TickFrequency = 30R
        tL_El.ShowTicks = True
        tL_El.FillMode = TinySliderFillMode.Center
        tL_El.Name = "tL_El"
        tL_El.TabIndex = 2
        '
        btnFillLColor.Location = New Point(357, 24)
        btnFillLColor.Name = "btnFillLColor"
        btnFillLColor.Size = New Size(50, 88)
        btnFillLColor.TabIndex = 3
        btnFillLColor.UseVisualStyleBackColor = False
        '
        ' grpFillR -- Strength / Azimuth / Elevation + la muestra de tinte, que ocupa las tres filas de alto
        '
        grpFillR.Controls.Add(lblR_Str)
        grpFillR.Controls.Add(tbFillR)
        grpFillR.Controls.Add(lblR_Az)
        grpFillR.Controls.Add(tR_Az)
        grpFillR.Controls.Add(lblR_El)
        grpFillR.Controls.Add(tR_El)
        grpFillR.Controls.Add(btnFillRColor)
        grpFillR.Location = New Point(12, 272)
        grpFillR.Name = "grpFillR"
        grpFillR.Size = New Size(418, 118)
        grpFillR.TabIndex = 2
        grpFillR.TabStop = False
        grpFillR.Text = "Fill Right"
        '
        lblR_Str.AutoSize = True
        lblR_Str.Location = New Point(11, 30)
        lblR_Str.Name = "lblR_Str"
        lblR_Str.Text = "Strength"
        '
        tbFillR.Location = New Point(87, 24)
        tbFillR.Size = New Size(264, 28)
        tbFillR.Minimum = 0R
        tbFillR.Maximum = 2R
        tbFillR.DisplayFormat = "0.00%"
        tbFillR.InputScale = 0.01R
        tbFillR.SmallChange = 0.05R
        tbFillR.LargeChange = 0.1R
        tbFillR.TickFrequency = 0.25R
        tbFillR.ShowTicks = True
        tbFillR.Name = "tbFillR"
        tbFillR.TabIndex = 0
        '
        lblR_Az.AutoSize = True
        lblR_Az.Location = New Point(11, 60)
        lblR_Az.Name = "lblR_Az"
        lblR_Az.Text = "Azimuth"
        '
        tR_Az.Location = New Point(87, 54)
        tR_Az.Size = New Size(264, 28)
        tR_Az.Minimum = 0R
        tR_Az.Maximum = 360R
        tR_Az.DisplayFormat = "0.0°"
        tR_Az.InputScale = 1R
        tR_Az.SmallChange = 1R
        tR_Az.LargeChange = 15R
        tR_Az.TickFrequency = 45R
        tR_Az.ShowTicks = True
        tR_Az.Name = "tR_Az"
        tR_Az.TabIndex = 1
        '
        lblR_El.AutoSize = True
        lblR_El.Location = New Point(11, 90)
        lblR_El.Name = "lblR_El"
        lblR_El.Text = "Elevation"
        '
        tR_El.Location = New Point(87, 84)
        tR_El.Size = New Size(264, 28)
        tR_El.Minimum = -90R
        tR_El.Maximum = 90R
        tR_El.DisplayFormat = "0.0°"
        tR_El.InputScale = 1R
        tR_El.SmallChange = 1R
        tR_El.LargeChange = 15R
        tR_El.TickFrequency = 30R
        tR_El.ShowTicks = True
        tR_El.FillMode = TinySliderFillMode.Center
        tR_El.Name = "tR_El"
        tR_El.TabIndex = 2
        '
        btnFillRColor.Location = New Point(357, 24)
        btnFillRColor.Name = "btnFillRColor"
        btnFillRColor.Size = New Size(50, 88)
        btnFillRColor.TabIndex = 3
        btnFillRColor.UseVisualStyleBackColor = False
        '
        ' grpBack -- Strength / Azimuth / Elevation + la muestra de tinte, que ocupa las tres filas de alto
        '
        grpBack.Controls.Add(lblB_Str)
        grpBack.Controls.Add(tbBack)
        grpBack.Controls.Add(lblB_Az)
        grpBack.Controls.Add(tB_Az)
        grpBack.Controls.Add(lblB_El)
        grpBack.Controls.Add(tB_El)
        grpBack.Controls.Add(btnBackColor)
        grpBack.Location = New Point(12, 402)
        grpBack.Name = "grpBack"
        grpBack.Size = New Size(418, 118)
        grpBack.TabIndex = 3
        grpBack.TabStop = False
        grpBack.Text = "Back Light"
        '
        lblB_Str.AutoSize = True
        lblB_Str.Location = New Point(11, 30)
        lblB_Str.Name = "lblB_Str"
        lblB_Str.Text = "Strength"
        '
        tbBack.Location = New Point(87, 24)
        tbBack.Size = New Size(264, 28)
        tbBack.Minimum = 0R
        tbBack.Maximum = 2R
        tbBack.DisplayFormat = "0.00%"
        tbBack.InputScale = 0.01R
        tbBack.SmallChange = 0.05R
        tbBack.LargeChange = 0.1R
        tbBack.TickFrequency = 0.25R
        tbBack.ShowTicks = True
        tbBack.Name = "tbBack"
        tbBack.TabIndex = 0
        '
        lblB_Az.AutoSize = True
        lblB_Az.Location = New Point(11, 60)
        lblB_Az.Name = "lblB_Az"
        lblB_Az.Text = "Azimuth"
        '
        tB_Az.Location = New Point(87, 54)
        tB_Az.Size = New Size(264, 28)
        tB_Az.Minimum = 0R
        tB_Az.Maximum = 360R
        tB_Az.DisplayFormat = "0.0°"
        tB_Az.InputScale = 1R
        tB_Az.SmallChange = 1R
        tB_Az.LargeChange = 15R
        tB_Az.TickFrequency = 45R
        tB_Az.ShowTicks = True
        tB_Az.Name = "tB_Az"
        tB_Az.TabIndex = 1
        '
        lblB_El.AutoSize = True
        lblB_El.Location = New Point(11, 90)
        lblB_El.Name = "lblB_El"
        lblB_El.Text = "Elevation"
        '
        tB_El.Location = New Point(87, 84)
        tB_El.Size = New Size(264, 28)
        tB_El.Minimum = -90R
        tB_El.Maximum = 90R
        tB_El.DisplayFormat = "0.0°"
        tB_El.InputScale = 1R
        tB_El.SmallChange = 1R
        tB_El.LargeChange = 15R
        tB_El.TickFrequency = 30R
        tB_El.ShowTicks = True
        tB_El.FillMode = TinySliderFillMode.Center
        tB_El.Name = "tB_El"
        tB_El.TabIndex = 2
        '
        btnBackColor.Location = New Point(357, 24)
        btnBackColor.Name = "btnBackColor"
        btnBackColor.Size = New Size(50, 88)
        btnBackColor.TabIndex = 3
        btnBackColor.UseVisualStyleBackColor = False
        ' grpPresets -- fila 1: [Preset v] [Apply] [Reset]; fila 2: la casilla de anclaje del rig.
        ' ⛔ AL AGRANDARLO 25 px HAY QUE MOVER LO DE ABAJO: la columna derecha llegaba a y=482 sobre un tab
        ' de 494, o sea 12 px de margen. Se recuperan comprimiendo los huecos entre grupos (que eran de 19-20)
        ' en vez de estirar el form, que obligaria a re-tunear la otra pestana. Nuevo reparto:
        '   grpAmbient 12..142 · grpBackground 150..218 · grpPresets 226..317 · grpShadows 325..473.
        ' El gate [ui-layout] del arnes verifica que nada se pise; si alguien vuelve a tocar esto, ahi salta.
        grpPresets.Controls.Add(lblPreset)
        grpPresets.Controls.Add(cmbPreset)
        grpPresets.Controls.Add(btnApplyPreset)
        grpPresets.Controls.Add(btnReset)
        grpPresets.Controls.Add(chkLightsFollowCamera)
        grpPresets.Location = New Point(444, 224)
        grpPresets.Name = "grpPresets"
        grpPresets.Size = New Size(418, 110)
        grpPresets.TabIndex = 6
        grpPresets.TabStop = False
        grpPresets.Text = "Rig"
        '
        ' chkLightsFollowCamera
        '
        chkLightsFollowCamera.AutoSize = True
        chkLightsFollowCamera.Location = New Point(11, 68)
        chkLightsFollowCamera.Name = "chkLightsFollowCamera"
        chkLightsFollowCamera.TabIndex = 3
        chkLightsFollowCamera.Text = "Lights follow the camera"
        '
        ' lblPreset
        '
        lblPreset.AutoSize = True
        lblPreset.Location = New Point(11, 32)
        lblPreset.Name = "lblPreset"
        lblPreset.Text = "Set"
        '
        ' cmbPreset
        '
        cmbPreset.DropDownStyle = ComboBoxStyle.DropDownList
        cmbPreset.FormattingEnabled = True
        cmbPreset.Location = New Point(87, 28)
        cmbPreset.Name = "cmbPreset"
        cmbPreset.Size = New Size(190, 23)
        cmbPreset.TabIndex = 0
        '
        ' btnApplyPreset
        '
        btnApplyPreset.Location = New Point(283, 27)
        btnApplyPreset.Name = "btnApplyPreset"
        btnApplyPreset.Size = New Size(58, 25)
        btnApplyPreset.TabIndex = 1
        btnApplyPreset.Text = "Apply"
        btnApplyPreset.UseVisualStyleBackColor = True
        '
        ' btnReset
        '
        btnReset.Location = New Point(349, 27)
        btnReset.Name = "btnReset"
        btnReset.Size = New Size(58, 25)
        btnReset.TabIndex = 2
        btnReset.Text = "Reset"
        btnReset.UseVisualStyleBackColor = True
        '
        ' grpAmbient
        '
        grpAmbient.Controls.Add(lblIntensity)
        grpAmbient.Controls.Add(tambient)
        grpAmbient.Controls.Add(lblGroundLvl)
        grpAmbient.Controls.Add(tGroundLevel)
        grpAmbient.Controls.Add(lblAmbSky)
        grpAmbient.Controls.Add(btnAmbSky)
        grpAmbient.Controls.Add(lblAmbGround)
        grpAmbient.Controls.Add(btnAmbGround)
        grpAmbient.Location = New Point(444, 12)
        grpAmbient.Name = "grpAmbient"
        grpAmbient.Size = New Size(418, 118)
        grpAmbient.TabIndex = 4
        grpAmbient.TabStop = False
        grpAmbient.Text = "Ambient"
        '
        ' tambient
        '
        lblIntensity.AutoSize = True
        lblIntensity.Location = New Point(11, 30)
        lblIntensity.Name = "lblIntensity"
        lblIntensity.Text = "Intensity"
        '
        tambient.Location = New Point(87, 24)
        tambient.Size = New Size(264, 28)
        tambient.Minimum = 0R
        tambient.Maximum = 2R
        tambient.DisplayFormat = "0.00%"
        tambient.InputScale = 0.01R
        tambient.SmallChange = 0.05R
        tambient.LargeChange = 0.1R
        tambient.TickFrequency = 0.25R
        tambient.ShowTicks = True
        tambient.Name = "tambient"
        tambient.TabIndex = 0
        '
        lblGroundLvl.AutoSize = True
        lblGroundLvl.Location = New Point(11, 60)
        lblGroundLvl.Name = "lblGroundLvl"
        lblGroundLvl.Text = "Ground level"
        '
        tGroundLevel.Location = New Point(87, 54)
        tGroundLevel.Size = New Size(264, 28)
        tGroundLevel.Minimum = 0R
        tGroundLevel.Maximum = 1R
        tGroundLevel.DisplayFormat = "0.00%"
        tGroundLevel.InputScale = 0.01R
        tGroundLevel.SmallChange = 0.05R
        tGroundLevel.LargeChange = 0.1R
        tGroundLevel.TickFrequency = 0.25R
        tGroundLevel.ShowTicks = True
        tGroundLevel.Name = "tGroundLevel"
        tGroundLevel.TabIndex = 1
        '
        ' grpShadows -- sombras proyectadas del previewer. Ver ShadowMap.vb.
        '
        chkShadows.AutoSize = True
        chkShadows.Location = New Point(11, 26)
        chkShadows.Name = "chkShadows"
        chkShadows.TabIndex = 0
        chkShadows.Text = "Cast shadows"
        '
        chkGroundShadow.AutoSize = True
        chkGroundShadow.Location = New Point(11, 146)
        chkGroundShadow.Name = "chkGroundShadow"
        chkGroundShadow.TabIndex = 4
        chkGroundShadow.Text = "Shadow on the ground"
        '
        lblShadowQuality.AutoSize = True
        lblShadowQuality.Location = New Point(11, 60)
        lblShadowQuality.Name = "lblShadowQuality"
        lblShadowQuality.Text = "Quality"
        '
        cmbShadowQuality.DropDownStyle = ComboBoxStyle.DropDownList
        cmbShadowQuality.Location = New Point(87, 56)
        cmbShadowQuality.Name = "cmbShadowQuality"
        cmbShadowQuality.Size = New Size(264, 23)
        cmbShadowQuality.TabIndex = 1
        '
        lblShadowSoft.AutoSize = True
        lblShadowSoft.Location = New Point(11, 90)
        lblShadowSoft.Name = "lblShadowSoft"
        lblShadowSoft.Text = "Softness"
        '
        tShadowSoft.Location = New Point(87, 84)
        tShadowSoft.Size = New Size(264, 28)
        tShadowSoft.Minimum = 0R
        tShadowSoft.Maximum = 4R
        tShadowSoft.DisplayFormat = "0.0"
        tShadowSoft.InputScale = 1R
        tShadowSoft.SmallChange = 0.5R
        tShadowSoft.LargeChange = 1R
        tShadowSoft.TickFrequency = 1R
        tShadowSoft.ShowTicks = True
        tShadowSoft.Name = "tShadowSoft"
        tShadowSoft.TabIndex = 2
        '
        lblShadowStrength.AutoSize = True
        lblShadowStrength.Location = New Point(11, 120)
        lblShadowStrength.Name = "lblShadowStrength"
        lblShadowStrength.Text = "Darkness"
        '
        tShadowStrength.Location = New Point(87, 114)
        tShadowStrength.Size = New Size(264, 28)
        tShadowStrength.Minimum = 0R
        tShadowStrength.Maximum = 1R
        tShadowStrength.DisplayFormat = "0.00%"
        tShadowStrength.InputScale = 0.01R
        tShadowStrength.SmallChange = 0.05R
        tShadowStrength.LargeChange = 0.1R
        tShadowStrength.TickFrequency = 0.25R
        tShadowStrength.ShowTicks = True
        tShadowStrength.Name = "tShadowStrength"
        tShadowStrength.TabIndex = 3
        '
        grpShadows.Controls.Add(chkShadows)
        grpShadows.Controls.Add(chkGroundShadow)
        grpShadows.Controls.Add(lblShadowQuality)
        grpShadows.Controls.Add(cmbShadowQuality)
        grpShadows.Controls.Add(lblShadowSoft)
        grpShadows.Controls.Add(tShadowSoft)
        grpShadows.Controls.Add(lblShadowStrength)
        grpShadows.Controls.Add(tShadowStrength)
        grpShadows.Location = New Point(444, 346)
        grpShadows.Name = "grpShadows"
        grpShadows.Size = New Size(418, 174)
        grpShadows.TabIndex = 7
        grpShadows.TabStop = False
        grpShadows.Text = "Shadows"
        '
        ' pestana Rendering (migrada del tab Rendering de Wardrobe Manager)
        '
        chkRecalcNormals.AutoSize = True
        chkRecalcNormals.Location = New Point(11, 24)
        chkRecalcNormals.Name = "chkRecalcNormals"
        chkRecalcNormals.Size = New Size(190, 19)
        chkRecalcNormals.TabIndex = 0
        ' ⛔ NO dice "on load": el flag NO es de carga. Gobierna TRES caminos, y el rotulo viejo invitaba a
        ' creer que tocarlo solo afectaba a la proxima apertura de un NIF:
        '   1) extraccion de geometria  - SkinningHelper.ExtractSkinnedGeometry (`If RecalculateNormals OrElse...`)
        '   2) MORPHS/sliders           - MorphEngine: `soloTangentes = Not (recalculateNormals AndAlso huboCambioDePosicion)`,
        '                                 o sea con el flag apagado un morph que mueve posiciones NO regenera normales
        '   3) el BAKE                  - BuildingForm lo lee para hornear el NIF que se escribe a disco
        ' Ademas el MISMO setting ya se llamaba "Recalculate Normals" en la barra de Wardrobe Manager y
        ' "Recalculate normals" en su editor: tres rotulos para un flag es una invitacion a creer que son tres.
        chkRecalcNormals.Text = "Recalculate normals"
        '
        chkRepairNaN.AutoSize = True
        chkRepairNaN.Location = New Point(11, 48)
        chkRepairNaN.Name = "chkRepairNaN"
        chkRepairNaN.Size = New Size(190, 19)
        chkRepairNaN.TabIndex = 1
        chkRepairNaN.Text = "Repair NaN normals/tangents"
        '
        chkNormalize.AutoSize = True
        chkNormalize.Location = New Point(221, 24)
        chkNormalize.Name = "chkNormalize"
        chkNormalize.Size = New Size(190, 19)
        chkNormalize.TabIndex = 2
        chkNormalize.Text = "Normalize outputs"
        '
        chkDeterministic.AutoSize = True
        chkDeterministic.Location = New Point(221, 48)
        chkDeterministic.Name = "chkDeterministic"
        chkDeterministic.Size = New Size(190, 19)
        chkDeterministic.TabIndex = 3
        chkDeterministic.Text = "Deterministic on collapse"
        '
        chkSmoothSeams.AutoSize = True
        chkSmoothSeams.Location = New Point(11, 76)
        chkSmoothSeams.Name = "chkSmoothSeams"
        chkSmoothSeams.Size = New Size(190, 19)
        chkSmoothSeams.TabIndex = 4
        chkSmoothSeams.Text = "Smooth seam normals"
        '
        lblSeamAngle.AutoSize = True
        lblSeamAngle.Location = New Point(221, 78)
        lblSeamAngle.Name = "lblSeamAngle"
        lblSeamAngle.Size = New Size(70, 15)
        lblSeamAngle.Text = "Seam angle"
        '
        nudSeamAngle.DecimalPlaces = 1
        nudSeamAngle.Location = New Point(300, 74)
        nudSeamAngle.Maximum = New Decimal(New Integer() {180, 0, 0, 0})
        nudSeamAngle.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        nudSeamAngle.Name = "nudSeamAngle"
        nudSeamAngle.Size = New Size(80, 23)
        nudSeamAngle.TabIndex = 5
        nudSeamAngle.TextAlign = HorizontalAlignment.Right
        '
        grpNormals.Controls.Add(chkRecalcNormals)
        grpNormals.Controls.Add(chkRepairNaN)
        grpNormals.Controls.Add(chkNormalize)
        grpNormals.Controls.Add(chkDeterministic)
        grpNormals.Controls.Add(chkSmoothSeams)
        grpNormals.Controls.Add(lblSeamAngle)
        grpNormals.Controls.Add(nudSeamAngle)
        grpNormals.Controls.Add(lblEpsPos)
        grpNormals.Controls.Add(nudEpsPos)
        grpNormals.Location = New Point(12, 12)
        grpNormals.Name = "grpNormals"
        grpNormals.Size = New Size(418, 144)
        grpNormals.TabIndex = 0
        grpNormals.TabStop = False
        grpNormals.Text = "Normals and tangents"
        '
        chkWelding.AutoSize = True
        chkWelding.Location = New Point(11, 24)
        chkWelding.Name = "chkWelding"
        chkWelding.Size = New Size(200, 19)
        chkWelding.TabIndex = 0
        chkWelding.Text = "Weld vertices for normals"
        '
        rbWeldBoth.AutoSize = True
        rbWeldBoth.Location = New Point(11, 50)
        rbWeldBoth.Name = "rbWeldBoth"
        rbWeldBoth.Size = New Size(170, 19)
        rbWeldBoth.TabIndex = 1
        rbWeldBoth.Text = "By position and UVs"
        '
        rbWeldPosOnly.AutoSize = True
        rbWeldPosOnly.Location = New Point(221, 50)
        rbWeldPosOnly.Name = "rbWeldPosOnly"
        rbWeldPosOnly.Size = New Size(170, 19)
        rbWeldPosOnly.TabIndex = 2
        rbWeldPosOnly.Text = "By position only"
        '
        lblWeldPos.AutoSize = True
        lblWeldPos.Location = New Point(11, 82)
        lblWeldPos.Name = "lblWeldPos"
        lblWeldPos.Size = New Size(110, 15)
        lblWeldPos.Text = "Weld pos epsilon"
        '
        nudWeldPos.DecimalPlaces = 12
        nudWeldPos.Location = New Point(130, 78)
        nudWeldPos.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudWeldPos.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        nudWeldPos.Name = "nudWeldPos"
        nudWeldPos.Size = New Size(128, 23)
        nudWeldPos.TabIndex = 3
        nudWeldPos.TextAlign = HorizontalAlignment.Right
        nudWeldPos.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        '
        lblWeldUv.AutoSize = True
        lblWeldUv.Location = New Point(11, 110)
        lblWeldUv.Name = "lblWeldUv"
        lblWeldUv.Size = New Size(110, 15)
        lblWeldUv.Text = "Weld UV epsilon"
        '
        nudWeldUv.DecimalPlaces = 12
        nudWeldUv.Location = New Point(130, 106)
        nudWeldUv.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudWeldUv.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        nudWeldUv.Name = "nudWeldUv"
        nudWeldUv.Size = New Size(128, 23)
        nudWeldUv.TabIndex = 4
        nudWeldUv.TextAlign = HorizontalAlignment.Right
        nudWeldUv.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        '
        lblEpsPos.AutoSize = True
        lblEpsPos.Location = New Point(11, 106)
        lblEpsPos.Name = "lblEpsPos"
        lblEpsPos.Size = New Size(110, 15)
        lblEpsPos.Text = "Position epsilon"
        '
        nudEpsPos.DecimalPlaces = 12
        nudEpsPos.Location = New Point(130, 102)
        nudEpsPos.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudEpsPos.Minimum = New Decimal(0)
        nudEpsPos.Name = "nudEpsPos"
        nudEpsPos.Size = New Size(128, 23)
        nudEpsPos.TabIndex = 5
        nudEpsPos.TextAlign = HorizontalAlignment.Right
        nudEpsPos.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        '
        grpWeld.Controls.Add(chkWelding)
        grpWeld.Controls.Add(rbWeldBoth)
        grpWeld.Controls.Add(rbWeldPosOnly)
        grpWeld.Controls.Add(lblWeldPos)
        grpWeld.Controls.Add(nudWeldPos)
        grpWeld.Controls.Add(lblWeldUv)
        grpWeld.Controls.Add(nudWeldUv)
        grpWeld.Location = New Point(12, 168)
        grpWeld.Name = "grpWeld"
        grpWeld.Size = New Size(418, 148)
        grpWeld.TabIndex = 1
        grpWeld.TabStop = False
        grpWeld.Text = "Welding"
        '
        chkGpuSkinning.AutoSize = True
        chkGpuSkinning.Location = New Point(11, 24)
        chkGpuSkinning.Name = "chkGpuSkinning"
        chkGpuSkinning.Size = New Size(190, 19)
        chkGpuSkinning.TabIndex = 0
        chkGpuSkinning.Text = "GPU skinning"
        '
        chkSingleBone.AutoSize = True
        chkSingleBone.Location = New Point(11, 48)
        chkSingleBone.Name = "chkSingleBone"
        chkSingleBone.Size = New Size(190, 19)
        chkSingleBone.TabIndex = 1
        chkSingleBone.Text = "Single bone skinning"
        '
        chkHiddenSegments.AutoSize = True
        chkHiddenSegments.Location = New Point(11, 72)
        chkHiddenSegments.Name = "chkHiddenSegments"
        chkHiddenSegments.Size = New Size(220, 19)
        chkHiddenSegments.TabIndex = 2
        chkHiddenSegments.Text = "Draw hidden segments"
        '
        grpSkin.Controls.Add(chkGpuSkinning)
        grpSkin.Controls.Add(chkSingleBone)
        grpSkin.Controls.Add(chkHiddenSegments)
        grpSkin.Location = New Point(444, 12)
        grpSkin.Name = "grpSkin"
        grpSkin.Size = New Size(418, 120)
        grpSkin.TabIndex = 2
        grpSkin.TabStop = False
        grpSkin.Text = "Skinning"
        '
        chkResetAngles.AutoSize = True
        chkResetAngles.Location = New Point(11, 24)
        chkResetAngles.Name = "chkResetAngles"
        chkResetAngles.Size = New Size(190, 19)
        chkResetAngles.TabIndex = 0
        chkResetAngles.Text = "Reset rotation on load"
        '
        chkResetZoom.AutoSize = True
        chkResetZoom.Location = New Point(11, 48)
        chkResetZoom.Name = "chkResetZoom"
        chkResetZoom.Size = New Size(190, 19)
        chkResetZoom.TabIndex = 1
        chkResetZoom.Text = "Reset to optimal zoom on load"
        '
        chkFreezeCamera.AutoSize = True
        chkFreezeCamera.Location = New Point(11, 72)
        chkFreezeCamera.Name = "chkFreezeCamera"
        chkFreezeCamera.Size = New Size(380, 19)
        chkFreezeCamera.TabIndex = 2
        chkFreezeCamera.Text = "Completely freeze camera on model change"
        '
        grpCamera.Controls.Add(chkResetAngles)
        grpCamera.Controls.Add(chkResetZoom)
        grpCamera.Controls.Add(chkFreezeCamera)
        grpCamera.Location = New Point(444, 144)
        grpCamera.Name = "grpCamera"
        grpCamera.Size = New Size(418, 120)
        grpCamera.TabIndex = 3
        grpCamera.TabStop = False
        grpCamera.Text = "Camera"
        '
        chkFloorEnabled.AutoSize = True
        chkFloorEnabled.Location = New Point(11, 24)
        chkFloorEnabled.Name = "chkFloorEnabled"
        chkFloorEnabled.Size = New Size(190, 19)
        chkFloorEnabled.TabIndex = 0
        chkFloorEnabled.Text = "Show floor grid"
        '
        lblFloorSize.AutoSize = True
        lblFloorSize.Location = New Point(11, 54)
        lblFloorSize.Name = "lblFloorSize"
        lblFloorSize.Size = New Size(40, 15)
        lblFloorSize.Text = "Size"
        '
        nudFloorSize.DecimalPlaces = 3
        nudFloorSize.Location = New Point(90, 50)
        nudFloorSize.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        nudFloorSize.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        nudFloorSize.Name = "nudFloorSize"
        nudFloorSize.Size = New Size(100, 23)
        nudFloorSize.TabIndex = 1
        nudFloorSize.TextAlign = HorizontalAlignment.Right
        '
        lblFloorStep.AutoSize = True
        lblFloorStep.Location = New Point(210, 54)
        lblFloorStep.Name = "lblFloorStep"
        lblFloorStep.Size = New Size(40, 15)
        lblFloorStep.Text = "Step"
        '
        nudFloorStep.DecimalPlaces = 3
        nudFloorStep.Location = New Point(280, 50)
        nudFloorStep.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        nudFloorStep.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        nudFloorStep.Name = "nudFloorStep"
        nudFloorStep.Size = New Size(100, 23)
        nudFloorStep.TabIndex = 2
        nudFloorStep.TextAlign = HorizontalAlignment.Right
        '
        lblFloorColor.AutoSize = True
        lblFloorColor.Location = New Point(11, 88)
        lblFloorColor.Name = "lblFloorColor"
        lblFloorColor.Size = New Size(70, 15)
        lblFloorColor.Text = "Grid color"
        '
        cmbFloorColor.DropDownStyle = ComboBoxStyle.DropDownList
        cmbFloorColor.Location = New Point(90, 84)
        cmbFloorColor.Name = "cmbFloorColor"
        cmbFloorColor.Size = New Size(290, 24)
        cmbFloorColor.TabIndex = 3
        '
        grpFloor.Controls.Add(chkFloorEnabled)
        grpFloor.Controls.Add(lblFloorSize)
        grpFloor.Controls.Add(nudFloorSize)
        grpFloor.Controls.Add(lblFloorStep)
        grpFloor.Controls.Add(nudFloorStep)
        grpFloor.Controls.Add(lblFloorColor)
        grpFloor.Controls.Add(cmbFloorColor)
        grpFloor.Location = New Point(444, 276)
        grpFloor.Name = "grpFloor"
        grpFloor.Size = New Size(418, 148)
        grpFloor.TabIndex = 4
        grpFloor.TabStop = False
        grpFloor.Text = "Floor grid"
        '
        TabRender.Controls.Add(grpNormals)
        TabRender.Controls.Add(grpWeld)
        TabRender.Controls.Add(grpSkin)
        TabRender.Controls.Add(grpCamera)
        TabRender.Controls.Add(grpFloor)
        TabRender.Controls.Add(btnResetRender)
        TabRender.BackColor = SystemColors.Control
        TabRender.Location = New Point(4, 24)
        TabRender.Name = "TabRender"
        TabRender.Padding = New Padding(3)
        TabRender.Size = New Size(866, 540)
        TabRender.TabIndex = 1
        TabRender.Text = "Rendering"
        TabRender.AutoScroll = True
        '
        ' btnResetRender -- reemplaza el "reset render options" que Wardrobe Manager perdio al migrar
        ' su pestana Rendering. btnReset (el de la pestana de luces) solo repone luces, sombras y fondo:
        ' sin este, romper un epsilon de welding se arreglaba editando el config.json a mano.
        '
        btnResetRender.Location = New Point(12, 330)
        btnResetRender.Name = "btnResetRender"
        btnResetRender.Size = New Size(200, 27)
        btnResetRender.Text = "Reset rendering to defaults"
        btnResetRender.UseVisualStyleBackColor = True
        ToolTip1.SetToolTip(btnResetRender, "Reset every setting on this tab -- normals, welding, skinning, camera and floor grid -- to its default. Lights and shadows are on the other tab and are not touched.")
        '
        TabLights.BackColor = SystemColors.Control
        TabLights.Location = New Point(4, 24)
        TabLights.Name = "TabLights"
        TabLights.Padding = New Padding(3)
        TabLights.Size = New Size(866, 540)
        TabLights.TabIndex = 0
        TabLights.Text = "Lights and shadows"
        ' El AutoScroll del Form es inerte: su unico hijo es el TabControl con Dock=Fill. Con
        ' FormBorderStyle FixedDialog y MaximizeBox=False, un desborde futuro seria INALCANZABLE — ni
        ' scroll ni resize. El margen real es de 12 px. Va en las PAGINAS, que son las que scrollean.
        TabLights.AutoScroll = True
        '
        TabsMain.Controls.Add(TabLights)
        TabsMain.Controls.Add(TabRender)
        TabsMain.Dock = DockStyle.Fill
        TabsMain.Location = New Point(0, 0)
        TabsMain.Name = "TabsMain"
        TabsMain.SelectedIndex = 0
        TabsMain.Size = New Size(874, 568)
        TabsMain.TabIndex = 0
        '
        ' grpBackground
        '
        grpBackground.Controls.Add(lblBackground)
        grpBackground.Controls.Add(cmbBackground)
        grpBackground.Location = New Point(444, 142)
        grpBackground.Name = "grpBackground"
        grpBackground.Size = New Size(418, 68)
        grpBackground.TabIndex = 6
        grpBackground.TabStop = False
        grpBackground.Text = "Background"
        '
        ' lblBackground
        '
        lblBackground.AutoSize = True
        lblBackground.Location = New Point(11, 32)
        lblBackground.Name = "lblBackground"
        lblBackground.Text = "Color"
        '
        ' cmbBackground
        '
        cmbBackground.DropDownStyle = ComboBoxStyle.DropDownList
        cmbBackground.Location = New Point(87, 28)
        cmbBackground.Name = "cmbBackground"
        cmbBackground.Size = New Size(264, 23)
        cmbBackground.TabIndex = 0
        '
        ' Tooltip
        '
        ToolTip1.SetToolTip(tbKey, "Strength of the KEY light: the main one, and the only one that casts shadows.")

        ToolTip1.SetToolTip(tbFillL, "Strength of the left fill light. Fills open up the shadow side; they never cast.")

        ToolTip1.SetToolTip(tbFillR, "Strength of the right fill light. Fills open up the shadow side; they never cast.")

        ToolTip1.SetToolTip(tbBack, "Strength of the back light. Separates the silhouette from the background; it never casts.")

        ToolTip1.SetToolTip(tambient, "Adjust ambient light intensity.")
        ToolTip1.SetToolTip(tGroundLevel, "Brightness of the lower hemisphere as a fraction of the sky, in RADIANCE: 100% is a flat ambient, 0% a black ground. The tint beside it only colours that light, it does not brighten it.")
        ToolTip1.SetToolTip(btnApplyPreset, "Load the selected preset into every control below.")
        ' ⛔ EL TOOLTIP DICE TODO LO QUE EL BOTON HACE. Decia solo "Studio preset AND the default background
        ' color" mientras el handler ademas repone las SOMBRAS a Defaults() — o sea que las PRENDE, porque
        ' Defaults().Enabled es True. Un boton que prende una feature sin anunciarlo es el que despues
        ' aparece como "se me activaron las sombras solas".
        ToolTip1.SetToolTip(btnReset, "Reset the whole lighting tab: Studio preset, default background color, " &
                            "shadow settings back to their defaults (which turns shadows ON), and the light anchoring " &
                            "back to its default.")
        ToolTip1.SetToolTip(chkLightsFollowCamera, "Off: the lights sit in the WORLD. Orbiting turns the " &
                            "character inside the light, so you see the back lit from behind -- this is what the game " &
                            "does, and it is what you want when judging how a piece will look in-game." & vbCrLf &
                            "On (default): the rig turns WITH the camera, like a mesh viewer. The model stays evenly lit from " &
                            "every angle, which is what you want when inspecting geometry." & vbCrLf &
                            "The shadow follows either way: it is cast from the same key direction the shader uses.")
        ToolTip1.SetToolTip(cmbBackground, "Select the preview background color.")
        ToolTip1.SetToolTip(btnAmbSky, "Ambient color when a surface faces UP (world +Z). Engine ambient is normal-dependent.")
        ToolTip1.SetToolTip(btnAmbGround, "Ambient color when a surface faces DOWN (world -Z) -- ground bounce.")
        ToolTip1.SetToolTip(btnKeyColor, "Tint of the key light. Strength is the slider on the left.")
        ToolTip1.SetToolTip(btnFillLColor, "Tint of the left fill light.")
        ToolTip1.SetToolTip(btnFillRColor, "Tint of the right fill light.")
        ToolTip1.SetToolTip(btnBackColor, "Tint of the back light.")
        '
        ' The lights are FIXED TO THE WORLD, so the angles need saying out loud: nothing about them
        ' is obvious from the control, and they used to follow the camera.
        '
        ToolTip1.SetToolTip(tK_Az, "Compass direction the key light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(tK_El, "Height of the key light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        ToolTip1.SetToolTip(tL_Az, "Compass direction the left fill light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(tL_El, "Height of the left fill light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        ToolTip1.SetToolTip(tR_Az, "Compass direction the right fill light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(tR_El, "Height of the right fill light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        ToolTip1.SetToolTip(tB_Az, "Compass direction the back (rim) light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(tB_El, "Height of the back (rim) light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        '
        ' Rendering tab -- migrated from Wardrobe Manager's Settings dialog together with the
        ' controls themselves. The text is the user's own documentation of each knob; dropping it on
        ' the way over would have made the shared dialog strictly worse than the screen it replaced.
        '
        ToolTip1.SetToolTip(chkRecalcNormals, "Recalculate normals and the tangent basis when geometry is loaded, after morphs deform it, and when building a NIF. Without it a morphed mesh keeps the tangent basis of its un-morphed shape, which is the frame the shader reads the normal map in — and the built NIF is written that way too.")
        ToolTip1.SetToolTip(chkRepairNaN, "Repair invalid NaN values found in recalculated tangent-space data.")
        ToolTip1.SetToolTip(chkNormalize, "Normalize recalculated tangent-space vectors.")
        ToolTip1.SetToolTip(chkDeterministic, "When the bitangent's Gram-Schmidt residual falls below single-precision noise, complete the basis from the normal and tangent instead of normalizing rounding noise. Turn off to reproduce BodySlide byte for byte.")
        ToolTip1.SetToolTip(nudEpsPos, "Degenerate-triangle threshold, as a LENGTH in model units: a face whose tangent direction is shorter than this is discarded. 0 (default) matches BodySlide.")
        ToolTip1.SetToolTip(chkWelding, "Temporarily weld matching vertices before recalculating normals.")
        ToolTip1.SetToolTip(rbWeldPosOnly, "Weld vertices using position only.")
        ToolTip1.SetToolTip(rbWeldBoth, "Weld vertices only when both position and UVs match.")
        ToolTip1.SetToolTip(nudWeldPos, "Position epsilon used when welding vertices.")
        ToolTip1.SetToolTip(nudWeldUv, "UV epsilon used when welding vertices.")
        ToolTip1.SetToolTip(chkGpuSkinning, "Toggles GPU Skinning (otherwise CPU Skinning) best performance will depend on your computer specs")
        ToolTip1.SetToolTip(chkSingleBone, "Use single-bone skinning in rendering and preview.")
        ToolTip1.SetToolTip(chkHiddenSegments, "Draw normally-hidden geometry segments (e.g. Pip-Boy forearm variant, occluded segments) in the viewport. WM inspection aid; does not affect exports.")
        ToolTip1.SetToolTip(chkResetAngles, "Reset camera rotation when loading a new project.")
        ToolTip1.SetToolTip(chkResetZoom, "Reset the camera zoom to an optimal distance when loading a new project.")
        ToolTip1.SetToolTip(chkFreezeCamera, "Keep the camera fully frozen when the loaded NIF changes (be sure to uncheck it for different size nifs).")
        ToolTip1.SetToolTip(chkFloorEnabled, "Show the render grid in preview.")
        ToolTip1.SetToolTip(nudFloorSize, "Total grid size.")
        ToolTip1.SetToolTip(nudFloorStep, "Distance between grid lines.")
        ToolTip1.SetToolTip(cmbFloorColor, "Color of the grid lines.")
        '
        lblAmbSky.AutoSize = True
        lblAmbSky.Location = New Point(11, 90)
        lblAmbSky.Name = "lblAmbSky"
        lblAmbSky.Text = "Sky tint"
        '
        btnAmbSky.Location = New Point(87, 86)
        btnAmbSky.Name = "btnAmbSky"
        btnAmbSky.Size = New Size(50, 23)
        btnAmbSky.TabIndex = 2
        btnAmbSky.UseVisualStyleBackColor = False
        '
        lblAmbGround.AutoSize = True
        lblAmbGround.Location = New Point(287, 90)
        lblAmbGround.Name = "lblAmbGround"
        lblAmbGround.Text = "Ground tint"
        '
        btnAmbGround.Location = New Point(357, 86)
        btnAmbGround.Name = "btnAmbGround"
        btnAmbGround.Size = New Size(50, 23)
        btnAmbGround.TabIndex = 3
        btnAmbGround.UseVisualStyleBackColor = False
        '
        ' LightRigForm
        '
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        AutoScroll = True
        Controls.Add(TabsMain)
        ClientSize = New Size(874, 568)
        TabLights.Controls.Add(grpBackground)
        TabLights.Controls.Add(grpAmbient)
        TabLights.Controls.Add(grpShadows)
        TabLights.Controls.Add(grpPresets)
        TabLights.Controls.Add(grpBack)
        TabLights.Controls.Add(grpFillR)
        TabLights.Controls.Add(grpFillL)
        TabLights.Controls.Add(grpKey)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "LightRigForm"
        StartPosition = FormStartPosition.CenterParent
        Text = "Light Rig"
        grpKey.ResumeLayout(False)
        grpKey.PerformLayout()
        grpFillL.ResumeLayout(False)
        grpFillL.PerformLayout()
        grpFillR.ResumeLayout(False)
        grpFillR.PerformLayout()
        grpBack.ResumeLayout(False)
        grpBack.PerformLayout()
        grpShadows.ResumeLayout(False)
        grpShadows.PerformLayout()
        grpNormals.ResumeLayout(False)
        grpNormals.PerformLayout()
        grpWeld.ResumeLayout(False)
        grpWeld.PerformLayout()
        grpSkin.ResumeLayout(False)
        grpSkin.PerformLayout()
        grpCamera.ResumeLayout(False)
        grpCamera.PerformLayout()
        grpFloor.ResumeLayout(False)
        grpFloor.PerformLayout()
        TabLights.ResumeLayout(False)
        TabRender.ResumeLayout(False)
        TabsMain.ResumeLayout(False)
        grpAmbient.ResumeLayout(False)
        grpAmbient.PerformLayout()
        grpBackground.ResumeLayout(False)
        grpBackground.PerformLayout()
        grpPresets.ResumeLayout(False)
        grpPresets.PerformLayout()
        CType(nudSeamAngle, ComponentModel.ISupportInitialize).EndInit()
        CType(nudWeldPos, ComponentModel.ISupportInitialize).EndInit()
        CType(nudWeldUv, ComponentModel.ISupportInitialize).EndInit()
        CType(nudEpsPos, ComponentModel.ISupportInitialize).EndInit()
        CType(nudFloorSize, ComponentModel.ISupportInitialize).EndInit()
        CType(nudFloorStep, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)

    End Sub

    Friend WithEvents grpKey As GroupBox
    Friend WithEvents tK_Az As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents tK_El As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblK_Str As Label
    Friend WithEvents lblK_Az As Label
    Friend WithEvents lblK_El As Label
    Friend WithEvents tbKey As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents grpFillL As GroupBox
    Friend WithEvents tL_Az As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents tL_El As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblL_Str As Label
    Friend WithEvents lblL_Az As Label
    Friend WithEvents lblL_El As Label
    Friend WithEvents tbFillL As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents grpFillR As GroupBox
    Friend WithEvents tR_Az As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents tR_El As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblR_Str As Label
    Friend WithEvents lblR_Az As Label
    Friend WithEvents lblR_El As Label
    Friend WithEvents tbFillR As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents grpBack As GroupBox
    Friend WithEvents tB_Az As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents tB_El As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblB_Str As Label
    Friend WithEvents lblB_Az As Label
    Friend WithEvents lblB_El As Label
    Friend WithEvents tbBack As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents btnReset As Button
    Friend WithEvents grpPresets As GroupBox
    Friend WithEvents lblPreset As Label
    Friend WithEvents cmbPreset As ComboBox
    Friend WithEvents btnApplyPreset As Button
    Friend WithEvents grpAmbient As GroupBox
    Friend WithEvents tambient As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents grpBackground As GroupBox
    Friend WithEvents lblBackground As Label
    Friend WithEvents cmbBackground As ColorComboBox
    Friend WithEvents TabsMain As TabControl
    Friend WithEvents TabLights As TabPage
    Friend WithEvents TabRender As TabPage
    Friend WithEvents grpNormals As GroupBox
    Friend WithEvents chkRecalcNormals As CheckBox
    Friend WithEvents chkRepairNaN As CheckBox
    Friend WithEvents chkNormalize As CheckBox
    Friend WithEvents chkDeterministic As CheckBox
    Friend WithEvents chkSmoothSeams As CheckBox
    Friend WithEvents lblSeamAngle As Label
    Friend WithEvents nudSeamAngle As NumericUpDownCultura
    Friend WithEvents grpWeld As GroupBox
    Friend WithEvents chkWelding As CheckBox
    Friend WithEvents rbWeldPosOnly As RadioButton
    Friend WithEvents rbWeldBoth As RadioButton
    Friend WithEvents lblWeldPos As Label
    Friend WithEvents nudWeldPos As NumericUpDownCultura
    Friend WithEvents lblWeldUv As Label
    Friend WithEvents nudWeldUv As NumericUpDownCultura
    Friend WithEvents lblEpsPos As Label
    Friend WithEvents nudEpsPos As NumericUpDownCultura
    Friend WithEvents grpSkin As GroupBox
    Friend WithEvents chkGpuSkinning As CheckBox
    Friend WithEvents chkSingleBone As CheckBox
    Friend WithEvents chkHiddenSegments As CheckBox
    Friend WithEvents grpCamera As GroupBox
    Friend WithEvents chkResetAngles As CheckBox
    Friend WithEvents chkResetZoom As CheckBox
    Friend WithEvents chkFreezeCamera As CheckBox
    Friend WithEvents grpFloor As GroupBox
    Friend WithEvents btnResetRender As Button
    Friend WithEvents chkFloorEnabled As CheckBox
    Friend WithEvents lblFloorSize As Label
    Friend WithEvents nudFloorSize As NumericUpDownCultura
    Friend WithEvents lblFloorStep As Label
    Friend WithEvents nudFloorStep As NumericUpDownCultura
    Friend WithEvents lblFloorColor As Label
    Friend WithEvents cmbFloorColor As ColorComboBox
    Friend WithEvents grpShadows As GroupBox
    Friend WithEvents chkShadows As CheckBox
    Friend WithEvents chkGroundShadow As CheckBox
    Friend WithEvents chkLightsFollowCamera As CheckBox
    Friend WithEvents lblShadowQuality As Label
    Friend WithEvents cmbShadowQuality As ComboBox
    Friend WithEvents lblShadowSoft As Label
    Friend WithEvents tShadowSoft As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblShadowStrength As Label
    Friend WithEvents tShadowStrength As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblAmbSky As Label
    Friend WithEvents btnAmbSky As Button
    Friend WithEvents lblAmbGround As Label
    Friend WithEvents btnAmbGround As Button
    Friend WithEvents btnKeyColor As Button
    Friend WithEvents btnFillLColor As Button
    Friend WithEvents btnFillRColor As Button
    Friend WithEvents btnBackColor As Button
    Friend WithEvents tGroundLevel As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblIntensity As Label
    Friend WithEvents lblGroundLvl As Label
End Class
