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
        ''' está sana. Lo ejecuta el gate, y muere por partida doble: un nombre que no existe deja el
        ''' campo del lado del hijo sin que nada falle, y un miembro sin clasificar miente para siempre
        ''' el día que una resincronización de xEdit agregue uno.
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
        ''' <para><b>Devuelve la CRUDA</b> cuando no hay herencia (<c>TNAM = 0</c>) o cuando el
        ''' <c>TNAM</c> no resuelve (la precondición citada). <b>Tira</b> sólo si la materialización
        ''' falla, que es otra cosa.</para>
        ''' <para>Costo medido: <b>~0,06 ms</b> por ARMO con la cadena incluida (4 corridas del mismo
        ''' binario: 0,0589 · 0,0593 · 0,0657 · 0,0690 ⇒ ~17 % de ruido). Profundidad real del corpus:
        ''' <b>1 en el 100 %</b> de los 2.679, y 0 cadenas cíclicas — por eso cachear la CADENA no compra
        ''' nada y pagaría el borrador en el medio, que ningún guard por FormID puede cazar.</para></summary>
        Public Function ArmoEfectivo(formID As UInteger, resolver As Func(Of UInteger, IArmo)) As IArmo
            If resolver Is Nothing Then Return Nothing
            Dim hijo = resolver(formID)
            If hijo Is Nothing Then Return Nothing
            If hijo.TemplateArmor = 0UI Then Return hijo

            Dim visitados As New HashSet(Of UInteger) From {formID}
            Dim actual = hijo
            Do
                Dim sig = actual.TemplateArmor
                If sig = 0UI Then Exit Do
                If Not visitados.Add(sig) Then Exit Do          ' ciclo: se corta donde está
                Dim v = resolver(sig)
                If v Is Nothing Then Return hijo                ' ⛔ TNAM colgado ⇒ gana el HIJO (0x14027E808)
                actual = v
            Loop
            If ReferenceEquals(actual, hijo) Then Return hijo

            Dim efectiva = Materializar(hijo, actual)
            If efectiva Is Nothing Then
                Throw New InvalidOperationException(
                    $"ArmoEfectivo: la materializacion de {formID:X8} contra su terminal fallo. " &
                    "⛔ Esto NO es el caso del TNAM colgado (ese devuelve el hijo): es el arbol o el " &
                    "contexto en Nothing, y devolver la cruda aca reintroduciria el defecto en silencio.")
            End If
            Return efectiva
        End Function

        ''' <summary>Clona el árbol del hijo y reemplaza los miembros heredados por los del terminal.
        ''' <para>⛔ Se QUITAN siempre, aunque el terminal no traiga el miembro: el motor pone
        ''' <c>armorAddons.count = 0</c> <b>incondicionalmente</b> (<c>0x14027E578</c>) y recién después
        ''' mira cuántos tiene el template (<c>0x14027E57B</c>). Terminal sin armatures ⇒ el hijo queda
        ''' con CERO, no conserva los suyos. La «optimización» de no tocar el miembro cuando el terminal
        ''' no lo trae es un defecto, y tiene su caso.</para></summary>
        Private Function Materializar(hijo As IArmo, terminal As IArmo) As IArmo
            Dim vh = TryCast(hijo, CanonRecordView)
            Dim vt = TryCast(terminal, CanonRecordView)
            If vh Is Nothing OrElse vt Is Nothing OrElse vt.Node Is Nothing Then Return Nothing
            Dim clon = TryCast(CanonInterpretacion.Copia(vh), CanonRecordView)
            If clon Is Nothing OrElse clon.Node Is Nothing OrElse clon.Context Is Nothing Then Return Nothing

            Dim juego = clon.Context.Game
            Dim heredados = MiembrosHeredados(juego)

            For i = clon.Node.Children.Count - 1 To 0 Step -1
                Dim h = clon.Node.Children(i)
                If h.Def IsNot Nothing AndAlso heredados.Contains(h.Def.Name) Then clon.Node.QuitarHijoEn(i)
            Next
            For Each h In vt.Node.Children
                If h.Def IsNot Nothing AndAlso heredados.Contains(h.Def.Name) Then
                    clon.Node.InsertarHijo(clon.Node.Children.Count, h.Clonar())
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
