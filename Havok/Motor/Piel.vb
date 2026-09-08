Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' EL OPERADOR DE PIEL — `hclObjectSpaceSkinPNOperator` (type 23), `0x14193BFE0`.
'
' Es el operador que ABRE la cadena en las 762 apariciones del corpus vanilla: pone la malla de
' simulación en la pose del cuadro, y de ahí salen tanto las partículas (vía `MoveParticles`) como
' el buffer de referencia contra el que mide `hclLocalRangeConstraintSet`.
'
' Dos fases (RE cap. 15.4):
'
'   (a) `TtPrepare Matrices` — `0x141A0EEB0` (sin subconjunto) / `0x141A10F00` (con
'       `op.transformSubset`, +0x30): un scratch de 64 B por hueso con
'       `boneFromSkinMeshTransforms[b] (+0x20) × transformSet.transforms[b]`.
'
'   (b) El deform — `0x14193DC90`: los vértices van en BLOQUES DE 16 y `controlBytes` (+0x40) dice,
'       por bloque, de cuál de las cuatro listas sale — cuatro, tres, dos o una influencia.
'
' ⛔⛔ LA DESQUANTIZACIÓN NO ES UNA DIVISIÓN POR 32767. El motor mete el `int16` en la mitad ALTA de
' un `int32` (`punpcklwd` contra cero), lo convierte con `cvtdq2ps`, y lo multiplica por una escala
' que sale de la CUARTA lane **reinterpretada como float** (`pshufd 0xFF`, sin conversión):
'
'     localPos_i = float(int16_i << 16) × bitcast_float(int16_3 << 16)      ' i = 0,1,2
'
' Escribir la desquantización «normal» (`v / 32767 × escala`) da otra malla.
'
' ⛔ Los pesos son `uint8` y la constante es `0,00392156886` (`0x142492850`), que es 1/255 redondeado
' a float — no `1/255` calculado en doble y truncado.
'
' ⛔ La lista de UNA influencia **no trae pesos**: el peso es 1 y el motor ni lee el array.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>
    ''' Un `hclObjectSpaceSkinPNOperator` ya compilado: los bloques resueltos a arreglos planos, una
    ''' sola vez.
    ''' <para>⛔ Igual que <see cref="SetCompilado"/>: el motor tiene esto contiguo y lo recorre con
    ''' `movzx`/`movss`. Bajar al packfile por vértice y por cuadro es otro orden de costo.</para>
    ''' </summary>
    Friend NotInheritable Class PielCompilada

        ''' <summary>`op.outputBufferIndex` (+0x40). El buffer al que escribe, con la doble
        ''' indirección de `Buffers.Real` (`0x1418C6134`).</summary>
        Friend ReadOnly BufferDeSalida As Integer

        ''' <summary>`op.transformSetIndex` (+0x44).</summary>
        Friend ReadOnly IndiceDelTransformSet As Integer

        ''' <summary>`op.boneFromSkinMeshTransforms` (+0x20), una matriz por hueso.</summary>
        Friend ReadOnly BoneDesdeMalla As Mat4()

        ''' <summary>`op.transformSubset` (+0x30). Vacío = todos los huesos.</summary>
        Friend ReadOnly Subconjunto As Integer()

        ''' <summary>El índice de vértice de cada entrada.</summary>
        Friend ReadOnly Vertice As Integer()

        ''' <summary>Los huesos de cada entrada, `MaxInfluencias` por vértice.</summary>
        Friend ReadOnly Huesos As Integer()

        ''' <summary>Los pesos ya en float, `MaxInfluencias` por vértice. Cero = influencia que no
        ''' está (el motor no la lee; acá suma cero, que es lo mismo).</summary>
        Friend ReadOnly Pesos As Single()

        ''' <summary>La posición local desquantizada, 4 floats por vértice.</summary>
        Friend ReadOnly PosLocal As Single()

        ''' <summary>La normal local desquantizada, 4 floats por vértice. Vacía en la variante `P`,
        ''' que no la trae.</summary>
        Friend ReadOnly NrmLocal As Single()

        ''' <summary>La tangente local desquantizada, 4 floats por vértice. Vacía salvo en `PNT` y
        ''' `PNTB`.</summary>
        Friend ReadOnly TanLocal As Single()

        ''' <summary>La bitangente local desquantizada. Vacía salvo en `PNTB`.</summary>
        Friend ReadOnly BitLocal As Single()

        ''' <summary>
        ''' Qué canales trae el bloque: 1 = P, 2 = PN, 3 = PNT, 4 = PNTB. Es lo único que separa a
        ''' las cuatro variantes del operador (tipos 22 a 25).
        ''' <para>⭐ Los cuatro se deforman. El destino de la tangente y la bitangente en el buffer
        ''' vivo ya no es una suposición: `+0x50`/`+0x5C` y `+0x68`/`+0x74`, leídos en
        ''' `hclSkinOperator::TtSkin` (`0x14190A310`-`0x14190A32C`), en las escrituras del kernel de
        ''' `PNT` (`0x141941985`-`0x141941998`) y en las de `PNTB` (`0x141948FD8`-`0x141949043`).</para>
        ''' </summary>
        Friend ReadOnly Canales As Integer

        ''' <summary>Cuántas influencias caben por vértice en los arreglos planos. Es 4 porque la
        ''' lista más gorda del deformer es `fourBlendEntries`; las de 3, 2 y 1 dejan el resto en
        ''' peso cero.</summary>
        Friend Const MaxInfluencias As Integer = 4

        Friend ReadOnly Cuenta As Integer

        Friend Sub New(bufferDeSalida As Integer, indiceDelTransformSet As Integer,
                       boneDesdeMalla As Mat4(), subconjunto As Integer(),
                       vertice As Integer(), huesos As Integer(), pesos As Single(),
                       posLocal As Single(), nrmLocal As Single(),
                       Optional canales As Integer = 2,
                       Optional tanLocal As Single() = Nothing,
                       Optional bitLocal As Single() = Nothing)
            Me.BufferDeSalida = bufferDeSalida
            Me.IndiceDelTransformSet = indiceDelTransformSet
            Me.BoneDesdeMalla = boneDesdeMalla
            Me.Subconjunto = subconjunto
            Me.Vertice = vertice
            Me.Huesos = huesos
            Me.Pesos = pesos
            Me.PosLocal = posLocal
            Me.NrmLocal = nrmLocal
            Me.TanLocal = If(tanLocal, Array.Empty(Of Single)())
            Me.BitLocal = If(bitLocal, Array.Empty(Of Single)())
            Me.Cuenta = If(vertice Is Nothing, 0, vertice.Length)
            Me.Canales = canales
        End Sub


    End Class

    Friend Module Piel

        ''' <summary>`0,00392156886` — `0x142492850`. El `1/255` **del motor**, redondeado a float;
        ''' no el resultado de dividir en doble.</summary>
        Friend ReadOnly PorPeso As Single = 0.00392156886F

        ''' <summary>
        ''' La desquantización de un `int16` de posición o normal — la mitad ALTA de un `int32`,
        ''' convertida, por una escala que es la cuarta lane REINTERPRETADA como float.
        ''' <para>`punpcklwd` contra cero + `cvtdq2ps` para las tres primeras lanes; `pshufd 0xFF`
        ''' para la cuarta, que NO pasa por `cvtdq2ps` (`0x14193DD10` y siguientes).</para>
        ''' </summary>
        Friend Function Desquantizar(v0 As Short, v1 As Short, v2 As Short, v3 As Short) As Vector128(Of Single)
            ' ⛔ la escala se REINTERPRETA, no se convierte: es un float armado con el int16 en la
            ' mitad alta y ceros abajo.
            Dim esc = BitConverter.Int32BitsToSingle(CInt(v3) << 16)
            Dim c = Vector128.Create(CSng(CInt(v0) << 16), CSng(CInt(v1) << 16),
                                     CSng(CInt(v2) << 16), 0.0F)
            Return Vector128.Multiply(c, Vector128.Create(esc))
        End Function

        ''' <summary>
        ''' (a) `TtPrepare Matrices` — `0x141A0EEB0` / `0x141A10F00`.
        ''' <para>`M[b] = boneFromSkinMeshTransforms[b] × transformSet.transforms[b]`. Con
        ''' `op.transformSubset` no vacío sólo se preparan esos huesos; los demás quedan como estén
        ''' (el motor no los toca, y el deformer tampoco los nombra).</para>
        ''' </summary>
        Friend Function PrepararMatrices(p As PielCompilada, ts As Mat4()) As Mat4()
            Dim nb = If(p.BoneDesdeMalla Is Nothing, 0, p.BoneDesdeMalla.Length)
            If nb = 0 Then Return Array.Empty(Of Mat4)()

            If p.Subconjunto IsNot Nothing AndAlso p.Subconjunto.Length > 0 Then
                ' ⭐⭐ 0x141A10F00 — CON SUBCONJUNTO: el scratch tiene `subset.count` entradas
                ' (`ebx = op.transformSubset.count`, `0x14193BFFC`…`0x14193C00F`) y se indexa POR
                ' POSICION en el subconjunto; `subset[k]` elige el hueso del transformSet.
                '   scratch[k] = boneFromSkinMeshTransforms[k] × transforms[ subset[k] ]
                ' ⛔ Los `boneIndices` del deformer indexan ESTE arreglo, no el transformSet.
                Dim n = p.Subconjunto.Length
                Dim r(n - 1) As Mat4
                For k = 0 To n - 1
                    Dim bfm = If(k < nb, p.BoneDesdeMalla(k), Mat4.Identidad)
                    Dim b = p.Subconjunto(k)
                    If ts Is Nothing OrElse b < 0 OrElse b >= ts.Length Then
                        r(k) = bfm
                    Else
                        r(k) = Operadores.Componer(bfm, ts(b))
                    End If
                Next
                Return r
            End If

            ' 0x141A0EEB0 — SIN subconjunto: una entrada por hueso del transformSet
            ' (`ebx = transformSet.numTransforms`, `0x14193C011`), en el mismo orden.
            Dim m = If(ts Is Nothing, nb, Math.Min(nb, ts.Length))
            Dim q(Math.Max(0, m - 1)) As Mat4
            For b = 0 To m - 1
                If ts Is Nothing Then
                    q(b) = p.BoneDesdeMalla(b)
                Else
                    q(b) = Operadores.Componer(p.BoneDesdeMalla(b), ts(b))
                End If
            Next
            Return q
        End Function

        ''' <summary>
        ''' (b) El deform — `0x14193DC90`.
        ''' <para>`M = Σ_k w_k · M[boneIndices[k]]`, y después `salida = local · M`: la posición con
        ''' traslación y la normal sin ella.</para>
        ''' <para>⚠️ Acá decía que promediar los puntos transformados «da otro número en cuanto
        ''' hay traslación de por medio». **Es falso, y lo mide GPL4b**: `TransformarPunto` es
        ''' LINEAL en la matriz, así que `Σ wₖ·T(p, Mₖ) = T(p, Σ wₖ·Mₖ)` con traslación y todo.
        ''' El motor suma matrices porque es UNA transformación en vez de k, no porque dé otro
        ''' resultado. Era un argumento de plausibilidad donde va una medición.</para>
        ''' </summary>
        Friend Sub Deformar(p As PielCompilada, salida As Buffer, matrices As Mat4())
            If p Is Nothing OrElse salida Is Nothing OrElse matrices Is Nothing Then Return
            For i = 0 To p.Cuenta - 1
                Dim vi = p.Vertice(i)
                If vi < 0 OrElse vi >= salida.Cuenta Then Continue For

                Dim m As Mat4
                m.F0 = Vector128(Of Single).Zero
                m.F1 = Vector128(Of Single).Zero
                m.F2 = Vector128(Of Single).Zero
                m.F3 = Vector128(Of Single).Zero
                Dim hubo = False
                For k = 0 To PielCompilada.MaxInfluencias - 1
                    Dim w = p.Pesos(i * PielCompilada.MaxInfluencias + k)
                    If w = 0.0F Then Continue For
                    Dim b = p.Huesos(i * PielCompilada.MaxInfluencias + k)
                    If b < 0 OrElse b >= matrices.Length Then Continue For
                    Dim vw = Vector128.Create(w)
                    m.F0 = Vector128.Add(m.F0, Vector128.Multiply(matrices(b).F0, vw))
                    m.F1 = Vector128.Add(m.F1, Vector128.Multiply(matrices(b).F1, vw))
                    m.F2 = Vector128.Add(m.F2, Vector128.Multiply(matrices(b).F2, vw))
                    m.F3 = Vector128.Add(m.F3, Vector128.Multiply(matrices(b).F3, vw))
                    hubo = True
                Next
                If Not hubo Then Continue For

                ' la POSICION es un punto (con traslacion); la normal, una direccion
                Dim lp = Simd.Leer(p.PosLocal, i)
                salida.SetVertice(vi, Operadores.TransformarPunto(lp, m))
                If p.Canales >= 2 AndAlso salida.Normales IsNot Nothing AndAlso
                   p.NrmLocal IsNot Nothing AndAlso p.NrmLocal.Length > 0 Then
                    Dim ln = Simd.Leer(p.NrmLocal, i)
                    salida.SetNormal(vi, Operadores.TransformarDireccion(ln, m))
                End If
                ' ⛔ LA TANGENTE Y LA BITANGENTE, tambien como DIRECCION. Y solo si el buffer las
                ' tiene: que el canal sea `Nothing` es lo que el motor comprueba antes de tocarlo
                ' (`0x14190A216`, `0x14190A23B`), no un detalle de esta replica.
                If p.Canales >= 3 AndAlso salida.Tangentes IsNot Nothing AndAlso
                   p.TanLocal IsNot Nothing AndAlso p.TanLocal.Length > 0 Then
                    Dim lt = Simd.Leer(p.TanLocal, i)
                    salida.SetTangente(vi, Operadores.TransformarDireccion(lt, m))
                End If
                If p.Canales >= 4 AndAlso salida.Bitangentes IsNot Nothing AndAlso
                   p.BitLocal IsNot Nothing AndAlso p.BitLocal.Length > 0 Then
                    Dim lb = Simd.Leer(p.BitLocal, i)
                    salida.SetBitangente(vi, Operadores.TransformarDireccion(lb, m))
                End If
            Next
        End Sub

        ''' <summary>
        ''' ⭐ UN punto skinneado — la misma ley del deform (`0x14193DC90`) para un vertice suelto:
        ''' `M = Σ_k w_k · matrices[huesos[k]]`, y despues `p · M` (fila-vector, con traslacion).
        ''' <para>⚠️ La suma es de MATRICES porque es UNA transformación en vez de k, **no**
        ''' porque promediar los puntos transformados dé otro número: `TransformarPunto` es lineal
        ''' en la matriz y los dos caminos coinciden. Lo mide GPL4b.</para>
        ''' <para>Los pesos vienen en `uint8` y se escalan con `PorPeso` (`0x142492850`). Un peso que
        ''' no esta — la familia de UNA influencia no trae pesos — vale 1.</para>
        ''' <para>Existe para que los arneses midan con la MISMA aritmetica que corre el motor, en vez
        ''' de con una copia propia.</para>
        ''' </summary>
        Friend Function PuntoSkinneado(p As Vector128(Of Single), huesos As Integer(),
                                       pesos As Single(), matrices As Mat4()) As Vector128(Of Single)
            If huesos Is Nothing OrElse matrices Is Nothing Then Return p
            Dim m As Mat4
            m.F0 = Vector128(Of Single).Zero
            m.F1 = Vector128(Of Single).Zero
            m.F2 = Vector128(Of Single).Zero
            m.F3 = Vector128(Of Single).Zero
            Dim hubo = False
            For k = 0 To huesos.Length - 1
                Dim w = If(pesos Is Nothing OrElse k >= pesos.Length, 1.0F, pesos(k))
                If w = 0.0F Then Continue For
                Dim b = huesos(k)
                If b < 0 OrElse b >= matrices.Length Then Continue For
                Dim vw = Vector128.Create(w)
                m.F0 = Vector128.Add(m.F0, Vector128.Multiply(matrices(b).F0, vw))
                m.F1 = Vector128.Add(m.F1, Vector128.Multiply(matrices(b).F1, vw))
                m.F2 = Vector128.Add(m.F2, Vector128.Multiply(matrices(b).F2, vw))
                m.F3 = Vector128.Add(m.F3, Vector128.Multiply(matrices(b).F3, vw))
                hubo = True
            Next
            If Not hubo Then Return p
            Return Operadores.TransformarPunto(p, m)
        End Function

        ''' <summary>Las dos fases juntas, que es como el operador aparece en la cadena.</summary>
        Friend Sub Ejecutar(p As PielCompilada, bufs As Buffer(), transformSets As Mat4()())
            If p Is Nothing Then Return
            Dim ts As Mat4() = Nothing
            If transformSets IsNot Nothing AndAlso p.IndiceDelTransformSet >= 0 AndAlso
               p.IndiceDelTransformSet < transformSets.Length Then
                ts = transformSets(p.IndiceDelTransformSet)
            End If
            ' ⛔ la doble indirección del motor (`0x1418C6134`), sin camino de escape: `Buffers.Real`
            ' revienta si la ranura no resuelve, igual que el `mov r11, [r10+rax*8]` del binario.
            Dim salida = Buffers.Real(bufs, p.BufferDeSalida)
            Deformar(p, salida, PrepararMatrices(p, ts))
        End Sub

    End Module

End Namespace

#End If
