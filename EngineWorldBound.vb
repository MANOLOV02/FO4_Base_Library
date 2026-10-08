Imports OpenTK.Mathematics
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>The CENTRE of a shape's world bound as each game computes it (D-L4, chunk C8): the point the game sorts the alpha list by
''' (SSE merge sort 0x141555540, FO4 0x14221DA70) and measures the refraction LOD fade from (0x1414E7120 / 0x14217EC00).
''' Unskinned: the NIF bounding sphere placed by the shape's world (SSE 0x140EFC810 -&gt; 0x140EFCF20, FO4 0x1416D54A0 -&gt; 0x1416D5EF0).
''' Skinned: the union, bone by bone in order, of each bone's sphere placed by the bone's world; then the skin root's inverse, the
''' NiSkinData overall transform (SSE only) and the shape's world (SSE 0x140F09CB0, FO4 0x1416D7D50). Union: SSE 0x140EFECA0, FO4
''' inline in 0x1416D7D50. Measured over both corpora by sort_anchor2.py and re2/anim (row O-PT).
''' <para>Bone and root worlds: the posed globals the app skins with (skeleton bone by name, else the NIF node), the same rule
''' sort_anchor2.py used. Placements in the app's Double (they are the app's pose); the union in float32 as the engine.</para></summary>
Friend Module EngineWorldBound

    Friend Structure Sphere
        Public Center As Vector3
        Public Radius As Single
        Public Sub New(c As Vector3, r As Single)
            Center = c : Radius = r
        End Sub
    End Structure

    ''' <summary>[0x1420CF074] (.data, 1e-6 in the image) and [0x141B5BE14] = 0.5: SSE's union guard and half.</summary>
    Private ReadOnly SseUnionGuard As Single = BitConverter.Int32BitsToSingle(&H358637BD)

    ''' <summary>SSE 0x140EFECA0: A grows to contain B (dump of 7-oct-2026).</summary>
    Friend Function UnionSse(a As Sphere, b As Sphere) As Sphere
        Dim d = a.Center - b.Center
        Dim d2 As Single = d.X * d.X + d.Y * d.Y + d.Z * d.Z
        Dim dr As Single = b.Radius - a.Radius
        Dim dr2 As Single = dr * dr
        If dr >= 0.0F Then
            If dr2 >= d2 Then Return b                                   ' 0x140EFED1C..D42
        ElseIf dr2 >= d2 Then
            Return a                                                     ' 0x140EFED44..D47
        End If
        Dim l As Single = MathF.Sqrt(d2)                                 ' sqrtss 0x140EFED54
        Dim c = a.Center
        If l > SseUnionGuard Then                                        ' 0x140EFED58..D5F
            Dim k As Single = (l - dr) * (0.5F / l)                      ' 0x140EFED61..D6F
            c = New Vector3(k * d.X + b.Center.X, k * d.Y + b.Center.Y, k * d.Z + b.Center.Z)
        End If
        Return New Sphere(c, (b.Radius + l + a.Radius) * 0.5F)           ' 0x140EFEDA9..DB5
    End Function

    ''' <summary>FO4 inline union in 0x1416D7D50: containment L &lt;= |rA - rB|, no guard (transcribed in sort_anchor2.merge_fo4).</summary>
    Friend Function UnionFo4(a As Sphere, b As Sphere) As Sphere
        Dim diff = a.Center - b.Center
        Dim l As Single = MathF.Sqrt(diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z)
        Dim dr As Single = a.Radius - b.Radius
        If l <= MathF.Abs(dr) Then Return If(a.Radius <= b.Radius, b, a)
        Dim t As Single = (l + dr) * 0.5F / l
        Return New Sphere(b.Center + t * diff, (l + b.Radius + a.Radius) * 0.5F)
    End Function

    ''' <summary>0x140EFCF20: c' = t + s R c, r' = s r - with the app's matrix (the placement it draws the vertices with) and the scalar
    ''' scale the engine's NiTransform has (Transform_Class.EscalaComoEscalar; a non-uniform scale exists only in the app's pose).</summary>
    Friend Function Place(t As Transform_Class, s As Sphere) As Sphere
        Dim m = t.ToMatrix4d()
        Dim c = Vector3d.TransformPosition(New Vector3d(s.Center.X, s.Center.Y, s.Center.Z), m)
        Dim exacto As Boolean
        Return New Sphere(New Vector3(CSng(c.X), CSng(c.Y), CSng(c.Z)), s.Radius * t.EscalaComoEscalar(exacto))
    End Function

    ''' <summary>The world-bound centre of <paramref name="shape"/> with the bone / root worlds <paramref name="geo"/> holds this frame
    ''' (SkinnedGeometry.BoneWorldPose / SkinRootPose, written where the app skins). Unskinned or a synthetic anchor: the NIF sphere
    ''' placed by palette 0 (rev-04). Nothing when the shape has no sphere the game would use or no geometry / palette yet, and when the
    ''' bone-data list does not have the palette's bone count (declared gap, D-L4): the caller keeps the vertex box centre.</summary>
    Friend Function Centre(shape As IRenderableShape, geo As SkinnedGeometry) As Vector3?
        Dim nif = shape?.NifContent
        Dim ns = shape?.NifShape
        If nif Is Nothing OrElse ns Is Nothing Then Return Nothing
        Dim skinRef = ns.SkinInstanceRef
        If skinRef Is Nothing OrElse skinRef.IsEmpty() Then
            ' Unskinned or synthetic anchor: the NIF sphere placed by the matrix the app draws the vertices with (palette 0:
            ' ShapeGlobalTransform for an unskinned shape, SkinningHelper.vb:2142; the anchor bone for a synthetic skin).
            If shape.Geometry Is Nothing OrElse geo.GPUBoneMatrices Is Nothing OrElse geo.GPUBoneMatrices.Length = 0 Then Return Nothing
            Dim b = shape.Geometry.Bounds
            Return Vector3.TransformPosition(New Vector3(b.Center.X, b.Center.Y, b.Center.Z), geo.GPUBoneMatrices(0))
        End If
        If geo.BoneWorldPose Is Nothing OrElse geo.SkinRootPose Is Nothing Then Return Nothing
        Dim geomWorld = Transform_Class.GetGlobalTransform(ns, nif)
        Dim skin = nif.GetBlock(skinRef)
        Dim u As Sphere? = Nothing
        Dim local As Transform_Class
        Dim niSkin = TryCast(skin, NiSkinInstance)
        If niSkin IsNot Nothing Then
            Dim data = nif.GetBlock(niSkin.Data)
            If data?.BoneList Is Nothing OrElse data.BoneList.Count <> geo.BoneWorldPose.Length Then Return Nothing
            For i = 0 To data.BoneList.Count - 1
                Dim bs = data.BoneList(i).BoundingSphere
                Dim p = Place(geo.BoneWorldPose(i), New Sphere(New Vector3(bs.Center.X, bs.Center.Y, bs.Center.Z), bs.Radius))
                u = If(u.HasValue, UnionSse(u.Value, p), p)
            Next
            Dim st = data.SkinTransform
            Dim overall As New Transform_Class With {.Rotation = st.Rotation, .Translation = st.Translation, .Scale = st.Scale}
            local = overall.ComposeTransforms(geo.SkinRootPose.Inverse())   ' overall x inverse(root): inverse(root) first
        Else
            Dim bsSkin = TryCast(skin, BSSkin_Instance)
            Dim bd = If(bsSkin Is Nothing, Nothing, nif.GetBlock(bsSkin.Data))
            If bd?.BoneList Is Nothing OrElse bd.BoneList.Count <> geo.BoneWorldPose.Length Then Return Nothing
            For i = 0 To bd.BoneList.Count - 1
                Dim bs = bd.BoneList(i).BoundingSphere
                Dim p = Place(geo.BoneWorldPose(i), New Sphere(New Vector3(bs.Center.X, bs.Center.Y, bs.Center.Z), bs.Radius))
                u = If(u.HasValue, UnionFo4(u.Value, p), p)
            Next
            local = geo.SkinRootPose.Inverse()
        End If
        If Not u.HasValue Then Return Nothing
        Return Place(geomWorld.ComposeTransforms(local), u.Value).Center
    End Function

End Module
