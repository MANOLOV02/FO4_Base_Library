Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclBoneSpaceSkin*Operator` — types **18 a 21** (`P`, `PN`, `PNT`, `PNTB`).
'
' La tabla de salto de `0x1418C6390` manda las cuatro a `0x1418C60B5`, que resuelve
'     salida = buffers[ buffers[ op.outputBufferIndex (+0x30) ].Ranura ]      ⬅ doble indirección
'     ts     = transformSets[ op.transformSetIndex (+0x34) ]                  ⬅ directa
' y salta a `0x141920EA0` (P), `0x141923BE0` (PN), `0x141928760` (PNT) o `0x14192F6A0` (PNTB).
'
' Cada una hace DOS cosas:
'   1. `TtPrepare Matrices` — `0x141926FE0` / `0x141927380` / `0x141927720` según los dos bits de
'      layout del buffer, y otras tres para la variante DESEMPAQUETADA (`+0xA0` en vez de `+0x90`).
'      Compone las matrices de hueso en un scratch de 64 B cada una; el conteo es
'      `transformSubset.count` (+0x28) o, si es 0, `transformSet.numTransforms`.
'   2. el DEFORM — `0x141924EB0` («TtBone Space Deformer», y `0x141923CF0` la otra entrada de la
'      misma familia), con `(boneSpaceDeformer (+0x38), bloques locales, scratch, salida)`.
'
' ⭐⭐ LA LEY DEL DEFORM (familia de UNA influencia en `0x141924FFA`-`0x1419250BC`, la de VARIAS en
' `0x1419254D2`-…):
'
'     acc_p = 0 ; acc_n = 0
'     por cada influencia k del vértice:
'         p  = localPosition[k]                        ' float4 CRUDO, 16 B — avanza 0x10
'         nq = localNormal[k]                          ' cuatro int16 — avanza 8
'         n  = float(nq << 16) · bitcast(nq.w << 16)   ' 0x141925510/14/19/26
'         M  = matrices[ boneIndices[k] ]              ' `uint16`, ×64
'         acc_p += p.x·M0 + p.y·M1 + p.z·M2 + p.w·M3   ' CUATRO términos, 0x14192553E/33/45/50
'         acc_n += n.x·M0 + n.y·M1 + n.z·M2            ' TRES,          0x14192555B/68/70
'     posiciones[v] = acc_p     ' 16 B (`movups`), stride `+0x1C` sobre `+0x10`
'     normales[v]   = acc_n     ' 12 B (`movsd` + `movss`), stride `+0x44` sobre `+0x38`
'
' ⭐⭐ **EL PESO NO TIENE ARRAY PROPIO: VIENE HORNEADO EN `localPosition`.** Por eso los bloques de
' entrada (`{vertexIndices, boneIndices}`) no traen pesos y la fila 3 entra multiplicada por `p.w`.
' Suponer un peso aparte, o tratar `p` como un punto con `w = 1`, da otra malla.
'
' ⛔ La desquantización de la normal es la MISMA que la de espacio-objeto (`Piel.Desquantizar`):
' `punpcklwd` contra cero y la escala en la cuarta lane SIN `cvtdq2ps`.
'
' ⛔ Las cuatro familias sólo cambian cuántas influencias tiene el vértice y cómo se empaquetan
' (tabla de layout): `four` → 4 vértices y 16 huesos · `three` → 5 y 15 · `two` → 8 y 16 ·
' `one` → 16 y 16. El bloque local tiene 16 casillas `(float4, int16[4])` y se consumen EN ORDEN,
' agrupadas por vértice: el vértice `i` se lleva sus `n` influencias seguidas (`0x1419255B6`).
'
' ⭐⭐ LA TANGENTE Y LA BITANGENTE — el hueco que este archivo declaraba está CERRADO, y no por
' analogía: el bucle está leído. `hclBoneSpaceSkinPNTOperator::execute` (`0x141928760`) despacha
' OCHO kernels por los tres bits de layout del buffer y todos terminan en el mismo
' «TtBone Space Deformer» (`0x1419288A0` para PNT, `0x14192F820` para PNTB). En `0x141928C10`-
' `0x141928D58`:
'
'     t = desquantizar(localTangent[k])          ' 0x141928C53/65/6F — la MISMA ley que la normal
'     accT += t.x·M0 + t.y·M1 + t.z·M2           ' 0x141928C86/8E/98/9C/9F/A3, TRES terminos
'     tangentes[v] = accT                        ' [buf+0x50] + byte[buf+0x5C]·v, 0x141928D49/4D/52
'
' y el kernel de PNTB (`0x14192F820`) toca los CUATRO canales: `0x14192FA0B` (+0x1C/+0x10),
' `0x14192FB12` (+0x44/+0x38), `0x14192FB49` (+0x5C/+0x50) y `0x14192FB61` (+0x74/+0x68).
'
' Los canales locales salen de la reflexión, no de la simetría: `hclBoneSpaceDeformerLocalBlockPNT`
' es `{localPosition vector4[16] @0, localNormal int16[64] @0x100, localTangent int16[64] @0x180}`,
' y el `PNTB` agrega `localBiTangent int16[64] @0x200`.
'
' ⚠️ CERO apariciones en el corpus vanilla. Va igual: es de la lista cerrada.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>Una piel de ESPACIO DE HUESO ya compilada: por vértice, sus influencias con la
    ''' posición local (con el peso horneado) y la normal local desquantizada.</summary>
    Friend NotInheritable Class PielDeHuesoCompilada

        ''' <summary>`outputBufferIndex` (+0x30).</summary>
        Friend ReadOnly BufferDeSalida As Integer

        ''' <summary>`transformSetIndex` (+0x34).</summary>
        Friend ReadOnly IndiceDelTransformSet As Integer

        ''' <summary>`transformSubset` (+0x20) — vacío: se preparan todas las del set.</summary>
        Friend ReadOnly Subconjunto As Integer()

        ''' <summary>El índice de vértice de cada entrada.</summary>
        Friend ReadOnly Vertice As Integer()

        ''' <summary>Los huesos de cada entrada, `MaxInfluencias` por vértice; `−1` = casilla vacía.</summary>
        Friend ReadOnly Huesos As Integer()

        ''' <summary>Cuántas influencias tiene de verdad cada vértice.</summary>
        Friend ReadOnly Cuantas As Integer()

        ''' <summary>La posición local, 4 floats por (vértice × influencia). ⛔ CRUDA: su `w` trae el
        ''' PESO y multiplica la fila 3 de la matriz.</summary>
        Friend ReadOnly PosLocal As Single()

        ''' <summary>La normal local ya desquantizada, 4 floats por (vértice × influencia).</summary>
        Friend ReadOnly NrmLocal As Single()

        ''' <summary>La TANGENTE local desquantizada, ídem — `localTangent`, `int16[64]` en
        ''' `+0x180` del bloque PNT/PNTB.</summary>
        Friend ReadOnly TanLocal As Single()

        ''' <summary>La BITANGENTE local desquantizada — `localBiTangent`, `int16[64]` en `+0x200`
        ''' del bloque PNTB.</summary>
        Friend ReadOnly BitLocal As Single()

        ''' <summary>1 = P, 2 = PN, 3 = PNT, 4 = PNTB — los types 18 a 21.</summary>
        Friend ReadOnly Canales As Integer

        Friend Const MaxInfluencias As Integer = 4

        Friend ReadOnly Cuenta As Integer

        Friend Sub New(bufferDeSalida As Integer, indiceDelTransformSet As Integer,
                       subconjunto As Integer(), vertice As Integer(), huesos As Integer(),
                       cuantas As Integer(), posLocal As Single(), nrmLocal As Single(),
                       canales As Integer, Optional tanLocal As Single() = Nothing,
                       Optional bitLocal As Single() = Nothing)
            Me.BufferDeSalida = bufferDeSalida
            Me.IndiceDelTransformSet = indiceDelTransformSet
            Me.Subconjunto = subconjunto
            Me.Vertice = vertice
            Me.Huesos = huesos
            Me.Cuantas = cuantas
            Me.PosLocal = posLocal
            Me.NrmLocal = nrmLocal
            Me.TanLocal = tanLocal
            Me.BitLocal = bitLocal
            Me.Canales = canales
            Me.Cuenta = If(vertice Is Nothing, 0, vertice.Length)
        End Sub

    End Class

    ''' <summary>`hclBoneSpaceSkin*Operator` — types 18 a 21.</summary>
    Friend NotInheritable Class OpPielDeHueso
        Inherits OperadorCompilado

        Friend ReadOnly Piel As PielDeHuesoCompilada

        Friend Sub New(piel As PielDeHuesoCompilada, nombre As String)
            ' 18 = P, 19 = PN, 20 = PNT, 21 = PNTB — la tabla de `0x1418C6390`
            MyBase.New(17 + Math.Max(1, Math.Min(4, piel.Canales)), nombre)
            Me.Piel = piel
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            Dim p = Piel
            If p Is Nothing OrElse p.Cuenta = 0 Then Return
            If ctx.TransformSets Is Nothing Then Return
            If p.IndiceDelTransformSet < 0 OrElse
               p.IndiceDelTransformSet >= ctx.TransformSets.Length Then Return
            Dim ts = ctx.TransformSets(p.IndiceDelTransformSet)
            If ts Is Nothing Then Return
            Dim salida = Buffers.Real(ctx.Buffers, p.BufferDeSalida)
            If salida Is Nothing Then Return
            Deformar(p, salida, Matrices(p, ts))
        End Sub

        ''' <summary>`TtPrepare Matrices` — con subconjunto, una entrada POR POSICIÓN; sin él, las
        ''' del transformSet en su orden (`0x141927720` y hermanas).</summary>
        Friend Shared Function Matrices(p As PielDeHuesoCompilada, ts As Mat4()) As Mat4()
            If p.Subconjunto Is Nothing OrElse p.Subconjunto.Length = 0 Then Return ts
            Dim n = p.Subconjunto.Length
            Dim r(n - 1) As Mat4
            For k = 0 To n - 1
                Dim b = p.Subconjunto(k)
                r(k) = If(b >= 0 AndAlso b < ts.Length, ts(b), Mat4.Identidad)
            Next
            Return r
        End Function

        ''' <summary>
        ''' El deform de espacio de hueso — `0x141924EB0` / `0x141923CF0`.
        ''' <para>⛔ Es `Shared` porque lo comparte `hclBoneSpaceMeshMeshDeform` (types 26-29), que
        ''' llama al MISMO kernel con las matrices salidas de los marcos de triángulo en vez de los
        ''' huesos. Dos copias de esta aritmética podrían divergir sin que nada lo viera.</para>
        ''' </summary>
        Friend Shared Sub Deformar(p As PielDeHuesoCompilada, salida As Buffer, m As Mat4())
            If p Is Nothing OrElse salida Is Nothing OrElse m Is Nothing Then Return
            For i = 0 To p.Cuenta - 1
                Dim v = p.Vertice(i)
                If v < 0 OrElse v >= salida.Cuenta Then Continue For
                Dim accP = Vector128(Of Single).Zero
                Dim accN = Vector128(Of Single).Zero
                Dim accT = Vector128(Of Single).Zero
                Dim accB = Vector128(Of Single).Zero
                Dim cuantas = If(p.Cuantas Is Nothing, PielDeHuesoCompilada.MaxInfluencias, p.Cuantas(i))
                For k = 0 To Math.Min(cuantas, PielDeHuesoCompilada.MaxInfluencias) - 1
                    Dim idx = i * PielDeHuesoCompilada.MaxInfluencias + k
                    If idx >= p.Huesos.Length Then Exit For
                    Dim b = p.Huesos(idx)
                    If b < 0 OrElse b >= m.Length Then Continue For
                    Dim mm = m(b)
                    ' ⛔ CUATRO terminos: la `w` de la posicion local multiplica la fila 3, y ahi es
                    ' donde viene el PESO (`0x14192553E`/`33`/`45`/`50`).
                    accP = Vector128.Add(accP, Operadores.FilaPorMarco(Simd.Leer(p.PosLocal, idx), mm))
                    If Canal(p.NrmLocal, idx) Then
                        ' ⛔ TRES terminos: la normal es DIRECCION (`0x14192555B`/`68`/`70`)
                        accN = Vector128.Add(accN,
                            Operadores.TransformarDireccion(Simd.Leer(p.NrmLocal, idx), mm))
                    End If
                    ' ⛔ la tangente y la bitangente van con la MISMA forma de tres terminos:
                    ' `0x141928C53`-`0x141928CC5` desquantiza la tangente igual que la normal y la
                    ' transforma con las mismas tres filas.
                    If Canal(p.TanLocal, idx) Then
                        accT = Vector128.Add(accT,
                            Operadores.TransformarDireccion(Simd.Leer(p.TanLocal, idx), mm))
                    End If
                    If Canal(p.BitLocal, idx) Then
                        accB = Vector128.Add(accB,
                            Operadores.TransformarDireccion(Simd.Leer(p.BitLocal, idx), mm))
                    End If
                Next
                salida.SetVertice(v, accP)                       ' 0x141928D1D/22, 12 B
                If p.Canales >= 2 AndAlso salida.Normales IsNot Nothing AndAlso
                   p.NrmLocal IsNot Nothing AndAlso p.NrmLocal.Length > 0 Then
                    salida.SetNormal(v, accN)                    ' 0x141928D35/3A
                End If
                If p.Canales >= 3 AndAlso salida.Tangentes IsNot Nothing AndAlso
                   p.TanLocal IsNot Nothing AndAlso p.TanLocal.Length > 0 Then
                    salida.SetTangente(v, accT)                  ' 0x141928D4D/52
                End If
                If p.Canales >= 4 AndAlso salida.Bitangentes IsNot Nothing AndAlso
                   p.BitLocal IsNot Nothing AndAlso p.BitLocal.Length > 0 Then
                    salida.SetBitangente(v, accB)                ' 0x14192FB69, la variante PNTB
                End If
            Next
        End Sub

        ''' <summary>`True` si el canal local trae el vector `idx`.</summary>
        Private Shared Function Canal(a As Single(), idx As Integer) As Boolean
            Return a IsNot Nothing AndAlso idx * 4 + 3 < a.Length
        End Function

    End Class

End Namespace

