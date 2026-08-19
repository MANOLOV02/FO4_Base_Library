''' <summary>
''' SSE (Skyrim) head-morph slider map — the engine's NAM9/NAMA -> chargen-morph mapping, byte-verified
''' against SkyrimSE.exe (slider table @0x1ff92a0). Single source of truth shared by the render/bake morph
''' resolver AND the face editor (so the UI sliders, the live render and the bake all agree).
'''
''' NAM9 = 18 signed floats (+ a 19th trailing float, unused here). Each slider i drives the chargen TRI
''' morph <see cref="Slider.Pos"/> when value>=0 or <see cref="Slider.Neg"/> when value&lt;0, weighted by |value|.
''' NAMA = 4 uint32 "type" indices (Nose, Brow, Eyes, Mouth); index 0 = "Default", index N = family+N morph
''' (e.g. Eyes 3 -> "EyesType3"). 0xFFFFFFFF = unset.
''' </summary>
Public NotInheritable Class SseNam9MorphMap
    Private Sub New()
    End Sub

    ''' <summary>One NAM9 slider: the positive/negative chargen-morph names and a human label for the UI.</summary>
    Public Structure Slider
        Public ReadOnly Pos As String
        Public ReadOnly Neg As String
        Public ReadOnly Label As String
        Public Sub New(pos As String, neg As String, label As String)
            Me.Pos = pos : Me.Neg = neg : Me.Label = label
        End Sub
    End Structure

    ''' <summary>The 18 NAM9 sliders in engine order (index = NAM9 float index). Names are byte-verified
    ''' chargen TRI morph names; labels are for the editor UI.</summary>
    Public Shared ReadOnly Sliders As Slider() = {
        New Slider("NoseLong", "NoseShort", "Nose Length"),
        New Slider("NoseUp", "NoseDown", "Nose Height"),
        New Slider("JawDown", "JawUp", "Jaw Height"),
        New Slider("JawWide", "JawNarrow", "Jaw Width"),
        New Slider("JawForward", "JawBack", "Jaw Forward"),
        New Slider("CheeksUp", "CheeksDown", "Cheekbone Height"),
        New Slider("CheeksOut", "CheeksIn", "Cheekbone Width"),
        New Slider("EyesMoveUp", "EyesMoveDown", "Eye Height"),
        New Slider("EyesMoveOut", "EyesMoveIn", "Eye Width"),
        New Slider("BrowUp", "BrowDown", "Brow Height"),
        New Slider("BrowOut", "BrowIn", "Brow Width"),
        New Slider("BrowForward", "BrowBack", "Brow Forward"),
        New Slider("LipMoveUp", "LipMoveDown", "Mouth Height"),
        New Slider("LipMoveOut", "LipMoveIn", "Mouth Forward"),
        New Slider("ChinWide", "ChinThin", "Chin Width"),
        New Slider("ChinMoveDown", "ChinMoveUp", "Chin Length"),
        New Slider("Underbite", "Overbite", "Chin Forward"),
        New Slider("EyesForward", "EyesBack", "Eye Depth")}

    ''' <summary>A NAMA "type" family: the chargen-morph name prefix and a UI label. NAMA value N selects
    ''' "&lt;Prefix&gt;N" (N>=1); value 0 = "Default" (no type morph).</summary>
    Public Structure TypeFamily
        Public ReadOnly Prefix As String
        Public ReadOnly Label As String
        Public Sub New(prefix As String, label As String)
            Me.Prefix = prefix : Me.Label = label
        End Sub
    End Structure

    ''' <summary>The 4 NAMA families in engine order (index = NAMA uint index): Nose, Brow, Eyes, Mouth.</summary>
    Public Shared ReadOnly Families As TypeFamily() = {
        New TypeFamily("NoseType", "Nose Type"),
        New TypeFamily("BrowType", "Brow Type"),
        New TypeFamily("EyesType", "Eyes Type"),
        New TypeFamily("LipType", "Mouth Type")}

    Public Const Nam9SliderCount As Integer = 18

    ''' <summary>El vector NAMA por defecto: TODAS las familias en el centinela "sin tipo asignado"
    ''' (<see cref="NamaUnset"/>), que NO es lo mismo que el tipo 0. Existe para que las cuatro ramas que
    ''' construyen un NAMA vacio digan lo mismo — estaban repartidas entre MainForm y PresetCategoryFilter y
    ''' dos de ellas devolvian CEROS, o sea el tipo 0 real.</summary>
    Public Shared Function DefaultNamaVector() As UInteger()
        Dim v(NamaFamilyCount - 1) As UInteger
        For i = 0 To v.Length - 1 : v(i) = NamaUnset : Next
        Return v
    End Function

    ''' <summary>El slot 18 del NAM9 (VampireMorph) de un payload crudo, o Nothing si no llega a ese slot.
    ''' <para>Vive ACA y no en cada consumidor porque son TRES los que lo necesitan (BuildPresetFromState en sus
    ''' dos ramas, y el revert de la categoria FaceVertexMorphs) y la primera version quedo escrita en una sola,
    ''' con lo cual el arreglo era inerte en el camino normal.</para>
    ''' <para>NAM9 son 19 floats (76 bytes); el modelo editable dimensiona 18 sliders, asi que este slot no entra
    ''' en <c>SseNam9</c> y hay que llevarlo aparte.</para></summary>
    Public Shared Function VampireMorphFromNam9Raw(nam9Raw As Byte()) As Single?
        If nam9Raw Is Nothing OrElse nam9Raw.Length < (Nam9SliderCount + 1) * 4 Then Return Nothing
        Return BitConverter.ToSingle(nam9Raw, Nam9SliderCount * 4)
    End Function

    Public Const NamaFamilyCount As Integer = 4
    Public Const NamaUnset As UInteger = &HFFFFFFFFUI

    ''' <summary>The chargen-morph name a slider value selects (Pos if >=0, Neg if &lt;0), or "" if EXACTLY zero.
    ''' ZERO TEST, NOT A DEADZONE. SOURCE: RaceMenu tests <c>value != 0</c> (FaceMorphInterface.cpp:1140 and
    ''' :1512) and the engine only compares the slot against FLT_MAX (the "never set" sentinel) before applying
    ''' the value unconditionally — NEITHER has a magnitude threshold, so inventing one is a divergence.
    ''' The old <c>Math.Abs(value) &lt; 0.001F</c> deadzone silently dropped 25 vanilla values; all 25 are
    ''' DENORMALS (~1e-38), i.e. numeric noise that a plain <c>&lt;&gt; 0</c> also has to route somewhere, while
    ''' the smallest genuinely AUTHORED value in the corpus is 0.02 — two orders of magnitude above the old
    ''' threshold, so no authored value was ever in the deadzone and none changes behavior here.
    ''' The NaN/Inf guard is KEPT: those are not values the engine's sentinel check would let through.</summary>
    Public Shared Function MorphForSlider(sliderIndex As Integer, value As Single) As String
        If sliderIndex < 0 OrElse sliderIndex >= Sliders.Length Then Return ""
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) OrElse value = 0.0F Then Return ""
        Return If(value >= 0, Sliders(sliderIndex).Pos, Sliders(sliderIndex).Neg)
    End Function

    ''' <summary>The chargen-morph name a NAMA type value selects ("Default" for 0, "&lt;Prefix&gt;N" for N>=1),
    ''' or "" if unset (0xFFFFFFFF).</summary>
    Public Shared Function MorphForType(familyIndex As Integer, value As UInteger) As String
        If familyIndex < 0 OrElse familyIndex >= Families.Length OrElse value = NamaUnset Then Return ""
        If value = 0UI Then Return "Default"
        Return Families(familyIndex).Prefix & value.ToString()
    End Function

    ''' <summary>INVERSA de <see cref="MorphForType"/>: ¿este nombre de morph es el miembro N de esta familia?
    ''' <para>⭐ La validación es un ROUND-TRIP contra el propio constructor, no un parseo de la cola. El motor
    ''' arma el nombre con <c>sprintf("%s%d", family, N)</c>, así que un nombre sólo es miembro si el
    ''' constructor lo reproduce EXACTAMENTE. Eso descarta gratis y sin reglas extra:</para>
    ''' <list type="bullet">
    ''' <item>ceros a la izquierda — <c>NoseType03</c> parsea a 3, pero el motor pide <c>NoseType3</c>, que es
    ''' OTRO morph (o ninguno) ⇒ ofrecerlo sería una entrada que no mueve un vértice;</item>
    ''' <item>signos y separadores — <c>NoseType+1</c>, <c>NoseType 1</c> (por eso <c>NumberStyles.None</c> e
    ''' <c>InvariantCulture</c>: el overload corto de <c>TryParse</c> acepta signo y depende del locale);</item>
    ''' <item><c>N = 0</c> — el valor 0 selecciona el morph "Default", NO <c>&lt;Prefix&gt;0</c>. Contarlo como
    ''' miembro duplicaría la fila "Default" del combo con el mismo valor.</item>
    ''' </list>
    ''' <para>Case-insensitive porque <see cref="TriHeadFile.GetMorph"/> lo es: si el motor aplica
    ''' <c>nosetype3</c>, el catálogo tiene que verlo.</para></summary>
    Public Shared Function TryParseFamilyMember(familyIndex As Integer, morphName As String, ByRef value As UInteger) As Boolean
        value = 0UI
        If familyIndex < 0 OrElse familyIndex >= Families.Length Then Return False
        If String.IsNullOrEmpty(morphName) Then Return False
        Dim prefix = Families(familyIndex).Prefix
        If morphName.Length <= prefix.Length Then Return False
        If Not morphName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then Return False
        Dim n As UInteger
        If Not UInteger.TryParse(morphName.Substring(prefix.Length), Globalization.NumberStyles.None,
                                 Globalization.CultureInfo.InvariantCulture, n) Then Return False
        If n = 0UI Then Return False
        If Not String.Equals(MorphForType(familyIndex, n), morphName, StringComparison.OrdinalIgnoreCase) Then Return False
        value = n
        Return True
    End Function

    ''' <summary>Los tipos NAMA que un conjunto de nombres de morph pone al alcance, por familia.
    ''' <para><see cref="IsKnown"/> distingue "leí los .tri y esta familia no tiene tipos" (caso REAL: vanilla
    ''' no trae ningún <c>BrowType</c>) de "todavía no pude leer ningún .tri". Colapsar los dos en una lista
    ''' vacía es lo que obligaría a la UI a elegir entre mentir y bloquear.</para></summary>
    Public NotInheritable Class NamaTypeCatalog
        ''' <summary>Por familia, los N disponibles ORDENADOS y SIN REPETIR. El dedup no es cosmético:
        ''' <c>femaleheadchargen.tri</c> trae <c>NoseType9</c> DUPLICADO (índices 44 y 45) y
        ''' <c>mouthhumanfchargen.tri</c> repite <c>LipType18</c> — medido 2026-08-17 con
        ''' <c>NpcSseRoundtripProbe --tricollide</c>. Sin dedup el combo mostraría dos filas idénticas.</summary>
        Public ReadOnly Available As List(Of UInteger)()
        ''' <summary>Por familia: ¿existe el morph "Default" (el que selecciona el valor 0)?</summary>
        Public ReadOnly HasDefault As Boolean()
        ''' <summary>False = no se leyó ningún .tri todavía. Las listas vacías NO significan "sin tipos".</summary>
        Public ReadOnly IsKnown As Boolean

        Friend Sub New(available As List(Of UInteger)(), hasDefault As Boolean(), isKnown As Boolean)
            Me.Available = available : Me.HasDefault = hasDefault : Me.IsKnown = isKnown
        End Sub

        Public Shared Function Unknown() As NamaTypeCatalog
            Return New NamaTypeCatalog(EmptyFamilies(), New Boolean(NamaFamilyCount - 1) {}, False)
        End Function

        ''' <summary>Catálogo CONOCIDO y sin tipos — se leyó y no hay ninguno. Distinto de
        ''' <see cref="Unknown"/>, que es "no se pudo leer".</summary>
        Public Shared Function KnownEmpty() As NamaTypeCatalog
            Return New NamaTypeCatalog(EmptyFamilies(), New Boolean(NamaFamilyCount - 1) {}, True)
        End Function

        Private Shared Function EmptyFamilies() As List(Of UInteger)()
            Dim av(NamaFamilyCount - 1) As List(Of UInteger)
            For f = 0 To NamaFamilyCount - 1 : av(f) = New List(Of UInteger)() : Next
            Return av
        End Function
    End Class

    ''' <summary>Atajo de <see cref="NamaTypeCatalog.KnownEmpty"/> para los llamadores que ya saben que no
    ''' hay nada que leer (p.ej. ninguna head part declara chargen .tri).</summary>
    Public Shared Function KnownEmptyTypeCatalog() As NamaTypeCatalog
        Return NamaTypeCatalog.KnownEmpty()
    End Function

    ''' <summary>Arma el catálogo desde los nombres de morph de los chargen .tri en juego. PURO: sin I/O y sin
    ''' UI, así que lo consumen por igual el editor y el reverse-engineer de morphs (una sola ley, un solo lugar).
    ''' <paramref name="morphNames"/> vacío ⇒ catálogo <c>IsKnown=False</c>.</summary>
    Public Shared Function BuildTypeCatalog(morphNames As IEnumerable(Of String)) As NamaTypeCatalog
        If morphNames Is Nothing Then Return NamaTypeCatalog.Unknown()
        Dim seen(NamaFamilyCount - 1) As HashSet(Of UInteger)
        Dim available(NamaFamilyCount - 1) As List(Of UInteger)
        Dim hasDefault(NamaFamilyCount - 1) As Boolean
        For f = 0 To NamaFamilyCount - 1
            seen(f) = New HashSet(Of UInteger)()
            available(f) = New List(Of UInteger)()
        Next
        For Each nm In morphNames
            If String.IsNullOrEmpty(nm) Then Continue For
            If String.Equals(nm, "Default", StringComparison.OrdinalIgnoreCase) Then
                For f = 0 To NamaFamilyCount - 1 : hasDefault(f) = True : Next
                Continue For
            End If
            For f = 0 To NamaFamilyCount - 1
                Dim n As UInteger
                If TryParseFamilyMember(f, nm, n) AndAlso seen(f).Add(n) Then available(f).Add(n)
            Next
        Next
        ' ⛔ Esta función NO opina sobre known-ness: siempre devuelve un catálogo CONOCIDO (aunque quede
        ' vacío). Quién sabe si los datos se pudieron leer es el LLAMADOR — antes acá había un segundo
        ' "si no vi ningún nombre ⇒ Unknown" que PISABA esa decisión, y un .tri que parsea con 0 morphs
        ' terminaba deshabilitando el combo por el motivo equivocado. Una sola ley, en un solo lugar.
        For f = 0 To NamaFamilyCount - 1 : available(f).Sort() : Next
        Return New NamaTypeCatalog(available, hasDefault, True)
    End Function
End Class
