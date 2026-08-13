Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.ComponentModel


<ToolboxItem(True)>
Public Class LightSphereControl
    Inherits UserControl


#Region "Campos"

    Private _azimuth As Single = 45.0F
    Private _elevation As Single = 30.0F
    Private _lightColor As Color = Color.Gold

#End Region


#Region "Estructura 3D"

    Private Structure Point3D

        Public X As Single
        Public Y As Single
        Public Z As Single

        Public Sub New(x As Single,
                       y As Single,
                       z As Single)

            Me.X = x
            Me.Y = y
            Me.Z = z

        End Sub

    End Structure

#End Region


#Region "Constructor"

    Public Sub New()

        MyBase.New()

        SetStyle(
            ControlStyles.UserPaint Or
            ControlStyles.AllPaintingInWmPaint Or
            ControlStyles.OptimizedDoubleBuffer Or
            ControlStyles.ResizeRedraw Or
            ControlStyles.SupportsTransparentBackColor,
            True)

        DoubleBuffered = True
        BackColor = Color.Transparent

        Size = New Size(72, 72)
        MinimumSize = New Size(40, 40)

        TabStop = False

        UpdateStyles()

    End Sub

#End Region


#Region "Propiedades"


    <Category("Light Sphere")>
    <Description("Azimut de la luz en grados. 0=frente, 90=derecha, 180=atrás, 270=izquierda.")>
    <DefaultValue(GetType(Single), "45")>
    Public Property Azimuth As Single

        Get
            Return _azimuth
        End Get

        Set(value As Single)

            Dim newValue As Single = value Mod 360.0F

            If newValue < 0.0F Then
                newValue += 360.0F
            End If

            If Math.Abs(_azimuth - newValue) < 0.0001F Then
                Return
            End If

            _azimuth = newValue

            Invalidate()

        End Set

    End Property



    <Category("Light Sphere")>
    <Description("Elevación de la luz. -90=abajo, 0=ecuador, +90=arriba.")>
    <DefaultValue(GetType(Single), "30")>
    Public Property Elevation As Single

        Get
            Return _elevation
        End Get

        Set(value As Single)

            Dim newValue As Single =
                Math.Max(
                    -90.0F,
                    Math.Min(90.0F, value))

            If Math.Abs(_elevation - newValue) < 0.0001F Then
                Return
            End If

            _elevation = newValue

            Invalidate()

        End Set

    End Property



    <Category("Light Sphere")>
    <Description("Color utilizado para representar la luz.")>
    Public Property LightColor As Color

        Get
            Return _lightColor
        End Get

        Set(value As Color)

            If _lightColor = value Then
                Return
            End If

            _lightColor = value

            Invalidate()

        End Set

    End Property


#End Region


#Region "Paint"

    Protected Overrides Sub OnPaint(e As PaintEventArgs)

        MyBase.OnPaint(e)

        If ClientSize.Width < 10 OrElse
           ClientSize.Height < 10 Then
            Return
        End If


        Dim g As Graphics = e.Graphics

        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality
        g.CompositingQuality = CompositingQuality.HighQuality


        Dim controlSize As Single =
            Math.Min(
                ClientSize.Width,
                ClientSize.Height)


        Dim radius As Single =
            controlSize * 0.405F


        Dim cx As Single =
            ClientSize.Width / 2.0F


        Dim cy As Single =
            ClientSize.Height / 2.0F


        Dim sphereRect As New RectangleF(
            cx - radius,
            cy - radius,
            radius * 2.0F,
            radius * 2.0F)


        '============================================================
        ' Posición 3D de la luz
        '============================================================

        Dim lightPosition As Point3D =
            GetLightPosition()


        Dim lightIsFront As Boolean =
            lightPosition.Z >= 0.0F


        '============================================================
        ' Vidrio / cuerpo transparente
        '============================================================

        DrawGlassSphere(
            g,
            sphereRect,
            cx,
            cy,
            radius)


        '============================================================
        ' Malla trasera
        '============================================================

        DrawGrid(
            g,
            cx,
            cy,
            radius,
            False)


        '============================================================
        ' Si la luz está atrás, la dibujamos antes de la malla
        ' delantera para que parezca verse a través de la esfera.
        '============================================================

        If Not lightIsFront Then

            DrawLight(
                g,
                cx,
                cy,
                radius,
                controlSize,
                lightPosition,
                False)

        End If


        '============================================================
        ' Malla delantera
        '============================================================

        DrawGrid(
            g,
            cx,
            cy,
            radius,
            True)


        '============================================================
        ' Contorno
        '============================================================

        Using p As New Pen(
            Color.FromArgb(
                205,
                75,
                90,
                105),
            1.2F)

            g.DrawEllipse(
                p,
                sphereRect)

        End Using


        '============================================================
        ' Si la luz está adelante, va encima de la esfera
        '============================================================

        If lightIsFront Then

            DrawLight(
                g,
                cx,
                cy,
                radius,
                controlSize,
                lightPosition,
                True)

        End If


        '============================================================
        ' Centro
        '============================================================

        DrawCenter(
            g,
            cx,
            cy,
            controlSize)

    End Sub

#End Region


#Region "Esfera"

    Private Sub DrawGlassSphere(g As Graphics,
                                sphereRect As RectangleF,
                                cx As Single,
                                cy As Single,
                                radius As Single)


        '============================================================
        ' Cuerpo transparente
        '============================================================

        Using path As New GraphicsPath()

            path.AddEllipse(sphereRect)


            Using brush As New PathGradientBrush(path)

                brush.CenterPoint =
                    New PointF(
                        cx - radius * 0.3F,
                        cy - radius * 0.32F)


                brush.CenterColor =
                    Color.FromArgb(
                        8,
                        255,
                        255,
                        255)


                brush.SurroundColors = {
                    Color.FromArgb(
                        30,
                        95,
                        125,
                        150)
                }


                g.FillEllipse(
                    brush,
                    sphereRect)

            End Using

        End Using


        '============================================================
        ' Brillo superior izquierdo
        '============================================================

        Dim reflectionRect As New RectangleF(
            cx - radius * 0.58F,
            cy - radius * 0.62F,
            radius * 0.82F,
            radius * 0.48F)


        Using p As New Pen(
            Color.FromArgb(
                105,
                255,
                255,
                255),
            1.05F)

            p.StartCap = LineCap.Round
            p.EndCap = LineCap.Round

            g.DrawArc(
                p,
                reflectionRect,
                195.0F,
                78.0F)

        End Using

    End Sub

#End Region


#Region "Malla"

    Private Sub DrawGrid(g As Graphics,
                         cx As Single,
                         cy As Single,
                         radius As Single,
                         front As Boolean)


        Dim normalColor As Color
        Dim strongColor As Color


        If front Then

            normalColor =
                Color.FromArgb(
                    88,
                    95,
                    110,
                    125)

            strongColor =
                Color.FromArgb(
                    210,
                    50,
                    65,
                    80)

        Else

            normalColor =
                Color.FromArgb(
                    28,
                    95,
                    110,
                    125)

            strongColor =
                Color.FromArgb(
                    55,
                    50,
                    65,
                    80)

        End If


        '============================================================
        ' LATITUDES
        '
        ' -60
        ' -30
        '   0  <- ECUADOR FUERTE
        ' +30
        ' +60
        '============================================================

        For elev As Integer = -60 To 60 Step 30

            Dim isEquator As Boolean =
                (elev = 0)


            Using p As New Pen(
                If(
                    isEquator,
                    strongColor,
                    normalColor),
                If(
                    isEquator,
                    1.35F,
                    0.7F))


                p.StartCap = LineCap.Round
                p.EndCap = LineCap.Round


                Dim previous As Point3D
                Dim havePrevious As Boolean = False


                For az As Integer = 0 To 360 Step 2

                    Dim current As Point3D =
                        SphericalToCartesian(
                            az,
                            elev)


                    If havePrevious Then

                        DrawGridSegment(
                            g,
                            p,
                            previous,
                            current,
                            cx,
                            cy,
                            radius,
                            front)

                    End If


                    previous = current
                    havePrevious = True

                Next

            End Using

        Next


        '============================================================
        ' LONGITUDES
        '
        ' Azimuth 0° está DIRECTAMENTE DE FRENTE.
        '
        ' El meridiano 0° es la línea vertical fuerte.
        '
        ' 90°  = derecha
        ' 180° = atrás
        ' 270° = izquierda
        '============================================================

        For az As Integer = 0 To 330 Step 30

            Dim isMainMeridian As Boolean =
                (az = 0 OrElse az = 180)


            Using p As New Pen(
                If(
                    isMainMeridian,
                    strongColor,
                    normalColor),
                If(
                    isMainMeridian,
                    1.35F,
                    0.7F))


                p.StartCap = LineCap.Round
                p.EndCap = LineCap.Round


                Dim previous As Point3D
                Dim havePrevious As Boolean = False


                For elev As Integer = -90 To 90 Step 2

                    Dim current As Point3D =
                        SphericalToCartesian(
                            az,
                            elev)


                    If havePrevious Then

                        DrawGridSegment(
                            g,
                            p,
                            previous,
                            current,
                            cx,
                            cy,
                            radius,
                            front)

                    End If


                    previous = current
                    havePrevious = True

                Next

            End Using

        Next

    End Sub



    Private Sub DrawGridSegment(g As Graphics,
                                pen As Pen,
                                a As Point3D,
                                b As Point3D,
                                cx As Single,
                                cy As Single,
                                radius As Single,
                                front As Boolean)


        Dim depth As Single =
            (a.Z + b.Z) * 0.5F


        ' Z positiva = frente.
        ' Z negativa = parte trasera.

        If front Then

            If depth < 0.0F Then
                Return
            End If

        Else

            If depth >= 0.0F Then
                Return
            End If

        End If


        Dim p1 As New PointF(
            cx + a.X * radius,
            cy - a.Y * radius)


        Dim p2 As New PointF(
            cx + b.X * radius,
            cy - b.Y * radius)


        g.DrawLine(
            pen,
            p1,
            p2)

    End Sub

#End Region


#Region "Luz"

    Private Function GetLightPosition() As Point3D

        Return SphericalToCartesian(
            _azimuth,
            _elevation)

    End Function



    Private Sub DrawLight(g As Graphics,
                          cx As Single,
                          cy As Single,
                          radius As Single,
                          controlSize As Single,
                          position As Point3D,
                          isFront As Boolean)


        Dim px As Single =
            cx + position.X * radius


        Dim py As Single =
            cy - position.Y * radius


        '============================================================
        ' Línea CENTRO -> LUZ
        '
        ' SOLID / CONTINUA.
        ' NO DOTTED.
        '============================================================

        Dim lineAlpha As Integer =
            If(
                isFront,
                145,
                48)


        Using p As New Pen(
            Color.FromArgb(
                lineAlpha,
                _lightColor),
            1.15F)


            p.DashStyle = DashStyle.Solid

            p.StartCap = LineCap.Round
            p.EndCap = LineCap.Round


            g.DrawLine(
                p,
                cx,
                cy,
                px,
                py)

        End Using


        '============================================================
        ' Tamaño del punto
        '============================================================

        Dim dotSize As Single =
            Math.Max(
                5.0F,
                controlSize * 0.1F)


        '============================================================
        ' Halo exterior
        '============================================================

        Dim haloSize As Single =
            dotSize * 2.15F


        Dim haloAlpha As Integer =
            If(
                isFront,
                60,
                22)


        Using haloBrush As New SolidBrush(
            Color.FromArgb(
                haloAlpha,
                _lightColor))


            g.FillEllipse(
                haloBrush,
                px - haloSize / 2.0F,
                py - haloSize / 2.0F,
                haloSize,
                haloSize)

        End Using


        '============================================================
        ' Punto de luz
        '============================================================

        Dim dotColor As Color


        If isFront Then

            dotColor = _lightColor

        Else

            dotColor =
                Color.FromArgb(
                    135,
                    _lightColor)

        End If


        Dim dotRect As New RectangleF(
            px - dotSize / 2.0F,
            py - dotSize / 2.0F,
            dotSize,
            dotSize)


        Using b As New SolidBrush(dotColor)

            g.FillEllipse(
                b,
                dotRect)

        End Using


        '============================================================
        ' Borde del punto
        '============================================================

        Using p As New Pen(
            Color.FromArgb(
                If(isFront, 235, 120),
                255,
                255,
                255),
            1.0F)

            g.DrawEllipse(
                p,
                dotRect)

        End Using


        '============================================================
        ' Brillito del punto
        '============================================================

        Dim shineSize As Single =
            Math.Max(
                1.4F,
                dotSize * 0.28F)


        Using b As New SolidBrush(
            Color.FromArgb(
                If(isFront, 230, 100),
                255,
                255,
                255))


            g.FillEllipse(
                b,
                px - dotSize * 0.22F,
                py - dotSize * 0.24F,
                shineSize,
                shineSize)

        End Using

    End Sub



    Private Sub DrawCenter(g As Graphics,
                           cx As Single,
                           cy As Single,
                           controlSize As Single)


        Dim centerSize As Single =
            Math.Max(
                2.2F,
                controlSize * 0.035F)


        Using b As New SolidBrush(
            Color.FromArgb(
                165,
                55,
                65,
                75))


            g.FillEllipse(
                b,
                cx - centerSize / 2.0F,
                cy - centerSize / 2.0F,
                centerSize,
                centerSize)

        End Using

    End Sub

#End Region


#Region "Coordenadas esféricas"

    Private Shared Function SphericalToCartesian(
        azimuthDegrees As Double,
        elevationDegrees As Double) As Point3D


        Dim az As Double =
            DegreesToRadians(
                azimuthDegrees)


        Dim el As Double =
            DegreesToRadians(
                elevationDegrees)


        '============================================================
        ' CONVENCIÓN
        '
        ' X = izquierda / derecha
        ' Y = abajo / arriba
        ' Z = atrás / frente
        '
        '
        ' Azimuth = 0
        '
        '     X = 0
        '     Y = 0
        '     Z = +1
        '
        '     -> directamente hacia el observador
        '
        '
        ' Azimuth = 90
        '
        '     X = +1
        '     Z = 0
        '
        '     -> derecha
        '
        '
        ' Azimuth = 180
        '
        '     Z = -1
        '
        '     -> atrás
        '
        '
        ' Azimuth = 270
        '
        '     X = -1
        '
        '     -> izquierda
        '
        '============================================================


        Dim cosElevation As Double =
            Math.Cos(el)


        Return New Point3D(
            CSng(
                cosElevation *
                Math.Sin(az)),
            CSng(
                Math.Sin(el)),
            CSng(
                cosElevation *
                Math.Cos(az)))

    End Function



    Private Shared Function DegreesToRadians(
        degrees As Double) As Double

        Return degrees *
               Math.PI /
               180.0

    End Function

#End Region


End Class
