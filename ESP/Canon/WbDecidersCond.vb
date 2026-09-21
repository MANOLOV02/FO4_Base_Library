' ============================================================================================
' Este archivo transcribe a mano logica de decision de las declaraciones de formato de xEdit,
' que estan bajo Mozilla Public License 2.0, y por lo tanto es una obra derivada de ellas.
'
' This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
' If a copy of the MPL was not distributed with this file, You can obtain one at
' https://mozilla.org/MPL/2.0/
'
' Proyecto original: https://github.com/TES5Edit/TES5Edit  (ElminsterAU y colaboradores)
' Ver THIRD-PARTY-NOTICES.md en la raiz del repositorio.
' ============================================================================================
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
        ''' <summary>⛔ LA SEDE UNICA DE «DE QUE TIPO ES ESTE PARAMETRO DE CONDICION».
        ''' Ajusta el ordinal que la tabla declara segun los DISCRIMINADORES del propio `CTDA`.
        '''
        ''' <para>Transcripcion de `wbConditionParam1Decider` / `wbConditionParam2Decider`
        ''' (`wbDefinitionsFO4.pas:808-857`): con el tipo declarado en
        ''' <c>{ptReference, ptActor, ptPackage}</c>, el bit <c>0x02</c> del <c>Type</c>
        ''' (<i>Use Aliases</i>) lo fuerza a <c>ptAlias</c> y el <c>0x08</c> (<i>Use PackData</i>) a
        ''' <c>ptPackdata</c>. El <c>0x02</c> se evalua ANTES del <c>0x08</c> —es el <c>else</c> del
        ''' Pascal—, asi que con los dos puestos gana el alias.</para>
        '''
        ''' <para>⛔ ES PUBLICA PORQUE EL EDITOR TIENE QUE PREGUNTARLE A ELLA. La sede del editor
        ''' (<c>CanonInterpretacion.RamaDeParametroDeCondicion</c>) leia <c>Params</c> a secas, sin los
        ''' flags ni el <c>Run On</c>, o sea que habia DOS respuestas a la misma pregunta y divergian:
        ''' medido con el EJE 5 de <c>CondicionesGate</c> antes del arreglo, <b>1.342 divergencias de
        ''' 8.820 comparaciones</b>, en seis formas que son exactamente los tres tipos de origen por los
        ''' dos destinos. El editor mostraba un selector de record para un parametro que el motor iba a
        ''' leer como indice de alias.</para>
        '''
        ''' <para>Recibe el <c>Type</c> COMPLETO y hace las mascaras adentro: si el llamador las
        ''' hiciera, la mascara quedaria escrita dos veces.</para>
        '''
        ''' <para>⚠️ LA EXCEPCION DE <c>GetIsCurrentPackage</c> ES DEL PARAMETRO 1 EN EL <c>.pas</c>, y
        ''' esta funcion la aplica a los DOS. `wbConditionParam1Decider` trae
        ''' <c>if not ((Run On = 5) and (Desc.Name = 'GetIsCurrentPackage'))</c> y
        ''' `wbConditionParam2Decider` NO la trae. La equivalencia es POR DATO, no por codigo:
        ''' <c>Params(161) = {40, 0}</c>, o sea que el parametro 2 de la unica funcion afectada es
        ''' <c>ptNone</c> y esta funcion sale antes de tocarlo. ⛔ Esa tabla se REGENERA del `.pas`:
        ''' el dia que un commit de xEdit le de a <c>GetIsCurrentPackage</c> un <c>ParamType2</c> en el
        ''' conjunto re-ruteado, el lector de la app se separa del de xEdit EN SILENCIO. La asercion que
        ''' lo ataja vive en el EJE 4 de <c>CondicionesGate</c>.</para></summary>
        Public Function OrdinalDeParametroAjustado(game As WbGame, pt As Integer, typeFlags As Long,
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
                       ' ⛔ LLAMA A LA PUBLICA. Si quedaran las dos --la Private para el lector y la
                       ' publica para el editor-- volverian a haber dos duenos de la misma ley, esta vez
                       ' con cuerpos identicos que nadie va a comparar.
                       Dim pt = OrdinalDeParametroAjustado(ctx.Game, p(which),
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
