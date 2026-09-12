Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclMeshBoneDeformOperator` — type **16**, `0x14195AA90`.
'
' ⛔ NO es `hclSimpleMeshBoneDeformOperator` (type **17**, `0x14195B320`). Son dos clases y dos
' entradas distintas de la tabla de salto de `0x1418C6390`, y el motor le da al 16 lo que al 17 no:
' **un hueso puede colgar de VARIOS triángulos, con peso**.
'
' Los buffers, en `0x1418C62CF`-`0x1418C62FD`:
'     entrada  = buffers[ buffers[ op.inputBufferIdx (+0x20) ].Ranura ]     ⬅ doble indirección
'     salida   = transformSets[ op.outputTransformSetIdx (+0x24) ]          ⬅ directa
'
' Los datos (tabla de layout):
'     triangleBonePairs        +0x28, array de `0x50` B:
'                                  { localBoneTransform matrix4 @0x00,
'                                    weight             real    @0x40,
'                                    triangleIndex      uint16  @0x44 }
'     triangleBoneStartForBone +0x38, array de `uint16`: el hueso `b` usa los pares
'                              `[start[b], start[b+1])`  (`0x14195ABD7`/`DB`/`E0`)
'
' ⭐ FASE 1 — los MARCOS DE TRIÁNGULO, todos de una (`0x14195B030`), 64 B cada uno:
'     c = (p0 + p1 + p2) · 1/3      ' 0x14195B0CE, la MISMA constante `0x142F3C660` del type 17
'     a = p0 − c ; b = p1 − c ; n = a × b   (CRUDO, sin normalizar)
'     marco = { a, b, n, c }                ' 0x14195B0D5/0x14195B114
'
' ⭐ FASE 2 — el BLEND por hueso (`0x14195AC20`-`0x14195AD2A`):
'     acc = 0
'     por cada par p del hueso:
'         m    = Componer(localBoneTransform[p], marco[triangleIndex[p]])
'         acc += weight[p] · m               ' 0x14195ACE5/EA + 0x14195ACF3/F6/FC/0A, y el
'                                            ' `acc += m` es `0x141539840` (cuatro `addps`)
'
' ⛔⛔ LA COMPOSICIÓN ES DE **TRES TÉRMINOS** para las filas y `TransformarPunto` para la
' traslación (`0x14195AC78`-`0x14195AC8E`: `mulps` por F0/F1/F2 y un solo `addps [rax+0x30]`, en el
' sitio del término en `x`). O sea `Operadores.Componer` — **no** el `FilaPorMarco` de CUATRO
' términos que usa el type 17, que además multiplica por la `w` de la fila. Son dos leyes distintas
' y se transcriben distintas.
'
' ⭐ FASE 3 — el CIERRE, igual que el type 17 (`0x14195AD30`-`0x14195ADC1`): la fila 3 se escribe
' tal cual (`0x14195AD6D`) y las tres filas se rehacen con dos productos cruzados y se normalizan.
'
' ⚠️ CERO apariciones en el corpus vanilla. Va igual: es de la lista cerrada.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>`hclMeshBoneDeformOperator` — type 16, `0x14195AA90`.</summary>
    Friend NotInheritable Class OpDeformarHuesos
        Inherits OperadorCompilado

        Private ReadOnly _bufferDeEntrada As Integer
        Friend ReadOnly TransformSetDeSalida As Integer
        Private ReadOnly _transformLocal As Mat4()
        Private ReadOnly _peso As Single()
        Private ReadOnly _triangulo As Integer()
        Private ReadOnly _arranquePorHueso As Integer()

        ''' <summary>Los huesos a los que este deform ESCRIBE — los que tienen al menos un par.
        ''' <para>⛔ Los demás no los toca, igual que en el type 17: escribirles la identidad sería
        ''' inventar una capa de física que el motor no pone.</para></summary>
        Friend ReadOnly HuesosQueEscribe As Integer()

        Friend Sub New(bufferDeEntrada As Integer, transformSetDeSalida As Integer,
                       transformLocal As Mat4(), peso As Single(), triangulo As Integer(),
                       arranquePorHueso As Integer(), nombre As String)
            MyBase.New(16, nombre)
            _bufferDeEntrada = bufferDeEntrada
            Me.TransformSetDeSalida = transformSetDeSalida
            _transformLocal = transformLocal
            _peso = peso
            _triangulo = triangulo
            _arranquePorHueso = arranquePorHueso
            Dim vistos As New List(Of Integer)()
            If arranquePorHueso IsNot Nothing Then
                For b = 0 To arranquePorHueso.Length - 2
                    If arranquePorHueso(b + 1) > arranquePorHueso(b) Then vistos.Add(b)
                Next
            End If
            HuesosQueEscribe = vistos.ToArray()
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _arranquePorHueso Is Nothing OrElse _arranquePorHueso.Length < 2 Then Return
            If ctx.TransformSets Is Nothing Then Return
            If TransformSetDeSalida < 0 OrElse TransformSetDeSalida >= ctx.TransformSets.Length Then Return
            Dim buf = Buffers.Real(ctx.Buffers, _bufferDeEntrada)
            If buf Is Nothing OrElse buf.IndicesDeTriangulo Is Nothing Then Return
            Dim salida = ctx.TransformSets(TransformSetDeSalida)
            If salida Is Nothing Then Return

            ' ---- fase 1: los marcos, todos de una (`0x14195B030`)
            Dim marcos = MarcosDeTriangulo(buf)

            ' ---- fase 2: el blend por hueso
            For b = 0 To _arranquePorHueso.Length - 2
                Dim desde = _arranquePorHueso(b), hasta = _arranquePorHueso(b + 1)
                If hasta <= desde Then Continue For
                If b >= salida.Length Then Continue For

                Dim acc As Mat4
                acc.F0 = Vector128(Of Single).Zero
                acc.F1 = Vector128(Of Single).Zero
                acc.F2 = Vector128(Of Single).Zero
                acc.F3 = Vector128(Of Single).Zero
                For p = desde To hasta - 1
                    If p < 0 OrElse p >= _triangulo.Length Then Continue For
                    Dim t = _triangulo(p)
                    If t < 0 OrElse t >= marcos.Length Then Continue For
                    ' ⛔ TRES terminos por fila y `TransformarPunto` para la traslacion
                    Dim m = Operadores.Componer(_transformLocal(p), marcos(t))
                    Dim w = Vector128.Create(_peso(p))                  ' 0x14195ACE5/EA
                    acc.F0 = Vector128.Add(acc.F0, Vector128.Multiply(m.F0, w))
                    acc.F1 = Vector128.Add(acc.F1, Vector128.Multiply(m.F1, w))
                    acc.F2 = Vector128.Add(acc.F2, Vector128.Multiply(m.F2, w))
                    acc.F3 = Vector128.Add(acc.F3, Vector128.Multiply(m.F3, w))
                Next

                ' ---- fase 3: el cierre, el mismo del type 17
                Dim u = Polar.Cruz(acc.F2, acc.F0)
                Dim vv = Polar.Cruz(u, acc.F2)
                Dim r As Mat4
                r.F0 = Simd.NormalizarCrudo(vv).WithElement(Simd.LaneW, 0.0F)
                r.F1 = Simd.NormalizarCrudo(u).WithElement(Simd.LaneW, 0.0F)
                r.F2 = Simd.NormalizarCrudo(acc.F2).WithElement(Simd.LaneW, 0.0F)
                r.F3 = acc.F3.WithElement(Simd.LaneW, 1.0F)             ' 0x14195AD6D, tal cual
                salida(b) = r
            Next
        End Sub

        ''' <summary>
        ''' Los marcos de triángulo — `0x14195B030`, con la misma `1/3` de `0x142F3C660` que el
        ''' type 17.
        ''' <para>⛔ Es `Friend` porque lo comparten las DOS familias de mesh-mesh: su
        ''' `0x1419FD0E0` arma exactamente este marco, con la misma constante. Dos copias
        ''' podrían divergir sin que nada lo viera.</para>
        ''' </summary>
        Friend Shared Function MarcosDeTriangulo(buf As Buffer) As Mat4()
            Dim n = Math.Max(0, buf.NumTriangulos)
            Dim r(Math.Max(0, n - 1)) As Mat4
            Dim unTercio = Vector128.Create(0.333333343F)               ' 0x142F3C660
            For t = 0 To n - 1
                Dim i0 = CInt(buf.IndicesDeTriangulo(t * 3))
                Dim i1 = CInt(buf.IndicesDeTriangulo(t * 3 + 1))
                Dim i2 = CInt(buf.IndicesDeTriangulo(t * 3 + 2))
                If i0 >= buf.Cuenta OrElse i1 >= buf.Cuenta OrElse i2 >= buf.Cuenta Then Continue For
                Dim p0 = buf.Vertice(i0), p1 = buf.Vertice(i1), p2 = buf.Vertice(i2)
                Dim c = Vector128.Multiply(Vector128.Add(Vector128.Add(p0, p1), p2), unTercio)
                Dim a = Vector128.Subtract(p0, c)                       ' 0x14195B0DA
                Dim b = Vector128.Subtract(p1, c)                       ' 0x14195B0DD
                r(t).F0 = a
                r(t).F1 = b
                r(t).F2 = Polar.Cruz(a, b)                              ' 0x14195B0E3…0x14195B114
                r(t).F3 = c.WithElement(Simd.LaneW, 1.0F)               ' 0x14195B0D5
            Next
            Return r
        End Function

    End Class

End Namespace


