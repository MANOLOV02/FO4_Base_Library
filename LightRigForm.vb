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

    ''' <summary>Demora para los cuatro NumericUpDown de TBN, cuyo cambio cuesta una recarga COMPLETA de
    ''' geometría del proyecto en pantalla. Ver dónde se enganchan, en <c>AddHandlers</c>.
    ''' <para>⛔ <c>System.Windows.Forms.Timer</c> a propósito: su Tick corre en el hilo de UI, que es el único
    ''' desde el que se puede tocar <c>Config_App</c> y disparar el re-render. Con un
    ''' <c>Threading.Timer</c> habría que hacer Invoke y se abre una carrera con el cierre del diálogo.</para>
    ''' <para>400 ms: más corto y una pulsación normal ya dispara; más largo y se siente colgado.</para></summary>
    Private ReadOnly _demoraTbn As New System.Windows.Forms.Timer With {.Interval = 400}

    ''' <summary>El diálogo ya entró en FormClosing. Corta los ValueChanged tardíos —los que dispara un
    ''' NumericUpDown al perder el foco DURANTE el cierre— para que no vuelvan a arrancar la demora sobre
    ''' un formulario que se está destruyendo.</summary>
    Private _cerrando As Boolean = False

    Public Sub New()

        InitializeComponent()

        ' ⛔ Y si el diálogo se cierra con un cambio todavía en la demora, hay que APLICARLO: si no, el
        ' usuario escribe un epsilon, cierra, y su valor se pierde sin que nada lo diga.
        AddHandler _demoraTbn.Tick, Sub(s2, e2)
                                        _demoraTbn.Stop()
                                        ' ⛔ Si el diálogo ya se cerró, este Tick leería DIEZ CONTROLES YA
                                        ' DISPUESTOS y escribiría Config_App desde un formulario muerto.
                                        If _cerrando OrElse IsDisposed Then Return
                                        VolcarRenderEnModelo()
                                    End Sub
        AddHandler Me.FormClosing, Sub(s2, e2)
                                       ' ⛔⛔ HACEN FALTA LOS DOS MECANISMOS, uno por familia de control.
                                       ' ⛔ `ValidateChildren()` SI SIRVE — para los 16 TinySliderTextBox
                                       ' del diálogo (los 8 azimut/elevación, las 4 intensidades, ambiente,
                                       ' ground level y los 2 de sombras): ésos commitean en
                                       ' `_textBox.Validating`, que es exactamente lo que ValidateChildren
                                       ' dispara. Lo saqué generalizando una medición que valía sólo para
                                       ' NumericUpDown, y con eso rompí el commit de TODA la pestaña Lights:
                                       ' tipear un azimut y cerrar con la X perdía el valor.
                                       Me.ValidateChildren()
                                       ' ⛔ Y ADEMAS leer los `.Value`, porque para los NumericUpDown
                                       ' ValidateChildren NO alcanza: `NumericUpDown` no sobrescribe
                                       ' `OnValidating`, que es lo único que ValidateChildren dispara.
                                       ' MEDIDO con un NUD real:
                                       '     tras tipear      : currentValue=60 UserEdit=True ValueChanged=0
                                       '     ValidateChildren : currentValue=60 UserEdit=True ValueChanged=0
                                       '     leer .Value      : 13   currentValue=13  ValueChanged=1
                                       ' El commit vive en OnLostFocus y en el GETTER de `.Value`. Leerlos
                                       ' es lo que convierte el texto, dispara ValueChanged y deja el valor
                                       ' donde `VolcarRenderEnModelo` lo va a encontrar.
                                       ' ⚠️ Y sin esto el `_cerrando` de abajo EMPEORABA el caso: antes el
                                       ' Tick corría 400 ms tarde y al menos escribía; con el flag, el
                                       ' ValueChanged tardío se descarta y el valor se perdía SIEMPRE.
                                       ' ⛔ LOS SEIS, no los cuatro de TBN. `nudFloorSize` y `nudFloorStep`
                                       ' quedaban afuera, y su único camino de commit es `ValueChanged` —
                                       ' que al cerrar con una edición pendiente NO se dispara nunca, ni
                                       ' antes de FormClosed ni después de Dispose. O sea que el tamaño y
                                       ' el paso del piso no se perdían "a veces": se perdían SIEMPRE.
                                       Dim forzarCommit = nudSeamAngle.Value + nudWeldPos.Value +
                                                          nudWeldUv.Value + nudEpsPos.Value +
                                                          nudFloorSize.Value + nudFloorStep.Value
                                       If forzarCommit < Decimal.MinValue Then Return   ' nunca; el compilador no elide la lectura
                                       If _demoraTbn.Enabled Then
                                           _demoraTbn.Stop()
                                           VolcarRenderEnModelo()
                                       End If
                                       ' ⛔ SI EL CIERRE SE CANCELA, NO SE DESARMA NADA. Poner `_cerrando`
                                       ' y disponer el Timer sin mirar `e2.Cancel` deja el diálogo vivo con
                                       ' los cuatro NumericUpDown de TBN MUDOS para el resto de la sesión:
                                       ' el handler de la demora arranca con `If _preventchanges OrElse
                                       ' _cerrando Then Return`, así que nada de lo que la persona escriba
                                       ' se aplica nunca, en silencio. Hoy no hay ningún cancelador (ni el
                                       ' form ni los dos hosts tocan e.Cancel), pero el commit de arriba ya
                                       ' corrió y es idempotente: cancelar tiene que dejar el diálogo
                                       ' exactamente como estaba.
                                       If e2.Cancel Then Return
                                       ' A partir de acá ningún ValueChanged tardío puede volver a
                                       ' arrancarlo, y el Timer se libera: NO está en `components`, así que
                                       ' el Dispose generado no lo alcanza.
                                       _cerrando = True
                                       _demoraTbn.Stop()
                                       _demoraTbn.Dispose()
                                   End Sub
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
        ' ⛔ EL FLAG DE CASTEO ENTRA EN LA COMPARACION. Sin esto, prender la sombra de un fill dejaba el
        ' combo diciendo "Studio" cuando el rig ya NO es Studio — y Apply habilitado, o sea un click de
        ' distancia de perder el cambio sin aviso.
        Return CasiIgual(a.Strength, b.Strength) AndAlso ColorCoincide(a.Color, b.Color) AndAlso
               dAz <= RigMatchEpsilon AndAlso CasiIgual(a.ElevationDeg, b.ElevationDeg) AndAlso
               a.CastsShadow = b.CastsShadow
    End Function

    Private Shared Function RigCoincide(a As PreviewLightRig, b As PreviewLightRig) As Boolean
        Return LuzCoincide(a.KeyLight, b.KeyLight) AndAlso LuzCoincide(a.FillLeft, b.FillLeft) AndAlso
               LuzCoincide(a.FillRight, b.FillRight) AndAlso LuzCoincide(a.BackLight, b.BackLight) AndAlso
               CasiIgual(a.AmbientIntensity, b.AmbientIntensity) AndAlso
               CasiIgual(a.AmbientGroundLevel, b.AmbientGroundLevel) AndAlso
               ColorCoincide(a.AmbientSkyColor, b.AmbientSkyColor) AndAlso
               ColorCoincide(a.AmbientGroundColor, b.AmbientGroundColor) AndAlso
               CasiIgual(a.ShadowSoftnessTexels, b.ShadowSoftnessTexels) AndAlso
               CasiIgual(a.ShadowDarkness, b.ShadowDarkness) AndAlso
               a.ShadowOnGround = b.ShadowOnGround
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
        CargarAngulo(tK_Az, rig.KeyLight.AzimuthDeg) : CargarAngulo(tK_El, rig.KeyLight.ElevationDeg)
        CargarAngulo(tL_Az, rig.FillLeft.AzimuthDeg) : CargarAngulo(tL_El, rig.FillLeft.ElevationDeg)
        CargarAngulo(tR_Az, rig.FillRight.AzimuthDeg) : CargarAngulo(tR_El, rig.FillRight.ElevationDeg)
        CargarAngulo(tB_Az, rig.BackLight.AzimuthDeg) : CargarAngulo(tB_El, rig.BackLight.ElevationDeg)

        ' Quien castea es parte del RIG (viaja con el preset), no de Setting_PreviewShadows_*: por eso se
        ' carga acá con las otras propiedades de luz y se vuelca por VolcarUIenModelo, que es lo que hace
        ' que tocarlo marque el combo de presets como "Custom" — que en este caso SI es lo que significa.
        chkCastKey.Checked = rig.KeyLight.CastsShadow
        chkCastFillL.Checked = rig.FillLeft.CastsShadow
        chkCastFillR.Checked = rig.FillRight.CastsShadow
        chkCastBack.Checked = rig.BackLight.CastsShadow

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
        chkDepth16.Checked = sh.Depth16
        ' ⭐ ESTOS TRES SALEN DEL RIG, no de ActiveShadows(): blandura, oscuridad y receptor de suelo son
        ' parte del LOOK del set de luces y viajan con el preset. Ver el bloque de PreviewLightRig.
        chkGroundShadow.Checked = rig.ShadowOnGround
        ' Anclaje del rig. Vive en Config_App como el resto de las preferencias del visor y NO en
        ' PreviewLightRig: no es parte del set calibrado —no cambia una sola intensidad ni un angulo— sino
        ' de COMO se interpreta. Metiendolo en el rig habria que versionar el schema y ademas tocarlo
        ' marcaria el combo de presets como "Custom", que no es lo que significa.
        chkLightsFollowCamera.Checked = Config_App.Current.Setting_LightsFollowCamera
        CargarCalidadSombra(sh.MapSize)
        tShadowSoft.Value = rig.ShadowSoftnessTexels
        tShadowStrength.Value = rig.ShadowDarkness
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
    ''' <para>⛔ `Setting_DrawHiddenSegments` se repone SOLO si esta app lo edita. El default declarado en
    ''' <c>Config_Class.vb</c> es True —es la ayuda de inspeccion de Wardrobe Manager— asi que un reset
    ''' ciego se lo PRENDERIA a FO4_NPC_Manager, que depende de la oclusion por segmento y no expone la
    ''' casilla. Ver AllowHiddenSegments.
    ''' <para>⚠️ Este doc decia "y el de la libreria False". Es al reves: el default de la libreria ES True
    ''' (Config_Class.vb:52). Quien lo leyera para decidir si el guard de NPC Manager seguia haciendo falta
    ''' concluia justo lo contrario.</para></para>
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
        ' ⛔ EL CORTE SE LEE DEL RENDER, no se transcribe. Estaba como `Const ElevacionMinima = 11.54F`
        ' comentado "asin(0.2)", y asin(0,2) es 11,5370: redondeado PARA ARRIBA dejaba una banda muerta en
        ' [11,5370 , 11,54) donde el motor si dibuja la sombra y la casilla estaba gris. Y el texto, con
        ' formato "0.#", mostraba "11,5" — un valor con el que la casilla se deshabilita, o sea que el
        ' mensaje contradecia a su propio gate. Se muestra con dos decimales por lo mismo.
        Dim ElevacionMinima As Single = ShadowMapMath.ElevacionMinimaGrados

        ' ⛔⛔ CON LAS LUCES SIGUIENDO A LA CAMARA, ESTE AVISO NO PUEDE PREDECIR NADA — Y NO DEBE GRISAR.
        ' El render no decide con la elevacion del RIG: decide con `_frameLights.KeyDir.Z`, que cuando
        ' `Setting_LightsFollowCamera` esta prendido (el default) es la direccion de la key YA ROTADA POR
        ' LA CAMARA. Son dos cantidades distintas y solo coinciden con la opcion apagada.
        ' Los dos sintomas que provocaba comparar la equivocada:
        '   · preset con la key a 30 grados: casilla habilitada y tildada, el usuario baja la camara, el
        '     KeyDir efectivo cae bajo el corte y el receptor deja de dibujarse sin una palabra.
        '   · preset Dungeon (key a -22 grados): casilla PERMANENTEMENTE gris, aunque orbitando hacia
        '     arriba la key efectiva quede muy por encima del corte y el motor la dibujaria perfecto.
        ' Este dialogo no conoce la camara y no tiene por que conocerla: lo honesto es NO decidir por el
        ' usuario cuando el resultado depende de algo que va a cambiar en cuanto mueva la vista.
        Dim sigueALaCamara = Config_App.Current.Setting_LightsFollowCamera
        ' ⛔ LA CONDICION ES SOBRE EL CONJUNTO DE CASTERS, no sobre la key. Desde que cualquier luz puede
        ' castear, el receptor se dibuja si AL MENOS UNA de las que castean esta por encima del corte: con
        ' la key rasante y un fill alto hay sombra de piso perfectamente valida, y el cartel decia que no.
        Dim rigActual = Config_App.Current.ActiveLights()
        Dim puede As Boolean = sigueALaCamara
        If Not puede Then
            For i = 0 To PreviewShadowSettings.MaxShadowLights - 1
                Dim l = ShadowMapMath.LuzDelRig(rigActual, i)
                If l.CasteaDeVerdad() AndAlso l.ElevationDeg >= ElevacionMinima Then
                    puede = True
                    Exit For
                End If
            Next
        End If
        ' ⛔ NINGUNA DE LAS DOS CASILLAS ANUNCIA "(follows the camera)" EN SU TEXTO — decision del usuario.
        ' La del suelo lo decia cuando `Setting_LightsFollowCamera` estaba prendido, para explicar por que
        ' seguia habilitada aunque la elevacion AUTORADA del rig fuera baja: con el rig pegado a la camara la
        ' direccion efectiva la decide el arrastre, asi que el corte no se puede evaluar de antemano y la
        ' casilla se deja habilitada siempre.
        ' Esa razon SIGUE VIVA y por eso no se borro: se movio al TOOLTIP. El rotulo se leia mal —parecia una
        ' propiedad del receptor de suelo cuando en realidad describe al rig entero— y encima crecia justo en
        ' el control mas ancho del grupo.
        If sigueALaCamara Then
            chkGroundShadow.Text = "Shadow on the ground"
            ToolTip1.SetToolTip(chkGroundShadow,
                "The rig follows the camera, so each light's effective elevation depends on where you drag: " &
                "the ground catcher stays available and simply does not draw for a light that ends up too low.")
        Else
            ' ⛔ CORTO: el gate `ui-layout` del ShadowGate mide el ancho real del control contra el interior
            ' del grupo, y un texto de una linea mas largo que el original lo saca del recuadro. Ya me paso
            ' con "(follows the camera: shows when the light is high enough)" — 458 px contra 418.
            chkGroundShadow.Text = If(puede, "Shadow on the ground",
                                      $"Shadow on the ground (needs a casting light above {ElevacionMinima:0.00} deg)")
            ToolTip1.SetToolTip(chkGroundShadow,
                $"Draws the character's silhouette on the floor plane. Needs at least one casting light above " &
                $"{ElevacionMinima:0.00} deg: below that the projected shadow has no finite framing.")
        End If
        chkGroundShadow.Enabled = puede AndAlso chkShadows.Checked
        ' ⛔ Y EL CARTEL DE VRAM SE REFRESCA ACA, que es el unico lugar donde se decide `chkGroundShadow.Enabled`
        ' — el factor que DUPLICA la cuenta. Sin esto, mover el slider de elevacion de la ultima luz que
        ' calificaba dejaba el cartel mostrando el doble, sin que nada lo volviera a llamar.
        ActualizarCartelDeVram()
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

        For Each nud In New TinySliderTextBox() {
            tK_Az, tK_El, tL_Az, tL_El,
            tR_Az, tR_El, tB_Az, tB_El}
            AddHandler nud.ValueChanged, nudChanged
        Next

        AddHandler chkShadows.CheckedChanged, Sub(sender, e)
                                                  ActualizarHabilitadoSombras()
                                                  VolcarSombrasEnModelo()
                                              End Sub
        ' ⛔ VAN A VolcarUIenModelo (el rig) Y NO A VolcarSombrasEnModelo: el flag vive en el rig. Cruzarlos
        ' escribiria el rig en Setting_PreviewShadows_* y viceversa, y el sintoma seria que la casilla se
        ' olvida al cerrar el dialogo.
        ' ⛔ EL ORDEN IMPORTA Y ESTABA AL REVES. El cartel de VRAM lee el RIG (con el mismo predicado que el
        ' render) y el habilitado del receptor de suelo, y las dos cosas las actualiza VolcarUIenModelo: si
        ' el cartel corre ANTES, muestra el estado anterior al click. Se veia al apagar la ultima luz que
        ' calificaba para el receptor: el cartel seguia contando dos juegos de mapas y nada lo volvia a
        ' llamar. VolcarUIenModelo primero, cartel despues.
        For Each c In New CheckBox() {chkCastKey, chkCastFillL, chkCastFillR, chkCastBack}
            AddHandler c.CheckedChanged, Sub(sender, e)
                                             VolcarUIenModelo()
                                             ActualizarCartelDeVram()
                                         End Sub
        Next
        ' ⛔ VAN A VolcarUIenModelo (el RIG), no a VolcarSombrasEnModelo: blandura, oscuridad y receptor de
        ' suelo se mudaron al rig. Cruzarlos escribiria en la estructura equivocada y el sintoma seria que
        ' la perilla se olvida al cerrar el dialogo.
        ' ⭐ Y por eso mismo tocarlas AHORA marca el combo como "Custom" y Apply/Reset las restauran: son
        ' parte del preset. Antes Reset te devolvia las luces y te dejaba la sombra como estaba.
        AddHandler chkGroundShadow.CheckedChanged, Sub(sender, e)
                                                       VolcarUIenModelo()
                                                       ActualizarCartelDeVram()
                                                   End Sub
        ' La precision parte la cuenta de VRAM al medio, asi que el cartel se refresca con ella.
        AddHandler chkDepth16.CheckedChanged, Sub(sender, e)
                                                  VolcarSombrasEnModelo()
                                                  ActualizarCartelDeVram()
                                              End Sub
        AddHandler chkLightsFollowCamera.CheckedChanged, Sub(sender, e)
                                                             If _preventchanges Then Return
                                                             Config_App.Current.Setting_LightsFollowCamera = chkLightsFollowCamera.Checked
                                                             ' ⛔ Esta casilla cambia CON QUE cantidad decide
                                                             ' el render si dibuja el receptor de suelo, asi
                                                             ' que el aviso de al lado queda obsoleto en el
                                                             ' acto. Sin esto, apagarla dejaba el texto
                                                             ' "follows the camera" y la casilla habilitada
                                                             ' con la key por debajo del corte.
                                                             ActualizarAvisoDeSuelo()
                                                             ' Redibuja: cambia la direccion de las 4 luces
                                                             ' y, con ella, el encuadre del shadow map.
                                                             RaiseEvent LightsChanged()
                                                         End Sub
        AddHandler cmbShadowQuality.SelectedIndexChanged, Sub(sender, e)
                                                              ' Elegir a mano en el combo ES renunciar al
                                                              ' tamano custom: recien ahi se descarta.
                                                              ' Con `_preventchanges` esto viene de la carga,
                                                              ' que no es una eleccion del usuario.
                                                              If Not _preventchanges Then _mapSizeFueraDeLista = 0
                                                              VolcarSombrasEnModelo()
                                                              ActualizarCartelDeVram()
                                                          End Sub
        AddHandler tShadowSoft.ValueChanged, Sub(sender, e) VolcarUIenModelo()
        AddHandler tShadowStrength.ValueChanged, Sub(sender, e) VolcarUIenModelo()

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
        ' ⛔⛔ LOS CUATRO DE TBN VAN CON DEMORA; LOS DOS DEL PISO, AL TOQUE. No es lo mismo:
        ' `AjustesDeGeometria` INCLUYE `.Tbn`, asi que cualquier cambio en seam angle o en los epsilons
        ' marca `RenderDirtyFlags.Force` = Clean + esqueleto + LoadShapesParallel + TBN + welding + morphs
        ' + subida a GPU. Y los tres epsilons tienen `DecimalPlaces = 12`: escribir "0,000000000005" a mano
        ' son ~14 `ValueChanged`, o sea ~14 RECARGAS COMPLETAS del proyecto en pantalla encoladas mientras
        ' el usuario todavia esta tipeando. Cada flechita del seam angle, una mas.
        ' Este mismo archivo ya documenta el caso hermano: sacaron camara y grilla de `AjustesDeGeometria`
        ' porque "tipear '500' en el tamano del piso costaba TRES recargas completas de un NPC con outfit".
        ' El problema quedo abierto justo en los campos donde la recarga es mas cara.
        ' ⛔ El anterior mecanismo de WM era un boton "Apply to rendered project" explicito; al mudar la
        ' pestana a este dialogo compartido se perdio y quedaron todos en vivo.
        For Each nud In New NumericUpDown() {nudSeamAngle, nudWeldPos, nudWeldUv, nudEpsPos}
            AddHandler nud.ValueChanged, Sub(sender, e)
                                             If _preventchanges OrElse _cerrando Then Return
                                             ' Reiniciar la cuenta en cada tecla: se aplica cuando el
                                             ' usuario PARA, no mientras escribe.
                                             _demoraTbn.Stop()
                                             _demoraTbn.Start()
                                         End Sub
        Next
        For Each nud In New NumericUpDown() {nudFloorSize, nudFloorStep}
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
            .KeyLight = LeerLuz(tbKey, btnKeyColor, tK_Az, tK_El, chkCastKey, ant.KeyLight),
            .FillLeft = LeerLuz(tbFillL, btnFillLColor, tL_Az, tL_El, chkCastFillL, ant.FillLeft),
            .FillRight = LeerLuz(tbFillR, btnFillRColor, tR_Az, tR_El, chkCastFillR, ant.FillRight),
            .BackLight = LeerLuz(tbBack, btnBackColor, tB_Az, tB_El, chkCastBack, ant.BackLight),
            .AmbientIntensity = CSng(tambient.Value),
            .AmbientGroundLevel = CSng(tGroundLevel.Value),
            .AmbientSkyColor = RigColor.FromColor(btnAmbSky.BackColor),
            .AmbientGroundColor = RigColor.FromColor(btnAmbGround.BackColor),
            .ShadowSoftnessTexels = CSng(tShadowSoft.Value),
            .ShadowDarkness = CSng(tShadowStrength.Value),
            .ShadowOnGround = chkGroundShadow.Checked}

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
        ' ⛔⛔ UN TAMANO FUERA DE LA LISTA NO SE PISA EN SILENCIO — y hasta ahora SI SE PISABA, contra lo que
        ' decia este mismo comentario. `VolcarSombrasEnModelo` es el handler de TODAS las perillas de sombra
        ' (la casilla, el suelo, suavidad, intensidad) y escribia `sh.MapSize` desde el combo cada vez. O sea
        ' que un usuario con MapSize = 8192 —legitimo: Sanitized() lo permite y hay un gate que lo prueba—
        ' lo perdia apenas movia el slider de Darkness, sin tocar la calidad.
        ' Se recuerda el valor original y se sigue escribiendo ESE hasta que el usuario cambie el combo a
        ' proposito. El combo, mientras tanto, muestra el mas cercano para no mentir.
        _mapSizeFueraDeLista = 0
        If idx < 0 Then
            _mapSizeFueraDeLista = mapSize
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
        chkCastKey.Enabled = on_
        chkCastFillL.Enabled = on_
        chkCastFillR.Enabled = on_
        chkCastBack.Enabled = on_
        ActualizarCartelDeVram()
        lblShadowQuality.Enabled = on_
        cmbShadowQuality.Enabled = on_
        chkDepth16.Enabled = on_
        lblShadowSoft.Enabled = on_
        tShadowSoft.Enabled = on_
        lblShadowStrength.Enabled = on_
        tShadowStrength.Enabled = on_
        ' El receptor de suelo tiene ADEMAS su propia condicion (elevacion de la key). Una sola fuente.
        ActualizarAvisoDeSuelo()
    End Sub

    ''' <summary>Dice cuantos mapas y cuanta VRAM cuesta la configuracion actual. NO es un tope: no hay
    ''' tope, las cuatro luces pueden castear. Es la alternativa honesta a un cap mudo — quien decide los
    ''' bytes es el usuario, y para decidir necesita el numero.
    ''' <para>La cuenta es lado^2 x 4 bytes (DepthComponent24 se almacena en 32 bits en todo driver de
    ''' escritorio) x capas, y por DOS si el receptor de suelo esta activo: desde que su array se reserva
    ''' fijo —del mismo lado y con las mismas capas que el del personaje, que es lo que evita recrearlo al
    ''' orbitar— prenderlo DUPLICA exactamente la reserva. Este doc decia "se menciona sin numero en vez de
    ''' inventarlo", que era cierto cuando el mapa ancho se dimensionaba solo y ya no lo es: ahora el numero
    ''' se conoce y decir la mitad de un costo es peor que no decirlo.</para></summary>
    Private Sub ActualizarCartelDeVram()
        ' ⛔ CUENTA CON EL MISMO PREDICADO QUE EL RENDER, no las casillas tildadas. `SlotsDeSombra` reparte
        ' capas con `CasteaDeVerdad()`, que descarta la luz que no APORTA luz (Strength 0 o color negro):
        ' una luz tildada y apagada no reserva un byte. Contando `Checked` el cartel cobraba VRAM que la GPU
        ' no reserva — un error "del lado seguro", pero el doc de aca abajo afirma ser LA cuenta, y una
        ' cuenta que sobra no es la cuenta.
        Dim rigActual = Config_App.Current.ActiveLights()
        Dim n As Integer = 0
        For i = 0 To PreviewShadowSettings.MaxShadowLights - 1
            If ShadowMapMath.LuzDelRig(rigActual, i).CasteaDeVerdad() Then n += 1
        Next
        ' ⛔ EL NUMERO VA EN EL TITULO DEL GROUPBOX, no en una etiqueta adentro. Estuvo como `lblShadowVram`
        ' a la derecha de la fila de calidad y se PISABA con la casilla de 16 bits: la etiqueta es AutoSize
        ' y crece hacia la derecha ("128 MB" son ~50 px desde x=317) justo encima del control que arranca en
        ' x=350. El titulo no compite con nada, se lee sin buscarlo, y de paso el costo queda al lado del
        ' nombre de la feature que lo causa.
        If Not chkShadows.Checked OrElse n = 0 Then
            grpShadows.Text = "Shadows"
            Exit Sub
        End If
        Dim sh = Config_App.Current.ActiveShadows().Sanitized()
        Dim lado As Integer = sh.MapSize
        ' Los DOS arrays van a DepthComponent16 = 2 B por texel (el receptor de suelo reserva el suyo del
        ' mismo lado y con las mismas capas: reserva fija, es lo que evita recrearlo al orbitar).
        ' ⛔ La cuenta tiene que seguir a la del render. Fueron 4 B (DepthComponent24, que el driver guarda
        ' en 32) hasta que se midio que en un ortho de rango corto los 16 bits no mueven un pixel: el escalon
        ' de profundidad queda 30x mas fino que el TEXEL, que es lo unico que se ve en el borde. Si algun dia
        ' alguno vuelve a 24 y esto no se actualiza, el cartel miente — y un cartel que miente sobre bytes es
        ' peor que no tenerlo.
        Dim conSuelo As Boolean = chkGroundShadow.Checked AndAlso chkGroundShadow.Enabled
        ' 24 bits los guarda el driver en 32 = 4 B por texel; 16 bits son 2. Por array.
        Dim bytesPorArray As Double = If(chkDepth16.Checked, 2.0, 4.0)
        Dim bytesPorTexel As Double = bytesPorArray * If(conSuelo, 2.0, 1.0)
        Dim mb As Double = CDbl(lado) * lado * bytesPorTexel * n / (1024.0 * 1024.0)
        grpShadows.Text = $"Shadows ({mb:0} MB)"
        ToolTip1.SetToolTip(grpShadows,
            $"{n} shadow map(s) of {lado}x{lado} at {If(chkDepth16.Checked, 16, 24)}-bit depth" &
            If(conSuelo, $", plus {n} more for the ground catcher.", "."))
    End Sub

    ''' <summary>MapSize del config cuando NO esta entre las opciones del combo (0 = esta en la lista).
    ''' Ver el comentario de la carga: sin esto, mover cualquier otra perilla de sombra lo pisaba.</summary>
    Private _mapSizeFueraDeLista As Integer = 0

    Private Sub VolcarSombrasEnModelo()
        If _preventchanges Then Return
        Dim sh = Config_App.Current.ActiveShadows().Sanitized()
        sh.Enabled = chkShadows.Checked
        sh.Depth16 = chkDepth16.Checked
        If _mapSizeFueraDeLista > 0 Then
            ' El usuario tiene un tamano propio y todavia no eligio uno de la lista: se conserva.
            sh.MapSize = _mapSizeFueraDeLista
        ElseIf cmbShadowQuality.SelectedIndex >= 0 Then
            sh.MapSize = ShadowQualities(cmbShadowQuality.SelectedIndex).Size
        End If
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
    ''' <para>⚠️ Migrado de <c>NumericUpDown</c> a <see cref="TinySliderTextBox"/>: el control nuevo expone
    ''' <c>Value</c> como Double, no como Decimal, asi que la anotacion del Tag pasa a ser Double. La
    ''' comparacion sigue siendo por IGUALDAD EXACTA y eso sigue siendo correcto — se compara contra el
    ''' valor que este mismo metodo escribio, no contra una cuenta.</para></summary>
    Private Shared Sub CargarAngulo(nud As TinySliderTextBox, grados As Single)
        nud.Value = Math.Clamp(CDbl(grados), nud.Minimum, nud.Maximum)
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
    Private Shared Function AnguloDesdeNud(nud As TinySliderTextBox, actual As Single) As Single
        ' ⛔ LA COMPARACION ES CONTRA LO QUE SE CARGO, NO CONTRA UNA TOLERANCIA. La primera version
        ' conservaba el valor del modelo cuando distaba menos de medio paso de pantalla (0,05 grados) del
        ' mostrado — y eso descarta en silencio una edicion TIPEADA adentro de esa banda: el usuario
        ' escribe 41,45 sobre un modelo de 41,42367, el NUD queda mostrando 41,5 y el modelo no se entera.
        ' Con el valor cargado anotado en el Tag la pregunta se contesta exacta: si el control muestra
        ' todavia lo que se le puso, nadie lo toco.
        If TypeOf nud.Tag Is Double AndAlso DirectCast(nud.Tag, Double) = nud.Value Then Return actual
        ' ⛔⭐ LA ANOTACION SE ANULA APENAS EL CONTROL SE MOVIO UNA VEZ. Sin esto la condicion de arriba deja
        ' de significar "nadie lo toco" y pasa a significar "justo ahora muestra lo que se cargo", que no es
        ' lo mismo: con Increment = 5, subir una flecha y bajarla vuelve EXACTAMENTE al valor cargado (es
        ' aritmetica Decimal, no hay error), el Tag matchea de nuevo y el modelo se queda con el angulo
        ' nudgeado mientras el NUD muestra el original. Tres estados en desacuerdo —control, modelo y combo
        ' de presets— y ninguna forma de volver.
        ' ⚠️ El argumento de arriba hablaba de `Increment = 5` del NumericUpDown; con TinySliderTextBox el
        ' equivalente es `SmallChange`/`LargeChange`, y el razonamiento no cambia: cualquier gesto que
        ' devuelva el control a su valor de partida haria matchear el Tag otra vez.
        nud.Tag = Nothing
        Return CSng(nud.Value)
    End Function

    Private Shared Function LeerLuz(strength As TinySliderTextBox, swatch As Button,
                                    azimuth As TinySliderTextBox, elevation As TinySliderTextBox,
                                    castea As CheckBox, actual As PreviewLight) As PreviewLight
        ' Todos los campos por el ctor: sin `With { }` que complete a medias. Ver su doc — asi se olvido
        ' un CastsShadow en el arnes y todos sus A/B de sombra pasaron a medir cero.
        Return New PreviewLight(CSng(strength.Value),
                                azimuthDeg:=AnguloDesdeNud(azimuth, actual.AzimuthDeg),
                                elevationDeg:=AnguloDesdeNud(elevation, actual.ElevationDeg),
                                color:=RigColor.FromColor(swatch.BackColor),
                                castsShadow:=castea.Checked)
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
        ' El anclaje del rig tambien: vive en este mismo grupo, al lado del boton, asi que dejarlo afuera
        ' seria la misma inconsistencia.
        ' ⛔ Vuelve al DEFAULT DECLARADO en Config_App, no a un literal. Escribir False aca haria que Reset
        ' contradiga al default el dia que alguien lo cambie — y ya se cambio una vez.
        Dim anclajeDefault = New Config_App().Setting_LightsFollowCamera
        Config_App.Current.Setting_LightsFollowCamera = anclajeDefault
        chkLightsFollowCamera.Checked = anclajeDefault
        AplicarRig(PreviewLightRig.Defaults())
    End Sub
End Class
