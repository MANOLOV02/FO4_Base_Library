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
        tbKey = New FO4_Base_Library.TinySliderTextBox()
        nudK_F = New NumericUpDown()
        nudK_R = New NumericUpDown()
        nudK_U = New NumericUpDown()
        nudK_B = New NumericUpDown()
        nudK_L = New NumericUpDown()
        nudK_D = New NumericUpDown()
        lblK_U = New Label()
        lblK_R = New Label()
        lblK_F = New Label()
        lblK_D = New Label()
        lblK_L = New Label()
        lblK_B = New Label()
        grpFillL = New GroupBox()
        tbFillL = New FO4_Base_Library.TinySliderTextBox()
        nudL_F = New NumericUpDown()
        nudL_R = New NumericUpDown()
        nudL_U = New NumericUpDown()
        nudL_B = New NumericUpDown()
        nudL_L = New NumericUpDown()
        nudL_D = New NumericUpDown()
        Label1 = New Label()
        Label2 = New Label()
        Label3 = New Label()
        Label4 = New Label()
        Label5 = New Label()
        Label6 = New Label()
        grpFillR = New GroupBox()
        tbFillR = New FO4_Base_Library.TinySliderTextBox()
        nudR_F = New NumericUpDown()
        nudR_R = New NumericUpDown()
        nudR_U = New NumericUpDown()
        nudR_B = New NumericUpDown()
        nudR_L = New NumericUpDown()
        nudR_D = New NumericUpDown()
        Label7 = New Label()
        Label8 = New Label()
        Label9 = New Label()
        Label10 = New Label()
        Label11 = New Label()
        Label12 = New Label()
        grpBack = New GroupBox()
        tbBack = New FO4_Base_Library.TinySliderTextBox()
        nudB_F = New NumericUpDown()
        nudB_R = New NumericUpDown()
        nudB_U = New NumericUpDown()
        nudB_B = New NumericUpDown()
        nudB_L = New NumericUpDown()
        nudB_D = New NumericUpDown()
        Label13 = New Label()
        Label14 = New Label()
        Label15 = New Label()
        Label16 = New Label()
        Label17 = New Label()
        Label18 = New Label()
        btnReset = New Button()
        GroupBox1 = New GroupBox()
        tambient = New FO4_Base_Library.TinySliderTextBox()
        grpBackground = New GroupBox()
        lblBackground = New Label()
        cmbBackground = New ColorComboBox()
        ToolTip1 = New ToolTip(components)
        grpKey.SuspendLayout()
        CType(nudK_F, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudK_R, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudK_U, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudK_B, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudK_L, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudK_D, ComponentModel.ISupportInitialize).BeginInit()
        grpFillL.SuspendLayout()
        CType(nudL_F, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_R, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_U, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_B, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_L, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudL_D, ComponentModel.ISupportInitialize).BeginInit()
        grpFillR.SuspendLayout()
        CType(nudR_F, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_R, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_U, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_B, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_L, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudR_D, ComponentModel.ISupportInitialize).BeginInit()
        grpBack.SuspendLayout()
        CType(nudB_F, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_R, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_U, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_B, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_L, ComponentModel.ISupportInitialize).BeginInit()
        CType(nudB_D, ComponentModel.ISupportInitialize).BeginInit()
        GroupBox1.SuspendLayout()
        grpBackground.SuspendLayout()
        SuspendLayout()
        '
        ' grpKey
        '
        grpKey.Controls.Add(tbKey)
        grpKey.Controls.Add(nudK_F)
        grpKey.Controls.Add(nudK_R)
        grpKey.Controls.Add(nudK_U)
        grpKey.Controls.Add(nudK_B)
        grpKey.Controls.Add(nudK_L)
        grpKey.Controls.Add(nudK_D)
        grpKey.Controls.Add(lblK_U)
        grpKey.Controls.Add(lblK_R)
        grpKey.Controls.Add(lblK_F)
        grpKey.Controls.Add(lblK_D)
        grpKey.Controls.Add(lblK_L)
        grpKey.Controls.Add(lblK_B)
        grpKey.Location = New Point(12, 12)
        grpKey.Name = "grpKey"
        grpKey.Size = New Size(418, 150)
        grpKey.TabIndex = 0
        grpKey.TabStop = False
        grpKey.Text = "Frontal Light"
        '
        ' tbKey
        '
        tbKey.Location = New Point(11, 30)
        tbKey.Minimum = 0R
        tbKey.Maximum = 1.5R
        tbKey.DisplayFormat = "0.00%"
        tbKey.InputScale = 0.01R
        tbKey.SmallChange = 0.05R
        tbKey.LargeChange = 0.1R
        tbKey.TickFrequency = 0.1R
        tbKey.ShowTicks = True
        tbKey.Name = "tbKey"
        tbKey.Size = New Size(389, 28)
        tbKey.TabIndex = 0
        '
        ' nudK_F
        '
        nudK_F.DecimalPlaces = 2
        nudK_F.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudK_F.Location = New Point(340, 110)
        nudK_F.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudK_F.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudK_F.Name = "nudK_F"
        nudK_F.Size = New Size(60, 23)
        nudK_F.TabIndex = 12
        nudK_F.TextAlign = HorizontalAlignment.Right
        '
        ' nudK_R
        '
        nudK_R.DecimalPlaces = 2
        nudK_R.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudK_R.Location = New Point(200, 110)
        nudK_R.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudK_R.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudK_R.Name = "nudK_R"
        nudK_R.Size = New Size(60, 23)
        nudK_R.TabIndex = 10
        nudK_R.TextAlign = HorizontalAlignment.Right
        '
        ' nudK_U
        '
        nudK_U.DecimalPlaces = 2
        nudK_U.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudK_U.Location = New Point(60, 110)
        nudK_U.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudK_U.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudK_U.Name = "nudK_U"
        nudK_U.Size = New Size(60, 23)
        nudK_U.TabIndex = 8
        nudK_U.TextAlign = HorizontalAlignment.Right
        '
        ' nudK_B
        '
        nudK_B.DecimalPlaces = 2
        nudK_B.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudK_B.Location = New Point(340, 80)
        nudK_B.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudK_B.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudK_B.Name = "nudK_B"
        nudK_B.Size = New Size(60, 23)
        nudK_B.TabIndex = 6
        nudK_B.TextAlign = HorizontalAlignment.Right
        '
        ' nudK_L
        '
        nudK_L.DecimalPlaces = 2
        nudK_L.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudK_L.Location = New Point(200, 80)
        nudK_L.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudK_L.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudK_L.Name = "nudK_L"
        nudK_L.Size = New Size(60, 23)
        nudK_L.TabIndex = 4
        nudK_L.TextAlign = HorizontalAlignment.Right
        '
        ' nudK_D
        '
        nudK_D.DecimalPlaces = 2
        nudK_D.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudK_D.Location = New Point(60, 80)
        nudK_D.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudK_D.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudK_D.Name = "nudK_D"
        nudK_D.Size = New Size(60, 23)
        nudK_D.TabIndex = 2
        nudK_D.TextAlign = HorizontalAlignment.Right
        '
        ' lblK_U
        '
        lblK_U.AutoSize = True
        lblK_U.Location = New Point(20, 112)
        lblK_U.Name = "lblK_U"
        lblK_U.Size = New Size(22, 15)
        lblK_U.TabIndex = 7
        lblK_U.Text = "Up"
        '
        ' lblK_R
        '
        lblK_R.AutoSize = True
        lblK_R.Location = New Point(160, 112)
        lblK_R.Name = "lblK_R"
        lblK_R.Size = New Size(35, 15)
        lblK_R.TabIndex = 9
        lblK_R.Text = "Right"
        '
        ' lblK_F
        '
        lblK_F.AutoSize = True
        lblK_F.Location = New Point(300, 112)
        lblK_F.Name = "lblK_F"
        lblK_F.Size = New Size(50, 15)
        lblK_F.TabIndex = 11
        lblK_F.Text = "Forward"
        '
        ' lblK_D
        '
        lblK_D.AutoSize = True
        lblK_D.Location = New Point(20, 82)
        lblK_D.Name = "lblK_D"
        lblK_D.Size = New Size(38, 15)
        lblK_D.TabIndex = 1
        lblK_D.Text = "Down"
        '
        ' lblK_L
        '
        lblK_L.AutoSize = True
        lblK_L.Location = New Point(160, 82)
        lblK_L.Name = "lblK_L"
        lblK_L.Size = New Size(27, 15)
        lblK_L.TabIndex = 3
        lblK_L.Text = "Left"
        '
        ' lblK_B
        '
        lblK_B.AutoSize = True
        lblK_B.Location = New Point(300, 82)
        lblK_B.Name = "lblK_B"
        lblK_B.Size = New Size(32, 15)
        lblK_B.TabIndex = 5
        lblK_B.Text = "Back"
        '
        ' grpFillL
        '
        grpFillL.Controls.Add(tbFillL)
        grpFillL.Controls.Add(nudL_F)
        grpFillL.Controls.Add(nudL_R)
        grpFillL.Controls.Add(nudL_U)
        grpFillL.Controls.Add(nudL_B)
        grpFillL.Controls.Add(nudL_L)
        grpFillL.Controls.Add(nudL_D)
        grpFillL.Controls.Add(Label1)
        grpFillL.Controls.Add(Label2)
        grpFillL.Controls.Add(Label3)
        grpFillL.Controls.Add(Label4)
        grpFillL.Controls.Add(Label5)
        grpFillL.Controls.Add(Label6)
        grpFillL.Location = New Point(12, 168)
        grpFillL.Name = "grpFillL"
        grpFillL.Size = New Size(418, 150)
        grpFillL.TabIndex = 1
        grpFillL.TabStop = False
        grpFillL.Text = "Left Light"
        '
        ' tbFillL
        '
        tbFillL.Location = New Point(11, 30)
        tbFillL.Minimum = 0R
        tbFillL.Maximum = 1.5R
        tbFillL.DisplayFormat = "0.00%"
        tbFillL.InputScale = 0.01R
        tbFillL.SmallChange = 0.05R
        tbFillL.LargeChange = 0.1R
        tbFillL.TickFrequency = 0.1R
        tbFillL.ShowTicks = True
        tbFillL.Name = "tbFillL"
        tbFillL.Size = New Size(389, 28)
        tbFillL.TabIndex = 0
        '
        ' nudL_F
        '
        nudL_F.DecimalPlaces = 2
        nudL_F.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudL_F.Location = New Point(340, 110)
        nudL_F.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudL_F.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudL_F.Name = "nudL_F"
        nudL_F.Size = New Size(60, 23)
        nudL_F.TabIndex = 12
        nudL_F.TextAlign = HorizontalAlignment.Right
        '
        ' nudL_R
        '
        nudL_R.DecimalPlaces = 2
        nudL_R.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudL_R.Location = New Point(200, 110)
        nudL_R.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudL_R.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudL_R.Name = "nudL_R"
        nudL_R.Size = New Size(60, 23)
        nudL_R.TabIndex = 10
        nudL_R.TextAlign = HorizontalAlignment.Right
        '
        ' nudL_U
        '
        nudL_U.DecimalPlaces = 2
        nudL_U.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudL_U.Location = New Point(60, 110)
        nudL_U.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudL_U.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudL_U.Name = "nudL_U"
        nudL_U.Size = New Size(60, 23)
        nudL_U.TabIndex = 8
        nudL_U.TextAlign = HorizontalAlignment.Right
        '
        ' nudL_B
        '
        nudL_B.DecimalPlaces = 2
        nudL_B.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudL_B.Location = New Point(340, 80)
        nudL_B.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudL_B.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudL_B.Name = "nudL_B"
        nudL_B.Size = New Size(60, 23)
        nudL_B.TabIndex = 6
        nudL_B.TextAlign = HorizontalAlignment.Right
        '
        ' nudL_L
        '
        nudL_L.DecimalPlaces = 2
        nudL_L.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudL_L.Location = New Point(200, 80)
        nudL_L.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudL_L.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudL_L.Name = "nudL_L"
        nudL_L.Size = New Size(60, 23)
        nudL_L.TabIndex = 4
        nudL_L.TextAlign = HorizontalAlignment.Right
        '
        ' nudL_D
        '
        nudL_D.DecimalPlaces = 2
        nudL_D.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudL_D.Location = New Point(60, 80)
        nudL_D.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudL_D.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudL_D.Name = "nudL_D"
        nudL_D.Size = New Size(60, 23)
        nudL_D.TabIndex = 2
        nudL_D.TextAlign = HorizontalAlignment.Right
        '
        ' Label1
        '
        Label1.AutoSize = True
        Label1.Location = New Point(20, 82)
        Label1.Name = "Label1"
        Label1.Size = New Size(38, 15)
        Label1.TabIndex = 1
        Label1.Text = "Down"
        '
        ' Label2
        '
        Label2.AutoSize = True
        Label2.Location = New Point(160, 82)
        Label2.Name = "Label2"
        Label2.Size = New Size(27, 15)
        Label2.TabIndex = 3
        Label2.Text = "Left"
        '
        ' Label3
        '
        Label3.AutoSize = True
        Label3.Location = New Point(300, 82)
        Label3.Name = "Label3"
        Label3.Size = New Size(32, 15)
        Label3.TabIndex = 5
        Label3.Text = "Back"
        '
        ' Label4
        '
        Label4.AutoSize = True
        Label4.Location = New Point(20, 112)
        Label4.Name = "Label4"
        Label4.Size = New Size(22, 15)
        Label4.TabIndex = 7
        Label4.Text = "Up"
        '
        ' Label5
        '
        Label5.AutoSize = True
        Label5.Location = New Point(160, 112)
        Label5.Name = "Label5"
        Label5.Size = New Size(35, 15)
        Label5.TabIndex = 9
        Label5.Text = "Right"
        '
        ' Label6
        '
        Label6.AutoSize = True
        Label6.Location = New Point(300, 112)
        Label6.Name = "Label6"
        Label6.Size = New Size(50, 15)
        Label6.TabIndex = 11
        Label6.Text = "Forward"
        '
        ' grpFillR
        '
        grpFillR.Controls.Add(tbFillR)
        grpFillR.Controls.Add(nudR_F)
        grpFillR.Controls.Add(nudR_R)
        grpFillR.Controls.Add(nudR_U)
        grpFillR.Controls.Add(nudR_B)
        grpFillR.Controls.Add(nudR_L)
        grpFillR.Controls.Add(nudR_D)
        grpFillR.Controls.Add(Label7)
        grpFillR.Controls.Add(Label8)
        grpFillR.Controls.Add(Label9)
        grpFillR.Controls.Add(Label10)
        grpFillR.Controls.Add(Label11)
        grpFillR.Controls.Add(Label12)
        grpFillR.Location = New Point(12, 324)
        grpFillR.Name = "grpFillR"
        grpFillR.Size = New Size(418, 150)
        grpFillR.TabIndex = 2
        grpFillR.TabStop = False
        grpFillR.Text = "Right Ligh"
        '
        ' tbFillR
        '
        tbFillR.Location = New Point(11, 30)
        tbFillR.Minimum = 0R
        tbFillR.Maximum = 1.5R
        tbFillR.DisplayFormat = "0.00%"
        tbFillR.InputScale = 0.01R
        tbFillR.SmallChange = 0.05R
        tbFillR.LargeChange = 0.1R
        tbFillR.TickFrequency = 0.1R
        tbFillR.ShowTicks = True
        tbFillR.Name = "tbFillR"
        tbFillR.Size = New Size(389, 28)
        tbFillR.TabIndex = 0
        '
        ' nudR_F
        '
        nudR_F.DecimalPlaces = 2
        nudR_F.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudR_F.Location = New Point(340, 110)
        nudR_F.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudR_F.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudR_F.Name = "nudR_F"
        nudR_F.Size = New Size(60, 23)
        nudR_F.TabIndex = 12
        nudR_F.TextAlign = HorizontalAlignment.Right
        '
        ' nudR_R
        '
        nudR_R.DecimalPlaces = 2
        nudR_R.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudR_R.Location = New Point(200, 110)
        nudR_R.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudR_R.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudR_R.Name = "nudR_R"
        nudR_R.Size = New Size(60, 23)
        nudR_R.TabIndex = 10
        nudR_R.TextAlign = HorizontalAlignment.Right
        '
        ' nudR_U
        '
        nudR_U.DecimalPlaces = 2
        nudR_U.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudR_U.Location = New Point(60, 110)
        nudR_U.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudR_U.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudR_U.Name = "nudR_U"
        nudR_U.Size = New Size(60, 23)
        nudR_U.TabIndex = 8
        nudR_U.TextAlign = HorizontalAlignment.Right
        '
        ' nudR_B
        '
        nudR_B.DecimalPlaces = 2
        nudR_B.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudR_B.Location = New Point(340, 80)
        nudR_B.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudR_B.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudR_B.Name = "nudR_B"
        nudR_B.Size = New Size(60, 23)
        nudR_B.TabIndex = 6
        nudR_B.TextAlign = HorizontalAlignment.Right
        '
        ' nudR_L
        '
        nudR_L.DecimalPlaces = 2
        nudR_L.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudR_L.Location = New Point(200, 80)
        nudR_L.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudR_L.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudR_L.Name = "nudR_L"
        nudR_L.Size = New Size(60, 23)
        nudR_L.TabIndex = 4
        nudR_L.TextAlign = HorizontalAlignment.Right
        '
        ' nudR_D
        '
        nudR_D.DecimalPlaces = 2
        nudR_D.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudR_D.Location = New Point(60, 80)
        nudR_D.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudR_D.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudR_D.Name = "nudR_D"
        nudR_D.Size = New Size(60, 23)
        nudR_D.TabIndex = 2
        nudR_D.TextAlign = HorizontalAlignment.Right
        '
        ' Label7
        '
        Label7.AutoSize = True
        Label7.Location = New Point(20, 82)
        Label7.Name = "Label7"
        Label7.Size = New Size(38, 15)
        Label7.TabIndex = 13
        Label7.Text = "Down"
        '
        ' Label8
        '
        Label8.AutoSize = True
        Label8.Location = New Point(160, 82)
        Label8.Name = "Label8"
        Label8.Size = New Size(27, 15)
        Label8.TabIndex = 14
        Label8.Text = "Left"
        '
        ' Label9
        '
        Label9.AutoSize = True
        Label9.Location = New Point(300, 82)
        Label9.Name = "Label9"
        Label9.Size = New Size(32, 15)
        Label9.TabIndex = 15
        Label9.Text = "Back"
        '
        ' Label10
        '
        Label10.AutoSize = True
        Label10.Location = New Point(20, 112)
        Label10.Name = "Label10"
        Label10.Size = New Size(22, 15)
        Label10.TabIndex = 16
        Label10.Text = "Up"
        '
        ' Label11
        '
        Label11.AutoSize = True
        Label11.Location = New Point(160, 112)
        Label11.Name = "Label11"
        Label11.Size = New Size(35, 15)
        Label11.TabIndex = 17
        Label11.Text = "Right"
        '
        ' Label12
        '
        Label12.AutoSize = True
        Label12.Location = New Point(300, 112)
        Label12.Name = "Label12"
        Label12.Size = New Size(50, 15)
        Label12.TabIndex = 18
        Label12.Text = "Forward"
        '
        ' grpBack
        '
        grpBack.Controls.Add(tbBack)
        grpBack.Controls.Add(nudB_F)
        grpBack.Controls.Add(nudB_R)
        grpBack.Controls.Add(nudB_U)
        grpBack.Controls.Add(nudB_B)
        grpBack.Controls.Add(nudB_L)
        grpBack.Controls.Add(nudB_D)
        grpBack.Controls.Add(Label13)
        grpBack.Controls.Add(Label14)
        grpBack.Controls.Add(Label15)
        grpBack.Controls.Add(Label16)
        grpBack.Controls.Add(Label17)
        grpBack.Controls.Add(Label18)
        grpBack.Location = New Point(436, 12)
        grpBack.Name = "grpBack"
        grpBack.Size = New Size(418, 150)
        grpBack.TabIndex = 3
        grpBack.TabStop = False
        grpBack.Text = "Back Light"
        '
        ' tbBack
        '
        tbBack.Location = New Point(11, 30)
        tbBack.Minimum = 0R
        tbBack.Maximum = 1.5R
        tbBack.DisplayFormat = "0.00%"
        tbBack.InputScale = 0.01R
        tbBack.SmallChange = 0.05R
        tbBack.LargeChange = 0.1R
        tbBack.TickFrequency = 0.1R
        tbBack.ShowTicks = True
        tbBack.Name = "tbBack"
        tbBack.Size = New Size(389, 28)
        tbBack.TabIndex = 0
        '
        ' nudB_F
        '
        nudB_F.DecimalPlaces = 2
        nudB_F.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudB_F.Location = New Point(340, 110)
        nudB_F.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudB_F.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudB_F.Name = "nudB_F"
        nudB_F.Size = New Size(60, 23)
        nudB_F.TabIndex = 2
        nudB_F.TextAlign = HorizontalAlignment.Right
        '
        ' nudB_R
        '
        nudB_R.DecimalPlaces = 2
        nudB_R.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudB_R.Location = New Point(200, 110)
        nudB_R.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudB_R.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudB_R.Name = "nudB_R"
        nudB_R.Size = New Size(60, 23)
        nudB_R.TabIndex = 3
        nudB_R.TextAlign = HorizontalAlignment.Right
        '
        ' nudB_U
        '
        nudB_U.DecimalPlaces = 2
        nudB_U.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudB_U.Location = New Point(60, 110)
        nudB_U.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudB_U.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudB_U.Name = "nudB_U"
        nudB_U.Size = New Size(60, 23)
        nudB_U.TabIndex = 4
        nudB_U.TextAlign = HorizontalAlignment.Right
        '
        ' nudB_B
        '
        nudB_B.DecimalPlaces = 2
        nudB_B.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudB_B.Location = New Point(340, 80)
        nudB_B.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudB_B.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudB_B.Name = "nudB_B"
        nudB_B.Size = New Size(60, 23)
        nudB_B.TabIndex = 5
        nudB_B.TextAlign = HorizontalAlignment.Right
        '
        ' nudB_L
        '
        nudB_L.DecimalPlaces = 2
        nudB_L.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudB_L.Location = New Point(200, 80)
        nudB_L.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudB_L.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudB_L.Name = "nudB_L"
        nudB_L.Size = New Size(60, 23)
        nudB_L.TabIndex = 6
        nudB_L.TextAlign = HorizontalAlignment.Right
        '
        ' nudB_D
        '
        nudB_D.DecimalPlaces = 2
        nudB_D.Increment = New Decimal(New Integer() {5, 0, 0, 131072})
        nudB_D.Location = New Point(60, 80)
        nudB_D.Maximum = New Decimal(New Integer() {200, 0, 0, 131072})
        nudB_D.Minimum = New Decimal(New Integer() {200, 0, 0, -2147418112})
        nudB_D.Name = "nudB_D"
        nudB_D.Size = New Size(60, 23)
        nudB_D.TabIndex = 7
        nudB_D.TextAlign = HorizontalAlignment.Right
        '
        ' Label13
        '
        Label13.AutoSize = True
        Label13.Location = New Point(20, 82)
        Label13.Name = "Label13"
        Label13.Size = New Size(38, 15)
        Label13.TabIndex = 8
        Label13.Text = "Down"
        '
        ' Label14
        '
        Label14.AutoSize = True
        Label14.Location = New Point(160, 82)
        Label14.Name = "Label14"
        Label14.Size = New Size(27, 15)
        Label14.TabIndex = 9
        Label14.Text = "Left"
        '
        ' Label15
        '
        Label15.AutoSize = True
        Label15.Location = New Point(300, 82)
        Label15.Name = "Label15"
        Label15.Size = New Size(32, 15)
        Label15.TabIndex = 10
        Label15.Text = "Back"
        '
        ' Label16
        '
        Label16.AutoSize = True
        Label16.Location = New Point(20, 112)
        Label16.Name = "Label16"
        Label16.Size = New Size(22, 15)
        Label16.TabIndex = 11
        Label16.Text = "Up"
        '
        ' Label17
        '
        Label17.AutoSize = True
        Label17.Location = New Point(160, 112)
        Label17.Name = "Label17"
        Label17.Size = New Size(35, 15)
        Label17.TabIndex = 12
        Label17.Text = "Right"
        '
        ' Label18
        '
        Label18.AutoSize = True
        Label18.Location = New Point(300, 112)
        Label18.Name = "Label18"
        Label18.Size = New Size(50, 15)
        Label18.TabIndex = 13
        Label18.Text = "Forward"
        '
        ' btnReset
        '
        btnReset.Location = New Point(436, 425)
        btnReset.Name = "btnReset"
        btnReset.Size = New Size(418, 49)
        btnReset.TabIndex = 4
        btnReset.Text = "Reset to default"
        btnReset.UseVisualStyleBackColor = True
        '
        ' GroupBox1
        '
        GroupBox1.Controls.Add(tambient)
        GroupBox1.Location = New Point(436, 168)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New Size(418, 68)
        GroupBox1.TabIndex = 5
        GroupBox1.TabStop = False
        GroupBox1.Text = "Ambient"
        '
        ' tambient
        '
        tambient.Location = New Point(11, 30)
        tambient.Minimum = 0R
        tambient.Maximum = 1.5R
        tambient.DisplayFormat = "0.00%"
        tambient.InputScale = 0.01R
        tambient.SmallChange = 0.05R
        tambient.LargeChange = 0.1R
        tambient.TickFrequency = 0.1R
        tambient.ShowTicks = True
        tambient.Name = "tambient"
        tambient.Size = New Size(389, 28)
        tambient.TabIndex = 0
        '
        ' grpBackground
        '
        grpBackground.Controls.Add(lblBackground)
        grpBackground.Controls.Add(cmbBackground)
        grpBackground.Location = New Point(436, 250)
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
        lblBackground.Text = "Background"
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
        '
        ToolTip1.SetToolTip(tbKey, "Adjust frontal light strength.")
        ToolTip1.SetToolTip(nudK_F, "Move frontal light forward or backward.")
        ToolTip1.SetToolTip(nudK_R, "Move frontal light right or left.")
        ToolTip1.SetToolTip(nudK_U, "Move frontal light up or down.")
        ToolTip1.SetToolTip(nudK_B, "Move frontal light toward the back direction.")
        ToolTip1.SetToolTip(nudK_L, "Move frontal light toward the left direction.")
        ToolTip1.SetToolTip(nudK_D, "Move frontal light downward.")

        ToolTip1.SetToolTip(tbFillL, "Adjust left light strength.")
        ToolTip1.SetToolTip(nudL_F, "Move left light forward or backward.")
        ToolTip1.SetToolTip(nudL_R, "Move left light right or left.")
        ToolTip1.SetToolTip(nudL_U, "Move left light up or down.")
        ToolTip1.SetToolTip(nudL_B, "Move left light toward the back direction.")
        ToolTip1.SetToolTip(nudL_L, "Move left light toward the left direction.")
        ToolTip1.SetToolTip(nudL_D, "Move left light downward.")

        ToolTip1.SetToolTip(tbFillR, "Adjust right light strength.")
        ToolTip1.SetToolTip(nudR_F, "Move right light forward or backward.")
        ToolTip1.SetToolTip(nudR_R, "Move right light right or left.")
        ToolTip1.SetToolTip(nudR_U, "Move right light up or down.")
        ToolTip1.SetToolTip(nudR_B, "Move right light toward the back direction.")
        ToolTip1.SetToolTip(nudR_L, "Move right light toward the left direction.")
        ToolTip1.SetToolTip(nudR_D, "Move right light downward.")

        ToolTip1.SetToolTip(tbBack, "Adjust back light strength.")
        ToolTip1.SetToolTip(nudB_F, "Move back light forward or backward.")
        ToolTip1.SetToolTip(nudB_R, "Move back light right or left.")
        ToolTip1.SetToolTip(nudB_U, "Move back light up or down.")
        ToolTip1.SetToolTip(nudB_B, "Move back light toward the back direction.")
        ToolTip1.SetToolTip(nudB_L, "Move back light toward the left direction.")
        ToolTip1.SetToolTip(nudB_D, "Move back light downward.")

        ToolTip1.SetToolTip(tambient, "Adjust ambient light intensity.")
        ToolTip1.SetToolTip(btnReset, "Restore the default light rig values.")
        ToolTip1.SetToolTip(cmbBackground, "Select the preview background color.")
        ' LightRigForm
        '
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        AutoScroll = True
        ClientSize = New Size(866, 487)
        Controls.Add(grpBackground)
        Controls.Add(GroupBox1)
        Controls.Add(btnReset)
        Controls.Add(grpBack)
        Controls.Add(grpFillR)
        Controls.Add(grpFillL)
        Controls.Add(grpKey)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "LightRigForm"
        StartPosition = FormStartPosition.CenterParent
        Text = "Light Rig"
        grpKey.ResumeLayout(False)
        grpKey.PerformLayout()
        CType(nudK_F, ComponentModel.ISupportInitialize).EndInit()
        CType(nudK_R, ComponentModel.ISupportInitialize).EndInit()
        CType(nudK_U, ComponentModel.ISupportInitialize).EndInit()
        CType(nudK_B, ComponentModel.ISupportInitialize).EndInit()
        CType(nudK_L, ComponentModel.ISupportInitialize).EndInit()
        CType(nudK_D, ComponentModel.ISupportInitialize).EndInit()
        grpFillL.ResumeLayout(False)
        grpFillL.PerformLayout()
        CType(nudL_F, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_R, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_U, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_B, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_L, ComponentModel.ISupportInitialize).EndInit()
        CType(nudL_D, ComponentModel.ISupportInitialize).EndInit()
        grpFillR.ResumeLayout(False)
        grpFillR.PerformLayout()
        CType(nudR_F, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_R, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_U, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_B, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_L, ComponentModel.ISupportInitialize).EndInit()
        CType(nudR_D, ComponentModel.ISupportInitialize).EndInit()
        grpBack.ResumeLayout(False)
        grpBack.PerformLayout()
        CType(nudB_F, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_R, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_U, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_B, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_L, ComponentModel.ISupportInitialize).EndInit()
        CType(nudB_D, ComponentModel.ISupportInitialize).EndInit()
        GroupBox1.ResumeLayout(False)
        GroupBox1.PerformLayout()
        grpBackground.ResumeLayout(False)
        grpBackground.PerformLayout()
        ResumeLayout(False)

    End Sub

    Friend WithEvents grpKey As GroupBox
    Friend WithEvents tbKey As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents nudK_F As NumericUpDown
    Friend WithEvents nudK_R As NumericUpDown
    Friend WithEvents nudK_U As NumericUpDown
    Friend WithEvents nudK_B As NumericUpDown
    Friend WithEvents nudK_L As NumericUpDown
    Friend WithEvents nudK_D As NumericUpDown
    Friend WithEvents lblK_U As Label
    Friend WithEvents lblK_R As Label
    Friend WithEvents lblK_F As Label
    Friend WithEvents lblK_D As Label
    Friend WithEvents lblK_L As Label
    Friend WithEvents lblK_B As Label
    Friend WithEvents grpFillL As GroupBox
    Friend WithEvents tbFillL As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents nudL_F As NumericUpDown
    Friend WithEvents nudL_R As NumericUpDown
    Friend WithEvents nudL_U As NumericUpDown
    Friend WithEvents nudL_B As NumericUpDown
    Friend WithEvents nudL_L As NumericUpDown
    Friend WithEvents nudL_D As NumericUpDown
    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents Label6 As Label
    Friend WithEvents grpFillR As GroupBox
    Friend WithEvents tbFillR As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents nudR_F As NumericUpDown
    Friend WithEvents nudR_R As NumericUpDown
    Friend WithEvents nudR_U As NumericUpDown
    Friend WithEvents nudR_B As NumericUpDown
    Friend WithEvents nudR_L As NumericUpDown
    Friend WithEvents nudR_D As NumericUpDown
    Friend WithEvents Label7 As Label
    Friend WithEvents Label8 As Label
    Friend WithEvents Label9 As Label
    Friend WithEvents Label10 As Label
    Friend WithEvents Label11 As Label
    Friend WithEvents Label12 As Label
    Friend WithEvents grpBack As GroupBox
    Friend WithEvents tbBack As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents nudB_F As NumericUpDown
    Friend WithEvents nudB_R As NumericUpDown
    Friend WithEvents nudB_U As NumericUpDown
    Friend WithEvents nudB_B As NumericUpDown
    Friend WithEvents nudB_L As NumericUpDown
    Friend WithEvents nudB_D As NumericUpDown
    Friend WithEvents Label13 As Label
    Friend WithEvents Label14 As Label
    Friend WithEvents Label15 As Label
    Friend WithEvents Label16 As Label
    Friend WithEvents Label17 As Label
    Friend WithEvents Label18 As Label
    Friend WithEvents btnReset As Button
    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents tambient As FO4_Base_Library.TinySliderTextBox
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents grpBackground As GroupBox
    Friend WithEvents lblBackground As Label
    Friend WithEvents cmbBackground As ColorComboBox
End Class
