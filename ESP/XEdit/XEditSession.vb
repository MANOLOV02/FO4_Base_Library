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
    ''' El orden de carga visto como lo ve xEdit: cada FormID con TODAS sus versiones (la cadena de overrides) y,
    ''' ademas, un archivo de salida en construccion que ocupa su lugar en el orden de carga.
    ''' <para>Se arma SOBRE un <see cref="PluginManager"/> ya cargado, sin tocarlo: el gestor guarda solo la
    ''' version ganadora de cada record, pero cada <see cref="PluginReader"/> sigue teniendo las suyas, asi que la
    ''' cadena se reconstruye recorriendo los lectores en orden. Es opt-in por construccion: las apps que no crean
    ''' una sesion no pagan nada.</para>
    ''' <para>El archivo de salida (el parche) NO es un archivo de disco: sus records viven aca. El que ya exista en
    ''' disco con ese nombre, y todo archivo cuyo autor sea uno de los excluidos, quedan FUERA de la cadena: un
    ''' parche viejo no puede ser fuente del nuevo.</para>
    ''' </summary>
    Public NotInheritable Class XEditSession

        Private NotInheritable Class FileEntry
            Public Name As String
            Public Index As Integer
            Public Reader As PluginReader
            Public Excluded As Boolean
        End Class

        Public ReadOnly Property Plugins As PluginManager
        Public ReadOnly Property OutputFileName As String
        ''' <summary>Posicion del parche en el orden de carga: la del archivo de salida si esta cargado, o el final
        ''' (<c>AddNewFileName</c> agrega al final, xeMainForm.pas:1898-1900).</summary>
        Public ReadOnly Property CutIndex As Integer
        ''' <summary>Byte alto del <c>LoadOrderFormID</c> de los records PROPIOS del parche.</summary>
        Public ReadOnly Property PatchFullSlot As UInteger

        ''' <summary>
        ''' La clave con la que xEdit ordena los records de un GRUP al guardar: <c>LoadOrderFormID</c>, sin signo
        ''' (CompareGroupContents, wbI:18392-18400; <c>wbDisplayLoadOrderFormID := True</c>, xeMainForm.pas:5150).
        ''' Un override lleva el FormID global del record (slots full y light particionados, los light con 0xFE
        ''' quedan al final); uno nuevo, el slot del parche con el object id que le toco.
        ''' <para>Medido sobre un parche de referencia: los 15 GRUP respetan esta clave,
        ''' incluido NOTE, donde un .esp con flag 0x200 (DCRE.esp) va detras de los .esl.</para>
        ''' </summary>
        Public Function LoadOrderSortKey(globalFormID As UInteger, isNew As Boolean, newObjectId As UInteger) As UInteger
            If isNew Then Return (_PatchFullSlot << 24) Or (newObjectId And &HFFFFFFUI)
            Return globalFormID
        End Function

        Private ReadOnly _files As New List(Of FileEntry)
        Private ReadOnly _byName As New Dictionary(Of String, FileEntry)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _chains As New Dictionary(Of UInteger, List(Of XEditRecord))
        Private ReadOnly _patch As New Dictionary(Of UInteger, XEditRecord)
        Private ReadOnly _patchOrder As New List(Of XEditRecord)
        ' Masters de cada archivo de SALIDA (el principal y sus -partN), en el orden en que se agregaron.
        Private ReadOnly _patchMasters As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _visibleCache As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase)

        ''' <param name="excludedAuthors">Autores (CNAM del TES4) cuyos archivos no son fuente.</param>
        Public Sub New(pm As PluginManager, outputFileName As String, excludedAuthors As IEnumerable(Of String))
            _Plugins = pm
            _OutputFileName = outputFileName
            Dim autores As New HashSet(Of String)(If(excludedAuthors, Array.Empty(Of String)()), StringComparer.Ordinal)
            Dim cut = -1
            For i = 0 To pm.Plugins.Count - 1
                Dim r = pm.Plugins(i)
                Dim fe As New FileEntry With {.Name = r.FileName, .Index = i, .Reader = r}
                If String.Equals(r.FileName, outputFileName, StringComparison.OrdinalIgnoreCase) Then
                    fe.Excluded = True
                    cut = i
                ElseIf autores.Contains(If(r.Author, "")) Then
                    fe.Excluded = True
                End If
                _files.Add(fe)
                _byName(fe.Name) = fe
            Next
            _CutIndex = If(cut >= 0, cut, pm.Plugins.Count)
            ' Slot FULL del parche: los slots full se cuentan aparte de los light (0xFE), como en el FormID global de
            ' la libreria. El parche es un .esp sin flag light, asi que su slot es la cantidad de archivos full
            ' cargados antes del corte.
            Dim full = 0
            For i = 0 To Math.Min(_CutIndex, pm.Plugins.Count) - 1
                If Not pm.Plugins(i).IsESL Then full += 1
            Next
            _PatchFullSlot = CUInt(full)
            pm.RunUnderRecordsReadLock(
                Function()
                    For Each fe In _files
                        If fe.Excluded Then Continue For
                        For Each rec In fe.Reader.Records.Values
                            Dim fid = rec.Header.FormID
                            Dim chain As List(Of XEditRecord) = Nothing
                            If Not _chains.TryGetValue(fid, chain) Then
                                chain = New List(Of XEditRecord)(2)
                                _chains(fid) = chain
                            End If
                            chain.Add(New XEditRecord(Me, rec, fe.Name, fe.Index))
                        Next
                    Next
                    Return True
                End Function)
        End Sub

        ' ======================================================================================== archivos

        ''' <summary>Archivos que son fuente, en orden de carga (sin el de salida ni los excluidos).</summary>
        Public Function SourceFileNames() As List(Of String)
            Return _files.Where(Function(f) Not f.Excluded).Select(Function(f) f.Name).ToList()
        End Function

        ''' <summary>Todos los archivos cargados, en orden, con si son fuente o no.</summary>
        Public Function LoadedFiles() As List(Of KeyValuePair(Of String, Boolean))
            Return _files.Select(Function(f) New KeyValuePair(Of String, Boolean)(f.Name, Not f.Excluded)).ToList()
        End Function

        Public Function IsExcluded(fileName As String) As Boolean
            Dim fe As FileEntry = Nothing
            Return _byName.TryGetValue(fileName, fe) AndAlso fe.Excluded
        End Function

        Public Function IsLoaded(fileName As String) As Boolean
            Return _byName.ContainsKey(fileName)
        End Function

        Public Function LoadOrderIndexOf(fileName As String) As Integer
            If IsPatchFile(fileName) Then Return _CutIndex
            Dim fe As FileEntry = Nothing
            If _byName.TryGetValue(fileName, fe) Then Return fe.Index
            Return -1
        End Function

        Public Function Reader(fileName As String) As PluginReader
            Dim fe As FileEntry = Nothing
            If _byName.TryGetValue(fileName, fe) Then Return fe.Reader
            Return Nothing
        End Function

        ''' <summary>Los archivos cargados DESPUES del parche (se avisa antes de generar).</summary>
        Public Function FilesAfterCut() As List(Of String)
            Return _files.Where(Function(f) f.Index > _CutIndex AndAlso Not f.Excluded).Select(Function(f) f.Name).ToList()
        End Function

        ' ======================================================================================== versiones

        ''' <summary>
        ''' Las versiones de una firma que trae UN archivo, en el orden del lector (<c>GroupBySignature(file, sig)</c>).
        ''' Para un archivo fuente devuelve las mismas instancias de la cadena; para un archivo EXCLUIDO (la salida en
        ''' disco, un autor excluido) arma versiones sueltas: solo las usa el modo replica del indice de EDID.
        ''' </summary>
        Public Iterator Function FileVersions(fileName As String, sig As String) As IEnumerable(Of XEditRecord)
            Dim fe As FileEntry = Nothing
            If Not _byName.TryGetValue(fileName, fe) Then Return
            For Each rec In fe.Reader.Records.Values
                If rec.Header.Signature <> sig Then Continue For
                If fe.Excluded Then
                    Yield New XEditRecord(Me, rec, fe.Name, fe.Index)
                Else
                    For Each v In Chain(rec.Header.FormID)
                        If ReferenceEquals(v.Source, rec) Then Yield v : Exit For
                    Next
                End If
            Next
        End Function

        ''' <summary>Las versiones EN DISCO de un FormID, en orden de carga (sin el parche).</summary>
        Public Function Chain(fid As UInteger) As IReadOnlyList(Of XEditRecord)
            Dim c As List(Of XEditRecord) = Nothing
            If _chains.TryGetValue(fid, c) Then Return c
            Return Array.Empty(Of XEditRecord)()
        End Function

        ''' <summary>Todos los FormID de una firma que tienen alguna version en disco.</summary>
        Public Iterator Function FormIDsOfSignature(sig As String) As IEnumerable(Of UInteger)
            For Each kv In _chains
                If kv.Value.Count > 0 AndAlso kv.Value(0).Signature = sig Then Yield kv.Key
            Next
        End Function

        ''' <summary>La version de este FormID en el parche, o Nothing.</summary>
        Public Function PatchRecord(fid As UInteger) As XEditRecord
            Dim r As XEditRecord = Nothing
            _patch.TryGetValue(fid, r)
            Return r
        End Function

        ''' <summary>Los records del parche en el orden en que se agregaron.</summary>
        Public ReadOnly Property PatchRecords As IReadOnlyList(Of XEditRecord)
            Get
                Return _patchOrder
            End Get
        End Property

        Friend Sub AddPatchRecord(r As XEditRecord)
            If _patch.ContainsKey(r.FormID) Then Throw New InvalidOperationException($"{r.FormID:X8} is already in the patch.")
            _patch(r.FormID) = r
            _patchOrder.Add(r)
        End Sub

        Private _nextProvisional As UInteger = 0UI

        ''' <summary>
        ''' Un record NUEVO en el parche (<c>Add(grupo, SIG, True)</c>): FormID provisional (byte alto 0xFF, la ley del
        ''' escritor, <c>SaveNpcEspWriter.FormIdAltoDeBorrador</c>) que el escritor reemplaza por uno real al guardar; solo
        ''' los miembros Required del record, creados con la ley de xEdit (<see cref="XCreate.CreateMember"/>).
        ''' </summary>
        Public Function NewPatchRecord(sig As String) As XEditRecord
            Dim def = WbSchema.Get(WbGame.Fallout4, sig)
            If def Is Nothing Then Throw New NotSupportedException("Record type " & sig & " is not declared in the schema.")
            _nextProvisional += 1UI
            Dim fid = SaveNpcEspWriter.FormIdAltoDeBorrador Or _nextProvisional
            Dim ctx As New WbContext(WbGame.Fallout4) With {.RecordSignature = sig, .FormID = fid, .RecordFlags = 0UI,
                                                             .Localized = False, .DestinoLocalizado = False}
            Dim root As New WbNode(New WbRootDef(sig))
            Dim rec As New XEditRecord(Me, sig, fid, root, ctx, _OutputFileName, _CutIndex)
            For Each m In def.Members
                If m.Required Then root.AddChild(XCreate.CreateMember(rec, m))
            Next
            AddPatchRecord(rec)
            Return rec
        End Function

        ''' <summary>Saca un record del parche (el <c>Remove(rec)</c> del original).</summary>
        Public Sub RemovePatchRecord(fid As UInteger)
            Dim r As XEditRecord = Nothing
            If _patch.TryGetValue(fid, r) Then
                _patch.Remove(fid)
                _patchOrder.Remove(r)
            End If
        End Sub

        ''' <summary>Cambia el FormID de un record del parche (provisional → definitivo).</summary>
        Friend Sub RekeyPatchRecord(oldFid As UInteger, newFid As UInteger)
            Dim r As XEditRecord = Nothing
            If Not _patch.TryGetValue(oldFid, r) Then Return
            _patch.Remove(oldFid)
            r.FormID = newFid
            r.Context.FormID = newFid
            _patch(newFid) = r
        End Sub

        ''' <summary>Todas las versiones, con la del parche en su lugar del orden de carga.</summary>
        Public Function AllVersions(fid As UInteger) As List(Of XEditRecord)
            Dim out = New List(Of XEditRecord)(Chain(fid))
            Dim p = PatchRecord(fid)
            If p IsNot Nothing Then
                Dim i = out.FindIndex(Function(x) x.LoadOrderIndex > _CutIndex)
                If i < 0 Then out.Add(p) Else out.Insert(i, p)
            End If
            Return out
        End Function

        ''' <summary><c>MasterOrSelf</c>: la version del archivo que DEFINE el record (la primera de la cadena), o
        ''' la del parche si el record es nuevo.</summary>
        Public Function MasterOrSelf(fid As UInteger) As XEditRecord
            Dim c = Chain(fid)
            If c.Count > 0 Then Return c(0)
            Return PatchRecord(fid)
        End Function

        ''' <summary><c>WinningOverride</c>: la ultima version en el orden de carga, el parche incluido.</summary>
        Public Function WinningOverride(fid As UInteger) As XEditRecord
            Dim all = AllVersions(fid)
            If all.Count = 0 Then Return Nothing
            Return all(all.Count - 1)
        End Function

        ''' <summary>La ultima version EN DISCO anterior a <paramref name="beforeIndex"/> en el orden de carga.</summary>
        Public Function WinningOverrideBefore(fid As UInteger, beforeIndex As Integer) As XEditRecord
            Dim best As XEditRecord = Nothing
            For Each r In Chain(fid)
                If r.LoadOrderIndex >= beforeIndex Then Exit For
                best = r
            Next
            Return best
        End Function

        ''' <summary>
        ''' Record "partial form": bit 0x4000 de la cabecera, SOLO en los tipos cuya definicion lo declara (en
        ''' Fallout 4: CELL, DIAL, QUST, WRLD; <c>IsPartialForm</c> exige la bandera en la definicion,
        ''' wbI:12029-12032; wbDefinitionsFO4.pas).
        ''' </summary>
        Public Shared Function IsPartialForm(r As XEditRecord) As Boolean
            Select Case r.Signature
                Case "CELL", "DIAL", "QUST", "WRLD"
                    Return (r.RecordFlags And &H4000UI) <> 0UI
                Case Else
                    Return False
            End Select
        End Function

        ' ======================================================================================== visibilidad

        ''' <summary>
        ''' Los archivos VISIBLES desde un archivo: el mismo y sus masters (transitivos). Es contra lo que xEdit
        ''' resuelve una referencia (<c>TwbFile.GetRecordByFormID</c> → <c>HighestOverrideVisibleForFile</c>,
        ''' wbI:4881-4900). Para el parche, sus masters son los que se le fueron agregando.
        ''' </summary>
        Public Function VisibleFiles(fileName As String) As HashSet(Of String)
            If IsPatchFile(fileName) Then
                Return New HashSet(Of String)(PatchMasters(fileName), StringComparer.OrdinalIgnoreCase) From {fileName}
            End If
            Dim hit As HashSet(Of String) = Nothing
            If _visibleCache.TryGetValue(fileName, hit) Then Return hit
            Dim res As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim pila As New Stack(Of String)
            pila.Push(fileName)
            While pila.Count > 0
                Dim n = pila.Pop()
                If Not res.Add(n) Then Continue While
                Dim r = Reader(n)
                If r IsNot Nothing AndAlso r.Masters IsNot Nothing Then
                    For Each m In r.Masters
                        pila.Push(m)
                    Next
                End If
            End While
            _visibleCache(fileName) = res
            Return res
        End Function

        ''' <summary>Registra un archivo de salida adicional (<c>-partN</c>); el principal ya lo es.</summary>
        Public Sub AddPatchPart(fileName As String)
            If Not _patchMasters.ContainsKey(fileName) Then _patchMasters(fileName) = New List(Of String)
        End Sub

        ''' <summary>True si el archivo es una salida (el principal o una parte).</summary>
        Public Function IsPatchFile(fileName As String) As Boolean
            Return String.Equals(fileName, _OutputFileName, StringComparison.OrdinalIgnoreCase) OrElse _patchMasters.ContainsKey(fileName)
        End Function

        ''' <summary>Masters que un archivo de salida tiene hasta ahora (en el orden en que se agregaron). Sin argumento, el
        ''' principal.</summary>
        Public Function PatchMasters(Optional patchFile As String = Nothing) As IReadOnlyList(Of String)
            Dim l As List(Of String) = Nothing
            If _patchMasters.TryGetValue(If(patchFile, _OutputFileName), l) Then Return l
            Return Array.Empty(Of String)()
        End Function

        ''' <summary><c>AddMasterIfMissing</c> sobre un archivo de salida (sin argumento, el principal): agranda lo que
        ''' ese archivo ve.</summary>
        Public Sub AddPatchMaster(fileName As String, Optional patchFile As String = Nothing)
            Dim destino = If(patchFile, _OutputFileName)
            If String.Equals(fileName, destino, StringComparison.OrdinalIgnoreCase) Then Return
            Dim l As List(Of String) = Nothing
            If Not _patchMasters.TryGetValue(destino, l) Then
                l = New List(Of String)
                _patchMasters(destino) = l
            End If
            If l.Contains(fileName, StringComparer.OrdinalIgnoreCase) Then Return
            l.Add(fileName)
        End Sub

        ''' <summary>
        ''' La version de <paramref name="fid"/> que ve un record del archivo <paramref name="fromFile"/>: la mas
        ''' alta en el orden de carga entre los archivos visibles desde ese (wbI:4881-4900). Es lo que devuelve
        ''' <c>LinksTo</c> y de donde sale el EDID/FULL del texto de una referencia.
        ''' </summary>
        Public Function RecordVisibleFrom(fid As UInteger, fromFile As String) As XEditRecord
            Dim vis = VisibleFiles(fromFile)
            Dim best As XEditRecord = Nothing
            For Each r In AllVersions(fid)
                If vis.Contains(r.FileName) Then best = r
            Next
            Return best
        End Function

        ''' <summary>Archivo dueño del FormID (el que lo define), o '' si ninguno de los cargados.</summary>
        Public Function OwnerFileName(fid As UInteger) As String
            Dim m = MasterOrSelf(fid)
            If m IsNot Nothing Then Return m.FileName
            Return If(Plugins.GetOriginatingPluginName(fid), "")
        End Function

        ' ======================================================================================== creado vacio

        ''' <summary>Contenedores recien creados con su elemento de relleno (<c>csAsCreatedEmpty</c>): el primer
        ''' <c>ElementAssign</c> reusa ese elemento en vez de agregar otro (wbI:15358-15368, 20714-20719).</summary>
        Private ReadOnly _createdEmpty As New HashSet(Of WbNode)(ReferenceEqualityComparer.Instance)

        Friend Sub MarkCreatedEmpty(n As WbNode)
            _createdEmpty.Add(n)
        End Sub

        Friend Function IsCreatedEmpty(n As WbNode) As Boolean
            Return _createdEmpty.Contains(n)
        End Function

        Friend Sub ClearCreatedEmpty(n As WbNode)
            _createdEmpty.Remove(n)
        End Sub

        ''' <summary>Como <see cref="ResolveText"/>, pero False si el identificador no esta en la tabla del idioma.</summary>
        Public Function TryResolveText(rec As XEditRecord, node As WbNode, ByRef text As String) As Boolean
            text = ""
            If node Is Nothing Then Return True
            If TypeOf node.Value Is String Then text = CStr(node.Value) : Return True
            If rec.Source Is Nothing Then Return False
            Return New CanonResolver(rec.Source, Plugins).TryText(node, text)
        End Function

        ''' <summary>Texto de una hoja localizable de una version de disco (id → tabla del idioma).</summary>
        Public Function ResolveText(rec As XEditRecord, node As WbNode) As String
            If node Is Nothing Then Return ""
            If TypeOf node.Value Is String Then Return CStr(node.Value)
            If rec.Source Is Nothing Then Return ""
            Return New CanonResolver(rec.Source, Plugins).Text(node)
        End Function

    End Class

End Namespace
