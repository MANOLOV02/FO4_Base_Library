Namespace Canon

    ''' <summary>Deciders de los PARÁMETROS de una condición (CTDA). Son los únicos que no
    ''' despachan por un valor del propio dato sino por una TABLA: la que dice, para cada función de
    ''' condición, de qué tipo es cada uno de sus parámetros. El índice de rama es el ordinal de ese
    ''' tipo más uno, porque la rama 0 queda reservada para el caso desconocido.
    '''
    ''' <para>La tabla y la numeración de los tipos son POR JUEGO, y viven generadas en
    ''' <c>Generated/WbConditions_*.vb</c>: Fallout 4 tiene 479 funciones y 50 tipos de parámetro,
    ''' Skyrim 402 y 57, y el tipo "referencia" es el ordinal <b>44</b> en uno y el <b>50</b> en el
    ''' otro. Compartir una sola tabla entre los dos juegos elegiría una rama distinta y parsearía
    ''' el parámetro con un tipo que no es el suyo.</para></summary>
    Partial Public Module WbDeciders

        Private Function CondParams(game As WbGame, funcIndex As Integer) As Integer()
            Dim r As Integer() = Nothing
            If game = WbGame.Skyrim Then
                If WbConditionsTES5.Params.TryGetValue(funcIndex, r) Then Return r
            Else
                If WbConditionsFO4.Params.TryGetValue(funcIndex, r) Then Return r
            End If
            Return Nothing
        End Function

        ''' <summary>Corrige el tipo de parámetro según las banderas de la condición: si el tipo
        ''' es Reference, Actor o Package, el bit 0x02 de <c>Type</c> ("usar alias") lo convierte en
        ''' Alias, y el bit 0x08 ("usar packdata") en Packdata.
        ''' <para>Fallout 4 tiene UNA excepción que Skyrim no: con <c>Run On = 5</c> (alias de
        ''' quest) y la función <c>GetIsCurrentPackage</c>, el tipo NO se fuerza a Alias.</para></summary>
        Private Function AdjustParam(game As WbGame, pt As Integer, typeFlags As Long,
                                     runOn As Long, funcIndex As Integer) As Integer
            Dim isSse = (game = WbGame.Skyrim)
            Dim refO = If(isSse, WbConditionsTES5.ReferenceOrdinal, WbConditionsFO4.ReferenceOrdinal)
            Dim actO = If(isSse, WbConditionsTES5.ActorOrdinal, WbConditionsFO4.ActorOrdinal)
            Dim pkgO = If(isSse, WbConditionsTES5.PackageOrdinal, WbConditionsFO4.PackageOrdinal)
            Dim aliO = If(isSse, WbConditionsTES5.AliasOrdinal, WbConditionsFO4.AliasOrdinal)
            Dim pkdO = If(isSse, WbConditionsTES5.PackdataOrdinal, WbConditionsFO4.PackdataOrdinal)

            If pt <> refO AndAlso pt <> actO AndAlso pt <> pkgO Then Return pt

            If (typeFlags And &H2L) <> 0L Then
                If Not isSse AndAlso runOn = 5L AndAlso WbConditionsFO4.IsCurrentPackage.Contains(funcIndex) Then
                    Return pt
                End If
                Return aliO
            ElseIf (typeFlags And &H8L) <> 0L Then
                Return pkdO
            End If
            Return pt
        End Function

        Private Function ConditionParamN(which As Integer) As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim fi = Sibling(parent, "Function")
                       If Not fi.HasValue Then Return 0
                       Dim p = CondParams(ctx.Game, CInt(fi.Value))
                       If p Is Nothing Then Return 0
                       Dim tf = Sibling(parent, "Type")
                       Dim ro = Sibling(parent, "Run On")
                       Dim pt = AdjustParam(ctx.Game, p(which),
                                            If(tf.HasValue, tf.Value, 0L),
                                            If(ro.HasValue, ro.Value, -1L),
                                            CInt(fi.Value))
                       Return pt + 1                     ' la rama 0 queda para el caso desconocido
                   End Function
        End Function

        ''' <summary>Tipo del primer parámetro de la condición.</summary>
        Public Function ConditionParam1() As WbDecider
            Return ConditionParamN(0)
        End Function

        ''' <summary>Tipo del segundo parámetro de la condición.</summary>
        Public Function ConditionParam2() As WbDecider
            Return ConditionParamN(1)
        End Function

        ''' <summary>Valor de un dato público de paquete: el tipo lo declara el <c>ANAM</c>
        ''' hermano. Bool → 1, Int → 2, y tanto Float como ObjectList → 3. Idéntico en los dos
        ''' juegos.</summary>
        Public Function PubPackCnam() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "ANAM")
                       If Not v.HasValue Then Return 0
                       ' El ANAM es un entero con enum ['Bool','Int','Float','ObjectList'] en ese
                       ' orden, asi que el indice del enum ES el nombre.
                       Select Case v.Value
                           Case 0 : Return 1   ' Bool
                           Case 1 : Return 2   ' Int
                           Case 2 : Return 3   ' Float
                           Case 3 : Return 3   ' ObjectList
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Tipo de acción de una escena: el índice de rama ES el valor del subrecord
        ''' <c>ANAM</c> de la acción. Devuelve <c>-1</c> si no se puede resolver, y entonces la
        ''' unión cae a probar sus variantes una por una.</summary>
        Public Function SceneActionType() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       If parent Is Nothing Then Return -1
                       Dim n = WbPath.ResolveUpwards(parent, "ANAM")
                       If n Is Nothing OrElse n.Value Is Nothing Then Return -1
                       Try
                           Return CInt(Convert.ToInt64(n.Value))
                       Catch
                           Return -1
                       End Try
                   End Function
        End Function

    End Module

End Namespace
