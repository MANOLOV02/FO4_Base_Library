Imports System.Linq
Imports NiflySharp.Blocks

' =============================================================================
' BSConnectPointBoneInjector — soporte de mounting para chunks BSConnectPoint
' (robot chunks, weapon mods, PA pieces, workshop items, etc.) que vienen en
' NIFs standalone con jerarquía de bones interna.
'
' Caso típico:
'   - Chunk NIF declara bones tipo HeadArmorFXBone001 / Pt_HandRootBone que viven
'     solo dentro del chunk (no en RACE.ANAM ni BPTD.MODL).
'   - Chunk se mountea via BSConnectPoint::Children "C-X" matcheando "P-X" del
'     host (resuelto por ConnectPointMountResolver).
'   - Para que el shape skinea, los bones internos deben existir en
'     SkeletonDictionary del actor. Este injector los agrega.
'
' Dos APIs estructurales:
'
'   MaterializeSocketsAsConnectPointBones(nif, targetSkeleton):
'     Lee BSConnectPoint::Parents del NIF (host o chunk). Para cada socket "P-X",
'     materializa un NiNode virtual "C-X" en el SkeletonDictionary del actor,
'     anclado al socket.ParentBoneName con socket.LocalTransform.
'     Idempotente — si C-X ya existe, skipea.
'
'   InjectChunkBonesIntoLiveSkeleton(chunkNif, shape, socket, targetSkeleton):
'     Para cada bone declarado en shape.ShapeBones que NO esté en el dict, lo
'     inyecta usando su LocalTransform cruda del NIF. Recursa por parent NIF
'     hasta el chunkRoot, donde se ancla al targetBone (= socket.ParentBoneName
'     resuelto en el dict del actor).
' =============================================================================
Public NotInheritable Class BSConnectPointBoneInjector_Class

    ''' <summary>Inject bones internos del chunk NIF al SkeletonInstance del actor,
    ''' anchored al socket bone con la socket transform como punto de inserción.
    ''' Idempotente — bones ya en SkeletonDictionary se skipean.
    '''
    ''' Aplicar SOLO cuando el chunk se mountea via BSConnectPoint y los bones del
    ''' shape NO existen en el SkeletonInstance del actor.</summary>
    ''' <param name="chunkNif">NIF del chunk (Nifcontent_Class_Manolo cargado).</param>
    ''' <param name="shape">El IRenderableShape cuyos bones queremos inyectar.</param>
    ''' <param name="socket">Socket parent del actor donde el chunk se mountea
    ''' (resuelto por ConnectPointMountResolver). Provee ParentBoneName + local T/R/S.</param>
    ''' <param name="targetSkeleton">SkeletonInstance del actor — destino del inject.</param>
    ''' <returns>Cantidad de bones inyectados (0 si todos ya estaban presentes).</returns>
    Public Shared Function InjectChunkBonesIntoLiveSkeleton(chunkNif As Nifcontent_Class_Manolo,
                                                            shape As IRenderableShape,
                                                            socket As BSConnectPointReader.ConnectPointInfo,
                                                            targetSkeleton As SkeletonInstance) As Integer
        If chunkNif Is Nothing OrElse shape Is Nothing OrElse socket Is Nothing OrElse targetSkeleton Is Nothing Then Return 0
        If Not targetSkeleton.HasSkeleton Then Return 0
        If shape.ShapeBones Is Nothing OrElse shape.ShapeBones.Count = 0 Then Return 0

        ' Resolve target bone — donde el chunk se cuelga del actor skeleton.
        Dim targetBone As HierarchiBone_class = Nothing
        If String.IsNullOrEmpty(socket.ParentBoneName) Then Return 0
        If Not targetSkeleton.SkeletonDictionary.TryGetValue(socket.ParentBoneName, targetBone) Then Return 0

        ' Heurística empírica (NO engine-verified): si chunkRoot.local es identity,
        ' el chunk fue autorado en actor-world frame con offset del parent_bone pre-baked
        ' en su NiNode hierarchy (ej. brahmin PackBase02: BaseLagBone.local.Z ≈ Spine3.world.Z).
        ' En ese caso, NO aplicar socket × parent — daría duplicación. Anchor.world = identity.
        '
        ' Si chunkRoot.local NO es identity (ej. Assaultron HeadArmor: 90° Z), el chunk fue
        ' autorado en chunk-local frame y necesita el attachment via socket × parent.
        Dim chunkRootNode = chunkNif.GetRootNode()
        Dim chunkRootLocalT As Transform_Class = If(chunkRootNode IsNot Nothing, New Transform_Class(chunkRootNode), New Transform_Class())
        Dim chunkRootIsIdentity As Boolean = chunkRootLocalT.Equals(New Transform_Class())

        Dim anchorLocal As Transform_Class
        If chunkRootIsIdentity Then
            anchorLocal = targetBone.OriginalGetGlobalTransform.Inverse()
        Else
            anchorLocal = SocketToTransform(socket)
        End If

        Dim chunkRootName = If(chunkRootNode?.Name?.String, "chunk")
        Dim anchorName = "__chunkAnchor__" & socket.Name & "__" & chunkRootName

        Dim anchorBone As HierarchiBone_class = Nothing
        If Not targetSkeleton.SkeletonDictionary.TryGetValue(anchorName, anchorBone) Then
            anchorBone = New HierarchiBone_class With {
                .BoneName = anchorName,
                .Parent = targetBone,
                .DeltaTransform = Nothing,
                .OriginalLocaLTransform = anchorLocal
            }
            targetBone.Childrens.Add(anchorBone)
            targetSkeleton.SkeletonDictionary.Add(anchorName, anchorBone)
            targetSkeleton.InjectedBones.Add(anchorName)
        End If

        Dim effectiveParent As HierarchiBone_class = anchorBone

        Dim injected As Integer = 0
        For Each shapeBoneNode In shape.ShapeBones
            Dim niNode = TryCast(shapeBoneNode, NiNode)
            If niNode Is Nothing Then Continue For
            Dim boneName = niNode.Name?.String
            If String.IsNullOrWhiteSpace(boneName) Then Continue For
            ' Idempotencia — si ya está en el dict, skip.
            If targetSkeleton.SkeletonDictionary.ContainsKey(boneName) Then Continue For

            EnsureInjectedChunkBone(niNode, chunkNif, chunkRootNode, effectiveParent, targetSkeleton, injected)
        Next

        Return injected
    End Function

    ''' <summary>Inyección recursiva del bone + sus padres hasta el chunkRoot del NIF.
    ''' Si el bone ya existe en el dict, devuelve la entry existente. Si su parent es el chunkRoot
    ''' (o Nothing), Parent = targetBone. Si su parent es otro NiNode interno, recursa.
    ''' LocaLTransform = transform crudo del NiNode en el NIF (sin composición), EXCEPTO en el
    ''' topmost (parent = chunkRoot) donde se compone con chunkRoot.local para preservar la
    ''' transformación del root del NIF (relevante cuando chunkRoot ≠ identity, p.ej. Assaultron
    ''' chunks).</summary>
    Private Shared Function EnsureInjectedChunkBone(node As NiNode,
                                                    chunkNif As Nifcontent_Class_Manolo,
                                                    chunkRootNode As NiNode,
                                                    targetBone As HierarchiBone_class,
                                                    targetSkeleton As SkeletonInstance,
                                                    ByRef injectedCounter As Integer) As HierarchiBone_class
        If node Is Nothing Then Return Nothing
        Dim boneName = node.Name?.String
        If String.IsNullOrWhiteSpace(boneName) Then Return Nothing
        Dim existing As HierarchiBone_class = Nothing
        If targetSkeleton.SkeletonDictionary.TryGetValue(boneName, existing) Then Return existing

        Dim nodeLocalT As New Transform_Class(node)
        Dim parentNode = TryCast(chunkNif.GetParentNode(node), NiNode)
        Dim parentBone As HierarchiBone_class = Nothing
        If parentNode IsNot Nothing AndAlso Not ReferenceEquals(parentNode, chunkRootNode) Then
            parentBone = EnsureInjectedChunkBone(parentNode, chunkNif, chunkRootNode, targetBone, targetSkeleton, injectedCounter)
        Else
            parentBone = targetBone
        End If

        ' chunkRoot.local NO se compone — es scene-viewer rotation del modelador, no parte
        ' del attachment. La rotación correcta del chunk en actor world viene del socket × parent
        ' aplicado en el anchor. Componer chunkRoot agrega 90° espurios (verificado vs render
        ' Assaultron HeadArmor).
        Dim nuevo As New HierarchiBone_class With {
            .BoneName = boneName,
            .Parent = parentBone,
            .DeltaTransform = Nothing,
            .OriginalLocaLTransform = nodeLocalT
        }

        If parentBone Is Nothing Then
            targetSkeleton.SkeletonStructure.Add(nuevo)
        Else
            parentBone.Childrens.Add(nuevo)
        End If

        targetSkeleton.SkeletonDictionary.Add(boneName, nuevo)
        targetSkeleton.InjectedBones.Add(boneName)
        injectedCounter += 1

        Return nuevo
    End Function

    ''' <summary>Convierte un socket.Name del lado parent ("P-X" / "P_X") al nombre counterpart
    ''' del lado children ("C-X" / "C_X"). Si el name no empieza con prefix P-/P_ devuelve "".</summary>
    Private Shared Function TryGetSocketCounterpartName(socketName As String) As String
        If String.IsNullOrEmpty(socketName) OrElse socketName.Length < 2 Then Return ""
        If socketName.StartsWith("P-", StringComparison.OrdinalIgnoreCase) Then Return String.Concat("C-", socketName.AsSpan(2))
        If socketName.StartsWith("P_", StringComparison.OrdinalIgnoreCase) Then Return String.Concat("C_", socketName.AsSpan(2))
        Return ""
    End Function

    ''' <summary>Lee los BSConnectPoint::Parents del NIF (host O chunk) y materializa cada socket
    ''' "P-X" como un NiNode virtual "C-X" en el SkeletonDictionary del actor, anclado al
    ''' socket.ParentBoneName con socket.LocalTransform.
    '''
    ''' Idempotente: si C-X ya existe en el dict, skipea (caso típico: el BPTD merge ya lo trajo).
    '''
    ''' Casos cubiertos:
    '''   - Host NIF (skeleton del actor): expone P-Head, P-HandLeft, P-PackBase, etc.
    '''   - Chunk NIF: expone sub-sockets como P-HeadArmorAssaultron (chunk parent expone slot
    '''     para que un sub-chunk se monte encima).
    '''
    ''' Si el socket.ParentBoneName no existe en el dict (target bone faltante), skipea el socket
    ''' (caso límite: requiere topological order si depende de un bone interno de otro chunk no
    ''' inyectado todavía).</summary>
    Public Shared Function MaterializeSocketsAsConnectPointBones(nif As Nifcontent_Class_Manolo,
                                                                 targetSkeleton As SkeletonInstance) As Integer
        If nif Is Nothing OrElse targetSkeleton Is Nothing OrElse Not targetSkeleton.HasSkeleton Then Return 0
        Dim sockets = BSConnectPointReader.ReadParents(nif)
        If sockets Is Nothing OrElse sockets.Count = 0 Then Return 0

        Dim added As Integer = 0
        For Each sock In sockets
            Dim before = targetSkeleton.SkeletonDictionary.Count
            Dim cn = EnsureSocketCounterpartBone(sock, targetSkeleton)
            If Not String.IsNullOrEmpty(cn) AndAlso targetSkeleton.SkeletonDictionary.Count > before Then added += 1
        Next
        Return added
    End Function

    ''' <summary>Materializa el counterpart bone "C-X" de UN socket "P-X" en el targetSkeleton,
    ''' anclado a socket.ParentBoneName con socket.LocalTransform. Idempotente: si C-X ya existe,
    ''' devuelve su nombre sin recrear. Devuelve el nombre del bone C-X creado/existente, o "" si
    ''' no se pudo (name sin prefix P-/P_, parent bone ausente en el skel, o skel inválido).
    ''' <para>Extraído del loop de <see cref="MaterializeSocketsAsConnectPointBones"/> para poder
    ''' materializar on-demand un único socket — caso FAKE-SKIN de chunks unskinned puros
    ''' (ShapeBones=0) cuyo C-X no fue materializado por el bulk (orden) ni por el injector
    ''' (early-exit con 0 bones). El caller pasa el socket EFECTIVO (post SOCKET-EFFECTIVE-OVERRIDE).</para></summary>
    Public Shared Function EnsureSocketCounterpartBone(sock As BSConnectPointReader.ConnectPointInfo,
                                                       targetSkeleton As SkeletonInstance) As String
        If sock Is Nothing OrElse String.IsNullOrEmpty(sock.Name) Then Return ""
        If targetSkeleton Is Nothing OrElse Not targetSkeleton.HasSkeleton Then Return ""

        Dim cName = TryGetSocketCounterpartName(sock.Name)
        If String.IsNullOrEmpty(cName) Then Return ""

        ' Idempotencia: si C-X ya está en el dict, devolverlo sin recrear.
        If targetSkeleton.SkeletonDictionary.ContainsKey(cName) Then Return cName

        ' Parent bone — donde el C-X se ancla en el actor. Si falta, no se puede materializar.
        If String.IsNullOrEmpty(sock.ParentBoneName) Then Return ""
        Dim parentBone As HierarchiBone_class = Nothing
        If Not targetSkeleton.SkeletonDictionary.TryGetValue(sock.ParentBoneName, parentBone) Then Return ""

        Dim socketLocalT As Transform_Class = SocketToTransform(sock)
        Dim nuevo As New HierarchiBone_class With {
            .BoneName = cName,
            .Parent = parentBone,
            .DeltaTransform = Nothing,
            .OriginalLocaLTransform = socketLocalT
        }
        parentBone.Childrens.Add(nuevo)
        targetSkeleton.SkeletonDictionary.Add(cName, nuevo)
        targetSkeleton.InjectedBones.Add(cName)
        Return cName
    End Function

    ''' <summary>Composición Transform_Class desde una BSConnectPointReader.ConnectPointInfo
    ''' (Translation + Rotation quaternion + Scale). La quat se baka a Matrix33 vía la fórmula
    ''' estándar de quaternion-to-matrix.</summary>
    Private Shared Function SocketToTransform(socket As BSConnectPointReader.ConnectPointInfo) As Transform_Class
        Dim t As New Transform_Class With {
            .Translation = socket.Translation,
            .Rotation = QuatToMatrix33(socket.Rotation),
            .Scale = If(socket.Scale > 0.0F, socket.Scale, 1.0F)
        }
        Return t
    End Function

    ''' <summary>Delega a <see cref="BSConnectPointReader.QuatToMatrix33"/> — fuente única de
    ''' la conversión quat→matrix con paridad runtime al resto del render pipeline.</summary>
    Private Shared Function QuatToMatrix33(q As System.Numerics.Quaternion) As NiflySharp.Structs.Matrix33
        Return BSConnectPointReader.QuatToMatrix33(q)
    End Function

End Class
