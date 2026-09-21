Imports System.Linq

Namespace Canon

    ''' <summary>La SEDE UNICA de la ley "el nombre de la malla y su bloque de Model Information viajan
    ''' juntos".
    '''
    ''' <para><b>Donde se llama y por que ahi.</b> Desde <c>NpcOverrideSaver</c>, fase 2, una linea
    ''' antes de que el borrador se convierta en entrada. NO desde los editores:</para>
    ''' <list type="number">
    ''' <item>El commit de los editores corre <b>por tecla</b> (<c>TextBoxModl.TextChanged</c> →
    ''' <c>CommitVivo</c>), asi que escribir una ruta de 14 caracteres derivaria 14 veces, abriendo el
    ''' NIF y sus materiales en cada una.</item>
    ''' <item>Los editores son <b>esquivables</b>: clon, clon de borrador, apertura por plantilla y los
    ''' borradores adoptados de otra sesion no pasan por ahi.</item>
    ''' <item>Tampoco va dentro de <c>SaveNpcEspWriter.SerializarRecord</c>, que es la funcion pura
    ''' arbol→bytes: los arneses de round-trip la ejercitan esperando re-emision byte-identica, y
    ''' meterle una mutacion haria que emitir deje de ser idempotente.</item>
    ''' </list>
    '''
    ''' <para><b>Cuanto alcanza.</b> El saver ya salteo antes los borradores limpios
    ''' (<c>If d.IsOverride AndAlso Not d.IsDirty Then Continue For</c>), asi que esto solo mira lo que
    ''' el usuario va a escribir igual. De ese conjunto, se recalcula el bloque que NO describe a su
    ''' malla —lo diga porque el usuario la cambio, porque un mod reemplazo el NIF, o porque el record
    ''' ya venia asi del propio juego—, y no se toca el que ya la describe. Un record que el usuario no
    ''' edito no llega hasta aca y queda intacto.</para></summary>
    Public Module CanonModelInfo

        ''' <summary>Los dos nombres con los que el esquema declara el nombre de archivo del modelo.
        ''' No es una eleccion: <c>HDPT</c> lo llama <c>Model FileName</c> y <c>ARMA</c>/<c>ARMO</c>
        ''' <c>Model Filename</c>, y los dos salen de la declaracion generada.</summary>
        Private ReadOnly NOMBRES_DE_MALLA As String() = {"Model Filename", "Model FileName"}

        ''' <summary>El nombre con el que el esquema declara el bloque, igual en los dos juegos y en
        ''' las siete sedes. Sale de la declaracion generada, no de una eleccion.</summary>
        Private Const NOMBRE_DEL_BLOQUE As String = "Model Information"

        ''' <summary>Los hermanos del grupo que describen al MODELO y que por lo tanto se van con el:
        ''' el indice de remapeo de color, el material swap y las banderas. Se preguntan por NOMBRE de
        ''' campo y no por firma porque las firmas son doce
        ''' (<c>MODC/MO2C/MO3C/MO4C/MO5C/DMDC</c>, <c>MODS/MO2S/…</c>, <c>MODF/MO2F/…</c>) y una lista
        ''' a mano se queda vieja.</summary>
        Private ReadOnly CAMPOS_DEL_MODELO As String() =
            {"Color Remapping Index", "Material Swap", "Flags", "Alternate Textures"}

        '==========================================================================================
        ' La entrada
        '==========================================================================================

        ''' <summary>Deja el bloque de Model Information de cada grupo <c>Model</c> del record en linea
        ''' con la malla que el record nombra HOY.
        '''
        ''' <para><b>LA LEY: si el bloque no describe la malla, se recalcula.</b> No se pregunta si el
        ''' usuario cambio el nombre — se pregunta si lo que el bloque dice es lo que la malla tiene. Un
        ''' bloque viejo es un dato viejo escondido venga de donde venga: de que el usuario cambio la
        ''' malla, de que un mod reemplazo el NIF sin tocar el record, o de que el record nacio asi
        ''' —eso pasa en el propio juego, probado: <c>ARMO 0010FC28 EnchArmorElvenShieldBlock04</c>
        ''' tiene malla <c>ElvenShield.nif</c> y su bloque lista <c>armor\dwarven\dwarvenshield</c>—.</para>
        '''
        ''' <para>Las cuatro clausulas, y son todas las que hay:</para>
        ''' <list type="number">
        ''' <item>El nombre quedo <b>vacio</b> ⇒ se saca el grupo ENTERO: el nombre, el bloque y los
        ''' campos que describen al modelo. Es la ley del motor —<c>TESModel::SaveBuffer</c> sale sin
        ''' escribir nada cuando el nombre esta vacio— y el censo la respalda: sobre 16.376 records de
        ''' los dos juegos, "bloque sin su nombre" existe <b>1</b> vez.</item>
        ''' <item>No se pudo derivar (la malla no esta instalada, no parsea) ⇒ <b>se conserva el bloque
        ''' viejo</b> y se carga un aviso. Nunca se escribe medio bloque.</item>
        ''' <item>Lo derivado dice <b>lo mismo</b> que lo que el bloque ya trae ⇒ no se toca. La
        ''' comparacion es por CONTENIDO y no por orden a proposito: el orden del CK no es derivable, y
        ''' reescribir un bloque correcto solo para cambiarle el orden es ensuciar el diff sin
        ''' arreglar nada.</item>
        ''' <item>Dice <b>otra cosa</b> ⇒ se reemplaza.</item>
        ''' </list>
        ''' <para>⛔ Cuando (4) dispara, el bloque que queda tiene el contenido correcto y puede tener
        ''' otro ORDEN que el que hubiera escrito el CreationKit — ver <see cref="ModelInfoBuilder"/>.
        ''' Es una consecuencia aceptada, y solo la paga el bloque que HABIA que reescribir.</para></summary>
        Public Sub Refrescar(vista As CanonView, plugins As PluginManager, avisos As List(Of String))
            If vista Is Nothing OrElse vista.Node Is Nothing OrElse vista.Context Is Nothing Then Return

            For Each grupo In GruposDeModelo(vista.Node)
                Dim nodoNombre = NodoDelNombre(grupo)
                If nodoNombre Is Nothing Then Continue For
                Dim malla = CStr(If(nodoNombre.Value, "")).Trim()

                ' (1) Sin malla no hay grupo.
                If malla = "" Then
                    SacarGrupo(grupo)
                    Continue For
                End If

                Dim derivado = ModelInfoBuilder.Derivar(
                    FO4UnifiedMaterial_Class.CorrectMeshPath(malla),
                    SustitucionesDelGrupo(grupo, plugins),
                    vista.Context.Game)

                ' (2) No se pudo ⇒ se conserva lo viejo y se AVISA. El aviso tiene que llegar al
                ' usuario: un bloque que describe otra malla es un dato viejo escondido, y tragarlo
                ' seria el defecto de hoy con una capa mas de silencio encima.
                ' ⛔ Y VA ANTES DE CREAR NADA. Al reves —crear primero, derivar despues— un record
                ' cuya malla no esta instalada se quedaba con un bloque VACIO que no tenia: estructura
                ' escrita sin dato. Lo levanto el viaje redondo del arnes en `ARMA 00109491`
                ' (`AlduinMouthFireSkin.nif`, que no esta en el juego).
                If Not derivado.Pudo Then
                    Agregar(avisos, $"{vista.Context.RecordSignature} {vista.Context.FormID:X8}: " &
                                    $"{derivado.Motivo} — kept the Model Information block the record already had.")
                    Continue For
                End If

                ' (3) El record trae malla y no trae el bloque. El bloque NO es un dato que el record
                ' guarde: es la lista de recursos que el CK DERIVA del modelo cada vez que lo graba
                ' (`TESModel::SaveBuffer`). Si hay modelo y se pudo derivar, hay bloque. Crearlo no es
                ' inventar estructura: es escribir el campo que el formato declara para ese grupo.
                Dim nodoInfo = NodoDelBloque(grupo)
                If nodoInfo Is Nothing Then
                    ' ⛔ Y SOLO SI HAY ALGO QUE PONER. Una malla que no aporta ningun recurso no
                    ' tiene bloque que escribir, y crear el subrecord igual seria agregarle al record
                    ' un subrecord de 0 bytes que no tenia: estructura vacia, no derivacion. Lo
                    ' levanto el viaje redondo en `ARMA 0010B2FC` de Skyrim, cuyas dos mallas son
                    ' `FXEmptyExplosionArt.nif` y `FXEmptyObject.nif` — vacias de verdad.
                    If derivado.Texturas.Count = 0 AndAlso derivado.Materiales.Count = 0 AndAlso
                       derivado.AddonNodes.Count = 0 Then Continue For
                    WbEdit.EnsureFieldPath(grupo, vista.Context, NOMBRE_DEL_BLOQUE)
                    nodoInfo = NodoDelBloque(grupo)
                    If nodoInfo IsNot Nothing Then WbEdit.AsegurarRamaVigente(nodoInfo, vista.Context)
                End If
                If nodoInfo Is Nothing Then
                    Agregar(avisos, $"{vista.Context.RecordSignature} {vista.Context.FormID:X8}: '{malla}' " &
                                    "has no Model Information subrecord and the format does not declare one " &
                                    "for that group — could not create it.")
                    Continue For
                End If

                ' (4) Ya dice lo mismo ⇒ no se toca.
                If MismoContenido(nodoInfo, derivado) Then Continue For

                ' (5) Reemplazar.
                If Not Escribir(nodoInfo, derivado, vista.Context) Then
                    Agregar(avisos, $"{vista.Context.RecordSignature} {vista.Context.FormID:X8}: el bloque de " &
                                    $"Model Information block for '{malla}' is in a shape this routine does not write " &
                                    "— kept as it was.")
                End If
            Next
        End Sub

        ''' <summary>¿El bloque que el record trae dice LO MISMO que lo derivado? Se comparan los tres
        ''' conjuntos y el contador de color, <b>sin mirar el orden</b>.
        ''' <para>⛔ Ignorar el orden no es laxitud: el orden del CreationKit <b>no es derivable</b> —el
        ''' mismo NIF sale con dos ordenes distintos en dos records vanilla—, asi que exigirlo haria
        ''' reescribir bloques que ya estan bien solo para ponerles el nuestro. El bloque es una lista
        ''' de recursos a precargar: dos listas con los mismos elementos dicen lo mismo.</para></summary>
        Private Function MismoContenido(nodoInfo As WbNode, d As ModelInfoBuilder.ModelInfoDerivado) As Boolean
            ' ⛔ UN BLOQUE QUE NO TIENE NADA Y UNA DERIVACION QUE NO DA NADA DICEN LO MISMO. El
            ' subrecord de 0 bytes existe en el corpus (101 en Skyrim) y su nodo no tiene rama: sin
            ' esta linea se caia por "no dice lo mismo" y despues `Escribir` no podia tocarlo, asi que
            ' salia un aviso —"esta en una forma que esta rutina no escribe"— para un bloque que ya
            ' estaba exactamente como tiene que estar. El caso es `ARMA 0010B2FC` de Skyrim, cuyas
            ' mallas son `FXEmptyExplosionArt.nif` y `FXEmptyObject.nif`: vacias de verdad.
            If nodoInfo.ChildCount = 0 Then
                Return d.Texturas.Count = 0 AndAlso d.Materiales.Count = 0 AndAlso d.AddonNodes.Count = 0
            End If

            Dim rama = nodoInfo.Children(0)
            Dim texturas As WbNode = Nothing, materiales As WbNode = Nothing
            Dim addons As WbNode = Nothing, contadores As WbNode = Nothing
            For Each hijo In rama.Children
                Select Case hijo.Name
                    Case "Textures" : texturas = hijo
                    Case "Materials" : materiales = hijo
                    Case "Addon Nodes" : addons = hijo
                    Case "Counters" : contadores = hijo
                End Select
            Next
            ' ⛔ La rama que el decisor elige para un subrecord de 0 bytes NO TIENE arreglo de
            ' texturas: es la rama 'Unused' de `WbCommon.ModelInfoValue`. Un bloque asi no lista NADA,
            ' y si la derivacion tampoco da nada los dos dicen lo mismo. Sin esto se caia por
            ' "distinto", `Escribir` no podia tocarlo —no hay arreglo que llenar— y salia un aviso
            ' para un bloque que ya estaba como tiene que estar: `ARMA 0010B2FC` de Skyrim, cuyas
            ' mallas son `FXEmptyExplosionArt.nif` y `FXEmptyObject.nif`, vacias de verdad.
            '   ⛔ Y NO tapa el caso contrario: si la derivacion SI trae algo y el bloque esta en esa
            ' rama, esto devuelve False, se intenta escribir y el aviso sale. Medido: en los dos
            ' juegos no hay ni un caso asi.
            If texturas Is Nothing Then
                Return d.Texturas.Count = 0 AndAlso d.Materiales.Count = 0 AndAlso d.AddonNodes.Count = 0
            End If
            If Not MismoConjunto(texturas, d.Texturas) Then Return False
            ' Un bloque sin arreglo de materiales (Skyrim, y los de Fallout 4 con menos de cuatro
            ' contadores) solo dice lo mismo si lo derivado tampoco trae materiales.
            If materiales Is Nothing Then
                If d.Materiales.Count > 0 Then Return False
            ElseIf Not MismoConjunto(materiales, d.Materiales) Then
                Return False
            End If
            If addons Is Nothing Then
                If d.AddonNodes.Count > 0 Then Return False
            Else
                Dim mios As New HashSet(Of UInteger)(d.AddonNodes)
                Dim suyos As New HashSet(Of UInteger)()
                For Each n In addons.Children
                    Try
                        suyos.Add(Convert.ToUInt32(n.Value))
                    Catch ex As Exception
                        Return False
                    End Try
                Next
                If Not mios.SetEquals(suyos) Then Return False
            End If
            If contadores IsNot Nothing AndAlso contadores.ChildCount >= 3 Then
                Dim actual As Long
                Try
                    actual = Convert.ToInt64(contadores.Children(2).Value)
                Catch ex As Exception
                    Return False
                End Try
                If actual <> CLng(d.Color) Then Return False
            End If
            Return True
        End Function

        Private Function MismoConjunto(arreglo As WbNode, entradas As List(Of ModelInfoBuilder.ModelInfoEntrada)) As Boolean
            If arreglo.ChildCount <> entradas.Count Then Return False
            Dim mios As New HashSet(Of String)(entradas.Select(Function(e) Clave(e.Hash, e.Ext, e.Dir)), StringComparer.Ordinal)
            For Each n In arreglo.Children
                Dim h As UInteger = 0UI, dir As UInteger = 0UI, ext As String = ""
                For Each campo In n.Children
                    Try
                        Select Case campo.Name
                            Case "File Hash" : h = Convert.ToUInt32(campo.Value)
                            Case "Extension" : ext = CStr(If(campo.Value, ""))
                            Case "Folder Hash" : dir = Convert.ToUInt32(campo.Value)
                        End Select
                    Catch ex As Exception
                        Return False
                    End Try
                Next
                If Not mios.Contains(Clave(h, ext, dir)) Then Return False
            Next
            Return True
        End Function

        Private Function Clave(hash As UInteger, ext As String, dir As UInteger) As String
            Return dir.ToString("X8") & "|" & hash.ToString("X8") & "|" & If(ext, "")
        End Function

        '==========================================================================================
        ' Encontrar los grupos
        '==========================================================================================

        ''' <summary>Los grupos <c>Model</c> del record, en orden de recorrido. Un grupo es el nodo que
        ''' tiene como hijo un subrecord cuyo campo se llama <c>Model Filename</c>.
        ''' <para>Se busca por ESTRUCTURA y no por una lista de rutas para que sirva igual en los siete
        ''' lugares que hoy existen —<c>HDPT\Model</c>, los cuatro de <c>ARMA</c>, los dos de
        ''' <c>ARMO</c>— y en los que cuelgan de un ARREGLO, como el <c>Model</c> de cada etapa de
        ''' <c>Destructible</c>, donde una ruta fija no alcanzaria.</para></summary>
        Private Iterator Function GruposDeModelo(raiz As WbNode) As IEnumerable(Of WbNode)
            For Each n In raiz.Walk()
                If NodoDelNombre(n) IsNot Nothing Then Yield n
            Next
        End Function

        ''' <summary>La hoja del nombre de archivo dentro de un grupo, o Nothing si ese nodo no es un
        ''' grupo de modelo.</summary>
        Private Function NodoDelNombre(grupo As WbNode) As WbNode
            If grupo Is Nothing Then Return Nothing
            For Each sr In grupo.Children
                If String.IsNullOrEmpty(sr.Signature) Then Continue For
                For Each hoja In sr.Children
                    If NOMBRES_DE_MALLA.Contains(hoja.Name) AndAlso TypeOf hoja.Def Is WbStringDef Then Return hoja
                Next
            Next
            Return Nothing
        End Function

        ''' <summary>El nodo de la UNION del bloque dentro de un grupo, o Nothing si el record no lo
        ''' trae.
        ''' <para>⛔ Se filtra por la DEF y no por el nombre: <c>WbSubrecordDef</c> toma como nombre el
        ''' del valor que envuelve, asi que el nodo del subrecord <c>MODT</c> tambien se llama
        ''' "Model Information" y filtrar por nombre devuelve el mismo bloque dos veces.</para></summary>
        Private Function NodoDelBloque(grupo As WbNode) As WbNode
            If grupo Is Nothing Then Return Nothing
            For Each sr In grupo.Children
                For Each hijo In sr.Children
                    If TypeOf hijo.Def Is WbUnionDef AndAlso hijo.Name = "Model Information" Then Return hijo
                Next
            Next
            Return Nothing
        End Function

        '==========================================================================================
        ' Las cuatro clausulas
        '==========================================================================================

        ''' <summary>Saca del grupo el nombre, el bloque y todo lo que describe al modelo.</summary>
        Private Sub SacarGrupo(grupo As WbNode)
            For i = grupo.Children.Count - 1 To 0 Step -1
                Dim sr = grupo.Children(i)
                Dim esDelModelo = False
                For Each hoja In sr.Children
                    If NOMBRES_DE_MALLA.Contains(hoja.Name) OrElse
                       CAMPOS_DEL_MODELO.Contains(hoja.Name) OrElse
                       (TypeOf hoja.Def Is WbUnionDef AndAlso hoja.Name = "Model Information") Then
                        esDelModelo = True
                        Exit For
                    End If
                Next
                If esDelModelo Then grupo.QuitarHijoEn(i)
            Next
        End Sub

        ''' <summary>Vuelca lo derivado sobre el nodo del bloque. Devuelve False si la rama de la union
        ''' no es una de las dos que se saben escribir.
        ''' <para>Las ramas son cuatro y solo dos tienen listas: la <b>3</b> (formato nuevo: contadores
        ''' + texturas + addon nodes + materiales) y la <b>2</b> (Skyrim con Form Version 39: solo
        ''' texturas). La <b>0</b> es un bloque opaco y la <b>1</b> es la que el propio esquema llama
        ''' <c>ERROR</c> — a esas no se les toca un byte.</para></summary>
        Private Function Escribir(nodoInfo As WbNode, d As ModelInfoBuilder.ModelInfoDerivado,
                                  ctx As WbContext) As Boolean
            If nodoInfo.ChildCount = 0 Then Return False
            Dim rama = nodoInfo.Children(0)
            Dim texturas As WbNode = Nothing, materiales As WbNode = Nothing
            Dim addons As WbNode = Nothing, contadores As WbNode = Nothing
            For Each hijo In rama.Children
                Select Case hijo.Name
                    Case "Textures" : texturas = hijo
                    Case "Materials" : materiales = hijo
                    Case "Addon Nodes" : addons = hijo
                    Case "Counters" : contadores = hijo
                End Select
            Next
            If texturas Is Nothing OrElse Not (TypeOf texturas.Def Is WbArrayDef) Then Return False

            ' ⛔ UN BLOQUE RECIEN CREADO NACE CON EL ARREGLO DE CONTADORES VACIO, y entonces el
            ' prefijo u32 del arreglo sale 0: los 'Textures' se emiten igual pero `Counters\[0]` no
            ' existe al releer, asi que el bloque vuelve VACIO del re-parseo. Se llena con la cantidad
            ' que DECLARA el esquema para este juego (`ElementNames`, que son las etiquetas
            ' `Textures`/`Addon Nodes`[/`Unknown`/`Materials`] de `WbCommon.ModelInfoValue`), no con un
            ' numero elegido. A un bloque que ya venia no se le toca la cantidad: hay bloques de 2 y de
            ' 3 contadores en Fallout 4 y forzarlos a 4 cambiaria bytes que el CK no cambia.
            If contadores IsNot Nothing AndAlso contadores.ChildCount = 0 Then
                Dim defArr = TryCast(contadores.Def, WbArrayDef)
                Dim cuantos = If(defArr?.ElementNames, Array.Empty(Of String)()).Length
                For k = 1 To cuantos
                    Dim nuevo = WbEdit.AgregarElemento(contadores, ctx)
                    If nuevo IsNot Nothing Then WbEdit.PonerValor(nuevo, 0L)
                Next
            End If


            LlenarEntradas(texturas, d.Texturas, ctx)
            If materiales IsNot Nothing Then LlenarEntradas(materiales, d.Materiales, ctx)
            If addons IsNot Nothing Then
                addons.LimpiarHijos()
                For Each v In d.AddonNodes
                    Dim nuevo = WbEdit.AgregarElemento(addons, ctx)
                    If nuevo IsNot Nothing Then WbEdit.PonerValor(nuevo, CLng(v))
                Next
            End If

            ' Los contadores: el emisor del CK escribe `[texturas, addon nodes, color, materiales]`.
            ' Los de las listas los recalcula `WbWriter.SyncCounters` desde el largo real, asi que aca
            ' solo hace falta el TERCERO — el unico que no sale de ninguna lista. Se escribe solo si el
            ' bloque declara los cuatro: hay bloques con 2 y con 3 (medido: 2.043 en Fallout 4) y en
            ' Skyrim son SIEMPRE 2, y ahi ese contador no existe.
            If contadores IsNot Nothing AndAlso contadores.ChildCount >= 3 Then
                WbEdit.PonerValor(contadores.Children(2), CLng(d.Color))
            End If
            Return True
        End Function

        Private Sub LlenarEntradas(arreglo As WbNode, entradas As List(Of ModelInfoBuilder.ModelInfoEntrada),
                                   ctx As WbContext)
            arreglo.LimpiarHijos()
            For Each e In entradas
                Dim nuevo = WbEdit.AgregarElemento(arreglo, ctx)
                If nuevo Is Nothing Then Continue For
                For Each campo In nuevo.Children
                    Select Case campo.Name
                        Case "File Hash" : WbEdit.PonerValor(campo, CLng(e.Hash))
                        Case "Extension" : WbEdit.PonerValor(campo, e.Ext)
                        Case "Folder Hash" : WbEdit.PonerValor(campo, CLng(e.Dir))
                    End Select
                Next
            Next
        End Sub

        '==========================================================================================
        ' El contexto: que trae el material swap
        '==========================================================================================

        ''' <summary>Las sustituciones del material swap de ESTE grupo, normalizadas.
        ''' <para>La sustitucion IDENTIDAD (<c>X → X</c>) se descarta: el material no cambia, el CK no
        ''' lo reabre y su raiz no entra al bloque. Existe en el corpus — <c>MISC 000AC8BE</c> trae
        ''' <c>…\deadflower.bgsm → …\deadflower.bgsm</c>.</para></summary>
        Private Function SustitucionesDelGrupo(grupo As WbNode, plugins As PluginManager) As Dictionary(Of String, String)
            Dim mapa As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            If plugins Is Nothing Then Return mapa
            Dim fid As UInteger = 0UI
            For Each sr In grupo.Children
                For Each hoja In sr.Children
                    If hoja.Name <> "Material Swap" OrElse hoja.Value Is Nothing Then Continue For
                    Try
                        fid = Convert.ToUInt32(hoja.Value)
                    Catch ex As Exception
                        fid = 0UI
                    End Try
                Next
            Next
            If fid = 0UI Then Return mapa
            Dim rec As PluginRecord = Nothing
            Try
                rec = plugins.GetRecord(fid)
            Catch ex As Exception
                rec = Nothing
            End Try
            If rec Is Nothing OrElse rec.Header.Signature <> "MSWP" Then Return mapa
            Dim mswp = CanonRecords.Mswp(rec, plugins)
            If mswp Is Nothing Then Return mapa
            For Each s In CanonInterpretacion.Sustituciones(mswp)
                Dim orig = FO4UnifiedMaterial_Class.CorrectMaterialPath(If(s.SubstitutionOriginalMaterial, ""))
                Dim repl = FO4UnifiedMaterial_Class.CorrectMaterialPath(If(s.SubstitutionReplacementMaterial, ""))
                If orig = "" OrElse repl = "" Then Continue For
                If String.Equals(orig, repl, StringComparison.OrdinalIgnoreCase) Then Continue For
                If Not mapa.ContainsKey(orig) Then mapa(orig) = repl
            Next
            Return mapa
        End Function

        Private Sub Agregar(avisos As List(Of String), texto As String)
            If avisos Is Nothing Then Return
            avisos.Add(texto)
        End Sub

    End Module

End Namespace
