' Version Uploaded of Fo4Library 3.2.0
Imports System.IO
Imports System.Security.Cryptography.X509Certificates
Imports MaterialLib
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Enums
Imports NiflySharp.Structs
Imports OpenTK.Mathematics



Public Class Nifcontent_Class_Manolo
    Inherits NiflySharp.NifFile

    Sub New()
    End Sub
    Public BaseMaterials As New SortedDictionary(Of String, RelatedMaterial_Class)
    Public Class RelatedMaterial_Class
        Public path As String
        Public material As FO4UnifiedMaterial_Class
    End Class
    Public Sub Load_Manolo(Filename As String)
        Try
            Using fs As New FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.Read)
                Load_Manolo(fs)
            End Using
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try
    End Sub

    Public Sub Load_Manolo(FileBytes As Byte())
        Try
            Using ms As New MemoryStream(FileBytes, False)
                Load_Manolo(ms)
            End Using
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try
    End Sub

    Private Sub Load_Manolo(input As Stream)
        Try
            input.Position = 0
            MyBase.Load(input)
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try

        Read_MaterialsDictionary()

        If HasUnknownBlocks Then
#If DEBUG Then
            Debugger.Break()
#End If
            Throw New Exception("Unknown blocks")
        End If
    End Sub
    Private Sub Read_MaterialsDictionary()
        BaseMaterials.Clear()
        For Each shap In Me.GetShapes
            If SupportedShape(shap.GetType) Then
                BaseMaterials(shap.Name.String) = GetRelatedMaterial(shap)
            End If
        Next
    End Sub
    ''' <summary><paramref name="headPartsOnly"/>=True hace que OptimizeFor convierta los shapes a
    ''' BSDynamicTriShape en vez de BSTriShape (ver NifFile.cs:1990) — es lo que el CK hace con los head
    ''' parts de FaceGeom. Solo aplica en la conversión LE→SSE (OptimizeFor no-op si ya es SSE).</summary>
    Public Function Optimize(Game As Config_App.Game_Enum, Optional headPartsOnly As Boolean = False) As NifFileOptimizeResult
        Dim opt As NifFileOptimizeOptions
        Select Case Game
            Case Config_App.Game_Enum.Fallout4
                opt = New NifFileOptimizeOptions With {.TargetVersion = NiVersion.GetFO4}
            Case Config_App.Game_Enum.Skyrim
                opt = New NifFileOptimizeOptions With {.TargetVersion = NiVersion.GetSSE, .HeadPartsOnly = headPartsOnly}
            Case Else
#If DEBUG Then
                Debugger.Break()
#End If
                Throw New Exception
        End Select
        Dim result = Me.OptimizeFor(opt)

        ' Rebuild BaseMaterials only when OptimizeFor actually renamed duplicates.
        ' `result.DuplicatesRenamed = true` means `RenameDuplicateShapes` appended
        ' "_1"/"_2" etc. to shape names; the dict built in Load_Manolo is keyed by
        ' the original names and would KeyNotFoundException on subsequent
        ' `BaseMaterials(shape.Name.String)` lookups (e.g. via RelatedNifMaterial).
        ' When no rename happened, the dict keys are still valid.
        If Not IsNothing(result) AndAlso result.DuplicatesRenamed Then
            Read_MaterialsDictionary()
        End If

        Return result
    End Function

    Public Shared Function SupportedShape(shapetype As Type) As Boolean
        Select Case shapetype
            Case GetType(NiParticles)
                Return False
            Case GetType(BSStripParticleSystem)
                Return False
            Case GetType(NiParticleSystem)
                Return False
            Case GetType(BSSubIndexTriShape)
                Return True
            Case GetType(BSTriShape)
                Return True
            Case GetType(BSLODTriShape)
                Return True
            Case GetType(BSSegmentedTriShape)
                Return True
            Case GetType(BSMeshLODTriShape)
                Return True
            Case GetType(BSDynamicTriShape)
                Return True
            Case GetType(NiTriShape)
                Return True
            Case GetType(NiTriStrips)
                Return True
            Case Else
#If DEBUG Then
                Debugger.Break()
#End If
                Throw New Exception
        End Select
        Return False
    End Function
    Public Sub AddTriData(shapeName As String, triPath As String, toRoot As Boolean)
        Dim target As NiAVObject
        If toRoot Then
            target = GetRootNode()
        Else
            target = FindBlockByName(Of INiShape)(shapeName)
        End If

        If target IsNot Nothing Then
            AssignExtraData(target, triPath)
        End If
    End Sub
    ''' <summary>
    ''' Prende el bit 0 (hidden) de NiAVObject.flags, que es lo que hace BodySlide con
    ''' <c>shape-&gt;flags |= 1</c> (BodySlideApp.cpp:3619) para una shape que quedaria totalmente
    ''' zapeada pero debe conservarse en el archivo.
    '''
    ''' NiAVObject serializa flags como uint32 con StreamVersion &gt; 26 y como uint16 en adelante
    ''' hacia atras (NiMain.NiAVObject.g.cs:139-149). FO4 (130) y SSE (100) caen siempre en el
    ''' uint32; se escriben los dos para que el campo quede coherente sea cual sea el que se
    ''' serialice, ya que el no usado no se emite.
    ''' </summary>
    Public Sub SetShapeHidden(shape As INiShape)
        Dim avo = TryCast(shape, NiAVObject)
        If avo Is Nothing Then Exit Sub
        avo.Flags_ui = avo.Flags_ui Or 1UI
        avo.Flags_us = CUShort(avo.Flags_us Or 1US)
    End Sub

    ''' <summary>Hermano simétrico de <see cref="SetShapeHidden"/>: APAGA el bit 0 (hidden).
    ''' Único par escritor del bit, para que no haya un <c>Or 1</c> suelto en ningún lado.
    ''' <para>⛔ <c>And Not</c>, NUNCA asignar un literal: el valor normal lleva otros bits REALES —
    ''' <c>0x8000E</c> (NoAnimSyncS), <c>SaveExtGeom</c>, <c>MeshLOD_FO4</c>, <c>NoDecals</c>…
    ''' Pisar el campo entero borraría flags que nadie pidió tocar.</para></summary>
    ''' <summary>Quita el shader de la shape y borra su CLAUSURA huérfana. Es la operación "Make helper"
    ''' del editor de WM, pero vive ACÁ porque es cirugía sobre el grafo de bloques del NIF, no UI —
    ''' y porque así un probe puede ejercitar el CÓDIGO REAL en vez de una copia.
    ''' <para>⛔ NO se usa <c>RemoveUnreferencedBlocks</c>: es un barrido de ARCHIVO ENTERO que se lleva
    ''' puestos los huérfanos PREEXISTENTES del NIF del usuario (un <c>NiStringExtraData</c> suelto, un
    ''' alpha property desenganchado, controladores muertos — habituales en meshes editadas a mano).
    ''' Esto es el mismo barrido, acotado al subárbol del shader.</para>
    ''' <para>⛔ TODO POR OBJETO, nunca por índice: <c>RemoveBlock</c> hace <c>Blocks.RemoveAt</c> ANTES
    ''' del fixup y ahí decrementa todo <c>Index &gt; index</c>, así que un índice capturado a través de
    ''' un RemoveBlock apunta a OTRO bloque.</para>
    ''' <para>⛔ Se usa <c>shader.References</c> como fuente en vez de enumerar campos a mano: ya trae
    ''' texture set + controller + extraData + extraDataList, y el controlador además ENCADENA
    ''' (NextController → interpolador → NiFloatData). Una lista escrita a mano envejece mal.</para>
    ''' <para>⚠️ Un CICLO no se borra (cada miembro se ve referenciado). Mismo comportamiento que
    ''' <c>RemoveUnreferencedBlocks</c>, o sea sin regresión.</para>
    ''' <para>Devuelve la cantidad de bloques borrados (shader incluido). 0 = no había shader.</para></summary>
    Public Function RemoveShaderAndOrphanClosure(shape As INiShape) As Integer
        Dim bs = TryCast(shape, NiflySharp.Blocks.BSTriShape)
        If bs Is Nothing Then Throw New NotSupportedException("RemoveShaderAndOrphanClosure requires a BSTriShape family shape.")
        Dim shader = TryCast(GetShader(shape), NiObject)

        ' 1) resolver a OBJETOS todo lo que el shader referencia, ANTES de borrar nada: después no hay
        '    cómo navegar del shape al shader ni del shader a su clausura.
        Dim pendientes As New List(Of NiObject)
        If shader IsNot Nothing Then
            For Each r In shader.References
                If r Is Nothing OrElse r.IsEmpty() Then Continue For
                Dim b = GetBlock(Of NiObject)(r)
                If b IsNot Nothing Then pendientes.Add(b)
            Next
        End If

        ' 2) soltar el ref del shape. .Clear() deja Index = -1, que es EXACTAMENTE el estado que produce
        '    leer del disco un NIF con -1; con Nothing el ref sale de References y el Clone de la shape
        '    copia null, un estado que ningún NIF leído produce.
        bs.ShaderPropertyRef.Clear()

        ' 3) el shader, y después su clausura
        Dim borrados As Integer = 0
        If shader IsNot Nothing AndAlso Not IsBlockReferenced(shader) Then
            If RemoveBlock(shader) Then borrados += 1
        End If
        borrados += BorrarClausuraHuerfana(pendientes)
        Return borrados
    End Function

    ''' <summary>Worklist del borrado de clausura. ⛔ DEDUPE POR REFERENCIA: un bloque puede estar
    ''' encolado dos veces (un <c>ExtraDataList</c> que liste el mismo <c>NiExtraData</c> dos veces es
    ''' legal). En la segunda visita ya no está en el archivo, <c>IsBlockReferenced</c> devuelve False, y
    ''' leer sus <c>References</c> resolvería ÍNDICES PODRIDOS al bloque que hoy ocupa esa ranura — la
    ''' misma corrupción que evita el "todo por objeto". Termina siempre: sólo se encola tras un
    ''' <c>RemoveBlock</c> exitoso, y los borrados están acotados por el <c>Blocks.Count</c> inicial.</summary>
    Private Function BorrarClausuraHuerfana(pendientes As List(Of NiObject)) As Integer
        Dim vistos As New HashSet(Of NiObject)(System.Collections.Generic.ReferenceEqualityComparer.Instance)
        Dim borrados As Integer = 0
        Dim i As Integer = 0
        While i < pendientes.Count
            Dim b = pendientes(i)
            i += 1
            If b Is Nothing OrElse Not vistos.Add(b) Then Continue While
            Dim idx As Integer
            If Not GetBlockIndex(b, idx) Then Continue While      ' ya no está en el archivo
            If IsBlockReferenced(b) Then Continue While
            For Each r In b.References                             ' capturar ANTES de borrar
                If r Is Nothing OrElse r.IsEmpty() Then Continue For
                Dim hijo = GetBlock(Of NiObject)(r)
                If hijo IsNot Nothing Then pendientes.Add(hijo)
            Next
            If RemoveBlock(b) Then borrados += 1
        End While
        Return borrados
    End Function

    Public Sub ClearShapeHidden(shape As INiShape)
        Dim avo = TryCast(shape, NiAVObject)
        If avo Is Nothing Then Exit Sub
        avo.Flags_ui = avo.Flags_ui And Not 1UI
        avo.Flags_us = CUShort(avo.Flags_us And Not 1US)
    End Sub

    Public Sub RemoveTriData(shapeName As String, toRoot As Boolean)
        Dim target As NiAVObject
        If toRoot Then
            target = GetRootNode()
        Else
            target = FindBlockByName(Of INiShape)(shapeName)
        End If

        If Not IsNothing(target) AndAlso Not IsNothing(target.ExtraDataList) Then
            For Each ref As NiRef In target.ExtraDataList.References
                Dim ed As NiStringExtraData
                ed = TryCast(Blocks(ref.Index), NiStringExtraData)
                If Not IsNothing(ed) Then
                    'AssignExtraData()
                    If ed.Name.String = "BODYTRI" Then
                        target.ExtraDataList.RemoveBlockRef(ref.Index)
                        RemoveBlock(ed)
                        RemoveUnreferencedBlocks()
                        Exit Sub
                    End If
                End If
            Next
        End If
    End Sub
    Public Function AssignExtraData(target As NiAVObject, triPath As String) As UInteger
        If Not IsNothing(target.ExtraDataList) Then
            For Each ref As NiRef In target.ExtraDataList.References
                Dim ed As NiStringExtraData = TryCast(Blocks(ref.Index), NiStringExtraData)
                If Not IsNothing(ed) AndAlso ed.Name.String = "BODYTRI" Then
                    ed.StringData.String = triPath
                    Return ref.Index
                End If
            Next
        End If

        Dim triExtraData As New NiStringExtraData With {
            .Name = New NiStringRef("BODYTRI"),
            .StringData = New NiStringRef(triPath)
        }
        Dim extraDataId As UInteger = AddBlock(triExtraData)

        If IsNothing(target.ExtraDataList) Then
            target.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
        End If
        target.ExtraDataList.AddBlockRef(extraDataId)

        Return extraDataId
    End Function

    Public Function GetRelatedMaterial(shap As INiShape) As RelatedMaterial_Class
        Dim prefix = MaterialsPrefix
        Dim shad = GetShader(shap)

        ' Sin shader: material vacío desde shader nulo
        If IsNothing(shad) Then
            Dim mat As New FO4UnifiedMaterial_Class
            mat.Create_From_Shader(Me, shap, New BSLightingShaderProperty)
            Return New RelatedMaterial_Class With {.material = mat, .path = ""}
        End If

        ' Extraer solo lo que difiere entre tipos de shader
        Dim shadName As String
        Dim matType As Type
        Dim createFromShader As Action(Of FO4UnifiedMaterial_Class)

        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                Dim typed = CType(shad, BSLightingShaderProperty)
                shadName = typed.Name.String
                matType = GetType(BGSM)
                createFromShader = Sub(m) m.Create_From_Shader(Me, shap, typed)
            Case GetType(BSEffectShaderProperty)
                Dim typed = CType(shad, BSEffectShaderProperty)
                shadName = typed.Name.String
                matType = GetType(BGEM)
                createFromShader = Sub(m) m.Create_From_Shader(Me, shap, typed)
            Case Else
#If DEBUG Then
                Debugger.Break()
#End If
                Throw New Exception
        End Select

        ' Lógica común (antes duplicada en cada Case)
        Dim fullpath = FO4UnifiedMaterial_Class.CorrectMaterialPath(shadName)
        fullpath = fullpath.StripPrefix(prefix)


        Dim material As New FO4UnifiedMaterial_Class
        If fullpath = "" Then
            createFromShader(material)
        Else
            ' Pass shap+Me so Deserialize can: (a) seed the three alpha fields from the NIF's
            ' NiAlphaProperty before applying the canonical-vs-Unknown rule (BGSM canonical
            ' wins, BGSM Unknown defers to NIF); (b) resolve ShaderType from the NIF shader
            ' when the BGSM-derived value is Default.
            material.Deserialize(prefix & fullpath, matType, shap, Me)
        End If

        Return New RelatedMaterial_Class With {.material = material, .path = fullpath}
    End Function

    Public Sub SetRelatedMaterial(shap As INiShape, MatPath As String, mat As FO4UnifiedMaterial_Class)
        Dim prefix = MaterialsPrefix
        MatPath = FO4UnifiedMaterial_Class.CorrectMaterialPath(MatPath)
        MatPath = MatPath.StripPrefix(prefix)


        Dim shad = GetShader(shap)

        ' ⛔ CONTRATO, y TIRA — no `Exit Sub`. GetRelatedMaterial (el LECTOR, arriba) sí tolera el shader
        ' nulo y sintetiza un material vacío; este es el ESCRITOR y su único caller es el camino de
        ' GUARDAR de WM (Editor_Form.Revisa_Material), que devuelve Boolean. Tragarlo en silencio haría
        ' que el usuario apriete Save, la UI cierre como si hubiera guardado, y el material no se escriba
        ' en ninguna parte. Lector con guard + escritor que traga = 00-reglas-paridad-canonica.
        ' El caller filtra las helper shapes ANTES y con aviso; esto es la red, no la política.
        If shad Is Nothing Then
            Throw New InvalidOperationException(
                $"SetRelatedMaterial on a shape with no BSShaderProperty ('{shap?.Name?.String}'). " &
                "It is a helper shape (collision/marker): the caller must filter it out first.")
        End If

        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                Select Case shad.GetType
                    Case GetType(BSLightingShaderProperty)
                        DirectCast(shad, BSShaderProperty).Name.String = MatPath
                    Case GetType(BSEffectShaderProperty)
                        DirectCast(shad, BSEffectShaderProperty).Name.String = MatPath
                    Case Else
#If DEBUG Then
                        Debugger.Break()
#End If
                        Throw New Exception
                End Select
                ' FO4 path does not rewrite the full inline shader (the BGSM file owns it),
                ' but the NiAlphaProperty is shape-local — the BGSM serializer cannot carry
                ' the AlphaBlend on/off bit independently from the factors (Unknown's bytes
                ' are hardcoded by MaterialLib). The renderer (OS/NifSkope) reads from
                ' NiAlphaProperty, so we must sync the shape-local alpha state here too.
                mat.WriteAlphaPropertyToShape(shap, Me)

                ' ⛔⛔ Y EL BIT Cast_Shadows, POR EL MISMO MOTIVO QUE EL ALPHA: es shape-local y el archivo
                ' de material NO puede llevarlo. Sin esto el flag se LEIA del NIF (Deserialize lo siembra en
                ' `_castShadowsDelNif`) y no se ESCRIBIA NUNCA en Fallout 4 — el usuario lo cambiaba en la
                ' UI, guardaba, y al recargar volvia el valor viejo. Lector sin escritor: ver
                ' 00-reglas-paridad-canonica-como-no-cagarla.
                '
                ' ⛔ SOLO PARA EL EFFECT SHADER (.bgem), a proposito. Un .bgsm SI tiene el campo
                ' CastShadows y es su duenio —el material es reemplazo total del NIF—, asi que escribir
                ' ademas el bit del shader duplicaria la sede del dato y moveria bytes de todo NIF con
                ' BGSM sin que nadie lo haya pedido. El .bgem no tiene ese campo: para el, el bit del NIF
                ' es la UNICA sede posible, que es justamente por lo que se abrio este camino.
                If TypeOf shad Is BSEffectShaderProperty Then
                    FO4UnifiedMaterial_Class.EscribirCastShadowsEnShader(shad, mat.CastShadows, fo4:=True)
                End If
            Case Config_App.Game_Enum.Skyrim
                Dim saveAction As Action
                Select Case shad.GetType
                    Case GetType(BSLightingShaderProperty)
                        Dim typed = CType(shad, BSLightingShaderProperty)
                        saveAction = Sub() mat.Save_To_Shader(Me, shap, typed, mat.NifShaderType, mat.EnvmapMaskTexture)
                    Case GetType(BSEffectShaderProperty)
                        Dim typed = CType(shad, BSEffectShaderProperty)
                        saveAction = Sub() mat.Save_To_Shader(Me, shap, typed)
                    Case Else
#If DEBUG Then
                        Debugger.Break()
#End If
                        Throw New Exception
                End Select
                saveAction()
                DirectCast(shad, BSShaderProperty).Name.String = MatPath   ' común a ambos cases
        End Select
    End Sub

    ''' <summary>
    ''' Fix de build: el engine (FO4/SSE) resuelve el material de una shape leyendo el Name del
    ''' shader como path relativo a Data\, por lo que DEBE empezar con "Materials\" (las NIF vanilla
    ''' siempre lo llevan). WM guarda el path pelado internamente — ver SetRelatedMaterial, que hace
    ''' CorrectMaterialPath y luego StripPrefix(MaterialsPrefix) — así que una NIF grabada directo
    ''' desde ese estado se ve bien in-app (GetRelatedMaterial le vuelve a poner el prefijo al leer)
    ''' pero deja al engine buscando en Data\&lt;path pelado&gt; y el material nunca carga. Llamar
    ''' esto justo antes de grabar una NIF destinada al juego garantiza que cada Name lleve el prefijo.
    ''' Idempotente: no-op si el Name ya contiene el ancla "materials\" — tal cual la resuelve el
    ''' engine, sea prefijo limpio ("Materials\...") o path absoluto de build ("C:\...\Data\materials\...");
    ''' en ambos casos preservamos los bytes originales. Salta shapes cuyo shader no tiene material
    ''' (Name vacío) para no inventar uno. Sólo antepone cuando NO hay ancla (p.ej. "ManoloCloned\...").
    ''' </summary>
    Public Sub EnsureMaterialPrefixForGame()
        Dim anchor = MaterialsPrefix.ToLowerInvariant()   ' "materials\"
        For Each shap In NifShapes
            Dim shad = TryCast(GetShader(shap), BSShaderProperty)
            If shad Is Nothing OrElse shad.Name Is Nothing Then Continue For
            Dim name = shad.Name.String
            If String.IsNullOrEmpty(name) Then Continue For
            ' Ya trae el ancla (prefijo limpio o path absoluto): el engine la resuelve → no-op.
            If name.Replace("/"c, "\"c).ToLowerInvariant().Contains(anchor) Then Continue For
            ' Sin ancla: el engine buscaría en Data\<path pelado> y no encontraría el .bgsm/.bgem.
            shad.Name.String = MaterialsPrefix & name.TrimStart("\"c)
        Next
    End Sub

    Public Sub Save_As_Manolo(Filename As String, Overwrite As Boolean)
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then
            If MsgBox("NIF File already exists, replace?", vbYesNo, "Warning") = MsgBoxResult.No Then
                Exit Sub
            End If
        End If
        If MyBase.Save(Filename) <> 0 Then
            Throw New Exception("Error saving NIF")
        End If
    End Sub

    ''' <summary>Serialize the NIF to a byte array (in-memory save) — used by headless bakes/compares that
    ''' don't want a disk file. Mirrors Save_As_Manolo but to a MemoryStream via NifFile.Save(Stream).</summary>
    Public Function Save_To_Bytes_Manolo() As Byte()
        Using ms As New IO.MemoryStream()
            If MyBase.Save(ms) <> 0 Then Throw New Exception("Error saving NIF to bytes")
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>
    ''' Shapes del NIF, o vacio si todavia no se cargo ninguno. NiflySharp.GetShapes hace
    ''' `Blocks.OfType(...)` sin guard (NifFile.cs:1055) y tira ArgumentNullException con un
    ''' Nifcontent recien construido — justo el estado de un sliderset antes de Load_and_Check_Shapedata.
    ''' Los callers lo usan como "dame las shapes que haya" (p. ej. el guard
    ''' `Not IsNothing(NIFContent.NifShapes)` de EnsureShapeDataLookupCacheCore, que al evaluar la
    ''' propiedad se llevaba por delante el chequeo que pretendia hacer).
    ''' </summary>
    Public ReadOnly Property NifShapes As IEnumerable(Of NiflySharp.INiShape)
        Get
            If Me.Blocks Is Nothing Then Return Array.Empty(Of NiflySharp.INiShape)()
            Return Me.GetShapes
        End Get
    End Property

    Public Sub RemoveShape_Manolo(Shape As INiShape)
        Me.RemoveBlock(Shape)
        Me.RemoveUnreferencedBlocks()
    End Sub
    Public Shared Sub Merge_Shapes_Original(DestNif As Nifcontent_Class_Manolo, SrcNif As Nifcontent_Class_Manolo, MergeClothesData As Boolean)
        SrcNif.GetRootNode().Name.String = "Scene Root"
        ' BSClothExtraData is used by both FO4 and SSE (vanilla Havok); sidecar XML (HDT-SMP) is handled separately by SliderSet_Class
        If Not MergeClothesData Then SrcNif.RemoveBlocksOfType(Of BSClothExtraData)()
        SrcNif.RemoveUnreferencedBlocks()
        For Each shap In SrcNif.GetShapes.ToList
            DestNif.CloneShape_Original(shap, shap.Name.String, SrcNif)
        Next
        If MergeClothesData Then CloneRootClothExtraData(DestNif, SrcNif)

    End Sub

    Private Shared Sub CloneRootClothExtraData(DestNif As Nifcontent_Class_Manolo, SrcNif As Nifcontent_Class_Manolo)
        Dim destRoot = DestNif.GetRootNode()
        Dim srcRoot = SrcNif.GetRootNode()
        If IsNothing(destRoot) OrElse IsNothing(srcRoot) Then Exit Sub

        Dim sourceCloth = GetRootExtraData(srcRoot, SrcNif).OfType(Of BSClothExtraData).ToList()
        If sourceCloth.Count = 0 Then
            sourceCloth = SrcNif.Blocks.OfType(Of BSClothExtraData).ToList()
        End If
        If sourceCloth.Count = 0 Then Exit Sub

        Dim destCloth = GetRootExtraData(destRoot, DestNif).OfType(Of BSClothExtraData).ToList()
        If destCloth.Count > 0 Then
            MsgBox("The destination mesh already has physics. Physics from the merged mesh will be omitted.", vbInformation, "Merge Physics")
            Exit Sub
        End If

        If IsNothing(destRoot.ExtraDataList) Then destRoot.ExtraDataList = New NiBlockRefArray(Of NiExtraData)

        For Each srcCloth In sourceCloth
            Dim cloned = TryCast(srcCloth.Clone(), BSClothExtraData)
            If IsNothing(cloned) Then Continue For
            If Not IsNothing(cloned.NextExtraData) Then cloned.NextExtraData.Clear()

            Dim blockId = DestNif.AddBlock(cloned)
            destRoot.ExtraDataList.AddBlockRef(blockId)

            If IsNothing(destRoot.ExtraData) Then
                destRoot.ExtraData = New NiBlockRef(Of NiExtraData) With {
                    .Index = blockId
                }
            End If
        Next
    End Sub

    ''' <summary>Copia el/los BSClothExtraData del root de <paramref name="srcNif"/> al root de Me.
    ''' Versión NO-interactiva (sin MsgBox) e IDEMPOTENTE de <see cref="CloneRootClothExtraData"/>:
    ''' si Me ya tiene un BSClothExtraData en el root, no hace nada. La usa el FaceGen bake para
    ''' preservar el cloth-physics del pelo (CloneShape_Original no transfiere el cloth extradata
    ''' porque es extradata del ROOT, no de la shape). El bloque es un blob HKX self-contained
    ''' (refs por índice a su propio hkaSkeleton), así que clonarlo tal cual es válido en el destino.</summary>
    Public Sub TransferRootClothExtraDataFrom(srcNif As Nifcontent_Class_Manolo)
        If srcNif Is Nothing Then Exit Sub
        Dim destRoot = Me.GetRootNode()
        If IsNothing(destRoot) Then Exit Sub

        Dim sourceCloth = srcNif.Blocks.OfType(Of BSClothExtraData).ToList()
        If sourceCloth.Count = 0 Then Exit Sub

        ' Idempotente: si el root del destino ya tiene cloth, no duplicar.
        Dim destCloth = GetRootExtraData(destRoot, Me).OfType(Of BSClothExtraData).ToList()
        If destCloth.Count > 0 Then Exit Sub

        If IsNothing(destRoot.ExtraDataList) Then destRoot.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
        For Each srcCloth In sourceCloth
            Dim cloned = TryCast(srcCloth.Clone(), BSClothExtraData)
            If IsNothing(cloned) Then Continue For
            If Not IsNothing(cloned.NextExtraData) Then cloned.NextExtraData.Clear()
            Dim blockId = Me.AddBlock(cloned)
            destRoot.ExtraDataList.AddBlockRef(blockId)
            If IsNothing(destRoot.ExtraData) Then
                destRoot.ExtraData = New NiBlockRef(Of NiExtraData) With {.Index = blockId}
            End If
        Next
    End Sub

    Public Const SmpPhysicsExtraDataName As String = "HDT Skinned Mesh Physics Object"

    ''' <summary>True si el NiStringExtraData es el vínculo de física HDT-SMP de Skyrim (por nombre exacto).</summary>
    Private Shared Function IsSmpPhysicsExtraData(ed As NiStringExtraData) As Boolean
        Return ed IsNot Nothing AndAlso ed.Name IsNot Nothing AndAlso
               String.Equals(ed.Name.String, SmpPhysicsExtraDataName, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Copia el NiStringExtraData "HDT Skinned Mesh Physics Object" (el vínculo HDT-SMP de
    ''' Skyrim: su StringData es la ruta Data-relative al XML de física) del ROOT de <paramref name="srcNif"/>
    ''' al root de Me. Paralelo SSE de <see cref="TransferRootClothExtraDataFrom"/> (que preserva el cloth de
    ''' FO4): el bake reconstruye el root desde cero y CloneShape_Original solo preserva el extradata de la
    ''' SHAPE, así que sin esto el vínculo SMP —que cuelga del ROOT— se pierde y el motor nunca carga el XML,
    ''' dejando el pelo sin física. IDEMPOTENTE: si el root del destino ya tiene el vínculo, no duplica (varias
    ''' partes fuente —pelo + hairline— suelen apuntar al mismo XML). Filtrado POR NOMBRE: no toca BODYTRI ni
    ''' otros NiStringExtraData. El XML NO se copia: la ruta es fija y ya está instalada con el mod. Reconstruye
    ''' el bloque (Name/StringData) en vez de Clone() para ser cross-file safe con la string table.</summary>
    Public Sub TransferRootSmpExtraDataFrom(srcNif As Nifcontent_Class_Manolo)
        If srcNif Is Nothing Then Exit Sub
        Dim destRoot = Me.GetRootNode()
        Dim srcRoot = srcNif.GetRootNode()
        If IsNothing(destRoot) OrElse IsNothing(srcRoot) Then Exit Sub

        ' Solo el NiStringExtraData de física SMP colgado del ROOT del source (no BODYTRI ni otros strings).
        Dim srcSmp = GetRootExtraData(srcRoot, srcNif).OfType(Of NiStringExtraData).FirstOrDefault(AddressOf IsSmpPhysicsExtraData)
        If IsNothing(srcSmp) Then Exit Sub

        ' Idempotente: si el root del destino ya tiene el vínculo SMP, no duplicar.
        If GetRootExtraData(destRoot, Me).OfType(Of NiStringExtraData).Any(AddressOf IsSmpPhysicsExtraData) Then Exit Sub

        Dim cloned As New NiStringExtraData With {
            .Name = New NiStringRef(SmpPhysicsExtraDataName),
            .StringData = New NiStringRef(If(srcSmp.StringData?.String, ""))
        }
        If IsNothing(destRoot.ExtraDataList) Then destRoot.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
        Dim blockId = Me.AddBlock(cloned)
        destRoot.ExtraDataList.AddBlockRef(blockId)
        If IsNothing(destRoot.ExtraData) Then
            destRoot.ExtraData = New NiBlockRef(Of NiExtraData) With {.Index = blockId}
        End If
    End Sub

    ''' <summary>Devuelve el path (tal cual lo guarda el mod, Data-relative, p.ej.
    ''' "Data\meshes\KS Hairdo's\HDT\XML\Amor.xml") del XML de física HDT-SMP declarado por el
    ''' NiStringExtraData "HDT Skinned Mesh Physics Object" del ROOT, o Nothing si el NIF no trae ese
    ''' vínculo. Es la FUENTE AUTORITATIVA del link SMP: el sidecar same-basename es solo una convención
    ''' que no todos los mods siguen (KS Hairdos apunta a una subcarpeta). El caller resuelve el path.</summary>
    Public Function TryGetSmpPhysicsXmlPath() As String
        Dim root = Me.GetRootNode()
        If IsNothing(root) Then Return Nothing
        Dim smp = GetRootExtraData(root, Me).OfType(Of NiStringExtraData).FirstOrDefault(AddressOf IsSmpPhysicsExtraData)
        If IsNothing(smp) OrElse IsNothing(smp.StringData) Then Return Nothing
        Dim p = smp.StringData.String
        Return If(String.IsNullOrWhiteSpace(p), Nothing, p.Trim())
    End Function

    ''' <summary>Crea o actualiza el NiStringExtraData "HDT Skinned Mesh Physics Object" del ROOT para que
    ''' apunte a <paramref name="dataRelativeXmlPath"/> (el path que el motor leerá; convención KS =
    ''' "Data\meshes\...\x.xml"). Si ya existe, solo reescribe su StringData; si no, lo crea en el root.
    ''' Contraparte de <see cref="TryGetSmpPhysicsXmlPath"/>: WM lo usa al grabar/buildear para mantener el
    ''' link in-NIF sincronizado con dónde escribe el sidecar ("paths ajustados"). Sin esto, el motor lee el
    ''' path viejo del extra-data y el sidecar reubicado se ignora.</summary>
    Public Sub SetSmpPhysicsXmlPath(dataRelativeXmlPath As String)
        If String.IsNullOrWhiteSpace(dataRelativeXmlPath) Then Exit Sub
        Dim root = Me.GetRootNode()
        If IsNothing(root) Then Exit Sub

        Dim existing = GetRootExtraData(root, Me).OfType(Of NiStringExtraData).FirstOrDefault(AddressOf IsSmpPhysicsExtraData)
        If existing IsNot Nothing Then
            existing.StringData = New NiStringRef(dataRelativeXmlPath)
            Return
        End If

        Dim ed As New NiStringExtraData With {
            .Name = New NiStringRef(SmpPhysicsExtraDataName),
            .StringData = New NiStringRef(dataRelativeXmlPath)
        }
        If IsNothing(root.ExtraDataList) Then root.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
        Dim blockId = Me.AddBlock(ed)
        root.ExtraDataList.AddBlockRef(blockId)
        If IsNothing(root.ExtraData) Then root.ExtraData = New NiBlockRef(Of NiExtraData) With {.Index = blockId}
    End Sub

    ''' <summary>Quita el/los NiStringExtraData "HDT Skinned Mesh Physics Object" del ROOT (si existen).
    ''' WM lo usa cuando el proyecto queda sin física (PhysicsXmlContent vacío) para no dejar un link
    ''' colgando a un XML inexistente.</summary>
    Public Sub RemoveSmpPhysicsExtraData()
        Dim root = Me.GetRootNode()
        If IsNothing(root) OrElse IsNothing(root.ExtraDataList) Then Exit Sub
        For Each ed In GetRootExtraData(root, Me).OfType(Of NiStringExtraData).Where(AddressOf IsSmpPhysicsExtraData).ToList()
            Dim idx As Integer
            If GetBlockIndex(ed, idx) Then
                root.ExtraDataList.RemoveBlockRef(idx)
                RemoveBlock(ed)
            End If
        Next
        RemoveUnreferencedBlocks()
    End Sub

    ''' <summary>Copia el/los BSClothExtraData de <paramref name="srcNif"/> al ExtraDataList de la
    ''' SHAPE <paramref name="destShape"/> (en Me). Audit byte-fidelity vs CK: CK cuelga el cloth del
    ''' pelo de la SHAPE, no del root (256/256 NIFs FaceGen de CK; 0 en el root). CloneShape_Original
    ''' NO transfiere el cloth extradata, así que lo clonamos del NIF source y lo colgamos de la
    ''' dest-shape, replicando a CK. Idempotente: si la dest-shape ya tiene un BSClothExtraData no
    ''' duplica. Source-driven. NO setea el ref único ExtraData (igual que TransferShapeEyeCenterExtraData).</summary>
    Public Sub TransferShapeClothExtraDataFrom(srcNif As Nifcontent_Class_Manolo, destShape As INiShape)
        If srcNif Is Nothing OrElse destShape Is Nothing Then Exit Sub
        Dim destAv = TryCast(destShape, NiAVObject)
        If destAv Is Nothing Then Exit Sub

        Dim sourceCloth = srcNif.Blocks.OfType(Of BSClothExtraData).ToList()
        If sourceCloth.Count = 0 Then Exit Sub

        ' Idempotente: si la dest-shape ya tiene un BSClothExtraData, no duplicar.
        If destAv.ExtraDataList IsNot Nothing Then
            For Each di In destAv.ExtraDataList.Indices
                If di >= 0 AndAlso di < Blocks.Count AndAlso TypeOf Blocks(di) Is BSClothExtraData Then Exit Sub
            Next
        End If

        For Each srcCloth In sourceCloth
            Dim cloned = TryCast(srcCloth.Clone(), BSClothExtraData)
            If IsNothing(cloned) Then Continue For
            If Not IsNothing(cloned.NextExtraData) Then cloned.NextExtraData.Clear()
            Dim blockId = Me.AddBlock(cloned)
            If destAv.ExtraDataList Is Nothing Then destAv.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
            destAv.ExtraDataList.AddBlockRef(blockId)
        Next
    End Sub

    ''' <summary>Preserva el/los BSEyeCenterExtraData('ECED') de la shape <paramref name="srcShape"/>
    ''' (en <paramref name="srcNif"/>) copiándolos al ExtraDataList de <paramref name="destShape"/> (en Me).
    ''' CloneShape no transfiere el extradata de la shape; CK SÍ lo preserva (el iris MaleEyes.nif trae
    ''' un ECED con Data constante; FemaleEyes.nif NO → female no recibe = CK). Idempotente: si la
    ''' dest-shape ya tiene un ECED no duplica. Source-driven → gender/parte correctos solos.
    ''' Ver 10-stack-arnes-de-medicion (#c ECED).</summary>
    Public Sub TransferShapeEyeCenterExtraData(srcNif As Nifcontent_Class_Manolo, srcShape As INiShape, destShape As INiShape)
        If srcNif Is Nothing OrElse srcShape Is Nothing OrElse destShape Is Nothing Then Exit Sub
        Dim srcAv = TryCast(srcShape, NiAVObject)
        Dim destAv = TryCast(destShape, NiAVObject)
        If srcAv Is Nothing OrElse destAv Is Nothing OrElse srcAv.ExtraDataList Is Nothing Then Exit Sub

        ' Idempotente: si la dest-shape ya tiene un ECED, no duplicar.
        If destAv.ExtraDataList IsNot Nothing Then
            For Each di In destAv.ExtraDataList.Indices
                If di >= 0 AndAlso di < Blocks.Count AndAlso TypeOf Blocks(di) Is BSEyeCenterExtraData Then Exit Sub
            Next
        End If

        For Each si In srcAv.ExtraDataList.Indices
            If si < 0 OrElse si >= srcNif.Blocks.Count Then Continue For
            Dim ece = TryCast(srcNif.Blocks(si), BSEyeCenterExtraData)
            If ece Is Nothing Then Continue For
            Dim cloned = TryCast(ece.Clone(), BSEyeCenterExtraData)
            If cloned Is Nothing Then Continue For
            Dim blockId = Me.AddBlock(cloned)
            If destAv.ExtraDataList Is Nothing Then destAv.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
            destAv.ExtraDataList.AddBlockRef(blockId)
        Next
    End Sub

    Private Shared Iterator Function GetRootExtraData(root As NiNode, nif As Nifcontent_Class_Manolo) As IEnumerable(Of NiExtraData)
        If IsNothing(root) OrElse IsNothing(nif) Then Return

        Dim visited As New HashSet(Of Integer)
        If Not IsNothing(root.ExtraData) Then
            Dim current = nif.GetBlock(Of NiExtraData)(root.ExtraData)
            Do While Not IsNothing(current)
                Dim idx = nif.Blocks.IndexOf(current)
                If idx <> -1 AndAlso visited.Add(idx) = False Then Exit Do
                Yield current
                If IsNothing(current.NextExtraData) Then Exit Do
                current = nif.GetBlock(Of NiExtraData)(current.NextExtraData)
            Loop
        End If

        If IsNothing(root.ExtraDataList) Then Return

        For Each reference In root.ExtraDataList.References
            Dim extra = nif.GetBlock(Of NiExtraData)(reference)
            If IsNothing(extra) Then Continue For

            Dim idx = nif.Blocks.IndexOf(extra)
            If idx = -1 OrElse visited.Add(idx) Then Yield extra
        Next
    End Function

    Public Function CloneShape_Original(srcShape As INiShape, destShapeName As String, srcNif As Nifcontent_Class_Manolo) As INiShape
        ' BSDynamicTriShape clone path validated 2026-06-15 via TestNifFile_Skinned_Dynamic_SE
        ' roundtrip (dynamic _vertices stay in sync with vertData via CalcDynamicData; skin
        ' consistent after the SSE NiSkinData rebuild fix).  Earlier Debugger.Break guard removed.
        Dim destShape = Me.CloneShape(srcShape, destShapeName, srcNif)

        ' Preservar el ExtraDataList de la shape (REGLA GENERAL, no solo ECED). NiflySharp.CloneShape
        ' hace srcShape.Clone() que copia las REFS del ExtraDataList (índices) pero NO re-clona los
        ' BLOQUES cross-file → las refs apuntan a bloques del NIF source, quedan colgando en el destino
        ' y se pierden (RemoveUnreferencedBlocks las evicta). Verificado: el BSEyeCenterExtraData('ECED')
        ' de los ojos desaparecía. Afecta a TODO consumidor de CloneShape_Original (FaceGen bake, WM
        ' Merge_Shapes/SplitShape). Re-clonamos cada NiExtraData del source a Me y re-referenciamos,
        ' preservando el orden. Solo cross-file (same-file las refs ya son válidas). Las flags
        ' (NiAVObject.Flags) ya las preserva Clone(); el BSClothExtraData es root-level (aparte).
        If destShape IsNot Nothing AndAlso Not Object.ReferenceEquals(srcNif, Me) Then
            Dim srcAvEd = TryCast(srcShape, NiAVObject)
            Dim destAvEd = TryCast(destShape, NiAVObject)
            If srcAvEd IsNot Nothing AndAlso destAvEd IsNot Nothing AndAlso srcAvEd.ExtraDataList IsNot Nothing Then
                Dim rebuilt As New NiBlockRefArray(Of NiExtraData)
                For Each si In srcAvEd.ExtraDataList.Indices
                    If si < 0 OrElse si >= srcNif.Blocks.Count Then Continue For
                    Dim ed = TryCast(srcNif.Blocks(si), NiExtraData)
                    If ed Is Nothing Then Continue For
                    Dim clonedEd = TryCast(ed.Clone(), NiExtraData)
                    If clonedEd Is Nothing Then Continue For
                    If Not IsNothing(clonedEd.NextExtraData) Then clonedEd.NextExtraData.Clear()
                    rebuilt.AddBlockRef(Me.AddBlock(clonedEd))
                Next
                destAvEd.ExtraDataList = rebuilt
            End If
        End If

        ' Cross-file clone: NiflySharp.NifFile.CloneShape parents the cloned shape to destRoot
        ' (NifFile.cs:758-762), losing intermediate NiNodes between srcShape and srcRoot. Para
        ' shapes UNSKINNED la posición global se compone como shape.T/R/S × parent_chain — si
        ' había NiNodes intermedios con transform no-identidad, después del clone esa contribución
        ' desaparece y el shape aparece desplazado.
        '
        ' Fix (Opción C): bakear SOLO los NiNodes intermedios entre srcShape y srcRoot. NO se
        ' bakea el srcRoot porque su transform suele representar contexto del NIF (heel offset,
        ' floor offset, body-height) que es accidental al fichero source y no se debe arrastrar
        ' al destino. Si srcShape cuelga directo de srcRoot (NIF flat), no se hace nada — el
        ' Clone ya preservó srcShape.T/R/S y eso basta.
        '
        ' Math: queremos render(destShape) = M_srcShape × M_intermediates (sin srcRoot, sin
        ' destRoot). Como render(destShape) = destShape.localT × destParentChain, despejando:
        '   destShape.localT = M_srcShape × M_intermediates × destParentChain^-1
        ' donde M_srcShape × M_intermediates = GetGlobalTransform(srcShape) × srcRoot^-1.
        '
        ' Skinned: NO se bakea. La posición de un skinned viene SOLO del bone palette y el
        ' skin data (xformSkinToBone embebido en BSSkin_BoneData / NiSkinData). SkinningHelper.vb:151
        ' y :968 usan Matrix4d.Identity — ignoran shape.T/R/S Y todo el parent chain. Paridad OS
        ' Anim.cpp:717-728 (GetTransformShapeToGlobal para skinned = inv(xformGlobalToSkin), sin
        ' recorrer parent chain). NiflySharp re-mapea los bone refs por nombre cross-file
        ' (NifFile.cs:788-821), así que la palette llega coherente al destino. Si un skinned
        ' post-merge aparece mal posicionado, el problema es xformGlobalToSkin del source vs el
        ' esqueleto destino, no parent chain.
        '
        ' Same-file (srcNif == Me): NiflySharp parentea al mismo padre que srcShape (NifFile.cs:752),
        ' la posición se preserva sin baking.
        If destShape IsNot Nothing AndAlso Not destShape.IsSkinned AndAlso Not Object.ReferenceEquals(srcNif, Me) Then
            Dim srcParent = TryCast(srcNif.GetParentNode(srcShape), NiNode)
            Dim srcRoot = srcNif.GetRootNode()
            ' Sólo bakear si hay NiNodes intermedios. Si srcParent IS srcRoot (flat NIF), skip.
            If srcParent IsNot Nothing AndAlso Not Object.ReferenceEquals(srcParent, srcRoot) Then
                Dim oldShapeToWorld As Matrix4d = Transform_Class.GetGlobalTransform(srcShape, srcNif).ToMatrix4d()
                Dim srcRootMat As Matrix4d = If(srcRoot IsNot Nothing,
                    Transform_Class.GetGlobalTransform(srcRoot, srcNif).ToMatrix4d(),
                    Matrix4d.Identity)
                Dim srcRootInv As Matrix4d = srcRootMat
                srcRootInv.Invert()
                ' shapeWithoutRoot = M_srcShape × M_intermediates (sin srcRoot)
                Dim shapeWithoutRoot As Matrix4d = oldShapeToWorld * srcRootInv

                Dim destParentNode = TryCast(Me.GetParentNode(destShape), NiNode)
                Dim destParentToWorld As Matrix4d = If(destParentNode IsNot Nothing,
                    Transform_Class.GetGlobalTransform(destParentNode, Me).ToMatrix4d(),
                    Matrix4d.Identity)
                Dim destParentInverse As Matrix4d = destParentToWorld
                destParentInverse.Invert()
                Dim newLocalMat As Matrix4d = shapeWithoutRoot * destParentInverse
                Dim newLocal As New Transform_Class(newLocalMat)
                destShape.Translation = newLocal.Translation
                destShape.Rotation = newLocal.Rotation
                destShape.Scale = newLocal.Scale
            End If
        End If

        ' Acá vivía un workaround manual del aliasing de NiSkinData.BoneList[].VertexWeights, escrito
        ' cuando el DeepCopy de NiflySharp cortaba en los structs. YA NO HACE FALTA: el fork lo
        ' resuelve en el código generado, y mantener la copia a mano sólo duplicaba trabajo y sugería
        ' un bug inexistente. Verificado en el generador —
        '   NiSkinData copy-ctor:      _boneList.ConvertAll(e => e.DeepClone())
        '   BoneData.DeepClone:        VertexWeights = new List<BoneVertData>(this.VertexWeights)
        '   NiSkinPartition copy-ctor: _partitions.ConvertAll(e => e.DeepClone())
        '   SkinPartition.DeepClone:   re-aloja sus OCHO listas (Bones, VertexMap, VertexWeights,
        '                              Strips, StripLengths, Triangles, BoneIndices, TrianglesCopy)
        ' ⇒ un shape clonado NO comparte listas de skin con el NIF fuente.

        Return destShape
    End Function


    ''' Returns the internal triParts list from a NiSkinPartition via reflection.
    ''' triParts is internal to NiflySharp; reflection lets us read/write it without
    ''' adding any public API to that library.
    Private Shared Function GetTriParts(skinPart As NiflySharp.Blocks.NiSkinPartition) As List(Of Integer)
        Static field As Reflection.FieldInfo = GetType(NiflySharp.Blocks.NiSkinPartition).GetField(
            "triParts", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        Return CType(field.GetValue(skinPart), List(Of Integer))
    End Function

    ''' Removes partitions with no triangle assignments from NiSkinPartition.Partitions
    ''' and BSDismemberSkinInstance.Partitions, remapping triParts accordingly.
    ''' Prevents the NiflySharp null-partBones crash for empty partitions.
    Private Sub CompactEmptyPartitions(shape As INiShape)
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing OrElse skinPart.Partitions Is Nothing OrElse skinPart.Partitions.Count = 0 Then Return
        Dim triPartsField = GetTriParts(skinPart)

        Dim triCount(skinPart.Partitions.Count - 1) As Integer
        If triPartsField.Count > 0 Then
            ' triParts is populated — use it to count triangles per partition.
            For Each partIdx In triPartsField
                If partIdx >= 0 AndAlso partIdx < triCount.Length Then triCount(partIdx) += 1
            Next
        ElseIf skinPart.Partitions.Any(Function(p) p.TrianglesCopy IsNot Nothing) Then
            ' triParts was cleared (e.g., after RemapSkinPartitionTriangles) but TrianglesCopy
            ' is set — use TrianglesCopy counts directly.
            For i As Integer = 0 To skinPart.Partitions.Count - 1
                Dim p = skinPart.Partitions(i)
                triCount(i) = If(p.TrianglesCopy IsNot Nothing, p.TrianglesCopy.Count, 0)
            Next
        Else
            Return  ' truly fresh load; triParts not yet computed — let base handle
        End If

        Dim oldToNew(triCount.Length - 1) As Integer
        Dim newIdx As Integer = 0
        For i As Integer = 0 To triCount.Length - 1
            oldToNew(i) = If(triCount(i) > 0, newIdx, -1)
            If triCount(i) > 0 Then newIdx += 1
        Next
        If newIdx = triCount.Length Then Return  ' nothing to compact

        For i As Integer = 0 To triPartsField.Count - 1
            Dim p = triPartsField(i)
            If p >= 0 AndAlso p < oldToNew.Length Then triPartsField(i) = oldToNew(p)
        Next

        Dim newParts As New List(Of SkinPartition)(newIdx)
        For i As Integer = 0 To skinPart.Partitions.Count - 1
            If oldToNew(i) >= 0 Then newParts.Add(skinPart.Partitions(i))
        Next
        skinPart.Partitions = newParts
        skinPart.NumPartitions = CUInt(newParts.Count)

        Dim bsdSkinInst = TryCast(skinInst, BSDismemberSkinInstance)
        If bsdSkinInst?.Partitions IsNot Nothing Then
            Dim newBsdParts As New List(Of BodyPartList)(newIdx)
            For i As Integer = 0 To Math.Min(bsdSkinInst.Partitions.Count, oldToNew.Length) - 1
                If oldToNew(i) >= 0 Then newBsdParts.Add(bsdSkinInst.Partitions(i))
            Next
            bsdSkinInst.Partitions = newBsdParts
            bsdSkinInst.NumPartitions = CUInt(newBsdParts.Count)
        End If
    End Sub

    ''' <summary>
    ''' Shadows NifFile.UpdateSkinPartitions: compacts empty partitions first so the
    ''' unmodified NiflySharp code never encounters a null partBones entry.
    '''
    ''' ORDER CONTRACT (critical for correctness, especially NiTriShape family):
    '''   1. Any caller mutating per-vertex data (positions, per-vertex skin) MUST do so
    '''      via the IShapeGeometry adapter (SetVertexPositions, SetSkinning, SetTriangles)
    '''      BEFORE calling this.
    '''   2. For the NiTriShape family, the partition is regenerated from NiSkinData by
    '''      NiflySharp's UpdateSkinPartitions.  If the caller calls UpdateSkinPartitions
    '''      BEFORE SetSkinning, the partition is built from stale NiSkinData → saved NIF
    '''      has partition inconsistent with NiSkinData → skinning in-game corrupt.
    '''   3. SkinningHelper.InjectToTrishape + Wardrobe_Manager.BuildingForm follow this
    '''      order correctly (InjectToTrishape calls adapter.SetSkinning;
    '''      UpdateSkinPartitions is called later by BuildingForm).  Any new caller that
    '''      batches these operations must respect the same order.
    ''' </summary>
    Public Shadows Sub UpdateSkinPartitions(shape As INiShape)
        CompactEmptyPartitions(shape)
        MyBase.UpdateSkinPartitions(shape)
    End Sub

    ''' <summary>
    ''' Returns the BSDismemberBodyPartType value (cast to Integer) for each triangle in
    ''' the shape's skin partition, in triangle-list order.  Returns -1 for unassigned
    ''' triangles or when there is no BSDismemberSkinInstance.  Returns Nothing when the
    ''' shape has no NiSkinInstance or no NiSkinPartition (e.g. FO4 shapes).
    ''' </summary>
    Public Function GetTriangleBodyParts(shape As INiShape) As List(Of Integer)
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return Nothing
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing Then Return Nothing

        Dim tris = shape.Triangles.ToList()

        ' Guard against the NiflySharp Count>0 bug in PrepareTrueTriangles:
        ' only call it when TrianglesCopy is null (fresh load); skip if already set.
        Dim triPartsField = GetTriParts(skinPart)
        If triPartsField.Count <> tris.Count Then
            If skinPart.Partitions.Any(Function(p) p.TrianglesCopy Is Nothing) Then
                skinPart.PrepareTrueTriangles()
            End If
            skinPart.GenerateTriPartsFromTrueTriangles(tris)
        End If

        Dim bsdSkinInst = TryCast(skinInst, BSDismemberSkinInstance)
        Dim bsdParts = bsdSkinInst?.Partitions
        Dim result As New List(Of Integer)(tris.Count)
        For Each partInd In triPartsField
            If bsdParts IsNot Nothing AndAlso partInd >= 0 AndAlso partInd < bsdParts.Count Then
                result.Add(CInt(bsdParts(partInd).BodyPart))
            Else
                result.Add(-1)
            End If
        Next
        Return result
    End Function

    ''' <summary>SSE per-partition skin occlusion — the engine-faithful analog of FO4's per-segment
    ''' <see cref="BSTriShapeGeometry.ComputeHiddenTriangles"/>. Byte-level RE of SkyrimSE.exe
    ''' (ApplyOcclusionToGeometry 0x1403C56B0 → SetPartitionVisible 0x14021A530, see
    ''' 23-armor-oclusion-sse-re): Skyrim hides a skinned shape PER-PARTITION — the
    ''' BSDismemberSkinInstance partition whose body-part biped slot is covered by a worn item is
    ''' hidden, its siblings stay visible. Returns a per-triangle "hidden" array aligned with the
    ''' shape's GetTriangles() order (same order geom.Indices / EnsureZapIndexBuffer consume), or
    ''' Nothing when the shape has no dismember partitions (caller falls back to whole-node hide, which
    ''' mirrors the engine's SetAppCulled fallback for non-dismember geometry).
    '''
    ''' Slot rule: body-part value v is a Skyrim SBP; dismemberment-state variants (130-161 / 230-261)
    ''' fold to the canonical biped slot 30-61 via <c>30 + ((v-30) mod 100)</c> (engine folds the same
    ''' three ranges). A triangle is hidden iff its slot bit (slot-30) is set in coveredSlotsMask.
    ''' Non-biped body parts (FO3-style gore 0-9, or -1 unassigned) are never slot-occluded. NO N+100
    ''' inverse-swap (that is an FO4-only Pipboy-forearm mechanism; Skyrim has none).</summary>
    Public Function ComputeHiddenTrianglesDismember(shape As INiShape, coveredSlotsMask As UInteger) As Boolean()
        Dim bodyParts = GetTriangleBodyParts(shape)
        If bodyParts Is Nothing OrElse bodyParts.Count = 0 Then Return Nothing
        Dim result(bodyParts.Count - 1) As Boolean
        For ti = 0 To bodyParts.Count - 1
            Dim v As Integer = bodyParts(ti)
            If v < 30 Then Continue For   ' gore/unassigned — not a biped-slot partition
            Dim slot As Integer = 30 + ((v - 30) Mod 100)
            If slot >= 30 AndAlso slot <= 61 Then
                result(ti) = (coveredSlotsMask And (1UI << (slot - 30))) <> 0UI
            End If
        Next
        Return result
    End Function

    ''' <summary>
    ''' Pre-sets body-part assignments per triangle before calling UpdateSkinPartitions.
    ''' triangleBodyParts(i) is the BSDismemberBodyPartType value (cast to Integer) for
    ''' triangle i; -1 means partition 0.  Missing body-part partitions are added to
    ''' BSDismemberSkinInstance automatically.  No-op for FO4 shapes.
    ''' </summary>
    Public Sub SetTriangleBodyParts(shape As INiShape, triangleBodyParts As IReadOnlyList(Of Integer))
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing Then Return

        Dim bsdSkinInst = TryCast(skinInst, BSDismemberSkinInstance)
        Dim bsdParts = bsdSkinInst?.Partitions

        ' Build body-part value → partition index map from existing partitions.
        Dim bpToPartIndex As New Dictionary(Of Integer, Integer)()
        If bsdParts IsNot Nothing Then
            For i As Integer = 0 To bsdParts.Count - 1
                bpToPartIndex(CInt(bsdParts(i).BodyPart)) = i
            Next
        End If

        ' Build TriParts, adding new partitions to bsdSkinInst as needed.
        Dim newTriParts As New List(Of Integer)(triangleBodyParts.Count)
        For Each bp In triangleBodyParts
            If bp < 0 Then
                newTriParts.Add(0)
                Continue For
            End If
            Dim partIdx As Integer
            If Not bpToPartIndex.TryGetValue(bp, partIdx) Then
                partIdx = If(bsdParts IsNot Nothing, bsdParts.Count, 0)
                bpToPartIndex(bp) = partIdx
                If bsdSkinInst IsNot Nothing Then
                    If bsdParts Is Nothing Then
                        bsdSkinInst.Partitions = New List(Of BodyPartList)()
                        bsdParts = bsdSkinInst.Partitions
                    End If
                    bsdParts.Add(New BodyPartList() With {
                        .PartFlag = BSPartFlag.PF_EDITOR_VISIBLE,
                        .BodyPart = CType(bp, BSDismemberBodyPartType)
                    })
                    bsdSkinInst.NumPartitions = CUInt(bsdParts.Count)
                End If
            End If
            newTriParts.Add(partIdx)
        Next

        ' Sync NiSkinPartition.Partitions count to match BSDismemberSkinInstance.
        Dim numParts As Integer
        If bsdParts IsNot Nothing Then
            numParts = bsdParts.Count
        Else
            numParts = Math.Max(1, If(newTriParts.Count > 0, newTriParts.Max() + 1, 1))
        End If
        If skinPart.Partitions Is Nothing Then skinPart.Partitions = New List(Of SkinPartition)()
        Do While skinPart.Partitions.Count < numParts
            skinPart.Partitions.Add(New SkinPartition())
        Loop
        skinPart.NumPartitions = CUInt(skinPart.Partitions.Count)

        ' Set TriParts directly so UpdateSkinPartitions skips PrepareTriParts.
        GetTriParts(skinPart).Clear()
        GetTriParts(skinPart).AddRange(newTriParts)
    End Sub

    ''' <summary>
    ''' Remaps vertex indices in the shape's skin partition TrianglesCopy using oldToNew.
    ''' Triangles whose vertices are absent from the map are dropped.
    ''' Call before UpdateSkinPartitions whenever vertex compaction changes indices
    ''' (e.g. zap removal or shape splitting).
    ''' </summary>
    ''' <summary>
    ''' Reindexa <c>NiSkinData.BoneList[].VertexWeights</c> al espacio de vértices posterior a una
    ''' compactación, descartando los pesos de los vértices que se cayeron.
    '''
    ''' <para>⛔ HACE FALTA AUNQUE YA SE HAYA LLAMADO A <c>SetSkinning</c>. En la familia BSTriShape
    ''' (FO4/SSE) el skin vive DOS VECES: por vértice dentro del vertex data (que es lo que
    ''' <c>BSTriShapeGeometry.SetSkinning</c> escribe) y otra vez en <c>NiSkinData.BoneList</c>, como
    ''' lista de pares (índice de vértice, peso). El adapter NO toca la segunda — sólo la familia
    ''' NiTriShape la reconstruye.</para>
    '''
    ''' <para>Y esa segunda copia no es decorativa: <c>UpdateSkinPartitions</c> regenera la partición
    ''' leyendo de ahí (<c>vertBoneWeights[bw.Index]</c> en NifFile.cs). Si los índices quedaron en el
    ''' espacio viejo, cada peso se aplica al vértice EQUIVOCADO y los que apuntan más allá del nuevo
    ''' conteo se pierden ⇒ la malla sale con vértices disparados. MEDIDO en un export con oclusión:
    ''' el shape pasó de 2434 a 1900 vértices y salió reventado, mientras el shape hermano del mismo
    ''' NIF —que no se compactó— salió intacto.</para>
    '''
    ''' <para>Llamar ANTES de <see cref="UpdateSkinPartitions"/>, junto con
    ''' <see cref="RemapSkinPartitionTriangles"/>.</para></summary>
    Public Sub RemapSkinDataVertexWeights(shape As INiShape, oldToNew As IReadOnlyDictionary(Of Integer, Integer))
        If shape Is Nothing Then Return
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinData = GetBlock(skinInst.Data)
        If skinData Is Nothing OrElse skinData.BoneList Is Nothing Then Return

        Dim newBoneList As New List(Of NiflySharp.Structs.BoneData)(skinData.BoneList.Count)
        For Each bone In skinData.BoneList
            Dim copy = bone   ' struct copy
            If bone.VertexWeights IsNot Nothing Then
                Dim remapped As New List(Of NiflySharp.Structs.BoneVertData)(bone.VertexWeights.Count)
                For Each vw In bone.VertexWeights
                    Dim ni As Integer
                    If oldToNew.TryGetValue(CInt(vw.Index), ni) AndAlso ni >= 0 Then
                        Dim c = vw   ' struct copy
                        c.Index = CUShort(ni)
                        remapped.Add(c)
                    End If
                Next
                copy.VertexWeights = remapped
                ' NumVertices de un BoneData es la cantidad de PESOS de ese hueso, no la del shape.
                copy.NumVertices = CUShort(remapped.Count)
            End If
            newBoneList.Add(copy)
        Next
        skinData.BoneList = newBoneList
    End Sub

    Public Sub RemapSkinPartitionTriangles(shape As INiShape, oldToNew As IReadOnlyDictionary(Of Integer, Integer))
        ' El caller obtiene el shape con `geom.Geometry?.BackingShape`, que puede dar Nothing para una
        ' SkinnedGeometry que no viene de un NIF. Sin este guard reventaba con NRE en vez de ser no-op,
        ' igual que los dos guards de abajo cuando no hay skin o no hay particion.
        If shape Is Nothing Then Return
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing Then Return

        ' Guard against the NiflySharp Count>0 bug: only call PrepareTrueTriangles
        ' when TrianglesCopy is null (fresh load); empty [] means intentionally zapped.
        If skinPart.Partitions.Any(Function(p) p.TrianglesCopy Is Nothing) Then
            skinPart.PrepareTrueTriangles()
        End If

        For i As Integer = 0 To skinPart.Partitions.Count - 1
            Dim p = skinPart.Partitions(i)
            If p.TrianglesCopy Is Nothing OrElse p.TrianglesCopy.Count = 0 Then Continue For
            Dim remapped As New List(Of Triangle)(p.TrianglesCopy.Count)
            For Each t In p.TrianglesCopy
                Dim nv1 As Integer, nv2 As Integer, nv3 As Integer
                If oldToNew.TryGetValue(CInt(t.V1), nv1) AndAlso
                   oldToNew.TryGetValue(CInt(t.V2), nv2) AndAlso
                   oldToNew.TryGetValue(CInt(t.V3), nv3) Then
                    remapped.Add(New Triangle(CUShort(nv1), CUShort(nv2), CUShort(nv3)))
                End If
            Next
            p.TrianglesCopy = remapped
            p.NumTriangles = CUShort(remapped.Count)
            skinPart.Partitions(i) = p
        Next
        GetTriParts(skinPart).Clear()
    End Sub

End Class
