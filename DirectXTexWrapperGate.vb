Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text

''' <summary>Chequea que el <c>DirectXTexWrapper.dll</c> que hay al lado del exe es el que ESTA libreria
''' espera, y devuelve un diagnostico legible en vez de dejar que el desajuste salga como silencio.
'''
''' <para>POR QUE EXISTE. Hasta 1.5.2 la lib descubria <c>ConvertSubresources</c> por REFLEXION; 1.5.3 pasó
''' a llamadas tipadas y consume ademas <c>DdsMetadata.ArraySize</c>, la sobrecarga
''' <c>LoadTextures(..., onlyMipLevel)</c> y <c>ConvertSubresourcesToDds</c>. Un wrapper viejo al lado de una
''' lib nueva ya no degrada: es <c>MissingMethodException</c> / <c>TypeLoadException</c>. Y el desajuste de
''' PLATAFORMA es peor porque es MUDO — esta medido y escrito en el .vbproj del CLI: un wrapper x86 en un
''' proceso x64 tira <c>BadImageFormatException</c> adentro del lector de BA2 y CADA textura DX10 devuelve
''' 0 bytes, sin un cartel.</para>
'''
''' <para>NO ES UN GATE DE ARRANQUE DE LA GUI, Y ES A PROPOSITO. Con el wrapper roto, NPC Manager sigue
''' sirviendo para navegar el arbol y editar ESP, y el preview muestra el problema a la vista; abortar ahi
''' convierte una instalacion DEGRADADA en una app muerta. Los que abortan son los modos que ESCRIBEN
''' ARCHIVOS sin nadie mirando —los bakes headless y el CLI—, donde seguir significa dejar en disco DDS
''' equivocados, que es peor que fallar. En Wardrobe_Manager lo llaman el PACK y el UNPACK, que parsean cada
''' .dds con el wrapper via <c>Dx10Importer</c> / <c>EncodeDDSHeader</c> — el unpack es el caso mas grave de
''' todos: escribe un .dds vacio por textura y despues BORRA el .ba2, que era la unica copia. Su
''' <c>--build</c> escribe NIF y no decodifica una sola textura, y por eso no lo llama.</para>
'''
''' <para>NO PASA POR <c>CrashReport</c>. Ese modulo tiene un guard <c>_reported</c> de por vida: gastarlo
''' acá dejaria MUDA cualquier caida posterior de la sesion, que es justo el diagnostico para el que
''' existe.</para></summary>
Public Module DirectXTexWrapperGate

    Private _veredicto As String = Nothing
    Private ReadOnly _candado As New Object()

    ''' <summary>Cadena vacia si el wrapper es el esperado; si no, el diagnostico para el usuario.
    ''' <para>SE MEMOIZA TAMBIEN EL FALLO. Antes solo el exito, "por si el fallo era transitorio" — y eso
    ''' era tolerable con el gate en tres entry points, pero ahora vive en el chokepoint del bake: un barrido
    ''' de miles de NPC con el wrapper roto correria la sonda entera (4 llamadas nativas) una vez POR NPC y
    ''' serializada bajo este mismo SyncLock. Y los modos de falla reales —bitness equivocado, metodo que no
    ''' existe, DLL en cuarentena— no son transitorios.</para></summary>
    Public Function Verificar() As String
        SyncLock _candado
            If _veredicto IsNot Nothing Then Return _veredicto
            Try
                Sonda()
                _veredicto = ""
            Catch ex As Exception
                _veredicto = Diagnostico(ex)
            End Try
            Return _veredicto
        End SyncLock
    End Function

    ''' <summary><c>NoInlining</c> ES PARTE DEL CONTRATO, por el mismo motivo que <c>RealMain</c> en
    ''' <c>Program.vb</c>: el JIT resuelve las referencias del cuerpo ENTERO de un metodo antes de ejecutar su
    ''' primera linea. Con estas llamadas inlineadas en <see cref="Verificar"/>, la resolucion —y con ella el
    ''' <c>MissingMethodException</c> o el <c>BadImageFormatException</c>— ocurriria en el JIT de
    ''' <c>Verificar</c>, o sea ANTES de que su <c>Try</c> este instalado: el gate se convertiria en la caida
    ''' muda que vino a explicar.
    ''' <para>Las cuatro llamadas cubren las cuatro superficies del wrapper que la app consume hoy. Como el
    ''' JIT resuelve el cuerpo de una, que falte CUALQUIERA se ve acá.</para>
    ''' <para>El camino esta elegido para no poder fallar en una instalacion sana: DXGI 87
    ''' (B8G8R8A8_UNORM) esta en el mapa de formatos con <c>glInternalFormat</c> valido y
    ''' <c>isCompressed=false</c>, asi que con <c>useCompress:=False, forceOpenGL:=False</c> no hay
    ''' descompresion, ni generacion de mips, ni <c>Convert</c>, ni compresion BCn — y el header lo escribe y
    ''' lo lee el MISMO build de DirectXTex adentro del MISMO DLL, con lo cual el round-trip es
    ''' autoconsistente por construccion. Un falso positivo acá deja sin bake a todos los usuarios: cualquier
    ''' aserto que se agregue tiene que cumplir la misma vara.</para></summary>
    <MethodImpl(MethodImplOptions.NoInlining)>
    Private Sub Sonda()
        ' 1. ConvertSubresourcesToDds (el camino de un solo buffer que estrenó 1.5.3), via el helper.
        Dim px As Byte() = {255, 255, 255, 255}
        Dim dds = DirectXTextureConversionHelper.Bgra32BytesToDdsBytes(
            1, 1, px, DirectXTextureConversionHelper.DxgiFormatB8G8R8A8Unorm, generateMipMaps:=False)
        If dds Is Nothing OrElse dds.Length = 0 Then
            Throw New InvalidOperationException("ConvertSubresourcesToDds devolvio un DDS vacio para un BGRA 1x1.")
        End If

        ' 2. GetDdsMetadata + el campo ArraySize (el que distingue un Texture2DArray de una 2D suelta).
        Dim md = DirectXTexWrapperCLI.Loader.GetDdsMetadata(dds)
        If md Is Nothing OrElse Not md.Loaded Then
            Throw New InvalidOperationException("GetDdsMetadata no pudo leer un DDS 1x1 recien generado por el mismo wrapper.")
        End If
        If md.ArraySize <> 1 Then
            Throw New InvalidOperationException($"DdsMetadata.ArraySize={md.ArraySize} para una Texture2D suelta (esperado 1).")
        End If

        ' 3. La sobrecarga LoadTextures(..., onlyMipLevel), que no existia antes de 1.5.3.
        Dim cargadas = DirectXTexWrapperCLI.Loader.LoadTextures(New Byte()() {dds}, False, False, 0)
        If cargadas Is Nothing OrElse cargadas.Count <> 1 OrElse cargadas(0) Is Nothing OrElse Not cargadas(0).Loaded Then
            Throw New InvalidOperationException("LoadTextures(onlyMipLevel:=0) no cargo un DDS 1x1 B8G8R8A8.")
        End If

        ' 4. EncodeDDSHeader: es lo que usa el lector de BA2 para CADA textura DX10 (Bsa_Ba2Reader,
        '    Dx10Importer). No comparte camino con las tres de arriba y es el de mayor volumen en produccion.
        Dim header = DirectXTexWrapperCLI.Loader.EncodeDDSHeader(
            DirectXTextureConversionHelper.DxgiFormatB8G8R8A8Unorm, 1, 1, 1, 1, False)
        If header Is Nothing OrElse header.Length = 0 Then
            Throw New InvalidOperationException("EncodeDDSHeader devolvio un header vacio.")
        End If
    End Sub

    ''' <summary>NO TOCA UN SOLO TIPO DEL WRAPPER. Se llama desde el <c>Catch</c> del gate, o sea despues de
    ''' que la resolucion de esos tipos YA fallo: mirar <c>GetType(Loader).Assembly.Location</c> para "informar
    ''' mejor" volveria a dispararla ahi, donde no hay red, y el diagnostico terminaria siendo la caida.
    ''' Todo sale de rutas de archivo.
    ''' <para><c>Ijwhost.dll</c> va en la lista porque sin el un ensamblado mixto C++/CLI no carga: un reporte
    ''' que solo nombre al wrapper manda a mirar el archivo equivocado.</para></summary>
    Private Function Diagnostico(ex As Exception) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("El componente nativo de texturas (DirectXTexWrapper) no coincide con esta version de la aplicacion.")
        sb.AppendLine()
        sb.AppendLine($"  detalle : {ex.GetType().Name}: {ex.Message}")
        Try
            sb.AppendLine($"  proceso : {If(Environment.Is64BitProcess, "x64", "x86")}")
        Catch
        End Try
        For Each nombre In New String() {"DirectXTexWrapper.dll", "Ijwhost.dll"}
            Try
                Dim ruta = Path.Combine(AppContext.BaseDirectory, nombre)
                Dim fi As New FileInfo(ruta)
                If fi.Exists Then
                    sb.AppendLine($"  {nombre,-24} {fi.Length,10} bytes  {fi.LastWriteTime:yyyy-MM-dd HH:mm}")
                Else
                    sb.AppendLine($"  {nombre,-24} NO ESTA en {AppContext.BaseDirectory}")
                End If
            Catch exFi As Exception
                sb.AppendLine($"  {nombre,-24} (no se pudo leer - {exFi.GetType().Name})")
            End Try
        Next
        sb.AppendLine()
        sb.AppendLine("El wrapper y la aplicacion son UNA UNIDAD: viajan juntos y tienen que ser de la misma")
        sb.AppendLine("compilacion y de la misma plataforma (x86/x64). Reinstala la aplicacion completa, y si")
        sb.AppendLine("un antivirus puso el archivo en cuarentena, restauralo.")
        Return sb.ToString()
    End Function

End Module
