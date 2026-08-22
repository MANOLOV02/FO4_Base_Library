Imports System.IO
Imports System.Text.Json

''' <summary>
''' Registro de LUTs de pelo de LooksMenu: <c>Data\F4SE\Plugins\F4EE\LUTs\&lt;pluginFileName&gt;\haircolors.json</c>.
'''
''' <para>POR QUÉ EXISTE. Un <c>CLFM</c> de pelo de FO4 no lleva la textura de paleta: lleva la FILA
''' (<c>CNAM</c> como float, con el bit <c>FNAM</c> 0x2 RemappingIndex) y la textura sale del RACE
''' (<c>HLTX</c>) o del BGSM del peinado. Con una sola LUT el techo son las filas de esa textura —
''' vanilla usa 32 CLFM sobre <c>haircolor_lgrad_d.dds</c>, en <c>(i+0.5)/32</c>. Para pasar de ahí,
''' LooksMenu ata cada color form a SU PROPIA textura de paleta vía este JSON. Sin leerlo, N colores
''' que comparten fila se renderizan idénticos: el mod "512 Standalone Hair Colors" tiene 512 CLFM
''' con sólo 16 valores de fila distintos (32 LUTs × 16 filas) — el discriminador es la LUT, no la fila.</para>
'''
''' <para>FIEL A f4ee (F4SEPlugins-master/f4ee/CharGenInterface.cpp):</para>
''' <list type="bullet">
''' <item><c>LoadHairColorMods</c> (:637) — un <c>haircolors.json</c> por plugin cargado, en load order.
'''   NO hay carpeta <c>Loose\</c> (a diferencia de Overlays/Skin).</item>
''' <item><c>LoadHairColorData</c> (:1211) — <c>Form</c> es el FormID LOCAL (string o array de strings);
'''   el mapa se indexa SÓLO por color form: <c>Races</c>/<c>Gender</c> no eligen la LUT, sólo deciden en
'''   qué <c>race-&gt;chargenData[i]-&gt;colors</c> se empuja el color (el catálogo del selector). El registro
'''   en el mapa igual EXIGE que al menos una (raza, género) resuelva — está adentro de ese bucle (:1298-1313).</item>
''' <item><c>m_LUTMap.emplace</c> ⇒ gana el PRIMER plugin que registre un color form dado, no el último.</item>
''' <item>La comparación de paths es case-insensitive: <c>m_LUTs</c> es un set de <c>F4EEFixedString</c>,
'''   que hashea y compara con <c>_stricmp</c> (StringTable.h:21-45).</item>
''' <item>Todo el subsistema está gateado por <c>[CharGen] bExtendedLUTs</c> de <c>Data\F4SE\Plugins\f4ee.ini</c>
'''   (main.cpp:597, 678, 708; default 1). Con eso en 0 no se carga ningún JSON ni se instala ningún hook.</item>
''' </list>
'''
''' <para>ESL — DIVERGENCIA DELIBERADA (decidida por el usuario: "no repliques el bug, hazlo bien").
''' f4ee compone el FormID con
''' <c>GetPartialIndex() &lt;&lt; 16</c> para un plugin light, y <c>GetPartialIndex()</c> devuelve
''' <c>0xFE000 | lightIndex</c> (f4se GameData.h:87): el corrimiento desborda los 32 bits y el resultado
''' apunta al slot 224 del load order en vez de al ESL (p. ej. lightIndex 0x07B, local 0x800 →
''' <c>0xE07B0800</c> en vez de <c>0xFE07B800</c>). <c>LookupFormByID</c> falla y f4ee no registra nada.
''' Nosotros lo resolvemos BIEN, con <see cref="PluginManager.GlobalFormIDFromObjectID"/>.</para>
''' <para>OJO con cuál API: el "Form" del JSON es el object ID CRUDO (12 bits útiles en un ESL), la
''' convención del CK. <c>GlobalFormIDFromIdentifierLocal</c> —la que usa el loader de presets— espera el
''' local de 24 bits de LooksMenu, que en un ESL YA lleva el light slot adentro; alimentarla con un object
''' ID pelado da <c>0xFE000xxx</c>, o sea el record del ESL en el slot 0: OTRO plugin. Si ese plugin tiene
''' un CLFM en esa posición, pasa el chequeo de firma y le atamos la LUT a un color ajeno, en silencio.</para>
'''
''' <para>Perezoso e idempotente, como <c>LmCustomTintLoader</c>: el escaneo de disco corre una vez por
''' sesión hasta que <see cref="Invalidate"/> lo tire (reparse de load order).</para>
''' </summary>
Public Module LmHairColorLutLoader

    ''' <summary>La LUT de pelo vanilla (CharGenInterface.cpp:47). Es la ÚNICA textura sobre la que f4ee
    ''' acepta cambiar la paleta: si el material o el RACE ya apuntan a otra cosa, no la toca (salvo que esa
    ''' otra cosa sea, a su vez, una LUT registrada). Normalizada: minúsculas, backslashes, sin <c>textures\</c>.</summary>
    Public Const HairGradientPalette As String = "actors\character\hair\haircolor_lgrad_d.dds"

    ''' <summary>TODO el estado del registro vive en UNA instancia inmutable, publicada con una sola
    ''' escritura volátil. NO son campos sueltos, y el motivo es concreto: el bake corre
    ''' <c>Parallel.ForEach</c> por NPC (BakeAllRunner), así que N hilos tocan esto a la vez. Con campos
    ''' sueltos, <c>Invalidate</c> los ponía en Nothing de a uno y un lector podía agarrar la mitad viejos y
    ''' la mitad nulos; y como todos los getters degradan en silencio (devuelven "" / False), el resultado no
    ''' era un crash sino un BAKE DISTINTO según cómo cayó el scheduling — justo la firma de un A/A que no da
    ''' cero. Con el snapshot, un lector ve o Nothing (sin cargar) o el registro completo. Nunca la mitad.</summary>
    Private NotInheritable Class LutRegistry
        Public ReadOnly LutByColorForm As Dictionary(Of UInteger, String)
        Public ReadOnly RegisteredLuts As HashSet(Of String)
        Public ReadOnly FlaggedColorForms As HashSet(Of UInteger)
        Public ReadOnly ColorsByRace As Dictionary(Of String, List(Of UInteger)())
        Public ReadOnly ExtendedLuts As Boolean
        ''' <summary>Data\ del que se leyo este registro. Si el proximo EnsureLoaded llega con OTRO, se
        ''' recarga: el CLI admite read&lt;&gt;write a proposito, y sin esto el PRIMER caller fijaba el registro
        ''' para todo el proceso y los demas caminos leian el LUTs\ equivocado sin enterarse.</summary>
        Public ReadOnly SourceDataPath As String
        Public Sub New(lutByColorForm As Dictionary(Of UInteger, String), registeredLuts As HashSet(Of String),
                       flaggedColorForms As HashSet(Of UInteger), colorsByRace As Dictionary(Of String, List(Of UInteger)()),
                       extendedLuts As Boolean, sourceDataPath As String)
            Me.LutByColorForm = lutByColorForm
            Me.RegisteredLuts = registeredLuts
            Me.FlaggedColorForms = flaggedColorForms
            Me.ColorsByRace = colorsByRace
            Me.ExtendedLuts = extendedLuts
            ' Ya viene canonicalizado por el caller (EnsureLoaded) — se guarda así para que la comparación
            ' de IsLoadedFor sea forma-contra-forma y no texto-contra-texto.
            Me.SourceDataPath = If(sourceDataPath, "")
        End Sub
    End Class

    Private ReadOnly _lock As New Object()
    Private _registry As LutRegistry = Nothing
    Private ReadOnly _noColors As IReadOnlyList(Of UInteger) = New List(Of UInteger)()

    ''' <summary>Tira el escaneo cacheado para que el próximo uso relea <c>LUTs\</c>. Llamar en un reparse
    ''' de load order, junto a <c>LmCustomTintLoader.Invalidate</c>.</summary>
    Public Sub Invalidate()
        SyncLock _lock
            Threading.Volatile.Write(_registry, Nothing)
        End SyncLock
    End Sub

    ''' <summary>Normalización de path de f4ee (CharGenInterface.cpp:1117-1122): minúsculas, todo separador
    ''' colapsado a <c>\</c>, sin <c>\</c> al frente, y recortado todo lo que haya hasta e incluyendo
    ''' <c>textures\</c>. Es la forma en la que se comparan la LUT actual y las registradas.</summary>
    Public Function NormalizeLutPath(path As String) As String
        If String.IsNullOrEmpty(path) Then Return ""
        Dim s = path.ToLowerInvariant().Replace("/"c, "\"c)
        While s.Contains("\\")
            s = s.Replace("\\", "\")
        End While
        s = s.TrimStart("\"c)
        ' LastIndexOf, no IndexOf: std::regex_replace reemplaza TODAS las ocurrencias no solapadas, así
        ' que en la práctica recorta hasta el ÚLTIMO "textures\". Con IndexOf, un path con el prefijo
        ' repetido (textures\mimod\textures\pelo\lut.dds — typo común de autor) quedaba como
        ' mimod\textures\pelo\lut.dds y no matcheaba la gradient vanilla ⇒ eligible=False y la LUT custom
        ' no se aplicaba nunca.
        Dim idx = s.LastIndexOf("textures\", StringComparison.Ordinal)
        If idx >= 0 Then s = s.Substring(idx + "textures\".Length)
        Return s
    End Function

    ''' <summary>GetLUTFromColor (CharGenInterface.cpp:1369): la LUT registrada para este color form, o ""
    ''' si no está registrado. El mapa NO depende de raza ni de género.</summary>
    Public Function TryGetLut(colorFormID As UInteger) As String
        Dim reg = Threading.Volatile.Read(_registry)
        If colorFormID = 0UI OrElse reg Is Nothing Then Return ""
        Dim v As String = Nothing
        If reg.LutByColorForm.TryGetValue(colorFormID, v) Then Return v
        Return ""
    End Function

    ''' <summary>IsLUTUsed (CharGenInterface.cpp:1358): ¿este path (ya normalizado) es una de las LUTs
    ''' registradas? Es lo que deja que un NPC que YA tiene una LUT custom pase a otra, o vuelva a la vanilla.</summary>
    Public Function IsRegisteredLut(normalizedPath As String) As Boolean
        Dim reg = Threading.Volatile.Read(_registry)
        If String.IsNullOrEmpty(normalizedPath) OrElse reg Is Nothing Then Return False
        Return reg.RegisteredLuts.Contains(normalizedPath)
    End Function

    ''' <summary><c>bNeedsCustomLUT</c> = <c>colorForm-&gt;flags &amp; 0x8000</c> (CharGenInterface.cpp:1126).
    ''' <para>OJO: marcado NO implica que haya LUT. La marca se pone para TODO color form listado en un JSON
    ''' (CharGenInterface.cpp:1284, FUERA del bucle de razas), mientras que el mapa se llena sólo si alguna
    ''' (raza, género) resuelve. Un color marcado pero sin LUT no vuelve a la paleta vanilla: no cambia nada.</para></summary>
    Public Function IsFlaggedColorForm(colorFormID As UInteger) As Boolean
        Dim reg = Threading.Volatile.Read(_registry)
        If colorFormID = 0UI OrElse reg Is Nothing Then Return False
        Return reg.FlaggedColorForms.Contains(colorFormID)
    End Function

    ''' <summary>Gate de la MALLA de pelo/barba — traducción literal de <c>ProcessHairColor</c>
    ''' (CharGenInterface.cpp:1126-1151). Dado el path de paleta que la app resolvió del material y el color
    ''' de pelo del NPC, devuelve el path que el motor terminaría bindeando en la ranura 3 del TXST.
    '''
    ''' <para>NO usar para las cejas: ése es <see cref="ApplyCustomLutEyebrow"/>, y f4ee lo implementa con
    ''' OTRA función, con menos ramas. Tenerlas separadas es a propósito.</para>
    '''
    ''' <para>La condición de elegibilidad es lo que hace que esto NO pueda romper nada instalado: f4ee sólo
    ''' pisa la paleta si la actual ES la gradient vanilla, o si ya es una LUT registrada. Un peinado con
    ''' paleta propia (p. ej. las de KSHairdos que apuntan a <c>vhaircolor_lgrad_d.dds</c>) queda intacto —
    ''' el motor tampoco se la cambia.</para>
    '''
    ''' <para>Devuelve <paramref name="baseLutPath"/> sin tocar cuando no aplica. NO cambia la FILA: el
    ''' <c>RemappingIndex</c> del CLFM lo sigue escribiendo el caller (el hook hace lo mismo, main.cpp:412).</para></summary>
    Public Function ApplyCustomLutMesh(baseLutPath As String, hairColorFormID As UInteger) As String
        If String.IsNullOrEmpty(baseLutPath) Then Return baseLutPath
        ' UNA sola lectura del snapshot para TODA la decisión. Con un Volatile.Read por consulta, un
        ' Invalidate()+EnsureLoaded() concurrente podía dejar `needsCustom` calculado sobre el registro viejo
        ' y `usingCustom` sobre el nuevo ⇒ la rama de revert de abajo se disparaba y DESTRUÍA una paleta
        ' custom legítima. La clase promete "o Nothing o el registro completo": eso vale por lectura, no por
        ' una decisión compuesta de cuatro.
        Dim reg = Threading.Volatile.Read(_registry)
        If reg Is Nothing Then Return baseLutPath

        Dim current = NormalizeLutPath(baseLutPath)
        Dim needsCustom = hairColorFormID <> 0UI AndAlso reg.FlaggedColorForms.Contains(hairColorFormID)
        Dim usingCustom = current <> "" AndAlso reg.RegisteredLuts.Contains(current)
        Dim eligible = String.Equals(current, HairGradientPalette, StringComparison.Ordinal)

        ' "Ya no queremos la LUT custom": venía con una registrada y el color nuevo no está marcado.
        If usingCustom AndAlso Not needsCustom Then Return HairGradientPalette

        ' Elegible (o ya usando una custom) y el color pide una: si el mapa la tiene, se cambia.
        If (eligible OrElse usingCustom) AndAlso needsCustom Then
            Dim lut As String = Nothing
            If reg.LutByColorForm.TryGetValue(hairColorFormID, lut) AndAlso Not String.IsNullOrEmpty(lut) Then Return lut
        End If

        Return baseLutPath
    End Function

    ''' <summary>Gate de las CEJAS — <c>ProcessEyebrowPath</c> (CharGenInterface.cpp:1181-1207). Es MÁS SIMPLE
    ''' que el de la malla y la diferencia importa:
    ''' <code>
    '''   auto hairTexturePath = npc-&gt;race.race-&gt;hairColorLUT.str.c_str();
    '''   if (colorForm &amp;&amp; (colorForm-&gt;flags &amp; 0x8000)) {
    '''       ...normalizar...
    '''       if (fullPath == HairGradientPalette)          // SOLO elegible
    '''           if (GetLUTFromColor(colorForm, str)) return str;
    '''   }
    '''   return hairTexturePath;                           // sin rama de revert
    ''' </code>
    ''' <para>No existe <c>bUsingCustomLUT</c> ni vuelta a la gradient vanilla. Usar acá el gate de la malla
    ''' divergía en dos casos, los dos disparables por un mod de "raza custom + pack de colores a juego"
    ''' (basta con que el HNAM de la raza sea, él mismo, una LUT que algún <c>haircolors.json</c> registre):
    ''' con el color NO marcado le devolvíamos la gradient vanilla en vez del HNAM, y con el color marcado le
    ''' devolvíamos la LUT custom donde el motor deja el HNAM intacto.</para></summary>
    ''' <para>Divergencia menor, deliberada: si el color quedó registrado con una LUT VACÍA (JSON con
    ''' <c>Races</c>/<c>Gender</c> válidos y sin <c>"LUT"</c>), <c>GetLUTFromColor</c> devuelve true con
    ''' <c>str=""</c> y el motor termina devolviendo "" ⇒ la ceja NO samplea nada. Nosotros exigimos LUT no
    ''' vacía y dejamos el HNAM ⇒ la ceja se tinta. Preferimos el comportamiento útil ante un JSON roto.</para>
    Public Function ApplyCustomLutEyebrow(raceLutPath As String, hairColorFormID As UInteger) As String
        Dim dummy As String = Nothing
        Return ApplyCustomLutEyebrow(raceLutPath, hairColorFormID, dummy)
    End Function

    ''' <summary>Igual que la sobrecarga de un solo valor, pero además informa por
    ''' <paramref name="appliedCustomLut"/> si el resultado ES una LUT custom del registro — desde la MISMA
    ''' lectura del snapshot, para que un caller que necesita las dos cosas (el reporte de compatibilidad) no
    ''' las pida por separado y pueda ver dos registros distintos si entre medio corre un
    ''' <see cref="Invalidate"/>.
    ''' <para>SEMÁNTICA: "la que se APLICÓ", no "la que el registro TIENE". La diferencia importa justo en
    ''' el caso que documenta esta función — raza custom cuyo HNAM no es la gradient vanilla + pack de colores
    ''' que registra ese HCLF: ahí el registro TIENE una LUT pero <c>ProcessEyebrowPath</c> NO la aplica, y un
    ''' caller que leyera "la que tiene" reportaría que la ceja usa una paleta que no usa.</para></summary>
    Public Function ApplyCustomLutEyebrow(raceLutPath As String, hairColorFormID As UInteger,
                                          ByRef appliedCustomLut As String) As String
        appliedCustomLut = ""
        If String.IsNullOrEmpty(raceLutPath) Then Return raceLutPath
        Dim reg = Threading.Volatile.Read(_registry)
        If reg Is Nothing Then Return raceLutPath
        If hairColorFormID = 0UI OrElse Not reg.FlaggedColorForms.Contains(hairColorFormID) Then Return raceLutPath

        ' Elegibilidad SOLA: sin `OrElse usingCustom`, y sin rama de revert.
        If Not String.Equals(NormalizeLutPath(raceLutPath), HairGradientPalette, StringComparison.Ordinal) Then Return raceLutPath

        Dim lut As String = Nothing
        If reg.LutByColorForm.TryGetValue(hairColorFormID, lut) AndAlso Not String.IsNullOrEmpty(lut) Then
            appliedCustomLut = lut          ' se setea SOLO cuando de verdad se devuelve
            Return lut
        End If
        Return raceLutPath
    End Function

    ''' <summary>La paleta con la que se tintan las CEJAS (y el resto del tint de facegen que la use).
    ''' Sale del RACE, NO de la malla de pelo.
    '''
    ''' <para>VERIFICADO EN EL BINARIO (Fallout4.exe, no sólo en la fuente de f4ee). Dentro de
    ''' <c>BSFaceGenUtils::StartFaceCustomizationGenerationForNPC</c> el motor hace, justo antes del epílogo:</para>
    ''' <code>
    '''   mov  rcx, [rbp+0x1DE0]     ; el NPC
    '''   mov  rcx, [rcx+0x1B8]      ; npc -> race
    '''   add  rcx, 0x6C0            ; race -> hairColorLUT.str   (TESTexture@0x6B8 + 8, f4se GameForms.h:924)
    '''   call BSFixedString::GetCString
    ''' </code>
    ''' <para>Esas son las 3 instrucciones (0x13 bytes) que f4ee reemplaza con su hook
    ''' <c>GetHairTexturePath_Hook</c> (main.cpp:707-737, rotulado <c>// SetEyebrowLUTPath</c>) para poder
    ''' devolver una LUT custom. El string se consume sólo si es no vacío. En ese camino NO se lee ninguna
    ''' malla ni ningún BGSM: mirar el material del peinado —como hacía la app— era aplicar acá la ley de
    ''' <c>ProcessHairColor</c>, que es la del MESH.</para>
    '''
    ''' <para><c>race+0x6B8</c> = <c>HNAM</c> (medido: HumanRace trae
    ''' <c>Actors\Character\Hair\HairColor_Lgrad_d.DDS</c> en HNAM y no trae HLTX). <c>HLTX</c> —el
    ''' "extended LUT", que el motor elegiría por el bit <c>FNAM 0x4</c> del CLFM— queda como fallback sólo
    ''' si HNAM está vacío: esa segunda ley NO está verificada, y ningún record vanilla la ejercita.</para>
    '''
    ''' <para>Una raza sin HNAM (vanilla <c>HumanChildRace</c>) devuelve "" y la rama de paleta de la ceja
    ''' no hace nada — que es exactamente lo que hace el motor (<c>test eax,eax; je</c> sobre el string).</para>
    '''
    ''' <para>Sólo FO4 en la práctica: el consumidor exige <c>CLFM.HasRemappingIndex</c>, y el índice de paleta
    ''' sólo existe en Fallout 4. En Skyrim el color de pelo es siempre RGB.</para></summary>
    ''' <para>A propósito NO copia la preferencia-por-existencia de
    ''' <c>NpcMaterialResolver.ResolveRaceHairLookupTexture</c> (que entre HNAM y HLTX elige el que esté
    ''' instalado). Acá manda el motor: lee <c>race+0x6C0</c> = HNAM y nada más. Si el HNAM de una raza
    ''' apunta a una textura que no está, la ceja no samplea — igual que en el juego. Que la MALLA sí use esa
    ''' preferencia es una heurística nuestra, y sólo actúa cuando el BGSM del peinado no trae paleta propia;
    ''' son dos leyes distintas, no una duplicación que haya que unificar.</para>
    Public Function ResolveBrowPaletteTexture(race As Canon.IRace, hairColorFormID As UInteger) As String
        Dim dummy As String = Nothing
        Return ResolveBrowPaletteTexture(race, hairColorFormID, dummy)
    End Function

    ''' <summary>Igual, y además informa si el resultado es una LUT custom APLICADA, desde la MISMA lectura
    ''' del snapshot (ver la sobrecarga de <see cref="ApplyCustomLutEyebrow"/> y su nota de semántica).</summary>
    Public Function ResolveBrowPaletteTexture(race As Canon.IRace, hairColorFormID As UInteger,
                                              ByRef appliedCustomLut As String) As String
        appliedCustomLut = ""
        ' HairColorLookupTexture/HairColorExtendedLookupTexture (HNAM/HLTX con este significado) son
        ' exclusivos de Fallout 4 — Skyrim no los declara en RACE.
        Dim raceFo4 = TryCast(race, Canon.RaceFO4)
        If raceFo4 Is Nothing Then Return ""
        Dim chosen = raceFo4.HairColorLookupTexture
        If String.IsNullOrWhiteSpace(chosen) Then chosen = raceFo4.HairColorExtendedLookupTexture
        If String.IsNullOrWhiteSpace(chosen) Then Return ""
        Return ApplyCustomLutEyebrow(chosen, hairColorFormID, appliedCustomLut)
    End Function

    ''' <summary>Colores registrados que el selector debe ofrecer para esta raza y género — el equivalente
    ''' del <c>chargenData[i]-&gt;colors-&gt;Push</c> de f4ee (:1308). El ESP que agrega los colores normalmente
    ''' NO toca el record RACE, así que sin esto los colores existen pero no hay forma de elegirlos.</summary>
    Public Function RegisteredColorsFor(raceEditorID As String, isFemale As Boolean) As IReadOnlyList(Of UInteger)
        Dim reg = Threading.Volatile.Read(_registry)
        If String.IsNullOrEmpty(raceEditorID) OrElse reg Is Nothing Then Return _noColors
        Dim byGender As List(Of UInteger)() = Nothing
        If Not reg.ColorsByRace.TryGetValue(raceEditorID, byGender) Then Return _noColors
        Dim lst = byGender(If(isFemale, 1, 0))
        Return If(lst, _noColors)
    End Function

    ''' <summary>Cantidad de colores con LUT registrada (diagnóstico / gate).</summary>
    Public ReadOnly Property RegisteredColorCount As Integer
        Get
            Dim reg = Threading.Volatile.Read(_registry)
            Return If(reg Is Nothing, 0, reg.LutByColorForm.Count)
        End Get
    End Property

    ''' <summary>¿<c>[CharGen] bExtendedLUTs</c> está habilitado? Con esto en False el registro queda vacío
    ''' y todo el camino se comporta como si no existiera ningún JSON — igual que el juego.</summary>
    Public ReadOnly Property ExtendedLutsEnabled As Boolean
        Get
            Dim reg = Threading.Volatile.Read(_registry)
            Return reg Is Nothing OrElse reg.ExtendedLuts
        End Get
    End Property

    ''' <summary>Forma canónica de un Data\ para poder compararlo. Comparar el TEXTO CRUDO no sirve (ver
    ''' también el <c>SamePath</c> del CLI): <c>--data F:/x/Data</c> —como lo tipea un script— y el
    ''' <c>DataPath</c> del config (<c>F:\x\Data</c>) son LA MISMA carpeta y difieren como string. Sin
    ''' canonicalizar, cada cruce entre el camino de la malla (que resuelve por Config_App) y el del bake (que
    ''' recibe el --data) da "distinto" y fuerza un rescan completo del registro —
    ''' <c>BuildRaceEditorIdIndex</c> recorre TODOS los RACE del load order— por NPC.</summary>
    Private Function CanonicalDataPath(dataPath As String) As String
        If String.IsNullOrWhiteSpace(dataPath) Then Return ""
        Dim t = dataPath.Trim()
        Try
            t = Path.GetFullPath(t)
        Catch
            t = t.Replace("/"c, "\"c)
        End Try
        Return t.TrimEnd("\"c, "/"c)
    End Function

    ''' <summary>¿Ya esta cargado, y del MISMO Data\ que se pide? Un dataPath realmente distinto obliga a
    ''' recargar; uno que sólo difiere en la forma de escribirlo, no.</summary>
    Private Function IsLoadedFor(dataPath As String) As Boolean
        Dim reg = Threading.Volatile.Read(_registry)
        If reg Is Nothing Then Return False
        Return String.Equals(reg.SourceDataPath, CanonicalDataPath(dataPath), StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Sobrecarga de la app: resuelve el Data\ del <see cref="Config_App"/> global.</summary>
    Public Sub EnsureLoaded(pluginManager As PluginManager)
        EnsureLoaded(pluginManager, Config_App.Current?.DataPath)
    End Sub

    ''' <summary>Sobrecarga con <paramref name="dataPath"/> explícito — la usa el CLI headless, que thread-ea
    ''' su propio Data\ (honrando <c>--data</c>) en vez del global de la app.</summary>
    Public Sub EnsureLoaded(pluginManager As PluginManager, dataPath As String)
        If IsLoadedFor(dataPath) Then Return
        If pluginManager Is Nothing Then Return
        ' SIN dataPath NO se publica nada — se sale como "todavía sin cargar". Publicar un registro vacío
        ' acá lo latchearía para toda la sesión: el primer caller que llegue con un Data\ vacío (p. ej. la
        ' sobrecarga de la app cuando Config_App todavía no resolvió el exe del juego) dejaría a TODOS los
        ' consumidores posteriores sin LUTs, en silencio. Un Data\ válido sin carpeta LUTs\ sí publica
        ' (registro vacío legítimo, y se cachea).
        ' IsNullOrWhiteSpace, igual que CanonicalDataPath. Con IsNullOrEmpty, un path de sólo espacios
        ' pasaba esta guarda y publicaba un registro VACÍO cuya SourceDataPath canonicaliza a "" — y a partir
        ' de ahí IsLoadedFor(Nothing) lo daba por cargado, dejando a todos los consumidores sin LUTs para el
        ' resto de la sesión. Las dos guardas tienen que usar el mismo criterio de "vacío".
        If String.IsNullOrWhiteSpace(dataPath) Then Return
        SyncLock _lock
            If IsLoadedFor(dataPath) Then Return

            Dim lutMap As New Dictionary(Of UInteger, String)()
            Dim luts As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim flagged As New HashSet(Of UInteger)()
            Dim byRace As New Dictionary(Of String, List(Of UInteger)())(StringComparer.OrdinalIgnoreCase)
            Dim enabled As Boolean = True

            Try
                ' SOLO FO4. LooksMenu/f4ee no existe en Skyrim: SSE usa RaceMenu (skee64), que no tiene
                ' registro de LUTs — el color de pelo ahí es un RGB absoluto. Y un CLFM de Skyrim NO lleva
                ' índice de paleta, así que en Skyrim todo este camino queda inerte igual; el gate explícito
                ' evita depender de esa inercia.
                If Config_App.Current Is Nothing OrElse Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then
                    Logger.LogLazy(Function() "[LM-HAIRLUT] juego != FO4: el registro de LUTs de LooksMenu no aplica (SSE usa RaceMenu con RGB absoluto).")
                Else
                    ' dataPath ya viene garantizado no vacío por la guarda de arriba.
                    enabled = ReadExtendedLutsIni(dataPath)
                    If enabled Then
                        Dim baseDir = Path.Combine(dataPath, "F4SE", "Plugins", "F4EE", "LUTs")
                        If Directory.Exists(baseDir) Then
                            Dim raceIdx = BuildRaceEditorIdIndex(pluginManager)
                            For Each plugin In pluginManager.Plugins
                                Dim p = Path.Combine(baseDir, plugin.FileName, "haircolors.json")
                                If Not File.Exists(p) Then Continue For
                                If plugin.IsESL Then
                                    ' Ver la nota de ESL del encabezado: acá SÍ resuelve, en el juego no.
                                    ' Se avisa para que un "en la app se ve y en el juego no" tenga explicación.
                                    Dim nm = plugin.FileName
                                    Logger.LogLazy(Function() $"[LM-HAIRLUT] '{nm}' es ESL: f4ee no puede registrar estos colores en el juego (desborde de GetPartialIndex()<<16). Los resolvemos igual, a propósito.")
                                End If
                                LoadHairColorFile(p, plugin.FileName, pluginManager, raceIdx, lutMap, luts, flagged, byRace)
                            Next
                        End If
                    Else
                        Logger.LogLazy(Function() "[LM-HAIRLUT] [CharGen] bExtendedLUTs=0 en f4ee.ini: el sistema de LUTs extendidas está APAGADO, no se lee ningún haircolors.json.")
                    End If
                End If
            Catch ex As Exception
                Dim msg = ex.Message
                Logger.LogLazy(Function() $"[LM-HAIRLUT] EnsureLoaded falló: {msg}")
            End Try

            ' UNA sola publicación, al final: hasta acá ningún otro hilo puede ver un registro a medias.
            Threading.Volatile.Write(_registry, New LutRegistry(lutMap, luts, flagged, byRace, enabled, CanonicalDataPath(dataPath)))

            Dim nColors = lutMap.Count, nLuts = luts.Count
            Logger.LogLazy(Function() $"[LM-HAIRLUT] registro cargado: {nColors} color form(s) → {nLuts} LUT(s) distintas")
        End SyncLock
    End Sub

    ''' <summary><c>[CharGen] bExtendedLUTs</c> de <c>Data\F4SE\Plugins\f4ee.ini</c>. Default True (main.cpp:76)
    ''' — un ini ausente o sin la clave deja el sistema encendido, igual que el plugin.</summary>
    Private Function ReadExtendedLutsIni(dataPath As String) As Boolean
        Dim iniPath = Path.Combine(dataPath, "F4SE", "Plugins", "f4ee.ini")
        If Not File.Exists(iniPath) Then Return True
        Try
            Dim inCharGen As Boolean = False
            For Each raw In File.ReadAllLines(iniPath)
                Dim line = raw.Trim()
                If line.Length = 0 Then Continue For
                If line.StartsWith("[", StringComparison.Ordinal) Then
                    inCharGen = line.StartsWith("[CharGen]", StringComparison.OrdinalIgnoreCase)
                    Continue For
                End If
                If Not inCharGen Then Continue For
                Dim eq = line.IndexOf("="c)
                If eq <= 0 Then Continue For
                If Not line.Substring(0, eq).Trim().Equals("bExtendedLUTs", StringComparison.OrdinalIgnoreCase) Then Continue For
                ' El ini de f4ee lleva comentarios al final de la línea: "1 ; Default[1]".
                Dim v = line.Substring(eq + 1).Trim()
                Dim semi = v.IndexOfAny(New Char() {";"c, "#"c})
                If semi >= 0 Then v = v.Substring(0, semi).Trim()
                Dim n As Integer
                If Integer.TryParse(v, n) Then Return n <> 0
                Return True
            Next
        Catch
        End Try
        Return True
    End Function

    ''' <summary>Set de EditorIDs de RACE — sólo PRESENCIA, que es todo lo que el <c>GetRaceByName</c> de f4ee
    ''' (:1292) necesita; el valor del diccionario NO se usa. Case-insensitive, como su <c>F4EEFixedString</c>
    ''' (<c>_stricmp</c>). No parsea los records: alcanza con el EditorID del header, así que armarlo una vez
    ''' por escaneo no cuesta nada aunque haya cientos de razas.</summary>
    Private Function BuildRaceEditorIdIndex(pluginManager As PluginManager) As Dictionary(Of String, Boolean())
        Dim idx As New Dictionary(Of String, Boolean())(StringComparer.OrdinalIgnoreCase)
        Try
            For Each rec In pluginManager.GetRecordsOfType("RACE")
                If rec Is Nothing Then Continue For
                Dim edid = rec.EditorID
                If String.IsNullOrEmpty(edid) OrElse idx.ContainsKey(edid) Then Continue For
                idx(edid) = Nothing
            Next
        Catch
        End Try
        Return idx
    End Function

    ''' <summary>Hex al estilo <c>sscanf_s(s, "%X", &amp;v)</c>, que es lo que usa f4ee (CharGenInterface.cpp:1243):
    ''' saltea espacios, acepta el prefijo <c>0x</c>, y PARA en el primer carácter no hexadecimal en vez de
    ''' fallar. <c>UInteger.TryParse(NumberStyles.HexNumber)</c> rechaza las tres cosas, así que un
    ''' <c>"Form": "0x801"</c> —que en el juego registra— se cae en silencio.</summary>
    Friend Function TryParseSscanfHex(s As String, ByRef value As UInteger) As Boolean
        value = 0UI
        If s Is Nothing Then Return False
        Dim i As Integer = 0
        While i < s.Length AndAlso Char.IsWhiteSpace(s(i))
            i += 1
        End While
        If i + 1 < s.Length AndAlso s(i) = "0"c AndAlso (s(i + 1) = "x"c OrElse s(i + 1) = "X"c) Then i += 2
        Dim digits As Integer = 0
        Dim acc As ULong = 0UL
        While i < s.Length
            Dim c = s(i)
            Dim d As Integer
            If c >= "0"c AndAlso c <= "9"c Then
                d = AscW(c) - AscW("0"c)
            ElseIf c >= "a"c AndAlso c <= "f"c Then
                d = AscW(c) - AscW("a"c) + 10
            ElseIf c >= "A"c AndAlso c <= "F"c Then
                d = AscW(c) - AscW("A"c) + 10
            Else
                Exit While
            End If
            acc = (acc << 4) Or CULng(d)
            If acc > UInteger.MaxValue Then Return False   ' overflow: sscanf sería UB, nosotros lo rechazamos
            digits += 1
            i += 1
        End While
        If digits = 0 Then Return False
        value = CUInt(acc)
        Return True
    End Function

    Private ReadOnly _jsonOpts As New JsonDocumentOptions With {
        .CommentHandling = JsonCommentHandling.Skip,
        .AllowTrailingCommas = True}

    ''' <summary>Parsea un <c>haircolors.json</c>: <c>{ "Colors": [ { Form, LUT, Races, Gender } ] }</c>.
    ''' Espeja <c>CharGenInterface::LoadHairColorData</c> (:1211-1356). Una entrada mal formada se saltea sin
    ''' tirar el archivo entero (el C++ tiene el try/catch por item).</summary>
    Private Sub LoadHairColorFile(filePath As String, pluginFileName As String, pluginManager As PluginManager,
                                  raceIdx As Dictionary(Of String, Boolean()),
                                  lutMap As Dictionary(Of UInteger, String),
                                  luts As HashSet(Of String),
                                  flagged As HashSet(Of UInteger),
                                  byRace As Dictionary(Of String, List(Of UInteger)()))
        Try
            Using doc = JsonDocument.Parse(File.ReadAllText(filePath), _jsonOpts)
                Dim colors As JsonElement
                If Not doc.RootElement.TryGetProperty("Colors", colors) OrElse colors.ValueKind <> JsonValueKind.Array Then Return

                For Each item In colors.EnumerateArray()
                    Try
                        If item.ValueKind <> JsonValueKind.Object Then Continue For

                        ' "Form": string o array de strings, FormID LOCAL en hex.
                        Dim formEl As JsonElement
                        If Not item.TryGetProperty("Form", formEl) Then Continue For
                        Dim formIds As New List(Of UInteger)()
                        If formEl.ValueKind = JsonValueKind.String Then
                            AddResolvedForm(formEl.GetString(), pluginFileName, pluginManager, filePath, formIds)
                        ElseIf formEl.ValueKind = JsonValueKind.Array Then
                            For Each fe In formEl.EnumerateArray()
                                If fe.ValueKind = JsonValueKind.String Then
                                    AddResolvedForm(fe.GetString(), pluginFileName, pluginManager, filePath, formIds)
                                End If
                            Next
                        End If
                        If formIds.Count = 0 Then Continue For

                        ' "LUT" ausente/no-string ⇒ path vacío, NO se saltea la entrada: jsoncpp devuelve ""
                        ' para una clave que no está y f4ee igual marca el color (:1278 se lee ANTES del bucle
                        ' de razas, y el flag se pone en :1284). Un color marcado sin LUT no vuelve a la
                        ' gradient vanilla — que es justo la asimetría que el gate necesita ver.
                        Dim lutPath As String = ""
                        Dim lutEl As JsonElement
                        If item.TryGetProperty("LUT", lutEl) AndAlso lutEl.ValueKind = JsonValueKind.String Then
                            lutPath = If(lutEl.GetString(), "")
                        End If

                        ' `Gender` presente pero NO numerico: en jsoncpp `asUInt()` tira, y el try/catch por
                        ' item (CharGenInterface.cpp:1348) descarta la entrada ENTERA — antes del
                        ' `flags |= 0x8000` de :1284. O sea que el color NO queda marcado. Sin este Continue
                        ' marcabamos un color que el juego no marca, y la rama de revert de
                        ' ApplyCustomLutMesh se comportaba distinto que el motor.
                        Dim gender As UInteger = 0UI
                        Dim gEl As JsonElement
                        If item.TryGetProperty("Gender", gEl) Then
                            If gEl.ValueKind <> JsonValueKind.Number Then Continue For
                            Dim gi As Integer
                            If gEl.TryGetInt32(gi) AndAlso gi >= 0 Then gender = CUInt(gi)
                        End If

                        Dim raceNames As New List(Of String)()
                        Dim rEl As JsonElement
                        If item.TryGetProperty("Races", rEl) AndAlso rEl.ValueKind = JsonValueKind.Array Then
                            For Each re_ In rEl.EnumerateArray()
                                If re_.ValueKind = JsonValueKind.String Then raceNames.Add(re_.GetString())
                            Next
                        End If

                        For Each fid In formIds
                            ' Sólo CLFM: el C++ hace DYNAMIC_CAST a BGSColorForm y descarta el resto (:1253).
                            Dim rec = pluginManager.GetRecord(fid)
                            If rec Is Nothing Then
                                Dim f1 = fid, fp1 = filePath
                                Logger.LogLazy(Function() $"[LM-HAIRLUT] {fp1}: no existe el form 0x{f1:X8}")
                                Continue For
                            End If
                            If rec.Header.Signature <> "CLFM" Then
                                Dim f2 = fid, sg = rec.Header.Signature, fp2 = filePath
                                Logger.LogLazy(Function() $"[LM-HAIRLUT] {fp2}: 0x{f2:X8} es {sg}, no CLFM")
                                Continue For
                            End If

                            ' colorForm->flags |= 0x8000 (:1284) — FUERA del bucle de razas: la marca se pone
                            ' aunque después no resuelva ninguna raza.
                            flagged.Add(fid)

                            ' El registro en el mapa vive DENTRO del bucle de razas × géneros (:1298-1313):
                            ' sin una (raza, género) válida el color queda marcado pero SIN LUT, y el motor
                            ' no le cambia la paleta. Replicado tal cual.
                            ' El C++ tiene dos condiciones distintas (CharGenInterface.cpp:1303-1312):
                            '     auto charGenData = race->chargenData[i];
                            '     if(!charGenData) continue;                                 <- saltea AMBOS
                            '     if(charGenData->colors) charGenData->colors->Push(color);   <- SOLO el catálogo
                            '     m_LUTMap.emplace(...); m_LUTs.insert(...);                  <- va igual
                            ' Ninguna de las dos es observable desde los records, así que en LAS DOS caemos
                            ' del lado PERMISIVO. Tentación descartada: usar AHCM/AHCF como proxy de `colors`.
                            ' No lo es — `colors` es un BGSListForm de RUNTIME que existe (aunque vacío) para
                            ' cualquier raza con chargen data, mientras que AHCM/AHCF es lo que el AUTOR
                            ' escribió. Con ese proxy, una raza custom que no autora colores (porque los trae
                            ' el pack, que es justo el caso de uso) los recibía en el juego y NO en el combo.
                            Dim registered As Boolean = False
                            For Each raceName In raceNames
                                If Not raceIdx.ContainsKey(raceName) Then
                                    Dim rn = raceName, fp3 = filePath
                                    Logger.LogLazy(Function() $"[LM-HAIRLUT] {fp3}: no se encontró la raza '{rn}'")
                                    Continue For
                                End If
                                For genderIdx = 0 To 1
                                    Dim bit As UInteger = If(genderIdx = 0, 1UI, 2UI)
                                    If (gender And bit) = 0UI Then Continue For
                                    registered = True
                                    Dim slots As List(Of UInteger)() = Nothing
                                    If Not byRace.TryGetValue(raceName, slots) Then
                                        slots = New List(Of UInteger)() {New List(Of UInteger)(), New List(Of UInteger)()}
                                        byRace(raceName) = slots
                                    End If
                                    If Not slots(genderIdx).Contains(fid) Then slots(genderIdx).Add(fid)
                                Next
                            Next
                            If Not registered Then Continue For

                            ' emplace: gana el PRIMERO que lo registre (:1311).
                            If Not lutMap.ContainsKey(fid) Then lutMap(fid) = lutPath
                            ' Guardamos la forma NORMALIZADA; f4ee mete el string crudo del JSON (:1312) y
                            ' después lo compara contra un path ya normalizado (IsLUTUsed sobre el resultado
                            ' de ProcessHairColor). Con un "LUT" que traiga el prefijo `textures\`, el suyo no
                            ' matchea NUNCA y el nuestro sí. Es una divergencia a favor, y consistente: los
                            ' dos lados de la comparación pasan por el mismo normalizador.
                            If Not String.IsNullOrWhiteSpace(lutPath) Then luts.Add(NormalizeLutPath(lutPath))
                        Next
                    Catch
                        ' item malformado: el C++ tiene su try/catch por entrada — seguir con la siguiente.
                    End Try
                Next
            End Using
        Catch ex As Exception
            Dim msg = ex.Message, fp = filePath
            Logger.LogLazy(Function() $"[LM-HAIRLUT] no se pudo leer '{fp}': {msg}")
        End Try
    End Sub

    ''' <summary>FormID local en hex → FormID global del plugin dueño. f4ee hace
    ''' <c>formId |= GetPartialIndex() &lt;&lt; (IsLight ? 16 : 24)</c>. Para plugins completos es lo mismo que
    ''' hacemos; para ESL el suyo desborda y el nuestro no (ver la nota de ESL del encabezado).</summary>
    Private Sub AddResolvedForm(hex As String, pluginFileName As String, pluginManager As PluginManager,
                                filePath As String, sink As List(Of UInteger))
        If String.IsNullOrWhiteSpace(hex) Then Return
        Dim local As UInteger
        If Not TryParseSscanfHex(hex, local) Then
            Dim h = hex, fp = filePath
            Logger.LogLazy(Function() $"[LM-HAIRLUT] {fp}: '{h}' no es un FormID hexadecimal")
            Return
        End If
        Dim fid = pluginManager.GlobalFormIDFromObjectID(pluginFileName, local)
        If fid <> 0UI Then sink.Add(fid)
    End Sub

End Module
