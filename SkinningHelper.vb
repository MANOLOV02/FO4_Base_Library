' Version Uploaded of Fo4Library 3.2.0
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Imports FO4_Base_Library.RecalcTBN
Imports SysNumerics = System.Numerics
Imports System.Collections.Concurrent
Imports System.Threading.Tasks

' --- STRUCTURE PARA ALMACENAR GEOMETRÍA SKINEADA ---
'
' Fully polymorphic — no BSTriShape-specific fields.  The packed BSVertexData/BSVertexDataSSE
' buffers that used to be cached here were an optimization for BSTriShape in-place struct copy
' during RemoveZaps; that path was eliminated in favor of adapter.ResizeVertices + individual
' field setters + adapter.SetSkinning, which works uniformly across BSTriShape and NiTriShape
' families.
Public Structure SkinnedGeometry
    Public Vertices() As Vector3d
    Public BaseVertices() As Vector3d
    Public NifLocalVertices() As Vector3d      ' pre-skinning NIF local space — base for morph application
    ' EN SINGLE, NO EN DOUBLE. Es 128 bytes por vertice en Matrix4d y se recorre ENTERA una vez por
    ' vertice por frame durante una animacion: con 130.500 vertices son 16,7 MB de lectura por pasada, y el
    ' destino de todo eso es un VBO de floats. La precision extra no se usaba en ningun lado.
    ' Ver NormalMatrixOrIdentity, que se paso a Single por el mismo motivo y en el mismo movimiento.
    ''' <summary>Mtot per-vertice (GlobalTransform * skin), en SoA. Ver <see cref="SkinMatricesSoA"/>:
    ''' se indexa igual que el <c>Matrix4()</c> que reemplaza —<c>mats(i)</c>, <c>mats.Length</c>— pero por
    ''' dentro son 12 arrays planos, que es lo que el kernel necesita para vectorizar sin copiar.</summary>
    Public PerVertexSkinMatrix As SkinMatricesSoA
    ''' <summary>Vertices cuya MASCARA (zap/oclusion) cambio desde la ultima subida. Mismo tipo y por
    ''' el mismo motivo que <see cref="dirtyVertexIndices"/>: ver <see cref="ConjuntoDeSucios"/>.</summary>
    Public dirtyMaskIndices As ConjuntoDeSucios
    ' Set by MorphEngine.ApplyMorphPlan whenever it (re)computes the zap mask; consumed + cleared by
    ' Render.EnsureZapIndexBuffer to rebuild the filtered element buffer only when the zap set changed.
    ' Initialized True in ExtractSkinnedGeometry so the first draw filters (a Structure can't have an
    ' instance field initializer in VB — BC31049 — so the default is set at construction instead).
    ' (See render-zap-clean-cpu-index-filter.)
    Public ZapTopologyDirty As Boolean
    ''' <summary>Vertices cuya posicion/normal cambio desde la ultima subida a la GPU. Ver
    ''' <see cref="ConjuntoDeSucios"/>: se usa igual que el <c>HashSet(Of Integer)</c> que reemplaza
    ''' —<c>.Count</c>, <c>.Add</c>, <c>.Clear</c>, <c>For Each</c>— pero marcar "todos" es O(1) en vez
    ''' de hashear un entero por vertice en cada frame de animacion.</summary>
    Public dirtyVertexIndices As ConjuntoDeSucios
    Public dirtyMaskFlags() As Boolean
    Public dirtyVertexFlags() As Boolean
    ''' <summary>
    ''' N/T/B se GUARDAN en Single (12 B por vertice cada uno en vez de 24). El dato ya nace y
    ''' muere en float: entra por <c>IShapeGeometry.GetNormals/GetTangents/GetBitangents</c>, que
    ''' devuelven <c>System.Numerics.Vector3</c>, y sale por <c>InjectNormalsToTrishape</c>, que hace
    ''' <c>CSng</c>. En <c>BSTriShape</c> —los dos juegos— la Normal y la Tangent del NIF son
    ''' <c>ByteVector3</c> (sbyte, paso 1/127) y solo <c>BitangentX</c> es float/half: 8 de las 9
    ''' componentes estan cuantizadas a byte, asi que guardar el intermedio en Double no aportaba
    ''' informacion recuperable.
    '''
    ''' LA MATEMATICA SIGUE EN DOUBLE. El acumulador y el Gram-Schmidt de <c>RecalcTBN</c> trabajan
    ''' en <c>Vector3d</c> y solo redondean AL ESCRIBIR. No confundir este cambio con igualar la
    ''' precision del acumulador a la del canonico: eso es otra cosa, se midio, y ADEMAS degradaba
    ''' `BaseArmor` (ver el comentario de <c>ASingle</c>).
    '''
    ''' Los que RELEEN estos arrays y siguen calculando en Double ven el valor ya redondeado:
    ''' <c>KeepExistingNormals</c> (camino solo-UV), el miembro soldado, y el world-cache. El pase de
    ''' costura NO: se lleva la normal en Double por <c>accCrudo</c> justamente para no depender de la
    ''' relectura.
    ''' </summary>
    Public Normals() As Vector3
    Public Tangents() As Vector3
    Public Bitangents() As Vector3
    Public Uvs_Weight() As Vector3

    ' UVs TAL COMO SALIERON DEL NIF, antes de cualquier slider uv. Las hace falta por lo mismo que
    ' NifLocalVertices: ApplyUVDiff ACUMULA (`uvs[i].u += ...`), asi que sin una base a la que volver
    ' cada re-aplicacion sumaria encima de la anterior y las UVs derivarian a cada movimiento del
    ' slider. La escribe ExtractSkinnedGeometry; MorphEngine/MorphingHelper restauran desde aca.
    Public BaseUvs_Weight() As Vector3

    ' True cuando un canal uv reescribio Uvs_Weight y el VBO de UV (StaticDraw) quedo viejo.
    ' Lo levanta MorphEngine.ApplyMorphPlan y lo consume/limpia Render.UpdateUvBuffer_GL.
    Public UvsDirty As Boolean

    ' True si la ULTIMA aplicacion de morphs escribio deltas de uv. Es lo que permite saltear el
    ' reset (un Array.Copy del array entero) en el caso normal — ningun slider uv — sin perder el
    ' momento en que hay que volver a la base: el update en el que el ultimo canal uv se apaga.
    Public UvsMorphed As Boolean
    Public Eyedata() As Single
    Public ParentGlobalTransform As Matrix4d
    Public BoneMatsBind() As Matrix4d   ' bind-pose matrices
    Public BoneMatsPose() As Matrix4d  ' pose matrices
    Public VertexColors() As Vector4
    Public VertexMask() As Single
    Public Indices() As UInteger
    Public Geometry As IShapeGeometry                 ' polymorphic adapter for the underlying shape (BSTriShape / NiTriShape / BSLODTriShape / BSSegmented / ...)
    Public Skinning As ShapeSkinningData              ' polymorphic per-vertex bone idx[4]+weight[4]; sourced from BSVertexData inline for BSTriShape or NiSkinPartition/NiSkinData for NiTriShape family
    Public TriangleProvenance As TriangleRemap        ' optional per-new-triangle source map; populated by zap/split/merge so InjectToTrishape can redistribute Segments/LOD sizes
    Public Boundingcenter As Vector3d
    Public Minv As Vector3d
    Public Maxv As Vector3d
    Public CachedTBN As TBNCache
    Public Version As NiVersion
    ' GPU Skinning: flat arrays for VBO upload
    Public GPUBoneIndices() As Byte        ' 4 bytes per vertex, flattened: [v0b0,v0b1,v0b2,v0b3, v1b0,...]
    Public GPUBoneWeights() As Single      ' 4 floats per vertex, flattened: [v0w0,v0w1,v0w2,v0w3, v1w0,...]
    Public GPUBoneMatrices() As Matrix4    ' one Matrix4 per bone in the palette for SSBO
    ' Lazy world-space cache (computed on demand, invalidated by pose/morph changes)
    Public CachedWorldVertices() As Vector3d
    Public CachedWorldNormals() As Vector3d
    Public WorldCacheValid As Boolean
    ' Option B: en GPU-mode durante animación, RecomputeGPUBoneMatrices saltea el blend per-vértice
    ' (PerVertexSkinMatrix) porque el shader skinnea del SSBO y nadie muestra el world-cache. Este flag
    ' marca si PerVertexSkinMatrix está al día; si no, EnsurePerVertexSkinMatrix lo recompone lazy desde
    ' BoneMatsPose cuando un lector (bounds/picking/export/occlusion) realmente lo pide.
    Public PerVertexMatrixValid As Boolean
End Structure
Public Structure MorphData
    Public index As UInteger
    Public PosDiff As Vector3
End Structure


Public Class SkinningHelper

    ''' <summary>
    ''' Rangos para un <c>Parallel.ForEach</c> sobre <paramref name="n"/> elementos, con
    ''' <b><c>n = 0</c> permitido</b>.
    '''
    ''' <c>Partitioner.Create(0, 0)</c> <b>LANZA</b>
    ''' (<c>toExclusive ('0') must be greater than '0'</c>), mientras que el <c>Parallel.For(0, 0, …)</c>
    ''' que había antes en estos mismos loops era un <b>no-op silencioso</b>. Esa asimetría es
    ''' traicionera: al pasar los loops a Partitioner por performance, toda shape que llegara con CERO
    ''' vértices dejó de no hacer nada y pasó a tirar el build ENTERO, envuelta en una
    ''' <c>AggregateException</c> que no dice qué shape fue.
    '''
    ''' MEDIDO sobre el corpus de FO4: <b>5 de 95 .osp</b> con exit 5 y <b>14 .nif perdidos</b>
    ''' (Pampas BodySuit 1..10, Fortaleza Belt-1/-2, COCO KDAKaisa, Vtaw 5 Hotpants) — todos outfits
    ''' con zaps, o sea shapes que quedan compactadas a 0 vértices. En 1.4.1 construían bien.
    ''' Las shapes de 0 vértices NO son nuevas; lo nuevo era que explotaran.
    '''
    ''' Esto es para loops de GEOMETRÍA, donde una shape vacía es un dato legítimo. Los loops por
    ''' píxel (compositores, bakers) NO usan esto a propósito: un bitmap de 0 píxeles sería otro bug y
    ''' taparlo lo escondería.
    ''' </summary>
    Public Shared Function RangosDe(n As Integer) As Partitioner(Of Tuple(Of Integer, Integer))
        If n <= 0 Then Return Partitioner.Create(Array.Empty(Of Tuple(Of Integer, Integer))())
        Return Partitioner.Create(0, n)
    End Function

    ' SYNC: CPU/GPU skinning — blend de bone matrices del lado CPU (double).
    '   Fórmula: skinMatrix = Σ(bones[idx[j]] · weight[j]) / sumW.  Fallback (sumW=0): bones[idx[0]].
    '   Sitios gemelos que hay que mover JUNTOS (si divergen, el bug es silencioso: compila, no tira, y
    '   sólo se ve mal el OTRO camino, que el usuario alterna con el toggle):
    '     1. Shader_Class.vb — bloque de skinning del vertex shader (DUPLICADO en FO4 y en SSE)
    '     2. esta función
    '     3. RecomputeGPUBoneMatrices  (composición de matrices → SSBO)
    '     4. ExtractSkinnedGeometry    (arrays de GPU: índices/pesos, normalizados a sum=1)
    '     5. Render.UpdateSkinBuffers_GL (pre-skin del camino CPU)
    '     + SkinBakeMath / FaceGenBuildPipeline (el bake usa la misma fórmula)
    '   Diferencias POR DISEÑO, no drift: la GPU va en float con pesos ya normalizados; la CPU en double y
    '   normaliza en runtime; la GPU aplica transpose(inverse(mat3)) a N/T/B y la CPU los deja en local.
    '   Test de paridad: alternar Setting_GPUSkinning sobre un shape posado — debe verse idéntico.
    '   Ver 00-reglas-ui-y-vb.md (§10) y 00-reglas-comentarios.md.
    ''' <summary>
    ''' Paleta que consume el blend por vértice: <c>globalTransform × matsPose(k)</c> para cada hueso.
    '''
    ''' <para>ESTE ES EL ÚNICO LUGAR DONDE SE ESCRIBE ESA FÓRMULA. Los tres sitios que llenan
    ''' <c>PerVertexSkinMatrix</c> —<see cref="ExtractSkinnedGeometry"/>,
    ''' <see cref="RecomputeGPUBoneMatrices"/> y <see cref="EnsurePerVertexSkinMatrix"/>— pasan por
    ''' acá. Antes cada uno la escribía por su cuenta y <b>ya habían divergido</b>: el perezoso
    ''' blendeaba <c>BoneMatsPose</c> CRUDO mientras los otros dos multiplicaban por
    ''' <c>globalTransform</c>. El comentario que había decía que daba igual "porque GlobalTransform es
    ''' Identity", y es FALSO: ese producto suma ceros, o sea que el elemento (1,j) sale de
    ''' <c>1·M1j + 0·M2j + 0·M3j + 0·M4j</c> y convierte <c>-0.0</c> en <c>+0.0</c> (y daría NaN si
    ''' hubiera un ±Inf en la columna). El blend normal lo lava porque parte de <c>Matrix4d.Zero</c> y
    ''' suma, <b>pero la rama <c>sumW = 0</c> devuelve la matriz de la paleta TAL CUAL</b> — ahí el
    ''' signo sobrevive y el mismo vértice salía con distinto bit según si la pasada 2 se había
    ''' salteado. Verificado con test en negativo: revertir esto hace fallar S3 y S4 de la suite.</para>
    '''
    ''' <para>El arreglo NO es "copiar la fórmula en el tercer sitio" —eso fue mi primer intento y
    ''' dejaba viva la posibilidad de volver a divergir, además de acoplar el perezoso a un campo que
    ''' un tercer camino no actualizaba. El arreglo es que <b>haya una sola fórmula</b>: acá.</para>
    ''' </summary>
    Private Shared Function BuildPosePalette(matsPose() As Matrix4d, globalTransform As Matrix4d) As Matrix4d()
        If matsPose Is Nothing Then Return Array.Empty(Of Matrix4d)()
        Dim pal(matsPose.Length - 1) As Matrix4d
        For k = 0 To matsPose.Length - 1
            pal(k) = globalTransform * matsPose(k)
        Next
        Return pal
    End Function

    ''' <summary>El blend dejando el resultado EN EL SCRATCH (16 Single), sin construir una
    ''' <c>Matrix4d</c>. Devuelve el scratch, o <c>Nothing</c> si el caso cae en una rama de
    ''' excepcion que devuelve una matriz de la paleta tal cual (sin peso, pesos nulos, wpv 0).
    ''' <para>NO DUPLICA LA LEY: las guardas y la normalizacion son LAS MISMAS lineas que
    ''' <see cref="BlendBoneMatrices"/> — de hecho esa funcion ahora llama a esta y solo agrega el
    ''' armado de la Matrix4d para sus consumidores (bake, exportador, gate). Si hubiera dos copias de
    ''' las guardas, el render y el bake podrian divergir en QUE peso se descarta, que es peor que una
    ''' diferencia de redondeo.</para>
    ''' <para>Cuando devuelve el scratch, <c>sc.Acc</c> tiene los 16 elementos ya escalados.</para>
    ''' </summary>
    Private Shared Function BlendEnScratch(boneWeights As System.Half(), boneIndices As Byte(), baseIdx As Integer,
                                           wpv As Integer, precomputed() As Matrix4d, flatPal As Single(),
                                           sc As BlendScratch) As BlendScratch
        If flatPal Is Nothing Then Return Nothing
        If boneWeights Is Nothing OrElse boneIndices Is Nothing OrElse precomputed.Length = 0 OrElse wpv <= 0 Then Return Nothing
        Dim available As Integer = Math.Min(wpv, Math.Min(boneWeights.Length - baseIdx, boneIndices.Length - baseIdx))
        Dim nUsed As Integer = 0

        ' SIN Array.Clear. `TryComputeWeights` llena `ckW` cuando devuelve True, y cuando devuelve False
        ' el contenido no se lee: limpiarlo por vertice eran 130.500 clears por frame de un buffer que o se
        ' sobrescribe entero o se ignora.
        ' EL GATE DEL CK SE PREGUNTA ANTES DE LLAMAR. `TryComputeWeights` arranca con
        ' `If Not Enabled Then Return False`, y `Enabled` es False por default: sin este corto-circuito se
        ' pagaba UNA LLAMADA NO INLINEADA POR VERTICE —130.500 por frame— para que devolviera False en la
        ' primera linea. El `AndAlso` garantiza que ni se evalue.
        Dim ckW = sc.CkW
        If EngineSkinWeightNormalization.Enabled AndAlso available >= EngineSkinWeightNormalization.Slots AndAlso
           EngineSkinWeightNormalization.TryComputeWeights(boneWeights, baseIdx, wpv, ckW) Then
            For j = 0 To EngineSkinWeightNormalization.Slots - 1
                If ckW(j) > 0.0F Then
                    Dim idxc = boneIndices(baseIdx + j)
                    If idxc >= 0 AndAlso idxc < precomputed.Length Then
                        sc.Idx(nUsed) = CInt(idxc) : sc.W(nUsed) = ckW(j) : nUsed += 1
                    End If
                End If
            Next
            FastGeom.BlendIntoS(flatPal, sc.Idx, sc.W, nUsed, sc.Acc)
            Return sc
        End If

        Dim sumW As Double = 0
        For j = 0 To available - 1
            ' UNA sola conversion. Estaba Half → Double → Single: `CType(h, Double)` y despues `CSng(w)`.
            ' Half tiene 11 bits de mantisa y Single 24, asi que ir por Double es EXACTO y volver tambien —
            ' `CSng(CDbl(h))` y `CSng(h)` dan el mismo bit. Lo que cambia es que ahora hay una conversion
            ' por slot en vez de dos. `sumW` sigue acumulando en Double, que es la ley del divisor.
            Dim wS = CSng(boneWeights(baseIdx + j))
            sumW += CDbl(wS)
            Dim idx = boneIndices(baseIdx + j)
            If idx >= 0 AndAlso idx < precomputed.Length Then
                sc.Idx(nUsed) = CInt(idx) : sc.W(nUsed) = wS : nUsed += 1
            End If
        Next
        ' sumW = 0 devuelve una matriz de la paleta TAL CUAL, sin sumar: esa rama no pasa por el
        ' acumulador y la resuelve el camino largo.
        If sumW = 0 Then Return Nothing
        ' El escalado va FUSIONADO en el kernel: ver el parametro `escala` de BlendIntoS. Como paso aparte
        ' costaba 0,68 ms de un blend de 5,2 por recorrer el acumulador una segunda vez.
        FastGeom.BlendIntoS(flatPal, sc.Idx, sc.W, nUsed, sc.Acc, CSng(1.0 / sumW))
        Return sc
    End Function

    ''' <summary>El blend de un vertice, devolviendo la matriz. Mezcla las matrices de los huesos que influyen
    ''' al vertice que arranca en <paramref name="baseIdx"/>, leyendo <paramref name="wpv"/> slots de los
    ''' arrays PLANOS de indices y pesos (sin slice por vertice). La usan el BAKE, el exportador y el gate; el
    ''' render va por <see cref="BlendEnScratch"/>, que evita construir la Matrix4d.
    ''' <para><b>Public a proposito: es lo que hace CIERTO el contrato SYNC de arriba.</b>
    ''' <c>SkinBakeMath</c> y <c>FaceGenBuildPipeline.BlendMtot</c> llaman ACA en vez de transcribir la
    ''' formula, asi que el gate <c>skin-blend</c> cubre la ley que el bake realmente corre. Una copia a mano
    ''' de esta ley deja al gate probando una funcion que el bake no llama.</para>
    ''' <para>LAS GUARDAS ESTAN UNA SOLA VEZ. Este metodo delega en <c>BlendEnScratch</c> y solo
    ''' agrega el armado de la <c>Matrix4d</c> y las ramas de excepcion. Tenerlas duplicadas seria
    ''' peor que una diferencia de redondeo: render y bake podrian discrepar en QUE peso se descarta
    ''' o en cual es el divisor, que es una diferencia de FORMA. Ver la regla RENDER==BAKE.</para>
    ''' <para>Las guardas quedan ESCALARES a proposito: no hay una sola mascara por lane en el
    ''' camino vectorial, y por eso las trampas #4 (orden de los selects) y #5 (NaN) no aplican.</para>
    ''' </summary>
    Public Shared Function BlendBoneMatrices(boneWeights As System.Half(), boneIndices As Byte(), baseIdx As Integer, wpv As Integer, precomputed() As Matrix4d,
                                              Optional flatPal As Single() = Nothing) As Matrix4d
        If boneWeights Is Nothing OrElse boneIndices Is Nothing OrElse precomputed.Length = 0 OrElse wpv <= 0 Then
            Return If(precomputed.Length > 0, precomputed(0), Matrix4d.Identity)
        End If
        Dim available As Integer = Math.Min(wpv, Math.Min(boneWeights.Length - baseIdx, boneIndices.Length - baseIdx))
        Dim sc = GetBlendScratch(Math.Max(available, EngineSkinWeightNormalization.Slots))

        If flatPal IsNot Nothing Then
            Dim listo = BlendEnScratch(boneWeights, boneIndices, baseIdx, wpv, precomputed, flatPal, sc)
            If listo IsNot Nothing Then Return FastGeom.LoadMatrixS(listo.Acc, 0)
            ' `Nothing` = una de las ramas de excepcion: se resuelven abajo, con la MISMA ley.
        End If

        ' Camino sin paleta plana (y las ramas de excepcion). Acumula en Matrix4d desde `precomputed`.
        ' NO es bit-comparable con el camino de arriba: son precisiones distintas. El gate compara
        ' vectorial contra escalar DENTRO del camino de la paleta, con FastGeom.ForzarEscalarS.
        Dim nUsed As Integer = 0
        Dim ckW = sc.CkW
        If EngineSkinWeightNormalization.Enabled AndAlso available >= EngineSkinWeightNormalization.Slots AndAlso
           EngineSkinWeightNormalization.TryComputeWeights(boneWeights, baseIdx, wpv, ckW) Then
            For j = 0 To EngineSkinWeightNormalization.Slots - 1
                If ckW(j) > 0.0F Then
                    Dim idxc = boneIndices(baseIdx + j)
                    If idxc >= 0 AndAlso idxc < precomputed.Length Then
                        sc.Idx(nUsed) = CInt(idxc) : sc.W(nUsed) = ckW(j) : nUsed += 1
                    End If
                End If
            Next
            Return AccumulateBlend(precomputed, flatPal, sc, nUsed)
        End If

        Dim sumW As Double = 0
        For j = 0 To available - 1
            Dim wS = CSng(boneWeights(baseIdx + j))
            ' sumW acumula TODOS los pesos, tambien los de indice fuera de rango: sacar los descartados
            ' cambia el DIVISOR, y con el la salida del bake.
            sumW += CDbl(wS)
            Dim idx = boneIndices(baseIdx + j)
            If idx >= 0 AndAlso idx < precomputed.Length Then
                sc.Idx(nUsed) = CInt(idx) : sc.W(nUsed) = wS : nUsed += 1
            End If
        Next
        If sumW = 0 Then
            Dim idx0 As Byte = If(available > 0, boneIndices(baseIdx), CByte(0))
            Return precomputed(Math.Max(0, Math.Min(CInt(idx0), precomputed.Length - 1)))
        End If
        Return AccumulateBlend(precomputed, flatPal, sc, nUsed, CSng(1.0 / sumW))
    End Function
    Private NotInheritable Class BlendScratch
        Public Idx() As Integer
        ''' <summary>Pesos, en Single: la paleta plana tambien lo es. Ver FastGeom.BuildFlatPaletteS.</summary>
        Public W() As Single
        ''' <summary>Pesos normalizados del CK, reusados entre vertices. Ver el call site en
        ''' <see cref="BlendBoneMatrices"/>: ahi se alocaba uno nuevo por vertice.</summary>
        Public CkW() As Single
        Public ReadOnly Acc(FastGeom.MatSingles - 1) As Single
        Public Sub New(slots As Integer)
            Grow(slots)
        End Sub
        Public Sub Grow(slots As Integer)
            Dim n = Math.Max(1, slots)
            ' CkW siempre tiene al menos los slots del CK: el call site lo indexa por esos, no por `slots`.
            If CkW Is Nothing OrElse CkW.Length < EngineSkinWeightNormalization.Slots Then
                ReDim CkW(EngineSkinWeightNormalization.Slots - 1)
            End If
            If Idx Is Nothing OrElse Idx.Length < n Then
                ReDim Idx(n - 1)
                ReDim W(n - 1)
            End If
        End Sub
    End Class

    <ThreadStatic>
    Private Shared _blendScratch As BlendScratch

    Private Shared Function GetBlendScratch(slots As Integer) As BlendScratch
        Dim s = _blendScratch
        If s Is Nothing Then
            s = New BlendScratch(slots)
            _blendScratch = s
        Else
            s.Grow(slots)
        End If
        Return s
    End Function

    ''' <summary>
    ''' Suma <c>Σ pares(indice, peso)</c> sobre la paleta y devuelve la matriz, opcionalmente escalada
    ''' por <paramref name="postScale"/> (el <c>1/sumW</c> de la normalizacion).
    '''
    ''' <para>UN SOLO ORIGEN DE DATOS: la paleta PLANA en Single. Es el UNICO punto donde se elige camino, y
    ''' lo elige <c>BlendIntoS</c> adentro —vectorial o escalar, este ultimo forzable con
    ''' <c>FastGeom.ForzarEscalarS</c>—: los dos leen exactamente lo mismo y recorren los pares en el MISMO
    ''' orden ⇒ el resultado es bit-identico, y eso es lo que verifica <c>SkinningSimdSelfTest</c> sobre la
    ''' funcion real, no sobre una maqueta.</para>
    ''' <para>El fallback a <c>precomputed</c> —acumular <c>precomputed(idx) * w</c> en <c>Matrix4d</c>, o sea
    ''' en Double y desde OTRO array— queda SOLO para las llamadas que no construyen paleta plana, y es lo que
    ''' dice ser: un camino de compatibilidad que NO es bit-comparable con el principal. Usarlo como
    ''' "referencia escalar" compara dos LEYES, no dos implementaciones de una.</para>
    ''' </summary>
    Private Shared Function AccumulateBlend(precomputed() As Matrix4d, flatPal As Single(), sc As BlendScratch, nUsed As Integer,
                                            Optional postScale As Single = 1.0F) As Matrix4d
        If flatPal IsNot Nothing Then
            FastGeom.BlendIntoS(flatPal, sc.Idx, sc.W, nUsed, sc.Acc)
            If postScale <> 1.0F Then FastGeom.ScaleAccS(sc.Acc, postScale)
            Return FastGeom.LoadMatrixS(sc.Acc, 0)
        End If
        Dim result As Matrix4d = Matrix4d.Zero
        For j = 0 To nUsed - 1
            result += precomputed(sc.Idx(j)) * CDbl(sc.W(j))
        Next
        If postScale <> 1.0F Then result = result * CDbl(postScale)
        Return result
    End Function

    ''' <summary>
    ''' Gate del blend vectorial: corre <b>la funcion REAL</b> <c>BlendBoneMatrices</c> por los dos
    ''' caminos (con paleta plana ⇒ vectorial, sin paleta ⇒ escalar) y compara BIT A BIT.
    ''' Devuelve "" si pasa.
    '''
    ''' <para>Prueba la produccion, no una maqueta. Un test que reimplementa el kernel al lado
    ''' puede dar verde mientras la funcion real diverge — es la trampa #10 de
    ''' 61-perf-simd-trampas, y la forma de no pisarla es llamar al codigo que corre de verdad.</para>
    '''
    ''' <para>Da veredicto AL ANCHO DE ESTA MAQUINA. Hay que correrlo TAMBIEN con
    ''' <c>DOTNET_MaxVectorTBitWidth=128</c>: un test que solo corre al ancho nativo no prueba nada
    ''' del otro (trampa #3).</para>
    ''' </summary>
    Public Shared Function SkinningSimdSelfTest() As String
        ' Primero el kernel suelto; si eso ya falla, el resto solo agrega ruido.
        Dim baseTest = FastGeom.VectorParitySelfTest()
        If baseTest.Length > 0 Then Return baseTest

        Dim rng As ULong = &HD1B54A32D192ED03UL
        Dim NextB = Function() As ULong
                        rng = rng Xor (rng << 13)
                        rng = rng Xor (rng >> 7)
                        rng = rng Xor (rng << 17)
                        Return rng
                    End Function

        Const nBones As Integer = 6
        Dim precomputed(nBones - 1) As Matrix4d
        For k = 0 To nBones - 1
            Dim m As Matrix4d = Matrix4d.Zero
            Dim tmp(FastGeom.MatDoubles - 1) As Double
            For e = 0 To FastGeom.MatDoubles - 1
                Dim b = NextB()
                tmp(e) = (CDbl(b And &HFFFFFFUL) / 16777216.0 - 0.5) * Math.Pow(2.0, CInt(b >> 60) - 6)
            Next
            m = FastGeom.LoadMatrix(tmp, 0)
            precomputed(k) = m
        Next
        Dim flatPal = FastGeom.BuildFlatPaletteS(precomputed)

        ' Pesos "interesantes": el cero exacto y el negativo disparan ramas distintas del guard, y el
        ' NaN prueba que las dos ramas lo tratan igual (el guard es escalar y compartido, asi que
        ' debe serlo — este caso existe para que un futuro refactor a mascaras por lane lo rompa
        ' RUIDOSAMENTE en vez de en silencio).
        Dim pesos As Single() = {0.0F, 1.0F, 0.5F, 0.25F, -0.5F, 0.75F, Single.NaN, 0.125F}

        Const wpvMax As Integer = 4
        Dim wgt(wpvMax * 4 - 1) As System.Half
        Dim idx(wpvMax * 4 - 1) As Byte
        Dim a(FastGeom.MatDoubles - 1) As Double
        Dim b2(FastGeom.MatDoubles - 1) As Double

        For iter As Integer = 0 To 999
            Dim wpv As Integer = 1 + CInt(NextB() Mod 4UL)
            For s = 0 To wgt.Length - 1
                wgt(s) = CType(pesos(CInt(NextB() Mod CULng(pesos.Length))), System.Half)
                ' Indices a proposito FUERA de rango una de cada ~4 veces: la guarda
                ' `idx < precomputed.Length` tiene que descartarlos igual por los dos caminos.
                idx(s) = CByte(NextB() Mod CULng(nBones + 2))
            Next
            Dim baseIdx As Integer = CInt(NextB() Mod 3UL) * wpv

            ' LOS DOS LADOS USAN LA PALETA PLANA. Forzar el lado "escalar" con `flatPal:=Nothing` cae en el
            ' camino que acumula `precomputed(idx) * w` en Matrix4d —en DOUBLE y desde otro array— y compara
            ' dos LEYES, no dos implementaciones: sale `escalar=0,14812919066753238`
            ' `vectorial=0,1481291949748993`, que es exactamente la diferencia Double/Single y NO un bug del
            ' vectorial. El toggle `ForzarEscalarS` compara lo que hay que comparar.
            Dim mVec = BlendBoneMatrices(wgt, idx, baseIdx, wpv, precomputed, flatPal)
            Dim mEsc As Matrix4d
            ' Se restaura el valor PREVIO, no un False fijo: si un arnes dejo el toggle encendido para
            ' medir, correr el self-test se lo apagaba en silencio y la medicion pasaba a ser del camino
            ' vectorial sin que nada avisara.
            Dim previoM = FastGeom.ForzarEscalarS
            FastGeom.ForzarEscalarS = True
            Try
                mEsc = BlendBoneMatrices(wgt, idx, baseIdx, wpv, precomputed, flatPal)
            Finally
                FastGeom.ForzarEscalarS = previoM
            End Try
            FastGeom.StoreMatrix(mEsc, a, 0)
            FastGeom.StoreMatrix(mVec, b2, 0)
            For e = 0 To FastGeom.MatDoubles - 1
                If BitConverter.DoubleToInt64Bits(a(e)) <> BitConverter.DoubleToInt64Bits(b2(e)) Then
                    Return $"[skin-blend] iter {iter} wpv={wpv} base={baseIdx}: elemento {e} difiere " &
                           $"(escalar={a(e):R} vectorial={b2(e):R}, {FastGeom.WidthInfo})"
                End If
            Next
        Next

        ' Degenerados que tienen que devolver LO MISMO por los dos caminos sin tirar.
        Dim vacio() As Matrix4d = Array.Empty(Of Matrix4d)()
        Dim palVacia = FastGeom.BuildFlatPaletteS(vacio)
        Dim m1 = BlendBoneMatrices(wgt, idx, 0, 4, vacio)
        Dim m2 = BlendBoneMatrices(wgt, idx, 0, 4, vacio, palVacia)
        FastGeom.StoreMatrix(m1, a, 0) : FastGeom.StoreMatrix(m2, b2, 0)
        For e = 0 To FastGeom.MatDoubles - 1
            If BitConverter.DoubleToInt64Bits(a(e)) <> BitConverter.DoubleToInt64Bits(b2(e)) Then
                Return $"[skin-blend-vacio] elemento {e} difiere con paleta vacia"
            End If
        Next
        If BlendBoneMatrices(Nothing, idx, 0, 4, precomputed, flatPal) <> precomputed(0) Then
            Return "[skin-blend-nulo] pesos Nothing no devolvio precomputed(0)"
        End If
        If BlendBoneMatrices(wgt, idx, 0, 0, precomputed, flatPal) <> precomputed(0) Then
            Return "[skin-blend-wpv0] wpv=0 no devolvio precomputed(0)"
        End If

        ' LA RAMA DEL GATE DEL MOTOR. `EngineSkinWeightNormalization.Enabled` es False por default, asi que
        ' TryComputeWeights sale temprano y todo lo de arriba ejercita SOLO la rama normal.
        ' SE CORRE SOLO SI EL USUARIO YA LA TIENE PRENDIDA —que es cuando esa rama le importa a SU bake— y NO
        ' se toca la global: un test no muta estado de produccion en caliente en una app que se distribuye. Si
        ' algo tira entre el Set y el Finally, o si otro hilo lee la propiedad en esa ventana, el bake sale con
        ' una ley que el usuario no eligio. El caso FORZADO (prenderla para cubrir la rama con el default
        ' apagado) es un gate de BUILD y vive en Tools/ParityGate, que si puede mutar lo que quiera.
        If EngineSkinWeightNormalization.Enabled Then
            For iter As Integer = 0 To 499
                For s = 0 To wgt.Length - 1
                    wgt(s) = CType(pesos(CInt(NextB() Mod CULng(pesos.Length))), System.Half)
                    idx(s) = CByte(NextB() Mod CULng(nBones + 2))
                Next
                Dim baseIdx As Integer = CInt(NextB() Mod 3UL) * 4
                ' Mismo criterio que el bucle de arriba: los dos lados con la paleta plana, el escalar
                ' forzado con el toggle. Ver el comentario de alla.
                Dim gVec = BlendBoneMatrices(wgt, idx, baseIdx, 4, precomputed, flatPal)
                Dim gEsc As Matrix4d
                Dim previoG = FastGeom.ForzarEscalarS
                FastGeom.ForzarEscalarS = True
                Try
                    gEsc = BlendBoneMatrices(wgt, idx, baseIdx, 4, precomputed, flatPal)
                Finally
                    FastGeom.ForzarEscalarS = previoG
                End Try
                FastGeom.StoreMatrix(gEsc, a, 0) : FastGeom.StoreMatrix(gVec, b2, 0)
                For e = 0 To FastGeom.MatDoubles - 1
                    If BitConverter.DoubleToInt64Bits(a(e)) <> BitConverter.DoubleToInt64Bits(b2(e)) Then
                        Return $"[skin-blend-enginenorm] iter {iter} base={baseIdx}: elemento {e} difiere " &
                               $"(escalar={a(e):R} vectorial={b2(e):R}, {FastGeom.WidthInfo})"
                    End If
                Next
            Next
        End If

        Return ""
    End Function

    ''' <summary>Extrae vértices, normales, tangentes y bitangentes del shape, aplicando el mismo skinning
    ''' que LoadShapeSafe, y arma los arrays de índices y pesos que consume la GPU.
    ''' <para>SYNC: CPU/GPU skinning — acá se NORMALIZAN los pesos que después usa el vertex shader
    ''' (sum=1), así que un cambio en este sitio mueve la GPU sin tocar el camino CPU. Lista completa de
    ''' sitios gemelos en el contrato de <c>BlendBoneMatrices</c> y en 00-reglas-ui-y-vb.md §10.</para></summary>
    ''' <param name="skeleton">SkeletonInstance to read bind/pose transforms from. If Nothing,
    ''' falls back to <see cref="SkeletonInstance.Default"/>. Pose application is implicit:
    ''' bones whose <see cref="HierarchiBone_class.DeltaTransform"/> is set get pose-folded;
    ''' bones with DeltaTransform=Nothing collapse to bind. Callers that want a "render bind
    ''' regardless of stored pose" call <see cref="SkeletonInstance.Reset"/> on the instance
    ''' first (they are responsible for the side effect on shared instances).</param>
    Public Shared Function ExtractSkinnedGeometry(shape As IRenderableShape, singleboneskinning As Boolean, RecalculateNormals As Boolean, Optional skeleton As SkeletonInstance = Nothing) As SkinnedGeometry
        Dim effectiveSkel As SkeletonInstance = If(skeleton, SkeletonInstance.Default)
        Dim shapeGeom = shape.Geometry
        If shapeGeom Is Nothing Then Throw New InvalidOperationException("IRenderableShape.Geometry is null")
        Dim backing = shapeGeom.BackingShape
        ' Capture the bone palette ONCE. Do NOT re-read shape.ShapeBones/ShapeBoneTransforms inline
        ' (.Count or (k) on the property): some IRenderableShape impls (Wardrobe OSP_Clases) re-parse
        ' the NIF skin instance on every getter call, so repeated reads would be O(bones) re-parses.
        Dim bones = shape.ShapeBones
        Dim boneTrans = shape.ShapeBoneTransforms

        If boneTrans.Count <> bones.Count Then Throw New Exception("BonesTransform and Bones are out of sync")
        Dim Nifversion = shape.NifContent.Header.Version
        ' 1) Transformación global del shape
        Dim shapeNode = TryCast(shape.NifContent.GetParentNode(backing), NiNode)
        If IsNothing(shapeNode) Then
#If DEBUG Then
            Debugger.Break()
#End If
            shapeNode = shape.NifContent.GetRootNode()
        End If

        Dim GlobalTransform = Matrix4d.Identity

        ' 2) Datos brutos — la INVERTIDAS swap y el byte-decode de TBN viven en el adapter:
        '    GetTangents/GetBitangents devuelven ya en convención del renderer.
        Dim srcVertexPositions = shapeGeom.GetVertexPositions()
        Dim rawVerts(srcVertexPositions.Count - 1) As Vector3d
        For i = 0 To srcVertexPositions.Count - 1
            Dim v = srcVertexPositions(i)
            rawVerts(i) = New Vector3d(v.X, v.Y, v.Z)
        Next
        ' N/T/B se ALMACENAN en Single (ver SkinnedGeometry.Normals). La normalizacion de entrada se
        ' sigue haciendo en Double y solo se redondea al guardar: es el mismo valor que antes llegaba
        ' al NIF, porque la salida ya pasaba por CSng en InjectNormalsToTrishape.
        Dim rawNormals() As Vector3
        Dim rawTangents() As Vector3
        Dim rawBitangs() As Vector3

        If shapeGeom.HasNormals Then
            Dim srcNormals = shapeGeom.GetNormals()
            rawNormals = New Vector3(rawVerts.Length - 1) {}
            Parallel.ForEach(RangosDe(rawVerts.Length),
                Sub(rango As Tuple(Of Integer, Integer))
                    For i = rango.Item1 To rango.Item2 - 1
                        Dim v As New Vector3d(srcNormals(i).X, srcNormals(i).Y, srcNormals(i).Z)
                        Dim l = v.Length
                        rawNormals(i) = If(l > 0.000001, RecalcTBN.ASng(v / l), Vector3.Zero)
                    Next
                End Sub)
        Else
            rawNormals = New Vector3(rawVerts.Length - 1) {}
        End If

        If shapeGeom.HasTangents Then
            Dim srcTan = shapeGeom.GetTangents()
            Dim srcBit = shapeGeom.GetBitangents()
            rawTangents = New Vector3(rawVerts.Length - 1) {}
            rawBitangs = New Vector3(rawVerts.Length - 1) {}
            Parallel.ForEach(RangosDe(rawVerts.Length),
                Sub(rango As Tuple(Of Integer, Integer))
                    For i = rango.Item1 To rango.Item2 - 1
                        Dim t = srcTan(i)
                        Dim b = srcBit(i)
                        Dim tv As New Vector3d(t.X, t.Y, t.Z)
                        Dim bv As New Vector3d(b.X, b.Y, b.Z)
                        Dim tl = tv.Length
                        Dim bl = bv.Length
                        rawTangents(i) = If(tl > 0.000001, RecalcTBN.ASng(tv / tl), Vector3.Zero)
                        rawBitangs(i) = If(bl > 0.000001, RecalcTBN.ASng(bv / bl), Vector3.Zero)
                    Next
                End Sub)
        Else
            rawTangents = New Vector3(rawVerts.Length - 1) {}
            rawBitangs = New Vector3(rawVerts.Length - 1) {}
        End If

        ' Polymorphic per-vertex skinning data (BSTriShape inline, NiTriShape NiSkinPartition).
        Dim shapeSkin As ShapeSkinningData = shapeGeom.GetSkinning()

        Dim vertexCount As Integer = rawVerts.Length
        Dim vertexColorsList = If(shapeGeom.HasVertexColors, shapeGeom.GetVertexColors(), Nothing)
        Dim uvsList = If(shapeGeom.HasUVs, shapeGeom.GetUVs(), Nothing)
        If Not ((rawNormals.Length = vertexCount OrElse Not shapeGeom.HasNormals) AndAlso
                (rawTangents.Length = vertexCount OrElse Not shapeGeom.HasNormals) AndAlso
                (rawBitangs.Length = vertexCount OrElse Not shapeGeom.HasNormals) AndAlso
                (Not shapeGeom.HasVertexColors OrElse vertexColorsList.Count = vertexCount) AndAlso
                (Not shapeGeom.HasUVs OrElse uvsList.Count = vertexCount)) Then
#If DEBUG Then
            Debugger.Break()
#End If
            Throw New Exception("The vertex attributes do not all have the same length!")
        End If


        ' 3) Calcular matrices bind-pose y pose actual
        Dim matsBind(bones.Count - 1) As Matrix4d
        Dim matsPose(bones.Count - 1) As Matrix4d
        For k = 0 To bones.Count - 1
            Dim localT = boneTrans(k)
            Dim boneName = bones(k).Name.String
            Dim bindT As Transform_Class
            Dim poseT As Transform_Class
            Dim SkeletonBone As HierarchiBone_class = Nothing

            If effectiveSkel.SkeletonDictionary.TryGetValue(boneName, SkeletonBone) Then
                bindT = SkeletonBone.OriginalGetGlobalTransform
            Else
                bindT = Transform_Class.GetGlobalTransform(bones(k), shape.NifContent)
            End If

            matsBind(k) = bindT.ComposeTransforms(localT).ToMatrix4d()

            If Not singleboneskinning AndAlso Not IsNothing(SkeletonBone) Then
                poseT = SkeletonBone.GetGlobalTransform()
                matsPose(k) = poseT.ComposeTransforms(localT).ToMatrix4d()
            Else
                poseT = bindT
                matsPose(k) = matsBind(k)
            End If

            ' [SKIN-MAT] DIAG A/B: skin-matrix REAL de render por shape/hueso (poseT ∘ localT = world de un
            ' vértice bone-local-origin). Comparar OFF (baseline) vs ON (rebind) revela qué shapes/huesos
            ' divergen (p.ej. armaduras no rebindeadas). Read-only, gateado a Logger.Enabled.
            If Logger.Enabled Then
                Dim skm = poseT.ComposeTransforms(localT)
                Dim skt = skm.Translation
                Dim bt = bindT.Translation, lt = localT.Translation
                ' src= de qué rama salió el bind. Las dos ramas dan mundos DISTINTOS, así que un shape
                ' con huesos mezclados tiene la paleta partida y el blend LBS deforma (single-bone lo
                ' tapa porque usa sólo pal[0]). Sin este campo el [SKIN-MAT] no permite distinguirlo.
                Dim src = If(SkeletonBone Is Nothing, "nif-fallback", "skel")
                Dim shNm = shape.ShapeName, bnNm = boneName, kIdx = k
                Logger.LogLazy(Function() $"[SKIN-MAT] shape='{shNm}' bone[{kIdx}]='{bnNm}' src={src} skin.T=({skt.X:F3},{skt.Y:F3},{skt.Z:F3}) bind.T=({bt.X:F3},{bt.Y:F3},{bt.Z:F3}) local.T=({lt.X:F3},{lt.Y:F3},{lt.Z:F3})")
            End If
        Next

        ' [SKIN-SKEL] Resumen por shape: qué esqueleto está vivo y cuántos huesos salieron de cada rama.
        ' Una paleta partida (ambos contadores > 0) es condición SUFICIENTE para que full skinning
        ' deforme y single-bone no.
        If Logger.Enabled Then
            Dim nSkel = 0, nFallback = 0
            For k = 0 To bones.Count - 1
                If effectiveSkel.SkeletonDictionary.ContainsKey(bones(k).Name.String) Then nSkel += 1 Else nFallback += 1
            Next
            Dim shNm2 = shape.ShapeName
            Dim skelFile = If(effectiveSkel.HasSkeleton, Config_App.Current.SkeletonFilePath, "<SIN ESQUELETO CARGADO>")
            Dim nDict = effectiveSkel.SkeletonDictionary.Count
            Dim isDefault = (effectiveSkel Is SkeletonInstance.Default)
            Dim nb = bones.Count
            Logger.LogLazy(Function() $"[SKIN-SKEL] shape='{shNm2}' bones={nb} src:skel={nSkel} src:nif-fallback={nFallback} PALETA-PARTIDA={(nSkel > 0 AndAlso nFallback > 0)} skelDict={nDict} isDefaultInstance={isDefault} skelFile='{skelFile}'")
        End If

        ' 4) Aplicar skinning CPU
        ' Save NIF-local vertices BEFORE skinning (needed for correct morph-space application)
        Dim nifLocalVerts = rawVerts.ToArray()
        ' Se guarda en Single y en SoA: ver SkinMatricesSoA.
        Dim perVertexMtot As New SkinMatricesSoA(vertexCount)

        ' O2.4: Parallel options — use regular For for small meshes, bound parallelism for large ones
        Dim useParallel As Boolean = vertexCount >= 500
        Dim parallelOpts As New ParallelOptions With {.MaxDegreeOfParallelism = Environment.ProcessorCount}

        ' GPU Skinning: allocate flat arrays for per-vertex bone data
        Dim gpuBoneIdx(vertexCount * 4 - 1) As Byte
        Dim gpuBoneWgt(vertexCount * 4 - 1) As Single
        Dim gpuBoneMats() As Matrix4 = Nothing

        ' Branching predicate: shape.IsSkinned (parity con Outfit Studio Anim.cpp:692, 706,
        ' GLSurface.cpp:1247 — IsSkinned() es !skinInstanceRef.IsEmpty()). NO usamos bones.Length
        ' como predicado primario porque depende de que ResolveSkinData haya resuelto la palette;
        ' un shape con SkinInstanceRef válido pero bones no resueltos (post-merge, índices
        ' inválidos, etc.) es IsSkinned=True con bones.Length=0 y debe tratarse como skinned
        ' degenerado, no como unskinned. bones.Length>0 sigue como guard secundario para
        ' acceso seguro a matsBind/matsPose.
        Select Case True
            Case shape.IsSkinned AndAlso Not singleboneskinning AndAlso bones.Count > 0
                ' La fórmula vive en BuildPosePalette y en ningún otro lado. Ver su docstring.
                Dim precomputedBoneMatrices = BuildPosePalette(matsPose, GlobalTransform)
                ' Paleta plana para el blend vectorial. Se arma UNA vez por shape (20-60 matrices),
                ' no por vertice: la copia no esta en el camino caliente. Ver FastGeom.
                Dim flatPalette = FastGeom.BuildFlatPaletteS(precomputedBoneMatrices)

                ' GPU Skinning: compute float-precision bone matrices for SSBO upload
                gpuBoneMats = New Matrix4(bones.Count - 1) {}
                For k = 0 To bones.Count - 1
                    Dim m = precomputedBoneMatrices(k)
                    gpuBoneMats(k) = New Matrix4(
                        CSng(m.M11), CSng(m.M12), CSng(m.M13), CSng(m.M14),
                        CSng(m.M21), CSng(m.M22), CSng(m.M23), CSng(m.M24),
                        CSng(m.M31), CSng(m.M32), CSng(m.M33), CSng(m.M34),
                        CSng(m.M41), CSng(m.M42), CSng(m.M43), CSng(m.M44))
                Next

                ' Multibone skinning inner loop — GPU path: store perVertexMtot + extract bone data, do NOT transform rawVerts/N/T/B.
                ' Per-vertex bone influences come from the polymorphic ShapeSkinningData (BSTriShape inline or NiSkinPartition).
                Dim skinFlatIdx = shapeSkin.BoneIndices
                Dim skinFlatWgt = shapeSkin.BoneWeights
                Dim skinWpv = If(shapeSkin.WeightsPerVertex > 0, shapeSkin.WeightsPerVertex, 4)
                Dim hasSkin = (skinFlatIdx IsNot Nothing AndAlso skinFlatWgt IsNot Nothing AndAlso shapeSkin.VertexCount = vertexCount)

                ' LOS MISMOS CUATRO PATRONES QUE EL BLEND. Este bucle NO corre por frame —corre al re-extraer
                ' geometria: cambiar un morph, un preset, un outfit— pero es lo que el usuario siente como
                ' "tarda en responder": sin tratarlos son 116-120 ms por re-extraccion sobre el Serena Battle
                ' Suit. Los cuatro, ya probados en FillPerVertexSkinMatrix:
                '   1. delegate por vertice -> se llama desde un bucle por RANGOS
                '   2. `Dim ckWg(3) As Single` adentro = alocacion en heap POR VERTICE -> al scratch por hilo
                '   3. `TryComputeWeights` sin corto-circuito de `Enabled` (que es False por default)
                '   4. `AMatrix4(Mtot)` -> Matrix4 intermedia -> indexador -> 12 escrituras
                Dim skinningBody As Action(Of Integer, BlendScratch) = Sub(i, scE)
                                                             Dim Mtot As Matrix4d
                                                             Dim baseIdx = i * 4

                                                             If hasSkin Then
                                                                 Dim baseSkin = i * skinWpv
                                                                 Mtot = BlendBoneMatrices(skinFlatWgt, skinFlatIdx, baseSkin, skinWpv, precomputedBoneMatrices, flatPalette)

                                                                 ' GPU arrays: copy up to 4 slots, normalize weights to sum=1.
                                                                 ' Normalizacion de pesos del MOTOR: el shader ya suma Σ w·bone SIN dividir, así que
                                                                 ' alcanza con subir acá los pesos de la ley (w3 = 1−Σ, y 0 en vez de descartar, que es
                                                                 ' lo mismo porque el shader saltea los ≤0) para que el GPU quede igual que el CPU.
                                                                 ' Gate apagado (default) ⇒ se toma la rama normalizada de siempre, bit-idéntica.
                                                                 Dim ckWg = scE.CkW
                                                                 If EngineSkinWeightNormalization.Enabled AndAlso
                                                                    EngineSkinWeightNormalization.TryComputeWeights(skinFlatWgt, baseSkin, skinWpv, ckWg) Then
                                                                     For j = 0 To 3
                                                                         gpuBoneIdx(baseIdx + j) = skinFlatIdx(baseSkin + j)
                                                                         gpuBoneWgt(baseIdx + j) = If(ckWg(j) > 0.0F, ckWg(j), 0.0F)
                                                                     Next
                                                                 Else
                                                                     Dim copySlots = Math.Min(4, skinWpv)
                                                                     Dim localSumW As Double = 0
                                                                     For j = 0 To copySlots - 1
                                                                         localSumW += CType(skinFlatWgt(baseSkin + j), Double)
                                                                     Next
                                                                     For j = 0 To 3
                                                                         If j < copySlots Then
                                                                             gpuBoneIdx(baseIdx + j) = skinFlatIdx(baseSkin + j)
                                                                             gpuBoneWgt(baseIdx + j) = CSng(If(localSumW > 0, CType(skinFlatWgt(baseSkin + j), Double) / localSumW, 0))
                                                                         Else
                                                                             gpuBoneIdx(baseIdx + j) = 0
                                                                             gpuBoneWgt(baseIdx + j) = 0.0F
                                                                         End If
                                                                     Next
                                                                 End If
                                                             Else
                                                                 ' No per-vertex skin data — bind to bone 0 with full weight (same fallback as before).
                                                                 Mtot = If(precomputedBoneMatrices.Length > 0, precomputedBoneMatrices(0), Matrix4d.Identity)
                                                                 gpuBoneIdx(baseIdx) = 0 : gpuBoneWgt(baseIdx) = 1.0F
                                                                 gpuBoneIdx(baseIdx + 1) = 0 : gpuBoneWgt(baseIdx + 1) = 0.0F
                                                                 gpuBoneIdx(baseIdx + 2) = 0 : gpuBoneWgt(baseIdx + 2) = 0.0F
                                                                 gpuBoneIdx(baseIdx + 3) = 0 : gpuBoneWgt(baseIdx + 3) = 0.0F
                                                             End If

                                                             ' Mtot va DIRECTO a las 12 secciones: `AMatrix4(Mtot)` construia una Matrix4
                                                             ' de 64 B que el indexador desarmaba con 12 escrituras. Mismos bits.
                                                             perVertexMtot.EstablecerDesde(i, Mtot)
                                                         End Sub

                If useParallel Then
                    ' Por RANGOS: el scratch se toma una vez por rango en vez de alocar por vertice.
                    Parallel.ForEach(RangosDe(vertexCount),
                        Sub(rango As Tuple(Of Integer, Integer))
                            Dim scE = GetBlendScratch(Math.Max(skinWpv, EngineSkinWeightNormalization.Slots))
                            For i = rango.Item1 To rango.Item2 - 1
                                skinningBody(i, scE)
                            Next
                        End Sub)
                Else
                    Dim scE = GetBlendScratch(Math.Max(skinWpv, EngineSkinWeightNormalization.Slots))
                    For i As Integer = 0 To vertexCount - 1
                        skinningBody(i, scE)
                    Next
                End If

            Case shape.IsSkinned AndAlso singleboneskinning AndAlso bones.Count > 0
                ' Single-bone: pre-compute once — GPU path: do NOT transform rawVerts/N/T/B
                Dim Mtot = GlobalTransform * matsPose(0)
                perVertexMtot.Llenar(AMatrix4(Mtot))

                ' GPU Skinning: single bone matrix for SSBO
                gpuBoneMats = New Matrix4(0) {}
                gpuBoneMats(0) = New Matrix4(
                    CSng(Mtot.M11), CSng(Mtot.M12), CSng(Mtot.M13), CSng(Mtot.M14),
                    CSng(Mtot.M21), CSng(Mtot.M22), CSng(Mtot.M23), CSng(Mtot.M24),
                    CSng(Mtot.M31), CSng(Mtot.M32), CSng(Mtot.M33), CSng(Mtot.M34),
                    CSng(Mtot.M41), CSng(Mtot.M42), CSng(Mtot.M43), CSng(Mtot.M44))

                ' All vertices reference bone 0 with weight 1.0
                For i As Integer = 0 To vertexCount - 1
                    Dim baseIdx = i * 4
                    gpuBoneIdx(baseIdx) = 0 : gpuBoneWgt(baseIdx) = 1.0F
                    gpuBoneIdx(baseIdx + 1) = 0 : gpuBoneWgt(baseIdx + 1) = 0.0F
                    gpuBoneIdx(baseIdx + 2) = 0 : gpuBoneWgt(baseIdx + 2) = 0.0F
                    gpuBoneIdx(baseIdx + 3) = 0 : gpuBoneWgt(baseIdx + 3) = 0.0F
                Next

            Case Else
                ' Dos sub-casos colapsados acá:
                '  (A) shape.IsSkinned=False  → genuinamente unskinned. Mtot = shape.T/R/S × parent_chain
                '      (paridad con Outfit Studio: Anim.cpp:692-704 GetTransformShapeToGlobal).
                '  (B) shape.IsSkinned=True AndAlso bones.Length=0 → skinned degenerado (skin instance
                '      presente pero palette de huesos no resoluble, p.ej. post-merge con bone refs
                '      colgados). OS devuelve MatTransform() = identity en este caso (Anim.cpp:711).
                '      NO aplicamos shape.T/R/S+parents porque los vértices están en skin-space, no
                '      en shape-local; mezclar parent chain ahí los manda "a cualquier parte".
                Dim Mtot As Matrix4d
                If shape.IsSkinned Then
                    ' (B) Degenerado: identity, igual que OS.
                    Mtot = Matrix4d.Identity
                Else
                    ' (A) Unskinned puro: shape.T/R/S × parent chain.
                    Mtot = Transform_Class.GetGlobalTransform(backing, shape.NifContent).ToMatrix4d()
                End If

                perVertexMtot.Llenar(AMatrix4(Mtot))

                ' GPU Skinning: single bone matrix for SSBO
                gpuBoneMats = New Matrix4(0) {}
                gpuBoneMats(0) = New Matrix4(
                    CSng(Mtot.M11), CSng(Mtot.M12), CSng(Mtot.M13), CSng(Mtot.M14),
                    CSng(Mtot.M21), CSng(Mtot.M22), CSng(Mtot.M23), CSng(Mtot.M24),
                    CSng(Mtot.M31), CSng(Mtot.M32), CSng(Mtot.M33), CSng(Mtot.M34),
                    CSng(Mtot.M41), CSng(Mtot.M42), CSng(Mtot.M43), CSng(Mtot.M44))

                ' All vertices reference bone 0 with weight 1.0
                For i As Integer = 0 To vertexCount - 1
                    Dim baseIdx = i * 4
                    gpuBoneIdx(baseIdx) = 0 : gpuBoneWgt(baseIdx) = 1.0F
                    gpuBoneIdx(baseIdx + 1) = 0 : gpuBoneWgt(baseIdx + 1) = 0.0F
                    gpuBoneIdx(baseIdx + 2) = 0 : gpuBoneWgt(baseIdx + 2) = 0.0F
                    gpuBoneIdx(baseIdx + 3) = 0 : gpuBoneWgt(baseIdx + 3) = 0.0F
                Next
        End Select
        ' 7) Bounding center — rawVerts is now local-space, compute world-space bounds via PerVertexSkinMatrix
        Dim minV As New Vector3d(Double.MaxValue)
        Dim maxV As New Vector3d(Double.MinValue)
        For i As Integer = 0 To rawVerts.Length - 1
            ' Sin la Matrix4 ni la Matrix4d intermedias: ver SkinMatricesSoA.TransformarPosicion.
            Dim wv = perVertexMtot.TransformarPosicion(i, rawVerts(i))
            If wv.X < minV.X Then minV.X = wv.X
            If wv.Y < minV.Y Then minV.Y = wv.Y
            If wv.Z < minV.Z Then minV.Z = wv.Z

            If wv.X > maxV.X Then maxV.X = wv.X
            If wv.Y > maxV.Y Then maxV.Y = wv.Y
            If wv.Z > maxV.Z Then maxV.Z = wv.Z
        Next
        Dim center = (minV + maxV) * 0.5

        ' Pre-compute indices (avoid SelectMany creating thousands of temp arrays)
        Dim trianglesList = shapeGeom.GetTriangles()
        Dim flatIndices As UInteger()
        If trianglesList IsNot Nothing AndAlso trianglesList.Count > 0 Then
            flatIndices = New UInteger(trianglesList.Count * 3 - 1) {}
            For ti = 0 To trianglesList.Count - 1
                flatIndices(ti * 3) = trianglesList(ti).V1
                flatIndices(ti * 3 + 1) = trianglesList(ti).V2
                flatIndices(ti * 3 + 2) = trianglesList(ti).V3
            Next
        Else
            flatIndices = Array.Empty(Of UInteger)()
        End If

        ' Pre-compute vertex colors
        Dim vtxColors As Vector4()
        If shapeGeom.HasVertexColors Then
            vtxColors = New Vector4(vertexCount - 1) {}
            Parallel.ForEach(RangosDe(vertexCount),
                Sub(rango As Tuple(Of Integer, Integer))
                    For i = rango.Item1 To rango.Item2 - 1
                        vtxColors(i) = New Vector4(vertexColorsList(i).R, vertexColorsList(i).G, vertexColorsList(i).B, vertexColorsList(i).A)
                    Next
                End Sub)
        Else
            vtxColors = New Vector4(vertexCount - 1) {}
            Array.Fill(vtxColors, New Vector4(1.0F, 1.0F, 1.0F, 1.0F))
        End If

        Dim vtxMask = New Single(vertexCount - 1) {}
        Dim dirtyVFlags = New Boolean(vertexCount - 1) {}
        Dim dirtyMFlags = New Boolean(vertexCount - 1) {}
        Array.Fill(dirtyVFlags, True)
        Array.Fill(dirtyMFlags, True)

        Dim geo = New SkinnedGeometry With {
            .Vertices = rawVerts,
            .BaseVertices = rawVerts.ToArray,
            .NifLocalVertices = nifLocalVerts,
            .PerVertexSkinMatrix = perVertexMtot,
            .Normals = rawNormals,
            .Tangents = rawTangents,
            .Bitangents = rawBitangs,
            .ParentGlobalTransform = GlobalTransform,
            .BoneMatsBind = matsBind,
            .BoneMatsPose = matsPose,
            .Indices = flatIndices,
            .VertexColors = vtxColors,
            .Eyedata = If(shapeGeom.HasEyeData, shapeGeom.GetEyeData().ToArray(), New Single(vertexCount - 1) {}),
            .Geometry = shapeGeom,
            .Skinning = shapeSkin,
            .VertexMask = vtxMask,
            .dirtyVertexIndices = ConjuntoDeSucios.Todos(vertexCount),
            .dirtyMaskIndices = ConjuntoDeSucios.Todos(vertexCount),
            .dirtyMaskFlags = dirtyMFlags,
            .dirtyVertexFlags = dirtyVFlags,
             .Boundingcenter = center,
             .Minv = minV,
             .Maxv = maxV,
             .CachedTBN = Nothing,
             .Version = Nifversion,
             .GPUBoneIndices = gpuBoneIdx,
             .GPUBoneWeights = gpuBoneWgt,
             .GPUBoneMatrices = gpuBoneMats,
             .WorldCacheValid = False,
             .PerVertexMatrixValid = True,
             .ZapTopologyDirty = True
        }

        ' Uvs_Weight packs per-vertex UV (X,Y) and the first bone weight (Z) — used by the
        ' shader weight-paint visualization.  Sourced from the polymorphic skinning data so it
        ' works for both BSTriShape (BoneWeights inline) and NiTriShape (NiSkinPartition).
        Dim uvsWeight(vertexCount - 1) As Vector3
        Dim wpvForUv = If(shapeSkin.WeightsPerVertex > 0, shapeSkin.WeightsPerVertex, 4)
        Dim hasSkinForUv = (shapeSkin.BoneWeights IsNot Nothing AndAlso shapeSkin.VertexCount = vertexCount)
        For i As Integer = 0 To vertexCount - 1
            Dim u As Single = 0
            Dim v As Single = 0
            If shapeGeom.HasUVs AndAlso uvsList IsNot Nothing AndAlso i < uvsList.Count Then
                u = uvsList(i).U
                v = uvsList(i).V
            End If
            Dim w0 As Single = 0
            If hasSkinForUv Then w0 = CType(shapeSkin.BoneWeights(i * wpvForUv), Single)
            uvsWeight(i) = New Vector3(u, v, w0)
        Next
        geo.Uvs_Weight = uvsWeight
        geo.BaseUvs_Weight = CType(uvsWeight.Clone(), Vector3())

        ' ⛔ LAS TANGENTES NO VAN GATEADAS. El canónico las recalcula en CADA build y para CADA shape, sin
        ' condición: `BodySlideApp.cpp:3904` (y sus gemelas :3932, :4778, :4806) tiene
        ' `nifBig.CalcTangentsForShape(shape);` FUERA del `if (!lockNormals)`; lo único gateado es el pase
        ' de NORMALES. Antes las dos cosas estaban dentro del mismo `If`, así que con el ajuste
        ' "Recalculate normals" APAGADO no se rehacía ninguna de las dos y el marco tangente salía el del
        ' .nif fuente — movido de posición pero sin rebasar.
        '
        ' MEDIDO: las 11.725 shapes de FO4 y 3.844 de las 4.821 de SSE traen normales Y tangentes, así que
        ' con el ajuste apagado el `If` viejo no pasaba NUNCA para ellas. Contra una reimplementación de la
        ' fase canónica —que reproduce 2.553 shapes del corpus byte a byte—, eso dejaba 1.358 de los 3.190
        ' sliderSets con un marco tangente medible­mente distinto del que emite BodySlide: 509 por encima
        ' de 1°, 208 por encima de 5° y 30 por encima de 15°, con un peor caso de 32° de media.
        '
        ' Ahora la función corre SIEMPRE y quien decide el pase de normales es `KeepExistingNormals`:
        '   • ajuste ENCENDIDO (el default)      -> idéntico a antes, normales y tangentes.
        '   • ajuste APAGADO y con normales      -> sólo tangentes. Es el cambio.
        '   • sin canal de normales              -> idéntico a antes, se recalculan las dos.
        ' Y la inyección sigue gateada aparte (`InjectToTrishape`, :1473 `HasNormals OrElse HasTangents`),
        ' así que una shape sin ninguno de los dos canales sigue sin recibir escritura.
        '
        ' ⚠️ EL OTRO SITIO QUE RECALCULA NO SE TOCÓ: `MorphingHelper.ApplyMorph_CPU` (:307) tiene su propio
        ' gate `(RecalculateNormals AndAlso huboCambioDePosicion) OrElse movioUVs`. Sacarlo de ahí también
        ' sería la ley canónica, pero eso corre por cada aplicación de morph —o sea por cada movimiento de
        ' slider en el preview— y el costo hay que medirlo antes. Queda declarado, no hecho.
        Dim opts = Config_App.Current.Setting_TBN
        ' `Setting_TBN` es una Structure devuelta POR VALOR desde una Property, así que esto es una COPIA
        ' y mutarla no toca el config del usuario. (Verificado: Config_Class.vb:520.)
        If Not (RecalculateNormals OrElse Not shapeGeom.HasNormals) Then opts.KeepExistingNormals = True
        RecalcTBN.AplicarRestriccionesDelAutor(opts, shape)
        RecalculateNormalsTangentsBitangents(geo, opts)
        Return geo
    End Function


    ''' <summary>SOLO PARA MEDIR: apaga el camino vectorial del kernel. Ver FastSkin.ForzarEscalar.</summary>
    Friend Shared Sub FastSkinForzarEscalar(v As Boolean)
        FastSkin.ForzarEscalar = v
    End Sub

    ''' <summary>SOLO PARA MEDIR: elige el camino con staging+SIMD o el directo.</summary>
    Friend Shared Sub FastSkinUsarStaging(v As Boolean)
        FastSkin.UsarStaging = v
    End Sub

    ''' <summary>Puente para el camino SPARSE del upload. Ver FastSkin.UnVertice.</summary>
    Friend Shared Sub FastSkinUnVertice(m As Matrix4, p As Vector3d, vn As Vector3, vt As Vector3, vb As Vector3,
                                        msn As Boolean, ByRef pos As Vector3, ByRef nrm As Vector3,
                                        ByRef tan As Vector3, ByRef bit As Vector3)
        FastSkin.UnVertice(m, p, vn, vt, vb, msn, pos, nrm, tan, bit)
    End Sub

    ''' <summary>Puente al kernel de <see cref="FastSkin"/>. Existe para que Render.vb no tenga que
    ''' conocer un Module aparte: el punto de entrada del skinning de CPU sigue siendo SkinningHelper.
    ''' </summary>
    Friend Shared Sub FastSkinTransformar(mats As SkinMatricesSoA, lv() As Vector3d,
                                          ln() As Vector3, lt() As Vector3, lb() As Vector3,
                                          msn As Boolean, n As Integer,
                                          posOut() As Vector3, nrmOut() As Vector3,
                                          tanOut() As Vector3, bitOut() As Vector3)
        If FastSkin.UsarStaging Then
            FastSkin.Transformar(mats, lv, ln, lt, lb, msn, n, posOut, nrmOut, tanOut, bitOut)
        Else
            FastSkin.TransformarDirecto(mats, lv, ln, lt, lb, msn, n, posOut, nrmOut, tanOut, bitOut)
        End If
    End Sub

    ''' <summary>Matrix4d -> Matrix4. Se usa al GUARDAR en <c>PerVertexSkinMatrix</c>: el blend sigue
    ''' evaluandose en Double (es la ley canonica, con su self-test SIMD bit a bit), y lo unico que baja
    ''' de precision es el ALMACENAMIENTO.</summary>
    Public Shared Function AMatrix4(m As Matrix4d) As Matrix4
        Return New Matrix4(CSng(m.M11), CSng(m.M12), CSng(m.M13), CSng(m.M14),
                           CSng(m.M21), CSng(m.M22), CSng(m.M23), CSng(m.M24),
                           CSng(m.M31), CSng(m.M32), CSng(m.M33), CSng(m.M34),
                           CSng(m.M41), CSng(m.M42), CSng(m.M43), CSng(m.M44))
    End Function

    ''' <summary>Matrix4 -> Matrix4d. Para los consumidores que siguen haciendo su cuenta en Double
    ''' (world-cache, exportador): ensanchar en un registro no cuesta memoria, y asi lo unico que cambia es
    ''' la precision GUARDADA, no la de las cuentas.</summary>
    Public Shared Function AMatrix4d(m As Matrix4) As Matrix4d
        Return New Matrix4d(m.M11, m.M12, m.M13, m.M14,
                            m.M21, m.M22, m.M23, m.M24,
                            m.M31, m.M32, m.M33, m.M34,
                            m.M41, m.M42, m.M43, m.M44)
    End Function

    ''' <summary>ESTA SOBRECARGA NO TIENE UN SOLO CONSUMIDOR DE PRODUCCION. Existe para el ARNES, que
    ''' la usa como opinion INDEPENDIENTE —el <c>Inverted().Transposed()</c> de OpenTK, otro algoritmo—
    ''' contra la ley por cofactores de <see cref="FastSkin"/>. Es el lado (a) del check [cofactores].
    ''' <para>NO la usan el exportador de NIF ni el world-cache, aunque lo parezca: los dos ensanchan a
    ''' <c>Matrix4d</c> antes de llamar (<c>SceneNifExporter</c> y <c>Create_Normal_Matrix</c>), o sea que
    ''' resuelven a la OTRA sobrecarga. Desde que el render se mudo a FastSkin, esta no corre en ninguna
    ''' app.</para>
    ''' <para>Por eso es <c>Friend</c> y no <c>Public</c>: llega al arnes por <c>InternalsVisibleTo</c> y la
    ''' superficie de API que se distribuye no crece con una funcion que nadie de afuera usa.</para>
    ''' </summary>
    Friend Shared Function NormalMatrixOrIdentity(Origen As Matrix4) As Matrix3d
        Dim L As New OpenTK.Mathematics.Matrix3(Origen.M11, Origen.M12, Origen.M13,
                                                Origen.M21, Origen.M22, Origen.M23,
                                                Origen.M31, Origen.M32, Origen.M33)
        ' EL PREDICADO SALE DE `FastSkin.EsDegenerada`, NO DE UN LITERAL NI DE UNA COPIA. Escrito a mano acá
        ' y en la otra sobrecarga serian tres copias de la misma decision sin nada que las ate, y mover una
        ' separa en silencio la DEGENERACION del render de la del bake y el exportador — que no es un error de
        ' redondeo: es normal identidad contra normal transformada.
        ' Ademas es RELATIVO a la escala de la matriz: un corte absoluto sobre un determinante en Single
        ' no distingue "matriz chica" de "matriz singular". Ver el doc de FastSkin.EpsDetRel.
        If FastSkin.EsDegenerada(FastSkin.DetPorPrimeraFila(L.M11, L.M12, L.M13, L.M21, L.M22, L.M23, L.M31, L.M32, L.M33),
                                 L.M11, L.M12, L.M13, L.M21, L.M22, L.M23, L.M31, L.M32, L.M33) Then Return Matrix3d.Identity
        Dim inv = L.Inverted().Transposed()
        Return New Matrix3d(inv.M11, inv.M12, inv.M13,
                            inv.M21, inv.M22, inv.M23,
                            inv.M31, inv.M32, inv.M33)
    End Function

    ''' <summary>Normal matrix (inverse-transpose de la parte lineal) tolerante a singularidad.
    ''' Con un eje escalado a 0 — p.ej. Scale=0 en el editor de transforms — la 3×3 no tiene
    ''' inversa: la geometría colapsa a un plano/punto y la normal queda matemáticamente
    ''' indefinida. Devolvemos identidad en lugar de dejar que OpenTK tire
    ''' InvalidOperationException ("Matrix is singular and cannot be inverted").
    ''' <para>LA INVERSA SE EVALUA EN SINGLE, NO EN DOUBLE, Y ES A PROPOSITO. El vertex shader hace
    ''' <c>transpose(inverse(mat3(skinMatrix)))</c> en float: calcularla en Double de este lado no era mas
    ''' fiel, era OTRA cuenta, y parte de la divergencia CPU/GPU que mide el arnes salia de aca.</para>
    ''' <para>EL CAMBIO ES GLOBAL A PROPOSITO. Acotarlo al camino de render habria dejado el preview
    ''' calculando una cosa y el BAKE otra — que es exactamente la divergencia que la regla RENDER==BAKE
    ''' existe para impedir. Consumidores: la extraccion de geometria, el world-cache, el bake (via
    ''' <see cref="Create_Normal_Matrix"/>) y el exportador de NIF.</para>
    ''' <para>POR LO TANTO CAMBIA BYTES HORNEADOS Y EXPORTADOS. No es una optimizacion invisible: hay
    ''' que correr el corpus de bake y mirar la diff. El motivo es que la precision extra no se usaba —
    ''' el destino de todas estas normales es un buffer de floats o un NIF, que guarda floats.</para>
    ''' <para>ESTADO ABIERTO — el render ya NO pasa por aca. Desde que el skinning de CPU se mudo a
    ''' <see cref="FastSkin"/>, el render calcula la matriz de normales por cofactores inline y esta
    ''' funcion quedo con los otros consumidores. Las dos leyes son la misma algebra y el gate [cofactores]
    ''' mide en grados cuanto se apartan sobre geometria real y posada: 24.927 normales del actor canonico
    ''' posado, desvio medio 0,0036 grados y PEOR 0,0296 grados — ruido de redondeo de Single, no forma.
    ''' Mientras ese numero siga ahi, RENDER==BAKE se sostiene. Si algun dia deja de serlo, la salida es
    ''' que el bake llame a FastSkin, no que el render vuelva aca.</para>
    ''' <para>Y hay un defecto PRE-EXISTENTE encima: <c>Vector3d.TransformNormal</c> invierte la matriz
    ''' por dentro, asi que los call sites que le pasan el resultado de <see cref="Create_Normal_Matrix"/>
    ''' terminan aplicando la matriz CRUDA a la normal. No se toca sin decision expresa porque mueve bytes
    ''' horneados; ver la nota de memoria del proyecto.</para>
    ''' <para>El resultado se devuelve en Matrix3d para no cambiar la firma ni los call sites; lo que
    ''' viaja adentro es precision de Single. El corte por determinante es RELATIVO a la escala de la
    ''' matriz (<see cref="FastSkin.EsDegenerada"/>) y NO la constante absoluta de 1e-12 que habia antes:
    ''' esa venia de cuando el determinante se calculaba en Double, y al pasar a Single quedo por debajo
    ''' del ruido de la propia cantidad — dejaba pasar matrices EXACTAMENTE singulares. Cambiar esto SI
    ''' mueve que se considera degenerado, que es justamente el defecto que arregla.</para></summary>
    Public Shared Function NormalMatrixOrIdentity(Origen As Matrix4d) As Matrix3d
        ' Matrix3 es ambiguo: NiflySharp.Structs tambien define uno. Se califica.
        Dim L As New OpenTK.Mathematics.Matrix3(CSng(Origen.M11), CSng(Origen.M12), CSng(Origen.M13),
                             CSng(Origen.M21), CSng(Origen.M22), CSng(Origen.M23),
                             CSng(Origen.M31), CSng(Origen.M32), CSng(Origen.M33))
        If FastSkin.EsDegenerada(FastSkin.DetPorPrimeraFila(L.M11, L.M12, L.M13, L.M21, L.M22, L.M23, L.M31, L.M32, L.M33),
                                 L.M11, L.M12, L.M13, L.M21, L.M22, L.M23, L.M31, L.M32, L.M33) Then Return Matrix3d.Identity   ' ver la otra sobrecarga
        Dim inv = L.Inverted().Transposed()
        Return New Matrix3d(inv.M11, inv.M12, inv.M13,
                            inv.M21, inv.M22, inv.M23,
                            inv.M31, inv.M32, inv.M33)
    End Function

    ''' <summary>LA LEY DEL BAKE SOBRE UN VERTICE, escrita UNA sola vez.
    ''' <para>ESTA PRIMITIVA EXISTE PARA QUE LA LEY NO SE REPITA POR CALL SITE. La decision que importa no es
    ''' como se calcula la matriz de normales —eso ya lo cubre el gate [cofactores]— sino QUE MATRIZ RECIBE
    ''' CADA CANAL, y ningun gate que ejercite la primitiva la ve: con las matrices CRUZADAS en los tres
    ''' bloques del bake, ParityGate sigue dando 21/21.</para>
    ''' <para>LA LEY: la POSICION y las direcciones SOBRE la superficie (T y B) van con la matriz
    ''' total; la NORMAL va con la inversa-transpuesta. Es la misma que corre el render
    ''' (<see cref="FastSkin"/> y los dos vertex shaders), y esa igualdad ES la regla RENDER==BAKE.</para>
    ''' <para>Nunca <c>Vector3d.TransformNormal</c>: invierte la matriz por dentro. Ver
    ''' <see cref="PorMatriz3x3"/>.</para></summary>
    Friend Shared Sub BakearVertice(ByRef v As Vector3d, ByRef n As Vector3, ByRef t As Vector3, ByRef b As Vector3,
                                    total As Matrix4d, normales As Matrix4d)
        v = Vector3d.TransformPosition(v, total)
        n = RecalcTBN.ASng(RecalcTBN.NormalizaComoNifly(PorMatriz3x3(RecalcTBN.ADbl(n), normales)))
        t = RecalcTBN.ASng(RecalcTBN.NormalizaComoNifly(PorMatriz3x3(RecalcTBN.ADbl(t), total)))
        b = RecalcTBN.ASng(RecalcTBN.NormalizaComoNifly(PorMatriz3x3(RecalcTBN.ADbl(b), total)))
    End Sub

    ''' <summary>El eps RELATIVO del kernel, para que el camino vectorial del arnes pueda armar su
    ''' mascara. Un llamador que use esto tiene que transcribir el predicado COMPLETO —cuadrados
    ''' contra la cota de Hadamard—, no comparar el determinante pelado contra este numero.</summary>
    Friend Shared ReadOnly Property EpsDetRelDelKernel As Single
        Get
            Return FastSkin.EpsDetRel
        End Get
    End Property

    ''' <summary>El PREDICADO de degeneracion del kernel, expuesto para que el arnes no lo transcriba.
    ''' <para>Se exporta el PREDICADO y no el umbral: la decision depende tambien de la ESCALA de la matriz,
    ''' asi que dejar que el llamador arme la comparacion la parte en dos. Ver FastSkin.EsDegenerada.</para></summary>
    Friend Shared Function EsDegeneradaDelKernel(det As Single,
                                                 m11 As Single, m12 As Single, m13 As Single,
                                                 m21 As Single, m22 As Single, m23 As Single,
                                                 m31 As Single, m32 As Single, m33 As Single) As Boolean
        Return FastSkin.EsDegenerada(det, m11, m12, m13, m21, m22, m23, m31, m32, m33)
    End Function

    ''' <summary>Direccion x la parte 3x3 de la matriz, TAL CUAL. No invierte, no transpone, no normaliza:
    ''' aplica lo que se le da. Public y no Friend: el exportador de NIF vive en FO4_NPC_Manager, otro assembly.
    ''' <para>EXISTE PORQUE <c>Vector3d.TransformNormal</c> INVIERTE LA MATRIZ POR DENTRO. OpenTK define
    ''' <c>TransformNormal(v, M) = v · (M⁻¹)ᵀ</c>: espera la matriz ORIGINAL y hace la inversa-transpuesta el
    ''' solo. Pasarle <c>NormalsMat = (A⁻¹)ᵀ</c> —la matriz de normales YA calculada— da
    ''' <c>v · (((A⁻¹)ᵀ)⁻¹)ᵀ = v · A</c>: <b>la matriz CRUDA aplicada a la normal</b>, que es exactamente lo
    ''' que la inversa-transpuesta existe para evitar. Y al reves con la tangente:
    ''' <c>TransformNormal(T, totalSkinMat)</c> da <c>T · A⁻ᵀ</c>, la ley de la NORMAL aplicada a una
    ''' direccion de la SUPERFICIE.</para>
    ''' <para>Magnitud MEDIDA contra OpenTK 4.9.3: con rotacion pura el error es <b>0,000 grados</b> (una
    ''' rotacion es ortogonal, <c>A⁻ᵀ = A</c>) y con SHEAR —el blend de dos rotaciones distintas, o sea todo
    ''' vertice con 2+ influencias: codo, rodilla, hombro— es de <b>36,44 grados</b>. Cuesta verlo porque N, T
    ''' y B quedan sheareados IGUAL entre si: la base TBN sigue coherente y el sintoma es un sesgo suave de
    ''' iluminacion, no un artefacto duro.</para>
    ''' <para>Y EL ARREGLO NO ES INTERCAMBIAR LOS ARGUMENTOS: pasarle <c>NormalsMat</c> a la tangente daria el
    ''' resultado correcto, pero haria que OpenTK INVIERTA UNA 4x4 POR VERTICE sobre una matriz que ya es una
    ''' inversa — trabajo tirado y precision perdida. Se aplica cada matriz directamente: la normal con
    ''' <c>NormalsMat</c>, la tangente y la bitangente con la matriz total.</para>
    ''' <para>⚠️ ESTA LEY MUEVE BYTES HORNEADOS Y EXPORTADOS en toda malla con vertices de 2+
    ''' influencias.</para></summary>
    Public Shared Function PorMatriz3x3(v As Vector3d, m As Matrix4d) As Vector3d
        Return New Vector3d(v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31,
                            v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32,
                            v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33)
    End Function

    Private Shared Function Create_Normal_Matrix(Origen As Matrix4d) As Matrix4d
        Dim nm3 = NormalMatrixOrIdentity(Origen)

        ' Reinyectar nm3 en una 4×4 sin traslación
        Dim nm4 As Matrix4d = Matrix4d.Identity
        nm4.M11 = nm3.M11 : nm4.M12 = nm3.M12 : nm4.M13 = nm3.M13
        nm4.M21 = nm3.M21 : nm4.M22 = nm3.M22 : nm4.M23 = nm3.M23
        nm4.M31 = nm3.M31 : nm4.M32 = nm3.M32 : nm4.M33 = nm3.M33
        Return nm4
    End Function
    ''' <summary>
    ''' Tupla (indices de hueso, pesos) de un vertice. Es la ENTRADA completa del calculo de las
    ''' matrices de skin: dos vertices con la misma firma dan exactamente las mismas matrices.
    ''' Se comparan los bits crudos de los pesos (no hay tolerancia) justo para que un acierto
    ''' devuelva el mismo resultado que el calculo, hasta el ultimo bit.
    ''' Cubre hasta 4 influencias, que es el maximo del formato.
    ''' </summary>
    Private Structure SkinFirma
        Implements IEquatable(Of SkinFirma)
        Public i0, i1, i2, i3 As Integer
        Public w0, w1, w2, w3 As Single

        Public Shared Function Desde(idx() As Byte, wgt() As System.Half, baseIdx As Integer, wpv As Integer) As SkinFirma
            Dim f As New SkinFirma
            Dim n = Math.Min(wpv, Math.Min(idx.Length - baseIdx, wgt.Length - baseIdx))
            ' Los pesos son Half: se guardan como Single porque la conversion es exacta y hace la
            ' comparacion trivial. No hay tolerancia — se compara bit a bit.
            If n > 0 Then f.i0 = idx(baseIdx) : f.w0 = CSng(wgt(baseIdx))
            If n > 1 Then f.i1 = idx(baseIdx + 1) : f.w1 = CSng(wgt(baseIdx + 1))
            If n > 2 Then f.i2 = idx(baseIdx + 2) : f.w2 = CSng(wgt(baseIdx + 2))
            If n > 3 Then f.i3 = idx(baseIdx + 3) : f.w3 = CSng(wgt(baseIdx + 3))
            Return f
        End Function

        Public Overloads Function Equals(o As SkinFirma) As Boolean Implements IEquatable(Of SkinFirma).Equals
            Return i0 = o.i0 AndAlso i1 = o.i1 AndAlso i2 = o.i2 AndAlso i3 = o.i3 AndAlso
                   w0.Equals(o.w0) AndAlso w1.Equals(o.w1) AndAlso w2.Equals(o.w2) AndAlso w3.Equals(o.w3)
        End Function

        Public Overrides Function Equals(o As Object) As Boolean
            Return TypeOf o Is SkinFirma AndAlso Equals(DirectCast(o, SkinFirma))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return HashCode.Combine(i0, i1, i2, i3, w0, w1, w2, w3)
        End Function
    End Structure

    ''' <summary>Lo que se memoiza por firma: la matriz del vertice y su inversa-transpuesta.</summary>
    Private Structure MatricesDeVertice
        Public ReadOnly Total As Matrix4d
        Public ReadOnly Normales As Matrix4d
        Public Sub New(total_ As Matrix4d, normales_ As Matrix4d)
            Total = total_
            Normales = normales_
        End Sub
    End Structure

    ''' <summary>
    ''' Bake current pose into geometry: vertices/normals/tangents/bitangents are transformed
    ''' by the per-bone skin matrices stored in <paramref name="geom"/>. If the underlying
    ''' SkeletonInstance has no DeltaTransforms, matsBind == matsPose and the bake collapses
    ''' to identity (no-op outcome, callers paid the parallel-loop cost). Callers that want
    ''' "bake skipped when no pose" must check upstream and avoid invoking this method.
    ''' </summary>
    Public Shared Sub BakeFromMemoryUsingOriginal(Shape As IRenderableShape, ByRef geom As SkinnedGeometry, inverse As Boolean, ApplyMorph As Boolean, RemoveZaps As Boolean, singleBoneSkinning As Boolean,
                                                   Optional geometryModifier As IGeometryModifier = Nothing)
        ' 2) Matrices calculadas en ExtractSkinnedGeometry
        Dim matsBind() As Matrix4d = geom.BoneMatsBind
        Dim matsPose() As Matrix4d = geom.BoneMatsPose

        ' 3) Transformación global e inversa
        Dim GlobalTransform As Matrix4d = geom.ParentGlobalTransform
        Dim InverseGlobal As Matrix4d = GlobalTransform
        InverseGlobal.Invert()

        ' 4) Vértices resultantes de ExtractSkinnedGeometry (now local-space with GPU skinning)
        Dim worldV() As Vector3d

        ' 4b) Apply geometry modifier (e.g. zap removal) if provided
        If RemoveZaps AndAlso geometryModifier IsNot Nothing Then geometryModifier.Apply(Shape, geom)

        If ApplyMorph Then
            worldV = geom.Vertices.ToArray
        Else
            worldV = geom.BaseVertices.ToArray
        End If

        ' Son LA MISMA referencia que geom.Normals/Tangents/Bitangents y se transforman IN PLACE.
        ' El transform sigue en Double (ADbl -> PorMatriz3x3 -> Normalize) y solo redondea al
        ' guardar; la entrada ya venia redondeada de RecalcTBN, asi que el unico cambio respecto de
        ' antes es que el valor intermedio no se arrastra en Double entre los dos pasos.
        Dim worldN() As Vector3 = geom.Normals
        Dim worldT() As Vector3 = geom.Tangents
        Dim worldB() As Vector3 = geom.Bitangents

        ' 5) Datos de skinning por vértice — polimórficos via ShapeSkinningData
        '    (BSTriShape inline o NiSkinPartition expandido).
        Dim skinFlatIdx = geom.Skinning.BoneIndices
        Dim skinFlatWgt = geom.Skinning.BoneWeights
        Dim skinWpv = If(geom.Skinning.WeightsPerVertex > 0, geom.Skinning.WeightsPerVertex, 4)
        Dim hasSkin = (skinFlatIdx IsNot Nothing AndAlso skinFlatWgt IsNot Nothing AndAlso geom.Skinning.VertexCount = worldV.Length)
        ' Paletas planas para el blend vectorial: UNA vez por shape (20-60 matrices), no por vértice.
        ' Acá son DOS porque el blend de este camino mezcla pose y bind a la vez.
        Dim flatPalPose = FastGeom.BuildFlatPaletteS(matsPose)
        Dim flatPalBind = FastGeom.BuildFlatPaletteS(matsBind)

        'A - REVIERTE Skinning y Bakea
        ' Per-vertex linear blend (arithmetic mean) of matsBind y matsPose — coincide
        ' EXACTAMENTE con la fórmula del shader (Σw·bone[k]). La versión anterior calculaba
        ' Mskin = Σw·(matsBind·invMatsPose) e invertía: ése es la "media armónica" de
        ' matrices y NO equivale a Σw·matsPose para vértices con peso repartido entre
        ' huesos. Como resultado, render-with-bind(v_baked) ≠ render-with-pose(v_orig)
        ' cuando wpv>1 (típico en bodies). Round-trip seguía siendo identidad porque la
        ' fórmula es invertible consigo misma, pero no preservaba la pose visualmente.

        ' Branching predicate: Shape.IsSkinned (paridad OS bake — OutfitProject.cpp:1620).
        ' matsBind.Length>0 sigue como guard secundario para acceso a arrays de matrices.
        Select Case True
            Case Shape.IsSkinned AndAlso Not singleBoneSkinning AndAlso matsBind.Length > 0
                ' MEMOIZACION POR FIRMA DE SKIN. Las dos matrices que se derivan del vertice
                ' (totalSkinMat y su inversa-transpuesta) dependen SOLO de la tupla
                ' (indices de hueso, pesos). Miles de vertices comparten esa tupla — todo lo rigido
                ' pegado a un solo hueso con peso 1, que en un cuerpo es la mayor parte — y para cada
                ' uno se pagaba un Matrix4d.Invert MAS un Create_Normal_Matrix (otra inversa 3x3 y una
                ' transpuesta). La clave es EXACTA (los mismos bits de indices y pesos), asi que un
                ' acierto devuelve exactamente las matrices que se habrian calculado: byte-identico.
                Dim memo As New Concurrent.ConcurrentDictionary(Of SkinFirma, MatricesDeVertice)()
                Parallel.ForEach(RangosDe(worldV.Length),
                    Sub(rango As Tuple(Of Integer, Integer))
                        For i = rango.Item1 To rango.Item2 - 1
                            Dim MposeBlend As Matrix4d = Matrix4d.Zero
                            Dim MbindBlend As Matrix4d = Matrix4d.Zero
                            Dim sumW As Double = 0

                            ' Camino rapido: si esta tupla (indices, pesos) ya se
                            ' resolvio, se reusan sus matrices tal cual.
                            Dim firma As SkinFirma = Nothing
                            Dim memoOk As Boolean = False
                            If hasSkin Then
                                firma = SkinFirma.Desde(skinFlatIdx, skinFlatWgt, i * skinWpv, skinWpv)
                                Dim listo As MatricesDeVertice = Nothing
                                If memo.TryGetValue(firma, listo) Then
                                    BakearVertice(worldV(i), worldN(i), worldT(i), worldB(i), listo.Total, listo.Normales)
                                    ' `Continue For`, NUNCA `Return`. Esto vive dentro de un
                                    ' Sub(rango) de Parallel.ForEach sobre Partitioner.Create(0, n):
                                    ' un `Return` sale del SUB y abandona el RANGO ENTERO: se bakean ~50
                                    ' de 20.000 vertices, y cuantos depende del scheduler ⇒ el build deja
                                    ' de ser reproducible. El gate que lo detecta es el A/A —construir dos
                                    ' veces y comparar los bytes—: con `Return`, 61 de 821 archivos
                                    ' distintos entre dos corridas; sin el, 0 en 6 pares.
                                    Continue For
                                End If
                                memoOk = True
                            End If

                            If hasSkin Then
                                ' LAS DOS MEZCLAS VAN POR `BlendBoneMatrices`, NUNCA escritas a mano acá: la
                                ' forma DECLARADA en el encabezado de este archivo es
                                ' `skinMatrix = Σ(bones·weight) / sumW`. Escribirla como
                                ' `Σ(bones·(weight/sumW))` —dividir POR SLOT antes de multiplicar en vez de
                                ' acumular crudo y escalar UNA vez al final— es lo mismo en aritmética exacta y
                                ' NO en punto flotante: N divisiones y N redondeos de producto contra un solo
                                ' recíproco y un solo escalado. Con `sumW = 1,0` exacto (malla normalizada, el
                                ' caso normal) las dos coinciden bit a bit, así que la divergencia sólo asoma
                                ' donde Σw ≠ 1: es de las que pasan un gate y se ven en el bake.
                                '
                                ' Dos llamadas, una por paleta. `TryComputeWeights` se evalúa dos veces: es puro
                                ' (lee pesos, escribe el buffer de salida) y sus 3 contadores Interlocked no los
                                ' consume nadie, así que repetirlo no cambia ningún resultado.
                                ' `matsPose` y `matsBind` se dimensionan los dos con `bones.Count - 1`, así que el
                                ' bound del índice es el mismo para las dos.
                                Dim baseIdx = i * skinWpv
                                MposeBlend = BlendBoneMatrices(skinFlatWgt, skinFlatIdx, baseIdx, skinWpv, matsPose, flatPalPose)
                                MbindBlend = BlendBoneMatrices(skinFlatWgt, skinFlatIdx, baseIdx, skinWpv, matsBind, flatPalBind)
                            Else
                                MposeBlend = matsPose(0)
                                MbindBlend = matsBind(0)
                            End If

                            ' v_baked tal que v_baked·MbindBlend = v_orig·MposeBlend
                            '   ⇒ v_baked = v_orig · MposeBlend · inv(MbindBlend)
                            ' Inverse=True invierte la dirección (unbake).
                            Dim skinMat As Matrix4d
                            If Not inverse Then
                                skinMat = MposeBlend * Matrix4d.Invert(MbindBlend)
                            Else
                                skinMat = MbindBlend * Matrix4d.Invert(MposeBlend)
                            End If
                            Dim totalSkinMat As Matrix4d = InverseGlobal * skinMat * GlobalTransform
                            Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)
                            If memoOk Then
                                memo.TryAdd(firma, New MatricesDeVertice(totalSkinMat, NormalsMat))
                            End If

                            ' Bake (local -> new-local)
                            BakearVertice(worldV(i), worldN(i), worldT(i), worldB(i), totalSkinMat, NormalsMat)
                        Next
                    End Sub)

            Case Shape.IsSkinned AndAlso singleBoneSkinning AndAlso matsBind.Length > 0
                ' Single-bone — vertices are already in local space, transform per-vertex.
                ' Por diseño matsPose(0)=matsBind(0) en single-bone (no aplica pose), así que
                ' skinMat colapsa a identidad. Mantenemos la fórmula explícita para que la
                ' lógica sea legible (no es un caso optimizado, sólo correcto).
                Dim skinMat As Matrix4d
                If Not inverse Then
                    skinMat = matsPose(0) * Matrix4d.Invert(matsBind(0))
                Else
                    skinMat = matsBind(0) * Matrix4d.Invert(matsPose(0))
                End If
                Dim totalSkinMat As Matrix4d = InverseGlobal * skinMat * GlobalTransform
                Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                Parallel.ForEach(RangosDe(worldV.Length),
                    Sub(rango As Tuple(Of Integer, Integer))
                        For i = rango.Item1 To rango.Item2 - 1
                            ' Bake (local -> new-local)
                            BakearVertice(worldV(i), worldN(i), worldT(i), worldB(i), totalSkinMat, NormalsMat)
                        Next
                    End Sub)

            Case Else
                ' Cubre dos casos: (A) IsSkinned=False (genuinamente unskinned) — la geometría ya
                ' está en shape-local; el transform del shape va guardado en NiAVObject.T/R/S, no
                ' se mete en los vértices (paridad OS OutfitProject.cpp:1645-1648 unskinned bake).
                ' (B) IsSkinned=True con matsBind.Length=0 (degenerado): no hay skin que invertir;
                ' identity preserva los vértices tal cual.
                Dim totalSkinMat As Matrix4d = Matrix4d.Identity
                Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                Parallel.ForEach(RangosDe(worldV.Length),
                    Sub(rango As Tuple(Of Integer, Integer))
                        For i = rango.Item1 To rango.Item2 - 1
                            BakearVertice(worldV(i), worldN(i), worldT(i), worldB(i), totalSkinMat, NormalsMat)
                        Next
                    End Sub)

        End Select

        If ApplyMorph Then
            geom.Vertices = worldV
            geom.BaseVertices = CType(worldV.Clone(), Vector3d())
        Else
            geom.Vertices = worldV
        End If

        InjectToTrishape(geom)

    End Sub
    ''' <summary>
    ''' Writes the SkinnedGeometry contents back to the underlying NIF shape.  Fully
    ''' polymorphic via geom.Geometry — works identically for BSTriShape family and
    ''' NiTriShape family.
    '''
    ''' Flow: ResizeVertices(newCount) establishes the target size on the underlying block
    ''' (BSTriShape replaces its packed BSVertexData/SSE list, NiTriShape resizes
    ''' NiTriShapeData.Vertices).  Then each per-field setter writes into the already-sized
    ''' buffer.  SetSkinning rebuilds per-vertex bone data (BSTriShape inline,
    ''' NiTriShape NiSkinData.BoneList).
    '''
    ''' Skin partition regeneration is NOT performed here — caller must call
    ''' Nifcontent_Class_Manolo.UpdateSkinPartitions(geom.Geometry.BackingShape) before
    ''' saving (BuildingForm, MorphingHelper.RemoveZaps callers and SplitShapeHelper already
    ''' follow that contract).  See UpdateSkinPartitions docstring for the order contract.
    ''' </summary>
    Public Shared Sub InjectToTrishape(ByRef geom As SkinnedGeometry)
        Dim nNew As Integer = geom.Vertices.Length
        Dim shapeGeom = geom.Geometry
        If shapeGeom Is Nothing Then Exit Sub

        Dim posN As New List(Of System.Numerics.Vector3)(nNew)
        Dim uvN As New List(Of TexCoord)(nNew)
        Dim colN As New List(Of NiflySharp.Structs.Color4)(nNew)

        For i As Integer = 0 To nNew - 1
            Dim v1 = geom.Vertices(i) : posN.Add(New System.Numerics.Vector3(CSng(v1.X), CSng(v1.Y), CSng(v1.Z)))
            Dim uv = geom.Uvs_Weight(i) : uvN.Add(New TexCoord(CSng(uv.X), CSng(uv.Y)))
            Dim c = geom.VertexColors(i) : colN.Add(New NiflySharp.Structs.Color4(CSng(c.X), CSng(c.Y), CSng(c.Z), CSng(c.W)))
        Next

        Dim idxArr = geom.Indices
        Dim tmpTris As New List(Of Triangle)(idxArr.Length \ 3)
        For tr As Integer = 0 To idxArr.Length - 3 Step 3
            tmpTris.Add(New Triangle(CInt(idxArr(tr)), CInt(idxArr(tr + 1)), CInt(idxArr(tr + 2))))
        Next

        ' Establish new vertex count on the block (no-op if unchanged).  For BSTriShape
        ' this allocates a fresh zero-init packed list of the new size; for NiTriShape it
        ' resizes NiTriShapeData.Vertices.  Subsequent Set* calls write into the sized
        ' storage.
        shapeGeom.ResizeVertices(nNew)

        ' Per-field writes.  Order matters slightly: positions before normals/tangents so
        ' adapter-internal TBN recalc (if any) has correct positions; skinning + triangles
        ' last since they reference the established vertex/triangle space.
        shapeGeom.SetVertexPositions(posN)
        If shapeGeom.HasNormals OrElse shapeGeom.HasTangents Then
            InjectNormalsToTrishape(geom)
        End If
        If shapeGeom.HasUVs Then shapeGeom.SetUVs(uvN)
        If shapeGeom.HasVertexColors Then shapeGeom.SetVertexColors(colN)
        If shapeGeom.HasEyeData Then shapeGeom.SetEyeData(geom.Eyedata.ToList())

        ' Polymorphic skin write-back.  For BSTriShape writes BoneIndices/BoneWeights inline
        ' into BSVertexData.  For NiTriShape rebuilds NiSkinData.BoneList[].VertexWeights
        ' from the per-vertex Skinning data.  Critical for the NiTriShape family:
        ' UpdateSkinPartitions later reads from NiSkinData to regenerate the partition.
        If geom.Skinning.VertexCount = nNew Then
            shapeGeom.SetSkinning(geom.Skinning)
        End If

        ' Provenance-aware triangle write — redistributes BSMeshLOD/BSLOD LOD sizes and
        ' BSSubIndex/BSSegmented Segments via the adapter when geom.TriangleProvenance is
        ' present (zap/split populate it).  Must come AFTER SetSkinning because the
        ' segment redistribution reads the post-write triangle list.
        shapeGeom.SetTriangles(tmpTris, geom.TriangleProvenance)

        ' Edge case: empty shape (all vertices zapped).  BSTriShape exposes writable flag
        ' properties for this; NiTriShape handles empty state via the Has* setters on
        ' NiGeometryData (automatically triggered when lists are empty).  Only BSTriShape
        ' needs the explicit flag flip here.
        If nNew = 0 Then
            Dim bsTri = TryCast(shapeGeom.BackingShape, BSTriShape)
            If bsTri IsNot Nothing Then
                bsTri.HasVertices = False
                bsTri.HasNormals = False
                bsTri.HasTangents = False
                bsTri.HasVertexColors = False
                bsTri.HasEyeData = False
                bsTri.HasUVs = False
            End If
        End If
    End Sub

    ''' <summary>
    ''' Injects only normals, tangents and bitangents from geo into the underlying shape via
    ''' the polymorphic adapter.  The INVERTIDAS swap (renderer Tangent/Bitangent ⇆ NIF
    ''' Bitangent/Tangent) is encapsulated in IShapeGeometry.SetTangents/SetBitangents — this
    ''' method passes geom.Tangents and geom.Bitangents straight through.
    ''' </summary>
    ''' <param name="opts">Sin especificar, toma las opciones vivas del config — que es la MISMA
    ''' fuente que usa el recálculo. Se puede pasar explícitamente desde un test o un camino headless.</param>
    Public Shared Sub InjectNormalsToTrishape(ByRef geom As SkinnedGeometry, Optional opts As TBNOptions? = Nothing)
        Dim shapeGeom = geom.Geometry
        If shapeGeom Is Nothing Then Exit Sub
        Dim nNew = geom.Vertices.Length
        If nNew = 0 Then Exit Sub

        Dim norN As New List(Of System.Numerics.Vector3)(nNew)
        Dim tanN As New List(Of System.Numerics.Vector3)(nNew)
        Dim bitN As New List(Of System.Numerics.Vector3)(nNew)

        ' RED FINAL, y son DOS reparaciones con reglas distintas — mezclarlas era lo que dejaba la
        ' mejora de WM hardcodeada por encima de su propia opcion.
        '
        '   NaN  — se repara SIEMPRE, sin opcion. Un NaN no es un valor del canonico: `Normalize()` de
        '          nifly divide por 1 cuando la longitud es cero (Object3d.hpp), asi que nunca emite
        '          NaN. Si llega uno hasta aca es un defecto NUESTRO, y escribirlo al NIF deja un
        '          archivo que el motor dibuja como mancha negra y que no se puede diagnosticar
        '          despues. MEDIDO: sin esta red quedaban vertices con NaN en la bitangente de
        '          `BaseUndies` y del CBBE de FO4 (la media salia NaN con p50 en 0,07).
        '
        '   NULO — lo gobierna `RepairNaNs`. El canonico SI escribe marcos nulos: en el CBBE de FO4 hay
        '          14 vertices degenerados (el cluster de z=-119,25, espejado) donde BodySlide deja
        '          normal, tangente y bitangente en CERO. Con la opcion en True —el default— WM escribe
        '          una base valida en su lugar, que es la unica diferencia que queda contra BodySlide
        '          en ese archivo; con la opcion en False sale el cero del canonico, byte por byte.
        Dim reparaNulos As Boolean
        If opts.HasValue Then
            reparaNulos = opts.Value.RepairNaNs
        Else
            reparaNulos = Config_App.Current.Setting_TBN.RepairNaNs
        End If

        ' La red se evalua en Double sobre el valor YA almacenado en Single: es el mismo vector, y los
        ' guards son contra NaN y contra el vector nulo, que sobreviven exactos al redondeo.
        For i = 0 To nNew - 1
            Dim n1 = RecalcTBN.ADbl(geom.Normals(i))
            Dim t1 = RecalcTBN.ADbl(geom.Tangents(i))
            Dim b1 = RecalcTBN.ADbl(geom.Bitangents(i))
            ' Se anota si la red TOCO algo: sustituir un solo eje deja los otros dos apuntando
            ' donde estaban, o sea una base torcida en el NIF. Cuando toca, se reortonormaliza; cuando
            ' no toca, no se altera ni un bit de lo que calculo el recalculo.
            Dim reparado As Boolean = False
            If RecalcTBN.HasNaN(n1) Then n1 = New Vector3d(0, 0, 1) : reparado = True
            If RecalcTBN.HasNaN(b1) Then b1 = New Vector3d(n1.Y, n1.Z, n1.X) : reparado = True
            If RecalcTBN.HasNaN(t1) Then t1 = Vector3d.Cross(n1, b1) : reparado = True
            If RecalcTBN.HasNaN(t1) Then t1 = New Vector3d(1, 0, 0) : reparado = True
            If reparaNulos Then
                If n1.LengthSquared <= 0.0 Then n1 = New Vector3d(0, 0, 1) : reparado = True
                If b1.LengthSquared <= 0.0 Then b1 = New Vector3d(n1.Y, n1.Z, n1.X) : reparado = True
                If t1.LengthSquared <= 0.0 Then t1 = Vector3d.Cross(n1, b1) : reparado = True
                If t1.LengthSquared <= 0.0 Then t1 = New Vector3d(1, 0, 0) : reparado = True
            End If
            If reparado Then
                Dim nn As Double = n1.LengthSquared
                If nn > 0.0 Then
                    b1 -= n1 * (Vector3d.Dot(n1, b1) / nn)
                    If b1.LengthSquared > 0.0 Then b1 = Vector3d.Normalize(b1)
                    t1 -= n1 * (Vector3d.Dot(n1, t1) / nn)
                End If
                Dim bb As Double = b1.LengthSquared
                If bb > 0.0 Then t1 -= b1 * (Vector3d.Dot(b1, t1) / bb)
                If t1.LengthSquared > 0.0 Then t1 = Vector3d.Normalize(t1)
            End If
            norN.Add(New System.Numerics.Vector3(CSng(n1.X), CSng(n1.Y), CSng(n1.Z)))
            tanN.Add(New System.Numerics.Vector3(CSng(t1.X), CSng(t1.Y), CSng(t1.Z)))
            bitN.Add(New System.Numerics.Vector3(CSng(b1.X), CSng(b1.Y), CSng(b1.Z)))
        Next
        shapeGeom.SetNormals(norN)
        shapeGeom.SetTangents(tanN)
        shapeGeom.SetBitangents(bitN)
    End Sub

    ''' <summary>
    ''' Snapshots the per-vertex separate arrays from a shape via the polymorphic adapter.
    ''' UVs are converted from TexCoord to Vector3(U,V,0) — that packing is what
    ''' ApplyShapeGeometry / WM merge/split helpers expect when concatenating arrays from
    ''' multiple shapes before re-injecting.  The adapter takes care of the INVERTIDAS swap
    ''' for BSTriShape, so the snapshot is in renderer convention regardless of family.
    ''' </summary>
    Public Shared Function SnapshotSeparateArrays(shape As IShapeGeometry) As ShapeArrays
        If shape Is Nothing Then Return New ShapeArrays()
        Dim snap As New ShapeArrays With {
            .Positions = shape.GetVertexPositions()
        }
        If shape.HasNormals Then snap.Normals = shape.GetNormals()
        If shape.HasTangents Then
            snap.Tangents = shape.GetTangents()
            snap.Bitangents = shape.GetBitangents()
        End If
        If shape.HasUVs Then snap.UVs = shape.GetUVs().Select(
            Function(u) New System.Numerics.Vector3(u.U, u.V, 0)).ToList()
        If shape.HasVertexColors Then snap.VertexColors = shape.GetVertexColors()
        If shape.HasEyeData Then snap.EyeData = shape.GetEyeData()
        ' Capture per-vertex skin uniformly for all families.  After the unified
        ' InjectToTrishape / ApplyShapeGeometry refactor there's no packed-buffer fast
        ' path for BSTriShape — skin always travels via ShapeArrays.Skinning and the
        ' adapter's SetSkinning writes it back (inline BSVertexData for BS, NiSkinData
        ' rebuild for NiTri).
        If shape.IsSkinned Then snap.Skinning = shape.GetSkinning()
        Return snap
    End Function

    ''' <summary>
    ''' Applies separate per-vertex arrays + triangles + optional skinning to the underlying
    ''' shape via the polymorphic adapter.  Single authoritative point for updating shape
    ''' geometry when vertex count changes.  Fully polymorphic — no BSTriShape-specific
    ''' packed buffer parameters; the adapter internally handles BSTriShape packed resize
    ''' via ResizeVertices and the per-field setters write into the established storage.
    '''
    ''' Skin partition update remains caller's responsibility (same as InjectToTrishape).
    ''' </summary>
    Public Shared Sub ApplyShapeGeometry(
            shape As IShapeGeometry,
            triangles As List(Of Triangle),
            arrays As ShapeArrays,
            Optional provenance As TriangleRemap = Nothing)
        If shape Is Nothing Then Return

        ' Establish new vertex count on the backing block before any per-field write.
        ' Uses positions count as the new vertex count (canonical per the BSTriShape /
        ' NiTriShape setters — SetVertexPositions silently no-ops if count doesn't match).
        Dim newVc As Integer = If(arrays IsNot Nothing AndAlso arrays.Positions IsNot Nothing,
                                   arrays.Positions.Count, shape.VertexCount)
        shape.ResizeVertices(newVc)

        If arrays IsNot Nothing Then
            If arrays.Positions IsNot Nothing Then shape.SetVertexPositions(arrays.Positions)
            If arrays.Normals IsNot Nothing AndAlso shape.HasNormals Then shape.SetNormals(arrays.Normals)
            If arrays.Tangents IsNot Nothing AndAlso shape.HasTangents Then shape.SetTangents(arrays.Tangents)
            If arrays.Bitangents IsNot Nothing AndAlso shape.HasTangents Then shape.SetBitangents(arrays.Bitangents)
            If arrays.UVs IsNot Nothing AndAlso shape.HasUVs Then shape.SetUVs(arrays.UVs.Select(Function(v) New TexCoord(v.X, v.Y)).ToList())
            If arrays.VertexColors IsNot Nothing AndAlso shape.HasVertexColors Then shape.SetVertexColors(arrays.VertexColors)
            If arrays.EyeData IsNot Nothing AndAlso shape.HasEyeData Then shape.SetEyeData(arrays.EyeData)

            ' Polymorphic skin write-back when caller populated it.  For BSTriShape this
            ' writes BoneIndices/BoneWeights into the packed buffer that ResizeVertices
            ' established; for NiTriShape it rebuilds NiSkinData.BoneList[].VertexWeights.
            If arrays.Skinning.HasValue Then shape.SetSkinning(arrays.Skinning.Value)
        End If

        ' Provenance-aware triangle write: redistribute Segments / LOD sizes when caller
        ' supplied a per-new-triangle source map (split / merge populate this).  Without
        ' provenance the adapter leaves metadata stale.
        shape.SetTriangles(triangles, provenance)
    End Sub

    ' =========================================================================
    ' World-space cache functions (GPU skinning: vertices are local-space,
    ' world-space is computed lazily on demand)
    ' =========================================================================

    ''' <summary>
    ''' Lazily computes and caches world-space vertex positions from local-space + PerVertexSkinMatrix.
    ''' </summary>
    Public Shared Function GetWorldVertices(ByRef geo As SkinnedGeometry) As Vector3d()
        If geo.WorldCacheValid AndAlso geo.CachedWorldVertices IsNot Nothing Then Return geo.CachedWorldVertices
        ComputeWorldSpaceCache(geo)
        Return geo.CachedWorldVertices
    End Function

    Public Shared Function GetWorldNormals(ByRef geo As SkinnedGeometry) As Vector3d()
        If geo.WorldCacheValid AndAlso geo.CachedWorldNormals IsNot Nothing Then Return geo.CachedWorldNormals
        ComputeWorldSpaceCache(geo)
        Return geo.CachedWorldNormals
    End Function

    ''' <summary>Recompone PerVertexSkinMatrix (lazy) si quedó inválida porque
    ''' RecomputeGPUBoneMatrices la salteó en GPU-mode durante animación (Option B).
    ''' El caso single-bone/unskinned siempre llena PerVertexSkinMatrix (Array.Fill, barato) → nunca
    ''' llega acá. Solo lo dispara un lector del world-cache (bounds/picking/export) en el hilo UI tras
    ''' un play GPU; el occlusion en background nunca corre sobre un control que está reproduciendo
    ''' animación.
    '''
    ''' <para>ACÁ HABÍA UNA DIVERGENCIA REAL CON EL CAMINO EAGER. La fórmula de la paleta —y el
    ''' por qué de la divergencia— están en <see cref="BuildPosePalette"/>, que ahora es el único
    ''' lugar donde se escribe. Este camino y los dos eager la llaman, así que no pueden volver a
    ''' separarse. El <c>globalTransform</c> sale de <c>geo.ParentGlobalTransform</c>, que ahora
    ''' escriben LOS DOS caminos eager.</para>
    ''' <para>La OTRA mitad —saltear la pasada 2 en GPU-skin opaco en play— NO es una discrepancia y
    ''' se deja como está: en CPU-skin nunca se saltea, y en GPU-skin el salto marca
    ''' <c>PerVertexMatrixValid=False</c> de modo que cualquier lector pasa por acá antes de leer. Es
    ''' una recomposición perezosa correcta, siempre que las DOS fórmulas coincidan — que es
    ''' exactamente lo que faltaba.</para>
    ''' </summary>
    Private Shared Sub EnsurePerVertexSkinMatrix(ByRef geo As SkinnedGeometry)
        If geo.PerVertexMatrixValid Then Return
        Dim mats = geo.PerVertexSkinMatrix
        If mats Is Nothing Then Return
        Dim poseMats = geo.BoneMatsPose
        If poseMats Is Nothing Then Return
        ' MISMA paleta y MISMO cuerpo de relleno que el eager: BuildPosePalette +
        ' FillPerVertexSkinMatrix.
        ' `ParentGlobalTransform` lo escribe SOLO ExtractSkinnedGeometry, y con el mismo valor que
        ' usa RecomputeGPUBoneMatrices (los dos son Matrix4d.Identity hardcodeado), asi que leerlo
        ' acá da el transform que realmente uso el eager. NO agregar una escritura en Recompute
        ' "por las dudas": hoy seria un no-op demostrable, y codigo especulativo en un camino que
        ' corre por frame es ruido. Si algun dia esos dos valores pudieran diferir, lo que hace falta
        ' es un TEST que lo detecte, no una escritura preventiva.
        FillPerVertexSkinMatrix(mats, geo.Skinning, BuildPosePalette(poseMats, geo.ParentGlobalTransform))
        geo.PerVertexMatrixValid = True
    End Sub

    ''' <summary>SOLO PARA MEDIR: el mismo cuerpo que FillPerVertexSkinMatrix pero recibiendo los
    ''' atributos sueltos, para que el arnes pueda cronometrarlo sin montar una ShapeSkinningData.</summary>
    Friend Shared Sub FillParaMedir(mats As SkinMatricesSoA, idx As Byte(), wgt As System.Half(),
                                    wpv As Integer, palette() As Matrix4d)
        Dim sk As New ShapeSkinningData With {.BoneIndices = idx, .BoneWeights = wgt,
                                              .WeightsPerVertex = wpv, .VertexCount = mats.Length}
        FillPerVertexSkinMatrix(mats, sk, palette)
    End Sub

    ''' <summary>
    ''' Llena <paramref name="mats"/> con la matriz de skin de cada vértice, mezclando
    ''' <paramref name="palette"/> con los pesos de <paramref name="skinning"/>.
    '''
    ''' <para>ÚNICO CUERPO, y tiene que seguir siéndolo: lo llaman la pasada 2 de
    ''' <see cref="RecomputeGPUBoneMatrices"/> (eager) y <see cref="EnsurePerVertexSkinMatrix"/> (perezoso).
    ''' Dos cuerpos divergen en la rama que NO se mira: con <c>hasSkin = False</c> hay que llenar cada vértice
    ''' con <c>palette(0)</c>: no tocar nada y marcar <c>PerVertexMatrixValid = True</c> igual deja las
    ''' matrices del extract anterior —de otra pose— dadas por buenas, y tras un play en GPU sobre una shape
    ''' sin datos de skin per-vértice el world-cache, los bounds, el picking y el export leen matrices
    ''' rancias.</para>
    ''' </summary>
    Friend Shared Sub FillPerVertexSkinMatrix(mats As SkinMatricesSoA, skinning As ShapeSkinningData, palette() As Matrix4d)
        If mats Is Nothing OrElse palette Is Nothing Then Return
        Dim vc = mats.Length
        If vc = 0 Then Return
        Dim flatIdx = skinning.BoneIndices
        Dim flatWgt = skinning.BoneWeights
        Dim wpv = If(skinning.WeightsPerVertex > 0, skinning.WeightsPerVertex, 4)
        Dim hasSkin = (flatIdx IsNot Nothing AndAlso flatWgt IsNot Nothing AndAlso skinning.VertexCount = vc)
        Dim sinSkin = If(palette.Length > 0, palette(0), Matrix4d.Identity)
        ' Paleta plana para el blend vectorial: una vez por shape, no por vértice. Ver FastGeom.
        Dim flatPalette = FastGeom.BuildFlatPaletteS(palette)
        ' SE RECORRE POR RANGOS, NO POR INDICE: `Parallel.For(0, vc, body)` con `body` como
        ' `Action(Of Integer)` es una INVOCACION DE DELEGATE POR VERTICE —130.500 llamadas indirectas por
        ' frame sobre el Serena Battle Suit— y cada una impide el inline de todo lo de adentro. Mismo patron
        ' que `UpdateSkinBuffers_GL`: Partitioner + For interno, byte-identico.
        ' Y el resultado se escribe DIRECTO a las 12 secciones con `EstablecerDesde`, sin pasar por
        ' Matrix4d → AMatrix4 → Matrix4 → indexador: ese camino son dos copias de struct (128 B y 64 B) por
        ' vertice para terminar guardando los mismos 12 Single.
        Dim cuerpo As Action(Of Tuple(Of Integer, Integer)) =
            Sub(rango)
                If hasSkin Then
                    ' CAMINO FUSIONADO. `BlendEnScratch` deja los 16 Single ya blendeados en el scratch
                    ' y de ahi se copian los 12 utiles a las secciones. Construir una `Matrix4d` de 128 B con
                    ' `LoadMatrixS` para que `EstablecerDesde` la desarme con 12 `CSng` es un ida y vuelta por
                    ' vertice sobre datos que ya estan en el formato final.
                    ' MEDIDO (blend-bench, Serena Battle Suit, 130.180 vertices): el blend completo costaba
                    ' 8,95 ms de los cuales el trabajo REAL —blend 1,63 + escritura 1,26— era 2,89; los otros
                    ' 6,06 ms (68 %) eran envoltorio de esta clase.
                    ' El scratch se toma UNA vez por RANGO. `GetBlendScratch` es un acceso ThreadStatic
                    ' con una guarda de tamano, y hacerlo por vertice son 130.500 lookups por frame para
                    ' devolver siempre el mismo objeto: el rango entero corre en un solo hilo.
                    Dim scR = GetBlendScratch(Math.Max(wpv, EngineSkinWeightNormalization.Slots))
                    ' SIN MEMO POR VERTICE PREVIO: MEDIDO y NEUTRO, con tendencia a costar. Reusar el blend
                    ' cuando la tupla (indices, pesos) repite la del vertice anterior toca al 14,0 % de los
                    ' vertices del Serena Battle Suit (18.205 de 130.180), pero las 8 comparaciones se pagan
                    ' en el 100 %: 5 corridas del bench dan -0,08 · +0,21 · +0,07 · +0,03 · -0,20 ⇒ +0,03 ms
                    ' de promedio. Ademas obliga a apagar el memo en las ramas de excepcion, que es una fuente
                    ' de error silencioso. Un memo por FIRMA (hash) es peor todavia: ahorraria 33 % pero paga
                    ' hash + lookup en el 100 %, ~3,9 ms contra 1,75 de ahorro.
                    For i = rango.Item1 To rango.Item2 - 1
                        Dim b = i * wpv
                        Dim sc = BlendEnScratch(flatWgt, flatIdx, b, wpv, palette, flatPalette, scR)
                        If sc Is Nothing Then
                            mats.EstablecerDesde(i, BlendBoneMatrices(flatWgt, flatIdx, b, wpv, palette, flatPalette))
                        Else
                            mats.CopiarDeAcumulador(i, sc.Acc)
                        End If
                    Next
                Else
                    For i = rango.Item1 To rango.Item2 - 1
                        mats.EstablecerDesde(i, sinSkin)
                    Next
                End If
            End Sub
        If vc >= 500 Then
            Parallel.ForEach(RangosDe(vc), cuerpo)
        Else
            cuerpo(Tuple.Create(0, vc))
        End If
    End Sub

    Public Shared Sub ComputeWorldSpaceCache(ByRef geo As SkinnedGeometry)
        EnsurePerVertexSkinMatrix(geo)   ' Option B: recompone PerVertexSkinMatrix si pass-2 se salteó
        Dim count = geo.Vertices.Length
        ' Capture arrays as locals — VB.NET cannot capture ByRef params in lambdas
        Dim localVerts = geo.Vertices
        Dim localNorms = geo.Normals
        Dim localMats = geo.PerVertexSkinMatrix
        Dim wv(count - 1) As Vector3d
        Dim wn(count - 1) As Vector3d
        Parallel.ForEach(RangosDe(count),
            Sub(rango As Tuple(Of Integer, Integer))
                ' Idem que en ComputeWorldBoundsSinNormales: las secciones se leen directo, sin reconstruir
                ' la Matrix4 y ensancharla a Matrix4d por vertice. `md` se arma UNA vez por vertice porque
                ' Create_Normal_Matrix la necesita entera; lo que se evita es la pasada del indexador.
                Dim t0 = localMats.Secciones(0), t3 = localMats.Secciones(3), t6 = localMats.Secciones(6), t9 = localMats.Secciones(9)
                Dim t1 = localMats.Secciones(1), t4 = localMats.Secciones(4), t7 = localMats.Secciones(7), t10 = localMats.Secciones(10)
                Dim t2 = localMats.Secciones(2), t5 = localMats.Secciones(5), t8 = localMats.Secciones(8), t11 = localMats.Secciones(11)
                For i = rango.Item1 To rango.Item2 - 1
                    Dim md As New Matrix4d(t0(i), t1(i), t2(i), 0.0,
                                           t3(i), t4(i), t5(i), 0.0,
                                           t6(i), t7(i), t8(i), 0.0,
                                           t9(i), t10(i), t11(i), 1.0)
                    Dim lv2 = localVerts(i)
                    wv(i) = New Vector3d(lv2.X * t0(i) + lv2.Y * t3(i) + lv2.Z * t6(i) + t9(i),
                                         lv2.X * t1(i) + lv2.Y * t4(i) + lv2.Z * t7(i) + t10(i),
                                         lv2.X * t2(i) + lv2.Y * t5(i) + lv2.Z * t8(i) + t11(i))
                    Dim nm = Create_Normal_Matrix(md)
                    ' El world-cache queda en Double: lo consumen bounds, picking, el
                    ' exportador y el raytracer de oclusion. Lo unico que cambia es que
                    ' la normal de ENTRADA ya viene redondeada a Single.
                    ' `nm` YA es la matriz de normales: se aplica tal cual. TransformNormal la volvia a
                    ' invertir y terminaba transformando la normal con la matriz cruda. Ver PorMatriz3x3.
                    wn(i) = RecalcTBN.NormalizaComoNifly(PorMatriz3x3(RecalcTBN.ADbl(localNorms(i)), nm))
                Next
            End Sub)
        geo.CachedWorldVertices = wv
        geo.CachedWorldNormals = wn
        geo.WorldCacheValid = True
    End Sub

    ''' <summary>El AABB de mundo SIN materializar el cache: transforma solo POSICIONES y acumula min/max.
    ''' <para>Existe porque el AABB no lee las normales, y en <see cref="ComputeWorldSpaceCache"/> las
    ''' normales son la parte cara: por vertice, una <c>Create_Normal_Matrix</c> (inversa + transpuesta de
    ''' una 3x3) mas un TransformNormal y una normalizacion, contra UNA multiplicacion para la posicion.
    ''' Y ademas evita las dos <c>Vector3d()</c> por malla por frame, que en el camino de dibujo son basura
    ''' de GC.</para>
    ''' <para>NO deja el cache valido, a proposito: quien necesite normales de mundo (picking, export,
    ''' el raytracer de oclusion) lo va a pedir y lo va a computar entero. Lo que no se puede es dejarlo
    ''' MEDIO lleno, con posiciones nuevas y normales viejas.</para>
    ''' <para>El resultado es identico al de <see cref="ComputeWorldBounds"/>: la misma matriz por vertice,
    ''' el mismo TransformPosition, el mismo min/max en Double. Es la misma cuenta sin la parte que sobra.
    ''' </para></summary>
    Public Shared Sub ComputeWorldBoundsSinNormales(ByRef geo As SkinnedGeometry)
        ' SI EL CACHE YA ESTA VIVO, NO SE RECALCULA NADA. Sin esta salida, el camino FUERA DE PLAY hacia
        ' DOS pasadas O(vertices): RecomputeGPUBoneMatrices invalida el cache y —con updateWorldCache=True,
        ' que es justo lo que vale fuera de play— llama a ComputeWorldBounds, que lo reconstruye ENTERO y lo
        ' marca valido; y despues ComputeBounds volvia a transformar todos los vertices ignorandolo. O sea
        ' que el ahorro que esta funcion busca en play se pagaba de mas en cada slider de morph, cada cambio
        ' de pose y cada toggle de shape. El caso que si sirve —play con Option B— llega con el cache frio y
        ' no toca esta rama.
        If geo.WorldCacheValid AndAlso geo.CachedWorldVertices IsNot Nothing Then
            ComputeWorldBounds(geo)
            Exit Sub
        End If
        EnsurePerVertexSkinMatrix(geo)   ' Option B: recompone PerVertexSkinMatrix si pass-2 se salteo
        Dim count = geo.Vertices.Length
        If count = 0 Then Exit Sub
        Dim localVerts = geo.Vertices
        Dim localMats = geo.PerVertexSkinMatrix

        ' Chunks explicitos en vez de RangosDe: hace falta INDEXAR los rangos para el scatter->gather, y
        ' RangosDe devuelve un Partitioner, que no se indexa.
        Dim chunk As Integer = Math.Max(4096, count \ Math.Max(1, Environment.ProcessorCount * 4))
        Dim nChunks As Integer = (count + chunk - 1) \ chunk
        Dim mins(nChunks - 1) As Vector3d
        Dim maxs(nChunks - 1) As Vector3d
        ' SCATTER->GATHER, no un acumulador compartido: cada chunk escribe SU celda y despues se pliegan
        ' en serie. Un min/max compartido entre hilos es una carrera. (El pliegue es exacto en cualquier
        ' orden —min y max sobre Double no redondean—, asi que el resultado es determinista igual.)
        Parallel.For(0, nChunks,
            Sub(r As Integer)
                Dim mn As New Vector3d(Double.MaxValue)
                Dim mx As New Vector3d(Double.MinValue)
                Dim i1 = Math.Min(r * chunk + chunk, count)
                ' SIN Matrix4 NI Matrix4d POR VERTICE. `AMatrix4d(localMats(i))` hacia dos pasadas:
                ' el indexador reconstruia una Matrix4 (64 B) desde las 12 secciones y despues se ensanchaba
                ' a Matrix4d (128 B) — dos structs para usar 12 floats. Se leen las secciones directo.
                ' BIT A BIT IDENTICO: `TransformPosition` acumula en Double, y `Single -> Double` es
                ' exacto, asi que multiplicar `v.X * CDbl(s0(i))` da el mismo bit que el camino largo.
                ' Este bucle es el que corre POR FRAME cuando hay sombras (es el `bounds-en-play` que
                ' mide 9-13 ms sobre el Serena Battle Suit), asi que es donde mas duele la pasada de mas.
                Dim s0 = localMats.Secciones(0), s3 = localMats.Secciones(3), s6 = localMats.Secciones(6), s9 = localMats.Secciones(9)
                Dim s1 = localMats.Secciones(1), s4 = localMats.Secciones(4), s7 = localMats.Secciones(7), s10 = localMats.Secciones(10)
                Dim s2 = localMats.Secciones(2), s5 = localMats.Secciones(5), s8 = localMats.Secciones(8), s11 = localMats.Secciones(11)
                For i = r * chunk To i1 - 1
                    Dim lv = localVerts(i)
                    Dim w As New Vector3d(lv.X * s0(i) + lv.Y * s3(i) + lv.Z * s6(i) + s9(i),
                                          lv.X * s1(i) + lv.Y * s4(i) + lv.Z * s7(i) + s10(i),
                                          lv.X * s2(i) + lv.Y * s5(i) + lv.Z * s8(i) + s11(i))
                    If w.X < mn.X Then mn.X = w.X
                    If w.Y < mn.Y Then mn.Y = w.Y
                    If w.Z < mn.Z Then mn.Z = w.Z
                    If w.X > mx.X Then mx.X = w.X
                    If w.Y > mx.Y Then mx.Y = w.Y
                    If w.Z > mx.Z Then mx.Z = w.Z
                Next
                mins(r) = mn : maxs(r) = mx
            End Sub)

        Dim minV As New Vector3d(Double.MaxValue)
        Dim maxV As New Vector3d(Double.MinValue)
        For r = 0 To nChunks - 1
            minV = Vector3d.ComponentMin(minV, mins(r))
            maxV = Vector3d.ComponentMax(maxV, maxs(r))
        Next
        geo.Boundingcenter = (minV + maxV) * 0.5
        geo.Minv = minV
        geo.Maxv = maxV
    End Sub

    Public Shared Sub InvalidateWorldCache(ByRef geo As SkinnedGeometry)
        geo.WorldCacheValid = False
        geo.CachedWorldVertices = Nothing
        geo.CachedWorldNormals = Nothing
    End Sub

    ''' <summary>
    ''' Computes world-space bounding box from the world-space cache.
    ''' </summary>
    Public Shared Sub ComputeWorldBounds(ByRef geo As SkinnedGeometry)
        Dim wv = GetWorldVertices(geo)
        Dim minV As New Vector3d(Double.MaxValue)
        Dim maxV As New Vector3d(Double.MinValue)
        For Each v In wv
            If v.X < minV.X Then minV.X = v.X
            If v.Y < minV.Y Then minV.Y = v.Y
            If v.Z < minV.Z Then minV.Z = v.Z
            If v.X > maxV.X Then maxV.X = v.X
            If v.Y > maxV.Y Then maxV.Y = v.Y
            If v.Z > maxV.Z Then maxV.Z = v.Z
        Next
        geo.Boundingcenter = (minV + maxV) * 0.5
        geo.Minv = minV
        geo.Maxv = maxV
    End Sub

    ''' <summary>bindMount global del hueso (Original×Mount): de la cache de pase si está, o recursivo
    ''' (fallback: hueso huérfano / sin cache). Bit-idéntico a <c>OriginalGetGlobalTransform</c>.</summary>
    Private Shared Function CachedBindMount(bone As HierarchiBone_class, cache As SkeletonGlobalTransformCache) As Transform_Class
        Dim t As Transform_Class = Nothing
        If cache IsNot Nothing AndAlso cache.TryGetBindMount(bone, t) Then Return t
        Return bone.OriginalGetGlobalTransform
    End Function

    ''' <summary>display global del hueso (Original×Mount×Morph×Delta): de la cache de pase si está, o
    ''' recursivo (fallback). Bit-idéntico a <c>GetGlobalTransform</c>.</summary>
    Private Shared Function CachedDisplay(bone As HierarchiBone_class, cache As SkeletonGlobalTransformCache) As Transform_Class
        Dim t As Transform_Class = Nothing
        If cache IsNot Nothing AndAlso cache.TryGetDisplay(bone, t) Then Return t
        Return bone.GetGlobalTransform
    End Function

    ''' <summary>Recompone las <c>GPUBoneMatrices</c> (los datos del SSBO) para una pose nueva.
    ''' Composición: <c>GlobalTransform · poseT.ComposeTransforms(localT)</c>.
    ''' <para>SYNC: CPU/GPU skinning — esta composición tiene que coincidir con la de
    ''' <see cref="ExtractSkinnedGeometry"/>, y las matrices que produce las consume el loop de blend del
    ''' vertex shader. Lista completa de sitios gemelos en el contrato de
    ''' <c>BlendBoneMatrices</c> (arriba en este archivo) y en 00-reglas-ui-y-vb.md §10.</para></summary>
    ''' <param name="updateWorldCache">False saltea la pasada 3 (ComputeWorldBounds + world-cache
    ''' per-vértice): en animación + mesh OPACO nadie la muestra (frustum congelado; el display —GPU
    ''' del SSBO, CPU de UpdateSkinBuffers— NO lee el world-cache; en CPU es además redundante con
    ''' UpdateSkinBuffers). Aplica a GPU Y CPU. Default True = fuera de play, o meshes HasAlphaBlend
    ''' (Boundingcenter del sort de transparentes).</param>
    ''' <param name="updatePerVertexSkin">False saltea la pasada 2 (PerVertexSkinMatrix). Solo se
    ''' saltea en GPU-skin opaco en play (el shader skinnea del SSBO, no la usa). En CPU-skin SIEMPRE
    ''' se computa (el display la necesita en UpdateSkinBuffers). Cuando se saltea, marca
    ''' PerVertexMatrixValid=False → EnsurePerVertexSkinMatrix la recompone lazy si un lector la pide.
    ''' Invariante del caller: updatePerVertexSkin = cpuSkin OrElse updateWorldCache (la pasada 3 lee
    ''' PerVertexSkinMatrix, así que si la 3 corre, la 2 también). La pasada 1 (matrices→SSBO) siempre.</param>
    ''' <param name="globalCache">Memoización #3: cache de globals construida una vez por instancia
    ''' antes del loop de meshes. Si se pasa, los globals de hueso se leen de ahí (O(1) lookup) en vez
    ''' de recursar la cadena de padres. Nothing = recursivo (comportamiento original). Fallback
    ''' recursivo por hueso si falta en la cache.</param>
    Public Shared Sub RecomputeGPUBoneMatrices(shape As IRenderableShape, ByRef geo As SkinnedGeometry, singleboneskinning As Boolean, Optional skeleton As SkeletonInstance = Nothing, Optional updateWorldCache As Boolean = True, Optional updatePerVertexSkin As Boolean = True, Optional globalCache As SkeletonGlobalTransformCache = Nothing)
        If geo.GPUBoneMatrices Is Nothing Then Exit Sub
        Dim effectiveSkel As SkeletonInstance = If(skeleton, SkeletonInstance.Default)

        ' Capture the bone palette ONCE (per-frame hot path). Do NOT re-read shape.ShapeBones/
        ' ShapeBoneTransforms inline — some IRenderableShape impls (Wardrobe OSP_Clases) re-parse the
        ' NIF skin instance on every getter call, which would make repeated reads O(bones) re-parses.
        Dim bones = shape.ShapeBones
        Dim boneTrans = shape.ShapeBoneTransforms
        If boneTrans.Count <> bones.Count Then Exit Sub

        ' Recompute GlobalTransform
        Dim backing = shape.Geometry?.BackingShape
        Dim shapeNode = TryCast(shape.NifContent.GetParentNode(backing), NiNode)
        If IsNothing(shapeNode) Then shapeNode = shape.NifContent.GetRootNode()
        Dim GlobalTransform = Matrix4d.Identity
        ' Branching predicate: shape.IsSkinned (consistente con ExtractSkinnedGeometry).
        ' bones.Length>0 secundario para acceso seguro a array.
        If shape.IsSkinned AndAlso Not singleboneskinning AndAlso bones.Count > 0 Then
            ' Multi-bone path: recompute bone matrices once, use for both SSBO and per-vertex blending.
            ' Keep geo.BoneMatsBind/BoneMatsPose in sync too — BakeFromMemoryUsingOriginal reads
            ' them to compute bindTimesInvPose, and if they're stale from the previous Extract
            ' the first bake after a pose change collapses to identity.
            Dim precomputedBoneMatrices(bones.Count - 1) As Matrix4d
            If geo.BoneMatsBind Is Nothing OrElse geo.BoneMatsBind.Length <> bones.Count Then
                ReDim geo.BoneMatsBind(bones.Count - 1)
            End If
            If geo.BoneMatsPose Is Nothing OrElse geo.BoneMatsPose.Length <> bones.Count Then
                ReDim geo.BoneMatsPose(bones.Count - 1)
            End If
            ' Threading contract (lock-free read): SkeletonDictionary y globalCache se leen aquí SIN
            ' lock. Es seguro por la invariante documentada en
            ' SkeletonInstance.BuildGlobalTransformCacheForRenderPass: toda mutación del esqueleto y
            ' la construcción de la cache COMPLETAN antes de que arranque este render pass, así que no
            ' hay solapamiento mutación↔lectura. No agregar locks acá (riesgo perf/deadlock); el
            ' contrato a preservar es el orden build-then-read del caller.
            For k = 0 To bones.Count - 1
                Dim localT = boneTrans(k)
                Dim boneName = bones(k).Name.String
                Dim SkeletonBone As HierarchiBone_class = Nothing
                Dim poseT As Transform_Class
                Dim bindT As Transform_Class

                If effectiveSkel.SkeletonDictionary.TryGetValue(boneName, SkeletonBone) Then
                    bindT = CachedBindMount(SkeletonBone, globalCache)
                Else
                    bindT = Transform_Class.GetGlobalTransform(bones(k), shape.NifContent)
                End If

                If Not IsNothing(SkeletonBone) Then
                    poseT = CachedDisplay(SkeletonBone, globalCache)
                Else
                    poseT = bindT
                End If

                Dim matBind = bindT.ComposeTransforms(localT).ToMatrix4d()
                Dim matPose = poseT.ComposeTransforms(localT).ToMatrix4d()
                geo.BoneMatsBind(k) = matBind
                geo.BoneMatsPose(k) = matPose
            Next

            ' La paleta se arma DESPUES del loop y con BuildPosePalette, no inline: es la misma
            ' fórmula que usan Extract y la recomposición perezosa, y tiene que salir del mismo sitio.
            ' Cuesta una segunda pasada sobre ≤60 huesos.
            precomputedBoneMatrices = BuildPosePalette(geo.BoneMatsPose, GlobalTransform)
            For k = 0 To bones.Count - 1
                Dim m = precomputedBoneMatrices(k)
                geo.GPUBoneMatrices(k) = New Matrix4(
                    CSng(m.M11), CSng(m.M12), CSng(m.M13), CSng(m.M14),
                    CSng(m.M21), CSng(m.M22), CSng(m.M23), CSng(m.M24),
                    CSng(m.M31), CSng(m.M32), CSng(m.M33), CSng(m.M34),
                    CSng(m.M41), CSng(m.M42), CSng(m.M43), CSng(m.M44))
            Next

            ' Also update perVertexSkinMatrix for world-space cache
            ' (Recompute per-vertex blended matrices using the same precomputed bone matrices)

            Dim vertexCount = geo.Vertices.Length
            ' Capture arrays as locals for safe parallel access (geo is ByRef).
            ' Reuse the polymorphic skin data filled by ExtractSkinnedGeometry — no need to
            ' re-snapshot tri.VertexData/VertexDataSSE here, those arrays are already encoded
            ' in geo.Skinning (and they're empty for NiTriShape, where the partition path was used).
            Dim perVertexSkinMatrix = geo.PerVertexSkinMatrix
            Dim localSkinning = geo.Skinning

            If updatePerVertexSkin Then
                ' UN SOLO cuerpo, compartido con la recomposición perezosa. Ver FillPerVertexSkinMatrix.
                FillPerVertexSkinMatrix(perVertexSkinMatrix, localSkinning, precomputedBoneMatrices)
                geo.PerVertexMatrixValid = True
            Else
                ' GPU-skin + animación + opaco: el shader skinnea del SSBO (GPUBoneMatrices ya
                ' recomputadas arriba); PerVertexSkinMatrix solo alimenta el world-cache, que nadie
                ' muestra en play. Stale + inválido → EnsurePerVertexSkinMatrix lo recompone desde
                ' BoneMatsPose si un lector lo pide. (En CPU-skin esto NUNCA se saltea: el display
                ' lo necesita en UpdateSkinBuffers_GL.)
                geo.PerVertexMatrixValid = False
            End If
        Else
            ' Tres sub-casos: (1) skinned single-bone, (2) skinned degenerado (IsSkinned=True
            ' AndAlso bones=0), (3) unskinned puro. La rama (2) matchea OS Anim.cpp:711 →
            ' identity. La rama (3) matchea OS Anim.cpp:692-704 → shape.T/R/S × parent_chain.
            Dim Mtot As Matrix4d
            If shape.IsSkinned AndAlso bones.Count > 0 Then
                ' (1) Skinned single-bone: ignora pose animada por diseño (single-bone es modo
                ' preview rigido), pero debe respetar bindT(0) * localT(0) del hueso 0. Si no
                ' lo hiciera, el shape salta cada vez que cambia la pose porque se pierde el
                ' transform del hueso. Coincide con ExtractSkinnedGeometry caso single-bone.
                Dim localT = boneTrans(0)
                Dim boneName = bones(0).Name.String
                Dim SkeletonBone As HierarchiBone_class = Nothing
                Dim bindT As Transform_Class
                If effectiveSkel.SkeletonDictionary.TryGetValue(boneName, SkeletonBone) Then
                    bindT = CachedBindMount(SkeletonBone, globalCache)
                Else
                    bindT = Transform_Class.GetGlobalTransform(bones(0), shape.NifContent)
                End If
                Mtot = GlobalTransform * bindT.ComposeTransforms(localT).ToMatrix4d()
            ElseIf shape.IsSkinned Then
                ' (2) Skinned degenerado (skin instance presente, palette no resuelta).
                ' Identity, igual que OS Anim.cpp:711 (shapeSkinning lookup miss → MatTransform()).
                Mtot = Matrix4d.Identity
            Else
                ' (3) Unskinned puro: shape.T/R/S + parent chain.
                Mtot = Transform_Class.GetGlobalTransform(backing, shape.NifContent).ToMatrix4d()
            End If

            geo.GPUBoneMatrices(0) = New Matrix4(
                CSng(Mtot.M11), CSng(Mtot.M12), CSng(Mtot.M13), CSng(Mtot.M14),
                CSng(Mtot.M21), CSng(Mtot.M22), CSng(Mtot.M23), CSng(Mtot.M24),
                CSng(Mtot.M31), CSng(Mtot.M32), CSng(Mtot.M33), CSng(Mtot.M34),
                CSng(Mtot.M41), CSng(Mtot.M42), CSng(Mtot.M43), CSng(Mtot.M44))
            geo.PerVertexSkinMatrix.Llenar(AMatrix4(Mtot))
            geo.PerVertexMatrixValid = True   ' rama single-bone/unskinned: siempre se llena (barato)
        End If

        ' Invalidate world-space cache so it gets recomputed on next access. SIEMPRE se invalida
        ' (aunque salteemos el recompute) para que el próximo lector recompute con la pose nueva.
        InvalidateWorldCache(geo)
        ' Recompute world bounds from new pose — SALVO en Option B (GPU-anim opaco): nadie muestra los
        ' bounds en play (frustum ya usa mesh.BoundsMin congelado; el sort de transparentes pasa
        ' updateWorldCache=True). Tras Stop, el setter de PlayingAnimation fuerza un Pose dirty que
        ' recomputa todo con updateWorldCache=True.
        If updateWorldCache Then ComputeWorldBounds(geo)
    End Sub

End Class

''' <summary>
''' Holds per-vertex arrays in the types expected by IShapeGeometry.Set* methods.
''' Skinning is optional — populated by SnapshotSeparateArrays for round-trip on the
''' NiTriShape family (where per-vertex skin lives in NiSkinData rather than inline);
''' BSTriShape consumers can ignore it because their per-vertex skin travels inside
''' BSVertexData/SSE structs already.
''' </summary>
Public Class ShapeArrays
    Public Positions As List(Of System.Numerics.Vector3)
    Public Normals As List(Of System.Numerics.Vector3)
    Public Tangents As List(Of System.Numerics.Vector3)
    Public Bitangents As List(Of System.Numerics.Vector3)
    Public UVs As List(Of System.Numerics.Vector3)
    Public VertexColors As List(Of NiflySharp.Structs.Color4)
    Public EyeData As List(Of Single)
    Public Skinning As ShapeSkinningData?

    ''' <summary>Returns a new ShapeArrays containing only elements at the given original indices.</summary>
    Public Function FilterByIndices(indices As HashSet(Of Integer)) As ShapeArrays
        Dim r As New ShapeArrays()
        If Positions IsNot Nothing Then r.Positions = Positions.Where(Function(x, i) indices.Contains(i)).ToList()
        If Normals IsNot Nothing Then r.Normals = Normals.Where(Function(x, i) indices.Contains(i)).ToList()
        If Tangents IsNot Nothing Then r.Tangents = Tangents.Where(Function(x, i) indices.Contains(i)).ToList()
        If Bitangents IsNot Nothing Then r.Bitangents = Bitangents.Where(Function(x, i) indices.Contains(i)).ToList()
        If UVs IsNot Nothing Then r.UVs = UVs.Where(Function(x, i) indices.Contains(i)).ToList()
        If VertexColors IsNot Nothing Then r.VertexColors = VertexColors.Where(Function(x, i) indices.Contains(i)).ToList()
        If EyeData IsNot Nothing Then r.EyeData = EyeData.Where(Function(x, i) indices.Contains(i)).ToList()

        ' Per-vertex skinning compaction: keep only slots for surviving vertex indices,
        ' preserve WeightsPerVertex layout (default 4).  Bone palette unchanged.
        If Skinning.HasValue AndAlso Skinning.Value.BoneIndices IsNot Nothing Then
            Dim sk = Skinning.Value
            Dim wpv As Integer = If(sk.WeightsPerVertex > 0, sk.WeightsPerVertex, 4)
            Dim ordered = indices.OrderBy(Function(x) x).ToList()
            Dim newCount As Integer = ordered.Count
            Dim newIdx(newCount * wpv - 1) As Byte
            Dim newWgt(newCount * wpv - 1) As System.Half
            For i As Integer = 0 To newCount - 1
                Dim oldVert As Integer = ordered(i)
                Dim oldBase As Integer = oldVert * wpv
                Dim newBase As Integer = i * wpv
                For j As Integer = 0 To wpv - 1
                    newIdx(newBase + j) = sk.BoneIndices(oldBase + j)
                    newWgt(newBase + j) = sk.BoneWeights(oldBase + j)
                Next
            Next
            r.Skinning = New ShapeSkinningData() With {
                .BoneIndices = newIdx,
                .BoneWeights = newWgt,
                .WeightsPerVertex = wpv,
                .VertexCount = newCount,
                .BoneRefIndices = sk.BoneRefIndices
            }
        End If
        Return r
    End Function

    ''' <summary>Appends all arrays from another ShapeArrays (for merge/concatenation).</summary>
    Public Sub Append(other As ShapeArrays)
        If other Is Nothing Then Return
        If other.Positions IsNot Nothing Then
            If Positions Is Nothing Then Positions = New List(Of System.Numerics.Vector3)()
            Positions.AddRange(other.Positions)
        End If
        If other.Normals IsNot Nothing Then
            If Normals Is Nothing Then Normals = New List(Of System.Numerics.Vector3)()
            Normals.AddRange(other.Normals)
        End If
        If other.Tangents IsNot Nothing Then
            If Tangents Is Nothing Then Tangents = New List(Of System.Numerics.Vector3)()
            Tangents.AddRange(other.Tangents)
        End If
        If other.Bitangents IsNot Nothing Then
            If Bitangents Is Nothing Then Bitangents = New List(Of System.Numerics.Vector3)()
            Bitangents.AddRange(other.Bitangents)
        End If
        If other.UVs IsNot Nothing Then
            If UVs Is Nothing Then UVs = New List(Of System.Numerics.Vector3)()
            UVs.AddRange(other.UVs)
        End If
        If other.VertexColors IsNot Nothing Then
            If VertexColors Is Nothing Then VertexColors = New List(Of NiflySharp.Structs.Color4)()
            VertexColors.AddRange(other.VertexColors)
        End If
        If other.EyeData IsNot Nothing Then
            If EyeData Is Nothing Then EyeData = New List(Of Single)()
            EyeData.AddRange(other.EyeData)
        End If
        ' Skinning concat: flat BoneIndices + BoneWeights arrays concatenated with aligned
        ' WeightsPerVertex.  Caller is responsible for bone-palette remap on the donor's
        ' BoneIndices BEFORE Append (see MergeShapesHelper).  Both sides must agree on
        ' WeightsPerVertex; if not, throw loud — a 4-wpv target + 5-wpv donor merge is
        ' undefined behaviour in the NIF schema.
        If other.Skinning.HasValue Then
            If Not Skinning.HasValue Then
                ' Target had no skinning; adopt donor's as the start.
                Skinning = other.Skinning
            Else
                Dim a = Skinning.Value
                Dim b = other.Skinning.Value
                If a.WeightsPerVertex <> b.WeightsPerVertex AndAlso
                   a.WeightsPerVertex > 0 AndAlso b.WeightsPerVertex > 0 Then
                    Throw New NotSupportedException(
                        $"ShapeArrays.Append: WeightsPerVertex mismatch ({a.WeightsPerVertex} vs " &
                        $"{b.WeightsPerVertex}).  Cannot merge per-vertex skin with different slot " &
                        "counts without re-padding.")
                End If
                Dim wpv As Integer = If(a.WeightsPerVertex > 0, a.WeightsPerVertex, b.WeightsPerVertex)
                Dim aCount = a.VertexCount
                Dim bCount = b.VertexCount
                Dim combined As Integer = aCount + bCount
                Dim newIdx(combined * wpv - 1) As Byte
                Dim newWgt(combined * wpv - 1) As System.Half
                If a.BoneIndices IsNot Nothing Then Array.Copy(a.BoneIndices, 0, newIdx, 0, aCount * wpv)
                If a.BoneWeights IsNot Nothing Then Array.Copy(a.BoneWeights, 0, newWgt, 0, aCount * wpv)
                If b.BoneIndices IsNot Nothing Then Array.Copy(b.BoneIndices, 0, newIdx, aCount * wpv, bCount * wpv)
                If b.BoneWeights IsNot Nothing Then Array.Copy(b.BoneWeights, 0, newWgt, aCount * wpv, bCount * wpv)
                Skinning = New ShapeSkinningData() With {
                    .BoneIndices = newIdx,
                    .BoneWeights = newWgt,
                    .WeightsPerVertex = wpv,
                    .VertexCount = combined,
                    .BoneRefIndices = a.BoneRefIndices   ' target's palette reference wins
                }
            End If
        End If
    End Sub
End Class


Public Class RecalcTBN

    ''' <summary>Deja que el <c>&lt;Shape&gt;</c> APAGUE lo que el usuario dejó prendido, nunca al revés.
    ''' <para>⛔ SYNC: <c>BodySlideApp.cpp</c>, cuatro sitios con la misma forma —
    ''' <c>if (!lockNormals) CalcNormalsForShape(shape, force, smoothSeamNormals);</c> y
    ''' <c>CalcTangentsForShape(shape)</c> FUERA del <c>if</c>. <c>KeepExistingNormals = True</c> es
    ''' equivalente a no llamar a <c>CalcNormalsForShape</c>: envuelve el pase de normales entero
    ''' (recálculo, bloqueadas, suavizado de costura y restauración) y deja correr el de tangentes, que
    ''' es incondicional en las dos partes.</para>
    ''' <para>Los operadores no son simétricos a propósito: <c>OrElse</c> para bloquear —el .osp puede
    ''' pedir que NO se toquen aunque el usuario pida recalcular— y <c>AndAlso</c> para suavizar —el .osp
    ''' puede apagar el suavizado, pero no prenderlo si el usuario lo apagó—. Con una asignación directa,
    ''' como el default del atributo es True, se pisaba el toggle global en TODAS las shapes.</para>
    ''' <para>La casilla "Ignore authored restrictions" de la ventana de opciones (default OFF) es la
    ''' salida de emergencia para una prenda cuyas normales autoradas estén rotas.</para></summary>
    Public Shared Sub AplicarRestriccionesDelAutor(ByRef opts As TBNOptions, shape As IRenderableShape)
        If shape Is Nothing OrElse opts.IgnoreAuthoredRestrictions Then Return
        opts.KeepExistingNormals = opts.KeepExistingNormals OrElse shape.LockNormals
        opts.SmoothSeamNormals = opts.SmoothSeamNormals AndAlso shape.SmoothSeamNormals
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════════════════
    ' ⚠️ PENDIENTE, A PROPÓSITO SIN IMPLEMENTAR — el corte de NORMALES EN ESPACIO DE MODELO.
    '
    ' LA LEY EXISTE Y ACÁ FALTA. `nifly\src\NifFile.cpp:3812-3816`, lo primero de `CalcNormalsForShape`:
    '     if (hdr.GetVersion().IsSK() || hdr.GetVersion().IsSSE()) {
    '         NiShader* shader = GetShader(shape);
    '         if (shader && shader->IsModelSpace() && !force) return; }
    ' con `IsModelSpace()` = `shaderFlags1 & (1 << 12)` (`Shaders.cpp:268-270`). Corta SÓLO las normales;
    ' las tangentes siguen fuera del `if`.
    '
    ' POR QUÉ NO ESTÁ PUESTO. Dos motivos, y el segundo es el que manda:
    '
    '   1) HOY ES UN NO-OP, MEDIDO. Sobre los 5.610 NIF de los dos corpus: SSE tiene 977 shapes
    '      model-space en 905 de 2.412 archivos y FO4 tiene 0 (la ley canónica ni siquiera aplica ahí).
    '      De esas 977, NINGUNA pasa el gate de inyección de `InjectToTrishape` (`SkinningHelper.vb:1473`,
    '      `HasNormals OrElse HasTangents`): no traen ni un canal ni el otro. O sea que la app ya calcula
    '      en vano y NO ESCRIBE NADA — cero bytes de diferencia contra BodySlide.
    '      ⛔ Ya se reportó una vez como defecto vivo del 40 % del corpus de SSE. No lo es.
    '
    '   2) DE DÓNDE SALE EL FLAG NO ESTÁ RESUELTO, y elegir mal lo rompe:
    '        • el bloque de shader del NIF (`GetShader(shape).ModelSpace`) es lo que lee el canónico,
    '          porque nifly es una librería de NIF y no conoce los materiales externos;
    '        • el material RESUELTO (`ShapeMaterial.material.ModelSpaceNormals`) es lo que lee el RENDER
    '          (`Render.vb:3028`), y en FO4 un BGSM REEMPLAZA lo del NIF
    '          (`FO4UnifiedMaterial_Class.vb:3439` lo siembra desde el shader sólo cuando NO hay material).
    '      En Skyrim las dos fuentes coinciden, así que el gate de versión las vuelve indistintas y la
    '      pregunta queda sin responder por construcción. Poner una de las dos "porque compila" es elegir
    '      a ciegas la ley de RENDER == BAKE.
    '
    ' Quien lo implemente: decidir primero la fuente con una medición que las SEPARE, no con el corpus
    ' actual, que no puede distinguirlas.
    ' ═══════════════════════════════════════════════════════════════════════════════════════════════


    Public Structure TBNCache
        ' Copia/Referencia de índices del mesh (no se modifica aquí)
        Public Indices As UInteger()
        ' Cantidad de triángulos
        Public TriCount As Integer
        ' Adjacencia: por cada vértice -> lista de triángulos incidentes (ID de tri: [0..TriCount-1])
        ' Adyacencia vertice -> incidencias, en CSR: las del vertice v son
        ' V2TData(V2TStart(v) .. V2TStart(v+1)-1). V2TStart tiene nVerts+1 entradas.
        ' Cada entrada empaqueta `triangulo*4 + esquina` (esquina 0..2).
        Public V2TStart As Integer()
        Public V2TData As Integer()
        ' Derivadas UV precomputadas por triángulo (dependen SOLO de UV)
        ' EN SINGLE, como el canonico: `CalcTangentSpace` (nifly Geometry.cpp:999-1026) calcula
        ' s1/s2/t1/t2 en float a partir de UVs float. Las UVs de WM YA son Single (Uvs_Weight), asi
        ' que hacer la resta en Double no aporta informacion: solo la retiene mas tiempo.
        Public Tri_du1 As Single()
        Public Tri_dv1 As Single()
        Public Tri_du2 As Single()
        Public Tri_dv2 As Single()
        ''' <summary>
        ''' SOLO EL SIGNO del determinante UV, no el determinante. `True` = negativo.
        '''
        ''' El unico consumidor del determinante en todo el repo es <c>ComputeFaceTB</c>, y lo usa
        ''' EXCLUSIVAMENTE para <c>r = If(det &gt;= 0, +1, -1)</c> — la magnitud no se lee nunca. Guardarlo
        ''' como Double cuesta 8 bytes por triangulo (~16 B por vertice en una malla cerrada) para
        ''' transportar un bit. El determinante se calcula en SINGLE, igual que el canonico
        ''' (<c>float r = s1*t2 - s2*t1</c>, nifly Geometry.cpp), asi que el signo es el mismo por
        ''' construccion.
        '''
        ''' La forma es <c>Not (det &gt;= 0.0)</c>, NO <c>det &lt; 0.0</c>. Con un determinante NaN
        ''' —UVs corruptas en el NIF de un mod— <c>NaN &gt;= 0</c> es False (r = -1) pero
        ''' <c>NaN &lt; 0</c> TAMBIEN es False (r = +1). Escribirlo al reves invertiria la tangente de
        ''' esas caras en vez de dejarlas como estaban.
        ''' </summary>
        Public Tri_DetNeg As Boolean()

        ''' <summary>La precisión de UV con la que se calcularon las derivadas. Parte de la firma del
        ''' cache: si cambia, las derivadas guardadas ya no son las que corresponden.</summary>
        Public UvHalf As Boolean

        ''' <summary>
        ''' SCRATCH REUSABLE del recálculo, colgado del cache porque vive exactamente lo mismo que él
        ''' —la topología— y porque así se asigna UNA vez por malla en vez de una por llamada.
        '''
        ''' La validez va por SELLO DE CORRIDA, no por un valor centinela: `Scratch_SlotSello(v) =
        ''' Corrida` significa "`Scratch_SlotDe(v)` es de ESTA llamada". Antes cada llamada asignaba
        ''' `slotDe`, `vertArr`, `triVisto`, `triArr` y `slotTri` —cinco arrays del tamaño de la MALLA—
        ''' y encima barría dos de ellos enteros para inicializarlos a -1. O sea que mover un puñado de
        ''' vértices seguía pagando O(malla) en asignaciones, en puesta a cero del runtime y en presión
        ''' de GC, dentro de una operación cuya razón de ser es ser proporcional a lo que cambió.
        ''' Con el sello no hay init ni reseteo: una entrada vieja simplemente no coincide.
        ''' Es estado MUTABLE por malla: dos hilos recalculando la MISMA `SkinnedGeometry` a la vez se
        ''' pisarían. Ya no era seguro antes —escriben `geo.Normals` sin sincronizar— y el paralelismo
        ''' del bake es POR NPC, así que cada hilo trae su propia geometría.
        ''' </summary>
        Public Corrida As Integer
        Public Scratch_SlotDe As Integer()
        Public Scratch_SlotSello As Integer()
        Public Scratch_VertArr As Integer()
        Public Scratch_SlotTri As Integer()
        Public Scratch_TriSello As Integer()
        Public Scratch_TriArr As Integer()

        ''' <summary>
        ''' El orden por X de la ultima llamada, y las X con las que se armo. Es lo que permite no
        ''' re-ordenar la malla entera cuando se movieron unos pocos vertices. Ver
        ''' <see cref="OrdenPorX"/>.
        ''' NO se invalida con las posiciones: se CONTRASTA contra ellas. Guardar las X es lo que
        ''' hace que un cambio de posicion que nadie declaro sucio igual se detecte.
        ''' </summary>
        Public Orden_PorX As Integer()
        Public X_DelOrden As Double()
    End Structure

    ' -------------------------------
    ' Opciones de calidad / robustez
    ' -------------------------------
    ' HAY UNA SOLA LEY de ponderado, la de BodySlide: la normal de cara se acumula SIN normalizar
    ' (pesa por area) y la base tangente se normaliza por triangulo y se acumula sin peso. El
    ' NO reintroducir un `NormalWeightMode` configurable (area / angulo / area x angulo): deja el marco
    ' tangente rotado respecto del canonico, que es el marco contra el que se autoran los normal maps del
    ' ecosistema. Una clave `WeightMode` en un config.json viejo se ignora.
    Public Structure TBNOptions
        ''' <summary>
        ''' Umbral de TRIÁNGULO DEGENERADO, en LONGITUD y en unidades del modelo: se descarta el aporte
        ''' de una cara cuya dirección tangente mide menos que esto. Default 0 = el canónico.
        ''' Es una longitud, no una longitud al cuadrado. El predicado compara contra
        ''' <c>LengthSquared</c> —para no pagar una raíz por triángulo— así que quien lo consume lo
        ''' eleva al cuadrado UNA vez (<see cref="ComputeFaceTB"/>). Pasarlo crudo deja el umbral efectivo en
        ''' <c>sqrt(eps)</c> y la opción filtra mil veces más de lo que el usuario pidió.
        ''' </summary>
        Public Property EpsilonPos As Double
        Public Property NormalizeOutputs As Boolean             ' normalizar N/T/B al final
        Public Property RepairNaNs As Boolean                   ' si True: reemplaza NaN por vectores seguros

        ''' <summary>
        ''' Sólo TANGENTES: ortogonaliza contra la normal ALMACENADA y no reescribe <c>geo.Normals</c>.
        ''' Es lo que hace <c>CalcTangentsForShape</c> del canónico, que corre INCONDICIONAL en la
        ''' fase 3 del build (BodySlideApp.cpp:4501 y :4529) mientras las normales van aparte y
        ''' gateadas por <c>lockNormals</c> (:4494).
        ''' Hace falta cuando lo único que cambió son las UVs — la base tangente se deriva de ellas,
        ''' las normales no. NO alcanza con recalcular todo y restaurar <c>Normals</c> después: el
        ''' Gram-Schmidt de abajo ortogonaliza T (y deriva B) contra la N RECALCULADA, así que
        ''' restaurarla al final dejaba una base que no es ortonormal respecto de la normal que
        ''' finalmente queda en la geometría.
        ''' Con <c>EnableWelding</c> cada miembro del grupo conserva SU normal, asi que T/B se
        ''' reortogonalizan por miembro: propagarle la del maestro (que es lo que se hace cuando la
        ''' opcion esta apagada, porque ahi la normal del maestro TAMBIEN se le escribe) dejaria la
        ''' base torcida respecto de la normal que el miembro se queda.
        ''' </summary>
        Public Property KeepExistingNormals As Boolean

        ''' <summary>
        ''' Promedia las normales de los vértices COINCIDENTES en posición — las costuras — sumando
        ''' sólo las que estén a menos de <see cref="SmoothSeamNormalsAngle"/>.
        '''
        ''' NO es opcional para tener paridad: es lo que hace el canónico en CADA build
        ''' (<c>CalcNormalsForShape(shape, force, smoothSeamNormals)</c>, BodySlideApp.cpp:4494-4496 →
        ''' nifly <c>CalculateNormals</c>, Geometry.cpp:912-935), su default es <b>true</b> y el corpus
        ''' trae el atributo <c>SmoothSeamNormals="false"</c> en <b>8</b> shapes de FO4 (la afirmación
        ''' anterior de este comentario, "ni una sola vez", era FALSA y por eso nadie lo leía). En los otros
        ''' 15.461 el default true rige igual, así que acá también arranca en True — pero la decisión es
        ''' POR SHAPE y la trae el .osp: ver <see cref="IRenderableShape.SmoothSeamNormals"/>.
        '''
        ''' Sin esto, cada duplicado de una costura acumula sólo las caras que referencian SU índice —
        ''' medio vecindario — así que dos vértices en el MISMO punto de la superficie quedan con
        ''' normales distintas y el juego dibuja una línea de iluminación. MEDIDO sobre `CBBE Body`: en los
        ''' 2.444 vértices de costura, 6,52° de media contra BodySlide y hasta 54°; en los 20.264 sin costura,
        ''' 0,02°. `LaceBra`, que no tiene ninguna costura, da 0,01° — o sea que el error está TODO acá.
        '''
        ''' No confundir con <see cref="EnableWelding"/>: aquél elige un MAESTRO y le copia su
        ''' normal a todo el grupo, lo que borra las aristas duras. Éste deja que cada miembro conserve
        ''' la suya y sólo suma los que estén dentro del umbral, que es justo lo que las preserva.
        ''' </summary>
        Public Property SmoothSeamNormals As Boolean

        ''' <summary>Umbral en GRADOS. Un compañero de costura que difiera en <b>más o igual</b> que
        ''' esto no aporta — así una arista dura sigue dura. Default 60, que es
        ''' <c>SliderSetShape::SliderSetDefaultSmoothAngle</c> (SliderSet.h:24).</summary>
        Public Property SmoothSeamNormalsAngle As Double

        ''' <summary>Ignora lo que cada shape pidió en su <c>&lt;Shape&gt;</c> y recalcula todo con estas
        ''' opciones. Default <b>False</b>.
        ''' <para>Con False —el default— manda el autor de la prenda, que es lo que hace BodySlide:
        ''' <c>LockNormals</c> le dice "no me toques las normales" y <c>SmoothSeamNormals="false"</c> "no me
        ''' promedies la costura", y el canónico los respeta shape por shape
        ''' (<c>SliderSet.cpp:255-257</c> + los cuatro sitios de <c>BodySlideApp.cpp</c>). MEDIDO: 41 shapes
        ''' del corpus piden lo primero y 8 lo segundo.</para>
        ''' <para>Con True se ignoran los dos atributos y se recalcula con lo que diga esta ventana. Es la
        ''' conducta que la app tenía antes de leerlos, y se deja como salida de emergencia para una prenda
        ''' cuyas normales autoradas estén rotas.</para></summary>
        Public Property IgnoreAuthoredRestrictions As Boolean

        ''' <summary>
        ''' Cuando el Gram-Schmidt del SECUNDARIO se cancela, completa la base con
        ''' <c>N × primario</c> en vez de normalizar el residuo.
        '''
        ''' Esto es un DEFECTO DEL CANÓNICO, medido, no una preferencia. Si el acumulado de
        ''' <c>sdir</c> queda contenido en el plano de la normal y del primario, el residuo
        ''' <c>S − P·(P·S)</c> cae al piso de ruido de <c>Single</c> y el <c>Normalize()</c> final lo
        ''' amplifica a un unitario cuya dirección es puro redondeo — tan puro que ni siquiera es
        ''' perpendicular a la normal. MEDIDO sobre SSE <c>BaseUndies</c> (288 vértices), partiendo la
        ''' población por si coincide o no con lo que se escribió:
        '''   · coinciden (230): residuo p50 = 9,6e−1
        '''   · difieren  ( 52): residuo p50 = 9,2e−8, máximo 5,7e−7  ⇒ bajo el épsilon de Single
        ''' El histograma tiene un HUECO de cinco décadas entre 1e−6 y 1e−2, así que la separación no
        ''' es un corte elegido a dedo. Los 52 son exactamente los que BodySlide y WM resuelven con
        ''' signo OPUESTO: no hay respuesta correcta, la decide el último bit.
        '''
        ''' Con esto en True la base sale ortonormal y REPRODUCIBLE; en False sale el unitario de ruido
        ''' del canónico, que es lo que hace falta para comparar byte a byte contra BodySlide.
        ''' El umbral no es una constante calibrada: es <c>sqrt(2^-23)</c>, la raíz del épsilon de
        ''' IEEE-754 binary32 y el criterio clásico de pérdida catastrófica de significancia en una
        ''' resta. Depende del TIPO, no del equipo ni de la malla.
        ''' </summary>
        Public Property DeterministicOnCollapse As Boolean

        ' --- Welding (opcional) ---
        Public Property EnableWelding As Boolean                ' activa agrupación por posición+UV
        Public Property WeldPosEpsilon As Double                ' tolerancia para posición (en unidades del modelo)
        Public Property WeldUVEpsilon As Double                 ' tolerancia para UV (u,v)
        Public Property WeldByPositionOnly As Boolean           ' Only positions or positions + UV

        ''' <summary>
        ''' CENTINELA DE MIGRACIÓN. <c>TBNOptions</c> es una Structure: el deserializador la crea en
        ''' CERO y sólo asigna las claves que encuentra, así que una opción NUEVA queda en <c>False</c>
        ''' o en <c>0</c> para todo usuario que ya tenga un <c>config.json</c> — o sea que estrenaría la
        ''' opción APAGADA sin haberlo pedido. Este número dice con qué juego de opciones se escribió el
        ''' archivo; <c>Config_App.RepararOpcionesTBN</c> compara contra
        ''' <see cref="VersionDeOpcionesTBN"/> y, si el archivo declara una version ANTERIOR, repone los
        ''' defaults COMPLETOS. NO hay ramas por version — se probaron y trajeron mas defectos que los que
        ''' evitaban (una clave salteada dos versiones, un centinela invalido, una rama inalcanzable).
        ''' Al agregar una opcion: subir la constante Y darle su default en <c>DefaultTBNOptions</c>. El
        ''' default es OBLIGATORIO: sin el, el campo queda en el cero de la Structure, que es justo el
        ''' defecto que este mecanismo cierra.
        ''' </summary>
        Public Property OptionsVersion As Integer
    End Structure

    ''' <summary>Version del juego de opciones de TBN con el que se escribio el config.
    ''' <para>SUBIRLA ES TODO LO QUE HAY QUE HACER al agregar o cambiar una opcion: `RepararOpcionesTBN`
    ''' repone los defaults COMPLETOS cuando el archivo declara una version anterior. No hay ramas por
    ''' version que escribir — ni, por lo tanto, que olvidarse, que era de donde salian los defectos.</para>
    ''' <para>Eso PISA lo que el usuario hubiera elegido. Es una decision expresa suya: un cambio de
    ''' version significa que los criterios cambiaron, y arrancar con los defaults nuevos es preferible a
    ''' arrastrar una mezcla que nadie eligio.</para></summary>
    Public Const VersionDeOpcionesTBN As Integer = 3

    ''' <summary>
    ''' ÚNICA fuente de los defaults del TBN. No re-declararlos en ningún otro lado: el centinela de
    ''' <c>Config_Class.LoadConfig</c> los toma de acá.
    '''
    ''' Coinciden con el canónico salvo en <c>DeterministicOnCollapse</c> —donde el canónico amplifica
    ''' ruido de redondeo y está documentado en la propia opción— y en el detalle de abajo:
    '''
    ''' <c>EpsilonPos = 0</c> es el canónico (<c>sdir.Normalize()</c> a secas, sin umbral) y es el
    ''' default. Estuvo en 1e-12 con una justificación que resultó FALSA: se había medido con las
    ''' posiciones redondeadas a la precisión del formato, y ese redondeo era justo lo que fabricaba los
    ''' triángulos casi degenerados que el umbral parecía salvar. Sacado el redondeo se volvió a medir:
    ''' en FO4 <c>CBBE</c> el umbral EMPEORA (bitangente de costura 0,52° → 0,85° y su máximo de 153°
    ''' → 180°), y en SSE <c>BaseUndies</c>/<c>BaseArmor</c> es INERTE, byte por byte, porque ningún
    ''' triángulo cae debajo. La opción sigue expuesta —descarta el aporte de un triángulo cuya
    ''' dirección es puro redondeo— pero su default es el canónico. Control positivo de que llega al
    ''' motor: con 1.0 destruye la base en los dos juegos (tangente a 79°/90°).
    '''
    ''' <c>WeldByPositionOnly = True</c>. Estuvo en False —agrupar por posición Y UV— con el
    ''' argumento de que por posición sola fusionaría vértices separados justamente por tener UV
    ''' distinta. Ese argumento es exactamente al revés y la medición lo demuestra: un vértice de
    ''' costura ESTÁ duplicado porque su UV difiere, así que exigir UV igual no agrupa nada y la
    ''' función queda en un no-op. MEDIDO en FO4 <c>CBBE Body</c> con <c>EnableWelding=True</c>,
    ''' mirando la dispersión del marco tangente DENTRO de cada grupo de posición idéntica —que es el
    ''' propósito declarado de la opción—:
    '''   · por posición Y UV : 84,13° de media, 1.175 de 1.191 grupos dispersos  (= no hace nada)
    '''   · sólo por posición :  6,19° de media,   185 de 1.191 grupos dispersos
    '''   · en <c>Panty</c>: 64,67° → 0,01°, y CERO grupos dispersos
    ''' Los 185 que quedan son costuras de espejo cuyos miembros tienen normales distintas: ahí la base
    ''' se reortogonaliza por miembro a propósito, para que ninguno herede un marco torcido.
    ''' Sólo se lee con <c>EnableWelding</c> puesta, que viene apagada, así que cambiar este default no
    ''' mueve un byte de la salida por defecto.
    '''
    ''' Nota histórica del texto anterior, que se conserva porque la comparación sigue siendo válida:
    ''' el weld del canónico agrupa
    ''' por posición sola, pero es para promediar NORMALES y eso ya lo cubre <c>SmoothSeamNormals</c>;
    ''' éste comparte la base entera, y por posición sola fusionaría vértices que están separados
    ''' justamente porque tienen UV distinta.
    ''' </summary>
    ''' <remarks><c>KeepExistingNormals</c> ESTABA FALTANDO, y era el UNICO de los doce sin default:
    ''' vivia del cero de la Structure. Coincide con el valor correcto, asi que nunca hubo sintoma — pero
    ''' desde que la migracion es "version anterior ⇒ <c>DefaultTBNOptions()</c> completo", este metodo es
    ''' la UNICA fuente de los defaults, y un campo que no este aca no se repone: se estrena en el cero del
    ''' deserializador. La regla nacio incumplida en el mismo cambio que la escribio.
    ''' <para>Y el gate no lo veia: comparaba False (golden) contra False (cero de la Structure), o sea que
    ''' pasaba por coincidencia. Ahora hay un valor DECLARADO detras de esa comparacion.</para></remarks>
    Public Shared Function DefaultTBNOptions() As TBNOptions
        Return New TBNOptions With {
                .EpsilonPos = 0.0,
                .NormalizeOutputs = True,
                .RepairNaNs = True,
                .KeepExistingNormals = False,
                .SmoothSeamNormals = True,             ' canonico: smooth = true por defecto
                .SmoothSeamNormalsAngle = 60.0,        ' canonico: SliderSetDefaultSmoothAngle
                .IgnoreAuthoredRestrictions = False,   ' manda el autor de la prenda, como BodySlide
                .DeterministicOnCollapse = True,       ' el canonico ahi amplifica ruido: ver la doc de la opcion
                .EnableWelding = False,
                .WeldPosEpsilon = 0.000000000001,
                .WeldUVEpsilon = 0.000000000001,
                .WeldByPositionOnly = True,     ' por posicion Y UV el welding no agrupa nada: ver la doc de arriba
                .OptionsVersion = VersionDeOpcionesTBN
            }
    End Function

    ' =========================================================================
    ' BUILD CACHE (llamar una sola vez al cargar o cuando cambien UV o índices)
    ' - Precomputa:
    '   * VertexToTriangles (adjacencia)
    '   * Derivadas UV por triángulo (du1,dv1,du2,dv2,det)
    ' =========================================================================
    Public Shared Function BuildTBNCache(ByRef Uvs_Weight() As Vector3, ByVal indices As UInteger(),
                                         ByVal uvHalf As Boolean) As TBNCache
        Dim nVerts As Integer = Uvs_Weight.Length
        Dim triCount As Integer = indices.Length \ 3

        ' ADYACENCIA EN CSR PLANO (offsets + array), no una List(Of Integer) por vertice.
        ' Una List por vertice son N objetos por cada construccion de cache (22.700 en un cuerpo) y
        ' deja el recorrido de la fase B saltando de heap en heap. Con CSR son DOS arrays y el
        ' recorrido de los triangulos incidentes de un vertice es secuencial en memoria.
        ' El ORDEN dentro de cada vertice es el mismo que tenia la List (triangulos en orden
        ' creciente), asi que la suma se hace en el mismo orden ⇒ mismos bytes.
        ' Pasada 1: contar. Pasada 2: llenar.
        Dim v2tStart(nVerts) As Integer          ' nVerts + 1 offsets
        For t As Integer = 0 To triCount - 1
            Dim a As Integer = CInt(indices(3 * t + 0))
            Dim b As Integer = CInt(indices(3 * t + 1))
            Dim c As Integer = CInt(indices(3 * t + 2))
            If a >= nVerts OrElse b >= nVerts OrElse c >= nVerts Then Continue For
            v2tStart(a + 1) += 1
            v2tStart(b + 1) += 1
            v2tStart(c + 1) += 1
        Next
        For v = 1 To nVerts
            v2tStart(v) += v2tStart(v - 1)
        Next
        Dim v2tData(Math.Max(0, v2tStart(nVerts) - 1)) As Integer
        Dim cursor(nVerts - 1) As Integer
        Array.Copy(v2tStart, cursor, nVerts)

        ' Derivadas UV por tri
        Dim du1(triCount - 1) As Single
        Dim dv1(triCount - 1) As Single
        Dim du2(triCount - 1) As Single
        Dim dv2(triCount - 1) As Single
        ' Solo el SIGNO — ver Tri_DetNeg.
        Dim detNeg(triCount - 1) As Boolean

        For t As Integer = 0 To triCount - 1
            Dim i0 As Integer = CInt(indices(3 * t + 0))
            Dim i1 As Integer = CInt(indices(3 * t + 1))
            Dim i2 As Integer = CInt(indices(3 * t + 2))

            If i0 >= nVerts OrElse i1 >= nVerts OrElse i2 >= nVerts Then Continue For
            ' Se guarda `triangulo*4 + esquina`, no sólo el triángulo. La esquina es lo que el
            ' consumidor tenía que redescubrir comparando los 3 índices del triángulo contra el
            ' vértice — 3 loads y 3 comparaciones por entrada que ahora no existen. Y un vértice
            ' repetido en un triángulo degenerado queda como DOS entradas, que es exactamente el
            ' doble aporte que corresponde.
            v2tData(cursor(i0)) = t * 4 : cursor(i0) += 1
            v2tData(cursor(i1)) = t * 4 + 1 : cursor(i1) += 1
            v2tData(cursor(i2)) = t * 4 + 2 : cursor(i2) += 1


            ' UV del tri
            Dim uv0 As Vector3 = Uvs_Weight(i0)
            Dim uv1 As Vector3 = Uvs_Weight(i1)
            Dim uv2 As Vector3 = Uvs_Weight(i2)

            ' Las UV entran con la PRECISION DEL FORMATO. `CalcTangentSpace` deriva s1/s2/t1/t2
            ' de `vertData[i].uv`, que en BSTriShape esta guardado en HALF (Geometry.cpp:535); con las
            ' de plena precision el residuo es otro, y en una costura de espejo eso cambia el signo.
            Dim u0 As Single = UvComponente(uv0.X, uvHalf), v0 As Single = UvComponente(uv0.Y, uvHalf)
            Dim _du1 As Single = UvComponente(uv1.X, uvHalf) - u0
            Dim _dv1 As Single = UvComponente(uv1.Y, uvHalf) - v0
            Dim _du2 As Single = UvComponente(uv2.X, uvHalf) - u0
            Dim _dv2 As Single = UvComponente(uv2.Y, uvHalf) - v0

            du1(t) = _du1 : dv1(t) = _dv1
            du2(t) = _du2 : dv2(t) = _dv2
            detNeg(t) = Not ((_du1 * _dv2 - _du2 * _dv1) >= 0.0F)
        Next

        Return New TBNCache With {
            .Indices = indices,
            .TriCount = triCount,
            .V2TStart = v2tStart,
            .V2TData = v2tData,
            .Tri_du1 = du1, .Tri_dv1 = dv1,
            .Tri_du2 = du2, .Tri_dv2 = dv2,
            .Tri_DetNeg = detNeg,
            .UvHalf = uvHalf,
            .Corrida = 0,
            .Scratch_SlotDe = New Integer(Math.Max(0, nVerts - 1)) {},
            .Scratch_SlotSello = New Integer(Math.Max(0, nVerts - 1)) {},
            .Scratch_VertArr = New Integer(Math.Max(0, nVerts - 1)) {},
            .Scratch_SlotTri = New Integer(Math.Max(0, triCount - 1)) {},
            .Scratch_TriSello = New Integer(Math.Max(0, triCount - 1)) {},
            .Scratch_TriArr = New Integer(Math.Max(0, triCount - 1)) {}
        }
    End Function

    ''' <summary>
    ''' ¿El cache corresponde a ESTA geometría? Se compara la FIRMA ESTRUCTURAL, no un flag de sucio:
    ''' la referencia del array de índices, el conteo de vértices y la precisión de UV con la que se
    ''' calcularon las derivadas.
    '''
    ''' Con <c>Indices Is Nothing</c> como única condición, la coherencia dependería enteramente de que todos
    ''' los llamadores se acuerden de invalidar a mano. Para las UVs esa disciplina existe y está documentada
    ''' (<c>CachedTBN = Nothing</c> al mover UVs); para un cambio de topología o de conteo de vértices no hay
    ''' ninguna, y el modo de fallar es silencioso y feo: adyacencia y derivadas de OTRA malla, o un
    ''' <c>IndexOutOfRange</c> a mitad de un build.
    ''' Verificar la firma no reemplaza a la invalidación explícita —no ve un cambio de VALOR de las
    ''' UVs, que conserva la firma— sino que le pone piso a lo que un olvido puede romper.
    ''' </summary>
    Private Shared Function CacheEsCoherente(ByRef c As TBNCache, indices As UInteger(),
                                             nVerts As Integer, uvHalf As Boolean) As Boolean
        If c.Indices Is Nothing OrElse c.V2TStart Is Nothing Then Return False
        If Not Object.ReferenceEquals(c.Indices, indices) Then Return False
        If c.V2TStart.Length <> nVerts + 1 Then Return False
        If c.UvHalf <> uvHalf Then Return False
        If c.Scratch_SlotDe Is Nothing OrElse c.Scratch_SlotDe.Length < nVerts Then Return False
        If c.Scratch_TriArr Is Nothing OrElse c.Scratch_TriArr.Length < c.TriCount Then Return False
        Return True
    End Function

    ''' <summary>
    ''' Refresca SOLO las derivadas UV por triangulo de los triangulos incidentes a
    ''' <paramref name="verticesTocados"/>, conservando el resto del cache.
    '''
    ''' Existe para no tirar el cache entero cuando lo unico que se movio son UVs. El cache tiene
    ''' dos mitades con dependencias distintas: la ADJACENCIA (<c>VertexToTriangles</c>,
    ''' <c>TriCount</c>, <c>Indices</c>) depende solo de los indices, y las DERIVADAS
    ''' (<c>Tri_du*</c>, <c>Tri_det</c>) dependen de las UVs. Un slider uv no toca los indices, asi
    ''' que rehacer la adjacencia — que es la parte cara, O(triangulos) con una List por vertice —
    ''' era trabajo tirado en cada tick del arrastre.
    '''
    ''' No hace nada si todavia no hay cache: ahi <c>RecalculateNormalsTangentsBitangents</c> lo
    ''' construye entero desde las UVs actuales, que ya es lo correcto.
    ''' </summary>
    Public Shared Sub RefreshUvDerivatives(ByRef geo As SkinnedGeometry, verticesTocados As HashSet(Of Integer))
        If verticesTocados Is Nothing OrElse verticesTocados.Count = 0 Then Return
        Dim c = geo.CachedTBN
        If c.Indices Is Nothing OrElse c.V2TStart Is Nothing OrElse c.V2TData Is Nothing Then Return
        If geo.Uvs_Weight Is Nothing Then Return

        Dim nVerts As Integer = geo.Uvs_Weight.Length
        Dim uvHalf As Boolean = geo.Geometry IsNot Nothing AndAlso geo.Geometry.UvsAreHalfPrecision
        Dim tris As New HashSet(Of Integer)()
        For Each vi In verticesTocados
            If vi < 0 OrElse vi >= c.V2TStart.Length - 1 Then Continue For
            For k = c.V2TStart(vi) To c.V2TStart(vi + 1) - 1
                tris.Add(c.V2TData(k) >> 2)
            Next
        Next

        For Each t In tris
            If t < 0 OrElse t >= c.TriCount Then Continue For
            Dim i0 As Integer = CInt(c.Indices(3 * t + 0))
            Dim i1 As Integer = CInt(c.Indices(3 * t + 1))
            Dim i2 As Integer = CInt(c.Indices(3 * t + 2))
            If i0 >= nVerts OrElse i1 >= nVerts OrElse i2 >= nVerts Then Continue For

            Dim uv0 As Vector3 = geo.Uvs_Weight(i0)
            Dim uv1 As Vector3 = geo.Uvs_Weight(i1)
            Dim uv2 As Vector3 = geo.Uvs_Weight(i2)

            Dim u0 As Single = UvComponente(uv0.X, uvHalf), v0 As Single = UvComponente(uv0.Y, uvHalf)
            Dim _du1 As Single = UvComponente(uv1.X, uvHalf) - u0
            Dim _dv1 As Single = UvComponente(uv1.Y, uvHalf) - v0
            Dim _du2 As Single = UvComponente(uv2.X, uvHalf) - u0
            Dim _dv2 As Single = UvComponente(uv2.Y, uvHalf) - v0

            c.Tri_du1(t) = _du1 : c.Tri_dv1(t) = _dv1
            c.Tri_du2(t) = _du2 : c.Tri_dv2(t) = _dv2
            ' SITIO GEMELO de BuildTBNCache: la MISMA expresion, el MISMO tipo (Single) y la MISMA
            ' forma `Not (x >= 0)`. Si divergen, el bug solo se ve al mover un slider uv.
            c.Tri_DetNeg(t) = Not ((_du1 * _dv2 - _du2 * _dv1) >= 0.0F)
        Next
    End Sub

    ' ===========================================================================================
    ' API PÚBLICA: Recalcular N/T/B SOLO para la clausura afectada (dirty + sus triángulos)
    ' - Usa el cache (adjacencia + UV-derivs). Welding opcional (NO cacheado).
    ' ===========================================================================================
    ''' <summary>
    ''' Grupos de vértices COINCIDENTES en posición — las costuras. Réplica de <c>SortingMatcher</c>
    ''' (nifly KDMatcher.hpp:79-126): epsilon relativo a la escala del modelo, orden por X, y barrido
    ''' hacia adelante que corta apenas la diferencia en X llega al epsilon.
    '''
    ''' El epsilon es <c>EPSILON * 0.01 * escala</c> con <c>EPSILON = 1e-4</c> y escala = el mayor
    ''' |coordenada| de TODO el conjunto, o sea ~1e-6 relativo. NO es igualdad exacta de floats:
    ''' medirlo con igualdad exacta subestima cuántos vértices son de costura.
    ''' El <c>used</c> del canónico se indexa por POSICIÓN EN EL ORDEN, no por índice de vértice.
    ''' Cambiarlo altera qué grupos salen cuando hay tres o más coincidentes.
    ''' </summary>
    ''' <param name="grupoDe">Salida: por vértice, el índice de su grupo, o -1 si no tiene compañeros.</param>
    ''' <returns>Los grupos, cada uno con 2 o más miembros. Un vértice solo NO forma grupo, igual que
    ''' en el canónico, que sólo emite un matchset cuando encontró al menos una coincidencia.</returns>
    Public Shared Function ConstruyeGruposDeCostura(verts() As Vector3d, nVerts As Integer,
                                                    ByRef grupoDe() As Integer) As List(Of Integer())
        Dim sinCache As TBNCache = Nothing
        Return ConstruyeGruposDeCostura(verts, nVerts, grupoDe, sinCache, False)
    End Function

    ''' <summary>
    ''' Igual que la sobrecarga de arriba, pero manteniendo el ORDEN POR X entre llamadas en vez de
    ''' re-ordenar la malla entera en cada una.
    '''
    ''' Es EXACTAMENTE equivalente, y la razon es una propiedad del resultado y no del algoritmo:
    ''' el orden que produce esta funcion es un orden TOTAL —X ascendente, y los empates desempatados
    ''' por indice de vertice ascendente, cosa que el bloque de estabilizacion ya hacia explicita— asi
    ''' que hay UN solo orden valido y cualquier metodo que lo produzca da los mismos bytes. Sin esa
    ''' propiedad esto no se podria hacer.
    '''
    ''' POR QUE. MEDIDO con este arnés (`Tools\TbnPerfProbe`), sobre una rejilla de 22.201 vertices
    ''' con las X todas distintas —o sea sin la patologia de la rejilla perfecta, donde una columna
    ''' entera comparte X y el barrido se degrada— un arrastre de UN vertice costaba 1,76 ms, de los
    ''' cuales el 99,9 % era este agrupado, y de ese agrupado el 85-89 % era el <c>Array.Sort</c>. O
    ''' sea: el grueso del costo de mover un vertice era re-ordenar los otros 22.200, que no se
    ''' movieron.
    '''
    ''' La ley: los que no cambiaron de X conservan su orden relativo, asi que alcanza con sacarlos del
    ''' orden anterior, ordenar SOLO los que cambiaron y mezclar. Queda O(V + k·log k) en vez de
    ''' O(V·log V), con k = cuantos se movieron.
    ''' </summary>
    ''' <param name="cache">Donde vive el orden anterior. Con <c>usaCache</c> en False se ignora y se
    ''' ordena todo, que es el camino de la sobrecarga publica y el que corre la primera vez.</param>
    ''' <remarks>Es PUBLICA y no Friend para que el self-test pueda comparar las dos salidas
    ''' DIRECTAMENTE. Comparar el efecto rio abajo —las normales— no alcanza: un orden equivocado casi
    ''' nunca se ve en la salida (en el corpus real, 4 shapes de 12.789), asi que un test por el efecto
    ''' da verde con el orden roto. MEDIDO: rompiendo el desempate a proposito, el caso que comparaba
    ''' normales seguia en verde.</remarks>
    Public Shared Function ConstruyeGruposDeCostura(verts() As Vector3d, nVerts As Integer,
                                                    ByRef grupoDe() As Integer,
                                                    ByRef cache As TBNCache,
                                                    usaCache As Boolean) As List(Of Integer())
        Dim grupos As New List(Of Integer())
        grupoDe = New Integer(Math.Max(0, nVerts - 1)) {}
        For i = 0 To nVerts - 1
            grupoDe(i) = -1
        Next
        If nVerts <= 1 Then Return grupos

        Dim escala As Double = 0.0
        For i = 0 To nVerts - 1
            Dim v = verts(i)
            escala = Math.Max(escala, Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z))))
        Next
        ' NO es un numero elegido: es el epsilon del `SortingMatcher` del canonico, que es quien
        ' arma sus weld sets (nifly KDMatcher.hpp:89-92): `scale` = la mayor componente absoluta del
        ' conjunto de puntos, y `epsilon = EPSILON * 0.01f * scale` con `EPSILON = 0.0001f`
        ' (Object3d.hpp:16). Relativo a la escala de la malla, o sea independiente de las unidades.
        Dim eps As Double = 0.0001 * 0.01 * escala
        If eps <= 0.0 Then Return grupos

        Dim orden() As Integer = Nothing
        Dim clave() As Double = Nothing
        OrdenPorX(verts, nVerts, cache, usaCache, orden, clave)

        Dim usado(nVerts - 1) As Boolean      ' por POSICIÓN EN EL ORDEN, como el canónico
        For si = 0 To nVerts - 1
            If usado(si) Then Continue For
            Dim vi = orden(si)
            Dim actual As List(Of Integer) = Nothing
            For mi = si + 1 To nVerts - 1
                If clave(mi) - clave(si) >= eps Then Exit For
                If usado(mi) Then Continue For
                Dim vj = orden(mi)
                If Math.Abs(verts(vi).Y - verts(vj).Y) >= eps Then Continue For
                If Math.Abs(verts(vi).Z - verts(vj).Z) >= eps Then Continue For
                If actual Is Nothing Then
                    actual = New List(Of Integer)(4) From {vi}
                End If
                actual.Add(vj)
                usado(mi) = True
            Next
            If actual IsNot Nothing Then
                Dim idx = grupos.Count
                For Each v In actual
                    grupoDe(v) = idx
                Next
                grupos.Add(actual.ToArray())
            End If
        Next
        Return grupos
    End Function

    ''' <summary>
    ''' Los vértices ordenados por X, con los empates desempatados por índice ascendente — el orden
    ''' TOTAL del que depende la partición en grupos de costura. Devuelve también la clave (la X en
    ''' ese orden), que es lo que usa el barrido para cortar.
    '''
    ''' DESEMPATE EXPLÍCITO, y no es cosmético. <c>Array.Sort</c> es INESTABLE: ante claves iguales
    ''' su orden es un detalle de implementación del runtime, no parte del contrato, y la partición SÍ
    ''' depende de él — MEDIDO sobre 3.742 mallas fuente de los dos juegos (12.789 shapes): 4 shapes
    ''' cambian de agrupación según el desempate, hasta 128 vértices (<c>Shino Body_Suit_1.nif</c>).
    ''' Sin fijarlo, una actualización de .NET podía cambiar la salida de esos builds EN SILENCIO, y
    ''' esta app se distribuye.
    ''' NO acerca al canónico: <c>std::sort</c> tampoco define su desempate, así que la paridad
    ''' exacta en esas 4 shapes es inalcanzable por construcción. Lo que se gana es que lo NUESTRO sea
    ''' reproducible. Y es justamente esa reproducibilidad la que habilita el camino incremental de
    ''' abajo: con el orden fijado por contrato hay UNA sola respuesta correcta.
    ''' </summary>
    Private Shared Sub OrdenPorX(verts() As Vector3d, nVerts As Integer,
                                 ByRef cache As TBNCache, usaCache As Boolean,
                                 ByRef orden() As Integer, ByRef clave() As Double)
        ' ---- ¿se puede reusar el orden de la llamada anterior? ----
        Dim previo() As Integer = Nothing
        Dim xPrevia() As Double = Nothing
        If usaCache Then
            previo = cache.Orden_PorX
            xPrevia = cache.X_DelOrden
            If previo Is Nothing OrElse previo.Length <> nVerts OrElse
               xPrevia Is Nothing OrElse xPrevia.Length <> nVerts Then previo = Nothing
        End If

        ' ---- los que cambiaron de X desde la ultima vez ----
        ' Se detecta COMPARANDO, no confiando en `dirtyVertexIndices`: el conjunto de sucios dice
        ' que pidio recalcular el llamador, no que se movio de verdad, y hay caminos (el morph
        ' reescribe posiciones desde la base) donde no coinciden. Un falso negativo aca corrompe la
        ' particion en silencio, asi que la fuente de verdad son las posiciones mismas.
        Dim cambiados As List(Of Integer) = Nothing
        If previo IsNot Nothing Then
            cambiados = New List(Of Integer)()
            For v = 0 To nVerts - 1
                Dim x As Double = verts(v).X
                ' Un NaN en X nunca es "igual" a si mismo, asi que caeria en `cambiados` siempre; y
                ' peor, la mezcla de abajo compara con `<` y con NaN eso no ordena. Se cae al camino
                ' completo, que es lo que hacia antes.
                If Double.IsNaN(x) Then
                    cambiados = Nothing
                    Exit For
                End If
                If x <> xPrevia(v) Then cambiados.Add(v)
            Next
            ' Si se movio casi todo, mezclar no ahorra: la mezcla es O(V) ADEMAS del sort de los k.
            If cambiados IsNot Nothing AndAlso cambiados.Count * 2 > nVerts Then cambiados = Nothing
        End If

        orden = New Integer(nVerts - 1) {}
        clave = New Double(nVerts - 1) {}

        If cambiados Is Nothing Then
            ' ---- camino completo ----
            For i = 0 To nVerts - 1
                orden(i) = i
                clave(i) = verts(i).X
            Next
            ' Clave separada: Array.Sort(keys, items) es introsort sobre el array de claves y evita el
            ' delegado de comparación por par, que en 22.700 vértices es el grueso del costo.
            Array.Sort(clave, orden)
            Dim ini As Integer = 0
            While ini < nVerts
                Dim fin As Integer = ini + 1
                While fin < nVerts AndAlso clave(fin) = clave(ini)
                    fin += 1
                End While
                ' Se estabiliza sólo DENTRO de cada corrida de X idéntica, en vez de pasar un
                ' comparador al sort: el delegado por par sobre 22.700 vértices es justamente lo que
                ' la clave separada de arriba evita, y las corridas de X repetida son una minoría.
                If fin - ini > 1 Then Array.Sort(orden, ini, fin - ini)
                ini = fin
            End While
        Else
            ' ---- camino incremental: sacar los movidos, ordenarlos, y mezclar ----
            Dim movido(nVerts - 1) As Boolean
            For Each v In cambiados
                movido(v) = True
            Next

            ' Los quietos, en el orden anterior. Su X no cambio y su orden relativo tampoco, asi que
            ' la subsecuencia sigue ordenada por (X, indice) sin tocarla.
            Dim quietos(Math.Max(0, nVerts - cambiados.Count - 1)) As Integer
            Dim nQ As Integer = 0
            For k = 0 To nVerts - 1
                Dim v = previo(k)
                If Not movido(v) Then
                    quietos(nQ) = v
                    nQ += 1
                End If
            Next

            ' Los movidos, ordenados por la MISMA ley: X y, en el empate, indice.
            Dim mov(cambiados.Count - 1) As Integer
            Dim movX(cambiados.Count - 1) As Double
            For k = 0 To cambiados.Count - 1
                mov(k) = cambiados(k)
                movX(k) = verts(cambiados(k)).X
            Next
            Array.Sort(movX, mov)
            Dim ini2 As Integer = 0
            While ini2 < mov.Length
                Dim fin2 As Integer = ini2 + 1
                While fin2 < mov.Length AndAlso movX(fin2) = movX(ini2)
                    fin2 += 1
                End While
                If fin2 - ini2 > 1 Then Array.Sort(mov, ini2, fin2 - ini2)
                ini2 = fin2
            End While

            ' Mezcla estable con el comparador COMPLETO (X, y en el empate el indice): las dos
            ' entradas ya estan en ese orden, asi que la salida tambien lo esta. Es el mismo unico
            ' orden que produce el camino completo.
            Dim iq As Integer = 0, im As Integer = 0, o As Integer = 0
            While iq < nQ OrElse im < mov.Length
                Dim tomaQuieto As Boolean
                If iq >= nQ Then
                    tomaQuieto = False
                ElseIf im >= mov.Length Then
                    tomaQuieto = True
                Else
                    Dim xq As Double = xPrevia(quietos(iq))
                    Dim xm As Double = movX(im)
                    If xq < xm Then
                        tomaQuieto = True
                    ElseIf xm < xq Then
                        tomaQuieto = False
                    Else
                        tomaQuieto = quietos(iq) < mov(im)
                    End If
                End If
                Dim v As Integer
                If tomaQuieto Then
                    v = quietos(iq) : iq += 1
                Else
                    v = mov(im) : im += 1
                End If
                orden(o) = v
                clave(o) = verts(v).X
                o += 1
            End While
        End If

        ' ---- se guarda para la proxima ----
        If usaCache Then
            cache.Orden_PorX = orden
            Dim xs(nVerts - 1) As Double
            For v = 0 To nVerts - 1
                xs(v) = verts(v).X
            Next
            cache.X_DelOrden = xs
        End If
    End Sub

    ''' <summary>
    ''' Recalcula normales y base tangente. Replica la ley de nifly —el productor contra el que se
    ''' autoran los normal maps del ecosistema— en DOS PASES, igual que el canónico:
    '''
    '''   PASE A  <c>CalculateNormals</c> + <c>RecalcNormals</c> (Geometry.cpp): normal de cara SIN
    '''           normalizar (pondera por área) → normalizar por vértice → promediar las costuras →
    '''           respetar <c>LOCKEDNORM</c>.
    '''   PASE B  <c>CalcTangentSpace</c> (Geometry.cpp): lee la normal YA FINAL, acumula
    '''           <c>tdir</c>/<c>sdir</c> normalizados por cara, y cierra con doble Gram-Schmidt.
    '''
    ''' Los dos pases van SEPARADOS y en ese orden: la base tangente se ortogonaliza contra la
    ''' normal definitiva, no contra una intermedia. Fusionarlos obliga a rehacer las tangentes de las
    ''' costuras después, que es de donde salían las divergencias.
    '''
    ''' La normal contra la que se ortogonaliza pasa por la PRECISIÓN DEL FORMATO: en BSTriShape el
    ''' NIF la guarda en 3 bytes y <c>CalcTangentSpace</c> arranca con <c>UpdateRawNormals()</c>, que
    ''' la re-decodifica desde ahí. Medio paso de cuantización son 0,45°, y en el secundario de un
    ''' shell de UV espejado eso decide el signo. Ver <see cref="IShapeGeometry.NormalsAreByteQuantized"/>.
    '''
    ''' <b>Dominio.</b> El canónico recorre la malla entera. Acá se recorre un SUBCONJUNTO —los
    ''' vértices sucios y su clausura— que da el MISMO resultado porque el conjunto de triángulos que
    ''' alimenta el acumulado incluye todos los incidentes de cada vértice que se escribe. Si esa
    ''' propiedad se rompe, los vértices del borde quedan con un acumulado parcial.
    '''
    ''' Devuelve los vértices tocados ADEMÁS de los sucios; el llamador los marca para el render.
    ''' </summary>
    Public Shared Function RecalculateNormalsTangentsBitangents(ByRef geo As SkinnedGeometry, ByVal opts As TBNOptions) As List(Of Integer)
        Dim nVerts As Integer = geo.Vertices.Length
        If nVerts = 0 OrElse geo.dirtyVertexIndices Is Nothing OrElse geo.dirtyVertexIndices.Count = 0 Then
            Return New List(Of Integer)()
        End If
        Dim uvHalf As Boolean = geo.Geometry IsNot Nothing AndAlso geo.Geometry.UvsAreHalfPrecision
        If Not CacheEsCoherente(geo.CachedTBN, geo.Indices, nVerts, uvHalf) Then
            geo.CachedTBN = BuildTBNCache(geo.Uvs_Weight, geo.Indices, uvHalf)
        End If

        Dim v2tS = geo.CachedTBN.V2TStart
        Dim v2tD = geo.CachedTBN.V2TData
        Dim idxTri = geo.CachedTBN.Indices
        Dim triCount As Integer = geo.CachedTBN.TriCount
        ' `EpsilonPos` es una LONGITUD y el predicado del degenerado compara contra `LengthSquared`
        ' —sin raiz, que corre por triangulo—, asi que el cuadrado se hace UNA vez aca. Pasar la longitud
        ' cruda contra el cuadrado deja el umbral efectivo en `sqrt(eps)`: con el default 0 no se nota
        ' (0² = 0 y el predicado es `> 0` en los dos casos), y para cualquier otro valor el numero deja de
        ' significar lo que dice.
        ' SANEADO antes de elevar al cuadrado, y no es defensa decorativa: el cuadrado convierte
        ' entradas invalidas en un umbral que DESCARTA TODO en silencio, y la salida no seria un error
        ' sino una malla con la base tangente inventada por la rama degenerada en cada vertice.
        '   · NaN  -> `x > NaN` es False para todo x ⇒ cero aportes, sin excepcion que lo delate.
        '   · +Inf, o un valor grande que al elevarlo al cuadrado desborda a +Inf en Single ⇒ idem.
        '   · NEGATIVO ⇒ el cuadrado lo vuelve POSITIVO y grande: -1 pasaba de "no filtrar nada"
        '     (`LengthSquared > -1` es siempre cierto) a filtrar con umbral 1. Ese cambio de sentido lo
        '     introdujo el paso a longitud-al-cuadrado y hay que taparlo acá: la UI tiene el minimo en
        '     0, pero un config.json editado a mano no, y la migracion deja pasar los negativos.
        ' SON DOS POLITICAS DISTINTAS, a proposito, y la diferencia es si el numero es algo que el
        ' usuario pudo haber QUERIDO:
        '   · NaN, ±Inf y negativo -> 0 (el canonico, sin umbral). Ninguno de los tres es un umbral que
        '     alguien pueda pretender; son corrupcion del config o el efecto no deseado del cuadrado.
        '   · Un finito enorme (1e200) -> SATURA en `Single.MaxValue`, o sea descarta todo. NO cae a 0.
        '     Es lo que el usuario pidio, y ademas es lo que ya hacia ANTES de este cambio: `CSng(1e200)`
        '     daba +Inf y `LengthSquared > Inf` descartaba todo igual. Mandarlo a 0 seria ignorar en
        '     silencio un numero que alguien escribio, que es peor que obedecerlo.
        ' La saturacion existe para no ESCRIBIR un infinito en el umbral, no para acotar el efecto.
        Dim epsPosLong As Double = opts.EpsilonPos
        If Double.IsNaN(epsPosLong) OrElse Double.IsInfinity(epsPosLong) OrElse epsPosLong <= 0.0 Then epsPosLong = 0.0
        Dim epsPosSq As Single = CSng(Math.Min(epsPosLong * epsPosLong, CDbl(Single.MaxValue)))
        Dim cuantizaNormal As Boolean = geo.Geometry IsNot Nothing AndAlso geo.Geometry.NormalsAreByteQuantized

        ' ---- welding: agrupación propia de WM (posición [+ UV]), opt-in ----
        Dim masterOf() As Integer = Nothing
        Dim membersOf As Dictionary(Of Integer, List(Of Integer)) = Nothing
        If opts.EnableWelding Then
            BuildWeldGroups(geo, opts.WeldPosEpsilon, opts.WeldUVEpsilon, opts.WeldByPositionOnly, masterOf, membersOf, geo.CachedTBN)
        End If

        ' ---- grupos de costura: posición coincidente, réplica de SortingMatcher ----
        Dim grupoDe() As Integer = Nothing
        Dim gruposCostura As List(Of Integer()) = Nothing
        Dim suaviza As Boolean = opts.SmoothSeamNormals AndAlso Not opts.KeepExistingNormals
        If suaviza Then
            gruposCostura = ConstruyeGruposDeCostura(geo.Vertices, nVerts, grupoDe, geo.CachedTBN, True)
            suaviza = gruposCostura.Count > 0
        End If

        ' ================= DOMINIO =================
        ' W = vértices a escribir. Arranca en los sucios; su cola es lo que se devuelve al llamador.
        ' `slotDe` mapea vertice -> ranura en los acumuladores, que se dimensionan a la CLAUSURA y
        ' no a la malla. Reemplaza al camino sparse (diccionarios) sin duplicar la ley: mismo codigo
        ' para un arrastre de 200 vertices que para un build entero.
        ' Los buffers salen del cache y la validez va por SELLO DE CORRIDA (ver `TBNCache`): no se
        ' asigna ni se inicializa nada del tamaño de la malla en cada llamada. `slotDe(v)` vale sólo si
        ' `slotSello(v) = corrida`, así que una entrada de una llamada anterior no coincide y equivale
        ' al -1 que antes había que ir a escribir vértice por vértice.
        Dim corrida As Integer = ProximaCorrida(geo.CachedTBN)
        Dim slotDe() As Integer = geo.CachedTBN.Scratch_SlotDe
        Dim slotSello() As Integer = geo.CachedTBN.Scratch_SlotSello
        Dim vertArr() As Integer = geo.CachedTBN.Scratch_VertArr
        Dim nAff As Integer = 0
        For Each vi In geo.dirtyVertexIndices
            AgregaConRanura(vi, slotDe, slotSello, corrida, vertArr, nAff)
        Next
        Dim nDirty As Integer = nAff

        ' Los vértices de todo triángulo incidente a un sucio también cambian de base.
        Dim nSemilla As Integer = nAff
        For kv = 0 To nSemilla - 1
            Dim vi = vertArr(kv)
            For k = v2tS(vi) To v2tS(vi + 1) - 1
                Dim t = v2tD(k) >> 2
                AgregaConRanura(CInt(idxTri(3 * t + 0)), slotDe, slotSello, corrida, vertArr, nAff)
                AgregaConRanura(CInt(idxTri(3 * t + 1)), slotDe, slotSello, corrida, vertArr, nAff)
                AgregaConRanura(CInt(idxTri(3 * t + 2)), slotDe, slotSello, corrida, vertArr, nAff)
            Next
        Next

        ' Compañeros de costura y de weld: comparten normal o base, así que entran al conjunto de
        ' escritura.
        ' HASTA PUNTO FIJO, y con TODOS los miembros del grupo de weld — no sólo el maestro.
        ' `FusionaGrupos` suma únicamente a los compañeros QUE TIENEN RANURA y saltea a los que
        ' quedaron afuera, así que un grupo partido entre dentro y fuera de la clausura producía una
        ' base fusionada distinta de la que da el recálculo de malla entera: el resultado dependía de
        ' cuántos compañeros hubiera arrastrado el gesto del usuario. Es justo la propiedad de dominio
        ' que declara la doc de esta función («el subconjunto da el MISMO resultado»), y con welding
        ' puesto no se cumplía. Traer el grupo entero la restablece.
        ' El punto fijo hace falta porque un compañero recién agregado pertenece a su propio grupo de
        ' costura, que también hay que traer; una sola pasada dejaba esa segunda capa afuera.
        ' Ampliar la clausura NO cambia lo que se escribe en los que ya estaban: el acumulado de un
        ' vértice sale de SUS triángulos incidentes, que ya estaban todos incluidos. Sólo agrega
        ' vértices más, calculados bien.
        Dim kExp As Integer = 0
        While kExp < nAff
            Dim vi = vertArr(kExp)
            If suaviza AndAlso grupoDe(vi) >= 0 Then
                For Each vj In gruposCostura(grupoDe(vi))
                    AgregaConRanura(vj, slotDe, slotSello, corrida, vertArr, nAff)
                Next
            End If
            If masterOf IsNot Nothing Then
                Dim m As Integer = masterOf(vi)
                AgregaConRanura(m, slotDe, slotSello, corrida, vertArr, nAff)
                Dim hermanos As List(Of Integer) = Nothing
                If membersOf IsNot Nothing AndAlso membersOf.TryGetValue(m, hermanos) AndAlso hermanos IsNot Nothing Then
                    For Each vk In hermanos
                        AgregaConRanura(vk, slotDe, slotSello, corrida, vertArr, nAff)
                    Next
                End If
            End If
            kExp += 1
        End While

        ' T = triángulos que alimentan el acumulado: TODOS los incidentes de TODO vértice de W.
        ' Es la condición que hace que el subconjunto dé lo mismo que la malla entera. Sin esto un
        ' vértice del borde de W recibe sólo parte de sus caras y su base no es la de la malla.
        ' Un SOLO sello para los triángulos: `triSello(t) = corrida` significa a la vez «t ya está en
        ' `triArr`» —lo que antes hacía el array `triVisto`— y «`slotTri(t)` es de esta corrida». Son la
        ' misma condición: `slotTri` se llena exactamente para los triángulos de `triArr`.
        Dim triSello() As Integer = geo.CachedTBN.Scratch_TriSello
        Dim triArr() As Integer = geo.CachedTBN.Scratch_TriArr
        Dim nTris As Integer = 0
        For kv = 0 To nAff - 1
            Dim vi = vertArr(kv)
            For k = v2tS(vi) To v2tS(vi + 1) - 1
                AgregaUnaVez(v2tD(k) >> 2, triSello, corrida, triArr, nTris)
            Next
        Next
        If nTris = 0 Then Return AdicionalesDe(vertArr, nDirty, nAff)
        ' Orden creciente de triángulo, que es el del canónico. Con el acumulador en Single la suma no
        ' es asociativa, así que el orden es parte de la ley.
        Array.Sort(triArr, 0, nTris)

        ' ================= ACUMULACION =================
        ' UN SOLO recorrido de triangulos para los tres canales. Los dos pases del canonico son
        ' independientes POR TRIANGULO —lo que tiene que ir en orden es la FINALIZACION: primero la
        ' normal definitiva (con suavizado de costura) y recien despues la base contra ella— asi que
        ' fusionar la acumulacion no cambia el resultado y evita recorrer los triangulos y convertir
        ' las posiciones dos veces.
        ' Los acumuladores van indexados por RANURA y dimensionados a la clausura (`nAff`), no a la
        ' malla: un arrastre que toca 200 vertices no aloca 22.000 entradas.
        Dim accN(Math.Max(0, nAff - 1)) As Vector3
        Dim tan1(Math.Max(0, nAff - 1)) As Vector3   ' tdir -> PRIMARIO  (campo Tangent del NIF)
        Dim tan2(Math.Max(0, nAff - 1)) As Vector3   ' sdir -> SECUNDARIO (campo Bitangent del NIF)
        Dim quiral(Math.Max(0, nAff - 1)) As Single  ' signo del determinante UV, sumado por vertice
        Acumula(triArr, nTris, idxTri, geo.Vertices, geo.CachedTBN, epsPosSq,
                vertArr, nAff, corrida, accN, tan1, tan2, quiral)

        ' ================= PASE A: NORMALES =================
        Dim norms() As Vector3 = geo.Normals
        If Not opts.KeepExistingNormals Then
            ' El weld NO promedia normales. Dos vertices co-locados a los dos lados de una arista
            ' dura tienen normales opuestas, y promediarlas sin condicion arruina las dos: medido,
            ' 40 grados de error en los vertices de costura contra BodySlide. El promedio de normales
            ' es el del canonico y lleva umbral angular — lo hace el suavizado de costura de abajo.

            ' ORDEN DEL CANONICO: se recalculan TODAS las normales —incluidas las bloqueadas—, se
            ' suaviza con esas, y recien al final se restaura la del archivo en las bloqueadas
            ' (nifly `CalculateNormals`, Geometry.cpp:891-968: trabaja sobre `tnorms` completo y copia
            ' a `outNorms` solo los indices no bloqueados). Saltear la bloqueada ANTES hacia que
            ' aportara al promedio de costura su normal VIEJA, y sus companeros no bloqueados salian
            ' con otra normal que la de BodySlide.
            Dim bloqueadas = NormalesBloqueadas(geo)
            Dim normalOriginal As Dictionary(Of Integer, Vector3) = Nothing
            If bloqueadas IsNot Nothing AndAlso bloqueadas.Count > 0 Then
                normalOriginal = New Dictionary(Of Integer, Vector3)(bloqueadas.Count)
            End If
            For kv = 0 To nAff - 1
                Dim vi = vertArr(kv)
                If normalOriginal IsNot Nothing AndAlso bloqueadas.Contains(vi) Then
                    If Not normalOriginal.ContainsKey(vi) Then normalOriginal(vi) = norms(vi)
                End If
                norms(vi) = NormalizaComoNifly(accN(kv))
            Next

            If suaviza Then
                ' Promedio de costura: cada miembro suma los que estén a MENOS del umbral. Los dos
                ' bucles van separados — el promedio se calcula contra las normales sin suavizar.
                Dim umbral As Single = CSng(Math.Cos(Math.Max(0.0, opts.SmoothSeamNormalsAngle) * Math.PI / 180.0))
                Dim grupoVisto(Math.Max(0, gruposCostura.Count - 1)) As Boolean
                For kv = 0 To nAff - 1
                    Dim g = grupoDe(vertArr(kv))
                    If g < 0 OrElse grupoVisto(g) Then Continue For
                    grupoVisto(g) = True
                    Dim miembros = gruposCostura(g)
                    Dim suavizadas(miembros.Length - 1) As Vector3
                    For j = 0 To miembros.Length - 1
                        Dim nj = norms(miembros(j))
                        Dim sn = nj
                        Dim lj As Single = nj.Length
                        For k = 0 To miembros.Length - 1
                            If k = j Then Continue For
                            Dim nk = norms(miembros(k))
                            Dim lk As Single = nk.Length
                            If lj <= 0.0F OrElse lk <= 0.0F Then Continue For
                            If Vector3.Dot(nj, nk) / (lj * lk) <= umbral Then Continue For
                            sn += nk
                        Next
                        suavizadas(j) = NormalizaComoNifly(sn)
                    Next
                    For j = 0 To miembros.Length - 1
                        norms(miembros(j)) = suavizadas(j)
                    Next
                Next
            End If

            ' La normal del archivo vuelve a su lugar recien aca: participo del promedio pero no se
            ' reescribe, que es exactamente lo que hace el canonico.
            If normalOriginal IsNot Nothing Then
                For Each par In normalOriginal
                    norms(par.Key) = par.Value
                Next
            End If
        End If

        ' ================= PASE B: BASE TANGENTE =================
        ' El weld comparte la BASE TANGENTE del grupo: es su razon de existir —que los vertices
        ' co-locados tengan un solo marco— y es lo que el suavizado de costura NO hace. El canonico no
        ' tiene este mecanismo (ni BSTriShape::CalcTangentSpace ni Mesh::CalcTangentSpace usan weld
        ' sets), asi que es una opcion propia y por eso viene apagada.
        ' Compartir la BASE es seguro donde promediar la NORMAL no lo era: la base se reortogonaliza
        ' despues contra la normal de cada miembro, asi que un miembro con normal distinta no hereda
        ' un marco torcido.
        ' El welding comparte la base tangente con el MISMO umbral angular que el suavizado de
        ' costura, y por la misma razon. Antes no tenia ninguno: fusionaba todo vertice co-locado,
        ' incluida la geometria de DOBLE CARA. MEDIDO en `CBBE Body`: de sus 1.191 grupos de posicion
        ' identica, 964 son costuras de UV reales (normales a menos de 60 grados), 99 son aristas
        ' duras y 128 son doble cara (normales a mas de 120). En esos 227 los dos lados tienen
        ' tangentes GENUINAMENTE distintas y forzarles un marco comun rompe el normal map de uno.
        ' La QUIRALIDAD se fusiona con los acumulados, en la misma pasada y con el mismo criterio de
        ' pertenencia. Es lo que dice de que lado apunta el secundario cuando hay que reconstruirlo
        ' (`SecundarioDeLaBase`), o sea que pertenece al mismo modelo de datos que `tan1`/`tan2`: si el
        ' acumulado es la suma del grupo, el signo del determinante tambien tiene que serlo. Quedandose
        ' con la quiralidad LOCAL, dos miembros con el MISMO acumulado fusionado reconstruian el
        ' secundario con signos OPUESTOS —el caso tipico es la costura de espejo, donde los dos lados
        ' tienen det de signo contrario— y eso invierte el canal verde del normal map de uno de los dos.
        ' El chequeo de ortogonalidad no lo puede ver: es ciego al signo.
        ' En una costura de espejo la suma fusionada da ~0, que es la respuesta honesta —los datos no
        ' determinan un lado— y ahi `SecundarioDeLaBase` cae a la convencion del formato, igual para
        ' todos los miembros.
        Dim cosUmbralWeld As Single = CSng(Math.Cos(Math.Max(0.0, opts.SmoothSeamNormalsAngle) * Math.PI / 180.0))
        FusionaGrupos(tan1, tan2, quiral, vertArr, nAff, slotDe, slotSello, corrida,
                      masterOf, membersOf, norms, cosUmbralWeld)

        ' PROBADO Y DESCARTADO paralelizar este pase. Es correcto hacerlo —cada iteración lee
        ' `norms(vi)`, que el pase A ya dejó escrito entero, y escribe SÓLO su propio `vi`, único en la
        ' clausura— y de hecho salió BYTE-IDÉNTICO al serial. Pero no gana: MEDIDO sobre el build de
        ' `CBBE Body` en FO4, 6 corridas descartando la primera, 945 ms de mediana en serie contra
        ' 966 ms con `Parallel.ForEach`. El trabajo por vértice es demasiado barato para pagar el
        ' particionado. No re-intentarlo sin cambiar antes el costo por vértice.
        Dim salidaT = geo.Tangents
        Dim salidaB = geo.Bitangents
        For kv = 0 To nAff - 1
            BaseDeUnVertice(kv, vertArr, norms, tan1, tan2, quiral, opts, cuantizaNormal, salidaT, salidaB)
        Next

        Return AdicionalesDe(vertArr, nDirty, nAff)
    End Function

    ''' <summary>
    ''' La base tangente de UNA ranura de la clausura, escrita en la geometría. Sale del cuerpo del
    ''' bucle para que el pase pueda paralelizarse sin duplicar la ley en dos lados.
    ''' </summary>
    Private Shared Sub BaseDeUnVertice(kv As Integer, vertArr() As Integer, norms() As Vector3,
                                       tan1() As Vector3, tan2() As Vector3, quiral() As Single,
                                       opts As TBNOptions, cuantizaNormal As Boolean,
                                       salidaT() As Vector3, salidaB() As Vector3)
        Dim vi = vertArr(kv)
        ' Misma regla que la red final de InjectNormalsToTrishape, y por la misma razon: el NaN no
        ' es un valor del canonico y se repara siempre; la normal NULA si lo es —el canonico la
        ' ortogonaliza contra cero, o sea no la ortogonaliza— asi que sustituirla es la mejora de
        ' WM y la gobierna `RepairNaNs`.
        ' El umbral es el CERO exacto, no `epsPos`: `EpsilonPos` es el umbral de TRIANGULO
        ' degenerado y usarlo aca mezclaba dos magnitudes distintas bajo un mismo numero.
        Dim nUso As Vector3 = norms(vi)
        If HasNaN(nUso) Then nUso = New Vector3(0, 0, 1)
        If opts.RepairNaNs AndAlso nUso.LengthSquared <= 0.0F Then nUso = New Vector3(0, 0, 1)
        Dim T As Vector3, B As Vector3
        BaseTangenteDeVertice(NormalParaTBN(nUso, cuantizaNormal), tan2(kv), tan1(kv), quiral(kv), opts, T, B)
        salidaT(vi) = T          ' secundario
        salidaB(vi) = B          ' primario
    End Sub

    ''' <summary>
    ''' Acumulado por vértice de los tres canales, en UN solo recorrido de triángulos: normal de cara
    ''' (sin normalizar, o sea ponderada por área) y base tangente normalizada por cara.
    '''
    ''' GATHER, no scatter: la fase 1 calcula por triángulo —cada iteración escribe sólo en su
    ''' índice— y la fase 2 suma por vértice recorriendo su lista CSR, que está en orden creciente de
    ''' triángulo. Ese es el mismo orden en que sumaría un scatter secuencial sobre <c>triArr</c>
    ''' ordenado, y la suma en Single no es asociativa, así que el orden es parte de la ley. Además
    ''' cada vértice lo escribe una sola iteración, así que el resultado no depende de cuántos núcleos
    ''' tenga la máquina — requisito, porque la app se distribuye.
    ''' </summary>
    Private Shared Sub Acumula(triArr() As Integer, nTris As Integer, idxTri As UInteger(),
                               verts As Vector3d(), cache As TBNCache,
                               epsPosSq As Single, vertArr() As Integer, nAff As Integer,
                               corrida As Integer,
                               accN As Vector3(), tan1 As Vector3(), tan2 As Vector3(),
                               quiral As Single())
        ' Fase 1 — por triángulo. Los tres vectores van juntos: la fase 2 los lee de un bloque
        ' contiguo en vez de indexar tres arrays.
        Dim cara((nTris * 3) - 1) As Vector3
        ' Quiralidad de la parametrizacion UV, por cara: +1 o -1. Se acumula por vertice porque es
        ' lo UNICO que dice de que lado apunta el secundario cuando hay que reconstruirlo, y el
        ' residuo del Gram-Schmidt justamente ya no lo dice. Algebra: con `sdir` y `tdir` como los
        ' define el canonico, `sdir x tdir = det * (e1 x e2)`, o sea que el signo del determinante UV
        ' ES la quiralidad del marco respecto de la normal de cara.
        Dim signo(Math.Max(0, nTris - 1)) As Single
        ' Del cache y con sello: `triSello(t) = corrida` ya marca exactamente a los triangulos de
        ' `triArr`, asi que `slotTri` no necesita ni asignacion ni el barrido completo a -1 que habia
        ' aca — O(triangulos de la MALLA) en cada llamada, para despues escribir O(triangulos de la
        ' clausura) posiciones.
        Dim slotTri() As Integer = cache.Scratch_SlotTri
        Dim triSello() As Integer = cache.Scratch_TriSello
        For k = 0 To nTris - 1
            slotTri(triArr(k)) = k
        Next

        Dim du1 = cache.Tri_du1, dv1 = cache.Tri_dv1, du2 = cache.Tri_du2, dv2 = cache.Tri_dv2
        Dim detNeg = cache.Tri_DetNeg
        Dim porCara As Action(Of Tuple(Of Integer, Integer)) =
            Sub(rango As Tuple(Of Integer, Integer))
                For k = rango.Item1 To rango.Item2 - 1
                    Dim t = triArr(k)
                    Dim i0 = CInt(idxTri(3 * t + 0)), i1 = CInt(idxTri(3 * t + 1)), i2 = CInt(idxTri(3 * t + 2))
                    Dim p0 = PosParaTBN(verts(i0))
                    Dim e1 = PosParaTBN(verts(i1)) - p0
                    Dim e2 = PosParaTBN(verts(i2)) - p0
                    Dim sdir As Vector3, tdir As Vector3
                    ComputeFaceTB(e1, e2, du1(t), dv1(t), du2(t), dv2(t), detNeg(t), epsPosSq, sdir, tdir)
                    Dim fb = k * 3
                    cara(fb) = Vector3.Cross(e1, e2)   ' normal de cara, SIN normalizar
                    cara(fb + 1) = tdir
                    cara(fb + 2) = sdir
                    signo(k) = If(detNeg(t), -1.0F, 1.0F)
                Next
            End Sub

        Dim v2tS = cache.V2TStart, v2tD = cache.V2TData
        Dim porVertice As Action(Of Tuple(Of Integer, Integer)) =
            Sub(rango As Tuple(Of Integer, Integer))
                For kv = rango.Item1 To rango.Item2 - 1
                    Dim vi = vertArr(kv)
                    Dim aN As Vector3 = Vector3.Zero, a1 As Vector3 = Vector3.Zero, a2 As Vector3 = Vector3.Zero
                    Dim aQ As Single = 0.0F
                    For k = v2tS(vi) To v2tS(vi + 1) - 1
                        Dim t = v2tD(k) >> 2
                        ' El sello es lo que dice si `slotTri(t)` es de esta corrida. Sin el, una
                        ' entrada de una llamada anterior es un indice perfectamente valido apuntando
                        ' al triangulo equivocado.
                        If triSello(t) <> corrida Then Continue For
                        Dim fb = slotTri(t) * 3
                        aN += cara(fb)
                        a1 += cara(fb + 1)
                        a2 += cara(fb + 2)
                        aQ += signo(slotTri(t))
                    Next
                    accN(kv) = aN : tan1(kv) = a1 : tan2(kv) = a2 : quiral(kv) = aQ
                Next
            End Sub

        ' PROBADO Y DESCARTADO paralelizar esta acumulacion. Es CORRECTO hacerlo —el gather de
        ' arriba hace que cada vertice lo escriba una sola iteracion— pero NO PAGA. MEDIDO con
        ' `Tools\TbnPerfProbe` (modo `escala`), dos corridas independientes, minimo de 11 repeticiones,
        ' comparando una libreria forzada a serie contra una forzada a paralelo sobre las MISMAS
        ' mallas, en un equipo de 12 nucleos — cociente paralelo/serie:
        '
        '     8 tri  4,2x     288 tri  1,5x     4.608 tri  0,87x
        '    18 tri  3,1x     512 tri  1,3x     8.192 tri  0,83x
        '    72 tri  1,3x   1.152 tri  1,00x   18.432 tri  1,61x
        '   128 tri  1,4x   2.048 tri  0,94x   32.768 tri  1,12x
        '
        ' O sea: gana como mucho un 17 %, y solo en la banda de 2k a 8k triangulos; afuera pierde, y
        ' en los cierres CHICOS —el caso interactivo, un arrastre que toca unos pocos vertices— pierde
        ' hasta 4,2x. El umbral que habia (`nTris >= ProcessorCount`, o sea 12 triangulos) mandaba por
        ' el camino paralelo practicamente a toda malla, incluida esa zona.
        '
        ' Y no se reemplaza por un umbral: cualquier constante que separe esa banda estaria
        ' calibrada a ESTE equipo, y la app se distribuye (ver 00-reglas-app-distribuida). La unica
        ' respuesta que no depende del equipo es no paralelizar. Es la misma conclusion —y por la misma
        ' razon, trabajo por item demasiado barato para pagar el particionado— a la que se llego
        ' midiendo el PASE B, que tambien salio correcto y tambien se revirtio.
        ' Para re-intentarlo hay que cambiar antes el costo por item, no el umbral.
        porCara(Tuple.Create(0, nTris))
        porVertice(Tuple.Create(0, nAff))
    End Sub

    ''' <summary>
    ''' Suma los acumuladores de cada grupo de weld en su maestro y le da a todos los miembros ese
    ''' mismo valor.
    '''
    ''' El welding es una operacion sobre los ACUMULADORES, no un retoque posterior: si al miembro
    ''' se le pisa la base DESPUES de armarla, queda ortogonalizada contra otra normal — o sea torcida.
    ''' Se aplica a `tan1`, `tan2` y `quiral` —los tres canales de la BASE TANGENTE— y NUNCA a
    ''' `accN`: promediar normales aca arruinaria las aristas duras, y el promedio de normales con
    ''' umbral angular es otra cosa y la hace el suavizado de costura. El canonico no aplica weld sets a
    ''' la base tangente —sus weld sets alimentan unicamente `Mesh::SmoothNormals`—, asi que esto es una
    ''' funcion propia de WM y por eso viene apagada.
    ''' Los tres van en UNA pasada, no en tres llamadas. El criterio de pertenencia —el umbral
    ''' angular contra la normal del compañero— es el MISMO para los tres, y con una llamada por canal
    ''' ese predicado se evaluaba una vez por canal (tres veces el mismo producto punto y la misma
    ''' division) y, peor, podia quedar un canal sin fusionar sin que nada lo delatara: fue exactamente
    ''' lo que paso con `quiral`.
    ''' </summary>
    ''' <remarks>La ranura se lee con <see cref="RanuraDe"/> y NO con <c>slotDe(v) &lt; 0</c>: los
    ''' buffers se reusan entre llamadas, así que una entrada de una corrida anterior tiene un número
    ''' perfectamente válido y apuntaría a la ranura de OTRO vértice.</remarks>
    Private Shared Sub FusionaGrupos(tan1() As Vector3, tan2() As Vector3, quiral() As Single,
                                     vertArr() As Integer, nAff As Integer,
                                     slotDe() As Integer, slotSello() As Integer, corrida As Integer,
                                     masterOf() As Integer,
                                     membersOf As Dictionary(Of Integer, List(Of Integer)),
                                     norms() As Vector3, cosUmbral As Single)
        If membersOf Is Nothing Then Exit Sub
        ' Se recorre el GRUPO, no los afectados, para no sumar dos veces si dos miembros del mismo
        ' grupo estan en la clausura. Un miembro fuera de la clausura no tiene ranura y no aporta.
        Dim hecho As New HashSet(Of Integer)()
        For kv = 0 To nAff - 1
            Dim m = If(masterOf Is Nothing, vertArr(kv), masterOf(vertArr(kv)))
            If Not hecho.Add(m) Then Continue For
            Dim members As List(Of Integer) = Nothing
            If Not membersOf.TryGetValue(m, members) OrElse members Is Nothing Then Continue For
            ' POR MIEMBRO, no una suma unica para todo el grupo: cada uno acumula SOLO a los
            ' companeros cuya normal esta dentro del umbral, exactamente igual que el suavizado de
            ' costura. Con una suma unica, un vertice de doble cara recibia tambien el aporte del
            ' lado opuesto —que apunta al reves— y su marco salia del promedio de dos superficies
            ' distintas. Asi una arista dura sigue dura, que es lo que preserva el normal map.
            Dim fus1(members.Count - 1) As Vector3
            Dim fus2(members.Count - 1) As Vector3
            Dim fusQ(members.Count - 1) As Single
            For j = 0 To members.Count - 1
                Dim sj = RanuraDe(members(j), slotDe, slotSello, corrida)
                If sj < 0 Then Continue For
                Dim nj = norms(members(j))
                Dim lj As Single = nj.Length
                Dim suma1 As Vector3 = tan1(sj)
                Dim suma2 As Vector3 = tan2(sj)
                Dim sumaQ As Single = quiral(sj)
                For k = 0 To members.Count - 1
                    If k = j Then Continue For
                    Dim sk = RanuraDe(members(k), slotDe, slotSello, corrida)
                    If sk < 0 Then Continue For
                    Dim nk = norms(members(k))
                    Dim lk As Single = nk.Length
                    If lj <= 0.0F OrElse lk <= 0.0F Then Continue For
                    If Vector3.Dot(nj, nk) / (lj * lk) <= cosUmbral Then Continue For
                    suma1 += tan1(sk)
                    suma2 += tan2(sk)
                    sumaQ += quiral(sk)
                Next
                fus1(j) = suma1 : fus2(j) = suma2 : fusQ(j) = sumaQ
            Next
            For j = 0 To members.Count - 1
                Dim sj = RanuraDe(members(j), slotDe, slotSello, corrida)
                If sj >= 0 Then
                    tan1(sj) = fus1(j) : tan2(sj) = fus2(j) : quiral(sj) = fusQ(j)
                End If
            Next
        Next
    End Sub

    ''' <summary>La ranura de un vértice en los acumuladores de ESTA corrida, o -1 si no está en la
    ''' clausura. El sello es lo que distingue «ranura 0» de «entrada de una llamada anterior».</summary>
    Private Shared Function RanuraDe(v As Integer, slotDe() As Integer, sello() As Integer,
                                     corrida As Integer) As Integer
        If v < 0 OrElse v >= slotDe.Length Then Return -1
        If sello(v) <> corrida Then Return -1
        Return slotDe(v)
    End Function

    ''' <summary>
    ''' Índices con <c>LOCKEDNORM</c>: el canónico deja su normal intacta
    ''' (<c>NifFile::CalcNormalsForShape</c>). Nothing si la shape no trae ese extra data.
    ''' </summary>
    Private Shared Function NormalesBloqueadas(ByRef geo As SkinnedGeometry) As HashSet(Of Integer)
        Return geo.Geometry?.GetLockedNormalIndices()
    End Function

    ''' <summary>
    ''' Los vertices tocados que NO venian sucios: la COLA de la clausura (<c>[nDirty, nAff)</c>). No
    ''' hace falta un conjunto aparte — el array de la clausura arranca justamente con los sucios, en
    ''' orden, asi que todo lo que sigue es lo agregado.
    '''
    ''' Ya NO se suma el conjunto del welding, y no es una optimizacion: era INCORRECTO. Ese
    ''' conjunto lo arma <c>BuildWeldGroups</c> sobre la MALLA ENTERA —todo vertice que entro a un
    ''' grupo, este o no en la clausura— asi que se devolvian como "tocados" miles de vertices que esta
    ''' llamada no escribio. El llamador los marca sucios, con dos efectos: sube al render vertices que
    ''' no cambiaron, y —lo caro— la clausura de la llamada SIGUIENTE arranca conteniendo medio mesh,
    ''' o sea que con welding puesto el camino incremental dejaba de ser incremental a partir del
    ''' segundo tick. Existia para tapar que la clausura no traia a los companeros de weld; ahora los
    ''' trae (ver la expansion a punto fijo), asi que la cola YA ES el conjunto exacto de lo escrito
    ''' de mas.
    ''' </summary>
    Private Shared Function AdicionalesDe(vertArr() As Integer,
                                          nDirty As Integer, nAff As Integer) As List(Of Integer)
        Dim res As New List(Of Integer)(Math.Max(0, nAff - nDirty))
        If vertArr IsNot Nothing Then
            For k = nDirty To nAff - 1
                res.Add(vertArr(k))
            Next
        End If
        Return res
    End Function

    ''' <summary>
    ''' Agrega <paramref name="v"/> al conjunto compacto (sellos + array + contador) si no estaba, y
    ''' descarta los indices fuera de rango. Reemplaza a <c>HashSet(Of Integer).Add</c> conservando el
    ''' ORDEN DE INSERCION, que es lo que hace que el camino secuencial sume en el mismo orden.
    ''' </summary>
    Private Shared Sub AgregaUnaVez(v As Integer, sello As Integer(), corrida As Integer,
                                    arr As Integer(), ByRef n As Integer)
        If v < 0 OrElse v >= sello.Length Then Exit Sub
        If sello(v) = corrida Then Exit Sub
        sello(v) = corrida
        arr(n) = v
        n += 1
    End Sub

    ''' <summary>Igual que <see cref="AgregaUnaVez"/> pero sobre un mapa vertice -> ranura: la ranura
    ''' queda registrada para indexar los acumuladores, y vale sólo mientras el sello coincida con la
    ''' corrida.</summary>
    Private Shared Sub AgregaConRanura(v As Integer, slotDe As Integer(), sello As Integer(),
                                       corrida As Integer, arr As Integer(), ByRef n As Integer)
        If v < 0 OrElse v >= slotDe.Length Then Exit Sub
        If sello(v) = corrida Then Exit Sub
        sello(v) = corrida
        slotDe(v) = n
        arr(n) = v
        n += 1
    End Sub

    ''' <summary>
    ''' El número de corrida de esta llamada, y la garantía de que ningún sello viejo lo iguale.
    '''
    ''' Arranca en 1, no en 0: un array de sellos recién asignado está TODO en cero, así que con
    ''' corrida 0 cada vértice de la malla se vería como «ya agregado en esta corrida» y la clausura
    ''' saldría vacía.
    ''' Y al desbordar se limpian los sellos. Es inalcanzable en la práctica —2^31 recálculos de la
    ''' misma malla sin recargarla— pero el modo de fallar sería un sello viejo coincidiendo con la
    ''' corrida nueva, o sea vértices fantasma en la clausura, que es indepurable. Limpiar cuesta una
    ''' pasada cada 2.000 millones de llamadas.
    ''' </summary>
    Private Shared Function ProximaCorrida(ByRef c As TBNCache) As Integer
        If c.Corrida >= Integer.MaxValue - 1 Then
            Array.Clear(c.Scratch_SlotSello, 0, c.Scratch_SlotSello.Length)
            Array.Clear(c.Scratch_TriSello, 0, c.Scratch_TriSello.Length)
            c.Corrida = 0
        End If
        c.Corrida += 1
        Return c.Corrida
    End Function

    ' -----------------------
    ' Utilitarios privados
    ' -----------------------

    ' Welding lógico por posición+UV con tolerancias (NO cacheado)
    ''' <remarks>Es un Sub. Devolvia el conjunto de los vertices que entraron a un grupo y ese
    ''' conjunto se devolvia al llamador como "vertices tocados", que era falso: se arma sobre la malla
    ''' ENTERA y no sobre la clausura. Ver <see cref="AdicionalesDe"/>. Sin ese consumidor, armarlo era
    ''' un HashSet del tamaño de la malla por llamada, para nada.</remarks>
    Private Shared Sub BuildWeldGroups(ByRef geo As SkinnedGeometry, ByVal weldPosEpsOrig As Double, ByVal weldUVEps As Double, ByVal byPosOnly As Boolean,
                                       ByRef masterOf() As Integer, ByRef membersOf As Dictionary(Of Integer, List(Of Integer)),
                                       ByRef cache As TBNCache)
        Dim n As Integer = geo.Vertices.Length
        masterOf = New Integer(n - 1) {}
        membersOf = New Dictionary(Of Integer, List(Of Integer))(n)
        ' `weldPosEpsOrig` es una DISTANCIA en unidades de modelo, que es lo que declaran los
        ' llamadores y lo que dice el control de la UI. NO consumirlo como fraccion de la escala de la malla
        ' (k * L): el epsilon efectivo cambiaria con el tamano del mesh y dejaria de ser el que el usuario pide.
        ' Techo del epsilon. FUENTE: CreationKit.exe 0x142677EF0 usa el MISMO predicado que ClosePos
        ' (comparacion POR COMPONENTE, L-infinito) con tolerancia 1e-5 — constante @0x2FC4848 =
        ' 0x3727C5AC (float 1.0e-5). Pedir mas que eso sobre-suelda respecto del CK.
        Dim weldPosEps As Double = Math.Min(0.00001, Math.Max(0.0, weldPosEpsOrig))

        If weldPosEps <= 0 OrElse (Not byPosOnly AndAlso weldUVEps <= 0) OrElse n = 0 Then
            For i As Integer = 0 To n - 1
                masterOf(i) = i
                membersOf(i) = New List(Of Integer)(1) From {i}
            Next
            Exit Sub
        End If

        ' SE AGRUPA SOBRE EL ORDEN POR X, el mismo que mantiene el agrupado de costura entre llamadas.
        ' NO volver a una GRILLA HASH propia —cuantizar cada vertice a una celda, meterlo en un diccionario y
        ' barrer las celdas vecinas—: la grilla se reconstruye ENTERA en cada recalculo, y el barrido son 27
        ' consultas al diccionario por vertice agrupando por posicion, y 243 agrupando por posicion Y UV.
        '
        '   MEDIDO (`Tools\TbnPerfProbe`, modo `weldarrastre`), arrastre de UN vertice:
        '     malla de  9.409 vertices: 0,27 ms sin welding vs 25,6 ms con welding = 96x
        '     malla de 22.201 vertices: 0,55 ms sin welding vs 33,6 ms con welding = 61x
        '
        ' O sea que mover un vertice paga como si se hubieran movido los 22.201, en cada tick de un arrastre:
        ' con un cuadro de 16,6 ms a 60 fps, UNA shape con welding puesto se come dos cuadros.
        '
        ' El orden por X ya esta calculado y ya se mantiene incremental (ver `OrdenPorX`), y el predicado de
        ' posicion del weld es del mismo tipo que el del canonico —L-infinito contra un epsilon— asi que el
        ' mismo barrido hacia adelante con corte por X sirve, con OTRO epsilon: sin diccionario, sin hashing,
        ' sin cuantizar, y sin una segunda ley de agrupado.
        '
        ' EL GRUPO ES "todos los que coinciden con el PRIMERO en orden de X" —la misma forma que usa
        ' `ConstruyeGruposDeCostura` y la del `SortingMatcher` canonico—, NO "el primer candidato que aparece
        ' recorriendo los vertices en orden de indice", que depende del orden de recorrido.
        Dim orden() As Integer = Nothing
        Dim clave() As Double = Nothing
        OrdenPorX(geo.Vertices, n, cache, True, orden, clave)

        Dim asignado(n - 1) As Boolean
        For si As Integer = 0 To n - 1
            Dim vi As Integer = orden(si)
            If asignado(vi) Then Continue For
            asignado(vi) = True
            masterOf(vi) = vi
            Dim grupo As New List(Of Integer)(4) From {vi}
            For mi As Integer = si + 1 To n - 1
                ' Corte por X: `ClosePos` exige |dx| <= eps, asi que en cuanto la diferencia lo supera
                ' no puede haber mas candidatos mas adelante — el orden es creciente en X.
                If clave(mi) - clave(si) > weldPosEps Then Exit For
                Dim vj As Integer = orden(mi)
                If asignado(vj) Then Continue For
                If Not ClosePos(geo.Vertices(vj), geo.Vertices(vi), weldPosEps) Then Continue For
                If Not byPosOnly AndAlso Not CloseUV(geo.Uvs_Weight(vj), geo.Uvs_Weight(vi), weldUVEps) Then Continue For
                asignado(vj) = True
                masterOf(vj) = vi
                grupo.Add(vj)
            Next
            membersOf(vi) = grupo
        Next
    End Sub

    ' Comparación fina por componente (posición)
    Private Shared Function ClosePos(a As Vector3d, b As Vector3d, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps AndAlso Math.Abs(a.Z - b.Z) <= eps
    End Function

    ' Comparación fina por componente (UV)
    Private Shared Function CloseUV(a As Vector3, b As Vector3, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps
    End Function

    ''' <summary>
    ''' Tangente y bitangente de UNA cara: <c>BSTriShape::CalcTangentSpace</c>
    ''' (nifly Geometry.cpp:999-1026). Del determinante UV se usa SOLO EL SIGNO, cada direccion se
    ''' normaliza por triangulo y el llamador las acumula sin peso.
    '''
    ''' La formula se aplica SIEMPRE, sin rama para UV degenerada: <c>r</c> es +1 o -1 y nunca
    ''' cero. Inventar una base en el plano de la cara rompe los ROLES —<c>tFace</c> tiene que ser
    ''' dP/du— y en un shell de UV espejado sale con los canales intercambiados.
    '''
    ''' <paramref name="epsPosSq"/> es el umbral de degenerado, del config, y su default es CERO — o sea
    ''' el canonico, que normaliza sin umbral. Sigue expuesto porque descarta el aporte de un triangulo
    ''' cuya direccion es puro redondeo, pero prenderlo hoy solo aleja: ver la medicion en
    ''' <see cref="DefaultTBNOptions"/>.
    '''
    ''' Entra YA ELEVADO AL CUADRADO, y el nombre lo dice. <c>EpsilonPos</c> es una LONGITUD —asi lo
    ''' declara la opcion y asi lo pide el control de la UI— y aca se compara contra
    ''' <c>LengthSquared</c>, que es lo que evita la raiz por triangulo. Pasando la longitud cruda, el
    ''' umbral efectivo sobre la longitud era <c>sqrt(eps)</c>: pedir 1e-6 filtraba a 1e-3, mil veces
    ''' mas de lo pedido. El cuadrado lo hace el llamador UNA vez por malla, no una por triangulo.
    ''' </summary>
    Private Shared Sub ComputeFaceTB(e1 As Vector3, e2 As Vector3,
                                      _du1 As Single, _dv1 As Single, _du2 As Single, _dv2 As Single,
                                      _detNeg As Boolean, epsPosSq As Single,
                                      ByRef tFace As Vector3, ByRef bFace As Vector3)
        Dim r As Single = If(_detNeg, -1.0F, 1.0F)
        Dim tf = (e1 * _dv2 - e2 * _dv1) * r
        Dim bf = (e2 * _du1 - e1 * _du2) * r
        tFace = If(tf.LengthSquared > epsPosSq, Vector3.Normalize(tf), Vector3.Zero)
        bFace = If(bf.LengthSquared > epsPosSq, Vector3.Normalize(bf), Vector3.Zero)
    End Sub

    ''' <summary>
    ''' La base tangente de UN vertice: el bloque por vertice de <c>BSTriShape::CalcTangentSpace</c>
    ''' (nifly Geometry.cpp:1032-1053).
    ''' <code>
    ''' if (rawTangents.IsZero() || rawBitangents.IsZero()) {
    '''     rawTangents = (N.y, N.z, N.x); rawBitangents = N.cross(rawTangents); }
    ''' else {
    '''     rawTangents.Normalize();   rawTangents -= N * N.dot(rawTangents);   rawTangents.Normalize();
    '''     rawBitangents.Normalize(); rawBitangents -= N * N.dot(rawBitangents);
    '''     rawBitangents -= rawTangents * rawTangents.dot(rawBitangents); rawBitangents.Normalize(); }
    ''' </code>
    '''
    ''' <b>EL PRIMARIO ES <paramref name="B"/>.</b> Los dos acumulados no son perpendiculares
    ''' —ese es el sesgo de la parametrizacion UV— asi que ortogonalizar A contra B no da lo mismo
    ''' que B contra A: el marco queda girado alrededor de la normal. El que termina en el campo
    ''' TANGENTE del NIF es <c>geo.Bitangents</c> (el adaptador cruza), y ese es el primario.
    '''
    ''' <b>NORMALIZAR Y DESPUES PROYECTAR.</b> En una costura de ESPEJO los dos lados aportan
    ''' direcciones opuestas y el acumulado casi se cancela: queda un vector diminuto pero no cero,
    ''' que el canonico normaliza igual —amplificando ese residuo— antes de proyectar.
    '''
    ''' <b>El cero es EXACTO</b> (<c>IsZero()</c> sin epsilon, Object3d.hpp:111-119) y dispara si
    ''' CUALQUIERA de los dos acumulados es cero, reemplazando los DOS.
    '''
    ''' <paramref name="opts"/> manda sobre los dos agregados al canonico, y los dos vienen del
    ''' config con el default en True: <c>NormalizeOutputs</c> garantiza base ortonormal (el gate
    ''' F19 la exige y una base torcida se ve como iluminacion sucia) y <c>RepairNaNs</c> impide
    ''' que un NaN o un vector nulo lleguen al NIF. Apagarlos no rompe: deja pasar exactamente lo
    ''' que calculo el canonico.
    ''' </summary>
    ''' <remarks>NO recibe <c>EpsilonPos</c> a proposito: el criterio de acá es el CERO EXACTO del canonico,
    ''' no un umbral configurable. Ver la nota de <see cref="BaseDeUnVertice"/>.</remarks>
    Private Shared Sub BaseTangenteDeVertice(N As Vector3, accU As Vector3, accV As Vector3,
                                             quiral As Single, opts As TBNOptions,
                                             ByRef T As Vector3, ByRef B As Vector3)
        ' Rama degenerada del canonico: rotacion de componentes de la normal + cross. El canonico
        ' NO la ortogonaliza, asi que su `rawTangents` puede no ser perpendicular a la normal. Es el
        ' UNICO punto donde hace falta la garantia de `NormalizeOutputs`; en el camino normal el
        ' Gram-Schmidt de abajo ya deja la base ortonormal y re-proyectarla solo la perturba.
        ' MEDIDO en el CBBE de FO4: re-proyectar siempre empeora la tangente de 0,41 a 0,44 y la
        ' bitangente de 0,98 a 1,17 contra BodySlide.
        If EsCeroExacto(accU) OrElse EsCeroExacto(accV) Then
            ParDegenerado(N, T, B)
            If opts.NormalizeOutputs Then Ortonormaliza(N, T, B)
        Else
            ' Gram-Schmidt del canonico, TAL CUAL. El primario (tdir) se ortogonaliza solo contra la
            ' normal; el secundario (sdir) contra la normal y contra el primario ya fijado.
            B = NormalizaComoNifly(accV)
            B = NormalizaComoNifly(B - N * Vector3.Dot(N, B))

            T = NormalizaComoNifly(accU)
            T = T - N * Vector3.Dot(N, T)
            T = NormalizaComoNifly(T - B * Vector3.Dot(B, T))

            ' POST-CONDICION, no heuristica. El Gram-Schmidt del canonico NO garantiza una base
            ' ortonormal: cuando una de las restas se come la significancia, el `Normalize` final
            ' amplifica el redondeo y el resultado puede quedar hasta ANTIPARALELO a la normal.
            ' Se verifica el INVARIANTE que tiene que cumplir lo que se escribe, en vez de adivinar
            ' por que mecanismo se rompio: mirar el tamano del residuo dejaba pasar los casos justo
            ' por encima del umbral (MEDIDO en SSE `BaseArmor` v2079: residuo 4,97e-4, o sea por
            ' arriba de sqrt(eps), y la base igual a 87,7 grados de ortogonal).
            If opts.DeterministicOnCollapse AndAlso Not BaseEsOrtogonal(N, B, T) Then
                ' Se repara SOLO el eje roto. El primario y el secundario fallan por separado, y
                ' rehacer los dos tiraba a la basura un acumulado sano.
                Dim primarioOk As Boolean = Not EsCeroExacto(B) AndAlso Not HasNaN(B) AndAlso
                                            SonPerpendiculares(N, B, ToleranciaDePerpendicularidad(N))
                If Not primarioOk Then B = PrimarioDesdeNormal(N, T, quiral)
                T = SecundarioDeLaBase(N, B, quiral)
            End If

            ' El SECUNDARIO puede colapsar a cero DESPUES del Gram-Schmidt, cuando los dos
            ' acumulados salen (anti)paralelos. El canonico casi no lo ve porque ortogonaliza contra la
            ' normal CUANTIZADA A BYTE (`UpdateRawNormals`, Geometry.cpp:642 y :975) y ese redondeo de
            ' hasta 0,45 grados le impide colapsar. Derivarlo del primario es una reparacion de vector
            ' nulo como cualquier otra, asi que la gobierna la misma opcion.
            If opts.RepairNaNs AndAlso EsCeroExacto(T) AndAlso Not EsCeroExacto(B) Then
                T = SecundarioDeLaBase(N, B, quiral)
            End If
        End If

        ' RED FINAL de la base. Comparte la REGLA DE DECISION con la de InjectNormalsToTrishape
        ' —el NaN se repara siempre, el marco nulo lo gobierna `RepairNaNs`— pero no el tratamiento:
        ' aquella es la ultima barrera antes de escribir y sustituye componente por componente sin
        ' reortonormalizar, esta si deja una base. El canonico
        ' SI escribe marcos nulos —con la normal en cero su rama degenerada da tangente y bitangente
        ' nulas, y asi salen del BodySlide real—, asi que sustituirlos es la mejora de WM y la gobierna
        ' `RepairNaNs`. Apagada, sale exactamente el cero del canonico.
        ' Aca `Ortonormaliza` va SIEMPRE, sin mirar `NormalizeOutputs`: el unico proposito de esta rama
        ' es producir una base valida, y dejarla a medias no seria ni lo uno ni lo otro.
        If opts.RepairNaNs Then
            If HasNaN(T) OrElse HasNaN(B) OrElse EsCeroExacto(T) OrElse EsCeroExacto(B) Then
                ParDegenerado(N, T, B)
                Ortonormaliza(N, T, B)
                ' Con la normal NULA la rama degenerada tambien da cero: ahi ya no hay nada que
                ' derivar de la geometria y se cae a un par ortonormal cualquiera, que es lo unico
                ' que no deja basura en el archivo.
                If EsCeroExacto(B) OrElse EsCeroExacto(T) Then
                    B = PrimarioDesdeNormal(N, T, quiral)
                    T = SecundarioDeLaBase(N, B, quiral)
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Deja los dos ejes ortogonales a la normal y entre si. Se aplica SOLO donde el canonico no
    ''' garantiza una base —la rama degenerada y la red de NaN—, porque una base torcida en el NIF se
    ''' ve como iluminacion sucia y el gate F19 la exige.
    ''' El ORDEN importa: primero el primario contra la normal, y despues el secundario contra la
    ''' normal Y contra el primario ya fijado. Proyectar los dos contra la normal por separado deja T
    ''' y B sin ser perpendiculares entre si.
    ''' </summary>
    Private Shared Sub Ortonormaliza(N As Vector3, ByRef T As Vector3, ByRef B As Vector3)
        Dim nn As Single = N.LengthSquared
        If nn <= 0.0F Then Exit Sub
        B -= N * (Vector3.Dot(N, B) / nn)
        If B.LengthSquared > 0.0F Then B = Vector3.Normalize(B)
        T -= N * (Vector3.Dot(N, T) / nn)
        Dim bb As Single = B.LengthSquared
        If bb > 0.0F Then T -= B * (Vector3.Dot(B, T) / bb)
        If T.LengthSquared > 0.0F Then T = Vector3.Normalize(T)
    End Sub

    ''' <summary>
    ''' El <c>Normalize()</c> de nifly en Double, para el pase de skinning.
    ''' <c>Vector3d.Normalize</c> de OpenTK divide por la longitud SIN guarda, asi que con un vector
    ''' nulo devuelve NaN. Una normal nula NO es basura ni un caso imposible: la fuente puede traerla
    ''' —el <c>CBBEBody.nif</c> de CBBE tiene 14— y el canonico la conserva. Al convertirla en NaN, el
    ''' skinning obligaba a la red final a inventarle un valor, y esa sustitucion era la unica
    ''' diferencia que quedaba contra BodySlide en ese archivo.
    ''' </summary>
    Public Shared Function NormalizaComoNifly(v As Vector3d) As Vector3d
        Dim l As Double = v.Length
        If l = 0.0 Then Return v
        Return v / l
    End Function

    ''' <summary>El <c>Normalize()</c> de nifly: divide por la longitud, y con longitud CERO deja el
    ''' vector como esta. Sin umbral — el umbral es justo lo que hacia divergir las costuras de espejo.</summary>
    Private Shared Function NormalizaComoNifly(v As Vector3) As Vector3
        Dim l As Single = v.Length
        If l = 0.0F Then Return v
        Return v / l
    End Function

    ''' <summary>
    ''' El par degenerado del canonico, TAL CUAL: <c>rawTangents = (N.y, N.z, N.x)</c> y
    ''' <c>rawBitangents = N x rawTangents</c> (nifly, Geometry.cpp).
    ''' Sin proyectar, sin normalizar y sin inventar nada: con la normal en CERO devuelve el par NULO, que es
    ''' exactamente lo que escribe el BodySlide real. Hacer acá cualquiera de esas tres cosas duplica la ley,
    ''' porque el llamador ya ortonormaliza encima — dejar la base ortonormal y que no salga un marco nulo lo
    ''' deciden <c>NormalizeOutputs</c> y <c>RepairNaNs</c>, allá.
    ''' </summary>
    Private Shared Sub ParDegenerado(N As Vector3, ByRef T As Vector3, ByRef B As Vector3)
        B = New Vector3(N.Y, N.Z, N.X)
        T = Vector3.Cross(N, B)
    End Sub

    ''' <summary>Cero EXACTO en las tres componentes, que es el <c>IsZero()</c> del canonico sin
    ''' epsilon. Un umbral aca cambiaria cuantos vertices caen en el fallback degenerado.</summary>
    Private Shared Function EsCeroExacto(v As Vector3) As Boolean
        Return v.X = 0.0F AndAlso v.Y = 0.0F AndAlso v.Z = 0.0F
    End Function

    ''' <summary>
    ''' Precisión de la mantisa de IEEE-754 binary32: 2^-23. Es una propiedad del TIPO, no una
    ''' constante ajustada — <c>Single.Epsilon</c> NO sirve acá, que es el subnormal más chico.
    ''' <para>Se DERIVA, no se transcribe: el literal `1.1920929E-07F` obliga a confiar en que alguien lo
    ''' copió bien.</para>
    ''' </summary>
    Private Shared ReadOnly EpsilonDeMantisaSingle As Single = 1.0F / (1UL << 23)

    ''' <summary>Precalculado, no <c>Math.Sqrt(...)</c> dentro de las dos funciones que corren POR VERTICE:
    ''' serian una raiz en Double mas la conversion, dos veces por vertice, sobre decenas de millones. Es una
    ''' constante del TIPO — no depende de la malla ni del vertice.</summary>
    Private Shared ReadOnly RaizDeEpsilonSingle As Single = CSng(Math.Sqrt(1.0F / (1UL << 23)))

    ''' <summary>
    ''' ¿Los dos vectores son perpendiculares dentro de lo que permite el tipo? Un Gram-Schmidt sano
    ''' deja el producto punto en el orden de <c>eps</c> (1e-7); uno cuyo residuo se fue al ruido lo
    ''' deja en el orden de 1. El límite es <c>sqrt(eps)</c> — el criterio clásico de pérdida
    ''' catastrófica de significancia— y separa las dos poblaciones por varias décadas.
    ''' Compara DIRECCIONES: normaliza, así que no depende de la escala de la malla ni del equipo.
    ''' </summary>
    ''' Al cuadrado y sin dividir: <c>|a·b| / (|a||b|) &lt;= tol</c> es lo mismo que
    ''' <c>(a·b)² &lt;= tol²·|a|²·|b|²</c>, y así el chequeo —que corre por vértice— no paga tres raíces
    ''' ni dos divisiones. Es equivalente exacto salvo el redondeo del producto, muy por debajo del
    ''' límite que se compara.
    Private Shared Function SonPerpendiculares(a As Vector3, b As Vector3, tol As Single) As Boolean
        Dim aa As Single = a.LengthSquared, bb As Single = b.LengthSquared
        If aa <= 0.0F OrElse bb <= 0.0F Then Return False
        Dim d As Single = Vector3.Dot(a, b)
        Return d * d <= tol * tol * aa * bb
    End Function

    ''' <summary>
    ''' Cuánto puede alejarse de perpendicular la base SIN que sea un defecto, para ESTA normal.
    '''
    ''' No es una constante elegida: es el término de error de la propia fórmula del canónico.
    ''' <c>B −= N·(N·B)</c> sólo deja <c>B</c> perpendicular a <c>N</c> si <c>N</c> es UNITARIA — falta
    ''' dividir por <c>|N|²</c>. Y la normal que entra al Gram-Schmidt viene cuantizada a 3 bytes
    ''' (<c>UpdateRawNormals</c>), así que no lo es: con <c>|N|² = 1 − δ</c> el coseno que queda es
    ''' exactamente <c>δ</c>. Por eso el límite se calcula por vértice a partir de la normal real, y no
    ''' con un número global que habría que calibrar. Debajo de eso está el redondeo puro, que es
    ''' <c>sqrt(eps)</c>.
    ''' </summary>
    Private Shared Function ToleranciaDePerpendicularidad(N As Vector3) As Single
        Dim piso As Single = RaizDeEpsilonSingle
        Dim delta As Single = Math.Abs(1.0F - N.LengthSquared)
        ' La derivacion vale para una normal que es UNITARIA salvo la cuantizacion del formato: ahi
        ' `delta` es como mucho ~0,0136 (error de 1/255 por componente). Una normal cuya longitud no
        ' se parece a 1 esta malformada —pasa con `KeepExistingNormals` sobre un NIF cuyo array de
        ' normales no vino normalizado— y con `delta >= 1` el chequeo aceptaria CUALQUIER angulo,
        ' o sea que la opcion se apagaria sola y en silencio. Ahi no se le concede tolerancia: se
        ' exige el piso, y la base termina reconstruida ortonormal alrededor de esa direccion.
        If delta >= 1.0F Then Return piso
        Return Math.Max(piso, delta)
    End Function

    ''' <summary>
    ''' El invariante que tiene que cumplir la base ANTES de escribirse: los tres ejes mutuamente
    ''' perpendiculares y ninguno nulo. Es la post-condición de <see cref="BaseTangenteDeVertice"/>
    ''' cuando <see cref="TBNOptions.DeterministicOnCollapse"/> está puesta. El mismo invariante se
    ''' verifica sobre el NIF ya escrito con <c>Tools\TbnGate\gate.ps1</c>.
    ''' Es ciego al SIGNO: compara el producto punto al cuadrado, así que una base con la
    ''' quiralidad invertida pasa. Por eso la quiralidad se fija por construcción en
    ''' <see cref="SecundarioDeLaBase"/> y no se deja para que la valide este chequeo.
    ''' </summary>
    Friend Shared Function BaseEsOrtogonal(N As Vector3, primario As Vector3, secundario As Vector3) As Boolean
        If EsCeroExacto(N) OrElse EsCeroExacto(primario) OrElse EsCeroExacto(secundario) Then Return False
        If HasNaN(N) OrElse HasNaN(primario) OrElse HasNaN(secundario) Then Return False
        ' Contra la normal, el límite lo pone la propia normal (ver ToleranciaDePerpendicularidad).
        ' Entre primario y secundario no interviene: esa proyección usa un primario ya unitario, así
        ' que ahí lo único que queda es el redondeo.
        Dim tolN As Single = ToleranciaDePerpendicularidad(N)
        Dim tolPuro As Single = RaizDeEpsilonSingle
        Return SonPerpendiculares(N, primario, tolN) AndAlso
               SonPerpendiculares(N, secundario, tolN) AndAlso
               SonPerpendiculares(primario, secundario, tolPuro)
    End Function

    ''' <summary>
    ''' El SECUNDARIO que corresponde a una normal y un primario dados, con la QUIRALIDAD correcta.
    '''
    ''' No es <c>N x B</c> a secas, y esto es algebra, no preferencia. Con <c>sdir</c> y <c>tdir</c>
    ''' como los define el canonico se cumple <c>sdir x tdir = det · (e1 x e2)</c>; el secundario es
    ''' <c>Σsdir</c>, el primario <c>Σtdir</c> y la normal de cara es <c>e1 x e2</c>, o sea que el
    ''' marco cumple <c>T x B = det · N</c>. Para <c>det &gt; 0</c> eso da <c>T = B x N</c>, que es el
    ''' OPUESTO de <c>N x B</c>. Escribirlo al reves invierte el canal verde del normal map en ese
    ''' vertice respecto de sus vecinos, y —peor— el chequeo de ortogonalidad no lo puede ver, porque
    ''' compara el producto punto al cuadrado y es ciego al signo.
    ''' <paramref name="quiral"/> es la suma de <c>sign(det)</c> de las caras incidentes. En cero
    ''' —tantas caras de una quiralidad como de la otra— no hay respuesta de los datos y se toma la
    ''' derecha, que es la convencion del formato.
    ''' </summary>
    Private Shared Function SecundarioDeLaBase(N As Vector3, B As Vector3, quiral As Single) As Vector3
        Dim t As Vector3 = If(quiral < 0.0F, Vector3.Cross(N, B), Vector3.Cross(B, N))
        If t.LengthSquared > 0.0F Then Return Vector3.Normalize(t)
        Return t
    End Function

    ''' <summary>
    ''' El PRIMARIO cuando el suyo no sirve. Si el secundario si sirve se lo deriva de EL —así se
    ''' conserva la unica direccion tangente que los datos todavia determinan— y recien si tampoco hay
    ''' secundario se cae a un eje perpendicular a la normal.
    ''' </summary>
    Private Shared Function PrimarioDesdeNormal(N As Vector3, secundario As Vector3, quiral As Single) As Vector3
        If Not EsCeroExacto(secundario) AndAlso Not HasNaN(secundario) AndAlso
           SonPerpendiculares(N, secundario, ToleranciaDePerpendicularidad(N)) Then
            ' De T x B = det·N sale B = N x T para det > 0.
            Dim b As Vector3 = If(quiral < 0.0F, Vector3.Cross(secundario, N), Vector3.Cross(N, secundario))
            If b.LengthSquared > 0.0F Then Return Vector3.Normalize(b)
        End If
        Return OrthonormalTangentFromNormal(N)
    End Function

    ''' <summary>
    ''' Un eje perpendicular a la normal, deterministico, para cuando la geometria no determina
    ''' ninguno. Se elige el eje del mundo MENOS alineado con la normal para que el producto cruz no
    ''' salga degenerado; el 0,9 es el corte estandar de esa heuristica (con |n.x| &gt;= 0,9 la normal ya
    ''' esta casi sobre X y conviene cruzar contra Y). No hay respuesta canonica: el canonico no llega
    ''' hasta aca. Lo unico que importa es que sea SIEMPRE la misma para la misma normal.
    ''' </summary>
    Private Shared Function OrthonormalTangentFromNormal(n As Vector3) As Vector3
        Dim ax As Vector3 = If(Math.Abs(n.X) < 0.9F, New Vector3(1, 0, 0), New Vector3(0, 1, 0))
        Dim t As Vector3 = Vector3.Cross(ax, n)
        If t.LengthSquared <= 1.0E-20F Then t = Vector3.Cross(New Vector3(0, 0, 1), n)
        If t.LengthSquared <= 1.0E-20F Then Return New Vector3(1, 0, 0)
        Return Vector3.Normalize(t)
    End Function

    Friend Shared Function HasNaN(v As Vector3d) As Boolean
        Return Double.IsNaN(v.X) OrElse Double.IsNaN(v.Y) OrElse Double.IsNaN(v.Z)
    End Function

    ''' <summary>La normal contra la que ortogonalizar: cuantizada si el bloque destino guarda la
    ''' normal en bytes, tal cual si la guarda en float.</summary>
    Friend Shared Function NormalParaTBN(v As Vector3, cuantiza As Boolean) As Vector3
        Return If(cuantiza, NormalComoLaVeElCanonico(v), v)
    End Function

    ''' <summary>
    ''' La posicion para el TBN: en Single, SIN redondear a la precision del formato.
    '''
    ''' PROBADO Y DESCARTADO redondearla a half cuando el NIF la guarda en half. El razonamiento
    ''' era que el canonico ve las posiciones ya cuantizadas por el archivo, asi que replicarlo
    ''' acercaria; es falso. WM tiene la posicion en plena precision ANTES de escribirla, y degradarla
    ''' solo suma error propio: MEDIDO contra BodySlide en FO4, `LaceBra` y `Panty` pasan de 0,01 a
    ''' 0,57 y 1,67 grados, y el CBBE de 0,27 a 0,39 en normales.
    ''' </summary>
    Friend Shared Function PosParaTBN(v As Vector3d) As Vector3
        Return ASng(v)
    End Function

    ''' <summary>Round-trip por HALF (16 bits): asi guarda el NIF las UV de BSTriShape y las
    ''' posiciones de un vertice de precision reducida.</summary>
    Friend Shared Function AHalf(c As Single) As Single
        Return CSng(CType(c, Half))
    End Function


    ''' <summary>La componente de UV con la precision del formato.</summary>
    Friend Shared Function UvComponente(c As Single, uvHalf As Boolean) As Single
        Return If(uvHalf, AHalf(c), c)
    End Function

    ''' <summary>
    ''' La normal tal como la VE el canonico al calcular la base tangente: ida y vuelta por los 3 bytes del
    ''' NIF. <c>CalcTangentSpace</c> de nifly llama <c>UpdateRawNormals()</c> antes de acumular, asi que
    ''' ortogonaliza contra esto y no contra la normal de plena precision.
    '''
    ''' El resultado NO es unitario y NO hay que normalizarlo: el canonico lo usa asi, crudo, en
    ''' <c>N.dot(...)</c> y en <c>N * dot</c>. Normalizarlo lo aleja de nuevo.
    ''' La codificacion es la de <c>SetNormals</c>: <c>round((x+1)/2*255)</c> a byte, y la vuelta es
    ''' <c>b/255*2-1</c>. El redondeo es a entero mas cercano.
    ''' </summary>
    Friend Shared Function NormalComoLaVeElCanonico(v As Vector3) As Vector3
        Return New Vector3(ByteIdaYVuelta(v.X), ByteIdaYVuelta(v.Y), ByteIdaYVuelta(v.Z))
    End Function

    Private Shared Function ByteIdaYVuelta(c As Single) As Single
        Dim b As Integer = CInt(Math.Round((c + 1.0F) / 2.0F * 255.0F, MidpointRounding.AwayFromZero))
        If b < 0 Then b = 0
        If b > 255 Then b = 255
        Return CSng(b) / 255.0F * 2.0F - 1.0F
    End Function

    ''' <summary>Sobrecarga en Single: la cadena del TBN trabaja entera en esta precision.</summary>
    Friend Shared Function HasNaN(v As Vector3) As Boolean
        Return Single.IsNaN(v.X) OrElse Single.IsNaN(v.Y) OrElse Single.IsNaN(v.Z)
    End Function

    ''' <summary>
    ''' Double -> Single, EXPLICITO. OpenTK tiene conversion entre <c>Vector3d</c> y <c>Vector3</c>, y
    ''' con <c>Option Strict Off</c> —que es como esta el proyecto— el compilador la aplicaria sola y
    ''' en silencio. Que el redondeo se vea en el codigo es el punto: es el unico lugar donde N/T/B
    ''' pierden precision, y tiene que ser buscable con un grep.
    ''' </summary>
    Public Shared Function ASng(v As Vector3d) As Vector3
        Return New Vector3(CSng(v.X), CSng(v.Y), CSng(v.Z))
    End Function

    ''' <summary>Single -> Double. Exacta (todo float es un double).</summary>
    Public Shared Function ADbl(v As Vector3) As Vector3d
        Return New Vector3d(v.X, v.Y, v.Z)
    End Function

End Class
