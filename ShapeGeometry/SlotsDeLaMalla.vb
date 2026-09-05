Imports NiflySharp.Blocks

''' <summary>Sede ÚNICA de la pregunta «¿qué biped slots DECLARA este archivo de malla?». La respuesta
''' sale del ARCHIVO y es distinta por juego, porque los dos motores leen cosas distintas: Skyrim las
''' particiones de <c>BSDismemberSkinInstance</c> (el hider es per-partición, <c>ApplyOcclusionToGeometry
''' 0x1403C56B0</c>) y Fallout 4 los biped objects de los segmentos de <c>BSSubIndexTriShape</c> (el
''' resolver per-slot es <c>0x14035E3B0</c>).
''' <para>⛔ Nace porque la MISMA aritmética estaba escrita en CUATRO lugares: las dos funciones privadas
''' del colector de mallas, el resolvedor de material (por shape) y el probe de medición. Acá el plegado
''' sigue delegando en <see cref="BipedSlots.FoldPartitionBodyPart"/> —la ley del plegado NO se re-enuncia,
''' ni en código ni en prosa— y el recorte a [30,61] queda una sola vez.</para>
''' <para>⛔ Es <c>Class</c> con miembros <c>Shared</c> y no <c>Module</c> a propósito: los tres proyectos
''' que consumen esta librería la importan a nivel de PROYECTO, y los miembros de un Public Module quedan
''' visibles SIN calificar en todos sus archivos. <c>ModoDeLectura</c> y <c>ConteoDeSlots</c> son nombres
''' demasiado genéricos para soltarlos al espacio global de tres proyectos.</para></summary>
Public Class SlotsDeLaMalla

    ''' <summary>Qué lector corresponde. Se pasa EXPLÍCITO y no se deriva de
    ''' <c>Config_App.Current.Game</c> adentro: el default de esa propiedad es Skyrim, así que un camino
    ''' que no la fije leería el archivo con el layout del otro juego sin que nadie se entere. Quien sabe
    ''' de qué juego se trata es el llamador, y lo dice.</summary>
    Public Enum ModoDeLectura
        ''' <summary>Skyrim: particiones de BSDismemberSkinInstance, plegadas.</summary>
        ParticionesDismember = 0
        ''' <summary>Fallout 4: biped objects de los segmentos de BSSubIndexTriShape. ⛔ FO4 NO pliega.</summary>
        SegmentosBiped = 1
    End Enum

    ''' <summary>El lector que le corresponde a cada juego. Sede única del mapeo juego → lector.</summary>
    ''' <param name="juego">El juego de la sesión, resuelto por el llamador.</param>
    Public Shared Function LecturaDelJuego(juego As Config_App.Game_Enum) As ModoDeLectura
        Return If(juego = Config_App.Game_Enum.Skyrim, ModoDeLectura.ParticionesDismember, ModoDeLectura.SegmentosBiped)
    End Function

    ''' <summary>Lo que declara una malla: la máscara (bit i = biped 30+i) MÁS los dos contadores que
    ''' distinguen «no declara nada» de «declara y todo cae fuera de [30,61]».
    ''' <para>⛔ Esa distinción NO es cosmética: la consecuencia de caer fuera de banda es OPUESTA en los
    ''' dos motores —Skyrim lo oculta y Fallout 4 lo deja visible— y cada una ya tiene su sede
    ''' (<c>Nifcontent_Class_Manolo.ParticionOculta</c> con <c>hideOutOfBand</c>, y
    ''' <c>BSTriShapeGeometry.SegmentoOculto</c>). Quien quiera la consecuencia le pregunta a esas dos, no
    ''' la re-escribe.</para></summary>
    Public Structure ConteoDeSlots
        ''' <summary>Bit i = biped slot 30+i. Sólo entra lo que cae en [30,61].</summary>
        Public Mascara As UInteger
        ''' <summary>Cuántas particiones (SSE) o tags de segmento (FO4) se miraron, en banda o no.</summary>
        Public Declaraciones As Integer
        ''' <summary>Cuántas de ésas cayeron FUERA de [30,61] y por lo tanto no tocaron la máscara.</summary>
        Public FueraDeBanda As Integer
    End Structure

    ''' <summary>Átomo de Skyrim: lo que declara UNA skin instance con particiones. Contrato:
    ''' <paramref name="dism"/> nulo (o sin particiones) devuelve el conteo en cero — nunca tira, porque el
    ''' llamador del render lo invoca por shape dentro del loop de material y una excepción ahí se lleva
    ''' puesto el render del NPC entero.</summary>
    ''' <param name="dism">La BSDismemberSkinInstance de la shape, o Nothing.</param>
    Public Shared Function DeParticiones(dism As BSDismemberSkinInstance) As ConteoDeSlots
        Dim r As ConteoDeSlots = Nothing
        If dism Is Nothing OrElse dism.Partitions Is Nothing Then Return r
        For Each p In dism.Partitions
            r.Declaraciones += 1
            ' Ley del plegado: BipedSlots.FoldPartitionBodyPart (una sola sede). El filtro [30,61] es de
            ' ESTA sede, no de la ley — ver su doc.
            Dim v = BipedSlots.FoldPartitionBodyPart(CInt(p.BodyPart))
            If v >= 30 AndAlso v <= 61 Then
                r.Mascara = r.Mascara Or (1UI << (v - 30))
            Else
                r.FueraDeBanda += 1
            End If
        Next
        Return r
    End Function

    ''' <summary>Átomo de Fallout 4: lo que declaran los segmentos de UNA shape. ⛔ Acá NO se pliega: el
    ''' plegado de las bandas 130/230 es de Skyrim, y en FO4 un tag 160 es un tag 160. Los biped objects
    ''' salen de <see cref="BSTriShapeGeometry.GetBipedObjects"/>, que es la sede de ese barrido. Mismo
    ''' contrato de nulo que el átomo de particiones.</summary>
    ''' <param name="subIdx">La BSSubIndexTriShape, o Nothing.</param>
    Public Shared Function DeSegmentos(subIdx As BSSubIndexTriShape) As ConteoDeSlots
        Dim r As ConteoDeSlots = Nothing
        If subIdx Is Nothing Then Return r
        For Each tag In BSTriShapeGeometry.GetBipedObjects(subIdx)
            r.Declaraciones += 1
            If tag >= 30UI AndAlso tag <= 61UI Then
                r.Mascara = r.Mascara Or (1UI << CInt(tag - 30UI))
            Else
                r.FueraDeBanda += 1
            End If
        Next
        Return r
    End Function

    ''' <summary>Lo que declara un ARCHIVO de malla entero, sumando todas sus shapes con el lector que
    ''' corresponda. <c>Nothing</c> significa «no se pudo leer» y es distinto de un conteo en cero, que
    ''' significa «se leyó y no declara nada»: los dos estados le dicen cosas distintas al usuario.
    ''' <para>La clave tiene que venir ya normalizada por la sede de normalización
    ''' (<c>MeshPathHelpers.NormalizeMeshKey</c> / <c>FO4UnifiedMaterial_Class.CorrectMeshPath</c>); acá no
    ''' se normaliza para no abrir una segunda ley de rutas.</para></summary>
    ''' <param name="meshKey">Clave normalizada de FilesDictionary.</param>
    ''' <param name="modo">Qué lector usar; sale de <see cref="LecturaDelJuego"/>.</param>
    Public Shared Function DeLaMalla(meshKey As String, modo As ModoDeLectura) As ConteoDeSlots?
        If String.IsNullOrEmpty(meshKey) Then Return Nothing
        Try
            Dim bytes = FilesDictionary_class.GetBytes(meshKey)
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing
            Dim nif As New Nifcontent_Class_Manolo()
            nif.Load_Manolo(bytes)
            Dim total As ConteoDeSlots = Nothing
            For Each shp In nif.GetShapes()
                Dim parcial As ConteoDeSlots
                If modo = ModoDeLectura.ParticionesDismember Then
                    parcial = DeParticiones(TryCast(nif.GetBlock(Of NiSkinInstance)(shp.SkinInstanceRef), BSDismemberSkinInstance))
                Else
                    parcial = DeSegmentos(TryCast(shp, BSSubIndexTriShape))
                End If
                total.Mascara = total.Mascara Or parcial.Mascara
                total.Declaraciones += parcial.Declaraciones
                total.FueraDeBanda += parcial.FueraDeBanda
            Next
            Return total
        Catch ex As Exception
            ' Malla ilegible / bloques desconocidos. Nothing, NO cero: el llamador decide qué decir.
            Return Nothing
        End Try
    End Function

End Class
