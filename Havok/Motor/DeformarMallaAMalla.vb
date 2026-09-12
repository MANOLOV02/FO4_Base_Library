Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclObjectSpaceMeshMeshDeform*Operator` — types **30 a 33**, y
' `hclBoneSpaceMeshMeshDeform*Operator` — types **26 a 29**.
'
' ⭐⭐ LA CADENA, LEÍDA DE PUNTA A PUNTA (`0x141957FF0` → `0x141A04B60` → `0x141A0EEB0` →
' `0x14193C5E0`, y el equivalente de espacio de hueso `0x141954BC0` → `0x1419FD0E0` → `0x141923CF0`):
'
'   1. `M = Componer( entrada.AEspacioDeSimulacion , salida.DesdeEspacioDeSimulacion )`
'      `0x141A04BEE`-`0x141A04CCA`: las filas de `[rsi+0x80…0xB0]` transformadas por `[rbp+0x00…0x30]`,
'      con la traslación entrando tras el término en `x`.
'
'   2. `marco[t]` = el MISMO marco de triángulo del type 16 (`0x141A04FE7`, con la misma `1/3` de
'      `0x142F3C660`): `c = (p0+p1+p2)/3`, `a = p0−c`, `b = p1−c`, `n = a × b`, con `w = 0` en las
'      filas (`pslldq`/`psrldq`) y `w = 1` en el centroide (`0x141A0504F`, el `{1,1,1,1}` de
'      `0x142F3C560`).
'
'   3. `scratch[t] = Componer( Componer( triangleFromMeshTransforms[t] , marco[t] ) , M )`
'      `0x141A0EEB0` lo hace en DOS fases: la primera compone `[r8]` (= `op+0x40`, o sea
'      `triangleFromMeshTransforms`) con `[rdx]` (= los marcos) — `0x141A0EFE8`-`0x141A0F0BC` —, y la
'      segunda compone eso con `[r10]` (= `M`) — `0x141A0FED7`-`0x141A0FF56`. Las dos con filas de
'      TRES términos y la traslación tras el término en `x` (`0x141A0F04A`, `0x141A0FEFC`).
'
'   4. el DEFORM: `0x14193C5E0` en espacio-objeto — **el mismo que consume los bloques locales de la
'      piel** (`Piel.Deformar`) — y `0x141923CF0` en espacio de hueso — **el mismo de
'      `hclBoneSpaceSkin`** (`OpPielDeHueso.Deformar`).
'
' ⭐⭐ `scaleNormalBehaviour` (+0x28) NO ES UN CAMPO IGNORABLE: elige entre TRES constructores de
' marco (`0x141A04B9B`-`0x141A04BB2`), y los tres son leyes distintas — medido por multiconjunto de
' instrucciones SIMD:
'
'     0     → `0x141A051C0`  (196 ins · 2 `rsqrtps` + 2 `cmpleps` + 2 `andnps`)
'             la normal del marco sale **NORMALIZADA**, con el `rsqrt` CRUDO y la guarda de norma nula
'     2     → `0x141A054D0`  (185 ins · 2 `rcpps`)
'             la normal se escala por el **RECÍPROCO**
'     otro  → `0x141A04F20`  (162 ins · ni `rsqrt` ni `rcp`)
'             la normal queda **CRUDA**
'
' ⛔ `hclBoneSpaceMeshMeshDeform*` **NO declara `triangleFromMeshTransforms`** — la reflexión le da
' `inputBufferIdx`, `outputBufferIdx`, `scaleNormalBehaviour`, `inputTrianglesSubset` y
' `boneSpaceDeformer`, y nada más. Su paso 3 es sólo `Componer(marco[t], M)`. Las dos familias NO
' son simétricas, y transcribir una por analogía con la otra habría metido un factor que no existe.
'
' Los buffers, con doble indirección las dos (`0x1418C5FBB`-`0x1418C5FE1`):
'     entrada = buffers[ buffers[ op.inputBufferIdx  (+0x20) ].Ranura ]
'     salida  = buffers[ buffers[ op.outputBufferIdx (+0x24) ].Ranura ]
'
' ⛔ `inputTrianglesSubset` (+0x30) elige QUÉ triángulos entran: con la lista vacía entran todos y el
' `scratch` se indexa por triángulo; con ella, **por POSICIÓN en el subconjunto** — los índices del
' deformer apuntan a ESE arreglo. Es la misma ley que el `transformSubset` de la piel (`0x141A10F00`).
'
' ⚠️ CERO apariciones en el corpus vanilla para las ocho.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>`hclObjectSpaceMeshMeshDeform*Operator` — types 30 a 33.</summary>
    Friend NotInheritable Class OpMallaAMallaEnEspacioObjeto
        Inherits OperadorCompilado

        Private ReadOnly _bufferDeEntrada As Integer
        Private ReadOnly _trianguloDesdeMalla As Mat4()
        Private ReadOnly _subconjunto As Integer()
        Private ReadOnly _escalaDeNormal As Integer
        Private ReadOnly _piel As PielCompilada

        Friend Sub New(bufferDeEntrada As Integer, trianguloDesdeMalla As Mat4(),
                       subconjunto As Integer(), escalaDeNormal As Integer,
                       piel As PielCompilada, nombre As String)
            ' 30 = P, 31 = PN, 32 = PNT, 33 = PNTB — la tabla de `0x1418C6390`
            MyBase.New(29 + Math.Max(1, Math.Min(4, piel.Canales)), nombre)
            _bufferDeEntrada = bufferDeEntrada
            _trianguloDesdeMalla = trianguloDesdeMalla
            _subconjunto = subconjunto
            _escalaDeNormal = escalaDeNormal
            _piel = piel
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _piel Is Nothing Then Return
            Dim entrada = Buffers.Real(ctx.Buffers, _bufferDeEntrada)
            Dim salida = Buffers.Real(ctx.Buffers, _piel.BufferDeSalida)
            If entrada Is Nothing OrElse salida Is Nothing Then Return
            Piel.Deformar(_piel, salida,
                          MallaAMalla.Compuestas(entrada, salida, _trianguloDesdeMalla,
                                                 _subconjunto, _escalaDeNormal))
        End Sub

    End Class

    ''' <summary>`hclBoneSpaceMeshMeshDeform*Operator` — types 26 a 29.
    ''' <para>⛔ Esta familia NO declara `triangleFromMeshTransforms`: su compuesta es sólo
    ''' `Componer(marco[t], M)`.</para></summary>
    Friend NotInheritable Class OpMallaAMallaEnEspacioDeHueso
        Inherits OperadorCompilado

        Private ReadOnly _bufferDeEntrada As Integer
        Private ReadOnly _subconjunto As Integer()
        Private ReadOnly _escalaDeNormal As Integer
        Private ReadOnly _piel As PielDeHuesoCompilada

        Friend Sub New(bufferDeEntrada As Integer, subconjunto As Integer(),
                       escalaDeNormal As Integer, piel As PielDeHuesoCompilada, nombre As String)
            ' 26 = P, 27 = PN, 28 = PNT, 29 = PNTB
            MyBase.New(25 + Math.Max(1, Math.Min(4, piel.Canales)), nombre)
            _bufferDeEntrada = bufferDeEntrada
            _subconjunto = subconjunto
            _escalaDeNormal = escalaDeNormal
            _piel = piel
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _piel Is Nothing Then Return
            Dim entrada = Buffers.Real(ctx.Buffers, _bufferDeEntrada)
            Dim salida = Buffers.Real(ctx.Buffers, _piel.BufferDeSalida)
            If entrada Is Nothing OrElse salida Is Nothing Then Return
            OpPielDeHueso.Deformar(_piel, salida,
                                   MallaAMalla.Compuestas(entrada, salida, Nothing,
                                                          _subconjunto, _escalaDeNormal))
        End Sub

    End Class

    ''' <summary>Lo único propio de las dos familias: los marcos y las matrices compuestas.</summary>
    Friend Module MallaAMalla

        ''' <summary>`scaleNormalBehaviour` — el eje que decide cómo sale la normal del marco.</summary>
        Friend Const NormalNormalizada As Integer = 0
        Friend Const NormalPorReciproco As Integer = 2

        ''' <summary>
        ''' La cadena entera de `0x141A04B60` + `0x141A0EEB0`.
        ''' <para>```
        ''' M          = Componer(entrada.AEspacioDeSim, salida.DesdeEspacioDeSim)
        ''' marco[t]   = { p0−c, p1−c, escala(n), (c,1) }   con c = (p0+p1+p2)/3
        ''' scratch[t] = Componer( Componer(trianguloDesdeMalla[t], marco[t]), M )
        ''' ```</para>
        ''' <para>⛔ Sin `trianguloDesdeMalla` (la familia de espacio de HUESO no lo declara) la
        ''' primera composición no existe: `scratch[t] = Componer(marco[t], M)`.</para>
        ''' </summary>
        Friend Function Compuestas(entrada As Buffer, salida As Buffer,
                                   trianguloDesdeMalla As Mat4(), subconjunto As Integer(),
                                   escalaDeNormal As Integer) As Mat4()
            ' (1) la matriz de buffer (`0x141A04BEE`-`0x141A04CCA`)
            Dim m = Operadores.Componer(entrada.AEspacioDeSimulacion, salida.DesdeEspacioDeSimulacion)

            ' (2) los marcos, con la normal escalada segun `scaleNormalBehaviour`
            Dim marcos = MarcosConEscala(entrada, escalaDeNormal)
            Dim nb = If(trianguloDesdeMalla Is Nothing, 0, trianguloDesdeMalla.Length)

            ' (3) las dos composiciones (`0x141A0EEB0`, sus dos fases)
            If subconjunto IsNot Nothing AndAlso subconjunto.Length > 0 Then
                Dim n = subconjunto.Length
                Dim r(n - 1) As Mat4
                For k = 0 To n - 1
                    Dim t = subconjunto(k)
                    Dim marco = If(t >= 0 AndAlso t < marcos.Length, marcos(t), Mat4.Identidad)
                    Dim uno = If(k < nb, Operadores.Componer(trianguloDesdeMalla(k), marco), marco)
                    r(k) = Operadores.Componer(uno, m)
                Next
                Return r
            End If

            Dim cuantos = If(nb = 0, marcos.Length, Math.Min(nb, marcos.Length))
            Dim q(Math.Max(0, cuantos - 1)) As Mat4
            For t = 0 To cuantos - 1
                Dim uno = If(t < nb, Operadores.Componer(trianguloDesdeMalla(t), marcos(t)), marcos(t))
                q(t) = Operadores.Componer(uno, m)
            Next
            Return q
        End Function

        ''' <summary>
        ''' Los marcos de triángulo con la normal escalada según `scaleNormalBehaviour`
        ''' (`0x141A04B9B`-`0x141A04BB2`, tres kernels y tres leyes MEDIDAS).
        ''' <para>· `0` → `0x141A051C0`, normalizada con el `rsqrt` CRUDO y la guarda de norma nula</para>
        ''' <para>· `2` → `0x141A054D0`, escalada por el RECÍPROCO</para>
        ''' <para>· otro → `0x141A04F20`, CRUDA</para>
        ''' </summary>
        Friend Function MarcosConEscala(entrada As Buffer, escalaDeNormal As Integer) As Mat4()
            Dim r = OpDeformarHuesos.MarcosDeTriangulo(entrada)
            If escalaDeNormal = NormalNormalizada Then
                For t = 0 To r.Length - 1
                    r(t).F2 = Simd.NormalizarRsqrtCrudoConGuarda(r(t).F2)
                Next
            ElseIf escalaDeNormal = NormalPorReciproco Then
                For t = 0 To r.Length - 1
                    r(t).F2 = Vector128.Multiply(r(t).F2, Simd.RcpNewton(Simd.Dot3(r(t).F2, r(t).F2)))
                Next
            End If
            Return r
        End Function

    End Module

End Namespace

