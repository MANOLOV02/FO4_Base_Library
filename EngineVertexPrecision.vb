Imports System.Linq
Imports NiflySharp
Imports NiflySharp.Blocks
Imports SysNumerics = System.Numerics

''' <summary>
''' Precision de los vertices de una malla skinneada de Fallout 4. Gateado a FO4.
'''
''' <para><b>La ley.</b> Al armar una cabeza, el motor recorre el subarbol y convierte a
''' <c>BSDynamicTriShape</c> cada geometria que tenga skin instance. El visitante es
''' <c>0x142275FE0</c> (slot 12 de <c>BSShaderResourceManager</c>, vtable <c>0x14291D158</c>):
''' recorre los hijos del nodo (<c>[rsi+0x128]</c> array, <c>[rsi+0x132]</c> count), se llama a si
''' mismo en cada <c>NiNode</c> hijo (<c>0x142276046</c>) y convierte al hijo cuando es geometria y
''' <c>[geom+0x140] != 0</c> (<c>0x142276051</c>; el setter del skin instance es <c>0x1416D5A70</c>).
''' La conversion es <c>0x141831D50</c>, cuyo UNICO llamador es <c>0x142276064</c>.</para>
'''
''' <para>Al reescribir el <c>vertexDesc</c> la conversion apaga el bit 44 (<c>0x141831ED2</c>
''' <c>and rdx, 0xFFFFEFFFFFFFFFFF</c>), prende el bit 54 (<c>0x141831EC5</c>) y le resta <b>8
''' fijo</b> al tamano del vertice (<c>0x141831EED</c> <c>sub eax, 8</c>), repitiendo el <c>-8</c>
''' en cada nibble de offset (<c>0x141831F0D</c>-<c>0x141832087</c>). Ese 8 es el bloque de posicion
''' en MEDIA precision (3 half + bitangentX half). En precision COMPLETA mide 16.
''' <b>No hay una sola rama que mire el bit 54 en toda la rutina.</b> Con precision completa el
''' stride y todos los offsets quedan 8 bytes corridos, <c>BLENDINDICES</c> se lee del vertice
''' siguiente y la malla se deforma a toda la pantalla.</para>
'''
''' <para><b>Alcance: HEAD PARTS.</b> Medido in-game el 21-sep con <c>DesireEarring.nif</c> (export
''' de Outfit Studio, vertice de 40 B): la MISMA malla puesta como outfit se ve bien y puesta como
''' head part explota. La guarda <c>[geom+0x140]</c> filtra DENTRO del subarbol que le pasen al
''' visitante; quien se lo pasa NO esta cerrado (el slot 12 se despacha por puntero, sin <c>lea</c>
''' a la vtable ni llamadores estaticos), y la prueba in-game dice que ese subarbol es la cabeza.
''' No se afirma mas que eso.</para>
'''
''' <para><b>Censo que lo respalda</b> (1.205.659 shapes en 218.377 NIF de
''' <c>Fallout4 - Meshes</c> + <c>MeshesExtra</c> + los 6 DLC): de las 23.225 shapes CON skin
''' instance, <b>0</b> traen precision completa; de las 1.087.624 con precision completa, <b>0</b>
''' tienen skin instance. Los dos conjuntos son disjuntos sin una sola excepcion. Y
''' <c>BSDynamicTriShape</c> no existe como bloque guardado en FO4 (0 de 218.377): lo fabrica el
''' motor al cargar, que es por lo que corre esta aritmetica.</para>
'''
''' <para><b>Gate por juego.</b> En Skyrim SE la posicion es SIEMPRE float32 (<c>BSVertexDataSSE</c>
''' no tiene campo half y su <c>Sync</c> no mira el bit 0x400), <c>CalcDataSizes</c> corta antes por
''' <c>StreamVersion == 100</c>, y el <c>sub eax, 8</c> no existe en <c>SkyrimSE.exe</c> (la
''' secuencia <c>and eax,0x3C ; sub eax,8</c> aparece 1 vez en Fallout4.exe y 0 en SkyrimSE.exe).
''' Ahi el bit 54 marca los <c>BSDynamicTriShape</c>, que el ARCHIVO ya trae convertidos
''' (21.054 de 21.054 con bit 44 apagado y bit 54 prendido, medido sobre
''' <c>Skyrim - Meshes0/1</c> + <c>_ResourcePack</c>) y el motor solo lo LEE
''' (<c>0x1410117A1</c> y <c>0x141560D4F</c>, los dos <c>test</c>). Por eso el gate es por ARCHIVO
''' (<c>Header.Version.IsFO4</c>) y no por el juego de la sesion.</para>
'''
''' <para><b>Canon.</b> Outfit Studio expone el toggle y lo gatea igual:
''' <c>ShapeProperties.cpp:1429-1431</c> - <i>"Changing precision only possible in FO4"</i> - y lo
''' repite al aplicar (<c>:2469</c>). En nifly C++ <c>SetFullPrecision(false)</c>
''' (<c>Geometry.cpp:819</c>) alcanza por si solo, porque su <c>BSVertexData</c> tiene UN solo
''' <c>Vector3 vert</c> (<c>VertexData.hpp:104</c>) y el medio/completo vive unicamente en el Sync
''' (<c>Geometry.cpp:506-517</c>). <b>NiflySharp partio ese campo en dos</b>
''' (<c>Vertex</c>/<c>VertexHalf</c>, <c>BitangentX</c>/<c>BitangentXHalf</c>), asi que aca hay que
''' mover los datos: bajar el flag a secas deja <c>VertexHalf</c> en cero, que es exactamente el NIF
''' roto que escribe el CK. <c>OptimizeFor</c> NO toca la precision (0 apariciones de
''' <c>SetFullPrecision</c> en <c>NifFile.cpp</c>).</para>
'''
''' <para>Los tamanos NO se tocan a mano: <c>NifFile.Save</c> llama a <c>FinalizeData</c>, que corre
''' <c>BSTriShape.CalcDataSizes</c> sobre cada shape y deriva offsets, <c>VertexSize</c> y
''' <c>DataSize</c> de los flags. En FO4 tampoco hay <c>NiSkinPartition</c> con copia de los
''' vertices que sincronizar: ese bloque de <c>FinalizeData</c> esta dentro de
''' <c>if (Header.Version.IsSSE())</c>.</para>
''' </summary>
Public Module EngineVertexPrecision

    ''' <summary>¿Este NIF es de Fallout 4? El gate va por el ARCHIVO y no por
    ''' <c>Config_App.Current.Game</c>: un proyecto puede tener cargado un NIF del otro juego, y lo
    ''' que decide el formato de los vertices es la version del archivo.</summary>
    Public Function EsNifDeFallout4(nif As NifFile) As Boolean
        If nif Is Nothing Then Return False
        If nif.Header Is Nothing OrElse nif.Header.Version Is Nothing Then Return False
        Return nif.Header.Version.IsFO4()
    End Function

    ''' <summary>¿Esta shape es una que el motor va a romper si se la usa en una cabeza?
    ''' <para>Los cuatro filtros, y de donde sale cada uno:</para>
    ''' <list type="bullet">
    ''' <item><description>es de la familia <c>BSTriShape</c> - es la unica que lleva
    ''' <c>vertexDesc</c>;</description></item>
    ''' <item><description>NO es <c>BSDynamicTriShape</c> - ahi el bit 54 es del TIPO, no de la
    ''' precision (en SSE lo llevan 21.054 de 21.054), y en FO4 la conversion ya corrio;</description></item>
    ''' <item><description>tiene skin instance - es <c>[geom+0x140] != 0</c>, la guarda literal del
    ''' motor en <c>0x142276051</c>;</description></item>
    ''' <item><description>declara precision completa - el bit 54 que la rutina no mira.</description></item>
    ''' </list></summary>
    Public Function ShapeEnPrecisionCompleta(sh As INiShape) As Boolean
        Dim bts = TryCast(sh, BSTriShape)
        If bts Is Nothing Then Return False
        If TypeOf bts Is BSDynamicTriShape Then Return False
        If Not bts.HasSkinInstance Then Return False
        Return bts.IsFullPrecision
    End Function

    ''' <summary>¿Hay al menos una shape que el motor va a romper? Es el predicado del AVISO (no
    ''' modifica nada). Devuelve False para cualquier NIF que no sea de Fallout 4.</summary>
    Public Function TienePrecisionCompleta(nif As NifFile) As Boolean
        If Not EsNifDeFallout4(nif) Then Return False
        Dim shapes = nif.GetShapes()
        If shapes Is Nothing Then Return False
        Return shapes.Any(Function(sh) ShapeEnPrecisionCompleta(sh))
    End Function

    ''' <summary>Baja a media precision todas las shapes que lo necesiten y devuelve cuantas
    ''' convirtio (0 = no habia ninguna, o el NIF no es de Fallout 4).
    ''' <para>⛔ El orden importa: las posiciones y las bitangentes se LEEN antes de bajar el flag.
    ''' Los getters de NiflySharp (<c>UpdateRawVertexPositions</c>, <c>UpdateRawBitangents</c>)
    ''' eligen el campo por <c>IsFullPrecision</c>, asi que despues de bajarlo leerian
    ''' <c>VertexHalf</c>/<c>BitangentXHalf</c>, que todavia estan en cero.</para>
    ''' <para>Se copian a listas propias en vez de pasar la que devuelve el getter: esa es la lista
    ''' interna (<c>rawVertexPositions</c>) que el propio setter redimensiona, y no hace falta
    ''' razonar sobre el aliasing para saber que esta bien.</para>
    ''' <para><c>SetBitangents</c> prende <c>HasTangents</c>, asi que solo se llama cuando la shape
    ''' YA las tenia: una shape sin tangentes no las gana por pasar por aca.</para></summary>
    Public Function BajarAMediaPrecision(nif As NifFile) As Integer
        If Not EsNifDeFallout4(nif) Then Return 0
        Dim shapes = nif.GetShapes()
        If shapes Is Nothing Then Return 0
        Dim convertidas As Integer = 0
        For Each sh In shapes.ToList()
            If Not ShapeEnPrecisionCompleta(sh) Then Continue For
            Dim bts = DirectCast(sh, BSTriShape)
            Dim posiciones As New List(Of SysNumerics.Vector3)(bts.VertexPositions)
            Dim bitangentes As List(Of SysNumerics.Vector3) = Nothing
            If bts.HasTangents Then bitangentes = New List(Of SysNumerics.Vector3)(bts.Bitangents)
            bts.IsFullPrecision = False
            bts.SetVertexPositions(posiciones)
            If bitangentes IsNot Nothing Then bts.SetBitangents(bitangentes)
            convertidas += 1
        Next
        Return convertidas
    End Function

End Module
