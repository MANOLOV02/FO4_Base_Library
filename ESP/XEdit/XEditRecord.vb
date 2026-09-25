' ============================================================================================
' Este archivo transcribe comportamiento de xEdit (TES5Edit), que esta bajo Mozilla Public
' License 2.0, y por lo tanto es una obra derivada de el.
'
' This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
' If a copy of the MPL was not distributed with this file, You can obtain one at
' https://mozilla.org/MPL/2.0/
'
' Proyecto original: https://github.com/TES5Edit/TES5Edit  (ElminsterAU y colaboradores)
' Las citas `wbI:` / `wbIf:` son de wbImplementation.pas / wbInterface.pas del tag xedit-4.1.5q.
' ============================================================================================
Option Strict On
Option Infer On

Imports FO4_Base_Library.Canon

Namespace XEdit

    ''' <summary>
    ''' Una VERSION de un record: la que trae un archivo del orden de carga, o la que vive en el parche que se
    ''' esta generando. Es lo que en xEdit es un <c>IwbMainRecord</c> dentro de un <c>IwbFile</c>.
    ''' <para>El arbol se arma recien al primer acceso: el orden de carga del sorter tiene ~16.000 records de las
    ''' firmas procesadas y ~500.000 en total, y el 95 % nunca se abre.</para>
    ''' </summary>
    Public NotInheritable Class XEditRecord

        Public ReadOnly Property Session As XEditSession
        ''' <summary>Version en disco; Nothing para un record del parche.</summary>
        Public ReadOnly Property Source As PluginRecord
        ''' <summary>Archivo al que pertenece ESTA version.</summary>
        Public ReadOnly Property FileName As String
        ''' <summary>Posicion del archivo en el orden de carga (la del parche es su punto de corte).</summary>
        Public ReadOnly Property LoadOrderIndex As Integer
        Public ReadOnly Property IsPatch As Boolean
        Public ReadOnly Property Signature As String
        ''' <summary>FormID en el espacio del orden de carga (el que xEdit muestra con
        ''' <c>wbDisplayLoadOrderFormID</c>).</summary>
        Public Property FormID As UInteger

        Private _root As WbNode
        Private _ctx As WbContext

        Friend Sub New(session As XEditSession, source As PluginRecord, fileName As String, loadOrderIndex As Integer)
            _Session = session
            _Source = source
            _FileName = fileName
            _LoadOrderIndex = loadOrderIndex
            _IsPatch = False
            _Signature = source.Header.Signature
            _FormID = source.Header.FormID
        End Sub

        ''' <summary>Record del parche, con un arbol ya armado (copia o record nuevo).</summary>
        Friend Sub New(session As XEditSession, signature As String, formID As UInteger, root As WbNode, ctx As WbContext,
                       fileName As String, loadOrderIndex As Integer)
            _Session = session
            _Source = Nothing
            _FileName = fileName
            _LoadOrderIndex = loadOrderIndex
            _IsPatch = True
            _Signature = signature
            _FormID = formID
            _root = root
            _ctx = ctx
        End Sub

        ''' <summary>El arbol de campos (referencias ya en el espacio del orden de carga).</summary>
        Public ReadOnly Property Root As WbNode
            Get
                EnsureTree()
                Return _root
            End Get
        End Property

        Public ReadOnly Property Context As WbContext
            Get
                EnsureTree()
                Return _ctx
            End Get
        End Property

        Public ReadOnly Property HasTree As Boolean
            Get
                Return _root IsNot Nothing
            End Get
        End Property

        Private Sub EnsureTree()
            If _root IsNot Nothing Then Return
            Dim ctx As WbContext = Nothing
            _root = CanonBridge.Tree(_Source, _Session.Plugins, ctx)
            If _root Is Nothing Then
                Throw New InvalidOperationException("Record type " & _Signature & " is not declared in the schema.")
            End If
            ctx.FormID = _FormID
            _ctx = ctx
        End Sub

        ''' <summary>Descarta el arbol de una version de disco (se vuelve a armar si se pide). Los del parche no.</summary>
        Public Sub ReleaseTree()
            If Not _IsPatch Then
                _root = Nothing
                _ctx = Nothing
            End If
        End Sub

        ''' <summary>Copia del arbol de un record del PARCHE (para deshacer un bloque de ediciones).</summary>
        Public Function SnapshotTree() As WbNode
            If Not _IsPatch Then Throw New InvalidOperationException("Only patch records are edited.")
            Return Root.Clonar()
        End Function

        ''' <summary>Vuelve el arbol de un record del PARCHE a una copia tomada con <see cref="SnapshotTree"/>.</summary>
        Public Sub RestoreTree(snapshot As WbNode)
            If Not _IsPatch Then Throw New InvalidOperationException("Only patch records are edited.")
            _root = snapshot.Clonar()
        End Sub

        ''' <summary>Flags de la cabecera del record.</summary>
        Public ReadOnly Property RecordFlags As UInteger
            Get
                If _root IsNot Nothing Then Return _ctx.RecordFlags
                Return _Source.Header.Flags
            End Get
        End Property

        Public ReadOnly Property FormVersion As UShort
            Get
                If _root IsNot Nothing Then Return _ctx.FormVersion
                Return _Source.Header.Version
            End Get
        End Property

        Public ReadOnly Property IsDeleted As Boolean
            Get
                Return (RecordFlags And &H20UI) <> 0UI
            End Get
        End Property

        ''' <summary>EditorID (EDID). Para una version de disco se lee sin armar el arbol.</summary>
        Public ReadOnly Property EditorID As String
            Get
                If _root IsNot Nothing Then
                    Dim n = _root.BySignature("EDID")
                    If n Is Nothing OrElse n.ChildCount = 0 Then Return ""
                    Return If(TryCast(n.Children(0).Value, String), "")
                End If
                Return If(_Source.EditorID, "")
            End Get
        End Property

        ''' <summary>El elemento RAIZ de este record, con la vista de xEdit (cabecera + miembros).</summary>
        Public Function Element() As XElement
            Return XElement.ForRecord(Me)
        End Function

        Public Overrides Function ToString() As String
            Return $"{Signature}:{FormID:X8} {EditorID} [{FileName}]"
        End Function

    End Class

End Namespace
