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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(LightRigForm))
        grpKey = New GroupBox()
        lblK_Str = New Label()
        tbKey = New TinySliderTextBox()
        lblK_Az = New Label()
        tK_Az = New TinySliderTextBox()
        lblK_El = New Label()
        tK_El = New TinySliderTextBox()
        btnKeyColor = New Button()
        chkCastKey = New CheckBox()
        grpFillL = New GroupBox()
        lblL_Str = New Label()
        tbFillL = New TinySliderTextBox()
        lblL_Az = New Label()
        tL_Az = New TinySliderTextBox()
        lblL_El = New Label()
        tL_El = New TinySliderTextBox()
        btnFillLColor = New Button()
        chkCastFillL = New CheckBox()
        grpFillR = New GroupBox()
        lblR_Str = New Label()
        tbFillR = New TinySliderTextBox()
        lblR_Az = New Label()
        tR_Az = New TinySliderTextBox()
        lblR_El = New Label()
        tR_El = New TinySliderTextBox()
        btnFillRColor = New Button()
        chkCastFillR = New CheckBox()
        grpBack = New GroupBox()
        lblB_Str = New Label()
        tbBack = New TinySliderTextBox()
        lblB_Az = New Label()
        tB_Az = New TinySliderTextBox()
        lblB_El = New Label()
        tB_El = New TinySliderTextBox()
        btnBackColor = New Button()
        chkCastBack = New CheckBox()
        grpPresets = New GroupBox()
        lblPreset = New Label()
        cmbPreset = New ComboBox()
        btnApplyPreset = New Button()
        btnReset = New Button()
        chkLightsFollowCamera = New CheckBox()
        grpAmbient = New GroupBox()
        lblIntensity = New Label()
        tambient = New TinySliderTextBox()
        lblGroundLvl = New Label()
        tGroundLevel = New TinySliderTextBox()
        btnAmbSky = New Button()
        btnAmbGround = New Button()
        grpBackground = New GroupBox()
        lblBackground = New Label()
        cmbBackground = New ColorComboBox()
        grpShadows = New GroupBox()
        chkShadows = New CheckBox()
        lblShadowVram = New Label()
        chkDepth16 = New CheckBox()
        chkGroundShadow = New CheckBox()
        lblShadowQuality = New Label()
        cmbShadowQuality = New ComboBox()
        lblShadowSoft = New Label()
        tShadowSoft = New TinySliderTextBox()
        lblShadowStrength = New Label()
        tShadowStrength = New TinySliderTextBox()
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
        lblEpsPos = New Label()
        nudEpsPos = New NumericUpDownCultura()
        grpWeld = New GroupBox()
        chkWelding = New CheckBox()
        rbWeldBoth = New RadioButton()
        rbWeldPosOnly = New RadioButton()
        lblWeldPos = New Label()
        nudWeldPos = New NumericUpDownCultura()
        lblWeldUv = New Label()
        nudWeldUv = New NumericUpDownCultura()
        grpSkin = New GroupBox()
        chkGpuSkinning = New CheckBox()
        chkSingleBone = New CheckBox()
        chkHiddenSegments = New CheckBox()
        grpCamera = New GroupBox()
        chkResetAngles = New CheckBox()
        chkResetZoom = New CheckBox()
        chkFreezeCamera = New CheckBox()
        grpFloor = New GroupBox()
        chkFloorEnabled = New CheckBox()
        lblFloorSize = New Label()
        nudFloorSize = New NumericUpDownCultura()
        lblFloorStep = New Label()
        nudFloorStep = New NumericUpDownCultura()
        lblFloorColor = New Label()
        cmbFloorColor = New ColorComboBox()
        btnResetRender = New Button()
        ToolTip1 = New ToolTip(components)
        grpKey.SuspendLayout()
        grpFillL.SuspendLayout()
        grpFillR.SuspendLayout()
        grpBack.SuspendLayout()
        grpPresets.SuspendLayout()
        grpAmbient.SuspendLayout()
        grpBackground.SuspendLayout()
        grpShadows.SuspendLayout()
        TabsMain.SuspendLayout()
        TabLights.SuspendLayout()
        TabRender.SuspendLayout()
        grpNormals.SuspendLayout()
        CType(nudSeamAngle, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudEpsPos, ComponentModel.ISupportInitialize).BeginInit()
        grpWeld.SuspendLayout()
        CType(nudWeldPos, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudWeldUv, ComponentModel.ISupportInitialize).BeginInit()
        grpSkin.SuspendLayout()
        grpCamera.SuspendLayout()
        grpFloor.SuspendLayout()
        CType(nudFloorSize, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudFloorStep, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' grpKey
        ' 
        grpKey.Controls.Add(lblK_Str)
        grpKey.Controls.Add(tbKey)
        grpKey.Controls.Add(lblK_Az)
        grpKey.Controls.Add(tK_Az)
        grpKey.Controls.Add(lblK_El)
        grpKey.Controls.Add(tK_El)
        grpKey.Controls.Add(btnKeyColor)
        grpKey.Controls.Add(chkCastKey)
        grpKey.Location = New Point(12, 12)
        grpKey.Name = "grpKey"
        grpKey.Size = New Size(418, 118)
        grpKey.TabIndex = 0
        grpKey.TabStop = False
        grpKey.Text = "Key Light"
        ' 
        ' lblK_Str
        ' 
        lblK_Str.AutoSize = True
        lblK_Str.Location = New Point(11, 30)
        lblK_Str.Name = "lblK_Str"
        lblK_Str.Size = New Size(52, 15)
        lblK_Str.TabIndex = 0
        lblK_Str.Text = "Strength"
        ' 
        ' tbKey
        ' 
        tbKey.AccentColor = SystemColors.HotTrack
        tbKey.BackColor = SystemColors.Control
        tbKey.DisplayFormat = "0.00%"
        tbKey.InputScale = 0.01R
        tbKey.LargeChange = 0.1R
        tbKey.Location = New Point(87, 24)
        tbKey.Maximum = 2R
        tbKey.MinimumSize = New Size(100, 24)
        tbKey.Name = "tbKey"
        tbKey.ShowTicks = True
        tbKey.Size = New Size(224, 28)
        tbKey.SmallChange = 0.05R
        tbKey.TabIndex = 0
        tbKey.TextBoxTextAlign = HorizontalAlignment.Right
        tbKey.ThumbColor = SystemColors.HotTrack
        tbKey.ThumbRadius = 4F
        tbKey.TickFrequency = 0.25R
        ToolTip1.SetToolTip(tbKey, "Strength of the KEY light: the main one, and the one that stands in for the engine's single directional. Whether it casts is the Shadows box.")
        tbKey.TrackColor = SystemColors.ControlDark
        ' 
        ' lblK_Az
        ' 
        lblK_Az.AutoSize = True
        lblK_Az.Location = New Point(11, 60)
        lblK_Az.Name = "lblK_Az"
        lblK_Az.Size = New Size(52, 15)
        lblK_Az.TabIndex = 1
        lblK_Az.Text = "Azimuth"
        ' 
        ' tK_Az
        ' 
        tK_Az.AccentColor = SystemColors.HotTrack
        tK_Az.BackColor = SystemColors.Control
        tK_Az.DisplayFormat = "0.0°"
        tK_Az.LargeChange = 15R
        tK_Az.Location = New Point(87, 54)
        tK_Az.Maximum = 360R
        tK_Az.MinimumSize = New Size(100, 24)
        tK_Az.Name = "tK_Az"
        tK_Az.ShowTicks = True
        tK_Az.Size = New Size(224, 28)
        tK_Az.TabIndex = 1
        tK_Az.TextBoxTextAlign = HorizontalAlignment.Right
        tK_Az.ThumbColor = SystemColors.HotTrack
        tK_Az.ThumbRadius = 4F
        tK_Az.TickFrequency = 45R
        ToolTip1.SetToolTip(tK_Az, "Compass direction the key light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        tK_Az.TrackColor = SystemColors.ControlDark
        ' 
        ' lblK_El
        ' 
        lblK_El.AutoSize = True
        lblK_El.Location = New Point(11, 90)
        lblK_El.Name = "lblK_El"
        lblK_El.Size = New Size(55, 15)
        lblK_El.TabIndex = 2
        lblK_El.Text = "Elevation"
        ' 
        ' tK_El
        ' 
        tK_El.AccentColor = SystemColors.HotTrack
        tK_El.BackColor = SystemColors.Control
        tK_El.DisplayFormat = "0.0°"
        tK_El.FillMode = TinySliderFillMode.Center
        tK_El.LargeChange = 15R
        tK_El.Location = New Point(87, 84)
        tK_El.Maximum = 90R
        tK_El.Minimum = -90R
        tK_El.MinimumSize = New Size(100, 24)
        tK_El.Name = "tK_El"
        tK_El.ShowTicks = True
        tK_El.Size = New Size(224, 28)
        tK_El.TabIndex = 2
        tK_El.TextBoxTextAlign = HorizontalAlignment.Right
        tK_El.ThumbColor = SystemColors.HotTrack
        tK_El.ThumbRadius = 4F
        tK_El.TickFrequency = 30R
        ToolTip1.SetToolTip(tK_El, "Height of the key light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        tK_El.TrackColor = SystemColors.ControlDark
        ' 
        ' btnKeyColor
        ' 
        btnKeyColor.Location = New Point(317, 24)
        btnKeyColor.Name = "btnKeyColor"
        btnKeyColor.Size = New Size(90, 28)
        btnKeyColor.TabIndex = 3
        ToolTip1.SetToolTip(btnKeyColor, "Tint of the key light. Strength is the slider on the left.")
        btnKeyColor.UseVisualStyleBackColor = False
        ' 
        ' chkCastKey
        ' 
        chkCastKey.AutoSize = True
        chkCastKey.Location = New Point(317, 58)
        chkCastKey.Name = "chkCastKey"
        chkCastKey.Size = New Size(73, 19)
        chkCastKey.TabIndex = 4
        chkCastKey.Text = "Shadows"
        ToolTip1.SetToolTip(chkCastKey, "This light writes its own shadow map: more VRAM and one PCF lookup per pixel. Only the key casts by default.")
        ' 
        ' grpFillL
        ' 
        grpFillL.Controls.Add(lblL_Str)
        grpFillL.Controls.Add(tbFillL)
        grpFillL.Controls.Add(lblL_Az)
        grpFillL.Controls.Add(tL_Az)
        grpFillL.Controls.Add(lblL_El)
        grpFillL.Controls.Add(tL_El)
        grpFillL.Controls.Add(btnFillLColor)
        grpFillL.Controls.Add(chkCastFillL)
        grpFillL.Location = New Point(12, 142)
        grpFillL.Name = "grpFillL"
        grpFillL.Size = New Size(418, 118)
        grpFillL.TabIndex = 1
        grpFillL.TabStop = False
        grpFillL.Text = "Fill Left"
        ' 
        ' lblL_Str
        ' 
        lblL_Str.AutoSize = True
        lblL_Str.Location = New Point(11, 30)
        lblL_Str.Name = "lblL_Str"
        lblL_Str.Size = New Size(52, 15)
        lblL_Str.TabIndex = 0
        lblL_Str.Text = "Strength"
        ' 
        ' tbFillL
        ' 
        tbFillL.AccentColor = SystemColors.HotTrack
        tbFillL.BackColor = SystemColors.Control
        tbFillL.DisplayFormat = "0.00%"
        tbFillL.InputScale = 0.01R
        tbFillL.LargeChange = 0.1R
        tbFillL.Location = New Point(87, 24)
        tbFillL.Maximum = 2R
        tbFillL.MinimumSize = New Size(100, 24)
        tbFillL.Name = "tbFillL"
        tbFillL.ShowTicks = True
        tbFillL.Size = New Size(224, 28)
        tbFillL.SmallChange = 0.05R
        tbFillL.TabIndex = 0
        tbFillL.TextBoxTextAlign = HorizontalAlignment.Right
        tbFillL.ThumbColor = SystemColors.HotTrack
        tbFillL.ThumbRadius = 4F
        tbFillL.TickFrequency = 0.25R
        ToolTip1.SetToolTip(tbFillL, "Strength of the left fill light. Fills open up the shadow side; ticking its Shadows box makes it cast too.")
        tbFillL.TrackColor = SystemColors.ControlDark
        ' 
        ' lblL_Az
        ' 
        lblL_Az.AutoSize = True
        lblL_Az.Location = New Point(11, 60)
        lblL_Az.Name = "lblL_Az"
        lblL_Az.Size = New Size(52, 15)
        lblL_Az.TabIndex = 1
        lblL_Az.Text = "Azimuth"
        ' 
        ' tL_Az
        ' 
        tL_Az.AccentColor = SystemColors.HotTrack
        tL_Az.BackColor = SystemColors.Control
        tL_Az.DisplayFormat = "0.0°"
        tL_Az.LargeChange = 15R
        tL_Az.Location = New Point(87, 54)
        tL_Az.Maximum = 360R
        tL_Az.MinimumSize = New Size(100, 24)
        tL_Az.Name = "tL_Az"
        tL_Az.ShowTicks = True
        tL_Az.Size = New Size(224, 28)
        tL_Az.TabIndex = 1
        tL_Az.TextBoxTextAlign = HorizontalAlignment.Right
        tL_Az.ThumbColor = SystemColors.HotTrack
        tL_Az.ThumbRadius = 4F
        tL_Az.TickFrequency = 45R
        ToolTip1.SetToolTip(tL_Az, "Compass direction the left fill light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        tL_Az.TrackColor = SystemColors.ControlDark
        ' 
        ' lblL_El
        ' 
        lblL_El.AutoSize = True
        lblL_El.Location = New Point(11, 90)
        lblL_El.Name = "lblL_El"
        lblL_El.Size = New Size(55, 15)
        lblL_El.TabIndex = 2
        lblL_El.Text = "Elevation"
        ' 
        ' tL_El
        ' 
        tL_El.AccentColor = SystemColors.HotTrack
        tL_El.BackColor = SystemColors.Control
        tL_El.DisplayFormat = "0.0°"
        tL_El.FillMode = TinySliderFillMode.Center
        tL_El.LargeChange = 15R
        tL_El.Location = New Point(87, 84)
        tL_El.Maximum = 90R
        tL_El.Minimum = -90R
        tL_El.MinimumSize = New Size(100, 24)
        tL_El.Name = "tL_El"
        tL_El.ShowTicks = True
        tL_El.Size = New Size(224, 28)
        tL_El.TabIndex = 2
        tL_El.TextBoxTextAlign = HorizontalAlignment.Right
        tL_El.ThumbColor = SystemColors.HotTrack
        tL_El.ThumbRadius = 4F
        tL_El.TickFrequency = 30R
        ToolTip1.SetToolTip(tL_El, "Height of the left fill light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        tL_El.TrackColor = SystemColors.ControlDark
        ' 
        ' btnFillLColor
        ' 
        btnFillLColor.Location = New Point(317, 24)
        btnFillLColor.Name = "btnFillLColor"
        btnFillLColor.Size = New Size(90, 28)
        btnFillLColor.TabIndex = 3
        ToolTip1.SetToolTip(btnFillLColor, "Tint of the left fill light.")
        btnFillLColor.UseVisualStyleBackColor = False
        ' 
        ' chkCastFillL
        ' 
        chkCastFillL.AutoSize = True
        chkCastFillL.Location = New Point(317, 58)
        chkCastFillL.Name = "chkCastFillL"
        chkCastFillL.Size = New Size(73, 19)
        chkCastFillL.TabIndex = 4
        chkCastFillL.Text = "Shadows"
        ToolTip1.SetToolTip(chkCastFillL, "This light writes its own shadow map: more VRAM and one PCF lookup per pixel. Only the key casts by default.")
        ' 
        ' grpFillR
        ' 
        grpFillR.Controls.Add(lblR_Str)
        grpFillR.Controls.Add(tbFillR)
        grpFillR.Controls.Add(lblR_Az)
        grpFillR.Controls.Add(tR_Az)
        grpFillR.Controls.Add(lblR_El)
        grpFillR.Controls.Add(tR_El)
        grpFillR.Controls.Add(btnFillRColor)
        grpFillR.Controls.Add(chkCastFillR)
        grpFillR.Location = New Point(12, 272)
        grpFillR.Name = "grpFillR"
        grpFillR.Size = New Size(418, 118)
        grpFillR.TabIndex = 2
        grpFillR.TabStop = False
        grpFillR.Text = "Fill Right"
        ' 
        ' lblR_Str
        ' 
        lblR_Str.AutoSize = True
        lblR_Str.Location = New Point(11, 30)
        lblR_Str.Name = "lblR_Str"
        lblR_Str.Size = New Size(52, 15)
        lblR_Str.TabIndex = 0
        lblR_Str.Text = "Strength"
        ' 
        ' tbFillR
        ' 
        tbFillR.AccentColor = SystemColors.HotTrack
        tbFillR.BackColor = SystemColors.Control
        tbFillR.DisplayFormat = "0.00%"
        tbFillR.InputScale = 0.01R
        tbFillR.LargeChange = 0.1R
        tbFillR.Location = New Point(87, 24)
        tbFillR.Maximum = 2R
        tbFillR.MinimumSize = New Size(100, 24)
        tbFillR.Name = "tbFillR"
        tbFillR.ShowTicks = True
        tbFillR.Size = New Size(224, 28)
        tbFillR.SmallChange = 0.05R
        tbFillR.TabIndex = 0
        tbFillR.TextBoxTextAlign = HorizontalAlignment.Right
        tbFillR.ThumbColor = SystemColors.HotTrack
        tbFillR.ThumbRadius = 4F
        tbFillR.TickFrequency = 0.25R
        ToolTip1.SetToolTip(tbFillR, "Strength of the right fill light. Fills open up the shadow side; ticking its Shadows box makes it cast too.")
        tbFillR.TrackColor = SystemColors.ControlDark
        ' 
        ' lblR_Az
        ' 
        lblR_Az.AutoSize = True
        lblR_Az.Location = New Point(11, 60)
        lblR_Az.Name = "lblR_Az"
        lblR_Az.Size = New Size(52, 15)
        lblR_Az.TabIndex = 1
        lblR_Az.Text = "Azimuth"
        ' 
        ' tR_Az
        ' 
        tR_Az.AccentColor = SystemColors.HotTrack
        tR_Az.BackColor = SystemColors.Control
        tR_Az.DisplayFormat = "0.0°"
        tR_Az.LargeChange = 15R
        tR_Az.Location = New Point(87, 54)
        tR_Az.Maximum = 360R
        tR_Az.MinimumSize = New Size(100, 24)
        tR_Az.Name = "tR_Az"
        tR_Az.ShowTicks = True
        tR_Az.Size = New Size(224, 28)
        tR_Az.TabIndex = 1
        tR_Az.TextBoxTextAlign = HorizontalAlignment.Right
        tR_Az.ThumbColor = SystemColors.HotTrack
        tR_Az.ThumbRadius = 4F
        tR_Az.TickFrequency = 45R
        ToolTip1.SetToolTip(tR_Az, "Compass direction the right fill light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        tR_Az.TrackColor = SystemColors.ControlDark
        ' 
        ' lblR_El
        ' 
        lblR_El.AutoSize = True
        lblR_El.Location = New Point(11, 90)
        lblR_El.Name = "lblR_El"
        lblR_El.Size = New Size(55, 15)
        lblR_El.TabIndex = 2
        lblR_El.Text = "Elevation"
        ' 
        ' tR_El
        ' 
        tR_El.AccentColor = SystemColors.HotTrack
        tR_El.BackColor = SystemColors.Control
        tR_El.DisplayFormat = "0.0°"
        tR_El.FillMode = TinySliderFillMode.Center
        tR_El.LargeChange = 15R
        tR_El.Location = New Point(87, 84)
        tR_El.Maximum = 90R
        tR_El.Minimum = -90R
        tR_El.MinimumSize = New Size(100, 24)
        tR_El.Name = "tR_El"
        tR_El.ShowTicks = True
        tR_El.Size = New Size(224, 28)
        tR_El.TabIndex = 2
        tR_El.TextBoxTextAlign = HorizontalAlignment.Right
        tR_El.ThumbColor = SystemColors.HotTrack
        tR_El.ThumbRadius = 4F
        tR_El.TickFrequency = 30R
        ToolTip1.SetToolTip(tR_El, "Height of the right fill light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        tR_El.TrackColor = SystemColors.ControlDark
        ' 
        ' btnFillRColor
        ' 
        btnFillRColor.Location = New Point(317, 24)
        btnFillRColor.Name = "btnFillRColor"
        btnFillRColor.Size = New Size(90, 28)
        btnFillRColor.TabIndex = 3
        ToolTip1.SetToolTip(btnFillRColor, "Tint of the right fill light.")
        btnFillRColor.UseVisualStyleBackColor = False
        ' 
        ' chkCastFillR
        ' 
        chkCastFillR.AutoSize = True
        chkCastFillR.Location = New Point(317, 58)
        chkCastFillR.Name = "chkCastFillR"
        chkCastFillR.Size = New Size(73, 19)
        chkCastFillR.TabIndex = 4
        chkCastFillR.Text = "Shadows"
        ToolTip1.SetToolTip(chkCastFillR, "This light writes its own shadow map: more VRAM and one PCF lookup per pixel. Only the key casts by default.")
        ' 
        ' grpBack
        ' 
        grpBack.Controls.Add(lblB_Str)
        grpBack.Controls.Add(tbBack)
        grpBack.Controls.Add(lblB_Az)
        grpBack.Controls.Add(tB_Az)
        grpBack.Controls.Add(lblB_El)
        grpBack.Controls.Add(tB_El)
        grpBack.Controls.Add(btnBackColor)
        grpBack.Controls.Add(chkCastBack)
        grpBack.Location = New Point(12, 402)
        grpBack.Name = "grpBack"
        grpBack.Size = New Size(418, 118)
        grpBack.TabIndex = 3
        grpBack.TabStop = False
        grpBack.Text = "Back Light"
        ' 
        ' lblB_Str
        ' 
        lblB_Str.AutoSize = True
        lblB_Str.Location = New Point(11, 30)
        lblB_Str.Name = "lblB_Str"
        lblB_Str.Size = New Size(52, 15)
        lblB_Str.TabIndex = 0
        lblB_Str.Text = "Strength"
        ' 
        ' tbBack
        ' 
        tbBack.AccentColor = SystemColors.HotTrack
        tbBack.BackColor = SystemColors.Control
        tbBack.DisplayFormat = "0.00%"
        tbBack.InputScale = 0.01R
        tbBack.LargeChange = 0.1R
        tbBack.Location = New Point(87, 24)
        tbBack.Maximum = 2R
        tbBack.MinimumSize = New Size(100, 24)
        tbBack.Name = "tbBack"
        tbBack.ShowTicks = True
        tbBack.Size = New Size(224, 28)
        tbBack.SmallChange = 0.05R
        tbBack.TabIndex = 0
        tbBack.TextBoxTextAlign = HorizontalAlignment.Right
        tbBack.ThumbColor = SystemColors.HotTrack
        tbBack.ThumbRadius = 4F
        tbBack.TickFrequency = 0.25R
        ToolTip1.SetToolTip(tbBack, "Strength of the back light. Separates the silhouette from the background; ticking its Shadows box makes it cast too.")
        tbBack.TrackColor = SystemColors.ControlDark
        ' 
        ' lblB_Az
        ' 
        lblB_Az.AutoSize = True
        lblB_Az.Location = New Point(11, 60)
        lblB_Az.Name = "lblB_Az"
        lblB_Az.Size = New Size(52, 15)
        lblB_Az.TabIndex = 1
        lblB_Az.Text = "Azimuth"
        ' 
        ' tB_Az
        ' 
        tB_Az.AccentColor = SystemColors.HotTrack
        tB_Az.BackColor = SystemColors.Control
        tB_Az.DisplayFormat = "0.0°"
        tB_Az.LargeChange = 15R
        tB_Az.Location = New Point(87, 54)
        tB_Az.Maximum = 360R
        tB_Az.MinimumSize = New Size(100, 24)
        tB_Az.Name = "tB_Az"
        tB_Az.ShowTicks = True
        tB_Az.Size = New Size(224, 28)
        tB_Az.TabIndex = 1
        tB_Az.TextBoxTextAlign = HorizontalAlignment.Right
        tB_Az.ThumbColor = SystemColors.HotTrack
        tB_Az.ThumbRadius = 4F
        tB_Az.TickFrequency = 45R
        ToolTip1.SetToolTip(tB_Az, "Compass direction the back (rim) light comes FROM, in degrees. Fixed to the world: orbiting the camera does not move it.")
        tB_Az.TrackColor = SystemColors.ControlDark
        ' 
        ' lblB_El
        ' 
        lblB_El.AutoSize = True
        lblB_El.Location = New Point(11, 90)
        lblB_El.Name = "lblB_El"
        lblB_El.Size = New Size(55, 15)
        lblB_El.TabIndex = 2
        lblB_El.Text = "Elevation"
        ' 
        ' tB_El
        ' 
        tB_El.AccentColor = SystemColors.HotTrack
        tB_El.BackColor = SystemColors.Control
        tB_El.DisplayFormat = "0.0°"
        tB_El.FillMode = TinySliderFillMode.Center
        tB_El.LargeChange = 15R
        tB_El.Location = New Point(87, 84)
        tB_El.Maximum = 90R
        tB_El.Minimum = -90R
        tB_El.MinimumSize = New Size(100, 24)
        tB_El.Name = "tB_El"
        tB_El.ShowTicks = True
        tB_El.Size = New Size(224, 28)
        tB_El.TabIndex = 2
        tB_El.TextBoxTextAlign = HorizontalAlignment.Right
        tB_El.ThumbColor = SystemColors.HotTrack
        tB_El.ThumbRadius = 4F
        tB_El.TickFrequency = 30R
        ToolTip1.SetToolTip(tB_El, "Height of the back (rim) light above the horizon, in degrees. 0 is level with the model, 90 is straight overhead.")
        tB_El.TrackColor = SystemColors.ControlDark
        ' 
        ' btnBackColor
        ' 
        btnBackColor.Location = New Point(317, 24)
        btnBackColor.Name = "btnBackColor"
        btnBackColor.Size = New Size(90, 28)
        btnBackColor.TabIndex = 3
        ToolTip1.SetToolTip(btnBackColor, "Tint of the back light.")
        btnBackColor.UseVisualStyleBackColor = False
        ' 
        ' chkCastBack
        ' 
        chkCastBack.AutoSize = True
        chkCastBack.Location = New Point(317, 58)
        chkCastBack.Name = "chkCastBack"
        chkCastBack.Size = New Size(73, 19)
        chkCastBack.TabIndex = 4
        chkCastBack.Text = "Shadows"
        ToolTip1.SetToolTip(chkCastBack, "This light writes its own shadow map: more VRAM and one PCF lookup per pixel. Only the key casts by default.")
        ' 
        ' grpPresets
        ' 
        grpPresets.Controls.Add(lblPreset)
        grpPresets.Controls.Add(cmbPreset)
        grpPresets.Controls.Add(btnApplyPreset)
        grpPresets.Location = New Point(444, 364)
        grpPresets.Name = "grpPresets"
        grpPresets.Size = New Size(418, 59)
        grpPresets.TabIndex = 6
        grpPresets.TabStop = False
        grpPresets.Text = "Rig"
        ' 
        ' lblPreset
        ' 
        lblPreset.AutoSize = True
        lblPreset.Location = New Point(11, 32)
        lblPreset.Name = "lblPreset"
        lblPreset.Size = New Size(23, 15)
        lblPreset.TabIndex = 0
        lblPreset.Text = "Set"
        ' 
        ' cmbPreset
        ' 
        cmbPreset.DropDownStyle = ComboBoxStyle.DropDownList
        cmbPreset.FormattingEnabled = True
        cmbPreset.Location = New Point(87, 28)
        cmbPreset.Name = "cmbPreset"
        cmbPreset.Size = New Size(254, 23)
        cmbPreset.TabIndex = 0
        ' 
        ' btnApplyPreset
        ' 
        btnApplyPreset.Location = New Point(349, 26)
        btnApplyPreset.Name = "btnApplyPreset"
        btnApplyPreset.Size = New Size(58, 25)
        btnApplyPreset.TabIndex = 1
        btnApplyPreset.Text = "Apply"
        ToolTip1.SetToolTip(btnApplyPreset, "Load the selected preset into every control below.")
        btnApplyPreset.UseVisualStyleBackColor = True
        ' 
        ' btnReset
        ' 
        btnReset.Location = New Point(444, 432)
        btnReset.Name = "btnReset"
        btnReset.Size = New Size(414, 25)
        btnReset.TabIndex = 2
        btnReset.Text = "Reset Lighting to default"
        ToolTip1.SetToolTip(btnReset, "Reset the whole lighting tab: Studio preset, default background color, shadow settings back to their defaults (which turns shadows ON), and the light anchoring back to its default.")
        btnReset.UseVisualStyleBackColor = True
        ' 
        ' chkLightsFollowCamera
        ' 
        chkLightsFollowCamera.AutoSize = True
        chkLightsFollowCamera.Location = New Point(121, 26)
        chkLightsFollowCamera.Name = "chkLightsFollowCamera"
        chkLightsFollowCamera.Size = New Size(156, 19)
        chkLightsFollowCamera.TabIndex = 3
        chkLightsFollowCamera.Text = "Lights follow the camera"
        ToolTip1.SetToolTip(chkLightsFollowCamera, resources.GetString("chkLightsFollowCamera.ToolTip"))
        ' 
        ' grpAmbient
        ' 
        grpAmbient.Controls.Add(lblIntensity)
        grpAmbient.Controls.Add(tambient)
        grpAmbient.Controls.Add(lblGroundLvl)
        grpAmbient.Controls.Add(tGroundLevel)
        grpAmbient.Controls.Add(btnAmbSky)
        grpAmbient.Controls.Add(btnAmbGround)
        grpAmbient.Location = New Point(444, 12)
        grpAmbient.Name = "grpAmbient"
        grpAmbient.Size = New Size(418, 92)
        grpAmbient.TabIndex = 4
        grpAmbient.TabStop = False
        grpAmbient.Text = "Ambient"
        ' 
        ' lblIntensity
        ' 
        lblIntensity.AutoSize = True
        lblIntensity.Location = New Point(11, 30)
        lblIntensity.Name = "lblIntensity"
        lblIntensity.Size = New Size(52, 15)
        lblIntensity.TabIndex = 0
        lblIntensity.Text = "Intensity"
        ' 
        ' tambient
        ' 
        tambient.AccentColor = SystemColors.HotTrack
        tambient.BackColor = SystemColors.Control
        tambient.DisplayFormat = "0.00%"
        tambient.InputScale = 0.01R
        tambient.LargeChange = 0.1R
        tambient.Location = New Point(87, 24)
        tambient.Maximum = 2R
        tambient.MinimumSize = New Size(100, 24)
        tambient.Name = "tambient"
        tambient.ShowTicks = True
        tambient.Size = New Size(224, 28)
        tambient.SmallChange = 0.05R
        tambient.TabIndex = 0
        tambient.TextBoxTextAlign = HorizontalAlignment.Right
        tambient.ThumbColor = SystemColors.HotTrack
        tambient.ThumbRadius = 4F
        tambient.TickFrequency = 0.25R
        ToolTip1.SetToolTip(tambient, "Adjust ambient light intensity.")
        tambient.TrackColor = SystemColors.ControlDark
        ' 
        ' lblGroundLvl
        ' 
        lblGroundLvl.AutoSize = True
        lblGroundLvl.Location = New Point(11, 60)
        lblGroundLvl.Name = "lblGroundLvl"
        lblGroundLvl.Size = New Size(74, 15)
        lblGroundLvl.TabIndex = 1
        lblGroundLvl.Text = "Ground level"
        ' 
        ' tGroundLevel
        ' 
        tGroundLevel.AccentColor = SystemColors.HotTrack
        tGroundLevel.BackColor = SystemColors.Control
        tGroundLevel.DisplayFormat = "0.00%"
        tGroundLevel.InputScale = 0.01R
        tGroundLevel.LargeChange = 0.1R
        tGroundLevel.Location = New Point(87, 54)
        tGroundLevel.Maximum = 1R
        tGroundLevel.MinimumSize = New Size(100, 24)
        tGroundLevel.Name = "tGroundLevel"
        tGroundLevel.ShowTicks = True
        tGroundLevel.Size = New Size(224, 28)
        tGroundLevel.SmallChange = 0.05R
        tGroundLevel.TabIndex = 1
        tGroundLevel.TextBoxTextAlign = HorizontalAlignment.Right
        tGroundLevel.ThumbColor = SystemColors.HotTrack
        tGroundLevel.ThumbRadius = 4F
        tGroundLevel.TickFrequency = 0.25R
        ToolTip1.SetToolTip(tGroundLevel, "Brightness of the lower hemisphere as a fraction of the sky, in RADIANCE: 100% is a flat ambient, 0% a black ground. The tint beside it only colours that light, it does not brighten it.")
        tGroundLevel.TrackColor = SystemColors.ControlDark
        ' 
        ' btnAmbSky
        ' 
        btnAmbSky.Location = New Point(317, 23)
        btnAmbSky.Name = "btnAmbSky"
        btnAmbSky.Size = New Size(90, 28)
        btnAmbSky.TabIndex = 2
        ToolTip1.SetToolTip(btnAmbSky, "Ambient color when a surface faces UP (world +Z). Engine ambient is normal-dependent.")
        btnAmbSky.UseVisualStyleBackColor = False
        ' 
        ' btnAmbGround
        ' 
        btnAmbGround.Location = New Point(317, 53)
        btnAmbGround.Name = "btnAmbGround"
        btnAmbGround.Size = New Size(90, 28)
        btnAmbGround.TabIndex = 3
        ToolTip1.SetToolTip(btnAmbGround, "Ambient color when a surface faces DOWN (world -Z) -- ground bounce.")
        btnAmbGround.UseVisualStyleBackColor = False
        ' 
        ' grpBackground
        ' 
        grpBackground.Controls.Add(lblBackground)
        grpBackground.Controls.Add(cmbBackground)
        grpBackground.Location = New Point(444, 110)
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
        lblBackground.Size = New Size(36, 15)
        lblBackground.TabIndex = 0
        lblBackground.Text = "Color"
        ' 
        ' cmbBackground
        ' 
        cmbBackground.Dibuja = False
        cmbBackground.DrawMode = DrawMode.OwnerDrawFixed
        cmbBackground.DropDownStyle = ComboBoxStyle.DropDownList
        cmbBackground.Location = New Point(87, 28)
        cmbBackground.Name = "cmbBackground"
        cmbBackground.SelectedColor = Color.Black
        cmbBackground.Size = New Size(320, 24)
        cmbBackground.TabIndex = 0
        ToolTip1.SetToolTip(cmbBackground, "Select the preview background color.")
        ' 
        ' grpShadows
        ' 
        grpShadows.Controls.Add(chkShadows)
        grpShadows.Controls.Add(lblShadowVram)
        grpShadows.Controls.Add(chkDepth16)
        grpShadows.Controls.Add(chkGroundShadow)
        grpShadows.Controls.Add(chkLightsFollowCamera)
        grpShadows.Controls.Add(lblShadowQuality)
        grpShadows.Controls.Add(cmbShadowQuality)
        grpShadows.Controls.Add(lblShadowSoft)
        grpShadows.Controls.Add(tShadowSoft)
        grpShadows.Controls.Add(lblShadowStrength)
        grpShadows.Controls.Add(tShadowStrength)
        grpShadows.Location = New Point(444, 184)
        grpShadows.Name = "grpShadows"
        grpShadows.Size = New Size(418, 174)
        grpShadows.TabIndex = 7
        grpShadows.TabStop = False
        grpShadows.Text = "Shadows"
        ' 
        ' chkShadows
        ' 
        chkShadows.AutoSize = True
        chkShadows.Location = New Point(11, 26)
        chkShadows.Name = "chkShadows"
        chkShadows.Size = New Size(98, 19)
        chkShadows.TabIndex = 0
        chkShadows.Text = "Cast shadows"
        ' 
        ' lblShadowVram
        ' 
        lblShadowVram.AutoSize = True
        lblShadowVram.Location = New Point(317, 60)
        lblShadowVram.Name = "lblShadowVram"
        lblShadowVram.Size = New Size(0, 15)
        lblShadowVram.TabIndex = 5
        ' 
        ' chkDepth16
        ' 
        chkDepth16.AutoSize = True
        chkDepth16.Location = New Point(350, 60)
        chkDepth16.Name = "chkDepth16"
        chkDepth16.Size = New Size(57, 19)
        chkDepth16.TabIndex = 9
        chkDepth16.Text = "16-bit"
        ToolTip1.SetToolTip(chkDepth16, resources.GetString("chkDepth16.ToolTip"))
        ' 
        ' chkGroundShadow
        ' 
        chkGroundShadow.AutoSize = True
        chkGroundShadow.Location = New Point(11, 146)
        chkGroundShadow.Name = "chkGroundShadow"
        chkGroundShadow.Size = New Size(147, 19)
        chkGroundShadow.TabIndex = 4
        chkGroundShadow.Text = "Shadow on the ground"
        ' 
        ' lblShadowQuality
        ' 
        lblShadowQuality.AutoSize = True
        lblShadowQuality.Location = New Point(11, 60)
        lblShadowQuality.Name = "lblShadowQuality"
        lblShadowQuality.Size = New Size(45, 15)
        lblShadowQuality.TabIndex = 9
        lblShadowQuality.Text = "Quality"
        ' 
        ' cmbShadowQuality
        ' 
        cmbShadowQuality.DropDownStyle = ComboBoxStyle.DropDownList
        cmbShadowQuality.Location = New Point(87, 56)
        cmbShadowQuality.Name = "cmbShadowQuality"
        cmbShadowQuality.Size = New Size(254, 23)
        cmbShadowQuality.TabIndex = 1
        ' 
        ' lblShadowSoft
        ' 
        lblShadowSoft.AutoSize = True
        lblShadowSoft.Location = New Point(11, 90)
        lblShadowSoft.Name = "lblShadowSoft"
        lblShadowSoft.Size = New Size(51, 15)
        lblShadowSoft.TabIndex = 10
        lblShadowSoft.Text = "Softness"
        ' 
        ' tShadowSoft
        ' 
        tShadowSoft.AccentColor = SystemColors.HotTrack
        tShadowSoft.BackColor = SystemColors.Control
        tShadowSoft.DisplayFormat = "0.0"
        tShadowSoft.LargeChange = 1R
        tShadowSoft.Location = New Point(87, 84)
        tShadowSoft.Maximum = 4R
        tShadowSoft.MinimumSize = New Size(100, 24)
        tShadowSoft.Name = "tShadowSoft"
        tShadowSoft.ShowTicks = True
        tShadowSoft.Size = New Size(320, 28)
        tShadowSoft.SmallChange = 0.5R
        tShadowSoft.TabIndex = 2
        tShadowSoft.TextBoxTextAlign = HorizontalAlignment.Right
        tShadowSoft.ThumbColor = SystemColors.HotTrack
        tShadowSoft.ThumbRadius = 4F
        tShadowSoft.TickFrequency = 1R
        tShadowSoft.TrackColor = SystemColors.ControlDark
        ' 
        ' lblShadowStrength
        ' 
        lblShadowStrength.AutoSize = True
        lblShadowStrength.Location = New Point(11, 120)
        lblShadowStrength.Name = "lblShadowStrength"
        lblShadowStrength.Size = New Size(54, 15)
        lblShadowStrength.TabIndex = 11
        lblShadowStrength.Text = "Darkness"
        ' 
        ' tShadowStrength
        ' 
        tShadowStrength.AccentColor = SystemColors.HotTrack
        tShadowStrength.BackColor = SystemColors.Control
        tShadowStrength.DisplayFormat = "0.00%"
        tShadowStrength.InputScale = 0.01R
        tShadowStrength.LargeChange = 0.1R
        tShadowStrength.Location = New Point(87, 114)
        tShadowStrength.Maximum = 1R
        tShadowStrength.MinimumSize = New Size(100, 24)
        tShadowStrength.Name = "tShadowStrength"
        tShadowStrength.ShowTicks = True
        tShadowStrength.Size = New Size(320, 28)
        tShadowStrength.SmallChange = 0.05R
        tShadowStrength.TabIndex = 3
        tShadowStrength.TextBoxTextAlign = HorizontalAlignment.Right
        tShadowStrength.ThumbColor = SystemColors.HotTrack
        tShadowStrength.ThumbRadius = 4F
        tShadowStrength.TickFrequency = 0.25R
        tShadowStrength.TrackColor = SystemColors.ControlDark
        ' 
        ' TabsMain
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
        ' TabLights
        ' 
        TabLights.AutoScroll = True
        TabLights.BackColor = SystemColors.Control
        TabLights.Controls.Add(grpBackground)
        TabLights.Controls.Add(grpAmbient)
        TabLights.Controls.Add(grpShadows)
        TabLights.Controls.Add(btnReset)
        TabLights.Controls.Add(grpPresets)
        TabLights.Controls.Add(grpBack)
        TabLights.Controls.Add(grpFillR)
        TabLights.Controls.Add(grpFillL)
        TabLights.Controls.Add(grpKey)
        TabLights.Location = New Point(4, 24)
        TabLights.Name = "TabLights"
        TabLights.Padding = New Padding(3)
        TabLights.Size = New Size(866, 540)
        TabLights.TabIndex = 0
        TabLights.Text = "Lights and shadows"
        ' 
        ' TabRender
        ' 
        TabRender.AutoScroll = True
        TabRender.BackColor = SystemColors.Control
        TabRender.Controls.Add(grpNormals)
        TabRender.Controls.Add(grpWeld)
        TabRender.Controls.Add(grpSkin)
        TabRender.Controls.Add(grpCamera)
        TabRender.Controls.Add(grpFloor)
        TabRender.Controls.Add(btnResetRender)
        TabRender.Location = New Point(4, 24)
        TabRender.Name = "TabRender"
        TabRender.Padding = New Padding(3)
        TabRender.Size = New Size(866, 540)
        TabRender.TabIndex = 1
        TabRender.Text = "Rendering"
        ' 
        ' grpNormals
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
        grpNormals.Location = New Point(8, 132)
        grpNormals.Name = "grpNormals"
        grpNormals.Size = New Size(418, 144)
        grpNormals.TabIndex = 0
        grpNormals.TabStop = False
        grpNormals.Text = "Normals and tangents"
        ' 
        ' chkRecalcNormals
        ' 
        chkRecalcNormals.AutoSize = True
        chkRecalcNormals.Location = New Point(11, 24)
        chkRecalcNormals.Name = "chkRecalcNormals"
        chkRecalcNormals.Size = New Size(132, 19)
        chkRecalcNormals.TabIndex = 0
        chkRecalcNormals.Text = "Recalculate normals"
        ToolTip1.SetToolTip(chkRecalcNormals, resources.GetString("chkRecalcNormals.ToolTip"))
        ' 
        ' chkRepairNaN
        ' 
        chkRepairNaN.AutoSize = True
        chkRepairNaN.Location = New Point(11, 48)
        chkRepairNaN.Name = "chkRepairNaN"
        chkRepairNaN.Size = New Size(183, 19)
        chkRepairNaN.TabIndex = 1
        chkRepairNaN.Text = "Repair NaN normals/tangents"
        ToolTip1.SetToolTip(chkRepairNaN, "Repair invalid NaN values found in recalculated tangent-space data.")
        ' 
        ' chkNormalize
        ' 
        chkNormalize.AutoSize = True
        chkNormalize.Location = New Point(221, 24)
        chkNormalize.Name = "chkNormalize"
        chkNormalize.Size = New Size(124, 19)
        chkNormalize.TabIndex = 2
        chkNormalize.Text = "Normalize outputs"
        ToolTip1.SetToolTip(chkNormalize, "Normalize recalculated tangent-space vectors.")
        ' 
        ' chkDeterministic
        ' 
        chkDeterministic.AutoSize = True
        chkDeterministic.Location = New Point(221, 48)
        chkDeterministic.Name = "chkDeterministic"
        chkDeterministic.Size = New Size(159, 19)
        chkDeterministic.TabIndex = 3
        chkDeterministic.Text = "Deterministic on collapse"
        ToolTip1.SetToolTip(chkDeterministic, resources.GetString("chkDeterministic.ToolTip"))
        ' 
        ' chkSmoothSeams
        ' 
        chkSmoothSeams.AutoSize = True
        chkSmoothSeams.Location = New Point(11, 76)
        chkSmoothSeams.Name = "chkSmoothSeams"
        chkSmoothSeams.Size = New Size(145, 19)
        chkSmoothSeams.TabIndex = 4
        chkSmoothSeams.Text = "Smooth seam normals"
        ' 
        ' lblSeamAngle
        ' 
        lblSeamAngle.AutoSize = True
        lblSeamAngle.Location = New Point(221, 78)
        lblSeamAngle.Name = "lblSeamAngle"
        lblSeamAngle.Size = New Size(68, 15)
        lblSeamAngle.TabIndex = 5
        lblSeamAngle.Text = "Seam angle"
        ' 
        ' nudSeamAngle
        ' 
        nudSeamAngle.DecimalPlaces = 1
        nudSeamAngle.Location = New Point(300, 74)
        nudSeamAngle.Maximum = New Decimal(New Integer() {180, 0, 0, 0})
        nudSeamAngle.Name = "nudSeamAngle"
        nudSeamAngle.Size = New Size(80, 23)
        nudSeamAngle.TabIndex = 5
        nudSeamAngle.TextAlign = HorizontalAlignment.Right
        ' 
        ' lblEpsPos
        ' 
        lblEpsPos.AutoSize = True
        lblEpsPos.Location = New Point(11, 106)
        lblEpsPos.Name = "lblEpsPos"
        lblEpsPos.Size = New Size(91, 15)
        lblEpsPos.TabIndex = 6
        lblEpsPos.Text = "Position epsilon"
        ' 
        ' nudEpsPos
        ' 
        nudEpsPos.DecimalPlaces = 12
        nudEpsPos.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        nudEpsPos.Location = New Point(130, 102)
        nudEpsPos.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudEpsPos.Name = "nudEpsPos"
        nudEpsPos.Size = New Size(128, 23)
        nudEpsPos.TabIndex = 5
        nudEpsPos.TextAlign = HorizontalAlignment.Right
        ToolTip1.SetToolTip(nudEpsPos, "Degenerate-triangle threshold, as a LENGTH in model units: a face whose tangent direction is shorter than this is discarded. 0 (default) matches BodySlide.")
        ' 
        ' grpWeld
        ' 
        grpWeld.Controls.Add(chkWelding)
        grpWeld.Controls.Add(rbWeldBoth)
        grpWeld.Controls.Add(rbWeldPosOnly)
        grpWeld.Controls.Add(lblWeldPos)
        grpWeld.Controls.Add(nudWeldPos)
        grpWeld.Controls.Add(lblWeldUv)
        grpWeld.Controls.Add(nudWeldUv)
        grpWeld.Location = New Point(8, 282)
        grpWeld.Name = "grpWeld"
        grpWeld.Size = New Size(418, 148)
        grpWeld.TabIndex = 1
        grpWeld.TabStop = False
        grpWeld.Text = "Welding"
        ' 
        ' chkWelding
        ' 
        chkWelding.AutoSize = True
        chkWelding.Location = New Point(11, 24)
        chkWelding.Name = "chkWelding"
        chkWelding.Size = New Size(160, 19)
        chkWelding.TabIndex = 0
        chkWelding.Text = "Weld vertices for normals"
        ToolTip1.SetToolTip(chkWelding, "Temporarily weld matching vertices before recalculating normals.")
        ' 
        ' rbWeldBoth
        ' 
        rbWeldBoth.AutoSize = True
        rbWeldBoth.Location = New Point(11, 50)
        rbWeldBoth.Name = "rbWeldBoth"
        rbWeldBoth.Size = New Size(130, 19)
        rbWeldBoth.TabIndex = 1
        rbWeldBoth.Text = "By position and UVs"
        ToolTip1.SetToolTip(rbWeldBoth, "Weld vertices only when both position and UVs match.")
        ' 
        ' rbWeldPosOnly
        ' 
        rbWeldPosOnly.AutoSize = True
        rbWeldPosOnly.Location = New Point(221, 50)
        rbWeldPosOnly.Name = "rbWeldPosOnly"
        rbWeldPosOnly.Size = New Size(110, 19)
        rbWeldPosOnly.TabIndex = 2
        rbWeldPosOnly.Text = "By position only"
        ToolTip1.SetToolTip(rbWeldPosOnly, "Weld vertices using position only.")
        ' 
        ' lblWeldPos
        ' 
        lblWeldPos.AutoSize = True
        lblWeldPos.Location = New Point(11, 82)
        lblWeldPos.Name = "lblWeldPos"
        lblWeldPos.Size = New Size(97, 15)
        lblWeldPos.TabIndex = 3
        lblWeldPos.Text = "Weld pos epsilon"
        ' 
        ' nudWeldPos
        ' 
        nudWeldPos.DecimalPlaces = 12
        nudWeldPos.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        nudWeldPos.Location = New Point(130, 78)
        nudWeldPos.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudWeldPos.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        nudWeldPos.Name = "nudWeldPos"
        nudWeldPos.Size = New Size(128, 23)
        nudWeldPos.TabIndex = 3
        nudWeldPos.TextAlign = HorizontalAlignment.Right
        ToolTip1.SetToolTip(nudWeldPos, "Position epsilon used when welding vertices.")
        nudWeldPos.Value = New Decimal(New Integer() {1, 0, 0, 786432})
        ' 
        ' lblWeldUv
        ' 
        lblWeldUv.AutoSize = True
        lblWeldUv.Location = New Point(11, 110)
        lblWeldUv.Name = "lblWeldUv"
        lblWeldUv.Size = New Size(93, 15)
        lblWeldUv.TabIndex = 4
        lblWeldUv.Text = "Weld UV epsilon"
        ' 
        ' nudWeldUv
        ' 
        nudWeldUv.DecimalPlaces = 12
        nudWeldUv.Increment = New Decimal(New Integer() {5, 0, 0, 786432})
        nudWeldUv.Location = New Point(130, 106)
        nudWeldUv.Maximum = New Decimal(New Integer() {1, 0, 0, 196608})
        nudWeldUv.Minimum = New Decimal(New Integer() {1, 0, 0, 786432})
        nudWeldUv.Name = "nudWeldUv"
        nudWeldUv.Size = New Size(128, 23)
        nudWeldUv.TabIndex = 4
        nudWeldUv.TextAlign = HorizontalAlignment.Right
        ToolTip1.SetToolTip(nudWeldUv, "UV epsilon used when welding vertices.")
        nudWeldUv.Value = New Decimal(New Integer() {1, 0, 0, 786432})
        ' 
        ' grpSkin
        ' 
        grpSkin.Controls.Add(chkGpuSkinning)
        grpSkin.Controls.Add(chkSingleBone)
        grpSkin.Controls.Add(chkHiddenSegments)
        grpSkin.Location = New Point(8, 6)
        grpSkin.Name = "grpSkin"
        grpSkin.Size = New Size(418, 120)
        grpSkin.TabIndex = 2
        grpSkin.TabStop = False
        grpSkin.Text = "Skinning"
        ' 
        ' chkGpuSkinning
        ' 
        chkGpuSkinning.AutoSize = True
        chkGpuSkinning.Location = New Point(11, 24)
        chkGpuSkinning.Name = "chkGpuSkinning"
        chkGpuSkinning.Size = New Size(97, 19)
        chkGpuSkinning.TabIndex = 0
        chkGpuSkinning.Text = "GPU skinning"
        ToolTip1.SetToolTip(chkGpuSkinning, "Toggles GPU Skinning (otherwise CPU Skinning) best performance will depend on your computer specs")
        ' 
        ' chkSingleBone
        ' 
        chkSingleBone.AutoSize = True
        chkSingleBone.Location = New Point(11, 48)
        chkSingleBone.Name = "chkSingleBone"
        chkSingleBone.Size = New Size(136, 19)
        chkSingleBone.TabIndex = 1
        chkSingleBone.Text = "Single bone skinning"
        ToolTip1.SetToolTip(chkSingleBone, "Use single-bone skinning in rendering and preview.")
        ' 
        ' chkHiddenSegments
        ' 
        chkHiddenSegments.AutoSize = True
        chkHiddenSegments.Location = New Point(11, 72)
        chkHiddenSegments.Name = "chkHiddenSegments"
        chkHiddenSegments.Size = New Size(147, 19)
        chkHiddenSegments.TabIndex = 2
        chkHiddenSegments.Text = "Draw hidden segments"
        ToolTip1.SetToolTip(chkHiddenSegments, "Draw normally-hidden geometry segments (e.g. Pip-Boy forearm variant, occluded segments) in the viewport. WM inspection aid; does not affect exports.")
        ' 
        ' grpCamera
        ' 
        grpCamera.Controls.Add(chkResetAngles)
        grpCamera.Controls.Add(chkResetZoom)
        grpCamera.Controls.Add(chkFreezeCamera)
        grpCamera.Location = New Point(440, 6)
        grpCamera.Name = "grpCamera"
        grpCamera.Size = New Size(418, 120)
        grpCamera.TabIndex = 3
        grpCamera.TabStop = False
        grpCamera.Text = "Camera"
        ' 
        ' chkResetAngles
        ' 
        chkResetAngles.AutoSize = True
        chkResetAngles.Location = New Point(11, 24)
        chkResetAngles.Name = "chkResetAngles"
        chkResetAngles.Size = New Size(142, 19)
        chkResetAngles.TabIndex = 0
        chkResetAngles.Text = "Reset rotation on load"
        ToolTip1.SetToolTip(chkResetAngles, "Reset camera rotation when loading a new project.")
        ' 
        ' chkResetZoom
        ' 
        chkResetZoom.AutoSize = True
        chkResetZoom.Location = New Point(11, 48)
        chkResetZoom.Name = "chkResetZoom"
        chkResetZoom.Size = New Size(188, 19)
        chkResetZoom.TabIndex = 1
        chkResetZoom.Text = "Reset to optimal zoom on load"
        ToolTip1.SetToolTip(chkResetZoom, "Reset the camera zoom to an optimal distance when loading a new project.")
        ' 
        ' chkFreezeCamera
        ' 
        chkFreezeCamera.AutoSize = True
        chkFreezeCamera.Location = New Point(11, 72)
        chkFreezeCamera.Name = "chkFreezeCamera"
        chkFreezeCamera.Size = New Size(259, 19)
        chkFreezeCamera.TabIndex = 2
        chkFreezeCamera.Text = "Completely freeze camera on model change"
        ToolTip1.SetToolTip(chkFreezeCamera, "Keep the camera fully frozen when the loaded NIF changes (be sure to uncheck it for different size nifs).")
        ' 
        ' grpFloor
        ' 
        grpFloor.Controls.Add(chkFloorEnabled)
        grpFloor.Controls.Add(lblFloorSize)
        grpFloor.Controls.Add(nudFloorSize)
        grpFloor.Controls.Add(lblFloorStep)
        grpFloor.Controls.Add(nudFloorStep)
        grpFloor.Controls.Add(lblFloorColor)
        grpFloor.Controls.Add(cmbFloorColor)
        grpFloor.Location = New Point(440, 132)
        grpFloor.Name = "grpFloor"
        grpFloor.Size = New Size(418, 144)
        grpFloor.TabIndex = 4
        grpFloor.TabStop = False
        grpFloor.Text = "Floor grid"
        ' 
        ' chkFloorEnabled
        ' 
        chkFloorEnabled.AutoSize = True
        chkFloorEnabled.Location = New Point(11, 24)
        chkFloorEnabled.Name = "chkFloorEnabled"
        chkFloorEnabled.Size = New Size(107, 19)
        chkFloorEnabled.TabIndex = 0
        chkFloorEnabled.Text = "Show floor grid"
        ToolTip1.SetToolTip(chkFloorEnabled, "Show the render grid in preview.")
        ' 
        ' lblFloorSize
        ' 
        lblFloorSize.AutoSize = True
        lblFloorSize.Location = New Point(11, 54)
        lblFloorSize.Name = "lblFloorSize"
        lblFloorSize.Size = New Size(27, 15)
        lblFloorSize.TabIndex = 1
        lblFloorSize.Text = "Size"
        ' 
        ' nudFloorSize
        ' 
        nudFloorSize.DecimalPlaces = 3
        nudFloorSize.Location = New Point(90, 50)
        nudFloorSize.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        nudFloorSize.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        nudFloorSize.Name = "nudFloorSize"
        nudFloorSize.Size = New Size(100, 23)
        nudFloorSize.TabIndex = 1
        nudFloorSize.TextAlign = HorizontalAlignment.Right
        ToolTip1.SetToolTip(nudFloorSize, "Total grid size.")
        nudFloorSize.Value = New Decimal(New Integer() {1, 0, 0, 0})
        ' 
        ' lblFloorStep
        ' 
        lblFloorStep.AutoSize = True
        lblFloorStep.Location = New Point(210, 54)
        lblFloorStep.Name = "lblFloorStep"
        lblFloorStep.Size = New Size(30, 15)
        lblFloorStep.TabIndex = 2
        lblFloorStep.Text = "Step"
        ' 
        ' nudFloorStep
        ' 
        nudFloorStep.DecimalPlaces = 3
        nudFloorStep.Location = New Point(280, 50)
        nudFloorStep.Maximum = New Decimal(New Integer() {100000, 0, 0, 0})
        nudFloorStep.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        nudFloorStep.Name = "nudFloorStep"
        nudFloorStep.Size = New Size(100, 23)
        nudFloorStep.TabIndex = 2
        nudFloorStep.TextAlign = HorizontalAlignment.Right
        ToolTip1.SetToolTip(nudFloorStep, "Distance between grid lines.")
        nudFloorStep.Value = New Decimal(New Integer() {1, 0, 0, 0})
        ' 
        ' lblFloorColor
        ' 
        lblFloorColor.AutoSize = True
        lblFloorColor.Location = New Point(11, 88)
        lblFloorColor.Name = "lblFloorColor"
        lblFloorColor.Size = New Size(59, 15)
        lblFloorColor.TabIndex = 3
        lblFloorColor.Text = "Grid color"
        ' 
        ' cmbFloorColor
        ' 
        cmbFloorColor.Dibuja = False
        cmbFloorColor.DrawMode = DrawMode.OwnerDrawFixed
        cmbFloorColor.DropDownStyle = ComboBoxStyle.DropDownList
        cmbFloorColor.Location = New Point(90, 84)
        cmbFloorColor.Name = "cmbFloorColor"
        cmbFloorColor.SelectedColor = Color.Black
        cmbFloorColor.Size = New Size(290, 24)
        cmbFloorColor.TabIndex = 3
        ToolTip1.SetToolTip(cmbFloorColor, "Color of the grid lines.")
        ' 
        ' btnResetRender
        ' 
        btnResetRender.Location = New Point(440, 282)
        btnResetRender.Name = "btnResetRender"
        btnResetRender.Size = New Size(418, 27)
        btnResetRender.TabIndex = 5
        btnResetRender.Text = "Reset rendering to defaults"
        ToolTip1.SetToolTip(btnResetRender, "Reset every setting on this tab -- normals, welding, skinning, camera and floor grid -- to its default. Lights and shadows are on the other tab and are not touched.")
        btnResetRender.UseVisualStyleBackColor = True
        ' 
        ' LightRigForm
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        AutoScroll = True
        ClientSize = New Size(874, 568)
        Controls.Add(TabsMain)
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
        grpPresets.ResumeLayout(False)
        grpPresets.PerformLayout()
        grpAmbient.ResumeLayout(False)
        grpAmbient.PerformLayout()
        grpBackground.ResumeLayout(False)
        grpBackground.PerformLayout()
        grpShadows.ResumeLayout(False)
        grpShadows.PerformLayout()
        TabsMain.ResumeLayout(False)
        TabLights.ResumeLayout(False)
        TabRender.ResumeLayout(False)
        grpNormals.ResumeLayout(False)
        grpNormals.PerformLayout()
        CType(nudSeamAngle, ComponentModel.ISupportInitialize).EndInit()
        CType(nudEpsPos, ComponentModel.ISupportInitialize).EndInit()
        grpWeld.ResumeLayout(False)
        grpWeld.PerformLayout()
        CType(nudWeldPos, ComponentModel.ISupportInitialize).EndInit()
        CType(nudWeldUv, ComponentModel.ISupportInitialize).EndInit()
        grpSkin.ResumeLayout(False)
        grpSkin.PerformLayout()
        grpCamera.ResumeLayout(False)
        grpCamera.PerformLayout()
        grpFloor.ResumeLayout(False)
        grpFloor.PerformLayout()
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
    Friend WithEvents chkCastKey As CheckBox
    Friend WithEvents chkCastFillL As CheckBox
    Friend WithEvents chkCastFillR As CheckBox
    Friend WithEvents chkCastBack As CheckBox
    Friend WithEvents lblShadowVram As Label
    Friend WithEvents chkDepth16 As CheckBox
    Friend WithEvents chkGroundShadow As CheckBox
    Friend WithEvents chkLightsFollowCamera As CheckBox
    Friend WithEvents lblShadowQuality As Label
    Friend WithEvents cmbShadowQuality As ComboBox
    Friend WithEvents lblShadowSoft As Label
    Friend WithEvents tShadowSoft As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblShadowStrength As Label
    Friend WithEvents tShadowStrength As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents btnAmbSky As Button
    Friend WithEvents btnAmbGround As Button
    Friend WithEvents btnKeyColor As Button
    Friend WithEvents btnFillLColor As Button
    Friend WithEvents btnFillRColor As Button
    Friend WithEvents btnBackColor As Button
    Friend WithEvents tGroundLevel As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents lblIntensity As Label
    Friend WithEvents lblGroundLvl As Label
End Class
