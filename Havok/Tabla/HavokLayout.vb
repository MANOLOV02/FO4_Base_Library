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

        Friend Sub New(name As String, offset As Integer, typeName As String,
                       subTypeName As String, cArraySize As Integer, structClassName As String)
            _Name = name
            _Offset = offset
            _TypeName = typeName
            _SubTypeName = subTypeName
            _CArraySize = cArraySize
            _StructClassName = structClassName
        End Sub

        Friend Function Shifted(delta As Integer, path As String) As HavokMember
            Return New HavokMember(path, Offset + delta, TypeName, SubTypeName, CArraySize, StructClassName)
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
        ''' <summary>La derivacion PURA del ABI. Ver <see cref="SizeOfClassAbi"/>.</summary>
        Private ReadOnly _abi As Dictionary(Of String, Integer)
        Private ReadOnly _flat As New ConcurrentDictionary(Of String, IReadOnlyDictionary(Of String, HavokMember))(StringComparer.OrdinalIgnoreCase)

        Public ReadOnly Property Tag As String
        Public ReadOnly Property SourceSha256 As String
        Public ReadOnly Property SourceStamp As String
        ''' <summary>La tabla describe el layout x64. Siempre 8.</summary>
        Public ReadOnly Property PointerSize As Integer = 8
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

        Private Sub New(tag As String, sha As String, stamp As String, rows As String(), enums As String())
            _Tag = tag
            _SourceSha256 = sha
            _SourceStamp = stamp
            _sizes = HkSizes.Para(tag)
            _abi = HkSizes.AbiPara(tag)
            _classes = New Dictionary(Of String, HavokClass)(StringComparer.OrdinalIgnoreCase)
            For Each row In rows
                Dim parsed = ParseRow(row)
                If parsed IsNot Nothing Then _classes(parsed.Name) = parsed
            Next

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
        End Sub

        Private Shared Function ParseRow(row As String) As HavokClass
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
                    Dim off As Integer
                    Integer.TryParse(f(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, off)
                    Dim carr As Integer
                    Integer.TryParse(f(4), NumberStyles.Integer, CultureInfo.InvariantCulture, carr)
                    members.Add(New HavokMember(f(0), off, f(2), f(3), carr, f(5)))
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

        ''' <summary>
        ''' El tamano que da la REGLA DEL ABI x64 —ultimo miembro + su ancho, redondeado al
        ''' alineamiento del struct— ejercida SIEMPRE, sin el atajo del `objectSize`.
        ''' <para>⛔ EXISTE PARA PODER COTEJAR LA REGLA CONTRA EL BINARIO, y por ninguna otra razon:
        ''' NO se usa para leer. `SizeOfClass` devuelve el `objectSize` que declara la reflexion cuando
        ''' lo declara, asi que para esas clases la derivacion no se ejerce — y son justamente las
        ''' unicas para las que el .exe tiene la respuesta. Comparar las dos es lo que hace que un
        ''' error DENTRO de la derivacion pueda ponerse en rojo; sin esto el gate compara la
        ''' derivacion contra si misma.</para>
        ''' <para>MEDIDO: reproduce el `objectSize` declarado en 230 de 240 clases de FO4 y 129 de 138
        ''' de SSE. Las que no, son clases con miembros finales que la reflexion NO declara
        ''' (`hkDefaultMemoryTracker` declara 2 miembros de una clase de 248 bytes) — por eso el
        ''' derivado es una COTA INFERIOR y nunca puede pasarse.</para>
        ''' </summary>
        Public Function SizeOfClassAbi(className As String) As Integer
            If String.IsNullOrEmpty(className) Then Return 0
            Dim n As Integer
            If _abi IsNot Nothing AndAlso _abi.TryGetValue(className, n) Then Return n
            Return 0
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
                                       HavokLayoutData_FO4.Enums))

        Private Shared ReadOnly _sse As New Lazy(Of HavokLayout)(
            Function() New HavokLayout("SSE",
                                       HavokLayoutData_SSE.SourceSha256,
                                       HavokLayoutData_SSE.SourceStamp,
                                       HavokLayoutData_SSE.Rows,
                                       HavokLayoutData_SSE.Enums))

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
