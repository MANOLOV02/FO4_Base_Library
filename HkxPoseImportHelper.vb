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
    Private ReadOnly _additiveHint As Boolean
    Private ReadOnly _previewPoseCache As New Dictionary(Of Integer, HkxPoseImportHelper.ImportResult)

    Private Sub New(animation As HkaSplineCompressedAnimationGraph_Class,
                    hkxSkeleton As HkaSkeletonGraph_Class,
                    liveSkeleton As SkeletonInstance,
                    tracks As List(Of ResolvedTrack),
                    diagnostics As HkxPoseImportDiagnostics,
                    additiveHint As Boolean)
        _animation = animation
        _hkxSkeleton = hkxSkeleton
        _liveSkeleton = liveSkeleton
        _tracks = tracks
        _baseDiagnostics = diagnostics
        _additiveHint = additiveHint
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

    ''' <param name="additiveHint">Aditividad declarada FUERA del archivo (el behavior graph envuelve
    ''' el clip en un DynamicAnimationTaggingGenerator 'Additive*'). OR con el blendHint del binding.</param>
    Public Shared Function Create(skeletonHkxBytes As Byte(),
                                  animationHkxBytes As Byte(),
                                  liveSkeleton As SkeletonInstance,
                                  animationDisplayPath As String,
                                  skeletonDisplayPath As String,
                                  Optional additiveHint As Boolean = False) As HkxPoseImportSession
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
        AnalyzeTrackContent(animation, tracks)
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

        Logger.LogLazy(Function() $"[HKX-POSE] Track mapping strategy={skeletonSource} resolvedTracks={tracks.Count} hasTrackNames={hasTrackNames} externalAvailable={externalSkeletonAvailable} additiveHint={additiveHint}")
        Return New HkxPoseImportSession(animation, hkxSkeleton, liveSkeleton, tracks, diagnostics, additiveHint)
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
        ' ── ADITIVOS: los tracks son DELTAS cerca de identidad, van DIRECTO a la capa Δ (componentes
        ' sin dato = identidad — ver BuildFrameLocalTransform). Aditividad declarada por (a) blendHint
        ' del binding del archivo (CrippledNoise) O (b) el behavior graph vía additiveHint (clip envuelto
        ' en DynamicAnimationTaggingGenerator 'Additive*', ej. AdditiveDynamicIdle con blendHint=0).
        ' NORMALES: Δ = inv(S) × frameLocal, donde frameLocal toma del clip SOLO los componentes CON
        ' CONTENIDO (ver AnalyzeTrackContent) y conserva S en los demás.
        Dim additive = (_animation.Binding IsNot Nothing AndAlso _animation.Binding.BlendHint <> 0) OrElse _additiveHint

        For Each resolved In _tracks
            Dim hkxTransform = _animation.GetTransform(usedFrame, resolved.TrackIndex)
            If hkxTransform Is Nothing Then
                skippedInvalidBindings += 1
                Logger.LogLazy(Function() $"[HKX-POSE] skip track={resolved.TrackIndex} bone='{resolved.BoneName}': transform missing at frame={usedFrame}.")
                Continue For
            End If

            If resolved.LiveBone Is Nothing OrElse resolved.StructuralLocalInverse Is Nothing Then
                skippedMissingLiveBones += 1
                Logger.LogLazy(Function() $"[HKX-POSE] skip track={resolved.TrackIndex} bone='{resolved.BoneName}': not present in live NIF skeleton.")
                Continue For
            End If

            ' Track sin NINGÚN componente con contenido (ver AnalyzeTrackContent) = el clip no
            ' opina sobre este hueso ⇒ queda en su local estructural S (mount incluido).
            If Not additive AndAlso Not resolved.HasContent Then Continue For


            Dim frameLocal = BuildFrameLocalTransform(hkxTransform, resolved, additive, diagnostics)
            Dim delta As Transform_Class
            If additive Then
                delta = frameLocal
            Else
                delta = resolved.StructuralLocalInverse.ComposeTransforms(frameLocal)
            End If
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

    ''' <summary>Clasifica cada COMPONENTE de cada track por TIPO DE DATO, escaneando el clip entero
    ''' una vez (clasificación medida del archivo, sin decisiones en runtime).
    ''' <para>SIN OPINIÓN = identity-typed O constante(±ε quantización) e IGUAL al refPose en TODO el
    ''' clip ⇒ conserva el local estructural S. Probado: los sockets de brazo del Handy (P-ArmsTypeA1
    ''' publicado en Ring+2.91; rig refPose 2.91 abajo; tracks constantes==refPose — honrarlos despega
    ''' los brazos del anillo) y los EyeArm (constantes==refPose; su config vive en el mount).</para>
    ''' <para>CON CONTENIDO = VARÍA en el tiempo (¡los clips crippled ANIMAN C-Head a (9.35,9.51) — la
    ''' pose del robot derrumbado!) o constante≠refPose (el despliegue del Assaultron: C-Head −3.92 ==
    ''' P-Head publicado). NO congelar sockets por nombre: el dato crippled lo refuta.</para></summary>
    Private Shared Sub AnalyzeTrackContent(animation As HkaSplineCompressedAnimationGraph_Class,
                                           tracks As List(Of ResolvedTrack))
        Const EPS_CONST_T As Single = 0.02F   ' constancia traslación (ruido de quantización spline)
        Const EPS_REF_T As Single = 0.08F     ' igualdad a refPose (traslación)
        Const EPS_CONST_S As Single = 0.002F
        Const EPS_REF_S As Single = 0.01F
        Const EPS_ROT_DOT As Single = 0.9997F ' |dot| de quats ≈ igual (≈1.4°, quats comprimidos)

        If animation Is Nothing OrElse tracks Is Nothing Then Return
        Dim nF = Math.Max(1, animation.NumFrames)

        For Each resolved In tracks
            If resolved Is Nothing OrElse resolved.LiveBone Is Nothing Then Continue For
            Dim ht0 = animation.GetTransform(0, resolved.TrackIndex)
            If ht0 Is Nothing Then Continue For
            Dim refp = resolved.ReferencePose

            For axis = 0 To 2
                Dim tAnim = (axis = 0 AndAlso ht0.TranslationXAnimated) OrElse (axis = 1 AndAlso ht0.TranslationYAnimated) OrElse (axis = 2 AndAlso ht0.TranslationZAnimated)
                Dim sAnim = (axis = 0 AndAlso ht0.ScaleXAnimated) OrElse (axis = 1 AndAlso ht0.ScaleYAnimated) OrElse (axis = 2 AndAlso ht0.ScaleZAnimated)
                Dim tContent = False, sContent = False
                If tAnim OrElse sAnim Then
                    Dim t0 = GetVectorAxis(ht0.Translation, axis, 0.0F), tMin = t0, tMax = t0
                    Dim s0 = GetVectorAxis(ht0.Scale, axis, 1.0F), sMin = s0, sMax = s0
                    For f = 1 To nF - 1
                        Dim htf = animation.GetTransform(f, resolved.TrackIndex)
                        If htf Is Nothing Then Continue For
                        If tAnim Then
                            Dim v = GetVectorAxis(htf.Translation, axis, 0.0F)
                            tMin = Math.Min(tMin, v) : tMax = Math.Max(tMax, v)
                        End If
                        If sAnim Then
                            Dim v = GetVectorAxis(htf.Scale, axis, 1.0F)
                            sMin = Math.Min(sMin, v) : sMax = Math.Max(sMax, v)
                        End If
                    Next
                    If tAnim Then tContent = (tMax - tMin) > EPS_CONST_T OrElse refp Is Nothing OrElse Math.Abs(t0 - GetVectorAxis(refp.Translation, axis, 0.0F)) > EPS_REF_T
                    If sAnim Then sContent = (sMax - sMin) > EPS_CONST_S OrElse refp Is Nothing OrElse Math.Abs(s0 - GetVectorAxis(refp.Scale, axis, 1.0F)) > EPS_REF_S
                End If
                Select Case axis
                    Case 0 : resolved.ContentTX = tContent : resolved.ContentSX = sContent
                    Case 1 : resolved.ContentTY = tContent : resolved.ContentSY = sContent
                    Case 2 : resolved.ContentTZ = tContent : resolved.ContentSZ = sContent
                End Select
            Next

            If ht0.RotationAnimated AndAlso ht0.Rotation IsNot Nothing Then
                Dim varies = False
                For f = 1 To nF - 1
                    Dim htf = animation.GetTransform(f, resolved.TrackIndex)
                    If htf Is Nothing OrElse htf.Rotation Is Nothing Then Continue For
                    If Math.Abs(QuatDot(ht0.Rotation, htf.Rotation)) < EPS_ROT_DOT Then varies = True : Exit For
                Next
                Dim refEq = refp IsNot Nothing AndAlso refp.Rotation IsNot Nothing AndAlso
                            Math.Abs(QuatDot(ht0.Rotation, refp.Rotation)) >= EPS_ROT_DOT
                resolved.ContentR = varies OrElse Not refEq
            End If

            resolved.HasContent = resolved.ContentTX OrElse resolved.ContentTY OrElse resolved.ContentTZ OrElse
                                  resolved.ContentR OrElse
                                  resolved.ContentSX OrElse resolved.ContentSY OrElse resolved.ContentSZ
        Next
    End Sub

    ''' <summary>Producto punto de quats normalizados (|dot|≈1 ⇒ misma rotación).</summary>
    Private Shared Function QuatDot(a As HkxQuaternionGraph_Class, b As HkxQuaternionGraph_Class) As Single
        Dim la = CSng(Math.Sqrt(CDbl(a.X) * a.X + CDbl(a.Y) * a.Y + CDbl(a.Z) * a.Z + CDbl(a.W) * a.W))
        Dim lb = CSng(Math.Sqrt(CDbl(b.X) * b.X + CDbl(b.Y) * b.Y + CDbl(b.Z) * b.Z + CDbl(b.W) * b.W))
        If la <= 0.000001F OrElse lb <= 0.000001F Then Return 1.0F
        Return (a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W) / (la * lb)
    End Function

    ''' <summary>Local del frame del clip.
    ''' <para>NORMAL: componente CON CONTENIDO ← clip; SIN OPINIÓN ← S (local estructural vivo) ⇒
    ''' Δ=0 en esos ejes. Con la distribución del mount corregida (CHUNK-TREE-FULL: la corrección
    ''' vive en sockets/ramas como Bethesda, no en los huesos skinneados profundos), S coincide con
    ''' la base del clip donde el clip tiene contenido — sin doble conteo. Mezclar componentes
    ''' clip/S es válido: la convención HKX→render es trivial (quat xyzw directo).</para>
    ''' <para>ADITIVO: componente sin dato = delta CERO.</para></summary>
    Private Shared Function BuildFrameLocalTransform(hkxTransform As HkxAnimationTransformGraph_Class,
                                                     resolved As ResolvedTrack,
                                                     additive As Boolean,
                                                     diagnostics As HkxPoseImportDiagnostics) As Transform_Class
        If additive Then
            Dim txA = If(hkxTransform.TranslationXAnimated, GetVectorAxis(hkxTransform.Translation, 0, 0.0F), 0.0F)
            Dim tyA = If(hkxTransform.TranslationYAnimated, GetVectorAxis(hkxTransform.Translation, 1, 0.0F), 0.0F)
            Dim tzA = If(hkxTransform.TranslationZAnimated, GetVectorAxis(hkxTransform.Translation, 2, 0.0F), 0.0F)
            Dim rA = If(hkxTransform.RotationAnimated, hkxTransform.Rotation, Nothing) ' Nothing → identidad
            Dim sxA = If(hkxTransform.ScaleXAnimated, GetVectorAxis(hkxTransform.Scale, 0, 1.0F), 1.0F)
            Dim syA = If(hkxTransform.ScaleYAnimated, GetVectorAxis(hkxTransform.Scale, 1, 1.0F), 1.0F)
            Dim szA = If(hkxTransform.ScaleZAnimated, GetVectorAxis(hkxTransform.Scale, 2, 1.0F), 1.0F)
            Return HkxTransformConventionHelper.ToTransform(txA, tyA, tzA, rA, sxA, syA, szA)
        End If

        Dim s = resolved.StructuralLocal
        Dim tx = If(resolved.ContentTX, GetVectorAxis(hkxTransform.Translation, 0, 0.0F), s.Translation.X)
        Dim ty = If(resolved.ContentTY, GetVectorAxis(hkxTransform.Translation, 1, 0.0F), s.Translation.Y)
        Dim tz = If(resolved.ContentTZ, GetVectorAxis(hkxTransform.Translation, 2, 0.0F), s.Translation.Z)
        Dim sx = If(resolved.ContentSX, GetVectorAxis(hkxTransform.Scale, 0, 1.0F), s.Scale)
        Dim sy = If(resolved.ContentSY, GetVectorAxis(hkxTransform.Scale, 1, 1.0F), s.Scale)
        Dim sz = If(resolved.ContentSZ, GetVectorAxis(hkxTransform.Scale, 2, 1.0F), s.Scale)

        If diagnostics IsNot Nothing Then
            If Not resolved.ContentTX Then diagnostics.TranslationComponentsFromReferencePose += 1
            If Not resolved.ContentTY Then diagnostics.TranslationComponentsFromReferencePose += 1
            If Not resolved.ContentTZ Then diagnostics.TranslationComponentsFromReferencePose += 1
            If Not resolved.ContentR Then diagnostics.RotationComponentsFromReferencePose += 1
            If Not (resolved.ContentSX AndAlso resolved.ContentSY AndAlso resolved.ContentSZ) Then diagnostics.ScaleComponentsFromReferencePose += 1
        End If

        If resolved.ContentR Then
            Return HkxTransformConventionHelper.ToTransform(tx, ty, tz, hkxTransform.Rotation, sx, sy, sz)
        End If
        ' Rotación sin opinión ← rotación estructural del hueso vivo.
        Return New Transform_Class With {
            .Translation = New Vector3(tx, ty, tz),
            .Rotation = s.Rotation,
            .Scale = HkxTransformConventionHelper.ResolveUniformScale(sx, sy, sz)
        }
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

    ''' <summary>Liga cada track al hueso del esqueleto vivo, captura su local ESTRUCTURAL
    ''' <c>S_b = O×Mount</c> y el refPose del rig del clip.
    ''' <para>MODELO FINAL (2026-06-11): la animación es REEMPLAZO TOTAL del local en el frame del
    ''' rig del clip — <c>local_b(t) = L_anim_b(t)</c>, con componentes identity ← refPose del
    ''' skeleton.hkx (semántica del engine). En la capa Δ del getter <c>O×Mount×Morph×Δ</c>:
    ''' <c>Δ_b = inv(S_b) × L_anim_b</c>, UNIVERSAL, sin modos. Funciona porque el MOUNT EN REPOSO
    ''' debe ser engine-correcto (= los skin-binds de los chunks bien placeados): para Assaultron
    ''' los wants del mount coinciden EXACTO con los binds del chunk, con Assaultron.nif y con lo
    ''' que juegan los clips (Neck −3.921, HeadNod −4.999, clavículas 17.14) ⇒ local=L_anim no
    ''' doble-cuenta nada. Humano/criatura: S=O≈refPose ⇒ legacy. Si un robot se deforma al animar
    ''' con esta fórmula, el bug está EN EL MOUNT DE REPOSO (placement del chunk), no acá — ej.
    ''' detectado: Handy Pelvis mount +8.690/+0.269 == EXACTO el local de C-BotLegs del rig
    ''' (socket contado dos veces en el placement).</para></summary>
    Private Shared Sub BindLiveSkeletonTracks(tracks As List(Of ResolvedTrack),
                                              hkxSkeleton As HkaSkeletonGraph_Class,
                                              liveSkeleton As SkeletonInstance)
        If tracks Is Nothing OrElse liveSkeleton Is Nothing OrElse liveSkeleton.SkeletonDictionary Is Nothing Then Return

        For Each resolved In tracks
            If resolved Is Nothing OrElse String.IsNullOrWhiteSpace(resolved.BoneName) Then Continue For

            Dim liveBone As HierarchiBone_class = Nothing
            If liveSkeleton.SkeletonDictionary.TryGetValue(resolved.BoneName, liveBone) AndAlso liveBone IsNot Nothing Then
                resolved.LiveBone = liveBone
                Dim s = liveBone.OriginalLocaLTransform
                If liveBone.MountDeltaTransform IsNot Nothing Then s = s.ComposeTransforms(liveBone.MountDeltaTransform)
                resolved.StructuralLocal = s
                resolved.StructuralLocalInverse = s.Inverse()
            End If

            If hkxSkeleton IsNot Nothing AndAlso hkxSkeleton.ReferencePose IsNot Nothing AndAlso
               resolved.BoneIndex >= 0 AndAlso resolved.BoneIndex < hkxSkeleton.ReferencePose.Count Then
                resolved.ReferencePose = hkxSkeleton.ReferencePose(resolved.BoneIndex)
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
        ''' <summary>S_b = O×Mount del hueso vivo al crear la sesión — el local ESTRUCTURAL.
        ''' Los componentes SIN OPINIÓN del clip conservan S; Δ = inv(S)×frameLocal.</summary>
        Public Property StructuralLocal As Transform_Class
        Public Property StructuralLocalInverse As Transform_Class
        ''' <summary>refPose del rig DEL CLIP — referencia de la clasificación por componente.</summary>
        Public Property ReferencePose As HkxQsTransformGraph_Class
        ''' <summary>Clasificación por TIPO DE DATO (AnalyzeTrackContent): True = contenido del clip
        ''' (varía, o constante≠refPose); False = sin opinión (identity o constante==refPose) ⇒ S.</summary>
        Public Property ContentTX As Boolean
        Public Property ContentTY As Boolean
        Public Property ContentTZ As Boolean
        Public Property ContentR As Boolean
        Public Property ContentSX As Boolean
        Public Property ContentSY As Boolean
        Public Property ContentSZ As Boolean
        Public Property HasContent As Boolean
    End Class
End Class
