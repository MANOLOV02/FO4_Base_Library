Option Strict On
Option Explicit On

' =============================================================================
' Behavior graph (hkb*) — parsing ESTRUCTURAL (datos, NO el runtime de ejecución).
'
' FASE 1: el "pegamento" string-data — qué skeleton/ragdoll/behavior/animaciones usa
' cada actor + los nombres de eventos/variables del grafo. Es lo más valioso para
' resolver assets por actor.
'
' SIN referencia de layout (HavokLib NO trae clases hkb*): offsets verificados
' empíricamente con el modo --dump del HkxLoadOrderAudit sobre archivos reales del
' load order (AlienCharacter + BloatFlyCharacter, FO4 64-bit). Donde el offset exacto
' de un array no está confirmado, se extrae por contenido (honesto, sin inventar campos).
'
' hkbCharacterStringData (offsets escalares confirmados en 2 instancias):
'   +0x0A0 m_characterName    (StringPtr)
'   +0x0A8 m_rigName          (StringPtr)  → skeleton (ej. "CharacterAssets\skeleton.hkt")
'   +0x0B0 m_ragdollName      (StringPtr)  → ragdoll
'   +0x0B8 m_behaviorFilename (StringPtr)  → "Behaviors\...RootBehavior.hkx"
'   + arrays de string (deformableSkins, animationNames, etc.) en offsets variados.
' hkbBehaviorGraphStringData:
'   +0x010 m_eventNames       (hkArray<hkStringPtr>)  → eventos (FootLeft, Ragdoll, defaultState, ...)
'   +0x030 / +0x040 arrays de variables/attributes (nombre por posición, tentativo)
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Public Partial Class HkxObjectGraph_Class

    ''' <summary>hkbCharacterStringData: rig(skeleton)/ragdoll/behavior/nombre del actor + animaciones.</summary>
    Public Function ParseCharacterStringData(source As HkxVirtualObjectGraph_Class) As HkbCharacterStringDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbCharacterStringData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbCharacterStringData(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset

        Dim result As New HkbCharacterStringDataGraph_Class With {
            .SourceObject = source,
            .CharacterName = hkr.Name,
            .RigName = hkr.RigName,
            .RagdollName = hkr.RagdollName,
            .BehaviorFilename = hkr.BehaviorFilename
        }

        ' Lista de animaciones: por contenido (robusto sin depender del offset exacto del array).
        Dim allStrings = ReadAllReferencedStrings(source)
        result.AllStrings.AddRange(allStrings)
        result.AnimationFilenames.AddRange(
            allStrings.Where(Function(s) LooksLikeAnimationFile(s)).
                       Distinct(StringComparer.OrdinalIgnoreCase))
        Return result
    End Function

    ''' <summary>hkbBehaviorGraphStringData: nombres de eventos + variables/attributes del grafo.</summary>
    Public Function ParseBehaviorGraphStringData(source As HkxVirtualObjectGraph_Class) As HkbBehaviorGraphStringDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBehaviorGraphStringData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBehaviorGraphStringData(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset

        Dim result As New HkbBehaviorGraphStringDataGraph_Class With {.SourceObject = source}
        ' ⛔ LOS DOS ÚLTIMOS ESTABAN MAL, y el comentario viejo lo admitía: decía "por posición
        ' (tentativo)". La reflexión lo cierra: `hkbBehaviorGraphStringData` declara
        '   +0x10 eventNames · +0x20 attributeNames · +0x30 variableNames · +0x40 characterPropertyNames
        ' O sea que `AttributeNames` estaba leyendo +0x40, que es `characterPropertyNames`, y los
        ' nombres de atributo (+0x20) no los leía nadie. Adivinar por posición acertó uno de tres.
        result.EventNames.AddRange(ReadStringPtrArray(hkr.EventNames))
        result.VariableNames.AddRange(ReadStringPtrArray(hkr.VariableNames))
        result.AttributeNames.AddRange(ReadStringPtrArray(hkr.AttributeNames))
        Return result
    End Function

    ''' <summary>hkbProjectStringData: paths del proyecto (character files, animation/behavior roots).</summary>
    Public Function ParseProjectStringData(source As HkxVirtualObjectGraph_Class) As HkbProjectStringDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbProjectStringData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbProjectStringData(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbProjectStringDataGraph_Class With {.SourceObject = source}
        result.Strings.AddRange(ReadAllReferencedStrings(source).Distinct(StringComparer.OrdinalIgnoreCase))
        ' DATA-DRIVEN: los character files salen del MIEMBRO real m_characterFilenames (hkArray<hkStringPtr>), no de
        ' un match por nombre de carpeta. Layout hkbProjectStringData (hk_2014, estable FO4/SSE): base hkReferencedObject
        ' (0x10) + m_animationFilenames@+0x10 + m_behaviorFilenames@+0x20 + m_characterFilenames@+0x30. Leer el array
        ' directo evita el hardcodeo "Characters\" (que dejaba fuera la carpeta "Characters Female\" de la rama female SSE).
        result.CharacterFilenames.AddRange(
            ReadStringPtrArray(source.RelativeOffset + &H30).Where(Function(s) Not String.IsNullOrWhiteSpace(s)))
        Return result
    End Function

    ' Lee un hkArray<hkStringPtr> (cada elemento = puntero a string, stride = PointerSizeValue).
    ''' <summary>Strings del array, a partir de su CABECERA (lo que devuelve el lector generado).</summary>
    Private Function ReadStringPtrArray(header As HkxObjectArrayHeader_Class) As List(Of String)
        Dim result As New List(Of String)
        If header Is Nothing OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        For i = 0 To header.Count - 1
            result.Add(ResolveLocalString(header.DataRelativeOffset + (i * PointerSizeValue)))
        Next
        Return result
    End Function

    Private Function ReadStringPtrArray(fieldRelativeOffset As Integer) As List(Of String)
        Dim result As New List(Of String)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        For i = 0 To header.Count - 1
            result.Add(ResolveLocalString(header.DataRelativeOffset + (i * PointerSizeValue)))
        Next
        Return result
    End Function

    ' Todas las strings ASCII imprimibles referenciadas por local-fixups dentro del objeto.
    Private Function ReadAllReferencedStrings(source As HkxVirtualObjectGraph_Class) As List(Of String)
        Dim result As New List(Of String)
        For Each lf In GetLocalFixupsInRange(source.RelativeOffset, source.Size)
            Dim s = ReadNullTerminatedString(lf.DestinationRelativeOffset)
            If IsPrintableString(s) Then result.Add(s)
        Next
        Return result
    End Function

    Private Shared Function IsPrintableString(s As String) As Boolean
        If String.IsNullOrEmpty(s) OrElse s.Length > 256 Then Return False
        For Each c In s
            If AscW(c) < 32 OrElse AscW(c) > 126 Then Return False
        Next
        Return True
    End Function

    Private Shared Function LooksLikeAnimationFile(s As String) As Boolean
        If String.IsNullOrEmpty(s) Then Return False
        Dim lc = s.ToLowerInvariant()
        Return (lc.EndsWith(".hkt") OrElse lc.EndsWith(".hkx")) AndAlso lc.Contains("animation")
    End Function

    ' =====================================================================================
    ' LAYOUT hkb* POR VERSIÓN DE HAVOK  (ver HkbLayout_Class)
    ' -------------------------------------------------------------------------------------
    ' LEY: el layout hkb* NO es común a los dos juegos y el packfile SÍ los distingue — nunca
    ' aplicar un offset de un juego al otro. Censo sobre los .hkx no-clip vanilla:
    ' SSE 1763/1763 = fileVersion 8 / hk_2010.2.0-r1 → PackfileFormat.Skyrim64;
    ' FO4 1330/1330 = fileVersion 11 / hk_2014.1.0-r1 → PackfileFormat.Fallout64.
    '
    ' IDÉNTICOS en ambos (verificado campo a campo con dumps de fixups): hkbStateMachineStateInfo
    ' (144B; m_transitions@+0x50, m_generator@+0x58, m_name@+0x60, m_stateId@+0x68, enter/exit
    ' NotifyEvents@+0x40/+0x48), hkbStateMachineEventPropertyArray (@+0x10, stride 0x10),
    ' hkbCharacterStringData (@+0xA0/A8/B0/B8), hkbBehaviorGraphStringData (@+0x10/+0x30/+0x40),
    ' hkbProjectStringData (m_characterFilenames@+0x30), hkbBlenderGeneratorChild
    ' (m_weight@+0x40, m_worldFromModelWeight@+0x44), hkbClipTriggerArray/hkbVariableValueSet/
    ' hkbVariableBindingSet/hkbExpressionDataArray/hkbStringEventPayload (@+0x10) y hkbNode::m_name@+0x38.
    '
    ' DIFIEREN (lo que gatea HkbLayout_Class): hkbClipGenerator (bloque completo tras m_name),
    ' hkbStateMachine::m_states y hkbBehaviorReferenceGenerator::m_behaviorName.
    ' El stride de hkbStateMachineTransitionInfoArray NO difiere: es 0x48 en los dos (NO 0x40,
    ' que sólo acierta el elemento 0 — ver ParseTransitions).
    ' =====================================================================================

    ''' <summary>Offsets de los miembros hkb* que NO son iguales entre hk_2014 (Fallout 4) y
    ''' hk_2010 (Skyrim SE). Se resuelven del <c>PackfileFormat</c> del propio archivo.</summary>
    Friend Class HkbLayout_Class
        ' hkbClipGenerator
        Public ClipAnimationName As Integer
        Public ClipTriggers As Integer
        Public ClipCropStart As Integer
        Public ClipCropEnd As Integer
        Public ClipStartTime As Integer
        Public ClipPlaybackSpeed As Integer
        Public ClipEnforcedDuration As Integer
        Public ClipBindingIndex As Integer
        Public ClipMode As Integer
        Public ClipFlags As Integer
        ' hkbBehaviorReferenceGenerator / BGSGamebryoSequenceGenerator (string secundario del nodo)
        Public BehaviorRefName As Integer
        ' hkbBlendingTransitionEffect::m_duration (el resto del bloque es relativo a este)
        Public TransitionDuration As Integer
        ' wrappers
        Public ModGenModifier As Integer
        Public ModGenGenerator As Integer
        Public ModifierListArray As Integer
        Public ManualSelectorArray As Integer
        Public EvalExprData As Integer
        Public EventDrivenModifier As Integer
        Public StateTagGenerator As Integer
        Public SyncClipGenerator As Integer
        Public ModEnable As Integer
        Public IsActiveFlags As Integer
        Public CyclicBlender As Integer
        Public CyclicEvents As Integer
        Public CyclicEventCount As Integer
        Public CyclicBlendParam As Integer
        Public BoneSwitchDefault As Integer
        Public BoneSwitchChildren As Integer
        Public DynTagGenerator As Integer
        Public LayerGenerator As Integer
        Public LayerWeight As Integer
        Public TwistAxis As Integer
        Public TwistAngle As Integer
        Public DeactEventId As Integer
        Public DeactPayload As Integer
        Public AssignFloatPairs As Integer
        Public AssignIntPairs As Integer
        ' BSLookAtModifier: el struct de bone difiere ENTERO entre juegos, no solo el offset
        Public LookAtBones As Integer
        Public LookAtBoneStride As Integer
        Public LookAtIndex As Integer
        Public LookAtUpAxis As Integer
        Public LookAtLimitAngle As Integer
        Public LookAtOnGain As Integer
        Public LookAtOffGain As Integer
        ' hkbBehaviorGraph / hkbBehaviorGraphData (medidos; el orden de miembros difiere entre juegos)
        Public GraphRoot As Integer
        Public GraphData As Integer
        Public GraphDataInitial As Integer
        Public GraphDataStrings As Integer
        ' hkbStateMachine
        Public StateMachineStates As Integer
        ' hkbStateMachineTransitionInfoArray
        Public TransitionStride As Integer
        ''' <summary>False = formato sin layout medido (Skyrim LE 32-bit): los campos numéricos se
        ''' devuelven neutros en vez de basura leída con offsets de otro juego.</summary>
        Public Known As Boolean = True
    End Class

    Private _hkbLayout As HkbLayout_Class

    ''' <summary>Layout hkb* de ESTE packfile. Medido:
    ''' <list type="bullet">
    ''' <item>FO4 (hk_2014): clip m_animationName@+0x90, m_triggers@+0x98, playbackSpeed@+0xB0 (=1.0),
    ''' bindingIndex@+0xBC (=-1), mode@+0xBE, flags@+0xBF; SM m_states@+0xD0; behaviorRef@+0x88.</item>
    ''' <item>SSE (hk_2010): clip m_animationName@+0x48, m_triggers@+0x50, playbackSpeed@+0x64,
    ''' bindingIndex@+0x70, mode@+0x72, flags@+0x73; SM m_states@+0x90; behaviorRef@+0x48.</item>
    ''' </list>
    ''' playbackSpeed SSE verificado POR VALOR, no por posición: MT_TurnRight60Fast=2, MT_TurnRight60=1,
    ''' MT_TurnRight360Fast=1.5, MT_TurnRight360_Slow=0.5 (mt_behavior.hkx vanilla). El stride 0x48 de SSE
    ''' se midió por el espaciado de los global-fixups al hkbBlendingTransitionEffect de cada transición
    ''' (0x40, 0x88, 0xD0, 0x118, … = +0x48).
    ''' crop/cropEnd/startTime CONFIRMADOS por valor sobre el corpus completo (FO4 3740 clips / SSE 9973):
    ''' contando cuántos clips tienen valor ≠ 0 en cada offset, los roles se alinean 1 a 1 entre juegos —
    ''' cropStart SSE +0x58 (81) ↔ FO4 +0xA4 (184) · cropEnd +0x5C (17) ↔ +0xA8 (54) · startTime +0x60 (143)
    ''' ↔ +0xAC (7) · playbackSpeed +0x64 ↔ +0xB0 · enforcedDuration +0x68 (21) ↔ +0xB4 (40) ·
    ''' userControlledTimeFraction +0x6C ↔ +0xB8 (ambos acotados a ≤1, que es lo que sella la alineación).
    ''' ⛔ FO4 +0xA0 NO es cropStart: vale 0 en LOS 3740 clips = m_userPartitionMask (int, sólo en hk_2014).
    ''' Tomarlo como cropStart corre TODO el trío un campo y el startTime real no se lee nunca.</summary>
    Friend ReadOnly Property HkbLayout As HkbLayout_Class
        Get
            If _hkbLayout IsNot Nothing Then Return _hkbLayout
            Select Case Packfile.Header.PackfileFormat
                Case HkxPackfileFormat_Enum.Fallout64
                    _hkbLayout = New HkbLayout_Class With {
                        .ClipAnimationName = &H90, .ClipTriggers = &H98,
                        .ClipCropStart = &HA4, .ClipCropEnd = &HA8, .ClipStartTime = &HAC,
                        .ClipPlaybackSpeed = &HB0, .ClipEnforcedDuration = &HB4,
                        .ClipBindingIndex = &HBC, .ClipMode = &HBE, .ClipFlags = &HBF,
                        .BehaviorRefName = &H88, .TransitionDuration = &HA8,
                        .ModGenModifier = &H88, .ModGenGenerator = &H90, .ModifierListArray = &H58,
                        .ManualSelectorArray = &H88, .EvalExprData = &H58, .EventDrivenModifier = &H58,
                        .StateTagGenerator = &H90, .SyncClipGenerator = -1,
                        .ModEnable = &H50, .IsActiveFlags = &H58,
                        .CyclicBlender = &H90, .CyclicEvents = &H98, .CyclicEventCount = 4, .CyclicBlendParam = &HDC,
                        .BoneSwitchDefault = &H90, .BoneSwitchChildren = &H98,
                        .DynTagGenerator = &H90, .LayerGenerator = &H30, .LayerWeight = &H48,
                        .TwistAxis = &H60, .TwistAngle = &H70, .DeactEventId = &H58, .DeactPayload = &H60,
                        .AssignFloatPairs = &H58, .AssignIntPairs = &HF8,
                        .LookAtBones = &H270, .LookAtBoneStride = &H210, .LookAtIndex = -1,
                        .LookAtUpAxis = &H20, .LookAtLimitAngle = &H34, .LookAtOnGain = &H48, .LookAtOffGain = &H4C,
                        .GraphRoot = &HC0, .GraphData = &HC8, .GraphDataInitial = &H60, .GraphDataStrings = &H68,
                        .StateMachineStates = &HD0, .TransitionStride = &H48}
                Case HkxPackfileFormat_Enum.Skyrim64
                    _hkbLayout = New HkbLayout_Class With {
                        .ClipAnimationName = &H48, .ClipTriggers = &H50,
                        .ClipCropStart = &H58, .ClipCropEnd = &H5C, .ClipStartTime = &H60,
                        .ClipPlaybackSpeed = &H64, .ClipEnforcedDuration = &H68,
                        .ClipBindingIndex = &H70, .ClipMode = &H72, .ClipFlags = &H73,
                        .BehaviorRefName = &H48, .TransitionDuration = &H50,
                        .ModGenModifier = &H48, .ModGenGenerator = &H50, .ModifierListArray = &H50,
                        .ManualSelectorArray = &H48, .EvalExprData = &H50, .EventDrivenModifier = &H50,
                        .StateTagGenerator = &H50, .SyncClipGenerator = &H50,
                        .ModEnable = &H48, .IsActiveFlags = &H50,
                        .CyclicBlender = &H50, .CyclicEvents = &H58, .CyclicEventCount = 2, .CyclicBlendParam = &H7C,
                        .BoneSwitchDefault = &H50, .BoneSwitchChildren = &H58,
                        .DynTagGenerator = -1, .LayerGenerator = &H30, .LayerWeight = &H48,
                        .TwistAxis = &H50, .TwistAngle = &H60, .DeactEventId = &H50, .DeactPayload = &H58,
                        .AssignFloatPairs = -1, .AssignIntPairs = -1,
                        .LookAtBones = &H58, .LookAtBoneStride = &H40, .LookAtIndex = 0,
                        .LookAtUpAxis = -1, .LookAtLimitAngle = &H20, .LookAtOnGain = &H24, .LookAtOffGain = &H28,
                        .GraphRoot = &H80, .GraphData = &H88, .GraphDataInitial = &H70, .GraphDataStrings = &H78,
                        .StateMachineStates = &H90, .TransitionStride = &H48}
                Case Else
                    ' Skyrim LE 32-bit: sin archivos para medir ⇒ Known=False y offsets en -1 (lecturas
                    ' neutras) + un aviso. NO reusar los offsets de FO4 acá: dan basura silenciosa.
                    _hkbLayout = New HkbLayout_Class With {.Known = False,
                        .ClipAnimationName = &H90, .ClipTriggers = -1,
                        .ClipCropStart = -1, .ClipCropEnd = -1, .ClipStartTime = -1,
                        .ClipPlaybackSpeed = -1, .ClipEnforcedDuration = -1,
                        .ClipBindingIndex = -1, .ClipMode = -1, .ClipFlags = -1,
                        .BehaviorRefName = &H88, .TransitionDuration = -1,
                        .ModGenModifier = -1, .ModGenGenerator = -1, .ModifierListArray = -1,
                        .ManualSelectorArray = -1, .EvalExprData = -1, .EventDrivenModifier = -1,
                        .StateTagGenerator = -1, .SyncClipGenerator = -1,
                        .ModEnable = -1, .IsActiveFlags = -1,
                        .CyclicBlender = -1, .CyclicEvents = -1, .CyclicEventCount = 0, .CyclicBlendParam = -1,
                        .BoneSwitchDefault = -1, .BoneSwitchChildren = -1,
                        .DynTagGenerator = -1, .LayerGenerator = -1, .LayerWeight = -1,
                        .TwistAxis = -1, .TwistAngle = -1, .DeactEventId = -1, .DeactPayload = -1,
                        .AssignFloatPairs = -1, .AssignIntPairs = -1,
                        .LookAtBones = -1, .LookAtBoneStride = -1, .LookAtIndex = -1,
                        .LookAtUpAxis = -1, .LookAtLimitAngle = -1, .LookAtOnGain = -1, .LookAtOffGain = -1,
                        .GraphRoot = -1, .GraphData = -1, .GraphDataInitial = -1, .GraphDataStrings = -1,
                        .StateMachineStates = -1, .TransitionStride = -1}
                    Dim fmt = Packfile.Header.PackfileFormat
                    Logger.LogLazy(Function() $"[HKB-LAYOUT] Formato '{fmt}' sin layout hkb* medido: los campos numéricos de hkbClipGenerator, los states de hkbStateMachine y las transiciones se devuelven vacíos (antes se leían con offsets de Fallout 4 = basura).")
            End Select
            Return _hkbLayout
        End Get
    End Property

    ' Lecturas que respetan un offset "no medido" (-1) devolviendo neutro en vez de leer basura.
    Private Function ReadSingleAt(rel As Integer, fieldOffset As Integer) As Single
        If fieldOffset < 0 Then Return 0.0F
        Return ReadSingle(rel + fieldOffset)
    End Function

    Private Function ReadInt16At(rel As Integer, fieldOffset As Integer, fallback As Integer) As Integer
        If fieldOffset < 0 Then Return fallback
        Return CInt(ReadInt16(rel + fieldOffset))
    End Function

    Private Function ReadByteAt(rel As Integer, fieldOffset As Integer) As Integer
        If fieldOffset < 0 Then Return 0
        Return CInt(ReadByte(rel + fieldOffset))
    End Function

    ' --------------------- FASE 2: topología state-machine → clips ---------------------
    ' Los nodos hkb (generadores) llevan m_name@+0x38 en AMBOS formatos.
    ' El m_generator/m_transitions del state-info se resuelven por offset (verificado idéntico
    ' en los dos juegos).

    ''' <summary>Nombre del nodo (m_name@+0x38) de cualquier hkb generator/modifier.</summary>
    Public Function ReadNodeName(obj As HkxVirtualObjectGraph_Class) As String
        If IsNothing(obj) Then Return ""
        Return ResolveLocalString(obj.RelativeOffset + &H38)
    End Function

    ''' <summary>hkbClipGenerator: nodo + la animación (.hkt) que reproduce + params de playback.
    ''' TODOS los offsets salen de <see cref="HkbLayout"/> (medidos por formato); ninguno está hardcodeado acá.</summary>
    Public Function ParseClipGenerator(source As HkxVirtualObjectGraph_Class) As HkbClipGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbClipGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbClipGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Return New HkbClipGeneratorGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .AnimationName = ResolveClipAnimationName(source),
            .TriggersObject = If(L.ClipTriggers < 0, Nothing, ResolveGlobalObject(rel + L.ClipTriggers)),
            .CropStartLocalTime = ReadSingleAt(rel, L.ClipCropStart),
            .CropEndLocalTime = ReadSingleAt(rel, L.ClipCropEnd),
            .StartTime = ReadSingleAt(rel, L.ClipStartTime),
            .PlaybackSpeed = ReadSingleAt(rel, L.ClipPlaybackSpeed),
            .EnforcedDuration = ReadSingleAt(rel, L.ClipEnforcedDuration),
            .AnimationBindingIndex = ReadInt16At(rel, L.ClipBindingIndex, -1),
            .PlaybackMode = ReadByteAt(rel, L.ClipMode),
            .FlagsRaw = ReadByteAt(rel, L.ClipFlags)
        }
    End Function

    ''' <summary>Resuelve el string secundario de un generador (hkbClipGenerator::m_animationName,
    ''' hkbBehaviorReferenceGenerator::m_behaviorName, BGSGamebryoSequenceGenerator).
    ''' <para>DATA-DRIVEN: el offset viene MEDIDO por formato desde <see cref="HkbLayout"/>
    ''' (clip FO4 +0x90 / SSE +0x48 ; behRef FO4 +0x88 / SSE +0x48). El barrido de fixups ("el unico
    ''' string que no es m_name@+0x38") es HEURISTICO —queda a merced del ORDEN de los fixups si un
    ''' nodo tuviera mas de un string— asi que solo se usa cuando el formato NO tiene layout medido,
    ''' y en ese caso LOGUEA. Medido sobre el corpus vanilla completo (13.713 hkbClipGenerator + 220
    ''' hkbBehaviorReferenceGenerator de los DOS juegos): 0 nodos ambiguos y 0 diferencias contra el
    ''' offset medido.</para></summary>
    Public Function ResolveGeneratorTargetString(source As HkxVirtualObjectGraph_Class, measuredOffset As Integer) As String
        If IsNothing(source) Then Return ""
        Dim rel = source.RelativeOffset
        If measuredOffset >= 0 Then
            ' Camino unico cuando el formato TIENE layout medido: sin heuristica, sin ambiguedad.
            Return ResolveLocalString(rel + measuredOffset)
        End If
        ' Formato sin layout medido: ultimo recurso por barrido de fixups, AVISANDO.
        Dim nameSrc = rel + &H38
        For Each lf In GetLocalFixupsInRange(rel, source.Size)
            If lf.SourceRelativeOffset = nameSrc Then Continue For
            Dim s = ReadNullTerminatedString(lf.DestinationRelativeOffset)
            If Not String.IsNullOrEmpty(s) AndAlso IsPrintableString(s) Then
                Dim cls = source.ClassName
                Logger.LogLazy(Function() $"[HKB-LAYOUT] '{cls}': sin offset medido para este formato, string resuelto por BARRIDO de fixups (heuristico). Medir la clase.")
                Return s
            End If
        Next
        Return ""
    End Function

    ''' <summary>hkbClipGenerator::m_animationName, por el offset del layout MEDIDO por formato
    ''' (FO4 +0x90 / SSE +0x48). Ver ResolveGeneratorTargetString para el fallback por barrido.</summary>
    Private Function ResolveClipAnimationName(source As HkxVirtualObjectGraph_Class) As String
        Return ResolveGeneratorTargetString(source, HkbLayout.ClipAnimationName)
    End Function

    ''' <summary>hkbBlenderGenerator: nombre + children (cada uno con su weight y el generador que mezcla).</summary>
    Public Function ParseBlenderGenerator(source As HkxVirtualObjectGraph_Class) As HkbBlenderGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBlenderGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBlenderGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbBlenderGeneratorGraph_Class With {.SourceObject = source, .Name = ResolveLocalString(source.RelativeOffset + &H38)}
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt IsNot Nothing AndAlso tgt.ClassName.Equals("hkbBlenderGeneratorChild", StringComparison.OrdinalIgnoreCase) Then
                Dim ch = ParseBlenderChild(tgt)
                If ch IsNot Nothing Then result.Children.Add(ch)
            End If
        Next
        Return result
    End Function

    ''' <summary>hkbBlenderGeneratorChild: weight (+0x40) + el generador que aporta a la mezcla.</summary>
    Public Function ParseBlenderChild(source As HkxVirtualObjectGraph_Class) As HkbBlenderGeneratorChildGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBlenderGeneratorChild", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBlenderGeneratorChild(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim gen As HkxVirtualObjectGraph_Class = Nothing
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt IsNot Nothing AndAlso IsGeneratorClass(tgt.ClassName) Then
                gen = tgt
                Exit For
            End If
        Next
        Return New HkbBlenderGeneratorChildGraph_Class With {
            .SourceObject = source,
            .Weight = hkr.Weight,
            .WorldFromModelWeight = hkr.WorldFromModelWeight,
            .GeneratorSummary = DescribeGenerator(gen)
        }
    End Function

    ''' <summary>hkbStateMachine: nombre + estados (refs a hkbStateMachineStateInfo).</summary>
    Public Function ParseStateMachine(source As HkxVirtualObjectGraph_Class) As HkbStateMachineGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachine", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbStateMachine(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbStateMachineGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name
        }
        If HkbLayout.StateMachineStates < 0 Then Return result
        For Each stateObj In ReadObjectReferenceArray(source.RelativeOffset + HkbLayout.StateMachineStates)
            Dim st = ParseStateInfo(stateObj)
            If st IsNot Nothing Then result.States.Add(st)
        Next
        Return result
    End Function

    ''' <summary>hkbStateMachineStateInfo: nombre + generador (qué produce la pose) + transiciones.
    ''' El generador y las transiciones se identifican por la CLASE del objeto referenciado.</summary>
    Public Function ParseStateInfo(source As HkxVirtualObjectGraph_Class) As HkbStateInfoGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachineStateInfo", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbStateMachineStateInfo(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbStateInfoGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .StateId = hkr.StateId
        }
        ' Layout hkbStateMachineStateInfo (idéntico en los dos juegos): m_transitions@+0x50,
        ' m_generator@+0x58, m_name@+0x60. Se leen por OFFSET, no por class-scan del rango: el scan
        ' ("el primer generator que aparezca") es una heurística que se rompe en cuanto un state-info
        ' tenga >1 ref a clase generator. Verificado sobre 239 state-info de Fallout4 - Animations.ba2
        ' (Tools/StateInfoOffsetProbe): +0x58 es generator en 239/239 y ninguno tiene más de una ref.
        result.TransitionsObject = ResolveGlobalRefAt(source.RelativeOffset + &H50)
        result.GeneratorObject = ResolveGlobalRefAt(source.RelativeOffset + &H58)
        result.GeneratorSummary = DescribeGenerator(result.GeneratorObject)
        result.Transitions.AddRange(ParseTransitions(result.TransitionsObject))
        Return result
    End Function

    ''' <summary>Resuelve el objeto referenciado por el puntero que vive EN un offset de source exacto
    ''' (lectura de campo por offset). Devuelve Nothing si no hay fixup global ahí (puntero null). El
    ''' puntero ocupa 8 bytes, así que se busca el fixup cuyo SourceRelativeOffset == el offset pedido
    ''' dentro de un rango de 8.</summary>
    Private Function ResolveGlobalRefAt(sourceRelativeOffset As Integer) As HkxVirtualObjectGraph_Class
        For Each gf In GetGlobalFixupsInRange(sourceRelativeOffset, 8)
            If gf.SourceRelativeOffset = sourceRelativeOffset Then Return GetObject(gf.TargetRelativeOffset)
        Next
        Return Nothing
    End Function

    ''' <summary>Resumen "qué reproduce" un generador, recursando los wrappers (Fase 3a) hasta los
    ''' clips/behaviors/gamebryo reales. Sigue refs cuya clase sea generador; SM anidada = hoja "sm:".</summary>
    Public Function DescribeGenerator(gen As HkxVirtualObjectGraph_Class) As String
        If IsNothing(gen) Then Return ""
        Dim leaves As New List(Of String)
        CollectGeneratorLeaves(gen, leaves, New HashSet(Of Integer), 0)
        If leaves.Count = 0 Then Return gen.ClassName & " '" & ReadNodeName(gen) & "'"
        Dim distinct = leaves.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        If distinct.Count = 1 AndAlso gen.ClassName.Equals("hkbClipGenerator", StringComparison.OrdinalIgnoreCase) Then Return distinct(0)
        Return gen.ClassName & " → [" & String.Join(", ", distinct) & "]"
    End Function

    ' Recolecta las hojas (clip/behavior/gamebryo/sm) alcanzables siguiendo refs de generador.
    Private Sub CollectGeneratorLeaves(gen As HkxVirtualObjectGraph_Class, leaves As List(Of String), visited As HashSet(Of Integer), depth As Integer)
        If IsNothing(gen) OrElse depth > 8 OrElse Not visited.Add(gen.RelativeOffset) Then Return
        Dim cn = If(gen.ClassName, "")
        If cn.Equals("hkbClipGenerator", StringComparison.OrdinalIgnoreCase) Then
            leaves.Add("clip:" & ResolveClipAnimationName(gen))
        ElseIf cn.Equals("hkbBehaviorReferenceGenerator", StringComparison.OrdinalIgnoreCase) Then
            leaves.Add("behavior:" & ResolveGeneratorTargetString(gen, HkbLayout.BehaviorRefName))
        ElseIf cn.Equals("BGSGamebryoSequenceGenerator", StringComparison.OrdinalIgnoreCase) Then
            leaves.Add("gamebryo:" & ResolveGeneratorTargetString(gen, HkbLayout.BehaviorRefName))
        ElseIf cn.Equals("hkbStateMachine", StringComparison.OrdinalIgnoreCase) Then
            leaves.Add("sm:" & ResolveLocalString(gen.RelativeOffset + &H38))   ' SM anidada: no expandir
        Else
            ' wrapper (modifier/blender/child/selector/poseMatching/layer/…): seguir refs de generador.
            For Each gf In GetGlobalFixupsInRange(gen.RelativeOffset, gen.Size)
                Dim tgt = GetObject(gf.TargetRelativeOffset)
                If tgt IsNot Nothing AndAlso IsGeneratorClass(tgt.ClassName) Then
                    CollectGeneratorLeaves(tgt, leaves, visited, depth + 1)
                End If
            Next
        End If
    End Sub

    ''' <summary>hkbStateMachineTransitionInfoArray → lista de (eventId, toStateId). Array@+0x10;
    ''' stride del elemento = <see cref="HkbLayout"/>.TransitionStride (0x48 en ambos juegos);
    ''' eventId@elem+0x30, toStateId@elem+0x34.</summary>
    Public Function ParseTransitions(source As HkxVirtualObjectGraph_Class) As List(Of HkbTransitionGraph_Class)
        Dim result As New List(Of HkbTransitionGraph_Class)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachineTransitionInfoArray", StringComparison.OrdinalIgnoreCase) Then Return result

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbStateMachineTransitionInfoArray(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim header = hkr.Transitions
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        ' stride del hkbStateMachineTransitionInfo = 0x48 en LOS DOS formatos (medido por el espaciado de
        ' los global-fixups al hkbBlendingTransitionEffect de cada transición: FO4 0x50→0x98, SSE
        ' 0x40→0x88→0xD0→…). ⛔ NO es 0x40: ese valor sólo acierta el elemento 0 y hace que todo array
        ' con ≥2 transiciones devuelva basura, en los dos juegos.
        Dim stride As Integer = HkbLayout.TransitionStride
        If stride <= 0 Then Return result
        For i = 0 To header.Count - 1
            Dim e = header.DataRelativeOffset + (i * stride)
            result.Add(New HkbTransitionGraph_Class With {
                .EventId = ReadInt32(e + &H30),
                .ToStateId = ReadInt32(e + &H34)
            })
        Next
        Return result
    End Function

    ' --------------------- Clases de soporte hkb (campos tipados) ---------------------
    ' Offsets verificados con --dump; idénticos en los dos juegos salvo donde se aclare.

    ''' <summary>hkbClipTriggerArray → triggers {localTime, eventId} (eventos disparados en tiempos del clip).</summary>
    Public Function ParseClipTriggerArray(source As HkxVirtualObjectGraph_Class) As HkbClipTriggerArrayGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbClipTriggerArray", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbClipTriggerArray(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.Triggers
        Dim result As New HkbClipTriggerArrayGraph_Class With {.SourceObject = source}
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * &H20)   ' hkbClipTrigger stride 0x20
                result.Triggers.Add(New HkbClipTrigger_Class With {.LocalTime = ReadSingle(e + 0), .EventId = ReadInt32(e + 8)})
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbBoneIndexArray → índices de hueso (int16, -1 = ninguno). Array@+0x30.</summary>
    Public Function ParseBoneIndexArray(source As HkxVirtualObjectGraph_Class) As List(Of Integer)
        Dim result As New List(Of Integer)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBoneIndexArray", StringComparison.OrdinalIgnoreCase) Then Return result

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBoneIndexArray(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.BoneIndices
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                result.Add(CInt(ReadInt16(h.DataRelativeOffset + (i * 2))))
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbVariableValueSet → valores de variables como words crudos (int o float según el tipo
    ''' declarado en hkbBehaviorGraphData). Array@+0x10.</summary>
    Public Function ParseVariableValueSet(source As HkxVirtualObjectGraph_Class) As HkbVariableValueSetGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbVariableValueSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbVariableValueSet(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.WordVariableValues
        Dim result As New HkbVariableValueSetGraph_Class With {.SourceObject = source}
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim w = ReadInt32(h.DataRelativeOffset + (i * 4))
                result.Values.Add(New HkbVariableValue_Class With {.AsInt = w, .AsFloat = BitConverter.ToSingle(BitConverter.GetBytes(w), 0)})
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbStateMachineEventPropertyArray → eventos {eventId, payload}. Array@+0x10, stride 0x10.</summary>
    Public Function ParseEventPropertyArray(source As HkxVirtualObjectGraph_Class) As List(Of HkbEventProperty_Class)
        Dim result As New List(Of HkbEventProperty_Class)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachineEventPropertyArray", StringComparison.OrdinalIgnoreCase) Then Return result

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbStateMachineEventPropertyArray(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.Events
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * &H10)
                result.Add(New HkbEventProperty_Class With {.EventId = ReadInt32(e + 0), .PayloadObject = ResolveGlobalObject(e + 8)})
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbVariableBindingSet → bindings {memberPath, variableIndex, bitIndex} (liga variables del
    ''' grafo a miembros de nodos). Element@array+0x10, stride 0x28; memberPath@+0, varIdx@+0x1C, bitIdx@+0x20.</summary>
    Public Function ParseVariableBindingSet(source As HkxVirtualObjectGraph_Class) As List(Of HkbVariableBinding_Class)
        Dim result As New List(Of HkbVariableBinding_Class)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbVariableBindingSet", StringComparison.OrdinalIgnoreCase) Then Return result

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbVariableBindingSet(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.Bindings
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * &H28)
                ' m_bitIndex es int8@+0x20 y m_bindingType int8@+0x21: son DOS campos. Leerlos como UN
                ' int32 da 255 (0xFF) o 511 (0xFF|0x01<<8) donde el valor real es -1.
                ' MEDIDO sobre 13.580 bindings de los dos juegos: el byte@+0x21 sólo vale 0 ó 1, y los
                ' binding con VariableIndex fuera del rango de VariableNames son SIEMPRE tipo 1 (5/5 en
                ' FO4) — no es basura: el tipo 1 indexa las PROPERTIES del character, otro espacio de
                ' nombres. Los tipo 0 están 100% en rango (4074 FO4 + 7781 SSE).
                result.Add(New HkbVariableBinding_Class With {
                    .MemberPath = ResolveLocalString(e + 0),
                    .VariableIndex = ReadInt32(e + &H1C),
                    .BitIndex = ToSByteValue(ReadByte(e + &H20)),
                    .BindingType = CInt(ReadByte(e + &H21))})
            Next
        End If
        Return result
    End Function

    ' int8 con signo desde un byte crudo (CSByte revienta con >127 bajo checked).
    Private Shared Function ToSByteValue(b As Byte) As Integer
        Return If(b > 127, CInt(b) - 256, CInt(b))
    End Function

    ''' <summary>hkbExpressionDataArray → expresiones {expression, assignmentVariableIndex, assignmentEventIndex}.
    ''' Element@array+0x10, stride 0x18; expression@+0, assignVar@+8, assignEvt@+0xC.</summary>
    Public Function ParseExpressionDataArray(source As HkxVirtualObjectGraph_Class) As List(Of HkbExpressionData_Class)
        Dim result As New List(Of HkbExpressionData_Class)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbExpressionDataArray", StringComparison.OrdinalIgnoreCase) Then Return result

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbExpressionDataArray(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.ExpressionsData
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * &H18)
                result.Add(New HkbExpressionData_Class With {
                    .Expression = ResolveLocalString(e + 0),
                    .AssignmentVariableIndex = ReadInt32(e + 8),
                    .AssignmentEventIndex = ReadInt32(e + &HC)})
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbStringEventPayload → el string del payload (@+0x10).</summary>
    Public Function ParseStringEventPayload(source As HkxVirtualObjectGraph_Class) As String
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStringEventPayload", StringComparison.OrdinalIgnoreCase) Then Return ""
        ' Lector generado: el offset sale de la reflexion de los dos .exe.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbStringEventPayload(Me, source)
        If Not hkr.IsValid Then Return ""
        Return hkr.Data
    End Function

    ''' <summary>hkbMirroredSkeletonInfo → eje de espejo + mapa de pares de hueso (bonePairMap[i] = hueso espejo de i).
    ''' mirrorAxis@+0x10 (vec4), bonePairMap (int16[])@+0x20.</summary>
    Public Function ParseMirroredSkeletonInfo(source As HkxVirtualObjectGraph_Class) As HkbMirroredSkeletonInfoGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbMirroredSkeletonInfo", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbMirroredSkeletonInfo(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkbMirroredSkeletonInfoGraph_Class With {
            .SourceObject = source,
            .MirrorAxisX = ReadSingle(rel + &H10), .MirrorAxisY = ReadSingle(rel + &H14),
            .MirrorAxisZ = ReadSingle(rel + &H18), .MirrorAxisW = ReadSingle(rel + &H1C)}
        Dim h = ReadArrayHeader(rel + &H20)
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                result.BonePairMap.Add(CInt(ReadInt16(h.DataRelativeOffset + (i * 2))))
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbBoneWeightArray → pesos por hueso (float[]) @+0x30 (máscaras de cuerpo parcial),
    ''' mismo offset que hkbBoneIndexArray. ⛔ NO es +0x10: ahí no hay fixup en NINGUNA instancia
    ''' (0/1304 SSE, 0/621 FO4) y la lista sale siempre vacía, en silencio.</summary>
    Public Function ParseBoneWeightArray(source As HkxVirtualObjectGraph_Class) As List(Of Single)
        Dim result As New List(Of Single)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBoneWeightArray", StringComparison.OrdinalIgnoreCase) Then Return result

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBoneWeightArray(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim h = hkr.BoneWeights
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                result.Add(ReadSingle(h.DataRelativeOffset + (i * 4)))
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbFootIkDriverInfo → params globales de Foot IK + nº de piernas. Floats verificados por --dump
    ''' (raycast 30/100, gains 0.1/0.2/1.0...). Nombres best-effort por orden de miembros (sin reflection).</summary>
    Public Function ParseFootIkDriverInfo(source As HkxVirtualObjectGraph_Class) As HkbFootIkDriverInfoGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbFootIkDriverInfo", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbFootIkDriverInfo(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim legsHeader = ReadArrayHeader(rel + &H10)
        Return New HkbFootIkDriverInfoGraph_Class With {
            .SourceObject = source,
            .LegCount = legsHeader.Count,
            .RaycastDistanceUp = ReadSingle(rel + &H20),
            .RaycastDistanceDown = ReadSingle(rel + &H24),
            .OriginalGroundHeightMS = ReadSingle(rel + &H34),
            .VerticalOffset = ReadSingle(rel + &H38),
            .CollisionUpAxisMS = ReadSingle(rel + &H58)
        }
    End Function

    ''' <summary>hkbHandIkDriverInfo → array de Hand @+0x10 (típ. 2: izquierda/derecha). Layout de
    ''' Hand (stride 0x60) verificado contra --dump: elbowAxisLS@+0x00, backHandNormalLS@+0x10,
    ''' handOffsetLS@+0x20, handOrienationLS(quat)@+0x30, maxElbowAngleDegrees@+0x40,
    ''' minElbowAngleDegrees@+0x44, shoulderIndex@+0x48, shoulderSiblingIndex@+0x4A, elbowIndex@+0x4C,
    ''' elbowSiblingIndex@+0x4E, wristIndex@+0x50, enforceEndPosition@+0x52, enforceEndRotation@+0x53.</summary>
    Public Function ParseHandIkDriverInfo(source As HkxVirtualObjectGraph_Class) As HkbHandIkDriverInfoGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbHandIkDriverInfo", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbHandIkDriverInfo(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkbHandIkDriverInfoGraph_Class With {.SourceObject = source}
        Dim handsHeader = ReadArrayHeader(rel + &H10)
        If handsHeader.Count > 0 AndAlso handsHeader.DataRelativeOffset >= 0 Then
            For i = 0 To handsHeader.Count - 1
                Dim off = handsHeader.DataRelativeOffset + (i * &H60)
                result.Hands.Add(New HkbHandIkHand_Class With {
                    .ElbowAxisLS = New HkxVector4Graph_Class With {.X = ReadSingle(off + &H0), .Y = ReadSingle(off + &H4), .Z = ReadSingle(off + &H8), .W = ReadSingle(off + &HC)},
                    .BackHandNormalLS = New HkxVector4Graph_Class With {.X = ReadSingle(off + &H10), .Y = ReadSingle(off + &H14), .Z = ReadSingle(off + &H18), .W = ReadSingle(off + &H1C)},
                    .HandOffsetLS = New HkxVector4Graph_Class With {.X = ReadSingle(off + &H20), .Y = ReadSingle(off + &H24), .Z = ReadSingle(off + &H28), .W = ReadSingle(off + &H2C)},
                    .HandOrientationLS = New HkxQuaternionGraph_Class With {.X = ReadSingle(off + &H30), .Y = ReadSingle(off + &H34), .Z = ReadSingle(off + &H38), .W = ReadSingle(off + &H3C)},
                    .MaxElbowAngleDegrees = ReadSingle(off + &H40),
                    .MinElbowAngleDegrees = ReadSingle(off + &H44),
                    .ShoulderIndex = ReadInt16(off + &H48),
                    .ShoulderSiblingIndex = ReadInt16(off + &H4A),
                    .ElbowIndex = ReadInt16(off + &H4C),
                    .ElbowSiblingIndex = ReadInt16(off + &H4E),
                    .WristIndex = ReadInt16(off + &H50),
                    .EnforceEndPosition = ReadByte(off + &H52) <> 0,
                    .EnforceEndRotation = ReadByte(off + &H53) <> 0
                })
            Next
        End If
        Return result
    End Function

    Private Shared Function IsGeneratorClass(className As String) As Boolean
        If String.IsNullOrEmpty(className) Then Return False
        If className.Equals("hkbStateMachine", StringComparison.OrdinalIgnoreCase) Then Return True
        Return className.IndexOf("Generator", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    ' --------------------- FASE 3b: modifiers / IK / ragdoll-controls ---------------------
    ' Todo nodo hkb (generator o modifier) lleva m_name@+0x38 y referencia sus sub-objetos por
    ' global-fixup. Un parser genérico de nodo (nombre + clases referenciadas) deja TODO hkb*
    ' estructuralmente accesible sin RE per-campo de cada uno de los ~30 modifiers.

    ''' <summary>hkbBlendingTransitionEffect: el efecto de blend de una transicion (nombre + duracion).
    ''' <para>m_duration MEDIDO POR VALOR en los dos juegos, usando instancias cuyo NOMBRE codifica la
    ''' duracion: SSE +0x50 ('0.5secondBlend'=0.5, '1secondBlend'=1, '2secondBlend'=2 - 12 instancias);
    ''' FO4 +0xA8 ('blend_0.25s'=0.25, 'blend_0.50s'=0.5, 'blend_0.2s'=0.2, '4Seconds'=4 - 8 instancias).
    ''' m_toGeneratorStartTimeFraction es el float inmediatamente posterior (mismo orden en ambos).</para></summary>
    Public Function ParseBlendingTransitionEffect(source As HkxVirtualObjectGraph_Class) As HkbBlendingTransitionEffectGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBlendingTransitionEffect", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBlendingTransitionEffect(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim d = HkbLayout.TransitionDuration
        Return New HkbBlendingTransitionEffectGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .Duration = ReadSingleAt(rel, d),
            .ToGeneratorStartTimeFraction = ReadSingleAt(rel, If(d < 0, -1, d + 4))
        }
    End Function

    ''' <summary>BGSGamebryoSequenceGenerator: reproduce una secuencia Gamebryo (NiControllerSequence)
    ''' en vez de un clip Havok. El nombre de la secuencia vive en el MISMO offset que
    ''' hkbBehaviorReferenceGenerator::m_behaviorName (FO4 +0x88 / SSE +0x48) y el resto del bloque es
    ''' RELATIVO a el, identico en ambos juegos: +8 = m_eBlendModeFunction, +0xC = m_fPercent
    ''' (medido: vale 1.0 en 603/603 instancias SSE y 875/875 FO4).
    ''' <para>El nombre Havok del miembro del string NO esta confirmado (no aparece en el pool de
    ''' reflexion del binario): la propiedad se llama SequenceName por su CONTENIDO.</para></summary>
    Public Function ParseGamebryoSequenceGenerator(source As HkxVirtualObjectGraph_Class) As HkbGamebryoSequenceGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BGSGamebryoSequenceGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BGSGamebryoSequenceGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim b = HkbLayout.BehaviorRefName
        Return New HkbGamebryoSequenceGeneratorGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .SequenceName = ResolveGeneratorTargetString(source, b),
            .BlendModeFunction = If(b < 0, 0, ReadInt32(rel + b + 8)),
            .Percent = ReadSingleAt(rel, If(b < 0, -1, b + &HC))
        }
    End Function

    ' ===================== El resto del grafo hkb =====================
    ' DISENO: los generadores "wrapper" resuelven su(s) hijo(s) por la CLASE del objeto apuntado por
    ' el global-fixup, NO por offset. Es determinista (la unica ref a clase-generador de estos nodos
    ' ES el hijo) y no necesita una constante por juego — los offsets del array de hijos SI difieren
    ' (ej. hkbManualSelectorGenerator: SSE header@+0x48 -> datos@+0x70 ; FO4 datos@+0xF0).

    ''' <summary>hkbExpressionCondition: la condicion de una transicion, como texto.
    ''' m_expression@+0x10, VERIFICADO identico en los dos juegos ("isInFurniture == 0" en SSE,
    ''' "iSyncJumpState!=3" en FO4).</summary>
    Public Function ParseExpressionCondition(source As HkxVirtualObjectGraph_Class) As String
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbExpressionCondition", StringComparison.OrdinalIgnoreCase) Then Return ""
        ' Lector generado: el offset sale de la reflexion de los dos .exe.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbExpressionCondition(Me, source)
        If Not hkr.IsValid Then Return ""
        Return hkr.Expression
    End Function

    ''' <summary>hkbBehaviorGraph: la raiz de un archivo de behavior. Nombre + generador raiz + data.
    ''' Las dos refs salen del offset MEDIDO por formato (root/data: SSE +0x80/+0x88, FO4 +0xC0/+0xC8);
    ''' el class-scan queda solo para un formato sin layout.</summary>
    Public Function ParseBehaviorGraph(source As HkxVirtualObjectGraph_Class) As HkbBehaviorGraphGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBehaviorGraph", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBehaviorGraph(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbBehaviorGraphGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H38)
        }
        ' DATA-DRIVEN: offsets medidos (SSE +0x80/+0x88, FO4 +0xC0/+0xC8). El class-scan del Else es el
        ' ultimo recurso para un formato sin layout, y depende de que haya EXACTAMENTE una ref de cada
        ' clase en el rango: no es equivalente al offset, es una heuristica.
        If HkbLayout.GraphRoot >= 0 Then
            result.RootGeneratorObject = ResolveGlobalObject(source.RelativeOffset + HkbLayout.GraphRoot)
            result.DataObject = ResolveGlobalObject(source.RelativeOffset + HkbLayout.GraphData)
        Else
            For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
                Dim tgt = GetObject(gf.TargetRelativeOffset)
                If tgt Is Nothing Then Continue For
                If tgt.ClassName.Equals("hkbBehaviorGraphData", StringComparison.OrdinalIgnoreCase) Then
                    result.DataObject = tgt
                ElseIf result.RootGeneratorObject Is Nothing AndAlso IsGeneratorClass(tgt.ClassName) Then
                    result.RootGeneratorObject = tgt
                End If
            Next
        End If
        result.RootGeneratorSummary = DescribeGenerator(result.RootGeneratorObject)
        Return result
    End Function

    ''' <summary>hkbBehaviorGraphData: los datos del grafo — string-data (eventos/variables) y los
    ''' valores INICIALES de las variables. Ambos por offset MEDIDO: SSE +0x70/+0x78, FO4 +0x60/+0x68
    ''' (el ORDEN de miembros difiere entre versiones, no es un delta constante). El class-scan queda
    ''' solo para un formato sin layout.</summary>
    Public Function ParseBehaviorGraphData(source As HkxVirtualObjectGraph_Class) As HkbBehaviorGraphDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBehaviorGraphData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBehaviorGraphData(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbBehaviorGraphDataGraph_Class With {.SourceObject = source}
        If HkbLayout.GraphDataStrings >= 0 Then
            result.StringData = ParseBehaviorGraphStringData(ResolveGlobalObject(source.RelativeOffset + HkbLayout.GraphDataStrings))
            result.InitialValues = ParseVariableValueSet(ResolveGlobalObject(source.RelativeOffset + HkbLayout.GraphDataInitial))
        Else
            For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
                Dim tgt = GetObject(gf.TargetRelativeOffset)
                If tgt Is Nothing Then Continue For
                If tgt.ClassName.Equals("hkbBehaviorGraphStringData", StringComparison.OrdinalIgnoreCase) Then
                    result.StringData = ParseBehaviorGraphStringData(tgt)
                ElseIf tgt.ClassName.Equals("hkbVariableValueSet", StringComparison.OrdinalIgnoreCase) Then
                    result.InitialValues = ParseVariableValueSet(tgt)
                End If
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbBehaviorReferenceGenerator: un nodo que delega en OTRO archivo de behavior.
    ''' m_behaviorName sale del layout medido (FO4 +0x88 / SSE +0x48).</summary>
    Public Function ParseBehaviorReferenceGenerator(source As HkxVirtualObjectGraph_Class) As HkbBehaviorReferenceGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBehaviorReferenceGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbBehaviorReferenceGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Return New HkbBehaviorReferenceGeneratorGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .BehaviorName = ResolveGeneratorTargetString(source, HkbLayout.BehaviorRefName)
        }
    End Function

    ''' <summary>Generadores WRAPPER: envuelven a uno o varios generadores hijos y eligen/etiquetan.
    ''' Cubre hkbManualSelectorGenerator (elige uno de N por indice/variable),
    ''' BSSynchronizedClipGenerator (sincroniza un clip con otro actor),
    ''' BSiStateTaggingGenerator y DynamicAnimationTaggingGenerator (etiquetan el estado del hijo).
    ''' Los hijos se resuelven por CLASE del destino, sin offsets por juego.</summary>
    Public Function ParseWrapperGenerator(source As HkxVirtualObjectGraph_Class) As HkbWrapperGeneratorGraph_Class
        If IsNothing(source) Then Return Nothing
        Dim wrappers As String() = {"hkbManualSelectorGenerator", "BSSynchronizedClipGenerator",
                                                "BSiStateTaggingGenerator", "DynamicAnimationTaggingGenerator",
                                                "hkbModifierGenerator"}
        If Not wrappers.Any(Function(w) w.Equals(source.ClassName, StringComparison.OrdinalIgnoreCase)) Then Return Nothing
        Dim result As New HkbWrapperGeneratorGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Name = ResolveLocalString(source.RelativeOffset + &H38)
        }
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt Is Nothing OrElse Not IsGeneratorClass(tgt.ClassName) Then Continue For
            result.ChildObjects.Add(tgt)
            result.ChildSummaries.Add(DescribeGenerator(tgt))
        Next
        Return result
    End Function

    ''' <summary>hkbModifierGenerator: aplica un modifier a un generador. m_modifier y m_generator
    ''' MEDIDOS: SSE +0x48/+0x50, FO4 +0x88/+0x90 (1875 y 1747 instancias).</summary>
    Public Function ParseModifierGenerator(source As HkxVirtualObjectGraph_Class) As HkbModifierGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbModifierGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbModifierGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Return New HkbModifierGeneratorGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .ModifierObject = If(L.ModGenModifier < 0, Nothing, ResolveGlobalObject(rel + L.ModGenModifier)),
            .GeneratorObject = If(L.ModGenGenerator < 0, Nothing, ResolveGlobalObject(rel + L.ModGenGenerator))
        }
    End Function

    ''' <summary>hkbModifierList: lista ordenada de modifiers. Array MEDIDO: SSE +0x50 / FO4 +0x58.</summary>
    Public Function ParseModifierListTyped(source As HkxVirtualObjectGraph_Class) As HkbModifierListTypedGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbModifierList", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbModifierList(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbModifierListTypedGraph_Class With {
            .SourceObject = source, .Name = hkr.Name}
        If HkbLayout.ModifierListArray >= 0 Then
            result.Modifiers.AddRange(ReadObjectReferenceArray(source.RelativeOffset + HkbLayout.ModifierListArray))
        End If
        Return result
    End Function

    ''' <summary>hkbManualSelectorGenerator: elige UNO de N generadores. Array MEDIDO: SSE +0x48 / FO4 +0x88.
    ''' Devuelve los generadores EN ORDEN (el indice seleccionado es relativo a este array).</summary>
    Public Function ParseManualSelectorGenerator(source As HkxVirtualObjectGraph_Class) As HkbManualSelectorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbManualSelectorGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbManualSelectorGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbManualSelectorGraph_Class With {
            .SourceObject = source, .Name = hkr.Name}
        If HkbLayout.ManualSelectorArray >= 0 Then
            result.Generators.AddRange(ReadObjectReferenceArray(source.RelativeOffset + HkbLayout.ManualSelectorArray))
        End If
        Return result
    End Function

    ''' <summary>Nodos que envuelven a UN solo hijo por puntero, con offset medido por clase y juego:
    ''' hkbEvaluateExpressionModifier -> hkbExpressionDataArray (SSE +0x50 / FO4 +0x58, 449 y 91 = 100%),
    ''' hkbEventDrivenModifier -> el modifier disparado (SSE +0x50 / FO4 +0x58),
    ''' BSiStateTaggingGenerator -> el generador etiquetado (SSE +0x50 / FO4 +0x90),
    ''' BSSynchronizedClipGenerator -> el hkbClipGenerator sincronizado (SSE +0x50, 531/531).</summary>
    Public Function ParseSingleChildNode(source As HkxVirtualObjectGraph_Class) As HkbSingleChildGraph_Class
        If IsNothing(source) Then Return Nothing
        Dim off As Integer = -1
        Dim L = HkbLayout
        If source.ClassName.Equals("hkbEvaluateExpressionModifier", StringComparison.OrdinalIgnoreCase) Then
            off = L.EvalExprData
        ElseIf source.ClassName.Equals("hkbEventDrivenModifier", StringComparison.OrdinalIgnoreCase) Then
            off = L.EventDrivenModifier
        ElseIf source.ClassName.Equals("BSiStateTaggingGenerator", StringComparison.OrdinalIgnoreCase) Then
            off = L.StateTagGenerator
        ElseIf source.ClassName.Equals("BSSynchronizedClipGenerator", StringComparison.OrdinalIgnoreCase) Then
            off = L.SyncClipGenerator
        ElseIf source.ClassName.Equals("DynamicAnimationTaggingGenerator", StringComparison.OrdinalIgnoreCase) Then
            off = L.DynTagGenerator
        Else
            Return Nothing
        End If
        Return New HkbSingleChildGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Name = ResolveLocalString(source.RelativeOffset + &H38),
            .ChildObject = If(off < 0, Nothing, ResolveGlobalObject(source.RelativeOffset + off))
        }
    End Function

    ''' <summary>BSIsActiveModifier: activa/desactiva por hasta 5 flags. Enable MEDIDO SSE +0x48 / FO4 +0x50;
    ''' los flags son BYTES consecutivos desde SSE +0x50 / FO4 +0x58 (valen 0 o 1 — invariante testeada).</summary>
    Public Function ParseIsActiveModifier(source As HkxVirtualObjectGraph_Class) As HkbIsActiveModifierGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSIsActiveModifier", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSIsActiveModifier(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Dim r As New HkbIsActiveModifierGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .Enable = (L.ModEnable >= 0 AndAlso ReadByte(rel + L.ModEnable) <> 0)}
        If L.IsActiveFlags >= 0 Then
            For i = 0 To 9
                r.ActiveFlags.Add(CInt(ReadByte(rel + L.IsActiveFlags + i)))
            Next
        End If
        Return r
    End Function

    ''' <summary>BSCyclicBlendTransitionGenerator: mezcla ciclica sobre un blender. MEDIDO:
    ''' m_pBlenderGenerator SSE +0x50 / FO4 +0x90 (153/153 y 77/77); los eventos van en bloques de
    ''' 0x10 desde SSE +0x58 (2) / FO4 +0x98 (4); el parametro de blend SSE +0x7C / FO4 +0xDC.</summary>
    Public Function ParseCyclicBlendTransitionGenerator(source As HkxVirtualObjectGraph_Class) As HkbCyclicBlendGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSCyclicBlendTransitionGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSCyclicBlendTransitionGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Dim r As New HkbCyclicBlendGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .BlenderGeneratorObject = If(L.CyclicBlender < 0, Nothing, ResolveGlobalObject(rel + L.CyclicBlender)),
            .BlendParameter = ReadSingleAt(rel, L.CyclicBlendParam)}
        If L.CyclicEvents >= 0 Then
            For i = 0 To L.CyclicEventCount - 1
                r.EventIds.Add(ReadInt32(rel + L.CyclicEvents + i * &H10))
            Next
        End If
        Return r
    End Function

    ''' <summary>BSBoneSwitchGenerator: reparte el cuerpo entre generadores por peso de hueso.
    ''' MEDIDO: pDefaultGenerator SSE +0x50 / FO4 +0x90 ; array ChildrenA SSE +0x58 / FO4 +0x98.
    ''' (Ambos nombres CONFIRMADOS en el pool de reflexion del binario.)</summary>
    Public Function ParseBoneSwitchGenerator(source As HkxVirtualObjectGraph_Class) As HkbBoneSwitchGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSBoneSwitchGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSBoneSwitchGenerator(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Dim r As New HkbBoneSwitchGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .DefaultGeneratorObject = If(L.BoneSwitchDefault < 0, Nothing, ResolveGlobalObject(rel + L.BoneSwitchDefault))}
        If L.BoneSwitchChildren >= 0 Then r.BoneDatas.AddRange(ReadObjectReferenceArray(rel + L.BoneSwitchChildren))
        Return r
    End Function

    ''' <summary>BSBoneSwitchGeneratorBoneData: un generador + su mascara de pesos de hueso.
    ''' m_pGenerator@+0x30 y m_spBoneWeight@+0x38 — MEDIDOS IDENTICOS en los dos juegos
    ''' (137/137 SSE y 27/27 FO4 tienen el hkbBoneWeightArray en +0x38).</summary>
    Public Function ParseBoneSwitchBoneData(source As HkxVirtualObjectGraph_Class) As HkbBoneSwitchBoneDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSBoneSwitchGeneratorBoneData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSBoneSwitchGeneratorBoneData(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Return New HkbBoneSwitchBoneDataGraph_Class With {
            .SourceObject = source,
            .GeneratorObject = ResolveGlobalObject(rel + &H30),
            .BoneWeightsObject = ResolveGlobalObject(rel + &H38)
        }
    End Function

    ''' <summary>hkbLayer: una capa de hkbLayerGenerator = generador + peso + mascara de huesos.
    ''' MEDIDO en FO4 (121 instancias): m_generator@+0x30, m_weight@+0x48. No aparece en el corpus SSE.</summary>
    Public Function ParseLayer(source As HkxVirtualObjectGraph_Class) As HkbLayerGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbLayer", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbLayer(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Dim r As New HkbLayerGraph_Class With {
            .SourceObject = source,
            .GeneratorObject = If(L.LayerGenerator < 0, Nothing, ResolveGlobalObject(rel + L.LayerGenerator)),
            .Weight = ReadSingleAt(rel, L.LayerWeight)}
        For Each gf In GetGlobalFixupsInRange(rel, source.Size)
            Dim t = GetObject(gf.TargetRelativeOffset)
            If t IsNot Nothing AndAlso t.ClassName.Equals("hkbBoneWeightArray", StringComparison.OrdinalIgnoreCase) Then r.BoneWeightsObject = t
        Next
        Return r
    End Function

    ''' <summary>hkbTwistModifier: tuerce una cadena de huesos. Nombres CONFIRMADOS por los bindings
    ''' del propio dato (memberPath = 'twistAngle', 'startBoneIndex', 'endBoneIndex').
    ''' Offsets MEDIDOS: eje SSE +0x50/54/58 y FO4 +0x60/64/68 ; twistAngle SSE +0x60 / FO4 +0x70.</summary>
    Public Function ParseTwistModifier(source As HkxVirtualObjectGraph_Class) As HkbTwistModifierGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbTwistModifier", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbTwistModifier(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Return New HkbTwistModifierGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .Enable = (L.ModEnable >= 0 AndAlso ReadByte(rel + L.ModEnable) <> 0),
            .AxisX = ReadSingleAt(rel, L.TwistAxis),
            .AxisY = ReadSingleAt(rel, If(L.TwistAxis < 0, -1, L.TwistAxis + 4)),
            .AxisZ = ReadSingleAt(rel, If(L.TwistAxis < 0, -1, L.TwistAxis + 8)),
            .TwistAngle = ReadSingleAt(rel, L.TwistAngle)
        }
    End Function

    ''' <summary>Nodos con UN evento + payload opcional. BSEventOnDeactivateModifier:
    ''' enable SSE +0x48 / FO4 +0x50 ; eventId SSE +0x50 / FO4 +0x58 ; payload SSE +0x58 / FO4 +0x60.</summary>
    Public Function ParseEventOnDeactivateModifier(source As HkxVirtualObjectGraph_Class) As HkbEventModifierGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSEventOnDeactivateModifier", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSEventOnDeactivateModifier(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Return New HkbEventModifierGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .Enable = (L.ModEnable >= 0 AndAlso ReadByte(rel + L.ModEnable) <> 0),
            .EventId = If(L.DeactEventId < 0, -1, ReadInt32(rel + L.DeactEventId)),
            .PayloadObject = If(L.DeactPayload < 0, Nothing, ResolveGlobalObject(rel + L.DeactPayload))
        }
    End Function

    ''' <summary>hkbPoweredRagdollControlsModifier / hkbKeyframeBonesModifier: modifiers que apuntan a
    ''' una mascara de huesos. Resueltos por CLASE del destino (hkbBoneIndexArray / hkbBoneWeightArray),
    ''' que es 1 de cada uno y vale para ambos juegos sin constante.</summary>
    Public Function ParseBoneMaskModifier(source As HkxVirtualObjectGraph_Class) As HkbBoneMaskModifierGraph_Class
        If IsNothing(source) Then Return Nothing
        If Not (source.ClassName.Equals("hkbPoweredRagdollControlsModifier", StringComparison.OrdinalIgnoreCase) OrElse
                source.ClassName.Equals("hkbKeyframeBonesModifier", StringComparison.OrdinalIgnoreCase) OrElse
                source.ClassName.Equals("hkbRigidBodyRagdollControlsModifier", StringComparison.OrdinalIgnoreCase)) Then Return Nothing
        Dim r As New HkbBoneMaskModifierGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Name = ResolveLocalString(source.RelativeOffset + &H38),
            .Enable = (HkbLayout.ModEnable >= 0 AndAlso ReadByte(source.RelativeOffset + HkbLayout.ModEnable) <> 0)}
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim t = GetObject(gf.TargetRelativeOffset)
            If t Is Nothing Then Continue For
            If t.ClassName.Equals("hkbBoneIndexArray", StringComparison.OrdinalIgnoreCase) Then r.BoneIndexObject = t
            If t.ClassName.Equals("hkbBoneWeightArray", StringComparison.OrdinalIgnoreCase) Then r.BoneWeightObject = t
        Next
        Return r
    End Function

    ''' <summary>BSModifyOnceModifier: aplica un modifier al activarse y otro al desactivarse.
    ''' Los dos hijos se resuelven por CLASE (son las unicas refs a *Modifier del nodo).</summary>
    Public Function ParseModifyOnceModifier(source As HkxVirtualObjectGraph_Class) As HkbModifyOnceGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSModifyOnceModifier", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSModifyOnceModifier(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim r As New HkbModifyOnceGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .Enable = (HkbLayout.ModEnable >= 0 AndAlso ReadByte(source.RelativeOffset + HkbLayout.ModEnable) <> 0)}
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size).OrderBy(Function(x) x.SourceRelativeOffset)
            Dim t = GetObject(gf.TargetRelativeOffset)
            If t Is Nothing OrElse Not t.ClassName.EndsWith("Modifier", StringComparison.OrdinalIgnoreCase) Then Continue For
            r.Modifiers.Add(t)
        Next
        Return r
    End Function

    ''' <summary>BSAssignVariablesModifier (FO4-only: 205 instancias, 0 en SSE): asigna valores a
    ''' variables del grafo. Los NOMBRES salen del oraculo de bindings del propio dato
    ''' (`enable`, `intVariable1..4`, `intValue1`, `floatVariable1..4`, `floatValue1..2`).
    ''' <para>Estructura MEDIDA: enable@+0x50 y luego 4 pares (int32 indiceDeVariable, float32 valor)
    ''' desde +0x58 con stride 8. La orientacion del par esta probada por VALOR: los flotantes reales
    ''' (60, 60, 5, 75) caen SIEMPRE en el segundo miembro y el primero vale 0 — si fuese al reves, un
    ''' "indice" de 60.0f seria basura. Hay un segundo bloque de pares en +0xF8 (enteros).</para>
    ''' La correspondencia exacta par-a-nombre (cual es floatVariable1 vs floatValue1) NO esta medida:
    ''' por eso se exponen como lista indexada y no con los nombres Havok.</summary>
    Public Function ParseAssignVariablesModifier(source As HkxVirtualObjectGraph_Class) As HkbAssignVariablesGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSAssignVariablesModifier", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSAssignVariablesModifier(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Dim r As New HkbAssignVariablesGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .Enable = (L.ModEnable >= 0 AndAlso ReadByte(rel + L.ModEnable) <> 0)}
        If L.AssignFloatPairs >= 0 Then
            For i = 0 To 3
                r.FloatAssignments.Add(New HkbAssignPair_Class With {
                    .VariableIndex = ReadInt32(rel + L.AssignFloatPairs + i * 8),
                    .Value = ReadSingle(rel + L.AssignFloatPairs + i * 8 + 4)})
            Next
        End If
        If L.AssignIntPairs >= 0 Then
            For i = 0 To 3
                r.IntAssignments.Add(New HkbAssignPair_Class With {
                    .VariableIndex = ReadInt32(rel + L.AssignIntPairs + i * 8),
                    .Value = CSng(ReadInt32(rel + L.AssignIntPairs + i * 8 + 4))})
            Next
        End If
        Return r
    End Function

    ''' <summary>BSLookAtModifier: head-tracking, con un ARRAY de huesos. Nombres del oraculo de bindings
    ''' (`bones:N/index|fwdAxisLS|upAxisLS|limitAngleDegrees|onGain|offGain|...`).
    ''' <para>LOS DOS JUEGOS MEDIDOS, cada uno con su propio struct — no es solo otro offset:
    ''' <list type="bullet">
    ''' <item>SSE: array@+0x58, **stride 0x40**; dentro: index@+0x00, fwdAxis@+0x10, limitAngle@+0x20,
    ''' onGain@+0x24, offGain@+0x28. Tiene UN solo eje.</item>
    ''' <item>FO4: array@+0x270, **stride 0x210** (528B!); dentro: fwdAxis@+0x10, upAxis@+0x20,
    ''' limitAngle@+0x34, onGain@+0x48, offGain@+0x4C. Tiene DOS ejes y el indice va empaquetado.</item>
    ''' </list>
    ''' Strides decididos por el invariante del EJE UNITARIO, no por tanteo: SSE 521/521 elementos con eje
    ''' unitario e indice valido con 0x40 (0x50/0x60 caen a 29-39%); FO4 102/102 con 0x210 (0x200 y 0x220
    ''' caen a 34-60%).</para></summary>
    Public Function ParseLookAtModifier(source As HkxVirtualObjectGraph_Class) As HkbLookAtModifierGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("BSLookAtModifier", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_BSLookAtModifier(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim L = HkbLayout
        Dim r As New HkbLookAtModifierGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .Enable = (L.ModEnable >= 0 AndAlso ReadByte(rel + L.ModEnable) <> 0)}
        If L.LookAtBones < 0 OrElse L.LookAtBoneStride <= 0 Then Return r
        Dim h = ReadArrayHeader(rel + L.LookAtBones)
        If h.Count <= 0 OrElse h.DataRelativeOffset < 0 Then Return r
        For i = 0 To h.Count - 1
            Dim b = h.DataRelativeOffset + i * L.LookAtBoneStride
            Dim bone As New HkbLookAtBone_Class With {
                .BoneIndex = If(L.LookAtIndex < 0, -1, ReadInt32(b + L.LookAtIndex)),
                .FwdAxisX = ReadSingle(b + &H10), .FwdAxisY = ReadSingle(b + &H14), .FwdAxisZ = ReadSingle(b + &H18),
                .LimitAngleDegrees = ReadSingle(b + L.LookAtLimitAngle),
                .OnGain = ReadSingle(b + L.LookAtOnGain),
                .OffGain = ReadSingle(b + L.LookAtOffGain)}
            If L.LookAtUpAxis >= 0 Then
                bone.UpAxisX = ReadSingle(b + L.LookAtUpAxis)
                bone.UpAxisY = ReadSingle(b + L.LookAtUpAxis + 4)
                bone.UpAxisZ = ReadSingle(b + L.LookAtUpAxis + 8)
            End If
            r.Bones.Add(bone)
        Next
        Return r
    End Function

    ''' <summary>Clases con parser de CAMPOS tipado (offsets medidos y validados). Cualquier otra clase
    ''' sólo obtiene la vista PARCIAL de <see cref="ParseNode"/> (nombre + refs), que NO es un parseo de
    ''' sus campos. Esta lista es la ÚNICA fuente de verdad de cobertura: las herramientas de auditoría
    ''' deben consultarla en vez de mantener su propia tabla (una tabla a mano ya fue fuente de error).</summary>
    Public Shared Function HasTypedFieldParser(className As String) As Boolean
        If String.IsNullOrEmpty(className) Then Return False
        Static typed As String() = {
            "hkbCharacterStringData", "hkbBehaviorGraphStringData", "hkbProjectStringData",
            "hkbClipGenerator", "hkbBlenderGenerator", "hkbBlenderGeneratorChild",
            "hkbStateMachine", "hkbStateMachineStateInfo", "hkbStateMachineTransitionInfoArray",
            "hkbClipTriggerArray", "hkbBoneIndexArray", "hkbVariableValueSet",
            "hkbStateMachineEventPropertyArray", "hkbVariableBindingSet", "hkbExpressionDataArray",
            "hkbStringEventPayload", "hkbMirroredSkeletonInfo", "hkbBoneWeightArray",
            "hkbFootIkDriverInfo", "hkbHandIkDriverInfo", "hkbBlendingTransitionEffect",
            "BGSGamebryoSequenceGenerator", "hkbBehaviorGraph", "hkbBehaviorGraphData",
            "hkbBehaviorReferenceGenerator", "hkbExpressionCondition", "hkRootLevelContainer",
            "hkbModifierGenerator", "hkbModifierList", "hkbManualSelectorGenerator",
            "hkbEvaluateExpressionModifier", "hkbEventDrivenModifier", "BSiStateTaggingGenerator",
            "BSSynchronizedClipGenerator", "BSIsActiveModifier", "BSCyclicBlendTransitionGenerator",
            "BSBoneSwitchGenerator", "BSBoneSwitchGeneratorBoneData",
            "DynamicAnimationTaggingGenerator", "hkbLayer", "hkbTwistModifier",
            "BSEventOnDeactivateModifier", "hkbPoweredRagdollControlsModifier",
            "hkbKeyframeBonesModifier", "hkbRigidBodyRagdollControlsModifier", "BSModifyOnceModifier",
            "BSAssignVariablesModifier", "BSLookAtModifier"}
        Return typed.Any(Function(t) t.Equals(className, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>Vista PARCIAL de un nodo hkb/BS. NO es un parser de la clase: sirve para las que no
    ''' tienen funcion propia (BSCyclicBlendTransitionGenerator, hkbPoseMatchingGenerator,
    ''' BSBoneSwitchGenerator, hkbLayerGenerator, hkbReferencePoseGenerator y los ~20 *Modifier).
    ''' <para>Devuelve HasTypedFields=False para esas clases. Extrae SOLO lo verificable sin su layout:
    ''' m_name@+0x38 (base hkbNode — MEDIDO presente en el 100% de las instancias de todas las clases
    ''' de nodo del corpus vanilla), los generadores hijos y los objetos referenciados, ambos por CLASE
    ''' del fixup. NO inventa campos escalares: para eso hace falta medir la clase concreta.</para></summary>
    Public Function ParseNode(source As HkxVirtualObjectGraph_Class) As HkbNodeGraph_Class
        If IsNothing(source) Then Return Nothing
        Dim result As New HkbNodeGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Name = ResolveLocalString(source.RelativeOffset + &H38),
            .HasTypedFields = HasTypedFieldParser(source.ClassName)
        }
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt Is Nothing Then Continue For
            If tgt.ClassName.Equals("hkbVariableBindingSet", StringComparison.OrdinalIgnoreCase) Then
                result.Bindings.AddRange(ParseVariableBindingSet(tgt))
            ElseIf IsGeneratorClass(tgt.ClassName) Then
                result.ChildGenerators.Add(tgt)
            Else
                result.ReferencedObjects.Add(tgt)
            End If
        Next
        Return result
    End Function

    ''' <summary>Expone IsGeneratorClass para herramientas de auditoria.</summary>
    Public Function IsGeneratorClassPublic(className As String) As Boolean
        Return IsGeneratorClass(className)
    End Function

    Public Function ParseModifier(source As HkxVirtualObjectGraph_Class) As HkbModifierGraph_Class
        If IsNothing(source) OrElse source.ClassName.IndexOf("Modifier", StringComparison.OrdinalIgnoreCase) < 0 Then Return Nothing
        Dim result As New HkbModifierGraph_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .Name = ReadNodeName(source)
        }
        result.ReferencedClasses.AddRange(ReadReferencedClasses(source))
        Return result
    End Function

    ''' <summary>hkbModifierList → la lista de modifiers que agrupa.</summary>
    Public Function ParseModifierList(source As HkxVirtualObjectGraph_Class) As HkbModifierListGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbModifierList", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkbModifierList(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Dim result As New HkbModifierListGraph_Class With {.SourceObject = source, .Name = ReadNodeName(source)}
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt IsNot Nothing AndAlso tgt.ClassName.IndexOf("Modifier", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Dim m = ParseModifier(tgt)
                If m IsNot Nothing Then result.Modifiers.Add(m)
            End If
        Next
        Return result
    End Function

    ' Clases de los objetos referenciados por un nodo (excluye el ruido de hkbVariableBindingSet).
    Private Function ReadReferencedClasses(source As HkxVirtualObjectGraph_Class) As List(Of String)
        Dim result As New List(Of String)
        For Each gf In GetGlobalFixupsInRange(source.RelativeOffset, source.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt IsNot Nothing AndAlso Not tgt.ClassName.Equals("hkbVariableBindingSet", StringComparison.OrdinalIgnoreCase) Then
                result.Add(tgt.ClassName)
            End If
        Next
        Return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
    End Function

End Class

Public Class HkbCharacterStringDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property CharacterName As String
    Public Property RigName As String           ' skeleton (CharacterAssets\skeleton.hkt)
    Public Property RagdollName As String        ' ragdoll
    Public Property BehaviorFilename As String   ' Behaviors\...RootBehavior.hkx
    Public ReadOnly Property AnimationFilenames As New List(Of String)
    Public ReadOnly Property AllStrings As New List(Of String)
End Class

Public Class HkbBehaviorGraphStringDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property EventNames As New List(Of String)       ' +0x10 (confirmado)
    Public ReadOnly Property VariableNames As New List(Of String)    ' +0x30 (por posición, tentativo)
    Public ReadOnly Property AttributeNames As New List(Of String)   ' +0x40 (por posición, tentativo)
End Class

Public Class HkbProjectStringDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property Strings As New List(Of String)
    Public ReadOnly Property CharacterFilenames As New List(Of String)
End Class

' Los offsets de esta clase DIFIEREN entre juegos: salen de HkbLayout, no hay ninguno fijo.
Public Class HkbClipGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String            ' nodo, m_name@+0x38 (igual en los dos juegos)
    Public Property AnimationName As String   ' .hkt que reproduce
    Public Property TriggersObject As HkxVirtualObjectGraph_Class  ' hkbClipTriggerArray
    Public Property CropStartLocalTime As Single
    Public Property CropEndLocalTime As Single
    Public Property StartTime As Single
    Public Property PlaybackSpeed As Single   ' 1.0 = normal
    Public Property EnforcedDuration As Single
    Public Property AnimationBindingIndex As Integer  ' int16 (-1 = sin binding)
    Public Property PlaybackMode As Integer           ' int8 enum (loop/once/...)
    Public Property FlagsRaw As Integer               ' int8 hkbClipGenerator::flags
End Class

Public Class HkbBlenderGeneratorChildGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Weight As Single             ' +0x40
    Public Property WorldFromModelWeight As Single  ' +0x44
    Public Property GeneratorSummary As String   ' qué generador aporta a la mezcla
End Class

Public Class HkbBlenderGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property Children As New List(Of HkbBlenderGeneratorChildGraph_Class)
End Class

Public Class HkbStateMachineGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property States As New List(Of HkbStateInfoGraph_Class)
End Class

Public Class HkbStateInfoGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property StateId As Integer                               ' m_stateId@+0x68
    Public Property GeneratorObject As HkxVirtualObjectGraph_Class    ' qué produce la pose del estado
    Public Property TransitionsObject As HkxVirtualObjectGraph_Class  ' hkbStateMachineTransitionInfoArray (si hay)
    Public Property GeneratorSummary As String                       ' "clip → Animations\X.hkt" / "hkbBlenderGenerator → [..]" / ...
    Public ReadOnly Property Transitions As New List(Of HkbTransitionGraph_Class)
End Class

Public Class HkbTransitionGraph_Class
    Public Property EventId As Integer     ' índice en hkbBehaviorGraphStringData.EventNames
    Public Property ToStateId As Integer   ' StateId destino dentro del state-machine
End Class

Public Class HkbBlendingTransitionEffectGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Duration As Single                      ' segundos de blend
    Public Property ToGeneratorStartTimeFraction As Single
End Class

Public Class HkbGamebryoSequenceGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property SequenceName As String                  ' NiControllerSequence del NIF
    Public Property BlendModeFunction As Integer
    Public Property Percent As Single
End Class

Public Class HkbBehaviorGraphGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property RootGeneratorObject As HkxVirtualObjectGraph_Class
    Public Property RootGeneratorSummary As String
    Public Property DataObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbBehaviorGraphDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property StringData As HkbBehaviorGraphStringDataGraph_Class
    Public Property InitialValues As HkbVariableValueSetGraph_Class
End Class

Public Class HkbBehaviorReferenceGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property BehaviorName As String   ' otro archivo Behaviors\X.hkx
End Class

''' <summary>Generador que envuelve a otros (selector manual, synchronized clip, i-state tagging...).</summary>
Public Class HkbWrapperGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String
    Public Property Name As String
    Public ReadOnly Property ChildObjects As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ChildSummaries As New List(Of String)
End Class

''' <summary>Vista generica de un nodo del grafo (cualquier clase).</summary>
Public Class HkbNodeGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String
    Public Property Name As String
    ''' <summary>False = de esta clase SÓLO se conocen el nombre y las referencias; sus campos
    ''' propios NO están identificados. No contarla como "parseada".</summary>
    Public Property HasTypedFields As Boolean
    Public ReadOnly Property ChildGenerators As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ReferencedObjects As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Bindings As New List(Of HkbVariableBinding_Class)
End Class

Public Class HkbModifierGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ModifierObject As HkxVirtualObjectGraph_Class
    Public Property GeneratorObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbModifierListTypedGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property Modifiers As New List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HkbManualSelectorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property Generators As New List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HkbSingleChildGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String
    Public Property Name As String
    Public Property ChildObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbIsActiveModifierGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Enable As Boolean
    ''' <summary>Flags crudos (0/1) leidos como bytes consecutivos. No se les pone nombre
    ''' porque su semantica individual no esta medida.</summary>
    Public ReadOnly Property ActiveFlags As New List(Of Integer)
End Class

Public Class HkbCyclicBlendGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property BlenderGeneratorObject As HkxVirtualObjectGraph_Class
    Public Property BlendParameter As Single
    Public ReadOnly Property EventIds As New List(Of Integer)
End Class

Public Class HkbBoneSwitchGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property DefaultGeneratorObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property BoneDatas As New List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HkbBoneSwitchBoneDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property GeneratorObject As HkxVirtualObjectGraph_Class
    Public Property BoneWeightsObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbLayerGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property GeneratorObject As HkxVirtualObjectGraph_Class
    Public Property Weight As Single
    Public Property BoneWeightsObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbTwistModifierGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Enable As Boolean
    Public Property AxisX As Single
    Public Property AxisY As Single
    Public Property AxisZ As Single
    Public Property TwistAngle As Single
End Class

Public Class HkbEventModifierGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Enable As Boolean
    Public Property EventId As Integer
    Public Property PayloadObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbBoneMaskModifierGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String
    Public Property Name As String
    Public Property Enable As Boolean
    Public Property BoneIndexObject As HkxVirtualObjectGraph_Class
    Public Property BoneWeightObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbModifyOnceGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Enable As Boolean
    Public ReadOnly Property Modifiers As New List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HkbAssignPair_Class
    Public Property VariableIndex As Integer
    Public Property Value As Single
End Class

Public Class HkbAssignVariablesGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Enable As Boolean
    Public ReadOnly Property FloatAssignments As New List(Of HkbAssignPair_Class)
    Public ReadOnly Property IntAssignments As New List(Of HkbAssignPair_Class)
End Class

Public Class HkbLookAtBone_Class
    Public Property BoneIndex As Integer          ' -1 = no aislado en este formato
    Public Property FwdAxisX As Single
    Public Property FwdAxisY As Single
    Public Property FwdAxisZ As Single
    Public Property UpAxisX As Single
    Public Property UpAxisY As Single
    Public Property UpAxisZ As Single
    Public Property LimitAngleDegrees As Single
    Public Property OnGain As Single
    Public Property OffGain As Single
End Class

Public Class HkbLookAtModifierGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Enable As Boolean
    Public ReadOnly Property Bones As New List(Of HkbLookAtBone_Class)
End Class

Public Class HkbModifierGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String
    Public Property Name As String                          ' m_name@+0x38
    Public ReadOnly Property ReferencedClasses As New List(Of String)  ' bone arrays, driver-info, sub-modifiers…
End Class

Public Class HkbModifierListGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property Modifiers As New List(Of HkbModifierGraph_Class)
End Class

' --- Soporte hkb (campos tipados) ---
Public Class HkbClipTrigger_Class
    Public Property LocalTime As Single
    Public Property EventId As Integer
End Class
Public Class HkbClipTriggerArrayGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property Triggers As New List(Of HkbClipTrigger_Class)
End Class

Public Class HkbVariableValue_Class
    Public Property AsInt As Integer       ' word crudo
    Public Property AsFloat As Single      ' mismo word reinterpretado (el tipo real lo da hkbBehaviorGraphData)
End Class
Public Class HkbVariableValueSetGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property Values As New List(Of HkbVariableValue_Class)
End Class

Public Class HkbEventProperty_Class
    Public Property EventId As Integer
    Public Property PayloadObject As HkxVirtualObjectGraph_Class
End Class

Public Class HkbVariableBinding_Class
    Public Property MemberPath As String   ' miembro del nodo al que se liga (ej. "bIsActive0")
    ''' <summary>Índice en <c>hkbBehaviorGraphStringData.VariableNames</c> SÓLO si
    ''' <see cref="BindingType"/> = 0. Con BindingType = 1 indexa las PROPERTIES del character
    ''' (otro espacio de nombres) y resolverlo contra VariableNames da un nombre equivocado
    ''' o fuera de rango.</summary>
    Public Property VariableIndex As Integer
    Public Property BitIndex As Integer     ' int8; -1 = el binding no es de un bit
    ''' <summary>hkbVariableBindingSet::Binding::m_bindingType — 0 = variable del grafo, 1 = property
    ''' del character. Medido: sólo toma 0 ó 1 en 13.580 bindings de FO4+SSE.</summary>
    Public Property BindingType As Integer
End Class

Public Class HkbExpressionData_Class
    Public Property Expression As String   ' ej. "iCombatState = 0"
    Public Property AssignmentVariableIndex As Integer
    Public Property AssignmentEventIndex As Integer
End Class

Public Class HkbMirroredSkeletonInfoGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property MirrorAxisX As Single
    Public Property MirrorAxisY As Single
    Public Property MirrorAxisZ As Single
    Public Property MirrorAxisW As Single
    Public ReadOnly Property BonePairMap As New List(Of Integer)  ' [i] = índice de hueso espejo de i
End Class

Public Class HkbFootIkDriverInfoGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property LegCount As Integer
    Public Property RaycastDistanceUp As Single
    Public Property RaycastDistanceDown As Single
    Public Property OriginalGroundHeightMS As Single
    Public Property VerticalOffset As Single
    Public Property CollisionUpAxisMS As Single
End Class

Public Class HkbHandIkDriverInfoGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property Hands As New List(Of HkbHandIkHand_Class)
End Class

' Una mano del hand-IK: ejes/offset en local space + cadena hombro→codo→muñeca (índices de hueso).
Public Class HkbHandIkHand_Class
    Public Property ElbowAxisLS As HkxVector4Graph_Class
    Public Property BackHandNormalLS As HkxVector4Graph_Class
    Public Property HandOffsetLS As HkxVector4Graph_Class
    Public Property HandOrientationLS As HkxQuaternionGraph_Class
    Public Property MaxElbowAngleDegrees As Single
    Public Property MinElbowAngleDegrees As Single
    Public Property ShoulderIndex As Short
    Public Property ShoulderSiblingIndex As Short
    Public Property ElbowIndex As Short
    Public Property ElbowSiblingIndex As Short
    Public Property WristIndex As Short
    Public Property EnforceEndPosition As Boolean
    Public Property EnforceEndRotation As Boolean
End Class
