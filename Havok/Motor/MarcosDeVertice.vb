Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclUpdateAllVertexFramesOperator` — type **12**, `0x1418FB3B0`.
'
' El `type` sale de la tabla de salto del despachador (`0x1418C6390`): la entrada 12 cae en
' `0x1418C627B`, que resuelve **un** buffer con la doble indirección
' `buffers[ buffers[ op.bufferIdx (+0x80) ].Ranura ]` y salta a `0x1418FB3B0`.
'
' Recalcula el marco TBN de la malla a partir de las posiciones ya simuladas. Tres fases, cada una
' con su bandera: `updateNormals` (+0x88), `updateTangents` (+0x89), `updateBitangents` (+0x8A).
'
' ⭐ FASE 1 — LAS NORMALES (`0x1418FB580`; la ley entera, sin desenrollar, está en el bucle de la
' cola, `0x1418FC164`-`0x1418FC256`):
'
'     scratch[0 … numUniqueNormalIDs) = 0                    ' 0x1418FB6BC-D A
'     por cada triángulo t con vértices (v0, v1, v2):
'         e0 = P[v1] − P[v0]                                 ' 0x1418FC1A8
'         e1 = P[v2] − P[v0]                                 ' 0x1418FC1BD
'         n  = e0 × e1                                       ' 0x1418FC1D0…0x1418FC20E
'         bit = (triangleFlips[t >> 3] >> (7 − (t AND 7))) AND 1   ' 0x1418FC1B8-C5, MSB primero
'         n  = (1,0 − 4,0 · bit) · n                         ' 0x1418FC201/08/12
'         scratch[vertToNormalID[v0]] += n                    ' 0x1418FC218/1B
'         scratch[vertToNormalID[v1]] += n                    ' 0x1418FC236/39
'         scratch[vertToNormalID[v2]] += n                    ' 0x1418FC24D/50
'     por cada slot del scratch: se NORMALIZA EN EL SITIO     ' 0x1418FC3C7-D7, rsqrt CRUDO con guarda
'     por cada vértice v: N[v] = scratch[vertToNormalID[v]]   ' 0x1418FC544-62, **12 bytes**
'
' ⛔⛔ EL FACTOR DEL FLIP ES `1 − 4·bit`, o sea **+1 o −3**. No es −1. Las dos constantes están en
' `0x142F3C560` (`{1,1,1,1}`) y `0x142483EE0` (`{4,4,4,4}`), y el `cvtdq2ps` del bit las junta en
' `0x1418FC201`/`08`. Con la normalización final el signo es lo que se ve, pero el **peso por área**
' del triángulo dado vuelta queda triplicado, y eso sí cambia el número.
'
' ⭐ FASE 2 — LAS TANGENTES (`0x1418FD6A0`, y el mismo núcleo en la variante TB `0x1418FF430`):
'
'     e  = P[referenceVertices[v]] − P[v]                     ' 0x1418FD8C5/D6
'     n  = N[v]
'     t0 = normalizar_rsqrt_crudo_con_guarda(e − n · dot3(n, e))   ' 0x1418FD92F…0x1418FD9E8
'     p  = n × t0                                             ' 0x1418FD9F8…0x1418FDA20
'     T[v] = tangentEdgeCosAngle[v] · t0 + tangentEdgeSinAngle[v] · p   ' 0x1418FF33A/56/32D/6E/71
'
' ⭐ FASE 3 — LAS BITANGENTES (`0x1418FF430`):
'
'     B[v] = biTangentFlip[v] · (n × T[v])                    ' 0x1418FFA0B…0x1418FFA71
'
' ⛔ Los tres canales se escriben de a **12 bytes** (`movsd` + `movhlps` + `movss`), como el skin.
'
' ⚠️ CERO apariciones en el corpus vanilla. Va igual: es de la lista cerrada.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>`hclUpdateAllVertexFramesOperator` — type 12, `0x1418FB3B0`.</summary>
    Friend NotInheritable Class OpMarcosDeVertice
        Inherits OperadorCompilado

        Private ReadOnly _buffer As Integer
        Private ReadOnly _vertANormalId As Integer()
        Private ReadOnly _flipsDeTriangulo As Byte()
        Private ReadOnly _verticeDeReferencia As Integer()
        Private ReadOnly _cos As Single(), _sen As Single(), _flipBitangente As Single()
        Private ReadOnly _numNormalIdsUnicos As Integer
        Private ReadOnly _normales As Boolean, _tangentes As Boolean, _bitangentes As Boolean

        Friend Sub New(buffer As Integer, vertANormalId As Integer(), flipsDeTriangulo As Byte(),
                       verticeDeReferencia As Integer(), cosAngulo As Single(), senAngulo As Single(),
                       flipBitangente As Single(), numNormalIdsUnicos As Integer,
                       normales As Boolean, tangentes As Boolean, bitangentes As Boolean,
                       nombre As String)
            MyBase.New(12, nombre)
            _buffer = buffer
            _vertANormalId = vertANormalId
            _flipsDeTriangulo = flipsDeTriangulo
            _verticeDeReferencia = verticeDeReferencia
            _cos = cosAngulo : _sen = senAngulo : _flipBitangente = flipBitangente
            _numNormalIdsUnicos = numNormalIdsUnicos
            _normales = normales : _tangentes = tangentes : _bitangentes = bitangentes
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            Dim b = Buffers.Real(ctx.Buffers, _buffer)
            If b Is Nothing Then Return
            If _normales Then Normales(b)
            If _tangentes Then Tangentes(b)
        End Sub

        ''' <summary>
        ''' Fase 1 — `0x1418FB580`, con la ley sin desenrollar en `0x1418FC164`-`0x1418FC256`.
        ''' <para>⛔ El scratch tiene `numUniqueNormalIDs` casillas de 16 B y se acumula por
        ''' `vertToNormalID`, no por vértice: dos vértices que comparten normal comparten casilla, que
        ''' es exactamente el «soldado» de la malla.</para>
        ''' </summary>
        Private Sub Normales(b As Buffer)
            If b.Normales Is Nothing OrElse b.IndicesDeTriangulo Is Nothing Then Return
            If _vertANormalId Is Nothing OrElse _vertANormalId.Length = 0 Then Return
            Dim n = Math.Max(1, _numNormalIdsUnicos)
            Dim scratch(n * 4 - 1) As Single                       ' 0x1418FB6BC-DA: arranca en CERO

            Dim uno = Vector128.Create(1.0F)                       ' 0x142F3C560
            Dim cuatro = Vector128.Create(4.0F)                    ' 0x142483EE0
            For t = 0 To b.NumTriangulos - 1
                Dim v0 = CInt(b.IndicesDeTriangulo(t * 3))
                Dim v1 = CInt(b.IndicesDeTriangulo(t * 3 + 1))
                Dim v2 = CInt(b.IndicesDeTriangulo(t * 3 + 2))
                If v0 >= b.Cuenta OrElse v1 >= b.Cuenta OrElse v2 >= b.Cuenta Then Continue For
                Dim p0 = b.Vertice(v0)
                Dim e0 = Vector128.Subtract(b.Vertice(v1), p0)     ' 0x1418FC1A8
                Dim e1 = Vector128.Subtract(b.Vertice(v2), p0)     ' 0x1418FC1BD
                Dim cruz = Polar.Cruz(e0, e1)                      ' 0x1418FC1D0…0x1418FC20E

                ' ⛔ el bit del flip: un BYTE cada ocho triangulos, el MSB primero
                Dim bit = 0.0F
                If _flipsDeTriangulo IsNot Nothing AndAlso (t >> 3) < _flipsDeTriangulo.Length Then
                    bit = CSng((CInt(_flipsDeTriangulo(t >> 3)) >> (7 - (t And 7))) And 1)
                End If
                ' ⛔⛔ `1 − 4·bit`, o sea +1 o **−3**. No es −1: son las dos constantes del binario.
                Dim f = Vector128.Subtract(uno, Vector128.Multiply(Vector128.Create(bit), cuatro))
                Dim nrm = Vector128.Multiply(f, cruz)              ' 0x1418FC212

                Acumular(scratch, v0, nrm)                          ' 0x1418FC218/1B
                Acumular(scratch, v1, nrm)                          ' 0x1418FC236/39
                Acumular(scratch, v2, nrm)                          ' 0x1418FC24D/50
            Next

            ' se normaliza EN EL SITIO, casilla por casilla (`0x1418FC3C7`-`D7`)
            For k = 0 To n - 1
                Simd.Escribir(scratch, k, Simd.NormalizarRsqrtCrudoConGuarda(Simd.Leer(scratch, k)))
            Next

            ' y recien ahi se reparte a los vertices (`0x1418FC544`-`62`)
            For v = 0 To Math.Min(b.Cuenta, _vertANormalId.Length) - 1
                Dim id = _vertANormalId(v)
                If id < 0 OrElse id >= n Then Continue For
                b.SetNormal(v, Simd.Leer(scratch, id))
            Next
        End Sub

        Private Sub Acumular(scratch As Single(), v As Integer, nrm As Vector128(Of Single))
            If _vertANormalId Is Nothing OrElse v < 0 OrElse v >= _vertANormalId.Length Then Return
            Dim id = _vertANormalId(v)
            If id < 0 OrElse id * 4 + 3 >= scratch.Length Then Return
            Simd.Escribir(scratch, id, Vector128.Add(Simd.Leer(scratch, id), nrm))
        End Sub

        ''' <summary>
        ''' Fases 2 y 3 — `0x1418FD6A0` (T) y `0x1418FF430` (TB).
        ''' <para>La tangente es la arista de referencia PROYECTADA fuera de la normal, normalizada, y
        ''' después girada alrededor de la normal por el ángulo que el archivo trae ya en seno y
        ''' coseno. La bitangente es `flip · (n × T)`.</para>
        ''' </summary>
        Private Sub Tangentes(b As Buffer)
            If b.Tangentes Is Nothing OrElse b.Normales Is Nothing Then Return
            If _verticeDeReferencia Is Nothing OrElse _cos Is Nothing OrElse _sen Is Nothing Then Return
            Dim n = Math.Min(b.Cuenta, _verticeDeReferencia.Length)
            For v = 0 To n - 1
                Dim r = _verticeDeReferencia(v)
                If r < 0 OrElse r >= b.Cuenta Then Continue For
                Dim nor = b.Normal(v)
                Dim e = Vector128.Subtract(b.Vertice(r), b.Vertice(v))          ' 0x1418FD8C5/D6
                ' proyeccion fuera de la normal (`0x1418FD92F`…`0x1418FD944`)
                Dim d = Simd.Dot3(nor, e)
                Dim t0 = Simd.NormalizarRsqrtCrudoConGuarda(
                    Vector128.Subtract(e, Vector128.Multiply(nor, d)))          ' 0x1418FD9DA-E8
                Dim p = Polar.Cruz(nor, t0)                                     ' 0x1418FD9F8…0x1418FDA20
                Dim c = If(v < _cos.Length, _cos(v), 1.0F)
                Dim s = If(v < _sen.Length, _sen(v), 0.0F)
                Dim tan = Vector128.Add(Vector128.Multiply(Vector128.Create(c), t0),
                                        Vector128.Multiply(Vector128.Create(s), p))
                b.SetTangente(v, tan)                                           ' 12 bytes

                If _bitangentes AndAlso b.Bitangentes IsNot Nothing Then
                    Dim fl = If(_flipBitangente IsNot Nothing AndAlso v < _flipBitangente.Length,
                                _flipBitangente(v), 1.0F)
                    b.SetBitangente(v, Vector128.Multiply(Vector128.Create(fl),
                                                          Polar.Cruz(nor, tan)))  ' 0x1418FFA0B…A71
                End If
            Next
        End Sub

    End Class

    ''' <summary>
    ''' `hclUpdateSomeVertexFramesOperator` — type **13**, `0x141902600`.
    ''' <para>Es la MISMA ley que <see cref="OpMarcosDeVertice"/> — mismo prólogo, mismas dos tablas
    ''' de salto, misma familia de cadenas de perfilado — pero sobre una SELECCIÓN, con dos niveles
    ''' de índice que hubo que leer porque los nombres no los dicen:</para>
    ''' <para>· los índices de `involvedTriangles` (`+0x20`, `{uint16[3]}` de 6 B) NO son del buffer:
    ''' son índices INVOLVED. La posición sale de `involvedVertices[iv]` (`+0x30`, `0x141902A0D`) y
    ''' el destino del acumulador de `involvedVertexToNormalID[iv]` (`+0x50`, `0x141903076`), con el
    ''' MISMO `iv`.</para>
    ''' <para>· la fase de tangentes recorre la SELECCIÓN: `iv = selectionVertexToInvolvedVertex[s]`
    ''' (`+0x40`, `0x141904628`), `vBuffer = involvedVertices[iv]` (`0x14190462E`), y
    ''' `referenceVertices[s]` (`+0x70`) es índice de BUFFER — va derecho al `imul` por el stride
    ''' (`0x1419046B4`/`BA`/`BE`).</para>
    ''' <para>⚠️ CERO apariciones en el corpus vanilla.</para>
    ''' </summary>
    Friend NotInheritable Class OpMarcosDeVerticeAlgunos
        Inherits OperadorCompilado

        Private ReadOnly _buffer As Integer
        Private ReadOnly _triangulos As Integer()
        Private ReadOnly _verticesInvolucrados As Integer()
        Private ReadOnly _seleccionAInvolucrado As Integer()
        Private ReadOnly _involucradoANormalId As Integer()
        Private ReadOnly _flipsDeTriangulo As Byte()
        Private ReadOnly _verticeDeReferencia As Integer()
        Private ReadOnly _cos As Single(), _sen As Single(), _flipBitangente As Single()
        Private ReadOnly _numNormalIdsUnicos As Integer
        Private ReadOnly _normales As Boolean, _tangentes As Boolean, _bitangentes As Boolean

        Friend Sub New(buffer As Integer, triangulos As Integer(), verticesInvolucrados As Integer(),
                       seleccionAInvolucrado As Integer(), involucradoANormalId As Integer(),
                       flipsDeTriangulo As Byte(), verticeDeReferencia As Integer(),
                       cosAngulo As Single(), senAngulo As Single(), flipBitangente As Single(),
                       numNormalIdsUnicos As Integer, normales As Boolean, tangentes As Boolean,
                       bitangentes As Boolean, nombre As String)
            MyBase.New(13, nombre)
            _buffer = buffer
            _triangulos = triangulos
            _verticesInvolucrados = verticesInvolucrados
            _seleccionAInvolucrado = seleccionAInvolucrado
            _involucradoANormalId = involucradoANormalId
            _flipsDeTriangulo = flipsDeTriangulo
            _verticeDeReferencia = verticeDeReferencia
            _cos = cosAngulo : _sen = senAngulo : _flipBitangente = flipBitangente
            _numNormalIdsUnicos = numNormalIdsUnicos
            _normales = normales : _tangentes = tangentes : _bitangentes = bitangentes
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            Dim b = Buffers.Real(ctx.Buffers, _buffer)
            If b Is Nothing Then Return
            If _normales Then Normales(b)
            If _tangentes Then Tangentes(b)
        End Sub

        ''' <summary>Del índice INVOLVED al vértice del buffer (`involvedVertices`, `0x141902A0D`).</summary>
        Private Function VerticeDeInvolucrado(iv As Integer) As Integer
            If _verticesInvolucrados Is Nothing OrElse iv < 0 OrElse
               iv >= _verticesInvolucrados.Length Then Return -1
            Return _verticesInvolucrados(iv)
        End Function

        Private Sub Normales(b As Buffer)
            If b.Normales Is Nothing OrElse _triangulos Is Nothing Then Return
            If _involucradoANormalId Is Nothing OrElse _involucradoANormalId.Length = 0 Then Return
            Dim n = Math.Max(1, _numNormalIdsUnicos)
            Dim scratch(n * 4 - 1) As Single

            Dim uno = Vector128.Create(1.0F), cuatro = Vector128.Create(4.0F)
            Dim nt = _triangulos.Length \ 3
            For t = 0 To nt - 1
                Dim a = _triangulos(t * 3), bb = _triangulos(t * 3 + 1), c = _triangulos(t * 3 + 2)
                Dim va = VerticeDeInvolucrado(a), vb = VerticeDeInvolucrado(bb), vc = VerticeDeInvolucrado(c)
                If va < 0 OrElse vb < 0 OrElse vc < 0 Then Continue For
                If va >= b.Cuenta OrElse vb >= b.Cuenta OrElse vc >= b.Cuenta Then Continue For
                Dim p0 = b.Vertice(va)
                Dim cruz = Polar.Cruz(Vector128.Subtract(b.Vertice(vb), p0),
                                      Vector128.Subtract(b.Vertice(vc), p0))
                Dim bit = 0.0F
                If _flipsDeTriangulo IsNot Nothing AndAlso (t >> 3) < _flipsDeTriangulo.Length Then
                    bit = CSng((CInt(_flipsDeTriangulo(t >> 3)) >> (7 - (t And 7))) And 1)
                End If
                Dim f = Vector128.Subtract(uno, Vector128.Multiply(Vector128.Create(bit), cuatro))
                Dim nrm = Vector128.Multiply(f, cruz)
                AcumularInvolucrado(scratch, a, nrm)
                AcumularInvolucrado(scratch, bb, nrm)
                AcumularInvolucrado(scratch, c, nrm)
            Next

            For k = 0 To n - 1
                Simd.Escribir(scratch, k, Simd.NormalizarRsqrtCrudoConGuarda(Simd.Leer(scratch, k)))
            Next

            For iv = 0 To _involucradoANormalId.Length - 1
                Dim v = VerticeDeInvolucrado(iv)
                If v < 0 OrElse v >= b.Cuenta Then Continue For
                Dim id = _involucradoANormalId(iv)
                If id < 0 OrElse id >= n Then Continue For
                b.SetNormal(v, Simd.Leer(scratch, id))
            Next
        End Sub

        Private Sub AcumularInvolucrado(scratch As Single(), iv As Integer, nrm As Vector128(Of Single))
            If _involucradoANormalId Is Nothing OrElse iv < 0 OrElse
               iv >= _involucradoANormalId.Length Then Return
            Dim id = _involucradoANormalId(iv)
            If id < 0 OrElse id * 4 + 3 >= scratch.Length Then Return
            Simd.Escribir(scratch, id, Vector128.Add(Simd.Leer(scratch, id), nrm))
        End Sub

        Private Sub Tangentes(b As Buffer)
            If b.Tangentes Is Nothing OrElse b.Normales Is Nothing Then Return
            If _seleccionAInvolucrado Is Nothing OrElse _verticeDeReferencia Is Nothing Then Return
            For sel = 0 To _seleccionAInvolucrado.Length - 1
                Dim v = VerticeDeInvolucrado(_seleccionAInvolucrado(sel))
                If v < 0 OrElse v >= b.Cuenta Then Continue For
                If sel >= _verticeDeReferencia.Length Then Continue For
                Dim r = _verticeDeReferencia(sel)
                If r < 0 OrElse r >= b.Cuenta Then Continue For
                Dim nor = b.Normal(v)
                Dim e = Vector128.Subtract(b.Vertice(r), b.Vertice(v))
                Dim t0 = Simd.NormalizarRsqrtCrudoConGuarda(
                    Vector128.Subtract(e, Vector128.Multiply(nor, Simd.Dot3(nor, e))))
                Dim p = Polar.Cruz(nor, t0)
                Dim c = If(_cos IsNot Nothing AndAlso sel < _cos.Length, _cos(sel), 1.0F)
                Dim sn = If(_sen IsNot Nothing AndAlso sel < _sen.Length, _sen(sel), 0.0F)
                Dim tng = Vector128.Add(Vector128.Multiply(Vector128.Create(c), t0),
                                        Vector128.Multiply(Vector128.Create(sn), p))
                b.SetTangente(v, tng)
                If _bitangentes AndAlso b.Bitangentes IsNot Nothing Then
                    Dim fl = If(_flipBitangente IsNot Nothing AndAlso sel < _flipBitangente.Length,
                                _flipBitangente(sel), 1.0F)
                    b.SetBitangente(v, Vector128.Multiply(Vector128.Create(fl), Polar.Cruz(nor, tng)))
                End If
            Next
        End Sub

    End Class

End Namespace

