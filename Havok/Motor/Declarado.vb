Option Strict On
Option Explicit On

' =================================================================================================
' LO QUE ESTE MOTOR DECLARA QUE INGIERE — como DATO, no como comentario.
'
' ⛔⛔ No es una lista decorativa: `HavokLayoutGate` la contrasta contra la lista CERRADA que declara
' la reflexión del `.exe` (`HavokConstraintSets.SubclasesDeclaradas`). Una clase que aparezca en el
' motor y no acá, o una subclase nueva del binario que nadie ingiera, salen ahí — no en un barrido a
' mano ni en una lectura de código.
'
' ⛔ Los nombres NO se escriben: cada clase generada emite su `NombreDeClase` desde la reflexión, y
' la lista lo cita. Un literal acá sería una cuarta copia de algo que ya vive en el .exe.
'
' Vive en `Havok.Motor` porque es el motor el que las cumple: la declaración y el que la ejecuta
' tienen que estar en el mismo sitio para que no puedan divergir.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>Las clases del archivo que este motor sabe ejecutar.</summary>
    Public NotInheritable Class Declarado

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Los OPERADORES que la cadena despacha (`Fachada.OperadorDe`).
        ''' <para>El censo M1 midió que SIETE clases son todo lo que traen las 759 prendas del corpus
        ''' vanilla: `hclObjectSpaceSkinPNOperator` (762), `hclSimulateOperator` (759),
        ''' `hclMoveParticlesOperator` (759), `hclSimpleMeshBoneDeformOperator` (759),
        ''' `hclCopyVerticesOperator` (582), `hclGatherAllVerticesOperator` (174) y
        ''' `hclGatherSomeVerticesOperator` (3).</para>
        ''' <para>Las otras tres variantes de `ObjectSpaceSkin` — `P` (22), `PNT` (24) y `PNTB` (25) —
        ''' tienen CERO apariciones en el corpus y están igual: un mod puede traerlas, y sin ellas el
        ''' operador caía en el hueco y la malla no se deformaba.</para>
        ''' </summary>
        Public Shared ReadOnly Property OperadoresQueEjecuta As Havok.Canon.HavokConstraintSets.LectorDeClase() =
            {
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclSimulateOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclSimulateOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNTOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNTOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNTBOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceSkinPNTBOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclSimpleMeshBoneDeformOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclSimpleMeshBoneDeformOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclMoveParticlesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclMoveParticlesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclGatherAllVerticesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclGatherAllVerticesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclCopyVerticesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclCopyVerticesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclGatherSomeVerticesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclGatherSomeVerticesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBlendSomeVerticesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBlendSomeVerticesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclUpdateAllVertexFramesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclUpdateAllVertexFramesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclUpdateSomeVertexFramesOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclUpdateSomeVertexFramesOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclMeshBoneDeformOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclMeshBoneDeformOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclSkinOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclSkinOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclInputConvertOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclInputConvertOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclOutputConvertOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclOutputConvertOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPNOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPNOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPNTOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPNTOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPNTBOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceSkinPNTBOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPNOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPNOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPNTOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPNTOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPNTBOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclObjectSpaceMeshMeshDeformPNTBOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPNOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPNOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPNTOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPNTOperator.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPNTBOperator.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBoneSpaceMeshMeshDeformPNTBOperator.Leer)
            }

        ''' <summary>
        ''' Los CONSTRAINT SETS que el solver aplica (`Fachada.RestriccionesDe`), con el `type` que el
        ''' `.exe` le pone a cada clase en su constructor (RE cap. 2.2).
        ''' <para>⭐ Con `hclVolumeConstraintMx` (type 17) adentro, el censo M17 mide **2.859 de 2.859**
        ''' sets del corpus llegando al solver: cero huecos.</para>
        ''' <para>⭐ Las cinco `Mx` de enlace (13, 14, 15, 16, 21) y `hclAntiPinchConstraintSet`
        ''' (19) tienen CERO apariciones en el corpus vanilla (medido) y están igual: un mod puede
        ''' traerlas y hasta ayer caían en el hueco. El gate G22 exige la equivalencia de cada `Mx`
        ''' con su gemelo escalar en vez de suponerla.</para>
        ''' </summary>
        Public Shared ReadOnly Property SetsQueIngiere As Havok.Canon.HavokConstraintSets.LectorDeClase() =
            {
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBonePlanesConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBonePlanesConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBendLinkConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBendLinkConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclCompressibleLinkConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclCompressibleLinkConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclVolumeConstraintMx.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclVolumeConstraintMx.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclVolumeConstraint.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclVolumeConstraint.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclTransitionConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclTransitionConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclLocalRangeConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclLocalRangeConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclAntiPinchConstraintSet.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclAntiPinchConstraintSet.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSetMx.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSetMx.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBendLinkConstraintSetMx.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBendLinkConstraintSetMx.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSetMx.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSetMx.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSetMx.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSetMx.Leer),
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclCompressibleLinkConstraintSetMx.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclCompressibleLinkConstraintSetMx.Leer)
            }

        ''' <summary>Las ACCIONES que el integrador aplica. `hclSimpleWindAction` es la única que el
        ''' corpus trae.</summary>
        Public Shared ReadOnly Property AccionesQueAplica As Havok.Canon.HavokConstraintSets.LectorDeClase() =
            {
                New Havok.Canon.HavokConstraintSets.LectorDeClase(Havok.Canon.Objects.HkObj_HclSimpleWindAction.NombreDeClase, AddressOf Havok.Canon.Objects.HkObj_HclSimpleWindAction.Leer)
            }

    End Class

End Namespace

#End If
