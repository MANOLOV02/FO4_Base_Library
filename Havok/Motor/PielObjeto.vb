Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' hclObjectSpaceSkin{P,PN,PNT,PNTB}Operator — tipos 22..25 — transcripto del `.exe`, desde los bloques
' CRUDOS del archivo.
'
' Despachadores (entrada desde `0x1418C6134`: `rcx = op`, `rdx = transformSet`, `r8 = buffer real`):
'   22 P    `0x141938CB0`   23 PN   `0x14193BBD0`   24 PNT  `0x141940960`   25 PNTB `0x141947C60`
' Cada uno elige un envoltorio por (a) `op.localBlocks.count` (+0xA8) > 0 ⇒ bloques EMPAQUETADOS
' (+0xA0), si no los SIN EMPAQUETAR (+0xB0) (`0x141938D10`, `0x14193BC3D`, `0x1419409D9`,
' `0x141947CE5`); y (b) el código de layout del buffer — bit 0 de `+0x20`, `+0x48`, `+0x60`, `+0x78`
' (`0x141938D04`, `0x14193BC24`-`0x14193BC38`, `0x1419409B4`-`0x1419409D7`, `0x141947CB4`-`0x141947CE3`).
' Los códigos especiales son «los primeros k canales simples y el resto no»; cualquier otro código va
' al kernel general.
'
' Envoltorio (p. ej. `0x14193BE60`): `n = op.transformSubset.count` (+0x38) o, sin subconjunto,
' `transformSet.count` (+0x18) (`0x14193BE7C`-`0x14193BE91`); scratch de 64 B por entrada; prepara con
' `0x141A10F00` (con subconjunto) o `0x141A0EEB0` (sin), pasando `buffer + 0xC0` (`0x14193BF15`); llama al
' kernel con `rcx = op + 0x48` (el deformer), `rdx = bloques locales`, `r8 = scratch`, `r9 = buffer`.
'
' Kernels (28): P `0x141939CF0` `0x141939390` `0x14193B0D0` `0x14193A700`; PN `0x14193C5E0` `0x14193D100`
' `0x14193DC90` `0x14193E840` `0x14193F2E0` `0x14193FD20`; PNT `0x1419416A0` `0x1419423B0` `0x141943060`
' `0x141943D50` `0x141944A60` `0x141945650` `0x141946310` `0x141946ED0`; PNTB `0x141948CD0` `0x141949B70`
' `0x14194A990` `0x14194B7D0` `0x14194C640` `0x14194D4C0` `0x14194E200` `0x14194EEB0` `0x14194FB60`
' `0x141950850`. Medidos bit a bit contra esta transcripción: Diferencial\piel_objeto.json (GDFo*).
' =================================================================================================

Namespace Havok.Motor

    ''' <summary>Un `hclObjectSpaceSkin*Operator` compilado desde los bloques CRUDOS del archivo.</summary>
    Friend NotInheritable Class PielObjeto

        ''' <summary>22 = P, 23 = PN, 24 = PNT, 25 = PNTB.</summary>
        Friend ReadOnly Tipo As Integer
        ''' <summary>Canales por vértice: 1..4 (posición, normal, tangente, bitangente).</summary>
        Friend ReadOnly Canales As Integer
        ''' <summary>`op.outputBufferIndex` (+0x40).</summary>
        Friend ReadOnly BufferDeSalida As Integer
        ''' <summary>`op.transformSetIndex` (+0x44).</summary>
        Friend ReadOnly IndiceDelTransformSet As Integer
        ''' <summary>`op.boneFromSkinMeshTransforms` (+0x20/+0x28).</summary>
        Friend ReadOnly BoneDesdeMalla As Mat4()
        ''' <summary>`op.transformSubset` (+0x30/+0x38). Vacío = sin subconjunto.</summary>
        Friend ReadOnly Subconjunto As Integer()
        ''' <summary>`deformer.controlBytes` (+0x40/+0x48 del deformer).</summary>
        Friend ReadOnly Control As Byte()
        ''' <summary>Por familia (0 = cuatro, 1 = tres, 2 = dos, 3 = una influencia): los
        ''' `vertexIndices` (16 por bloque), los `boneIndices` (16·n por bloque) y los `boneWeights`
        ''' (16·n por bloque; vacío en la de una), tal cual están en el archivo.</summary>
        Friend ReadOnly VerticesDeFamilia As UShort()()
        Friend ReadOnly HuesosDeFamilia As UShort()()
        Friend ReadOnly PesosDeFamilia As Byte()()
        ''' <summary>`op.localBlocks.count > 0` (+0xA8): bloques empaquetados (`int16`).</summary>
        Friend ReadOnly Empaquetado As Boolean
        ''' <summary>Empaquetados: `Canales · 64` `int16` por bloque, en el orden del archivo.</summary>
        Friend ReadOnly LocalesQ As Short()
        ''' <summary>Sin empaquetar: `Canales · 64` `Single` por bloque (16 `vector4` por canal).</summary>
        Friend ReadOnly LocalesF As Single()
        ''' <summary>Cuántos bloques locales hay (`+0xA8` o `+0xB8`).</summary>
        Friend ReadOnly NumLocales As Integer

        Friend Sub New(tipo As Integer, bufferDeSalida As Integer, indiceDelTransformSet As Integer,
                       boneDesdeMalla As Mat4(), subconjunto As Integer(), control As Byte(),
                       vertices As UShort()(), huesos As UShort()(), pesos As Byte()(),
                       empaquetado As Boolean, localesQ As Short(), localesF As Single(), numLocales As Integer)
            Me.Tipo = tipo
            Me.Canales = tipo - 21
            Me.BufferDeSalida = bufferDeSalida
            Me.IndiceDelTransformSet = indiceDelTransformSet
            Me.BoneDesdeMalla = boneDesdeMalla
            Me.Subconjunto = If(subconjunto, Array.Empty(Of Integer)())
            Me.Control = If(control, Array.Empty(Of Byte)())
            Me.VerticesDeFamilia = vertices
            Me.HuesosDeFamilia = huesos
            Me.PesosDeFamilia = pesos
            Me.Empaquetado = empaquetado
            Me.LocalesQ = If(localesQ, Array.Empty(Of Short)())
            Me.LocalesF = If(localesF, Array.Empty(Of Single)())
            Me.NumLocales = numLocales
        End Sub

        ''' <summary>INSTRUMENTO (Cobertura): cuántas casillas escriben los bloques que el control
        ''' recorre, en su orden.</summary>
        Friend ReadOnly Property Cuenta As Integer
            Get
                Dim n = 0
                Dim c(3) As Integer
                For Each cb In Control
                    If cb > 3 Then Continue For
                    If c(cb) * 16 < VerticesDeFamilia(cb).Length Then n += 16
                    c(cb) += 1
                Next
                Return n
            End Get
        End Property

        ''' <summary>INSTRUMENTO (Cobertura): el índice de vértice de la casilla `i`, en el orden del
        ''' recorrido del control.</summary>
        Friend Function Vertice(i As Integer) As Integer
            Dim c(3) As Integer
            Dim base = 0
            For Each cb In Control
                If cb > 3 Then Continue For
                If c(cb) * 16 >= VerticesDeFamilia(cb).Length Then Continue For
                If i < base + 16 Then Return VerticesDeFamilia(cb)(c(cb) * 16 + (i - base))
                base += 16
                c(cb) += 1
            Next
            Return -1
        End Function

    End Class

    Friend Module PielDeObjeto

        ''' <summary>`0x14262BA50` = 65536,0 (`0x14193D9BF`, `0x14193D52C`).</summary>
        Private Const Dos16 As Single = 65536.0F

        ' --------------------------------------------------------------------------------------------
        ' (a) Las matrices — `0x141A0EEB0` (sin subconjunto) / `0x141A10F00` (con subconjunto)
        ' --------------------------------------------------------------------------------------------

        ''' <summary>
        ''' `M[k] = (bfm[k] · T) · C`, fila-vector, con `C = buffer + 0xC0`.
        ''' <para>Filas 0..2: `(r.x·A0 + r.y·A1) + r.z·A2`; fila 3: `((r.x·A0 + A3) + r.y·A1) + r.z·A2`
        ''' — el resto por elemento de `0x141A10D00`-`0x141A10EB4` (sin subconjunto: primer producto
        ''' `0x141A10D45`-`0x141A10E01`, segundo `0x141A10E16`-`0x141A10EA1`, escritura `0x141A10EA5`-`EB0`)
        ''' y `0x141A12CF2`-`0x141A12EAB` (con subconjunto: `T = transforms[subset[k]]`, `0x141A12D12`-`D4B`).
        ''' Los lotes de 16 (`0x141A0EF30`-`0x141A10CB8`, `0x141A10F80`-`0x141A12CB0`) hacen la misma cuenta.</para>
        ''' </summary>
        Friend Function Matrices(p As PielObjeto, ts As Mat4(), cInv As Mat4) As Mat4()
            Dim usaSub = p.Subconjunto.Length > 0
            Dim n = If(usaSub, p.Subconjunto.Length, ts.Length)                       ' 0x14193BE7C-0x14193BE91
            Dim r(n - 1) As Mat4
            For k = 0 To n - 1
                If k >= p.BoneDesdeMalla.Length Then
                    ' el motor lee `bfm[k]` sin cota (`[r11 + ...]` en 0x141A10D1F)
                    Throw New InvalidOperationException($"PielObjeto: boneFromSkinMeshTransforms tiene {p.BoneDesdeMalla.Length} y el envoltorio pide {n}.")
                End If
                Dim t = If(usaSub, ts(p.Subconjunto(k)), ts(k))                        ' 0x141A12D12-0x141A12D43
                r(k) = Operadores.Componer(Operadores.Componer(p.BoneDesdeMalla(k), t), cInv)
            Next
            Return r
        End Function

        ' --------------------------------------------------------------------------------------------
        ' (b) El kernel
        ' --------------------------------------------------------------------------------------------

        ''' <summary>La desquantización de 4 `int16` (`0x14193D903`-`0x14193D977`): `punpcklwd` contra
        ''' cero ⇒ `int32 = s·2¹⁶` por lane, `cvtdq2ps`, y por la lane 3 REINTERPRETADA como float
        ''' (`pshufd 0xFF`). ⛔ Las CUATRO lanes, la `w` incluida.</summary>
        Friend Function Desquantizar4(s0 As Short, s1 As Short, s2 As Short, s3 As Short) As Vector128(Of Single)
            Dim esc = BitConverter.Int32BitsToSingle(CInt(s3) << 16)
            Dim c = Vector128.Create(CSng(CInt(s0) << 16), CSng(CInt(s1) << 16), CSng(CInt(s2) << 16), CSng(CInt(s3) << 16))
            Return Vector128.Multiply(c, Vector128.Create(esc))
        End Function

        ''' <summary>El peso `b` como lo arma el kernel de cuatro (`0x14193D99C`-`0x14193D9D7`) y el de dos
        ''' (`0x14193D499`, `0x14193D50A`-`0x14193D542`): `(float(b ≫ 16)·65536 + float(b ∧ 0xFFFF)) · c`.
        ''' El de tres (`0x14193D670`-`0x14193D739`): `float(b) · c`.</summary>
        Private Function Peso(b As Byte) As Single
            Return (CSng(CInt(b) >> 16) * Dos16 + CSng(CInt(b) And &HFFFF)) * Piel.PorPeso
        End Function

        Private Function Peso3(b As Byte) As Single
            Return CSng(CInt(b)) * Piel.PorPeso
        End Function

        Private Function Ponderada(m As Mat4, w As Single) As Mat4
            Dim vw = Vector128.Create(w)
            Dim r As Mat4
            r.F0 = Vector128.Multiply(m.F0, vw) : r.F1 = Vector128.Multiply(m.F1, vw)
            r.F2 = Vector128.Multiply(m.F2, vw) : r.F3 = Vector128.Multiply(m.F3, vw)
            Return r
        End Function

        Private Function Sumar(a As Mat4, b As Mat4) As Mat4
            Dim r As Mat4
            r.F0 = Vector128.Add(a.F0, b.F0) : r.F1 = Vector128.Add(a.F1, b.F1)
            r.F2 = Vector128.Add(a.F2, b.F2) : r.F3 = Vector128.Add(a.F3, b.F3)
            Return r
        End Function

        ''' <summary>
        ''' ⭐ El operador entero.
        ''' <para>```
        ''' salida = buffers[buffers[op.outputBufferIndex].ranura]            ' 0x1418C6134-0x1418C614B
        ''' M      = Matrices(op, transformSet, salida+0xC0)                  ' envoltorio
        ''' k      = canales con 4 lanes (código de layout)                    ' despachador
        ''' para cada controlByte c (en orden):                               ' 0x14193D26B-0x14193D28B / 0x14193DBA0-0x14193DBB3
        '''   c > 3: siguiente, SIN consumir bloque local                      ' 0x14193D28B jne 0x14193DBA0
        '''   bloque = familia[c][siguiente]; local = locales[siguiente]      ' 0x14193D427-0x14193D43C, 0x14193DB29-0x14193DB54
        '''   para cada casilla j = 0..15 (TODAS, sin mirar peso ni repetidos) ' 0x14193DB1F cmp r11, 0x10
        '''     B = Σ w·M[hueso] (orden del kernel)                            ' cuatro 0x14193D9EF-0x14193DAB5
        '''     posición = ((p.x·B0 + B3) + p.y·B1) + p.z·B2                    ' 0x14193DAD3-0x14193DAE9
        '''     dirección = (d.x·B0 + d.y·B1) + d.z·B2                          ' 0x14193DAE5-0x14193DB15
        '''     canal &lt; k: 4 lanes (`movups`, 0x14193DAFB / 0x14193DB1B); si no, 3 floats (`movss [+8]`, 0x14193C836)
        '''     en data + stride·vertice (stride = byte +0x1C / +0x44 / +0x5C / +0x74)
        ''' ```</para>
        ''' </summary>
        Friend Sub Ejecutar(p As PielObjeto, bufs As Buffer(), transformSets As Mat4()())
            Dim ts = transformSets(p.IndiceDelTransformSet)                         ' 0x1418C6147-0x1418C6153
            Dim salida = Buffers.Real(bufs, p.BufferDeSalida)                         ' 0x1418C6134-0x1418C614B
            Dim m = Matrices(p, ts, salida.DesdeEspacioDeSimulacion)

            ' el código de layout — los k primeros canales simples y el resto no, o el general
            Dim simples = {salida.LayoutSimple, salida.LayoutSimpleNormales, salida.LayoutSimpleTangentes, salida.LayoutSimpleBitangentes}
            Dim k = 0
            While k < p.Canales AndAlso simples(k)
                k += 1
            End While
            For c = k To p.Canales - 1
                If simples(c) Then k = 0 : Exit For
            Next

            Dim canal = New Single(p.Canales - 1)() {}
            Dim stride(p.Canales - 1) As Integer
            For c = 0 To p.Canales - 1
                canal(c) = Buffers.FloatsDeCanal(salida, c)
                stride(c) = Buffers.StrideDeCanal(salida, c) And &HFF                  ' movzx byte [rdi+0x1C] (0x14193DA60)
                If (stride(c) And 3) <> 0 Then
                    Throw New InvalidOperationException($"PielObjeto: stride {stride(c)} del canal {c} no es múltiplo de 4; este buffer de floats no lo representa.")
                End If
            Next

            Dim cuenta(3) As Integer
            Dim local = 0
            For Each cb In p.Control
                If cb > 3 Then Continue For                                          ' 0x14193D28B
                Dim ninf = 4 - cb
                Dim bloque = cuenta(cb) : cuenta(cb) += 1
                Dim bl = local : local += 1
                Dim vs = p.VerticesDeFamilia(cb), hs = p.HuesosDeFamilia(cb), ps = p.PesosDeFamilia(cb)
                For j = 0 To 15
                    Dim vi = CInt(vs(bloque * 16 + j))
                    Dim hb = bloque * 16 * ninf + j * ninf                                   ' carril intercalado
                    Dim bm As Mat4
                    Select Case cb
                        Case 0      ' 0x14193D90C-0x14193D91C huesos; 0x14193D9EF-0x14193DAB5 suma
                            Dim w0 = Peso(ps(hb)), w1 = Peso(ps(hb + 1)), w2 = Peso(ps(hb + 2)), w3 = Peso(ps(hb + 3))
                            bm = Sumar(Sumar(Sumar(Ponderada(m(hs(hb)), w0), Ponderada(m(hs(hb + 1)), w1)),
                                             Ponderada(m(hs(hb + 2)), w2)), Ponderada(m(hs(hb + 3)), w3))
                        Case 1      ' 0x14193D685-0x14193D690 huesos; 0x14193D74A-0x14193D7C2 suma
                            Dim w0 = Peso3(ps(hb)), w1 = Peso3(ps(hb + 1)), w2 = Peso3(ps(hb + 2))
                            bm = Sumar(Sumar(Ponderada(m(hs(hb)), w0), Ponderada(m(hs(hb + 1)), w1)), Ponderada(m(hs(hb + 2)), w2))
                        Case 2      ' 0x14193D4A3-0x14193D4A7 huesos; 0x14193D54D-0x14193D591 suma
                            Dim w0 = Peso(ps(hb)), w1 = Peso(ps(hb + 1))
                            bm = Sumar(Ponderada(m(hs(hb)), w0), Ponderada(m(hs(hb + 1)), w1))
                        Case Else   ' 0x14193D2BF hueso, sin peso
                            bm = m(hs(hb))
                    End Select
                    For c = 0 To p.Canales - 1
                        Dim lv As Vector128(Of Single)
                        If p.Empaquetado Then
                            Dim o = bl * p.Canales * 64 + c * 64 + j * 4                        ' [rbx], [rbx+0x80], … 8 B por casilla
                            lv = Desquantizar4(p.LocalesQ(o), p.LocalesQ(o + 1), p.LocalesQ(o + 2), p.LocalesQ(o + 3))
                        Else
                            Dim o = bl * p.Canales * 64 + c * 64 + j * 4                        ' [rbx], [rbx+0x100], … 16 B por casilla
                            lv = Vector128.Create(p.LocalesF(o), p.LocalesF(o + 1), p.LocalesF(o + 2), p.LocalesF(o + 3))
                        End If
                        Dim r = If(c = 0, Operadores.TransformarPunto(lv, bm), Operadores.TransformarDireccion(lv, bm))
                        Dim dst = canal(c)
                        Dim off = (stride(c) \ 4) * vi
                        dst(off) = r.GetElement(0) : dst(off + 1) = r.GetElement(1) : dst(off + 2) = r.GetElement(2)
                        If c < k Then dst(off + 3) = r.GetElement(3)
                    Next
                Next
            Next
        End Sub

        ' --------------------------------------------------------------------------------------------
        ' La compilación desde el archivo
        ' --------------------------------------------------------------------------------------------

        Private Function I16(v As Integer) As Short
            Dim u = v And &HFFFF
            If u >= 32768 Then u -= 65536
            Return CShort(u)
        End Function

        ''' <summary>Un `hclObjectSpaceSkin*Operator` del grafo, compilado. `Nothing` si no es de esas clases.</summary>
        Friend Function Compilar(g As HkxObjectGraph_Class, crudo As HkxVirtualObjectGraph_Class) As PielObjeto
            If g Is Nothing OrElse crudo Is Nothing Then Return Nothing
            Dim tipo = 0
            Dim bfmL As List(Of Single()) = Nothing, subL As List(Of Integer) = Nothing
            Dim outIdx = 0UI, tsIdx = 0UI
            Dim def As HkObj_HclObjectSpaceDeformer = Nothing
            Dim q As New List(Of List(Of Integer)())      ' por bloque: canales de int16
            Dim f As New List(Of List(Of Single())())     ' por bloque: canales de vector4

            Dim oP = HkObj_HclObjectSpaceSkinPOperator.Leer(g, crudo)
            Dim oPN = HkObj_HclObjectSpaceSkinPNOperator.Leer(g, crudo)
            Dim oPNT = HkObj_HclObjectSpaceSkinPNTOperator.Leer(g, crudo)
            Dim oPNTB = HkObj_HclObjectSpaceSkinPNTBOperator.Leer(g, crudo)
            If oP IsNot Nothing Then
                tipo = 22 : bfmL = oP.BoneFromSkinMeshTransforms : subL = oP.TransformSubset
                outIdx = oP.OutputBufferIndex : tsIdx = oP.TransformSetIndex : def = oP.ObjectSpaceDeformer
                For Each b In oP.LocalPs : q.Add({b.LocalPosition}) : Next
                For Each b In oP.LocalUnpackedPs : f.Add({b.LocalPosition}) : Next
            ElseIf oPN IsNot Nothing Then
                tipo = 23 : bfmL = oPN.BoneFromSkinMeshTransforms : subL = oPN.TransformSubset
                outIdx = oPN.OutputBufferIndex : tsIdx = oPN.TransformSetIndex : def = oPN.ObjectSpaceDeformer
                For Each b In oPN.LocalPNs : q.Add({b.LocalPosition, b.LocalNormal}) : Next
                For Each b In oPN.LocalUnpackedPNs : f.Add({b.LocalPosition, b.LocalNormal}) : Next
            ElseIf oPNT IsNot Nothing Then
                tipo = 24 : bfmL = oPNT.BoneFromSkinMeshTransforms : subL = oPNT.TransformSubset
                outIdx = oPNT.OutputBufferIndex : tsIdx = oPNT.TransformSetIndex : def = oPNT.ObjectSpaceDeformer
                For Each b In oPNT.LocalPNTs : q.Add({b.LocalPosition, b.LocalNormal, b.LocalTangent}) : Next
                For Each b In oPNT.LocalUnpackedPNTs : f.Add({b.LocalPosition, b.LocalNormal, b.LocalTangent}) : Next
            ElseIf oPNTB IsNot Nothing Then
                tipo = 25 : bfmL = oPNTB.BoneFromSkinMeshTransforms : subL = oPNTB.TransformSubset
                outIdx = oPNTB.OutputBufferIndex : tsIdx = oPNTB.TransformSetIndex : def = oPNTB.ObjectSpaceDeformer
                For Each b In oPNTB.LocalPNTBs : q.Add({b.LocalPosition, b.LocalNormal, b.LocalTangent, b.LocalBiTangent}) : Next
                For Each b In oPNTB.LocalUnpackedPNTBs : f.Add({b.LocalPosition, b.LocalNormal, b.LocalTangent, b.LocalBiTangent}) : Next
            Else
                Return Nothing
            End If
            Dim canales = tipo - 21

            Dim bfm(If(bfmL Is Nothing, 0, bfmL.Count) - 1) As Mat4
            For i = 0 To bfm.Length - 1
                bfm(i) = Fachada.M4(bfmL(i))
            Next
            Dim subc = If(subL Is Nothing, Array.Empty(Of Integer)(), subL.Select(Function(x) x And &HFFFF).ToArray())

            Dim ctrl = If(def?.ControlBytes Is Nothing, Array.Empty(Of Byte)(), def.ControlBytes.Select(Function(x) CByte(x And &HFF)).ToArray())
            Dim vs(3)() As UShort, hs(3)() As UShort, ps(3)() As Byte
            For fam = 0 To 3
                Dim lv As New List(Of UShort), lh As New List(Of UShort), lw As New List(Of Byte)
                Select Case fam
                    Case 0
                        For Each b In If(def?.FourBlendEntries, New List(Of HkObj_HclObjectSpaceDeformerFourBlendEntryBlock))
                            lv.AddRange(b.VertexIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lh.AddRange(b.BoneIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lw.AddRange(b.BoneWeights.Select(Function(x) CByte(x And &HFF)))
                        Next
                    Case 1
                        For Each b In If(def?.ThreeBlendEntries, New List(Of HkObj_HclObjectSpaceDeformerThreeBlendEntryBlock))
                            lv.AddRange(b.VertexIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lh.AddRange(b.BoneIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lw.AddRange(b.BoneWeights.Select(Function(x) CByte(x And &HFF)))
                        Next
                    Case 2
                        For Each b In If(def?.TwoBlendEntries, New List(Of HkObj_HclObjectSpaceDeformerTwoBlendEntryBlock))
                            lv.AddRange(b.VertexIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lh.AddRange(b.BoneIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lw.AddRange(b.BoneWeights.Select(Function(x) CByte(x And &HFF)))
                        Next
                    Case 3
                        For Each b In If(def?.OneBlendEntries, New List(Of HkObj_HclObjectSpaceDeformerOneBlendEntryBlock))
                            lv.AddRange(b.VertexIndices.Select(Function(x) CUShort(x And &HFFFF)))
                            lh.AddRange(b.BoneIndices.Select(Function(x) CUShort(x And &HFFFF)))
                        Next
                End Select
                vs(fam) = lv.ToArray() : hs(fam) = lh.ToArray() : ps(fam) = lw.ToArray()
            Next

            ' ⛔ la elección empaquetado/sin empaquetar es `localBlocks.count > 0` (+0xA8), no «el que tenga datos»
            Dim empaquetado = q.Count > 0
            Dim lq As Short() = Nothing, lf As Single() = Nothing
            Dim numLocales As Integer
            If empaquetado Then
                numLocales = q.Count
                ReDim lq(numLocales * canales * 64 - 1)
                For bi = 0 To numLocales - 1
                    For c = 0 To canales - 1
                        Dim src = q(bi)(c)
                        For i = 0 To 63
                            lq(bi * canales * 64 + c * 64 + i) = I16(src(i))
                        Next
                    Next
                Next
            Else
                numLocales = f.Count
                ReDim lf(numLocales * canales * 64 - 1)
                For bi = 0 To numLocales - 1
                    For c = 0 To canales - 1
                        Dim src = f(bi)(c)
                        For s = 0 To 15
                            For i = 0 To 3
                                lf(bi * canales * 64 + c * 64 + s * 4 + i) = src(s)(i)
                            Next
                        Next
                    Next
                Next
            End If
            Return New PielObjeto(tipo, CInt(outIdx), CInt(tsIdx), bfm, subc, ctrl, vs, hs, ps, empaquetado, lq, lf, numLocales)
        End Function

    End Module

End Namespace
