Imports System.Numerics

''' <summary>
''' Resuelve el mounting de "addons" (chunks robot, weapon mods, PA pieces, workshop items, etc.)
''' sobre un "host" (skeleton del actor, weapon base, frame de PA, mueble) usando los
''' BSConnectPoint::Parents del host y BSConnectPoint::Children de cada addon.
'''
''' Per-NIF (no per-shape): los connect points viven en NiNode root del NIF, así que un NIF
''' entero contribuye sus sockets/children — la abstracción IShapeGeometry NO aplica acá
''' porque opera a nivel shape (ver ShapeGeometry/IShapeGeometry.vb para el contract per-shape).
'''
''' Distintas implementaciones cubren distintas estrategias de match:
'''   - <see cref="ConnectPointMountResolver"/> — engine canónica vanilla FO4
'''     ("P-X" en parents matchea "C-X" en children, suffix tras el prefix-2-chars).
'''
''' Apps que ya tienen otro consumer real (PA path C en NPC_Manager hoy, weapon preview en
''' WM mañana) consumen la misma interface — la lógica de match no se duplica entre apps.
''' </summary>
Public Interface IMountResolver

    ''' <summary>Resuelve el mounting de cada addon NIF contra los sockets del host.
    ''' Devuelve un map keyed por <paramref name="addons"/>.Key con el resultado.
    '''
    ''' Contract:
    '''   - host puede ser Nothing → devuelve dict vacío (no hay sockets para matchear).
    '''   - addons puede estar vacío → devuelve dict vacío.
    '''   - Un addon sin BSConnectPoint::Children → entry con MatchedSocket = Nothing y
    '''     reason = NoChildren.
    '''   - Un addon con Children que no matchean ningún socket → entry con MatchedSocket =
    '''     Nothing y reason = NoMatch.
    '''   - Un addon con Children y match → entry con MatchedSocket poblado, reason = Resolved.
    ''' Idempotente. Pure: no muta los NIFs.</summary>
    Function ResolveMounts(host As Nifcontent_Class_Manolo,
                           addons As IEnumerable(Of MountAddon)) As Dictionary(Of String, MountResolution)

End Interface

''' <summary>Una entrada del lado addon. Key sirve para identificar el addon en el dict
''' de salida (típicamente el dictKey del FilesDictionary o un FormID stringificado).</summary>
Public Class MountAddon
    Public Property Key As String
    Public Property Nif As Nifcontent_Class_Manolo
    ''' <summary>Etiqueta opcional para logging (e.g. "Bot_TorsoHandy"). Si vacío se usa Key.</summary>
    Public Property Label As String
End Class

''' <summary>Estados de resolución del mounting per addon.</summary>
Public Enum MountResolutionStatus
    ''' <summary>Match exitoso: MatchedSocket está poblado.</summary>
    Resolved = 0
    ''' <summary>El addon NIF no declara BSConnectPoint::Children — el engine no tiene
    ''' un socket canónico al que mountarlo. El render path debería caer al fallback
    ''' (mount al origen del actor, sin transform).</summary>
    NoChildren = 1
    ''' <summary>El addon declara Children pero ninguno matchea sockets del host. Esto
    ''' indica un mismatch real: la combination del OBTS está mal armada o el host no es
    ''' el correcto para este addon.</summary>
    NoMatch = 2
End Enum

''' <summary>Resultado del mount-resolve para un addon individual.</summary>
Public Class MountResolution
    Public Property Status As MountResolutionStatus
    ''' <summary>Socket del host al que el addon quedó mountado. Nothing si Status ≠ Resolved.</summary>
    Public Property MatchedSocket As BSConnectPointReader.ConnectPointInfo
    ''' <summary>Children point name del addon que produjo el match (e.g. "C-ArmsTypeA1|0").
    ''' Vacío si Status ≠ Resolved.</summary>
    Public Property MatchedChildName As String
    ''' <summary>Lista completa de children que el addon declaró, para diagnóstico.</summary>
    Public Property AddonChildren As IReadOnlyCollection(Of String)
End Class
