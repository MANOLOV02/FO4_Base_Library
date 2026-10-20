Imports System.IO
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>How the game puts the NIF a shape comes from into an actor (Fallout4.exe 1.11.240; Tools\re-docs\RE_FO4_SKINNED_ATTACH_2026-10-19\re.md sections 1 and 9).
''' The host that knows the record path sets it (IRenderableShape.GameAttach); a loose NIF stays Unknown and no attach law is applied.</summary>
Public Enum GameAttachKind
    ''' <summary>No record path known (a loose NIF, a picker without a record): no attach law.</summary>
    Unknown = 0
    ''' <summary>The model of an ARMA of a worn ARMO (skin or outfit): BipedAnim 0x140359BA0 (ARMO 0x1D / LIGH 0x22 with a model name,
    ''' 0x14035A1A4..A1D1) -&gt; 0x14035DC20 -&gt; AttachSkinnedObject 0x14035E960 when the tree has a skinned geometry (0x1403602D0).</summary>
    BipedWorn = 1
    ''' <summary>An OMOD model attached by connect point (NPC_ mods: 0x14050AF40 -&gt; 0x140247F90 -&gt; 0x1404350D0 -&gt; 0x14186EF40 -&gt;
    ''' 0x14186F330; ARMO mods after the biped attach, 0x14035F550 -&gt; 0x140247F90): the clone hangs whole at the connect point.</summary>
    ConnectPoint = 2
    ''' <summary>A head part (the FaceGen path, not the biped).</summary>
    HeadPart = 3
    ''' <summary>A *_faceBones.nif: the game derives it from a drawn model's path (0x140653410, string 0x1424D6D58 '_faceBones.nif', callers
    ''' 0x14045F780 / 0x14061C960 / 0x1406870D0) and only reads it as face input (biped: 0x14035EA12..EB1C, released at 0x14035EB0A, never
    ''' attached; head parts: the FaceGen bake). No record of the 8 masters names a *_faceBones.nif as a model (census ola 7b).</summary>
    FaceBonesInput = 4
End Enum

''' <summary>Ola 7b ronda 3 (r19i-01): which rest-layer law of a skinned shape mounted on the skeleton the trace covers
''' (GameAttachLaw.RestTraceOf, its one owner; NodeRestLayer.NoteMounted applies it). Traced on Fallout4.exe 1.11.240 only, for the biped and
''' the connect point (re.md 1.2, 2, 9.6); everything else - Skyrim SE, Fallout 4's head parts (the FaceGen path), *_faceBones files, loose
''' NIFs - keeps the law before 7b.</summary>
Friend Enum RestLawTrace
    ''' <summary>Not traced: the chains the shape draws as the file or by the skeleton are noted as before 7b (the V1 notice).</summary>
    Untraced = 0
    ''' <summary>Fallout 4's biped (BipedWorn): only a skin bone the skeleton does not resolve, by its own controller - the bone the biped
    ''' re-parents (0x14035F9B3..F9C8; F2). A resolved bone (F1), an ancestor (F4) and the skin root reach no skin (0x14035F890 -&gt; 0x1416C6510,
    ''' 0x14035EE98).</summary>
    Biped = 1
    ''' <summary>Fallout 4's connect point (ConnectPoint): nothing (the clone hangs whole at the connect point, re.md 9.6).</summary>
    ConnectPoint = 2
End Enum

''' <summary>THE ATTACH LAWS OF FALLOUT 4 the preview applies to a shape, one owner (ola 7b): which path the game puts the shape's NIF
''' through, what the biped's skinned attach leaves out of the actor, and the notices that say it.</summary>
Public Module GameAttachLaw

    ''' <summary>A *_faceBones.nif: the name the engine builds from a model path by appending "_faceBones.nif" (0x140653410), compared
    ''' case-insensitively as the engine's own test does (tolower per char, 0x140361879 / 0x140361884).</summary>
    Public Function IsFaceBonesFile(filePath As String) As Boolean
        If String.IsNullOrEmpty(filePath) Then Return False
        Return Path.GetFileName(filePath).EndsWith("_faceBones.nif", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>The attach path of a shape: a Fallout 4 *_faceBones.nif is face input whatever the host declared (the file is never
    ''' attached), else what the host declared. The one rule both implementers of IRenderableShape.GameAttach read. Ola 7b ronda 3
    ''' (r19i-02): only a Fallout 4 NIF (TracedGame) - the name is Fallout4.exe's (0x140653410); Skyrim SE has no such file: 0 entries named
    ''' *_facebones.nif in its 99 archives and its loose Meshes (the reviewer's count, r19i-02, and Census7b "facebones sse"; Fallout 4: 484).</summary>
    Public Function Resolve(sourcePath As String, nif As NifFile, declared As GameAttachKind) As GameAttachKind
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("ga-no-facebones-precedence") Then Return declared
        ' ronda 3 directed mutant (ParityGate fo4-attach-law [facebones-juego]): the _faceBones rule without the game's condition
        If Fo4GBufferConstants.GateMutantOn("ga-facebones-any-game") Then Return If(IsFaceBonesFile(sourcePath), GameAttachKind.FaceBonesInput, declared)
#End If
        Return If(IsFaceBonesFile(sourcePath) AndAlso TracedGame(nif), GameAttachKind.FaceBonesInput, declared)
    End Function

    ''' <summary>THE BIPED'S SKINNED ATTACH (AttachSkinnedObject 0x14035E960): for a skinned object it walks only the root's DIRECT children
    ''' (0x14035ED1A..EECA); a child that is not a geometry is RemoveChild'ed and released with its whole subtree (0x14035ED52..EDBB,
    ''' RemoveChild = vt+0x1E8 = 0x1416BE860); an unskinned geometry stays in the root ([geo+0x140] = 0: 0x14035EDED..EE00), which the
    ''' caller releases (0x14035A719); only a skinned direct child is moved into the actor (0x14035EEA3). True = the game does not draw
    ''' <paramref name="shape"/>: a BipedWorn Fallout 4 shape whose NIF tree has some skinned geometry (0x1403602D0; without one the NIF
    ''' is attached whole, 0x14035A4CF..A4F1 -&gt; 0x14035D4F0) and that is not a skinned direct child of the root.
    ''' <para>It reads only what a load fixes (the NIF's tree and skin instances, the host's declared path): the preview evaluates it once
    ''' per mesh, when the mesh is built (RenderableMesh, through NifSceneVisibility.OutsideAttachOf), never per frame (p7b-01).</para></summary>
    Friend Function OutsideSkinnedAttach(shape As IRenderableShape) As Boolean
        If shape Is Nothing Then Return False
        Dim worn = shape.GameAttach = GameAttachKind.BipedWorn
#If DEBUG Then
        ' ParityGate fo4-attach-law directed mutants (Fo4GBufferConstants.GateMutantOn, the one switch): the law applied to a connect-point part
        If Fo4GBufferConstants.GateMutantOn("ga-connectpoint-applies") Then worn = worn OrElse shape.GameAttach = GameAttachKind.ConnectPoint
#End If
        If Not worn Then Return False
        Dim nif = shape.NifContent
        Dim ns = shape.NifShape
        If nif Is Nothing OrElse ns Is Nothing Then Return False
        Dim tracedFo4 = TracedGame(nif)   ' traced on Fallout4.exe only
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("ga-sse-applies") Then tracedFo4 = nif.Header?.Version IsNot Nothing
#End If
        If Not tracedFo4 Then Return False
        Dim root = nif.GetRootNode()
        Dim skinnedTree = root IsNot Nothing AndAlso TreeHasSkin(nif, root, New HashSet(Of INiObject)(ReferenceEqualityComparer.Instance))
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("ga-no-treehasskin") Then skinnedTree = root IsNot Nothing
#End If
        If Not skinnedTree Then Return False
        Dim keptByAttach = HasSkin(nif, ns) AndAlso nif.GetParentNode(ns) Is root
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("ga-no-direct-child") Then keptByAttach = HasSkin(nif, ns)
        If Fo4GBufferConstants.GateMutantOn("ga-unskinned-kept") Then keptByAttach = nif.GetParentNode(ns) Is root
#End If
        Return Not keptByAttach
    End Function

    ''' <summary>Ola 7b ronda 3: the laws of this module are traced on Fallout4.exe 1.11.240 only - a Fallout 4 NIF (NifFile version FO4).
    ''' The one test OutsideSkinnedAttach, Resolve and RestTraceOf read.</summary>
    Private Function TracedGame(nif As NifFile) As Boolean
        Dim ver = nif?.Header?.Version
        Return ver IsNot Nothing AndAlso ver.IsFO4()
    End Function

    ''' <summary>Ola 7b ronda 3 (r19i-01): the rest-layer law the trace covers for <paramref name="shape"/> (RestLawTrace): Fallout 4's biped
    ''' (BipedWorn) and connect point (ConnectPoint); Untraced for anything else - Skyrim SE, a head part, a *_faceBones file, a loose NIF.
    ''' It reads only what a load fixes: SkinningHelper.ExtractSkinnedGeometry evaluates it once per extraction and keeps it in the geometry
    ''' (SkinnedGeometry.RestTrace).</summary>
    Friend Function RestTraceOf(shape As IRenderableShape) As RestLawTrace
        If shape Is Nothing Then Return RestLawTrace.Untraced
        Dim traced = TracedGame(shape.NifContent)
        Dim kind = shape.GameAttach
#If DEBUG Then
        ' ronda 3 directed mutants (ParityGate fo4-attach-law [traza], ShadowGate --attach-law [sse v1] / [f2 HeadPart v1]): the 7b rest law
        ' applied where it is not traced - Skyrim SE, a Fallout 4 head part
        If Fo4GBufferConstants.GateMutantOn("ga-rest-trace-sse") Then traced = shape.NifContent?.Header?.Version IsNot Nothing
        If Fo4GBufferConstants.GateMutantOn("ga-rest-trace-headpart") AndAlso kind = GameAttachKind.HeadPart Then kind = GameAttachKind.BipedWorn
#End If
        If Not traced Then Return RestLawTrace.Untraced
        Select Case kind
            Case GameAttachKind.BipedWorn : Return RestLawTrace.Biped
            Case GameAttachKind.ConnectPoint : Return RestLawTrace.ConnectPoint
            Case Else : Return RestLawTrace.Untraced
        End Select
    End Function

    ''' <summary>[geo+0x140] (the skin instance) set: the test of 0x1403602D0 / 0x14035EDED.</summary>
    Private Function HasSkin(nif As NifFile, ns As INiShape) As Boolean
        Return ns.HasSkinInstance AndAlso nif.GetBlock(ns.SkinInstanceRef) IsNot Nothing
    End Function

    ''' <summary>0x1403602D0: some geometry of the tree under <paramref name="node"/> (children by vt+0x20, NiNode) has a skin.</summary>
    Private Function TreeHasSkin(nif As NifFile, node As NiNode, seen As HashSet(Of INiObject)) As Boolean
        If node Is Nothing OrElse Not seen.Add(node) OrElse node.Children Is Nothing Then Return False
        For Each r In node.Children.References
            Dim c = nif.GetBlock(r)
            Dim s = TryCast(c, INiShape)
            If s IsNot Nothing AndAlso HasSkin(nif, s) Then Return True
            Dim n = TryCast(c, NiNode)
            If n IsNot Nothing AndAlso TreeHasSkin(nif, n, seen) Then Return True
        Next
        Return False
    End Function

    ''' <summary>The notice of a composite view (the shape is not drawn): the user's words (decision 19-oct-2026) in the culling notices'
    ''' form, cause then consequence (NifSceneVisibility: "Bounding sphere of radius 0: Fallout 4 does not draw it"), so the line reads
    ''' "Outside the skinned attach: Fallout 4 does not draw it (n shapes)" - one parenthesis (p7b-06).</summary>
    Friend Const OutsideAttachNotice As String = "Outside the skinned attach: Fallout 4 does not draw it"
    ''' <summary>The notice of a piece view (drawn for editing, RenderIntent.DrawEngineSkippedForEditing), in the culling notices' piece form
    ''' ("Fallout 4: bounding sphere of radius 0"): the line "Drawn by the preview, not by the game (n shapes): Fallout 4: outside the skinned
    ''' attach when worn" says once that the game does not draw it (p7b-06).</summary>
    Friend Const OutsideAttachShownNotice As String = "Fallout 4: outside the skinned attach when worn"
    ''' <summary>The notice of a *_faceBones.nif opened by hand (user decision 19-oct-2026).</summary>
    Friend Const FaceBonesNotice As String = "the game uses this file only as face input; it never draws it"

    ''' <summary>The notice of a worn shape whose skin bone <paramref name="names"/> has its OWN controller writing at rest and no node of
    ''' that name in the actor's skeleton: the game binds each skin bone to the FIRST node of its name in the actor's live tree, preorder
    ''' (0x14035F890 -&gt; 0x1416C6510 -&gt; 0x1416C7A30); with none, it re-parents the NIF's own node under the BipedAnim root keeping its
    ''' local and its controller chain (0x14035F9B3..F9C8, AttachChild 0x1416BE690 -&gt; AttachParent 0x1416C9080), so that controller
    ''' writes the bone. The preview draws the file's transform.</summary>
    Friend Function ReparentedNotice(names As String) As String
        Return $"transform driven by a controller at rest, not applied: the game binds each skin bone to the first node of its name in the actor's tree (preorder); the skeleton has none for {names}, so the game re-parents the NIF's own bone under the skeleton root with its controller - drawn as the file"
    End Function

End Module
