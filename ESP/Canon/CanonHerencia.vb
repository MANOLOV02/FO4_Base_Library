Imports System.Linq

Namespace Canon

    ''' <summary>La herencia por <c>ARMO.TNAM</c>, en UN solo lugar y como la hace el motor.
    '''
    ''' <para><b>LA LEY</b>, con cita en los DOS binarios: si un ARMO declara <c>TNAM</c>, un conjunto
    ''' CERRADO de componentes sale del TERMINAL de la cadena, y los <c>MODL</c> del hijo son letra
    ''' muerta — el motor ni siquiera los resuelve.</para>
    '''
    ''' <para><b>SkyrimSE.exe 1.7.104.</b> RTTI → vtable de <c>TESObjectARMO</c> en <c>0x141816580</c>;
    ''' slot <c>+0x98</c> = <c>0x14027E780</c> = <c>InitItem</c>. Con <c>TNAM ≠ 0</c> la rama viva va de
    ''' <c>0x14027E837</c> a <c>0x14027E95E</c>: <b>11 llamadas, 12 componentes, y no hay una 13ª</b>.
    ''' <c>templateArmor</c> en <c>+0x220</c>.</para>
    '''
    ''' <para><b>Fallout4.exe 1.11.240.</b> <c>0x1404626A0</c> es la misma función; <c>templateArmor</c>
    ''' en <c>+0x2C0</c>; rama viva <c>0x14046276F</c>–<c>0x1404628D5</c>, <b>13 componentes</b> — cuatro
    ''' de ellos inexistentes en Skyrim (<c>OBTE/OBTS</c>, <c>INRD</c>, <c>DAMA</c>,
    ''' <c>FNAM\Base Addon Index</c>). ⛔ <b>La tabla es un dato POR JUEGO</b>: aplicar la de Skyrim a
    ''' FO4 deja <c>Combinations</c> y <c>BaseAddonIndex</c> saliendo del hijo, que junto con
    ''' <c>Models</c> son los tres insumos de qué armature dibuja FO4.</para>
    '''
    ''' <para><b>Precondición, citada:</b> si el <c>TNAM</c> no resuelve, <c>0x14027E808</c> hace
    ''' <c>test rdx,rdx / je 0x14027E963</c> y el motor usa los campos del HIJO. ⛔ No es un caso de
    ''' error: devolver <c>Nothing</c> —o tirar— sería implementar la precondición al revés, y llega a
    ''' <c>NpcMeshCollector.CollectArmoCandidates</c> y <c>NpcMaterialResolver</c>, que NO atrapan.</para>
    '''
    ''' <para><b>Por qué MATERIALIZAR y no decorar:</b> la lista de armatures no es miembro de
    ''' <see cref="IArmo"/> —vive en <c>ArmoFO4.Models</c> / <c>ArmoSSE.Armature</c>—, las dos clases son
    ''' <c>NotInheritable</c>, y 25 sitios de la app bajan a ellas con <c>TryCast</c>. Un decorador que
    ''' sólo implemente <c>IArmo</c> los deja en <c>Nothing</c> <b>sin excepción y sin log</b> ⇒ NPC
    ''' desnudos en los dos juegos. Clonar y devolver por <see cref="CanonRecords.Reenvolver"/> da la
    ''' MISMA clase concreta, así que los <c>TryCast</c> sobreviven.</para>
    '''
    ''' <para><b>Por qué la llave es <c>Def.Name</c> y no la firma:</b> <c>WbNode.BySignature</c> NO
    ''' recursa, y los armatures cuelgan de un contenedor sin firma (<c>Armature</c> en SSE,
    ''' <c>Models</c> en FO4). Medido: mutar la llave a firma falla en <b>exactamente los 2 records
    ''' divergentes</b> del corpus — el injerto no haría nada justo en el único campo que diverge.</para>
    ''' </summary>
    Public Module CanonHerencia

        ''' <summary>Los miembros de primer nivel que el motor toma del TERMINAL, <b>por juego</b>.
        ''' <para>⛔ La lista sale de la DEF y se VALIDA contra ella en las dos direcciones
        ''' (<see cref="ValidarTabla"/>). Un nombre mal escrito no coincide con nada y el campo se queda
        ''' del lado del HIJO <b>en silencio</b>: así estaban <c>"BMCT"</c> (el miembro se llama
        ''' <c>Ragdoll Constraint Template</c>) y <c>"Bash Impact Data Set"</c> en FO4 (allá es
        ''' <c>Block Bash Impact Data Set</c>). Ni el corpus ni los sujetos sintéticos los cazaron.</para>
        ''' <para><c>Body Template</c> es la otra rama del <c>RUnion</c> de <c>BOD2</c>/<c>BODT</c>: la
        ''' unión no crea nodo intermedio, y 10 records del corpus traen <c>BODT</c>.</para>
        ''' <para>⛔ <c>Data</c> de Skyrim NO está: ese nodo trae <c>Value</c> (del HIJO,
        ''' <c>TESValueForm +0x68</c>, que <b>no se copia en ninguna de las dos ramas</b>) y
        ''' <c>Weight</c> (del TERMINAL, copiado en <c>0x14027E85A</c>) juntos, así que ahí la llave es
        ''' la HOJA. Es la ÚNICA excepción, y sale de cruzar la tabla del binario con la DEF. En FO4 no
        ''' hay excepción: <c>InstanceData +0x250</c> hereda Value+Weight+Health y el nodo va entero.</para>
        ''' </summary>
        Public Function MiembrosHeredados(juego As WbGame) As HashSet(Of String)
            If juego = WbGame.Fallout4 Then
                Return New HashSet(Of String)(New String() {
                    "Race",                       ' 0x140462777 -> +0x078  RNAM
                    "Destructible",               ' 0x140462798 -> +0x0A0  DEST/DSTD/DSTF/DMDL
                    "Sound - Pick Up",            ' 0x1404627B9 -> +0x0B0  YNAM
                    "Sound - Put Down",           '                        ZNAM
                    "Male",                       ' 0x1404627DA -> +0x0C8  MOD2/MO2T/MODC/MO2S/ICON/MICO
                    "Female",                     '                        MOD4/MO4T/MO4S/ICO2/MIC2
                    "Equipment Type",             ' 0x1404627FB -> +0x1D0  ETYP
                    "Biped Body Template",        ' 0x14046281C -> +0x1E0  BOD2
                    "Block Bash Impact Data Set", ' 0x14046283D -> +0x1F0  BIDS
                    "Alternate Block Material",   '                        BAMT
                    "Keywords",                   ' 0x14046285E -> +0x208  KSIZ/KWDA
                    "Description",                ' 0x14046287F -> +0x228  DESC
                    "Instance Naming",            ' 0x1404628A0 -> +0x240  INRD      (no existe en SSE)
                    "Object Template",            ' 0x1404628C3 -> +0x030  OBTE/OBTS (no existe en SSE)
                    "DATA",                       ' 0x1404628D0 -> +0x250  Value+Weight+Health
                    "FNAM",                       '                        ArmorRating+BaseAddonIndex+Stagger
                    "DAMA",                       '                        Resistance
                    "Models"                      ' 0x1404628D0 -> +0x2A8  INDX+MODL
                }, StringComparer.Ordinal)
            End If
            Return New HashSet(Of String)(New String() {
                "Race",                        ' 0x14027E83F -> +0x040  RNAM
                "Destructible",                ' 0x14027E878 -> +0x088
                "Sound - Pick Up",             ' 0x14027E896 -> +0x098  YNAM
                "Sound - Put Down",            '                        ZNAM
                "Male",                        ' 0x14027E8B4 -> +0x0B0  MOD2../ICON
                "Female",                      '                        MOD4../ICO2
                "Ragdoll Constraint Template", '                        BMCT (LoadForm 0x14027E2F2 -> this+0x178)
                "Equipment Type",              ' 0x14027E8D2 -> +0x1A0  ETYP
                "Biped Body Template",         ' 0x14027E8F0 -> +0x1B0  BOD2
                "Body Template",               '                        BODT (la otra rama del RUnion)
                "Bash Impact Data Set",        ' 0x14027E90E -> +0x1C0  BIDS
                "Alternate Block Material",    '                        BAMT
                "Keywords",                    ' 0x14027E92C -> +0x1D8  KSIZ/KWDA
                "Description",                 ' 0x14027E94A -> +0x1F0  DESC
                "Armor Rating",                ' dentro de 0x14027E540: 0x14027E5DF / 0x14027E5ED  DNAM
                "Armature"                     ' 0x14027E959 -> +0x208  MODL
            }, StringComparer.Ordinal)
        End Function

        ''' <summary>Los miembros que el HIJO conserva. Es el complemento de
        ''' <see cref="MiembrosHeredados"/> sobre la DEF, y está escrito aparte a propósito: así
        ''' <see cref="ValidarTabla"/> puede exigir que NINGÚN miembro quede sin lado.
        ''' <para>⛔ <c>Template Armor</c> va acá: si se heredara, la vista efectiva declararía el
        ''' <c>TNAM</c> del ABUELO y una segunda pasada recorrería otra cadena.</para></summary>
        Public Function MiembrosDelHijo(juego As WbGame) As HashSet(Of String)
            Dim comunes = New String() {"Editor ID", "Virtual Machine Adapter", "Object Bounds",
                                        "Name", "Enchantment", "Template Armor"}
            If juego = WbGame.Fallout4 Then
                Return New HashSet(Of String)(comunes.Concat(New String() {
                    "Preview Transform",    ' PTRN, +0x050 — LoadForm 0x1404583B5, no tocado por la rama
                    "Attach Parent Slots"   ' APPR, +0x2C8 — LoadForm 0x140461DE5, no tocado por la rama
                }), StringComparer.Ordinal)
            End If
            ' SSE: `Data` trae Value (del hijo) y Weight (del terminal): el nodo NO se injerta, y el
            ' peso entra por la excepción de hoja. Ver `InjertarExcepcionesDeHoja`.
            Return New HashSet(Of String)(comunes.Concat(New String() {"Data"}), StringComparer.Ordinal)
        End Function

        ''' <summary>⛔ La tabla contra la DEF, en las DOS direcciones. Devuelve los problemas; vacío si
        ''' está sana. Muere por partida doble: un nombre que no existe deja el campo del lado del hijo
        ''' sin que nada falle, y un miembro sin clasificar miente para siempre el día que una
        ''' resincronización de xEdit agregue uno.
        ''' <para>⛔ <b>QUIÉN LA EJECUTA</b>: <c>Tools\OutfitDraftSaveGate</c>, caso <c>TABLA</c>, y para
        ''' <b>los dos juegos en la misma corrida</b> —no depende del corpus ni del <c>Data\</c>, así que
        ''' no hay razón para dejar la mitad sin testigo—. Esto se escribe porque durante un tiempo el
        ''' docstring decía «lo ejecuta el gate» y era falso: la función no tenía un solo llamador, y el
        ''' gate corría una RÉPLICA suya que ya había derivado (su lista del lado del hijo era la unión de
        ''' los dos juegos escrita a mano, no comprobaba la dirección hijo→DEF, y no sabía de
        ''' <c>RUnion</c>). Si esta línea vuelve a quedar sin call site, la tabla vuelve a no medirse.</para>
        ''' <para>Es consciente de <c>RUnion</c>: la unión no crea nodo intermedio, así que el nodo lleva
        ''' el nombre de la RAMA elegida y las dos ramas son nombres válidos.</para></summary>
        Public Function ValidarTabla(juego As WbGame) As List(Of String)
            Dim problemas As New List(Of String)
            Dim def = WbSchema.Get(juego, "ARMO")
            If def Is Nothing Then
                problemas.Add($"{juego}: el formato no declara ARMO")
                Return problemas
            End If
            Dim nombres As New HashSet(Of String)(StringComparer.Ordinal)
            Dim agregar As Action(Of WbMemberDef) = Nothing
            agregar = Sub(m As WbMemberDef)
                          If m Is Nothing Then Return
                          If Not String.IsNullOrEmpty(m.Name) Then nombres.Add(m.Name)
                          Dim u = TryCast(m, WbRUnionDef)
                          If u IsNot Nothing Then
                              For Each r In u.Members
                                  agregar(r)
                              Next
                          End If
                      End Sub
            For Each m In def.Members
                agregar(m)
            Next
            Dim heredados = MiembrosHeredados(juego)
            Dim delHijo = MiembrosDelHijo(juego)
            For Each n In heredados
                If Not nombres.Contains(n) Then
                    problemas.Add($"{juego}: la tabla nombra '{n}' y la DEF del ARMO no tiene ese " &
                                  "miembro. Ese campo se quedaria del lado del HIJO sin que nada falle.")
                End If
            Next
            For Each n In delHijo
                If Not nombres.Contains(n) Then
                    problemas.Add($"{juego}: '{n}' esta declarado del HIJO y la DEF no lo tiene.")
                End If
            Next
            For Each m In def.Members
                If String.IsNullOrEmpty(m.Name) Then Continue For
                If heredados.Contains(m.Name) OrElse delHijo.Contains(m.Name) Then Continue For
                Dim u = TryCast(m, WbRUnionDef)
                If u IsNot Nothing AndAlso u.Members.Any(Function(r) heredados.Contains(r.Name) OrElse delHijo.Contains(r.Name)) Then Continue For
                problemas.Add($"{juego}: el miembro '{m.Name}' de la DEF no esta clasificado ni como " &
                              "heredado ni como del hijo. Un campo sin lado declarado miente para siempre.")
            Next
            Return problemas
        End Function

        ''' <summary>La vista EFECTIVA de un ARMO: lo que el motor va a usar, no lo que dice el archivo.
        ''' <para><paramref name="resolver"/> es el resolvedor de vistas CRUDAS por FormID. La app le pasa
        ''' su resolvedor cacheado y draft-aware, así que el borrador gana <b>para el hijo y para el
        ''' terminal</b> por la ley de override que ya existe — no se agrega ninguna regla.</para>
        ''' <para>⛔ La llamada interna es SIEMPRE cruda. Si no, cada apertura cuesta O(profundidad²).</para>
        ''' <para><b>Devuelve la CRUDA</b> exactamente cuando <see cref="CaminarCadena"/> devuelve el hijo
        ''' como vista, que son TRES casos y no dos: no hay herencia (<c>TNAM = 0</c>); el <c>TNAM</c>
        ''' cuelga <b>en el primer salto</b> (la precondición citada); o el <c>TNAM</c> apunta al PROPIO
        ''' record. ⛔ Un <c>TNAM</c> colgado a DOS o más saltos NO devuelve la cruda: materializa contra el
        ''' último eslabón resuelto, y tiene que hacerlo o la prenda se cae del render. La tabla completa,
        ''' caso por caso, está en <see cref="CaminarCadena"/> — acá no se repite.</para>
        ''' <para><b>Tira</b> sólo si la materialización falla, que es otra cosa.</para>
        ''' <para>Costo medido: <b>~0,06 ms</b> por ARMO con la cadena incluida (4 corridas del mismo
        ''' binario: 0,0589 · 0,0593 · 0,0657 · 0,0690 ⇒ ~17 % de ruido). Profundidad real del corpus:
        ''' <b>1 en el 100 %</b> de los 2.679, <b>0 cadenas cíclicas y 0 <c>TNAM</c> colgados</b> (adv-27,
        ''' los dos órdenes de carga) — por eso cachear la CADENA no compra nada y pagaría el borrador en
        ''' el medio, que ningún guard por FormID puede cazar. Los casos degenerados de la tabla de
        ''' <see cref="CaminarCadena"/> son todos cobertura para plugins de TERCEROS: ninguno aparece con
        ''' los plugins del usuario.</para></summary>
        Public Function ArmoEfectivo(formID As UInteger, resolver As Func(Of UInteger, IArmo)) As IArmo
            If resolver Is Nothing Then Return Nothing
            Dim hijo = resolver(formID)
            If hijo Is Nothing Then Return Nothing
            Dim actual = TerminalDeLaCadena(hijo, formID, resolver)
            If ReferenceEquals(actual, hijo) Then Return hijo

            Dim efectiva = Materializar(hijo, actual)
            If efectiva Is Nothing Then
                Throw New InvalidOperationException(
                    $"ArmoEfectivo: la materializacion de {formID:X8} contra su terminal fallo. " &
                    "⛔ Esto NO es ninguno de los casos degenerados de la cadena — esos ya salieron por " &
                    "el `ReferenceEquals` de arriba o materializaron contra el ultimo eslabon resuelto. " &
                    "Es el arbol o el contexto en Nothing, y devolver la cruda aca reintroduciria el " &
                    "defecto en silencio.")
            End If
            Return efectiva
        End Function

        ''' <summary>⛔ LA ÚNICA CAMINATA DE LA CADENA DE <c>TNAM</c> DEL ÁRBOL. Devuelve el TERMINAL como
        ''' vista Y como FormID, porque los dos consumidores piden cosas distintas y partir la caminata en
        ''' dos es exactamente cómo empezaron a contestar distinto.
        '''
        ''' <para><b>QUÉ DEVUELVE, CASO POR CASO.</b> Esta tabla se lee contra el código de abajo, línea
        ''' por línea: es la tercera redacción de este bloque y las dos anteriores afirmaron leyes que el
        ''' código no tenía.</para>
        ''' <list type="table">
        ''' <item><term>No declara <c>TNAM</c></term><description>hijo · hijo · hijo</description></item>
        ''' <item><term><c>TNAM</c> colgado en el PRIMER salto</term><description>hijo · hijo · hijo —
        ''' es la precondición CITADA: <c>0x14027E808</c> hace <c>test rdx,rdx / je 0x14027E963</c> y el
        ''' record del puntero nulo usa sus propios campos. Acá ese record ES el hijo.</description></item>
        ''' <item><term><c>TNAM</c> que apunta al PROPIO record (<c>A→A</c>)</term><description>hijo ·
        ''' hijo · hijo. Es un ciclo, pero de largo 1: se detecta en la primera vuelta con
        ''' <c>sig = formID</c>, así que las tres caras coinciden en el hijo. Es el ÚNICO ciclo que
        ''' devuelve la cruda.</description></item>
        ''' <item><term><c>TNAM</c> colgado a DOS o más saltos (<c>A→B→X</c>)</term><description>último
        ''' RESUELTO (B) en las tres caras. ⛔ La cita NO cubre esto: el del puntero nulo es B, no A, y el
        ''' motor igual copia de A a B porque el <c>templateArmor</c> de A no es nulo.</description></item>
        ''' <item><term>Ciclo de dos o más (<c>A→B→A</c>)</term><description>materializar y FormID: el
        ''' último RESUELTO (B). Identidad: el primer nodo RE-VISITADO (A).</description></item>
        ''' <item><term>Cola a un ciclo (<c>A→B→C→B</c>)</term><description>materializar y FormID: el
        ''' último RESUELTO (C). Identidad: el primer RE-VISITADO (B), que es lo que agrupa la cola con el
        ''' ciclo.</description></item>
        ''' <item><term>Cadena sana</term><description>el terminal, en las tres caras.</description></item>
        ''' </list>
        '''
        ''' <para>⛔⛔ <b>EL CICLO SON DOS PREGUNTAS DISTINTAS Y SE CONTESTAN DISTINTO.</b> Una cadena
        ''' <c>A→B→A</c> es entrada DEGENERADA y no tiene cita del motor —el CK no crea una—, así que el
        ''' desempate es DE LA APP y va declarado como tal. Pero no es UN desempate: los dos consumidores
        ''' preguntan cosas que no son la misma.</para>
        ''' <list type="bullet">
        ''' <item><b>MATERIALIZAR</b> (<see cref="ArmoEfectivo"/>) pregunta "¿de qué record copio los
        ''' campos?". La respuesta es el <b>ÚLTIMO ESLABÓN RESUELTO</b>. ⛔ <c>B</c> ES una plantilla
        ''' perfectamente utilizable: resolvió, tiene armatures, y es lo que el motor copia en el primer
        ''' salto. Contestar "el hijo" acá <b>borra la prenda del render</b>: un ARMO con <c>TNAM</c>
        ''' normalmente NO trae <c>Models</c>/<c>Armature</c> propios, así que la vista cruda da
        ''' <c>LeerComplementos = 0</c>, el colector no emite candidato y la armadura DESAPARECE, en los
        ''' dos juegos y sin un aviso.</item>
        ''' <item><b>IDENTIFICAR</b> (<see cref="TerminalFormID"/>) pregunta "¿bajo qué FormID agrupo esta
        ''' prenda?". La respuesta es el <b>PRIMER NODO RE-VISITADO</b>, o sea el primer nodo DEL CICLO que
        ''' alcanza el recorrido. Eso agrupa la COLA con el ciclo: en <c>A→B→C→B</c> las tres contestan
        ''' <c>A⇒B</c>, <c>B⇒B</c>, <c>C⇒C</c>, y <c>A</c> —que sí hereda de verdad— no se queda con una
        ''' identidad propia y aislada.</item>
        ''' </list>
        '''
        ''' <para>⛔ <b>NO es lo mismo que el <c>TNAM</c> colgado, y creerlo fue el error.</b> La guarda
        ''' citada (<c>0x14027E808</c>: <c>test rdx,rdx / je 0x14027E963</c>) dispara porque el puntero es
        ''' NULO. En un ciclo <c>rdx</c> no es nulo: <c>B</c> resolvió bien. La cita no cubre este caso, y
        ''' usarla para justificar "gana el hijo" era extenderla a un caso que no describe.</para>
        '''
        ''' <para><b>Qué se preserva y qué no — medido, no afirmado.</b> La IDENTIDAD devuelve en todos los
        ''' casos lo que devolvía <c>OutfitResolver.ResolveTerminalArmorFormID</c>, así que el agrupamiento
        ''' del selector no cambia. La MATERIALIZACIÓN devuelve lo que devolvía <c>CanonHerencia</c> salvo
        ''' en UN caso, y ahí cambia a propósito: <b><c>TNAM</c> colgado a dos o más saltos</b>
        ''' (<c>A→B→X</c>), donde antes devolvía el hijo —o sea la cruda, sin armatures— y ahora devuelve
        ''' <c>B</c>. Es lo que hace el motor y es lo que evita que la prenda se caiga del render.</para>
        ''' <para>⛔ Este bloque llegó a decir dos veces cosas que el código no hacía: primero afirmó que el
        ''' ciclo devolvía el hijo mientras devolvía el último eslabón, y después afirmó que "las dos caras
        ''' devuelven lo que devolvían" sin haber trazado el colgado a dos saltos, que es justo donde no era
        ''' cierto. Las dos veces la afirmación estaba escrita antes que la medición.</para>
        '''
        ''' <para>Medido: <b>0 cadenas cíclicas</b> en los dos órdenes de carga (adv-27, gate
        ''' <c>OutfitDraftSaveGate</c>) — no le pasa a nadie con los plugins del usuario, pero la app se
        ''' distribuye y dos leyes que discrepan no se arreglan con "no está en mi corpus".</para>
        ''' </summary>
        Private Function CaminarCadena(hijo As IArmo, formID As UInteger,
                                       resolver As Func(Of UInteger, IArmo)) _
                                       As (Vista As IArmo, FormID As UInteger, Identidad As UInteger)
            If hijo.TemplateArmor = 0UI Then Return (hijo, formID, formID)
            Dim visitados As New HashSet(Of UInteger) From {formID}
            Dim actual = hijo
            Dim actualFid = formID
            Do
                Dim sig = actual.TemplateArmor
                If sig = 0UI Then Exit Do
                If Not visitados.Add(sig) Then
                    ' ⛔ CICLO: acá —y SÓLO acá— las dos caras se separan.
                    '   materializar ⇒ el ULTIMO ESLABON RESUELTO (`actual`): es una plantilla usable, y
                    '                  contestar el hijo deja la prenda sin armatures y sin dibujar.
                    '   identificar  ⇒ el PRIMER NODO RE-VISITADO (`sig`): agrupa la cola con el ciclo.
                    Return (actual, actualFid, sig)
                End If
                Dim v = resolver(sig)
                If v Is Nothing Then
                    ' ⛔⛔ TNAM COLGADO — Y LA CITA SÓLO CUBRE EL PRIMER SALTO.
                    ' `0x14027E808` (`test rdx,rdx / je 0x14027E963`) dispara para el record cuyo PROPIO
                    ' `templateArmor` es nulo, y entonces ESE record usa sus propios campos. En `A→X` el
                    ' del puntero nulo es A ⇒ gana A, y eso es lo que dice la cita.
                    ' Pero en `A→B→X` el del puntero nulo es B, NO A: el motor igual copia de A a B en el
                    ' primer salto (el `templateArmor` de A no es nulo, la rama corre) y es B el que se
                    ' queda con lo suyo. O sea que el efectivo de A sale de B. Devolver el HIJO acá
                    ' extendía la cita a un caso que no describe, y costaba caro: la IDENTIDAD pasaba de B
                    ' a A, el render enruta POR la identidad (`FaceGenBuilder:2244` resuelve el terminal y
                    ' recién ahí materializa), y A —que como todo ARMO con plantilla no trae armatures
                    ' propios— llegaba con CERO. La prenda desaparecía.
                    ' ⇒ Si ya resolvimos al menos un eslabón, gana el ÚLTIMO RESUELTO, para las dos caras.
                    '   Si el colgado es el PRIMER salto, gana el hijo: ahí sí es el caso citado.
                    If Not ReferenceEquals(actual, hijo) Then Return (actual, actualFid, actualFid)
                    Return (hijo, formID, formID)
                End If
                actual = v
                actualFid = sig
            Loop
            ' Cadena sana: el terminal es la misma respuesta para las dos.
            Return (actual, actualFid, actualFid)
        End Function

        ''' <summary>La cara de MATERIALIZAR: de qué record se copian los campos heredados.
        ''' <para>Cara delgada de <see cref="CaminarCadena"/> para los consumidores de adentro
        ''' (<see cref="ArmoEfectivo"/>, <see cref="MotivoNoClonable"/>), que preguntan
        ''' <c>ReferenceEquals(terminal, hijo)</c> para saber si hay algo que materializar y por eso no
        ''' tienen que repetir ninguno de los casos degenerados.</para>
        ''' <para>⛔ Ante un CICLO devuelve el último eslabón RESUELTO, no el hijo — si devolviera el hijo,
        ''' <c>ArmoEfectivo</c> entregaría la vista CRUDA y la prenda se caería del render por no tener
        ''' armatures propios. Ver el bloque de <see cref="CaminarCadena"/>.</para></summary>
        Private Function TerminalDeLaCadena(hijo As IArmo, formID As UInteger,
                                            resolver As Func(Of UInteger, IArmo)) As IArmo
            Return CaminarCadena(hijo, formID, resolver).Vista
        End Function

        ''' <summary>El FormID del TERMINAL de la cadena de <c>TNAM</c> que arranca en
        ''' <paramref name="formID"/>. <c>0</c> si ese FormID no resuelve a un ARMO.
        '''
        ''' <para>⛔ ES LA PUERTA PÚBLICA DE LA CAMINATA, y existe para que no haya una segunda: la
        ''' identidad de una armadura —la que usan el agrupamiento del selector, la vista previa, el
        ''' footprint y el clon— tiene que salir de UN solo recorrido.
        ''' <c>OutfitResolver.ResolveTerminalArmorFormID</c> delega acá; lo que queda de su lado es CÓMO se
        ''' encuentra un ARMO (el borrador primero, después el archivo), que es su problema y no el de la
        ''' cadena.</para>
        ''' <para><paramref name="resolver"/> tiene que ver los BORRADORES si el llamador los tiene: un
        ''' eslabón que es borrador no está en ningún archivo, y sin él la cadena se corta ahí.</para>
        ''' <para>⛔ Ante un CICLO devuelve el PRIMER NODO RE-VISITADO, que NO es lo mismo que la cara de
        ''' materializar: acá lo que importa es que la cola de la cadena agrupe con el ciclo
        ''' (<c>A→B→C→B</c> ⇒ <c>A</c> y <c>B</c> contestan ambas <c>B</c>). Ver
        ''' <see cref="CaminarCadena"/>.</para>
        ''' </summary>
        Public Function TerminalFormID(formID As UInteger, resolver As Func(Of UInteger, IArmo)) As UInteger
            If formID = 0UI OrElse resolver Is Nothing Then Return 0UI
            Dim hijo = resolver(formID)
            If hijo Is Nothing Then Return 0UI
            Return CaminarCadena(hijo, formID, resolver).Identidad
        End Function

        ''' <summary>Por qué este ARMO NO se puede clonar llevándose la herencia materializada, o
        ''' <c>""</c> si se puede. El texto es para el USUARIO.
        '''
        ''' <para><b>El caso.</b> Un clon nace de la vista MATERIALIZADA (<see cref="ArmoEfectivo"/>): se
        ''' lleva los campos que el motor usaba —armatures, slots, keywords, OBTS, FNAM— y suelta el
        ''' <c>TNAM</c>. Pero los injertos vienen del TERMINAL con sus ramas de unión ya FIJADAS por la
        ''' Form Version del terminal (<c>CanonHerencia.Materializar</c> los estampa), y el record se emite
        ''' con la del HIJO. Si las dos versiones no coinciden, el clon es INGUARDABLE: la guarda de
        ''' <c>WbWriter.EmitBody</c> tira, y tira con razón — <c>DAMA</c> mide 8 bytes por entrada hasta la
        ''' 151 y 12 desde la 152 (<c>WbSchemaGen_FO4.vb:328</c>), así que serían bytes que el propio
        ''' header del record desmiente.</para>
        '''
        ''' <para><b>Por qué NEGARSE y no arreglarlo solo.</b> Un record lleva UNA Form Version y acá hay
        ''' dos fuentes. La ley citable dice a quién le corresponde, pero no desempata dos:
        ''' <c>TwbMainRecord.Assign</c> con <c>wbAssignThis</c> —el "copiá este record adentro mío" de
        ''' xEdit— hace <c>Self.mrStruct.mrsVersion^ := mrStruct.mrsVersion^</c>
        ''' (<c>wbImplementation.pas:9384-9391</c>): la versión sale de la FUENTE, no del archivo destino ni
        ''' del default por juego (los VCS1/VCS2 sí se resetean, las flags se copian). Contra el camino de
        ''' record NUEVO, que sí estampa el default (<c>:10145-10153</c>, la tabla que transcribe
        ''' <see cref="WbContext.VersionPorDefecto"/>). Elegir una de las dos fuentes CAMBIA BYTES del
        ''' clon, y eso lo decide el usuario, no esta función.</para>
        '''
        ''' <para><b>Medido</b> (2026-09-01, <c>OutfitDraftSaveGate</c>, M2): pares (hijo, terminal) con
        ''' Form Version distinta = <b>0 de 2.679</b> en SSE y <b>0 de 0</b> en FO4 (ese orden de carga no
        ''' tiene un solo ARMO con <c>TNAM</c>). O sea que hoy esto no le pasa a nadie: es la puerta
        ''' NOMBRADA para cuando un plugin de terceros la abra, en vez de un
        ''' <c>InvalidOperationException</c> del emisor al apretar Guardar.</para></summary>
        Public Function MotivoNoClonable(formID As UInteger, resolver As Func(Of UInteger, IArmo)) As String
            If resolver Is Nothing Then Return ""
            Dim hijo = resolver(formID)
            If hijo Is Nothing Then Return ""
            Dim caminata = CaminarCadena(hijo, formID, resolver)
            Dim terminal = caminata.Vista
            If ReferenceEquals(terminal, hijo) Then Return ""

            Dim vh = TryCast(hijo, CanonRecordView)
            Dim vt = TryCast(terminal, CanonRecordView)
            If vh Is Nothing OrElse vt Is Nothing OrElse vh.Context Is Nothing OrElse vt.Context Is Nothing Then Return ""
            If vh.Context.FormVersion = vt.Context.FormVersion Then Return ""
            ' ⛔ EL MENSAJE NOMBRA EL TERMINAL REAL, no `hijo.TemplateArmor`. En una cadena de tres
            ' (A→B→C) el conflicto de versión es contra C y el `TNAM` del hijo apunta a B: nombrar B
            ' mandaba al usuario a mirar el record equivocado — y el consejo "cloná la plantilla" le
            ' habría dado el mismo conflicto otra vez.
            Dim fidTerminal = caminata.FormID

            Dim viaTexto = If(fidTerminal = hijo.TemplateArmor, "",
                              $" (through {hijo.TemplateArmor:X8})")
            Return $"This armor cannot be cloned: the fields the game actually uses come from armor " &
                   $"{fidTerminal:X8}{viaTexto}, and the two records were written with different Form Versions " &
                   $"(this one {vh.Context.FormVersion}, {fidTerminal:X8} {vt.Context.FormVersion}). A clone takes " &
                   "those fields, but they are laid out according to THAT record's version while the clone would be " &
                   "saved declaring THIS one — the record's own header would contradict its bytes, and the save would " &
                   "refuse it. Nothing was changed. You can still edit this armor directly, or clone " &
                   $"{fidTerminal:X8} instead."
        End Function

        ''' <summary>Clona el árbol del hijo y reemplaza los miembros heredados por los del terminal.
        ''' <para>⛔ Se QUITAN siempre, aunque el terminal no traiga el miembro: el motor pone
        ''' <c>armorAddons.count = 0</c> <b>incondicionalmente</b> (<c>0x14027E578</c>) y recién después
        ''' mira cuántos tiene el template (<c>0x14027E57B</c>). Terminal sin armatures ⇒ el hijo queda
        ''' con CERO, no conserva los suyos. La «optimización» de no tocar el miembro cuando el terminal
        ''' no lo trae es un defecto, y tiene su caso.</para>
        ''' <para>⛔ El injerto va en la POSICIÓN QUE DECLARA LA DEF, por
        ''' <see cref="WbEdit.InsertarEnPosicionDeclarada"/> — la misma ley que usa
        ''' <c>EnsureSubrecord</c>, no una segunda copia. Appendear al final producía un árbol que se
        ''' emite (<c>WbWriter.EmitBody</c> recorre en orden) y NO se puede volver a leer: el cursor de
        ''' miembros de <see cref="WbReader"/> es monótono y ARMO no es <c>AllowUnordered</c>, así que
        ''' todo lo que cae después de <c>TNAM</c>/<c>APPR</c> —en Skyrim, después de <c>DATA</c>— se va a
        ''' <c>WbPassthroughDef</c>. El clon perdía armatures, slots, keywords y raza al recargar, y
        ''' re-guardarlo TIRA.</para>
        ''' <para>⛔ Y cada injerto se ESTAMPA con la Form Version del TERMINAL. Las ramas de unión que
        ''' dependen de la versión —<c>DAMA</c> mide 8 bytes hasta la 151 y 12 desde la 152
        ''' (<c>WbSchemaGen_FO4.vb:328</c>), <c>MO2T</c>/<c>MO4T</c>— quedaron FIJADAS al parsear el
        ''' terminal (<c>WbValueDefs.vb:1111-1129</c>), y el record se emite con la versión del HIJO. Sin
        ''' el estampado, la guarda de <c>EmitBody</c> no ve nada: el <c>ParsedFormVersion</c> lo pone
        ''' sólo la raíz (<c>WbReader.vb:55</c>), y la raíz acá es la del hijo. Con el estampado, un par
        ''' de versiones distintas no se puede emitir en silencio.</para></summary>
        Private Function Materializar(hijo As IArmo, terminal As IArmo) As IArmo
            Dim vh = TryCast(hijo, CanonRecordView)
            Dim vt = TryCast(terminal, CanonRecordView)
            If vh Is Nothing OrElse vt Is Nothing OrElse vt.Node Is Nothing Then Return Nothing
            Dim clon = TryCast(CanonInterpretacion.Copia(vh), CanonRecordView)
            If clon Is Nothing OrElse clon.Node Is Nothing OrElse clon.Context Is Nothing Then Return Nothing

            Dim juego = clon.Context.Game
            Dim heredados = MiembrosHeredados(juego)
            ' El mismo idiom que `CanonInterpretacion.CopiarSubrecord`: la posición sale de la
            ' declaración del record, y la declaración se pide por (juego, firma).
            Dim def = WbSchema.Get(juego, clon.Context.RecordSignature)
            If def Is Nothing Then Return Nothing

            For i = clon.Node.Children.Count - 1 To 0 Step -1
                Dim h = clon.Node.Children(i)
                If h.Def IsNot Nothing AndAlso heredados.Contains(h.Def.Name) Then clon.Node.QuitarHijoEn(i)
            Next
            For Each h In vt.Node.Children
                If h.Def IsNot Nothing AndAlso heredados.Contains(h.Def.Name) Then
                    Dim injerto = h.Clonar()
                    injerto.ParsedFormVersion = vt.Node.ParsedFormVersion
                    WbEdit.InsertarEnPosicionDeclarada(clon.Node, def, injerto)
                End If
            Next

            InjertarExcepcionesDeHoja(clon, terminal, juego)
            clon.Context.EsVistaEfectiva = True
            Return TryCast(clon, IArmo)
        End Function

        ''' <summary>La única excepción de hoja: <c>DATA\Weight</c> en Skyrim.
        ''' <para>Ese nodo trae <c>Value</c> y <c>Weight</c> juntos y el motor los reparte distinto:
        ''' <c>+0x78</c> (<c>TESWeightForm</c>) se copia en <c>0x14027E85A</c>, y <c>+0x68</c>
        ''' (<c>TESValueForm</c>) <b>no se copia en NINGUNA de las dos ramas</b>. Por eso el nodo no se
        ''' injerta y el peso entra por la hoja.</para>
        ''' <para>En FO4 no hay excepción: <c>InstanceData</c> hereda Value+Weight+Health.</para></summary>
        Private Sub InjertarExcepcionesDeHoja(clon As CanonRecordView, terminal As IArmo, juego As WbGame)
            If juego = WbGame.Fallout4 Then Return
            Dim ce = TryCast(clon, ArmoSSE)
            Dim te = TryCast(terminal, ArmoSSE)
            If ce Is Nothing OrElse te Is Nothing Then Return
            ce.DataWeight = te.DataWeight
        End Sub

    End Module

End Namespace
