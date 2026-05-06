''' <summary>
''' Resolves and prepares the skeleton(s) for a set of shapes before geometry extraction.
''' The caller is responsible for fully preparing each <see cref="SkeletonInstance"/>
''' (load + cloth-bone injection + ApplyPose) BEFORE invoking the render. The resolver
''' only orchestrates loading/cloth-injection of the global default for legacy single-actor
''' callers and exposes the per-shape lookup the pipeline uses for skinning.
''' </summary>
Public Interface ISkeletonResolver
    ''' <summary>Called once per frame BEFORE geometry extraction. Resolvers that own the
    ''' default instance use this to load + cloth-inject; resolvers receiving a pre-prepared
    ''' instance (Single/Multi) leave this as no-op.</summary>
    Sub ResolveSkeleton(shapes As IEnumerable(Of IRenderableShape))
    ''' <summary>Returns the SkeletonInstance that this shape's skinning should use.
    ''' Default implementations return <see cref="SkeletonInstance.Default"/>.
    ''' Multi-actor resolvers return a different instance per shape (or per shape group).</summary>
    Function ResolveFor(shape As IRenderableShape) As SkeletonInstance
End Interface

''' <summary>
''' Aggregates all decisions for a render pass (legacy push API).
''' Kept for backward compatibility — internally converted to RenderIntent.
''' Pose is NOT a field here: the caller applies it directly to the SkeletonInstance(s)
''' before invoking RenderShapes(request) and signals via RenderDirtyFlags.Pose.
''' </summary>
Public Class RenderRequest
    Public Property Shapes As IEnumerable(Of IRenderableShape)
    Public Property SkeletonResolver As ISkeletonResolver = Nothing
    Public Property MorphResolver As IMorphResolver = Nothing
    Public Property GeometryModifiers As List(Of IGeometryModifier) = Nothing
    Public Property RecalculateNormals As Boolean = True
    Public Property ResetCamera As Boolean = True
    Public Property FloorOffset As Double = 0
    ''' <summary>When True, full reload preserves the GL texture cache + Textures_Dictionary +
    ''' raw bytes cache across the swap. Pending background uploads are still cancelled (the
    ''' part that's unsafe to skip when the shape set changes). Default False = legacy
    ''' behaviour (full clear on every reload). Set to True when callers know the new shape
    ''' set will mostly reuse textures from the previous one (e.g. NPC preset swap, outfit
    ''' change on the same actor). Trade-off: skips refresh of loose .dds/.bgsm files
    ''' modified on disk during the session, and lets unused textures linger in GPU memory
    ''' until the next non-preserving reload or control disposal.</summary>
    Public Property PreserveTextureCache As Boolean = False
End Class

''' <summary>
''' Default skeleton resolver that uses <see cref="SkeletonInstance.Default"/> (the global
''' single-actor instance). Loads the skeleton from a dictionary key (idempotent) and
''' performs cloth-bone injection on each ResolveSkeleton call. Pose application is the
''' CALLER's responsibility — call <c>SkeletonInstance.Default.ApplyPose(pose)</c> before
''' invoking the render. Apps that need multi-actor skeletons use
''' <see cref="SingleInstanceSkeletonResolver"/> or <see cref="MultiInstanceSkeletonResolver"/>.
''' </summary>
Public Class DefaultSkeletonResolver
    Implements ISkeletonResolver

    Private ReadOnly _skeletonKey As String

    Public Sub New(Optional skeletonKey As String = "")
        _skeletonKey = skeletonKey
    End Sub

    Public Sub ResolveSkeleton(shapes As IEnumerable(Of IRenderableShape)) Implements ISkeletonResolver.ResolveSkeleton
        If Not String.IsNullOrEmpty(_skeletonKey) Then
            SkeletonInstance.Default.LoadFromKey(_skeletonKey)
        End If
        SkeletonInstance.Default.PrepareForShapes(shapes)
    End Sub

    Public Function ResolveFor(shape As IRenderableShape) As SkeletonInstance Implements ISkeletonResolver.ResolveFor
        Return SkeletonInstance.Default
    End Function
End Class

''' <summary>
''' Resolver that returns the same caller-supplied <see cref="SkeletonInstance"/> for every shape.
''' The caller is fully responsible for the instance state (LoadFromKey + PrepareForShapes +
''' MergeAdditionalSkeleton + ApplyPose) BEFORE handing it over. <see cref="ResolveSkeleton"/>
''' is a no-op. Use this for single-actor scenes that want a distinct instance from
''' <see cref="SkeletonInstance.Default"/> (e.g. NPC previews with per-NPC bone multipliers).
''' </summary>
Public Class SingleInstanceSkeletonResolver
    Implements ISkeletonResolver

    Private ReadOnly _instance As SkeletonInstance

    Public Sub New(instance As SkeletonInstance)
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        _instance = instance
    End Sub

    Public ReadOnly Property Instance As SkeletonInstance
        Get
            Return _instance
        End Get
    End Property

    Public Sub ResolveSkeleton(shapes As IEnumerable(Of IRenderableShape)) Implements ISkeletonResolver.ResolveSkeleton
        ' No-op: instance is pre-prepared (load + merges + cloth-inject + ApplyPose) by caller.
    End Sub

    Public Function ResolveFor(shape As IRenderableShape) As SkeletonInstance Implements ISkeletonResolver.ResolveFor
        Return _instance
    End Function
End Class

''' <summary>
''' Resolver that maps each shape to its own <see cref="SkeletonInstance"/>. Use this when
''' rendering shapes that need DIFFERENT skeletons and/or poses in the same scene — e.g. two
''' NPCs with distinct race bone-multipliers, body+robot extension+face merges, or any
''' multi-actor composition. The caller fully prepares each instance (load + merges +
''' ApplyPose) BEFORE handing the map over.
''' </summary>
Public Class MultiInstanceSkeletonResolver
    Implements ISkeletonResolver

    Private ReadOnly _map As IReadOnlyDictionary(Of IRenderableShape, SkeletonInstance)
    Private ReadOnly _fallback As SkeletonInstance

    ''' <param name="map">Shape → SkeletonInstance lookup. Shapes not in the map fall back to
    ''' <paramref name="fallback"/> (or <see cref="SkeletonInstance.Default"/> if Nothing).</param>
    ''' <param name="fallback">Optional default instance for shapes not present in <paramref name="map"/>.</param>
    Public Sub New(map As IReadOnlyDictionary(Of IRenderableShape, SkeletonInstance), Optional fallback As SkeletonInstance = Nothing)
        If map Is Nothing Then Throw New ArgumentNullException(NameOf(map))
        _map = map
        _fallback = fallback
    End Sub

    Public Sub ResolveSkeleton(shapes As IEnumerable(Of IRenderableShape)) Implements ISkeletonResolver.ResolveSkeleton
        ' No-op: each instance is pre-prepared by the caller.
    End Sub

    Public Function ResolveFor(shape As IRenderableShape) As SkeletonInstance Implements ISkeletonResolver.ResolveFor
        Dim inst As SkeletonInstance = Nothing
        If _map.TryGetValue(shape, inst) AndAlso inst IsNot Nothing Then Return inst
        Return If(_fallback, SkeletonInstance.Default)
    End Function
End Class

' ──────────────────────────────────────────────────────────────────────
'  Pull-based pipeline types
' ──────────────────────────────────────────────────────────────────────

''' <summary>
''' Flags indicating what changed since the last pipeline execution.
''' Apps set these via RenderIntent.MarkDirty(); the pipeline reads them to decide work.
''' Multiple flags combine naturally: Pose Or Morphs = pose changed AND morphs need reapply.
''' </summary>
<Flags>
Public Enum RenderDirtyFlags
    None = 0
    ''' <summary>Geometry source changed — full reload (clean, skeleton, extract, upload).</summary>
    Shapes = 1
    ''' <summary>Pose changed — recompute bone matrices, re-prepare skeleton.</summary>
    Pose = 2
    ''' <summary>Morph weights changed — reapply morph plan per mesh.</summary>
    Morphs = 4
    ''' <summary>Textures need reprocessing (material change, texture swap).</summary>
    Textures = 8
    ''' <summary>Camera needs reset (new shapes, pose change).</summary>
    Camera = 16
    ''' <summary>Force full reload regardless of state diffing.</summary>
    Force = 32
End Enum

''' <summary>
''' Declarative render intent — the pull-based pipeline's input.
''' Apps set WHAT changed (dirty flags) and provide resolvers for HOW to handle it.
''' One instance per PreviewControl. Mutate, then call ctrl.InvalidateRender().
''' The timer-driven pipeline consumes it on the next tick.
'''
''' Pose state lives in the per-shape <see cref="SkeletonInstance"/> (via
''' <see cref="SkeletonInstance.ApplyPose"/>) — not in this intent. Callers signal pose
''' changes via <see cref="MarkDirty"/>(<see cref="RenderDirtyFlags.Pose"/>) optionally
''' restricted to specific shapes via <see cref="DirtyShapes"/>.
''' </summary>
Public Class RenderIntent
    ' ── Input state (set by apps) ──
    Public Property Shapes As IEnumerable(Of IRenderableShape)
    Public Property FloorOffset As Double = 0
    Public Property ResetCamera As Boolean = True
    Public Property RecalculateNormals As Boolean = True

    ' ── Pluggable resolvers (Nothing = skip that step) ──
    Public Property SkeletonResolver As ISkeletonResolver
    Public Property MorphResolver As IMorphResolver
    Public Property GeometryModifiers As List(Of IGeometryModifier)

    ' ── Optional callback for async texture prefetch before geometry load ──
    Public Property TexturePrefetchAction As Action

    ''' <summary>One-shot callback invoked exactly once after the next False→True transition of
    ''' <see cref="PreviewModel.TexturesReady"/> caused by background uploads completing.
    ''' Receives the <see cref="PreviewModel"/> so the callback can read / mutate
    ''' <c>Textures_Dictionary</c> entries that just got their GL TexIDs assigned.
    ''' <para>Use case: post-texture-bake passes (face tint compositor, skin softlight, body
    ''' morph diffuse bake) that mutate already-uploaded GL textures. Symmetric counterpart of
    ''' <see cref="TexturePrefetchAction"/> (which fires BEFORE texture loading starts).</para>
    ''' <para>Mutation contract: the callback MAY replace <c>Textures_Dictionary[path].Texture_ID</c>
    ''' (delete old, gen new). The pipeline calls <see cref="PreviewModel.MarkRenderBucketsDirty"/>
    ''' immediately after the callback returns, so any mesh sort by Texture_ID is rebuilt next
    ''' frame.</para>
    ''' <para>Lifecycle: NOT cleared by <see cref="ClearDirty"/> — survives across the pipeline
    ''' execution that triggers texture loading, until either the False→True transition fires it
    ''' or <see cref="PostTextureUploadTimeoutMs"/> elapses, whichever comes first. After firing
    ''' (or timing out) the property is reset to Nothing automatically; the caller registers a
    ''' fresh action per render that needs post-texture work.</para>
    ''' <para>Edge: if textures are already ready when the pipeline finishes (no background load
    ''' needed because cache reuse / PreserveTextureCache), the hook fires immediately at the end
    ''' of the pipeline — same observable behaviour as a zero-delay False→True transition.</para></summary>
    Public Property PostTextureUploadAction As Action(Of PreviewModel)

    ''' <summary>Watchdog timeout (ms) for <see cref="PostTextureUploadAction"/>. If the
    ''' False→True transition has not happened by this deadline (BA2 corrupt, FilesDictionary
    ''' miss, cancelled background load that left a path orphaned), the pipeline invokes
    ''' <see cref="PostTextureUploadTimeoutAction"/> instead of the success action and clears
    ''' both. 0 disables the watchdog (action waits forever).</summary>
    Public Property PostTextureUploadTimeoutMs As Integer = 7200

    ''' <summary>Fallback callback invoked instead of <see cref="PostTextureUploadAction"/> when
    ''' the watchdog deadline elapses without textures becoming ready. Typical use: reveal hidden
    ''' shapes so the user sees an untinted preview rather than a permanently-blank canvas.
    ''' Like the success action, this is one-shot and reset to Nothing after firing.</summary>
    Public Property PostTextureUploadTimeoutAction As Action(Of PreviewModel)

    ''' <summary>When True, full reload preserves the GL texture cache + Textures_Dictionary +
    ''' raw bytes cache across the swap. See <see cref="RenderRequest.PreserveTextureCache"/>
    ''' for trade-off details. Reset to False by <see cref="ClearDirty"/> — it's per-render
    ''' state, not an accumulating mode flag.</summary>
    Public Property PreserveTextureCache As Boolean = False

    ' ── Dirty flags + per-shape granularity ──
    Private _dirty As RenderDirtyFlags = RenderDirtyFlags.None

    Public ReadOnly Property DirtyFlags As RenderDirtyFlags
        Get
            Return _dirty
        End Get
    End Property

    ''' <summary>Optional subset of shapes affected by the current Pose/Morphs/Textures dirty
    ''' flags. Empty means "all shapes" (default — backward compatible). Populate to limit
    ''' recompute to specific shapes (typical multi-actor case where only one actor changed
    ''' pose). Cleared by <see cref="ClearDirty"/>.</summary>
    Public ReadOnly Property DirtyShapes As New HashSet(Of IRenderableShape)

    ''' <summary>True iff <paramref name="shape"/> should be processed for the current dirty
    ''' flags: empty <see cref="DirtyShapes"/> means all shapes pass; otherwise only shapes
    ''' present in the set pass.</summary>
    Public Function IsShapeDirty(shape As IRenderableShape) As Boolean
        If DirtyShapes.Count = 0 Then Return True
        Return DirtyShapes.Contains(shape)
    End Function

    ''' <summary>Accumulate dirty flags (OR semantics — never clears existing flags).</summary>
    Public Sub MarkDirty(flags As RenderDirtyFlags)
        _dirty = _dirty Or flags
    End Sub

    ''' <summary>Mark dirty flags AND restrict the affected shapes. Useful for multi-actor
    ''' setups where only a subset of shapes changed pose/morphs. Subsequent calls accumulate
    ''' both flags (OR) and shapes (UnionWith).</summary>
    Public Sub MarkDirty(flags As RenderDirtyFlags, ParamArray shapes As IRenderableShape())
        _dirty = _dirty Or flags
        If shapes IsNot Nothing Then DirtyShapes.UnionWith(shapes)
    End Sub

    ''' <summary>Mark dirty flags AND restrict the affected shapes (collection overload).</summary>
    Public Sub MarkDirty(flags As RenderDirtyFlags, shapes As IEnumerable(Of IRenderableShape))
        _dirty = _dirty Or flags
        If shapes IsNot Nothing Then DirtyShapes.UnionWith(shapes)
    End Sub

    ''' <summary>Clear all dirty flags + shape subset after pipeline execution. Also resets
    ''' <see cref="PreserveTextureCache"/> back to False so the next render starts from the
    ''' safe default — opt-in must be explicit per-render.
    ''' <para>Does NOT touch <see cref="PostTextureUploadAction"/>,
    ''' <see cref="PostTextureUploadTimeoutAction"/> or <see cref="PostTextureUploadTimeoutMs"/>:
    ''' those have their own one-shot lifecycle managed by the post-texture-upload watchdog
    ''' inside the render loop, because the False→True transition that fires them happens
    ''' asynchronously after this method has already returned (background texture load completes
    ''' over several frames).</para></summary>
    Public Sub ClearDirty()
        _dirty = RenderDirtyFlags.None
        DirtyShapes.Clear()
        PreserveTextureCache = False
    End Sub

    ''' <summary>True if any dirty flag is set — the pipeline has work to do.</summary>
    Public ReadOnly Property HasWork As Boolean
        Get
            Return _dirty <> RenderDirtyFlags.None
        End Get
    End Property
End Class
