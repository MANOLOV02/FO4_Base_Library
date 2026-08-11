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
        lblK_Tint = New Label()
        nudK_Az = New NumericUpDown()
        nudK_El = New NumericUpDown()
        lblK_Az = New Label()
        lblK_El = New Label()
        tbKey = New FO4_Base_Library.TinySliderTextBox()
        grpFillL = New GroupBox()
        lblL_Str = New Label()
        lblL_Tint = New Label()
        nudL_Az = New NumericUpDown()
        nudL_El = New NumericUpDown()
        lblL_Az = New Label()
        lblL_El = New Label()
        tbFillL = New FO4_Base_Library.TinySliderTextBox()
        grpFillR = New GroupBox()
        lblR_Str = New Label()
        lblR_Tint = New Label()
        nudR_Az = New NumericUpDown()
        nudR_El = New NumericUpDown()
        lblR_Az = New Label()
        lblR_El = New Label()
        tbFillR = New FO4_Base_Library.TinySliderTextBox()
        grpBack = New GroupBox()
        lblB_Str = New Label()
        lblB_Tint = New Label()
        nudB_Az = New NumericUpDown()
        nudB_El = New NumericUpDown()
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
        nudSeamAngle = New NumericUpDown()
        grpWeld = New GroupBox()
        chkWelding = New CheckBox()
        rbWeldPosOnly = New RadioButton()
        rbWeldBoth = New RadioButton()
        lblWeldPos = New Label()
        nudWeldPos = New NumericUpDown()
        lblWeldUv = New Label()
        nudWeldUv = New NumericUpDown()
        lblEpsPos = New Label()
        nudEpsPos = New NumericUpDown()
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
        nudFloorSize = New NumericUpDown()
        lblFloorStep = New Label()
        nudFloorStep = New NumericUpDown()
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
        CType(nudK_Az, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudK_El, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_Az, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_El, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_Az, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_El, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_Az, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_El, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudSeamAngle, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudWeldPos, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudWeldUv, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudEpsPos, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudFloorSize, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudFloorStep, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        '
        ' grpKey
        '
        grpKey.Controls.Add(lblK_Str)
        grpKey.Controls.Add(tbKey)
        grpKey.Controls.Add(lblK_Tint)
        grpKey.Controls.Add(btnKeyColor)
        grpKey.Controls.Add(nudK_Az)
        grpKey.Controls.Add(nudK_El)
        grpKey.Controls.Add(lblK_Az)
        grpKey.Controls.Add(lblK_El)
        grpKey.Location = New Point(12, 12)
        grpKey.Name = "grpKey"
        grpKey.Size = New Size(418, 110)
        grpKey.TabIndex = 0
        grpKey.TabStop = False
        '
        ' direccion de mundo de la luz: azimut + elevacion (reemplazo de la grilla de 6 NUD)
        '
        lblK_Str.AutoSize = True
        lblK_Str.Location = New Point(11, 34)
        lblK_Str.Name = "lblK_Str"
        lblK_Str.Text = "Strength"
        '
        lblK_Tint.AutoSize = True
        lblK_Tint.Location = New Point(311, 36)
        lblK_Tint.Name = "lblK_Tint"
        lblK_Tint.Text = "Tint"
        '
        lblK_Az.AutoSize = True
        lblK_Az.Location = New Point(11, 78)
        lblK_Az.Name = "lblK_Az"
        lblK_Az.Size = New Size(54, 15)
        lblK_Az.Text = "Azimuth"
        '
        nudK_Az.DecimalPlaces = 1
        nudK_Az.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudK_Az.Location = New Point(118, 74)
        nudK_Az.Maximum = New Decimal(New Integer() {360, 0, 0, 0})
        nudK_Az.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        nudK_Az.Name = "nudK_Az"
        nudK_Az.Size = New Size(90, 23)
        nudK_Az.TabIndex = 2
        nudK_Az.TextAlign = HorizontalAlignment.Right
        '
        lblK_El.AutoSize = True
        lblK_El.Location = New Point(208, 78)
        lblK_El.Name = "lblK_El"
        lblK_El.Size = New Size(60, 15)
        lblK_El.Text = "Elevation"
        '
        nudK_El.DecimalPlaces = 1
        nudK_El.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudK_El.Location = New Point(315, 74)
        nudK_El.Maximum = New Decimal(New Integer() {90, 0, 0, 0})
        nudK_El.Minimum = New Decimal(New Integer() {90, 0, 0, -2147483648})
        nudK_El.Name = "nudK_El"
        nudK_El.Size = New Size(90, 23)
        nudK_El.TabIndex = 3
        nudK_El.TextAlign = HorizontalAlignment.Right
        grpKey.Text = "Key Light"
        '
        ' tbKey
        '
        tbKey.Location = New Point(75, 30)
        tbKey.Minimum = 0R
        tbKey.Maximum = 2R
        tbKey.DisplayFormat = "0.00%"
        tbKey.InputScale = 0.01R
        tbKey.SmallChange = 0.05R
        tbKey.LargeChange = 0.1R
        tbKey.TickFrequency = 0.1R
        tbKey.ShowTicks = True
        tbKey.Name = "tbKey"
        tbKey.Size = New Size(230, 28)
        tbKey.TabIndex = 0
        '
        ' grpFillL
        '
        grpFillL.Controls.Add(lblL_Str)
        grpFillL.Controls.Add(tbFillL)
        grpFillL.Controls.Add(lblL_Tint)
        grpFillL.Controls.Add(btnFillLColor)
        grpFillL.Controls.Add(nudL_Az)
        grpFillL.Controls.Add(nudL_El)
        grpFillL.Controls.Add(lblL_Az)
        grpFillL.Controls.Add(lblL_El)
        grpFillL.Location = New Point(12, 132)
        grpFillL.Name = "grpFillL"
        grpFillL.Size = New Size(418, 110)
        grpFillL.TabIndex = 1
        grpFillL.TabStop = False
        '
        ' direccion de mundo de la luz: azimut + elevacion (reemplazo de la grilla de 6 NUD)
        '
        lblL_Str.AutoSize = True
        lblL_Str.Location = New Point(11, 34)
        lblL_Str.Name = "lblL_Str"
        lblL_Str.Text = "Strength"
        '
        lblL_Tint.AutoSize = True
        lblL_Tint.Location = New Point(311, 36)
        lblL_Tint.Name = "lblL_Tint"
        lblL_Tint.Text = "Tint"
        '
        lblL_Az.AutoSize = True
        lblL_Az.Location = New Point(11, 78)
        lblL_Az.Name = "lblL_Az"
        lblL_Az.Size = New Size(54, 15)
        lblL_Az.Text = "Azimuth"
        '
        nudL_Az.DecimalPlaces = 1
        nudL_Az.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudL_Az.Location = New Point(118, 74)
        nudL_Az.Maximum = New Decimal(New Integer() {360, 0, 0, 0})
        nudL_Az.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        nudL_Az.Name = "nudL_Az"
        nudL_Az.Size = New Size(90, 23)
        nudL_Az.TabIndex = 2
        nudL_Az.TextAlign = HorizontalAlignment.Right
        '
        lblL_El.AutoSize = True
        lblL_El.Location = New Point(208, 78)
        lblL_El.Name = "lblL_El"
        lblL_El.Size = New Size(60, 15)
        lblL_El.Text = "Elevation"
        '
        nudL_El.DecimalPlaces = 1
        nudL_El.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudL_El.Location = New Point(315, 74)
        nudL_El.Maximum = New Decimal(New Integer() {90, 0, 0, 0})
        nudL_El.Minimum = New Decimal(New Integer() {90, 0, 0, -2147483648})
        nudL_El.Name = "nudL_El"
        nudL_El.Size = New Size(90, 23)
        nudL_El.TabIndex = 3
        nudL_El.TextAlign = HorizontalAlignment.Right
        grpFillL.Text = "Fill Left"
        '
        ' tbFillL
        '
        tbFillL.Location = New Point(75, 30)
        tbFillL.Minimum = 0R
        tbFillL.Maximum = 2R
        tbFillL.DisplayFormat = "0.00%"
        tbFillL.InputScale = 0.01R
        tbFillL.SmallChange = 0.05R
        tbFillL.LargeChange = 0.1R
        tbFillL.TickFrequency = 0.1R
        tbFillL.ShowTicks = True
        tbFillL.Name = "tbFillL"
        tbFillL.Size = New Size(230, 28)
        tbFillL.TabIndex = 0
        '
        ' grpFillR
        '
        grpFillR.Controls.Add(lblR_Str)
        grpFillR.Controls.Add(tbFillR)
        grpFillR.Controls.Add(lblR_Tint)
        grpFillR.Controls.Add(btnFillRColor)
        grpFillR.Controls.Add(nudR_Az)
        grpFillR.Controls.Add(nudR_El)
        grpFillR.Controls.Add(lblR_Az)
        grpFillR.Controls.Add(lblR_El)
        grpFillR.Location = New Point(12, 252)
        grpFillR.Name = "grpFillR"
        grpFillR.Size = New Size(418, 110)
        grpFillR.TabIndex = 2
        grpFillR.TabStop = False
        '
        ' direccion de mundo de la luz: azimut + elevacion (reemplazo de la grilla de 6 NUD)
        '
        lblR_Str.AutoSize = True
        lblR_Str.Location = New Point(11, 34)
        lblR_Str.Name = "lblR_Str"
        lblR_Str.Text = "Strength"
        '
        lblR_Tint.AutoSize = True
        lblR_Tint.Location = New Point(311, 36)
        lblR_Tint.Name = "lblR_Tint"
        lblR_Tint.Text = "Tint"
        '
        lblR_Az.AutoSize = True
        lblR_Az.Location = New Point(11, 78)
        lblR_Az.Name = "lblR_Az"
        lblR_Az.Size = New Size(54, 15)
        lblR_Az.Text = "Azimuth"
        '
        nudR_Az.DecimalPlaces = 1
        nudR_Az.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudR_Az.Location = New Point(118, 74)
        nudR_Az.Maximum = New Decimal(New Integer() {360, 0, 0, 0})
        nudR_Az.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        nudR_Az.Name = "nudR_Az"
        nudR_Az.Size = New Size(90, 23)
        nudR_Az.TabIndex = 2
        nudR_Az.TextAlign = HorizontalAlignment.Right
        '
        lblR_El.AutoSize = True
        lblR_El.Location = New Point(208, 78)
        lblR_El.Name = "lblR_El"
        lblR_El.Size = New Size(60, 15)
        lblR_El.Text = "Elevation"
        '
        nudR_El.DecimalPlaces = 1
        nudR_El.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudR_El.Location = New Point(315, 74)
        nudR_El.Maximum = New Decimal(New Integer() {90, 0, 0, 0})
        nudR_El.Minimum = New Decimal(New Integer() {90, 0, 0, -2147483648})
        nudR_El.Name = "nudR_El"
        nudR_El.Size = New Size(90, 23)
        nudR_El.TabIndex = 3
        nudR_El.TextAlign = HorizontalAlignment.Right
        grpFillR.Text = "Fill Right"
        '
        ' tbFillR
        '
        tbFillR.Location = New Point(75, 30)
        tbFillR.Minimum = 0R
        tbFillR.Maximum = 2R
        tbFillR.DisplayFormat = "0.00%"
        tbFillR.InputScale = 0.01R
        tbFillR.SmallChange = 0.05R
        tbFillR.LargeChange = 0.1R
        tbFillR.TickFrequency = 0.1R
        tbFillR.ShowTicks = True
        tbFillR.Name = "tbFillR"
        tbFillR.Size = New Size(230, 28)
        tbFillR.TabIndex = 0
        '
        ' grpBack
        '
        grpBack.Controls.Add(lblB_Str)
        grpBack.Controls.Add(tbBack)
        grpBack.Controls.Add(lblB_Tint)
        grpBack.Controls.Add(btnBackColor)
        grpBack.Controls.Add(nudB_Az)
        grpBack.Controls.Add(nudB_El)
        grpBack.Controls.Add(lblB_Az)
        grpBack.Controls.Add(lblB_El)
        grpBack.Location = New Point(12, 372)
        grpBack.Name = "grpBack"
        grpBack.Size = New Size(418, 110)
        grpBack.TabIndex = 3
        grpBack.TabStop = False
        '
        ' direccion de mundo de la luz: azimut + elevacion (reemplazo de la grilla de 6 NUD)
        '
        lblB_Str.AutoSize = True
        lblB_Str.Location = New Point(11, 34)
        lblB_Str.Name = "lblB_Str"
        lblB_Str.Text = "Strength"
        '
        lblB_Tint.AutoSize = True
        lblB_Tint.Location = New Point(311, 36)
        lblB_Tint.Name = "lblB_Tint"
        lblB_Tint.Text = "Tint"
        '
        lblB_Az.AutoSize = True
        lblB_Az.Location = New Point(11, 78)
        lblB_Az.Name = "lblB_Az"
        lblB_Az.Size = New Size(54, 15)
        lblB_Az.Text = "Azimuth"
        '
        nudB_Az.DecimalPlaces = 1
        nudB_Az.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudB_Az.Location = New Point(118, 74)
        nudB_Az.Maximum = New Decimal(New Integer() {360, 0, 0, 0})
        nudB_Az.Minimum = New Decimal(New Integer() {0, 0, 0, 0})
        nudB_Az.Name = "nudB_Az"
        nudB_Az.Size = New Size(90, 23)
        nudB_Az.TabIndex = 2
        nudB_Az.TextAlign = HorizontalAlignment.Right
        '
        lblB_El.AutoSize = True
        lblB_El.Location = New Point(208, 78)
        lblB_El.Name = "lblB_El"
        lblB_El.Size = New Size(60, 15)
        lblB_El.Text = "Elevation"
        '
        nudB_El.DecimalPlaces = 1
        nudB_El.Increment = New Decimal(New Integer() {5, 0, 0, 0})
        nudB_El.Location = New Point(315, 74)
        nudB_El.Maximum = New Decimal(New Integer() {90, 0, 0, 0})
        nudB_El.Minimum = New Decimal(New Integer() {90, 0, 0, -2147483648})
        nudB_El.Name = "nudB_El"
        nudB_El.Size = New Size(90, 23)
        nudB_El.TabIndex = 3
        nudB_El.TextAlign = HorizontalAlignment.Right
        grpBack.Text = "Back Light"
        '
        ' tbBack
        '
        tbBack.Location = New Point(75, 30)
        tbBack.Minimum = 0R
        tbBack.Maximum = 2R
        tbBack.DisplayFormat = "0.00%"
        tbBack.InputScale = 0.01R
        tbBack.SmallChange = 0.05R
        tbBack.LargeChange = 0.1R
        tbBack.TickFrequency = 0.1R
        tbBack.ShowTicks = True
        tbBack.Name = "tbBack"
        tbBack.Size = New Size(230, 28)
        tbBack.TabIndex = 0
        '
        ' grpPresets -- una sola fila: [Preset v] [Apply] [Reset]. Reemplaza al botón "Reset to default"
        ' que ocupaba una banda de 418x49 él solo.
        '
        grpPresets.Controls.Add(lblPreset)
        grpPresets.Controls.Add(cmbPreset)
        grpPresets.Controls.Add(btnApplyPreset)
        grpPresets.Controls.Add(btnReset)
        grpPresets.Location = New Point(436, 248)
        grpPresets.Name = "grpPresets"
        grpPresets.Size = New Size(418, 66)
        grpPresets.TabIndex = 7
        grpPresets.TabStop = False
        grpPresets.Text = "Presets"
        '
        ' lblPreset
        '
        lblPreset.AutoSize = True
        lblPreset.Location = New Point(11, 31)
        lblPreset.Name = "lblPreset"
        lblPreset.Size = New Size(43, 15)
        lblPreset.TabIndex = 0
        lblPreset.Text = "Set"
        '
        ' cmbPreset
        '
        cmbPreset.DropDownStyle = ComboBoxStyle.DropDownList
        cmbPreset.FormattingEnabled = True
        cmbPreset.Location = New Point(56, 27)
        cmbPreset.Name = "cmbPreset"
        cmbPreset.Size = New Size(186, 23)
        cmbPreset.TabIndex = 1
        '
        ' btnApplyPreset
        '
        btnApplyPreset.Location = New Point(252, 26)
        btnApplyPreset.Name = "btnApplyPreset"
        btnApplyPreset.Size = New Size(72, 25)
        btnApplyPreset.TabIndex = 2
        btnApplyPreset.Text = "Apply"
        btnApplyPreset.UseVisualStyleBackColor = True
        '
        ' btnReset
        '
        btnReset.Location = New Point(332, 26)
        btnReset.Name = "btnReset"
        btnReset.Size = New Size(74, 25)
        btnReset.TabIndex = 3
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
        grpAmbient.Location = New Point(436, 12)
        grpAmbient.Name = "grpAmbient"
        grpAmbient.Size = New Size(418, 130)
        grpAmbient.TabIndex = 5
        grpAmbient.TabStop = False
        grpAmbient.Text = "Ambient"
        '
        ' tambient
        '
        lblIntensity.AutoSize = True
        lblIntensity.Location = New Point(11, 32)
        lblIntensity.Name = "lblIntensity"
        lblIntensity.Size = New Size(54, 15)
        lblIntensity.Text = "Intensity"
        '
        tambient.Location = New Point(98, 28)
        tambient.Minimum = 0R
        tambient.Maximum = 2R
        tambient.DisplayFormat = "0.00%"
        tambient.InputScale = 0.01R
        tambient.SmallChange = 0.05R
        tambient.LargeChange = 0.1R
        tambient.TickFrequency = 0.1R
        tambient.ShowTicks = True
        tambient.Name = "tambient"
        tambient.Size = New Size(300, 28)
        tambient.TabIndex = 0
        '
        lblGroundLvl.AutoSize = True
        lblGroundLvl.Location = New Point(11, 64)
        lblGroundLvl.Name = "lblGroundLvl"
        lblGroundLvl.Size = New Size(78, 15)
        lblGroundLvl.Text = "Ground level"
        '
        tGroundLevel.Location = New Point(98, 60)
        tGroundLevel.Minimum = 0R
        tGroundLevel.Maximum = 1R
        tGroundLevel.DisplayFormat = "0.00%"
        tGroundLevel.InputScale = 0.01R
        tGroundLevel.SmallChange = 0.05R
        tGroundLevel.LargeChange = 0.1R
        tGroundLevel.TickFrequency = 0.1R
        tGroundLevel.ShowTicks = True
        tGroundLevel.Name = "tGroundLevel"
        tGroundLevel.Size = New Size(300, 28)
        tGroundLevel.TabIndex = 1
        '
        ' grpShadows -- sombras proyectadas del previewer. Ver ShadowMap.vb.
        '
        chkShadows.AutoSize = True
        chkShadows.Location = New Point(11, 26)
        chkShadows.Name = "chkShadows"
        chkShadows.Size = New Size(110, 19)
        chkShadows.TabIndex = 0
        chkShadows.Text = "Cast shadows"
        '
        chkGroundShadow.AutoSize = True
        chkGroundShadow.Location = New Point(11, 118)
        chkGroundShadow.Name = "chkGroundShadow"
        chkGroundShadow.Size = New Size(150, 19)
        chkGroundShadow.TabIndex = 4
        chkGroundShadow.Text = "Shadow on the ground"
        '
        lblShadowQuality.AutoSize = True
        lblShadowQuality.Location = New Point(158, 27)
        lblShadowQuality.Name = "lblShadowQuality"
        lblShadowQuality.Size = New Size(48, 15)
        lblShadowQuality.Text = "Quality"
        '
        cmbShadowQuality.DropDownStyle = ComboBoxStyle.DropDownList
        cmbShadowQuality.Location = New Point(228, 23)
        cmbShadowQuality.Name = "cmbShadowQuality"
        cmbShadowQuality.Size = New Size(170, 23)
        cmbShadowQuality.TabIndex = 1
        '
        lblShadowSoft.AutoSize = True
        lblShadowSoft.Location = New Point(11, 60)
        lblShadowSoft.Name = "lblShadowSoft"
        lblShadowSoft.Size = New Size(60, 15)
        lblShadowSoft.Text = "Softness"
        '
        tShadowSoft.Location = New Point(98, 56)
        tShadowSoft.Minimum = 0R
        tShadowSoft.Maximum = 4R
        tShadowSoft.DisplayFormat = "0.0"
        tShadowSoft.InputScale = 1R
        tShadowSoft.SmallChange = 0.5R
        tShadowSoft.LargeChange = 1R
        tShadowSoft.TickFrequency = 1R
        tShadowSoft.ShowTicks = True
        tShadowSoft.Name = "tShadowSoft"
        tShadowSoft.Size = New Size(300, 28)
        tShadowSoft.TabIndex = 2
        '
        lblShadowStrength.AutoSize = True
        lblShadowStrength.Location = New Point(11, 92)
        lblShadowStrength.Name = "lblShadowStrength"
        lblShadowStrength.Size = New Size(60, 15)
        lblShadowStrength.Text = "Darkness"
        '
        tShadowStrength.Location = New Point(98, 88)
        tShadowStrength.Minimum = 0R
        tShadowStrength.Maximum = 1R
        tShadowStrength.DisplayFormat = "0.00%"
        tShadowStrength.InputScale = 0.01R
        tShadowStrength.SmallChange = 0.05R
        tShadowStrength.LargeChange = 0.1R
        tShadowStrength.TickFrequency = 0.1R
        tShadowStrength.ShowTicks = True
        tShadowStrength.Name = "tShadowStrength"
        tShadowStrength.Size = New Size(300, 28)
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
        grpShadows.Location = New Point(436, 334)
        grpShadows.Name = "grpShadows"
        grpShadows.Size = New Size(418, 148)
        grpShadows.TabIndex = 8
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
        chkRecalcNormals.Text = "Recalculate normals on load"
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
        grpSkin.Location = New Point(436, 12)
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
        grpCamera.Location = New Point(436, 144)
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
        grpFloor.Location = New Point(436, 276)
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
        TabRender.Size = New Size(866, 494)
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
        TabLights.Size = New Size(866, 494)
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
        TabsMain.Size = New Size(874, 522)
        TabsMain.TabIndex = 0
        '
        ' grpBackground
        '
        grpBackground.Controls.Add(lblBackground)
        grpBackground.Controls.Add(cmbBackground)
        grpBackground.Location = New Point(436, 161)
        grpBackground.Name = "grpBackground"
        grpBackground.Size = New Size(418, 68)
        grpBackground.TabIndex = 6
        grpBackground.TabStop = False
        grpBackground.Text = "Background"
        '
        ' lblBackground
        '
        lblBackground.AutoSize = True
        lblBackground.Location = New Point(11, 34)
        lblBackground.Name = "lblBackground"
        lblBackground.Size = New Size(72, 15)
        lblBackground.TabIndex = 0
        lblBackground.Text = "Color"
        '
        ' cmbBackground
        '
        cmbBackground.DrawMode = DrawMode.OwnerDrawFixed
        cmbBackground.DropDownStyle = ComboBoxStyle.DropDownList
        cmbBackground.FormattingEnabled = True
        cmbBackground.Location = New Point(110, 30)
        cmbBackground.Name = "cmbBackground"
        cmbBackground.Size = New Size(290, 24)
        cmbBackground.TabIndex = 1
        '
        ' Tooltip
        '
        ToolTip1.SetToolTip(tbKey, "Strength of the KEY light: the main one, and the only one that casts shadows.")

        ToolTip1.SetToolTip(tbFillL, "Strength of the left fill light. Fills open up the shadow side; they never cast.")

        ToolTip1.SetToolTip(tbFillR, "Strength of the right fill light. Fills open up the shadow side; they never cast.")

        ToolTip1.SetToolTip(tbBack, "Strength of the back light. Separates the silhouette from the background; it never casts.")

        ToolTip1.SetToolTip(tambient, "Adjust ambient light intensity.")
        ToolTip1.SetToolTip(btnApplyPreset, "Load the selected preset into every control below.")
        ToolTip1.SetToolTip(btnReset, "Back to the Studio preset AND the default background color.")
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
        ToolTip1.SetToolTip(nudK_Az, "Compass direction the key light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(nudK_El, "Height of the key light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        ToolTip1.SetToolTip(nudL_Az, "Compass direction the left fill light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(nudL_El, "Height of the left fill light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        ToolTip1.SetToolTip(nudR_Az, "Compass direction the right fill light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(nudR_El, "Height of the right fill light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        ToolTip1.SetToolTip(nudB_Az, "Compass direction the back (rim) light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        ToolTip1.SetToolTip(nudB_El, "Height of the back (rim) light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        '
        ' Rendering tab -- migrated from Wardrobe Manager's Settings dialog together with the
        ' controls themselves. The text is the user's own documentation of each knob; dropping it on
        ' the way over would have made the shared dialog strictly worse than the screen it replaced.
        '
        ToolTip1.SetToolTip(chkRecalcNormals, "Recalculate normals and the tangent basis for the preview, both when geometry is loaded and after morphs deform it. Without it a morphed mesh keeps the tangent basis of its un-morphed shape, which is the frame the shader reads the normal map in.")
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
        ' light color swatches (each one lives in its light group, beside the strength slider)
        '
        btnKeyColor.Location = New Point(345, 32)
        btnKeyColor.Name = "btnKeyColor"
        btnKeyColor.Size = New Size(60, 23)
        btnKeyColor.TabIndex = 1
        btnKeyColor.UseVisualStyleBackColor = False
        '
        btnFillLColor.Location = New Point(345, 32)
        btnFillLColor.Name = "btnFillLColor"
        btnFillLColor.Size = New Size(60, 23)
        btnFillLColor.TabIndex = 1
        btnFillLColor.UseVisualStyleBackColor = False
        '
        btnFillRColor.Location = New Point(345, 32)
        btnFillRColor.Name = "btnFillRColor"
        btnFillRColor.Size = New Size(60, 23)
        btnFillRColor.TabIndex = 1
        btnFillRColor.UseVisualStyleBackColor = False
        '
        btnBackColor.Location = New Point(345, 32)
        btnBackColor.Name = "btnBackColor"
        btnBackColor.Size = New Size(60, 23)
        btnBackColor.TabIndex = 1
        btnBackColor.UseVisualStyleBackColor = False
        '
        lblAmbSky.AutoSize = True
        lblAmbSky.Location = New Point(11, 102)
        lblAmbSky.Name = "lblAmbSky"
        lblAmbSky.Size = New Size(46, 15)
        lblAmbSky.Text = "Sky tint"
        '
        btnAmbSky.Location = New Point(98, 98)
        btnAmbSky.Name = "btnAmbSky"
        btnAmbSky.Size = New Size(60, 23)
        btnAmbSky.TabIndex = 2
        btnAmbSky.UseVisualStyleBackColor = False
        '
        lblAmbGround.AutoSize = True
        lblAmbGround.Location = New Point(266, 102)
        lblAmbGround.Name = "lblAmbGround"
        lblAmbGround.Size = New Size(68, 15)
        lblAmbGround.Text = "Ground tint"
        '
        btnAmbGround.Location = New Point(338, 98)
        btnAmbGround.Name = "btnAmbGround"
        btnAmbGround.Size = New Size(60, 23)
        btnAmbGround.TabIndex = 3
        btnAmbGround.UseVisualStyleBackColor = False
        '
        ' LightRigForm
        '
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        AutoScroll = True
        Controls.Add(TabsMain)
        ClientSize = New Size(874, 522)
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
        CType(nudK_Az, ComponentModel.ISupportInitialize).EndInit()
        CType(nudK_El, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_Az, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_El, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_Az, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_El, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_Az, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_El, ComponentModel.ISupportInitialize).EndInit()
        CType(nudSeamAngle, ComponentModel.ISupportInitialize).EndInit()
        CType(nudWeldPos, ComponentModel.ISupportInitialize).EndInit()
        CType(nudWeldUv, ComponentModel.ISupportInitialize).EndInit()
        CType(nudEpsPos, ComponentModel.ISupportInitialize).EndInit()
        CType(nudFloorSize, ComponentModel.ISupportInitialize).EndInit()
        CType(nudFloorStep, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)

    End Sub

    Friend WithEvents grpKey As GroupBox
    Friend WithEvents nudK_Az As NumericUpDown
    Friend WithEvents nudK_El As NumericUpDown
    Friend WithEvents lblK_Str As Label
    Friend WithEvents lblK_Tint As Label
    Friend WithEvents lblK_Az As Label
    Friend WithEvents lblK_El As Label
    Friend WithEvents tbKey As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents grpFillL As GroupBox
    Friend WithEvents nudL_Az As NumericUpDown
    Friend WithEvents nudL_El As NumericUpDown
    Friend WithEvents lblL_Str As Label
    Friend WithEvents lblL_Tint As Label
    Friend WithEvents lblL_Az As Label
    Friend WithEvents lblL_El As Label
    Friend WithEvents tbFillL As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents grpFillR As GroupBox
    Friend WithEvents nudR_Az As NumericUpDown
    Friend WithEvents nudR_El As NumericUpDown
    Friend WithEvents lblR_Str As Label
    Friend WithEvents lblR_Tint As Label
    Friend WithEvents lblR_Az As Label
    Friend WithEvents lblR_El As Label
    Friend WithEvents tbFillR As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents grpBack As GroupBox
    Friend WithEvents nudB_Az As NumericUpDown
    Friend WithEvents nudB_El As NumericUpDown
    Friend WithEvents lblB_Str As Label
    Friend WithEvents lblB_Tint As Label
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
    Friend WithEvents nudSeamAngle As NumericUpDown
    Friend WithEvents grpWeld As GroupBox
    Friend WithEvents chkWelding As CheckBox
    Friend WithEvents rbWeldPosOnly As RadioButton
    Friend WithEvents rbWeldBoth As RadioButton
    Friend WithEvents lblWeldPos As Label
    Friend WithEvents nudWeldPos As NumericUpDown
    Friend WithEvents lblWeldUv As Label
    Friend WithEvents nudWeldUv As NumericUpDown
    Friend WithEvents lblEpsPos As Label
    Friend WithEvents nudEpsPos As NumericUpDown
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
    Friend WithEvents nudFloorSize As NumericUpDown
    Friend WithEvents lblFloorStep As Label
    Friend WithEvents nudFloorStep As NumericUpDown
    Friend WithEvents lblFloorColor As Label
    Friend WithEvents cmbFloorColor As ColorComboBox
    Friend WithEvents grpShadows As GroupBox
    Friend WithEvents chkShadows As CheckBox
    Friend WithEvents chkGroundShadow As CheckBox
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
