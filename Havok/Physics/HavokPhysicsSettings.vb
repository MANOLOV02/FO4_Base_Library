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
'   · uNumSimSettleSteps = 10           (default compilado en Fallout4.exe)
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
                If wasOn AndAlso Not value Then HavokClothSimulation.ClearAllTouchedSkeletons()
            End Set
        End Property

        ''' <summary>Cuánto se corre cuando Enabled=True. Ver la nota de <see cref="Enabled"/>:
        ''' `Setting_HavokPhysicsMode` de la config lo pisa en cada frame.</summary>
        Public Shared Property Mode As HavokPhysicsMode = HavokPhysicsMode.FullSimulation

        ''' <summary>0 = usar el authored del hclSimulateOperator (lo que hace el motor). &gt;0 = pisarlo.</summary>
        Public Shared Property SubstepOverride As Integer = 0

        ''' <summary>0 = usar el authored (vanilla: 1 en el 100 % del corpus). &gt;0 = pisarlo.</summary>
        Public Shared Property SolveIterationOverride As Integer = 0

        ''' <summary>Multiplica la gravedad authored. 1.0 = fiel al archivo.</summary>
        Public Shared Property GravityScale As Single = 1.0F

        ''' <summary>
        ''' dt del paso. El previewer no tiene el reloj del juego, así que por defecto se usa un paso
        ''' FIJO: con dt variable el Verlet cambia de resultado según la carga de la máquina y el
        ''' render dejaría de ser reproducible (y RENDER == BAKE dejaría de poder cumplirse).
        ''' </summary>
        Public Shared Property FixedTimeStep As Single = 1.0F / 60.0F
        ''' <summary>Tiempo REAL transcurrido desde el frame anterior. 0 = "un paso fijo", que es el
        ''' caso del render interactivo. Un reproductor de animacion que avanza la pose un frame de
        ''' clip por cuadro tiene que poner aca la duracion de ESE frame: la fisica parte el intervalo
        ''' en pasos de <see cref="FixedTimeStep"/>, igual que el motor, que simula a su ritmo y no al
        ''' ritmo al que le llega la pose.</summary>
        Public Shared Property FrameDeltaSeconds As Single = 0.0F

        ''' <summary>`uNumSimSettleSteps` del motor: pasos de asentamiento al activar una prenda.</summary>
        Public Shared Property SettleSteps As Integer = 10

        ''' <summary>`fMaxRootDistanceBeforeTeleport` (100 u). Más que eso ⇒ reponer, no simular.</summary>
        Public Shared Property MaxRootDistanceBeforeTeleport As Single = 100.0F

        ''' <summary>`fMaxRootAngleBeforeTeleport` (π/2 rad).</summary>
        Public Shared Property MaxRootAngleBeforeTeleport As Single = 1.5707963F

        ''' <summary>Colisión contra las cápsulas del cuerpo. Se puede apagar para aislar el efecto.</summary>
        Public Shared Property EnableCollision As Boolean = True

        ''' <summary>La correa `hclLocalRangeConstraintSet`. Sin ella la tela sobre-cae (medido).</summary>
        Public Shared Property EnableLocalRange As Boolean = True

        ''' <summary>`hclBendStiffnessConstraintSet` — la rigidez de flexion, el cuarto constraint
        ''' set del motor. Prendido: es parte de la ley, no un extra. La perilla existe para poder
        ''' hacer el A/B y atribuir un cambio de forma a ESTE solver y no a otro.</summary>
        Public Shared Property EnableBend As Boolean = True

        ''' <summary>
        ''' A/B de la <b>rama adaptativa</b> de `hclLocalRangeConstraintSet` y
        ''' `hclBonePlanesConstraintSet` — el 6.º argumento del `solve` virtual.
        '''
        ''' <para>⛔ NO ES UN MODO LEGACY: apagarla corre la OTRA rama del motor (`0x141A02700` /
        ''' `0x1419FCBD0`), que es la que el propio motor usa cuando `subSteps = 1`. Existe por la
        ''' misma razon que <see cref="EnableBend"/> y <see cref="EnableLocalRange"/>: para poder
        ''' ATRIBUIR un cambio de forma a esta rama y no a otra cosa. El default es el
        ''' comportamiento del motor.</para>
        ''' <para>La rama adaptativa reescribe `Previous` despues de corregir la posicion, de modo que
        ''' la correccion no inyecta velocidad. Corre cuando el operador declara
        ''' `adaptConstraintStiffness` Y `subSteps &gt; 1`.</para>
        ''' </summary>
        Public Shared Property EnableAdaptiveConstraints As Boolean = True

        ''' <summary>Deja el estado de simulación limpio (todas las prendas vuelven a sembrar).</summary>
        Public Shared Sub ResetAll()
            HavokClothSimulation.ResetAll()
        End Sub

        Public Shared Function Describe() As String
            If Not Enabled Then Return "física Havok: APAGADA"
            Return $"física Havok: {Mode} · dt={FixedTimeStep:0.####} · substeps={If(SubstepOverride = 0, "authored", CStr(SubstepOverride))}" &
                   $" · iters={If(SolveIterationOverride = 0, "authored", CStr(SolveIterationOverride))}" &
                   $" · gravedad×{GravityScale:0.##} · colisión={EnableCollision} · correa={EnableLocalRange}"
        End Function
    End Class

End Namespace
