Imports System.Drawing
Imports System.Text
Imports FO4_Base_Library.Canon.CanonInterpretacion

' ============================================================================
' Bethesda plugin (.esp/.esm) record parsers — Fallout 4 / Skyrim SSE.
'
' This file and related files in the same project (PluginReader.vb,
' PluginWriter.vb, PluginManager.vb, PluginStructures.vb, SaveNpcEspWriter.vb)
' parse and write the binary record formats of the Bethesda plugin format:
' record / subrecord layouts, struct field offsets and types, FormID positions,
' MAST cleanup semantics, and ESP/ESM/ESL flag conventions.
'
' El UNICO Parse<SIG> escrito a mano que queda vivo es ParseNPC, en ESTE archivo.
' ⛔ NO agregar mas: el resto del formato lo lee y lo escribe el motor de layout
' de ESP/Canon/, generado de las declaraciones de xEdit. Una familia de parsers
' a mano en paralelo al motor ya existio (ActorRecords.vb, AudioRecords.vb,
' WorldRecords.vb, ItemRecords.vb y cuatro mas) y termino en 5.337 lineas con
' 97 Parse<SIG> sin un solo llamador en el arbol.
' ============================================================================

Public Enum NPC_TemplateCategory As Integer
    Traits = 0
    Stats = 1
    Factions = 2
    SpellList = 3
    AIData = 4
    AIPackages = 5
    ModelAnimation = 6
    BaseData = 7
    Inventory = 8
    Script = 9
    DefaultPackageList = 10
    AttackData = 11
    Keywords = 12
End Enum


''' <summary>RaceMenu (.jslot) per-vertex head sculpt delta — vertex index + WORLD-space delta (already
''' divided by the .jslot sculptDivisor).</summary>
Public Class NPC_SculptVert
    Public Index As Integer
    Public Dx As Single
    Public Dy As Single
    Public Dz As Single
End Class

''' <summary>One RaceMenu (.jslot) per-shape sculpt block. A preset sculpts head + brows + eyes + mouth as
''' SEPARATE blocks, each tagged with <see cref="Host"/> — the shape's chargen morph .tri (HDPT NAM0=2, e.g.
''' FemaleHeadBrowsCharGen.tri). The render/bake route a block to its shape by matching Host to that shape's
''' chargen tri path (engine-faithful: this is the geometry identity skee serializes, not a vertex-count guess).</summary>
Public Class NPC_SculptPart
    Public Host As String = ""
    Public Verts As List(Of NPC_SculptVert) = New List(Of NPC_SculptVert)
End Class

''' <summary>RaceMenu (.jslot) NiOverride custom morph — a named chargen morph (CME_/EFM_) at a weight.</summary>
Public Class NPC_CustomMorph
    Public Name As String = ""
    Public Value As Single
End Class

''' <summary>Un NPC_ tal como lo trabaja la aplicacion: el record, mas lo poco que NO viene del
''' archivo.
'''
''' <para>El objeto NO copia el record: LO ES. <see cref="Record"/> es el arbol de campos que se
''' leyo del plugin, y cada campo se lee y se escribe por su propiedad generada
''' (<c>npc.Record.Race</c>, <c>npc.Record.HeadParts</c>, ...). ⛔ NO volver a repetir aca los ~141
''' campos del record con dos mapas escritos a mano para ir y volver: los campos que ningun mapa
''' copia -172 nodos entre los dos esquemas- se pierden al guardar sin que nada avise.</para>
'''
''' <para>Lo unico que queda aca es el estado que el archivo no tiene: de que juego y de que plugin
''' salio, y los datos del sidecar de RaceMenu (.jslot/.bssliders) que viajan pegados al NPC para
''' que el render y el horneado resuelvan lo mismo. <see cref="FormID"/> y <see cref="EditorID"/>
''' se guardan aparte porque identifican al NPC incluso cuando el record se materializo desde una
''' plantilla y su identidad no es la del arbol.</para></summary>
Public Class NPC_Data

    ''' <summary>El record. Todo lo que viene del archivo se lee y se escribe aca.</summary>
    Public Property Record As Canon.INpc

    ''' <summary>Identificador EN EL ESPACIO DEL ORDEN DE CARGA.</summary>
    Public FormID As UInteger

    ''' <summary>Identificador de editor. Se guarda aparte del record porque un NPC materializado
    ''' desde una plantilla conserva el suyo aunque su arbol venga de otro.</summary>
    Public EditorID As String = ""

    ''' <summary>Juego del que salio este record — decide el juego con el que se abrio el arbol y
    ''' con el que se va a emitir. Por defecto el de la sesion, para los que se crean en el editor.</summary>
    Public Game As Config_App.Game_Enum = Config_App.Current.Game

    ''' <summary>Plugin de origen.</summary>
    Public PluginName As String = ""

    ''' <summary>RaceMenu: textura de mascara de tinte propia por capa (indice de capa -> ruta).
    ''' No esta en el plugin -TINI/TINC/TINV/TIAS no llevan ruta-: vive en el sidecar .bssliders y
    ''' viaja aca para que el compositor use esa mascara en vez de la de la RACE, en el render y en
    ''' el horneado. Indice ausente = la mascara propia de la capa de la RACE.</summary>
    Public Property SseTintTexOverride As Dictionary(Of Integer, String) = Nothing

    ''' <summary>RaceMenu: deltas de escultura por vertice de la CABEZA, ya en coordenadas de mundo.
    ''' Se aplican encima de los morfos de NAM9/NAMA. Nothing = sin escultura. No esta en el plugin.</summary>
    Public Property SseSculptHead As List(Of NPC_SculptVert) = Nothing

    ''' <summary>RaceMenu: escultura por FORMA -cabeza, cejas, ojos y boca-, cada bloque enrutado a
    ''' su forma por <see cref="NPC_SculptPart.Host"/>. Reemplaza a <see cref="SseSculptHead"/>, que
    ''' solo cubria la cabeza. Vacio = se usa el de la cabeza. No esta en el plugin.</summary>
    Public Property SseSculptParts As List(Of NPC_SculptPart) = Nothing

    ''' <summary>RaceMenu: morfos propios (CME_/EFM_) del .jslot, aplicados por nombre contra el
    ''' .tri del editor de personaje. Nothing = ninguno. No esta en el plugin.</summary>
    Public Property SseCustomMorphs As List(Of NPC_CustomMorph) = Nothing

    ''' <summary>Skyrim: tinte de pelo ABSOLUTO de RaceMenu, empaquetado 0xRRGGBB (.jslot
    ''' <c>actor.hairColor</c>). Le gana al HCLF al resolver el material del pelo; Nothing = manda el
    ''' CLFM. Viaja aca para que el render y el horneado resuelvan el mismo color.
    ''' <para>No es un subrecord: no tiene campo propio en el NPC_. Al guardar se materializa en un
    ''' CLFM real y el HCLF apunta ahi -ver <c>SaveNpcEspWriter.ClfmRecordEntry</c>-. Ese es el unico
    ''' canal persistente: el motor vuelve a empujar el color del CLFM sobre cada material de tinte
    ''' de pelo en cada actualizacion del 3D del actor, asi que un color solo horneado en la malla se
    ''' pisaria en juego.</para></summary>
    Public Property SseHairColorRgb As Integer? = Nothing

    ''' <summary>Una copia independiente, para editar sin tocar el original: el record se CLONA, asi
    ''' que escribirle a la copia no toca el arbol del que salio -que puede ser el que quedo en la
    ''' cache-. El estado de sesion se comparte: no se edita aca.</summary>
    Public Function Copia() As NPC_Data
        Return New NPC_Data With {
            .Record = If(Record Is Nothing, Nothing, Canon.CanonInterpretacion.Copia(Record)),
            .FormID = FormID, .EditorID = EditorID, .Game = Game, .PluginName = PluginName,
            .SseTintTexOverride = SseTintTexOverride, .SseSculptHead = SseSculptHead,
            .SseSculptParts = SseSculptParts, .SseCustomMorphs = SseCustomMorphs,
            .SseHairColorRgb = SseHairColorRgb}
    End Function

    Public Overrides Function ToString() As String
        Dim nombre = If(Record IsNot Nothing, Record.Name, "")
        If nombre <> "" Then Return $"{nombre} [{EditorID}]"
        Return EditorID
    End Function
End Class


''' <summary>RACE morph group preset (MPPI -> MPPM morph name).
''' Maps MSDK preset keys to chargen TRI morph names.</summary>
Public Class RACE_MorphPresetDef
    Public Index As UInteger      ' MPPI = same as MSDK key in NPC record
    Public PresetName As String = ""  ' MPPN = display name (localized)
    Public MorphName As String = ""   ' MPPM = morph name in Chargen.tri
    ''' <summary>MPPT = FormID reference to a TXST (TextureSet) record that holds the
    ''' diffuse / normal / wrinkles / specular for this preset. When the NPC's MSDV
    ''' selects this preset via its MPPI hash, the engine uses this TXST's textures
    ''' to replace the corresponding region of the base face, gated by the parent
    ''' Morph Group's MPPK mask. This is how Bethesda implements per-region texture
    ''' swaps (e.g. "Arrugado" forehead -> SkinHeadFemaleOld TXST gated by Female
    ''' Forehead Mask).</summary>
    Public TextureFormID As UInteger = 0UI   ' MPPT -> TXST
    Public Playable As Boolean = True        ' MPPF
End Class

''' <summary>Grupo de morphs de la RACE: lista de MorphPresetDefs que comparten mascara de region facial
''' (MPPK) y nombre (MPGN), uno por region (frente, ojos, nariz, orejas, mejillas, boca, cuello).
''' <para>Modelo de seleccion, IMPORTANTE: el NPC NO lleva un indice de "preset elegido por grupo". Solo trae
''' pares MSDK/MSDV, y que una clave sea preset o slider se decide por lookup contra este RACE. Pueden
''' convivir varios presets del mismo grupo y el motor los aplica todos (CharGenInterface.cpp camina
''' morphSetData sin dedup por grupo, y el render hace lo mismo). El "un preset por grupo" es como lo presenta
''' la UI de chargen, no una regla del schema ni del motor.</para></summary>
Public Class RACE_MorphGroup
    Public Name As String = ""           ' MPGN = "Forehead", "Eyes", etc.
    Public MaskEnum As UShort = 0US      ' MPPK = u16 enum.
    '   Male:   1171..1177 = Forehead..Neck Mask
    '   Female: 1221..1227 = Forehead..Neck Mask
    '   65535 = None
    ' Bethesda comment: "Maps to Faceregion tint groups".
    ' These are SEMANTIC IDs, not array indices. Each value
    ' maps by convention to a Slot (0..6) in TETI.Slot of the
    ' RACE's tint template options. Use TryGetMaskSlot below.
    Public Presets As New List(Of RACE_MorphPresetDef)
    Public SliderIndices As New List(Of UInteger)  ' MPGS = additional slider MSDK keys

    ''' <summary>Translate the MPPK u16 enum to a TintSlot (0..6) for the region masks.
    ''' Returns False if the value is 65535 (None) or out of range.
    ''' MPPK base values — region masks are stored as a
    ''' contiguous u16 range starting at MaleMaskBase / FemaleMaskBase, in the order
    ''' Forehead, Eyes, Nose, Ears, Cheeks, Mouth, Neck = TintSlot 0..6.</summary>
    Public Function TryGetMaskSlot(ByRef slot As TintSlot) As Boolean
        Const MaleMaskBase As UShort = 1171US
        Const FemaleMaskBase As UShort = 1221US
        Const RegionCount As UShort = 7US
        Dim offset As Integer
        If MaskEnum >= MaleMaskBase AndAlso MaskEnum < MaleMaskBase + RegionCount Then
            offset = CInt(MaskEnum) - CInt(MaleMaskBase)
        ElseIf MaskEnum >= FemaleMaskBase AndAlso MaskEnum < FemaleMaskBase + RegionCount Then
            offset = CInt(MaskEnum) - CInt(FemaleMaskBase)
        Else
            Return False
        End If
        slot = CType(offset, TintSlot)
        Return True
    End Function
End Class

Public Enum TintSlot As UShort
    ForeheadMask = 0
    EyesMask = 1
    NoseMask = 2
    EarsMask = 3
    CheeksMask = 4
    MouthMask = 5
    NeckMask = 6
    LipColor = 7
    CheekColor = 8
    Eyeliner = 9
    EyeSocketUpper = 10
    EyeSocketLower = 11
    SkinTone = 12
    Paint = 13
    LaughLines = 14
    CheekColorLower = 15
    Nose = 16
    Chin = 17
    Neck = 18
    Forehead = 19
    Dirt = 20
    Scars = 21
    FaceDetail = 22
    Brows = 23
    Wrinkles = 24
    Beards = 25
End Enum

''' <summary>RACE <c>MPAI</c>/<c>MPAV</c> "Available Morphs" — <b>SKYRIM-only</b>, por GÉNERO: el bitmask de
''' tipos NAMA (Nose/Brow/Eyes/Lip) que el <b>CREATION KIT ofrece en su desplegable</b> para esa raza.
''' <para><b>NO es lo que el motor ACEPTA, y por lo tanto NUNCA se usa como FILTRO.</b> El aplicador de
''' NAMA resuelve por NOMBRE contra el chargen <c>.tri</c> y es CIEGO a este bitmask
''' (<c>ApplyChargenMorph_Hooked</c>, skee64 <c>SKEEHooks.cpp:730-749</c>; y ver el comentario de
''' <c>NpcMorphResolver.AddNamaTypePreset</c>). MEDIDO sobre el corpus SSE: de 90 valores NAMA que su raza
''' NO declara, <b>75 EXISTEN igual en las head parts de ese NPC</b> y el juego se los aplica — entre ellos
''' NPC vanilla como <c>HighElfFemalePreset01</c> (NoseType7, que HighElfRace no declara). Filtrar por esto
''' rompería la edición de esos NPC. Sirve SÓLO para ANOTAR ("el CK no ofrece este tipo para esta raza").</para>
''' <para>El índice de familia NO es posicional: cada <c>MPAV</c> viene precedido por un <c>MPAI</c> de 4 bytes
''' con el índice explícito (0=Nose 1=Brow 2=Eyes 3=Lip) — medido, aunque no tenga nombre
''' documentado (aparece como campo sin identificar). Por eso
''' un bloque ausente sólo se pierde a sí mismo: los demás siguen siendo correctos.</para></summary>
Public Class RACE_AvailableMorphs
    Public Const FamilyCount As Integer = 4
    ''' <summary>¿Vino el bloque de esta familia? Sin esto, "no declara ninguno" y "no vino el dato" serían
    ''' el mismo cero, y la anotación afirmaría sobre el CK algo que no se leyó.</summary>
    Public ReadOnly Present As Boolean() = New Boolean(FamilyCount - 1) {}
    ''' <summary>Tipos 0..31.</summary>
    Public ReadOnly BitsLo As UInteger() = New UInteger(FamilyCount - 1) {}
    ''' <summary>Tipos 32..38. SÓLO Eyes los tiene: su MPAV es u32 + <b>u8</b>, mientras Nose/Brow/Lip
    ''' son u32 + relleno. Leer "el primer u32" en las cuatro perdería
    ''' EyesType32-38 en silencio.</summary>
    Public ReadOnly BitsHi As UInteger() = New UInteger(FamilyCount - 1) {}

    ''' <summary>¿El CK ofrece este tipo para esta raza+género? <c>False</c> también cuando el bloque no vino
    ''' (consultar <see cref="Present"/> para distinguir).
    ''' <para>El valor 0 ("Default") se reporta SIEMPRE como ofrecido: no está mapeado por bit. MEDIDO: 40
    ''' valores NAMA=0 del corpus caen en razas cuyo bit 0 está APAGADO, así que el bit 0 no representa al
    ''' valor 0.</para></summary>
    Public Function Offers(familyIndex As Integer, value As UInteger) As Boolean
        If familyIndex < 0 OrElse familyIndex >= FamilyCount Then Return False
        If Not Present(familyIndex) Then Return False
        If value = 0UI Then Return True
        If value < 32UI Then Return (BitsLo(familyIndex) And (1UI << CInt(value))) <> 0UI
        If value > 38UI Then Return False
        Return (BitsHi(familyIndex) And (1UI << CInt(value - 32UI))) <> 0UI
    End Function
End Class

''' <summary>Shared, cheap, robust race-level helpers. Canonical home for the FaceGen-eligibility
''' gate so that BOTH the FaceGen bake and the FaceGen-only UI buttons (Edit Face, bake-this-NPC, etc.)
''' agree on a single discriminator instead of weaker signals (head-part presence, race name).</summary>
Public Class RaceUtil
    ''' <summary>True when the given race is a FaceGen race (has a head/face to bake &amp; edit), keyed off
    ''' the RACE.DATA "FaceGen Head" flag (bit 0x2, version-aware). This is the canonical, 0-exception
    ''' discriminator: set on Human/Ghoul/Child/SuperMutant/Synth/PowerArmor races (+DLC variants),
    ''' CLEAR on dogs/creatures/robots/turrets/feral ghouls/HumanRaceSubGraphData/AlienRace/
    ''' SupermutantBehemothRace/DefaultRace/LibertyPrimeRace. Returns False for a missing/zero FormID
    ''' or a record that isn't a RACE — so it is safe to call as a preventive gate without throwing.
    ''' See [[24-anim-behavior-por-raza]].</summary>
    Public Shared Function RaceSupportsFaceGen(raceFormID As UInteger, pm As PluginManager) As Boolean
        If raceFormID = 0UI OrElse pm Is Nothing Then Return False
        Dim rec = pm.GetRecord(raceFormID)
        If rec Is Nothing OrElse rec.Header.Signature <> "RACE" Then Return False
        Dim race = Canon.CanonRecords.Race(rec, pm)
        Dim fo4 = TryCast(race, Canon.RaceFO4)
        If fo4 IsNot Nothing Then Return fo4.DataFlagsFaceGenHead
        Dim sse = TryCast(race, Canon.RaceSSE)
        Return sse IsNot Nothing AndAlso sse.FlagsFaceGenHead
    End Function

    ''' <summary>Convert a RACE.DATA "biped object" value to a slot-30-relative bit (bit N = biped slot 30+N,
    ''' the same convention as the app's occupiedSlots mask). Value v -> (1 &lt;&lt; v) when 0&lt;=v&lt;=31, else 0
    ''' (None: -1 or v&gt;31). Verified engine rule.</summary>
    Private Shared Function BipedValueToBit(v As Integer) As UInteger
        Return If(v >= 0 AndAlso v <= 31, 1UI << v, 0UI)
    End Function

    ''' <summary>Face-cull slot mask (A) for a race — the full-face slot whose coverage hides the
    ''' whole head (HumanRace=2 -> slot 32). slot-30-relative bit; 0 when None. FO4: reinterpreta
    ''' Unknown Bytes1 del DATA como s32
    ''' (<see cref="CanonInterpretacion.OcclusionFaceCullBipedDe"/>). SSE: Head Biped Object.
    ''' </summary>
    Public Shared Function RaceFaceCullMask(race As Canon.IRace) As UInteger
        If race Is Nothing Then Return 0UI
        Dim fo4 = TryCast(race, Canon.RaceFO4)
        If fo4 IsNot Nothing Then Return BipedValueToBit(fo4.OcclusionFaceCullBipedDe())
        Dim sse = TryCast(race, Canon.RaceSSE)
        ' Campo ausente = la raza NO reserva ranura, que no es lo mismo que reservar la cero: sin
        ' esto un record que no lo trae termina prendiendo el bit de la primera ranura.
        If sse IsNot Nothing Then
            If Not sse.HeadBipedObjectPresente Then Return 0UI
            Return BipedValueToBit(sse.HeadBipedObject)
        End If
        Return 0UI
    End Function

    ''' <summary>Hair slot mask (B) for a race. GAME-AWARE:
    ''' • FO4: the hair channel covers BOTH 30+B AND 30+B+1 (HumanRace B=0 -> slots 30 &amp; 31,
    '''   the two FO4 hair slots HairTop/HairLong). Sale de Unknown Bytes2 reinterpretado como s32.
    ''' • SSE: a SINGLE slot 30+B (the byte-level engine reader at [race+0x130] tests one slot;
    '''   Skyrim hair = slot 31, so B=1 -> slot 31, and 30+B+1 would wrongly add slot 32=Body).
    '''   Sale de Hair Biped Object.
    ''' Bits bounded to 0..31. 0 when None (B=-1 / >31).</summary>
    Public Shared Function RaceHairMask(race As Canon.IRace) As UInteger
        ' Se DERIVA de los dos bits; la ley del canal se declara una sola vez, abajo.
        Return RaceHairFirstBit(race) Or RaceHairSecondBit(race)
    End Function

    ''' <summary>Primer bit del canal de pelo, el que el motor llama B.
    ''' <para>FO4: <c>Unknown Bytes2</c> del RACE.DATA, que el driver lee en
    ''' <c>0x140506702 mov r13d,[r13+0x1b4]</c> y usa como tag <c>B+30</c> en la 1ª llamada
    ''' (<c>0x140506733</c>).</para>
    ''' <para>SSE: <c>Hair Biped Object</c>, leído en <c>0x1403C2A6A mov ecx,[r15+0x130]</c>.</para>
    ''' 0 cuando la raza no reserva canal.</summary>
    Public Shared Function RaceHairFirstBit(race As Canon.IRace) As UInteger
        If race Is Nothing Then Return 0UI
        Dim fo4 = TryCast(race, Canon.RaceFO4)
        If fo4 IsNot Nothing Then Return BipedValueToBit(fo4.OcclusionHairBipedDe())
        Dim sse = TryCast(race, Canon.RaceSSE)
        If sse IsNot Nothing Then
            If Not sse.HairBipedObjectPresente Then Return 0UI
            Return BipedValueToBit(sse.HairBipedObject)
        End If
        Return 0UI
    End Function

    ''' <summary>Segundo bit del canal de pelo (B+1). ⭐ SÓLO existe en Fallout 4: el driver arma el segundo
    ''' tag con <c>0x14050677B lea esi,[r13+1]</c> y hace una SEGUNDA llamada sobre el MISMO nodo
    ''' (<c>0x1405067A3</c>, con el nodo recargado de <c>[rsp+0x90]</c> en <c>0x140506790</c>).
    ''' <para>Skyrim tiene un solo canal y una sola llamada (<c>0x1403C2A9D call 0x1403CC770</c>), así que
    ''' acá devuelve 0 — sumar B+1 allá metería el slot 32 (Body) en el canal de pelo.</para>
    ''' <para>⛔ Es RELATIVO A LA RAZA, no la constante 31: por eso no se usa
    ''' <c>BipedSlots.SlotBitHairLong</c>. Medido sobre los 7 plugins vanilla de FO4: B vale −1 en 70 razas,
    ''' 1 en 30 y 0 en 10, y las 30 con B=1 son animales, robots, Power Armor y Vertibird. Lo MEDIDO sobre
    ''' ellas es que ningún <c>NPC_</c> de una raza con B ≠ 0 trae head part de tipo 3, así que en vanilla
    ''' el canal de pelo humano es siempre {30,31}.</para></summary>
    Public Shared Function RaceHairSecondBit(race As Canon.IRace) As UInteger
        Dim fo4 = TryCast(race, Canon.RaceFO4)
        If fo4 Is Nothing Then Return 0UI
        ' ⛔ SIN GUARD DE b < 0, Y ES A PROPÓSITO. "Ninguno" se codifica −1 y −1+1 = 0, o sea el slot 30 —
        ' y eso es EXACTAMENTE lo que hace el motor. Los dos guards del driver son POR LLAMADA y sólo el
        ' de la PRIMERA descarta el −1:
        '   1ª:  0x140506702 mov r13d,[r13+0x1b4]   ; B = −1
        '        0x140506714 cmp r13d,0x1f / ja     ; 0xFFFFFFFF > 0x1f ⇒ máscara 0, canal muerto
        '   2ª:  0x14050677B lea esi,[r13+1]        ; B+1 = 0
        '        0x140506782 cmp esi,0x1f / ja      ; 0 <= 0x1f ⇒ NO salta
        '        0x140506789 mov eax,1 / shl eax,cl ; máscara = bit 0 = slot 30
        ' ⇒ una raza con B = −1 igual tiene segundo canal, y es el slot 30. Poner el guard acá le sacaba
        ' al motor una rama que sí ejecuta.
        Return BipedValueToBit(fo4.OcclusionHairBipedDe() + 1)
    End Function

    ''' <summary>Máscara de la ranura del vello facial de una raza.
    ''' <para>Sale del entero con signo que el struct de datos declara para eso. En Skyrim ese
    ''' campo no existe: la raza no reserva ranura para vello facial.</para></summary>
    Public Shared Function RaceFacialHairMask(race As Canon.IRace) As UInteger
        Dim fo4 = TryCast(race, Canon.RaceFO4)
        If fo4 Is Nothing Then Return 0UI
        ' El formato declara este campo recien a partir de cierta version, asi que en un record mas
        ' viejo el nodo NO existe y la raza no reserva ranura para vello facial.
        If Not fo4.DataBeardBipedObjectPresente Then Return 0UI
        Return BipedValueToBit(fo4.DataBeardBipedObject)
    End Function

    ''' <summary>The union of the three head-part occlusion slot masks (face-cull A | hair B | facial-hair C)
    ''' for a race. This is the per-NPC, RACE-driven replacement for the old fixed HeadwearOcclusionSlots
    ''' const: the slice of the rendered worn-slot set that can hide head parts for THIS race.</summary>
    Public Shared Function RaceHeadOcclusionMask(race As Canon.IRace) As UInteger
        Return RaceFaceCullMask(race) Or RaceHairMask(race) Or RaceFacialHairMask(race)
    End Function

    ''' <summary>The biped slot this race reserves for the Pipboy device, as a slot-30-relative bit mask.
    ''' SOURCE: RACE.DATA 'Pipboy Biped Object' — the slot is PER-RACE data, so code that needs "the
    ''' Pipboy slot" must ask the race instead of assuming the constant slot 60 (BipedSlots.SLOT_PIPBOY).
    ''' 0 when the race declares None. FO4 only: the Skyrim RACE DATA layout has no such field, and
    ''' there slot 60 is a generic modular slot, so this returns 0 and callers keep the raw mask (see
    ''' NpcRenderHost.ApplyRenderToggleVisibility).</summary>
    Public Shared Function RacePipboyMask(race As Canon.IRace) As UInteger
        If race Is Nothing Then Return 0UI
        If IsSkyrim() Then Return 0UI
        Dim fo4 = TryCast(race, Canon.RaceFO4)
        If fo4 Is Nothing Then Return 0UI
        If Not fo4.DataPipboyBipedObjectPresente Then Return 0UI
        Return BipedValueToBit(fo4.DataPipboyBipedObject)
    End Function
End Class

''' <summary>Pareja (Addon Index, ARMA FormID) preservando el INDX. El AddonIndex es la clave que
''' los OMODs usan en su Property "AddonIndex" para seleccionar QUÉ addon de esta lista
''' renderizar — Lite/Mid/Heavy típicamente. ParseARMO conserva la pareja para que el resolver
''' pueda buscar por índice.</summary>
Public Class ARMO_AddonEntry
    Public AddonIndex As UShort
    Public ArmaFormID As UInteger
End Class

''' <summary>One ARMO Damage Type Array (DAMA) entry.
''' FO4 stride = 8 bytes: Type FormID [DMGT] @0 + Amount u32 @4. (The Curve Table field is FromVersion 152
''' = FO76/SF1 only, never present in FO4, so it is not modelled.) DamageTypeFormID resolved to GLOBAL at parse.</summary>
Public Class ARMO_DamageResist
    Public DamageTypeFormID As UInteger
    Public Value As UInteger
End Class


''' <summary>Per-bone scale delta from an ARMA record's BSMS subrecord: each ARMA can ship its own
''' "Bone Scale Modifier Set" with per-gender per-bone Vec3 deltas that the engine adds on top of
''' RACE.BSMS scaling. Used to shape outfits around the body (e.g. cinched waist, wider hip
''' extension).</summary>
Public Class ARMA_BoneScaleDelta
    Public BoneName As String = ""
    Public DeltaX As Single = 0.0F
    Public DeltaY As Single = 0.0F
    Public DeltaZ As Single = 0.0F
End Class

''' <summary>Per-gender ARMA bone scale modifier block (opened by BSMP in ARMA record).</summary>
Public Class ARMA_BoneScaleGender
    Public Gender As UInteger   ' 0 = masculino, 1 = femenino
    Public Bones As New List(Of ARMA_BoneScaleDelta)
End Class














Public Module RecordParsers

    ''' <summary>Reinterpret a raw byte as a signed 8-bit value (s8 / SByte). Direct CSByte(b)
    ''' overflows when bit 7 is set because VB does a checked narrowing conversion that requires
    ''' the value to fit in [-128, 127]. We need bit-pattern reinterpret: 0xFF → -1, 0x80 → -128.
    ''' Used wherever a field is a signed byte (Faction.Rank, REVB filters, PSDT schedule,
    ''' WRLD rank, etc.).</summary>
    Public Function ReadInt8(b As Byte) As SByte
        Return If(b < 128, CSByte(b), CSByte(CInt(b) - 256))
    End Function

    Public Function ReadOptionalFloat(data As Byte(), offset As Integer) As Single?
        If data Is Nothing OrElse data.Length < offset + 4 Then Return Nothing
        Dim raw = BitConverter.ToUInt32(data, offset)
        If raw = &H7F7FFFFFUI OrElse raw = &HFF7FFFFFUI Then Return Nothing
        Dim v = BitConverter.ToSingle(data, offset)
        If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Return Nothing
        Return v
    End Function

    Private Function ResolveDisplayString(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager, Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If pluginManager Is Nothing Then Return sr.AsString
        Return pluginManager.ResolveFieldString(rec, sr, kind)
    End Function

    ''' <summary>Gate de juego GLOBAL de estos parsers (el juego de la sesión, <c>Config_App.Current.Game</c>).
    ''' Es el ÚNICO lugar donde se hace ese chequeo: ⛔ no repetirlo inline.
    ''' <para>El <c>IsNot Nothing</c> es CINTURÓN, no el arreglo de un crash: <c>Config_App.Current</c> se
    ''' inicializa en su propia declaración (<c>Config_Class.Current</c>) y su único otro asignador escribe
    ''' dentro de un <c>If cfg IsNot Nothing</c>, así que hoy no puede ser Nothing. Está para quitar
    ''' repetición, no para tapar un NullReferenceException.</para>
    ''' <para>Justamente porque el caso nulo NO ocurre, no se lo usa para elegir rama: los gates de
    ''' <c>MO2S/MO3S/MO4S/MO5S</c> preguntan <see cref="IsFallout4"/> (positivo) y no <c>Not IsSkyrim()</c>.
    ''' Con la forma negativa, un hipotético <c>Current</c> nulo caería en la rama FO4 y ahí esos
    ''' subrecords —que en Skyrim son un ARRAY de Alternate Textures— se leerían como FormID, ensuciando
    ''' la master list (ver el comentario de cada Case). Preguntar en positivo hace que el caso imposible
    ''' sea INERTE en vez de destructivo. Y el default de <c>Config_App.Game</c> es Skyrim, no FO4.</para>
    ''' <para>NO unificar acá los gates que miran el juego del PROPIO record (<c>npc.Game</c>, o un
    ''' parámetro <c>game As Config_App.Game_Enum</c>): ésos describen el record que se está parseando, que
    ''' puede no ser el juego de la sesión. Son otra pregunta, no una copia de ésta.</para></summary>
    Friend Function IsSkyrim() As Boolean
        Return Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim
    End Function

    ''' <summary>Gemelo de <see cref="IsSkyrim"/> para la rama FO4. Ver ahí la nota sobre qué NO unificar.</summary>
    Friend Function IsFallout4() As Boolean
        Return Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Fallout4
    End Function

    ''' <summary>Nombre local, histórico, de <see cref="ParserHelpers.ResolveFIDRaw"/>. Es un REENVÍO, no
    ''' una segunda implementación: la política de "sin PluginManager el FormID vuelve crudo" vive en UN
    ''' solo lugar (ParserHelpers), igual que la LEY de conversión vive en un solo lugar
    ''' (PluginManager.ResolveReferencedFormID → ResolveFormID → MakeGlobalFormID).
    ''' <para>⛔ NO reponerle un cuerpo propio: los demás archivos de <c>Records\</c> no ven la versión
    ''' <c>Private</c> de este Module, y esa falta de accesibilidad es justo lo que tienta a copiarlo —con
    ''' el resultado de que la política de nulos queda escrita dos veces y sólo diverge el día que alguien
    ''' toque una. El NOMBRE se conserva para no mover 127 call sites (churn sin beneficio).</para></summary>
    Private Function ResolveFormIDReference(rec As PluginRecord, rawFormID As UInteger, pluginManager As PluginManager) As UInteger
        Return ParserHelpers.ResolveFIDRaw(rec, rawFormID, pluginManager)
    End Function

    ''' <summary>Reenvío a <see cref="ParserHelpers.ResolveFID"/>. Ver la nota del overload de arriba.</summary>
    Private Function ResolveFormIDReference(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As UInteger
        Return ParserHelpers.ResolveFID(rec, sr, pluginManager)
    End Function

    ''' <summary>Abre el record NPC_ y lo envuelve con el estado de sesion.
    ''' <para>No copia nada: el objeto ES el record. Lo que se agrega es lo que el archivo no tiene
    ''' -de que juego y de que plugin salio- y la identidad, que un NPC materializado desde una
    ''' plantilla conserva aunque su arbol venga de otro.</para></summary>
    Public Function ParseNPC(rec As PluginRecord, pluginManager As PluginManager) As NPC_Data
        Dim n = Canon.CanonRecords.Npc(rec, pluginManager)
        If n Is Nothing Then Return Nothing
        Return New NPC_Data With {.Record = n, .FormID = n.FormID, .EditorID = n.EditorID,
                                  .PluginName = rec.SourcePluginName, .Game = Config_App.Current.Game}
    End Function

    ''' <summary>RACE-AGNOSTIC face-morph driver: resolve the race's KWDA keyword EditorIDs. The SSE face plan
    ''' applies a chargen morph named "&lt;keyword&gt;Morph" at full weight for EACH — so a race carrying the
    ''' "Vampire" KYWD gets "VampireMorph", and any race+morph naming pairing works the same. It is NOT a
    ''' vampire special case: the code never names vampire; it just follows the keyword→morph convention, and
    ''' AddNam9Channel no-ops when no morph of that name exists (which is every vanilla race except vampires,
    ''' whose chargen tris ship "VampireMorph"). This is what the CK bakes for pre-placed vampire NPCs, whose
    ''' NAM9[18] (the per-actor vampire slider) is FLT_MAX — i.e. driven by the RACE keyword, not the slider.
    ''' What a vampire race "does that the rest don't" is precisely: carry a keyword whose name has a morph.</summary>
    Public Function GetRaceKeywordEditorIds(race As Canon.IRace, pm As PluginManager) As List(Of String)
        Dim result As New List(Of String)
        If race Is Nothing OrElse race.Keywords Is Nothing OrElse pm Is Nothing Then Return result
        For Each kw In race.Keywords
            If kw.Keyword = 0UI Then Continue For
            Dim rec = pm.GetRecord(kw.Keyword)
            If rec IsNot Nothing AndAlso rec.Header.Signature = "KYWD" AndAlso Not String.IsNullOrEmpty(rec.EditorID) Then
                result.Add(rec.EditorID)
            End If
        Next
        Return result
    End Function

    Public Function ResolveMorphRaceEditorId(race As Canon.IRace, pm As PluginManager) As String
        If race Is Nothing Then Return ""
        Dim own = If(race.EditorID, "")
        If race.MorphRace = 0UI OrElse pm Is Nothing Then Return own
        Dim rec = pm.GetRecord(race.MorphRace)
        If rec Is Nothing OrElse rec.Header.Signature <> "RACE" Then Return own
        Dim eid = If(rec.EditorID, "")
        Return If(String.IsNullOrEmpty(eid), own, eid)
    End Function











End Module

