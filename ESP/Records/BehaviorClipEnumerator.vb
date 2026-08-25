Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq

' =============================================================================
' Enumera las animaciones (clips) reproducibles de un NPC/raza: carga los behavior
' .hkx resueltos (RaceBehaviorResolver) y junta todos los hkbClipGenerator con su
' contexto (Role del subgraph + behavior de origen), deduplicados por archivo de clip.
'
' El caller inyecta el loader (Func path→bytes) que sabe de FilesDictionary (BA2+loose).
' Las referencias internas usan .hkt; los archivos reales son .hkx → se normaliza y se
' prueban candidatos (con/sin "Meshes\", .hkx/.hkt). El clip resultante (.hkx) se reproduce
' con HkxPoseImport sobre el skeleton de la raza.
' =============================================================================

Public NotInheritable Class BehaviorClipEnumerator
    Private Sub New()
    End Sub

    ''' <summary>Enumera los clips de todos los behavior .hkx de <paramref name="rb"/>. loadBehaviorHkx
    ''' recibe un path lógico ("Actors\…\X.hkx") y devuelve los bytes (o Nothing) — el caller resuelve
    ''' vía FilesDictionary. Devuelve clips deduplicados por archivo, con Roles/behaviors de origen.</summary>
    Public Shared Function EnumerateClips(rb As ResolvedRaceBehavior,
                                          loadBehaviorHkx As Func(Of String, Byte())) As List(Of ResolvedAnimationClip)
        Dim result As New List(Of ResolvedAnimationClip)
        If IsNothing(rb) OrElse loadBehaviorHkx Is Nothing Then Return result

        Dim actorRoot = DirName(rb.Project)
        ' Skeleton .hkx de la RAZA/GÉNERO del NPC = rigName del character del gender resuelto (project gender-aware →
        ' character → hkbCharacterStringData.RigName). SSE tiene skeleton por género (skeleton.hkx / skeleton_female.hkx);
        ' FO4 comparte uno (mismo archivo ambos géneros). Es el skeleton con el que se interpretan los clips del PROPIO
        ' actor del NPC (bind pose correcto por género) — sin esto, de SSE usaba el skeleton (bind distinto) y ESTIRABA
        ' los huesos. Los clips REUSADOS de otro actor (cross-actor) conservan el skeleton de su actor de ORIGEN.
        Dim raceSkel = ResolveHavokSkeleton(rb, loadBehaviorHkx)
        ' Índice de EXISTENCIA (.hkx/.hkt del load order, canon OrdinalIgnoreCase). La resolución clip→archivo es
        ' por existencia sobre las rutas SAPT (search-path del engine), NO por animationNames (incompleto).
        Dim animSet = BuildAnimExistenceSet()
        ' Filtro TYPE-DRIVEN (KYWD.TNAM): se EXCLUYE un subgraph solo si requiere una keyword de IDENTIDAD de OTRA
        ' raza (None-typed ∧ ∈ KWDA de alguna raza ∧ ∉ esta raza). Los ejes de estado (Anim Injured/Archetype/
        ' Gender/…) NUNCA excluyen. [[24-anim-behavior-por-raza]]
        Dim kwSet As New HashSet(Of UInteger)(rb.ActorKeywords)
        Dim byClip As New Dictionary(Of String, ResolvedAnimationClip)(StringComparer.OrdinalIgnoreCase)
        Dim graphCache As New Dictionary(Of String, HkxObjectGraph_Class)(StringComparer.OrdinalIgnoreCase)

        ' (1) ROOT behavior del actor (project→character→behaviorFilename): clips nativos (death/getup/swim/camera/
        '     pipboy) que NO están en ningún subgraph. SIN SAPT → resolución relativa al actor (incluye reuse
        '     explícito "..\Character\…", ej. SuperMutant reusa death humano). Eje "Core", neutro (no female-gated).
        Dim rootBeh = ResolveRootBehaviorFile(rb.Project, loadBehaviorHkx, actorRoot)
        If rootBeh <> "" Then
            EnumBehaviorClips(rootBeh, Nothing, "Core", "Normal", False, -1, loadBehaviorHkx, animSet, actorRoot, raceSkel,
                              byClip, result, graphCache, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        End If

        ' (2) Subgraphs aplicables.
        For Each sg In rb.Subgraphs
            Dim actorKw = sg.ActorKeywords.Select(Function(k) k.Keyword).ToList()
            Dim foreignId = actorKw.FirstOrDefault(Function(k) RaceBehaviorResolver.IsRaceIdentityKeyword(k) AndAlso Not kwSet.Contains(k))
            If foreignId <> 0UI Then Continue For
            Dim axis = StateAxisLabel(actorKw)
            Dim reqFemale = actorKw.Any(Function(k) RaceBehaviorResolver.KeywordType(k) = RaceBehaviorResolver.KwTypeAnimGender)
            ' Sigue el behavior del subgraph Y sus referencias (hkbBehaviorReferenceGenerator) recursivamente, con
            ' el MISMO SAPT/Role/eje. visited per-subgraph (un Core re-usado por varios actores con SAPT distinto).
            EnumBehaviorClips(NormHkx(sg.DataBehaviourGraph), sg.AnimationPaths.Select(Function(p) p.Path).ToList(),
                              RoleName(CInt(sg.FlagsRole)), axis, reqFemale, CInt(sg.FlagsPerspective), loadBehaviorHkx,
                              animSet, actorRoot, raceSkel, byClip, result, graphCache, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), 0)
        Next

        ' ── PASADA DE COBERTURA (file-driven): mapea TODO .hkx bajo las rutas SAPT de los subgraphs APLICADOS que el
        ' walk de clip-generators no alcanzó (variantes mood/gender + gestos/idle reproducidos por evento en runtime).
        ' Scope = subárbol de cada SAPT-dir de subgraphs aplicados (MISMO filtro de identidad ajena que arriba) → para
        ' robots de carpeta compartida queda DENTRO de su subcarpeta. Validado: residual=0 en razas de carpeta dedicada.
        Dim coverageDirs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim foreignDirs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)   ' SAPT de subgraphs de OTRA raza
        For Each sg In rb.Subgraphs
            Dim foreignId = sg.ActorKeywords.Select(Function(k) k.Keyword).
                            FirstOrDefault(Function(k) RaceBehaviorResolver.IsRaceIdentityKeyword(k) AndAlso Not kwSet.Contains(k))
            Dim target = If(foreignId <> 0UI, foreignDirs, coverageDirs)
            For Each sp In sg.AnimationPaths
                If String.IsNullOrWhiteSpace(sp.Path) Then Continue For
                target.Add(CanonHkx(sp.Path.Replace("/"c, "\"c).TrimEnd("\"c)))
            Next
        Next
        ' Las carpetas que pertenecen a OTRA raza (un subgraph EXCLUIDO por identidad las lista, ej. Assaultron\ para
        ' Protectron) NO se cubren — aunque un subgraph genérico (SAKD=[], ej. FurnitureBehavior compartido) también las
        ' liste. Si no, la pasada de variantes traía TODA la carpeta del otro robot (locomoción/combate) a esta raza.
        ' Los clips compartidos puntuales (furniture) que esa raza SÍ usa siguen entrando por el WALK (resuelve clip-gens).
        coverageDirs.ExceptWith(foreignDirs)

        ' ── PASADA IDLE (estructural): expande los patrones IDLE.GNAM aplicables a la raza. $(Subgraph) → cada carpeta
        ' SAPT aplicada (coverageDirs); * → glob sobre animSet; literal → match directo. Cada archivo matcheado entra
        ' con su Category (= evento ENAM del IDLE). Esto es la fuente AUTORITATIVA del pool de gestos/poses/turns.
        ' Gateo de los patrones $(Subgraph) por el DNAM del IDLE ∈ behaviors REALMENTE caminados (graphCache.Keys,
        ' que incluye los alcanzados por hkbBehaviorReferenceGenerator — no solo DistinctBehaviorFiles).
        Dim coverageList = coverageDirs.ToList()
        Dim walkedBases As New HashSet(Of String)(graphCache.Keys.Select(Function(k) System.IO.Path.GetFileNameWithoutExtension(k)), StringComparer.OrdinalIgnoreCase)
        ' ⛔ El pase IDLE deduplica por ARCHIVO, NO por variante. Es un pase de COBERTURA: agrega archivos que el
        ' walk no alcanzó. Si el walk ya lo trajo — con el crop/speed que sea — no hay nada que cubrir.
        ' Deduplicarlo por la clave compuesta fabricaba una entrada "×1, sin crop" que NINGÚN dato declara: el record
        ' IDLE declara archivo (GNAM) y evento (ENAM), no parámetros de reproducción, y AddIdleClip los hardcodea.
        ' Eso es exactamente la over-inclusion que prohíbe la ley 100 % DATA-DRIVEN de más abajo, y además la entrada
        ' libre (RequiresFemale = False) DESOCULTABA la variante restringida del walk al unificar por archivo.
        ' Medido: 229 de 2.061 archivos de FO4 tenían al menos una variante no-default ⇒ esa era la cota; ahora es 0.
        ' ⛔ Se DERIVA de result en vez de mantenerse en paralelo a byClip: así es imposible agregar mañana una vía
        ' de inserción que actualice uno y se olvide del otro.
        Dim archivosVistos As New HashSet(Of String)(result.Select(Function(c) c.AnimationFile), StringComparer.OrdinalIgnoreCase)
        For Each ia In rb.IdleAnimations
            ' GATE de raza para TODOS los patrones (token y literal): el IDLE aplica solo si su behavior (DNAM) es uno que
            ' la raza REALMENTE camina. Sin esto, un patrón de path literal (Actors\Character\…\Quest\Cheering\…) matchea
            ' por existencia GLOBAL y le metería animaciones de Character/PowerArmor a un robot (que nunca camina ese DNAM).
            If ia.DnamBasename = "" OrElse Not walkedBases.Contains(ia.DnamBasename) Then Continue For
            Dim pat = ia.GnamPattern.Replace("/"c, "\"c)
            Dim candidates As New List(Of String)
            Dim tok = pat.IndexOf("$(Subgraph)", StringComparison.OrdinalIgnoreCase)
            If tok >= 0 Then
                Dim tail = pat.Substring(tok + "$(Subgraph)".Length).TrimStart("\"c)
                For Each d In coverageList : candidates.Add(d & "\" & tail) : Next
            ElseIf pat.IndexOf("$(", StringComparison.Ordinal) >= 0 Then
                Continue For   ' otro token desconocido → no expandir
            Else
                candidates.Add(CanonHkx(pat))   ' patrón de path literal (ej. Quest\Cheering\…\*.hkx) → existencia gatea
            End If
            For Each cand In candidates
                Dim cf = CanonHkx(cand)
                Dim star = cf.IndexOf("*"c)
                If star < 0 Then
                    If animSet.Contains(cf) AndAlso Not archivosVistos.Contains(cf) Then AddIdleClip(cf, ia.Category, archivosVistos, result, actorRoot, raceSkel)
                Else
                    Dim pre = cf.Substring(0, star), suf = cf.Substring(star + 1)
                    For Each f In animSet
                        If f.StartsWith(pre, StringComparison.OrdinalIgnoreCase) AndAlso f.EndsWith(suf, StringComparison.OrdinalIgnoreCase) AndAlso Not archivosVistos.Contains(f) Then AddIdleClip(f, ia.Category, archivosVistos, result, actorRoot, raceSkel)
                    Next
                End If
            Next
        Next

        ' ── 100% DATA-DRIVEN (decisión del usuario): la lista = WALK (clip-generators del behavior graph) +
        ' IDLE records (patrón GNAM, FO4). ⛔ NO agregar pasadas heurísticas por-nombre. Las dos que tientan:
        '   (1) "variant-by-name": agregar todo .hkx con el MISMO nombre que un clip-generator bajo otra carpeta SAPT.
        '       Medido = over-inclusion (archivos de OTROS actores: _1stPerson\, DynamicAnims\ [=DATG runtime],
        '       Supermutant\, PowerArmor\Furniture\). No sale de ningún record ni del behavior graph.
        '   (2) "opción-B alt_/to_": los arma el DynamicAnimationTaggingGenerator en RUNTIME → NO existen en los datos
        '       (sin lista estática, ni el CK las enumera).
        ' Ninguna de las dos es data-driven. El WALK es la resolución exacta del engine (clip-gen → archivo vía
        ' SAPT) y se conserva ÍNTEGRO. La cobertura IDLE de arriba SÍ es data-driven (patrón del record IDLE.GNAM,
        ' expansión $(Subgraph) = mecanismo del engine). SSE no llega acá con idles: sus IDLE son event-driven por el
        ' behavior graph (DNAM=behavior, ENAM=evento) → ya están en el WALK (medido cover=0 en las 161 razas).
        UnificarPorArchivo(result)
        Return result
    End Function

''' <summary>Dos cosas que son propiedad de la LISTA, no de un clip suelto, y por eso van aca y no en
''' la etiqueta.
''' <para>(1) Propaga a todas las variantes del mismo archivo la metadata que el walk acumula POR OBJETO.
''' `Roles`/`StateAxes`/`SourceBehaviorFiles`/`RequiresFemale`/`Is1stPersonOnly` se acumulan sobre la
''' entrada que devuelve el TryGetValue, asi que desde que el dedup separa variantes cada una solo
''' recibiria los subgraphs que la alcanzan. Un archivo alcanzado por un subgraph female-gated con crop A
''' y por uno neutro con crop B dejaria la variante A con RequiresFemale = True, y el filtro por defecto
''' del picker (AnimationPicker_Form.vb:158) la ESCONDERIA para un NPC varon. El cambio que existe para
''' EXPONER variantes no puede esconder una. Estos cinco campos describen como el GRAFO alcanza el
''' ARCHIVO, no como se reproduce, asi que el archivo es su granularidad correcta.</para>
''' <para>(2) El sufijo desambiguante. NO se calcula en la etiqueta: AnimClipLabel/LeafLabel son Shared de
''' UN clip y las dos ven listas DISTINTAS (el combo ve todos, el picker ve el filtrado por genero +
''' 1a persona + texto), asi que el mismo clip saldria con dos nombres, y el sufijo PARPADEARIA al tipear
''' en el filtro (Rebuild corre en cada TextChanged).</para></summary>
    Private Shared Sub UnificarPorArchivo(clips As List(Of ResolvedAnimationClip))
        If clips Is Nothing Then Return
        Dim ci = Globalization.CultureInfo.InvariantCulture
        For Each grupo In clips.Where(Function(c) c IsNot Nothing).GroupBy(Function(c) c.AnimationFile, StringComparer.OrdinalIgnoreCase)
            Dim lista = grupo.ToList()
            If lista.Count <= 1 Then Continue For

            ' (1) union de la metadata del ARCHIVO
            Dim roles = lista.SelectMany(Function(c) c.Roles).Distinct().ToList()
            Dim ejes = lista.SelectMany(Function(c) c.StateAxes).Distinct().ToList()
            Dim behs = lista.SelectMany(Function(c) c.SourceBehaviorFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ' ⛔ All, no Any: el clip requiere genero / 1a persona solo si NINGUNA ruta del grafo lo alcanza sin
            ' esa restriccion. Con Any, una variante restringida contagiaria a las libres y las escondia del picker.
            Dim reqF = lista.All(Function(c) c.RequiresFemale)
            Dim only1 = lista.All(Function(c) c.Is1stPersonOnly)
            For Each c In lista
                c.Roles.Clear() : c.Roles.AddRange(roles)
                c.StateAxes.Clear() : c.StateAxes.AddRange(ejes)
                c.SourceBehaviorFiles.Clear() : c.SourceBehaviorFiles.AddRange(behs)
                c.RequiresFemale = reqF
                c.Is1stPersonOnly = only1
            Next

            ' (2) sufijo desambiguante
            Dim vistos As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            For Each c In lista
                Dim partes As New List(Of String)
                Dim cs = If(Single.IsFinite(c.CropStartLocalTime), c.CropStartLocalTime, 0.0F)
                Dim ce = If(Single.IsFinite(c.CropEndLocalTime), c.CropEndLocalTime, 0.0F)
                ' ⛔ <> 0, no > 0: un crop NEGATIVO tambien distingue, y con `> 0` quedaba fuera del sufijo pero
                ' DENTRO de la clave, o sea dos entradas visualmente identicas. Medido 0 en vanilla; pasa con mods.
                If cs <> 0.0F OrElse ce <> 0.0F Then partes.Add($"crop {cs.ToString("0.###", ci)}–{ce.ToString("0.###", ci)} s")
                Dim vel = c.VelocidadReproduccion
                ' ⛔ El signo se muestra como flecha, no como "x-1": el guion no se lee como "al reves", y en los
                ' 51 archivos donde el signo es lo UNICO que separa dos variantes la etiqueta TIENE que decirlo.
                If vel < 0.0F Then
                    partes.Add(If(Math.Abs(Math.Abs(vel) - 1.0F) > 0.0005F, $"◀ ×{Math.Abs(vel).ToString("0.###", ci)}", "◀ reversa"))
                ElseIf Math.Abs(vel - 1.0F) > 0.0005F Then
                    partes.Add($"×{vel.ToString("0.###", ci)}")
                End If
                If c.IsPingPong Then partes.Add("↔ rebota")
                Dim su = If(partes.Count = 0, "", " · " & String.Join(" ", partes))
                Dim n = 0
                If vistos.TryGetValue(su, n) Then
                    vistos(su) = n + 1
                    ' Empate del sufijo FORMATEADO (x1,0001 vs x1,0002, que el "0.###" iguala). NO se desempata con
                    ' un ordinal: "GetUp" y "GetUp #2" no le dicen NADA al usuario, que es lo que este sufijo vino
                    ' a evitar. Se emite el valor crudo.
                    su = $" · crop {cs.ToString("R", ci)}–{ce.ToString("R", ci)} s ×{vel.ToString("R", ci)}"
                Else
                    vistos(su) = 1
                End If
                c.VarianteSufijo = su
            Next
        Next
    End Sub

    ''' <summary>Datos del clip que SOLO salen de abrir el .hkx: si es aditivo y si su crop se puede
    ''' honrar. Pasada LAZY — el llamador la corre en segundo plano y la UI lee los flags cuando estan.
    ''' <para>⛔ Lo que se cachea es del ARCHIVO (blendHint, frames, duracion); lo que se DERIVA es del
    ''' CLIP. Desde el dedup por variante, N clips comparten un .hkx pero cada uno tiene su crop, asi que
    ''' `CropIgnorado` NO se puede memoizar por archivo — se recalcula por clip con los datos cacheados.
    ''' Memoizarlo por archivo le daria a todas las variantes el veredicto de la primera.</para>
    ''' <para>El memo evita N descompresiones BA2 + N BuildGraph del MISMO archivo: el guard
    ''' <see cref="ResolvedAnimationClip.HkxFlagsKnown"/> es por OBJETO y no alcanza.</para>
    ''' <para>Idempotente. Cara igual (1 archivo por archivo distinto) ⇒ el caller la corre en background,
    ''' UNA vez por lista cacheada. loadHkx = el mismo Func(path→bytes) del caller (BA2 + loose).</para></summary>
    Public Shared Sub DetectHkxFlags(clips As IEnumerable(Of ResolvedAnimationClip), loadHkx As Func(Of String, Byte()))
        If clips Is Nothing OrElse loadHkx Is Nothing Then Return
        For Each c In clips
            If c Is Nothing OrElse c.HkxFlagsKnown Then Continue For
            ' ⛔ EL MEMO ES COMPARTIDO ENTRE RAZAS, no local a esta llamada.
            '
            ' `DatosDeArchivo` es funcion PURA del archivo de animacion: mismo archivo, mismos frames,
            ' misma duracion, mismo blendHint. Pero el memo era `Dim memo As New Dictionary` DENTRO de
            ' esta funcion, o sea uno por raza — y el preload de fondo enumera TODAS las razas del load
            ' order. Medido en el log del usuario (Skyrim): 114 razas, 86.331 clips, y las razas que
            ' comparten behavior declaran la MISMA lista de 1.904 clips. Cada raza volvia a abrir y a
            ' parsear los mismos archivos: ~217.000 lecturas de unos 6.100 archivos distintos.
            '
            ' Con el memo compartido, la primera raza paga y las otras 113 salen del diccionario.
            ' Acotado: ~6.100 entradas de 5 campos, y muere con el load order (`LimpiarMemoDeArchivos`,
            ' que llama `InvalidateParseCaches` junto con las demas caches con clave de FormID/ruta).
            Dim d As DatosDeArchivo = Nothing
            If Not _memoArchivos.TryGetValue(c.AnimationFile, d) Then
                d = LeerDatosDeArchivo(loadHkx, c.AnimationFile)
                _memoArchivos(c.AnimationFile) = d
            End If
            c.IsAdditive = d.EsAditivo
            ' ⛔ Se pregunta por el crop de ESTE clip, no del archivo. Y con la MISMA funcion que el player
            ' usa despues para aplicarlo: si el picker avisara con una ley propia, las dos divergirian.
            If d.Leido AndAlso (c.CropStartLocalTime <> 0.0F OrElse c.CropEndLocalTime <> 0.0F) Then
                c.CropIgnorado = Not HkxAnimationPlayer.RangoDeCrop(d.Frames, d.DuracionDeFrame, d.Duracion,
                                                                    c.CropStartLocalTime, c.CropEndLocalTime).honrable
            End If
            c.HkxFlagsKnown = True
        Next
    End Sub

    ''' <summary>Memo COMPARTIDO de <see cref="DatosDeArchivo"/> por archivo de animacion. Ver el
    ''' porque en <see cref="DetectHkxFlags"/>. Concurrente porque el preload de razas corre en el
    ''' pool y varias razas pueden pedir el mismo archivo a la vez; el peor caso es leerlo dos veces,
    ''' nunca devolver algo distinto (el valor es funcion pura del archivo).</summary>
    Private Shared ReadOnly _memoArchivos As _
        New System.Collections.Concurrent.ConcurrentDictionary(Of String, DatosDeArchivo)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Suelta el memo de archivos. Va con el resto de las caches con clave de ruta/FormID:
    ''' un cambio de load order puede hacer que la misma ruta resuelva a OTROS bytes.</summary>
    Public Shared Sub LimpiarMemoDeArchivos()
        _memoArchivos.Clear()
    End Sub

    ''' <summary>Lo que se lee UNA vez por archivo. <c>Leido = False</c> si no se pudo abrir o parsear:
    ''' ahi no se afirma nada del crop, en vez de asumir que esta bien.</summary>
    Private Structure DatosDeArchivo
        Public Leido As Boolean
        Public EsAditivo As Boolean
        Public Frames As Integer
        Public DuracionDeFrame As Double
        Public Duracion As Double
    End Structure

    Private Shared Function LeerDatosDeArchivo(loadHkx As Func(Of String, Byte()), animFile As String) As DatosDeArchivo
        Dim d As New DatosDeArchivo()
        Dim bytes = LoadFirstHkxCandidate(loadHkx, animFile)
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return d
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(bytes))
            ' ⛔ El binding sale de `hkaAnimationContainer.bindings`, no del primer bloque serializado.
            Dim ab = g.BindingPrincipal()
            If ab IsNot Nothing Then d.EsAditivo = (ab.BlendHint <> 0)
            ' ⛔⛔ TRES ESCALARES SE LEEN COMO TRES ESCALARES. NO se llama a `ParseAnimations`.
            '
            ' Aca decia "el grafo YA esta construido: leer frames y duracion no cuesta ni un archivo ni
            ' un parseo mas". ERA FALSO Y ESTA MEDIDO: `ParseAnimations` no lee campos, DESCOMPRIME EL
            ' SPLINE — materializa un transform por (frame x track), 6.262 objetos de promedio por
            ' animacion en Skyrim y 7.313 en Fallout, a 4.039 / 5.491 us por archivo
            ' (`HkxLoadOrderAudit --hkxperf`). Y esto corre UNA VEZ POR ARCHIVO DE ANIMACION DISTINTO
            ' de cada raza, desde el preload de fondo que enumera TODAS las razas del load order.
            '
            ' Lo que costaba, del log del usuario (Skyrim, una sola seleccion de NPC): ManakinRace sola
            ' declara 1.904 clips; el preload de razas corria durante los 14,9 s que la UI tardo en
            ' subir la geometria a GL, y la seleccion entera tardo 28,8 s. En Fallout no se notaba
            ' porque tiene muchas menos razas con behavior graph y muchos menos clips por raza.
            '
            ' Los tres campos son escalares del encabezado del objeto y el lector generado los da
            ' directo, game-aware, sin tocar el blob: `duration` sale de `hkaAnimation` (+0x14),
            ' `numFrames` (+0x38) y `frameDuration` (+0x50) de `hkaSplineCompressedAnimation`. Las
            ' propiedades de array del objeto (`Data`, `BlockOffsets`) son PEREZOSAS: no se tocan aca,
            ' asi que el blob no se materializa.
            '
            ' ⚠ Se mira SOLO `hkaSplineCompressedAnimation`, igual que antes: `ParseAnimations`
            ' tampoco miraba `hkaLosslessCompressedAnimation`, y para esos archivos `Leido` quedaba en
            ' False. Se conserva tal cual para no cambiar en silencio que clips reportan crop ignorado.
            ' ⛔ La animacion sale de `hkaAnimationContainer.animations`, no del primer bloque serializado.
            Dim ao = g.BloquesDelContenedor("animations", {"hkaSplineCompressedAnimation"}).FirstOrDefault(Function(x) x.ClassName.Equals("hkaSplineCompressedAnimation", StringComparison.OrdinalIgnoreCase))
            If ao IsNot Nothing Then
                Dim an = Havok.Canon.Objects.HkObj_HkaSplineCompressedAnimation.Read(g, ao)
                If an IsNot Nothing Then
                    d.Frames = an.NumFrames
                    d.DuracionDeFrame = CDbl(an.FrameDuration)
                    d.Duracion = CDbl(an.Duration)
                    d.Leido = True
                End If
            End If
        Catch
        End Try
        Return d
    End Function

    ''' <summary>Enumera los clips alcanzables desde un behavior file: sus hkbClipGenerator (resueltos por EXISTENCIA
    ''' sobre las rutas SAPT) Y los behaviors referenciados (hkbBehaviorReferenceGenerator @+0x88), con el MISMO
    ''' SAPT/Role/eje. visited (per-subgraph) evita ciclos pero permite re-usar un Core con otro SAPT.</summary>
    Private Shared Sub EnumBehaviorClips(behFile As String, saptFolders As List(Of String), role As String, stateAxis As String, reqFemale As Boolean, perspective As Integer,
                                         loadBehaviorHkx As Func(Of String, Byte()),
                                         animSet As HashSet(Of String), actorRoot As String, raceSkel As String,
                                         byClip As Dictionary(Of String, ResolvedAnimationClip),
                                         result As List(Of ResolvedAnimationClip),
                                         graphCache As Dictionary(Of String, HkxObjectGraph_Class),
                                         visited As HashSet(Of String), depth As Integer)
        If depth > 12 OrElse String.IsNullOrWhiteSpace(behFile) OrElse Not visited.Add(behFile) Then Return
        Dim graph As HkxObjectGraph_Class = Nothing
        If Not graphCache.TryGetValue(behFile, graph) Then
            Dim bytes = LoadFirstHkxCandidate(loadBehaviorHkx, behFile)
            If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                Try
                    graph = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(bytes))
                Catch
                End Try
            End If
            graphCache(behFile) = graph
        End If
        If graph Is Nothing Then Return

        ' NOTA aditivos: la aditividad es CANÓNICA del archivo de animación — hkaAnimationBinding.blendHint
        ' (=2 en RotateRing*_Add/CrippledNoise/DialogueIdle_Long1; =0 en clips normales). Un offset roto
        ' del parser (saltaba 2 arrays en vez de 3) la leía mal y motivó un scan por NOMBRE de
        ' DynamicAnimationTaggingGenerator acá — ELIMINADO: el binding del archivo es la única fuente.
        For Each obj In graph.GetObjectsByClassName("hkbClipGenerator")
            Dim cg = Havok.Canon.Objects.HkObj_HkbClipGenerator.Read(graph, obj)
            If IsNothing(cg) OrElse String.IsNullOrWhiteSpace(cg.AnimationName) Then Continue For
            Dim animFile = ResolveClipByExistence(cg.AnimationName, saptFolders, actorRoot, animSet)
            If animFile = "" Then Continue For
            Dim velEfectiva = VelocidadEfectiva(cg.PlaybackSpeed)
            Dim esPP = EsPingPong(cg.Mode)
            Dim claveClip = ClaveDedup(animFile, cg.CropStartAmountLocalTime, cg.CropEndAmountLocalTime, velEfectiva, esPP)
            Dim clip As ResolvedAnimationClip = Nothing
            If Not byClip.TryGetValue(claveClip, clip) Then
                clip = New ResolvedAnimationClip With {
                    .AnimationFile = animFile,
                    .ClipName = If(cg.Name, ""),
                    .PlaybackSpeed = cg.PlaybackSpeed,
                    .CropStartLocalTime = cg.CropStartAmountLocalTime,
                    .CropEndLocalTime = cg.CropEndAmountLocalTime,
                    .PlaybackMode = cg.Mode,
                    .IsPingPong = esPP,
                    .VelocidadReproduccion = velEfectiva,
                    .SourceSkeletonPath = SourceSkelForAnim(animFile, actorRoot, raceSkel)
                }
                byClip(claveClip) = clip
                result.Add(clip)
            End If
            If Not clip.SourceBehaviorFiles.Contains(behFile, StringComparer.OrdinalIgnoreCase) Then clip.SourceBehaviorFiles.Add(behFile)
            If Not clip.Roles.Contains(role) Then clip.Roles.Add(role)
            If Not clip.StateAxes.Contains(stateAxis) Then clip.StateAxes.Add(stateAxis)
            If Not reqFemale Then clip.RequiresFemale = False   ' alcanzable por un subgraph NEUTRO → disponible para varón
            If perspective <> 1 Then clip.Is1stPersonOnly = False
        Next

        ' Referencias a otros behaviors (relativas al actor del behavior referenciante), MISMO SAPT/Role/eje.
        Dim behRoot = ActorRootOfAnim(behFile)
        For Each refObj In graph.GetObjectsByClassName("hkbBehaviorReferenceGenerator")
            ' ⛔ `hkbBehaviorReferenceGenerator.behaviorName` DEL LECTOR GENERADO. Antes salia de una
            ' tabla escrita a mano (FO4 +0x88 / SSE +0x48); ahora la elige el packfile. Sin esto el walk
            ' NO seguia las sub-behaviors de SSE (Weap/Magic/Locomotion/...) y la lista salia corta.
            ' [[24-anim-behavior-por-raza]]
            Dim refLector = Havok.Canon.Objects.HkObj_HkbBehaviorReferenceGenerator.Read(graph, refObj)
            Dim refName = If(refLector Is Nothing, String.Empty, If(refLector.BehaviorName, String.Empty))
            If String.IsNullOrWhiteSpace(refName) Then Continue For
            EnumBehaviorClips(NormHkx(CombineActor(behRoot, refName)), saptFolders, role, stateAxis, reqFemale, perspective, loadBehaviorHkx,
                              animSet, actorRoot, raceSkel, byClip, result, graphCache, visited, depth + 1)
        Next
    End Sub

    ''' <summary>Resuelve el archivo de animación REAL por EXISTENCIA (mecanismo search-path del engine), SIN
    ''' heurística de nombres. clipRel = parte del animName tras "Animations\". CON SAPT: por cada ruta (en orden
    ''' de prioridad) prueba el path completo y luego sin el primer segmento (actor-autor del core compartido); la
    ''' existencia en <paramref name="animSet"/> decide; NO cae a redirects cross-actor (el core del Alien no agarra
    ''' "..\MirelurkQueen\…" de otro consumidor). SIN SAPT (root): ResolveActorRelative (incluye reuse "..\Character\…").</summary>
    Private Shared Function ResolveClipByExistence(animName As String, saptFolders As List(Of String),
                                                   actorRoot As String, animSet As HashSet(Of String)) As String
        If String.IsNullOrWhiteSpace(animName) Then Return ""
        Dim norm = animName.Replace("/"c, "\"c)
        Dim i = norm.IndexOf("Animations\", StringComparison.OrdinalIgnoreCase)
        Dim clipRel = If(i >= 0, norm.Substring(i + "Animations\".Length), norm.TrimStart("\"c, "."c))
        If clipRel = "" Then Return ""
        If saptFolders Is Nothing OrElse saptFolders.Count = 0 Then
            Dim cand = CanonHkx(ResolveActorRelative(actorRoot, norm))
            Return If(animSet.Contains(cand), cand, "")
        End If
        For Each s In saptFolders
            If String.IsNullOrWhiteSpace(s) Then Continue For
            Dim sn = s.Replace("/"c, "\"c).TrimEnd("\"c)
            Dim c1 = CanonHkx(sn & "\" & clipRel)
            If animSet.Contains(c1) Then Return c1
            Dim j = clipRel.IndexOf("\"c)
            If j >= 0 Then
                Dim c2 = CanonHkx(sn & "\" & clipRel.Substring(j + 1))
                If animSet.Contains(c2) Then Return c2
            End If
        Next
        Return ""
    End Function

    ''' <summary>Etiqueta de eje de ESTADO de un subgraph = los nombres de tipo (KYWD.TNAM) de sus keywords de
    ''' estado (tipo ≠ None), EXCLUYENDO 'Anim Gender' (eso va al checkbox de género). "Normal" si no hay ninguna.</summary>
    Private Shared Function StateAxisLabel(sakd As List(Of UInteger)) As String
        If sakd Is Nothing OrElse sakd.Count = 0 Then Return "Normal"
        Dim names = sakd.Where(Function(k) RaceBehaviorResolver.KeywordType(k) <> RaceBehaviorResolver.KwTypeNone AndAlso
                                            RaceBehaviorResolver.KeywordType(k) <> RaceBehaviorResolver.KwTypeAnimGender).
                         Select(Function(k) RaceBehaviorResolver.KeywordTypeName(k)).Distinct().OrderBy(Function(s) s).ToList()
        Return If(names.Count = 0, "Normal", String.Join("+", names))
    End Function

    ' project → CharacterFilenames → character → behaviorFilename (root behavior del actor). "" si no resuelve.
    Private Shared Function ResolveRootBehaviorFile(proj As String, loadBehaviorHkx As Func(Of String, Byte()), actorRoot As String) As String
        Dim pb = LoadFirstHkxCandidate(loadBehaviorHkx, NormHkx(proj))
        If pb Is Nothing Then Return ""
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(pb))
            For Each o In g.GetObjectsByClassName("hkbProjectStringData")
                Dim psd = Havok.Canon.Objects.HkObj_HkbProjectStringData.Read(g, o)
                If psd Is Nothing Then Continue For
                For Each cf In psd.CharacterFilenames
                    If String.IsNullOrWhiteSpace(cf) Then Continue For
                    Dim cb = LoadFirstHkxCandidate(loadBehaviorHkx, CombineActor(actorRoot, cf))
                    If cb Is Nothing Then cb = LoadFirstHkxCandidate(loadBehaviorHkx, cf)
                    If cb Is Nothing Then Continue For
                    Dim gc = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(cb))
                    For Each co In gc.GetObjectsByClassName("hkbCharacterStringData")
                        Dim csd = Havok.Canon.Objects.HkObj_HkbCharacterStringData.Read(gc, co)
                        If csd IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(csd.BehaviorFilename) Then Return NormHkx(CombineActor(actorRoot, csd.BehaviorFilename))
                    Next
                Next
            Next
        Catch
        End Try
        Return ""
    End Function

    ' Índice de existencia: todos los .hkx/.hkt del load order, canon (sin Meshes\, .hkt→.hkx) OrdinalIgnoreCase.
    Private Shared Function BuildAnimExistenceSet() As HashSet(Of String)
        Dim s As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each key In FilesDictionary_class.Dictionary.Keys
            If key.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase) OrElse key.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then s.Add(CanonHkx(key))
        Next
        Return s
    End Function

    ' Canon de un path .hkx/.hkt: quita "Meshes\", normaliza .hkt→.hkx. NO lowercasea (el match es OrdinalIgnoreCase;
    ' preservar case para que el path resuelto cargue tal cual vía FilesDictionary, que es OrdinalIgnoreCase igual).
    Private Shared Function CanonHkx(p As String) As String
        If String.IsNullOrEmpty(p) Then Return ""
        p = p.Replace("/"c, "\"c)
        If p.StartsWith("Meshes\", StringComparison.OrdinalIgnoreCase) Then p = p.Substring(7)
        Return NormHkx(p)
    End Function

    ''' <summary>Skeleton de Havok SÓLIDO para NPC Manager: NO el "hermano" del skeleton.nif (heurístico
    ''' de WM), sino el <c>rigName</c> que declara el behavior character. Cadena: project .hkx →
    ''' hkbProjectStringData.CharacterFilenames → character .hkx → hkbCharacterStringData.RigName
    ''' (ej. "CharacterAssets\skeleton.HKT", relativo a la carpeta del actor = dirname del project).
    ''' Devuelve el path .hkx normalizado (o "" si no se pudo resolver).</summary>
    Public Shared Function ResolveHavokSkeleton(rb As ResolvedRaceBehavior, loadBehaviorHkx As Func(Of String, Byte())) As String
        If IsNothing(rb) OrElse loadBehaviorHkx Is Nothing OrElse String.IsNullOrWhiteSpace(rb.Project) Then Return ""
        Dim actorRoot = DirName(rb.Project)   ' p.ej. "actors\Character"

        ' 1) project → character files
        Dim projBytes = LoadFirstHkxCandidate(loadBehaviorHkx, NormHkx(rb.Project))
        If projBytes Is Nothing Then Return ""
        Dim charFiles As New List(Of String)
        Try
            Dim g = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(projBytes))
            For Each o In g.GetObjectsByClassName("hkbProjectStringData")
                Dim psd = Havok.Canon.Objects.HkObj_HkbProjectStringData.Read(g, o)
                If psd IsNot Nothing Then charFiles.AddRange(psd.CharacterFilenames)
            Next
        Catch
        End Try

        ' 2) character → rigName (skeleton de Havok)
        For Each cf In charFiles
            If String.IsNullOrWhiteSpace(cf) Then Continue For
            Dim charBytes = LoadFirstHkxCandidate(loadBehaviorHkx, CombineActor(actorRoot, cf))
            If charBytes Is Nothing Then charBytes = LoadFirstHkxCandidate(loadBehaviorHkx, cf)
            If charBytes Is Nothing Then Continue For
            Try
                Dim gc = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(charBytes))
                For Each o In gc.GetObjectsByClassName("hkbCharacterStringData")
                    Dim csd = Havok.Canon.Objects.HkObj_HkbCharacterStringData.Read(gc, o)
                    If csd IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(csd.RigName) Then
                        Return NormHkx(CombineActor(actorRoot, csd.RigName))
                    End If
                Next
            Catch
            End Try
        Next
        Return ""
    End Function


    ' Actor root de un path de animación = prefijo antes de la subcarpeta estándar. Maneja creatures DLC
    ' de 3 segmentos: "Actors\DLC03\Angler\Animations\X.hkx" → "Actors\DLC03\Angler".
    Private Shared Function ActorRootOfAnim(animPath As String) As String
        If String.IsNullOrWhiteSpace(animPath) Then Return ""
        For Each marker In {"\Animations\", "\CharacterAssets\", "\Characters\", "\Behaviors\"}
            Dim i = animPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase)
            If i > 0 Then Return animPath.Substring(0, i)
        Next
        Return DirName(animPath)
    End Function

    ''' <summary>Skeleton .hkx con el que se interpreta un clip. Si el clip es del PROPIO actor del NPC (su actor-root
    ''' == <paramref name="actorRoot"/>) devuelve el skeleton de la RAZA/GÉNERO (<paramref name="raceSkel"/>, resuelto
    ''' del rigName del character del gender — SSE = skeleton_female.hkx). Los clips REUSADOS de otro actor
    ''' (cross-actor, ej. SuperMutant→death humano) usan el skeleton genérico de SU actor de origen. En FO4 esto es
    ''' no-op (raceSkel resuelve al MISMO archivo que el genérico, un único skeleton por actor sin split de género).</summary>
    ''' <summary>True si el clip rebota en vez de loopear. ⛔ Se normaliza porque la clave del dedup tiene que
    ''' separar por lo que el consumidor REALMENTE hace: en la app los modos 0, 1 y 2 se reproducen los tres en
    ''' loop (decisión del usuario: que un SINGLE_PLAY loopee es deliberado), así que meterlos crudos en la
    ''' clave partía variantes que suenan igual — medido, 203 entradas idénticas de más en SSE. El 3 sí se
    ''' reproduce distinto (onda triangular, ver HkxAnimationPlayer.FrameForNow) y por eso sí separa.</summary>
    Private Shared Function EsPingPong(mode As Integer) As Boolean
        Return mode = 3
    End Function

    ''' <summary>Normaliza la velocidad autorada a la que de verdad se reproduce. Ver
    ''' <see cref="ResolvedAnimationClip.VelocidadReproduccion"/>, que es donde queda guardada.</summary>
    Private Shared Function VelocidadEfectiva(speed As Single) As Single
        If Not Single.IsFinite(speed) OrElse speed = 0.0F Then Return 1.0F
        Return speed
    End Function

    ''' <summary>Clave de deduplicación de clips. ⛔ NO es sólo el archivo: dos hkbClipGenerator pueden apuntar
    ''' al MISMO .hkx y reproducirlo distinto. Medido: 161 archivos de FO4 y 796 de SSE tienen clips con
    ''' crop/speed distintos entre sí; con la clave vieja sobrevivía UNO ARBITRARIO (el primero del walk) y su
    ''' crop decída por todos — es lo que hacía que RudderLeft/RudderStraight/RudderRight (mismo
    ''' RudderAndFlaps.hkx, crops 0,0333/0,0333/0) colapsaran en una sola entrada y el timón volara.
    ''' <para>⛔ Sólo la usa el WALK. El pase IDLE NO deduplica por esta clave sino por ARCHIVO: es un pase de
    ''' COBERTURA y el record IDLE declara el archivo (GNAM) y el evento (ENAM), NO parámetros de
    ''' reproducción. Deduplicar el pase IDLE por variante fabricaba una entrada "×1, sin crop" que ningún dato
    ''' respalda — justo la over-inclusion que prohíbe la ley 100 % DATA-DRIVEN de más arriba.</para>
    ''' <para>`InvariantCulture` a propósito: `"R"` usa CurrentCulture y acá daría "0,0333" con coma —
    ''' consistente dentro del proceso, pero rompe cualquier golden comparado entre máquinas. `+ 0.0F`
    ''' normaliza el −0 (`(-0.0F):R` = "-0" ≠ "0" pero −0,0F = 0,0F ⇒ no deduplicarían). El crop no finito se
    ''' mapea a 0 porque es lo que el player va a hacer con él; medido 0 casos en vanilla (28.954 valores), es
    ''' seguro para mods.</para></summary>
    Private Shared Function ClaveDedup(animFile As String, crop0 As Single, crop1 As Single,
                                       velocidadEfectiva As Single, esPingPong As Boolean) As String
        Dim ci = Globalization.CultureInfo.InvariantCulture
        Dim c0 = If(Single.IsFinite(crop0), crop0, 0.0F) + 0.0F
        Dim c1 = If(Single.IsFinite(crop1), crop1, 0.0F) + 0.0F
        Return String.Concat(animFile, "|", c0.ToString("R", ci),
                             "|", c1.ToString("R", ci),
                             "|", (velocidadEfectiva + 0.0F).ToString("R", ci),
                             "|", If(esPingPong, "pp", "lp"))
    End Function

    Private Shared Function SourceSkelForAnim(animFile As String, actorRoot As String, raceSkel As String) As String
        Dim src = ActorRootOfAnim(animFile)
        If Not String.IsNullOrWhiteSpace(raceSkel) AndAlso
           String.Equals(src.TrimEnd("\"c), If(actorRoot, "").TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then
            Return raceSkel
        End If
        Return src & "\CharacterAssets\skeleton.hkx"
    End Function

    ' dirname con separador backslash (los paths de FO4 usan "\").
    Private Shared Function DirName(p As String) As String
        Dim i = p.LastIndexOf("\"c)
        Return If(i > 0, p.Substring(0, i), "")
    End Function

    ' Combina actorRoot + path actor-relativo; si el path ya es absoluto (empieza con actors\/meshes\), lo deja.
    Private Shared Function CombineActor(actorRoot As String, rel As String) As String
        If String.IsNullOrWhiteSpace(rel) Then Return rel
        Dim lc = rel.TrimStart("\"c)
        If lc.StartsWith("actors\", StringComparison.OrdinalIgnoreCase) OrElse lc.StartsWith("meshes\", StringComparison.OrdinalIgnoreCase) Then Return lc
        If actorRoot = "" Then Return lc
        Return actorRoot.TrimEnd("\"c) & "\" & lc
    End Function

    ' Combina con actorRoot y RESUELVE segmentos "..\" / ".\" → path Data-relativo limpio (ej.
    ' "Actors\Molerat" + "..\Bloatfly\Animations\X.hkx" = "Actors\Bloatfly\Animations\X.hkx").
    Private Shared Function ResolveActorRelative(actorRoot As String, rel As String) As String
        If String.IsNullOrWhiteSpace(rel) Then Return ""
        Dim combined = CombineActor(actorRoot, rel)
        Dim stack As New List(Of String)
        For Each seg In combined.Split("\"c)
            If seg = "" OrElse seg = "." Then Continue For
            If seg = ".." Then
                If stack.Count > 0 Then stack.RemoveAt(stack.Count - 1)
            Else
                stack.Add(seg)
            End If
        Next
        Return String.Join("\", stack)
    End Function

    ' Carga probando candidatos de path (con/sin "Meshes\", .hkx/.hkt).
    Public Shared Function LoadFirstHkxCandidate(loader As Func(Of String, Byte()), path As String) As Byte()
        For Each cand In Candidates(path)
            Dim b = loader(cand)
            If b IsNot Nothing AndAlso b.Length > 0 Then Return b
        Next
        Return Nothing
    End Function

    Private Shared Iterator Function Candidates(path As String) As IEnumerable(Of String)
        If String.IsNullOrWhiteSpace(path) Then Return
        Dim variants As New List(Of String) From {path}
        If path.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase) Then variants.Add(path.Substring(0, path.Length - 4) & ".hkt")
        If path.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then variants.Add(path.Substring(0, path.Length - 4) & ".hkx")
        For Each v In variants
            Yield v
            If Not v.StartsWith("Meshes\", StringComparison.OrdinalIgnoreCase) Then Yield "Meshes\" & v
        Next
    End Function

    Private Shared Function NormHkx(p As String) As String
        If String.IsNullOrWhiteSpace(p) Then Return ""
        If p.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) Then Return p.Substring(0, p.Length - 4) & ".hkx"
        Return p
    End Function

    ''' <summary>Agrega una entrada de COBERTURA para un archivo que el walk no alcanzó.
    ''' <para>⛔ Recibe `archivosVistos` (por ARCHIVO) y no `byClip` (por VARIANTE): después de este pase nadie
    ''' vuelve a leer byClip — su único lector es el walk, que ya corrió entero — así que escribirlo acá era
    ''' estado muerto. Y esta entrada NO declara parámetros de reproducción porque el record IDLE no los
    ''' tiene: los de abajo son defaults, no datos.</para></summary>
    Private Shared Sub AddIdleClip(animFile As String, category As String, archivosVistos As HashSet(Of String), result As List(Of ResolvedAnimationClip), actorRoot As String, raceSkel As String)
        Dim clip As New ResolvedAnimationClip With {
            .AnimationFile = animFile,
            .ClipName = "",
            .PlaybackSpeed = 1.0F,
            .SourceSkeletonPath = SourceSkelForAnim(animFile, actorRoot, raceSkel),
            .FromBehaviorGraph = False,
            .RequiresFemale = False,
            .Is1stPersonOnly = (animFile.IndexOf("\_1stPerson\", StringComparison.OrdinalIgnoreCase) >= 0),
            .Category = category
        }
        archivosVistos.Add(animFile)
        result.Add(clip)
    End Sub

    Private Shared Function RoleName(role As Integer) As String
        Select Case role
            Case 0 : Return "MT"
            Case 1 : Return "Weapon"
            Case 2 : Return "Furniture"
            Case 3 : Return "Idle"
            Case 4 : Return "Pipboy"
            Case Else : Return "Other"
        End Select
    End Function
End Class

''' <summary>Una animación reproducible del NPC/raza: el archivo de clip .hkx, el nombre del clip
''' (hkbClipGenerator), velocidad, y el contexto (Roles + behaviors de origen). Se reproduce con
''' HkxPoseImport sobre el skeleton de la raza.</summary>
Public Class ResolvedAnimationClip
    Public AnimationFile As String = ""        ' path Data-relativo YA RESUELTO por existencia (.hkt→.hkx)
    Public ClipName As String = ""             ' nombre del hkbClipGenerator
    Public PlaybackSpeed As Single = 1.0F
    ''' <summary>Segundos recortados del ARRANQUE por el hkbClipGenerator (cropStartAmountLocalTime).
    ''' El motor NO reproduce ese tramo. Medido: 212 de 4.504 clips de FO4 y 95 de 9.973 de SSE tienen
    ''' crop; peor cropStart 6,667 s (FO4) / 9,644 s (SSE).</summary>
    Public CropStartLocalTime As Single = 0.0F
    ''' <summary>Segundos recortados del FINAL (cropEndAmountLocalTime).</summary>
    Public CropEndLocalTime As Single = 0.0F
    ''' <summary>hkbClipGenerator::m_mode. Censado sobre 14.477 generators de los dos juegos:
    ''' 0=SINGLE_PLAY (7.298), 1=LOOPING (6.459), 2=USER_CONTROLLED (719), 3=PING_PONG (<b>1</b>:
    ''' tailbehavior.hkx :: 1HM_WalkForward, sólo SSE). ⛔ Los NOMBRES del enum salen del SDK de Havok,
    ''' NO están medidos por RE en este repo: lo medido es que sólo aparecen los valores 0..3.
    ''' <para>Decisión del usuario: 0/1/2 se reproducen TODOS en loop en la app (que un SINGLE_PLAY
    ''' loopee es deliberado, no un bug). El 3 sí cambia la reproducción — ver <see cref="EsPingPong"/>.</para></summary>
    Public PlaybackMode As Integer = 0
    ''' <summary>Sufijo que DISTINGUE esta variante de las otras del MISMO archivo (" · crop 0,033–0 s",
    ''' " · ×−1"). Vacío si el archivo tiene una sola variante ⇒ el 100 % de lo que había antes se ve igual.
    ''' Lo estampa <see cref="EnumerateClips"/> AL FINAL (no al final del walk: el pase IDLE agrega
    ''' después). Medido: 40 etiquetas ambiguas en 17 archivos de FO4 y 58 en 20 de SSE sin esto.</summary>
    Public VarianteSufijo As String = ""
    ''' <summary>El clip rebota (ida y vuelta) en vez de loopear: <see cref="PlaybackMode"/> = 3.
    ''' <para>⛔ Se guarda DERIVADO en vez de exponer un helper Public: `MainForm` vive en otro ensamblado y
    ''' `Friend` no cruza (los 8 InternalsVisibleTo de esta lib son todos probes, ninguno es la app), pero el
    ''' .vbproj declara ocho veces que la superficie de API que se DISTRIBUYE no cambia. Un campo de datos no
    ''' es superficie nueva; una función Public sí.</para></summary>
    Public IsPingPong As Boolean = False
    ''' <summary>Velocidad con la que el clip REALMENTE se reproduce, SIGNO INCLUIDO: 0, NaN y ±Inf valen ×1.
    ''' <para>⛔ UNA ley, UN lugar. `MainForm.vb` repetía esta normalización a mano y quedaba acoplada a la
    ''' clave del dedup por convención: el día que una cambiara, el dedup separaba variantes que se reproducen
    ''' igual (o al revés) y ningún gate lo veía. Ahora las dos leen ESTE campo.</para>
    ''' <para>El signo NO se puede tirar: Bethesda autora la animación hacia atrás reproduciendo la de adelante
    ''' en reversa — `RifleIdleReadyCoverRightKneelShuffleBackward` apunta a `...ShuffleForward.hkt` con
    ''' speed −1. Medido: 108 clips negativos (17 FO4 + 91 SSE) y 51 archivos donde el signo es lo ÚNICO que
    ''' separa dos variantes.</para>
    ''' <para>El 0 aparece ENTERO cuando el PackfileFormat no es Fallout64 ni Skyrim64: HkbLayout cae al
    ''' `Case Else` con ClipPlaybackSpeed = -1 y ReadSingleAt devuelve 0.0F para TODOS los generators de ese
    ''' archivo (HkxBehaviorGraphParser.vb:272-273, :297). Medido: 34 clips con speed 0 en vanilla.</para></summary>
    Public VelocidadReproduccion As Single = 1.0F
    Public SourceSkeletonPath As String = ""   ' skeleton del actor de ORIGEN de la anim (para interpretarla)
    ''' <summary>Aditivo: el archivo de animación resuelto tiene hkaAnimationBinding.BlendHint &lt;&gt; 0
    ''' (1=ADDITIVE_DEPRECATED, 2=ADDITIVE; ambos = overlay, no pose standalone). Lo puebla
    ''' DetectHkxFlags (lazy, carga el archivo). El selector lo muestra con insignia ⊕.</summary>
    Public IsAdditive As Boolean = False
    ''' <summary>El crop que declara el hkbClipGenerator NO se puede honrar y el clip se va a reproducir
    ''' ENTERO. Lo decide <c>HkxAnimationPlayer.RangoDeCrop</c>, la MISMA funcion que despues aplica el
    ''' rango: si se calculara aparte, el picker podria decir una cosa y el player hacer otra.
    ''' <para>Solo tiene sentido con <see cref="HkxFlagsKnown"/> = True: hasta que la pasada lazy lea el
    ''' archivo no se sabe (hace falta la Duration del .hkx, que no esta en el behavior graph).
    ''' Medido: 38 clips de FO4 con crop que deja el rango vacio.</para></summary>
    Public CropIgnorado As Boolean = False
    ''' <summary>True cuando la pasada lazy ya leyo el .hkx y lleno lo que SOLO sale del archivo:
    ''' <see cref="IsAdditive"/> y <see cref="CropIgnorado"/>. Guard de <c>DetectHkxFlags</c> para no
    ''' recargar. (Se llamaba AdditiveKnown, cuando el additive era lo unico que la pasada leia.)</summary>
    Public HkxFlagsKnown As Boolean = False
    ''' <summary>True si el clip SOLO es alcanzable vía subgraphs de 1ª persona (SRAF.Perspective=1):
    ''' cámara/viewmodel (brazos del player), inútil para preview de NPC. Pasa a False en cuanto un
    ''' subgraph 3ª-persona o el root behavior (Perspective=none) lo alcanza. El selector lo oculta por
    ''' defecto. Análogo a RequiresFemale.</summary>
    Public Is1stPersonOnly As Boolean = True
    ''' <summary>False = clip "search-path-only": el archivo existe bajo una ruta SAPT de la raza pero NINGÚN
    ''' hkbClipGenerator estático lo referencia (variantes mood/archetype/gender + gestos/diálogo/special-idle
    ''' que el engine reproduce por evento en runtime). Lo agrega la pasada de cobertura por existencia; no trae
    ''' metadata de behavior (Roles se propagan por carpeta). True = vino del walk del behavior graph.</summary>
    Public FromBehaviorGraph As Boolean = True
    ''' <summary>Categoría semántica del clip cuando vino de un patrón IDLE (Talk_M/IdleDialogue/Listen/…); "" si no.</summary>
    Public Category As String = ""
    Public ReadOnly Property Roles As New List(Of String)               ' MT/Weapon/Furniture/Idle/Pipboy/Core
    Public ReadOnly Property SourceBehaviorFiles As New List(Of String) ' behavior .hkx que lo contienen
    ''' <summary>Ejes de ESTADO (nombre del tipo KYWD.TNAM de los SAKD del subgraph: "Anim Injured"/"Anim Archetype"/
    ''' "Anim Flavor"/"Attach Point"…), o "Normal" si el subgraph no tiene keyword de estado. Para el árbol del selector.</summary>
    Public ReadOnly Property StateAxes As New List(Of String)
    ''' <summary>True si el clip SOLO es alcanzable vía subgraphs que requieren la keyword 'Anim Gender' (Female).
    ''' Se pone False en cuanto un subgraph NEUTRO (sin gender) lo alcanza (queda disponible para varón).
    ''' El selector lo usa para el checkbox "filter by gender".</summary>
    Public RequiresFemale As Boolean = True
End Class
