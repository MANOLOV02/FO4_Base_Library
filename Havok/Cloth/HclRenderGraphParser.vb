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
    ''' <para>⭐ LAS CUATRO VARIANTES — `P` (22), `PN` (23), `PNT` (24) y `PNTB` (25).</para>
    ''' <para>⛔ Se prueban UNA POR UNA porque son HERMANAS, no derivadas: `Leer` acepta la clase
    ''' exacta o las que DERIVAN de ella, y la reflexión dice `padre=hclObjectSpaceSkinOperator`
    ''' para las cuatro. Probar sólo `PN` deja a las otras tres en `Nothing`.</para>
    ''' <para>Comparten TODO salvo cuántas listas de vectores locales traen; el layout de
    ''' influencias es el mismo y lo decodifica el mismo código.</para>
    ''' <para>⚠️ El corpus vanilla trae **cero** de las tres que no son `PN` (censo M1). Van igual
    ''' porque un mod puede traerlas.</para>
    ''' </summary>
    Friend Shared Function ParseObjectSpaceSkinOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclObjectSpaceSkinPNOperatorGraph_Class
        Dim result As New HclObjectSpaceSkinPNOperatorGraph_Class

        Dim oPn = Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNOperator.Leer(graph, source)
        If oPn IsNot Nothing Then
            result.Operador = oPn
            Comunes(result, oPn.ObjectSpaceDeformer, oPn.BoneFromSkinMeshTransforms,
                    oPn.TransformSubset, oPn.OutputBufferIndex, oPn.TransformSetIndex, oPn.Name, 2)
            If oPn.LocalPNs IsNot Nothing Then
                For Each b In oPn.LocalPNs
                    If b Is Nothing Then Continue For
                    result.CanalesPorBloque.Add(New IReadOnlyList(Of Integer)() {b.LocalPosition, b.LocalNormal})
                Next
            End If
            Return Cerrar(result)
        End If

        Dim oP = Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPOperator.Leer(graph, source)
        If oP IsNot Nothing Then
            Comunes(result, oP.ObjectSpaceDeformer, oP.BoneFromSkinMeshTransforms,
                    oP.TransformSubset, oP.OutputBufferIndex, oP.TransformSetIndex, oP.Name, 1)
            If oP.LocalPs IsNot Nothing Then
                For Each b In oP.LocalPs
                    If b Is Nothing Then Continue For
                    result.CanalesPorBloque.Add(New IReadOnlyList(Of Integer)() {b.LocalPosition})
                Next
            End If
            Return Cerrar(result)
        End If

        Dim oPnt = Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNTOperator.Leer(graph, source)
        If oPnt IsNot Nothing Then
            Comunes(result, oPnt.ObjectSpaceDeformer, oPnt.BoneFromSkinMeshTransforms,
                    oPnt.TransformSubset, oPnt.OutputBufferIndex, oPnt.TransformSetIndex, oPnt.Name, 3)
            If oPnt.LocalPNTs IsNot Nothing Then
                For Each b In oPnt.LocalPNTs
                    If b Is Nothing Then Continue For
                    result.CanalesPorBloque.Add(New IReadOnlyList(Of Integer)() {
                        b.LocalPosition, b.LocalNormal, b.LocalTangent})
                Next
            End If
            Return Cerrar(result)
        End If

        Dim oPntb = Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNTBOperator.Leer(graph, source)
        If oPntb IsNot Nothing Then
            Comunes(result, oPntb.ObjectSpaceDeformer, oPntb.BoneFromSkinMeshTransforms,
                    oPntb.TransformSubset, oPntb.OutputBufferIndex, oPntb.TransformSetIndex, oPntb.Name, 4)
            If oPntb.LocalPNTBs IsNot Nothing Then
                For Each b In oPntb.LocalPNTBs
                    If b Is Nothing Then Continue For
                    result.CanalesPorBloque.Add(New IReadOnlyList(Of Integer)() {
                        b.LocalPosition, b.LocalNormal, b.LocalTangent, b.LocalBiTangent})
                Next
            End If
            Return Cerrar(result)
        End If

        Return Nothing
    End Function

    ''' <summary>Los cinco campos que las cuatro variantes declaran igual.</summary>
    Private Shared Sub Comunes(r As HclObjectSpaceSkinPNOperatorGraph_Class,
                               deformador As Havok.Canon.Objects.HkObj_HclObjectSpaceDeformer,
                               huesos As List(Of Single()), subset As List(Of Integer),
                               bufferDeSalida As UInteger, transformSet As UInteger,
                               nombre As String, canales As Integer)
        r.Deformador = deformador
        r.HuesosDesdeMalla = huesos
        r.Subconjunto = subset
        r.BufferDeSalida = CInt(bufferDeSalida)
        r.IndiceDelTransformSet = CInt(transformSet)
        r.Nombre = nombre
        r.Canales = canales
    End Sub

    ''' <summary>Las cuatro familias de influencias y los vértices, que son iguales en las cuatro
    ''' variantes: lo único que cambió arriba es de dónde salen las listas de canal.</summary>
    Private Shared Function Cerrar(r As HclObjectSpaceSkinPNOperatorGraph_Class) As HclObjectSpaceSkinPNOperatorGraph_Class
        Dim d = r.Deformador
        If d Is Nothing Then Return r

        ' ⛔ LAS CUATRO FAMILIAS. `hclObjectSpaceDeformer` declara CUATRO arrays de entradas
        ' (four/three/two/oneBlendEntries) y acá se leían TRES. Los vértices con UNA sola influencia
        ' quedaban SIN skinnear: no entraban al diccionario, así que la partícula que los usaba caía
        ' al DefaultClothPose, que está en otro espacio.
        Dim porFamilia = New List(Of List(Of HclSkinVertice_Class))() From {
            SubconjuntosDe(d.FourBlendEntries.Select(Function(b) New BloquePesado(b.VertexIndices, b.BoneIndices, b.BoneWeights)).ToList(), 4),
            SubconjuntosDe(d.ThreeBlendEntries.Select(Function(b) New BloquePesado(b.VertexIndices, b.BoneIndices, b.BoneWeights)).ToList(), 3),
            SubconjuntosDe(d.TwoBlendEntries.Select(Function(b) New BloquePesado(b.VertexIndices, b.BoneIndices, b.BoneWeights)).ToList(), 2),
            SubconjuntosDeUnaInfluencia(d.OneBlendEntries)
        }
        r.Vertices.AddRange(VerticesDe(r, porFamilia))
        Return r
    End Function

    ''' <summary>La variante `PN`, para los consumidores que necesitan el operador TIPADO.
    ''' <para>Devuelve `Nothing` si el objeto no es `PN` — que es lo que esos consumidores esperan;
    ''' el motor usa <see cref="ParseObjectSpaceSkinOperator"/>, que acepta las cuatro.</para></summary>
    Friend Shared Function ParseObjectSpaceSkinPNOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclObjectSpaceSkinPNOperatorGraph_Class
        Dim r = ParseObjectSpaceSkinOperator(graph, source)
        Return If(r Is Nothing OrElse r.Operador Is Nothing, Nothing, r)
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
        Dim control = If(source.Deformador?.ControlBytes, New List(Of Integer)())
        ' ⛔ LAS LISTAS DE CANAL, no el bloque tipado: son 1 en `P`, 2 en `PN`, 3 en `PNT` y 4 en
        ' `PNTB`, y atarlo al tipo `LocalBlockPN` es lo que dejaba a tres variantes sin parsear.
        Dim bloques = source.CanalesPorBloque
        Dim nBloques = Math.Max(control.Count, bloques.Count)

        If Logger.Enabled Then
            ' Histograma de los tags `w` del vector de POSICION. La regla actual mira UN bit y
            ' ofrece dos escalas; si aca aparecen mas de dos valores distintos, la regla esta
            ' incompleta por construccion y no hace falta discutirlo.
            Dim ws As New List(Of String)
            For Each b In bloques
                If b Is Nothing OrElse b.Length = 0 Then Continue For
                For carril = 0 To 7
                    ws.Add("0x" & (HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(b(0), carril, 0)(3) And &HFFFF).ToString("X4"))
                    ws.Add("0x" & (HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(b(0), carril, 1)(3) And &HFFFF).ToString("X4"))
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

            Dim canales As IReadOnlyList(Of Integer)() = Nothing
            If iBloque < bloques.Count Then canales = bloques(iBloque)
            If canales Is Nothing OrElse canales.Length = 0 Then Continue For

            For slot = 0 To 15
                Dim v = lista(desde + slot)
                If slot > 0 AndAlso v.VertexIndex = lista(desde + slot - 1).VertexIndex Then Exit For
                v.BlockIndex = iBloque
                Dim carril = slot \ 2, par = slot Mod 2
                Dim pos = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(canales(0), carril, par)
                ' ⛔ SIN ESCALA NO HAY VERTICE. Antes se caia al 256 "historico" y el vertice salia
                ' decodificado con la escala equivocada; ahora se saltea y se cuenta.
                Dim escP = PositionScaleFromW(pos)
                If Not escP.HasValue Then
                    sinEscala += 1
                    Continue For
                End If
                v.Position = DecodeQuantizedVector3(pos, escP.Value, carril, par)

                ' ⛔⛔ CADA CANAL CON SU TAG, y ninguno decide por otro. El tag de la NORMAL no puede
                ' descartar la POSICION: estuvieron acopladas un rato y eso borraba el vertice ENTERO
                ' por un `w` de normal degenerado — el vertice desaparecia del mapa de skin, la
                ' particula que lo usa se quedaba sin puente, y `[CLOTH-ANCLASINMAPA]` acusaba a las
                ' anclas de un defecto de decodificacion.
                ' ⛔ Y LA NORMAL SE DECODIFICA COMO LA POSICION, no con un 32767 inventado: en el
                ' deformer que consume estos bloques (`0x14193C5E0`) las UNICAS dos constantes que
                ' multiplican son `65536.0` (`0x14262BA50`) y `1/255` (`0x142492850`) — el `<< 16` del
                ' reinterpretado y la normalizacion de los PESOS. No hay ningun 32767 en el camino.
                ' Lo mismo vale para la tangente y la bitangente: es el mismo layout de 64 `int16`.
                If canales.Length > 1 Then
                    Dim nor = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(canales(1), carril, par)
                    Dim escN = PositionScaleFromW(nor)
                    If escN.HasValue Then
                        v.Normal = DecodeQuantizedVector3(nor, escN.Value, carril + 8, par)
                    Else
                        sinEscalaNormal += 1
                    End If
                End If
                If canales.Length > 2 Then
                    Dim tan = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(canales(2), carril, par)
                    Dim escT = PositionScaleFromW(tan)
                    If escT.HasValue Then v.Tangent = DecodeQuantizedVector3(tan, escT.Value, carril + 16, par)
                End If
                If canales.Length > 3 Then
                    Dim bit = HclObjectSpaceSkinPNOperatorGraph_Class.VectorDeSlotEnLista(canales(3), carril, par)
                    Dim escB = PositionScaleFromW(bit)
                    If escB.HasValue Then v.BiTangent = DecodeQuantizedVector3(bit, escB.Value, carril + 24, par)
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
            Dim sv = source.Deformador.StartVertexIndex, ev = source.Deformador.EndVertexIndex
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
    ''' <summary>El operador tipado — SOLO en la variante `PN`. En las otras tres queda en
    ''' `Nothing` y lo que hay que mirar son los cinco campos de abajo.</summary>
    Public Property Operador As Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNOperator

    ''' <summary>
    ''' ⛔ LOS CAMPOS COMUNES A LAS CUATRO VARIANTES, copiados al parsear.
    ''' <para>Las cuatro son HERMANAS bajo `hclObjectSpaceSkinOperator` (reflexión:
    ''' `padre=hclObjectSpaceSkinOperator` en las cuatro), así que no hay un tipo común que las
    ''' cubra y `Operador` sólo puede sostener a `PN`. Atar el grafo a ese tipo era lo que dejaba
    ''' a las otras tres SIN PARSEAR — y con una línea de log diciendo que se soportaban.</para>
    ''' </summary>
    Public Property Deformador As Havok.Canon.Objects.HkObj_HclObjectSpaceDeformer
    Public Property HuesosDesdeMalla As List(Of Single())
    Public Property Subconjunto As List(Of Integer)
    Public Property BufferDeSalida As Integer
    Public Property IndiceDelTransformSet As Integer
    Public Property Nombre As String

    ''' <summary>Por bloque, las listas de `int16` de cada canal: 1 en `P`, 2 en `PN`, 3 en `PNT`
    ''' y 4 en `PNTB`. El layout de cada una es el mismo — 8 carriles de 8 enteros, dos vértices
    ''' por carril — y lo lee <see cref="VectorDeSlotEnLista"/>, que ya estaba escrita general.</summary>
    Public ReadOnly Property CanalesPorBloque As New List(Of IReadOnlyList(Of Integer)())

    ''' <summary>Los vertices del skin, cada uno con su posicion, su normal y sus influencias.
    ''' Es lo que caminan TODOS los consumidores.</summary>
    Public ReadOnly Property Vertices As New List(Of HclSkinVertice_Class)
    ''' <summary>Cuantos vertices del skin quedaron cubiertos por el operador. Analisis del package
    ''' parser: no sale del archivo. (El `&lt;summary&gt;` de "los carriles crudos" que estaba aca colgaba de
    ''' `...LocalBlockLaneGraph_Class`, que se borro; la ley vive en <see cref="CarrilDe"/>.)</summary>
    Public Property CoveredVertexCount As Integer

    ''' <summary>Cuantos canales trae cada bloque local: 1 = P, 2 = PN, 3 = PNT, 4 = PNTB. Es lo
    ''' unico que separa a las cuatro variantes del operador (tipos 22 a 25).</summary>
    Public Property Canales As Integer = 2

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
        Return CarrilDeLista(fuente, carril)
    End Function

    ''' <summary>
    ''' Un carril de UNA lista de 64 `int16` — la forma general, que sirve para los cuatro canales
    ''' de las cuatro variantes (`LocalBlockP`, `PN`, `PNT`, `PNTB`).
    ''' <para>El layout es siempre el mismo: 8 carriles de 8 enteros, dos vertices por carril. Lo
    ''' unico que cambia entre variantes es CUANTAS listas hay, no como se lee cada una.</para>
    ''' </summary>
    Public Shared Function CarrilDeLista(fuente As IReadOnlyList(Of Integer), carril As Integer) As Short()
        If fuente Is Nothing OrElse fuente.Count < 64 OrElse carril < 0 Then Return Array.Empty(Of Short)()
        Dim base_ = (carril Mod 8) * 8
        Dim r(7) As Short
        For i = 0 To 7
            r(i) = CShort(fuente(base_ + i))
        Next
        Return r
    End Function

    ''' <summary>Los cuatro `int16` de un slot dentro de una lista de canal.</summary>
    Public Shared Function VectorDeSlotEnLista(fuente As IReadOnlyList(Of Integer), carril As Integer,
                                               par As Integer) As Short()
        Dim c = CarrilDeLista(fuente, carril)
        If c.Length < 8 Then Return Array.Empty(Of Short)()
        Dim b = par * 4
        Return New Short() {c(b), c(b + 1), c(b + 2), c(b + 3)}
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
    ''' <summary>La tangente y la bitangente locales, en las variantes que las traen (`PNT` y
    ''' `PNTB`). `Nothing` en `P` y `PN`, que no las declaran.</summary>
    Public Property Tangent As HclObjectSpaceSkinQuantizedVectorGraph_Class
    Public Property BiTangent As HclObjectSpaceSkinQuantizedVectorGraph_Class
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








