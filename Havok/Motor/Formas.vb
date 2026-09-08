Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LOS SHAPES DE COLISIÓN — el punto más cercano de cada uno.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, caps. 6.6.2bis, 6.6.3bis, 6.6.6 y 6.13bis.
'
' ⭐ EL REPARTO ES EL DEL BINARIO, no una abstracción mía: la esfera (`0x141A70120`) y la cápsula
' (`0x141A6A490`) calculan cada una su punto más cercano y después entran en una respuesta
' **idéntica instrucción por instrucción**; y la cápsula cónica lo hace todavía más explícito —
' `0x141A708E0` llama a `0x141A07C00`, que devuelve la terna `(superficie, normal, distancia)`, y
' recién ahí aplica la respuesta. Por eso cada shape acá devuelve esa terna y la respuesta vive una
' sola vez, en `Colision`.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>Lo que un shape le entrega a la respuesta de contacto.</summary>
    Friend Structure Contacto
        ''' <summary>El punto de la superficie del shape más cercano a la partícula.</summary>
        Friend Superficie As Vector128(Of Single)
        ''' <summary>La normal unitaria, hacia afuera del shape.</summary>
        Friend Normal As Vector128(Of Single)
        ''' <summary>Distancia con signo de la partícula a la superficie, **sin** restar el radio de
        ''' la partícula: eso lo hace la respuesta.</summary>
        Friend Distancia As Single
    End Structure

    ''' <summary>
    ''' Un shape de colisión, ya con sus datos **derivados** al mundo.
    ''' <para>⛔ Los datos derivados se recalculan por substep desde `coll.transform`: el motor lo
    ''' hace en el envoltorio de cada tipo (`0x141A08550` para el cono) porque el colisionable
    ''' **camina** de su pose vieja a la nueva a lo largo de los substeps (cap. 6.9).</para>
    ''' </summary>
    Friend MustInherit Class Forma

        ''' <summary>El `type` del censo del binario (`Havok.Canon.CensoDeClasesHcl`).</summary>
        Friend ReadOnly Tipo As Integer

        Protected Sub New(tipo As Integer)
            Me.Tipo = tipo
        End Sub

        ''' <summary>
        ''' El punto de superficie, la normal y la distancia para esa partícula.
        ''' <para>⛔⛔ `enResto` NO es un detalle de implementación: **el bucle de resto del motor no
        ''' corre la misma ley que el de a 4**. Medido en los tres shapes:</para>
        ''' <para>· **esfera**: el bloque suma `FLT_EPSILON` a `|w|²` (`0x141A7030C`); el resto **no**
        ''' (`0x141A7078C` → `0x141A70796`, sin `addps`).</para>
        ''' <para>· **cápsula**: ídem (`0x141A6A7F6` contra `0x141A6ACE8` → `0x141A6ACF2`).</para>
        ''' <para>· **cápsula cónica**: son **dos funciones distintas**. El bloque llama a
        ''' `0x141A07C00`; el resto, a `0x141A078E0`, que no referencia `0x142F5AE70` ni una vez
        ''' **y normaliza la normal del lateral con Newton** (`0x141A07B87`/`0x141A07B91`/
        ''' `0x141A07BA6`) donde el de a 4 usa `rsqrtps` **crudo** (`0x141A07FFE`).</para>
        ''' <para>⇒ Para las últimas `count mod 4` partículas de la lista de cada colisionable, en cada
        ''' substep, la cuenta es otra. Aplicarles la del bloque es una invención — y encima invierte
        ''' el caso degenerado: sin `ε`, una partícula pegada al centro sí sale empujada (motor-54).</para>
        ''' </summary>
        Friend MustOverride Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                     enResto As Boolean) As Contacto

        ''' <summary>
        ''' Devuelve la forma **derivada al mundo** con el transform del colisionable.
        ''' <para>⛔⛔ **El motor deriva UNA VEZ por (colisionable × substep), antes del bucle de
        ''' partículas** — no por contacto. `0x141A71031 lea r8, [rbp+0x70]` reserva un scratch de
        ''' pila en el kernel `0x141A70FD0`, `0x141A7103A` lo llena y recién después
        ''' `0x141A71049…` recorre las partículas. Derivar por partícula daría el mismo número pero
        ''' N veces la misma cuenta, y movería los contadores de `G19b` (motor-75).</para>
        ''' <para>⭐ Por eso `PuntoMasCercano` **no** cambia de firma: opera sobre lo derivado.</para>
        ''' <para>⛔ Y el shape que trae el archivo está en espacio **LOCAL**: el RE titula sus dos
        ''' secciones «`hclTaperedCapsuleShape` — **recálculo por substep**» (`RE:2458`) y
        ''' «`hclCapsuleShape` — **recálculo por substep**» (`RE:2502`). Guardarlo ya derivado y no
        ''' recalcularlo hace que la tela colisione contra la pose del arranque del frame en todos
        ''' los substeps menos el primero — y el corpus mide `subSteps > 1` en **401 de 451**.</para>
        ''' </summary>
        Friend MustOverride Function Derivar(m As Mat4) As Forma

        ''' <summary>
        ''' Rota un vector al mundo **sin** la traslacion — `(y·M1 + x·M0) + z·M2`, la forma del
        ''' plano (`0x141A6E283`-`0x141A6E2B0`).
        ''' <para>⛔ Es la que va para una **normal**: sumarle `M3` la sacaria de la esfera unidad.
        ''' El plano corrige su `w` aparte, con `dot3(M.fila3, n̂)`.</para>
        ''' <para>Bit a bit es la misma que `Colisionables.FilaPorMat` (`0x14195C59E`): la suma
        ''' arranca por `y` en un sitio y por `x` en el otro, y sumar conmuta exacto en IEEE.</para>
        ''' </summary>
        Protected Shared Function AlMundoSinTraslacion(v As Vector128(Of Single),
                                                       m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Add(Vector128.Multiply(Simd.BcastY(v), m.F1),
                                  Vector128.Multiply(Simd.BcastX(v), m.F0))   ' 0x141A6E28E/92 + 0x141A6E2A3
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2)) ' 0x141A6E296/9A + 0x141A6E2B0
        End Function

        ''' <summary>
        ''' `((x·M0 + y·M1) + z·M2) + M3` — la transformación de punto de **la familia de
        ''' colisión**, con la traslación **al final**.
        ''' <para>⛔ **No es <see cref="Operadores.TransformarPunto"/>**, que suma la traslación
        ''' enseguida del término en `x` (motor-55) y da otro bit. Las cuatro derivaciones de shape
        ''' usan ésta: el cono (`0x141A085A2`), la cápsula (`0x141A6A569`), la esfera
        ''' (`0x141A701D3`) y el plano (`0x141A6E2A2`).</para>
        ''' <para>⚠️ La esfera suma `(y·M1 + x·M0)` en vez de `(x·M0 + y·M1)`; la suma de dos
        ''' `float` es conmutativa y exacta, así que es **el mismo bit**.</para>
        ''' </summary>
      Protected Shared Function AlMundo(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Multiply(Simd.BcastX(v), m.F0)
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastY(v), m.F1))
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))
            Return Vector128.Add(r, m.F3)                        ' ⬅ la traslación AL FINAL
        End Function

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' ⛔ Cuántas veces la geometría de un shape disparó un **reporte** del motor.
    ''' <para>Los dos de `hclTaperedCapsuleShape` (`«radii must differ by more than 2 percent»` y
    ''' `«R - r < l»`) **no cortan**: el código sigue y deriva con lo que haya (motor-77). Un
    ''' `Throw` acá sería inventar un corte que el motor no tiene, y tragárselos en silencio sería
    ''' el `Case Else` mudo. Se cuenta, y el gate lo mide.</para>
    ''' <para>Medido: en el corpus **ninguno de los 1.608 conos** lo dispara.</para>
    ''' </summary>
    Friend Module ReportesDelMotor
        Friend Geometria As Long
    End Module

    ''' <summary>
    ''' `hclSphereShape` (tipo 0) — kernel `0x141A70120`.
    ''' </summary>
    Friend NotInheritable Class Esfera
        Inherits Forma

        Friend ReadOnly Centro As Vector128(Of Single)
        Friend ReadOnly Radio As Single

        Friend Sub New(centro As Vector128(Of Single), radio As Single)
            MyBase.New(0)
            Me.Centro = centro
            Me.Radio = radio
        End Sub

        ''' <summary>`0x141A7019A`-`0x141A701E4`: el centro al mundo y el radio tal cual.
        ''' <para>⛔ El radio es la **`w`** del `hkSphere` (`shape[+0x2C]`, `0x141A701CB`), no un
        ''' campo aparte — por eso no se transforma.</para></summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Return New Esfera(AlMundo(Centro, m), Radio)
        End Function

        ''' <summary>
        ''' ```
        ''' w    = P − centro
        ''' len2 = dot3(w,w) + FLT_EPSILON                 ' 0x141A7030C (0x142F5AE70)
        ''' inv  = (len2 <= 0) ? 0 : rsqrtps + UNA Newton  ' 0x141A70314-0x141A70338
        ''' n̂   = w · inv        ;  |w| = len2 · inv
        ''' S    = n̂ · radio + centro
        ''' dist = |w| − radio
        ''' ```
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim w = Vector128.Subtract(p, Centro)                         ' 0x141A7027D
            ' ⛔ el `ε` es del bloque de 4 (0x141A7030C); el resto NO lo suma (0x141A7078C)
            Dim len2 = Simd.Dot3(w, w)
            If Not enResto Then len2 = Vector128.Add(len2, Vector128.Create(Simd.FltEpsilon))
            Dim inv = Simd.RsqrtNewtonConGuarda(len2)                     ' 0x141A70314…38
            Dim r As Contacto
            r.Normal = Vector128.Multiply(w, inv)
            Dim largo = Simd.Lane0(Vector128.Multiply(len2, inv))
            r.Superficie = Vector128.Add(Vector128.Multiply(r.Normal, Vector128.Create(Radio)), Centro)
            r.Distancia = largo - Radio
            Return r
        End Function

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclCapsuleShape` (tipo 2) — kernel `0x141A6A490`. **506 colisionables** del corpus.
    ''' <para>`start`, `end` y `dir` ya al mundo; `capLenSqrdInv` es `1/|end−start|²`.</para>
    ''' </summary>
    Friend NotInheritable Class Capsula
        Inherits Forma

        Friend ReadOnly Inicio As Vector128(Of Single)
        Friend ReadOnly Fin As Vector128(Of Single)
        Friend ReadOnly Radio As Single
        Friend ReadOnly LargoCuadInv As Single

        Friend Sub New(inicio As Vector128(Of Single), fin As Vector128(Of Single),
                       radio As Single, largoCuadInv As Single)
            MyBase.New(2)
            Me.Inicio = inicio
            Me.Fin = fin
            Me.Radio = radio
            Me.LargoCuadInv = largoCuadInv
        End Sub

        ''' <summary>
        ''' `0x141A6A52B`-`0x141A6A58A` transforma los dos extremos, y `0x1419FE400` →
        ''' **`0x1419FE440`** deriva `capLenSqrdInv`.
        ''' <para>⛔ `capLenSqrdInv` **no** es `1/|d|²` calculado como uno quiera: es
        ''' `1,0 / ((|d|²·inv)²)` con `inv` = `rsqrtps` + **UNA Newton** con guarda
        ''' (`0x1419FE483`-`0x1419FE4AA`) y después un `divss` **exacto**
        ''' (`0x1419FE4B5`/`BB`/`C3`). Es una clase de aproximación, no direccionamiento: el arnés
        ''' le venía pasando `1/16` exacto por constructor y eso **no es lo que el motor
        ''' calcula** (motor-78).</para>
        ''' <para>⭐ Y el kernel sigue usando `fin − inicio` **crudo** más este `+0x54`; el `dir`
        ''' unitario de `+0x40` es para otra cosa.</para>
        ''' </summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Dim a = AlMundo(Inicio, m)
            Dim b = AlMundo(Fin, m)
            Dim d = Vector128.Subtract(b, a)
            Dim len2 = Simd.Dot3(d, d)
            Dim inv = Simd.RsqrtNewtonConGuarda(len2)             ' 0x1419FE483…4AA
            Dim largo = Simd.Lane0(Vector128.Multiply(len2, inv))
            Dim lci = Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F),
                                                Vector128.Create(largo * largo)))  ' 0x1419FE4C3
            Return New Capsula(a, b, Radio, lci)
        End Function

        ''' <summary>
        ''' ```
        ''' d    = fin − inicio
        ''' t    = clamp( dot3(P − inicio, d) · capLenSqrdInv , 0, 1 )   ' maxps 0 / minps 1
        ''' C    = inicio + t·d
        ''' w    = P − C
        ''' len2 = dot3(w,w) + FLT_EPSILON
        ''' inv  = (len2 <= 0) ? 0 : rsqrtps + UNA Newton
        ''' n̂   = w·inv   ;   |w| = len2·inv
        ''' S    = C + n̂·radio    ;   dist = |w| − radio
        ''' ```
        ''' <para>Las constantes del `clamp` son `0` en `0x142F3C550` y `1` en `0x142F3C560`
        ''' (`0x141A6A6DD` `maxps` y `0x141A6A6E0` `minps`).</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim d = Vector128.Subtract(Fin, Inicio)                       ' 0x141A6A640
            Dim t = Vector128.Multiply(Simd.Dot3(Vector128.Subtract(p, Inicio), d),
                                       Vector128.Create(LargoCuadInv))    ' 0x141A6A6DA
            t = Vector128.Max(Vector128(Of Single).Zero, t)               ' 0x141A6A6DD maxps
            t = Vector128.Min(t, Vector128.Create(1.0F))                  ' 0x141A6A6E0 minps
            Dim c = Vector128.Add(Inicio, Vector128.Multiply(t, d))       ' 0x141A6A715/737

            Dim w = Vector128.Subtract(p, c)
            ' ⛔ el `ε` es del bloque de 4 (0x141A6A7F6); el resto NO lo suma (0x141A6ACE8)
            Dim len2 = Simd.Dot3(w, w)
            If Not enResto Then len2 = Vector128.Add(len2, Vector128.Create(Simd.FltEpsilon))
            Dim inv = Simd.RsqrtNewtonConGuarda(len2)         ' 0x141A6A7F6, la CÁPSULA RECTA
            Dim r As Contacto
            r.Normal = Vector128.Multiply(w, inv)
            r.Superficie = Vector128.Add(c, Vector128.Multiply(r.Normal, Vector128.Create(Radio)))
            r.Distancia = Simd.Lane0(Vector128.Multiply(len2, inv)) - Radio
            Return r
        End Function

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclTaperedCapsuleShape` (tipo 3) — geometría `0x141A07C00`. **845 colisionables** del
    ''' corpus: es el shape más frecuente de todos.
    ''' <para>Todos los campos son los **derivados** de `0x141A07600` (cap. 6bis), con el par
    ''' `(small, big)` ya reordenado para que `small` sea el del radio menor.</para>
    ''' </summary>
    Friend NotInheritable Class CapsulaConica
        Inherits Forma

        Friend ReadOnly Chico As Vector128(Of Single)      ' +0x20
        Friend ReadOnly Grande As Vector128(Of Single)     ' +0x30
        Friend ReadOnly Apice As Vector128(Of Single)      ' +0x40  coneApex
        Friend ReadOnly Eje As Vector128(Of Single)        ' +0x50  lVec, UNITARIO
        Friend ReadOnly RadioChico As Single               ' +0x90  r
        Friend ReadOnly RadioGrande As Single              ' +0x94  R
        Friend ReadOnly Largo As Single                    ' +0x98  l = |axis| + FLT_EPSILON
        Friend ReadOnly DistApice As Single                ' +0x9C  d = r / sinTheta
        Friend ReadOnly CosTheta As Single                 ' +0xA0
        Friend ReadOnly SinTheta As Single                 ' +0xA4
        Friend ReadOnly TanTheta As Single                 ' +0xA8

        Friend Sub New(chico As Vector128(Of Single), grande As Vector128(Of Single),
                       apice As Vector128(Of Single), eje As Vector128(Of Single),
                       radioChico As Single, radioGrande As Single, largo As Single,
                       distApice As Single, cosTheta As Single, sinTheta As Single,
                       tanTheta As Single)
            MyBase.New(3)
            Me.Chico = chico : Me.Grande = grande : Me.Apice = apice : Me.Eje = eje
            Me.RadioChico = radioChico : Me.RadioGrande = radioGrande
            Me.Largo = largo : Me.DistApice = distApice
            Me.CosTheta = cosTheta : Me.SinTheta = sinTheta : Me.TanTheta = tanTheta
        End Sub

        ''' <summary>
        ''' ⭐⭐ `0x141A08550` (los dos puntos al mundo) + `0x141A07600` (los once campos).
        ''' <para>**VALIDADA contra los 1.608 `hclTaperedCapsuleShape` del corpus**: el archivo
        ''' **serializa** sus derivados (`ConeApex`, `ConeAxis`, `L`, `D`, `CosTheta`, `SinTheta`,
        ''' `TanTheta`…), así que se pudo recalcular de `Small`/`Big`/radios y comparar. Peor error
        ''' relativo: `L` 1,80e-07 · `D` 1,97e-07 · `sinθ` 2,08e-07 · `cosθ` 9,34e-08 ·
        ''' `tanθ` 2,72e-07 · eje 2,37e-07 · ápice 1,10e-05. Todo en el orden del redondeo de
        ''' `Single` (2⁻²³ ≈ 1,19e-07). No es una invariante que elegí yo: es el dato real.</para>
        ''' <para>```
        ''' r = min(rA, rB)  ;  R = max(rA, rB)            ' 0x141A0761B minss / 0x141A0764E maxss
        ''' ⛔ y si vienen al revés, los DOS PUNTOS se intercambian   ' 0x141A0764B + dos cmovbe
        ''' eje  = B − A ;  inv = rsqrtps + UNA Newton con guarda    ' 0x141A07727…59
        ''' Eje  = eje·inv                                  ' UNITARIO, 0x141A07759
        ''' l    = inv·|eje|² + FLT_EPSILON                  ' 0x141A0775D/65 (ε en 0x142468470)
        ''' sinθ = (R − r) / l                              ' 0x141A07795 divss EXACTO
        ''' H    = ((l² − (R−r)²)·R) / ((R−r)·l)            ' 0x141A07786…B0  ⛔ (R−r) a la PRIMERA
        ''' u    = R·H·sinθ                                 ' 0x141A07813 + 0x141A07829
        ''' d    = r / sinθ                                 ' 0x141A07824 divss
        ''' Ápice= A − d·Eje                                ' 0x141A07847/59
        ''' tanθ = sqrt(u) / H                              ' 0x141A07850 sqrtss EXACTO + 0x141A0786D
        ''' cosθ = H / sqrt(u + H²)                         ' 0x141A078AA/B5/B9
        ''' ```</para>
        ''' <para>⛔ **`tanθ` NO es `sinθ/cosθ`** y **`H` lleva `(R−r)` a la primera**: `0x141A07792`
        ''' recarga `R−r` encima del `(R−r)²` antes del `mulss` de `0x141A077A4`. Con cualquiera de
        ''' las otras tres combinaciones el error contra el corpus sería del 100 %, no de 1e-07.</para>
        ''' <para>⛔⛔ **Los dos asserts del motor son REPORTES, no cortes** (motor-77): después de
        ''' `0x14137EBA0` el código **sigue** (`0x141A076E1` recarga `R`, `0x141A07802`/`0A`/`0E`
        ''' recargan `sinθ`, `A` y el eje) y deriva con lo que haya. Por eso acá se **cuenta** y se
        ''' sigue, no se tira. Medido: en el corpus **ninguno** dispara — `(R−r)/R ∈ [0,04 ; 0,487]`,
        ''' `r == R` en 0, `r > R` en 0, `sinθ > 1` en 0.</para>
        ''' </summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            ' 0x141A0761B / 0x141A0764E: los radios se ORDENAN…
            Dim rMin = Math.Min(RadioChico, RadioGrande)
            Dim rMax = Math.Max(RadioChico, RadioGrande)
            ' …y si vienen al revés, los dos PUNTOS se intercambian (0x141A0764B + cmovbe ×2)
            Dim locA = Chico, locB = Grande
            If RadioChico > RadioGrande Then
                locA = Grande : locB = Chico
            End If

            ' ⛔ REPORTE, no corte: el motor sigue derivando con lo que haya (motor-77)
            If rMax <> 0.0F AndAlso (rMax - rMin) / rMax < 0.0199999996F Then
                ReportesDelMotor.Geometria += 1L                          ' 0x141A0768C/93, 0x3CA3D70A
            End If

            Dim a = AlMundo(locA, m)                              ' 0x141A08565…A2
            Dim b = AlMundo(locB, m)                              ' 0x141A0858C…AE

            Dim ejeCrudo = Vector128.Subtract(b, a)               ' 0x141A076FB
            Dim len2 = Simd.Dot3(ejeCrudo, ejeCrudo)
            Dim inv = Simd.RsqrtNewtonConGuarda(len2)             ' 0x141A07727…56
            Dim eje = Vector128.Multiply(ejeCrudo, inv)           ' 0x141A07759, UNITARIO
            Dim l = Simd.Lane0(Vector128.Multiply(len2, inv)) + Simd.FltEpsilon  ' 0x141A0775D/65

            Dim dif = rMax - rMin
            Dim sn = Simd.Lane0(Simd.DivExacta(Vector128.Create(dif), Vector128.Create(l)))  ' 0x141A07795
            If sn > 1.0F Then
                ReportesDelMotor.Geometria += 1L                          ' 0x141A0779D/B4
            End If

            ' ⛔ (R−r) a la PRIMERA en el denominador: 0x141A07792 lo recarga
            Dim h = Simd.Lane0(Simd.DivExacta(
                Vector128.Create((l * l - dif * dif) * rMax),
                Vector128.Create(dif * l)))                       ' 0x141A07786…B0
            Dim u = rMax * h * sn                                 ' 0x141A07813 + 0x141A07829
            Dim d = Simd.Lane0(Simd.DivExacta(Vector128.Create(rMin), Vector128.Create(sn)))  ' 0x141A07824
            Dim apice = Vector128.Subtract(a, Vector128.Multiply(Vector128.Create(d), eje))   ' 0x141A07847/59

            Dim tn = Simd.Lane0(Simd.DivExacta(
                Vector128.Create(Simd.SqrtExacta(u)),
                Vector128.Create(h)))                             ' 0x141A07850 + 0x141A0786D
            Dim cs = Simd.Lane0(Simd.DivExacta(
                Vector128.Create(h),
                Vector128.Create(Simd.SqrtExacta(u + h * h))))  ' 0x141A078AA/B5/B9

            Return New CapsulaConica(a, b, apice, eje, rMin, rMax, l, d, cs, sn, tn)
        End Function

        ''' <summary>
        ''' Las **tres regiones** del cono, con los bordes INCLINADOS — `0x141A07C00`.
        ''' ```
        ''' v    = P − apice
        ''' t    = dot3(v, eje)                                  ' coordenada AXIAL
        ''' perp = cross( eje, cross(v, eje) )                   ' ⬅ EN ESE ORDEN
        ''' p2   = dot3(perp, perp)  [+ FLT_EPSILON sólo en el BLOQUE de 4]   ' 0x141A07E00
        ''' inv  = (p2 <= 0) ? 0 : rsqrtps + UNA Newton           ' 0x141A07E07-0x141A07E26
        ''' perpDir = perp · inv
        ''' perpLen = p2 · inv                                    ' 0x141A07E6B
        ''' dist = ( perpLen − tanTheta·t ) · cosTheta            ' 0x141A07E86 + 0x141A07E9E
        ''' b1   = d + (−tanTheta)·perpLen                        ' 0x141A07E77 + 0x141A07E91
        ''' b2   = l + b1                                         ' 0x141A07EA2
        '''
        ''' si t < b1        -> esfera contra `chico`  con radio `r`     ' 0x141A07EB9
        ''' si no y b2 < t   -> esfera contra `grande` con radio `R`     ' 0x141A07F2B
        ''' si no (LATERAL)  -> n̂ = normalizar( cosTheta·perpDir − sinTheta·eje )   ' 0x141A07FB6/FD9/FDD
        '''                     S  = P − n̂·dist                                     ' 0x141A0801E/8021
        ''' ```
        ''' <para>⛔⛔ **Los bordes se inclinan con `|perp|`, NO con `dist`**: `b1 = d − tanθ·|perp|`.
        ''' Cuando `0x141A07E77` hace el `mulps`, `dist` todavía no se fabricó (nace en
        ''' `0x141A07E86`/`0x141A07E9E`). Y es la geometría correcta: el borde es el plano por el
        ''' círculo de tangencia, `t + |perp|·tanθ < d`. La primera redacción decía `− tanθ·dist` y
        ''' mandaba a la tapa chica partículas que el motor manda al lateral (motor-52). Un
        ''' `clamp(t, 0, l)` como el de la cápsula recta parte el cono en otro lado todavía.</para>
        ''' <para>⛔ **El bloque de 4 y el resto son DOS FUNCIONES**: `0x141A07C00` suma `ε` a `p2`
        ''' y normaliza el lateral con `rsqrtps` crudo; `0x141A078E0` no suma `ε` (cero referencias
        ''' a `0x142F5AE70`) y normaliza con Newton (motor-54). De ahí el `enResto`.</para>
        ''' <para>⛔ **La normal del lateral no es `perpDir`**: es `cosθ·perpDir − sinθ·eje`. Con `R−r`
        ''' chico la diferencia es chica pero **sistemática**, y la tela se mete.</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim v = Vector128.Subtract(p, Apice)                              ' 0x141A07C1E…65
            Dim t = Simd.Lane0(Simd.Dot3(v, Eje))                             ' 0x141A07C77…CC3
            ' ⛔⛔ EL SEGUNDO CRUZ VA AL REVÉS de lo que parece: es `L × (v × L)`, no
            ' `(v × L) × L`. En el binario, `0x141A07CF8`/`0x141A07D00`/`0x141A07D11` hacen
            ' `shuf(c)·L − shuf(L)·c`, que con la forma del motor es `cross(L, c)`. La diferencia es
            ' el SIGNO: `L × (v × L) = v − t·L` (la componente perpendicular, hacia AFUERA), y
            ' `(v × L) × L` es su negativo — con eso la normal apunta para adentro y la partícula
            ' sale del lado equivocado del cono. Lo cazó `GC8a`, no la lectura.
            Dim perp = Polar.Cruz(Eje, Polar.Cruz(v, Eje))                    ' 0x141A07CB9…DA1
            ' ⛔ el resto corre OTRA funcion (0x141A078E0) que no suma el epsilon (cero
            ' referencias a 0x142F5AE70) y que normaliza el lateral CON Newton. Ver la firma de la base.
            Dim p2 = Simd.Dot3(perp, perp)
            If Not enResto Then p2 = Vector128.Add(p2, Vector128.Create(Simd.FltEpsilon))  ' 0x141A07E00
            Dim inv = Simd.RsqrtNewtonConGuarda(p2)                           ' 0x141A07E07…26
            Dim perpDir = Vector128.Multiply(perp, inv)                       ' 0x141A07E42…5F
            Dim perpLen = Simd.Lane0(Vector128.Multiply(p2, inv))             ' 0x141A07E6B mulps xmm14, xmm5
            Dim dist = (perpLen - TanTheta * t) * CosTheta                     ' 0x141A07E86 + 0x141A07E9E

            ' ⛔⛔ EL BORDE SE INCLINA CON `|perp|`, NO CON `dist`. El `mulps xmm1, xmm14` de
            ' `0x141A07E77` ocurre cuando `xmm14` todavía vale `|perp|`: `dist` recién se fabrica en
            ' `0x141A07E86` (`− tanθ·t`) y `0x141A07E9E` (`· cosθ`). O sea que cuando se calcula `b1`,
            ' `dist` NO EXISTE.
            ' Y además es la geometría correcta: el borde es el plano perpendicular a la generatriz
            ' por el círculo de tangencia, o sea `t + |perp|·tanθ < d`.
            ' Con `b1 = d − tanθ·dist` una partícula que el motor manda al LATERAL cae en la tapa
            ' chica y sale medio unidad para el otro lado — en el shape de 845 colisionables, y justo
            ' en las uniones de las cápsulas (motor-52).
            Dim b1 = DistApice + (-TanTheta) * perpLen                         ' 0x141A07E77 + 0x141A07E91
            Dim b2 = Largo + b1                                               ' 0x141A07EA2

            If t < b1 Then Return ComoEsfera(p, Chico, RadioChico)            ' 0x141A07EA6 cmpltps
            If b2 < t Then Return ComoEsfera(p, Grande, RadioGrande)          ' 0x141A07EAE cmpltps

            Dim r As Contacto
            Dim n = Vector128.Subtract(Vector128.Multiply(Vector128.Create(CosTheta), perpDir),
                                       Vector128.Multiply(Vector128.Create(SinTheta), Eje))  ' 0x141A07FB6/D9/DD
            ' ⛔ el bloque de 4 normaliza con `rsqrtps` CRUDO (0x141A07FFE); el resto, con Newton
            ' (0x141A07B87/B91/BA6). Son dos funciones distintas del binario (motor-54).
            r.Normal = If(enResto,
                          Simd.NormalizarNewton(n),
                          Simd.NormalizarRsqrtCrudoConGuarda(n))              ' 0x141A07FFE…800C
            r.Superficie = Vector128.Subtract(p, Vector128.Multiply(r.Normal, Vector128.Create(dist)))
            r.Distancia = dist
            Return r
        End Function

        ''' <summary>Las dos tapas: es exactamente la ley de la esfera, con `rsqrtps` + UNA Newton y
        ''' la misma guarda (`0x141A07EEA`-`0x141A07F0A` y `0x141A07F5C`-`0x141A07F7C`).</summary>
        Private Function ComoEsfera(p As Vector128(Of Single), centro As Vector128(Of Single),
                                    radio As Single) As Contacto
            Dim w = Vector128.Subtract(p, centro)
            Dim len2 = Simd.Dot3(w, w)                        ' ⬅ sin epsilon acá: el motor no lo suma
            Dim inv = Simd.RsqrtNewtonConGuarda(len2)
            Dim r As Contacto
            r.Normal = Vector128.Multiply(w, inv)
            r.Superficie = Vector128.Add(Vector128.Multiply(r.Normal, Vector128.Create(radio)), centro)
            r.Distancia = Simd.Lane0(Vector128.Multiply(len2, inv)) - radio
            Return r
        End Function

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclPlaneShape` (tipo 1) — kernel `0x141A6E210`, cache de pellizco `0x141A6D980`.
    ''' <para>El dato es **un solo `vec4`** en `shape+0x20`: la normal en `xyz` y la constante en
    ''' `w`. No hay nada mas.</para>
    ''' <para>⛔ **No usa `rsqrt` en ningun lado**: la normal del archivo ya viene unitaria y el
    ''' motor la rota y la usa tal cual. Normalizarla «por las dudas» seria inventar.</para>
    ''' </summary>
    Friend NotInheritable Class Plano
        Inherits Forma

        ''' <summary>`(n.x, n.y, n.z, w)` — `shape+0x20` (`0x141A6E279`).</summary>
        Friend ReadOnly Ecuacion As Vector128(Of Single)

        Friend Sub New(ecuacion As Vector128(Of Single))
            MyBase.New(1)
            Me.Ecuacion = ecuacion
        End Sub

        ''' <summary>
        ''' `0x141A6E279`-`0x141A6E2EA`:
        ''' `n̂ = n × T(3×3)` (**sin** traslacion) y `d = w − dot3(T.fila3, n̂)`.
        ''' <para>⛔ La correccion de la `w` usa la normal **ya rotada**, no la local
        ''' (`0x141A6E2B3 mulps xmm3, xmm2`, con `xmm2` = la rotada y `xmm3` = `T.fila3`).</para>
        ''' <para>⛔ El `w` local se difunde con **mascara + dos `orps`** (`0x14271CCB0` =
        ''' `(0,0,0,0xFFFFFFFF)`, medido), no con un `shufps 0xFF`. Da lo mismo; se cita la
        ''' forma.</para>
        ''' </summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Dim n = AlMundoSinTraslacion(Ecuacion, m)                      ' 0x141A6E283…0x141A6E2B0
            Dim d = Simd.Lane0(Simd.BcastW(Ecuacion)) -
                    Simd.Lane0(Simd.Dot3(m.F3, n))                         ' 0x141A6E2B3…0x141A6E2E7
            Return New Plano(n.WithElement(Simd.LaneW, d))                 ' 0x141A6E2EA movss [rsp+0x4C]
        End Function

        ''' <summary>
        ''' ```
        ''' dist = dot3(P, n̂) + d          ' hsum de CUATRO lanes: 0x141A6E8C2 (0x4E) + 0x141A6E8CC (0xB1)
        ''' S    = P − dist·n̂              ' 0x141A6E8D3/D9/E0
        ''' corta si  dist − radioP &lt; 0    ' 0x141A6E8D6 subps + 0x141A6E8DC comiss / jae
        ''' ```
        ''' <para>⛔ La suma horizontal es de las **cuatro** lanes, con el `w` del plano metido en
        ''' la lane 3 por `unpckhps`/`shufps 0xC4` (`0x141A6E8B8`/`BB`). No es `Dot3` de tres mas
        ''' `d` aparte: otro orden de sumas, otros bits.</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim dist = DistanciaAlPlano(p, Ecuacion)
            Dim r As Contacto
            r.Normal = Ecuacion                                            ' la `w` no molesta: sólo se usan 3 lanes
            r.Distancia = Simd.Lane0(dist)
            r.Superficie = Vector128.Add(Vector128.Multiply(
                Vector128.Subtract(Vector128(Of Single).Zero, dist), Ecuacion), p)  ' 0x141A6E8D3/D9/E0
            Return r
        End Function

        ''' <summary>
        ''' `dot3(P, plano) + plano.w` con la suma horizontal de las **cuatro** lanes.
        ''' <para>`(Px·nx, Py·ny, Pz·nz, w)` se arma con `unpckhps` + `shufps 0xC4`
        ''' (`0x141A6E8B8`/`BB`, y igual en `0x141A6F77A`/`7D` del tipo 10) y despues `shufps 0x4E`
        ''' + `0xB1`.</para>
        ''' </summary>
        Friend Shared Function DistanciaAlPlano(p As Vector128(Of Single),
                                                plano As Vector128(Of Single)) As Vector128(Of Single)
            Dim m = Vector128.Multiply(plano, p)                            ' 0x141A6E8A7 mulps
            ' (m.x, m.y, m.z, plano.w) — 0x141A6E8B8 unpckhps + 0x141A6E8BB shufps 0xC4
            Dim q = m.WithElement(Simd.LaneW, plano.GetElement(Simd.LaneW))
            Return Simd.Hsum4(q)                          ' 0x141A6E8C2/C6 + 0x141A6E8CC/D0
        End Function

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclPointContactPlanesShape` (tipo 10) — kernel `0x141A6F190`, cache `0x141A6E9D0`.
    ''' <para>⛔ **No esta en la reflexion**: su vtable (`0x14270C388`) se referencia en un solo
    ''' sitio (`0x14196092E`), asi que es de **runtime puro** — el shape del TERRENO (cap. 16).
    ''' No sale de ningun archivo.</para>
    ''' <para>Lleva **un plano por particula**, `(n̂.xyz, w)`, indexado por el indice de particula
    ''' (`0x141A6F76C movups xmm6, [rax + rcx*8]`, con `rax` = `[shape+0x18]`).</para>
    ''' <para>⛔⛔ Y su bookkeeping **no es el de los demas**: corta con `dist &lt; 0` **sin restar
    ''' el radio** (`0x141A6F7A1 comiss` sobre el valor al que ya le sacaron el radio) y mete el
    ''' radio **adentro de la superficie** (`S = P − (dist + r)·n̂`, `0x141A6F795`/`9E`/`A5`). La
    ''' fisica termina siendo la misma —la particula queda a `r` del plano— pero los numeros
    ''' intermedios son otros.</para>
    ''' </summary>
    Friend NotInheritable Class PlanosPorParticula
        Inherits Forma

        ''' <summary>`shape+0x18`: un `vec4` por particula, `(n̂, w)`.</summary>
        Friend ReadOnly Planos As Single()

        Friend Sub New(planos As Single())
            MyBase.New(10)
            Me.Planos = planos
        End Sub

        ''' <summary>⛔ **No deriva nada.** Los planos ya vienen en el espacio del mundo: los
        ''' fabrica `computeContactPlanes` cada cuadro (cap. 16), no un archivo con un transform.
        ''' El kernel `0x141A6F190` no toca `coll.transform` para nada.</summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Return Me
        End Function

        ''' <summary>
        ''' ```
        ''' plano = planos[indiceParticula]                  ' 0x141A6F76C
        ''' dist  = dot3(P, plano) + plano.w                 ' 0x141A6F777…92, hsum de 4 lanes
        ''' S     = P − (dist + radioP)·plano                ' 0x141A6F795/9E/A5
        ''' corta si  dist &lt; 0                              ' 0x141A6F79B/A1 comiss / jae
        ''' ```
        ''' <para>⭐⛔ La consecuencia fisica: la particula termina **exactamente sobre** el
        ''' plano, **sin** dejarle su radio (`corr = −dist`). Es el unico shape que hace eso; el
        ''' offset del radio ya viene metido en la `w` que fabrica `computeContactPlanes`
        ''' (cap. 16). Medido en `GP9c`.</para>
        ''' <para>Devuelto por el contrato comun: `Distancia = dist + radioP`, para que el corte
        ''' compartido (`Distancia − radioP &lt; 0`) y la correccion compartida
        ''' (`radioP − dot3(P − S, n̂)`) den **exactamente** lo del binario.</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim pl = Simd.Leer(Planos, indiceParticula)                  ' 0x141A6F76C
            Dim dist = Vector128.Add(Plano.DistanciaAlPlano(p, pl),
                                     Vector128.Create(radioP))              ' 0x141A6F795 addps
            Dim r As Contacto
            r.Normal = pl
            r.Distancia = Simd.Lane0(dist)
            r.Superficie = Vector128.Subtract(p, Vector128.Multiply(dist, pl))  ' 0x141A6F79E/A5
            Return r
        End Function

    End Class



    ' =============================================================================================

    ''' <summary>
    ''' `hclConvexPlanesShape` (tipo 11) — kernel `0x141A01A30`, setup `0x141A019C0`, ctor
    ''' `0x141A0148C` (el que escribe el `0xB` en `shape+0x10`).
    ''' <para>Una **interseccion de semiespacios**: la particula esta adentro si ningun plano la
    ''' deja afuera, y la cara de contacto es la de **mayor** distancia con signo.</para>
    ''' <para>⛔⛔ Y la superficie **no es la proyeccion perpendicular al plano ganador**: es el
    ''' **rayo desde el centro del objeto (`+0xD0`) hacia la particula**, intersecado con esa cara
    ''' (`0x141A01C6A`-`0x141A01D17`). Proyectar perpendicular daria otro punto y otra friccion.</para>
    ''' </summary>
    Friend NotInheritable Class PlanosConvexos
        Inherits Forma

        ''' <summary>`planeEquations` — `+0x18`, `vec4` por plano; la cuenta va en `+0x20`.</summary>
        Friend ReadOnly Planos As Single()
        ''' <summary>`+0x30..+0x60` — `worldToLocal`. Lo pone el setup con `InversaRigida`.</summary>
        Friend ReadOnly DelMundo As Mat4
        ''' <summary>`+0x70..+0xA0` — `localToWorld`, que es el transform del colisionable tal cual
        ''' (`0x141A019E8`-`0x141A01A09`, cuatro `movups`).</summary>
        Friend ReadOnly AlMundoM As Mat4
        ''' <summary>`+0xB0` — el minimo del AABB local (lo acumula el ctor sobre los vertices).</summary>
        Friend ReadOnly AabbMin As Vector128(Of Single)
        ''' <summary>`+0xC0` — el maximo.</summary>
        Friend ReadOnly AabbMax As Vector128(Of Single)
        ''' <summary>`+0xD0` — el centro del objeto, en local. Es el origen del rayo con el que se
        ''' saca el punto de superficie.</summary>
        Friend ReadOnly Centro As Vector128(Of Single)

        Friend Sub New(planos As Single(), delMundo As Mat4, alMundo As Mat4,
                       aabbMin As Vector128(Of Single), aabbMax As Vector128(Of Single),
                       centro As Vector128(Of Single))
            MyBase.New(11)
            Me.Planos = planos
            Me.DelMundo = delMundo
            Me.AlMundoM = alMundo
            Me.AabbMin = aabbMin
            Me.AabbMax = aabbMax
            Me.Centro = centro
        End Sub

        ''' <summary>
        ''' `0x141A019C0`: copia los `0xE0` B del shape, mete el transform tal cual como
        ''' `localToWorld` (`+0x70`) y su **inversa rigida** como `worldToLocal` (`+0x30`,
        ''' `0x141A01A10` → `0x141298100`).
        ''' <para>⛔ Los planos, el AABB y el centro **no se tocan**: viven en el espacio local del
        ''' shape y ahi se quedan. El que viaja es el punto, no la geometria.</para>
        ''' </summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Return New PlanosConvexos(Planos, Mat4.InversaRigida(m), m, AabbMin, AabbMax, Centro)
        End Function

        ''' <summary>
        ''' ```
        ''' pl = ((P.y·W1 + P.x·W0) + P.z·W2) + W3          ' 0x141A01A6A…0x141A01A97
        ''' si NO (min−r &lt;= pl &lt;= max+r) en x,y,z: SIN CONTACTO   ' 0x141A01A9E…0x141A01AB1
        ''' q  = (pl.x, pl.y, pl.z, 1)                      ' 0x141A01ABE/CF
        ''' mejor = (0,0,0, −3,40282002e+38)                ' 0x141A01AD6 (0x142F3C740)
        ''' para cada plano:  d = dot4(plano, q)
        '''     si d &gt; radio: SIN CONTACTO                  ' 0x141A01B64 / 0x141A01C2D
        '''     si mejor.w &lt; d: mejor = (plano.xyz, d)      ' 0x141A01BE9…0x141A01BFA
        ''' u  = pl − centro(+0xD0)                         ' 0x141A01C6A
        ''' û  = u · rsqrtNewtonConGuarda(|u|²)             ' 0x141A01CA3…0x141A01CCF
        ''' t  = dot3(û, mejor)                             ' 0x141A01CD5…0x141A01CED
        ''' S  = ((2 − t·rcp(t))·rcp(t)) · (−d) · û + pl    ' 0x141A01CF0…0x141A01D17
        ''' normal = mejor.xyz × L(3×3)  (SIN traslacion)   ' 0x141A01D20…0x141A01D50
        ''' punto  = S × L (CON traslacion)                 ' 0x141A01D53…0x141A01D8B
        ''' ```
        ''' <para>⛔ La distancia por defecto es **256,0** (`0x142F3C610`), no `FLT_MAX`: se
        ''' escribe al entrar (`0x141A01A53`) y es lo que queda si no hay contacto.</para>
        ''' <para>⛔ El `−FLT_MAX` del acumulador es `3,40282002e+38` = `0x7F7FFFEE`, que **no** es
        ''' `FLT_MAX` (`0x7F7FFFFF`). Se cita el numero del binario.</para>
        ''' <para>⛔ El `1/t` es `rcpps` + **UNA** Newton `(2 − t·r)·r` (`2,0` en `0x142629500`),
        ''' no una division exacta.</para>
        ''' <para>⛔ El empate del maximo lo resuelve la tabla `0x142639E70`
        ''' (`00 00 01 01 02 02 02 02 03×8`, medida), que es el **bit mas alto** de la mascara: con
        ''' varias caras a la misma distancia gana **la ultima**, no la primera.</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim r As Contacto
            r.Distancia = DistanciaPorDefecto                               ' 0x141A01A53 (0x142F3C610)
            r.Normal = Vector128(Of Single).Zero
            r.Superficie = p

            ' (1) al espacio local — la traslacion AL FINAL
            Dim pl = Vector128.Add(Vector128.Multiply(Simd.BcastY(p), DelMundo.F1),
                                   Vector128.Multiply(Simd.BcastX(p), DelMundo.F0))   ' 0x141A01A6A/72/7A
            pl = Vector128.Add(pl, Vector128.Multiply(Simd.BcastZ(p), DelMundo.F2))   ' 0x141A01A7E/82/94
            pl = Vector128.Add(pl, DelMundo.F3)                                       ' 0x141A01A97

            ' (2) rechazo por AABB con margen del radio — sólo x, y, z (movmskps AND 7)
            Dim rad = Vector128.Create(radioP)
            Dim dentro = Vector128.BitwiseAnd(
                Vector128.LessThanOrEqual(pl, Vector128.Add(AabbMax, rad)),
                Vector128.LessThanOrEqual(Vector128.Subtract(AabbMin, rad), pl))       ' 0x141A01A9E/A2/A6
            For l = 0 To 2
                If dentro.GetElement(l) = 0.0F Then Return r                           ' 0x141A01AAC/AF/B1
            Next

            ' (3) el mejor plano
            Dim q = pl.WithElement(Simd.LaneW, 1.0F)                                   ' 0x141A01ABE/CF
            Dim mejorN = Vector128(Of Single).Zero
            Dim mejorD = -Simd.CasiFltMax                                                   ' 0x141A01AD6
            Dim n = Planos.Length \ 4
            If n = 0 Then Return r

            ' ---- los lotes de 4 (0x141A01B10-0x141A01BFD): sar r10d,2 y despues sub 1/js
            Dim lotes = n \ 4
            For b = 0 To lotes - 1
                Dim dLote(3) As Single
                For l = 0 To 3
                    dLote(l) = Simd.Lane0(Dot4(Simd.Leer(Planos, b * 4 + l), q))
                    ' cualquiera de las 4 afuera ⇒ SIN CONTACTO (0x141A01B64 cmpltps radio<d)
                    If radioP < dLote(l) Then Return r
                Next
                ' el maximo del lote y, en el empate, la lane de indice MAS ALTO: la tabla
                ' 0x142639E70 (00 00 01 01 02 02 02 02 03×8, medida) es el bit mas alto de la
                ' mascara `max <= d` (0x141A01B92/96/9C).
                Dim m = Math.Max(Math.Max(dLote(0), dLote(1)), Math.Max(dLote(2), dLote(3)))
                Dim idx = 0
                For l = 0 To 3
                    If m <= dLote(l) Then idx = l
                Next
                ' entre lotes el `cmpltps` es ESTRICTO ⇒ el empate lo gana el lote anterior
                If mejorD < dLote(idx) Then                                              ' 0x141A01BE9
                    mejorD = dLote(idx)
                    mejorN = Simd.Leer(Planos, b * 4 + idx)                              ' 0x141A01BD7
                End If
            Next

            ' ---- la cola, de a uno (0x141A01C10-0x141A01C60), `and r9d, 3`
            For i = lotes * 4 To n - 1
                Dim plano = Simd.Leer(Planos, i)
                Dim d = Simd.Lane0(Dot4(plano, q))
                If d > radioP Then Return r                                              ' 0x141A01C2D ucomiss / ja
                If mejorD < d Then                                                       ' 0x141A01C4C cmpltps
                    mejorD = d
                    mejorN = plano
                End If
            Next

            ' (4) la superficie: el rayo centro→particula contra la cara ganadora
            Dim u = Vector128.Subtract(pl, Centro)                                     ' 0x141A01C6A
            Dim uh = Vector128.Multiply(u, Simd.RsqrtNewtonConGuarda(Simd.Dot3(u, u))) ' 0x141A01CA3…CF
            Dim tt = Simd.Dot3(uh, mejorN)                                             ' 0x141A01CD5…ED
            Dim invT = Simd.RcpNewton(tt)                                              ' 0x141A01CF0…0F
            Dim sLocal = Vector128.Add(
                Vector128.Multiply(Vector128.Multiply(invT, Vector128.Create(-mejorD)),
                                   uh), pl)                                            ' 0x141A01D10/14/17

            ' (5) de vuelta al mundo: la normal SIN traslacion, el punto CON traslacion
            r.Normal = AlMundoSinTraslacion(mejorN, AlMundoM)                           ' 0x141A01D20…50
            r.Superficie = AlMundo(sLocal, AlMundoM)                                    ' 0x141A01D53…8B
            r.Distancia = mejorD                                                        ' 0x141A01D1D
            Return r
        End Function

        ''' <summary>`dot4` con la suma horizontal de las **cuatro** lanes (`shufps 0x4E` y despues
        ''' `0xB1`) — la cola de `0x141A01C19`-`0x141A01C2A`.</summary>
        Private Shared Function Dot4(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            Dim m = Vector128.Multiply(a, b)                                            ' 0x141A01C16
            Dim h = Vector128.Add(Vector128.Shuffle(m, Vector128.Create(2, 3, 0, 1)), m) ' 0x141A01C1C/20
            Return Vector128.Add(Vector128.Shuffle(h, Vector128.Create(1, 0, 3, 2)), h)  ' 0x141A01C26/2A
        End Function

        ''' <summary>`256,0` — `0x43800000` en `0x142F3C610`. La distancia que queda cuando NO hay
        ''' contacto: la escribe al entrar (`0x141A01A53`) y no la pisa si sale por cualquiera de
        ''' los dos rechazos.</summary>
        Friend Const DistanciaPorDefecto As Single = 256.0F



    End Class


    ' =============================================================================================

    ''' <summary>
    ''' `hclConvexGeometryShape` (tipo 9) — despacho `0x1419FFE20`, fuerza bruta `0x141A00320`,
    ''' malla acelerada `0x1419FFF70`.
    ''' <para>La geometria es una **descomposicion en tetraedros**: `tetrahedraEquations`
    ''' (`+0x38`, cuenta `word` en `+0x40`) trae por tetraedro **cuatro filas de 16 B** que son la
    ''' matriz **TRANSPUESTA** de sus 4 planos, para evaluarlos de a cuatro con
    ''' `fila0·x + fila1·y + fila2·z + fila3`.</para>
    ''' <para>⛔⛔ **El margen del radio se aplica SOLO al plano 0.** La comparacion es
    ''' `cmpleps v, (radio, 0, 0, 0)` (`0x141A00455`, con la mascara `0x1427183E0` =
    ''' `(0xFFFFFFFF,0,0,0)`, medida): el plano 0 admite `v &lt;= radio` y los otros tres exigen
    ''' `v &lt;= 0`. El RE decia «si todos `v &lt;= radio`» y **eso es otra cosa**.</para>
    ''' <para>⛔⛔ Y el corte es del **barrido entero**, no de un tetraedro: si el plano 0 falla
    ''' (`test al, 1` / `je`, `0x141A00461`) se abandona la lista completa. Los tetraedros vienen
    ''' ordenados y su plano 0 es el de corte espacial.</para>
    ''' </summary>
    Friend NotInheritable Class GeometriaConvexa
        Inherits Forma

        ''' <summary>`tetrahedraEquations` — `+0x38`, 16 `Single` por tetraedro (4 filas de la
        ''' matriz transpuesta de sus 4 planos). La cuenta sale de `word [+0x40]`.</summary>
        Friend ReadOnly Tetraedros As Single()
        ''' <summary>`+0x110` — `gridRes`, **`uint16`** (`0x1419FFE24 cmp word`). `&gt; 1` manda a la
        ''' malla acelerada; si no, a la fuerza bruta.</summary>
        Friend ReadOnly ResolucionDeGrilla As Integer
        ''' <summary>`+0x50..+0x80` — `localFromWorld`.</summary>
        Friend ReadOnly DelMundo As Mat4
        ''' <summary>`+0x90..+0xC0` — `localToWorld`.</summary>
        Friend ReadOnly AlMundoM As Mat4
        ''' <summary>`+0xD0` / `+0xE0` — el AABB del objeto, en local.</summary>
        Friend ReadOnly AabbMin As Vector128(Of Single)
        Friend ReadOnly AabbMax As Vector128(Of Single)
        ''' <summary>`+0x18` — los OFFSETS de celda, `uint16`: la celda `c` usa los indices
        ''' `[offsets(c), offsets(c+1))` (`0x141A000B4`/`BE`).</summary>
        Friend ReadOnly OffsetsDeCelda As Integer()
        ''' <summary>`+0x28` — los INDICES de tetraedro, **`uint8`** (`0x141A00118 movzx ecx,
        ''' byte`). ⛔ Un shape acelerado no puede tener mas de 256 tetraedros.</summary>
        Friend ReadOnly IndicesDeTetraedro As Integer()
        ''' <summary>`+0x100` — la escala de la grilla: `(pl − aabbMin) · escala` da la celda
        ''' (`0x141A00033`).</summary>
        Friend ReadOnly EscalaDeGrilla As Vector128(Of Single)

        ''' <summary>`+0xF0` — el centro del objeto: el origen del rayo con el que sale la
        ''' superficie.</summary>
        Friend ReadOnly Centro As Vector128(Of Single)

        Friend Sub New(tetraedros As Single(), resolucionDeGrilla As Integer,
                       delMundo As Mat4, alMundo As Mat4,
                       aabbMin As Vector128(Of Single), aabbMax As Vector128(Of Single),
                       centro As Vector128(Of Single),
                       Optional offsetsDeCelda As Integer() = Nothing,
                       Optional indicesDeTetraedro As Integer() = Nothing,
                       Optional escalaDeGrilla As Vector128(Of Single) = Nothing)
            MyBase.New(9)
            Me.Tetraedros = tetraedros
            Me.ResolucionDeGrilla = resolucionDeGrilla
            Me.DelMundo = delMundo
            Me.AlMundoM = alMundo
            Me.AabbMin = aabbMin
            Me.AabbMax = aabbMax
            Me.Centro = centro
            Me.OffsetsDeCelda = offsetsDeCelda
            Me.IndicesDeTetraedro = indicesDeTetraedro
            Me.EscalaDeGrilla = escalaDeGrilla
        End Sub

        ''' <summary>Como el tipo 11: el transform tal cual es el `localToWorld` y su inversa
        ''' rigida el `localFromWorld`. La geometria no viaja.</summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Return New GeometriaConvexa(Tetraedros, ResolucionDeGrilla,
                                        Mat4.InversaRigida(m), m, AabbMin, AabbMax, Centro,
                                        OffsetsDeCelda, IndicesDeTetraedro, EscalaDeGrilla)
        End Function

        ''' <summary>
        ''' ```
        ''' pl = ((P.y·W1 + P.x·W0) + P.z·W2) + W3        ' 0x141A00335…0x141A00365
        ''' dist = 256,0                                  ' 0x141A0036C
        ''' si NO (min−r &lt;= pl &lt;= max+r) en x,y,z: return  ' 0x141A0038B…0x141A0039E
        ''' q = (pl.x, pl.y, pl.z, 1)                     ' 0x141A003C5/D0
        ''' tope = (radio, 0, 0, 0)                       ' 0x141A003B4 (0x1427183E0)
        ''' por cada tetraedro:
        '''     v = ((T0·q.x + T1·q.y) + T2·q.z) + T3     ' 0x141A00431…0x141A0044F
        '''     si (v &lt;= tope) en las CUATRO: HIT
        '''     si NO (v.x &lt;= radio): corta el BARRIDO    ' 0x141A00461/63
        ''' ' el HIT:
        ''' n    = (T0.x, T1.x, T2.x, T3.x)               ' 0x141A00501…07 — la COLUMNA 0
        ''' d    = v.x                                    ' 0x141A00497
        ''' u    = q − centro(+0xF0)                      ' 0x141A0047F
        ''' û    = u · rsqrtNewtonConGuarda(|u|²)         ' 0x141A004C0…F8
        ''' t    = dot3(n, û)                             ' 0x141A00510…2C
        ''' S    = ((2 − t·rcp(t))·rcp(t)) · (−d) · û + q ' 0x141A0052F…45
        ''' ```
        ''' <para>⛔ La normal que sale es la **columna 0** de la matriz transpuesta, o sea el
        ''' plano 0 **entero** — con su `w` en la lane 3. El transform de vuelta sólo usa tres
        ''' lanes, así que esa `w` es residuo, pero se transcribe lo que hay.</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim r As Contacto
            r.Distancia = PlanosConvexos.DistanciaPorDefecto                ' 0x141A0036C
            r.Normal = Vector128(Of Single).Zero
            r.Superficie = p

            Dim pl = Vector128.Add(Vector128.Multiply(Simd.BcastY(p), DelMundo.F1),
                                   Vector128.Multiply(Simd.BcastX(p), DelMundo.F0))   ' 0x141A00338…4C
            pl = Vector128.Add(pl, Vector128.Multiply(Simd.BcastZ(p), DelMundo.F2))   ' 0x141A00350/62
            pl = Vector128.Add(pl, DelMundo.F3)                                       ' 0x141A00365

            Dim rad = Vector128.Create(radioP)
            Dim dentro = Vector128.BitwiseAnd(
                Vector128.LessThanOrEqual(Vector128.Subtract(AabbMin, rad), pl),
                Vector128.LessThanOrEqual(pl, Vector128.Add(AabbMax, rad)))            ' 0x141A0038B/8F/93
            For l = 0 To 2
                If dentro.GetElement(l) = 0.0F Then Return r                           ' 0x141A00399/9C/9E
            Next

            Dim q = pl.WithElement(Simd.LaneW, 1.0F)                                   ' 0x141A003C5/D0
            ' ⛔ el tope: el radio SOLO en la lane 0 (0x1427183E0), cero en las otras tres
            Dim tope = Vector128.Create(radioP, 0.0F, 0.0F, 0.0F)                      ' 0x141A003B4

            ' ⛔ EL DESPACHO (0x1419FFE24 `cmp word [rcx+0x110], 1` / `jbe`): `gridRes > 1` va
            ' a la malla acelerada (0x1419FFF70), si no a la fuerza bruta (0x141A00320).
            Dim candidatos As Integer() = Nothing
            If ResolucionDeGrilla > 1 AndAlso OffsetsDeCelda IsNot Nothing Then
                candidatos = CandidatosDeLaCelda(pl)
            End If

            Dim n = If(candidatos IsNot Nothing, candidatos.Length, Tetraedros.Length \ 16)
            For k = 0 To n - 1
                Dim it = If(candidatos IsNot Nothing, candidatos(k), k)
                Dim t0 = Simd.Leer(Tetraedros, it * 4)
                Dim t1 = Simd.Leer(Tetraedros, it * 4 + 1)
                Dim t2 = Simd.Leer(Tetraedros, it * 4 + 2)
                Dim t3 = Simd.Leer(Tetraedros, it * 4 + 3)
                Dim v = Vector128.Add(Vector128.Multiply(t0, Simd.BcastX(q)),
                                      Vector128.Multiply(t1, Simd.BcastY(q)))          ' 0x141A00431/34/40
                v = Vector128.Add(v, Vector128.Multiply(t2, Simd.BcastZ(q)))           ' 0x141A0043C/4C
                v = Vector128.Add(v, Vector128.Multiply(t3, Simd.BcastW(q)))           ' 0x141A00448/4F

                Dim ok = Vector128.LessThanOrEqual(v, tope)                            ' 0x141A00455
                Dim todas = True
                For l = 0 To 3
                    If ok.GetElement(l) = 0.0F Then todas = False
                Next
                If todas Then
                    Return Golpe(q, v, t0, t1, t2, t3)                                 ' 0x141A00477
                End If
                ' ⛔ si falla el plano 0 se abandona el BARRIDO ENTERO, no este tetraedro
                If ok.GetElement(0) = 0.0F Then Return r                               ' 0x141A00461/63
            Next
            Return r
        End Function

        ''' <summary>
        ''' Los tetraedros candidatos de la celda de grilla — `0x141A0002D`-`0x141A000CE`.
        ''' <para>```
        ''' g   = (pl − aabbMin) · escala(+0x100)            ' 0x141A00030/33
        ''' g   = (g &lt; 0) ? 0 : g                            ' 0x141A00048/50 cmpltps + andnps
        ''' i_k = (bits(g_k + 196608,0) &gt;&gt; 6) AND 0xFFFF     ' 0x141A00053 (0x1427184D0) + shr 6
        ''' si i_k &gt;= res:  i_k = res − 1                    ' 0x141A0008E cmovae
        ''' celda = (i_x·res + i_y)·res + i_z                ' 0x141A00099…AA
        ''' ```</para>
        ''' <para>⛔ La cuantizacion es el **truco del exponente**, no un `CInt`: sumar
        ''' `196608,0` (`0x48400000`, que es `1,5·2^17`) deja `ulp = 1/64`, asi que la mantisa
        ''' baja guarda `round(g·64)` y el `shr 6` la divide. **Redondea al mas cercano antes de
        ''' truncar**: con `g = 0,9999` da 1, no 0.</para>
        ''' <para>⛔ Los offsets son `uint16` y los indices de tetraedro **`uint8`**: por el
        ''' camino acelerado no puede haber mas de 256 tetraedros.</para>
        ''' </summary>
        Private Function CandidatosDeLaCelda(pl As Vector128(Of Single)) As Integer()
            Dim g = Vector128.Multiply(Vector128.Subtract(pl, AabbMin), EscalaDeGrilla)  ' 0x141A00030/33
            ' los negativos a cero — cmpltps contra 0 + andnps (0x141A00048/4D/50)
            Dim neg = Vector128.LessThan(g, Vector128(Of Single).Zero)
            g = Vector128.AndNot(g, neg)
            Dim res = ResolucionDeGrilla
            Dim i(2) As Integer
            For l = 0 To 2
                Dim bits = BitConverter.SingleToUInt32Bits(g.GetElement(l) + MagicoDeCuantizar)
                i(l) = CInt((bits >> 6) And &HFFFFUI)                          ' 0x141A0006C/6F
                If i(l) >= res Then i(l) = res - 1                             ' 0x141A0008E cmovae
            Next
            Dim celda = (i(0) * res + i(1)) * res + i(2)                       ' 0x141A00099…AA
            If celda < 0 OrElse celda + 1 >= OffsetsDeCelda.Length Then Return Array.Empty(Of Integer)()
            Dim ini = OffsetsDeCelda(celda)                                    ' 0x141A000B4
            Dim cuenta = (OffsetsDeCelda(celda + 1) - ini) And &HFFFF          ' 0x141A000BE/C6 sub r11w
            Dim r(cuenta - 1) As Integer
            For j = 0 To cuenta - 1
                r(j) = IndicesDeTetraedro(ini + j) And &HFF                    ' 0x141A00118 movzx byte
            Next
            Return r
        End Function

        ''' <summary>`196608,0` — `0x48400000` en `0x1427184D0`. Es `1,5·2^17`, el numero magico
        ''' que deja `ulp = 1/64` para cuantizar por el exponente.</summary>
        Friend Const MagicoDeCuantizar As Single = 196608.0F

        ''' <summary>El bloque del HIT — `0x141A00477`-`0x141A005BC`.</summary>
        Private Function Golpe(q As Vector128(Of Single), v As Vector128(Of Single),
                               t0 As Vector128(Of Single), t1 As Vector128(Of Single),
                               t2 As Vector128(Of Single), t3 As Vector128(Of Single)) As Contacto
            ' la normal es la COLUMNA 0 de la transpuesta: el plano 0 entero, con su w
            Dim nLocal = Vector128.Create(t0.GetElement(0), t1.GetElement(0),
                                          t2.GetElement(0), t3.GetElement(0))          ' 0x141A00501…07
            Dim d = Simd.Lane0(v)                                                      ' 0x141A00497
            Dim u = Vector128.Subtract(q, Centro)                                      ' 0x141A0047F
            Dim uh = Vector128.Multiply(u, Simd.RsqrtNewtonConGuarda(Simd.Dot3(u, u))) ' 0x141A004C0…F8
            Dim tt = Simd.Dot3(nLocal, uh)                                             ' 0x141A00510…2C
            Dim invT = Simd.RcpNewton(tt)                                              ' 0x141A0052F…3B
            Dim sLocal = Vector128.Add(
                Vector128.Multiply(Vector128.Multiply(invT, Vector128.Create(-d)), uh), q)  ' 0x141A0053E/42/45

            Dim r As Contacto
            r.Distancia = d                                                            ' 0x141A00513
            r.Normal = AlMundoSinTraslacion(nLocal, AlMundoM)                          ' 0x141A0054B…7E
            r.Superficie = AlMundo(sLocal, AlMundoM)                                   ' 0x141A00581…BC
            Return r
        End Function

    End Class


    ' =============================================================================================

    ''' <summary>
    ''' `hclConvexHeightFieldShape` (tipo 5) — envoltorio `0x141A00B00`, muestreo `0x141A00D60`,
    ''' `direccion → cara/uv` `0x141A012E0`, setup `0x141A00A90`.
    ''' <para>Es un **campo de alturas radial sobre un cubo**: para cada direccion desde el origen
    ''' del mapa hay una altura, guardada como `uint8` en una de las **seis caras** del cubo. El
    ''' punto de superficie es `altura · direccion_unitaria`.</para>
    ''' <para>⛔ Las **cuatro** raices inversas de este shape son `rsqrtps` **CRUDAS, sin Newton**
    ''' (`0x141A00E65`, `0x141A011C2`, `0x141A011E9`, `0x141A011FD`, `0x141A01259`), cada una con
    ''' su guarda `cmpleps` contra 0. Es el shape menos preciso del motor y asi es.</para>
    ''' </summary>
    Friend NotInheritable Class CampoDeAlturasConvexo
        Inherits Forma

        ''' <summary>`+0x18` — `resolution`, `uint16` (`0x141A00FE6 movzx eax, word`).</summary>
        Friend ReadOnly Resolucion As Integer
        ''' <summary>`+0x1A` — `resIncBorder`, `uint16` (`0x141A00F09`). Es la resolucion **con** el
        ''' borde de un texel que el muestreo usa para los vecinos.</summary>
        Friend ReadOnly ResolucionConBorde As Integer
        ''' <summary>`+0x30` — `heights`, un `uint8` por texel.</summary>
        Friend ReadOnly Alturas As Byte()
        ''' <summary>`+0x40` — el offset de cada una de las **seis** caras dentro de `heights`
        ''' (`0x141A00F3C mov edx, [rbp + rsi*4 + 0x40]`).</summary>
        Friend ReadOnly OffsetsDeCara As Integer()
        ''' <summary>`+0x60..+0x90` — `localToMapTransform`, **ya compuesto** con el transform del
        ''' colisionable por el setup.</summary>
        Friend ReadOnly AlMapa As Mat4
        ''' <summary>`+0xA0` — `localToMapScale`. Las lanes 0-2 escalan las coordenadas del cubo;
        ''' la **`w` (`+0xAC`) es la escala de altura**, y el muestreo usa su reciproco.</summary>
        Friend ReadOnly EscalaDelMapa As Vector128(Of Single)

        Friend Sub New(resolucion As Integer, resolucionConBorde As Integer, alturas As Byte(),
                       offsetsDeCara As Integer(), alMapa As Mat4,
                       escalaDelMapa As Vector128(Of Single))
            MyBase.New(5)
            Me.Resolucion = resolucion
            Me.ResolucionConBorde = resolucionConBorde
            Me.Alturas = alturas
            Me.OffsetsDeCara = offsetsDeCara
            Me.AlMapa = alMapa
            Me.EscalaDelMapa = escalaDelMapa
        End Sub

        ''' <summary>
        ''' `0x141A00A90`: copia los `0xB0` B del shape y **compone** su `localToMapTransform` con
        ''' el transform del colisionable (`0x141A00AE1` → `0x141298780`), que es
        ''' `mapa ∘ inversaRigida(transform)`.
        ''' <para>⛔ Asi el kernel recibe la particula **en el mundo** y la lleva al mapa de una
        ''' sola pasada; el shape no se mueve.</para>
        ''' </summary>
        Friend Overrides Function Derivar(m As Mat4) As Forma
            Return New CampoDeAlturasConvexo(Resolucion, ResolucionConBorde, Alturas, OffsetsDeCara,
                                             Mat4.ComponerConInversa(AlMapa, m), EscalaDelMapa)
        End Function

        ''' <summary>
        ''' `0x141A00B00`, **28 instrucciones**: muestrea y despues
        ''' `dist = ((d.y + d.x) + d.z)` con `d = (P − punto)·normal`.
        ''' <para>⛔ El radio entra en el **muestreo**, no en la distancia (`0x141A00B1F`
        ''' `movups xmm2, [r8]` se pasa a `0x141A00D60`).</para>
        ''' </summary>
        Friend Overrides Function PuntoMasCercano(p As Vector128(Of Single),
                                                  indiceParticula As Integer,
                                                  radioP As Single,
                                                  enResto As Boolean) As Contacto
            Dim punto As Vector128(Of Single) = Nothing
            Dim normal As Vector128(Of Single) = Nothing
            Muestrear(p, radioP, punto, normal)                            ' 0x141A00B28
            Dim r As Contacto
            r.Superficie = punto
            r.Normal = normal
            r.Distancia = Simd.Lane0(Simd.Dot3(Vector128.Subtract(p, punto), normal))  ' 0x141A00B2D…5D
            Return r
        End Function

        ''' <summary>
        ''' El muestreo — `0x141A00D60`.
        ''' <para>```
        ''' pl   = AlMapa × P                                ' 0x141A00DCD → 0x141339F90
        ''' d    = pl · rsqrtCRUDOconGuarda(|pl|²)           ' 0x141A00E61/65/6B/78
        ''' rc   = rcpps(escala) + UNA Newton (2 − e·r)·r    ' 0x141A00E68/6E/75/7B/83
        ''' (cara,u,v,fu,fv) = direccionACaraUV(d, escala)   ' 0x141A00E8B → 0x141A012E0
        ''' su, fu = (fu &gt;= 0,5) ? (+1, 1−fu) : (−1, fu)     ' 0x141A00EAA…D0
        ''' sv, fv = (fv &gt;= 0,5) ? (+1, 1−fv) : (−1, fv)     ' 0x141A00EDC…FE
        ''' s    = su·sv                                     ' 0x141A00EC2 / 0x141A00EF0
        ''' base = (u+1)·R + caras[cara]                     ' 0x141A00F2D/34/3C
        ''' t    = rc.w                                      ' 0x141A00F0E [rsp+0x7C]
        ''' h00  = alturas[base + v + 1]·t                   ' 0x141A00F46 — ⛔ el +1 del borde
        ''' h0s  = alturas[base + sv + v + 1]·t              ' 0x141A00F51/55/61
        ''' h1s  = alturas[(u+1+su)·R + caras[cara] + v + 1]·t ' 0x141A00F67…8C
        ''' H    = ( ((1−2fv)·h0s + 2fv·h00) + ((1−2fu)·h1s + 2fu·h00) ) · 0,5  ' 0x141A00F29…CE
        ''' punto = inversaParaPuntos( H · d )               ' 0x141A00FD5/D9 + 0x141A00FE1
        ''' ```</para>
        ''' <para>⛔ `fv` va con `h0s` (el vecino en **v**) y `fu` con `h1s` (el vecino en **u**).
        ''' Cruzarlos da una superficie que se dobla al reves.</para>
        ''' </summary>
        Friend Sub Muestrear(p As Vector128(Of Single), radioP As Single,
                             ByRef punto As Vector128(Of Single), ByRef normal As Vector128(Of Single))
            Dim pl = AlMundo(p, AlMapa)                                    ' 0x141339F90
            Dim d = Vector128.Multiply(pl, Simd.RsqrtConGuarda(Simd.Dot3(pl, pl)))  ' 0x141A00E65 CRUDO
            Dim rc = Simd.RcpNewton(EscalaDelMapa)                         ' 0x141A00E68…83

            Dim cara As Integer, u As Integer, v As Integer
            Dim fu As Single, fv As Single
            DireccionACaraUV(d, cara, u, v, fu, fv)                        ' 0x141A012E0
            If cara < 0 Then
                punto = Vector128(Of Single).Zero
                normal = Vector128(Of Single).Zero
                Return
            End If

            Dim su = 1, sv = 1
            Dim s = 1.0F
            If fu >= 0.5F Then                                             ' 0x141A00EAA comiss / jae
                fu = 1.0F - fu
            Else
                su = -1 : s = -1.0F                                        ' 0x141A00EBC/C2
            End If
            If fv >= 0.5F Then                                             ' 0x141A00EDC
                fv = 1.0F - fv
            Else
                sv = -1 : s = -s                                           ' 0x141A00EEA/F0 xorps con −0
            End If

            Dim R = ResolucionConBorde
            Dim baseU = (u + 1) * R + OffsetsDeCara(cara)                  ' 0x141A00F2D/34/3C
            Dim t = rc.GetElement(Simd.LaneW)                              ' 0x141A00F0E
            Dim h00 = CSng(Alturas(baseU + v + 1)) * t                     ' 0x141A00F46/5D/71
            Dim h0s = CSng(Alturas(baseU + sv + v + 1)) * t                ' 0x141A00F51/55/61/7B/95
            Dim h1s = CSng(Alturas((u + 1 + su) * R + OffsetsDeCara(cara) + v + 1)) * t  ' 0x141A00F67…AF

            Dim dosFu = fu * 2.0F                                          ' 0x141A00F29 (0x142929498)
            Dim dosFv = fv * 2.0F                                          ' 0x141A00F30
            Dim enV = (1.0F - dosFv) * h0s + dosFv * h00                   ' 0x141A00F38/87/A2/AB
            Dim enU = (1.0F - dosFu) * h1s + dosFu * h00                   ' 0x141A00FB4/B8/BC/C1/C6
            Dim h = (enV + enU) * 0.5F                                     ' 0x141A00FCA/CE (0x142929448)

            punto = InversaParaPuntos(Vector128.Multiply(Vector128.Create(h), d), AlMapa)  ' 0x141A00FD5…E1

            ' ---- la normal: los tres puntos del cubo, su cruz, y el signo su·sv
            Dim paso = 2.0F / CSng(Resolucion)                             ' 0x141A0100F divss EXACTO
            Dim cc = 1.0F / CSng(Resolucion)                               ' 0x141A01013 divss EXACTO
            Dim a = CSng(u) * paso + cc - 1.0F                             ' 0x141A01017/1F/2E
            Dim b = CSng(v) * paso + cc - 1.0F                             ' 0x141A0101B/26/37
            Dim a2 = CSng(su) * paso + a                                   ' 0x141A0102A/41
            Dim b2 = CSng(sv) * paso + b                                   ' 0x141A0103C/45

            Dim q0, q1, q2 As Vector128(Of Single)
            Select Case cara                                               ' 0x141A0104A…0x141A01151
                Case 0                                                     ' +X — 0x141A0111B
                    q0 = Vector128.Create(1.0F, b, a, 0.0F)
                    q1 = Vector128.Create(1.0F, b, a2, 0.0F)
                    q2 = Vector128.Create(1.0F, b2, a, 0.0F)
                    s = -s                                                 ' 0x141A01136 xorps
                Case 1                                                     ' −X — 0x141A010FA
                    q0 = Vector128.Create(-1.0F, b, a, 0.0F)
                    q1 = Vector128.Create(-1.0F, b, a2, 0.0F)
                    q2 = Vector128.Create(-1.0F, b2, a, 0.0F)
                Case 2                                                     ' +Y — 0x141A010D0
                    q0 = Vector128.Create(a, 1.0F, b, 0.0F)
                    q1 = Vector128.Create(a2, 1.0F, b, 0.0F)
                    q2 = Vector128.Create(a, 1.0F, b2, 0.0F)
                    s = -s                                                 ' 0x141A010DE
                Case 3                                                     ' −Y — 0x141A010A7
                    q0 = Vector128.Create(a, -1.0F, b, 0.0F)
                    q1 = Vector128.Create(a2, -1.0F, b, 0.0F)
                    q2 = Vector128.Create(a, -1.0F, b2, 0.0F)
                Case 4                                                     ' +Z — 0x141A0108E
                    ' ⛔ La 4 es +Z y la 5 es −Z. El `cmp esi, 1` / `je 0x141A0108E` de
                    ' `0x141A01074` manda la cara 4 al bloque que usa `+1` y NO niega; la 5 cae
                    ' en `0x141A01079`, que usa `−1` y niega en `0x141A01136`. Y la tabla de
                    ' caras pone el eje Z positivo en la 4, asi que cierra.
                    ' Yo las tenia al reves: lo delato GY10 (la normal salia hacia adentro).
                    q0 = Vector128.Create(a, b, 1.0F, 0.0F)
                    q1 = Vector128.Create(a2, b, 1.0F, 0.0F)
                    q2 = Vector128.Create(a, b2, 1.0F, 0.0F)
                Case Else                                                  ' −Z — 0x141A01079
                    q0 = Vector128.Create(a, b, -1.0F, 0.0F)
                    q1 = Vector128.Create(a2, b, -1.0F, 0.0F)
                    q2 = Vector128.Create(a, b2, -1.0F, 0.0F)
                    s = -s                                                 ' 0x141A01136
            End Select

            Dim s0 = Vector128.Multiply(Vector128.Multiply(q0, Simd.RsqrtConGuarda(Simd.Dot3(q0, q0))),
                                        Vector128.Create(h00))             ' 0x141A011C2…E6
            Dim e1 = Vector128.Subtract(
                Vector128.Multiply(Vector128.Multiply(q1, Simd.RsqrtConGuarda(Simd.Dot3(q1, q1))),
                                   Vector128.Create(h1s)), s0)             ' 0x141A011E9…0x141A01200
            Dim e2 = Vector128.Subtract(
                Vector128.Multiply(Vector128.Multiply(q2, Simd.RsqrtConGuarda(Simd.Dot3(q2, q2))),
                                   Vector128.Create(h0s)), s0)             ' 0x141A011FD…0x141A0121D

            ' el cruz ROTADO, la misma forma que `Cuaternion.Producto` (0x141A01220…34)
            Dim cr = Vector128.Subtract(
                Vector128.Multiply(Vector128.Shuffle(e2, Vector128.Create(1, 2, 0, 3)), e1),
                Vector128.Multiply(Vector128.Shuffle(e1, Vector128.Create(1, 2, 0, 3)), e2))
            cr = Vector128.Shuffle(cr, Vector128.Create(1, 2, 0, 3))
            cr = Vector128.Multiply(cr, Vector128.Create(s))                ' 0x141A01238
            cr = Vector128.Multiply(cr, Simd.RsqrtConGuarda(Simd.Dot3(cr, cr)))  ' 0x141A01259…67
            normal = InversaParaNormales(cr, AlMapa)                        ' 0x141A0127A → 0x141339F20
        End Sub

        ''' <summary>
        ''' `dirección → (cara, u, v, fu, fv)` — `0x141A012E0`.
        ''' <para>```
        ''' a    = |dir| por BITS (pslld/psrld)              ' 0x141A012EB/F3
        ''' m    = max(max(|y|,|x|), |z|)
        ''' eje  = tabla[0x142639E70][ movmskps(m &lt;= a) AND 0x7 ]   ' 0x141A01337/4B
        ''' cara = (0 &gt; dir[eje]) ? {1,3,5}[eje] : {0,2,4}[eje]     ' 0x141A0134F…5F
        ''' q    = (|dir[eje]| != 0) ? dir/|dir[eje]| : dir         ' 0x141A01376/83 divss EXACTO
        ''' t    = ((1,1,1,0) + q) · 0,5 · escala                   ' 0x141A01391…A8
        ''' i_k  = (bits(t_k + 196608,0) &gt;&gt; 6) AND 0xFFFF          ' 0x141A013B3…F0
        ''' frac = t − (float)(i_0, i_1, i_2, 0)                    ' 0x141A013E8…0x141A01406
        ''' eje 0: u = i_2, v = i_1, fu = frac.z, fv = frac.y       ' 0x141A0143C…53
        ''' eje 1: u = i_0, v = i_2, fu = frac.x, fv = frac.z       ' 0x141A01425…3A
        ''' eje 2: u = i_0, v = i_1, fu = frac.x, fv = frac.y       ' 0x141A0141B…53
        ''' ```</para>
        ''' <para>⛔ La mascara `0x142718550` (`FFFFFFFF ×3, 00000000`, medida) **anula la lane 3**
        ''' antes del `movmskps`, asi que el eje nunca puede ser 3. Si lo fuera, el motor sale
        ''' **sin escribir nada** (`0x141A01419 jne`) y el llamador se queda con los `−1,0` que
        ''' dejo. Aca se devuelve `cara = −1` para no fabricar un contacto.</para>
        ''' <para>⛔ La constante que suma antes de escalar es `(1, 1, 1, 0)`
        ''' (`0x142483E80`, medida): la lane 3 **no** lleva el `+1`, y por eso `t` queda en
        ''' `[0, escala]` en x, y, z — lo que acota el indice del texel por construccion.
        ''' ⛔ Yo la habia transcrito como `(1, 0, 0, 0)` y eso mandaba `t.y`/`t.z` a negativo:
        ''' el indice se iba de rango. Lo delato una mutacion que reventaba el arnes.</para>
        ''' <para>⛔ Y la division por la componente dominante es `divss` **exacta**, con un
        ''' `ucomiss` contra 0 que la **saltea** si esa componente es cero.</para>
        ''' </summary>
        Friend Sub DireccionACaraUV(dir As Vector128(Of Single), ByRef cara As Integer,
                                    ByRef u As Integer, ByRef v As Integer,
                                    ByRef fu As Single, ByRef fv As Single)
            cara = -1 : u = 0 : v = 0 : fu = -1.0F : fv = -1.0F            ' 0x141A00DEC/E15

            Dim ab = Vector128.ShiftRightLogical(
                Vector128.ShiftLeft(dir.AsInt32(), 1), 1).AsSingle()        ' 0x141A012EB/F3
            Dim m = Math.Max(Math.Max(ab.GetElement(1), ab.GetElement(0)), ab.GetElement(2))
            Dim eje = -1
            For l = 0 To 2
                If m <= ab.GetElement(l) Then eje = l                       ' el bit MAS ALTO
            Next
            If eje < 0 Then Return
            ' {0,2,4} si la componente es >= 0, {1,3,5} si es < 0 — 0x1427185F0 + [rsp+0x10]/[0x14]
            cara = If(0.0F > dir.GetElement(eje), 1 + eje * 2, eje * 2)     ' 0x141A0134F comiss / jbe

            Dim q = dir
            Dim den = ab.GetElement(eje)
            If den <> 0.0F Then                                             ' 0x141A01376 ucomiss / je
                q = Vector128.Multiply(dir, Vector128.Create(
                    Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F), Vector128.Create(den)))))
            End If                                                          ' 0x141A01383 divss

            Dim t = Vector128.Multiply(
                Vector128.Multiply(Vector128.Add(Vector128.Create(1.0F, 1.0F, 1.0F, 0.0F), q),
                                   Vector128.Create(0.5F)), EscalaDelMapa)  ' 0x141A0139B/A1/A8
            Dim i0 = Cuantizar(t.GetElement(0))
            Dim i1 = Cuantizar(t.GetElement(1))
            Dim i2 = Cuantizar(t.GetElement(2))
            Dim frac = Vector128.Subtract(t, Vector128.Create(CSng(i0), CSng(i1), CSng(i2), 0.0F))

            Select Case eje
                Case 0 : u = i2 : v = i1 : fu = frac.GetElement(2) : fv = frac.GetElement(1)
                Case 1 : u = i0 : v = i2 : fu = frac.GetElement(0) : fv = frac.GetElement(2)
                Case Else : u = i0 : v = i1 : fu = frac.GetElement(0) : fv = frac.GetElement(1)
            End Select
        End Sub

        ''' <summary>El truco del exponente con `196608,0` (`0x1427185E0`), el mismo que la grilla
        ''' del tipo 9 — `0x141A013B3`-`0x141A013F0`.</summary>
        Private Shared Function Cuantizar(x As Single) As Integer
            Return CInt((BitConverter.SingleToUInt32Bits(
                x + GeometriaConvexa.MagicoDeCuantizar) >> 6) And &HFFFFUI)
        End Function

        ''' <summary>La inversa rigida aplicada a un **punto**, sin materializarla —
        ''' `0x141339FD0`: `d = v − M.F3` y despues `((d.y·Rᵀ1 + d.x·Rᵀ0) + d.z·Rᵀ2)`.</summary>
        Private Shared Function InversaParaPuntos(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Return PorTranspuesta(Vector128.Subtract(v, m.F3), m)           ' 0x141339FEA
        End Function

        ''' <summary>Idem para una **normal**: igual pero **sin restar la traslacion**
        ''' (`0x141339F20` no tiene el `subps` de `0x141339FEA`).</summary>
        Private Shared Function InversaParaNormales(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Return PorTranspuesta(v, m)
        End Function

        Private Shared Function PorTranspuesta(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim t0 = Vector128.Create(m.F0.GetElement(0), m.F1.GetElement(0), m.F2.GetElement(0), 0.0F)
            Dim t1 = Vector128.Create(m.F0.GetElement(1), m.F1.GetElement(1), m.F2.GetElement(1), 0.0F)
            Dim t2 = Vector128.Create(m.F0.GetElement(2), m.F1.GetElement(2), m.F2.GetElement(2), 0.0F)
            Dim r = Vector128.Add(Vector128.Multiply(Simd.BcastY(v), t1),
                                  Vector128.Multiply(Simd.BcastX(v), t0))
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), t2))
        End Function

    End Class

End Namespace

#End If
