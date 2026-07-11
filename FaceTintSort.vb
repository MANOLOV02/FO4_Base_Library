''' <summary>
''' Orden de composición configurable de tints y swaps de FaceTint. Reglas multi-clave (clave +
''' dirección) que el builder (FaceTintLayerBuilder) aplica como sort estable. El orden producido es el
''' orden de COMPOSICIÓN: el 1º compone = fondo; el último = encima (over-running).
''' Vive en la librería (no en la app) sólo para que Config_App lo serialice; el mapeo clave→valor y el
''' sort viven en el builder de FO4_NPC_Manager (las claves son conceptos de RACE/NPC).
''' Default (constructor de FaceTintSortSettings) = el comportamiento previo (tints PhysIndex desc,
''' swaps orden de build/forward), así no cambia nada salvo que el usuario edite las reglas.
''' </summary>

''' <summary>Claves de orden para TINTS. El valor numérico es el id serializado (apéndice: no reordenar).</summary>
Public Enum FaceTintSortKey
    ' (valor 0 = PhysIndex, ELIMINADO: era redundante = GroupIndex + OptionInGroup. Hueco a propósito
    '  para no renumerar y no romper configs guardados. Una regla vieja con Key=0 queda inerte.)
    Group_Index = 1         ' índice del TintTemplateGroup
    Option_Index = 2      ' posición dentro del grupo
    Template_Index = 3               ' tl.Index (índice numérico de la option / TETI)
    Npc_List_Order = 4       ' orden en NPC.FaceTintLayers (tiebreak estable default, asc)
    Slot = 5               ' opt.Slot (TintSlot: SkinTone=12, Scars=21, Brow=23...)
    Entry_Type = 6          ' Discriminator: 1=Palette/Mask, 2=TextureSet
    Blend_Operation = 7            ' blendop resuelto (0 Default..4 HardLight, 5+ estándar)
    Opacity = 8           ' tl.Value (intensidad 0-100)
    Flag_OnOffOnly = 9      ' TTEF 0x1
    Flag_ChargenDetail = 10 ' TTEF 0x2
    Flag_TakesSkinTone = 11 ' TTEF 0x4
    Template_ColorIndex = 12 ' TEND ColorID
    Category_Index = 13     ' TTGE Category Index del grupo de la option (xEdit: TTGE 'Category Index')
End Enum

''' <summary>Claves de orden para SWAPS (region swaps MPPT).</summary>
Public Enum FaceTintSwapSortKey
    Group_Index = 0         ' orden físico del MorphGroup (default forward)
    Preset_Index = 1      ' orden del preset dentro del grupo (default forward)
    Morph_Index = 2   ' p.Index (MSDV)
    Slot = 3           ' TintSlot del mask del grupo (Forehead/Eyes/.../Neck = 0..6)
    Intensity = 4          ' MSDV del NPC (0..1)
    Npc_Lits_Order = 5           ' orden del morph dentro del NPC (MorphValues)
End Enum

''' <summary>Claves de orden para los TINTS de SSE (capas de tint del RACE). Estructura DISTINTA a FO4:
''' una capa SSE es (TINI index, TINP type, TIND default CLFM, TINV coverage) y el orden RaceMenu = la
''' posición en el RACE (array tintMasks; RaceMenu NO reordena, override por índice — PresetInterface.cpp).
''' Default = [Race_Order asc] = identidad = orden RaceMenu (byte-idéntico a hoy). El valor es el id serializado.</summary>
Public Enum FaceTintSseTintSortKey
    Race_Order = 0    ' posición en el RACE (= array tintMasks / cb2 slot order) — DEFAULT, RaceMenu-fiel
    Tint_Index = 1    ' TINI (índice de la capa)
    Mask_Type = 2     ' TINP (tipo de máscara)
    Authored = 3      ' el NPC autoró esta capa (TINI/TINC/TINV) vs default del RACE
    Coverage = 4      ' TINV (cobertura 0-1) resuelta (authored o default)
End Enum

''' <summary>Claves de orden para los OVERLAYS de SSE (Face[Ovl], = análogo SSE de los SWAPS de FO4). El
''' orden skee/RaceMenu = el índice del nodo Ovl{n} ascendente (OverlayInterface for i=0..N). Default =
''' [Ovl_Index asc] = identidad = orden skee (byte-idéntico a hoy).</summary>
Public Enum FaceTintSseOverlaySortKey
    Ovl_Index = 0     ' índice del nodo Ovl{n} — DEFAULT, orden skee/RaceMenu
    Alpha = 1         ' opacidad (key8 / .Alpha)
    Has_Tint = 2      ' el overlay lleva tint (color) vs solo textura
End Enum

''' <summary>Placement especial del SkinTone (slot 12) en el orden de composición de tints. Gana sobre
''' las reglas: FirstOfAll/LastOfAll sacan la capa slot-12 del orden y la fuerzan al frente/final del
''' compose-list. Positional la deja donde caiga por las reglas (default).</summary>
Public Enum FaceTintSkinTonePlacement
    Positional = 0   ' por su posición en el orden (default)
    FirstOfAll = 1   ' compone ANTES que todos los otros tints (queda al fondo)
    LastOfAll = 2    ' compone DESPUÉS de todos (queda encima)
End Enum

''' <summary>Una regla de orden: clave (id del enum del dominio correspondiente) + dirección.</summary>
Public Class FaceTintSortRule
    Public Property Key As Integer
    Public Property Descending As Boolean
End Class

''' <summary>Config completa de orden, persistida en Config_App. Default = comportamiento previo:
''' tints = [PhysIndex desc] (+ tiebreak estable NpcListOrder asc implícito en el builder);
''' swaps = [] (vacío = orden de build/forward = grupo×preset asc); skintone = Positional.</summary>
Public Class FaceTintSortSettings
    Public Property TintRules As List(Of FaceTintSortRule)
    Public Property SwapRules As List(Of FaceTintSortRule)
    Public Property SkinTonePlacement As Integer   ' FaceTintSkinTonePlacement

    Public Sub New()
        ' Default = orden previo (= el viejo PhysIndex desc): GroupIndex desc, luego OptionInGroup desc.
        TintRules = New List(Of FaceTintSortRule) From {
            New FaceTintSortRule With {.Key = CInt(FaceTintSortKey.Group_Index), .Descending = True},
            New FaceTintSortRule With {.Key = CInt(FaceTintSortKey.Option_Index), .Descending = True}
        }
        ' Default explícito = forward (= orden de build): GroupIndex asc, luego PresetInGroup asc. Idéntico
        ' en resultado a una lista vacía, pero VISIBLE en la UI (el vacío no mostraba nada).
        SwapRules = New List(Of FaceTintSortRule) From {
            New FaceTintSortRule With {.Key = CInt(FaceTintSwapSortKey.Group_Index), .Descending = False},
            New FaceTintSortRule With {.Key = CInt(FaceTintSwapSortKey.Preset_Index), .Descending = False}
        }
        SkinTonePlacement = CInt(FaceTintSkinTonePlacement.Positional)
    End Sub

    ''' <summary>Default SSE (RaceMenu-fiel): tints = [Race_Order asc] (= orden del RACE / array tintMasks),
    ''' overlays (SwapRules) = [Ovl_Index asc] (= orden skee Ovl{n}), skintone = Positional. Ambas listas son
    ''' IDENTIDAD ⇒ el compose queda byte-idéntico al orden RaceMenu actual (la baseline vanilla no se mueve).
    ''' Las CLAVES se interpretan como <see cref="FaceTintSseTintSortKey"/> / <see cref="FaceTintSseOverlaySortKey"/>
    ''' (no como las FO4). Set SEPARADO (Setting_FaceTintSort_SSE) para no tocar el de FO4.</summary>
    Public Shared Function DefaultsForSse() As FaceTintSortSettings
        Return New FaceTintSortSettings With {
            .TintRules = New List(Of FaceTintSortRule) From {
                New FaceTintSortRule With {.Key = CInt(FaceTintSseTintSortKey.Race_Order), .Descending = False}},
            .SwapRules = New List(Of FaceTintSortRule) From {
                New FaceTintSortRule With {.Key = CInt(FaceTintSseOverlaySortKey.Ovl_Index), .Descending = False}},
            .SkinTonePlacement = CInt(FaceTintSkinTonePlacement.Positional)
        }
    End Function
End Class
