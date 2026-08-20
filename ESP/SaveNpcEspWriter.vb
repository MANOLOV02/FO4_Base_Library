Imports System.IO
Imports System.Linq
Imports System.Text
Imports FO4_Base_Library.Canon.CanonInterpretacion

' Save NPC ESP/ESM — emite un plugin de Bethesda con uno o más overrides de NPC_, con limpieza de
' masters: se descartan los masters que ya no tienen ninguna referencia.
'
' Secuencia: se recolectan los FormID que referencia cada override y se mapean a (plugin origen, ObjectID
' local) → se arma la MAST list nueva (el master del juego siempre primero, después todo plugin que sea
' dueño de algún FormID referenciado, ordenados por load order) → con eso se arma el remapper
' (global → (nuevo índice MAST << 24) | ObjectID) → se serializa cada NPC_ con ese remapper, se envuelven
' en un GRUP y se emite el TES4. La escritura es atómica: .tmp + fsync + rename.
'
' Al actualizar un plugin existente, los records ajenos se re-emiten VERBATIM, pero sus FormID y los
' embebidos dentro de ellos se remapean contra la MAST list NUEVA — que es justamente lo que hace
' CleanMasters cuando la lista de masters se corre.

Public Module SaveNpcEspWriter

    ''' <summary>Traduce un identificador del espacio del ORDEN DE CARGA al del archivo que se
    ''' esta escribiendo: byte alto = indice en la MAST nueva, 24 bits bajos = el ObjectID de
    ''' siempre. Lo arma quien escribe el plugin, que es el unico que conoce esa lista.</summary>
    Public Delegate Function FormIdRemapper(globalFormID As UInteger) As UInteger

    ''' <summary>One NPC_ override to write. Caller provides the type-safe parse model
    ''' and the source plugin name (for FormID master resolution).</summary>
    Public Class NpcOverrideEntry
        ''' <summary>El NPC_ a grabar. Su <see cref="NPC_Data.Record"/> ES el cuerpo: lo que el
        ''' usuario no toco se reproduce solo, incluidos los bytes que el formato declara como
        ''' relleno. Obligatorio.</summary>
        Public Npc As NPC_Data
        ''' <summary>Plugin name (with extension, e.g. "Fallout4.esm") that originally
        ''' DEFINES this NPC. Used to resolve the FormID's source master and emit the
        ''' record FormID on the override side.</summary>
        Public SourcePluginName As String
        ''' <summary>Original record header (Flags, VCS, Version) — preserved verbatim
        ''' when re-emitting except for DataSize (recomputed) and the IsCompressed flag
        ''' which we always strip (we emit uncompressed bodies for tooling friendliness).</summary>
        Public OriginalHeader As RecordHeader
    End Class

    ''' <summary>One NPC_ NEW record (not override) to write into the plugin. The writer assigns a real
    ''' self-index FormID via draftRemap exactly like NEW <see cref="OtftRecordEntry"/> / <see cref="LvliRecordEntry"/>:
    ''' the caller hands a PROVISIONAL sentinel (high byte 0xFF) in <see cref="ProvisionalFormID"/>; the writer
    ''' rewrites every reference to it (including the record's own header FormID) through the single remapper.
    ''' Use case: cloning a vanilla NPC into N variants with distinct FormIDs (debug tools, derivation experiments,
    ''' or future "Duplicate NPC" features in NPC_Manager). The caller fully populates <see cref="NpcData"/> with
    ''' the modifications (EditorID, TINI/TINC/TIAS for tint intensity, MSDV for morph values, etc.) BEFORE passing.
    ''' Record header values (Flags, VCS1, Version, VCS2) are emitted as defaults — see SerializeNpcCreateRecord.
    ''' All FormID references inside NpcData (RNAM, HEAD parts, factions, etc.) are remapped against the new
    ''' MAST list — the master discovery walk includes creates so referenced plugins land in the MAST list.</summary>
    Public Class NpcCreateEntry
        ''' <summary>El NPC_ a grabar. Su <see cref="NPC_Data.Record"/> ES el cuerpo, y quien clona lo
        ''' trae del original: asi el clon conserva todo lo que ningun editor toca (datos de IA, ataques,
        ''' sonidos). Sin record el NPC_ arranca con los campos que el formato marca como obligatorios y
        ''' solo lleva lo que la aplicacion escriba.</summary>
        Public NpcData As NPC_Data
        ''' <summary>Provisional sentinel FormID (high byte 0xFF). Used by the writer to allocate a real
        ''' self-index FormID and remap every reference to this NPC. Each create entry must have a unique
        ''' provisional FormID (caller responsibility — typically counter-based: 0xFFFFFFFF, 0xFFFFFFFE, ...).</summary>
        Public ProvisionalFormID As UInteger
        ''' <summary>Plugin name (with extension, e.g. "Fallout4.esm") used as the "source" context for FormID
        ''' master resolution of references INSIDE the cloned NpcData (RNAM=race, HDPT head parts, etc.).
        ''' Typically the plugin that owns the base NPC being cloned. Optional: defaults to game master if empty.</summary>
        Public BaseSourcePluginName As String = ""
    End Class

    ''' <summary>One OTFT (outfit) record to write into the same plugin as the NPC override(s).
    ''' Authored in the Edit Outfit "Create" tab. Two flavours:
    '''   • NEW (IsOverride=False): a brand-new outfit owned by this plugin. <see cref="FormID"/> is
    '''     the caller's PROVISIONAL sentinel (high byte 0xFF); the writer assigns the real plugin
    '''     self-index FormID ((masterCount &lt;&lt; 24) | objIndex, objIndex≥0x800, the Creation Kit's
    '''     convention for new records) and remaps
    '''     every reference to it (notably the NPC.DOFT that points at the provisional).
    '''   • OVERRIDE (IsOverride=True): edit of an existing OTFT keeping its EditorID. <see cref="FormID"/>
    '''     is that record's real global FormID; emitted as an override (master index remapped).
    ''' Body = EDID + INAM (array of ARMO/LVLI FormIDs, remapped against the new MAST list).</summary>
    Public Class OtftRecordEntry
        ''' <summary>El record a grabar. De acá sale todo el cuerpo.</summary>
        Public Record As Canon.CanonView

        ''' <summary>New: provisional sentinel (0xFF…). Override: the existing OTFT's real global FormID.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        Public ItemArmoFormIDs As New List(Of UInteger)
        Public IsOverride As Boolean
        ''' <summary>VCS1/VCS2 preserved from the source record on preserve-existing overrides — kept
        ''' verbatim so a re-save doesn't bump the version counters CK uses for conflict detection.
        ''' Defaults to zero for NEW drafts (no source).</summary>
        Public OriginalVcs1 As UInteger
        Public OriginalVcs2 As UShort
    End Class

    ''' <summary>One LVLI (leveled item) record to write into the same plugin — a leveled list authored in
    ''' the Edit Outfit editor's "New LVL…" flow. ALWAYS new (owned by this plugin): <see cref="FormID"/>
    ''' is the caller's PROVISIONAL sentinel (high byte 0xFF), assigned a real self-index FormID by the
    ''' writer exactly like a NEW <see cref="OtftRecordEntry"/>. Because every draft (OTFT + LVLI) is pre-
    ''' assigned its real FormID in <c>draftRemap</c> BEFORE any record is serialized, references between
    ''' drafts resolve through the single remapper regardless of emit order — an OTFT.INAM pointing at a
    ''' draft LVLI, and a draft LVLI's LVLO pointing at another draft LVLI, both rewrite to the real
    ''' self-index FormIDs. Body layout: EDID + OBND + LVLD + LVLM + LVLF +
    ''' LLCT + N×LVLO (12 bytes each).</summary>
    Public Class LvliRecordEntry
        ''' <summary>El record a grabar. De acá sale todo el cuerpo -EDID/OBND/LVLD/LVLM/LVLF/LLCT/entradas
        ''' y, en un LVLI, LLKC/LVSG/ONAM; en un LVLN, el generic model-. Los demás campos de esta clase
        ''' siguen poblados para el indexado que hace NpcOverrideSaver (que ARMO referencia una entrada,
        ''' deduplicado de NPC ya nivelados, etc.); si se tocan tienen que escribirse tambien acá.</summary>
        Public Record As Canon.CanonView

        ''' <summary>NEW: provisional sentinel (0xFF…), rewritten to the real self-index FormID by the writer.
        ''' OVERRIDE: the existing LVLI's real global FormID (master-remapped on emit), e.g. an LVLI authored in
        ''' a prior save and re-preserved when updating the same plugin.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        ''' <summary>OBND raw 12 bytes (6×s16). Set from <see cref="LVLI_Data.ObjectBoundsRaw"/> on preserve-existing
        ''' overrides so the writer preserves the source-LVLI's bounds verbatim. NEW drafts leave it Nothing →
        ''' writer emits 12 zero bytes (still valid per spec).</summary>
        Public ObjectBoundsRaw As Byte() = Nothing
        ''' <summary>LVLD — whole-list chance of yielding nothing (0-100).</summary>
        Public ChanceNone As Byte
        ''' <summary>LVLM — Max Count (0 = unlimited).</summary>
        Public MaxCount As Byte
        ''' <summary>LVLF — packed flag byte (0x01 all-levels, 0x02 each-in-count, 0x04 use-all).</summary>
        Public Flags As Byte
        Public Entries As New List(Of LvliEntryData)
        ''' <summary>True = override an existing LVLI (keep its real FormID + EditorID). False = brand-new
        ''' (draft) list assigned a self-index FormID.</summary>
        Public IsOverride As Boolean
        ''' <summary>LVLG — Use Global, FormID [GLOB]. Optional.
        ''' Set together with <see cref="UseGlobalFormID"/> on preserve-existing overrides.</summary>
        Public HasUseGlobal As Boolean
        Public UseGlobalFormID As UInteger
        ''' <summary>LLKC — Filter Keyword Chances. Re-emitted
        ''' on preserve-existing overrides. NEW drafts authored in-app leave this empty.</summary>
        Public FilterKeywords As New List(Of LvliFilterKeywordData)
        ''' <summary>LVSG — Epic Loot Chance, FormID [GLOB]. Optional.</summary>
        Public HasEpicLootChance As Boolean
        Public EpicLootChanceFormID As UInteger
        ''' <summary>ONAM — Override Name, translatable lstring.
        ''' Emitted via the central translatable encoder so users with non-ASCII locales keep characters.</summary>
        Public HasOverrideName As Boolean
        Public OverrideName As String = ""
        ''' <summary>VCS1/VCS2 preserved from the source record on preserve-existing overrides. See
        ''' <see cref="OtftRecordEntry.OriginalVcs1"/> for rationale.</summary>
        Public OriginalVcs1 As UInteger
        Public OriginalVcs2 As UShort
        ''' <summary>True = emit as LVLN (Leveled NPC) instead of LVLI.
        ''' El HEAD del body coincide (EDID/OBND/LVLD/LVLM/LVLF/LVLG/LLCT/N×(LVLO+COED)/LLKC), pero el
        ''' TAIL difiere: LVLN termina con un generic model (<see cref="ModelSubrecords"/>) y NO lleva
        ''' LVSG/ONAM; LVLI lleva LVSG+ONAM y NO model. Cada LVLO de una LVLN referencia un NPC_/LVLN
        ''' FormID. En este writer, LVLN va antes que LVLI en el orden de emision de los GRUP.</summary>
        Public IsNpcList As Boolean = False
        ''' <summary>LVLN-only generic model subrecords (MODL/MODT/MODC/MODS/MODF),
        ''' preserved verbatim in source order for byte-equivalent round-trip.
        ''' This is the real divergence between the LVLN and LVLI bodies: LVLN's tail is a model, LVLI's is
        ''' LVSG+ONAM. The MODS bytes hold the GLOBAL Material Swap FormID ([MSWP]),
        ''' remapped on emit; every other model subrecord is FormID-free. Empty for LVLI and for typical
        ''' leveled-NPC lists (which carry no model).</summary>
        Public ModelSubrecords As New List(Of (Signature As String, Data As Byte()))
    End Class

    ''' <summary>One LVLO entry inside an <see cref="LvliRecordEntry"/>. The reference is an ARMO (real),
    ''' a vanilla LVLI (real), or another draft LVLI (provisional — remapped via draftRemap). May carry a
    ''' trailing COED with per-entry Owner/Rank metadata.</summary>
    Public Class LvliEntryData
        Public Level As UShort = 1
        Public RefFormID As UInteger
        Public Count As UShort = 1
        Public ChanceNone As Byte
        ''' <summary>True when the entry carries a COED. Mirror of NPC_InventoryItem COED fields.</summary>
        Public HasCoed As Boolean
        Public CoedOwnerFormID As UInteger
        ''' <summary>COED +4 union: GLOB FormID when Owner=NPC_ (CoedExtraIsFormID=True), Required Rank
        ''' s32 when Owner=FACT, unused bytes otherwise. Same conditional-remap rule as NPC_ inventory.</summary>
        Public CoedOwnerExtra As UInteger
        Public CoedExtraIsFormID As Boolean
        Public CoedItemCondition As Single
    End Class

    ''' <summary>One LLKC filter-keyword chance pair re-emitted on preserve-existing LVLI overrides.</summary>
    Public Class LvliFilterKeywordData
        Public KeywordFormID As UInteger
        Public Chance As UInteger
    End Class

    ''' <summary>One MSWP (Material Swap) record to write into the plugin. NEW-only in this task:
    ''' <see cref="FormID"/> is the caller's PROVISIONAL sentinel (high byte 0xFF), assigned a real
    ''' self-index FormID by the writer (mirror of NEW <see cref="OtftRecordEntry"/>). Body order:
    ''' EDID + FNAM(Tree Folder, optional) + N×(BNAM 'Original Material' +
    ''' SNAM 'Replacement Material' + CNAM 'Color Remapping Index' optional). MSWP carries NO embedded
    ''' FormIDs in its body — only its own record FormID is remapped.</summary>
    Public Class MswpRecordEntry
        ''' <summary>El record a grabar. De acá sale TODO el cuerpo: el orden de los subrecords, qué
        ''' campos van y de qué tamaño es cada uno los decide la declaración del formato, no una
        ''' secuencia de llamadas escrita a mano.</summary>
        Public Record As Canon.CanonView

        ''' <summary>NEW: provisional sentinel (0xFF…). OVERRIDE (not implemented here): the existing
        ''' MSWP's real global FormID.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        ''' <summary>FNAM 'Tree Folder' (ZSTRING, first FNAM). Optional —
        ''' emitted only when non-empty.</summary>
        Public TreeFolder As String = ""
        Public Substitutions As New List(Of Canon.IMswp_MaterialSubstitutions)
        Public IsOverride As Boolean = False
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
        ''' <summary>The original parsed source record (required when <see cref="IsOverride"/>=True). On
        ''' override the record's own FormID = the real GLOBAL FormID (caller sets <see cref="FormID"/>);
        ''' header flags/Version come from <c>SourceRecord.Header</c>. MSWP has no body FormIDs and a simple
        ''' body, so its override just re-emits the entry with the source flags/Version — no subrecord merge
        ''' (every owned field already holds the final desired state).</summary>
        Public SourceRecord As PluginRecord = Nothing
    End Class

    ''' <summary>Un record CLFM (Color) a escribir: es el vehiculo de persistencia del tinte de pelo absoluto
    ''' de RaceMenu en SSE (el .jslot guarda un RGB empaquetado, no una ref a CLFM). Dos sabores, como
    ''' <see cref="OtftRecordEntry"/>: NEW (IsOverride=False), donde <see cref="FormID"/> es el centinela
    ''' provisional del caller y el writer asigna el FormID self-index real y remapea toda referencia -entre
    ''' ellas el NPC_.HCLF-; y OVERRIDE, un CLFM autorado por un guardado previo de este mismo plugin, que se
    ''' re-emite para que un re-save no lo tire y deje colgados los HCLF que lo apuntan.
    ''' <para>Cuerpo: EDID + [FULL] + CNAM + FNAM. FULL es opcional y traducible, asi que viaja en DOS formas
    ''' (<see cref="FullName"/> autorado aca para NEW, <see cref="FullNameRaw"/> verbatim para OVERRIDE). CNAM
    ''' es un RGBA de 4 bytes [R,G,B,A]; medido sobre Skyrim.esm, los 178 CLFM llevan A=0 y los 15 de
    ''' pelo FNAM=1
    ''' (Playable), que son los valores que recibe un color sintetizado. El CLFM no lleva FormID en el cuerpo.</para>
    ''' <para>âš ï¸ SSE-ONLY por construccion (lo gatea el caller): un CLFM de pelo de FO4 lleva RemappingIndex en
    ''' vez de RGB. El writer en si se mantiene game-agnostic.</para></summary>
    Public Class ClfmRecordEntry
        ''' <summary>El record a grabar. De acá sale todo el cuerpo: EDID + [FULL] + CNAM + FNAM. Los
        ''' demás campos siguen poblados para el índice de reuso por color de
        ''' <c>NpcOverrideSaver.MaterializeSseHairColors</c>; si se tocan tienen que escribirse tambien acá.</summary>
        Public Record As Canon.CanonView

        ''' <summary>NEW: provisional sentinel (0xFF…). OVERRIDE: the existing CLFM's real global FormID.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        ''' <summary>FULL — optional display name, the string the CK / our own editor combo show
        ''' instead of the EditorID. NEW entries author it here (see NpcOverrideSaver.MaterializeSseHairColors:
        ''' "NPC Manager custom hair color #RRGGBB"); empty = no FULL emitted, which is what every CLFM this
        ''' writer produced before carried. ENCODING: emitted with <c>EncodeTranslatable</c> (FULL is a
        ''' translatable lstring — NOT the cp1252 General encoder that EDID uses),
        ''' and that encoder has an ExceptionFallback, so an authored name MUST be ASCII-only: this record is
        ''' not covered by the Phase 2b encoding-conflict pre-check (that one walks NPC_ FULL/SHRT/ATTX), and a
        ''' character the chosen codepage can't represent would throw mid-write instead of being reported.</summary>
        Public FullName As String = ""
        ''' <summary>OVERRIDE-only: the source FULL subrecord's payload copied VERBATIM (NUL included), so a
        ''' re-save round-trips the name byte-exactly instead of decoding it with the source plugin's encoding
        ''' and re-encoding it with the current global Translatable — a lossy step that could also throw
        ''' mid-write for a name authored under another codepage. Same reasoning as CNAM/FNAM being copied from
        ''' the bytes in the preservation sweep. Nothing = source had no FULL. Wins over <see cref="FullName"/>.</summary>
        Public FullNameRaw As Byte() = Nothing
        ''' <summary>Packed 0xRRGGBB. Emitted to CNAM as bytes R,G,B then <see cref="ColorAlpha"/>.</summary>
        Public ColorRgb As Integer
        ''' <summary>CNAM's 4th byte. Default 0 = what all 178 vanilla Skyrim.esm CLFM carry (measured).
        ''' Preserved verbatim from the source record on OVERRIDE entries.</summary>
        Public ColorAlpha As Byte = 0
        ''' <summary>FNAM. Skyrim: 'Playable' bool (1 on all 15 vanilla hair colours). FO4: a flag field.
        ''' Emitted verbatim as u32 either way.</summary>
        Public Flags As UInteger = 1UI
        Public IsOverride As Boolean = False
        ''' <summary>VCS1/VCS2 preserved from the source record on OVERRIDE entries. See
        ''' <see cref="OtftRecordEntry.OriginalVcs1"/>.</summary>
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
    End Class

    ''' <summary>One ARMA (Armor Addon) record to write. NEW: <see cref="FormID"/> es el centinela
    ''' provisional del caller (0xFF…), el writer le asigna el FormID self-index real. OVERRIDE: el
    ''' FormID real del record que se esta sobrescribiendo. Las banderas de cabecera (No Underarmor
    ''' Scaling / Has Sculpt Data / Hi-Res 1st Person Only) salen del árbol, no de esta clase.
    ''' </summary>
    Public Class ArmaRecordEntry
        ''' <summary>El record a grabar. De acá sale todo el cuerpo y la cabecera y, en un OVERRIDE,
        ''' los campos que el usuario no tocó: el árbol viene de leer la fuente, así que
        ''' reproducirlo alcanza sin un camino de preservación aparte.</summary>
        Public Record As Canon.CanonView

        Public FormID As UInteger
        Public EditorID As String = ""
        Public SlotMask As UInteger                 ' BOD2 (u32)
        Public RaceFormID As UInteger               ' RNAM
        Public FootstepSetFormID As UInteger        ' SNDD (FSTS)
        Public ArtObjectFormID As UInteger          ' ONAM (ARTO) — owned optional
        Public MaleFPMaterialSwapFormID As UInteger   ' MO4S (MSWP) — owned optional
        Public FemaleFPMaterialSwapFormID As UInteger ' MO5S (MSWP) — owned optional
        Public MalePriority As Byte = 0
        Public FemalePriority As Byte = 0
        Public MaleWeightSliderFlags As Byte = 0
        Public FemaleWeightSliderFlags As Byte = 0
        Public DetectionSoundValue As Byte = 0
        Public WeaponAdjust As Single = 0.0F
        Public MaleMeshPath As String = ""          ' MOD2
        Public FemaleMeshPath As String = ""        ' MOD3
        Public MaleFPMeshPath As String = ""        ' MOD4
        Public FemaleFPMeshPath As String = ""      ' MOD5
        Public MaleModelFlags As Byte = 0           ' MO2F
        Public FemaleModelFlags As Byte = 0         ' MO3F
        Public MaleFPModelFlags As Byte = 0         ' MO4F
        Public FemaleFPModelFlags As Byte = 0       ' MO5F
        Public MaleColorRemapIndex As Single? = Nothing   ' MO2C
        Public FemaleColorRemapIndex As Single? = Nothing ' MO3C
        Public MaleSkinTextureFormID As UInteger    ' NAM0 (TXST)
        Public FemaleSkinTextureFormID As UInteger  ' NAM1 (TXST)
        Public MaleSkinTextureSwapListFormID As UInteger   ' NAM2 (FLST)
        Public FemaleSkinTextureSwapListFormID As UInteger ' NAM3 (FLST)
        Public MaleMaterialSwapFormID As UInteger   ' MO2S (MSWP)
        Public FemaleMaterialSwapFormID As UInteger ' MO3S (MSWP)
        Public AdditionalRaces As New List(Of UInteger)   ' MODL array (RACE)
        Public BoneScaleData As New List(Of ARMA_BoneScaleGender)  ' BSMP/BSMB/BSMS
        Public NoUnderarmorScaling As Boolean = False   ' header flag bit 6
        Public HasSculptData As Boolean = False         ' header flag bit 9
        Public HiRes1stPersonOnly As Boolean = False    ' header flag bit 30
        Public IsOverride As Boolean = False
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
        ''' <summary>El record fuente sin abrir, para auditoría. En un OVERRIDE la cabecera (flags y
        ''' versión) y los campos no tocados por el usuario salen del árbol de <see cref="Record"/>
        ''' —que se abrió leyendo esta misma fuente—, no de este campo.</summary>
        Public SourceRecord As PluginRecord = Nothing
    End Class

    ''' <summary>One ARMO (Armor) record to write. NEW: <see cref="FormID"/> es el centinela
    ''' provisional del caller (0xFF…), el writer le asigna el FormID self-index real. OVERRIDE: el
    ''' FormID real del record que se esta sobrescribiendo.</summary>
    Public Class ArmoRecordEntry
        ''' <summary>El record a grabar. De acá sale todo el cuerpo y la cabecera y, en un OVERRIDE,
        ''' los campos que el usuario no tocó: el árbol viene de leer la fuente, así que
        ''' reproducirlo alcanza sin un camino de preservación aparte.</summary>
        Public Record As Canon.CanonView

        Public FormID As UInteger
        Public EditorID As String = ""
        Public FullName As String = ""              ' FULL (optional)
        Public SlotMask As UInteger                 ' BOD2
        Public RaceFormID As UInteger               ' RNAM
        Public InstanceNamingFormID As UInteger     ' INRD (INNR)
        Public EnchantmentFormID As UInteger        ' EITM (ENCH) — owned optional
        Public PatternFormID As UInteger            ' PTRN (TRNS) — owned optional
        Public EquipTypeFormID As UInteger          ' ETYP (EQUP) — owned optional
        Public PickupSoundFormID As UInteger        ' YNAM (SNDR) — owned optional
        Public DropSoundFormID As UInteger          ' ZNAM (SNDR) — owned optional
        Public AlternateBlockMaterialFormID As UInteger ' BAMT (MATT) — owned optional
        Public Description As String = ""           ' DESC (translatable) — owned optional
        ''' <summary>Si el DESC tiene que emitirse. Se copia de <c>ARMO_Data.HasDescription</c> (presencia en
        ''' el record fuente), NO de que el texto sea no vacío: en un master localizado el id puede resolver a
        ''' "" y el subrecord igual está. Ponerlo en False es la forma de decir "sacá la descripción".</summary>
        Public HasDescription As Boolean = False
        Public NonPlayable As Boolean = False       ' header flag bit 2 — owned
        ''' <summary>OBND — Object Bounds 6×i16 min/max XYZ (required, always emitted).</summary>
        Public ObndX1 As Short
        Public ObndY1 As Short
        Public ObndZ1 As Short
        Public ObndX2 As Short
        Public ObndY2 As Short
        Public ObndZ2 As Short
        ''' <summary>DAMA — Damage Type Array / Resistances (owned, omit block when empty).</summary>
        Public DamageResistances As New List(Of ARMO_DamageResist)
        Public TemplateArmorFormID As UInteger      ' TNAM (ARMO)
        Public ArmorAddons As New List(Of ARMO_AddonEntry)   ' Models: INDX + ArmaFormID
        Public KeywordFormIDs As New List(Of UInteger)       ' KWDA
        Public AttachParentSlotFormIDs As New List(Of UInteger)  ' APPR (KYWD)
        Public MaleWorldModelPath As String = ""    ' MOD2 (robots)
        Public FemaleWorldModelPath As String = ""  ' MOD4
        Public MaleMaterialSwapFormID As UInteger   ' MO2S at ARMO level (MSWP)
        Public FemaleMaterialSwapFormID As UInteger ' MO4S at ARMO level (MSWP)
        Public Value As Integer = 0                 ' DATA Value (s32)
        Public Weight As Single = 0.0F              ' DATA Weight
        Public Health As UInteger = 0UI             ' DATA Health
        Public ArmorRating As UShort = 0US          ' FNAM (FO4)
        ''' <summary>SKYRIM ONLY — DNAM 'Armor Rating' (s32, wire value = rating×100).
        ''' Distinct from the FO4 <see cref="ArmorRating"/> (u16 in FNAM); Skyrim has no FNAM. 0 for FO4 entries.</summary>
        Public SkyrimArmorRating As Integer = 0
        Public BaseAddonIndex As UShort = 0US       ' FNAM (0 = load addon group 0)
        Public StaggerRating As Byte = 0            ' FNAM
        Public IsOverride As Boolean = False
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
        ''' <summary>El record fuente sin abrir, para auditoría. En un OVERRIDE la cabecera (flags y
        ''' versión) y los campos no tocados por el usuario salen del árbol de <see cref="Record"/>
        ''' —que se abrió leyendo esta misma fuente—, no de este campo.</summary>
        Public SourceRecord As PluginRecord = Nothing
    End Class

    ''' <summary>Result of a save operation.</summary>
    Public Class SaveResult
        Public OutputPath As String
        Public MasterList As New List(Of String)
        Public NpcCount As Integer
        Public RemovedMasters As New List(Of String)
        Public AddedMasters As New List(Of String)
        ''' <summary>For each master in the final MAST list, the FormIDs that brought it in.
        ''' Useful for auditing whether a master is legitimately required (an actual NPC reference
        ''' resolves to that plugin) or accidentally pulled in by a parser/collection bug.
        ''' Format: master name → list of resolved FormIDs.</summary>
        Public MasterAudit As New Dictionary(Of String, List(Of UInteger))(StringComparer.OrdinalIgnoreCase)
        ''' <summary>For every NEW draft emitted (OTFT outfits + LVLI leveled lists): provisional sentinel
        ''' FormID (0xFF… as the caller handed it) → the FILE-LOCAL real FormID written into the record
        ''' header ((selfMasterIdx &lt;&lt; 24) | objectIndex). The caller resolves each file-local value to a
        ''' GLOBAL FormID after re-mounting the saved plugin (PluginManager.ResolveReferencedFormID) to
        ''' "promote" the in-memory drafts to real records — remapping any overlay/draft reference that
        ''' still points at the provisional and dropping the now-persisted drafts (no duplicate on reuse).</summary>
        Public DraftFormIdMap As New Dictionary(Of UInteger, UInteger)
    End Class

    ''' <summary>Los buffers que produce UN recorrido completo del walk de emision, agrupados por GRUP.
    ''' Existe porque ese walk se corre DOS veces: una para DESCUBRIR que masters hacen falta y otra para
    ''' escribir los bytes definitivos. Ver el Paso 1 de <see cref="SaveOverridePlugin"/>.</summary>
    Private NotInheritable Class EmittedBuffers
        Public ReadOnly Records As New List(Of Byte())
        Public ReadOnly Otft As New List(Of Byte())
        Public ReadOnly Lvli As New List(Of Byte())
        Public ReadOnly Lvln As New List(Of Byte())
        Public ReadOnly Mswp As New List(Of Byte())
        Public ReadOnly Arma As New List(Of Byte())
        Public ReadOnly Armo As New List(Of Byte())
        Public ReadOnly Clfm As New List(Of Byte())
    End Class

    ''' <summary>Save (or update) a plugin file containing the given NPC overrides.
    ''' Performs full MAST cleanup: any masters not referenced by the final
    ''' record set are dropped (except the game master, which is always preserved).</summary>
    ''' <param name="outputPath">Final destination path for the plugin (.esp/.esm).</param>
    ''' <param name="game">FO4 or SSE — picks game master and TES4/HEDR version constants.</param>
    ''' <param name="markAsMaster">If True, set FLAG_ESM (master flag). Independent from
    ''' <paramref name="lightMaster"/>: any combination of the two is emitted verbatim into
    ''' the TES4 header. False = no master flag (plain ESP slot semantics).</param>
    ''' <param name="lightMaster">If True, set FLAG_ESL (light slot). Independent from
    ''' <paramref name="markAsMaster"/>.</param>
    ''' <param name="overrides">List of NPC overrides to emit. Order is preserved.</param>
    ''' <param name="existingRecords">Optional: records from a pre-existing plugin (loaded
    ''' via PluginReader) that should be preserved alongside the new overrides. The caller
    ''' filters out NPCs whose FormIDs are about to be replaced by entries in 'overrides'.</param>
    ''' <param name="existingMasters">MAST list of the pre-existing plugin, if any. Used as
    ''' the source for resolving FormIDs inside 'existingRecords'.</param>
    ''' <param name="pluginManager">Required for FormID resolution (master high-byte → plugin name).</param>
    Public Function SaveOverridePlugin(outputPath As String,
                                       game As Config_App.Game_Enum,
                                       markAsMaster As Boolean,
                                       lightMaster As Boolean,
                                       entries As List(Of NpcOverrideEntry),
                                       existingRecords As List(Of PluginRecord),
                                       existingMasters As List(Of String),
                                       pluginManager As PluginManager,
                                       Optional outfitEntries As List(Of OtftRecordEntry) = Nothing,
                                       Optional leveledEntries As List(Of LvliRecordEntry) = Nothing,
                                       Optional existingNextObjectId As UInteger = 0UI,
                                       Optional npcCreateEntries As List(Of NpcCreateEntry) = Nothing,
                                       Optional armoEntries As List(Of ArmoRecordEntry) = Nothing,
                                       Optional armaEntries As List(Of ArmaRecordEntry) = Nothing,
                                       Optional mswpEntries As List(Of MswpRecordEntry) = Nothing,
                                       Optional clfmEntries As List(Of ClfmRecordEntry) = Nothing) As SaveResult

        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("outputPath is empty.", NameOf(outputPath))
        If entries Is Nothing Then entries = New List(Of NpcOverrideEntry)()
        If existingRecords Is Nothing Then existingRecords = New List(Of PluginRecord)()
        If existingMasters Is Nothing Then existingMasters = New List(Of String)()
        If pluginManager Is Nothing Then Throw New ArgumentException("pluginManager is required for FormID resolution.", NameOf(pluginManager))
        If outfitEntries Is Nothing Then outfitEntries = New List(Of OtftRecordEntry)()
        If leveledEntries Is Nothing Then leveledEntries = New List(Of LvliRecordEntry)()
        If npcCreateEntries Is Nothing Then npcCreateEntries = New List(Of NpcCreateEntry)()
        If armoEntries Is Nothing Then armoEntries = New List(Of ArmoRecordEntry)()
        If armaEntries Is Nothing Then armaEntries = New List(Of ArmaRecordEntry)()
        If mswpEntries Is Nothing Then mswpEntries = New List(Of MswpRecordEntry)()
        If clfmEntries Is Nothing Then clfmEntries = New List(Of ClfmRecordEntry)()

        Dim gameMaster = MasterFileNamePublic(game)

        ' ====================================================================
        ' Paso 1: el walk de EMISION, parametrizado por el remapper.
        ' Es la UNICA ley sobre que FormID terminan en el archivo. Antes la MAST se armaba con un juego de
        ' COLECTORES que caminaban el modelo en paralelo a los emisores: dos leyes mantenidas a mano que ya
        ' divergieron dos veces (OBTS y CSDI), y cada divergencia es una referencia apuntando al mod
        ' equivocado. Ahora la MAST se DERIVA de lo que estos bucles realmente escriben, asi que no puede
        ' quedar corta por construccion.
        ' Se corre DOS veces porque el indice de master va horneado en cada FormID emitido y
        ' selfMasterIdx = sortedMasters.Count: el valor a escribir depende del conjunto COMPLETO de masters,
        ' que recien se conoce cuando el recorrido termino. La primera pasada solo DESCUBRE (remapper
        ' identidad, buffers descartados); la segunda, con el remapper real, es la que produce los bytes.
        ' ====================================================================
        ' selfIdx = el indice de master con el que el archivo de salida nombra a sus PROPIOS records.
        ' La pasada de DESCUBRIMIENTO lo pasa en -1 porque todavia no existe la MAST definitiva: sus
        ' bytes se tiran, y lo unico que importa de ella es a que archivos llama el remapper.
        Dim emitAll As Func(Of SaveNpcEspWriter.FormIdRemapper, Integer, EmittedBuffers) =
            Function(rm As SaveNpcEspWriter.FormIdRemapper, selfIdx As Integer) As EmittedBuffers
                Dim b As New EmittedBuffers
                For Each entry In entries
                    b.Records.Add(SerializeNpcRecord(entry, rm, game, selfIdx))
                Next
                For Each existing In existingRecords
                    b.Records.Add(SerializeExistingRecord(existing, existingMasters, pluginManager, rm, game, selfIdx))
                Next
                ' NEW NPC_ records (clones with self-index FormIDs). Emitted into the same NPC_ GRUP as the
                ' overrides — the CK and the engine consume NPC_ records uniformly regardless of
                ' override-vs-new.
                For Each ce In npcCreateEntries
                    b.Records.Add(SerializeNpcCreateRecord(ce, rm, game, selfIdx))
                Next

                ' OTFT outfit records (Edit Outfit "Create" tab). Each emits as a top-level record: NEW ones
                ' carry a self-index FormID (via draftRemap inside the remapper); OVERRIDE ones keep their real
                ' FormID. INAM items are remapped against the new MAST list.
                For Each oe In outfitEntries
                    b.Otft.Add(SerializeOtftRecord(oe, rm, game, selfIdx))
                Next

                ' LVLI leveled lists (Edit Outfit "New LVL…"). Each emits as a self-index top-level record; LVLO
                ' references are remapped (draft → self via draftRemap; real ARMO/LVLI → master remap).
                For Each le In leveledEntries
                    Dim buf = SerializeLvliRecord(le, rm, game, selfIdx)
                    If le.IsNpcList Then b.Lvln.Add(buf) Else b.Lvli.Add(buf)
                Next

                ' MSWP / ARMA / ARMO records (NEW-only). Each emits a self-index top-level record; every FormID it
                ' references is remapped (draft → self via draftRemap; real → master remap).
                For Each mw In mswpEntries
                    b.Mswp.Add(SerializeMswpRecord(mw, rm, game, selfIdx))
                Next
                For Each ae In armaEntries
                    b.Arma.Add(SerializeArmaRecord(ae, rm, game, selfIdx))
                Next
                For Each ao In armoEntries
                    b.Armo.Add(SerializeArmoRecord(ao, rm, game, selfIdx))
                Next

                ' CLFM colour records (SSE hair tint materialized from a RaceMenu preset). NEW ones take a self-index
                ' FormID via draftRemap; OVERRIDE ones (authored by a prior save of this plugin) keep their real FormID.
                For Each ce In clfmEntries
                    b.Clfm.Add(SerializeClfmRecord(ce, rm, game, selfIdx))
                Next
                Return b
            End Function

        ' ====================================================================
        ' Paso 2: armar la MAST list nueva. Por cada FormID del record se agrega el archivo que DEFINE el
        ' master (byte alto -> MAST del plugin origen -> load order). El archivo donde se vio por ultima vez
        ' el FormID NO es lo que hay que agregar: hay que agregar el que es dueno del record master.
        ' GetOriginatingPluginName resuelve eso, y nuestro ResolveFormID ya mapeo los FormID locales a
        ' archivos master por la MAST del plugin de origen.
        ' ====================================================================
        Dim referencedPluginNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        ' Audit table: which FormID is bringing in which master plugin? Logged so the user can
        ' identify "extra" masters as either legitimate references or as bugs upstream.
        ' HashSet, no List: el remapper se invoca UNA VEZ POR REFERENCIA EMITIDA, asi que una lista
        ' acumularia el mismo FormID decenas de veces por master. El audit contesta "que FormID trae
        ' a este master", que es una pregunta de CONJUNTO.
        Dim auditPerPlugin As New Dictionary(Of String, HashSet(Of UInteger))(StringComparer.OrdinalIgnoreCase)
        ' Pasada de DESCUBRIMIENTO: se corre el MISMO walk de emision del Paso 1 con un remapper que, de paso,
        ' anota cada FormID que le pasan. Por eso la MAST no puede quedar corta: lo que se anota es exactamente
        ' lo que despues se escribe. Los buffers que devuelve se tiran — todavia no existe el remapper real.
        ' Un solo nombre de salida para las DOS lambdas: es el predicado de "este FormID es mio, no de un
        ' master", y escribirlo dos veces es justo la clase de duplicacion que este refactor vino a sacar.
        Dim outName = Path.GetFileName(outputPath)
        Dim discoveryRemapper As SaveNpcEspWriter.FormIdRemapper =
            Function(g As UInteger) As UInteger
                If g = 0UI Then Return 0UI
                ' Los drafts provisionales (byte alto 0xFF) no son resolvibles a un master: los maneja
                ' draftRemap, que todavia no existe en esta pasada. Devolverlos tal cual.
                If IsProvisionalDraftFormID(g) Then Return g
                Dim pn = pluginManager.GetOriginatingPluginName(g)
                If String.IsNullOrEmpty(pn) Then
                    Throw New InvalidOperationException(
                        $"FormID {g:X8} does not belong to any loaded plugin, so it cannot be re-mastered into the output.")
                End If
                If Not String.Equals(pn, outName, StringComparison.OrdinalIgnoreCase) Then
                    referencedPluginNames.Add(pn)
                    Dim lst As HashSet(Of UInteger) = Nothing
                    If Not auditPerPlugin.TryGetValue(pn, lst) Then
                        lst = New HashSet(Of UInteger)
                        auditPerPlugin(pn) = lst
                    End If
                    lst.Add(g)
                End If
                ' IDENTIDAD deliberada (la misma convencion que usan los probes de round-trip): devolver el
                ' FormID sin tocar garantiza que ninguna rama del emisor que dependa de cero/no-cero tome un
                ' camino distinto al de la pasada real, o sea que las dos pasadas recorren lo mismo.
                Return g
            End Function
        Call emitAll(discoveryRemapper, -1)

        ' We do NOT force-add the game master here. Auto-adding a game-master file only makes sense when
        ' the source record itself is hardcoded as a game-master record — for normal overrides the game
        ' master arrives via the usual FormID resolution (RNAM=Race, VTCK=Voice, etc.). In practice any NPC
        ' override references game-master records so this is a no-op, but following that same
        ' restraint avoids a spurious master if a NPC somehow doesn't reference Fallout4.esm.

        ' Build MAST list following the standard masters-cleanup convention: preserve the original
        ' master ordering for masters that survive the cleanup, drop unused ones, append any new ones at
        ' the end (in load order). This minimizes the FormID-byte churn vs the "rebuild from scratch
        ' sorted by load order" approach which would re-shuffle high bytes for every survived master
        ' that isn't already in load order.
        Dim sortedMasters As New List(Of String)
        Dim seenLower As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        ' Step 2a: for "Update existing" — preserve survivors from existingMasters in original order:
        ' walk them in their original order and keep each one that is either (a) actually referenced by
        ' some record, or (b) the game master. We replicate that.
        For Each oldM In existingMasters
            If seenLower.Contains(oldM) Then Continue For
            If referencedPluginNames.Contains(oldM) OrElse String.Equals(oldM, gameMaster, StringComparison.OrdinalIgnoreCase) Then
                sortedMasters.Add(oldM)
                seenLower.Add(oldM)
            End If
        Next

        ' Step 2b: append any newly-added masters (referenced by the new override but not in the
        ' existing MAST) in load order. The game master is always added first if it wasn't in
        ' existingMasters and is referenced — Bethesda convention puts it at index 0.
        For Each plugin In pluginManager.Plugins
            If plugin Is Nothing Then Continue For
            If seenLower.Contains(plugin.FileName) Then Continue For
            If referencedPluginNames.Contains(plugin.FileName) Then
                sortedMasters.Add(plugin.FileName)
                seenLower.Add(plugin.FileName)
            End If
        Next

        ' Paso 2c: ORDENAR la MAST por LOAD ORDER, con el game master forzado al índice 0.
        ' La MAST de un plugin válido está siempre en load order. El paso 2a sólo SACA masters, nunca
        ' agrega, así que preserva el orden que ya traía ordenado. Pero al AGREGAR uno que carga antes
        ' que otro ya presente, el paso 2b lo dejaba al final y la lista quedaba fuera de orden.
        ' In-game es inerte (el motor resuelve la MAST por NOMBRE y nuestros tres consumidores son
        ' posicionales), pero el archivo dejaba de ser canónico: la MAST ya no reflejaba el load order,
        ' así que cualquier operación futura que dependa de esa convención (comparar archivos, fusionar,
        ' reordenar masters) partía de un estado inconsistente.
        ' Un plugin NUEVO ya salía ordenado (el paso 2b recorre pluginManager.Plugins en load order), así que
        ' esto sólo mueve bytes en un "Update existing" cuyo MAST estaba desordenado.
        Dim loadOrderRank As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To pluginManager.Plugins.Count - 1
            Dim pl = pluginManager.Plugins(i)
            If pl IsNot Nothing AndAlso Not loadOrderRank.ContainsKey(pl.FileName) Then loadOrderRank(pl.FileName) = i
        Next
        sortedMasters = sortedMasters.
            OrderBy(Function(m) If(String.Equals(m, gameMaster, StringComparison.OrdinalIgnoreCase), -1, 0)).
            ThenBy(Function(m)
                       Dim r As Integer
                       ' Un master que no está en el load order cargado no puede ordenarse contra los demás;
                       ' va al final, en su orden relativo previo (OrderBy es estable).
                       Return If(loadOrderRank.TryGetValue(m, r), r, Integer.MaxValue)
                   End Function).
            ToList()

        ' ====================================================================
        ' Paso 3: armar el FormIdRemapper. Por cada FormID global: resolver el plugin de origen, buscar su nuevo
        ' indice de MAST (-1 = error) y devolver (newMastIdx << 24) | (FormID & 0xFFFFFF).
        ' Casos especiales: FormID 0 se emite como 0 (ref nula); si el plugin de origen es el propio archivo de
        ' salida, el indice es el "self FileID": len(masters).
        ' ====================================================================
        Dim masterIndexLookup As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To sortedMasters.Count - 1
            masterIndexLookup(sortedMasters(i)) = i
        Next
        ' "Self" master index (records owned by THIS plugin) = sortedMasters.Count.
        Dim selfMasterIdx As Integer = sortedMasters.Count

        ' Cada draft NUEVO (outfits OTFT y leveled lists LVLI) recibe su FormID self-index real ANTES de
        ' serializar: (selfMasterIdx << 24) | objIndex, con objIndex arrancando en 0x800 (convencion de
        ' un record nuevo de FO4). El caller les habia dado un centinela PROVISIONAL con byte alto 0xFF, y
        ' draftRemap mapea provisional -> real para que el remapper reescriba tanto el header del record como
        ' toda referencia a el (el NPC.DOFT, el OTFT.INAM que apunta a un LVLI draft, un LVLI a otro).
        ' Asi las referencias cruzadas resuelven por el unico remapper sin importar el orden de emision; las
        ' claves son unicas entre ambos tipos porque salen de un solo contador de la app.
        Dim draftRemap As New Dictionary(Of UInteger, UInteger)
        ' Seed the draft object-ID counter from the disk HEDR if it's ahead of the default. On
        ' update-existing, a prior save consumed object IDs 0x800..existingNextObjectId-1 (those
        ' records are in existingRecords/Entries with self-index FormIDs). Starting at 0x800 again
        ' would re-hand the same IDs and collide with already-preserved overrides.
        ' Ancho del object id del ARCHIVO QUE SE ESCRIBE: 12 bits si sale con FLAG_ESL, 24 si es completo.
        ' Va ACÁ, antes de repartir, no sólo al escribir el HEDR. El enmascarado tiene que pasar AL
        ' REPARTIR cada object id nuevo; clampear sólo el header dejaba salir object ids > 0xFFF en un
        ' ESL, que el motor enmascara a 12 bits (ModInfo::GetFormID) y hace colisionar con otro record —
        ' y ademas congelaba el contador en 0xFFF, con lo que CADA guardado siguiente volvia a repartir
        ' 0xFFF a un record distinto.
        Dim objectIdMask As UInteger = If(lightMaster, &HFFFUI, &HFFFFFFUI)

        ' PISO del espacio de object ids. NO es la constante 0x800: el canónico lo decide POR ARCHIVO
        ' (juego + versión del HEDR + tener masters) y para un plugin SSE nuestro da 1, no 0x800 — ver
        ' PluginWriter.AllowsHardcodedRange. Usarlo cableado hacía que el agotamiento rehusara el guardado
        ' a los 2048 records cuando en SSE el canónico todavía tiene 2047 libres: un límite propio MÁS
        ' ESTRICTO que la referencia. Sólo afecta wrap / recuperación / agotamiento; el arranque de un
        ' guardado normal sale del HEDR (0x800), así que no mueve un byte del caso corriente.
        Dim objectIdFloor As UInteger = If(PluginWriter.AllowsHardcodedRange(game, sortedMasters.Count), 1UI, NEXT_OBJECT_ID_DEFAULT)

        ' Object ids YA OCUPADOS en este archivo: los de los records PROPIOS que se preservan o se
        ' re-emiten con su FormID real. Antes de entregar un id nuevo hay que chequear que ninguno
        ' de estos ya lo tenga, si no dos records terminan compartiendo FormID.
        Dim usedObjectIds As New HashSet(Of UInteger)
        ' Toma un FormID GLOBAL. El object id NO se saca enmascarando con el ancho de SALIDA: lo decide
        ' el encoding de ORIGEN, y esa ley ya está unificada en TryMapGlobalToFileLocal. Con el ancho de
        ' salida, un destino ESL que el usuario destilda a full registraba (lightSlot<<12)|obj en vez de
        ' obj — o sea no anotaba la ocupación real y encima bloqueaba un id espurio.
        Dim noteUsedObjectId As Action(Of UInteger) =
            Sub(g As UInteger)
                If g = 0UI OrElse IsProvisionalDraftFormID(g) Then Return
                Dim lf As UInteger = 0UI
                If pluginManager.TryMapGlobalToFileLocal(g, masterIndexLookup, selfMasterIdx, outName, lf) <> PluginManager.FileLocalMapResult.Ok Then Return
                ' Sólo los records PROPIOS ocupan object ids de este archivo; los overrides van bajo el
                ' índice de su master. El test por el alto == selfMasterIdx ES el test por "el dueño es
                ' outName": masterIndexLookup se llena con 0..sortedMasters.Count-1 (más arriba), o sea que
                ' todo índice de master es ESTRICTAMENTE MENOR que selfMasterIdx = sortedMasters.Count.
                If (lf >> 24) <> CUInt(selfMasterIdx) Then Return
                usedObjectIds.Add(lf And &HFFFFFFUI)
            End Sub
        For Each r In existingRecords
            ' `r.Header.FormID` es LOCAL: existingRecords viene de un PluginReader FRESCO del archivo de
            ' destino, que nunca pasa por MergeRecords (el único lugar que reescribe el header a global).
            ' Pasarlo tal cual a una función que lee el byte alto como SLOT DE SESIÓN es exactamente el
            ' defecto de este dominio. SerializeExistingRecord hace esta misma conversión, y por lo mismo.
            noteUsedObjectId(pluginManager.ResolveReferencedFormID(r.SourcePluginName, r.Header.FormID))
        Next
        ' Un override de un record de otro plugin queda filtrado por el chequeo de outName; el que cuenta es
        ' el NPC que ESTE plugin creó en un guardado anterior y ahora se vuelve a editar.
        For Each e In entries
            If e.Npc IsNot Nothing Then noteUsedObjectId(e.Npc.FormID)
        Next
        For Each oe In outfitEntries : If oe.IsOverride Then noteUsedObjectId(oe.FormID)
        Next
        For Each le In leveledEntries : If le.IsOverride Then noteUsedObjectId(le.FormID)
        Next
        For Each mw In mswpEntries : If mw.IsOverride Then noteUsedObjectId(mw.FormID)
        Next
        For Each ae In armaEntries : If ae.IsOverride Then noteUsedObjectId(ae.FormID)
        Next
        For Each ao In armoEntries : If ao.IsOverride Then noteUsedObjectId(ao.FormID)
        Next
        For Each ce In clfmEntries : If ce.IsOverride Then noteUsedObjectId(ce.FormID)
        Next

        ' Los records PROPIOS que se preservan también tienen que caber en el ancho de SALIDA.
        ' La condición: archivo LIGHT + ObjectID > 0xFFF + el record es DUEÑO de este archivo
        ' (nuestro filtro `(lf >> 24) = selfMasterIdx`: sólo los records PROPIOS). Fuera de esas tres
        ' condiciones no aplica.
        ' Esta app no tiene un canal de advertencias no bloqueantes, así que frente a esta condición
        ' el único punto de aplicación disponible es rehusar el guardado directamente.
        ' Sin esto, el remapper lo emite con el ancho de ORIGEN (que para el remapper es lo correcto) y en
        ' un archivo con FLAG_ESL el motor lo pliega a 12 bits (ModInfo::GetFormID) ⇒ colisiona con otro
        ' record IN-GAME, donde ningún assert lo ve. Se alcanza al TILDAR "Light" sobre un plugin full que
        ' ya tiene records por encima de 0xFFF, y al reabrir un ESL que el código viejo ya corrompió.
        Dim overWide = usedObjectIds.Where(Function(o) o > objectIdMask).OrderBy(Function(o) o).ToList()
        If overWide.Count > 0 Then
            Throw New InvalidOperationException(
                $"'{outName}' already contains {overWide.Count} record(s) whose object id does not fit this " &
                $"file's FormID width (first: 0x{overWide(0):X}, maximum 0x{objectIdMask:X})." &
                If(lightMaster, $" A light (ESL) plugin only addresses 0x{objectIdFloor:X}..0x{objectIdMask:X}, so the game would fold " &
                                "those records onto other FormIDs. Save it without the Light flag, or split " &
                                "the records across two plugins.", " Split the records across two plugins."))
        End If

        ' Semilla = el contador del HEDR ENMASCARADO, y nada más: sin piso en este paso. El único piso
        ' lo pone la recuperación de abajo. Acá había un SEGUNDO piso cableado en 0x800 que el
        ' formato no exige: en SSE (donde el piso real es 1) un HEDR en 0x300 —alcanzable sólo si un
        ' guardado previo ya envolvió al rango hardcoded— saltaba a 0x800 y tiraba ~1280 ids todavía
        ' vigentes.
        ' El caso "plugin NUEVO" (sin HEDR en disco, existingNextObjectId = 0) sí arranca en 0x800: es la
        ' convención del CK y lo que PluginWriter escribe en el header, y mantenerla deja los FormID de un
        ' guardado corriente donde estaban.
        Dim nextSelfObjIndex As UInteger = If(existingNextObjectId > 0UI, existingNextObjectId And objectIdMask, NEXT_OBJECT_ID_DEFAULT)

        ' Recuperación de una semilla no confiable: arrancar en el object id MÁS ALTO en uso en vez de
        ' barrer desde el piso. Barrer desde abajo también sería seguro —el salteo de ocupados impide la
        ' colisión— pero reciclaría el id de un record borrado, y esto deliberadamente no lo
        ' hace.
        ' La ley tiene DOS ramas con la MISMA forma, y `objectIdFloor` es justamente lo que las unifica:
        '     con rango hardcoded → si NextObjectID &lt; 1 o NextObjectID = Mask, sembrar en el piso 1
        '     sin rango hardcoded → si NextObjectID &lt; 0x800 o NextObjectID = Mask, sembrar en el
        '     piso 0x800
        ' Para SSE corre la PRIMERA (ver PluginWriter.AllowsHardcodedRange); confundir las dos
        ' ramas mandaría al próximo lector a "corregir" el código hacia la rama que no se ejecuta.
        ' El término `= Mask` NO es decorativo: es EXACTAMENTE el valor que escribía el código pre-fix
        ' cuando CLAMPEABA el HEDR, así que cualquier ESL que la app haya guardado tocando el tope lo tiene
        ' en disco. Ese valor se lee como "contador ya rodó, no confiable" y hay que re-sembrar; tomarlo
        ' como bueno sería confiar en el número que dejó el bug.
        If nextSelfObjIndex < objectIdFloor OrElse nextSelfObjIndex = objectIdMask Then
            Dim highest As UInteger = objectIdFloor
            For Each u In usedObjectIds
                If u >= highest Then highest = u + 1UI
            Next
            nextSelfObjIndex = If(highest > objectIdMask, objectIdFloor, highest)
        End If

        ' Entrega el próximo object id LIBRE, envolviendo AL PISO (objectIdFloor — 1 o 0x800 según el
        ' archivo, ver arriba) al pasarse del ancho y saltando los que ya están tomados, incluido el
        ' error duro al agotarse: sin él, el desborde es SILENCIOSO y produce dos records con el mismo
        ' FormID.
        Dim dispenseObjectId As Func(Of UInteger) =
            Function() As UInteger
                Dim span As Long = CLng(objectIdMask) - CLng(objectIdFloor) + 1L
                For attempt As Long = 0 To span - 1
                    If nextSelfObjIndex > objectIdMask OrElse nextSelfObjIndex < objectIdFloor Then
                        nextSelfObjIndex = objectIdFloor
                    End If
                    Dim candidate = nextSelfObjIndex
                    nextSelfObjIndex += 1UI
                    If usedObjectIds.Add(candidate) Then Return candidate
                Next
                Throw New InvalidOperationException(
                    $"'{outName}' has no free FormID left: every object id from 0x{objectIdFloor:X} to " &
                    $"0x{objectIdMask:X} is already used by a record in the file. " &
                    If(lightMaster, $"A light (ESL) plugin only addresses {span} of them — save without the " &
                                    "Light flag, or split the records across two plugins.",
                                    "Split the records across two plugins."))
            End Function
        For Each oe In outfitEntries
            If oe.IsOverride Then Continue For
            draftRemap(oe.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        ' NPC_ creates ANTES que leveled: los NPC_ son los records primarios y sus FormIDs deben ser
        ' estables (los bakes en disco se nombran por FormID). Una LVLN/LVLI que los referencia toma su
        ' slot DESPUES, asi agregar/quitar una leveled list no corre los FormIDs de los NPC_. La
        ' resolucion de refs es global (draftRemap completo antes de serializar), asi que el orden de
        ' asignacion no afecta la correctitud — solo que numero recibe cada record.
        For Each ce In npcCreateEntries
            If draftRemap.ContainsKey(ce.ProvisionalFormID) Then Continue For
            draftRemap(ce.ProvisionalFormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        For Each le In leveledEntries
            ' OVERRIDE LVLIs keep their real FormID (master-remapped on emit) — no self-index. Only NEW
            ' (draft) lists get a self-index. Guard against a duplicate provisional listed twice.
            If le.IsOverride Then Continue For
            If draftRemap.ContainsKey(le.FormID) Then Continue For
            draftRemap(le.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        ' NEW MSWP / ARMA / ARMO drafts: pre-assign each a real self-index FormID so cross-draft refs
        ' resolve through the single remapper irrespective of emit order (ARMA.MO2S → draft MSWP,
        ' ARMO.MODL → draft ARMA, ARMO.MO2S → draft MSWP). OVERRIDE entries keep their real FormID. The
        ' provisional keys are globally unique across all draft kinds (one app-side counter). Order
        ' (MSWP → ARMA → ARMO) is cosmetic — resolution is global once draftRemap is fully built.
        For Each mw In mswpEntries
            If mw.IsOverride Then Continue For
            If draftRemap.ContainsKey(mw.FormID) Then Continue For
            draftRemap(mw.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        For Each ae In armaEntries
            If ae.IsOverride Then Continue For
            If draftRemap.ContainsKey(ae.FormID) Then Continue For
            draftRemap(ae.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        For Each ao In armoEntries
            If ao.IsOverride Then Continue For
            If draftRemap.ContainsKey(ao.FormID) Then Continue For
            draftRemap(ao.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        ' NEW CLFM drafts (SSE hair colour materialized from a RaceMenu preset). Same pre-assignment as every
        ' other draft kind, so the NPC_.HCLF that points at the provisional sentinel rewrites to the real
        ' self-index FormID through the single remapper regardless of emit order.
        For Each ce In clfmEntries
            If ce.IsOverride Then Continue For
            If draftRemap.ContainsKey(ce.FormID) Then Continue For
            draftRemap(ce.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next

        Dim remapper As SaveNpcEspWriter.FormIdRemapper =
            Function(globalFormID As UInteger) As UInteger
                If globalFormID = 0UI Then Return 0UI
                ' NEW draft (OTFT/LVLI/ARMA/ARMO/MSWP/CLFM) → real self FormID. Branch on the SAME predicate
                ' the discovery pass uses — IsProvisionalDraftFormID — and only then consult draftRemap.
                ' Using `draftRemap.TryGetValue` as the branch instead would be the law written twice with
                ' two different tests: discovery would classify a FormID as a draft while this pass did not,
                ' or the reverse, and the two walks would stop agreeing. They must partition FormIDs
                ' identically; what differs between them is only what they DO with each part.
                If IsProvisionalDraftFormID(globalFormID) Then
                    Dim mappedDraft As UInteger
                    If draftRemap.TryGetValue(globalFormID, mappedDraft) Then Return mappedDraft
                    ' A provisional FormID that no draft claims: something references a draft record that is
                    ' not being emitted (e.g. a draft cancelled while another still points at it). Falling
                    ' through would ask GetOriginatingPluginName about a 0xFF high byte — never a valid slot,
                    ' MAX_FULL_SLOT is 0xFD — and throw the misleading "not in any loaded plugin".
                    Throw New InvalidOperationException(
                        $"Draft FormID {globalFormID:X8} is referenced by a record being written but no draft " &
                        "record claims it, so it cannot be given a real FormID. A referenced draft was most " &
                        "likely cancelled or deleted while something still pointed at it.")
                End If
                ' La conversión global → local (byte alto = índice en la MAST de ESTE archivo, self =
                ' masters.Count, ancho del object id según el encoding de ORIGEN) vive UNA sola vez, en
                ' PluginManager.TryMapGlobalToFileLocal. Acá sólo se decide QUÉ HACER con cada resultado:
                ' los dos casos de fallo son aserciones para el writer (ver los comentarios de abajo).
                Dim mappedLocal As UInteger = 0UI
                Dim mapRes = pluginManager.TryMapGlobalToFileLocal(globalFormID, masterIndexLookup, selfMasterIdx, outName, mappedLocal)
                If mapRes = PluginManager.FileLocalMapResult.Ok Then Return mappedLocal

                Dim pname = pluginManager.GetOriginatingPluginName(globalFormID)
                If mapRes = PluginManager.FileLocalMapResult.NoOwner Then
                    ' Was "best effort: keep raw", which wrote the GLOBAL FormID into a file where the
                    ' high byte means an index into THIS file's MAST — two different numbering spaces. The
                    ' reference silently ended up pointing at whatever plugin sat at that index.
                    ' Every way a FormID could reach here without a resolvable owner is now closed
                    ' upstream: the .esl extension gets a light slot (PluginReader.ReadTES4), a re-mount
                    ' keeps its slot instead of vacating one (MergeOverridePlugin), a persisted identifier
                    ' no longer carries a stale slot (GlobalFormIDFromIdentifierLocal), masters are merged
                    ' before their dependents (OrderByMasters), an unloaded save target is refused by the
                    ' Save dialog, and MakeGlobalFormID no longer invents slot 0. So this is an assertion
                    ' on an invariant, not a user-facing failure mode: if it fires, the bug is ours.
                    Throw New InvalidOperationException(
                        $"FormID {globalFormID:X8} does not belong to any loaded plugin, so it cannot be " &
                        "re-mastered into the output. Writing it unchanged would silently repoint it at " &
                        "whichever plugin occupies that index in the new master list.")
                End If
                ' OwnerNotInMasterList: el owner resolvio pero no quedo en la MAST. Esta rama nacio como
                ' detector de drift entre los COLECTORES (que armaban la MAST caminando el modelo) y los
                ' EMISORES (que producian los bytes) — dos leyes paralelas mantenidas a mano, que ya
                ' habian divergido dos veces. Los colectores ya no existen: la MAST se DERIVA del walk de
                ' emision (Paso 1), la misma pasada que produce estos bytes, asi que todo FormID que llega
                ' hasta aca ya paso por discoveryRemapper y su plugin ya esta en la lista. Por
                ' construccion, inalcanzable.
                '
                ' Para el WRITER es una asercion y por eso lanza; el otro llamador de
                ' TryMapGlobalToFileLocal (NpcOverrideSaver, que mapea contra la MAST VIEJA del disco)
                ' necesita lo OPUESTO — ahi este caso es legitimo y frecuente. Por eso la funcion
                ' compartida devuelve un enum en vez de decidir por los dos.
                '
                ' Sigue tirando en vez de devolver el FormID crudo: el crudo esta indexado
                ' por load order, mientras que el byte alto de un FormID en el archivo de salida es un
                ' indice en la MAST de ESTE archivo, asi que escribirlo repuntaria la referencia en
                ' silencio al plugin que ocupe ese indice. Si algun dia se dispara, lo que se rompio es la
                ' premisa de que las dos pasadas recorren lo mismo — y mejor mil veces que el guardado
                ' falle fuerte antes que shipear un plugin apuntando al mod equivocado.
                Throw New InvalidOperationException(
                    $"FormID {globalFormID:X8} is owned by '{pname}', which is not in the master list " &
                    "being written. The master list is built from the discovery pass over this very " &
                    "emission walk, so every plugin reached here should already be in it — this means " &
                    "the two passes disagreed. Writing it unchanged would silently repoint the " &
                    "reference at whichever plugin occupies that index.")
            End Function

        ' Diff against existing masters for the SaveResult report.
        Dim result As New SaveResult With {
            .OutputPath = outputPath,
            .NpcCount = entries.Count + existingRecords.Count,
            .MasterList = sortedMasters,
            .DraftFormIdMap = New Dictionary(Of UInteger, UInteger)(draftRemap)
        }
        ' Filter the audit to only masters that actually made it into the final MAST list
        ' (drop entries for plugins that referencedPluginNames had but Step 2 filtered out
        ' because they're not in the load order).
        For Each m In sortedMasters
            Dim list As HashSet(Of UInteger) = Nothing
            If auditPerPlugin.TryGetValue(m, list) Then
                result.MasterAudit(m) = list.ToList()
            Else
                result.MasterAudit(m) = New List(Of UInteger)
            End If
        Next
        For Each oldM In existingMasters
            If Not sortedMasters.Any(Function(m) String.Equals(m, oldM, StringComparison.OrdinalIgnoreCase)) Then
                result.RemovedMasters.Add(oldM)
            End If
        Next
        For Each newM In sortedMasters
            If Not existingMasters.Any(Function(m) String.Equals(m, newM, StringComparison.OrdinalIgnoreCase)) Then
                result.AddedMasters.Add(newM)
            End If
        Next

        ' ====================================================================
        ' Paso 4: la emision de verdad. Mismo walk que la pasada de descubrimiento, ahora con el remapper
        ' real (indices de la MAST ya cerrada + draftRemap). Se conservan los nombres locales de siempre
        ' para que el Paso 5 y todo lo de aguas abajo no se entere de que la emision se movio.
        ' ====================================================================
        Dim emitted As EmittedBuffers = emitAll(remapper, selfMasterIdx)
        Dim recordBuffers As List(Of Byte()) = emitted.Records
        Dim otftBuffers As List(Of Byte()) = emitted.Otft
        Dim lvliBuffers As List(Of Byte()) = emitted.Lvli
        Dim lvlnBuffers As List(Of Byte()) = emitted.Lvln
        Dim mswpBuffers As List(Of Byte()) = emitted.Mswp
        Dim armaBuffers As List(Of Byte()) = emitted.Arma
        Dim armoBuffers As List(Of Byte()) = emitted.Armo
        Dim clfmBuffers As List(Of Byte()) = emitted.Clfm

        ' ====================================================================
        ' Paso 5: envolver cada tipo de record en su GRUP de primer nivel, en orden referenced-first:
        ' CLFM -> MSWP -> ARMA -> ARMO -> OTFT -> LVLN -> LVLI -> NPC_ (un record referenciado precede a su
        ' referrer). La resolucion de FormID es global, asi que al motor el orden no le importa: es para que el
        ' archivo quede legible.
        ' âš ï¸ Es una desviacion DELIBERADA y PREEXISTENTE del orden de GRUP canonico, que en los dos
        ' juegos pone CLFM cerca del FINAL. Este writer ya emite referenced-first para los otros 7 grupos y CLFM
        ' sigue la MISMA convencion local en vez de partir el orden del archivo en dos reglas. Nada lo rechaza:
        ' el orden de GRUP no significa nada para el motor ni para el CK, así que reordenarlo acá no
        ' rompe nada.
        ' Lo que SI tiene que estar bien es HEDR.numRecords, mas abajo.
        ' ====================================================================
        Dim grupClfmBytes As Byte() = If(clfmBuffers.Count > 0, BuildGrup("CLFM", clfmBuffers), Array.Empty(Of Byte)())
        Dim grupMswpBytes As Byte() = If(mswpBuffers.Count > 0, BuildGrup("MSWP", mswpBuffers), Array.Empty(Of Byte)())
        Dim grupArmaBytes As Byte() = If(armaBuffers.Count > 0, BuildGrup("ARMA", armaBuffers), Array.Empty(Of Byte)())
        Dim grupArmoBytes As Byte() = If(armoBuffers.Count > 0, BuildGrup("ARMO", armoBuffers), Array.Empty(Of Byte)())
        Dim grupOtftBytes As Byte() = If(otftBuffers.Count > 0, BuildGrup("OTFT", otftBuffers), Array.Empty(Of Byte)())
        ' LVLN (Leveled NPC) va ANTES que LVLI en el orden de GRUP que usa este writer.
        Dim grupLvlnBytes As Byte() = If(lvlnBuffers.Count > 0, BuildGrup("LVLN", lvlnBuffers), Array.Empty(Of Byte)())
        Dim grupLvliBytes As Byte() = If(lvliBuffers.Count > 0, BuildGrup("LVLI", lvliBuffers), Array.Empty(Of Byte)())
        Dim grupNpcBytes = BuildGrup("NPC_", recordBuffers)

        ' ====================================================================
        ' Paso 6: armar el header TES4 y emitir el stream final.
        ' nextObjectId: el contador de drafts se sembro con
        ' max(0x800, HEDR del disco) y avanzo una vez por draft NUEVO, asi que su valor final es el primer slot
        ' libre despues de este guardado, que es exactamente lo que debe llevar HEDR.nextObjectId. Un plugin
        ' fresco sin drafts se queda en 0x800, y actualizar uno existente sin drafts nuevos preserva el contador
        ' por la semilla.
        ' El ancho (objectIdMask) ya se aplico AL REPARTIR, arriba, que es donde corresponde aplicarlo.
        ' Aca solo queda ENVOLVER a 0x800 si el contador quedo justo pasado del tope.
        ' Antes esto CLAMPEABA a objectIdMask, y ese clamp era el motor del defecto: dejaba el contador
        ' congelado en 0xFFF, asi que el guardado siguiente se sembraba ahi y volvia a repartir 0xFFF a un
        ' record distinto. Con el reparto ya acotado y el skip de ocupados, el clamp no protegia nada.
        ' ====================================================================
        Dim nextObjectId As UInteger = nextSelfObjIndex
        If nextObjectId > objectIdMask Then nextObjectId = objectIdFloor
        ' HEDR.numRecords counts EVERY element: TES4 itself counts as 1, plus each top-level GRUP,
        ' which counts as 1 ON TOP OF the sum of its children — i.e. the GRUP counts as one form ON TOP
        ' OF its records. Subtracting TES4 leaves:
        '     numRecords = (content records) + (top-level GRUPs emitted)
        ' Verified against CK-authored plugins (KSHairdos.esp: HEDR 304 = 303 records + 1 GRUP).
        ' Counting only the records made the CK pop "Form counts don't match / correct the file header?".
        ' The NPC_ GRUP is always emitted (Step 7), the rest only when non-empty.
        Dim grupCount As Integer = 1 +
                                   If(clfmBuffers.Count > 0, 1, 0) +
                                   If(mswpBuffers.Count > 0, 1, 0) + If(armaBuffers.Count > 0, 1, 0) +
                                   If(armoBuffers.Count > 0, 1, 0) + If(otftBuffers.Count > 0, 1, 0) +
                                   If(lvlnBuffers.Count > 0, 1, 0) + If(lvliBuffers.Count > 0, 1, 0)
        Dim totalRecords As Integer = recordBuffers.Count + otftBuffers.Count + lvliBuffers.Count + lvlnBuffers.Count +
                                      mswpBuffers.Count + armaBuffers.Count + armoBuffers.Count + clfmBuffers.Count + grupCount
        Dim tes4Bytes = BuildTes4Header(game, markAsMaster, lightMaster, sortedMasters, totalRecords, nextObjectId, gameMaster, Path.GetDirectoryName(outputPath))

        ' ====================================================================
        ' Step 7: escritura atómica — .tmp y después File.Replace (ver el bloque de abajo; NO es un rename
        ' a secas, que dejaba una ventana sin archivo).
        ' ====================================================================
        Dim outDir = Path.GetDirectoryName(outputPath)
        If Not String.IsNullOrEmpty(outDir) AndAlso Not Directory.Exists(outDir) Then
            Directory.CreateDirectory(outDir)
        End If

        Dim tmpPath = outputPath & ".tmp"
        Using fs As FileStream = File.Create(tmpPath)
            fs.Write(tes4Bytes, 0, tes4Bytes.Length)
            ' Canonical referenced-first GRUP order: CLFM → MSWP → ARMA → ARMO → OTFT → LVLN → LVLI → NPC_ (Step 5).
            If grupClfmBytes.Length > 0 Then fs.Write(grupClfmBytes, 0, grupClfmBytes.Length)
            If grupMswpBytes.Length > 0 Then fs.Write(grupMswpBytes, 0, grupMswpBytes.Length)
            If grupArmaBytes.Length > 0 Then fs.Write(grupArmaBytes, 0, grupArmaBytes.Length)
            If grupArmoBytes.Length > 0 Then fs.Write(grupArmoBytes, 0, grupArmoBytes.Length)
            If grupOtftBytes.Length > 0 Then fs.Write(grupOtftBytes, 0, grupOtftBytes.Length)
            If grupLvlnBytes.Length > 0 Then fs.Write(grupLvlnBytes, 0, grupLvlnBytes.Length)
            If grupLvliBytes.Length > 0 Then fs.Write(grupLvliBytes, 0, grupLvliBytes.Length)
            fs.Write(grupNpcBytes, 0, grupNpcBytes.Length)
        End Using

        ' `Delete` + `Move` NO es atómico y el docstring de arriba afirmaba que sí: entre las dos llamadas el
        ' plugin NO EXISTE. Si el Delete sale bien y el Move falla (un handle con FILE_SHARE_DELETE de un
        ' antivirus o del mod manager deja el borrado pendiente, un corte), el usuario se queda SIN el .esp y con
        ' un .esp.tmp al lado — después de haber guardado 300 NPC.
        ' `File.Replace` es el primitivo correcto y ya se usaba en este árbol (LoadOrderActivator.vb:376); exige
        ' que el destino exista, así que el Move queda para el caso "archivo nuevo".
        If File.Exists(outputPath) Then
            File.Replace(tmpPath, outputPath, Nothing, ignoreMetadataErrors:=True)
        Else
            File.Move(tmpPath, outputPath)
        End If

        Return result
    End Function

    ' ========================================================================
    ' Record / Group / Header serialization
    ' ========================================================================

    ''' <summary>Sobrescritura de un NPC_: el cuerpo es el arbol del record, tal cual quedo despues
    ''' de las ediciones. Igual que los demas records migrados.</summary>
    Private Function SerializeNpcRecord(entry As NpcOverrideEntry, remapper As SaveNpcEspWriter.FormIdRemapper,
                                        game As Config_App.Game_Enum, selfIdxDestino As Integer) As Byte()
        If entry.Npc Is Nothing OrElse entry.Npc.Record Is Nothing Then
            Throw New InvalidOperationException(
                "NpcOverrideEntry sin record: el cuerpo de un NPC_ ES su arbol. Sin el no hay nada que grabar.")
        End If
        Dim vista = TryCast(entry.Npc.Record, Canon.CanonView)
        If vista Is Nothing Then Return Array.Empty(Of Byte)()
        Return SerializarRecord(vista, entry.Npc.FormID, remapper, game,
                                entry.OriginalHeader.VCS1, entry.OriginalHeader.VCS2, selfIdxDestino)
    End Function

    ''' <summary>Cuerpo de un NPC_ sobrescrito, sin la cabecera. Es EXACTAMENTE lo que emite el
    ''' guardado: publico para que los arneses midan ese camino y no una replica.</summary>
    ''' <param name="selfIdxDestino">Igual que en <see cref="SerializarRecord"/>: el indice de master
    ''' con el que el archivo de salida nombra a sus propios records. -1 = no se sabe.</param>
    Public Function CuerpoDeNpcSobrescrito(npc As NPC_Data,
                                           remapper As SaveNpcEspWriter.FormIdRemapper,
                                           selfIdxDestino As Integer) As Byte()
        If npc Is Nothing Then Return Array.Empty(Of Byte)()
        Dim vista = TryCast(npc.Record, Canon.CanonView)
        If vista Is Nothing Then Return Array.Empty(Of Byte)()
        Dim traducir As Func(Of UInteger, UInteger) = Nothing
        If remapper IsNot Nothing Then traducir = Function(x) remapper(x)
        Return Canon.CanonEscritura.Cuerpo(vista, traducir, selfIdxDestino)
    End Function

    ''' <summary>Serialize a NEW NPC_ record (clone with self-index FormID). Mirrors
    ''' <see cref="SerializeNpcRecord"/> except header uses defaults (no source OriginalHeader): Flags=0
    ''' (no COMPRESSED, no special flags), VCS1=0, Version=record-version of the target game, VCS2=0.
    ''' FormID is the entry's provisional sentinel which the remapper rewrites to the real self-index.</summary>
    Private Function SerializeNpcCreateRecord(entry As NpcCreateEntry, remapper As SaveNpcEspWriter.FormIdRemapper,
                                              game As Config_App.Game_Enum, selfIdxDestino As Integer) As Byte()
        Dim origen = If(entry.NpcData Is Nothing, Nothing, entry.NpcData.Record)
        If origen Is Nothing Then
            ' Sin record del que clonar: arranca con los campos que el formato marca como obligatorios.
            ' Las banderas de cabecera quedan en cero y la version de formulario tambien, que es lo que
            ' el envoltorio interpreta como "la del juego" - igual que antes.
            origen = Canon.CanonRecords.NpcNuevo(JuegoCanonico(game))
        End If
        Dim vista = TryCast(origen, Canon.CanonView)
        If vista Is Nothing Then Return Array.Empty(Of Byte)()
        Return SerializarRecord(vista, entry.ProvisionalFormID, remapper, game, 0UI, CUShort(0), selfIdxDestino)
    End Function

    ''' <summary>El juego de la sesion, en la forma que entiende la declaracion del formato.</summary>
    Private Function JuegoCanonico(game As Config_App.Game_Enum) As Canon.WbGame
        If game = Config_App.Game_Enum.Fallout4 Then Return Canon.WbGame.Fallout4
        Return Canon.WbGame.Skyrim
    End Function

    Private Function SerializeExistingRecord(rec As PluginRecord,
                                             existingMasters As List(Of String),
                                             pluginManager As PluginManager,
                                             remapper As SaveNpcEspWriter.FormIdRemapper,
                                             game As Config_App.Game_Enum,
                                             selfIdxDestino As Integer) As Byte()
        ' Los NPC_ se re-emiten desde su arbol para que la MAST quede limpia. Los demas tipos de
        ' record (raros en un plugin generado por la aplicacion) no tienen camino: ver el throw.
        If rec.Header.Signature = "NPC_" Then
            Dim parsed = RecordParsers.ParseNPC(rec, pluginManager)
            ' ParseNPC copies rec.Header.FormID verbatim into parsed.FormID
            ' (RecordParsers.vb:1735). For records coming from a fresh PluginReader (the
            ' update-existing path) that value is LOCAL — but SerializeNpcRecord passes it to
            ' the remapper, which expects GLOBAL (GetOriginatingPluginName indexes the high
            ' byte against load order). Without this resolve the record's master high byte
            ' gets rewritten against the wrong plugin in the new MAST list. Subrecord FormIDs
            ' inside parsed are already GLOBAL (ResolveFormIDReference at parse time); only
            ' the record-own FormID needs the explicit resolve.
            parsed.FormID = pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rec.Header.FormID)
            Dim entry As New NpcOverrideEntry With {
                .Npc = parsed,
                .SourcePluginName = rec.SourcePluginName,
                .OriginalHeader = rec.Header
            }
            Return SerializeNpcRecord(entry, remapper, game, selfIdxDestino)
        End If

        ' Fallback path explicitly NOT supported. NPC_Manager auto-generated plugins should
        ' only ever contain NPC_ records. The "preserve existing" workflow filters them at
        ' load time. If a non-NPC record reaches here, the safest action is to throw — silent
        ' copy-through risks corrupting non-FormID 4-byte subrecords (NAM6 height float, KSIZ
        ' counter, etc. would be misidentified as FormIDs and re-mapped, producing garbage).
        ' See revisor finding m6.
        Throw New NotSupportedException(
            $"SaveNpcEspWriter currently only supports NPC_ records. Encountered '{rec.Header.Signature}' " &
            "while preserving existing records. The plugin file may have been edited externally and contains " &
            "record types this writer does not handle.")
    End Function

    ''' <summary>Serialize one OTFT (outfit) record: 24-byte header + EDID + INAM (array of remapped
    ''' ARMO/LVLI FormIDs). The record FormID is remapped (NEW → self-index via draftRemap; OVERRIDE →
    ''' master remap). INAM is omitted when there are no items.</summary>
    Private Function SerializeOtftRecord(entry As OtftRecordEntry, remapper As SaveNpcEspWriter.FormIdRemapper,
                                         game As Config_App.Game_Enum, selfIdxDestino As Integer) As Byte()
        Return SerializarRecord(entry.Record, entry.FormID, remapper, game,
                                entry.OriginalVcs1, entry.OriginalVcs2, selfIdxDestino)
    End Function

    ''' <summary>Serializa un record CLFM (Color): header de 24 bytes + cuerpo EDID + [FULL] + CNAM + FNAM.
    ''' <list type="bullet">
    ''' <item>EDID - ZSTRING cp1252 no traducible, el mismo encoder que todos los EDID de aca.</item>
    ''' <item>FULL - nombre opcional. OVERRIDE: <see cref="ClfmRecordEntry.FullNameRaw"/> verbatim. NEW:
    '''   <see cref="ClfmRecordEntry.FullName"/> por <c>EncodeTranslatable</c>, NO por el encoder cp1252 del
    '''   EDID. Los dos vacios = sin FULL. Se emite el literal y no un id de string table porque todo el writer
    '''   apunta a plugins NO localizados y no shippea strings.</item>
    ''' <item>CNAM - RGBA de 4 bytes [R,G,B,A] (espejo del parser). Medido sobre Skyrim.esm: alpha 0 en los
    '''   178 CLFM.</item>
    ''' <item>FNAM - u32. Skyrim: bool Playable, =1 en los 15 colores de pelo vanilla. FO4: campo de flags donde
    '''   el bit 1 significa "CNAM es un RemappingIndex, no un RGB", que es justo por lo que este camino es
    '''   SSE-only en el caller.</item>
    ''' </list>
    ''' El FormID del record es el self-index real del draft (NEW, via draftRemap) o el global existente
    ''' (OVERRIDE, master-remapeado). El CLFM no lleva FormID en el cuerpo.</summary>
    Private Function SerializeClfmRecord(entry As ClfmRecordEntry, remapper As SaveNpcEspWriter.FormIdRemapper,
                                         game As Config_App.Game_Enum, selfIdxDestino As Integer) As Byte()
        Return SerializarRecord(entry.Record, entry.FormID, remapper, game,
                                entry.OriginalVcs1, entry.OriginalVcs2, selfIdxDestino)
    End Function

    ''' <summary>Serialize one LVLI (leveled item) or LVLN (leveled NPC, <see cref="LvliRecordEntry.IsNpcList"/>)
    ''' record. El signature (LVLI/LVLN), el orden de los subrecords y cuáles de ellos existen -LVLM/LLKC/LVSG/ONAM
    ''' son FO4-only, el generic model es LVLN-only- salen de la declaración del formato para el (record, juego)
    ''' de <see cref="LvliRecordEntry.Record"/>, no de esta función.</summary>
    Private Function SerializeLvliRecord(entry As LvliRecordEntry, remapper As SaveNpcEspWriter.FormIdRemapper,
                                         game As Config_App.Game_Enum, selfIdxDestino As Integer) As Byte()
        Return SerializarRecord(entry.Record, entry.FormID, remapper, game,
                                entry.OriginalVcs1, entry.OriginalVcs2, selfIdxDestino)
    End Function

    ' ------------------------------------------------------------------------
    ' MSWP / ARMA / ARMO serializers (NEW records only). Each emits the 24-byte record header
    ' (Signature, DataSize, Flags, remapped FormID, VCS1, Version, VCS2) — same shape as
    ' SerializeOtftRecord / SerializeLvliRecord — followed by the body in the format's declared
    ' field order.
    ' ------------------------------------------------------------------------

    ''' <summary>Emit a NON-translatable ZSTRING subrecord (General/cp1252 encoding + trailing NUL),
    ''' mirror of SerializeOtftRecord's EDID emission. Used for EDID and
    ''' all model/material paths (MOD2/3/4/5, MSWP BNAM/SNAM/FNAM).</summary>
    Private Sub WriteZString(bw As BinaryWriter, sig As String, value As String)
        Dim bytes = PluginEncodingSettings.EncodeGeneral(If(value, ""))
        WriteSubrecordHeader(bw, sig, bytes.Length + 1)
        bw.Write(bytes)
        bw.Write(CByte(0))
    End Sub

    ''' <summary>Serialize one MSWP (Material Swap) record. Body order:
    ''' EDID, FNAM 'Tree Folder' (optional), then per substitution BNAM 'Original Material' +
    ''' SNAM 'Replacement Material' + CNAM 'Color Remapping Index' (float, only when present). The obsolete
    ''' per-substitution FNAM (12808) is deliberately NOT emitted. MSWP body has no FormIDs; only its own
    ''' record FormID is remapped. Header flags = 0 for NEW records; for OVERRIDE the source header flags
    ''' (COMPRESSED stripped) and source Version are preserved while the body is fully re-emitted from the
    ''' entry — MSWP has no body FormIDs and a simple substitution list, so no subrecord merge is needed.</summary>
    Private Function SerializeMswpRecord(entry As MswpRecordEntry, remapper As SaveNpcEspWriter.FormIdRemapper,
                                         game As Config_App.Game_Enum, selfIdxDestino As Integer) As Byte()
        Return SerializarRecord(entry.Record, entry.FormID, remapper, game,
                                entry.OriginalVcs1, entry.OriginalVcs2, selfIdxDestino)
    End Function

    ''' <summary>Serialize one ARMA (Armor Addon). Public para que el arnes de paridad round-trip
    ''' (Tools\ArmoArmaSseRoundtripProbe) lo pueda ejercitar directo con un remapper identidad.
    ''' pluginManager queda en la firma por compatibilidad con ese arnes: el cuerpo sale del arbol
    ''' del record (entry.Record), no hace falta resolver nada acá.</summary>
    Public Function SerializeArmaRecord(entry As ArmaRecordEntry,
                                        remapper As SaveNpcEspWriter.FormIdRemapper,
                                        game As Config_App.Game_Enum,
                                        selfIdxDestino As Integer) As Byte()
        Return SerializarRecord(entry.Record, entry.FormID, remapper, game,
                                entry.OriginalVcs1, entry.OriginalVcs2, selfIdxDestino)
    End Function

    ''' <summary>Serialize one ARMO (Armor). Misma nota que <see cref="SerializeArmaRecord"/> sobre
    ''' pluginManager.</summary>
    Public Function SerializeArmoRecord(entry As ArmoRecordEntry,
                                        remapper As SaveNpcEspWriter.FormIdRemapper,
                                        game As Config_App.Game_Enum,
                                        selfIdxDestino As Integer) As Byte()
        Return SerializarRecord(entry.Record, entry.FormID, remapper, game,
                                entry.OriginalVcs1, entry.OriginalVcs2, selfIdxDestino)
    End Function

    ''' <summary>Wrap a record body in the 24-byte record header (Signature, DataSize, Flags, FormID,
    ''' VCS1, Version, VCS2). Shared by create + override paths. The FormID passed is already remapped.
    ''' <paramref name="versionOverride"/> (override path) forces a specific record Version (the source
    ''' record's) instead of the target game's default — preserve the source header on re-save.</summary>
    Private Function WrapRecord(signature As String, body As Byte(), flags As UInteger, mappedFormID As UInteger,
                                vcs1 As UInteger, vcs2 As UShort, game As Config_App.Game_Enum,
                                Optional versionOverride As UShort = 0US) As Byte()
        Dim recordVersion As UShort = If(versionOverride <> 0US, versionOverride,
                                         If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE))
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes(signature))    ' Signature
                bw.Write(CUInt(body.Length))                    ' DataSize
                bw.Write(flags)                                 ' Flags
                bw.Write(mappedFormID)                          ' FormID (already remapped)
                bw.Write(vcs1)                                  ' VCS1
                bw.Write(recordVersion)                         ' Version
                bw.Write(vcs2)                                  ' VCS2
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>True if a FormID is a provisional draft sentinel (high byte 0xFF). Such FormIDs are not
    ''' resolvable to any loaded master (max real index 0xFD, ESL prefix 0xFE) — they resolve only through
    ''' draftRemap. Mirrors the app-side OutfitDraft.IsDraftFormID, kept local so the library has no app dep.</summary>
    Private Function IsProvisionalDraftFormID(formID As UInteger) As Boolean
        Return ((formID >> 24) And &HFFUI) = &HFFUI
    End Function

    Private Function BuildGrup(label As String, recordBuffers As List(Of Byte())) As Byte()
        ' GRUP header (24 bytes): Signature "GRUP" + GroupSize (incl. header) + Label (record-type
        ' signature) as u32 + GroupType (0 = top-level) + Stamp + Unknown.
        If label Is Nothing OrElse label.Length <> 4 Then Throw New ArgumentException($"GRUP label must be 4 chars: '{label}'.", NameOf(label))
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                Dim contentSize = recordBuffers.Sum(Function(b) b.Length)
                Dim totalSize = 24 + contentSize
                bw.Write(Encoding.ASCII.GetBytes("GRUP"))
                bw.Write(CUInt(totalSize))
                bw.Write(Encoding.ASCII.GetBytes(label))     ' Label u32 = record-type signature bytes
                bw.Write(0)                                  ' GroupType = 0 (top-level)
                bw.Write(0UI)                                ' Stamp
                bw.Write(0UI)                                ' Unknown
                For Each b In recordBuffers
                    bw.Write(b)
                Next
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Function BuildTes4Header(game As Config_App.Game_Enum,
                                     markAsMaster As Boolean,
                                     lightMaster As Boolean,
                                     masters As List(Of String),
                                     numContentRecords As Integer,
                                     nextObjectId As UInteger,
                                     gameMaster As String,
                                     outputDir As String) As Byte()
        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Dim hedrVersion As Single = If(game = Config_App.Game_Enum.Fallout4, HEDR_VERSION_FO4, HEDR_VERSION_SSE)

        ' HEDR + CNAM + (MAST + DATA)*N
        Using bodyMs As New MemoryStream()
            Using bw As New BinaryWriter(bodyMs)
                ' HEDR (12 bytes)
                WriteSubrecordHeader(bw, "HEDR", 12)
                bw.Write(hedrVersion)
                ' numRecords = content records + top-level GRUPs (TES4 itself excluded). The caller
                ' computes it; see the derivation at the call site.
                bw.Write(CUInt(numContentRecords))
                ' Next free self object index — must exceed any self-index FormID we assigned to new
                ' records (OTFT outfits start at NEXT_OBJECT_ID_DEFAULT) so the CK won't re-issue one.
                bw.Write(nextObjectId)

                ' CNAM (author, ZSTRING). TES4.CNAM is a translatable string.
                ' Literal is ASCII-only but route via central encoder for convention consistency.
                Dim authorBytes = PluginEncodingSettings.EncodeTranslatable(NPC_MANAGER_AUTHOR_CNAM)
                WriteSubrecordHeader(bw, "CNAM", authorBytes.Length + 1)
                bw.Write(authorBytes)
                bw.Write(CByte(0))

                ' SNAM (TES4 Description, ZSTRING). Convención de compatibilidad: un tag <cp:XXXX> en
                ' este campo señala el encoding Translatable del archivo, para que quien lo abra sepa
                ' qué codepage aplicar sin depender del idioma configurado en su máquina. No es
                ' obligatorio — se emite acá como mejora de UX deliberada (riesgo cero de bug): los
                ' plugins que genera NPC_Manager declaran su propio encoding sin importar la
                ' configuración de idioma de quien los abra. El tag no ayuda in-game (el motor del juego
                ' lo ignora) pero tampoco molesta — es sólo una descripción legible de más. Formato:
                ' "Plugin encoding: <cp:XXXX>" — prefijo descriptivo para quien navega el plugin con otra
                ' herramienta; el tag se puede ubicar en cualquier posición del texto.
                Dim cpTag = PluginEncodingSettings.GetTranslatableSnamCpTag()
                If cpTag <> "" Then
                    Dim snamText = "Plugin encoding: " & cpTag
                    Dim snamBytes = PluginEncodingSettings.EncodeTranslatable(snamText)
                    WriteSubrecordHeader(bw, "SNAM", snamBytes.Length + 1)
                    bw.Write(snamBytes)
                    bw.Write(CByte(0))
                End If

                ' MAST + DATA pairs. DATA is an 8-byte array with no known use, normally set by the CK
                ' but almost always null. The engine ignores the field at runtime — the canonical
                ' CK output is 8 zero bytes, so we match that and skip the file-size lookup.
                For Each masterName In masters
                    ' NO Encoding.ASCII: sustituye por '?' en silencio y el lector decodifica con la General.
                    ' Ver PluginEncodingSettings.EncodeMasterFileName — rehúsa en vez de escribir un master roto.
                    Dim masterBytes = PluginEncodingSettings.EncodeMasterFileName(masterName)
                    WriteSubrecordHeader(bw, "MAST", masterBytes.Length + 1)
                    bw.Write(masterBytes)
                    bw.Write(CByte(0))
                    WriteSubrecordHeader(bw, "DATA", 8)
                    bw.Write(0UL)
                Next

                ' INCC (Interior Cell Count, u32) is required on FO4 plugins: its correct value is the
                ' count of CELL records flagged Interior (DATA bit 0 = 1). NPC_Manager auto-gen plugins never
                ' contain CELL records, so
                ' INCC is always 0 — but the subrecord must be emitted (engine + CK validators
                ' expect it on FO4 ESPs).
                WriteSubrecordHeader(bw, "INCC", 4)
                bw.Write(0UI)
            End Using
            Dim bodyBytes = bodyMs.ToArray()

            Using ms As New MemoryStream()
                Using bw As New BinaryWriter(ms)
                    bw.Write(Encoding.ASCII.GetBytes("TES4"))
                    bw.Write(CUInt(bodyBytes.Length))
                    Dim flags As UInteger = 0UI
                    If markAsMaster Then flags = flags Or FLAG_ESM
                    If lightMaster Then flags = flags Or FLAG_ESL
                    bw.Write(flags)
                    bw.Write(0UI)               ' FormID always 0 for TES4
                    bw.Write(0UI)               ' VCS1
                    bw.Write(recordVersion)     ' Version
                    bw.Write(0US)               ' VCS2
                    bw.Write(bodyBytes)
                End Using
                Return ms.ToArray()
            End Using
        End Using
    End Function

    ''' <summary>The game's master file name. Public so callers building entries (NpcOverrideSaver's SSE
    ''' hair-colour materialization) can tell "this record lives in the game master, reusing it adds no
    ''' dependency" from "this record would drag a new plugin into the MAST list".</summary>
    Public Function MasterFileNamePublic(game As Config_App.Game_Enum) As String
        Select Case game
            Case Config_App.Game_Enum.Fallout4 : Return "Fallout4.esm"
            Case Config_App.Game_Enum.Skyrim : Return "Skyrim.esm"
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(game), $"Unsupported game: {game}")
        End Select
    End Function


    '==============================================================================================
    ' UN SOLO EMISOR
    '
    ' El orden de los subrecords, que campos van y de que tamano es cada uno salen de la
    ' declaracion del formato, no de una secuencia de llamadas escrita para cada tipo de record.
    '
    ' Eso borra la diferencia entre los tres modos que antes tenian cada uno su propia secuencia
    ' -crear uno nuevo, sobrescribir en un juego, sobrescribir en el otro-: los tres son el mismo
    ' recorrido sobre arboles que se armaron distinto. Un record sobrescrito reproduce los campos
    ' que traia la fuente porque su arbol viene de leerla, y uno nuevo arranca con los que el
    ' formato marca como obligatorios.
    '==============================================================================================

    ''' <summary>Serializa un record completo -cabecera y cuerpo- desde su arbol de campos.</summary>
    ''' <param name="vista">El record a grabar. Su contexto dice de que tipo y de que juego es.</param>
    ''' <param name="formID">Identificador del record EN EL ESPACIO DEL ORDEN DE CARGA. Se traduce
    ''' con el mismo mapa que las referencias del cuerpo, para que apunten todas al mismo lado.</param>
    ''' <param name="remapper">Traduce del espacio del orden de carga al del archivo que se escribe.</param>
    ''' <param name="selfIdxDestino">Indice de master con el que el archivo de SALIDA nombra a sus
    ''' propios records, o sea la cantidad de entradas de su MAST. -1 = no se sabe. Lo consume el
    ''' reindexado del cuerpo para poder re-emitir una referencia con la codificacion exacta que
    ''' traia la fuente cuando esa codificacion y la que devuelve la traduccion significan lo mismo
    ''' en el archivo destino; ver Canon.WbFormIdWalker.ReindexarADestino.</param>
    Public Function SerializarRecord(vista As Canon.CanonView,
                                     formID As UInteger,
                                     remapper As SaveNpcEspWriter.FormIdRemapper,
                                     game As Config_App.Game_Enum,
                                     vcs1 As UInteger,
                                     vcs2 As UShort,
                                     selfIdxDestino As Integer) As Byte()
        If vista Is Nothing OrElse vista.Node Is Nothing Then Return Array.Empty(Of Byte)()
        If vista.Context Is Nothing Then Return Array.Empty(Of Byte)()

        Dim traducir As Func(Of UInteger, UInteger) = Nothing
        If remapper IsNot Nothing Then traducir = Function(x) remapper(x)

        Dim cuerpo = Canon.CanonEscritura.Cuerpo(vista, traducir, selfIdxDestino)
        Dim idDestino = If(remapper Is Nothing, formID, remapper(formID))
        ' Las banderas salen del CONTEXTO del record y de ningun otro lado: ahi las dejo la lectura
        ' del original y ahi las cambia el editor, asi que lo que se graba es exactamente lo que se
        ' ve. Recibirlas por parametro obligaba a elegir entre dos fuentes y a inventar una regla
        ' para cuando no coinciden. El bit de comprimido se saca siempre: el cuerpo va sin comprimir.
        ' La version de formulario tambien sale del contexto: al leer el original queda ahi, y un
        ' record nuevo la deja en cero, que es lo que WrapRecord interpreta como "la del juego".
        Dim banderas = vista.Context.RecordFlags
        Return WrapRecord(vista.Context.RecordSignature, cuerpo,
                          banderas And Not FLAG_COMPRESSED, idDestino, vcs1, vcs2, game,
                          vista.Context.FormVersion)
    End Function

End Module
