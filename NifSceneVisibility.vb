Imports NiflySharp
Imports NiflySharp.Blocks
Imports OpenTK.Mathematics

''' <summary>Lo que el JUEGO deja de la visibilidad de una forma del NIF al cargar el archivo y en el reposo de la preview, leído de su
''' cadena de nodos, de sus bounds y de sus controladores de visibilidad (chunk C3, decisiones del usuario C-ANC / C-NAN; ola 0c ptos
''' 14 y 3a). Se evalúa sobre el NIF EN MEMORIA cada vez que se pregunta: el editor recalcula bounds (UpdateBounds) y el estado tiene
''' que seguirlo. El render lo memoiza por frame (RenderableMesh.SceneVisibility).
''' <para><b>C-ANC, ancestro oculto.</b> La cull de un nodo vuelve antes de sus hijos si está oculto (bit 0; NiCullingProcess SSE
''' 0x140F04AE0 / FO4 0x1416E6960, BSCullingProcess SSE 0x140FEDA70 / FO4 0x1417E1C60). Un nodo de rango (BSRangeNode y sus
''' subclases BSBlastNode, BSDamageStage, BSDebrisNode) reescribe su bit al cargar: LoadBinary (SSE 0x140FDD4D0, FO4 0x1417D35D0)
''' lee Min, Max, Current tras el NiNode y llama SetCurrent(Current) (SSE 0x140FDD820, FO4 0x1417D38D0): visible = Min &lt;= Current
''' &lt;= Max, bytes SIN signo (jb / ja), y el bit queda Not visible, diga lo que diga el archivo.</para>
''' <para><b>C-NAN, radio de mundo 0.</b> BSCullingProcess descarta un objeto cuyo radio de mundo es el PATRON DE BITS 0 (cmp dword:
''' -0.0 pasa) salvo el bit 11 (0x800): SSE 0x140FEDA82..0x140FEDA9F, FO4 0x1417E1C6D..0x1417E1C88. El radio de mundo de un
''' BSGeometry sin skin es radio del modelo x escala de mundo (SSE 0x140EFCFDF, FO4 0x1416D5FD0), y la escala de mundo la del padre
''' x la local (SSE 0x140F0303C, FO4 0x1416C8CAD -&gt; 0x140344966). Ley de los DOS juegos. BSGeometry::LoadBinary de Skyrim SE (0x140EFC18B..0x140EFC21A) reemplaza una esfera con NaN en
''' cualquiera de sus cuatro floats por centro 0 y radio 0; la de Fallout 4 (0x1416D4EA0 -&gt; 0x1416D71C0) la deja como está.</para>
''' <para><b>Pto 14, nodo de radio de mundo 0.</b> OnVisible (FO4 0x1416BF8C0 ranura 57, SSE 0x140EE2CC0 ranura 52; BSCullingProcess
''' FO4 0x1417E1C60 igual) no recorre los hijos de un nodo cuyo radio de mundo es el PATRON DE BITS 0 salvo el bit 11 (0x800). El bound
''' del nodo sale de la UpdateWorldBound de su clase (BoundOf); de una clase no transcripta no se decide nada.</para>
''' <para><b>Pto 3a, NiVisController en reposo.</b> El valor bool en reposo (ControllerRestLaw.BoolAtRest) escribe el bit 0 del
''' objeto = !valor (FO4 0x141744FD0, SSE 0x140F8A340): pisa el bit oculto del archivo. Oculto por él: motivo del aviso
''' (BoundCullReason); mostrado por él: el nodo no cuenta como ancestro oculto.</para>
''' <para>NO está acá (huecos declarados): el bound exacto de una forma con skin (SSE 0x140EFC870, FO4 0x1416D7D50; acá cuenta como
''' radio &gt; 0, resultado4 pto 14), el test de planos con bound NaN (FO4 0x1416E6A40), NiGeometry heredada, NiLODNode, las clases con
''' su propia ranura 48 (BSDamageStage, BSMasterParticleSystem, BSFaceGenNiNode) y las que no son dueñas de la ranura 51 (NiBillboardNode),
''' un nodo de lista de geometrías anidado (BSFadeNode / BSLeafAnimNode / BSTreeNode que no es la raíz), el BSMultiBound con datos
''' (vfunc +0x170), en Skyrim SE todo nodo que no sea NiNode y el constructor del bit 13 (PENDIENTE DE CITA), una forma con su propio
''' bit oculto que su NiVisController muestra (sigue siendo helper: IsHelperShape), el nodo nuevo que envuelve una raíz Wrapped (su
''' corte no se evalúa: la raíz vieja queda Unknown, rev-16), si los controladores de una raíz convertida pasan al nodo nuevo, y la
''' raíz de un NIF de pieza de actor (armadura, cuerpo): en el juego cuelga del esqueleto como nodo de lista ANIDADO (NOT TRACED) y
''' acá se trata como la raíz de su NIF (rev-21: el barrido mide esos cortes en los NIF con formas con skin).</para></summary>
Public Module NifSceneVisibility

    ''' <summary>El estado de una forma. El default (Nothing) = visible: sin ancestro oculto y sin descarte del culling.</summary>
    Public Structure LoadState
        ''' <summary>C-ANC: algún nodo de su cadena queda oculto tras la carga (su bit de archivo o de nodo de rango; no uno cuyo
        ''' NiVisController escribe en reposo, pto 3a). Se muestra con la casilla "Render hidden shapes".</summary>
        Public ReadOnly HiddenByAncestor As Boolean
        ''' <summary>Por qué el culling del juego no la dibuja nunca, como lo dice el aviso de una vista COMPUESTA (no se dibuja): C-NAN,
        ''' un nodo de su cadena de radio de mundo 0 (pto 14) o un NiVisController que la oculta en reposo (pto 3a); Nothing = el juego
        ''' la dibuja.</summary>
        Public ReadOnly BoundCullReason As String
        ''' <summary>El mismo motivo como lo dice el aviso de una vista de PIEZA, que la dibuja para editarla
        ''' (RenderIntent.DrawEngineSkippedForEditing, C2 L5). Nothing exactamente cuando BoundCullReason es Nothing.</summary>
        Public ReadOnly BoundCullShown As String
        ''' <summary>Rev-18: when a node of radius 0 cuts it (pto 14), which branch of the law did (the sweep reads it); None otherwise.</summary>
        Friend ReadOnly NodeCause As NodeCullCause
        ''' <summary>Ola 7e: the frame notice of BoundCullReason (PreviewModel.FrameNoticeId, classified by PreviewModel.NoticeClass). Read
        ''' only when BoundCullReason is not Nothing.</summary>
        Friend ReadOnly BoundCullReasonId As PreviewModel.FrameNoticeId
        ''' <summary>Ola 7e: the frame notice of BoundCullShown. Read only when BoundCullShown is not Nothing.</summary>
        Friend ReadOnly BoundCullShownId As PreviewModel.FrameNoticeId
        ''' <summary>Ola 7b: why the biped's skinned attach leaves it out of the actor (GameAttachLaw.OutsideSkinnedAttach), as a
        ''' COMPOSITE view's notice says it; Nothing = the attach keeps it.</summary>
        Public ReadOnly OutsideAttachReason As String
        ''' <summary>The same reason as a PIECE view's notice says it (drawn for editing). Nothing exactly when OutsideAttachReason is.</summary>
        Public ReadOnly OutsideAttachShown As String
        ''' <summary>The game does not draw it: its culling (BoundCullReason) or the biped's attach (OutsideAttachReason). The one test the
        ''' draw gate, the occluder gate and the cull registration read.</summary>
        Public ReadOnly Property GameSkips As Boolean
            Get
                Return BoundCullReason IsNot Nothing OrElse OutsideAttachReason IsNot Nothing
            End Get
        End Property
        ''' <summary>Ola 7b: <paramref name="s"/> with the attach reason.</summary>
        Friend Sub New(s As LoadState, outsideAttachReason As String, outsideAttachShown As String)
            Me.HiddenByAncestor = s.HiddenByAncestor
            Me.BoundCullReason = s.BoundCullReason
            Me.BoundCullShown = s.BoundCullShown
            Me.NodeCause = s.NodeCause
            Me.BoundCullReasonId = s.BoundCullReasonId
            Me.BoundCullShownId = s.BoundCullShownId
            Me.OutsideAttachReason = outsideAttachReason
            Me.OutsideAttachShown = outsideAttachShown
        End Sub
        Public Sub New(hiddenByAncestor As Boolean, boundCullReason As String, boundCullShown As String)
            Me.HiddenByAncestor = hiddenByAncestor
            Me.BoundCullReason = boundCullReason
            Me.BoundCullShown = boundCullShown
        End Sub
        Friend Sub New(hiddenByAncestor As Boolean, boundCullReason As String, boundCullShown As String, nodeCause As NodeCullCause,
                       boundCullReasonId As PreviewModel.FrameNoticeId, boundCullShownId As PreviewModel.FrameNoticeId)
            Me.New(hiddenByAncestor, boundCullReason, boundCullShown)
            Me.NodeCause = nodeCause
            Me.BoundCullReasonId = boundCullReasonId
            Me.BoundCullShownId = boundCullShownId
        End Sub
    End Structure

    ''' <summary>Rev-18: the branch of the pto 14 law that leaves a node with radius 0.</summary>
    Friend Enum NodeCullCause
        None
        ''' <summary>NiNode with flag bit 13: the constructor's radius 0 (0x1416BF48F..495; 0x1416E7B59..B64).</summary>
        Bit13
        ''' <summary>NiNode's union: no child of radius dword &lt;&gt; 0, or a union of them of radius +0.0 (0x1416BF4A5..567).</summary>
        ChildrenRadius0
        ''' <summary>The root BSFadeNode: no geometry of its list with radius &gt; 0 (0x14217EE50).</summary>
        RootList
        ''' <summary>BSOrderedNode's static bound of radius 0 (0x142245150).</summary>
        StaticBound
        ''' <summary>NiSwitchNode: its active child's bound (0x1416CBF20).</summary>
        ActiveChild
    End Enum

    ''' <summary>Rev-16: what the model loader makes of a Fallout 4 NIF's root (load flag 0x10: 0x1417B39B0 -&gt; 0x14217C970).</summary>
    Friend Enum RootKind
        ''' <summary>A BSFadeNode root: kept as it is.</summary>
        Kept
        ''' <summary>Any other class: replaced by a new BSFadeNode built from it (0x1417B3BB8..BD6, 0x14217C970): flags = bits 10 and 12 of
        ''' the old node plus the constructor's 0xE (0x14217CA10..A5F; 0x1416C931D..925) - so no hidden bit, no 0x800, no bit 13 -, the
        ''' scale as |s| (andps 0x14217CDC0..DCD), the children moved to it (a NiSwitchNode's too: its active-child rule is gone).</summary>
        Converted
        ''' <summary>NiBillboardNode, BSMultiBoundNode and its subclasses, exactly BSMasterParticleSystem (RTTI tests 0x14217CA75 / A9B /
        ''' AB9 -&gt; 0x14217CC2D): the old root stays, as the child of a new node, with its own class's law.</summary>
        Wrapped
    End Enum

    ''' <summary>Rev-16: the RootKind of a Fallout 4 root node.</summary>
    Friend Function RootKindOf(root As NiNode) As RootKind
        ' rev-25: BSLeafAnimNode (vt 0x142903E50) and BSTreeNode (vt 0x1429040E0) have BSFadeNode's vt+0x30 0x14043FEC0: the loader keeps
        ' them (0x1417B3BAC..BB2).
        If TypeOf root Is BSFadeNode OrElse IsGeometryListClass(root) Then Return RootKind.Kept
        If TypeOf root Is NiBillboardNode OrElse TypeOf root Is BSMultiBoundNode OrElse root.GetType() Is GetType(BSMasterParticleSystem) Then Return RootKind.Wrapped
        Return RootKind.Converted
    End Function

    ''' <summary>Rev-16: the chain's top is a Fallout 4 root the loader converts (RootKindOf = Converted).</summary>
    Private Function IsConvertedRoot(chain As List(Of NiNode), isSse As Boolean) As Boolean
        Return Not GateLoadLawsOnly AndAlso Not isSse AndAlso chain.Count > 0 AndAlso RootKindOf(chain(chain.Count - 1)) = RootKind.Converted
    End Function

    ''' <summary>GATE ONLY (sin UI): ShadowGate --scene-visibility, control negativo del pixel. True apaga las leyes de bound: C-NAN y el
    ''' corte de nodo de radio 0 (pto 14).</summary>
    Friend GateIgnoreBoundCull As Boolean = False

    ''' <summary>GATE ONLY (sin UI): ShadowGate --scene-visibility-sweep (rev-09). True evaluates only the load laws as they were before
    ''' the ola 0c (C-ANC, C-NAN): no NiVisController at rest (pto 3a), no node of radius 0 (pto 14), no root conversion (rev-16) - the
    ''' "before" of the sweep.</summary>
    Friend GateLoadLawsOnly As Boolean = False

    ''' <summary>Los motivos del aviso (inglés: UI), uno por rama de la ley, cada uno en sus dos formas (C2: compuesta = no se
    ''' dibuja, el motivo dice que el juego no la dibuja; pieza = se dibuja, el aviso antepone "Drawn by the preview, not by the
    ''' game"). El gate compara contra éstos.</summary>
    Friend Function NoticeNaN() As (Drawn As String, Undrawn As String)
        Return ("Skyrim SE: bounding sphere with NaN, loaded as radius 0",
                "Bounding sphere with NaN, loaded by Skyrim SE as radius 0: Skyrim SE does not draw it")
    End Function
    Friend Function NoticeRadius0(isSse As Boolean) As (Drawn As String, Undrawn As String)
        Return ($"{GameName(isSse)}: bounding sphere of radius 0",
                $"Bounding sphere of radius 0: {GameName(isSse)} does not draw it")
    End Function
    Friend Function NoticeScale0(isSse As Boolean) As (Drawn As String, Undrawn As String)
        Return ($"{GameName(isSse)}: world bound of radius 0 from the scales of its node chain",
                $"World bound of radius 0 from the scales of its node chain: {GameName(isSse)} does not draw it")
    End Function
    ''' <summary>Pto 14: a node of its chain has a world bound of radius 0 (OnVisible does not walk its children).</summary>
    Friend Function NoticeNodeRadius0(isSse As Boolean) As (Drawn As String, Undrawn As String)
        Return ($"{GameName(isSse)}: under a node whose world bound has radius 0",
                $"Under a node whose world bound has radius 0: {GameName(isSse)} does not draw it")
    End Function
    ''' <summary>Pto 3a: a NiVisController of the shape or of a node of its chain hides it at rest.</summary>
    Friend Function NoticeVisController(isSse As Boolean) As (Drawn As String, Undrawn As String)
        Return ($"{GameName(isSse)}: hidden by a visibility controller at rest",
                $"Hidden by a visibility controller at rest: {GameName(isSse)} does not draw it")
    End Function
    ''' <summary>Item 2b r19b-02: a geometry class Fallout 4's culling never registers (Fo4GeometryClass.NotRegistered).</summary>
    Friend Function NoticeFo4NotRegistered(className As String) As (Drawn As String, Undrawn As String)
        Return ($"Fallout 4: {className}, a NiGeometry class its culling never registers",
                $"{className}, a NiGeometry class Fallout 4's culling never registers: Fallout 4 does not draw it")
    End Function
    ''' <summary>Item 2b r19b-13: a geometry class whose registration by Fallout 4's culling is not traced (Fo4GeometryClass.Untraced).</summary>
    Friend Function NoticeFo4Untraced(className As String) As (Drawn As String, Undrawn As String)
        Return ($"Fallout 4: {className}, a geometry class whose registration by the culling is not traced",
                $"{className}: a geometry class whose registration by Fallout 4's culling is not traced - not drawn")
    End Function
    Private Function GameName(isSse As Boolean) As String
        Return If(isSse, "Skyrim SE", "Fallout 4")
    End Function

    ''' <summary>Ola 7e: a culling notice (its two forms, Notice*) with the frame notice of each form (PreviewModel.FrameNoticeId).</summary>
    Private Function WithIds(n As (Drawn As String, Undrawn As String), drawnId As PreviewModel.FrameNoticeId,
                             undrawnId As PreviewModel.FrameNoticeId) As (Drawn As String, Undrawn As String, DrawnId As PreviewModel.FrameNoticeId, UndrawnId As PreviewModel.FrameNoticeId)
#If DEBUG Then
        ' Ola 7e tester (ParityGate scene-visibility, CullIdsFails): each form with the other form's notice.
        If Fo4GBufferConstants.GateMutantOn("ntc-cull-ids-swapped") Then Return (n.Drawn, n.Undrawn, undrawnId, drawnId)
#End If
        Return (n.Drawn, n.Undrawn, drawnId, undrawnId)
    End Function

    ''' <summary>Item 2b r19b-02: the exact class of a Fallout 4 geometry as the engine builds it from the NIF, for the laws that read it
    ''' (Tools/re-docs/RE_FO4_TECHNIQUE_BUCKET_ORDER_2026-10-19, Addendum r19b-02; RTTI and vtables in re2\rtti.py).</summary>
    Friend Enum Fo4GeometryClass
        ''' <summary>A class this law does not trace (not one of the app's loadable shape classes, Nifcontent_Class_Manolo.SupportedShape).</summary>
        Untraced
        ''' <summary>NiTriShape (vtable 0x142688778), NiTriStrips (0x14268AC00), BSLODTriShape (0x14268AF70), BSSegmentedTriShape
        ''' (0x142687F28): NiTriBasedGeom : NiGeometry, not BSGeometry. Their OnVisible (slot 57) is 0x1416C9110 `ret` (BSSegmentedTriShape's
        ''' 0x1416E6EC0 only calls it): the culling (NiNode::OnVisible 0x1416BF8C0 -&gt; BSCullingProcess 0x1417E1C60 -&gt; the object's slot
        ''' 57, 0x1417E1D98 / 0x1417E1E45) never registers them in an accumulator - no pass, never drawn.</summary>
        NotRegistered
        ''' <summary>BSGeometry family: slot 57 = 0x1416D5720 registers the shape when it has a shader property (0x1416D5751..5764).</summary>
        BSTriShape
        BSDynamicTriShape
        BSMeshLODTriShape
        BSSubIndexTriShape
    End Enum

    ''' <summary>Item 2b r19b-02: <paramref name="shape"/>'s exact class (Fo4GeometryClass). The one table of the class names: the
    ''' z-prepass class law (MaterialData.Fo4PrepassGeometryClass), the type byte (MaterialData.Fo4GeometryType) and the culling
    ''' (Evaluate) read it.</summary>
    Friend Function Fo4GeometryClassOf(shape As INiShape) As Fo4GeometryClass
        If shape Is Nothing Then Return Fo4GeometryClass.Untraced
        Dim t = shape.GetType()
        If t Is GetType(BSTriShape) Then Return Fo4GeometryClass.BSTriShape
        If t Is GetType(BSDynamicTriShape) Then Return Fo4GeometryClass.BSDynamicTriShape
        If t Is GetType(BSMeshLODTriShape) Then Return Fo4GeometryClass.BSMeshLODTriShape
        If t Is GetType(BSSubIndexTriShape) Then Return Fo4GeometryClass.BSSubIndexTriShape
        If t Is GetType(NiTriShape) OrElse t Is GetType(NiTriStrips) OrElse t Is GetType(BSLODTriShape) OrElse t Is GetType(BSSegmentedTriShape) Then
            Return Fo4GeometryClass.NotRegistered
        End If
        Return Fo4GeometryClass.Untraced
    End Function

    Public Function Evaluate(shape As IRenderableShape) As LoadState
        If shape Is Nothing Then Return Nothing
        Return WithGameAttach(Evaluate(shape.NifContent, shape.NifShape), OutsideAttachOf(shape))
    End Function

    ''' <summary>Ola 7b: the biped's skinned attach leaves <paramref name="shape"/> out of the actor (GameAttachLaw.OutsideSkinnedAttach,
    ''' which the host's declared path decides, IRenderableShape.GameAttach) - the law's one caller. It reads only what a load fixes (the
    ''' NIF's tree and skin instances, the declared path): a mesh evaluates it once, when it is built (RenderableMesh), and each frame reads
    ''' that value (p7b-01).</summary>
    Friend Function OutsideAttachOf(shape As IRenderableShape) As Boolean
        Return GameAttachLaw.OutsideSkinnedAttach(shape)
    End Function

    ''' <summary>Ola 7b: <paramref name="state"/> plus the biped's skinned attach (<paramref name="outsideAttach"/>, OutsideAttachOf). The
    ''' NIF-only Evaluate overloads do not know the path and leave it out. LoadState is a structure: nothing is allocated.</summary>
    Friend Function WithGameAttach(state As LoadState, outsideAttach As Boolean) As LoadState
        If Not outsideAttach Then Return state
        Return New LoadState(state, GameAttachLaw.OutsideAttachNotice, GameAttachLaw.OutsideAttachShownNotice)
    End Function

    Public Function Evaluate(nif As Nifcontent_Class_Manolo, shape As INiShape) As LoadState
        Return Evaluate(nif, shape, Nothing)
    End Function

    ''' <summary>Evaluate with <paramref name="bounds"/> = the node bounds of this NIF in this frame (SceneBoundMemo.ForFrame, rev-13), or
    ''' Nothing for a map of this call only. <paramref name="cullingEngineIsSse"/> = the engine whose culling walks the scene (the
    ''' frame's: RenderableMesh.SceneVisibility, r19b-13); Nothing = the NIF's own game (TracedEngine), for a caller without a frame.
    ''' Only the geometry-class law reads it: the other laws stay the NIF's game's, as before.</summary>
    Friend Function Evaluate(nif As Nifcontent_Class_Manolo, shape As INiShape, bounds As Dictionary(Of NiAVObject, NodeBound),
                             Optional cullingEngineIsSse As Boolean? = Nothing) As LoadState
        If nif Is Nothing OrElse shape Is Nothing Then Return Nothing
        Dim chain = ParentChain(nif, shape)
        Dim engine = If(GateLoadLawsOnly, CType(Nothing, Boolean?), TracedEngine(nif))
        ' C-ANC, with pto 3a: a node whose NiVisController writes at rest takes that value instead of its load bit.
        Dim hidden = False
        Dim ancestorVis As (Drawn As String, Undrawn As String, DrawnId As PreviewModel.FrameNoticeId, UndrawnId As PreviewModel.FrameNoticeId) = Nothing
        ' Rev-16: a converted Fallout 4 root keeps no hidden bit (its flags are bits 10 / 12 + 0xE, 0x14217CA10..A5F).
        Dim tracedGame = TracedEngine(nif)
        Dim rootConverted = tracedGame.HasValue AndAlso IsConvertedRoot(chain, tracedGame.Value)
        For Each a In chain
            Dim v = If(engine.HasValue, VisibleAtRest(nif, a, engine.Value), CType(Nothing, Boolean?))
            If v.HasValue Then
                If Not v.Value AndAlso ancestorVis.Undrawn Is Nothing Then
                    ancestorVis = WithIds(NoticeVisController(engine.Value), PreviewModel.FrameNoticeId.VisControllerShown, PreviewModel.FrameNoticeId.VisControllerNotDrawn)
                End If
            ElseIf NodeHiddenAtLoad(a) AndAlso Not (rootConverted AndAlso a Is chain(chain.Count - 1)) Then
                hidden = True
            End If
        Next
        Dim own = If(engine.HasValue, VisibleAtRest(nif, TryCast(shape, NiAVObject), engine.Value), CType(Nothing, Boolean?))
        Dim cull As (Drawn As String, Undrawn As String, DrawnId As PreviewModel.FrameNoticeId, UndrawnId As PreviewModel.FrameNoticeId)
        Dim causa = NodeCullCause.None
        If own.HasValue AndAlso Not own.Value Then
            cull = WithIds(NoticeVisController(engine.Value), PreviewModel.FrameNoticeId.VisControllerShown, PreviewModel.FrameNoticeId.VisControllerNotDrawn)
        Else
            cull = BoundCull(nif, shape, chain)
            If cull.Undrawn Is Nothing Then cull = ancestorVis
            If cull.Undrawn Is Nothing Then
                Dim nc = NodeCull(nif, chain, engine, bounds)
                cull = WithIds((nc.Drawn, nc.Undrawn), PreviewModel.FrameNoticeId.NodeRadius0Shown, PreviewModel.FrameNoticeId.NodeRadius0NotDrawn)
                causa = nc.Cause
            End If
        End If
        ' Item 2b r19b-02 / r19b-13: when Fallout 4's culling walks the scene (the frame's engine; without a frame, the NIF's game) it
        ' reaches the object and calls its OnVisible (slot 57), which for the NiGeometry family is `ret` (Fo4GeometryClass.NotRegistered):
        ' the last cut of the walk, after the node and bound laws above. A class this law does not trace (Untraced; none the app loads,
        ' Nifcontent_Class_Manolo.SupportedShape): HUECO, declared in the notice, and not registered - no G-buffer pass is built for it.
        Dim culler = If(GateLoadLawsOnly, CType(Nothing, Boolean?), If(cullingEngineIsSse, engine))
        If cull.Undrawn Is Nothing AndAlso culler.HasValue AndAlso Not culler.Value Then
            Select Case Fo4GeometryClassOf(shape)
                Case Fo4GeometryClass.NotRegistered
#If DEBUG Then
                    ' Item 2b oracles' directed mutant (Fo4GBufferConstants.GateMutantOn): the law before r19b-02 - the culling registers
                    ' the NiGeometry family (with MaterialData.Fo4GeometryType's type byte 0: no packet). Here and not in Fo4GeometryClassOf:
                    ' since r19b-13 an Untraced class is not registered either, so a mutant returning Untraced no longer restores that law.
                    If Fo4GBufferConstants.GateMutantOn("geomclass-notreg-ignored") Then Exit Select
#End If
                    cull = WithIds(NoticeFo4NotRegistered(shape.GetType().Name), PreviewModel.FrameNoticeId.Fo4UnregisteredClassShown,
                                   PreviewModel.FrameNoticeId.Fo4UnregisteredClassNotDrawn)
                Case Fo4GeometryClass.Untraced
                    cull = WithIds(NoticeFo4Untraced(shape.GetType().Name), PreviewModel.FrameNoticeId.Fo4UntracedClassShown,
                                   PreviewModel.FrameNoticeId.Fo4UntracedClassNotDrawn)
            End Select
        End If
        Return New LoadState(hidden, cull.Undrawn, cull.Drawn, causa, cull.UndrawnId, cull.DrawnId)
    End Function

    ''' <summary>El bit oculto de un nodo tras su LoadBinary: el del archivo, salvo un nodo de rango, cuyo SetCurrent lo pisa con
    ''' Not (Min &lt;= Current &lt;= Max) (SSE 0x140FDD838..0x140FDD865, FO4 0x1417D38E8..0x1417D390B). Byte de VB = sin signo.</summary>
    Public Function NodeHiddenAtLoad(node As NiNode) As Boolean
        Dim rn = TryCast(node, BSRangeNode)
        If rn IsNot Nothing Then Return Not (rn.Min <= rn.Current AndAlso rn.Current <= rn.Max)
        Return (node.Flags_ui And 1UI) <> 0UI
    End Function

    ''' <summary>Pto 3a: the visibility a NiVisController of <paramref name="obj"/>'s controller chain writes at the preview's rest (FO4
    ''' Update 0x141744FD0, vt 0x14268E038 slot 42: SetAppCulled(!value), bit 0 of [obj+0x108]; SSE 0x140F8A340, vt 0x141A5C0E0 slot 39:
    ''' bit 0 of [obj+0xF4] = !value, 0x140F8A407..426): True = shown (value &lt;&gt; 0), False = hidden (value 0); Nothing when no
    ''' controller of the chain writes (ControllerRestLaw.BoolAtRest). Several: the last of the chain wins (UpdateControllers 0x1416CF440,
    ''' DEDUCED as in ControllerRestLaw). The chain is the object's own, the one its update walks (as MaterialAtRest reads the
    ''' property's); the controller's Target field is not read.</summary>
    Friend Function VisibleAtRest(nif As Nifcontent_Class_Manolo, obj As NiAVObject, isSse As Boolean) As Boolean?
        If obj Is Nothing Then Return Nothing
        Dim r As Boolean? = Nothing
        For Each c In nif.CadenaDeControllers(obj.Controller)
            Dim vc = TryCast(c, NiVisController)
            If vc Is Nothing Then Continue For
            Dim interp = nif.GetBlock(vc.Interpolator)
            Dim bi = TryCast(interp, NiBoolInterpolator)
            Dim data = If(bi Is Nothing, Nothing, nif.GetBlock(bi.Data))
            Dim v = ControllerRestLaw.BoolAtRest(vc, interp, data, isSse)
            If v.HasValue Then r = (v.Value <> 0)
        Next
        Return r
    End Function

    ''' <summary>The parents of <paramref name="obj"/>, from its direct parent to the root, by the app's parent law (NifFile.GetParentNode,
    ''' the one Transform_Class.GetGlobalTransform walks). The cycle guard is not a threshold: a malformed NIF with a cycle of Children
    ''' would hang the render.</summary>
    Friend Function ParentChain(nif As NifFile, obj As INiObject) As List(Of NiNode)
        Dim chain As New List(Of NiNode)
        Dim p = nif.GetParentNode(obj)
        While p IsNot Nothing AndAlso Not chain.Contains(p)
            chain.Add(p)
            p = nif.GetParentNode(p)
        End While
        Return chain
    End Function

    ''' <summary>The world scale of an object: its parents' scales from the root down, times its own, in Single (padre x local: SSE
    ''' 0x140F0303C..0x140F03049, FO4 0x1416C8CAD -&gt; 0x140344966). <paramref name="chain"/> = ParentChain; <paramref name="rootAbs"/> =
    ''' the root is a converted Fallout 4 root, whose scale enters as |s| (andps 0x14217CDC0..DCD, rev-16).</summary>
    Friend Function WorldScale(nif As NifFile, chain As List(Of NiNode), ownScale As Single, rootAbs As Boolean) As Single
        Dim ws As Single = 1.0F
        For i = chain.Count - 1 To 0 Step -1
            Dim s = DrawnScaleOf(nif, chain(i))
            ws = ws * If(rootAbs AndAlso i = chain.Count - 1, Math.Abs(s), s)
        Next
        Return ws * ownScale
    End Function

    ''' <summary>Ola 0c parte 2 (the connection): the scale <paramref name="o"/> is drawn with - what its NiTransformControllers write at
    ''' rest (NodeRestLayer.DrawnScale) - so the world scales and bounds of this module follow the drawn nodes. A NifFile that is not
    ''' the app's NIF content (no controller chain reader): the block's own.</summary>
    Private Function DrawnScaleOf(nif As NifFile, o As NiAVObject) As Single
        Dim m = TryCast(nif, Nifcontent_Class_Manolo)
        Return If(m Is Nothing, o.Scale, NodeRestLayer.DrawnScale(o, m))
    End Function

    ''' <summary>Ola 0c parte 2: the world <paramref name="o"/> is drawn with (NodeRestLayer.DrawnGlobal).</summary>
    Private Function DrawnWorldOf(nif As Nifcontent_Class_Manolo, o As NiAVObject) As Transform_Class
        Return NodeRestLayer.DrawnGlobal(o, nif)
    End Function

    ''' <summary>The depth-first walk of OnVisible from block <paramref name="root"/> (children 0..n-1: SSE 0x140EE2D10..D33, FO4
    ''' 0x1416BF910..930): each block with its parent's index (-1 for the root), in visit order. A block in <paramref name="visited"/> is
    ''' not visited again (a child under two parents: its first visit; a malformed cycle cannot hang). The walk of the registration
    ''' order (PreviewModel.NifWalkOrder).</summary>
    Friend Iterator Function WalkDepthFirst(nif As NifFile, root As Integer, visited As HashSet(Of Integer)) As IEnumerable(Of (Block As Integer, Parent As Integer))
        Dim stack As New Stack(Of (Block As Integer, Parent As Integer))
        stack.Push((root, -1))
        Do While stack.Count > 0
            Dim e = stack.Pop()
            If Not visited.Add(e.Block) Then Continue Do
            Yield e
            Dim n = TryCast(nif.Blocks(e.Block), NiNode)
            If n?.Children Is Nothing Then Continue Do
            Dim kids = n.Children.Indices.ToList()
            For j = kids.Count - 1 To 0 Step -1
                If kids(j) >= 0 AndAlso kids(j) < nif.Blocks.Count Then stack.Push((kids(j), e.Block))
            Next
        Loop
    End Function

    ''' <summary>The NiNode blocks no node lists as a child, in block order: the roots the walks start from (PreviewModel.NifWalkOrder and
    ''' PreviewModel.Fo4FadeNodes).</summary>
    Friend Function ParentlessNodes(nif As NifFile) As List(Of Integer)
        Dim hasParent As New HashSet(Of Integer)
        For Each b In nif.Blocks
            Dim n = TryCast(b, NiNode)
            If n?.Children Is Nothing Then Continue For
            For Each k In n.Children.Indices
                If k >= 0 Then hasParent.Add(k)
            Next
        Next
        Return Enumerable.Range(0, nif.Blocks.Count).Where(Function(i) TypeOf nif.Blocks(i) Is NiNode AndAlso Not hasParent.Contains(i)).ToList()
    End Function

    ''' <summary>Ola 0c ptos 25 / 14 (resultado5 item 2): the geometry list of a BSFadeNode root as 0x14217ED30 builds it - the recursive
    ''' visitor 0x14217F2C0 from <paramref name="root"/>: every BSGeometry (vt+0x40, 0x14217F315; it also writes [prop+0x48] = the root,
    ''' 0x14217F34E..357) appended in DFS pre-order by child index (0x14217F621..), the hidden ones included (0x14217F3AB..3E3), nested
    ''' BSFadeNodes walked (0x14217F501..51F), a NiSwitchNode walking only its active child [+0x144] (0x14217F531..58C). Each with its
    ''' parent's world scale (WorldScale's law, carried down; a converted root's scale as |s|, rev-16). A block already visited is not
    ''' visited again (the cycle guard of a malformed NIF; a block under two parents is listed once: NOT TRACED what the visitor does).
    ''' HasUnclassified (rev-17): the walk met a NiAVObject that is neither a NiNode nor a BSTriShape (a particle system, a light...),
    ''' whose membership in the list is not classified.</summary>
    Friend Function FadeListGeometries(nif As NifFile, root As Integer) As (Items As List(Of (Block As Integer, WsParent As Single)), HasUnclassified As Boolean)
        Dim out As New List(Of (Block As Integer, WsParent As Single))
        Dim unclassified = False
        Dim rootNode = TryCast(nif.Blocks(root), NiNode)
        Dim converted = rootNode IsNot Nothing AndAlso RootKindOf(rootNode) = RootKind.Converted
        Dim visited As New HashSet(Of Integer)
        Dim stack As New Stack(Of (Block As Integer, WsParent As Single))
        stack.Push((root, 1.0F))
        Do While stack.Count > 0
            Dim e = stack.Pop()
            If Not visited.Add(e.Block) Then Continue Do
            Dim o = TryCast(nif.Blocks(e.Block), NiAVObject)
            If o Is Nothing Then Continue Do
            If TypeOf o Is BSTriShape Then
                out.Add(e)
                Continue Do
            End If
            Dim n = TryCast(o, NiNode)
            If n Is Nothing Then
                unclassified = True
                Continue Do
            End If
            If n.Children Is Nothing Then Continue Do
            Dim ns = DrawnScaleOf(nif, n)
            Dim ws As Single = e.WsParent * If(converted AndAlso e.Block = root, Math.Abs(ns), ns)
            Dim kids = n.Children.Indices.ToList()
            Dim sw = TryCast(n, NiSwitchNode)
            If sw IsNot Nothing AndAlso Not (converted AndAlso e.Block = root) Then
                Dim activo = CLng(sw.Index)
                kids = If(activo < kids.Count, New List(Of Integer) From {kids(CInt(activo))}, New List(Of Integer))
            End If
            For j = kids.Count - 1 To 0 Step -1
                If kids(j) >= 0 AndAlso kids(j) < nif.Blocks.Count Then stack.Push((kids(j), ws))
            Next
        Loop
        Return (out, unclassified)
    End Function

    ''' <summary>The engine whose laws this module transcribes for <paramref name="nif"/>: True Skyrim SE, False Fallout 4, Nothing any
    ''' other (no law is applied).</summary>
    Private Function TracedEngine(nif As NifFile) As Boolean?
        Dim ver = nif.Header?.Version
        If ver Is Nothing Then Return Nothing
        If ver.IsSSE() Then Return True
        If ver.IsFO4() Then Return False
        Return Nothing
    End Function

    ''' <summary>C-NAN. <paramref name="chain"/> = padres de la forma, del padre directo a la raíz.</summary>
    Private Function BoundCull(nif As NifFile, shape As INiShape, chain As List(Of NiNode)) As (Drawn As String, Undrawn As String, DrawnId As PreviewModel.FrameNoticeId, UndrawnId As PreviewModel.FrameNoticeId)
        If GateIgnoreBoundCull Then Return Nothing
        Dim engine = TracedEngine(nif)
        If Not engine.HasValue Then Return Nothing                           ' sólo los dos motores trazados
        Dim isSse = engine.Value
        Dim tri = TryCast(shape, BSTriShape)                                ' familia BSGeometry (0x140EFC150 / 0x1416D4EA0)
        If tri Is Nothing Then Return Nothing
        ' Con skin el bound de mundo sale de los huesos, no de la esfera del modelo (SSE 0x140EFC870, FO4 0x1416D5502).
        If shape.SkinInstanceRef IsNot Nothing AndAlso Not shape.SkinInstanceRef.IsEmpty() Then Return Nothing
        If (tri.Flags_ui And &H800UI) <> 0UI Then Return Nothing                      ' SSE 0x140FEDA9A, FO4 0x1417E1C82
        Dim g = GeometryWorldRadius(nif, tri, WorldScale(nif, chain, 1.0F, IsConvertedRoot(chain, isSse)), isSse)
        If BitConverter.SingleToInt32Bits(g.Radius) <> 0 Then Return Nothing           ' cmp dword ..., 0
        If g.NaNSphere Then Return WithIds(NoticeNaN(), PreviewModel.FrameNoticeId.BoundNaNShown, PreviewModel.FrameNoticeId.BoundNaNNotDrawn)
        If BitConverter.SingleToInt32Bits(tri.Bounds.Radius) = 0 Then
            Return WithIds(NoticeRadius0(isSse), PreviewModel.FrameNoticeId.BoundRadius0Shown, PreviewModel.FrameNoticeId.BoundRadius0NotDrawn)
        End If
        Return WithIds(NoticeScale0(isSse), PreviewModel.FrameNoticeId.ScaleZeroShown, PreviewModel.FrameNoticeId.ScaleZeroNotDrawn)
    End Function

    ''' <summary>C-NAN's world radius of a BSGeometry without skin (rev-10, the one owner for BoundCull and GeometryBound): model radius x
    ''' world scale (SSE 0x140EFCFDF, FO4 0x1416D5FD0), the world scale its parent's (<paramref name="wsParent"/>) x its own; a Skyrim SE
    ''' sphere with NaN in any of its four floats is loaded as centre 0 radius 0 (_fdtest = 2, 0x140EFC191..0x140EFC21A; FO4
    ''' 0x1416D4EA0 keeps it).</summary>
    Private Function GeometryWorldRadius(nif As NifFile, tri As BSTriShape, wsParent As Single, isSse As Boolean) As (Radius As Single, NaNSphere As Boolean)
        Dim b = tri.Bounds
        Dim nanSphere = isSse AndAlso (Single.IsNaN(b.Center.X) OrElse Single.IsNaN(b.Center.Y) OrElse
                                       Single.IsNaN(b.Center.Z) OrElse Single.IsNaN(b.Radius))
        Dim r = If(nanSphere, 0.0F, b.Radius)
        Return (r * (wsParent * DrawnScaleOf(nif, tri)), nanSphere)
    End Function

    ' ================================================================= pto 14: the world bound of a node, by its class

    ''' <summary>What this module knows of an object's world bound.</summary>
    Friend Enum BoundKind
        ''' <summary>Its class's UpdateWorldBound is not transcribed (or not classified): nothing is decided from it.</summary>
        Unknown
        ''' <summary>The engine's sphere, computed (only when its radius is not &gt; 0: the cut needs the exact bits).</summary>
        Exact
        ''' <summary>A radius &gt; 0: a geometry whose world radius is &gt; 0, a skinned geometry (its bound comes from the bones,
        ''' SSE 0x140EFC870 / FO4 0x1416D7D50, resultado4 pto 14), or a union holding one - MergeBound keeps a radius &gt; 0 (it
        ''' returns A, B, or (rB + dist + rA) / 2 with |rB - rA| &lt; dist) or NaN when a NaN enters: never the bits 0.</summary>
        Positive
    End Enum

    Friend Structure NodeBound
        Public ReadOnly Kind As BoundKind
        Public ReadOnly Sphere As EngineWorldBound.Sphere
        ''' <summary>Rev-18: the branch of the law that produced an Exact bound (the sweep's sub-cause).</summary>
        Public ReadOnly Cause As NodeCullCause
        Public Sub New(kind As BoundKind, sphere As EngineWorldBound.Sphere, Optional cause As NodeCullCause = NodeCullCause.None)
            Me.Kind = kind : Me.Sphere = sphere : Me.Cause = cause
        End Sub
    End Structure

    Private Function UnknownBound() As NodeBound
        Return New NodeBound(BoundKind.Unknown, New EngineWorldBound.Sphere())
    End Function
    Private Function PositiveBound() As NodeBound
        Return New NodeBound(BoundKind.Positive, New EngineWorldBound.Sphere())
    End Function
    ''' <summary>Radius 0 (its centre is not read: a union skips a radius dword 0), with the branch that gave it.</summary>
    Private Function ZeroBound(cause As NodeCullCause) As NodeBound
        Return New NodeBound(BoundKind.Exact, New EngineWorldBound.Sphere(Vector3.Zero, 0.0F), cause)
    End Function
    Private Function IsPositive(b As NodeBound) As Boolean
        Return b.Kind = BoundKind.Positive OrElse (b.Kind = BoundKind.Exact AndAlso b.Sphere.Radius > 0.0F)
    End Function

    ''' <summary>Pto 14: OnVisible (FO4 0x1416BF8C0 slot 57, SSE 0x140EE2CC0 slot 52; BSCullingProcess FO4 0x1417E1C60 the same) does not
    ''' walk the children of a node whose world radius is the bit pattern 0 (cmp dword) unless it has flag 0x800. <paramref name="chain"/>
    ''' = the shape's parents, direct parent first; walked from the root down, as OnVisible. A converted Fallout 4 root (rev-16) has no
    ''' 0x800 (its flags are bits 10 / 12 + 0xE) and its scale enters as |s|.</summary>
    Private Function NodeCull(nif As Nifcontent_Class_Manolo, chain As List(Of NiNode), engine As Boolean?,
                              bounds As Dictionary(Of NiAVObject, NodeBound)) As (Drawn As String, Undrawn As String, Cause As NodeCullCause)
        If GateIgnoreBoundCull OrElse Not engine.HasValue OrElse chain.Count = 0 Then Return (Nothing, Nothing, NodeCullCause.None)
        Dim memo = If(bounds, New Dictionary(Of NiAVObject, NodeBound)(ReferenceEqualityComparer.Instance))
        Dim root = chain(chain.Count - 1)
        Dim converted = IsConvertedRoot(chain, engine.Value)
        Dim wsParent As Single = 1.0F
        For i = chain.Count - 1 To 0 Step -1
            Dim n = chain(i)
            Dim esRaizConvertida = converted AndAlso i = chain.Count - 1
            If esRaizConvertida OrElse (n.Flags_ui And &H800UI) = 0UI Then
                Dim b = BoundOf(nif, n, wsParent, root, engine.Value, memo)
                If b.Kind = BoundKind.Exact AndAlso BitConverter.SingleToInt32Bits(b.Sphere.Radius) = 0 Then
                    Dim nr = NoticeNodeRadius0(engine.Value)
                    Return (nr.Drawn, nr.Undrawn, b.Cause)
                End If
            End If
            Dim ns = DrawnScaleOf(nif, n)
            wsParent = wsParent * If(esRaizConvertida, Math.Abs(ns), ns)
        Next
        Return (Nothing, Nothing, NodeCullCause.None)
    End Function

    ''' <summary>The world bound <paramref name="obj"/> gets from its class's update (pto 14 / pto 16), memoized for one evaluation.
    ''' <paramref name="wsParent"/> = its parent's world scale (WorldScale's law, carried down). Classes (resultado5 item 3: the 26 owners
    ''' of FO4 slot 51 0x1416BF480 and the ones that change slot 48): the geometry-list nodes (IsGeometryListClass) at the root; NiSwitchNode
    ''' (slot 51 0x1416CC370); BSMultiBoundNode (0x1417E9A70); BSOrderedNode (0x1422450F0); the plain NiNode law (IsNiNodeLawClass). Skyrim
    ''' SE: NiNode only (0x140EE2920; the other owners are not listed). Anything else: unknown.</summary>
    Private Function BoundOf(nif As Nifcontent_Class_Manolo, obj As NiAVObject, wsParent As Single, root As NiNode, isSse As Boolean,
                             memo As Dictionary(Of NiAVObject, NodeBound)) As NodeBound
        Dim known As NodeBound
        If memo.TryGetValue(obj, known) Then Return known
        memo(obj) = UnknownBound()                                          ' a malformed cycle reads as unknown
        Dim r As NodeBound
        Dim tri = TryCast(obj, BSTriShape)
        Dim node = TryCast(obj, NiNode)
        Dim nodeScale = If(node Is Nothing, 1.0F, DrawnScaleOf(nif, node))
        If tri IsNot Nothing Then
            r = GeometryBound(nif, tri, wsParent, isSse)
        ElseIf node Is Nothing Then
            ' Another NiAVObject class (lights, particles, legacy NiGeometry): its bound is not classified (bound16_out.txt).
            r = UnknownBound()
        ElseIf isSse Then
            r = If(node.GetType() Is GetType(NiNode), NiNodeBound(nif, node, wsParent * nodeScale, root, isSse, memo), UnknownBound())
        ElseIf node Is root Then
            ' The model loader 0x1417B39B0 with load flag 0x10 turns a root that is not a BSFadeNode into a BSFadeNode built from it
            ' (0x1417B3BB8..BD6, 0x14217C970, measured live); 0x14217DFC0 replaces its bound with its geometry list at the top-level
            ' update. Which loads set flag 0x10: REFR and actors measured live, the rest NOT TRACED. A wrapped root (rev-16) keeps its
            ' own class's law under a new node: unknown here.
            r = If(RootKindOf(node) = RootKind.Wrapped, UnknownBound(), FadeRootBound(nif, node))
        ElseIf IsGeometryListClass(node) Then
            ' A geometry-list node that is not the root: what 0x14217DFC0 does outside the top-level update is NOT TRACED.
            r = UnknownBound()
        ElseIf node.GetType() Is GetType(NiSwitchNode) Then
            r = SwitchBound(nif, DirectCast(node, NiSwitchNode), wsParent * nodeScale, root, memo)
        ElseIf node.GetType() Is GetType(BSMultiBoundNode) Then
            ' 0x1417E9A70: with a multibound ([+0x140]+0x18) the bound is its shape's vfunc +0x170 (NOT TRACED); without one, NiNode's.
            Dim mbo = nif.GetBlock(DirectCast(node, BSMultiBoundNode).MultiBound)
            r = If(mbo IsNot Nothing AndAlso mbo.Data IsNot Nothing AndAlso Not mbo.Data.IsEmpty(), UnknownBound(),
                   NiNodeBound(nif, node, wsParent * nodeScale, root, isSse, memo))
        ElseIf node.GetType() Is GetType(BSOrderedNode) Then
            r = OrderedBound(nif, DirectCast(node, BSOrderedNode), wsParent * nodeScale, root, memo)
        ElseIf IsNiNodeLawClass(node) Then
            r = NiNodeBound(nif, node, wsParent * nodeScale, root, isSse, memo)
        Else
            r = UnknownBound()
        End If
        memo(obj) = r
        Return r
    End Function

    ''' <summary>Fallout 4's NIF classes that keep NiNode's UpdateDownwardPass and its UpdateWorldBound (slot 51 0x1416BF480; resultado5
    ''' item 3): NiNode, BSValueNode, BSRangeNode, BSBlastNode, BSDebrisNode. BSDamageStage, BSMasterParticleSystem and BSFaceGenNiNode
    ''' have their own slot 48 (not transcribed: unknown).</summary>
    Private Function IsNiNodeLawClass(node As NiNode) As Boolean
        Dim t = node.GetType()
        Return t Is GetType(NiNode) OrElse t Is GetType(BSValueNode) OrElse t Is GetType(BSRangeNode) OrElse
               t Is GetType(BSBlastNode) OrElse t Is GetType(BSDebrisNode)
    End Function

    ''' <summary>The NIF classes whose slot 48 is 0x14217DFC0 (resultado5 item 3: BSFadeNode, BSLeafAnimNode, BSTreeNode; RootFacade is
    ''' not a NIF class): their bound is their geometry list's (0x14217EE50).</summary>
    Private Function IsGeometryListClass(node As NiNode) As Boolean
        Dim t = node.GetType()
        Return t Is GetType(BSFadeNode) OrElse t Is GetType(BSLeafAnimNode) OrElse t Is GetType(BSTreeNode)
    End Function

    ''' <summary>NiNode::UpdateWorldBound (FO4 0x1416BF480 slot 51, SSE 0x140EE2920 slot 47, resultado5 item 1): with flag bit 13 (0x2000)
    ''' it does not touch the bound (0x1416BF48F..495), which keeps the constructor's - radius 0.0 (NiAVObject ctor 0x1416C91F0 -&gt;
    ''' initializer 0x1416E7B00, 0x1416E7B59..B64; Skyrim SE's constructor PENDIENTE DE CITA: unknown); otherwise radius 0
    ''' (0x1416BF4A5), the first child whose world radius dword is not 0 copied whole (0x1416BF4C9..50D) and every later child whose
    ''' radius dword is not 0 merged in child-index order (0x1416BF550..567; MergeBound: FO4 0x1416E8350, SSE 0x140EFECA0 at
    ''' 0x140EE2A07). The filter is a dword compare: only +0.0 is left out; -0.0, negatives and NaN enter. A child of radius &gt; 0 keeps
    ''' the union off the bits 0 whatever follows (IsPositive).</summary>
    Private Function NiNodeBound(nif As Nifcontent_Class_Manolo, node As NiNode, wsNode As Single, root As NiNode, isSse As Boolean,
                                 memo As Dictionary(Of NiAVObject, NodeBound)) As NodeBound
        If (node.Flags_ui And &H2000UI) <> 0UI Then Return If(isSse, UnknownBound(), ZeroBound(NodeCullCause.Bit13))
        If node.Children Is Nothing Then Return ZeroBound(NodeCullCause.ChildrenRadius0)
        Dim exact As New List(Of EngineWorldBound.Sphere)
        Dim unknown = False
        For Each k In node.Children.Indices
            If k < 0 OrElse k >= nif.Blocks.Count Then Continue For
            Dim child = TryCast(nif.GetBlock(k), NiAVObject)
            If child Is Nothing Then Continue For
            Dim b = BoundOf(nif, child, wsNode, root, isSse, memo)
            If IsPositive(b) Then Return PositiveBound()
            If b.Kind = BoundKind.Unknown Then unknown = True : Continue For
            If BitConverter.SingleToInt32Bits(b.Sphere.Radius) <> 0 Then exact.Add(b.Sphere)
        Next
        If unknown Then Return UnknownBound()
        If exact.Count = 0 Then Return ZeroBound(NodeCullCause.ChildrenRadius0)
        Dim acc = exact(0)
        For i = 1 To exact.Count - 1
            acc = EngineWorldBound.MergeBound(acc, exact(i))
        Next
        Return New NodeBound(BoundKind.Exact, acc, NodeCullCause.ChildrenRadius0)
    End Function

    ''' <summary>NiSwitchNode (FO4 slot 51 0x1416CC370; vt 0x142685710, slot 48 0x1416CBF20): the active child's ([+0x144] = Index) bound,
    ''' copied (0x1416CBFC7..CC03A). An index outside the children: NOT TRACED (unknown).</summary>
    Private Function SwitchBound(nif As Nifcontent_Class_Manolo, sw As NiSwitchNode, wsNode As Single, root As NiNode,
                                 memo As Dictionary(Of NiAVObject, NodeBound)) As NodeBound
        If sw.Children Is Nothing Then Return UnknownBound()
        Dim kids = sw.Children.Indices.ToList()
        Dim idx = CLng(sw.Index)
        If idx >= kids.Count Then Return UnknownBound()
        Dim k = kids(CInt(idx))
        If k < 0 OrElse k >= nif.Blocks.Count Then Return UnknownBound()
        Dim child = TryCast(nif.GetBlock(k), NiAVObject)
        If child Is Nothing Then Return UnknownBound()
        Dim b = BoundOf(nif, child, wsNode, root, False, memo)
        If b.Kind <> BoundKind.Exact Then Return b
        Return New NodeBound(BoundKind.Exact, b.Sphere, NodeCullCause.ActiveChild)
    End Function

    ''' <summary>BSOrderedNode (slot 48 0x1422450F0 = NiNode's UpdateDownwardPass + 0x142245150, resultado5 item 3): with flag bit 13 and
    ''' [+0x150] (the NIF's Static Bound) not 0, the bound is its static bound (+0x140..14C, the NIF's Alpha Sort Bound) in world space -
    ''' the centre rotated and translated, the radius x the world scale (0x142245154..2BD); otherwise NiNode's.</summary>
    Private Function OrderedBound(nif As Nifcontent_Class_Manolo, o As BSOrderedNode, wsNode As Single, root As NiNode,
                                  memo As Dictionary(Of NiAVObject, NodeBound)) As NodeBound
        If (o.Flags_ui And &H2000UI) = 0UI OrElse o.StaticBound.RawValue = 0UI Then Return NiNodeBound(nif, o, wsNode, root, False, memo)
        Dim sb = o.AlphaSortBound
        Dim wr As Single = sb.W * wsNode
        If wr > 0.0F Then Return PositiveBound()
        Dim placed = EngineWorldBound.Place(DrawnWorldOf(nif, o), New EngineWorldBound.Sphere(New Vector3(sb.X, sb.Y, sb.Z), sb.W))
        Return New NodeBound(BoundKind.Exact, New EngineWorldBound.Sphere(placed.Center, wr), NodeCullCause.StaticBound)
    End Function

    ''' <summary>The root geometry-list node's bound (FO4 slot 48 0x14217DFC0 at the top-level update -&gt; 0x14217EE50 =
    ''' EngineWorldBound.FadeNodeBound, resultado5 item 2): radius &gt; 0 when a geometry of its list (FadeListGeometries) has a world
    ''' radius &gt; 0 (comiss, 0x14217EED0..EF1; the first copied, the rest merged: the union stays &gt; 0), else radius 0 - unknown when
    ''' the walk met an unclassified object and no geometry is &gt; 0 (rev-17).</summary>
    Private Function FadeRootBound(nif As Nifcontent_Class_Manolo, root As NiNode) As NodeBound
        Dim rootIndex As Integer
        If Not nif.GetBlockIndex(root, rootIndex) Then Return UnknownBound()
        Dim lista = FadeListGeometries(nif, rootIndex)
        For Each g In lista.Items
            Dim geo = DirectCast(nif.Blocks(g.Block), BSTriShape)
            If IsPositive(GeometryBound(nif, geo, g.WsParent, False)) Then Return PositiveBound()
        Next
        If lista.HasUnclassified Then Return UnknownBound()
        Return ZeroBound(NodeCullCause.RootList)
    End Function

    ''' <summary>A BSGeometry's world bound: skinned - Positive (from the bones); otherwise radius = model radius x world scale (C-NAN:
    ''' 0x140EFCFDF / 0x1416D5FD0), a Skyrim SE sphere with NaN loaded as centre 0 radius 0 (0x140EFC18B..0x140EFC21A); a radius &gt; 0 is
    ''' Positive, any other the exact sphere placed by its world (SSE 0x140EFC810 -&gt; 0x140EFCF20, FO4 0x1416D54A0 -&gt; 0x1416D5EF0).</summary>
    Private Function GeometryBound(nif As Nifcontent_Class_Manolo, tri As BSTriShape, wsParent As Single, isSse As Boolean) As NodeBound
        If tri.SkinInstanceRef IsNot Nothing AndAlso Not tri.SkinInstanceRef.IsEmpty() Then Return PositiveBound()
        Dim g = GeometryWorldRadius(nif, tri, wsParent, isSse)
        If g.Radius > 0.0F Then Return PositiveBound()
        Dim b = tri.Bounds
        Dim c = If(g.NaNSphere, Vector3.Zero, New Vector3(b.Center.X, b.Center.Y, b.Center.Z))
        Dim placed = EngineWorldBound.Place(DrawnWorldOf(nif, tri), New EngineWorldBound.Sphere(c, 0.0F))
        Return New NodeBound(BoundKind.Exact, New EngineWorldBound.Sphere(placed.Center, g.Radius))
    End Function

    ''' <summary>Rev-13: the node bounds of one frame, one map per NIF, shared by every shape of that NIF (one walk per NIF and frame, not
    ''' one per shape). PreviewModel.FrameSceneBounds holds it; a new frame serial clears it.</summary>
    Friend NotInheritable Class SceneBoundMemo
        Private _serial As Integer = Integer.MinValue
        Private ReadOnly _porNif As New Dictionary(Of NifFile, Dictionary(Of NiAVObject, NodeBound))(ReferenceEqualityComparer.Instance)

        Friend Function ForFrame(serial As Integer, nif As NifFile) As Dictionary(Of NiAVObject, NodeBound)
            If serial <> _serial Then
                _porNif.Clear()
                _serial = serial
            End If
            Dim m As Dictionary(Of NiAVObject, NodeBound) = Nothing
            If Not _porNif.TryGetValue(nif, m) Then
                m = New Dictionary(Of NiAVObject, NodeBound)(ReferenceEqualityComparer.Instance)
                _porNif(nif) = m
            End If
            Return m
        End Function
    End Class

End Module
