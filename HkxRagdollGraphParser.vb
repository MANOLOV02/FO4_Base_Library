Option Strict On
Option Explicit On

' =============================================================================
' Ragdoll / rigid-body physics (último dominio HKX).
'
' FO4 (hknp = "new" Havok Physics 2014): hknpPhysicsSceneData → hknpRagdollData
'   (contiene skeleton + N rigid bodies con nombre + sus hknp*Shape + los
'   hkp*ConstraintData que los unen).
' Skyrim/BodySlide (hkp clásico): hkaRagdollInstance (rigidBodies + constraints +
'   boneToRigidBodyMap + skeleton) — layout AUTORITATIVO de HavokLib
'   (classgen/hka_ragdoll_instance.py).
'
' NIVELES DE CONFIANZA (importante):
'  - ESTRUCTURAL (sólido, no especulado): skeleton, nombres de body, refs a shapes
'    y constraints (se siguen los fixups reales del packfile), y hkaRagdollInstance
'    completo (fuente autoritativa HavokLib).
'  - INFERIDO SIN SDK (prefijo `Guess_`): los hknp*Shape y los atoms internos de los
'    hkp*ConstraintData NO están en HavokLib ni hay header del SDK Havok disponible.
'    Los offsets de geometría de cápsula (endpoints) y de los ángulos límite de los
'    joints (twist/cone/plane/hinge) se mapearon por --dump + invariante (ángulos en
'    rango sano, consistentes entre instancias). Cualquier campo cuyo SIGNIFICADO se
'    infirió así lleva el prefijo `Guess_` para que sea inequívoco en la API.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Public Partial Class HkxObjectGraph_Class

    ' --- Entry point: hknpPhysicsSceneData → hknpRagdollData (estructural, sólido) ---
    Public Function ParsePhysicsSceneData(source As HkxVirtualObjectGraph_Class) As HknpPhysicsSceneDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hknpPhysicsSceneData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim result As New HknpPhysicsSceneDataGraph_Class With {.SourceObject = source}
        result.SystemDatas.AddRange(ReadObjectReferenceArray(source.RelativeOffset + &H10))
        Return result
    End Function

    ''' <summary>hknpRagdollData (FO4) — vista ESTRUCTURAL: name, skeleton, nombres de rigid body,
    ''' y las refs (deduplicadas) a sus hknp*Shape y hkp*ConstraintData. Todo se obtiene siguiendo
    ''' los fixups reales del packfile (no se infieren offsets de campos internos del hknp).</summary>
    Public Function ParseRagdollData(source As HkxVirtualObjectGraph_Class) As HkxRagdollGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hknpRagdollData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkxRagdollGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Name = ResolveLocalString(rel + &H70),
            .Skeleton = ResolveGlobalObject(rel + &H78)
        }
        result.SkeletonName = If(IsNothing(result.Skeleton), "", ResolveLocalString(result.Skeleton.RelativeOffset + BaseObjectFieldOffset))

        ' Nombres de rigid body = strings referenciados por local-fixup dentro del objeto (≠ nombre del
        ' sistema). OJO: algunos local-fixups son punteros a DATOS de hkArray (no strings); se filtran
        ' exigiendo patrón "nombre válido" (1er char letra + charset de nombre de hueso) para no colar basura.
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
        For Each lf In GetLocalFixupsInRange(rel, source.Size)
            Dim s = ReadNullTerminatedString(lf.DestinationRelativeOffset)
            If IsLikelyNameToken(s) AndAlso Not s.Equals(result.Name, StringComparison.Ordinal) AndAlso seen.Add(s) Then
                result.BodyNames.Add(s)
            End If
        Next

        ' Shapes + constraints: refs globales del objeto, clasificadas por nombre de clase (deduplicadas).
        For Each gf In GetGlobalFixupsInRange(rel, source.Size)
            Dim o = GetObject(gf.TargetRelativeOffset)
            If IsNothing(o) Then Continue For
            If o.ClassName.IndexOf("Shape", StringComparison.OrdinalIgnoreCase) >= 0 Then
                If Not result.Shapes.Contains(o) Then result.Shapes.Add(o)
            ElseIf o.ClassName.IndexOf("Constraint", StringComparison.OrdinalIgnoreCase) >= 0 Then
                If Not result.Constraints.Contains(o) Then result.Constraints.Add(o)
            End If
        Next
        Return result
    End Function

    ''' <summary>hkaRagdollInstance (Skyrim/BodySlide) — layout AUTORITATIVO de HavokLib:
    ''' rigidBodies + constraints + boneToRigidBodyMap + skeleton. Offsets calculados desde
    ''' BaseObjectFieldOffset (format-robusto: calza con base-0x10 64-bit y base-0x08 32-bit).</summary>
    Public Function ParseRagdollInstance(source As HkxVirtualObjectGraph_Class) As HkxRagdollGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaRagdollInstance", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim baseOff = BaseObjectFieldOffset
        Dim ahs = ArrayHeaderSizeValue
        Dim result As New HkxRagdollGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Skeleton = ResolveGlobalObject(rel + baseOff + (3 * ahs))
        }
        result.SkeletonName = If(IsNothing(result.Skeleton), "", ResolveLocalString(result.Skeleton.RelativeOffset + BaseObjectFieldOffset))
        result.RigidBodies.AddRange(ReadObjectReferenceArray(rel + baseOff))
        result.Constraints.AddRange(ReadObjectReferenceArray(rel + baseOff + ahs))
        Dim mapHeader = ReadArrayHeader(rel + baseOff + (2 * ahs))
        If mapHeader.Count > 0 AndAlso mapHeader.DataRelativeOffset >= 0 Then
            For i = 0 To mapHeader.Count - 1
                result.BoneToBodyMap.Add(ReadInt32(mapHeader.DataRelativeOffset + (i * 4)))
            Next
        End If
        Return result
    End Function

    ''' <summary>Todos los ragdolls del grafo (hknpRagdollData + hkaRagdollInstance) unificados.</summary>
    Public Function ParseRagdolls() As List(Of HkxRagdollGraph_Class)
        Dim result As New List(Of HkxRagdollGraph_Class)
        For Each o In GetObjectsByClassName("hknpRagdollData")
            Dim r = ParseRagdollData(o)
            If Not IsNothing(r) Then result.Add(r)
        Next
        For Each o In GetObjectsByClassName("hkaRagdollInstance")
            Dim r = ParseRagdollInstance(o)
            If Not IsNothing(r) Then result.Add(r)
        Next
        Return result
    End Function

    ' Patrón "token de nombre válido": 1er char letra; resto en charset típico de nombres Havok.
    ' Filtra punteros a datos binarios de hkArray que ReadNullTerminatedString interpretaría como string.
    ' Compartido por ragdoll (body names) y cloth-setup (nombres/bones/anchors). [[HclClothSetupGraphParser]]
    Friend Shared Function IsLikelyNameToken(s As String) As Boolean
        If String.IsNullOrEmpty(s) OrElse s.Length < 2 OrElse s.Length > 96 Then Return False
        If Not Char.IsLetter(s(0)) Then Return False
        For Each ch In s
            If Not (Char.IsLetterOrDigit(ch) OrElse ch = " "c OrElse ch = "_"c OrElse ch = "."c OrElse ch = "-"c OrElse ch = ":"c OrElse ch = "["c OrElse ch = "]"c) Then Return False
        Next
        Return True
    End Function

    ' ---------------------------------------------------------------------------
    ' GEOMETRÍA / LÍMITES — TODO `Guess_` (inferido por --dump, sin SDK Havok).
    ' Los offsets de límite están calibrados para FO4 (hk2014); en Skyrim (otra versión
    ' de Havok, atoms de distinto tamaño) NO aplican y devuelven valores espurios.
    ' ---------------------------------------------------------------------------

    ''' <summary>hknpCapsuleShape. GUESS (sin SDK): los dos vec4 en +0x50/+0x60 (w=0, distintos de los
    ''' vértices del hull con w=0.5+índice) parecen los endpoints del eje de la cápsula; el spread entre
    ''' ellos = largo del hueso. El RADIO no se localizó con confianza → no se expone (no se fabrica).</summary>
    Public Function ParseNpCapsuleShape(source As HkxVirtualObjectGraph_Class) As HknpCapsuleShapeGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hknpCapsuleShape", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Return New HknpCapsuleShapeGraph_Class With {
            .SourceObject = source,
            .Guess_EndpointA = New HkxVector4Graph_Class With {.X = ReadSingle(rel + &H50), .Y = ReadSingle(rel + &H54), .Z = ReadSingle(rel + &H58), .W = ReadSingle(rel + &H5C)},
            .Guess_EndpointB = New HkxVector4Graph_Class With {.X = ReadSingle(rel + &H60), .Y = ReadSingle(rel + &H64), .Z = ReadSingle(rel + &H68), .W = ReadSingle(rel + &H6C)}
        }
    End Function

    ''' <summary>hkpRagdollConstraintData. GUESS (sin SDK): ángulos límite del joint ragdoll mapeados
    ''' por offset+invariante (rad, consistentes entre instancias). twist@+0x138/+0x13C, cone-max@+0x15C,
    ''' plane@+0x178/+0x17C; pivotes (w-column de transformA@+0x30 / transformB@+0x70). Los 3 refs a
    ''' hkpPositionConstraintMotor SÍ son sólidos (se siguen fixups). La asignación twist/cone/plane es
    ''' la inferencia más razonable por el rango de valores, pero NO está confirmada contra el SDK.</summary>
    Public Function ParseRagdollConstraint(source As HkxVirtualObjectGraph_Class) As HkpRagdollConstraintGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkpRagdollConstraintData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkpRagdollConstraintGraph_Class With {
            .SourceObject = source,
            .Guess_TwistMinAngle = ReadSingle(rel + &H138),
            .Guess_TwistMaxAngle = ReadSingle(rel + &H13C),
            .Guess_ConeMaxAngle = ReadSingle(rel + &H15C),
            .Guess_PlaneMinAngle = ReadSingle(rel + &H178),
            .Guess_PlaneMaxAngle = ReadSingle(rel + &H17C),
            .Guess_PivotA = New HkxVector4Graph_Class With {.X = ReadSingle(rel + &H3C), .Y = ReadSingle(rel + &H4C), .Z = ReadSingle(rel + &H5C), .W = 0},
            .Guess_PivotB = New HkxVector4Graph_Class With {.X = ReadSingle(rel + &H7C), .Y = ReadSingle(rel + &H8C), .Z = ReadSingle(rel + &H9C), .W = 0}
        }
        For Each gf In GetGlobalFixupsInRange(rel, source.Size)
            Dim o = GetObject(gf.TargetRelativeOffset)
            If Not IsNothing(o) AndAlso o.ClassName.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso Not result.Motors.Contains(o) Then
                result.Motors.Add(o)
            End If
        Next
        Return result
    End Function

    ''' <summary>hkpLimitedHingeConstraintData. GUESS (sin SDK): límite de bisagra min/max @+0xFC/+0x100
    ''' (rad), mapeado por offset+invariante. Pivote = w-column de transformA@+0x30. Motor por ref (sólido).</summary>
    Public Function ParseHingeConstraint(source As HkxVirtualObjectGraph_Class) As HkpLimitedHingeConstraintGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkpLimitedHingeConstraintData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkpLimitedHingeConstraintGraph_Class With {
            .SourceObject = source,
            .Guess_HingeMinAngle = ReadSingle(rel + &HFC),
            .Guess_HingeMaxAngle = ReadSingle(rel + &H100),
            .Guess_PivotA = New HkxVector4Graph_Class With {.X = ReadSingle(rel + &H3C), .Y = ReadSingle(rel + &H4C), .Z = ReadSingle(rel + &H5C), .W = 0}
        }
        For Each gf In GetGlobalFixupsInRange(rel, source.Size)
            Dim o = GetObject(gf.TargetRelativeOffset)
            If Not IsNothing(o) AndAlso o.ClassName.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso Not result.Motors.Contains(o) Then
                result.Motors.Add(o)
            End If
        Next
        Return result
    End Function

End Class

' ====================== Result classes (ragdoll / physics) ======================

' hknpPhysicsSceneData — entry point del ragdoll FO4.
Public Class HknpPhysicsSceneDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property SystemDatas As New List(Of HkxVirtualObjectGraph_Class) ' refs a hknpRagdollData / hknpPhysicsSystemData
End Class

' Vista unificada del ragdoll (hknpRagdollData FO4 o hkaRagdollInstance Skyrim). Lo de aquí es ESTRUCTURAL/sólido.
Public Class HkxRagdollGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String = ""
    Public Property Name As String = ""
    Public Property Skeleton As HkxVirtualObjectGraph_Class
    Public Property SkeletonName As String = ""
    Public ReadOnly Property BodyNames As New List(Of String)                          ' nombres de rigid body (p.ej. "Ragdoll_NPC COM")
    Public ReadOnly Property Shapes As New List(Of HkxVirtualObjectGraph_Class)        ' hknp*Shape (capsule/polytope) distintos
    Public ReadOnly Property Constraints As New List(Of HkxVirtualObjectGraph_Class)   ' hkp*ConstraintData distintos
    Public ReadOnly Property RigidBodies As New List(Of HkxVirtualObjectGraph_Class)   ' solo hkaRagdollInstance
    Public ReadOnly Property BoneToBodyMap As New List(Of Integer)                     ' solo hkaRagdollInstance (índice de hueso → body)
End Class

' hknpCapsuleShape — geometría. Endpoints INFERIDOS (Guess_), radio no expuesto.
Public Class HknpCapsuleShapeGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Guess_EndpointA As HkxVector4Graph_Class
    Public Property Guess_EndpointB As HkxVector4Graph_Class
    ' Largo del eje de la cápsula (derivado de los endpoints inferidos).
    Public ReadOnly Property Guess_AxisLength As Single
        Get
            If IsNothing(Guess_EndpointA) OrElse IsNothing(Guess_EndpointB) Then Return 0
            Dim dx = Guess_EndpointB.X - Guess_EndpointA.X, dy = Guess_EndpointB.Y - Guess_EndpointA.Y, dz = Guess_EndpointB.Z - Guess_EndpointA.Z
            Return CSng(Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)))
        End Get
    End Property
End Class

' hkpRagdollConstraintData — límites de joint. TODOS los ángulos/pivotes son Guess_ (sin SDK); Motors es sólido.
Public Class HkpRagdollConstraintGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Guess_TwistMinAngle As Single   ' rad
    Public Property Guess_TwistMaxAngle As Single   ' rad
    Public Property Guess_ConeMaxAngle As Single    ' rad (swing)
    Public Property Guess_PlaneMinAngle As Single   ' rad
    Public Property Guess_PlaneMaxAngle As Single   ' rad
    Public Property Guess_PivotA As HkxVector4Graph_Class
    Public Property Guess_PivotB As HkxVector4Graph_Class
    Public ReadOnly Property Motors As New List(Of HkxVirtualObjectGraph_Class) ' hkpPositionConstraintMotor (sólido, por fixup)
End Class

' hkpLimitedHingeConstraintData — límite de bisagra. Guess_ (sin SDK); Motors sólido.
Public Class HkpLimitedHingeConstraintGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Guess_HingeMinAngle As Single   ' rad
    Public Property Guess_HingeMaxAngle As Single   ' rad
    Public Property Guess_PivotA As HkxVector4Graph_Class
    Public ReadOnly Property Motors As New List(Of HkxVirtualObjectGraph_Class)
End Class
