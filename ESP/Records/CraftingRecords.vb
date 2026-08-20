' ============================================================================
' Crafting / Modification Record Data Classes and Parsers
' OMOD
' ============================================================================

#Region "Data Classes"

''' <summary>OMOD property value type enum.</summary>
Public Enum OMOD_ValueType As Byte
    IntType = 0
    FloatType = 1
    BoolType = 2
    StringType = 3
    FormIDInt = 4
    EnumType = 5
    FormIDFloat = 6
End Enum

''' <summary>OMOD property entry.</summary>
Public Class OMOD_Property
    Public ValueType As OMOD_ValueType
    Public FunctionType As Byte
    Public PropertyIndex As UShort
    Public Value1 As Single      ' float/int/formid depending on type
    Public Value1FormID As UInteger ' resolved FormID when applicable
    Public Value2 As Single
    Public StepValue As Single
End Class

#End Region
