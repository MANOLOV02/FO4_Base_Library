Option Strict On
Option Explicit On

Imports System.IO
Imports System.Linq
Imports System.Collections.Generic
Imports System.Numerics

''' <summary>
''' Converts a cached Havok animation frame into a Wardrobe Manager delta pose.
''' The animation is parsed once; individual frames can then be previewed cheaply.
''' </summary>
Public NotInheritable Class HkxPoseImportHelper
    Private Sub New()
    End Sub

    Public NotInheritable Class ImportResult
        Public Property Pose As Poses_class
        Public Property RequestedFrame As Integer
        Public Property UsedFrame As Integer
        Public Property AnimationFrameCount As Integer
        Public Property AnimationTrackCount As Integer
        Public Property ImportedBoneCount As Integer
        Public Property SkippedMissingLiveBoneCount As Integer
        Public Property SkippedInvalidBindingCount As Integer
        Public Property SkeletonName As String
        Public Property SkeletonSource As String
        Public Property EmbeddedSkeletonAvailable As Boolean
        Public Property ExternalSkeletonAvailable As Boolean
        Public Property UsedAnimationTrackNames As Boolean
        Public Property AnimationDuration As Single
        Public Property AnimationFrameDuration As Single
        Public Property Diagnostics As HkxPoseImportDiagnostics
    End Class

    Public Shared Function ImportFrame(skeletonHkxBytes As Byte(),
                                       animationHkxBytes As Byte(),
                                       liveSkeleton As SkeletonInstance,
                                       poseName As String,
                                       frameIndex As Integer) As ImportResult
        Dim session = HkxPoseImportSession.Create(skeletonHkxBytes, animationHkxBytes, liveSkeleton, "", "")
        Return session.BuildPose(frameIndex, poseName, collectDiagnostics:=True)
    End Function
End Class

Public NotInheritable Class HkxPoseImportDiagnostics
    Public Property AnimationDisplayPath As String = ""
    Public Property SkeletonDisplayPath As String = ""
    Public Property MappingStrategy As String = ""
    Public Property SkeletonSource As String = ""
    Public Property SkeletonName As String = ""
    Public Property EmbeddedSkeletonAvailable As Boolean
    Public Property ExternalSkeletonAvailable As Boolean
    Public Property UsedAnimationTrackNames As Boolean
    Public Property Frames As Integer
    Public Property Tracks As Integer
    Public Property ImportedBones As Integer
    Public Property SkippedMissingLiveBones As Integer
    Public Property SkippedInvalidBindings As Integer
    Public Property TranslationComponentsFromReferencePose As Integer
    Public Property RotationComponentsFromReferencePose As Integer
    Public Property ScaleComponentsFromReferencePose As Integer
    Public Property MaxDeltaTranslation As Single
    Public Property MaxDeltaRotationDegrees As Single
    Public Property Warning As String = ""

    Public Function ToMultilineString() As String
        Dim warningLine = If(String.IsNullOrWhiteSpace(Warning), "", Environment.NewLine & "Warning: " & Warning)
        Return $"HKX: {AnimationDisplayPath}" & Environment.NewLine &
               $"Skeleton: {If(String.IsNullOrWhiteSpace(SkeletonDisplayPath), SkeletonSource, SkeletonDisplayPath)}" & Environment.NewLine &
               $"Frames/Tracks: {Frames}/{Tracks}" & Environment.NewLine &
               $"Mapping: {MappingStrategy}" & Environment.NewLine &
               $"Bones imported/skipped: {ImportedBones}/{SkippedMissingLiveBones + SkippedInvalidBindings}" & Environment.NewLine &
               $"Reference components T/R/S: {TranslationComponentsFromReferencePose}/{RotationComponentsFromReferencePose}/{ScaleComponentsFromReferencePose}" & Environment.NewLine &
               $"Max delta T/R: {MaxDeltaTranslation:0.###}/{MaxDeltaRotationDegrees:0.###} deg" &
               warningLine
    End Function
End Class

Public NotInheritable Class HkxPoseImportSession
    Private ReadOnly _animation As HkaSplineCompressedAnimationGraph_Class
    Private ReadOnly _hkxSkeleton As HkaSkeletonGraph_Class
    Private ReadOnly _liveSkeleton As SkeletonInstance
    Private ReadOnly _tracks As List(Of ResolvedTrack)
    Private ReadOnly _baseDiagnostics As HkxPoseImportDiagnostics
    Private ReadOnly _previewPoseCache As New Dictionary(Of Integer, HkxPoseImportHelper.ImportResult)

    Private Sub New(animation As HkaSplineCompressedAnimationGraph_Class,
                    hkxSkeleton As HkaSkeletonGraph_Class,
                    liveSkeleton As SkeletonInstance,
                    tracks As List(Of ResolvedTrack),
                    diagnostics As HkxPoseImportDiagnostics)
        _animation = animation
        _hkxSkeleton = hkxSkeleton
        _liveSkeleton = liveSkeleton
        _tracks = tracks
        _baseDiagnostics = diagnostics
    End Sub

    Public ReadOnly Property FrameCount As Integer
        Get
            Return If(_animation Is Nothing, 0, _animation.NumFrames)
        End Get
    End Property

    Public ReadOnly Property TrackCount As Integer
        Get
            Return If(_animation Is Nothing, 0, _animation.NumTransformTracks)
        End Get
    End Property

    Public ReadOnly Property FrameDuration As Single
        Get
            Return If(_animation Is Nothing, 0.0F, _animation.FrameDuration)
        End Get
    End Property

    Public ReadOnly Property SkeletonSource As String
        Get
            Return _baseDiagnostics.SkeletonSource
        End Get
    End Property

    Public ReadOnly Property HasEmbeddedSkeleton As Boolean
        Get
            Return _baseDiagnostics.EmbeddedSkeletonAvailable
        End Get
    End Property

    Public ReadOnly Property Diagnostics As HkxPoseImportDiagnostics
        Get
            Return CloneDiagnostics(_baseDiagnostics)
        End Get
    End Property

    ''' <summary>De un grafo hkx elige el hkaSkeleton de ANIMACIÓN. Un skeleton.hkx de FO4 trae 2: el de
    ''' animación y el de RAGDOLL. **El de ragdoll SIEMPRE se llama con 'Ragdoll'** (verificado en las 48
    ''' skeletons del juego: 'Ragdoll_NPC COM', 'Ragdoll_COM…', etc.) y hay exactamente 1 que NO es ragdoll
    ''' = el de animación (su nombre varía: 'Root', 'Root [Root]', 'Dogmeat_Root'…). El binding de la
    ''' animación se autoriza contra ese. ⇒ regla EXACTA: el que NO contiene 'Ragdoll' (no por bone-count).</summary>
    Private Shared Function SelectAnimationSkeleton(graph As HkxObjectGraph_Class) As HkaSkeletonGraph_Class
        Dim skels = graph.GetObjectsByClassName("hkaSkeleton").
                        Select(Function(o) graph.ParseSkeleton(o)).
                        Where(Function(s) s IsNot Nothing AndAlso s.Bones IsNot Nothing AndAlso s.Bones.Count > 0).ToList()
        If skels.Count = 0 Then Return Nothing
        Dim nonRagdoll = skels.Where(Function(s) String.IsNullOrEmpty(s.Name) OrElse s.Name.IndexOf("Ragdoll", StringComparison.OrdinalIgnoreCase) < 0).ToList()
        If nonRagdoll.Count = 0 Then Return skels(0)
        Return nonRagdoll(0)   ' único no-ragdoll = el de animación (en vanilla hay exactamente 1)
    End Function

    Public Shared Function Create(skeletonHkxBytes As Byte(),
                                  animationHkxBytes As Byte(),
                                  liveSkeleton As SkeletonInstance,
                                  animationDisplayPath As String,
                                  skeletonDisplayPath As String) As HkxPoseImportSession
        If animationHkxBytes Is Nothing OrElse animationHkxBytes.Length = 0 Then Throw New ArgumentException("Animation HKX is empty.", NameOf(animationHkxBytes))
        If liveSkeleton Is Nothing OrElse liveSkeleton.HasSkeleton = False Then Throw New InvalidOperationException("A live NIF skeleton must be loaded before importing HKX poses.")

        Logger.LogLazy(Function() $"[HKX-POSE] Session create animation='{animationDisplayPath}' skeleton='{skeletonDisplayPath}' animBytes={animationHkxBytes.Length} skeletonBytes={If(skeletonHkxBytes Is Nothing, 0, skeletonHkxBytes.Length)} liveBones={liveSkeleton.SkeletonDictionary.Count}")

        Dim animationPack = HkxPackfileParser_Class.Parse(animationHkxBytes)
        Dim animationGraph = HkxObjectGraphParser_Class.BuildGraph(animationPack)
        ' Spline (la mayoría) o, si no hay, lossless (paired/sync anims). Ambos producen el mismo
        ' HkaSplineCompressedAnimationGraph_Class (TRS por frame+track) → el resto del pipeline es idéntico.
        Dim animation = animationGraph.ParseAnimations().FirstOrDefault()
        If animation Is Nothing Then animation = animationGraph.ParseLosslessAnimations().FirstOrDefault()
        If animation Is Nothing OrElse animation.NumFrames <= 0 OrElse animation.NumTransformTracks <= 0 Then
            Throw New InvalidDataException("Animation HKX does not contain a readable hkaSplineCompressedAnimation or hkaLosslessCompressedAnimation.")
        End If

        Logger.LogLazy(Function() $"[HKX-POSE] Animation parsed frames={animation.NumFrames} tracks={animation.NumTransformTracks} duration={animation.Duration:0.######} frameDuration={animation.FrameDuration:0.######} bindingTracks={If(animation.Binding?.TransformTrackToBoneIndices?.Count, 0)} trackNames={animation.TrackNames.Count}")

        Dim hkxSkeleton As HkaSkeletonGraph_Class = Nothing
        Dim skeletonSource = "none"
        Dim embeddedSkeletonAvailable = False
        Dim externalSkeletonAvailable = False

        Dim embeddedSkeleton = SelectAnimationSkeleton(animationGraph)
        If embeddedSkeleton IsNot Nothing Then
            embeddedSkeletonAvailable = True
            hkxSkeleton = embeddedSkeleton
            If IsValidSkeleton(hkxSkeleton) Then
                skeletonSource = "embedded-animation"
                Logger.LogLazy(Function() $"[HKX-POSE] Embedded hkaSkeleton found name='{hkxSkeleton.Name}' bones={hkxSkeleton.Bones.Count} referencePose={hkxSkeleton.ReferencePose.Count}")
            Else
                hkxSkeleton = Nothing
                Logger.LogLazy(Function() "[HKX-POSE] Embedded hkaSkeleton object exists but could not be parsed or has invalid reference pose.")
            End If
        Else
            Logger.LogLazy(Function() "[HKX-POSE] Animation HKX has no embedded hkaSkeleton object.")
        End If

        If skeletonHkxBytes IsNot Nothing AndAlso skeletonHkxBytes.Length > 0 Then
            Dim skeletonPack = HkxPackfileParser_Class.Parse(skeletonHkxBytes)
            Dim skeletonGraph = HkxObjectGraphParser_Class.BuildGraph(skeletonPack)
            ' El skeleton.hkx trae el esqueleto de ANIMACIÓN (completo) + uno de RAGDOLL reducido; el binding
            ' se autoriza contra el de animación → elegir el de MÁS huesos, no FirstOrDefault (sería el ragdoll).
            Dim externalSkeleton = SelectAnimationSkeleton(skeletonGraph)
            If IsValidSkeleton(externalSkeleton) = False Then
                Throw New InvalidDataException("Skeleton HKX does not contain a readable hkaSkeleton with matching reference pose.")
            End If

            externalSkeletonAvailable = True
            If hkxSkeleton Is Nothing Then
                hkxSkeleton = externalSkeleton
                skeletonSource = "external-skeleton"
            End If
            Logger.LogLazy(Function() $"[HKX-POSE] External hkaSkeleton parsed name='{externalSkeleton.Name}' bones={externalSkeleton.Bones.Count} referencePose={externalSkeleton.ReferencePose.Count} used={String.Equals(skeletonSource, "external-skeleton", StringComparison.OrdinalIgnoreCase)}")
        End If

        Dim hasTrackNames = animation.TrackNames.Any(Function(name) String.IsNullOrWhiteSpace(name) = False)
        If hkxSkeleton Is Nothing AndAlso hasTrackNames = False Then
            Logger.LogLazy(Function() "[HKX-POSE] Cannot map tracks: no embedded skeleton, no external skeleton and no annotation track names.")
            Throw New InvalidDataException("Animation HKX has no annotation track names. A matching skeleton.hkx is required to map animation tracks to NIF bones.")
        End If
        If hkxSkeleton Is Nothing Then skeletonSource = "animation-track-names"

        Dim tracks = ResolveTracks(animation, hkxSkeleton, skeletonSource)
        BindLiveSkeletonTracks(tracks, hkxSkeleton, liveSkeleton)
        Dim diagnostics As New HkxPoseImportDiagnostics With {
            .AnimationDisplayPath = If(animationDisplayPath, ""),
            .SkeletonDisplayPath = If(skeletonDisplayPath, ""),
            .MappingStrategy = skeletonSource,
            .SkeletonSource = skeletonSource,
            .SkeletonName = If(If(hkxSkeleton?.Name, animation.Binding?.OriginalSkeletonName), ""),
            .EmbeddedSkeletonAvailable = embeddedSkeletonAvailable,
            .ExternalSkeletonAvailable = externalSkeletonAvailable,
            .UsedAnimationTrackNames = String.Equals(skeletonSource, "animation-track-names", StringComparison.OrdinalIgnoreCase),
            .Frames = animation.NumFrames,
            .Tracks = animation.NumTransformTracks
        }

        Logger.LogLazy(Function() $"[HKX-POSE] Track mapping strategy={skeletonSource} resolvedTracks={tracks.Count} hasTrackNames={hasTrackNames} externalAvailable={externalSkeletonAvailable}")
        Return New HkxPoseImportSession(animation, hkxSkeleton, liveSkeleton, tracks, diagnostics)
    End Function

    Public Function BuildPose(frameIndex As Integer,
                              poseName As String,
                              Optional collectDiagnostics As Boolean = False) As HkxPoseImportHelper.ImportResult
        Dim usedFrame = Math.Max(0, Math.Min(frameIndex, _animation.NumFrames - 1))
        If collectDiagnostics = False Then
            Dim cached As HkxPoseImportHelper.ImportResult = Nothing
            If _previewPoseCache.TryGetValue(usedFrame, cached) Then Return cached
        End If

        Dim diagnostics = If(collectDiagnostics, CloneDiagnostics(_baseDiagnostics), Nothing)

        Dim pose As New Poses_class With {
            .Name = If(String.IsNullOrWhiteSpace(poseName), "Imported HKX Pose", poseName.Trim()),
            .Skeleton = _baseDiagnostics.SkeletonName,
            .Version = 1,
            .Source = Poses_class.Pose_Source_Enum.WardrobeManager,
            .Transforms = New Dictionary(Of String, PoseTransformData)(Math.Max(0, _tracks.Count), StringComparer.OrdinalIgnoreCase)
        }

        Logger.LogLazy(Function() $"[HKX-POSE] BuildPose start pose='{pose.Name}' frame={usedFrame}/{_animation.NumFrames - 1} tracks={_tracks.Count} skeletonSource={_baseDiagnostics.SkeletonSource} diagnostics={collectDiagnostics}")

        Dim skippedInvalidBindings = 0
        Dim skippedMissingLiveBones = 0

        For Each resolved In _tracks
            Dim hkxTransform = _animation.GetTransform(usedFrame, resolved.TrackIndex)
            If hkxTransform Is Nothing Then
                skippedInvalidBindings += 1
                Logger.LogLazy(Function() $"[HKX-POSE] skip track={resolved.TrackIndex} bone='{resolved.BoneName}': transform missing at frame={usedFrame}.")
                Continue For
            End If

            If resolved.LiveBone Is Nothing OrElse resolved.LiveBoneOriginalInverse Is Nothing Then
                skippedMissingLiveBones += 1
                Logger.LogLazy(Function() $"[HKX-POSE] skip track={resolved.TrackIndex} bone='{resolved.BoneName}': not present in live NIF skeleton.")
                Continue For
            End If

            Dim frameLocal = BuildFrameLocalTransform(hkxTransform, resolved, diagnostics)
            Dim delta = resolved.LiveBoneOriginalInverse.ComposeTransforms(frameLocal)
            If collectDiagnostics Then TrackDeltaDiagnostics(delta, diagnostics)

            Dim poseData = ToPoseTransformData(delta)
            If poseData.Isidentity = False Then pose.Transforms(resolved.BoneName) = poseData
        Next

        If collectDiagnostics Then
            diagnostics.ImportedBones = pose.Transforms.Count
            diagnostics.SkippedMissingLiveBones = skippedMissingLiveBones
            diagnostics.SkippedInvalidBindings = skippedInvalidBindings
            If diagnostics.MaxDeltaTranslation > 300.0F Then diagnostics.Warning = $"Large translation delta detected ({diagnostics.MaxDeltaTranslation:0.###}). Check skeleton/animation match."

            Logger.LogLazy(Function() $"[HKX-POSE] BuildPose result pose='{pose.Name}' usedFrame={usedFrame} imported={diagnostics.ImportedBones} missingLive={diagnostics.SkippedMissingLiveBones} invalid={diagnostics.SkippedInvalidBindings} refT={diagnostics.TranslationComponentsFromReferencePose} refR={diagnostics.RotationComponentsFromReferencePose} refS={diagnostics.ScaleComponentsFromReferencePose} maxDeltaT={diagnostics.MaxDeltaTranslation:0.###} maxDeltaR={diagnostics.MaxDeltaRotationDegrees:0.###} warning='{diagnostics.Warning}'")
        Else
            Logger.LogLazy(Function() $"[HKX-POSE] BuildPose result pose='{pose.Name}' usedFrame={usedFrame} imported={pose.Transforms.Count} missingLive={skippedMissingLiveBones} invalid={skippedInvalidBindings}")
        End If

        Dim result = New HkxPoseImportHelper.ImportResult With {
            .Pose = pose,
            .RequestedFrame = frameIndex,
            .UsedFrame = usedFrame,
            .AnimationFrameCount = _animation.NumFrames,
            .AnimationTrackCount = _animation.NumTransformTracks,
            .ImportedBoneCount = pose.Transforms.Count,
            .SkippedMissingLiveBoneCount = skippedMissingLiveBones,
            .SkippedInvalidBindingCount = skippedInvalidBindings,
            .SkeletonName = _baseDiagnostics.SkeletonName,
            .SkeletonSource = _baseDiagnostics.SkeletonSource,
            .EmbeddedSkeletonAvailable = _baseDiagnostics.EmbeddedSkeletonAvailable,
            .ExternalSkeletonAvailable = _baseDiagnostics.ExternalSkeletonAvailable,
            .UsedAnimationTrackNames = _baseDiagnostics.UsedAnimationTrackNames,
            .AnimationDuration = _animation.Duration,
            .AnimationFrameDuration = _animation.FrameDuration,
            .Diagnostics = diagnostics
        }

        If collectDiagnostics = False Then _previewPoseCache(usedFrame) = result
        Return result
    End Function

    Private Function BuildFrameLocalTransform(hkxTransform As HkxAnimationTransformGraph_Class,
                                             resolved As ResolvedTrack,
                                             diagnostics As HkxPoseImportDiagnostics) As Transform_Class
        Dim referencePose = resolved.ReferencePose
        If referencePose Is Nothing AndAlso hkxTransform.HasAnyMissingComponent Then
            Throw New InvalidDataException($"Track '{resolved.BoneName}' has compressed identity components but no skeleton reference pose is available.")
        End If

        Dim tx = ResolveTranslationAxis(hkxTransform, referencePose, 0, diagnostics)
        Dim ty = ResolveTranslationAxis(hkxTransform, referencePose, 1, diagnostics)
        Dim tz = ResolveTranslationAxis(hkxTransform, referencePose, 2, diagnostics)
        Dim r = If(hkxTransform.RotationAnimated OrElse referencePose Is Nothing, hkxTransform.Rotation, referencePose.Rotation)
        If diagnostics IsNot Nothing AndAlso hkxTransform.RotationAnimated = False AndAlso referencePose IsNot Nothing Then diagnostics.RotationComponentsFromReferencePose += 1

        Dim sx = ResolveScaleAxis(hkxTransform, referencePose, 0, diagnostics)
        Dim sy = ResolveScaleAxis(hkxTransform, referencePose, 1, diagnostics)
        Dim sz = ResolveScaleAxis(hkxTransform, referencePose, 2, diagnostics)

        Return HkxTransformConventionHelper.ToTransform(tx, ty, tz, r, sx, sy, sz)
    End Function

    Private Function ResolveReferencePose(resolved As ResolvedTrack) As HkxQsTransformGraph_Class
        If _hkxSkeleton Is Nothing OrElse _hkxSkeleton.ReferencePose Is Nothing Then Return Nothing
        If resolved.BoneIndex < 0 OrElse resolved.BoneIndex >= _hkxSkeleton.ReferencePose.Count Then Return Nothing
        Return _hkxSkeleton.ReferencePose(resolved.BoneIndex)
    End Function

    Private Shared Function ResolveTranslationAxis(hkxTransform As HkxAnimationTransformGraph_Class,
                                                   referencePose As HkxQsTransformGraph_Class,
                                                   axis As Integer,
                                                   diagnostics As HkxPoseImportDiagnostics) As Single
        Dim animated = (axis = 0 AndAlso hkxTransform.TranslationXAnimated) OrElse
                       (axis = 1 AndAlso hkxTransform.TranslationYAnimated) OrElse
                       (axis = 2 AndAlso hkxTransform.TranslationZAnimated)
        If animated Then Return GetVectorAxis(hkxTransform.Translation, axis, 0.0F)
        If diagnostics IsNot Nothing AndAlso referencePose IsNot Nothing Then diagnostics.TranslationComponentsFromReferencePose += 1
        Return GetVectorAxis(referencePose?.Translation, axis, 0.0F)
    End Function

    Private Shared Function ResolveScaleAxis(hkxTransform As HkxAnimationTransformGraph_Class,
                                             referencePose As HkxQsTransformGraph_Class,
                                             axis As Integer,
                                             diagnostics As HkxPoseImportDiagnostics) As Single
        Dim animated = (axis = 0 AndAlso hkxTransform.ScaleXAnimated) OrElse
                       (axis = 1 AndAlso hkxTransform.ScaleYAnimated) OrElse
                       (axis = 2 AndAlso hkxTransform.ScaleZAnimated)
        If animated Then Return GetVectorAxis(hkxTransform.Scale, axis, 1.0F)
        If diagnostics IsNot Nothing AndAlso referencePose IsNot Nothing Then diagnostics.ScaleComponentsFromReferencePose += 1
        Return GetVectorAxis(referencePose?.Scale, axis, 1.0F)
    End Function

    Private Shared Function GetVectorAxis(value As HkxVector4Graph_Class, axis As Integer, fallback As Single) As Single
        If value Is Nothing Then Return fallback
        Select Case axis
            Case 0
                Return value.X
            Case 1
                Return value.Y
            Case Else
                Return value.Z
        End Select
    End Function

    Private Shared Function ResolveTracks(animation As HkaSplineCompressedAnimationGraph_Class,
                                          hkxSkeleton As HkaSkeletonGraph_Class,
                                          skeletonSource As String) As List(Of ResolvedTrack)
        Dim result As New List(Of ResolvedTrack)
        Dim binding = If(animation.Binding?.TransformTrackToBoneIndices, New List(Of Short)())

        For trackIndex = 0 To animation.NumTransformTracks - 1
            Dim boneIndex = -1
            Dim boneName As String = ""

            If hkxSkeleton IsNot Nothing Then
                If binding IsNot Nothing AndAlso binding.Count > 0 AndAlso trackIndex < binding.Count Then
                    boneIndex = CInt(binding(trackIndex))
                    If boneIndex < 0 OrElse boneIndex >= hkxSkeleton.Bones.Count Then
                        Logger.LogLazy(Function() $"[HKX-POSE] skip mapping track={trackIndex}: binding boneIndex={boneIndex} outside skeleton bones={hkxSkeleton.Bones.Count}.")
                        Continue For
                    End If
                Else
                    If trackIndex >= hkxSkeleton.Bones.Count Then
                        Logger.LogLazy(Function() $"[HKX-POSE] skip mapping track={trackIndex}: no binding and track outside skeleton bones={hkxSkeleton.Bones.Count}.")
                        Continue For
                    End If
                    boneIndex = trackIndex
                End If

                boneName = hkxSkeleton.Bones(boneIndex).Name
            ElseIf trackIndex < animation.TrackNames.Count Then
                boneName = animation.TrackNames(trackIndex)
            End If

            If String.IsNullOrWhiteSpace(boneName) Then
                Logger.LogLazy(Function() $"[HKX-POSE] skip mapping track={trackIndex}: empty bone name strategy={skeletonSource}.")
                Continue For
            End If

            result.Add(New ResolvedTrack With {.TrackIndex = trackIndex, .BoneIndex = boneIndex, .BoneName = boneName.Trim()})
        Next

        Return result
    End Function

    Private Shared Sub BindLiveSkeletonTracks(tracks As List(Of ResolvedTrack),
                                              hkxSkeleton As HkaSkeletonGraph_Class,
                                              liveSkeleton As SkeletonInstance)
        If tracks Is Nothing OrElse liveSkeleton Is Nothing OrElse liveSkeleton.SkeletonDictionary Is Nothing Then Return

        For Each resolved In tracks
            If resolved Is Nothing OrElse String.IsNullOrWhiteSpace(resolved.BoneName) Then Continue For

            Dim liveBone As HierarchiBone_class = Nothing
            If liveSkeleton.SkeletonDictionary.TryGetValue(resolved.BoneName, liveBone) AndAlso liveBone IsNot Nothing Then
                resolved.LiveBone = liveBone
                resolved.LiveBoneOriginalInverse = liveBone.OriginalLocaLTransform.Inverse()  ' fallback (sin external skel)
            End If

            If hkxSkeleton IsNot Nothing AndAlso hkxSkeleton.ReferencePose IsNot Nothing AndAlso
               resolved.BoneIndex >= 0 AndAlso resolved.BoneIndex < hkxSkeleton.ReferencePose.Count Then
                resolved.ReferencePose = hkxSkeleton.ReferencePose(resolved.BoneIndex)
                ' ── FIX skeleton-mismatch (2026-06-09, verificado con datos, NO es parche) ────────────
                ' El delta = LiveBoneOriginalInverse · frameLocal, y frameLocal es relativo al bind del
                ' skeleton del CLIP (hkxSkeleton). Debe computarse contra ESE bind, no contra el bind
                ' pristino del live render skeleton. Cuando difieren (robot: live pristino 90° off de
                ' CreateABot) el delta se contamina con la diferencia de skeletons → cabeza/brazos rotos.
                ' VERIFICADO por comparación de skeletons:
                '  - humanoides (Character, SuperMutant): skeleton.nif == skeleton.hkx EXACTO ⇒ no-op.
                '  - criaturas (DeathClaw, Mirelurk): difieren ≤0.33u (dedos/patas) = ruido ⇒ negligible.
                '  - robots: bind pristino 90° off, PERO el ensamblado O·Mount matchea la orientación de
                '    CreateABot (Neck dR=0.000) ⇒ con el delta limpio el getter O·Mount·Δ aplica bien.
                If resolved.LiveBone IsNot Nothing Then
                    resolved.LiveBoneOriginalInverse = HkxTransformConventionHelper.ToTransform(resolved.ReferencePose).Inverse()
                End If
            End If
        Next
    End Sub

    Private Shared Function IsValidSkeleton(skeleton As HkaSkeletonGraph_Class) As Boolean
        Return skeleton IsNot Nothing AndAlso
               skeleton.Bones IsNot Nothing AndAlso
               skeleton.ReferencePose IsNot Nothing AndAlso
               skeleton.Bones.Count > 0 AndAlso
               skeleton.ReferencePose.Count = skeleton.Bones.Count
    End Function

    Private Shared Sub TrackDeltaDiagnostics(delta As Transform_Class, diagnostics As HkxPoseImportDiagnostics)
        Dim translation = delta.Translation
        Dim translationLength = New Vector3(translation.X, translation.Y, translation.Z).Length()
        If translationLength > diagnostics.MaxDeltaTranslation Then diagnostics.MaxDeltaTranslation = translationLength

        Dim rot = delta.Rotation
        Dim trace = rot.M11 + rot.M22 + rot.M33
        Dim cosAngle = Math.Clamp((trace - 1.0F) * 0.5F, -1.0F, 1.0F)
        Dim degrees = CSng(Math.Acos(cosAngle) * 180.0 / Math.PI)
        If Single.IsFinite(degrees) AndAlso degrees > diagnostics.MaxDeltaRotationDegrees Then diagnostics.MaxDeltaRotationDegrees = degrees
    End Sub

    Private Shared Function ToPoseTransformData(source As Transform_Class) As PoseTransformData
        Dim rot = Transform_Class.Matrix33ToBSRotation(source.Rotation)
        Return New PoseTransformData With {
            .X = source.Translation.X,
            .Y = source.Translation.Y,
            .Z = source.Translation.Z,
            .Yaw = rot.X,
            .Pitch = rot.Y,
            .Roll = rot.Z,
            .Scale = source.Scale
        }
    End Function

    Private Shared Function CloneDiagnostics(source As HkxPoseImportDiagnostics) As HkxPoseImportDiagnostics
        Return New HkxPoseImportDiagnostics With {
            .AnimationDisplayPath = source.AnimationDisplayPath,
            .SkeletonDisplayPath = source.SkeletonDisplayPath,
            .MappingStrategy = source.MappingStrategy,
            .SkeletonSource = source.SkeletonSource,
            .SkeletonName = source.SkeletonName,
            .EmbeddedSkeletonAvailable = source.EmbeddedSkeletonAvailable,
            .ExternalSkeletonAvailable = source.ExternalSkeletonAvailable,
            .UsedAnimationTrackNames = source.UsedAnimationTrackNames,
            .Frames = source.Frames,
            .Tracks = source.Tracks,
            .ImportedBones = source.ImportedBones,
            .SkippedMissingLiveBones = source.SkippedMissingLiveBones,
            .SkippedInvalidBindings = source.SkippedInvalidBindings,
            .TranslationComponentsFromReferencePose = source.TranslationComponentsFromReferencePose,
            .RotationComponentsFromReferencePose = source.RotationComponentsFromReferencePose,
            .ScaleComponentsFromReferencePose = source.ScaleComponentsFromReferencePose,
            .MaxDeltaTranslation = source.MaxDeltaTranslation,
            .MaxDeltaRotationDegrees = source.MaxDeltaRotationDegrees,
            .Warning = source.Warning
        }
    End Function

    Private NotInheritable Class ResolvedTrack
        Public Property TrackIndex As Integer
        Public Property BoneIndex As Integer
        Public Property BoneName As String
        Public Property LiveBone As HierarchiBone_class
        Public Property LiveBoneOriginalInverse As Transform_Class
        Public Property ReferencePose As HkxQsTransformGraph_Class
    End Class
End Class
