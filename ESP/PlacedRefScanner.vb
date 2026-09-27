Imports System.IO
Imports System.Text
Imports System.Threading

''' <summary>One placed reference as a plugin declares it (the WINNING version in the load order), with the FormIDs
''' already in load-order space. Only the fields a caller asked for are meaningful; everything else is its default.</summary>
Public Structure PlacedRef
    Public Signature As String
    Public FormID As UInteger
    Public RecordFlags As UInteger
    ''' <summary>EDID, or Nothing (most placed references have none).</summary>
    Public EditorID As String
    ''' <summary>NAME: the base object.</summary>
    Public BaseFormID As UInteger
    ''' <summary>WRLD of the enclosing world-children group; 0 for an interior cell's reference.</summary>
    Public WorldspaceFormID As UInteger
    ''' <summary>CELL of the enclosing cell-children group (for a world's persistent references: its persistent cell).</summary>
    Public CellFormID As UInteger
    ''' <summary>DATA: position and rotation (radians), as the record stores them.</summary>
    Public PosX As Single, PosY As Single, PosZ As Single
    Public RotX As Single, RotY As Single, RotZ As Single
    ''' <summary>XSCL as the record stores it (a float), or Nothing when absent.</summary>
    Public Scale As Single?
    ''' <summary>XLKR entries: (keyword, linked reference).</summary>
    Public LinkedRefs As List(Of (Keyword As UInteger, Ref As UInteger))
    ''' <summary>XPRM raw bytes, or Nothing.</summary>
    Public Primitive As Byte()
End Structure

''' <summary>Streams the placed references (REFR, ACHR and the other placed types) of the loaded plugins WITHOUT keeping
''' them: each winning, not-deleted reference is handed to a callback once and dropped.
''' <para>Winner law: a later plugin's version of a reference replaces the earlier ones. The plugins are walked in
''' REVERSE load order, so the first version met is the winner; the only state kept is the set of references already
''' decided that some later plugin overrode (a record whose origin is not the plugin being read), which is small.</para>
''' <para>Worlds and interior cells a caller does not need are skipped whole, by group, without reading them.</para></summary>
Public NotInheritable Class PlacedRefScanner

    ''' <summary>The placed-reference record types of Fallout 4: the list the FO4 schema gives for a placed reference
    ''' (Canon\Generated\WbSchemaGen_FO4.vb).</summary>
    Public Shared ReadOnly PlacedSignatures As New HashSet(Of String)(
        {"REFR", "ACHR", "PGRE", "PMIS", "PARW", "PBEA", "PFLA", "PCON", "PBAR", "PHZD"}, StringComparer.Ordinal)

    Private Const GroupWorldChildren As Integer = 1
    Private Const GroupInteriorBlock As Integer = 2
    Private Const GroupInteriorSubBlock As Integer = 3
    Private Const GroupCellChildren As Integer = 6
    Private Const GroupCellPersistent As Integer = 8
    Private Const GroupCellTemporary As Integer = 9

    ''' <summary>Progress of a scan: bytes read of all plugins, and the plugin being read.</summary>
    Public Structure Progress
        Public BytesDone As Long
        Public BytesTotal As Long
        Public Plugin As String
    End Structure

    ''' <summary>Walks every placed reference of <paramref name="pm"/>'s plugins (from <paramref name="dataPath"/>).
    ''' <paramref name="wantWorld"/> (world FormID) and <paramref name="wantInteriorCell"/> (cell FormID) decide which
    ''' groups are read at all (Nothing = all). <paramref name="onRef"/> gets each winning, not-deleted reference.</summary>
    Public Shared Sub Scan(pm As PluginManager, dataPath As String, onRef As Action(Of PlacedRef),
                           Optional wantWorld As Func(Of UInteger, Boolean) = Nothing,
                           Optional wantInteriorCell As Func(Of UInteger, Boolean) = Nothing,
                           Optional progress As Action(Of Progress) = Nothing,
                           Optional cancel As CancellationToken = Nothing)
        Dim plugins = pm.Plugins.Where(Function(p) p IsNot Nothing).Select(Function(p) p.FileName).ToList()
        Dim paths = plugins.Select(Function(f) Path.Combine(dataPath, f)).ToList()
        Dim total = paths.Sum(Function(p) New FileInfo(p).Length)
        Dim done As Long = 0
        Dim overridden As New HashSet(Of UInteger)
        For i = plugins.Count - 1 To 0 Step -1
            cancel.ThrowIfCancellationRequested()
            Dim state As New FileState With {
                .Pm = pm, .Plugin = plugins(i), .OnRef = onRef, .WantWorld = wantWorld, .WantInteriorCell = wantInteriorCell,
                .Overridden = overridden, .Cancel = cancel, .Progress = progress, .Before = done, .Total = total}
            Using fs As New FileStream(paths(i), FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan)
                Using br As New BinaryReader(fs, Encoding.UTF8, True)
                    Dim tes4 = RecordHeader.Read(br)
                    fs.Position += tes4.DataSize
                    While fs.Position < fs.Length
                        ReadGroup(br, fs, state, 0UI, 0UI, False)
                    End While
                    done += fs.Length
                End Using
            End Using
            progress?.Invoke(New Progress With {.BytesDone = done, .BytesTotal = total, .Plugin = plugins(i)})
        Next
    End Sub

    Private NotInheritable Class FileState
        Friend Pm As PluginManager
        Friend Plugin As String
        Friend OnRef As Action(Of PlacedRef)
        Friend WantWorld As Func(Of UInteger, Boolean)
        Friend WantInteriorCell As Func(Of UInteger, Boolean)
        Friend Overridden As HashSet(Of UInteger)
        Friend Cancel As CancellationToken
        Friend Progress As Action(Of Progress)
        Friend Before As Long
        Friend Total As Long
        Friend LastReport As Long

        ''' <summary>Reports at most once per 4 MB read (a UI refresh rate, not a law).</summary>
        Friend Sub Report(position As Long)
            If Progress Is Nothing OrElse position - LastReport < 4L * 1024 * 1024 Then Return
            LastReport = position
            Progress(New Progress With {.BytesDone = Before + position, .BytesTotal = Total, .Plugin = Plugin})
        End Sub
    End Class

    Private Shared Sub ReadGroup(br As BinaryReader, fs As Stream, st As FileState, world As UInteger, cell As UInteger, interior As Boolean)
        Dim start = fs.Position
        If fs.Length - start < GROUP_HEADER_SIZE Then fs.Position = fs.Length : Return
        Dim g = GroupHeader.Read(br)
        Dim groupEnd = Math.Min(start + g.GroupSize, fs.Length)
        If g.Signature <> "GRUP" Then fs.Position = fs.Length : Return

        Select Case g.GroupType
            Case 0
                ' Only CELL (interior cells) and WRLD hold placed references.
                Dim label = g.LabelAsSignature
                If label <> "CELL" AndAlso label <> "WRLD" Then fs.Position = groupEnd : Return
                interior = label = "CELL"
            Case GroupWorldChildren
                world = st.Pm.ResolveReferencedFormID(st.Plugin, g.Label)
                interior = False
                If st.WantWorld IsNot Nothing AndAlso Not st.WantWorld(world) Then fs.Position = groupEnd : Return
            Case GroupInteriorBlock, GroupInteriorSubBlock
                world = 0UI
                interior = True
            Case GroupCellChildren, GroupCellPersistent, GroupCellTemporary
                cell = st.Pm.ResolveReferencedFormID(st.Plugin, g.Label)
                If interior AndAlso st.WantInteriorCell IsNot Nothing AndAlso Not st.WantInteriorCell(cell) Then fs.Position = groupEnd : Return
        End Select

        While fs.Position < groupEnd - RECORD_HEADER_SIZE
            st.Report(fs.Position)
            Dim key = PluginSignatures.LeerClave(br)
            fs.Position -= 4
            If key = PluginSignatures.ClaveGRUP Then
                ReadGroup(br, fs, st, world, cell, interior)
            Else
                ReadRecord(br, fs, st, world, cell)
            End If
        End While
        fs.Position = groupEnd
    End Sub

    Private Shared Sub ReadRecord(br As BinaryReader, fs As Stream, st As FileState, world As UInteger, cell As UInteger)
        Dim h = RecordHeader.Read(br)
        Dim dataEnd = fs.Position + h.DataSize
        If Not PlacedSignatures.Contains(h.Signature) Then fs.Position = dataEnd : Return
        st.Cancel.ThrowIfCancellationRequested()

        Dim fid = st.Pm.ResolveReferencedFormID(st.Plugin, h.FormID)
        Dim isOverride = Not String.Equals(st.Pm.GetOriginatingPluginName(fid), st.Plugin, StringComparison.OrdinalIgnoreCase)
        If st.Overridden.Contains(fid) Then fs.Position = dataEnd : Return
        If isOverride Then st.Overridden.Add(fid)
        If h.IsDeleted Then fs.Position = dataEnd : Return

        Dim data = PluginReader.ReadRecordData(br, h)
        fs.Position = dataEnd
        Dim r As New PlacedRef With {.Signature = h.Signature, .FormID = fid, .RecordFlags = h.Flags,
                                     .WorldspaceFormID = world, .CellFormID = cell}
        For Each s In PluginReader.ParseSubrecords(data)
            Select Case s.Signature
                Case "EDID"
                    r.EditorID = s.AsStringGeneral
                Case "NAME"
                    If s.Data.Length >= 4 Then r.BaseFormID = st.Pm.ResolveReferencedFormID(st.Plugin, BitConverter.ToUInt32(s.Data, 0))
                Case "DATA"
                    If s.Data.Length >= 24 Then
                        r.PosX = BitConverter.ToSingle(s.Data, 0) : r.PosY = BitConverter.ToSingle(s.Data, 4) : r.PosZ = BitConverter.ToSingle(s.Data, 8)
                        r.RotX = BitConverter.ToSingle(s.Data, 12) : r.RotY = BitConverter.ToSingle(s.Data, 16) : r.RotZ = BitConverter.ToSingle(s.Data, 20)
                    End If
                Case "XSCL"
                    If s.Data.Length >= 4 Then r.Scale = BitConverter.ToSingle(s.Data, 0)
                Case "XLKR"
                    If s.Data.Length >= 8 Then
                        If r.LinkedRefs Is Nothing Then r.LinkedRefs = New List(Of (UInteger, UInteger))
                        r.LinkedRefs.Add((st.Pm.ResolveReferencedFormID(st.Plugin, BitConverter.ToUInt32(s.Data, 0)),
                                          st.Pm.ResolveReferencedFormID(st.Plugin, BitConverter.ToUInt32(s.Data, 4))))
                    End If
                Case "XPRM"
                    r.Primitive = s.Data
            End Select
        Next
        st.OnRef(r)
    End Sub

End Class
