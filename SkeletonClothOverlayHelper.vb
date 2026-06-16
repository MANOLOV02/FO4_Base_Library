' Version Uploaded of Fo4Library 3.2.0
Imports System.Linq
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics

' =============================================================================
' ESTADO: ACTIVO — ruta principal de bone injection para physics en el render.
' -----------------------------------------------------------------------------
' InjectMissingBonesIntoLiveSkeleton: llamado desde
'   SkeletonInstance.PrepareForShapes (per-instance cloth-bone injection).
' Parsea el hkaSkeleton del BSClothExtraData e inyecta los huesos de física
' que no existen en el esqueleto del juego como HierarchiBone_class temporales.
'
' LocalReferencePoseToTransform: usa OpenTK Matrix4 → Transform_Class(Matrix4).
' Es la implementación CORRECTA y consistente con el resto del render.
'
' AUTORÍA = BSClothExtraData: el hkaSkeleton de BSClothExtraData ES la rebanada de autoría
' embebida en el NIF (cloth-bones + ancla + bind + jerarquía). Leerlo y colgar los bones (esto)
' YA es usar la autoría directo — no hay que recalcular nada. El .hkx de autoría SUELTO
' (FemaleHair04.hkx, junto al NIF) tiene el skeleton COMPLETO de 201 huesos con el mismo bind de
' los cloth-bones (verificado: Δ<5e-4 u vs el embebido), pero el render no tiene el path del NIF
' (Nifcontent_Class_Manolo no guarda su filename), así que el embebido es la fuente in-memory.
' Si BSClothExtraData falta (CloneShape_Original no lo transfiere) la solución correcta es preservarlo
' en el clone / leer del NIF source — NO recalcular desde el skin. Ver [[arch_cloth_bones_inject]].
'
' PENDIENTES CONOCIDOS:
'  - LocalReferencePoseToTransform y ResolveUniformScale están duplicadas aquí
'    y en HclCollisionPoseHelper.vb. Candidatas a extraer a módulo compartido
'    cuando se decida conectar HclCollisionPoseHelper al render.
'  - NormalizeBoneName usa ToUpperInvariant(). Consistente con el resto de
'    bone lookups (OrdinalIgnoreCase). Revisar si hay casos edge con nombres
'    de huesos que usen caracteres no-ASCII.
' =============================================================================

Public NotInheritable Class SkeletonClothOverlayHelper_Class

    ' Caché por-BLOQUE del cloth hkaSkeleton parseado. La clave es el bloque BSClothExtraData (objeto
    ' estable, multi-block-safe). ParseClothSkeleton/ParseClothSkeletonFromBlock se invocan desde
    ' PrepareForShapes, que el path de pose-update corre EN CADA FRAME — sin caché, durante el play
    ' de una animación se re-parseaba el packfile Havok entero (Parse + BuildGraph + ParseSkeleton)
    ' de cada prenda con física ~60 veces/seg (el costo es del parse, no de la geometría: por eso
    ' lento aun con pocas shapes). El cloth-skeleton es INVARIANTE para un bloque dado, así que se
    ' parsea UNA vez por vida del bloque (no por NIF).
    ' ConditionalWeakTable: clave DÉBIL → cuando el bloque/NIF se libera (shape descargada/reemplazada
    ' por otra instancia), la entrada se evacúa sola por GC. Sin clear manual y sin mantener bloques
    ' vivos (no leak). TryGetValue/AddOrUpdate son thread-safe.
    Private Shared ReadOnly _clothSkeletonCache As _
        New Runtime.CompilerServices.ConditionalWeakTable(Of BSClothExtraData, HkaSkeletonGraph_Class)

    ' Parses the first BSClothExtraData from a NIF and returns the HKX skeleton (cached per block).
    ' Returns Nothing if the NIF has no cloth data or the skeleton cannot be parsed. Observable result
    ' idéntico al histórico (primer bloque), ahora vía el caché por-bloque. FaceGen bake depende de esta firma.
    Public Shared Function ParseClothSkeleton(nifContent As Nifcontent_Class_Manolo) As HkaSkeletonGraph_Class
        Dim cloth = nifContent?.Blocks.OfType(Of BSClothExtraData)().FirstOrDefault()
        If cloth Is Nothing Then Return Nothing
        Return ParseClothSkeletonFromBlock(cloth)
    End Function

    ' Parses a specific BSClothExtraData block and returns the HKX skeleton (cached per block instance).
    Private Shared Function ParseClothSkeletonFromBlock(cloth As BSClothExtraData) As HkaSkeletonGraph_Class
        Dim cached As HkaSkeletonGraph_Class = Nothing
        If _clothSkeletonCache.TryGetValue(cloth, cached) Then Return cached

        Dim parsed As HkaSkeletonGraph_Class = Nothing
        Try
            Dim graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(cloth))
            Dim skeletonObject = graph.GetObjectsByClassName("hkaSkeleton").FirstOrDefault()
            If skeletonObject IsNot Nothing Then
                Dim skeleton = graph.ParseSkeleton(skeletonObject)
                If skeleton IsNot Nothing AndAlso skeleton.Bones IsNot Nothing AndAlso skeleton.ReferencePose IsNot Nothing AndAlso skeleton.ParentIndices IsNot Nothing Then
                    If skeleton.Bones.Count > 0 AndAlso skeleton.ReferencePose.Count = skeleton.Bones.Count Then
                        parsed = skeleton
                    End If
                End If
            End If
        Catch ex As Exception
            parsed = Nothing
        End Try

        ' Solo se cachean resultados no-nulos: ConditionalWeakTable no admite Nothing como value, y un
        ' Nothing (parse fallido) es barato de re-evaluar.
        ' AddOrUpdate (no Add): si dos hilos parsean a la vez, el último gana; ambos resultados son
        ' equivalentes (el parse es determinístico para un bloque dado), así que es idempotente.
        If parsed IsNot Nothing Then _clothSkeletonCache.AddOrUpdate(cloth, parsed)
        Return parsed
    End Function

    ''' <param name="targetSkeleton">SkeletonInstance into which missing bones get injected.
    ''' Reads <see cref="SkeletonInstance.SkeletonDictionary"/> to detect already-present bones,
    ''' writes new entries into <see cref="SkeletonInstance.SkeletonStructure"/> /
    ''' <see cref="SkeletonInstance.SkeletonDictionary"/> / <see cref="SkeletonInstance.InjectedBones"/>.</param>
    Public Shared Sub InjectMissingBonesIntoLiveSkeleton(shape As IRenderableShape,
                                                         targetSkeleton As SkeletonInstance,
                                                         Optional cachedSkeleton As HkaSkeletonGraph_Class = Nothing)
        If IsNothing(shape) OrElse targetSkeleton Is Nothing OrElse Not targetSkeleton.HasSkeleton Then Exit Sub
        If Not shape.HasPhysics Then Exit Sub
        If IsNothing(shape.NifContent) Then Exit Sub

        Dim nifShape = ResolveShapeNifShape(shape)
        If IsNothing(nifShape) Then Exit Sub

        Dim relatedBones = ResolveShapeBones(shape, nifShape)
        If relatedBones.Count = 0 Then Exit Sub

        Dim skeleton As HkaSkeletonGraph_Class
        If cachedSkeleton IsNot Nothing Then
            skeleton = cachedSkeleton
        Else
            ' BSClothExtraData embebe la rebanada del hkaSkeleton de AUTORÍA con los cloth-bones + su
            ' ancla y bind/jerarquía — la fuente directa y correcta. (El .hkx de autoría suelto tiene el
            ' skeleton completo de 201 huesos, mismo bind, pero el render no tiene el path para cargarlo.)
            ' Resuelve PER-SHAPE el bloque referenciado desde el ExtraDataList de la propia shape; si la
            ' shape no tiene cloth atado, cae al scan plano del primer bloque (no-op para NIFs single-cloth).
            Dim shapeBlock = ResolveShapeClothBlock(nifShape, shape.NifContent)
            skeleton = If(shapeBlock IsNot Nothing, ParseClothSkeletonFromBlock(shapeBlock), ParseClothSkeleton(shape.NifContent))
            If skeleton Is Nothing Then Exit Sub
        End If

        Dim shapeName = ResolveShapeDisplayName(shape, nifShape)

        Try
            Dim hkxBoneLookup = skeleton.Bones.
                Where(Function(bone) bone IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(bone.Name)).
                GroupBy(Function(bone) bone.Name.Trim(), StringComparer.OrdinalIgnoreCase).
                ToDictionary(Function(group) group.Key,
                             Function(group) group.First().Index,
                             StringComparer.OrdinalIgnoreCase)

            For Each shapeBone In relatedBones
                If IsNothing(shapeBone) OrElse IsNothing(shapeBone.Name) Then Continue For

                Dim shapeBoneName = shapeBone.Name.String
                If String.IsNullOrWhiteSpace(shapeBoneName) Then Continue For
                shapeBoneName = shapeBoneName.Trim()
                If targetSkeleton.SkeletonDictionary.ContainsKey(shapeBoneName) Then Continue For

                Dim targetIndex As Integer = -1
                If Not hkxBoneLookup.TryGetValue(shapeBoneName, targetIndex) Then
                    Logger.LogLazy(Function() $"[CLOTH-BIND] '{shapeName}': bone '{shapeBoneName}' del skin no está en el hkaSkeleton del BSClothExtraData; se omite.")
                    Continue For
                End If
                EnsureLiveInjectedBone(targetIndex, skeleton, targetSkeleton, shapeName, shapeBoneName)
            Next
        Catch ex As Exception
            Logger.LogLazy(Function() $"[CLOTH-BIND] excepción inyectando cloth-bones (HKX) para '{shapeName}': {ex.Message}")
        End Try
    End Sub

    Private Shared Function ResolveShapeNifShape(shape As IRenderableShape) As INiShape
        If IsNothing(shape) OrElse IsNothing(shape.NifContent) Then Return Nothing

        Dim expectedNames = New List(Of String) From {
            NormalizeBoneName(shape.ShapeName),
            NormalizeBoneName(shape.ShapeTarget)
        }

        For Each nifShape In shape.NifContent.NifShapes
            Dim nifName = NormalizeBoneName(nifShape?.Name?.String)
            If String.IsNullOrWhiteSpace(nifName) Then Continue For
            If expectedNames.Any(Function(name) String.IsNullOrWhiteSpace(name) = False AndAlso String.Equals(name, nifName, StringComparison.OrdinalIgnoreCase)) Then Return nifShape
        Next

        Return Nothing
    End Function

    ' Returns the first BSClothExtraData referenced from the shape's own ExtraDataList, or Nothing.
    ' Espeja la lógica probada de Tools\HkxLoadOrderAudit\Program.vb (AvHasClothRef): recorre
    ' av.ExtraDataList.References y resuelve cada uno con GetBlock(Of NiExtraData).
    Private Shared Function ResolveShapeClothBlock(nifShape As INiShape, nifContent As Nifcontent_Class_Manolo) As BSClothExtraData
        Dim av = TryCast(nifShape, NiAVObject)
        If av Is Nothing OrElse av.ExtraDataList Is Nothing Then Return Nothing
        For Each reference In av.ExtraDataList.References
            If reference Is Nothing Then Continue For
            Dim ed = nifContent.GetBlock(Of NiExtraData)(reference)
            If TypeOf ed Is BSClothExtraData Then Return CType(ed, BSClothExtraData)
        Next
        Return Nothing
    End Function

    Private Shared Function ResolveShapeBones(shape As IRenderableShape, nifShape As INiShape) As List(Of NiNode)
        Dim result As New List(Of NiNode)
        If IsNothing(shape) OrElse IsNothing(nifShape) OrElse IsNothing(shape.NifContent) Then Return result
        If IsNothing(nifShape.SkinInstanceRef) OrElse nifShape.SkinInstanceRef.Index < 0 Then Return result

        Dim skin = TryCast(shape.NifContent.Blocks(nifShape.SkinInstanceRef.Index), INiSkin)
        If IsNothing(skin) OrElse IsNothing(skin.Bones) Then Return result

        For Each boneIndex In skin.Bones.Indices
            If boneIndex < 0 OrElse boneIndex >= shape.NifContent.Blocks.Count Then Continue For
            Dim node = TryCast(shape.NifContent.Blocks(boneIndex), NiNode)
            If IsNothing(node) Then Continue For
            result.Add(node)
        Next

        Return result
    End Function

    Private Shared Function ResolveShapeDisplayName(shape As IRenderableShape, nifShape As INiShape) As String
        Dim nifName = nifShape?.Name?.String
        If String.IsNullOrWhiteSpace(nifName) = False Then Return nifName
        If IsNothing(shape) Then Return "<shape>"
        If String.IsNullOrWhiteSpace(shape.ShapeName) = False Then Return shape.ShapeName
        If String.IsNullOrWhiteSpace(shape.ShapeTarget) = False Then Return shape.ShapeTarget
        Return "<shape>"
    End Function
    ' Public wrapper — creates the visited set on first call
    Private Shared Function EnsureLiveInjectedBone(index As Integer,
                                                   skeleton As HkaSkeletonGraph_Class,
                                                   targetSkeleton As SkeletonInstance,
                                                   shapeName As String,
                                                   Optional requestedName As String = Nothing) As HierarchiBone_class
        Return EnsureLiveInjectedBone(index, skeleton, targetSkeleton, shapeName, requestedName, New HashSet(Of Integer))
    End Function

    ' Private recursive overload with visited set to prevent stack overflow on circular HKX parent chains
    Private Shared Function EnsureLiveInjectedBone(index As Integer,
                                                   skeleton As HkaSkeletonGraph_Class,
                                                   targetSkeleton As SkeletonInstance,
                                                   shapeName As String,
                                                   requestedName As String,
                                                   visited As HashSet(Of Integer)) As HierarchiBone_class
        If Not visited.Add(index) Then Return Nothing ' cycle detected — break recursion
        If IsNothing(skeleton) OrElse IsNothing(skeleton.Bones) OrElse index < 0 OrElse index >= skeleton.Bones.Count Then Return Nothing
        If targetSkeleton Is Nothing Then Return Nothing

        Dim boneName = skeleton.Bones(index).Name
        If String.IsNullOrWhiteSpace(boneName) Then Return Nothing
        Dim dictionaryKey = If(String.IsNullOrWhiteSpace(requestedName), boneName, requestedName.Trim())

        Dim existing As HierarchiBone_class = Nothing
        If targetSkeleton.SkeletonDictionary.TryGetValue(dictionaryKey, existing) Then Return existing
        If Not dictionaryKey.Equals(boneName, StringComparison.OrdinalIgnoreCase) AndAlso targetSkeleton.SkeletonDictionary.TryGetValue(boneName, existing) Then Return existing

        Dim parentBone As HierarchiBone_class = Nothing
        Dim parentIndex = If(index < skeleton.ParentIndices.Count, CInt(skeleton.ParentIndices(index)), -1)
        If parentIndex >= 0 Then
            parentBone = EnsureLiveInjectedBone(parentIndex, skeleton, targetSkeleton, shapeName, Nothing, visited)
        End If

        Dim nuevo As New HierarchiBone_class With {
            .BoneName = dictionaryKey,
            .Parent = parentBone,
            .DeltaTransform = Nothing,
            .OriginalLocaLTransform = HkxTransformConventionHelper.ToTransform(skeleton.ReferencePose(index))
        }

        If IsNothing(parentBone) Then
            targetSkeleton.SkeletonStructure.Add(nuevo)
        Else
            parentBone.Childrens.Add(nuevo)
        End If

        targetSkeleton.SkeletonDictionary.Add(dictionaryKey, nuevo)
        targetSkeleton.InjectedBones.Add(dictionaryKey)
        Return nuevo
    End Function

    Private Shared Function NormalizeBoneName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return String.Empty
        Return name.Trim().ToUpperInvariant()
    End Function


End Class

