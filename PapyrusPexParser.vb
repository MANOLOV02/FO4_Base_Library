Imports System.Text

''' <summary>
''' Minimal, faithful parser for compiled Papyrus scripts (<c>.pex</c>) — Skyrim / Skyrim SE flavour.
'''
''' Purpose: recover the <c>Add{Warpaint,BodyPaint,HandPaint,FeetPaint,FacePaint}[Ex](name, path...)</c>
''' registrations that RaceMenu (skee64) accumulates from every mod's <c>On*PaintRequest</c> handler. RaceMenu
''' presents warpaints and body/hand/feet/face paints as named <c>name;;path</c> lists built AT RUNTIME from these
''' Papyrus calls (RaceMenuBase.psc inside RaceMenu.bsa: <c>AddWarpaint</c> stores <c>name &amp; ";;" &amp; path</c>,
''' then pushes the array to the Flash menu via <c>UI.InvokeStringA(..., "RSM_AddWarpaints", array)</c>). There is
''' no static catalog and no file browser: the offered list is the UNION of every installed script's registrations.
''' So to replicate RaceMenu's UI we must read the shipped scripts and extract the same string literals it does.
'''
''' We parse <c>.pex</c> (the compiled artifact the game actually loads) rather than <c>.psc</c> (optional source
''' that can be stale — measured: CommunityOverlays' .psc has 25 AddWarpaint calls its compiled .pex does not).
'''
''' Format notes (verified against RaceMenu.bsa scripts + a Python reference that matched .psc exactly, 87/87 and
''' 120/120): Skyrim <c>.pex</c> is BIG-ENDIAN (Fallout 4 <c>.pex</c> is little-endian). Magic <c>0xFA57C0DE</c>.
''' Layout: header → string table → debug info → user-flag table → objects{variables, properties,
''' states{functions{instructions}}}. Each instruction is <c>u8 opcode</c> + operands; the three call opcodes
''' (callmethod/callparent/callstatic) carry a variable argument list (an integer count operand followed by that
''' many operands). We walk the whole stream so a call's string arguments pair reliably.
''' </summary>
Public NotInheritable Class PapyrusPexParser

    Private Sub New()
    End Sub

    ''' <summary>One recovered paint registration. <see cref="Slots"/> is populated only for the <c>*Ex</c>
    ''' variants (8 texture-set slots); for the plain 2-arg variants it is Nothing and <see cref="Path"/> is the
    ''' single texture.</summary>
    Public Structure PaintRegistration
        ''' <summary>"warpaint" | "body" | "hand" | "feet" | "face".</summary>
        Public Category As String
        ''' <summary>Display name as the mod passed it (may be a "$translation_key"; RaceMenu localises it).</summary>
        Public Name As String
        ''' <summary>Primary texture path (slot 0), game-relative as the mod wrote it.</summary>
        Public Path As String
        ''' <summary>The 8 texture-set slots for an <c>*Ex</c> registration, else Nothing.</summary>
        Public Slots As String()
    End Structure

    ' --- Big-endian byte cursor over the .pex ---------------------------------------------------------------
    Private NotInheritable Class Cursor
        Private ReadOnly _d As Byte()
        Public Pos As Integer
        Public Sub New(d As Byte())
            _d = d
        End Sub
        Public Function U8() As Integer
            Dim v = _d(Pos) : Pos += 1 : Return v
        End Function
        Public Function U16() As Integer
            Dim v = (CInt(_d(Pos)) << 8) Or _d(Pos + 1) : Pos += 2 : Return v
        End Function
        Public Function U32() As UInteger
            Dim v = (CUInt(_d(Pos)) << 24) Or (CUInt(_d(Pos + 1)) << 16) Or (CUInt(_d(Pos + 2)) << 8) Or _d(Pos + 3)
            Pos += 4 : Return v
        End Function
        Public Function I32() As Integer
            ' Reinterpret the 32 bits as signed. CInt(UInteger) THROWS OverflowException for values ≥ 2^31
            ' (e.g. a Papyrus packed colour 0xAARRGGBB operand), which would abort the whole parse.
            Return BitConverter.ToInt32(BitConverter.GetBytes(U32()), 0)
        End Function
        Public Sub Skip(n As Integer)
            Pos += n
        End Sub
        Public Function WStr() As String
            Dim n = U16()
            Dim s = Encoding.GetEncoding("ISO-8859-1").GetString(_d, Pos, n)
            Pos += n
            Return s
        End Function
    End Class

    ' opcode -> (fixed operand count, has variadic tail). Table is the full Skyrim VM set 0x00..0x23.
    Private Shared ReadOnly OpFixed As Integer() = {
        0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3,
        1, 2, 2, 3, 2, 3, 1, 3, 3, 3, 2, 2, 3, 3, 4, 4}
    Private Shared ReadOnly OpVariadic As Boolean() = {
        False, False, False, False, False, False, False, False, False, False,
        False, False, False, False, False, False, False, False, False, False,
        False, False, False, True, True, True, False, False, False, False,
        False, False, False, False, False, False}

    Private Const OP_CALLMETHOD As Integer = &H17
    Private Const OP_CALLSTATIC As Integer = &H19
    ''' <summary>assign: operands [dest, src].</summary>
    Private Const OP_ASSIGN As Integer = &HD
    ''' <summary>cast: operands [dest, src].</summary>
    Private Const OP_CAST As Integer = &HE
    ''' <summary>propget: operands [propertyName, objectRef, dest].</summary>
    Private Const OP_PROPGET As Integer = &H1C
    ''' <summary>propset: operands [propertyName, objectRef, value].</summary>
    Private Const OP_PROPSET As Integer = &H1D

    ' A decoded operand. For our purpose we only need the resolved string (string literal OR identifier — the two
    ' are distinguished by <see cref="IsIdentifier"/> so a call arg that is a script VARIABLE can be resolved to
    ' that variable's initial string value) and the int value (for the variadic count operand).
    Private Structure Operand
        Public IsString As Boolean     ' string literal or identifier (both resolve to a string-table entry)
        Public IsIdentifier As Boolean ' True only for type-1 (a variable/property reference, not a literal)
        Public Str As String
        Public IsInt As Boolean
        Public IntVal As Integer
    End Structure

    Private Shared Function ReadOperand(c As Cursor, strings As String()) As Operand
        Dim t = c.U8()
        Dim o As New Operand()
        Select Case t
            Case 0 ' null
            Case 1 ' identifier (variable/property name)
                o.IsString = True : o.IsIdentifier = True : o.Str = strings(c.U16())
            Case 2 ' string literal
                o.IsString = True : o.Str = strings(c.U16())
            Case 3 ' integer
                o.IsInt = True : o.IntVal = c.I32()
            Case 4 ' float
                c.Skip(4)
            Case 5 ' bool
                c.Skip(1)
            Case Else
                Throw New FormatException($"bad operand type {t} at {c.Pos}")
        End Select
        Return o
    End Function

    ''' <summary>Read one function body and append any <c>callmethod</c> calls (method name + variadic string
    ''' args) to <paramref name="sink"/>.</summary>
    Private Shared Sub ReadFunction(c As Cursor, strings As String(), sink As List(Of (Method As String, Args As List(Of Operand))),
                                    Optional instrSink As List(Of (Op As Integer, Ops As List(Of Operand))) = Nothing)
        c.U16() ' return type
        c.U16() ' docstring
        c.U32() ' user flags
        c.U8()  ' flags
        Dim nparams = c.U16()
        For i = 1 To nparams : c.U16() : c.U16() : Next
        Dim nlocals = c.U16()
        For i = 1 To nlocals : c.U16() : c.U16() : Next
        Dim ninstr = c.U16()
        For i = 1 To ninstr
            Dim op = c.U8()
            If op >= OpFixed.Length Then Throw New FormatException($"bad opcode {op} at {c.Pos}")
            Dim fixedOperands As New List(Of Operand)(OpFixed(op))
            For k = 1 To OpFixed(op)
                fixedOperands.Add(ReadOperand(c, strings))
            Next
            Dim varArgs As New List(Of Operand)
            If OpVariadic(op) Then
                Dim cnt = ReadOperand(c, strings)
                Dim n = If(cnt.IsInt, cnt.IntVal, 0)
                For k = 1 To n
                    varArgs.Add(ReadOperand(c, strings))
                Next
            End If
            ' callmethod: fixed [methodName, self, dest] → method = operand 0. callstatic: fixed [scriptName,
            ' methodName, dest] → method = operand 1. Both carry the variadic arg list captured above.
            If op = OP_CALLMETHOD AndAlso fixedOperands.Count >= 1 AndAlso fixedOperands(0).IsString Then
                sink.Add((fixedOperands(0).Str, varArgs))
            ElseIf op = OP_CALLSTATIC AndAlso fixedOperands.Count >= 2 AndAlso fixedOperands(1).IsString Then
                sink.Add((fixedOperands(1).Str, varArgs))
            End If
            If instrSink IsNot Nothing Then instrSink.Add((op, fixedOperands))
        Next
    End Sub

    Private Shared Sub ReadProperty(c As Cursor, strings As String())
        c.U16() ' name
        c.U16() ' type
        c.U16() ' docstring
        c.U32() ' user flags
        Dim flags = c.U8()
        If (flags And &H4) <> 0 Then
            c.U16() ' auto var name
        Else
            If (flags And &H1) <> 0 Then ReadFunction(c, strings, New List(Of (Method As String, Args As List(Of Operand))))
            If (flags And &H2) <> 0 Then ReadFunction(c, strings, New List(Of (Method As String, Args As List(Of Operand))))
        End If
    End Sub

    ''' <summary>Walk a compiled <c>.pex</c> once and return every call it makes (method name + variadic args) plus
    ''' a map of script string-variable name → its initial string value (so a call arg that is a variable reference
    ''' can be resolved to the literal it holds — RaceMenu binds node names via <c>NINODE_*</c> string constants).
    ''' Returns (Nothing, Nothing) on a non-Papyrus / malformed script (never throws).</summary>
    Private Shared Function ParseScript(bytes As Byte()) As (Calls As List(Of (Method As String, Args As List(Of Operand))), StringVars As Dictionary(Of String, String), Strings As String(), Instrs As List(Of (Op As Integer, Ops As List(Of Operand))))
        If bytes Is Nothing OrElse bytes.Length < 16 Then Return (Nothing, Nothing, Nothing, Nothing)
        ' Skyrim .pex magic 0xFA57C0DE, big-endian. (FO4 .pex is little-endian and irrelevant here.)
        If Not (bytes(0) = &HFA AndAlso bytes(1) = &H57 AndAlso bytes(2) = &HC0 AndAlso bytes(3) = &HDE) Then Return (Nothing, Nothing, Nothing, Nothing)
        Try
            Dim c As New Cursor(bytes)
            c.U32()          ' magic
            c.U8() : c.U8()  ' major, minor
            c.U16()          ' game id
            c.Skip(8)        ' compilation time (u64)
            c.WStr() : c.WStr() : c.WStr()  ' src, user, machine
            Dim scount = c.U16()
            Dim strings(scount - 1) As String
            For i = 0 To scount - 1
                strings(i) = c.WStr()
            Next
            ' debug info
            Dim hasDebug = c.U8()
            If hasDebug <> 0 Then
                c.Skip(8)  ' modification time
                Dim fcount = c.U16()
                For i = 1 To fcount
                    c.U16() ' object name
                    c.U16() ' state name
                    c.U16() ' function name
                    c.U8()  ' function type
                    Dim ilc = c.U16()
                    c.Skip(ilc * 2) ' line numbers
                Next
            End If
            ' user flags
            Dim ufcount = c.U16()
            For i = 1 To ufcount
                c.U16() : c.U8()
            Next
            ' objects
            Dim calls As New List(Of (Method As String, Args As List(Of Operand)))
            Dim instrs As New List(Of (Op As Integer, Ops As List(Of Operand)))
            Dim stringVars As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Dim ocount = c.U16()
            For i = 1 To ocount
                c.U16()  ' object name index
                c.U32()  ' size
                c.U16()  ' parent class name
                c.U16()  ' docstring
                c.U32()  ' user flags
                c.U16()  ' auto state name
                Dim vcount = c.U16()
                For v = 1 To vcount
                    Dim vnameIdx = c.U16() : c.U16() : c.U32() ' name, type, user flags
                    Dim initVal = ReadOperand(c, strings) ' initial value
                    ' A string-literal initial value → remember it against the variable name for later resolution.
                    If initVal.IsString AndAlso Not initVal.IsIdentifier AndAlso vnameIdx >= 0 AndAlso vnameIdx < strings.Length Then
                        stringVars(strings(vnameIdx)) = initVal.Str
                    End If
                Next
                Dim pcount = c.U16()
                For p = 1 To pcount
                    ReadProperty(c, strings)
                Next
                Dim stcount = c.U16()
                For s = 1 To stcount
                    c.U16()  ' state name
                    Dim fcount2 = c.U16()
                    For f = 1 To fcount2
                        c.U16()  ' function name index
                        ReadFunction(c, strings, calls, instrs)
                    Next
                Next
            Next
            Return (calls, stringVars, strings, instrs)
        Catch
            ' A single malformed script must not break the whole catalog scan.
            Return (Nothing, Nothing, Nothing, Nothing)
        End Try
    End Function

    ''' <summary>Every <c>&lt;otherObject&gt;.TargetProp = SourceProp</c> assignment the script performs, as
    ''' (TargetProp → SourceProp).
    '''
    ''' This is what recovers the RaceCompatibility wiring: a race mod's controller does, in <c>OnInit</c>,
    ''' <c>raceController.NewNord = HeadPartsNord_DZ</c> — "my FormList &lt;HeadPartsNord_DZ&gt; occupies the vanilla
    ''' NORD slot". No record can express that; only the compiled script. What that source property HOLDS is in the
    ''' QUST's VMAD (<see cref="VmadPropertyReader"/>); the two together give the full binding.
    '''
    ''' Bytecode shape (verified against COtR's shipped .pex): an AUTO property is backed by a variable named
    ''' <c>::&lt;Name&gt;_var</c> and the compiler reads it DIRECTLY — there is no <c>propget</c> for it. The real
    ''' emission is
    '''     <c>assign ::temp0, ::HeadPartsNord_DZ_var</c> ; <c>propset NewNord, ::raceController_var, ::temp0</c>
    ''' and, for <c>= None</c>, <c>cast ::temp1, None</c> ; <c>assign ::temp0, ::temp1</c> ; <c>propset …</c>.
    ''' So we track temp → source-property through <c>assign</c>/<c>cast</c> (and <c>propget</c>, for non-auto
    ''' properties), resolving a backing variable to its property name. A temp assigned from something we cannot
    ''' resolve CLEARS its mapping — temps are reused across statements, and a stale entry would fabricate a
    ''' binding for a slot the script actually set to None. Returns an empty map on a malformed / non-Papyrus
    ''' script (never throws).</summary>
    Public Shared Function ExtractPropertyBindings(bytes As Byte()) As Dictionary(Of String, String)
        Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim parsed = ParseScript(bytes)
        If parsed.Instrs Is Nothing Then Return result

        ' temp/local variable name -> the property whose value it currently holds
        Dim tempSource As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        ' "::HeadPartsNord_DZ_var" -> "HeadPartsNord_DZ" (auto-property backing variable), or an already-tracked
        ' temp, else Nothing (unresolvable: a literal, None, a call result…).
        Dim resolve =
            Function(name As String) As String
                If String.IsNullOrEmpty(name) Then Return Nothing
                Dim viaTemp As String = Nothing
                If tempSource.TryGetValue(name, viaTemp) Then Return viaTemp
                If name.StartsWith("::", StringComparison.Ordinal) AndAlso name.EndsWith("_var", StringComparison.OrdinalIgnoreCase) Then
                    Return name.Substring(2, name.Length - 2 - 4)
                End If
                Return Nothing
            End Function

        For Each ins In parsed.Instrs
            Select Case ins.Op
                Case OP_ASSIGN, OP_CAST
                    If ins.Ops.Count >= 2 AndAlso ins.Ops(0).IsIdentifier Then
                        Dim src = If(ins.Ops(1).IsIdentifier, resolve(ins.Ops(1).Str), Nothing)
                        If src Is Nothing Then
                            tempSource.Remove(ins.Ops(0).Str)      ' temps are reused: never leave a stale mapping
                        Else
                            tempSource(ins.Ops(0).Str) = src
                        End If
                    End If
                Case OP_PROPGET
                    If ins.Ops.Count >= 3 AndAlso ins.Ops(0).IsString AndAlso ins.Ops(2).IsIdentifier Then
                        tempSource(ins.Ops(2).Str) = ins.Ops(0).Str
                    End If
                Case OP_PROPSET
                    If ins.Ops.Count >= 3 AndAlso ins.Ops(0).IsString AndAlso ins.Ops(2).IsIdentifier Then
                        Dim src = resolve(ins.Ops(2).Str)
                        If src IsNot Nothing Then result(ins.Ops(0).Str) = src
                    End If
            End Select
        Next
        Return result
    End Function

    ''' <summary>Extract every paint registration from a compiled <c>.pex</c>. Returns an empty list (never
    ''' throws) on a script that is malformed, not big-endian Papyrus, or simply carries no paint calls.</summary>
    Public Shared Function ExtractPaints(bytes As Byte()) As List(Of PaintRegistration)
        Dim result As New List(Of PaintRegistration)
        Dim parsed = ParseScript(bytes)
        If parsed.Calls Is Nothing Then Return result
        For Each callRec In parsed.Calls
            Dim cat = CategoryOf(callRec.Method)
            If cat Is Nothing Then Continue For
            Dim isEx = callRec.Method.EndsWith("Ex", StringComparison.OrdinalIgnoreCase)
            If callRec.Args.Count < 2 Then Continue For
            Dim nameOp = callRec.Args(0)
            If Not nameOp.IsString Then Continue For
            Dim reg As New PaintRegistration With {.Category = cat, .Name = nameOp.Str}
            If isEx Then
                Dim slots As New List(Of String)
                For a = 1 To callRec.Args.Count - 1
                    slots.Add(If(callRec.Args(a).IsString, callRec.Args(a).Str, ""))
                Next
                reg.Slots = slots.ToArray()
                reg.Path = If(slots.Count > 0, slots(0), "")
            Else
                Dim pathOp = callRec.Args(1)
                If Not pathOp.IsString Then Continue For
                reg.Path = pathOp.Str
            End If
            result.Add(reg)
        Next
        Return result
    End Function

    ''' <summary>Recover the skeleton NODE NAMES a compiled <c>.pex</c> uses with the NiOverride node-transform API.
    ''' This is the node-transform analogue of <see cref="ExtractPaints"/>, but the extraction differs because
    ''' RaceMenu does NOT pass node names as direct call-arg literals the way paints pass (name,path): it stores them
    ''' in runtime-filled arrays / <c>NINODE_*</c> constants, so a call-arg walk recovers nothing (measured: 0 on
    ''' RaceMenuPlugin.pex despite AddNodeTransformScale being called). Instead: if the script touches the
    ''' node-transform API at all (a call whose method mentions NodeTransform/NodeScale), scan its STRING TABLE for
    ''' entries that LOOK like skeleton node names — the literals are all there regardless of how the code wires them.
    ''' The caller intersects the result with the actor's real rig, so a non-node string that slips the heuristic is
    ''' harmless. Never throws.</summary>
    Public Shared Function ExtractNodeTransformNodeNames(bytes As Byte()) As List(Of String)
        Dim result As New List(Of String)
        Dim parsed = ParseScript(bytes)
        If parsed.Calls Is Nothing OrElse parsed.Strings Is Nothing Then Return result
        Dim touchesNodeApi = False
        For Each callRec In parsed.Calls
            If callRec.Method IsNot Nothing AndAlso
               (callRec.Method.IndexOf("NodeTransform", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                callRec.Method.IndexOf("NodeScale", StringComparison.OrdinalIgnoreCase) >= 0) Then
                touchesNodeApi = True : Exit For
            End If
        Next
        If Not touchesNodeApi Then Return result
        For Each s In parsed.Strings
            If LooksLikeNodeName(s) Then result.Add(s)
        Next
        Return result
    End Function

    ''' <summary>Heuristic: is <paramref name="s"/> plausibly a Skyrim skeleton node name? Matches the vanilla /
    ''' XPMSE conventions — <c>NPC …</c>, <c>CME …</c>, <c>NPC…[Tag]</c>, and the weapon/equip nodes RaceMenu scales.
    ''' Deliberately loose (the caller rig-filters); rejects obvious non-nodes (paths, dotted tokens).</summary>
    Private Shared Function LooksLikeNodeName(s As String) As Boolean
        If String.IsNullOrWhiteSpace(s) Then Return False
        Dim t = s.Trim()
        ' Reject paths/dotted tokens and ALL-CAPS flag-ish names with underscores (e.g. "WEAPONS_ENABLED").
        If t.Length > 64 OrElse t.IndexOf("/"c) >= 0 OrElse t.IndexOf("\"c) >= 0 OrElse t.IndexOf("."c) >= 0 OrElse t.IndexOf("_"c) >= 0 Then Return False
        If t.StartsWith("NPC ", StringComparison.OrdinalIgnoreCase) OrElse t.StartsWith("CME ", StringComparison.OrdinalIgnoreCase) OrElse
           t.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase) Then Return True
        Select Case t.ToUpperInvariant()
            Case "NPC", "WEAPON", "SHIELD", "QUIVER" : Return True
        End Select
        ' A bracketed bone tag like "…[Head]" / "…[Cam1]" is a strong signal.
        Return t.EndsWith("]") AndAlso t.IndexOf("["c) > 0
    End Function

    ''' <summary>DIAGNOSTIC: distinct method names this <c>.pex</c> calls, for probe/debug use only.</summary>
    Public Shared Function DebugCallMethodNames(bytes As Byte()) As List(Of String)
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim outp As New List(Of String)
        Dim parsed = ParseScript(bytes)
        If parsed.Calls Is Nothing Then Return outp
        For Each callRec In parsed.Calls
            If callRec.Method IsNot Nothing AndAlso seen.Add(callRec.Method) Then outp.Add(callRec.Method)
        Next
        outp.Sort(StringComparer.OrdinalIgnoreCase)
        Return outp
    End Function

    ''' <summary>Category for a paint-registration method name, or Nothing if it is not one.</summary>
    Private Shared Function CategoryOf(method As String) As String
        If String.IsNullOrEmpty(method) Then Return Nothing
        Dim m = method
        If m.EndsWith("Ex", StringComparison.OrdinalIgnoreCase) Then m = m.Substring(0, m.Length - 2)
        Select Case m.ToLowerInvariant()
            Case "addwarpaint" : Return "warpaint"
            Case "addbodypaint" : Return "body"
            Case "addhandpaint" : Return "hand"
            Case "addfeetpaint" : Return "feet"
            Case "addfacepaint" : Return "face"
        End Select
        Return Nothing
    End Function

End Class
