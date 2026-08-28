Option Strict On
Option Explicit On

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq

' ==============================================================================================
' LAYOUT CANONICO DE HAVOK  —  la ley de los offsets, en UN SOLO LUGAR.
' ----------------------------------------------------------------------------------------------
' Las tablas (Generated/HavokLayout_FO4.vb, Generated/HavokLayout_SSE.vb) salen de la reflexion
' hkClass/hkClassMember que el propio ejecutable del juego embebe. NO hay SDK de Havok de por medio.
' Regenerar con:  python Tools/HavokLayoutGen/gen.py
'
' ⛔ POR QUE ESTO NO MIRA Config_App.Current.Game
'   Porque el default de esa propiedad es Skyrim y porque el juego "activo" en la UI no dice nada
'   del archivo que se esta parseando: un .hkx de FO4 se puede abrir con el config en Skyrim.
'   El discriminador correcto es el que el FORMATO DECLARA en su propio header, que la libreria ya
'   deriva desde 2 campos (FileVersion + PointerSize) en HkxPackfileParser.ReadHeader:
'       HkxPackfileFormat_Enum.Fallout64  (FileVersion=11, PointerSize=8) -> tabla FO4
'       HkxPackfileFormat_Enum.Skyrim64   (FileVersion=8,  PointerSize=8) -> tabla SSE
'       HkxPackfileFormat_Enum.Skyrim32   (FileVersion=8,  PointerSize=4) -> NO soportado (ver abajo)
'
' ⛔ POR QUE Skyrim32 NO ESTA SOPORTADO
'   La reflexion del exe describe el layout x64 en memoria. Un packfile de 32 bits tiene OTRO
'   layout (puntero 4, hkArray 12 en vez de 16, alineaciones distintas) que la tabla NO describe.
'   Derivarlo re-maquetando la clase seria una SEGUNDA fuente de verdad, y una equivocada en
'   silencio es peor que un "no se". Por eso Supported=False y quien llame se entera.
'
' Los offsets son POR JUEGO y POR BUILD. Si Bethesda actualiza un .exe hay que regenerar y correr
' Tools/HavokLayoutGate. Las LEYES del motor no cambian con una actualizacion; los offsets si.
' ==============================================================================================

Namespace Havok.Canon

    ''' <summary>Un miembro declarado por la reflexion de Havok.</summary>
    Public NotInheritable Class HavokMember
        Public ReadOnly Property Name As String
        ''' <summary>Offset en BYTES, absoluto dentro de la clase que lo declara.</summary>
        Public ReadOnly Property Offset As Integer
        ''' <summary>Tipo Havok: real, uint16, vector4, array, pointer, struct, stringptr, ...</summary>
        Public ReadOnly Property SubTypeName As String
        Public ReadOnly Property TypeName As String
        ''' <summary>Cantidad de elementos si es un array C fijo; 0 si no lo es.</summary>
        Public ReadOnly Property CArraySize As Integer
        ''' <summary>Clase del struct/puntero apuntado. "" si no aplica.</summary>
        Public ReadOnly Property StructClassName As String

        ''' <summary>
        ''' ⛔ LOS `flags` QUE DECLARA LA REFLEXION (`hkClassMember` +0x1C).
        ''' <para>La propia tabla declara sus nombres en `hkClassMember|FlagValues`:
        ''' `FLAGS_NONE=0, ALIGN_8=128, ALIGN_16=256, NOT_OWNED=512, SERIALIZE_IGNORED=1024,
        ''' ALIGN_32=2048, ALIGN_REAL=256`.</para>
        ''' <para>El extractor los leia desde siempre y el emisor los tiraba, asi que el arbol no
        ''' podia saber que un miembro esta marcado. Medido al ponerlos: 482 miembros en FO4 y 397
        ''' en SSE traen `SERIALIZE_IGNORED`.</para>
        ''' </summary>
        Public ReadOnly Property Flags As Integer

        ''' <summary>
        ''' ⛔⛔ EL MOTOR DICE QUE ESTE MIEMBRO NO SE SERIALIZA (`SERIALIZE_IGNORED = 1024`).
        ''' <para>Los 47 arreglos de FO4 y 28 de SSE cuyo SUBTIPO la reflexion no declara tienen
        ''' TODOS este flag —`hkbBehaviorGraph.uniqueIdPool`, `hkbBindable.cachedBindables`...—.
        ''' No es que no se sepa que hay adentro: es que no hay nada, porque el serializador no lo
        ''' escribe. Contarlos como "datos que la app no lee" era contar un hueco que no existe.</para>
        ''' </summary>
        Public ReadOnly Property NoSeSerializa As Boolean
            Get
                Return BitSerializeIgnored > 0 AndAlso (Flags And BitSerializeIgnored) <> 0
            End Get
        End Property

        ''' <summary>El bit que ESTE juego declara para `SERIALIZE_IGNORED`, o 0 si no lo declara.
        ''' <para>⛔⛔ Salia de una constante escrita a mano (1024) al lado de la alineacion, que SI
        ''' sale del enum emitido. Y los dos enums NO son iguales: FO4 declara `ALIGN_32` y `ALIGN_REAL`
        ''' y SSE no. Si una actualizacion mueve el bit, la alineacion se actualiza sola y esto seguiria
        ''' contestando por 1024.</para></summary>
        Friend ReadOnly Property BitSerializeIgnored As Integer

        ''' <summary>
        ''' ⛔⛔ LOS BYTES QUE EL MIEMBRO OCUPA EN SU PADRE (elemento x cantidad), 0 si no se declara.
        ''' <para>La tabla decia DONDE empieza cada miembro y no hasta donde llega, asi que ningun
        ''' consumidor podia decir que byte de un bloque cubre un miembro declarado y cual no cubre
        ''' ninguno. El censo por byte lo suplia con el offset del miembro SIGUIENTE, que le adjudica
        ''' a cada miembro el relleno que le sigue.</para>
        ''' <para>Lo emite el generador con `tipos.tamano`, que transcribe
        ''' `hkStructureLayout::getMemberSize` (FO4 0x14142F160) rama por rama y consume la tabla de
        ''' tipos que el motor trae adentro. NO es el `size` crudo de esa tabla: `simplearray` da 12 y
        ''' la tabla dice 16; `enum` y `flags` dan el tamano del SUBTIPO. Medido: los 4.016 miembros de
        ''' FO4 y los 2.636 de SSE lo traen, ninguno queda en cero.</para>
        ''' </summary>
        Public ReadOnly Property Ancho As Integer

        ''' <summary>
        ''' ⛔⛔ EL ANCHO DE UN ELEMENTO, QUE NO ES EL DEL MIEMBRO.
        ''' <para>Un `hkArray` ocupa 16 bytes en su padre —la cabecera— y sus elementos viven
        ''' AFUERA, con el ancho de su subtipo. Quien quiera acotar la region de datos del arreglo
        ''' necesita este numero; sin el tendria que volver a derivar la ley del ancho por su
        ''' cuenta, que es exactamente la copia que se saco del gate.</para>
        ''' <para>Medido: de los 566 arreglos de FO4, 47 NO traen ancho de elemento, y de los 379
        ''' de SSE, 28. Y son exactamente los que el motor marca `SERIALIZE_IGNORED`: 47 de 47 y 28
        ''' de 28, sin una excepcion: si la reflexion no declara el subtipo, el serializador no
        ''' escribe nada, asi que ahi no hay bytes que caminar. El gate exige ESA direccion.</para>
        ''' <para>⛔ La vuelta NO vale: 12 arreglos de FO4 y 32 de SSE declaran subtipo Y ademas
        ''' traen `SERIALIZE_IGNORED`. El flag no implica que falte el subtipo.</para>
        ''' </summary>
        Public ReadOnly Property AnchoDeElemento As Integer

        ''' <summary>
        ''' ⛔⛔ LO QUE OCUPA UN ITEM DEL MIEMBRO, para caminar un arreglo C (`foo[4]`).
        ''' <para>No confundir con <see cref="AnchoDeElemento"/>: para un `hkArray` este es el ancho
        ''' de la CABECERA (16) y aquel el del subtipo. Uno camina el arreglo C dentro del padre; el
        ''' otro, la region de datos que la cabecera apunta. Confundirlos daba paso 8 donde van 16.</para>
        ''' <para>Sale de `Ancho`, que ya es `item x cantidad`: no hay una tercera ley.</para>
        ''' </summary>
        Public ReadOnly Property AnchoDeItem As Integer
            Get
                Return Ancho \ Math.Max(1, CArraySize)
            End Get
        End Property

        ''' <summary>
        ''' ⛔⛔⛔ A QUE SE ALINEA ESTE MIEMBRO, CON LA LEY DEL MOTOR.
        ''' <para>Es `max(alineacion del TIPO, alineacion de los FLAGS)` — ver `AlineacionDe`, que
        ''' transcribe `hkStructureLayout::getMemberAlignment` (FO4 0x14142F360, SSE 0x140B2F580).
        ''' La del tipo sale de la tabla que el motor trae adentro; la de los flags, del enum
        ''' `hkClassMember|FlagValues`, por igualdad y no por el bit mas alto prendido.</para>
        ''' <para>0 = la reflexion no alcanza para determinarla: `struct` (haria falta la clase
        ''' entera) e `inplacearray` (el motor deja el registro en -1).</para>
        ''' </summary>
        Public ReadOnly Property Alineacion As Integer

        Friend Sub New(name As String, offset As Integer, typeName As String,
                       subTypeName As String, cArraySize As Integer, structClassName As String,
                       Optional flags As Integer = 0, Optional ancho As Integer = 0,
                       Optional anchoDeElemento As Integer = 0, Optional alineacion As Integer = 0,
                       Optional bitSerializeIgnored As Integer = 0)
            _Name = name
            _Offset = offset
            _TypeName = typeName
            _SubTypeName = subTypeName
            _CArraySize = cArraySize
            _StructClassName = structClassName
            _Flags = flags
            _Ancho = ancho
            _AnchoDeElemento = anchoDeElemento
            _Alineacion = alineacion
            _BitSerializeIgnored = bitSerializeIgnored
        End Sub

        Friend Function Shifted(delta As Integer, path As String) As HavokMember
            Return New HavokMember(path, Offset + delta, TypeName, SubTypeName, CArraySize, StructClassName, Flags, Ancho, AnchoDeElemento, Alineacion, BitSerializeIgnored)
        End Function

        Public Overrides Function ToString() As String
            Return $"+0x{Offset:X3} {Name} {TypeName}"
        End Function
    End Class

    ''' <summary>Una clase Havok tal como la declara la reflexion del exe.</summary>
    Public NotInheritable Class HavokClass
        Public ReadOnly Property Name As String
        ''' <summary>Nombre de la clase padre, o "" si no hereda.</summary>
        Public ReadOnly Property ParentName As String
        ''' <summary>objectSize declarado. 0 cuando la reflexion no lo trae (structs embebidos).</summary>
        Public ReadOnly Property Size As Integer
        ''' <summary>describedVersion. -1 si la reflexion no lo trae.</summary>
        Public ReadOnly Property Version As Integer
        ''' <summary>Miembros DECLARADOS por esta clase (sin los del padre).</summary>
        Public ReadOnly Property Declared As IReadOnlyList(Of HavokMember)

        Friend Sub New(name As String, parentName As String, size As Integer,
                       version As Integer, declared As IReadOnlyList(Of HavokMember))
            _Name = name
            _ParentName = parentName
            _Size = size
            _Version = version
            _Declared = declared
        End Sub

        Public Overrides Function ToString() As String
            Return $"{Name} size=0x{Size:X} ver={Version} n={Declared.Count}"
        End Function
    End Class

    ''' <summary>
    ''' Tabla de layout de un juego. Se construye una sola vez (perezosa) desde el string table
    ''' generado, y memoiza el aplanado por clase.
    ''' </summary>
    Public NotInheritable Class HavokLayout

        Private ReadOnly _classes As Dictionary(Of String, HavokClass)
        ''' <summary>Los tamanos que emitio el generador para ESTE juego. Ver `SizeOfClass`.</summary>
        Private ReadOnly _sizes As Dictionary(Of String, Integer)
        Private ReadOnly _flat As New ConcurrentDictionary(Of String, IReadOnlyDictionary(Of String, HavokMember))(StringComparer.OrdinalIgnoreCase)

        Public ReadOnly Property Tag As String
        Public ReadOnly Property SourceSha256 As String
        Public ReadOnly Property SourceStamp As String
        ''' <summary>
        ''' ⛔⛔ NO ES EL ANCHO DE PUNTERO DEL ARCHIVO: es la EXPECTATIVA de este arbol.
        ''' <para>El ancho real lo declara el archivo en `layoutRules[0]`, y lo expone
        ''' `HkxObjectGraph_Class.AnchoDePuntero`. Quien lea bytes tiene que usar ESE, no este.</para>
        ''' <para>Este 8 existe para UN solo consumidor: el gate, que coteja los anchos que emite
        ''' el generador contra lo que dice el nombre de cada tipo, y necesita un numero escrito a
        ''' mano para que la comparacion no sea contra si misma. Los dos formatos soportados
        ''' (Fallout64 y Skyrim64) declaran 8; Skyrim32 declara 4 y NO esta soportado.</para>
        ''' </summary>
        Public ReadOnly Property PointerSizeEsperado As Integer = 8
        Public ReadOnly Property ClassCount As Integer
            Get
                Return _classes.Count
            End Get
        End Property
        Public ReadOnly Property ClassNames As IEnumerable(Of String)
            Get
                Return _classes.Keys
            End Get
        End Property

        ''' <summary>
        ''' ⛔⛔⛔ UNA TABLA QUE NO SALE DEL .exe DEL JUEGO, PARA UN INSTRUMENTO DE `Tools/`.
        ''' <para>El `CreationKit.exe` declara con la MISMA reflexion las clases que el .exe del juego
        ''' no declara —las `hcl*SetupObject` que el compilador de cloth emite offline— y el censo por
        ''' byte necesita esa tabla para decir que byte de esos bloques cubre un miembro declarado.
        ''' Es una pregunta distinta de "que lee la app": el motor SALTEA esos bloques (FO4
        ''' 0x14142B7B1, Skyrim 0x140B2E6C0) y la libreria replica al motor, asi que la tabla del CK
        ''' vive en `Tools/HkxLoadOrderAudit/Generated/` y NO viaja adentro de este binario.</para>
        ''' <para>Lo unico que aporta esta fabrica es el PARSEO, que ya vive aca: sin ella el
        ''' instrumento tendria que escribir una segunda copia de `ParseRow`, de la ley de alineacion
        ''' y del aplanado de herencia. No se llama desde ningun punto de la libreria, y el gate lo
        ''' EXIGE.</para>
        ''' <para>⛔⛔ LOS TAMANOS LOS TIENE QUE TRAER EL LLAMADOR, YA EMITIDOS. Aca se
        ''' sacaban del `objectSize` de las propias filas, con el argumento de que es "el mismo numero
        ''' de la misma fuente". No lo es: <see cref="HkSizes"/> ademas DERIVA el tamano de las clases
        ''' que declaran `objectSize` 0 con la regla del compilador de C++, y son 68 de las 674 del CK
        ''' de Skyrim y 3 de las 1105 del de Fallout. Con eso `SizeOfClass` —el mismo metodo publico—
        ''' contestaba un numero derivado o un 0 segun que fabrica hubiera armado la instancia: DOS
        ''' leyes. El generador emite ahora la tabla del CK con la MISMA funcion
        ''' (`gentyped.emitir_sizes` -> `HkSizesCK`), y esto solo la recibe.</para>
        ''' </summary>
        Public Shared Function Externa(tag As String, sha As String, stamp As String, rows As String(),
                                       enums As String(), memberFlagValues As String,
                                       typeTable As String,
                                       sizes As Dictionary(Of String, Integer)) As HavokLayout
            If sizes Is Nothing Then
                Throw New ArgumentNullException(NameOf(sizes),
                    "una tabla externa sin tamanos emitidos haria que `SizeOfClass` contestara con " &
                    "otra ley que la del juego. Ver `HkSizesCK`.")
            End If
            Return New HavokLayout(tag, sha, stamp, rows, enums, memberFlagValues, typeTable, sizes)
        End Function

        Private Sub New(tag As String, sha As String, stamp As String, rows As String(), enums As String(),
                        memberFlagValues As String, typeTable As String,
                        sizes As Dictionary(Of String, Integer))
            _Tag = tag
            _SourceSha256 = sha
            _SourceStamp = stamp
            _sizes = sizes
            _classes = New Dictionary(Of String, HavokClass)(StringComparer.OrdinalIgnoreCase)

            ' ⛔ LOS ENUMS SE PARSEAN PRIMERO por orden, no por dependencia: la alineacion y el bit de
            ' `SERIALIZE_IGNORED` salen de `memberFlagValues`, que el generador emite APARTE y llega por
            ' parametro. El orden se deja asi para que las dos cosas que describen a un miembro esten
            ' resueltas antes de construirlo.
            ' clase|enum|nombre=valor,nombre=valor,...
            _enums = New Dictionary(Of String, IReadOnlyDictionary(Of String, Integer))(StringComparer.OrdinalIgnoreCase)
            For Each row In enums
                Dim p = row.Split("|"c)
                If p.Length <> 3 Then Continue For
                Dim items As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                For Each it In p(2).Split(","c)
                    Dim eq = it.LastIndexOf("="c)
                    If eq <= 0 Then Continue For
                    Dim v As Integer
                    If Integer.TryParse(it.Substring(eq + 1), v) Then items(it.Substring(0, eq)) = v
                Next
                If items.Count > 0 Then _enums(p(0) & "|" & p(1)) = items
            Next

            ' ⛔⛔⛔ LA ALINEACION DEL TIPO SALE DE LA TABLA DEL MOTOR, y es lo que
            ' `getMemberAlignment` usa ANTES de mirar los flags. Ver `AlineacionDe`.
            _tipos = LeerTypeTable(typeTable)
            Dim alin = LeyDeAlineacion(memberFlagValues)
            ' ⛔⛔ IZADO, IGUAL QUE SU HERMANO DE ARRIBA. Se recalculaba por FILA: 946 veces
            ' en FO4 y 609 en SSE, spliteando el mismo string, mientras `AlineacionesDeclaradas` ya salia
            ' del bucle. Mismo dato, dos politicas, en lineas contiguas.
            Dim bitSI = BitDeSerializeIgnored(memberFlagValues)
            For Each row In rows
                Dim parsed = ParseRow(row, alin, bitSI)
                If parsed IsNot Nothing Then _classes(parsed.Name) = parsed
            Next

        End Sub

        ''' <summary>`tipo -> (size, align)` de la tabla que el MOTOR trae adentro. Ver `TypeTable`
        ''' en la tabla generada: la localiza el generador sin recibir direcciones.</summary>
        Private ReadOnly _tipos As Dictionary(Of String, (Size As Integer, Align As Integer))

        ''' <summary>`nombre=size:align,...` tal como lo emite el generador.</summary>
        Private Shared Function LeerTypeTable(s As String) As Dictionary(Of String, (Size As Integer, Align As Integer))
            Dim r As New Dictionary(Of String, (Size As Integer, Align As Integer))(StringComparer.OrdinalIgnoreCase)
            If String.IsNullOrEmpty(s) Then Return r
            For Each it In s.Split(","c)
                Dim eq = it.IndexOf("="c)
                If eq <= 0 Then Continue For
                Dim dp = it.IndexOf(":"c, eq + 1)
                If dp <= eq Then Continue For
                Dim sz As Integer, al As Integer
                If Not Integer.TryParse(it.Substring(eq + 1, dp - eq - 1), sz) Then Continue For
                If Not Integer.TryParse(it.Substring(dp + 1), al) Then Continue For
                r(it.Substring(0, eq)) = (sz, al)
            Next
            Return r
        End Function

        ''' <summary>
        ''' ⛔⛔⛔ LA LEY DE ALINEACION ES DEL MOTOR, Y ES POR IGUALDAD, NO POR MAXIMO.
        ''' <para><c>hkStructureLayout::getMemberAlignment</c> —FO4 0x14142F360, SSE 0x140B2F580—
        ''' enmascara los flags del miembro (<c>hkClassMember</c>+0x1C) y compara el resultado por
        ''' IGUALDAD: FO4 0x14142F434 <c>and ecx,0x980</c> y despues ==0x800 da 32, ==0x100 da 16, otro
        ''' no-cero da 8; SSE 0x140B2F654 <c>and ecx,0x180</c>, ==0x100 da 16, otro no-cero da 8.
        ''' La usa <c>computeMemberOffsets</c> (FO4 0x14142F4B0, SSE 0x140B2F6C0) para correr el offset
        ''' de cada miembro, con <c>idiv</c> —FO4 0x14142F6A7, SSE 0x140B2F8C4— y no con mascara de
        ''' bits, que es donde estuve buscando el relleno del .hkx y por eso no aparecia.</para>
        ''' <para>LA MASCARA NO SE HARDCODEA: es el OR de los bits que el propio enum declara con
        ''' nombre <c>ALIGN_*</c>. FO4 128|256|2048 = 0x980, el mismo inmediato de 0x14142F434; SSE
        ''' 128|256 = 0x180, el mismo de 0x140B2F654. Sale de la reflexion Y coincide con el binario.</para>
        ''' <para>Yo tomaba el MAXIMO de los bits prendidos. Es distinto: con <c>ALIGN_16</c> y
        ''' <c>ALIGN_8</c> a la vez, <c>v = 0x180</c> no es 0x800 ni 0x100, asi que el motor contesta 8
        ''' y yo contestaba 16. Medido sobre las tablas de hoy no cambia nada —ningun miembro prende
        ''' mas de uno: FO4 50 con solo 0x100 y 6 con solo 0x80, SSE 36 con solo 0x100, 0 divergencias
        ''' sobre 4.007 y 2.629 miembros—. Se cambia igual: lo que valia era una coincidencia del
        ''' corpus, no la ley.</para>
        ''' <para>QUE EL NOMBRE DIGA LA VERDAD NO SE SUPONE: lo prueba una columna INDEPENDIENTE de
        ''' la misma reflexion —el `offset` de cada miembro—. Medido: los 56 miembros de FO4 y los 36
        ''' de SSE que traen uno de estos flags tienen el offset alineado a ese numero. 92 de 92, cero
        ''' violaciones. Y desde abajo tambien: por cada alineacion declarada existe al menos un
        ''' miembro que la cumple y NO cumple el doble —2 con 8 que no cumplen 16, 16 con 16 que no
        ''' cumplen 32—, asi que un mapa a la mitad no pasaria. El gate exige las dos cosas.</para>
        ''' <para><c>ALIGN_REAL</c> no se interpreta: declara EL MISMO BIT que <c>ALIGN_16</c> (256) y
        ''' ningun dato los distingue. Al motor tampoco le importa: mira el bit, no el nombre.</para>
        ''' </summary>
        Private Shared Function LeyDeAlineacion(memberFlagValues As String) As (Mascara As Integer, Bit32 As Integer, Bit16 As Integer)
            Dim mascara = 0, b32 = 0, b16 = 0
            If String.IsNullOrEmpty(memberFlagValues) Then Return (0, 0, 0)
            For Each it In memberFlagValues.Split(","c)
                Dim eq = it.LastIndexOf("="c)
                If eq <= 0 Then Continue For
                Dim nombre = it.Substring(0, eq).Trim()
                Dim bit As Integer
                If Not Integer.TryParse(it.Substring(eq + 1), bit) OrElse bit <= 0 Then Continue For
                If Not nombre.StartsWith("ALIGN_", StringComparison.OrdinalIgnoreCase) Then Continue For
                mascara = mascara Or bit
                If String.Equals(nombre, "ALIGN_32", StringComparison.OrdinalIgnoreCase) Then b32 = bit
                If String.Equals(nombre, "ALIGN_16", StringComparison.OrdinalIgnoreCase) Then b16 = bit
            Next
            Return (mascara, b32, b16)
        End Function

        ''' <summary>
        ''' ⛔⛔⛔ LA ALINEACION DE UN MIEMBRO: LA DEL TIPO, Y DESPUES EL MAXIMO CON LA DE LOS FLAGS.
        ''' <para>Estuvo mal de dos maneras, las dos mias: la calculaba SOLO para los miembros con un
        ''' flag `ALIGN_*` (56 en FO4, 36 en SSE) cuando el motor le da alineacion a TODOS; y trataba
        ''' la etapa de flags como un reemplazo cuando es un MAXIMO — FO4 0x14142F44A y 0x14142F468,
        ''' SSE 0x140B2F66F: `cmp eax,ebx` + `cmovg ebx,eax`, con `ebx` = alineacion del tipo. Un
        ''' miembro con `ALIGN_8` sobre un tipo que alinea a 16 queda en 16.</para>
        ''' <para>Las ramas de `getMemberAlignment` (FO4 0x14142F360 / SSE 0x140B2F580): `zero` se
        ''' resuelve por el subtipo (0x14142F3B2); la familia de punteros —`pointer`,
        ''' `functionpointer`, `array`, `simplearray`, `homogeneousarray`, `variant`, `cstring`,
        ''' `ulong`, `stringptr`— alinea a `ptrSize` (0x14142F3AE); `enum` y `flags` por el subtipo;
        ''' `struct` es el maximo de sus miembros (0x14142F3C2), que aca NO se resuelve —haria falta
        ''' la clase entera y este parser va fila por fila— y se devuelve 0, que es "no determinada";
        ''' `inplacearray` deja `ebx` en -1 y tampoco se determina; el resto sale de `record[t].align`
        ''' de la tabla del motor.</para>
        ''' </summary>
        Private Function AlineacionDe(tipo As String, sub_ As String, flags As Integer,
                                      ley As (Mascara As Integer, Bit32 As Integer, Bit16 As Integer)) As Integer
            Dim a = AlineacionDelTipo(tipo, sub_)
            Dim fa = 0
            If ley.Mascara <> 0 Then
                Dim v = flags And ley.Mascara
                If v <> 0 Then
                    If ley.Bit32 <> 0 AndAlso v = ley.Bit32 Then
                        fa = 32
                    ElseIf ley.Bit16 <> 0 AndAlso v = ley.Bit16 Then
                        fa = 16
                    Else
                        fa = 8
                    End If
                End If
            End If
            Return Math.Max(a, fa)
        End Function

        ''' <summary>La alineacion que la tabla del MOTOR le da a un tipo, para que un gate pueda
        ''' preguntarle por uno NOMBRADO. Sin esto, vaciar la `TypeTable` no ponia nada en rojo.</summary>
        Public Function AlineacionDeTipoParaGate(tipo As String) As Integer
            Return AlineacionDelTipo(tipo, "")
        End Function

        ''' <summary>La alineacion que le da el TIPO, 0 si la reflexion no alcanza. Ver `AlineacionDe`.
        ''' <para>⛔⛔⛔ SALE ENTERA DE LA TABLA DEL MOTOR. Aca habia ademas una lista escrita a
        ''' mano de nueve nombres —`pointer`, `functionpointer`, `array`, `simplearray`,
        ''' `homogeneousarray`, `variant`, `cstring`, `ulong`, `stringptr`— que contestaban
        ''' `PointerSizeEsperado`. Es la rama de `getMemberAlignment` en FO4 `0x14142F3AE`, y el
        ''' generador ya la tenia transcrita en `tipos.T_ALIN_PTR` con esa misma cita — pero MUERTA,
        ''' sin un solo consumidor. Dos copias de una ley, y la que tenia la cita era la que no
        ''' corria.</para>
        ''' <para>Sobra: MEDIDO en los dos binarios, esos nueve tipos declaran `align = 8` en la
        ''' propia tabla del motor, que es el `ptrSize` de x64. No hace falta la rama; alcanza con
        ''' leer la tabla. `HavokLayoutGate` lo EXIGE nombre por nombre, asi que si algun dia
        ''' dejaran de coincidir se pone en rojo en vez de contestar distinto en silencio. (Un
        ''' layout de 32 bits los separaria, y por eso Skyrim32 no esta soportado — lo dice
        ''' <see cref="PointerSizeEsperado"/>.)</para>
        ''' <para>`-1` en la tabla del motor —`void`, `zero`, `inplacearray`, `enum`, `struct`,
        ''' `flags`— significa "no la determina el tipo": `zero`/`enum`/`flags` se resuelven por el
        ''' SUBTIPO antes de mirar, y `struct`/`inplacearray` necesitan la clase entera. Sale 0, que
        ''' es lo que `AlineacionDe` interpreta como no determinada.</para></summary>
        Private Function AlineacionDelTipo(tipo As String, sub_ As String) As Integer
            Dim t = (tipo + "").ToLowerInvariant()
            If t = "zero" OrElse t = "enum" OrElse t = "flags" Then t = (sub_ + "").ToLowerInvariant()
            If t.Length = 0 Then Return 0
            Dim v As (Size As Integer, Align As Integer) = Nothing
            If _tipos IsNot Nothing AndAlso _tipos.TryGetValue(t, v) AndAlso v.Align > 0 Then Return v.Align
            Return 0
        End Function

        ''' <summary>El bit que el juego declara para `SERIALIZE_IGNORED`, del mismo enum emitido.</summary>
        Private Shared Function BitDeSerializeIgnored(memberFlagValues As String) As Integer
            If String.IsNullOrEmpty(memberFlagValues) Then Return 0
            For Each it In memberFlagValues.Split(","c)
                Dim eq = it.LastIndexOf("="c)
                If eq <= 0 Then Continue For
                If Not it.Substring(0, eq).Trim().Equals("SERIALIZE_IGNORED", StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim v As Integer
                If Integer.TryParse(it.Substring(eq + 1), v) Then Return v
            Next
            Return 0
        End Function

        Private Function ParseRow(row As String, alineaciones As (Mascara As Integer, Bit32 As Integer, Bit16 As Integer),
                                         bitSI As Integer) As HavokClass
            If String.IsNullOrEmpty(row) Then Return Nothing
            Dim head = row.Split("|"c)
            If head.Length < 5 Then Return Nothing
            Dim size As Integer
            Integer.TryParse(head(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, size)
            Dim ver As Integer
            Integer.TryParse(head(3), NumberStyles.Integer, CultureInfo.InvariantCulture, ver)

            Dim members As New List(Of HavokMember)
            If head(4).Length > 0 Then
                For Each part In head(4).Split(";"c)
                    If part.Length = 0 Then Continue For
                    Dim f = part.Split(","c)
                    If f.Length < 6 Then Continue For
                    ' ⛔⛔⛔ CON INICIALIZADOR, SIEMPRE. En VB un `Dim` sin inicializador
                    ' adentro de un bucle NO vuelve a cero en cada vuelta: la variable es del METODO y
                    ' conserva el valor de la iteracion anterior. Con los campos que se leen bajo guarda
                    ' —`flags`, `ancho`, `anchoElem`— eso hacia que un miembro cuyo campo viene VACIO
                    ' heredara el valor del miembro ANTERIOR.
                    ' MEDIDO: `BSCyclicBlendTransitionGenerator.sortedChildren` es `array` sin subtipo, o
                    ' sea que la tabla no declara su ancho de elemento, y contestaba 8 — el de
                    ' `pTransitionBlenderGeneratorsA`, el miembro de justo antes, que es `array<pointer>`.
                    ' Estuvo tapado mientras el generador emitia un `0` explicito, porque la guarda nunca
                    ' se salteaba; al emitir "no declarado" quedo a la vista.
                    Dim off As Integer = 0
                    Integer.TryParse(f(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, off)
                    Dim carr As Integer = 0
                    Integer.TryParse(f(4), NumberStyles.Integer, CultureInfo.InvariantCulture, carr)
                    ' ⛔ EL SEPTIMO CAMPO SON LOS `flags`, EN HEXA. Se lee con guarda de largo porque una
                    ' tabla vieja no lo trae: ausente = 0, que es `FLAGS_NONE`.
                    Dim flg As Integer = 0
                    If f.Length > 6 AndAlso f(6).Length > 0 Then
                        Integer.TryParse(f(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, flg)
                    End If
                    ' ⛔ EL OCTAVO CAMPO ES EL ANCHO, EN HEXA. Misma guarda de largo que los flags:
                    ' ausente = 0 = "la tabla no lo declara", que el consumidor tiene que distinguir de un
                    ' ancho que de verdad vale 0.
                    Dim anc As Integer = 0
                    If f.Length > 7 AndAlso f(7).Length > 0 Then
                        Integer.TryParse(f(7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, anc)
                    End If
                    ' El NOVENO campo es el ancho del ELEMENTO, tambien en hexa y con la misma guarda.
                    Dim ael As Integer = 0
                    If f.Length > 8 AndAlso f(8).Length > 0 Then
                        Integer.TryParse(f(8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, ael)
                    End If
                    ' La alineacion, con la ley del motor. Ver `LeyDeAlineacion`.
                    Dim alg = AlineacionDe(f(2), f(3), flg, alineaciones)
                    members.Add(New HavokMember(f(0), off, f(2), f(3), carr, f(5), flg, anc, ael, alg, bitSI))
                Next
            End If
            Return New HavokClass(head(0), head(1), size, ver, members)
        End Function

        ' ---------------------------------------------------------------------------------------
        ' Consulta
        ' ---------------------------------------------------------------------------------------

        Public Function TryGetClass(className As String, <Runtime.InteropServices.Out> ByRef result As HavokClass) As Boolean
            result = Nothing
            If String.IsNullOrEmpty(className) Then Return False
            Return _classes.TryGetValue(className, result)
        End Function

        ''' <summary>
        ''' ⛔ SI UNA CLASE DERIVA DE OTRA, LO DICE LA TABLA. La reflexion declara el padre de cada
        ''' clase, asi que "¿es un generador?" se contesta subiendo por `ParentName` hasta la raiz.
        ''' <para>Decidirlo por el NOMBRE esta medidamente mal: contra la union de las dos tablas,
        ''' 40 clases derivan de `hkbGenerator` y 58 contienen "Generator". La regla por nombre se
        ''' pierde `hkbBehaviorGraph` y los cinco `*TransitionEffect`, y mete 25 de mas que son
        ''' `*InternalState` y `hkbGeneratorSyncInfo*` — datos, no generadores.</para>
        ''' </summary>
        Public Function DerivaDe(className As String, baseName As String) As Boolean
            If String.IsNullOrEmpty(className) OrElse String.IsNullOrEmpty(baseName) Then Return False
            Dim actual = className
            Dim vistas As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            While Not String.IsNullOrEmpty(actual) AndAlso vistas.Add(actual)
                If actual.Equals(baseName, StringComparison.OrdinalIgnoreCase) Then Return True
                Dim c As HavokClass = Nothing
                If Not _classes.TryGetValue(actual, c) OrElse c Is Nothing Then Return False
                actual = c.ParentName
            End While
            Return False
        End Function

        Private ReadOnly _enums As Dictionary(Of String, IReadOnlyDictionary(Of String, Integer))

        ''' <summary>
        ''' ⛔⛔ LOS ENUMS QUE LA REFLEXION DECLARA. MISMA FUENTE QUE LOS OFFSETS.
        ''' <para>El ctor de `hkClass` pasa `declaredEnums` en `[rsp+0x30]` y `numDeclaredEnums` en
        ''' `[rsp+0x38]`, al lado de los miembros. El generador los ignoraba, asi que cosas como
        ''' `hkaSplineCompressedAnimationTrackCompressionParams.RotationQuantization`
        ''' (`POLAR32=0 . THREECOMP40=1 . THREECOMP48=2 . THREECOMP24=3 . STRAIGHT16=4 .
        ''' UNCOMPRESSED=5`) se escribian a mano sin poder citarlas. Ahora salen del binario.</para>
        ''' </summary>
        Public Function EnumValues(className As String, enumName As String) As IReadOnlyDictionary(Of String, Integer)
            If String.IsNullOrEmpty(className) OrElse String.IsNullOrEmpty(enumName) Then Return Nothing
            Dim r As IReadOnlyDictionary(Of String, Integer) = Nothing
            If _enums.TryGetValue(className & "|" & enumName, r) Then Return r
            Return Nothing
        End Function

        Public Function HasClass(className As String) As Boolean
            Return Not String.IsNullOrEmpty(className) AndAlso _classes.ContainsKey(className)
        End Function

        Public Function ClassSize(className As String) As Integer
            Dim c As HavokClass = Nothing
            Return If(TryGetClass(className, c), c.Size, -1)
        End Function

        ''' <summary>
        ''' Mapa APLANADO de la clase: incluye los miembros heredados del padre y los de los structs
        ''' embebidos, con la ruta separada por puntos ("atoms.twistLimit.minAngle") y el offset ya
        ''' absoluto dentro de la clase.
        ''' </summary>
        Public Function Flat(className As String) As IReadOnlyDictionary(Of String, HavokMember)
            If String.IsNullOrEmpty(className) Then Return EmptyFlat
            Dim cached As IReadOnlyDictionary(Of String, HavokMember) = Nothing
            If _flat.TryGetValue(className, cached) Then Return cached
            Dim built As IReadOnlyDictionary(Of String, HavokMember) = BuildFlat(className, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase))
            _flat(className) = built
            Return built
        End Function

        Private Shared ReadOnly EmptyFlat As IReadOnlyDictionary(Of String, HavokMember) =
            New Dictionary(Of String, HavokMember)(StringComparer.OrdinalIgnoreCase)

        Private Function BuildFlat(className As String, visiting As HashSet(Of String)) As IReadOnlyDictionary(Of String, HavokMember)
            Dim result As New Dictionary(Of String, HavokMember)(StringComparer.OrdinalIgnoreCase)
            Dim c As HavokClass = Nothing
            If Not TryGetClass(className, c) Then Return result
            ' Ciclo (no deberia haberlo, pero una tabla generada corrupta no puede colgar el render).
            If Not visiting.Add(className) Then Return result

            If Not String.IsNullOrEmpty(c.ParentName) Then
                ' Los miembros del padre ya vienen con offset absoluto: no se desplazan.
                For Each kv In BuildFlat(c.ParentName, visiting)
                    result(kv.Key) = kv.Value
                Next
            End If

            For Each m In c.Declared
                result(m.Name) = m
                ' Struct embebido (no array C de structs: ahi el offset por elemento depende del stride
                ' y no hay un camino unico).
                If String.Equals(m.TypeName, "struct", StringComparison.Ordinal) AndAlso
                   m.CArraySize = 0 AndAlso Not String.IsNullOrEmpty(m.StructClassName) Then
                    For Each kv In BuildFlat(m.StructClassName, visiting)
                        result(m.Name & "." & kv.Key) = kv.Value.Shifted(m.Offset, m.Name & "." & kv.Key)
                    Next
                End If
            Next

            visiting.Remove(className)
            Return result
        End Function

        ''' <summary>
        ''' El tamano en bytes de la clase, en el layout x64.
        ''' <para>⛔ NO SE DERIVA ACA. Habia una derivacion propia —con su tabla de anchos, su tabla de
        ''' alineamientos y su respaldo a la otra tabla— que era una SEGUNDA transcripcion de
        ''' `gentyped.ancho`, y las dos YA HABIAN DIVERGIDO: medido, 253 clases de FO4 y 205 de SSE
        ''' daban distinto. Se dejo declarada como duplicacion inevitable "porque el generador corre
        ''' offline y no puede llamar a esta funcion" — lo que no puede es LLAMARLA; EMITIR la respuesta
        ''' si puede, que es lo que ya hacia con los offsets y con los strides. Ahora la emite:
        ''' <see cref="HkSizes"/>.</para>
        ''' <para>0 = la tabla no declara esa clase, o la declara sin miembros y sin `objectSize`. El
        ''' llamador decide; no se inventa un tamano.</para>
        ''' </summary>
        Public Function SizeOfClass(className As String) As Integer
            If String.IsNullOrEmpty(className) Then Return 0
            Dim n As Integer
            If _sizes IsNot Nothing AndAlso _sizes.TryGetValue(className, n) Then Return n
            Return 0
        End Function

        ''' <summary>
        ''' El ancho de un TIPO declarado en el layout x64, o -1 si no lo conoce.
        ''' <para>⛔ LO EMITE EL GENERADOR, igual que los tamanos de clase. La version anterior era una
        ''' transcripcion a mano (`SizeOfType`) que se borro al unificar sin reponer nada, y el
        ''' instrumento que coteja strides termino escribiendo su propia copia reducida adentro del
        ''' gate. Un tipo que no esta devuelve -1 y el llamador se planta: no se inventa un ancho.</para>
        ''' </summary>
        Public Shared Function AnchoDeTipo(tipo As String) As Integer
            Return HkSizes.AnchoDeTipo(tipo)
        End Function

        ''' <summary>Offset absoluto del miembro (ruta con puntos), o -1 si la clase o el miembro no existen.</summary>
        Public Function Offset(className As String, memberPath As String) As Integer
            Dim m = Member(className, memberPath)
            Return If(m Is Nothing, -1, m.Offset)
        End Function

        ''' <summary>El miembro, o Nothing.</summary>
        Public Function Member(className As String, memberPath As String) As HavokMember
            If String.IsNullOrEmpty(memberPath) Then Return Nothing
            Dim map = Flat(className)
            Dim result As HavokMember = Nothing
            If Not map.TryGetValue(memberPath, result) Then Return Nothing
            ' ⛔ SE REGISTRA DESPUES DE ENCONTRARLO. Antes se registraba la PETICION, asi que una ruta
            ' inexistente entraba al censo: el registro decia "lo que se pidio", no "lo que se leyo".
            If RecordCoverage Then RecordRequest(className, memberPath)
            Return result
        End Function

        ' =======================================================================================
        '  COBERTURA DE PARSEO — exacta, en runtime
        '
        '  ⛔ POR QUE ACA Y NO CON UN SCRIPT QUE MIRE EL CODIGO: intente auditar la cobertura con
        '  expresiones regulares sobre los .vb y mintio en las dos direcciones. Marcaba clases
        '  COMPLETAS como vacias (los lambdas `Function(...) ... End Function` cortaban el cuerpo que
        '  el script creia estar mirando) y no veia los campos leidos por helpers locales. Un
        '  instrumento que miente sobre que falta es peor que no tener instrumento: da por cerrado
        '  lo que esta abierto.
        '
        '  Aca no hay heuristica: TODO offset sale de `Member()`, asi que registrar la peticion es
        '  la verdad por construccion. Se prende desde un gate, se parsea un corpus, y lo que la
        '  reflexion declara y nadie pidio es EXACTAMENTE lo que el parser no lee.
        ' =======================================================================================

        ''' <summary>Prender ANTES de parsear. Apagado no cuesta nada (una comparacion booleana).</summary>
        Public Shared Property RecordCoverage As Boolean = False

        ''' <summary>
        ''' ⛔ LA CLAVE LLEVA EL JUEGO. Esto es `Shared` y antes se indexaba SOLO por nombre de clase:
        ''' como la mayoria de las `hka*`/`hkb*` existen con el mismo nombre en las dos tablas, un
        ''' barrido de un load order mixto acreditaba en SSE lo que solo se habia leido en FO4. La
        ''' cobertura salia inflada y no habia forma de verlo desde el reporte.
        ''' </summary>
        Private Shared ReadOnly _requested As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase)

        ''' <summary>La clave del censo: `tag|clase`. El tag lo declara la tabla.</summary>
        Private Function ClaveDeCenso(className As String) As String
            Return If(Tag, String.Empty) & "|" & className
        End Function

        Private Sub RecordRequest(className As String, memberPath As String)
            If String.IsNullOrEmpty(className) Then Exit Sub
            Dim clave = ClaveDeCenso(className)
            SyncLock _requested
                Dim set0 As HashSet(Of String) = Nothing
                If Not _requested.TryGetValue(clave, set0) Then
                    set0 = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                    _requested(clave) = set0
                End If
                set0.Add(memberPath)
                ' Una ruta anidada `a.b` tambien acredita al padre `a`: el parser lo esta leyendo,
                ' solo que entrando directo al campo de adentro.
                Dim dot = memberPath.IndexOf("."c)
                If dot > 0 Then set0.Add(memberPath.Substring(0, dot))
            End SyncLock
        End Sub

        ''' <summary>Lo pedido por `tag|clase`. ⛔ La clave lleva el JUEGO: dos tablas declaran la
        ''' misma clase y lo leido en una no acredita en la otra. Usar <see cref="CoberturaDe"/> para
        ''' consultarla desde una tabla concreta.</summary>
        Private Shared Function CoverageSnapshot() As Dictionary(Of String, HashSet(Of String))
            SyncLock _requested
                Dim copy As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase)
                For Each kv In _requested
                    copy(kv.Key) = New HashSet(Of String)(kv.Value, StringComparer.OrdinalIgnoreCase)
                Next
                Return copy
            End SyncLock
        End Function

        ''' <summary>Lo que se leyo de <paramref name="className"/> EN ESTA TABLA. Vacio si nada.</summary>
        Public Function CoberturaDe(className As String) As HashSet(Of String)
            Dim clave = ClaveDeCenso(className)
            SyncLock _requested
                Dim set0 As HashSet(Of String) = Nothing
                If _requested.TryGetValue(clave, set0) Then Return New HashSet(Of String)(set0, StringComparer.OrdinalIgnoreCase)
            End SyncLock
            Return New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        End Function

        ''' <summary>
        ''' ⛔ REGISTRA UNA LECTURA REAL DE CAMPOS. Existe porque el codigo GENERADO no pasa por
        ''' `Member`/`Offset`: los `HkObj_*` traen los offsets precalculados, asi que todo lo que se
        ''' lee por la capa de objetos era INVISIBLE para el censo. El unico punto por el que esa
        ''' capa pasa entera es `HavokObjetoGenerico.Leer`, y desde ahi se registra.
        ''' <para>Antes esto no se notaba porque `SizeOfClass` acreditaba el 100 % de cualquier
        ''' clase sin haber leido un byte: el censo daba numeros altos por el motivo equivocado.</para>
        ''' </summary>
        Public Sub RegistrarLectura(className As String, campos As IEnumerable(Of String))
            If Not RecordCoverage OrElse String.IsNullOrEmpty(className) OrElse campos Is Nothing Then Exit Sub
            For Each c In campos
                RecordRequest(className, c)
            Next
        End Sub

        Public Shared Sub ResetCoverage()
            SyncLock _requested
                _requested.Clear()
            End SyncLock
        End Sub

        ''' <summary>Offset del miembro; lanza si no existe. Para sitios donde un fallo debe aflorar.</summary>
        Public Function RequireOffset(className As String, memberPath As String) As Integer
            Dim o = Offset(className, memberPath)
            If o < 0 Then
                Throw New InvalidOperationException(
                    $"[HavokLayout/{Tag}] no existe '{className}.{memberPath}' en la tabla canonica " &
                    $"({ClassCount} clases, exe {SourceStamp}). Regenerar con Tools/HavokLayoutGen.")
            End If
            Return o
        End Function

        ' ---------------------------------------------------------------------------------------
        ' Instancias por juego
        ' ---------------------------------------------------------------------------------------

        Private Shared ReadOnly _fo4 As New Lazy(Of HavokLayout)(
            Function() New HavokLayout("FO4",
                                       HavokLayoutData_FO4.SourceSha256,
                                       HavokLayoutData_FO4.SourceStamp,
                                       HavokLayoutData_FO4.Rows,
                                       HavokLayoutData_FO4.Enums,
                                       HavokLayoutData_FO4.MemberFlagValues,
                                       HavokLayoutData_FO4.TypeTable,
                                       HkSizes.Para("FO4")))

        Private Shared ReadOnly _sse As New Lazy(Of HavokLayout)(
            Function() New HavokLayout("SSE",
                                       HavokLayoutData_SSE.SourceSha256,
                                       HavokLayoutData_SSE.SourceStamp,
                                       HavokLayoutData_SSE.Rows,
                                       HavokLayoutData_SSE.Enums,
                                       HavokLayoutData_SSE.MemberFlagValues,
                                       HavokLayoutData_SSE.TypeTable,
                                       HkSizes.Para("SSE")))

        ''' <summary>Tabla de Fallout 4 (hk2014 x64).</summary>
        Public Shared ReadOnly Property FO4 As HavokLayout
            Get
                Return _fo4.Value
            End Get
        End Property

        ''' <summary>Tabla de Skyrim Special Edition (x64).</summary>
        Public Shared ReadOnly Property SSE As HavokLayout
            Get
                Return _sse.Value
            End Get
        End Property

        ''' <summary>
        ''' Tabla canonica del formato que el PACKFILE DECLARA. Es el unico punto donde se decide que
        ''' juego se esta leyendo, y la decision sale del archivo, no de la config: `Config_App.Game`
        ''' viene en Skyrim por defecto y usarlo aca haria que un HKX de Fallout se leyera con la tabla
        ''' equivocada. Nothing = formato sin tabla (Skyrim32).
        ''' </summary>
        Public Shared Function ForGraph(graph As HkxObjectGraph_Class) As HavokLayout
            ' El formato lo declara el GRAFO: el de arranque lo trae derivado y el normal lo copia del
            ' packfile. Preguntarselo a `Packfile.Header` ataba esto a que la cabecera ya estuviera
            ' parseada, que es lo que impedia leer la cabecera con el lector generado.
            If graph Is Nothing Then Return Nothing
            Return [For](graph.Formato)
        End Function

        Public Shared Function [For](format As HkxPackfileFormat_Enum) As HavokLayout
            Select Case format
                Case HkxPackfileFormat_Enum.Fallout64 : Return FO4
                Case HkxPackfileFormat_Enum.Skyrim64 : Return SSE
                Case Else : Return Nothing          ' Skyrim32
            End Select
        End Function

        Public Overrides Function ToString() As String
            Return $"HavokLayout[{Tag}] {ClassCount} clases, exe {SourceStamp}"
        End Function

    End Class

End Namespace
