Option Strict On
Imports System.Windows.Forms

''' <summary>Un <see cref="NumericUpDown"/> que lee el numero TIPEADO con la misma ley que
''' <see cref="TinySliderTextBox"/>: acepta coma O punto como separador decimal, sea cual sea el locale de
''' la maquina, y NO interpreta ningun separador como de miles.
'''
''' <para>POR QUE EXISTE. <c>NumericUpDown.ParseEditText</c> hace
''' <c>Decimal.Parse(Text, CultureInfo.CurrentCulture)</c>, y esa sobrecarga usa <c>NumberStyles.Number</c>,
''' que incluye <b>AllowThousands</b>. En un Windows es-AR (decimal ",", grupo ".") tipear <c>41.4</c> NO
''' falla: .NET se come el punto como separador de miles —no valida el tamanio de los grupos— y devuelve
''' <b>414</b>. Ese 414 entra al clamp del control y sale como el <b>Maximum</b>. Medido en esta app:</para>
''' <list type="bullet">
''' <item><c>41.4</c> en el angulo de costura (max 180) =&gt; <b>180</b>. Un umbral de 180 grados promedia
''' TODA costura sin mirar el angulo: se van las aristas duras de cada horneado.</item>
''' <item><c>0.000000005</c> en el epsilon de weld (max 1e-3) =&gt; <b>0,001</b>, o sea 200.000 veces mas
''' grande que lo que la persona escribio.</item>
''' </list>
''' <para>El usuario ve saltar el valor y no tiene NINGUNA forma de entender por que. Y desde que el
''' dialogo commitea al cerrar (<c>LightRigForm.FormClosing</c> lee los <c>.Value</c>), el numero
''' equivocado se GRABA en el config.json y sobrevive a la sesion.</para>
'''
''' <para>Es EXACTAMENTE el defecto que <c>TinySliderTextBox.TryParseFlexibleDouble</c> ya cierra para la
''' otra familia de controles del mismo dialogo, con su propio gate (<c>slider-cultura</c> en ParityGate).
''' Los <c>NumericUpDown</c> quedaron fuera de ese alcance porque no pasan por ese parser: tienen el suyo,
''' adentro de WinForms. Esta clase los mete bajo la misma ley en vez de dejar dos criterios distintos en
''' la misma pestania.</para>
'''
''' <para>COSTO ACEPTADO, MEDIDO Y CON UN CASO QUE EMPEORA. La regla es "el ULTIMO separador que aparece
''' es el decimal", asi que <c>1.000</c> se lee como <b>1</b> y no como mil — en LAS DOS CULTURAS:</para>
''' <code>es-AR  '1.000' -&gt; 1   (el control de stock daba 1000)
''' en-US  '1,000' -&gt; 1   (el control de stock daba 1000)</code>
''' <para>Para los tres epsilons (maximo 1e-3) no hay nada que perder: un separador de miles ahi no puede
''' significar nada. Donde SI se pierde algo es en <c>nudFloorSize</c> / <c>nudFloorStep</c>, cuyo rango llega
''' a 100.000: quien escriba el tamanio del piso como <c>1,000</c> queriendo decir mil obtiene 1. Se acepta
''' igual, y el balance es explicito: el caso que se rompe es RECUPERABLE —se ve el 1 y se vuelve a tipear—
''' mientras que el que se arregla era SILENCIOSO y quedaba grabado en el config (<c>41.4</c> convertido en
''' 180 grados de umbral de costura se lleva puestas las aristas duras de cada horneado). Ademas ningun
''' control pone <c>ThousandsSeparator = True</c>, asi que la app nunca MUESTRA un separador de miles: lo que
''' se ve en esa caja es <c>1000,000</c>, y tipear un separador es inventarlo.</para>
'''
''' <para>El override va en <c>ValidateEditText</c> y no en <c>ParseEditText</c> porque el segundo no es
''' <c>Overridable</c>. <c>ValidateEditText</c> es el embudo: lo llaman <c>OnLostFocus</c>, los botones de
''' incremento, y —esto es lo que hace que el commit al cerrar funcione— el GETTER de <c>Value</c> cuando
''' hay una edicion pendiente.</para>
''' <para>NO lo llama <c>OnValidating</c>, o sea que <c>ValidateChildren()</c> NO commitea un
''' <c>NumericUpDown</c>. MEDIDO contando llamadas al override: tras tipear, <c>ValidateChildren()</c>
''' devuelve True con <c>UserEdit=True</c> y CERO llamadas; leer <c>.Value</c> da una. Este renglon decia lo
''' contrario, y esa afirmacion exacta es la que ya causo una regresion (ver la nota de
''' <c>LightRigForm.FormClosing</c>): por eso ese <c>FormClosing</c> necesita LOS DOS mecanismos, uno por
''' familia de control.</para></summary>
Friend Class NumericUpDownCultura
    Inherits NumericUpDown

    ''' <summary>Toma el texto tipeado con la ley flexible, lo acota al rango del control y lo commitea.
    ''' <para>Si el texto no parsea (vacio, basura), NO se inventa un valor: cae al comportamiento de la
    ''' clase base, que repone el <c>Value</c> vigente. Tragarse el error y dejar un 0 seria peor que el
    ''' defecto que esta clase arregla.</para></summary>
    Protected Overrides Sub ValidateEditText()
        Dim d As Double
        If Not TinySliderTextBox.TryParseFlexibleDouble(Me.Text, d) OrElse
           Double.IsNaN(d) OrElse Double.IsInfinity(d) Then
            MyBase.ValidateEditText()
            Return
        End If

        ' Decimal tiene menos rango que Double: un 1e40 pegado a mano tira OverflowException.
        ' SE CONSERVA EL VALOR VIGENTE, NO SE SALTA AL EXTREMO. Antes esto hacia
        ' `v = If(d < 0, Minimum, Maximum)`, y medido con `1e40` en el epsilon de weld el control saltaba a
        ' 0,001 — o sea el MISMO modo de falla que esta clase existe para cerrar ("el usuario ve saltar el
        ' valor y no entiende por que"), entrando por el otro extremo. El NumericUpDown de stock, ante un
        ' overflow al parsear, conserva lo que habia; se hace lo mismo delegando en la base.
        Dim v As Decimal
        Try
            v = Convert.ToDecimal(d)
        Catch ex As OverflowException
            MyBase.ValidateEditText()
            Return
        End Try

        If v < Me.Minimum Then v = Me.Minimum
        If v > Me.Maximum Then v = Me.Maximum

        ' UserEdit = False ANTES de tocar Value: el getter de Value vuelve a llamar a este metodo mientras
        ' la bandera este puesta, y el setter lee el getter.
        UserEdit = False
        Me.Value = v
        UpdateEditText()
    End Sub

End Class
