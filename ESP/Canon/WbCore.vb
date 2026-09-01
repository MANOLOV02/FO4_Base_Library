' ============================================================================================
' Este archivo transcribe a mano material de las declaraciones de formato de xEdit (ordinales de
' tipo, constantes de formato, y el DSL de declaracion en si), que estan bajo Mozilla Public
' License 2.0, y por lo tanto es una obra derivada de ellas.
'
' This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
' If a copy of the MPL was not distributed with this file, You can obtain one at
' https://mozilla.org/MPL/2.0/
'
' Proyecto original: https://github.com/TES5Edit/TES5Edit  (ElminsterAU y colaboradores)
' Ver THIRD-PARTY-NOTICES.md en la raiz del repositorio.
' ============================================================================================
Imports System.Text

''' <summary>
''' Motor de layout: declara cómo está armado por dentro un record de un plugin, y esa MISMA
''' declaración se usa para leerlo y para volver a escribirlo.
'''
''' <para>El formato de un record es un ÁRBOL de DOS capas:</para>
''' <list type="bullet">
''' <item><b>Capa A — árbol de MIEMBROS</b> (nivel subrecord): un record es una lista ORDENADA de
''' miembros, de cuatro clases posibles: subrecord suelto, struct de subrecords (una corrida de
''' firmas agrupada bajo un nombre), array de subrecords (un miembro que se repite) y unión de
''' miembros (una de varias formas alternativas para el mismo lugar).</item>
''' <item><b>Capa B — árbol de VALORES</b> (dentro de los bytes de UN subrecord): structs,
''' arrays, uniones, enteros, floats, cadenas de ancho fijo o terminadas en cero, cadenas
''' localizables, referencias a otros records, bloques de bytes y campos de cero bytes.</item>
''' </list>
'''
''' <para>El lector recorre esa declaración (ver <see cref="WbReader"/>) y el escritor emite el
''' MISMO árbol (ver <see cref="WbWriter"/>): UNA declaración, dos recorridos. No hay copia
''' verbatim de bytes en ningún punto del motor — si un subrecord no está declarado se REPORTA
''' como pendiente, nunca se copia en silencio.</para>
''' </summary>
Namespace Canon

    ''' <summary>Juego cuya definición de formato manda. El layout de un mismo record NO se comparte
    ''' entre los dos: el ARMO de Fallout 4 y el de Skyrim tienen distinto orden de campos y campos
    ''' que el otro no lleva.</summary>
    Public Enum WbGame
        Fallout4 = 0
        Skyrim = 1
    End Enum

    ''' <summary>Ancho y signo de un entero. Los anchos, en el orden de esta enumeración, son
    ''' 0, 1, 1, 2, 2, 4, 4, 8, 8 y 3 bytes.
    ''' <para>El TAMAÑO sale de acá y de ningún otro lado: el motor no tiene offsets escritos a mano.</para>
    ''' <para><see cref="i0"/> es el ancho CERO, con valor siempre 0. Es un alias de solapamiento:
    ''' sirve para darle nombre a bits que ya consumió el campo anterior, sin gastar bytes propios.
    ''' Es lo que hace el 'General Flags' del <c>BOD2</c> de Skyrim.</para></summary>
    Public Enum WbIntType
        ''' <summary>Ancho 0: no ocupa bytes, valor siempre 0.</summary>
        i0 = 0
        u8 = 1
        s8 = 2
        u16 = 3
        s16 = 4
        u32 = 5
        s32 = 6
        u64 = 7
        s64 = 8
        ''' <summary>3 bytes sin signo.</summary>
        u24 = 9
    End Enum

    ''' <summary>Qué hay en el VALOR de una hoja de texto localizable: el identificador de la tabla
    ''' de idioma, o el texto literal.
    ''' <para>Es la réplica del <c>aElement.Localized</c> de xEdit (<c>TwbLStringDef</c>): un
    ''' tri-estado POR ELEMENTO, no una propiedad del archivo. Hace falta porque el archivo FUENTE y
    ''' el archivo DESTINO pueden no coincidir, y entonces preguntarle al archivo no alcanza: el
    ''' mismo campo puede traer un id (leído de un master localizado) y tener que salir como texto (a
    ''' un plugin que no declara tablas).</para>
    ''' <para><see cref="Desconocido"/> no es un caso a completar: es lo que vale para todo árbol que
    ''' no pasó por acá, y hace que el emisor se comporte EXACTAMENTE como antes de que este estado
    ''' existiera — la localización la decide entonces el archivo fuente.</para></summary>
    Public Enum WbLocalizacion
        Desconocido = 0
        ''' <summary>El valor es texto literal.</summary>
        Texto = 1
        ''' <summary>El valor es un identificador de la tabla de idioma.</summary>
        IdDeTabla = 2
    End Enum

    ''' <summary>Contexto de parseo/emisión. <see cref="FormVersion"/> es la Form Version del HEADER
    ''' del record y es la entrada de los deciders de versión: si la versión del record llega al
    ''' mínimo que pide un campo se elige la rama que lo incluye, y si no, una rama de CERO bytes.
    ''' Por eso el tamaño de un struct es f(versión), no una constante.</summary>
    Public NotInheritable Class WbContext
        Public ReadOnly Property Game As WbGame
        ''' <summary>Form Version del record (RecordHeader.Version). Entrada de los deciders de versión.
        ''' <para>Arranca en <see cref="VersionPorDefecto"/> —131 en FO4, 44 en SSE—, no en 0: ver el
        ''' constructor. El lector la pisa con la del header al parsear.</para></summary>
        Public Property FormVersion As UShort
            Get
                Return _FormVersion
            End Get
            Set(value As UShort)
                _FormVersion = value
            End Set
        End Property
        Private _FormVersion As UShort
        ''' <summary>Flag 0x80 del TES4 del archivo FUENTE. Con localización un lstring es un id u32;
        ''' sin ella es una zstring inline.</summary>
        Public Property Localized As Boolean

        ''' <summary>Flag 0x80 del TES4 del archivo DESTINO. <b>Nothing = el destino es como la
        ''' fuente</b>, que es lo que valía antes de que esto existiera.
        ''' <para>⛔ La localización de un campo la decide el archivo DONDE SE ESCRIBE, no aquel de
        ''' donde se leyó. Es la ley del canónico: el <c>TwbLStringDef</c> de xEdit pregunta por
        ''' <c>aElement._File.IsLocalized</c> —el archivo del elemento— y al asignarle un valor a un
        ''' campo de un archivo sin tablas escribe la zstring y marca el elemento como no localizado
        ''' (<c>wbInterface.pas</c>, <c>FromStringNative</c>).</para>
        ''' <para>Decidirlo por la FUENTE fue un defecto real y medido: un NPC_ de un master
        ''' localizado salía a un plugin propio —que nunca lleva el flag 0x80— con los 4 bytes del
        ''' identificador donde el archivo declara que hay texto, y cualquier lector los leía como una
        ''' zstring de basura.</para></summary>
        Public Property DestinoLocalizado As Boolean?

        ''' <summary>Resuelve el texto de una hoja localizable contra las tablas de idioma. Sólo hace
        ''' falta al EMITIR hacia un destino sin tablas: ahí el identificador no se puede escribir y
        ''' hay que materializar el texto.
        ''' <para>Es un delegado por la misma razón que <see cref="ResolveSignature"/>: la resolución
        ''' vive en <c>CanonResolver</c>, que conoce el archivo de origen y de qué tabla sale el
        ''' campo. Duplicar esa ley acá serían dos copias de la misma pregunta.</para></summary>
        Public Property ResolverTextoLocalizado As Func(Of WbNode, String)

        ''' <summary>Esto NO es un grabado: es una comparación o una medición de tamaño, y cada campo
        ''' se emite <b>tal como lo guarda el nodo</b> — el identificador como identificador y el texto
        ''' como texto.
        ''' <para>Hace falta porque las dos preguntas son distintas. Grabar pregunta "¿qué forma pide el
        ''' archivo destino?", y hay una celda imposible —texto hacia un archivo con tablas— que TIENE
        ''' que tirar. Comparar pregunta "¿los dos árboles dicen lo mismo?", y ahí no hay archivo: la
        ''' respuesta sale del contenido.</para>
        ''' <para>⛔ Las dos salidas fáciles son peores: comparar como "destino con tablas" vuelve a
        ''' tirar en cuanto alguien editó un texto, y comparar como "destino sin tablas" sin resolvedor
        ''' manda todo identificador a la cadena vacía y hace que <b>dos records con nombres distintos
        ''' den IGUALES</b> — corrupción muda en la función cuyo trabajo es decir si algo cambió.</para>
        ''' <para>Un identificador y el literal al que resuelve dan DISTINTO, y está bien: son dos
        ''' estados distintos del record y se van a grabar distinto.</para></summary>
        Public ReadOnly Property Comparando As Boolean
            Get
                Return _comparando
            End Get
        End Property
        Private _comparando As Boolean
        ''' <summary>Encoding traducible del archivo fuente (TES4.SNAM &lt;cp:XXXX&gt;). Nothing =
        ''' usar el global de <c>PluginEncodingSettings</c>.</summary>
        Public Property TranslatableEncoding As Encoding
        ''' <summary>Signature del record que se está parseando (para diagnósticos).</summary>
        Public Property RecordSignature As String = ""

        ''' <summary>Identificador del record. No sale del arbol sino de la cabecera, y hace falta
        ''' porque casi todo lo que la aplicacion hace con un record empieza por identificarlo.</summary>
        Public Property FormID As UInteger
        ''' <summary>Flags del HEADER del record. Los consultan los deciders que dependen de ellos,
        ''' como el que distingue el tipo de un INFO mirando el bit 0x40.</summary>
        Public Property RecordFlags As UInteger
        ''' <summary>EditorID del record. Lo consulta el decider de los GMST, que despacha por su
        ''' PRIMERA LETRA.</summary>
        Public Property EditorId As String = ""

        ''' <summary>Permite EMITIR subrecords declarados como PENDIENTES. Por defecto <b>False</b>,
        ''' y entonces emitir uno TIRA.
        ''' <para>Un pendiente se re-emite copiando sus bytes, y como no está declarado el
        ''' <see cref="WbFormIdWalker"/> no ve NI UNA de las referencias que tiene adentro. Emitirlo
        ''' así deja esas referencias con los índices de master del archivo FUENTE, o sea un override
        ''' apuntando al plugin equivocado, y sin ningún aviso.</para>
        ''' <para>Sólo el arnés de medición lo prende, para poder MEDIR el round-trip. La app no
        ''' puede.</para></summary>
        Public Property AllowPendingSubrecords As Boolean

        ''' <summary>Devuelve la firma del record al que apunta un FormID, o cadena vacia si no se
        ''' puede resolver.
        ''' <para>Hace falta cuando el tipo de un campo depende de a QUE apunta otro campo del mismo
        ''' record. El caso concreto es el bloque de datos extra de un item: el campo que sigue al
        ''' dueño es una referencia a variable global si el dueño es un NPC, un rango numerico si es
        ''' una faccion, y relleno si no hay dueño. Los tres miden 4 bytes, asi que el archivo sale
        ''' igual en cualquier caso — pero solo uno de los tres es una referencia, y remapear el que
        ''' no es corrompe el dato en silencio.</para>
        ''' <para>Sin resolvedor se elige el relleno, que es la opcion que NO remapea nada.</para>
        ''' </summary>
        Public Property ResolveSignature As Func(Of UInteger, String)

        ''' <summary>Hallazgos del parseo del record en curso. El motor NUNCA absorbe una
        ''' inconsistencia en silencio: todo lo que no tesela, no está declarado o no round-trippea
        ''' entra acá y sale en el reporte.</summary>
        Public ReadOnly Property Findings As List(Of WbFinding)
            Get
                Return _findings
            End Get
        End Property
        Private ReadOnly _findings As List(Of WbFinding)

        Public Sub Report(kind As WbFindingKind, path As String, message As String)
            Findings.Add(New WbFinding(kind, RecordSignature, path, message))
        End Sub

        ''' <summary>Cuántas hojas de TEXTO conservaron el crudo porque decode→encode no reproduce la
        ''' fuente. Lo incrementa <c>WbStringDef</c> al conservarlo.
        ''' <para>Es un CONTADOR y no una bandera porque quien lo consulta no pregunta "hubo alguno"
        ''' sino "hubo alguno DENTRO de este subrecord": toma el valor antes de parsearlo y lo compara
        ''' después. Sirve para eso y para nada más — el aviso lo sigue emitiendo el mismo recorrido de
        ''' siempre, con la misma ruta y en el mismo orden; lo único que cambia es que ese recorrido no
        ''' corre cuando no hay nada que encontrar.</para>
        ''' <para>Vive en el contexto, que es POR RECORD. No puede vivir en la definición: el esquema
        ''' se comparte entre hilos.</para></summary>
        Public Property TextosCrudos As Integer

        Public Sub New(game As WbGame)
            _Game = game
            _findings = New List(Of WbFinding)()
            ' ⛔ LA VERSIÓN ARRANCA EN LA DEL JUEGO, NO EN 0. Un `UShort` nace en 0, y con FormVersion 0
            ' TODO decider `wbFromVersion(N, …)` elige la rama de CERO bytes: un record CREADO por la app
            ' sale sin los campos que el juego sí trae.
            ' SYNC: xEdit hace exactamente esto al crear un record —`wbImplementation.pas:10145-10152`:
            '     gmFO4, gmFO4VR               : BasePtr.mrsVersion^ := 131;
            '     gmSSE, gmTES5VR, gmEnderalSE : BasePtr.mrsVersion^ := 44;
            ' y después materializa todos los miembros `Required` (`:10220-10224`).
            ' MEDIDO: un AIDT creado así sale de 20 bytes donde los 4.629 del corpus miden 24 desde la
            ' Form Version 29 (`wbDefinitionsFO4.pas:6214-6231`), y un `RACE.DATA` saldría de 142 bytes
            ' donde los 115 RACE del juego miden 200.
            ' ⚠️ ALCANCE HOY: la vía que crea records (`npcCreateEntries`) tiene 0 llamadores, así que
            ' esto no mueve un byte de lo que la app emite hoy. Es la causa RAÍZ, no el síntoma.
            ' ⚠️ NO pisa la versión de un record PARSEADO: el lector asigna `FormVersion` desde el header
            ' antes de emitir, y `WbWriter.EmitBody` tira si no coincide con `ParsedFormVersion`.
            _FormVersion = VersionPorDefecto(game)
        End Sub

        ''' <summary>Form Version que estampa el CK/xEdit al crear un record nuevo, por juego.
        ''' SYNC: <c>wbImplementation.pas:10145-10152</c>.</summary>
        Public Shared Function VersionPorDefecto(game As WbGame) As UShort
            Select Case game
                Case WbGame.Fallout4 : Return 131US
                Case WbGame.Skyrim : Return 44US
                Case Else : Return 0US
            End Select
        End Function

        ''' <summary>El mismo contexto, pero contestando la pregunta del ESCRITOR: hacia qué archivo
        ''' se emite y con qué se resuelven los textos localizados.
        '''
        ''' <para>Es un objeto aparte y no dos campos que se prenden y se apagan sobre el de lectura,
        ''' porque una vista se puede seguir leyendo y editando después de guardar: dejarle encima el
        ''' estado del guardado es la clase de residuo que después contesta mal una lectura.</para>
        '''
        ''' <para>⛔ Los hallazgos NO se comparten con el contexto de lectura. Se compartían, y estaba
        ''' mal por tres motivos que valen los tres: el emisor corre DOS veces por guardado (la pasada de
        ''' descubrimiento y la de verdad), así que la lista del record crecía el doble en cada guardado y
        ''' no la limpiaba nadie; el guardado corre en otro hilo y <c>List(Of T).Add</c> no es
        ''' thread-safe sobre un objeto que comparten la lista de NPC, el render y el bake; y el
        ''' invariante que los consumidores creen —"Findings es lo que encontró el PARSEO"— dejaba de
        ''' valer. Quien quiera lo del escritor pasa <paramref name="hallazgos"/> y se lo lleva.</para></summary>
        ''' <param name="hallazgos">Dónde deja el emisor lo que encuentre. Nothing = una lista propia que
        ''' se descarta con el contexto.</param>
        ''' <summary>Esta vista NO es lo que dice el archivo: es lo que el MOTOR va a usar, con la
        ''' herencia de <c>ARMO.TNAM</c> ya aplicada (<see cref="CanonHerencia.ArmoEfectivo"/>).
        ''' <para>⛔ Es MIEMBRO de <see cref="ParaEscritura"/> a proposito, para que la hereden
        ''' <see cref="Clonar"/> y <see cref="ParaComparar"/>. Puesta afuera se perderia en la PRIMERA
        ''' copia — y el saver COPIA antes de escribir (<c>NpcOverrideSaver</c>), o sea que la guarda
        ''' desapareceria exactamente en el camino que protege.</para>
        ''' <para>Para que sirve: la efectiva es un <c>CanonView</c> escribible e INDISTINGUIBLE de la
        ''' cruda por el tipo. Si le llega al escritor, se emite el record del HIJO con los <c>MODL</c>
        ''' del TERMINAL, con los FormID plegados contra la lista de masters del hijo. Bytes del ESP, en
        ''' silencio. Que lo impida una guarda, no la disciplina.</para></summary>
        Public Property EsVistaEfectiva As Boolean

        Public Function ParaEscritura(destinoLocalizado As Boolean?,
                                      resolverTexto As Func(Of WbNode, String),
                                      Optional hallazgos As List(Of WbFinding) = Nothing) As WbContext
            Dim w As New WbContext(Game, If(hallazgos, New List(Of WbFinding)())) With {
                .FormVersion = FormVersion,
                .Localized = Localized,
                .TranslatableEncoding = TranslatableEncoding,
                .RecordSignature = RecordSignature,
                .FormID = FormID,
                .RecordFlags = RecordFlags,
                .EditorId = EditorId,
                .AllowPendingSubrecords = AllowPendingSubrecords,
                .ResolveSignature = ResolveSignature,
                .DestinoLocalizado = destinoLocalizado,
                .ResolverTextoLocalizado = resolverTexto,
                .EsVistaEfectiva = EsVistaEfectiva
            }
            Return w
        End Function

        ''' <summary>El mismo contexto, para COMPARAR o MEDIR: sin archivo destino y sin resolvedor.
        ''' Ver <see cref="Comparando"/>.</summary>
        Public Function ParaComparar() As WbContext
            Dim c = ParaEscritura(Nothing, Nothing)
            c._comparando = True
            Return c
        End Function

        ''' <summary>Un contexto PROPIO con el mismo estado, para editar una copia del record sin que
        ''' los cambios se le peguen al original.
        ''' <para>⛔ Hace falta porque las banderas de cabecera —<see cref="RecordFlags"/>,
        ''' <see cref="FormVersion"/>, <see cref="FormID"/>, <see cref="EditorId"/>— viven en el
        ''' CONTEXTO y no en el árbol. Clonar el árbol y reusar el contexto deja la copia y el original
        ''' compartiéndolas: tildar «Has Sculpt Data» en el editor y después CANCELAR dejaba la bandera
        ''' puesta en el record real para toda la sesión, y el guardado la escribía
        ''' (SaveNpcEspWriter lee <c>Context.RecordFlags</c>).</para>
        ''' <para>Delega en <see cref="ParaEscritura"/> a propósito: la lista de qué campos componen un
        ''' contexto está escrita UNA vez. Una segunda copia de esa lista se queda vieja el día que se
        ''' agregue un campo, y nadie se entera.</para>
        ''' <para>⛔ Los <see cref="Findings"/> se COPIAN, no se estrenan ni se comparten. Son lo que
        ''' encontró el PARSEO, y la copia tiene el mismo árbol parseado: estrenar la lista deja a la
        ''' copia diciendo que el record vino limpio —y quien pregunte "¿este record traía algo que el
        ''' esquema no supo ubicar?" recibe siempre que no—. Compartirla es el mismo defecto que este
        ''' método vino a arreglar, una casilla más abajo: dos dueños sobre un objeto mutable.</para></summary>
        Public Function Clonar() As WbContext
            Return ParaEscritura(DestinoLocalizado, ResolverTextoLocalizado,
                                 New List(Of WbFinding)(Findings))
        End Function

        Private Sub New(game As WbGame, hallazgos As List(Of WbFinding))
            _Game = game
            _findings = hallazgos
        End Sub
    End Class

    ''' <summary>Clases de hallazgo del motor. Cada una corresponde a un criterio de aceptación.</summary>
    Public Enum WbFindingKind
        ''' <summary>C1: bytes sin consumir, solapes o faltantes dentro de un subrecord.</summary>
        Tessellation = 0
        ''' <summary>C1: un subrecord del record no lo consumió ningún miembro declarado.</summary>
        Unconsumed = 1
        ''' <summary>C1: subrecord declarado explícitamente como PENDIENTE (todavía sin estructura).</summary>
        Pending = 2
        ''' <summary>C2: el round-trip no dio byte-idéntico.</summary>
        RoundTrip = 3
        ''' <summary>Texto cuyo decode→encode no reproduce la fuente (se conservó el crudo del campo).</summary>
        EncodingFallback = 4
        ''' <summary>La declaración no pudo parsear los bytes (excepción de layout).</summary>
        LayoutError = 5
        ''' <summary>Al emitir hacia un destino sin tablas de idioma, un identificador distinto de
        ''' cero no se pudo resolver a texto. El campo sale VACÍO.</summary>
        TextoLocalizadoSinResolver = 6
    End Enum

    ''' <summary>Un hallazgo, con la RUTA POR NOMBRE del campo — nunca "el byte 47 difiere".</summary>
    Public NotInheritable Class WbFinding
        Public ReadOnly Property Kind As WbFindingKind
        Public ReadOnly Property RecordSignature As String
        Public ReadOnly Property Path As String
        Public ReadOnly Property Message As String

        Public Sub New(kind As WbFindingKind, recSig As String, path As String, message As String)
            _Kind = kind
            _RecordSignature = recSig
            _Path = path
            _Message = message
        End Sub

        Public Overrides Function ToString() As String
            Return $"[{Kind}] {RecordSignature} {Path}: {Message}"
        End Function
    End Class

    ''' <summary>Base de toda definición. El NOMBRE es parte del contrato: es la ruta con la que el
    ''' diagnóstico por campo (C3) y el remapper identifican un valor.
    ''' <para>Un índice de enum NUNCA puede vivir en un nodo cuyo def sea <see cref="WbFormIdDef"/>:
    ''' el remapper recorre EXCLUSIVAMENTE ese tipo (ver <c>WbFormIdWalker</c>), así que un entero
    ''' con un enum asociado no puede entrar aunque mida los mismos 4 bytes.</para></summary>
    Public MustInherit Class WbDef
        Public Property Name As String = ""
        ''' <summary>Def que CONTIENE a esta. Es la cadena por la que se sube para resolver una
        ''' estructura recursiva (una def que se refiere a sí misma). La fijan los contenedores al
        ''' construirse.</summary>
        Public Property DefParent As WbDef
    End Class

    ''' <summary>Cajas compartidas para los valores CHICOS de las hojas.
    ''' <para><see cref="WbNode.Value"/> es `Object`, asi que cada entero y cada flotante que se lee
    ''' de un archivo se empaqueta en un objeto propio de 24 bytes. En un arbol de NPC de Fallout 4
    ''' eso son 1.077.727 cajas — 24,7 MB —, y el 76 % de los enteros, el 30 % de las referencias y
    ''' el 52 % de los flotantes son el MISMO punado de valores (0, 1, -1, indices bajos). Repartir
    ''' una sola caja por valor no cambia nada observable: una caja es inmutable, se compara por
    ''' valor en todo el arbol y no hay un solo `ReferenceEquals` sobre `Value`.</para>
    ''' <para>EL FLOTANTE SE INDEXA POR BITS, NO POR VALOR. `-0.0F = 0.0F` da True en IEEE pero sus
    ''' bytes son distintos, y el escritor emite los bytes: cachear por igualdad numerica convertiria
    ''' un `-0.0` del archivo en `+0.0` y romperia el round-trip. Por eso solo entran patrones de
    ''' bits exactos, y NaN —que ni siquiera es igual a si mismo— queda afuera solo.</para></summary>
    Friend Module WbCajas

        ''' <summary>Hasta que valor se comparte la caja. MEDIDO sobre los 1.872.192 nodos del arbol de NPC
        ''' de Fallout 4 (`Tools/ArbolMemProbe`, que imprime la sensibilidad):
        ''' <code>
        '''   &lt;=  255   Long 75,7 %   UInt  6,9 %   ~6,5 MB
        '''   &lt;= 1023   Long 76,4 %   UInt 29,9 %   ~7,7 MB
        '''   &lt;= 4095   Long 82,0 %   UInt 29,9 %   ~8,1 MB
        ''' </code>
        ''' 1023 se lleva casi todo lo que hay; 4095 agrega 0,4 MB con una tabla cuatro veces mas grande.
        ''' Las dos tablas juntas ocupan ~74 KB.</summary>
        Private Const MAXIMO As Integer = 1023

        ''' <summary>-1 va en el indice 0; de ahi en adelante 0..MAXIMO.</summary>
        Private ReadOnly Enteros As Object() = ConstruirEnteros()
        Private ReadOnly SinSigno As Object() = ConstruirSinSigno()
        Private ReadOnly CeroF As Object = 0.0F
        Private ReadOnly UnoF As Object = 1.0F
        Private ReadOnly MenosUnoF As Object = -1.0F

        Private Function ConstruirEnteros() As Object()
            Dim tabla(MAXIMO + 1) As Object
            tabla(0) = -1L
            For i = 0 To MAXIMO
                tabla(i + 1) = CLng(i)
            Next
            Return tabla
        End Function

        Private Function ConstruirSinSigno() As Object()
            Dim tabla(MAXIMO) As Object
            For i = 0 To MAXIMO
                tabla(i) = CUInt(i)
            Next
            Return tabla
        End Function

        Public Function Caja(v As Long) As Object
            If v = -1L Then Return Enteros(0)
            If v >= 0L AndAlso v <= MAXIMO Then Return Enteros(CInt(v) + 1)
            Return v
        End Function

        Public Function Caja(v As UInteger) As Object
            If v <= CUInt(MAXIMO) Then Return SinSigno(CInt(v))
            Return v
        End Function

        Public Function Caja(v As Single) As Object
            Select Case BitConverter.SingleToInt32Bits(v)
                Case 0 : Return CeroF                       ' +0.0F  (el -0.0F queda afuera: otros bits)
                Case &H3F800000 : Return UnoF               ' 1.0F
                Case &HBF800000 : Return MenosUnoF          ' -1.0F
                Case Else : Return v
            End Select
        End Function

    End Module

    ''' <summary>Nodo del árbol parseado. Presencia = existencia del nodo: "" y AUSENTE son cosas
    ''' distintas por construcción, sin banderas <c>HasXxx</c> paralelas al valor.</summary>
    ''' <remarks>⛔ NO ES SEGURO ESCRIBIRLE DESDE VARIOS HILOS AL MISMO NODO. Leer sí: el parseo arma un
    ''' árbol por hilo y el bake recorre árboles distintos en paralelo, que es lo que hace hoy.
    ''' <para>Escribir no: los hijos y los campos poco usados viven en objetos que se crean al primer uso
    ''' (<c>HijosMutables</c>, <c>ExtrasMutables</c>), y dos hilos que escriban campos DISTINTOS del mismo
    ''' nodo pueden crear cada uno el suyo y perder la escritura del otro. Hoy no hay ningún escritor
    ''' concurrente sobre el mismo nodo; el día que lo haya hay que sincronizar acá.</para></remarks>
    Public NotInheritable Class WbNode
        Public ReadOnly Property Def As WbDef
        Public Property Parent As WbNode
        ''' <summary>Los hijos del nodo, o una lista VACIA compartida si no tiene ninguno.
        ''' <para>La lista real se crea recien en el primer <see cref="AgregarHijo"/>. El 62 % de los
        ''' nodos de un arbol de NPC son hojas, y darle a cada una su propia `List` vacia costaba 32
        ''' bytes por nodo que no se usan nunca.</para>
        ''' <para>EL TIPO DE RETORNO ES DE SOLO LECTURA A PROPOSITO. Con `List(Of WbNode)`, un
        ''' `nodo.Children.Add(x)` sobre un nodo sin hijos escribiria en la instancia COMPARTIDA y
        ''' contaminaria a todos los demas nodos vacios del proceso — en silencio. Con
        ''' `IReadOnlyList` eso no compila, asi que el gate es el compilador y no una convencion.</para></summary>
        Public ReadOnly Property Children As IReadOnlyList(Of WbNode)
            Get
                If _children Is Nothing Then Return SinHijos
                Return _children
            End Get
        End Property
        Private _children As List(Of WbNode)
        Private Shared ReadOnly SinHijos As IReadOnlyList(Of WbNode) = Array.Empty(Of WbNode)()

        ''' <summary>Cuantos hijos tiene, sin materializar nada.</summary>
        Public ReadOnly Property ChildCount As Integer
            Get
                If _children Is Nothing Then Return 0
                Return _children.Count
            End Get
        End Property

        ''' <summary>La lista mutable, creandola si hace falta. Privada: toda mutacion entra por los
        ''' metodos de abajo, que son los que mantienen la invariante padre-hijo.</summary>
        Private ReadOnly Property HijosMutables As List(Of WbNode)
            Get
                If _children Is Nothing Then _children = New List(Of WbNode)()
                Return _children
            End Get
        End Property

        ''' <summary>Cuelga un hijo y le pone el padre. PRIVADA: el alta publica es <see cref="AddChild"/>,
        ''' una sola, para que no haya un camino que deje el nodo sin `Parent` — sin `Parent` se rompen
        ''' <see cref="Path"/> y la resolucion hacia arriba, o sea los contadores, y en silencio.</summary>
        Private Sub AgregarHijo(hijo As WbNode)
            hijo.Parent = Me
            HijosMutables.Add(hijo)
        End Sub

        ''' <summary>Inserta un hijo en una posicion y le pone el padre. Misma invariante que
        ''' <see cref="AddChild"/>.</summary>
        Public Sub InsertarHijo(indice As Integer, hijo As WbNode)
            hijo.Parent = Me
            HijosMutables.Insert(indice, hijo)
        End Sub

        Public Function QuitarHijo(hijo As WbNode) As Boolean
            If _children Is Nothing Then Return False
            Return _children.Remove(hijo)
        End Function

        Public Sub QuitarHijoEn(indice As Integer)
            If _children Is Nothing Then Return
            _children.RemoveAt(indice)
        End Sub

        Public Sub LimpiarHijos()
            If _children Is Nothing Then Return
            _children.Clear()
        End Sub

        Public Function IndiceDeHijo(hijo As WbNode) As Integer
            If _children Is Nothing Then Return -1
            Return _children.IndexOf(hijo)
        End Function

        ''' <summary>Valor tipado de una hoja. Nothing en los contenedores.</summary>
        Public Property Value As Object
        ''' <summary>Firma del subrecord si este nodo ES un subrecord; "" si no.</summary>
        Public Property Signature As String = ""
        ''' <summary>Bytes que el nodo consumió al parsear. Para C1 (teselación) y diagnóstico.</summary>
        Public Property SourceLength As Integer

        ''' <summary>Bytes que el subrecord ocupa en el archivo. Solo lo llevan los nodos de
        ''' subrecord, y se asigna ANTES de parsear su contenido, porque hay campos cuya presencia
        ''' depende de este largo.</summary>
        Public Property DataLength As Integer = -1
        ''' <summary>Para zstrings: cuántos NUL de terminación traía la fuente (0..n). Se re-emiten
        ''' tal cual, así el round-trip no depende de asumir que siempre hay exactamente uno.</summary>
        Public Property TerminatorCount As Integer
            Get
                If _extras Is Nothing Then Return VACIO.TerminatorCount
                Return _extras.TerminatorCount
            End Get
            Set(value As Integer)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.TerminatorCount) Then Return
                ExtrasMutables.TerminatorCount = value
            End Set
        End Property
        ''' <summary>Qué hay en <see cref="Value"/> cuando la hoja es un texto localizable: el
        ''' identificador de la tabla de idioma o el texto literal. Ver <see cref="WbLocalizacion"/>.
        ''' <para><c>Desconocido</c> es el default y significa "preguntale al archivo fuente", que es
        ''' exactamente lo que se hacía antes de que este estado existiera.</para></summary>
        Public Property ValorLocalizado As WbLocalizacion
            Get
                If _extras Is Nothing Then Return VACIO.ValorLocalizado
                Return _extras.ValorLocalizado
            End Get
            Set(value As WbLocalizacion)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ValorLocalizado) Then Return
                ExtrasMutables.ValorLocalizado = value
            End Set
        End Property
        ''' <summary>Rama elegida por una unión (índice en Members). -1 si no es unión.</summary>
        Public Property UnionBranch As Integer
            Get
                If _extras Is Nothing Then Return VACIO.UnionBranch
                Return _extras.UnionBranch
            End Get
            Set(value As Integer)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.UnionBranch) Then Return
                ExtrasMutables.UnionBranch = value
            End Set
        End Property
        ''' <summary>Bytes crudos SÓLO para hojas de texto cuyo decode→encode no reproduce la
        ''' fuente (secuencias que el codepage no representa de ida y vuelta). El campo sigue
        ''' declarado, nombrado y con sus bytes contabilizados; lo único que se conserva es la
        ''' forma exacta del texto. Se CUENTA en el reporte: no es un blob ni una copia por
        ''' firma.</summary>
        Public Property RawOverride As Byte()
            Get
                If _extras Is Nothing Then Return VACIO.RawOverride
                Return _extras.RawOverride
            End Get
            Set(value As Byte())
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.RawOverride) Then Return
                ExtrasMutables.RawOverride = value
            End Set
        End Property
        ''' <summary>Nombre por posición dentro de un array cuyos elementos tienen nombre propio
        ''' declarado ('Textures', 'Addon Nodes', …). Pisa al de la def.</summary>
        Public Property OverrideName As String
            Get
                If _extras Is Nothing Then Return VACIO.OverrideName
                Return _extras.OverrideName
            End Get
            Set(value As String)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.OverrideName) Then Return
                ExtrasMutables.OverrideName = value
            End Set
        End Property
        ''' <summary>Sólo en la RAÍZ: la Form Version con la que se parseó.
        ''' <para>Las ramas de unión que dependen de la versión se ELIGEN en el parseo según ese
        ''' número. Si el consumidor después escribe el header del override con otra versión, los
        ''' bytes de campos como MODT o DAMA quedan en un formato que el header desmiente. El
        ''' escritor verifica que la versión de emisión sea la misma; ver <c>WbWriter.EmitBody</c>.</para>
        ''' <para>-1 = árbol creado desde cero (record nuevo), no hay versión de origen que atar.</para></summary>
        Public Property ParsedFormVersion As Integer
            Get
                If _extras Is Nothing Then Return VACIO.ParsedFormVersion
                Return _extras.ParsedFormVersion
            End Get
            Set(value As Integer)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ParsedFormVersion) Then Return
                ExtrasMutables.ParsedFormVersion = value
            End Set
        End Property
        ''' <summary>La hoja se leyó con MENOS bytes de los que declara su tipo. Se
        ''' re-emite con esa misma cantidad para no cambiar el archivo.</summary>
        Public Property ShortRead As Boolean
            Get
                If _extras Is Nothing Then Return VACIO.ShortRead
                Return _extras.ShortRead
            End Get
            Set(value As Boolean)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ShortRead) Then Return
                ExtrasMutables.ShortRead = value
            End Set
        End Property
        ''' <summary>Cantidad de elementos que el CONTADOR declaraba al parsear.
        ''' <para>Un round-trip NO puede "corregir" nada: hay records cuyo contador ya viene mal en
        ''' el archivo original (hay NPC_ del master de Fallout 4 que declaran <c>COCT = 2</c> y
        ''' traen UN solo <c>CNTO</c>). Recalcularlo siempre cambiaría ese byte. El contador se
        ''' recalcula sólo si la cantidad de elementos CAMBIÓ respecto de lo que decía la fuente —
        ''' o sea, sólo si alguien editó el array.</para>
        ''' <para>-1 = el nodo no vino de un parseo (array creado desde cero).</para></summary>
        Public Property ParsedCount As Integer
            Get
                If _extras Is Nothing Then Return VACIO.ParsedCount
                Return _extras.ParsedCount
            End Get
            Set(value As Integer)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ParsedCount) Then Return
                ExtrasMutables.ParsedCount = value
            End Set
        End Property

        ''' <summary>Sólo en las hojas de REFERENCIA, y sólo cuando la lectura tradujo el valor al
        ''' espacio del orden de carga: qué decía el archivo y a qué se tradujo.
        '''
        ''' <para>La traducción local → orden de carga NO es inyectiva. Un índice de master
        ''' mayor que la cantidad de masters del archivo no existe, y el motor lo pliega al propio
        ''' archivo; dos índices distintos entran así al mismo valor, y la vuelta sólo puede
        ''' devolver la forma canónica. Lo que decía el archivo no está en ninguna otra parte, así
        ''' que lo guarda el nodo.</para>
        '''
        ''' <para>Es la misma política que <see cref="ParsedCount"/>, <see
        ''' cref="TerminatorCount"/> y <see cref="ShortRead"/>: la fuente puede traer una forma que
        ''' no es la canónica y el round-trip no la "corrige"; se vuelve a la canónica sólo si
        ''' alguien EDITÓ el campo.</para>
        '''
        ''' <para>False = el nodo no se leyó de un archivo (record creado desde cero) o se leyó
        ''' sin traducir (inspección de un archivo suelto). En los dos casos el valor ya ES el del
        ''' archivo y no hay nada que restituir.</para></summary>
        Public Property ReferenciaDeArchivo As Boolean
            Get
                If _extras Is Nothing Then Return VACIO.ReferenciaDeArchivo
                Return _extras.ReferenciaDeArchivo
            End Get
            Set(value As Boolean)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ReferenciaDeArchivo) Then Return
                ExtrasMutables.ReferenciaDeArchivo = value
            End Set
        End Property
        ''' <summary>El valor TAL CUAL lo trae el archivo, con el índice de master de ESE archivo.
        ''' Sólo vale si <see cref="ReferenciaDeArchivo"/>.</summary>
        Public Property ReferenciaLocalDeArchivo As UInteger
            Get
                If _extras Is Nothing Then Return VACIO.ReferenciaLocalDeArchivo
                Return _extras.ReferenciaLocalDeArchivo
            End Get
            Set(value As UInteger)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ReferenciaLocalDeArchivo) Then Return
                ExtrasMutables.ReferenciaLocalDeArchivo = value
            End Set
        End Property
        ''' <summary>El valor del orden de carga al que se tradujo ese local. Sirve para saber si
        ''' alguien cambió a dónde apunta la referencia después de leerla: si el valor actual
        ''' del nodo ya no es éste, el local de origen dejó de describirlo y no se
        ''' restituye.</summary>
        Public Property ReferenciaGlobalDeArchivo As UInteger
            Get
                If _extras Is Nothing Then Return VACIO.ReferenciaGlobalDeArchivo
                Return _extras.ReferenciaGlobalDeArchivo
            End Get
            Set(value As UInteger)
                If _extras Is Nothing AndAlso EsElDefault(value, VACIO.ReferenciaGlobalDeArchivo) Then Return
                ExtrasMutables.ReferenciaGlobalDeArchivo = value
            End Set
        End Property


        ''' <summary>Los campos que casi ningun nodo usa, apartados en un objeto que se crea recien
        ''' cuando alguno se aparta de su valor por defecto.
        ''' <para>Un arbol de NPC de Fallout 4 tiene 1.872.192 nodos. Diez campos que solo importan en
        ''' las hojas de REFERENCIA (203.486), en los zstring (10.237), en las uniones y en la RAIZ de
        ''' cada record (4.473) costaban 40 bytes en CADA nodo — 71 MB para llenar de ceros y de -1.
        ''' Aca cuestan una referencia, y el objeto lo pagan los ~200.000 nodos que de verdad lo usan.</para>
        ''' <para>Los defaults se conservan EXACTOS —los -1 no son cero— y por eso cada setter no aloca
        ''' nada si le asignan el default a un nodo que todavia no tiene extras: es la asignacion que hace
        ''' el inicializador de <see cref="Clonar"/> para todos los campos que el original no uso.</para></summary>
        Private NotInheritable Class WbNodeExtras
            Public ValorLocalizado As WbLocalizacion
            Public TerminatorCount As Integer
            Public UnionBranch As Integer = -1
            Public RawOverride As Byte()
            Public OverrideName As String
            Public ParsedFormVersion As Integer = -1
            Public ParsedCount As Integer = -1
            Public ShortRead As Boolean
            Public ReferenciaDeArchivo As Boolean
            Public ReferenciaLocalDeArchivo As UInteger
            Public ReferenciaGlobalDeArchivo As UInteger
        End Class

        ''' <summary>El nodo SIN extras. Es de donde sale el valor por defecto de cada campo, para los
        ''' getters y para el guard de los setters: asi el default de un campo esta escrito UNA sola vez —en
        ''' el inicializador del campo— y no tres. Con tres copias, mover una dejaba un nodo sin extras
        ''' devolviendo -1 y uno con extras devolviendo 0, segun si alguien toco un campo NO RELACIONADO.
        ''' NUNCA se muta: los setters escriben en <see cref="ExtrasMutables"/>.</summary>
        Private Shared ReadOnly VACIO As New WbNodeExtras()

        ''' <summary>Si el valor que entra ES el default del campo. Un setter que reciba el default sobre un
        ''' nodo sin extras no tiene nada que guardar — y asi el inicializador de <see cref="Clonar"/>, que
        ''' asigna los diez campos, no aloca nada para los que el original no uso.</summary>
        Private Shared Function EsElDefault(Of T)(valor As T, porDefecto As T) As Boolean
            Return Equals(valor, porDefecto)
        End Function

        Private _extras As WbNodeExtras

        Private ReadOnly Property ExtrasMutables As WbNodeExtras
            Get
                If _extras Is Nothing Then _extras = New WbNodeExtras()
                Return _extras
            End Get
        End Property
        Public Sub New(d As WbDef)
            _Def = d
        End Sub

        Public ReadOnly Property Name As String
            Get
                Return If(String.IsNullOrEmpty(OverrideName), Def.Name, OverrideName)
            End Get
        End Property

        ''' <summary>Cuelga un hijo al final y devuelve el hijo. Es EL alta: pone el padre y es lo unico
        ''' publico que agrega al final.</summary>
        Public Function AddChild(child As WbNode) As WbNode
            AgregarHijo(child)
            Return child
        End Function

        ''' <summary>Ruta completa por NOMBRE, estilo <c>ARMO\DATA\Weight</c>. Es lo que imprime el
        ''' reporte de diagnóstico en vez de "el byte 47 difiere".</summary>
        Public ReadOnly Property Path As String
            Get
                Dim parts As New List(Of String)
                Dim n = Me
                While n IsNot Nothing
                    Dim label = If(String.IsNullOrEmpty(n.Name), n.Signature, n.Name)
                    If Not String.IsNullOrEmpty(label) Then parts.Add(label)
                    n = n.Parent
                End While
                parts.Reverse()
                Return String.Join("\", parts)
            End Get
        End Property

        ''' <summary>Primer hijo con ese nombre (comparación ordinal), o Nothing.
        ''' <para>Los contenedores ANÓNIMOS son transparentes: hay structs declarados sin nombre —
        ''' el DATA del ARMO de Fallout 4 no lleva nombre y el de Skyrim sí. Sin la transparencia,
        ''' la MISMA ruta lógica cambiaría según el juego.</para></summary>
        Public Function ByName(n As String) As WbNode
            For Each c In Children
                If String.Equals(c.Name, n, StringComparison.Ordinal) Then Return c
            Next
            For Each c In Children
                If String.IsNullOrEmpty(c.Name) Then
                    Dim deep = c.ByName(n)
                    If deep IsNot Nothing Then Return deep
                End If
            Next
            Return Nothing
        End Function

        ''' <summary>Primer hijo con esa firma de subrecord, o Nothing.</summary>
        Public Function BySignature(sig As String) As WbNode
            For Each c In Children
                If String.Equals(c.Signature, sig, StringComparison.Ordinal) Then Return c
            Next
            Return Nothing
        End Function

        ''' <summary>Resuelve una ruta relativa: <c>Counters\[0]</c>, <c>Flags</c>,
        ''' <c>..\Data\Value</c>. Admite nombres de campo, índices entre corchetes y <c>..</c> para
        ''' subir al padre — lo que necesitan las rutas de contador declaradas en el esquema.</summary>

        ''' <summary>Copia profunda del nodo y de todo lo que cuelga de él.
        '''
        ''' <para>Hace falta para editar sobre una copia sin tocar el original: abrir un editor,
        ''' cancelar y que el record quede como estaba. Copiar el árbol es la forma barata de tener
        ''' ese "deshacer" sin inventar un modelo paralelo.</para></summary>
        Public Function Clonar(Optional padre As WbNode = Nothing) As WbNode
            Dim c As New WbNode(Def) With {
                .Parent = padre,
                .Signature = Signature,
                .SourceLength = SourceLength,
                .DataLength = DataLength,
                .TerminatorCount = TerminatorCount,
                .ValorLocalizado = ValorLocalizado,
                .UnionBranch = UnionBranch,
                .OverrideName = OverrideName,
                .ParsedFormVersion = ParsedFormVersion,
                .ShortRead = ShortRead,
                .ParsedCount = ParsedCount,
                .ReferenciaDeArchivo = ReferenciaDeArchivo,
                .ReferenciaLocalDeArchivo = ReferenciaLocalDeArchivo,
                .ReferenciaGlobalDeArchivo = ReferenciaGlobalDeArchivo
            }
            ' El valor es un tipo simple o un arreglo de bytes; el arreglo se copia para que las dos
            ' ramas no compartan el mismo buffer.
            Dim bytes = TryCast(Value, Byte())
            c.Value = If(bytes IsNot Nothing, DirectCast(bytes.Clone(), Byte()), Value)
            If RawOverride IsNot Nothing Then c.RawOverride = DirectCast(RawOverride.Clone(), Byte())
            For Each h In Children
                c.AddChild(h.Clonar(c))
            Next
            Return c
        End Function

        Public Function ByPath(path As String) As WbNode
            If String.IsNullOrEmpty(path) Then Return Nothing
            Dim cur = Me
            For Each rawStep In path.Split("\"c)
                If cur Is Nothing Then Return Nothing
                Dim s = rawStep.Trim()
                If s.Length = 0 Then Continue For
                If s = ".." Then
                    cur = cur.Parent
                ElseIf s.StartsWith("[", StringComparison.Ordinal) AndAlso s.EndsWith("]", StringComparison.Ordinal) Then
                    Dim idx As Integer
                    If Not Integer.TryParse(s.Substring(1, s.Length - 2), idx) Then Return Nothing
                    If idx < 0 OrElse idx >= cur.Children.Count Then Return Nothing
                    cur = cur.Children(idx)
                Else
                    Dim nxt = cur.ByName(s)
                    If nxt Is Nothing Then
                        nxt = cur.BySignature(s)
                        ' El nodo del subrecord es un envoltorio: su VALOR es el hijo único, así
                        ' que una ruta como XCNT\Swimming Count tiene que atravesarlo para llegar
                        ' al valor. Sin esto no resuelven los contadores que apuntan a una firma.
                        If nxt IsNot Nothing AndAlso nxt.Children.Count = 1 Then nxt = nxt.Children(0)
                    End If
                    cur = nxt
                End If
            Next
            Return cur
        End Function

        ''' <summary>Resuelve la ruta de un CAMPO tal como la nombra la estructura del record:
        ''' <c>CNAM\Color/Index</c>, <c>Model\MODL\Model FileName</c>.
        '''
        ''' <para>Se diferencia de <see cref="ByPath"/> en un punto: no atraviesa el envoltorio de
        ''' un subrecord hasta llegar al final de la ruta. Atravesarlo antes deja los segmentos que
        ''' siguen buscando entre los hijos del valor en vez de entre los del subrecord, y la ruta
        ''' no resuelve.</para>
        '''
        ''' <para>Las dos formas coexisten porque responden preguntas distintas: las rutas de
        ''' contador declaradas en la estructura apuntan a la firma y quieren el valor, mientras que
        ''' un campo se nombra bajando por la estructura completa.</para></summary>
        Public Function ByFieldPath(path As String) As WbNode
            If String.IsNullOrEmpty(path) Then Return Nothing
            Return ResolverCampo(Me, Segmentos(path), 0)
        End Function

        ''' <summary>Los tramos de una ruta, ya partidos y sin espacios sobrantes.
        '''
        ''' <para>Memoizados porque las rutas son LITERALES del código generado —una por propiedad,
        ''' un conjunto finito y estable— y esto se llama una vez por lectura de campo. Partir la
        ''' cadena asignaba un arreglo y un string por tramo en cada lectura.</para>
        '''
        ''' <para>El arreglo se devuelve compartido y NADIE lo escribe: <c>ResolverCampo</c> sólo
        ''' lee <c>pasos(idx)</c>. Si alguna vez hiciera falta modificarlo, hay que copiarlo primero.</para>
        '''
        ''' <para>Con el techo puesto el diccionario no puede crecer sin control aunque alguien
        ''' empiece a armar rutas a mano: pasado el tope se parte igual, sólo que sin memo.</para></summary>
        Private Const TopeDeRutasMemoizadas As Integer = 20000
        Private Shared ReadOnly _segmentosPorRuta As New Concurrent.ConcurrentDictionary(Of String, String())(StringComparer.Ordinal)
        ''' <summary>Cuántas rutas hay memoizadas. Es un contador propio y NO <c>_segmentosPorRuta.Count</c>:
        ''' esa propiedad toma TODOS los locks internos del diccionario, y si alguna vez se llegara al
        ''' tope toda lectura de campo pasaría a ser un fallo de caché y por lo tanto un bloqueo
        ''' completo del diccionario. El techo existe para acotar la memoria, no para agregar un
        ''' cuello de botella cuando se alcanza.</summary>
        Private Shared _rutasMemoizadas As Integer

        Private Shared Function Segmentos(path As String) As String()
            Dim ps As String() = Nothing
            If _segmentosPorRuta.TryGetValue(path, ps) Then Return ps
            ps = path.Split("\"c)
            For i = 0 To ps.Length - 1
                ps(i) = ps(i).Trim()
            Next
            If Threading.Volatile.Read(_rutasMemoizadas) < TopeDeRutasMemoizadas AndAlso
               _segmentosPorRuta.TryAdd(path, ps) Then
                Threading.Interlocked.Increment(_rutasMemoizadas)
            End If
            Return ps
        End Function

        ''' <summary>Busca el nodo que satisface la ruta ENTERA, no el primero que coincide con un
        ''' tramo.
        '''
        ''' <para>La diferencia importa cuando dos hermanos comparten la firma y sólo se distinguen
        ''' por el nombre de su valor. Pasa de verdad: una raza declara el esqueleto masculino y el
        ''' femenino con la MISMA firma, uno detrás del otro. Quedarse con el primero que coincide
        ''' hace que la ruta del femenino no resuelva nunca, y leerlo devuelve vacío sin ningún
        ''' aviso — que es exactamente la peor forma de fallar.</para>
        '''
        ''' <para>El orden en que se prueban los candidatos: primero los que coinciden por nombre,
        ''' después los envoltorios sin nombre, y al final los que coinciden por firma. Si un camino
        ''' no llega a destino, se prueba el siguiente.</para></summary>
        Private Shared Function ResolverCampo(cur As WbNode, pasos As String(), idx As Integer) As WbNode
            If cur Is Nothing Then Return Nothing
            If idx >= pasos.Length Then Return Desenvolver(cur)

            ' Los tramos vienen ya recortados de Segmentos().
            Dim s = pasos(idx)
            If s.Length = 0 Then Return ResolverCampo(cur, pasos, idx + 1)
            If s = ".." Then Return ResolverCampo(cur.Parent, pasos, idx + 1)

            If s.StartsWith("[", StringComparison.Ordinal) AndAlso s.EndsWith("]", StringComparison.Ordinal) Then
                Dim i As Integer
                If Not Integer.TryParse(s.Substring(1, s.Length - 2), i) Then Return Nothing
                If i < 0 OrElse i >= cur.Children.Count Then Return Nothing
                Return ResolverCampo(cur.Children(i), pasos, idx + 1)
            End If

            Dim porCandidato = ProbarCandidatos(cur, s, pasos, idx)
            If porCandidato IsNot Nothing Then Return porCandidato

            ' El segmento puede nombrar al nodo en el que YA estamos. Pasa cuando el elemento de un
            ' arreglo es el subrecord mismo: la ruta del campo arranca con su firma, y desde el
            ' elemento esa firma no es un hijo sino uno mismo.
            If String.Equals(cur.Signature, s, StringComparison.Ordinal) OrElse
               (cur.Def IsNot Nothing AndAlso String.Equals(cur.Def.Name, s, StringComparison.Ordinal)) Then
                Return ResolverCampo(cur, pasos, idx + 1)
            End If

            Return Nothing
        End Function

        ''' <summary>Prueba los hijos que pueden ser el tramo <paramref name="s"/>, EN ORDEN DE
        ''' PREFERENCIA, siguiendo con el resto de la ruta desde cada uno; devuelve el primero que
        ''' llega a destino.
        '''
        ''' <para>El orden es: (1) los que coinciden por NOMBRE, (2) atravesando cada envoltorio sin
        ''' nombre —y dentro de él, otra vez las tres fases—, y (3) los que coinciden por FIRMA.
        ''' Un envoltorio sin nombre no es un candidato en sí: se lo atraviesa y se prueba el mismo
        ''' tramo contra lo que tiene adentro.</para>
        '''
        ''' <para>⛔ ESE ORDEN ES CARGA ÚTIL, no un detalle. Hay hermanos que comparten la firma y sólo
        ''' se distinguen por el nombre de su valor —una raza declara el esqueleto masculino y el
        ''' femenino con la MISMA firma, uno detrás del otro—, así que aplanar las fases (todos los
        ''' nombres a toda profundidad primero, después todas las firmas) hace que una ruta ambigua
        ''' resuelva a OTRO hermano — el femenino de RACE leyendo vacío para siempre, sin ningún aviso.</para>
        '''
        ''' <para>⛔ NO convertirlo en un iterador que devuelva los candidatos para que el llamador los
        ''' pruebe: agrega una máquina de estados por nodo visitado, que es el grueso del costo de leer
        ''' un campo. Devolver el resultado da EXACTAMENTE el mismo orden —fase por fase, con la misma
        ''' recursión—.</para></summary>
        Private Shared Function ProbarCandidatos(cur As WbNode, s As String, pasos As String(), idx As Integer) As WbNode
            Dim hijos = cur.Children

            ' (1) por NOMBRE
            For i = 0 To hijos.Count - 1
                Dim c = hijos(i)
                If String.Equals(c.Name, s, StringComparison.Ordinal) Then
                    Dim r = ResolverCampo(c, pasos, idx + 1)
                    If r IsNot Nothing Then Return r
                End If
            Next

            ' (2) atravesando los envoltorios ANÓNIMOS, con las tres fases adentro de cada uno
            For i = 0 To hijos.Count - 1
                Dim c = hijos(i)
                If String.IsNullOrEmpty(c.Name) Then
                    Dim r = ProbarCandidatos(c, s, pasos, idx)
                    If r IsNot Nothing Then Return r
                End If
            Next

            ' (3) por FIRMA
            For i = 0 To hijos.Count - 1
                Dim c = hijos(i)
                If String.Equals(c.Signature, s, StringComparison.Ordinal) AndAlso
                   Not String.Equals(c.Name, s, StringComparison.Ordinal) Then
                    Dim r = ResolverCampo(c, pasos, idx + 1)
                    If r IsNot Nothing Then Return r
                End If
            Next

            Return Nothing
        End Function

        ''' <summary>Si la ruta terminó en el envoltorio de un subrecord, lo que se busca es su
        ''' valor.
        ''' <para>Se exige que sea un subrecord y no cualquier nodo con un hijo: un ARREGLO DE UN
        ''' SOLO ELEMENTO también tiene un hijo, y devolver ese elemento en su lugar deja la lista
        ''' vacía para quien pidió el arreglo.</para></summary>
        Private Shared Function Desenvolver(cur As WbNode) As WbNode
            If cur IsNot Nothing AndAlso TypeOf cur.Def Is WbSubrecordDef AndAlso
               cur.Children.Count = 1 AndAlso cur.Children(0).Children.Count = 0 Then
                Return cur.Children(0)
            End If
            Return cur
        End Function

        ''' <summary>Recorre el árbol en pre-orden: primero el nodo, después cada hijo en orden.
        '''
        ''' <para>Sigue siendo PEREZOSO —hay quien corta en la primera coincidencia
        ''' (<c>WbEdit.FindSubrecord</c>)— pero con UNA pila propia en vez de un iterador por nodo y
        ''' por nivel. La versión recursiva creaba una máquina de estados nueva por cada nodo, y
        ''' además cada elemento que salía de una hoja tenía que atravesar tantos <c>MoveNext</c>
        ''' como profundidad hubiera: el costo era O(nodos × profundidad) en llamadas y O(nodos) en
        ''' asignaciones. Con la pila es O(nodos) y una sola asignación por recorrido.</para>
        '''
        ''' <para>Los hijos se apilan del último al primero para que salgan en su orden: el orden de
        ''' visita es EXACTAMENTE el mismo que el de la versión recursiva, y de ese orden dependen el
        ''' orden de los avisos y el de la traducción de referencias.</para></summary>
        Public Iterator Function Walk() As IEnumerable(Of WbNode)
            Dim pila As New Stack(Of WbNode)()
            pila.Push(Me)
            While pila.Count > 0
                Dim n = pila.Pop()
                Yield n
                Dim hijos = n.Children
                For i = hijos.Count - 1 To 0 Step -1
                    pila.Push(hijos(i))
                Next
            End While
        End Function
    End Class

End Namespace
