Namespace Canon

    ''' <summary>DSL con el que se declara el layout de los records: una función corta por cada
    ''' construcción del formato, para que la declaración de un record se lea como una lista de
    ''' campos en vez de como código de parseo.
    ''' <para>La capa B declara VALORES (lo que hay adentro de un subrecord) y la capa A declara
    ''' MIEMBROS (los subrecords y sus agrupaciones).</para></summary>
    Public Module Wb

        '--- Capa B: valores -------------------------------------------------------------

        Public Function Int(name As String, t As WbIntType, Optional enumName As String = "") As WbValueDef
            Return New WbIntegerDef(name, t, enumName)
        End Function

        Public Function Flt(name As String) As WbValueDef
            Return New WbFloatDef(name)
        End Function

        ''' <summary>Referencia a otro record. <paramref name="allowed"/> son las firmas de record
        ''' a las que puede apuntar; <c>"FFFF"</c> entre ellas indica que el valor
        ''' <c>0xFFFFFFFF</c> es un centinela ("ninguno" o "todos") y por lo tanto NO se remapea al
        ''' cambiar los índices de master.</summary>
        Public Function Fid(name As String, ParamArray allowed As String()) As WbValueDef
            Return New WbFormIdDef(name, allowed)
        End Function

        ''' <summary>Cadena NO traducible: EditorID, rutas de modelo y demás texto interno.</summary>
        Public Function Str(name As String, Optional fixedLength As Integer = 0) As WbValueDef
            Return New WbStringDef(name, fixedLength, translatable:=False)
        End Function

        ''' <summary>Cadena traducible guardada dentro del propio subrecord; se decodifica con la
        ''' codificación de textos traducibles, no con la interna.</summary>
        Public Function StrT(name As String, Optional fixedLength As Integer = 0) As WbValueDef
            Return New WbStringDef(name, fixedLength, translatable:=True)
        End Function

        ''' <summary>Cadena localizable: id u32 si el archivo fuente está localizado, zstring si no.</summary>
        Public Function LStr(name As String) As WbValueDef
            Return New WbLStringDef(name)
        End Function

        ''' <summary>Cadena precedida por su longitud (prefijo de 4 bytes por defecto).</summary>
        Public Function LenStr(name As String, Optional prefixWidth As Integer = 4,
                               Optional enc As WbTextEncoding = WbTextEncoding.General) As WbValueDef
            Return New WbLenStringDef(name, prefixWidth, enc)
        End Function

        ''' <summary>Remite a la definición que está <paramref name="levelsUp"/> niveles más
        ''' arriba en el árbol de DECLARACIONES (no en el de nodos). Es la forma de declarar una
        ''' estructura que se contiene a sí misma, como las propiedades anidadas del VMAD.</summary>
        Public Function RecursiveV(name As String, levelsUp As Integer) As WbValueDef
            Return New WbRecursiveDef(name, levelsUp)
        End Function

        Public Function Bytes(name As String, Optional size As Integer = -1) As WbValueDef
            Return New WbByteArrayDef(name, size)
        End Function

        ''' <summary>Relleno sin significado: con tamaño, esa cantidad de bytes; sin tamaño, CERO
        ''' bytes, o sea que no ocupa nada en el archivo.</summary>
        Public Function Unused(Optional size As Integer = 0) As WbValueDef
            If size <= 0 Then Return New WbEmptyDef("Unused")
            Return New WbByteArrayDef("Unused", size)
        End Function

        Public Function EmptyV(name As String) As WbValueDef
            Return New WbEmptyDef(name)
        End Function

        Public Function StructV(name As String, ParamArray members As WbValueDef()) As WbStructDef
            Return New WbStructDef(name, members)
        End Function

        Public Function ArrayV(name As String, element As WbValueDef, Optional count As Integer = -1,
                               Optional countPath As String = Nothing, Optional elementNames As String() = Nothing) As WbValueDef
            Return New WbArrayDef(name, element, count, countPath, elementNames)
        End Function

        ''' <summary>Arreglo cuya cantidad de elementos no está en un campo del archivo sino que
        ''' se calcula recorriendo el árbol ya parseado.</summary>
        Public Function ArrayC(name As String, element As WbValueDef, counter As WbCounter) As WbValueDef
            Return New WbArrayDef(name, element, 0, Nothing, Nothing, counter)
        End Function

        Public Function UnionV(name As String, decider As WbDecider, ParamArray members As WbValueDef()) As WbValueDef
            Return New WbUnionDef(name, decider, members)
        End Function

        ''' <summary>Campo que sólo existe a partir de la versión de formato indicada: una unión
        ''' entre "nada" y el valor.
        ''' <para>Es la forma de expresar que el TAMAÑO de una estructura depende de la versión.
        ''' Gracias a esto el paso de un arreglo deja de ser una constante: <c>DAMA</c> mide 8
        ''' bytes por entrada hasta la versión 151 y 12 desde la 152, y la declaración lo expresa
        ''' sola.</para></summary>
        Public Function FromVersion(version As Integer, value As WbValueDef) As WbValueDef
            Return New WbUnionDef(value.Name, VersionDecider(version), New WbValueDef() {New WbEmptyDef(value.Name), value})
        End Function

        ''' <summary>Elige la rama 1 si la versión de formato del record es mayor o igual a la
        ''' indicada, y la 0 si no.</summary>
        Public Function VersionDecider(version As Integer) As WbDecider
            Return Function(ctx, data, offset, avail, parent) If(CInt(ctx.FormVersion) >= version, 1, 0)
        End Function

        Public Function Vec3(name As String) As WbValueDef
            Return StructV(name, Flt("X"), Flt("Y"), Flt("Z"))
        End Function

        '--- Capa A: miembros ------------------------------------------------------------

        ''' <summary>Subrecord suelto.</summary>
        Public Function Sub_(sig As String, value As WbValueDef) As WbSubrecordDef
            Dim d As New WbSubrecordDef(sig, value)
            Return d
        End Function

        Public Function FidSub(sig As String, name As String, allowed As String()) As WbSubrecordDef
            Return Sub_(sig, Fid(name, allowed))
        End Function

        Public Function StrSub(sig As String, name As String) As WbSubrecordDef
            Return Sub_(sig, Str(name))
        End Function

        Public Function LStrSub(sig As String, name As String) As WbSubrecordDef
            Return Sub_(sig, LStr(name))
        End Function

        Public Function IntSub(sig As String, name As String, t As WbIntType, Optional enumName As String = "") As WbSubrecordDef
            Return Sub_(sig, Int(name, t, enumName))
        End Function

        Public Function FltSub(sig As String, name As String) As WbSubrecordDef
            Return Sub_(sig, Flt(name))
        End Function

        ''' <summary>Subrecord marcador: está presente, pero su carga útil mide cero bytes (STOP, DSTF…).</summary>
        Public Function MarkerSub(sig As String, name As String) As WbSubrecordDef
            Return Sub_(sig, EmptyV(name))
        End Function

        Public Function RStruct(name As String, ParamArray members As WbMemberDef()) As WbRStructDef
            Return New WbRStructDef(name, members)
        End Function

        Public Function RArray(name As String, element As WbMemberDef) As WbRArrayDef
            Return New WbRArrayDef(name, element)
        End Function

        Public Function RUnion(name As String, ParamArray members As WbMemberDef()) As WbRUnionDef
            Return New WbRUnionDef(name, members)
        End Function

        ''' <summary>Subrecord conocido pero TODAVÍA SIN DESCRIBIR campo a campo. Sus bytes se
        ''' consumen enteros y el reporte lo cuenta aparte, en el grupo de pendientes.
        ''' <para>Existe para que un record cuyo contenido no se entendió no pueda darse por bueno.
        ''' Copiar los bytes en silencio produce una salida idéntica a la original sin que nadie
        ''' note que el subrecord nunca se interpretó.</para></summary>
        Public Function PendingSub(sig As String, name As String) As WbSubrecordDef
            Dim d = Sub_(sig, Bytes(name))
            d.IsPending = True
            Return d
        End Function

    End Module

End Namespace
