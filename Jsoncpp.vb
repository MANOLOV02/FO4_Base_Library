Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes

''' <summary>
''' jsoncpp, ONE transcription for every JSON the app reads the way a game engine reads it: the Json::Reader that turns the bytes into a
''' value tree (<see cref="Parsear"/>) and the value conversions (asBool / asInt / asUInt / asFloat / asString / asCString, get,
''' isMember, operator[], getMemberNames, the range-for) over that tree (<see cref="JsonValor"/>). Two copies of jsoncpp, one law with
''' their differences as a parameter (<see cref="Dialecto"/>):
''' <list type="bullet">
''' <item><b>Fallout4.exe 1.11.240</b> (<see cref="Dialecto.Fallout4Exe"/>): its amalgamated jsoncpp 1.6.x
''' (d:\radio_2\sdkgen9\jsoncpp\src\lib_json\jsoncpp.cpp, 0x142616950), compiled with JSON_USE_EXCEPTION 0 - every failed assertion is
''' _wassert + abort(): the game closes. The material files (LoadMaterialFile 0x1417A9420 -&gt; 0x14216EF30, MaterialFileReader).
''' D:\WinTmp\rc\re30\re.md 2 and 5.</item>
''' <item><b>f4ee / skee64</b> (<see cref="Dialecto.F4eeSkee64"/>): the copy F4SEPlugins/f4ee/jsoncpp (byte-identical to
''' SKSE64Plugins/jsoncpp), JSON_USE_EXCEPTION 1 (json/config.h:33): a failed assertion throws std::runtime_error
''' (json/assertions.h:19), which each loader catches its own way. LooksMenu presets and overlay templates, RaceMenu .jslot.</item>
''' </list>
''' A conversion that would fail returns <c>ok = False</c>; what that means (the game aborts / the loader's catch) is the caller's.
''' The loaders that edit and re-emit their document (RaceMenuJslot, LooksMenu) navigate a System.Text.Json DOM the tree materializes
''' (<see cref="DocumentoDe"/>, <see cref="NodoDe"/>): System.Text.Json decides nothing - it holds what this reader read, and every
''' value read from it goes back through the same law (<see cref="ValorDe(JsonElement, Dialecto)"/>).
''' </summary>
Public Module Jsoncpp

    ''' <summary>Which jsoncpp copy (the module's summary).</summary>
    Public Enum Dialecto
        ''' <summary>Fallout4.exe 1.11.240's jsoncpp 1.6.x: materials. Aborts.</summary>
        Fallout4Exe
        ''' <summary>f4ee / skee64's jsoncpp: LooksMenu and RaceMenu presets, overlay templates. Throws.</summary>
        F4eeSkee64
    End Enum

    ''' <summary>Json::ValueType, the byte the converters switch on ([value+8] in the exe: 0x1411CC6CB, 0x1411CCABB, 0x1411CCF27).</summary>
    Public Enum JsonTipo As Byte
        Nulo = 0
        Entero = 1
        SinSigno = 2
        Real = 3
        Texto = 4
        Booleano = 5
        Lista = 6
        Objeto = 7
    End Enum

    ''' <summary>A Json::Value. Absent (Nothing) is not null: an absent member is what get / isMember / find tell apart.</summary>
    Public NotInheritable Class JsonValor
        Public ReadOnly Tipo As JsonTipo
        Friend ReadOnly Entero As Long
        Friend ReadOnly SinSigno As ULong
        Friend ReadOnly Real As Double
        Friend ReadOnly Booleano As Boolean
        ''' <summary>A string: its bytes as decodeString left them (may hold a NUL).</summary>
        Friend ReadOnly Bytes As Byte()
        Friend ReadOnly Lista As List(Of JsonValor)
        ''' <summary>An object: the map (key bytes -&gt; value), its order the map's (<see cref="ComparadorDeClaves"/>).</summary>
        Friend ReadOnly Miembros As SortedDictionary(Of Byte(), JsonValor)
        ''' <summary>An object: its keys in the order they first appear in the file (only to materialize the DOM, which keeps the
        ''' file's order as the loaders always saw it; the engine iterates <see cref="Miembros"/>).</summary>
        Friend ReadOnly OrdenDelArchivo As List(Of Byte())
        ''' <summary>A number: the token as the file wrote it (to materialize the DOM verbatim).</summary>
        Friend ReadOnly Literal As String

        Private Sub New(t As JsonTipo, Optional e As Long = 0, Optional u As ULong = 0, Optional r As Double = 0, Optional b As Boolean = False,
                        Optional bytes As Byte() = Nothing, Optional lit As String = Nothing)
            Tipo = t : Entero = e : SinSigno = u : Real = r : Booleano = b : Me.Bytes = bytes : Literal = lit
            If t = JsonTipo.Lista Then Lista = New List(Of JsonValor)
            If t = JsonTipo.Objeto Then
                Miembros = New SortedDictionary(Of Byte(), JsonValor)(ComparadorDeClaves.Instancia)
                OrdenDelArchivo = New List(Of Byte())
            End If
        End Sub

        Public Shared ReadOnly Nulo As New JsonValor(JsonTipo.Nulo)
        Friend Shared Function DeEntero(v As Long, Optional lit As String = Nothing) As JsonValor
            Return New JsonValor(JsonTipo.Entero, e:=v, lit:=lit)
        End Function
        Friend Shared Function DeSinSigno(v As ULong, Optional lit As String = Nothing) As JsonValor
            Return New JsonValor(JsonTipo.SinSigno, u:=v, lit:=lit)
        End Function
        Friend Shared Function DeReal(v As Double, Optional lit As String = Nothing) As JsonValor
            Return New JsonValor(JsonTipo.Real, r:=v, lit:=lit)
        End Function
        Friend Shared Function DeBooleano(v As Boolean) As JsonValor
            Return New JsonValor(JsonTipo.Booleano, b:=v)
        End Function
        Friend Shared Function DeBytes(v As Byte()) As JsonValor
            Return New JsonValor(JsonTipo.Texto, bytes:=v)
        End Function
        ''' <summary>A string value from text (a default of get: "None", "#ffffff"...).</summary>
        Public Shared Function DeTexto(v As String) As JsonValor
            Return New JsonValor(JsonTipo.Texto, bytes:=Encoding.UTF8.GetBytes(v))
        End Function
        Public Shared Function DeDouble(v As Double) As JsonValor
            Return New JsonValor(JsonTipo.Real, r:=v)
        End Function
        Public Shared Function DeInt(v As Integer) As JsonValor
            Return New JsonValor(JsonTipo.Entero, e:=v)
        End Function
        Public Shared Function DeBool(v As Boolean) As JsonValor
            Return New JsonValor(JsonTipo.Booleano, b:=v)
        End Function
        Friend Shared Function NuevaLista() As JsonValor
            Return New JsonValor(JsonTipo.Lista)
        End Function
        Friend Shared Function NuevoObjeto() As JsonValor
            Return New JsonValor(JsonTipo.Objeto)
        End Function

        ''' <summary><c>currentValue()[name] = value</c> while reading (resolveReference, find-or-insert: a repeated key keeps its
        ''' LAST value; exe 0x1411D7C03 -&gt; 0x1411DA990, f4ee json_reader.cpp:432 -&gt; json_value.cpp:974-994).</summary>
        Friend Sub PonerMiembro(clave As Byte(), v As JsonValor)
            If Not Miembros.ContainsKey(clave) Then OrdenDelArchivo.Add(clave)
            Miembros(clave) = v
        End Sub

        ''' <summary>The jsoncpp type's name, for a notice ("a string", "an array"...).</summary>
        Public ReadOnly Property NombreDelTipo As String
            Get
                Return {"null", "an int", "a uint", "a real", "a string", "a bool", "an array", "an object"}(CInt(Tipo))
            End Get
        End Property
    End Class

    ''' <summary>The object map's key order, both copies: unsigned bytes, a prefix before the longer key (exe CZString operator&lt;:
    ''' memcmp of the shorter length, then the lengths - the comparison find 0x1411D0C8A..CF4 walks the tree with; f4ee strcmp
    ''' json_value.cpp:200-204 on keys that never hold a NUL, <see cref="Lector"/> cuts them there).</summary>
    Friend NotInheritable Class ComparadorDeClaves
        Implements IComparer(Of Byte())
        Public Shared ReadOnly Instancia As New ComparadorDeClaves()
        Public Function Compare(x As Byte(), y As Byte()) As Integer Implements IComparer(Of Byte()).Compare
            Dim n = Math.Min(x.Length, y.Length)
            For i = 0 To n - 1
                If x(i) <> y(i) Then Return CInt(x(i)) - CInt(y(i))
            Next
            Return x.Length - y.Length
        End Function
    End Class

    ' ===================================================================================================
    ' THE READER: Json::Reader::parse over bytes
    ' ===================================================================================================

    ''' <summary>What a parse ended with.</summary>
    Public Enum ResultadoDelParseo
        ''' <summary>parse returned true: the root is the first value; what follows it is not looked at.</summary>
        Leido
        ''' <summary>parse returned false (an error of the grammar): the caller applies nothing.</summary>
        Rechazado
        ''' <summary>Fallout4.exe only: nesting at stackDepth 1000 (readValue 0x1411D9169 -&gt; throwRuntimeError 0x1411DBB70, which is
        ''' _wassert + abort() with JSON_USE_EXCEPTION 0, 0x1411DBC4D / 0x1411DBC53): the game closes.</summary>
        Aborta
    End Enum

    ''' <summary>Json::Reader::parse(begin, end, root, false) over <paramref name="bytes"/>[<paramref name="inicio"/>,
    ''' <paramref name="fin"/>) with the Features the two engines build (Reader() = Features::all(): allowComments on, strictRoot,
    ''' allowDroppedNullPlaceholders and allowNumericKeys off; exe ctor 0x1411C7E05 [+0xB8] = 1, f4ee json_reader.cpp:28-32 / 76-79):
    ''' readValue, then the comment tokens after it, and nothing else (exe 0x1411D53B2..53E6, f4ee :115-141): what follows the first
    ''' value is ignored and no root type is checked. <paramref name="mensaje"/> = the first error's text (the engine keeps it, nobody
    ''' reads it).</summary>
    Public Function Parsear(bytes As Byte(), inicio As Integer, fin As Integer, d As Dialecto, ByRef raiz As JsonValor, Optional ByRef mensaje As String = Nothing) As ResultadoDelParseo
        Dim l As New Lector(bytes, inicio, fin, d)
        Dim r = l.Leer(raiz)
        mensaje = l.Mensaje
        Return r
    End Function

    ''' <summary>The tokenizer and the grammar, iterative (an explicit stack instead of readValue's recursion: same order of reads and
    ''' checks; f4ee's copy has no depth limit - json_reader.cpp:143-212 - and an iterative reader keeps the app from a stack overflow
    ''' the plug-in would hit with its own native stack, a crash not transcribed).</summary>
    Private NotInheritable Class Lector
        ' Token types (exe readToken 0x1411D86B0 writes the same numbers: 1 '{' .. 13 error; f4ee reader.h enum TokenType).
        Private Const TEos = 0, TObjIni = 1, TObjFin = 2, TArrIni = 3, TArrFin = 4, TTexto = 5, TNumero = 6, TTrue = 7, TFalse = 8,
                      TNull = 9, TComa = 10, TDosPuntos = 11, TComentario = 12, TError = 13

        Private Structure Token
            Dim Tipo As Integer
            Dim Ini As Integer
            Dim Fin As Integer
        End Structure

        Private ReadOnly b As Byte()
        Private ReadOnly finTexto As Integer
        Private ReadOnly _d As Dialecto
        Private cur As Integer
        Friend Mensaje As String

        Friend Sub New(bytes As Byte(), inicio As Integer, fin As Integer, d As Dialecto)
            b = bytes : cur = inicio : finTexto = fin : _d = d
        End Sub

        ''' <summary>A byte at <paramref name="i"/>; reads at or past the end see the byte that follows in the buffer (both readers peek
        ''' there: readArray's empty test, readCStyleComment's '/'), or 0 past the array.</summary>
        Private Function Byte_(i As Integer) As Integer
            Return If(i < b.Length, CInt(b(i)), 0)
        End Function

        ''' <summary>skipSpaces: ' ', '\t', '\r', '\n' (exe 0x1411D86C0..8705, mask 0x100002200; f4ee :241-249).</summary>
        Private Sub SaltarEspacios()
            While cur <> finTexto
                Dim c = b(cur)
                If c = 32 OrElse c = 9 OrElse c = 13 OrElse c = 10 Then cur += 1 Else Exit While
            End While
        End Sub

        ''' <summary>getNextChar: 0 at the end, else the byte (exe 0x1411D870B..8722, f4ee :797-801).</summary>
        Private Function Siguiente() As Integer
            If cur = finTexto Then Return 0
            Dim c = CInt(b(cur))
            cur += 1
            Return c
        End Function

        ''' <summary>readToken (exe 0x1411D86B0, its jump table 0x1411D8AAC / 0x1411D8A74; f4ee :180-239). A NUL byte and the end
        ''' are end-of-stream; any byte above 0x7D not listed - the bytes &gt;= 0x80 included (movsx) - is an error token.</summary>
        Private Function LeerToken() As Token
            SaltarEspacios()
            Dim t As New Token With {.Ini = cur}
            Dim c = Siguiente()
            Dim ok = True
            Select Case c
                Case &H7B : t.Tipo = TObjIni
                Case &H7D : t.Tipo = TObjFin
                Case &H5B : t.Tipo = TArrIni
                Case &H5D : t.Tipo = TArrFin
                Case &H22 : t.Tipo = TTexto : ok = LeerTexto()
                Case &H2F
                    t.Tipo = TComentario : ok = LeerComentario()
#If DEBUG Then
                    ' Ola JSON tester: comments are an error (System.Text.Json's default, the app's read before this wave).
                    If Fo4GBufferConstants.GateMutantOn("oj-lector-sin-comentarios") Then ok = False
#End If
                Case &H2D, &H30 To &H39 : t.Tipo = TNumero : LeerNumero()
                Case &H74 : t.Tipo = TTrue : ok = Calza("rue")
                Case &H66 : t.Tipo = TFalse : ok = Calza("alse")
                Case &H6E : t.Tipo = TNull : ok = Calza("ull")
                Case &H2C : t.Tipo = TComa
                Case &H3A : t.Tipo = TDosPuntos
                Case 0 : t.Tipo = TEos
                Case Else : ok = False
            End Select
            If Not ok Then t.Tipo = TError
            t.Fin = cur
            Return t
        End Function

        ''' <summary>match: the rest of a literal, no check of what follows (exe 0x1411D8928..8A4B, f4ee :251-260).</summary>
        Private Function Calza(resto As String) As Boolean
            If finTexto - cur < resto.Length Then Return False
            For i = 0 To resto.Length - 1
                If b(cur + i) <> AscW(resto(i)) Then Return False
            Next
            cur += resto.Length
            Return True
        End Function

        ''' <summary>readString: any byte up to the unescaped '"' - no UTF-8 or control check (exe 0x1411D8775..87BD, f4ee :311-321).</summary>
        Private Function LeerTexto() As Boolean
            Dim c = 0
            While cur <> finTexto
                c = Siguiente()
                If c = &H5C Then
                    Siguiente()
                ElseIf c = &H22 Then
                    Exit While
                End If
            End While
            Return c = &H22
        End Function

        ''' <summary>readComment: "/* ... */" or "// ..." to the line's end; '/' with anything else is an error (exe 0x1411D6930,
        ''' f4ee :262-307). The exe takes a "\r\n" pair whole, f4ee stops at the '\r' and skipSpaces takes the '\n': the same
        ''' tokens.</summary>
        Private Function LeerComentario() As Boolean
            Dim c = Siguiente()
            If c = &H2A Then
                While cur <> finTexto
                    Dim k = Siguiente()
                    If k = &H2A AndAlso Byte_(cur) = &H2F Then Exit While
                End While
                Return Siguiente() = &H2F
            ElseIf c = &H2F Then
                While cur <> finTexto
                    Dim k = Siguiente()
                    If k = &HD OrElse k = &HA Then Exit While
                End While
                Return True
            End If
            Return False
        End Function

        ''' <summary>readNumber. Exe (0x1411D87F2..8923): digits, then '.' and digits, then 'e'/'E', an optional sign and digits -
        ''' each part may stop short ("1.", "1e", "-"). f4ee (:303-309): every byte in [0-9.eE+-], greedily ("1-2", "1.2.3").</summary>
        Private Sub LeerNumero()
            Dim codicioso = _d = Dialecto.F4eeSkee64
#If DEBUG Then
            ' Ola JSON tester: Fallout4.exe's number read with f4ee's greedy one (the two dialects mixed up).
            If Fo4GBufferConstants.GateMutantOn("oj-lector-numero-f4ee") Then codicioso = True
#End If
            If codicioso Then
                While cur <> finTexto
                    Dim c = b(cur)
                    If Not ((c >= &H30 AndAlso c <= &H39) OrElse c = &H2E OrElse c = &H65 OrElse c = &H45 OrElse c = &H2B OrElse c = &H2D) Then Exit While
                    cur += 1
                End While
                Return
            End If
            Dim p = cur
            Dim d = &H30
            While d >= &H30 AndAlso d <= &H39
                cur = p
                d = If(p < finTexto, CInt(b(p)), 0)
                If p < finTexto Then p += 1
            End While
            If d = &H2E Then
                cur = p
                d = If(p < finTexto, CInt(b(p)), 0)
                If p < finTexto Then p += 1
                While d >= &H30 AndAlso d <= &H39
                    cur = p
                    d = If(p < finTexto, CInt(b(p)), 0)
                    If p < finTexto Then p += 1
                End While
            End If
            If d = &H45 OrElse d = &H65 Then
                cur = p
                d = If(p < finTexto, CInt(b(p)), 0)
                Dim q = If(p < finTexto, p + 1, p)
                If d = &H2B OrElse d = &H2D Then
                    cur = q
                    If q >= finTexto Then Return
                    d = b(q) : q += 1
                End If
                While d >= &H30 AndAlso d <= &H39
                    cur = q
                    d = If(q < finTexto, CInt(b(q)), 0)
                    If q < finTexto Then q += 1
                End While
            End If
        End Sub

        ''' <summary>decodeString: the escapes \" \/ \\ \b \f \n \r \t \uXXXX (with a surrogate pair), any other escape an error; other
        ''' bytes copied (exe 0x1411CF820, table 0x1411CFCBC; f4ee :674-725). f4ee keeps the bytes; its asString / asCString and its
        ''' keys stop at a NUL (C strings).</summary>
        Private Function DecodificarTexto(t As Token, ByRef salida As Byte()) As Boolean
            Dim o As New List(Of Byte)(Math.Max(0, t.Fin - t.Ini - 2))
            Dim i = t.Ini + 1
            Dim e = t.Fin - 1
            While i <> e
                Dim c = b(i) : i += 1
                If c = &H22 Then Exit While
                If c <> &H5C Then o.Add(c) : Continue While
                If i = e Then Return Falla("Empty escape sequence in string")
                Dim esc = b(i) : i += 1
                Select Case esc
                    Case &H22 : o.Add(&H22)
                    Case &H2F : o.Add(&H2F)
                    Case &H5C : o.Add(&H5C)
                    Case &H62 : o.Add(8)
                    Case &H66 : o.Add(12)
                    Case &H6E : o.Add(10)
                    Case &H72 : o.Add(13)
                    Case &H74 : o.Add(9)
                    Case &H75
                        Dim cp As UInteger
                        If Not CuatroHex(i, e, cp) Then Return False
                        If cp >= &HD800UI AndAlso cp <= &HDBFFUI Then
                            If e - i < 6 Then Return Falla("additional six characters expected to parse unicode surrogate pair.")
                            Dim a = b(i) : i += 1
                            Dim bb = b(i) : i += 1
                            If a <> &H5C OrElse bb <> &H75 Then Return Falla("expecting another \u token to begin the second half of a unicode surrogate pair")
                            Dim bajo As UInteger
                            If Not CuatroHex(i, e, bajo) Then Return False
                            cp = &H10000UI + ((cp And &H3FFUI) << 10) + (bajo And &H3FFUI)
                        End If
                        o.AddRange(CodePointToUtf8(cp))
                    Case Else
                        Return Falla("Bad escape sequence in string")
                End Select
            End While
            salida = o.ToArray()
            Return True
        End Function

        ''' <summary>decodeUnicodeEscapeSequence (f4ee :754-781; exe the same path inside 0x1411CF820).</summary>
        Private Function CuatroHex(ByRef i As Integer, e As Integer, ByRef u As UInteger) As Boolean
            If e - i < 4 Then Return Falla("Bad unicode escape sequence in string: four digits expected.")
            u = 0UI
            For k = 0 To 3
                Dim c = b(i) : i += 1
                u *= 16UI
                If c >= &H30 AndAlso c <= &H39 Then
                    u += CUInt(c - &H30)
                ElseIf c >= &H61 AndAlso c <= &H66 Then
                    u += CUInt(c - &H61 + 10)
                ElseIf c >= &H41 AndAlso c <= &H46 Then
                    u += CUInt(c - &H41 + 10)
                Else
                    Return Falla("Bad unicode escape sequence in string: hexadecimal digit expected.")
                End If
            Next
            Return True
        End Function

        ''' <summary>codePointToUTF8 (f4ee json_tool.h:18-44): above 0x10FFFF, nothing.</summary>
        Private Shared Function CodePointToUtf8(cp As UInteger) As Byte()
            If cp <= &H7FUI Then Return {CByte(cp)}
            If cp <= &H7FFUI Then Return {CByte(&HC0UI Or (cp >> 6)), CByte(&H80UI Or (cp And &H3FUI))}
            If cp <= &HFFFFUI Then Return {CByte(&HE0UI Or (cp >> 12)), CByte(&H80UI Or ((cp >> 6) And &H3FUI)), CByte(&H80UI Or (cp And &H3FUI))}
            If cp <= &H10FFFFUI Then Return {CByte(&HF0UI Or (cp >> 18)), CByte(&H80UI Or ((cp >> 12) And &H3FUI)), CByte(&H80UI Or ((cp >> 6) And &H3FUI)), CByte(&H80UI Or (cp And &H3FUI))}
            Return Array.Empty(Of Byte)()
        End Function

        ''' <summary>decodeNumber. Exe (0x1411CEF00): an optional '-' and digits accumulated in 64 bits (the thresholds
        ''' 0x1999999999999999 / 0x0CCCCCCCCCCCCCCC and the last digit, 0x1411CEF26..CEFAF); any other byte, or an overflow, goes to
        ''' decodeDouble; negative -&gt; int, up to 0x7FFFFFFF -&gt; int, above -&gt; uint (0x1411CEFD5..CEFE9); "-" alone is int 0.
        ''' f4ee (:497-545): any '.', 'e', 'E', '+' or a '-' past the first byte sends the token to decodeDouble first; then the same
        ''' loop, a non-digit there an error ("is not a number"), the same classification.</summary>
        Private Function DecodificarNumero(t As Token, ByRef v As JsonValor) As Boolean
            Dim lit = Encoding.ASCII.GetString(b, t.Ini, t.Fin - t.Ini)
            If _d = Dialecto.F4eeSkee64 Then
                For i = t.Ini To t.Fin - 1
                    Dim c = b(i)
                    If c = &H2E OrElse c = &H65 OrElse c = &H45 OrElse c = &H2B OrElse (c = &H2D AndAlso i <> t.Ini) Then Return DecodificarDouble(t, lit, v)
                Next
            End If
            Dim p = t.Ini
            Dim negativo = b(p) = &H2D
            If negativo Then p += 1
            Dim maximo As ULong = If(negativo, &H8000000000000000UL, ULong.MaxValue)
            Dim umbral = maximo \ 10UL
            Dim valor As ULong = 0UL
            While p < t.Fin
                Dim c = b(p) : p += 1
                If c < &H30 OrElse c > &H39 Then
                    If _d = Dialecto.F4eeSkee64 Then Return Falla($"'{lit}' is not a number.")
                    Return DecodificarDouble(t, lit, v)
                End If
                Dim digito = CULng(c - &H30)
                If valor >= umbral Then
                    If valor > umbral OrElse p <> t.Fin OrElse digito > maximo Mod 10UL Then Return DecodificarDouble(t, lit, v)
                End If
                valor = valor * 10UL + digito
            End While
            If negativo Then
                v = JsonValor.DeEntero(If(valor = &H8000000000000000UL, Long.MinValue, -CLng(valor)), lit)
            ElseIf valor <= &H7FFFFFFFUL Then
                v = JsonValor.DeEntero(CLng(valor), lit)
            Else
                v = JsonValor.DeSinSigno(valor, lit)
            End If
            Return True
        End Function

        ''' <summary>decodeDouble: sscanf(token, "%lf") must return 1 (exe 0x1411CE850 -&gt; 0x1403D2050 -&gt; ucrtbase
        ''' __stdio_common_vsscanf; f4ee :556-595, the same sscanf); else "'...' is not a number.". The value is <see cref="SscanfLf"/>.</summary>
        Private Function DecodificarDouble(t As Token, lit As String, ByRef v As JsonValor) As Boolean
            Dim d As Double
            If Not SscanfLf(lit, d) Then Return Falla($"'{lit}' is not a number.")
            v = JsonValor.DeReal(d, lit)
            Return True
        End Function

        Private Function Falla(m As String) As Boolean
            If Mensaje Is Nothing Then Mensaje = m
            Return False
        End Function

        ''' <summary>A value being read: an open object or array.</summary>
        Private NotInheritable Class Marco
            Friend Contenedor As JsonValor
            Friend Nombre As Byte() = Array.Empty(Of Byte)()
        End Class

        ''' <summary>parse: readValue for the root, then the comment tokens after it (exe 0x1411D53AF..53E6, f4ee :115-141).</summary>
        Friend Function Leer(ByRef raiz As JsonValor) As ResultadoDelParseo
            Dim pila As New List(Of Marco)
            Dim valor As JsonValor = Nothing
            Dim estado = 0          ' 0: a value starts (readValue); 1: a value is complete; 2: an object waits for a name
            Dim tok As Token
            Do
                Select Case estado
                    Case 0
                        ' readValue (exe 0x1411D9130; f4ee :143-212): the exe's depth check first (stackDepth_g >= 1000,
                        ' 0x1411D9163..9174), then the comment tokens, then the value's token.
                        Dim conLimite = _d = Dialecto.Fallout4Exe
#If DEBUG Then
                        ' Ola JSON tester: no stackLimit (f4ee's copy, or System.Text.Json's 64 off).
                        If Fo4GBufferConstants.GateMutantOn("oj-lector-sin-limite") Then conLimite = False
#End If
                        If conLimite AndAlso pila.Count >= 1000 Then
                            Mensaje = "Exceeded stackLimit in readValue()."
                            Return ResultadoDelParseo.Aborta
                        End If
                        Do
                            tok = LeerToken()
                        Loop While tok.Tipo = TComentario
                        Select Case tok.Tipo
                            Case TObjIni
                                pila.Add(New Marco With {.Contenedor = JsonValor.NuevoObjeto()})
                                estado = 2
                            Case TArrIni
                                ' readArray (exe 0x1411D6320, f4ee :455-488): skipSpaces and a peek at ']' for the empty array.
                                Dim m As New Marco With {.Contenedor = JsonValor.NuevaLista()}
                                pila.Add(m)
                                SaltarEspacios()
                                If Byte_(cur) = &H5D Then
                                    LeerToken()
                                    pila.RemoveAt(pila.Count - 1)
                                    valor = m.Contenedor
                                    estado = 1
                                Else
                                    estado = 0
                                End If
                            Case TNumero
                                If Not DecodificarNumero(tok, valor) Then Return ResultadoDelParseo.Rechazado
                                estado = 1
                            Case TTexto
                                Dim s As Byte() = Nothing
                                If Not DecodificarTexto(tok, s) Then Return ResultadoDelParseo.Rechazado
                                valor = JsonValor.DeBytes(s)
                                estado = 1
                            Case TTrue : valor = JsonValor.DeBooleano(True) : estado = 1
                            Case TFalse : valor = JsonValor.DeBooleano(False) : estado = 1
                            Case TNull : valor = JsonValor.Nulo : estado = 1
                            Case Else
#If DEBUG Then
                                ' Ola JSON tester (revisor oj-01): "[1,]" taken.
                                If tok.Tipo = TArrFin AndAlso pila.Count > 0 AndAlso pila(pila.Count - 1).Contenedor.Tipo = JsonTipo.Lista AndAlso
                                   Fo4GBufferConstants.GateMutantOn("oj-lector-coma-final") Then
                                    valor = pila(pila.Count - 1).Contenedor
                                    pila.RemoveAt(pila.Count - 1)
                                    estado = 1
                                    Continue Do
                                End If
#End If
                                ' '}' ']' ',' with allowDroppedNullPlaceholders off, ':' , the end, an error token.
                                Mensaje = If(Mensaje, "Syntax error: value, object or array expected.")
                                Return ResultadoDelParseo.Rechazado
                        End Select
                    Case 1
                        If pila.Count = 0 Then
                            ' parse: the comment tokens after the root are read; nothing else is checked.
                            Do
                                tok = LeerToken()
                            Loop While tok.Tipo = TComentario
#If DEBUG Then
                            ' Ola JSON tester: what follows the first value rejects the document (System.Text.Json's single value).
                            If tok.Tipo <> TEos AndAlso Fo4GBufferConstants.GateMutantOn("oj-lector-resto-rechaza") Then Return ResultadoDelParseo.Rechazado
#End If
                            raiz = valor
                            Return ResultadoDelParseo.Leido
                        End If
                        Dim m = pila(pila.Count - 1)
                        If m.Contenedor.Tipo = JsonTipo.Objeto Then
                            m.Contenedor.PonerMiembro(m.Nombre, valor)
                            ' readObject's comma (exe 0x1411D7CB2..7CF3, f4ee :437-450): '}' ',' or a comment, then the comments;
                            ' a '}' closes, anything else goes back to read a name.
                            tok = LeerToken()
                            If tok.Tipo <> TObjFin AndAlso tok.Tipo <> TComa AndAlso tok.Tipo <> TComentario Then
                                Mensaje = If(Mensaje, "Missing ',' or '}' in object declaration")
                                Return ResultadoDelParseo.Rechazado
                            End If
                            While tok.Tipo = TComentario
                                tok = LeerToken()
                            End While
                            If tok.Tipo = TObjFin Then
                                pila.RemoveAt(pila.Count - 1)
                                valor = m.Contenedor
                                estado = 1
                            Else
                                estado = 2
                            End If
                        Else
                            m.Contenedor.Lista.Add(valor)
                            ' readArray's separator (exe 0x1411D64A4..64D0, f4ee :472-485): the comments, then ',' or ']'.
                            tok = LeerToken()
                            While tok.Tipo = TComentario
                                tok = LeerToken()
                            End While
                            If tok.Tipo <> TComa AndAlso tok.Tipo <> TArrFin Then
                                Mensaje = If(Mensaje, "Missing ',' or ']' in array declaration")
                                Return ResultadoDelParseo.Rechazado
                            End If
                            If tok.Tipo = TArrFin Then
                                pila.RemoveAt(pila.Count - 1)
                                valor = m.Contenedor
                                estado = 1
                            Else
                                estado = 0
                            End If
                        End If
                    Case 2
                        ' readObject (exe 0x1411D7920, f4ee :402-453): the name token after the comments; '}' closes only while
                        ' the PREVIOUS member's name is empty ("{}", and "{"":1,}" too); the name must be a string (numeric keys
                        ' off); then ':'.
                        Dim m = pila(pila.Count - 1)
                        Do
                            tok = LeerToken()
                        Loop While tok.Tipo = TComentario
                        Dim comaFinal = False
#If DEBUG Then
                        ' Ola JSON tester (revisor oj-01): a trailing comma taken (System.Text.Json's AllowTrailingCommas).
                        comaFinal = Fo4GBufferConstants.GateMutantOn("oj-lector-coma-final")
#End If
                        If tok.Tipo = TObjFin AndAlso (m.Nombre.Length = 0 OrElse comaFinal) Then
                            pila.RemoveAt(pila.Count - 1)
                            valor = m.Contenedor
                            estado = 1
                            Continue Do
                        End If
                        m.Nombre = Array.Empty(Of Byte)()
                        If tok.Tipo <> TTexto Then
                            Mensaje = If(Mensaje, "Missing '}' or object member name")
                            Return ResultadoDelParseo.Rechazado
                        End If
                        Dim nombre As Byte() = Nothing
                        If Not DecodificarTexto(tok, nombre) Then Return ResultadoDelParseo.Rechazado
                        ' f4ee: currentValue()[name] takes name.c_str() (json_reader.cpp:432 -&gt; json_value.cpp:970-974): the key
                        ' stops at a NUL. The exe passes the length (0x1411D7BF9..7C03).
                        If _d = Dialecto.F4eeSkee64 Then nombre = CortarEnNul(nombre)
                        m.Nombre = nombre
                        tok = LeerToken()
                        If tok.Tipo <> TDosPuntos Then
                            Mensaje = If(Mensaje, "Missing ':' after object member name")
                            Return ResultadoDelParseo.Rechazado
                        End If
                        estado = 0
                End Select
            Loop
        End Function
    End Class

    ''' <summary>sscanf(texto, "%lf") == 1 and its value, as ucrtbase does it (measured, D:\WinTmp\rc\olajson\med\sscanf_prefijo.py over
    ''' __stdio_common_vsscanf, the call of Fallout4.exe's 0x1403D2050): an optional sign, digits with an optional '.' and digits - at
    ''' least one digit -, and when an 'e'/'E' follows them an optional sign and at least ONE digit, else the conversion fails
    ''' ("1e", "2E+", "1.e" fail; "1.", ".5", "1-2", "1.2.3" read 1, 0.5, 1, 1.2: what follows the float is not looked at). The
    ''' value: .NET's correctly rounded parse of that prefix (ucrtbase's strtod measured equal to correct rounding on 300000 tokens,
    ''' D:\WinTmp\rc\re30\re.md 2.3; out of range: +-inf / 0, as both do).</summary>
    Friend Function SscanfLf(texto As String, ByRef valor As Double) As Boolean
        Dim i = 0
        If i < texto.Length AndAlso (texto(i) = "+"c OrElse texto(i) = "-"c) Then i += 1
        Dim digitos = 0
        While i < texto.Length AndAlso Char.IsAsciiDigit(texto(i)) : i += 1 : digitos += 1 : End While
        If i < texto.Length AndAlso texto(i) = "."c Then
            i += 1
            While i < texto.Length AndAlso Char.IsAsciiDigit(texto(i)) : i += 1 : digitos += 1 : End While
        End If
        If digitos = 0 Then Return False
        If i < texto.Length AndAlso (texto(i) = "e"c OrElse texto(i) = "E"c) Then
            i += 1
            If i < texto.Length AndAlso (texto(i) = "+"c OrElse texto(i) = "-"c) Then i += 1
            Dim exp = 0
            While i < texto.Length AndAlso Char.IsAsciiDigit(texto(i)) : i += 1 : exp += 1 : End While
            If exp = 0 Then Return False
        End If
        valor = Double.Parse(texto.Substring(0, i), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture)
        Return True
    End Function

    ''' <summary>The bytes up to the first NUL (a C string).</summary>
    Friend Function CortarEnNul(b As Byte()) As Byte()
        Dim k = Array.IndexOf(b, CByte(0))
        If k < 0 Then Return b
        Dim r(k - 1) As Byte
        Array.Copy(b, r, k)
        Return r
    End Function

    ''' <summary>A string's bytes as .NET text (UTF-8; the presets' bytes were already made UTF-8 by <see cref="BytesComoLosVeJsoncpp"/>).</summary>
    Public Function TextoDe(b As Byte()) As String
        If b Is Nothing Then Return ""
        Return Encoding.UTF8.GetString(b)
    End Function

    ' ===================================================================================================
    ' THE LAW: Json::Value's accessors and conversions over JsonValor
    ' ===================================================================================================

    ''' <summary>find(key) (exe 0x1411D0B30; f4ee operator[] const json_value.cpp:1003-1015): null -&gt; absent; object -&gt; the member
    ''' or absent; anything else fails (exe _wassert line 3409 "requires objectValue or nullValue"; f4ee throws).</summary>
    Public Function Buscar(v As JsonValor, clave As String, ByRef ok As Boolean) As JsonValor
        ok = True
        If v Is Nothing OrElse v.Tipo = JsonTipo.Nulo Then Return Nothing
        If v.Tipo <> JsonTipo.Objeto Then ok = False : Return Nothing
        Dim r As JsonValor = Nothing
        v.Miembros.TryGetValue(Encoding.UTF8.GetBytes(clave), r)
        Return r
    End Function

    ''' <summary>get(key, default) (exe 0x1411D0E10: find, absent -&gt; the default).</summary>
    Public Function Obtener(v As JsonValor, clave As String, defecto As JsonValor, ByRef ok As Boolean) As JsonValor
        Dim r = Buscar(v, clave, ok)
        Return If(r, defecto)
    End Function

    ''' <summary>isMember(key) (exe 0x1411D3830 = find != NULL; f4ee :1090-1093 over operator[] const): null -&gt; false; object -&gt;
    ''' whether the key is there (a member whose value is null COUNTS); anything else fails.</summary>
    Public Function EsMiembro(v As JsonValor, clave As String, ByRef ok As Boolean) As Boolean
        Return Buscar(v, clave, ok) IsNot Nothing
    End Function

    ''' <summary>operator[](const char*) non-const (f4ee :970-994, resolveReference; exe 0x1411C9F40 on a member isMember already
    ''' found): null -&gt; becomes an object whose member is born null (absent here, which reads as null); object -&gt; the member or
    ''' null; anything else fails.</summary>
    Public Function Miembro(v As JsonValor, clave As String, ByRef ok As Boolean) As JsonValor
        Return Buscar(v, clave, ok)
    End Function

    ''' <summary>operator[](ArrayIndex) non-const (f4ee :918-936): null -&gt; becomes an array, the element born null; array -&gt; the
    ''' element or null past its end; anything else fails.</summary>
    Public Function Elemento(v As JsonValor, index As Integer, ByRef ok As Boolean) As JsonValor
        ok = True
        If v Is Nothing OrElse v.Tipo = JsonTipo.Nulo Then Return Nothing
        If v.Tipo <> JsonTipo.Lista Then ok = False : Return Nothing
        Return If(index < v.Lista.Count, v.Lista(index), Nothing)
    End Function

    ''' <summary>getMemberNames + the member by key (f4ee :1105-1127): null/absent -&gt; empty; object -&gt; its members in the map's
    ''' order; array/scalar/string -&gt; fails. The key as .NET text and as its bytes.</summary>
    Public Function Miembros(v As JsonValor, ByRef ok As Boolean) As List(Of (Clave As String, Bytes As Byte(), Valor As JsonValor))
        ok = True
        Dim r As New List(Of (Clave As String, Bytes As Byte(), Valor As JsonValor))
        If v Is Nothing OrElse v.Tipo = JsonTipo.Nulo Then Return r
        If v.Tipo <> JsonTipo.Objeto Then ok = False : Return r
        For Each kv In v.Miembros
            r.Add((TextoDe(kv.Key), kv.Key, kv.Value))
        Next
        Return r
    End Function

    ''' <summary>The range-for over a Json::Value (f4ee :1284-1402): array -&gt; its elements; object -&gt; its VALUES in the map's order;
    ''' null/scalar/string -&gt; nothing (does not fail).</summary>
    Public Function Valores(v As JsonValor) As List(Of JsonValor)
        Dim r As New List(Of JsonValor)
        If v Is Nothing Then Return r
        If v.Tipo = JsonTipo.Lista Then r.AddRange(v.Lista)
        If v.Tipo = JsonTipo.Objeto Then r.AddRange(v.Miembros.Values)
        Return r
    End Function

    ''' <summary>empty() (f4ee :862-867): null/array/object -&gt; size() == 0; a scalar or a string -&gt; false.</summary>
    Public Function Vacio(v As JsonValor) As Boolean
        If v Is Nothing OrElse v.Tipo = JsonTipo.Nulo Then Return True
        If v.Tipo = JsonTipo.Lista Then Return v.Lista.Count = 0
        If v.Tipo = JsonTipo.Objeto Then Return v.Miembros.Count = 0
        Return False
    End Function

    ''' <summary>asBool (exe 0x1411CC6B0; f4ee :780-795): null false; int / uint not 0; real not 0.0 (NaN true: exe jp 0x1411CC7D2,
    ''' f4ee `real_ ? true : false`); bool itself; string / array / object fail (exe line 3203).</summary>
    Public Function AsBool(v As JsonValor, ByRef ok As Boolean) As Boolean
        ok = True
        If v Is Nothing Then Return False
        Select Case v.Tipo
            Case JsonTipo.Nulo : Return False
            Case JsonTipo.Entero : Return v.Entero <> 0L
            Case JsonTipo.SinSigno : Return v.SinSigno <> 0UL
            Case JsonTipo.Real : Return Not (v.Real = 0.0)
            Case JsonTipo.Booleano : Return v.Booleano
        End Select
        ok = False : Return False
    End Function

    ''' <summary>asInt (exe 0x1411CCF00; f4ee :631-651): null 0; int within [-2^31, 2^31-1] (isInt, exe 0x1411D3786), uint up to
    ''' 0x7FFFFFFF (0x1411D3777); real within [-2147483648.0, 2147483647.0] truncated (comisd 0x1411CD01A..D030, cvttsd2si 0x1411CD032;
    ''' NaN fails; f4ee InRange :61-63); bool 1 / 0; anything else fails.</summary>
    Public Function AsInt(v As JsonValor, ByRef ok As Boolean) As Integer
        ok = True
        If v Is Nothing Then Return 0
        Select Case v.Tipo
            Case JsonTipo.Nulo : Return 0
            Case JsonTipo.Entero
                If v.Entero >= Integer.MinValue AndAlso v.Entero <= Integer.MaxValue Then Return CInt(v.Entero)
            Case JsonTipo.SinSigno
                If v.SinSigno <= &H7FFFFFFFUL Then Return CInt(v.SinSigno)
            Case JsonTipo.Real
#If DEBUG Then
                ' Ola JSON tester: asInt of a real rounded (cvtsd2si) instead of truncated (cvttsd2si 0x1411CD032).
                If Fo4GBufferConstants.GateMutantOn("oj-asint-redondea") AndAlso v.Real >= -2147483648.0 AndAlso v.Real <= 2147483647.0 Then Return CInt(Math.Round(v.Real))
#End If
                If v.Real >= -2147483648.0 AndAlso v.Real <= 2147483647.0 Then Return CInt(Math.Truncate(v.Real))
            Case JsonTipo.Booleano : Return If(v.Booleano, 1, 0)
        End Select
        ok = False : Return 0
    End Function

    ''' <summary>asUInt (f4ee :653-673; the exe's filler does not call it): null 0; int in [0, 2^32-1] (isUInt :1178-1191), uint up to
    ''' 2^32-1; real in [0, 4294967295.0] truncated; bool 1 / 0; anything else fails.</summary>
    Public Function AsUInt(v As JsonValor, ByRef ok As Boolean) As UInteger
        ok = True
        If v Is Nothing Then Return 0UI
        Select Case v.Tipo
            Case JsonTipo.Nulo : Return 0UI
            Case JsonTipo.Entero
                If v.Entero >= 0L AndAlso v.Entero <= UInteger.MaxValue Then Return CUInt(v.Entero)
            Case JsonTipo.SinSigno
                If v.SinSigno <= UInteger.MaxValue Then Return CUInt(v.SinSigno)
            Case JsonTipo.Real
                If v.Real >= 0.0 AndAlso v.Real <= 4294967295.0 Then Return CUInt(Math.Truncate(v.Real))
            Case JsonTipo.Booleano : Return If(v.Booleano, 1UI, 0UI)
        End Select
        ok = False : Return 0UI
    End Function

    ''' <summary>asFloat (exe 0x1411CCAA0; f4ee :758-778): null 0; int -&gt; float (cvtsi2ss r64 0x1411CCBF4, correctly rounded; f4ee
    ''' static_cast); uint -&gt; float: exe cvtsi2ss of the bits as int64 and, when they read negative, + 2^64 in float
    ''' (0x1411CCBD1..BE1: TWO roundings, 1 ulp off the correct one for some values at or above 2^63); f4ee / skee64
    ''' static_cast&lt;float&gt;(UInt64) (json_value.cpp:762-764, JSON_USE_INT64_DOUBLE_CONVERSION off: config.h:70-73) as the DLLs compiled it,
    ''' f4ee.dll 0x18006515F..518D = skee64.dll 0x18014B1BF..B1ED: below 2^63 cvtsi2ss, at or above (v &gt;&gt; 1) | (v And 1), cvtsi2ss and
    ''' addss x, x - one rounding, the sticky bit kept (<see cref="UInt64AFloatF4ee"/>); real -&gt; float (cvtpd2ps
    ''' 0x1411CCBC5); bool 1 / 0; string / array / object fail (exe 0x1411CCB9F / 0x1411CCBA5, line 3185).</summary>
    Public Function AsFloat(v As JsonValor, d As Dialecto, ByRef ok As Boolean) As Single
        ok = True
        If v Is Nothing Then Return 0.0F
        Select Case v.Tipo
            Case JsonTipo.Nulo : Return 0.0F
            Case JsonTipo.Entero : Return CSng(v.Entero)
            Case JsonTipo.SinSigno
#If DEBUG Then
                ' Ola JSON tester: the exe's uint at or above 2^63 correctly rounded (one rounding, not the two of 0x1411CCBD1..BE1).
                If Fo4GBufferConstants.GateMutantOn("oj-asfloat-uint-correcto") Then Return CSng(v.SinSigno)
#End If
                If d = Dialecto.Fallout4Exe Then
                    If v.SinSigno < &H8000000000000000UL Then Return CSng(CLng(v.SinSigno))
                    Dim comoInt64 = CSng(CLng(v.SinSigno - &H8000000000000000UL) + Long.MinValue)
                    Return comoInt64 + 1.8446744E+19F
                End If
#If DEBUG Then
                ' Ola JSON tester: f4ee's uint at or above 2^63 with the exe's two roundings (the dialects mixed up).
                If Fo4GBufferConstants.GateMutantOn("oj-asfloat-f4ee-dos-redondeos") AndAlso v.SinSigno >= &H8000000000000000UL Then
                    Return CSng(CLng(v.SinSigno - &H8000000000000000UL) + Long.MinValue) + 1.8446744E+19F
                End If
#End If
                Return UInt64AFloatF4ee(v.SinSigno)
            Case JsonTipo.Real : Return CSng(v.Real)
            Case JsonTipo.Booleano : Return If(v.Booleano, 1.0F, 0.0F)
        End Select
        ok = False : Return 0.0F
    End Function

    ''' <summary>static_cast&lt;float&gt;(UInt64) as f4ee.dll / skee64.dll compiled it (0x18006515F..518D / 0x18014B1BF..B1ED): below 2^63
    ''' cvtsi2ss of the int64; at or above, the value halved keeping its low bit ((v &gt;&gt; 1) Or (v And 1)), cvtsi2ss, and doubled
    ''' (addss x, x, exact).</summary>
    Friend Function UInt64AFloatF4ee(u As ULong) As Single
        If u < &H8000000000000000UL Then Return CSng(CLng(u))
        Dim mitad = CSng(CLng((u >> 1) Or (u And 1UL)))
        Return mitad + mitad
    End Function

    ''' <summary>asString (f4ee :606-623): null -&gt; ""; a string -&gt; its C string (stops at a NUL); bool -&gt; "true"/"false"; a number
    ''' -&gt; its text (a real: jsoncpp writes it with "%.17g", here "R" - only ever used for identifiers, which are not numbers);
    ''' array / object fail.</summary>
    Public Function AsString(v As JsonValor, ByRef ok As Boolean) As String
        ok = True
        If v Is Nothing Then Return ""
        Select Case v.Tipo
            Case JsonTipo.Nulo : Return ""
            Case JsonTipo.Texto : Return TextoDe(CortarEnNul(v.Bytes))
            Case JsonTipo.Booleano : Return If(v.Booleano, "true", "false")
            Case JsonTipo.Entero : Return v.Entero.ToString(Globalization.CultureInfo.InvariantCulture)
            Case JsonTipo.SinSigno : Return v.SinSigno.ToString(Globalization.CultureInfo.InvariantCulture)
            Case JsonTipo.Real : Return v.Real.ToString("R", Globalization.CultureInfo.InvariantCulture)
        End Select
        ok = False : Return ""
    End Function

    ''' <summary>asCString's bytes (exe 0x1411CC810, f4ee :600-604): ONLY a string - anything else, null included, fails (exe
    ''' "requires stringValue", line 2987). All of the string's bytes: a reader that takes them as a C string cuts at the NUL itself
    ''' (<see cref="AsCString"/>); the exe's colour parser reads past one.</summary>
    Public Function AsCStringBytes(v As JsonValor, ByRef ok As Boolean) As Byte()
        ok = v IsNot Nothing AndAlso v.Tipo = JsonTipo.Texto
        Return If(ok, v.Bytes, Array.Empty(Of Byte)())
    End Function

    ''' <summary>asCString as a C string: <see cref="AsCStringBytes"/> up to the first NUL, as text.</summary>
    Public Function AsCString(v As JsonValor, ByRef ok As Boolean) As String
        Dim b = AsCStringBytes(v, ok)
        Return If(ok, TextoDe(CortarEnNul(b)), "")
    End Function

    ''' <summary><c>value[i].asFloat()</c> for i = 0..count-1 (f4ee CharGenInterface.cpp:596-611, :478-480): over null/absent the [i]
    ''' makes it an array of nulls -&gt; zeros; a short array -&gt; 0 for the rest; object/scalar/string -&gt; [0] fails; an element
    ''' string/array/object -&gt; asFloat fails.</summary>
    Public Function Floats(v As JsonValor, count As Integer, d As Dialecto, ByRef ok As Boolean) As Single()
        Dim result(count - 1) As Single
        ok = True
        If v IsNot Nothing AndAlso v.Tipo <> JsonTipo.Nulo AndAlso v.Tipo <> JsonTipo.Lista Then ok = False : Return result
        For i = 0 To count - 1
            Dim e = If(v IsNot Nothing AndAlso v.Tipo = JsonTipo.Lista AndAlso i < v.Lista.Count, v.Lista(i), Nothing)
            result(i) = AsFloat(e, d, ok)
            If Not ok Then Exit For
        Next
        Return result
    End Function

    ' ===================================================================================================
    ' The DOM the editing loaders navigate (materialized from the tree; it decides nothing)
    ' ===================================================================================================

    ''' <summary>The tree as JSON text, the System.Text.Json DOM's input: objects with their keys in the file's order (a repeated
    ''' key once, with jsoncpp's last value), numbers with the file's own token when it is a JSON number (else the value: an int's
    ''' digits, a real with a '.' or an exponent so that it stays a real), strings re-escaped from their bytes (the same bytes back
    ''' through <see cref="ValorDe(JsonElement, Dialecto)"/>).</summary>
    Private Sub Escribir(v As JsonValor, o As List(Of Byte))
        Select Case v.Tipo
            Case JsonTipo.Nulo : o.AddRange(Encoding.ASCII.GetBytes("null"))
            Case JsonTipo.Booleano : o.AddRange(Encoding.ASCII.GetBytes(If(v.Booleano, "true", "false")))
            Case JsonTipo.Entero, JsonTipo.SinSigno, JsonTipo.Real
                o.AddRange(Encoding.ASCII.GetBytes(LiteralJson(v)))
            Case JsonTipo.Texto
                EscribirTexto(v.Bytes, o)
            Case JsonTipo.Lista
                o.Add(&H5B)
                For i = 0 To v.Lista.Count - 1
                    If i > 0 Then o.Add(&H2C)
                    Escribir(v.Lista(i), o)
                Next
                o.Add(&H5D)
            Case JsonTipo.Objeto
                o.Add(&H7B)
                Dim primero = True
                For Each k In v.OrdenDelArchivo
                    If Not primero Then o.Add(&H2C)
                    primero = False
                    EscribirTexto(k, o)
                    o.Add(&H3A)
                    Escribir(v.Miembros(k), o)
                Next
                o.Add(&H7D)
        End Select
    End Sub

    ''' <summary>A number's literal for the DOM: the file's token when it is a JSON number literal, else one that reads back as the
    ''' same jsoncpp value (an int's digits, a real's "R" text with ".0" added when it has no '.' or exponent; +-inf / NaN keep the
    ''' file's token, which <see cref="ValorDe(JsonElement, Dialecto)"/> reads back through the same decodeDouble).</summary>
    Private Function LiteralJson(v As JsonValor) As String
        If v.Literal IsNot Nothing AndAlso EsNumeroJson(v.Literal) Then Return v.Literal
        Select Case v.Tipo
            Case JsonTipo.Entero : Return v.Entero.ToString(Globalization.CultureInfo.InvariantCulture)
            Case JsonTipo.SinSigno : Return v.SinSigno.ToString(Globalization.CultureInfo.InvariantCulture)
        End Select
        If Double.IsInfinity(v.Real) OrElse Double.IsNaN(v.Real) Then Return If(v.Real < 0, "-1e400", "1e400")
        Dim s = v.Real.ToString("R", Globalization.CultureInfo.InvariantCulture)
        If s.IndexOfAny({"."c, "E"c, "e"c}) < 0 Then s &= ".0"
        Return s
    End Function

    ''' <summary>A JSON number literal (RFC 8259 6): -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?.</summary>
    Private Function EsNumeroJson(s As String) As Boolean
        Dim i = 0
        If i < s.Length AndAlso s(i) = "-"c Then i += 1
        If i >= s.Length OrElse Not Char.IsAsciiDigit(s(i)) Then Return False
        If s(i) = "0"c Then
            i += 1
        Else
            While i < s.Length AndAlso Char.IsAsciiDigit(s(i)) : i += 1 : End While
        End If
        If i < s.Length AndAlso s(i) = "."c Then
            i += 1
            If i >= s.Length OrElse Not Char.IsAsciiDigit(s(i)) Then Return False
            While i < s.Length AndAlso Char.IsAsciiDigit(s(i)) : i += 1 : End While
        End If
        If i < s.Length AndAlso (s(i) = "e"c OrElse s(i) = "E"c) Then
            i += 1
            If i < s.Length AndAlso (s(i) = "+"c OrElse s(i) = "-"c) Then i += 1
            If i >= s.Length OrElse Not Char.IsAsciiDigit(s(i)) Then Return False
            While i < s.Length AndAlso Char.IsAsciiDigit(s(i)) : i += 1 : End While
        End If
        Return i = s.Length
    End Function

    ''' <summary>A string's bytes as a JSON string: '"', '\' and the bytes below 0x20 escaped, every other byte as it is.</summary>
    Private Sub EscribirTexto(b As Byte(), o As List(Of Byte))
        o.Add(&H22)
        For Each c In b
            If c = &H22 OrElse c = &H5C Then
                o.Add(&H5C) : o.Add(c)
            ElseIf c < &H20 Then
                o.AddRange(Encoding.ASCII.GetBytes("\u" & CInt(c).ToString("x4")))
            Else
                o.Add(c)
            End If
        Next
        o.Add(&H22)
    End Sub

    ''' <summary>The tree as a JsonDocument (the caller disposes it).</summary>
    Public Function DocumentoDe(v As JsonValor) As JsonDocument
        Dim o As New List(Of Byte)
        Escribir(v, o)
        Return JsonDocument.Parse(New ReadOnlyMemory(Of Byte)(o.ToArray()), New JsonDocumentOptions With {.MaxDepth = Integer.MaxValue})
    End Function

    ''' <summary>The tree as a JsonNode (RaceMenuJslot's editable document).</summary>
    Public Function NodoDe(v As JsonValor) As JsonNode
        Dim o As New List(Of Byte)
        Escribir(v, o)
        Using ms As New IO.MemoryStream(o.ToArray())
            Return JsonNode.Parse(ms, Nothing, New JsonDocumentOptions With {.MaxDepth = Integer.MaxValue})
        End Using
    End Function

    ''' <summary>f4ee / skee64's read of a preset file: <see cref="Parsear"/> over the whole buffer, the root or Nothing when the reader
    ''' rejects it (the loaders' "invalid JSON": nothing applied).</summary>
    Public Function LeerF4ee(bytes As Byte(), Optional ByRef mensaje As String = Nothing) As JsonValor
        If bytes Is Nothing Then Return Nothing
        Dim raiz As JsonValor = Nothing
        If Parsear(bytes, 0, bytes.Length, Dialecto.F4eeSkee64, raiz, mensaje) <> ResultadoDelParseo.Leido Then Return Nothing
        Return raiz
    End Function

    ''' <summary>FALLOUT4.EXE'S LOADER OF A JSON DATA FILE (the *.sclp files, 0x14045F9D3; the *FacialBoneRegions*.txt files, 0x14068E73E):
    ''' 0x1403665F0 reads the whole file and hands it to 0x140367BC0, which NUL-terminates the buffer (0x140367BE9), cuts the text after
    ''' its LAST ']' or '}' (0x140367BF0..C03: byte - 0x5D tested against 0xDF; without one nothing is parsed and the load fails) and
    ''' parses it with the exe's Json::Reader (ctor 0x1411C7D40: Features::all; parse 0x1411D52F0) - no backslash pre-pass (that is the
    ''' material reader's only, 0x1417A9420). <paramref name="resultado"/>: Leido, Rechazado (the caller's value stays null: its load
    ''' fails) or Aborta (nesting 1000: the game closes). The tree for Leido, Nothing otherwise.</summary>
    Public Function LeerArchivoDelMotor(bytes As Byte(), ByRef resultado As ResultadoDelParseo, Optional ByRef mensaje As String = Nothing) As JsonValor
        resultado = ResultadoDelParseo.Rechazado
        If bytes Is Nothing Then Return Nothing
        ' The search walks back from the buffer's end (r8 = begin + size), not from a NUL inside it: the parse then stops at such a
        ' NUL by itself (the tokenizer's end of stream).
        Dim cierre = bytes.Length - 1
        Dim corchete As Byte = &H5D
#If DEBUG Then
        ' Ola JSON tester (revisor oj-01): the cut only at '}' (the material reader's strrchr 0x14216EF70), not at ']'.
        If Fo4GBufferConstants.GateMutantOn("oj-archivo-motor-solo-llave") Then corchete = &H7D
#End If
        While cierre >= 0 AndAlso bytes(cierre) <> corchete AndAlso bytes(cierre) <> &H7D
            cierre -= 1
        End While
        If cierre < 0 Then
            mensaje = "no ']' or '}': nothing parsed (0x140367C03)"
            Return Nothing
        End If
        Dim raiz As JsonValor = Nothing
        resultado = Parsear(bytes, 0, cierre + 1, Dialecto.Fallout4Exe, raiz, mensaje)
        Return If(resultado = ResultadoDelParseo.Leido, raiz, Nothing)
    End Function

    ''' <summary>A DOM value back as the jsoncpp value it was materialized from: a number through the same decodeNumber of
    ''' <paramref name="d"/> on its literal, a string's bytes (UTF-8 of its text), a container as itself (only its type matters to a
    ''' conversion). An undefined element (an absent member) is Nothing.</summary>
    Public Function ValorDe(el As JsonElement, d As Dialecto) As JsonValor
        Select Case el.ValueKind
            Case JsonValueKind.Undefined : Return Nothing
            Case JsonValueKind.Null : Return JsonValor.Nulo
            Case JsonValueKind.True : Return JsonValor.DeBooleano(True)
            Case JsonValueKind.False : Return JsonValor.DeBooleano(False)
            Case JsonValueKind.String : Return JsonValor.DeBytes(Encoding.UTF8.GetBytes(If(el.GetString(), "")))
            Case JsonValueKind.Array : Return JsonValor.NuevaLista()
            Case JsonValueKind.Object : Return JsonValor.NuevoObjeto()
        End Select
        Return NumeroDeLiteral(el.GetRawText(), d)
    End Function

    ''' <summary><see cref="ValorDe(JsonElement, Dialecto)"/> for a JsonNode (Nothing = absent).</summary>
    Public Function ValorDe(n As JsonNode, d As Dialecto) As JsonValor
        If n Is Nothing Then Return Nothing
        If TypeOf n Is JsonObject Then Return JsonValor.NuevoObjeto()
        If TypeOf n Is JsonArray Then Return JsonValor.NuevaLista()
        Dim v = TryCast(n, JsonValue)
        Dim el As JsonElement
        If v IsNot Nothing AndAlso v.TryGetValue(el) Then Return ValorDe(el, d)
        Return ValorDe(JsonSerializer.SerializeToElement(v), d)
    End Function

    ''' <summary>A number literal through the reader's decodeNumber of <paramref name="d"/>.</summary>
    Private Function NumeroDeLiteral(lit As String, d As Dialecto) As JsonValor
        Dim b = Encoding.ASCII.GetBytes(lit)
        Dim raiz As JsonValor = Nothing
        If Parsear(b, 0, b.Length, d, raiz) <> ResultadoDelParseo.Leido OrElse raiz Is Nothing Then Throw New InvalidOperationException($"not a jsoncpp number: '{lit}'")
        Return raiz
    End Function

    ''' <summary>Orden de claves de un objeto jsoncpp: <c>strcmp</c> sobre los bytes UTF-8 (:200-204).</summary>
    Public NotInheritable Class ComparadorStrcmp
        Implements IComparer(Of String)
        Public Shared ReadOnly Instancia As New ComparadorStrcmp()
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            Return ComparadorDeClaves.Instancia.Compare(Encoding.UTF8.GetBytes(If(x, "")), Encoding.UTF8.GetBytes(If(y, "")))
        End Function
    End Class

    ' ===================================================================================================
    ' The conversions over the DOM: the same law (ValorDe + the JsonValor conversion), f4ee's dialect
    ' ===================================================================================================

    Public Function AsFloat(el As JsonElement, ByRef ok As Boolean) As Single
        Return AsFloat(ValorDe(el, Dialecto.F4eeSkee64), Dialecto.F4eeSkee64, ok)
    End Function
    Public Function AsInt(el As JsonElement, ByRef ok As Boolean) As Integer
        Return AsInt(ValorDe(el, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsUInt(el As JsonElement, ByRef ok As Boolean) As UInteger
        Return AsUInt(ValorDe(el, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsBool(el As JsonElement, ByRef ok As Boolean) As Boolean
        Return AsBool(ValorDe(el, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsString(el As JsonElement, ByRef ok As Boolean) As String
        Return AsString(ValorDe(el, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsCString(el As JsonElement, ByRef ok As Boolean) As String
        Return AsCString(ValorDe(el, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function Empty(el As JsonElement) As Boolean
        Select Case el.ValueKind
            Case JsonValueKind.Array : Return el.GetArrayLength() = 0
            Case JsonValueKind.Object : Return Not el.EnumerateObject().MoveNext()
        End Select
        Return Vacio(ValorDe(el, Dialecto.F4eeSkee64))
    End Function

    ''' <summary><c>getMemberNames</c> + the member by key over the DOM (f4ee :1105-1127): null/absent -&gt; empty; object -&gt; its
    ''' members in strcmp order (the DOM holds each key once, with jsoncpp's last value); array/scalar/string -&gt; fails.</summary>
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

    ''' <summary><c>operator[](const char*)</c> non-const over the DOM (f4ee :970-994): null/absent -&gt; the member is born null
    ''' (Undefined here, which reads as null); object -&gt; the member or null; array/scalar/string -&gt; fails.</summary>
    Public Function Miembro(el As JsonElement, key As String, ByRef ok As Boolean) As JsonElement
        ok = True
        Dim r As New JsonElement()
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined
            Case JsonValueKind.Object
                el.TryGetProperty(key, r)
            Case Else
                ok = False
        End Select
        Return r
    End Function

    ''' <summary><c>isMember(key)</c> over the DOM (f4ee :1090-1093): null/absent -&gt; false; object -&gt; whether the key is there (null
    ''' counts); array/scalar/string -&gt; fails.</summary>
    Public Function IsMember(el As JsonElement, key As String, ByRef ok As Boolean) As Boolean
        ok = True
        Select Case el.ValueKind
            Case JsonValueKind.Null, JsonValueKind.Undefined : Return False
            Case JsonValueKind.Object
                Dim r As JsonElement
                Return el.TryGetProperty(key, r)
            Case Else
                ok = False : Return False
        End Select
    End Function

    ''' <summary>The range-for over the DOM (f4ee :1284-1402): array -&gt; its elements; object -&gt; its VALUES in strcmp key order;
    ''' null/scalar/string -&gt; nothing (does not fail).</summary>
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

    ''' <summary><c>value[i].asFloat()</c> for i = 0..count-1 over the DOM (the JsonValor law's <see cref="Floats(JsonValor, Integer, Dialecto, ByRef Boolean)"/>).</summary>
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

    Public Function AsFloat(n As JsonNode, ByRef ok As Boolean) As Single
        Return AsFloat(ValorDe(n, Dialecto.F4eeSkee64), Dialecto.F4eeSkee64, ok)
    End Function
    Public Function AsInt(n As JsonNode, ByRef ok As Boolean) As Integer
        Return AsInt(ValorDe(n, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsUInt(n As JsonNode, ByRef ok As Boolean) As UInteger
        Return AsUInt(ValorDe(n, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsBool(n As JsonNode, ByRef ok As Boolean) As Boolean
        Return AsBool(ValorDe(n, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsString(n As JsonNode, ByRef ok As Boolean) As String
        Return AsString(ValorDe(n, Dialecto.F4eeSkee64), ok)
    End Function
    Public Function AsCString(n As JsonNode, ByRef ok As Boolean) As String
        Return AsCString(ValorDe(n, Dialecto.F4eeSkee64), ok)
    End Function

    ''' <summary><c>empty()</c> (:862-867) over a node.</summary>
    Public Function Empty(n As JsonNode) As Boolean
        If n Is Nothing Then Return True
        Dim o = TryCast(n, JsonObject)
        If o IsNot Nothing Then Return o.Count = 0
        Dim a = TryCast(n, JsonArray)
        If a IsNot Nothing Then Return a.Count = 0
        Return Vacio(ValorDe(n, Dialecto.F4eeSkee64))
    End Function

    ''' <summary><c>isMember(key)</c> over a node (:1090-1093): null -&gt; false; object -&gt; whether the key is there; anything else
    ''' fails.</summary>
    Public Function IsMember(n As JsonNode, key As String, ByRef ok As Boolean) As Boolean
        ok = True
        If n Is Nothing Then Return False
        Dim o = TryCast(n, JsonObject)
        If o IsNot Nothing Then Return o.ContainsKey(key)
        If ValorDe(n, Dialecto.F4eeSkee64).Tipo = JsonTipo.Nulo Then Return False
        ok = False : Return False
    End Function

    ''' <summary><c>operator[](const char*)</c> non-const over a node (:970-994): null -&gt; the member is born null (Nothing); object
    ''' -&gt; the member (or null); anything else fails.</summary>
    Public Function Miembro(n As JsonNode, key As String, ByRef ok As Boolean) As JsonNode
        ok = True
        If n Is Nothing Then Return Nothing
        Dim o = TryCast(n, JsonObject)
        If o Is Nothing Then
            If ValorDe(n, Dialecto.F4eeSkee64).Tipo <> JsonTipo.Nulo Then ok = False
            Return Nothing
        End If
        Dim r As JsonNode = Nothing
        o.TryGetPropertyValue(key, r)
        Return r
    End Function

    ''' <summary><c>operator[](ArrayIndex)</c> non-const over a node (:918-936): null -&gt; the element is born null (Nothing); array -&gt;
    ''' the element (or null past its end); anything else fails.</summary>
    Public Function Elemento(n As JsonNode, index As Integer, ByRef ok As Boolean) As JsonNode
        ok = True
        If n Is Nothing Then Return Nothing
        Dim a = TryCast(n, JsonArray)
        If a Is Nothing Then
            If ValorDe(n, Dialecto.F4eeSkee64).Tipo <> JsonTipo.Nulo Then ok = False
            Return Nothing
        End If
        Return If(index < a.Count, a(index), Nothing)
    End Function

    ''' <summary>The range-for over a node (:1284-1402): array -&gt; its elements; object -&gt; its VALUES in strcmp key order;
    ''' null/scalar/string -&gt; nothing (does not fail).</summary>
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
    ''' valor se niega en aritmetica sin signo.
    '''
    ''' <para><b>DESBORDE — el HUECO se cerró con fuente, y el comentario que estaba acá era FALSO.</b> Decía
    ''' «el CRT acumula sin chequear; se asume que envuelve modulo 2^32». No es eso lo que hace: el UCRT
    ''' (fuente en disco, <c>Windows Kits/10/Source/10.0.26100.0/ucrt/</c>) SATURA. Cadena leída:</para>
    ''' <list type="number">
    ''' <item><c>%X</c> ⇒ <c>process_integer_specifier(16, false)</c> — corecrt_internal_stdio_input.h:1218.</item>
    ''' <item>acumula en <b>64 bits</b>: <c>parse_integer&lt;uint64_t&gt;</c> — corecrt_internal_strtox.h:223-350.</item>
    ''' <item>marca <c>FL_OVERFLOW</c> con <c>number &gt; max_pre_multiply_value</c> o suma que se da vuelta (:296-311).</item>
    ''' <item>con <c>FL_OVERFLOW</c> y <c>is_signed=false</c> <b>satura</b>: <c>number = (UnsignedInteger)-1</c> (:333-340).</item>
    ''' <item>trunca al ancho del destino: <c>static_cast&lt;uint32_t&gt;(value)</c> — stdio_input.h:1573.</item>
    ''' </list>
    ''' <para>⇒ para todo valor ≥ 2^64 el motor devuelve <b>0xFFFFFFFF</b>, no los 32 bits bajos. Por debajo de
    ''' 2^64 el acumulador de 64 bits no desborda y la truncada equivale a envolver módulo 2^32 — que es lo que
    ''' hacía esta función —, por eso coincidían en todo el rango alcanzable y la diferencia quedaba escondida.
    ''' Acá se transcribe la ley entera: acumulador de 64 bits, bandera de desborde y saturación.</para>
    ''' <para>ALCANCE: la clave sale del nombre de miembro de <c>"Tints"</c>, que LooksMenu escribe como
    ''' <c>%X</c> de un índice de 16 bits, y después pasa por <c>GetTemplateByIndex(UInt16)</c>
    ''' (CharGenInterface.cpp:512) ⇒ hace falta un archivo editado a mano con ≥17 dígitos hex significativos
    ''' para verlo. Es red, no reparación de algo que rompa hoy.</para>
    ''' <para>CAVEAT: vale si los plugins linkean UCRT (VS2015 en adelante). No se verificó con qué toolset se
    ''' compilaron <c>f4ee</c> y <c>skee64</c>; con el CRT anterior el comportamiento podría ser otro.</para></summary>
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
        ' Acumulador de 64 bits + bandera, como `parse_integer<uint64_t>` (corecrt_internal_strtox.h:246-315).
        Dim v As ULong = 0UL
        Dim desbordo = False
        Dim maxAntesDeMultiplicar As ULong = ULong.MaxValue \ 16UL      ' `max_pre_multiply_value` (:296)
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
            ' `number_after_multiply` / `number_after_add` y la marca de desborde (:302-311).
            ' ⛔ EL ORDEN NO ES COSMETICO. C++ deja que `number * base` ENVUELVA y despues mira la bandera;
            ' VB tiene el chequeo de desbordamiento PRENDIDO y ahi `v * 16UL` LANZARIA — un preset con una
            ' clave larga tiraria OverflowException en vez de dar el 0xFFFFFFFF del motor. Con la guardia
            ' `v > max_pre_multiply_value` ADELANTE, la multiplicacion ya no puede desbordar (v <= MaxValue entre 16
            ' ⇒ v*16 <= MaxValue-15) y la suma tampoco (d <= 15). El `number_after_add < number_after_multiply`
            ' de C++ queda muerto por construccion: solo cazaba el caso en que la multiplicacion ya habia
            ' envuelto. Y una vez marcado el desborde el acumulado ya no importa — `is_overflow_condition`
            ' (:205-217) hace que la funcion devuelva el saturado igual.
            If v > maxAntesDeMultiplicar Then
                desbordo = True
            Else
                v = v * 16UL + CULng(d)
            End If
            digitos += 1
            i += 1
        End While
        ' `if ((flags & FL_READ_DIGIT) == 0) return 0` (:325-329). Los llamadores ya traen `keyValue = 0`, asi
        ' que "no asigna" y "asigna 0" dan lo mismo.
        If digitos = 0 Then Return 0UI
        ' `is_overflow_condition` (:205-217): sin FL_SIGNED solo cuenta FL_OVERFLOW, y ahi `number = (uint64)-1`
        ' (:337-340), que truncado a 32 bits (stdio_input.h:1573) queda 0xFFFFFFFF. Va ANTES que el signo porque
        ' esa rama sale por su propio `return`, sin pasar por la negacion (:333-347).
        If desbordo Then Return UInteger.MaxValue
        ' `number = -(signed)number` en 64 bits (:344) y despues la truncada de stdio_input.h:1573. Negar en
        ' 64 y truncar a 32 da lo mismo que negar los 32 bits bajos modulo 2^32, y asi se escribe: `0UL - v`
        ' lanzaria con el chequeo de desbordamiento de VB.
        Dim v32 As UInteger = CUInt(v And &HFFFFFFFFUL)
        If negativo Then v32 = CUInt((&H100000000UL - CULng(v32)) And &HFFFFFFFFUL)
        Return v32
    End Function

    ' ===================================================================================================
    ' Los bytes tal como los ve jsoncpp
    ' ===================================================================================================

    ''' <summary>Un lector UTF-8 que LANZA en vez de sustituir. `Encoding.UTF8` trae el fallback de
    ''' reemplazo, que es justamente el que no avisa.</summary>
    Private ReadOnly Utf8Estricto As Encoding =
        Encoding.GetEncoding("utf-8", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)

    ''' <summary>Los bytes de un preset tal como los ve jsoncpp, para que el texto de sus strings sea el que la app compara.
    '''
    ''' <para><b>El motor.</b> <c>Reader::readString</c> (json_reader.cpp:390-400) consume CUALQUIER byte
    ''' hasta la comilla sin escapar: no valida UTF-8 ni rechaza controles. El byte llega crudo al
    ''' <c>std::string</c>, y de ahí a <c>GetFormFromIdentifier</c> → <c>LookupModByName</c>, que compara
    ''' BYTES contra el nombre del <c>ModInfo</c>. Acá se decodifica con
    ''' <see cref="PluginEncodingSettings.General"/>, que es EXACTAMENTE la encoding con la que esta lib lee los
    ''' campos no traducibles de los plugins — o sea, contra la que se va a comparar después. Su default es
    ''' cp1252 (<c>PluginEncodingSettings.vb:197</c>) pero es CONFIGURABLE (<c>:327</c>).</para>
    '''
    ''' <para><b>La regla.</b> Si el archivo es UTF-8 válido no se toca NADA. Sólo cuando el UTF-8 es inválido se
    ''' transcodifica con la encoding General, que es el espacio de bytes en el que la app compara nombres de plugin,
    ''' y el llamador lo registra. Censo del 2026-09-04 sobre el disco entero: 807 archivos de preset, 0 con UTF-8
    ''' inválido. (El carácter de control crudo dentro de un string, que System.Text.Json rechazaba, ya no es un hueco:
    ''' el lector es la transcripción de jsoncpp, <see cref="Parsear"/>.)</para></summary>
    ''' <param name="transcodificado">True cuando hubo que caer a la encoding General. El llamador tiene que dejarlo
    ''' en el log: si no, se vuelve a perder el aviso.</param>
    Public Function BytesComoLosVeJsoncpp(bytes As Byte(), ByRef transcodificado As Boolean) As Byte()
        transcodificado = False
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return bytes
        Try
            ' GetCharCount y no GetString: valida igual y no aloca la cadena entera en el camino feliz.
            Utf8Estricto.GetCharCount(bytes)
            Return bytes
        Catch ex As DecoderFallbackException
            transcodificado = True
            Return Encoding.UTF8.GetBytes(PluginEncodingSettings.General.GetString(bytes))
        End Try
    End Function
End Module
