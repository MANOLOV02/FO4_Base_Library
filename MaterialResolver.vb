Imports System.IO
Imports MaterialLib
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>
''' Generic per-shape material resolution helpers shared by the human path and the OMOD path.
''' Lives in FO4_Base_Library (uses only lib-resident types: FO4UnifiedMaterial_Class,
''' FilesDictionary_class, Logger, the NiflySharp shader/shape types and Nifcontent_Class_Manolo)
''' so ShapeMaterialOverrides / OmodResolutionApplier can call it directly instead of receiving
''' MainForm AddressOf delegates.
''' </summary>
Public Module MaterialResolver

    Public Function TryLoadMaterialFromDictionary(materialPath As String, fallbackMaterial As FO4UnifiedMaterial_Class, shap As NiflySharp.INiShape, nif As Nifcontent_Class_Manolo) As FO4UnifiedMaterial_Class
        Dim logEnabled = Logger.Enabled
        Dim rawPathLog = If(logEnabled, If(materialPath, ""), Nothing)
        Dim correctedPath = FO4UnifiedMaterial_Class.CorrectMaterialPath(materialPath)
        If correctedPath = "" Then
            If logEnabled Then
                Logger.LogLazy(Function() $"[MAT-LOAD] rawPath='{rawPathLog}' lookupKey='' result=NULL-PATH")
            End If
            Return Nothing
        End If
        Dim containsKey = FilesDictionary_class.Dictionary.ContainsKey(correctedPath)
        If Not containsKey Then
            If logEnabled Then
                Dim lookupKeyLog = correctedPath
                Logger.LogLazy(Function() $"[MAT-LOAD] rawPath='{rawPathLog}' lookupKey='{lookupKeyLog}' containsKey=False result=NOT-FOUND")
            End If
            Return Nothing
        End If

        Dim materialType = GetMaterialTypeFromPath(correctedPath, fallbackMaterial)
        If materialType Is Nothing Then
            If logEnabled Then
                Dim lookupKeyLog = correctedPath
                Logger.LogLazy(Function() $"[MAT-LOAD] rawPath='{rawPathLog}' lookupKey='{lookupKeyLog}' containsKey=True result=UNKNOWN-TYPE")
            End If
            Return Nothing
        End If

        Try
            Dim material As New FO4UnifiedMaterial_Class()
            material.Deserialize(correctedPath, materialType, shap, nif)
            If logEnabled Then
                Dim lookupKeyLog = correctedPath
                Dim loadedAt = material.AlphaTest.ToString()
                Dim typeLog = materialType.Name
                Dim palOnLoad = material.GrayscaleToPaletteColor
                Dim palScaleLoad = material.GrayscaleToPaletteScale
                Dim greyTexLoad = If(material.GreyscaleTexture, "")
                Logger.LogLazy(Function() $"[MAT-LOAD] rawPath='{rawPathLog}' lookupKey='{lookupKeyLog}' containsKey=True type={typeLog} loadedAT={loadedAt} palColor={palOnLoad} palScale={palScaleLoad:F4} greyTex='{greyTexLoad}' result=OK")
            End If
            Return material
        Catch ex As Exception
            If logEnabled Then
                Dim lookupKeyLog = correctedPath
                Dim msg = ex.Message
                Logger.LogLazy(Function() $"[MAT-LOAD] rawPath='{rawPathLog}' lookupKey='{lookupKeyLog}' containsKey=True result=EX msg='{msg}'")
            End If
            Return Nothing
        End Try
    End Function

    Private Function GetMaterialTypeFromPath(materialPath As String, fallbackMaterial As FO4UnifiedMaterial_Class) As Type
        Select Case Path.GetExtension(materialPath).ToLowerInvariant()
            Case ".bgsm"
                Return GetType(BGSM)
            Case ".bgem"
                Return GetType(BGEM)
        End Select

        If fallbackMaterial IsNot Nothing AndAlso fallbackMaterial.Underlying_Material IsNot Nothing Then
            Return fallbackMaterial.Underlying_Material.GetType()
        End If

        Return Nothing
    End Function

    ''' <summary>Ensure the shape's <c>ShapeMaterial</c> is fully resolved (rebuild from the NIF
    ''' shader when the cached material is missing or its .bgsm/.bgem path is unreachable).
    ''' <c>Public</c> in the shared lib so the renderer, the OMOD/MSWP path, and HeadPartPicker_Form
    ''' can all re-use it without needing a MainForm instance.</summary>
    Public Sub EnsureShapeMaterialResolved(shape As IRenderableShape)
        If shape Is Nothing Then Return

        Dim logEnabled = Logger.Enabled
        Dim relatedMaterial = shape.ShapeMaterial
        If relatedMaterial Is Nothing Then Return

        Dim rawPath = If(relatedMaterial.path, "")
        Dim materialPath = FO4UnifiedMaterial_Class.CorrectMaterialPath(relatedMaterial.path)
        Dim containsKey = FilesDictionary_class.Dictionary.ContainsKey(materialPath)
        Dim hasResolvableMaterial = relatedMaterial.path <> "" AndAlso containsKey

        If logEnabled Then
            Dim hadMaterial = relatedMaterial.material IsNot Nothing
            Dim preAT = "?"
            If hadMaterial Then preAT = relatedMaterial.material.AlphaTest.ToString()
            Dim shapeNameLog = shape.ShapeName
            Dim rawPathLog = rawPath
            Dim lookupKeyLog = materialPath
            Dim containsKeyLog = containsKey
            Dim hasResolvableLog = hasResolvableMaterial
            Dim hadMaterialLog = hadMaterial
            Dim preAtLog = preAT
            Logger.LogLazy(Function() $"[MAT-RESOLVE] shape='{shapeNameLog}' rawPath='{rawPathLog}' lookupKey='{lookupKeyLog}' containsKey={containsKeyLog} hasResolvable={hasResolvableLog} hadMat={hadMaterialLog} preAT={preAtLog}")
        End If

        If relatedMaterial.material IsNot Nothing AndAlso (relatedMaterial.path = "" OrElse hasResolvableMaterial) Then Return
        If shape.NifContent Is Nothing OrElse shape.NifShape Is Nothing OrElse shape.NifShader Is Nothing Then
            If logEnabled Then
                Dim shapeNameLog = shape.ShapeName
                Logger.LogLazy(Function() $"[MAT-RESOLVE-SKIP] shape='{shapeNameLog}' reason='missing NifContent/Shape/Shader'")
            End If
            Return
        End If

        Dim rebuiltMaterial As New FO4UnifiedMaterial_Class()

        Select Case shape.NifShader.GetType()
            Case GetType(BSLightingShaderProperty)
                rebuiltMaterial.Create_From_Shader(shape.NifContent, shape.NifShape, CType(shape.NifShader, BSLightingShaderProperty))
            Case GetType(BSEffectShaderProperty)
                rebuiltMaterial.Create_From_Shader(shape.NifContent, shape.NifShape, CType(shape.NifShader, BSEffectShaderProperty))
            Case Else
                Return
        End Select

        relatedMaterial.material = rebuiltMaterial
        relatedMaterial.path = ""
        If logEnabled Then
            Dim shapeNameLog = shape.ShapeName
            Dim rawPathLog = rawPath
            Dim lookupKeyLog = materialPath
            Dim postAtLog = rebuiltMaterial.AlphaTest.ToString()
            Logger.LogLazy(Function() $"[MAT-REBUILD] shape='{shapeNameLog}' rawPath='{rawPathLog}' lookupKey='{lookupKeyLog}' → REBUILT via Create_From_Shader, postAT={postAtLog}")
        End If
    End Sub

End Module
