''' <summary>
''' Resolves and prepares the skeleton for a set of shapes before geometry extraction.
''' Default implementation uses Skeleton_Class. Apps can override for custom behavior.
''' </summary>
Public Interface ISkeletonResolver
    Sub ResolveSkeleton(shapes As IEnumerable(Of IRenderableShape), pose As Poses_class)
End Interface

''' <summary>
''' Aggregates all decisions for a render pass (legacy push API).
''' Kept for backward compatibility — internally converted to RenderIntent.
''' </summary>
Public Class RenderRequest
    Public Property Shapes As IEnumerable(Of IRenderableShape)
    Public Property Pose As Poses_class = Nothing
    Public Property SkeletonResolver As ISkeletonResolver = Nothing
    Public Property MorphResolver As IMorphResolver = Nothing
    Public Property GeometryModifiers As List(Of IGeometryModifier) = Nothing
    Public Property RecalculateNormals As Boolean = True
    Public Property ResetCamera As Boolean = True
    Public Property FloorOffset As Double = 0
End Class

''' <summary>
''' Default skeleton resolver that uses the global Skeleton_Class.
''' Loads skeleton from a dictionary key and prepares it for shapes with cloth bone injection.
''' </summary>
Public Class DefaultSkeletonResolver
    Implements ISkeletonResolver

    Private ReadOnly _skeletonKey As String

    Public Sub New(Optional skeletonKey As String = "")
        _skeletonKey = skeletonKey
    End Sub

    Public Sub ResolveSkeleton(shapes As IEnumerable(Of IRenderableShape), pose As Poses_class) Implements ISkeletonResolver.ResolveSkeleton
        If Not String.IsNullOrEmpty(_skeletonKey) Then
            Skeleton_Class.LoadSkeletonFromKey(_skeletonKey)
        End If
        Skeleton_Class.PrepareSkeletonForShapes(shapes, pose)
    End Sub
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
''' </summary>
Public Class RenderIntent
    ' ── Input state (set by apps) ──
    Public Property Shapes As IEnumerable(Of IRenderableShape)
    Public Property Pose As Poses_class
    Public Property FloorOffset As Double = 0
    Public Property ResetCamera As Boolean = True
    Public Property RecalculateNormals As Boolean = True

    ' ── Pluggable resolvers (Nothing = skip that step) ──
    Public Property SkeletonResolver As ISkeletonResolver
    Public Property MorphResolver As IMorphResolver
    Public Property GeometryModifiers As List(Of IGeometryModifier)

    ' ── Optional callback for async texture prefetch before geometry load ──
    Public Property TexturePrefetchAction As Action

    ' ── Dirty flags ──
    Private _dirty As RenderDirtyFlags = RenderDirtyFlags.None

    Public ReadOnly Property DirtyFlags As RenderDirtyFlags
        Get
            Return _dirty
        End Get
    End Property

    ''' <summary>Accumulate dirty flags (OR semantics — never clears existing flags).</summary>
    Public Sub MarkDirty(flags As RenderDirtyFlags)
        _dirty = _dirty Or flags
    End Sub

    ''' <summary>Clear all dirty flags after pipeline execution.</summary>
    Public Sub ClearDirty()
        _dirty = RenderDirtyFlags.None
    End Sub

    ''' <summary>True if any dirty flag is set — the pipeline has work to do.</summary>
    Public ReadOnly Property HasWork As Boolean
        Get
            Return _dirty <> RenderDirtyFlags.None
        End Get
    End Property
End Class
