Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms

Public Enum TinySliderThumbStyle
    Circle = 0
    Triangle = 1
    Diamond = 2
    VerticalBar = 3
    Ring = 4
    Needle = 5
End Enum

Public Enum TinySliderThumbDirection
    Down = 0
    Up = 1
End Enum

Public Enum TinySliderFillMode
    Left = 0
    Center = 1
End Enum

<DefaultEvent("ValueChanged")>
Public Class TinySliderTextBox
    Inherits UserControl

    Private ReadOnly _textBox As New TextBox()
    Private Const DesignDpi As Single = 96.0F
    Private _allowExtremeValues As Boolean = False
    Private _minimum As Double = 0.0R
    Private _maximum As Double = 100.0R
    Private _value As Double = 0.0R
    Private _smallChange As Double = 1.0R
    Private _largeChange As Double = 10.0R
    Private _tickFrequency As Double = 10.0R
    Private _displayFormat As String = String.Empty
    Private _inputScale As Double = 1.0R

    Private _showTicks As Boolean = False
    Private _showTextBox As Boolean = True
    Private _showFocus As Boolean = False
    Private _textBoxGap As Integer = 8
    Private _textBoxWidth As Integer = 60
    Private _textBoxHeight As Integer = 22
    Private _trackHeight As Integer = 3
    Private _thumbRadius As Single = 4
    Private _thumbStyle As TinySliderThumbStyle = TinySliderThumbStyle.Circle
    Private _fillMode As TinySliderFillMode = TinySliderFillMode.Left

    Private _dragging As Boolean = False
    Private _accentColor As Color = Color.FromKnownColor(KnownColor.HotTrack)
    Private _trackColor As Color = Color.FromKnownColor(KnownColor.ControlDark)
    Private _thumbColor As Color = Color.FromKnownColor(KnownColor.HotTrack)

    Public Event ValueChanged As EventHandler
    Public Event DragEnded As EventHandler
    Private _thumbDirection As TinySliderThumbDirection = TinySliderThumbDirection.Down


    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or
                 ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.Selectable, True)

        TabStop = True
        Size = New Size(240, 28)
        MinimumSize = New Size(100, 24)
        BackColor = Color.FromKnownColor(KnownColor.Control)
        _textBox.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        _textBox.BorderStyle = BorderStyle.FixedSingle
        _textBox.TextAlign = HorizontalAlignment.Right
        _textBox.AutoSize = False
        _textBox.Font = Font
        _textBox.Text = FormatValue(_value)
        _textBox.Margin = Padding.Empty

        AddHandler _textBox.Validating, AddressOf TextBox_Validating
        AddHandler _textBox.KeyDown, AddressOf TextBox_KeyDown
        AddHandler _textBox.GotFocus, AddressOf ChildFocusChanged
        AddHandler _textBox.LostFocus, AddressOf ChildFocusChanged

        Controls.Add(_textBox)
        LayoutTextBox()
    End Sub


    <Browsable(True), Category("Behavior"), DefaultValue(0.0R)>
    Public Property Minimum As Double
        Get
            Return _minimum
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) Then Return

            _minimum = value
            If _maximum < _minimum Then _maximum = _minimum

            If Not _allowExtremeValues Then Me.Value = Clamp(_value)

            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Behavior"), DefaultValue(100.0R)>
    Public Property Maximum As Double
        Get
            Return _maximum
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) Then Return

            _maximum = value
            If _minimum > _maximum Then _minimum = _maximum

            If Not _allowExtremeValues Then Me.Value = Clamp(_value)

            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Behavior"), Bindable(True), DefaultValue(0.0R)>
    Public Property Value As Double
        Get
            Return _value
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) Then Return

            Dim newValue As Double = NormalizeValue(value)
            If AreClose(_value, newValue) Then
                UpdateTextBoxText()
                Return
            End If

            _value = newValue
            UpdateTextBoxText()
            Invalidate()
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End Set
    End Property

    <Browsable(True), Category("Behavior"), DefaultValue(1.0R)>
    Public Property SmallChange As Double
        Get
            Return _smallChange
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) Then Return
            _smallChange = Math.Abs(value)
        End Set
    End Property

    <Browsable(True), Category("Behavior"), DefaultValue(10.0R)>
    Public Property LargeChange As Double
        Get
            Return _largeChange
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) Then Return
            _largeChange = Math.Abs(value)
        End Set
    End Property

    <Browsable(True), Category("Behavior"), DefaultValue(False)>
    Public Property AllowExtremeValues As Boolean
        Get
            Return _allowExtremeValues
        End Get
        Set(value As Boolean)
            If _allowExtremeValues = value Then Return

            _allowExtremeValues = value
            If Not _allowExtremeValues Then Me.Value = Clamp(_value)
        End Set
    End Property

    <Browsable(True), Category("Behavior"), DefaultValue("")>
    Public Property DisplayFormat As String
        Get
            Return _displayFormat
        End Get
        Set(value As String)
            Dim normalized As String = If(value, String.Empty)
            If Not IsValidFormatString(normalized) Then
                normalized = String.Empty
            End If
            _displayFormat = normalized
            UpdateTextBoxText()
        End Set
    End Property

    <Browsable(True), Category("Behavior"), DefaultValue(1.0R)>
    Public Property InputScale As Double
        Get
            Return _inputScale
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) OrElse value = 0.0R Then Return
            _inputScale = value
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(False)>
    Public Property ShowFocusRectangle As Boolean
        Get
            Return _showFocus
        End Get
        Set(value As Boolean)
            _showFocus = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(10.0R)>
    Public Property TickFrequency As Double
        Get
            Return _tickFrequency
        End Get
        Set(value As Double)
            If Not IsUsableNumber(value) Then Return
            _tickFrequency = Math.Abs(value)
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(False)>
    Public Property ShowTicks As Boolean
        Get
            Return _showTicks
        End Get
        Set(value As Boolean)
            _showTicks = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(True)>
    Public Property ShowTextBox As Boolean
        Get
            Return _showTextBox
        End Get
        Set(value As Boolean)
            _showTextBox = value
            _textBox.Visible = value
            LayoutTextBox()
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(60)>
    Public Property TextBoxWidth As Integer
        Get
            Return _textBoxWidth
        End Get
        Set(value As Integer)
            _textBoxWidth = Math.Max(36, value)
            LayoutTextBox()
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(22)>
    Public Property TextBoxHeight As Integer
        Get
            Return _textBoxHeight
        End Get
        Set(value As Integer)
            _textBoxHeight = Math.Max(18, value)
            LayoutTextBox()
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(8)>
    Public Property TextBoxGap As Integer
        Get
            Return _textBoxGap
        End Get
        Set(value As Integer)
            _textBoxGap = Math.Max(0, value)
            LayoutTextBox()
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(3)>
    Public Property TrackHeight As Integer
        Get
            Return _trackHeight
        End Get
        Set(value As Integer)
            _trackHeight = Math.Max(2, value)
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(4)>
    Public Property ThumbRadius As Single
        Get
            Return _thumbRadius
        End Get
        Set(value As Single)
            _thumbRadius = Math.Max(3, value)
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(TinySliderThumbStyle.Circle)>
    Public Property ThumbStyle As TinySliderThumbStyle
        Get
            Return _thumbStyle
        End Get
        Set(value As TinySliderThumbStyle)
            If Not [Enum].IsDefined(GetType(TinySliderThumbStyle), value) Then Return
            _thumbStyle = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(TinySliderFillMode.Left)>
    Public Property FillMode As TinySliderFillMode
        Get
            Return _fillMode
        End Get
        Set(value As TinySliderFillMode)
            If Not [Enum].IsDefined(GetType(TinySliderFillMode), value) Then Return
            _fillMode = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance"), DefaultValue(TinySliderThumbDirection.Down)>
    Public Property ThumbDirection As TinySliderThumbDirection
        Get
            Return _thumbDirection
        End Get
        Set(value As TinySliderThumbDirection)
            If Not [Enum].IsDefined(GetType(TinySliderThumbDirection), value) Then Return
            _thumbDirection = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance")>
    Public Property AccentColor As Color
        Get
            Return _accentColor
        End Get
        Set(value As Color)
            _accentColor = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance")>
    Public Property TrackColor As Color
        Get
            Return _trackColor
        End Get
        Set(value As Color)
            _trackColor = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance")>
    Public Property ThumbColor As Color
        Get
            Return _thumbColor
        End Get
        Set(value As Color)
            _thumbColor = value
            Invalidate()
        End Set
    End Property

    <Browsable(True), Category("Appearance")>
    Public Property TextBoxTextAlign As HorizontalAlignment
        Get
            Return _textBox.TextAlign
        End Get
        Set(value As HorizontalAlignment)
            _textBox.TextAlign = value
        End Set
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property TextBoxControl As TextBox
        Get
            Return _textBox
        End Get
    End Property

    <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
    Public ReadOnly Property FormattedValue As String
        Get
            Return FormatValue(_value)
        End Get
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.Clear(BackColor)

        Dim track As Rectangle = GetTrackRectangle()
        If track.Width <= 0 Then Return

        Dim centerY As Single = track.Top + track.Height / 2.0F
        Dim x As Integer = ValueToX(_value)

        Using trackPath As GraphicsPath = RoundedRect(New RectangleF(track.X, track.Y, track.Width, track.Height), track.Height / 2.0F),
              trackBrush As New SolidBrush(If(Enabled, _trackColor, Color.FromArgb(235, 235, 235)))
            g.FillPath(trackBrush, trackPath)
        End Using

        DrawFill(g, track, x)

        DrawTicks(g, track, centerY)
        DrawThumb(g, x, centerY)

        If _showFocus Then
            If Focused OrElse _textBox.Focused Then

                Using focusPen As New Pen(Color.FromArgb(120, _accentColor), ScalePenWidth(1.0F))
                    focusPen.DashStyle = DashStyle.Dot
                    Dim focusInset As Integer = ScaleToCurrentDpi(2)
                    Dim focusRect As Rectangle = Rectangle.Inflate(ClientRectangle, -focusInset, -focusInset)
                    g.DrawRectangle(focusPen, focusRect)
                End Using
            End If
        End If
    End Sub
    Private Function ScaleToCurrentDpi(value As Integer) As Integer
        Return Math.Max(1, CInt(Math.Round(value * GetCurrentDpiScale())))
    End Function
    Private Function ScaleToCurrentDpi(value As Single) As Integer
        Return Math.Max(1, CInt(Math.Round(value * GetCurrentDpiScale())))
    End Function
    Private Function ScalePenWidth(value As Single) As Single
        Return Math.Max(1.0F, value * GetCurrentDpiScale())
    End Function

    Private Function GetCurrentDpiScale() As Single
        Dim dpi As Integer = DeviceDpi
        If dpi <= 0 Then dpi = CInt(DesignDpi)
        Return dpi / DesignDpi
    End Function

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        LayoutTextBox()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        _textBox.Font = Font
        LayoutTextBox()
    End Sub
    Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
        MyBase.OnDpiChangedAfterParent(e)
        LayoutTextBox()
        Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        _textBox.Enabled = Enabled
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left OrElse Not Enabled Then Return

        Focus()
        _dragging = True
        Capture = True
        Value = XToValue(e.X)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _dragging AndAlso Enabled Then
            Value = XToValue(e.X)
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        Dim wasDragging As Boolean = _dragging
        _dragging = False
        Capture = False
        If wasDragging Then RaiseEvent DragEnded(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        MyBase.OnMouseWheel(e)
        If Not Enabled Then Return
        Value += If(e.Delta > 0, _smallChange, -_smallChange)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If Not Enabled Then Return

        Select Case e.KeyCode
            Case Keys.Left, Keys.Down
                Value -= _smallChange
                e.Handled = True
            Case Keys.Right, Keys.Up
                Value += _smallChange
                e.Handled = True
            Case Keys.PageDown
                Value -= _largeChange
                e.Handled = True
            Case Keys.PageUp
                Value += _largeChange
                e.Handled = True
            Case Keys.Home
                Value = _minimum
                e.Handled = True
            Case Keys.End
                Value = _maximum
                e.Handled = True
        End Select
    End Sub

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnLostFocus(e As EventArgs)
        MyBase.OnLostFocus(e)
        Invalidate()
    End Sub

    Private Sub ChildFocusChanged(sender As Object, e As EventArgs)
        Invalidate()
    End Sub

    Private Sub TextBox_Validating(sender As Object, e As CancelEventArgs)
        ApplyTextBoxValue()
    End Sub

    Private Sub TextBox_KeyDown(sender As Object, e As KeyEventArgs)
        Select Case e.KeyCode
            Case Keys.Enter
                ApplyTextBoxValue()
                _textBox.SelectAll()
                e.SuppressKeyPress = True
            Case Keys.Escape
                UpdateTextBoxText()
                _textBox.SelectAll()
                e.SuppressKeyPress = True
            Case Keys.Up
                Value += _smallChange
                _textBox.SelectAll()
                e.SuppressKeyPress = True
            Case Keys.Down
                Value -= _smallChange
                _textBox.SelectAll()
                e.SuppressKeyPress = True
        End Select
    End Sub

    Private Sub ApplyTextBoxValue()
        Dim parsed As Double
        If TryParseFlexibleDouble(_textBox.Text, parsed) Then
            Value = parsed * _inputScale
        End If

        UpdateTextBoxText()
    End Sub

    Private Sub UpdateTextBoxText()
        Dim text As String = FormatValue(_value)
        If _textBox.Text <> text Then
            _textBox.Text = text
        End If
    End Sub

    Private Function FormatValue(value As Double) As String
        If Not String.IsNullOrEmpty(_displayFormat) Then
            Try
                Return value.ToString(_displayFormat, CultureInfo.CurrentCulture)
            Catch
                ' Fall through to default below if the stored format somehow throws.
            End Try
        End If
        Return value.ToString("F2", CultureInfo.CurrentCulture)
    End Function

    Private Shared Function IsValidFormatString(format As String) As Boolean
        If String.IsNullOrEmpty(format) Then Return True
        Try
            Dim _unused As String = (0.0R).ToString(format, CultureInfo.CurrentCulture)
            _unused = (1.5R).ToString(format, CultureInfo.CurrentCulture)
            _unused = (-1.5R).ToString(format, CultureInfo.CurrentCulture)
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub LayoutTextBox()
        If _textBox Is Nothing Then Return

        _textBox.Visible = _showTextBox
        If Not _showTextBox Then Return

        Dim inset As Integer = 1

        _textBox.Width = Math.Min(_textBoxWidth, Math.Max(0, ClientSize.Width - (inset * 2)))
        _textBox.Height = Math.Min(_textBoxHeight, Math.Max(18, ClientSize.Height - 4))

        _textBox.Left = Math.Max(0, ClientSize.Width - _textBox.Width - inset)
        _textBox.Top = Math.Max(0, (ClientSize.Height - _textBox.Height) \ 2)
    End Sub
    Protected Overrides Sub OnParentChanged(e As EventArgs)
        MyBase.OnParentChanged(e)
        LayoutTextBox()
        Invalidate()
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        LayoutTextBox()
    End Sub
    Private Function GetTrackRectangle() As Rectangle
        Dim scaledTrackHeight As Integer = ScaleToCurrentDpi(_trackHeight)
        Dim thumbHalf As Integer = GetThumbHalfWidth()
        Dim sidePadding As Integer = ScaleToCurrentDpi(Padding.Left)
        Dim scaledGap As Integer = ScaleToCurrentDpi(_textBoxGap)

        Dim left As Integer = thumbHalf + sidePadding

        Dim actualTextBoxWidth As Integer =
        If(_showTextBox AndAlso _textBox.Visible, _textBox.Width, 0)

        Dim rightMargin As Integer =
        If(_showTextBox,
           actualTextBoxWidth + scaledGap + thumbHalf + sidePadding,
           thumbHalf + sidePadding)

        Dim width As Integer = Math.Max(ScaleToCurrentDpi(8), ClientSize.Width - left - rightMargin)
        Dim y As Integer = Math.Max(0, (ClientSize.Height - scaledTrackHeight) \ 2)

        Return New Rectangle(left, y, width, scaledTrackHeight)
    End Function

    Private Function GetThumbHalfWidth() As Integer
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)

        Select Case _thumbStyle
            Case TinySliderThumbStyle.Triangle
                Return r + ScaleToCurrentDpi(1)

            Case TinySliderThumbStyle.Diamond
                Return r + ScaleToCurrentDpi(1)

            Case TinySliderThumbStyle.VerticalBar
                Return Math.Max(ScaleToCurrentDpi(3), r \ 2)

            Case TinySliderThumbStyle.Ring
                Return r + ScaleToCurrentDpi(2)

            Case TinySliderThumbStyle.Needle
                Return Math.Max(ScaleToCurrentDpi(3), CInt(Math.Round(r * 0.5R))) + ScaleToCurrentDpi(2)
            Case Else
                Return r
        End Select
    End Function

    Private Function ValueToX(v As Double) As Integer
        Dim track As Rectangle = GetTrackRectangle()
        If AreClose(_maximum, _minimum) Then Return track.Left

        Dim ratio As Double = (Clamp(v) - _minimum) / (_maximum - _minimum)
        Return CInt(Math.Round(track.Left + ratio * track.Width))
    End Function

    Private Function XToValue(x As Integer) As Double
        Dim track As Rectangle = GetTrackRectangle()
        If AreClose(_maximum, _minimum) Then Return _minimum

        Dim clampedX As Integer = Math.Max(track.Left, Math.Min(track.Right, x))
        Dim ratio As Double = (clampedX - track.Left) / CDbl(track.Width)
        Return NormalizeValue(_minimum + ratio * (_maximum - _minimum))
    End Function

    Private Function GetCenterValue() As Double
        Return _minimum + ((_maximum - _minimum) / 2.0R)
    End Function

    Private Sub DrawFill(g As Graphics, track As Rectangle, thumbX As Integer)
        Dim fillRect As RectangleF

        Select Case _fillMode
            Case TinySliderFillMode.Center
                Dim centerX As Integer = ValueToX(GetCenterValue())
                Dim leftX As Integer = Math.Min(centerX, thumbX)
                Dim rightX As Integer = Math.Max(centerX, thumbX)
                If rightX <= leftX Then Return
                fillRect = New RectangleF(leftX, track.Top, rightX - leftX, track.Height)

            Case Else
                If thumbX <= track.Left Then Return
                fillRect = New RectangleF(track.Left, track.Top, thumbX - track.Left, track.Height)
        End Select

        Using progressPath As GraphicsPath = RoundedRect(fillRect, track.Height / 2.0F),
              progressBrush As New SolidBrush(If(Enabled, _accentColor, Color.FromArgb(180, 180, 180)))
            g.FillPath(progressBrush, progressPath)
        End Using
    End Sub

    Private Function NormalizeValue(v As Double) As Double
        If _allowExtremeValues Then Return v
        Return Clamp(v)
    End Function

    Private Function Clamp(v As Double) As Double
        Return Math.Max(_minimum, Math.Min(_maximum, v))
    End Function

    Private Shared Function AreClose(a As Double, b As Double) As Boolean
        Return Math.Abs(a - b) < 0.0000000001R
    End Function

    Private Shared Function IsUsableNumber(value As Double) As Boolean
        Return Not Double.IsNaN(value) AndAlso Not Double.IsInfinity(value)
    End Function

    Private Shared Function TryParseFlexibleDouble(text As String, ByRef result As Double) As Boolean
        result = 0.0R
        If String.IsNullOrEmpty(text) Then Return False

        Dim trimmed As String = text.Trim()
        If Double.TryParse(trimmed,
                           NumberStyles.Float Or NumberStyles.AllowThousands,
                           CultureInfo.CurrentCulture,
                           result) Then Return True
        If Double.TryParse(trimmed,
                           NumberStyles.Float Or NumberStyles.AllowThousands,
                           CultureInfo.InvariantCulture,
                           result) Then Return True

        Dim numericOnly As String = ExtractFirstNumber(trimmed)
        If numericOnly.Length = 0 Then Return False

        If Double.TryParse(numericOnly,
                           NumberStyles.Float Or NumberStyles.AllowThousands,
                           CultureInfo.CurrentCulture,
                           result) Then Return True
        Return Double.TryParse(numericOnly,
                               NumberStyles.Float Or NumberStyles.AllowThousands,
                               CultureInfo.InvariantCulture,
                               result)
    End Function

    Private Shared Function ExtractFirstNumber(text As String) As String
        Dim sb As New System.Text.StringBuilder()
        Dim startedDigits As Boolean = False
        Dim decimalSeen As Boolean = False
        Dim signSeen As Boolean = False
        Dim expSeen As Boolean = False
        Dim culture = CultureInfo.CurrentCulture
        Dim decimalSep As String = culture.NumberFormat.NumberDecimalSeparator
        Dim groupSep As String = culture.NumberFormat.NumberGroupSeparator

        For i As Integer = 0 To text.Length - 1
            Dim c As Char = text(i)

            If Char.IsDigit(c) Then
                sb.Append(c)
                startedDigits = True
                Continue For
            End If

            If Not startedDigits Then
                If (c = "-"c OrElse c = "+"c) AndAlso Not signSeen Then
                    sb.Append(c)
                    signSeen = True
                    Continue For
                End If
                If MatchesAt(text, i, decimalSep) Then
                    sb.Append(decimalSep)
                    decimalSeen = True
                    startedDigits = True
                    i += decimalSep.Length - 1
                    Continue For
                End If
                If c = "."c OrElse c = ","c Then
                    sb.Append(decimalSep)
                    decimalSeen = True
                    startedDigits = True
                    Continue For
                End If
                Continue For
            End If

            If MatchesAt(text, i, decimalSep) AndAlso Not decimalSeen AndAlso Not expSeen Then
                sb.Append(decimalSep)
                decimalSeen = True
                i += decimalSep.Length - 1
                Continue For
            End If
            If MatchesAt(text, i, groupSep) AndAlso Not decimalSeen AndAlso Not expSeen Then
                i += groupSep.Length - 1
                Continue For
            End If
            If (c = "."c OrElse c = ","c) AndAlso Not decimalSeen AndAlso Not expSeen Then
                sb.Append(decimalSep)
                decimalSeen = True
                Continue For
            End If
            If (c = "e"c OrElse c = "E"c) AndAlso Not expSeen Then
                sb.Append(c)
                expSeen = True
                If i + 1 < text.Length AndAlso (text(i + 1) = "+"c OrElse text(i + 1) = "-"c) Then
                    sb.Append(text(i + 1))
                    i += 1
                End If
                Continue For
            End If

            Exit For
        Next

        Return sb.ToString()
    End Function

    Private Shared Function MatchesAt(text As String, index As Integer, token As String) As Boolean
        If String.IsNullOrEmpty(token) Then Return False
        If index + token.Length > text.Length Then Return False
        Return String.CompareOrdinal(text, index, token, 0, token.Length) = 0
    End Function

    Private Sub DrawTicks(g As Graphics, track As Rectangle, centerY As Single)
        If Not _showTicks OrElse AreClose(_maximum, _minimum) OrElse _tickFrequency <= 0.0R Then Return
        Dim radius As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim tickTopOffset As Integer = ScaleToCurrentDpi(3)
        Dim tickBottomOffset As Integer = ScaleToCurrentDpi(6)

        Using tickPen As New Pen(Color.FromArgb(150, 150, 150), ScalePenWidth(1.0F))
            Dim tickValue As Double = _minimum
            Dim guard As Integer = 0

            While tickValue <= _maximum + (_tickFrequency / 2.0R) AndAlso guard < 10000
                Dim tx As Integer = ValueToX(tickValue)
                g.DrawLine(tickPen, tx, CInt(centerY + radius + tickTopOffset), tx, CInt(centerY + radius + tickBottomOffset))
                tickValue += _tickFrequency
                guard += 1
            End While
        End Using
    End Sub

    Private Sub DrawThumb(g As Graphics, x As Integer, centerY As Single)
        Select Case _thumbStyle
            Case TinySliderThumbStyle.Triangle
                DrawTriangleThumb(g, x, centerY)

            Case TinySliderThumbStyle.Diamond
                DrawDiamondThumb(g, x, centerY)

            Case TinySliderThumbStyle.VerticalBar
                DrawVerticalBarThumb(g, x, centerY)

            Case TinySliderThumbStyle.Ring
                DrawRingThumb(g, x, centerY)

            Case TinySliderThumbStyle.Needle
                DrawNeedleThumb(g, x, centerY)

            Case Else
                DrawCircleThumb(g, x, centerY)
        End Select
    End Sub

    Private Sub DrawCircleThumb(g As Graphics, x As Integer, centerY As Single)
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim shadowOffset As Integer = ScaleToCurrentDpi(1)
        Dim thumbRect As New RectangleF(x - r, centerY - r, r * 2, r * 2)

        Using shadowBrush As New SolidBrush(Color.FromArgb(35, Color.Black))
            g.FillEllipse(shadowBrush, thumbRect.X, thumbRect.Y + shadowOffset, thumbRect.Width, thumbRect.Height)
        End Using

        Using thumbBrush As New SolidBrush(If(Enabled, _thumbColor, Color.FromArgb(245, 245, 245))),
          borderPen As New Pen(If(Enabled, ForeColor, Color.FromKnownColor(KnownColor.GrayText)), ScalePenWidth(1.5F))
            g.FillEllipse(thumbBrush, thumbRect)
            g.DrawEllipse(borderPen, thumbRect)
        End Using
    End Sub

    Private Sub DrawTriangleThumb(g As Graphics, x As Integer, centerY As Single)
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim shadowOffset As Integer = ScaleToCurrentDpi(1)
        Dim halfWidth As Single = r + ScaleToCurrentDpi(1)

        Dim topY As Single = centerY - r
        Dim bottomY As Single = centerY + r

        Dim points() As PointF

        If _thumbDirection = TinySliderThumbDirection.Up Then
            points = {
            New PointF(x, topY),
            New PointF(x + halfWidth, bottomY),
            New PointF(x - halfWidth, bottomY)
        }
        Else
            points = {
            New PointF(x - halfWidth, topY),
            New PointF(x + halfWidth, topY),
            New PointF(x, bottomY)
        }
        End If

        DrawFilledPolygonThumb(g, points, shadowOffset)
    End Sub

    Private Sub DrawNeedleThumb(g As Graphics, x As Integer, centerY As Single)
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim shadowOffset As Integer = ScaleToCurrentDpi(1)

        Dim bodyHalf As Single = Math.Max(ScaleToCurrentDpi(3), r * 0.85F)
        Dim midHalf As Single = Math.Max(ScaleToCurrentDpi(2), r * 0.65F)
        Dim tipHalf As Single = Math.Max(ScaleToCurrentDpi(1), r * 0.45F)

        Dim cornerRadius As Single = Math.Max(ScaleToCurrentDpi(1), r * 0.45F)

        Dim points() As PointF

        If _thumbDirection = TinySliderThumbDirection.Up Then
            Dim tipY As Single = centerY - r - ScaleToCurrentDpi(3)
            Dim preTipY As Single = centerY - r * 0.45F
            Dim taperY As Single = centerY - r * 0.05F
            Dim bodyTopY As Single = centerY + r * 0.25F
            Dim bottomY As Single = centerY + r + ScaleToCurrentDpi(4)

            points = {
            New PointF(x, tipY),
            New PointF(x + tipHalf, preTipY),
            New PointF(x + midHalf, taperY),
            New PointF(x + bodyHalf, bodyTopY),
            New PointF(x + bodyHalf, bottomY),
            New PointF(x - bodyHalf, bottomY),
            New PointF(x - bodyHalf, bodyTopY),
            New PointF(x - midHalf, taperY),
            New PointF(x - tipHalf, preTipY)
        }
        Else
            Dim topY As Single = centerY - r - ScaleToCurrentDpi(4)
            Dim bodyBottomY As Single = centerY - r * 0.25F
            Dim taperY As Single = centerY + r * 0.05F
            Dim preTipY As Single = centerY + r * 0.45F
            Dim tipY As Single = centerY + r + ScaleToCurrentDpi(3)

            points = {
            New PointF(x - bodyHalf, topY),
            New PointF(x + bodyHalf, topY),
            New PointF(x + bodyHalf, bodyBottomY),
            New PointF(x + midHalf, taperY),
            New PointF(x + tipHalf, preTipY),
            New PointF(x, tipY),
            New PointF(x - tipHalf, preTipY),
            New PointF(x - midHalf, taperY),
            New PointF(x - bodyHalf, bodyBottomY)
        }
        End If

        Using path As GraphicsPath = RoundedPolygon(points, cornerRadius)
            Using shadowPath As GraphicsPath = CType(path.Clone(), GraphicsPath)
                Using m As New Matrix()
                    m.Translate(0.0F, CSng(shadowOffset))
                    shadowPath.Transform(m)
                End Using

                Using shadowBrush As New SolidBrush(Color.FromArgb(35, Color.Black))
                    g.FillPath(shadowBrush, shadowPath)
                End Using
            End Using

            Using thumbBrush As New SolidBrush(If(Enabled, _thumbColor, Color.FromArgb(245, 245, 245))),
              borderPen As New Pen(If(Enabled, ForeColor, Color.FromKnownColor(KnownColor.GrayText)), ScalePenWidth(1.2F))

                borderPen.LineJoin = LineJoin.Round
                borderPen.StartCap = LineCap.Round
                borderPen.EndCap = LineCap.Round

                g.FillPath(thumbBrush, path)
                g.DrawPath(borderPen, path)
            End Using
        End Using
    End Sub
    Private Shared Function RoundedPolygon(points() As PointF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()

        If points Is Nothing OrElse points.Length < 3 OrElse radius <= 0.0F Then
            If points IsNot Nothing AndAlso points.Length >= 3 Then
                path.AddPolygon(points)
            End If
            Return path
        End If

        Dim count As Integer = points.Length
        Dim startPoints(count - 1) As PointF
        Dim endPoints(count - 1) As PointF

        For i As Integer = 0 To count - 1
            Dim prev As PointF = points((i - 1 + count) Mod count)
            Dim current As PointF = points(i)
            Dim nextPoint As PointF = points((i + 1) Mod count)

            Dim lenPrev As Single = Distance(current, prev)
            Dim lenNext As Single = Distance(current, nextPoint)

            Dim cut As Single = Math.Min(radius, Math.Min(lenPrev, lenNext) / 2.0F)

            startPoints(i) = MoveTowards(current, prev, cut)
            endPoints(i) = MoveTowards(current, nextPoint, cut)
        Next

        path.StartFigure()

        path.AddLine(endPoints(0), startPoints(1))

        For i As Integer = 1 To count - 1
            path.AddBezier(startPoints(i), points(i), points(i), endPoints(i))

            Dim nextIndex As Integer = (i + 1) Mod count
            path.AddLine(endPoints(i), startPoints(nextIndex))
        Next

        path.AddBezier(startPoints(0), points(0), points(0), endPoints(0))
        path.CloseFigure()

        Return path
    End Function

    Private Shared Function Distance(a As PointF, b As PointF) As Single
        Dim dx As Single = b.X - a.X
        Dim dy As Single = b.Y - a.Y
        Return CSng(Math.Sqrt(dx * dx + dy * dy))
    End Function

    Private Shared Function MoveTowards(fromPoint As PointF, toPoint As PointF, distance As Single) As PointF
        Dim dx As Single = toPoint.X - fromPoint.X
        Dim dy As Single = toPoint.Y - fromPoint.Y
        Dim length As Single = CSng(Math.Sqrt(dx * dx + dy * dy))

        If length <= 0.0001F Then Return fromPoint

        Dim ratio As Single = distance / length

        Return New PointF(
        fromPoint.X + dx * ratio,
        fromPoint.Y + dy * ratio
    )
    End Function
    Private Sub DrawDiamondThumb(g As Graphics, x As Integer, centerY As Single)
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim shadowOffset As Integer = ScaleToCurrentDpi(1)

        Dim points() As PointF = {
        New PointF(x, centerY - r),
        New PointF(x + r, centerY),
        New PointF(x, centerY + r),
        New PointF(x - r, centerY)
    }

        DrawFilledPolygonThumb(g, points, shadowOffset)
    End Sub

    Private Sub DrawVerticalBarThumb(g As Graphics, x As Integer, centerY As Single)
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim shadowOffset As Integer = ScaleToCurrentDpi(1)

        Dim barWidth As Integer = Math.Max(ScaleToCurrentDpi(4), r)
        Dim barHeight As Integer = Math.Max(ScaleToCurrentDpi(14), r * 3)

        Dim rect As New RectangleF(
        x - barWidth / 2.0F,
        centerY - barHeight / 2.0F,
        barWidth,
        barHeight
    )

        Dim radius As Single = Math.Min(rect.Width, rect.Height) / 2.0F

        Using shadowPath As GraphicsPath = RoundedRect(New RectangleF(rect.X, rect.Y + shadowOffset, rect.Width, rect.Height), radius),
          shadowBrush As New SolidBrush(Color.FromArgb(35, Color.Black))
            g.FillPath(shadowBrush, shadowPath)
        End Using

        Using barPath As GraphicsPath = RoundedRect(rect, radius),
          thumbBrush As New SolidBrush(If(Enabled, _thumbColor, Color.FromArgb(245, 245, 245))),
          borderPen As New Pen(If(Enabled, ForeColor, Color.FromKnownColor(KnownColor.GrayText)), ScalePenWidth(1.2F))
            g.FillPath(thumbBrush, barPath)
            g.DrawPath(borderPen, barPath)
        End Using
    End Sub

    Private Sub DrawRingThumb(g As Graphics, x As Integer, centerY As Single)
        Dim r As Integer = ScaleToCurrentDpi(_thumbRadius)
        Dim shadowOffset As Integer = ScaleToCurrentDpi(1)
        Dim ringPenWidth As Single = ScalePenWidth(2.0F)

        Dim rect As New RectangleF(x - r, centerY - r, r * 2, r * 2)

        Using shadowPen As New Pen(Color.FromArgb(35, Color.Black), ringPenWidth)
            g.DrawEllipse(shadowPen, rect.X, rect.Y + shadowOffset, rect.Width, rect.Height)
        End Using

        Using ringPen As New Pen(If(Enabled, _thumbColor, Color.FromArgb(170, 170, 170)), ringPenWidth),
          borderPen As New Pen(If(Enabled, ForeColor, Color.FromKnownColor(KnownColor.GrayText)), ScalePenWidth(1.0F))
            g.DrawEllipse(ringPen, rect)
            g.DrawEllipse(borderPen, rect)
        End Using
    End Sub

    Private Sub DrawFilledPolygonThumb(g As Graphics, points() As PointF, shadowOffset As Integer)
        Dim shadowPoints() As PointF = OffsetPoints(points, 0.0F, CSng(shadowOffset))

        Using shadowBrush As New SolidBrush(Color.FromArgb(35, Color.Black))
            g.FillPolygon(shadowBrush, shadowPoints)
        End Using

        Using thumbBrush As New SolidBrush(If(Enabled, _thumbColor, Color.FromArgb(245, 245, 245))),
          borderPen As New Pen(If(Enabled, ForeColor, Color.FromKnownColor(KnownColor.GrayText)), ScalePenWidth(1.2F))
            g.FillPolygon(thumbBrush, points)
            g.DrawPolygon(borderPen, points)
        End Using
    End Sub

    Private Shared Function OffsetPoints(points() As PointF, dx As Single, dy As Single) As PointF()
        Dim result(points.Length - 1) As PointF

        For i As Integer = 0 To points.Length - 1
            result(i) = New PointF(points(i).X + dx, points(i).Y + dy)
        Next

        Return result
    End Function

    Private Shared Function RoundedRect(rect As RectangleF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim r As Single = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2.0F)
        Dim d As Single = r * 2.0F

        If d <= 0 Then
            path.AddRectangle(rect)
            Return path
        End If

        path.AddArc(rect.Left, rect.Top, d, d, 180, 90)
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90)
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function
End Class
