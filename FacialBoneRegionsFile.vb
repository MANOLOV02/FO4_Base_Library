Imports System.Text.Json
Imports System.Numerics

' ============================================================================
' Parser for the per-race face bone region JSON files shipped by vanilla FO4.
'
'   Meshes\Actors\Character\CharacterAssets\HumanRaceFacialBoneRegions<Gender>.txt
'   Meshes\Actors\Character\CharacterAssets\GhoulRaceFacialBoneRegions<Gender>.txt
'   Meshes\Actors\Character\CharacterAssets\PowerArmorRaceFacialBoneRegions<Gender>.txt
'
' Format (JSON array of region objects):
'   [
'     {
'       "AssociatedTintGroup": "Brows",
'       "BonesA": [
'         {
'           "Bone": "bone_C_MasterEyebrow",      // NOTE: without "skin_" prefix!
'           "Maxima": { Position{x,y,z}, Rotation{x,y,z}, Scale{x,y,z} },
'           "Minima": { ... }
'         },
'         ...
'       ],
'       "Defaults": { Position, Rotation, Scale },
'       "ID": 100000,                              // ← NPC FMRI value
'       "Name": "Eyebrows - Full"                  // ← English display name (FMRN)
'     },
'     ...
'   ]
'
' The ID field matches the uint32 FMRI read from NPC FMRI/FMRS subrecords.
' Applying FMRS means: look up by ID, iterate over BonesA, compute each bone's
' delta transform by lerping Minima↔Defaults↔Maxima with the 7 FMRS floats.
' ============================================================================

''' <summary>A single bone entry inside a FacialBoneRegion, with its min/max transforms.</summary>
Public Class FacialBoneEntry
    Public Property Bone As String = ""            ' Raw name from JSON (prefix "bone_", not "skin_bone_")
    Public Property MinimaPosition As Vector3
    Public Property MinimaRotation As Vector3      ' Euler degrees
    Public Property MinimaScale As Vector3         ' Additive offset (0 = no change, not 1.0)
    Public Property MaximaPosition As Vector3
    Public Property MaximaRotation As Vector3
    Public Property MaximaScale As Vector3
End Class

''' <summary>A single region from the FacialBoneRegions JSON file.</summary>
Public Class FacialBoneRegion
    Public Property ID As UInteger                 ' Matches NPC FMRI
    Public Property Name As String = ""            ' English display name (FMRN)
    Public Property AssociatedTintGroup As String = ""
    Public Property DefaultPosition As Vector3
    Public Property DefaultRotation As Vector3
    Public Property DefaultScale As Vector3
    Public Property Bones As New List(Of FacialBoneEntry)
End Class

''' <summary>Parsed FacialBoneRegions JSON file. Maps FMRI index → region data.</summary>
Public Class FacialBoneRegionsFile
    Public Property Regions As New Dictionary(Of UInteger, FacialBoneRegion)

    ''' <summary>Load and parse a FacialBoneRegions JSON file from raw bytes. Returns Nothing on failure.</summary>
    Public Shared Function ParseFromBytes(data As Byte()) As FacialBoneRegionsFile
        If data Is Nothing OrElse data.Length = 0 Then Return Nothing

        Try
            Dim json = System.Text.Encoding.UTF8.GetString(data)
            Using doc As JsonDocument = JsonDocument.Parse(json)
                If doc.RootElement.ValueKind <> JsonValueKind.Array Then Return Nothing

                Dim result As New FacialBoneRegionsFile
                For Each regionElem In doc.RootElement.EnumerateArray()
                    Dim region = ParseRegion(regionElem)
                    If region IsNot Nothing Then result.Regions(region.ID) = region
                Next
                Return result
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function ParseRegion(elem As JsonElement) As FacialBoneRegion
        If elem.ValueKind <> JsonValueKind.Object Then Return Nothing

        Dim region As New FacialBoneRegion
        Dim prop As JsonElement

        If elem.TryGetProperty("ID", prop) AndAlso prop.ValueKind = JsonValueKind.Number Then
            region.ID = prop.GetUInt32()
        Else
            Return Nothing  ' ID is required
        End If

        If elem.TryGetProperty("Name", prop) AndAlso prop.ValueKind = JsonValueKind.String Then
            region.Name = prop.GetString()
        End If

        If elem.TryGetProperty("AssociatedTintGroup", prop) AndAlso prop.ValueKind = JsonValueKind.String Then
            region.AssociatedTintGroup = prop.GetString()
        End If

        If elem.TryGetProperty("Defaults", prop) AndAlso prop.ValueKind = JsonValueKind.Object Then
            ParseTransform(prop, region.DefaultPosition, region.DefaultRotation, region.DefaultScale)
        End If

        If elem.TryGetProperty("BonesA", prop) AndAlso prop.ValueKind = JsonValueKind.Array Then
            For Each boneElem In prop.EnumerateArray()
                Dim bone = ParseBone(boneElem)
                If bone IsNot Nothing Then region.Bones.Add(bone)
            Next
        End If

        Return region
    End Function

    Private Shared Function ParseBone(elem As JsonElement) As FacialBoneEntry
        If elem.ValueKind <> JsonValueKind.Object Then Return Nothing

        Dim entry As New FacialBoneEntry
        Dim prop As JsonElement

        If elem.TryGetProperty("Bone", prop) AndAlso prop.ValueKind = JsonValueKind.String Then
            entry.Bone = prop.GetString()
        Else
            Return Nothing  ' Bone name required
        End If

        If elem.TryGetProperty("Maxima", prop) AndAlso prop.ValueKind = JsonValueKind.Object Then
            ParseTransform(prop, entry.MaximaPosition, entry.MaximaRotation, entry.MaximaScale)
        End If

        If elem.TryGetProperty("Minima", prop) AndAlso prop.ValueKind = JsonValueKind.Object Then
            ParseTransform(prop, entry.MinimaPosition, entry.MinimaRotation, entry.MinimaScale)
        End If

        Return entry
    End Function

    ''' <summary>Parse a {Position, Rotation, Scale} triple of NiPoint3 vectors from a JSON object.</summary>
    Private Shared Sub ParseTransform(elem As JsonElement,
                                      ByRef pos As Vector3,
                                      ByRef rot As Vector3,
                                      ByRef scale As Vector3)
        Dim sub_ As JsonElement
        If elem.TryGetProperty("Position", sub_) Then pos = ParseVec3(sub_)
        If elem.TryGetProperty("Rotation", sub_) Then rot = ParseVec3(sub_)
        If elem.TryGetProperty("Scale", sub_) Then scale = ParseVec3(sub_)
    End Sub

    Private Shared Function ParseVec3(elem As JsonElement) As Vector3
        If elem.ValueKind <> JsonValueKind.Object Then Return Vector3.Zero

        Dim x As Single = 0, y As Single = 0, z As Single = 0
        Dim prop As JsonElement
        If elem.TryGetProperty("x", prop) AndAlso prop.ValueKind = JsonValueKind.Number Then x = prop.GetSingle()
        If elem.TryGetProperty("y", prop) AndAlso prop.ValueKind = JsonValueKind.Number Then y = prop.GetSingle()
        If elem.TryGetProperty("z", prop) AndAlso prop.ValueKind = JsonValueKind.Number Then z = prop.GetSingle()
        Return New Vector3(x, y, z)
    End Function
End Class

' Uses System.Numerics.Vector3 (aliased to avoid collision with OpenTK.Mathematics.Vector3)
