' Mapeo compartido índice-de-combo ↔ versión de header BA2 (FO4) + populate del combo.
' Antes estaba duplicado en WM Config_Form y NPC SaveEsp_Form. La visibilidad FO4-only
' se queda en cada form. La opción "Loose" (índice 2 / versión 0) solo la ofrece NPC.
Public Module Ba2VersionUI
    ' Versiones de header BA2 (FO4).
    Public Const NextGen As UInteger = 8UI        ' índice 0
    Public Const OldGen As UInteger = 1UI         ' índice 1
    Public Const LooseOnly As UInteger = 0UI      ' índice 2 (solo NPC) — saltea el pack BA2

    ' versión → índice de combo (1→1, 0→2, cualquier otra/8 → 0 = NG default).
    Public Function Ba2VersionToComboIndex(version As UInteger) As Integer
        Select Case version
            Case OldGen : Return 1
            Case LooseOnly : Return 2
            Case Else : Return 0
        End Select
    End Function

    ' índice de combo → versión (1→1, 2→0, cualquier otro/0 → 8 = NG default).
    Public Function ComboIndexToBa2Version(index As Integer) As UInteger
        Select Case index
            Case 1 : Return OldGen
            Case 2 : Return LooseOnly
            Case Else : Return NextGen
        End Select
    End Function

    ' Llena el combo con las opciones de versión. includeLoose agrega la opción 3 (índice 2).
    Public Sub PopulateBa2VersionCombo(combo As System.Windows.Forms.ComboBox, includeLoose As Boolean)
        If combo Is Nothing Then Return
        combo.Items.Clear()
        combo.Items.Add("8 - Next Gen (NG)")
        combo.Items.Add("1 - Old Gen (OG / universal)")
        If includeLoose Then combo.Items.Add("None - Loose files (skip BA2 pack)")
    End Sub
End Module
