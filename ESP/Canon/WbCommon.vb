Namespace Canon

    ''' <summary>Composiciones de subrecords que se repiten en muchos records y que comparten los
    ''' dos juegos.
    ''' <para>Varias dependen del juego y por eso lo reciben como parámetro: aunque el subrecord
    ''' lleve la misma firma en Fallout 4 y en Skyrim, su contenido puede diferir. Declararlas una
    ''' sola vez "porque se llaman igual" produce layouts cruzados en casos como <c>WEAP.DNAM</c>,
    ''' <c>BPTD/BPND</c> o <c>MGEF.ResistValue</c>.</para></summary>
    Public Module WbCommon

        '=====================================================================================
        ' Object Bounds: caja envolvente del objeto, seis enteros de 16 bits con signo.
        '=====================================================================================

        Public Function Obnd(Optional required As Boolean = False) As WbMemberDef
            Dim d = Wb.Sub_("OBND", Wb.StructV("Object Bounds",
                Wb.Int("X1", WbIntType.s16), Wb.Int("Y1", WbIntType.s16), Wb.Int("Z1", WbIntType.s16),
                Wb.Int("X2", WbIntType.s16), Wb.Int("Y2", WbIntType.s16), Wb.Int("Z2", WbIntType.s16)))
            d.Required = required
            Return d
        End Function

        '=====================================================================================
        ' Keywords: el par KSIZ / KWDA. KSIZ es un contador u32 y KWDA el arreglo de referencias
        ' a KYWD.
        ' El contador NO es un dato independiente: al emitir se recalcula desde la longitud real
        ' del arreglo, de modo que no puede quedar desincronizado con él.
        '=====================================================================================

        Public Function Keywords() As WbMemberDef
            Dim kwda = Wb.Sub_("KWDA",
                Wb.ArrayV("Keywords", Wb.Fid("Keyword", "KYWD", "NULL"), 0, "KSIZ\Keyword Count"))
            kwda.Required = True
            Return Wb.RStruct("Keywords",
                Wb.IntSub("KSIZ", "Keyword Count", WbIntType.u32, ""),
                kwda)
        End Function

        '=====================================================================================
        ' Model Information (MODT / MO2T / MO4T / DMDT).
        '
        ' El bloque se puede tratar como un arreglo de bytes opaco, pero acá se declara
        ' DECODIFICADO: se entienden los hashes de textura en vez de copiarlos verbatim. Es lo que
        ' convierte a MO2T/MO4T de "copiado a ciegas" en "declarado".
        '
        ' El formato del bloque no es único. Se elige entre CUATRO variantes según la versión de
        ' formato del record y el primer u32 del subrecord:
        '   v >= 40 : primer u32 > 8  -> 1 (ERROR)          si no -> 3 (formato nuevo)
        '   v >= 38 : primer u32 <= 8 -> 1 (ERROR)          si no -> 2 (sólo texturas)
        '   v <  38 : 0 (bloque opaco)
        '=====================================================================================

        Public Function ModelInfoDecider() As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = CInt(ctx.FormVersion)
                       Dim head As Long = -1
                       If data IsNot Nothing AndAlso avail >= 4 Then head = CLng(BitConverter.ToUInt32(data, offset))
                       If v >= 40 Then
                           If head > 8 Then Return 1
                           Return 3
                       ElseIf v >= 38 Then
                           If head >= 0 AndAlso head <= 8 Then Return 1
                           Return 2
                       End If
                       Return 0
                   End Function
        End Function

        ''' <summary>Entrada de archivo del bloque de model info: 12 bytes exactos — hash del
        ''' nombre de archivo (u32), extensión de 4 caracteres SIN terminador nulo, y hash de la
        ''' carpeta (u32).</summary>
        Private Function FileEntry(name As String) As WbValueDef
            Return Wb.StructV(name,
                Wb.Int("File Hash", WbIntType.u32),
                Wb.Str("Extension", 4),
                Wb.Int("Folder Hash", WbIntType.u32))
        End Function

        Public Function ModelInfoValue(game As WbGame) As WbValueDef
            Dim texture = FileEntry("Texture")

            ' El arreglo de contadores lleva un PREFIJO u32 que dice cuántos contadores vienen;
            ' las etiquetas sólo les ponen nombre, no cambian el layout.
            Dim counterNames As String() =
                If(game = WbGame.Skyrim,
                   New String() {"Textures", "Addon Nodes"},
                   New String() {"Textures", "Addon Nodes", "Unknown", "Materials"})

            Dim newMembers As New List(Of WbValueDef) From {
                Wb.ArrayV("Counters", Wb.Int("Counter", WbIntType.u32), -1, Nothing, counterNames),
                Wb.ArrayV("Textures", texture, 0, "Counters\[0]"),
                Wb.ArrayV("Addon Nodes", Wb.Int("Addon Node", WbIntType.u32), 0, "Counters\[1]")
            }
            ' El bloque 'Materials' no existe en Skyrim: sólo aparece fuera de ese juego.
            If game <> WbGame.Skyrim Then
                newMembers.Add(Wb.ArrayV("Materials", FileEntry("Material"), 0, "Counters\[3]"))
            End If

            Dim branch0 = Wb.StructV("", Wb.EmptyV("Unused"), Wb.Bytes("Unused"), Wb.EmptyV("Unused"), Wb.EmptyV("Unused"))
            Dim branch1 = Wb.StructV("", Wb.EmptyV("Unused"), Wb.Bytes("ERROR"), Wb.EmptyV("Unused"), Wb.EmptyV("Unused"))
            Dim branch2 = Wb.StructV("", Wb.EmptyV("Unused"), Wb.ArrayV("Textures", texture, 0), Wb.EmptyV("Unused"), Wb.EmptyV("Unused"))
            Dim branch3 = Wb.StructV("", newMembers.ToArray())

            Return Wb.UnionV("Model Information", ModelInfoDecider(), branch0, branch1, branch2, branch3)
        End Function

        Public Function ModelInfoSub(sig As String, game As WbGame) As WbMemberDef
            Return Wb.Sub_(sig, ModelInfoValue(game))
        End Function

        '=====================================================================================
        ' Alternate Texture: contenido de MO2S / MO3S / MO4S / MO5S en SKYRIM.
        ' Las mismas firmas significan cosas distintas según el juego: en Fallout 4 son una
        ' referencia simple a MSWP, y en Skyrim un arreglo con prefijo u32 de esta estructura.
        '=====================================================================================

        Public Function AlternateTextures(sig As String) As WbMemberDef
            Return Wb.Sub_(sig, Wb.ArrayV("Alternate Textures",
                Wb.StructV("Alternate Texture",
                    Wb.LenStr("3D Name"),
                    Wb.Fid("New Texture", "TXST"),
                    Wb.Int("3D Index", WbIntType.s32)), -1))
        End Function

        '=====================================================================================
        ' Textured Model: el nombre de archivo del modelo, su bloque de model info, y después los
        ' subrecords de textura que agregue el llamador.
        ' El grupo puede ABRIR con cualquiera de sus firmas, no necesariamente con la primera: el
        ' nombre de archivo puede faltar y el bloque empezar por otro miembro.
        '=====================================================================================

        Public Function TexturedModel(name As String, sigModel As String, sigModelInfo As String,
                                      game As WbGame, ParamArray textureSubs As WbMemberDef()) As WbMemberDef
            Dim members As New List(Of WbMemberDef) From {
                Wb.StrSub(sigModel, "Model Filename"),
                ModelInfoSub(sigModelInfo, game)
            }
            For Each t In textureSubs
                If t IsNot Nothing Then members.Add(t)
            Next
            ' Además de poder abrir con cualquier miembro, los subrecords de este grupo pueden
            ' venir en cualquier orden.
            Return Wb.RStruct(name, members.ToArray()).WithAnyMember().WithUnordered()
        End Function

        '=====================================================================================
        ' Damage Type Array (DAMA): arreglo de pares tipo de daño / cantidad, más una referencia
        ' a tabla de curva que se agregó en versiones nuevas del formato.
        ' El paso del arreglo NO es constante: 8 bytes por entrada hasta la versión de formato 151
        ' y 12 desde la 152. Leerlo con paso fijo desalinea todas las entradas siguientes.
        '=====================================================================================

        Public Function DamageTypeArray(itemName As String) As WbMemberDef
            Return Wb.Sub_("DAMA", Wb.ArrayV(itemName & "s",
                Wb.StructV(itemName,
                    Wb.Fid("Type", "DMGT"),
                    Wb.Int("Amount", WbIntType.u32),
                    Wb.FromVersion(152, Wb.Fid("Curve Table", "CURV", "NULL"))), 0))
        End Function

        '=====================================================================================
        ' Destructible: salud, resistencias y etapas de destrucción del objeto.
        '=====================================================================================

        Public Function Dest(game As WbGame) As WbMemberDef
            Dim header = Wb.Sub_("DEST", Wb.StructV("Header",
                Wb.Int("Health", WbIntType.s32),
                Wb.Int("DEST Count", WbIntType.u8),
                Wb.Int("Flags", WbIntType.u8, "wbFlags"),
                Wb.Bytes("Unknown", 2)))

            Dim damc = Wb.Sub_("DAMC", Wb.ArrayV("Resistances",
                Wb.StructV("Resistance", Wb.Fid("Damage Type", "DMGT"), Wb.Int("Value", WbIntType.u32)), 0))

            Dim dstd = Wb.Sub_("DSTD", Wb.StructV("Destruction Stage Data",
                Wb.Int("Health %", WbIntType.u8),
                Wb.Int("Index", WbIntType.u8),
                Wb.Int("Model Damage Stage", WbIntType.u8),
                Wb.Int("Flags", WbIntType.u8, "wbFlags"),
                Wb.Int("Self Damage per Second", WbIntType.s32),
                Wb.Fid("Explosion", "EXPL", "NULL"),
                Wb.Fid("Debris", "DEBR", "NULL"),
                Wb.Int("Debris Count", WbIntType.s32)))
            dstd.Required = True

            ' Bloque 'Model' de la etapa: nombre de archivo, bloque de model info en DMDT, índice
            ' de remapeo de color y material swap.
            Dim dmdl = Wb.StrSub("DMDL", "Model FileName")
            dmdl.Required = True
            ' Los subrecords del bloque 'Model' pueden venir en cualquier orden.
            Dim modelStruct = Wb.RStruct("Model",
                dmdl,
                ModelInfoSub("DMDT", game),
                Wb.FltSub("DMDC", "Color Remapping Index"),
                Wb.FidSub("DMDS", "Material Swap", New String() {"MSWP"})).WithUnordered()

            Dim dstf = Wb.MarkerSub("DSTF", "End Marker")
            dstf.Required = True

            Dim stage = Wb.RStruct("Stage", dstd, Wb.StrSub("DSTA", "Sequence Name"), modelStruct, dstf)

            Return Wb.RStruct("Destructible", header, damc, Wb.RArray("Stages", stage))
        End Function

        '=====================================================================================
        ' Object Template: un contador OBTE, el arreglo de combinaciones (cada una con su marca
        ' OBTF, su nombre FULL y su bloque OBTS) y un marcador STOP de cierre.
        ' El contenido de OBTS queda declarado como PENDIENTE y se reporta como tal: el bloque de
        ' propiedades es grande y todavía no está descrito campo a campo. Marcarlo pendiente es
        ' deliberado; copiar sus bytes en silencio daría por entendido algo que no lo está.
        '=====================================================================================

        Public Function ObjectTemplate() As WbMemberDef
            Dim stop_ = Wb.MarkerSub("STOP", "Marker")
            stop_.Required = True
            Dim obts = Wb.PendingSub("OBTS", "Object Template Payload")
            obts.Required = True
            Dim combos = Wb.RArray("Combinations",
                Wb.RStruct("Combination",
                    Wb.MarkerSub("OBTF", "Editor Only"),
                    Wb.LStrSub("FULL", "Name"),
                    obts).WithUnordered())
            combos.CountPath = "OBTE\Count"
            Return Wb.RStruct("Object Template",
                Wb.IntSub("OBTE", "Count", WbIntType.u32, ""),
                combos,
                stop_)
        End Function

        '=====================================================================================
        ' Sculpt Data del ARMA: arreglo de conjuntos de modificadores de escala de hueso. Cada
        ' conjunto apunta a un género (BSMP) y lleva su lista de pares nombre de hueso (BSMB) y
        ' delta de escala en tres floats (BSMS).
        '=====================================================================================

        Public Function ArmorAddonSculptData() As WbMemberDef
            Dim bsms = Wb.Sub_("BSMS", Wb.Vec3("Bone Scale Delta"))
            bsms.Required = True
            Dim modifier = Wb.RStruct("Bone Scale Modifier",
                Wb.StrSub("BSMB", "Bone Name"),
                bsms)
            Dim item = Wb.RStruct("Bone Scale Modifier Set",
                Wb.IntSub("BSMP", "Target Gender", WbIntType.u32, "wbSexEnum"),
                Wb.RArray("Bone Scale Modifiers", modifier))
            Return Wb.RArray("Sculpt Data", item)
        End Function

    End Module

End Namespace
