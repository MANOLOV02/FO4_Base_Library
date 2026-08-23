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
' NIVELES DE CONFIANZA — 2026-08-22: se acabaron los `Guess_`.
'  - ESTRUCTURAL (siempre fue sólido): skeleton, nombres de body, refs a shapes y
'    constraints (se siguen los fixups reales del packfile), y hkaRagdollInstance
'    completo (fuente autoritativa HavokLib).
'  - LAYOUT DE CAMPOS: ya NO se infiere. Sale de `Havok.Canon.HavokLayout`, que se genera
'    desde la reflexión `hkClass`/`hkClassMember` que el propio ejecutable del juego embebe
'    (Tools/HavokLayoutGen). Eso nombró todo lo que antes era `Guess_`:
'       Guess_TwistMinAngle  -> atoms.twistLimit.minAngle
'       Guess_ConeMaxAngle   -> atoms.coneLimit.maxAngle
'       Guess_PlaneMin/Max   -> atoms.planesLimit.minAngle/.maxAngle
'       Guess_HingeMin/Max   -> atoms.angLimit.minAngle/.maxAngle
'       Guess_EndpointA/B    -> hknpCapsuleShape::a / ::b
'    ⛔ Y CORRIGIÓ un error real: `Guess_PivotA/B` leían las lanes `w` de las columnas de
'    ROTACIÓN (padding / residuo SIMD). El pivote es la TRASLACIÓN del hkTransform.
'    Ver el contraejemplo medido en HkTransformTranslationOffset.
'  - Lo que sigue SIN resolver se dice, no se fabrica: `hknpShape::convexRadius` está
'    declarado pero sus valores no se comportan como un radio (ver ParseNpCapsuleShape).
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Public Partial Class HkxObjectGraph_Class

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
    ' GEOMETRÍA / LÍMITES — offsets desde la TABLA CANÓNICA (Havok.Canon.HavokLayout),
    ' que sale de la reflexión hkClass del propio .exe. Ya no hay literales acá.
    '
    ' GAME-AWARE GRATIS: la tabla se elige por `Packfile.Header.PackfileFormat`, que la
    ' librería deriva de FileVersion+PointerSize del header (Fallout64 / Skyrim64 / Skyrim32).
    ' Los layouts DIFIEREN entre juegos — p.ej. `hkpRagdollConstraintData` mide 0x1A0 en FO4 y
    ' 0x180 en SSE porque los atoms de FO4 llevan 12 bytes de padding que SSE no tiene — así que
    ' pedirle el offset a la tabla del formato correcto es lo único que da el número correcto.
    ' Sin tabla (Skyrim32, que es x86 y la tabla describe x64) NO se inventan números: se
    ' devuelve la parte estructural y `LayoutNote` dice por qué.
    ' ---------------------------------------------------------------------------

    ''' <summary>
    ''' `hkTransform` = `hkRotation` (3 × hkVector4, las columnas) + `hkVector4 m_translation`.
    ''' La traslación está en +0x30 del transform. No es una inferencia: es la definición del tipo
    ''' `transform` que la reflexión declara, y mide 0x40 bytes.
    ''' ⛔ HISTORIA: hasta 2026-08-22 este parser leía el pivote de las lanes `w` de las tres columnas
    ''' de ROTACIÓN (+0x3C/+0x4C/+0x5C). Esas lanes son padding. CONTRAEJEMPLO medido en
    ''' `Meshes\Actors\Alien\CharacterAssets\skeleton.hkx` (BA2 vanilla de FO4): en `transformB` las
    ''' lanes w valen (-7.4e-08, 6.0e-07, 0) — residuo SIMD — mientras la traslación real en +0xA0
    ''' vale (-4.5334, 0.24482, ±6.0444), espejada en Z entre el hueso izquierdo y el derecho.
    ''' </summary>
    Private Const HkTransformTranslationOffset As Integer = &H30

    ''' <summary>Tabla canónica del formato que este packfile DECLARA. Nothing si no hay (Skyrim32).</summary>
    Private ReadOnly Property CanonLayout As Havok.Canon.HavokLayout
        Get
            Return Havok.Canon.HavokLayout.For(Packfile.Header.PackfileFormat)
        End Get
    End Property

    Private Function ReadVec3At(rel As Integer, offset As Integer) As HkxVector4Graph_Class
        If offset < 0 Then Return Nothing
        Return New HkxVector4Graph_Class With {
            .X = ReadSingle(rel + offset),
            .Y = ReadSingle(rel + offset + 4),
            .Z = ReadSingle(rel + offset + 8),
            .W = ReadSingle(rel + offset + 12)
        }
    End Function

    ''' <summary>Traslación (pivote) de un miembro de tipo `transform`. Nothing si el miembro no existe.</summary>
    Private Function ReadTransformTranslation(rel As Integer, layout As Havok.Canon.HavokLayout,
                                              className As String, memberPath As String) As HkxVector4Graph_Class
        Dim o = layout.Offset(className, memberPath)
        If o < 0 Then Return Nothing
        Return ReadVec3At(rel, o + HkTransformTranslationOffset)
    End Function

    ''' <summary>
    ''' `hkpRagdollConstraintData` — límites del joint. Los cinco ángulos ya NO son `Guess_`: la
    ''' reflexión los nombra (`atoms.twistLimit.minAngle/.maxAngle`, `atoms.coneLimit.maxAngle`,
    ''' `atoms.planesLimit.minAngle/.maxAngle`) y los valores cierran sobre datos vanilla de FO4:
    ''' ∓10° de twist, 35° de cono y −30°/+50° de planos en `Alien\skeleton.hkx`.
    ''' Los pivotes son la traslación de `atoms.transforms.transformA/B` (ver HkTransformTranslationOffset).
    ''' Los refs a `hkpPositionConstraintMotor` se siguen por fixup (siempre fueron sólidos).
    ''' </summary>
    Public Function ParseRagdollConstraint(source As HkxVirtualObjectGraph_Class) As HkpRagdollConstraintGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkpRagdollConstraintData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkpRagdollConstraintGraph_Class With {.SourceObject = source}
        Dim layout = CanonLayout
        If layout Is Nothing Then
            result.LayoutNote = Havok.Canon.HavokLayout.UnsupportedNote(Packfile.Header.PackfileFormat)
        Else
            Const cls = "hkpRagdollConstraintData"
            result.LayoutSupported = True
            result.LayoutNote = layout.Tag
            result.TwistMinAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.twistLimit.minAngle"))
            result.TwistMaxAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.twistLimit.maxAngle"))
            result.ConeMaxAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.coneLimit.maxAngle"))
            result.PlaneMinAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.planesLimit.minAngle"))
            result.PlaneMaxAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.planesLimit.maxAngle"))
            result.PivotA = ReadTransformTranslation(rel, layout, cls, "atoms.transforms.transformA")
            result.PivotB = ReadTransformTranslation(rel, layout, cls, "atoms.transforms.transformB")
        End If
        CollectMotors(rel, source.Size, result.Motors)
        Return result
    End Function

    ''' <summary>
    ''' `hkpLimitedHingeConstraintData` — límite de bisagra. `atoms.angLimit.minAngle/.maxAngle` por
    ''' reflexión; medido en FO4 vanilla: −110° / +1,8°. Pivote = traslación de `atoms.transforms.transformA`.
    ''' </summary>
    Public Function ParseHingeConstraint(source As HkxVirtualObjectGraph_Class) As HkpLimitedHingeConstraintGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkpLimitedHingeConstraintData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkpLimitedHingeConstraintGraph_Class With {.SourceObject = source}
        Dim layout = CanonLayout
        If layout Is Nothing Then
            result.LayoutNote = Havok.Canon.HavokLayout.UnsupportedNote(Packfile.Header.PackfileFormat)
        Else
            Const cls = "hkpLimitedHingeConstraintData"
            result.LayoutSupported = True
            result.LayoutNote = layout.Tag
            result.HingeMinAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.angLimit.minAngle"))
            result.HingeMaxAngle = ReadNullableSingle(rel, layout.Offset(cls, "atoms.angLimit.maxAngle"))
            result.PivotA = ReadTransformTranslation(rel, layout, cls, "atoms.transforms.transformA")
        End If
        CollectMotors(rel, source.Size, result.Motors)
        Return result
    End Function

    ''' <summary>
    ''' Nothing cuando el miembro no existe en la tabla. Un `Single?` sin valor NO se puede confundir
    ''' con "el límite vale 0 rad", que es lo que pasaba cuando se devolvía 0.0F.
    ''' </summary>
    Private Function ReadNullableSingle(rel As Integer, offset As Integer) As Single?
        If offset < 0 Then Return Nothing
        Return ReadSingle(rel + offset)
    End Function

    Private Sub CollectMotors(rel As Integer, size As Integer, target As List(Of HkxVirtualObjectGraph_Class))
        For Each gf In GetGlobalFixupsInRange(rel, size)
            Dim o = GetObject(gf.TargetRelativeOffset)
            If Not IsNothing(o) AndAlso o.ClassName.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso Not target.Contains(o) Then
                target.Add(o)
            End If
        Next
    End Sub

End Class

' ====================== Result classes (ragdoll / physics) ======================

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

''' <summary>Común a los resultados que dependen de la tabla canónica de layout.</summary>
Public MustInherit Class HkxCanonLayoutResult_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    ''' <summary>True si había tabla canónica para el formato de este packfile.</summary>
    Public Property LayoutSupported As Boolean = False
    ''' <summary>Tag de la tabla usada ("FO4"/"SSE"), o el motivo por el que no hay.</summary>
    Public Property LayoutNote As String = ""
End Class

' hkpRagdollConstraintData — límites de joint. Ángulos y pivotes por reflexión; Motors por fixup.
' Los ángulos son Single? a propósito: sin tabla no hay valor, y "sin valor" no se confunde con "0 rad".
Public Class HkpRagdollConstraintGraph_Class
    Inherits HkxCanonLayoutResult_Class
    Public Property TwistMinAngle As Single?    ' rad — atoms.twistLimit.minAngle
    Public Property TwistMaxAngle As Single?    ' rad — atoms.twistLimit.maxAngle
    Public Property ConeMaxAngle As Single?     ' rad — atoms.coneLimit.maxAngle (swing)
    Public Property PlaneMinAngle As Single?    ' rad — atoms.planesLimit.minAngle
    Public Property PlaneMaxAngle As Single?    ' rad — atoms.planesLimit.maxAngle
    ''' <summary>Traslación de atoms.transforms.transformA (frame del constraint en el cuerpo A).</summary>
    Public Property PivotA As HkxVector4Graph_Class
    ''' <summary>Traslación de atoms.transforms.transformB (el mismo frame en el cuerpo B).</summary>
    Public Property PivotB As HkxVector4Graph_Class
    Public ReadOnly Property Motors As New List(Of HkxVirtualObjectGraph_Class) ' hkpPositionConstraintMotor
End Class

' hkpLimitedHingeConstraintData — límite de bisagra.
Public Class HkpLimitedHingeConstraintGraph_Class
    Inherits HkxCanonLayoutResult_Class
    Public Property HingeMinAngle As Single?    ' rad — atoms.angLimit.minAngle
    Public Property HingeMaxAngle As Single?    ' rad — atoms.angLimit.maxAngle
    Public Property PivotA As HkxVector4Graph_Class
    Public ReadOnly Property Motors As New List(Of HkxVirtualObjectGraph_Class)
End Class
