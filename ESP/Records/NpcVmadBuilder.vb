Imports FO4_Base_Library.Canon.CanonInterpretacion

' ============================================================================
' VMAD: alta y baja de NUESTRO script de Papyrus sobre el arbol del record. Los campos se escriben por
' su nombre y el emisor arma los bytes y remapea las referencias contra la MAST del archivo destino,
' asi que aca las referencias van GLOBALES.
'
' UPSERT, NUNCA APPEND CIEGO, Y NUNCA TOCAR UN SCRIPT AJENO. Dos restricciones, las dos de datos reales:
'   1. 805 de 5118 NPC_ de Skyrim.esm y 382 de 3015 de Fallout4.esm YA traen VMAD con scripts vanilla
'      (workshopnpcscript, WIDeadBodyCleanupScript, masterambushscript). Pisar uno rompe la logica de
'      asentamientos, la limpieza de cadaveres o el scripting de quests de ese actor. No se tocan.
'   2. Guardar el mismo NPC dos veces tiene que converger a UNA sola copia de nuestro script con los valores
'      actuales; un append creceria una entrada por guardado y la VM las correria todas.
' Por eso UpsertScript saca los que llevan nuestro prefijo y agrega el nuestro al final. Se borra por
' PREFIJO y no por nombre exacto, asi tambien limpia lo que dejo una version anterior de la app con otro
' nombre de script.
'
' GAME-AWARE: lo unico que cambia por juego es la Version de la cabecera (Skyrim 5, FO4 6; medido 951/951 y
' 382/382). ObjectFormat es 2 en los dos (1333/1333). Cuando el record ya trae VMAD manda el record y no el
' valor por defecto.
'
' Los valores constantes de mas abajo estan MEDIDOS sobre los 1333 VMAD vanilla, no asumidos: Flags de script
' = 0 (Local) en 1628/1628; Flags de property = 1 (Edited) en 5341/5341; 'Unused' del Object = 0 en 5846/5846;
' 'Alias' = -1 en 5840/5846 (los otros 6 son alias de quest, que no aplican a un script de base form).
' ============================================================================

Public Module NpcVmadBuilder

    ' --- measured constants (see header) ---
    Private Const ScriptFlagLocal As Byte = 0
    Private Const PropertyFlagEdited As Byte = 1
    Private Const ObjectAliasNone As Short = -1S
    Private Const ObjectUnused As UShort = 0US

    ' --- ids de tipo de propiedad que usa el motor ---
    Public Enum VmadPropType As Byte
        ObjectRef = 1
        Str = 2
        Int32 = 3
        Float = 4
        Bool = 5
        ArrayOfObject = 11
        ArrayOfString = 12
        ArrayOfInt32 = 13
        ArrayOfFloat = 14
        ArrayOfBool = 15
    End Enum

    ''' <summary>One Papyrus script property to author. Build these with the From* factories rather
    ''' than by hand — they set the type tag and the matching value field together.
    ''' <para>FormIDs in <see cref="ObjectValue"/> / <see cref="ObjectArray"/> are GLOBAL (resolved)
    ''' El emisor los remapea contra la MAST del archivo destino, igual que cualquier otra referencia
    ''' del record; no hay que pre-codificar un indice de master aca.</para></summary>
    Public Class VmadPropertySpec
        Public Name As String = ""
        Public PropType As VmadPropType

        Public StringValue As String
        Public IntValue As Integer
        Public FloatValue As Single
        Public BoolValue As Boolean
        Public ObjectValue As UInteger

        Public StringArray As List(Of String)
        Public IntArray As List(Of Integer)
        Public FloatArray As List(Of Single)
        Public BoolArray As List(Of Boolean)
        Public ObjectArray As List(Of UInteger)

        Public Shared Function FromString(name As String, value As String) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Str, .StringValue = If(value, "")}
        End Function

        Public Shared Function FromInt(name As String, value As Integer) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Int32, .IntValue = value}
        End Function

        Public Shared Function FromFloat(name As String, value As Single) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Float, .FloatValue = value}
        End Function

        Public Shared Function FromBool(name As String, value As Boolean) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Bool, .BoolValue = value}
        End Function

        ''' <param name="globalFormID">Resolved/global FormID — NOT a source-plugin-encoded one.</param>
        Public Shared Function FromObject(name As String, globalFormID As UInteger) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.ObjectRef, .ObjectValue = globalFormID}
        End Function

        Public Shared Function FromStringArray(name As String, values As IEnumerable(Of String)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfString,
                .StringArray = If(values Is Nothing, New List(Of String), values.Select(Function(s) If(s, "")).ToList())}
        End Function

        Public Shared Function FromIntArray(name As String, values As IEnumerable(Of Integer)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfInt32,
                .IntArray = If(values Is Nothing, New List(Of Integer), values.ToList())}
        End Function

        Public Shared Function FromFloatArray(name As String, values As IEnumerable(Of Single)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfFloat,
                .FloatArray = If(values Is Nothing, New List(Of Single), values.ToList())}
        End Function

        Public Shared Function FromBoolArray(name As String, values As IEnumerable(Of Boolean)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfBool,
                .BoolArray = If(values Is Nothing, New List(Of Boolean), values.ToList())}
        End Function

        Public Shared Function FromObjectArray(name As String, globalFormIDs As IEnumerable(Of UInteger)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfObject,
                .ObjectArray = If(globalFormIDs Is Nothing, New List(Of UInteger), globalFormIDs.ToList())}
        End Function
    End Class

    ''' <summary>One Papyrus script to attach. <see cref="Name"/> must match the compiled .pex file
    ''' name (case-insensitive to the VM, but write it as authored).</summary>
    Public Class VmadScriptSpec
        Public Name As String = ""
        Public Properties As New List(Of VmadPropertySpec)
    End Class

    ''' <summary>Name prefix RESERVED for scripts this app authors. Every script under this prefix is
    ''' considered ours and is replaced wholesale on each write; every script NOT under it belongs to
    ''' vanilla or another mod and is copied through untouched.
    ''' <para>This prefix is a DELETE POWER: anything matching it gets removed on every save. It is
    ''' deliberately long and author-namespaced so it cannot collide with a real mod's script name —
    ''' a collision would mean silently deleting that mod's script from the NPC. VMAD script names are
    ''' u16-length-prefixed strings (65535 bytes max), so length costs nothing; the only real constraint
    ''' is that the name must match the compiled .pex file name on disk.</para></summary>
    Public Const ReservedScriptPrefix As String = "NPCM_Manolov_"

    ''' <summary>Escribe nuestro script en el VMAD del record, IDEMPOTENTEMENTE.
    '''
    ''' <para>Deja el arreglo de scripts como <c>[todo script que NO empieza con
    ''' <paramref name="reservedPrefix"/>, tal cual estaba] + [<paramref name="script"/>]</c>. Asi:
    ''' guardar dos veces no lo duplica, volver a editar reemplaza los valores viejos, lo que dejo una
    ''' version anterior de la app (otro nombre, mismo prefijo) se limpia, y los scripts de vanilla o de
    ''' otro mod no se tocan nunca. Ver la nota de UPSERT en la cabecera.</para>
    '''
    ''' <para><paramref name="script"/> = Nothing SACA el nuestro y deja el resto — el camino de "el
    ''' usuario apago todos los extras de RaceMenu". Si no queda ninguno, se saca el subrecord VMAD
    ''' entero, que es lo correcto: el record no tenia scripts propios.</para>
    '''
    ''' <para>El record puede no traer VMAD: se le crea con la Version del juego (Skyrim 5 / FO4 6) y
    ''' ObjectFormat 2. Si ya lo traia, manda el record y no el valor por defecto.</para></summary>
    Public Sub UpsertScript(npc As Canon.INpc,
                            script As VmadScriptSpec,
                            game As Config_App.Game_Enum,
                            Optional reservedPrefix As String = ReservedScriptPrefix)
        If npc Is Nothing Then Return
        If script IsNot Nothing Then
            If String.IsNullOrWhiteSpace(script.Name) Then
                Throw New ArgumentException("VMAD script spec requires a script name.", NameOf(script))
            End If
            If Not script.Name.StartsWith(reservedPrefix, StringComparison.OrdinalIgnoreCase) Then
                ' Guard against the foot-gun: a name outside the reserved prefix would NOT be found by
                ' the next upsert, so the next save would append a second copy instead of replacing it.
                Throw New ArgumentException(
                    $"Script name '{script.Name}' must start with the reserved prefix '{reservedPrefix}', " &
                    "otherwise repeated saves would duplicate it instead of updating it.", NameOf(script))
            End If
        End If

        Dim nf = TryCast(npc, Canon.NpcFO4)
        Dim ns = TryCast(npc, Canon.NpcSSE)
        If nf Is Nothing AndAlso ns Is Nothing Then Return
        Dim tenia = npc.VirtualMachineAdapterVersionPresente
        If script Is Nothing AndAlso Not tenia Then Return

        ' Sacar los nuestros, de atras para adelante para que los indices no se corran.
        Dim nombres = NombresDeScripts(npc)
        For i = nombres.Count - 1 To 0 Step -1
            If Not nombres(i).StartsWith(reservedPrefix, StringComparison.OrdinalIgnoreCase) Then Continue For
            If nf IsNot Nothing Then
                nf.QuitarScripts(i)
            Else
                ns.QuitarScripts(i)
            End If
        Next

        If script Is Nothing Then
            ' Sin scripts no hay VMAD: un subrecord con la cabecera y cero scripts no es lo que traia
            ' un record que nunca tuvo uno.
            If NombresDeScripts(npc).Count = 0 Then npc.QuitarSubrecord("VMAD")
            Return
        End If

        ' Version y ObjectFormat: manda el record cuando ya los traia.
        If Not tenia Then
            npc.VirtualMachineAdapterVersion = DefaultVmadVersion(game)
            npc.VirtualMachineAdapterObjectFormat = 2S
        End If
        Dim objectFormat = npc.VirtualMachineAdapterObjectFormat
        Dim props = If(script.Properties, New List(Of VmadPropertySpec))
        ' El contexto del arbol: lo necesita EnsureFieldPath para crear el contenedor de un arreglo vacio.
        Dim ctxVista As Canon.WbContext = TryCast(npc, Canon.CanonView)?.Context

        If nf IsNot Nothing Then
            Dim e = TryCast(nf.AgregarScripts(), Canon.NpcFO4_Scripts)
            If e Is Nothing Then Return
            e.ScriptName = script.Name
            e.ScriptFlags = ScriptFlagLocal
            For Each p In props
                If p Is Nothing Then Continue For
                Dim d = e.AgregarProperties()
                If d Is Nothing Then Continue For
                EscribirEscalarDePropiedad(d, p, objectFormat, ctxVista)
                ' El generador le pone al mismo campo un nombre distinto en cada juego, porque lo
                ' desambigua contra los otros niveles del VMAD, que no son iguales en los dos. Por eso
                ' los arreglos se escriben por juego y los escalares una sola vez.
                Select Case p.PropType
                    Case VmadPropType.ArrayOfObject
                        For Each v In If(p.ObjectArray, New List(Of UInteger))
                            Dim x = d.AgregarArrayOfObject2()
                            If x Is Nothing Then Continue For
                            If objectFormat = 1S Then
                                x.ObjectV1FormID = v : x.ObjectV1Alias = ObjectAliasNone
                            Else
                                x.ObjectV2FormID = v : x.ObjectV2Alias = ObjectAliasNone
                            End If
                        Next
                    Case VmadPropType.ArrayOfString
                        For Each v In If(p.StringArray, New List(Of String))
                            Dim x = d.AgregarArrayOfString2()
                            If x IsNot Nothing Then x.Element = If(v, "")
                        Next
                    Case VmadPropType.ArrayOfInt32
                        For Each v In If(p.IntArray, New List(Of Integer))
                            Dim x = d.AgregarArrayOfInt322()
                            If x IsNot Nothing Then x.Element = v
                        Next
                    Case VmadPropType.ArrayOfFloat
                        For Each v In If(p.FloatArray, New List(Of Single))
                            Dim x = d.AgregarArrayOfFloat2()
                            If x IsNot Nothing Then x.Element = v
                        Next
                    Case VmadPropType.ArrayOfBool
                        For Each v In If(p.BoolArray, New List(Of Boolean))
                            Dim x = d.AgregarArrayOfBool2()
                            If x IsNot Nothing Then x.Element = v
                        Next
                End Select
            Next
        Else
            Dim e = TryCast(ns.AgregarScripts(), Canon.NpcSSE_Scripts)
            If e Is Nothing Then Return
            e.ScriptName = script.Name
            e.ScriptFlags = ScriptFlagLocal
            For Each p In props
                If p Is Nothing Then Continue For
                Dim d = e.AgregarProperties()
                If d Is Nothing Then Continue For
                EscribirEscalarDePropiedad(d, p, objectFormat, ctxVista)
                Select Case p.PropType
                    Case VmadPropType.ArrayOfObject
                        For Each v In If(p.ObjectArray, New List(Of UInteger))
                            Dim x = d.AgregarArrayOfObject()
                            If x Is Nothing Then Continue For
                            If objectFormat = 1S Then
                                x.ObjectV1FormID = v : x.ObjectV1Alias = ObjectAliasNone
                            Else
                                x.ObjectV2FormID = v : x.ObjectV2Alias = ObjectAliasNone
                            End If
                        Next
                    Case VmadPropType.ArrayOfString
                        For Each v In If(p.StringArray, New List(Of String))
                            Dim x = d.AgregarArrayOfString()
                            If x IsNot Nothing Then x.Element = If(v, "")
                        Next
                    Case VmadPropType.ArrayOfInt32
                        For Each v In If(p.IntArray, New List(Of Integer))
                            Dim x = d.AgregarArrayOfInt32()
                            If x IsNot Nothing Then x.Element = v
                        Next
                    Case VmadPropType.ArrayOfFloat
                        For Each v In If(p.FloatArray, New List(Of Single))
                            Dim x = d.AgregarArrayOfFloat()
                            If x IsNot Nothing Then x.Element = v
                        Next
                    Case VmadPropType.ArrayOfBool
                        For Each v In If(p.BoolArray, New List(Of Boolean))
                            Dim x = d.AgregarArrayOfBool()
                            If x IsNot Nothing Then x.Element = v
                        Next
                End Select
            Next
        End If
    End Sub

    ''' <summary>Los nombres de los scripts del record, en orden. Lista vacia si no trae VMAD.</summary>
    ''' <summary>⭐ LA REGLA: <b>un solo apply-script de esta app por record; todo lo demás queda</b>.
    ''' Saca del VMAD cada script con el prefijo reservado cuyo nombre NO sea
    ''' <paramref name="elQueSobrevive"/>, y devuelve los que sacó.
    '''
    ''' <para><b>Por qué hace falta.</b> El nombre del apply-script lleva el stem del plugin adentro, y la
    ''' app arma el record de salida a partir del que está GANANDO en ese momento
    ''' (<c>PluginManager.GetRecord</c> — su doc dice que <c>AllRecords</c> guarda sólo el winning
    ''' record). Si otro plugin nuestro ya scripteó ese NPC y sigue instalado, su script viaja adentro y
    ''' <see cref="UpsertScript"/> lo conserva —porque se llama distinto y cae en "los demás"—. Resultado:
    ''' UN solo record, el ganador, con DOS apply-scripts nuestros. El motor instancia los dos, y cada uno
    ''' al cargar barre overlays/morphs/piel y aplica lo suyo: el segundo pisa al primero y el orden entre
    ''' dos <c>OnLoad</c> no está garantizado (el <c>.psc</c> documenta haber visto los dos sobre la MISMA
    ''' referencia en el mismo instante).</para>
    '''
    ''' <para>⛔ <b>Que un record nombre varios scripts es NORMAL</b>, no una anomalía: medido sobre el
    ''' Data de SSE, 248 NPC_ tienen un único record con más de uno (189 con 2, 57 con 3, 2 con 4).
    ''' Por eso la preservación de <see cref="UpsertScript"/> es correcta en general y NO se toca: es lo
    ''' que impide borrarle a Bijin Warmaidens su <c>MQDelphineScript</c>. Lo que este barrido acota es
    ''' sólo el prefijo RESERVADO.</para>
    '''
    ''' <para>⭐ <b>Por qué sacar el viejo no deja nada pegado en el savegame.</b>
    ''' <c>RemovePrevious()</c> del <c>.psc</c> no barre por la ficha de lo que ese script puso: ENUMERA
    ''' las zonas (<c>ClearOverlayGroup</c> + <c>PurgeOverlayGroup</c> sobre cada pool) y las limpia
    ''' todas. Así que el script que sobrevive limpia igual lo que había dejado el que se sacó.</para>
    '''
    ''' <para>⚠️ <b>Alcance real</b>, sobre el disco del usuario al 2026-08-25: <b>0 records</b> con más de
    ''' un apply-script en los dos juegos, y de los 9 <c>.pex</c> con nombre por plugin <b>7 son
    ''' huérfanos</b> (su ESP ya no está). El defecto es prospectivo: muerde el día que dos plugins
    ''' nuestros estén instalados a la vez sobre el mismo NPC.</para>
    '''
    ''' <para>⚠️ Un apply-script de OTRO AUTOR que use esta app también se saca. Es la semántica de un
    ''' override: el record ganador es el nuestro y decide. Conservarlo sería volver a poner dos a
    ''' pelearse, que es justo lo que esto evita.</para></summary>
    Public Function DejarSoloNuestroApplyScript(npc As Canon.INpc,
                                                game As Config_App.Game_Enum,
                                                elQueSobrevive As String) As List(Of String)
        Dim sacados As New List(Of String)
        If npc Is Nothing OrElse String.IsNullOrEmpty(elQueSobrevive) Then Return sacados
        For Each n In NombresDeScripts(npc)
            If String.IsNullOrEmpty(n) Then Continue For
            If Not n.StartsWith(ReservedScriptPrefix, StringComparison.OrdinalIgnoreCase) Then Continue For
            If String.Equals(n, elQueSobrevive, StringComparison.OrdinalIgnoreCase) Then Continue For
            sacados.Add(n)
        Next
        ' El borrado va en una segunda pasada: `UpsertScript` muta la lista que `NombresDeScripts` recorrió.
        For Each n In sacados
            UpsertScript(npc, Nothing, game, n)
        Next
        Return sacados
    End Function

    Private Function NombresDeScripts(npc As Canon.INpc) As List(Of String)
        Dim salida As New List(Of String)
        If npc Is Nothing Then Return salida
        Dim nf = TryCast(npc, Canon.NpcFO4)
        If nf IsNot Nothing Then
            For Each s In nf.Scripts
                salida.Add(If(s.ScriptName, ""))
            Next
            Return salida
        End If
        Dim ns = TryCast(npc, Canon.NpcSSE)
        If ns IsNot Nothing Then
            For Each s In ns.Scripts
                salida.Add(If(s.ScriptName, ""))
            Next
        End If
        Return salida
    End Function

    ''' <summary>Rutas de los CONTENEDORES de arreglo dentro de una property, por tipo. Son las mismas
    ''' en los dos juegos: lo que cambia por juego es el NOMBRE del metodo generado, no la ruta.</summary>
    Private ReadOnly RutaDelArreglo As New Dictionary(Of VmadPropType, String) From {
        {VmadPropType.ArrayOfObject, "Property\Value\Array of Object"},
        {VmadPropType.ArrayOfString, "Property\Value\Array of String"},
        {VmadPropType.ArrayOfInt32, "Property\Value\Array of Int32"},
        {VmadPropType.ArrayOfFloat, "Property\Value\Array of Float"},
        {VmadPropType.ArrayOfBool, "Property\Value\Array of Bool"}
    }

    ''' <summary>Nombre, tipo, banderas y el valor cuando NO es un arreglo; y cuando SI lo es, el
    ''' CONTENEDOR vacio. El valor es una UNION: cual rama lleva dato lo dice el tipo, asi que el tipo se
    ''' escribe PRIMERO y despues la rama que le toca. Escribir otra rama crearia un campo que el motor
    ''' leeria como basura.
    ''' <para>Las referencias van GLOBALES: el remapeo contra la MAST del archivo que se escribe lo hace
    ''' el emisor, igual que con cualquier otra referencia del record.</para>
    ''' <para>El destino entra sin tipo porque el generador da una clase por juego con los mismos
    ''' nombres para estos campos; los ELEMENTOS de los arreglos, que si cambian de nombre, los escribe
    ''' el llamador.</para>
    ''' <para>⛔ EL CONTENEDOR SE CREA AUNQUE EL ARREGLO ESTE VACIO. Un arreglo vacio no es "nada": el
    ''' formato pide igual su u32 de cantidad, en cero. Sin el contenedor el emisor escribia
    ''' nombre+tipo+banderas y NO escribia la cantidad, y el VMAD salia CORRUPTO — MEDIDO 2026-08-22 con
    ''' una property <c>OvlNode</c> de tipo 12 y cero elementos: los bytes pasaban de <c>0C 01</c>
    ''' directo al nombre de la property SIGUIENTE, sin los <c>00 00 00 00</c>, y el lector siguiente
    ''' tomaba <c>08 00 4E 6F</c> como cantidad, o sea 1.867 millones de elementos: colgado y sin
    ''' memoria. El emisor real produce arreglos vacios (un NPC con skin override y sin overlays deja
    ''' <c>OvlNode</c> en cero) y NINGUN VMAD vanilla los trae, asi que el camino no lo ejercia nadie.
    ''' Lo caza el caso K de <c>Tools/VmadBuilderProbe</c>.</para></summary>
    Private Sub EscribirEscalarDePropiedad(destino As Object, p As VmadPropertySpec, objectFormat As Short,
                                           ctx As Canon.WbContext)
        destino.PropertyName = If(p.Name, "")
        destino.PropertyType = CByte(p.PropType)
        destino.PropertyFlags = PropertyFlagEdited
        Dim ruta As String = Nothing
        If ctx IsNot Nothing AndAlso RutaDelArreglo.TryGetValue(p.PropType, ruta) Then
            Canon.WbEdit.EnsureFieldPath(DirectCast(destino.Node, Canon.WbNode), ctx, ruta)
        End If
        Select Case p.PropType
            Case VmadPropType.ObjectRef
                If objectFormat = 1S Then
                    destino.ObjectV1FormID = p.ObjectValue
                    destino.ObjectV1Alias = ObjectAliasNone
                Else
                    destino.ObjectV2FormID = p.ObjectValue
                    destino.ObjectV2Alias = ObjectAliasNone
                End If
            Case VmadPropType.Str
                destino.ValueString = If(p.StringValue, "")
            Case VmadPropType.Int32
                destino.ValueInt32 = p.IntValue
            Case VmadPropType.Float
                destino.ValueFloat = p.FloatValue
            Case VmadPropType.Bool
                destino.ValueBool = p.BoolValue
        End Select
    End Sub

    ''' <summary>Saca nuestro script (todo lo que empieza con <paramref name="reservedPrefix"/>) y deja
    ''' los demas. Si no queda ninguno, saca el subrecord VMAD.</summary>
    Public Sub RemoveAppScripts(npc As Canon.INpc,
                                game As Config_App.Game_Enum,
                                Optional reservedPrefix As String = ReservedScriptPrefix)
        UpsertScript(npc, Nothing, game, reservedPrefix)
    End Sub
    ''' named <paramref name="excludeProperty"/>. Deterministic across runs and machines (plain FNV-1a over the
    ''' canonical text of each name/type/value — no GetHashCode, whose string seed is randomized per process).
    '''
    ''' <para>Purpose: give the emitted script a version number that is a function of THIS NPC's payload. The
    ''' script remembers, per actor instance in the savegame, which version it already applied; when the user
    ''' edits an NPC and re-saves, only THAT NPC's hash changes, so only that actor re-applies on its next load.
    ''' Every other NPC keeps its number and stays quiet. A global constant would instead force every NPC in the
    ''' plugin to re-apply on any edit.</para></summary>
    ''' <param name="logicRevision">Revisión de la LÓGICA del script (no del payload). Se mezcla igual que
    ''' un campo más, así que cambiarla cambia el hash de TODOS los NPC de una. Existe porque el sello por
    ''' sí solo cubre el PAYLOAD: al arreglar el .pex (p.ej. que RemovePrevious barra también los nodos
    ''' Face), los actores cuyo payload no cambió ven el MISMO número, el guard de OnLoad corta en la
    ''' primera línea y el arreglo no se ejecuta NUNCA. Nothing/"" ⇒ no se mezcla.</param>
    Public Function StablePayloadHash(script As VmadScriptSpec, excludeProperty As String,
                                      Optional logicRevision As String = Nothing) As Integer
        ' FNV-1a depends on the 32-bit multiply WRAPPING AROUND. VB.NET has integer overflow checks on by
        ' default, so `h * 16777619UI` on a UInteger throws OverflowException instead of truncating (C gets
        ' the wrap for free; VB does not). Accumulate in 64 bits and mask back to 32 after every step — same
        ' result as the C reference, no exception, and no dependency on the project's overflow-check setting.
        Const Mask32 As ULong = &HFFFFFFFFUL
        Const Prime As ULong = 16777619UL
        Dim h As ULong = 2166136261UL               ' FNV-1a 32-bit offset basis

        Dim mix = Sub(s As String)
                      For Each ch In If(s, "")
                          h = ((h Xor CULng(AscW(ch) And &HFFFF)) * Prime) And Mask32
                      Next
                      h = ((h Xor 10UL) * Prime) And Mask32  ' field separator — "ab"+"c" must not hash like "a"+"bc"
                  End Sub

        If script IsNot Nothing AndAlso script.Properties IsNot Nothing Then
            mix(script.Name)
            ' Va DENTRO del mismo FNV y con el mismo separador que los demás campos: no es un post-proceso,
            ' es un campo más del sello. Así "revisión 2 + payload P" nunca puede colisionar con "payload P".
            If Not String.IsNullOrEmpty(logicRevision) Then mix(logicRevision)
            For Each p In script.Properties
                If p Is Nothing Then Continue For
                If String.Equals(p.Name, excludeProperty, StringComparison.OrdinalIgnoreCase) Then Continue For

                mix(p.Name)
                mix(CInt(p.PropType).ToString(Globalization.CultureInfo.InvariantCulture))

                Dim inv = Globalization.CultureInfo.InvariantCulture
                Select Case p.PropType
                    Case VmadPropType.ObjectRef : mix(p.ObjectValue.ToString("X8", inv))
                    Case VmadPropType.Str : mix(p.StringValue)
                    Case VmadPropType.Int32 : mix(p.IntValue.ToString(inv))
                        ' "R" round-trips the float exactly, so a 1-ULP edit still changes the hash.
                    Case VmadPropType.Float : mix(p.FloatValue.ToString("R", inv))
                    Case VmadPropType.Bool : mix(If(p.BoolValue, "1", "0"))
                    Case VmadPropType.ArrayOfObject
                        For Each v In If(p.ObjectArray, New List(Of UInteger)) : mix(v.ToString("X8", inv)) : Next
                    Case VmadPropType.ArrayOfString
                        For Each v In If(p.StringArray, New List(Of String)) : mix(v) : Next
                    Case VmadPropType.ArrayOfInt32
                        For Each v In If(p.IntArray, New List(Of Integer)) : mix(v.ToString(inv)) : Next
                    Case VmadPropType.ArrayOfFloat
                        For Each v In If(p.FloatArray, New List(Of Single)) : mix(v.ToString("R", inv)) : Next
                    Case VmadPropType.ArrayOfBool
                        For Each v In If(p.BoolArray, New List(Of Boolean)) : mix(If(v, "1", "0")) : Next
                End Select
            Next
        End If

        ' Fold to a positive Int32: the script compares it against an int it stores, and its "never applied"
        ' sentinel is -1, which must never collide with a real hash.
        Return CInt(h And &H7FFFFFFFUL)
    End Function
    ''' <summary>True cuando el record ya lleva un script nuestro. Chequeo barato para el guardado
    ''' ("¿hay que reescribirle el VMAD a este NPC?").</summary>
    Public Function HasAppScript(npc As Canon.INpc,
                                 Optional reservedPrefix As String = ReservedScriptPrefix) As Boolean
        For Each n In NombresDeScripts(npc)
            If n.StartsWith(reservedPrefix, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function
    ''' <summary>VMAD header Version by game. THE one game-aware field in a VMAD payload.</summary>
    Public Function DefaultVmadVersion(game As Config_App.Game_Enum) As Short
        ' Version por defecto de la cabecera VMAD: 5 en Skyrim, 6 en FO4. Coincide con los 1333/1333
        ' VMAD vanilla medidos.
        Return If(game = Config_App.Game_Enum.Skyrim, CShort(5), CShort(6))
    End Function

End Module
