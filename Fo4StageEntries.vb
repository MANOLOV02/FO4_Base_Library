Option Strict On
Option Explicit On

' THE ENTRIES OF THE TRANSCRIBED MODULES' CLOSED LISTS (Ola SSE S1, fase GZ, rev-21). Before GZ each list was a Dictionary of value
' tuples whose Fn / CpuFn / InDefines held the stage text, read from the whole block when the module was initialised. Each entry is
' now one of these structures: the same members with the same names and types (String, Integer, Boolean, Fo4CpuRouteLayout), read
' the same way (.Fn, .Record, ...), and the stage texts are PROPERTIES over GlslText - the class of the module's GlslBlock read when the
' text is first asked for (GlslBlockStore). The generators write the tables (common\gen_block_vb.py, gen_block_vbs.py,
' fo4-effect\gen_effect_vb.py, fo4-gbuffer\gen_fo4_profile_v6.py). FnKey / CpuFnKey / InDefinesKey name the class (ParityGate
' glsl-blockstore compares each text with its class); WithFn / WithGap give a gate's mutated entry (a literal text), as the gates
' rebuilt a tuple before. GlslPixelStage and GlslVertexStage are the shapes the transcribed blocks of both games share (user decision,
' fase GZ rev-10: Fallout 4's b06 / b07 / b10 / b11 / image space / b05 VS now, Skyrim SE's g6 / g7 in S1); the Fo4* ones are shapes only
' Fallout 4's G-buffer / effect blocks have.

''' <summary>A PS of b06 / b07 / b10 / b11 / image space: (record, its class block or empty for a declared gap, the resource blocks it
''' declares, the app entry of its output shape, why the preview cannot draw it - empty when it can).</summary>
Friend Structure GlslPixelStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly Resources As String
    Friend ReadOnly Entry As String
    Friend ReadOnly Gap As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, resources As String, entry As String, gap As String)
        Me.New(record, GlslText.OfClass(block, fnKey), resources, entry, gap)
    End Sub

    Private Sub New(record As Integer, fn As GlslText, resources As String, entry As String, gap As String)
        Me.Record = record : _fn = fn : Me.Resources = resources : Me.Entry = entry : Me.Gap = gap
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property

    ''' <summary>GATE: this entry with the class text <paramref name="text"/>.</summary>
    Friend Function WithFn(text As String) As GlslPixelStage
        Return New GlslPixelStage(Record, GlslText.OfLiteral(text), Resources, Entry, Gap)
    End Function
End Structure

''' <summary>A PS of b05 (BSEffect): (record, its class block, why the preview cannot draw it - a runtime texture it samples - empty when
''' it can).</summary>
Friend Structure Fo4EffectPixelStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly Gap As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, gap As String)
        Me.New(record, GlslText.OfClass(block, fnKey), gap)
    End Sub

    Private Sub New(record As Integer, fn As GlslText, gap As String)
        Me.Record = record : _fn = fn : Me.Gap = gap
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property

    ''' <summary>GATE: this entry with the class text <paramref name="text"/>.</summary>
    Friend Function WithFn(text As String) As Fo4EffectPixelStage
        Return New Fo4EffectPixelStage(Record, GlslText.OfLiteral(text), Gap)
    End Function

    ''' <summary>GATE: this entry with the gap <paramref name="value"/>.</summary>
    Friend Function WithGap(value As String) As Fo4EffectPixelStage
        Return New Fo4EffectPixelStage(Record, _fn, value)
    End Function
End Structure

''' <summary>A2-N (Ola SSE S1 F2): why a VS entry has no CPU-route text - the table's structure, not a missing transcription. RigidVs: the
''' VS has no skin input (no BLENDINDICES: stage_text.skin_rule); NoPalette: a skinned VS that reads no palette (block_source NO_PALETTE:
''' nothing to skin). None: the VS has its CPU route - a skinned VS with an empty CpuFn is a failed route (ParityGate glsl-blockstore
''' FAILs on it).</summary>
Friend Enum Fo4CpuRouteAbsence
    None = 0
    RigidVs = 1
    NoPalette = 2
End Enum

''' <summary>A VS of b05 / b06 / b07 / b10 / b11 / image space / Skyrim SE g6 / g7: (record, its class block, the resource blocks, whether
''' rule D2 put AppSkinLaw in it; for a skinned VS its CPU route - block, resources, AppSkinLaw, layout -; why it has none; why the
''' preview cannot draw it - empty when it can).</summary>
Friend Structure GlslVertexStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly Resources As String
    Friend ReadOnly SkinLaw As Boolean
    Private ReadOnly _cpuFn As GlslText
    Friend ReadOnly CpuResources As String
    Friend ReadOnly CpuSkinLaw As Boolean
    Friend ReadOnly CpuLayout As Fo4CpuRouteLayout
    Friend ReadOnly CpuAbsence As Fo4CpuRouteAbsence
    Friend ReadOnly Gap As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, resources As String, skinLaw As Boolean, cpuFnKey As String,
                   cpuResources As String, cpuSkinLaw As Boolean, cpuLayout As Fo4CpuRouteLayout, cpuAbsence As Fo4CpuRouteAbsence, previewReason As String)
        Me.New(record, GlslText.OfClass(block, fnKey), resources, skinLaw, GlslText.OfClass(block, cpuFnKey), cpuResources, cpuSkinLaw, cpuLayout, cpuAbsence, previewReason)
    End Sub

    Private Sub New(record As Integer, fn As GlslText, resources As String, skinLaw As Boolean, cpuFn As GlslText, cpuResources As String,
                    cpuSkinLaw As Boolean, cpuLayout As Fo4CpuRouteLayout, cpuAbsence As Fo4CpuRouteAbsence, previewReason As String)
        Me.Record = record : _fn = fn : Me.Resources = resources : Me.SkinLaw = skinLaw : _cpuFn = cpuFn
        Me.CpuResources = cpuResources : Me.CpuSkinLaw = cpuSkinLaw : Me.CpuLayout = cpuLayout : Me.CpuAbsence = cpuAbsence : Me.Gap = previewReason
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
    Friend ReadOnly Property CpuFn As String
        Get
            Return _cpuFn.Value
        End Get
    End Property
    Friend ReadOnly Property CpuFnKey As String
        Get
            Return _cpuFn.Key
        End Get
    End Property

    ''' <summary>GATE: this entry with the class text <paramref name="text"/> (the GPU route's).</summary>
    Friend Function WithFn(text As String) As GlslVertexStage
        Return New GlslVertexStage(Record, GlslText.OfLiteral(text), Resources, SkinLaw, _cpuFn, CpuResources, CpuSkinLaw, CpuLayout, CpuAbsence, Gap)
    End Function
End Structure

''' <summary>A b09 DFPrePass PS record: (record, class function text, function name, forceEarlyDepthStencil, the resource blocks it
''' declares, the preview gap - empty when it can draw it -, the record's FO4_GBUF_IN defines).</summary>
Friend Structure Fo4GBufferRecord
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly FnName As String
    Friend ReadOnly EarlyZ As Boolean
    Friend ReadOnly Resources As String
    Friend ReadOnly Gap As String
    Private ReadOnly _inDefines As GlslText

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, fnName As String, earlyZ As Boolean, resources As String,
                   gap As String, inDefinesKey As String)
        Me.New(record, GlslText.OfClass(block, fnKey), fnName, earlyZ, resources, gap, GlslText.OfClass(block, inDefinesKey))
    End Sub

    Private Sub New(record As Integer, fn As GlslText, fnName As String, earlyZ As Boolean, resources As String, gap As String, inDefines As GlslText)
        Me.Record = record : _fn = fn : Me.FnName = fnName : Me.EarlyZ = earlyZ : Me.Resources = resources : Me.Gap = gap : _inDefines = inDefines
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
    Friend ReadOnly Property InDefines As String
        Get
            Return _inDefines.Value
        End Get
    End Property
    Friend ReadOnly Property InDefinesKey As String
        Get
            Return _inDefines.Key
        End Get
    End Property

    ''' <summary>GATE: this entry with the class text <paramref name="text"/>.</summary>
    Friend Function WithFn(text As String) As Fo4GBufferRecord
        Return New Fo4GBufferRecord(Record, GlslText.OfLiteral(text), FnName, EarlyZ, Resources, Gap, _inDefines)
    End Function

    ''' <summary>GATE: this entry with the gap <paramref name="value"/>.</summary>
    Friend Function WithGap(value As String) As Fo4GBufferRecord
        Return New Fo4GBufferRecord(Record, _fn, FnName, EarlyZ, Resources, value, _inDefines)
    End Function
End Structure

''' <summary>A b09 VS: (record, its class block, the resource blocks it declares, whether rule D2 put AppSkinLaw in it, why it is not
''' transcribed - empty when it is; for a skinned VS its CPU route: block, resources, AppSkinLaw, layout).</summary>
Friend Structure Fo4GBufferVertexStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly Resources As String
    Friend ReadOnly SkinLaw As Boolean
    Friend ReadOnly NotTranscribed As String
    Private ReadOnly _cpuFn As GlslText
    Friend ReadOnly CpuResources As String
    Friend ReadOnly CpuSkinLaw As Boolean
    Friend ReadOnly CpuLayout As Fo4CpuRouteLayout
    Friend ReadOnly CpuAbsence As Fo4CpuRouteAbsence

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, resources As String, skinLaw As Boolean, notTranscribed As String,
                   cpuFnKey As String, cpuResources As String, cpuSkinLaw As Boolean, cpuLayout As Fo4CpuRouteLayout, cpuAbsence As Fo4CpuRouteAbsence)
        Me.Record = record : _fn = GlslText.OfClass(block, fnKey) : Me.Resources = resources : Me.SkinLaw = skinLaw
        Me.NotTranscribed = notTranscribed : _cpuFn = GlslText.OfClass(block, cpuFnKey) : Me.CpuResources = cpuResources
        Me.CpuSkinLaw = cpuSkinLaw : Me.CpuLayout = cpuLayout : Me.CpuAbsence = cpuAbsence
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
    Friend ReadOnly Property CpuFn As String
        Get
            Return _cpuFn.Value
        End Get
    End Property
    Friend ReadOnly Property CpuFnKey As String
        Get
            Return _cpuFn.Key
        End Get
    End Property
End Structure

''' <summary>A b09 HS: (record, its class block, why it is not transcribed - empty when it is).</summary>
Friend Structure Fo4HullStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly NotTranscribed As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, notTranscribed As String)
        Me.Record = record : _fn = GlslText.OfClass(block, fnKey) : Me.NotTranscribed = notTranscribed
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
End Structure

''' <summary>A b09 DS: (record, its class block, the resource blocks it declares, why it is not transcribed - empty when it is).</summary>
Friend Structure Fo4DomainStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly Resources As String
    Friend ReadOnly NotTranscribed As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, resources As String, notTranscribed As String)
        Me.Record = record : _fn = GlslText.OfClass(block, fnKey) : Me.Resources = resources : Me.NotTranscribed = notTranscribed
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
End Structure

''' <summary>A BSEffect decal PS drawn into the G-buffer: (record, function text, its name).</summary>
Friend Structure Fo4EffectDecalStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly FnName As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, fnName As String)
        Me.Record = record : _fn = GlslText.OfClass(block, fnKey) : Me.FnName = fnName
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
End Structure

''' <summary>An image-space CS: (record, its class block or empty for a declared gap, the thread group, the UAV slots, the gap).</summary>
Friend Structure Fo4ComputeStage
    Friend ReadOnly Record As Integer
    Private ReadOnly _fn As GlslText
    Friend ReadOnly GroupX As Integer
    Friend ReadOnly GroupY As Integer
    Friend ReadOnly GroupZ As Integer
    Friend ReadOnly Uavs As String
    Friend ReadOnly Gap As String

    Friend Sub New(block As GlslBlock, record As Integer, fnKey As String, groupX As Integer, groupY As Integer, groupZ As Integer,
                   uavs As String, gap As String)
        Me.Record = record : _fn = GlslText.OfClass(block, fnKey) : Me.GroupX = groupX : Me.GroupY = groupY : Me.GroupZ = groupZ
        Me.Uavs = uavs : Me.Gap = gap
    End Sub

    Friend ReadOnly Property Fn As String
        Get
            Return _fn.Value
        End Get
    End Property
    Friend ReadOnly Property FnKey As String
        Get
            Return _fn.Key
        End Get
    End Property
End Structure
