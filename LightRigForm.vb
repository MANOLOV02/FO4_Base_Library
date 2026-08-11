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

    ''' <summary>Se dispara cuando cambia algo de la pestana Rendering (normales, welding, skinning,
    ''' camara, grilla). Los hosts que necesitan RECARGAR geometria (no solo repintar) se enganchan aca:
    ''' recalcular normales o cambiar el welding invalida la geometria ya subida a la GPU.</summary>
    Public Event RenderSettingsChanged()

    ''' <summary>EL UNICO AJUSTE APP-AWARE DEL DIALOGO. <c>Setting_DrawHiddenSegments</c> es una
    ''' preferencia de INSPECCION de Wardrobe Manager (dibujar tambien lo que la oclusion por segmento
    ''' esconde). FO4_NPC_Manager NO puede exponerla: su render DEPENDE de esa oclusion (el swap de
    ''' Pip-Boy 60/160 y el ocultado de head parts salen de ahi) y por eso la fuerza a False en el
    ''' arranque, en Program.vb. Si el dialogo compartido la dejara editar, el usuario podria romper el
    ''' render de NPC desde una casilla. Con False la casilla no se muestra y el setting no se toca.</summary>
    Private _allowHiddenSegments As Boolean = True

    Public Property AllowHiddenSegments As Boolean
        Get
            Return _allowHiddenSegments
        End Get
        Set(value As Boolean)
            _allowHiddenSegments = value
            ' ⛔⭐ LA VISIBILIDAD SE APLICA ACA, EN EL SETTER, y no donde se cargan los demas controles.
            ' El consumidor escribe `New LightRigForm With {.AllowHiddenSegments = False}`, y en VB el
            ' inicializador de objeto corre DESPUES del constructor: cuando CargarPestanaRender() leia la
            ' propiedad todavia valia el default True. Resultado en NPC Manager: la casilla SE VEIA, no
            ' hacia nada (el guard de escritura si miraba el valor ya asignado) y recien desaparecia a
            ' mitad de sesion, si el usuario tocaba Apply preset o Reset. Un control mudo y mentiroso
            ' justo en el unico ajuste app-aware del dialogo.
            If chkHiddenSegments IsNot Nothing Then chkHiddenSegments.Visible = value
        End Set
    End Property

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
        ' ⛔ El azimut se compara MODULO 360: 0 y 360 son la misma dirección y el NUD deja escribir los dos.
        ' Sin esto, aplicar un preset con azimut 0 y que el control redondee a 360 deseleccionaba el combo.
        ' `dAz` ES la diferencia angular en [0,180], y la resta cruda daria 360 para el mismo rayo.
        ' ⛔ LA TOLERANCIA ES LA MISMA EN LOS DOS EJES. Estuvo un tiempo en 0,5° para el azimut y 0,005°
        ' para la elevación, para absorber el redondeo del NUD: asimétrica no absorbía nada —la elevación
        ' sola ya mandaba el combo a "Custom"— y encima tapaba el problema real, que era que el modelo se
        ' cuantizaba. Eso lo arregla AnguloDesdeNud; acá alcanza con el epsilon de siempre.
        Dim dAz As Single = Math.Abs(((a.AzimuthDeg - b.AzimuthDeg) Mod 360.0F + 540.0F) Mod 360.0F - 180.0F)
        Return CasiIgual(a.Strength, b.Strength) AndAlso ColorCoincide(a.Color, b.Color) AndAlso
               dAz <= RigMatchEpsilon AndAlso CasiIgual(a.ElevationDeg, b.ElevationDeg)
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

        ' Dirección de MUNDO de cada luz: azimut + elevación (antes eran 6 multiplicadores relativos
        ' a la cámara; ver PreviewLight). Los NUD ya vienen acotados por el Designer.
        CargarAngulo(nudK_Az, rig.KeyLight.AzimuthDeg) : CargarAngulo(nudK_El, rig.KeyLight.ElevationDeg)
        CargarAngulo(nudL_Az, rig.FillLeft.AzimuthDeg) : CargarAngulo(nudL_El, rig.FillLeft.ElevationDeg)
        CargarAngulo(nudR_Az, rig.FillRight.AzimuthDeg) : CargarAngulo(nudR_El, rig.FillRight.ElevationDeg)
        CargarAngulo(nudB_Az, rig.BackLight.AzimuthDeg) : CargarAngulo(nudB_El, rig.BackLight.ElevationDeg)

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
        chkGroundShadow.Checked = sh.GroundShadow
        CargarCalidadSombra(sh.MapSize)
        tShadowSoft.Value = sh.SoftnessTexels
        tShadowStrength.Value = sh.Intensity
        ActualizarHabilitadoSombras()
        ActualizarAvisoDeSuelo()

        ' Background color picker (handler is wired later in AddHandlers, so this init does not fire it)
        cmbBackground.Rellena()
        cmbBackground.SelectedColor = Config_App.Current.Setting_BackColor

        CargarPestanaRender()

        SincronizarComboConRig(rig)
    End Sub

    ''' <summary>Modelo -> UI de la pestana Rendering. Todo sale de Config_App (la libreria), que es
    ''' justamente lo que permite que WM y NPC Manager compartan este dialogo.</summary>
    Private Sub CargarPestanaRender()
        Dim tbn = Config_App.Current.Setting_TBN
        chkRecalcNormals.Checked = Config_App.Current.Setting_RecalculateNormals
        chkRepairNaN.Checked = tbn.RepairNaNs
        chkNormalize.Checked = tbn.NormalizeOutputs
        chkDeterministic.Checked = tbn.DeterministicOnCollapse
        chkSmoothSeams.Checked = tbn.SmoothSeamNormals
        nudSeamAngle.Value = ClampDec(nudSeamAngle, CDec(tbn.SmoothSeamNormalsAngle))

        chkWelding.Checked = tbn.EnableWelding
        rbWeldPosOnly.Checked = tbn.WeldByPositionOnly
        rbWeldBoth.Checked = Not tbn.WeldByPositionOnly
        nudWeldPos.Value = ClampDec(nudWeldPos, CDec(tbn.WeldPosEpsilon))
        nudWeldUv.Value = ClampDec(nudWeldUv, CDec(tbn.WeldUVEpsilon))
        nudEpsPos.Value = ClampDec(nudEpsPos, CDec(tbn.EpsilonPos))
        ActualizarHabilitadoWelding()
        ActualizarHabilitadoPiso()
        ActualizarHabilitadoCamara()

        chkGpuSkinning.Checked = Config_App.Current.Setting_GPUSkinning
        chkSingleBone.Checked = Config_App.Current.Setting_SingleBoneSkinning
        ' Ver AllowHiddenSegments: en NPC Manager la casilla NO se muestra y el valor no se toca.
        chkHiddenSegments.Visible = AllowHiddenSegments
        chkHiddenSegments.Checked = Config_App.Current.Setting_DrawHiddenSegments

        chkResetAngles.Checked = Config_App.Current.Settings_Camara.ResetAngles
        chkResetZoom.Checked = Config_App.Current.Settings_Camara.ResetZoom
        chkFreezeCamera.Checked = Config_App.Current.Settings_Camara.FreezeCamera

        chkFloorEnabled.Checked = Config_App.Current.Settings_RenderGrid.Enabled
        nudFloorSize.Value = ClampDec(nudFloorSize, CDec(Config_App.Current.Settings_RenderGrid.Size))
        nudFloorStep.Value = ClampDec(nudFloorStep, CDec(Config_App.Current.Settings_RenderGrid.StepSize))
        cmbFloorColor.Rellena()
        cmbFloorColor.SelectedColor = Config_App.Current.RenderGridColor()
    End Sub

    ''' <summary>Un valor fuera del rango del control tira ArgumentOutOfRangeException y DEJA A MEDIAS
    ''' la carga: los controles que vienen despues se quedan con el valor del Designer y el guardado los
    ''' escribe encima de la config del usuario. Es el mismo modo de falla que ya documenta
    ''' Config_Form.Setea_Render_Options con su bandera _cargaCompleta. Acotar es mas barato que un
    ''' Try/Catch mudo, que es lo que escondia el problema alla.</summary>
    Private Shared Function ClampDec(nud As NumericUpDown, v As Decimal) As Decimal
        Return Math.Clamp(v, nud.Minimum, nud.Maximum)
    End Function

    ''' <summary>Repone TODA la pestana Rendering a sus defaults. Es el reemplazo del boton que Wardrobe
    ''' Manager tenia en su pantalla de Settings y que se perdio al migrar la pestana; btnReset, el de la
    ''' otra pestana, no toca nada de esto.
    ''' <para>⛔ `Setting_DrawHiddenSegments` se repone SOLO si esta app lo edita. El default de Wardrobe
    ''' Manager es True (es su ayuda de inspeccion) y el de la libreria False, asi que un reset ciego se lo
    ''' prenderia a FO4_NPC_Manager, que depende de la oclusion por segmento. Ver AllowHiddenSegments.</para>
    ''' </summary>
    Private Sub BtnResetRender_Click(sender As Object, e As EventArgs) Handles btnResetRender.Click
        If MessageBox.Show("Reset every setting on the Rendering tab to its default?",
                           "Rendering", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return

        Config_App.Current.Setting_RecalculateNormals = True
        Config_App.Current.Setting_TBN = RecalcTBN.DefaultTBNOptions
        Config_App.Current.Setting_GPUSkinning = True
        Config_App.Current.Setting_SingleBoneSkinning = False
        If AllowHiddenSegments Then Config_App.Current.Setting_DrawHiddenSegments = True
        Config_App.Current.Settings_Camara = Config_App.Default_CameraSettings
        Config_App.Current.Settings_RenderGrid = Config_App.Default_RenderGrid_Settings
        Config_App.Current.Setting_RenderGridColor = Color.FromKnownColor(KnownColor.LightGray).Name

        _preventchanges = True
        CargarPestanaRender()
        _preventchanges = False
        VolcarRenderEnModelo()
    End Sub

    ''' <summary>Los controles de la grilla no hacen nada con la grilla apagada. Wardrobe Manager tenia
    ''' esto (<c>Update_RenderGrid_Controls</c>) y la migracion se lo comio: quedaban tres controles vivos
    ''' que no cambiaban un pixel. Mismo patron de "control mudo" del chkHiddenSegments.</summary>
    Private Sub ActualizarHabilitadoPiso()
        Dim on_ = chkFloorEnabled.Checked
        lblFloorSize.Enabled = on_ : nudFloorSize.Enabled = on_
        lblFloorStep.Enabled = on_ : nudFloorStep.Enabled = on_
        lblFloorColor.Enabled = on_ : cmbFloorColor.Enabled = on_
    End Sub

    ''' <summary>Con la camara congelada los dos "reset on load" no pueden hacer nada: el freeze los anula.
    ''' Tambien venia de Wardrobe Manager (<c>CheckBoxFreeze_CheckedChanged</c>).</summary>
    Private Sub ActualizarHabilitadoCamara()
        Dim libre = Not chkFreezeCamera.Checked
        chkResetAngles.Enabled = libre
        chkResetZoom.Enabled = libre
    End Sub

    ''' <summary>El receptor de suelo NO PUEDE dibujarse con la key por debajo de ~11,5 grados: ahi la
    ''' sombra se estira al infinito y ShadowMapMath.ExpandForGroundShadow la rechaza. El preset Dungeon
    ''' tiene la key a -22,29, o sea que la casilla estaba tildada y no pasaba nada, sin una palabra.
    ''' Se dice en el propio texto de la casilla, que es donde el usuario esta mirando.</summary>
    Private Sub ActualizarAvisoDeSuelo()
        Const ElevacionMinima As Single = 11.54F     ' asin(0.2) en grados: el corte de ExpandForGroundShadow
        Dim elev = Config_App.Current.ActiveLights().KeyLight.ElevationDeg
        Dim puede = elev >= ElevacionMinima
        chkGroundShadow.Text = If(puede, "Shadow on the ground",
                                  $"Shadow on the ground (needs the key above {ElevacionMinima:0.#} deg)")
        chkGroundShadow.Enabled = puede AndAlso chkShadows.Checked
    End Sub

    Private Sub ActualizarHabilitadoWelding()
        Dim on_ = chkWelding.Checked
        rbWeldPosOnly.Enabled = on_
        rbWeldBoth.Enabled = on_
        lblWeldPos.Enabled = on_
        nudWeldPos.Enabled = on_
        lblWeldUv.Enabled = on_
        nudWeldUv.Enabled = on_
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
            nudK_Az, nudK_El, nudL_Az, nudL_El,
            nudR_Az, nudR_El, nudB_Az, nudB_El}
            AddHandler nud.ValueChanged, nudChanged
        Next

        AddHandler chkShadows.CheckedChanged, Sub(sender, e)
                                                  ActualizarHabilitadoSombras()
                                                  VolcarSombrasEnModelo()
                                              End Sub
        AddHandler chkGroundShadow.CheckedChanged, Sub(sender, e) VolcarSombrasEnModelo()
        AddHandler cmbShadowQuality.SelectedIndexChanged, Sub(sender, e) VolcarSombrasEnModelo()
        AddHandler tShadowSoft.ValueChanged, Sub(sender, e) VolcarSombrasEnModelo()
        AddHandler tShadowStrength.ValueChanged, Sub(sender, e) VolcarSombrasEnModelo()

        ' Pestana Rendering: todo escribe en Config_App al vuelo, igual que las luces.
        For Each c In New CheckBox() {chkRecalcNormals, chkRepairNaN, chkNormalize, chkDeterministic,
                                      chkSmoothSeams, chkGpuSkinning, chkSingleBone, chkHiddenSegments,
                                      chkResetAngles, chkResetZoom}
            AddHandler c.CheckedChanged, Sub(sender, e) VolcarRenderEnModelo()
        Next
        AddHandler chkFloorEnabled.CheckedChanged, Sub(sender, e)
                                                       ActualizarHabilitadoPiso()
                                                       VolcarRenderEnModelo()
                                                   End Sub
        AddHandler chkFreezeCamera.CheckedChanged, Sub(sender, e)
                                                       ActualizarHabilitadoCamara()
                                                       VolcarRenderEnModelo()
                                                   End Sub
        AddHandler chkWelding.CheckedChanged, Sub(sender, e)
                                                  ActualizarHabilitadoWelding()
                                                  VolcarRenderEnModelo()
                                              End Sub
        ' Un click en un par de radios levanta CheckedChanged en LOS DOS (el que se prende y el que se
        ' apaga). Sin este filtro cada cambio de modo de welding costaba dos recargas de geometria.
        For Each r In New RadioButton() {rbWeldPosOnly, rbWeldBoth}
            AddHandler r.CheckedChanged, Sub(sender, e)
                                             If DirectCast(sender, RadioButton).Checked Then VolcarRenderEnModelo()
                                         End Sub
        Next
        For Each nud In New NumericUpDown() {nudSeamAngle, nudWeldPos, nudWeldUv, nudEpsPos,
                                             nudFloorSize, nudFloorStep}
            AddHandler nud.ValueChanged, Sub(sender, e) VolcarRenderEnModelo()
        Next
        AddHandler cmbFloorColor.SelectedIndexChanged, Sub(sender, e) VolcarRenderEnModelo()

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

        ' El rig vivo entra como referencia para que los angulos sobrevivan al redondeo del NUD.
        Dim ant = Config_App.Current.ActiveLights()
        Dim rig As New PreviewLightRig With {
            .KeyLight = LeerLuz(tbKey, btnKeyColor, nudK_Az, nudK_El, ant.KeyLight),
            .FillLeft = LeerLuz(tbFillL, btnFillLColor, nudL_Az, nudL_El, ant.FillLeft),
            .FillRight = LeerLuz(tbFillR, btnFillRColor, nudR_Az, nudR_El, ant.FillRight),
            .BackLight = LeerLuz(tbBack, btnBackColor, nudB_Az, nudB_El, ant.BackLight),
            .AmbientIntensity = CSng(tambient.Value),
            .AmbientGroundLevel = CSng(tGroundLevel.Value),
            .AmbientSkyColor = RigColor.FromColor(btnAmbSky.BackColor),
            .AmbientGroundColor = RigColor.FromColor(btnAmbGround.BackColor),
            .SchemaVersion = PreviewLightRig.CurrentSchemaVersion}

        Config_App.Current.SetActiveLights(rig)
        ' Mover la elevacion de la key puede habilitar o deshabilitar el receptor de suelo.
        ActualizarAvisoDeSuelo()
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
        ' El receptor de suelo tiene ADEMAS su propia condicion (elevacion de la key). Una sola fuente.
        ActualizarAvisoDeSuelo()
    End Sub

    Private Sub VolcarSombrasEnModelo()
        If _preventchanges Then Return
        Dim sh = Config_App.Current.ActiveShadows().Sanitized()
        sh.Enabled = chkShadows.Checked
        sh.GroundShadow = chkGroundShadow.Checked
        If cmbShadowQuality.SelectedIndex >= 0 Then sh.MapSize = ShadowQualities(cmbShadowQuality.SelectedIndex).Size
        sh.SoftnessTexels = CSng(tShadowSoft.Value)
        sh.Intensity = CSng(tShadowStrength.Value)
        Config_App.Current.SetActiveShadows(sh)
        RaiseEvent LightsChanged()
    End Sub

    ''' <summary>UI -> modelo de la pestana Rendering. Escribe en Config_App (la libreria) y avisa con
    ''' <see cref="RenderSettingsChanged"/>, que es un evento DISTINTO de LightsChanged: cambiar el
    ''' welding o el recalculo de normales invalida la geometria subida a la GPU, no alcanza con
    ''' repintar.</summary>
    Private Sub VolcarRenderEnModelo()
        If _preventchanges Then Return

        Dim tbn = Config_App.Current.Setting_TBN
        tbn.RepairNaNs = chkRepairNaN.Checked
        tbn.NormalizeOutputs = chkNormalize.Checked
        tbn.DeterministicOnCollapse = chkDeterministic.Checked
        tbn.SmoothSeamNormals = chkSmoothSeams.Checked
        tbn.SmoothSeamNormalsAngle = CDbl(nudSeamAngle.Value)
        tbn.EnableWelding = chkWelding.Checked
        tbn.WeldByPositionOnly = rbWeldPosOnly.Checked
        tbn.WeldPosEpsilon = CDbl(nudWeldPos.Value)
        tbn.WeldUVEpsilon = CDbl(nudWeldUv.Value)
        tbn.EpsilonPos = CDbl(nudEpsPos.Value)
        Config_App.Current.Setting_TBN = tbn

        Config_App.Current.Setting_RecalculateNormals = chkRecalcNormals.Checked
        Config_App.Current.Setting_GPUSkinning = chkGpuSkinning.Checked
        Config_App.Current.Setting_SingleBoneSkinning = chkSingleBone.Checked
        ' SOLO si la app lo permite. En NPC Manager la casilla ni se muestra, y no tocar el valor es lo
        ' que evita que el dialogo compartido le rompa la oclusion por segmento.
        If AllowHiddenSegments Then Config_App.Current.Setting_DrawHiddenSegments = chkHiddenSegments.Checked

        Config_App.Current.Settings_Camara = New Config_App.CameraSettings With {
            .ResetAngles = chkResetAngles.Checked,
            .ResetZoom = chkResetZoom.Checked,
            .FreezeCamera = chkFreezeCamera.Checked}

        Config_App.Current.Settings_RenderGrid = New Config_App.RenderGridSettings With {
            .Enabled = chkFloorEnabled.Checked,
            .Size = CSng(nudFloorSize.Value),
            .StepSize = CSng(nudFloorStep.Value)}
        Config_App.Current.Setting_RenderGridColor = cmbFloorColor.SelectedColor.Name

        RaiseEvent RenderSettingsChanged()
    End Sub

    ''' <summary>Carga un angulo en su NUD y DEJA ANOTADO lo que quedo mostrando, en el Tag. Ver
    ''' <see cref="AnguloDesdeNud"/>: esa anotacion es la que distingue "el control redondeo" de "el
    ''' usuario escribio".</summary>
    Private Shared Sub CargarAngulo(nud As NumericUpDown, grados As Single)
        nud.Value = Math.Clamp(CDec(grados), nud.Minimum, nud.Maximum)
        nud.Tag = nud.Value
    End Sub

    ''' <summary>Devuelve el angulo que muestra el control, PERO conserva el del modelo si el control
    ''' no puede distinguirlos.
    ''' <para>⛔⭐ Los NUD muestran 1 decimal y los presets llevan 5, porque a 2 decimales el error de
    ''' direccion ya voltea ~340 px de 648.000 (medido; ver PreviewLightRig). <c>ValidateEditText</c>
    ''' re-parsea el texto MOSTRADO al perder el foco, asi que con solo TABULAR por el campo el rig se
    ''' reescribia cuantizado a 0,1 grados y el preset dejaba de coincidir consigo mismo: el combo se
    ''' iba a "Custom" y el render cambiaba, sin que el usuario tocara nada.</para>
    ''' <para>La alternativa —mostrar 5 decimales— hace una UI ilegible para ganar una precision que
    ''' nadie va a tipear. Si lo que el control muestra sigue siendo el redondeo del valor vivo,
    ''' entonces el usuario NO lo cambio y el modelo manda.</para></summary>
    Private Shared Function AnguloDesdeNud(nud As NumericUpDown, actual As Single) As Single
        ' ⛔ LA COMPARACION ES CONTRA LO QUE SE CARGO, NO CONTRA UNA TOLERANCIA. La primera version
        ' conservaba el valor del modelo cuando distaba menos de medio paso de pantalla (0,05 grados) del
        ' mostrado — y eso descarta en silencio una edicion TIPEADA adentro de esa banda: el usuario
        ' escribe 41,45 sobre un modelo de 41,42367, el NUD queda mostrando 41,5 y el modelo no se entera.
        ' Con el valor cargado anotado en el Tag la pregunta se contesta exacta: si el control muestra
        ' todavia lo que se le puso, nadie lo toco.
        If TypeOf nud.Tag Is Decimal AndAlso DirectCast(nud.Tag, Decimal) = nud.Value Then Return actual
        ' ⛔⭐ LA ANOTACION SE ANULA APENAS EL CONTROL SE MOVIO UNA VEZ. Sin esto la condicion de arriba deja
        ' de significar "nadie lo toco" y pasa a significar "justo ahora muestra lo que se cargo", que no es
        ' lo mismo: con Increment = 5, subir una flecha y bajarla vuelve EXACTAMENTE al valor cargado (es
        ' aritmetica Decimal, no hay error), el Tag matchea de nuevo y el modelo se queda con el angulo
        ' nudgeado mientras el NUD muestra el original. Tres estados en desacuerdo —control, modelo y combo
        ' de presets— y ninguna forma de volver.
        nud.Tag = Nothing
        Return CSng(nud.Value)
    End Function

    Private Shared Function LeerLuz(strength As TinySliderTextBox, swatch As Button,
                                    azimuth As NumericUpDown, elevation As NumericUpDown,
                                    actual As PreviewLight) As PreviewLight
        Return New PreviewLight(CSng(strength.Value),
                                azimuthDeg:=AnguloDesdeNud(azimuth, actual.AzimuthDeg),
                                elevationDeg:=AnguloDesdeNud(elevation, actual.ElevationDeg)) With {
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
