Option Strict On
Option Explicit On

' =================================================================================================
' LOS AJUSTES [Cloth] DEL JUEGO — los `Setting` de Bethesda que lee la capa de tela.
'
' Cada `Setting` es un objeto de `.data` `{vtbl 0x142467DE8, valor (+8), nombre (+0x10)}` que su
' inicializador estático sólo registra en la colección (`0x14022D910` → `call [r8+8]`). El código lee
' el VALOR VIVO (`+8`). Sin sección [Cloth] en los INI del usuario rigen los valores de la imagen, que
' son los de abajo. Los lectores citados son TODOS los del `.text` (barrido de referencias rip
' decodificadas, 15-sep).
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>Los valores de los ajustes [Cloth]. `Defecto` = la imagen del `.exe`.</summary>
    Friend NotInheritable Class AjustesDeTela

        ''' <summary>Los valores de la imagen del `.exe` (sin sección [Cloth] en los INI).</summary>
        Friend Shared ReadOnly Defecto As New AjustesDeTela()

        ''' <summary>`bAnimClothLOD:Cloth` — objeto `0x142F4FB80`, 1. Lector `0x1418A7A02`.</summary>
        Friend BAnimClothLOD As Boolean = True
        ''' <summary>`fClothLODDistanceSqr:Cloth` — `0x142F357D0`, 5.000.000. Lectores `0x140DB3C47`, `0x140DB3DAC`.</summary>
        Friend FClothLODDistanceSqr As Single = 5000000.0F
        ''' <summary>`fClothTimingBudget:Cloth` — `0x142F357E8`, 0,01 (`0x3C23D70A`). Lector `0x140DB3DD0`.</summary>
        Friend FClothTimingBudget As Single = 0.01F
        ''' <summary>`uMaxClothCount:Cloth` (primer objeto) — `0x142F35800`, 100. Lector `0x140DB3DA0`.</summary>
        Friend UMaxClothCount As UInteger = 100UI
        ''' <summary>`uMaxClothCount:Cloth` (segundo objeto, mismo nombre `0x14256EC08` y misma colección,
        ''' registrador `0x14014FE12`) — `0x142F35818`, 200. Lector `0x140DB3DC5`.</summary>
        Friend UMaxClothCount2 As UInteger = 200UI
        ''' <summary>`bAnimateClothOnSitSleep:Cloth` — `0x142F35710`, 1. Lector `0x140DB3C57`.</summary>
        Friend BAnimateClothOnSitSleep As Boolean = True
        ''' <summary>`bAnimateClothOnSwim:Cloth` — `0x142F35728`, 1. Lector `0x140DB3CEC`.</summary>
        Friend BAnimateClothOnSwim As Boolean = True
        ''' <summary>`bAnimateClothOnDown:Cloth` — `0x142F35740`, 1. Lector `0x140DB3C78`.</summary>
        Friend BAnimateClothOnDown As Boolean = True
        ''' <summary>`bAnimateClothOnDead:Cloth` — `0x142F35758`, 0. Lector `0x140DB3CD0`.</summary>
        Friend BAnimateClothOnDead As Boolean = False
        ''' <summary>`bSimulateClothDisable:Cloth` — `0x142F35770`, 0. Lector `0x140DB3D0A`.</summary>
        Friend BSimulateClothDisable As Boolean = False
        ''' <summary>`uNumSimSettleSteps:Cloth` — `0x142F4F7B8`, 10. Lector `0x14187906F`.</summary>
        Friend UNumSimSettleSteps As Integer = 10
        ''' <summary>`fMaxFrameCounterDifferenceToConsiderVisible` — `0x142F33AF8`, 0,0666667 (`0x3D888889`).
        ''' `0x140DB3A40` lo copia UNA vez a su estática `0x14330B508` (`0x140DB3EA8`/`0x140DB3EB7`).</summary>
        Friend FMaxFrameCounterDifferenceToConsiderVisible As Single = BitConverter.Int32BitsToSingle(&H3D888889)

        ' `bConstantlySwitchClothLOD` (`0x142F356F8`, 0) y `bDrawClothDeformedBones` (`0x142F4FB50`, 0) no
        ' tienen lector del valor en el `.text`: no se transcriben.

    End Class

End Namespace
