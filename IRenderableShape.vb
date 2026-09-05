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

    ''' <summary>El autor pidió CONSERVAR las normales de esta shape: no se recalculan.
    ''' <para>⛔ SYNC: <c>SliderSet.cpp:255-257</c> lo lee del atributo <c>LockNormals</c> del
    ''' <c>&lt;Shape&gt;</c>, y <c>BodySlideApp.cpp</c> lo consume en sus cuatro sitios de build con la misma
    ''' forma: <c>if (!lockNormals) CalcNormalsForShape(shape, force, smoothSeamNormals);</c> — y
    ''' <c>CalcTangentsForShape</c> FUERA del <c>if</c>. Es decisión POR SHAPE, no una casilla global.</para>
    ''' <para>Default <b>False</b>, el del canónico. MEDIDO: 41 shapes del corpus lo traen en true (37 de
    ''' FO4, 4 de SSE), y a todas se les recalculaban normales que el autor había hecho a mano.</para>
    ''' <para>Vive acá, en la interfaz, y no como parámetro: la misma función la llaman el build
    ''' (<c>BuildingForm</c>) y el PREVIEW (<c>Render.vb</c>), y un parámetro que el preview se olvide de
    ''' pasar hace que lo que ves y lo que se construye dejen de coincidir — justo lo que la regla
    ''' RENDER == BAKE del workspace prohíbe.</para></summary>
    ReadOnly Property LockNormals As Boolean

    ''' <summary>El autor pidió promediar (o NO promediar) las normales de la costura de esta shape.
    ''' <para>⛔ SYNC: atributo <c>SmoothSeamNormals</c> del <c>&lt;Shape&gt;</c>, tercer argumento de
    ''' <c>CalcNormalsForShape</c>. Default <b>True</b>, el del canónico. MEDIDO: 8 shapes de FO4 lo traen
    ''' en false, y promediarles la costura les borra los cantos duros del metal y las gemas.</para>
    ''' <para>⚠️ El atributo hermano <c>SmoothSeamNormalsAngle</c> (8 casos, todos en SSE) es INERTE para
    ''' el build y por eso no está acá: el canónico llama <c>CalcNormalsForShape</c> con 3 argumentos y el
    ''' umbral queda en el default <c>60.0f</c> (<c>NifFile.hpp:566-571</c>). Sólo lo consume el preview de
    ''' Outfit Studio.</para></summary>
    ReadOnly Property SmoothSeamNormals As Boolean

    ''' <summary>Geometría auxiliar que el MOTOR no dibuja nunca. Predicado del canónico
    ''' (BodySlide/OutfitStudio, <c>GLSurface.cpp</c>): <c>!shader || (shape-&gt;flags &amp; 1) != 0</c>,
    ''' con el comentario de <c>bHelperShape</c> en <c>Mesh.h</c>: "true for shapes with no shader or with
    ''' the hidden flag set (e.g. collisions)". Ellos lo exponen con la casilla "Show Helper Shapes".
    ''' <para>Las dos señales: <b>(a)</b> sin <c>BSShaderProperty</c> — así marca HDT-SMP sus proxies de
    ''' colisión (<c>VirtualHairCollision_*</c>), vanilla la colisión Havok (<c>SKY_HAV_MAT_*</c> en los
    ''' <c>*_col.nif</c>), los <c>EditorMarker</c> y los volúmenes de emisor; <b>(b)</b> bit 0 de
    ''' <c>NiAVObject.Flags</c> = Hidden (NifSkope <c>spells/flags.cpp</c>; BodySlide
    ''' <c>BodySlideApp.cpp</c>, <c>shape-&gt;flags |= 1</c>) — la sangre de las armas, el
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
    ''' <summary>Estado del slot <b>occluder</b> de este actor, que el resolver <c>0x14035E3B0</c> calcula
    ''' una sola vez y NO saca de <see cref="CoveredSlotsMask"/>: arranca en 0
    ''' (<c>0x14035E418 xor bpl,bpl</c>) y se enciende sólo si <c>table[D]+0x28 ≠ table[D]+0x10</c>
    ''' (<c>0x14035E473</c> + <c>0x14035E47D cmovne</c>), detrás de <c>0x14035E45E cmp eax,-1 / je</c>.
    ''' <para><c>+0x28</c> es el ARMA y <c>+0x10</c> el ARMO, pero el writer <c>0x1403597E0</c> escribe el
    ''' ARMO en UN SOLO slot: barre ascendente y el one-shot <c>0x1403599CA xor bl,bl</c> /
    ''' <c>0x140359B15 mov bl,1</c> se consume en el PRIMER slot que el ARMA ganó y el ARMO también declara.
    ''' ⇒ True ⟺ el ganador del slot occluder registró SU ARMO ahí, o sea
    ''' <c>D = min(ARMO.BOD2 ∩ ARMA.BOD2 ∩ ganados)</c>. Slot vacío ⇒ los dos punteros nulos ⇒ False.</para>
    ''' <para>⛔ NO es identidad contra los default objects del Pipboy: el resolver no consulta ningún DFOB.
    ''' La ley estructural resuelve sola el uniforme que declara el 60 de incidente (su primer slot
    ''' compartido es el 33, así que en el 60 le queda el ARMA ⇒ su antebrazo-60 sigue visible).</para>
    ''' False (el default) = sin dispositivo ⇒ el segmento del slot occluder se dibuja y su variante
    ''' <c>+130</c> no, que es lo que WM necesita al previsualizar una prenda sin actor.</summary>
    Property OccluderConDispositivo As Boolean
    ''' <summary>Bit (N−30) del biped slot que la RACE del actor reserva como <b>occluder</b>. En Fallout 4
    ''' sale de <c>RACE.DATA</c> "Pipboy Biped Object" (<c>race+0x200</c>, leído por
    ''' <c>Fallout4.exe 0x1404FCEC0</c>) y gobierna DOS ramas del resolver per-segmento
    ''' <c>0x14035E3B0</c>: la rama occluder-order (<c>0x14035E4F1</c>, que saca al slot de la comparación
    ''' de clave de cobertura) y el <b>swap N+100</b> del post-loop (<c>0x14035E65C</c>), que el motor aplica
    ''' <b>sólo</b> al tag <c>occluder+130</c> y a ningún otro de la banda 130-161.
    ''' <para>⛔ NO es la constante 60: el motor lo lee de la raza. Medido sobre el load order, 15 razas
    ''' declaran 30 (⇒ biped slot 60), 3 declaran 0 (⇒ biped slot <b>30</b>: FeralGhoul y sus variantes) y 97
    ''' declaran None. 0 acá = la raza no reserva ninguno ⇒ no hay rama occluder y la banda 130-161 queda
    ''' intacta, que es lo que hace el motor cuando el campo vale −1.</para>
    ''' <para>Skyrim no tiene este campo en su layout de <c>RACE.DATA</c> ⇒ 0.</para></summary>
    Property OccluderSlotMask As UInteger
    ''' <summary>True cuando esta shape se resuelve por el camino de <b>worn items</b>; False = camino de
    ''' <b>head parts</b>. Los dos caminos del motor son disjuntos y su valor por DEFECTO es OPUESTO, así que
    ''' el filtro per-partición necesita saber en cuál está:
    ''' <list type="bullet">
    ''' <item>head parts (<c>SkyrimSE.exe ApplyOcclusionToGeometry 0x1403CC770</c>):
    ''' <c>visible = (folded == 30+B) ? !hide : 1</c> ⇒ toda partición que no sea la del slot de pelo se
    ''' <b>fuerza visible</b>, incluidas las que caen fuera de [30,61].</item>
    ''' <item>worn items (<c>SkyrimSE.exe 0x14021DAE0</c> fase 1): la visibilidad nace en <b>0</b> y una
    ''' partición cuyo slot plegado cae fuera de [30,61] no pasa por ninguna rama que la encienda
    ''' (<c>lea eax,[rdi-0x1e] ; cmp ax,0x1f ; ja</c>) ⇒ queda <b>oculta</b>.</item>
    ''' </list>
    ''' False (el default) = semántica de head part, que es la que WM necesita: previsualiza una prenda
    ''' suelta, sin actor ni dueño de slot, y ahí ninguna partición debe desaparecer sola.</summary>
    Property OcclusionAsWornItem As Boolean
    Property MaskedVertices As HashSet(Of Integer)
    ''' <summary>Render-only extra material layers drawn as coplanar decals over this shape's deformed
    ''' geometry (LooksMenu overlays/tattoos). Nothing/empty = no overlay (the default; WM never sets
    ''' it). Drawn in list order = draw order (the app pre-sorts by LooksMenu priority ascending, so
    ''' higher priority ends up on top); the lib just honors the order.</summary>
    Property OverlayLayers As IReadOnlyList(Of OverlayMaterialLayer)
End Interface
