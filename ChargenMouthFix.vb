Imports OpenTK.Mathematics

''' <summary>
''' Optional fix for the vanilla FO4 mouth bug shipped in BaseFemaleHeadChargen.tri (FRTRI003).
'''
''' MEASURED (byte compare vs the BA2 vanilla): three chargen morphs — DefaultFaceType0, EyesLowLidUp,
''' EyesLowSunken — carry a SHARED block of 22 spurious vertex displacements in the mouth/lip region
''' (X≈±1.3..1.6, below the nose tip, up to 0.73 units). DefaultFaceType0 is applied to the base female
''' face, so every default female mouth inherits the distortion; the two eye morphs bleed the same
''' displacement onto the mouth when used. The community fix zeroes those 22 deltas.
'''
''' This module replicates ONLY that mouth fix (the 22 shared verts). For DefaultFaceType0 those 22 are
''' its entire non-zero set, so it becomes null (identical to the fixed file). For the two eye morphs the
''' 22 mouth verts are zeroed and the legit eye deltas are left untouched (the checkbox is a MOUTH fix).
'''
''' Applied at TRI read time — render (NpcMorphResolver.TryLoadTriHead) AND bake
''' (FaceGenBuildPipeline.ParseHeadTri) — gated on <see cref="Config_App.Setting_ApplyMouthVanillaFix"/>
''' and ONLY for this one file. Default OFF = pure vanilla. Callers vary their path-keyed cache on
''' <see cref="CacheKeySuffix"/> so toggling the setting re-reads instead of serving a stale head.
''' </summary>
Public Module ChargenMouthFix

    ''' <summary>The one file this fix targets, matched case-insensitively on the path/key tail.</summary>
    Public Const TargetTriFileName As String = "basefemaleheadchargen.tri"

    ' The chargen morphs that carry the spurious mouth-region deltas in vanilla.
    Private ReadOnly TargetMorphNames As String() = {"DefaultFaceType0", "EyesLowLidUp", "EyesLowSunken"}

    ' The 22 shared vertex indices (mouth/lip region, below the nose tip) whose deltas are zeroed.
    ' = the ENTIRE non-zero set of DefaultFaceType0 (nulls it), a SUBSET of EyesLowLidUp/EyesLowSunken.
    Private ReadOnly MouthVertexIds As Integer() = {
        815, 819, 1358, 1363, 1364, 1365, 1369, 1371, 1372, 1373, 1374,
        1376, 1606, 1609, 1610, 1615, 1617, 1618, 1619, 1620, 1679, 1680
    }

    ''' <summary>True when this path/key is the female chargen head tri (case-insensitive tail match).</summary>
    Public Function AppliesTo(pathOrKey As String) As Boolean
        If String.IsNullOrEmpty(pathOrKey) Then Return False
        Dim p = pathOrKey.Replace("/"c, "\"c).ToLowerInvariant()
        Return p.EndsWith(TargetTriFileName, StringComparison.Ordinal)
    End Function

    ''' <summary>True when the fix WOULD be applied for this path (FO4 active game + setting ON + right file).
    ''' Game-gated to Fallout4: this is a FO4 vanilla file — SSE has no BaseFemaleHeadChargen.tri (it uses
    ''' femalehead.tri), so the filename alone would already miss, but the explicit gate keeps it unambiguous.
    ''' Callers key their cache on this (via <see cref="CacheKeySuffix"/>) so a vanilla and a fixed head never alias.</summary>
    Public Function IsActiveFor(pathOrKey As String) As Boolean
        If Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 Then Return False
        Return Config_App.Current.Setting_ApplyMouthVanillaFix AndAlso AppliesTo(pathOrKey)
    End Function

    ''' <summary>Cache-key suffix ("" or "|mouthfix"): a path-keyed TriHead cache holds the vanilla and the
    ''' fixed head under distinct keys, so toggling the setting hits/parses the right one (no stale head).</summary>
    Public Function CacheKeySuffix(pathOrKey As String) As String
        Return If(IsActiveFor(pathOrKey), "|mouthfix", "")
    End Function

    ''' <summary>Zero the 22 mouth-region deltas across the 3 target morphs, IN PLACE, iff the setting is ON
    ''' and this is the female chargen tri. No-op otherwise. Call on a freshly parsed head (not one already
    ''' shared) before it is cached, so the merge/render/bake downstream sees the fixed deltas.</summary>
    Public Sub MaybeApplyInPlace(pathOrKey As String, head As TriHeadFile)
        If head Is Nothing Then Return
        If Not IsActiveFor(pathOrKey) Then Return
        For Each name In TargetMorphNames
            Dim m = head.GetMorph(name)
            If m Is Nothing OrElse m.Vertices Is Nothing Then Continue For
            For Each vid In MouthVertexIds
                If vid >= 0 AndAlso vid < m.Vertices.Length Then
                    m.Vertices(vid) = New Vector3(0.0F, 0.0F, 0.0F)
                End If
            Next
        Next
    End Sub

End Module
