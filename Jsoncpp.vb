Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes

''' <summary>
''' jsoncpp — la tabla de conversiones que usan los dos cargadores de presets: LoadPreset de LooksMenu
''' (f4ee/CharGenInterface.cpp) y LoadJsonPreset de RaceMenu (skee64/PresetInterface.cpp). Los dos motores
''' llevan la MISMA copia de <c>jsoncpp/json_value.cpp</c> (mismos numeros de linea), asi que la ley vive una
''' sola vez aca y cada cargador la consume sobre su arbol (<see cref="JsonElement"/> en LooksMenu,
''' <see cref="JsonNode"/> en RaceMenu).
'''
''' Un <see cref="JsonElement"/> "Undefined" (clave ausente) y un <see cref="JsonNode"/> <c>Nothing</c> se
''' tratan como null: <c>operator[]</c> no-const sobre un objeto crea el miembro null (:970-994) y sobre un
''' array fuera de rango tambien (:918-936).
'''
''' <c>ok</c> = False cuando jsoncpp lanzaria (<c>JSON_ASSERT_MESSAGE</c> / <c>JSON_FAIL_MESSAGE</c> ⇒
''' <c>std::runtime_error</c>, json/assertions.h:19 con JSON_USE_EXCEPTION=1 en json/config.h:33). En f4ee eso
''' lo atrapa el try/catch por canal de LoadPreset; en skee64 NO hay try/catch (PresetInterface.cpp:898-1249 ni
''' sus llamadores) y el archivo entero se rechaza.
''' </summary>
Public Module Jsoncpp

    ' ===================================================================================================
    ' Nucleo sobre JsonElement
    ' ===================================================================================================

    ''' <summary>jsoncpp clasifica el literal numerico al parsear (json_reader.cpp decodeNumber): sin punto ni
    ''' exponente y dentro de Int64/UInt64 es intValue/uintValue; si no, realValue (double).</summary>
    Private Function EsLiteralEntero(el As JsonElement) As Boolean
        Dim t = el.GetRawText()
        Return t.IndexOf("."c) < 0 AndAlso t.IndexOf("e"c) < 0 AndAlso t.IndexOf("E"c) < 0
    End Function

    ''' <summary><c>asFloat</c> (:758-778): null ⇒ 0, bool ⇒ 1/0, entero ⇒ cast directo, real ⇒ cast a float;
    ''' string/array/objeto lanzan.</summary>
    Public Function AsFloat(el As JsonElement, ByRef ok As Boolean) As Single
        ok = True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return 0.0F
            Case JsonValueKind.True : Return 1.0F
            Case JsonValueKind.False : Return 0.0F
            Case JsonValueKind.Number
                If EsLiteralEntero(el) Then
                    Dim i64 As Long, u64 As ULong
                    If el.TryGetInt64(i64) Then Return CSng(i64)
                    If el.TryGetUInt64(u64) Then Return CSng(u64)
                End If
                Return CSng(el.GetDouble())
            Case Else
                ok = False : Return 0.0F
        End Select
    End Function

    ''' <summary><c>asInt</c> (:631-651): null ⇒ 0, bool ⇒ 1/0, entero fuera de Int32 lanza (isInt :1163-1176),
    ''' real en rango [minInt, maxInt] (InRange :61-63) se trunca hacia cero (fuera de rango lanza);
    ''' string/array/objeto lanzan.</summary>
    Public Function AsInt(el As JsonElement, ByRef ok As Boolean) As Integer
        ok = True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return 0
            Case JsonValueKind.True : Return 1
            Case JsonValueKind.False : Return 0
            Case JsonValueKind.Number
                If EsLiteralEntero(el) Then
                    Dim i64 As Long
                    ok = el.TryGetInt64(i64) AndAlso i64 >= Integer.MinValue AndAlso i64 <= Integer.MaxValue
                    Return If(ok, CInt(i64), 0)
                End If
                Dim d = el.GetDouble()
                ok = (d >= Integer.MinValue AndAlso d <= Integer.MaxValue)
                Return If(ok, CInt(Math.Truncate(d)), 0)
            Case Else
                ok = False : Return 0
        End Select
    End Function

    ''' <summary><c>asUInt</c> (:653-673): null ⇒ 0, bool ⇒ 1/0, entero negativo o fuera de UInt32 lanza
    ''' (isUInt :1178-1191), real en [0, UInt32.Max] se trunca hacia cero (fuera lanza); string/array/objeto
    ''' lanzan.</summary>
    Public Function AsUInt(el As JsonElement, ByRef ok As Boolean) As UInteger
        ok = True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return 0UI
            Case JsonValueKind.True : Return 1UI
            Case JsonValueKind.False : Return 0UI
            Case JsonValueKind.Number
                If EsLiteralEntero(el) Then
                    Dim u64 As ULong
                    ok = el.TryGetUInt64(u64) AndAlso u64 <= UInteger.MaxValue
                    Return If(ok, CUInt(u64), 0UI)
                End If
                Dim d = el.GetDouble()
                ok = (d >= 0.0 AndAlso d <= UInteger.MaxValue)
                Return If(ok, CUInt(Math.Truncate(d)), 0UI)
            Case Else
                ok = False : Return 0UI
        End Select
    End Function

    ''' <summary><c>asBool</c> (:780-795): null ⇒ false, bool ⇒ tal cual, entero ⇒ != 0, real ⇒ != 0.0;
    ''' string/array/objeto lanzan.</summary>
    Public Function AsBool(el As JsonElement, ByRef ok As Boolean) As Boolean
        ok = True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return False
            Case JsonValueKind.True : Return True
            Case JsonValueKind.False : Return False
            Case JsonValueKind.Number
                If EsLiteralEntero(el) Then
                    Dim i64 As Long, u64 As ULong
                    If el.TryGetInt64(i64) Then Return i64 <> 0L
                    If el.TryGetUInt64(u64) Then Return u64 <> 0UL
                End If
                Return el.GetDouble() <> 0.0
            Case Else
                ok = False : Return False
        End Select
    End Function

    ''' <summary><c>asString</c> (:606-623): null ⇒ "", string ⇒ tal cual, numero ⇒ texto, bool ⇒ "true"/"false";
    ''' array/objeto lanzan. El texto de un real lo formatea jsoncpp con "%.17g"; aca se usa "R" — solo se usa
    ''' como identificador de form o de plantilla, que nunca es un numero.</summary>
    Public Function AsString(el As JsonElement, ByRef ok As Boolean) As String
        ok = True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return ""
            Case JsonValueKind.String : Return If(el.GetString(), "")
            Case JsonValueKind.True : Return "true"
            Case JsonValueKind.False : Return "false"
            Case JsonValueKind.Number
                If EsLiteralEntero(el) Then
                    Dim i64 As Long, u64 As ULong
                    If el.TryGetInt64(i64) Then Return i64.ToString(Globalization.CultureInfo.InvariantCulture)
                    If el.TryGetUInt64(u64) Then Return u64.ToString(Globalization.CultureInfo.InvariantCulture)
                End If
                Return el.GetDouble().ToString("R", Globalization.CultureInfo.InvariantCulture)
            Case Else
                ok = False : Return ""
        End Select
    End Function

    ''' <summary><c>asCString</c> (:600-604): SOLO string; todo lo demas (null incluido) lanza.</summary>
    Public Function AsCString(el As JsonElement, ByRef ok As Boolean) As String
        ok = (el.ValueKind = JsonValueKind.String)
        Return If(ok, If(el.GetString(), ""), "")
    End Function

    ''' <summary><c>empty()</c> (:862-867): null/array/objeto ⇒ <c>size() == 0</c> (:832-860); escalar y string ⇒
    ''' false (NO estan vacios).</summary>
    Public Function Empty(el As JsonElement) As Boolean
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return True
            Case JsonValueKind.Array : Return el.GetArrayLength() = 0
            Case JsonValueKind.Object : Return Not el.EnumerateObject().MoveNext()
            Case Else : Return False
        End Select
    End Function

    ''' <summary>Orden de claves de un objeto jsoncpp: <c>strcmp</c> sobre los bytes UTF-8 (:200-204).</summary>
    Public NotInheritable Class ComparadorStrcmp
        Implements IComparer(Of String)
        Public Shared ReadOnly Instancia As New ComparadorStrcmp()
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            Dim bx = Encoding.UTF8.GetBytes(If(x, ""))
            Dim by = Encoding.UTF8.GetBytes(If(y, ""))
            Dim n = Math.Min(bx.Length, by.Length)
            For i = 0 To n - 1
                If bx(i) <> by(i) Then Return CInt(bx(i)) - CInt(by(i))
            Next
            Return bx.Length - by.Length
        End Function
    End Class

    ''' <summary><c>getMemberNames</c> + acceso por clave (:1105-1127): null/ausente ⇒ vacio; objeto ⇒ sus
    ''' miembros en orden <c>strcmp</c>, y con clave repetida en el archivo gana la ULTIMA (json_reader.cpp:433
    ''' `currentValue()[name] = ...`); array/escalar/string ⇒ lanza (<paramref name="ok"/> = False).</summary>
    Public Function Miembros(el As JsonElement, ByRef ok As Boolean) As SortedDictionary(Of String, JsonElement)
        Dim r As New SortedDictionary(Of String, JsonElement)(ComparadorStrcmp.Instancia)
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : ok = True
            Case JsonValueKind.Object
                ok = True
                For Each prop In el.EnumerateObject()
                    r(prop.Name) = prop.Value
                Next
            Case Else : ok = False
        End Select
        Return r
    End Function

    ''' <summary>range-for sobre un <c>Json::Value</c> (:1284-1402): array ⇒ sus elementos; objeto ⇒ sus VALORES en
    ''' orden <c>strcmp</c> de clave; null/escalar/string ⇒ vacio (no lanza).</summary>
    Public Function Valores(el As JsonElement) As List(Of JsonElement)
        Dim r As New List(Of JsonElement)
        Select Case el.ValueKind
            Case JsonValueKind.Array
                For Each v In el.EnumerateArray() : r.Add(v) : Next
            Case JsonValueKind.Object
                Dim ok As Boolean
                For Each kv In Miembros(el, ok) : r.Add(kv.Value) : Next
        End Select
        Return r
    End Function

    ''' <summary><c>value[i].asFloat()</c> para i = 0..count-1 (CharGenInterface.cpp:596-611, :478-480): sobre
    ''' null/ausente el <c>[i]</c> lo vuelve array y crea nulls ⇒ ceros; array corto ⇒ 0 en lo que falta;
    ''' objeto/escalar/string ⇒ <c>[0]</c> lanza; elemento string/array/objeto ⇒ <c>asFloat</c> lanza.</summary>
    Public Function Floats(el As JsonElement, count As Integer, ByRef ok As Boolean) As Single()
        Dim result(count - 1) As Single
        If el.ValueKind <> JsonValueKind.Null AndAlso el.ValueKind <> JsonValueKind.Undefined AndAlso el.ValueKind <> JsonValueKind.Array Then
            ok = False : Return result
        End If
        Dim arr = If(el.ValueKind = JsonValueKind.Array, el.EnumerateArray().ToArray(), New JsonElement() {})
        ok = True
        For i = 0 To count - 1
            result(i) = AsFloat(If(i < arr.Length, arr(i), New JsonElement()), ok)
            If Not ok Then Exit For
        Next
        Return result
    End Function

    ' ===================================================================================================
    ' Adaptadores sobre JsonNode (System.Text.Json.Nodes) — la misma ley, para el arbol que usa RaceMenuJslot.
    ' Nothing = null jsoncpp (miembro ausente creado por operator[]). Objeto/array se clasifican por tipo sin
    ' serializar el subarbol; un JsonValue se baja a su JsonElement (los nodos parseados lo llevan adentro).
    ' ===================================================================================================

    Private Function Escalar(n As JsonNode) As JsonElement
        If n Is Nothing Then Return New JsonElement()
        Dim v = TryCast(n, JsonValue)
        If v Is Nothing Then Throw New InvalidOperationException("Escalar(): no es JsonValue")
        Dim el As JsonElement
        If v.TryGetValue(el) Then Return el
        Return JsonSerializer.SerializeToElement(v)
    End Function

    Private Function EsContenedor(n As JsonNode) As Boolean
        Return TypeOf n Is JsonObject OrElse TypeOf n Is JsonArray
    End Function

    Public Function AsFloat(n As JsonNode, ByRef ok As Boolean) As Single
        If EsContenedor(n) Then ok = False : Return 0.0F
        Return AsFloat(Escalar(n), ok)
    End Function

    Public Function AsInt(n As JsonNode, ByRef ok As Boolean) As Integer
        If EsContenedor(n) Then ok = False : Return 0
        Return AsInt(Escalar(n), ok)
    End Function

    Public Function AsUInt(n As JsonNode, ByRef ok As Boolean) As UInteger
        If EsContenedor(n) Then ok = False : Return 0UI
        Return AsUInt(Escalar(n), ok)
    End Function

    Public Function AsBool(n As JsonNode, ByRef ok As Boolean) As Boolean
        If EsContenedor(n) Then ok = False : Return False
        Return AsBool(Escalar(n), ok)
    End Function

    Public Function AsString(n As JsonNode, ByRef ok As Boolean) As String
        If EsContenedor(n) Then ok = False : Return ""
        Return AsString(Escalar(n), ok)
    End Function

    Public Function AsCString(n As JsonNode, ByRef ok As Boolean) As String
        If EsContenedor(n) Then ok = False : Return ""
        Return AsCString(Escalar(n), ok)
    End Function

    ''' <summary><c>empty()</c> (:862-867) sobre un nodo.</summary>
    Public Function Empty(n As JsonNode) As Boolean
        If n Is Nothing Then Return True
        Dim o = TryCast(n, JsonObject)
        If o IsNot Nothing Then Return o.Count = 0
        Dim a = TryCast(n, JsonArray)
        If a IsNot Nothing Then Return a.Count = 0
        Return Empty(Escalar(n))
    End Function

    ''' <summary><c>isMember(key)</c> (:1090-1093): usa el <c>operator[]</c> const (:1003-1015), que sobre un valor
    ''' que NO es objeto ni null lanza; null ⇒ false; objeto ⇒ si tiene la clave.</summary>
    Public Function IsMember(n As JsonNode, key As String, ByRef ok As Boolean) As Boolean
        ok = True
        If n Is Nothing Then Return False
        Dim o = TryCast(n, JsonObject)
        If o Is Nothing Then ok = False : Return False
        Return o.ContainsKey(key)
    End Function

    ''' <summary><c>operator[](const char*)</c> no-const (:970-994, resolveReference): null ⇒ se vuelve objeto y el
    ''' miembro nace null (⇒ Nothing); objeto ⇒ el miembro (o null si no esta); array/escalar/string ⇒ lanza.</summary>
    Public Function Miembro(n As JsonNode, key As String, ByRef ok As Boolean) As JsonNode
        ok = True
        If n Is Nothing Then Return Nothing
        Dim o = TryCast(n, JsonObject)
        If o Is Nothing Then ok = False : Return Nothing
        Dim r As JsonNode = Nothing
        o.TryGetPropertyValue(key, r)
        Return r
    End Function

    ''' <summary><c>operator[](ArrayIndex)</c> no-const (:918-936): null ⇒ se vuelve array y el elemento nace null
    ''' (⇒ Nothing); array ⇒ el elemento (o null fuera de rango); objeto/escalar/string ⇒ lanza.</summary>
    Public Function Elemento(n As JsonNode, index As Integer, ByRef ok As Boolean) As JsonNode
        ok = True
        If n Is Nothing Then Return Nothing
        Dim a = TryCast(n, JsonArray)
        If a Is Nothing Then ok = False : Return Nothing
        Return If(index < a.Count, a(index), Nothing)
    End Function

    ''' <summary>range-for sobre un nodo (:1284-1402): array ⇒ sus elementos; objeto ⇒ sus VALORES en orden
    ''' <c>strcmp</c> de clave; null/escalar/string ⇒ vacio (no lanza).</summary>
    Public Function Valores(n As JsonNode) As List(Of JsonNode)
        Dim r As New List(Of JsonNode)
        Dim a = TryCast(n, JsonArray)
        If a IsNot Nothing Then
            For Each v In a : r.Add(v) : Next
            Return r
        End If
        Dim o = TryCast(n, JsonObject)
        If o IsNot Nothing Then
            Dim ordenado As New SortedDictionary(Of String, JsonNode)(ComparadorStrcmp.Instancia)
            For Each kv In o : ordenado(kv.Key) = kv.Value : Next
            For Each kv In ordenado : r.Add(kv.Value) : Next
        End If
        Return r
    End Function

    ' ===================================================================================================
    ' sscanf_s "%X" — la lectura de FormID/clave hex de los dos motores.
    ' ===================================================================================================

    ''' <summary><c>sscanf_s(texto, "%X", &amp;v)</c> con v inicializado en 0 (f4ee CharGenInterface.cpp:412, :444,
    ''' :505, :543; Utilities.cpp:140): salta espacio en blanco C (' ', \t, \n, \v, \f, \r), acepta un signo, un
    ''' prefijo 0x/0X opcional y digitos hex hasta el primero que no lo es; sin digitos no asigna ⇒ 0; con '-' el
    ''' valor se niega en aritmetica sin signo. Desborde: el CRT acumula sin chequear (HUECO: no leimos la
    ''' implementacion de MSVC; se asume que envuelve modulo 2^32).</summary>
    Public Function ClaveSscanfX(texto As String) As UInteger
        If texto Is Nothing Then Return 0UI
        Dim i = 0
        While i < texto.Length AndAlso (texto(i) = " "c OrElse texto(i) = ChrW(9) OrElse texto(i) = ChrW(10) OrElse texto(i) = ChrW(11) OrElse texto(i) = ChrW(12) OrElse texto(i) = ChrW(13))
            i += 1
        End While
        Dim negativo = False
        If i < texto.Length AndAlso (texto(i) = "+"c OrElse texto(i) = "-"c) Then
            negativo = (texto(i) = "-"c)
            i += 1
        End If
        If i + 1 < texto.Length AndAlso texto(i) = "0"c AndAlso (texto(i + 1) = "x"c OrElse texto(i + 1) = "X"c) Then i += 2
        Dim v As UInteger = 0UI
        Dim digitos = 0
        While i < texto.Length
            Dim c = texto(i)
            Dim d As Integer
            If c >= "0"c AndAlso c <= "9"c Then
                d = AscW(c) - AscW("0"c)
            ElseIf c >= "a"c AndAlso c <= "f"c Then
                d = AscW(c) - AscW("a"c) + 10
            ElseIf c >= "A"c AndAlso c <= "F"c Then
                d = AscW(c) - AscW("A"c) + 10
            Else
                Exit While
            End If
            v = (v << 4) Or CUInt(d)
            digitos += 1
            i += 1
        End While
        If digitos = 0 Then Return 0UI
        If negativo Then v = CUInt((&H100000000UL - v) And &HFFFFFFFFUL)
        Return v
    End Function
End Module
