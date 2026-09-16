Option Strict On
Option Explicit On

' =================================================================================================
' EL INTERRUPTOR de la física Havok, y sus perillas.
'
' `Enabled = False` (el DEFAULT) deja el render EXACTAMENTE como estaba: la capa
' `HierarchiBone_class.PhysicsDeltaTransform` queda en Nothing y componer con Nothing es no-op, así
' que el resultado es bit-idéntico al de antes de que existiera este módulo. Eso es lo que permite
' comparar CON y SIN física sin recargar nada.
'
' Los defaults NO son inventados: salen del RE del motor (Tools/re-docs/RE_FO4_CLOTH_PHYSICS.md) y
' del censo del corpus vanilla (`HkxLoadOrderAudit --clothengine`, 342 hclSimClothData):
'   · fMaxRootDistanceBeforeTeleport = 100 u   ·   fMaxRootAngleBeforeTeleport = π/2
'   · subSteps: el 100 % del corpus trae 0 en simulationInfo ⇒ el motor SIEMPRE cae al del
'     hclSimulateOperator, que en vanilla vale 1 (48), 2 (232), 3 (56) o 4 (6).
'   · numberOfSolveIterations = 1 en los 342.
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>Qué tan lejos se lleva la física.</summary>
    Public Enum HavokPhysicsMode
        ''' <summary>Nada. Idéntico a Enabled=False.</summary>
        [Off] = 0
        ''' <summary>
        ''' SÓLO el operador de hueso: las partículas se toman de la malla SKINNEADA en la pose actual
        ''' y se corre `bind × M` + ortonormalización. NO hay integración, así que no puede explotar y
        ''' en reposo es un no-op exacto. Es el efecto "el pelo sigue a la cabeza" sin inercia.
        ''' </summary>
        DeformOnly = 1
        ''' <summary>El bucle completo del motor: substeps, Verlet, anclas interpoladas, constraints y colisión.</summary>
        FullSimulation = 2
    End Enum

    ''' <summary>
    ''' El valor más alto de <see cref="HavokPhysicsMode"/>, DERIVADO del enum.
    ''' <para>⛔ Existe porque el techo escrito a mano ya quedó viejo una vez y costó una medición
    ''' falsa: `Config_App.ApplyHavokPhysicsSettings` recortaba con `Math.Min(2, …)` de cuando el enum
    ''' llegaba hasta `FullSimulation`. Al agregar `MotorCanonico = 3` el recorte lo devolvió a 2 en
    ''' silencio, así que el arnés pidió el motor canónico y corrió el viejo — y el A/B dio "x1,
    ''' idénticos", que es exactamente lo que uno querría creer.</para>
    ''' <para>Se calcula UNA vez: el volcado de config corre en cada frame y `GetValues` reflexiona.</para>
    ''' </summary>
    Public Module RangoDeModo
        Public ReadOnly Maximo As Integer = MaximoDelEnum()

        Private Function MaximoDelEnum() As Integer
            Dim m = 0
            For Each v In [Enum].GetValues(GetType(HavokPhysicsMode))
                m = Math.Max(m, CInt(v))
            Next
            Return m
        End Function
    End Module

    ''' <summary>Ajustes globales de la física Havok. Todo estático: es una perilla de sesión, no estado.</summary>
    Public NotInheritable Class HavokPhysicsSettings

        Private Sub New()
        End Sub

        ''' <summary>
        ''' ⭐ EL INTERRUPTOR. False = el render queda exactamente como sin este módulo.
        ''' <para>⚠️ El pase de render vuelca `Config_App.Current.Setting_HavokPhysics` acá en CADA
        ''' frame (`Config_App.ApplyHavokPhysicsSettings`). O sea: **la config gana**. Si lo ponés a
        ''' mano desde el depurador, el próximo frame te lo pisa. Para probar, cambiá la clave de
        ''' `config.json` — es la fuente de verdad a propósito, para que no haya dos.</para>
        ''' </summary>
        Private Shared _enabled As Boolean = False
        Public Shared Property Enabled As Boolean
            Get
                Return _enabled
            End Get
            Set(value As Boolean)
                Dim wasOn = _enabled
                _enabled = value
                ' ⛔ LIMPIAR EN LA TRANSICIÓN, no esperar al próximo frame de física. El pase de render
                ' sólo llama a `StepShapes` en la rama de actualización de POSE; si el usuario apaga el
                ' interruptor y el siguiente evento es un cambio de preset (rama de morph) o de textura,
                ' el `PhysicsDeltaTransform` del último frame simulado seguiría compuesto en el hueso.
                ' Apagar tiene que limpiar por sí solo, no depender de qué evento venga después.
                If wasOn AndAlso Not value Then ClothCanonico.LimpiarTodos()
            End Set
        End Property

        ''' <summary>Cuánto se corre cuando Enabled=True. Ver la nota de <see cref="Enabled"/>:
        ''' `Setting_HavokPhysicsMode` de la config lo pisa en cada frame.</summary>
        Public Shared Property Mode As HavokPhysicsMode = HavokPhysicsMode.FullSimulation

        ''' <summary>
        ''' ⭐ ¿Este modo corre el `hclClothState` que declara el `hclSimulateOperator`?
        ''' <para>⛔ La pregunta se hace ACÁ y en un solo lugar. Preguntar `Mode = FullSimulation` en el
        ''' punto de uso ya costó una corrida entera: `MotorCanonico` no es `FullSimulation`, así que
        ''' `SeleccionarEstado` eligió el estado SIN simulador y el motor canónico produjo, bit a bit,
        ''' lo mismo que `DeformOnly` — con el gate en verde y el PNG idéntico al de DeformOnly.</para>
        ''' <para>`MotorCanonico` **es** simulación: lo único que cambia es QUIÉN integra las
        ''' partículas, no qué estado del archivo se corre.</para>
        ''' </summary>
        Public Shared ReadOnly Property CorreSimulacion As Boolean
            Get
                Return Mode = HavokPhysicsMode.FullSimulation
            End Get
        End Property

        ''' <summary>
        ''' Los milisegundos del cuadro que recibe el reloj de la tela (`RelojDelMundo`).
        ''' <para>−1 (el default) = el reloj REAL entre renders, que es lo que hace el timer del
        ''' juego (`0x14165BA10`). Un valor ≥ 0 lo fija: lo usan los arneses para que dos corridas den
        ''' el mismo número.</para>
        ''' <para>⛔ Acá había un paso FIJO de 1/60 s por render. Con un clip de 30 Hz la tela vivía a la
        ''' mitad del tiempo real. El paso lo decide el motor (`bhkWorld::SetDeltaTime`), no la app.</para>
        ''' </summary>
        Public Shared Property MilisegundosDelCuadro As Long = -1


        ''' <summary>`fMaxRootDistanceBeforeTeleport` (100 u). Más que eso ⇒ reponer, no simular.</summary>
        Public Shared Property MaxRootDistanceBeforeTeleport As Single = 100.0F

        ''' <summary>`fMaxRootAngleBeforeTeleport` (π/2 rad).
        ''' <para>⛔ ESCRITO COMO PI/2, QUE ES LO QUE ES: un literal decimal no deja ver de donde sale.
        ''' MEDIDO: `CSng(Math.PI/2)` = 0x3FC90FDB = 1,5707963705, que es el Single MAS CERCANO a pi/2;
        ''' el literal que habia (`1.5707963F` = 0x3FC90FDA = 1,5707962513) esta 1 ULP por debajo. La
        ''' diferencia es 1,19e-7 rad sobre un umbral de teleport, o sea nada — pero el valor correcto
        ''' es el de arriba y ahora se ve de donde sale.</para></summary>
        Public Shared Property MaxRootAngleBeforeTeleport As Single = CSng(Math.PI / 2.0)

    End Class

End Namespace
