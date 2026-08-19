Namespace Canon

    ''' <summary>Punto de entrada ÚNICO al esquema: dado (juego, firma) devuelve la definición del
    ''' record.
    '''
    ''' <para>El juego no es un parámetro que cada emisor decida por su cuenta: es parte de la
    ''' clave del esquema. Así la dependencia del juego queda declarada en un solo lugar, en vez de
    ''' repartida en condicionales <c>If game = Skyrim</c> por todo el escritor.</para>
    '''
    ''' <para>Las declaraciones concretas viven en <c>Generated/WbSchemaGen_*.vb</c>, una por
    ''' juego.</para></summary>
    Public Module WbSchema

        ''' <summary>Definición del record, o Nothing si el juego no lo declara.</summary>
        Public Function [Get](game As WbGame, sig As String) As WbRecordDef
            If String.IsNullOrEmpty(sig) Then Return Nothing
            Select Case game
                Case WbGame.Skyrim : Return WbSchemaGenTES5.Get(sig)
                Case Else : Return WbSchemaGenFO4.Get(sig)
            End Select
        End Function

        ''' <summary>Todas las signatures declaradas para el juego.</summary>
        Public Function Signatures(game As WbGame) As String()
            Select Case game
                Case WbGame.Skyrim : Return WbSchemaGenTES5.Signatures
                Case Else : Return WbSchemaGenFO4.Signatures
            End Select
        End Function

        ''' <summary>Firmas cuyo esquema está COMPLETO (ningún miembro sin describir).
        ''' Un record marcado <c>IsIncomplete</c> no cuenta como verificado: se reporta aparte.</summary>
        Public Function CompleteSignatures(game As WbGame) As String()
            Dim outp As New List(Of String)
            For Each s In Signatures(game)
                Dim d = [Get](game, s)
                If d IsNot Nothing AndAlso Not d.IsIncomplete Then outp.Add(s)
            Next
            Return outp.ToArray()
        End Function

    End Module

End Namespace
