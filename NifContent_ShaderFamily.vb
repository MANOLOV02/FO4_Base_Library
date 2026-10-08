' Conversion de familia de shader (BSLightingShaderProperty <-> BSEffectShaderProperty), predicado de
' re-apuntado por ref, y la ley de "que secuencia anima a esta shape" (Clone/Split agregan, Convert niega,
' Remove quita). Vive en la LIBRERIA y no en un Form por la misma razon que RemoveShaderAndOrphanClosure:
' es cirugia sobre el grafo de bloques y los gates tienen que ejercitar el CODIGO REAL.
Imports MaterialLib
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Helpers
Imports NiflySharp.Structs

Partial Public Class Nifcontent_Class_Manolo

#Region "Vocabulario del NIF"

    ''' <summary>Vocabulario de flags/shader que habla ESTE archivo. Es el eje de toda ley nueva de la conversion
    ''' (nunca Config_App): la misma escalera que <c>FO4UnifiedMaterial_Class.EnsureShaderGameType</c> y que
    ''' <c>BSLightingShaderProperty.BeforeSync</c> (stream 155 = FO76/SF, >= 130 = FO4, resto = Skyrim).</summary>
    Public Enum VocabularioDeShader
        Fo4
        Skyrim
        NoSoportado
    End Enum

    Public Function Vocabulario() As VocabularioDeShader
        Dim s = Header.Version.StreamVersion
        If s = 155 Then Return VocabularioDeShader.NoSoportado
        If s >= 130 Then Return VocabularioDeShader.Fo4
        Return VocabularioDeShader.Skyrim
    End Function

#End Region

#Region "Re-apuntado de shader / alpha por ref (las dos familias)"

    ''' <summary>La shape admite que se le re-apunte el shader y el NiAlphaProperty POR REF: BSTriShape family o
    ''' NiGeometry family (NiTriShape/NiTriStrips/BSLODTriShape: <c>NiGeometry.ShaderPropertyRef</c> y
    ''' <c>AlphaPropertyRef</c> son escribibles en la clase concreta; el get-only es solo la vista INiShape), y NI
    ''' el shader NI el alpha aparecen en la lista <c>Properties</c>. Si aparecen, <c>GetShader</c> /
    ''' <c>IsBlockReferenced</c> los siguen viendo desde la lista y re-apuntar el ref no alcanza.
    ''' <para>⛔ SEDE UNICA: la usan Convert renderable, Make helper y Convert shader.</para></summary>
    Public Function AdmiteReapuntarPorRef(shape As INiShape) As Boolean
        ' Familias que la app SOPORTA (sede: ShapeGeometryFactory.IsSupported — BSTriShape y subclases, NiTriShape,
        ' NiTriStrips, BSLODTriShape). NiParticleSystem/NiParticles/BSStripParticleSystem tambien son NiGeometry y quedan
        ' afuera aca, nunca en una excepcion mas abajo.
        If Not ShapeGeometryFactory.IsSupported(shape) Then Return False
        If Not (TypeOf shape Is BSTriShape OrElse TypeOf shape Is NiGeometry) Then Return False
        If shape.Properties IsNot Nothing Then
            For Each r In shape.Properties.References
                Dim b = GetBlock(Of NiObject)(r)
                If TypeOf b Is INiShader OrElse TypeOf b Is NiAlphaProperty Then Return False
            Next
        End If
        Return True
    End Function

    Private Shared Function RefDeShaderEscribible(shape As INiShape) As NiBlockRef(Of BSShaderProperty)
        Dim bs = TryCast(shape, BSTriShape)
        If bs IsNot Nothing Then Return bs.ShaderPropertyRef
        Dim geo = TryCast(shape, NiGeometry)
        If geo IsNot Nothing Then Return geo.ShaderPropertyRef
        Throw New NotSupportedException($"'{shape?.Name?.String}': shader re-pointing requires a BSTriShape or NiGeometry family shape.")
    End Function

    Private Shared Sub AsignarRefDeShader(shape As INiShape, indice As Integer)
        Dim nuevo As New NiBlockRef(Of BSShaderProperty) With {.Index = indice}
        Dim bs = TryCast(shape, BSTriShape)
        If bs IsNot Nothing Then bs.ShaderPropertyRef = nuevo : Return
        Dim geo = TryCast(shape, NiGeometry)
        If geo IsNot Nothing Then geo.ShaderPropertyRef = nuevo : Return
        Throw New NotSupportedException($"'{shape?.Name?.String}': shader re-pointing requires a BSTriShape or NiGeometry family shape.")
    End Sub

    ''' <summary>Engancha un shader RECIEN CONSTRUIDO a una shape que no tiene ninguno. Alta comun de
    ''' <see cref="CrearShaderVacio"/> y de la conversion de familia.</summary>
    Private Function AdjuntarShaderNuevo(shape As INiShape, shad As BSShaderProperty) As Integer
        Dim r = RefDeShaderEscribible(shape)
        If r IsNot Nothing AndAlso Not r.IsEmpty() Then
            Throw New InvalidOperationException($"'{shape.Name?.String}' already references a shader block (index {r.Index}).")
        End If
        Dim idx = AddBlock(shad)
        AsignarRefDeShader(shape, idx)
        FO4UnifiedMaterial_Class.EnsureShaderGameType(shad, Me)
        Return idx
    End Function

    ''' <summary>Crea el <c>BSLightingShaderProperty</c> vacio + su <c>BSShaderTextureSet</c> de 8 slots y los
    ''' engancha a la shape (boton "Make shape renderable" de WM). Name VACIO = material embebido.
    ''' <para>El assert se conserva: <c>GetShader</c> devuelve Nothing tambien cuando el ref apunta fuera de rango
    ''' o a un bloque que no es <c>INiShader</c>; sin el, ese caso pasaria de fallar ruidoso a orfanar el bloque
    ''' referenciado en silencio.</para></summary>
    Public Sub CrearShaderVacio(shape As INiShape)
        If Not AdmiteReapuntarPorRef(shape) Then
            Throw New NotSupportedException($"'{shape?.Name?.String}': the shape does not allow re-pointing its shader by reference.")
        End If
        Dim r = RefDeShaderEscribible(shape)
        If r IsNot Nothing AndAlso Not r.IsEmpty() Then
            Throw New InvalidOperationException(
                $"'{shape.Name?.String}' resolves no shader but ShaderPropertyRef={r.Index}: " &
                "the ref points to a missing block or to one that is not an INiShader.")
        End If
        Dim shad As New BSLightingShaderProperty With {.Name = New NiStringRef("")}
        AdjuntarShaderNuevo(shape, shad)
        ' Skinned sigue a la geometria, la misma ley que la conversion (nifly CreateSkinning :4695-4697): el default de
        ' nif.xml no lo trae y una shape con skin sin el bit no se deforma con el esqueleto.
        ShaderHelper.SetFlagSF1(shad, ShaderHelper.SkinnedFlagValue(shad), shape.IsSkinned)
        Dim texset As New BSShaderTextureSet With {.Textures = New List(Of NiString4)}
        shad.TextureSetRef = New NiBlockRef(Of BSShaderTextureSet) With {.Index = AddBlock(texset)}
        FO4UnifiedMaterial_Class.EnsureTextureSetSlots(texset)
    End Sub

#End Region

#Region "Cadenas de controllers y la ley de secuencias"

    ''' <summary>La cadena Controller → NextController → … colgada de <paramref name="raiz"/>, en orden.</summary>
    Public Function CadenaDeControllers(raiz As NiBlockRef(Of NiTimeController)) As List(Of NiTimeController)
        Dim cadena As New List(Of NiTimeController)
        Dim vistos As New HashSet(Of NiTimeController)(System.Collections.Generic.ReferenceEqualityComparer.Instance)
        Dim actual = GetBlock(Of NiTimeController)(raiz)
        While actual IsNot Nothing AndAlso vistos.Add(actual)
            cadena.Add(actual)
            actual = GetBlock(Of NiTimeController)(actual.NextController)
        End While
        Return cadena
    End Function

    ''' <summary>Las TRES cadenas de controllers que pueden animar a una shape: la de la shape misma, la de su
    ''' shader y la de su NiAlphaProperty.</summary>
    Public Function CadenasDeShape(shape As INiShape) As (DeShape As List(Of NiTimeController), DeShader As List(Of NiTimeController), DeAlpha As List(Of NiTimeController))
        Dim deShape = CadenaDeControllers(shape.Controller)
        Dim deShader = CadenaDeShader(shape)
        Dim alp = GetBlock(Of NiAlphaProperty)(shape.AlphaPropertyRef)
        Dim deAlpha = If(alp Is Nothing, New List(Of NiTimeController), CadenaDeControllers(alp.Controller))
        Return (deShape, deShader, deAlpha)
    End Function

    ''' <summary>The shader's controller chain of <paramref name="shape"/> - CadenasDeShape's DeShader, its one owner; empty without a
    ''' shader. Read alone by MaterialData.ShaderChain (C10), once per frame and shape.</summary>
    Public Function CadenaDeShader(shape As INiShape) As List(Of NiTimeController)
        Dim shad = TryCast(GetShader(shape), NiObjectNET)
        Return If(shad Is Nothing, New List(Of NiTimeController), CadenaDeControllers(shad.Controller))
    End Function

    ''' <summary>F-A0 / S-ND: the shape's shader alpha controller. Present: a BSLightingShaderPropertyFloatController on variable 12
    ''' (Alpha) in the shader's controller chain - its update writes material +0x80, the alpha GetRenderPasses reads (Fallout4.exe
    ''' 0x14224A7C0, var 12 -> +0x80 at 0x14224A9FD; SkyrimSE.exe 0x14157AC20, 0x14157AE1D). Max: the highest value its
    ''' interpolators can output (nif.xml): the controller's own Interpolator and the Interpolator of every NiSequence ControlledBlock
    ''' bound to it by its Controller ref (BloquesQueAnimanA, the one law); of an NiFloatInterpolator, its pose Value (nif.xml default
    ''' -3.402823466e+38 = no pose, skipped) and every key of its NiFloatData. Other interpolators (NiBlendFloatInterpolator only blends
    ''' its sequences' outputs) give no value. Nothing when no value is readable. Key maximum, not curve maximum: quadratic tangents
    ''' can overshoot (not evaluated). Blocks bound by name only (no Controller ref) are not read.</summary>
    Public Function LightingAlphaController(shape As INiShape) As (Present As Boolean, Max As Single?)
        Return LightingAlphaController(shape, CadenasDeShape(shape).DeShader)
    End Function

    ''' <summary>LightingAlphaController on the shader chain the caller already walked (<paramref name="shaderChain"/> =
    ''' CadenasDeShape(shape).DeShader; MaterialData.ShaderChain walks it once per frame for this and MaterialAtRest, C10 rev-07).</summary>
    Public Function LightingAlphaController(shape As INiShape, shaderChain As List(Of NiTimeController)) As (Present As Boolean, Max As Single?)
        Dim ctrls = shaderChain.Where(
            Function(c) TypeOf c Is BSLightingShaderPropertyFloatController AndAlso
                        DirectCast(c, BSLightingShaderPropertyFloatController).ControlledVariable = NiflySharp.Enums.LightingShaderControlledFloat.Alpha).ToList()
        If ctrls.Count = 0 Then Return (False, Nothing)
        Dim max As Single? = Nothing
        Dim leer = Sub(interp As NiInterpolator)
                       Dim fi = TryCast(interp, NiFloatInterpolator)
                       If fi Is Nothing Then Return
                       Dim vals As New List(Of Single)
                       If fi.Value <> -Single.MaxValue Then vals.Add(fi.Value)
                       Dim data = GetBlock(fi.Data)
                       If data IsNot Nothing AndAlso data.Data.Keys IsNot Nothing Then vals.AddRange(data.Data.Keys.Select(Function(k) k.Value))
                       For Each v In vals
                           If Not Single.IsNaN(v) AndAlso (Not max.HasValue OrElse v > max.Value) Then max = v
                       Next
                   End Sub
        For Each c In ctrls
            leer(GetBlock(DirectCast(c, NiSingleInterpController).Interpolator))
        Next
        For Each b In BloquesQueAnimanA(shape)
            If b.Controller IsNot Nothing AndAlso ctrls.Contains(b.Controller) Then leer(GetBlock(b.Secuencia.ControlledBlocks(b.Indice).Interpolator))
        Next
        Return (True, max)
    End Function

    ''' <summary>C10: the shape's material at rest - what its active shader controllers write at the game clock's origin
    ''' (ControllerRestLaw), from <paramref name="shaderChain"/> = CadenasDeShape(shape).DeShader (the engine's UpdateControllers of
    ''' the property, 0x1416CF440; MaterialData.ShaderChain walks it once per frame, rev-07), in chain order (a later controller on the
    ''' same variable wins, DEDUCED). Alpha (lighting 12, effect 5) is left to C2 (PreviewAlphaRule). Lighting vars 3..7 and 16..19 (FO4
    ''' feature-gated; census: all manager-controlled, they write nothing) are not mapped. Friend: MaterialRest is the library's.</summary>
    Friend Function MaterialAtRest(shaderChain As List(Of NiTimeController), isFo4 As Boolean) As MaterialRest
        If shaderChain Is Nothing OrElse shaderChain.Count = 0 Then Return MaterialRest.None
        Dim r As New MaterialRest()
        Dim fdata = Function(i As NiInterpolator) If(TypeOf i Is NiFloatInterpolator, GetBlock(DirectCast(i, NiFloatInterpolator).Data), Nothing)
        Dim pdata = Function(i As NiInterpolator) If(TypeOf i Is NiPoint3Interpolator, GetBlock(DirectCast(i, NiPoint3Interpolator).Data), Nothing)
        For Each c In shaderChain
            Dim sc = TryCast(c, NiSingleInterpController)
            If sc Is Nothing Then Continue For
            Dim interp = GetBlock(sc.Interpolator)
            If TypeOf c Is BSEffectShaderPropertyFloatController OrElse TypeOf c Is BSLightingShaderPropertyFloatController Then
                Dim v = ControllerRestLaw.FloatAtRest(sc, interp, fdata(interp))
                If Not v.HasValue Then Continue For
                Dim x = v.Value
                If TypeOf c Is BSEffectShaderPropertyFloatController Then
                    Select Case CInt(DirectCast(c, BSEffectShaderPropertyFloatController).ControlledVariable)
                        Case 0 : r.SetValue(RestVariable.EffectEmissiveMultiple, x)
                        Case 1 : r.SetValue(RestVariable.FalloffStartAngle, ControllerRestLaw.FalloffCos(x))
                        Case 2 : r.SetValue(RestVariable.FalloffStopAngle, ControllerRestLaw.FalloffCos(x))
                        Case 3 : r.SetValue(RestVariable.FalloffStartOpacity, x)
                        Case 4 : r.SetValue(RestVariable.FalloffStopOpacity, x)
                        Case 6 : r.SetValue(RestVariable.UOffset, x)
                        Case 7 : r.SetValue(RestVariable.UScale, x)
                        Case 8 : r.SetValue(RestVariable.VOffset, x)
                        Case 9 : r.SetValue(RestVariable.VScale, x)
                    End Select
                Else
                    Select Case CInt(DirectCast(c, BSLightingShaderPropertyFloatController).ControlledVariable)
                        Case 0 : r.SetValue(RestVariable.Refraction, x)
                        Case 8 : r.SetValue(RestVariable.EnvMapScale, x)
                        Case 9 : r.SetValue(RestVariable.Glossiness, x)
                        Case 10 : r.SetValue(RestVariable.SpecularStrength, x)
                        Case 11 : r.SetValue(RestVariable.LightingEmissiveMultiple, x)
                        Case 20 : r.SetValue(RestVariable.UOffset, x)
                        Case 21 : r.SetValue(RestVariable.UScale, x)
                        Case 22 : r.SetValue(RestVariable.VOffset, x)
                        Case 23 : r.SetValue(RestVariable.VScale, x)
                    End Select
                End If
            ElseIf TypeOf c Is BSEffectShaderPropertyColorController OrElse TypeOf c Is BSLightingShaderPropertyColorController Then
                Dim v = ControllerRestLaw.Point3AtRest(sc, interp, pdata(interp))
                If Not v.HasValue Then Continue For
                Dim col As New OpenTK.Mathematics.Vector3(v.Value.X, v.Value.Y, v.Value.Z)
                If TypeOf c Is BSEffectShaderPropertyColorController Then
                    ' FO4 clamps each component below 0 to 0 (0x14224C165..19D); SSE writes it raw (0x14157C619).
                    If isFo4 Then col = New OpenTK.Mathematics.Vector3(Math.Max(col.X, 0.0F), Math.Max(col.Y, 0.0F), Math.Max(col.Z, 0.0F))
                    r.SetColour(RestVariable.EffectEmissiveColor, col)
                Else
                    Select Case CInt(DirectCast(c, BSLightingShaderPropertyColorController).ControlledColor)
                        Case 0 : r.SetColour(RestVariable.SpecularColor, col)
                        Case 1 : r.SetColour(RestVariable.LightingEmissiveColor, col)
                    End Select
                End If
            End If
        Next
        Return r
    End Function

    ''' <summary>A que cadena pertenece un ControlledBlock que anima a la shape.</summary>
    Public Enum CadenaAnimada
        DeShape
        DeShader
        DeAlpha
    End Enum

    Public Structure BloqueAnimador
        Public Secuencia As NiSequence
        Public Indice As Integer
        Public Cadena As CadenaAnimada
        ''' <summary>El controller que el bloque referencia, o Nothing si el ref esta vacio.</summary>
        Public Controller As NiTimeController
        ''' <summary>El controller referenciado pertenece a una de las tres cadenas de la shape.</summary>
        Public ControllerEnCadena As Boolean
    End Structure

    ''' <summary>⛔ LA LEY UNICA de "que ControlledBlock anima a esta shape". Matchea si (a) el bloque referencia por
    ''' Controller a un controller de la cadena de la shape, del shader o del alpha; o (b) NodeName = nombre de la
    ''' shape y PropertyType ∈ {"" (la shape misma), clase del shader actual, "NiAlphaProperty"} — el motor ata la
    ''' secuencia por nombre+tipo (nif.xml ControlledBlock; censo: 3 casos FO4 GaussRifleMag*_1 sin ref).
    ''' <para>Usos: Clone/Split agregan los equivalentes del clon, Convert niega, Remove quita.</para></summary>
    Public Function BloquesQueAnimanA(shape As INiShape) As List(Of BloqueAnimador)
        Dim res As New List(Of BloqueAnimador)
        If shape Is Nothing OrElse Blocks Is Nothing Then Return res
        Dim cad = CadenasDeShape(shape)
        Dim deQue As New Dictionary(Of NiTimeController, CadenaAnimada)(System.Collections.Generic.ReferenceEqualityComparer.Instance)
        For Each c In cad.DeShape : deQue(c) = CadenaAnimada.DeShape : Next
        For Each c In cad.DeShader : deQue(c) = CadenaAnimada.DeShader : Next
        For Each c In cad.DeAlpha : deQue(c) = CadenaAnimada.DeAlpha : Next
        Dim nombre = shape.Name?.String
        Dim claseShader = GetShader(shape)?.GetType().Name
        ' Controllers de OTRA shape con el mismo nombre: un bloque cuyo Controller es de esa otra shape la anima a
        ' ELLA, aunque el NodeName coincida (nombres duplicados son legales en un NIF).
        Dim deOtraHomonima As New HashSet(Of NiTimeController)(System.Collections.Generic.ReferenceEqualityComparer.Instance)
        If Not String.IsNullOrEmpty(nombre) Then
            For Each otra In NifShapes
                If Object.ReferenceEquals(otra, shape) OrElse Not String.Equals(otra.Name?.String, nombre, StringComparison.Ordinal) Then Continue For
                Dim co = CadenasDeShape(otra)
                deOtraHomonima.UnionWith(co.DeShape) : deOtraHomonima.UnionWith(co.DeShader) : deOtraHomonima.UnionWith(co.DeAlpha)
            Next
        End If

        For Each seq In Blocks.OfType(Of NiSequence)()
            If seq.ControlledBlocks Is Nothing Then Continue For
            For i = 0 To seq.ControlledBlocks.Count - 1
                Dim cb = seq.ControlledBlocks(i)
                Dim ctrl = GetBlock(Of NiTimeController)(cb.Controller)
                Dim cadena As CadenaAnimada
                Dim enCadena = ctrl IsNot Nothing AndAlso deQue.TryGetValue(ctrl, cadena)
                Dim porNombre = False
                If Not enCadena AndAlso Not String.IsNullOrEmpty(nombre) AndAlso
                   Not (ctrl IsNot Nothing AndAlso deOtraHomonima.Contains(ctrl)) AndAlso
                   String.Equals(cb.NodeName?.String, nombre, StringComparison.Ordinal) Then
                    Dim pt = If(cb.PropertyType?.String, "")
                    If pt = "" Then
                        porNombre = True : cadena = CadenaAnimada.DeShape
                    ElseIf claseShader IsNot Nothing AndAlso pt = claseShader Then
                        porNombre = True : cadena = CadenaAnimada.DeShader
                    ElseIf pt = NameOf(NiAlphaProperty) Then
                        porNombre = True : cadena = CadenaAnimada.DeAlpha
                    End If
                End If
                If enCadena OrElse porNombre Then
                    res.Add(New BloqueAnimador With {.Secuencia = seq, .Indice = i, .Cadena = cadena,
                                                     .Controller = ctrl, .ControllerEnCadena = enCadena})
                End If
            Next
        Next
        Return res
    End Function

    ''' <summary>Copia en las secuencias del NIF la animacion de <paramref name="orig"/> para su clon
    ''' <paramref name="clon"/> (mismo archivo): por cada ControlledBlock que la ley de <see cref="BloquesQueAnimanA"/>
    ''' matchea, uno nuevo al final de la lista con NodeName del clon, el controller EMPAREJADO del clon e
    ''' interpolador clonado en profundidad (no compartido); y en todo NiDefaultAVObjectPalette que liste al
    ''' original, una entrada nombre-del-clon → clon.
    ''' <para>EMPAREJAMIENTO por posicion en las tres cadenas (CloneBlocksRec recorre References en orden). Si una
    ''' cadena difiere en largo o en tipo en cualquier posicion, se aborta ANTES de mutar nada.</para>
    ''' <para>Un bloque que matchea por nombre pero cuyo Controller apunta FUERA de las tres cadenas (censo: 3 en
    ''' SalvageBeacons comstation.nif, NiMultiTargetTransformController de 'BSX') no se copia: ese controller no es
    ''' de la shape y duplicarle el bloque haria que la secuencia lo maneje dos veces. Se devuelve la cuenta.</para></summary>
    ''' <returns>(bloques agregados, entradas de palette agregadas, bloques NO copiados por controller ajeno)</returns>
    Public Function CopiarAnimacionAlClon(orig As INiShape, clon As INiShape) As (Bloques As Integer, Palette As Integer, Ajenos As Integer)
        Dim co = CadenasDeShape(orig)
        Dim cc = CadenasDeShape(clon)
        Dim par As New Dictionary(Of NiTimeController, NiTimeController)(System.Collections.Generic.ReferenceEqualityComparer.Instance)
        Emparejar(co.DeShape, cc.DeShape, par, "shape")
        Emparejar(co.DeShader, cc.DeShader, par, "shader")
        Emparejar(co.DeAlpha, cc.DeAlpha, par, "alpha")

        Dim idxOrig As Integer, idxClon As Integer
        If Not GetBlockIndex(orig, idxOrig) OrElse Not GetBlockIndex(clon, idxClon) Then
            Throw New InvalidOperationException("Original or clone shape is not in this NIF.")
        End If
        Dim nombreClon = clon.Name.String

        ' Plan completo antes de mutar.
        Dim plan = BloquesQueAnimanA(orig)
        Dim ajenos = plan.Where(Function(b) b.Controller IsNot Nothing AndAlso Not b.ControllerEnCadena).Count()
        Dim aCopiar = plan.Where(Function(b) b.Controller Is Nothing OrElse b.ControllerEnCadena).ToList()
        For Each b In aCopiar
            If b.Controller IsNot Nothing AndAlso Not par.ContainsKey(b.Controller) Then
                Throw New InvalidOperationException("Controller pairing between original and clone is incomplete.")
            End If
        Next

        Dim agregados = 0
        For Each b In aCopiar
            Dim cb = b.Secuencia.ControlledBlocks(b.Indice).DeepClone()
            cb.NodeName = New NiStringRef(nombreClon) With {.Index = Header.AddOrFindStringId(nombreClon)}
            If b.Controller IsNot Nothing Then
                Dim idxCtrl As Integer
                If Not GetBlockIndex(par(b.Controller), idxCtrl) Then Throw New InvalidOperationException("Paired controller is not in this NIF.")
                cb.Controller = New NiBlockRef(Of NiTimeController) With {.Index = idxCtrl}
            End If
            Dim interp = GetBlock(Of NiObject)(cb.Interpolator)
            If interp IsNot Nothing Then
                Dim copia = DirectCast(interp.Clone(), NiObject)
                Dim idxI = AddBlock(copia)
                CloneChildren(copia)
                cb.Interpolator = New NiBlockRef(Of NiInterpolator) With {.Index = idxI}
            End If
            b.Secuencia.ControlledBlocks.Add(cb)
            b.Secuencia.NumControlledBlocks = CUInt(b.Secuencia.ControlledBlocks.Count)
            agregados += 1
        Next

        Dim enPalette = 0
        For Each pal In Blocks.OfType(Of NiDefaultAVObjectPalette)()
            If pal.Objs Is Nothing Then Continue For
            If Not pal.Objs.Any(Function(o) o.AV_Object IsNot Nothing AndAlso o.AV_Object.Index = idxOrig) Then Continue For
            pal.Objs.Add(New AVObject With {.Name = New NiString4(nombreClon), .AV_Object = New NiBlockPtr(Of NiAVObject)(idxClon)})
            pal.NumObjs = CUInt(pal.Objs.Count)
            enPalette += 1
        Next
        Return (agregados, enPalette, ajenos)
    End Function

    ''' <summary>Clona una shape DENTRO de este NIF y le copia la animacion (<see cref="CopiarAnimacionAlClon"/>).
    ''' ⛔ SEDE UNICA de "clonar shape" para Clone y Split de WM. Si la copia de animacion falla, el clon del bloque se
    ''' revierte con su clausura (consciente de ciclos, sin el barrido de archivo entero) y se re-lanza: el NIF queda
    ''' como estaba. Split la llama ANTES de tocar la geometria por eso mismo.</summary>
    Public Function ClonarShapeConAnimacion(orig As INiShape, nombreNuevo As String) As (Clon As INiShape, Ajenos As Integer)
        Dim clon = CloneShape_Original(orig, nombreNuevo, Me)
        If clon Is Nothing Then Throw New InvalidOperationException("The NIF shape could not be cloned.")
        Try
            Dim r = CopiarAnimacionAlClon(orig, clon)
            Return (clon, r.Ajenos)
        Catch
            RemoveShapeAndOrphanClosure(clon)
            Throw
        End Try
    End Function

    ''' <summary>En la clausura (por refs) de <paramref name="raiz"/>, todo puntero que valga
    ''' <paramref name="idxViejo"/> pasa a <paramref name="idxNuevo"/>. Es el re-bind que CloneBlocksRec hace solo para
    ''' el hijo directo: lo usan CloneShape_Original (same-file) y el des-compartido del NiAlphaProperty.</summary>
    Private Sub ReapuntarPunterosDeClausura(raiz As NiObject, idxViejo As Integer, idxNuevo As Integer)
        Dim vistos As New HashSet(Of NiObject)(System.Collections.Generic.ReferenceEqualityComparer.Instance)
        Dim pendientes As New Stack(Of NiObject)
        For Each r In raiz.References
            Dim b = GetBlock(Of NiObject)(r)
            If b IsNot Nothing Then pendientes.Push(b)
        Next
        While pendientes.Count > 0
            Dim b = pendientes.Pop()
            If Not vistos.Add(b) Then Continue While
            For Each p In b.Pointers
                If p IsNot Nothing AndAlso p.Index = idxViejo Then p.Index = idxNuevo
            Next
            For Each r In b.References
                Dim hijo = GetBlock(Of NiObject)(r)
                If hijo IsNot Nothing Then pendientes.Push(hijo)
            Next
        End While
    End Sub

    Private Shared Sub Emparejar(a As List(Of NiTimeController), b As List(Of NiTimeController),
                                 par As Dictionary(Of NiTimeController, NiTimeController), que As String)
        If a.Count <> b.Count Then
            Throw New InvalidOperationException($"The {que} controller chains of original and clone differ in length ({a.Count} vs {b.Count}).")
        End If
        For i = 0 To a.Count - 1
            If a(i).GetType() IsNot b(i).GetType() Then
                Throw New InvalidOperationException($"The {que} controller chains of original and clone differ in type at position {i}.")
            End If
            par(a(i)) = b(i)
        Next
    End Sub

    ''' <summary>Ley inversa de <see cref="CopiarAnimacionAlClon"/>: saca de toda secuencia los ControlledBlock que
    ''' animan a la shape y de todo NiDefaultAVObjectPalette su entrada. Lo llama <see cref="RemoveShape_Manolo"/>
    ''' ANTES de borrar la shape (con la shape viva todavia se pueden resolver sus cadenas).</summary>
    Private Sub QuitarAnimacionDeShape(shape As INiShape)
        For Each grupo In BloquesQueAnimanA(shape).GroupBy(Function(b) b.Secuencia)
            For Each idx In grupo.Select(Function(b) b.Indice).OrderByDescending(Function(i) i)
                grupo.Key.ControlledBlocks.RemoveAt(idx)
            Next
            grupo.Key.NumControlledBlocks = CUInt(grupo.Key.ControlledBlocks.Count)
        Next
        Dim idxShape As Integer
        If Not GetBlockIndex(shape, idxShape) Then Return
        For Each pal In Blocks.OfType(Of NiDefaultAVObjectPalette)()
            If pal.Objs Is Nothing Then Continue For
            Dim antes = pal.Objs.Count
            pal.Objs.RemoveAll(Function(o) o.AV_Object IsNot Nothing AndAlso o.AV_Object.Index = idxShape)
            If pal.Objs.Count <> antes Then pal.NumObjs = CUInt(pal.Objs.Count)
        Next
    End Sub

    ''' <summary>Renombra la shape DENTRO de este NIF con lo que la nombra por texto: hermana de
    ''' <see cref="CopiarAnimacionAlClon"/> (que escribe el nombre del clon) y de <see cref="QuitarAnimacionDeShape"/>.
    ''' <list type="bullet">
    ''' <item>ControlledBlock: de los que la ley de <see cref="BloquesQueAnimanA"/> matchea (plan con el nombre VIEJO,
    ''' porque la regla (b) compara contra el nombre actual), los que tienen NodeName = nombre viejo pasan al nuevo.
    ''' Los de la regla (a) con NodeName ajeno no nombraban a la shape y no se tocan. Comparacion Ordinal, la de la
    ''' regla (b): censo 3-oct-2026 sin un solo NodeName/palette que coincida con una shape solo sin mayusculas
    ''' (FO4 32.267 CB y 11.798 entradas de palette; SSE 47.333 y 10.610; gate censo-nombres).</item>
    ''' <item>NiDefaultAVObjectPalette: la entrada que apunta (puntero) a la shape toma el nombre nuevo.</item>
    ''' <item>El Name del bloque. La tabla de strings se reconstruye al guardar (NiHeader.UpdateHeaderStrings), como
    ''' nifly NifFile::RenameShape (NifFile.cpp:2261-2268), que solo cambia el nombre.</item>
    ''' </list>
    ''' Outfit Studio no toca secuencias ni palette (OutfitProject.cpp:5713-5783); la app si, porque Clone y Remove ya
    ''' mantienen esas referencias.</summary>
    ''' <returns>(ControlledBlocks renombrados, entradas de palette renombradas)</returns>
    Public Function RenombrarShapeConAnimacion(shape As INiShape, nuevo As String) As (Bloques As Integer, Palette As Integer)
        If shape Is Nothing Then Throw New ArgumentNullException(NameOf(shape))
        If String.IsNullOrEmpty(nuevo) Then Throw New ArgumentException("The new name cannot be empty.", NameOf(nuevo))
        Dim idxShape As Integer
        If Not GetBlockIndex(shape, idxShape) Then Throw New InvalidOperationException("The shape is not in this NIF.")
        Dim viejo = shape.Name?.String

        ' Plan completo con el nombre viejo, antes de mutar. ControlledBlock y AVObject son STRUCTS (generados): se
        ' reescriben por indice en su lista, no sobre una copia.
        Dim bloques = BloquesQueAnimanA(shape).
            Where(Function(b) String.Equals(b.Secuencia.ControlledBlocks(b.Indice).NodeName?.String, viejo, StringComparison.Ordinal)).ToList()
        Dim entradas As New List(Of (Palette As NiDefaultAVObjectPalette, Indice As Integer))
        For Each pal In Blocks.OfType(Of NiDefaultAVObjectPalette)()
            If pal.Objs Is Nothing Then Continue For
            For k = 0 To pal.Objs.Count - 1
                If pal.Objs(k).AV_Object IsNot Nothing AndAlso pal.Objs(k).AV_Object.Index = idxShape Then entradas.Add((pal, k))
            Next
        Next

        For Each b In bloques
            Dim cb = b.Secuencia.ControlledBlocks(b.Indice)
            cb.NodeName = New NiStringRef(nuevo) With {.Index = Header.AddOrFindStringId(nuevo)}
            b.Secuencia.ControlledBlocks(b.Indice) = cb
        Next
        For Each e In entradas
            Dim o = e.Palette.Objs(e.Indice)
            o.Name = New NiString4(nuevo)
            e.Palette.Objs(e.Indice) = o
        Next
        If shape.Name Is Nothing Then
            DirectCast(shape, NiObjectNET).Name = New NiStringRef(nuevo)
        Else
            shape.Name.String = nuevo
        End If
        Return (bloques.Count, entradas.Count)
    End Function

#End Region

#Region "Conversion de familia de shader"

    ''' <summary>Bits de SF1/SF2 OBSERVADOS por clase de shader en el corpus de cada juego (union archivos +
    ''' sueltos). Tools\ShaderFamilyVertexCensus, salidas en Tools\ShaderFamilyVertexCensus\salidas (2-oct-2026):
    ''' FO4 227.270 NIF de 148 BA2 + 721 sueltos (1 falla de carga), SSE 25.537 NIF de 99 BSA + 4.870 sueltos.
    ''' Un bit que NUNCA aparece en la clase destino es un bit de material de la OTRA clase y se apaga al convertir;
    ''' el resto de la palabra vieja se conserva. Lo verifica un gate que relee el censo.</summary>
    Public Shared Function BitsObservados(vocab As VocabularioDeShader, efecto As Boolean) As (SF1 As UInteger, SF2 As UInteger)
        Select Case vocab
            Case VocabularioDeShader.Fo4
                Return If(efecto, (&HEC4181FAUI, &H400400B9UI), (&HAE7F979BUI, &H2E6482F9UI))
            Case VocabularioDeShader.Skyrim
                Return If(efecto, (&HEC40007AUI, &H40020039UI), (&HEE6F9F8BUI, &H3F2081F9UI))
        End Select
        Throw New NotSupportedException("Unsupported shader vocabulary.")
    End Function

    ''' <summary>Bits 10-15 de NiAlphaProperty.Flags (TestFunc 10-12 y los tres altos: los que el escritor de la
    ''' app NO escribe) para un bloque PRE-CREADO: 0x04 &lt;&lt; 10 = TestFunc GREATER, bits 13-15 en cero.
    ''' Medido sobre la poblacion donde el bloque inline es ley (FO4 sin archivo resoluble; MaterialResolver real,
    ''' Tools\ShaderFamilyVertexCensus pasada 4) y SOLO en las celdas donde esos bits son invariantes al 100 %:
    ''' FO4 Effect b0t1 (N=152) y b1t1 (451), FO4 Lighting b1t0 (5.667). En las demas celdas varian: no hay base que
    ''' escribir, asi que el alpha se DESCARTA (ver <see cref="MaterialConvertidoPara"/>).
    ''' <para>Solo FO4: la pre-creacion exige un archivo de material FO4 aplicado (<see cref="FO4UnifiedMaterial_Class.ArchivoFo4Aplicado"/>).
    ''' En SSE el alpha resuelto ES el estado del NIF, asi que una shape sin bloque no pide alpha y nunca se
    ''' pre-crea (las celdas SSE medidas no tienen consumidor).</para></summary>
    Private Const BitsAltosAlphaPreCreado As UShort = &H1000US

    Private Shared Function CeldaAlphaInvariante(efecto As Boolean, blend As Boolean, test As Boolean) As Boolean
        If efecto Then Return test
        Return blend AndAlso Not test
    End Function

    ''' <summary>⛔ SEDE UNICA del material convertido para una shape: <see cref="FO4UnifiedMaterial_Class.ConvertidoA"/>
    ''' hacia la otra familia (vocabulario del bloque) y la regla del alpha que no se puede crear.
    ''' <para>ALPHA DESCARTADO (decision del usuario, 3-oct-2026: "que deje pero avise que se perdera alpha"): si el
    ''' material convertido pide alpha, la shape no tiene NiAlphaProperty (el plan lo PRE-CREARIA) y la celda no tiene
    ''' una base medida (<see cref="CeldaAlphaInvariante"/>), no hay valor del motor para los bits 10-15 del bloque
    ''' nuevo. En vez de negar la conversion o inventar esos bits, el material convertido queda SIN alpha (tupla
    ''' None: blend apagado ONE/ZERO, test apagado) y la shape se dibuja opaca. El llamador avisa
    ''' (<see cref="AvisoAlphaQueSePierde"/>) y la perdida sale medida en <see cref="PerdidasAlConvertirShader"/>.</para>
    ''' <para>Precondicion: el material es de la familia del shader (lo garantiza el predicado antes de llamar).</para></summary>
    Public Function MaterialConvertidoPara(shape As INiShape, mat As FO4UnifiedMaterial_Class) As (Conv As FO4UnifiedMaterial_Class, AlphaDescartado As Boolean)
        Dim efectoDestino = TypeOf GetShader(shape) Is BSLightingShaderProperty
        Dim conv = mat.ConvertidoA(If(efectoDestino, GetType(BGEM), GetType(BGSM)), Vocabulario() = VocabularioDeShader.Skyrim)
        If Not PlanDeAlpha(shape, mat, conv).SePreCrea OrElse CeldaAlphaInvariante(efectoDestino, conv.AlphaBlendEnabled, conv.AlphaTest) Then
            Return (conv, False)
        End If
        conv.RestoreAlphaState(New FO4UnifiedMaterial_Class.AlphaStateSnapshot With {
            .Mode = MaterialLib.BaseMaterialFile.AlphaBlendModeType.None, .Enabled = False,
            .Src = NiflySharp.Enums.AlphaFunction.ONE, .Dst = NiflySharp.Enums.AlphaFunction.ZERO,
            .Test = False, .TestRef = conv.CaptureAlphaState().TestRef})
        Return (conv, True)
    End Function

    ''' <summary>El aviso de <see cref="MaterialConvertidoPara"/> cuando descarta el alpha, o Nothing. Sale de la MISMA
    ''' decision que la conversion, asi el dialogo no puede prometer otra cosa.</summary>
    Public Function AvisoAlphaQueSePierde(shape As INiShape, mat As FO4UnifiedMaterial_Class) As String
        If Not MaterialConvertidoPara(shape, mat).AlphaDescartado Then Return Nothing
        Return "The material asks for transparency, but the shape has no alpha property and the game has no single base value to create one for this case: the converted shape will be drawn OPAQUE (its transparency will be lost)."
    End Function

    ''' <summary>Que canales le faltan a la geometria para un shader Lighting (consulta PURA, para avisar ANTES de
    ''' mutar). Hermana de <see cref="CompletarCanalesParaLighting"/>.</summary>
    Public Shared Function CanalesQueFaltanParaLighting(geom As IShapeGeometry) As (FaltanNormales As Boolean, FaltanTangentes As Boolean, SinUVs As Boolean)
        Return (Not geom.HasNormals, Not geom.HasTangents, Not geom.HasUVs)
    End Function

    ''' <summary>El texto que describe lo que <see cref="CompletarCanalesParaLighting"/> va a hacer, o Nothing si no
    ''' falta nada. Sale de la misma consulta, asi el dialogo no puede prometer otra cosa.</summary>
    Public Shared Function AvisoCanalesParaLighting(geom As IShapeGeometry) As String
        Dim q = CanalesQueFaltanParaLighting(geom)
        If q.FaltanTangentes AndAlso q.SinUVs Then
            If q.FaltanNormales Then Return "The shape has no normals, tangents or UVs: normals will be computed and added; tangents cannot be computed without UVs."
            Return "The shape has no tangents and no UVs: tangents cannot be computed without UVs, so the Lighting shader will have none."
        End If
        If q.FaltanNormales Then Return "The shape has no normals" & If(q.FaltanTangentes, " or tangents", "") & "; a Lighting shader needs them, so they will be computed and added to the mesh."
        If q.FaltanTangentes Then Return "The shape has no tangents; a Lighting shader needs them, so they will be computed and added to the mesh."
        Return Nothing
    End Function

    ''' <summary>⛔ LEY UNICA de "la geometria de un shader Lighting": activa y calcula los canales que falten.
    ''' Normales si no hay; tangentes si no hay y hay UVs (sin UVs no hay de donde: el motor convive con Lighting sin
    ''' tangentes, censo FO4 17 shapes). Sobre la geometria BASE sin morfear (ExtractSkinnedGeometry con la pose por
    ''' defecto), con la ley del autor por la unica puerta (RecalcTBN): si la geometria no traia normales, el lock no
    ''' tiene que conservar (geometriaSinNormales); con normales previas se calculan solo tangentes. Al final
    ''' UpdateSkinPartitions (la particion SSE lleva su copia del VertexDesc).
    ''' <para>La usan la conversion Effect→Lighting y "Make shape renderable".</para></summary>
    ''' <returns>El aviso para el usuario (<see cref="AvisoCanalesParaLighting"/>), o Nothing.</returns>
    Public Function CompletarCanalesParaLighting(autor As IRenderableShape) As String
        Dim geom = autor.Geometry
        Dim aviso = AvisoCanalesParaLighting(geom)
        Dim q = CanalesQueFaltanParaLighting(geom)
        Dim activarT = q.FaltanTangentes AndAlso Not q.SinUVs
        If Not q.FaltanNormales AndAlso Not activarT Then Return aviso
        geom.EnsureNormalChannels(q.FaltanNormales, activarT)
        Dim g = SkinningHelper.ExtractSkinnedGeometry(autor, singleboneskinning:=False, RecalculateNormals:=q.FaltanNormales,
                                                      geometriaSinNormales:=q.FaltanNormales)
        SkinningHelper.InjectNormalsToTrishape(g)
        UpdateSkinPartitions(autor.NifShape)
        Return aviso
    End Function

    ''' <summary>Por que esta shape NO se puede convertir de familia de shader, o Nothing si se puede. Es el
    ''' predicado del boton (deshabilitado + tooltip con este texto) y la guarda de
    ''' <see cref="ConvertirFamiliaDeShader"/>, asi que no hay dos copias de las negaciones.</summary>
    Public Function MotivoParaNoConvertirShader(shape As INiShape, mat As FO4UnifiedMaterial_Class) As String
        If shape Is Nothing Then Return "No shape selected."
        If Not ShapeGeometryFactory.IsSupported(shape) Then Return "This kind of shape is not supported."
        Dim shad = GetShader(shape)
        If Not (TypeOf shad Is BSLightingShaderProperty OrElse TypeOf shad Is BSEffectShaderProperty) Then
            Return "The shape has no Lighting or Effect shader to convert."
        End If
        If Not AdmiteReapuntarPorRef(shape) Then
            Return "The shape's shader or alpha property is referenced from its property list; it cannot be re-pointed safely."
        End If
        Dim vocab = Vocabulario()
        If vocab = VocabularioDeShader.NoSoportado Then Return "This NIF version is not supported."
        Dim juego = Config_App.Current.Game
        If (vocab = VocabularioDeShader.Fo4) <> (juego = Config_App.Game_Enum.Fallout4) Then
            Return "This NIF belongs to a different game than the one configured in the app; saving would treat it with the other game's rules."
        End If
        If mat Is Nothing Then Return "The shape has no material loaded."
        If mat.ArchivoNombradoIlegible() Then
            Return "The material file this shape names could not be read (empty or text/JSON material); its values are not known."
        End If

        ' El material cargado tiene que ser de la familia del shader (BGSM ⇔ Lighting): si no, ConvertidoA tiraria y el
        ' predicado de un boton no puede tirar.
        If (TypeOf mat.Underlying_Material Is BGSM) <> (TypeOf shad Is BSLightingShaderProperty) Then
            Return "The loaded material does not belong to the shape's shader family."
        End If
        Dim conv = MaterialConvertidoPara(shape, mat).Conv
        Dim plan = PlanDeAlpha(shape, mat, conv)
        For Each b In BloquesQueAnimanA(shape)
            If b.Cadena = CadenaAnimada.DeShader Then
                Return "An animation sequence in this NIF drives the shader's controllers; converting would leave it dangling."
            End If
            ' El alpha deja de ser de esta shape solo si se BORRA de verdad o se DES-COMPARTE. Si sobrevive, el escritor
            ' lo reescribe in situ y la secuencia sigue atada a el.
            If b.Cadena = CadenaAnimada.DeAlpha AndAlso (plan.SeBorraDeVerdad OrElse plan.SeDescomparte) Then
                Return "An animation sequence in this NIF drives the alpha property, which the conversion would " & If(plan.SeDescomparte, "unshare", "remove") & "."
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>Convierte el shader de la shape de <paramref name="autor"/> a la OTRA familia (Lighting ⇄ Effect),
    ''' con el material resultante EMBEBIDO (Name = ""). Ley acordada (CHECKLIST de la ola, A1-A8): material por
    ''' <see cref="FO4UnifiedMaterial_Class.ConvertidoA"/>; alpha del motor (borrado con su clausura si ya no hace
    ''' falta, des-compartido si hace falta y esta compartido, pre-creado si hace falta y no hay); flags = palabra
    ''' vieja ∩ bits observados en la clase destino, escritor destino encima, Skinned desde la geometria; clamp;
    ''' canales de Lighting que falten (<see cref="CompletarCanalesParaLighting"/>).
    ''' <para><paramref name="autor"/> aporta la shape, su adaptador de geometria y la ley del autor
    ''' (LockNormals/SmoothSeamNormals) para el recalculo de TBN.</para></summary>
    Public Function ConvertirFamiliaDeShader(autor As IRenderableShape, mat As FO4UnifiedMaterial_Class) As INiShader
        Dim shape = autor.NifShape
        If Not Object.ReferenceEquals(autor.NifContent, Me) Then Throw New ArgumentException("The shape does not belong to this NIF.", NameOf(autor))
        Dim motivo = MotivoParaNoConvertirShader(shape, mat)
        If motivo IsNot Nothing Then Throw New InvalidOperationException(motivo)

        Dim vocab = Vocabulario()
        Dim sk = vocab = VocabularioDeShader.Skyrim
        Dim viejo = DirectCast(GetShader(shape), BSShaderProperty)
        Dim efectoDestino = TypeOf viejo Is BSLightingShaderProperty
        Dim conv = MaterialConvertidoPara(shape, mat).Conv

        ' --- capturas del bloque viejo, antes de borrarlo ---
        FO4UnifiedMaterial_Class.EnsureShaderGameType(viejo, Me)
        Dim palabrasViejas = LeerPalabrasDeFlags(viejo, sk)
        Dim clamp As Integer
        If mat.ArchivoFo4Aplicado() Then
            ' Con archivo aplicado el sampler sale de los tile flags del material (RE_ENGINE_PROPERTY_MAP.md:96,
            ' PROBADO). Mapeo S↔TileU, T↔TileV, True = WRAP: INFERIDO de los nombres (valor exacto no localizado).
            clamp = If(mat.TileU, 2, 0) Or If(mat.TileV, 1, 0)
        ElseIf TypeOf viejo Is BSLightingShaderProperty Then
            clamp = CInt(DirectCast(viejo, BSLightingShaderProperty).TextureClampMode)
        Else
            clamp = DirectCast(viejo, BSEffectShaderProperty).TextureClampMode
        End If

        ' --- alpha (orden acordado): si ya no hace falta, se borra con su clausura (candidato: si otra shape lo
        ' comparte, queda para ella) y el escritor ve "sin bloque y no hace falta"; si hace falta y esta compartido, se
        ' des-comparte; si hace falta y no hay, se pre-crea (el escritor lo completa por la rama no-createdNew). La
        ' pre-creacion solo llega en celdas medidas: en las demas MaterialConvertidoPara ya descarto el alpha. ---
        Dim alp = GetBlock(Of NiAlphaProperty)(shape.AlphaPropertyRef)
        Dim plan = PlanDeAlpha(shape, mat, conv)
        If plan.SeQuita Then
            RemoveAlphaAndOrphanClosure(shape)
        ElseIf plan.SeDescomparte Then
            Dim idxViejo As Integer
            GetBlockIndex(alp, idxViejo)
            Dim copia = DirectCast(alp.Clone(), NiAlphaProperty)
            Dim idxCopia = AddBlock(copia)
            CloneChildren(copia)
            ReapuntarPunterosDeClausura(copia, idxViejo, idxCopia)
            shape.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = idxCopia}
        ElseIf plan.SePreCrea Then
            Dim nuevoAlp As New NiAlphaProperty()
            nuevoAlp.Flags.Value = BitsAltosAlphaPreCreado
            shape.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = AddBlock(nuevoAlp)}
        End If

        ' --- baja del shader viejo (y su clausura huerfana) y alta del nuevo ---
        RemoveShaderAndOrphanClosure(shape)
        Dim nuevo As BSShaderProperty = If(efectoDestino, New BSEffectShaderProperty(), CType(New BSLightingShaderProperty(), BSShaderProperty))
        nuevo.Name = New NiStringRef("")
        AdjuntarShaderNuevo(shape, nuevo)
        Dim obs = BitsObservados(vocab, efectoDestino)
        EscribirPalabrasDeFlags(nuevo, sk, palabrasViejas.SF1 And obs.SF1, palabrasViejas.SF2 And obs.SF2)
        If efectoDestino Then
            DirectCast(nuevo, BSEffectShaderProperty).TextureClampMode = CByte(clamp)
        Else
            DirectCast(nuevo, BSLightingShaderProperty).TextureClampMode = CType(clamp, NiflySharp.Enums.TexClampMode)
        End If

        EscribirMaterialEnShader(shape, nuevo, conv)
        ' Skinned sigue a la geometria (nifly NifFile.cpp CreateSkinning :4695-4697 / RemoveSkinning :4230-4234):
        ' BSTriShape lee VF_SKINNED del VertexDesc, NiGeometry el SkinInstanceRef.
        ShaderHelper.SetFlagSF1(nuevo, ShaderHelper.SkinnedFlagValue(nuevo), shape.IsSkinned)

        ' --- Effect → Lighting: la geometria que un Lighting necesita (ley unica) ---
        If Not efectoDestino AndAlso Not conv.ModelSpaceNormals Then CompletarCanalesParaLighting(autor)
        Return nuevo
    End Function

    ''' <summary>Las dos palabras de flags del bloque en el vocabulario dado. Ramas por clase concreta: las
    ''' propiedades ShaderFlags_* viven en BSLightingShaderProperty / BSEffectShaderProperty, no en la base.</summary>
    Private Shared Function LeerPalabrasDeFlags(s As BSShaderProperty, sk As Boolean) As (SF1 As UInteger, SF2 As UInteger)
        Dim l = TryCast(s, BSLightingShaderProperty)
        If l IsNot Nothing Then
            If sk Then Return (CUInt(l.ShaderFlags_SSPF1), CUInt(l.ShaderFlags_SSPF2))
            Return (CUInt(l.ShaderFlags_F4SPF1), CUInt(l.ShaderFlags_F4SPF2))
        End If
        Dim e = TryCast(s, BSEffectShaderProperty)
        If e IsNot Nothing Then
            If sk Then Return (CUInt(e.ShaderFlags_SSPF1), CUInt(e.ShaderFlags_SSPF2))
            Return (CUInt(e.ShaderFlags_F4SPF1), CUInt(e.ShaderFlags_F4SPF2))
        End If
        Throw New NotSupportedException($"Unsupported shader block {s.GetType().Name}.")
    End Function

    Private Shared Sub EscribirPalabrasDeFlags(s As BSShaderProperty, sk As Boolean, sf1 As UInteger, sf2 As UInteger)
        Dim l = TryCast(s, BSLightingShaderProperty)
        If l IsNot Nothing Then
            If sk Then
                l.ShaderFlags_SSPF1 = CType(sf1, NiflySharp.Enums.SkyrimShaderPropertyFlags1)
                l.ShaderFlags_SSPF2 = CType(sf2, NiflySharp.Enums.SkyrimShaderPropertyFlags2)
            Else
                l.ShaderFlags_F4SPF1 = CType(sf1, NiflySharp.Enums.Fallout4ShaderPropertyFlags1)
                l.ShaderFlags_F4SPF2 = CType(sf2, NiflySharp.Enums.Fallout4ShaderPropertyFlags2)
            End If
            Return
        End If
        Dim e = TryCast(s, BSEffectShaderProperty)
        If e IsNot Nothing Then
            If sk Then
                e.ShaderFlags_SSPF1 = CType(sf1, NiflySharp.Enums.SkyrimShaderPropertyFlags1)
                e.ShaderFlags_SSPF2 = CType(sf2, NiflySharp.Enums.SkyrimShaderPropertyFlags2)
            Else
                e.ShaderFlags_F4SPF1 = CType(sf1, NiflySharp.Enums.Fallout4ShaderPropertyFlags1)
                e.ShaderFlags_F4SPF2 = CType(sf2, NiflySharp.Enums.Fallout4ShaderPropertyFlags2)
            End If
            Return
        End If
        Throw New NotSupportedException($"Unsupported shader block {s.GetType().Name}.")
    End Sub

    ''' <summary>Lo que la conversion PIERDE para esta shape concreta, MEDIDO: se convierte una copia del NIF
    ''' (guardado a memoria y releido) con la funcion real y se compara el material releido del bloque nuevo contra
    ''' el material vivo. Devuelve los nombres de las propiedades visibles en el grid del tipo de origen cuyo valor
    ''' no sobrevive (las solo-de-origen que no estaban en su default, y las compartidas que cambian por el
    ''' escritor/lector de la otra clase o por campos que la version del NIF no serializa), mas "Shader type" y
    ''' "Alpha (as drawn)" si cambian. Nothing en la lista = no se pudo simular (el llamador lo dice).</summary>
    Public Function PerdidasAlConvertirShader(shape As INiShape, mat As FO4UnifiedMaterial_Class) As List(Of String)
        ' ⛔ SIN normalizar: NifFile.Save por defecto borra huerfanos y ordena los bloques EN EL LUGAR (NiflySharp
        ' NifFile.cs:367-371), o sea mutaria el NIF del editor aunque el usuario cancele, y el indice de la shape en la
        ' copia dejaria de ser el del vivo.
        Dim idxShape As Integer
        If Not GetBlockIndex(shape, idxShape) Then Return Nothing
        Dim bytes = Save_To_Bytes_Manolo(normalizar:=False)
        Dim copia As New Nifcontent_Class_Manolo()
        copia.Load_Manolo(bytes)
        Dim shapeCopia = TryCast(copia.Blocks(idxShape), INiShape)
        If shapeCopia Is Nothing Then Return Nothing
        copia.ConvertirFamiliaDeShader(New NifRenderableShape(copia, shapeCopia, 0), mat)
        Dim releido = copia.GetRelatedMaterial(shapeCopia)?.material
        If releido Is Nothing Then Return Nothing

        Dim tipoOrigen = mat.Underlying_Material.GetType()
        Dim aplicaOrigen = If(tipoOrigen Is GetType(BGSM), FieldApplies.BGSM, FieldApplies.BGEM)
        Dim defaultOrigen As New FO4UnifiedMaterial_Class()
        defaultOrigen.Underlying_Material = If(tipoOrigen Is GetType(BGSM), CType(New BGSM(), BaseMaterialFile), New BGEM())
        Dim ignorar As New HashSet(Of String)(FO4UnifiedMaterial_Class.NifShaderOnlyPropertyNames, StringComparer.Ordinal)
        ignorar.UnionWith(FO4UnifiedMaterial_Class.AlphaStatePropertyNames.Values)

        Dim perdidas As New List(Of String)
        Dim contraNuevo = FO4UnifiedMaterial_Class.GetDifferences(mat, releido, ignorar).ToDictionary(Function(d) d.PropertyName)
        Dim contraDefault = FO4UnifiedMaterial_Class.GetDifferences(mat, defaultOrigen, ignorar).ToDictionary(Function(d) d.PropertyName)
        For Each p In GetType(FO4UnifiedMaterial_Class).GetProperties(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)
            If Not p.CanRead OrElse Not p.CanWrite OrElse p.GetIndexParameters().Length <> 0 Then Continue For
            Dim br = Reflection.CustomAttributeExtensions.GetCustomAttribute(Of ComponentModel.BrowsableAttribute)(p)
            If br IsNot Nothing AndAlso Not br.Browsable Then Continue For
            Dim ro = Reflection.CustomAttributeExtensions.GetCustomAttribute(Of ComponentModel.ReadOnlyAttribute)(p)
            If ro IsNot Nothing AndAlso ro.IsReadOnly Then Continue For
            Dim aplica = FO4UnifiedMaterial_Class.AplicaA(p)
            If aplica = FieldApplies.Both Then
                If contraNuevo.ContainsKey(p.Name) Then perdidas.Add(p.Name)
            ElseIf aplica = aplicaOrigen Then
                If contraDefault.ContainsKey(p.Name) Then perdidas.Add(p.Name)
            End If
        Next
        ' TileU/TileV: el lector (Create_From_Shader) no los deriva del clamp del bloque, pero la conversion SI los
        ' escribe ahi (clamp desde los tile flags con archivo aplicado; copia del bloque viejo si no). Si el clamp del
        ' bloque nuevo los codifica, no se pierden: se sacan de la lista.
        Dim nuevoCopia = copia.GetShader(shapeCopia)
        Dim clampNuevo = If(TypeOf nuevoCopia Is BSLightingShaderProperty, CInt(DirectCast(nuevoCopia, BSLightingShaderProperty).TextureClampMode),
                            CInt(DirectCast(nuevoCopia, BSEffectShaderProperty).TextureClampMode))
        If mat.TileU = ((clampNuevo And 2) <> 0) Then perdidas.Remove(NameOf(FO4UnifiedMaterial_Class.TileU))
        If mat.TileV = ((clampNuevo And 1) <> 0) Then perdidas.Remove(NameOf(FO4UnifiedMaterial_Class.TileV))
        If mat.NifShaderType <> releido.NifShaderType Then perdidas.Add("Shader type")
        Dim a0 = mat.ResolveEngineAlpha()
        Dim a1 = releido.ResolveEngineAlpha()
        ' El umbral solo dibuja con el test prendido; src/dst solo con blend.
        If a0.Blend <> a1.Blend OrElse a0.Test <> a1.Test OrElse (a1.Test AndAlso a0.Threshold <> a1.Threshold) OrElse
           (a0.Blend AndAlso (a0.Src <> a1.Src OrElse a0.Dst <> a1.Dst)) Then
            perdidas.Add("Alpha (as drawn)")
        End If
        Return perdidas
    End Function

    ''' <summary>Que le pasa al NiAlphaProperty de la shape al convertir. ⛔ SEDE UNICA del plan: lo usan el predicado
    ''' (negacion por alpha animado), la conversion (que lo ejecuta) y el dialogo (cuantos controllers se van).
    ''' <list type="bullet">
    ''' <item>SeQuita: hay bloque y el material convertido no lo necesita (<see cref="FO4UnifiedMaterial_Class.NecesitaBloqueAlpha"/>):
    ''' se quita de esta shape con su clausura; SeBorraDeVerdad si ademas nadie mas lo referencia.</item>
    ''' <item>SeDescomparte: hay bloque, hace falta y lo comparte otra shape.</item>
    ''' <item>SePreCrea: no hay bloque, hace falta y es FO4 con archivo aplicado (en SSE y FO4 embebido el alpha
    ''' resuelto es el estado del NIF: sin bloque no pide nada).</item>
    ''' </list></summary>
    Public Function PlanDeAlpha(shape As INiShape, mat As FO4UnifiedMaterial_Class, conv As FO4UnifiedMaterial_Class) As (SeQuita As Boolean, SeBorraDeVerdad As Boolean, SeDescomparte As Boolean, SePreCrea As Boolean)
        Dim alp = GetBlock(Of NiAlphaProperty)(shape.AlphaPropertyRef)
        Dim necesita = conv.NecesitaBloqueAlpha()
        If alp Is Nothing Then Return (False, False, False, necesita AndAlso mat.ArchivoFo4Aplicado())
        Dim compartido = AlphaCompartido(alp, shape)
        Return (Not necesita, Not necesita AndAlso Not compartido, necesita AndAlso compartido, False)
    End Function

    ''' <summary>Cuantos controllers se BORRAN al convertir (para el aviso): los de la clausura huerfana del shader
    ''' viejo y, si el plan quita el alpha, los de la suya. Sale de la MISMA decision que el borrado
    ''' (<see cref="ClausuraHuerfana"/>, con la shape como duenia del ref que se suelta), no de las cadenas: si una
    ''' hermana comparte el shader, sus controllers quedan y no se cuentan.</summary>
    Public Function ControllersQueSeBorranAlConvertir(shape As INiShape, mat As FO4UnifiedMaterial_Class) As Integer
        Dim raiz = DirectCast(shape, NiObject)
        Dim inicio As New List(Of NiObject)
        Dim shad = TryCast(GetShader(shape), NiObject)
        If shad IsNot Nothing Then inicio.Add(shad)
        Dim conv = MaterialConvertidoPara(shape, mat).Conv
        If PlanDeAlpha(shape, mat, conv).SeQuita Then
            Dim alp = GetBlock(Of NiAlphaProperty)(shape.AlphaPropertyRef)
            If alp IsNot Nothing Then inicio.Add(alp)
        End If
        ' La raiz (la shape) cuenta como interna: es exactamente soltar sus refs al shader/alpha. Solo se cuentan los
        ' candidatos; la raiz no se borra en la conversion.
        Return ClausuraHuerfana(raiz, inicio).OfType(Of NiTimeController)().Count()
    End Function

    Private Function AlphaCompartido(alp As NiAlphaProperty, duenio As INiShape) As Boolean
        For Each s In NifShapes
            If Object.ReferenceEquals(s, duenio) Then Continue For
            If Object.ReferenceEquals(GetBlock(Of NiAlphaProperty)(s.AlphaPropertyRef), alp) Then Return True
        Next
        Return False
    End Function

    ''' <summary>Escribe el material en el bloque de shader segun su clase. ⛔ SEDE UNICA del despacho: la usan
    ''' <see cref="SetRelatedMaterial"/> (rama Skyrim) y la conversion de familia.</summary>
    Friend Sub EscribirMaterialEnShader(shap As INiShape, shad As INiShader, mat As FO4UnifiedMaterial_Class)
        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                mat.Save_To_Shader(Me, shap, CType(shad, BSLightingShaderProperty), mat.NifShaderType, mat.EnvmapMaskTexture)
            Case GetType(BSEffectShaderProperty)
                mat.Save_To_Shader(Me, shap, CType(shad, BSEffectShaderProperty))
            Case Else
#If DEBUG Then
                Debugger.Break()
#End If
                Throw New NotSupportedException($"Unsupported shader block {shad.GetType().Name}.")
        End Select
    End Sub

#End Region

End Class
