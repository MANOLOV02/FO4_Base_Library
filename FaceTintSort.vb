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
    GroupIndex = 1         ' índice del TintTemplateGroup
    OptionInGroup = 2      ' posición dentro del grupo
    Teti = 3               ' tl.Index (índice numérico de la option / TETI)
    NpcListOrder = 4       ' orden en NPC.FaceTintLayers (tiebreak estable default, asc)
    Slot = 5               ' opt.Slot (TintSlot: SkinTone=12, Scars=21, Brow=23...)
    EntryType = 6          ' Discriminator: 1=Palette/Mask, 2=TextureSet
    BlendOp = 7            ' blendop resuelto (0 Default..4 HardLight, 5+ estándar)
    Opacity = 8           ' tl.Value (intensidad 0-100)
    FlagOnOffOnly = 9      ' TTEF 0x1
    FlagChargenDetail = 10 ' TTEF 0x2
    FlagTakesSkinTone = 11 ' TTEF 0x4
    TemplateColorIndex = 12 ' TEND ColorID
    CategoryIndex = 13     ' TTGE Category Index del grupo de la option (xEdit: TTGE 'Category Index')
End Enum

''' <summary>Claves de orden para SWAPS (region swaps MPPT).</summary>
Public Enum FaceTintSwapSortKey
    GroupIndex = 0         ' orden físico del MorphGroup (default forward)
    PresetInGroup = 1      ' orden del preset dentro del grupo (default forward)
    PresetMorphIndex = 2   ' p.Index (MSDV)
    MaskSlot = 3           ' TintSlot del mask del grupo (Forehead/Eyes/.../Neck = 0..6)
    Intensity = 4          ' MSDV del NPC (0..1)
    NpcOrder = 5           ' orden del morph dentro del NPC (MorphValues)
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
            New FaceTintSortRule With {.Key = CInt(FaceTintSortKey.GroupIndex), .Descending = True},
            New FaceTintSortRule With {.Key = CInt(FaceTintSortKey.OptionInGroup), .Descending = True}
        }
        ' Default explícito = forward (= orden de build): GroupIndex asc, luego PresetInGroup asc. Idéntico
        ' en resultado a una lista vacía, pero VISIBLE en la UI (el vacío no mostraba nada).
        SwapRules = New List(Of FaceTintSortRule) From {
            New FaceTintSortRule With {.Key = CInt(FaceTintSwapSortKey.GroupIndex), .Descending = False},
            New FaceTintSortRule With {.Key = CInt(FaceTintSwapSortKey.PresetInGroup), .Descending = False}
        }
        SkinTonePlacement = CInt(FaceTintSkinTonePlacement.Positional)
    End Sub
End Class
