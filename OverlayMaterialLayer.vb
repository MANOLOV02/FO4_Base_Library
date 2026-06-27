''' <summary>
''' One render-only material layer drawn as a coplanar decal over a shape's ALREADY-deformed
''' (morphed + skinned) geometry — the LooksMenu "body overlays"/tattoos model: the engine clones
''' the base skin shape, shares its skinInstance/geometry, and draws a different material on top
''' (F4SEPlugins-master/f4ee/OverlayInterface.cpp:69-160, 162-233).
'''
''' The APP loads <see cref="Material"/> from the overlay template's material path and PRE-BAKES the
''' LooksMenu offsetUV (into UOffset/VOffset) and tint (into BaseColor) before constructing the layer,
''' so the lib just renders the supplied material — no UV/tint fields live here on purpose.
''' </summary>
Public Class OverlayMaterialLayer
    ''' <summary>The overlay material (BGEM effect-shader for tattoos, or BGSM), already loaded and
    ''' fully configured by the app. Same wrapper type the base shape carries
    ''' (Nifcontent_Class_Manolo.RelatedMaterial_Class), so it flows through MaterialData unchanged.</summary>
    Public Property Material As Nifcontent_Class_Manolo.RelatedMaterial_Class
End Class
