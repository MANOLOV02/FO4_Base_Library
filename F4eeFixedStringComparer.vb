Imports System.Text

''' <summary>La igualdad de <c>F4EEFixedString</c> (f4ee StringTable.h:13-53), la clave de los mapas de LooksMenu
''' (p. ej. <c>m_overlayTemplates</c>, OverlayInterface.h:207): mismo largo en BYTES y <c>_stricmp</c> igual (:21-30);
''' el hash es FNV-1a sobre <c>tolower</c> de cada byte (:40-52). <c>_stricmp</c> y <c>tolower</c> en el locale "C"
''' sólo pliegan A-Z, así que la comparación es sin mayúsculas en ASCII y exacta en todo lo demás.
''' <para>Las cadenas llegan decodificadas de los mismos bytes que ve el motor
''' (<see cref="Jsoncpp.BytesComoLosVeJsoncpp"/>); se comparan sus bytes UTF-8.</para></summary>
Public NotInheritable Class F4eeFixedStringComparer
    Implements IEqualityComparer(Of String)

    Public Shared ReadOnly Instancia As New F4eeFixedStringComparer()

    Private Shared Function Bajar(b As Byte) As Byte
        Return If(b >= &H41 AndAlso b <= &H5A, CByte(b + &H20), b)
    End Function

    Public Overloads Function Equals(x As String, y As String) As Boolean Implements IEqualityComparer(Of String).Equals
        Dim bx = Encoding.UTF8.GetBytes(If(x, ""))
        Dim by = Encoding.UTF8.GetBytes(If(y, ""))
        If bx.Length <> by.Length Then Return False
        For i = 0 To bx.Length - 1
            If Bajar(bx(i)) <> Bajar(by(i)) Then Return False
        Next
        Return True
    End Function

    ''' <summary>Coherente con <see cref="Equals"/> (los mismos bytes plegados dan el mismo hash); no hace falta que
    ''' sea el valor FNV del motor, sólo que dos claves iguales para el motor caigan en el mismo cubo.</summary>
    Public Overloads Function GetHashCode(s As String) As Integer Implements IEqualityComparer(Of String).GetHashCode
        Dim h As Long = 17
        For Each b In Encoding.UTF8.GetBytes(If(s, ""))
            h = (h * 31 + Bajar(b)) And &H7FFFFFFFL
        Next
        Return CInt(h)
    End Function
End Class
