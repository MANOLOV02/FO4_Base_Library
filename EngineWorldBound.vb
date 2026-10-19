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

    ''' <summary>The merge's guard, 1e-6: SSE [0x1420CF074] (.data, 1e-6 in the image; [0x141B5BE14] = 0.5 the half), FO4
    ''' [0x142F4C75C] (bytes BD 37 86 35 = 0x358637BD, verified 12-oct-2026).</summary>
    Private ReadOnly MergeGuard As Single = BitConverter.Int32BitsToSingle(&H358637BD)

    ''' <summary>NiBound::Merge, A grows to contain B - one law in both exes: SSE 0x140EFECA0 (dump of 7-oct-2026) and FO4 0x1416E8350 (ola
    ''' 0c pto 25: D = A.c - B.c, dr = rB - rA; dr &gt;= 0 and dr^2 &gt;= |D|^2 -&gt; B, 0x1416E83D6..F2; dr &lt; 0 and dr^2 &gt;= |D|^2 -&gt; A,
    ''' 0x1416E83F4..F7; else dist = sqrt, the centre B.c + t D with t = (dist - dr) (0.5 / dist) when dist &gt; 1e-6, 0x1416E8411..44F;
    ''' r = (rB + dist + rA) 0.5, 0x1416E8459..465). FO4's NiNode::UpdateWorldBound and the BSFadeNode bound use it (ptos 14 / 25).</summary>
    Friend Function MergeBound(a As Sphere, b As Sphere) As Sphere
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
        If l > MergeGuard Then                                           ' 0x140EFED58..D5F / 0x1416E8411
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

    ''' <summary>Ola 0c ptos 25 / 14: the root BSFadeNode's world bound at the top-level update (BSFadeNode::UpdateDownwardPass, vt
    ''' 0x142903BA8 slot 48 0x14217DFC0, [upd+0x18] = 1, replaces NiNode's with 0x14217EE50): radius 0 (0x14217EE60), then each geometry
    ''' of its list whose world radius is &gt; 0 (comiss, 0x14217EECD..EF1: not 0, not negative, not NaN) - the first copied, the rest
    ''' merged (MergeBound, 0x1416E8350). Nothing when none qualifies (the radius stays 0). The list (members and order) is the
    ''' caller's: NifSceneVisibility.FadeListGeometries (0x14217ED30, resultado5 item 2).</summary>
    Friend Function FadeNodeBound(spheres As IEnumerable(Of Sphere)) As Sphere?
        Dim acc As Sphere? = Nothing
        For Each s In spheres
            If Not (s.Radius > 0.0F) Then Continue For
            acc = If(acc.HasValue, MergeBound(acc.Value, s), s)
        Next
        Return acc
    End Function

    ''' <summary>0x140EFCF20: c' = t + s R c, r' = s r - with the app's matrix (the placement it draws the vertices with) and the scalar
    ''' scale the engine's NiTransform has (Transform_Class.EscalaComoEscalar; a non-uniform scale exists only in the app's pose).</summary>
    Friend Function Place(t As Transform_Class, s As Sphere) As Sphere
        Dim m = t.ToMatrix4d()
        Dim c = Vector3d.TransformPosition(New Vector3d(s.Center.X, s.Center.Y, s.Center.Z), m)
        Dim exacto As Boolean
        Return New Sphere(New Vector3(CSng(c.X), CSng(c.Y), CSng(c.Z)), s.Radius * t.EscalaComoEscalar(exacto))
    End Function

    ''' <summary>The world-bound centre of <paramref name="shape"/> (WorldBound without the unskinned radius, rev-10). Nothing as
    ''' WorldBound: the caller keeps the vertex box centre.</summary>
    Friend Function Centre(shape As IRenderableShape, geo As SkinnedGeometry) As Vector3?
        Dim s = WorldBound(shape, geo, withRadius:=False)
        Return If(s.HasValue, s.Value.Center, CType(Nothing, Vector3?))
    End Function

    ''' <summary>The alpha list's sort key, one law in both exes: k = (d.y c.y + d.x c.x) + d.z c.z in float32, c = the world-bound
    ''' centre (<see cref="Centre"/>), d = the view direction. FO4 0x14221DA70 (0x14221DB5A..DBB3; d = camera+0x70..0x78, the third
    ''' column of the rotation-only view 0x14182D83C..D884). SSE 0x141555540 (0x14155563A..68F; d = camera+0x7C / +0x88 / +0x94),
    ''' reached for the alpha list through the thunk 0x1415617D0, which clears r9 (the key's negation, 0x1415556A9..6B1, off). The
    ''' radius is subtracted only with [accumulator+0x51] (FO4 0x14221DBB7..DBC4, SSE 0x141555693..6A0), which the world accumulator
    ''' never sets (FO4: every ctor writes word [+0x50] = 1, census of .text; SSE: row O-PT of C8): not here. Sorted descending (the
    ''' farthest first) and stable (FO4 0x14221DBCD..DBE0, SSE 0x1415556B4..6C7). Tools/re-docs/RE_FO4_ALPHA_ORDER_2026-10-19.
    ''' <paramref name="viewDirection"/> is the direction of the app's camera (rig aware: the law is applied over it).</summary>
    Friend Function AlphaSortKey(viewDirection As Vector3, centre As Vector3) As Single
#If DEBUG Then
        ' ParityGate alpha-sort-key's directed mutants (Fo4GBufferConstants.GateMutantOn, the one switch). The translation is a fault
        ' injection (no such input reaches this law): 4096 = the corpus' coordinate magnitude (RE 6.1). A per-shape radius and the view's
        ' translation at the call site are the gate's text mutants of Render.vb.
        If Fo4GBufferConstants.GateMutantOn("alpha-key-assoc") Then Return viewDirection.Y * centre.Y + (viewDirection.X * centre.X + viewDirection.Z * centre.Z)
        If Fo4GBufferConstants.GateMutantOn("alpha-key-translation") Then Return ((viewDirection.Y * centre.Y + viewDirection.X * centre.X) + viewDirection.Z * centre.Z) + 4096.0F
        ' ShadowGate --order-ab's instrument: the key before item 2, -TransformPosition(c, view).Z (view.M?3 = -viewDirection, exact).
        If Fo4GBufferConstants.GateMutantOn("alpha-key-old") Then
            Return -(((centre.X * -viewDirection.X + centre.Y * -viewDirection.Y) + centre.Z * -viewDirection.Z) + Fo4GBufferConstants.GateAlphaOldViewM43)
        End If
#End If
        Return (viewDirection.Y * centre.Y + viewDirection.X * centre.X) + viewDirection.Z * centre.Z
    End Function

    ''' <summary>The view direction <see cref="AlphaSortKey"/> takes, read from a view matrix built by Matrix4.LookAt (row vectors:
    ''' the third column is the camera's +Z, which looks away from the scene; the engine's d looks into it).</summary>
    Friend Function ViewDirection(view As Matrix4) As Vector3
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("alpha-dir-sign") Then Return New Vector3(view.M13, view.M23, view.M33)   ' ParityGate alpha-sort-key
#End If
        Return New Vector3(-view.M13, -view.M23, -view.M33)
    End Function

    ''' <summary>Ola 0c pto 25: the world bound sphere of <paramref name="shape"/> (WorldBound with its radius).</summary>
    Friend Function WorldSphere(shape As IRenderableShape, geo As SkinnedGeometry) As Sphere?
        Return WorldBound(shape, geo, withRadius:=True)
    End Function

    ''' <summary>The world bound of <paramref name="shape"/> with the bone / root worlds <paramref name="geo"/> holds this frame
    ''' (SkinnedGeometry.BoneWorldPose / SkinRootPose, written where the app skins). Unskinned or a synthetic anchor: the NIF sphere
    ''' placed by palette 0 (rev-04), its radius r' = s r (0x140EFCF20 / 0x1416D5EF0) only with <paramref name="withRadius"/>. Nothing
    ''' when the shape has no sphere the game would use or no geometry / palette yet, and when the bone-data list does not have the
    ''' palette's bone count (declared gap, D-L4).</summary>
    Private Function WorldBound(shape As IRenderableShape, geo As SkinnedGeometry, withRadius As Boolean) As Sphere?
        Dim nif = shape?.NifContent
        Dim ns = shape?.NifShape
        If nif Is Nothing OrElse ns Is Nothing Then Return Nothing
        Dim skinRef = ns.SkinInstanceRef
        If skinRef Is Nothing OrElse skinRef.IsEmpty() Then
            ' Unskinned or synthetic anchor: the NIF sphere placed by the matrix the app draws the vertices with (palette 0:
            ' ShapeGlobalTransform for an unskinned shape, SkinningHelper.vb:2142; the anchor bone for a synthetic skin).
            If shape.Geometry Is Nothing OrElse geo.GPUBoneMatrices Is Nothing OrElse geo.GPUBoneMatrices.Length = 0 Then Return Nothing
            Dim b = shape.Geometry.Bounds
            Dim c = Vector3.TransformPosition(New Vector3(b.Center.X, b.Center.Y, b.Center.Z), geo.GPUBoneMatrices(0))
            ' The radius only when asked (rev-10): r' = s r of 0x140EFCF20 / 0x1416D5EF0 (Place, with the app's world of the shape).
            If Not withRadius Then Return New Sphere(c, 0.0F)
            ' Ola 0c parte 2: the world the shape is drawn with (NodeRestLayer), as palette 0.
            Return New Sphere(c, Place(NodeRestLayer.DrawnGlobal(TryCast(ns, NiAVObject), nif), New Sphere(Vector3.Zero, b.Radius)).Radius)
        End If
        If geo.BoneWorldPose Is Nothing OrElse geo.SkinRootPose Is Nothing Then Return Nothing
        ' Ola 0c parte 2: drawn (NodeRestLayer) when the shape's own NIF chain is (SkinnedGeometry.NifOwnDrawn), else the file's.
        Dim geomWorld = If(geo.NifOwnDrawn, NodeRestLayer.DrawnGlobal(TryCast(ns, NiAVObject), nif), Transform_Class.GetGlobalTransform(ns, nif))
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
                u = If(u.HasValue, MergeBound(u.Value, p), p)
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
        Return Place(geomWorld.ComposeTransforms(local), u.Value)
    End Function

End Module
