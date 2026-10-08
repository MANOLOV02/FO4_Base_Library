''' <summary>
''' Gate ÚNICO de dibujo del pipeline de render para las "helper shapes"
''' (ver <see cref="IRenderableShape.IsHelperShape"/> para la ley y las fuentes canónicas).
'''
''' <para>Copia la mecánica del canónico: BodySlide expone la casilla "Show Helper Shapes" y apaga la
''' visibilidad en vivo, sin recargar la escena
''' (<c>PreviewPanel.cpp</c>, <c>OnShowHelperShapes</c> →
''' <c>if (m-&gt;bHelperShape &amp;&amp; !showHelperShapes) SetMeshVisibility(false)</c>). Su default es DESTILDADO. OutfitStudio —el editor— no lo consulta y
''' muestra todo: la misma asimetría que acá hay entre NPC Manager (visor) y Wardrobe Manager (editor).</para>
'''
''' <para><b>Esto decide qué se DIBUJA, nunca qué se CONSERVA.</b> Ningún camino de escritura
''' (guardar, construir, clonar, copiar, mergear, shapedata) puede perder una helper. La única
''' excepción es el exporter de NPC Manager, que tiene su PROPIA casilla y NO consulta este gate.</para>
'''
''' <para>El "Mask Occluded" de WM NO pasa por la casilla: usa <see cref="IsOccluderCandidate"/>. Un proxy
''' de colisión no es un occluder legítimo lo estés mirando o no, y su máscara alimenta el zap, que al
''' construir puede llegar a borrar la shape. Una preferencia de VISIBILIDAD no puede decidir qué
''' geometría sobrevive. Por la misma razón el oclusor excluye lo que el MOTOR no dibuja nunca (ancestro
''' oculto, bound de radio 0: <see cref="NifSceneVisibility"/>), con la casilla prendida o no.</para>
'''
''' <para>NO se gatea <c>RebuildRenderBuckets</c>: su detector de staleness es
''' <c>(Opaque+Cutout+Decal+Blended).Count &lt;&gt; meshes.Count</c>, así que filtrar ahí lo dejaría
''' permanentemente falso y el rebuild correría en TODOS los frames.</para>
''' </summary>
Public Module HelperShapeGate

    ''' <summary>Gate de dibujo: pase iluminado, overlays, pase de profundidad de sombras, lista de
    ''' casters y encuadre de cámara. Se evalúa POR FRAME —no en la recolección de meshes— para que la
    ''' casilla repinte sin recargar la escena, igual que <c>OnShowHelperShapes</c> del canónico.
    ''' <para>Además de la casilla, la ley de carga del MOTOR (<see cref="NifSceneVisibility"/>, chunk C3): una forma bajo un
    ''' nodo que queda oculto (C-ANC) se muestra con la MISMA casilla que las helper; una forma cuyo bound de mundo tiene radio 0
    ''' (C-NAN) no se dibuja en una vista compuesta y sí en una de pieza (RenderIntent.DrawEngineSkippedForEditing, regla de
    ''' editor del usuario, C2 L5), con la casilla prendida o no; el aviso del frame la lista en las dos (RenderableMesh.ReportUndrawn).
    ''' Sin control (un censo, una herramienta): la regla compuesta, el default de RenderIntent (C2 v7 D-G1).</para></summary>
    Public Function IsShapeDrawable(shape As IRenderableShape) As Boolean
        If shape Is Nothing OrElse shape.RenderHide Then Return False
        Return IsShapeDrawable(shape, NifSceneVisibility.Evaluate(shape), drawEngineSkippedForEditing:=False)
    End Function

    ''' <summary>Lo mismo con el estado ya evaluado y la vista: el render lo pasa memoizado por frame (RenderableMesh.IsDrawable).
    ''' Friend: el estado sólo lo arma la lib.</summary>
    Friend Function IsShapeDrawable(shape As IRenderableShape, vis As NifSceneVisibility.LoadState, drawEngineSkippedForEditing As Boolean) As Boolean
        Return IsShapeShown(shape, vis) AndAlso DrawnBy(vis.BoundCullReason IsNot Nothing, drawEngineSkippedForEditing)
    End Function

    ''' <summary>La parte de la CASILLA: RenderHide, helper shape y ancestro oculto (C-ANC). Sin C-NAN: el aviso del frame la usa
    ''' para listar lo que el usuario pidió ver y el juego no dibuja.</summary>
    Friend Function IsShapeShown(shape As IRenderableShape, vis As NifSceneVisibility.LoadState) As Boolean
        If shape Is Nothing OrElse shape.RenderHide Then Return False
        Return ShownBy(shape.IsHelperShape, vis.HiddenByAncestor, Config_App.ShowHelperShapesEfectivo())
    End Function

    ''' <summary>La composición de la casilla, pura (gate scene-visibility): visible si no es helper ni cuelga de un nodo oculto; si
    ''' no, lo que diga la casilla.</summary>
    Friend Function ShownBy(isHelper As Boolean, hiddenByAncestor As Boolean, showHelpers As Boolean) As Boolean
        If Not isHelper AndAlso Not hiddenByAncestor Then Return True
        Return showHelpers
    End Function

    ''' <summary>La composición de C-NAN con la vista, pura (gate scene-visibility): el juego no la dibuja; la vista de pieza sí.</summary>
    Friend Function DrawnBy(boundCulled As Boolean, drawEngineSkippedForEditing As Boolean) As Boolean
        Return Not boundCulled OrElse drawEngineSkippedForEditing
    End Function

    ''' <summary>¿Puede esta shape OCLUIR a otra? El único predicado del "Mask Occluded" de WM. Independiente de la casilla y de la
    ''' vista: excluye RenderHide, las helper shapes y lo que el MOTOR no dibuja nunca (NifSceneVisibility: C-ANC, C-NAN). Se evalúa
    ''' fresco: lo llama un botón, no un frame.</summary>
    Public Function IsOccluderCandidate(shape As IRenderableShape) As Boolean
        If shape Is Nothing OrElse shape.RenderHide Then Return False
        Return OccluderBy(shape.IsHelperShape, NifSceneVisibility.Evaluate(shape))
    End Function

    ''' <summary>La composición del oclusor, pura (gate scene-visibility).</summary>
    Friend Function OccluderBy(isHelper As Boolean, vis As NifSceneVisibility.LoadState) As Boolean
        Return Not isHelper AndAlso Not vis.HiddenByAncestor AndAlso vis.BoundCullReason Is Nothing
    End Function

End Module
