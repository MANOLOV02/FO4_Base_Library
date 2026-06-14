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
        Dim rel = source.RelativeOffset

        Dim result As New HkbCharacterStringDataGraph_Class With {
            .SourceObject = source,
            .CharacterName = ResolveLocalString(rel + &HA0),
            .RigName = ResolveLocalString(rel + &HA8),
            .RagdollName = ResolveLocalString(rel + &HB0),
            .BehaviorFilename = ResolveLocalString(rel + &HB8)
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
        Dim rel = source.RelativeOffset

        Dim result As New HkbBehaviorGraphStringDataGraph_Class With {.SourceObject = source}
        result.EventNames.AddRange(ReadStringPtrArray(rel + &H10))     ' confirmado = eventos
        result.VariableNames.AddRange(ReadStringPtrArray(rel + &H30))  ' por posición (tentativo)
        result.AttributeNames.AddRange(ReadStringPtrArray(rel + &H40)) ' por posición (tentativo)
        Return result
    End Function

    ''' <summary>hkbProjectStringData: paths del proyecto (character files, animation/behavior roots).</summary>
    Public Function ParseProjectStringData(source As HkxVirtualObjectGraph_Class) As HkbProjectStringDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbProjectStringData", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim result As New HkbProjectStringDataGraph_Class With {.SourceObject = source}
        result.Strings.AddRange(ReadAllReferencedStrings(source).Distinct(StringComparer.OrdinalIgnoreCase))
        result.CharacterFilenames.AddRange(
            result.Strings.Where(Function(s) s.IndexOf("Characters\", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                                             s.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase)))
        Return result
    End Function

    ' Lee un hkArray<hkStringPtr> (cada elemento = puntero a string, stride = PointerSizeValue).
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

    ' --------------------- FASE 2: topología state-machine → clips ---------------------
    ' Offsets confirmados con --dump (Alien/varios): los nodos hkb (generadores) llevan m_name@+0x38;
    ' hkbStateMachine m_states@+0xD0 (hkArray<ptr>); hkbClipGenerator m_animationName@+0x90;
    ' hkbStateMachineStateInfo m_name@+0x60. El m_generator/m_transitions del state-info se resuelven
    ' por CLASE del objeto referenciado (robusto, sin fijar offsets de ref frágiles).

    ''' <summary>Nombre del nodo (m_name@+0x38) de cualquier hkb generator/modifier.</summary>
    Public Function ReadNodeName(obj As HkxVirtualObjectGraph_Class) As String
        If IsNothing(obj) Then Return ""
        Return ResolveLocalString(obj.RelativeOffset + &H38)
    End Function

    ''' <summary>hkbClipGenerator: nodo + la animación (.hkt) que reproduce + params de playback.
    ''' Offsets verificados con --dump (PlaybackSpeed=1.0@+0xB0, AnimationBindingIndex=-1@+0xBC);
    ''' los floats de crop/startTime siguen el orden de miembros de hkbClipGenerator (best-effort, sin reflection).</summary>
    Public Function ParseClipGenerator(source As HkxVirtualObjectGraph_Class) As HkbClipGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbClipGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Return New HkbClipGeneratorGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + &H38),
            .AnimationName = ResolveLocalString(rel + &H90),
            .TriggersObject = ResolveGlobalObject(rel + &H98),
            .CropStartLocalTime = ReadSingle(rel + &HA0),
            .CropEndLocalTime = ReadSingle(rel + &HA4),
            .StartTime = ReadSingle(rel + &HA8),
            .PlaybackSpeed = ReadSingle(rel + &HB0),
            .EnforcedDuration = ReadSingle(rel + &HB4),
            .AnimationBindingIndex = CInt(ReadInt16(rel + &HBC)),
            .PlaybackMode = CInt(ReadByte(rel + &HBE)),
            .FlagsRaw = CInt(ReadByte(rel + &HBF))
        }
    End Function

    ''' <summary>hkbBlenderGenerator: nombre + children (cada uno con su weight y el generador que mezcla).</summary>
    Public Function ParseBlenderGenerator(source As HkxVirtualObjectGraph_Class) As HkbBlenderGeneratorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBlenderGenerator", StringComparison.OrdinalIgnoreCase) Then Return Nothing
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
            .Weight = ReadSingle(source.RelativeOffset + &H40),
            .WorldFromModelWeight = ReadSingle(source.RelativeOffset + &H44),
            .GeneratorSummary = DescribeGenerator(gen)
        }
    End Function

    ''' <summary>hkbStateMachine: nombre + estados (refs a hkbStateMachineStateInfo).</summary>
    Public Function ParseStateMachine(source As HkxVirtualObjectGraph_Class) As HkbStateMachineGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachine", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim result As New HkbStateMachineGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H38)
        }
        For Each stateObj In ReadObjectReferenceArray(source.RelativeOffset + &HD0)
            Dim st = ParseStateInfo(stateObj)
            If st IsNot Nothing Then result.States.Add(st)
        Next
        Return result
    End Function

    ''' <summary>hkbStateMachineStateInfo: nombre + generador (qué produce la pose) + transiciones.
    ''' El generador y las transiciones se identifican por la CLASE del objeto referenciado.</summary>
    Public Function ParseStateInfo(source As HkxVirtualObjectGraph_Class) As HkbStateInfoGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachineStateInfo", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim result As New HkbStateInfoGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H60),
            .StateId = ReadInt32(source.RelativeOffset + &H68)
        }
        ' Layout hkbStateMachineStateInfo (FO4 64-bit): los punteros van ... m_transitions(+0x50),
        ' m_generator(+0x58), m_name(+0x60). m_name@0x60 está confirmado (se lee arriba y da nombres
        ' correctos); m_generator es el puntero INMEDIATAMENTE anterior → +0x58, y m_transitions → +0x50.
        ' VERIFICADO empíricamente sobre 239 hkbStateMachineStateInfo reales de Fallout4 - Animations.ba2
        ' (Tools/StateInfoOffsetProbe, 2026-06-13): +0x58 es una clase generator en 239/239, y NINGÚN
        ' state-info tiene >1 referencia a clase generator. Se lee el campo por OFFSET (determinístico),
        ' reemplazando el viejo class-scan ("primer generator que aparezca en el rango"), que era una
        ' heurística (coincidía con +0x58 en los 239, pero era frágil ante múltiples refs a generator).
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
            leaves.Add("clip:" & ResolveLocalString(gen.RelativeOffset + &H90))
        ElseIf cn.Equals("hkbBehaviorReferenceGenerator", StringComparison.OrdinalIgnoreCase) Then
            leaves.Add("behavior:" & ResolveLocalString(gen.RelativeOffset + &H88))
        ElseIf cn.Equals("BGSGamebryoSequenceGenerator", StringComparison.OrdinalIgnoreCase) Then
            leaves.Add("gamebryo:" & ResolveLocalString(gen.RelativeOffset + &H88))
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

    ''' <summary>hkbStateMachineTransitionInfoArray → lista de (eventId, toStateId). Struct stride 0x40
    ''' (verificado por método de diferencias: count1→región 0x50, count2→región 0x90 ⇒ 0x40 + 0x10 pad).
    ''' eventId@elem+0x30, toStateId@elem+0x34.</summary>
    Public Function ParseTransitions(source As HkxVirtualObjectGraph_Class) As List(Of HkbTransitionGraph_Class)
        Dim result As New List(Of HkbTransitionGraph_Class)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbStateMachineTransitionInfoArray", StringComparison.OrdinalIgnoreCase) Then Return result
        Dim header = ReadArrayHeader(source.RelativeOffset + &H10)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        Const stride As Integer = &H40
        For i = 0 To header.Count - 1
            Dim e = header.DataRelativeOffset + (i * stride)
            result.Add(New HkbTransitionGraph_Class With {
                .EventId = ReadInt32(e + &H30),
                .ToStateId = ReadInt32(e + &H34)
            })
        Next
        Return result
    End Function

    ' --------------------- Ola 3: clases de soporte hkb (datos reales) ---------------------
    ' Offsets verificados con --dump. Todo a campos tipados.

    ''' <summary>hkbClipTriggerArray → triggers {localTime, eventId} (eventos disparados en tiempos del clip).</summary>
    Public Function ParseClipTriggerArray(source As HkxVirtualObjectGraph_Class) As HkbClipTriggerArrayGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbClipTriggerArray", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim h = ReadArrayHeader(source.RelativeOffset + &H10)
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
        Dim h = ReadArrayHeader(source.RelativeOffset + &H30)
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
        Dim h = ReadArrayHeader(source.RelativeOffset + &H10)
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
        Dim h = ReadArrayHeader(source.RelativeOffset + &H10)
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
        Dim h = ReadArrayHeader(source.RelativeOffset + &H10)
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * &H28)
                result.Add(New HkbVariableBinding_Class With {
                    .MemberPath = ResolveLocalString(e + 0),
                    .VariableIndex = ReadInt32(e + &H1C),
                    .BitIndex = ReadInt32(e + &H20)})
            Next
        End If
        Return result
    End Function

    ''' <summary>hkbExpressionDataArray → expresiones {expression, assignmentVariableIndex, assignmentEventIndex}.
    ''' Element@array+0x10, stride 0x18; expression@+0, assignVar@+8, assignEvt@+0xC.</summary>
    Public Function ParseExpressionDataArray(source As HkxVirtualObjectGraph_Class) As List(Of HkbExpressionData_Class)
        Dim result As New List(Of HkbExpressionData_Class)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbExpressionDataArray", StringComparison.OrdinalIgnoreCase) Then Return result
        Dim h = ReadArrayHeader(source.RelativeOffset + &H10)
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
        Return ResolveLocalString(source.RelativeOffset + &H10)
    End Function

    ''' <summary>hkbMirroredSkeletonInfo → eje de espejo + mapa de pares de hueso (bonePairMap[i] = hueso espejo de i).
    ''' mirrorAxis@+0x10 (vec4), bonePairMap (int16[])@+0x20.</summary>
    Public Function ParseMirroredSkeletonInfo(source As HkxVirtualObjectGraph_Class) As HkbMirroredSkeletonInfoGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbMirroredSkeletonInfo", StringComparison.OrdinalIgnoreCase) Then Return Nothing
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

    ''' <summary>hkbBoneWeightArray → pesos por hueso (float[])@+0x10 (máscaras de cuerpo parcial).</summary>
    Public Function ParseBoneWeightArray(source As HkxVirtualObjectGraph_Class) As List(Of Single)
        Dim result As New List(Of Single)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkbBoneWeightArray", StringComparison.OrdinalIgnoreCase) Then Return result
        Dim h = ReadArrayHeader(source.RelativeOffset + &H10)
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

    ''' <summary>Cualquier hkb*/BS* modifier → nombre + clases de sus objetos referenciados
    ''' (bone arrays, driver-info, sub-modifiers…). Sirve para IK (Foot/Hand), twist, ragdoll-controls, etc.</summary>
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

Public Class HkbClipGeneratorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String            ' nodo (+0x38)
    Public Property AnimationName As String   ' .hkt que reproduce (+0x90)
    Public Property TriggersObject As HkxVirtualObjectGraph_Class  ' hkbClipTriggerArray (+0x98)
    Public Property CropStartLocalTime As Single
    Public Property CropEndLocalTime As Single
    Public Property StartTime As Single
    Public Property PlaybackSpeed As Single   ' +0xB0 (1.0 = normal)
    Public Property EnforcedDuration As Single
    Public Property AnimationBindingIndex As Integer  ' +0xBC int16 (-1 = sin binding)
    Public Property PlaybackMode As Integer           ' +0xBE enum (loop/once/...)
    Public Property FlagsRaw As Integer               ' +0xBF int8 hkbClipGenerator::flags
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

' --- Ola 3: soporte hkb (campos tipados) ---
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
    Public Property VariableIndex As Integer
    Public Property BitIndex As Integer
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
