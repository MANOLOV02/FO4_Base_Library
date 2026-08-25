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
' La conversión reference-pose → Transform_Class va por HkxTransformConventionHelper.ToTransform:
' fuente ÚNICA, con la misma convención que el resto del render.
'
' AUTORÍA = BSClothExtraData: el hkaSkeleton de BSClothExtraData ES la rebanada de autoría
' embebida en el NIF (cloth-bones + ancla + bind + jerarquía). Leerlo y colgar los bones (esto)
' YA es usar la autoría directo — no hay que recalcular nada. El .hkx de autoría SUELTO
' (FemaleHair04.hkx, junto al NIF) tiene el skeleton COMPLETO de 201 huesos con el mismo bind de
' los cloth-bones (verificado: Δ<5e-4 u vs el embebido), pero el render no tiene el path del NIF
' (Nifcontent_Class_Manolo no guarda su filename), así que el embebido es la fuente in-memory.
' Si BSClothExtraData falta (CloneShape_Original no lo transfiere) la solución correcta es preservarlo
' en el clone / leer del NIF source — NO recalcular desde el skin. Ver [[25-cloth-inyeccion-de-huesos]].
'
' PENDIENTES CONOCIDOS:
'  - HclCollisionPoseHelper todavía tiene copias PRIVADAS de LocalReferencePoseToTransform y de un
'    ResolveUniformScale que PROMEDIA X/Y/Z. La compartida ya no existe: se borró al pasar la
'    convención a per-eje (ResolveScaleVector), porque quedó sin llamadores. El promedio que queda
'    en HclCollisionPoseHelper NO es camino vivo -- esa clase entera está sin llamadores -- pero es
'    la conducta VIEJA, así que el día que se la conecte al render hay que pasarla a per-eje ANTES,
'    no después: conectada, promediar la escala es una pérdida silenciosa.
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
        New Runtime.CompilerServices.ConditionalWeakTable(Of BSClothExtraData, Havok.Canon.Objects.HkObj_HkaSkeleton)

    ' Parses the first BSClothExtraData from a NIF and returns the HKX skeleton (cached per block).
    ' Returns Nothing if the NIF has no cloth data or the skeleton cannot be parsed. Observable result
    ' idéntico al histórico (primer bloque), ahora vía el caché por-bloque. FaceGen bake depende de esta firma.
    Public Shared Function ParseClothSkeleton(nifContent As Nifcontent_Class_Manolo) As Havok.Canon.Objects.HkObj_HkaSkeleton
        Dim cloth = nifContent?.Blocks.OfType(Of BSClothExtraData)().FirstOrDefault()
        If cloth Is Nothing Then Return Nothing
        Return ParseClothSkeletonFromBlock(cloth)
    End Function

    ''' <summary>
    ''' El bloque BSClothExtraData que le corresponde a ESTA shape (per-shape vía su ExtraDataList,
    ''' con fallback al primer bloque plano). Lo necesita la simulación de física, que trabaja por
    ''' shape y cachea su estado por BLOQUE, no por NIF.
    ''' </summary>
    Public Shared Function ResolveClothBlockForShape(shape As IRenderableShape) As BSClothExtraData
        If IsNothing(shape) OrElse IsNothing(shape.NifContent) Then Return Nothing
        Dim nifShape = ResolveShapeNifShape(shape)
        If Not IsNothing(nifShape) Then
            Dim block = ResolveShapeClothBlock(nifShape, shape.NifContent)
            If block IsNot Nothing Then Return block
        End If
        Return shape.NifContent.Blocks.OfType(Of BSClothExtraData)().FirstOrDefault()
    End Function

    ''' <summary>Igual que el parse interno, pero accesible: la física reusa el mismo cache por bloque.</summary>
    Public Shared Function ParseClothSkeletonForBlock(cloth As BSClothExtraData) As Havok.Canon.Objects.HkObj_HkaSkeleton
        If cloth Is Nothing Then Return Nothing
        Return ParseClothSkeletonFromBlock(cloth)
    End Function

    ' Parses a specific BSClothExtraData block and returns the HKX skeleton (cached per block instance).
    Private Shared Function ParseClothSkeletonFromBlock(cloth As BSClothExtraData) As Havok.Canon.Objects.HkObj_HkaSkeleton
        Dim cached As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing
        If _clothSkeletonCache.TryGetValue(cloth, cached) Then Return cached

        Dim parsed As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing
        Try
            Dim graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(cloth))
            ' ⛔ El esqueleto sale de `hkaAnimationContainer.skeletons`, no del primer bloque.
            Dim skeleton = graph.EsqueletoPrincipal()
            If skeleton IsNot Nothing AndAlso skeleton.Bones IsNot Nothing AndAlso skeleton.ReferencePose IsNot Nothing AndAlso skeleton.ParentIndices IsNot Nothing Then
                If skeleton.Bones.Count > 0 AndAlso skeleton.ReferencePose.Count = skeleton.Bones.Count Then
                    parsed = skeleton
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
                                                         Optional cachedSkeleton As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing)
        If IsNothing(shape) OrElse targetSkeleton Is Nothing OrElse Not targetSkeleton.HasSkeleton Then Exit Sub
        If Not shape.HasPhysics Then Exit Sub
        If IsNothing(shape.NifContent) Then Exit Sub

        Dim nifShape = ResolveShapeNifShape(shape)
        If IsNothing(nifShape) Then Exit Sub

        Dim relatedBones = ResolveShapeBones(shape, nifShape)
        If relatedBones.Count = 0 Then Exit Sub

        Dim skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton
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
                Select(Function(bone, idx) New With {.Bone = bone, .Index = idx}).
                GroupBy(Function(x) x.Bone.Name.Trim(), StringComparer.OrdinalIgnoreCase).
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
                                                   skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                                   targetSkeleton As SkeletonInstance,
                                                   shapeName As String,
                                                   Optional requestedName As String = Nothing) As HierarchiBone_class
        Return EnsureLiveInjectedBone(index, skeleton, targetSkeleton, shapeName, requestedName, New HashSet(Of Integer))
    End Function

    ' Private recursive overload with visited set to prevent stack overflow on circular HKX parent chains
    Private Shared Function EnsureLiveInjectedBone(index As Integer,
                                                   skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
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

        ' ⛔ REUTILIZAR UN HUESO QUE YA EXISTE NO ES GRATIS. La fisica escribe el cloth-bone contra el
        ' bind que declara el HKX de la prenda; el render lo lee contra el bind del hueso VIVO. Si el
        ' nombre ya estaba en el esqueleto con OTRO bind, los dos espacios dejan de coincidir y la
        ' malla se desgarra justo donde estan los vertices de ese hueso. En reposo no se ve —cada lado
        ' es consistente consigo mismo—, aparece con la pose.
        ' No se corrige acá (pisar el bind de un hueso del cuerpo romperia el resto del render): se
        ' MIDE, para saber si el desgarro que se ve viene de aca o de otro lado.
        Dim existing As HierarchiBone_class = Nothing
        If targetSkeleton.SkeletonDictionary.TryGetValue(dictionaryKey, existing) Then
            AvisarBindDistinto(existing, skeleton, index, dictionaryKey, shapeName)
            Return existing
        End If
        If Not dictionaryKey.Equals(boneName, StringComparison.OrdinalIgnoreCase) AndAlso targetSkeleton.SkeletonDictionary.TryGetValue(boneName, existing) Then
            AvisarBindDistinto(existing, skeleton, index, boneName, shapeName)
            Return existing
        End If

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

    ''' <summary>
    ''' Compara el bind LOCAL del hueso vivo contra el `referencePose` que declara el HKX de la prenda
    ''' para ese mismo hueso. Tienen que ser el mismo: la fisica escribe contra el segundo y el render
    ''' lee contra el primero.
    ''' <para>⛔ EN REPOSO ESTA DIFERENCIA ES INVISIBLE, porque cada lado es consistente consigo mismo.
    ''' Solo se manifiesta con pose, y como un desgarro LOCAL — justo en los vertices de ese hueso.</para>
    ''' </summary>
    Private Shared Sub AvisarBindDistinto(vivo As HierarchiBone_class,
                                          skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                          index As Integer, nombre As String, shapeName As String)
        If Not Logger.Enabled OrElse vivo Is Nothing Then Exit Sub
        If skeleton Is Nothing OrElse skeleton.ReferencePose Is Nothing OrElse index >= skeleton.ReferencePose.Count Then Exit Sub
        Dim hkx = HkxTransformConventionHelper.ToTransform(skeleton.ReferencePose(index))
        Dim viv = vivo.OriginalLocaLTransform
        If hkx Is Nothing OrElse viv Is Nothing Then Exit Sub
        Dim a = hkx.ToMatrix4(), b = viv.ToMatrix4()
        Dim dT = Math.Sqrt(((a.M41 - b.M41) ^ 2) + ((a.M42 - b.M42) ^ 2) + ((a.M43 - b.M43) ^ 2))
        Dim tr = (a.M11 * b.M11) + (a.M12 * b.M12) + (a.M13 * b.M13) +
                 (a.M21 * b.M21) + (a.M22 * b.M22) + (a.M23 * b.M23) +
                 (a.M31 * b.M31) + (a.M32 * b.M32) + (a.M33 * b.M33)
        Dim ang = Math.Acos(Math.Max(-1.0R, Math.Min(1.0R, (tr - 1.0R) / 2.0R))) * 180.0R / Math.PI
        If dT <= 0.01R AndAlso ang <= 0.1R Then Exit Sub
        Dim nm = nombre, sh = shapeName
        Logger.LogLazy(Function() $"[CLOTH-BINDDIF] '{nm}' (shape '{sh}') ya existia con OTRO bind: dT={dT:F3} dAng={ang:F2} ⇒ la fisica y el render usan espacios distintos para este hueso")
    End Sub

    Private Shared Function NormalizeBoneName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return String.Empty
        Return name.Trim().ToUpperInvariant()
    End Function


End Class

