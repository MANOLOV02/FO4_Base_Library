Option Strict On
Option Explicit On

' =============================================================================
' HCL cloth SETUP / autoría (los hcl*SetupObject / *SetupMesh / *SetupContainer).
'
' Estos objetos viven en .hkx sueltos junto al NIF (p.ej. FemaleHair04.hkx ↔
' FemaleHair04.nif) y son la fuente de AUTORÍA que el compilador HCL convierte en
' el hclClothData runtime embebido. Contienen el BINDING que determina DÓNDE se
' coloca el cloth: a qué huesos se ata el mesh de simulación, el transform-set raíz,
' y el nombre del mesh — lo que hoy se resuelve con un parche (inyección de cloth
' bones desde BSClothExtraData del NIF, ver [[arch_cloth_bones_inject]]).
'
' DECODE SÓLIDO (no especulado): cada setup object es un nodo NOMBRADO. Se extrae:
'   - Strings: todos los strings referenciados por local-fixup (nombre + nombres de
'     mesh + nombres de hueso del MeshBoneDeform + anchors "Fixed/Chest/Neck/Head"…),
'     filtrados con IsLikelyNameToken para no colar punteros a datos de array.
'   - References: refs globales a otros setup objects, como (clase, nombre).
' Esto da el grafo de autoría completo y navegable, con TODO el binding de placement.
' NO se decodifican los params numéricos de simulación (stiffness/damping/gravity…):
' son tuning de sim, no placement, y sin SDK serían inferencias — fuera de alcance aquí.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Public Partial Class HkxObjectGraph_Class

    ''' <summary>Recolecta TODO el grafo de autoría HCL (clases hcl*…Setup…) como nodos planos
    ''' (nombre + strings + refs), más el linking de nivel-container. Devuelve Nothing si el .hkx
    ''' no tiene objetos de setup (es un cloth runtime puro o no-cloth).</summary>
    Public Function ParseClothSetup() As HclClothSetupGraph_Class
        Dim setupObjs = Objects.Where(Function(o) o.ClassName.StartsWith("hcl", StringComparison.OrdinalIgnoreCase) AndAlso
                                                  o.ClassName.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0).ToList()
        If setupObjs.Count = 0 Then Return Nothing

        Dim result As New HclClothSetupGraph_Class
        For Each o In setupObjs
            result.Nodes.Add(ParseSetupNode(o))
        Next

        ' Container de nivel superior: clothObjects@+0x10, meshes@+0x20, transformSets@+0x30 (arrays de refs).
        Dim container = setupObjs.FirstOrDefault(Function(o) o.ClassName.Equals("hclClothSetupContainer", StringComparison.OrdinalIgnoreCase))
        If Not IsNothing(container) Then
            result.Container = container
            result.ClothObjectNames.AddRange(ReadObjectReferenceArray(container.RelativeOffset + &H10).Select(AddressOf SetupNodeName))
            result.MeshNames.AddRange(ReadObjectReferenceArray(container.RelativeOffset + &H20).Select(AddressOf SetupNodeName))
            result.TransformSetNames.AddRange(ReadObjectReferenceArray(container.RelativeOffset + &H30).Select(AddressOf SetupNodeName))
        End If
        Return result
    End Function

    ''' <summary>Un setup object → nodo plano: ClassName + Strings (nombre + mesh/bone/anchor names,
    ''' por local-fixup filtrado) + References (refs globales como clase+nombre). Todo SÓLIDO.</summary>
    Public Function ParseSetupNode(source As HkxVirtualObjectGraph_Class) As HclSetupNode_Class
        Dim node As New HclSetupNode_Class With {.SourceObject = source, .ClassName = source.ClassName}
        If IsNothing(source) Then Return node

        For Each lf In GetLocalFixupsInRange(source.RelativeOffset, source.Size)
            Dim s = ReadNullTerminatedString(lf.DestinationRelativeOffset)
            If IsLikelyNameToken(s) AndAlso Not node.Strings.Contains(s) Then node.Strings.Add(s)
        Next

        Dim seenRefs As New HashSet(Of String)(StringComparer.Ordinal)
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim o = GetObject(gf.TargetRelativeOffset)
            If IsNothing(o) Then Continue For
            Dim refName = SetupNodeName(o)
            If seenRefs.Add(o.ClassName & "|" & refName) Then
                node.References.Add(New HclSetupRef_Class With {.ClassName = o.ClassName, .Name = refName})
            End If
        Next
        Return node
    End Function

    ' Nombre de un setup object = su primer string referenciado por local-fixup (filtrado).
    Private Function SetupNodeName(o As HkxVirtualObjectGraph_Class) As String
        If IsNothing(o) Then Return ""
        For Each lf In GetLocalFixupsInRange(o.RelativeOffset, o.Size)
            Dim s = ReadNullTerminatedString(lf.DestinationRelativeOffset)
            If IsLikelyNameToken(s) Then Return s
        Next
        Return ""
    End Function

End Class

' ====================== Result classes (cloth setup / autoría) ======================

Public Class HclClothSetupGraph_Class
    Public Property Container As HkxVirtualObjectGraph_Class
    Public ReadOnly Property Nodes As New List(Of HclSetupNode_Class)            ' TODOS los setup objects del .hkx
    Public ReadOnly Property ClothObjectNames As New List(Of String)             ' hclClothSetupObject del container
    Public ReadOnly Property MeshNames As New List(Of String)                    ' hclNamedSetupMesh del container
    Public ReadOnly Property TransformSetNames As New List(Of String)            ' hclNamedTransformSetSetupObject del container

    ''' <summary>Atajo: los nodos de un tipo de setup-class.</summary>
    Public Function NodesOfClass(className As String) As IEnumerable(Of HclSetupNode_Class)
        Return Nodes.Where(Function(n) n.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>Placement: los cloth-bones a los que el mesh de simulación se deforma
    ''' (strings del/los hclMeshBoneDeformSetupObject, saltando el nombre del nodo).</summary>
    Public Function ClothDeformBones() As List(Of String)
        Dim r As New List(Of String)
        For Each n In NodesOfClass("hclMeshBoneDeformSetupObject")
            ' Strings = [nombre("Deform"), bone0, bone1, …] → los huesos son del 2do en adelante.
            For Each s In n.Strings.Skip(1)
                If Not r.Contains(s) Then r.Add(s)
            Next
        Next
        Return r
    End Function
End Class

' Un setup object como nodo plano navegable.
Public Class HclSetupNode_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String = ""
    Public ReadOnly Property Strings As New List(Of String)        ' nombre + mesh/bone/anchor names (en orden; [0]=nombre)
    Public ReadOnly Property References As New List(Of HclSetupRef_Class)
    Public ReadOnly Property Name As String
        Get
            Return If(Strings.Count > 0, Strings(0), "")
        End Get
    End Property
End Class

Public Class HclSetupRef_Class
    Public Property ClassName As String = ""
    Public Property Name As String = ""
End Class
