Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>Lo que el JUEGO deja de la visibilidad de una forma del NIF al cargar el archivo, leído de su cadena de nodos y de su
''' bound (chunk C3, decisiones del usuario C-ANC / C-NAN). Se evalúa sobre el NIF EN MEMORIA cada vez que se pregunta: el editor
''' recalcula bounds (UpdateBounds) y el estado tiene que seguirlo. El render lo memoiza por frame (RenderableMesh.SceneVisibility).
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
''' <para>NO está acá (huecos declarados, propuesta C3): el bound de una forma con skin (SSE 0x140EFC870, FO4 0x1416D7D50), el test
''' de planos con bound NaN (FO4 0x1416E6A40), un ancestro de radio 0 sobre una forma con 0x800, NiGeometry, NiSwitchNode/NiLODNode.</para></summary>
Public Module NifSceneVisibility

    ''' <summary>El estado de carga de una forma. El default (Nothing) = visible: sin ancestro oculto y sin descarte por bound.</summary>
    Public Structure LoadState
        ''' <summary>C-ANC: algún nodo de su cadena queda oculto tras la carga. Se muestra con la casilla "Render hidden shapes".</summary>
        Public ReadOnly HiddenByAncestor As Boolean
        ''' <summary>C-NAN: por qué el culling del juego no la dibuja nunca, como lo dice el aviso de una vista COMPUESTA (no se dibuja);
        ''' Nothing = el juego la dibuja.</summary>
        Public ReadOnly BoundCullReason As String
        ''' <summary>C-NAN: el mismo motivo como lo dice el aviso de una vista de PIEZA, que la dibuja para editarla
        ''' (RenderIntent.DrawEngineSkippedForEditing, C2 L5). Nothing exactamente cuando BoundCullReason es Nothing.</summary>
        Public ReadOnly BoundCullShown As String
        Public Sub New(hiddenByAncestor As Boolean, boundCullReason As String, boundCullShown As String)
            Me.HiddenByAncestor = hiddenByAncestor
            Me.BoundCullReason = boundCullReason
            Me.BoundCullShown = boundCullShown
        End Sub
    End Structure

    ''' <summary>GATE ONLY (sin UI): ShadowGate --scene-visibility, control negativo del pixel. True apaga C-NAN.</summary>
    Friend GateIgnoreBoundCull As Boolean = False

    ''' <summary>Los tres motivos del aviso (inglés: UI), uno por rama de la ley, cada uno en sus dos formas (C2: compuesta = no se
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
    Private Function GameName(isSse As Boolean) As String
        Return If(isSse, "Skyrim SE", "Fallout 4")
    End Function

    Public Function Evaluate(shape As IRenderableShape) As LoadState
        If shape Is Nothing Then Return Nothing
        Return Evaluate(shape.NifContent, shape.NifShape)
    End Function

    Public Function Evaluate(nif As NifFile, shape As INiShape) As LoadState
        If nif Is Nothing OrElse shape Is Nothing Then Return Nothing
        ' UNA subida por la cadena, con la ley de padre de las transformadas de la app (Transform_Class.GetGlobalTransform).
        ' La guarda de ciclo no es un umbral: un NIF malformado con un ciclo de Children colgaría el render.
        Dim chain As New List(Of NiNode)
        Dim p = nif.GetParentNode(shape)
        While p IsNot Nothing AndAlso Not chain.Contains(p)
            chain.Add(p)
            p = nif.GetParentNode(p)
        End While
        Dim hidden = False
        For Each a In chain
            If NodeHiddenAtLoad(a) Then hidden = True : Exit For
        Next
        Dim cull = BoundCull(nif, shape, chain)
        Return New LoadState(hidden, cull.Undrawn, cull.Drawn)
    End Function

    ''' <summary>El bit oculto de un nodo tras su LoadBinary: el del archivo, salvo un nodo de rango, cuyo SetCurrent lo pisa con
    ''' Not (Min &lt;= Current &lt;= Max) (SSE 0x140FDD838..0x140FDD865, FO4 0x1417D38E8..0x1417D390B). Byte de VB = sin signo.</summary>
    Public Function NodeHiddenAtLoad(node As NiNode) As Boolean
        Dim rn = TryCast(node, BSRangeNode)
        If rn IsNot Nothing Then Return Not (rn.Min <= rn.Current AndAlso rn.Current <= rn.Max)
        Return (node.Flags_ui And 1UI) <> 0UI
    End Function

    ''' <summary>C-NAN. <paramref name="chain"/> = padres de la forma, del padre directo a la raíz.</summary>
    Private Function BoundCull(nif As NifFile, shape As INiShape, chain As List(Of NiNode)) As (Drawn As String, Undrawn As String)
        If GateIgnoreBoundCull Then Return (Nothing, Nothing)
        Dim ver = nif.Header?.Version
        If ver Is Nothing Then Return (Nothing, Nothing)
        Dim isSse = ver.IsSSE()
        If Not isSse AndAlso Not ver.IsFO4() Then Return (Nothing, Nothing)          ' sólo los dos motores trazados
        Dim tri = TryCast(shape, BSTriShape)                                ' familia BSGeometry (0x140EFC150 / 0x1416D4EA0)
        If tri Is Nothing Then Return (Nothing, Nothing)
        ' Con skin el bound de mundo sale de los huesos, no de la esfera del modelo (SSE 0x140EFC870, FO4 0x1416D5502).
        If shape.SkinInstanceRef IsNot Nothing AndAlso Not shape.SkinInstanceRef.IsEmpty() Then Return (Nothing, Nothing)
        If (tri.Flags_ui And &H800UI) <> 0UI Then Return (Nothing, Nothing)           ' SSE 0x140FEDA9A, FO4 0x1417E1C82
        Dim b = tri.Bounds
        Dim r = b.Radius
        Dim nanSphere = isSse AndAlso (Single.IsNaN(b.Center.X) OrElse Single.IsNaN(b.Center.Y) OrElse
                                       Single.IsNaN(b.Center.Z) OrElse Single.IsNaN(r))   ' _fdtest = 2, 0x140EFC191..0x140EFC1F2
        If nanSphere Then r = 0.0F                                         ' 0x140EFC21A
        ' Escala de mundo de la raíz hacia la forma (padre x local: SSE 0x140F0303C..0x140F03049, FO4 0x140344966), en Single.
        Dim ws As Single = 1.0F
        For i = chain.Count - 1 To 0 Step -1
            ws = ws * chain(i).Scale
        Next
        ws = ws * tri.Scale
        Dim worldRadius As Single = r * ws                                 ' 0x140EFCFDF / 0x1416D5FD0
        If BitConverter.SingleToInt32Bits(worldRadius) <> 0 Then Return (Nothing, Nothing)   ' cmp dword ..., 0
        If nanSphere Then Return NoticeNaN()
        If BitConverter.SingleToInt32Bits(r) = 0 Then Return NoticeRadius0(isSse)
        Return NoticeScale0(isSse)
    End Function

End Module
