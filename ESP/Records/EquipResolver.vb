Imports FO4_Base_Library.Canon.CanonInterpretacion
Imports System.Linq

''' <summary>LEY ÚNICA DE EQUIP. Quién sobrevive cuando dos prendas se pisan, y con qué máscara se
''' decide. Vive acá —al lado de <see cref="OutfitResolver"/>, que resuelve OTFT→ARMO— porque es ley del
''' MOTOR sobre registros: no necesita UI, ni GL, ni mallas, ni estado de ninguna app. Antes estaba
''' repartida en seis lugares (el render, el bake vía un delegado a un <c>Friend Shared</c> de un
''' formulario, el editor de outfits, el gate de piel, el chequeo de raza y el editor de ARMO), cada uno
''' recalculando la misma máscara a mano y divergiendo entre sí.
'''
''' ══ LA UNIDAD ES EL ARMO, NUNCA LA ARMA ══
''' RE del Fallout4.exe instalado (VAs re-localizadas por patrón; las de las memorias viejas ya no
''' resuelven en este build):
'''   · <c>SlotsOverlap 0x1402FCB00</c> = `mov eax,[rdx+8]; test [rcx+8],eax; setne al` — AND crudo, any-bit.
'''   · <c>AsBipedObjectForm 0x1402FCB60</c>: ARMO (`cmp al,0x1d`) → `+0x1E0` · ARMA (`cmp al,0x69`) → `+0x30`.
'''   · Tiene EXACTAMENTE 3 callers en todo el binario. Los dos de equip gatean `cmp byte [rcx+0x1a],0x1d`
'''     (sólo ARMO) y el walk `0x140990C70` compara `+0x1E0` contra `+0x1E0`: los DOS lados son el
'''     subobjeto biped del ARMO. Una ARMA nunca está en la lista de equipados.
''' ⇒ el mutex se decide con el BOD2 del ARMO (<see cref="ArmoFootprint.EquipMask"/>). Los bits que sólo
''' declara la ARMA (34 Forearms, 38 Calves, 41 LongHair…) gobiernan PARTICIONES, no equip.
'''
''' ══ LAS TRES MÁSCARAS ══ (un solo campo las mezclaba y cada lector entendía otra cosa)
'''   · <see cref="ArmoFootprint.EquipMask"/>      BOD2 crudo del ARMO      → mutex de equip.
'''   · <see cref="ArmoFootprint.GeometryMask"/>   unión de ARMA válidas    → particiones, segmentos, dedup.
'''   · <see cref="ArmoFootprint.OcclusionMask"/>  geometría ∪ (ARMO ∩ headwear) → oclusión de head-parts,
'''     categoría Headwear del toggle de render, cobertura de piel. Es la que el render venía usando como
''' `candidate.SlotMask` — salvo que el render le suma además los slots de oclusión que declara la
'''     RACE (`headOcclGate`, NpcMeshCollector.vb:396), así que para razas modeadas su candidate.SlotMask
'''     puede traer bits que este footprint no tiene. NO migrar el render a este campo sin contemplarlo.
''' La cobertura de piel se queda acá y NO sube a EquipMask: subirla le da a dos ARMO de guantes los
'''     bits 34/35 y vuelve la regresión histórica "broke hands".
'''
''' ══ EL JUEGO ENTRA UNA SOLA VEZ ══
''' Ni <see cref="Resolve"/> ni <see cref="BuildFootprint"/> reciben el juego por parámetro: lo leen de
''' <c>Config_App.Current.Game</c>, que es de donde ya lo lee <see cref="BipedSlots"/>. Pasarlo por
''' parámetro habilitaría construir con un juego y resolver con otro sin que nada falle ruidosamente.
''' </summary>
Public Module EquipResolver

    ' ════════════════════════════════════════════════════════════════════════════════════════════════
    ' LA LEY, VERIFICADA EN LOS DOS MOTORES (RE 2026-08-19 sobre los binarios instalados)
    ' ════════════════════════════════════════════════════════════════════════════════════════════════

    ''' <summary>Gana el ÚLTIMO equipado: el ítem nuevo entra y el motor DESEQUIPA al viejo con el que
    ''' choca. Verificado por desensamblado en LOS DOS juegos, no heredado de notas:
    '''
    ''' · FO4 (`Fallout4.exe` instalado, base 0x140000000) — resolver `0x140988CD0`: recorre los equipados
    '''   del actor (`[actor+0xF8]+0x58` datos / `+0x68` count, stride 0x10) y por cada uno llama a
    '''   `SlotsOverlap 0x1402FCB00`. Al solapar guarda el puntero del ítem **YA EQUIPADO** y baja el flag
    '''   de "sin conflicto"; en `0x140988EE8` le pasa ESE (el viejo) al despachador `0x140992FF0`, que por
    '''   la vtable del actor (`+0x368` → `0x140C9ACD0`) llega a `0x140D323B0`, que busca el nodo
    '''   `(forma, slot)` en la lista de ítems adjuntos y lo DESENLAZA Y LIBERA. El ítem nuevo queda.
    '''
    ''' · Skyrim SE 1.6.1170.0 (desempacado con Steamless; VAs cruzadas contra la Address Library
    '''   `versionlib-1-6-1170-0.bin`) — `Actor::AddWornItem 0x1406A0AA0` (slot 0x2B8 de la vtable de Actor):
    '''   recorre los 32 slots del caché de equipados y por cada ocupante que solapa llama a
    '''   `ActorEquipManager::UnequipObject 0x1406CA010` sobre EL VIEJO, y después marca el nuevo como worn.
    '''   El motor tiene además dos particularidades: (a) el ARMO de PIEL está exento de ser tratado como
    '''   ocupante desplazable — eso la app SÍ lo cumple, la piel ni entra al torneo
    '''   (`NpcMeshCollector.SelectWinningCandidates`); (b) si el ocupante viejo está protegido por quest la
    ''' llamada ABORTA y se rechaza el ítem NUEVO — eso la app NO lo modela: no hay estado de quest en
    '''   un editor, y la app siempre deja ganar al nuevo. PENDIENTE declarado, no cubierto.
    '''
    ''' Antes de este RE la app usaba first-wins en Skyrim, y eso es lo que dejaba a `Beem-Ja`
    ''' (`dunIronbindBarrowBeemJaOutfit`) con el torso desnudo: su INAM es botas[37], circlet[42],
    ''' guantes[33], anillo y túnica de mago[31,32,42]; con first-wins el circlet reclamaba el 42 y la
    ''' túnica ENTERA se caía. 98 de 2382 realizaciones vanilla SSE dependían de esto.</summary>
    ''' <summary>Y el ORDEN con el que se resuelve el torneo ES el del INAM, ascendente — también
    ''' verificado con bytes (SSE 1.6.1170.0), porque con last-wins el resultado depende enteramente de él:
    ''' `InitOutfitItems 0x14022E730` → worker `0x14023A2B0` recorre `BGSOutfit::outfitItems`
    ''' (data `+0x20`, count `+0x30`) hacia ADELANTE (`add rsi,8` / `cmp rsi,end`), y por cada ítem inserta
    ''' en el `entryList` de `InventoryChanges` con `0x14023B3A0`, que camina hasta `next==NULL` y hace
    ''' `[tail+8] = nuevo`: es APPEND, no prepend. Después `AddWornOutfit 0x1402E1B00` recorre ese mismo
    ''' `entryList` de cabeza a cola equipando cada entrada tageada con el FormID del outfit. Leer-adelante
    ''' + insertar-al-final + recorrer-adelante ⇒ el orden del INAM se preserva de punta a punta.
    '''
    ''' Lo que ESO todavía no explica: `DA13MissileOutfit` (30 NPC, los Afflicted de Bthardamz) lleva el
    ''' LVLI de sombrero DESPUÉS del de cuerpo, así que con last-wins un sombrero {31,42} desequipa una
    ''' túnica con capucha {31,32,42} y el NPC queda sin torso en el 3,2 % de las realizaciones. El orden es
    ''' el correcto y la dirección también, así que falta OTRA regla del motor. Único candidato vivo: una
    ''' pasada posterior de "el NPC se pone lo de mayor valor" en `0x1403BD380`, gateada por un contador
    ''' persistente del NPC_ (`TESNPC+0x241`, que arranca en 1, no en 0) y por un predicado virtual del
    ''' actor; no está confirmado que corra al instanciar un NPC genérico. Divergencia conocida y medida.</summary>

    ''' <summary>CON QUÉ MÁSCARA se decide el mutex: el BOD2 CRUDO DEL ARMO, en los DOS juegos. La ARMA
    ''' NUNCA entra — sus bits (34 Forearms, 38 Calves, 41 LongHair…) gobiernan particiones, no equip.
    '''
    ''' · FO4: `SlotsOverlap 0x1402FCB00` = `mov eax,[rdx+8]; test [rcx+8],eax; setne al`; sus llamadores de
    '''   equip gatean `cmp byte [rcx+0x1a],0x1d` (sólo ARMO) y `AsBipedObjectForm 0x1402FCB60` mapea
    '''   ARMO → `+0x1E0`.
    ''' · SSE: `SlotsOverlap 0x1401CCA90` (un único call-site en los 24 MB de `.text`, `0x1403BD5A2`) y
    '''   `AsBipedObjectForm 0x1401CCAF0` mapea ARMO(0x1A) → `+0x1B0` y ARMA(0x66) → `+0x30`, offsets que
    '''   coinciden byte a byte con `BGSBipedObjectForm` en CommonLibSSE. `Actor::AddWornItem` compara la
    '''   máscara propia del ítem nuevo contra la de cada ocupante; nunca toca `armorAddons`.
    '''
    ''' Lo único que se le saca es el slot 60, y SÓLO en FO4: es coexist-by-design (el Pip-Boy es el único
    ''' ítem 60-solo y equipa salteando la resolución de conflicto; ~todo outfit declara 60 para el swap
    ''' 60/160 y mutexea igual por el 33). En Skyrim el 60 es un slot MOD genérico y no se toca.</summary>
    Public Function MutexMaskOf(it As EquipItem) As UInteger
        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim Then Return it.EquipMask
        Return it.EquipMask And Not BipedSlots.SLOT_PIPBOY
    End Function

    ' ════════════════════════════════════════════════════════════════════════════════════════════════
    ' FOOTPRINT — un solo recorrido de los armatures de un ARMO
    ' ════════════════════════════════════════════════════════════════════════════════════════════════

    ''' <summary>Resultado por armature del recorrido único. El render lo consume para NO volver a decidir
    ''' slots por su cuenta: itera esta lista en vez de <c>armo.ArmorAddons</c> y sigue resolviendo por su
    ''' lado lo que es suyo (mallas, material swaps, facebones, bone scale).</summary>
    Public Class ArmaFootprint
        Public ArmaFormID As UInteger
        ''' <summary><c>arma.SlotMask</c>, o el del ARMO cuando la ARMA no declara ninguno (regla única de
        ''' footprint por armature; era <c>MainForm.EffectiveArmaSlotMask</c>).</summary>
        Public GeometryMask As UInteger
        ''' <summary>La ARMA matchea la raza (RaceFormID o AdditionalRaces, con el redirect RNAM ya
        ''' resuelto por el caller en <see cref="EquipContext.EffectiveArmorRaces"/>).</summary>
        Public RaceOk As Boolean
        ''' <summary>Hay malla para el género pedido (con el fallback al otro género que hace el render).</summary>
        Public HasGenderMesh As Boolean
    End Class

    ''' <summary>Footprint completo de un ARMO para una (raza, género). Producido por UN recorrido.</summary>
    Public Class ArmoFootprint
        Public ArmoFormID As UInteger
        Public EquipMask As UInteger
        Public GeometryMask As UInteger
        ''' <summary>Unión de la geometría de TODOS los armatures que resuelven, SIN filtrar por raza ni
        ''' género (con el mismo fallback al BOD2 del ARMO cuando ninguno resuelve). Es el footprint del
        ''' REGISTRO, independiente del actor: lo consume el gate de piel, que pregunta "¿este ARMO cubre
        ''' el slot de cuerpo?" sobre el registro y no sobre lo que este actor puede ponerse.</summary>
        Public RecordGeometryMask As UInteger
        Public OcclusionMask As UInteger
        ''' <summary>Al menos un armature matchea raza Y tiene malla del género ⇒ este ARMO aporta algo a
        ''' ESTE actor. Es un GATE, no un adorno: el bake (ResolveOutfitHeadwearSlots) sólo ocluye
        ''' pelo/barba con footprints válidos, porque las máscaras de abajo caen a un fallback de display
        ''' cuando no hay ningún addon aplicable y ocluir con eso tapa pelo que el motor no tapa.</summary>
        Public Valid As Boolean
        ''' <summary>Descartado por el gate de power-armor (pieza de PA sobre una raza que no es de PA).
        ''' El render no lo colecta, así que tampoco compite.</summary>
        Public PowerArmorRejected As Boolean
        Public Addons As New List(Of ArmaFootprint)

    End Class

    ''' <summary>Lo que la ley necesita del mundo de afuera. Los resolvedores se INYECTAN (mismo patrón que
    ''' <see cref="OutfitResolver.LeveledListResolver"/>): así la librería no conoce la app y los drafts en
    ''' memoria del editor se resuelven por el mismo camino que los registros reales. Sin ellos se cae al
    ''' <see cref="PluginManager"/>.</summary>
    Public Class EquipContext
        Public PluginManager As PluginManager
        Public RaceFormID As UInteger
        Public IsFemale As Boolean
        ''' <summary>Raza del NPC + la cadena de redirect RACE.RNAM ("Armor Race"). La calcula el caller
        ''' porque la cadena es suya; el match por ARMA es aditivo (ver 23-armor-race-redirect-rnam).</summary>
        Public EffectiveArmorRaces As ICollection(Of UInteger)
        Public ArmoResolver As Func(Of UInteger, Canon.IArmo)
        Public ArmaResolver As Func(Of UInteger, Canon.IArma)
        ''' <summary>Gate de power-armor del caller (necesita el catálogo de keywords, que es suyo).
        ''' Nothing ⇒ sin gate.</summary>
        Public IsPowerArmorArmo As Func(Of UInteger, Boolean)
        Public IsPowerArmorRace As Boolean

        Friend Function Armo(fid As UInteger) As Canon.IArmo
            If ArmoResolver IsNot Nothing Then
                Dim a = ArmoResolver(fid)
                If a IsNot Nothing Then Return a
            End If
            If PluginManager Is Nothing Then Return Nothing
            Dim rec = PluginManager.GetRecord(fid)
            If rec Is Nothing OrElse rec.Header.Signature <> "ARMO" Then Return Nothing
            Return Canon.CanonRecords.Armo(rec, PluginManager)
        End Function

        Friend Function Arma(fid As UInteger) As Canon.IArma
            If ArmaResolver IsNot Nothing Then
                Dim a = ArmaResolver(fid)
                If a IsNot Nothing Then Return a
            End If
            If PluginManager Is Nothing Then Return Nothing
            Dim rec = PluginManager.GetRecord(fid)
            If rec Is Nothing OrElse rec.Header.Signature <> "ARMA" Then Return Nothing
            Return Canon.CanonRecords.Arma(rec, PluginManager)
        End Function
    End Class

    ''' <summary>El footprint de <paramref name="armoFid"/> para el contexto dado. ÚNICA fuente de las tres
    ''' máscaras: nadie más recorre armatures para calcular slots.
    ''' <paramref name="addonFormIDs"/> acota el recorrido a esos armatures, en ese orden — es lo que pasa el
    ''' render, que antes del footprint ya resolvió el AddonIndex efectivo (OBTS/OMOD) y sólo dibuja el grupo
    ''' con ese INDX. Nothing ⇒ todos los Models del ARMO, que es lo que quieren el catálogo, el editor y el
    ''' bake (footprint del ítem, independiente de qué variante toque).</summary>
    Public Function BuildFootprint(armoFid As UInteger, ctx As EquipContext,
                                   Optional addonFormIDs As ICollection(Of UInteger) = Nothing) As ArmoFootprint
        Dim fp As New ArmoFootprint With {.ArmoFormID = armoFid}
        If armoFid = 0UI OrElse ctx Is Nothing Then Return fp

        If ctx.IsPowerArmorArmo IsNot Nothing AndAlso Not ctx.IsPowerArmorRace Then
            If ctx.IsPowerArmorArmo(armoFid) Then
                fp.PowerArmorRejected = True
                Return fp
            End If
        End If

        Dim armo = ctx.Armo(armoFid)
        If armo Is Nothing Then Return fp

        Dim mascaraDelArmo = armo.SlotMaskDe()
        fp.EquipMask = mascaraDelArmo
        Dim headwearBits As UInteger = mascaraDelArmo And BipedSlots.HeadwearMaskForGame()

        ' recordSlot = footprint del REGISTRO (todos los armatures, sin filtrar por raza/género): sirve de
        ' fallback de DISPLAY para que un ítem no se lea "(none)" sólo porque este actor no lo puede usar.
        ' raceSlot = lo que el render efectivamente colecta. Se prefiere raceSlot; Valid sale de él.
        Dim recordSlot As UInteger = 0UI
        Dim raceSlot As UInteger = 0UI

        Dim walk As IEnumerable(Of UInteger)
        If addonFormIDs Is Nothing Then
            walk = armo.ComplementosDe()
        Else
            walk = addonFormIDs
        End If

        For Each armaFid In walk
            Dim arma As Canon.IArma = Nothing
            Try
                arma = ctx.Arma(armaFid)
            Catch
                Continue For
            End Try
            If arma Is Nothing Then Continue For

            Dim af As New ArmaFootprint With {.ArmaFormID = armaFid}
            af.GeometryMask = ArmaGeometryMask(arma, mascaraDelArmo)
            recordSlot = recordSlot Or af.GeometryMask

            af.RaceOk = ArmaMatchesRace(arma, ctx.RaceFormID, ctx.EffectiveArmorRaces)
            Dim genderMesh = If(ctx.IsFemale, arma.FemaleModelFilename, arma.MaleModelFilename)
            If genderMesh = "" Then genderMesh = If(arma.MaleModelFilename <> "", arma.MaleModelFilename, arma.FemaleModelFilename)
            af.HasGenderMesh = (genderMesh <> "")

            If af.RaceOk AndAlso af.HasGenderMesh Then
                fp.Valid = True
                ' El footprint de MÁSCARAS es la UNIÓN de los armatures aplicables. El dedup intra-ARMO
                ' (qué armature DIBUJA cada slot) NO vive acá: es del colector, que además acepta mallas que
                ' vienen del ARMO (robots) y el bypass del editor de ARMA. Escribirlo también acá sería una
                ' segunda copia de la misma regla, con otro criterio.
                raceSlot = raceSlot Or af.GeometryMask
            End If
            fp.Addons.Add(af)
        Next

        fp.RecordGeometryMask = If(recordSlot <> 0UI, recordSlot, mascaraDelArmo)
        fp.GeometryMask = If(raceSlot <> 0UI, raceSlot, fp.RecordGeometryMask)
        fp.OcclusionMask = fp.GeometryMask Or headwearBits
        Return fp
    End Function

    ''' <summary>Footprint geométrico de UN armature: su propio BOD2, o el del ARMO dueño cuando la ARMA no
    ''' declara ninguno. Átomo compartido — lo usan <see cref="BuildFootprint"/> y el editor de ARMO, que
    ''' muestra el slot efectivo de un addon contra el BOD2 que el usuario está editando (todavía sin
    ''' registro). Una sola definición de la regla ARMA-primero.</summary>
    Public Function ArmaGeometryMask(arma As Canon.IArma, owningArmoSlotMask As UInteger) As UInteger
        If arma Is Nothing Then Return owningArmoSlotMask
        Return If(arma.SlotMaskDe() <> 0UI, arma.SlotMaskDe(), owningArmoSlotMask)
    End Function

    ''' <summary>Match de raza por ARMA: la propia o AdditionalRaces, contra la raza del NPC más la cadena
    ''' de redirect que el caller ya resolvió. raza 0 ⇒ sin filtro (evita que un NPC cuya raza no resolvió
    ''' se renderice desnudo). Ver 23-armor-race-redirect-rnam.</summary>
    Public Function ArmaMatchesRace(arma As Canon.IArma, npcRaceFormID As UInteger,
                                    effectiveArmorRaces As ICollection(Of UInteger)) As Boolean
        If arma Is Nothing Then Return False
        Return ArmaMatchesRace(arma.Race, arma.RazasAdicionalesDe(), npcRaceFormID, effectiveArmorRaces)
    End Function

    ''' <summary>La misma regla, sobre los VALORES sueltos en vez de sobre un record.
    ''' <para>La necesita el editor: mientras el usuario está tocando los paneles todavía no hay
    ''' record que leer, y el identificador seguiría resolviendo a los valores de ANTES de la edición.
    ''' Que la forma con record delegue en ésta es lo que evita tener la ley escrita dos veces.</para></summary>
    Public Function ArmaMatchesRace(armaRace As UInteger, razasAdicionales As IEnumerable(Of UInteger),
                                    npcRaceFormID As UInteger,
                                    effectiveArmorRaces As ICollection(Of UInteger)) As Boolean
        If npcRaceFormID = 0UI Then Return True
        Dim razas As New List(Of UInteger)
        If razasAdicionales IsNot Nothing Then razas.AddRange(razasAdicionales)
        If armaRace = npcRaceFormID OrElse razas.Contains(npcRaceFormID) Then Return True
        If effectiveArmorRaces IsNot Nothing Then
            For Each r In effectiveArmorRaces
                If r <> npcRaceFormID AndAlso
                   (armaRace = r OrElse razas.Contains(r)) Then Return True
            Next
        End If
        Return False
    End Function

    ' ════════════════════════════════════════════════════════════════════════════════════════════════
    ' LA LEY — mutex entre ARMO equipados
    ' ════════════════════════════════════════════════════════════════════════════════════════════════

    ''' <summary>Un ARMO equipado. <see cref="Tag"/> es identidad del caller (el candidate del render, la
    ''' fila del editor): la ley no lo mira, lo devuelve.</summary>
    Public Class EquipItem
        Public ArmoFormID As UInteger
        ''' <summary>Secuencia de equipado: el orden del INAM del outfit. Ascendente = antes.</summary>
        Public Order As Integer
        Public EquipMask As UInteger
        Public GeometryMask As UInteger
        Public OcclusionMask As UInteger
        Public Tag As Object

        Public Shared Function FromFootprint(fp As ArmoFootprint, order As Integer, Optional tag As Object = Nothing) As EquipItem
            Return New EquipItem With {.ArmoFormID = fp.ArmoFormID, .Order = order, .EquipMask = fp.EquipMask,
                                       .GeometryMask = fp.GeometryMask, .OcclusionMask = fp.OcclusionMask, .Tag = tag}
        End Function
    End Class

    ''' <summary>Reglas que NO son del motor y por eso se piden explícitamente.</summary>
    Public Class EquipOptions
        ''' <summary>Excepción "underarmor extendido" (caso Bridget/DCGuard, pedida por el usuario contra el
        ''' clipping): una pieza que declara capa de abajo (BODY o [U]) Y ADEMÁS bits [A] reserva esos [A] y
        ''' blinda su máscara, así que una pieza [A]-pura posterior que los pise se descarta entera.
        ''' NO es ley del motor. Medido sobre 581 realizaciones vanilla FO4: se activa en 45, elimina a
        ''' alguien que el any-bit puro no eliminaría en 30, y en 12 el veredicto final difiere del motor.
        ''' Clasifica con <see cref="EquipItem.GeometryMask"/> porque su premisa es geométrica ("esta malla
        ''' ya cubre el brazo"): moverla a EquipMask hace calificar a ~105 ARMO vanilla que hoy no y empeora
        ''' 22 realizaciones, todas en contra. Inerte en Skyrim (no tiene capas [U]/[A]).</summary>
        Public ExtendedUnderarmorException As Boolean = True
    End Class

    Public Class EquipResolution
        Public ReadOnly Winners As New List(Of EquipItem)
        Public ReadOnly Losers As New List(Of EquipItem)
        ''' <summary>Unión de <see cref="EquipItem.OcclusionMask"/> de los ganadores. NO es la unión de
        ''' EquipMask: aguas abajo la consumen la cobertura de piel y la oclusión de head-parts, que razonan
        ''' sobre particiones (bits de la ARMA), no sobre el equip.</summary>
        Public OccupiedSlots As UInteger
    End Class

    ''' <summary>Resuelve el mutex entre los ARMO de un loadout. Any-bit sobre <see cref="EquipItem.EquipMask"/>
    ''' en los DOS juegos (ver el encabezado del módulo); la dirección la da <see cref="LastEquippedWins"/>.
    ''' EquipMask = 0 ⇒ el ítem NUNCA conflictúa: el motor hace `test` con 0 y da 0. (Medido: 0 ARMO con
    ''' BOD2=0 alcanzables desde un OTFT en Skyrim.esm y en Fallout4.esm; los que existen entran como piel,
    ''' que ni siquiera participa del torneo.)</summary>
    Public Function Resolve(items As IEnumerable(Of EquipItem), Optional options As EquipOptions = Nothing) As EquipResolution
        Dim res As New EquipResolution
        If items Is Nothing Then Return res
        Dim opts = If(options, New EquipOptions())
        Dim list = items.ToList()

        ' Sin bits con los que chocar, no hay torneo: entran verbatim y aportan su ocupación.
        For Each it In list.Where(Function(x) MutexMaskOf(x) = 0UI)
            res.Winners.Add(it)
        Next
        Dim contenders = list.Where(Function(x) MutexMaskOf(x) <> 0UI).ToList()

        Dim occupied As UInteger = 0UI      ' bits de EQUIP ya reclamados
        Dim reservedA As UInteger = 0UI
        Dim shielded As UInteger = 0UI
        Dim accepted As New List(Of EquipItem)

        ' Pasada 1a — underarmor extendido (regla nuestra, no del motor). Ascendente por Order.
        ' Clasifica y blinda con la GEOMETRÍA; reserva sobre los bits [A] geométricos.
        Dim aMask As UInteger = BipedSlots.RegionMask(BipedSlots.BipedRegion.Over)
        Dim uMask As UInteger = BipedSlots.RegionMask(BipedSlots.BipedRegion.Under)
        Const BODY_MASK As UInteger = &H8UI     ' bit 3 = slot 33 (FO4). En Skyrim aMask=0 ⇒ toda la pasada es inerte.
        Dim extendedSet As New HashSet(Of EquipItem)
        If opts.ExtendedUnderarmorException AndAlso aMask <> 0UI Then
            For Each it In contenders.OrderBy(Function(x) x.Order)
                Dim g = it.GeometryMask
                Dim hasUnderlayer = (g And BODY_MASK) <> 0UI OrElse (g And uMask) <> 0UI
                If Not (hasUnderlayer AndAlso (g And aMask) <> 0UI) Then Continue For
                extendedSet.Add(it)
                Dim m = MutexMaskOf(it)
                ' La MISMA máscara que acumula `occupied`. Mezclar las dos (EquipMask acá y MutexMaskOf
                ' allá) dejaba el bit 60 fuera de `occupied` para siempre ⇒ `freeBits` nunca daba 0 para un
                ' ítem que lo declarara y el guard de abajo quedaba decorativo: dos underarmor extendidos con
                ' BOD2 idéntico {33,41,60} ganaban LOS DOS (dos torsos dibujados uno sobre otro). Sin
                ' alcance vanilla (0 de 79 extended-underarmor de FO4 declaran el 60), pero alcanzable con
                ' mods y tildando el slot a mano en el editor de ARMO.
                Dim freeBits = m And Not occupied
                If freeBits = 0UI Then
                    res.Losers.Add(it)
                    Continue For
                End If
                occupied = occupied Or m
                shielded = shielded Or g
                reservedA = reservedA Or (g And aMask)
                accepted.Add(it)
            Next
        End If

        ' Pasada 1b — mutex atómico any-bit. Descendente por Order = gana el último equipado, que es la ley
        ' verificada en LOS DOS motores (ver LAST_EQUIPPED_WINS). Que `Order` sea el orden del INAM del
        ' outfit es premisa NUESTRA: el RE estableció la DIRECCIÓN, no en qué orden el motor equipa los
        ' ítems de un outfit. Ver el comentario de LAST_EQUIPPED_WINS.
        Dim ordered = contenders.Where(Function(x) Not extendedSet.Contains(x)).
                                 OrderByDescending(Function(x) x.Order).ToList()
        Dim acceptedRest As New List(Of EquipItem)
        For Each it In ordered
            If (it.GeometryMask And reservedA) <> 0UI Then res.Losers.Add(it) : Continue For
            If (it.GeometryMask And shielded) <> 0UI Then res.Losers.Add(it) : Continue For
            Dim m = MutexMaskOf(it)
            If (m And occupied) <> 0UI Then res.Losers.Add(it) : Continue For
            occupied = occupied Or m
            acceptedRest.Add(it)
        Next
        accepted.AddRange(acceptedRest)

        res.Winners.AddRange(accepted)
        Dim sorted = res.Winners.OrderBy(Function(x) x.Order).ToList()
        res.Winners.Clear()
        res.Winners.AddRange(sorted)
        For Each w In res.Winners
            res.OccupiedSlots = res.OccupiedSlots Or w.OcclusionMask
        Next
        Return res
    End Function

End Module
