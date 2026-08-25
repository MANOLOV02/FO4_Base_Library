' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

Imports System.Linq

Namespace Havok.Canon

    ''' <summary>
    ''' ⛔⛔ LOS CONSTRAINT SETS DE UNA CLASE, RESUELTOS POR NOMBRE DE BLOQUE. UNA SOLA LEY, ACÁ.
    '''
    ''' <para>`hclSimClothData.staticConstraintSets` (+0xB8) y `antiPinchConstraintSets` (+0xC8) son
    ''' arreglos de punteros a `hclConstraintSet`. La subclase real la dice el bloque del archivo y el
    ''' motor la resuelve por su vtable. El objeto generado entrega la lista con el tipo BASE, así que
    ''' <c>OfType(Of HkObj_HclStandardLinkConstraintSet)</c> sobre esa lista devuelve CERO — y lo hace
    ''' EN SILENCIO, que es peor que fallar.</para>
    '''
    ''' <para>⛔ ESTA ES LA ÚNICA COPIA. Hubo tres resolutores iguales escritos por separado (en el
    ''' solver, en el visor de drape y en el modo de auditoría) y ese es exactamente el camino por el
    ''' que una ley se desincroniza. Todo consumidor pasa por acá.</para>
    ''' </summary>
    Public NotInheritable Class HavokConstraintSets

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Los sets de <paramref name="clase"/> que el sim-cloth declara en `staticConstraintSets`,
        ''' en el ORDEN del arreglo. Devuelve además la posición de cada uno, porque
        ''' `hclSimulateOperator.constraintExecution` los referencia por ese índice.
        ''' </summary>
        Public Shared Function CrudosDe(sim As Havok.Canon.Objects.HkObj_HclSimClothData, clase As String) As List(Of (Indice As Integer, Bloque As HkxVirtualObjectGraph_Class))
            Dim r As New List(Of (Indice As Integer, Bloque As HkxVirtualObjectGraph_Class))
            If sim Is Nothing OrElse sim.StaticConstraintSets Is Nothing Then Return r
            For i = 0 To sim.StaticConstraintSets.Count - 1
                Dim crudo = sim.Raw.StaticConstraintSetsRef(i)
                If crudo Is Nothing Then Continue For
                If Not String.Equals(crudo.ClassName, clase, StringComparison.OrdinalIgnoreCase) Then Continue For
                r.Add((i, crudo))
            Next
            Return r
        End Function

        Public Shared Function Standard(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSet)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSet)
            For Each e In CrudosDe(sim, "hclStandardLinkConstraintSet")
                Dim o = Havok.Canon.Objects.HkObj_HclStandardLinkConstraintSet.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

        Public Shared Function Stretch(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSet)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSet)
            For Each e In CrudosDe(sim, "hclStretchLinkConstraintSet")
                Dim o = Havok.Canon.Objects.HkObj_HclStretchLinkConstraintSet.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

        Public Shared Function BendStiffness(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSet)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSet)
            For Each e In CrudosDe(sim, "hclBendStiffnessConstraintSet")
                Dim o = Havok.Canon.Objects.HkObj_HclBendStiffnessConstraintSet.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

        Public Shared Function LocalRange(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclLocalRangeConstraintSet)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclLocalRangeConstraintSet)
            For Each e In CrudosDe(sim, "hclLocalRangeConstraintSet")
                Dim o = Havok.Canon.Objects.HkObj_HclLocalRangeConstraintSet.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

        Public Shared Function BonePlanes(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclBonePlanesConstraintSet)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclBonePlanesConstraintSet)
            For Each e In CrudosDe(sim, "hclBonePlanesConstraintSet")
                Dim o = Havok.Canon.Objects.HkObj_HclBonePlanesConstraintSet.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

        Public Shared Function BendLink(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclBendLinkConstraintSet)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclBendLinkConstraintSet)
            For Each e In CrudosDe(sim, "hclBendLinkConstraintSet")
                Dim o = Havok.Canon.Objects.HkObj_HclBendLinkConstraintSet.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

        Public Shared Function VolumeMx(sim As Havok.Canon.Objects.HkObj_HclSimClothData) As List(Of Havok.Canon.Objects.HkObj_HclVolumeConstraintMx)
            Dim r As New List(Of Havok.Canon.Objects.HkObj_HclVolumeConstraintMx)
            For Each e In CrudosDe(sim, "hclVolumeConstraintMx")
                Dim o = Havok.Canon.Objects.HkObj_HclVolumeConstraintMx.Read(sim.Graph, e.Bloque)
                If o IsNot Nothing Then r.Add(o)
            Next
            Return r
        End Function

    End Class

End Namespace
