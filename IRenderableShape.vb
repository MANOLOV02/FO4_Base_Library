Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>
''' Renderable wrapper around a NIF shape.  Two kinds of access live here:
'''
''' - <see cref="NifShape"/> exposes the raw INiShape (BSTriShape / NiTriShape / BSLODTriShape /
'''   any other supported subclass).  Use it for identity, parenting and partition operations
'''   that already accept INiShape (Nifcontent_Class_Manolo.UpdateSkinPartitions, etc.).
''' - <see cref="Geometry"/> is the polymorphic IShapeGeometry adapter.  Use it for any read or
'''   write of vertex/triangle/skin data — it hides the BSTriShape vs NiTriBasedGeom split and
'''   the INVERTIDAS tangent swap.
'''
''' Code that only needs identity (Name, Index, parent transform) can stick with NifShape.
''' Code that touches geometry MUST go through Geometry — direct BSTriShape casts are no longer
''' safe because the wrapped shape may be NiTriShape or BSLODTriShape.
''' </summary>
Public Interface IRenderableShape
    ' Identity
    ReadOnly Property ShapeName As String
    ReadOnly Property ShapeTarget As String
    ReadOnly Property ShapeIndex As Integer

    ' NIF data
    ReadOnly Property NifContent As Nifcontent_Class_Manolo
    ReadOnly Property NifShape As INiShape
    ReadOnly Property Geometry As IShapeGeometry
    ReadOnly Property NifSkin As INiSkin
    ReadOnly Property NifShader As INiShader
    ''' <summary>Geometría auxiliar que el MOTOR no dibuja nunca. Predicado del canónico
    ''' (BodySlide/OutfitStudio <c>GLSurface.cpp:1298</c>): <c>!shader || (shape-&gt;flags &amp; 1) != 0</c>,
    ''' con el comentario de <c>Mesh.h:169</c>: "true for shapes with no shader or with the hidden flag
    ''' set (e.g. collisions)". Ellos lo exponen con la casilla "Show Helper Shapes".
    ''' <para>Las dos señales: <b>(a)</b> sin <c>BSShaderProperty</c> — así marca HDT-SMP sus proxies de
    ''' colisión (<c>VirtualHairCollision_*</c>), vanilla la colisión Havok (<c>SKY_HAV_MAT_*</c> en los
    ''' <c>*_col.nif</c>), los <c>EditorMarker</c> y los volúmenes de emisor; <b>(b)</b> bit 0 de
    ''' <c>NiAVObject.Flags</c> = Hidden (NifSkope <c>spells/flags.cpp:539</c>; BodySlide
    ''' <c>BodySlideApp.cpp:3864</c> <c>shape-&gt;flags |= 1</c>) — la sangre de las armas, el
    ''' <c>PageText</c> de los libros, el glow del Pip-Boy.</para>
    ''' <para><b>NO ES UN GUARD DE NULO.</b> Quien pregunta "¿puedo LEER el shader?" usa
    ''' <c>NifShader Is Nothing</c> — ver <c>OSP_Clases</c> (plan de clonado) y los gates del editor de
    ''' materiales de WM. Una shape con bit0 <b>y material válido</b> es editable y clonable, y WM mismo
    ''' las fabrica (<c>BuildingForm.SetShapeHidden</c> sobre las 100 % zapeadas). Confundirlos deja el
    ''' mod construido apuntando al material de la BA2 vanilla.</para>
    ''' <para><b>NO CACHEAR</b>: los botones "Convert to renderable" / "Make helper" del editor de WM
    ''' cambian el valor en runtime.</para>
    ''' <para>El bit 0 tiene OTRO significado en un FaceGeom de FO4: ahí el CK (y nuestro bake, ver
    ''' <c>FaceGenBuilder</c>) lo usa como marca de oclusión de headwear horneada. No colisiona porque
    ''' NPC Manager nunca LEE un FaceGeom — sólo los escribe.</para>
    ''' <para>MEDIDO sobre los BSA/BA2 de los dos juegos (<c>Tools/HelperShapeScan</c>, 253.770 NIF /
    ''' 1.308.751 shapes): 5.197 = 0,40 %, sin un solo caso de geometría visible legítima. Y sobre el
    ''' ShapeData de Wardrobe Manager: 246 de 4.821 shapes (5,1 %), TODAS proxies de colisión
    ''' (Virtual*, BCA_*, *collision).</para>
    ''' <para>EL CÓDIGO DICE "helper", LA UI DICE "hidden". No es un descuido: <c>helper shape</c> es
    ''' el término del CANÓNICO (<c>bHelperShape</c> en OutfitStudio), y conservarlo deja el código
    ''' trazable contra la fuente. Pero la etiqueta era imprecisa para el usuario — el bit0 no marca sólo
    ''' mallas auxiliares: también marca sangre de armas, el <c>PageText</c> de los libros, los glows de
    ''' pantalla, las etapas de destrucción y la oclusión de headwear que el bake de FO4 hornea. Lo único
    ''' que TODAS comparten es que el motor no las dibuja, así que la UI las llama
    ''' <b>"Render hidden shapes"</b> / <b>"Make shape hidden"</b>.</para>
    ''' <para>Y por eso NO se puede partir este predicado por intención: el bit no guarda POR QUÉ está
    ''' oculta. Distinguir "pelo bajo casco" de "sangre de arma" exigiría contexto que el archivo no
    ''' tiene (y que Wardrobe Manager, abriendo un NIF suelto, nunca va a tener).</para></summary>
    ReadOnly Property IsHelperShape As Boolean
    ReadOnly Property ShapeBones As IReadOnlyList(Of NiNode)
    ReadOnly Property ShapeBoneTransforms As IReadOnlyList(Of Transform_Class)
    ReadOnly Property ShapeMaterial As Nifcontent_Class_Manolo.RelatedMaterial_Class
    ReadOnly Property IsSkinned As Boolean
    ReadOnly Property HasPhysics As Boolean

    ' Display flags
    Property ShowTexture As Boolean
    Property ShowMask As Boolean
    Property ShowWeight As Boolean
    Property ShowVertexColor As Boolean
    Property RenderHide As Boolean
    Property Wireframe As Boolean
    Property Wirecolor As Color
    Property WireAlpha As Single
    Property TintColor As Color
    Property ApplyZaps As Boolean
    ''' <summary>Worn biped-slot mask of the actor wearing this shape — bit (N-30) = biped slot N,
    ''' the SAME convention as <c>SlotConflictResolver.OccupiedSlots</c>. 0 = no per-segment occlusion
    ''' (the default; e.g. Wardrobe_Manager never sets it, so its render is unaffected). Read by the
    ''' render per-segment index filter (EnsureZapIndexBuffer → BSTriShapeGeometry.ComputeHiddenTriangles).</summary>
    Property CoveredSlotsMask As UInteger
    ''' <summary>This shape's OWN worn biped-slot mask (bit N-30 = slot N) = the item's BOD2 footprint.
    ''' Lets the per-segment filter tell a SELF-tagged segment (slot the item occupies → hide-if-covered,
    ''' engine self-exclude/occluder-order) from a FOREIGN one (slot the item does NOT occupy → the engine
    ''' coverage-key companion branch, inverse polarity). 0 = unknown owner ⇒ the foreign branch is skipped
    ''' (current behaviour). Only NpcRenderHost sets it, on FO4 worn items; WM/SSE leave it 0.</summary>
    Property OwnSlotsMask As UInteger
    Property MaskedVertices As HashSet(Of Integer)
    ''' <summary>Render-only extra material layers drawn as coplanar decals over this shape's deformed
    ''' geometry (LooksMenu overlays/tattoos). Nothing/empty = no overlay (the default; WM never sets
    ''' it). Drawn in list order = draw order (the app pre-sorts by LooksMenu priority ascending, so
    ''' higher priority ends up on top); the lib just honors the order.</summary>
    Property OverlayLayers As IReadOnlyList(Of OverlayMaterialLayer)
End Interface
