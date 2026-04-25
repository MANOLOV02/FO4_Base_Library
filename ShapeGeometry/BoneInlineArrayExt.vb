Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports NiflySharp.Structs

''' <summary>
''' Consumer helpers for NiflySharp's [InlineArray(4)] bone structs from VB.NET.
''' VB cannot index InlineArray directly nor instantiate Span(Of T) via New, but
''' it CAN consume Spans returned by methods (MemoryMarshal.CreateSpan, AsSpan)
''' as long as they are never stored in a local. Each helper does one memcopy
''' between the struct and a slice of the caller's array - no intermediate buffer.
''' NiflySharp itself is not modified.
''' </summary>
Public Module BoneInlineArrayExt
    <Extension>
    Public Sub CopyTo(ByRef arr As BoneIndices4, destination As Byte(), destOffset As Integer, count As Integer)
        MemoryMarshal.CreateSpan(Unsafe.As(Of BoneIndices4, Byte)(arr), count).
            CopyTo(destination.AsSpan(destOffset, count))
    End Sub

    <Extension>
    Public Sub CopyFrom(ByRef arr As BoneIndices4, source As Byte(), srcOffset As Integer, count As Integer)
        source.AsSpan(srcOffset, count).
            CopyTo(MemoryMarshal.CreateSpan(Unsafe.As(Of BoneIndices4, Byte)(arr), count))
    End Sub

    <Extension>
    Public Sub CopyTo(ByRef arr As BoneWeights4, destination As Half(), destOffset As Integer, count As Integer)
        MemoryMarshal.CreateSpan(Unsafe.As(Of BoneWeights4, Half)(arr), count).
            CopyTo(destination.AsSpan(destOffset, count))
    End Sub

    <Extension>
    Public Sub CopyFrom(ByRef arr As BoneWeights4, source As Half(), srcOffset As Integer, count As Integer)
        source.AsSpan(srcOffset, count).
            CopyTo(MemoryMarshal.CreateSpan(Unsafe.As(Of BoneWeights4, Half)(arr), count))
    End Sub
End Module
