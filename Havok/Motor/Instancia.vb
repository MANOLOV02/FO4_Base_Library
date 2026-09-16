Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclSimClothInstance` — el ESTADO VIVO de una prenda simulada.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, caps. 4.1 y 6.12bis (el layout salió del CONSTRUCTOR
' `0x1418C65C0`, no de una lista recordada).
'
' ⛔⛔ ESTA CLASE ES LA CASA DEL ESTADO QUE VIVE ENTRE CUADROS. Todo lo que el motor guarda de una
' vuelta para la siguiente vive acá y en ningún otro lado:
'   · `Posiciones` / `Previas`          — la integración de Verlet
'   · `DtSubCacheado` / `DampingEfectivo` — para no recalcular el `powf`, y para RE-ESCALAR `Previas`
'                                          cuando el paso cambia (cap. 6.2ter)
'   · `TransformPrevioTransferMotion`   — de dónde viene el arrastre del hueso
'   · `EstadosPorSet`                    — ⭐ el bloque por set: la fase de `Transition` y el
'                                        MARCO de `VolumeMx`, que es la semilla en caliente del
'                                        eigensolver (cap. 6q.4bis)
'   · `ContactosCacheados` / `HayContacto` — el pellizco
' Tratar cualquiera de ellos como temporal da un motor que «anda» y tiembla.
'
' ⭐ LAYOUT DE PARTÍCULA: **4 `Single` por partícula**, igual que el motor (16 B). No son 3: el motor
' hace `movups` de 16 B y opera las cuatro lanes. Con stride 3 los kernels SIMD leen cruzado.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' El estado vivo de una prenda. Se construye una vez por prenda instanciada y sobrevive a los
    ''' cuadros.
    ''' </summary>
    Friend NotInheritable Class Instancia

        ''' <summary>Cuántos `Single` ocupa una partícula. **4**, como el motor (`movups` de 16 B).</summary>
        Friend Const AnchoDeParticula As Integer = 4

        ' -----------------------------------------------------------------------------------------
        ' El dato del archivo — `+0x10`
        ' -----------------------------------------------------------------------------------------

        ''' <summary>`hclSimClothData` del paquete. **Sólo lectura**: es el archivo.</summary>
        Friend ReadOnly Datos As HkObj_HclSimClothData

        ''' <summary>`simulationInfo` efectivo. `getSimulationInfo` (`0x1418C7730`) es literalmente
        ''' `inst[+0x1D8] ? inst[+0x1D8] : (inst[+0x10] + 0x10)`: el override gana si existe. En el
        ''' corpus el override es siempre nulo, así que acá queda el del dato.</summary>
        Friend ReadOnly Info As HkObj_HclSimClothDataOverridableSimulationInfo

        ''' <summary>`numParticles` — `+0x20`, sale de `data.particleDatas.count`.</summary>
        Friend ReadOnly NumParticulas As Integer

        ' -----------------------------------------------------------------------------------------
        ' Las partículas — `+0x18` / `+0x28` / `+0x38`
        ' -----------------------------------------------------------------------------------------

        ''' <summary>`positions` (`+0x18`), `AnchoDeParticula` `Single` por partícula.</summary>
        Friend ReadOnly Posiciones As Single()

        ''' <summary>`previous` (`+0x28`). ⛔ En Verlet **es** la velocidad: `v = (pos − prev)/dtSub`.
        ''' Nada la puede pisar sin querer decir «esta partícula se queda quieta».</summary>
        Friend ReadOnly Previas As Single()

        ''' <summary>Normales por partícula (`+0x38`). Sólo existe si `data.doNormals`; si no, es
        ''' `Nothing` y hay kernels que cambian de rama por eso (no es un array de ceros).
        ''' <para>⚠️ No es `ReadOnly`: el gate necesita poder dárselas a una instancia sin dato de
        ''' archivo (`HabilitarNormales`) para ejercitar la rama de `data.doNormals`. En el motor
        ''' las escribe el constructor y no cambia después.</para></summary>
        Friend Normales As Single()

        ''' <summary>`mass` de cada partícula — `hclSimClothDataParticleData +0x00`, stride `0x10`.</summary>
        Friend ReadOnly Masa As Single()

        ''' <summary>`invMass` — `hclSimClothDataParticleData +0x04`. ⛔ Es lo que el motor usa para
        ''' repartir la corrección de los enlaces: **no** hay reparto por masa combinada.</summary>
        Friend ReadOnly InvMasa As Single()

        ''' <summary>`radius` — `hclSimClothDataParticleData +0x08`. Es el radio de contacto que
        ''' la respuesta de colisión resta a la distancia al shape (`0x141A6AC7B` lo lee de
        ''' `particleData + 8` en el bucle de resto de la cápsula).</summary>
        Friend ReadOnly Radio As Single()

        ''' <summary>`friction` — `+0x0C`. ⛔ Se aplica sobre **`Previas`**, no sobre `Posiciones`:
        ''' en Verlet eso es restarle velocidad tangencial (cap. 6.6.3).</summary>
        Friend ReadOnly Friccion As Single()

        ' (el doc de `staticCollisionMasks` bajo a su campo, `MascarasDeColision`)

        ''' <summary>`inst+0x50` / `+0x60` — el AABB de las **posiciones crudas**, sin margen. Lo
        ''' escriben las dos variantes del paso 5.</summary>
        Friend AabbMinParticulas As Vector128(Of Single)
        Friend AabbMaxParticulas As Vector128(Of Single)
        ''' <summary>`inst+0x70` / `+0x80` — el AABB **ancho**: todas las particulas que pasan el
        ''' filtro, extrapoladas y con el margen de `collisionTolerance`. Solo lo escribe
        ''' `UpdateAABBs`.</summary>
        Friend AabbMinAncho As Vector128(Of Single)
        Friend AabbMaxAncho As Vector128(Of Single)
        ''' <summary>`inst+0x90` / `+0xA0` — el AABB de las particulas con `staticCollisionMasks`
        ''' **negativa** (el bit 31 puesto). Solo lo escribe `UpdateAABBs`.</summary>
        Friend AabbMinMascara As Vector128(Of Single)
        Friend AabbMaxMascara As Vector128(Of Single)
        ''' <summary>`simulationInfo.collisionTolerance` (`+0x14`, de la reflexion). Es el margen de
        ''' los dos ultimos AABB (`0x1418C750A`).</summary>
        Friend ToleranciaDeColision As Single

        ''' <summary>
        ''' `simulationInfo.landscapeCollisionEnabled` (`+0x1D` de la reflexión), el permiso que
        ''' lee la puerta del terreno por `getSimulationInfo` (`0x14195E3AE`/`B2`).
        ''' <para>⛔ Sale del ARCHIVO, no del mundo (motor-83). Vive acá por el MISMO motivo que
        ''' <see cref="ToleranciaDeColision"/>: la fachada lo copia del dato y el motor lo lee de la
        ''' instancia, que es lo que hace la ley medible con un fixture.</para>
        ''' </summary>
        Friend LandscapeHabilitado As Boolean

        ''' <summary>`data.staticCollisionMasks` (+0xF8): una máscara `uint32` por partícula. El
        ''' bit que se consulta para el colisionable `ci` es `1 &lt;&lt; min(ci, 30)`
        ''' (`0x141A71641`, `0x141A71893`). `Nothing` si el dato no la trae.
        ''' <para>⚠️ No es `ReadOnly`: el gate necesita poder ponerla para ejercitar la rama del
        ''' filtro. En el motor la escribe el constructor y no cambia después.</para></summary>
        Friend MascarasDeColision As UInteger()

        ''' <summary>
        ''' `data.perInstanceCollidables.count` (`data+0xB0`), leído UNA vez al entrar en
        ''' `SolveContacts` (`0x141A71653 mov r9d, [rax+0xB0]`).
        ''' <para>⛔ Es la **puerta de la máscara**: `staticCollisionMasks` sólo se consulta para
        ''' los colisionables con índice `&lt; count` (`0x141A7173F cmp ebx, r9d / jge`); del
        ''' `count` en adelante la lista se arma con **todas** las partículas (motor-57).</para>
        ''' </summary>
        Friend NumColisionablesPorInstancia As Integer

        ''' <summary>`data.fixedParticles`: los índices de las partículas ancladas.</summary>
        Friend ReadOnly ParticulasFijas As Integer()

        ' -----------------------------------------------------------------------------------------
        ' El tiempo — `+0x108` / `+0x10C` / `+0x1CC` / `+0x1D0` / `+0x1D4`
        ' -----------------------------------------------------------------------------------------

        ''' <summary>`+0x108`: el `dtSub` de la vuelta anterior. **Arranca en 0** y ese 0 es la
        ''' señal de «primera vez» (cap. 6.2ter): decide si hay que sembrar el transform previo del
        ''' TransferMotion y si hay que re-escalar `Previas`.</summary>
        Friend DtSubCacheado As Single = 0.0F

        ''' <summary>`+0x10C`: el damping efectivo. **Arranca en 1,0**, no en 0 — con 0 la primera
        ''' vuelta mataría toda la velocidad.</summary>
        Friend DampingEfectivo As Single = 1.0F

        ''' <summary>`+0x1CC`: el modo de rigidez. El constructor lo deja en **1**; Bethesda pone 2.</summary>
        Friend Modo As Integer = 1

        ''' <summary>`+0x1D0` (`s1`). ⛔ **DIVIDE el `dt`** en `execute` (`0x14195C3EF`) SIEMPRE, y
        ''' además entra en el exponente del damping sólo si `modo ≠ 1` — la asimetría está
        ''' medida (cap. 6.2). Arranca en 1,0.</summary>
        Friend S1 As Single = 1.0F

        ''' <summary>`+0x1D4` (`s2`). Arranca en 1,0.</summary>
        Friend S2 As Single = 1.0F

        ''' <summary>`+0x1C8`: 0 ⇒ `UpdateParticlesAABB`, 1 ⇒ `UpdateAABBs`. Sólo pasa a 1 si se
        ''' activó la colisión de terreno, que en FO4 no pasa nunca (cap. 2ter.1).</summary>
        Friend ModoAabb As Integer = 0

        ' -----------------------------------------------------------------------------------------
        ' El estado que vive entre cuadros
        ' -----------------------------------------------------------------------------------------

        ''' <summary>`simCloth+0x120` — el transform del cuadro ANTERIOR que usa
        ''' `TransferMotion`. Lo pisa el paso 1 despues de transferir (RE cap. 5).
        ''' <para>⚠️ Convive con `TransformPrevioTransferMotion` (el arreglo plano de 16
        ''' `Single` que modela los mismos 64 B). Hay que unificarlos.</para></summary>
        Friend TransformPrevioDeTransferMotion As Mat4 = PrevioDelConstructor()

        ''' <summary>Lo que escribe el constructor del sim-cloth: filas 0-2 de la identidad desde
        ''' `0x142F3C700`/`0x142F3C710`/`0x142F3C720` (`0x1418C6A54`-`0x1418C6A7F`) y la fila 3 en
        ''' CERO, `w` incluida (`0x1418C6A75 xorps` + `0x1418C6A87 movups [r14+0x150]`). No es
        ''' `Mat4.Identidad`: esa trae `w = 1` en la fila 3.</summary>
        Private Shared Function PrevioDelConstructor() As Mat4
            Dim m = Mat4.Identidad
            m.F3 = Vector128(Of Single).Zero
            Return m
        End Function

        ''' <summary>¿Ya se sembró el transform previo de la transferencia? Es el `simCloth[+0x108] == 0`
        ''' del motor: en el PRIMER cuadro el previo se pone igual al actual y no se transfiere nada
        ''' (`0x14195C401`). Sin esto, el primer cuadro transfiere el salto desde la identidad.</summary>
        ' ⛔ `TransferenciaSembrada` SE FUE (motor-125): era un segundo dueno de una senal
        ' que el motor ya tiene. La primera vez es `DtSubCacheado = 0` (`0x14195B827`), y la
        ' siembra vive en `Tiempo.Preparar`.

        ''' <summary>`+0x120 … +0x160`: el transform del hueso de referencia en el cuadro anterior,
        ''' 4 filas de 16 B. Lo siembra `prepare` la primera vez y lo actualiza `execute`.</summary>
        Friend TransformPrevioTransferMotion As Single()

        ''' <summary>
        ''' `[inst+0xB0]` / `[inst+0xB8]` — los bloques de estado **por set** de
        ''' `hclTransitionConstraintSet` (y de `hclVolumeConstraintMx`).
        ''' <para>⛔ Se buscan **linealmente por id**, no por índice: `0x141A08CBE`-`0x141A08CFB`
        ''' recorre registros de `0x10` B comparando el `u32` de `+0` con el 4.º argumento de
        ''' `solve` y se queda con el puntero de `+8`. Lo mismo hace `Volume` en `0x141A0A528`.</para>
        ''' <para>⛔⛔ **No confundir con `data+0xB0`**, que es `perInstanceCollidables.count`
        ''' (motor-57). Uno cuelga de la INSTANCIA y el otro del DATA, y los dos son `+0xB0`.</para>
        ''' <para>⚠️ `hclAntiPinchConstraintSet` usa OTRO arreglo, `[inst+0xC0]`/`[inst+0xC8]`
        ''' (`0x1419F81D0`). Son dos listas distintas.</para>
        ''' </summary>
        ''' <para>⭐ **UNA sola casa, y por eso `EstadoDeSet` tiene los campos de las dos clases.**
        ''' `Transition` guarda acá su distancia de arranque y su fase; `VolumeMx`, el centroide y el
        ''' MARCO que entra como semilla en caliente del eigensolver (cap. 6q.4bis). Modelarlo dos
        ''' veces —una lista por id y un arreglo por posición— garantizaba que los dos kernels se
        ''' desincronizaran en cuanto `Volume` entrara en juego (motor-73).</para>
        Friend EstadosPorSet As New List(Of EstadoDeSet)()

        ''' <summary>El bloque de estado del set con ese id, o `Nothing`. ⛔ Búsqueda LINEAL por id,
        ''' como el motor (`0x141A0A528`): el arreglo no está indexado por posición.</summary>
        Friend Function EstadoDelSet(id As Integer) As EstadoDeSet
            For Each e In EstadosPorSet
                If e.Id = id Then Return e
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' `[inst+0xC0]` / `[inst+0xC8]` — los bloques de estado de `hclAntiPinchConstraintSet`.
        ''' <para>⛔ ES OTRA LISTA que la de `Transition` y `Volume` (`inst[+0xB0]`), con su propia
        ''' cuenta y su propia búsqueda lineal por id (`0x1419F81E3`-`0x1419F820C`). Son dos arreglos
        ''' distintos del `hclSimClothInstance`, no dos vistas del mismo.</para>
        ''' </summary>
        Friend EstadosDeAntiPellizco As New List(Of EstadoPorParticula)()

        ''' <summary>El bloque de AntiPinch con ese id, o `Nothing`.</summary>
        Friend Function EstadoDeAntiPellizco(id As Integer) As EstadoPorParticula
            For Each e In EstadosDeAntiPellizco
                If e.Id = id Then Return e
            Next
            Return Nothing
        End Function

        ''' <summary>`data.minPinchedParticleIndex` (+0x128) — la base del índice del array de
        ''' pellizco: `HayContacto` se indexa `p − este valor`, no `p`.</summary>
        Friend MinimoDePellizco As Integer
        ''' <summary>`hclSimClothData.maxParticleRadius` (`data+0x130`): lo que agranda la caja del prefiltro
        ''' de la cápsula cónica (`0x141A08700`/`08708`).</summary>
        Friend RadioMaximoDeParticula As Single

        ''' <summary>`+0x188`: los contactos cacheados del pellizco, **48 B** cada uno
        ''' (`{puntoDelPlano, normal, velocidadDelCuerpo}`).</summary>
        Friend ContactosCacheados As Single()

        ''' <summary>`data.maxPinchedParticleIndex` (+0x12A). Con el minimo define el RANGO
        ''' INCLUSIVO que recorren las dos piezas de pellizco (`0x141A75E2F cmp bx, di` + `jbe`),
        ''' y el tamano que `zeroCachedContacts` limpia (`0x141A75F5A`-`68`: `max - min + 1`).</summary>
        Friend MaximoDePellizco As Integer

        ''' <summary>`data.perParticlePinchDetectionEnabledFlags` (+0x108) — un byte por
        ''' particula: si opta a la deteccion de pellizco.
        ''' <para>⛔ PARTE LA LISTA EN DOS. `0x141A71893`-`0x141A718C2`: de las particulas que la
        ''' mascara habilita, las de bandera != 0 van a la variante de PELLIZCO (que solo detecta) y
        ''' las de bandera = 0 al kernel NORMAL (que aplica la respuesta). Ignorarla hacia que
        ''' TODAS fueran por el mismo camino.</para></summary>
        Friend PellizcoPorParticula As Byte()

        ''' <summary>`simulationInfo.pinchDetectionEnabled` (+0x1C) — la PRIMERA de las tres
        ''' comprobaciones de la puerta de `TtCollideAndSolve` (`0x141A697BF`).</summary>
        Friend PellizcoHabilitado As Boolean

        ''' <summary>`+0x198`: un byte por partícula, «hay contacto CACHEADO», indexado **relativo
        ''' a `minPinchedParticleIndex`** — no absoluto.
        ''' <para>⛔ NO es el del rescate. Lo escribe `SolveContacts` cuando el colisionable
        ''' declara pellizco, y lo lee la resolucion de contactos (`0x141A75E51`).</para></summary>
        Friend HayContacto As Byte()

        ''' <summary>`+0x1A0`: un byte por particula, «esta PELLIZCADA», tambien relativo al
        ''' minimo.
        ''' <para>⛔⛔ ES OTRO ARRAY QUE `HayContacto`. `zeroCachedContacts` pone los dos a cero por
        ''' separado (`0x141A75F4C` el +0x198, `0x141A75F96` el +0x1A0), y el rescate de
        ''' `hclAntiPinchConstraintSet` lee **este** (`0x1419F833C mov rax, [rbp + 0x1a0]`, con
        ''' `0x1419F8343 sub rcx, r11` para el indice relativo). Tenerlos colapsados hacia que el
        ''' rescate se disparara con un contacto cualquiera en vez de con un pellizco.</para>
        ''' </summary>
        Friend EstaPellizcada As Byte()

        ''' <summary>`+0x1A8`: la PRIORIDAD del colisionable que reclamo cada particula, **un
        ''' byte por particula**, relativa al minimo como los otros dos arrays.
        ''' <para>⛔ `zeroCachedContacts` la llena con `0xFFFFFFFF` por DWORD (`0x141A75FC8`-`D0`
        ''' `rep stosd`), o sea `-1` en cada byte: «nadie la reclamo todavia». Y ese `-1` es lo que
        ''' hace de centinela — la deteccion mira si el byte dejo de ser negativo.</para>
        ''' <para>⛔ Es `Byte()`, no `Integer()`: el motor la lee de a UN byte
        ''' (`0x141A6B0D1 movzx ecx, byte ptr [rdx + r10]`). Escribirla como enteros la haria
        ''' cuatro veces mas grande y el indice relativo dejaria de caer donde cae.</para></summary>
        Friend PrioridadDeContacto As Byte()

        ' -----------------------------------------------------------------------------------------
        ' Construcción
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Arma el estado desde el dato del archivo. ⛔ **No siembra las posiciones**: eso lo hace
        ''' `0x1418ECFF0` con la transformación de la pose (cap. 6ter.6), y es otro paso.
        ''' </summary>
        Friend Sub New(datos As HkObj_HclSimClothData)
            If datos Is Nothing Then Throw New ArgumentNullException(NameOf(datos))
            Me.Datos = datos
            Info = datos.SimulationInfo
            ' ⛔ LA BASE DEL INDICE DE PELLIZCO. `HayContacto` se indexa `p − minPinchedParticleIndex`
            ' (`0x1419F8343 sub rcx, r11`), no `p`. Sin esto el AntiPinch mira otra particula.
            MinimoDePellizco = datos.MinPinchedParticleIndex
            RadioMaximoDeParticula = datos.MaxParticleRadius

            Dim pds = datos.ParticleDatas
            NumParticulas = If(pds Is Nothing, 0, pds.Count)

            ReDim Posiciones(Math.Max(1, NumParticulas * AnchoDeParticula) - 1)
            ReDim Previas(Posiciones.Length - 1)
            ReDim Masa(Math.Max(1, NumParticulas) - 1)
            ReDim InvMasa(Masa.Length - 1)

            ReDim Radio(Masa.Length - 1)
            ReDim Friccion(Masa.Length - 1)
            For i = 0 To NumParticulas - 1
                Masa(i) = pds(i).Mass
                InvMasa(i) = pds(i).InvMass
                Radio(i) = pds(i).Radius
                Friccion(i) = pds(i).Friction
            Next

            Dim masc = datos.StaticCollisionMasks
            If masc IsNot Nothing AndAlso masc.Count > 0 Then MascarasDeColision = masc.ToArray()
            Dim pic = datos.PerInstanceCollidables
            NumColisionablesPorInstancia = If(pic Is Nothing, 0, pic.Count)   ' data+0xB0

            ' Las normales sólo existen si el dato las pide: la ausencia CAMBIA de rama en varios
            ' kernels, así que se deja en Nothing y no en un array de ceros.
            If datos.DoNormals Then ReDim Normales(Posiciones.Length - 1)

            Dim fijas = datos.FixedParticles
            ParticulasFijas = If(fijas Is Nothing, Array.Empty(Of Integer)(), fijas.ToArray())

            ReDim TransformPrevioTransferMotion(15)     ' 4 filas de 4 = los 64 B de +0x120

            Dim sets = datos.StaticConstraintSets
            ' ⛔ Los bloques de estado por set NO se predimensionan por posicion: el motor
            ' los busca por ID (`0x141A0A528`) y los crea cuando el set los pide.
        End Sub

        ''' <summary>
        ''' Una instancia con `n` partículas y **sin dato de archivo**, para los gates que miden
        ''' leyes que no leen el dato (el modelo de tiempo, la integración).
        ''' <para>⚠️ No es un «modo de prueba» del motor: `Datos` queda en `Nothing` y cualquier
        ''' kernel que lo toque va a reventar, que es lo que corresponde. Existe para que el gate del
        ''' tiempo no dependa de tener un NIF a mano.</para>
        ''' </summary>
        ''' <param name="fijas">Los índices de <see cref="ParticulasFijas"/>. ⭐ No es un adorno
        ''' del arnés: que el arreglo esté vacío o no es lo que elige **la rama** de
        ''' <see cref="Operadores.MoverParticulas"/>, y las dos ramas escriben `previous`
        ''' distinto. Sin esto, la rama que corre en las prendas reales no tiene cómo medirse.</param>
        Friend Shared Function SoloParticulas(n As Integer, ParamArray fijas As Integer()) As Instancia
            Return New Instancia(n, fijas)
        End Function

        Private Sub New(n As Integer, fijas As Integer())
            NumParticulas = n
            ReDim Posiciones(Math.Max(1, n * AnchoDeParticula) - 1)
            ReDim Previas(Posiciones.Length - 1)
            ReDim Masa(Math.Max(1, n) - 1)
            ReDim InvMasa(Masa.Length - 1)
            ReDim Radio(Masa.Length - 1)
            ReDim Friccion(Masa.Length - 1)
            For i = 0 To n - 1
                Masa(i) = 1.0F
                InvMasa(i) = 1.0F
            Next
            ParticulasFijas = If(fijas Is Nothing, Array.Empty(Of Integer)(), fijas)
            ReDim TransformPrevioTransferMotion(15)
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' Acceso a una partícula
        ' -----------------------------------------------------------------------------------------


        ''' <summary>La normal de la particula `i` como vector de 4 lanes.</summary>
        Friend Function Nrm(i As Integer) As Vector128(Of Single)
            Return Simd.Leer(Normales, i)
        End Function

        ''' <summary>
        ''' Le da normales a una instancia armada con <see cref="SoloParticulas"/>.
        ''' <para>⚠️ No es un «modo de prueba»: en el motor la existencia de `normals` la decide
        ''' `data.doNormals` (`+0x14C`) y el gate necesita poder ejercitar la rama que las tiene.
        ''' Con dato de archivo esto no se llama.</para>
        ''' </summary>
        Friend Sub HabilitarNormales()
            If Normales Is Nothing Then ReDim Normales(Posiciones.Length - 1)
        End Sub

        ''' <summary>La posición de la partícula `i` como vector de 4 lanes.</summary>
        Friend Function Pos(i As Integer) As Vector128(Of Single)
            Return Simd.Leer(Posiciones, i)
        End Function

        ''' <summary>La posición previa de la partícula `i`.</summary>
        Friend Function Prev(i As Integer) As Vector128(Of Single)
            Return Simd.Leer(Previas, i)
        End Function

        Friend Sub SetPos(i As Integer, v As Vector128(Of Single))
            Simd.Escribir(Posiciones, i, v)
        End Sub

        Friend Sub SetPrev(i As Integer, v As Vector128(Of Single))
            Simd.Escribir(Previas, i, v)
        End Sub

    End Class

End Namespace

