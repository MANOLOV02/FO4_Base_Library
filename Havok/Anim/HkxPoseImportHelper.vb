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
        Public Property AnimationTrackCount As Integer
        Public Property ImportedBoneCount As Integer
        Public Property SkippedMissingLiveBoneCount As Integer
        Public Property SkippedInvalidBindingCount As Integer
        Public Property SkeletonName As String
        Public Property SkeletonSource As String
    End Class

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

End Class

Public NotInheritable Class HkxPoseImportSession
    Private ReadOnly _animation As HkxAnimacionDescomprimida_Class
    Private ReadOnly _hkxSkeleton As Havok.Canon.Objects.HkObj_HkaSkeleton
    Private ReadOnly _liveSkeleton As SkeletonInstance
    Private ReadOnly _tracks As List(Of ResolvedTrack)
    Private ReadOnly _baseDiagnostics As HkxPoseImportDiagnostics
    Private ReadOnly _additiveHint As Boolean
    ''' <summary>El clip es un overlay aditivo (blendHint 1 o 2, o el hint del caller). Se resuelve UNA vez en
    ''' el constructor y lo leen BuildPose, BuildUnboundBoneWmData y BuildUnboundBoneSamData: una ley, un lugar.</summary>
    Private ReadOnly _esAditivo As Boolean
    ''' <summary>blendHint = 2 (ADDITIVE actual). Decide el ORDEN de composicion: S o add, en vez de add o S.</summary>
    Private ReadOnly _esAditivoActual As Boolean
    ''' <summary>
    ''' ⛔ LA UNICA CACHE DE POSES, Y LA CLAVE LLEVA EL NOMBRE.
    ''' <para>Estaba indexada SOLO por frame, y `HkxAnimationPlayer` tenia ENCIMA una segunda cache
    ''' sobre la misma llamada. El setter de `PoseName` limpiaba la del player "porque las poses
    ''' cacheadas llevan el nombre viejo" — y esta seguia devolviendo la vieja. Camino vivo:
    ''' `HkxPoseImport_Form.vb:319` setea `PoseName` y a la linea siguiente pide `PoseForFrame`.</para>
    ''' </summary>
    Private ReadOnly _previewPoseCache As New Dictionary(Of Integer, HkxPoseImportHelper.ImportResult)
    ''' <summary>El nombre con el que se cachearon las poses de arriba. ⛔ ACOTADA POR FRAMES: la
    ''' clave es el frame y el nombre se guarda aparte; al cambiar el nombre se TIRA la cache entera.
    ''' Meter el nombre en la clave la dejaba sin tope — el nombre sale de un TextBox, asi que cada
    ''' tecla sembraba una generacion entera de poses que no se liberaba en toda la sesion.</summary>
    Private _previewPoseNombre As String = Nothing

    Private Sub New(animation As HkxAnimacionDescomprimida_Class,
                    hkxSkeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
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
        ' ⛔ ACA, DESPUES de `_animation = animation`. Si este bloque sube arriba de esa linea, `_animation` es
        ' Nothing, el guard devuelve bh = 0 y `_esAditivo` queda valiendo `additiveHint`, que en Wardrobe Manager
        ' es SIEMPRE False (HkxPoseImport_Form.vb llama Create sin ese argumento). Resultado: TODO clip bh=1/2 se
        ' procesaria como NORMAL. Compila, y los 4 gates de poses quedan VERDES; lo que se rompe es el render
        ' (RudderAndFlaps.hkx: el timon vuela ~423 u). Mismo perfil que el bug de ComposeTransforms.
        Dim bhCtor = If(_animation IsNot Nothing AndAlso _animation.Binding IsNot Nothing, _animation.Binding.BlendHint, 0)
        _esAditivo = (bhCtor = 1 OrElse bhCtor = 2) OrElse additiveHint
        _esAditivoActual = (bhCtor = 2)
    End Sub

    Public ReadOnly Property FrameCount As Integer
        Get
            Return If(_animation Is Nothing, 0, _animation.NumFrames)
        End Get
    End Property

    Public ReadOnly Property TrackCount As Integer
        Get
            Return If(_animation Is Nothing, 0, _animation.Animacion.NumberOfTransformTracks)
        End Get
    End Property

    Public ReadOnly Property FrameDuration As Single
        Get
            Return If(_animation Is Nothing, 0.0F, _animation.FrameDuration)
        End Get
    End Property

    ''' <summary>Duracion DECLARADA del clip, en segundos.
    ''' <para>⛔ NO es (FrameCount-1) x FrameDuration: son dos Single autorados por SEPARADO y no siempre
    ''' coinciden. Medido sobre 15.713 animaciones de FO4: 14.628 cumplen Duration ~ (N-1)*fd y 1.085
    ''' cumplen Duration ~ N*fd. El crop del hkbClipGenerator esta definido contra ESTE valor, asi que el
    ''' rango reproducible se deriva de aca y no del conteo de frames.</para></summary>
    Public ReadOnly Property Duration As Single
        Get
            Return If(_animation Is Nothing, 0.0F, _animation.Animacion.Duration)
        End Get
    End Property

    Public ReadOnly Property SkeletonSource As String
        Get
            Return _baseDiagnostics.SkeletonSource
        End Get
    End Property

    Public ReadOnly Property Diagnostics As HkxPoseImportDiagnostics
        Get
            Return CloneDiagnostics(_baseDiagnostics)
        End Get
    End Property

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
        ' HkxAnimacionDescomprimida_Class (TRS por frame+track) → el resto del pipeline es idéntico.
        Dim animation = animationGraph.Animaciones().FirstOrDefault()
        If animation Is Nothing OrElse animation.NumFrames <= 0 OrElse animation.Animacion.NumberOfTransformTracks <= 0 Then
            Throw New InvalidDataException("Animation HKX does not contain a readable hkaSplineCompressedAnimation or hkaLosslessCompressedAnimation.")
        End If

        Logger.LogLazy(Function() $"[HKX-POSE] Animation parsed frames={animation.NumFrames} tracks={animation.Animacion.NumberOfTransformTracks} duration={animation.Animacion.Duration:0.######} frameDuration={animation.FrameDuration:0.######} bindingTracks={If(animation.Binding?.TransformTrackToBoneIndices?.Count, 0)} trackNames={animation.TrackNames.Count}")

        Dim hkxSkeleton As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing
        Dim skeletonSource = "none"
        Dim embeddedSkeletonAvailable = False
        Dim externalSkeletonAvailable = False

        Dim embeddedSkeleton = animationGraph.EsqueletoDeAnimacion()
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
            ' El skeleton.hkx trae el esqueleto de ANIMACIÓN + uno de RAGDOLL; el binding se autoriza
            ' contra el de animación ⇒ la ley del grafo, no un FirstOrDefault (puede dar el ragdoll).
            Dim externalSkeleton = skeletonGraph.EsqueletoDeAnimacion()
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
            .Tracks = animation.Animacion.NumberOfTransformTracks
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
            If Not String.Equals(_previewPoseNombre, poseName, StringComparison.Ordinal) Then
                _previewPoseCache.Clear()
                _previewPoseNombre = poseName
            End If
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
        Dim skippedNoContent = 0
        Dim droppedIdentity = 0
        Dim skippedMissingLiveBones = 0
        ' ── ADITIVOS: los tracks son DELTAS cerca de identidad, van DIRECTO a la capa Δ (componentes
        ' sin dato = identidad — ver BuildFrameLocalTransform).
        ' NORMALES: Δ = inv(S) × frameLocal, donde frameLocal toma del clip SOLO los componentes CON
        ' CONTENIDO y conserva S en los demás.
        '
        ' ES aditivo ⟺ (a) blendHint ∈ {1,2} del binding del archivo, O (b) el behavior graph lo declara
        ' vía `_additiveHint` (clip envuelto en DynamicAnimationTaggingGenerator 'Additive*', ej.
        ' AdditiveDynamicIdle con blendHint=0). Enum `hkaAnimationBinding::BlendHint`, leído de la
        ' reflexión del binario (.rdata 0x14263e428, build 2026-08-18): NORMAL=0 · ADDITIVE_DEPRECATED=1 ·
        ' ADDITIVE=2. `hkbClipGenerator` (0x1414afedd) lo lee en [binding+0x50] y marca aditivo con
        ' `(bh−1) ≤ 1` unsigned ⇒ SOLO 1 y 2 (bh=0 y bh≥3 no).
        '
        ' ⛔ CORRECCIÓN de la nota vieja, que decía «el motor COLAPSA 1 y 2 ⇒ procesamiento idéntico».
        ' Es FALSO. Colapsa sólo en el CLIP GENERATOR, que guarda un booleano «es aditivo». El BLEND los
        ' SEPARA: `hkbBlenderGeneratorUtils` (fn 0x1414c773d) lleva las dos clases en bits distintos del
        ' pose-header y ABORTA si se mezclan — cadena literal del binario: "Trying to mix deprecated and
        ' current additive animations in blend, this is not supported. Older assets must be re-exported."
        ' (hkbblendergeneratorutils.cpp; gemela en hkblayergeneratorutils.cpp, 0x141a5904d).
        ' ⛔ UNA ley, UN lugar: los dos se resuelven en el constructor y aca solo se leen. Antes se
        ' recalculaban aca, y BuildUnboundBoneWmData / BuildUnboundBoneSamData NO los calculaban en
        ' absoluto: por eso las dos emitian con semantica de clip NORMAL sobre clips aditivos.
        ' ORDEN de composicion del aditivo: lo DECLARA el binding; no se infiere en runtime ni se vota.
        ' Ver la ley, su DOMINIO y su evidencia en el sitio de uso, mas abajo.
        Dim additive = _esAditivo
        Dim additiveCurrent = _esAditivoActual

        For Each resolved In _tracks
            Dim hkxTransform = _animation.GetTransform(usedFrame, resolved.TrackIndex)
            Dim hkxMask = _animation.GetMask(usedFrame, resolved.TrackIndex)
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
            ' El track no opina EN ESTE FRAME (mismo criterio que BuildFrameLocalTransform: la
            ' declaracion del bloque al que pertenece el frame, no la del bloque 0).
            Dim opinaAqui = hkxMask <> 0
            If Not additive AndAlso Not opinaAqui Then
                skippedNoContent += 1
                Continue For
            End If


            ' [NO-ANIM-SYNC — REGLA UNIVERSAL, = pose-writer del motor 0x1413995D0] Se aplica SIEMPRE, por-componente,
            ' según el mask del NIF (bits 16-19). Con mask=0 (hueso sin flag) BuildNoAnimSyncLocal es passthrough
            ' (honored ≡ frameLocal) ⇒ Δ = inv(S)∘frameLocal = EXACTO el base. Con mask≠0, mantiene la traslación/escala
            ' ESTRUCTURAL (S = OriginalLocaL∘Mount = socket ensamblado) y solo aplica la ROTACIÓN del clip por eje
            ' flagueado ⇒ chunk montado anima RÍGIDO rooteado en el socket. NO hay dos caminos: es la misma regla del
            ' motor (por-bit), mask=0 = sin lock. (Validado --animsynccheck: bone-len honored = bind; LForearm1 1.611u.)
            Dim frameLocal = BuildFrameLocalTransform(hkxTransform, hkxMask, resolved, additive, diagnostics)
            Dim naMask As Byte = If(resolved.LiveBone IsNot Nothing, resolved.LiveBone.NoAnimSyncMask, CByte(0))
            Dim delta As Transform_Class
            ' Los TRES caminos hacen lo MISMO: (1) armar el LOCAL ABSOLUTO del hueso en el frame,
            ' (2) re-basarlo a la skeleton viva ⇒ Δ = inv(S)∘absoluto (S = OriginalLocaL∘Mount).
            ' Difieren SOLO en cómo se arma el absoluto:
            '   • no-additive   : el clip YA es el absoluto (componentes no-animadas ← S).
            '   • additive bh=2 : S ∘ add   (el aditivo compone en el frame local del hueso).
            '   • additive RESTO: add ∘ S   (el aditivo entra como PADRE). Cubre bh=1 Y el caso
            '                     `_additiveHint` sin blendHint propio (bh=0, ej. AdditiveDynamicIdle;
            '                     también bh≥3). ⚠️ Para ese caso NO hay ley medida ni respaldo del motor:
            '                     con bh=0 el clip generator ni siquiera lo marca aditivo. Queda en add∘S
            '                     = el comportamiento previo, sin cambio, a propósito.
            '
            ' ⭐ LEY: bh=2 ⇒ S∘add · TODO LO DEMÁS ⇒ add∘S. Contraejemplo de cada lado:
            '   add∘S (bh=1): Meshes\Actors\Vertibird\Animations\ForwardGlideInjured.hkx, LeftPropeller
            '        f110 — la traslación autorada (|addT| = 748,081, rotación 160,19°) CANCELA el arco de
            '        su propia rotación sobre una palanca de 414 u con residuo 0,225 (0,03 %). Con S∘add el
            '        propeller se va 748 u.
            '   S∘add (bh=2): Meshes\Actors\Vertibird\Animations\RudderAndFlaps.hkx — sus 71 tracks declaran
            '        la traslación Identity y aun así add∘S corre el ORIGEN de Rudder/LeftElevator/
            '        RightElevator 403,66 / 423,85 / 423,85 u (frame 0 bien, frame 1 volando: reportado en
            '        la app por el usuario).
            '
            ' De dónde sale la DIRECCIÓN de la ley, para que nadie la re-litigue con el RE en la mano:
            '   • El RE prueba que el motor tiene EXACTAMENTE estos dos órdenes y elige uno POR CLIP —
            '     helper 0x141543d40, misma función con los operandos cruzados: modo A∘B (out.T = A.T +
            '     rot(A.R, B.T), out.R = A.R·B.R) vs modo B∘A. A = pose acumulada, B = hijo aditivo.
            '   • El RE **NO** prueba cuál de los dos bits de clase corresponde a cuál blendHint. Eso NO se
            '     infirió: se MIDIÓ, con dos pruebas de imposibilidad de una sola dirección:
            '       – un track con la traslación declarada Identity y palanca no puede ser add∘S-autorado
            '         (autorar así con rotación EXIGE traslación compensatoria) ⇒ es S∘add;
            '       – una traslación que coincide con el arco de su propia rotación (error EXACTAMENTE 0 en
            '         decenas de tracks) es la firma de add∘S.
            '     Resultado: 316 determinaciones sobre 1.065 aditivas MEDIDAS, CERO contradicciones, cero
            '     archivos que activen las dos pruebas. Las ~749 restantes NO fueron determinadas: heredan
            '     la ley por el blendHint DECLARADO, que es justamente el punto del diseño.
            '
            ' Alcance medido (corpus completo del load order, sin filtro de carpeta):
            '   FO4 17.513 .hkx · bh1 108 · bh2 941. SSE 7.704 .hkx · bh1 21 · bh2 0 ⇒ el cambio es no-op
            '   para Skyrim EN EL CORPUS INSTALADO (es propiedad del corpus, no del código: un mod con bh=2
            '   recibiría una ley cuya evidencia es 100 % de FO4 — para SSE bh=2 la muestra es n=0).
            '   Total aditivas 1.070; 1.065 medidas (5 de FO4 sin esqueleto resoluble, nunca evaluadas).
            '   La SALIDA cambia para los ~941 bh=2; de esos, 443 tienen al menos un track «afectado» en el
            '   sentido estrecho (traslación declarada Identity y origen corrido > 1 u).
            '   29 archivos bh=2 (todos Character\_1stPerson\WPN*Add) miden mejor con el orden viejo; peor
            '   caso 2,096 u sobre COM (WPNAfterJiggleSneakDown f5). Se aceptan: son aditivos de viewmodel
            '   que el juego nunca muestra aislados. ⚠️ Ocultos POR DEFECTO en el selector de NPC Manager
            '   (BehaviorClipEnumerator, Is1stPersonOnly), pero el picker de Wardrobe Manager NO filtra
            '   carpeta (HkxPoseImport_Form: GetFilteredKeys("Meshes\", …)) ⇒ ahí están a la vista.
            '
            ' ⛔ NO-ANIM-SYNC: el mask se aplica al DELTA (structural = identidad), NO al absoluto. Se deja
            ' así a propósito, y con este alcance exacto:
            '   • Censo de los NIF vanilla replicando PlumbNoAnimSyncMasks (FO4 5.513 NIF / 75.161 nodos;
            '     SSE 6.152 / 44.905): las únicas máscaras que existen son 0, 7=X|Y|Z, 8=S y 15=X|Y|Z|S.
            '     CERO parciales ⇒ los bits de traslación vienen los tres juntos o ninguno.
            '   • Con máscara de traslación completa `honored.T = 0` ⇒ S∘honored da out.T = S.T EXACTO, que
            '     es lo que hace el pose-writer del motor (mantiene la traslación estructural del eje
            '     flagueado). Esto vale SEA CUAL SEA el orden del blend, así que para los chunks de robot el
            '     cambio es correcto con independencia de la ley del blendHint: el orden viejo entregaba
            '     rot(add.R, S.T), que está mal bajo cualquier orden.
            '   • Medición: 12.997.836 comparaciones pre-mask (app) vs post-mask (motor), mount con
            '     rotación. ⚠️ Las filas de mask=0 y mask=8 son CIRCULARES para el origen (la referencia usa
            '     la composición post, y mask=8 no toca la traslación: BuildNoAnimSyncLocal la decide con
            '     fx/fy/fz y `fs` sólo elige scaleSrc). No circulares en las TRES componentes: mask=7 y
            '     mask=15 ⇒ 1/3 de las comparaciones = 4.332.612 (el probe recorre las 6 máscaras una vez
            '     por (track, mount, frame) ⇒ 2.166.306 por máscara); los dos controles parciales son no
            '     circulares sólo en su eje. En las no circulares el orden nuevo da 0,000000 contra el motor
            '     y el viejo hasta 693,57 u.
            '   • ⛔ LATENTE, no arreglado: para bh=1 CON máscara, add∘S sigue entregando rot(add.R, S.T)
            '     donde el pose-writer entregaría S.T ⇒ bh=1 + mask queda engine-infiel. Único bh=1 en
            '     carpeta de robot: Meshes\Actors\CreateABot\Animations\SentryBot\CrippledNoise.hkx, con
            '     add.R ≈ I (maxAddT 0,474, desplazamiento medido 0) ⇒ arco ≈ 0. ⚠️ OJO al grepear
            '     "CrippledNoise": hay TRES archivos distintos y NO comparten blendHint — medido:
            '     CreateABot\Animations\{Assaultron,Protectron}\CrippledNoise\CrippledNoise.hkx son bh=2 y
            '     sólo el de SentryBot es bh=1 (por eso BehaviorClipEnumerator dice "=2 en …/CrippledNoise/…"
            '     y acá dice bh=1: hablan de archivos distintos). Con máscaras parciales (inexistentes en
            '     vanilla) el pre-mask también divergiría. Los dos casos quedan documentados acá.
            Dim absoluteLocal As Transform_Class
            If additive Then
                Dim honored = BuildNoAnimSyncLocal(frameLocal, New Transform_Class(), naMask)
                If additiveCurrent Then
                    absoluteLocal = resolved.StructuralLocal.ComposeTransforms(honored)   ' bh=2 : S ∘ add
                Else
                    absoluteLocal = honored.ComposeTransforms(resolved.StructuralLocal)   ' resto: add ∘ S
                End If
            Else
                absoluteLocal = BuildNoAnimSyncLocal(frameLocal, resolved.StructuralLocal, naMask)
            End If
            delta = resolved.StructuralLocalInverse.ComposeTransforms(absoluteLocal)
            If collectDiagnostics Then TrackDeltaDiagnostics(delta, diagnostics)

            Dim poseData = ToPoseTransformData(delta)
            If poseData.Isidentity = False Then
                pose.Transforms(resolved.BoneName) = poseData
            Else
                droppedIdentity += 1
            End If
        Next

        If collectDiagnostics Then
            diagnostics.ImportedBones = pose.Transforms.Count
            diagnostics.SkippedMissingLiveBones = skippedMissingLiveBones
            diagnostics.SkippedInvalidBindings = skippedInvalidBindings
            If diagnostics.MaxDeltaTranslation > 300.0F Then diagnostics.Warning = $"Large translation delta detected ({diagnostics.MaxDeltaTranslation:0.###}). Check skeleton/animation match."

            Logger.LogLazy(Function() $"[HKX-POSE] BuildPose result pose='{pose.Name}' usedFrame={usedFrame} imported={diagnostics.ImportedBones} missingLive={diagnostics.SkippedMissingLiveBones} invalid={diagnostics.SkippedInvalidBindings} noContent={skippedNoContent} identity={droppedIdentity} refT={diagnostics.TranslationComponentsFromReferencePose} refR={diagnostics.RotationComponentsFromReferencePose} refS={diagnostics.ScaleComponentsFromReferencePose} maxDeltaT={diagnostics.MaxDeltaTranslation:0.###} maxDeltaR={diagnostics.MaxDeltaRotationDegrees:0.###} warning='{diagnostics.Warning}'")
        Else
            Logger.LogLazy(Function() $"[HKX-POSE] BuildPose result pose='{pose.Name}' usedFrame={usedFrame} imported={pose.Transforms.Count} missingLive={skippedMissingLiveBones} invalid={skippedInvalidBindings} noContent={skippedNoContent} identity={droppedIdentity} tracks={_tracks.Count}")
        End If

        Dim result = New HkxPoseImportHelper.ImportResult With {
            .Pose = pose,
            .AnimationTrackCount = _animation.Animacion.NumberOfTransformTracks,
            .ImportedBoneCount = pose.Transforms.Count,
            .SkippedMissingLiveBoneCount = skippedMissingLiveBones,
            .SkippedInvalidBindingCount = skippedInvalidBindings,
            .SkeletonName = _baseDiagnostics.SkeletonName,
            .SkeletonSource = _baseDiagnostics.SkeletonSource
        }

        If collectDiagnostics = False Then _previewPoseCache(usedFrame) = result
        Return result
    End Function

    ''' <summary>SAM (ScreenArcher) transforms for the bones the HKX animation defines but the LIVE
    ''' NIF skeleton does NOT have (LiveBone Is Nothing). Lets the SAM export carry those bones for
    ''' portability (applied later when the pose is loaded onto a skeleton that HAS them). Built from
    ''' the HKX rig directly (frame transform + ReferencePose fallback), independent of the live
    ''' skeleton. Keyed by bone name.</summary>
    Public Function BuildUnboundBoneSamData(frameIndex As Integer) As Dictionary(Of String, PoseTransformData)
        Dim usedFrame = Math.Max(0, Math.Min(frameIndex, _animation.NumFrames - 1))
        Dim result As New Dictionary(Of String, PoseTransformData)(StringComparer.OrdinalIgnoreCase)

        For Each resolved In _tracks
            Try
                ' Only the MISSING live bones (LiveBone Is Nothing). Need a name and a rig rest pose
                ' (refPose) to build a sensible absolute local — skip otherwise.
                If resolved Is Nothing OrElse resolved.LiveBone IsNot Nothing Then Continue For
                If String.IsNullOrWhiteSpace(resolved.BoneName) Then Continue For
                If resolved.ReferencePose Is Nothing Then Continue For

                Dim ht = _animation.GetTransform(usedFrame, resolved.TrackIndex)
                Dim htMask = _animation.GetMask(usedFrame, resolved.TrackIndex)
                If ht Is Nothing Then Continue For

                ' Absolute LOCAL transform of the bone at this frame.
                ' ⛔ La ley la DECLARA el blendHint, igual que en BuildPose y en BuildUnboundBoneWmData.
                ' `BuildUnboundFrameLocal` rellena las componentes NO animadas con el refPose — semantica de
                ' clip NORMAL. Sobre un clip ADITIVO eso mezcla un delta (~0 en traslacion) con absolutos del
                ' rest: el hueso se va al ORIGEN en los ejes animados y la rotacion pierde R_rest.
                ' Contraejemplo: RudderAndFlaps.hkx (bh=2), hueso Rudder, abierto desde el picker de Wardrobe
                ' Manager (que NO filtra carpeta) con "Save SAM" tildado — el JSON salia con el timon en el
                ' origen. Era un bug VIVO, no un riesgo introducido por este cambio.
                ' ⛔ Aca se emite el ABSOLUTO, no el delta: esa es la diferencia con la gemela de formato WM,
                ' y por eso NO se puede copiar su expresion (le falta el inv(s)). Ver el remarks de mas abajo.
                ' ⛔ BuildNoAnimSyncLocal no se aplica, igual que en la gemela: el filtro de arriba es
                ' `LiveBone Is Nothing`, o sea que no hay NoAnimSyncMask que leer y seria passthrough.
                Dim tr As Transform_Class
                If _esAditivo Then
                    Dim sRest = HkxTransformConventionHelper.ToTransform(resolved.ReferencePose)
                    Dim add = LocalDelFrame(ht, htMask, _neutroAditivo)
                    If _esAditivoActual Then
                        tr = sRest.ComposeTransforms(add)          ' bh=2 : S o add
                    Else
                        tr = add.ComposeTransforms(sRest)          ' resto: add o S
                    End If
                Else
                    tr = LocalDelFrame(ht, htMask, resolved.ReferencePose)
                End If

                ' Escala COMPLETA: uniforme + per-eje. `BuildUnboundFrameLocal` ya NO promedia (usa
                ' `ResolveScaleVector`), así que el per-eje del clip llega hasta acá y hay que copiarlo
                ' o se pierde. Este triple Yaw/Pitch/Roll es EULER —lo decodifica la rama ScreenArcher
                ' de `Transform_Class.New(pd, Source)`—, a diferencia del de `ToPoseTransformData`, que
                ' es eje·ángulo. Ver su remarks: NO se pueden unificar.
                Dim nuevo As New PoseTransformData With {
                    .X = tr.Translation.X,
                    .Y = tr.Translation.Y,
                    .Z = tr.Translation.Z,
                    .Scale = tr.Scale,
                    .ScaleX = tr.ScaleVector.X,
                    .ScaleY = tr.ScaleVector.Y,
                    .ScaleZ = tr.ScaleVector.Z
                }
                Dim degs = Transform_Class.Matrix33ToEulerXYZ(tr.Rotation)
                nuevo.Yaw = degs.X
                nuevo.Pitch = degs.Y
                nuevo.Roll = degs.Z

                If Not result.ContainsKey(resolved.BoneName) Then result.Add(resolved.BoneName, nuevo)
            Catch ex As Exception
                Logger.LogLazy(Function() $"[HKX-POSE] BuildUnboundBoneSamData skip track={If(resolved Is Nothing, -1, resolved.TrackIndex)} bone='{If(resolved Is Nothing, "", resolved.BoneName)}': {ex.Message}")
            End Try
        Next

        Return result
    End Function

    ''' <summary>
    ''' ⛔⛔ LOS SIETE BITS DE LA MASCARA DE TRACK, EN UN SOLO LUGAR.
    ''' <para>Salen del bloque de mascaras del blob de spline (4 bytes por track — `lea rdi, [rdi+4]`
    ''' SIN condicion en `SkyrimSE.exe` 0x140C4C9ED). El bit dice si ESE componente viene en el frame;
    ''' si no viene, lo pone el llamador.</para>
    ''' <para>Estaban escritos como literales <c>1/2/4/8/16/32/64</c> en TRES funciones distintas de
    ''' este archivo, veintiuna veces.</para>
    ''' </summary>
    <Flags>
    Public Enum BitDeTrack
        TX = 1
        TY = 2
        TZ = 4
        Rotacion = 8
        SX = 16
        SY = 32
        SZ = 64
    End Enum

    ''' <summary>
    ''' ⛔ EL SELECTOR POR COMPONENTE, UNA SOLA VEZ. El local ABSOLUTO de un track en un frame: el
    ''' `hkQsTransform` son 12 floats con traslacion 0..2, rotacion 4..7 y escala 8..10, y de cada
    ''' componente se toma el del frame si el bit de <see cref="BitDeTrack"/> lo declara, y si no el
    ''' del arreglo <paramref name="falta"/>.
    ''' <para>Los dos lados son el MISMO `hkQsTransform` de doce floats — el del frame y el del
    ''' faltante — asi que se leen por el mismo indice y no hay conversion en el medio.</para>
    ''' <para>Lo unico que distingue a los consumidores es QUE arreglo pasan como faltante: el
    ''' refPose del rig, o el neutro aditivo. Lo comparten el camino SAM (absoluto) y el de delta de
    ''' Wardrobe Manager, para que los dos salgan de la MISMA cuenta. Antes eran dos funciones de
    ''' doce lineas cada una.</para>
    ''' </summary>
    Public Shared Function LocalDelFrame(qs As Single(), msk As Integer, falta As Single()) As Transform_Class
        ' ⛔ EL NEUTRO DE UN `hkQsTransform` NO ES CERO. Aca habia `New Single(11) {}`, que da
        ' rotacion (0,0,0,0) y escala (0,0,0): un cuaternion de magnitud cero produce NaN al construir
        ' la matriz, y una escala cero colapsa el subarbol del hueso al origen. El neutro es
        ' T=(0,0,0), R=(0,0,0,1), S=(1,1,1) — las mismas constantes que el motor guarda en
        ' 0x142F3C730 (`[0,0,0,1]`) y 0x142F3C560 (`[1,1,1,1]`).
        ' El privado, sin clonar: este es el camino CALIENTE (por hueso y por frame) y aca no sale
        ' de la clase. `NeutroAditivo()` clona para los de afuera.
        Dim rp = If(falta IsNot Nothing AndAlso falta.Length >= 12, falta, _neutroAditivo)
        Dim f = If(qs IsNot Nothing AndAlso qs.Length >= 12, qs, rp)
        Dim tx = If((msk And BitDeTrack.TX) <> 0, f(0), rp(0))
        Dim ty = If((msk And BitDeTrack.TY) <> 0, f(1), rp(1))
        Dim tz = If((msk And BitDeTrack.TZ) <> 0, f(2), rp(2))
        Dim sx = If((msk And BitDeTrack.SX) <> 0, f(8), rp(8))
        Dim sy = If((msk And BitDeTrack.SY) <> 0, f(9), rp(9))
        Dim sz = If((msk And BitDeTrack.SZ) <> 0, f(10), rp(10))
        Dim r = If((msk And BitDeTrack.Rotacion) <> 0, f, rp)
        Return HkxTransformConventionHelper.ToTransformRaw(tx, ty, tz, r(4), r(5), r(6), r(7), sx, sy, sz)
    End Function

    ''' <summary>WM-format DELTA transforms for the bones the HKX animation defines but the LIVE NIF
    ''' skeleton does NOT have (LiveBone Is Nothing). Mirrors <see cref="BuildUnboundBoneSamData"/>'s
    ''' track filtering, but encodes the WM delta (inv(rigRest) × frameLocal → Matrix33ToBSRotation via
    ''' ToPoseTransformData) instead of the SAM absolute local. Used by the EXPORT/save path only to
    ''' append the missing bones — NOT by BuildPose (per-frame playback). Identity deltas are skipped,
    ''' same criterion as BuildPose. Keyed by bone name; never Nothing.
    ''' <para>⛔ NO RAMIFICA POR ADITIVIDAD, y BuildPose SÍ (desde el fix del orden aditivo: bh=2 ⇒
    ''' S∘add). Consecuencia VERIFICABLE, no una magnitud estimada: en un mismo &lt;Pose&gt; del XML los
    ''' huesos LIGADOS salen con la ley del blendHint y los NO LIGADOS con <c>inv(refPose)∘frameLocal</c>,
    ''' que rellena las componentes no animadas con el refPose — semántica de clip NORMAL, no de delta
    ''' aditivo. El archivo queda internamente ASIMÉTRICO para clips bh=2. Hueco PREEXISTENTE: el fix no
    ''' lo introduce. Arreglarlo = pasarle <c>additive</c>/<c>additiveCurrent</c>; fuera del alcance de
    ''' ese cambio a propósito.</para></summary>
    Public Function BuildUnboundBoneWmData(frameIndex As Integer) As Dictionary(Of String, PoseTransformData)
        Dim usedFrame = Math.Max(0, Math.Min(frameIndex, _animation.NumFrames - 1))
        Dim result As New Dictionary(Of String, PoseTransformData)(StringComparer.OrdinalIgnoreCase)

        For Each resolved In _tracks
            Try
                ' Only the MISSING live bones (LiveBone Is Nothing), with a name and a rig rest pose.
                If resolved Is Nothing OrElse resolved.LiveBone IsNot Nothing Then Continue For
                If String.IsNullOrWhiteSpace(resolved.BoneName) Then Continue For
                If resolved.ReferencePose Is Nothing Then Continue For

                Dim ht = _animation.GetTransform(usedFrame, resolved.TrackIndex)
                Dim htMask = _animation.GetMask(usedFrame, resolved.TrackIndex)
                If ht Is Nothing Then Continue For

                Dim s = HkxTransformConventionHelper.ToTransform(resolved.ReferencePose)   ' structural proxy = HKX rig rest
                ' MISMA ley que BuildPose (el orden lo declara el blendHint), con el round-trip
                ' inv(S)o(So add) algebraicamente CANCELADO: no es la misma expresion, es la misma ley.
                ' ⛔ Para bh=2 el delta ES el aditivo, exacto. Componer y descomponer seria un round-trip
                ' que solo agrega ruido, y PoseTransformData.Isidentity compara SIN epsilon
                ' (PoseClasses.vb:151), o sea que ese ruido DECIDE si el hueso entra al XML. Medido sobre
                ' 20.000 S sinteticos con add = identidad: sin round-trip fallan Isidentity 5.015/20.000;
                ' con round-trip 7.189/20.000 (+43 % de entradas espurias).
                ' ⛔ BuildNoAnimSyncLocal NO se aplica aca, y esta bien: el filtro de arriba es
                ' `LiveBone Is Nothing`, o sea que no hay NoAnimSyncMask que leer (se lee de resolved.LiveBone)
                ' y seria passthrough. Si manana cambia ese filtro, la asimetria vuelve callada.
                Dim delta As Transform_Class
                If _esAditivo Then
                    Dim add = LocalDelFrame(ht, htMask, _neutroAditivo)
                    If _esAditivoActual Then
                        delta = add
                    Else
                        delta = s.Inverse().ComposeTransforms(add.ComposeTransforms(s))
                    End If
                Else
                    delta = s.Inverse().ComposeTransforms(LocalDelFrame(ht, htMask, resolved.ReferencePose))
                End If
                Dim poseData = ToPoseTransformData(delta)              ' reuse → Matrix33ToBSRotation
                If Not poseData.Isidentity AndAlso Not result.ContainsKey(resolved.BoneName) Then result.Add(resolved.BoneName, poseData)
            Catch ex As Exception
                Logger.LogLazy(Function() $"[HKX-POSE] BuildUnboundBoneWmData skip track={If(resolved Is Nothing, -1, resolved.TrackIndex)} bone='{If(resolved Is Nothing, "", resolved.BoneName)}': {ex.Message}")
            End Try
        Next

        Return result
    End Function

    ''' <summary>
    ''' ⛔⛔ LOS BITS DE NO-ANIM-SYNC DEL `NiNode`, EN UN SOLO LUGAR. Son los bits 16..19 de
    ''' `NiNode.flags`, y dicen que componente del hueso NO sigue a la animacion.
    ''' <para>Estaban escritos como literales `&amp;H10000UI`..`&amp;H80000UI` en CINCO sitios distintos
    ''' (`NpcMountingResolver`, tres bloques de `FO4_FaceTint_CLI` y `FaceGenBuilder`), ninguno con
    ''' una constante con nombre.</para>
    ''' </summary>
    <Flags>
    Public Enum BitDeNoAnimSync As UInteger
        X = &H10000UI
        Y = &H20000UI
        Z = &H40000UI
        S = &H80000UI
    End Enum

    ''' <summary>La mascara compacta que consume <see cref="BuildNoAnimSyncLocal"/> (bits 1/2/4/8),
    ''' sacada de los bits 16..19 de `NiNode.flags`.</summary>
    Public Shared Function MascaraNoAnimSync(flags As UInteger) As Byte
        Dim m As Byte = 0
        If (flags And BitDeNoAnimSync.X) <> 0UI Then m = CByte(m Or 1)
        If (flags And BitDeNoAnimSync.Y) <> 0UI Then m = CByte(m Or 2)
        If (flags And BitDeNoAnimSync.Z) <> 0UI Then m = CByte(m Or 4)
        If (flags And BitDeNoAnimSync.S) <> 0UI Then m = CByte(m Or 8)
        Return m
    End Function

    ''' <summary>[NO-ANIM-SYNC] Local que produce el motor para un hueso con flag No Anim Sync: ROTACIÓN del clip
    ''' (siempre) + traslación/escala de <paramref name="structural"/> (S) para las componentes flagueadas
    ''' (mask bit0=X, 1=Y, 2=Z, 3=S), o del clip para las no flagueadas. = pose-writer 0x1413995D0 (mantiene
    ''' [out+0x30/34/38] existente por eje flagueado; escala en [out+0x3c]).</summary>
    Public Shared Function BuildNoAnimSyncLocal(clipLocal As Transform_Class, structural As Transform_Class, mask As Byte) As Transform_Class
        Dim ct = clipLocal.Translation, st = structural.Translation
        Dim fx = (mask And 1) <> 0, fy = (mask And 2) <> 0, fz = (mask And 4) <> 0, fs = (mask And 8) <> 0
        Dim scaleSrc = If(fs, structural, clipLocal)
        Return New Transform_Class With {
            .Rotation = clipLocal.Rotation,
            .Scale = scaleSrc.Scale,
            .ScaleVector = scaleSrc.ScaleVector,
            .Translation = New Vector3(If(fx, st.X, ct.X), If(fy, st.Y, ct.Y), If(fz, st.Z, ct.Z))
        }
    End Function

    ''' <summary>El neutro ADITIVO como `hkQsTransform`: cero en traslacion, identidad en rotacion
    ''' (0,0,0,1 en los slots 4..7) y uno en escala.
    ''' <para>⛔ UNA COPIA POR LLAMADA, NO EL ARREGLO COMPARTIDO. Estuvo como
    ''' `Public Shared ReadOnly ... As Single()`, y el `ReadOnly` de VB protege la REFERENCIA, no
    ''' los doce floats: un `NeutroAditivo(7) = 0.0F` en cualquier herramienta convertiria el
    ''' neutro en un cuaternion nulo y TODA pose aditiva saldria con rotacion identidad por la
    ''' guarda de `ToTransformRaw`. Los seis consumidores de hoy solo leen; el arreglo publico
    ''' compartido por todos los huesos, todos los frames y todos los hilos es la exposicion.</para>
    ''' </summary>
    Public Shared Function NeutroAditivo() As Single()
        Return CType(_neutroAditivo.Clone(), Single())
    End Function

    Private Shared ReadOnly _neutroAditivo As Single() =
        {0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 1.0F, 1.0F, 1.0F, 1.0F, 0.0F}



    ''' <summary>Local del frame del clip.
    ''' <para>NORMAL: componente CON CONTENIDO ← clip; SIN OPINIÓN ← S (local estructural vivo) ⇒
    ''' Δ=0 en esos ejes. Con la distribución del mount corregida (CHUNK-TREE-FULL: la corrección
    ''' vive en sockets/ramas como Bethesda, no en los huesos skinneados profundos), S coincide con
    ''' la base del clip donde el clip tiene contenido — sin doble conteo. Mezclar componentes
    ''' clip/S es válido: la convención HKX→render es trivial (quat xyzw directo).</para>
    ''' <para>ADITIVO: componente sin dato = delta CERO.</para></summary>
    Private Shared Function BuildFrameLocalTransform(qs As Single(), msk As Integer,
                                                     resolved As ResolvedTrack,
                                                     additive As Boolean,
                                                     diagnostics As HkxPoseImportDiagnostics) As Transform_Class
        ' ⛔ EL PRIVADO: este es el camino CALIENTE (`BuildPose` lo llama por hueso y por frame) y
        ' `NeutroAditivo()` CLONA. El publico clona para los consumidores de afuera, que no pueden
        ' recibir el arreglo compartido; adentro no hace falta y la allocacion se nota.
        If additive Then Return LocalDelFrame(qs, msk, _neutroAditivo)
        Dim f = If(qs IsNot Nothing AndAlso qs.Length >= 12, qs, New Single(11) {})

        ' ⛔ LOS FLAGS SALEN DE ESTE FRAME, no de una clasificacion hecha una vez sobre el frame 0.
        ' La mascara Identity/StaticValue/SplineValue es POR BLOQUE: el parser la reescribe en cada
        ' bloque, asi que la del bloque 0 NO es la del frame que se esta posando. Medido en FO4 vanilla:
        ' 16 de 744 animaciones son multi-bloque y 10 tracks cambian de mascara entre bloques. En SSE es
        ' mucho peor -- maxFPB = 32, o sea que casi todo clip de mas de 32 frames es multi-bloque.
        ' Ademas era la MISMA pregunta contestada de dos formas dentro de esta funcion: la rama aditiva
        ' de arriba y `BuildUnboundFrameLocal` ya leian `hkxTransform.*Animated`. Ahora las tres coinciden.
        Dim s = resolved.StructuralLocal
        Dim cTX = (msk And BitDeTrack.TX) <> 0
        Dim cTY = (msk And BitDeTrack.TY) <> 0
        Dim cTZ = (msk And BitDeTrack.TZ) <> 0
        Dim cSX = (msk And BitDeTrack.SX) <> 0
        Dim cSY = (msk And BitDeTrack.SY) <> 0
        Dim cSZ = (msk And BitDeTrack.SZ) <> 0
        Dim cR = (msk And BitDeTrack.Rotacion) <> 0
        Dim tx = If(cTX, f(0), s.Translation.X)
        Dim ty = If(cTY, f(1), s.Translation.Y)
        Dim tz = If(cTZ, f(2), s.Translation.Z)
        ' ⛔ `EffectiveScale`, NO `s.Scale`. Es la ley que la propia clase documenta: la escala real es
        ' `Scale · ScaleVector`, y desde que el HKX conserva el per-eje un local no uniforme lleva
        ' `Scale = 1` con todo el peso en `ScaleVector` ⇒ leer `s.Scale` devolvia 1.0 y el delta salia
        ' con el hueso achicado contra su bind (medido: 2,5 % en Z con un refPose per-eje).
        ' ⛔ Y NO `EscalaComoEscalar`: eso devuelve UN escalar (eff.X) y aca los tres fallbacks son
        ' INDEPENDIENTES, uno por eje — ponerle eff.X a los tres reintroduce el aplastamiento de ejes
        ' que este trabajo vino a sacar, con otro disfraz. Esa funcion es para destinos de un solo float.
        Dim se = s.EffectiveScale        ' una sola vez: esto corre por track y por frame durante el play
        Dim sx = If(cSX, f(8), se.X)
        Dim sy = If(cSY, f(9), se.Y)
        Dim sz = If(cSZ, f(10), se.Z)

        If diagnostics IsNot Nothing Then
            If Not cTX Then diagnostics.TranslationComponentsFromReferencePose += 1
            If Not cTY Then diagnostics.TranslationComponentsFromReferencePose += 1
            If Not cTZ Then diagnostics.TranslationComponentsFromReferencePose += 1
            If Not cR Then diagnostics.RotationComponentsFromReferencePose += 1
            If Not (cSX AndAlso cSY AndAlso cSZ) Then diagnostics.ScaleComponentsFromReferencePose += 1
        End If

        If cR Then
            Return HkxTransformConventionHelper.ToTransformRaw(tx, ty, tz, f(4), f(5), f(6), f(7), sx, sy, sz)
        End If
        ' Rotación sin opinión ← rotación estructural del hueso vivo.
        ' La escala va PER-EJE en `ScaleVector`, no promediada: es el mismo dato que el otro brazo del
        ' If (ToTransform) conserva, y si acá se promediara, la pose saldría distinta según si el clip
        ' anima la rotación o no — una divergencia invisible entre dos caminos del mismo Return.
        ' ⛔ LA ESCALA, COMO LA DECLARA EL ARCHIVO. `ResolveScaleVector` corregia cada eje con un
        ' umbral que el motor no tiene (ver `HkxTransformConventionHelper.ToTransformRaw`).
        Dim sv As New Vector3(sx, sy, sz)
        Return New Transform_Class With {
            .Translation = New Vector3(tx, ty, tz),
            .Rotation = s.Rotation,
            .Scale = 1.0F,
            .ScaleVector = New System.Numerics.Vector3(sv.X, sv.Y, sv.Z)
        }
    End Function

    Private Shared Function ResolveTracks(animation As HkxAnimacionDescomprimida_Class,
                                          hkxSkeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                          skeletonSource As String) As List(Of ResolvedTrack)
        Dim result As New List(Of ResolvedTrack)
        Dim binding = If(animation.Binding?.TransformTrackToBoneIndices, New List(Of Integer)())

        For trackIndex = 0 To animation.Animacion.NumberOfTransformTracks - 1
            Dim boneIndex = -1
            Dim boneName As String = ""

            If hkxSkeleton IsNot Nothing Then
                If binding IsNot Nothing AndAlso binding.Count > 0 AndAlso trackIndex < binding.Count Then
                    boneIndex = CInt(binding(trackIndex))
                    If boneIndex < 0 OrElse boneIndex >= hkxSkeleton.Bones.Count Then
                        Dim ti = trackIndex
                        Dim bi = boneIndex
                        Dim boneCount = hkxSkeleton.Bones.Count
                        Logger.LogLazy(Function() $"[HKX-POSE] skip mapping track={ti}: binding boneIndex={bi} outside skeleton bones={boneCount}.")
                        Continue For
                    End If
                Else
                    If trackIndex >= hkxSkeleton.Bones.Count Then
                        Dim ti = trackIndex
                        Dim boneCount = hkxSkeleton.Bones.Count
                        Logger.LogLazy(Function() $"[HKX-POSE] skip mapping track={ti}: no binding and track outside skeleton bones={boneCount}.")
                        Continue For
                    End If
                    boneIndex = trackIndex
                End If

                boneName = hkxSkeleton.Bones(boneIndex).Name
            ElseIf trackIndex < animation.TrackNames.Count Then
                boneName = animation.TrackNames(trackIndex)
            End If

            If String.IsNullOrWhiteSpace(boneName) Then
                Dim ti = trackIndex
                Logger.LogLazy(Function() $"[HKX-POSE] skip mapping track={ti}: empty bone name strategy={skeletonSource}.")
                Continue For
            End If

            result.Add(New ResolvedTrack With {.TrackIndex = trackIndex, .BoneIndex = boneIndex, .BoneName = boneName.Trim()})
        Next

        Return result
    End Function

    ''' <summary>Liga cada track al hueso del esqueleto vivo, captura su local ESTRUCTURAL
    ''' <c>S_b = O×Mount</c> y el refPose del rig del clip.
    ''' <para>MODELO: la animación es REEMPLAZO TOTAL del local en el frame del
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
                                              hkxSkeleton As Havok.Canon.Objects.HkObj_HkaSkeleton,
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

    Private Shared Function IsValidSkeleton(skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton) As Boolean
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

    ''' <remarks>⛔⛔ EL TRIPLE <c>Yaw/Pitch/Roll</c> DE ESTA SALIDA ES EJE·ÁNGULO, no Euler. Quien
    ''' decide cómo se decodifica es <c>Poses_class.Source</c>, en
    ''' <c>Transform_Class.New(PoseTransformData, Pose_Source_Enum)</c>: la rama WardrobeManager usa
    ''' <c>BSRotationToMatrix33</c> y la ScreenArcher usa <c>EulerXYZToMatrix33</c>. Por eso esta función
    ''' y <c>BuildUnboundBoneSamData</c> NO se pueden unificar aunque llenen los mismos campos: la de
    ''' allá produce Euler porque su pose sale con <c>Source = ScreenArcher</c>.
    ''' <para>⛔ Copiaba sólo <c>Scale</c>, y eso era una BOMBA: mientras el HKX venía promediado, un
    ''' delta siempre salía uniforme y la línea funcionaba de casualidad. Al conservar el HKX su escala
    ''' per-eje, <c>ComposeTransforms</c> pasa a emitir <c>Scale = 1</c> con todo en <c>ScaleVector</c>
    ''' ⇒ esta función escribía 1 y tiraba la escala ENTERA. Peor: un hueso cuyo único cambio fuera la
    ''' escala quedaba <c>Isidentity = True</c> y NI SIQUIERA ENTRABA a la pose. El remarks viejo ya lo
    ''' predecía y daba tres razones para no arreglarlo — las tres CAÍDAS: <c>Clone</c> ahora copia el
    ''' per-eje, el XML ahora lo escribe, y el clip ya no se promedia.</para>
    ''' <para>⚠️ CONSECUENCIA DECLARADA: al copiarlo, huesos que antes se descartaban por
    ''' <c>Isidentity</c> ahora ENTRAN a la pose, y <c>SaveImportedHkxPoseXml</c> escribe más
    ''' <c>&lt;Bone&gt;</c>. El dato es legítimo (es la escala real del clip) y BodySlide ignora los
    ''' atributos que no conoce, pero cambia cuántos huesos trae una pose.</para></remarks>
    Public Shared Function ToPoseTransformData(source As Transform_Class) As PoseTransformData
        Dim rot = Transform_Class.Matrix33ToBSRotation(source.Rotation)
        Return New PoseTransformData With {
            .X = source.Translation.X,
            .Y = source.Translation.Y,
            .Z = source.Translation.Z,
            .Yaw = rot.X,
            .Pitch = rot.Y,
            .Roll = rot.Z,
            .Scale = source.Scale,
            .ScaleX = source.ScaleVector.X,
            .ScaleY = source.ScaleVector.Y,
            .ScaleZ = source.ScaleVector.Z
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
        ''' <summary>Pose de referencia del hueso: los 12 floats crudos del `hkQsTransform`,
        ''' que es lo que entrega el objeto generado. La conversion a `Transform_Class` la hace
        ''' `HkxTransformConventionHelper.ToTransform`.</summary>
        Public Property ReferencePose As Single()
    End Class
End Class
