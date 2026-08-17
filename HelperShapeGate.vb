''' <summary>
''' Gate ÚNICO de dibujo del pipeline de render para las "helper shapes"
''' (ver <see cref="IRenderableShape.IsHelperShape"/> para la ley y las fuentes canónicas).
'''
''' <para>Copia la mecánica del canónico: BodySlide expone la casilla "Show Helper Shapes"
''' (<c>PreviewPanel.cpp:83-85</c>) y apaga la visibilidad en vivo
''' (<c>if (m-&gt;bHelperShape &amp;&amp; !showHelperShapes) SetMeshVisibility(false)</c>, <c>:519/:550</c>),
''' sin recargar la escena. Su default es DESTILDADO. OutfitStudio —el editor— no lo consulta y
''' muestra todo: la misma asimetría que acá hay entre NPC Manager (visor) y Wardrobe Manager (editor).</para>
'''
''' <para>⛔ <b>Esto decide qué se DIBUJA, nunca qué se CONSERVA.</b> Ningún camino de escritura
''' (guardar, construir, clonar, copiar, mergear, shapedata) puede perder una helper. La única
''' excepción es el exporter de NPC Manager, que tiene su PROPIA casilla y NO consulta este gate.</para>
'''
''' <para>⛔ El "Mask Occluded" de WM tampoco pasa por acá: usa <c>IsHelperShape</c> a secas. Un proxy
''' de colisión no es un occluder legítimo lo estés mirando o no, y su máscara alimenta el zap, que al
''' construir puede llegar a borrar la shape. Una preferencia de VISIBILIDAD no puede decidir qué
''' geometría sobrevive.</para>
'''
''' <para>⛔ NO se gatea <c>RebuildRenderBuckets</c>: su detector de staleness es
''' <c>(Opaque+Cutout+Decal+Blended).Count &lt;&gt; meshes.Count</c>, así que filtrar ahí lo dejaría
''' permanentemente falso y el rebuild correría en TODOS los frames.</para>
''' </summary>
Public Module HelperShapeGate

    ''' <summary>Gate de dibujo: pase iluminado, overlays, pase de profundidad de sombras, lista de
    ''' casters y encuadre de cámara. Se evalúa POR FRAME —no en la recolección de meshes— para que la
    ''' casilla repinte sin recargar la escena, igual que <c>OnShowHelperShapes</c> del canónico.</summary>
    Public Function IsShapeDrawable(shape As IRenderableShape) As Boolean
        If shape Is Nothing Then Return False
        If shape.RenderHide Then Return False
        If Not shape.IsHelperShape Then Return True
        Return Config_App.ShowHelperShapesEfectivo()
    End Function

End Module
