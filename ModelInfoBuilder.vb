Imports System.IO
Imports System.Linq
Imports BSA_BA2_Library_DLL.BethesdaArchive.Core
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>Deriva el contenido del bloque de <b>Model Information</b> (<c>MODT</c> / <c>MO2T</c> /
''' <c>MO3T</c> / <c>MO4T</c> / <c>MO5T</c> / <c>DMDT</c> / <c>NAM2</c> / <c>NAM5</c>) a partir de la
''' MALLA: la lista de texturas, la de materiales, la de addon nodes y el contador de color.
'''
''' <para><b>Quien construye el bloque de verdad es el CreationKit</b>, no el juego. Las direcciones,
''' sobre `CreationKit.exe` de Fallout 4:</para>
''' <list type="bullet">
''' <item><c>0x1408E8DC0</c> — <c>TESModel::SaveBuffer</c>. Si el nombre de archivo esta vacio SALE SIN
''' ESCRIBIR NADA, ni el nombre ni el bloque. Esa es la ley detras de "el MODL y su bloque viajan
''' juntos".</item>
''' <item><c>0x1408EE3D0</c> — el emisor del subrecord. Escribe, en este orden:
''' <c>u32 = 4</c> (cantidad de contadores, constante en el codigo), <c>u32</c> texturas,
''' <c>u32</c> addon nodes, <c>u32</c> = <c>TESModel+0x29</c>, <c>u32</c> materiales, y despues las
''' entradas de textura (12 B), los addon nodes (4 B) y los materiales (12 B). Cada entrada de 12 B es
''' <c>{u32 hash del nombre, u32 extension, u32 hash de la carpeta}</c>.</item>
''' <item><c>0x140355680</c> — <c>TESModelTri::SaveBuffer</c>: el <c>MODC</c> se emite solo si el float
''' es <c>&lt; FLT_MAX</c>. El centinela de "no esta" es FLT_MAX, no el cero.</item>
''' <item><c>0x140C09E20</c> — <c>BGSDebris::SaveBuffer</c> hace <c>xor r8d, r8d</c>: <b>DEBR escribe
''' el contador de color en CERO siempre</b> (31 de 31 bloques del corpus).</item>
''' </list>
'''
''' <para>⛔⛔ <b>EL ORDEN DE LAS ENTRADAS NO ES DERIVABLE, y esta probado.</b> Siete mallas del corpus
''' aparecen con el MISMO conjunto de texturas en DISTINTO orden en dos records distintos — el caso
''' limpio es <c>base meshes\actors\deathclaw\characterassets\skeleton.nif</c>, que sale con las dos
''' formas al derecho en un record y al reves en otro. El mismo archivo produce dos salidas, asi que no
''' hay funcion que lo derive: depende de en que orden resolvieron los recursos en la sesion del CK.
''' <b>El bloque que sale de aca tiene el mismo CONTENIDO y puede tener otro ORDEN que el que hubiera
''' escrito el CK.</b> Funcionalmente da igual —es una lista de recursos a precargar y xEdit muestra
''' las mismas entradas—, pero un diff byte a byte contra un esp hecho con el CK va a marcar el
''' subrecord. Es una consecuencia ACEPTADA, no un defecto.</para>
'''
''' <para><b>Lo que acierta</b>, medido por <c>Tools\ModelInfoDerivationGate</c> sobre la poblacion
''' LIMPIA (record de un plugin del juego + malla de un archive del juego + ningun material ni textura
''' reemplazado por un mod): <b>Fallout 4 7.799 de 7.816 = 99,78 %</b> y
''' <b>Skyrim SE 8.867 de 9.001 = 98,51 %</b> en CONTENIDO. De lo que falta, una parte es el archivo el
''' que esta mal: 1 bloque en FO4 y 11 en SSE listan las texturas de OTRA malla (probado:
''' <c>ARMO 0010FC28 EnchArmorElvenShieldBlock04</c> tiene malla <c>ElvenShield.nif</c> y su bloque
''' lista <c>armor\dwarven\dwarvenshield</c>).</para></summary>
Public Module ModelInfoBuilder

    '==============================================================================================
    ' Las entradas
    '==============================================================================================

    ''' <summary>Una entrada del bloque: el trio con el que un BA2 indexa un archivo.</summary>
    Public NotInheritable Class ModelInfoEntrada
        Public ReadOnly Hash As UInteger
        ''' <summary>Cuatro caracteres, rellenados con NUL. `dds` viaja como <c>"dds" &amp; NUL</c>.</summary>
        Public ReadOnly Ext As String
        Public ReadOnly Dir As UInteger
        Public Sub New(hash As UInteger, ext As String, dir As UInteger)
            Me.Hash = hash : Me.Ext = ext : Me.Dir = dir
        End Sub
    End Class

    ''' <summary>Lo derivado. <see cref="Motivo"/> vacio = se pudo derivar; con texto = NO se pudo, y
    ''' entonces no hay nada que escribir y el bloque que el record ya traia se conserva.</summary>
    Public NotInheritable Class ModelInfoDerivado
        Public Property Motivo As String = ""
        Public ReadOnly Texturas As New List(Of ModelInfoEntrada)
        Public ReadOnly Materiales As New List(Of ModelInfoEntrada)
        Public ReadOnly AddonNodes As New List(Of UInteger)
        ''' <summary>El tercer contador (<c>TESModel+0x29</c>, que xEdit llama <c>SRGB</c>).</summary>
        Public Property Color As Integer
        Public ReadOnly Property Pudo As Boolean
            Get
                Return String.IsNullOrEmpty(Motivo)
            End Get
        End Property
    End Class

    '==============================================================================================
    ' Las ranuras y su orden — MEDIDO, no elegido
    '==============================================================================================

    ''' <summary>Una ranura del material. <see cref="EsColor"/> es lo que decide el contador.
    ''' <para>⛔⛔ <b>NO es la tabla del render y no se puede reusar
    ''' <c>Render.ColorTextures_Path_List</c>.</b> Esa contesta otra pregunta —como tiene que samplear
    ''' la GPU— y difiere en tres puntos: alla el <c>Diffuse</c> deja de ser color cuando
    ''' <c>GrayscaleToPaletteColor</c> esta prendido, el <c>InnerLayer</c> SI es color, y los BGEM
    ''' NUNCA son sRGB. Medido sobre 35.708 bloques: aplicar el flip de <c>GrayscaleToPaletteColor</c>
    ''' baja de 99,97 % a 88,2 %, y sacar los BGEM baja a 99,48 %.</para></summary>
    Private NotInheritable Class Ranura
        Public ReadOnly Nombre As String
        Public ReadOnly EsColor As Boolean
        Public ReadOnly Leer As Func(Of FO4UnifiedMaterial_Class, String)
        Public Sub New(nombre As String, esColor As Boolean, leer As Func(Of FO4UnifiedMaterial_Class, String))
            Me.Nombre = nombre : Me.EsColor = esColor : Me.Leer = leer
        End Sub
    End Class

    ''' <summary>El orden de <b>Fallout 4</b>, derivado por grafo de precedencia sobre 16.866 bloques de
    ''' un solo material: <c>7 → 2 → 1 → 5 → 0 → 3 → 4</c> sobre los indices del
    ''' <c>BSShaderTextureSet</c> (las constantes estan en <c>FO4UnifiedMaterial_Class.vb:4230-4237</c>).
    ''' <para><c>Wrinkles</c> ocupa la MISMA posicion que <c>Glow</c>: el caso que lo mostro es
    ''' <c>HDPT 001B51A9 SupermutantHeadStrong</c>, cuyo <c>…crease_n.dds</c> sale SEGUNDO. Ningun
    ''' material del corpus trae las dos ranuras a la vez.</para>
    ''' <para>⛔ Sin comentarios sueltos DENTRO del inicializador: una linea de comentario entre dos
    ''' elementos de un <c>{ … }</c> corta la continuacion implicita de VB.</para></summary>
    Private ReadOnly RANURAS_FO4 As Ranura() = {
        New Ranura("SmoothSpec", False, Function(m) m.SmoothSpecTexture),
        New Ranura("Glow", False, Function(m) m.GlowTexture),
        New Ranura("Wrinkles", False, Function(m) m.WrinklesTexture),
        New Ranura("Normal", False, Function(m) m.NormalTexture),
        New Ranura("EnvmapMask", False, Function(m) m.EnvmapMaskTexture),
        New Ranura("Diffuse", True, Function(m) m.Diffuse_or_Base_Texture),
        New Ranura("Greyscale", True, Function(m) m.GreyscaleTexture),
        New Ranura("Envmap", True, Function(m) m.EnvmapTexture),
        New Ranura("InnerLayer", False, Function(m) m.InnerLayerTexture),
        New Ranura("Displacement", False, Function(m) m.DisplacementTexture),
        New Ranura("Specular", False, Function(m) m.SpecularTexture),
        New Ranura("Lighting", False, Function(m) m.LightingTexture),
        New Ranura("Flow", False, Function(m) m.FlowTexture),
        New Ranura("DistanceFieldAlpha", False, Function(m) m.DistanceFieldAlphaTexture)
    }

    ''' <summary>El orden de <b>Skyrim SE</b>, medido sobre los bloques de UNA sola forma cuyo conjunto
    ''' cerraba y el orden no: <c>Diffuse · Normal · Glow|Lighting · SmoothSpec · Envmap · Flow</c>.
    ''' <para>⛔ <b>No es ascendente por indice</b> —seria 0,1,2,4,5,6,7 y el corpus pone el 7 antes del
    ''' 4—. El ascendente coincidia en los casos de dos texturas, que son mayoria, y por eso pasaba
    ''' desapercibido. Con el orden de Fallout 4 el byte-a-byte de Skyrim da 214 sobre 10.054; con
    ''' este, 9.083.</para></summary>
    Private ReadOnly RANURAS_SSE As Ranura() = OrdenarPorNombre(
        {"Diffuse", "Normal", "Glow", "Lighting", "SmoothSpec", "InnerLayer", "Envmap", "Flow"})

    Private Function OrdenarPorNombre(nombres As String()) As Ranura()
        Dim outp As New List(Of Ranura)
        For Each n In nombres
            For Each r In RANURAS_FO4
                If r.Nombre = n Then outp.Add(r) : Exit For
            Next
        Next
        For Each r In RANURAS_FO4
            If Not outp.Contains(r) Then outp.Add(r)
        Next
        Return outp.ToArray()
    End Function


    ''' <summary>⛔ En Skyrim la ranura 6 del <c>BSShaderTextureSet</c> NO llega al material: el mapeo
    ''' compartido <c>FO4UnifiedMaterial_Class.ReadBgsmTexturesFromTextureSet</c> (…vb:4286-4292) la
    ''' pone en <c>InnerLayerTexture</c> solo si el material es FaceGen, en <c>LightingTexture</c> solo
    ''' si esa quedo vacia, y en cualquier otro caso <b>la descarta</b>. Esa tabla contesta otra
    ''' pregunta —como samplea el render—, igual que <c>Render.ColorTextures_Path_List</c>; para el
    ''' bloque hay que leer la ranura del texture set, que en Skyrim ES la fuente (no hay BGSM).
    ''' <para>El caso que lo muestra es <c>ARMA 0005B2E8 NakedAtronachFrost</c>
    ''' (<c>AtronachFrost.nif</c>, shader <c>MultiLayerParallax</c>): la ranura 2 lleva el
    ''' subsurface, con lo cual <c>LightingTexture</c> queda ocupada y <c>frostatronachinner.dds</c>
    ''' se perdia. Medido sobre la poblacion limpia de Skyrim: <b>+1 equivalente y +1 byte a byte,
    ''' cero falsos positivos</b> en 9.001 bloques (no aparece ni una entrada "me SOBRAN de la ranura
    ''' InnerLayer").</para>
    ''' <para>Del ORDEN el corpus dice lo que puede: <b>al final NO va</b> (ahi el byte-a-byte del
    ''' corpus completo baja de 9.442 a 9.441). Entre "despues de Lighting" y "despues de SmoothSpec"
    ''' empata —el unico caso tiene la ranura 7 repetida y deduplicada—, asi que va entre las dos y se
    ''' toma la segunda; las dos dan los mismos bytes sobre el corpus entero. La perilla
    ''' <c>--slot6</c> del gate vuelve a preguntarlo.</para></summary>
    Private Function RanuraCrudaDelTexSet(nif As Nifcontent_Class_Manolo, forma As INiShape, idx As Integer) As String
        Try
            Dim shad = TryCast(nif.GetShader(forma), INiShader)
            If shad Is Nothing Then Return ""
            Dim refe = shad.TextureSetRef
            If refe Is Nothing OrElse refe.Index < 0 OrElse refe.Index >= nif.Blocks.Count Then Return ""
            Dim ts = TryCast(nif.Blocks(refe.Index), BSShaderTextureSet)
            If ts Is Nothing OrElse ts.Textures Is Nothing OrElse idx >= ts.Textures.Count Then Return ""
            Return If(ts.Textures(idx)?.Content, "")
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>SYNC con <c>FO4UnifiedMaterial_Class.vb:3911-3912</c> (alla son <c>Private Const</c>):
    ''' la plantilla de la que hereda un material que no declara raiz.</summary>
    Private Const PLANTILLA_DEFECTO As String = "template/defaultTemplate_wet.bgsm"
    Private Const SENTINELA_WETNESS As Single = -1.0F

    '==============================================================================================
    ' La derivacion
    '==============================================================================================

    ''' <summary>Deriva el bloque desde la malla.
    ''' <param name="mallaRelativa">Ruta de la malla relativa a Data, ya normalizada con
    ''' <c>FO4UnifiedMaterial_Class.CorrectMeshPath</c> (o sea CON el prefijo <c>Meshes\</c>).</param>
    ''' <param name="swaps">Las sustituciones del material swap del record, ya normalizadas
    ''' (original → reemplazo). Vacio si el record no trae swap.</param></summary>
    Public Function Derivar(mallaRelativa As String,
                            swaps As Dictionary(Of String, String),
                            game As Canon.WbGame) As ModelInfoDerivado
        Dim d As New ModelInfoDerivado
        If String.IsNullOrWhiteSpace(mallaRelativa) Then
            d.Motivo = "no model filename"
            Return d
        End If
        If swaps Is Nothing Then swaps = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Dim bytes As Byte() = Nothing
        Try
            bytes = FilesDictionary_class.GetBytes(mallaRelativa)
        Catch ex As Exception
            bytes = Nothing
        End Try
        If bytes Is Nothing OrElse bytes.Length = 0 Then
            d.Motivo = $"mesh '{mallaRelativa}' is not installed"
            Return d
        End If

        Dim nif As New Nifcontent_Class_Manolo()
        Try
            nif.Load_Manolo(bytes)
        Catch ex As Exception
            d.Motivo = $"mesh '{mallaRelativa}' could not be read"
            Return d
        End Try

        Dim formas As List(Of INiShape)
        Try
            formas = nif.NifShapes.ToList()
        Catch ex As Exception
            d.Motivo = $"mesh '{mallaRelativa}' lists no shapes"
            Return d
        End Try

        Dim ordenRanuras = If(game = Canon.WbGame.Skyrim, RANURAS_SSE, RANURAS_FO4)
        Dim vistasTex As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim vistosMat As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim porForma As New List(Of List(Of (Ruta As String, Ranura As Ranura)))
        For k = 0 To formas.Count - 1
            porForma.Add(New List(Of (Ruta As String, Ranura As Ranura)))
        Next

        Dim iForma = -1
        For Each forma In formas
            iForma += 1
            Dim rel As Nifcontent_Class_Manolo.RelatedMaterial_Class = Nothing
            Try
                rel = nif.GetRelatedMaterial(forma)
            Catch ex As Exception
                rel = Nothing
            End Try
            If rel Is Nothing OrElse rel.material Is Nothing Then Continue For

            ' El MATERIAL SWAP del record REEMPLAZA el material de la forma, no se le agrega: medido,
            ' `agrega` da 8.344 equivalentes contra 8.581 de `reemplaza`.
            Dim materiales As New List(Of (Ruta As String, Mat As FO4UnifiedMaterial_Class, VinoDeSwap As Boolean))
            Dim rutaBase = FO4UnifiedMaterial_Class.CorrectMaterialPath(If(rel.path, ""))
            Dim rutaRepl As String = Nothing
            If rutaBase <> "" Then swaps.TryGetValue(rutaBase, rutaRepl)
            If rutaRepl IsNot Nothing Then
                Dim matRepl = MaterialResolver.TryLoadMaterialFromDictionary(rutaRepl, rel.material, forma, nif)
                If matRepl IsNot Nothing Then
                    materiales.Add((rutaRepl, matRepl, True))
                Else
                    materiales.Add((rutaBase, rel.material, False))
                End If
            Else
                materiales.Add((rutaBase, rel.material, False))
            End If

            For Each par In materiales
                If par.Ruta <> "" AndAlso vistosMat.Add(par.Ruta) Then
                    d.Materiales.Add(EntradaDeRuta(par.Ruta))
                End If
                ' La RAIZ: el bloque lista los materiales que el CK tuvo que ABRIR, y el de reemplazo
                ' no estaba en la malla — asi que lo abre, y al abrirlo resuelve su herencia de wetness
                ' subiendo por `RootMaterialPath`. El material propio de la forma ya venia resuelto del
                ' load del modelo. Medido: recorrer la cadena para TODOS da 3,92 %; solo para el del
                ' swap, 98,44 %.
                If par.VinoDeSwap Then
                    For Each raiz In CadenaDeRaices(par.Mat, forma, nif, par.VinoDeSwap)
                        If vistosMat.Add(raiz) Then d.Materiales.Add(EntradaDeRuta(raiz))
                    Next
                End If
                For Each r In ordenRanuras
                    Dim t As String = Nothing
                    If game = Canon.WbGame.Skyrim AndAlso r.Nombre = "InnerLayer" Then
                        t = RanuraCrudaDelTexSet(nif, forma, 6)
                    Else
                        Try
                            t = r.Leer(par.Mat)
                        Catch ex As Exception
                            t = Nothing
                        End Try
                    End If
                    If String.IsNullOrEmpty(t) Then Continue For
                    ' En Skyrim la ranura 5 (`Flow` para la app = ENVIRONMENT MASK) solo entra cuando
                    ' hay mapa de entorno: la mascara sin el mapa no la samplea nadie. Sin esta
                    ' condicion sobran 370 texturas de esa ranura y Skyrim queda en 94,41 % en vez de
                    ' 98,51 %.
                    If game = Canon.WbGame.Skyrim AndAlso r.Nombre = "Flow" Then
                        Dim env = ""
                        Try
                            env = par.Mat.EnvmapTexture
                        Catch ex As Exception
                        End Try
                        If String.IsNullOrEmpty(env) Then Continue For
                    End If
                    Dim rutaTex = FO4UnifiedMaterial_Class.CorrectTexturePath(t)
                    If rutaTex = "" Then Continue For
                    porForma(iForma).Add((rutaTex, r))
                Next
            Next
        Next

        ' ⛔⛔ EL RECORRIDO, que NO es el mismo en los dos juegos y esta DERIVADO del corpus:
        '   Fallout 4 -> DOS pasadas. Primero las ranuras que NO son de color recorriendo las formas al
        '     REVES, y despues las de color al DERECHO. El patron del archivo, reexpresado como
        '     (forma, ranura), lo dice solo:
        '       s2/SmoothSpec s2/Normal s1/SmoothSpec s1/Normal s0/SmoothSpec s0/Normal
        '       s0/Diffuse s1/Diffuse s2/Diffuse
        '     Byte-a-byte 7.012 -> 8.524 sobre 9.447. Con UNA forma degenera en el orden de ranuras.
        '   Skyrim SE -> UNA pasada, formas al derecho. Su patron pone el Diffuse PRIMERO, justo lo que
        '     la pasada de color de Fallout 4 manda al final.
        Dim emitir =
            Sub(idx As Integer, filtro As Integer)
                For Each par In porForma(idx)
                    If filtro < 2 AndAlso par.Ranura.EsColor <> (filtro = 1) Then Continue For
                    If Not vistasTex.Add(par.Ruta) Then Continue For
                    d.Texturas.Add(EntradaDeRuta(par.Ruta))
                    ' El contador cuenta una textura si ALGUNA de sus ranuras es de color, NO la
                    ' primera que la reclamo. Medido: por PRIMERA falla en 74 bloques limpios de
                    ' Fallout 4, por ALGUNA en 8. Y asi el contador NO depende del orden, que es lo
                    ' unico que no se puede derivar.
                    Dim esColorEnAlguna =
                        Enumerable.Any(porForma,
                            Function(lista) Enumerable.Any(lista,
                                Function(x) x.Ranura.EsColor AndAlso
                                            String.Equals(x.Ruta, par.Ruta, StringComparison.OrdinalIgnoreCase)))
                    If esColorEnAlguna Then d.Color += 1
                Next
            End Sub

        If game = Canon.WbGame.Skyrim Then
            For i = 0 To porForma.Count - 1
                emitir(i, 2)
            Next
        Else
            For i = porForma.Count - 1 To 0 Step -1
                emitir(i, 0)
            Next
            For i = 0 To porForma.Count - 1
                emitir(i, 1)
            Next
        End If

        ' Addon nodes: los `BSValueNode` de la malla, deduplicados, en orden de bloque. xEdit los
        ' declara como indices de `ADDN\Node Index`. Medido: 9.447/9.447 en FO4 y 10.054/10.054 en SSE,
        ' conjunto Y orden.
        Dim vistosAdd As New HashSet(Of UInteger)()
        Try
            For Each b In nif.Blocks
                Dim vn = TryCast(b, BSValueNode)
                If vn Is Nothing Then Continue For
                If vistosAdd.Add(vn.Value) Then d.AddonNodes.Add(vn.Value)
            Next
        Catch ex As Exception
            d.Motivo = $"mesh '{mallaRelativa}' lists no blocks"
            Return d
        End Try

        Return d
    End Function

    '==============================================================================================
    ' La cadena de raices
    '==============================================================================================

    ''' <summary>Los materiales que el CK tuvo que ABRIR para resolver la herencia de wetness de este
    ''' material, en el orden en que los abre. Es <c>ResolveEffectiveWetness</c> recorrido igual pero
    ''' registrando cada padre: mientras algun campo siga en el centinela −1 hay que subir, y subir es
    ''' abrir el archivo del padre. Si el material declara sus seis campos, la lista sale VACIA.
    ''' <para>SYNC: <c>FO4UnifiedMaterial_Class.ResolveEffectiveWetness</c> — mismo tope de 16 saltos,
    ''' misma guarda de ciclo, y el default se aplica UNA sola vez.</para>
    ''' <para>Un <b>BGEM no aporta raiz</b>: los seis campos son de BGSM, asi que no abre ningun padre.
    ''' Caso que lo mostro: <c>ARMO 001C4BE8 ClothesEyeGlasses</c>, cuyo bloque lista la raiz del
    ''' <c>.bgsm</c> y NO la del <c>.bgem</c>.</para></summary>
    Private Function CadenaDeRaices(mat As FO4UnifiedMaterial_Class, forma As INiShape,
                                    nif As Nifcontent_Class_Manolo,
                                    vinoDeSwap As Boolean) As List(Of String)
        Dim outp As New List(Of String)
        If mat Is Nothing Then Return outp
        Try
            If mat.IsBGEM() Then Return outp
        Catch ex As Exception
        End Try

        Dim eff = Wetness(mat)
        Dim ruta = PrimeraRaiz(mat, forma, nif, vinoDeSwap)
        Dim vistos As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim defaultUsado = False
        Dim salto = 0
        While salto < 16 AndAlso eff.Any(Function(v) v = SENTINELA_WETNESS)
            If String.IsNullOrEmpty(ruta) Then
                If defaultUsado Then Exit While
                ' ⛔⛔ LA PLANTILLA IMPLICITA ES SIEMPRE LA DEFAULT, y aca esto SE APARTA A PROPOSITO
                ' de `FO4UnifiedMaterial_Class.ResolveEffectiveWetness` (…vb:3932), que para un material
                ' con `Facegen`/`SkinTint` usa `SkinTemplate_Wet`. Las dos cosas son ciertas porque
                ' contestan preguntas distintas: alla se resuelve QUE MOJADO pinta el motor —y esa rama
                ' esta verificada con GhoulMatProbe—; aca se replica QUE ARCHIVOS ABRIO EL CK para
                ' escribir el bloque. Medido sobre la poblacion limpia de Fallout 4: con la rama de piel
                ' 7.800 equivalentes, sin ella 7.802.
                '   ⛔ Y NO es "no hay plantilla implicita": sacarla del todo derrumba a 7.682 (−118),
                ' asi que la plantilla va — lo que no va es la variante de piel.
                ' Caso testigo `ARMA 0303D2AC DLC03_AAMutatedWolf_Red`: `mutatedwof_redalpha.bgsm` no
                ' declara raiz y trae `SkinTint`, y el bloque del archivo NO lista `SkinTemplate_Wet`.
                ruta = PLANTILLA_DEFECTO
                defaultUsado = True
            End If
            Dim clave = FO4UnifiedMaterial_Class.CorrectMaterialPath(ruta)
            If clave = "" OrElse Not vistos.Add(clave.ToLowerInvariant()) Then Exit While
            Dim padre = MaterialResolver.TryLoadMaterialFromDictionary(clave, mat, forma, nif)
            If padre Is Nothing Then Exit While
            outp.Add(clave)
            Dim pv = Wetness(padre)
            For i = 0 To 5
                If eff(i) = SENTINELA_WETNESS AndAlso pv(i) <> SENTINELA_WETNESS Then eff(i) = pv(i)
            Next
            ruta = ""
            Try
                ruta = If(padre.RootMaterialPath, "")
            Catch ex As Exception
            End Try
            salto += 1
        End While
        Return outp
    End Function

    Private Function Wetness(m As FO4UnifiedMaterial_Class) As Single()
        Try
            Return {m.WetnessControlSpecScale, m.WetnessControlSpecPowerScale, m.WetnessControlSpecMinvar,
                    m.WetnessControlEnvMapScale, m.WetnessControlFresnelPower, m.WetnessControlMetalness}
        Catch ex As Exception
            Return {0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F}
        End Try
    End Function

    ''' <summary>El arranque de la cadena tiene DOS fuentes y las dos existen en el corpus: el
    ''' <c>RootMaterialPath</c> del ARCHIVO de material y el <c>RootMaterialName</c> del SHADER del NIF.
    ''' De 341 bloques a los que les faltaba la raiz, 247 la traian en el shader y 94 solo en el
    ''' material, asi que quedarse con una sola fuente deja fuera a los otros.</summary>
    Private Function PrimeraRaiz(mat As FO4UnifiedMaterial_Class, forma As INiShape,
                                 nif As Nifcontent_Class_Manolo, vinoDeSwap As Boolean) As String
        Dim delArchivo = "", delShader = ""
        Try
            delArchivo = If(mat.RootMaterialPath, "")
        Catch ex As Exception
        End Try
        Try
            delShader = If(TryCast(nif.GetShader(forma), INiShader)?.RootMaterialName, "")
        Catch ex As Exception
        End Try
        ' ⛔ El respaldo del shader describe al material que la FORMA declara. Si un material swap
        ' lo reemplazo, el que el motor resuelve es OTRO archivo y su raiz es la suya; la del shader
        ' quedo hablando del original. Medido sobre la poblacion limpia de Fallout 4: usarlo igual
        ' cuesta 1 bloque (7.799 -> 7.800 equivalentes). Caso testigo
        ' `ARMA 001236AC AAClothesInstituteLabCoatDivisionHead`, donde el bloque del archivo trae
        ' `DefaultTemplate_Wet` y el shader hacia poner `OutfitTemplate_Wet`.
        If vinoDeSwap Then delShader = ""
        Return If(delArchivo <> "", delArchivo, delShader)
    End Function

    '==============================================================================================
    ' Ruta -> entrada
    '==============================================================================================

    ''' <summary>Una ruta relativa a Data, convertida al trio con el que el bloque la nombra: hash del
    ''' tronco, extension de 4 bytes y hash de la carpeta. El hash es el del ARCHIVE
    ''' (<see cref="Ba2WriterCommon.Fo4PathHash"/>), que es de donde sale la ley: CRC32 con la tabla de
    ''' zlib, init 0 y SIN complemento final, en minuscula.</summary>
    Public Function EntradaDeRuta(ruta As String) As ModelInfoEntrada
        Dim limpia = If(ruta, "").Replace("/"c, "\"c).TrimStart("\"c)
        Dim carpeta = Path.GetDirectoryName(limpia)
        Dim tronco = Path.GetFileNameWithoutExtension(limpia)
        Dim ext = Path.GetExtension(limpia)
        If ext.StartsWith(".") Then ext = ext.Substring(1)
        ext = ext.ToLowerInvariant()
        If ext.Length > 4 Then ext = ext.Substring(0, 4)
        ext = ext.PadRight(4, ChrW(0))
        Return New ModelInfoEntrada(Ba2WriterCommon.Fo4PathHash(tronco), ext,
                                    Ba2WriterCommon.Fo4PathHash(If(carpeta, "")))
    End Function

End Module
