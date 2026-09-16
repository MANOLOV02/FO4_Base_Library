Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LOS BUFFERS — `hclInstantiationUtil::createBuffers` `0x1418ED2D0` y su tabla de 8 entradas
' `0x1418ED6E0`.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, cap. 4.2.
'
' ⛔⛔⛔ TRES COSAS QUE HAY QUE RESPETAR, Y LAS TRES SON CONTRAINTUITIVAS:
'
' 1. **El buffer de simulación ES el array de partículas.** El creador del tipo 1 (`0x1418C7040`)
'    pone `buf.Datos = simCloth.positions` — el MISMO arreglo, no una copia. Lo que un operador
'    escribe ahí **es** la posición de la partícula.
'
' 2. **Cada buffer tiene su PROPIO espacio.** `+0x80…+0xB0` es su matriz al espacio de simulación y
'    `+0xC0…+0xF0` la inversa. `CopyVertices` compone `M_entrada × M_salida⁻¹` y transforma cada
'    vértice al pasarlo. Tratar los buffers como si compartieran espacio rompe todo aguas abajo.
'    ⚠️ El constructor base (`0x1418ED150`) deja las DOS en la identidad, así que en reposo el cambio
'    de espacio es un no-op — pero eso se **mide** (G20), no se supone.
'
' 3. **`+0x100` no es un puntero: es la RANURA a la que el buffer pertenece.** `setBuffer(idx, buf)`
'    (`0x1418C8530`) escribe `buf[+0x100] = idx`, así que en reposo la doble indirección
'    `buffers[buffers[i][+0x100]]` es la identidad. Existe para sobrevivir a la sustitución de
'    buffers en `Operator Prepare` (`0x1418C8C61`), donde el sustituto guarda la ranura original.
' =================================================================================================

Namespace Havok.Motor

    ''' <summary>
    ''' Un buffer vivo. ⚠️ **El tamaño depende del creador**: los tipos 6 y 7 piden `0x120` B, y el
    ''' **1** y el **8** piden `0x110` (`0x1418C7059 mov edx, 0x110`). La primera redacción decía
    ''' «`0x120`, `0x110` el tipo 8» y dejaba al tipo 1 —el del sim-cloth— del lado equivocado
    ''' (motor-58).
    ''' </summary>
    Friend NotInheritable Class Buffer

        ''' <summary>`+0x10` — los datos de vértice. ⭐ Para el buffer de simulación **es** el arreglo
        ''' de posiciones de la instancia, no una copia (`0x1418C7040`).</summary>
        Friend Datos As Single()

        ''' <summary>`+0x18` — cuántos vértices.</summary>
        Friend Cuenta As Integer

        ''' <summary>`+0x1C` — el **stride en bytes**. 16 para el buffer de simulación.</summary>
        Friend StrideBytes As Integer

        ''' <summary>`+0x20` bit 0 — layout simple de posiciones. Decide entre el kernel SIMD y el
        ''' escalar de `MoveParticles` (`0x141952480` / `0x141952660`).</summary>
        Friend LayoutSimple As Boolean

        ''' <summary>`+0x28` — los índices de triángulo (`uint16`), o `Nothing`.</summary>
        Friend IndicesDeTriangulo As UShort()

        ''' <summary>`+0x30` — cuántos triángulos.</summary>
        Friend NumTriangulos As Integer

        ''' <summary>`+0x38` — las normales por vértice, o `Nothing`. ⛔ Que sea `Nothing` **cambia de
        ''' rama** en varios kernels; no es un arreglo de ceros.</summary>
        Friend Normales As Single()

        ''' <summary>`+0x44` — el stride de las normales, en bytes.</summary>
        Friend StrideNormalesBytes As Integer

        ''' <summary>`+0x48` bit 0 — layout simple de normales.</summary>
        Friend LayoutSimpleNormales As Boolean

        ''' <summary>
        ''' `+0x50` — las TANGENTES por vértice, o `Nothing`.
        ''' <para>⭐ Los tres offsets salen del binario, no de la simetría: `hclSkinOperator::TtSkin`
        ''' arma los cuatro punteros de entrada con el mismo patrón `base + stride·startVertex` en el
        ''' orden P, N, T, B (`0x14190A2E1`-`0x14190A32C`), y el despacho de
        ''' `hclObjectSpaceSkinPNTOperator` lee `+0x60` como TERCER bit de layout
        ''' (`0x1419409D0`).</para>
        ''' </summary>
        Friend Tangentes As Single()

        ''' <summary>`+0x5C` — el stride de las tangentes, en bytes.</summary>
        Friend StrideTangentesBytes As Integer

        ''' <summary>`+0x60` bit 0 — layout simple de tangentes.</summary>
        Friend LayoutSimpleTangentes As Boolean

        ''' <summary>`+0x68` — las BITANGENTES por vértice, o `Nothing`. El despacho de `PNTB` lee
        ''' `+0x78` como CUARTO bit de layout (`0x141947CDC`).</summary>
        Friend Bitangentes As Single()

        ''' <summary>`+0x74` — el stride de las bitangentes, en bytes.</summary>
        Friend StrideBitangentesBytes As Integer

        ''' <summary>`+0x78` bit 0 — layout simple de bitangentes.</summary>
        Friend LayoutSimpleBitangentes As Boolean

        ''' <summary>`+0x80…+0xB0` — la matriz del buffer **al espacio de simulación**. El constructor
        ''' base la deja en la IDENTIDAD (`0x1418ED150`).</summary>
        Friend AEspacioDeSimulacion As Mat4

        ''' <summary>`+0xC0…+0xF0` — la INVERSA: del espacio de simulación al del buffer. Ídem,
        ''' identidad al construir.</summary>
        Friend DesdeEspacioDeSimulacion As Mat4

        ''' <summary>
        ''' Los cuatro canales vistos como BYTES — `+0x10`, `+0x38`, `+0x50` y `+0x68`.
        ''' <para>⭐ El puntero del motor es un `void*`: lo que hay del otro lado lo decide el
        ''' FORMATO del vértice, no el canal. Los únicos que lo miran así son
        ''' `hclInputConvertOperator` (14) y `hclOutputConvertOperator` (15), que son los dos
        ''' únicos operadores que hablan con el buffer del USUARIO — el que trae `VC_BYTE4`,
        ''' `VC_SHORT3` y `VC_HFLOAT3`.</para>
        ''' <para>⛔ Un canal es de floats **o** empaquetado, nunca las dos cosas: acá va
        ''' `Nothing` mientras el canal sea de floats. No hay dos copias del dato.</para>
        ''' </summary>
        Friend Empaquetados As Byte()()

        ''' <summary>`+0x100` — **la ranura a la que este buffer pertenece**, no un puntero.</summary>
        Friend Ranura As Integer

        Friend Sub New()
            AEspacioDeSimulacion = Mat4.Identidad
            DesdeEspacioDeSimulacion = Mat4.Identidad
            Empaquetados = New Byte(3)() {}
            Ranura = -1
        End Sub

        ''' <summary>El vértice `i`, leyendo con el stride del buffer.</summary>
        Friend Function Vertice(i As Integer) As Vector128(Of Single)
            Dim off = (StrideBytes \ 4) * i
            Return Vector128.Create(Datos(off), Datos(off + 1), Datos(off + 2), 0.0F)
        End Function

        Friend Sub SetVertice(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideBytes \ 4) * i
            Datos(off) = v.GetElement(0)
            Datos(off + 1) = v.GetElement(1)
            Datos(off + 2) = v.GetElement(2)
        End Sub

        ''' <summary>La normal `i`, con **su propio** stride (`+0x44`).</summary>
        Friend Function Normal(i As Integer) As Vector128(Of Single)
            Dim off = (StrideNormalesBytes \ 4) * i
            Return Vector128.Create(Normales(off), Normales(off + 1), Normales(off + 2), 0.0F)
        End Function

        Friend Sub SetNormal(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideNormalesBytes \ 4) * i
            Normales(off) = v.GetElement(0)
            Normales(off + 1) = v.GetElement(1)
            Normales(off + 2) = v.GetElement(2)
        End Sub

        ''' <summary>La tangente `i`, con su propio stride (`+0x5C`).</summary>
        Friend Function Tangente(i As Integer) As Vector128(Of Single)
            Dim off = (StrideTangentesBytes \ 4) * i
            Return Vector128.Create(Tangentes(off), Tangentes(off + 1), Tangentes(off + 2), 0.0F)
        End Function

        ''' <summary>
        ''' ⛔ TRES floats, no cuatro. El kernel escribe 12 bytes por canal — `movsd` para (x, y) y
        ''' `movhlps` + `movss` para z (`0x141941991`/`995`/`998`) —, así que la cuarta lane no se
        ''' toca: con stride 12 sería el x del vértice siguiente.
        ''' </summary>
        Friend Sub SetTangente(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideTangentesBytes \ 4) * i
            Tangentes(off) = v.GetElement(0)
            Tangentes(off + 1) = v.GetElement(1)
            Tangentes(off + 2) = v.GetElement(2)
        End Sub

        ''' <summary>La bitangente `i`, con su propio stride (`+0x74`).</summary>
        Friend Function Bitangente(i As Integer) As Vector128(Of Single)
            Dim off = (StrideBitangentesBytes \ 4) * i
            Return Vector128.Create(Bitangentes(off), Bitangentes(off + 1), Bitangentes(off + 2), 0.0F)
        End Function

        Friend Sub SetBitangente(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideBitangentesBytes \ 4) * i
            Bitangentes(off) = v.GetElement(0)
            Bitangentes(off + 1) = v.GetElement(1)
            Bitangentes(off + 2) = v.GetElement(2)
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' EL ELEMENTO ENTERO DE 16 B — los kernels de LAYOUT SIMPLE.
        '
        ' ⛔ Los kernels que el despacho elige con el bit 0 de `+0x20`/`+0x48`/`+0x60`/`+0x78` puesto
        ' leen y escriben el elemento con UN `movups`: la lane `w` entra y sale. Sitios de escritura:
        ' `CopyVertices` `0x1418FAACD` (0x1418FA990) y `0x1418FAC8C`/`0x1418FACBA` (0x1418FAB10);
        ' `GatherAll` `0x1418F96B5` (0x1418F9560) y `0x1418F9870`/`0x1418F989A` (0x1418F96F0);
        ' `GatherSome` `0x1418FA4F8` (0x1418FA3B0) y `0x1418FA6AC`/`0x1418FA6E3` (0x1418FA530);
        ' `hclSkinOperator` `0x14191F6B9` (lineal P) y `0x141910174` (dual P), y los `movups` de
        ' `0x1419173B0`-`0x141917430` (lineal PNTB). Lectura: `0x14191E601` / `0x14190FD4F`.
        ' Los kernels no simples usan `movsd`+`movss` (12 B) y van por los `Set*` de arriba.
        ' -----------------------------------------------------------------------------------------

        ''' <summary>El vértice `i` con sus CUATRO lanes (`movups`, `0x14190FD4F`).</summary>
        Friend Function VerticeEntero(i As Integer) As Vector128(Of Single)
            Dim off = (StrideBytes \ 4) * i
            Return Vector128.Create(Datos(off), Datos(off + 1), Datos(off + 2), Datos(off + 3))
        End Function

        ''' <summary>Escribe las CUATRO lanes del vértice `i` (`movups`, `0x1418FAACD`).</summary>
        Friend Sub SetVerticeEntero(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideBytes \ 4) * i
            Datos(off) = v.GetElement(0)
            Datos(off + 1) = v.GetElement(1)
            Datos(off + 2) = v.GetElement(2)
            Datos(off + 3) = v.GetElement(3)
        End Sub

        ''' <summary>La normal `i` con sus cuatro lanes.</summary>
        Friend Function NormalEntera(i As Integer) As Vector128(Of Single)
            Dim off = (StrideNormalesBytes \ 4) * i
            Return Vector128.Create(Normales(off), Normales(off + 1), Normales(off + 2), Normales(off + 3))
        End Function

        ''' <summary>Escribe las cuatro lanes de la normal `i` (`movups`, `0x1418FACBA`).</summary>
        Friend Sub SetNormalEntera(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideNormalesBytes \ 4) * i
            Normales(off) = v.GetElement(0)
            Normales(off + 1) = v.GetElement(1)
            Normales(off + 2) = v.GetElement(2)
            Normales(off + 3) = v.GetElement(3)
        End Sub

        ''' <summary>La tangente `i` con sus cuatro lanes.</summary>
        Friend Function TangenteEntera(i As Integer) As Vector128(Of Single)
            Dim off = (StrideTangentesBytes \ 4) * i
            Return Vector128.Create(Tangentes(off), Tangentes(off + 1), Tangentes(off + 2), Tangentes(off + 3))
        End Function

        ''' <summary>Escribe las cuatro lanes de la tangente `i` (`movups`, `0x141917401`).</summary>
        Friend Sub SetTangenteEntera(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideTangentesBytes \ 4) * i
            Tangentes(off) = v.GetElement(0)
            Tangentes(off + 1) = v.GetElement(1)
            Tangentes(off + 2) = v.GetElement(2)
            Tangentes(off + 3) = v.GetElement(3)
        End Sub

        ''' <summary>La bitangente `i` con sus cuatro lanes.</summary>
        Friend Function BitangenteEntera(i As Integer) As Vector128(Of Single)
            Dim off = (StrideBitangentesBytes \ 4) * i
            Return Vector128.Create(Bitangentes(off), Bitangentes(off + 1), Bitangentes(off + 2), Bitangentes(off + 3))
        End Function

        ''' <summary>Escribe las cuatro lanes de la bitangente `i` (`movups`, `0x141917430`).</summary>
        Friend Sub SetBitangenteEntera(i As Integer, v As Vector128(Of Single))
            Dim off = (StrideBitangentesBytes \ 4) * i
            Bitangentes(off) = v.GetElement(0)
            Bitangentes(off + 1) = v.GetElement(1)
            Bitangentes(off + 2) = v.GetElement(2)
            Bitangentes(off + 3) = v.GetElement(3)
        End Sub

    End Class

    ' =============================================================================================

    Friend Module Buffers

        ''' <summary>
        ''' La tabla de creadores de `0x1418ED6E0` — **8 entradas**, indexada por
        ''' `hclBufferDefinition.type` (+0x18).
        ''' <para>⛔ El tipo **3** no es «no soportado»: el motor **assertea**
        ''' *«Unknown buffer type. Can't instantiate cloth.»* (`hclinstantiationutil.cpp:342`). Un
        ''' motor que lo trate como los demás se traga un archivo que el juego rechaza.</para>
        ''' </summary>
        Friend Enum TipoDeBuffer
            ''' <summary>1 — buffer del usuario, `0x1418C7040`, **`0x110` B**. ⭐ Es el que ata el
            ''' buffer al arreglo de partículas de la instancia.</summary>
            DelUsuario = 1
            ''' <summary>2 — otra forma de buffer del usuario, `0x1418C71A0`.</summary>
            DelUsuarioB = 2
            ''' <summary>3 — ⛔ ASSERT del motor. No se instancia.</summary>
            Invalido = 3
            ''' <summary>4 — virtual del contexto, `vtbl+0x20`.</summary>
            DelContextoA = 4
            ''' <summary>5 — virtual del contexto, `vtbl+0x28`.</summary>
            DelContextoB = 5
            ''' <summary>6 — propio del motor, 0x120 B.</summary>
            PropioA = 6
            ''' <summary>7 — propio del motor, 0x120 B.</summary>
            PropioB = 7
            ''' <summary>8 — propio del motor, `0x110` B, como el tipo 1.</summary>
            PropioChico = 8
        End Enum

        ''' <summary>
        ''' El creador del tipo 1 — `0x1418C7040`. **Ata el buffer al arreglo de partículas.**
        ''' <para>⭐⭐ `buf.Datos` queda apuntando al MISMO `Single()` de `inst.Posiciones`: no hay
        ''' copia. Por eso lo que un operador escriba en este buffer **es** la posición de la
        ''' partícula, y por eso el orden de los operadores importa.</para>
        ''' </summary>
        Friend Function CrearDelSimCloth(inst As Instancia) As Buffer
            Dim b As New Buffer()
            b.Datos = inst.Posiciones                         ' ⬅ EL MISMO arreglo (buf[+0x10])
            b.Cuenta = inst.NumParticulas                     ' buf[+0x18]
            b.StrideBytes = 16                                ' buf[+0x1C] = 0x10
            b.LayoutSimple = True                             ' buf[+0x20] = 1
            If inst.Normales IsNot Nothing Then
                b.Normales = inst.Normales                    ' buf[+0x38]
                b.StrideNormalesBytes = 16                    ' buf[+0x44] = 0x10
                b.LayoutSimpleNormales = True                 ' buf[+0x48] = 1
            End If
            Dim tris = inst.Datos?.TriangleIndices
            If tris IsNot Nothing AndAlso tris.Count > 0 Then
                Dim t(tris.Count - 1) As UShort
                For i = 0 To tris.Count - 1
                    t(i) = CUShort(tris(i))
                Next
                b.IndicesDeTriangulo = t                      ' buf[+0x28]
                b.NumTriangulos = tris.Count \ 3              ' buf[+0x30]
            End If
            Return b
        End Function

        ''' <summary>
        ''' La doble indirección `buffers[ buffers[idx].Ranura ]` — cap. 4.2, ejercida de verdad en
        ''' `hclLocalRangeConstraintSet::solve` (`0x141A01F5A` → `+0x100` → `0x141A01F65`).
        ''' <para>⛔ No se resuelve como identidad «porque en reposo lo es»: se resuelve como el
        ''' motor, y el gate G20 **mide** sobre el corpus que nunca difiera.</para>
        ''' <para>⛔⛔ **Y NO TIENE CAMINO DE ESCAPE.** El motor hace `movsxd rax, [r8+0x100]` y
        ''' `mov r11, [r10+rax*8]` sin chequear nada; si acá se devolviera «el buffer directo»
        ''' cuando la ranura no vale, una prenda mal registrada perdería su `LocalRange` **en
        ''' silencio** y ningún gate lo vería — el `Case Else` mudo de siempre (motor-61). En Debug
        ''' revienta con el índice y la ranura adentro, que es lo que corresponde.</para>
        ''' </summary>
        Friend Function Real(buffers As Buffer(), idx As Integer) As Buffer
            If buffers Is Nothing Then
                Throw New InvalidOperationException("Buffers.Real: no hay arreglo de buffers.")
            End If
            If idx < 0 OrElse idx >= buffers.Length Then
                Throw New InvalidOperationException(
                    $"Buffers.Real: índice {idx} fuera de [0, {buffers.Length - 1}].")
            End If
            Dim b = buffers(idx)
            If b Is Nothing Then
                Throw New InvalidOperationException($"Buffers.Real: la ranura {idx} está vacía.")
            End If
            Dim r = b.Ranura
            If r < 0 OrElse r >= buffers.Length Then
                Throw New InvalidOperationException(
                    $"Buffers.Real: el buffer {idx} declara la ranura {r}, fuera de " &
                    $"[0, {buffers.Length - 1}]. Se creó sin pasar por `PonerBuffer`.")
            End If
            Dim destino = buffers(r)
            If destino Is Nothing Then
                Throw New InvalidOperationException($"Buffers.Real: la ranura {r} está vacía.")
            End If
            Return destino
        End Function

        ''' <summary>
        ''' La tabla de descriptores POR CANAL que los dos `Convert` arman en la pila
        ''' (`0x14195E547`-`0x14195E590` y `0x14195EA2A`-`0x14195EA6F`): el canal `i` es
        ''' `buf + {0x10, 0x38, 0x50, 0x68}[i]`, y de ahí salen `ptr` (+0), `count` (+8) y
        ''' `stride` (+0xC).
        ''' <para>0 = posiciones · 1 = normales · 2 = tangentes · 3 = bitangentes.</para>
        ''' </summary>
        Friend Function FloatsDeCanal(b As Buffer, i As Integer) As Single()
            If b Is Nothing Then Return Nothing
            Select Case i
                Case 0 : Return b.Datos
                Case 1 : Return b.Normales
                Case 2 : Return b.Tangentes
                Case 3 : Return b.Bitangentes
            End Select
            Return Nothing
        End Function

        ''' <summary>El mismo canal, visto como bytes — <see cref="Buffer.Empaquetados"/>.</summary>
        Friend Function BytesDeCanal(b As Buffer, i As Integer) As Byte()
            If b Is Nothing OrElse b.Empaquetados Is Nothing Then Return Nothing
            If i < 0 OrElse i >= b.Empaquetados.Length Then Return Nothing
            Return b.Empaquetados(i)
        End Function

        ''' <summary>El stride del canal `i`, en BYTES — `+0x1C`, `+0x44`, `+0x5C`, `+0x74`.</summary>
        Friend Function StrideDeCanal(b As Buffer, i As Integer) As Integer
            If b Is Nothing Then Return 0
            Select Case i
                Case 0 : Return b.StrideBytes
                Case 1 : Return b.StrideNormalesBytes
                Case 2 : Return b.StrideTangentesBytes
                Case 3 : Return b.StrideBitangentesBytes
            End Select
            Return 0
        End Function

        ''' <summary>
        ''' El conteo del canal `i` — `+0x18`, `+0x40`, `+0x58`, `+0x70`.
        ''' <para>⛔ **El `.exe` tiene CUATRO conteos y este motor modela UNO.** Es una
        ''' simplificación con medición atrás, no una ley: el creador de buffers del motor
        ''' escribe el MISMO número en los dos que se le ven (`0x1418C708A` pone `[buf+0x18]` y
        ''' `0x1418C70CA` pone `[buf+0x40]`, las dos veces con `[inst+0x20]`). Si alguna vez
        ''' apareciera un buffer con conteos distintos por canal, esto divergiría — se dice, no se
        ''' tapa.</para>
        ''' </summary>
        Friend Function CuentaDeCanal(b As Buffer, i As Integer) As Integer
            If b Is Nothing Then Return 0
            Return b.Cuenta
        End Function

        ''' <summary>`setBuffer(idx, buf)` — `0x1418C8530`: además de guardarlo, le escribe la
        ''' ranura.</summary>
        Friend Sub PonerBuffer(buffers As Buffer(), idx As Integer, b As Buffer)
            buffers(idx) = b
            If b IsNot Nothing Then b.Ranura = idx            ' 0x1418C8530
        End Sub

    End Module

End Namespace

