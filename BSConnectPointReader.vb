Imports System.Numerics
Imports NiflySharp.Blocks

''' <summary>
''' Lee BSConnectPoint::Parents y BSConnectPoint::Children de un NIF.
'''
''' BSConnectPoint::Parents (root NiNode del skeleton "host"):
'''   Cada entry es un "socket" donde se mountea algo:
'''     Name           — string socket name. Convención vanilla: "P-X".
'''     Parent         — bone name al que pertenece el socket (e.g. "Chest_skin").
'''     Rotation/Translation/Scale — transform local respecto al bone padre.
'''
''' BSConnectPoint::Children (root NiNode del NIF "addon"):
'''   Lista de point names que ESTE addon espera encontrar como Parent socket.
'''   Convención vanilla: "C-X" (mismo X que el "P-X" del socket destino).
'''
''' Schema NIF: nif.xml:8360-8379 (struct BSConnectPoint + ::Parents + ::Children).
'''
''' Engine vanilla usa este mecanismo para:
'''   - Robot chunks (Mr Handy arms, Sentry torsos, etc.) en NPCs OBTE-driven.
'''   - Power Armor frame ↔ piezas (Helmet/LArm/RArm sobre Frame.nif).
'''   - Weapon mods (silenciador → P-Muzzle, mira → P-Sight, etc.).
'''   - Settlement workshop items, furniture interaction nodes.
'''
''' Vive en FO4_Base_Library porque ≥2 paths reales lo consumen (NPC_Manager render
''' robot path + IMountResolver default impl que cubre los demás casos).
''' </summary>
Public Module BSConnectPointReader

    ''' <summary>Una entry "socket" del lado parent (host). Lo que el addon va a buscar
    ''' para mountarse, con la transform local relativa al bone padre del skeleton.</summary>
    Public Class ConnectPointInfo
        ''' <summary>Socket name. Convención vanilla "P-X". El addon matchea su Children
        ''' "C-X" contra esto via tail común tras prefix de 1 char + separador.</summary>
        Public Name As String
        ''' <summary>Bone name al que pertenece este socket. El addon se mountea en
        ''' transform local relativa a este bone del skeleton del host.</summary>
        Public ParentBoneName As String
        Public Rotation As Quaternion
        Public Translation As Vector3
        Public Scale As Single
    End Class

    ''' <summary>Itera el root NiNode del NIF buscando bloques BSConnectPoint::Parents en su
    ''' ExtraDataList; aplana cada Parent.ConnectPoints en una lista de ConnectPointInfo.</summary>
    Public Function ReadParents(nif As Nifcontent_Class_Manolo) As List(Of ConnectPointInfo)
        Dim result As New List(Of ConnectPointInfo)
        If nif Is Nothing Then Return result

        Dim root = nif.GetRootNode()
        If root Is Nothing OrElse root.ExtraDataList Is Nothing Then Return result

        For Each ref In root.ExtraDataList.References
            Dim block = nif.Blocks(ref.Index)
            Dim parents = TryCast(block, BSConnectPoint_Parents)
            If parents Is Nothing OrElse parents.ConnectPoints Is Nothing Then Continue For
            For Each cp In parents.ConnectPoints
                Dim info As New ConnectPointInfo With {
                    .Name = If(cp.Name?.Content, ""),
                    .ParentBoneName = If(cp.Parent?.Content, ""),
                    .Rotation = cp.Rotation,
                    .Translation = cp.Translation,
                    .Scale = cp.Scale
                }
                result.Add(info)
            Next
        Next

        Return result
    End Function

    ''' <summary>Lee los Children point names del root del NIF (addon que declara a qué
    ''' sockets parent espera adjuntarse). Devuelve set vacío si el NIF no tiene Children
    ''' block. Case-insensitive set.</summary>
    Public Function ReadChildrenNames(nif As Nifcontent_Class_Manolo) As HashSet(Of String)
        Dim result As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If nif Is Nothing Then Return result

        Dim root = nif.GetRootNode()
        If root Is Nothing OrElse root.ExtraDataList Is Nothing Then Return result

        For Each ref In root.ExtraDataList.References
            Dim block = nif.Blocks(ref.Index)
            Dim children = TryCast(block, BSConnectPoint_Children)
            If children Is Nothing OrElse children.PointName Is Nothing Then Continue For
            For Each pn In children.PointName
                Dim s = If(pn?.Content, "")
                If s <> "" Then result.Add(s)
            Next
        Next

        Return result
    End Function

    ''' <summary>Read the BSConnectPoint::Children block in full: the Skinned flag plus the
    ''' PointName list. The Skinned flag is the engine convention marker — when True the addon
    ''' is meant to be skin-driven (vertex pre-transformed in chunk-space; the engine respects
    ''' the skinning and does NOT apply the socket transform on top). When False the addon is
    ''' a rigid attachment that gets the socket transform applied as its world placement.
    ''' Returns Nothing when the NIF has no Children block.</summary>
    Public Function ReadChildren(nif As Nifcontent_Class_Manolo) As (Skinned As Boolean, PointNames As List(Of String))
        Dim names As New List(Of String)
        Dim skinned As Boolean = False
        If nif Is Nothing Then Return (False, names)

        Dim root = nif.GetRootNode()
        If root Is Nothing OrElse root.ExtraDataList Is Nothing Then Return (False, names)

        For Each ref In root.ExtraDataList.References
            Dim block = nif.Blocks(ref.Index)
            Dim children = TryCast(block, BSConnectPoint_Children)
            If children Is Nothing Then Continue For
            ' Skinned es Boolean? — interpretar Nothing como False (defensivo).
            If children.Skinned.HasValue AndAlso children.Skinned.Value Then skinned = True
            If children.PointName IsNot Nothing Then
                For Each pn In children.PointName
                    Dim s = If(pn?.Content, "")
                    If s <> "" Then names.Add(s)
                Next
            End If
        Next

        Return (skinned, names)
    End Function

    ''' <summary>Convert System.Numerics.Quaternion (como vienen los sockets) a Matrix33
    ''' (formato Transform_Class.Rotation). Reusable por consumers que arman socket
    ''' transforms para mounting (e.g. NPC_Manager.PrepareSkeleton mount-map builder).
    '''
    ''' Implementación: delega a OpenTK Matrix4.CreateFromQuaternion para usar exactamente
    ''' la misma convención row-vector-matrix que el resto del render pipeline
    ''' (SkeletonClothOverlayHelper.vb:209-219, HclCollisionPoseHelper.vb:114-122). Toma el
    ''' 3×3 superior-izquierdo. Esto es paridad-runtime con el resto del codebase y evita
    ''' tener que decidir si la fórmula manual quat→matrix es column- o row-vector
    ''' (ambigüedad que en el caso BSConnectPoint manual produjo rotaciones espejadas).
    '''
    ''' Convención de componentes: nif.xml define Quaternion en disco como (w,x,y,z).
    ''' NiflySharp lee 4 floats lineales en (X,Y,Z,W) sin remapear (NiStreamReversible.cs:275-278),
    ''' por lo que el componente .X de la struct contiene el W del disco, .Y contiene X, etc.
    ''' Remapeamos a un OpenTK Quaternion(x,y,z,w) — que es el constructor estándar.</summary>
    Public Function QuatToMatrix33(q As Quaternion) As NiflySharp.Structs.Matrix33
        ' Remap NiflySharp(X,Y,Z,W) ← disco(w,x,y,z) → OpenTK Quaternion(x,y,z,w) estándar.
        Dim openTkQuat As New OpenTK.Mathematics.Quaternion(q.Y, q.Z, q.W, q.X)
        Dim m4 = OpenTK.Mathematics.Matrix4.CreateFromQuaternion(openTkQuat)
        Dim m As New NiflySharp.Structs.Matrix33()
        m.M11 = m4.M11 : m.M12 = m4.M12 : m.M13 = m4.M13
        m.M21 = m4.M21 : m.M22 = m4.M22 : m.M23 = m4.M23
        m.M31 = m4.M31 : m.M32 = m4.M32 : m.M33 = m4.M33
        Return m
    End Function

End Module
