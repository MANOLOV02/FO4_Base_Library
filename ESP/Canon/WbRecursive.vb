Imports System.IO

Namespace Canon

    ''' <summary>Definición que se resuelve a la de un ANCESTRO en el árbol de DECLARACIONES.
    '''
    ''' <para>Sube la cantidad indicada de niveles por la cadena de DECLARACIONES —no por la de
    ''' nodos— y cachea el resultado. Es lo que permite declarar una estructura recursiva: dentro
    ''' del bloque de scripts VMAD, la estructura de una propiedad contiene un arreglo de esa misma
    ''' estructura.</para>
    '''
    ''' <para>Sin esto no se puede declarar ningún record que lleve VMAD, que son casi todos los
    ''' que admiten scripts: ACTI, ARMO, BOOK, CONT, DOOR, FLOR, FURN, INFO, INGR, KEYM, LIGH,
    ''' MGEF, MISC, MSTT, NOTE, NPC_, PACK, PERK, QUST, SCEN, STAT, TACT, TERM, TREE, WEAP.</para></summary>
    Public NotInheritable Class WbRecursiveDef
        Inherits WbValueDef

        Public ReadOnly Property LevelsUp As Integer
        Private _cached As WbValueDef

        Public Sub New(name As String, levelsUp As Integer)
            Me.Name = name
            _LevelsUp = levelsUp
        End Sub

        ''' <summary>Sube <see cref="LevelsUp"/> niveles por la cadena de declaraciones y cachea el
        ''' resultado. Nothing si la cadena se corta antes.</summary>
        Public Function Resolve() As WbValueDef
            If _cached IsNot Nothing Then Return _cached
            Dim cur As WbValueDef = Nothing
            For i = 1 To LevelsUp
                If i = 1 Then
                    cur = TryCast(DefParent, WbValueDef)
                Else
                    If cur Is Nothing Then Return Nothing
                    cur = TryCast(cur.DefParent, WbValueDef)
                End If
                If cur Is Nothing Then Return Nothing
            Next
            _cached = cur
            Return cur
        End Function

        Public Overrides Function Parse(ctx As WbContext, data As Byte(), offset As Integer, avail As Integer, parent As WbNode) As WbNode
            Dim tgt = Resolve()
            If tgt Is Nothing Then
                Dim n = NewNode()
                n.Parent = parent
                Throw New WbLayoutException(n.Path,
                    $"wbRecursive('{Name}', {LevelsUp}): no hay def a {LevelsUp} niveles de distancia")
            End If
            Dim node = tgt.Parse(ctx, data, offset, avail, parent)
            Return node
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            CType(node.Def, WbValueDef).Emit(node, bw, ctx)
        End Sub

        Public Overrides Function CreateDefault(ctx As WbContext) As WbNode
            Dim tgt = Resolve()
            If tgt Is Nothing Then Return NewNode()
            Return tgt.CreateDefault(ctx)
        End Function
    End Class

    ''' <summary>Codificación con la que se decodifica una hoja de texto. Cambia el juego de
    ''' caracteres, nunca el tamaño en bytes.</summary>
    Public Enum WbTextEncoding
        ''' <summary>Campos no traducibles: EditorID, rutas de archivo.</summary>
        General = 0
        ''' <summary>Campos traducibles, los que ve el jugador.</summary>
        Translatable = 1
        ''' <summary>Cadenas del bloque de scripts VMAD, que usa su propia codificación.</summary>
        Vmad = 2
    End Enum

    ''' <summary>Deciders de unión escritos a mano: la regla que elige, al parsear, cuál de las
    ''' variantes de una unión corresponde. Son lógica y no tabla, así que no se pueden derivar
    ''' automáticamente y se escriben uno por uno.
    ''' <para>El decider que falta devuelve <see cref="Unimplemented"/>, el record que lo usa queda
    ''' marcado como INCOMPLETO y se reporta como tal. Nunca se elige una rama "plausible".</para></summary>
    Partial Public Module WbDeciders

        ''' <summary>Decider ausente: siempre rama 0, y el record queda marcado como INCOMPLETO.
        ''' Existe para que el hueco sea RUIDOSO, no para taparlo.</summary>
        Public Function Unimplemented() As WbDecider
            Return Function(ctx, data, offset, avail, parent) 0
        End Function

        ''' <summary>Rama 1 si la versión de formato del record cae dentro del rango indicado
        ''' (ambos extremos incluidos), 0 si no.</summary>
        Public Function FormVersionRange(minV As Integer, maxV As Integer) As WbDecider
            Return Function(ctx, data, offset, avail, parent)
                       Dim v = CInt(ctx.FormVersion)
                       Return If(v >= minV AndAlso v <= maxV, 1, 0)
                   End Function
        End Function

        ''' <summary>Elección de variante del bloque de model info (MODT y sus parientes).</summary>
        Public Function ModelInfo() As WbDecider
            Return WbCommon.ModelInfoDecider()
        End Function

        '-----------------------------------------------------------------------------------------
        ' Lectura de hermanos: casi todos los deciders despachan por el valor YA PARSEADO de otro
        ' campo del mismo contenedor, no por los bytes crudos.
        '-----------------------------------------------------------------------------------------

        ''' <summary>Valor entero de un hermano por nombre, subiendo por los ancestros. Nothing si
        ''' todavía no existe; en ese caso el decider que lo consulta cae a la rama 0.</summary>
        Friend Function Sibling(parent As WbNode, name As String) As Long?
            If parent Is Nothing Then Return Nothing
            Dim n = WbPath.ResolveUpwards(parent, name)
            If n Is Nothing Then n = parent.ByName(name)
            If n Is Nothing OrElse n.Value Is Nothing Then Return Nothing
            Try
                Return Convert.ToInt64(n.Value)
            Catch
                Return Nothing
            End Try
        End Function

    End Module

End Namespace
