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
    Public PerVertexSkinMatrix() As Matrix4d   ' per-vertex blended Mtot = GlobalTransform * skin; filled once in ExtractSkinnedGeometry
    Public dirtyMaskIndices As HashSet(Of Integer)              ' Para dirty-tracking de máscara
    ' Set by MorphEngine.ApplyMorphPlan whenever it (re)computes the zap mask; consumed + cleared by
    ' Render.EnsureZapIndexBuffer to rebuild the filtered element buffer only when the zap set changed.
    ' Initialized True in ExtractSkinnedGeometry so the first draw filters (a Structure can't have an
    ' instance field initializer in VB — BC31049 — so the default is set at construction instead).
    ' (See render-zap-clean-cpu-index-filter.)
    Public ZapTopologyDirty As Boolean
    Public dirtyVertexIndices As HashSet(Of Integer)
    Public dirtyMaskFlags() As Boolean
    Public dirtyVertexFlags() As Boolean
    Public Normals() As Vector3d
    Public Tangents() As Vector3d
    Public Bitangents() As Vector3d
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
    ''' ⛔⛔ <c>Partitioner.Create(0, 0)</c> <b>LANZA</b>
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
    ''' ⚠️ Esto es para loops de GEOMETRÍA, donde una shape vacía es un dato legítimo. Los loops por
    ''' píxel (compositores, bakers) NO usan esto a propósito: un bitmap de 0 píxeles sería otro bug y
    ''' taparlo lo escondería.
    ''' </summary>
    Public Shared Function RangosDe(n As Integer) As Partitioner(Of Tuple(Of Integer, Integer))
        If n <= 0 Then Return Partitioner.Create(Array.Empty(Of Tuple(Of Integer, Integer))())
        Return Partitioner.Create(0, n)
    End Function

    ' ⛔ SYNC: CPU/GPU skinning — blend de bone matrices del lado CPU (double).
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
    Private Shared Function BlendBoneMatrices(boneWeights As System.Half(), boneIndices As Byte(), precomputed() As Matrix4d) As Matrix4d
        If boneWeights Is Nothing OrElse boneIndices Is Nothing OrElse precomputed.Length = 0 Then Return If(precomputed.Length > 0, precomputed(0), Matrix4d.Identity)
        Dim result As Matrix4d = Matrix4d.Zero
        Dim sumW As Double = 0
        Dim cnt = Math.Min(boneWeights.Length, boneIndices.Length) - 1
        ' Normalizacion de pesos del MOTOR (EngineSkinWeightNormalization): w3 = 1−Σ, descarta el slot con peso ≤0,
        ' Gate apagado (default) ⇒ early-return y el camino de abajo queda bit-idéntico al de siempre.
        Dim ckW(EngineSkinWeightNormalization.Slots - 1) As Single
        If EngineSkinWeightNormalization.TryComputeWeights(boneWeights, 0, boneWeights.Length, ckW) Then
            For j = 0 To EngineSkinWeightNormalization.Slots - 1
                If ckW(j) > 0.0F Then
                    Dim idxc = boneIndices(j)
                    If idxc >= 0 AndAlso idxc < precomputed.Length Then result += precomputed(idxc) * CDbl(ckW(j))
                End If
            Next
            Return result
        End If
        ' Single pass: accumulate weighted matrices and sum of weights simultaneously
        For j = 0 To cnt
            Dim w = CType(boneWeights(j), Double)
            sumW += w
            Dim idx = boneIndices(j)
            If idx >= 0 AndAlso idx < precomputed.Length Then result += precomputed(idx) * w
        Next
        If sumW = 0 Then
            Dim idx0 = If(boneIndices.Length > 0, boneIndices(0), 0)
            Return precomputed(Math.Max(0, Math.Min(idx0, precomputed.Length - 1)))
        End If
        Return result * (1.0 / sumW)
    End Function

    ''' <summary>
    ''' Flat-array overload of BlendBoneMatrices that reads <paramref name="wpv"/> bone slots
    ''' starting at <paramref name="baseIdx"/> in the flat <paramref name="boneWeights"/> /
    ''' <paramref name="boneIndices"/> arrays.  Same semantics and fallback as the per-vertex
    ''' overload but avoids per-vertex slice allocation, which matters in the inner skinning
    ''' loop (called once per vertex per Extract/Bake call).
    ''' </summary>
    Private Shared Function BlendBoneMatrices(boneWeights As System.Half(), boneIndices As Byte(), baseIdx As Integer, wpv As Integer, precomputed() As Matrix4d,
                                              Optional flatPal As Double() = Nothing) As Matrix4d
        If boneWeights Is Nothing OrElse boneIndices Is Nothing OrElse precomputed.Length = 0 OrElse wpv <= 0 Then
            Return If(precomputed.Length > 0, precomputed(0), Matrix4d.Identity)
        End If
        Dim sumW As Double = 0
        Dim available As Integer = Math.Min(wpv, Math.Min(boneWeights.Length - baseIdx, boneIndices.Length - baseIdx))

        ' ⭐ Las GUARDAS se recorren UNA sola vez y dejan los pares (indice, peso) validos en el
        ' scratch, EN EL MISMO ORDEN en que el escalar los sumaria. Recien despues se decide con que
        ' camino acumular (ver AccumulateBlend). Antes de esto la guarda estaba pegada a la suma y
        ' habia que duplicarla para tener dos caminos; asi hay una sola copia de la ley.
        ' ⛔ Las guardas quedan ESCALARES a proposito: no hay una sola mascara por lane en el camino
        ' vectorial, y por eso las trampas #4 (orden de los selects) y #5 (NaN) no aplican acá.
        Dim sc = GetBlendScratch(Math.Max(available, EngineSkinWeightNormalization.Slots))
        Dim nUsed As Integer = 0

        ' Normalizacion de pesos del MOTOR — ver el overload de arriba. Gate apagado ⇒ bit-idéntico al historico.
        Dim ckW(EngineSkinWeightNormalization.Slots - 1) As Single
        If available >= EngineSkinWeightNormalization.Slots AndAlso EngineSkinWeightNormalization.TryComputeWeights(boneWeights, baseIdx, wpv, ckW) Then
            For j = 0 To EngineSkinWeightNormalization.Slots - 1
                If ckW(j) > 0.0F Then
                    Dim idxc = boneIndices(baseIdx + j)
                    If idxc >= 0 AndAlso idxc < precomputed.Length Then
                        sc.Idx(nUsed) = CInt(idxc) : sc.W(nUsed) = CDbl(ckW(j)) : nUsed += 1
                    End If
                End If
            Next
            Return AccumulateBlend(precomputed, flatPal, sc, nUsed)
        End If

        For j = 0 To available - 1
            Dim w = CType(boneWeights(baseIdx + j), Double)
            ' ⛔ sumW acumula TODOS los pesos, tambien los de indice fuera de rango: es la ley
            ' historica y mover eso cambiaria el divisor.
            sumW += w
            Dim idx = boneIndices(baseIdx + j)
            If idx >= 0 AndAlso idx < precomputed.Length Then
                sc.Idx(nUsed) = CInt(idx) : sc.W(nUsed) = w : nUsed += 1
            End If
        Next
        If sumW = 0 Then
            Dim idx0 As Byte = If(available > 0, boneIndices(baseIdx), CByte(0))
            Return precomputed(Math.Max(0, Math.Min(CInt(idx0), precomputed.Length - 1)))
        End If
        Return AccumulateBlend(precomputed, flatPal, sc, nUsed, 1.0 / sumW)
    End Function

    ''' <summary>
    ''' Buffers de trabajo del blend, UNO POR HILO. Se reusan entre vertices: alocarlos por vertice
    ''' seria una allocation Gen0 en el bucle mas caliente del skinning.
    ''' <para>⛔ Cada hilo construye EL SUYO (ver <see cref="GetBlendScratch"/>). No repetir el bug de
    ''' RecalcTBN, donde la fabrica del ThreadLocal devolvia SIEMPRE EL MISMO array y los "locales"
    ''' no eran locales de nadie ⇒ carrera silenciosa y build no reproducible. Acá además el scratch
    ''' se escribe y se lee dentro de UNA sola llamada, sin cruzar hilos ni iteraciones.</para>
    ''' </summary>
    Private NotInheritable Class BlendScratch
        Public Idx() As Integer
        Public W() As Double
        Public ReadOnly Acc(FastGeom.MatDoubles - 1) As Double
        Public Sub New(slots As Integer)
            Grow(slots)
        End Sub
        Public Sub Grow(slots As Integer)
            Dim n = Math.Max(1, slots)
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
    ''' <para>⭐ Es el UNICO punto donde se elige camino. Con paleta plana y SIMD acelerado va por
    ''' <see cref="FastGeom.BlendInto"/>; si no, por el mismo <c>result += precomputed(idx) * w</c>
    ''' de siempre. Los dos recorren los pares en el MISMO orden ⇒ el resultado es bit-identico, y eso
    ''' es lo que verifica <c>SkinningSimdSelfTest</c> sobre la funcion real, no sobre una maqueta.</para>
    ''' </summary>
    Private Shared Function AccumulateBlend(precomputed() As Matrix4d, flatPal As Double(), sc As BlendScratch, nUsed As Integer,
                                            Optional postScale As Double = 1.0) As Matrix4d
        If flatPal IsNot Nothing AndAlso FastGeom.Accelerated Then
            FastGeom.BlendInto(flatPal, sc.Idx, sc.W, nUsed, sc.Acc)
            If postScale <> 1.0 Then FastGeom.ScaleAcc(sc.Acc, postScale)
            Return FastGeom.LoadMatrix(sc.Acc, 0)
        End If
        Dim result As Matrix4d = Matrix4d.Zero
        For j = 0 To nUsed - 1
            result += precomputed(sc.Idx(j)) * sc.W(j)
        Next
        If postScale <> 1.0 Then result = result * postScale
        Return result
    End Function

    ''' <summary>
    ''' Gate del blend vectorial: corre <b>la funcion REAL</b> <c>BlendBoneMatrices</c> por los dos
    ''' caminos (con paleta plana ⇒ vectorial, sin paleta ⇒ escalar) y compara BIT A BIT.
    ''' Devuelve "" si pasa.
    '''
    ''' <para>⭐ Prueba la produccion, no una maqueta. Un test que reimplementa el kernel al lado
    ''' puede dar verde mientras la funcion real diverge — es la trampa #10 de
    ''' 61-perf-simd-trampas, y la forma de no pisarla es llamar al codigo que corre de verdad.</para>
    '''
    ''' <para>⛔ Da veredicto AL ANCHO DE ESTA MAQUINA. Hay que correrlo TAMBIEN con
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
        Dim flatPal = FastGeom.BuildFlatPalette(precomputed)

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

            Dim mEsc = BlendBoneMatrices(wgt, idx, baseIdx, wpv, precomputed)
            Dim mVec = BlendBoneMatrices(wgt, idx, baseIdx, wpv, precomputed, flatPal)
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
        Dim palVacia = FastGeom.BuildFlatPalette(vacio)
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

        Return ""
    End Function

    ''' <summary>Extrae vértices, normales, tangentes y bitangentes del shape, aplicando el mismo skinning
    ''' que LoadShapeSafe, y arma los arrays de índices y pesos que consume la GPU.
    ''' <para>⛔ SYNC: CPU/GPU skinning — acá se NORMALIZAN los pesos que después usa el vertex shader
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
        Dim rawNormals() As Vector3d
        Dim rawTangents() As Vector3d
        Dim rawBitangs() As Vector3d

        If shapeGeom.HasNormals Then
            Dim srcNormals = shapeGeom.GetNormals()
            rawNormals = New Vector3d(rawVerts.Length - 1) {}
            Parallel.ForEach(RangosDe(rawVerts.Length),
                Sub(rango As Tuple(Of Integer, Integer))
                    For i = rango.Item1 To rango.Item2 - 1
                                                     Dim v As New Vector3d(srcNormals(i).X, srcNormals(i).Y, srcNormals(i).Z)
                                                     Dim l = v.Length
                                                     rawNormals(i) = If(l > 0.000001, v / l, Vector3d.Zero)
                    Next
                End Sub)
        Else
            rawNormals = New Vector3d(rawVerts.Length - 1) {}
        End If

        If shapeGeom.HasTangents Then
            Dim srcTan = shapeGeom.GetTangents()
            Dim srcBit = shapeGeom.GetBitangents()
            rawTangents = New Vector3d(rawVerts.Length - 1) {}
            rawBitangs = New Vector3d(rawVerts.Length - 1) {}
            Parallel.ForEach(RangosDe(rawVerts.Length),
                Sub(rango As Tuple(Of Integer, Integer))
                    For i = rango.Item1 To rango.Item2 - 1
                                                     Dim t = srcTan(i)
                                                     Dim b = srcBit(i)
                                                     Dim tv As New Vector3d(t.X, t.Y, t.Z)
                                                     Dim bv As New Vector3d(b.X, b.Y, b.Z)
                                                     Dim tl = tv.Length
                                                     Dim bl = bv.Length
                                                     rawTangents(i) = If(tl > 0.000001, tv / tl, Vector3d.Zero)
                                                     rawBitangs(i) = If(bl > 0.000001, bv / bl, Vector3d.Zero)
                    Next
                End Sub)
        Else
            rawTangents = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
            rawBitangs = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
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
                Dim shNm = shape.ShapeName, bnNm = boneName, kIdx = k
                Logger.LogLazy(Function() $"[SKIN-MAT] shape='{shNm}' bone[{kIdx}]='{bnNm}' skin.T=({skt.X:F3},{skt.Y:F3},{skt.Z:F3})")
            End If
        Next

        ' 4) Aplicar skinning CPU
        ' Save NIF-local vertices BEFORE skinning (needed for correct morph-space application)
        Dim nifLocalVerts = rawVerts.ToArray()
        Dim perVertexMtot(vertexCount - 1) As Matrix4d

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
                ' Pre-compute bone matrices (shapeGlobalTransform * matsPose(k))
                Dim precomputedBoneMatrices(bones.Count - 1) As Matrix4d
                For k = 0 To bones.Count - 1
                    precomputedBoneMatrices(k) = GlobalTransform * matsPose(k)
                Next
                ' Paleta plana para el blend vectorial. Se arma UNA vez por shape (20-60 matrices),
                ' no por vertice: la copia no esta en el camino caliente. Ver FastGeom.
                Dim flatPalette = FastGeom.BuildFlatPalette(precomputedBoneMatrices)

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

                Dim skinningBody As Action(Of Integer) = Sub(i)
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
                                                                 Dim ckWg(EngineSkinWeightNormalization.Slots - 1) As Single
                                                                 If EngineSkinWeightNormalization.TryComputeWeights(skinFlatWgt, baseSkin, skinWpv, ckWg) Then
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

                                                             ' Store double-precision Mtot for world-space cache / bake
                                                             perVertexMtot(i) = Mtot
                                                         End Sub

                If useParallel Then
                    Parallel.For(0, vertexCount, parallelOpts, skinningBody)
                Else
                    For i As Integer = 0 To vertexCount - 1
                        skinningBody(i)
                    Next
                End If

            Case shape.IsSkinned AndAlso singleboneskinning AndAlso bones.Count > 0
                ' Single-bone: pre-compute once — GPU path: do NOT transform rawVerts/N/T/B
                Dim Mtot = GlobalTransform * matsPose(0)
                Array.Fill(perVertexMtot, Mtot)

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

                Array.Fill(perVertexMtot, Mtot)

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
            Dim wv = Vector3d.TransformPosition(rawVerts(i), perVertexMtot(i))
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
            .dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, vertexCount)),
            .dirtyMaskIndices = New HashSet(Of Integer)(Enumerable.Range(0, vertexCount)),
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

        If RecalculateNormals OrElse Not shapeGeom.HasNormals OrElse Not shapeGeom.HasTangents Then
            Dim opts = Config_App.Current.Setting_TBN
            RecalculateNormalsTangentsBitangents(geo, opts)
        End If
        Return geo
    End Function

    ''' <summary>Normal matrix (inverse-transpose de la parte lineal) tolerante a singularidad.
    ''' Con un eje escalado a 0 — p.ej. Scale=0 en el editor de transforms — la 3×3 no tiene
    ''' inversa: la geometría colapsa a un plano/punto y la normal queda matemáticamente
    ''' indefinida. Devolvemos identidad en lugar de dejar que OpenTK tire
    ''' InvalidOperationException ("Matrix is singular and cannot be inverted").</summary>
    Public Shared Function NormalMatrixOrIdentity(Origen As Matrix4d) As Matrix3d
        Dim L As New Matrix3d(Origen)
        If Math.Abs(L.Determinant) < 1.0E-12 Then Return Matrix3d.Identity
        Return L.Inverted().Transposed()
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
    ''' Bake current pose into geometry: vertices/normals/tangents/bitangents are transformed
    ''' by the per-bone skin matrices stored in <paramref name="geom"/>. If the underlying
    ''' SkeletonInstance has no DeltaTransforms, matsBind == matsPose and the bake collapses
    ''' to identity (no-op outcome, callers paid the parallel-loop cost). Callers that want
    ''' "bake skipped when no pose" must check upstream and avoid invoking this method.
    ''' </summary>
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

        Dim worldN() As Vector3d = geom.Normals
        Dim worldT() As Vector3d = geom.Tangents
        Dim worldB() As Vector3d = geom.Bitangents

        ' 5) Datos de skinning por vértice — polimórficos via ShapeSkinningData
        '    (BSTriShape inline o NiSkinPartition expandido).
        Dim skinFlatIdx = geom.Skinning.BoneIndices
        Dim skinFlatWgt = geom.Skinning.BoneWeights
        Dim skinWpv = If(geom.Skinning.WeightsPerVertex > 0, geom.Skinning.WeightsPerVertex, 4)
        Dim hasSkin = (skinFlatIdx IsNot Nothing AndAlso skinFlatWgt IsNot Nothing AndAlso geom.Skinning.VertexCount = worldV.Length)

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
                ' ⭐ MEMOIZACION POR FIRMA DE SKIN. Las dos matrices que se derivan del vertice
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
                                                               worldV(i) = Vector3d.TransformPosition(worldV(i), listo.Total)
                                                               worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), listo.Normales))
                                                               worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), listo.Total))
                                                               worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), listo.Total))
                                                               Return
                                                           End If
                                                           memoOk = True
                                                       End If

                                                       If hasSkin Then
                                                           Dim baseIdx = i * skinWpv
                                                           Dim cnt = Math.Min(skinWpv, Math.Min(skinFlatWgt.Length - baseIdx, skinFlatIdx.Length - baseIdx)) - 1
                                                           ' Normalizacion de pesos del MOTOR — se aplica a las DOS mezclas (pose y bind),
                                                           ' que es lo que hace el CK: los dos SkinBlend de ApplyCustomizationRemap corren la
                                                           ' misma ley. Gate apagado (default) ⇒ rama normalizada de siempre, bit-idéntica.
                                                           Dim ckWv(EngineSkinWeightNormalization.Slots - 1) As Single
                                                           If EngineSkinWeightNormalization.TryComputeWeights(skinFlatWgt, baseIdx, skinWpv, ckWv) Then
                                                               For j = 0 To EngineSkinWeightNormalization.Slots - 1
                                                                   If ckWv(j) > 0.0F Then
                                                                       Dim idxc = skinFlatIdx(baseIdx + j)
                                                                       If idxc >= 0 AndAlso idxc < matsBind.Length Then
                                                                           MposeBlend += matsPose(idxc) * CDbl(ckWv(j))
                                                                           MbindBlend += matsBind(idxc) * CDbl(ckWv(j))
                                                                       End If
                                                                   End If
                                                               Next
                                                           Else
                                                           For j = 0 To cnt
                                                               sumW += CType(skinFlatWgt(baseIdx + j), Double)
                                                           Next
                                                           If sumW = 0F Then
                                                               Dim idx0 = If(cnt >= 0, skinFlatIdx(baseIdx), CByte(0))
                                                               Dim idx0c = Math.Max(0, Math.Min(CInt(idx0), matsBind.Length - 1))
                                                               MposeBlend = matsPose(idx0c)
                                                               MbindBlend = matsBind(idx0c)
                                                           Else
                                                               For j = 0 To cnt
                                                                   Dim w = CType(skinFlatWgt(baseIdx + j), Double) / sumW
                                                                   Dim idx = skinFlatIdx(baseIdx + j)
                                                                   If idx >= 0 AndAlso idx < matsBind.Length Then
                                                                       MposeBlend += matsPose(idx) * w
                                                                       MbindBlend += matsBind(idx) * w
                                                                   End If
                                                               Next
                                                           End If
                                                           End If
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
                                                       worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                       worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                       ' T y B van con la MATRIZ, no con la inversa-transpuesta:
                                                       ' son direcciones SOBRE la superficie. La
                                                       ' inversa-transpuesta es la ley de la NORMAL.
                                                       worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), totalSkinMat))
                                                       worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), totalSkinMat))
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
                                                       worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                       worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                       ' T y B van con la MATRIZ, no con la inversa-transpuesta:
                                                       ' son direcciones SOBRE la superficie. La
                                                       ' inversa-transpuesta es la ley de la NORMAL.
                                                       worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), totalSkinMat))
                                                       worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), totalSkinMat))
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
                                                       worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                       worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                       ' T y B van con la MATRIZ, no con la inversa-transpuesta:
                                                       ' son direcciones SOBRE la superficie. La
                                                       ' inversa-transpuesta es la ley de la NORMAL.
                                                       worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), totalSkinMat))
                                                       worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), totalSkinMat))
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
    Public Shared Sub InjectNormalsToTrishape(ByRef geom As SkinnedGeometry)
        Dim shapeGeom = geom.Geometry
        If shapeGeom Is Nothing Then Exit Sub
        Dim nNew = geom.Vertices.Length
        If nNew = 0 Then Exit Sub

        Dim norN As New List(Of System.Numerics.Vector3)(nNew)
        Dim tanN As New List(Of System.Numerics.Vector3)(nNew)
        Dim bitN As New List(Of System.Numerics.Vector3)(nNew)
        For i = 0 To nNew - 1
            Dim n1 = geom.Normals(i) : norN.Add(New System.Numerics.Vector3(CSng(n1.X), CSng(n1.Y), CSng(n1.Z)))
            Dim t1 = geom.Tangents(i) : tanN.Add(New System.Numerics.Vector3(CSng(t1.X), CSng(t1.Y), CSng(t1.Z)))
            Dim b1 = geom.Bitangents(i) : bitN.Add(New System.Numerics.Vector3(CSng(b1.X), CSng(b1.Y), CSng(b1.Z)))
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
    ''' <para>⛔⛔ ACA HABIA UNA DIVERGENCIA REAL CON EL CAMINO EAGER, arreglada 2026-08-03. El
    ''' docstring afirmaba que blendear <c>BoneMatsPose</c> crudo era "bit-idéntico al cálculo eager
    ''' porque GlobalTransform es Identity". <b>Es falso.</b> El eager blendea
    ''' <c>GlobalTransform * matsPose(k)</c>, y ese producto con la identidad NO es la identidad a
    ''' nivel de bits: el elemento (1,j) sale de <c>1·M1j + 0·M2j + 0·M3j + 0·M4j</c>, o sea que suma
    ''' ceros. Con <c>M1j = -0.0</c> el resultado es <c>+0.0</c> (IEEE: la suma de ceros de signo
    ''' opuesto da +0), y con cualquier ±Inf en la columna daria NaN. O sea que el mismo vértice podía
    ''' salir con el SIGNO DEL CERO distinto según si la pasada 2 se había salteado o no — y ese bit
    ''' se escribe tal cual al NIF cuando la coordenada es exactamente cero.
    ''' <br/>Peor que el síntoma: era una bomba latente. El día que <c>GlobalTransform</c> deje de ser
    ''' <c>Matrix4d.Identity</c> (hoy está hardcodeado así en Extract y en Recompute), los dos caminos
    ''' divergirían de verdad, en silencio y solo después de un play en GPU.
    ''' <br/>El arreglo aplica el MISMO producto que el eager, tomando el transform de
    ''' <c>geo.ParentGlobalTransform</c> — que es justo el <c>GlobalTransform</c> que
    ''' ExtractSkinnedGeometry guardó. Se eligió alinear el lazy CON el eager (y no al revés) porque el
    ''' eager es el que produce los bytes del corpus: mover el eager habría movido bytes.</para>
    ''' <para>⭐ La OTRA mitad —saltear la pasada 2 en GPU-skin opaco en play— NO es una discrepancia y
    ''' se deja como está: en CPU-skin nunca se saltea, y en GPU-skin el salto marca
    ''' <c>PerVertexMatrixValid=False</c> de modo que cualquier lector pasa por acá antes de leer. Es
    ''' una recomposición perezosa correcta, siempre que las DOS fórmulas coincidan — que es
    ''' exactamente lo que faltaba.</para>
    ''' </summary>
    Private Shared Sub EnsurePerVertexSkinMatrix(ByRef geo As SkinnedGeometry)
        If geo.PerVertexMatrixValid Then Return
        Dim mats = geo.PerVertexSkinMatrix
        If mats Is Nothing Then Return
        Dim vc = mats.Length
        Dim poseMats = geo.BoneMatsPose
        Dim flatIdx = geo.Skinning.BoneIndices
        Dim flatWgt = geo.Skinning.BoneWeights
        Dim wpv = If(geo.Skinning.WeightsPerVertex > 0, geo.Skinning.WeightsPerVertex, 4)
        Dim hasSkin = (flatIdx IsNot Nothing AndAlso flatWgt IsNot Nothing AndAlso geo.Skinning.VertexCount = vc AndAlso poseMats IsNot Nothing)
        If hasSkin Then
            ' MISMA paleta que el eager: GlobalTransform × matsPose(k). Ver la nota del docstring.
            Dim g = geo.ParentGlobalTransform
            Dim precomputed(poseMats.Length - 1) As Matrix4d
            For k = 0 To poseMats.Length - 1
                precomputed(k) = g * poseMats(k)
            Next
            Dim flatPalette = FastGeom.BuildFlatPalette(precomputed)
            Dim body As Action(Of Integer) = Sub(i) mats(i) = BlendBoneMatrices(flatWgt, flatIdx, i * wpv, wpv, precomputed, flatPalette)
            If vc >= 500 Then
                Parallel.For(0, vc, body)
            Else
                For i = 0 To vc - 1 : body(i) : Next
            End If
        End If
        geo.PerVertexMatrixValid = True
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
                For i = rango.Item1 To rango.Item2 - 1
                                       wv(i) = Vector3d.TransformPosition(localVerts(i), localMats(i))
                                       Dim nm = Create_Normal_Matrix(localMats(i))
                                       wn(i) = Vector3d.Normalize(Vector3d.TransformNormal(localNorms(i), nm))
                Next
            End Sub)
        geo.CachedWorldVertices = wv
        geo.CachedWorldNormals = wn
        geo.WorldCacheValid = True
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
    ''' <para>⛔ SYNC: CPU/GPU skinning — esta composición tiene que coincidir con la de
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

                Dim m = GlobalTransform * matPose
                precomputedBoneMatrices(k) = m
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
            Dim localFlatIdx = geo.Skinning.BoneIndices
            Dim localFlatWgt = geo.Skinning.BoneWeights
            Dim localWpv = If(geo.Skinning.WeightsPerVertex > 0, geo.Skinning.WeightsPerVertex, 4)
            Dim localHasSkin = (localFlatIdx IsNot Nothing AndAlso localFlatWgt IsNot Nothing AndAlso geo.Skinning.VertexCount = vertexCount)
            Dim localPrecomputed = precomputedBoneMatrices
            ' Paleta plana para el blend vectorial: una vez por shape, no por vertice. Ver FastGeom.
            Dim localFlatPalette = FastGeom.BuildFlatPalette(precomputedBoneMatrices)

            Dim skinBody As Action(Of Integer) = Sub(i)
                                                     If localHasSkin Then
                                                         perVertexSkinMatrix(i) = BlendBoneMatrices(localFlatWgt, localFlatIdx, i * localWpv, localWpv, localPrecomputed, localFlatPalette)
                                                     Else
                                                         perVertexSkinMatrix(i) = If(localPrecomputed.Length > 0, localPrecomputed(0), Matrix4d.Identity)
                                                     End If
                                                 End Sub

            If updatePerVertexSkin Then
                If vertexCount >= 500 Then
                    Parallel.For(0, vertexCount, skinBody)
                Else
                    For i = 0 To vertexCount - 1
                        skinBody(i)
                    Next
                End If
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
            Array.Fill(geo.PerVertexSkinMatrix, Mtot)
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
        Public Tri_du1 As Double()
        Public Tri_dv1 As Double()
        Public Tri_du2 As Double()
        Public Tri_dv2 As Double()
        Public Tri_det As Double()
    End Structure

    ' -------------------------------
    ' Opciones de calidad / robustez
    ' -------------------------------
    ' ⛔ HAY UNA SOLA LEY de ponderado, la de BodySlide: la normal de cara se acumula SIN normalizar
    ' (pesa por area) y la base tangente se normaliza por triangulo y se acumula sin peso. El
    ' `NormalWeightMode` configurable (area / angulo / area x angulo) se fue el 2026-08-03: dejaba el
    ' marco tangente rotado respecto del canonico, que es el marco contra el que se autoran los
    ' normal maps del ecosistema. Una clave `WeightMode` en un config.json viejo se ignora.
    Public Structure TBNOptions
        Public Property EpsilonPos As Double                    ' umbral para degenerados geométricos
        Public Property EpsilonUV As Double                     ' umbral para degenerados en UV (det≈0)
        Public Property NormalizeOutputs As Boolean             ' normalizar N/T/B al final
        Public Property RepairNaNs As Boolean                   ' si True: reemplaza NaN por vectores seguros

        ''' <summary>
        ''' Sólo TANGENTES: ortogonaliza contra la normal ALMACENADA y no reescribe <c>geo.Normals</c>.
        ''' Es lo que hace <c>CalcTangentsForShape</c> del canónico, que corre INCONDICIONAL en la
        ''' fase 3 del build (BodySlideApp.cpp:4501 y :4529) mientras las normales van aparte y
        ''' gateadas por <c>lockNormals</c> (:4494).
        ''' Hace falta cuando lo único que cambió son las UVs — la base tangente se deriva de ellas,
        ''' las normales no. ⛔ NO alcanza con recalcular todo y restaurar <c>Normals</c> después: el
        ''' Gram-Schmidt de abajo ortogonaliza T (y deriva B) contra la N RECALCULADA, así que
        ''' restaurarla al final dejaba una base que no es ortonormal respecto de la normal que
        ''' finalmente queda en la geometría.
        ''' Con <c>EnableWelding</c> cada miembro del grupo conserva SU normal, asi que T/B se
        ''' reortogonalizan por miembro: propagarle la del maestro (que es lo que se hace cuando la
        ''' opcion esta apagada, porque ahi la normal del maestro TAMBIEN se le escribe) dejaria la
        ''' base torcida respecto de la normal que el miembro se queda.
        ''' </summary>
        Public Property KeepExistingNormals As Boolean

        ' --- Welding (opcional) ---
        Public Property EnableWelding As Boolean                ' activa agrupación por posición+UV
        Public Property WeldPosEpsilon As Double                ' tolerancia para posición (en unidades del modelo)
        Public Property WeldUVEpsilon As Double                 ' tolerancia para UV (u,v)
        Public Property WeldByPositionOnly As Boolean           ' Only positions or positions + UV
    End Structure

    Public Shared Function DefaultTBNOptions() As TBNOptions
        Return New TBNOptions With {
                .EpsilonPos = 0.000000000001,
                .EpsilonUV = 0.000000000001,
                .NormalizeOutputs = True,
                .RepairNaNs = True,
                .EnableWelding = False,                ' desactivado por defecto
                .WeldPosEpsilon = 0.000000000001,
                .WeldUVEpsilon = 0.000000000001,
                .WeldByPositionOnly = False           ' Positions + UV
            }
    End Function

    ' =========================================================================
    ' BUILD CACHE (llamar una sola vez al cargar o cuando cambien UV o índices)
    ' - Precomputa:
    '   * VertexToTriangles (adjacencia)
    '   * Derivadas UV por triángulo (du1,dv1,du2,dv2,det)
    ' =========================================================================
    Public Shared Function BuildTBNCache(ByRef Uvs_Weight() As Vector3, ByVal indices As UInteger()) As TBNCache
        Dim nVerts As Integer = Uvs_Weight.Length
        Dim triCount As Integer = indices.Length \ 3

        ' ⭐ ADYACENCIA EN CSR PLANO (offsets + array), no una List(Of Integer) por vertice.
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
        Dim du1(triCount - 1) As Double
        Dim dv1(triCount - 1) As Double
        Dim du2(triCount - 1) As Double
        Dim dv2(triCount - 1) As Double
        Dim det(triCount - 1) As Double

        For t As Integer = 0 To triCount - 1
            Dim i0 As Integer = CInt(indices(3 * t + 0))
            Dim i1 As Integer = CInt(indices(3 * t + 1))
            Dim i2 As Integer = CInt(indices(3 * t + 2))

            If i0 >= nVerts OrElse i1 >= nVerts OrElse i2 >= nVerts Then Continue For
            ' Se guarda `triangulo*4 + esquina`, no sólo el triángulo. La esquina es lo que el
            ' consumidor tenía que redescubrir comparando los 3 índices del triángulo contra el
            ' vértice — 3 loads y 3 comparaciones por entrada que ahora no existen. Y un vértice
            ' repetido en un triángulo degenerado queda como DOS entradas, que es exactamente el
            ' doble aporte que antes se contaba a mano.
            v2tData(cursor(i0)) = t * 4 : cursor(i0) += 1
            v2tData(cursor(i1)) = t * 4 + 1 : cursor(i1) += 1
            v2tData(cursor(i2)) = t * 4 + 2 : cursor(i2) += 1


            ' UV del tri
            Dim uv0 As Vector3 = Uvs_Weight(i0)
            Dim uv1 As Vector3 = Uvs_Weight(i1)
            Dim uv2 As Vector3 = Uvs_Weight(i2)

            Dim _du1 As Double = uv1.X - uv0.X
            Dim _dv1 As Double = uv1.Y - uv0.Y
            Dim _du2 As Double = uv2.X - uv0.X
            Dim _dv2 As Double = uv2.Y - uv0.Y

            du1(t) = _du1 : dv1(t) = _dv1
            du2(t) = _du2 : dv2(t) = _dv2
            det(t) = _du1 * _dv2 - _du2 * _dv1
        Next

        Return New TBNCache With {
            .Indices = indices,
            .TriCount = triCount,
            .V2TStart = v2tStart,
            .V2TData = v2tData,
            .Tri_du1 = du1, .Tri_dv1 = dv1,
            .Tri_du2 = du2, .Tri_dv2 = dv2,
            .Tri_det = det
        }
    End Function

    ''' <summary>
    ''' Refresca SOLO las derivadas UV por triangulo de los triangulos incidentes a
    ''' <paramref name="verticesTocados"/>, conservando el resto del cache.
    '''
    ''' ⭐ Existe para no tirar el cache entero cuando lo unico que se movio son UVs. El cache tiene
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

            Dim _du1 As Double = uv1.X - uv0.X
            Dim _dv1 As Double = uv1.Y - uv0.Y
            Dim _du2 As Double = uv2.X - uv0.X
            Dim _dv2 As Double = uv2.Y - uv0.Y

            c.Tri_du1(t) = _du1 : c.Tri_dv1(t) = _dv1
            c.Tri_du2(t) = _du2 : c.Tri_dv2(t) = _dv2
            c.Tri_det(t) = _du1 * _dv2 - _du2 * _dv1
        Next
    End Sub

    ' ===========================================================================================
    ' API PÚBLICA: Recalcular N/T/B SOLO para la clausura afectada (dirty + sus triángulos)
    ' - Usa el cache (adjacencia + UV-derivs). Welding opcional (NO cacheado).
    ' ===========================================================================================
    Public Shared Function RecalculateNormalsTangentsBitangents(ByRef geo As SkinnedGeometry, ByVal opts As TBNOptions) As HashSet(Of Integer)
        If IsNothing(geo.CachedTBN.Indices) Then
            geo.CachedTBN = BuildTBNCache(geo.Uvs_Weight, geo.Indices)
        End If
        Dim nVerts As Integer = geo.Vertices.Length

        Dim Vertices_Adicionales As New HashSet(Of Integer)
        If nVerts = 0 OrElse geo.dirtyVertexIndices Is Nothing OrElse geo.dirtyVertexIndices.Count = 0 Then
            Return Vertices_Adicionales ' nada que hacer; si querés todo, pasá todos los índices como dirty
        End If

        ' -------- (Opcional) Welding lógico por posición+UV (NO cacheado) --------
        ' ⭐ `membersOf Is Nothing` SIGNIFICA IDENTIDAD (cada vértice es su propio grupo, él solo).
        ' Con el welding apagado —que es el default— materializar eso costaba un Dictionary de N
        ' entradas MÁS una List(Of Integer) de un elemento por vértice: ~45.000 objetos por recálculo
        ' en un cuerpo de 22.700 vértices, para no representar nada. Los dos consumidores tienen su
        ' rama de identidad, así que sacarlo es byte-idéntico.
        Dim masterOf() As Integer = Nothing
        Dim membersOf As Dictionary(Of Integer, List(Of Integer)) = Nothing
        If opts.EnableWelding Then
            Vertices_Adicionales.UnionWith(BuildWeldGroups(geo, opts.WeldPosEpsilon, opts.WeldUVEpsilon, opts.WeldByPositionOnly, masterOf, membersOf))
        Else
            masterOf = New Integer(nVerts - 1) {}
            For i As Integer = 0 To nVerts - 1
                masterOf(i) = i
            Next
        End If

        ' -------- 1) Triángulos afectados via adjacencia --------
        Dim affectedTris As New HashSet(Of Integer)()
        Dim v2tS = geo.CachedTBN.V2TStart
        Dim v2tD = geo.CachedTBN.V2TData
        For Each vi In geo.dirtyVertexIndices
            If vi < 0 OrElse vi >= nVerts Then Continue For
            For k = v2tS(vi) To v2tS(vi + 1) - 1
                affectedTris.Add(v2tD(k) >> 2)
            Next
        Next
        If affectedTris.Count = 0 Then Return Vertices_Adicionales

        ' -------- 2) Clausura de vértices a actualizar (incluye grupos por maestro si hay welding) --------
        Dim affectedVerts As New HashSet(Of Integer)(geo.dirtyVertexIndices)
        For Each t In affectedTris
            Dim i0 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 0))
            Dim i1 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 1))
            Dim i2 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 2))
            affectedVerts.Add(i0) : affectedVerts.Add(i1) : affectedVerts.Add(i2)
            affectedVerts.Add(masterOf(i0)) : affectedVerts.Add(masterOf(i1)) : affectedVerts.Add(masterOf(i2))
            Vertices_Adicionales.Add(i0)
            Vertices_Adicionales.Add(i1)
            Vertices_Adicionales.Add(i2)
            Vertices_Adicionales.Add(masterOf(i0))
            Vertices_Adicionales.Add(masterOf(i1))
            Vertices_Adicionales.Add(masterOf(i2))
        Next

        ' -------- 3) Acumuladores: sparse cuando el update es parcial, full cuando es masivo --------
        Dim useFullArrays As Boolean = (affectedTris.Count > geo.CachedTBN.TriCount * 0.4)
        Dim nAccum() As Vector3d = Nothing
        Dim tAccum() As Vector3d = Nothing
        Dim bAccum() As Vector3d = Nothing
        Dim sparseN As Dictionary(Of Integer, Vector3d) = Nothing
        Dim sparseT As Dictionary(Of Integer, Vector3d) = Nothing
        Dim sparseB As Dictionary(Of Integer, Vector3d) = Nothing

        If useFullArrays Then
            nAccum = New Vector3d(nVerts - 1) {}
            tAccum = New Vector3d(nVerts - 1) {}
            bAccum = New Vector3d(nVerts - 1) {}
        Else
            Dim capacity = affectedVerts.Count
            sparseN = New Dictionary(Of Integer, Vector3d)(capacity)
            sparseT = New Dictionary(Of Integer, Vector3d)(capacity)
            sparseB = New Dictionary(Of Integer, Vector3d)(capacity)
        End If

        ' -------- 4) Accumulate per-face contributions --------
        ' Parallel when triangle count is large enough to amortize overhead.
        ' Each thread accumulates into thread-local dictionaries, then merged.
        Dim triArray = affectedTris.ToArray()
        Dim epsPos As Double = opts.EpsilonPos
        Dim epsUV As Double = opts.EpsilonUV
        Dim localIndices = geo.CachedTBN.Indices
        Dim localVerts = geo.Vertices
        Dim localDu1 = geo.CachedTBN.Tri_du1
        Dim localDv1 = geo.CachedTBN.Tri_dv1
        Dim localDu2 = geo.CachedTBN.Tri_du2
        Dim localDv2 = geo.CachedTBN.Tri_dv2
        Dim localDet = geo.CachedTBN.Tri_det
        Dim localMasterOf = masterOf

        If useFullArrays AndAlso triArray.Length >= 2000 Then
            ' ===== Camino PARALELO — SCATTER reemplazado por GATHER =====
            '
            ' ⛔ EL BUG QUE ESTO ARREGLA (medido 2026-08-03). La version anterior era:
            '
            '     Dim x1 = New Vector3d(nVerts - 1) {}
            '     Dim threadLocalN As New Threading.ThreadLocal(Of Vector3d())(Function() x1, trackAllValues:=True)
            '
            ' La fabrica devolvia SIEMPRE EL MISMO array x1, asi que los "thread locals" no eran
            ' locales de nadie: todos los hilos acumulaban sobre el MISMO buffer y `tlN(m) += ...`
            ' era un read-modify-write concurrente ⇒ se perdian actualizaciones, distintas en cada
            ' corrida. (El merge ademas recorria .Values y sumaba N veces el mismo array, pero eso
            ' solo ESCALA el acumulador y la normalizacion final lo cancelaba; lo que rompia era la
            ' carrera.) Sintoma medido: construir DOS VECES el mismo proyecto daba NIF distintos —
            ' 2 o 3 de cada 243 en el corpus de FO4, y no siempre los mismos, con posiciones, UV y
            ' triangulos IDENTICOS y hasta 1,78 de delta en una normal (o sea una normal apuntando a
            ' otro lado, no ruido de ultimo bit). El self-test TB3 lo reproduce en proceso.
            '
            ' ⭐ POR QUE GATHER Y NO "un array por hilo". Darle a cada hilo su propio buffer saca la
            ' carrera pero NO alcanza para que el resultado sea reproducible: el merge recorreria
            ' .Values, cuyo orden depende del scheduler, y la suma flotante no es asociativa. Ademas
            ' la cantidad de hilos cambia entre maquinas, y la app SE DISTRIBUYE: el resultado no
            ' puede depender de cuantos nucleos tenga quien la corre.
            '
            ' Con gather cada vertice maestro lo escribe UNA sola iteracion, sumando en un orden
            ' fijado por el cache (miembros del grupo x VertexToTriangles), que es el mismo en toda
            ' maquina y en toda corrida. Sin carrera y sin dependencia del paralelismo.
            '
            ' Fase A (paralela, una escritura por triangulo en SU indice) precomputa la normal de
            ' cara, la base tangente y los tres pesos por esquina; fase B (paralela, una escritura
            ' por maestro) los suma. La fase A evita recalcular cross/angulos las 3 veces que la
            ' fase B visita cada triangulo; cuesta 96 bytes por triangulo afectado.
            '
            ' ⚠️ El resultado NO es bit-identico al del scatter (cambia el orden de suma), asi que
            ' este cambio MUEVE BYTES una vez. A partir de ahi es estable.
            Dim nTri As Integer = triArray.Length
            ' ⭐ N, T y B de la cara INTERLEAVADOS en un solo array, 3 por triangulo. Son temporales
            ' internos, asi que el layout es libre: la fase B lee los tres juntos, y asi salen de UN
            ' bloque contiguo de 72 bytes en vez de tres indexaciones a tres arrays distintos.
            Dim face((nTri * 3) - 1) As Vector3d
            ' triangulo -> su lugar en los arrays de arriba, o -1 si no esta afectado. El centinela
            ' es -1 y no 0 porque el triangulo 0 es un indice valido.
            Dim slotOf(geo.CachedTBN.TriCount - 1) As Integer
            For k = 0 To slotOf.Length - 1
                slotOf(k) = -1
            Next
            For k = 0 To nTri - 1
                slotOf(triArray(k)) = k
            Next

            ' --- FASE A: por triangulo. Cada iteracion escribe solo en su propio indice k.
            Parallel.ForEach(SkinningHelper.RangosDe(nTri),
                Sub(range As Tuple(Of Integer, Integer))
                    For k = range.Item1 To range.Item2 - 1
                        Dim t = triArray(k)
                        Dim i0 As Integer = CInt(localIndices(3 * t))
                        Dim i1 As Integer = CInt(localIndices(3 * t + 1))
                        Dim i2 As Integer = CInt(localIndices(3 * t + 2))
                        Dim p0 = localVerts(i0), p1 = localVerts(i1), p2 = localVerts(i2)
                        Dim e1 = p1 - p0, e2 = p2 - p0
                        Dim fn = Vector3d.Cross(e1, e2)
                        Dim area2 = fn.Length
                        ' Cara degenerada: queda todo en cero y no aporta, igual que el Exit Sub de
                        ' AccumulateTriangle.
                        If area2 <= epsPos Then Continue For

                        Dim tf As Vector3d, bf As Vector3d
                        ComputeFaceTB(fn, e1, e2, localDu1(t), localDv1(t), localDu2(t), localDv2(t),
                                      localDet(t), epsPos, epsUV, tf, bf)
                        Dim fb = k * 3
                        face(fb) = fn : face(fb + 1) = tf : face(fb + 2) = bf
                    Next
                End Sub)

            ' --- FASE B: por vertice MAESTRO. Cada iteracion escribe solo nAccum(m)/tAccum(m)/bAccum(m).
            ' Se recorre (miembros del grupo) x (triangulos incidentes del miembro), y de cada
            ' triangulo se suma SOLO la esquina que ES ese miembro. Asi cada par (triangulo, esquina)
            ' se visita exactamente una vez, que es el mismo multiconjunto de aportes que hacia el
            ' scatter: no se puede contar dos veces un triangulo que toque dos miembros del grupo.
            Dim maestros As New List(Of Integer)()
            Dim vistos As New HashSet(Of Integer)()
            For Each vi In affectedVerts
                Dim m = localMasterOf(vi)
                If vistos.Add(m) Then maestros.Add(m)
            Next
            Dim maestroArr = maestros.ToArray()
            Dim locS = geo.CachedTBN.V2TStart
            Dim locD = geo.CachedTBN.V2TData
            Dim locMembers = membersOf

            Parallel.ForEach(SkinningHelper.RangosDe(maestroArr.Length),
                Sub(range As Tuple(Of Integer, Integer))
                    ' Buffer reusado: sin welding el grupo es SIEMPRE {m}, y alocar una List por
                    ' vertice para eso era el grueso de las allocations de esta fase.
                    Dim unoSolo(0) As Integer
                    For ci = range.Item1 To range.Item2 - 1
                        Dim m = maestroArr(ci)
                        Dim miembros As IList(Of Integer)
                        If locMembers Is Nothing Then
                            unoSolo(0) = m
                            miembros = unoSolo
                        Else
                            ' ⛔ La variable de salida del TryGetValue tiene que ser del tipo EXACTO:
                            ' castearla en el sitio del argumento manda el valor a un temporal y la
                            ' local queda en Nothing. Lo cazo TB6b (paralelo != secuencial con welding).
                            Dim lst As List(Of Integer) = Nothing
                            If Not locMembers.TryGetValue(m, lst) Then Continue For
                            miembros = lst
                        End If
                        If miembros Is Nothing Then Continue For
                        Dim accN As Vector3d = Vector3d.Zero
                        Dim accT As Vector3d = Vector3d.Zero
                        Dim accB As Vector3d = Vector3d.Zero
                        For Each vi In miembros
                            If vi < 0 OrElse vi >= locS.Length - 1 Then Continue For
                            ' Una entrada del CSR = UNA incidencia (una esquina de un triangulo que es
                            ' este vertice), asi que se suma una vez y listo: ya no hay que releer los
                            ' 3 indices del triangulo ni contar cuantas esquinas coinciden. Un vertice
                            ' repetido en un triangulo degenerado son dos entradas, o sea el mismo
                            ' doble aporte (y f+f = f*2 exacto en punto flotante).
                            ' El aporte no lleva peso: la normal de cara viene sin normalizar (pesa
                            ' por area) y la base tangente viene normalizada por triangulo.
                            For kk = locS(vi) To locS(vi + 1) - 1
                                Dim k = slotOf(locD(kk) >> 2)
                                If k < 0 Then Continue For   ' triangulo no afectado por este update
                                Dim fb = k * 3
                                accN += face(fb)
                                accT += face(fb + 1)
                                accB += face(fb + 2)
                            Next
                        Next
                        nAccum(m) = accN : tAccum(m) = accT : bAccum(m) = accB
                    Next
                End Sub)
        Else
            ' Sequential path: direct accumulation (full arrays or sparse)
            For Each t In triArray
                If useFullArrays Then
                    AccumulateTriangle(t, localIndices, localVerts, localMasterOf,
                                       localDu1, localDv1, localDu2, localDv2, localDet,
                                       epsPos, epsUV, nAccum, tAccum, bAccum)
                Else
                    AccumulateTriangleSparse(t, localIndices, localVerts, localMasterOf,
                                             localDu1, localDv1, localDu2, localDv2, localDet,
                                             epsPos, epsUV, sparseN, sparseT, sparseB)
                End If
            Next
        End If

        ' -------- 5) Finalize masters and propagate to all group members --------
        Dim candidates As New HashSet(Of Integer)()
        For Each vi In affectedVerts
            candidates.Add(localMasterOf(vi))
        Next

        For Each m As Integer In candidates
            Dim NX As Vector3d = Nothing
            Dim TX As Vector3d = Nothing
            Dim Tb As Vector3d = Nothing
            If useFullArrays = False Then If sparseN.TryGetValue(m, NX) = False Then NX = Vector3d.Zero
            If useFullArrays = False Then If sparseT.TryGetValue(m, TX) = False Then TX = Vector3d.Zero
            If useFullArrays = False Then If sparseB.TryGetValue(m, Tb) = False Then Tb = Vector3d.Zero

            Dim N As Vector3d = If(useFullArrays, nAccum(m), NX)
            Dim T As Vector3d = If(useFullArrays, tAccum(m), TX)
            Dim B As Vector3d = If(useFullArrays, bAccum(m), Tb)

            ' Copias de los ACUMULADOS, antes de que el Gram-Schmidt de abajo los pise. Las necesita
            ' la rama de miembros soldados de KeepExistingNormals: proyectar la T/B FINALES del
            ' maestro (ya ortogonalizadas contra SU normal) hacia la normal del miembro deriva su
            ' base — y su handedness — del maestro, que es justo lo que esa rama existe para no hacer.
            Dim Tacc As Vector3d = T
            Dim Bacc As Vector3d = B

            ' Normal
            If opts.KeepExistingNormals Then
                ' Sólo tangentes: la N es la que YA está en la geometría (ver KeepExistingNormals).
                N = geo.Normals(m)
            End If
            If N.LengthSquared <= epsPos OrElse HasNaN(N) Then
                N = New Vector3d(0, 0, 1)
            ElseIf opts.NormalizeOutputs Then
                N = Vector3d.Normalize(N)
            End If

            ' ⛔⛔ EL PRIMARIO ES **B**, NO T: decide el ROLL del marco alrededor de la normal.
            ' `tAcc` acumula ∂P/∂u y `bAcc` ∂P/∂v, y NO son perpendiculares (sesgo de la
            ' parametrizacion UV) ⇒ ortogonalizar A contra B no da lo mismo que B contra A.
            ' El que termina en el campo TANGENTE del NIF es `geo.Bitangents` (el adaptador cruza:
            ' SetTangents escribe el campo Bitangent), y ese tiene que ser el primario.
            ' Al reves el marco quedaba rotado: mediana 14,6° contra BodySlide, y la mitad de los
            ' vertices por encima de 15°. Con este orden, SSE sale BYTE-IDENTICO.
            Dim Bcross As Vector3d
            B -= N * Vector3d.Dot(N, B)
            If B.LengthSquared <= epsPos OrElse HasNaN(B) Then
                B = OrthonormalTangentFromNormal(N)
            ElseIf opts.NormalizeOutputs Then
                B = Vector3d.Normalize(B)
            End If

            ' El secundario se proyecta contra N y DESPUES contra el primario.
            Bcross = Vector3d.Cross(N, B)
            Dim Tproj As Vector3d = T - N * Vector3d.Dot(N, T)
            T = Tproj - B * Vector3d.Dot(B, Tproj)
            If T.LengthSquared <= epsPos OrElse HasNaN(T) Then
                T = Bcross
            ElseIf opts.NormalizeOutputs Then
                T = Vector3d.Normalize(T)
            End If

            If opts.RepairNaNs Then
                If HasNaN(B) Then B = Bcross
            End If

            ' Propagate to all members of the weld group
            ' Convención uniforme para los dos juegos: T->geo.Tangents, B->geo.Bitangents. El cruce
            ' hacia los campos del NIF lo hace el adaptador de shape, no esto.
            Dim escribeNormales As Boolean = Not opts.KeepExistingNormals
            ' Sin welding (membersOf Is Nothing) el grupo es {m}: se escribe directo y se evita el
            ' lookup. Es exactamente la rama `vi = m` de abajo.
            If membersOf Is Nothing Then
                If escribeNormales Then geo.Normals(m) = N
                geo.Tangents(m) = T
                geo.Bitangents(m) = B
                Continue For
            End If
            Dim members As List(Of Integer) = Nothing
            If membersOf.TryGetValue(m, members) Then
                For Each vi As Integer In members
                    If escribeNormales Then
                        geo.Normals(vi) = N
                        geo.Tangents(vi) = T
                        geo.Bitangents(vi) = B
                    ElseIf vi = m Then
                        ' El maestro: T y B ya se calcularon contra geo.Normals(m). Escribir tal cual
                        ' deja este camino BYTE-IDENTICO al de siempre cuando no hay welding — y sin
                        ' welding `membersOf` tiene un singleton por vertice, asi que este es el caso
                        ' normal, no una excepcion.
                        geo.Tangents(vi) = T
                        geo.Bitangents(vi) = B
                    Else
                        ' Miembro soldado que conserva SU normal (no se le escribe la del maestro):
                        ' la base se reortogonaliza contra esa, o queda torcida. Parte de Tacc/Bacc
                        ' (los ACUMULADOS del grupo), no de la T/B ya ortogonalizadas del maestro, y
                        ' cierra con el mismo doble Gram-Schmidt que el camino del maestro.
                        Dim Nv As Vector3d = geo.Normals(vi)
                        If Nv.LengthSquared <= epsPos OrElse HasNaN(Nv) Then
                            Nv = New Vector3d(0, 0, 1)
                        ElseIf opts.NormalizeOutputs Then
                            Nv = Vector3d.Normalize(Nv)
                        End If
                        ' Mismo orden que el camino del maestro: PRIMARIO el que va al campo tangente
                        ' del NIF (Bacc), y despues el otro proyectado contra el.
                        Dim Bv As Vector3d = Bacc - Nv * Vector3d.Dot(Nv, Bacc)
                        If Bv.LengthSquared <= epsPos OrElse HasNaN(Bv) Then
                            Bv = OrthonormalTangentFromNormal(Nv)
                        ElseIf opts.NormalizeOutputs Then
                            Bv = Vector3d.Normalize(Bv)
                        End If

                        Dim Bcrossv As Vector3d = Vector3d.Cross(Nv, Bv)
                        Dim Tprojv As Vector3d = Tacc - Nv * Vector3d.Dot(Nv, Tacc)
                        Dim Tv As Vector3d = Tprojv - Bv * Vector3d.Dot(Bv, Tprojv)
                        If Tv.LengthSquared <= epsPos OrElse HasNaN(Tv) Then
                            Tv = Bcrossv
                        ElseIf opts.NormalizeOutputs Then
                            Tv = Vector3d.Normalize(Tv)
                        End If
                        If opts.RepairNaNs AndAlso HasNaN(Tv) Then Tv = Bcrossv

                        geo.Tangents(vi) = Tv
                        geo.Bitangents(vi) = Bv
                    End If
                Next
            Else
                If escribeNormales Then geo.Normals(m) = N
                geo.Tangents(m) = T
                geo.Bitangents(m) = B
            End If
        Next
        Return Vertices_Adicionales
    End Function

    ' -----------------------
    ' Utilitarios privados
    ' -----------------------

    ' Welding lógico por posición+UV con tolerancias (NO cacheado)
    Private Shared Function BuildWeldGroups(ByRef geo As SkinnedGeometry, ByVal weldPosEpsOrig As Double, ByVal weldUVEps As Double, ByVal byPosOnly As Boolean, ByRef masterOf() As Integer, ByRef membersOf As Dictionary(Of Integer, List(Of Integer))) As HashSet(Of Integer)
        Dim n As Integer = geo.Vertices.Length
        Dim vertices_adicionales As New HashSet(Of Integer)
        masterOf = New Integer(n - 1) {}
        membersOf = New Dictionary(Of Integer, List(Of Integer))(n)
        Dim extent As Vector3d = geo.Maxv - geo.Minv
        Dim diag As Double = extent.Length
        Dim maxSpan As Double = Math.Max(Math.Max(Math.Abs(extent.X), Math.Abs(extent.Y)), Math.Abs(extent.Z))
        ' Heurística de epsilon relativo (elegí uno de los dos L)
        Dim L As Double = If(diag > 0.0, diag, maxSpan)
        ' Parámetros de control (ajustables)
        ' ⚠ PENDIENTE 1 — CONTRATO DE UNIDADES INCONSISTENTE (NO tocado acá a propósito).
        ' `weldPosEpsOrig` se DECLARA en unidades de modelo por los llamadores, pero acá se consume como
        ' FRACCIÓN de la escala de la malla (k * L). Las dos lecturas no pueden ser ambas correctas: el
        ' epsilon efectivo escala con el tamaño del mesh cuando el llamador creía estar pidiendo una
        ' distancia absoluta. Arreglarlo cambia el epsilon de TODAS las mallas a la vez ⇒ fuera del
        ' alcance "mínimo riesgo" de este cambio. Queda anotado, sin medir el impacto por malla.
        Dim k As Double = weldPosEpsOrig     ' fracción de la escala de la malla (ver PENDIENTE 1)
        Dim floorEps As Double = 0.000000000001
        ' Techo del epsilon de weld. FUENTE: CreationKit.exe 0x142677EF0 usa el MISMO predicado que
        ' ClosePos (comparación POR COMPONENTE, L∞) con tolerancia 1e-5 — constante @0x2FC4848 =
        ' 0x3727C5AC (float 1.0e-5). El 0,001 anterior era 100x más permisivo que el CK y sobre-soldaba.
        ' Mitigante: EnableWelding está en False por defecto, así que este camino hoy casi no corre.
        Dim ceilEps As Double = 0.00001   ' 1e-5 = tolerancia del CK (0x142677EF0), no 0.001

        Dim weldPosEps As Double
        If L <= 0.0 Then
            weldPosEps = floorEps
        Else
            weldPosEps = Math.Max(floorEps, Math.Min(ceilEps, k * L))
        End If

        If weldPosEps <= 0 OrElse (Not byPosOnly AndAlso weldUVEps <= 0) OrElse n = 0 Then
            For i As Integer = 0 To n - 1
                masterOf(i) = i
                membersOf(i) = New List(Of Integer)(1) From {i}
            Next
            Return vertices_adicionales
        End If

        ' Hash buckets por celda cuantizada.
        ' ⚠ PENDIENTE 2 — EL BUCKET NO MIRA CELDAS VECINAS (NO tocado acá a propósito).
        ' La clave cuantiza la posición a una celda y el chequeo fino sólo recorre los candidatos de ESA
        ' celda. Dos vértices a distancia < epsilon pero a ambos lados de un borde de celda caen en celdas
        ' distintas y NUNCA se comparan ⇒ no se sueldan aunque deberían. MEDIDO: el 64 % de los pares que
        ' pasarían el predicado de distancia nunca llegan a compararse. El arreglo es barrer las 26 celdas
        ' vecinas (3x3x3) además de la propia; es un cambio de comportamiento y de costo ⇒ fuera del
        ' alcance "mínimo riesgo" de este cambio.
        Dim buckets As New Dictionary(Of WeldKey, List(Of Integer))(n)

        For i As Integer = 0 To n - 1
            Dim p As Vector3d = geo.Vertices(i)
            Dim uv As Vector3 = geo.Uvs_Weight(i)

            ' Clave cuantizada por tolerancia (redondeo a celda)
            Dim key As WeldKey = WeldKey.From(p, uv, weldPosEps, weldUVEps, byPosOnly)

            Dim list As List(Of Integer) = Nothing
            If Not buckets.TryGetValue(key, list) Then
                list = New List(Of Integer)()
                buckets(key) = list
            End If

            ' Buscar en el bucket si ya existe un maestro compatible (chequeo fino)
            Dim assigned As Boolean = False
            For Each cand As Integer In list.ToList
                Dim posOk As Boolean = ClosePos(geo.Vertices(cand), p, weldPosEps)
                Dim uvOk As Boolean = byPosOnly OrElse CloseUV(geo.Uvs_Weight(cand), uv, weldUVEps)
                If posOk AndAlso uvOk Then
                    masterOf(i) = masterOf(cand)
                    membersOf(masterOf(cand)).Add(i)
                    list.Add(i)
                    vertices_adicionales.Add(i)
                    assigned = True
                    Exit For
                End If
            Next

            If Not assigned Then
                ' Nuevo grupo con i como maestro
                masterOf(i) = i
                list.Add(i)
                membersOf(i) = New List(Of Integer)(4) From {i}
            End If
        Next
        Return vertices_adicionales
    End Function


    ' Clave de bucket (cuantización por eps)
    Private Structure WeldKey
        Public qx As Long, qy As Long, qz As Long
        Public qu As Long, qv As Long

        Public Shared Function From(p As Vector3d, uv As Vector3, posEps As Double, uvEps As Double, byPosOnly As Boolean) As WeldKey
            Dim invPos As Double = If(posEps > 0.0, 1.0 / posEps, 0.0)
            Dim invUV As Double = If(uvEps > 0.0, 1.0 / uvEps, 0.0)

            Dim k As WeldKey
            k.qx = QuantizeToLong(p.X, invPos)
            k.qy = QuantizeToLong(p.Y, invPos)
            k.qz = QuantizeToLong(p.Z, invPos)
            If byPosOnly Then
                k.qu = 0 : k.qv = 0
            Else
                k.qu = QuantizeToLong(uv.X, invUV)
                k.qv = QuantizeToLong(uv.Y, invUV)
            End If
            Return k
        End Function

        Private Shared Function QuantizeToLong(val As Double, invStep As Double) As Long
            If invStep <= 0.0 Then Return 0
            If Double.IsNaN(val) OrElse Double.IsInfinity(val) Then Return 0
            Dim q As Double = Math.Round(val * invStep)
            Const LMAX As Double = 9.2233720368547758E+18
            Const LMIN As Double = -9.2233720368547758E+18
            If q > LMAX Then Return Long.MaxValue
            If q < LMIN Then Return Long.MinValue
            Return CLng(q)
        End Function

        Public Overrides Function GetHashCode() As Integer
            ' versión segura (sin overflow)
            Dim hc As New HashCode()
            hc.Add(qx) : hc.Add(qy) : hc.Add(qz) : hc.Add(qu) : hc.Add(qv)
            Return hc.ToHashCode()
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj IsNot WeldKey Then Return False
            Dim o As WeldKey = CType(obj, WeldKey)
            Return qx = o.qx AndAlso qy = o.qy AndAlso qz = o.qz AndAlso qu = o.qu AndAlso qv = o.qv
        End Function
    End Structure

    ' Comparación fina por componente (posición)
    Private Shared Function ClosePos(a As Vector3d, b As Vector3d, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps AndAlso Math.Abs(a.Z - b.Z) <= eps
    End Function

    ' Comparación fina por componente (UV)
    Private Shared Function CloseUV(a As Vector3, b As Vector3, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps
    End Function

    ' Core per-triangle accumulation logic — extracted to avoid duplication between sequential/parallel paths.
    Private Shared Sub AccumulateTriangle(t As Integer,
                                          indices As UInteger(), verts As Vector3d(), masterOf As Integer(),
                                          du1 As Double(), dv1 As Double(), du2 As Double(), dv2 As Double(), det As Double(),
                                          epsPos As Double, epsUV As Double,
                                          nAcc As Vector3d(), tAcc As Vector3d(), bAcc As Vector3d())
        Dim i0 As Integer = CInt(indices(3 * t)), i1 As Integer = CInt(indices(3 * t + 1)), i2 As Integer = CInt(indices(3 * t + 2))
        Dim m0 = masterOf(i0), m1 = masterOf(i1), m2 = masterOf(i2)
        Dim p0 = verts(i0), p1 = verts(i1), p2 = verts(i2)
        Dim e1 = p1 - p0, e2 = p2 - p0
        Dim fn = Vector3d.Cross(e1, e2)
        Dim area2 = fn.Length
        If area2 <= epsPos Then Exit Sub

        ' La normal de cara va SIN normalizar: eso ya la pondera por AREA. La base tangente viene
        ' normalizada por triangulo desde ComputeFaceTB y se acumula sin peso.
        Dim tFace As Vector3d, bFace As Vector3d
        ComputeFaceTB(fn, e1, e2, du1(t), dv1(t), du2(t), dv2(t), det(t), epsPos, epsUV, tFace, bFace)

        nAcc(m0) += fn : nAcc(m1) += fn : nAcc(m2) += fn
        tAcc(m0) += tFace : tAcc(m1) += tFace : tAcc(m2) += tFace
        bAcc(m0) += bFace : bAcc(m1) += bFace : bAcc(m2) += bFace
    End Sub

    ' Sparse variant for small partial updates — avoids allocating full-size arrays.
    Private Shared Sub AccumulateTriangleSparse(t As Integer,
                                                indices As UInteger(), verts As Vector3d(), masterOf As Integer(),
                                                du1 As Double(), dv1 As Double(), du2 As Double(), dv2 As Double(), det As Double(),
                                                epsPos As Double, epsUV As Double,
                                                nAcc As Dictionary(Of Integer, Vector3d),
                                                tAcc As Dictionary(Of Integer, Vector3d),
                                                bAcc As Dictionary(Of Integer, Vector3d))
        Dim i0 As Integer = CInt(indices(3 * t)), i1 As Integer = CInt(indices(3 * t + 1)), i2 As Integer = CInt(indices(3 * t + 2))
        Dim m0 = masterOf(i0), m1 = masterOf(i1), m2 = masterOf(i2)
        Dim p0 = verts(i0), p1 = verts(i1), p2 = verts(i2)
        Dim e1 = p1 - p0, e2 = p2 - p0
        Dim fn = Vector3d.Cross(e1, e2)
        Dim area2 = fn.Length
        If area2 <= epsPos Then Exit Sub

        ' ⛔ Este bloque tiene que ser IDENTICO al de AccumulateTriangle. Al migrar el ponderado me
        ' quede sin actualizarlo y el camino secuencial (sparse) siguio con el viejo mientras el
        ' paralelo usaba el nuevo: los tests TB5/TB6b (paralelo == secuencial) lo cazaron con un
        ' delta de 0,1. Si se toca uno, se tocan los dos.
        Dim tFace As Vector3d, bFace As Vector3d
        ComputeFaceTB(fn, e1, e2, du1(t), dv1(t), du2(t), dv2(t), det(t), epsPos, epsUV, tFace, bFace)

        Dim vn0 As Vector3d, vn1 As Vector3d, vn2 As Vector3d
        nAcc.TryGetValue(m0, vn0) : nAcc(m0) = vn0 + fn
        nAcc.TryGetValue(m1, vn1) : nAcc(m1) = vn1 + fn
        nAcc.TryGetValue(m2, vn2) : nAcc(m2) = vn2 + fn
        Dim vt0 As Vector3d, vt1 As Vector3d, vt2 As Vector3d
        tAcc.TryGetValue(m0, vt0) : tAcc(m0) = vt0 + tFace
        tAcc.TryGetValue(m1, vt1) : tAcc(m1) = vt1 + tFace
        tAcc.TryGetValue(m2, vt2) : tAcc(m2) = vt2 + tFace
        Dim vb0 As Vector3d, vb1 As Vector3d, vb2 As Vector3d
        bAcc.TryGetValue(m0, vb0) : bAcc(m0) = vb0 + bFace
        bAcc.TryGetValue(m1, vb1) : bAcc(m1) = vb1 + bFace
        bAcc.TryGetValue(m2, vb2) : bAcc(m2) = vb2 + bFace
    End Sub

    ''' <summary>
    ''' Tangente y bitangente de UNA cara, a partir de las aristas y las derivadas de UV cacheadas.
    ''' Del determinante se usa SOLO EL SIGNO, y cada direccion se NORMALIZA por triangulo: el
    ''' llamador las acumula sin peso. Es la ley de BodySlide, y es la que decide en que marco se
    ''' interpreta el normal map — las texturas del ecosistema se autoran contra ese marco.
    ''' </summary>
    Private Shared Sub ComputeFaceTB(fn As Vector3d, e1 As Vector3d, e2 As Vector3d,
                                      _du1 As Double, _dv1 As Double, _du2 As Double, _dv2 As Double, _det As Double,
                                      epsPos As Double, epsUV As Double,
                                      ByRef tFace As Vector3d, ByRef bFace As Vector3d)
        If Math.Abs(_det) <= epsUV Then
            ' Degenerate UV: stable fallback in face-normal plane
            Dim nf = Vector3d.Normalize(fn)
            Dim e1p = e1 - nf * Vector3d.Dot(nf, e1)
            If e1p.LengthSquared <= epsPos Then e1p = e2 - nf * Vector3d.Dot(nf, e2)
            If e1p.LengthSquared <= epsPos Then
                tFace = Vector3d.Zero
                bFace = Vector3d.Zero
            Else
                tFace = Vector3d.Normalize(e1p)
                bFace = Vector3d.Normalize(Vector3d.Cross(nf, tFace))
            End If
        Else
            Dim r As Double = If(_det >= 0.0, 1.0, -1.0)
            Dim tf = (e1 * _dv2 - e2 * _dv1) * r
            Dim bf = (e2 * _du1 - e1 * _du2) * r
            tFace = If(tf.LengthSquared > epsPos, Vector3d.Normalize(tf), Vector3d.Zero)
            bFace = If(bf.LengthSquared > epsPos, Vector3d.Normalize(bf), Vector3d.Zero)
        End If
    End Sub

    ' Tangente ortonormal a partir de una normal: elige un eje auxiliar poco alineado
    Private Shared Function OrthonormalTangentFromNormal(n As Vector3d) As Vector3d
        Dim ax As Vector3d = If(Math.Abs(n.X) < 0.9, New Vector3d(1, 0, 0), New Vector3d(0, 1, 0))
        Dim t As Vector3d = Vector3d.Cross(ax, n)
        If t.LengthSquared <= 1.0E-20 Then t = Vector3d.Cross(New Vector3d(0, 0, 1), n)
        If t.LengthSquared <= 1.0E-20 Then Return New Vector3d(1, 0, 0)
        Return Vector3d.Normalize(t)
    End Function

    Private Shared Function HasNaN(v As Vector3d) As Boolean
        Return Double.IsNaN(v.X) OrElse Double.IsNaN(v.Y) OrElse Double.IsNaN(v.Z)
    End Function

End Class
