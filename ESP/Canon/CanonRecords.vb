Imports System.Drawing

Namespace Canon

    ''' <summary>Punto de entrada de la aplicación a los records.
    '''
    ''' <para>Devuelve conceptos, no campos: "el color de este record de color de pelo" en vez de
    ''' "el u32 del CNAM". Los campos ya los da la vista generada; lo que vive acá es lo que hay que
    ''' INTERPRETAR, que es poco y es distinto en cada juego.</para>
    '''
    ''' <para>Nada de esto copia el record: cada propiedad consulta el árbol en el momento. Cambiar
    ''' un valor y guardar emite el cambio, sin pasos intermedios.</para></summary>
    Public Module CanonRecords

        ''' <summary>Color de pelo o de material.</summary>
        Public Function Color(rec As PluginRecord, plugins As PluginManager) As ColorRecord
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Return New ColorRecord(rec, raiz, ctx, New CanonResolver(rec, plugins))
        End Function

        ''' <summary>Una lista de formularios.</summary>
        Public Function FormList(rec As PluginRecord, plugins As PluginManager) As FormListRecord
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Return New FormListRecord(rec, raiz, ctx, New CanonResolver(rec, plugins))
        End Function

        ''' <summary>Un conjunto de equipo.</summary>
        Public Function Outfit(rec As PluginRecord, plugins As PluginManager) As OutfitRecord
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Return New OutfitRecord(rec, raiz, ctx, New CanonResolver(rec, plugins))
        End Function

        ''' <summary>Un objeto por defecto.</summary>
        Public Function DefaultObject(rec As PluginRecord, plugins As PluginManager) As DefaultObjectRecord
            Dim ctx As WbContext = Nothing
            Dim raiz = CanonBridge.Tree(rec, plugins, ctx)
            If raiz Is Nothing Then Return Nothing
            Return New DefaultObjectRecord(rec, raiz, ctx, New CanonResolver(rec, plugins))
        End Function

    End Module

    ''' <summary>Un record de color, visto igual en los dos juegos.
    '''
    ''' <para>Los dos guardan el color de forma distinta y hay que decirlo: en Fallout 4 es un solo
    ''' número de 32 bits cuyo significado depende de una bandera del propio record — o son cuatro
    ''' bytes de color, o es la fila de una paleta de remapeo. En Skyrim son siempre cuatro
    ''' componentes y no existe el concepto de paleta.</para>
    '''
    ''' <para>Aplicar la regla de un juego en el otro no da un color equivocado: da un color
    ''' INVENTADO. Un record de Skyrim con esa bandera en 2 se leería como fila de paleta y perdería
    ''' su color; guardarlo después lo dejaría en negro.</para></summary>
    Public NotInheritable Class ColorRecord

        Private ReadOnly _rec As PluginRecord
        Private ReadOnly _fo4 As ClfmFO4
        Private ReadOnly _sse As ClfmSSE

        Friend Sub New(rec As PluginRecord, node As WbNode, ctx As WbContext, resolver As CanonResolver)
            _rec = rec
            If ctx IsNot Nothing AndAlso ctx.Game = WbGame.Fallout4 Then
                _fo4 = New ClfmFO4(node, ctx, resolver)
            Else
                _sse = New ClfmSSE(node, ctx, resolver)
            End If
        End Sub

        Public ReadOnly Property FormID As UInteger
            Get
                Return _rec.Header.FormID
            End Get
        End Property

        Public ReadOnly Property EditorID As String
            Get
                Return If(_fo4 IsNot Nothing, _fo4.EditorID, _sse.EditorID)
            End Get
        End Property

        Public ReadOnly Property FullName As String
            Get
                Return If(_fo4 IsNot Nothing, _fo4.Name, _sse.Name)
            End Get
        End Property

        ''' <summary>Banderas del record. En Fallout 4 el bit 1 dice que el color es en realidad una
        ''' fila de paleta; en Skyrim el mismo campo es sólo "jugable".</summary>
        Public ReadOnly Property Flags As UInteger
            Get
                Return If(_fo4 IsNot Nothing, _fo4.Flags, _sse.Playable)
            End Get
        End Property

        ''' <summary>El record lleva un color propio. Un record sin campo de color no lo tiene, y
        ''' eso es distinto de tener el color negro.</summary>
        Public ReadOnly Property HasColor As Boolean
            Get
                If Not TieneCampoDeColor Then Return False
                If _sse IsNot Nothing Then Return True
                Return (_fo4.Flags And 2UI) = 0UI
            End Get
        End Property

        Private ReadOnly Property TieneCampoDeColor As Boolean
            Get
                Dim v As CanonView = If(DirectCast(_fo4, CanonView), DirectCast(_sse, CanonView))
                Return v IsNot Nothing AndAlso v.Node IsNot Nothing AndAlso
                       v.Node.BySignature("CNAM") IsNot Nothing
            End Get
        End Property

        Public ReadOnly Property Color As Color
            Get
                If Not HasColor Then Return Drawing.Color.Empty
                If _sse IsNot Nothing Then
                    Return Drawing.Color.FromArgb(_sse.ColorAlpha, _sse.ColorRed, _sse.ColorGreen, _sse.ColorBlue)
                End If
                Dim v = _fo4.ColorIndex
                Return Drawing.Color.FromArgb(CInt((v >> 24) And &HFFUI),
                                              CInt(v And &HFFUI),
                                              CInt((v >> 8) And &HFFUI),
                                              CInt((v >> 16) And &HFFUI))
            End Get
        End Property

        ''' <summary>El record apunta a una fila de la paleta de remapeo en vez de llevar color.
        ''' Sólo existe en Fallout 4.</summary>
        Public ReadOnly Property HasRemappingIndex As Boolean
            Get
                Return _fo4 IsNot Nothing AndAlso TieneCampoDeColor AndAlso (_fo4.Flags And 2UI) <> 0UI
            End Get
        End Property

        Public ReadOnly Property RemappingIndex As Single
            Get
                If Not HasRemappingIndex Then Return 0.0F
                Return BitConverter.ToSingle(BitConverter.GetBytes(_fo4.ColorIndex), 0)
            End Get
        End Property

    End Class

    ''' <summary>Una lista de formularios: agrupa records de cualquier tipo bajo un nombre.</summary>
    Public NotInheritable Class FormListRecord

        Private ReadOnly _rec As PluginRecord
        Private ReadOnly _fo4 As FlstFO4
        Private ReadOnly _sse As FlstSSE

        Friend Sub New(rec As PluginRecord, node As WbNode, ctx As WbContext, resolver As CanonResolver)
            _rec = rec
            If ctx IsNot Nothing AndAlso ctx.Game = WbGame.Fallout4 Then
                _fo4 = New FlstFO4(node, ctx, resolver)
            Else
                _sse = New FlstSSE(node, ctx, resolver)
            End If
        End Sub

        Public ReadOnly Property FormID As UInteger
            Get
                Return _rec.Header.FormID
            End Get
        End Property

        Public ReadOnly Property EditorID As String
            Get
                Return If(_fo4 IsNot Nothing, _fo4.EditorID, _sse.EditorID)
            End Get
        End Property

        ''' <summary>Los miembros de la lista. Se descarta el cero, que en este formato significa
        ''' "ninguno" y no un record valido.</summary>
        Public ReadOnly Property ItemFormIDs As List(Of UInteger)
            Get
                Dim salida As New List(Of UInteger)
                If _fo4 IsNot Nothing Then
                    For Each e In _fo4.FormIDs
                        If e.FormID <> 0UI Then salida.Add(e.FormID)
                    Next
                Else
                    For Each e In _sse.FormIDs
                        If e.FormID <> 0UI Then salida.Add(e.FormID)
                    Next
                End If
                Return salida
            End Get
        End Property
    End Class

    ''' <summary>Un conjunto de equipo: la lista de prendas que un personaje lleva puestas.</summary>
    Public NotInheritable Class OutfitRecord

        Private ReadOnly _rec As PluginRecord
        Private ReadOnly _fo4 As OtftFO4
        Private ReadOnly _sse As OtftSSE

        Friend Sub New(rec As PluginRecord, node As WbNode, ctx As WbContext, resolver As CanonResolver)
            _rec = rec
            If ctx IsNot Nothing AndAlso ctx.Game = WbGame.Fallout4 Then
                _fo4 = New OtftFO4(node, ctx, resolver)
            Else
                _sse = New OtftSSE(node, ctx, resolver)
            End If
        End Sub

        Public ReadOnly Property FormID As UInteger
            Get
                Return _rec.Header.FormID
            End Get
        End Property

        Public ReadOnly Property EditorID As String
            Get
                Return If(_fo4 IsNot Nothing, _fo4.EditorID, _sse.EditorID)
            End Get
        End Property

        Public ReadOnly Property ItemFormIDs As List(Of UInteger)
            Get
                Dim salida As New List(Of UInteger)
                If _fo4 IsNot Nothing Then
                    For Each e In _fo4.Items
                        If e.Item <> 0UI Then salida.Add(e.Item)
                    Next
                Else
                    For Each e In _sse.Items
                        If e.Item <> 0UI Then salida.Add(e.Item)
                    Next
                End If
                Return salida
            End Get
        End Property
    End Class

    ''' <summary>Un objeto por defecto: le da nombre a un record que el juego busca por rol en vez
    ''' de por identificador. Solo existe en Fallout 4.</summary>
    Public NotInheritable Class DefaultObjectRecord

        Private ReadOnly _rec As PluginRecord
        Private ReadOnly _fo4 As DfobFO4

        Friend Sub New(rec As PluginRecord, node As WbNode, ctx As WbContext, resolver As CanonResolver)
            _rec = rec
            If ctx IsNot Nothing AndAlso ctx.Game = WbGame.Fallout4 Then _fo4 = New DfobFO4(node, ctx, resolver)
        End Sub

        Public ReadOnly Property FormID As UInteger
            Get
                Return _rec.Header.FormID
            End Get
        End Property

        Public ReadOnly Property EditorID As String
            Get
                Return If(_fo4 Is Nothing, "", _fo4.EditorID)
            End Get
        End Property

        Public ReadOnly Property ObjectFormID As UInteger
            Get
                Return If(_fo4 Is Nothing, 0UI, _fo4.Object)
            End Get
        End Property
    End Class

End Namespace
