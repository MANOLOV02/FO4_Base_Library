Option Strict On
Option Explicit On
Imports System.Collections.Concurrent
Imports System.IO
Imports System.IO.Compression
Imports System.Security.Cryptography
Imports System.Text

''' <summary>ONE BLOCK OF GENERATED STAGE TEXTS (Ola SSE S1, fase GZ; user decision Q1: the generated GLSL is stored compressed). The
''' format is Tools\ShaderTranscription\common\glslgz.py (its writer, reader and check G0): <c>&lt;block&gt;.glsl.gz</c> = one GZip member
''' per class (RFC 1952 2.2), each member the class's text with its BEGIN / END lines; <c>&lt;block&gt;.glsl.idx</c> = ASCII, LF, the
''' header <c>GLSLGZ 1 &lt;block&gt; &lt;classes&gt; &lt;sha256 of the text&gt;</c> and one line per class: key, part file, offset,
''' compressed length, text length, sha256 of the member's text.
''' <para>Text(key) reads the index once and then ONLY that class's member: Seek to its offset, a stream bounded to its compressed
''' length inside GZipStream, the text into a ConcurrentDictionary (thread-safe like the Lazy of the 7 readers it replaces; a class is
''' read once). Before GZ every block was read whole into a Dictionary at the first use (the G-buffer's records called it in their
''' static initialisation): the whole text in the heap twice (UTF-16 and the Dictionary). In Debug every read is checked against the
''' index (length, sha256, its BEGIN / END lines, the block the index names): closed data, checked by the gate (D-19), not in Release.</para>
''' <para>The bytes come from an opener (part or index file name -&gt; stream): the library's embedded resources (GlslBlockStore) or a
''' folder (a gate's snapshot of a block, InFolder).</para></summary>
Friend NotInheritable Class GlslBlock

    ''' <summary>The first word of an index (glslgz.MAGIC).</summary>
    Friend Const Magic As String = "GLSLGZ"
    ''' <summary>The format's version (glslgz.VERSION): an index of another version is refused.</summary>
    Friend Const FormatVersion As Integer = 1
    Friend Const PartExtension As String = ".glsl.gz"
    Friend Const IndexExtension As String = ".glsl.idx"

    ''' <summary>One line of the index: where class Key's member is and what it holds.</summary>
    Friend Structure IndexRow
        Friend Key As String
        Friend Part As String
        Friend Offset As Long
        Friend CompressedLength As Integer
        Friend TextLength As Integer
        Friend Sha256 As String
    End Structure

    ''' <summary>The index read: the header and the rows in order (and by key).</summary>
    Friend NotInheritable Class IndexData
        Friend Block As String
        Friend Version As Integer
        Friend TextSha256 As String
        Friend Rows As IndexRow()
        Friend ByKey As Dictionary(Of String, Integer)
    End Class

    ''' <summary>The block's name (e.g. Fo4GBufferBlocks).</summary>
    Friend ReadOnly Name As String
    Private ReadOnly _open As Func(Of String, Stream)
    Private ReadOnly _index As Lazy(Of IndexData)
    Private ReadOnly _texts As New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

    ''' <param name="open">Opens a file of the block by name (<c>&lt;block&gt;.glsl.idx</c>, a part): a readable, seekable stream, or
    ''' Nothing when the file does not exist.</param>
    Friend Sub New(name As String, open As Func(Of String, Stream))
        Me.Name = name
        _open = open
        _index = New Lazy(Of IndexData)(AddressOf ReadIndex, Threading.LazyThreadSafetyMode.ExecutionAndPublication)
    End Sub

    ''' <summary>Block <paramref name="name"/> in a folder (<c>&lt;folder&gt;\&lt;block&gt;.glsl.idx</c> and its parts).</summary>
    Friend Shared Function InFolder(folder As String, name As String) As GlslBlock
        Return New GlslBlock(name, Function(file)
                                       Dim p = Path.Combine(folder, file)
                                       Return If(IO.File.Exists(p), New FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read), Nothing)
                                   End Function)
    End Function

    ''' <summary>The index (read once).</summary>
    Friend ReadOnly Property Index As IndexData
        Get
            Return CurrentIndex()
        End Get
    End Property

    ''' <summary>The classes' keys in the index's order.</summary>
    Friend Function Keys() As String()
        Return Array.ConvertAll(CurrentIndex().Rows, Function(r) r.Key)
    End Function

    Friend Function ContainsKey(key As String) As Boolean
        Return CurrentIndex().ByKey.ContainsKey(key)
    End Function

    ''' <summary>The text of class <paramref name="key"/> (without its BEGIN / END lines; LF line ends): its member alone is read and
    ''' decompressed, once.</summary>
    Friend Function Text(key As String) As String
#If DEBUG Then
        If GateMutantRead() Then Return ReadClass(key)
#End If
        Return _texts.GetOrAdd(key, AddressOf ReadClass)
    End Function

    ''' <summary>The index this read uses: the cached one (Lazy, read once).</summary>
    Private Function CurrentIndex() As IndexData
#If DEBUG Then
        If GateMutantRead() Then Return ReadIndex()
#End If
        Return _index.Value
    End Function

#If DEBUG Then
    ''' <summary>GATE: a subject mutant of this reader is on (ParityGate glsl-blockstore). What is read then - the index and the texts -
    ''' bypasses both caches: a mutated read never outlives its mutant, whatever the order of the gate's passes and whatever the caches
    ''' held before (the mutant on with empty caches, then the base pass, is green: the gate's rule [isolation]).</summary>
    Private Shared Function GateMutantRead() As Boolean
        If Fo4GBufferConstants.GateMutantOn("glsl-blockstore-cache-keeps-mutant") Then Return False   ' GATE: the bypass gone ([isolation])
        Return Fo4GBufferConstants.GateMutantOn("glsl-blockstore-offset") OrElse Fo4GBufferConstants.GateMutantOn("glsl-blockstore-other-index")
    End Function
#End If

    Private Function OpenFile(file As String) As Stream
        Dim s = _open(file)
        If s Is Nothing Then Throw New FileNotFoundException($"GLSL block {Name}: {file} missing", file)
        Return s
    End Function

    Private Function ReadIndex() As IndexData
        Dim d = ParseIndex(Name & IndexExtension)
#If DEBUG Then
        ' Closed data (D-19): the index is this block's.
        If d.Block <> Name Then Throw New InvalidDataException($"{Name}{IndexExtension}: the index names block {d.Block}")
        ' GATE (ParityGate glsl-blockstore, subject mutant), AFTER the reader's own checks: the index of another embedded block takes
        ' this one's place, so only the gate's comparison of the block the index names ([index]) can see it.
        If Fo4GBufferConstants.GateMutantOn("glsl-blockstore-other-index") Then
            d = ParseIndex(GetType(GlslBlock).Assembly.GetManifestResourceNames().
                Where(Function(r) r.EndsWith(IndexExtension, StringComparison.Ordinal) AndAlso r <> "FO4_Base_Library." & Name & IndexExtension).
                OrderBy(Function(r) r, StringComparer.Ordinal).First().Substring("FO4_Base_Library.".Length))
        End If
#End If
        Return d
    End Function

    ''' <summary>An index file read into IndexData. The format checks (final line end, magic, version, count of classes and of columns)
    ''' are closed data: Debug only (D-19); ParityGate glsl-blockstore checks every embedded index ([index], [consistency]).</summary>
    Private Function ParseIndex(indexFile As String) As IndexData
        Dim lines As String()
        Using s = OpenFile(indexFile)
            Using ms As New MemoryStream()
                s.CopyTo(ms)
                lines = Encoding.ASCII.GetString(ms.GetBuffer(), 0, CInt(ms.Length)).Split(ChrW(10))
            End Using
        End Using
        Dim h = lines(0).Split(" "c)
#If DEBUG Then
        If lines.Length < 2 OrElse lines(lines.Length - 1) <> "" Then Throw New InvalidDataException($"{indexFile}: not a GLSLGZ index (no final line end)")
        If h.Length <> 5 OrElse h(0) <> Magic Then Throw New InvalidDataException($"{indexFile}:1: not a GLSLGZ header")
#End If
        Dim d As New IndexData With {.Block = h(2), .Version = Integer.Parse(h(1), Globalization.CultureInfo.InvariantCulture), .TextSha256 = h(4)}
        Dim n = Integer.Parse(h(3), Globalization.CultureInfo.InvariantCulture)
#If DEBUG Then
        If d.Version <> FormatVersion Then Throw New InvalidDataException($"{indexFile}: format version {d.Version}, this reader reads {FormatVersion}")
        If lines.Length - 2 <> n Then Throw New InvalidDataException($"{indexFile}: the header counts {n} classes, the index has {lines.Length - 2} lines")
#End If
        ReDim d.Rows(n - 1)
        d.ByKey = New Dictionary(Of String, Integer)(n, StringComparer.Ordinal)
        For i = 0 To n - 1
            Dim c = lines(i + 1).Split(ChrW(9))
#If DEBUG Then
            If c.Length <> 6 Then Throw New InvalidDataException($"{indexFile}:{i + 2}: {c.Length} columns, the format has 6")
#End If
            d.Rows(i) = New IndexRow With {.Key = c(0), .Part = c(1),
                .Offset = Long.Parse(c(2), Globalization.CultureInfo.InvariantCulture),
                .CompressedLength = Integer.Parse(c(3), Globalization.CultureInfo.InvariantCulture),
                .TextLength = Integer.Parse(c(4), Globalization.CultureInfo.InvariantCulture), .Sha256 = c(5)}
            d.ByKey.Add(c(0), i)
        Next
        Return d
    End Function

    ''' <summary>The bytes of the member of row <paramref name="r"/> decompressed: Seek to its offset, a stream bounded to its
    ''' compressed length inside GZipStream, exactly TextLength bytes read.</summary>
    Friend Function ReadMember(r As IndexRow) As Byte()
        Using s = OpenFile(r.Part)
            s.Seek(r.Offset, SeekOrigin.Begin)
            Using gz As New GZipStream(New BoundedReadStream(s, r.CompressedLength), CompressionMode.Decompress)
                Dim b(r.TextLength - 1) As Byte
                gz.ReadExactly(b, 0, b.Length)
#If DEBUG Then
                ' Closed data (D-19): the member holds exactly TextLength bytes.
                Dim extra(0) As Byte
                If gz.Read(extra, 0, 1) <> 0 Then Throw New InvalidDataException($"{Name}: class {r.Key}'s member holds more than {r.TextLength} bytes")
#End If
                Return b
            End Using
        End Using
    End Function

    Private Function ReadClass(key As String) As String
        Dim idx = CurrentIndex()
        Dim i As Integer
        If Not idx.ByKey.TryGetValue(key, i) Then Throw New KeyNotFoundException($"GLSL block {Name}: no class {key}")
        Dim b = ReadMember(idx.Rows(i))
        Dim head = "//#### BEGIN " & key & " ####" & vbLf, tail = "//#### END " & key & " ####" & vbLf
#If DEBUG Then
        ' Closed data (D-19): the member is the class the index says (its sha256 and its BEGIN / END lines).
        If Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant() <> idx.Rows(i).Sha256 Then
            Throw New InvalidDataException($"{Name}: class {key}: the sha256 of the text read differs from the index")
        End If
        Dim all = Encoding.ASCII.GetString(b)
        If Not all.StartsWith(head, StringComparison.Ordinal) OrElse Not all.EndsWith(tail, StringComparison.Ordinal) Then
            Throw New InvalidDataException($"{Name}: class {key}: the member read is not that class (its BEGIN / END lines)")
        End If
        ' GATE (ParityGate glsl-blockstore, subject mutant), AFTER the reader's own checks: the text returned is the member after the
        ' class's own (an offset run on to the next member), so only the gate's comparison with the whole text ([text]) can see it.
        If Fo4GBufferConstants.GateMutantOn("glsl-blockstore-offset") AndAlso i + 1 < idx.Rows.Length Then
            Dim nx = idx.Rows(i + 1)
            Dim nb = ReadMember(nx)
            Dim nh = "//#### BEGIN " & nx.Key & " ####" & vbLf, nt = "//#### END " & nx.Key & " ####" & vbLf
            Return Encoding.ASCII.GetString(nb, nh.Length, nb.Length - nh.Length - nt.Length)
        End If
#End If
        Return Encoding.ASCII.GetString(b, head.Length, b.Length - head.Length - tail.Length)
    End Function

    ''' <summary>A read-only view of the next <c>length</c> bytes of a stream (the member GZipStream decompresses): reads stop at its
    ''' end, so the decompressor never sees the next member.</summary>
    Private NotInheritable Class BoundedReadStream
        Inherits Stream
        Private ReadOnly _inner As Stream
        Private _left As Long

        Friend Sub New(inner As Stream, length As Long)
            _inner = inner
            _left = length
        End Sub

        Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
            If _left <= 0 Then Return 0
            Dim n = _inner.Read(buffer, offset, CInt(Math.Min(count, _left)))
            _left -= n
            Return n
        End Function

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return True
            End Get
        End Property
        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return False
            End Get
        End Property
        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return False
            End Get
        End Property
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property
        Public Overrides Sub Flush()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
    End Class
End Class

''' <summary>THE STORE OF THE LIBRARY'S GENERATED BLOCKS (fase GZ): one GlslBlock per block over the embedded resources
''' <c>FO4_Base_Library.&lt;block&gt;.glsl.idx</c> / <c>.glsl.gz</c> (FO4_Base_Library.vbproj embeds every *.glsl.gz / *.glsl.idx under
''' that name). The one reader of the generated stage texts: each transcribed module (Fo4GBufferSource, Fo4EffectSource,
''' Fo4LightingSource, Fo4UtilitySource, Fo4DFLightSource, Fo4DFCompositeSource, Fo4ImageSpaceSource) holds its Block from here. A
''' class of Shared members, not a Module: a Module's members are visible unqualified in the whole namespace, and Text would hide
''' System.Text.</summary>
Friend NotInheritable Class GlslBlockStore

    Private Sub New()
    End Sub

    Private Shared ReadOnly _blocks As New ConcurrentDictionary(Of String, GlslBlock)(StringComparer.Ordinal)

    ''' <summary>The embedded resource <paramref name="file"/> of the library (Nothing when it is not there).</summary>
    Friend Shared Function OpenResource(file As String) As Stream
        Return GetType(GlslBlock).Assembly.GetManifestResourceStream("FO4_Base_Library." & file)
    End Function

    ''' <summary>Embedded block <paramref name="name"/> (one instance per block: its index and its texts are read once).</summary>
    Friend Shared Function Embedded(name As String) As GlslBlock
        Return _blocks.GetOrAdd(name, Function(n) New GlslBlock(n, AddressOf OpenResource))
    End Function

    ''' <summary>The text of class <paramref name="key"/> of embedded block <paramref name="block"/>.</summary>
    Friend Shared Function Text(block As String, key As String) As String
        Return Embedded(block).Text(key)
    End Function
End Class

''' <summary>A stage text of a closed list (the Fn / CpuFn / InDefines of the transcribed modules' tables): a class of a GlslBlock read
''' when it is asked for (lazy: building the tables reads no text), or a literal text (a gate's mutant of a table entry, WithFn). The
''' value of a default entry (Nothing) is Nothing, of a declared gap (key "") the empty text: the values the tuples' fields had.</summary>
Friend Structure GlslText
    Private ReadOnly _block As GlslBlock
    Private ReadOnly _key As String
    Private ReadOnly _literal As String

    Private Sub New(block As GlslBlock, key As String, literal As String)
        _block = block
        _key = key
        _literal = literal
    End Sub

    ''' <summary>Class <paramref name="key"/> of <paramref name="block"/> ("" = a declared gap: the empty text, nothing read).</summary>
    Friend Shared Function OfClass(block As GlslBlock, key As String) As GlslText
        Return New GlslText(block, key, Nothing)
    End Function

    ''' <summary>A literal text (GATE: a mutated entry).</summary>
    Friend Shared Function OfLiteral(text As String) As GlslText
        Return New GlslText(Nothing, Nothing, text)
    End Function

    ''' <summary>The class's key (Nothing for a literal or a default entry).</summary>
    Friend ReadOnly Property Key As String
        Get
            Return _key
        End Get
    End Property

    Friend ReadOnly Property Value As String
        Get
            If _literal IsNot Nothing Then Return _literal
            If _key Is Nothing Then Return Nothing
            If _key.Length = 0 Then Return ""
#If DEBUG Then
            ' GATE (ParityGate glsl-blockstore, subject mutant): an entry's text is another class's (the block's first class).
            If Fo4GBufferConstants.GateMutantOn("glsl-blockstore-fn-other") Then
                Dim k = _block.Keys()
                Return _block.Text(If(k(0) = _key, k(1), k(0)))
            End If
#End If
            Return _block.Text(_key)
        End Get
    End Property
End Structure
