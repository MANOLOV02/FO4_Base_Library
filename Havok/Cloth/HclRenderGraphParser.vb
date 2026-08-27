' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' Operadores de render/skin del HKX de tela. Lo llama HclClothPackageParser.
' ⛔ SI ESTA EN LA RUTA DEL RENDER: `HavokClothSimulation` consume `HclSkinVertice_Class` y
' `HclObjectSpaceSkinQuantizedVectorGraph_Class` para skinnear la malla de referencia, y esa sim
' corre desde `Render.vb` dentro de su `#If DEBUG`. La cabecera decia lo contrario.
'
' ⛔ TODO CAMPO DECLARADO SALE DEL OBJETO GENERADO. No hay un solo offset escrito aca: los
' `hclObjectSpaceSkinPNOperator`, `hclObjectSpaceDeformer*` y `hclSimpleMeshBoneDeformOperator`
' se leen con `HkObj_*.Read`, que resuelve la tabla de la reflexion del .exe del juego.
'
' LO QUE SIGUE ESCRITO A MANO, Y POR QUE — son DECODIFICACIONES, no offsets: la reflexion dice
' donde esta el campo y de que ancho es, pero no que significan los bits de adentro.
'
'   1. `hclSimpleMeshBoneDeformOperator.triangleBonePairs`: los dos uint16 vienen empaquetados
'      — `boneOffset` trae el indice de hueso en los bits 6..15 y seis bits de flags abajo,
'      `triangleOffset` el indice de triangulo por SEIS. Reverse engineering, consistente sobre
'      el corpus, SIN cita del .exe. Anotado tambien en el punto de uso.
'
'   2. El desentrelazado SIMD de los carriles de los `*BlendEntryBlock` y la dequantizacion
'      `float(v << 16) x bitcast_float(w << 16)` de `hclObjectSpaceDeformerLocalBlockPN`.
'      La escala de cuantizacion SI esta medida — ver `PositionScaleFromW`.
'
' Las `hcl*` no existen en la reflexion de Skyrim: ese juego no tiene motor de cloth.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Friend NotInheritable Class HclRenderGraphParser_Class

    ''' <summary>
    ''' ⛔⛔ LOS BLOQUES DEL DEFORMER SALEN DEL OBJETO GENERADO, NO DE UN STRIDE A MANO.
    '''
    ''' Antes esto leía los cuatro arrays del `hclObjectSpaceDeformer` como bloques de bytes crudos
    ''' con el tamaño escrito acá (224 / 176 / 128 / 64 / 256) y los desarmaba con `BitConverter`.
    ''' La reflexión del .exe declara los cinco por campo y el generador los emite:
    '''
    '''     hclObjectSpaceDeformerFourBlendEntryBlock   0xE0=224  vertexIndices[16] boneIndices[64] boneWeights[64]
    '''     hclObjectSpaceDeformerThreeBlendEntryBlock  0xB0=176  vertexIndices[16] boneIndices[48] boneWeights[48]
    '''     hclObjectSpaceDeformerTwoBlendEntryBlock    0x80=128  vertexIndices[16] boneIndices[32] boneWeights[32]
    '''     hclObjectSpaceDeformerOneBlendEntryBlock    0x40= 64  vertexIndices[16] boneIndices[16]   (sin pesos)
    '''     hclObjectSpaceDeformerLocalBlockPN         0x100=256  localPosition[64] localNormal[64]
    '''
    ''' Lo que NO describe la reflexión —y por eso se queda acá— es la DECODIFICACIÓN: el entrelazado
    ''' SIMD de los carriles y la dequantización `float(v &lt;&lt; 16) × bitcast_float(w &lt;&lt; 16)`.
    ''' </summary>
    Friend Shared Function ParseObjectSpaceSkinPNOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclObjectSpaceSkinPNOperatorGraph_Class
        ' ⛔ EL GUARDA POR NOMBRE Y LA LECTURA SON LA MISMA LLAMADA, Y LA HACE LA PROPIA CLASE.
        ' `Leer` compara contra su `NombreDeClase` —emitido desde la reflexion del .exe— y acepta
        ' tambien las subclases que la tabla declara. Antes esto pasaba por un generico que
        ' resolvia el lector por REFLEXION a partir del `Type`.
        Dim o = Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNOperator.Leer(graph, source)
        If o Is Nothing OrElse o.ObjectSpaceDeformer Is Nothing Then Return Nothing
        Dim d = o.ObjectSpaceDeformer

        Dim result As New HclObjectSpaceSkinPNOperatorGraph_Class With {.Operador = o}

        ' ⛔ LAS CUATRO FAMILIAS. `hclObjectSpaceDeformer` declara CUATRO arrays de entradas
        ' (four/three/two/oneBlendEntries) y acá se leían TRES. Los vértices con UNA sola influencia
        ' quedaban SIN skinnear: no entraban al diccionario, así que la partícula que los usaba caía
        ' al DefaultClothPose, que está en otro espacio.
        ' Las cuatro familias, desentrelazadas. Son LOCALES: lo que sale de aca es la lista de
        ' vertices, y guardar ademas los subconjuntos era tener el mismo vertice dos veces.
        Dim porFamilia = New List(Of List(Of HclSkinVertice_Class))() From {
            SubconjuntosDe(d.FourBlendEntries.Select(Function(b) New BloquePesado(b.VertexIndices, b.BoneIndices, b.BoneWeights)).ToList(), 4),
            SubconjuntosDe(d.ThreeBlendEntries.Select(Function(b) New BloquePesado(b.VertexIndices, b.BoneIndices, b.BoneWeights)).ToList(), 3),
            SubconjuntosDe(d.TwoBlendEntries.Select(Function(b) New BloquePesado(b.VertexIndices, b.BoneIndices, b.BoneWeights)).ToList(), 2),
            SubconjuntosDeUnaInfluencia(d.OneBlendEntries)
        }

        result.Vertices.AddRange(VerticesDe(result, porFamilia))
        Return result
    End Function


    ''' <summary>Los tres arrays de un bloque de entradas con peso, ya leídos por nombre.</summary>
    Private NotInheritable Class BloquePesado
        Public ReadOnly Vertices As List(Of Integer)
        Public ReadOnly Huesos As List(Of Integer)
        Public ReadOnly Pesos As List(Of Integer)
        Public Sub New(vertices As List(Of Integer), huesos As List(Of Integer), pesos As List(Of Integer))
            Me.Vertices = vertices
            Me.Huesos = huesos
            Me.Pesos = pesos
        End Sub
    End Class

    ''' <summary>
    ''' ⛔ EL ENTRELAZADO SIMD, QUE ES LO ÚNICO QUE LA REFLEXIÓN NO DICE.
    ''' Un bloque son 16 carriles. `boneIndices` viene por INFLUENCIA (los 16 carriles de la
    ''' influencia 0, después los de la 1…) y `boneWeights` viene por CARRIL (las n influencias del
    ''' carril 0, después las del 1…). No es simétrico y por eso los dos índices son distintos.
    ''' </summary>
    Private Shared Function SubconjuntosDe(bloques As List(Of BloquePesado), influenceCount As Integer) As List(Of HclSkinVertice_Class)
        Dim result As New List(Of HclSkinVertice_Class)
        If bloques Is Nothing OrElse influenceCount <= 0 Then Return result
        For Each b In bloques
            If b Is Nothing OrElse b.Vertices Is Nothing OrElse b.Huesos Is Nothing OrElse b.Pesos Is Nothing Then Continue For
            If b.Vertices.Count < 16 OrElse b.Huesos.Count < influenceCount * 16 OrElse b.Pesos.Count < influenceCount * 16 Then Continue For

            For lane = 0 To 15
                Dim v As New HclSkinVertice_Class With {
                    .SlotIndex = lane,
                    .VertexIndex = CUShort(b.Vertices(lane) And &HFFFF)}
                For influence = 0 To influenceCount - 1
                    v.TransformIndices.Add(CUShort(b.Huesos((influence * 16) + lane) And &HFFFF))
                Next
                For influence = 0 To influenceCount - 1
                    v.WeightBytes.Add(CByte(b.Pesos((lane * influenceCount) + influence) And &HFF))
                Next
                result.Add(v)
            Next
        Next
        Return result
    End Function

    ''' <summary>
    ''' `hclObjectSpaceDeformerOneBlendEntryBlock`: `vertexIndices[16]` y `boneIndices[16]`, y SIN
    ''' array de pesos, porque con una sola influencia el peso es 1 por definición.
    ''' </summary>
    Private Shared Function SubconjuntosDeUnaInfluencia(bloques As List(Of Havok.Canon.Objects.HkObj_HclObjectSpaceDeformerOneBlendEntryBlock)) As List(Of HclSkinVertice_Class)
        ' ⛔ EL DESENTRELAZADO ES `SubconjuntosDe`, NO UNA SEGUNDA COPIA. Lo unico propio del bloque
        ' de UNA influencia es que no trae array de pesos — con una sola influencia el peso es 1 por
        ' definicion — asi que se adapta con los 16 carriles en 255 y se usa la misma ley.
        Dim adaptados As New List(Of BloquePesado)
        If bloques IsNot Nothing Then
            For Each b In bloques
                If b Is Nothing OrElse b.VertexIndices Is Nothing OrElse b.BoneIndices Is Nothing Then Continue For
                adaptados.Add(New BloquePesado(b.VertexIndices, b.BoneIndices, Enumerable.Repeat(255, 16).ToList()))
            Next
        End If
        Return SubconjuntosDe(adaptados, 1)
    End Function

    ''' <summary>Desempaqueta un `hclSimpleMeshBoneDeformOperator`: los pares hueso/triangulo y el
    ''' bind de cada uno. Nothing si el bloque declara otra clase o no trae pares.</summary>
    Friend Shared Function ParseSimpleMeshBoneDeformOperator(graph As HkxObjectGraph_Class,
                                                             source As HkxVirtualObjectGraph_Class,
                                                             Optional skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing) As HclSimpleMeshBoneDeformOperatorGraph_Class
        ' ⛔ IDEM: el guarda por nombre lo hace `HkObj_*.Leer`, mas abajo.

        ' ⛔ TODO LO DECLARADO SALE DEL OBJETO GENERADO. `triangleBonePairs` es
        ' `boneOffset,0,uint16 ; triangleOffset,2,uint16` en la reflexion y `localBoneTransforms` es
        ' `array,matrix4`: los dos se leian a mano aca, byte por byte, con el mismo resultado.
        Dim o = Havok.Canon.Objects.HkObj_HclSimpleMeshBoneDeformOperator.Leer(graph, source)
        If o Is Nothing OrElse o.TriangleBonePairs Is Nothing OrElse o.TriangleBonePairs.Count = 0 Then Return Nothing

        Dim result As New HclSimpleMeshBoneDeformOperatorGraph_Class With {.Operador = o}
        Dim binds = If(o.LocalBoneTransforms, New List(Of Single()))

        ' ⛔ LO QUE LA REFLEXION NO DICE: los dos uint16 vienen EMPAQUETADOS. `boneOffset` trae el
        ' indice de hueso en los bits 6..15 y seis bits de flags abajo; `triangleOffset` trae el
        ' indice de triangulo por SEIS. Reverse engineering, internamente consistente sobre el
        ' corpus — no sale de una cita del .exe.
        Dim i = -1
        For Each par In o.TriangleBonePairs
            i += 1
            If par Is Nothing Then Continue For
            Dim packedBone = CUShort(par.BoneOffset And &HFFFF)
            Dim packedValue = CUShort(par.TriangleOffset And &HFFFF)
            Dim boneIndex = packedBone \ 64
            Dim boneName = String.Empty
            If Not IsNothing(skeleton) AndAlso Not IsNothing(skeleton.Bones) AndAlso boneIndex >= 0 AndAlso boneIndex < skeleton.Bones.Count Then
                boneName = skeleton.Bones(boneIndex).Name
            End If

            ' ⛔ SOLO LO DESEMPAQUETADO. Los cuatro `Packed*` eran copias crudas de
            ' `par.BoneOffset` / `par.TriangleOffset` (y dos derivados de un `And` y un `Mod`), con un
            ' solo consumidor: una linea de log. El dato crudo vive en `Operador`; duplicarlo aca es
            ' exactamente lo que el doc de esta clase dice que no se hace.
            result.BoneMappings.Add(New HclSimpleMeshBoneDeformMapping_Class With {
                .EntryIndex = i,
                .BoneIndex = boneIndex,
                .TriangleIndex = packedValue \ 6,
                .BoneName = boneName,
                .BindMatrix = If(i < binds.Count, binds(i), Nothing)
            })
        Next

        Return result
    End Function

    ''' <summary>
    ''' Dequantizacion de la POSICION del bloque local del ObjectSpaceDeformer.
    '''
    ''' <para>⭐ LEIDA DEL MOTOR, no ajustada al dato. `TtObject Space Deform` @0x141939390, bucle de
    ''' cuatro influencias en 0x1419399E0:</para>
    ''' <code>
    '''   movsd     xmm0, [ptr]        ; 8 bytes = los 4 int16 del vertice (x,y,z,w)
    '''   punpcklwd xmm2, xmm0         ; xmm2=0 ⇒ cada int16 queda en los 16 bits ALTOS de un dword
    '''   pshufd    xmm0, xmm2, 0xFF   ; lane 3 (el tag `w`) broadcast, SIN convertir
    '''   cvtdq2ps  xmm1, xmm2         ; float(v &lt;&lt; 16)
    '''   mulps     xmm1, xmm0         ; × el tag interpretado como PATRON DE BITS de un float
    ''' </code>
    ''' <para>O sea: <c>valor = float(v &lt;&lt; 16) × bitcast_float(w &lt;&lt; 16)</c>. El `w` NO es un flag:
    ''' es la MITAD ALTA de un float IEEE-754 (mantisa baja en cero) que multiplica al vector entero.
    ''' Es el truco clasico para guardar un exponente en 16 bits.</para>
    '''
    ''' <para>⛔ LO QUE HABIA ACA ESTABA ADIVINADO y por eso rompia. Decia: <i>"la escala es per-vertice
    ''' {256, 512}, seleccionada por el bit 7"</i> — un ajuste de DOS puntos (w=0x3380→256,
    ''' w=0x3300→512) presentado como ley. Los dos casos salen bien con la formula real
    ''' (bitcast(0x33800000)=2^-24, ×65536 = 1/256 ✓ ; bitcast(0x33000000)=2^-25 ⇒ 1/512 ✓), pero
    ''' cualquier otro exponente se decodificaba con la escala de al lado. MEDIDO en
    ''' HouseDress\Dress.nif: 24 de 321 particulas salian con la posicion ×4 EXACTO (un exponente de
    ''' diferencia, 1/1024 leido como 1/256), y con UNA sola alcanzaba para que el triangulo del
    ''' cloth-bone del ruedo quedara convertido en una astilla de 100 unidades y la pollera se abriera
    ''' en abanico.</para>
    ''' <para>⛔ LAS NORMALES VAN POR ACA TAMBIEN. El doc decia "van con 32767 fijo (ver el
    ''' llamador)" y el llamador hace exactamente lo contrario desde que se midio el deformer: las
    ''' unicas dos constantes que multiplican en `0x14193C5E0` son `65536.0` y `1/255`, y no hay
    ''' ningun 32767 en el camino. El doc describia la ley vieja.</para>
    ''' </summary>
    Private Shared Function PositionScaleFromW(values As IReadOnlyList(Of Short)) As Double?
        ' ⛔ NOTHING, NO 256. El 256 es el mismo numero que el parrafo de arriba declara ADIVINADO:
        ' reponerlo cuando el tag no sirve es decodificar con la escala de al lado, que es exactamente
        ' el defecto que costo 24 particulas x4 en `Dress.nif` y una pollera abierta en abanico. Un
        ' bloque cuyo tag no se puede leer NO se decodifica, y el vertice se cuenta como perdido.
        If values Is Nothing OrElse values.Count < 4 Then Return Nothing
        ' El multiplicador que aplica el motor: bitcast_float(w << 16) escalado por el << 16 de los datos.
        Dim mul = CDbl(BitConverter.Int32BitsToSingle(CInt(values(3)) << 16)) * 65536.0R
        ' `DecodeQuantizedVector3` DIVIDE, asi que se devuelve el reciproco.
        If mul <= 0.0R OrElse Double.IsNaN(mul) OrElse Double.IsInfinity(mul) Then Return Nothing
        Return 1.0R / mul
    End Function

    ''' <summary>
    ''' ⛔ EL APAREO QUE DICTA `controlBytes`, Y LA LISTA DE VERTICES QUE SALE DE AHI.
    '''
    ''' <para>`hclObjectSpaceDeformer.controlBytes` dice, por bloque de 16, de que familia es: 0 son
    ''' cuatro influencias, 1 tres, 2 dos, 3 una. Ese byte es lo unico que aparea el bloque de
    ''' influencias con el bloque local que trae su posicion y su normal.</para>
    '''
    ''' <para>⛔⛔ RELLENO DEL BLOQUE PARCIAL. Los bloques son de 16 vertices FIJOS y el ultimo de
    ''' cada familia viene a medias: Havok rellena los slots sobrantes REPITIENDO el ultimo indice
    ''' valido. MEDIDO en HouseDress\Dress.nif: el bloque 20 termina en `...,336,337,337,337,337` y
    ''' el 21 en `338,339,339,...` (nueve veces 339). Escribirlos igual PISA la entrada buena de ese
    ''' vertice con la posicion local de un slot de relleno, y con UNO alcanza para que el triangulo
    ''' de un cloth-bone quede convertido en una astilla de 100 unidades.</para>
    ''' <para>⛔ Filtrar por `startVertexIndex`/`endVertexIndex` NO alcanza: el relleno repite un
    ''' indice que esta DENTRO del rango (339 &lt;= endVertexIndex = 339). La senal es la REPETICION,
    ''' no el rango: un vertice no puede aparecer dos veces en un deformer.</para>
    ''' </summary>
    Private Shared Function VerticesDe(source As HclObjectSpaceSkinPNOperatorGraph_Class,
                                       porFamilia As List(Of List(Of HclSkinVertice_Class))) As List(Of HclSkinVertice_Class)
        Dim result As New List(Of HclSkinVertice_Class)
        If IsNothing(source) Then Return result

        Dim tomados(porFamilia.Count - 1) As Integer
        Dim control = If(source.Operador?.ObjectSpaceDeformer?.ControlBytes, New List(Of Integer)())
        Dim bloques = If(source.Operador?.LocalPNs, New List(Of Havok.Canon.Objects.HkObj_HclObjectSpaceDeformerLocalBlockPN)())
        Dim nBloques = Math.Max(control.Count, bloques.Count)

        If Logger.Enabled Then
            ' Histograma de los tags `w` del vector de POSICION. La regla actual mira UN bit y
            ' ofrece dos escalas; si aca aparecen mas de dos valores distintos, la regla esta
            ' incompleta por construccion y no hace falta discutirlo.
            Dim ws As New List(Of String)
            For Each b In bloques
                If b Is Nothing Then Continue For
                For carril = 0 To 7
                    ws.Add("0x" & (HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlot(b, carril, 0)(3) And &HFFFF).ToString("X4"))
                    ws.Add("0x" & (HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlot(b, carril, 1)(3) And &HFFFF).ToString("X4"))
                Next
            Next
            Dim wl = ws
            Logger.LogLazy(Function() "[CLOTH-WTAG] " & String.Join(",", wl))
        End If

        ' Vertices que quedaron SIN DECODIFICAR porque el tag de escala del bloque no sirve. Se
        ' cuentan en vez de decodificarse con la escala de al lado (ver `PositionScaleFromW`).
        Dim sinEscala = 0, sinEscalaNormal = 0
        For iBloque = 0 To nBloques - 1
            Dim familia = If(iBloque < control.Count, control(iBloque) And &HFF, -1)
            If familia < 0 OrElse familia >= porFamilia.Count Then Continue For
            Dim lista = porFamilia(familia)
            Dim desde = tomados(familia)
            If desde + 16 > lista.Count Then Continue For
            tomados(familia) = desde + 16

            Dim local As Havok.Canon.Objects.HkObj_HclObjectSpaceDeformerLocalBlockPN = Nothing
            If iBloque < bloques.Count Then local = bloques(iBloque)
            If local Is Nothing Then Continue For

            For slot = 0 To 15
                Dim v = lista(desde + slot)
                If slot > 0 AndAlso v.VertexIndex = lista(desde + slot - 1).VertexIndex Then Exit For
                v.BlockIndex = iBloque
                Dim carril = slot \ 2, par = slot Mod 2
                Dim pos = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlot(local, carril, par)
                Dim nor = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlot(local, carril + 8, par)
                ' ⛔ SIN ESCALA NO HAY VERTICE. Antes se caia al 256 "historico" y el vertice salia
                ' decodificado con la escala equivocada; ahora se saltea y se cuenta.
                ' ⛔ Y CADA UNA CON LA SUYA: el tag de la NORMAL no puede decidir si se decodifica la
                ' POSICION. Estuvieron acopladas un rato (`Not escP.HasValue OrElse Not escN.HasValue`)
                ' y eso descartaba el vertice ENTERO por un `w` de normal degenerado — el vertice
                ' desaparecia del mapa de skin, la particula que lo usa se quedaba sin puente, y
                ' `[CLOTH-ANCLASINMAPA]` acusaba a las anclas de un defecto de decodificacion.
                Dim escP = PositionScaleFromW(pos)
                Dim escN = PositionScaleFromW(nor)
                If Not escP.HasValue Then
                    sinEscala += 1
                    Continue For
                End If
                v.Position = DecodeQuantizedVector3(pos, escP.Value, carril, par)
                ' ⛔⛔ LA NORMAL SE DECODIFICA COMO LA POSICION, NO CON UN 32767 INVENTADO.
                ' En el deformer que consume estos bloques (0x14193C5E0) las UNICAS dos constantes
                ' que multiplican son `65536.0` (0x14262BA50) y `1/255` (0x142492850): la primera
                ' es el `<< 16` del reinterpretado y la segunda la normalizacion de los PESOS. No
                ' hay ningun 32767 en el camino, asi que la normal comparte `PositionScaleFromW`.
                ' La normal se omite sola si su tag no sirve: el vertice sale con la posicion buena y
                ' la normal en cero, que es lo que hacia antes de todo esto.
                If escN.HasValue Then
                    v.Normal = DecodeQuantizedVector3(nor, escN.Value, carril + 8, par)
                Else
                    sinEscalaNormal += 1
                End If
                result.Add(v)
            Next
        Next

        If Logger.Enabled Then
            Dim hist As New Dictionary(Of Integer, Integer)
            For Each bt In control
                Dim k = bt And &HFF
                hist(k) = If(hist.ContainsKey(k), hist(k) + 1, 1)
            Next
            Dim h = String.Join(" ", hist.OrderBy(Function(kv) kv.Key).Select(Function(kv) $"tipo{kv.Key}x{kv.Value}"))
            Dim ultimos = String.Join(",", result.Skip(Math.Max(0, result.Count - 24)).Select(Function(x) x.VertexIndex.ToString()))
            Dim sv = source.Operador.ObjectSpaceDeformer.StartVertexIndex, ev = source.Operador.ObjectSpaceDeformer.EndVertexIndex
            Dim nv = result.Count, nb = nBloques, nlb = bloques.Count, nse = sinEscala, nsn = sinEscalaNormal
            If nse > 0 Then Logger.LogLazy(Function() $"[CLOTH-SKINBLK-SINESCALA] {nse} vertices sin decodificar: el tag `w` de POSICION no da una escala usable")
            ' ⛔ OTRO TAG: son dos leyes distintas. "el vertice desaparecio del mapa de skin" y "el
            ' vertice esta y su normal quedo en cero" no se pueden contar juntos — es la confusion que
            ' el arreglo de arriba deshizo en el CODIGO y que compartir el tag rehacia en el LOG.
            If nsn > 0 Then Logger.LogLazy(Function() $"[CLOTH-SKINBLK-SINESCALAN] {nsn} vertices con la NORMAL en cero: el tag `w` de normal no da una escala usable")
            Logger.LogLazy(Function() $"[CLOTH-SKINBLK-LAST] ultimos indices: {ultimos}")
            Logger.LogLazy(Function() $"[CLOTH-SKINBLK-RANGE] startVertexIndex={sv} endVertexIndex={ev}")
            Logger.LogLazy(Function() $"[CLOTH-SKINBLK] bloques={nb} localBlocks={nlb} vertices={nv} · controlBytes: {h}")
        End If

        Return result
    End Function

    Private Shared Function DecodeQuantizedVector3(values As IReadOnlyList(Of Short), scale As Double, laneIndex As Integer, pairIndex As Integer) As HclObjectSpaceSkinQuantizedVectorGraph_Class
        Dim result As New HclObjectSpaceSkinQuantizedVectorGraph_Class With {
            .LaneIndex = laneIndex,
            .PairIndex = pairIndex,
            .Scale = scale
        }

        If values Is Nothing OrElse values.Count < 3 Then Return result

        For Each value In values
            result.RawInt16Values.Add(value)
        Next

        result.X = values(0) / scale
        result.Y = values(1) / scale
        result.Z = values(2) / scale
        Return result
    End Function

End Class


''' <summary>
''' ⛔ EL RESULTADO DE **DECODIFICAR** UN `hclObjectSpaceSkinPNOperator`.
'''
''' <para>Lo que el archivo DECLARA vive en `Operador` — el objeto generado — y no se copia a
''' ningun lado: `name`, `outputBufferIndex`, `transformSetIndex`, `transformSubset` (la paleta de
''' huesos), `boneFromSkinMeshTransforms`, y el `hclObjectSpaceDeformer` entero con sus cuatro
''' familias de entradas, sus `controlBytes` y su rango de vertices.</para>
'''
''' <para>Aca queda SOLO lo que la reflexion no describe: el entrelazado SIMD de los 16 carriles, la
''' dequantizacion de los bloques locales, y el apareo bloque↔subconjunto que dicta `controlBytes`.
''' Los `Resolved*` / `Covered*` son analisis del package parser — se cruzan con el esqueleto y con
''' `hclMoveParticlesOperator` — y tampoco salen del archivo.</para>
''' </summary>
Public Class HclObjectSpaceSkinPNOperatorGraph_Class
    Public Property Operador As Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNOperator

    ''' <summary>Los vertices del skin, cada uno con su posicion, su normal y sus influencias.
    ''' Es lo que caminan TODOS los consumidores.</summary>
    Public ReadOnly Property Vertices As New List(Of HclSkinVertice_Class)
    ''' <summary>Cuantos vertices del skin quedaron cubiertos por el operador. Analisis del package
    ''' parser: no sale del archivo. (El `&lt;summary&gt;` de "los carriles crudos" que estaba aca colgaba de
    ''' `...LocalBlockLaneGraph_Class`, que se borro; la ley vive en <see cref="CarrilDe"/>.)</summary>
    Public Property CoveredVertexCount As Integer

    ''' <summary>Los nombres de hueso que se pudieron resolver contra el esqueleto. Analisis del
    ''' package parser: no sale del archivo.</summary>
    Public ReadOnly Property ResolvedBoneNames As New List(Of String)

    ''' <summary>
    ''' ⛔⛔ LOS CARRILES SE LEEN DEL BLOQUE DECLARADO. NO SE MATERIALIZA NADA.
    ''' <para>`hclObjectSpaceDeformerLocalBlockPN` declara `localPosition int16[64]` y
    ''' `localNormal int16[64]`. Cada CUATRO int16 son un vector (x, y, z, w): 16 vectores de
    ''' cada cosa por bloque. El carril L son los 8 int16 en `(L Mod 8) * 8` — de la posicion
    ''' si L&lt;8, de la normal si L&gt;=8.</para>
    ''' <para>Antes esto copiaba los 128 int16 de cada bloque a dos clases
    ''' (`...LocalBlockPNGraph_Class` y `...LocalBlockLaneGraph_Class`), y cada carril a CUATRO
    ''' listas que eran los mismos ocho numeros rebanados distinto — por bloque y por carril,
    ''' para un volcado. Las dos clases se borraron: el dato lo entrega el objeto generado.</para>
    ''' </summary>
    Public Shared Function CarrilDe(bloque As Havok.Canon.Objects.HkObj_HclObjectSpaceDeformerLocalBlockPN, carril As Integer) As Short()
        If bloque Is Nothing OrElse carril < 0 OrElse carril > 15 Then Return Array.Empty(Of Short)()
        Dim fuente = If(carril < 8, bloque.LocalPosition, bloque.LocalNormal)
        If fuente Is Nothing OrElse fuente.Count < 64 Then Return Array.Empty(Of Short)()
        Dim base_ = (carril Mod 8) * 8
        Dim r(7) As Short
        For i = 0 To 7
            r(i) = CShort(fuente(base_ + i))
        Next
        Return r
    End Function

    ''' <summary>Los cuatro int16 (x, y, z, w) de un slot: `par` elige la mitad del carril.</summary>
    Public Shared Function VectorDeSlot(bloque As Havok.Canon.Objects.HkObj_HclObjectSpaceDeformerLocalBlockPN, carril As Integer, par As Integer) As Short()
        Dim c = CarrilDe(bloque, carril)
        If c.Length < 8 Then Return Array.Empty(Of Short)()
        Dim b = par * 4
        Return New Short() {c(b), c(b + 1), c(b + 2), c(b + 3)}
    End Function
End Class

''' <summary>
''' ⛔ UN VERTICE DEL SKIN, YA DECODIFICADO — Y EL UNICO SITIO DONDE VIVE.
'''
''' <para>Antes el mismo vertice estaba TRES veces: en `subset.VertexIndices(slot)`, en
''' `subset.VertexInfluences(slot).VertexIndex` y en `block.VertexEntries[].VertexIndex`. Y como la
''' posicion vivia en la tercera y las influencias en la segunda, los CINCO consumidores tenian que
''' cruzar `entry(slot)` con `lane(slot)` a mano, cada uno con su propia guarda de rango.</para>
''' </summary>
Public Class HclSkinVertice_Class
    ''' <summary>Indice en el buffer de skin. NO es el indice de particula: el puente es
    ''' `hclMoveParticlesOperator.vertexParticlePairs`.</summary>
    Public Property VertexIndex As UShort
    ''' <summary>Bloque de 16 y carril dentro del bloque, para poder volver a los int16 crudos.</summary>
    Public Property BlockIndex As Integer = -1
    Public Property SlotIndex As Integer = -1
    Public Property Position As HclObjectSpaceSkinQuantizedVectorGraph_Class
    Public Property Normal As HclObjectSpaceSkinQuantizedVectorGraph_Class
    ''' <summary>`boneIndices` viene por INFLUENCIA y `boneWeights` por CARRIL: no es simetrico,
    ''' y desentrelazarlo es lo unico que la reflexion no dice.</summary>
    Public ReadOnly Property TransformIndices As New List(Of UShort)
    Public ReadOnly Property WeightBytes As New List(Of Byte)
    ''' <summary>Analisis del package parser: el nombre sale del esqueleto, no del archivo.</summary>
    Public ReadOnly Property ResolvedBoneNames As New List(Of String)
End Class



Public Class HclObjectSpaceSkinQuantizedVectorGraph_Class
    Public Property LaneIndex As Integer
    Public Property PairIndex As Integer
    Public Property Scale As Double
    Public Property X As Double
    Public Property Y As Double
    Public Property Z As Double
    Public ReadOnly Property RawInt16Values As New List(Of Short)
End Class

''' <summary>
''' ⛔ EL RESULTADO DE **DECODIFICAR** UN `hclSimpleMeshBoneDeformOperator`.
'''
''' <para>Lo declarado vive en `Operador`: `inputBufferIdx`, `outputTransformSetIdx`,
''' `triangleBonePairs` y `localBoneTransforms`. Aca solo esta el DESEMPAQUETADO de cada par — el
''' indice de hueso en los bits altos de `boneOffset` y el de triangulo en `triangleOffset \ 6` —
''' y el nombre resuelto contra el esqueleto, que no sale del archivo.</para>
''' <para>El operador y lo UNICO que este parser agrega: los pares hueso/triangulo ya
''' desempaquetados. `Operador` se expone tal cual — no se copia un solo campo suyo.</para>
''' </summary>
Public Class HclSimpleMeshBoneDeformOperatorGraph_Class
    Public Property Operador As Havok.Canon.Objects.HkObj_HclSimpleMeshBoneDeformOperator
    Public Property BoneMappings As New List(Of HclSimpleMeshBoneDeformMapping_Class)
End Class

''' <summary>UN par hueso/triangulo YA DESEMPAQUETADO. ⛔ Nada de lo crudo se copia aca: los
''' `boneOffset`/`triangleOffset` del archivo salen de `Operador.TriangleBonePairs(EntryIndex)`.</summary>
Public Class HclSimpleMeshBoneDeformMapping_Class
    Public Property EntryIndex As Integer
    Public Property BoneIndex As Integer
    Public Property TriangleIndex As Integer
    Public Property BoneName As String
    Public Property BindMatrix As Single()
    Public Property ResolvedTriangle As HclTrianguloDeSim_Class
End Class








