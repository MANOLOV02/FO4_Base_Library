Imports OpenTK.Mathematics

''' <summary>
''' A single morph channel: a named set of vertex deltas with a weight.
''' Produced by IMorphResolver, consumed by MorphEngine.
''' </summary>
Public Class MorphChannel
    Public Property Name As String
    Public Property Weight As Single = 0
    Public Property Deltas As List(Of MorphData)
    Public Property IsZap As Boolean = False

    Sub New(name As String, weight As Single, deltas As List(Of MorphData), Optional isZap As Boolean = False)
        Me.Name = name
        Me.Weight = weight
        Me.Deltas = deltas
        Me.IsZap = isZap
    End Sub
End Class

''' <summary>
''' A complete morph plan for one shape: all active channels with their weights and deltas.
''' The engine doesn't know WHERE these came from (sliders, face morphs, expressions, etc.)
''' </summary>
Public Class MorphPlan
    Public Property Channels As New List(Of MorphChannel)
    Public ReadOnly Property HasMorphs As Boolean
        Get
            Return Channels.Count > 0
        End Get
    End Property
    Public ReadOnly Property HasZaps As Boolean
        Get
            Return Channels.Any(Function(c) c.IsZap)
        End Get
    End Property
End Class

''' <summary>
''' Resolves morph data for a shape. Consumers implement this to produce morph plans
''' from their specific data sources (WM sliders, NPC face morphs, expressions, etc.)
''' </summary>
Public Interface IMorphResolver
    ''' <summary>
    ''' Build a morph plan for the given shape. Called once per shape per render update.
    ''' Return an empty MorphPlan (no channels) if no morphs apply.
    ''' </summary>
    Function ResolveMorphPlan(shape As IRenderableShape, geom As SkinnedGeometry) As MorphPlan
End Interface

''' <summary>
''' A geometry modifier that transforms geometry after morphs are applied.
''' Examples: vertex masking, topology compaction (zap removal), etc.
''' </summary>
Public Interface IGeometryModifier
    ''' <summary>Apply this modifier to the geometry. Called in pipeline order after morphs.</summary>
    Sub Apply(shape As IRenderableShape, ByRef geom As SkinnedGeometry)
End Interface

''' <summary>
''' Generic morph engine that applies a MorphPlan to geometry.
''' Does NOT know about sliders, presets, BodySlide, face morphs, or any consumer-specific concepts.
''' Works purely with vertex deltas in NIF local space.
''' </summary>
Public Class MorphEngine

    ''' <summary>
    ''' Apply all channels in the plan to the geometry.
    ''' Deltas are applied in NIF local space (pre-skinning).
    '''
    ''' Contract for null/empty plans: if <paramref name="plan"/> is Nothing or has no
    ''' channels, the method performs a RESET — geom.Vertices is rewritten from
    ''' NifLocalVertices (raw, pre-skin), mask/dirty state is cleared, and TBN is
    ''' recalculated for any vertex that changed. This lets callers toggle morphs OFF
    ''' by passing a null plan (or a resolver that returns null) instead of keeping
    ''' stale deltas pegged on the mesh.
    ''' </summary>
    Public Shared Sub ApplyMorphPlan(ByRef geom As SkinnedGeometry, plan As MorphPlan,
                                     recalculateNormals As Boolean,
                                     Optional allowMask As Boolean = False,
                                     Optional maskedVertices As HashSet(Of Integer) = Nothing)
        Dim count = geom.NifLocalVertices.Length
        If count = 0 Then Return

        ' Start from NIF local space (pre-skinning)
        Dim verts = geom.NifLocalVertices.ToArray()

        ' Apply mask if provided
        If allowMask AndAlso maskedVertices IsNot Nothing Then
            For i = 0 To count - 1
                If maskedVertices.Contains(i) Then
                    geom.VertexMask(i) = 1
                    geom.dirtyMaskIndices.Add(i)
                    geom.dirtyMaskFlags(i) = True
                Else
                    If geom.VertexMask(i) = 1 Then
                        geom.VertexMask(i) = 0
                        geom.dirtyMaskIndices.Add(i)
                        geom.dirtyMaskFlags(i) = True
                    End If
                End If
            Next
        Else
            Array.Clear(geom.VertexMask, 0, count)
            geom.dirtyMaskIndices.Clear()
            For i = 0 To count - 1
                geom.dirtyMaskFlags(i) = False
            Next
        End If

        geom.dirtyVertexIndices.Clear()

        ' Apply each channel (skipped entirely for null/empty plan -> reset semantics)
        If plan IsNot Nothing AndAlso plan.HasMorphs Then
            For Each channel In plan.Channels
                Dim t = channel.Weight
                If Single.IsNaN(t) Then t = 0
                If channel.Deltas Is Nothing Then Continue For

                If channel.IsZap Then
                    ' Zap: mark vertices for removal
                    For Each morph In channel.Deltas
                        Dim i = CInt(morph.index)
                        If i >= 0 AndAlso i < count Then
                            geom.VertexMask(i) = -t
                            geom.dirtyMaskIndices.Add(i)
                            geom.dirtyMaskFlags(i) = True
                        End If
                    Next
                Else
                    ' Position morph: move vertices in NIF local space
                    For Each morph In channel.Deltas
                        Dim i = CInt(morph.index)
                        If i >= 0 AndAlso i < count Then
                            Dim delta = morph.PosDiff * t
                            If delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z < 0.000001F Then Continue For
                            verts(i) = verts(i) + delta
                        End If
                    Next
                End If
            Next
        End If

        ' Track dirty vertices
        For i = 0 To count - 1
            If geom.Vertices(i) <> verts(i) Then
                geom.dirtyVertexIndices.Add(i)
                geom.dirtyVertexFlags(i) = True
            Else
                geom.dirtyVertexFlags(i) = False
            End If
        Next

        ' Optimize: if >60% dirty, mark all dirty
        If geom.dirtyVertexIndices.Count > count * 0.6 Then
            geom.dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, count))
            For i = 0 To count - 1
                geom.dirtyVertexFlags(i) = True
            Next
        End If

        geom.Vertices = verts

        ' Invalidate caches
        geom.WorldCacheValid = False
        geom.CachedWorldVertices = Nothing
        geom.CachedWorldNormals = Nothing

        ' Recalculate normals/TBN if needed
        If recalculateNormals AndAlso geom.dirtyVertexIndices.Count > 0 Then
            Dim opt As RecalcTBN.TBNOptions = Config_App.Current.Setting_TBN
            Dim adicionales = RecalcTBN.RecalculateNormalsTangentsBitangents(geom, opt)
            adicionales.ExceptWith(geom.dirtyVertexIndices)
            For Each ad In adicionales
                geom.dirtyVertexIndices.Add(ad)
                geom.dirtyVertexFlags(ad) = True
            Next
        End If
    End Sub
End Class
