' Version Uploaded of Wardrobe 3.2.0
Public Class ColorComboBox
    Inherits ComboBox
    Public Property Dibuja As Boolean = False
    Public Sub New()
        MyBase.New()
        Me.DropDownStyle = ComboBoxStyle.DropDownList
        Me.DrawMode = DrawMode.OwnerDrawFixed
    End Sub
    Public Sub Rellena()
        Me.Items.Clear()

        For Each kc As KnownColor In [Enum].GetValues(GetType(KnownColor))
            Me.Items.Add(kc)
        Next
        Dibuja = True
    End Sub

    Protected Overrides Sub OnDrawItem(e As DrawItemEventArgs)
        If Not Dibuja Then
            MyBase.OnDrawItem(e)
            Exit Sub
        End If


        If Me.Enabled Then
            e.DrawBackground()
        Else
            Using br As New SolidBrush(SystemColors.Control)
                e.Graphics.FillRectangle(br, e.Bounds)
            End Using
        End If


        If e.Index >= 0 Then
            Dim kc As KnownColor = CType(Items(e.Index), KnownColor)
            Dim c As Color = Color.FromKnownColor(kc)

            Dim swatchSize As Integer = e.Bounds.Height - 4
            Dim swatchRect As New Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, swatchSize, swatchSize)

            Using b As New SolidBrush(c)
                e.Graphics.FillRectangle(b, swatchRect)
                e.Graphics.DrawRectangle(Pens.Black, swatchRect)
            End Using

            Dim textRect As New Rectangle(swatchRect.Right + 4, e.Bounds.Y, e.Bounds.Width - swatchRect.Width - 6, e.Bounds.Height)
            Dim textColor As Color = If(Me.Enabled, e.ForeColor, SystemColors.GrayText)

            TextRenderer.DrawText(e.Graphics, kc.ToString(), Font, textRect, textColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left)
        End If

        e.DrawFocusRectangle()
        MyBase.OnDrawItem(e)
    End Sub

    ''' <summary>
    ''' Obtiene o establece el System.Drawing.Color seleccionado en el ComboBox.
    ''' Al asignar un Color, busca su KnownColor asociado y selecciona ese ítem.
    ''' </summary>
    Public Property SelectedColor As Color
        Get
            If Me.SelectedIndex >= 0 Then
                If TypeOf Me.SelectedItem Is KnownColor Then
                    Dim kc As KnownColor = CType(Me.SelectedItem, KnownColor)
                    Return Color.FromKnownColor(kc)
                Else
                    Return Color.Empty
                End If
            Else
                Return Color.Black
            End If
        End Get
        Set(value As Color)
            If Dibuja Then
                If value.IsKnownColor Then
                    Dim kc As KnownColor = value.ToKnownColor()
                    If Me.Items.Contains(kc) Then
                        Me.SelectedIndex = Me.Items.IndexOf(kc)
                    Else
                        Me.SelectedIndex = -1
                    End If
                Else
                    Me.SelectedIndex = -1
                End If
            End If
        End Set
    End Property

End Class
