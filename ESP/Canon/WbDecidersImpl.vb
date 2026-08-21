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

    ''' <summary>Deciders de unión escritos a mano.
    '''
    ''' <para>Son reglas y no tablas, así que no se derivan automáticamente: cada uno se escribe
    ''' entero, incluidas las combinaciones que caen en la rama 0.</para>
    '''
    ''' <para>El decider que todavía no está acá deja fuera de la tabla al record que lo usa, que
    ''' sale marcado <c>IsIncomplete</c> y se reporta aparte. Nunca se elige una rama "razonable"
    ''' para salir del paso.</para></summary>
    Partial Public Module WbDeciders

        ''' <summary>Escalón por versión de formato: devuelve el PRIMER índice <c>i</c> tal que la
        ''' versión del record sea menor que <c>versions(i)</c>; si no hay ninguno, la cantidad de
        ''' umbrales. Lo usa el <c>SPED</c> de MOVT con los umbrales 28, 60 y 104, donde cada rama
        ''' tiene un TAMAÑO distinto.</summary>
        Public Function FormVersionList(ParamArray versions As Integer()) As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = CInt(ctx.FormVersion)
                       For i = 0 To versions.Length - 1
                           If v < versions(i) Then Return i
                       Next
                       Return versions.Length
                   End Function
        End Function

        '=========================================================================================
        ' OMOD / Object Template: los tres deciders leen el hermano 'Value Type' de la misma
        ' estructura de propiedad.
        '=========================================================================================

        ''' <summary>Tipo de función de una propiedad de OMOD, según el 'Value Type' hermano:
        ''' 0, 1 y 6 → 0; 2 → 1; 4 → 3; 5 → 2. Cualquier otro valor cae en 0.</summary>
        Public Function OmodFunctionType() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim vt = Sibling(parent, "Value Type")
                       If Not vt.HasValue Then Return 0
                       Select Case vt.Value
                           Case 0 : Return 0
                           Case 1 : Return 0
                           Case 2 : Return 1
                           Case 4 : Return 3
                           Case 5 : Return 2
                           Case 6 : Return 0
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Primer valor de una propiedad de OMOD, según el 'Value Type' hermano:
        ''' 0 → 1; 1 → 2; 2 → 3; 4 y 6 → 4; 5 → 5.
        ''' <para>Para el tipo 5 el formato afina todavía más mirando el NOMBRE de la propiedad
        ''' (SoundLevel, StaggerValue y HitBehaviour tienen rama propia). Esa sub-división no se
        ''' replica porque las cuatro ramas son un entero u32 con distinta enumeración asociada:
        ''' mismo ancho y ninguna es una referencia, así que sólo cambia la etiqueta que se muestra,
        ''' no los bytes que se leen ni los que se emiten. Los tres nombres pertenecen al conjunto
        ''' de propiedades de WEAP; el de ARMO no tiene ninguno.</para></summary>
        Public Function OmodValue1() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim vt = Sibling(parent, "Value Type")
                       If Not vt.HasValue Then Return 0
                       Select Case vt.Value
                           Case 0 : Return 1
                           Case 1 : Return 2
                           Case 2 : Return 3
                           Case 4, 6 : Return 4
                           Case 5 : Return 5
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Segundo valor de una propiedad de OMOD, según el 'Value Type' hermano:
        ''' 0 → 1; 1 → 2; 2 → 3; 4 → 1; 6 → 2.</summary>
        Public Function OmodValue2() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim vt = Sibling(parent, "Value Type")
                       If Not vt.HasValue Then Return 0
                       Select Case vt.Value
                           Case 0 : Return 1
                           Case 1 : Return 2
                           Case 2 : Return 3
                           Case 4 : Return 1
                           Case 6 : Return 2
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Tipo de nota: lo dice el subrecord hermano <c>DNAM</c> del record.
        ''' 0 → 1; 1 → 2; 3 → 3; el resto cae en 0.</summary>
        Public Function NoteType() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = Sibling(parent, "DNAM")
                       If Not v.HasValue Then Return 0
                       Select Case v.Value
                           Case 0 : Return 1
                           Case 1 : Return 2
                           Case 3 : Return 3
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Cantidad de fragmentos de script: sube por los contenedores hasta el que se
        ''' llama <c>'Script Fragments'</c> y devuelve la cantidad de bits encendidos de su campo
        ''' <c>Flags</c>. Vale igual para escenas, diálogos y paquetes: en los tres casos se cuentan
        ''' los OCHO bits del byte de banderas.
        ''' <para>Sin este conteo el arreglo se leería "hasta agotar los bytes", invadiría los
        ''' <c>Phase Fragments</c> y la estructura se cortaría en <c>ScriptName</c>.</para></summary>
        Public Function ScriptFragmentsPopCount() As WbCounter
            Return Function(node)
                       Dim c = node
                       While c IsNot Nothing AndAlso c.Def.Name <> "Script Fragments"
                           c = c.Parent
                       End While
                       If c Is Nothing Then Return 0
                       Dim f = c.ByName("Flags")
                       If f Is Nothing OrElse f.Value Is Nothing Then Return 0
                       Dim v = CInt(Convert.ToInt64(f.Value)) And &HFF
                       Dim n = 0
                       While v <> 0
                           n += (v And 1)
                           v >>= 1
                       End While
                       Return n
                   End Function
        End Function
        ''' <summary>Sube por los padres hasta el nodo que representa un subrecord entero.</summary>
        Private Function EnclosingSubrecord(node As WbNode) As WbNode
            Dim c = node
            While c IsNot Nothing
                If TypeOf c.Def Is WbSubrecordDef Then Return c
                c = c.Parent
            End While
            Return Nothing
        End Function

        ''' <summary>El campo existe solo si el subrecord que lo contiene mide al menos
        ''' <paramref name="minSize"/> bytes. Es como el formato declara campos que se fueron
        ''' agregando al final de un mismo subrecord sin cambiar la version: el largo del subrecord
        ''' es lo que dice cuantos hay.</summary>
        Public Function MinSubrecordSize(minSize As Integer) As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim sub_ = EnclosingSubrecord(parent)
                       ' Sin bytes de origen estamos CREANDO un record, y un record nuevo se arma
                       ' con el formato mas completo: el campo va.
                       If sub_ Is Nothing OrElse sub_.DataLength < 0 Then Return 1
                       Return If(sub_.DataLength >= minSize, 1, 0)
                   End Function
        End Function

        ''' <summary>Cantidad de colores por capa de nube del clima: 32 desde la version de formato
        ''' 35, 4 antes.</summary>
        Public Function WeatherCloudColorsCount() As WbCounter
            Return Function(node)
                       Dim root = node
                       While root IsNot Nothing AndAlso root.Parent IsNot Nothing
                           root = root.Parent
                       End While
                       If root Is Nothing OrElse root.ParsedFormVersion < 0 Then Return 4
                       Return If(root.ParsedFormVersion >= 35, 32, 4)
                   End Function
        End Function

        ''' <summary>Tipo de datos de un efecto de audio. El campo de tipo no guarda un nombre sino
        ''' un hash de 32 bits de la clase que implementa el efecto; los tres valores posibles estan
        ''' fijos en el formato.</summary>
        Public Function AudioEffectData() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim k = Sibling(parent, "KNAM")
                       If Not k.HasValue Then Return 0
                       Select Case CUInt(k.Value And &HFFFFFFFFL)
                           Case &H864804BEUI : Return 0     ' distorsion
                           Case &HEF575F7FUI : Return 1     ' filtro de variable de estado
                           Case &H18837B4FUI : Return 2     ' retardo
                           Case Else : Return 0
                       End Select
                   End Function
        End Function

        ''' <summary>Los descriptores de sonido de arma automatica llevan un bloque de datos propio.
        ''' El tipo de descriptor tampoco es un nombre sino un hash de 32 bits.</summary>
        Public Function SoundDescriptorData() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim c = Sibling(parent, "CNAM")
                       If Not c.HasValue Then Return 0
                       Return If(CUInt(c.Value And &HFFFFFFFFL) = &HED157AE3UI, 1, 0)
                   End Function
        End Function

        ''' <summary>Datos extra de un item: el campo que sigue al dueño cambia de tipo segun a que
        ''' apunte ese dueño.
        ''' <list type="bullet">
        ''' <item>dueño ausente o no resoluble ⇒ 4 bytes de relleno, que NO son una referencia</item>
        ''' <item>dueño = personaje ⇒ referencia a una variable global</item>
        ''' <item>dueño = faccion ⇒ rango numerico requerido</item>
        ''' </list>
        ''' <para>Los tres miden 4 bytes: el archivo sale igual con cualquiera. Lo que cambia es si
        ''' el remapeo de indices de master toca ese campo o no. Por eso sin resolvedor se elige el
        ''' relleno: equivocarse hacia "no es referencia" deja el dato intacto; equivocarse hacia
        ''' "si es" lo corrompe.</para></summary>
        Public Function ItemExtraDataOwner() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       If ctx.ResolveSignature Is Nothing OrElse parent Is Nothing Then Return 0
                       Dim owner = parent.ByName("Owner")
                       If owner Is Nothing OrElse owner.Value Is Nothing Then Return 0
                       Dim id As UInteger
                       Try
                           id = Convert.ToUInt32(owner.Value)
                       Catch
                           Return 0
                       End Try
                       If id = 0UI Then Return 0
                       Select Case ctx.ResolveSignature(id)
                           Case "NPC_" : Return 1
                           Case "FACT" : Return 2
                           Case Else : Return 0
                       End Select
                   End Function
        End Function
    End Module

End Namespace
