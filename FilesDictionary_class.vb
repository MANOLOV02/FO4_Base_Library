' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading
Imports System.Timers
Imports NiflySharp.Enums

Public Module Extensions
    Public Const MaterialsPrefix As String = "Materials\"
    Public Const TexturesPrefix As String = "Textures\"
    Public Const MeshesPrefix As String = "Meshes\"

    <Extension>
    Public Function Correct_Path_Separator(St As String) As String
        If IsNothing(St) Then Return ""
        Return St.Replace("/", "\")
    End Function

    ''' <summary>Removes prefix (case-insensitive) from the start of the string if present.</summary>
    <Extension>
    Public Function StripPrefix(St As String, prefix As String) As String
        If Not IsNothing(St) AndAlso St.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Return St.Substring(prefix.Length)
        End If
        Return St
    End Function
End Module

Public Class FilesDictionary_class
    Public Shared Property TexturesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = TexturesPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}}
    Public Shared Property MaterialsDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm", ".bgem"}}
    Public Shared Property MaterialsDictionary_BGEM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgem"}}
    Public Shared Property MaterialsDictionary_BGSM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm"}}
    Public Shared Property MeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Meshes\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Shared Property ALLMeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Class DictionaryFilePickerConfig
        ' Debe apuntar a tu ConcurrentDictionary(Of String, File_Location)
        Public Property DictionaryProvider As Func(Of ConcurrentDictionary(Of String, FilesDictionary_class.File_Location))

        ' Prefijo raíz (case-insensitive). Default: "Textures\"
        Public Property RootPrefix As String = TexturesPrefix

        ' Extensiones permitidas (case-insensitive). Default: ".dds"
        Private _allowedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}

        Public Property AllowedExtensions As HashSet(Of String)
            Get
                Return _allowedExtensions
            End Get
            Set(value As HashSet(Of String))
                _allowedExtensions = value
            End Set
        End Property

        Public Sub SetAllowedExtensions(exts As IEnumerable(Of String))
            ArgumentNullException.ThrowIfNull(exts)
            _allowedExtensions = New HashSet(Of String)(exts, StringComparer.OrdinalIgnoreCase)
        End Sub

        Public Function ExtensionAllowed(normalized As String) As Boolean
            Dim fileName = normalized
            Dim iSlash = normalized.LastIndexOf("\"c)
            If iSlash >= 0 AndAlso iSlash < normalized.Length - 1 Then
                fileName = normalized.Substring(iSlash + 1)
            End If
            Dim iDot = fileName.LastIndexOf("."c)
            If iDot < 0 Then Return False
            Dim ext = fileName.Substring(iDot)
            Return AllowedExtensions.Contains(ext)
        End Function
        Public Shared Function PathStartsWithRoot(normalized As String, rootPrefix As String) As Boolean
            If String.IsNullOrEmpty(rootPrefix) Then Return True
            Return normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
        End Function
    End Class

    Private Class DictionaryScanWorkItem
        Public Property IsArchive As Boolean
        Public Property FilePath As String = ""
        Public Property SourceOrder As Integer = Integer.MinValue
        ' Loose-file mtime captured at enumeration time (from WIN32_FIND_DATA) so the
        ' worker doesn't need to issue a second syscall per file.
        Public Property LooseLastWrite As Date = Date.MinValue
        ' Loose-file path RELATIVE to the data root, computed by the walk (which already knows the
        ' root) instead of by Path.GetRelativePath in the worker — that call re-normalizes BOTH
        ' paths on every invocation, and it ran once per loose file.
        Public Property RelativePath As String = ""
    End Class
    Public Class File_Location

        Public Property BA2File As String = ""
        Public Property Index As Integer = -1
        Public Property FullPath As String = ""
        Public Property SourceOrder As Integer = Integer.MinValue
        Public Property FileDate As Date = Date.MinValue

        ''' <summary>SELLO de la generacion de CONTENIDO del archive del que salio este `Index`.
        ''' <para>⛔ El `Index` es un numero de entrada dentro de UN archive concreto. Cuando el packager
        ''' reescribe ese .ba2, los indices cambian — pero un `File_Location` que otro hilo capturo ANTES
        ''' sigue vivo con el indice viejo, y `ExtractToMemory(IndexViejo)` sobre el archive NUEVO devuelve
        ''' LOS BYTES DE OTRO ARCHIVO. Sin excepcion, sin log, y encima se quedan pegados en el cache por
        ''' path. Vaciar el pool de readers no toca esto: el problema no es el reader, es el indice.</para>
        ''' <para>Se compara contra <see cref="FilesDictionary_class.ContentGenOf"/> antes de extraer. Los
        ''' sueltos (`BA2File = ""`) no lo usan.</para></summary>
        Public Property ArchiveGen As Integer = 0

        Public Function GetBytesFromOpenArchive(pack As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader) As Byte()
            If IsNothing(pack) OrElse IsLosseFile Then Return Array.Empty(Of Byte)
            Try
                Return pack.ExtractToMemory(Index)
            Catch
                Return Array.Empty(Of Byte)
            End Try
        End Function

        Public ReadOnly Property IsLosseFile As Boolean
            Get
                Return BA2File = ""
            End Get
        End Property
        Public Function GetBytes() As Byte()
            ' O1.1: Check WeakReference byte cache first
            Dim cached As Byte() = Nothing
            Dim weakRef As WeakReference(Of Byte()) = Nothing
            If FilesDictionary_class._bytesCache.TryGetValue(FullPath, weakRef) Then
                If weakRef.TryGetTarget(cached) Then Return cached
            End If

            Dim result As Byte()

            If IsLosseFile Then
                If IO.File.Exists(IO.Path.Combine(FO4Path, Me.FullPath)) = False Then Return Array.Empty(Of Byte)
                result = IO.File.ReadAllBytes(IO.Path.Combine(FO4Path, Me.FullPath))
            Else
                ' O1.2: Use archive reader pool instead of opening/closing each time
                Dim archivePath = IO.Path.Combine(FO4Path, Me.BA2File)

                ' ⛔ EL SELLO SE MIRA ANTES DE ABRIR NADA. Si el archive se invalido (pack, unpack, re-scan)
                ' despues de que este File_Location se creo, su `Index` apunta a otra entrada del archive
                ' NUEVO: extraer devolveria bytes de un archivo distinto, sin error. Vacio es la respuesta
                ' correcta — el llamador ya sabe tratarlo, y el diccionario ya tiene la entrada re-estampada
                ' para quien la busque de nuevo. Ver File_Location.ArchiveGen.
                If FilesDictionary_class.ContentGenOf(archivePath) <> Me.ArchiveGen Then Return Array.Empty(Of Byte)
                Dim leased As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer) = Nothing
                Dim returned As Boolean = False
                Try
                    leased = FilesDictionary_class.LeaseReader(archivePath)
                    result = leased.Reader.ExtractToMemory(Index)
                    FilesDictionary_class.ReturnReader(archivePath, leased)
                    returned = True
                Catch ex As Exception
                    ' On error, dispose the leased reader rather than returning it
                    If Not returned Then
                        If leased.Reader IsNot Nothing Then
                            Try : leased.Reader.Dispose() : Catch : End Try
                        End If
                        If leased.Stream IsNot Nothing Then
                            Try : leased.Stream.Dispose() : Catch : End Try
                        End If
                    End If
                    Return Array.Empty(Of Byte)
                End Try
            End If

            ' O1.1: Store result in WeakReference cache
            If result IsNot Nothing AndAlso result.Length > 0 Then
                FilesDictionary_class._bytesCache(FullPath) = New WeakReference(Of Byte())(result)
            End If

            Return result
        End Function

    End Class
    Private Shared _fO4Path As String = ""
    Private Shared _cacheDirectory As String = ""
    Private Shared _dictionary As New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Stack of overridden entries per key. When a loose overrides a BA2 (or a BA2 overrides another), the loser is pushed here.</summary>
    Private Shared ReadOnly _overriddenEntries As New ConcurrentDictionary(Of String, ConcurrentStack(Of File_Location))(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Extensiones que el diccionario indexa de sueltos y archives. Las ultimas cubren archivos de
    ''' configuracion de RaceMenu (skee64) y LooksMenu (f4ee): los dos los abren por la capa de archives del
    ''' juego, asi que pueden vivir DENTRO de un BSA/BA2 y a menudo lo hacen - .ini (morphs extendidos, BodyGen),
    ''' .jslot/.slot (presets) y .pex/.psc (de donde salen las listas de paints, ver 60-racemenu-listas-de-paint).
    ''' Sin .ini el catalogo de sliders extendidos cargaba vacio y todo slider extra resolvia a "sin morph".</summary>
    Private Shared ReadOnly SupportedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds", ".bgsm", ".bgem", ".nif", ".tri", ".txt", ".json", ".xml", ".ssf", ".sclp", ".hkx", ".hkt", ".ini", ".jslot", ".slot", ".pex", ".psc"}

    ''' <summary>App-specific data store. Apps register their own data here (presets, high heels, etc.) keyed by type.</summary>
    Private Shared ReadOnly _appData As New ConcurrentDictionary(Of Type, Object)

    ' O1.1: Lazy byte cache with WeakReference — allows GC to reclaim when memory is needed
    Private Shared ReadOnly _bytesCache As New ConcurrentDictionary(Of String, WeakReference(Of Byte()))(StringComparer.OrdinalIgnoreCase)

    ' O1.2: Archive reader pool — reuses BethesdaReader instances to avoid repeated open/close
    ' ⛔ El pool guarda ADEMAS el instante en que cada reader volvio. Sin eso `DisposeIdleReaders` no
    ' podia distinguir un reader ocioso de uno que se acababa de usar, y vaciaba el pool ENTERO cada 30 s.
    ' Reconstruir un `BethesdaReader` no es gratis: su constructor hace `ListEntries()`, o sea PARSEA LA
    ' TABLA DE ARCHIVOS COMPLETA del BA2 (un BSA de Skyrim son ~100k nombres leidos byte a byte). En un
    ' bake de una hora eso son >=120 re-parseos forzados de cada archivo tocado, en el medio del trabajo.
    Private Shared ReadOnly _archivePool As New ConcurrentDictionary(Of String, ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer)))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>GENERACION por archive. Un reader ALQUILADO ya salio del bag, asi que
    ''' <see cref="UnregisterArchive"/> —que solo vacia el bag— no lo alcanza: cuando ese reader vuelve,
    ''' <see cref="ReturnReader"/> re-crea el bag con `GetOrAdd` y el pool INVALIDADO resucita, sirviendo
    ''' entradas del archive VIEJO por el resto de la sesion. Y como <see cref="File_Location.GetBytes"/>
    ''' cachea lo que extrae en <see cref="_bytesCache"/>, esos bytes equivocados se quedan pegados.
    ''' <para>El epoch se captura EN EL LEASE y se compara al devolver, al re-alquilar y en la limpieza: un
    ''' reader de otra generacion se DESTRUYE, nunca se poolea ni se sirve.</para>
    ''' <para>⛔⛔ LO QUE ESTO **NO** ARREGLA, y el comentario de los dos packagers afirma que si: que la
    ''' reescritura del .ba2 quede libre de carreras. Un reader ALQUILADO tiene su `FileStream` abierto
    ''' (`File.OpenRead` ⇒ FileShare.Read) mientras dura el `ExtractToMemory`, y `UnregisterArchive` vuelve
    ''' con ese handle vivo ⇒ el `File.Move`/`Delete` del packager puede seguir fallando. Y un lector que
    ''' entro a `LeaseReader` justo antes del bump abre el archivo que el packager esta por reescribir.
    ''' Cerrar ESO pide exclusion lease↔unregister (un lock por archive), no una generacion. Queda ABIERTO
    ''' y a proposito: es un lock en el camino mas caliente de la app y no se mete sin medirlo.</para>
    ''' <para>⛔ NUNCA se borra una entrada de acá, tampoco en Unregister. Si se borrara, un reader viejo que
    ''' vuelve despues compararia contra un contador RECIEN CREADO en 0 y volveria a parecer valido — que es
    ''' exactamente el agujero que esto cierra. Es un Integer por archive.</para></summary>
    Private Shared ReadOnly _archiveEpoch As New ConcurrentDictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Memo de <see cref="ArchiveKey"/>. Acotado por la cantidad de deletreos distintos de ruta de
    ''' archive (decenas), y saca un P/Invoke a GetFullPathName de CADA lectura de BA2.</summary>
    Private Shared ReadOnly _archiveKeyCache As New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

    ''' <summary>Clave canonica del pool y del epoch. Cada entrada arma la ruta por su cuenta
    ''' (`Path.Combine(FO4Path, BA2File)` en el lease; el path que le pasen a Unregister/Register), asi que
    ''' sin normalizar, dos deletreos de la MISMA ruta (`Data\x.ba2` vs `Data\.\x.ba2`) son dos claves y la
    ''' invalidacion no alcanza a los readers que hay que matar.
    ''' <para>⛔ SOLO se normaliza lo que ya esta COMPLETAMENTE CALIFICADO. `Path.GetFullPath` de una ruta
    ''' relativa la resuelve contra `Environment.CurrentDirectory`, que es GLOBAL y lo cambia cualquier
    ''' OpenFileDialog de WinForms (RestoreDirectory viene en False): la clave del lease y la del return
    ''' saldrian distintas si el usuario abre un dialogo en el medio de un bake, y el reader rancio se
    ''' pool-earia bajo una clave que ningun Unregister va a drenar nunca. `FO4Path` ademas vale "" hasta que
    ''' corre el scan, con lo cual el Combine da genuinamente relativo. Sin calificar ⇒ se usa verbatim, que
    ''' es lo que hacia antes.</para></summary>
    Private Shared Function ArchiveKey(archivePath As String) As String
        If String.IsNullOrEmpty(archivePath) Then Return ""
        Dim cached As String = Nothing
        If _archiveKeyCache.TryGetValue(archivePath, cached) Then Return cached
        Dim key As String = archivePath
        Try
            If Path.IsPathFullyQualified(archivePath) Then key = Path.GetFullPath(archivePath)
        Catch
            key = archivePath
        End Try
        _archiveKeyCache(archivePath) = key
        Return key
    End Function

    ''' <summary>⛔⛔ ABRE CON `FileShare.Delete`, NO CON `File.OpenRead`. Es LA razon por la que
    ''' <see cref="UnregisterArchive"/> no entregaba lo que sus dos llamadores afirman por escrito
    ''' ("makes the rewrite path race-free"): `File.OpenRead` mapea a `CreateFile` con
    ''' `dwShareMode = FILE_SHARE_READ`, y `File.Move` abre el origen pidiendo acceso DELETE. El kernel
    ''' compara ese acceso contra el share mode de CADA handle abierto ⇒ falta `FILE_SHARE_DELETE` ⇒
    ''' `STATUS_SHARING_VIOLATION`. Un solo reader alquilado en otro hilo —su `FileStream` vive todo el
    ''' `ExtractToMemory`— hacia fallar el `File.Move` del packager. No es una carrera de tiempos: es un
    ''' invariante del kernel.
    ''' <para>⛔ EL ARBITRO LECTOR/ESCRITOR DE UN ARCHIVO ES EL SO, NO UN LOCK NUESTRO. Se evaluo un
    ''' `ReaderWriterLockSlim` por archive y es la capa equivocada: no cubre al OTRO proceso (MO2/USVFS,
    ''' xEdit, el antivirus), no cubre ningun `FileStream` futuro que alguien se olvide de envolver, y para
    ''' servir de algo el write lock tendria que abarcar el `Pack` ENTERO — con lo cual RWLS bloquea a los
    ''' lectores nuevos (para no matar de hambre al writer) y el hilo de UI queda clavado los minutos que
    ''' dura el pack.</para>
    ''' <para>`Read Or Delete`, NO `ReadWrite`: `File.Move` necesita DELETE, no WRITE. Agregar
    ''' `FILE_SHARE_WRITE` solo abriria la puerta a que un escritor externo nos rompa una lectura a mitad,
    ''' sin comprar nada.</para></summary>
    Private Shared Function AbrirArchiveParaLectura(archivePath As String) As FileStream
        Return New FileStream(archivePath, FileMode.Open, FileAccess.Read,
                              FileShare.Read Or FileShare.Delete, 4096, FileOptions.None)
    End Function

    Private Shared Sub BumpArchiveEpoch(key As String)
        If String.IsNullOrEmpty(key) Then Return
        _archiveEpoch.AddOrUpdate(key, 1, Function(k, v) v + 1)
    End Sub

    ''' <summary>GENERACION DE CONTENIDO del archive, que es OTRA COSA que <see cref="_archiveEpoch"/> y por
    ''' eso es un contador aparte. El epoch gobierna los READERS pooleados y por eso lo bumpea tambien
    ''' `RegisterArchive` (montar puede cambiar lo que hay en esa ruta). Este gobierna la validez de los
    ''' INDICES ya repartidos en `File_Location`, y `RegisterArchive` es justamente quien los RE-ESTAMPA.
    ''' <para>⛔⛔ SI FUERAN EL MISMO CONTADOR, ESTO SE ROMPE EN SILENCIO: `RegisterArchive` bumpea ANTES de
    ''' su guard de idempotencia (a proposito, porque `_registeredArchives` va por NOMBRE de archivo), asi
    ''' que un segundo `RegisterArchive` del mismo archive bumpearia y saldria por `Exit Sub` SIN volver a
    ''' estampar — y a partir de ahi TODA lectura de ese archive devolveria vacio por el resto de la sesion.
    ''' </para>
    ''' <para>Por eso este SOLO se bumpea donde el contenido deja de valer y las entradas se van a rehacer:
    ''' <see cref="UnregisterArchive"/> y el re-scan de <see cref="Fill_DictionaryAsync"/>.</para></summary>
    Private Shared ReadOnly _archiveContentGen As New ConcurrentDictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Generacion de contenido vigente para esa ruta de archive (0 si nunca se invalido).</summary>
    Friend Shared Function ContentGenOf(archivePath As String) As Integer
        Dim g As Integer = 0
        _archiveContentGen.TryGetValue(ArchiveKey(archivePath), g)
        Return g
    End Function

    Private Shared Sub BumpArchiveContentGen(key As String)
        If String.IsNullOrEmpty(key) Then Return
        _archiveContentGen.AddOrUpdate(key, 1, Function(k, v) v + 1)
    End Sub

    Private Shared Sub DisposePooled(entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer))
        If entry.Reader IsNot Nothing Then
            Try : entry.Reader.Dispose() : Catch : End Try
        End If
        If entry.Stream IsNot Nothing Then
            Try : entry.Stream.Dispose() : Catch : End Try
        End If
    End Sub

    ''' <summary>Saca el bag del pool y destruye todo lo que tenga. Cuerpo compartido por la invalidacion y
    ''' por <see cref="UnregisterArchive"/>.</summary>
    Private Shared Sub DrainAndDisposeBag(key As String)
        Dim bag As ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer)) = Nothing
        If Not _archivePool.TryRemove(key, bag) OrElse bag Is Nothing Then Return
        Dim entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer) = Nothing
        While bag.TryTake(entry)
            DisposePooled(entry)
        End While
    End Sub

    ''' <summary>Periodo del timer de limpieza, en ms. Es TAMBIEN el umbral de ocio: un reader devuelto
    ''' hace menos que esto se considera en uso y no se toca. No es una constante elegida a ojo — es el
    ''' mismo numero que ya gobierna la limpieza.</summary>
    Private Const PoolCleanupPeriodMs As Integer = 30000
    Private Shared ReadOnly MaxPooledReadersPerArchive As Integer = 2
    Private Shared _poolCleanupTimer As System.Timers.Timer

    ' Track archives mounted at runtime via RegisterArchive (vs. those discovered by Fill_DictionaryAsync).
    ' Key: archive file name (matches File_Location.BA2File). Value: unused (used as a set).
    Private Shared ReadOnly _registeredArchives As New ConcurrentDictionary(Of String, Byte)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>
    ''' SourceOrder for archives mounted via RegisterArchive after the initial scan.
    ''' Higher than any value assigned by BuildArchivePriority but lower than Integer.MaxValue
    ''' (which is reserved for loose files), so runtime-registered archives win over scan-time
    ''' archives but loose still overrides everything.
    ''' </summary>
    Public Const ArchiveSourceOrder_RuntimeRegistered As Integer = Integer.MaxValue - 1

    ''' <summary>⛔ EL CHEQUEO SOLO NO ALCANZA: dos primeros lectores concurrentes lo pasaban los DOS y
    ''' creaban dos timers, uno de los cuales quedaba corriendo sin referencia (limpiezas dobles + un timer
    ''' que nadie puede parar). El CAS publica uno solo y el perdedor se destruye SIN haber arrancado — por
    ''' eso el `Start` va despues de ganar y no en la construccion.
    ''' <para>⛔ La sobrecarga generica va EXPLICITA. Con `Option Strict Off` (que es como compila esto),
    ''' `Interlocked.CompareExchange` tambien ofrece la version de `Object`, y esa toma el destino `ByRef
    ''' Object`: VB tendria que materializar un temporal y copiar de vuelta, o sea que el "CAS" correria
    ''' sobre el temporal y volveria a ser una asignacion con carrera.</para>
    ''' <para>⛔ El handler va envuelto en Try. En .NET moderno una excepcion no atrapada en `Elapsed` puede
    ''' matar el proceso, y esto vive en un binario que se distribuye: una limpieza de cache no puede ser un
    ''' modo de caida. Ver 00-reglas-app-distribuida.</para></summary>
    Private Shared Sub InitPoolCleanupTimer()
        If _poolCleanupTimer IsNot Nothing Then Return
        Dim nuevo As New System.Timers.Timer(PoolCleanupPeriodMs)
        AddHandler nuevo.Elapsed, Sub(sender, e)
                                      Try
                                          DisposeIdleReaders()
                                      Catch
                                      End Try
                                  End Sub
        nuevo.AutoReset = True
        If Threading.Interlocked.CompareExchange(Of System.Timers.Timer)(_poolCleanupTimer, nuevo, Nothing) IsNot Nothing Then
            Try : nuevo.Dispose() : Catch : End Try
            Return
        End If
        nuevo.Start()
    End Sub

    ''' <summary>Lease a BethesdaReader from the pool, or create a new one if pool is empty.
    ''' <para>⛔ El epoch de una entrada del bag se compara contra el ACTUAL leido en ese instante, no contra
    ''' uno capturado al entrar: capturarlo antes del `TryTake` hace que un lease preemptado tire entradas de
    ''' una generacion MAS NUEVA que la suya (cada una cuesta un `ListEntries()` completo).</para>
    ''' <para>⛔ `Friend`, no `Private`: es la costura que necesita el gate de `Tools/PreflightScanProbe` para
    ''' poner un lease en vuelo de forma DETERMINISTA (lease → Unregister → Return en un solo hilo) en vez de
    ''' correr una carrera con sleeps que pasa en verde por casualidad. No cambia la superficie distribuida.
    ''' Ver 00-reglas-self-tests-no-van-en-el-binario.</para></summary>
    Friend Shared Function LeaseReader(archivePath As String) As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer)
        ' Lazy-init the pool cleanup timer on first use
        InitPoolCleanupTimer()

        Dim key = ArchiveKey(archivePath)
        Dim bag As ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer)) = Nothing
        Dim entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer) = Nothing

        If _archivePool.TryGetValue(key, bag) Then
            While bag.TryTake(entry)
                ' Una entrada del bag puede ser de otra generacion: la devolvio un hilo que tenia el lease
                ' tomado cuando se invalido el archive. No se sirve — se destruye y se sigue drenando.
                If entry.Epoch = _archiveEpoch.GetOrAdd(key, 0) Then Return entry
                DisposePooled(entry)
            End While
        End If

        ' Create new reader
        Dim fs As FileStream = AbrirArchiveParaLectura(archivePath)
        Dim reader As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
        Return (reader, fs, Stopwatch.GetTimestamp(), _archiveEpoch.GetOrAdd(key, 0))
    End Function

    ''' <summary>Return a reader to the pool if below cap, otherwise dispose it.
    ''' <para>⛔ EL CHEQUEO DE EPOCH VA ANTES DEL `GetOrAdd`. Al reves, el propio `GetOrAdd` re-crea el bag que
    ''' la invalidacion acababa de sacar del pool: el pool muerto resucita aunque despues tiremos el reader.
    ''' Un epoch AUSENTE cuenta como distinto (se destruye): que falte solo puede significar que la clave se
    ''' desalineo, y ahi tratar el hueco como "0" seria pool-ear a ciegas.</para>
    ''' <para>Se probó ademas RE-CHEQUEAR despues del `Add` y purgar el bag si el epoch cambio en el medio, y
    ''' esta MAL: el purgado se lleva puestos readers VIGENTES que otro hilo acababa de devolver, y cada uno
    ''' cuesta re-parsear la tabla de entradas entera. No hace falta: una entrada rancia que se cuele nunca se
    ''' SIRVE —la filtran `LeaseReader` y `DisposeIdleReaders`— y se cosecha en el proximo lease o tick.</para>
    ''' <para><see cref="LeaseReader"/> explica por que es `Friend`.</para></summary>
    Friend Shared Sub ReturnReader(archivePath As String, entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer))
        Dim key = ArchiveKey(archivePath)

        Dim epochActual As Integer = 0
        If Not _archiveEpoch.TryGetValue(key, epochActual) OrElse entry.Epoch <> epochActual Then
            DisposePooled(entry)
            Return
        End If

        Dim bag = _archivePool.GetOrAdd(key, Function(k) New ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer))())

        ' El cap es BEST-EFFORT a proposito: `Count` y `Add` no son atomicos, asi que dos devoluciones
        ' simultaneas pueden dejar 3. Hacerlo estricto pide un lock en el camino caliente de CADA lectura
        ' para ahorrar un FileStream que `DisposeIdleReaders` cosecha igual.
        If bag.Count < MaxPooledReadersPerArchive Then
            entry.DevueltoEn = Stopwatch.GetTimestamp()
            bag.Add(entry)
        Else
            ' Over capacity — dispose
            DisposePooled(entry)
        End If
    End Sub

    ''' <summary>Suelta los readers OCIOSOS del pool (los devueltos hace mas de un periodo del timer) y
    ''' purga el cache de bytes muerto. La llama el timer de limpieza.
    ''' <para>⛔ Antes vaciaba el pool ENTERO cada vez, sin mirar si algo estaba en uso — el nombre decia
    ''' "Idle" y el codigo no lo miraba. Como el constructor de `BethesdaReader` parsea la tabla de
    ''' archivos completa, durante un bake eso forzaba a re-indexar cada BA2 tocado cada 30 s.</para></summary>
    Private Shared Sub DisposeIdleReaders()
        Dim umbral As Long = CLng(Stopwatch.Frequency) * PoolCleanupPeriodMs \ 1000L
        Dim ahora As Long = Stopwatch.GetTimestamp()
        For Each kvp In _archivePool
            Dim bag = kvp.Value
            Dim epochActual As Integer = 0
            Dim hayEpoch = _archiveEpoch.TryGetValue(kvp.Key, epochActual)
            Dim entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer) = Nothing
            ' ⛔⛔ SE DRENA LA BAG ENTERA Y RECIEN DESPUES SE DEVUELVEN LOS VIVOS.
            ' Probe a devolverlos EN EL ACTO para achicar la ventana en que la bag queda vacia, y esta MAL:
            ' `ConcurrentBag` mantiene una lista POR HILO y `TryTake` saca de la del hilo actual antes de
            ' robarle a otro. El timer corre en un hilo del pool con lista local vacia, asi que al re-agregar
            ' el reader fresco quedaba en SU propia lista y el `TryTake` siguiente devolvia EL MISMO: las
            ' demas entradas no se examinaban nunca y ningun reader ocioso se liberaba mientras hubiera uno
            ' fresco encima. Peor que el problema que queria evitar — retiene el FileStream y la tabla de
            ' entradas (~100k nombres en un BSA) por archive, para siempre.
            ' La ventana con la bag vacia es el precio y es corto; que un LeaseReader concurrente construya
            ' un reader de mas es barato al lado de no liberar nunca.
            Dim aConservar As New List(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer))()
            While bag.TryTake(entry)
                If Not hayEpoch OrElse entry.Epoch <> epochActual Then
                    ' De una generacion anterior (el archive se desmonto o se reemplazo mientras este reader
                    ' estaba alquilado). Tiene la tabla de entradas VIEJA: no vuelve al pool ni por ocioso.
                    DisposePooled(entry)
                ElseIf (ahora - entry.DevueltoEn) < umbral Then
                    aConservar.Add(entry)
                Else
                    DisposePooled(entry)
                End If
            End While
            ' El cap se respeta tambien acá: `ReturnReader` lo chequea antes de agregar, asi que sin esto
            ' los dos juntos podian dejar el pool POR ENCIMA del maximo.
            ' ⛔⛔ SE RE-OBTIENE EL BAG DEL POOL, NO SE USA `kvp.Value`. Entre el drenaje de arriba y este
            ' re-agregado, otro hilo puede haber hecho `TryRemove` de esta clave (`UnregisterArchive`): se
            ' llevaba un bag YA VACIO —no disponia nada, "quedo limpio"— y despues nosotros re-agregabamos los
            ' vivos a un bag DESACOPLADO del pool. Esos FileStream quedaban inalcanzables: nadie los alquila,
            ' el proximo tick no los ve (enumera `_archivePool`), `ArchivePoolReaderCount` no los cuenta, y
            ' siguen bloqueando el .ba2 hasta que corra el finalizer del handle. Con `GetOrAdd` se re-publica.
            For Each vivo In aConservar
                Dim bagVivo = _archivePool.GetOrAdd(kvp.Key, Function(k) New ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer))())
                Dim epochPost As Integer = 0
                If bagVivo.Count < MaxPooledReadersPerArchive AndAlso
                   _archiveEpoch.TryGetValue(kvp.Key, epochPost) AndAlso epochPost = vivo.Epoch Then
                    bagVivo.Add(vivo)
                Else
                    DisposePooled(vivo)
                End If
        Next
        Next

        ' Purge dead WeakReference entries from _bytesCache
        For Each key In _bytesCache.Keys
            Dim weakRef As WeakReference(Of Byte()) = Nothing
            If _bytesCache.TryGetValue(key, weakRef) Then
                Dim dummy As Byte() = Nothing
                If Not weakRef.TryGetTarget(dummy) Then
                    _bytesCache.TryRemove(key, weakRef)
                End If
            End If
        Next
    End Sub

    ''' <summary>Clear the byte cache (call when dictionary is rebuilt).</summary>
    Public Shared Sub ClearBytesCache()
        _bytesCache.Clear()
    End Sub

    ''' <summary>Count of entries in _bytesCache (for memory diagnostics).</summary>
    Public Shared Function BytesCacheCount() As Integer
        Return _bytesCache.Count
    End Function

    ''' <summary>Count of total pooled archive readers (for memory diagnostics).</summary>
    Public Shared Function ArchivePoolReaderCount() As Integer
        Dim total = 0
        For Each kvp In _archivePool
            total += kvp.Value.Count
        Next
        Return total
    End Function

    ''' <summary>Dispose all pooled archive readers and clear the bytes cache.
    ''' Call periodically during bulk load to keep memory from ballooning.</summary>
    Public Shared Sub PurgeCachesAndReaders()
        DisposeIdleReaders()
        _bytesCache.Clear()
    End Sub

    ' ⛔ NO volver a agregar un índice por extensión sola: existió, se escribía en cada alta, y NINGUNA query
    ' lo leía — una copia completa de todas las claves (millones en un install modeado) sostenida al pedo.
    ''' <summary>⛔ LAZY — se construye en el PRIMER USO, no durante el scan. Su único lector es la rama
    ''' "sin extensiones" de <see cref="GetFilesInDirectory"/>, que hoy ningún caller usa; poblarlo durante el
    ''' scan era una segunda copia completa de todas las claves para una query que nadie hace. Sobrevive
    ''' porque esa query es parte del contrato público y tiene que FUNCIONAR — sólo que no cuesta nada hasta
    ''' que alguien la pida.
    ''' <para>Una vez construido, <see cref="IndexDictionaryKey"/> lo mantiene y
    ''' <see cref="ClearSearchIndexes"/> lo devuelve al estado sin construir.</para></summary>
    Private Shared ReadOnly _KeysByDirectory As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared _keysByDirectoryBuilt As Boolean = False
    Private Shared ReadOnly _keysByDirectoryLock As New Object

    ''' <summary>LAZY: se construye en el PRIMER USO, no durante el scan. Mismo patron y candado que
    ''' <see cref="_KeysByDirectory"/>. Todos sus lectores son on-demand de UI o tools (pickers, catalogos,
    ''' import de poses), ninguno esta en el arranque; construirlo en el scan era una pasada COMPLETA sobre
    ''' millones de claves pagada en CADA arranque. El costo se MUDO, no desaparecio: lo paga quien abre un
    ''' picker, una vez por scan. Si no esta construido, altas y bajas son no-ops correctas: la construccion
    ''' posterior lee el diccionario ya con el cambio aplicado.</summary>
    Private Shared ReadOnly _KeysByDirectoryExtension As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared _keysByDirectoryExtensionBuilt As Boolean = False

    ''' <summary>Pool para de-duplicar las strings que se guardan A LARGO PLAZO (claves y FullPath).
    ''' <para>No se usa String.Intern: toma un lock sobre la tabla GLOBAL del runtime con millones de paths
    ''' desde varios workers, y lo interned no se libera nunca, asi que cada recarga de load order filtraba
    ''' los paths del scan anterior. Este pool se limpia al empezar cada scan.</para>
    ''' <para>Ordinal, NO OrdinalIgnoreCase: colapsaria Textures\A.dds con textures\a.dds y reescribiria el
    ''' casing en silencio, y estas strings se muestran verbatim en los pickers y se escriben en records.</para>
    ''' <para>Solo en los sitios de INSERCION, nunca en el lookup: poolear strings arbitrarias del caller
    ''' recrearia la fuga de arriba.</para></summary>
    Private Shared _pathPool As New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

    Private Shared Function PoolPath(s As String) As String
        If String.IsNullOrEmpty(s) Then Return s
        Return _pathPool.GetOrAdd(s, s)
    End Function

    Public Shared Function GetBytes(File As String) As Byte()
        Dim located_File As File_Location = Nothing
        If Not Dictionary.TryGetValue(NormalizeDictionaryKey(File), located_File) Then
            Return Array.Empty(Of Byte)
        Else
            Return located_File.GetBytes
        End If
    End Function


    ''' <summary>Lee los bytes ORIGINALES del archive (BA2/BSA) para un path, IGNORANDO cualquier suelto que
    ''' lo sombree y también la caché por path — que está indexada por ruta y sería la MISMA para el suelto
    ''' ganador y para la entrada del archive, así que devolvería el suelto por colisión.
    ''' <para>Es la función que hay que usar para comparar contra la referencia vanilla: ver
    ''' 10-stack-arnes-de-medicion.md, donde usar el resolver normal es la trampa #1.</para>
    ''' <para>⛔ Entre todos los candidatos archivados elige el de SourceOrder MÁS BAJO, que es el archive
    ''' vanilla. "El primero no-suelto" está MAL: la pila de overrides se llena en paralelo, así que cuando un
    ''' mod shippea su override dentro de un .ba2 el primero puede ser el del MOD.</para>
    ''' <para>Devuelve Nothing si no hay ningún candidato archivado; ahí el caller cae al resolver normal,
    ''' cuyo ganador ya es vanilla.</para></summary>
    Public Shared Function GetArchiveOriginalBytes(path As String) As Byte()
        Dim key = NormalizeDictionaryKey(path)
        If String.IsNullOrEmpty(key) Then Return Nothing

        ' Pick the vanilla archived entry = the archived candidate with the minimum SourceOrder.
        ' Candidates: the dictionary winner (only if it's archived) plus every archived loser shadowed
        ' in the override stack. Loose entries (SourceOrder = Integer.MaxValue) are excluded.
        Dim entry As File_Location = Nothing
        Dim winner As File_Location = Nothing
        If _dictionary.TryGetValue(key, winner) AndAlso winner IsNot Nothing AndAlso Not winner.IsLosseFile Then
            entry = winner
        End If
        For Each loser In GetOverriddenEntries(key)
            If loser IsNot Nothing AndAlso Not loser.IsLosseFile Then
                If entry Is Nothing OrElse loser.SourceOrder < entry.SourceOrder Then
                    entry = loser
                End If
            End If
        Next

        If entry Is Nothing Then Return Nothing

        ' Read directly from the archive, bypassing _bytesCache. Reuse the reader pool.
        Dim archivePath = IO.Path.Combine(FO4Path, entry.BA2File)
        Dim leased As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream, DevueltoEn As Long, Epoch As Integer) = Nothing
        Dim returned As Boolean = False
        Try
            leased = LeaseReader(archivePath)
            Dim result = entry.GetBytesFromOpenArchive(leased.Reader)
            ReturnReader(archivePath, leased)
            returned = True
            Return result
        Catch
            If Not returned Then
                If leased.Reader IsNot Nothing Then
                    Try : leased.Reader.Dispose() : Catch : End Try
                End If
                If leased.Stream IsNot Nothing Then
                    Try : leased.Stream.Dispose() : Catch : End Try
                End If
            End If
            Return Nothing
        End Try
    End Function
    ''' <summary>⛔ TOP-LEVEL only (RecurseSubdirectories = False) — the recursion is done by
    ''' <see cref="WalkLooseFilesParallel"/>, one directory per work item, so it can be spread across
    ''' threads. The other two settings are load-bearing and must match what the old single recursive
    ''' <c>EnumerateFiles</c> call used, or the walk would return a DIFFERENT set of files:
    '''   • IgnoreInaccessible = True — a directory we can't open is skipped, not thrown on.
    '''   • AttributesToSkip is left at its DEFAULT (Hidden | System), which is what an
    '''     <c>EnumerationOptions</c> constructed with no explicit value gives you. It applies to
    '''     directories as well as files, so hidden/system subtrees stay excluded exactly as before.</summary>
    Private Shared ReadOnly _looseEnumOptionsTopLevel As New EnumerationOptions() With {
        .RecurseSubdirectories = False,
        .IgnoreInaccessible = True
    }

    ''' <summary>Walk recursivo PARALELO de Data\, filtrando por extension en managed code y emitiendo cada
    ''' archivo que matchea a <paramref name="onFile"/>. Devuelve cuantos emitio.
    ''' <para>UNA sola travesia del arbol, no una por extension: bajo MO2 cada FindNextFile pasa por el hook
    ''' de USVFS y es lo que "congela" el arranque, no los archives.</para>
    ''' <para>COLA de directorios, no split por profundidad fija: el arbol es muy desbalanceado (Textures\ y
    ''' Meshes\ concentran casi todo) y un split a profundidad 1 deja un hilo haciendo todo.</para>
    ''' <para>Cierre: <paramref name="pending"/> cuenta directorios encolados-pero-no-terminados. "Cola vacia
    ''' implica listo" seria un bug - perderia los subdirectorios que un directorio en proceso aun puede
    ''' producir - asi que el worker que la ve vacia hace SpinWait, no sale.</para>
    ''' <para>DirectoryInfo/FileInfo y no la sobrecarga de string: FileInfo llega con la metadata de
    ''' WIN32_FIND_DATA, asi que leer LastWriteTime no cuesta otro syscall, y ese mtime alimenta
    ''' <see cref="File_Location.FileDate"/>, que el planner de clonado de WM usa para decidir reescrituras.</para>
    ''' <param name="extensions">Snapshot de las extensiones soportadas, tomado por el caller antes del walk
    ''' para que un RegisterExtensions concurrente no mute el set a mitad de la enumeracion.</param>
    ''' <param name="onFile">Se llama una vez por archivo que matchea, DESDE VARIOS HILOS: debe ser thread-safe.
    ''' Recibe el path completo, el relativo a <paramref name="root"/> y el mtime.</param>
    ''' </summary>
    Private Shared Function WalkLooseFilesParallel(root As String,
                                                   extensions As HashSet(Of String),
                                                   dop As Integer,
                                                   onFile As Action(Of String, String, Date)) As Integer
        ' Relative paths are cut with a Substring against this length instead of Path.GetRelativePath
        ' (which re-normalizes both operands on every call). Trimming any trailing separator off the root
        ' first makes the cut correct whether the caller passed "…\Data" or "…\Data\".
        Dim rootTrimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Dim cutAt As Integer = rootTrimmed.Length + 1

        Dim dirs As New ConcurrentQueue(Of String)
        dirs.Enqueue(rootTrimmed)

        ' Directories enqueued but not yet finished. Starts at 1 for the root.
        Dim pending As Integer = 1
        Dim emitted As Integer = 0

        Dim workers = Enumerable.Range(0, Math.Max(1, dop)).
            Select(Function(unused)
                       Return Task.Run(
                           Sub()
                               Dim dir As String = Nothing

                               ' ⛔ Declared OUTSIDE the loop on purpose. SpinWait escalates: the first few
                               ' SpinOnce calls busy-spin, then it starts yielding the thread and finally
                               ' sleeping — but only because it COUNTS its own calls. Constructing a fresh
                               ' one inside the loop resets that counter every iteration, so it would never
                               ' get past the cheapest tight spin and an idle worker would burn a core at
                               ' 100% while another chews a big directory — stealing CPU from the scan
                               ' workers, which now run CONCURRENTLY with this walk. Reset it only when we
                               ' actually get work, so the back-off restarts from cheap each time.
                               Dim spin As New SpinWait()

                               Do
                                   If Not dirs.TryDequeue(dir) Then
                                       ' Nothing to take right now. If no directory is in flight anywhere,
                                       ' the walk is over; otherwise another worker is about to enqueue
                                       ' children, so back off and retry.
                                       If Volatile.Read(pending) = 0 Then Exit Do
                                       spin.SpinOnce()
                                       Continue Do
                                   End If
                                   spin.Reset()

                                   Try
                                       Dim di As New DirectoryInfo(dir)
                                       For Each info In di.EnumerateFileSystemInfos("*", _looseEnumOptionsTopLevel)
                                           Dim sub_ = TryCast(info, DirectoryInfo)
                                           If sub_ IsNot Nothing Then
                                               ' Count it BEFORE publishing it, or a worker could dequeue and
                                               ' finish this child before we incremented, driving pending to 0
                                               ' while the walk is still going and letting everyone exit early.
                                               Interlocked.Increment(pending)
                                               dirs.Enqueue(sub_.FullName)
                                           Else
                                               Dim fi = TryCast(info, FileInfo)
                                               If fi IsNot Nothing AndAlso extensions.Contains(fi.Extension) Then
                                                   Interlocked.Increment(emitted)
                                                   onFile(fi.FullName, fi.FullName.Substring(cutAt), fi.LastWriteTime)
                                               End If
                                           End If
                                       Next
                                   Catch ex As Exception
                                       ' A directory that vanished or that we can't read is skipped, exactly as
                                       ' IgnoreInaccessible did for the old recursive walk. One unreadable folder
                                       ' must not abort the scan of the other 400.000 files.
                                       _scanErrors.Enqueue("Error walking directory " & dir & ": " & ex.Message)
                                   Finally
                                       Interlocked.Decrement(pending)
                                   End Try
                               Loop
                           End Sub)
                   End Function).
            ToArray()

        Task.WaitAll(workers)
        Return Volatile.Read(emitted)
    End Function
    Public Shared Function GetMultipleFilesBytes(files As String()) As Byte()()
        If IsNothing(files) OrElse files.Length = 0 Then Return Array.Empty(Of Byte())()

        Dim output As Byte()() = New Byte(files.Length - 1)() {}
        Dim looseIndexes As New Dictionary(Of Integer, File_Location)
        Dim packedGroups As New Dictionary(Of String, List(Of (OutputIndex As Integer, Location As File_Location)))(StringComparer.OrdinalIgnoreCase)

        For i As Integer = 0 To files.Length - 1
            Dim normalizedPath As String = files(i).Correct_Path_Separator
            Dim located_File As File_Location = Nothing

            If Dictionary.TryGetValue(normalizedPath, located_File) = False OrElse IsNothing(located_File) Then
                output(i) = Array.Empty(Of Byte)
                Continue For
            End If

            If located_File.IsLosseFile Then
                looseIndexes.Add(i, located_File)
            Else
                Dim group As List(Of (OutputIndex As Integer, Location As File_Location)) = Nothing
                If packedGroups.TryGetValue(located_File.BA2File, group) = False Then
                    group = New List(Of (OutputIndex As Integer, Location As File_Location))()
                    packedGroups.Add(located_File.BA2File, group)
                End If
                group.Add((i, located_File))
            End If
        Next

        Parallel.ForEach(looseIndexes.Keys, Sub(i As Integer)
                                                Dim located_File As File_Location = looseIndexes(i)
                                                If Not IsNothing(located_File) Then
                                                    output(i) = located_File.GetBytes()
                                                Else
                                                    output(i) = Array.Empty(Of Byte)
                                                End If
                                            End Sub)

        Parallel.ForEach(packedGroups, Sub(group)
                                           Dim archivePath = IO.Path.Combine(FO4Path, group.Key)

                                           Try
                                               ' Este camino NO usa el pool (abre su propio stream), pero necesita el
                                               ' MISMO share mode: un prefetch en vuelo bloqueaba el File.Move del
                                               ' packager igual que un reader pooleado. Ver AbrirArchiveParaLectura.
                                               Using fs As FileStream = AbrirArchiveParaLectura(archivePath)
                                                   Using pack As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                                                       For Each item In group.Value
                                                           Dim bytes = item.Location.GetBytesFromOpenArchive(pack)
                                                           output(item.OutputIndex) = bytes
                                                           ' Populate _bytesCache so subsequent GetBytes() calls hit the cache
                                                           ' instead of re-opening the archive (prefetch only warms OS cache otherwise)
                                                           If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                                                               _bytesCache(item.Location.FullPath) = New WeakReference(Of Byte())(bytes)
                                                           End If
                                                       Next
                                                   End Using
                                               End Using
                                           Catch
                                               For Each item In group.Value
                                                   output(item.OutputIndex) = Array.Empty(Of Byte)
                                               Next
                                           End Try
                                       End Sub)

        Return output
    End Function

    Private Shared totalCount As Integer
    Private Shared completed As Integer

    ''' <summary>Los sueltos reportan progreso cada N (esta mascara + 1), no en cada item.
    ''' <para>Cada Report es un Post que el hilo de UI tiene que drenar y los workers no bloquean, asi que con
    ''' cientos de miles de sueltos la cola crecia sin limite y el form seguia procesandola mucho despues de
    ''' terminado el scan: esa es la forma exacta del "se queda colgado montando archives".</para>
    ''' <para>Los ARCHIVES no se throttlean (son decenas y cada uno trae su label), ni el canal de BYTES, que
    ''' es lo unico que mueve la barra de detalle. El ULTIMO item siempre reporta pase lo que pase: los
    ''' consumidores no clampean al maximo y sin ese tick la barra queda visiblemente corta.</para></summary>
    Private Const LooseProgressReportMask As Integer = &H1FF   ' report every 512th loose file

    ''' <summary>Heartbeat cadence for the loose WALK (every 4096th file discovered). Same reasoning as
    ''' <see cref="LooseProgressReportMask"/> — every Report is a Post the UI thread has to drain — but a
    ''' coarser mask, because this one fires from the walk threads while the scan workers are ALSO
    ''' reporting, and its only job is to prove the app is alive during what used to be a dark window.</summary>
    Private Const WalkHeartbeatMask As Integer = &HFFF

    ''' <summary>True once the loose walk has stopped producing (<c>CompleteAdding</c> called).
    '''
    ''' <para>⛔ Load-bearing for the progress throttle. <see cref="ProcessLooseFile"/> force-reports the
    ''' item where <c>completed &gt;= totalCount</c> so consumers that never clamp their bar to Max still
    ''' finish full. That was safe when totalCount was known up front. Now the walk STREAMS and totalCount
    ''' GROWS behind it, so during the scan the workers are routinely caught up with the producer and
    ''' <c>completed = totalCount</c> holds for a large fraction of the files — which would fire the
    ''' "final" report on nearly EVERY loose file and rebuild the exact Post storm the throttle exists to
    ''' prevent. Gating the force on "production is finished" restores it to what it means: the last
    ''' item.</para></summary>
    Private Shared _scanProductionComplete As Boolean = False

    ''' <summary>Null-safe progress report. Fill_DictionaryAsync's <c>progress</c> parameter is not
    ''' optional, but callers do pass Nothing (the CLI used to, and hit an NRE that its own Try swallowed —
    ''' leaving an EMPTY dictionary and no error). A no-op is the right answer for a caller that doesn't
    ''' want progress.</summary>
    Private Shared Sub ReportScan(progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer)),
                                  stepName As String, value As Integer, max As Integer)
        progress?.Report((stepName, value, max))
    End Sub

    ''' <summary>One-line summary of the LAST <see cref="Fill_DictionaryAsync"/>: volumes (archives, cache
    ''' hits, loose, entries) and per-phase timings. Held in memory only — building it is three stopwatches
    ''' and one string, and NOTHING is written anywhere unless a caller decides to (NPC_Manager only does so
    ''' under its <c>--diagnoseLoad</c> switch). Empty until the first scan completes.</summary>
    Public Shared Property LastScanDiagnostics As String = ""

    ' Byte-weighted progress for the archive (BA2/BSA) phase only. Mirrors the completed/totalCount
    ' pattern above (module-level Shared, read/incremented by ProcessBa2File). Loose files are NOT
    ' counted here (stat'ing thousands of loose files would be expensive); this bar reaches 100% when
    ' the BA2/BSA set is done. Nothing when the caller didn't request a byte progress (CLI/WM/MainForm).
    Private Shared _archiveBytesDone As Long
    Private Shared _archiveBytesTotal As Long
    Private Shared _archiveByteProgress As IProgress(Of (Done As Long, Total As Long))

    ' Diagnostics for the per-phase log (see Fill_DictionaryAsync). Reset at the start of each scan.
    Private Shared _archivesFromCache As Integer
    Private Shared _archivesReindexed As Integer

    ''' <summary>
    ''' Errores acumulados por workers durante Fill_DictionaryAsync. Se drenan en el
    ''' UI thread después del await. NUNCA mostrar MsgBox desde un worker: bloquea
    ''' el Parallel.ForEach indefinidamente si la UI no pumpea (ventana oculta atrás
    ''' del form principal) y cuelga toda la app.
    ''' </summary>
    Private Shared ReadOnly _scanErrors As New System.Collections.Concurrent.ConcurrentQueue(Of String)

    Public Shared Function DrainScanErrors() As List(Of String)
        Dim result As New List(Of String)
        Dim msg As String = Nothing
        While _scanErrors.TryDequeue(msg)
            result.Add(msg)
        End While
        Return result
    End Function

    ''' <summary>
    ''' Per-archive scan outcome reported by Fill_DictionaryAsync workers. Apps drain
    ''' this after the fill to log whether each BA2/BSA was loaded from the index
    ''' cache or re-scanned from the archive.
    ''' </summary>
    Private Shared ReadOnly _scanReport As New System.Collections.Concurrent.ConcurrentQueue(Of (ArchiveName As String, CacheHit As Boolean))

    Public Shared Function DrainScanReport() As List(Of (ArchiveName As String, CacheHit As Boolean))
        Dim result As New List(Of (ArchiveName As String, CacheHit As Boolean))
        Dim item As (ArchiveName As String, CacheHit As Boolean) = Nothing
        While _scanReport.TryDequeue(item)
            result.Add(item)
        End While
        Return result
    End Function

    ''' <summary>Register app-specific extensions to include in dictionary scans (e.g. ".osp", ".xml").</summary>
    Public Shared Sub RegisterExtensions(ParamArray extensions() As String)
        For Each ext In extensions
            SupportedExtensions.Add(ext)
        Next
    End Sub

    ''' <summary>Store app-specific data by type. Apps use this to attach their own state to the dictionary lifecycle.</summary>
    Public Shared Sub SetAppData(Of T As Class)(value As T)
        _appData(GetType(T)) = value
    End Sub

    ''' <summary>Retrieve app-specific data by type. Returns Nothing if not set.</summary>
    Public Shared Function GetAppData(Of T As Class)() As T
        Dim val As Object = Nothing
        If _appData.TryGetValue(GetType(T), val) Then Return DirectCast(val, T)
        Return Nothing
    End Function

    Public Shared Property FO4Path As String
        Get
            Return _fO4Path
        End Get
        Set(value As String)
            _fO4Path = value
        End Set
    End Property

    ''' <summary>
    ''' RAÍZ del cache de índices de archives. El caller la setea antes de Fill_DictionaryAsync; vacía =
    ''' cache deshabilitado. Los <c>.cac</c> NO viven acá directamente: van en una SUBCARPETA POR JUEGO
    ''' (ver <see cref="EffectiveCacheDirectory"/>). Los ~30 call sites siguen seteando la raíz y no saben
    ''' nada del juego — la game-awareness vive acá adentro, en un solo lugar, para que no puedan divergir.
    ''' </summary>
    Public Shared Property CacheDirectory As String
        Get
            Return _cacheDirectory
        End Get
        Set(value As String)
            _cacheDirectory = If(value, "")
        End Set
    End Property

    ''' <summary>Subcarpeta del cache para el juego ACTIVO. Se lee en cada operación de cache (no se
    ''' memoiza) porque el juego se puede cambiar en caliente desde el selector de la app.</summary>
    Private Shared Function GameCacheFolderName() As String
        ' Config_App.Current se inicializa siempre (= New Config_App()), pero es una propiedad seteable:
        ' si alguien la pone en Nothing, un nombre neutro es mejor que mezclar los dos juegos en la raíz.
        If Config_App.Current Is Nothing Then Return "Unknown"
        Return If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, "Skyrim", "Fallout4")
    End Function

    ''' <summary>Etiqueta estable (8 hex) del SET DE EXTENSIONES con el que se indexó. FNV-1a sobre la lista
    ''' canónica (minúsculas, ordenada) — ⛔ NO <c>String.GetHashCode</c>: está randomizado por proceso, así
    ''' que daría una carpeta distinta en cada arranque y el cache no serviría nunca.</summary>
    Private Shared Function ExtensionSetTag() As String
        Dim exts = _canonicalExtensionsSnapshot
        If exts Is Nothing Then exts = BuildCanonicalExtensionsSnapshot()

        Dim h As ULong = 2166136261UL
        For Each ext In exts
            For Each ch In ext
                h = ((h Xor CULng(AscW(ch))) * 16777619UL) And &HFFFFFFFFUL
            Next
            h = ((h Xor 124UL) * 16777619UL) And &HFFFFFFFFUL   ' "|" separator
        Next
        Return h.ToString("x8")
    End Function

    ''' <summary>⛔ Dónde viven REALMENTE los <c>.cac</c>: <c>{CacheDirectory}\{Juego}\{ExtSetTag}\</c>.
    '''
    ''' <para>⛔ Las DOS subcarpetas son necesarias y arreglan el mismo bug en dos capas. La de JUEGO: la
    ''' limpieza de huérfanos borra todo <c>.cac</c> que no esté en los archives del juego ACTIVO, así que
    ''' con carpeta compartida cambiar de juego DESTRUÍA el cache del otro. La de SET DE EXTENSIONES: un
    ''' <c>.cac</c> sólo vale para el set con el que se generó, y las apps no comparten set — con una sola
    ''' carpeta cada app rechazaba y REESCRIBÍA los de la otra, y la siguiente corrida re-indexaba todo desde
    ''' cero. Separados, coexisten en vez de pisarse.</para>
    '''
    ''' <para>Devuelve "" con el cache deshabilitado, para que los callers usen el guard de siempre.</para></summary>
    Private Shared Function EffectiveCacheDirectory() As String
        If Not IsCacheEnabled() Then Return ""
        Return Path.Combine(_cacheDirectory, GameCacheFolderName(), ExtensionSetTag())
    End Function

    ' ================== Archive index cache ==================
    ' Binary format "FD4I" v1 per-archive file at {CacheDirectory}\{name}.cac
    '   4B  magic        = 'F','D','4','I'
    '   2B  version u16  = 1
    '   8B  size i64
    '   8B  mtimeUtc i64 (DateTime.ToBinary of LastWriteTimeUtc)
    '   4B  ext_count u32
    '   N x [u16 len + utf8 bytes]   lowercase, sorted ordinal ascending (canonical)
    '   4B  entry_count u32
    '   N x [i32 index + u16 dir_len + utf8 fullpath bytes]
    Private Shared ReadOnly CacheMagic As Byte() = {&H46, &H44, &H34, &H49} ' "FD4I"
    Private Const CacheFormatVersion As UShort = 1US
    Private Const CacheFileSuffix As String = ".cac"
    Private Const CacheTempSuffix As String = ".cac.tmp"

    Private Shared _canonicalExtensionsSnapshot As List(Of String) = Nothing

    Private Structure CachedEntry
        Public Index As Integer
        Public FullPath As String
    End Structure

    Private Shared Function IsCacheEnabled() As Boolean
        Return Not String.IsNullOrEmpty(_cacheDirectory)
    End Function

    Private Shared Function GetCacheFilePath(archiveFileName As String) As String
        If Not IsCacheEnabled() Then Return ""
        ' Subcarpeta por juego. WriteCacheFile crea el directorio a partir de este path, así que no hace
        ' falta crearlo antes en ningún lado.
        Return Path.Combine(EffectiveCacheDirectory(), archiveFileName & CacheFileSuffix)
    End Function

    Private Shared Function BuildCanonicalExtensionsSnapshot() As List(Of String)
        Dim result As New List(Of String)(SupportedExtensions.Count)
        For Each ext In SupportedExtensions
            If Not String.IsNullOrEmpty(ext) Then result.Add(ext.ToLowerInvariant())
        Next
        result.Sort(StringComparer.Ordinal)
        Return result
    End Function

    Private Shared Sub WriteUtf8String(bw As BinaryWriter, s As String)
        If s Is Nothing Then s = ""
        Dim bytes = Encoding.UTF8.GetBytes(s)
        If bytes.Length > UInt16.MaxValue Then
            Throw New InvalidDataException("Archive cache: string exceeds u16 length prefix.")
        End If
        bw.Write(CUShort(bytes.Length))
        If bytes.Length > 0 Then bw.Write(bytes)
    End Sub

    Private Shared Function ReadUtf8String(br As BinaryReader) As String
        Dim len As UShort = br.ReadUInt16()
        If len = 0US Then Return ""
        Dim bytes = br.ReadBytes(CInt(len))
        If bytes.Length <> CInt(len) Then Throw New EndOfStreamException("Archive cache: short string read.")
        Return Encoding.UTF8.GetString(bytes)
    End Function

    Private Shared Function TryLoadArchiveIndex(
        cachePath As String,
        expectedSize As Long,
        expectedMtimeUtc As Date,
        expectedExtsCanonical As List(Of String),
        ByRef entries As List(Of CachedEntry)) As Boolean

        entries = Nothing
        If String.IsNullOrEmpty(cachePath) Then Return False
        If Not File.Exists(cachePath) Then Return False

        Try
            Using fs As FileStream = File.OpenRead(cachePath)
                Using br As New BinaryReader(fs, Encoding.UTF8, leaveOpen:=False)
                    Dim magic = br.ReadBytes(4)
                    If magic.Length <> 4 Then Return False
                    For i As Integer = 0 To 3
                        If magic(i) <> CacheMagic(i) Then Return False
                    Next

                    Dim version As UShort = br.ReadUInt16()
                    If version <> CacheFormatVersion Then Return False

                    Dim cachedSize As Long = br.ReadInt64()
                    If cachedSize <> expectedSize Then Return False

                    Dim cachedMtimeBinary As Long = br.ReadInt64()
                    Dim cachedMtime = Date.FromBinary(cachedMtimeBinary)
                    If cachedMtime <> expectedMtimeUtc Then Return False

                    Dim extCount As UInteger = br.ReadUInt32()
                    If extCount > 1024UI Then Return False
                    If CInt(extCount) <> expectedExtsCanonical.Count Then Return False
                    For i As Integer = 0 To CInt(extCount) - 1
                        Dim ext = ReadUtf8String(br)
                        If Not String.Equals(ext, expectedExtsCanonical(i), StringComparison.Ordinal) Then Return False
                    Next

                    Dim entryCount As UInteger = br.ReadUInt32()
                    If entryCount > 10000000UI Then Return False
                    Dim result As New List(Of CachedEntry)(CInt(entryCount))
                    For i As Integer = 0 To CInt(entryCount) - 1
                        Dim idx As Integer = br.ReadInt32()
                        Dim fullPath = ReadUtf8String(br)
                        result.Add(New CachedEntry With {.Index = idx, .FullPath = fullPath})
                    Next

                    entries = result
                    Return True
                End Using
            End Using
        Catch
            entries = Nothing
            Return False
        End Try
    End Function

    Private Shared Sub SaveArchiveIndex(
        cachePath As String,
        archiveSize As Long,
        archiveMtimeUtc As Date,
        extsCanonical As List(Of String),
        entries As List(Of CachedEntry))

        If String.IsNullOrEmpty(cachePath) Then Return

        Dim dir = Path.GetDirectoryName(cachePath)
        If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
            Directory.CreateDirectory(dir)
        End If

        Dim temp = cachePath & ".tmp"
        Try
            Using fs As FileStream = File.Create(temp)
                Using bw As New BinaryWriter(fs, Encoding.UTF8, leaveOpen:=False)
                    bw.Write(CacheMagic)
                    bw.Write(CacheFormatVersion)
                    bw.Write(archiveSize)
                    bw.Write(archiveMtimeUtc.ToBinary())
                    bw.Write(CUInt(extsCanonical.Count))
                    For Each ext In extsCanonical
                        WriteUtf8String(bw, ext)
                    Next
                    bw.Write(CUInt(entries.Count))
                    For Each e In entries
                        bw.Write(e.Index)
                        WriteUtf8String(bw, e.FullPath)
                    Next
                End Using
            End Using

            If File.Exists(cachePath) Then
                File.Replace(temp, cachePath, Nothing, ignoreMetadataErrors:=True)
            Else
                File.Move(temp, cachePath)
            End If
        Catch
            Try
                If File.Exists(temp) Then File.Delete(temp)
            Catch
            End Try
            Throw
        End Try
    End Sub

    ''' <summary>Borra los <c>.cac</c> del juego ACTIVO que ya no corresponden a ningún archive escaneado.
    '''
    ''' <para>⛔ Opera SÓLO dentro de <see cref="EffectiveCacheDirectory"/> (la subcarpeta del juego) y
    ''' <c>EnumerateFiles</c> NO recursa ⇒ el cache del otro juego es literalmente invisible para este
    ''' barrido. Antes los dos juegos compartían carpeta y este mismo código, al no encontrar los archives
    ''' del otro juego en <c>scannedArchives</c>, los declaraba huérfanos y los BORRABA: cambiar de juego
    ''' costaba un re-index completo.</para></summary>
    Private Shared Sub CleanupOrphanCacheFiles(scannedArchives As IEnumerable(Of String))
        If Not IsCacheEnabled() Then Return

        PurgeLegacyRootCaches()

        Dim dir = EffectiveCacheDirectory()
        If Not Directory.Exists(dir) Then Return

        Try
            Dim validNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each ba2 In scannedArchives
                validNames.Add(Path.GetFileName(ba2) & CacheFileSuffix)
            Next

            For Each cacheFile In Directory.EnumerateFiles(dir, "*" & CacheFileSuffix)
                Dim name = Path.GetFileName(cacheFile)
                If Not validNames.Contains(name) Then
                    Try
                        File.Delete(cacheFile)
                    Catch
                    End Try
                End If
            Next

            ' Clean leftover temp files from aborted writes.
            For Each tmpFile In Directory.EnumerateFiles(dir, "*" & CacheTempSuffix)
                Try
                    File.Delete(tmpFile)
                Catch
                End Try
            Next
        Catch ex As Exception
            _scanErrors.Enqueue("Cache cleanup failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Migracion de una sola vez: barre los <c>.cac</c> que quedaron en la RAIZ del cache, de cuando
    ''' los dos juegos compartian carpeta. Son inalcanzables desde que pasaron a la subcarpeta por juego, y el
    ''' barrido por-juego no los ve. Borrar es seguro: un <c>.cac</c> es cache puro derivado del archive y se
    ''' regenera solo; ademas no hay forma de saber a que juego pertenece cada uno (justo el bug que arregla).
    ''' <c>EnumerateFiles</c> no recursa, asi que las subcarpetas por juego quedan intactas.</summary>
    Private Shared Sub PurgeLegacyRootCaches()
        If Not IsCacheEnabled() Then Return

        ' Las DOS ubicaciones que quedaron obsoletas, en orden histórico:
        '   1. la RAÍZ del cache        — de cuando los dos juegos compartían carpeta;
        '   2. {raíz}\{Juego}\          — de cuando un juego tenía UNA sola carpeta para todos los sets de
        '                                 extensiones (ver EffectiveCacheDirectory).
        ' Ninguna de las dos es alcanzable ya: nadie las lee y el barrido por-set no las ve. EnumerateFiles
        ' NO recursa, así que borrar en {raíz}\{Juego}\ deja intactas las subcarpetas por-set que cuelgan de
        ' ella. Borrar es seguro en cualquier caso: un .cac es cache PURO derivado del archive y se regenera
        ' solo; el peor caso es un re-index una única vez.
        For Each legacyDir In {_cacheDirectory, Path.Combine(_cacheDirectory, GameCacheFolderName())}
            Try
                If Not Directory.Exists(legacyDir) Then Continue For
                For Each pat In {"*" & CacheFileSuffix, "*" & CacheTempSuffix}
                    For Each f In Directory.EnumerateFiles(legacyDir, pat)   ' sólo ese nivel, no recursa
                        Try
                            File.Delete(f)
                        Catch
                        End Try
                    Next
                Next
            Catch
                ' Best-effort: si no se puede limpiar, son bytes muertos y nada más. No romper el scan por esto.
            End Try
        Next
    End Sub
    ' =========================================================


    Public Shared Property Dictionary As ConcurrentDictionary(Of String, File_Location)
        Get
            Return _dictionary
        End Get
        Set(value As ConcurrentDictionary(Of String, File_Location))
            If IsNothing(value) Then
                _dictionary = New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
            Else
                _dictionary = value
            End If

            _overriddenEntries.Clear()
            RebuildSearchIndexesFromDictionary()
        End Set
    End Property
    Private Shared Sub PushOverriddenEntry(normalized As String, loser As File_Location)
        Dim stack = _overriddenEntries.GetOrAdd(normalized, Function(key) New ConcurrentStack(Of File_Location)())
        stack.Push(loser)
    End Sub

    ''' <summary>Normalizes a path into a dictionary key. This is the HOT LOOKUP path (GetBytes,
    ''' GetOverriddenEntries, RemoveDictionaryEntry…), so it must stay allocation-free for the common
    ''' case — String.Replace returns the same instance when there is nothing to replace. It used to
    ''' String.Intern the result, which permanently retained every string anyone ever looked up; see
    ''' <see cref="PoolPath"/> for why that went away and where de-duplication happens now.</summary>
    Private Shared Function NormalizeDictionaryKey(fullPath As String) As String
        If IsNothing(fullPath) Then Return ""
        Return fullPath.Correct_Path_Separator
    End Function

    ''' <summary>Inserts <paramref name="entry"/> under <paramref name="key"/> during a scan, resolving a
    ''' collision with any entry already there via <see cref="Resolve_Conflict"/> and pushing the LOSER
    ''' onto the override stack.
    '''
    ''' <para>⛔ Why this exists instead of ConcurrentDictionary.AddOrUpdate: AddOrUpdate's
    ''' updateValueFactory is documented to run MORE THAN ONCE when its CAS loses a race, and the previous
    ''' code called PushOverriddenEntry from INSIDE that factory — so a losing attempt pushed a loser onto
    ''' the override stack and then pushed it again on the retry, leaving duplicate/phantom entries. That
    ''' was rare with 4 workers and becomes routine as the worker count goes up. Here the push happens only
    ''' after the compare-and-swap that actually won.</para></summary>
    Private Shared Sub AddEntryResolvingConflict(key As String, entry As File_Location)
        Do
            Dim existing As File_Location = Nothing
            If Not _dictionary.TryGetValue(key, existing) Then
                If _dictionary.TryAdd(key, entry) Then Return
                Continue Do   ' another worker inserted between the read and the add — re-resolve against it
            End If

            If Resolve_Conflict(existing, entry) Then
                ' New entry wins. Swap it in, then retire the loser. TryUpdate fails only if someone
                ' changed the slot meanwhile, in which case we re-resolve against the new occupant.
                If _dictionary.TryUpdate(key, entry, existing) Then
                    PushOverriddenEntry(key, existing)
                    Return
                End If
            Else
                ' Existing wins: OUR entry is the loser. The slot is untouched, so there is nothing to CAS.
                PushOverriddenEntry(key, entry)
                Return
            End If
        Loop
    End Sub

    Private Shared Function NormalizeDirectoryKey(directoryPath As String) As String
        If IsNothing(directoryPath) Then Return ""
        Dim normalized = directoryPath.Correct_Path_Separator.Trim()

        While normalized.EndsWith("\"c, StringComparison.Ordinal)
            normalized = normalized.Substring(0, normalized.Length - 1)
        End While

        Return normalized
    End Function

    Private Shared Function NormalizeRootPrefix(rootPrefix As String) As String
        Dim normalized = NormalizeDirectoryKey(rootPrefix)
        If String.IsNullOrEmpty(normalized) Then Return ""
        Return normalized & "\"
    End Function

    Private Shared Function NormalizeExtensionKey(extension As String) As String
        If String.IsNullOrWhiteSpace(extension) Then Return ""
        Dim ext = extension.Trim()
        If ext.StartsWith("."c) = False Then ext = "." & ext
        Return ext.ToLowerInvariant()
    End Function

    Private Shared Function BuildDirectoryExtensionBucketKey(directoryPath As String, extension As String) As String
        Return NormalizeDirectoryKey(directoryPath) & "|" & NormalizeExtensionKey(extension)
    End Function

    Private Shared Sub AddKeyToSearchIndex(index As ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte)), bucketKey As String, fullKey As String)
        Dim bucket = index.GetOrAdd(bucketKey, Function(key) New ConcurrentDictionary(Of String, Byte)(StringComparer.OrdinalIgnoreCase))
        bucket.TryAdd(fullKey, 0)
    End Sub

    Private Shared Sub IndexDictionaryKey(fullKey As String)
        IndexNormalizedKey(NormalizeDictionaryKey(fullKey))
    End Sub

    ''' <summary>Indexa una clave YA normalizada (las del diccionario siempre lo estan por construccion). Estar
    ''' separado de <see cref="IndexDictionaryKey"/> es lo que permite que el rebuild no re-normalice millones
    ''' de claves.
    ''' <para>CORRE BAJO <see cref="_keysByDirectoryLock"/> y eso NO es decoracion: sin el candado hay una
    ''' ventana que PIERDE claves en silencio. Un alta que entra mientras el build enumera lee built=False y
    ''' saltea el indexado, pero el enumerado del build ya paso por ese bucket y no la ve, asi que la clave
    ''' queda en el diccionario y fuera del indice. El enumerador de ConcurrentDictionary es lock-free y NO
    ''' garantiza ver lo agregado despues de empezar: "el build la levanta igual" es falso.</para>
    ''' <para>Costo: un SyncLock sin contencion por clave, despreciable porque esto NO corre durante el scan.
    ''' Los builds usan AddKeyToSearchIndex directo, asi que no hay reentrada desde sus workers.</para></summary>
    Private Shared Sub IndexNormalizedKey(fullKey As String)
        If String.IsNullOrEmpty(fullKey) Then Exit Sub

        ' LOS DOS índices son lazy ahora. Si ninguno está construido no hay nada que mantener, y salir ACÁ
        ' evita también el GetDirectoryName + GetExtension + ToLowerInvariant de abajo — que era lo caro y se
        ' pagaba igual aunque después no se escribiera en ningún lado. Chequeo barato SIN candado primero:
        ' si acá da "ninguno construido" y un build arranca justo después, ese build enumera el diccionario
        ' que YA contiene esta clave (el caller la insertó antes de llamarnos) ⇒ la levanta igual.
        If Not Volatile.Read(_keysByDirectoryBuilt) AndAlso Not Volatile.Read(_keysByDirectoryExtensionBuilt) Then Exit Sub

        Dim directoryKey = NormalizeDirectoryKey(IO.Path.GetDirectoryName(fullKey))
        Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(fullKey))

        SyncLock _keysByDirectoryLock
            ' Re-leídos DENTRO del candado: entre el chequeo barato de arriba y este punto pudo terminar (o
            ' empezar y terminar) un build, o un ClearSearchIndexes pudo bajar los flags.
            If _keysByDirectoryBuilt Then
                AddKeyToSearchIndex(_KeysByDirectory, directoryKey, fullKey)
            End If

            If _keysByDirectoryExtensionBuilt AndAlso extensionKey <> "" Then
                AddKeyToSearchIndex(_KeysByDirectoryExtension, directoryKey & "|" & extensionKey, fullKey)
            End If
        End SyncLock
    End Sub

    ''' <summary>Build <see cref="_KeysByDirectory"/> on demand, from the dictionary as it stands. Idempotent
    ''' and thread-safe; the flag is published INSIDE the lock and only after the index is fully populated, so
    ''' a concurrent <see cref="IndexNormalizedKey"/> either sees "not built" (and skips, because this pass
    ''' will pick its key up from the dictionary anyway) or sees "built" (and maintains it from then on).</summary>
    Private Shared Sub EnsureKeysByDirectoryBuilt()
        If Volatile.Read(_keysByDirectoryBuilt) Then Exit Sub
        SyncLock _keysByDirectoryLock
            If _keysByDirectoryBuilt Then Exit Sub
            _KeysByDirectory.Clear()
            For Each kvp In _dictionary
                Dim key = kvp.Key
                If String.IsNullOrEmpty(key) Then Continue For
                AddKeyToSearchIndex(_KeysByDirectory, NormalizeDirectoryKey(IO.Path.GetDirectoryName(key)), key)
            Next
            Volatile.Write(_keysByDirectoryBuilt, True)
        End SyncLock
    End Sub

    ''' <summary>Construye <see cref="_KeysByDirectoryExtension"/> on demand. Gemelo de
    ''' <see cref="EnsureKeysByDirectoryBuilt"/> - mismo candado, flag publicado DENTRO del lock y solo despues
    ''' de poblar - pero en PARALELO, porque es la pasada que antes corria dentro del scan: millones de claves
    ''' con GetDirectoryName + GetExtension + ToLowerInvariant + un insert hasheado cada una. Es seguro porque
    ''' los buckets son ConcurrentDictionary y los inserts son independientes del orden.
    ''' <para>El Parallel.ForEach corre DENTRO del SyncLock a proposito: su cuerpo no toma este candado, asi que
    ''' no hay reentrada, y otro hilo que entre queda bloqueado esperando la construccion en vez de duplicarla.</para>
    ''' <para>Itera el diccionario directamente y no <c>.Keys</c>: esa propiedad toma todos los locks internos y
    ''' materializa un array snapshot de todas las claves (decenas de MB de basura pura).</para></summary>
    Private Shared Sub EnsureKeysByDirectoryExtensionBuilt()
        If Volatile.Read(_keysByDirectoryExtensionBuilt) Then Exit Sub
        SyncLock _keysByDirectoryLock
            If _keysByDirectoryExtensionBuilt Then Exit Sub
            _KeysByDirectoryExtension.Clear()
            Parallel.ForEach(_dictionary,
                Sub(kvp)
                    Dim key = kvp.Key
                    If String.IsNullOrEmpty(key) Then Exit Sub
                    Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(key))
                    If extensionKey = "" Then Exit Sub
                    AddKeyToSearchIndex(_KeysByDirectoryExtension,
                                        NormalizeDirectoryKey(IO.Path.GetDirectoryName(key)) & "|" & extensionKey,
                                        key)
                End Sub)
            Volatile.Write(_keysByDirectoryExtensionBuilt, True)
        End SyncLock
    End Sub

    Private Shared Sub ClearSearchIndexes()
        ' ⛔ LOS DOS clears van DENTRO del candado ahora. Antes el de _KeysByDirectoryExtension estaba afuera
        ' porque ese índice no tenía flag y limpiarlo era idempotente. Con el flag hay estado que mantener
        ' coherente con el contenido: si el Clear corriera fuera del lock podría intercalarse con una
        ' construcción en curso y dejar el flag en True sobre un índice ya vaciado — o sea, "construido" y
        ' vacío, que se lee como "este directorio no tiene archivos" en vez de reconstruirse.
        SyncLock _keysByDirectoryLock
            _KeysByDirectory.Clear()
            Volatile.Write(_keysByDirectoryBuilt, False)
            _KeysByDirectoryExtension.Clear()
            Volatile.Write(_keysByDirectoryExtensionBuilt, False)
        End SyncLock
    End Sub

    ''' <summary>Invalida los indices de busqueda: el diccionario cambio de arriba abajo y lo indexado ya no le
    ''' corresponde. Ya NO reconstruye nada (conserva el nombre por sus dos call sites); los dos indices son
    ''' lazy y la reconstruccion se difiere a los Ensure*, que corren cuando -y solo si- alguien consulta. La
    ''' pasada paralela que vivia aca era el grueso de la fase "Building search index..." del arranque.</summary>
    Private Shared Sub RebuildSearchIndexesFromDictionary()
        ClearSearchIndexes()
    End Sub

    Public Shared Function TryAddDictionaryEntry(fullPath As String, location As File_Location) As Boolean
        Dim normalized = NormalizeDictionaryKey(fullPath)
        If _dictionary.TryAdd(normalized, location) Then
            IndexDictionaryKey(normalized)
            ' Clear stale byte cache for this entry
            Dim dummy As WeakReference(Of Byte()) = Nothing
            _bytesCache.TryRemove(normalized, dummy)
            Return True
        End If
        Return False
    End Function

    ''' <summary>Alta o actualizacion de una entrada (p.ej. una del BA2 reemplazada por un suelto que acaba de
    ''' escribir WM o el cloner de materiales). Solo las entradas de BA2 van al stack de overrides: loose sobre
    ''' loose es el mismo archivo sobreescrito y no hay nada que restaurar.
    ''' <para>CAS explicito y no AddOrUpdate, misma razon que <see cref="AddEntryResolvingConflict"/>: el
    ''' updateValueFactory puede correr MAS DE UNA VEZ si pierde la carrera, y esta factory tiene efecto
    ''' colateral (PushOverriddenEntry), asi que un intento perdedor apilaria el mismo loser dos veces.</para>
    ''' <para>Semantica distinta a la del scan: NO consulta Resolve_Conflict. La entrada del caller siempre
    ''' gana porque acaba de escribir el archivo.</para></summary>
    Public Shared Sub AddOrUpdateDictionaryEntry(fullPath As String, location As File_Location)
        Dim normalized = NormalizeDictionaryKey(fullPath)

        Do
            Dim existing As File_Location = Nothing
            If Not _dictionary.TryGetValue(normalized, existing) Then
                If _dictionary.TryAdd(normalized, location) Then Exit Do
                Continue Do   ' someone inserted between the read and the add — re-read and replace it
            End If

            ' Replace, then retire the loser — but only once the swap actually landed. TryUpdate fails
            ' only if another thread changed the slot meanwhile, in which case we retry against the new
            ' occupant (and push THAT one, not the stale one we had read).
            If _dictionary.TryUpdate(normalized, location, existing) Then
                If Not existing.IsLosseFile Then PushOverriddenEntry(normalized, existing)
                Exit Do
            End If
        Loop

        IndexDictionaryKey(normalized)
        Dim dummy As WeakReference(Of Byte()) = Nothing
        _bytesCache.TryRemove(normalized, dummy)
    End Sub

    ''' <summary>Removes the current entry. If an overridden entry exists (e.g. BA2 behind a loose), restores it.</summary>
    Public Shared Sub RemoveDictionaryEntry(fullPath As String)
        Dim normalized = NormalizeDictionaryKey(fullPath)
        Dim dummy As WeakReference(Of Byte()) = Nothing
        _bytesCache.TryRemove(normalized, dummy)

        ' Try to restore a previously overridden entry
        Dim stack As ConcurrentStack(Of File_Location) = Nothing
        Dim restored As File_Location = Nothing
        If _overriddenEntries.TryGetValue(normalized, stack) AndAlso stack.TryPop(restored) Then
            _dictionary(normalized) = restored
        Else
            Dim removed As File_Location = Nothing
            If _dictionary.TryRemove(normalized, removed) Then
                ' Remove from search indexes only if truly gone.
                ' ⛔ BAJO EL CANDADO, por la ventana SIMÉTRICA a la de IndexNormalizedKey: si un build está
                ' enumerando el diccionario y alcanzó a ver esta clave ANTES del TryRemove de arriba, la va a
                ' insertar en el índice; si la poda corriera sin candado podría ejecutarse ANTES de esa
                ' inserción y no borrar nada, dejando en el índice una clave que ya no está en el diccionario
                ' (un picker ofreciendo un archivo inexistente). Con el candado la poda espera a que el build
                ' publique y borra después. Si el índice no está construido, los TryGetValue no encuentran
                ' bucket y esto es un no-op correcto: el build posterior lee el diccionario YA sin la clave.
                Dim directoryKey = NormalizeDirectoryKey(IO.Path.GetDirectoryName(normalized))
                Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(normalized))
                SyncLock _keysByDirectoryLock
                    Dim bucket As ConcurrentDictionary(Of String, Byte) = Nothing
                    If _KeysByDirectory.TryGetValue(directoryKey, bucket) Then bucket.TryRemove(normalized, 0)
                    If extensionKey <> "" Then
                        If _KeysByDirectoryExtension.TryGetValue(BuildDirectoryExtensionBucketKey(directoryKey, extensionKey), bucket) Then bucket.TryRemove(normalized, 0)
                    End If
                End SyncLock
            End If
        End If
    End Sub

    ''' <summary>
    ''' Mounts a BA2/BSA archive at runtime, populating Dictionary with all of its supported entries.
    ''' Use this when adding archives generated after Fill_DictionaryAsync has run (e.g. WM Pack output).
    ''' Idempotent: a second call on the same archive name is a no-op (call UnregisterArchive first if
    ''' the archive content changed and needs to be re-read).
    ''' </summary>
    ''' <param name="archivePath">Absolute or Data-relative path to the .ba2 or .bsa file.</param>
    ''' <param name="sourceOrder">Resolve_Conflict priority. Default makes runtime-registered archives
    ''' win over any scan-time archive while still letting loose files override them.</param>
    Public Shared Sub RegisterArchive(archivePath As String,
                                      Optional sourceOrder As Integer = ArchiveSourceOrder_RuntimeRegistered)
        If String.IsNullOrWhiteSpace(archivePath) Then Throw New ArgumentException("archivePath is empty.", NameOf(archivePath))

        Dim absolutePath As String = If(Path.IsPathRooted(archivePath),
                                        archivePath,
                                        Path.Combine(FO4Path, archivePath))
        If Not File.Exists(absolutePath) Then
            Throw New FileNotFoundException("Archive not found: " & absolutePath, absolutePath)
        End If

        Dim archiveFileName = Path.GetFileName(absolutePath)

        ' Montar es CAMBIAR lo que hay en esa ruta (WM Pack y el packer de FaceGen escriben un .ba2 nuevo
        ' ahi, y el CLI monta SIN desmontar antes): los readers pooleados de la generacion anterior tienen la
        ' tabla de entradas VIEJA. Va ANTES del guard de idempotencia porque `_registeredArchives` esta
        ' indexado por NOMBRE DE ARCHIVO: con dos archives homonimos en carpetas distintas el segundo sale
        ' temprano y nunca bumpearia. Bumpear de mas sale un re-parseo; no bumpear un archive que cambio sale
        ' bytes equivocados.
        BumpArchiveEpoch(ArchiveKey(absolutePath))

        If Not _registeredArchives.TryAdd(archiveFileName, 0) Then Exit Sub

        Dim added As New ConcurrentBag(Of String)()
        Dim noopProgress As IProgress(Of (String, Integer, Integer)) =
            New Progress(Of (String, Integer, Integer))(Sub(_x)
                                                            ' no-op: runtime register doesn't surface progress
                                                        End Sub)

        ProcessBa2File(absolutePath, sourceOrder, noopProgress, added)

        ' Index only the keys touched by this archive instead of rebuilding the entire search index.
        For Each key In added
            IndexDictionaryKey(key)
        Next
    End Sub

    ''' <summary>
    ''' Unmounts an archive registered at runtime (or discovered by the initial scan): removes its
    ''' entries from Dictionary, restoring any previously overridden entry from the override stack,
    ''' and disposes pooled readers for the archive file.
    ''' Safe to call on archives that aren't currently mounted (no-op).
    ''' </summary>
    ''' <param name="archivePath">Absolute or Data-relative path to the .ba2 or .bsa file.</param>
    Public Shared Sub UnregisterArchive(archivePath As String)
        If String.IsNullOrWhiteSpace(archivePath) Then Throw New ArgumentException("archivePath is empty.", NameOf(archivePath))

        Dim absolutePath As String = If(Path.IsPathRooted(archivePath),
                                        archivePath,
                                        Path.Combine(FO4Path, archivePath))
        Dim archiveFileName = Path.GetFileName(absolutePath)

        ' Snapshot keys to remove before mutating the dictionary.
        Dim toRemove As New List(Of String)
        For Each kvp In _dictionary
            If kvp.Value IsNot Nothing AndAlso
               kvp.Value.BA2File.Equals(archiveFileName, StringComparison.OrdinalIgnoreCase) Then
                toRemove.Add(kvp.Key)
            End If
        Next

        For Each key In toRemove
            RemoveDictionaryEntry(key)
        Next

        ' Drop pooled readers for this archive (their backing FileStream may be invalid after rewrite).
        ' ⛔ EL BUMP DE GENERACION VA ANTES DE VACIAR. Al reves queda una ventana en la que un reader devuelto
        ' entre el vaciado y el bump entra al pool con el epoch viejo TODAVIA vigente y sobrevive a la
        ' invalidacion. Ver `_archiveEpoch` — incluido lo que esto NO cierra.
        Dim poolKey = ArchiveKey(absolutePath)
        BumpArchiveEpoch(poolKey)
        ' Y el sello de CONTENIDO: los `Index` que este archive repartio dejan de valer acá. Es lo que
        ' invalida los File_Location que otros hilos ya tengan en la mano. Ver File_Location.ArchiveGen.
        BumpArchiveContentGen(poolKey)
        DrainAndDisposeBag(poolKey)

        Dim removedFlag As Byte = 0
        _registeredArchives.TryRemove(archiveFileName, removedFlag)
    End Sub

    ''' <summary>Returns the overridden entries for a key (from most recent to oldest), or empty if none.</summary>
    Public Shared Function GetOverriddenEntries(fullPath As String) As File_Location()
        Dim normalized = NormalizeDictionaryKey(fullPath)
        Dim stack As ConcurrentStack(Of File_Location) = Nothing
        If _overriddenEntries.TryGetValue(normalized, stack) Then
            Return stack.ToArray()
        End If
        Return Array.Empty(Of File_Location)()
    End Function

    Public Shared Function GetFilesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As List(Of String)
        Return CollectFilesInDirectory(directoryPath, allowedExtensions).
        OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).
        ToList()
    End Function

    ''' <summary>Cuerpo de <see cref="GetFilesInDirectory"/> SIN ordenar, para
    ''' <see cref="GetFileNamesInDirectory"/>, que re-ordena por nombre de archivo después de mapear.</summary>
    Private Shared Function CollectFilesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As HashSet(Of String)
        Dim directoryKey = NormalizeDirectoryKey(directoryPath)
        Dim results As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Dim extensionSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If Not IsNothing(allowedExtensions) Then
            For Each ext In allowedExtensions
                Dim normalizedExt = NormalizeExtensionKey(ext)
                If normalizedExt <> "" Then extensionSet.Add(normalizedExt)
            Next
        End If

        If extensionSet.Count = 0 Then
            ' "Every file in this directory, any extension" — the only query that reads _KeysByDirectory.
            ' The index is not populated during the scan (see _KeysByDirectory); build it on first ask.
            EnsureKeysByDirectoryBuilt()
            Dim directoryBucket As ConcurrentDictionary(Of String, Byte) = Nothing
            If _KeysByDirectory.TryGetValue(directoryKey, directoryBucket) Then
                For Each key In directoryBucket.Keys
                    results.Add(key)
                Next
            End If
        Else
            ' El índice por (directorio, extensión) tampoco se puebla durante el scan — ver
            ' _KeysByDirectoryExtension. Construirlo en el primer pedido.
            EnsureKeysByDirectoryExtensionBuilt()
            For Each ext In extensionSet
                Dim bucketKey = BuildDirectoryExtensionBucketKey(directoryKey, ext)
                Dim directoryExtBucket As ConcurrentDictionary(Of String, Byte) = Nothing

                If _KeysByDirectoryExtension.TryGetValue(bucketKey, directoryExtBucket) Then
                    For Each key In directoryExtBucket.Keys
                        results.Add(key)
                    Next
                End If
            Next
        End If

        Return results
    End Function

    Public Shared Function GetFileNamesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As String()
        Return CollectFilesInDirectory(directoryPath, allowedExtensions).
        Select(Function(k) IO.Path.GetFileName(k)).
        OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).
        ToArray()
    End Function

    Public Shared Function GetFilteredKeys(config As DictionaryFilePickerConfig) As List(Of String)
        ArgumentNullException.ThrowIfNull(config)
        Return GetFilteredKeys(config.RootPrefix, config.AllowedExtensions)
    End Function

    Public Shared Function GetFilteredKeys(rootPrefix As String, allowedExtensions As IEnumerable(Of String)) As List(Of String)
        Dim normalizedRoot = NormalizeRootPrefix(rootPrefix)
        Dim results As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim extensionSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        If Not IsNothing(allowedExtensions) Then
            For Each ext In allowedExtensions
                Dim normalizedExt = NormalizeExtensionKey(ext)
                If normalizedExt <> "" Then extensionSet.Add(normalizedExt)
            Next
        End If

        If extensionSet.Count = 0 Then Return New List(Of String)

        ' Único otro lector del índice por (directorio, extensión). Ver _KeysByDirectoryExtension: se
        ' construye acá, en el primer pedido, no durante el scan. El guard de arriba va ANTES a propósito —
        ' sin extensiones esta función devuelve vacío sin mirar el índice, así que no hay por qué construirlo.
        EnsureKeysByDirectoryExtensionBuilt()

        For Each ext In extensionSet
            Dim suffix = "|" & ext   ' ej: "|.dds"

            For Each bucketKey In _KeysByDirectoryExtension.Keys   ' recorre directorios, no archivos
                If Not bucketKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) Then Continue For
                If Not DictionaryFilePickerConfig.PathStartsWithRoot(bucketKey, normalizedRoot) Then Continue For

                Dim bucket As ConcurrentDictionary(Of String, Byte) = Nothing
                If _KeysByDirectoryExtension.TryGetValue(bucketKey, bucket) Then
                    For Each key In bucket.Keys
                        results.Add(key)
                    Next
                End If
            Next
        Next

        Return results.OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    ''' <param name="includeInactiveArchives">True: los archives de plugins NO cargados se indexan igual, pero
    ''' con el SourceOrder MAS BAJO, asi que cualquier archive de plugin cargado (y los sueltos) le gana. WM lo
    ''' usa para inspeccionar/clonar material de mods inactivos; NPC_Manager usa False, que es lo que hace el
    ''' motor.</param>
    ''' <param name="loadedPlugins">El set de plugins que la sesion considera CARGADOS, en load order: una sola
    ''' respuesta a "que esta cargado", compartida por records y assets. Destildar un plugin baja sus records Y
    ''' sus archives juntos. Nothing = leer el load order activo de Plugins.txt. Antes los records salian de los
    ''' ticks y los archives siempre de Plugins.txt: dos nociones distintas de "cargado".</param>
    Public Shared Async Function Fill_DictionaryAsync(Fo4DataPath As String,
                                                      progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer)),
                                                      Optional includeInactiveArchives As Boolean = False,
                                                      Optional archiveByteProgress As IProgress(Of (Done As Long, Total As Long)) = Nothing,
                                                      Optional loadedPlugins As IEnumerable(Of String) = Nothing) As Task
        Try
            ' Sub-phase timings + counts. The preflight slowdown reports come from users' rigs, not from
            ' a repro we have, so the log has to say WHICH phase ate the time (enumerate / scan / index)
            ' and on what volume (archives, cache hits, loose, entries).
            Dim swTotal = System.Diagnostics.Stopwatch.StartNew()
            Dim swPhase = System.Diagnostics.Stopwatch.StartNew()
            _archivesFromCache = 0
            _archivesReindexed = 0

            FO4Path = Fo4DataPath
            Dictionary.Clear()
            _overriddenEntries.Clear()
            ClearSearchIndexes()

            ' Drop the previous scan's pooled paths — they're only referenced by the dictionary we
            ' just cleared. (String.Intern, which this replaced, could never release them.)
            _pathPool = New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

            ' O1.1: Clear byte cache when dictionary is rebuilt
            ClearBytesCache()

            ' O1.2: Dispose idle readers and initialize pool cleanup timer
            ' ⛔ EL RE-SCAN INVALIDA TODA GENERACION. `DisposeIdleReaders` CONSERVA a proposito lo devuelto en
            ' los ultimos 30 s, y despues de esto `_dictionary` va a tener `Index` NUEVOS, derivados de los
            ' archivos como estan AHORA. Si algun .ba2 cambio entre scans —que es exactamente lo que hacen WM
            ' Pack y el packer de FaceGen— un reader conservado extraeria en el indice equivocado y esos bytes
            ' quedan ademas pegados en `_bytesCache`. Bumpear primero hace que ese mismo `DisposeIdleReaders`
            ' los coseche en vez de conservarlos.
            For Each poolKey In _archivePool.Keys
                BumpArchiveEpoch(poolKey)
            Next
            ' Y el sello de CONTENIDO de todo archive que haya repartido indices: el re-scan los va a
            ' re-derivar de los archivos como estan AHORA. Se recorren las claves del sello, no las del pool:
            ' un archive puede haber repartido entradas sin que nadie le haya alquilado un reader todavia.
            For Each genKey In _archiveContentGen.Keys
                BumpArchiveContentGen(genKey)
            Next
            DisposeIdleReaders()
            InitPoolCleanupTimer()

            ' Snapshot SupportedExtensions once per scan, BEFORE the loose walk that filters against it.
            ' Workers read these read-only, so extensions registered AFTER the scan starts won't affect
            ' this run (and can't mutate the set mid-enumeration).
            _canonicalExtensionsSnapshot = BuildCanonicalExtensionsSnapshot()
            Dim extensionsSnapshot As New HashSet(Of String)(SupportedExtensions, StringComparer.OrdinalIgnoreCase)

            Dim ba2Files = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*.ba2;*.bsa", False).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim archivePriority = BuildArchivePriority(ba2Files, includeInactiveArchives, Fo4DataPath, loadedPlugins)
            Dim msArchiveEnum = swPhase.ElapsedMilliseconds
            Logger.LogLazy(Function() $"[FilesDictionary] archive enumerate + priority: {ba2Files.Count} archives in {msArchiveEnum} ms")
            swPhase.Restart()

            ' The queue is a BlockingCollection, not a plain ConcurrentQueue, because the loose WALK is now
            ' a PRODUCER that streams into it while the workers below are already draining it. Previously
            ' the walk had to finish and be fully materialized into a List before a single worker started,
            ' which meant the longest phase of the scan (a recursive traversal of a huge Data tree, through
            ' the USVFS hook under MO2) ran with every core idle and NOT ONE progress report emitted — the
            ' bar sat at 0% under the label "Mounting archives...", which is exactly the freeze users see.
            ' Now the walk overlaps the archive mounting and the loose insertion entirely.
            Dim workQueue As New BlockingCollection(Of DictionaryScanWorkItem)(New ConcurrentQueue(Of DictionaryScanWorkItem)())

            ' Sum the byte size of ONLY the archives we will actually index (those that passed the
            ' archivePriority filter and get enqueued below as IsArchive=True). This drives the
            ' byte-weighted Detail bar. A FileInfo.Length is a cheap stat; there are only tens-to-
            ' hundreds of archives. Wrap each in Try so a vanished file just contributes 0, no throw.
            Dim archiveBytesTotal As Long = 0
            Dim indexableArchiveCount As Integer = 0

            For Each ba2 In ba2Files
                Dim ba2Name = Path.GetFileName(ba2)
                Dim sourceOrder As Integer = Integer.MinValue
                If archivePriority.TryGetValue(ba2Name, sourceOrder) = False Then
                    ' Archive not in priority map = doesn't belong to any active plugin (and, if
                    ' includeInactiveArchives is False, doesn't belong to any inactive one either,
                    ' since BuildArchivePriority skips inactives in that mode). Skip indexing it
                    ' so an orphan/inactive .ba2 can't override vanilla paths.
                    Continue For
                End If

                Try
                    archiveBytesTotal += New FileInfo(ba2).Length
                Catch
                    ' File vanished between enumeration and stat — counts as 0, don't abort the scan.
                End Try

                indexableArchiveCount += 1
                workQueue.Add(New DictionaryScanWorkItem With {
                .IsArchive = True,
                .FilePath = ba2,
                .SourceOrder = sourceOrder
            })
            Next

            ' Reset and publish the byte total upfront. If there are no indexable archives (all loose),
            ' total is 0 and the (0,0) report + the consumer's b.Total>0 guard handle it gracefully.
            _archiveByteProgress = archiveByteProgress
            _archiveBytesDone = 0
            _archiveBytesTotal = archiveBytesTotal
            archiveByteProgress?.Report((0L, _archiveBytesTotal))

            ' ⛔ totalCount is now a MOVING target: it starts at the archives (the only thing we can count
            ' up front) and GROWS as the walk discovers loose files. Every consumer re-reads Max from each
            ' report and re-sets its bar's Maximum, so a growing Max is fine — the bar rubber-bands a little
            ' while the walk runs, which is a fair picture of "still discovering how much there is". What is
            ' NOT fine is reporting Max=0 to say "unknown": Wardrobe_Manager assigns Max to ProgressBar1
            ' .Maximum unconditionally, so a 0 there would blank its bar on every heartbeat.
            totalCount = indexableArchiveCount
            completed = 0
            Volatile.Write(_scanProductionComplete, False)
            ReportScan(progress, "Mounting archives…", 0, totalCount)

            ' Capped at 8 rather than ProcessorCount: on a cache MISS each archive worker opens a FileStream
            ' over a (possibly multi-GB) archive, and 16-32 concurrent big-archive reads on a spinning disk
            ' regress wall-clock instead of improving it. On a cache HIT no archive is opened at all (the
            ' .cac index is read instead) and the loose branch is pure CPU; both parallelize freely.
            Dim workerCount As Integer = Math.Min(8, Math.Max(1, Environment.ProcessorCount))

            Dim workers = Enumerable.Range(0, workerCount).
            Select(Function(funza)
                       Return Task.Run(
                           Sub()
                               ' Blocks while the queue is empty and the walk is still producing; ends
                               ' cleanly once CompleteAdding has been called AND the queue has drained.
                               For Each item In workQueue.GetConsumingEnumerable()
                                   If item.IsArchive Then
                                       ProcessBa2File(item.FilePath, item.SourceOrder, progress)
                                   Else
                                       ProcessLooseFile(item.FilePath, item.RelativePath, item.LooseLastWrite, progress)
                                   End If
                               Next
                           End Sub)
                   End Function).
            ToArray()

            ' --- Producer: the parallel loose walk, running CONCURRENTLY with the workers above. ---
            Dim looseCount As Integer = 0
            Try
                Dim walkDop As Integer = Math.Min(8, Math.Max(1, Environment.ProcessorCount))
                looseCount = Await Task.Run(
                    Function()
                        Return WalkLooseFilesParallel(Fo4DataPath, extensionsSnapshot, walkDop,
                            Sub(fullPath, relativePath, lastWrite)
                                Dim discovered = Interlocked.Increment(totalCount)
                                workQueue.Add(New DictionaryScanWorkItem With {
                                    .IsArchive = False,
                                    .FilePath = fullPath,
                                    .RelativePath = relativePath,
                                    .SourceOrder = Integer.MaxValue,
                                    .LooseLastWrite = lastWrite
                                })

                                ' Heartbeat so the walk is no longer a dark window. Throttled hard (every
                                ' 4096th file) for the same reason ProcessLooseFile's own reporting is:
                                ' each Report is a SynchronizationContext.Post the UI thread must drain.
                                If (discovered And WalkHeartbeatMask) = 0 Then
                                    Dim found = discovered - indexableArchiveCount
                                    ReportScan(progress,
                                               $"Scanning Data folder — {found:N0} loose files found…",
                                               Volatile.Read(completed), discovered)
                                End If
                            End Sub)
                    End Function).ConfigureAwait(False)
            Catch ex As Exception
                ' Swallow-and-record rather than rethrow: falling out of here without reaching the Finally
                ' would leave CompleteAdding uncalled and every worker blocked in GetConsumingEnumerable
                ' forever — a hang instead of a scan that came up short.
                _scanErrors.Enqueue("Loose file walk failed: " & ex.Message)
                Logger.LogLazy(Function() "[FilesDictionary] WalkLooseFilesParallel error: " & ex.ToString())
            Finally
                Volatile.Write(_scanProductionComplete, True)
                workQueue.CompleteAdding()
            End Try

            Dim msWalkAndScan = swPhase.ElapsedMilliseconds
            Await Task.WhenAll(workers).ConfigureAwait(False)

            ' Read the counters, not _scanReport: that's a queue the apps DRAIN, so it may still hold
            ' (or have already lost) items from an earlier scan.
            Dim hits = _archivesFromCache, missed = _archivesReindexed
            Dim msScan = swPhase.ElapsedMilliseconds
            Dim entryCount = _dictionary.Count
            Logger.LogLazy(Function() $"[FilesDictionary] walk+scan: {workerCount} workers, {looseCount} loose, {hits} cache-hit / {missed} re-indexed archives, {entryCount} entries in {msScan} ms (walk done at {msWalkAndScan} ms)")
            swPhase.Restart()

            ' ⛔ Acá vivía la fase "Building search index…" — una pasada COMPLETA sobre todas las claves del
            ' diccionario, corriendo con la barra ya en 100% y por eso reportada aparte para que no se leyera
            ' como un cuelgue. Esa fase YA NO EXISTE: los dos índices de búsqueda son lazy y sólo los
            ' consultan pickers y catálogos on-demand, ninguno en el arranque (ver _KeysByDirectoryExtension).
            ' Esto ahora sólo los deja en "sin construir"; el primer lector los puebla desde este mismo
            ' diccionario. Sin fase no hay nada que reportar — un label para trabajo que no ocurre es
            ' desinformación, no feedback. El timing de abajo (msIndex) se conserva: verlo caer a ~0 ms en
            ' LastScanDiagnostics es justamente la evidencia de que el diferido está funcionando.
            RebuildSearchIndexesFromDictionary()

            Dim msIndex = swPhase.ElapsedMilliseconds
            Logger.LogLazy(Function() $"[FilesDictionary] index rebuild: {msIndex} ms")

            ' Remove cache files for archives that no longer exist in the data root.
            CleanupOrphanCacheFiles(ba2Files)

            Dim msTotal = swTotal.ElapsedMilliseconds
            Logger.LogLazy(Function() $"[FilesDictionary] Fill_DictionaryAsync total: {msTotal} ms")

            ' In-memory only (three stopwatches and a string — no I/O, no log). A caller that wants to profile
            ' a rig can read it; NPC_Manager only persists it under --diagnoseLoad. ⛔ Deliberately NOT wired
            ' to Logger.Enabled: that flag also drives FaceGenBuilder.DebugMode, so using it as a profiling
            ' switch would silently change how FaceGen bakes.
            LastScanDiagnostics =
                $"archives={ba2Files.Count} (indexed={indexableArchiveCount}, cache-hit={hits}, re-indexed={missed}), " &
                $"loose={looseCount}, entries={entryCount}, workers={workerCount} | " &
                $"archive-enum={msArchiveEnum}ms walk+scan={msScan}ms (walk={msWalkAndScan}ms) index={msIndex}ms TOTAL={msTotal}ms"

        Catch ex As Exception
            ' No MsgBox desde acá: después del ConfigureAwait(False) estamos en el
            ' ThreadPool, sin sync context de la UI. MsgBox desde worker cuelga.
            _scanErrors.Enqueue("Fill_DictionaryAsync failed: " & ex.Message)
            Logger.LogLazy(Function() "[FilesDictionary] Fill_DictionaryAsync error: " & ex.ToString())
        End Try
    End Function
    Private Shared Function ArchiveBelongsToPlugin(archiveFileName As String, pluginFileName As String) As Boolean
        Dim archiveBase = Path.GetFileNameWithoutExtension(archiveFileName)
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        If archiveBase.Equals(pluginBase, StringComparison.OrdinalIgnoreCase) Then Return True
        If archiveBase.StartsWith(pluginBase & " - ", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    ''' <summary>Invierte <see cref="ArchiveBelongsToPlugin"/> en un lookup: nombre base de plugin -> archives
    ''' que le pertenecen, pre-ordenados OrdinalIgnoreCase.
    ''' <para>Por que: los grupos de prioridad preguntaban, por CADA plugin del load order, cuales de los
    ''' archives pendientes le pertenecian - un scan LINQ del set entero por plugin, con dos substrings por
    ''' comparacion. Miles de plugins contra cientos de archives son millones de comparaciones y de strings
    ''' descartables, y corre ANTES del primer reporte de progreso, con la barra ya inmovil.</para>
    ''' <para>El predicado es: base del archive == base del plugin, o empieza con base + " - ". O sea el
    ''' conjunto de bases que pueden reclamar un archive es el propio mas cada prefijo que termina justo antes
    ''' de un " - ": entre uno y tres, asi que se enumeran una vez por ARCHIVE. Mismos matches, mismo orden.</para></summary>
    Private Shared Function BuildPluginBaseToArchives(archiveNames As IEnumerable(Of String)) As Dictionary(Of String, List(Of String))
        Const Sep As String = " - "
        Dim map As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

        Dim add = Sub(pluginBase As String, archiveName As String)
                      Dim bucket As List(Of String) = Nothing
                      If Not map.TryGetValue(pluginBase, bucket) Then
                          bucket = New List(Of String)
                          map(pluginBase) = bucket
                      End If
                      bucket.Add(archiveName)
                  End Sub

        For Each name In archiveNames
            Dim archiveBase = Path.GetFileNameWithoutExtension(name)
            add(archiveBase, name)

            ' Every prefix that ends immediately before a " - " is a plugin base this archive would match
            ' via the StartsWith arm. E.g. "Foo - Main.ba2" → also claimable by plugin "Foo"; a pathological
            ' "A - B - C.ba2" → also by "A" and by "A - B", exactly as the predicate says.
            Dim at As Integer = archiveBase.IndexOf(Sep, StringComparison.Ordinal)
            While at > 0
                add(archiveBase.Substring(0, at), name)
                at = archiveBase.IndexOf(Sep, at + 1, StringComparison.Ordinal)
            End While
        Next

        For Each bucket In map.Values
            bucket.Sort(StringComparer.OrdinalIgnoreCase)
        Next
        Return map
    End Function

    ''' <summary>Assign the next SourceOrder values to the still-pending archives claimed by
    ''' <paramref name="pluginFileName"/>, in OrdinalIgnoreCase name order. Mirrors what the old
    ''' <c>pending.Where(ArchiveBelongsToPlugin).OrderBy(name)</c> loop did, off the prebuilt lookup.</summary>
    Private Shared Sub AssignArchivesOfPlugin(pluginFileName As String,
                                              byPluginBase As Dictionary(Of String, List(Of String)),
                                              pending As HashSet(Of String),
                                              result As Dictionary(Of String, Integer),
                                              ByRef nextOrder As Integer)
        Dim candidates As List(Of String) = Nothing
        If Not byPluginBase.TryGetValue(Path.GetFileNameWithoutExtension(pluginFileName), candidates) Then Exit Sub

        For Each match In candidates          ' already sorted OrdinalIgnoreCase
            If Not pending.Remove(match) Then Continue For   ' claimed by an earlier group/plugin
            result(match) = nextOrder
            nextOrder += 1
        Next
    End Sub

    ''' <summary>Mapa de prioridad SourceOrder de los archives. Valor mas alto gana el conflicto. De MENOR a
    ''' MAYOR: (0) archives huerfanos, que ningun plugin reclama - ordenes negativos, por debajo de todo: el
    ''' motor no los montaria, asi que no pueden tapar a un mod real (antes se asignaban ULTIMOS, o sea por
    ''' ENCIMA de todo plugin activo, y un .ba2 olvidado en Data\ le ganaba a vanilla); (1) archives de plugins
    ''' inactivos, solo con <paramref name="includeInactive"/>; (2) base + DLC; (3) plugins cargados, en load
    ''' order, que son los que ganan. Con includeInactive=False los de plugins no cargados quedan fuera del
    ''' resultado y el caller ni los indexa.</summary>
    Private Shared Function BuildArchivePriority(ba2Files As List(Of String),
                                                 includeInactive As Boolean,
                                                 dataPath As String,
                                                 Optional loadedPlugins As IEnumerable(Of String) = Nothing) As Dictionary(Of String, Integer)
        Dim result As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        ' "Loaded" = the caller's set if it gave one (NPC_Manager: the Preflight selection), else the engine's
        ' active load order. Both groups below — the loaded archives AND the inactive ones — must be derived from
        ' the SAME set, or an unticked plugin would land in neither and its archive would vanish silently.
        Dim loadedOrder As List(Of String) = If(loadedPlugins Is Nothing,
                                                PluginManager.ReadActiveLoadOrder(),
                                                loadedPlugins.Where(Function(p) Not String.IsNullOrEmpty(p)).ToList())

        Dim archiveNames = ba2Files.
        Select(Function(p) Path.GetFileName(p)).
        OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase).
        ToList()

        Dim fullPathsByName = ba2Files.
        GroupBy(Function(p) Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).
        ToDictionary(Function(g) g.Key, Function(g) g.First(), StringComparer.OrdinalIgnoreCase)

        Dim pending As New HashSet(Of String)(archiveNames, StringComparer.OrdinalIgnoreCase)
        Dim nextOrder As Integer = 0

        ' Built ONCE and shared by the two plugin-driven groups below (see BuildPluginBaseToArchives).
        Dim byPluginBase = BuildPluginBaseToArchives(archiveNames)

        ' Group 1: archives of inactive plugins. Only included in the map if the caller asked for
        ' it (WM mode). Order within this group: loadorder.txt if present (skipping anything that
        ' is in the active set), alphabetical fallback for anything on disk that loadorder.txt
        ' didn't list. Inactives are processed FIRST so they get the lowest SourceOrder — every
        ' active plugin's archive (and loose files) wins on conflict.
        If includeInactive Then
            For Each plugin In EnumerateInactivePlugins(dataPath, loadedOrder)
                AssignArchivesOfPlugin(plugin, byPluginBase, pending, result, nextOrder)
            Next
        End If

        ' Group 2: implicit base + DLC archives (always loaded by the engine regardless of any
        ' Plugins.txt / loadorder.txt state; matched by archive name prefix).
        Dim baseAndDlcOrder As String() = {
        "Fallout4",
        "DLCRobot",
        "DLCworkshop01",
        "DLCCoast",
        "DLCworkshop02",
        "DLCworkshop03",
        "DLCNukaWorld",
        "DLCUltraHighResolution"
    }

        For Each prefix In baseAndDlcOrder
            Dim matches = pending.
            Where(Function(name) Path.GetFileNameWithoutExtension(name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            For Each match In matches
                result(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        ' Group 3: archives of the LOADED plugins, in load order (loadedOrder above). When the caller passes
        ' nothing this is PluginManager's canonical active load order (single source of truth: implicit DLCs,
        ' Creation Club entries from Fallout4.ccc/Skyrim.ccc, and Plugins.txt actives). Pre-2026-05-01 this read
        ' loadorder.txt directly via a duplicated parser that missed CC content entirely, leaving cc*.ba2 at
        ' fallback (mtime) priority — wrong against the engine.
        For Each plugin In loadedOrder
            AssignArchivesOfPlugin(plugin, byPluginBase, pending, result, nextOrder)
        Next

        ' Orphans. Archives in Data\ that no plugin claims. Only indexed when the caller wants to see
        ' inactive content (WM); in NPC mode (engine parity) an orphan archive isn't indexed at all — same
        ' as the engine ignoring it.
        ' They are identified LAST (whatever no plugin claimed) but must rank LOWEST: nothing mounts them
        ' in-game, so a leftover .ba2 must not shadow vanilla or an active mod. Every plugin-claimed archive
        ' above got an order >= 0, so orphans take NEGATIVE orders — still ordered among themselves by mtime.
        If includeInactive Then
            Dim fallbackMatches = pending.
            OrderBy(Function(name) File.GetLastWriteTimeUtc(fullPathsByName(name))).
            ThenBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim orphanOrder As Integer = -fallbackMatches.Count
            For Each match In fallbackMatches
                result(match) = orphanOrder
                orphanOrder += 1
                pending.Remove(match)
            Next
        End If

        Return result
    End Function

    ''' <summary>Enumerate plugins on disk in <paramref name="dataPath"/> that are NOT loaded this session
    ''' (<paramref name="loadedOrder"/> — the caller's loaded set, i.e. the active load order unless the app
    ''' passed its own selection). Order: loadorder.txt order for entries it lists (filtered to the not-loaded
    ''' ones present on disk), then alphabetical for anything on disk that loadorder.txt didn't list.</summary>
    Private Shared Function EnumerateInactivePlugins(dataPath As String, loadedOrder As List(Of String)) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrEmpty(dataPath) OrElse Not Directory.Exists(dataPath) Then Return result

        Dim active = New HashSet(Of String)(loadedOrder, StringComparer.OrdinalIgnoreCase)

        Dim diskPlugins As New List(Of String)
        For Each ext In {"*.esp", "*.esm", "*.esl"}
            For Each fp In Directory.EnumerateFiles(dataPath, ext, SearchOption.TopDirectoryOnly)
                diskPlugins.Add(Path.GetFileName(fp))
            Next
        Next
        Dim diskSet = New HashSet(Of String)(diskPlugins, StringComparer.OrdinalIgnoreCase)

        ' loadorder.txt vive al lado del Plugins.txt vigente, que lo decide GamePathsResolver (variante del
        ' exe + tabla de nombres verificados, o la ruta que fijó el usuario). Devuelve "" cuando no se pudo
        ' resolver: ahí no se arma ninguna ruta — Path.Combine("", "loadorder.txt") daría una ruta RELATIVA
        ' contra el directorio de trabajo. Sin este archivo los plugins inactivos se ordenan alfabéticamente,
        ' que es el fallback de abajo.
        Dim loadorderDir = PluginManager.ResolveGameAppDataDir()
        Dim loadorderTxt = If(loadorderDir = "", "", Path.Combine(loadorderDir, "loadorder.txt"))

        Dim emitted As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If loadorderTxt <> "" AndAlso File.Exists(loadorderTxt) Then
            For Each line In File.ReadAllLines(loadorderTxt, Encoding.UTF8)
                Dim trimmed = line.Trim()
                If trimmed.Length = 0 Then Continue For
                If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                If trimmed.StartsWith("*") Then trimmed = trimmed.Substring(1).Trim()
                If trimmed.Length = 0 Then Continue For
                If active.Contains(trimmed) Then Continue For
                If Not diskSet.Contains(trimmed) Then Continue For
                If emitted.Add(trimmed) Then result.Add(trimmed)
            Next
        End If

        Dim leftovers = diskPlugins.
            Where(Function(p) Not active.Contains(p) AndAlso Not emitted.Contains(p)).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase)
        For Each p In leftovers
            If emitted.Add(p) Then result.Add(p)
        Next

        Return result
    End Function
    Private Shared Sub ProcessBa2File(ba2 As String,
                                      sourceOrder As Integer,
                                      progress As IProgress(Of (String, Integer, Integer)),
                                      Optional addedKeys As ConcurrentBag(Of String) = Nothing)
        ' Declared at method scope with a safe default so the Finally can attribute this archive's
        ' bytes even if the FileInfo below throws (vanished file). Assigned once we have the FileInfo.
        Dim ba2Size As Long = 0
        Try
            ' O5.4: Intern the BA2 filename since it is stored in many File_Location instances
            Dim ba2FileName = String.Intern(Path.GetFileName(ba2))
            Dim fi As New FileInfo(ba2)
            ba2Size = fi.Length
            Dim ba2DateLocal As Date = fi.LastWriteTime   ' preserved for File_Location.FileDate
            Dim ba2DateUtc As Date = fi.LastWriteTimeUtc  ' cache signature component
            Dim extsCanonical = _canonicalExtensionsSnapshot
            Dim cachePath = GetCacheFilePath(ba2FileName)
            ' Sello de generacion para TODAS las entradas que este archive produzca en esta pasada. Se lee
            ' UNA vez, izado fuera de los dos loops. Ver File_Location.ArchiveGen.
            ' ⛔ `GetOrAdd`, no `ContentGenOf`: hace falta que la CLAVE exista aunque el valor sea 0, porque
            ' el bump masivo del re-scan recorre las claves de este diccionario. Con `TryGetValue` un archive
            ' que nunca se desmonto no tendria clave, el re-scan no lo bumpearia, y un `File_Location` viejo
            ' seguiria matcheando en 0 contra entradas re-derivadas — que es justo el caso "el .ba2 cambio
            ' entre dos scans" (o sea, lo que hacen WM Pack y el packer de FaceGen).
            Dim genArchive = _archiveContentGen.GetOrAdd(ArchiveKey(ba2), 0)

            ' Cache hit: populate dict from index without opening the archive.
            Dim cachedEntries As List(Of CachedEntry) = Nothing
            If extsCanonical IsNot Nothing AndAlso
               TryLoadArchiveIndex(cachePath, ba2Size, ba2DateUtc, extsCanonical, cachedEntries) Then
                For Each ce In cachedEntries
                    Dim standardized = PoolPath(ce.FullPath)
                    Dim entry As New File_Location With {
                        .BA2File = ba2FileName,
                        .Index = ce.Index,
                        .FullPath = standardized,
                        .SourceOrder = sourceOrder,
                        .FileDate = ba2DateLocal,
                        .ArchiveGen = genArchive
                    }
                    AddEntryResolvingConflict(standardized, entry)
                    addedKeys?.Add(standardized)
                Next
                _scanReport.Enqueue((ba2FileName, True))
                Interlocked.Increment(_archivesFromCache)
                Return
            End If

            ' Cache miss: open the archive, filter entries by SupportedExtensions,
            ' populate dict and collect for cache write.
            Dim collected As List(Of CachedEntry) = Nothing
            If extsCanonical IsNot Nothing AndAlso IsCacheEnabled() Then
                collected = New List(Of CachedEntry)
            End If

            ' Mismo share mode que el resto de las lecturas de archive: el scan puede correr mientras un
            ' packager reescribe otro .ba2 del set. Ver AbrirArchiveParaLectura.
            Using fs As FileStream = AbrirArchiveParaLectura(ba2)
                Using arc As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                    For Each fil In arc.EntriesFiles
                        Dim rawPath = fil.FullPath.Correct_Path_Separator
                        Dim extKey = NormalizeExtensionKey(IO.Path.GetExtension(rawPath))
                        If extKey = "" OrElse Not SupportedExtensions.Contains(extKey) Then Continue For

                        ' O5.4: De-dupe the standardized path — stored long-term as dictionary key and File_Location.FullPath
                        Dim standardized = PoolPath(rawPath)
                        Dim entry As New File_Location With {
                            .BA2File = ba2FileName,
                            .Index = fil.Index,
                            .FullPath = standardized,
                            .SourceOrder = sourceOrder,
                            .FileDate = ba2DateLocal,
                            .ArchiveGen = genArchive
                        }

                        ' O1.3: During scan, only populate _dictionary; indexes are built in batch after scan
                        AddEntryResolvingConflict(standardized, entry)
                        addedKeys?.Add(standardized)

                        collected?.Add(New CachedEntry With {.Index = fil.Index, .FullPath = standardized})
                    Next
                End Using
            End Using
            _scanReport.Enqueue((ba2FileName, False))
            Interlocked.Increment(_archivesReindexed)

            If collected IsNot Nothing Then
                Try
                    SaveArchiveIndex(cachePath, ba2Size, ba2DateUtc, extsCanonical, collected)
                Catch ex As Exception
                    _scanErrors.Enqueue("Error saving cache for " & ba2FileName & ": " & ex.Message)
                End Try
            End If

        Catch ex As Exception
            _scanErrors.Enqueue("Error processing BA2 " & ba2 & ": " & ex.Message)
            Logger.LogLazy(Function() "[FilesDictionary] ProcessBa2File error: " & ex.ToString())
        Finally
            Dim current = Interlocked.Increment(completed)
            ReportScan(progress, $"Indexed: {Path.GetFileName(ba2)}", current, totalCount)

            ' Byte-weighted Detail bar (archives only). _archiveByteProgress is a Progress(Of T)
            ' created on the UI thread, so Report marshals back safely from this worker.
            If _archiveByteProgress IsNot Nothing Then
                Dim bd = Interlocked.Add(_archiveBytesDone, ba2Size)
                _archiveByteProgress.Report((bd, _archiveBytesTotal))
            End If
        End Try
    End Sub

    Public Shared Function EnumerateFilesWithSymlinkSupport(root As String, pattern As String, Recursive As Boolean) As IEnumerable(Of String)
        Dim spl() As String = {pattern}
        If pattern.Contains(";"c) Then
            spl = pattern.Split(";"c)
        End If
        Dim result As IEnumerable(Of String) = Enumerable.Empty(Of String)()
        Dim opts As New EnumerationOptions() With {.RecurseSubdirectories = Recursive}

        For Each pat In spl
            result = result.Concat(Directory.EnumerateFiles(root, pat, opts))
        Next
        Return result
    End Function

    ''' <param name="relativePath">Path relative to the data root, computed by the walk. It used to be
    ''' derived here with <c>Path.GetRelativePath(basePath, file)</c>, which re-normalizes both operands on
    ''' every call — once per loose file. The walk already knows the root, so it just cuts the prefix.</param>
    Private Shared Sub ProcessLooseFile(file As String, relativePath As String, lastWrite As Date, progress As IProgress(Of (String, Integer, Integer)))
        Try
            ' O5.4: De-dupe the standardized path — stored long-term as dictionary key and File_Location.FullPath
            Dim standardized = PoolPath(relativePath.Correct_Path_Separator)

            Dim entry As New File_Location With {
            .BA2File = String.Empty,
            .Index = -1,
            .FullPath = standardized,
            .SourceOrder = Integer.MaxValue,
            .FileDate = lastWrite
        }

            ' O1.3: During scan, only populate _dictionary; indexes are built in batch after scan
            AddEntryResolvingConflict(standardized, entry)

        Catch ex As Exception
            _scanErrors.Enqueue("Error processing loose file " & file & ": " & ex.Message)
            Logger.LogLazy(Function() "[FilesDictionary] ProcessLooseFile error: " & ex.ToString())
        Finally
            ' Throttled — see LooseProgressReportMask. The genuinely-last item always reports, so consumers
            ' that don't clamp the bar to Max still finish full; the _scanProductionComplete gate is what
            ' keeps "last item" from meaning "workers momentarily caught up with the walk" (see that field).
            Dim current = Interlocked.Increment(completed)
            If (current And LooseProgressReportMask) = 0 OrElse
               (Volatile.Read(_scanProductionComplete) AndAlso current >= totalCount) Then
                ReportScan(progress, $"Indexed: {Path.GetFileName(file)}", current, totalCount)
            End If
        End Try
    End Sub

    Private Shared Function Resolve_Conflict(Original As File_Location, Nueva As File_Location) As Boolean
        If IsNothing(Original) Then Return True
        If IsNothing(Nueva) Then Return False

        If Nueva.IsLosseFile AndAlso Original.IsLosseFile = False Then Return True
        If Original.IsLosseFile AndAlso Nueva.IsLosseFile = False Then Return False

        If Nueva.SourceOrder > Original.SourceOrder Then Return True
        If Nueva.SourceOrder < Original.SourceOrder Then Return False

        If Nueva.IsLosseFile AndAlso Original.IsLosseFile Then
            Return False
        End If

        Return StringComparer.OrdinalIgnoreCase.Compare(Nueva.BA2File, Original.BA2File) >= 0
    End Function

End Class
