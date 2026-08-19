Imports System.IO

Namespace Canon

    ''' <summary>Escritor: emite el MISMO árbol que produjo <see cref="WbReader"/>, recorriéndolo en
    ''' orden. Para todo lo DECLARADO no hay ninguna ruta que copie bytes del original — ni colas
    ''' por firma, ni bloques contiguos rescatados a mano.
    '''
    ''' <para>La única excepción: un subrecord marcado como PENDIENTE (<c>VMAD</c> y el payload de
    ''' <c>OBTS</c>) SÍ se re-emite copiando bytes, y sus FormID quedan invisibles para el remapper
    ''' de masters. Por eso emitir un pendiente <b>TIRA</b> salvo que se prenda
    ''' <c>WbContext.AllowPendingSubrecords</c>, cosa que sólo hace el arnés de medición. Mientras
    ''' queden pendientes, ARMO no se puede migrar.</para>
    '''
    ''' <para><b>Por qué esto elimina las tres copias del layout de ARMO.</b> Cableado a mano, el
    ''' orden de subrecords es una SECUENCIA DE LLAMADAS, y hay una secuencia por modo (override
    ''' Fallout 4, override Skyrim, nuevo Skyrim). Acá el orden es el de
    ''' <c>WbRecordDef.Members</c>, o sea la declaración del record:</para>
    ''' <list type="bullet">
    ''' <item><b>override Fallout 4</b> = <c>WbReader.Parse</c> con el esquema de Fallout 4 → mutar → emitir.</item>
    ''' <item><b>override Skyrim</b> = lo mismo con el esquema de Skyrim.</item>
    ''' <item><b>nuevo Skyrim</b> = <c>WbReader.CreateNew</c> con el esquema de Skyrim → mutar → emitir.</item>
    ''' </list>
    ''' <para>Un solo emisor para los tres. La presencia de cada subrecord viene del ÁRBOL, así que
    ''' un override reproduce lo que había y un record nuevo arranca con sus miembros requeridos —
    ''' sin ningún parámetro <c>required</c> por call site.</para>
    '''
    ''' <para>Y el caso del <c>MODC</c>: un escritor con una cola por firma tiene que RECHAZAR un
    ''' ARMO con dos MODC, porque no puede saber si el segundo es del struct Male o del Female. En
    ''' el árbol cada MODC es hijo de su struct: la pregunta no existe.</para>
    ''' </summary>
    Public NotInheritable Class WbWriter

        Private Sub New()
        End Sub

        Public Const SUBRECORD_HEADER_SIZE As Integer = 6
        Private Const MAX_U16 As Integer = &HFFFF

        ''' <summary>Emite el cuerpo del record: la secuencia de subrecords, sin el header de 24
        ''' bytes. Es lo que se compara contra los bytes de la fuente para C2.</summary>
        Public Shared Function EmitBody(root As WbNode, ctx As WbContext) As Byte()
            ' La Form Version con la que se PARSEÓ eligió las ramas de unión que dependen de ella.
            ' Emitir con otra deja los bytes de campos como MODT o DAMA en un formato que el header
            ' del record desmiente, y nada más lo notaría.
            If root.ParsedFormVersion >= 0 AndAlso CInt(ctx.FormVersion) <> root.ParsedFormVersion Then
                Throw New InvalidOperationException(
                    $"EmitBody: el árbol se parseó con Form Version {root.ParsedFormVersion} y se está " &
                    $"emitiendo con {CInt(ctx.FormVersion)}. Las ramas de unión ya quedaron fijadas por la " &
                    "versión de origen; cambiarla al emitir produce bytes que el header contradice.")
            End If
            SyncCounters(root)
            Using ms As New MemoryStream()
                Using bw As New BinaryWriter(ms)
                    For Each c In root.Children
                        CType(c.Def, WbMemberDef).Emit(c, bw, ctx)
                    Next
                End Using
                Return ms.ToArray()
            End Using
        End Function

        ''' <summary>Escribe un subrecord con su header de 6 bytes (firma + tamaño u16).
        ''' <para>Si el cuerpo no entra en un u16 se antepone un <c>XXXX</c> con el tamaño real en
        ''' u32 y el subrecord real declara tamaño 0 — la convención que
        ''' <c>PluginReader.ParseSubrecords</c> deshace al leer.</para></summary>
        Public Shared Sub EmitSubrecord(bw As BinaryWriter, sig As String, body As Byte())
            If body Is Nothing Then body = Array.Empty(Of Byte)()
            If body.Length > MAX_U16 Then
                WriteHeader(bw, "XXXX", 4)
                bw.Write(CUInt(body.Length))
                WriteHeader(bw, sig, 0)
            Else
                WriteHeader(bw, sig, body.Length)
            End If
            If body.Length > 0 Then bw.Write(body)
        End Sub

        ''' <summary>PASE PREVIO: recalcula TODOS los contadores remotos antes de emitir un solo
        ''' byte.
        '''
        ''' <para>Sin este pase el orden lo arruina: en un bloque de keywords el <c>KSIZ</c> se
        ''' emite ANTES que el <c>KWDA</c>, así que recalcularlo dentro del <c>Emit</c> del array lo
        ''' actualiza cuando el contador ya se escribió. El resultado es un <c>KSIZ</c> con el valor
        ''' viejo y un <c>KWDA</c> más corto: el archivo sale corrupto y el LECTOR se cae al
        ''' releerlo.</para></summary>
        Public Shared Sub SyncCounters(root As WbNode)
            ' De adentro hacia afuera: un array anidado tiene que fijar su contador antes de que el
            ' de afuera cuente.
            For Each c In root.Children
                SyncCounters(c)
            Next
            Dim vArr = TryCast(root.Def, WbArrayDef)
            If vArr IsNot Nothing AndAlso Not String.IsNullOrEmpty(vArr.CountPath) _
               AndAlso root.ParsedCount <> root.Children.Count Then
                Dim cn = WbPath.ResolveUpwards(root, vArr.CountPath)
                If cn IsNot Nothing Then cn.Value = CLng(root.Children.Count)
            End If
            Dim mArr = TryCast(root.Def, WbRArrayDef)
            If mArr IsNot Nothing AndAlso Not String.IsNullOrEmpty(mArr.CountPath) _
               AndAlso root.ParsedCount <> root.Children.Count Then
                Dim cn = WbPath.ResolveUpwards(root, mArr.CountPath)
                If cn IsNot Nothing Then cn.Value = CLng(root.Children.Count)
            End If
        End Sub

        Private Shared Sub WriteHeader(bw As BinaryWriter, sig As String, size As Integer)
            If sig Is Nothing OrElse sig.Length <> 4 Then
                Throw New InvalidOperationException($"firma de subrecord inválida: '{sig}'")
            End If
            For i = 0 To 3
                bw.Write(CByte(Microsoft.VisualBasic.Strings.AscW(sig(i)) And &HFF))
            Next
            bw.Write(CUShort(size And &HFFFF))
        End Sub

    End Class

    ''' <summary>Recorre el árbol tocando EXCLUSIVAMENTE los nodos cuya def es
    ''' <see cref="WbFormIdDef"/>. Es la puerta única del remapper de índices de master.
    '''
    ''' <para>La garantía es ESTRUCTURAL, no una convención de nombres: un entero de 32 bits que
    ''' codifica un índice de enum no puede entrar aunque mida los mismos 4 bytes, porque su def es
    ''' <see cref="WbIntegerDef"/>. Así es como <c>WEAP.Skill</c>, <c>WEAP.Resist</c> y
    ''' <c>MGEF.ResistValue</c> dejan de poder salir como referencia a otro mod: no existe la
    ''' ruta.</para>
    ''' <para>El centinela <c>0xFFFFFFFF</c> se saltea cuando el campo declara la pseudo-firma
    ''' <c>FFFF</c> entre sus destinos permitidos — ahí significa "ninguno/todos", no una
    ''' referencia.</para></summary>
    Public NotInheritable Class WbFormIdWalker

        Private Sub New()
        End Sub

        Public Const SENTINEL As UInteger = &HFFFFFFFFUI

        ''' <summary>Aplica <paramref name="map"/> a cada FormID del árbol. Devuelve cuántos tocó.</summary>
        Public Shared Function Remap(root As WbNode, map As Func(Of UInteger, UInteger)) As Integer
            Dim touched = 0
            For Each n In Enumerate(root)
                Dim v = CUInt(n.Value)
                If v = SENTINEL AndAlso CType(n.Def, WbFormIdDef).AllowsSentinel Then Continue For
                n.Value = map(v)
                touched += 1
            Next
            Return touched
        End Function

        ''' <summary>Todos los nodos que SON una referencia. Nada más entra en esta lista.</summary>
        Public Shared Iterator Function Enumerate(root As WbNode) As IEnumerable(Of WbNode)
            For Each n In root.Walk()
                If TypeOf n.Def Is WbFormIdDef AndAlso n.Value IsNot Nothing Then Yield n
            Next
        End Function
    End Class

End Namespace
