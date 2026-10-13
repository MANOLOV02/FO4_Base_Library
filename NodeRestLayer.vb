Imports System.Runtime.CompilerServices
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>THE DRAWING LAYER OF THE NODE TRANSFORMS AT REST (ola 0c parte 2, the connection; user decision 12-oct-2026, option 1 of
''' parte2 section 5): the preview draws a node with the local transform the game holds after its NiTransformControllers' first update
''' (TransformRestLaw.TransformAtRest, its time from ControllerRestLaw.ControllerTimeAtRest) instead of the one stored in the NIF. A
''' DRAWING layer: nothing here writes a block - the NIF the editors show, edit (gizmos: Wardrobe_Manager EspacioDeShape) and save keeps
''' its own transforms; the bakes and builds read the file (Transform_Class.GetGlobalTransform, SkinnedGeometry.BoneMatsBind /
''' BoneMatsPose). The render reads it through its world-transform seats - SkinningHelper.ShapeGlobalTransform, SkinningHelper.NifOwnWorld / NifOwnDrawnWorld
''' (a NIF-own bone and the skin root), EngineWorldBound and NifSceneVisibility - so the drawn vertices, the world cache (picking, mask
''' brush, scene bounds, occlusion) and the culling bounds read the same position.
''' <para>The controllers of a node: its own chain (NifContent.CadenaDeControllers on Controller), in chain order - the order of the
''' engine's UpdateControllers walk, the same walk NifSceneVisibility.VisibleAtRest reads its NiVisControllers by; each
''' NiTransformController writes its valid components over the ones the earlier wrote (FO4 0x1417455BA..759, SSE 0x140F6987C..A10: the
''' translation, the rotation, |s|).</para>
''' <para>When it is evaluated: with the mesh - SkinningHelper.ExtractSkinnedGeometry and RecomputeGPUBoneMatrices of the dirty meshes
''' (and the bounds that read their result) - not on every frame; the culling bounds (NifSceneVisibility, once per NIF and frame through
''' PreviewModel.FrameSceneBounds) read it too. The node's own NIF local is read at that moment (an edit to it shows on the mesh's next
''' recompute). The stamp spares the law: what a controller writes is memoized per controller with every input TransformAtRest reads
''' (RestMemo: the controller's flags / frequency / phase / start / stop, its interpolator and pose, the data block - the key lists
''' themselves have no writer in the app, KeyFillLaw), and recomputed only when one of them changed.</para></summary>
Friend Module NodeRestLayer

    ''' <summary>What one NiTransformController writes at rest and the inputs it was computed from (the stamp).</summary>
    Private NotInheritable Class RestMemo
        Friend Flags As UShort
        Friend Frequency As Single
        Friend Phase As Single
        Friend StartTime As Single
        Friend StopTime As Single
        Friend Interp As NiInterpolator
        Friend PoseT As System.Numerics.Vector3
        Friend PoseR As System.Numerics.Quaternion
        Friend PoseS As Single
        Friend Data As NiTransformData
        Friend Rest As TransformRestLaw.TransformRest?
        Friend Hole As String

        Friend Function Matches(c As NiTransformController, interp As NiInterpolator, data As NiTransformData) As Boolean
            If c.Flags.Value <> Flags OrElse Not Same(c.Frequency, Frequency) OrElse Not Same(c.Phase, Phase) OrElse
               Not Same(c.StartTime, StartTime) OrElse Not Same(c.StopTime, StopTime) Then Return False
            If interp IsNot Me.Interp OrElse data IsNot Me.Data Then Return False
            Dim ti = TryCast(interp, NiTransformInterpolator)
            If ti Is Nothing Then Return True
            Dim p = ti.Transform
            Return Same(p.Translation.X, PoseT.X) AndAlso Same(p.Translation.Y, PoseT.Y) AndAlso Same(p.Translation.Z, PoseT.Z) AndAlso
                   Same(p.Rotation.X, PoseR.X) AndAlso Same(p.Rotation.Y, PoseR.Y) AndAlso Same(p.Rotation.Z, PoseR.Z) AndAlso
                   Same(p.Rotation.W, PoseR.W) AndAlso Same(p.Scale, PoseS)
        End Function

        ''' <summary>Bit equality (a NaN input equals itself: a stamp, not arithmetic).</summary>
        Private Shared Function Same(a As Single, b As Single) As Boolean
            Return BitConverter.SingleToInt32Bits(a) = BitConverter.SingleToInt32Bits(b)
        End Function
    End Class

    Private ReadOnly Memo As New ConditionalWeakTable(Of NiTransformController, RestMemo)()

    ''' <summary>What <paramref name="c"/> writes at rest (TransformRestLaw.TransformAtRest), from the memo while its inputs are the same.</summary>
    Private Function ControllerRest(nif As Nifcontent_Class_Manolo, c As NiTransformController, ByRef hole As String) As TransformRestLaw.TransformRest?
        Dim interp = nif.GetBlock(c.Interpolator)
        Dim ti = TryCast(interp, NiTransformInterpolator)
        Dim data = If(ti Is Nothing, Nothing, nif.GetBlock(ti.Data))
        Dim m As RestMemo = Nothing
        If Memo.TryGetValue(c, m) AndAlso m.Matches(c, interp, data) Then
            hole = m.Hole
            Return m.Rest
        End If
        Dim h As String = Nothing
        Dim r = TransformRestLaw.TransformAtRest(c, interp, data, h)
        m = New RestMemo With {.Flags = c.Flags.Value, .Frequency = c.Frequency, .Phase = c.Phase, .StartTime = c.StartTime,
                               .StopTime = c.StopTime, .Interp = interp, .Data = data, .Rest = r, .Hole = h}
        If ti IsNot Nothing Then
            m.PoseT = ti.Transform.Translation
            m.PoseR = ti.Transform.Rotation
            m.PoseS = ti.Transform.Scale
        End If
        Memo.AddOrUpdate(c, m)
        hole = h
        Return r
    End Function

    ''' <summary>The local transform <paramref name="o"/> is drawn with: its NIF local (Transform_Class of the block) with each component
    ''' a NiTransformController of its chain writes at rest. <paramref name="driven"/> = some controller of the chain writes a component;
    ''' <paramref name="hole"/> gets the reason a controller of the chain is not determinable (its components are not written).
    ''' Nothing when the block is not a NiNode or a shape.</summary>
    Friend Function DrawnLocal(o As NiAVObject, nif As Nifcontent_Class_Manolo, ByRef driven As Boolean, ByRef hole As String) As Transform_Class
        Dim local As Transform_Class
        Dim sh = TryCast(o, INiShape)
        If sh IsNot Nothing Then
            local = New Transform_Class(sh)
        Else
            Dim n = TryCast(o, NiNode)
            If n Is Nothing Then Return Nothing
            local = New Transform_Class(n)
        End If
        If o.Controller Is Nothing OrElse o.Controller.IsEmpty() Then Return local
        For Each c In nif.CadenaDeControllers(o.Controller)
            Dim tc = TryCast(c, NiTransformController)
            If tc Is Nothing Then Continue For
            Dim h As String = Nothing
            Dim r = ControllerRest(nif, tc, h)
            If h IsNot Nothing Then hole = $"{If(o.Name?.String, "")}: {h}"
            If Not r.HasValue Then Continue For
            Dim v = r.Value
            If v.Translation.HasValue Then local.Translation = v.Translation.Value : driven = True
            If v.Rotation.HasValue Then local.Rotation = v.Rotation.Value : driven = True
            If v.Scale.HasValue Then local.Scale = v.Scale.Value : driven = True
        Next
        Return local
    End Function

    ''' <summary>What the layer does to the chain of a block (the block and its parents): the names of the blocks some controller
    ''' writes, in chain order from the block up, and the holes. Empty without controllers.</summary>
    Friend NotInheritable Class ChainReport
        Friend ReadOnly Driven As New List(Of String)
        Friend ReadOnly Holes As New List(Of String)
        ''' <summary>Blocks the layer would move on a chain the shape draws as the file (SkinningHelper.NifOwnDrawnWorld: a skinned shape
        ''' mounted on the skeleton).</summary>
        Friend ReadOnly Excluded As New List(Of String)
    End Class

    ''' <summary>What the layer did to one shape, for the frame's notice (RenderableMesh.ReportUndrawn): the driven blocks drawn at rest,
    ''' the excluded ones, the holes - each the distinct names joined in first-seen order, Nothing when none.</summary>
    Friend NotInheritable Class ShapeLayer
        Friend Driven As String
        Friend Excluded As String
        Friend Hole As String
    End Class

    ''' <summary>The shape's ShapeLayer from the report its drawing collected; Nothing when the layer did nothing to it.</summary>
    Friend Function Summary(rep As ChainReport) As ShapeLayer
        If rep Is Nothing Then Return Nothing
        Dim join = Function(l As List(Of String)) If(l.Count = 0, Nothing, String.Join(", ", l.Distinct(StringComparer.Ordinal)))
        Dim r As New ShapeLayer With {.Driven = join(rep.Driven), .Excluded = join(rep.Excluded), .Hole = join(rep.Holes)}
        If r.Driven Is Nothing AndAlso r.Excluded Is Nothing AndAlso r.Hole Is Nothing Then Return Nothing
        Return r
    End Function

    ''' <summary>The world transform <paramref name="o"/> is drawn with, or Nothing when no controller of its chain (the block and its
    ''' parents) writes a component - then it is the file's (Transform_Class.GetGlobalTransform). DrawnLocal of the block x the DrawnLocal
    ''' of each parent, composed as Transform_Class.GetGlobalTransform composes the file's (parent.ComposeTransforms(child), up to the
    ''' root). <paramref name="report"/> (optional) collects the driven blocks and the holes.</summary>
    Friend Function DrawnGlobalOrNothing(o As NiAVObject, nif As Nifcontent_Class_Manolo, Optional report As ChainReport = Nothing) As Transform_Class
        If Not ChainHasController(o, nif) Then Return Nothing
        Dim glob As Transform_Class = Nothing
        Dim anyDriven = False
        Dim cur As NiAVObject = o
        While cur IsNot Nothing
            Dim driven = False
            Dim hole As String = Nothing
            Dim local = DrawnLocal(cur, nif, driven, hole)
            If local Is Nothing Then Exit While
            If driven Then
                anyDriven = True
                report?.Driven.Add(If(cur.Name?.String, ""))
            End If
            If hole IsNot Nothing Then report?.Holes.Add(hole)
            glob = If(glob Is Nothing, local, local.ComposeTransforms(glob))
            cur = nif.GetParentNode(cur)
        End While
        Return If(anyDriven, glob, Nothing)
    End Function

    ''' <summary>Does a block of <paramref name="o"/>'s chain (the block and its parents) carry a controller? The cheap test before any
    ''' evaluation: no allocation, one walk up the parents.</summary>
    Friend Function ChainHasController(o As NiAVObject, nif As Nifcontent_Class_Manolo) As Boolean
        If o Is Nothing OrElse nif Is Nothing Then Return False
        Dim cur As NiAVObject = o
        While cur IsNot Nothing
            If cur.Controller IsNot Nothing AndAlso Not cur.Controller.IsEmpty() Then Return True
            cur = nif.GetParentNode(cur)
        End While
        Return False
    End Function

    ''' <summary>The blocks the layer would move on <paramref name="o"/>'s chain, added to <paramref name="report"/>.Excluded: a chain the
    ''' shape does not draw with the layer (a skinned shape mounted on the skeleton: its NIF-own bones and skin root drawn as the file,
    ''' its resolved bones drawn by the skeleton).</summary>
    Friend Sub NoteExcluded(o As NiAVObject, nif As Nifcontent_Class_Manolo, report As ChainReport)
        If report Is Nothing OrElse Not ChainHasController(o, nif) Then Return
        Dim probe As New ChainReport()
        If DrawnGlobalOrNothing(o, nif, probe) IsNot Nothing Then report.Excluded.AddRange(probe.Driven)
    End Sub

    ''' <summary>DrawnGlobalOrNothing, or the file's world when it gives Nothing (the file's bits).</summary>
    Friend Function DrawnGlobal(o As NiAVObject, nif As Nifcontent_Class_Manolo, Optional report As ChainReport = Nothing) As Transform_Class
        If o Is Nothing OrElse nif Is Nothing Then Return New Transform_Class()
        Return If(DrawnGlobalOrNothing(o, nif, report), FileGlobal(o, nif))
    End Function

    ''' <summary>The file's world transform of <paramref name="o"/> (Transform_Class.GetGlobalTransform, by the block's kind).</summary>
    Private Function FileGlobal(o As NiAVObject, nif As Nifcontent_Class_Manolo) As Transform_Class
        Dim sh = TryCast(o, INiShape)
        If sh IsNot Nothing Then Return Transform_Class.GetGlobalTransform(sh, nif)
        Dim n = TryCast(o, NiNode)
        If n IsNot Nothing Then Return Transform_Class.GetGlobalTransform(n, nif)
        Return New Transform_Class()
    End Function

    ''' <summary>The scale <paramref name="o"/> is drawn with (DrawnLocal's), for the world-scale products of NifSceneVisibility.</summary>
    Friend Function DrawnScale(o As NiAVObject, nif As Nifcontent_Class_Manolo) As Single
        If o Is Nothing Then Return 1.0F
        If o.Controller Is Nothing OrElse o.Controller.IsEmpty() Then Return o.Scale
        Dim driven = False
        Dim hole As String = Nothing
        Dim l = DrawnLocal(o, nif, driven, hole)
        Return If(l Is Nothing, o.Scale, l.Scale)
    End Function

End Module
