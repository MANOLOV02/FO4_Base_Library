Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Motor

' =================================================================================================
' `BSTransformSet` — la capa de Bethesda entre el esqueleto del actor y `hclClothInstance.transformSets`.
'
' Vtable `0x1426B24E0`: `+0x20` = TtBeginAccess BoneMap -> TransformSet (`0x1418A6080`), que llama
' `Operator Prepare` (`0x1418C8DE0`) ANTES de la cadena; `+0x28` = TtBSTransformSet::notifyEndAccess
' (`0x1418A6980`), que llama `Runtime Buffers Release` (`0x1418C908C`) DESPUÉS.
'
' ⛔⛔ LO QUE HACÍA LA APP Y EL MOTOR NO HACE:
'   · rearmaba el transform set entero cada cuadro desde el esqueleto, y le devolvía a los cloth-bones
'     lo que el deform había escrito — el motor lee SÓLO los huesos de la máscara de lectura;
'   · escribía la capa con la salida CRUDA del último paso, contra la pose del padre del cuadro — el
'     motor EXTRAPOLA la salida al tiempo del cuadro y compone el hijo contra la SALIDA del padre;
'   · no interpolaba la entrada entre dos cuadros — el motor la interpola en cada paso.
'
' Todo lo que sigue sale de esas dos funciones, del armado de máscaras (`0x1418A50A3`) y del
' reparto del cuadro (`0x1418A5DF0`).
'
' Transcripto además: la raíz y el teletransporte (`+0x188`/`+0x140`, `0x1418A6132`-`0x1418A63DC`), el
' camino sin nodo (`+0x100`), los nodos creados y su cuelgue (`0x1418A45EE`, `0x1418A6F5A`), las
' inversas traspuestas (`0x1418EE230`), el chequeo de hueso rápido (`0x1418A65E3`) y las banderas de
' la máquina de estados del job (`+0x198`/`+0x19C`/`+0x1A0`/`+0x1B6`).
'
' ⭐ CERRADOS CON CITA (el `.exe` no alcanza esos caminos, no son huecos):
'   · la escala por eje del padre (`[ts+0xB8]`/`[ts+0xA0]`, `0x1418A7014`): el constructor llena `+0xB8`
'     con `0xFFFF` (`0x1418A441D rep stosw`) y el único escritor que lo cambia (`0x1418A5E40`) no tiene
'     referencias (ni `call`, ni `jmp`, ni puntero, ni `lea`; sólo su `.pdata`);
'
' ⛔ CORREGIDO (15-sep): el asentamiento (`uNumSimSettleSteps`, `0x14187903E`) SÍ se alcanza. Exige
' `[ts+0x1B7]`; el constructor lo deja en 0 (`0x1418A410A mov dword [+0x1B4], 0x101`), pero lo escribe
' `0x1418A1B60` (`mov byte [rcx+0x1B7], r9b` sobre `[[ci+0x40]]` de cada instancia; función hoja SIN
' `.pdata`, por eso el barrido por funciones no la vio), llamada sólo desde `0x1406F145F` con
' `dl = (actor == [0x1431EDE50])`, el `PlayerCharacter` (`0x140D53CFD`, en el constructor `0x140D52870`
' que pone la vtable `0x14256C938`). Transcripto en `Asentar` y en `ClothCanonico.CorrerJob`.
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>El NiTransform de un nodo como lo lee y escribe el motor: tres filas del
    ''' `NiMatrix3` (`+0x70`/`+0x80`/`+0x90`), traslación (`+0xA0`) y escala (`+0xAC`).</summary>
    Friend Structure NiTransformDelMotor
        Friend F0 As Vector128(Of Single)
        Friend F1 As Vector128(Of Single)
        Friend F2 As Vector128(Of Single)
        Friend T As Vector128(Of Single)
        Friend S As Single

        Friend Shared Function DeTransform(t As Transform_Class) As NiTransformDelMotor
            Dim r As NiTransformDelMotor
            Dim m = t.Rotation
            r.F0 = Vector128.Create(m.M11, m.M12, m.M13, 0.0F)
            r.F1 = Vector128.Create(m.M21, m.M22, m.M23, 0.0F)
            r.F2 = Vector128.Create(m.M31, m.M32, m.M33, 0.0F)
            r.T = Vector128.Create(t.Translation.X, t.Translation.Y, t.Translation.Z, 0.0F)
            Dim exacto As Boolean
            r.S = t.EscalaComoEscalar(exacto)
            Return r
        End Function

        Friend Function ATransform() As Transform_Class
            Return New Transform_Class With {
                .Rotation = New NiflySharp.Structs.Matrix33 With {
                    .M11 = F0.GetElement(0), .M12 = F0.GetElement(1), .M13 = F0.GetElement(2),
                    .M21 = F1.GetElement(0), .M22 = F1.GetElement(1), .M23 = F1.GetElement(2),
                    .M31 = F2.GetElement(0), .M32 = F2.GetElement(1), .M33 = F2.GetElement(2)},
                .Translation = New System.Numerics.Vector3(T.GetElement(0), T.GetElement(1), T.GetElement(2)),
                .Scale = S,
                .ScaleVector = New System.Numerics.Vector3(1, 1, 1)}
        End Function
    End Structure

    ''' <summary>Lo que el `BSTransformSet` necesita del esqueleto vivo, por índice de hueso del
    ''' `hkaSkeleton` de la prenda.</summary>
    Friend Interface INodosDeTela
        ''' <summary>El mundo del nodo (`node+0x70..+0xAC`), o `Nothing` si el hueso no tiene nodo.</summary>
        Function Mundo(indice As Integer) As Transform_Class
        ''' <summary>El índice (en el `hkaSkeleton`) del PADRE NiNode del nodo, o −1 si el padre no
        ''' es un hueso de la prenda.</summary>
        Function IndiceDelPadreNi(indice As Integer) As Integer
        ''' <summary>El mundo del padre NiNode (`node[+0x28]`), o `Nothing` si no tiene.</summary>
        Function MundoDelPadreNi(indice As Integer) As Transform_Class
        ''' <summary>Escribe el LOCAL que `0x1418A75CD`-`0x1418A7841` deja en `node+0x30..+0x6C`.</summary>
        Sub EscribirLocal(indice As Integer, local As NiTransformDelMotor)
        ''' <summary>`AttachChild` (`0x1418A6F5A`, `vtbl[+0x1D0]` del nodo del padre HK): el nodo sin
        ''' padre NiNode pasa a colgar del hueso `padre`. Desde ahí su padre NiNode es ése, y su mundo
        ''' es `W(padre) ∘ local`.</summary>
        Sub Colgar(indice As Integer, padre As Integer)
    End Interface

    Friend NotInheritable Class ConjuntoDeTransforms

        Friend ReadOnly NumHuesos As Integer
        ''' <summary>`+0x58`: los huesos cuya entrada se lee del nodo.</summary>
        Friend ReadOnly Leidos As Boolean()
        ''' <summary>`+0x40`: los huesos cuyo nodo se escribe.</summary>
        Friend ReadOnly Escritos As Boolean()
        ''' <summary>`+0x70`: escritos y sus padres — los que tienen salida (`+0x130`).</summary>
        Friend ReadOnly EscritosYPadres As Boolean()
        ''' <summary>`hkaSkeleton.parentIndices` (`[ts+0x30]`→`[+0x18]`, `int16`).</summary>
        Friend ReadOnly Padres As Integer()

        ''' <summary>`+0xE8`: los índices, ordenados y sin repetir (`0x1418A8980` + `0x1418A4CB0`).</summary>
        Private ReadOnly _entradas As Integer()
        Private ReadOnly _actual As Qs()      ' +0x110
        Private ReadOnly _previo As Qs()      ' +0x120
        Private ReadOnly _salida As Qs()      ' +0x130
        Private ReadOnly _guardado As Qs()    ' +0xE8, los primeros 0x30 B de cada entrada

        ''' <summary>`+0x1B4`: la próxima entrada va sin interpolar. El constructor lo deja en 1
        ''' (`0x1418A410A`).</summary>
        Friend SinInterpolar As Boolean = True
        ''' <summary>`+0x1B5`: la próxima salida va sin extrapolar. Idem.</summary>
        Friend SinExtrapolar As Boolean = True
        ''' <summary>`+0x194`: 1 en el constructor (`0x1418A40DE`); 0 si una partícula es NaN
        ''' (`0x1418A6B07`).</summary>
        Friend Valido As Boolean = True
        ''' <summary>`+0x198`: el índice del estado llamado EXACTAMENTE «Animate», −1 si no hay
        ''' (constructor `0x1418A40EA`, armado `0x1418A5496`; gana el último).</summary>
        Friend IndiceAnimate As Integer = -1
        ''' <summary>`+0x19C`: idem «Simulate» (`0x1418A54BA`).</summary>
        Friend IndiceSimulate As Integer = -1
        ''' <summary>`+0x1A0`: la fase del pasaje Animate→Simulate. −1 en el constructor
        ''' (`0x1418A40FE`) y al enganchar (`0x1418A0F03`).</summary>
        Friend Fase As Integer = -1
        ''' <summary>`+0x1B6`: el próximo `BeginAccess` recoloca los colisionables y resiembra la
        ''' transferencia (`0x141879516` lo prende, `0x1418A6906` lo apaga).</summary>
        Friend Teletransporte As Boolean
        ''' <summary>`+0x1B7`: el job asienta (`0x14187903E`). 0 en el constructor (`0x1418A410A`); lo
        ''' escribe `0x1418A1B60` desde `0x1406F145F` con «el actor es el jugador».</summary>
        Friend Asentar As Boolean

        ''' <summary>`+0x188`: el hueso raíz — de los huesos con nodo vivo, el de MENOS ancestros
        ''' (`0x1418A45B6` `0x141493630` + `0x1418A45BF` `jae`, gana el primero). −1 sin ninguno
        ''' (`0x1418A40D2`).</summary>
        Friend IndiceRaiz As Integer = -1
        ''' <summary>`+0x140`/`+0x150`/`+0x160`: el Qs del nodo raíz del último cuadro. El
        ''' constructor lo siembra con el transform que recibe (`0x1418A413A`, `0x14082B4D0`).</summary>
        Friend Raiz As Qs = Qs.Identidad
        ''' <summary>`+0x100`: `referencePose` en espacio de modelo, relativa a la raíz
        ''' (`0x1418A4E10`-`0x1418A4F79`). Vacío con raíz −1 (`0x1418A4D81`).</summary>
        Private _desdeRaiz As Qs() = Array.Empty(Of Qs)()
        ''' <summary>`+0x170..+0x178`: el origen del mundo que el constructor copia de
        ''' `0x143448450`/`54`/`58` (`0x1418A406D`-`0x1418A4099`). En la imagen del `.exe` vale
        ''' `(0, 0, 0)`; el juego lo corre en tiempo de ejecución, y la app no tiene mundo.</summary>
        Friend Origen As Vector128(Of Single) = Vector128(Of Single).Zero

        ''' <summary>`hclTransformSet +0x20`: la inversa traspuesta de cada transform LEÍDO, que
        ''' `0x1418EE230` recalcula en cada `BeginAccess` (`0x1418A6891`). Del tamaño del set
        ''' (`[+0x18]`, `0x1418EE281`).</summary>
        Friend Inversas As Mat4() = Array.Empty(Of Mat4)()

        ''' <summary>Los nodos que el constructor CREA (`0x1418A4643`-`0x1418A46B1`): un `NiNode` nuevo
        ''' (`0x141657DE0` 0x140 B + `0x1416BE500`) con el nombre del hueso (`0x1416BD250`), para cada
        ''' hueso sin nodo vivo que escribe un `hclSimpleMeshBoneDeformOperator`. ⛔ Viven con el
        ''' `BSTransformSet`, no con el esqueleto.</summary>
        Private _creado As Boolean() = Array.Empty(Of Boolean)()
        ''' <summary>El local del nodo creado (`node+0x30..+0x6C`): identidad al nacer, y lo que
        ''' escriba el cierre.</summary>
        Private _creadoLocal As NiTransformDelMotor() = Array.Empty(Of NiTransformDelMotor)()
        ''' <summary>El padre NiNode que el CIERRE le dio a un nodo escrito que no tenía (−1 si no se
        ''' colgó): `0x1418A6F1D`-`0x1418A6F61` toma el nodo del padre HK (`vtbl[+0x20]`) y le cuelga el
        ''' hijo (`vtbl[+0x1D0]`, `AttachChild`). ⛔ Vale para los CREADOS y para los del esqueleto por
        ''' igual: el motor no distingue (GDFt5). El esqueleto de la app no se reestructura; la vista
        ''' de nodos reporta el padre y compone el mundo contra él.</summary>
        Private _padreColgado As Integer()
        ''' <summary>La vista de los nodos con los creados y los colgados adentro. Una por conjunto.</summary>
        Private ReadOnly _vista As New NodosConCreados(Me)

        ''' <summary>Scratch del cierre (los mundos escritos en esta pasada). Sin estado entre cuadros.</summary>
        Private ReadOnly _mundos As NiTransformDelMotor()
        ''' <summary>INSTRUMENTO: el mundo W que el cierre calculó para el hueso en la última pasada
        ''' que escribió (`0x1418A6CF4`-`0x1418A74C0`). Sólo lectura, para medir la capa.</summary>
        Friend Function MundoDelCierre(idx As Integer, ByRef w As NiTransformDelMotor) As Boolean
            If idx < 0 OrElse idx >= NumHuesos OrElse Not _tieneMundo(idx) OrElse Not Escritos(idx) Then Return False
            w = _mundos(idx)
            Return True
        End Function
        Private ReadOnly _tieneMundo As Boolean()

        Private _contador As Integer          ' +0x190
        Private _nPasos As Integer            ' +0x18C
        Private _paso As Single               ' +0x1A8
        Private _dtConsumido As Single        ' +0x1A4
        Private _sobranteViejo As Single      ' +0x1AC
        Private _sobranteNuevo As Single      ' +0x1B0

        ''' <summary>Huesos leídos que no resolvieron nodo en la última entrada (hueco declarado).</summary>
        Friend LeidosSinNodo As Integer

        ''' <summary>Con las máscaras ya armadas. `Crear` las arma del archivo; el gate las arma a mano.</summary>
        Friend Sub New(numHuesos As Integer, leidos As Boolean(), escritos As Boolean(),
                        escritosYPadres As Boolean(), padres As Integer())
            Me.NumHuesos = numHuesos
            Me.Leidos = leidos
            Me.Escritos = escritos
            Me.EscritosYPadres = escritosYPadres
            Me.Padres = padres
            Dim lista As New List(Of Integer)
            For i = 0 To numHuesos - 1
                If escritosYPadres(i) Then lista.Add(i)
            Next
            _entradas = lista.ToArray()
            ReDim _padreColgado(numHuesos - 1)
            Array.Fill(_padreColgado, -1)
            ReDim _mundos(numHuesos - 1) : ReDim _tieneMundo(numHuesos - 1)
            ReDim _actual(numHuesos - 1) : ReDim _previo(numHuesos - 1)
            ReDim _salida(numHuesos - 1) : ReDim _guardado(numHuesos - 1)
            ' ⛔ `_actual` (+0x110), `_previo` (+0x120) y `_salida` (+0x130) NO los construye nadie: el
            ' motor sólo reserva (`hkArray::reserve` 0x141277540 desde 0x1418A421D / 0x1418A4261 /
            ' 0x1418A42AA, que llama al asignador vtbl[+0x18]/[+0x28] y copia los 0 elementos viejos) y
            ' fija la cuenta (0x1418A4222 / 0x1418A4266 / 0x1418A42B2). MEDIDO con un hook de escritura
            ' sobre los tres arreglos enteros: ninguna escritura antes del primer `BeginAccess`
            ' (la primera es 0x14082B521 / 0x1418A65DE para +0x110 y 0x141483D4E para +0x120/+0x130,
            ' en la llamada 0). O sea que el contenido inicial es el del ASIGNADOR, no un valor del
            ' motor: la emulación da CERO (páginas nuevas) y se copia ese, que es lo que `ReDim` ya
            ' deja. La identidad que había acá no tenía cita (la de 0x1418A4D46…52 es otro arreglo,
            ' de elementos de 0x40 B) y difería del `.exe` emulado en las lanes 7-11 hasta la
            ' llamada 0.
            For i = 0 To numHuesos - 1
                _guardado(i) = Qs.Identidad   ' 0x1418A4D46…52
            Next
        End Sub

        ''' <summary>
        ''' Las máscaras — `0x1418A51B5`-`0x1418A542A`.
        ''' <para>```
        ''' tmp = usedTransformSets[0].usage del estado 0          ' 0x1418A51B5 (0x1418A9070)
        ''' tmp |= el de cada estado k ≥ 1, componente a componente ' 0x1418A51E0…2E9
        ''' +0x40 = tmp[0].written                                 ' 0x1418A52F4
        ''' +0x58 = tmp[0].read | tmp[1].read                       ' 0x1418A5307…33E
        ''' +0x58 |= padre de todo escrito cuyo padre NO es escrito ' 0x1418A5350…3A8
        ''' +0x70 = +0x40 | padre de todo escrito                   ' 0x1418A53BE…42A
        ''' ```</para>
        ''' </summary>
        Friend Shared Function Crear(estados As IList(Of Havok.Canon.Objects.HkObj_HclClothState),
                                     padres As IList(Of Integer), numHuesos As Integer) As ConjuntoDeTransforms
            If estados Is Nothing OrElse estados.Count = 0 OrElse numHuesos <= 0 Then Return Nothing
            Dim read0(numHuesos - 1) As Boolean, read1(numHuesos - 1) As Boolean, written0(numHuesos - 1) As Boolean
            For Each est In estados
                Dim acc = If(est Is Nothing OrElse est.UsedTransformSets Is Nothing OrElse est.UsedTransformSets.Count = 0,
                             Nothing, est.UsedTransformSets(0))
                Dim tr = acc?.TransformSetUsage?.PerComponentTransformTrackers
                If tr Is Nothing Then Continue For
                If tr.Count > 0 Then
                    OrBits(read0, tr(0).Read_)
                    OrBits(written0, tr(0).Written)
                End If
                If tr.Count > 1 Then OrBits(read1, tr(1).Read_)
            Next

            Dim par(numHuesos - 1) As Integer
            For i = 0 To numHuesos - 1
                par(i) = If(padres IsNot Nothing AndAlso i < padres.Count, padres(i), -1)
            Next

            Dim escritos = CType(written0.Clone(), Boolean())
            Dim leidos(numHuesos - 1) As Boolean
            For i = 0 To numHuesos - 1
                leidos(i) = read0(i) OrElse read1(i)
            Next
            For i = 0 To numHuesos - 1                                          ' 0x1418A5350
                If Not escritos(i) Then Continue For
                Dim p = par(i)
                If p = -1 OrElse p < 0 OrElse p >= numHuesos Then Continue For    ' 0x1418A537E cmp ax, -1
                If Not escritos(p) Then leidos(p) = True                        ' 0x1418A5398 / 0x1418A53A8
            Next
            Dim conPadres = CType(escritos.Clone(), Boolean())                  ' 0x1418A53C3
            For i = 0 To numHuesos - 1                                          ' 0x1418A53E0
                If Not escritos(i) Then Continue For
                Dim p = par(i)
                If p = -1 OrElse p < 0 OrElse p >= numHuesos Then Continue For
                conPadres(p) = True                                             ' 0x1418A542A
            Next
            Dim r = New ConjuntoDeTransforms(numHuesos, leidos, escritos, conPadres, par)
            ' 0x1418A5459-0x1418A54D0: TODOS los estados, `strcmp` exacto contra el nombre (`[state+0x10]`).
            For ie = 0 To estados.Count - 1
                Dim nm = estados(ie)?.Name
                If nm Is Nothing Then Continue For                              ' 0x1418A5484 je
                If String.Equals(nm, "Animate", StringComparison.Ordinal) Then  ' 0x142543800
                    r.IndiceAnimate = ie                                        ' 0x1418A5496
                ElseIf String.Equals(nm, "Simulate", StringComparison.Ordinal) Then   ' 0x142675CD8
                    r.IndiceSimulate = ie                                       ' 0x1418A54BA
                End If
            Next
            Return r
        End Function

        ' =========================================================================================
        ' UN BSTransformSet POR DEFINICIÓN — 0x1418A36F0 (llamada desde 0x1418A36C0 ← 0x1418A0AF9)
        ' =========================================================================================
        '
        ' ```
        ' para i en 0 .. clothData.transformSetDefinitions.count − 1       ' 0x1418A3719 / 0x1418A3A91 ([cd+0x40])
        '   nombre = transformSetDefinitions[i].name                         ' 0x1418A3751…75D ([def+0x10] & ~1)
        '   esqueleto = nulo
        '   si nombre: para cada hkaSkeleton del conjunto del paquete         ' 0x1418A3767…822 (BSClothExtraData+0x30)
        '     si strncmp(nombre, skel.name, strlen(skel.name)) = 0: esqueleto = skel (el primero; corta)
        '                                                                    ' 0x1418A37E4 / 0x1418A37F2 (IAT 0x142441590)
        '   tipo17 = boneOffset >> 6 de los hclSimpleMeshBoneDeformOperator  ' 0x1418A38A7…907
        '   ts = nulo
        '   si esqueleto y nodo raíz: ts = new BSTransformSet(0x1C0)         ' 0x1418A391C / 0x1418A3923 / 0x1418A3936
        '                             ctor(nodo, esqueleto, tipo17, …, instancia, referencia)   ' 0x1418A398C (0x1418A3E50)
        '   instancia.transformSets[i] = ts                                  ' 0x1418A39AA (0x1418C85F0: [inst+0x40][i])
        ' ```
        ' El conjunto de esqueletos lo llena la carga (`0x14189FFAB`-`0x1418A0088`): TODAS las variantes
        ' RAÍZ del `hkRootLevelContainer` de clase `hkaSkeleton` (`0x141483120` findObjectByType,
        ' clase `0x14334CC30`, nombre `0x142649338`).
        '
        ' ⛔ HUECOS DECLARADOS (el `.exe` no tiene un camino que transcribir):
        '   · sin esqueleto que case o sin nodo raíz, `0x1418C85F0` recibe NULO y hace `cmp word
        '     [r8+0xA], 0` sin guarda (`0x1418C85FF`): el motor revienta. La app no arma la prenda.
        '     M22: 808 de 808 definiciones del corpus casan con exactamente un esqueleto.
        '   · con MÁS de un esqueleto que case, gana el primero en el orden del `BSTSet` (por hash de
        '     puntero, `0x1418A3790`): no es reproducible. La app toma el primero en el orden del
        '     contenedor y lo dice en el log. M22: 0 en el corpus.

        ''' <summary>Las variantes raíz de clase `hkaSkeleton`, en el orden del contenedor
        ''' (`0x14189FFAB`-`0x1418A0088`, `0x141483120`: compara la clase del objeto —`0x141540300`— o,
        ''' sin ella, `className`, con `strcmp` `0x14133A520`).</summary>
        Friend Shared Function EsqueletosRaiz(graph As HkxObjectGraph_Class) As List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
            If graph Is Nothing Then Return r
            Dim raiz = Havok.Canon.Objects.HkObj_HkRootLevelContainer.Leer(graph, graph.GetRootObject())
            If raiz Is Nothing Then Return r
            For Each nv In raiz.NamedVariants
                Dim v = nv?.Variant
                If v Is Nothing Then Continue For
                Dim cls = If(String.IsNullOrEmpty(v.ClassName), nv.ClassName, v.ClassName)
                If Not String.Equals(cls, Havok.Canon.Objects.HkObj_HkaSkeleton.NombreDeClase, StringComparison.Ordinal) Then Continue For
                Dim s = Havok.Canon.Objects.HkObj_HkaSkeleton.Read(graph, v)
                If s IsNot Nothing Then r.Add(s)
            Next
            Return r
        End Function

        ''' <summary>`strncmp(definicion, esqueleto, strlen(esqueleto)) = 0` — `0x1418A37E4`
        ''' (`0x14125CE80`: 0 con puntero nulo) y `0x1418A37F2` (IAT `0x142441590` strncmp).</summary>
        Friend Shared Function CasaPorPrefijo(nombreDeLaDefinicion As String, nombreDelEsqueleto As String) As Boolean
            Dim a = If(nombreDeLaDefinicion Is Nothing, Array.Empty(Of Byte)(), System.Text.Encoding.Latin1.GetBytes(nombreDeLaDefinicion))
            Dim b = If(nombreDelEsqueleto Is Nothing, Array.Empty(Of Byte)(), System.Text.Encoding.Latin1.GetBytes(nombreDelEsqueleto))
            For k = 0 To b.Length - 1
                If If(k < a.Length, a(k), CByte(0)) <> b(k) Then Return False
            Next
            Return True
        End Function

        ''' <summary>Los esqueletos que casan con la definición, en el orden del contenedor. Vacío si
        ''' la definición no tiene nombre (`0x1418A3761 je`).</summary>
        Friend Shared Function EsqueletosDeLaDefinicion(esqueletos As IList(Of Havok.Canon.Objects.HkObj_HkaSkeleton),
                                                         nombreDeLaDefinicion As String) As List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
            If nombreDeLaDefinicion Is Nothing OrElse esqueletos Is Nothing Then Return r
            For Each s In esqueletos
                If s IsNot Nothing AndAlso CasaPorPrefijo(nombreDeLaDefinicion, s.Name) Then r.Add(s)
            Next
            Return r
        End Function

        ''' <summary>
        ''' La raíz y la pose sin nodo — el tramo del constructor `0x1418A4540`-`0x1418A4F79`.
        ''' <para>```
        ''' para cada hueso i con nodo vivo:                        ' 0x1418A4568 (GetObjectByName)
        '''   n = ancestros(i)                                      ' 0x1418A45B6 (0x141493630)
        '''   si n &lt; mínimo: mínimo = n; raíz = i                  ' 0x1418A45BF jae / 0x1418A45C7
        ''' si raíz ≠ −1:                                           ' 0x1418A4D7E
        '''   model = poseDeModelo(referencePose)                   ' 0x1418A447F (0x141491BA0 → 0x1414920E0)
        '''   +0x100[i] = model[raíz]⁻¹ ∘ model[i]                  ' 0x1418A4E40…F67
        ''' ```</para>
        ''' </summary>
        Friend Sub FijarRaiz(nodos As INodosDeTela, referencePose As IList(Of Single()))
            Dim minimo = UInteger.MaxValue                                      ' 0x1418A4510 r15d = −1
            IndiceRaiz = -1
            For i = 0 To NumHuesos - 1
                If nodos.Mundo(i) Is Nothing Then Continue For                  ' 0x1418A4573 je
                Dim n = 0UI
                Dim p = Padres(i)
                While p <> -1 AndAlso p >= 0 AndAlso p < NumHuesos               ' 0x141493651 cmp di, −1
                    n += 1UI
                    p = Padres(p)
                End While
                If n < minimo Then                                              ' 0x1418A45C2 jae
                    minimo = n
                    IndiceRaiz = i
                End If
            Next
            If Logger.Enabled Then
                Dim rq = IndiceRaiz, mq = minimo
                Logger.LogLazy(Function() $"[CLOTH-RAIZ] hueso raiz={rq} · ancestros={mq}")
            End If
            If IndiceRaiz = -1 OrElse referencePose Is Nothing Then Return

            Dim locales(NumHuesos - 1) As Qs
            For i = 0 To NumHuesos - 1
                locales(i) = Qs.Identidad
                If i >= referencePose.Count OrElse referencePose(i) Is Nothing Then Continue For
                Dim f = referencePose(i)
                locales(i).T = LeerLane(f, 0)
                locales(i).R = LeerLane(f, 4)
                locales(i).S = LeerLane(f, 8)
            Next
            Dim modelo = Descomposicion.PoseDeModelo(locales, Padres)
            Dim inv = Descomposicion.InversaQs(modelo(IndiceRaiz))
            ReDim _desdeRaiz(NumHuesos - 1)
            For i = 0 To NumHuesos - 1
                _desdeRaiz(i) = Descomposicion.ComponerQs(inv, modelo(i))
            Next
        End Sub

        ''' <summary>
        ''' Los nodos que crea el constructor — `0x1418A45EE`-`0x1418A46B1`: si el hueso no tiene nodo
        ''' vivo y está en la lista de huesos de los `hclSimpleMeshBoneDeformOperator` (tipo 17) de
        ''' `clothData.operators` (`0x1418A38AA cmp [op+0x18], 0x11`; `triangleBonePairs[k].boneOffset
        ''' &gt;&gt; 6`, `0x1418A38C7`), se crea. ⛔ Después de <see cref="FijarRaiz"/>: el nodo creado no
        ''' entra en la elección de la raíz (`0x1418A4573 je 0x1418A45EE` salta ese tramo).
        ''' </summary>
        Friend Sub FijarCreados(nodos As INodosDeTela, huesosDeTipo17 As IEnumerable(Of Integer))
            ReDim _creado(NumHuesos - 1) : ReDim _creadoLocal(NumHuesos - 1)
            Dim lista As New HashSet(Of Integer)(If(huesosDeTipo17, Enumerable.Empty(Of Integer)()))
            For i = 0 To NumHuesos - 1
                If nodos.Mundo(i) IsNot Nothing OrElse Not lista.Contains(i) Then Continue For   ' 0x1418A4573 / 0x1418A4606
                _creado(i) = True
                _creadoLocal(i) = NiTransformDelMotor.DeTransform(New Transform_Class())   ' 0x1416BE500: identidad
                _creadoLocal(i).S = 1.0F
            Next
        End Sub

        ''' <summary>¿El hueso tiene un nodo creado por este conjunto?</summary>
        Friend Function EsCreado(i As Integer) As Boolean
            Return i >= 0 AndAlso i < _creado.Length AndAlso _creado(i)
        End Function

        ''' <summary>La vista de nodos que usan la entrada y el cierre: los vivos, los creados y los
        ''' colgados por el cierre.</summary>
        Private NotInheritable Class NodosConCreados
            Implements INodosDeTela
            Private ReadOnly _c As ConjuntoDeTransforms
            Friend Base As INodosDeTela
            Friend Sub New(c As ConjuntoDeTransforms)
                _c = c
            End Sub
            ''' <summary>El mundo del nodo creado, como lo deja `NiAVObject::UpdateWorldData`
            ''' (`0x1416C8AC0`): sin padre, el local; con padre, `0x140344820(W(padre), local)`. El nodo
            ''' nace con `+0x100` nulo y el bit 45 de `+0x108` apagado (`0x1416C9282`/`0x1416C9321`),
            ''' así que nunca toma las otras dos ramas.</summary>
            Public Function Mundo(indice As Integer) As Transform_Class Implements INodosDeTela.Mundo
                If Not _c.EsCreado(indice) Then Return Base.Mundo(indice)
                Dim p = _c._padreColgado(indice)
                Dim wp = If(p < 0, Nothing, Mundo(p))
                Dim padre As NiTransformDelMotor? = Nothing
                If wp IsNot Nothing Then padre = NiTransformDelMotor.DeTransform(wp)
                Return ActualizarMundoDelNodo(padre, _c._creadoLocal(indice), False).ATransform()
            End Function
            Public Function IndiceDelPadreNi(indice As Integer) As Integer Implements INodosDeTela.IndiceDelPadreNi
                Return If(_c.EsCreado(indice), _c._padreColgado(indice), Base.IndiceDelPadreNi(indice))
            End Function
            Public Function MundoDelPadreNi(indice As Integer) As Transform_Class Implements INodosDeTela.MundoDelPadreNi
                If Not _c.EsCreado(indice) Then Return Base.MundoDelPadreNi(indice)
                Dim p = _c._padreColgado(indice)
                Return If(p < 0, Nothing, Mundo(p))
            End Function
            Public Sub EscribirLocal(indice As Integer, local As NiTransformDelMotor) Implements INodosDeTela.EscribirLocal
                If _c.EsCreado(indice) Then
                    _c._creadoLocal(indice) = local
                Else
                    Base.EscribirLocal(indice, local)
                End If
            End Sub
            Public Sub Colgar(indice As Integer, padre As Integer) Implements INodosDeTela.Colgar
                _c._padreColgado(indice) = padre
                If Not _c.EsCreado(indice) Then Base.Colgar(indice, padre)
            End Sub
        End Class

        Private Function Vista(nodos As INodosDeTela) As INodosDeTela
            _vista.Base = nodos
            Return _vista
        End Function

        ''' <summary>
        ''' `NiAVObject::UpdateWorldData` — `0x1416C8AC0` (vtable de `NiAVObject` `0x1426851F0` y de
        ''' `NiNode` `0x1426849B8`, ranura 52), sin datos de update (`rdx = 0`, `0x1416C8ACC je`).
        ''' <para>```
        ''' +0xC0..+0xFC = +0x70..+0xAC                              ' 0x1416C8AF7…BC9 (el mundo previo)
        ''' si padre (+0x28) nulo:          mundo = local (+0x30)     ' 0x1416C8BD6 je → 0x1416C8D66
        ''' si bit 45 de +0x108:            mundo = mundo del padre   ' 0x1416C8BE7 shr 0x2d / 0x1416C8BF3
        ''' si no:                          mundo = 0x140344820(W(padre), local)   ' 0x1416C8CAD
        ''' ```</para>
        ''' <para>⛔ La rama con `+0x100` (el objeto de colisión, `0x1416C8AF0 jmp [rax+0x148]`) sólo
        ''' corre con datos de update; el nodo creado la tiene nula (`0x1416C9282`).</para>
        ''' </summary>
        Friend Shared Function ActualizarMundoDelNodo(padre As NiTransformDelMotor?, local As NiTransformDelMotor,
                                                      copiaElDelPadre As Boolean) As NiTransformDelMotor
            If Not padre.HasValue Then Return local
            If copiaElDelPadre Then Return padre.Value
            Return ComponerNiTransform(padre.Value, local)
        End Function

        ''' <summary>
        ''' `NiTransform::operator*` — `0x140344820(P = rcx, out = rdx, L = r8)`:
        ''' <para>```
        ''' out.Fk = (L.Fk.y·P.F1 + L.Fk.x·P.F0) + L.Fk.z·P.F2       ' 0x14034488E…8D9 / 8B9…8FF / 8C5…95E
        ''' t      = (L.t.y·P.F1 + L.t.x·P.F0) + L.t.z·P.F2           ' 0x1403448A5…936 (lanes enteras, w = L.s)
        ''' t      = (t AND (1,1,1,0)) OR (L.t AND (0,0,0,1))         ' 0x14034494A / 0x140344929 / 0x140344952
        ''' t      = t · P.s + (P.t AND (1,1,1,0))                    ' 0x140344966 / 0x140344955 / 0x140344972
        ''' out+0x3C = 1, pisado por out+0x30 = t                     ' 0x140344975 / 0x14034497C
        ''' ```</para>
        ''' La lane w de la traslación en memoria es la escala (`+0x3C`): acá vive en `S`.
        ''' </summary>
        Friend Shared Function ComponerNiTransform(p As NiTransformDelMotor, l As NiTransformDelMotor) As NiTransformDelMotor
            Dim r As NiTransformDelMotor
            r.F0 = FilaNi(l.F0, p)
            r.F1 = FilaNi(l.F1, p)
            r.F2 = FilaNi(l.F2, p)
            Dim l3 = l.T.WithElement(Simd.LaneW, l.S)
            Dim p3 = p.T.WithElement(Simd.LaneW, p.S)
            Dim sinW = Vector128.Create(&HFFFFFFFFUI, &HFFFFFFFFUI, &HFFFFFFFFUI, 0UI).AsSingle()   ' 0x142924BE0
            Dim soloW = Vector128.Create(0UI, 0UI, 0UI, &HFFFFFFFFUI).AsSingle()                  ' 0x142924E30
            Dim t = FilaNi(l3, p)
            t = Vector128.BitwiseOr(Vector128.BitwiseAnd(t, sinW), Vector128.BitwiseAnd(soloW, l3))
            t = Vector128.Multiply(t, Simd.BcastW(p3))
            t = Vector128.Add(t, Vector128.BitwiseAnd(sinW, p3))
            r.T = t.WithElement(Simd.LaneW, 0.0F)
            r.S = t.GetElement(Simd.LaneW)
            Return r
        End Function

        ''' <summary>`(v.y·P.F1 + v.x·P.F0) + v.z·P.F2` con las cuatro lanes de cada fila.</summary>
        Private Shared Function FilaNi(v As Vector128(Of Single), p As NiTransformDelMotor) As Vector128(Of Single)
            Dim a = Vector128.Multiply(Simd.BcastY(v), p.F1)
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastX(v), p.F0))
            Return Vector128.Add(a, Vector128.Multiply(Simd.BcastZ(v), p.F2))
        End Function

        Private Shared Function LeerLane(f As Single(), o As Integer) As Vector128(Of Single)
            Dim g = Function(k As Integer) If(o + k < f.Length, f(o + k), 0.0F)
            Return Vector128.Create(g(0), g(1), g(2), g(3))
        End Function

        ''' <summary>`0x1418A6070`: `[+0x1B4] = [+0x1B5] = 1` (`mov word ptr [rcx+0x1B4], 0x101`).</summary>
        Friend Sub ReiniciarBanderas()
            SinInterpolar = True
            SinExtrapolar = True
        End Sub

        ''' <summary>`0x1418A79F0`: los dos índices ≠ −1 y `bAnimClothLOD` (`0x142F4FB88`).</summary>
        Friend Function PuedeCambiarDeEstado(bAnimClothLOD As Boolean) As Boolean
            Return IndiceSimulate <> -1 AndAlso IndiceAnimate <> -1 AndAlso bAnimClothLOD
        End Function

        Private Shared Sub OrBits(destino As Boolean(), bf As Havok.Canon.Objects.HkObj_HkBitField)
            Dim w = bf?.Storage?.Words
            If w Is Nothing Then Return
            For i = 0 To destino.Length - 1
                If (i >> 5) >= w.Count Then Exit For
                If ((w(i >> 5) >> (i And 31)) And 1UI) <> 0UI Then destino(i) = True
            Next
        End Sub

        ''' <summary>`0x1418A5DF0`: el reparto del cuadro, y el contador a cero.</summary>
        Friend Sub CargarCuadro(c As CuadroDeTela)
            _paso = c.Paso                     ' 0x1418A5DFA
            _dtConsumido = c.DtConsumido       ' 0x1418A5E02
            _sobranteViejo = c.SobranteViejo   ' 0x1418A5E0A
            _sobranteNuevo = c.SobranteNuevo   ' 0x1418A5E12
            _nPasos = c.NPasos                 ' 0x1418A5E1A
            _contador = 0                      ' 0x1418A5E20
        End Sub

        ' =========================================================================================
        ' TtBeginAccess BoneMap -> TransformSet — 0x1418A6080
        ' =========================================================================================

        Friend Sub AbrirAcceso(conjunto As Mat4(), nodos As INodosDeTela,
                               Optional prenda As PrendaSimulada = Nothing)
            _contador += 1                                                      ' 0x1418A60E4
            If _contador = 1 Then                                               ' 0x1418A60ED / jne
                LeerEntrada(conjunto, Vista(nodos), prenda)
            End If

            If SinInterpolar Then                                               ' 0x1418A66C9 cmp [+0x1b4]
                For i = 0 To NumHuesos - 1                                      ' 0x1418A66F0
                    If Leidos(i) AndAlso i < conjunto.Length Then conjunto(i) = Descomposicion.AMatriz(_actual(i))   ' 0x1418A671B
                Next
            Else
                Interpolar(conjunto)
            End If

            CalcularInversas(conjunto)                                          ' 0x1418A6891 (0x1418EE230)

            ' 0x1418A6896: el teletransporte que prendió el pasaje a Simulate.
            If Teletransporte Then
                If prenda IsNot Nothing Then
                    prenda.RecolocarColisionables()                             ' 0x1418A68C6 (0x1418C94D0)
                    prenda.SembrarTransferencia()                               ' 0x1418A68EF (0x1418C98F0)
                End If
                Teletransporte = False                                          ' 0x1418A6906
            End If
        End Sub

        ''' <summary>
        ''' La lectura del cuadro — `0x1418A6132`-`0x1418A66AD`, sólo en el primer paso.
        ''' <para>```
        ''' raízT, raízR, raízS = 0, (0,0,0,1), 1                   ' 0x1418A6138 / 0x1418A6148 / 0x1418A6155
        ''' si raíz ≠ −1:                                           ' 0x1418A6161
        '''   raízT = nodo.T (w=0); raízR = normalizar(quat(nodo)); raízS = 1   ' 0x1418A617C…209
        '''   d² = |raízT − +0x140|²;  ang = ángulo(conj(+0x150) ⊗ raízR normalizado)  ' 0x1418A6230…309
        '''   si d² &gt; fMaxRootDistance² o ang &gt; fMaxRootAngle:  ' 0x1418A631D ja / 0x1418A6322 jbe
        '''     delta = (raízT−origen, raízR, 1) ∘ (+0x140−origen, +0x150, 1)⁻¹      ' 0x1418A6384 (0x1418C99C0)
        '''     cada sim: teletransportar(delta, 1)                ' 0x1418C9A1F (0x1418CA380)
        '''     cada set: rebasar(delta, +0x58)                     ' 0x1418A63BC (0x1418A5930)
        '''   +0x140/+0x150/+0x160 = raízT, raízR, raízS            ' 0x1418A63CD…DC
        ''' para cada hueso leído:                                  ' 0x1418A6410 / 0x1418A6431
        '''   con nodo: actual = qs(nodo con T − origen, S = 1)     ' 0x1418A6458…64CB
        '''   sin nodo: actual = (raízT, raízR, raízS) ∘ +0x100[i]; actual.T −= origen   ' 0x1418A64D5…65CC
        '''   actual.S = 1                                          ' 0x1418A65DE
        '''   si no +0x1B4 y todavía no rápido:                     ' 0x1418A65E3 / 0x1418A65F9
        '''     rápido = +0x1B6 ≠ 0  o  |actual.T − previo.T|² &gt; (50·69,99125)²·(sobrante+dt)²   ' 0x1418A6610…670
        ''' si rápido: setStiffnessMode(2, 1, 1)                    ' 0x1418A66A8 (0x1418C6540)
        ''' ```</para>
        ''' </summary>
        Private Sub LeerEntrada(conjunto As Mat4(), nodos As INodosDeTela, prenda As PrendaSimulada)
            Dim raizT = Vector128(Of Single).Zero                                ' 0x1418A6148 xorps
            Dim raizR = Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)                 ' 0x1418A6138 (0x142F3C730)
            Dim raizS = Vector128.Create(1.0F)                                   ' 0x1418A6155 (0x142F3C560)
            Dim origenSinW = Origen.WithElement(Simd.LaneW, 0.0F)                ' 0x1418A60FC andps (0x142924BE0)

            Dim wRaiz = If(IndiceRaiz <> -1, nodos.Mundo(IndiceRaiz), Nothing)
            If wRaiz IsNot Nothing Then
                Dim ni = NiTransformDelMotor.DeTransform(wRaiz)
                ni.S = 1.0F                                                     ' 0x1418A6196
                Dim q = QsDelNodo(ni)                                           ' 0x1418A61B0…209: la misma cuenta
                raizT = q.T                                                     ' 0x1418A61DD andps
                raizR = q.R
                ' ⛔ NO es 1: el 1,0 de `0x1418A6196` lo pisa `0x1418A61AC` al copiar `[node+0xA0..AF]`
                ' (la escala del nodo vive en `+0xAC`), y `0x1418A61F0 shufps 0xFF` difunde ESA lane
                ' (GDFt3a). Es la escala con que se compone la raíz de los huesos sin nodo.
                raizS = Vector128.Create(NiTransformDelMotor.DeTransform(wRaiz).S)

                Dim dif = Vector128.Subtract(raizT, Raiz.T)                     ' 0x1418A6234
                dif = Vector128.Multiply(dif, dif)                              ' 0x1418A6245
                Dim d2 = (dif.GetElement(1) + dif.GetElement(0)) + dif.GetElement(2)   ' 0x1418A6255…263

                ' conj(viejo) ⊗ nuevo — 0x1418A6266…2CB
                Dim viejo = Raiz.R
                Dim cr = Vector128.Subtract(Vector128.Multiply(Vector128.Shuffle(raizR, Vector128.Create(1, 2, 0, 3)), viejo),
                                            Vector128.Multiply(Vector128.Shuffle(viejo, Vector128.Create(1, 2, 0, 3)), raizR))
                Dim v = Vector128.Shuffle(cr, Vector128.Create(1, 2, 0, 3))
                v = Vector128.Subtract(v, Vector128.Multiply(Simd.BcastW(raizR), viejo))
                v = Vector128.Add(v, Vector128.Multiply(Simd.BcastW(viejo), raizR))
                Dim qRel = v.WithElement(Simd.LaneW, Simd.Hsum4(Vector128.Multiply(viejo, raizR)).GetElement(3))
                qRel = Vector128.Multiply(Simd.RsqrtNewton(Simd.Hsum4(Vector128.Multiply(qRel, qRel))), qRel)   ' 0x1418A62CF…302
                Dim ang = Cuaternion.Angulo(qRel)                               ' 0x1418A6309 (0x14135FA20)

                Dim lim = HavokPhysicsSettings.MaxRootDistanceBeforeTeleport    ' 0x142F4FBD0 = 100
                If d2 > lim * lim OrElse ang > HavokPhysicsSettings.MaxRootAngleBeforeTeleport Then   ' 0x142F4FBE8 = π/2
                    Dim a As Qs, b As Qs
                    a.T = Vector128.Subtract(Raiz.T, origenSinW) : a.R = Raiz.R : a.S = Vector128.Create(1.0F)   ' 0x1418A6346…377
                    b.T = Vector128.Subtract(raizT, origenSinW) : b.R = raizR : b.S = Vector128.Create(1.0F)     ' 0x1418A6363…380
                    Dim delta = Descomposicion.DeltaDeTeletransporte(a, b)      ' 0x1418C99E5 (0x1418CA1E0)
                    If Logger.Enabled Then
                        Dim dq = d2, aq = ang
                        Logger.LogLazy(Function() $"[CLOTH-TELEPORT] d²={dq:0.###} ang={aq:0.#####}")
                    End If
                    If prenda IsNot Nothing Then prenda.Teletransportar(delta, True)   ' 0x1418C9A1F, byte 1 (0x1418A637B)
                    ' 0x1418A6389-0x1418A63CB: TODOS los BSTransformSet de la instancia
                    ' (`[inst+0x40]`, cuenta `[inst+0x48]`), en orden, con la máscara de lectura de
                    ' ESTE (`lea r8, [rdi+0x58]`, 0x1418A63B3).
                    Dim lista = If(prenda Is Nothing, Nothing, prenda.Conjuntos)
                    If lista Is Nothing Then
                        ' INSTRUMENTO (gates sin instancia): el motor siempre tiene `[ts+0x180]`.
                        RebasarConjunto(delta, conjunto, Leidos, Nothing)
                    Else
                        Dim filas = prenda.FilasDeTransferencia
                        Dim sets = prenda.TransformSets
                        For s = 0 To lista.Length - 1                           ' 0x1418A63B0
                            If lista(s) Is Nothing OrElse s >= sets.Length Then Continue For
                            lista(s).RebasarConjunto(delta, sets(s), Leidos, filas)   ' 0x1418A63BC (0x1418A5930)
                        Next
                    End If
                End If
                Raiz.T = raizT : Raiz.R = raizR : Raiz.S = raizS                ' 0x1418A63CD…DC
            End If

            LeidosSinNodo = 0
            Dim rapido = False                                                  ' 0x1418A63E4 xor r13b
            Dim tope = 50.0F * 69.99125F                                        ' 0x142F4FC00 · 0x142483E4C
            For i = 0 To NumHuesos - 1                                          ' 0x1418A6410
                If Not Leidos(i) Then Continue For                              ' 0x1418A6431
                Dim w = nodos.Mundo(i)
                If w IsNot Nothing Then
                    Dim ni = NiTransformDelMotor.DeTransform(w)
                    ni.S = 1.0F                                                 ' 0x1418A647C
                    ni.T = Vector128.Create(ni.T.GetElement(0) - Origen.GetElement(0),
                                            ni.T.GetElement(1) - Origen.GetElement(1),
                                            ni.T.GetElement(2) - Origen.GetElement(2), 0.0F)   ' 0x1418A6495…4C5
                    _actual(i) = QsDelNodo(ni)                                  ' 0x1418A64CB (0x14082B4D0)
                ElseIf i < _desdeRaiz.Length Then
                    Dim r As Qs
                    r.T = raizT : r.R = raizR : r.S = raizS
                    _actual(i) = Descomposicion.ComponerQs(r, _desdeRaiz(i))    ' 0x1418A64D5…65B8
                    _actual(i).T = Vector128.Subtract(_actual(i).T, origenSinW) ' 0x1418A65C8
                    LeidosSinNodo += 1
                Else
                    ' Raíz −1: el motor lee `+0x100` sin llenar. No hay de dónde leer.
                    LeidosSinNodo += 1
                    Continue For
                End If
                _actual(i).S = Vector128.Create(1.0F)                           ' 0x1418A65DE

                If SinInterpolar OrElse rapido Then Continue For                ' 0x1418A65F7 / 0x1418A65FC
                If Teletransporte Then                                          ' 0x1418A6610
                    rapido = True
                Else
                    Dim dt = _sobranteViejo + _dtConsumido                      ' 0x1418A662E/636
                    Dim umbral = ((tope * tope) * dt) * dt                      ' 0x1418A663E/64A/651
                    Dim dd = Vector128.Subtract(_actual(i).T, _previo(i).T)     ' 0x1418A6646
                    dd = Vector128.Multiply(dd, dd)
                    Dim d2 = (dd.GetElement(1) + dd.GetElement(0)) + dd.GetElement(2)   ' 0x1418A6655…66A
                    If d2 > umbral Then rapido = True                           ' 0x1418A666D comiss / jbe
                End If
            Next
            If rapido AndAlso prenda IsNot Nothing Then prenda.ModoDeRigidezPorHuesoRapido()   ' 0x1418A66A8
            If rapido AndAlso Logger.Enabled Then Logger.LogLazy(Function() "[CLOTH-RAPIDO] setStiffnessMode(2, 1, 1)")
        End Sub

        ''' <summary>
        ''' `0x1418A5930(ts, delta, máscara)` — el `BSTransformSet` llevado por el delta del
        ''' teletransporte.
        ''' <para>```
        ''' +0x120[i] = delta ∘ +0x120[i]   si el bit i de la máscara (nula: todos)  ' 0x1418A5952…A93 (0x1418A5964)
        ''' +0xE8[k]  = delta ∘ +0xE8[k]    todas                       ' 0x1418A5A99…BC9
        ''' si +0x180: M = matriz(delta); cada sim con transferencia:  ' 0x1418A5BCF / 0x1418A5BE4
        '''   F[transformIndex].k = ((y·M1 + x·M0) + z·M2) + w·M3     ' 0x1418A5C60…D7B
        ''' ```</para>
        ''' <para>⛔ La fila es la de ESTE set (`[rsi+0x10]`, 0x1418A5C81) en el índice
        ''' `transferMotionData.transformIndex` (`[data+0x154]`, 0x1418A5C7A): el motor NO mira
        ''' `transformSetIndex` acá. `filasDeTransferencia` trae un `transformIndex` por cada sim-cloth
        ''' de la instancia con datos y `transferMotionEnabled` (0x1418A5C3A / 0x1418A5C43 / 0x1418A5C5A),
        ''' en el orden de `[inst+0x20]`.</para>
        ''' <para>⛔ HUECOS DECLARADOS: el motor indexa sin cota —la fila fuera del set y el bit de la
        ''' máscara del llamador más allá de sus palabras son memoria ajena—; la app no escribe esa fila
        ''' y lee ese bit como apagado.</para>
        ''' </summary>
        Friend Sub RebasarConjunto(delta As Qs, conjunto As Mat4(), mascara As Boolean(), filasDeTransferencia As IList(Of Integer))
            For i = 0 To NumHuesos - 1                                          ' 0x1418A5952 [rcx+0x128]
                Dim bit = mascara Is Nothing OrElse (i < mascara.Length AndAlso mascara(i))   ' 0x1418A5961…980
                If bit Then _previo(i) = Descomposicion.ComponerQs(delta, _previo(i))
            Next
            For Each idx In _entradas
                _guardado(idx) = Descomposicion.ComponerQs(delta, _guardado(idx))
            Next
            If filasDeTransferencia Is Nothing OrElse filasDeTransferencia.Count = 0 OrElse conjunto Is Nothing Then Return
            Dim m = Descomposicion.AMatriz(delta)                               ' 0x1418A5BE4 (0x141539460)
            For Each fila In filasDeTransferencia                               ' 0x1418A5C32
                If fila < 0 OrElse fila >= conjunto.Length Then Continue For
                conjunto(fila) = FilaDeTransferencia(conjunto(fila), m)
            Next
        End Sub

        ''' <summary>`0x1418A5C89`-`0x1418A5D7B`: cada fila `a.Fk` pasa a
        ''' `((a.Fk.y·M1 + a.Fk.x·M0) + a.Fk.z·M2) + a.Fk.w·M3`.</summary>
        Friend Shared Function FilaDeTransferencia(a As Mat4, m As Mat4) As Mat4
            Dim r As Mat4
            r.F0 = FilaPorM(a.F0, m)                                            ' 0x1418A5D4D…D77 → +0x00
            r.F1 = FilaPorM(a.F1, m)                                            ' 0x1418A5CA7…CFB → +0x10
            r.F2 = FilaPorM(a.F2, m)                                            ' 0x1418A5CAB…D1A → +0x20
            r.F3 = FilaPorM(a.F3, m)                                            ' 0x1418A5CF5…D6C → +0x30
            Return r
        End Function

        Private Shared Function FilaPorM(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim a = Vector128.Multiply(Simd.BcastY(v), m.F1)
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastX(v), m.F0))
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastZ(v), m.F2))
            Return Vector128.Add(a, Vector128.Multiply(Simd.BcastW(v), m.F3))
        End Function

        ''' <summary>
        ''' `0x1418EE230(ts, máscara +0x58)`:
        ''' <para>```
        ''' inversas.count = transforms.count                         ' 0x1418EE247…281
        ''' para cada i con el bit i de la máscara:                    ' 0x1418EE2A0…2BB
        '''   inv[i] = inversaRígida(transforms[i])                    ' 0x1418EE2CF (0x141298100)
        '''   inv[i] = traspuesta(inv[i])                              ' 0x1418EE2DB (0x1415397F0)
        '''   inv[i].fila3 = (0, 0, 0, 1)                              ' 0x1418EE2E4/EB (0x142F3C730)
        ''' ```</para>
        ''' </summary>
        Private Sub CalcularInversas(conjunto As Mat4())
            If Inversas.Length <> conjunto.Length Then ReDim Preserve Inversas(conjunto.Length - 1)
            For i = 0 To conjunto.Length - 1
                If i >= NumHuesos OrElse Not Leidos(i) Then Continue For
                Dim inv = Descomposicion.Transponer4(Mat4.InversaRigida(conjunto(i)))
                inv.F3 = Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)
                Inversas(i) = inv
            Next
        End Sub

        Private Sub Interpolar(conjunto As Mat4())

            Dim alfa = (CSng(_contador) * _paso) / (_sobranteViejo + _dtConsumido)   ' 0x1418A673A…766
            Dim vAlfa = Vector128.Create(alfa)
            Dim unoMenos = Vector128.Subtract(Vector128.Create(1.0F), vAlfa)    ' 0x1418A67B6…C1
            For i = 0 To NumHuesos - 1                                          ' 0x1418A6780
                If Not Leidos(i) OrElse i >= conjunto.Length Then Continue For
                Dim p = _previo(i), a = _actual(i)
                Dim q As Qs
                q.S = Vector128.Add(Vector128.Multiply(Vector128.Subtract(a.S, p.S), vAlfa), p.S)   ' 0x1418A67C8…D9
                q.T = Vector128.Add(Vector128.Multiply(Vector128.Subtract(a.T, p.T), vAlfa), p.T)   ' 0x1418A67E2…ED
                Dim d = Simd.Hsum4(Vector128.Multiply(p.R, a.R))                ' 0x1418A67F5…818
                Dim signo = Vector128.BitwiseAnd(Vector128.LessThan(d, Vector128(Of Single).Zero),
                                                 Vector128.Create(-0.0F))       ' 0x1418A681B…824
                Dim alfaS = Vector128.Xor(vAlfa, signo)                         ' 0x1418A6829
                Dim r = Vector128.Add(Vector128.Multiply(unoMenos, p.R),
                                      Vector128.Multiply(alfaS, a.R))           ' 0x1418A67FF / 0x1418A682C / 0x1418A6834
                q.R = Vector128.Multiply(Simd.RsqrtNewton(Simd.Hsum4(Vector128.Multiply(r, r))), r)   ' 0x1418A6837…86E
                conjunto(i) = Descomposicion.AMatriz(q)                         ' 0x1418A6875
            Next
        End Sub

        ''' <summary>
        ''' `0x14082B4D0`: el cuaternión del `NiMatrix3` (`0x1416CD820`, en orden `(w,x,y,z)`) pasado
        ''' a `(x,y,z,w)` y normalizado con rsqrt + Newton sin guarda; la traslación con `w = 0`; la
        ''' escala difundida a las cuatro lanes.
        ''' </summary>
        Friend Shared Function QsDelNodo(ni As NiTransformDelMotor) As Qs
            Dim nq = NiCuaternionDeMatriz(ni)                                   ' 0x14082B4E5
            Dim q = Vector128.Create(nq.GetElement(1), nq.GetElement(2), nq.GetElement(3), nq.GetElement(0))               ' 0x14082B4EA…51D
            q = Vector128.Multiply(Simd.RsqrtNewton(Simd.Hsum4(Vector128.Multiply(q, q))), q)   ' 0x14082B525…55C
            Dim r As Qs
            r.R = q
            r.T = ni.T.WithElement(Simd.LaneW, 0.0F)                            ' 0x14082B567/73
            r.S = Vector128.Create(ni.S)                                        ' 0x14082B576
            Return r
        End Function

        Private Shared Function E(ni As NiTransformDelMotor, f As Integer, c As Integer) As Single
            Return If(f = 0, ni.F0, If(f = 1, ni.F1, ni.F2)).GetElement(c)
        End Function

        ''' <summary>
        ''' `NiMatrix3 → NiQuaternion` — `0x1416CD820`. Devuelve `{w, x, y, z}` en las lanes 0..3.
        ''' <para>Traza positiva (`0x1416CD84C`): `w = √(tr+1)·0,5`, `k = 0,5/√(tr+1)`,
        ''' `x = (m12−m21)·k`, `y = (m20−m02)·k`, `z = (m01−m10)·k`. Si no, la rama del mayor de la
        ''' diagonal con la tabla `{1,2,0}` de `0x142F4C600`.</para>
        ''' </summary>
        Friend Shared Function NiCuaternionDeMatriz(ni As NiTransformDelMotor) As Vector128(Of Single)
            Dim tr = (E(ni, 0, 0) + E(ni, 1, 1)) + E(ni, 2, 2)                              ' 0x1416CD83E / 0x1416CD848
            Dim r0, r1, r2, r3 As Single
            If tr > 0.0F Then                                                   ' 0x1416CD84C comiss / jbe
                Dim s = Simd.SqrtExacta(tr + 1.0F)                              ' 0x1416CD851/61
                r0 = s * 0.5F                                                 ' 0x1416CD868
                Dim k = 0.5F / s                                                ' 0x1416CD86C
                r1 = (E(ni, 1, 2) - E(ni, 2, 1)) * k                                  ' 0x1416CD874…7E
                r2 = (E(ni, 2, 0) - E(ni, 0, 2)) * k                                  ' 0x1416CD887…91
                r3 = (E(ni, 0, 1) - E(ni, 1, 0)) * k                                  ' 0x1416CD89A…A4
                Return Vector128.Create(r0, r1, r2, r3)
            End If
            Dim i = 0                                                           ' 0x1416CD8B8
            If E(ni, 1, 1) > E(ni, 0, 0) Then i = 1                                     ' 0x1416CD8BF comiss / cmova
            If E(ni, 2, 2) > E(ni, i, i) Then i = 2                                     ' 0x1416CD8D9 comiss / cmova
            Dim j = If(i = 2, 0, i + 1)                                         ' tabla 0x142F4C600
            Dim k2 = If(j = 2, 0, j + 1)
            Dim s2 = Simd.SqrtExacta(((E(ni, i, i) - E(ni, j, j)) - E(ni, k2, k2)) + 1.0F)  ' 0x1416CD8F6…93E
            Dim inv = 0.5F / s2                                                 ' 0x1416CD949
            ' las componentes vectoriales van en las lanes 1..3 = (x, y, z); la 0 = w
            Dim v(2) As Single
            v(i) = s2 * 0.5F                                                    ' 0x1416CD945/4D
            r0 = (E(ni, j, k2) - E(ni, k2, j)) * inv                            ' 0x1416CD951…6B
            v(j) = (E(ni, j, i) + E(ni, i, j)) * inv                            ' 0x1416CD96F…85
            v(k2) = (E(ni, i, k2) + E(ni, k2, i)) * inv                         ' 0x1416CD98D…A4
            Return Vector128.Create(r0, v(0), v(1), v(2))
        End Function

        ''' <summary>`NiQuaternion {w,x,y,z} → NiMatrix3` — `0x1403C59C0`, con el orden de sumas
        ''' de cada elemento.</summary>
        Friend Shared Sub NiMatrizDeCuaternion(w As Single, x As Single, y As Single, z As Single,
                                               ByRef f0 As Vector128(Of Single), ByRef f1 As Vector128(Of Single),
                                               ByRef f2 As Vector128(Of Single))
            Dim z2 = z + z, y2 = y + y, x2 = x + x                              ' 0x1403C59F3/A11/A1D
            Dim zz = z * z2, wz = w * z2, wy = w * y2, wx = w * x2              ' 0x1403C5A22/2B/2F/37
            Dim xx = x * x2, yy = y * y2, xy = x * y2, xz = x * z2, yz = y * z2 ' 0x1403C5A40/44/48/50/5E
            Dim zzMasXx = zz + xx                                               ' 0x1403C5A55
            Dim zzMasYy = zz + yy                                               ' 0x1403C5A5A
            Dim yyMasXx = yy + xx                                               ' 0x1403C5A6B
            f0 = Vector128.Create(1.0F - zzMasYy, xy + wz, xz - wy, 0.0F)       ' 0x1403C5A77/7E/93
            f1 = Vector128.Create(xy - wz, 1.0F - zzMasXx, yz + wx, 0.0F)       ' 0x1403C5A82/C2/9B→AA
            f2 = Vector128.Create(xz + wy, yz - wx, 1.0F - yyMasXx, 0.0F)       ' 0x1403C5A9B/A5/CD
        End Sub

        ' =========================================================================================
        ' TtBSTransformSet::notifyEndAccess — 0x1418A6980
        ' =========================================================================================

        ''' <summary>Devuelve cuántos nodos escribió (0 si no era el último paso o si hubo NaN).</summary>
        Friend Function CerrarAcceso(conjunto As Mat4(), sims As IList(Of Instancia),
                                     nodosVivos As INodosDeTela) As Integer
            Dim nodos = Vista(nodosVivos)
            ' ---- la puerta de NaN — 0x1418A69DF-0x1418A6B07
            If sims IsNot Nothing Then
                For Each s In sims
                    If s Is Nothing Then Continue For
                    If Not Ordenado(s.Posiciones, s.NumParticulas) OrElse
                       Not Ordenado(s.Previas, s.NumParticulas) OrElse
                       (s.Normales IsNot Nothing AndAlso Not Ordenado(s.Normales, s.NumParticulas)) Then
                        Valido = False                                          ' 0x1418A6B07
                        Return 0                                                ' jmp 0x1418A7922
                    End If
                Next
            End If

            Dim nEscritos = 0
            If _contador = _nPasos Then                                         ' 0x1418A6B13…1F
                ' ---- (1) la salida — 0x1418A6B25-0x1418A6CBC
                Dim k = _sobranteNuevo / _paso + 1.0F                           ' 0x1418A6B3B…5D
                Dim vk = Vector128.Create(k)
                For Each idx In _entradas
                    If Not EscritosYPadres(idx) OrElse idx >= conjunto.Length Then Continue For   ' 0x1418A6BAA
                    If SinExtrapolar Then                                       ' 0x1418A6BCD
                        _salida(idx) = Descomposicion.AQs(conjunto(idx))        ' 0x1418A6BD9
                    Else
                        Dim c = Descomposicion.AQs(conjunto(idx))               ' 0x1418A6BE7
                        Dim g = _guardado(idx)
                        Dim o As Qs
                        o.S = Vector128.Add(Vector128.Multiply(Vector128.Subtract(c.S, g.S), vk), g.S)   ' 0x1418A6BEC…C03
                        o.T = Vector128.Add(Vector128.Multiply(Vector128.Subtract(c.T, g.T), vk), g.T)   ' 0x1418A6C07…16
                        Dim d = Simd.Hsum4(Vector128.Multiply(c.R, g.R))        ' 0x1418A6C19…35
                        Dim signo = Vector128.BitwiseAnd(Vector128.LessThan(d, Vector128(Of Single).Zero),
                                                         Vector128.Create(-0.0F))   ' 0x1418A6C38…4B
                        Dim kS = Vector128.Xor(vk, signo)                       ' 0x1418A6C50
                        Dim unoMenosK = Vector128.Subtract(Vector128.Create(1.0F), vk)   ' 0x1418A6C3C/43
                        Dim r = Vector128.Add(Vector128.Multiply(kS, c.R),
                                              Vector128.Multiply(unoMenosK, g.R))   ' 0x1418A6C53/57/5B
                        o.R = Vector128.Multiply(r, Simd.RsqrtNewton(Simd.Hsum4(Vector128.Multiply(r, r))))   ' 0x1418A6C5E…98
                        _salida(idx) = o
                    End If
                    _salida(idx).S = Vector128.Create(1.0F)                     ' 0x1418A6CA6/AD
                Next

                ' ---- (2) el mundo de cada nodo escrito — 0x1418A6CF4-0x1418A74C0
                Dim mundos = _mundos
                Dim tieneMundo = _tieneMundo
                Array.Clear(tieneMundo)
                For Each idx In _entradas
                    If Not Escritos(idx) Then Continue For                      ' 0x1418A6D6D
                    Dim propio = nodos.Mundo(idx)
                    If propio Is Nothing Then Continue For                      ' 0x1418A6D51
                    Dim p = Padres(idx)
                    Dim mundo As NiTransformDelMotor
                    If p <> -1 AndAlso p >= 0 AndAlso nodos.Mundo(p) IsNot Nothing Then   ' 0x1418A6D8F / 0x1418A6DA5
                        ' 0x1418A6F1D-0x1418A6F61: sin padre NiNode, el motor toma el nodo del padre
                        ' HK (`nodes[p]->vtbl[+0x20]`) y le cuelga el hijo (`vtbl[+0x1D0]`, AttachChild).
                        ' ⛔ creado o del esqueleto, es el mismo cuelgue (GDFt5).
                        If nodos.IndiceDelPadreNi(idx) = -1 AndAlso nodos.MundoDelPadreNi(idx) Is Nothing Then
                            nodos.Colgar(idx, p)                                ' 0x1418A6F5A AttachChild
                        End If
                        Dim padreMundo = MundoDelPadre(idx, nodos, mundos, tieneMundo)
                        mundo = ComponerConPadre(_salida(p), _salida(idx), padreMundo.Value)
                    Else
                        mundo = MundoAbsoluto(_salida(idx), NiTransformDelMotor.DeTransform(propio).S, Origen)   ' 0x1418A72D9
                    End If
                    mundos(idx) = mundo
                    tieneMundo(idx) = True
                Next

                ' ---- (3) la entrada que viene — 0x1418A74E9-0x1418A7542
                For i = 0 To NumHuesos - 1
                    If Leidos(i) AndAlso i < conjunto.Length Then _previo(i) = Descomposicion.AQs(conjunto(i))   ' 0x1418A752B
                Next
                SinInterpolar = False                                           ' 0x1418A7542

                ' ---- (4) el local de cada nodo escrito — 0x1418A7557-0x1418A786A
                For Each idx In _entradas
                    If Not Escritos(idx) OrElse Not tieneMundo(idx) Then Continue For   ' 0x1418A75AC
                    Dim padreMundo = MundoDelPadre(idx, nodos, mundos, tieneMundo)
                    If Not padreMundo.HasValue Then Continue For                ' 0x1418A75B9
                    nodos.EscribirLocal(idx, LocalContraPadre(mundos(idx), padreMundo.Value))
                    nEscritos += 1
                Next
            End If

            ' ---- (5) lo que guarda cada paso para la extrapolación — 0x1418A78BB-0x1418A791B
            For Each idx In _entradas
                If EscritosYPadres(idx) AndAlso idx < conjunto.Length Then _guardado(idx) = Descomposicion.AQs(conjunto(idx))   ' 0x1418A790D
            Next
            SinExtrapolar = False                                               ' 0x1418A791B
            Return nEscritos
        End Function

        ''' <summary>`cmpordps` + `movmskps AND 7 == 7` sobre `xyz` de cada partícula.</summary>
        Private Shared Function Ordenado(arr As Single(), n As Integer) As Boolean
            If arr Is Nothing Then Return True
            For i = 0 To n - 1
                Dim b = i * Instancia.AnchoDeParticula
                If b + 2 >= arr.Length Then Exit For
                If Single.IsNaN(arr(b)) OrElse Single.IsNaN(arr(b + 1)) OrElse Single.IsNaN(arr(b + 2)) Then Return False
            Next
            Return True
        End Function

        ''' <summary>El mundo del padre NiNode: el que este mismo cierre acaba de escribir si el padre
        ''' es un hueso escrito (el lazo va en orden de índice, y el padre va antes), o el de la escena
        ''' si no.</summary>
        Private Shared Function MundoDelPadre(idx As Integer, nodos As INodosDeTela, mundos As NiTransformDelMotor(),
                                              tieneMundo As Boolean()) As NiTransformDelMotor?
            Dim ip = nodos.IndiceDelPadreNi(idx)
            If ip >= 0 AndAlso ip < tieneMundo.Length AndAlso tieneMundo(ip) Then Return mundos(ip)
            Dim w = nodos.MundoDelPadreNi(idx)
            If w Is Nothing Then Return Nothing
            Return NiTransformDelMotor.DeTransform(w)
        End Function

        ''' <summary>
        ''' La rama con padre — `0x1418A6DB0`-`0x1418A7408`:
        ''' <para>```
        ''' invP.t = −rotarPorConjugado(tp, qp)        ' 0x1418A6E09 (0x14133A0C0) + xorps
        ''' qpc    = (−qp.xyz, qp.w)                   ' 0x1418A6E2C/31
        ''' rel.t  = 2·((dot3(tc,qpc)·qpc + (qpc.w²−½)·tc) + cruz·qpc.w) + invP.t
        ''' rel.q  = qpc ⊗ qc
        ''' L      = NiMatriz(NiQuat normalizado(rel.q))     ' 0x1418A6F64…FEC
        ''' W.Fk   = (L.Fk.y·P.F1 + L.Fk.x·P.F0) + L.Fk.z·P.F2
        ''' W.t    = ((t.y·P.F1 + t.x·P.F0) + t.z·P.F2)·P.s + P.t ;  W.s = P.s
        ''' ```</para>
        ''' </summary>
        Private Shared Function ComponerConPadre(sp As Qs, sc As Qs, padre As NiTransformDelMotor) As NiTransformDelMotor
            Dim tp = sp.T, qp = sp.R, tc = sc.T, qc = sc.R
            Dim invPt = Vector128.Xor(RotarPorConjugado(tp, qp), Vector128.Create(-0.0F))           ' 0x1418A6E09 / 0x1418A6E25
            Dim qpc = Vector128.Xor(qp, Vector128.Create(-0.0F, -0.0F, -0.0F, 0.0F))                ' 0x1418A6E2C / 0x1418A6E31
            Dim w = Simd.BcastW(qpc)                                                                ' 0x1418A6E4B
            Dim cruzT = Vector128.Multiply(CruzYzx(tc, qpc), w)                                     ' 0x1418A6E1A…78
            Dim dotT = Hsum3(Vector128.Multiply(tc, qpc))                                           ' 0x1418A6E48…85
            Dim relT = Vector128.Multiply(dotT, qpc)                                                 ' 0x1418A6E90
            relT = Vector128.Add(relT, Vector128.Multiply(Vector128.Subtract(Vector128.Multiply(w, w),
                                                                             Vector128.Create(0.5F)), tc))   ' 0x1418A6E7B…97
            relT = Vector128.Add(relT, cruzT)                                                       ' 0x1418A6EA5
            relT = Vector128.Add(relT, relT)                                                        ' 0x1418A6EB3
            relT = Vector128.Add(relT, invPt)                                                       ' 0x1418A6EC0

            Dim vec = CruzYzx(qc, qpc)                                                              ' 0x1418A6EA9…D4
            vec = Vector128.Add(vec, Vector128.Multiply(w, qc))                                     ' 0x1418A6EBA/BD/D8
            vec = Vector128.Add(vec, Vector128.Multiply(Simd.BcastW(qc), qpc))                      ' 0x1418A6EC4…D1/E7
            Dim dotQ = Hsum3(Vector128.Multiply(qc, qpc))                                           ' 0x1418A6ECB…F05
            Dim wq = Simd.BcastW(qc).GetElement(0) * w.GetElement(0) - dotQ.GetElement(0)           ' 0x1418A6F08/1A
            Dim relQ = vec.WithElement(Simd.LaneW, wq)                                              ' 0x1418A6F21/24

            Dim l0 As Vector128(Of Single), l1 As Vector128(Of Single), l2 As Vector128(Of Single)
            MatrizDeCuaternionNormalizado(relQ, l0, l1, l2)                                         ' 0x1418A6F64…FEC

            Dim r As NiTransformDelMotor
            r.F0 = FilaPorPadre(l0, padre)                                                          ' 0x1418A713C…1C5
            r.F1 = FilaPorPadre(l1, padre)
            r.F2 = FilaPorPadre(l2, padre)
            Dim t = FilaPorPadre(relT, padre).WithElement(Simd.LaneW, 1.0F)                         ' 0x1418A70FF…21A
            t = Vector128.Multiply(t, Vector128.Create(padre.S))                                    ' 0x1418A7220
            r.T = Vector128.Add(t, padre.T.WithElement(Simd.LaneW, 0.0F))                           ' 0x1418A71F2 / 0x1418A7241
            r.S = t.GetElement(Simd.LaneW)                                                          ' 0x1418A72C2
            Return r
        End Function

        ''' <summary>La rama sin padre — `0x1418A72D9`-`0x1418A7408`: `L` del cuaternión de salida y
        ''' `t` = la de salida MÁS EL ORIGEN (`addss [ts+0x170/174/178]`, `0x1418A73FF`/`73EF`/`73F7`).
        ''' ⛔ La entrada le resta `Origen` (`LeerEntrada`) y acá se le devuelve; sin la suma las dos
        ''' mitades no cierran, y aun con origen 0 el `−0 + 0 = +0` cambia el bit (GDFt4b/4f/4g).
        ''' <para>⛔ La ESCALA del nodo NO se escribe en esta rama: el único `movss [rsi+0xac]` es
        ''' `0x1418A72C2`, de la rama con padre (motor-134). El nodo conserva la suya.</para></summary>
        Private Shared Function MundoAbsoluto(sc As Qs, escalaDelNodo As Single, origen As Vector128(Of Single)) As NiTransformDelMotor
            Dim r As NiTransformDelMotor
            MatrizDeCuaternionNormalizado(sc.R, r.F0, r.F1, r.F2)                                   ' 0x1418A72F5…369
            r.T = Vector128.Create(sc.T.GetElement(0) + origen.GetElement(0),
                                   sc.T.GetElement(1) + origen.GetElement(1),
                                   sc.T.GetElement(2) + origen.GetElement(2), 0.0F)                 ' 0x1418A7372/75 + 0x1418A73EF…3FF
            r.S = escalaDelNodo
            Return r
        End Function

        ''' <summary>La normalización con `sqrtss` + `1/len` de `0x1418A6F64`-`0x1418A6FE7` (y su gemelo
        ''' `0x1418A72F5`-`0x1418A7364`), y `0x1403C59C0`.</summary>
        Private Shared Sub MatrizDeCuaternionNormalizado(q As Vector128(Of Single),
                                                         ByRef f0 As Vector128(Of Single),
                                                         ByRef f1 As Vector128(Of Single),
                                                         ByRef f2 As Vector128(Of Single))
            Dim x = q.GetElement(0), y = q.GetElement(1), z = q.GetElement(2), w = q.GetElement(3)
            Dim n = ((w * w + x * x) + y * y) + z * z
            Dim inv = 1.0F / Simd.SqrtExacta(n)
            NiMatrizDeCuaternion(inv * w, inv * x, inv * y, inv * z, f0, f1, f2)
        End Sub

        ''' <summary>`(v.y·P.F1 + v.x·P.F0) + v.z·P.F2`.</summary>
        Private Shared Function FilaPorPadre(v As Vector128(Of Single), p As NiTransformDelMotor) As Vector128(Of Single)
            Dim a = Vector128.Multiply(Simd.BcastY(v), p.F1)
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastX(v), p.F0))
            Return Vector128.Add(a, Vector128.Multiply(Simd.BcastZ(v), p.F2))
        End Function

        ''' <summary>
        ''' El local contra el padre — `0x1418A75CD`-`0x1418A7841`.
        ''' <para>```
        ''' c0,c1,c2 = columnas de P (la traspuesta)            ' 0x1418A75F4…630
        ''' invS     = 1 / P.s                                  ' 0x1418A7655/5C divps
        ''' invT     = (((−P.t).y·c1 + (−P.t).x·c0) + (−P.t).z·c2)·invS
        ''' L.Fk     = (W.Fk.y·c1 + W.Fk.x·c0) + W.Fk.z·c2
        ''' L.t      = ((W.t.y·c1 + W.t.x·c0) + W.t.z·c2)·invS + invT ;  L.s = W.s·invS
        ''' ```</para>
        ''' </summary>
        Private Shared Function LocalContraPadre(w As NiTransformDelMotor, p As NiTransformDelMotor) As NiTransformDelMotor
            Dim c0 = Vector128.Create(p.F0.GetElement(0), p.F1.GetElement(0), p.F2.GetElement(0), 0.0F)
            Dim c1 = Vector128.Create(p.F0.GetElement(1), p.F1.GetElement(1), p.F2.GetElement(1), 0.0F)
            Dim c2 = Vector128.Create(p.F0.GetElement(2), p.F1.GetElement(2), p.F2.GetElement(2), 0.0F)
            Dim invS = Simd.DivExacta(Vector128.Create(1.0F), Vector128.Create(p.S)).GetElement(0)   ' 0x1418A7655/5C
            Dim menosT = Vector128.Subtract(Vector128(Of Single).Zero, p.T.WithElement(Simd.LaneW, p.S))  ' 0x1418A75D5/E0
            Dim invT = Vector128.Multiply(Simd.BcastY(menosT), c1)                                  ' 0x1418A766C
            invT = Vector128.Add(invT, Vector128.Multiply(Simd.BcastX(menosT), c0))                 ' 0x1418A7674/80
            invT = Vector128.Add(invT, Vector128.Multiply(Simd.BcastZ(menosT), c2))                 ' 0x1418A767C/98
            invT = Vector128.Multiply(invT, Vector128.Create(invS))                                 ' 0x1418A76C4

            Dim r As NiTransformDelMotor
            r.F0 = PorColumnas(w.F0, c0, c1, c2)                                                    ' 0x1418A76A4…6E6
            r.F1 = PorColumnas(w.F1, c0, c1, c2)                                                    ' 0x1418A763C…714
            r.F2 = PorColumnas(w.F2, c0, c1, c2)                                                    ' 0x1418A7643…7D9
            Dim t = PorColumnas(w.T, c0, c1, c2).WithElement(Simd.LaneW, w.S)                       ' 0x1418A7717…77C
            t = Vector128.Multiply(t, Vector128.Create(invS))                                       ' 0x1418A777F
            r.T = Vector128.Add(t, invT.WithElement(Simd.LaneW, 0.0F))                              ' 0x1418A776F / 0x1418A779D
            r.S = t.GetElement(Simd.LaneW)                                                          ' 0x1418A7841
            Return r
        End Function

        Private Shared Function PorColumnas(v As Vector128(Of Single), c0 As Vector128(Of Single),
                                            c1 As Vector128(Of Single), c2 As Vector128(Of Single)) As Vector128(Of Single)
            Dim a = Vector128.Multiply(Simd.BcastY(v), c1)
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastX(v), c0))
            Return Vector128.Add(a, Vector128.Multiply(Simd.BcastZ(v), c2))
        End Function

        ''' <summary>`0x14133A0C0(out, q, v)` = `2·((dot3(v,q)·q + (w²−½)·v) + cruzYzx(q,v)·w)`.</summary>
        Private Shared Function RotarPorConjugado(v As Vector128(Of Single), q As Vector128(Of Single)) As Vector128(Of Single)
            Dim w = Simd.BcastW(q)
            Dim c = Vector128.Multiply(CruzYzx(q, v), w)                         ' 0x14133A0C4…115
            Dim d = Hsum3(Vector128.Multiply(v, q))                             ' 0x14133A0DC…112
            Dim r = Vector128.Multiply(d, q)                                    ' 0x14133A123
            r = Vector128.Add(r, Vector128.Multiply(Vector128.Subtract(Vector128.Multiply(w, w),
                                                                       Vector128.Create(0.5F)), v))   ' 0x14133A108…129
            r = Vector128.Add(r, c)                                             ' 0x14133A12C
            Return Vector128.Add(r, r)                                          ' 0x14133A12F
        End Function

        Private Shared Function Hsum3(p As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Add(Vector128.Add(Simd.BcastY(p), Simd.BcastX(p)), Simd.BcastZ(p))
        End Function

        Private Shared Function CruzYzx(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            Dim ay = Vector128.Shuffle(a, Vector128.Create(1, 2, 0, 3))
            Dim by = Vector128.Shuffle(b, Vector128.Create(1, 2, 0, 3))
            Return Vector128.Shuffle(Vector128.Subtract(Vector128.Multiply(ay, b), Vector128.Multiply(a, by)),
                                     Vector128.Create(1, 2, 0, 3))
        End Function

    End Class

End Namespace
