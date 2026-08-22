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

    ''' <summary>Segundo bloque de deciders de unión.
    ''' <para>Los que faltan —típicamente los que necesitan resolver una referencia hasta el record
    ''' apuntado, o el NOMBRE de un valor de enumeración— quedan fuera de la tabla: el record que
    ''' los usa se marca <c>IsIncomplete</c> y se reporta aparte. No se inventa una rama para salir
    ''' del paso.</para></summary>
    Partial Public Module WbDeciders

        '=========================================================================================
        ' CONDICIONES (CTDA): el mismo bloque de condición aparece en decenas de records.
        '=========================================================================================

        ''' <summary>Valor de comparación de una condición: el bit 0x04 de <c>Type</c> indica que,
        ''' en vez de un número literal, viene una referencia a variable global.</summary>
        Public Function ConditionCompValue() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim t = Sibling(parent, "Type")
                       If Not t.HasValue Then Return 0
                       Return If((t.Value And 4L) <> 0L, 1, 0)
                   End Function
        End Function

        ''' <summary>Tercer parámetro de una condición: el índice de rama ES el valor de
        ''' <c>Run On</c>.</summary>
        Public Function ConditionParam3() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Run On")
                       Return If(v.HasValue, CInt(v.Value), 0)
                   End Function
        End Function

        ''' <summary>Referencia de la condición: sólo con <c>Run On = 2</c> el campo es una
        ''' referencia a un objeto colocado en el mundo.</summary>
        Public Function ConditionReference() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Run On")
                       Return If(v.HasValue AndAlso v.Value = 2L, 1, 0)
                   End Function
        End Function

        ''' <summary>Parámetro de valor de las condiciones de VATS: el índice de rama es el valor
        ''' del primer parámetro. Sólo existe en Skyrim.</summary>
        Public Function ConditionVatsValueParam() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Parameter #1")
                       Return If(v.HasValue, CInt(v.Value), 0)
                   End Function
        End Function

        '=========================================================================================
        ' VMAD / scripts
        '=========================================================================================

        ''' <summary>Formato de las referencias a objeto dentro del VMAD. Lo fija el campo
        ''' <c>Object Format</c> de la cabecera del bloque, que es un ANCESTRO del punto donde se
        ''' decide y no un hermano directo.</summary>
        Public Function ScriptObjFormat() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       ' El índice NO es el valor crudo del campo: la unión tiene sólo dos ramas, y
                       ' el formato 2, que es el habitual, comparte rama con el 0. Devolver el valor
                       ' tal cual se sale del rango de ramas.
                       Dim v = Sibling(parent, "Object Format")
                       Return If(v.HasValue AndAlso v.Value = 1L, 1, 0)
                   End Function
        End Function

        ''' <summary>Tipo de una propiedad de script del VMAD. DEPENDE DEL JUEGO.
        ''' <para>Fallout 4 conoce CATORCE tipos: 1-7 → 1-7 y 11-17 → 8-14. Skyrim conoce sólo
        ''' DIEZ: 1-5 → 1-5 y 11-15 → 6-10. El tipo 6 existe en Fallout 4 y no en Skyrim.</para>
        ''' <para>Usar el mapeo de un juego con el otro elige la variante equivocada y parsea la
        ''' propiedad con un tipo que no es el suyo.</para></summary>
        Public Function ScriptProperty() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim t = Sibling(parent, "Type")
                       If Not t.HasValue Then Return 0
                       If ctx.Game = WbGame.Skyrim Then
                           Select Case t.Value
                               Case 1 : Return 1
                               Case 2 : Return 2
                               Case 3 : Return 3
                               Case 4 : Return 4
                               Case 5 : Return 5
                               Case 11 : Return 6
                               Case 12 : Return 7
                               Case 13 : Return 8
                               Case 14 : Return 9
                               Case 15 : Return 10
                               Case Else : Return 0
                           End Select
                       End If
                       Select Case t.Value
                           Case 1 : Return 1
                           Case 2 : Return 2
                           Case 3 : Return 3
                           Case 4 : Return 4
                           Case 5 : Return 5
                           Case 6 : Return 6
                           Case 7 : Return 7
                           Case 11 : Return 8
                           Case 12 : Return 9
                           Case 13 : Return 10
                           Case 14 : Return 11
                           Case 15 : Return 12
                           Case 16 : Return 13
                           Case 17 : Return 14
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Fragmento sin script asociado: si el <c>ScriptName</c> está vacío, lo que
        ''' sigue tiene otra forma.</summary>
        Public Function ScriptFragmentsEmptyScript() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       If parent Is Nothing Then Return 0
                       Dim n = WbPath.ResolveUpwards(parent, "ScriptName")
                       If n Is Nothing Then Return 0
                       Return If(String.IsNullOrEmpty(TryCast(n.Value, String)), 1, 0)
                   End Function
        End Function

        '=========================================================================================
        ' Varios
        '=========================================================================================

        ''' <summary>Despacho directo por tipo: el índice de rama ES el valor del hermano
        ''' <c>Type</c>. Idéntico en los dos juegos.</summary>
        Public Function TypeDecider() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Type")
                       Return If(v.HasValue, CInt(v.Value), 0)
                   End Function
        End Function

        ''' <summary>Nivel del actor en el <c>ACBS</c>: el bit 0x80 de <c>Flags</c> distingue un
        ''' nivel fijo de uno relativo al del jugador, que se guarda con otro formato en el mismo
        ''' lugar.</summary>
        Public Function AcbsLevel() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Flags")
                       Return If(v.HasValue AndAlso (v.Value And &H80L) <> 0L, 1, 0)
                   End Function
        End Function

        ''' <summary>Contenido del <c>DATA</c> de una entrada de perk: lo determina el <c>Type</c>
        ''' del <c>PRKE</c> hermano. Idéntico en los dos juegos.</summary>
        Public Function PerkData() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       If parent Is Nothing Then Return 0
                       Dim n = WbPath.ResolveUpwards(parent, "PRKE\Type")
                       If n Is Nothing OrElse n.Value Is Nothing Then Return 0
                       Try
                           Return CInt(Convert.ToInt64(n.Value))
                       Catch
                           Return 0
                       End Try
                   End Function
        End Function

        ''' <summary>Contenido del <c>EPFD</c>: lo fija el valor del <c>EPFT</c> hermano, salvo que
        ''' ese valga 2 y la función del entry point sea 5, 12, 13 o 14, en cuyo caso el dato tiene
        ''' otra forma. Idéntico en los dos juegos.</summary>
        Public Function Epfd() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "EPFT")
                       If Not v.HasValue Then Return 0
                       Dim r = CInt(v.Value)
                       If r = 2 Then
                           Dim f = Sibling(parent, "Function")
                           If f.HasValue Then
                               Select Case f.Value
                                   Case 5, 12, 13, 14 : Return 8
                               End Select
                           End If
                       End If
                       Return r
                   End Function
        End Function

        ''' <summary>Qué enseña un libro. DEPENDE DEL JUEGO en la CANTIDAD DE RAMAS, no en la regla.
        ''' <para>Las dos uniones empiezan con una rama de relleno de 4 bytes, así que la rama CERO
        ''' es "no enseña nada": 0x01 → 1, 0x04 → 2, y sólo Fallout 4 tiene una tercera, 0x10 → 3.
        ''' </para>
        ''' <para>⛔ CONTRAEJEMPLO que costó encontrar: mientras la unión de Skyrim tuvo DOS ramas
        ''' (sin la de relleno), la regla correcta era 0x04 → 1. Cuando el formato le antepuso la
        ''' rama de relleno, esa misma regla pasó a devolver la rama del Skill para un libro que
        ''' enseña un hechizo — un FormID leído como entero con signo. No lo ve ni la ida y vuelta
        ''' byte a byte ni la cobertura, porque son los mismos cuatro bytes: lo único que lo caza es
        ''' comparar la cantidad de ramas de la unión contra la implementación.</para></summary>
        Public Function BookTeaches() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Flags")
                       If Not v.HasValue Then Return 0
                       Dim i = v.Value
                       If (i And &H1L) <> 0L Then Return 1
                       If (i And &H4L) <> 0L Then Return 2
                       If ctx.Game <> WbGame.Skyrim AndAlso (i And &H10L) <> 0L Then Return 3
                       Return 0
                   End Function
        End Function

        ''' <summary>Item asociado a un efecto mágico. DEPENDE DEL JUEGO en el arquetipo 45 y 46:
        ''' el 46 es Immunity en Fallout 4 y Vampire Lord en Skyrim, y el 45 sólo existe en
        ''' Fallout 4.
        ''' <para>⛔ CONTRAEJEMPLO: el campo que discrimina se llamó <c>'Archtype'</c> —sin la 'e'—
        ''' en Skyrim durante años, y buscarlo por ese nombre era lo correcto. Cuando el formato
        ''' corrigió el error de tipeo, seguir buscando <c>'Archtype'</c> dejó de encontrar el campo
        ''' y el efecto entero caía en la rama cero, sin que cambiara un solo byte. Buscar por el
        ''' nombre de un campo obliga a validar ese nombre contra el esquema, no a recordarlo.
        ''' </para></summary>
        Public Function MgefAssocItem() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim isSse = (ctx.Game = WbGame.Skyrim)
                       Dim v = Sibling(parent, "Archetype")
                       If Not v.HasValue Then Return 0
                       Select Case v.Value
                           Case 12 : Return 1
                           Case 17 : Return 2
                           Case 18 : Return 3
                           Case 25 : Return 4
                           Case 34 : Return 8
                           Case 35 : Return 5
                           Case 36 : Return 6
                           Case 39 : Return 7
                           Case 40 : Return 4
                           Case 45 : Return If(isSse, 0, 9)
                           Case 46 : Return If(isSse, 6, 9)
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Padre de una malla de navegación: si <c>Parent World</c> es cero se trata de
        ''' una celda interior y lo que sigue tiene otra forma. Vale igual para el bloque de navmesh
        ''' y para el índice de navegación.</summary>
        Public Function NvnmParent() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Parent World")
                       Return If(v.HasValue AndAlso v.Value = 0L, 1, 0)
                   End Function
        End Function

        Public Function NaviParent() As WbDecider
            Return NvnmParent()
        End Function

        ''' <summary>Datos de isla del índice de navegación: el índice de rama ES el valor del
        ''' campo <c>Has Island Data</c>.</summary>
        Public Function NaviIslandData() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "Has Island Data")
                       Return If(v.HasValue, CInt(v.Value), 0)
                   End Function
        End Function

        ''' <summary>Variante horaria de los datos de clima: se decide por el TAMAÑO del
        ''' subrecord, 64 o 160 bytes. <paramref name="avail"/> es ese tamaño cuando la unión está
        ''' en la raíz del subrecord.</summary>
        Public Function WeatherTimeOfDay() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Return If(avail = 64 OrElse avail = 160, 1, 0)
                   End Function
        End Function

        ''' <summary>Tipo del valor de un GMST: no está en ningún campo, lo dice la PRIMERA LETRA
        ''' del EditorID. <c>s</c> → cadena; <c>i</c> → entero de 32 bits; <c>f</c> → float;
        ''' <c>b</c> → booleano, que sólo existe desde Skyrim en adelante y por lo tanto también en
        ''' Fallout 4. Ante cualquier otra letra se asume entero, que es la interpretación más
        ''' inofensiva.</summary>
        Public Function GmstUnion() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim ed = ctx.EditorId
                       If String.IsNullOrEmpty(ed) Then Return 1
                       Select Case Char.ToLowerInvariant(ed(0))
                           Case "s"c : Return 0
                           Case "i"c : Return 1
                           Case "f"c : Return 2
                           Case "b"c : Return 3
                           Case Else : Return 1
                       End Select
                   End Function
        End Function

        ''' <summary>Variante del bloque de un INFO: la determina el bit 0x40 de las banderas de
        ''' la CABECERA del record, no un campo de datos.</summary>
        Public Function InfoGroup() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Return If((ctx.RecordFlags And &H40UI) <> 0UI, 1, 0)
                   End Function
        End Function

    End Module

End Namespace
