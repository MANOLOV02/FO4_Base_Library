' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' HkxObjectGraph_Class: infraestructura de parsing del grafo de objetos HKX (objetos por
' virtual-fixup, lectura de campos, hkArray, resolución de punteros local/global).
' La usan SkeletonClothOverlayHelper, HkxPoseImportHelper, los Hkx*/Hcl*GraphParser y
' HclClothPackageParser.
'
' ALCANCE: el soporte genérico 32/64-bit cubre hkArray, root container, skeleton y
' animation/binding.
'
' ⛔ ESTE ARCHIVO ES EL SUSTRATO, NO UN PARSER DE CLASES. No conoce un solo campo de una clase
' Havok: expone las primitivas que el codigo generado usa para leerlas (enteros, floats, strings,
' cabeceras de array, punteros resueltos por fixup). Quien sabe que campo hay y en que offset es
' la tabla de la reflexion (`HavokLayout*.vb`) y quien la lee es `HavokTyped.vb`.
' =============================================================================

Imports System.IO
Imports System.Linq
Imports System.Text

Public NotInheritable Class HkxObjectGraphParser_Class
    Public Shared Function BuildGraph(packfile As HkxPackfile_Class) As HkxObjectGraph_Class
        Return New HkxObjectGraph_Class(packfile)
    End Function
End Class

Partial Public Class HkxObjectGraph_Class
    Public ReadOnly Property Packfile As HkxPackfile_Class
    Public ReadOnly Property ContentsSection As HkxPackfileSection_Class
    Public ReadOnly Property Objects As New List(Of HkxVirtualObjectGraph_Class)

    Private ReadOnly _localFixupsBySource As New Dictionary(Of Integer, HkxLocalFixupEntry_Class)
    Private ReadOnly _globalFixupsBySource As New Dictionary(Of Integer, HkxGlobalFixupEntry_Class)
    Private ReadOnly _objectsByOffset As New Dictionary(Of Integer, HkxVirtualObjectGraph_Class)
    Private ReadOnly _objectsByClassName As New Dictionary(Of String, List(Of HkxVirtualObjectGraph_Class))(StringComparer.OrdinalIgnoreCase)

    ' Fixups de la contents-section ordenados por SourceRelativeOffset ascendente (los empates
    ' conservan el orden de enumeración original), en arrays paralelos. Se arman una vez en
    ' BuildIndices para que GetLocal/GlobalFixupsInRange hagan búsqueda binaria en vez de
    ' filtrar + ordenar la lista completa en cada llamada.
    Private _localFixupSourcesSorted As Integer() = Array.Empty(Of Integer)()
    Private _localFixupsSorted As HkxLocalFixupEntry_Class() = Array.Empty(Of HkxLocalFixupEntry_Class)()
    Private _globalFixupSourcesSorted As Integer() = Array.Empty(Of Integer)()
    Private _globalFixupsSorted As HkxGlobalFixupEntry_Class() = Array.Empty(Of HkxGlobalFixupEntry_Class)()

    ' ⛔⛔ EL GRAFO SE ANCLA SOLO. Antes leia `RawBytes(ContentsSection.AbsoluteDataStart + rel)`
    ' y sacaba el juego de `Packfile.Header.PackfileFormat` — las DOS son salidas de parsear la
    ' cabecera, asi que el lector generado no podia leer el envoltorio: para arrancar necesitaba
    ' justo lo que el envoltorio produce. Con anclaje y formato propios esa circularidad no existe:
    ' el grafo de ARRANQUE se ancla en el byte 0 y `Hk_HkPackfileHeader` lee la cabecera como
    ' cualquier otra clase que la reflexion declara.
    ''' <summary>
    ''' ⛔⛔ EL CENSO POR BYTE, MARCADO POR EL PROPIO LECTOR.
    ''' <para>La cobertura que habia contaba MIEMBROS DECLARADOS de las clases que aparecen. Eso
    ''' deja afuera todo lo que nadie lee: el relleno entre miembros, los huecos entre bloques, las
    ''' secciones que no son `__data__` y los bloques de clases que ningun `.exe` declara.</para>
    ''' <para>⛔ NO SE TRANSCRIBE NINGUNA LEY DE ANCHOS. Se marca en `EnsureReadable`, que es el
    ''' embudo por el que pasa TODA lectura declarando offset y largo, mas el recorrido de strings,
    ''' que camina bytes por su cuenta. Lo que quede sin marcar es lo que nadie leyo.</para>
    ''' <para>Apagado por defecto, como `HavokLayout.RecordCoverage`: prenderlo cuesta una marca
    ''' por lectura sobre 148.354 objetos.</para>
    ''' </summary>
    Public Shared Property RegistrarBytes As Boolean = False

    Private _tocados As Boolean()
    Private _ranuras As Boolean()

    ''' <summary>El byte del archivo en una posicion ABSOLUTA. Solo para el censo por byte.</summary>
    Public Function RawEnAbsoluto(i As Integer) As Byte
        If _bytes Is Nothing OrElse i < 0 OrElse i >= _bytes.Length Then Return 0
        Return _bytes(i)
    End Function

    ''' <summary>Los bytes del ARCHIVO que alguna lectura toco, o Nothing si no se registro.</summary>
    Public Function BytesTocados() As Boolean()
        Return _tocados
    End Function

    ''' <summary>
    ''' ⛔⛔ LAS RANURAS DE REUBICACION QUE EL LECTOR TOCO. Solo para el censo por byte.
    ''' <para>Una ranura de puntero se escribe en CERO en el archivo y su valor lo pone el FIXUP
    ''' al cargar. Ni el motor las lee: las parchea. Contarlas como "dato que la app no lee" es
    ''' contar un hueco que no existe.</para>
    ''' <para>Se marcan ACA y no en el instrumento porque el instrumento solo podia derivarlas de
    ''' las tablas de fixups, y asi se le escapaban las dos mitades que importan: la ranura NULA
    ''' (no tiene entrada en ninguna tabla) y la que vive adentro del elemento i de un arreglo.
    ''' El lector pasa por TODAS, porque para eso las resuelve.</para>
    ''' </summary>
    Public Function BytesDeRanura() As Boolean()
        Return _ranuras
    End Function

    ''' <summary>El largo del archivo entero, para poder dividir.</summary>
    Public ReadOnly Property BytesTotales As Integer
        Get
            Return If(_bytes Is Nothing, 0, _bytes.Length)
        End Get
    End Property

    ''' <summary>Una ranura de reubicacion de `PointerSize` bytes, en offset RELATIVO.</summary>
    Private Sub MarcarRanura(relativeOffset As Integer)
        If _ranuras Is Nothing Then Exit Sub
        Dim i0 = Math.Max(0, _ancla + relativeOffset)
        Dim i1 = Math.Min(_ancla + relativeOffset + PointerSizeValue, _ranuras.Length)
        For i = i0 To i1 - 1
            _ranuras(i) = True
        Next
    End Sub

    Private Sub Marcar(absoluteStart As Integer, byteCount As Integer)
        If _tocados Is Nothing OrElse byteCount <= 0 Then Exit Sub
        Dim i0 = Math.Max(0, absoluteStart)
        Dim i1 = Math.Min(absoluteStart + byteCount, _tocados.Length)
        For i = i0 To i1 - 1
            _tocados(i) = True
        Next
    End Sub

    Private ReadOnly _bytes As Byte()
    Private ReadOnly _ancla As Integer
    Private ReadOnly _fin As Integer
    Private ReadOnly _pointerSize As Integer

    ''' <summary>Que tabla de la reflexion aplica. Lo declara el propio formato del archivo.</summary>
    Public ReadOnly Property Formato As HkxPackfileFormat_Enum

    ''' <summary>El ancho de puntero que declara el ARCHIVO en `layoutRules[0]`.</summary>
    Public ReadOnly Property AnchoDePuntero As Integer
        Get
            Return _pointerSize
        End Get
    End Property

    Private ReadOnly Property PointerSizeValue As Integer
        Get
            Return _pointerSize
        End Get
    End Property

    Public Sub New(packfile As HkxPackfile_Class)
        If IsNothing(packfile) Then Throw New ArgumentNullException(NameOf(packfile))
        ' `Header` es un Structure: `IsNothing` sobre un tipo por valor da SIEMPRE False y el guard
        ' quedaba mudo. `IsValid` es la pregunta de verdad: hay grafo y hay tabla para ese juego.
        ' ⛔ LA PREGUNTA ES `Raw.IsValid`, NO `Is Nothing`. `HkObj_*.ReadAt` solo devuelve Nothing si
        ' el grafo es Nothing o el offset es negativo, y `Parse` siempre le pasa un grafo recien
        ' construido y 0: `Header Is Nothing` era un guard que no podia dispararse nunca. `IsValid`
        ' pregunta lo que importa — hay grafo Y hay tabla para ese juego — y da False justo en el caso
        ' que hay que ver: un packfile sin tabla de reflexion (Skyrim32).
        If packfile.Header Is Nothing OrElse Not packfile.Header.Raw.IsValid Then
            Throw New InvalidOperationException("The HKX packfile has not been parsed, or there is no reflection table for its format.")
        End If

        Me.Packfile = packfile
        Me.ContentsSection = packfile.GetSection(packfile.Header.ContentsSectionIndex)
        If IsNothing(Me.ContentsSection) Then Throw New InvalidOperationException("The HKX contents section was not found.")

        _bytes = packfile.RawBytes
        If RegistrarBytes AndAlso _bytes IsNot Nothing Then
            ReDim _tocados(_bytes.Length - 1)
            ReDim _ranuras(_bytes.Length - 1)
        End If
        _ancla = ContentsSection.AbsoluteDataStart
        _fin = ContentsSection.DataEndAbsolute
        ' `pointerSize` es `layoutRules[0]`, y el formato lo derivo el envoltorio: ninguno de los dos
        ' es un campo aparte.
        _pointerSize = Math.Max(1, packfile.Header.LayoutRules(0))
        Me.Formato = packfile.Formato

        BuildIndices()
    End Sub

    ''' <summary>
    ''' ⛔ EL GRAFO DE ARRANQUE: anclado en el byte 0 del archivo, sin secciones ni fixups.
    ''' <para>Existe para UNA cosa: que `Hk_HkPackfileHeader` y `Hk_HkPackfileSectionHeader` puedan
    ''' leer el envoltorio con la misma tabla que todo lo demas. Los offsets relativos son, aca,
    ''' absolutos. No tiene objetos: la lista de objetos sale de los virtual-fixups, que es
    ''' precisamente lo que todavia no se leyo.</para>
    ''' </summary>
    Friend Sub New(bytes As Byte(), formato As HkxPackfileFormat_Enum, pointerSize As Integer)
        If IsNothing(bytes) Then Throw New ArgumentNullException(NameOf(bytes))
        _bytes = bytes
        _ancla = 0
        _fin = bytes.Length
        _pointerSize = Math.Max(1, pointerSize)
        Me.Formato = formato
    End Sub

    Private Sub BuildIndices()
        ' ⛔ UN SOLO BARRIDO POR LISTA. Antes se filtraba cuatro veces sobre las dos listas completas:
        ' aca por el diccionario y otra vez adentro de `BuildSortedFixupIndices`.
        Dim sec = Packfile.Header.ContentsSectionIndex
        Dim locales = Packfile.LocalFixups.Where(Function(pf) pf.SectionIndex = sec).ToList()
        Dim globales = Packfile.GlobalFixups.Where(Function(pf) pf.SectionIndex = sec).ToList()
        For Each fixup In locales
            _localFixupsBySource.TryAdd(fixup.SourceRelativeOffset, fixup)
        Next
        For Each fixup In globales
            _globalFixupsBySource.TryAdd(fixup.SourceRelativeOffset, fixup)
        Next

        OrdenarFixups(locales, Function(x) x.SourceRelativeOffset, _localFixupsSorted, _localFixupSourcesSorted)
        OrdenarFixups(globales, Function(x) x.SourceRelativeOffset, _globalFixupsSorted, _globalFixupSourcesSorted)

        Dim dataRelativeEnd = ContentsSection.DataEndAbsolute - ContentsSection.AbsoluteDataStart
        Dim orderedVirtualFixups = Packfile.VirtualFixups.
            Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).
            OrderBy(Function(pf) pf.ObjectRelativeOffset).
            ToList()

        For i = 0 To orderedVirtualFixups.Count - 1
            Dim fixup = orderedVirtualFixups(i)
            Dim classEntry = Packfile.GetClassName(fixup.ClassNameSectionIndex, fixup.ClassNameRelativeOffset)
            Dim size = If(i < orderedVirtualFixups.Count - 1,
                          orderedVirtualFixups(i + 1).ObjectRelativeOffset - fixup.ObjectRelativeOffset,
                          dataRelativeEnd - fixup.ObjectRelativeOffset)

            Dim obj As New HkxVirtualObjectGraph_Class With {
                .SectionIndex = fixup.SectionIndex,
                .RelativeOffset = fixup.ObjectRelativeOffset,
                .ClassName = If(classEntry?.Name, String.Empty),
                .Size = size
            }

            Objects.Add(obj)
            _objectsByOffset(obj.RelativeOffset) = obj

            Dim value As List(Of HkxVirtualObjectGraph_Class) = Nothing
            If Not _objectsByClassName.TryGetValue(obj.ClassName, value) Then
                value = New List(Of HkxVirtualObjectGraph_Class)
                _objectsByClassName.Add(obj.ClassName, value)
            End If

            value.Add(obj)
        Next
    End Sub

    ''' <summary>
    ''' ⛔ EL ORDENADO DE FIXUPS, UNA SOLA VEZ. Estaban los dos bloques escritos verbatim (local y
    ''' global), 13 lineas cada uno.
    ''' <para>El desempate por indice de enumeracion original es OBLIGATORIO: sin el, el orden dentro
    ''' de un rango deja de ser estable y los parsers que leen "el primer fixup del rango" cambian de
    ''' resultado entre corridas.</para>
    ''' </summary>
    Private Shared Sub OrdenarFixups(Of T)(lista As List(Of T), clave As Func(Of T, Integer),
                                           ByRef ordenados As T(), ByRef fuentes As Integer())
        Dim n = lista.Count
        Dim idx = Enumerable.Range(0, n).ToArray()
        Array.Sort(idx, Function(a, c)
                            Dim d = clave(lista(a)).CompareTo(clave(lista(c)))
                            If d <> 0 Then Return d
                            Return a.CompareTo(c)
                        End Function)
        ordenados = New T(n - 1) {}
        fuentes = New Integer(n - 1) {}
        For i = 0 To n - 1
            ordenados(i) = lista(idx(i))
            fuentes(i) = clave(ordenados(i))
        Next
    End Sub

    ' First index in the ascending-sorted array whose value is >= target (lower bound).
    ' Returns sources.Length if every value is below target.
    Private Shared Function LowerBound(sources As Integer(), target As Integer) As Integer
        Dim low = 0
        Dim high = sources.Length
        While low < high
            Dim mid = low + ((high - low) \ 2)
            If sources(mid) < target Then
                low = mid + 1
            Else
                high = mid
            End If
        End While
        Return low
    End Function

    Public Function GetObject(relativeOffset As Integer) As HkxVirtualObjectGraph_Class
        Dim value As HkxVirtualObjectGraph_Class = Nothing
        If _objectsByOffset.TryGetValue(relativeOffset, value) Then Return value
        Return Nothing
    End Function

    Public Function GetObjectsByClassName(className As String) As IEnumerable(Of HkxVirtualObjectGraph_Class)
        If String.IsNullOrWhiteSpace(className) Then Return Enumerable.Empty(Of HkxVirtualObjectGraph_Class)()
        Dim values As List(Of HkxVirtualObjectGraph_Class) = Nothing
        If _objectsByClassName.TryGetValue(className, values) Then Return values
        Return Enumerable.Empty(Of HkxVirtualObjectGraph_Class)()
    End Function

    ' ⛔ `Math.Max(0, ...)` NO ES DECORACION. `Raw.XCount` devuelve el `Count` CRUDO de la
    ' cabecera del `hkArray` (`ReadArrayHeader` no lo acota; el unico sitio que lo guarda con
    ' `<= 0` es `ReadObjectReferenceArray`). El bucle viejo `For i = 0 To n - 1` con `n` negativo
    ' simplemente NO ITERABA y caia al respaldo por clase, que es lo que recupera los esqueletos
    ' sueltos. `Enumerable.Range` valida el conteo AL CONSTRUIRSE y tira
    ' `ArgumentOutOfRangeException` desde adentro de esta funcion, antes del respaldo: se llevaria
    ' puestas TODAS las animaciones del archivo, y en `SkeletonInstance` el `Catch` devuelve 0 y el
    ' bake sale con el esqueleto del NIF pelado. Es tolerancia que el reemplazo perdio.
    ''' <summary>
    ''' ⛔⛔ LO QUE EL CONTENEDOR DECLARA, EN EL ORDEN EN QUE LO DECLARA.
    ''' <para>`hkaAnimationContainer` declara `animations`, `bindings` y `skeletons`. El archivo DICE
    ''' cuales son y en que orden; barrer el packfile por clase y quedarse con el primero es tirar una
    ''' moneda cuando hay dos.</para>
    ''' <para>⛔ SE ACOTA CON EL CONTEO QUE DECLARA LA CABECERA (`Raw.XCount` + `Raw.XRef(i)`), no con
    ''' el largo de la lista materializada: la propiedad generada COMPACTA —descarta el que no pudo
    ''' leer— asi que su `.Count` puede ser MENOR que lo que el archivo declara, y acotar con el corta
    ''' la cola en silencio.</para>
    ''' <para>⛔ DEVUELVE BLOQUES, NO OBJETOS. Los tres arreglos estan declarados con la clase BASE
    ''' (`hkaAnimation`, `hkaSkeleton`) y el archivo pone la SUBCLASE: el `ClassName` del bloque es lo
    ''' unico que la dice. Materializar aca a la clase base la perderia.</para>
    ''' <para>⛔ HABIA UN `Select Case` SOBRE UN ENUM DE LA APP —`CampoDelContenedor.Animations`— para
    ''' elegir cual de los tres leer: una etiqueta de la app en vez del miembro que declara el motor.
    ''' Los tres miembros son ahora tres accesores con SU nombre, y esta ley vive UNA vez.</para>
    ''' </summary>
    Private Function DelContenedor(refs As Func(Of Havok.Canon.Objects.HkObj_HkaAnimationContainer, IEnumerable(Of HkxVirtualObjectGraph_Class)),
                                   claseSuelta As String()) As List(Of HkxVirtualObjectGraph_Class)
        Dim r As New List(Of HkxVirtualObjectGraph_Class)
        For Each c In GetObjectsByClassName(Havok.Canon.Objects.HkObj_HkaAnimationContainer.NombreDeClase)
            Dim cont = Havok.Canon.Objects.HkObj_HkaAnimationContainer.Leer(Me, c)
            If cont Is Nothing Then Continue For
            For Each b In refs(cont)
                If b IsNot Nothing Then r.Add(b)
            Next
        Next
        If r.Count > 0 Then Return r

        ' ⛔ EL RESPALDO ES POR AUSENCIA DE RESULTADO, no por ausencia del contenedor. La
        ' condicion es `r.Count > 0`, asi que tambien cae aca un archivo que SI trae contenedor pero
        ' con ese arreglo VACIO. Es el comportamiento que el arbol tiene desde siempre y el que los
        ' `.hkx` de esqueleto suelto necesitan —son un `hkaSkeleton` y nada mas, sin contenedor—,
        ' pero decirlo como 'ausencia del contenedor' hace que el proximo lector descarte el otro
        ' caso sin mirarlo.
        For Each cn In claseSuelta
            r.AddRange(GetObjectsByClassName(cn).OrderBy(Function(x) x.RelativeOffset))
        Next
        Return r
    End Function

    ''' <summary>Los bloques de `hkaAnimationContainer.animations`, o los sueltos si no hay contenedor.</summary>
    Public Function AnimacionesDeclaradas(claseSuelta As String()) As List(Of HkxVirtualObjectGraph_Class)
        Return DelContenedor(Function(c) Enumerable.Range(0, Math.Max(0, c.Raw.AnimationsCount)).
                                                    Select(Function(i) c.Raw.AnimationsRef(i)), claseSuelta)
    End Function

    ''' <summary>Los bloques de `hkaAnimationContainer.bindings`, o los sueltos si no hay contenedor.</summary>
    Public Function BindingsDeclarados(claseSuelta As String()) As List(Of HkxVirtualObjectGraph_Class)
        Return DelContenedor(Function(c) Enumerable.Range(0, Math.Max(0, c.Raw.BindingsCount)).
                                                    Select(Function(i) c.Raw.BindingsRef(i)), claseSuelta)
    End Function

    ''' <summary>Los bloques de `hkaAnimationContainer.skeletons`, o los sueltos si no hay contenedor.</summary>
    Public Function EsqueletosDeclarados(claseSuelta As String()) As List(Of HkxVirtualObjectGraph_Class)
        Return DelContenedor(Function(c) Enumerable.Range(0, Math.Max(0, c.Raw.SkeletonsCount)).
                                                    Select(Function(i) c.Raw.SkeletonsRef(i)), claseSuelta)
    End Function

    ''' <summary>
    ''' ⛔⛔ LOS ESQUELETOS QUE EL ARCHIVO DECLARA, EN EL ORDEN QUE LOS DECLARA.
    ''' <para>`hkaAnimationContainer` declara `skeletons` como `array of hkaSkeleton`: el
    ''' archivo DICE cuales son. Seis sitios del arbol hacian
    ''' `GetObjectsByClassName("hkaSkeleton").FirstOrDefault()` — o sea el primer BLOQUE de esa
    ''' clase que aparece en el packfile. El orden de los bloques no es una ley: es como
    ''' quedaron serializados, y con dos esqueletos elegir el primero es tirar una moneda.</para>
    ''' <para>Si el archivo no trae contenedor —pasa en los `.hkx` de esqueleto suelto, que son
    ''' un `hkaSkeleton` y nada mas— se cae al barrido por clase, que es lo que habia. Esa
    ''' rama es una AUSENCIA CONOCIDA del archivo, no una preferencia.</para>
    ''' <para>⛔ EL RECORRIDO ES <see cref="EsqueletosDeclarados"/>, NO UNA COPIA. Aca habia el
    ''' mismo bucle —contenedor primero, barrido por clase si no hay— escrito una segunda vez, y con
    ''' `.Read` directo en vez de `Leer(Of T)`.</para>
    ''' </summary>
    Public Function Esqueletos() As List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
        ' ⛔ EL TIPO LO DECLARA EL CAMPO, NO EL BLOQUE — por eso `.Read` y no `Leer(Of T)`.
        ' `hkaAnimationContainer.skeletons` esta declarado `array of hkaSkeleton` en la reflexion: el
        ' archivo YA dijo que son esqueletos, y no hay ninguna subclase que resolver. Volver a
        ' preguntarle el `ClassName` al bloque solo puede PERDER: un `.hkx` cuya seccion
        ' `__classnames__` corte antes de esa entrada deja `ClassName = ""` (ParseClassNames sale por
        ' padding o por `signature = &HFFFFFFFF`) y la lista saldria VACIA sin que nada falle.
        ' En el respaldo por clase la situacion es la inversa —ahi el `ClassName` es COMO se encontro
        ' el bloque—, y el mismo `.Read` sirve para las dos ramas.
        Return EsqueletosDeclarados({Havok.Canon.Objects.HkObj_HkaSkeleton.NombreDeClase}).
            Select(Function(b) Havok.Canon.Objects.HkObj_HkaSkeleton.Read(Me, b)).
            Where(Function(s) s IsNot Nothing).ToList()
    End Function

    ''' <summary>
    ''' ⛔ EL PREDICADO «este es el de RAGDOLL», UNA SOLA VEZ. Medido sobre los 48 esqueletos del
    ''' juego: el de ragdoll SIEMPRE lleva 'Ragdoll' en el nombre y hay exactamente uno que no.
    ''' Un esqueleto sin nombre NO es ragdoll (es el caso del `.hkx` de esqueleto suelto).
    ''' </summary>
    Public Shared Function EsRagdoll(s As Havok.Canon.Objects.HkObj_HkaSkeleton) As Boolean
        Return s IsNot Nothing AndAlso EsRagdoll(s.Name)
    End Function

    ''' <summary>Idem, cuando el consumidor ya solo tiene el nombre.</summary>
    Public Shared Function EsRagdoll(nombre As String) As Boolean
        If String.IsNullOrEmpty(nombre) Then Return False
        Return nombre.IndexOf("Ragdoll", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    ''' <summary>
    ''' Los esqueletos declarados que se pueden USAR: con huesos y con una pose de referencia que los
    ''' cubra. Uno con `referencePose` mas corta que `bones` no permite componer una world: es un
    ''' archivo roto, no una variante.
    ''' </summary>
    Public Function EsqueletosUsables() As List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
        Return Esqueletos().Where(Function(s) s IsNot Nothing AndAlso
                                              s.Bones IsNot Nothing AndAlso s.Bones.Count > 0 AndAlso
                                              s.ParentIndices IsNot Nothing AndAlso
                                              s.ReferencePose IsNot Nothing AndAlso
                                              s.ReferencePose.Count >= s.Bones.Count).ToList()
    End Function

    ''' <summary>
    ''' ⛔⛔ EL ESQUELETO DE ANIMACION DEL ARCHIVO. UNA SOLA LEY EN TODO EL ARBOL.
    ''' <para>Un `skeleton.hkx` de FO4 declara DOS: el de animacion y el de RAGDOLL. El de ragdoll
    ''' SIEMPRE lleva 'Ragdoll' en el nombre — medido sobre los 48 esqueletos del juego
    ''' ('Ragdoll_NPC COM', 'Ragdoll_COM'...) — y hay exactamente UNO que no lo lleva, cuyo nombre si
    ''' varia ('Root', 'Root [Root]', 'Dogmeat_Root'). La regla EXACTA es esa, no el conteo de huesos.</para>
    ''' <para>Sale de <see cref="Esqueletos"/>, o sea del orden que DECLARA el contenedor. Las SEIS
    ''' copias que habia partian de `GetObjectsByClassName("hkaSkeleton")`, que es el orden en que
    ''' quedaron serializados los bloques: con dos esqueletos, eso es tirar una moneda.</para>
    ''' <param name="preferido">Desempate opcional del consumidor — p.ej. "su root existe en el NIF
    ''' vivo". Si ninguno lo cumple, cae a la regla del nombre.</param>
    ''' </summary>
    Public Function EsqueletoDeAnimacion(Optional preferido As Func(Of Havok.Canon.Objects.HkObj_HkaSkeleton, Boolean) = Nothing) _
            As Havok.Canon.Objects.HkObj_HkaSkeleton
        Dim usables = EsqueletosUsables()
        If usables.Count = 0 Then Return Nothing
        If preferido IsNot Nothing Then
            Dim p = usables.FirstOrDefault(preferido)
            If p IsNot Nothing Then Return p
        End If
        ' ⛔ SIN RESPALDO AL RAGDOLL. Si el archivo no declara ninguno que no sea ragdoll, la
        ' respuesta es "no hay esqueleto de animacion" — devolver el ragdoll hace que el llamador
        ' mergee huesos de ragdoll en el esqueleto vivo creyendo que son los de animacion. De los
        ' consumidores que esta ley reemplazo, UNO SOLO tenia ese respaldo; los otros cortaban.
        Return usables.FirstOrDefault(Function(s) Not EsRagdoll(s))
    End Function

    ''' <summary>El esqueleto del archivo: el PRIMERO QUE EL CONTENEDOR DECLARA, no el primer
    ''' bloque. Nothing si el archivo no trae ninguno.</summary>
    Public Function EsqueletoPrincipal() As Havok.Canon.Objects.HkObj_HkaSkeleton
        Dim e = Esqueletos()
        If e.Count = 0 Then Return Nothing
        Return e(0)
    End Function

    Public Function GetRootObject() As HkxVirtualObjectGraph_Class
        If IsNothing(Packfile.RootObject) Then Return Nothing
        Return GetObject(Packfile.RootObject.RelativeOffset)
    End Function
    Public Function TryGetLocalFixup(sourceRelativeOffset As Integer, ByRef result As HkxLocalFixupEntry_Class) As Boolean
        Return _localFixupsBySource.TryGetValue(sourceRelativeOffset, result)
    End Function

    Public Function TryGetGlobalFixup(sourceRelativeOffset As Integer, ByRef result As HkxGlobalFixupEntry_Class) As Boolean
        Return _globalFixupsBySource.TryGetValue(sourceRelativeOffset, result)
    End Function

    Public Function GetLocalFixupsInRange(relativeOffset As Integer, byteCount As Integer) As List(Of HkxLocalFixupEntry_Class)
        Return FixupsEnRango(_localFixupsSorted, _localFixupSourcesSorted, relativeOffset, byteCount)
    End Function

    Public Function GetGlobalFixupsInRange(relativeOffset As Integer, byteCount As Integer) As List(Of HkxGlobalFixupEntry_Class)
        Return FixupsEnRango(_globalFixupsSorted, _globalFixupSourcesSorted, relativeOffset, byteCount)
    End Function

    ''' <summary>Los fixups cuyo origen cae en [offset, offset+bytes). Un solo cuerpo: los dos de
    ''' arriba eran verbatim.</summary>
    Private Shared Function FixupsEnRango(Of T)(ordenados As T(), fuentes As Integer(),
                                                relativeOffset As Integer, byteCount As Integer) As List(Of T)
        Dim result As New List(Of T)
        If byteCount <= 0 OrElse fuentes Is Nothing Then Return result
        Dim rangeEnd = relativeOffset + byteCount
        For i = LowerBound(fuentes, relativeOffset) To fuentes.Length - 1
            If fuentes(i) >= rangeEnd Then Exit For
            result.Add(ordenados(i))
        Next
        Return result
    End Function

    Public Function ResolveLocalPointer(sourceRelativeOffset As Integer) As Integer?
        ' ⛔ LA RANURA SE MARCA HAYA FIXUP O NO. Justamente la que NO lo tiene es la que el censo
        ' no podia ver derivandola de la tabla, y es la mitad del faltante.
        If _ranuras IsNot Nothing Then MarcarRanura(sourceRelativeOffset)
        Dim fixup As HkxLocalFixupEntry_Class = Nothing
        If Not TryGetLocalFixup(sourceRelativeOffset, fixup) Then Return Nothing
        Return fixup.DestinationRelativeOffset
    End Function

    Public Function ResolveGlobalObject(sourceRelativeOffset As Integer) As HkxVirtualObjectGraph_Class
        If _ranuras IsNot Nothing Then MarcarRanura(sourceRelativeOffset)
        Dim fixup As HkxGlobalFixupEntry_Class = Nothing
        If Not TryGetGlobalFixup(sourceRelativeOffset, fixup) Then Return Nothing
        Return GetObject(fixup.TargetRelativeOffset)
    End Function

    Public Function ResolveLocalString(sourceRelativeOffset As Integer) As String
        Dim destination = ResolveLocalPointer(sourceRelativeOffset)
        If Not destination.HasValue Then Return String.Empty
        Return ReadNullTerminatedString(destination.Value)
    End Function

    Public Function ReadNullTerminatedString(relativeOffset As Integer) As String
        Dim absoluteOffset = _ancla + relativeOffset
        If absoluteOffset < _ancla OrElse absoluteOffset >= _fin Then Return String.Empty

        Dim endOffset = absoluteOffset
        While endOffset < _fin AndAlso _bytes(endOffset) <> 0
            endOffset += 1
        End While

        ' ⛔ ESTA NO PASA POR `EnsureReadable`: camina bytes hasta el NUL. Se marca el texto Y el
        ' terminador, que tambien es parte del dato.
        ' ⛔ EL `+ 1` ES EL TERMINADOR, Y SOLO EXISTE SI LO HUBO. Cuando la cadena llega al fin
        ' de la seccion sin NUL, `endOffset` YA es `_fin` y sumarle uno marcaba como leido el primer byte
        ' de la tabla de fixups, que no es dato.
        Marcar(absoluteOffset, Math.Min(endOffset + 1, _fin) - absoluteOffset)
        Return Encoding.ASCII.GetString(_bytes, absoluteOffset, endOffset - absoluteOffset)
    End Function

    Public Function ReadInt16(relativeOffset As Integer) As Short
        EnsureReadable(relativeOffset, 2)
        Return BitConverter.ToInt16(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadByte(relativeOffset As Integer) As Byte
        EnsureReadable(relativeOffset, 1)
        Return _bytes(_ancla + relativeOffset)
    End Function

    Public Function ReadInt32(relativeOffset As Integer) As Integer
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToInt32(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadUInt32(relativeOffset As Integer) As UInteger
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToUInt32(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadSingle(relativeOffset As Integer) As Single
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToSingle(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadBytes(relativeOffset As Integer, byteCount As Integer) As Byte()
        If byteCount <= 0 Then Return Array.Empty(Of Byte)()
        EnsureReadable(relativeOffset, byteCount)

        Dim result(byteCount - 1) As Byte
        Buffer.BlockCopy(_bytes, _ancla + relativeOffset, result, 0, byteCount)
        Return result
    End Function

    Public Function ReadArrayHeader(fieldRelativeOffset As Integer) As HkxObjectArrayHeader_Class
        Dim pointer = ResolveLocalPointer(fieldRelativeOffset)
        Return New HkxObjectArrayHeader_Class With {
            .DataRelativeOffset = If(pointer, -1),
            .Count = ReadInt32(fieldRelativeOffset + PointerSizeValue),
            .CapacityAndFlags = ReadInt32(fieldRelativeOffset + PointerSizeValue + 4)
        }
    End Function

    Public Function ReadObjectReferenceArray(fieldRelativeOffset As Integer) As List(Of HkxVirtualObjectGraph_Class)
        Dim result As New List(Of HkxVirtualObjectGraph_Class)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        Dim stride = PointerSizeValue
        For i = 0 To header.Count - 1
            Dim obj = ResolveGlobalObject(header.DataRelativeOffset + (i * stride))
            If Not IsNothing(obj) Then result.Add(obj)
        Next

        Return result
    End Function

    Private Sub EnsureReadable(relativeOffset As Integer, byteCount As Integer)
        Dim dataRelativeEnd = _fin - _ancla
        If relativeOffset < 0 OrElse byteCount < 0 OrElse relativeOffset + byteCount > dataRelativeEnd Then
            Throw New InvalidDataException($"Requested HKX range is out of bounds: offset=0x{relativeOffset:X} size={byteCount}.")
        End If
        ' ⛔ EL EMBUDO. Toda lectura pasa por aca declarando offset y largo, asi que marcar aca es
        ' marcar exactamente lo que se lee, sin repetir en ningun lado la ley de cuanto mide cada tipo.
        ' ⛔⛔ LA GUARDA VA EN EL LLAMADOR. `Marcar` sale sola si `_tocados` es Nothing, pero esta es la
        ' funcion por la que pasan TODAS las lecturas del parser: con la guarda adentro queda una llamada
        ' por lectura en el camino normal, que es el de render y bake. Aca no queda ni eso.
        If _tocados IsNot Nothing Then Marcar(_ancla + relativeOffset, byteCount)
    End Sub

    ' Todas las strings ASCII imprimibles referenciadas por local-fixups dentro del objeto.
    ''' <summary>
    ''' Todos los strings que un bloque referencia por local-fixup. Es utilidad del GRAFO — recorre
    ''' los fixups del packfile, no campos de una clase — y por eso es publica: la usan consumidores
    ''' que antes la recibian precocinada dentro de un wrapper (`AllStrings`, `Strings`).
    ''' </summary>
    Public Function ReadAllReferencedStrings(source As HkxVirtualObjectGraph_Class) As List(Of String)
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


    ''' <summary>El `name` de CUALQUIER nodo de behavior. Va por el lector tipado de la clase
    ''' BASE `hkbNode` a proposito: esto corre sobre bloques de cualquier clase derivada, incluidas
    ''' las `BS*` de Bethesda, y el objeto `HkObj_*` existe por clase CONCRETA.</summary>
    Public Function ReadNodeName(obj As HkxVirtualObjectGraph_Class) As String
        If IsNothing(obj) Then Return ""
        Return If(Havok.Canon.Objects.HkObj_HkbNode.Read(Me, obj)?.Name, String.Empty)
    End Function
    ''' <summary>Resumen "qué reproduce" un generador, recursando los wrappers (Fase 3a) hasta los
    ''' clips/behaviors/gamebryo reales. Sigue refs cuya clase sea generador; SM anidada = hoja "sm:".</summary>
    Public Function DescribeGenerator(gen As HkxVirtualObjectGraph_Class) As String
        If IsNothing(gen) Then Return ""
        Dim leaves As New List(Of String)
        CollectGeneratorLeaves(gen, leaves, New HashSet(Of Integer), 0)
        If leaves.Count = 0 Then Return gen.ClassName & " '" & ReadNodeName(gen) & "'"
        Dim distinct = leaves.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        ' ⛔ SIN LITERAL: "¿este bloque declara `hkbClipGenerator`?" es exactamente lo que contesta
        ' `Leer(Of T)`, que deriva el nombre del tipo con la regla del generador.
        ' ⛔ CORTOCIRCUITADO: `AndAlso` no evalua la derecha si la izquierda es False. Estaba en un
        ' `Dim` de arriba, asi que la lectura reflexiva corria y se tiraba en todo generador con mas
        ' de una hoja distinta — que es el caso comun de un blender o un selector.
        If distinct.Count = 1 AndAlso
           Havok.Canon.Objects.HkObj_HkbClipGenerator.Leer(Me, gen) IsNot Nothing Then Return distinct(0)
        Return gen.ClassName & " → [" & String.Join(", ", distinct) & "]"
    End Function

    ''' <summary>
    ''' Las hojas (clip/behavior/gamebryo/sm) alcanzables siguiendo refs de generador.
    ''' <para>⛔ SIN UN SOLO NOMBRE DE CLASE A MANO. Cada rama era `cn.Equals("...")` + `.Read()`, o
    ''' sea el `Leer` de cada clase generada escrito a mano cuatro
    ''' veces: el nombre lo deriva el generador del propio tipo. Si el bloque no declara esa clase,
    ''' `Leer` devuelve Nothing y se prueba la siguiente; si ninguna matchea es un envoltorio
    ''' (modifier/blender/child/selector/poseMatching/layer/…) y se siguen sus refs.</para>
    ''' <para>⛔ Y la rama de Bethesda estaba MAL: leia `BGSGamebryoSequenceGenerator` con el objeto de
    ''' `hkbBehaviorReferenceGenerator` "porque no esta en la reflexion". MEDIDO: SI esta, en las dos
    ''' tablas (`BGSGamebryoSequenceGenerator|hkbGenerator|…|pSequence,88,cstring` en FO4, `…,48,…` en
    ''' SSE), y el generador emitio `HkObj_BGSGamebryoSequenceGenerator`. El campo que se leia caia en
    ''' el mismo offset por herencia de `hkbGenerator`, pero declarado `stringptr` y no `cstring`.</para>
    ''' </summary>
    Private Sub CollectGeneratorLeaves(gen As HkxVirtualObjectGraph_Class, leaves As List(Of String), visited As HashSet(Of Integer), depth As Integer)
        If IsNothing(gen) OrElse depth > 8 OrElse Not visited.Add(gen.RelativeOffset) Then Return

        Dim clip = Havok.Canon.Objects.HkObj_HkbClipGenerator.Leer(Me, gen)
        If clip IsNot Nothing Then
            leaves.Add("clip:" & If(clip.AnimationName, ""))
            Return
        End If
        Dim beh = Havok.Canon.Objects.HkObj_HkbBehaviorReferenceGenerator.Leer(Me, gen)
        If beh IsNot Nothing Then
            leaves.Add("behavior:" & If(beh.BehaviorName, ""))
            Return
        End If
        Dim seq = Havok.Canon.Objects.HkObj_BGSGamebryoSequenceGenerator.Leer(Me, gen)
        If seq IsNot Nothing Then
            leaves.Add("gamebryo:" & If(seq.PSequence, ""))
            Return
        End If
        Dim sm = Havok.Canon.Objects.HkObj_HkbStateMachine.Leer(Me, gen)
        If sm IsNot Nothing Then
            leaves.Add("sm:" & If(sm.Name, ""))   ' SM anidada: no expandir
            Return
        End If

        For Each gf In GetGlobalFixupsInRange(gen.RelativeOffset, gen.Size)
            Dim tgt = GetObject(gf.TargetRelativeOffset)
            If tgt IsNot Nothing AndAlso IsGeneratorClass(tgt.ClassName) Then
                CollectGeneratorLeaves(tgt, leaves, visited, depth + 1)
            End If
        Next
    End Sub

    ''' <summary>
    ''' ⛔ LO DECIDE LA HERENCIA QUE DECLARA LA REFLEXION, NO EL NOMBRE.
    ''' <para>Antes esto era `nombre contiene "Generator"` mas un caso especial para
    ''' `hkbStateMachine`. Medido contra la union de las dos tablas: 40 clases derivan de
    ''' `hkbGenerator` y 58 contienen la palabra. La regla por nombre se perdia
    ''' `hkbBehaviorGraph` y los cinco `*TransitionEffect` (generadores de verdad, sin la
    ''' palabra en el nombre) y contaba como generadores 25 estructuras de estado interno.</para>
    ''' <para>Si el archivo es de un juego sin tabla (Skyrim32) no hay herencia que consultar y
    ''' la respuesta es False, igual que para cualquier clase que la tabla no declare.</para>
    ''' </summary>
    Private Function IsGeneratorClass(className As String) As Boolean
        Dim lay = Havok.Canon.HavokLayout.ForGraph(Me)
        If lay Is Nothing Then Return False
        Return lay.DerivaDe(className, Havok.Canon.Objects.HkObj_HkbGenerator.NombreDeClase)
    End Function

End Class

Public Class HkxVirtualObjectGraph_Class
    Public Property SectionIndex As Integer
    Public Property RelativeOffset As Integer
    Public Property ClassName As String
    Public Property Size As Integer
End Class

Public Class HkxObjectArrayHeader_Class
    Public Property DataRelativeOffset As Integer
    Public Property Count As Integer
    Public Property CapacityAndFlags As Integer
End Class

