''' <summary>
''' Implementación de <see cref="IMountResolver"/> que matchea via convención observada
''' en NIFs vanilla FO4: sockets parent con prefix "P-" matchean children con prefix "C-"
''' cuando comparten el suffix tras el prefix de 2 chars (ambos case-insensitive).
'''
''' ⚠ A VERIFICAR — la regla está INFERIDA, no confirmada contra spec del engine:
'''   Evidencia actual: ~10 pares observados en HandyRace (P-ArmsTypeA1|0/C-ArmsTypeA1|0,
'''   P-BotCore/C-BotCore, P-BotLegs/C-BotLegs, P-ModSlotA/C-ModSlotA, etc.). Todos
'''   siguen el patrón "P-X" ↔ "C-X".
'''
'''   Pendiente verificar:
'''     - Power Armor frame.nif (Helmet/LArm/RArm/etc): ¿también P-/C-?
'''     - Weapon mods (silenciador, mira): ¿P-Muzzle/C-Muzzle o convención distinta?
'''     - Workshop items / furniture interaction nodes.
'''     - Sockets sin prefix conocido (vanilla no parece usar; defensivo cubierto via
'''       fallback "name tal cual" pero ad hoc).
'''     - Que la regla real del engine sea "match por suffix tras strip" y no "match
'''       exacto Name = PointName" (en vanilla ambas dan el mismo resultado, pero la
'''       implementación correcta importa para mods con naming no-canónico).
'''
'''   Fuente canónica NO encontrada en xEdit, F4SE source ni nif.xml docs. Si aparece
'''   evidencia que contradiga el patrón, esta clase debe revisarse ANTES de hardcodear
'''   más excepciones (ver feedback_no_workarounds.md).
'''
''' OMOD.AttachPoint KYWD (e.g. "ap_Bot_BotCore") NO interviene en el match — es metadata
''' del CK para validar compatibilidad chunk↔slot al armar la combination, pero el runtime
''' engine usa exclusivamente los strings dentro del NIF.
''' </summary>
Public Class ConnectPointMountResolver
    Implements IMountResolver

    ''' <summary>Singleton stateless. Cualquier consumer puede usar
    ''' <c>ConnectPointMountResolver.Instance</c> sin instanciar.</summary>
    Public Shared ReadOnly Property Instance As New ConnectPointMountResolver()

    Public Function ResolveMounts(host As Nifcontent_Class_Manolo,
                                   addons As IEnumerable(Of MountAddon)) As Dictionary(Of String, MountResolution) Implements IMountResolver.ResolveMounts
        Dim result As New Dictionary(Of String, MountResolution)(StringComparer.OrdinalIgnoreCase)
        If addons Is Nothing Then Return result

        ' Index host sockets by tail (lo que viene tras el prefix "P-" / "P_"). Si un socket
        ' no tiene prefix conocido se indexa por su Name tal cual.
        Dim socketsByTail As New Dictionary(Of String, BSConnectPointReader.ConnectPointInfo)(StringComparer.OrdinalIgnoreCase)
        If host IsNot Nothing Then
            For Each socket In BSConnectPointReader.ReadParents(host)
                Dim tail = StripParentPrefix(socket.Name)
                ' Last-wins on duplicate tails (defensive — vanilla shouldn't have duplicates).
                socketsByTail(tail) = socket
            Next
        End If

        ' Chained mounting: cada addon NIF puede declarar SUS PROPIOS BSConnectPoint::Parents
        ' (sub-sockets que otros addons pueden usar para mountarse encima — patrón confirmado
        ' en Assaultron: el torso chunk expone sockets para que el front/rear armor se mounten
        ' encima del torso y no del skeleton). Acumular en el dict global para que addons
        ' subsiguientes encuentren matches via sub-sockets de addons previos.
        For Each addon In addons
            If addon Is Nothing OrElse addon.Nif Is Nothing Then Continue For
            For Each subSocket In BSConnectPointReader.ReadParents(addon.Nif)
                If String.IsNullOrEmpty(subSocket.Name) Then Continue For
                Dim tail = StripParentPrefix(subSocket.Name)
                socketsByTail(tail) = subSocket
            Next
        Next

        For Each addon In addons
            If addon Is Nothing OrElse String.IsNullOrEmpty(addon.Key) Then Continue For
            Dim label = If(String.IsNullOrEmpty(addon.Label), addon.Key, addon.Label)
            Dim resolution As New MountResolution()

            Dim children = BSConnectPointReader.ReadChildrenNames(addon.Nif)
            resolution.AddonChildren = children

            If children.Count = 0 Then
                resolution.Status = MountResolutionStatus.NoChildren
                result(addon.Key) = resolution
                Continue For
            End If

            ' Try each declared child against the parent index. First match wins (vanilla
            ' chunks declare a single child; defensive vs multi-child addons).
            Dim matched As BSConnectPointReader.ConnectPointInfo = Nothing
            Dim matchedChild As String = ""
            For Each childName In children
                Dim tail = StripChildPrefix(childName)
                If socketsByTail.TryGetValue(tail, matched) Then
                    matchedChild = childName
                    Exit For
                End If
            Next

            If matched IsNot Nothing Then
                resolution.Status = MountResolutionStatus.Resolved
                resolution.MatchedSocket = matched
                resolution.MatchedChildName = matchedChild
            Else
                resolution.Status = MountResolutionStatus.NoMatch
            End If

            result(addon.Key) = resolution
        Next

        Return result
    End Function

    ''' <summary>Strip prefix "P-" / "P_" del nombre del socket parent (case-insens). Si el
    ''' nombre no tiene ese prefix se devuelve tal cual.</summary>
    Private Shared Function StripParentPrefix(name As String) As String
        If String.IsNullOrEmpty(name) Then Return ""
        If name.Length >= 2 AndAlso (
                name.StartsWith("P-", StringComparison.OrdinalIgnoreCase) OrElse
                name.StartsWith("P_", StringComparison.OrdinalIgnoreCase)) Then
            Return name.Substring(2)
        End If
        Return name
    End Function

    ''' <summary>Strip prefix "C-" / "C_" del nombre del child point (case-insens). Si el
    ''' nombre no tiene ese prefix se devuelve tal cual.</summary>
    Private Shared Function StripChildPrefix(name As String) As String
        If String.IsNullOrEmpty(name) Then Return ""
        If name.Length >= 2 AndAlso (
                name.StartsWith("C-", StringComparison.OrdinalIgnoreCase) OrElse
                name.StartsWith("C_", StringComparison.OrdinalIgnoreCase)) Then
            Return name.Substring(2)
        End If
        Return name
    End Function

End Class
