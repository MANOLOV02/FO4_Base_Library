''' <summary>
''' Minimal interface for preset/pose collection storage in FilesDictionary.
''' Wardrobe_Manager's SliderPresetCollection implements this.
''' Generic consumers (NPC Manager) can ignore it.
''' </summary>
Public Interface IPresetCollection
    ReadOnly Property Presets As IDictionary
    ReadOnly Property Poses As IDictionary
    ReadOnly Property Categories As IDictionary
    Sub LoadCategories(xmlFolder As String)
    Sub LoadDefaultPose()
    Sub LoadPosesBS(posesPath As String)
    Sub LoadPosesSAM(posesPath As String)
End Interface
